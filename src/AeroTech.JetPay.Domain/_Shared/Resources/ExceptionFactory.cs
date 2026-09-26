using AeroTech.Framework.Core.Domain.Exceptions;

namespace AeroTech.JetPay.Domain._Shared.Resources
{
    public static class ExceptionFactory
    {
        public static BusinessException PaymentSessionNotFound(params object?[] args) =>
            new(8000, ExceptionMessages.PaymentSessionNotFound, args) { HttpStatus = 404 };

        public static BusinessException PaymentSessionCannotTransition(params object?[] args) =>
            new(8001, ExceptionMessages.PaymentSessionCannotTransition, args) { HttpStatus = 409 };

        public static BusinessException PaymentOperationInProgress(params object?[] args) =>
            new(8002, ExceptionMessages.PaymentOperationInProgress, args) { HttpStatus = 409 };

        public static BusinessException PaymentSessionExpired(params object?[] args) =>
            new(8003, ExceptionMessages.PaymentSessionExpired, args) { HttpStatus = 409 };

        public static BusinessException PaymentIntentNotFound(params object?[] args) =>
            new(8004, ExceptionMessages.PaymentIntentNotFound, args) { HttpStatus = 404 };

        public static BusinessException NothingToFund(params object?[] args) =>
            new(8005, ExceptionMessages.NothingToFund, args) { HttpStatus = 409 };

        public static BusinessException PaymentIntentCannotTransition(params object?[] args) =>
            new(8006, ExceptionMessages.PaymentIntentCannotTransition, args) { HttpStatus = 409 };

        public static BusinessException PaymentAmountMustBePositive(params object?[] args) =>
            new(8008, ExceptionMessages.PaymentAmountMustBePositive, args) { HttpStatus = 422 };

        public static BusinessException SessionExpiryMustBeInTheFuture(params object?[] args) =>
            new(8009, ExceptionMessages.SessionExpiryMustBeInTheFuture, args) { HttpStatus = 422 };

        public static BusinessException CurrencyMismatch(params object?[] args) =>
            new(8010, ExceptionMessages.CurrencyMismatch, args) { HttpStatus = 422 };

        public static BusinessException IdempotencyKeyReusedWithDifferentPayload(params object?[] args) =>
            new(8020, ExceptionMessages.IdempotencyKeyReusedWithDifferentPayload, args) { HttpStatus = 409 };

        public static BusinessException PayableInstructionConflict(params object?[] args) =>
            new(8021, ExceptionMessages.PayableInstructionConflict, args) { HttpStatus = 409 };

        public static BusinessException PaymentMethodOptionNotAvailable(params object?[] args) =>
            new(8030, ExceptionMessages.PaymentMethodOptionNotAvailable, args) { HttpStatus = 422 };

        public static BusinessException SelectionsRequired(params object?[] args) =>
            new(8031, ExceptionMessages.SelectionsRequired, args) { HttpStatus = 422 };

        public static BusinessException SelectionsNotAllowedForDefault(params object?[] args) =>
            new(8032, ExceptionMessages.SelectionsNotAllowedForDefault, args) { HttpStatus = 422 };

        public static BusinessException DuplicateSelection(params object?[] args) =>
            new(8033, ExceptionMessages.DuplicateSelection, args) { HttpStatus = 422 };

        public static BusinessException SelectionSumMismatch(params object?[] args) =>
            new(8034, ExceptionMessages.SelectionSumMismatch, args) { HttpStatus = 422 };

        public static BusinessException SelectionExceedsAvailableAmount(params object?[] args) =>
            new(8035, ExceptionMessages.SelectionExceedsAvailableAmount, args) { HttpStatus = 422 };

        public static BusinessException SelectionOutsideAmountLimits(params object?[] args) =>
            new(8036, ExceptionMessages.SelectionOutsideAmountLimits, args) { HttpStatus = 422 };

        public static BusinessException PartialAmountNotSupported(params object?[] args) =>
            new(8037, ExceptionMessages.PartialAmountNotSupported, args) { HttpStatus = 422 };

        public static BusinessException TenderProviderNotRegistered(params object?[] args) =>
            new(8040, ExceptionMessages.TenderProviderNotRegistered, args) { HttpStatus = 500 };

        public static BusinessException ProviderOutcomeCannotBeRecorded(params object?[] args) =>
            new(8041, ExceptionMessages.ProviderOutcomeCannotBeRecorded, args) { HttpStatus = 409 };

        public static BusinessException ProviderOutcomeIsNotExpected(params object?[] args) =>
            new(8042, ExceptionMessages.ProviderOutcomeIsNotExpected, args) { HttpStatus = 502 };

        public static BusinessException ProviderAmountMismatch(params object?[] args) =>
            new(8043, ExceptionMessages.ProviderAmountMismatch, args) { HttpStatus = 502 };

        public static BusinessException ProviderDeclinedRelease(params object?[] args) =>
            new(8045, ExceptionMessages.ProviderDeclinedRelease, args) { HttpStatus = 502 };

        public static BusinessException PaidUnappliedRequiresCapturedMoney(params object?[] args) =>
            new(8090, ExceptionMessages.PaidUnappliedRequiresCapturedMoney, args) { HttpStatus = 500 };

        public static BusinessException InvariantViolation(params object?[] args) =>
            new(8099, ExceptionMessages.InvariantViolation, args) { HttpStatus = 500 };
    }
}
