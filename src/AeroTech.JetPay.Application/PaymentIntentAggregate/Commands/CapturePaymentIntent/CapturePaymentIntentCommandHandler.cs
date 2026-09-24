using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Services;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CapturePaymentIntent
{
    public sealed class CapturePaymentIntentCommandHandler : IRequestHandler<CapturePaymentIntentCommand, PaymentIntentView>
    {
        private readonly IPaymentIntentRepository _intents;
        private readonly ITenderProviderResolver _providers;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentIntentLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CapturePaymentIntentCommandHandler(
            IPaymentIntentRepository intents,
            ITenderProviderResolver providers,
            IIdempotencyGuard idempotency,
            IPaymentIntentLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _intents = intents;
            _providers = providers;
            _idempotency = idempotency;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentIntentView> Handle(CapturePaymentIntentCommand command, CancellationToken cancellationToken)
        {
            var fingerprint = command.Fingerprint();

            await using var intentLock = await _locks.AcquireIntentAsync(command.PaymentIntentId, cancellationToken);

            var intent = await _intents.GetAsync(command.PaymentIntentId, cancellationToken)
                         ?? throw ExceptionFactory.PaymentIntentNotFound(command.PaymentIntentId);

            if (await _idempotency.FindReplayAsync(IdempotentOperation.CapturePaymentIntent, intent.Id, command.IdempotencyKey, fingerprint, cancellationToken) is not null)
                return intent.ToView();

            if (intent.ExpireIfDue(_idGenerator, _clock.GetDateTime()))
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw ExceptionFactory.PaymentIntentExpired(intent.Id);
            }

            var amount = intent.EnsureCapturable(command.Amount);

            var outcome = await _providers.Resolve(intent.SelectedTenderType!.Value).CaptureAsync(
                new TenderCaptureRequest(intent.Id, amount, intent.CurrencyId, command.FinalCapture, $"capture:{intent.Id}:{command.IdempotencyKey}"),
                cancellationToken);

            if (outcome.Kind != TenderOutcomeKind.Captured)
                throw ExceptionFactory.ProviderDeclinedCapture(intent.Id, outcome.FailureCode);

            if (outcome.Amount != amount)
                throw ExceptionFactory.ProviderAmountMismatch(intent.Id, outcome.Amount, amount);

            intent.RecordCapture(amount, command.FinalCapture, _idGenerator, _clock.GetDateTime());

            await _idempotency.RecordAsync(IdempotentOperation.CapturePaymentIntent, intent.Id, command.IdempotencyKey, fingerprint, intent.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return intent.ToView();
        }
    }
}
