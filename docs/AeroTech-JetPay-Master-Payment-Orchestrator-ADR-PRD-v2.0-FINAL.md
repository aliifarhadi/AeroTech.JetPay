# AeroTech JetPay — Master Payment Orchestrator ADR/PRD v2.0 FINAL

**Status:** FINAL DOMAIN AUTHORITY FOR JETPAY REDESIGN BEFORE ORDERING STAGE 3

**Purpose:** Define a clean, benchmarked airline payment-orchestration domain that supports direct web/mobile sales, agency API issuance, airline back-office/call-center sales, multi-tender payments, stored value, credit, BNPL, cash, local Iranian PGWs, future card/A2A/BSP instruments, and later refund/reconciliation flows without redesigning Ordering.

---

## 1. Product decision

JetPay is not merely a bank-gateway adapter. It is the airline's omnichannel payment-orchestration capability.

Benchmark basis:
- Stripe: PaymentIntent lifecycle, payment-method selection, confirmation, customer action, read-back, idempotency.
- Adyen: payment-method resolution, partial payments with multiple payment methods, order-level remaining amount, stored-value balance and partial authorization.
- PayPal: AUTHORIZE vs CAPTURE and idempotent order/payment operations.
- IATA Payment Services / IFG: airline omnichannel orchestration, hundreds of forms of payment, smart routing, direct + agency + corporate + counter channels, authorization/capture, local payment methods, BNPL, BSP and EasyPay.
- IATA Airline Payment Framework: cash, card, wallets, EFT/A2A, online credit, offline payment, installments, points/miles.
- IATA Settlement with Orders / BSP / EasyPay: agency commitment-to-pay, agent wallet funding and ticket issuance.

No proprietary Amadeus/Sabre internal payment aggregate is inferred.

---

## 2. Why v1 JetPay is not final

KEEP from current JetPay:
- idempotent mutations;
- server-side read-back;
- no `Unknown` business payment status;
- callback is not payment truth; verify/inquiry is required;
- provider routing hidden behind tender adapters;
- outbox/integration-event model;
- `PaymentMethodOption` concept;
- deterministic mock clock/fault injection;
- Iranian PGW, StoredValue, BNPL, AgencyCredit mock tender behavior.

REDESIGN before Ordering Stage 3:
- current `PaymentIntent` represents exactly one full-amount tender and therefore cannot support airline multi-tender checkout;
- `RequiredGuarantee` and `CaptureMode` are incorrectly frozen before the payment method is selected;
- one failed tender currently fails/releases the entire payable intent instead of allowing a new tender for the outstanding amount;
- `PaymentMethodOption` is too thin: it does not expose currency-specific available balance/limit, partial-payment ability, default source, or auto-selection eligibility;
- current mock has no default wallet by currency;
- current mock has no explicit/unattended payment-selection policy;
- current mock has no back-office split payment;
- current payer model conflates `Organization` and does not distinguish Corporate/Partner;
- current design has no staff cash payment;
- current design cannot faithfully model `StoredValue + Cash + Credit` or `StoredValue + PGW` for one payable instruction.

REMOVE as canonical assumptions:
- one `PaymentIntent` == one whole payable instruction;
- caller must choose `CaptureMode`;
- caller must choose a tender-specific `RequiredGuarantee` pair before creating the payment;
- one tender failure means the whole payment obligation is failed.

---

# 3. Canonical domain model

## 3.1 Aggregate roots

JetPay final domain has two core roots:

1. `PaymentSession`
   - one logical checkout/payment obligation for one `PayableInstructionId`;
   - owns overall amount coverage across one or many payment intents;
   - owns payer, channel context, expiry, overall coverage/readiness.

2. `PaymentIntent`
   - one attempt to fund a portion of a PaymentSession through one selected payment-method option;
   - owns tender-specific authorization/capture/customer-action lifecycle;
   - maps closely to Stripe PaymentIntent semantics.

A `PaymentSession` is analogous to the order/session container used by modern checkout systems for partial/multi-method payment. `PaymentIntent` remains the single-tender financial attempt.

This separation is required by the airline scenarios below and by Adyen-style partial/multiple-payment behavior.

---

## 3.2 PaymentSession — field-by-field

