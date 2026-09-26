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

        public Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default)
        {
            var (debit, failureCode) = _ledger.DebitWallet(request.IdempotencyKey, request.FundingReference ?? string.Empty, request.Amount, _clock.GetDateTime());

            return Task.FromResult(debit is not null
                ? TenderOutcome.Captured(debit.Amount, $"{debit.Reference}|{debit.Key}")
                : TenderOutcome.Failed(failureCode!, failureCode == "InsufficientFunds"
                    ? "The wallet balance does not cover the amount."
                    : "The wallet is not available."));
        }

        public Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.FindOperation(request.IdempotencyKey) is { Kind: MockOperationKind.WalletDebit } debit
                ? TenderOutcome.Captured(debit.Amount, $"{debit.Reference}|{debit.Key}")
                : TenderOutcome.Failed("NoProviderEffect", "The wallet has no debit for this attempt."));

        public Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenderOutcome.Released());
    }
}
