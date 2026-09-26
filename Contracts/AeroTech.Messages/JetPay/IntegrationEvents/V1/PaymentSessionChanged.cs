using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.Messages.JetPay.IntegrationEvents.V1
{
    public sealed record PaymentSessionChanged(
        string PaymentSessionId,
        string PayableInstructionId,
        long OrderId,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PaymentSessionStatus Status,
        PaymentAssuranceRequirement AssuranceRequirement,
        decimal RequiredAmount,
        decimal GuaranteedAmount,
        decimal CapturedAmount,
        decimal OutstandingAmount,
        int CurrencyId,
        DateTimeOffset? ExpiresAt,
        long Version,
        string? FailureCode,
        DateTimeOffset OccurredAt) : BaseIntegrationEvent;
}
