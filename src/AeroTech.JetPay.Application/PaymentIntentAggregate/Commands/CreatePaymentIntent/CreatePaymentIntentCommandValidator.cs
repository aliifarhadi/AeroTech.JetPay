using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CreatePaymentIntent
{
    public sealed class CreatePaymentIntentCommandValidator : AbstractValidator<CreatePaymentIntentCommand>
    {
        public CreatePaymentIntentCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PayableInstructionId).NotEmpty().MaximumLength(100);
            RuleFor(command => command.OrderId).GreaterThan(0);
            RuleFor(command => command.OrderReference).NotEmpty().MaximumLength(64);
            RuleFor(command => command.CommercialVersion).GreaterThan(0);
            RuleFor(command => command.Purpose).IsInEnum();
            RuleFor(command => command.PayerType).IsInEnum();
            RuleFor(command => command.PayerId).GreaterThan(0);
            RuleFor(command => command.Amount).GreaterThan(0);
            RuleFor(command => command.CurrencyId).GreaterThan(0);
            RuleFor(command => command.RequiredGuarantee).IsInEnum();
            RuleFor(command => command.CaptureMode).IsInEnum();
        }
    }
}
