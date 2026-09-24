using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CancelPaymentIntent
{
    public sealed class CancelPaymentIntentCommandValidator : AbstractValidator<CancelPaymentIntentCommand>
    {
        public CancelPaymentIntentCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PaymentIntentId).NotEmpty().MaximumLength(64);
            RuleFor(command => command.Reason).IsInEnum();
        }
    }
}
