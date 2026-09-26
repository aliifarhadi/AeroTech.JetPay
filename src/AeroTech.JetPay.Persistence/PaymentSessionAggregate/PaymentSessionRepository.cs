using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.Persistence.PaymentSessionAggregate
{
    public sealed class PaymentSessionRepository : IPaymentSessionRepository
    {
        private readonly JetPayDbContext _dbContext;

        public PaymentSessionRepository(JetPayDbContext dbContext) => _dbContext = dbContext;

        private IQueryable<PaymentSession> Sessions
            => _dbContext.PaymentSessions
                .Include(session => session.Intents)
                .ThenInclude(intent => intent.ProviderAttempts)
                .AsSplitQuery();

        public async Task AddAsync(PaymentSession session, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentSessions.AddAsync(session, cancellationToken);

        public Task<PaymentSession?> GetAsync(string id, CancellationToken cancellationToken = default)
            => Sessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        public Task<string?> FindSessionIdByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
            => _dbContext.Set<PaymentIntent>()
                .Where(intent => intent.Id == paymentIntentId)
                .Select(intent => intent.PaymentSessionId)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<PaymentSession?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
            => Sessions
                .Where(session => session.PayableInstructionId == payableInstructionId
                                  && session.Status != PaymentSessionStatus.Failed
                                  && session.Status != PaymentSessionStatus.Cancelled
                                  && session.Status != PaymentSessionStatus.Expired)
                .OrderByDescending(session => session.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default)
            => _dbContext.PaymentSessions.AnyAsync(
                session => session.OrderId == orderId && session.CommercialVersion > commercialVersion,
                cancellationToken);

        public async Task<IReadOnlyList<string>> ListDueForSweepAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentSessions
                .Where(session =>
                    (session.Status != PaymentSessionStatus.Failed
                     && session.Status != PaymentSessionStatus.Cancelled
                     && session.Status != PaymentSessionStatus.Expired
                     && session.Status != PaymentSessionStatus.Paid
                     && session.Status != PaymentSessionStatus.Guaranteed
                     && session.ExpiresAt <= now)
                    || session.Intents.Any(intent =>
                        (intent.Status == PaymentIntentStatus.RequiresCustomerAction && intent.NextAction!.ExpiresAt <= now)
                        || ((intent.Status == PaymentIntentStatus.Expired || intent.Status == PaymentIntentStatus.Cancelled)
                            && intent.ProviderAttempts.Any(attempt => attempt.Status == ProviderPaymentAttemptStatus.CustomerActionPending))
                        || intent.ProviderAttempts.Any(attempt =>
                            (attempt.Status == ProviderPaymentAttemptStatus.AutoReversalPending || attempt.Status == ProviderPaymentAttemptStatus.Unknown)
                            && attempt.ReversalExpectedAt <= now)))
                .OrderBy(session => session.UpdatedAt)
                .Select(session => session.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
    }
}
