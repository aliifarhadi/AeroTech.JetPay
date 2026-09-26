using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate
{
    public sealed record SessionCoverage(decimal GuaranteedAmount, decimal AppliedCapturedAmount, DateTimeOffset? EarliestGuaranteeExpiry)
    {
        public static SessionCoverage Of(
            IReadOnlyCollection<PaymentIntent> intents,
            decimal requiredAmount,
            PaymentAssuranceRequirement requirement,
            DateTimeOffset now)
        {
            var contributions = intents
                .Select(intent => (
                    Amount: requirement == PaymentAssuranceRequirement.FundsReceived ? intent.ValidFundsReceivedAt(now) : intent.ValidGuaranteeAt(now),
                    ExpiresAt: requirement == PaymentAssuranceRequirement.FundsReceived ? null : intent.GuaranteeExpiresAt))
                .Where(contribution => contribution.Amount > 0)
                .ToList();

            var total = contributions.Sum(contribution => contribution.Amount);
            var appliedCaptured = intents.Sum(intent => intent.ValidFundsReceivedAt(now));

            return new SessionCoverage(
                Math.Min(total, requiredAmount),
                appliedCaptured,
                EarliestNeededExpiry(contributions, total, requiredAmount));
        }

        private static DateTimeOffset? EarliestNeededExpiry(
            IReadOnlyCollection<(decimal Amount, DateTimeOffset? ExpiresAt)> contributions,
            decimal total,
            decimal requiredAmount)
        {
            var expiring = contributions
                .Where(contribution => contribution.ExpiresAt is not null)
                .GroupBy(contribution => contribution.ExpiresAt!.Value)
                .OrderBy(group => group.Key)
                .ToList();

            if (expiring.Count == 0)
                return null;

            if (total < requiredAmount)
                return expiring[0].Key;

            var remaining = total;

            foreach (var lapse in expiring)
            {
                remaining -= lapse.Sum(contribution => contribution.Amount);

                if (remaining < requiredAmount)
                    return lapse.Key;
            }

            return null;
        }
    }
}
