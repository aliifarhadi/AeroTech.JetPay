using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Services
{
    public interface IPaymentIntentExecution
    {
        Task DispatchAsync(PaymentSession session, PaymentIntent intent, string? returnUrl, CancellationToken cancellationToken = default);

        Task ApplyCallbackAsync(PaymentSession session, PaymentIntent intent, DateTimeOffset? paidAt, CancellationToken cancellationToken = default);

        Task ReconcileAsync(PaymentSession session, PaymentIntent intent, CancellationToken cancellationToken = default);

        Task ResolveAbandonedAttemptAsync(PaymentSession session, PaymentIntent intent, DateTimeOffset abandonedAt, CancellationToken cancellationToken = default);
    }

    public sealed class PaymentIntentExecution : IPaymentIntentExecution
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IProviderProfileCatalog _profiles;
        private readonly ITenderProviderResolver _providers;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public PaymentIntentExecution(
            IPaymentSessionRepository sessions,
            IProviderProfileCatalog profiles,
            ITenderProviderResolver providers,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _sessions = sessions;
            _profiles = profiles;
            _providers = providers;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task DispatchAsync(PaymentSession session, PaymentIntent intent, string? returnUrl, CancellationToken cancellationToken = default)
        {
            while (intent.AwaitsDispatch && !session.IsTerminal)
            {
                var attempt = intent.CurrentAttempt is { Status: ProviderPaymentAttemptStatus.Created } pending ? pending : null;
                var resuming = attempt is not null;
                ProviderProfile profile;

                if (attempt is null)
                {
                    if (await NextRouteAsync(intent, cancellationToken) is not { } route)
                    {
                        session.FailIntent(intent, IntentFailureCode.ProviderUnavailable, "No provider route could take the payment.", _idGenerator, _clock.GetDateTime());
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        return;
                    }

                    profile = route;
                    attempt = session.OpenProviderAttempt(intent, _idGenerator.NewId(), profile, _clock.GetDateTime());
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    profile = await ProfileAsync(attempt.ProviderProfileId, cancellationToken);
                }

                var provider = _providers.Resolve(intent.TenderType);
                var result = !resuming || profile.SupportsProviderIdempotency
                    ? await provider.StartAsync(StartRequest(session, intent, attempt, returnUrl), cancellationToken)
                    : profile.SupportsInquiry
                        ? await provider.InquireAsync(OperationRequest(intent, attempt), cancellationToken)
                        : ProviderResult.Unknown(null);

                await ApplyStartResultAsync(session, intent, attempt, profile, result, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task ApplyCallbackAsync(PaymentSession session, PaymentIntent intent, DateTimeOffset? paidAt, CancellationToken cancellationToken = default)
        {
            var attempt = intent.CurrentAttempt ?? throw ExceptionFactory.NoProviderAttempt(intent.Id);
            var profile = await ProfileAsync(attempt.ProviderProfileId, cancellationToken);

            await ApplyEvidenceAsync(session, intent, attempt, profile, paidAt ?? _clock.GetDateTime(), fromCallback: true, cancellationToken);
        }

        public async Task ReconcileAsync(PaymentSession session, PaymentIntent intent, CancellationToken cancellationToken = default)
        {
            var attempt = intent.CurrentAttempt ?? throw ExceptionFactory.NoProviderAttempt(intent.Id);
            var profile = await ProfileAsync(attempt.ProviderProfileId, cancellationToken);

            switch (attempt.Status)
            {
                case ProviderPaymentAttemptStatus.Created when intent.AwaitsDispatch:
                    await DispatchAsync(session, intent, null, cancellationToken);
                    return;
                case ProviderPaymentAttemptStatus.Verified when profile.RequiresSettlementAfterVerify && intent.IsOpen:
                    await SettleAsync(session, intent, attempt, profile, cancellationToken);
                    return;
                case ProviderPaymentAttemptStatus.VerificationPending when intent.IsOpen && !session.IsTerminal:
                    await VerifyAsync(session, intent, attempt, profile, cancellationToken);
                    return;
                case ProviderPaymentAttemptStatus.CustomerActionPending
                    or ProviderPaymentAttemptStatus.CallbackReceived
                    or ProviderPaymentAttemptStatus.VerificationPending
                    or ProviderPaymentAttemptStatus.Unknown:
                    await InquireAsync(session, intent, attempt, profile, cancellationToken);
                    return;
            }
        }

        public async Task ResolveAbandonedAttemptAsync(PaymentSession session, PaymentIntent intent, DateTimeOffset abandonedAt, CancellationToken cancellationToken = default)
        {
            if (intent.CurrentAttempt is not { Status: ProviderPaymentAttemptStatus.CustomerActionPending } attempt)
                return;

            var profile = await ProfileAsync(attempt.ProviderProfileId, cancellationToken);

            if (profile.SupportsInquiry)
            {
                await InquireAsync(session, intent, attempt, profile, cancellationToken);
                return;
            }

            session.RecordProviderUnknown(intent, attempt, null, profile.AutoReversalBoundary(abandonedAt), _idGenerator, _clock.GetDateTime());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task InquireAsync(
            PaymentSession session,
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            ProviderProfile profile,
            CancellationToken cancellationToken)
        {
            if (!profile.SupportsInquiry)
                return;

            var result = await _providers.Resolve(intent.TenderType).InquireAsync(OperationRequest(intent, attempt), cancellationToken);
            var now = _clock.GetDateTime();

            switch (result.Kind)
            {
                case ProviderResultKind.Succeeded:
                    await ApplyEvidenceAsync(session, intent, attempt, profile, result.PaidAt ?? now, fromCallback: false, cancellationToken);
                    return;
                case ProviderResultKind.NoEffect:
                    session.RecordNoProviderEffect(intent, attempt, result.FailureCode, result.FailureReason, _idGenerator, now);
                    break;
                case ProviderResultKind.NotYetPaid when intent.Status != PaymentIntentStatus.RequiresCustomerAction || intent.IsCustomerActionLapsedAt(now):
                    session.RecordNoProviderEffect(intent, attempt, IntentFailureCode.NoProviderEffect, "The customer did not pay before the payment page lapsed.", _idGenerator, now);
                    break;
                case ProviderResultKind.Declined:
                    session.RecordProviderDeclined(intent, attempt, result.FailureCode, result.FailureReason, _idGenerator, now);
                    break;
                default:
                    return;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task ApplyEvidenceAsync(
            PaymentSession session,
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            ProviderProfile profile,
            DateTimeOffset paidAt,
            bool fromCallback,
            CancellationToken cancellationToken)
        {
            var superseded = await _sessions.HasNewerCommercialVersionAsync(session.OrderId, session.CommercialVersion, cancellationToken);
            var decision = session.RecordPaymentEvidence(intent, attempt, profile, paidAt, fromCallback, superseded, _idGenerator, _clock.GetDateTime());

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            switch (decision)
            {
                case PaymentEvidenceDecision.Verify:
                    await VerifyAsync(session, intent, attempt, profile, cancellationToken);
                    break;
                case PaymentEvidenceDecision.AlreadyVerified
                    when attempt.Status == ProviderPaymentAttemptStatus.Verified && profile.RequiresSettlementAfterVerify && intent.IsOpen:
                    await SettleAsync(session, intent, attempt, profile, cancellationToken);
                    break;
            }
        }

        private async Task VerifyAsync(
            PaymentSession session,
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            ProviderProfile profile,
            CancellationToken cancellationToken)
        {
            session.BeginVerification(intent, attempt, _idGenerator, _clock.GetDateTime());
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var result = await _providers.Resolve(intent.TenderType).VerifyAsync(OperationRequest(intent, attempt), cancellationToken);
            var now = _clock.GetDateTime();

            switch (result.Kind)
            {
                case ProviderResultKind.Succeeded:
                    session.RecordVerified(intent, attempt, result.ProviderTransactionRef, profile.RequiresSettlementAfterVerify, _idGenerator, now);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    if (profile.RequiresSettlementAfterVerify)
                        await SettleAsync(session, intent, attempt, profile, cancellationToken);

                    return;
                case ProviderResultKind.Declined or ProviderResultKind.NoEffect or ProviderResultKind.NotYetPaid:
                    session.RecordNoProviderEffect(
                        intent,
                        attempt,
                        result.FailureCode ?? IntentFailureCode.NoProviderEffect,
                        result.FailureReason ?? "The provider did not confirm the payment.",
                        _idGenerator,
                        now);
                    break;
                default:
                    session.RecordProviderUnknown(
                        intent,
                        attempt,
                        result.ProviderTransactionRef,
                        profile.SupportsInquiry ? null : profile.AutoReversalAt(attempt.VerifyDeadline ?? now),
                        _idGenerator,
                        now);
                    break;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task SettleAsync(
            PaymentSession session,
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            ProviderProfile profile,
            CancellationToken cancellationToken)
        {
            var result = await _providers.Resolve(intent.TenderType).SettleAsync(OperationRequest(intent, attempt), cancellationToken);

            if (result.Kind != ProviderResultKind.Succeeded)
                return;

            session.RecordSettled(intent, attempt, _idGenerator, _clock.GetDateTime());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task ApplyStartResultAsync(
            PaymentSession session,
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            ProviderProfile profile,
            ProviderResult result,
            CancellationToken cancellationToken)
        {
            var now = _clock.GetDateTime();

            switch (result.Kind)
            {
                case ProviderResultKind.CustomerActionRequired:
                    session.RecordCustomerActionRequired(intent, attempt, result.CustomerAction!, result.ProviderTransactionRef, _idGenerator, now);
                    break;
                case ProviderResultKind.Succeeded:
                    session.RecordVerified(intent, attempt, result.ProviderTransactionRef, profile.RequiresSettlementAfterVerify, _idGenerator, now);

                    if (profile.RequiresSettlementAfterVerify)
                    {
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        await SettleAsync(session, intent, attempt, profile, cancellationToken);
                    }

                    break;
                case ProviderResultKind.Declined:
                    session.RecordProviderDeclined(intent, attempt, result.FailureCode, result.FailureReason, _idGenerator, now);
                    break;
                case ProviderResultKind.UnavailableBeforeEffect or ProviderResultKind.NoEffect:
                    session.RecordRouteUnavailable(intent, attempt, result.FailureCode, result.FailureReason, now);
                    break;
                default:
                    session.RecordProviderUnknown(
                        intent,
                        attempt,
                        result.ProviderTransactionRef,
                        profile.SupportsInquiry ? null : profile.AutoReversalBoundary(now),
                        _idGenerator,
                        now);
                    break;
            }
        }

        private async Task<ProviderProfile?> NextRouteAsync(PaymentIntent intent, CancellationToken cancellationToken)
            => (await _profiles.ListActiveAsync(intent.TenderType, intent.CurrencyId, cancellationToken))
                .Where(profile => profile.Accepts(intent.CurrencyId, intent.RequestedAmount) && !intent.HasTriedProfile(profile.Id))
                .OrderBy(profile => profile.Id, StringComparer.Ordinal)
                .FirstOrDefault();

        private async Task<ProviderProfile> ProfileAsync(string providerProfileId, CancellationToken cancellationToken)
            => await _profiles.FindAsync(providerProfileId, cancellationToken)
               ?? throw ExceptionFactory.ProviderProfileNotFound(providerProfileId);

        private static ProviderStartRequest StartRequest(PaymentSession session, PaymentIntent intent, ProviderPaymentAttempt attempt, string? returnUrl)
            => new(
                attempt.IdempotencyKey,
                attempt.ProviderProfileId,
                session.Id,
                intent.Id,
                intent.RequestedAmount,
                intent.CurrencyId,
                intent.FundingReference,
                returnUrl,
                session.ExpiresAt);

        private static ProviderOperationRequest OperationRequest(PaymentIntent intent, ProviderPaymentAttempt attempt)
            => new(
                attempt.IdempotencyKey,
                attempt.ProviderProfileId,
                attempt.ProviderTransactionRef,
                intent.RequestedAmount,
                intent.CurrencyId);
    }
}
