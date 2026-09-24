# CODING AGENT PROMPT 2 - Ordering Stage 3 Checkout + Funding Guarantee + Confirm Reservation

## Prerequisite

Run only after Prompt 1 MockJetPay is available and its contract tests pass.

Stage 1 and Stage 2 are CLOSED. Do not redesign them.

## Authorities

1. `AeroTech-Ordering-Master-Domain-ADR-PRD-v1.0-FINAL.md`
2. `AeroTech-Ordering-Stage3-Payment-Issue-Contract-v1.0-FINAL.md` - this document is the Stage-3 override where payment details are more specific.
3. `AeroTech-JetPay-Payment-Orchestrator-Benchmark-and-Contract-v1.0-FINAL.md`
4. current Stage-1/Stage-2 code

Do not copy old local Payment aggregates from v1/v2.

## Stage-3 outcome

Implement a working end-to-end flow:

```text
Held Order
 -> create/read PaymentIntent in MockJetPay
 -> complete/observe payment guarantee
 -> persist OrderPaymentCoverage
 -> revalidate stale fare reservation evidence if needed
 -> Confirm HoldThenConfirm reservations
 -> expose a deterministic Stage-3 readiness result for Stage 4
```

Do NOT issue ETKT/EMD.

## OrderPaymentCoverage

Add the Order-owned evidence entity with exactly these fields:

```text
Id: long
OrderId: long
PaymentIntentId: string
PayableInstructionId: string
CommercialVersion: int
Purpose: PaymentCoveragePurpose
RequestedAmount: decimal
CurrencyId: int
RequiredGuarantee: RequiredGuarantee
CaptureMode: PaymentCaptureMode
Status: CheckoutPaymentStatus
AuthorizedAmount: decimal
GuaranteedAmount: decimal
CapturedAmount: decimal
AppliedAmount: decimal
GuaranteeExpiresAt: DateTimeOffset?
IntentExpiresAt: DateTimeOffset?
AppliesToCurrentOrder: bool
ProviderVersion: long
CreatedAt: DateTimeOffset
UpdatedAt: DateTimeOffset
```

Do not add provider/card/wallet-specific fields to OrderPaymentCoverage.

## Stage-3 enums

Use the exact semantic values in the Stage-3 spec.
Do not invent additional payment statuses.

## PayableInstruction

For Stage 3 create one immutable payable instruction for the exact current commercial version.

Identity must include:

```text
PayableInstructionId
OrderId
OrderReference
CommercialVersion
Purpose
Amount
CurrencyId
RequiredGuarantee
```

Changing commercial facts requires a new payable instruction/payment intent.
Never mutate amount/currency/version of an existing intent.

## Payment commands/behaviors in Ordering

Implement semantic application commands equivalent to:

```text
StartOrderPayment
GetOrderPayment
CancelOrderPayment
ProcessPaymentIntentChanged
ConfirmOrderReservations
```

Exact class/file names follow repository conventions; semantics are frozen here.

### StartOrderPayment

Preconditions:
- Order not terminal.
- current CommercialVersion selected.
- LastTicketingDate not passed.
- payable amount >= 0.

Behavior:
- zero payable -> no PaymentIntent; funding gate satisfied.
- otherwise resolve/select allowed payment method and create/get a JetPay intent idempotently.
- store/update OrderPaymentCoverage only from authoritative JetPay response/event.

### ProcessPaymentIntentChanged

- deduplicate EventId according to existing platform convention;
- ignore ProviderVersion <= currently stored ProviderVersion;
- reject contradictory amount/currency/commercial-version facts;
- preserve stale historical coverage with AppliesToCurrentOrder=false;
- never double count authorization plus capture.

### CancelOrderPayment

- ask JetPay to cancel an uncaptured/current intent;
- never locally pretend captured money was cancelled.

## Funding gate

Gate is satisfied only when:

