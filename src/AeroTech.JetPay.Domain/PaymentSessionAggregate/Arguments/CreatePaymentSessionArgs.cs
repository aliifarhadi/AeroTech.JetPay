using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.Arguments
{
    public sealed record CreatePaymentSessionArgs(
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContext InitiatorContext,
        decimal Amount,
        int CurrencyId,
        PaymentAssuranceRequirement AssuranceRequirement,
        PaymentInteractionMode InteractionMode,
        DateTimeOffset? ExpiresAt);
}
