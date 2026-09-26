namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts
{
    public interface IPaymentSessionRepository
    {
        Task AddAsync(PaymentSession session, CancellationToken cancellationToken = default);

        Task<PaymentSession?> GetAsync(string id, CancellationToken cancellationToken = default);

        Task<string?> FindSessionIdByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);

        Task<PaymentSession?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default);

        Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> ListDueForSweepAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
    }
}
