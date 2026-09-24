using AeroTech.Framework.Core.Domain.Exceptions;

namespace AeroTech.JetPay.Domain._Shared.Resources
{
    public static class ExceptionFactory
    {
        // Payment intent lifecycle: 8000-8019
        public static BusinessException PaymentIntentNotFound(params object?[] args) =>
            new(8000, ExceptionMessages.PaymentIntentNotFound, args) { HttpStatus = 404 };

        public static BusinessException PaymentIntentCannotTransition(params object?[] args) =>
            new(8001, ExceptionMessages.PaymentIntentCannotTransition, args) { HttpStatus = 409 };

        public static BusinessException PaymentIntentOperationInProgress(params object?[] args) =>
            new(8002, ExceptionMessages.PaymentIntentOperationInProgress, args) { HttpStatus = 409 };

        public static BusinessException PaymentIntentExpired(params object?[] args) =>
            new(8003, ExceptionMessages.PaymentIntentExpired, args) { HttpStatus = 409 };

        public static BusinessException PaymentIntentOutcomePending(params object?[] args) =>
            new(8004, ExceptionMessages.PaymentIntentOutcomePending, args) { HttpStatus = 409 };

        public static BusinessException CapturedPaymentIntentCannotBeCancelled(params object?[] args) =>
            new(8005, ExceptionMessages.CapturedPaymentIntentCannotBeCancelled, args) { HttpStatus = 409 };

        public static BusinessException CaptureIsNotAllowedForCaptureMode(params object?[] args) =>
            new(8006, ExceptionMessages.CaptureIsNotAllowedForCaptureMode, args) { HttpStatus = 409 };

        public static BusinessException CaptureAmountExceedsCapturable(params object?[] args) =>
            new(8007, ExceptionMessages.CaptureAmountExceedsCapturable, args) { HttpStatus = 422 };

        public static BusinessException PaymentAmountMustBePositive(params object?[] args) =>
            new(8008, ExceptionMessages.PaymentAmountMustBePositive, args) { HttpStatus = 422 };

        public static BusinessException IntentExpiryMustBeInTheFuture(params object?[] args) =>
            new(8009, ExceptionMessages.IntentExpiryMustBeInTheFuture, args) { HttpStatus = 422 };

        // Idempotency and instruction identity: 8020-8029
        public static BusinessException IdempotencyKeyReusedWithDifferentPayload(params object?[] args) =>
            new(8020, ExceptionMessages.IdempotencyKeyReusedWithDifferentPayload, args) { HttpStatus = 409 };

        public static BusinessException PayableInstructionConflict(params object?[] args) =>
            new(8021, ExceptionMessages.PayableInstructionConflict, args) { HttpStatus = 409 };

        // Payment method selection: 8030-8039
        public static BusinessException PaymentMethodOptionNotAvailable(params object?[] args) =>
            new(8030, ExceptionMessages.PaymentMethodOptionNotAvailable, args) { HttpStatus = 422 };

        public static BusinessException PaymentMethodOptionIncompatible(params object?[] args) =>
            new(8031, ExceptionMessages.PaymentMethodOptionIncompatible, args) { HttpStatus = 422 };

        // Tender providers: 8040-8059
        public static BusinessException TenderProviderNotRegistered(params object?[] args) =>
            new(8040, ExceptionMessages.TenderProviderNotRegistered, args) { HttpStatus = 500 };

        public static BusinessException ProviderOutcomeCannotBeRecorded(params object?[] args) =>
            new(8041, ExceptionMessages.ProviderOutcomeCannotBeRecorded, args) { HttpStatus = 409 };

        public static BusinessException ProviderOutcomeIsNotExpected(params object?[] args) =>
            new(8042, ExceptionMessages.ProviderOutcomeIsNotExpected, args) { HttpStatus = 502 };

        public static BusinessException ProviderAmountMismatch(params object?[] args) =>
            new(8043, ExceptionMessages.ProviderAmountMismatch, args) { HttpStatus = 502 };

        public static BusinessException ProviderDeclinedCapture(params object?[] args) =>
            new(8044, ExceptionMessages.ProviderDeclinedCapture, args) { HttpStatus = 502 };

        public static BusinessException ProviderDeclinedRelease(params object?[] args) =>
            new(8045, ExceptionMessages.ProviderDeclinedRelease, args) { HttpStatus = 502 };

        // Invariants: 8090-8099
        public static BusinessException PaidUnappliedRequiresCapturedMoney(params object?[] args) =>
            new(8090, ExceptionMessages.PaidUnappliedRequiresCapturedMoney, args) { HttpStatus = 500 };

        public static BusinessException InvariantViolation(params object?[] args) =>
            new(8099, ExceptionMessages.InvariantViolation, args) { HttpStatus = 500 };
    }
}
