using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Queries.GetPaymentIntentById
{
    /// <summary>
    /// Authoritative read-back: served from the write model, never from a projection that could lag a commit.
    /// </summary>
    public sealed class GetPaymentIntentByIdQueryHandler : IRequestHandler<GetPaymentIntentByIdQuery, PaymentIntentView>
    {
        private readonly IPaymentIntentRepository _intents;

        public GetPaymentIntentByIdQueryHandler(IPaymentIntentRepository intents) => _intents = intents;

        public async Task<PaymentIntentView> Handle(GetPaymentIntentByIdQuery query, CancellationToken cancellationToken)
            => (await _intents.GetAsync(query.PaymentIntentId, cancellationToken)
                ?? throw ExceptionFactory.PaymentIntentNotFound(query.PaymentIntentId)).ToView();
    }
}
