using AeroTech.JetPay.Application.AcceptanceTests.Fakes;
using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Arguments;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Persistence.IdempotencyRecordAggregate;
using AeroTech.JetPay.Persistence.PaymentSessionAggregate;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Persistence;

public sealed class PaymentSessionPersistenceTests : IAsyncLifetime
{
    private static readonly ProviderProfile Pgw = new(
        "mock-pgw-a", TenderType.IranianPgw, ProviderProfileStatus.Active, "3", [70], AmountUnit.Irr, null, null,
        SupportsInquiry: true, SupportsRefund: false, SupportsPartialRefund: false, SupportsReversal: true,
        RequiresSettlementAfterVerify: true, SupportsProviderIdempotency: true, SupportsPartialAmount: false,
        UnverifiedPaymentExpiryBehavior.AutoReverse, TimeSpan.FromMinutes(10));

    private readonly FixedClock _clock = new();
    private readonly SequentialIdGenerator _ids = new();
    private readonly TestDatabase _database;

    public PaymentSessionPersistenceTests() => _database = new TestDatabase(_clock);

    public Task InitializeAsync() => _database.MigrateAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task A18_The_issuer_legal_entity_and_the_whole_aggregate_survive_persistence_and_reload()
    {
        var session = NewSession("session-1", "payable-1", 1_000_000.125m, _clock.Now.AddHours(1));
        var intent = session.AddIntent("intent-1", Option(TenderType.IranianPgw, null), 1_000_000.125m, _ids, _clock.Now);
        var failed = session.OpenProviderAttempt(intent, 9001, Pgw, _clock.Now);
        session.RecordRouteUnavailable(intent, failed, "ProviderUnavailable", "refused", _clock.Now);
        var attempt = session.OpenProviderAttempt(intent, 9002, Pgw, _clock.Now);
        var action = new CustomerAction(CustomerActionType.HtmlForm, "https://gateway.test/pay", "POST", new Dictionary<string, string> { ["token"] = "abc" }, _clock.Now.AddMinutes(15));
        session.RecordCustomerActionRequired(intent, attempt, action, "pgw:mock-pgw-a:token", _ids, _clock.Now);
        session.RecordPaymentEvidence(intent, attempt, Pgw, _clock.Now, fromCallback: true, instructionSuperseded: false, _ids, _clock.Now.AddMinutes(1));

        await using (var context = _database.NewContext())
        {
            await new PaymentSessionRepository(context).AddAsync(session);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            var loaded = await new PaymentSessionRepository(context).GetAsync("session-1");

            Assert.NotNull(loaded);
            Assert.Equal(11, loaded.IssuerLegalEntityId);
            Assert.Equal(new PaymentInitiatorContext("AirlineEmployee", 42, SalesChannel.BackOffice, 501), loaded.Initiator);
            Assert.Equal((PaymentSelectionMode.Explicit, PaymentInteractionMode.StaffAssisted), (loaded.SelectionMode, loaded.InteractionMode));
            Assert.Equal((PaymentSessionStatus.Processing, 1_000_000.125m, 1_000_000.125m), (loaded.Status, loaded.RequiredAmount, loaded.OutstandingAmount));

            var loadedIntent = Assert.Single(loaded.Intents);
            Assert.Equal(action, loadedIntent.NextAction);
            Assert.Null(loadedIntent.FundingReference);

            var attempts = loadedIntent.ProviderAttempts.OrderBy(candidate => candidate.AttemptNumber).ToList();
            Assert.Equal([ProviderPaymentAttemptStatus.Failed, ProviderPaymentAttemptStatus.CallbackReceived], attempts.Select(candidate => candidate.Status));
            Assert.Equal(("mock-pgw-a", "3", "jetpay-attempt-9002"), (attempts[1].ProviderProfileId, attempts[1].ProviderProfileVersion, attempts[1].IdempotencyKey));
            Assert.Equal(_clock.Now.AddMinutes(1), attempts[1].CallbackReceivedAt);
            Assert.Equal(_clock.Now.AddMinutes(10), attempts[1].VerifyDeadline);
            Assert.Equal("pgw:mock-pgw-a:token", attempts[1].ProviderTransactionRef);

            Assert.Equal("session-1", await new PaymentSessionRepository(context).FindSessionIdByPaymentIntentAsync("intent-1"));
        }
    }

