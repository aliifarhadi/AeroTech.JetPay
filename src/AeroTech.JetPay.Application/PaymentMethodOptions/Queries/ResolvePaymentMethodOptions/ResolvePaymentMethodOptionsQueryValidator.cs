using FluentValidation;

namespace AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions
{
    public sealed class ResolvePaymentMethodOptionsQueryValidator : AbstractValidator<ResolvePaymentMethodOptionsQuery>
    {
        public ResolvePaymentMethodOptionsQueryValidator()
        {
            RuleFor(query => query.OrderId).GreaterThan(0);
            RuleFor(query => query.OrderReference).NotEmpty().MaximumLength(64);
            RuleFor(query => query.CommercialVersion).GreaterThan(0);
            RuleFor(query => query.PayerType).IsInEnum();
            RuleFor(query => query.PayerId).GreaterThan(0);
            RuleFor(query => query.Amount).GreaterThan(0);
            RuleFor(query => query.CurrencyId).GreaterThan(0);
            RuleFor(query => query.SalesChannel).NotEmpty().MaximumLength(64);
        }
    }
}