```text
coverage.AppliesToCurrentOrder
coverage.CommercialVersion == order.CommercialVersion
coverage.RequestedAmount == required payable amount
coverage.CurrencyId == order.CurrencyId
coverage.GuaranteedAmount >= required payable amount
coverage.GuaranteeExpiresAt is null OR guarantee expiry > now
```

Ordering MUST NOT infer guarantee from TenderType.

Captured full coverage may set/derive Order Paid according to current Order status rules.
Authorization-only coverage MUST NOT be represented as Paid.

## Reservation revalidation before confirm

For each current Held HoldThenConfirm reservation:

- if latest ReservationValidationTimeLimit is still current, continue;
- if stale, call AirPrice reservation validation again for exact covered air-service scope;
- failure -> do not ConfirmHold;
- success -> update latest reservation validation limit;
- do not create a new FulfillmentReservation solely because the old validation evidence became stale.

## ConfirmInventory behavior

Materialize `OrderFulfillmentTaskType.ConfirmInventory` behavior using the existing FulfillmentTask/Attempt/ProviderInteraction recovery model.

For FlightFlow:
- request uses the existing reservation ProviderOperationRef/HoldId;
- durable request evidence exists before dispatch;
- success -> FulfillmentReservation and covered ReservationUnits become Confirmed;
- preserve provider refs.

Permanent provider rejection:
- do not mark reservation Released/Expired/Cancelled;
- reservation remains truthful Held if the hold still exists;
- confirmation task Failed;
- Order/Stage-3 readiness becomes ReservationUnconfirmed/not-ready;
- no automatic compensation.

Timeout/indeterminate outcome:
- confirmation task Unknown;
- do not mark reservation Confirmed;
- Order -> ReservationUnconfirmed;
- Issue blocked;
- NO blind automatic retry of FlightFlow ConfirmHold until its idempotency/read-back contract is formally certified.

## Multi-provider

Confirm providers independently.
Do not release/cancel already confirmed Provider A because Provider B fails or is Unknown.

## Stage-3 readiness

Expose/use a derived predicate equivalent to:

```text
FundingGateSatisfied
AND LastTicketingDate still open
AND every required reservation unit is Confirmed
AND no required confirmation is Unknown
```

Do not create a generic workflow aggregate for this.

## MockJetPay scenarios to cover E2E

Use the mock to prove:

1. IranianPgw redirect -> Captured -> ConfirmHold.
2. StoredValue Captured -> ConfirmHold.
3. BNPL Authorized guarantee -> ConfirmHold without marking Order Paid.
4. AgencyCredit Authorized guarantee -> ConfirmHold without marking Order Paid.
5. payment failure blocks confirm but does not release a still-valid hold.
6. payment timeout/read-back does not create a second intent.
7. expired guarantee blocks confirm.
8. stale AirPrice validation is refreshed.
9. stale validation refresh failure blocks confirm.
10. hold expiry blocks confirm despite payment.
11. LastTicketingDate blocks confirm/Issue.
12. FlightFlow ConfirmHold success confirms reservation.
13. FlightFlow confirm permanent rejection preserves Held truth and blocks Issue.
14. FlightFlow confirm timeout produces Unknown execution truth and blocks Issue without blind retry.
15. multi-provider mixed confirm has no automatic compensation.
16. late payment for superseded commercial version is not applied.
17. zero-value Order confirms without PaymentIntent.
18. duplicate/out-of-order JetPay event does not corrupt coverage.

## Non-goals

Do NOT implement:
- ETKT/EMD issuance;
- DocumentStock;
- refund;
- exchange;
- real JetPay adapters;
- bank gateway code;
- DigiPay code;
- StoredValue ledger;
- agency credit ledger;
- split tender;
- generic payment workflow engine.

## Completion report

Return:
1. domain types/fields materialized;
2. exact JetPay calls/events consumed;
3. funding-gate rules implemented;
4. reservation-confirmation behavior implemented;
5. E2E scenario results and test counts;
6. any provider-contract blocker.

Do not add fields/entities/statuses outside the authority documents.
