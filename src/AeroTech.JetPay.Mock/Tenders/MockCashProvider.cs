using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Mock.Tenders
{
    public sealed class MockCashProvider : ITenderProvider
    {
        private readonly MockFundingLedger _ledger;
        private readonly IClock _clock;

        public MockCashProvider(MockFundingLedger ledger, IClock clock)
        {
            _ledger = ledger;
            _clock = clock;
        }

        public TenderType TenderType => TenderType.Cash;

        public Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default)
        {
            if (_ledger.FindOperation(request.IdempotencyKey) is { Kind: MockOperationKind.CashReceipt } recorded)
                return Task.FromResult(TenderOutcome.Captured(recorded.Amount, $"{recorded.Reference}|{recorded.Key}"));

            if (request.OfficeId is not { } officeId || !_ledger.IsCashAccepted(officeId, request.SalesChannel))
                return Task.FromResult(TenderOutcome.Failed("CashNotAccepted", "Cash is not accepted at this office or channel."));

            var receipt = _ledger.RecordCashReceipt(request.IdempotencyKey, officeId, request.Amount, _clock.GetDateTime());
            return Task.FromResult(TenderOutcome.Captured(receipt.Amount, $"{receipt.Reference}|{receipt.Key}"));
        }

        public Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.FindOperation(request.IdempotencyKey) is { Kind: MockOperationKind.CashReceipt } receipt
                ? TenderOutcome.Captured(receipt.Amount, $"{receipt.Reference}|{receipt.Key}")
                : TenderOutcome.Failed("NoProviderEffect", "No cash receipt was recorded for this attempt."));

        public Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenderOutcome.Released());
    }
}
