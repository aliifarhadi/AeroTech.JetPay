using System.Globalization;
using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Arguments;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.DomainEvents;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.ValueObjects;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentSessionAggregate
{
    public sealed class PaymentSession : AggregateRoot<string>
    {
        private readonly List<string> _paymentIntentIds = new();

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
            PayerType = args.PayerType;
            PayerId = args.PayerId;
            InitiatorContext = args.InitiatorContext;
            RequiredAmount = args.Amount;
            CurrencyId = args.CurrencyId;
            AssuranceRequirement = args.AssuranceRequirement;
            InteractionMode = args.InteractionMode;
            ExpiresAt = args.ExpiresAt;
            Status = PaymentSessionStatus.Created;
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

        public PaymentInitiatorContext InitiatorContext { get; private set; } = default!;

        public decimal RequiredAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public PaymentAssuranceRequirement AssuranceRequirement { get; private set; }

        public PaymentInteractionMode InteractionMode { get; private set; }

        public PaymentSessionStatus Status { get; private set; }

        public decimal GuaranteedAmount { get; private set; }

        public decimal CapturedAmount { get; private set; }

        public decimal RefundedAmount { get; private set; }

        public DateTimeOffset? EarliestGuaranteeExpiry { get; private set; }

        public DateTimeOffset? ExpiresAt { get; private set; }

        public string? FailureCode { get; private set; }

        public long Version { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public IReadOnlyCollection<string> PaymentIntentIds => _paymentIntentIds.AsReadOnly();

        public bool IsTerminal => Status is PaymentSessionStatus.Cancelled or PaymentSessionStatus.Expired;

        public decimal OutstandingGuaranteeAmount => Math.Max(0, RequiredAmount - GuaranteedAmount);

        public static PaymentSession Create(string id, CreatePaymentSessionArgs args, IIdGenerator idGenerator, DateTimeOffset createdAt)
        {
            if (args.Amount <= 0)
                throw ExceptionFactory.PaymentAmountMustBePositive(args.Amount);

            if (args.ExpiresAt <= createdAt)
                throw ExceptionFactory.SessionExpiryMustBeInTheFuture(args.ExpiresAt, createdAt);

            var session = new PaymentSession(id, args, createdAt);
            session.Changed(idGenerator, createdAt);

            return session;
        }

        public bool HasTermsOf(CreatePaymentSessionArgs args)
            => PayableInstructionId == args.PayableInstructionId
               && OrderId == args.OrderId
               && OrderReference == args.OrderReference
               && CommercialVersion == args.CommercialVersion
               && Purpose == args.Purpose
               && PayerType == args.PayerType
               && PayerId == args.PayerId
               && InitiatorContext == args.InitiatorContext
               && RequiredAmount == args.Amount
               && CurrencyId == args.CurrencyId
               && AssuranceRequirement == args.AssuranceRequirement
               && InteractionMode == args.InteractionMode
               && ExpiresAt == args.ExpiresAt;

        public decimal FundableAmount(IReadOnlyCollection<PaymentIntent> intents)
            => Math.Max(0, RequiredAmount - intents.Where(intent => intent.HoldsFunding).Sum(intent => intent.RequestedAmount));

        public void EnsureFundable()
        {
            if (IsTerminal)
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "be funded");
        }

        public void Attach(PaymentIntent intent)
        {
            if (intent.PaymentSessionId != Id)
                throw ExceptionFactory.InvariantViolation(Id, $"intent '{intent.Id}' belongs to another session");

            _paymentIntentIds.Add(intent.Id);
        }

        public void ClearFundingFailure() => FailureCode = null;

        public void RecordFundingFailure(string failureCode, IReadOnlyCollection<PaymentIntent> intents, IIdGenerator idGenerator, DateTimeOffset now)
        {
            FailureCode = failureCode;
            Refresh(intents, idGenerator, now, force: true);
        }

        public void Refresh(IReadOnlyCollection<PaymentIntent> intents, IIdGenerator idGenerator, DateTimeOffset now, bool force = false)
        {
            var before = Snapshot();
            var coverage = SessionCoverage.Of(intents, RequiredAmount, AssuranceRequirement, now);

            CapturedAmount = intents.Sum(intent => intent.CapturedAmount);
            RefundedAmount = intents.Sum(intent => intent.RefundedAmount);

            if (IsTerminal)
            {
                GuaranteedAmount = 0;
                EarliestGuaranteeExpiry = null;
            }
            else
            {
                GuaranteedAmount = coverage.GuaranteedAmount;
                EarliestGuaranteeExpiry = coverage.EarliestGuaranteeExpiry;
                Status = StatusOf(intents, coverage);
                FailureCode = Status is PaymentSessionStatus.RequiresPaymentMethod or PaymentSessionStatus.PartiallyCovered
                    ? FailureCode ?? LatestFailureCode(intents)
                    : null;
            }

            if (force || before != Snapshot())
                Changed(idGenerator, now);
        }

        public void Cancel(IReadOnlyCollection<PaymentIntent> intents, IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (IsTerminal)
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "be cancelled");

            Status = PaymentSessionStatus.Cancelled;
            Refresh(intents, idGenerator, now, force: true);
        }

        public bool IsDueForExpiryAt(DateTimeOffset now)
            => !IsTerminal && Status != PaymentSessionStatus.Paid && ExpiresAt <= now;

        public void Expire(IReadOnlyCollection<PaymentIntent> intents, IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (!IsDueForExpiryAt(now))
                throw ExceptionFactory.PaymentSessionCannotTransition(Id, Status, "expire");

            Status = PaymentSessionStatus.Expired;
            Refresh(intents, idGenerator, now, force: true);
        }

        public void RecordPaidUnapplied(PaymentIntent intent, string reasonCode, IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (intent.CapturedAmount <= 0)
                throw ExceptionFactory.PaidUnappliedRequiresCapturedMoney(intent.Id);

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

        private PaymentSessionStatus StatusOf(IReadOnlyCollection<PaymentIntent> intents, SessionCoverage coverage)
        {
            if (coverage.AppliedCapturedAmount >= RequiredAmount)
                return PaymentSessionStatus.Paid;

            if (coverage.GuaranteedAmount >= RequiredAmount)
                return PaymentSessionStatus.Guaranteed;

            if (intents.Any(intent => intent.Status == PaymentIntentStatus.RequiresCustomerAction))
                return PaymentSessionStatus.RequiresCustomerAction;

            if (intents.Any(intent => intent.Status is PaymentIntentStatus.Processing or PaymentIntentStatus.Created))
                return PaymentSessionStatus.Processing;

            if (coverage.GuaranteedAmount > 0)
                return PaymentSessionStatus.PartiallyCovered;

            return intents.Count > 0 || FailureCode is not null
                ? PaymentSessionStatus.RequiresPaymentMethod
                : PaymentSessionStatus.Created;
        }

        private static string? LatestFailureCode(IReadOnlyCollection<PaymentIntent> intents)
            => intents
                .Where(intent => intent.Status == PaymentIntentStatus.Failed)
                .OrderByDescending(intent => intent.UpdatedAt)
                .ThenByDescending(intent => intent.Sequence)
                .Select(intent => intent.FailureCode)
                .FirstOrDefault();

        private (PaymentSessionStatus, decimal, decimal, decimal, DateTimeOffset?, string?, int) Snapshot()
            => (Status, GuaranteedAmount, CapturedAmount, RefundedAmount, EarliestGuaranteeExpiry, FailureCode, _paymentIntentIds.Count);

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
                RequiredAmount,
                GuaranteedAmount,
                CapturedAmount,
                RefundedAmount,
                CurrencyId,
                EarliestGuaranteeExpiry,
                ExpiresAt,
                Version,
                FailureCode,
                now));
        }

        private static string NewEventId(IIdGenerator idGenerator)
            => idGenerator.NewId().ToString(CultureInfo.InvariantCulture);
    }
}
