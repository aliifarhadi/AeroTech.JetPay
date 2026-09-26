using System.Globalization;
using AeroTech.JetPay.Application.PaymentMethodOptions.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Services
{
    public sealed record PlannedContribution(PaymentMethodOption Option, decimal Amount);

    public sealed record DefaultFundingPlan(PlannedContribution? Contribution, string? FailureCode);

    public interface IPaymentSelectionPlanner
    {
        Task<IReadOnlyList<PlannedContribution>> PlanExplicitAsync(
            PaymentSession session,
            IReadOnlyList<PaymentSelection> selections,
            CancellationToken cancellationToken = default);

        Task<DefaultFundingPlan> PlanDefaultAsync(PaymentSession session, CancellationToken cancellationToken = default);
    }

    public sealed class PaymentSelectionPlanner : IPaymentSelectionPlanner
    {
        private readonly IPaymentMethodOptionResolver _options;

        public PaymentSelectionPlanner(IPaymentMethodOptionResolver options) => _options = options;

        public async Task<IReadOnlyList<PlannedContribution>> PlanExplicitAsync(
            PaymentSession session,
            IReadOnlyList<PaymentSelection> selections,
            CancellationToken cancellationToken = default)
        {
            if (selections.Count == 0)
                throw ExceptionFactory.SelectionsRequired();

            if (selections.Count > 1)
                throw ExceptionFactory.MultipleSelectionsNotSupported();

            var fundable = session.FundableAmount;
            var total = selections.Sum(selection => selection.Amount);

            if (total != fundable)
                throw ExceptionFactory.SelectionSumMismatch(total.ToString(CultureInfo.InvariantCulture), fundable.ToString(CultureInfo.InvariantCulture));

            var options = await _options.ResolveAsync(PaymentEligibilityContext.Of(session, fundable), cancellationToken);
            var plan = new List<PlannedContribution>();

            foreach (var selection in selections)
            {
                var option = options.FirstOrDefault(candidate => candidate.Id == selection.PaymentMethodOptionId)
                             ?? throw ExceptionFactory.PaymentMethodOptionNotAvailable(selection.PaymentMethodOptionId, session.Id);

                if (selection.Amount > option.AvailableAmount)
                    throw ExceptionFactory.SelectionExceedsAvailableAmount(option.Id, selection.Amount, option.AvailableAmount);

                if (selection.Amount < option.MinimumAmount || selection.Amount > option.MaximumAmount)
                    throw ExceptionFactory.SelectionOutsideAmountLimits(option.Id, selection.Amount, option.MinimumAmount, option.MaximumAmount);

                if (selection.Amount < fundable && !option.SupportsPartialAmount)
                    throw ExceptionFactory.PartialAmountNotSupported(option.Id);

                plan.Add(new PlannedContribution(option, selection.Amount));
            }

            return plan;
        }

        public async Task<DefaultFundingPlan> PlanDefaultAsync(PaymentSession session, CancellationToken cancellationToken = default)
        {
            var fundable = session.FundableAmount;
            var options = await _options.ResolveAsync(PaymentEligibilityContext.Of(session, fundable), cancellationToken);
            var source = options.FirstOrDefault(option => option.IsDefault && option.CanAutoSelect && option.CustomerActionType == CustomerActionType.None);

            return source switch
            {
                null => new DefaultFundingPlan(null, FundingFailureCode.DefaultFundingSourceUnavailable),
                { AvailableAmount: { } available } when available < fundable => new DefaultFundingPlan(null, FundingFailureCode.InsufficientFunds),
                _ => new DefaultFundingPlan(new PlannedContribution(source, fundable), null)
            };
        }
    }
}
