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

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CancelPaymentIntent
{
    /// <summary>
    /// Cancels an uncaptured intent and releases any provider-side session or authorization. Captured money is never
    /// cancelled; it leaves only through a later refund.
    /// </summary>
    public sealed class CancelPaymentIntentCommandHandler : IRequestHandler<CancelPaymentIntentCommand, PaymentIntentView>
    {
        private readonly IPaymentIntentRepository _intents;
        private readonly ITenderProviderResolver _providers;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentIntentLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public CancelPaymentIntentCommandHandler(
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

        public async Task<PaymentIntentView> Handle(CancelPaymentIntentCommand command, CancellationToken cancellationToken)
        {
            var fingerprint = command.Fingerprint();

            await using var intentLock = await _locks.AcquireIntentAsync(command.PaymentIntentId, cancellationToken);

            var intent = await _intents.GetAsync(command.PaymentIntentId, cancellationToken)
                         ?? throw ExceptionFactory.PaymentIntentNotFound(command.PaymentIntentId);

            if (await _idempotency.FindReplayAsync(IdempotentOperation.CancelPaymentIntent, intent.Id, command.IdempotencyKey, fingerprint, cancellationToken) is not null)
                return intent.ToView();

            intent.EnsureCancellable();

            if (intent.RequiresProviderRelease)
            {
                var outcome = await _providers.Resolve(intent.SelectedTenderType!.Value).ReleaseAsync(
                    new TenderReleaseRequest(intent.Id, command.Reason, $"release:{intent.Id}"),
                    cancellationToken);

                if (outcome.Kind != TenderOutcomeKind.Released)
                    throw ExceptionFactory.ProviderDeclinedRelease(intent.Id, outcome.FailureCode);
            }

            intent.Cancel(_idGenerator, _clock.GetDateTime());

            await _idempotency.RecordAsync(IdempotentOperation.CancelPaymentIntent, intent.Id, command.IdempotencyKey, fingerprint, intent.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return intent.ToView();
        }
    }
}
