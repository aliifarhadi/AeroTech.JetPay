using System.Text.Json;
using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Consumers.Outbox;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using Xunit;
using static AeroTech.JetPay.Application.AcceptanceTests.Fixtures.PaymentHarness;

namespace AeroTech.JetPay.Application.AcceptanceTests.Scenarios;

public sealed class EventTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task Every_session_change_publishes_a_snapshot_with_a_monotonic_version()
    {
        var session = (await _harness.CreateAsync()).Session;
        var leg = Leg(await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)), TenderType.IranianPgw);
        var paid = await _harness.CompleteAsync(leg.Id);

        var changes = _harness.ChangesOf(session.Id);
        var last = changes[^1];

        Assert.Equal(Enumerable.Range(1, changes.Count).Select(version => (long)version), changes.Select(change => change.Version));
        Assert.Equal(changes.Count, changes.Select(change => change.EventId).Distinct().Count());
        Assert.Equal(
            (paid.Session.Version, paid.Session.Status, paid.Session.GuaranteedAmount, paid.Session.CapturedAmount, paid.Session.PayableInstructionId, paid.Session.UpdatedAt),
            (last.Version, last.Status, last.GuaranteedAmount, last.CapturedAmount, last.PayableInstructionId, last.OccurredAt));
    }

    [Fact]
    public async Task S34_Duplicate_and_out_of_order_events_do_not_double_count()
    {
        var session = (await _harness.CreateAsync()).Session;
        var leg = Leg(await _harness.FundAsync(session, null, (TenderType.IranianPgw, Amount)), TenderType.IranianPgw);
        var paid = await _harness.CompleteAsync(leg.Id);
        var changes = _harness.ChangesOf(session.Id);

        IEnumerable<PaymentSessionChanged> delivered = [changes[^1], changes[1], changes[^1], changes[0], changes[2], changes[^1]];
        var seen = new HashSet<string>();
        PaymentSessionChanged? applied = null;

        foreach (var change in delivered)
        {
            if (!seen.Add(change.EventId) || change.Version <= (applied?.Version ?? 0))
                continue;

            applied = change;
        }

        Assert.Equal(paid.Session.Version, applied!.Version);
        Assert.Equal(paid.Session.CapturedAmount, applied.CapturedAmount);
        Assert.Equal(Amount, applied.CapturedAmount);
    }

    [Fact]
    public async Task A_republished_event_keeps_its_transport_identity()
    {
        await _harness.CreateAsync();
        var created = _harness.Outbox.Written.OfType<PaymentSessionChanged>().Single();

        var republished = JsonSerializer.Deserialize<PaymentSessionChanged>(JsonSerializer.Serialize(created))!;

        Assert.NotNull(OutboxMessageIdentity.Of(created));
        Assert.Equal(OutboxMessageIdentity.Of(created), OutboxMessageIdentity.Of(republished));
    }

    [Fact]
    public async Task Replays_reads_and_rejected_verifies_publish_nothing()
    {
        _harness.Wallet(PayerType.Customer, CustomerId, Amount);
        var session = (await _harness.CreateAsync()).Session;
        IReadOnlyList<Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession.PaymentSelection> plan =
            [new(OptionOf(await _harness.OptionsFor(session), TenderType.StoredValue), Amount)];
        var paid = await _harness.ConfirmAsync(session.Id, plan, "confirm-1");
        var published = _harness.Outbox.Written.Count;

        await _harness.ConfirmAsync(session.Id, plan, "confirm-1");
        await _harness.GetAsync(session.Id);
        await AssertRejectedAsync(8041, () => _harness.ReconcileAsync(Leg(paid, TenderType.StoredValue).Id));

        Assert.Equal(published, _harness.Outbox.Written.Count);
    }

    public void Dispose() => _harness.Dispose();
}
