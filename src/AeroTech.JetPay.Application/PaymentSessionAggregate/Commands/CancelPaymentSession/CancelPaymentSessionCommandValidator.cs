using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CancelPaymentSession
{
    public sealed class CancelPaymentSessionCommandValidator : AbstractValidator<CancelPaymentSessionCommand>
    {
        public CancelPaymentSessionCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PaymentSessionId).NotEmpty().MaximumLength(64);
        }
    }
}