| Field | Type | Null | Meaning |
|---|---|---:|---|
| `Id` | `string` | No | JetPay session identity. |
| `PayableInstructionId` | `string` | No | Stable Ordering-owned financial obligation identity. |
| `OrderId` | `long` | No | Ordering identity. |
| `OrderReference` | `string` | No | Commercial order reference. |
| `CommercialVersion` | `int` | No | Exact commercial version being funded. |
| `Purpose` | `PaymentPurpose` | No | InitialSale/AddService/ExchangeAdditionalCollection/GroupDeposit/FinalPayment/Other. |
| `PayerType` | `PayerType` | No | Customer/Agency/Corporate/Partner. |
| `PayerId` | `long` | No | Financial payer identity, not initiating actor. |
| `InitiatorContext` | `PaymentInitiatorContext` | No | Who/channel initiated payment. |
| `RequiredAmount` | `decimal` | No | Total amount to fund. |
| `CurrencyId` | `int` | No | Single session currency. |
| `AssuranceRequirement` | `PaymentAssuranceRequirement` | No | Required financial assurance for the business step. |
| `InteractionMode` | `PaymentInteractionMode` | No | CustomerInteractive/UnattendedApi/StaffAssisted. |
| `Status` | `PaymentSessionStatus` | No | Current overall payment state. |
| `GuaranteedAmount` | `decimal` | No | Sum of currently valid issuance guarantees across intents, capped by RequiredAmount. |
| `CapturedAmount` | `decimal` | No | Sum of provider-verified received/debited/captured funds. |
| `RefundedAmount` | `decimal` | No | Later aggregate refund execution amount. |
| `EarliestGuaranteeExpiry` | `DateTimeOffset?` | Yes | Earliest expiry among guarantee amounts still needed for full coverage. |
| `ExpiresAt` | `DateTimeOffset?` | Yes | Checkout/session deadline supplied by caller/business flow. |
| `Version` | `long` | No | Monotonic snapshot version. |
| `CreatedAt` | `DateTimeOffset` | No | Created time. |
| `UpdatedAt` | `DateTimeOffset` | No | Latest authoritative change. |
| `PaymentIntentIds` | collection<string> | No | Financial attempts belonging to the session. |

Derived values:

```text
OutstandingGuaranteeAmount = max(0, RequiredAmount - GuaranteedAmount)
IsGuaranteedForBusinessStep = GuaranteedAmount >= RequiredAmount
IsFullyPaid = CapturedAmount >= RequiredAmount
```

`CapturedAmount` and `GuaranteedAmount` are different facts.

---

## 3.3 PaymentIntent — field-by-field

One PaymentIntent funds one part of a PaymentSession with one selected `PaymentMethodOption`.

| Field | Type | Null | Meaning |
|---|---|---:|---|
| `Id` | `string` | No | JetPay payment-intent identity. |
| `PaymentSessionId` | `string` | No | Parent session. |
| `Sequence` | `int` | No | Stable order within session. |
| `PaymentMethodOptionId` | `string` | No | Opaque resolved option snapshot used for this intent. |
| `TenderType` | `TenderType` | No | Normalized form of payment. |
| `RequestedAmount` | `decimal` | No | Amount assigned to this intent. |
| `CurrencyId` | `int` | No | Must equal session currency. |
| `CaptureMode` | `PaymentCaptureMode` | No | JetPay-selected/option-owned execution mode, not Ordering-selected. |
| `Status` | `PaymentIntentStatus` | No | Single-tender lifecycle. |
| `AuthorizedAmount` | `decimal` | No | Provider authorized amount. |
| `GuaranteedAmount` | `decimal` | No | Amount JetPay certifies as usable for business assurance. |
| `CapturedAmount` | `decimal` | No | Provider-verified received/debited/captured amount. |
| `RefundedAmount` | `decimal` | No | Later refund amount on this intent. |
| `GuaranteeExpiresAt` | `DateTimeOffset?` | Yes | Expiry of the currently exposed guarantee. |
| `NextAction` | `CustomerAction?` | Yes | Redirect/form/SDK/3DS action. |
| `FailureCode` | `string?` | Yes | Normalized reason code. |
| `FailureReason` | `string?` | Yes | Safe reason text. |
| `ProviderReference` | `string?` | Yes | Internal JetPay evidence; need not be exposed to Ordering. |
| `Version` | `long` | No | Monotonic intent version. |
| `CreatedAt` | `DateTimeOffset` | No | Created. |
| `UpdatedAt` | `DateTimeOffset` | No | Latest authoritative update. |

