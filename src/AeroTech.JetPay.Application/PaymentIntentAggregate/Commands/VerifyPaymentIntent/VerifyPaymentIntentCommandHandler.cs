using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Services;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.VerifyPaymentIntent
{
    public sealed class VerifyPaymentIntentCommandHandler : IRequestHandler<VerifyPaymentIntentCommand, PaymentIntentView>
    {
        private readonly IPaymentIntentRepository _intents;
        private readonly ITenderProviderResolver _providers;
        private readonly IPaymentIntentLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public VerifyPaymentIntentCommandHandler(
            IPaymentIntentRepository intents,
            ITenderProviderResolver providers,
            IPaymentIntentLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _intents = intents;
            _providers = providers;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentIntentView> Handle(VerifyPaymentIntentCommand command, CancellationToken cancellationToken)
        {
            await using var intentLock = await _locks.AcquireIntentAsync(command.PaymentIntentId, cancellationToken);

            var intent = await _intents.GetAsync(command.PaymentIntentId, cancellationToken)
                         ?? throw ExceptionFactory.PaymentIntentNotFound(command.PaymentIntentId);

            if (intent.SelectedTenderType is not { } tenderType)
                throw ExceptionFactory.PaymentIntentCannotTransition(intent.Id, intent.Status, "be verified");

            // The callback is durable evidence that verification is under way; if verify times out the intent stays
            // Processing (never Unknown) and is verified again later.
            if (intent.Status == PaymentIntentStatus.RequiresCustomerAction)
            {
                intent.RecordVerifiedOutcome(TenderOutcome.Processing(), instructionSuperseded: false, _idGenerator, _clock.GetDateTime());
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var outcome = await _providers.Resolve(tenderType).VerifyAsync(
                new TenderVerifyRequest(
                    intent.Id,
                    intent.OrderId,
                    intent.RequestedAmount,
                    intent.CurrencyId,
                    intent.RequiredGuarantee,
                    $"verify:{intent.Id}"),
                cancellationToken);

            var superseded = await _intents.HasNewerCommercialVersionAsync(intent.OrderId, intent.CommercialVersion, cancellationToken);

            intent.RecordVerifiedOutcome(outcome, superseded, _idGenerator, _clock.GetDateTime());
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return intent.ToView();
        }
    }
}
