using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Queries.GetPaymentSessionById
{
    public sealed record GetPaymentSessionByIdQuery(string PaymentSessionId) : IRequest<PaymentSessionResponse>;

    public sealed class GetPaymentSessionByIdQueryHandler : IRequestHandler<GetPaymentSessionByIdQuery, PaymentSessionResponse>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentSessionFunding _funding;

        public GetPaymentSessionByIdQueryHandler(IPaymentSessionRepository sessions, IPaymentSessionFunding funding)
        {
            _sessions = sessions;
            _funding = funding;
        }

        public async Task<PaymentSessionResponse> Handle(GetPaymentSessionByIdQuery query, CancellationToken cancellationToken)
        {
            var session = await _sessions.GetAsync(query.PaymentSessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(query.PaymentSessionId);

            return session.ToResponse(await _funding.LoadIntentsAsync(session, cancellationToken));
        }
    }
}