    [Fact]
    public async Task The_sweep_query_selects_exactly_the_sessions_the_sweep_has_work_for()
    {
        var dueSession = NewSession("session-due", "payable-due", 100m, _clock.Now.AddMinutes(5));
        var lapsedSession = NewSession("session-lapsed", "payable-lapsed", 100m, _clock.Now.AddHours(1));
        var lapsedIntent = lapsedSession.AddIntent("intent-lapsed", Option(TenderType.IranianPgw, null), 100m, _ids, _clock.Now);
        var lapsedAttempt = lapsedSession.OpenProviderAttempt(lapsedIntent, 9101, Pgw, _clock.Now);
        lapsedSession.RecordCustomerActionRequired(lapsedIntent, lapsedAttempt, CustomerAction.Redirect("https://gateway.test", _clock.Now.AddMinutes(5)), "pgw:1", _ids, _clock.Now);
        var reversalSession = NewSession("session-reversal", "payable-reversal", 100m, _clock.Now.AddHours(1));
        var reversalIntent = reversalSession.AddIntent("intent-reversal", Option(TenderType.IranianPgw, null), 100m, _ids, _clock.Now);
        var reversalAttempt = reversalSession.OpenProviderAttempt(reversalIntent, 9201, Pgw, _clock.Now);
        reversalSession.RecordProviderUnknown(reversalIntent, reversalAttempt, null, _clock.Now.AddMinutes(8), _ids, _clock.Now);
        var quietSession = NewSession("session-quiet", "payable-quiet", 100m, _clock.Now.AddHours(1));

        await using (var context = _database.NewContext())
        {
            foreach (var session in new[] { dueSession, lapsedSession, reversalSession, quietSession })
                await new PaymentSessionRepository(context).AddAsync(session);

            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            var repository = new PaymentSessionRepository(context);

            Assert.Empty(await repository.ListDueForSweepAsync(_clock.Now, 10));
            Assert.Equal(
                ["session-due", "session-lapsed"],
                (await repository.ListDueForSweepAsync(_clock.Now.AddMinutes(6), 10)).Order());
            Assert.Equal(
                ["session-due", "session-lapsed", "session-reversal"],
                (await repository.ListDueForSweepAsync(_clock.Now.AddMinutes(9), 10)).Order());
        }
    }

    [Fact]
    public async Task Terminal_sessions_do_not_hold_their_payable_instruction()
    {
        var cancelled = NewSession("session-cancelled", "payable-shared", 100m, null);
        cancelled.Cancel(_ids, _clock.Now);
        var active = NewSession("session-active", "payable-shared", 100m, null);

        await using (var context = _database.NewContext())
        {
            await new PaymentSessionRepository(context).AddAsync(cancelled);
            await new PaymentSessionRepository(context).AddAsync(active);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            var repository = new PaymentSessionRepository(context);
            Assert.Equal("session-active", (await repository.FindActiveByPayableInstructionAsync("payable-shared"))?.Id);
            Assert.False(await repository.HasNewerCommercialVersionAsync(5001, 1));
        }
    }

    [Fact]
    public async Task The_store_rejects_a_second_commit_under_the_same_idempotency_key()
    {
        await using (var context = _database.NewContext())
        {
            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "session-a", ["intent-1"]));
            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "session-b", []));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            var record = await new IdempotencyRecordRepository(context).FindAsync(IdempotentOperation.AddPaymentSelections, "session-a", "key-1");
            Assert.Equal(["intent-1"], record!.PaymentIntentIds);

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
                11,
                PayerType.Customer,
                7001,
                new PaymentInitiatorContext("AirlineEmployee", 42, SalesChannel.BackOffice, 501),
                PaymentInteractionMode.StaffAssisted,
                PaymentSelectionMode.Explicit,
                amount,
                70,
                PaymentAssuranceRequirement.IssuanceGuaranteed,
                expiresAt),
            _ids,
            _clock.Now);

    private static PaymentMethodOption Option(TenderType tenderType, string? fundingReference)
        => new(
            $"opt-{tenderType}",
            tenderType,
            tenderType.ToString(),
            70,
            null,
            null,
            null,
            false,
            false,
            false,
            tenderType == TenderType.IranianPgw ? CustomerActionType.Redirect : CustomerActionType.None,
            [PaymentAssuranceRequirement.FundsReceived, PaymentAssuranceRequirement.IssuanceGuaranteed],
            null,
            fundingReference);

    private IdempotencyRecord Record(string key, string scope, IEnumerable<string> paymentIntentIds)
        => IdempotencyRecord.Create(_ids.NewId(), IdempotentOperation.AddPaymentSelections, scope, key, new string('A', 64), scope, paymentIntentIds, _clock.Now);
}
