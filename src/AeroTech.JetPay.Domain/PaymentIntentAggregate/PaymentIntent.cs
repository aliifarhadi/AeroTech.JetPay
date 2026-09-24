using System.Globalization;
using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Arguments;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.DomainEvents;
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

        private PaymentIntent(string id, CreatePaymentIntentArgs args, DateTimeOffset createdAt)
        {
            Id = id;
            PayableInstructionId = args.PayableInstructionId;
            OrderId = args.OrderId;
            OrderReference = args.OrderReference;
            CommercialVersion = args.CommercialVersion;
            Purpose = args.Purpose;
            PayerType = args.PayerType;
            PayerId = args.PayerId;
            RequestedAmount = args.Amount;
            CurrencyId = args.CurrencyId;
            RequiredGuarantee = args.RequiredGuarantee;
            CaptureMode = args.CaptureMode;
            IntentExpiresAt = args.IntentExpiresAt;
            Status = PaymentIntentStatus.Created;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public string PayableInstructionId { get; private set; } = default!;

        public long OrderId { get; private set; }

        public string OrderReference { get; private set; } = default!;

        public int CommercialVersion { get; private set; }

        public PaymentPurpose Purpose { get; private set; }

        public PayerType PayerType { get; private set; }

        public long PayerId { get; private set; }

        public decimal RequestedAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public RequiredGuarantee RequiredGuarantee { get; private set; }

        public PaymentCaptureMode CaptureMode { get; private set; }

        public PaymentIntentStatus Status { get; private set; }

        public decimal AuthorizedAmount { get; private set; }

        public decimal GuaranteedAmount { get; private set; }

        public decimal CapturedAmount { get; private set; }

        public decimal RefundedAmount { get; private set; }

        public DateTimeOffset? GuaranteeExpiresAt { get; private set; }

        public DateTimeOffset? IntentExpiresAt { get; private set; }

        public TenderType? SelectedTenderType { get; private set; }

        public string? SelectedPaymentMethodOptionId { get; private set; }

        public CustomerAction? NextAction { get; private set; }

        public string? FailureCode { get; private set; }

        public string? FailureReason { get; private set; }

        public long Version { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public bool IsActive => Status is not (PaymentIntentStatus.Failed or PaymentIntentStatus.Cancelled or PaymentIntentStatus.Expired);

        public decimal CapturableAmount => AuthorizedAmount - CapturedAmount;

        public bool RequiresProviderRelease => Status is PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Authorized;

        public static PaymentIntent Create(string id, CreatePaymentIntentArgs args, IIdGenerator idGenerator, DateTimeOffset createdAt)
        {
            if (args.Amount <= 0)
                throw ExceptionFactory.PaymentAmountMustBePositive(args.Amount);

            if (args.IntentExpiresAt <= createdAt)
                throw ExceptionFactory.IntentExpiryMustBeInTheFuture(args.IntentExpiresAt, createdAt);

            var intent = new PaymentIntent(id, args, createdAt);
            intent.Changed(idGenerator, createdAt);

            return intent;
        }

        public bool HasTermsOf(CreatePaymentIntentArgs args)
            => PayableInstructionId == args.PayableInstructionId
               && OrderId == args.OrderId
               && OrderReference == args.OrderReference
               && CommercialVersion == args.CommercialVersion
               && Purpose == args.Purpose
               && PayerType == args.PayerType
               && PayerId == args.PayerId
               && RequestedAmount == args.Amount
               && CurrencyId == args.CurrencyId
               && RequiredGuarantee == args.RequiredGuarantee
               && CaptureMode == args.CaptureMode
               && IntentExpiresAt == args.IntentExpiresAt;

        public void EnsureConfirmable()
        {
            if (Status != PaymentIntentStatus.Created)
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "be confirmed");
        }

        public void Confirm(PaymentMethodOption option, TenderOutcome outcome, IIdGenerator idGenerator, DateTimeOffset now)
        {
            EnsureConfirmable();

            if (!option.Supports(RequiredGuarantee, CaptureMode))
                throw ExceptionFactory.PaymentMethodOptionIncompatible(option.Id, RequiredGuarantee, CaptureMode);

            if (outcome.Kind == TenderOutcomeKind.Released)
                throw ExceptionFactory.ProviderOutcomeIsNotExpected(Id, outcome.Kind);

            SelectedPaymentMethodOptionId = option.Id;
            SelectedTenderType = option.TenderType;

            Apply(outcome);
            Changed(idGenerator, now);
        }

        /// <summary>
        /// Records a server-side verified provider outcome. Money that the provider captured after the intent was
        /// closed, or for a superseded payable instruction, is recorded truthfully but never exposed as guarantee.
        /// </summary>
        public void RecordVerifiedOutcome(TenderOutcome outcome, bool instructionSuperseded, IIdGenerator idGenerator, DateTimeOffset now)
        {
            var unappliedReason = Status switch
            {
                PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Processing
                    when outcome.Kind is not TenderOutcomeKind.Released => instructionSuperseded && outcome.Kind == TenderOutcomeKind.Captured
                        ? PaidUnappliedReason.PayableInstructionSuperseded
                        : null,
                PaymentIntentStatus.Cancelled when outcome.Kind == TenderOutcomeKind.Captured => PaidUnappliedReason.PaymentIntentCancelled,
                PaymentIntentStatus.Expired when outcome.Kind == TenderOutcomeKind.Captured => PaidUnappliedReason.PaymentIntentExpired,
                _ => throw ExceptionFactory.ProviderOutcomeCannotBeRecorded(Id, Status, outcome.Kind)
            };

            if (outcome.Kind == TenderOutcomeKind.RequiresCustomerAction && Status == PaymentIntentStatus.RequiresCustomerAction && NextAction == outcome.CustomerAction)
                return;

            if (outcome.Kind == TenderOutcomeKind.Processing && Status == PaymentIntentStatus.Processing)
                return;

            Apply(outcome);

            if (unappliedReason is not null)
            {
                GuaranteedAmount = 0;
                GuaranteeExpiresAt = null;
            }

            Changed(idGenerator, now);

            if (unappliedReason is not null)
                RecordPaidUnapplied(unappliedReason, idGenerator, now);
        }

        public decimal EnsureCapturable(decimal? requestedAmount)
        {
            if (Status is not (PaymentIntentStatus.Authorized or PaymentIntentStatus.PartiallyCaptured))
                throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "be captured");

            if (CaptureMode != PaymentCaptureMode.Manual)
                throw ExceptionFactory.CaptureIsNotAllowedForCaptureMode(Id, CaptureMode);

            var amount = requestedAmount ?? CapturableAmount;

            if (amount <= 0 || amount > CapturableAmount)
                throw ExceptionFactory.CaptureAmountExceedsCapturable(Id, amount, CapturableAmount);

            return amount;
        }

        public void RecordCapture(decimal amount, bool finalCapture, IIdGenerator idGenerator, DateTimeOffset now)
        {
            EnsureCapturable(amount);

            CapturedAmount += amount;

            if (finalCapture || CapturedAmount == AuthorizedAmount)
            {
                Status = PaymentIntentStatus.Captured;
                GuaranteedAmount = CapturedAmount;
                GuaranteeExpiresAt = null;
            }
            else
            {
                Status = PaymentIntentStatus.PartiallyCaptured;
                GuaranteedAmount = RequiredGuarantee == RequiredGuarantee.PaidBeforeIssuance
                    ? CapturedAmount
                    : Math.Max(GuaranteedAmount, CapturedAmount);
            }

            Changed(idGenerator, now);
        }

        public void EnsureCancellable()
        {
            switch (Status)
            {
                case PaymentIntentStatus.Created:
                case PaymentIntentStatus.RequiresCustomerAction:
                case PaymentIntentStatus.Authorized:
                case PaymentIntentStatus.Cancelled:
                    return;

                case PaymentIntentStatus.Processing:
                    throw ExceptionFactory.PaymentIntentOutcomePending(Id);

                case PaymentIntentStatus.PartiallyCaptured:
                case PaymentIntentStatus.Captured:
                    throw ExceptionFactory.CapturedPaymentIntentCannotBeCancelled(Id, CapturedAmount);

                default:
                    throw ExceptionFactory.PaymentIntentCannotTransition(Id, Status, "be cancelled");
            }
        }

        public void Cancel(IIdGenerator idGenerator, DateTimeOffset now)
        {
            EnsureCancellable();

            if (Status == PaymentIntentStatus.Cancelled)
                return;

            Status = PaymentIntentStatus.Cancelled;
            NextAction = null;
            GuaranteedAmount = 0;
            GuaranteeExpiresAt = null;

            Changed(idGenerator, now);
        }

        public bool IsDueForExpiryAt(DateTimeOffset now) => Status switch
        {
            PaymentIntentStatus.Created => IntentExpiresAt <= now,
            PaymentIntentStatus.RequiresCustomerAction => IntentExpiresAt <= now || NextAction?.ExpiresAt <= now,
            PaymentIntentStatus.Authorized => GuaranteeExpiresAt <= now || IntentExpiresAt <= now,
            PaymentIntentStatus.PartiallyCaptured => GuaranteedAmount > CapturedAmount && GuaranteeExpiresAt <= now,
            _ => false
        };

        /// <summary>
        /// An expired guarantee contributes zero; the historical <see cref="AuthorizedAmount"/> and
        /// <see cref="GuaranteeExpiresAt"/> are kept as evidence.
        /// </summary>
        public bool ExpireIfDue(IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (!IsDueForExpiryAt(now))
                return false;

            if (Status == PaymentIntentStatus.PartiallyCaptured)
            {
                GuaranteedAmount = CapturedAmount;
                GuaranteeExpiresAt = null;
            }
            else
            {
                Status = PaymentIntentStatus.Expired;
                NextAction = null;
                GuaranteedAmount = 0;
            }

            Changed(idGenerator, now);
            return true;
        }

        private void Apply(TenderOutcome outcome)
        {
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

                    EnsureFullAmount(outcome.Amount);

                    Status = PaymentIntentStatus.Authorized;
                    NextAction = null;
                    AuthorizedAmount = outcome.Amount;

                    var guaranteed = RequiredGuarantee == RequiredGuarantee.AuthorizedBeforeIssuance && outcome.IssuanceGuarantee;
                    GuaranteedAmount = guaranteed ? outcome.Amount : 0;
                    GuaranteeExpiresAt = guaranteed ? outcome.AuthorizationExpiresAt : null;
                    break;

                case TenderOutcomeKind.Captured:
                    EnsureFullAmount(outcome.Amount);

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

        private void EnsureFullAmount(decimal amount)
        {
            if (amount != RequestedAmount)
                throw ExceptionFactory.ProviderAmountMismatch(Id, amount, RequestedAmount);
        }

        private void RecordPaidUnapplied(string reasonCode, IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (CapturedAmount <= 0)
                throw ExceptionFactory.PaidUnappliedRequiresCapturedMoney(Id);

            Causes(new PaymentPaidUnapplied(
                NewEventId(idGenerator),
                Id,
                now,
                Id,
                OrderId,
                PayableInstructionId,
                CapturedAmount,
                CurrencyId,
                reasonCode,
                now));
        }

        private void Changed(IIdGenerator idGenerator, DateTimeOffset now)
        {
            EnsureInvariants();

            Version++;
            UpdatedAt = now;

            Causes(new PaymentIntentChanged(
                NewEventId(idGenerator),
                Id,
                now,
                Id,
                PayableInstructionId,
                OrderId,
                CommercialVersion,
                Purpose,
                Status,
                RequiredGuarantee,
                CaptureMode,
                RequestedAmount,
                AuthorizedAmount,
                GuaranteedAmount,
                CapturedAmount,
                CurrencyId,
                GuaranteeExpiresAt,
                IntentExpiresAt,
                Version,
                FailureCode,
                now));
        }

        private void EnsureInvariants()
        {
            if (GuaranteedAmount < 0 || GuaranteedAmount > RequestedAmount)
                throw ExceptionFactory.InvariantViolation(Id, "0 <= GuaranteedAmount <= RequestedAmount");

            if (CapturedAmount < 0 || CapturedAmount > RequestedAmount)
                throw ExceptionFactory.InvariantViolation(Id, "0 <= CapturedAmount <= RequestedAmount");

            if (CapturedAmount > AuthorizedAmount)
                throw ExceptionFactory.InvariantViolation(Id, "CapturedAmount <= AuthorizedAmount");

            if (RequiredGuarantee == RequiredGuarantee.PaidBeforeIssuance && GuaranteedAmount > CapturedAmount)
                throw ExceptionFactory.InvariantViolation(Id, "GuaranteedAmount <= CapturedAmount for PaidBeforeIssuance");
        }

        private static string NewEventId(IIdGenerator idGenerator)
            => idGenerator.NewId().ToString(CultureInfo.InvariantCulture);
    }
}
