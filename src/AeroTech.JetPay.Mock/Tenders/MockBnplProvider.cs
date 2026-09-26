using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    public sealed class MockBnplProvider : ITenderProvider
    {
        private readonly MockFundingLedger _ledger;
        private readonly MockJetPayOptions _options;
        private readonly IClock _clock;

        public MockBnplProvider(MockFundingLedger ledger, IOptions<MockJetPayOptions> options, IClock clock)
        {
            _ledger = ledger;
            _options = options.Value;
            _clock = clock;
        }

        public TenderType TenderType => TenderType.Bnpl;

        public Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default)
        {
            var application = _ledger.OpenBnplApplication(request.IdempotencyKey, request.Amount, _clock.GetDateTime());
            return Task.FromResult(TenderOutcome.RequiresCustomerAction(Redirect(application, request.SessionExpiresAt), application.Reference));
        }

        public Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default)
        {
            if (_ledger.FindOperation(request.IdempotencyKey) is not { Kind: MockOperationKind.BnplApplication } application || application.Released)
                return Task.FromResult(TenderOutcome.Failed("NoProviderEffect", "The BNPL provider has no application for this attempt."));

            return Task.FromResult(application.CustomerOutcome switch
            {
                MockCustomerOutcome.Approved => TenderOutcome.Authorized(
                    application.Amount,
                    issuanceGuarantee: true,
                    _clock.GetDateTime().AddSeconds(_options.BnplGuaranteeValiditySeconds),
                    application.Reference),
                MockCustomerOutcome.Declined => TenderOutcome.Failed("Declined", "The BNPL provider rejected the instalment plan.", application.Reference),
                _ => TenderOutcome.RequiresCustomerAction(Redirect(application, null), application.Reference)
            });
        }

        public Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default)
        {
            _ledger.Release(request.PaymentIntentId);
            return Task.FromResult(TenderOutcome.Released());
        }

        private CustomerAction Redirect(MockOperation application, DateTimeOffset? sessionExpiresAt)
        {
            var expiresAt = application.CreatedAt.AddSeconds(_options.CustomerActionTtlSeconds);

            if (sessionExpiresAt < expiresAt)
                expiresAt = sessionExpiresAt.Value;

            return CustomerAction.Redirect(
                $"{_options.PublicBaseUrl.TrimEnd('/')}/Mock/v1/Bnpl/Apply/{Uri.EscapeDataString(application.Key)}",
                expiresAt);
        }
    }
}
