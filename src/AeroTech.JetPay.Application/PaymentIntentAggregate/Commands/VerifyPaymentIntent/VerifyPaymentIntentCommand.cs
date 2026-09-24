using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.VerifyPaymentIntent
{
    /// <summary>
    /// Raised by a provider callback, customer return, or recovery sweep. The callback itself is only a trigger; the
    /// outcome is taken from the provider's server-side verify/inquiry.
    /// </summary>
    public sealed record VerifyPaymentIntentCommand(string PaymentIntentId) : IRequest<PaymentIntentView>;
}
