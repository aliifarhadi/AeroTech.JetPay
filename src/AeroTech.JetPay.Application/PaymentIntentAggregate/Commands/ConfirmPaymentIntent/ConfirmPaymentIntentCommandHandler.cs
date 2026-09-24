using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Services;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ConfirmPaymentIntent
{
    public sealed class ConfirmPaymentIntentCommandHandler : IRequestHandler<ConfirmPaymentIntentCommand, PaymentIntentView>
    {
        private readonly IPaymentIntentRepository _intents;
        private readonly IPaymentMethodOptionCatalog _catalog;
        private readonly ITenderProviderResolver _providers;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentIntentLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ConfirmPaymentIntentCommandHandler(
            IPaymentIntentRepository intents,
            IPaymentMethodOptionCatalog catalog,
            ITenderProviderResolver providers,
            IIdempotencyGuard idempotency,
            IPaymentIntentLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _intents = intents;
            _catalog = catalog;
            _providers = providers;
            _idempotency = idempotency;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentIntentView> Handle(ConfirmPaymentIntentCommand command, CancellationToken cancellationToken)
        {
            var fingerprint = command.Fingerprint();

            await using var intentLock = await _locks.AcquireIntentAsync(command.PaymentIntentId, cancellationToken);

            var intent = await _intents.GetAsync(command.PaymentIntentId, cancellationToken)
                         ?? throw ExceptionFactory.PaymentIntentNotFound(command.PaymentIntentId);

            if (await _idempotency.FindReplayAsync(IdempotentOperation.ConfirmPaymentIntent, intent.Id, command.IdempotencyKey, fingerprint, cancellationToken) is not null)
                return intent.ToView();

            if (intent.ExpireIfDue(_idGenerator, _clock.GetDateTime()))
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw ExceptionFactory.PaymentIntentExpired(intent.Id);
            }

            intent.EnsureConfirmable();

            var option = await ResolveOptionAsync(intent, command.PaymentMethodOptionId, cancellationToken);

            var outcome = await _providers.Resolve(option.TenderType).StartAsync(
                new TenderStartRequest(
                    intent.Id,
                    intent.OrderId,
                    intent.PayerType,
                    intent.PayerId,
                    intent.RequestedAmount,
                    intent.CurrencyId,
                    intent.RequiredGuarantee,
                    intent.CaptureMode,
                    intent.IntentExpiresAt,
                    command.ReturnUrl,
                    $"confirm:{intent.Id}"),
                cancellationToken);

            intent.Confirm(option, outcome, _idGenerator, _clock.GetDateTime());

            await _idempotency.RecordAsync(IdempotentOperation.ConfirmPaymentIntent, intent.Id, command.IdempotencyKey, fingerprint, intent.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return intent.ToView();
        }

        private async Task<PaymentMethodOption> ResolveOptionAsync(PaymentIntent intent, string optionId, CancellationToken cancellationToken)
        {
            var options = await _catalog.ResolveAsync(
                new PaymentMethodEligibilityQuery(
                    intent.OrderId,
                    intent.OrderReference,
                    intent.CommercialVersion,
                    intent.PayerType,
                    intent.PayerId,
                    intent.RequestedAmount,
                    intent.CurrencyId,
                    SalesChannel: null),
                cancellationToken);

            var option = options.FirstOrDefault(candidate => candidate.Id == optionId)
                         ?? throw ExceptionFactory.PaymentMethodOptionNotAvailable(optionId, intent.Id);

            if (!option.Supports(intent.RequiredGuarantee, intent.CaptureMode))
                throw ExceptionFactory.PaymentMethodOptionIncompatible(option.Id, intent.RequiredGuarantee, intent.CaptureMode);

            return option;
        }
    }
}
