using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.RestApi.V1.PaymentSessionAggregate.Requests
{
    public sealed record CreatePaymentSessionRequest(
        string PayableInstructionId,
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
        PaymentInteractionMode InteractionMode,
        DateTimeOffset? ExpiresAt)
    {
        public CreatePaymentSessionCommand ToCommand(string idempotencyKey)
            => new(
                idempotencyKey,
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                PayerType,
                PayerId,
                InitiatorContext,
                Amount,
                CurrencyId,
                AssuranceRequirement,
                InteractionMode,
                ExpiresAt);
    }

    public sealed record PaymentSelectionRequest(string PaymentMethodOptionId, decimal Amount);

    public sealed record ConfirmPaymentSessionRequest(
        PaymentSelectionMode SelectionMode,
        IReadOnlyList<PaymentSelectionRequest>? Selections,
        string? ReturnUrl)
    {
        public ConfirmPaymentSessionCommand ToCommand(string idempotencyKey, string paymentSessionId)
            => new(
                idempotencyKey,
                paymentSessionId,
                SelectionMode,
                (Selections ?? []).Select(selection => new PaymentSelection(selection.PaymentMethodOptionId, selection.Amount)).ToList(),
                ReturnUrl);
    }
}
