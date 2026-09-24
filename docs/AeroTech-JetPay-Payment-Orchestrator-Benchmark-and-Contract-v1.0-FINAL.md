# AeroTech JetPay - Payment Orchestrator Benchmark and Canonical Contract v1.0 FINAL

Status: FINAL PAYMENT DOMAIN AUTHORITY for Stage 3 design
Scope: Payment orchestration semantics, not implementation architecture
Audience: Product Owner, Ordering domain reviewer, Coding Agent

## 1. Decision

JetPay is a payment orchestrator, not an Ordering subdomain and not a bank-gateway wrapper only.

Ordering owns:
- the commercial Order and CommercialVersion;
- payable amount and currency;
- the business purpose of payment;
- whether Issue requires paid funds or an accepted authorization/commitment;
- Issue orchestration.

JetPay owns:
- PaymentIntent lifecycle;
- tender/payment-method selection and routing;
- PSP/bank/BNPL/wallet/credit-provider interaction;
- provider references and callback verification;
- authorization, capture, release/void, refund execution;
- customer-action state;
- payment reconciliation after timeout/lost callback;
- payment-provider capability differences.

Ordering MUST NOT infer a bank/BNPL/wallet provider state.

## 2. Benchmarks

The canonical contract is normalized from:
- IATA Financial Gateway / Airline Payment Services: omnichannel orchestration, multiple PSPs/payment methods, authorization/capture, agency channels, BNPL and local methods.
- IATA Settlement with Orders: commitment-to-pay semantics for seller/airline settlement.
- IATA BSP / Airline Payment Framework: airline direct and indirect payment instruments and agency settlement.
- Stripe PaymentIntent: one intent per order/session, requires-action/processing/authorization/capture style lifecycle, idempotent mutation, read-back.
- Adyen: authorization/capture separation, asynchronous webhooks, idempotency, stored value and partial payment patterns.
- PayPal Orders/Payments: AUTHORIZE vs CAPTURE, read-back, idempotent mutations.
- Iranian IPG patterns: create token/session, redirect customer, callback, server-side verify/inquiry, optional settle/reversal/refund.
- Behpardakht Mellat: Pay -> Verify -> Settle, with Inquiry/Reversal support.
- NextPay: token -> redirect -> callback -> server-side verify, plus refund/inquiry.

No proprietary Amadeus/Sabre payment aggregate shape is assumed.

## 3. Canonical principles

1. One PaymentIntent represents one logical payable instruction.
2. A transport timeout is not a PaymentIntent business status.
3. Every mutation is idempotent.
4. Callback/redirect alone is never authoritative payment truth.
5. JetPay performs server-side verify/read-back/reconciliation before publishing success.
6. Provider routing is JetPay-owned and opaque to Ordering.
7. Amount/currency are immutable for an existing intent.
8. A successful provider attempt is never repeated through another route until uncertainty is reconciled.
9. Authorization and capture are different facts.
10. Settlement to the airline bank account is different from capture/payment acceptance and is not an Issue gate.
11. Payment method and provider are different concepts.
12. No PAN/CVV or equivalent secret is stored in Ordering.
13. Stored-value, BNPL and credit are first-class tender categories, not special flags on card payment.

