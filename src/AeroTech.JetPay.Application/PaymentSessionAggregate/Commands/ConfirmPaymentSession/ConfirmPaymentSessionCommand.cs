using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession
{
    public sealed record PaymentSelection(string PaymentMethodOptionId, decimal Amount);

    public sealed record ConfirmPaymentSessionCommand(
        string IdempotencyKey,
        string PaymentSessionId,
        PaymentSelectionMode SelectionMode,
        IReadOnlyList<PaymentSelection> Selections,
        string? ReturnUrl) : IRequest<PaymentSessionResponse>
    {
        public string Fingerprint()
            => RequestFingerprint.Of(
                [PaymentSessionId, SelectionMode, ReturnUrl, Selections.Count,
                 .. Selections.SelectMany(selection => new object?[] { selection.PaymentMethodOptionId, selection.Amount })]);
    }
}