### PaymentIntentStatus

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

No `Unknown` financial business state.

Provider-operation uncertainty belongs to JetPay reconciliation/attempt evidence while the PaymentIntent remains at its last authoritative state.

---

## 3.4 PaymentMethodOption — field-by-field

This is the key object missing from v1.

A PaymentMethodOption is not a PSP name. It is one **eligible funding choice** for the exact payer, amount, currency, channel and business context.

| Field | Type | Null | Meaning |
|---|---|---:|---|
| `Id` | `string` | No | Opaque option identity. Caller must never construct it. |
| `TenderType` | `TenderType` | No | Normalized form of payment. |
| `DisplayCode` | `string` | No | UI/localization code. |
| `CurrencyId` | `int` | No | Currency this option can fund. |
| `AvailableAmount` | `decimal?` | Yes | Current usable wallet balance/credit exposure when meaningful. |
| `MinimumAmount` | `decimal?` | Yes | Current minimum if provider/policy has one. |
| `MaximumAmount` | `decimal?` | Yes | Current maximum if provider/policy has one. |
| `SupportsPartialAmount` | `bool` | No | Can be one leg of a split payment. |
| `IsDefault` | `bool` | No | Default payer funding source for this currency/channel. |
| `CanAutoSelect` | `bool` | No | May JetPay select it in Default mode without user interaction? |
| `CustomerActionType` | `CustomerActionType` | No | None/Redirect/HtmlForm/Sdk/ThreeDsChallenge. |
| `AssuranceCapability` | `PaymentAssuranceCapability` | No | FundsReceived or CommitmentToPay. |
| `CaptureMode` | `PaymentCaptureMode` | No | Execution behavior for this option. |
| `ExpiresAt` | `DateTimeOffset?` | Yes | Optional eligibility snapshot expiry. |

Provider profile, bank code, merchant id, DigiPay route, wallet account id and other routing details remain opaque to Ordering/UI.

The OptionId may internally bind to a specific wallet/credit facility/funding instrument.

---

## 3.5 PaymentAssuranceRequirement

Replace tender-specific `RequiredGuarantee` input semantics with business assurance semantics:

```text
FundsReceived
IssuanceGuaranteed
```

Meaning:

### FundsReceived
Only verified captured/debited/received funds count.

### IssuanceGuaranteed
Either received funds OR a provider/credit commitment contractually accepted by the airline may count.

Examples:
- PGW captured money satisfies both.
- StoredValue debit satisfies both.
- BNPL provider commitment may satisfy IssuanceGuaranteed, but not FundsReceived.
- AgencyCredit authorization may satisfy IssuanceGuaranteed, but not FundsReceived.

Ordering requests the business assurance. JetPay decides whether a selected payment method's provider state qualifies.

Ordering MUST NOT choose `CaptureMode`.

---

## 3.6 PaymentInteractionMode

```text
CustomerInteractive
UnattendedApi
StaffAssisted
```

This is separate from payer type.

Examples:
- B2C website: CustomerInteractive.
- Agency API issue: UnattendedApi.
- Airline back office/call center/airport office: StaffAssisted.

This field is critical to method eligibility.

---

## 3.7 Payer vs Initiator

Never conflate who owes the money with who clicks the button.

### PayerType

```text
Customer
Agency
Corporate
Partner
```

### PaymentInitiatorContext

| Field | Type | Null |
|---|---|---:|
| `SalesChannel` | string/enum | No |
| `ActorType` | string/enum | No |
| `ActorId` | long | No |
| `OfficeId` | long? | Yes |

Examples:
- Back-office employee issuing for a B2C customer: payer=Customer; initiator=AirlineEmployee/BackOffice.
- Agency API: payer=Agency; initiator=AgencyApiPrincipal.

---

# 4. Tender taxonomy

Canonical tender families must be extensible and airline-relevant:

```text
IranianPgw
ExternalCard
AccountToAccount
Bnpl
StoredValue
AgencyDeposit
CustomerCredit
AgencyCredit
CorporateCredit
Cash
BankTransferReference
Voucher
LoyaltyPoints
```

Stage-3/mock materialization must cover at least:
- IranianPgw
- StoredValue
- Bnpl
- AgencyCredit
- CustomerCredit or CorporateCredit representative credit path
- Cash