## 4. Canonical tender types

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
```

Stage 3 required tender families:
- IranianPgw
- Bnpl
- StoredValue
- AgencyCredit

Provider-specific values such as Mellat, SEP, Pasargad, DigiPay, etc. are routes/provider profiles, NOT TenderType values.

## 5. Required guarantee

```text
PaidBeforeIssuance
AuthorizedBeforeIssuance
```

Meaning:

### PaidBeforeIssuance
JetPay may report the requested amount as guaranteed only after the payment method has reached a provider-verified captured/paid state acceptable to the airline.

Typical Stage-3 examples:
- Iranian IPG
- StoredValue immediate debit

### AuthorizedBeforeIssuance
JetPay may report the requested amount as guaranteed after a provider-backed authorization or commitment-to-pay that the airline explicitly accepts for issuance.

Typical examples when contractually enabled:
- BNPL provider approval/guarantee
- Agency credit-line authorization
- Corporate credit-line authorization
- card authorization with manual capture, if airline policy explicitly allows it

Ordering never infers whether an authorization is good enough from TenderType. It consumes JetPay's normalized guarantee.

## 6. PaymentIntent aggregate - final shape

```text
PaymentIntent
  Id: string
  PayableInstructionId: string
  OrderId: long
  OrderReference: string
  CommercialVersion: int
  Purpose: PaymentPurpose

  PayerType: PayerType
  PayerId: long

  RequestedAmount: decimal
  CurrencyId: int
  RequiredGuarantee: RequiredGuarantee
  CaptureMode: PaymentCaptureMode

  Status: PaymentIntentStatus

  AuthorizedAmount: decimal
  GuaranteedAmount: decimal
  CapturedAmount: decimal
  RefundedAmount: decimal

  GuaranteeExpiresAt: DateTimeOffset?
  IntentExpiresAt: DateTimeOffset?

  SelectedTenderType: TenderType?
  SelectedPaymentMethodOptionId: string?

  NextAction: CustomerAction?
  FailureCode: string?
  FailureReason: string?

  Version: long
  CreatedAt: DateTimeOffset
  UpdatedAt: DateTimeOffset
```

### Null guarantee-expiry rule

`GuaranteeExpiresAt = null` means JetPay asserts there is no known time-limited expiry for the currently exposed guarantee.

It MUST NOT mean "expiry is unknown".

If expiry is unknown and the provider contract requires one, JetPay must not expose that authorization as valid issuance guarantee.

## 7. PaymentIntent status

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

No `Unknown` PaymentIntent status.

Unknown belongs to provider-operation execution/recovery inside JetPay. The intent remains at the last authoritative business state until reconciled.

## 8. Guarantee invariant

JetPay owns this invariant:

```text
0 <= GuaranteedAmount <= RequestedAmount
0 <= CapturedAmount <= RequestedAmount
```

For `PaidBeforeIssuance`:

```text
GuaranteedAmount <= CapturedAmount
```

For `AuthorizedBeforeIssuance`, JetPay may expose authorized commitment as GuaranteedAmount before CapturedAmount increases.

A guarantee that has expired contributes zero to Issue eligibility even if historical AuthorizedAmount remains non-zero.

## 9. PaymentLeg - internal JetPay child

One PaymentIntent may eventually have more than one leg for legitimate split tender, but Stage 3 allows only one active leg.

```text
PaymentLeg
  Id: long
  PaymentIntentId: string
  Sequence: int
  TenderType: TenderType
  RequestedAmount: decimal
  CurrencyId: int
  TransactionModel: TransactionModel
  Status: PaymentLegStatus

  AuthorizedAmount: decimal
  CapturedAmount: decimal
  RefundedAmount: decimal
  AuthorizationExpiresAt: DateTimeOffset?

  ProviderProfileId: long
  ProviderPaymentReference: string?
  ProviderSettlementReference: string?

  CreatedAt: DateTimeOffset
  UpdatedAt: DateTimeOffset
```

## 10. Transaction models

```text
ImmediateSale
AuthorizationCapture
DebitThenVerify
OfflineReference
```

Expected mappings:
- IranianPgw: usually DebitThenVerify or provider-equivalent immediate sale + verify.
- Bnpl: usually AuthorizationCapture / commitment-to-pay semantics.
- StoredValue: ImmediateSale or AuthorizationCapture depending wallet capability.
- AgencyCredit: AuthorizationCapture semantics where authorization reserves credit/exposure and capture posts the receivable/consumption.

The mapping is a ProviderProfile capability, not Ordering logic.

## 11. ProviderAttempt - internal JetPay execution evidence

```text
ProviderAttempt
  Id: long
  PaymentLegId: long
  AttemptNumber: int
  OperationType: PaymentProviderOperationType
  ProviderProfileId: long
  IdempotencyKey: string
  CorrelationReference: string
  RequestPayload: string
  RequestHash: string
  ResponsePayload: string?
  ResponseHash: string?
  ProviderReference: string?
  Status: ProviderAttemptStatus
  NormalizedOutcome: NormalizedOutcome?
  StartedAt: DateTimeOffset
  CompletedAt: DateTimeOffset?
  Error: string?
