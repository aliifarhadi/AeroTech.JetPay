using AeroTech.JetPay.Application.PaymentMethodOptions.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.Messages.JetPay.Enums;
using FluentValidation;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentMethodOptions.Queries.ResolvePaymentMethodOptions
{
    public sealed record ResolvePaymentMethodOptionsQuery(
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContextView InitiatorContext,
        decimal Amount,
        int CurrencyId,
        PaymentAssuranceRequirement AssuranceRequirement,
        PaymentInteractionMode InteractionMode) : IRequest<IReadOnlyList<PaymentMethodOptionView>>;

    public sealed record PaymentMethodOptionView(
        string Id,
        TenderType TenderType,
        string DisplayCode,
        int CurrencyId,
        decimal? AvailableAmount,
        decimal? MinimumAmount,
        decimal? MaximumAmount,
        bool SupportsPartialAmount,
        bool IsDefault,
        bool CanAutoSelect,
        CustomerActionType CustomerActionType,
        PaymentAssuranceCapability AssuranceCapability,
        PaymentCaptureMode CaptureMode,
        DateTimeOffset? ExpiresAt);

    public sealed class ResolvePaymentMethodOptionsQueryValidator : AbstractValidator<ResolvePaymentMethodOptionsQuery>
    {
        public ResolvePaymentMethodOptionsQueryValidator()
        {
            RuleFor(query => query.OrderId).GreaterThan(0);
            RuleFor(query => query.OrderReference).NotEmpty().MaximumLength(64);
            RuleFor(query => query.CommercialVersion).GreaterThan(0);
            RuleFor(query => query.Purpose).IsInEnum();
            RuleFor(query => query.PayerType).IsInEnum();
            RuleFor(query => query.PayerId).GreaterThan(0);
            RuleFor(query => query.InitiatorContext).NotNull().SetValidator(new PaymentInitiatorContextValidator());
            RuleFor(query => query.Amount).GreaterThan(0);
            RuleFor(query => query.CurrencyId).GreaterThan(0);
            RuleFor(query => query.AssuranceRequirement).IsInEnum();
            RuleFor(query => query.InteractionMode).IsInEnum();
        }
    }

    public sealed class ResolvePaymentMethodOptionsQueryHandler
        : IRequestHandler<ResolvePaymentMethodOptionsQuery, IReadOnlyList<PaymentMethodOptionView>>
    {
        private readonly IPaymentMethodOptionResolver _resolver;

        public ResolvePaymentMethodOptionsQueryHandler(IPaymentMethodOptionResolver resolver) => _resolver = resolver;

        public async Task<IReadOnlyList<PaymentMethodOptionView>> Handle(ResolvePaymentMethodOptionsQuery query, CancellationToken cancellationToken)
        {
            var options = await _resolver.ResolveAsync(
                new PaymentEligibilityContext(
                    query.PayerType,
                    query.PayerId,
                    query.InitiatorContext.ToValueObject(),
                    query.Amount,
                    query.CurrencyId,
                    query.Purpose,
                    query.AssuranceRequirement,
                    query.InteractionMode),
                cancellationToken);

            return options
                .Select(option => new PaymentMethodOptionView(
                    option.Id,
                    option.TenderType,
                    option.DisplayCode,
                    option.CurrencyId,
                    option.AvailableAmount,
                    option.MinimumAmount,
                    option.MaximumAmount,
                    option.SupportsPartialAmount,
                    option.IsDefault,
                    option.CanAutoSelect,
                    option.CustomerActionType,
                    option.AssuranceCapability,
                    option.CaptureMode,
                    option.ExpiresAt))
                .ToList();
        }
    }
}