Provider values such as Mellat/SEP/Pasargad/IranKish/DigiPay are provider profiles, never TenderType.

---

# 5. ProviderProfile — internal JetPay capability model

Needed so every Iranian bank/PSP can be added without changing Ordering or canonical JetPay contracts.

| Field | Meaning |
|---|---|
| `Id` | internal profile identity |
| `ProviderCode` | Mellat/SEP/Pasargad/etc internal code |
| `TenderType` | normalized tender family |
| `Status` | Active/Suspended/Retired |
| `SupportedCurrencyIds` | supported currencies |
| `AmountUnit` | MajorCurrency/IRR/Toman/ProviderDefined |
| `SupportsCreate` | create/token/session capability |
| `SupportsCustomerAction` | redirect/form/etc |
| `SupportsVerify` | server verify |
| `SupportsInquiry` | read-back |
| `RequiresSettlementAfterVerify` | provider-specific settle step |
| `SupportsAuthorization` | authorize |
| `SupportsCapture` | capture |
| `SupportsReleaseOrReversal` | release/reversal |
| `SupportsRefund` | refund |
| `SupportsPartialRefund` | partial refund |
| `SupportsIdempotency` | provider-native safe retry |
| `SupportsReadBack` | provider-native query/reconcile |

Routing policy is JetPay-owned.

Safety rule:

```text
Before any external effect -> another route may be selected.
After an Unknown external effect -> DO NOT route a second charge until reconciliation proves the first did not succeed.
```

---

# 6. Eligibility rules

`ResolvePaymentMethodOptions` MUST resolve from all of:
- payer type/id;
- amount;
- currency;
- payment purpose;
- interaction mode;
- sales/initiator context;
- configured airline acceptance policy;
- funding-source availability (wallet balance, credit limit, deposit balance);
- provider/currency availability;
- customer-action compatibility;
- partial-payment capability.

It MUST NOT simply return a static list by PayerType.

---

# 7. Selection modes

## Explicit
Caller supplies one or more option/amount selections.

Used by:
- B2C checkout;
- staff/back-office assisted payment;
- agency API when caller explicitly selects another permitted wallet/source.

## Default
Caller supplies no payment option. JetPay selects exactly the configured default eligible non-interactive funding source for payer+currency+channel.

Used by agency API when payment is omitted.

Rules:
- no customer-action tender may be silently selected;
- default must be eligible for current currency and purpose;
- default must cover requested amount unless policy explicitly supports an automatic multi-source plan;
- current Stage 3 policy: no automatic fallback from default agency wallet to PGW;
- insufficient/default-unavailable result is explicit and deterministic.

---

# 8. Multi-tender payment

Multi-tender is NOW a Stage-3 requirement because back-office issuance requires it.

Benchmark: Adyen partial payments explicitly combine different payment methods under one payment order, including stored value plus another method.

Rules:

```text
sum(selected intent RequestedAmount) == PaymentSession.RequiredAmount
```

or for a session that already has valid partial coverage:

```text
sum(new selections) == outstanding amount being funded
```

Each selection:
- same currency as session;
- amount > 0;
- amount <= available/max amount;
- if amount is partial, option must `SupportsPartialAmount`;
- one leg failing does not erase successful legs;
- session is issue-ready only when aggregate valid guarantee >= RequiredAmount.

Example:

```text
StoredValue 300
AgencyCredit 400
Cash 300
----------------
Order total 1000
```

This is one PaymentSession and three PaymentIntents.

---

# 9. Channel-specific E2E flows

## 9.1 B2C airline website

```text
Create Order / reserve
  -> ResolvePaymentMethodOptions(CustomerInteractive)
  -> show eligible methods
  -> user selects method(s)
  -> Create PaymentSession
  -> Confirm with Explicit selections
  -> if redirect/action: return NextAction
  -> provider callback to JetPay
  -> server verify/inquiry
  -> PaymentSession Guaranteed/Paid
  -> event/read-back to Ordering
  -> Ordering confirms inventory
  -> Stage 4 Issue
```

Eligible examples:
- IranianPgw
- StoredValue wallet in order currency
- BNPL when provider/policy says eligible
- later A2A/card/voucher/points.

## 9.2 Agency API — one external Issue call, payment omitted

External agency calls Ordering Issue once.

Ordering/JetPay internally:

