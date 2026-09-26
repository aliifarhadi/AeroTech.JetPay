using System.Globalization;
using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Arguments;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.DomainEvents;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Entities;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate
{
    public sealed class PaymentSession : AggregateRoot<string>
    {
        private readonly List<PaymentIntent> _intents = new();

        private PaymentSession()
        {
        }

        private PaymentSession(string id, CreatePaymentSessionArgs args, DateTimeOffset createdAt)
        {
            Id = id;
            PayableInstructionId = args.PayableInstructionId;
            OrderId = args.OrderId;
            OrderReference = args.OrderReference;
            CommercialVersion = args.CommercialVersion;
            Purpose = args.Purpose;
            IssuerLegalEntityId = args.IssuerLegalEntityId;
            PayerType = args.PayerType;
            PayerId = args.PayerId;
            Initiator = args.Initiator;
            InteractionMode = args.InteractionMode;
            SelectionMode = args.SelectionMode;
            RequiredAmount = args.RequiredAmount;
            CurrencyId = args.CurrencyId;
            AssuranceRequirement = args.AssuranceRequirement;
            ExpiresAt = args.ExpiresAt;
            Status = args.SelectionMode == PaymentSelectionMode.Interactive
                ? PaymentSessionStatus.RequiresPaymentMethod
                : PaymentSessionStatus.Created;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public string PayableInstructionId { get; private set; } = default!;

        public long OrderId { get; private set; }

        public string OrderReference { get; private set; } = default!;

        public int CommercialVersion { get; private set; }

        public PaymentPurpose Purpose { get; private set; }

        public long IssuerLegalEntityId { get; private set; }

        public PayerType PayerType { get; private set; }

        public long PayerId { get; private set; }

        public PaymentInitiatorContext Initiator { get; private set; } = default!;

        public PaymentInteractionMode InteractionMode { get; private set; }

        public PaymentSelectionMode SelectionMode { get; private set; }

        public decimal RequiredAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public PaymentAssuranceRequirement AssuranceRequirement { get; private set; }

        public PaymentSessionStatus Status { get; private set; }

        public decimal GuaranteedAmount { get; private set; }

        public decimal CapturedAmount { get; private set; }

        public decimal OutstandingAmount { get; private set; }

        public DateTimeOffset? ExpiresAt { get; private set; }

        public string? FailureCode { get; private set; }

        public long Version { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public IReadOnlyCollection<PaymentIntent> Intents => _intents.AsReadOnly();

        public bool IsTerminal => Status is PaymentSessionStatus.Failed or PaymentSessionStatus.Cancelled or PaymentSessionStatus.Expired;

        public decimal FundableAmount
            => Math.Max(0, RequiredAmount - _intents.Where(intent => intent.HoldsFunding).Sum(intent => intent.RequestedAmount));

        public bool HasUnresolvedProviderEffect => _intents.Any(intent => intent.HasUnresolvedProviderEffect);

        public static PaymentSession Create(string id, CreatePaymentSessionArgs args, IIdGenerator idGenerator, DateTimeOffset createdAt)
        {
            if (args.RequiredAmount < 0)
                throw ExceptionFactory.PaymentAmountMustNotBeNegative(args.RequiredAmount);

            if (args.ExpiresAt <= createdAt)
                throw ExceptionFactory.SessionExpiryMustBeInTheFuture(args.ExpiresAt, createdAt);

            var session = new PaymentSession(id, args, createdAt);
            session.Refresh(idGenerator, createdAt, force: true);

            return session;
        }

        public bool HasTermsOf(CreatePaymentSessionArgs args)
            => PayableInstructionId == args.PayableInstructionId
               && OrderId == args.OrderId
               && OrderReference == args.OrderReference
               && CommercialVersion == args.CommercialVersion
               && Purpose == args.Purpose
               && IssuerLegalEntityId == args.IssuerLegalEntityId
               && PayerType == args.PayerType
               && PayerId == args.PayerId
               && Initiator == args.Initiator
               && InteractionMode == args.InteractionMode
               && SelectionMode == args.SelectionMode
               && RequiredAmount == args.RequiredAmount
               && CurrencyId == args.CurrencyId
               && AssuranceRequirement == args.AssuranceRequirement
               && ExpiresAt == args.ExpiresAt;

        public bool IsDueForExpiryAt(DateTimeOffset now)
            => !IsTerminal
               && Status is not (PaymentSessionStatus.Paid or PaymentSessionStatus.Guaranteed)
               && ExpiresAt <= now;

        public PaymentIntent Intent(string paymentIntentId)
            => _intents.SingleOrDefault(intent => intent.Id == paymentIntentId)
               ?? throw ExceptionFactory.PaymentIntentNotFound(paymentIntentId);

        public void EnsureAcceptsFunding(DateTimeOffset now)
        {
            if (IsTerminal)
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "accept funding");

            if (IsDueForExpiryAt(now))
                throw ExceptionFactory.PaymentSessionExpired(Id);

            if (HasUnresolvedProviderEffect)
                throw ExceptionFactory.ProviderEffectUnresolved(Id);

            if (FundableAmount <= 0)
                throw ExceptionFactory.NothingToFund(Id);
        }

        public PaymentIntent AddIntent(string paymentIntentId, PaymentMethodOption option, decimal amount, IIdGenerator idGenerator, DateTimeOffset now)
        {
            EnsureAcceptsFunding(now);

            if (option.CurrencyId != CurrencyId)
                throw ExceptionFactory.CurrencyMismatch(CurrencyId, option.CurrencyId);

            if (amount <= 0 || amount > FundableAmount)
                throw ExceptionFactory.InvariantViolation(Id, "0 < intent amount <= fundable amount");

            var intent = PaymentIntent.Create(paymentIntentId, Id, option, amount, now);
            _intents.Add(intent);
            FailureCode = null;

            Refresh(idGenerator, now, force: true);
            return intent;
        }

        public void RecordFundingFailure(string failureCode, IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (IsTerminal)
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "record a funding failure");

            FailureCode = failureCode;
            Refresh(idGenerator, now, force: true);
        }

        public ProviderPaymentAttempt OpenProviderAttempt(PaymentIntent intent, long attemptId, ProviderProfile profile, DateTimeOffset now)
        {
            Own(intent);

            if (IsTerminal)
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "start a provider attempt");

            return intent.OpenAttempt(attemptId, profile, now);
        }

        public void RecordCustomerActionRequired(
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            CustomerAction action,
            string? providerTransactionRef,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkCustomerActionPending(providerTransactionRef, now);
            intent.AwaitCustomerAction(action, now);
            Refresh(idGenerator, now);
        }

        public void RecordRouteUnavailable(PaymentIntent intent, ProviderPaymentAttempt attempt, string? failureCode, string? failureReason, DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkFailed(failureCode, failureReason, now);
            intent.Touch(now);
        }

        public void FailIntent(PaymentIntent intent, string failureCode, string? failureReason, IIdGenerator idGenerator, DateTimeOffset now)
        {
            Own(intent);
            intent.Fail(failureCode, failureReason, now);
            Refresh(idGenerator, now);
        }

        public void RecordProviderDeclined(
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            string? failureCode,
            string? failureReason,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkFailed(failureCode, failureReason, now);

            if (intent.IsOpen)
                intent.Fail(failureCode, failureReason, now);

            Refresh(idGenerator, now);
        }

        public void RecordProviderUnknown(
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            string? providerTransactionRef,
            DateTimeOffset? reversalExpectedAt,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkUnknown(providerTransactionRef, reversalExpectedAt, now);

            if (intent.IsOpen)
                intent.AwaitProvider(now);

            Refresh(idGenerator, now);
        }

        public PaymentEvidenceDecision RecordPaymentEvidence(
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            ProviderProfile profile,
            DateTimeOffset paidAt,
            bool fromCallback,
            bool instructionSuperseded,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            Own(intent, attempt);

            var verifyDeadline = attempt.VerifyDeadline ?? (profile.DefaultVerifyWindow is { } window ? paidAt + window : null);

            if (fromCallback)
                attempt.RecordCallback(now, verifyDeadline);
            else
                attempt.RecordPaymentEvidence(verifyDeadline, now);

            intent.Touch(now);

            if (attempt.IsMoneyVerified)
            {
                if (intent.CapturedAmount > 0 && (IsTerminal || instructionSuperseded))
                    ReportPaidUnapplied(intent, UnappliedReason(instructionSuperseded), idGenerator, now);

                return PaymentEvidenceDecision.AlreadyVerified;
            }

            var unusable = IsTerminal || IsDueForExpiryAt(now) || instructionSuperseded || !intent.IsOpen;
            var late = attempt.VerifyDeadline < now;

            if (!unusable && !late)
                return PaymentEvidenceDecision.Verify;

            var failureCode = late ? IntentFailureCode.VerifyDeadlineElapsed : IntentFailureCode.PaymentNoLongerApplicable;

            if (profile.UnverifiedPaymentExpiryBehavior == UnverifiedPaymentExpiryBehavior.AutoReverse)
            {
                BeginAutoReversal(intent, attempt, attempt.VerifyDeadline ?? now, failureCode, idGenerator, now);
                return PaymentEvidenceDecision.AutoReversal;
            }

            if (intent.IsOpen && !IsTerminal)
                intent.AwaitProvider(now);

            Refresh(idGenerator, now);
            return PaymentEvidenceDecision.AwaitReconciliation;
        }

        public void BeginVerification(PaymentIntent intent, ProviderPaymentAttempt attempt, IIdGenerator idGenerator, DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.BeginVerification(now);

            if (intent.IsOpen)
                intent.AwaitProvider(now);

            Refresh(idGenerator, now);
        }

        public void RecordVerified(
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            string? providerTransactionRef,
            bool settlementRequired,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkVerified(providerTransactionRef, now);

            if (!settlementRequired)
                intent.Capture(now);

            Refresh(idGenerator, now);
        }

        public void RecordSettled(PaymentIntent intent, ProviderPaymentAttempt attempt, IIdGenerator idGenerator, DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkSettled(now);
            intent.Capture(now);
            Refresh(idGenerator, now);
        }

        public void BeginAutoReversal(
            PaymentIntent intent,
            ProviderPaymentAttempt attempt,
            DateTimeOffset reversalExpectedAt,
            string failureCode,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.BeginAutoReversal(reversalExpectedAt, now);

            if (intent.IsOpen)
                intent.Fail(failureCode, null, now);

            Refresh(idGenerator, now);
        }

        public void RecordReversed(PaymentIntent intent, ProviderPaymentAttempt attempt, IIdGenerator idGenerator, DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkReversed(now);

            if (intent.IsOpen)
                intent.Fail(IntentFailureCode.ProviderReversed, null, now);

            Refresh(idGenerator, now);
        }

        public void RecordNoProviderEffect(PaymentIntent intent, ProviderPaymentAttempt attempt, string? failureCode, string? failureReason, IIdGenerator idGenerator, DateTimeOffset now)
        {
            Own(intent, attempt);
            attempt.MarkFailed(failureCode, failureReason, now);

            if (intent.Status is PaymentIntentStatus.RequiresCustomerAction or PaymentIntentStatus.Processing)
                intent.Fail(failureCode, failureReason, now);

            Refresh(idGenerator, now);
        }

        public void ExpireCustomerAction(PaymentIntent intent, IIdGenerator idGenerator, DateTimeOffset now)
        {
            Own(intent);

            if (!intent.IsCustomerActionLapsedAt(now))
                throw ExceptionFactory.PaymentIntentCannotTransition(intent.Id, intent.Status, "expire");

            intent.Close(PaymentIntentStatus.Expired, now);
            Refresh(idGenerator, now);
        }

        public void Cancel(IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (IsTerminal)
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "be cancelled");

            Close(PaymentSessionStatus.Cancelled, PaymentIntentStatus.Cancelled, idGenerator, now);
        }

        public void Expire(IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (!IsDueForExpiryAt(now))
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "expire");

            Close(PaymentSessionStatus.Expired, PaymentIntentStatus.Expired, idGenerator, now);
        }

        public void ReportPaidUnapplied(PaymentIntent intent, string reasonCode, IIdGenerator idGenerator, DateTimeOffset now)
        {
            Own(intent);

            if (!intent.MarkPaidUnapplied(now))
                return;

            Causes(new PaymentPaidUnapplied(
                NewEventId(idGenerator),
                Id,
                now,
                Id,
                intent.Id,
                OrderId,
                PayableInstructionId,
                intent.CapturedAmount,
                CurrencyId,
                reasonCode,
                now));
        }

        private void Close(PaymentSessionStatus sessionStatus, PaymentIntentStatus intentStatus, IIdGenerator idGenerator, DateTimeOffset now)
        {
            foreach (var intent in _intents.Where(intent => intent.Status is PaymentIntentStatus.Created or PaymentIntentStatus.RequiresCustomerAction))
                intent.Close(intentStatus, now);

            Status = sessionStatus;

            foreach (var intent in _intents.Where(intent => intent.CapturedAmount > 0))
                ReportPaidUnapplied(intent, UnappliedReason(instructionSuperseded: false), idGenerator, now);

            Refresh(idGenerator, now, force: true);
        }

        private string UnappliedReason(bool instructionSuperseded) => Status switch
        {
            PaymentSessionStatus.Cancelled => PaidUnappliedReason.PaymentSessionCancelled,
            PaymentSessionStatus.Expired => PaidUnappliedReason.PaymentSessionExpired,
            _ when instructionSuperseded => PaidUnappliedReason.PayableInstructionSuperseded,
            _ => PaidUnappliedReason.PaymentSessionClosed
        };

        private void Refresh(IIdGenerator idGenerator, DateTimeOffset now, bool force = false)
        {
            var before = Snapshot();

            CapturedAmount = _intents.Sum(intent => intent.CapturedAmount);
            GuaranteedAmount = IsTerminal
                ? 0
                : Math.Min(
                    RequiredAmount,
                    AssuranceRequirement == PaymentAssuranceRequirement.FundsReceived
                        ? CapturedAmount
                        : _intents.Sum(intent => intent.ValidGuaranteeAt(now)));
            OutstandingAmount = Math.Max(0, RequiredAmount - GuaranteedAmount);

            if (!IsTerminal)
            {
                Status = DeriveStatus(now);
                FailureCode = Status is PaymentSessionStatus.RequiresPaymentMethod or PaymentSessionStatus.PartiallyFunded
                    ? FailureCode ?? LatestFailureCode()
                    : null;
            }

            if (force || before != Snapshot())
                Changed(idGenerator, now);
        }

        private PaymentSessionStatus DeriveStatus(DateTimeOffset now)
        {
            if (CapturedAmount >= RequiredAmount)
                return PaymentSessionStatus.Paid;

            if (GuaranteedAmount >= RequiredAmount)
                return PaymentSessionStatus.Guaranteed;

            if (GuaranteedAmount > 0)
                return PaymentSessionStatus.PartiallyFunded;

            if (_intents.Any(intent => intent.IsOpen || (intent.Status == PaymentIntentStatus.Authorized && intent.ValidGuaranteeAt(now) == 0)))
                return PaymentSessionStatus.Processing;

            return Status == PaymentSessionStatus.Created && _intents.Count == 0 && FailureCode is null
                ? PaymentSessionStatus.Created
                : PaymentSessionStatus.RequiresPaymentMethod;
        }

        private string? LatestFailureCode()
            => _intents
                .Where(intent => intent.Status == PaymentIntentStatus.Failed)
                .OrderByDescending(intent => intent.UpdatedAt)
                .ThenByDescending(intent => intent.CreatedAt)
                .Select(intent => intent.FailureCode)
                .FirstOrDefault();

        private (PaymentSessionStatus, decimal, decimal, decimal, string?) Snapshot()
            => (Status, GuaranteedAmount, CapturedAmount, OutstandingAmount, FailureCode);

        private void Changed(IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (GuaranteedAmount < 0 || GuaranteedAmount > RequiredAmount)
                throw ExceptionFactory.InvariantViolation(Id, "0 <= GuaranteedAmount <= RequiredAmount");

            Version++;
            UpdatedAt = now;

            Causes(new PaymentSessionChanged(
                NewEventId(idGenerator),
                Id,
                now,
                Id,
                PayableInstructionId,
                OrderId,
                CommercialVersion,
                Purpose,
                Status,
                AssuranceRequirement,
                RequiredAmount,
                GuaranteedAmount,
                CapturedAmount,
                OutstandingAmount,
                CurrencyId,
                ExpiresAt,
                Version,
                FailureCode,
                now));
        }

        private void Own(PaymentIntent intent)
        {
            if (!_intents.Contains(intent))
                throw ExceptionFactory.InvariantViolation(Id, $"intent '{intent.Id}' belongs to another session");
        }

        private void Own(PaymentIntent intent, ProviderPaymentAttempt attempt)
        {
            Own(intent);

            if (!intent.ProviderAttempts.Contains(attempt))
                throw ExceptionFactory.InvariantViolation(Id, $"attempt {attempt.Id} does not belong to intent '{intent.Id}'");
        }

        private static string NewEventId(IIdGenerator idGenerator)
            => idGenerator.NewId().ToString(CultureInfo.InvariantCulture);
    }
}
