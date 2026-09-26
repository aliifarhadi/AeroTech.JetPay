using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;

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

    public Task<PaymentSession?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
        => Task.FromResult(Tracked(_committed
            .Where(session => session.PayableInstructionId == payableInstructionId && !session.IsTerminal)
            .OrderByDescending(session => session.CreatedAt)
            .FirstOrDefault()));

    public Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default)
        => Task.FromResult(_committed.Any(session => session.OrderId == orderId && session.CommercialVersion > commercialVersion));

    public Task<IReadOnlyList<string>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_committed.Where(session => session.IsDueForExpiryAt(now)).Select(session => session.Id).Take(batchSize).ToList());

    private PaymentSession? Tracked(PaymentSession? session)
    {
        if (session is not null)
            unitOfWork.Track(session);

        return session;
    }
}

public sealed class InMemoryPaymentIntentRepository(InMemoryUnitOfWork unitOfWork) : IPaymentIntentRepository
{
    private readonly List<PaymentIntent> _committed = [];

    public IReadOnlyList<PaymentIntent> Committed => _committed;

    public Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        unitOfWork.Track(intent);
        unitOfWork.Stage(() => _committed.Add(intent));
        return Task.CompletedTask;
    }

    public Task<PaymentIntent?> GetAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(Tracked(_committed.FirstOrDefault(intent => intent.Id == id)));

    public Task<IReadOnlyList<PaymentIntent>> ListBySessionAsync(string paymentSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PaymentIntent>>(_committed
            .Where(intent => intent.PaymentSessionId == paymentSessionId)
            .OrderBy(intent => intent.Sequence)
            .Select(intent => Tracked(intent)!)
            .ToList());

    public Task<IReadOnlyList<string>> ListSessionsWithLegsDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_committed
            .Where(intent => intent.IsDueForExpiryAt(now))
            .Select(intent => intent.PaymentSessionId)
            .Distinct()
            .Take(batchSize)
            .ToList());

    private PaymentIntent? Tracked(PaymentIntent? intent)
    {
        if (intent is not null)
            unitOfWork.Track(intent);

        return intent;
    }
}
