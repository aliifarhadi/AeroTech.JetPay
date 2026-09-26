using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class EventTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A17_Session_changes_carry_a_strictly_monotonic_version_and_replays_add_nothing()
    {
        var command = CreateCommand("create-1");
        var session = await _harness.SendAsync(command);
        var selected = await _harness.SelectAsync(session, TenderType.IranianPgw, "select-1");
        var intent = selected.Intents.Single();
        var paid = await _harness.CompleteAsync(intent.Id);

        var changes = _harness.ChangesOf(session.Id);

        Assert.Equal(
            [PaymentSessionStatus.RequiresPaymentMethod, PaymentSessionStatus.Processing, PaymentSessionStatus.Paid],
            changes.Select(change => change.Status));
        Assert.Equal(Enumerable.Range(1, changes.Count).Select(version => (long)version), changes.Select(change => change.Version));
        Assert.Equal(paid.Version, changes[^1].Version);
        Assert.Equal(changes.Count, changes.Select(change => change.EventId).Distinct().Count());

        await _harness.SendAsync(command);
        await _harness.SelectAsync(session, TenderType.IranianPgw, "select-1");
        await _harness.CallbackAsync(intent.Id);

        Assert.Equal(changes.Count, _harness.ChangesOf(session.Id).Count);
    }

    [Fact]
    public async Task A17_A_consumer_keeping_the_highest_version_is_safe_against_duplicates_and_reordering()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 2_000_000m, isDefault: true);
        var session = await _harness.CreateAgencyDefaultAsync();
        var changes = _harness.ChangesOf(session.Id);

        var delivered = changes.Concat(changes).Reverse().ToList();
        var projection = delivered.Aggregate((PaymentSessionChanged?)null, (current, change) => current is null || change.Version > current.Version ? change : current)!;

        Assert.Equal(PaymentSessionStatus.Paid, projection.Status);
        Assert.Equal(session.Version, projection.Version);
    }

    [Fact]
    public async Task Session_changes_carry_the_outstanding_amount_and_the_assurance_requirement()
    {
        _harness.Wallet(PayerType.Agency, AgencyId, 2_000_000m, isDefault: true);
        var session = await _harness.CreateAgencyDefaultAsync();

        var last = _harness.ChangesOf(session.Id)[^1];

        Assert.Equal(session.PayableInstructionId, last.PayableInstructionId);
        Assert.Equal((session.OrderId, session.CommercialVersion, session.Purpose), (last.OrderId, last.CommercialVersion, last.Purpose));
        Assert.Equal(PaymentAssuranceRequirement.IssuanceGuaranteed, last.AssuranceRequirement);
        Assert.Equal((Amount, Amount, Amount, 0m, Irr), (last.RequiredAmount, last.GuaranteedAmount, last.CapturedAmount, last.OutstandingAmount, last.CurrencyId));
        Assert.Equal(session.Id, last.AggregateId);
        Assert.Equal(last.OccurredAt, last.TimeOfOccurrence);
    }
}
