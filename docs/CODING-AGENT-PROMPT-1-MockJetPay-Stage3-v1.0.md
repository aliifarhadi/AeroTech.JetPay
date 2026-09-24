# CODING AGENT PROMPT 1 - MockJetPay for Stage 3

## Objective

Build a deterministic MockJetPay service that implements the FINAL JetPay service contract needed by Ordering Stage 3.

This is NOT the real JetPay implementation.
This mock exists so Ordering can implement against a stable production-equivalent contract without guessing unfinished JetPay behavior.

## Authorities

1. `AeroTech-JetPay-Payment-Orchestrator-Benchmark-and-Contract-v1.0-FINAL.md`
2. `AeroTech-Ordering-Stage3-Payment-Issue-Contract-v1.0-FINAL.md`
3. Current platform service conventions only for implementation mechanics

Do not invent payment-domain fields or statuses outside these documents.

## Required service contract

Implement:

```text
POST /service/v1/payment-method-options/resolve
POST /service/v1/payment-intents
GET  /service/v1/payment-intents/{paymentIntentId}
POST /service/v1/payment-intents/{paymentIntentId}/confirm
POST /service/v1/payment-intents/{paymentIntentId}/capture
POST /service/v1/payment-intents/{paymentIntentId}/cancel
```

All mutation operations must support idempotency.

## Required PaymentIntent shape

Implement the exact fields specified in the JetPay benchmark contract:

```text
Id
PayableInstructionId
OrderId
OrderReference
CommercialVersion
Purpose
PayerType
PayerId
RequestedAmount
CurrencyId
RequiredGuarantee
CaptureMode
Status
AuthorizedAmount
GuaranteedAmount
CapturedAmount
RefundedAmount
GuaranteeExpiresAt
IntentExpiresAt
SelectedTenderType
SelectedPaymentMethodOptionId
NextAction
FailureCode
FailureReason
Version
CreatedAt
UpdatedAt
```

## Required tender behaviors

### IranianPgw
- resolve as an eligible method for supported Stage-3 consumer scenarios;
- Confirm may return RequiresCustomerAction + Redirect;
- simulated verified completion changes the same intent to Captured;
- for PaidBeforeIssuance, GuaranteedAmount becomes equal to captured applicable amount only after verified completion.

### StoredValue
- enough balance scenario -> Captured synchronously;
- insufficient balance -> Failed;
- no bank redirect.

### Bnpl
- Confirm -> RequiresCustomerAction when scenario requires it;
- simulated provider approval -> Authorized;
- when the mock provider profile supports issuance guarantee, AuthorizedAmount and GuaranteedAmount become the requested amount;
- captured money is not required for AuthorizedBeforeIssuance.

### AgencyCredit
- no customer redirect;
- enough credit -> Authorized and GuaranteedAmount=requested amount;
- insufficient limit -> Failed;
- authorization is not represented as Captured.

## Mock-only scenario control

Provide a deterministic test-only mechanism for selecting/simulating these scenarios:

```text
IranianPgwCaptured
IranianPgwFailed
IranianPgwRequiresActionThenCaptured
StoredValueCaptured
StoredValueInsufficientBalance
BnplRequiresActionThenAuthorized
BnplRejected
AgencyCreditAuthorized
AgencyCreditInsufficientLimit
PaymentProcessing
PaymentExpired
LateCapturedForSupersededInstruction
```

Choose the implementation mechanism according to repository conventions, but it MUST be isolated from the production service contract and clearly mock-only.

## Idempotency behavior

- Create with same idempotency key + same payload returns the original intent.
- Same key + different payload is rejected.
- Confirm/Capture/Cancel have the same rule.
- Transport retry must never create a second successful financial effect.

## Read-back behavior

GET PaymentIntent is authoritative and returns the latest durable state.
A simulated transport timeout must not invent an Unknown business status.

## Events

Publish `PaymentIntentChangedV1` after every durable business-state change.
Use exact schema from the contract.

Guarantees:
- EventId unique.
- Version monotonic per intent.
- duplicate publication is allowed by transport but consumers must be able to deduplicate.

Also support `PaymentPaidUnappliedV1` in the late-capture-after-supersession scenario.

## Security/domain rules

- No PAN/CVV model.
- No real external payment provider calls.
- Provider names/routes are not exposed as Ordering domain decisions.
- No refund/chargeback implementation in this prompt.
- No split tender in Stage 3.

## Required tests

At minimum prove:

1. Create idempotency.
2. Create key reuse with changed payload is rejected.
3. GET returns authoritative state.
4. Iranian PGW RequiresAction -> Captured transition.
5. StoredValue immediate Capture.
6. StoredValue insufficient balance failure.
7. BNPL Authorized guarantee without capture.
8. AgencyCredit Authorized guarantee without capture.
9. authorization-only state never reports CapturedAmount.
10. expired guarantee no longer contributes usable GuaranteedAmount/current guarantee.
11. Cancel of uncaptured intent releases/cancels it.
12. Captured intent cannot be Cancelled as if money had not moved.
13. monotonic event Version.
14. duplicate event-safe behavior.
15. late-capture/superseded scenario emits PaidUnapplied evidence.

## Completion report

Return only:
- implemented contract operations;
- exact PaymentIntent fields;
- event schemas;
- deterministic scenario list;
- test result counts;
- any contract mismatch/blocker.

Do not redesign Ordering in this prompt.
Do not implement real JetPay.
