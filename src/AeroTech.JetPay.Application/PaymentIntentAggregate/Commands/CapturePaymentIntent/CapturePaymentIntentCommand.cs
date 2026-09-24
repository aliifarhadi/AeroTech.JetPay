using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CapturePaymentIntent
{
    public sealed record CapturePaymentIntentCommand(
        string IdempotencyKey,
        string PaymentIntentId,
        decimal? Amount,
        bool FinalCapture) : IRequest<PaymentIntentView>
    {
        public string Fingerprint() => RequestFingerprint.Of(PaymentIntentId, Amount, FinalCapture);
    }
}
