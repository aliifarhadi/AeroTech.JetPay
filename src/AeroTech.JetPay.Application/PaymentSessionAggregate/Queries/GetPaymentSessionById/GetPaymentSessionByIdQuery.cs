using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Queries.GetPaymentSessionById
{
    public sealed record GetPaymentSessionByIdQuery(string PaymentSessionId) : IRequest<PaymentSessionView>;

    public sealed class GetPaymentSessionByIdQueryHandler : IRequestHandler<GetPaymentSessionByIdQuery, PaymentSessionView>
    {
        private readonly IPaymentSessionRepository _sessions;

        public GetPaymentSessionByIdQueryHandler(IPaymentSessionRepository sessions) => _sessions = sessions;

        public async Task<PaymentSessionView> Handle(GetPaymentSessionByIdQuery query, CancellationToken cancellationToken)
        {
            var session = await _sessions.GetAsync(query.PaymentSessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(query.PaymentSessionId);

            return session.ToView();
        }
    }
}
