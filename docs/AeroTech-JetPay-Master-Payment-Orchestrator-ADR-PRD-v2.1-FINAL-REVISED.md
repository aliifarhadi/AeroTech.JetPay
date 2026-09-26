# AeroTech JetPay — Master Payment Orchestrator ADR/PRD v2.1 FINAL REVISED

## Status
FINAL DOMAIN/PRODUCT AUTHORITY for JetPay. Implementation remains staged.
Supersedes JetPay Payment Orchestrator ADR/PRD v2.0 where they conflict.

## 1. Product goal
JetPay is the airline payment orchestration capability. It must support direct web/mobile sales, unattended agency APIs, staff-assisted backoffice/sales-office payment, stored value, Iranian PGWs, BNPL/credit commitments, multi-tender funding, POS/cash, and later refunds/reconciliation/accounting without forcing Ordering to know provider-specific behavior.

The contract is frozen across stages; only implementation depth changes.

## 2. Governing principles
1. Ordering owns the payable commercial obligation; JetPay owns payment orchestration and payment-provider truth.
2. Payment method != provider route.
3. Payment Session = funding of one payable instruction.
4. Payment Intent = one logical contribution using one selected payment-method option.
5. Provider Attempt = one concrete external provider effect/recovery unit.
6. Callback is not payment truth.
7. Verify/inquiry/settle/reversal semantics are provider-profile capabilities and first-class JetPay behavior.
8. Execution uncertainty is never represented as a fake successful/failed business state.
9. No second financial route is attempted while an earlier external effect is unresolved.
10. Captured funds and issuance guarantee are different facts.
11. A provider may have no refund API; that is a normal capability, not an exception.
12. Alternative refund destinations must be possible in the final domain.
13. Same tender family may fund a session multiple times (for example PGW + PGW).
14. Contract completeness is upfront; materialization is stage-by-stage.

## 3. Channels / interaction modes

### SalesChannel is source context, not payment behavior.

JetPay additionally receives:

```text
PaymentInteractionMode
  CustomerInteractive
  UnattendedApi
  StaffAssisted
```

Examples:
- B2C web: CustomerInteractive
- Agency API: UnattendedApi
- Airline backoffice / sales office: StaffAssisted

## 4. Payer and initiator

### Payer
The legal/business party whose funding source is used.

```text
PayerType
  Customer
  Agency
  Corporate
  Partner
```

Fields:
```text
PayerType
PayerId
```

### Initiator
Who is initiating the payment action; this is not necessarily the payer.

```text
PaymentInitiatorContext
  ActorType
  ActorId
  SalesChannel
  OfficeId?
```

Examples:
- Backoffice employee pays a Customer order: payer=Customer, initiator=AirlineEmployee.
- Agency API: payer=Agency, initiator=AgencyApiPrincipal.

## 5. Airline assurance requirement

Ordering must NOT choose JetPay capture mode or provider-specific guarantee semantics.

Ordering states only the business requirement:

```text
PaymentAssuranceRequirement
  FundsReceived
  IssuanceGuaranteed
```

Meaning:
- FundsReceived: only captured/debited/received funds count.
- IssuanceGuaranteed: captured funds OR a provider-backed commitment-to-pay accepted by airline policy counts.

JetPay maps this requirement to tender/provider behavior.

## 6. Aggregate roots

### 6.1 PaymentSession — aggregate root
One PaymentSession funds one PayableInstruction.

Fields:
```text
Id: string
PayableInstructionId: string
OrderId: long
OrderReference: string
CommercialVersion: int
Purpose: PaymentPurpose
IssuerLegalEntityId: long

PayerType: PayerType
PayerId: long
Initiator: PaymentInitiatorContext
InteractionMode: PaymentInteractionMode
SelectionMode: PaymentSelectionMode

RequiredAmount: decimal
CurrencyId: int
AssuranceRequirement: PaymentAssuranceRequirement

Status: PaymentSessionStatus
GuaranteedAmount: decimal
CapturedAmount: decimal
OutstandingAmount: decimal

ExpiresAt: DateTimeOffset?
Version: long
CreatedAt: DateTimeOffset
UpdatedAt: DateTimeOffset

Intents: collection<PaymentIntent>
```

