using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;
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

        public Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default)
        {
            if (_ledger.FindOperation(request.IdempotencyKey) is { } existing)
                return Task.FromResult(Replay(existing, request.SessionExpiresAt));

            foreach (var route in _ledger.PgwRoutes(request.CurrencyId))
            {
                if (request.Amount < route.MinimumAmount || request.Amount > route.MaximumAmount)
                {
                    _ledger.RecordRouteAttempt(request.PaymentIntentId, route.Code, "AmountOutOfRange");
                    continue;
                }

                if (route.Mode == MockPgwMode.UnavailableBeforeEffect)
                {
                    _ledger.RecordRouteAttempt(request.PaymentIntentId, route.Code, "UnavailableBeforeEffect");
                    continue;
                }

                var transaction = _ledger.OpenPgwTransaction(request.IdempotencyKey, route.Code, request.Amount, _clock.GetDateTime());

                if (route.Mode == MockPgwMode.UnknownAfterEffect)
                {
                    _ledger.RecordRouteAttempt(request.PaymentIntentId, route.Code, "UnknownAfterEffect");
                    return Task.FromResult(TenderOutcome.Processing(transaction.Reference));
                }

                _ledger.RecordRouteAttempt(request.PaymentIntentId, route.Code, "TokenIssued");
                return Task.FromResult(TenderOutcome.RequiresCustomerAction(Redirect(transaction, request.SessionExpiresAt), transaction.Reference));
            }

            return Task.FromResult(TenderOutcome.Failed("ProviderUnavailable", "No payment gateway route is available for this payment."));
        }

        public Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default)
        {
            if (_ledger.FindOperation(request.IdempotencyKey) is not { Kind: MockOperationKind.PgwTransaction } transaction || transaction.Released)
                return Task.FromResult(TenderOutcome.Failed("NoProviderEffect", "The gateway has no payment for this attempt."));

            var route = _ledger.PgwProfile(transaction.RouteCode!);

            if (route?.Mode == MockPgwMode.UnknownAfterEffect && transaction.CustomerOutcome is null)
                return Task.FromResult(route.InquiryOutcome == MockInquiryOutcome.Captured
                    ? Settle(transaction)
                    : TenderOutcome.Failed("NoProviderEffect", "Inquiry proved the gateway never charged this attempt.", transaction.Reference));

            return Task.FromResult(transaction.CustomerOutcome switch
            {
                MockCustomerOutcome.Approved => Settle(transaction),
                MockCustomerOutcome.Declined => TenderOutcome.Failed("Declined", "The bank declined the payment during verification.", transaction.Reference),
                _ => TenderOutcome.RequiresCustomerAction(Redirect(transaction, null), transaction.Reference)
            });
        }

        public Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default)
        {
            if (_ledger.FindOperation(request.PaymentIntentId)?.CustomerOutcome != MockCustomerOutcome.Approved)
                _ledger.Release(request.PaymentIntentId);

            return Task.FromResult(TenderOutcome.Released());
        }

        private TenderOutcome Settle(MockOperation transaction)
        {
            _ledger.MarkSettled(transaction.Key);
            return TenderOutcome.Captured(transaction.Amount, transaction.Reference);
        }

        private TenderOutcome Replay(MockOperation transaction, DateTimeOffset? sessionExpiresAt)
            => _ledger.PgwProfile(transaction.RouteCode!)?.Mode == MockPgwMode.UnknownAfterEffect
                ? TenderOutcome.Processing(transaction.Reference)
                : TenderOutcome.RequiresCustomerAction(Redirect(transaction, sessionExpiresAt), transaction.Reference);

        private CustomerAction Redirect(MockOperation transaction, DateTimeOffset? sessionExpiresAt)
        {
            var expiresAt = transaction.CreatedAt.AddSeconds(_options.CustomerActionTtlSeconds);

            if (sessionExpiresAt < expiresAt)
                expiresAt = sessionExpiresAt.Value;

            return CustomerAction.Redirect(
                $"{_options.PublicBaseUrl.TrimEnd('/')}/Mock/v1/Pgw/{transaction.RouteCode}/Pay/{Uri.EscapeDataString(transaction.Key)}",
                expiresAt);
        }
    }
}
