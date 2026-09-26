using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.AddPaymentSelections
{
    public sealed class AddPaymentSelectionsCommandValidator : AbstractValidator<AddPaymentSelectionsCommand>
    {
        public AddPaymentSelectionsCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PaymentSessionId).NotEmpty().MaximumLength(64);
            RuleFor(command => command.Selections).NotNull();
            RuleForEach(command => command.Selections).SetValidator(new PaymentSelectionValidator());
            RuleFor(command => command.ReturnUrl)
                .MaximumLength(2048)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .When(command => command.ReturnUrl is not null)
                .WithMessage("ReturnUrl must be an absolute URL.");
        }
    }
}
