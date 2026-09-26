using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    public sealed class MockIranianPgwProvider : ITenderProvider
    {
        private readonly MockFundingLedger _ledger;
        private readonly MockJetPayOptions _options;
        private readonly IClock _clock;

        public MockIranianPgwProvider(MockFundingLedger ledger, IOptions<MockJetPayOptions> options, IClock clock)
        {
            _ledger = ledger;
            _options = options.Value;
            _clock = clock;
        }

        public TenderType TenderType => TenderType.IranianPgw;

        public Task<ProviderResult> StartAsync(ProviderStartRequest request, CancellationToken cancellationToken = default)
        {
            var profile = _ledger.Profile(request.ProviderProfileId);

            if (profile is null || !profile.Enabled || profile.Mode == MockPgwMode.UnavailableBeforeEffect)
            {
                _ledger.RecordRouteAttempt(request.PaymentIntentId, request.ProviderProfileId, "UnavailableBeforeEffect");
                return Task.FromResult(ProviderResult.UnavailableBeforeEffect());
            }

            var transaction = _ledger.OpenPgwTransaction(
                request.IdempotencyKey,
                request.ProviderProfileId,
                request.PaymentIntentId,
                request.Amount,
                _clock.GetDateTime());

            if (profile.Mode == MockPgwMode.UnknownAfterEffect)
            {
                _ledger.RecordRouteAttempt(request.PaymentIntentId, request.ProviderProfileId, "UnknownAfterEffect");
                return Task.FromResult(ProviderResult.Unknown(transaction.Reference));
            }

            _ledger.RecordRouteAttempt(request.PaymentIntentId, request.ProviderProfileId, "TokenIssued");
            return Task.FromResult(ProviderResult.CustomerActionRequired(Redirect(transaction, request.SessionExpiresAt), transaction.Reference));
        }

        public Task<ProviderResult> VerifyAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
        {
            var transaction = _ledger.VerifyPgw(request.IdempotencyKey, _clock.GetDateTime());

            return Task.FromResult(transaction switch
            {
                null => ProviderResult.NoEffect(),
                { ReturnedAt: not null } => ProviderResult.Declined("PaymentReturned", "The gateway returned the unverified payment to the payer.", transaction.Reference),
                { CustomerOutcome: MockCustomerOutcome.Declined } => ProviderResult.Declined("Declined", "The bank declined the payment.", transaction.Reference),
                { VerifiedAt: not null } => ProviderResult.Succeeded(transaction.Reference, transaction.PaidAt),
                _ => ProviderResult.NotYetPaid()
            });
        }

        public Task<ProviderResult> SettleAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.SettlePgw(request.IdempotencyKey, _clock.GetDateTime()) is { SettledAt: not null } transaction
                ? ProviderResult.Succeeded(transaction.Reference, transaction.PaidAt)
                : ProviderResult.Declined("SettlementRejected", "The gateway has no verified payment to settle."));

        public Task<ProviderResult> InquireAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (_ledger.Profile(request.ProviderProfileId) is not { SupportsInquiry: true })
                return Task.FromResult(ProviderResult.Unknown(request.ProviderTransactionRef));

            var transaction = _ledger.InquirePgw(request.IdempotencyKey, _clock.GetDateTime());

            return Task.FromResult(transaction switch
            {
                null => ProviderResult.NoEffect(),
                { ReturnedAt: not null } => ProviderResult.NoEffect(),
                { CustomerOutcome: MockCustomerOutcome.Declined } => ProviderResult.Declined("Declined", "The bank declined the payment.", transaction.Reference),
                { CustomerOutcome: MockCustomerOutcome.Paid } => ProviderResult.Succeeded(transaction.Reference, transaction.PaidAt),
                _ => ProviderResult.NotYetPaid()
            });
        }

        private CustomerAction Redirect(MockOperation transaction, DateTimeOffset? sessionExpiresAt)
        {
            var expiresAt = transaction.CreatedAt.AddSeconds(_options.CustomerActionTtlSeconds);

            if (sessionExpiresAt < expiresAt)
                expiresAt = sessionExpiresAt.Value;

            return CustomerAction.Redirect(
                $"{_options.PublicBaseUrl.TrimEnd('/')}/Mock/v1/Pgw/{transaction.ProviderProfileId}/Pay/{Uri.EscapeDataString(transaction.PaymentIntentId)}",
                expiresAt);
        }
    }
}