```

Every provider mutation request must be durable before dispatch.
Unknown retry replays the exact original request when safe.

## 12. ProviderProfile capability model

```text
ProviderProfile
  Id: long
  ProviderCode: string
  Status: ProviderProfileStatus
  SupportedTenderTypes: collection
  TransactionModel: TransactionModel
  AmountUnit: AmountUnit

  SupportsInquiry: bool
  SupportsVerify: bool
  RequiresSettlementAfterVerify: bool
  SupportsReversal: bool
  SupportsRefund: bool
  SupportsPartialRefund: bool
  SupportsAuthorization: bool
  SupportsCapture: bool
  SupportsVoidAuthorization: bool
  SupportsWebhook: bool
  SupportsRedirect: bool
  SupportsHtmlForm: bool
  SupportsSdkAction: bool

  SupportsIssuanceGuaranteeOnAuthorization: bool
```

Routing can additionally consider provider health, cost, channel and merchant configuration, but those routing rules are JetPay-internal.

## 13. Iranian IPG normalization

Iranian bank/PSP adapters differ, but JetPay must normalize the common flow:

```text
Create provider transaction/session/token
        -> customer redirect/form
        -> provider callback
        -> server-side Verify or Inquiry
        -> Settle when the provider requires it
        -> Captured/paid normalized result
```

Rules:
- callback parameters are evidence, not final truth;
- verify/inquiry uses stored amount/order/provider reference;
- provider-specific Rial/Toman conversion happens only in the adapter;
- internal amount remains canonical Order currency amount;
- a gateway that requires Verify + Settle is not considered Paid before both provider-required steps succeed;
- lost callback must be recoverable by inquiry when supported;
- if a provider has no safe inquiry/idempotency and the external effect is uncertain, do not route a second charge to another provider until reconciled/manual resolution.

Behpardakht Mellat is a concrete example of Pay -> Verify -> Settle with Inquiry/Reversal.
NextPay is a concrete example of token -> redirect -> callback -> Verify, with transaction status and refund capabilities.

## 14. BNPL normalization

DigiPay is treated as a BNPL provider adapter behind TenderType.Bnpl.

Canonical semantics:
- customer may require redirect/app action;
- provider may approve a credit/instalment plan before airline cash settlement;
- provider approval may become issuance guarantee only when the merchant contract says the commitment is firm enough for ticket issuance;
- later capture/settlement/delivery notifications are provider-specific and remain inside JetPay.

IMPORTANT:
Public authoritative DigiPay Iran merchant API documentation was not available in the benchmark. Therefore exact DigiPay endpoint names, ticket types, delivery calls and OAuth payloads are NOT frozen here. Those remain adapter-contract-gated.

JetPay's public contract must not change when the real DigiPay adapter is later wired.

## 15. StoredValue normalization

StoredValue means an airline/customer stored-value balance, wallet or equivalent value account.

Canonical capabilities:
- balance/eligibility check;
- authorize/reserve value when supported;
- debit/capture;
- release unused authorization;
- refund/credit back later.

Adyen stored-value/gift-card patterns confirm that stored value is a first-class payment source with balance and partial-payment semantics.

Stage 3 mock may implement full-amount StoredValue only. Split tender is final-domain capability but not Stage-3 materialization.

## 16. AgencyCredit normalization

AgencyCredit is not a fake card payment.

It represents an airline-approved credit account / commercial commitment-to-pay for an agency.

Canonical Stage-3 behavior:
- identify Agency payer;
- check active credit account and available exposure;
- authorize/reserve requested amount;
- return Authorized state and GuaranteedAmount when approved;
- capture/commit later consumes the credit authorization / creates receivable settlement obligation;
- release authorization if Issue is abandoned before commit.

IATA Settlement with Orders and BSP semantics provide the benchmark: indirect sales can rely on settlement agreements and commitment-to-pay rather than immediate passenger cash capture.

## 17. Payment-method eligibility query

JetPay exposes normalized eligible methods rather than PSP names.

Request:

```text
ResolvePaymentMethodOptionsRequest
  OrderId: long
  OrderReference: string
  CommercialVersion: int
  PayerType: PayerType
  PayerId: long
  Amount: decimal
  CurrencyId: int
  SalesChannel: string