```text
Ordering requests Default payment
  -> JetPay resolves agency payer + order currency
  -> selects default non-interactive StoredValue wallet
  -> verifies available balance
  -> debits/captures requested amount
  -> PaymentSession becomes Paid/Guaranteed
  -> Ordering continues Confirm/Issue
```

Rules:
- no redirect;
- no PGW silent fallback;
- if default wallet is absent/insufficient -> deterministic payment failure returned to Ordering;
- agency may explicitly specify another eligible agency wallet if API contract allows it.

## 9.3 Airline staff back-office

```text
ResolvePaymentMethodOptions(StaffAssisted, actual payer)
  -> display wallet / credit / cash / permitted other sources
  -> employee enters composition
  -> Confirm payment with N selections
  -> JetPay executes each leg
  -> partial successes remain durable
  -> session guaranteed only when total coverage is sufficient
```

Examples:
- Customer StoredValue + Cash
- Agency StoredValue + AgencyCredit
- CorporateCredit + Cash
- StoredValue + Credit + Cash.

The employee initiator never changes the payer identity.

---

# 10. Cash

IATA Airline Payment Framework recognizes physical cash at airline sales offices.

Canonical JetPay behavior:
- eligible only in StaffAssisted context and permitted office/policy;
- no PSP redirect;
- confirmation means authorized employee recorded receipt of cash;
- intent becomes Captured/Guaranteed immediately;
- audit retains initiator/office context;
- later cash-reconciliation/accounting remains outside Ordering.

---

# 11. StoredValue

Canonical behavior:
- options are currency-specific wallet funding choices;
- option exposes current `AvailableAmount`;
- one wallet per currency may be marked `IsDefault`;
- supports partial payment;
- full/partial debit is provider-verified financial effect;
- insufficient balance never becomes Unknown;
- timeout after debit requires read-back/reconciliation before retry.

Agency default-wallet behavior is implemented through this same mechanism unless a separate `AgencyDeposit` product is explicitly introduced later.

---

# 12. Credit / BNPL

## BNPL
- can require customer action;
- provider approval may create `IssuanceGuaranteed` coverage without cash capture;
- guarantee expiry is authoritative provider/policy evidence;
- capture/settlement later is not required for Ordering Issue if guarantee remains valid.

## AgencyCredit / CorporateCredit / CustomerCredit
- non-interactive when policy allows;
- checks active facility + available exposure;
- reserves/utilizes exposure;
- creates commitment-to-pay guarantee;
- does not pretend money was captured;
- release commitment if abandoned before financial commitment becomes final.

IATA Settlement with Orders provides the industry benchmark for seller-airline commitment-to-pay.

---

# 13. Iranian PGW normalization

Canonical flow:

```text
Create provider session/token
 -> Redirect / Form
 -> Callback
 -> server-side Verify / Inquiry
 -> Settle when required by provider
 -> Captured
```

Rules:
- callback alone is never paid truth;
- Rial/Toman conversion only in provider adapter;
- provider token/reference is internal JetPay evidence;
- missing callback can be reconciled through inquiry when supported;
- unknown effect cannot be rerouted until reconciled.

---

# 14. APIs — canonical service contract

## 14.1 Resolve payment options

```text
POST /service/v2/payment-method-options/resolve
```

Request:

```text
OrderId
OrderReference
CommercialVersion
Purpose
PayerType
PayerId
InitiatorContext
Amount
CurrencyId
AssuranceRequirement
InteractionMode
```

Response: ordered collection of canonical `PaymentMethodOption`.

## 14.2 Create session

```text
POST /service/v2/payment-sessions
Idempotency-Key: ...
```

Request:

```text
PayableInstructionId
OrderId
OrderReference
CommercialVersion
Purpose
PayerType
PayerId
InitiatorContext
Amount
CurrencyId
AssuranceRequirement
InteractionMode
ExpiresAt?
```

No `CaptureMode` and no tender-specific guarantee pair are sent by Ordering.

## 14.3 Get session

```text
GET /service/v2/payment-sessions/{paymentSessionId}
```

Authoritative read-back.

## 14.4 Confirm/fund session

```text
POST /service/v2/payment-sessions/{paymentSessionId}/confirm
Idempotency-Key: ...
```

Explicit body:

```text
SelectionMode = Explicit
Selections:
  - PaymentMethodOptionId
    Amount
ReturnUrl?
```

