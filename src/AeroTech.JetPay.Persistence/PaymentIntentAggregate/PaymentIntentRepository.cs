using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.Persistence.PaymentIntentAggregate
{
    public sealed class PaymentIntentRepository : IPaymentIntentRepository
    {
        private readonly JetPayDbContext _dbContext;

        public PaymentIntentRepository(JetPayDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentIntents.AddAsync(intent, cancellationToken);

        public Task<PaymentIntent?> GetAsync(string id, CancellationToken cancellationToken = default)
            => _dbContext.PaymentIntents.FirstOrDefaultAsync(intent => intent.Id == id, cancellationToken);

        public async Task<IReadOnlyList<PaymentIntent>> ListBySessionAsync(string paymentSessionId, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentIntents
                .Where(intent => intent.PaymentSessionId == paymentSessionId)
                .OrderBy(intent => intent.Sequence)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<string>> ListSessionsWithLegsDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentIntents
                .Where(intent => (intent.Status == PaymentIntentStatus.RequiresCustomerAction && intent.NextAction!.ExpiresAt <= now)
                                 || (intent.Status == PaymentIntentStatus.Authorized && intent.GuaranteeExpiresAt <= now))
                .Select(intent => intent.PaymentSessionId)
                .Distinct()
                .Take(batchSize)
                .ToListAsync(cancellationToken);
    }
}
