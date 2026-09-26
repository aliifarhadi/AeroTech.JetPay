using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.Messages.JetPay.Enums;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class IdempotencyTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A10_The_same_create_key_returns_the_same_session()
    {
        var command = CreateCommand("create-1");

        var first = await _harness.SendAsync(command);
        var replay = await _harness.SendAsync(command);

        Assert.Equal(first.Id, replay.Id);
        Assert.Single(_harness.Sessions.Committed);
    }

    [Fact]
    public async Task A10_The_same_selection_key_returns_the_same_intent_and_charges_once()
    {
        var session = await _harness.CreateAsync();
        var options = await _harness.OptionsFor(session);
        var selection = new PaymentSelection(OptionOf(options, TenderType.IranianPgw), Amount);

        var first = await _harness.SelectAsync(session.Id, [selection], "select-1");
        var replay = await _harness.SelectAsync(session.Id, [selection], "select-1");

        Assert.Equal(first.Intents.Single().Id, replay.Intents.Single().Id);
        Assert.Equal(1, Assert.Single(_harness.Ledger.OperationsOf(first.Intents.Single().Id)).StartCalls);
    }

    [Fact]
    public async Task A10_Reusing_a_key_for_a_different_payload_is_rejected()
    {
        await _harness.SendAsync(CreateCommand("create-1"));
        await AssertRejectedAsync(8020, () => _harness.SendAsync(CreateCommand("create-1", amount: Amount + 1)));

        var session = await _harness.CreateAsync(orderId: 6001);
        var options = await _harness.OptionsFor(session);
        await _harness.SelectAsync(session.Id, [new PaymentSelection(OptionOf(options, TenderType.IranianPgw), Amount)], "select-1");

        await AssertRejectedAsync(8020, () => _harness.SelectAsync(session.Id, [new PaymentSelection(OptionOf(options, TenderType.IranianPgw), Amount)], "select-1", "https://shop.test/return"));
    }

    [Fact]
    public async Task A_second_key_for_the_same_payable_instruction_binds_to_the_active_session_unless_terms_differ()
    {
        var first = await _harness.SendAsync(CreateCommand("create-1"));

        var sameTerms = await _harness.SendAsync(CreateCommand("create-2"));
        Assert.Equal(first.Id, sameTerms.Id);

        await AssertRejectedAsync(8021, () => _harness.SendAsync(CreateCommand("create-3", issuerLegalEntityId: IssuerId + 1)));
    }

    [Fact]
    public async Task A11_A_crash_after_commit_is_recovered_by_replaying_the_create()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 2_000_000m, isDefault: true);
        var command = CreateCommand("agency-1", PayerType.Agency, AgencyId, PaymentInteractionMode.UnattendedApi);
        _harness.CrashBeforeNextDispatchOf(TenderType.StoredValue);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.SendAsync(command));

        var committed = await _harness.GetAsync(_harness.Sessions.Committed.Single().Id);
        Assert.Equal(PaymentSessionStatus.Processing, committed.Status);
        Assert.Equal(PaymentIntentStatus.Created, committed.Intents.Single().Status);

        var recovered = await _harness.SendAsync(command);

        Assert.Equal(committed.Id, recovered.Id);
        Assert.Equal(PaymentSessionStatus.Paid, recovered.Status);
        Assert.Single(_harness.AttemptsOf(recovered.Intents.Single().Id));
        Assert.Equal(1_000_000m, _harness.Ledger.WalletsOf(PayerType.Agency, AgencyId, Irr).Single().Balance);
    }

    [Fact]
    public async Task A11_A_crash_before_the_redirect_is_recovered_by_replaying_the_selection_on_the_same_attempt()
    {
        var session = await _harness.CreateAsync();
        var options = await _harness.OptionsFor(session);
        var selection = new PaymentSelection(OptionOf(options, TenderType.IranianPgw), Amount);
        _harness.CrashBeforeNextDispatchOf(TenderType.IranianPgw);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _harness.SelectAsync(session.Id, [selection], "select-1"));

        var read = await _harness.GetAsync(session.Id);
        Assert.Equal(PaymentIntentStatus.Created, read.Intents.Single().Status);

        var recovered = await _harness.SelectAsync(session.Id, [selection], "select-1");

        var intent = recovered.Intents.Single();
        Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, intent.Status);
        Assert.Single(_harness.AttemptsOf(intent.Id));
        Assert.Single(_harness.Ledger.OperationsOf(intent.Id));
    }
}
