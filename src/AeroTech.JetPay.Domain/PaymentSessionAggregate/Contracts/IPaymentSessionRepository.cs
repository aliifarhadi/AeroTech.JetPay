namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts
{
    public interface IPaymentSessionRepository
    {
        Task AddAsync(PaymentSession session, CancellationToken cancellationToken = default);

        Task<PaymentSession?> GetAsync(string id, CancellationToken cancellationToken = default);

        Task<PaymentSession?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default);

        Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
    }
}
