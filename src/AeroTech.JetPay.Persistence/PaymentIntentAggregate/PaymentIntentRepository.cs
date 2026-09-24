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

        public Task<PaymentIntent?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
            => _dbContext.PaymentIntents
                .Where(intent => intent.PayableInstructionId == payableInstructionId
                                 && intent.Status != PaymentIntentStatus.Failed
                                 && intent.Status != PaymentIntentStatus.Cancelled
                                 && intent.Status != PaymentIntentStatus.Expired)
                .OrderByDescending(intent => intent.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default)
            => _dbContext.PaymentIntents.AnyAsync(
                intent => intent.OrderId == orderId && intent.CommercialVersion > commercialVersion,
                cancellationToken);

        // Mirrors PaymentIntent.IsDueForExpiryAt so a sweep batch never fills with intents that will not change.
        public async Task<IReadOnlyList<string>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentIntents
                .Where(intent =>
                    (intent.Status == PaymentIntentStatus.Created && intent.IntentExpiresAt <= now)
                    || (intent.Status == PaymentIntentStatus.RequiresCustomerAction
                        && (intent.IntentExpiresAt <= now || intent.NextAction!.ExpiresAt <= now))
                    || (intent.Status == PaymentIntentStatus.Authorized
                        && (intent.GuaranteeExpiresAt <= now || intent.IntentExpiresAt <= now))
                    || (intent.Status == PaymentIntentStatus.PartiallyCaptured
                        && intent.GuaranteedAmount > intent.CapturedAmount
                        && intent.GuaranteeExpiresAt <= now))
                .OrderBy(intent => intent.UpdatedAt)
                .Select(intent => intent.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
    }
}
