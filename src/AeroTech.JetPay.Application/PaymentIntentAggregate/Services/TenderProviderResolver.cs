using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Services
{
    public interface ITenderProviderResolver
    {
        ITenderProvider Resolve(TenderType tenderType);
    }

    public sealed class TenderProviderResolver : ITenderProviderResolver
    {
        private readonly IReadOnlyDictionary<TenderType, ITenderProvider> _providers;

        public TenderProviderResolver(IEnumerable<ITenderProvider> providers)
            => _providers = providers.ToDictionary(provider => provider.TenderType);

        public ITenderProvider Resolve(TenderType tenderType)
            => _providers.TryGetValue(tenderType, out var provider)
                ? provider
                : throw ExceptionFactory.TenderProviderNotRegistered(tenderType);
    }
}
