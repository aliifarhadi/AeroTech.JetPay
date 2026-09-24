using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CancelPaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CapturePaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ConfirmPaymentIntent;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CreatePaymentIntent;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.RestApi.V1.PaymentIntentAggregate.Requests
{
    public sealed record CreatePaymentIntentRequest(
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        RequiredGuarantee RequiredGuarantee,
        PaymentCaptureMode CaptureMode,
        DateTimeOffset? IntentExpiresAt)
    {
        public CreatePaymentIntentCommand ToCommand(string idempotencyKey)
            => new(
                idempotencyKey,
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                PayerType,
                PayerId,
                Amount,
                CurrencyId,
                RequiredGuarantee,
                CaptureMode,
                IntentExpiresAt);
    }

    public sealed record ConfirmPaymentIntentRequest(string PaymentMethodOptionId, string? ReturnUrl)
    {
        public ConfirmPaymentIntentCommand ToCommand(string idempotencyKey, string paymentIntentId)
            => new(idempotencyKey, paymentIntentId, PaymentMethodOptionId, ReturnUrl);
    }

    public sealed record CapturePaymentIntentRequest(decimal? Amount, bool FinalCapture)
    {
        public CapturePaymentIntentCommand ToCommand(string idempotencyKey, string paymentIntentId)
            => new(idempotencyKey, paymentIntentId, Amount, FinalCapture);
    }

    public sealed record CancelPaymentIntentRequest(PaymentCancellationReason Reason)
    {
        public CancelPaymentIntentCommand ToCommand(string idempotencyKey, string paymentIntentId)
            => new(idempotencyKey, paymentIntentId, Reason);
    }
}
