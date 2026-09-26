using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.AddPaymentSelections;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.RestApi.V1.PaymentSessionAggregate.Requests
{
    public sealed record PaymentSelectionRequest(string PaymentMethodOptionId, decimal Amount);

    public sealed record CreatePaymentSessionRequest(
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        long IssuerLegalEntityId,
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContextView Initiator,
        PaymentInteractionMode InteractionMode,
        PaymentSelectionMode SelectionMode,
        decimal RequiredAmount,
        int CurrencyId,
        PaymentAssuranceRequirement AssuranceRequirement,
        DateTimeOffset? ExpiresAt,
        IReadOnlyList<PaymentSelectionRequest>? Selections)
    {
        public CreatePaymentSessionCommand ToCommand(string idempotencyKey)
            => new(
                idempotencyKey,
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                IssuerLegalEntityId,
                PayerType,
                PayerId,
                Initiator,
                InteractionMode,
                SelectionMode,
                RequiredAmount,
                CurrencyId,
                AssuranceRequirement,
                ExpiresAt,
                (Selections ?? []).Select(selection => new PaymentSelection(selection.PaymentMethodOptionId, selection.Amount)).ToList());
    }

    public sealed record AddPaymentSelectionsRequest(
        IReadOnlyList<PaymentSelectionRequest>? Selections,
        string? ReturnUrl)
    {
        public AddPaymentSelectionsCommand ToCommand(string idempotencyKey, string paymentSessionId)
            => new(
                idempotencyKey,
                paymentSessionId,
                (Selections ?? []).Select(selection => new PaymentSelection(selection.PaymentMethodOptionId, selection.Amount)).ToList(),
                ReturnUrl);
    }
}
