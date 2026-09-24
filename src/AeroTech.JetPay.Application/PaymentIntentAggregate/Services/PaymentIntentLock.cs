using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain._Shared.Resources;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Application.PaymentIntentAggregate.Services
{
    public interface IPaymentIntentLock
    {
        Task<IAsyncDisposable> AcquireIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);

        Task<IAsyncDisposable?> TryAcquireIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);

        Task<IAsyncDisposable> AcquireCreationAsync(string idempotencyKey, CancellationToken cancellationToken = default);

        Task<IAsyncDisposable> AcquireInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default);
    }

    public sealed class PaymentIntentLock : IPaymentIntentLock
    {
        private readonly IDistributedLock _distributedLock;
        private readonly TimeSpan _expiry;

        public PaymentIntentLock(IDistributedLock distributedLock, IOptions<PaymentIntentOptions> options)
        {
            _distributedLock = distributedLock;
            _expiry = TimeSpan.FromSeconds(options.Value.LockExpirySeconds);
        }

        public Task<IAsyncDisposable> AcquireIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
            => AcquireAsync($"jetpay:payment-intent:{paymentIntentId}", paymentIntentId, cancellationToken);

        public Task<IAsyncDisposable?> TryAcquireIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
            => _distributedLock.AcquireAsync($"jetpay:payment-intent:{paymentIntentId}", _expiry, cancellationToken);

        public Task<IAsyncDisposable> AcquireCreationAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => AcquireAsync($"jetpay:payment-intent-creation:{idempotencyKey}", idempotencyKey, cancellationToken);

        public Task<IAsyncDisposable> AcquireInstructionAsync(string payableInstructionId, CancellationToken cancellationToken = default)
            => AcquireAsync($"jetpay:payable-instruction:{payableInstructionId}", payableInstructionId, cancellationToken);

        private async Task<IAsyncDisposable> AcquireAsync(string resource, string subject, CancellationToken cancellationToken)
            => await _distributedLock.AcquireAsync(resource, _expiry, cancellationToken)
               ?? throw ExceptionFactory.PaymentIntentOperationInProgress(subject);
    }
}
