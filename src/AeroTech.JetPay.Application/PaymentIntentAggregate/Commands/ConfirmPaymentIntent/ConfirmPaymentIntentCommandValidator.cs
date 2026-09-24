using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ConfirmPaymentIntent
{
    public sealed class ConfirmPaymentIntentCommandValidator : AbstractValidator<ConfirmPaymentIntentCommand>
    {
        public ConfirmPaymentIntentCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PaymentIntentId).NotEmpty().MaximumLength(64);
            RuleFor(command => command.PaymentMethodOptionId).NotEmpty().MaximumLength(64);
            RuleFor(command => command.ReturnUrl)
                .MaximumLength(2048)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .When(command => command.ReturnUrl is not null)
                .WithMessage("ReturnUrl must be an absolute URL.");
        }
    }
}
