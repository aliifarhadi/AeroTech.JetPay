using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Services
{
    public interface IPaymentSessionFunding
    {
        Task DispatchAsync(PaymentSession session, List<PaymentIntent> intents, string? returnUrl, CancellationToken cancellationToken = default);

        Task ResolveUndispatchedAsync(PaymentSession session, List<PaymentIntent> intents, CancellationToken cancellationToken = default);

        Task ReleaseAsync(PaymentIntent intent, CancellationToken cancellationToken = default);

        Task<List<PaymentIntent>> LoadIntentsAsync(PaymentSession session, CancellationToken cancellationToken = default);
    }

    public sealed class PaymentSessionFunding : IPaymentSessionFunding
    {
        private readonly IPaymentIntentRepository _intents;
        private readonly ITenderProviderResolver _providers;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public PaymentSessionFunding(
            IPaymentIntentRepository intents,
            ITenderProviderResolver providers,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _intents = intents;
            _providers = providers;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<List<PaymentIntent>> LoadIntentsAsync(PaymentSession session, CancellationToken cancellationToken = default)
            => (await _intents.ListBySessionAsync(session.Id, cancellationToken)).ToList();

        public async Task DispatchAsync(PaymentSession session, List<PaymentIntent> intents, string? returnUrl, CancellationToken cancellationToken = default)
        {
            foreach (var intent in intents.Where(intent => intent.IsDispatchPending).OrderBy(intent => intent.Sequence).ToList())
            {
                var outcome = await _providers.Resolve(intent.TenderType).StartAsync(
                    new TenderStartRequest(
                        intent.Id,
                        session.Id,
                        session.OrderId,
                        session.PayerType,
                        session.PayerId,
                        intent.RequestedAmount,
                        intent.CurrencyId,
                        intent.ProviderReference,
                        session.InitiatorContext.SalesChannel,
                        session.InitiatorContext.OfficeId,
                        returnUrl,
                        session.ExpiresAt,
                        intent.Id),
                    cancellationToken);

                intent.ApplyStartOutcome(outcome, _clock.GetDateTime());
                session.Refresh(intents, _idGenerator, _clock.GetDateTime());
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task ResolveUndispatchedAsync(PaymentSession session, List<PaymentIntent> intents, CancellationToken cancellationToken = default)
        {
            foreach (var intent in intents.Where(intent => intent.IsDispatchPending).ToList())
            {
                var outcome = await _providers.Resolve(intent.TenderType).VerifyAsync(
                    new TenderVerifyRequest(intent.Id, session.Id, session.OrderId, intent.RequestedAmount, intent.CurrencyId, intent.ProviderReference, intent.Id),
                    cancellationToken);

                intent.ApplyStartOutcome(outcome, _clock.GetDateTime());
            }
        }

        public async Task ReleaseAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
        {
            if (!intent.RequiresProviderRelease)
                return;

            var outcome = await _providers.Resolve(intent.TenderType).ReleaseAsync(
                new TenderReleaseRequest(intent.Id, intent.ProviderReference, $"release:{intent.Id}"),
                cancellationToken);

            if (outcome.Kind != TenderOutcomeKind.Released)
                throw ExceptionFactory.ProviderDeclinedRelease(intent.Id, outcome.FailureCode);
        }
    }

    public static class PaymentIntentUnwinding
    {
        public static async Task CloseUnfundedLegsAsync(
            this IPaymentSessionFunding funding,
            List<PaymentIntent> intents,
            Action<PaymentIntent> close,
            CancellationToken cancellationToken)
        {
            foreach (var intent in intents.Where(intent => intent.Status is PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Authorized))
            {
                await funding.ReleaseAsync(intent, cancellationToken);
                close(intent);
            }
        }
    }
}
