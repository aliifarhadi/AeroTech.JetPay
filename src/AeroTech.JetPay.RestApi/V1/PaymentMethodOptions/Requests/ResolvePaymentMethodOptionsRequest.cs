using AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.RestApi.V1.PaymentMethodOptions.Requests
{
    public sealed record ResolvePaymentMethodOptionsRequest(
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        string SalesChannel)
    {
        public ResolvePaymentMethodOptionsQuery ToQuery()
            => new(OrderId, OrderReference, CommercialVersion, PayerType, PayerId, Amount, CurrencyId, SalesChannel);
    }
}
