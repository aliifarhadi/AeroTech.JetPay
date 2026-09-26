using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Mock.Tenders
{
    public sealed class MockCreditProvider : ITenderProvider
    {
        private readonly MockFundingLedger _ledger;
        private readonly IClock _clock;

        public MockCreditProvider(TenderType tenderType, MockFundingLedger ledger, IClock clock)
        {
            TenderType = tenderType;
            _ledger = ledger;
            _clock = clock;
        }

        public TenderType TenderType { get; }

        public Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default)
        {
            var (reservation, failureCode) = _ledger.ReserveCredit(request.IdempotencyKey, request.FundingReference ?? string.Empty, request.Amount, _clock.GetDateTime());

            return Task.FromResult(reservation is not null
                ? Authorized(reservation)
                : TenderOutcome.Failed(failureCode!, failureCode == "InsufficientFunds"
                    ? "The credit facility does not have enough available exposure."
                    : "The credit facility is not available."));
        }

        public Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledger.FindOperation(request.IdempotencyKey) is { Kind: MockOperationKind.CreditReservation, Released: false } reservation
                ? Authorized(reservation)
                : TenderOutcome.Failed("NoProviderEffect", "The credit facility has no reservation for this attempt."));

        public Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default)
        {
            _ledger.Release(request.PaymentIntentId);
            return Task.FromResult(TenderOutcome.Released());
        }

        private TenderOutcome Authorized(MockOperation reservation)
            => TenderOutcome.Authorized(
                reservation.Amount,
                issuanceGuarantee: true,
                _ledger.FindCreditFacility(reservation.Reference)?.AuthorizationValiditySeconds is { } seconds
                    ? reservation.CreatedAt.AddSeconds(seconds)
                    : null,
                $"{reservation.Reference}|{reservation.Key}");
    }
}
