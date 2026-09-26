using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.VerifyPaymentIntent
{
    public sealed record VerifyPaymentIntentCommand(string PaymentIntentId) : IRequest<PaymentSessionResponse>;
}
