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
        PayerType PayerType,
        long PayerId,
        PaymentInitiatorContextView InitiatorContext,
        decimal Amount,
        int CurrencyId,
        PaymentAssuranceRequirement AssuranceRequirement,
        PaymentInteractionMode InteractionMode,
        DateTimeOffset? ExpiresAt) : IRequest<PaymentSessionResponse>
    {
        public CreatePaymentSessionArgs ToArgs()
            => new(
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                PayerType,
                PayerId,
                InitiatorContext.ToValueObject(),
                Amount,
                CurrencyId,
                AssuranceRequirement,
                InteractionMode,
                ExpiresAt);

        public string Fingerprint()
            => RequestFingerprint.Of(
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                PayerType,
                PayerId,
                InitiatorContext.SalesChannel,
                InitiatorContext.ActorType,
                InitiatorContext.ActorId,
                InitiatorContext.OfficeId,
                Amount,
                CurrencyId,
                AssuranceRequirement,
                InteractionMode,
                ExpiresAt);
    }
}