Invariants:
- RequiredAmount >= 0.
- GuaranteedAmount = current valid applicable guarantee, never historical authorization.
- CapturedAmount = current captured/applicable money.
- OutstandingAmount = max(0, RequiredAmount - GuaranteedAmount) for issuance coverage.
- Intent amounts may not cause the session to apply more than RequiredAmount.
- A zero-value session is immediately Paid and creates no intent.

### 6.2 PaymentIntent — child of PaymentSession
One logical contribution from one PaymentMethodOption.

Fields:
```text
Id: string
PaymentSessionId: string
PaymentMethodOptionId: string
TenderType: TenderType
RequestedAmount: decimal
CurrencyId: int

Status: PaymentIntentStatus
AuthorizedAmount: decimal
GuaranteedAmount: decimal
CapturedAmount: decimal
RefundedAmount: decimal
GuaranteeExpiresAt: DateTimeOffset?

NextAction: CustomerAction?
FailureCode: string?
FailureReason: string?

Version: long
CreatedAt: DateTimeOffset
UpdatedAt: DateTimeOffset

ProviderAttempts: collection<ProviderPaymentAttempt>
```

### 6.3 ProviderPaymentAttempt — PaymentIntent child
This is the correct grain for Iranian callback/verify/recovery facts.

Fields:
```text
Id: long
PaymentIntentId: string
AttemptNumber: int
ProviderProfileId: string
ProviderProfileVersion: string
IdempotencyKey: string

ProviderTransactionRef: string?
Status: ProviderPaymentAttemptStatus

CallbackReceivedAt: DateTimeOffset?
VerifyDeadline: DateTimeOffset?
VerificationStartedAt: DateTimeOffset?
VerifiedAt: DateTimeOffset?
SettledAt: DateTimeOffset?
ReversalExpectedAt: DateTimeOffset?
ReversedAt: DateTimeOffset?
UnknownSince: DateTimeOffset?

FailureCode: string?
FailureReason: string?
CreatedAt: DateTimeOffset
UpdatedAt: DateTimeOffset
```

`CallbackReceivedAt` and `VerifyDeadline` are deliberately NOT PaymentIntent fields: retries/routes can create more than one provider attempt over the life of an intent, while each callback/verify window belongs to the concrete provider attempt.

## 7. PaymentSessionStatus

```text
Created
RequiresPaymentMethod
Processing
PartiallyFunded
Guaranteed
Paid
Failed
Cancelled
Expired
```

Derivation rules, in order:
1. Explicit terminal session state => Failed / Cancelled / Expired.
2. CapturedAmount >= RequiredAmount => Paid.
3. GuaranteedAmount >= RequiredAmount => Guaranteed.
4. GuaranteedAmount > 0 => PartiallyFunded, unless terminal.
5. At least one intent is RequiresCustomerAction / Processing / non-guaranteeing Authorized => Processing.
6. Session can still accept funding but no active contribution covers the remainder => RequiresPaymentMethod.
7. Created exists only before orchestration/selection begins.

One failed intent never fails the whole session by itself. The session can accept another contribution while it remains eligible.

### Guarantee expiry
When a BNPL/credit authorization guarantee expires:
- historical AuthorizedAmount remains evidence;
- current GuaranteedAmount of that intent becomes the still-captured amount, normally 0;
- session totals are recomputed;
- Guaranteed -> PartiallyFunded if some valid guarantee remains;
- Guaranteed -> Processing if another active contribution is still pending;
- Guaranteed -> RequiresPaymentMethod if no valid guarantee and no active contribution remain;
- guarantee expiry alone does NOT imply Failed;
- if Session.ExpiresAt has also passed, session becomes Expired.

## 8. PaymentIntentStatus

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

No Unknown status. Unknown belongs to ProviderPaymentAttempt execution/recovery.

