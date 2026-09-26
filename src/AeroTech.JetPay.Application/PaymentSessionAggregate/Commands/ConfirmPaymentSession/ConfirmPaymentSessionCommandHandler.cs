using System.Globalization;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentMethodOptions.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Views;
using AeroTech.JetPay.Application._Shared.Idempotency;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentSessionAggregate;
using AeroTech.JetPay.Domain.PaymentSessionAggregate.Contracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Domain._Shared.Resources;
using AeroTech.Messages.JetPay.Enums;
using MediatR;

namespace AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ConfirmPaymentSession
{
    public sealed class ConfirmPaymentSessionCommandHandler : IRequestHandler<ConfirmPaymentSessionCommand, PaymentSessionResponse>
    {
        private readonly IPaymentSessionRepository _sessions;
        private readonly IPaymentIntentRepository _intents;
        private readonly IPaymentMethodOptionResolver _options;
        private readonly IPaymentSessionFunding _funding;
        private readonly IIdempotencyGuard _idempotency;
        private readonly IPaymentSessionLock _locks;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ConfirmPaymentSessionCommandHandler(
            IPaymentSessionRepository sessions,
            IPaymentIntentRepository intents,
            IPaymentMethodOptionResolver options,
            IPaymentSessionFunding funding,
            IIdempotencyGuard idempotency,
            IPaymentSessionLock locks,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _sessions = sessions;
            _intents = intents;
            _options = options;
            _funding = funding;
            _idempotency = idempotency;
            _locks = locks;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<PaymentSessionResponse> Handle(ConfirmPaymentSessionCommand command, CancellationToken cancellationToken)
        {
            await using var sessionLock = await _locks.AcquireSessionAsync(command.PaymentSessionId, cancellationToken);

            var session = await _sessions.GetAsync(command.PaymentSessionId, cancellationToken)
                          ?? throw ExceptionFactory.PaymentSessionNotFound(command.PaymentSessionId);
            var intents = await _funding.LoadIntentsAsync(session, cancellationToken);
            var fingerprint = command.Fingerprint();

            var replay = await _idempotency.FindReplayAsync(
                IdempotentOperation.ConfirmPaymentSession,
                session.Id,
                command.IdempotencyKey,
                fingerprint,
                cancellationToken);

            if (replay is not null)
            {
                var planned = intents.Where(intent => replay.PaymentIntentIds.Contains(intent.Id)).ToList();

                if (planned.Any(intent => intent.IsDispatchPending) && !session.IsTerminal)
                    await _funding.DispatchAsync(session, intents, command.ReturnUrl, cancellationToken);

                return session.ToResponse(intents);
            }

            session.EnsureFundable();

            if (session.IsDueForExpiryAt(_clock.GetDateTime()))
                throw ExceptionFactory.PaymentSessionExpired(session.Id);

            var fundable = session.FundableAmount(intents);

            if (fundable <= 0)
                throw ExceptionFactory.NothingToFund(session.Id);

            var options = await _options.ResolveAsync(PaymentEligibilityContext.Of(session, fundable), cancellationToken);

            var plan = command.SelectionMode == PaymentSelectionMode.Default
                ? DefaultPlan(command, options, fundable, out var failureCode)
                : ExplicitPlan(command, session, options, fundable, out failureCode);

            if (failureCode is not null)
            {
                session.RecordFundingFailure(failureCode, intents, _idGenerator, _clock.GetDateTime());
                await _idempotency.RecordAsync(IdempotentOperation.ConfirmPaymentSession, session.Id, command.IdempotencyKey, fingerprint, session.Id, [], cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return session.ToResponse(intents);
            }

            var legs = await OpenLegsAsync(session, intents, plan, cancellationToken);

            await _idempotency.RecordAsync(
                IdempotentOperation.ConfirmPaymentSession,
                session.Id,
                command.IdempotencyKey,
                fingerprint,
                session.Id,
                legs.Select(leg => leg.Id),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _funding.DispatchAsync(session, intents, command.ReturnUrl, cancellationToken);

            return session.ToResponse(intents);
        }

        private async Task<List<PaymentIntent>> OpenLegsAsync(
            PaymentSession session,
            List<PaymentIntent> intents,
            IReadOnlyList<(PaymentMethodOption Option, decimal Amount)> plan,
            CancellationToken cancellationToken)
        {
            var now = _clock.GetDateTime();
            var sequence = intents.Count == 0 ? 0 : intents.Max(intent => intent.Sequence);
            var legs = new List<PaymentIntent>();

            session.ClearFundingFailure();

            foreach (var (option, amount) in plan)
            {
                var leg = PaymentIntent.Create(
                    _idGenerator.NewId().ToString(CultureInfo.InvariantCulture),
                    session.Id,
                    ++sequence,
                    option,
                    amount,
                    session.CurrencyId,
                    now);

                await _intents.AddAsync(leg, cancellationToken);
                session.Attach(leg);
                intents.Add(leg);
                legs.Add(leg);
            }

            session.Refresh(intents, _idGenerator, now);
            return legs;
        }

        private static IReadOnlyList<(PaymentMethodOption, decimal)> DefaultPlan(
            ConfirmPaymentSessionCommand command,
            IReadOnlyList<PaymentMethodOption> options,
            decimal fundable,
            out string? failureCode)
        {
            if (command.Selections.Count > 0)
                throw ExceptionFactory.SelectionsNotAllowedForDefault();

            var source = options.FirstOrDefault(option => option.IsDefault && option.CanAutoSelect && option.CustomerActionType == CustomerActionType.None);

            failureCode = source switch
            {
                null => FundingFailureCode.DefaultFundingSourceUnavailable,
                { AvailableAmount: { } available } when available < fundable => FundingFailureCode.InsufficientFunds,
                _ => null
            };

            return failureCode is null ? [(source!, fundable)] : [];
        }

        private static IReadOnlyList<(PaymentMethodOption, decimal)> ExplicitPlan(
            ConfirmPaymentSessionCommand command,
            PaymentSession session,
            IReadOnlyList<PaymentMethodOption> options,
            decimal fundable,
            out string? failureCode)
        {
            failureCode = null;

            if (command.Selections.Count == 0)
                throw ExceptionFactory.SelectionsRequired();

            var duplicate = command.Selections.GroupBy(selection => selection.PaymentMethodOptionId).FirstOrDefault(group => group.Count() > 1);

            if (duplicate is not null)
                throw ExceptionFactory.DuplicateSelection(duplicate.Key);

            var total = command.Selections.Sum(selection => selection.Amount);

            if (total != fundable)
                throw ExceptionFactory.SelectionSumMismatch(total.ToString(CultureInfo.InvariantCulture), fundable.ToString(CultureInfo.InvariantCulture));

            var plan = new List<(PaymentMethodOption, decimal)>();

            foreach (var selection in command.Selections)
            {
                var option = options.FirstOrDefault(candidate => candidate.Id == selection.PaymentMethodOptionId)
                             ?? throw ExceptionFactory.PaymentMethodOptionNotAvailable(selection.PaymentMethodOptionId, session.Id);

                if (selection.Amount > option.AvailableAmount)
                    throw ExceptionFactory.SelectionExceedsAvailableAmount(option.Id, selection.Amount, option.AvailableAmount);

                if (selection.Amount < option.MinimumAmount || selection.Amount > option.MaximumAmount)
                    throw ExceptionFactory.SelectionOutsideAmountLimits(option.Id, selection.Amount, option.MinimumAmount, option.MaximumAmount);

                if (selection.Amount < fundable && !option.SupportsPartialAmount)
                    throw ExceptionFactory.PartialAmountNotSupported(option.Id);

                plan.Add((option, selection.Amount));
            }

            return plan;
        }
    }
}
