using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Views
{
    public static class PaymentIntentViewMapper
    {
        public static PaymentIntentView ToView(this PaymentIntent intent)
            => new(
                intent.Id,
                intent.PayableInstructionId,
                intent.OrderId,
                intent.OrderReference,
                intent.CommercialVersion,
                intent.Purpose,
                intent.PayerType,
                intent.PayerId,
                intent.RequestedAmount,
                intent.CurrencyId,
                intent.RequiredGuarantee,
                intent.CaptureMode,
                intent.Status,
                intent.AuthorizedAmount,
                intent.GuaranteedAmount,
                intent.CapturedAmount,
                intent.RefundedAmount,
                intent.GuaranteeExpiresAt,
                intent.IntentExpiresAt,
                intent.SelectedTenderType,
                intent.SelectedPaymentMethodOptionId,
                intent.NextAction?.ToView(),
                intent.FailureCode,
                intent.FailureReason,
                intent.Version,
                intent.CreatedAt,
                intent.UpdatedAt);

        private static CustomerActionView ToView(this CustomerAction action)
            => new(action.Type, action.Url, action.HttpMethod, action.FormFields, action.ExpiresAt);
    }
}