## 9. ProviderPaymentAttemptStatus

```text
Created
CustomerActionPending
CallbackReceived
VerificationPending
Verified
Settled
AutoReversalPending
Reversed
Failed
Unknown
```

Rules:
- CallbackReceived is evidence, not payment success.
- Captured/paid session evidence is created only after all provider-required verify/settle steps succeed.
- If provider outcome is uncertain, attempt=Unknown and second route is blocked.

## 10. ProviderProfile — first-class reference/capability model

Fields/capabilities:
```text
Id
TenderType
Status
Version

SupportedCurrencies
AmountUnit
MinimumAmount?
MaximumAmount?

SupportsInquiry
SupportsRefund
SupportsPartialRefund
SupportsReversal
RequiresSettlementAfterVerify
SupportsProviderIdempotency
SupportsPartialAmount

UnverifiedPaymentExpiryBehavior
DefaultVerifyWindow?
```

```text
UnverifiedPaymentExpiryBehavior
  AutoReverse
  InquiryRequired
  ManualReconciliation
```

`DefaultVerifyWindow` is provider/profile data, not a universal Iranian constant. A concrete provider response may supply a more precise deadline; the actual `VerifyDeadline` is persisted on the ProviderPaymentAttempt.

## 11. Iranian PGW canonical behavior

Canonical flow:
```text
Provider attempt created
 -> redirect/token/session
 -> customer payment
 -> callback
 -> CallbackReceivedAt persisted
 -> Verify/Inquiry before VerifyDeadline
 -> Settle when profile requires it
 -> Captured
```

Rules:
1. Callback without server-side verification is never Captured.
2. Actual VerifyDeadline is stored per provider attempt.
3. If the provider contract guarantees automatic reversal for an unverified payment after the deadline, the attempt moves to AutoReversalPending/then Reversed according to authoritative provider behavior; JetPay never reports Captured.
4. If provider has no Inquiry, unresolved external effect blocks all alternative routes until:
   - authoritative callback/verification arrives, OR
   - guaranteed auto-reversal window elapses, OR
   - manual reconciliation resolves it.
5. Mellat-like Verify + Settle is one normalized capture operation; both required external steps must succeed before JetPay reports Captured.
6. Rial/Toman conversion is provider-adapter concern; JetPay canonical amounts use Order currency.

### Evidence verified for the benchmark
- NextPay publicly documents callback -> verify and a 10-minute verification window, after which an unverified successful payment is returned.
- NextPay documents a limited cancellation/refund window after verification.
- Mellat publicly documents Pay, Verify, Settle, Inquiry and Reversal as separate operations.

No universal fixed Iranian verify window is hard-coded.

## 12. Late callback rule

A late callback is handled based on whether money has already been authoritatively verified.

### A. Session/instruction is still active
If now <= VerifyDeadline, verify normally.

### B. Session is Expired/Cancelled or payable instruction is Superseded, and provider payment is NOT yet verified
JetPay MUST NOT deliberately verify/capture merely because a late callback arrived when the provider contract supports safe auto-reversal of unverified funds.

Instead:
```text
CallbackReceived
 -> AutoReversalPending
 -> Reversed / terminal non-applied outcome
```

No PaidUnapplied event is emitted because JetPay never accepted/captured the money.

If the provider does not guarantee auto-reversal, use Inquiry/manual reconciliation and continue blocking alternate financial routing while unresolved.

### C. Provider payment was already verified/captured before the session/instruction became unusable
The money is real. JetPay records it and emits PaymentPaidUnapplied.

This avoids unnecessary refund cost where an Iranian PSP can safely auto-return an unverified payment.

## 13. Payment method options

```text
PaymentMethodOption
  Id: string
  TenderType: TenderType
  DisplayCode: string
  CurrencyId: int
  AvailableAmount: decimal?
  MinimumAmount: decimal?
  MaximumAmount: decimal?
  SupportsPartialAmount: bool
  IsDefault: bool
  CanAutoSelect: bool
  CustomerActionType: CustomerActionType
  SupportedAssuranceRequirements: collection<PaymentAssuranceRequirement>
  ExpiresAt: DateTimeOffset?
```

