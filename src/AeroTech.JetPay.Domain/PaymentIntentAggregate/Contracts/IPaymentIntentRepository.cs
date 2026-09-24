namespace AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts
{
    public interface IPaymentIntentRepository
    {
        Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default);

        Task<PaymentIntent?> GetAsync(string id, CancellationToken cancellationToken = default);

        Task<PaymentIntent?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default);

        Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
    }
}
