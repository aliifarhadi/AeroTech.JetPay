using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using FluentValidation;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ProcessProviderCallback
{
    public sealed record ProcessProviderCallbackCommand(string PaymentIntentId, DateTimeOffset? PaidAt) : IRequest<PaymentSessionView>;

    public sealed class ProcessProviderCallbackCommandValidator : AbstractValidator<ProcessProviderCallbackCommand>
    {
        public ProcessProviderCallbackCommandValidator()
            => RuleFor(command => command.PaymentIntentId).NotEmpty().MaximumLength(64);
    }

    public sealed class ProcessProviderCallbackCommandHandler : IRequestHandler<ProcessProviderCallbackCommand, PaymentSessionView>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentIntentExecution _execution;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;

        public ProcessProviderCallbackCommandHandler(
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

        public async Task<PaymentSessionView> Handle(ProcessProviderCallbackCommand command, CancellationToken cancellationToken)
        {
            var sessionId = await _sessions.FindSessionIdByPaymentIntentAsync(command.PaymentIntentId, cancellationToken)
                            ?? throw ExceptionFactory.PaymentIntentNotFound(command.PaymentIntentId);

            await using var sessionLock = await _locks.AcquireSessionAsync(sessionId, cancellationToken);

            var session = await _sessions.GetAsync(sessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(sessionId);

            await _execution.ApplyCallbackAsync(session, session.Intent(command.PaymentIntentId), command.PaidAt, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return session.ToView();
        }
    }
}