Provider route/profile is not exposed to Ordering.

## 14. Tender taxonomy — final contract

```text
IranianPgw
ExternalCard
Bnpl
StoredValue
AgencyDeposit
CustomerCredit
AgencyCredit
CorporateCredit
BankTransferReference
Cash
PosTerminal
```

`PosTerminal` is separate from `Cash`; card-present receipt/reference, settlement and audit behavior are different from physical cash.

## 15. Multiple contributions / split tender

The final contract allows N PaymentIntents in one PaymentSession.

Valid examples:
```text
StoredValue + IranianPgw
IranianPgw + IranianPgw
StoredValue + AgencyCredit + Cash
PosTerminal + Cash
```

Same TenderType may appear more than once.

Per-leg/request amount must respect the chosen option's MaximumAmount and other eligibility constraints.

Iranian card transaction limits are runtime/reference-policy data; JetPay must never hard-code a permanent national number in the domain.

## 16. Selection modes

```text
PaymentSelectionMode
  Interactive
  Default
  Explicit
```

### Interactive
Typical B2C. Resolve eligible options; user chooses.

### Default
Typical unattended Agency API. JetPay chooses only an option marked `IsDefault && CanAutoSelect` matching payer, currency, amount and interaction mode.

No hidden fallback to a customer-interactive method is allowed.

### Explicit
Typical backoffice. Caller supplies one or more option selections and amounts.

## 17. Default Agency funding

For unattended agency API:
1. payer=Agency;
2. interaction=UnattendedApi;
3. selection=Default;
4. JetPay resolves eligible default funding for exact currency;
5. Stage A baseline: default StoredValue wallet;
6. if balance is enough -> immediate captured funding;
7. if insufficient -> session remains not funded / returns deterministic failure for default-funding operation;
8. JetPay does NOT silently redirect to PGW.

## 18. Cash and POS

### Cash
- eligible only for StaffAssisted contexts authorized for cash collection;
- employee confirmation records received cash as captured funding;
- receipt/reference may be required by airline policy.

### PosTerminal
- first-class card-present tender;
- eligible only for StaffAssisted/card-present contexts;
- provider/terminal reference and receipt evidence belong to JetPay;
- distinct from Cash.

These are Stage B materialization, but contract taxonomy is frozen now.

## 19. Refund routing — final-domain decision, Stage C materialization

ProviderProfile.SupportsRefund=false is a normal case.

The final refund model MUST separate:
```text
SourcePayment
RefundDestination
```

A refund may legitimately originate from IranianPgw and be paid to:
- original tender when supported;
- StoredValue;
- AgencyDeposit;
- approved BankAccount/IBAN payout rail;
- another airline-approved refund destination.

No automatic alternative destination is chosen without explicit airline refund policy / instruction.

Limited same-day provider reversal is not equivalent to a general post-ticket refund capability.

## 20. Accounting / Ledger future contract

Accounting stays out of Ordering, but JetPay must not lose the data required to dispatch accounting facts later.

`IssuerLegalEntityId` is immutable PaymentSession data from Stage A.

Stage C will reintroduce/replace the earlier `SubmitPaymentFactCommand` concept with normalized facts sufficient for at least:
- PGW captured sale and later T+N settlement/fee;
- cash/POS office receipt;
- stored-value liability movement;
- agency credit/deposit receivable/commitment;
- refunds/reversals;
- payment fee/settlement/reconciliation differences.

Exact LedgerFlow fact schema is Stage C source-gated. Do not invent journal entries in Stage A/B.

## 21. Public Ordering-facing contract — freeze now

Ordering integrates at PaymentSession level, not provider-attempt level.

### 21.1 Resolve eligible payment methods
```text
POST /service/v2/payment-method-options/resolve
```

Request:
```text
OrderId
OrderReference
CommercialVersion
Purpose
IssuerLegalEntityId
PayerType
PayerId
Initiator
InteractionMode
Amount
CurrencyId
AssuranceRequirement
```

