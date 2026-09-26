using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.AddPaymentSelections
{
    public sealed record AddPaymentSelectionsCommand(
        string IdempotencyKey,
        string PaymentSessionId,
        IReadOnlyList<PaymentSelection> Selections,
        string? ReturnUrl) : IRequest<PaymentSessionView>
    {
        public string Fingerprint()
            => RequestFingerprint.Of(
                [PaymentSessionId, ReturnUrl, Selections.Count,
                 .. Selections.SelectMany(selection => new object?[] { selection.PaymentMethodOptionId, selection.Amount })]);
    }
}
