using System.Globalization;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.AddPaymentSelections
{
    public sealed class AddPaymentSelectionsCommandHandler : IRequestHandler<AddPaymentSelectionsCommand, PaymentSessionView>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentSelectionPlanner _planner;
        private readonly IPaymentIntentExecution _execution;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public AddPaymentSelectionsCommandHandler(
            IPaymentSessionRepository sessions,
            IPaymentSelectionPlanner planner,
            IPaymentIntentExecution execution,
            IIdempotencyGuard idempotency,
            IPaymentSessionLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _sessions = sessions;
            _planner = planner;
            _execution = execution;
            _idempotency = idempotency;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentSessionView> Handle(AddPaymentSelectionsCommand command, CancellationToken cancellationToken)
        {
            await using var sessionLock = await _locks.AcquireSessionAsync(command.PaymentSessionId, cancellationToken);

            var session = await _sessions.GetAsync(command.PaymentSessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(command.PaymentSessionId);
            var fingerprint = command.Fingerprint();

            var replay = await _idempotency.FindReplayAsync(
                IdempotentOperation.AddPaymentSelections,
                session.Id,
                command.IdempotencyKey,
                fingerprint,
                cancellationToken);

            if (replay is not null)
            {
                foreach (var pending in session.Intents.Where(intent => replay.PaymentIntentIds.Contains(intent.Id) && intent.AwaitsDispatch).ToList())
                    await _execution.DispatchAsync(session, pending, command.ReturnUrl, cancellationToken);

                return session.ToView();
            }

            var now = _clock.GetDateTime();
            session.EnsureAcceptsFunding(now);

            var plan = await _planner.PlanExplicitAsync(session, command.Selections, cancellationToken);
            var contributions = plan
                .Select(contribution => session.AddIntent(
                    _idGenerator.NewId().ToString(CultureInfo.InvariantCulture),
                    contribution.Option,
                    contribution.Amount,
                    _idGenerator,
                    now))
                .ToList();

            await _idempotency.RecordAsync(
                IdempotentOperation.AddPaymentSelections,
                session.Id,
                command.IdempotencyKey,
                fingerprint,
                session.Id,
                contributions.Select(intent => intent.Id),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var intent in contributions)
                await _execution.DispatchAsync(session, intent, command.ReturnUrl, cancellationToken);

            return session.ToView();
        }
    }
}
