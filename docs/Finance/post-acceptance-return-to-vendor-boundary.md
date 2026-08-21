# Post-Acceptance Return to Vendor: Finance Boundary

**Status:** Planned cross-module integration; not available for end-to-end UAT
**Owners:** Procurement and Inventory produce the operational facts; Finance consumes them
**Review date:** 18 August 2026

> Maintainer note: this document records an agreed cross-team boundary. Finance's v0.1 envelope
> validator is implemented but deliberately non-posting; it must not be used as evidence that the
> Procurement/Inventory producer workflow or end-to-end accounting has been delivered. Keep the
> contract catalogue, consumer tests, user manual, and demo playbook aligned as the producer and
> executable Finance orchestration slices are merged.

## Why three different cases are required

The word "return" describes three economically different events. They must not share one generic
inventory adjustment because their supplier rights, evidence, tax, subledger, and accounting effects
are different.

| Case | Business meaning | System owner and treatment | Current status |
|---|---|---|---|
| Receipt rejection before acceptance | Inspection finds that delivered goods are unacceptable before they become accepted stock. | Procurement controls inspection and rejection; only accepted quantity may become Inventory stock and AP-matchable receipt quantity. Rejected quantity needs no inventory or AP reversal because it was never recognised. | Existing receipt-inspection control; verify in Procurement UAT. |
| Internal inventory write-off or adjustment | Accepted stock is lost, damaged internally, obsolete, expired, or otherwise derecognised without an enforceable supplier claim. | Inventory owns the governed adjustment, evidence, quantity and valuation. Finance consumes the approved adjustment posting. The loss belongs to the organisation rather than the supplier. | Existing Inventory adjustment/write-off control; test separately from Return to Vendor. |
| Return to Vendor after acceptance | Accepted stock is later returned under warranty, latent-defect, recall, excess-delivery, supplier-agreed, or similar contractual rights. | Procurement retains supplier/PO/GRN/RMA and commercial-resolution lineage; Inventory owns outbound stock and actual carrying cost; Finance owns GRV/AP/tax, supplier credit/refund application, and GL posting. | **Planned. Producer workflow and end-to-end handoff are not yet delivered.** |

An inspection officer's earlier acceptance does not extinguish warranty rights or prove that a latent
defect cannot emerge later. Conversely, internal damage without a supplier claim must not be recorded
as a supplier return merely to recover the cost from AP.

## Ownership boundary

### Procurement

- owns the post-acceptance return request and business reason;
- preserves supplier, purchase order, GRN/accepted-receipt, item and original invoice references;
- records supplier RMA/authorisation, approval, dispatch evidence and commercial correspondence;
- records the eventual supplier resolution: credit note, refund, replacement, repair, rejection, or
  future credit; and
- publishes immutable, tenant-scoped, versioned events rather than creating Finance journals.

### Inventory

- validates warehouse/location, available quantity, lot, batch and serial identity;
- reserves and issues the exact returned units;
- reverses the authoritative inventory valuation layers at actual carrying cost;
- publishes the stock movement and valuation references used by Finance; and
- owns a compensating stock movement when a completed dispatch is corrected.

### Finance

- validates posting date, period, currency, tax and configured accounts;
- handles GRV accrual for uninvoiced goods or Return-to-Vendor clearing for invoiced goods;
- creates and applies the supplier debit/credit adjustment to the original AP exposure;
- records supplier refund, replacement, future-credit and tax consequences;
- posts only through the canonical Finance posting engine with idempotency and audit lineage; and
- owns Finance reversal/correction without rewriting Procurement or Inventory history.

Operational modules retain their source transactions and approvals. They must not insert Finance
journals, AP applications, tax records, or posting events directly.

## Why the integration has two stages

Physical dispatch and supplier commercial acceptance rarely occur at the same time. A return may
leave the warehouse today while the supplier issues a credit note, sends a replacement, disputes the
claim, or refunds cash later. Combining these moments would either remove inventory too late or reduce
AP before the supplier has accepted the claim.

