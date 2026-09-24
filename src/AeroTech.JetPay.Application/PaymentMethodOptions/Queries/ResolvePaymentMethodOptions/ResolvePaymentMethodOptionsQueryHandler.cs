using AeroTech.JetPay.Domain.Providers.Tenders;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions
{
    public sealed class ResolvePaymentMethodOptionsQueryHandler
        : IRequestHandler<ResolvePaymentMethodOptionsQuery, IReadOnlyList<PaymentMethodOptionView>>
    {
        private readonly IPaymentMethodOptionCatalog _catalog;

        public ResolvePaymentMethodOptionsQueryHandler(IPaymentMethodOptionCatalog catalog) => _catalog = catalog;

        public async Task<IReadOnlyList<PaymentMethodOptionView>> Handle(ResolvePaymentMethodOptionsQuery query, CancellationToken cancellationToken)
        {
            var options = await _catalog.ResolveAsync(
                new PaymentMethodEligibilityQuery(
                    query.OrderId,
                    query.OrderReference,
                    query.CommercialVersion,
                    query.PayerType,
                    query.PayerId,
                    query.Amount,
                    query.CurrencyId,
                    query.SalesChannel),
                cancellationToken);

            return options
                .Select(option => new PaymentMethodOptionView(
                    option.Id,
                    option.TenderType,
                    option.DisplayCode,
                    option.CustomerActionType,
                    option.SupportedGuarantees,
                    option.SupportedCaptureModes))
                .ToList();
        }
    }
}
