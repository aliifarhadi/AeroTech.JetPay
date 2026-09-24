using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    /// <summary>
    /// Mock eligibility policy for the Stage-3 tender families. Option ids are opaque and stable; no provider code or
    /// route is exposed.
    /// </summary>
    public sealed class MockPaymentMethodOptionCatalog : IPaymentMethodOptionCatalog
    {
        public const string IranianPgwOptionId = "iranian-pgw";
        public const string StoredValueOptionId = "stored-value";
        public const string BnplOptionId = "bnpl";
        public const string AgencyCreditOptionId = "agency-credit";

        private readonly MockJetPayOptions _options;

        public MockPaymentMethodOptionCatalog(IOptions<MockJetPayOptions> options) => _options = options.Value;

        public Task<IReadOnlyList<PaymentMethodOption>> ResolveAsync(PaymentMethodEligibilityQuery query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PaymentMethodOption> options = query.PayerType switch
            {
                PayerType.Customer => [IranianPgw(), StoredValue(), Bnpl()],
                PayerType.Organization => [IranianPgw(), StoredValue()],
                PayerType.Agency => [AgencyCredit(), IranianPgw()],
                _ => []
            };

            return Task.FromResult(options);
        }

        private static PaymentMethodOption IranianPgw()
            => new(
                IranianPgwOptionId,
                TenderType.IranianPgw,
                nameof(TenderType.IranianPgw),
                CustomerActionType.Redirect,
                [RequiredGuarantee.PaidBeforeIssuance],
                [PaymentCaptureMode.Automatic]);

        private static PaymentMethodOption StoredValue()
            => new(
                StoredValueOptionId,
                TenderType.StoredValue,
                nameof(TenderType.StoredValue),
                CustomerActionType.None,
                [RequiredGuarantee.PaidBeforeIssuance],
                [PaymentCaptureMode.Automatic]);

        private PaymentMethodOption Bnpl()
            => new(
                BnplOptionId,
                TenderType.Bnpl,
                nameof(TenderType.Bnpl),
                CustomerActionType.Redirect,
                GuaranteesFor(_options.Bnpl),
                [PaymentCaptureMode.Manual]);

        private PaymentMethodOption AgencyCredit()
            => new(
                AgencyCreditOptionId,
                TenderType.AgencyCredit,
                nameof(TenderType.AgencyCredit),
                CustomerActionType.None,
                GuaranteesFor(_options.AgencyCredit),
                [PaymentCaptureMode.Manual]);

        private static IReadOnlyList<RequiredGuarantee> GuaranteesFor(MockTenderProfileOptions profile)
            => profile.SupportsIssuanceGuaranteeOnAuthorization
                ? [RequiredGuarantee.AuthorizedBeforeIssuance, RequiredGuarantee.PaidBeforeIssuance]
                : [RequiredGuarantee.PaidBeforeIssuance];
    }
}
