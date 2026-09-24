namespace AeroTech.Messages.JetPay.IntegrationEvents.V1
{
    public sealed record PaymentPaidUnappliedV1(
        string PaymentIntentId,
        long OrderId,
        string SupersededPayableInstructionId,
        decimal CapturedAmount,
        int CurrencyId,
        string ReasonCode,
        DateTimeOffset OccurredAt) : BaseIntegrationEvent;
}