Default body:

```text
SelectionMode = Default
Selections = []
ReturnUrl = null
```

JetPay revalidates option eligibility before financial effect.

## 14.5 Cancel session

```text
POST /service/v2/payment-sessions/{paymentSessionId}/cancel
```

Releases uncaptured authorizations/commitments where possible.
Captured money is never silently undone; refund is a later operation.

## 14.6 Later contracts

Future:
- capture intent if explicit delayed capture is needed;
- refund session/intent;
- reconcile/query provider operation;
- chargeback/dispute evidence.

---

# 15. Integration events

Use namespace versioning and do NOT suffix type names with `V2`.

```text
AeroTech.Messages.JetPay.IntegrationEvents.V2.PaymentSessionChanged
AeroTech.Messages.JetPay.IntegrationEvents.V2.PaymentPaidUnapplied
```

## PaymentSessionChanged

Fields:

```text
PaymentSessionId: string
PayableInstructionId: string
OrderId: long
CommercialVersion: int
Purpose: PaymentPurpose
Status: PaymentSessionStatus
RequiredAmount: decimal
GuaranteedAmount: decimal
CapturedAmount: decimal
RefundedAmount: decimal
CurrencyId: int
EarliestGuaranteeExpiry: DateTimeOffset?
ExpiresAt: DateTimeOffset?
Version: long
FailureCode: string?
OccurredAt: DateTimeOffset
```

Ordering issue gate consumes session-level guarantee, not tender-specific state.

## PaymentPaidUnapplied

Required for late financial success after cancellation/expiry/superseded commercial version.

---

# 16. Idempotency

Every mutation is idempotent.

- Create same key + same payload -> same PaymentSession.
- Same key + changed payload -> conflict.
- Lost Create response -> replay same key and identical body.
- Confirm same key + same selection plan -> same committed result.
- A new explicit funding plan requires a new key.

Do not derive attempt identity from `count(existing rows)+1` without durable operation identity.
Ordering should persist the operation/idempotency identity before calling JetPay.

---

# 17. PayableInstructionId

`PayableInstructionId` is Ordering-owned and stable for one exact payable obligation.

It is NOT derived from `RequiredGuarantee` or `CaptureMode`.

It should identify:
- Order/commercial version;
- business purpose/scope/change;
- one exact amount/currency obligation.

Ordering persists it in `OrderPaymentCoverage` / payment operation evidence.

JetPay allows only one active PaymentSession per PayableInstructionId.
A new session after a definitively terminal session is allowed with a new Create idempotency key.

---

# 18. Session expiry

Ordering supplies the checkout/payment deadline based on business context.

For Stage 3 initial sale, the payment deadline should not outlive the current hard commercial/ticketing deadline and should normally be bounded by current reservation usability where applicable.

JetPay does not infer airline ticketing deadlines.

A stale AirPrice validation is NOT itself a PaymentSession expiry rule.

---

# 19. Ordering status

Payment readiness is orthogonal to reservation/order lifecycle.

Do NOT use `Order.Status = Paid` as the canonical financial state.

Ordering derives issue readiness from `OrderPaymentCoverage` / PaymentSession guarantee evidence.

A captured payment followed by reservation-confirmation failure remains financially captured; it must not disappear because Order.Status changes.

---

# 20. Exact MockJetPay v2 scope

The mock is a deterministic implementation of the FINAL JetPay V2 contract, not a throwaway fake.

## 20.1 Mock tender families

Must implement:
1. `IranianPgw`
2. `StoredValue`
3. `Bnpl`
4. `AgencyCredit`
5. one of `CustomerCredit`/`CorporateCredit` plus the other if trivial
6. `Cash`

## 20.2 Mock funding-source catalogue

The mock MUST support multiple currency-specific options per payer.

Minimum controls:
- create/set StoredValue wallet balance by payer+currency;
- mark one wallet default by payer+currency;
- create/set AgencyCredit/CorporateCredit/CustomerCredit limit;
- enable/disable cash for office/channel;
- enable/disable BNPL eligibility;
- enable/disable PGW provider profiles.

## 20.3 Mock multi-tender

Required.

Must prove:
- partial StoredValue + PGW remainder;
- StoredValue + Credit + Cash;
- one leg failure preserves successful prior legs;
- outstanding amount can be funded with a new leg;
- duplicate/idempotent confirm never double debits.

