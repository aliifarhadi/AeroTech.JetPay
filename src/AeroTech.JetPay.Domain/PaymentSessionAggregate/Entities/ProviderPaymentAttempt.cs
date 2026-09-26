using System.Globalization;
using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.JetPay.Domain._Shared.Resources;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities
{
    public sealed class ProviderPaymentAttempt : Entity<long>
    {
        private ProviderPaymentAttempt()
        {
        }

        private ProviderPaymentAttempt(long id, string paymentIntentId, int attemptNumber, ProviderProfile profile, DateTimeOffset createdAt)
        {
            Id = id;
            PaymentIntentId = paymentIntentId;
            AttemptNumber = attemptNumber;
            ProviderProfileId = profile.Id;
            ProviderProfileVersion = profile.Version;
            IdempotencyKey = $"jetpay-attempt-{id.ToString(CultureInfo.InvariantCulture)}";
            Status = ProviderPaymentAttemptStatus.Created;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public string PaymentIntentId { get; private set; } = default!;

        public int AttemptNumber { get; private set; }

        public string ProviderProfileId { get; private set; } = default!;

        public string ProviderProfileVersion { get; private set; } = default!;

        public string IdempotencyKey { get; private set; } = default!;

        public string? ProviderTransactionRef { get; private set; }

        public ProviderPaymentAttemptStatus Status { get; private set; }

        public DateTimeOffset? CallbackReceivedAt { get; private set; }

        public DateTimeOffset? VerifyDeadline { get; private set; }

        public DateTimeOffset? VerificationStartedAt { get; private set; }

        public DateTimeOffset? VerifiedAt { get; private set; }

        public DateTimeOffset? SettledAt { get; private set; }

        public DateTimeOffset? ReversalExpectedAt { get; private set; }

        public DateTimeOffset? ReversedAt { get; private set; }

        public DateTimeOffset? UnknownSince { get; private set; }

        public string? FailureCode { get; private set; }

        public string? FailureReason { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public bool IsMoneyVerified => Status is ProviderPaymentAttemptStatus.Verified or ProviderPaymentAttemptStatus.Settled;

        public bool IsUnresolved => Status is ProviderPaymentAttemptStatus.Created
            or ProviderPaymentAttemptStatus.CustomerActionPending
            or ProviderPaymentAttemptStatus.CallbackReceived
            or ProviderPaymentAttemptStatus.VerificationPending
            or ProviderPaymentAttemptStatus.Verified
            or ProviderPaymentAttemptStatus.AutoReversalPending
            or ProviderPaymentAttemptStatus.Unknown;

        public bool IsDueForReversalAt(DateTimeOffset now)
            => Status is ProviderPaymentAttemptStatus.AutoReversalPending or ProviderPaymentAttemptStatus.Unknown
               && ReversalExpectedAt <= now;

        internal static ProviderPaymentAttempt Create(long id, string paymentIntentId, int attemptNumber, ProviderProfile profile, DateTimeOffset createdAt)
            => new(id, paymentIntentId, attemptNumber, profile, createdAt);

        internal void MarkCustomerActionPending(string? providerTransactionRef, DateTimeOffset now)
        {
            if (Status != ProviderPaymentAttemptStatus.Created)
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "await customer action");

            ProviderTransactionRef = providerTransactionRef;
            Status = ProviderPaymentAttemptStatus.CustomerActionPending;
            Touch(now);
        }

        internal void MarkUnknown(string? providerTransactionRef, DateTimeOffset? reversalExpectedAt, DateTimeOffset now)
        {
            if (IsMoneyVerified)
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "become Unknown");

            ProviderTransactionRef ??= providerTransactionRef;
            Status = ProviderPaymentAttemptStatus.Unknown;
            UnknownSince ??= now;
            ReversalExpectedAt = reversalExpectedAt;
            Touch(now);
        }

        internal void RecordCallback(DateTimeOffset callbackReceivedAt, DateTimeOffset? verifyDeadline)
        {
            CallbackReceivedAt ??= callbackReceivedAt;
            VerifyDeadline ??= verifyDeadline;

            if (Status is ProviderPaymentAttemptStatus.Created or ProviderPaymentAttemptStatus.CustomerActionPending or ProviderPaymentAttemptStatus.Unknown)
                Status = ProviderPaymentAttemptStatus.CallbackReceived;

            Touch(callbackReceivedAt);
        }

        internal void RecordPaymentEvidence(DateTimeOffset? verifyDeadline, DateTimeOffset now)
        {
            VerifyDeadline ??= verifyDeadline;
            Touch(now);
        }

        internal void BeginVerification(DateTimeOffset now)
        {
            if (IsMoneyVerified || !IsUnresolved)
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "begin verification");

            VerificationStartedAt ??= now;
            Status = ProviderPaymentAttemptStatus.VerificationPending;
            Touch(now);
        }

        internal void MarkVerified(string? providerTransactionRef, DateTimeOffset now)
        {
            if (Status is not (ProviderPaymentAttemptStatus.Created or ProviderPaymentAttemptStatus.VerificationPending))
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "become Verified");

            ProviderTransactionRef ??= providerTransactionRef;
            VerifiedAt = now;
            Status = ProviderPaymentAttemptStatus.Verified;
            Touch(now);
        }

        internal void MarkSettled(DateTimeOffset now)
        {
            if (Status != ProviderPaymentAttemptStatus.Verified)
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "become Settled");

            SettledAt = now;
            Status = ProviderPaymentAttemptStatus.Settled;
            Touch(now);
        }

        internal void MarkFailed(string? failureCode, string? failureReason, DateTimeOffset now)
        {
            if (IsMoneyVerified)
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "become Failed");

            FailureCode = failureCode;
            FailureReason = failureReason;
            Status = ProviderPaymentAttemptStatus.Failed;
            Touch(now);
        }

        internal void BeginAutoReversal(DateTimeOffset reversalExpectedAt, DateTimeOffset now)
        {
            if (IsMoneyVerified)
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "begin auto-reversal");

            ReversalExpectedAt = reversalExpectedAt;
            Status = ProviderPaymentAttemptStatus.AutoReversalPending;
            Touch(now);
        }

        internal void MarkReversed(DateTimeOffset now)
        {
            if (!IsDueForReversalAt(now))
                throw ExceptionFactory.ProviderAttemptCannotTransition(Id, Status, "become Reversed");

            ReversedAt = now;
            Status = ProviderPaymentAttemptStatus.Reversed;
            Touch(now);
        }

        private void Touch(DateTimeOffset now) => UpdatedAt = now;
    }
}
