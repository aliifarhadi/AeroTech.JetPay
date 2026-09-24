using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Queries.GetPaymentIntentById
{
    public sealed record GetPaymentIntentByIdQuery(string PaymentIntentId) : IRequest<PaymentIntentView>;
}
