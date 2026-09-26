using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Arguments;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CreatePaymentSession
{
    public sealed record CreatePaymentSessionCommand(
        string IdempotencyKey,
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
        IReadOnlyList<PaymentSelection> Selections) : IRequest<PaymentSessionView>
    {
        public CreatePaymentSessionArgs ToArgs()
            => new(
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                IssuerLegalEntityId,
                PayerType,
                PayerId,
                Initiator.ToValueObject(),
                InteractionMode,
                SelectionMode,
                RequiredAmount,
                CurrencyId,
                AssuranceRequirement,
                ExpiresAt);

        public string Fingerprint()
            => RequestFingerprint.Of(
            [
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                IssuerLegalEntityId,
                PayerType,
                PayerId,
                Initiator.ActorType,
                Initiator.ActorId,
                Initiator.SalesChannel,
                Initiator.OfficeId,
                InteractionMode,
                SelectionMode,
                RequiredAmount,
                CurrencyId,
                AssuranceRequirement,
                ExpiresAt,
                Selections.Count,
                .. Selections.SelectMany(selection => new object?[] { selection.PaymentMethodOptionId, selection.Amount })
            ]);
    }
}
