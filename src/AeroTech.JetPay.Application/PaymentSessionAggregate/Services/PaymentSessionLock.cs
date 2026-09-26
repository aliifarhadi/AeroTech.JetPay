using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Services
{
    public interface IPaymentSessionLock
    {
        Task<IAsyncDisposable> AcquireSessionAsync(string paymentSessionId, CancellationToken cancellationToken = default);

        Task<IAsyncDisposable?> TryAcquireSessionAsync(string paymentSessionId, CancellationToken cancellationToken = default);

        Task<IAsyncDisposable> AcquireCreationAsync(string idempotencyKey, CancellationToken cancellationToken = default);

        Task<IAsyncDisposable> AcquireInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default);
    }

    public sealed class PaymentSessionLock : IPaymentSessionLock
    {
        private readonly IDistributedLock _distributedLock;
        private readonly TimeSpan _expiry;

        public PaymentSessionLock(IDistributedLock distributedLock, IOptions<PaymentSessionOptions> options)
        {
            _distributedLock = distributedLock;
            _expiry = TimeSpan.FromSeconds(options.Value.LockExpirySeconds);
        }

        public Task<IAsyncDisposable> AcquireSessionAsync(string paymentSessionId, CancellationToken cancellationToken = default)
            => AcquireAsync($"jetpay:payment-session:{paymentSessionId}", paymentSessionId, cancellationToken);

        public Task<IAsyncDisposable?> TryAcquireSessionAsync(string paymentSessionId, CancellationToken cancellationToken = default)
            => _distributedLock.AcquireAsync($"jetpay:payment-session:{paymentSessionId}", _expiry, cancellationToken);

        public Task<IAsyncDisposable> AcquireCreationAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => AcquireAsync($"jetpay:payment-session-creation:{idempotencyKey}", idempotencyKey, cancellationToken);

        public Task<IAsyncDisposable> AcquireInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
            => AcquireAsync($"jetpay:payable-instruction:{payableInstructionId}", payableInstructionId, cancellationToken);

        private async Task<IAsyncDisposable> AcquireAsync(string resource, string subject, CancellationToken cancellationToken)
            => await _distributedLock.AcquireAsync(resource, _expiry, cancellationToken)
               ?? throw ExceptionFactory.PaymentOperationInProgress(subject);
    }
}
