using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Mock.Tenders
{
    public sealed class MockStoredValueProvider : ITenderProvider
    {
        private readonly MockFundingLedger _ledger;
        private readonly IClock _clock;

        public MockStoredValueProvider(MockFundingLedger ledger, IClock clock)
        {
            _ledger = ledger;
            _clock = clock;
        }

        public TenderType TenderType => TenderType.StoredValue;

        public Task<ProviderResult> StartAsync(ProviderStartRequest request, CancellationToken cancellationToken = default)
        {
            var (debit, failureCode) = _ledger.DebitWallet(
                request.IdempotencyKey,
                request.PaymentIntentId,
                request.FundingReference ?? string.Empty,
                request.Amount,
                _clock.GetDateTime());

            return Task.FromResult(debit is not null
                ? ProviderResult.Succeeded(debit.Key, debit.PaidAt)
                : ProviderResult.Declined(failureCode!, failureCode == "InsufficientFunds"
                    ? "The wallet balance does not cover the amount."
                    : "The wallet is not available."));
        }

        public Task<ProviderResult> VerifyAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
            => InquireAsync(request, cancellationToken);

        public Task<ProviderResult> SettleAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
            => InquireAsync(request, cancellationToken);

        public Task<ProviderResult> InquireAsync(ProviderOperationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.FindOperation(request.IdempotencyKey) is { Kind: MockOperationKind.WalletDebit } debit
                ? ProviderResult.Succeeded(debit.Key, debit.PaidAt)
                : ProviderResult.NoEffect());
    }
}
