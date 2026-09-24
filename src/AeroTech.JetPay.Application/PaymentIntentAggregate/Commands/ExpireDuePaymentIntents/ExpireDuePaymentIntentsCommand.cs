using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ExpireDuePaymentIntents
{
    public sealed record ExpireDuePaymentIntentsCommand(int BatchSize) : IRequest<int>;
}