## 20.4 Mock default agency flow

Required.

Given:
- Payer=Agency;
- InteractionMode=UnattendedApi;
- SelectionMode=Default;
- default StoredValue wallet exists in order currency;

Then JetPay automatically debits it without redirect.

If insufficient:
- deterministic failure/RequiresPaymentMethod result;
- no silent PGW fallback.

## 20.5 Mock back-office flow

Required.

Given `StaffAssisted`, options may include eligible:
- StoredValue;
- Customer/Agency/Corporate credit according to payer;
- Cash;
- PGW only if policy allows staff-assisted redirect.

Confirm with multiple selections must cover exact required amount.

## 20.6 Mock PGW routing

At least two mock provider profiles must exist to prove route abstraction.

Scenarios:
- primary unavailable before any external effect -> safe secondary route;
- timeout/unknown after provider effect -> no secondary charge until reconciliation;
- redirect -> callback -> verify -> captured;
- declined verification.

## 20.7 Mock BNPL

Scenarios:
- requires customer action -> provider commitment -> issuance guarantee;
- rejected;
- guarantee expires;
- no captured money before later capture/settlement.

## 20.8 Mock credit

Scenarios:
- sufficient limit -> issuance guarantee without cash capture;
- insufficient limit;
- release commitment on cancellation;
- optional expiring authorization.

## 20.9 Mock cash

Scenarios:
- StaffAssisted + authorized office -> immediate captured/guaranteed;
- non-staff channel -> not eligible.

## 20.10 Mock time and transport faults

Keep:
- controllable mock clock;
- transport timeout after commit;
- read-back recovery;
- event delay/outbox;
- expiry sweep.

Live integration tests MUST use mock clock control instead of waiting real minutes.

---

# 21. Acceptance scenarios — minimum final set

1. B2C web sees eligible payment methods for amount/currency.
2. Ineligible method does not appear.
3. StoredValue option reports currency-specific balance.
4. Default wallet is marked only for correct payer/currency.
5. B2C PGW redirect -> verify -> Paid.
6. B2C PGW decline.
7. BNPL redirect -> provider commitment -> Guaranteed but not Paid.
8. BNPL rejection.
9. StoredValue full debit.
10. StoredValue insufficient balance.
11. StoredValue partial + PGW remainder.
12. Agency API Default mode uses default wallet without customer action.
13. Agency API default wallet insufficient -> no PGW fallback.
14. Agency explicitly selects another eligible wallet.
15. AgencyCredit guarantee with zero captured amount.
16. Corporate/Customer credit equivalent eligibility.
17. Back-office customer sees StoredValue/Credit/Cash according to policy.
18. Back-office agency sees StoredValue/AgencyCredit/Cash.
19. Back-office three-way split fully covers order.
20. One split leg fails; successful legs remain; session not issue-ready.
21. Outstanding amount is funded with replacement leg.
22. Cash unavailable outside StaffAssisted.
23. Wrong-currency wallet is not eligible.
24. One PaymentSession per PayableInstruction while active.
25. Failed/cancelled/expired session allows fresh session.
26. Same create idempotency key replays same session.
27. Lost create response recovers by replay/read-back.
28. Lost confirm response recovers without double debit.
29. Provider primary unavailable before effect safely reroutes.
30. Provider unknown after effect does not reroute.
31. Session expiry releases non-captured commitments.
32. Captured money is not cancelled silently.
33. Late capture after superseded payable becomes PaidUnapplied.
34. Duplicate events do not double-count.
35. Session GuaranteedAmount equals sum of valid intent guarantees capped at RequiredAmount.
36. Earliest guarantee expiry is derived correctly.
37. FundsReceived requirement rejects commitment-only coverage.
38. IssuanceGuaranteed accepts provider commitment or captured funds.
39. Zero-amount payable does not create a PaymentSession.
40. Payer and initiator are distinct in staff-assisted issue.

---

# 22. Ordering Stage-3 dependency rule

Ordering Stage 3 MUST NOT integrate against the current JetPay V1 contract.

Start Ordering payment integration only after:
1. JetPay V2 contracts are frozen/pushed;
2. V2 Mock implements scenarios above;
3. contract conformance tests pass;
4. Ordering receives the exact V2 contract assembly/source.

This prevents a second contract rewrite inside Ordering.
