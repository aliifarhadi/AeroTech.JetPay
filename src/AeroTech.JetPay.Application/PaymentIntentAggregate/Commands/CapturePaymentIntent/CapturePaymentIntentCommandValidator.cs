using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CapturePaymentIntent
{
    public sealed class CapturePaymentIntentCommandValidator : AbstractValidator<CapturePaymentIntentCommand>
    {
        public CapturePaymentIntentCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PaymentIntentId).NotEmpty().MaximumLength(64);
            RuleFor(command => command.Amount).GreaterThan(0).When(command => command.Amount is not null);
        }
    }
}
