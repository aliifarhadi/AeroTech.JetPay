using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Views
{
    public static class PaymentSessionViewMapper
    {
        public static PaymentSessionView ToView(this PaymentSession session)
            => new(
                session.Id,
                session.PayableInstructionId,
                session.OrderId,
                session.OrderReference,
                session.CommercialVersion,
                session.Purpose,
                session.IssuerLegalEntityId,
                session.PayerType,
                session.PayerId,
                session.Initiator.ToView(),
                session.InteractionMode,
                session.SelectionMode,
                session.RequiredAmount,
                session.CurrencyId,
                session.AssuranceRequirement,
                session.Status,
                session.GuaranteedAmount,
                session.CapturedAmount,
                session.OutstandingAmount,
                session.ExpiresAt,
                session.FailureCode,
                session.Version,
                session.CreatedAt,
                session.UpdatedAt,
                session.Intents
                    .OrderBy(intent => intent.CreatedAt)
                    .ThenBy(intent => intent.Id, StringComparer.Ordinal)
                    .Select(intent => intent.ToView())
                    .ToList());

        public static PaymentInitiatorContextView ToView(this PaymentInitiatorContext initiator)
            => new(initiator.ActorType, initiator.ActorId, initiator.SalesChannel, initiator.OfficeId);

        public static PaymentInitiatorContext ToValueObject(this PaymentInitiatorContextView initiator)
            => new(initiator.ActorType, initiator.ActorId, initiator.SalesChannel, initiator.OfficeId);

        public static PaymentIntentView ToView(this PaymentIntent intent)
            => new(
                intent.Id,
                intent.PaymentSessionId,
                intent.PaymentMethodOptionId,
                intent.TenderType,
                intent.RequestedAmount,
                intent.CurrencyId,
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
