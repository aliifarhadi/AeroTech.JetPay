# AeroTech Ordering - Stage 3 Checkout, Funding Guarantee and Reservation Confirmation v1.0 FINAL

Status: FINAL STAGE-3 DOMAIN/BEHAVIOR AUTHORITY
Depends on: Stage 1 CLOSED, Stage 2 CLOSED, JetPay canonical payment-orchestrator contract v1.0

## 1. Stage 3 business capability

Stage 3 turns a commercially accepted and held Order into an Order that has:
1. valid payment/funding guarantee for the exact CommercialVersion;
2. confirmed reservation resources for every HoldThenConfirm provider;
3. sufficient evidence to allow Stage 4 Issue.

Stage 3 does NOT issue ETKT or EMD.

## 2. End-to-end flow

```text
Order with current held reservations
        -> derive immutable payable instruction
        -> Create/Get PaymentIntent
        -> Confirm PaymentIntent with selected method
        -> customer action / processing if required
        -> JetPay guarantee event/read-back
        -> persist OrderPaymentCoverage
        -> verify guarantee is current and applicable
        -> verify LastTicketingDate still open
        -> if reservation validation evidence stale, revalidate with AirPrice
        -> Confirm HoldThenConfirm reservations independently
        -> all required reservations confirmed
        -> Stage 4 Issue eligible
```

## 3. Payable instruction identity

Ordering creates an immutable identity for the exact payable commercial state:

```text
PayableInstructionId: string
OrderId: long
OrderReference: string
CommercialVersion: int
Purpose: PaymentCoveragePurpose
Amount: decimal
CurrencyId: int
RequiredGuarantee: RequiredGuarantee
```

The PaymentIntent amount/currency/version never mutate.
If commercial facts change, a new payable instruction and new PaymentIntent are required.

## 4. OrderPaymentCoverage - Order child

```text
OrderPaymentCoverage
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

Stage 3 initially allows one active PaymentIntent per PayableInstruction.
Final domain may support multiple intents/split tender later.

## 5. PaymentCoveragePurpose

```text
InitialSale
AddService
ExchangeAdditionalCollection
GroupDeposit
FinalPayment
Other
```

Stage 3 uses InitialSale only unless a currently implemented flow proves another value.

## 6. CheckoutPaymentStatus

```text
Created
RequiresCustomerAction
Processing
Authorized
PartiallyCaptured
Captured
Failed
Cancelled
Expired
```

Timeout is not a status.

## 7. Coverage update rules

On PaymentIntentChangedV1:

1. match exact OrderId + PaymentIntentId + PayableInstructionId;
2. ignore duplicate EventId;
3. ignore ProviderVersion <= current ProviderVersion;
4. never change RequestedAmount/CurrencyId/CommercialVersion from an event that contradicts the original instruction;
5. update payment evidence only from the authoritative event/read-back;
6. if event belongs to an old CommercialVersion, retain historical evidence but set AppliesToCurrentOrder=false;
7. never double-count Authorization then Capture as two payments.

## 8. Issue funding gate

A coverage record satisfies the funding gate only when all are true:

```text
AppliesToCurrentOrder == true
CommercialVersion == Order.CommercialVersion
RequestedAmount == required payable amount
CurrencyId == Order.CurrencyId
GuaranteedAmount >= required payable amount
Guarantee has not expired
```

If RequiredGuarantee == PaidBeforeIssuance, JetPay itself guarantees that GuaranteedAmount is backed by captured/paid money.
Ordering MUST NOT re-derive this from TenderType.

`CapturedAmount == RequestedAmount` may make Order eligible for the existing Paid commercial summary/status, but authorization-only guarantee does not mean cash has been received and MUST NOT be represented as Paid.

## 9. Zero-value Order

If required payable amount is zero:
- no PaymentIntent is required;
- payment gate is satisfied;
- reservation confirmation can continue.

## 10. Payment failure

Failed/Cancelled/Expired payment:
- does not automatically release valid reservations before LastTicketingDate;
- does not create a second payment intent automatically without an explicit retry/new checkout action;
- does not mark provider reservation Rejected;
- blocks Issue.

## 11. Late payment after commercial supersession

If money is captured for a superseded payable instruction:
- preserve payment history;
- AppliesToCurrentOrder=false;
- do not use it for Issue;
- consume PaymentPaidUnappliedV1 when provided;
- later servicing/refund flow resolves the money.

## 12. Revalidation before reservation confirmation

For every Held reservation that needs HoldThenConfirm:

```text
if ReservationValidationTimeLimit is current:
    use current validation evidence
else:
    call AirPrice reservation validation again for the exact covered scope
    on success, store the new latest ReservationValidationTimeLimit
    on failure, do not ConfirmHold