### Stage 1: approved physical dispatch

The planned dispatch handoff carries the return identity, supplier/PO/GRN lineage, item and quantity,
warehouse/location and tracking identities, Inventory movement, actual carrying cost, dispatch date,
approval/evidence references, correlation identifier, contract version and idempotency key.

Typical accounting theory:

- accepted but uninvoiced goods: debit GRV accrual and credit Inventory at the governed return value,
  with an explicit variance where the costing policy requires it;
- invoiced goods awaiting supplier resolution: debit Return-to-Vendor clearing and credit Inventory at
  actual carrying cost; and
- rejected-before-acceptance goods: no reversal, because neither accepted stock nor AP eligibility was
  created.

The final account selection and variance treatment remain Finance policy. Inventory supplies quantity
and carrying cost; it does not choose the Finance journal.

### Stage 2: supplier commercial resolution

The planned resolution handoff carries the original return identity, outcome type, supplier document
and date, original invoice where applicable, commercial and tax amounts, currency/rate evidence,
supporting documents, correction lineage, contract version and idempotency key.

Finance then applies the result according to its substance:

- **credit note:** reduce the exact supplier invoice/open AP exposure, reverse recoverable input tax as
  required, and clear Return-to-Vendor clearing;
- **cash refund:** recognise and settle the supplier refund without manufacturing a fictitious AP
  invoice payment;
- **replacement:** retain the outbound return lineage and link the later replacement receipt/cost
  resolution;
- **repair or warranty service:** retain the goods/custody and commercial outcome without pretending a
  credit note exists; or
- **claim rejected or value differs:** use an approved variance, impairment/write-off, or correction
  path; do not silently change the dispatched quantity or carrying cost.

## Correction and idempotency rules

- Before dispatch, an authorised cancellation may close the request without stock or Finance effect.
- After dispatch, quantity or valuation errors require an Inventory compensating movement and linked
  Finance reversal/correction; history is not edited in place.
- A supplier resolution correction references the superseded resolution and reverses or compensates
  its AP/tax effect.
- Replaying the same producer event must return the original Finance result rather than post again.
- Event ordering must prevent commercial resolution from being applied to an unknown or ineligible
  return.

## Current UI and testing limitation

The existing Finance route `Finance > Accounts Payable > Supplier Returns`
(`/finance/ap/returns`) is now a **read-only historical register**. Its former create and one-click
approve/post mutations are quarantined at both UI and API boundaries. Mutation requests return HTTP
409 with code `FIN-INT-012-013-PLANNED` and explicitly confirm that no return, inventory movement,
supplier debit note, tax adjustment, AP application, or journal was created.

This quarantine is intentional: a Finance screen cannot manufacture the Procurement request/RMA,
authoritative Inventory outbound movement and valuation reversal, two-stage producer handoffs, AP
application, statutory tax adjustment, or complete correction lifecycle. Historical statuses and
legacy debit-note/journal references remain visible for audit only and are not proof that the agreed
post-acceptance Return-to-Vendor lifecycle occurred.

Until the producer work and Finance consumers are merged and verified:

1. demonstrate receipt-stage rejection only from the governed Procurement receipt-inspection screen;
2. demonstrate an internal write-off only from its governed Inventory adjustment workflow;
3. label post-acceptance Return to Vendor **Planned - not executable end to end** in stakeholder demos;
4. do not use a manual inventory adjustment plus a standalone Finance debit note as evidence that the
   integration works; and
5. do not certify AP ageing-to-control, inventory-to-GL, or tax reconciliation for this scenario.

End-to-end UAT can begin only after the contract catalogue entries are published, producer and consumer
contract tests pass, the Inventory movement and Finance journals reconcile, the supplier resolution is
applied to AP/tax, and correction/replay tests pass.
