using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class InMemoryPaymentSessionRepository(InMemoryUnitOfWork unitOfWork) : IPaymentSessionRepository
{
    private readonly List<PaymentSession> _committed = [];

    public IReadOnlyList<PaymentSession> Committed => _committed;

    public Task AddAsync(PaymentSession session, CancellationToken cancellationToken = default)
    {
        unitOfWork.Track(session);
        unitOfWork.Stage(() => _committed.Add(session));
        return Task.CompletedTask;
    }

    public Task<PaymentSession?> GetAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(Tracked(_committed.FirstOrDefault(session => session.Id == id)));

    public Task<string?> FindSessionIdByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_committed.FirstOrDefault(session => session.Intents.Any(intent => intent.Id == paymentIntentId))?.Id);

    public Task<PaymentSession?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
        => Task.FromResult(Tracked(_committed
            .Where(session => session.PayableInstructionId == payableInstructionId && !session.IsTerminal)
            .OrderByDescending(session => session.CreatedAt)
            .FirstOrDefault()));

    public Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default)
        => Task.FromResult(_committed.Any(session => session.OrderId == orderId && session.CommercialVersion > commercialVersion));

    public Task<IReadOnlyList<string>> ListDueForSweepAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_committed
            .Where(session => session.IsDueForExpiryAt(now)
                              || session.Intents.Any(intent =>
                                  intent.IsCustomerActionLapsedAt(now)
                                  || (intent.Status is PaymentIntentStatus.Expired or PaymentIntentStatus.Cancelled
                                      && intent.ProviderAttempts.Any(attempt => attempt.Status == ProviderPaymentAttemptStatus.CustomerActionPending))
                                  || intent.ProviderAttempts.Any(attempt => attempt.IsDueForReversalAt(now))))
            .OrderBy(session => session.UpdatedAt)
            .Select(session => session.Id)
            .Take(batchSize)
            .ToList());

    private PaymentSession? Tracked(PaymentSession? session)
    {
        if (session is not null)
            unitOfWork.Track(session);

        return session;
    }
}
