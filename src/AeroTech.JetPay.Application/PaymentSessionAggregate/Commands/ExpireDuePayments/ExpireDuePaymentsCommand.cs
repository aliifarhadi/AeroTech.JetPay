using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments
{
    public sealed record ExpireDuePaymentsCommand(int BatchSize) : IRequest<int>;
}
