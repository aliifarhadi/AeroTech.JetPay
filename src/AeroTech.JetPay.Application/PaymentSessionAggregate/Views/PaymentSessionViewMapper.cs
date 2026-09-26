using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Views
{
    public static class PaymentSessionViewMapper
    {
        public static PaymentSessionResponse ToResponse(this PaymentSession session, IEnumerable<PaymentIntent> intents)
            => new(session.ToView(), intents.OrderBy(intent => intent.Sequence).Select(intent => intent.ToView()).ToList());

        public static PaymentSessionView ToView(this PaymentSession session)
            => new(
                session.Id,
                session.PayableInstructionId,
                session.OrderId,
                session.OrderReference,
                session.CommercialVersion,
                session.Purpose,
                session.PayerType,
                session.PayerId,
                session.InitiatorContext.ToView(),
                session.RequiredAmount,
                session.CurrencyId,
                session.AssuranceRequirement,
                session.InteractionMode,
                session.Status,
                session.GuaranteedAmount,
                session.CapturedAmount,
                session.RefundedAmount,
                session.OutstandingGuaranteeAmount,
                session.EarliestGuaranteeExpiry,
                session.ExpiresAt,
                session.FailureCode,
                session.Version,
                session.CreatedAt,
                session.UpdatedAt,
                session.PaymentIntentIds.ToList());

        public static PaymentInitiatorContextView ToView(this PaymentInitiatorContext initiator)
            => new(initiator.SalesChannel, initiator.ActorType, initiator.ActorId, initiator.OfficeId);

        public static PaymentInitiatorContext ToValueObject(this PaymentInitiatorContextView initiator)
            => new(initiator.SalesChannel, initiator.ActorType, initiator.ActorId, initiator.OfficeId);

        public static PaymentIntentView ToView(this PaymentIntent intent)
            => new(
                intent.Id,
                intent.PaymentSessionId,
                intent.Sequence,
                intent.PaymentMethodOptionId,
                intent.TenderType,
                intent.RequestedAmount,
                intent.CurrencyId,
                intent.CaptureMode,
                intent.Status,
                intent.AuthorizedAmount,
                intent.GuaranteedAmount,
                intent.CapturedAmount,
                intent.RefundedAmount,
                intent.GuaranteeExpiresAt,
                intent.NextAction?.ToView(),
                intent.FailureCode,
                intent.FailureReason,
                intent.Version,
                intent.CreatedAt,
                intent.UpdatedAt);

        private static CustomerActionView ToView(this CustomerAction action)
            => new(action.Type, action.Url, action.HttpMethod, action.FormFields, action.ExpiresAt);
    }
}
