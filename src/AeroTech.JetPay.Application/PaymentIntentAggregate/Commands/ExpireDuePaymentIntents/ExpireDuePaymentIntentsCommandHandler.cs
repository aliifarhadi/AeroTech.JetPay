using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Services;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ExpireDuePaymentIntents
{
    public sealed class ExpireDuePaymentIntentsCommandHandler : IRequestHandler<ExpireDuePaymentIntentsCommand, int>
    {
        private readonly IPaymentIntentRepository _intents;
        private readonly IPaymentIntentLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ExpireDuePaymentIntentsCommandHandler(
            IPaymentIntentRepository intents,
            IPaymentIntentLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _intents = intents;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<int> Handle(ExpireDuePaymentIntentsCommand command, CancellationToken cancellationToken)
        {
            var due = await _intents.ListDueForExpiryAsync(_clock.GetDateTime(), command.BatchSize, cancellationToken);
            var expired = 0;

            foreach (var paymentIntentId in due)
            {
                // An intent that is busy is picked up by the next sweep.
                await using var intentLock = await _locks.TryAcquireIntentAsync(paymentIntentId, cancellationToken);

                if (intentLock is null)
                    continue;

                var intent = await _intents.GetAsync(paymentIntentId, cancellationToken);

                if (intent is null || !intent.ExpireIfDue(_idGenerator, _clock.GetDateTime()))
                    continue;

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                expired++;
            }

            return expired;
        }
    }
}