Response: PaymentMethodOption[]

### 21.2 Create PaymentSession
```text
POST /service/v2/payment-sessions
Idempotency-Key: required
```

Request:
```text
PayableInstructionId
OrderId
OrderReference
CommercialVersion
Purpose
IssuerLegalEntityId
PayerType
PayerId
Initiator
InteractionMode
SelectionMode
RequiredAmount
CurrencyId
AssuranceRequirement
ExpiresAt?
Selections?       // allowed for Explicit
```

Behavior:
- zero amount -> Paid, no intents;
- Interactive -> RequiresPaymentMethod unless selections supplied later;
- Default -> attempts only eligible auto-selectable default method;
- Explicit -> creates requested contributions.

### 21.3 Add/Start payment selections
```text
POST /service/v2/payment-sessions/{sessionId}/selections
Idempotency-Key: required
```

Request:
```text
Selections[]
  PaymentMethodOptionId
  Amount
ReturnUrl?
```

Allows one or many selections; Stage A may enforce max one selection except default StoredValue/PGW flow, without changing the contract.

### 21.4 Get authoritative session
```text
GET /service/v2/payment-sessions/{sessionId}
```

### 21.5 Cancel session
```text
POST /service/v2/payment-sessions/{sessionId}/cancel
Idempotency-Key: required
```

Captured money is never silently erased; cancellation can leave PaidUnapplied/refund-required evidence according to timing.

### Future provider-independent operations without changing session identity
Stage B/C may add intent-level capture/refund/reconcile endpoints, but Ordering Stage 3 does not depend on them.

## 22. Integration events — freeze now

### PaymentSessionChanged V1
Fields:
```text
PaymentSessionId: string
PayableInstructionId: string
OrderId: long
CommercialVersion: int
Purpose: PaymentPurpose
Status: PaymentSessionStatus
AssuranceRequirement: PaymentAssuranceRequirement
RequiredAmount: decimal
GuaranteedAmount: decimal
CapturedAmount: decimal
OutstandingAmount: decimal
CurrencyId: int
ExpiresAt: DateTimeOffset?
Version: long
FailureCode: string?
OccurredAt: DateTimeOffset
```

### PaymentPaidUnapplied V1
Fields:
```text
PaymentSessionId: string
PaymentIntentId: string
OrderId: long
SupersededPayableInstructionId: string
CapturedAmount: decimal
CurrencyId: int
ReasonCode: string
OccurredAt: DateTimeOffset
```

Event type names live in `AeroTech.Messages.JetPay.IntegrationEvents.V1` and therefore do NOT include a `V1` suffix.

## 23. Mock requirements — contract vs stage depth

Mock implements the real contract. It is not a throwaway API.

### Stage A — required now to unblock Ordering
Materialize only:
- PaymentSession core and status derivation;
- ProviderPaymentAttempt core needed for Iranian PGW safety;
- Resolve options;
- Create/Get/Cancel session;
- Add/Start selection;
- PaymentSessionChanged / PaymentPaidUnapplied;
- IranianPgw mock with redirect -> callback -> verify -> optional settle;
- explicit VerifyDeadline and callback timestamps;
- provider-with-inquiry and provider-without-inquiry recovery scenario;
- safe late-callback no-verify rule;
- StoredValue wallet with payer+currency balance and default flag;
- unattended Agency default StoredValue funding;
- zero-value session;
- idempotency and lost-response read-back.

Do NOT implement Stage B/C behavior in Stage A.

### Stage B — later
- multi-tender execution;
- PGW + PGW;
- partial StoredValue;
- Cash;
- PosTerminal;
- AgencyCredit / CorporateCredit / CustomerCredit;
- BNPL;
- guarantee expiry transitions;
- staff-assisted backoffice selection.

The Stage A public contract MUST already tolerate multiple selections so Stage B does not break Ordering.

