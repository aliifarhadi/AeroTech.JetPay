using System.Globalization;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Services;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CreatePaymentIntent
{
    /// <summary>
    /// One payable instruction maps to at most one active intent: a retry under a new key with the same terms
    /// returns the active intent instead of opening a second one.
    /// </summary>
    public sealed class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, PaymentIntentView>
    {
        private readonly IPaymentIntentRepository _intents;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentIntentLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CreatePaymentIntentCommandHandler(
            IPaymentIntentRepository intents,
            IIdempotencyGuard idempotency,
            IPaymentIntentLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _intents = intents;
            _idempotency = idempotency;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentIntentView> Handle(CreatePaymentIntentCommand command, CancellationToken cancellationToken)
        {
            var fingerprint = command.Fingerprint();

            await using var creationLock = await _locks.AcquireCreationAsync(command.IdempotencyKey, cancellationToken);

            var replayId = await _idempotency.FindReplayAsync(
                IdempotentOperation.CreatePaymentIntent,
                IdempotencyRecord.GlobalScope,
                command.IdempotencyKey,
                fingerprint,
                cancellationToken);

            if (replayId is not null)
                return await ReadAsync(replayId, cancellationToken);

            await using var instructionLock = await _locks.AcquireInstructionAsync(command.PayableInstructionId, cancellationToken);

            var args = command.ToArgs();
            var intent = await _intents.FindActiveByPayableInstructionAsync(args.PayableInstructionId, cancellationToken);

            if (intent is null)
            {
                intent = PaymentIntent.Create(
                    _idGenerator.NewId().ToString(CultureInfo.InvariantCulture),
                    args,
                    _idGenerator,
                    _clock.GetDateTime());

                await _intents.AddAsync(intent, cancellationToken);
            }
            else if (!intent.HasTermsOf(args))
            {
                throw ExceptionFactory.PayableInstructionConflict(args.PayableInstructionId, intent.Id);
            }

            await _idempotency.RecordAsync(
                IdempotentOperation.CreatePaymentIntent,
                IdempotencyRecord.GlobalScope,
                command.IdempotencyKey,
                fingerprint,
                intent.Id,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return intent.ToView();
        }

        private async Task<PaymentIntentView> ReadAsync(string paymentIntentId, CancellationToken cancellationToken)
            => (await _intents.GetAsync(paymentIntentId, cancellationToken)
                ?? throw ExceptionFactory.PaymentIntentNotFound(paymentIntentId)).ToView();
    }
}
