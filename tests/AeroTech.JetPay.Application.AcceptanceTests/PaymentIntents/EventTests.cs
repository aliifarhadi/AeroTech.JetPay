using System.Text.Json;
using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Consumers.Outbox;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.JetPay.IntegrationEvents.V1;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.PaymentIntents;

public sealed class EventTests : IDisposable
{
    private readonly PaymentHarness _harness = new();

    [Fact]
    public async Task Every_durable_change_publishes_a_snapshot_with_a_monotonic_version()
    {
        var intent = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);
        var captured = await _harness.CompleteCustomerActionAsync(intent.Id);

        var changes = _harness.ChangesOf(intent.Id);

        Assert.Equal([1L, 2L, 3L, 4L], changes.Select(change => change.Version));
        Assert.Equal(changes.Count, changes.Select(change => change.EventId).Distinct().Count());
        Assert.All(changes, change => Assert.False(string.IsNullOrWhiteSpace(change.EventId)));

        var last = changes[^1];
        Assert.Equal(captured.Version, last.Version);
        Assert.Equal(captured.Status, last.Status);
        Assert.Equal(captured.PayableInstructionId, last.PayableInstructionId);
        Assert.Equal(captured.OrderId, last.OrderId);
        Assert.Equal(captured.CommercialVersion, last.CommercialVersion);
        Assert.Equal(captured.RequestedAmount, last.RequestedAmount);
        Assert.Equal(captured.GuaranteedAmount, last.GuaranteedAmount);
        Assert.Equal(captured.CapturedAmount, last.CapturedAmount);
        Assert.Equal(captured.CurrencyId, last.CurrencyId);
        Assert.Equal(captured.UpdatedAt, last.OccurredAt);
    }

    [Fact]
    public async Task Event_ids_are_unique_across_intents()
    {
        await _harness.CreateAndConfirmAsync(TenderType.StoredValue, orderId: 6001);
        await _harness.CreateAndConfirmAsync(TenderType.AgencyCredit, orderId: 6002);

        var eventIds = _harness.Outbox.Written.OfType<PaymentIntentChangedV1>().Select(change => change.EventId).ToList();

        Assert.Equal(eventIds.Count, eventIds.Distinct().Count());
    }

    [Fact]
    public async Task Idempotent_replays_and_reads_publish_nothing()
    {
        var intent = await _harness.CreateForAsync(TenderType.StoredValue);
        await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue), "confirm-1");
        var published = _harness.Outbox.Written.Count;

        await _harness.ConfirmAsync(intent.Id, PaymentHarness.OptionFor(TenderType.StoredValue), "confirm-1");
        await _harness.GetAsync(intent.Id);

        Assert.Equal(published, _harness.Outbox.Written.Count);
    }

    [Fact]
    public async Task A_republished_event_keeps_its_transport_identity_so_consumers_can_deduplicate()
    {
        await _harness.CreateAndConfirmAsync(TenderType.StoredValue);
        var changes = _harness.Outbox.Written.OfType<PaymentIntentChangedV1>().ToList();
        var first = changes[0];

        var republished = JsonSerializer.Deserialize<PaymentIntentChangedV1>(JsonSerializer.Serialize(first))!;

        Assert.NotNull(OutboxMessageIdentity.Of(first));
        Assert.Equal(OutboxMessageIdentity.Of(first), OutboxMessageIdentity.Of(republished));
        Assert.Equal(first.Version, republished.Version);
        Assert.NotEqual(OutboxMessageIdentity.Of(first), OutboxMessageIdentity.Of(changes[1]));
    }

    [Fact]
    public async Task A_consumer_keeping_the_highest_version_ends_on_the_authoritative_state_under_duplication_and_reordering()
    {
        var intent = await _harness.CreateAndConfirmAsync(TenderType.IranianPgw);
        var captured = await _harness.CompleteCustomerActionAsync(intent.Id);
        var changes = _harness.ChangesOf(intent.Id);

        IEnumerable<PaymentIntentChangedV1> delivered = [changes[3], changes[1], changes[3], changes[0], changes[2], changes[1]];

        var seen = new HashSet<string>();
        PaymentIntentChangedV1? applied = null;

        foreach (var change in delivered)
        {
            if (!seen.Add(change.EventId) || change.Version <= (applied?.Version ?? 0))
                continue;

            applied = change;
        }

        Assert.Equal(captured.Version, applied!.Version);
        Assert.Equal(PaymentIntentStatus.Captured, applied.Status);
    }

    public void Dispose() => _harness.Dispose();
}
