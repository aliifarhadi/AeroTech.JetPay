using AeroTech.JetPay.Application.PaymentIntentAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Arguments;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.CreatePaymentIntent
{
    public sealed record CreatePaymentIntentCommand(
        string IdempotencyKey,
        string PayableInstructionId,
        long OrderId,
        string OrderReference,
        int CommercialVersion,
        PaymentPurpose Purpose,
        PayerType PayerType,
        long PayerId,
        decimal Amount,
        int CurrencyId,
        RequiredGuarantee RequiredGuarantee,
        PaymentCaptureMode CaptureMode,
        DateTimeOffset? IntentExpiresAt) : IRequest<PaymentIntentView>
    {
        public CreatePaymentIntentArgs ToArgs()
            => new(
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                PayerType,
                PayerId,
                Amount,
                CurrencyId,
                RequiredGuarantee,
                CaptureMode,
                IntentExpiresAt);

        public string Fingerprint()
            => RequestFingerprint.Of(
                PayableInstructionId,
                OrderId,
                OrderReference,
                CommercialVersion,
                Purpose,
                PayerType,
                PayerId,
                Amount,
                CurrencyId,
                RequiredGuarantee,
                CaptureMode,
                IntentExpiresAt);
    }
}
