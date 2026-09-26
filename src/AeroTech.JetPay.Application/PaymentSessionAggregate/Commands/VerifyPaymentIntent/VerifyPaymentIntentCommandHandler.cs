using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.VerifyPaymentIntent
{
    public sealed class VerifyPaymentIntentCommandHandler : IRequestHandler<VerifyPaymentIntentCommand, PaymentSessionResponse>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentIntentRepository _intents;
        private readonly IPaymentSessionFunding _funding;
        private readonly ITenderProviderResolver _providers;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public VerifyPaymentIntentCommandHandler(
            IPaymentSessionRepository sessions,
            IPaymentIntentRepository intents,
            IPaymentSessionFunding funding,
            ITenderProviderResolver providers,
            IPaymentSessionLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _sessions = sessions;
            _intents = intents;
            _funding = funding;
            _providers = providers;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentSessionResponse> Handle(VerifyPaymentIntentCommand command, CancellationToken cancellationToken)
        {
            var sessionId = (await _intents.GetAsync(command.PaymentIntentId, cancellationToken)
                             ?? throw ExceptionFactory.PaymentIntentNotFound(command.PaymentIntentId)).PaymentSessionId;

            await using var sessionLock = await _locks.AcquireSessionAsync(sessionId, cancellationToken);

            var session = await _sessions.GetAsync(sessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(sessionId);
            var intents = await _funding.LoadIntentsAsync(session, cancellationToken);
            var intent = intents.Single(candidate => candidate.Id == command.PaymentIntentId);

            if (intent.Status == PaymentIntentStatus.RequiresCustomerAction)
            {
                intent.RecordVerifiedOutcome(TenderOutcome.Processing(), unapplied: false, _clock.GetDateTime());
                session.Refresh(intents, _idGenerator, _clock.GetDateTime());
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var outcome = await _providers.Resolve(intent.TenderType).VerifyAsync(
                new TenderVerifyRequest(
                    intent.Id,
                    session.Id,
                    session.OrderId,
                    intent.RequestedAmount,
                    intent.CurrencyId,
                    intent.ProviderReference,
                    intent.Id),
                cancellationToken);

            var superseded = await _sessions.HasNewerCommercialVersionAsync(session.OrderId, session.CommercialVersion, cancellationToken);
            var unapplied = outcome.Kind == TenderOutcomeKind.Captured && (session.IsTerminal || superseded);
            var now = _clock.GetDateTime();

            var arrivedLate = intent.RecordVerifiedOutcome(outcome, unapplied, now);

            if (outcome.Kind == TenderOutcomeKind.Captured && (arrivedLate || unapplied))
                session.RecordPaidUnapplied(intent, UnappliedReason(session, superseded), _idGenerator, now);

            session.Refresh(intents, _idGenerator, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return session.ToResponse(intents);
        }

        private static string UnappliedReason(PaymentSession session, bool superseded) => session.Status switch
        {
            PaymentSessionStatus.Cancelled => PaidUnappliedReason.PaymentSessionCancelled,
            PaymentSessionStatus.Expired => PaidUnappliedReason.PaymentSessionExpired,
            _ when superseded => PaidUnappliedReason.PayableInstructionSuperseded,
            _ => PaidUnappliedReason.PaymentIntentClosed
        };
    }
}
