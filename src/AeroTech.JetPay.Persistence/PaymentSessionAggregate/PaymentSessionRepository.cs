using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.Persistence.PaymentSessionAggregate
{
    public sealed class PaymentSessionRepository : IPaymentSessionRepository
    {
        private readonly JetPayDbContext _dbContext;

        public PaymentSessionRepository(JetPayDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(PaymentSession session, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentSessions.AddAsync(session, cancellationToken);

        public Task<PaymentSession?> GetAsync(string id, CancellationToken cancellationToken = default)
            => _dbContext.PaymentSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        public Task<PaymentSession?> FindActiveByPayableInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
            => _dbContext.PaymentSessions
                .Where(session => session.PayableInstructionId == payableInstructionId
                                  && session.Status != PaymentSessionStatus.Cancelled
                                  && session.Status != PaymentSessionStatus.Expired)
                .OrderByDescending(session => session.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<bool> HasNewerCommercialVersionAsync(long orderId, int commercialVersion, CancellationToken cancellationToken = default)
            => _dbContext.PaymentSessions.AnyAsync(
                session => session.OrderId == orderId && session.CommercialVersion > commercialVersion,
                cancellationToken);

        public async Task<IReadOnlyList<string>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
            => await _dbContext.PaymentSessions
                .Where(session => session.Status != PaymentSessionStatus.Cancelled
                                  && session.Status != PaymentSessionStatus.Expired
                                  && session.Status != PaymentSessionStatus.Paid
                                  && session.ExpiresAt <= now)
                .OrderBy(session => session.ExpiresAt)
                .Select(session => session.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
    }
}
