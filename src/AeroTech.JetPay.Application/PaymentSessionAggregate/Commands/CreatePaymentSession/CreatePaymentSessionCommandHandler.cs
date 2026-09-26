using System.Globalization;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession
{
    public sealed class CreatePaymentSessionCommandHandler : IRequestHandler<CreatePaymentSessionCommand, PaymentSessionResponse>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentSessionFunding _funding;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CreatePaymentSessionCommandHandler(
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

        public async Task<PaymentSessionResponse> Handle(CreatePaymentSessionCommand command, CancellationToken cancellationToken)
        {
            var fingerprint = command.Fingerprint();

            await using var creationLock = await _locks.AcquireCreationAsync(command.IdempotencyKey, cancellationToken);

            var replay = await _idempotency.FindReplayAsync(
                IdempotentOperation.CreatePaymentSession,
                IdempotencyRecord.GlobalScope,
                command.IdempotencyKey,
                fingerprint,
                cancellationToken);

            if (replay is not null)
                return await ReadAsync(replay.PaymentSessionId, cancellationToken);

            await using var instructionLock = await _locks.AcquireInstructionAsync(command.PayableInstructionId, cancellationToken);

            var args = command.ToArgs();
            var session = await _sessions.FindActiveByPayableInstructionAsync(args.PayableInstructionId, cancellationToken);

            if (session is null)
            {
                session = PaymentSession.Create(
                    _idGenerator.NewId().ToString(CultureInfo.InvariantCulture),
                    args,
                    _idGenerator,
                    _clock.GetDateTime());

                await _sessions.AddAsync(session, cancellationToken);
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
                [],
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return session.ToResponse(await _funding.LoadIntentsAsync(session, cancellationToken));
        }

        private async Task<PaymentSessionResponse> ReadAsync(string paymentSessionId, CancellationToken cancellationToken)
        {
            var session = await _sessions.GetAsync(paymentSessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(paymentSessionId);

            return session.ToResponse(await _funding.LoadIntentsAsync(session, cancellationToken));
        }
    }
}
