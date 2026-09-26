using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentIntentAggregate
{
    public sealed class PaymentIntent : AggregateRoot<string>
    {
        private PaymentIntent()
        {
        }

        private PaymentIntent(string id, string paymentSessionId, int sequence, PaymentMethodOption option, decimal requestedAmount, DateTimeOffset createdAt)
        {
            Id = id;
            PaymentSessionId = paymentSessionId;
            Sequence = sequence;
            PaymentMethodOptionId = option.Id;
            TenderType = option.TenderType;
            RequestedAmount = requestedAmount;
            CurrencyId = option.CurrencyId;
            CaptureMode = option.CaptureMode;
            ProviderReference = option.FundingReference;
            Status = PaymentIntentStatus.Created;
            Version = 1;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public string PaymentSessionId { get; private set; } = default!;

        public int Sequence { get; private set; }

        public string PaymentMethodOptionId { get; private set; } = default!;

        public TenderType TenderType { get; private set; }

        public decimal RequestedAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public PaymentCaptureMode CaptureMode { get; private set; }

        public PaymentIntentStatus Status { get; private set; }

        public decimal AuthorizedAmount { get; private set; }

        public decimal GuaranteedAmount { get; private set; }

        public decimal CapturedAmount { get; private set; }

        public decimal RefundedAmount { get; private set; }

        public DateTimeOffset? GuaranteeExpiresAt { get; private set; }

        public CustomerAction? NextAction { get; private set; }

        public string? FailureCode { get; private set; }

        public string? FailureReason { get; private set; }

        public string? ProviderReference { get; private set; }

        public long Version { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public bool HoldsFunding => Status is not (PaymentIntentStatus.Failed or PaymentIntentStatus.Cancelled or PaymentIntentStatus.Expired);

        public bool IsDispatchPending => Status == PaymentIntentStatus.Created;

        public bool RequiresProviderRelease => Status is PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Authorized;

        public static PaymentIntent Create(
            string id,
            string paymentSessionId,
            int sequence,
            PaymentMethodOption option,
            decimal requestedAmount,
            int sessionCurrencyId,
            DateTimeOffset createdAt)
        {
            if (requestedAmount <= 0)
                throw ExceptionFactory.PaymentAmountMustBePositive(requestedAmount);

            if (option.CurrencyId != sessionCurrencyId)
                throw ExceptionFactory.CurrencyMismatch(sessionCurrencyId, option.CurrencyId);

            return new PaymentIntent(id, paymentSessionId, sequence, option, requestedAmount, createdAt);
        }

        public decimal ValidGuaranteeAt(DateTimeOffset now)
            => Status is PaymentIntentStatus.Authorized or PaymentIntentStatus.PartiallyCaptured or PaymentIntentStatus.Captured
               && !(GuaranteeExpiresAt <= now)
                ? GuaranteedAmount
                : 0;

        public decimal ValidFundsReceivedAt(DateTimeOffset now) => Math.Min(ValidGuaranteeAt(now), CapturedAmount - RefundedAmount);

        public void ApplyStartOutcome(TenderOutcome outcome, DateTimeOffset now)
        {
            if (Status != PaymentIntentStatus.Created)
                throw ExceptionFactory.ProviderOutcomeCannotBeRecorded(Id, Status, outcome.Kind);

            if (outcome.Kind == TenderOutcomeKind.Released)
                throw ExceptionFactory.ProviderOutcomeIsNotExpected(Id, outcome.Kind);

            Apply(outcome);
            Touch(now);
        }

        public bool RecordVerifiedOutcome(TenderOutcome outcome, bool unapplied, DateTimeOffset now)
        {
            var arrivedLate = Status switch
            {
                PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Processing
                    when outcome.Kind != TenderOutcomeKind.Released => false,
                PaymentIntentStatus.Cancelled or PaymentIntentStatus.Expired
                    when outcome.Kind == TenderOutcomeKind.Captured => true,
                _ => throw ExceptionFactory.ProviderOutcomeCannotBeRecorded(Id, Status, outcome.Kind)
            };

            if (outcome.Kind == TenderOutcomeKind.RequiresCustomerAction && Status == PaymentIntentStatus.RequiresCustomerAction && NextAction == outcome.CustomerAction)
                return false;

            if (outcome.Kind == TenderOutcomeKind.Processing && Status == PaymentIntentStatus.Processing)
                return false;

            Apply(outcome);

            if (arrivedLate || unapplied)
            {
                GuaranteedAmount = 0;
                GuaranteeExpiresAt = null;
            }

            Touch(now);
            return arrivedLate;
        }

        public void Cancel(DateTimeOffset now)
        {
            if (Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Authorized))
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "be cancelled");

            Status = PaymentIntentStatus.Cancelled;
            NextAction = null;
            GuaranteedAmount = 0;
            GuaranteeExpiresAt = null;
            Touch(now);
        }

        public bool IsDueForExpiryAt(DateTimeOffset now) => Status switch
        {
            PaymentIntentStatus.RequiresCustomerAction => NextAction?.ExpiresAt <= now,
            PaymentIntentStatus.Authorized => GuaranteeExpiresAt <= now,
            _ => false
        };

        public bool ExpireIfDue(DateTimeOffset now)
        {
            if (!IsDueForExpiryAt(now))
                return false;

            Expire(now);
            return true;
        }

        public void Expire(DateTimeOffset now)
        {
            if (Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Authorized))
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "expire");

            Status = PaymentIntentStatus.Expired;
            NextAction = null;
            GuaranteedAmount = 0;
            Touch(now);
        }

        private void Apply(TenderOutcome outcome)
        {
            if (outcome.ProviderReference is not null)
                ProviderReference = outcome.ProviderReference;

            switch (outcome.Kind)
            {
                case TenderOutcomeKind.RequiresCustomerAction:
                    Status = PaymentIntentStatus.RequiresCustomerAction;
                    NextAction = outcome.CustomerAction ?? throw ExceptionFactory.ProviderOutcomeIsNotExpected(Id, outcome.Kind);
                    break;

                case TenderOutcomeKind.Processing:
                    Status = PaymentIntentStatus.Processing;
                    NextAction = null;
                    break;

                case TenderOutcomeKind.Authorized:
                    if (CaptureMode != PaymentCaptureMode.Manual)
                        throw ExceptionFactory.ProviderOutcomeIsNotExpected(Id, outcome.Kind);

                    EnsureRequestedAmount(outcome.Amount);

                    Status = PaymentIntentStatus.Authorized;
                    NextAction = null;
                    AuthorizedAmount = outcome.Amount;
                    GuaranteedAmount = outcome.IssuanceGuarantee ? outcome.Amount : 0;
                    GuaranteeExpiresAt = outcome.IssuanceGuarantee ? outcome.AuthorizationExpiresAt : null;
                    break;

                case TenderOutcomeKind.Captured:
                    EnsureRequestedAmount(outcome.Amount);

                    Status = PaymentIntentStatus.Captured;
                    NextAction = null;
                    AuthorizedAmount = Math.Max(AuthorizedAmount, outcome.Amount);
                    CapturedAmount = outcome.Amount;
                    GuaranteedAmount = outcome.Amount;
                    GuaranteeExpiresAt = null;
                    FailureCode = null;
                    FailureReason = null;
                    break;

                case TenderOutcomeKind.Failed:
                    Status = PaymentIntentStatus.Failed;
                    NextAction = null;
                    GuaranteedAmount = 0;
                    GuaranteeExpiresAt = null;
                    FailureCode = outcome.FailureCode;
                    FailureReason = outcome.FailureReason;
                    break;

                default:
                    throw ExceptionFactory.ProviderOutcomeIsNotExpected(Id, outcome.Kind);
            }
        }

        private void EnsureRequestedAmount(decimal amount)
        {
            if (amount != RequestedAmount)
                throw ExceptionFactory.ProviderAmountMismatch(Id, amount, RequestedAmount);
        }

        private void Touch(DateTimeOffset now)
        {
            if (GuaranteedAmount < 0 || GuaranteedAmount > RequestedAmount || CapturedAmount < 0 || CapturedAmount > RequestedAmount || CapturedAmount > AuthorizedAmount)
                throw ExceptionFactory.InvariantViolation(Id, "0 <= Captured <= Authorized <= Requested and 0 <= Guaranteed <= Requested");

            Version++;
            UpdatedAt = now;
        }
    }
}
