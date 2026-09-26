using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities
{
    public sealed class PaymentIntent : Entity<string>
    {
        private readonly List<ProviderPaymentAttempt> _providerAttempts = new();

        private PaymentIntent()
        {
        }

        private PaymentIntent(string id, string paymentSessionId, PaymentMethodOption option, decimal requestedAmount, DateTimeOffset createdAt)
        {
            Id = id;
            PaymentSessionId = paymentSessionId;
            PaymentMethodOptionId = option.Id;
            TenderType = option.TenderType;
            FundingReference = option.FundingReference;
            RequestedAmount = requestedAmount;
            CurrencyId = option.CurrencyId;
            Status = PaymentIntentStatus.Created;
            Version = 1;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public string PaymentSessionId { get; private set; } = default!;

        public string PaymentMethodOptionId { get; private set; } = default!;

        public TenderType TenderType { get; private set; }

        public string? FundingReference { get; private set; }

        public decimal RequestedAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public PaymentIntentStatus Status { get; private set; }

        public decimal AuthorizedAmount { get; private set; }

        public decimal GuaranteedAmount { get; private set; }

        public decimal CapturedAmount { get; private set; }

        public decimal RefundedAmount { get; private set; }

        public DateTimeOffset? GuaranteeExpiresAt { get; private set; }

        public CustomerAction? NextAction { get; private set; }

        public string? FailureCode { get; private set; }

        public string? FailureReason { get; private set; }

        public DateTimeOffset? PaidUnappliedAt { get; private set; }

        public long Version { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public IReadOnlyCollection<ProviderPaymentAttempt> ProviderAttempts => _providerAttempts.AsReadOnly();

        public bool IsOpen => Status is PaymentIntentStatus.Created or PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Processing;

        public bool HoldsFunding => Status is not (PaymentIntentStatus.Failed or PaymentIntentStatus.Cancelled or PaymentIntentStatus.Expired);

        public bool HasUnresolvedProviderEffect
            => Status != PaymentIntentStatus.Captured && _providerAttempts.Any(attempt => attempt.IsUnresolved);

        public ProviderPaymentAttempt? CurrentAttempt
            => _providerAttempts.OrderByDescending(attempt => attempt.AttemptNumber).FirstOrDefault();

        public bool AwaitsDispatch => Status == PaymentIntentStatus.Created
                                      && CurrentAttempt?.Status is null or ProviderPaymentAttemptStatus.Created or ProviderPaymentAttemptStatus.Failed;

        public bool IsCustomerActionLapsedAt(DateTimeOffset now)
            => Status == PaymentIntentStatus.RequiresCustomerAction && NextAction?.ExpiresAt <= now;

        public decimal ValidGuaranteeAt(DateTimeOffset now)
            => Status is PaymentIntentStatus.Authorized or PaymentIntentStatus.PartiallyCaptured or PaymentIntentStatus.Captured
               && !(GuaranteeExpiresAt <= now)
                ? GuaranteedAmount
                : 0;

        public bool HasTriedProfile(string providerProfileId)
            => _providerAttempts.Any(attempt => attempt.ProviderProfileId == providerProfileId);

        public ProviderPaymentAttempt Attempt(long attemptId)
            => _providerAttempts.SingleOrDefault(attempt => attempt.Id == attemptId)
               ?? throw ExceptionFactory.InvariantViolation(Id, $"attempt {attemptId} does not belong to this intent");

        internal static PaymentIntent Create(string id, string paymentSessionId, PaymentMethodOption option, decimal requestedAmount, DateTimeOffset createdAt)
            => new(id, paymentSessionId, option, requestedAmount, createdAt);

        internal ProviderPaymentAttempt OpenAttempt(long attemptId, ProviderProfile profile, DateTimeOffset now)
        {
            if (Status != PaymentIntentStatus.Created || HasUnresolvedProviderEffect)
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "open a provider attempt");

            var attempt = ProviderPaymentAttempt.Create(attemptId, Id, _providerAttempts.Count + 1, profile, now);
            _providerAttempts.Add(attempt);
            Touch(now);

            return attempt;
        }

        internal void AwaitCustomerAction(CustomerAction action, DateTimeOffset now)
        {
            if (Status != PaymentIntentStatus.Created)
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "await customer action");

            Status = PaymentIntentStatus.RequiresCustomerAction;
            NextAction = action;
            Touch(now);
        }

        internal void AwaitProvider(DateTimeOffset now)
        {
            if (!IsOpen)
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "await the provider");

            Status = PaymentIntentStatus.Processing;
            NextAction = null;
            Touch(now);
        }

        internal void Capture(DateTimeOffset now)
        {
            if (!IsOpen)
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "be captured");

            Status = PaymentIntentStatus.Captured;
            NextAction = null;
            AuthorizedAmount = RequestedAmount;
            CapturedAmount = RequestedAmount;
            GuaranteedAmount = RequestedAmount;
            GuaranteeExpiresAt = null;
            FailureCode = null;
            FailureReason = null;
            Touch(now);
        }

        internal void Fail(string? failureCode, string? failureReason, DateTimeOffset now)
        {
            if (!IsOpen)
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "fail");

            Status = PaymentIntentStatus.Failed;
            NextAction = null;
            GuaranteedAmount = 0;
            FailureCode = failureCode;
            FailureReason = failureReason;
            Touch(now);
        }

        internal void Close(PaymentIntentStatus closedStatus, DateTimeOffset now)
        {
            if (Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.RequiresCustomerAction))
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, $"become {closedStatus}");

            Status = closedStatus;
            NextAction = null;
            GuaranteedAmount = 0;
            Touch(now);
        }

        internal bool MarkPaidUnapplied(DateTimeOffset now)
        {
            if (CapturedAmount <= 0)
                throw ExceptionFactory.PaidUnappliedRequiresCapturedMoney(Id);

            if (PaidUnappliedAt is not null)
                return false;

            PaidUnappliedAt = now;
            Touch(now);
            return true;
        }

        internal void Touch(DateTimeOffset now)
        {
            Version++;
            UpdatedAt = now;
        }
    }
}
