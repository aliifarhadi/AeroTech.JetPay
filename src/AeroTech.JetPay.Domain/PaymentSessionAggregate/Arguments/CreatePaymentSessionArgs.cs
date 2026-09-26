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
        long IssuerLegalEntityId,
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContext Initiator,
        PaymentInteractionMode InteractionMode,
        PaymentSelectionMode SelectionMode,
        decimal RequiredAmount,
        int CurrencyId,
        PaymentAssuranceRequirement AssuranceRequirement,
        DateTimeOffset? ExpiresAt);
}
