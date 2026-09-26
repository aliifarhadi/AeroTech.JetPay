using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession
{
    public sealed class CreatePaymentSessionCommandValidator : AbstractValidator<CreatePaymentSessionCommand>
    {
        public CreatePaymentSessionCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(100);
            RuleFor(command => command.PayableInstructionId).NotEmpty().MaximumLength(100);
            RuleFor(command => command.OrderId).GreaterThan(0);
            RuleFor(command => command.OrderReference).NotEmpty().MaximumLength(64);
            RuleFor(command => command.CommercialVersion).GreaterThan(0);
            RuleFor(command => command.Purpose).IsInEnum();
            RuleFor(command => command.IssuerLegalEntityId).GreaterThan(0);
            RuleFor(command => command.PayerType).IsInEnum();
            RuleFor(command => command.PayerId).GreaterThan(0);
            RuleFor(command => command.Initiator).NotNull().SetValidator(new PaymentInitiatorContextValidator());
            RuleFor(command => command.InteractionMode).IsInEnum();
            RuleFor(command => command.SelectionMode).IsInEnum();
            RuleFor(command => command.RequiredAmount).GreaterThanOrEqualTo(0);
            RuleFor(command => command.CurrencyId).GreaterThan(0);
            RuleFor(command => command.AssuranceRequirement).IsInEnum();
            RuleFor(command => command.Selections).NotNull();
            RuleForEach(command => command.Selections).SetValidator(new PaymentSelectionValidator());
        }
    }

    public sealed class PaymentInitiatorContextValidator : AbstractValidator<PaymentInitiatorContextView>
    {
        public PaymentInitiatorContextValidator()
        {
            RuleFor(initiator => initiator.ActorType).NotEmpty().MaximumLength(64);
            RuleFor(initiator => initiator.ActorId).GreaterThan(0);
            RuleFor(initiator => initiator.SalesChannel).IsInEnum();
            RuleFor(initiator => initiator.OfficeId).GreaterThan(0).When(initiator => initiator.OfficeId is not null);
        }
    }

    public sealed class PaymentSelectionValidator : AbstractValidator<PaymentSelection>
    {
        public PaymentSelectionValidator()
        {
            RuleFor(selection => selection.PaymentMethodOptionId).NotEmpty().MaximumLength(64);
            RuleFor(selection => selection.Amount).GreaterThan(0);
        }
    }
}
