using System.Security.Cryptography;
using System.Text;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Funding;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Application.PaymentMethodOptions.Services
{
    public sealed record PaymentEligibilityContext(
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContext Initiator,
        decimal Amount,
        int CurrencyId,
        PaymentPurpose Purpose,
        PaymentAssuranceRequirement AssuranceRequirement,
        PaymentInteractionMode InteractionMode)
    {
        public static PaymentEligibilityContext Of(PaymentSession session, decimal amount)
            => new(
                session.PayerType,
                session.PayerId,
                session.InitiatorContext,
                amount,
                session.CurrencyId,
                session.Purpose,
                session.AssuranceRequirement,
                session.InteractionMode);
    }

    public interface IPaymentMethodOptionResolver
    {
        Task<IReadOnlyList<PaymentMethodOption>> ResolveAsync(PaymentEligibilityContext context, CancellationToken cancellationToken = default);
    }

    public sealed class PaymentMethodOptionResolver : IPaymentMethodOptionResolver
    {
        private readonly IFundingCatalog _funding;
        private readonly PaymentAcceptancePolicyOptions _policy;

        public PaymentMethodOptionResolver(IFundingCatalog funding, IOptions<PaymentAcceptancePolicyOptions> policy)
        {
            _funding = funding;
            _policy = policy.Value;
        }

        public async Task<IReadOnlyList<PaymentMethodOption>> ResolveAsync(PaymentEligibilityContext context, CancellationToken cancellationToken = default)
        {
            var options = new List<PaymentMethodOption>();

            options.AddRange(await StoredValueAsync(context, cancellationToken));
            options.AddRange(await CreditAsync(context, cancellationToken));

            if (await CashAsync(context, cancellationToken) is { } cash)
                options.Add(cash);

            if (await IranianPgwAsync(context, cancellationToken) is { } pgw)
                options.Add(pgw);

            if (await BnplAsync(context, cancellationToken) is { } bnpl)
                options.Add(bnpl);

            return options.OrderByDescending(option => option.IsDefault).ToList();
        }

        private async Task<IEnumerable<PaymentMethodOption>> StoredValueAsync(PaymentEligibilityContext context, CancellationToken cancellationToken)
        {
            var wallets = await _funding.ListStoredValueAccountsAsync(context.PayerType, context.PayerId, context.CurrencyId, cancellationToken);

            return wallets
                .Where(wallet => wallet.CurrencyId == context.CurrencyId && wallet.AvailableAmount > 0)
                .Select(wallet => Option(
                    context,
                    TenderType.StoredValue,
                    wallet.Reference,
                    wallet.AvailableAmount,
                    minimumAmount: null,
                    maximumAmount: null,
                    supportsPartialAmount: true,
                    isDefault: wallet.IsDefault,
                    canAutoSelect: wallet.IsDefault && context.InteractionMode == PaymentInteractionMode.UnattendedApi,
                    CustomerActionType.None,
                    PaymentAssuranceCapability.FundsReceived,
                    PaymentCaptureMode.Automatic));
        }

        private async Task<IEnumerable<PaymentMethodOption>> CreditAsync(PaymentEligibilityContext context, CancellationToken cancellationToken)
        {
            if (context.InteractionMode == PaymentInteractionMode.CustomerInteractive
                || context.AssuranceRequirement != PaymentAssuranceRequirement.IssuanceGuaranteed
                || CreditTenderFor(context.PayerType) is not { } creditTender)
                return [];

            var facilities = await _funding.ListCreditFacilitiesAsync(context.PayerType, context.PayerId, context.CurrencyId, cancellationToken);

            return facilities
                .Where(facility => facility.TenderType == creditTender && facility.CurrencyId == context.CurrencyId && facility.AvailableAmount > 0)
                .Select(facility => Option(
                    context,
                    creditTender,
                    facility.Reference,
                    facility.AvailableAmount,
                    minimumAmount: null,
                    maximumAmount: null,
                    supportsPartialAmount: true,
                    isDefault: false,
                    canAutoSelect: false,
                    CustomerActionType.None,
                    PaymentAssuranceCapability.CommitmentToPay,
                    PaymentCaptureMode.Manual));
        }

        private async Task<PaymentMethodOption?> CashAsync(PaymentEligibilityContext context, CancellationToken cancellationToken)
        {
            if (context.InteractionMode != PaymentInteractionMode.StaffAssisted || context.Initiator.OfficeId is not { } officeId)
                return null;

            if (!await _funding.IsCashAcceptedAsync(officeId, context.Initiator.SalesChannel, cancellationToken))
                return null;

            return Option(
                context,
                TenderType.Cash,
                $"office:{officeId}",
                availableAmount: null,
                minimumAmount: null,
                maximumAmount: null,
                supportsPartialAmount: true,
                isDefault: false,
                canAutoSelect: false,
                CustomerActionType.None,
                PaymentAssuranceCapability.FundsReceived,
                PaymentCaptureMode.Automatic);
        }

        private async Task<PaymentMethodOption?> IranianPgwAsync(PaymentEligibilityContext context, CancellationToken cancellationToken)
        {
            var customerCanAct = context.InteractionMode == PaymentInteractionMode.CustomerInteractive
                                 || (context.InteractionMode == PaymentInteractionMode.StaffAssisted && _policy.AllowPgwForStaffAssisted);

            if (!customerCanAct || await _funding.FindPgwAvailabilityAsync(context.CurrencyId, cancellationToken) is not { } availability)
                return null;

            return Option(
                context,
                TenderType.IranianPgw,
                fundingReference: null,
                availableAmount: null,
                availability.MinimumAmount,
                availability.MaximumAmount,
                supportsPartialAmount: true,
                isDefault: false,
                canAutoSelect: false,
                CustomerActionType.Redirect,
                PaymentAssuranceCapability.FundsReceived,
                PaymentCaptureMode.Automatic);
        }

        private async Task<PaymentMethodOption?> BnplAsync(PaymentEligibilityContext context, CancellationToken cancellationToken)
        {
            if (context.InteractionMode != PaymentInteractionMode.CustomerInteractive
                || context.PayerType != PayerType.Customer
                || context.Purpose != PaymentPurpose.InitialSale
                || context.AssuranceRequirement != PaymentAssuranceRequirement.IssuanceGuaranteed)
                return null;

            if (await _funding.FindBnplAvailabilityAsync(context.PayerType, context.PayerId, context.CurrencyId, cancellationToken) is not { } availability)
                return null;

            if (context.Amount < availability.MinimumAmount || context.Amount > availability.MaximumAmount)
                return null;

            return Option(
                context,
                TenderType.Bnpl,
                fundingReference: null,
                availableAmount: null,
                availability.MinimumAmount,
                availability.MaximumAmount,
                supportsPartialAmount: false,
                isDefault: false,
                canAutoSelect: false,
                CustomerActionType.Redirect,
                PaymentAssuranceCapability.CommitmentToPay,
                PaymentCaptureMode.Manual);
        }

        private static TenderType? CreditTenderFor(PayerType payerType) => payerType switch
        {
            PayerType.Agency => TenderType.AgencyCredit,
            PayerType.Corporate => TenderType.CorporateCredit,
            PayerType.Customer => TenderType.CustomerCredit,
            _ => null
        };

        private static PaymentMethodOption Option(
            PaymentEligibilityContext context,
            TenderType tenderType,
            string? fundingReference,
            decimal? availableAmount,
            decimal? minimumAmount,
            decimal? maximumAmount,
            bool supportsPartialAmount,
            bool isDefault,
            bool canAutoSelect,
            CustomerActionType customerActionType,
            PaymentAssuranceCapability assuranceCapability,
            PaymentCaptureMode captureMode)
            => new(
                OptionId(context, tenderType, fundingReference),
                tenderType,
                tenderType.ToString(),
                context.CurrencyId,
                availableAmount,
                minimumAmount,
                maximumAmount,
                supportsPartialAmount,
                isDefault,
                canAutoSelect,
                customerActionType,
                assuranceCapability,
                captureMode,
                ExpiresAt: null,
                fundingReference);

        private static string OptionId(PaymentEligibilityContext context, TenderType tenderType, string? fundingReference)
        {
            var binding = $"{context.PayerType}|{context.PayerId}|{tenderType}|{fundingReference}|{context.CurrencyId}";
            return $"opt_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(binding)))[..24].ToLowerInvariant()}";
        }
    }
}
