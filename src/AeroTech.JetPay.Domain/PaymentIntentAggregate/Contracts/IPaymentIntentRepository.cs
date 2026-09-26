namespace AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts
{
    public interface IPaymentIntentRepository
    {
        Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default);

        Task<PaymentIntent?> GetAsync(string id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PaymentIntent>> ListBySessionAsync(string paymentSessionId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> ListSessionsWithLegsDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
    }
}
