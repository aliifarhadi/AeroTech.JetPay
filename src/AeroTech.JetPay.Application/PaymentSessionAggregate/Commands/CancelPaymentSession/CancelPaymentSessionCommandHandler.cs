using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CancelPaymentSession
{
    public sealed class CancelPaymentSessionCommandHandler : IRequestHandler<CancelPaymentSessionCommand, PaymentSessionResponse>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentSessionFunding _funding;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CancelPaymentSessionCommandHandler(
            IPaymentSessionRepository sessions,
            IPaymentSessionFunding funding,
            IIdempotencyGuard idempotency,
            IPaymentSessionLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _sessions = sessions;
            _funding = funding;
            _idempotency = idempotency;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentSessionResponse> Handle(CancelPaymentSessionCommand command, CancellationToken cancellationToken)
        {
            await using var sessionLock = await _locks.AcquireSessionAsync(command.PaymentSessionId, cancellationToken);

            var session = await _sessions.GetAsync(command.PaymentSessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(command.PaymentSessionId);
            var intents = await _funding.LoadIntentsAsync(session, cancellationToken);
            var fingerprint = command.Fingerprint();

            if (await _idempotency.FindReplayAsync(IdempotentOperation.CancelPaymentSession, session.Id, command.IdempotencyKey, fingerprint, cancellationToken) is not null
                || session.Status == PaymentSessionStatus.Cancelled)
                return session.ToResponse(intents);

            if (session.IsTerminal)
                throw ExceptionFactory.PaymentSessionCannotTransition(session.Id, session.Status, "be cancelled");

            await _funding.ResolveUndispatchedAsync(session, intents, cancellationToken);

            var now = _clock.GetDateTime();
            await _funding.CloseUnfundedLegsAsync(intents, intent => intent.Cancel(now), cancellationToken);
            session.Cancel(intents, _idGenerator, now);

            await _idempotency.RecordAsync(IdempotentOperation.CancelPaymentSession, session.Id, command.IdempotencyKey, fingerprint, session.Id, [], cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return session.ToResponse(intents);
        }
    }
}
