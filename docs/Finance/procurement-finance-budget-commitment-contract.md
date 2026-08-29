# FIN-INT-015 Procurement–Finance Budget Commitment Contract

- Version: 1.1
- Finance provider status: Available for development and UAT integration
- Procurement consumer status: Pending in the Procurement-owned module

## Purpose

Finance is the source of truth for the organisation's adopted budget. Procurement consumes
canonical Finance budget cells and reservations; it does not maintain an unrelated ceiling or
write Finance-owned evidence.

The callable in-process boundary is `IFinanceBudgetCommitmentService`. It is intentionally
module-neutral so other approved producer adapters can reuse the same Finance controls without
weakening Procurement ownership.

## Ownership boundary

Finance owns:

- adopted `BudgetScenario`, approved `BudgetReturn` and exact `BudgetEntry` selection;
- account, fiscal-period and department/cost-centre combination validation;
- functional currency and approved exchange-rate evidence;
- current availability, reservation state, idempotency operations and audit evidence;
- posted actuals derived from Finance journals and account transactions.

Procurement owns:

- requisition, PO, receipt, cancellation, return and amendment lifecycles;
- supplier, item, warehouse, quantity, acceptance and document approval evidence;
- deciding when its validated lifecycle calls Check, Reserve, Set, Release or posting outcome;
- persisting the returned Finance reservation IDs and versions on Procurement-owned evidence;
- its existing roles, permissions, workflow and segregation-of-duties controls.

Neither side writes the other module's tables directly.

## Canonical budget cell

The authority key is `BudgetEntryId`. Finance verifies that it belongs to the authenticated
tenant and to the adopted scenario effective for the budget date. It also verifies:

- approved budget return;
- exact fiscal period and expense account;
- active budget-tracked account;
- department/cost-centre segment agreement between the account combination and budget return;
- the complete structured transaction-dimension evidence required by the scenario's declared
  budget-control dimensions;
- Finance functional currency and effective approved exchange-rate record.

The read model returns scenario/return/entry IDs, fiscal year and period, account and segment,
the canonical dimension-set ID/hash and readable controlling assignments,
functional currency, approved amount, posted actual, active reservation and available amount.
Procurement may display these values but must not cache them as accounting authority.

## Operations

| Operation | Meaning | Required use |
|---|---|---|
| `GetEligibleBudgetCellsAsync` | Finance-controlled selectable cells | Populate controlled Procurement selections; no free-text budget line |
| `GetBudgetPositionAsync` | Current exact Finance position | Display/recheck only |
| `EvaluateAsync` | Availability check with no mutation | Draft planning and pre-submission feedback |
| `ReserveAsync` | Create one active commitment per source/cell | Approved requisition only |
| `SetReservationAmountAsync` | Set absolute remaining/desired amount | Amendment, reduction or approved increase |
| `ReleaseAsync` | Close remaining exposure | Rejection, cancellation or final unused balance |
| `ApplyPostingOutcomeAsync` | Link exact posted source type/ID/action/event and reduce/consume commitment | Finance adapter only, after it validates source lineage and the related posting commits |

Finance deliberately does not expose `ConvertCommitmentToActual` or `ReverseActual` as mutable
amount operations. A posted journal is the actual; a posted reversal changes the actual. The
posting-outcome operation only updates the commitment that remains after that accounting event.

## Required request evidence

Every evaluation/reservation request carries:

- stable source document type, ID, human reference and source version/evidence hash;
- budget date;
- unique stable source-line IDs;
- exact Finance budget entry, account, fiscal-period and segment IDs;
- structured Finance dimension definition/value IDs from the producer line. The budget cell's
  controlling assignments must be a subset; additional analytical dimensions are permitted;
- positive transaction amount and ISO currency;
- exact Finance exchange-rate ID for foreign currency;
- deterministic idempotency key and end-to-end correlation ID.

Tenant and acting user are resolved from the authenticated current-user context. Producer-sent
tenant or actor IDs are not accepted as authority.

## Lifecycle mapping

| Procurement event | Finance budget action |
|---|---|
| Draft requisition/plan | Evaluate only |
| Requisition approved | Reserve once |
| Requisition amended | Set absolute target using returned version |
| Requisition rejected/cancelled | Release with reason |
| PO issued from requisition | Reuse/inherit reservation; no second reserve |
| Accepted receipt posted | Reduce remaining reservation by the accepted/posted portion |
| Supplier invoice posted | No second reservation or actual; AP/GRNI posting determines accounting |
| PO closed/cancelled | Release final unused balance |
| Return/reversal | Post compensating accounting evidence, then set/release remaining commitment |

The Procurement adapter must define exact state-transition hooks and retain the returned
reservation ID/version. It must not infer successful mutation from a timeout; retry the same
idempotency key and payload.

Because a requisition reservation can later be reduced by a receipt posting, the reservation
source and posting source need not be the same document. Before calling the Finance-internal
posting-outcome method, the Finance adapter must validate the authoritative Procurement lineage
from requisition to PO to accepted receipt. The request then names the exact posted source
type/ID/action; Finance rejects a different event, action (including a reversal) or journal.

## Idempotency and concurrency

- Mutation idempotency keys are unique per tenant across the Finance commitment boundary.
- The same key and payload returns the recorded result; a changed payload fails with
  `BUDGET_IDEMPOTENCY_CONFLICT`.
- Finance serializes tenant reservation mutations and stores immutable operation evidence.
- Target-state changes require the current reservation version; stale callers must reload.
- One source document cannot silently create a different active reservation set. Changes use
  the target-state operation.

## Currency

Finance stores both transaction and functional amounts. Functional-currency requests use a
multiplier of 1 and no rate ID. Foreign-currency requests require the exact active, approved and
effective Finance exchange-rate record whose pair converts the transaction currency to the
Finance functional currency. Procurement must not send an unverified numeric rate as authority.

## Actuals and double-counting prevention

Available budget is:

`Approved functional amount - posted actual - active Finance reservations`

Posted actual is the debit-minus-credit movement on the exact expense account and fiscal period
from posted journals whose immutable dimension set contains every controlling assignment on the
budget entry. A transaction may carry additional analytical dimensions without escaping the cell.
Receipt/GRNI and supplier-invoice stages therefore follow their real GL
accounting. Procurement must not write an "actual" alongside a posted journal, and an AP invoice
that clears GRNI must not reserve or expense the same amount again.

## Failure rules

The Finance provider fails closed for missing/cross-tenant budget cells, mismatched account or
segment, non-adopted scenarios, ineffective rates, insufficient availability, stale versions,
idempotency conflicts, inactive reservations and posting events that do not match the exact
source document.

The producer retains its own document state when Finance rejects an operation. It must not mark
a requisition reserved, a commitment released or a receipt consumed until the Finance outcome
has been persisted and linked.

## Procurement implementation acceptance evidence

Before the Procurement adapter is considered integrated, its PR should prove:

- controlled selection from eligible Finance cells and no free-text budget authority;
- preservation of the selected controlling dimension assignments on every check/reservation call;
- draft check versus approved-requisition reserve;
- retry and changed-payload conflict behaviour;
- amendment target-state/version handling;
- no second reserve at PO issue;
- proportional receipt reduction only after posted Finance evidence;
- no duplicate actual/reservation at supplier invoice;
- cancellation, return and reversal compensation;
- foreign-currency rate lineage;
- tenant isolation, failure-state preservation and existing Procurement workflow/SOD behaviour.

Procurement/Inventory ownership remains unchanged. This contract does not authorise Finance to
mutate requisitions, purchase orders, receipts, returns, warehouse/item evidence or supplier
return controls.