```

Fresh validation does not create a new FulfillmentReservation by itself.

Hard rule:
- LastTicketingDate remains the hard commercial deadline.
- stale validation is refreshable.
- expired provider hold is not confirmable.

## 13. Reservation confirmation behavior

Add the domain/application behavior represented semantically by:

```text
ConfirmOrderReservations(OrderId)
ConfirmReservation(FulfillmentReservationId)
```

Selection:
- ReservationMode.HoldThenConfirm
- current Status == Held
- required by an active OrderService

Preconditions:
- funding gate satisfied;
- LastTicketingDate not passed;
- provider hold not expired;
- reservation validation current or freshly revalidated.

Success:
- FulfillmentReservation -> Confirmed
- covered ReservationUnit values -> Confirmed
- preserve ProviderOperationRef and ProviderUnitRef
- record ConfirmInventory FulfillmentTask and ProviderInteraction evidence

Definitive provider rejection of ConfirmHold:
- do NOT fake Released/Expired/Cancelled;
- reservation remains Held if provider hold still exists;
- confirmation task is Failed;
- Order becomes/returns ReservationUnconfirmed for issuance readiness;
- Issue blocked.

Unknown/timeout:
- task -> Unknown;
- reservation must not be falsely changed to Confirmed or Released;
- Order -> ReservationUnconfirmed;
- Issue blocked;
- no blind automatic retry for FlightFlow until ConfirmHold idempotency/read-back is contractually certified.

## 14. Multi-provider confirmation

Each provider reservation confirms independently.

Provider A success + Provider B failure/unknown:
- keep A confirmed;
- do not automatically cancel/release A;
- Order is not Issue-ready until every required reservation is confirmed or otherwise does not require confirmation.

## 15. Stage-3 readiness predicate

Stage 3 is complete for an Order only when:

```text
FundingGateSatisfied
AND LastTicketingDateOpen
AND every required reservation unit is Confirmed
AND no required reservation/confirmation is Unknown
```

Do not create a new aggregate or generic workflow state for this predicate.

## 16. JetPay S2S calls used by Ordering

Required now:

```text
POST /service/v1/payment-method-options/resolve
POST /service/v1/payment-intents
GET  /service/v1/payment-intents/{id}
POST /service/v1/payment-intents/{id}/confirm
POST /service/v1/payment-intents/{id}/cancel
```

Supported by contract now for Stage 4/future use:

```text
POST /service/v1/payment-intents/{id}/capture
```

Ordering does not call bank/PSP/BNPL endpoints directly.

## 17. Event used by Ordering

```text
PaymentIntentChangedV1
```

and special stale-money event:

```text
PaymentPaidUnappliedV1
```

Stage 3 must not depend on redirect callbacks reaching Ordering.
Provider callback terminates at JetPay/MockJetPay.

## 18. Mock-specific deterministic scenarios

MockJetPay must support deterministic test scenarios for:

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

The scenario-control mechanism is mock-only and must not leak into the production JetPay contract.

## 19. Required Stage-3 acceptance scenarios

1. zero-value Order needs no PaymentIntent;
2. IranianPgw redirect then verified capture -> guarantee -> ConfirmHold;
3. StoredValue immediate capture -> ConfirmHold;
4. BNPL authorization with accepted guarantee -> ConfirmHold;
5. AgencyCredit authorization with accepted guarantee -> ConfirmHold;
6. authorization-only intent does not mark Order Paid;
7. expired authorization guarantee blocks ConfirmHold;
8. payment transport timeout -> Get/read-back; no duplicate intent;
9. duplicate payment event is idempotent;
10. out-of-order payment event is ignored by ProviderVersion;
11. wrong currency/version/amount event is rejected from coverage;
12. payment failure leaves valid hold intact and blocks Issue;
13. stale AirPrice validation is refreshed before ConfirmHold;
14. failed refresh blocks ConfirmHold;
15. expired provider hold blocks ConfirmHold even with payment;
16. LastTicketingDate blocks payment-driven confirmation/Issue;
17. ConfirmHold success -> reservation Confirmed;
18. ConfirmHold permanent reject -> reservation remains truthful Held, Issue blocked;
19. ConfirmHold timeout -> Unknown task, ReservationUnconfirmed, no blind retry;
20. multi-provider mixed result has no automatic compensation;
21. late captured payment for superseded commercial version is PaidUnapplied/not applicable;
22. captured full coverage may set Paid summary; authorized-only coverage may not.

## 20. Explicit non-goals

Do not implement in Stage 3:
- ETKT/EMD issuance;
- DocumentStock;
- refunds;
- chargebacks;
- split tender;
- real bank adapters;
- real DigiPay adapter;
- real StoredValue ledger;
- real agency-credit ledger;
- payment smart-routing engine;
- DCS, disruption, revenue accounting.

