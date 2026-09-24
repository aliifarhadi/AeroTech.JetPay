using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ConfirmPaymentIntent
{
    public sealed record ConfirmPaymentIntentCommand(
        string IdempotencyKey,
        string PaymentIntentId,
        string PaymentMethodOptionId,
        string? ReturnUrl) : IRequest<PaymentIntentView>
    {
        public string Fingerprint() => RequestFingerprint.Of(PaymentIntentId, PaymentMethodOptionId, ReturnUrl);
    }
}
