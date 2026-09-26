using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CancelPaymentSession
{
    public sealed record CancelPaymentSessionCommand(string IdempotencyKey, string PaymentSessionId) : IRequest<PaymentSessionResponse>
    {
        public string Fingerprint() => RequestFingerprint.Of(PaymentSessionId);
    }
}
