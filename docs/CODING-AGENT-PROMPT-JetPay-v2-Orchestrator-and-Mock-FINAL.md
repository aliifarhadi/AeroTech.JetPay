# CODING AGENT — JetPay v2 Orchestrator + Mock FINAL

Repository: `aliifarhadi/AeroTech.JetPay`
Branch: `k8s-stg`
Baseline reviewed: `b176e5ec124ec13682bb1e579c51ce8b52e2825d`

## Authority

Implement ONLY the domain/behavior defined in:
`AeroTech-JetPay-Master-Payment-Orchestrator-ADR-PRD-v2.0-FINAL.md`

The existing v1 JetPay code is donor code, not authority where it conflicts with v2.

Do not redesign Ordering.
Do not implement real bank/DigiPay/StoredValue/Ledger connectors.
Do not add speculative PSP-specific fields to public contracts.

## Required outcome

JetPay must behave as an airline payment orchestrator supporting:
- B2C method eligibility + explicit selection;
- unattended agency API default wallet by currency;
- staff-assisted eligible-method listing;
- staff multi-tender composition;
- PGW, StoredValue, BNPL, credit, Cash mock paths;
- exact idempotency/read-back/event behavior.

## Canonical roots

### PaymentSession
Implement exactly the fields and invariants in Master §3.2.

### PaymentIntent
Refactor the existing root so one PaymentIntent is one tender contribution belonging to a PaymentSession, with fields in Master §3.3.

Do not keep top-level payable behavior that assumes one full-amount tender == whole payable instruction.

## Required contract redesign

Create `AeroTech.Messages.JetPay` V2 external contract.

### Enums
Implement:
- PayerType = Customer, Agency, Corporate, Partner
- PaymentInteractionMode = CustomerInteractive, UnattendedApi, StaffAssisted
- PaymentSelectionMode = Explicit, Default
- PaymentAssuranceRequirement = FundsReceived, IssuanceGuaranteed
- PaymentAssuranceCapability = FundsReceived, CommitmentToPay
- PaymentSessionStatus = Created, RequiresPaymentMethod, RequiresCustomerAction, Processing, PartiallyCovered, Guaranteed, Paid, Cancelled, Expired
- existing per-intent PaymentIntentStatus may remain Created, RequiresCustomerAction, Processing, Authorized, PartiallyCaptured, Captured, Failed, Cancelled, Expired
- TenderType must include at least IranianPgw, ExternalCard, AccountToAccount, Bnpl, StoredValue, AgencyDeposit, CustomerCredit, AgencyCredit, CorporateCredit, Cash, BankTransferReference, Voucher, LoyaltyPoints

Do not expose ProviderCode in V2 Ordering-facing contracts.

### V2 API
Implement canonical endpoints from Master §14:
- POST `/service/v2/payment-method-options/resolve`
- POST `/service/v2/payment-sessions`
- GET `/service/v2/payment-sessions/{id}`
- POST `/service/v2/payment-sessions/{id}/confirm`
- POST `/service/v2/payment-sessions/{id}/cancel`

All mutations require Idempotency-Key.

No Ordering-facing request may require `CaptureMode`.
No Ordering-facing request may require tender-specific `RequiredGuarantee`.

## PaymentMethodOption
Implement exact shape from Master §3.4.

Eligibility must no longer be a static `PayerType -> list` switch.
Eligibility must account for payer, currency, amount, purpose, interaction mode, funding availability and policy.

## Explicit selection

Confirm request with `SelectionMode=Explicit` accepts N selections:

```text
PaymentMethodOptionId
Amount
```

Validate sum and partial-payment rules.

## Default selection

Confirm request with `SelectionMode=Default` has no selections.

For an Agency in `UnattendedApi`:
- select exact configured default StoredValue option for session currency;
- execute without customer action;
- if absent/insufficient, return deterministic RequiresPaymentMethod/failure result;
- DO NOT fall back to PGW.

## Mock funding catalogue

Add deterministic mock controls for:
- wallet balances by payer/currency;
- default wallet by payer/currency;
- agency/customer/corporate credit limits;
- cash eligibility by office/channel;
- BNPL eligibility;
- PGW provider-profile availability.

Keep mock controls outside public JetPay V2 contract.

## Mock tender behavior

Implement at least:
- IranianPgw
- StoredValue
- Bnpl
- AgencyCredit
- CustomerCredit or CorporateCredit (prefer both if same implementation is reusable)
- Cash

### IranianPgw
- at least two internal mock provider profiles;
- safe failover only before external effect;
- unknown-after-effect => no reroute until reconciliation;
- redirect -> callback -> server verify -> capture;
- decline path.

### StoredValue
- full debit;
- partial debit;
- insufficient balance;
- currency-specific option;
- default option;
- timeout/read-back without double debit.

### BNPL
- redirect/action;
- provider commitment;
- GuaranteedAmount without CapturedAmount;
- rejection;
- expiry.

### Credit
- available exposure -> commitment guarantee;
- insufficient exposure;
- release on cancellation;
- no fake cash capture.

### Cash
- only StaffAssisted and permitted office;
- immediate captured/guaranteed;
- unavailable for web/agency unattended API.

## Multi-tender — REQUIRED NOW

Implement these exact cases:
1. StoredValue partial + PGW remainder.
2. StoredValue + Credit + Cash.
3. One leg fails: successful prior legs remain durable.
4. Add replacement leg for outstanding amount.
5. Session is business-ready only when aggregate valid guarantee covers RequiredAmount.

Do not auto-refund successful legs because another leg fails.

## Events

Create V2 events in namespace:
`AeroTech.Messages.JetPay.IntegrationEvents.V2`

Type names have NO V2 suffix:
- PaymentSessionChanged
- PaymentPaidUnapplied

Implement exact PaymentSessionChanged fields from Master §15.

## Idempotency

Conformance tests must prove:
- create replay;
- create payload conflict;
- confirm plan replay;
- changed plan with same key conflict;
- timeout-after-commit read-back;
- no duplicate wallet debit;
- no duplicate cash receipt;
- no duplicate credit reservation.

## Required acceptance tests

Implement all 40 scenarios in Master §21 or an equivalent matrix proving each behavior exactly.

## Preserve from v1

KEEP where compatible:
- IdempotencyRecord machinery;
- outbox/inbox;
- mock clock;
- transport fault injection;
- server-side provider verification;
- ITenderProvider adapter boundary;
- read-back semantics;
- no Unknown PaymentIntent state.

## Remove/replace

Remove/replace V2 behavior that:
- requires CaptureMode from Ordering;
- requires exact RequiredGuarantee pair from Ordering;
- terminates whole payment obligation after one tender failure;
- returns static options by payer only;
- assumes a tender must cover the entire amount;
- omits Cash or multi-tender.

## Completion report

Return:
1. commit SHA;
2. exact V2 contract types and enum values;
3. PaymentSession fields;
4. PaymentIntent fields;
5. PaymentMethodOption fields;
6. endpoint list;
7. event list;
8. mock funding-source controls;
9. acceptance scenario matrix with pass/fail;
10. old V1 types intentionally retained for compatibility, if any;
11. any BLOCKED_SOURCE items.

Do not change Ordering in this task.