```

Response item:

```text
PaymentMethodOption
  Id: string
  TenderType: TenderType
  DisplayCode: string
  CustomerActionType: CustomerActionType
  SupportedGuarantees: collection<RequiredGuarantee>
  SupportedCaptureModes: collection<PaymentCaptureMode>
```

ProviderCode is not exposed to Ordering.

## 18. CustomerAction

```text
CustomerAction
  Type: CustomerActionType
  Url: string?
  HttpMethod: string?
  FormFields: map<string,string>?
  ExpiresAt: DateTimeOffset?
```

Types:

```text
None
Redirect
HtmlForm
Sdk
ThreeDsChallenge
```

Stage-3 mock requires Redirect and None.

## 19. S2S API contract

All paths are internal service-to-service paths.

### 19.1 Resolve eligible methods

```text
POST /service/v1/payment-method-options/resolve
```

### 19.2 Create intent

```text
POST /service/v1/payment-intents
Idempotency-Key: required
```

Request:

```text
CreatePaymentIntentRequest
  PayableInstructionId: string
  OrderId: long
  OrderReference: string
  CommercialVersion: int
  Purpose: PaymentPurpose
  PayerType: PayerType
  PayerId: long
  Amount: decimal
  CurrencyId: int
  RequiredGuarantee: RequiredGuarantee
  CaptureMode: PaymentCaptureMode
  IntentExpiresAt: DateTimeOffset?
```

Duplicate request with same idempotency key and same payload returns the same intent.
Same key with different payload is rejected.

### 19.3 Get intent

```text
GET /service/v1/payment-intents/{paymentIntentId}
```

Authoritative read-back of current intent state.

### 19.4 Confirm / start payment

```text
POST /service/v1/payment-intents/{paymentIntentId}/confirm
Idempotency-Key: required
```

Request:

```text
ConfirmPaymentIntentRequest
  PaymentMethodOptionId: string
  ReturnUrl: string?
```

Result may be RequiresCustomerAction, Processing, Authorized or Captured.

### 19.5 Capture

```text
POST /service/v1/payment-intents/{paymentIntentId}/capture
Idempotency-Key: required
```

Request:

```text
CapturePaymentIntentRequest
  Amount: decimal?
  FinalCapture: bool
```

Only valid for a capturable/authorized intent and provider capability.

### 19.6 Cancel

```text
POST /service/v1/payment-intents/{paymentIntentId}/cancel
Idempotency-Key: required
```

Request:

```text
CancelPaymentIntentRequest
  Reason: PaymentCancellationReason
```

Captured money is not cancelled; later refund flow handles money return.

## 20. Authoritative event contract

Use one versioned state-snapshot event for Stage 3.

```text
PaymentIntentChangedV1
  EventId: string
  PaymentIntentId: string
  PayableInstructionId: string
  OrderId: long
  CommercialVersion: int
  Purpose: PaymentPurpose
  Status: PaymentIntentStatus
  RequiredGuarantee: RequiredGuarantee
  CaptureMode: PaymentCaptureMode
  RequestedAmount: decimal
  AuthorizedAmount: decimal
  GuaranteedAmount: decimal
  CapturedAmount: decimal
  CurrencyId: int
  GuaranteeExpiresAt: DateTimeOffset?
  IntentExpiresAt: DateTimeOffset?
  Version: long
  FailureCode: string?
  OccurredAt: DateTimeOffset
