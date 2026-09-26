using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments
{
    public sealed class ExpireDuePaymentsCommandHandler : IRequestHandler<ExpireDuePaymentsCommand, int>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentIntentRepository _intents;
        private readonly IPaymentSessionFunding _funding;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ExpireDuePaymentsCommandHandler(
            IPaymentSessionRepository sessions,
            IPaymentIntentRepository intents,
            IPaymentSessionFunding funding,
            IPaymentSessionLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _sessions = sessions;
            _intents = intents;
            _funding = funding;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<int> Handle(ExpireDuePaymentsCommand command, CancellationToken cancellationToken)
        {
            var now = _clock.GetDateTime();
            var due = (await _sessions.ListDueForExpiryAsync(now, command.BatchSize, cancellationToken))
                .Concat(await _intents.ListSessionsWithLegsDueForExpiryAsync(now, command.BatchSize, cancellationToken))
                .Distinct()
                .ToList();

            var changed = 0;

            foreach (var sessionId in due)
            {
                await using var sessionLock = await _locks.TryAcquireSessionAsync(sessionId, cancellationToken);

                if (sessionLock is null || await _sessions.GetAsync(sessionId, cancellationToken) is not { } session)
                    continue;

                var intents = await _funding.LoadIntentsAsync(session, cancellationToken);

                if (session.IsDueForExpiryAt(now))
                {
                    await _funding.ResolveUndispatchedAsync(session, intents, cancellationToken);
                    await _funding.CloseUnfundedLegsAsync(intents, intent => intent.Expire(now), cancellationToken);
                    session.Expire(intents, _idGenerator, now);
                }
                else if (!session.IsTerminal && intents.Any(intent => intent.IsDueForExpiryAt(now)))
                {
                    foreach (var intent in intents.Where(intent => intent.IsDueForExpiryAt(now)))
                    {
                        await _funding.ReleaseAsync(intent, cancellationToken);
                        intent.ExpireIfDue(now);
                    }

                    session.Refresh(intents, _idGenerator, now);
                }
                else
                {
                    continue;
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                changed++;
            }

            return changed;
        }
    }
}
