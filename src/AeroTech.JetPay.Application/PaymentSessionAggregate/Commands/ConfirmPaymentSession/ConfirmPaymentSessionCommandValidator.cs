using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession
{
    public sealed class ConfirmPaymentSessionCommandValidator : AbstractValidator<ConfirmPaymentSessionCommand>
    {
        public ConfirmPaymentSessionCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PaymentSessionId).NotEmpty().MaximumLength(64);
            RuleFor(command => command.SelectionMode).IsInEnum();
            RuleFor(command => command.Selections).NotNull();
            RuleForEach(command => command.Selections).ChildRules(selection =>
            {
                selection.RuleFor(item => item.PaymentMethodOptionId).NotEmpty().MaximumLength(64);
                selection.RuleFor(item => item.Amount).GreaterThan(0);
            });
            RuleFor(command => command.ReturnUrl)
                .MaximumLength(2048)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .When(command => command.ReturnUrl is not null)
                .WithMessage("ReturnUrl must be an absolute URL.");
        }
    }
}