```

Rules:
- EventId is globally unique.
- Version is monotonic per PaymentIntent.
- Consumers ignore duplicates.
- Consumers ignore older versions.
- Event is emitted after durable JetPay state commit.
- RequiresCustomerAction event must not expose provider secrets or card data.

Special later event retained for stale money:

```text
PaymentPaidUnappliedV1
  EventId: string
  PaymentIntentId: string
  OrderId: long
  SupersededPayableInstructionId: string
  CapturedAmount: decimal
  CurrencyId: int
  ReasonCode: string
  OccurredAt: DateTimeOffset
```

Chargeback/refund events are future-stage contracts, not Stage-3 implementation.

## 21. Stage-3 mock behavior matrix

### IranianPgw

```text
Create -> Created
Confirm -> RequiresCustomerAction + Redirect
Simulated verified callback -> Processing -> Captured
GuaranteedAmount = CapturedAmount for PaidBeforeIssuance
```

### StoredValue

```text
Create -> Created
Confirm with enough balance -> Captured immediately
Confirm with insufficient balance -> Failed
```

### Bnpl

```text
Create -> Created
Confirm -> RequiresCustomerAction
Simulated provider approval -> Authorized
GuaranteedAmount = requested amount only if mock provider profile supports issuance guarantee
Capture may occur later
```

### AgencyCredit

```text
Create -> Created
Confirm with available credit -> Authorized immediately
GuaranteedAmount = requested amount
GuaranteeExpiresAt according to mock policy or null with the exact null semantics above
Insufficient credit -> Failed
```

## 22. Split tender

The final JetPay domain supports multiple PaymentLeg values and partial stored-value patterns.

Stage 3 MUST NOT implement split tender unless Ordering Stage-3 scenarios explicitly require it.

This prevents early complexity while keeping the final model extensible.

## 23. Refund / reversal / chargeback - future, not Stage 3

JetPay final domain must support:
- refund full/partial when provider supports it;
- provider reversal/void when appropriate;
- chargeback/dispute evidence;
- settlement/reconciliation evidence.

Ordering remains refund-entitlement authority; JetPay executes money movement.

## 24. Non-goals for Stage 3 mock

Do not implement:
- real bank/PSP connectors;
- real DigiPay connector;
- real StoredValue ledger;
- real agency credit ledger;
- fraud engine;
- smart routing optimization;
- PCI card capture;
- refund/chargeback processing;
- split tender;
- production settlement accounting.

The mock exists only to make the final Ordering/JetPay contract executable now.

## 25. Source references

- IATA Payment Services: https://www.iata.org/en/services/finance/payment-services/
- IATA Financial Gateway: https://www.iata.org/ifg/
- IATA Settlement with Orders: https://www.iata.org/en/programs/airline-distribution/retailing/settlement-orders-swo/
- IATA BSP: https://www.iata.org/en/services/finance/bsp/
- Stripe PaymentIntents: https://docs.stripe.com/api/payment_intents
- Stripe Confirm PaymentIntent: https://docs.stripe.com/api/payment_intents/confirm
- Stripe Idempotency: https://docs.stripe.com/api/idempotent_requests
- Adyen Capture: https://docs.adyen.com/online-payments/capture
- Adyen Idempotency: https://docs.adyen.com/development-resources/api-idempotency/
- Adyen Stored Value API: https://docs.adyen.com/payment-methods/gift-cards/stored-value-api
- PayPal Orders/Payments: https://developer.paypal.com/api/payments/v2
- NextPay API: https://nextpay.org/nx/docs
- Behpardakht Mellat user manual: public Mellat PGW guide describing Pay/Verify/Settle/Inquiry/Reversal.

