using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

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

    public Task<PaymentIntent?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
        => Task.FromResult(Tracked(_committed
            .Where(intent => intent.PayableInstructionId == payableInstructionId && intent.IsActive)
            .OrderByDescending(intent => intent.CreatedAt)
            .FirstOrDefault()));

    public Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default)
        => Task.FromResult(_committed.Any(intent => intent.OrderId == orderId && intent.CommercialVersion > commercialVersion));

    public Task<IReadOnlyList<string>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_committed
            .Where(intent => intent.IsDueForExpiryAt(now))
            .Select(intent => intent.Id)
            .Take(batchSize)
            .ToList());

    private PaymentIntent? Tracked(PaymentIntent? intent)
    {
        if (intent is not null)
            unitOfWork.Track(intent);

        return intent;
    }
}
