using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CancelPaymentIntent
{
    public sealed record CancelPaymentIntentCommand(
        string IdempotencyKey,
        string PaymentIntentId,
        PaymentCancellationReason Reason) : IRequest<PaymentIntentView>
    {
        public string Fingerprint() => RequestFingerprint.Of(PaymentIntentId, Reason);
    }
}
