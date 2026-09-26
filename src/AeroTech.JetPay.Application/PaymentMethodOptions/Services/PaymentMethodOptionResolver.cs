using System.Security.Cryptography;
using System.Text;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Funding;
using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.PaymentMethodOptions.Services
{
    public sealed record PaymentEligibilityContext(
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContext Initiator,
        PaymentInteractionMode InteractionMode,
        decimal Amount,
        int CurrencyId,
        PaymentPurpose Purpose,
        PaymentAssuranceRequirement AssuranceRequirement)
    {
        public static PaymentEligibilityContext Of(PaymentSession session, decimal amount)
            => new(
                session.PayerType,
                session.PayerId,
                session.Initiator,
                session.InteractionMode,
                amount,
                session.CurrencyId,
                session.Purpose,
                session.AssuranceRequirement);
    }

    public interface IPaymentMethodOptionResolver
    {
        Task<IReadOnlyList<PaymentMethodOption>> ResolveAsync(PaymentEligibilityContext context, CancellationToken cancellationToken = default);
    }

    public sealed class PaymentMethodOptionResolver : IPaymentMethodOptionResolver
    {
        private static readonly IReadOnlyList<PaymentAssuranceRequirement> CapturedFundsSatisfy =
            [PaymentAssuranceRequirement.FundsReceived, PaymentAssuranceRequirement.IssuanceGuaranteed];

        private readonly IFundingCatalog _funding;
        private readonly IProviderProfileCatalog _profiles;

        public PaymentMethodOptionResolver(IFundingCatalog funding, IProviderProfileCatalog profiles)
        {
            _funding = funding;
            _profiles = profiles;
        }

        public async Task<IReadOnlyList<PaymentMethodOption>> ResolveAsync(PaymentEligibilityContext context, CancellationToken cancellationToken = default)
        {
            var options = new List<PaymentMethodOption>();

            options.AddRange(await StoredValueAsync(context, cancellationToken));

            if (await IranianPgwAsync(context, cancellationToken) is { } pgw)
                options.Add(pgw);

            return options.OrderByDescending(option => option.IsDefault).ToList();
        }

        private async Task<IEnumerable<PaymentMethodOption>> StoredValueAsync(PaymentEligibilityContext context, CancellationToken cancellationToken)
        {
            if ((await _profiles.ListActiveAsync(TenderType.StoredValue, context.CurrencyId, cancellationToken)).Count == 0)
                return [];

            var wallets = await _funding.ListStoredValueAccountsAsync(context.PayerType, context.PayerId, context.CurrencyId, cancellationToken);

            return wallets
                .Where(wallet => wallet.TenderType == TenderType.StoredValue && wallet.CurrencyId == context.CurrencyId && wallet.AvailableAmount > 0)
                .Select(wallet => Option(
                    context,
                    TenderType.StoredValue,
                    wallet.Reference,
                    wallet.AvailableAmount,
                    minimumAmount: null,
                    maximumAmount: null,
                    isDefault: wallet.IsDefault,
                    canAutoSelect: wallet.IsDefault && context.InteractionMode == PaymentInteractionMode.UnattendedApi,
                    CustomerActionType.None));
        }

        private async Task<PaymentMethodOption?> IranianPgwAsync(PaymentEligibilityContext context, CancellationToken cancellationToken)
        {
            if (context.InteractionMode != PaymentInteractionMode.CustomerInteractive)
                return null;

            var routes = (await _profiles.ListActiveAsync(TenderType.IranianPgw, context.CurrencyId, cancellationToken))
                .Where(profile => profile.Accepts(context.CurrencyId, context.Amount))
                .ToList();

            if (routes.Count == 0)
                return null;

            return Option(
                context,
                TenderType.IranianPgw,
                fundingReference: null,
                availableAmount: null,
                routes.Any(profile => profile.MinimumAmount is null) ? null : routes.Min(profile => profile.MinimumAmount),
                routes.Any(profile => profile.MaximumAmount is null) ? null : routes.Max(profile => profile.MaximumAmount),
                isDefault: false,
                canAutoSelect: false,
                CustomerActionType.Redirect);
        }

        private static PaymentMethodOption Option(
            PaymentEligibilityContext context,
            TenderType tenderType,
            string? fundingReference,
            decimal? availableAmount,
            decimal? minimumAmount,
            decimal? maximumAmount,
            bool isDefault,
            bool canAutoSelect,
            CustomerActionType customerActionType)
            => new(
                OptionId(context, tenderType, fundingReference),
                tenderType,
                tenderType.ToString(),
                context.CurrencyId,
                availableAmount,
                minimumAmount,
                maximumAmount,
                SupportsPartialAmount: false,
                isDefault,
                canAutoSelect,
                customerActionType,
                CapturedFundsSatisfy,
                ExpiresAt: null,
                fundingReference);

        private static string OptionId(PaymentEligibilityContext context, TenderType tenderType, string? fundingReference)
        {
            var binding = $"{context.PayerType}|{context.PayerId}|{tenderType}|{fundingReference}|{context.CurrencyId}";
            return $"opt_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(binding)))[..24].ToLowerInvariant()}";
        }
    }
}
