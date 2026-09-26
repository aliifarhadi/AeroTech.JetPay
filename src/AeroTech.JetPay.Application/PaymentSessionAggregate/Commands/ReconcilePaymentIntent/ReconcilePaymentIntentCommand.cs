using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using FluentValidation;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ReconcilePaymentIntent
{
    public sealed record ReconcilePaymentIntentCommand(string PaymentIntentId) : IRequest<PaymentSessionView>;

    public sealed class ReconcilePaymentIntentCommandValidator : AbstractValidator<ReconcilePaymentIntentCommand>
    {
        public ReconcilePaymentIntentCommandValidator()
            => RuleFor(command => command.PaymentIntentId).NotEmpty().MaximumLength(64);
    }

    public sealed class ReconcilePaymentIntentCommandHandler : IRequestHandler<ReconcilePaymentIntentCommand, PaymentSessionView>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentIntentExecution _execution;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;

        public ReconcilePaymentIntentCommandHandler(
            IPaymentSessionRepository sessions,
            IPaymentIntentExecution execution,
            IPaymentSessionLock locks,
            IUnitOfWork unitOfWork)
        {
            _sessions = sessions;
            _execution = execution;
            _locks = locks;
            _unitOfWork = unitOfWork;
        }

        public async Task<PaymentSessionView> Handle(ReconcilePaymentIntentCommand command, CancellationToken cancellationToken)
        {
            var sessionId = await _sessions.FindSessionIdByPaymentIntentAsync(command.PaymentIntentId, cancellationToken)
                            ?? throw ExceptionFactory.PaymentIntentNotFound(command.PaymentIntentId);

            await using var sessionLock = await _locks.AcquireSessionAsync(sessionId, cancellationToken);

            var session = await _sessions.GetAsync(sessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(sessionId);

            await _execution.ReconcileAsync(session, session.Intent(command.PaymentIntentId), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return session.ToView();
        }
    }
}
