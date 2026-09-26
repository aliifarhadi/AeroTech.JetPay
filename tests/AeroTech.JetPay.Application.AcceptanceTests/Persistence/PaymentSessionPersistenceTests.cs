using AeroTech.JetPay.Application.AcceptanceTests.Fakes;
using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Arguments;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Persistence.IdempotencyRecordAggregate;
using AeroTech.JetPay.Persistence.PaymentIntentAggregate;
using AeroTech.JetPay.Persistence.PaymentSessionAggregate;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Persistence;

public sealed class PaymentSessionPersistenceTests : IAsyncLifetime
{
    private readonly FixedClock _clock = new();
    private readonly SequentialIdGenerator _ids = new();
    private readonly TestDatabase _database;

    public PaymentSessionPersistenceTests() => _database = new TestDatabase(_clock);

    public Task InitializeAsync() => _database.MigrateAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task A_session_and_its_legs_round_trip_with_exact_amounts_and_customer_action()
    {
        var session = NewSession("session-1", "payable-1", 1_000_000.125m, _clock.Now.AddHours(1));
        var wallet = NewIntent(session, 1, TenderType.StoredValue, 300_000.125m, "wallet:1");
        var pgw = NewIntent(session, 2, TenderType.IranianPgw, 700_000m, null);
        var action = new CustomerAction(CustomerActionType.HtmlForm, "https://gateway.test/pay", "POST", new Dictionary<string, string> { ["token"] = "abc" }, _clock.Now.AddMinutes(15));
        wallet.ApplyStartOutcome(TenderOutcome.Captured(300_000.125m, "wallet:1|debit"), _clock.Now);
        pgw.ApplyStartOutcome(TenderOutcome.RequiresCustomerAction(action, "pgw:mock-pgw-a:token"), _clock.Now);
        session.Refresh([wallet, pgw], _ids, _clock.Now);

        await using (var context = _database.NewContext())
        {
            await new PaymentSessionRepository(context).AddAsync(session);
            await new PaymentIntentRepository(context).AddAsync(wallet);
            await new PaymentIntentRepository(context).AddAsync(pgw);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            var loaded = await new PaymentSessionRepository(context).GetAsync("session-1");
            var legs = await new PaymentIntentRepository(context).ListBySessionAsync("session-1");

            Assert.NotNull(loaded);
            Assert.Equal(new PaymentInitiatorContext(SalesChannel.BackOffice, "AirlineEmployee", 42, 501), loaded.InitiatorContext);
            Assert.Equal([wallet.Id, pgw.Id], loaded.PaymentIntentIds);
            Assert.Equal((PaymentSessionStatus.RequiresCustomerAction, 1_000_000.125m, 300_000.125m), (loaded.Status, loaded.RequiredAmount, loaded.GuaranteedAmount));
            Assert.Equal([1, 2], legs.Select(leg => leg.Sequence));
            Assert.Equal(action, legs[1].NextAction);
            Assert.Equal("pgw:mock-pgw-a:token", legs[1].ProviderReference);
        }
    }

    [Fact]
    public async Task The_expiry_queries_select_exactly_what_the_domain_would_expire()
    {
        var dueSession = NewSession("session-due", "payable-due", 100m, _clock.Now.AddMinutes(5));
        var liveSession = NewSession("session-live", "payable-live", 100m, _clock.Now.AddHours(1));
        var dueLeg = NewIntent(liveSession, 1, TenderType.IranianPgw, 100m, null);
        dueLeg.ApplyStartOutcome(TenderOutcome.RequiresCustomerAction(CustomerAction.Redirect("https://gateway.test", _clock.Now.AddMinutes(5)), "pgw:a:1"), _clock.Now);

        await using (var context = _database.NewContext())
        {
            await new PaymentSessionRepository(context).AddAsync(dueSession);
            await new PaymentSessionRepository(context).AddAsync(liveSession);
            await new PaymentIntentRepository(context).AddAsync(dueLeg);
            await context.SaveChangesAsync();
        }

        var later = _clock.Now.AddMinutes(10);

        await using (var context = _database.NewContext())
        {
            Assert.Equal(["session-due"], await new PaymentSessionRepository(context).ListDueForExpiryAsync(later, 10));
            Assert.Equal(["session-live"], await new PaymentIntentRepository(context).ListSessionsWithLegsDueForExpiryAsync(later, 10));
        }
    }

    [Fact]
    public async Task Terminal_sessions_do_not_hold_their_payable_instruction()
    {
        var cancelled = NewSession("session-cancelled", "payable-shared", 100m, null);
        cancelled.Cancel([], _ids, _clock.Now);
        var active = NewSession("session-active", "payable-shared", 100m, null);

        await using (var context = _database.NewContext())
        {
            await new PaymentSessionRepository(context).AddAsync(cancelled);
            await new PaymentSessionRepository(context).AddAsync(active);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
            Assert.Equal("session-active", (await new PaymentSessionRepository(context).FindActiveByPayableInstructionAsync("payable-shared"))?.Id);
    }

    [Fact]
    public async Task The_store_rejects_a_second_commit_under_the_same_idempotency_key()
    {
        await using (var context = _database.NewContext())
        {
            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "session-a", ["leg-1", "leg-2"]));
            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "session-b", []));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            var record = await new IdempotencyRecordRepository(context).FindAsync(IdempotentOperation.ConfirmPaymentSession, "session-a", "key-1");
            Assert.Equal(["leg-1", "leg-2"], record!.PaymentIntentIds);

            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "session-a", []));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    private PaymentSession NewSession(string id, string payableInstructionId, decimal amount, DateTimeOffset? expiresAt)
        => PaymentSession.Create(
            id,
            new CreatePaymentSessionArgs(
                payableInstructionId,
                5001,
                "ORD-5001",
                1,
                PaymentPurpose.InitialSale,
                PayerType.Customer,
                7001,
                new PaymentInitiatorContext(SalesChannel.BackOffice, "AirlineEmployee", 42, 501),
                amount,
                70,
                PaymentAssuranceRequirement.IssuanceGuaranteed,
                PaymentInteractionMode.StaffAssisted,
                expiresAt),
            _ids,
            _clock.Now);

    private PaymentIntent NewIntent(PaymentSession session, int sequence, TenderType tenderType, decimal amount, string? fundingReference)
    {
        var intent = PaymentIntent.Create(
            $"{session.Id}-leg-{sequence}",
            session.Id,
            sequence,
            new PaymentMethodOption($"opt-{tenderType}", tenderType, tenderType.ToString(), 70, null, null, null, true, false, false,
                tenderType == TenderType.IranianPgw ? CustomerActionType.Redirect : CustomerActionType.None,
                PaymentAssuranceCapability.FundsReceived, PaymentCaptureMode.Automatic, null, fundingReference),
            amount,
            70,
            _clock.Now);

        session.Attach(intent);
        return intent;
    }

    private IdempotencyRecord Record(string key, string scope, IEnumerable<string> paymentIntentIds)
        => IdempotencyRecord.Create(_ids.NewId(), IdempotentOperation.ConfirmPaymentSession, scope, key, new string('A', 64), scope, paymentIntentIds, _clock.Now);
}
