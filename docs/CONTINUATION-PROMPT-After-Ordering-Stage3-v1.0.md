# Continuation Prompt - Review Ordering Stage 3

Continue the AeroTech Ordering project in Persian.

Current authority:
- Stage 1 CLOSED.
- Stage 2 CLOSED at/after `85f48bb652d96ce155c96ee07045223656db1ecb`.
- Master Domain ADR/PRD v1.0 FINAL.
- JetPay Payment Orchestrator Benchmark and Canonical Contract v1.0 FINAL.
- Ordering Stage 3 Payment Issue Contract v1.0 FINAL.

Expected implementation sequence:
1. MockJetPay service implemented from Coding Agent Prompt 1.
2. Ordering Stage 3 implemented from Coding Agent Prompt 2.

Review only domain behavior and end-to-end correctness.
Do not redesign architecture, folders, DI, EF style or framework conventions unless they directly violate a domain invariant.

Audit Stage 3 in this order:
1. actual branch HEAD/diff;
2. MockJetPay canonical PaymentIntent behavior;
3. idempotency/read-back/events;
4. OrderPaymentCoverage exact fields and invariants;
5. PaidBeforeIssuance vs AuthorizedBeforeIssuance semantics;
6. IranianPgw / StoredValue / BNPL / AgencyCredit E2E scenarios;
7. stale AirPrice validation refresh;
8. FlightFlow ConfirmHold success/reject/unknown behavior;
9. multi-provider no-auto-compensation;
10. Stage-3 readiness for Stage 4 Issue.

If all frozen acceptance scenarios pass, respond `STAGE_3_CLOSED` and proceed to Stage 4 design. Do not reopen Stage 1 or Stage 2 absent a concrete source-contract contradiction.
