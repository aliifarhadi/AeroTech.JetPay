using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions
{
    public sealed record ResolvePaymentMethodOptionsQuery(
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        string SalesChannel) : IRequest<IReadOnlyList<PaymentMethodOptionView>>;

    public sealed record PaymentMethodOptionView(
        string Id,
        TenderType TenderType,
        string DisplayCode,
        CustomerActionType CustomerActionType,
        IReadOnlyList<RequiredGuarantee> SupportedGuarantees,
        IReadOnlyList<PaymentCaptureMode> SupportedCaptureModes);
}