### Stage C — later
- refunds / alternate refund destinations;
- reversals beyond Stage-A PGW safety;
- settlement reconciliation;
- chargebacks/disputes;
- LedgerFlow payment facts;
- provider fees / settlement timing.

## 24. Stage A acceptance scenarios

A01 B2C resolves eligible methods for payer/currency/channel.
A02 B2C selects IranianPgw and receives RequiresCustomerAction redirect.
A03 callback is persisted but does not make payment Paid before verify.
A04 verify succeeds -> provider-required settle succeeds -> session Paid.
A05 callback received after VerifyDeadline -> never Captured.
A06 session expires before late unverified callback -> do not verify; safe auto-reversal path.
A07 callback after provider payment had already been verified but payable is superseded -> PaymentPaidUnapplied.
A08 lost callback + SupportsInquiry -> inquiry/recovery resolves without second charge.
A09 provider without Inquiry + unresolved effect -> alternative route blocked until auto-reversal/reconciliation boundary.
A10 same mutation/idempotency key -> same committed effect.
A11 transport timeout after commit -> GET/replay recovers same session/effect.
A12 Agency API with default IRR StoredValue and sufficient balance -> Paid with no customer action.
A13 Agency default wallet insufficient -> no hidden PGW fallback.
A14 wallet of wrong currency is not eligible.
A15 zero amount -> session Paid, no provider intent/attempt.
A16 cancelled/expired session does not accept new funding.
A17 PaymentSessionChanged version is monotonic and duplicate events are safe for consumers.
A18 IssuerLegalEntityId survives persistence/reload.

## 25. Stage B acceptance scenarios (frozen, not Stage A implementation)

B01 StoredValue + PGW.
B02 PGW + PGW due per-transaction cap.
B03 StoredValue + AgencyCredit + Cash.
B04 partial StoredValue balance.
B05 multiple identical TenderType intents remain distinct contributions.
B06 StaffAssisted Cash eligibility.
B07 StaffAssisted PosTerminal eligibility and receipt/reference.
B08 BNPL authorization provides valid issuance guarantee when policy/profile allows.
B09 credit guarantee expires -> session recomputes from Guaranteed to PartiallyFunded/RequiresPaymentMethod/Processing, never automatically Failed.
B10 backoffice explicit allocations must sum <= outstanding and cannot overfund.

## 26. Stage C acceptance scenarios (frozen, not Stage A implementation)

C01 ProviderProfile.SupportsRefund=false is handled normally.
C02 IranianPgw sale refunded to StoredValue when authorized by refund instruction.
C03 IranianPgw sale paid to approved bank-account payout rail when policy/source allows.
C04 provider reversal vs business refund remain different operations.
C05 PGW settlement/fee fact dispatch to LedgerFlow.
C06 cash/POS receipt fact dispatch.
C07 agency credit/deposit receivable fact dispatch.
C08 reconciliation mismatch is preserved, never silently normalized.

## 27. Explicit non-goals for Stage A
- real PSP integration;
- real StoredValue service integration;
- real LedgerFlow posting;
- real BNPL/credit;
- backoffice multi-tender execution;
- Cash/POS implementation;
- refund implementation;
- provider smart-routing optimization.

## 28. Source/benchmark notes
- IATA Payment Services / Financial Gateway: omnichannel airline payment orchestration, multiple forms of payment/providers.
- IATA Settlement with Orders: commitment-to-pay between airline and seller/agent.
- IATA Airline Payment Framework: cash, cards, wallets, online credit, offline payment and agency contexts.
- Stripe PaymentIntent: idempotent payment lifecycle and read-back concepts.
- Adyen partial payments/orders: one payable order funded by multiple payment methods.
- Adyen Terminal API: card-present POS is a distinct payment interaction.
- NextPay public docs: callback -> verify, explicit verify deadline and automatic return if unverified, limited post-verify reversal/refund window.
- Mellat public PGW guide: Pay, Verify, Settle, Inquiry, Reversal are distinct methods.

No universal Iranian verify window, refund support, inquiry support or transaction limit is hard-coded; all are provider/reference policy data.
