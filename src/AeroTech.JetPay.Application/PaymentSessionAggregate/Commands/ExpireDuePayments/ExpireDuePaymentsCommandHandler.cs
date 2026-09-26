using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments
{
    public sealed class ExpireDuePaymentsCommandHandler : IRequestHandler<ExpireDuePaymentsCommand, int>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentIntentExecution _execution;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ExpireDuePaymentsCommandHandler(
            IPaymentSessionRepository sessions,
            IPaymentIntentExecution execution,
            IPaymentSessionLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _sessions = sessions;
            _execution = execution;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<int> Handle(ExpireDuePaymentsCommand command, CancellationToken cancellationToken)
        {
            var due = await _sessions.ListDueForSweepAsync(_clock.GetDateTime(), command.BatchSize, cancellationToken);
            var swept = 0;

            foreach (var sessionId in due)
            {
                await using var sessionLock = await _locks.TryAcquireSessionAsync(sessionId, cancellationToken);

                if (sessionLock is null || await _sessions.GetAsync(sessionId, cancellationToken) is not { } session)
                    continue;

                await SweepAsync(session, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                swept++;
            }

            return swept;
        }

        private async Task SweepAsync(PaymentSession session, CancellationToken cancellationToken)
        {
            var now = _clock.GetDateTime();

            if (session.IsDueForExpiryAt(now))
                session.Expire(_idGenerator, now);

            foreach (var intent in session.Intents.Where(intent => intent.IsCustomerActionLapsedAt(now)).ToList())
            {
                var lapsedAt = intent.NextAction?.ExpiresAt ?? now;
                session.ExpireCustomerAction(intent, _idGenerator, now);
                await _execution.ResolveAbandonedAttemptAsync(session, intent, lapsedAt, cancellationToken);
            }

            foreach (var intent in session.Intents.Where(IsAbandoned).ToList())
                await _execution.ResolveAbandonedAttemptAsync(session, intent, intent.UpdatedAt, cancellationToken);

            foreach (var intent in session.Intents.ToList())
            {
                foreach (var attempt in intent.ProviderAttempts.Where(attempt => attempt.IsDueForReversalAt(now)).ToList())
                    session.RecordReversed(intent, attempt, _idGenerator, now);
            }
        }

        private static bool IsAbandoned(PaymentIntent intent)
            => !intent.IsOpen && intent.CurrentAttempt?.Status == ProviderPaymentAttemptStatus.CustomerActionPending;
    }
}
