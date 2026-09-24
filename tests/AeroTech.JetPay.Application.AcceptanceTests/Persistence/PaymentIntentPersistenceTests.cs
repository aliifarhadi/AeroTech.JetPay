using AeroTech.JetPay.Application.AcceptanceTests.Fakes;
using AeroTech.JetPay.Application.AcceptanceTests.Fixtures;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Arguments;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Persistence.IdempotencyRecordAggregate;
using AeroTech.JetPay.Persistence.PaymentIntentAggregate;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Persistence;

/// <summary>Runs against the development SQL Server through the real migrations.</summary>
public sealed class PaymentIntentPersistenceTests : IAsyncLifetime
{
    private readonly FixedClock _clock = new();
    private readonly SequentialIdGenerator _ids = new();
    private readonly TestDatabase _database;

    public PaymentIntentPersistenceTests() => _database = new TestDatabase(_clock);

    public Task InitializeAsync() => _database.MigrateAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task A_payment_intent_round_trips_with_its_customer_action_and_exact_amounts()
    {
        var action = new CustomerAction(
            CustomerActionType.HtmlForm,
            "https://gateway.test/pay",
            "POST",
            new Dictionary<string, string> { ["token"] = "abc", ["amount"] = "1250000" },
            _clock.Now.AddMinutes(15));

        var intent = NewIntent("pi-1", "payable-1", 1_250_000.125m);
        intent.Confirm(IranianPgwOption(), TenderOutcome.RequiresCustomerAction(action), _ids, _clock.Now);

        await using (var context = _database.NewContext())
        {
            await new PaymentIntentRepository(context).AddAsync(intent);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            var loaded = await new PaymentIntentRepository(context).GetAsync("pi-1");

            Assert.NotNull(loaded);
            Assert.Equal(PaymentIntentStatus.RequiresCustomerAction, loaded.Status);
            Assert.Equal(1_250_000.125m, loaded.RequestedAmount);
            Assert.Equal(action, loaded.NextAction);
            Assert.Equal(TenderType.IranianPgw, loaded.SelectedTenderType);
            Assert.Equal(2, loaded.Version);
        }
    }

    [Fact]
    public async Task The_expiry_query_selects_exactly_the_intents_the_domain_would_expire()
    {
        var due = NewIntent("pi-due", "payable-due", 100m);
        due.Confirm(IranianPgwOption(), TenderOutcome.RequiresCustomerAction(CustomerAction.Redirect("https://gateway.test", _clock.Now.AddMinutes(5))), _ids, _clock.Now);
        var notDue = NewIntent("pi-not-due", "payable-not-due", 100m);
        notDue.Confirm(IranianPgwOption(), TenderOutcome.RequiresCustomerAction(CustomerAction.Redirect("https://gateway.test", _clock.Now.AddHours(1))), _ids, _clock.Now);

        await using (var context = _database.NewContext())
        {
            var repository = new PaymentIntentRepository(context);
            await repository.AddAsync(due);
            await repository.AddAsync(notDue);
            await context.SaveChangesAsync();
        }

        var later = _clock.Now.AddMinutes(10);

        await using (var context = _database.NewContext())
        {
            var dueIds = await new PaymentIntentRepository(context).ListDueForExpiryAsync(later, 10);

            Assert.Equal(["pi-due"], dueIds);
            Assert.True(due.IsDueForExpiryAt(later));
            Assert.False(notDue.IsDueForExpiryAt(later));
        }
    }

    [Fact]
    public async Task Terminal_intents_do_not_hold_their_payable_instruction()
    {
        var cancelled = NewIntent("pi-cancelled", "payable-shared", 100m);
        cancelled.Cancel(_ids, _clock.Now);
        var active = NewIntent("pi-active", "payable-shared", 100m);

        await using (var context = _database.NewContext())
        {
            var repository = new PaymentIntentRepository(context);
            await repository.AddAsync(cancelled);
            await repository.AddAsync(active);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
            Assert.Equal("pi-active", (await new PaymentIntentRepository(context).FindActiveByPayableInstructionAsync("payable-shared"))?.Id);
    }

    [Fact]
    public async Task The_store_rejects_a_second_commit_under_the_same_idempotency_key()
    {
        await using (var context = _database.NewContext())
        {
            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "scope-a"));
            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "scope-b"));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.NewContext())
        {
            await new IdempotencyRecordRepository(context).AddAsync(Record("key-1", "scope-a"));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    private PaymentIntent NewIntent(string id, string payableInstructionId, decimal amount)
        => PaymentIntent.Create(
            id,
            new CreatePaymentIntentArgs(
                payableInstructionId,
                5001,
                "ORD-5001",
                1,
                PaymentPurpose.InitialSale,
                PayerType.Customer,
                7001,
                amount,
                364,
                RequiredGuarantee.PaidBeforeIssuance,
                PaymentCaptureMode.Automatic,
                null),
            _ids,
            _clock.Now);

    private IdempotencyRecord Record(string key, string scope)
        => IdempotencyRecord.Create(_ids.NewId(), IdempotentOperation.ConfirmPaymentIntent, scope, key, new string('A', 64), "pi-1", _clock.Now);

    private static PaymentMethodOption IranianPgwOption()
        => new("iranian-pgw", TenderType.IranianPgw, "IranianPgw", CustomerActionType.Redirect, [RequiredGuarantee.PaidBeforeIssuance], [PaymentCaptureMode.Automatic]);
}
