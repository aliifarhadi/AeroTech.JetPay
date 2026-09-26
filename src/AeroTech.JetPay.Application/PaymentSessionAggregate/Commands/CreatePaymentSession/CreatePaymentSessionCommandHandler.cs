using System.Globalization;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession
{
    public sealed class CreatePaymentSessionCommandHandler : IRequestHandler<CreatePaymentSessionCommand, PaymentSessionView>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentSelectionPlanner _planner;
        private readonly IPaymentIntentExecution _execution;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CreatePaymentSessionCommandHandler(
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

        public async Task<PaymentSessionView> Handle(CreatePaymentSessionCommand command, CancellationToken cancellationToken)
        {
            if (command.Selections.Count > 0 && command.SelectionMode != PaymentSelectionMode.Explicit)
                throw ExceptionFactory.SelectionsOnlyForExplicit();

            var fingerprint = command.Fingerprint();

            await using var creationLock = await _locks.AcquireCreationAsync(command.IdempotencyKey, cancellationToken);

            var replay = await _idempotency.FindReplayAsync(
                IdempotentOperation.CreatePaymentSession,
                IdempotencyRecord.GlobalScope,
                command.IdempotencyKey,
                fingerprint,
                cancellationToken);

            if (replay is not null)
                return await ResumeAsync(replay, cancellationToken);

            await using var instructionLock = await _locks.AcquireInstructionAsync(command.PayableInstructionId, cancellationToken);

            var args = command.ToArgs();
            var session = await _sessions.FindActiveByPayableInstructionAsync(args.PayableInstructionId, cancellationToken);
            var contributions = new List<PaymentIntent>();

            if (session is null)
            {
                session = PaymentSession.Create(
                    _idGenerator.NewId().ToString(CultureInfo.InvariantCulture),
                    args,
                    _idGenerator,
                    _clock.GetDateTime());

                await _sessions.AddAsync(session, cancellationToken);
                contributions.AddRange(await FundAsync(session, command, cancellationToken));
            }
            else if (!session.HasTermsOf(args))
            {
                throw ExceptionFactory.PayableInstructionConflict(args.PayableInstructionId, session.Id);
            }

            await _idempotency.RecordAsync(
                IdempotentOperation.CreatePaymentSession,
                IdempotencyRecord.GlobalScope,
                command.IdempotencyKey,
                fingerprint,
                session.Id,
                contributions.Select(intent => intent.Id),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (contributions.Count > 0)
            {
                await using var sessionLock = await _locks.AcquireSessionAsync(session.Id, cancellationToken);

                foreach (var intent in contributions)
                    await _execution.DispatchAsync(session, intent, null, cancellationToken);
            }

            return session.ToView();
        }

        private async Task<IReadOnlyList<PaymentIntent>> FundAsync(PaymentSession session, CreatePaymentSessionCommand command, CancellationToken cancellationToken)
        {
            if (session.RequiredAmount == 0)
                return [];

            var now = _clock.GetDateTime();

            switch (session.SelectionMode)
            {
                case PaymentSelectionMode.Default:
                    var plan = await _planner.PlanDefaultAsync(session, cancellationToken);

                    if (plan.Contribution is null)
                    {
                        session.RecordFundingFailure(plan.FailureCode!, _idGenerator, now);
                        return [];
                    }

                    return [session.AddIntent(NewIntentId(), plan.Contribution.Option, plan.Contribution.Amount, _idGenerator, now)];

                case PaymentSelectionMode.Explicit when command.Selections.Count > 0:
                    var contributions = await _planner.PlanExplicitAsync(session, command.Selections, cancellationToken);

                    return contributions
                        .Select(contribution => session.AddIntent(NewIntentId(), contribution.Option, contribution.Amount, _idGenerator, now))
                        .ToList();

                default:
                    return [];
            }
        }

        private async Task<PaymentSessionView> ResumeAsync(IdempotencyRecord replay, CancellationToken cancellationToken)
        {
            await using var sessionLock = await _locks.AcquireSessionAsync(replay.PaymentSessionId, cancellationToken);

            var session = await _sessions.GetAsync(replay.PaymentSessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(replay.PaymentSessionId);

            foreach (var intent in session.Intents.Where(intent => replay.PaymentIntentIds.Contains(intent.Id) && intent.AwaitsDispatch).ToList())
                await _execution.DispatchAsync(session, intent, null, cancellationToken);

            return session.ToView();
        }

        private string NewIntentId() => _idGenerator.NewId().ToString(CultureInfo.InvariantCulture);
    }
}
