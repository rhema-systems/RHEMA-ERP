# Finance Budget Control Foundation

## Scope of this slice

This foundation provides active budget control for Finance manual journals and certified direct
Accounts Payable expense invoices. Accounts Receivable, employee expenses, and Procurement
documents still require their own explicit adapters so their lifecycle semantics are not guessed
by the central posting engine.

Finance budget control applies only when all of the following are true:

- the GL account is an `Expense` account;
- `Account.BudgetTrackingEnabled` is `true`;
- an adopted Finance budget scenario is effective on the journal date;
- one approved Budget Return and Budget Entry match the exact fiscal period and full segmented
  `AccountId`; and
- the Budget Return department/cost-centre lookup matches a controlling lookup segment on that
  account combination. A general return is valid only when the account has no such segment.

Revenue accounts are deliberately outside enforcement in this foundation. A future revenue
adapter may provide warning-only control without changing expense enforcement.

## Lifecycle and accounting contract

Draft journals show a preview:

`available = adopted budget - posted expense actual - other active reservations`

All amounts are evaluated, reserved, approved, and displayed in the tenant's validated Finance
functional currency. The currency code is retained with the reservation and override evidence;
the UI does not infer it from a transaction currency or hard-code a symbol.

Submission reserves the requested net expense amount before the journal approval workflow is
started. A failure to start or advance the workflow releases the reservation. Withdrawal and
rejection also release it. Final approval and posting revalidate the canonical budget cell and
journal amount.

The central Finance posting engine consumes exact reservation IDs in the same database
transaction that creates the posting event and posts the journal. A reversal does not reserve
new expenditure: its credit to the expense account reduces posted actuals through normal GL
reporting while the original consumed reservation remains historical evidence.

Missing adoption, missing budget lines, dimensional mismatches, and ambiguous budget cells fail
closed and cannot be overridden. Only an insufficient available amount can request an override.

## Certified AP direct-expense adapter

A direct AP expense line on an `Expense` account with `BudgetTrackingEnabled` must select one
eligible adopted Finance Budget Entry before submission. The selected budget-cell ID is durable
source evidence on the vendor-invoice line; it is never inferred from a free-text category.

Submission reserves the exact line amount before the existing vendor-invoice approval workflow
starts. A workflow-start failure or final rejection releases the reservation. Final AP posting
revalidates the source version, consumes the exact reservation IDs inside the central posting
transaction, and stamps the budget cell's immutable Finance dimensions on the expense ledger
line. This prevents a committed journal without matching budget-consumption evidence.

The adapter deliberately excludes opening invoices, PO/GRV-backed invoices, inventory/product
lines, and fixed-asset lines. Opening invoices are cutover evidence, while PO/GRV-backed spend is
owned by the Procurement commitment lifecycle and must not be reserved a second time in AP.
Foreign-currency controlled lines require the exact approved Finance exchange-rate record.

## Transaction-dimension budget grain

An additive scenario-level control declares which Finance transaction dimensions define its
budget cells. Every entry in such a scenario carries one immutable Finance dimension-set value
for each declaration. A posted line consumes the cell only when its immutable transaction set
contains all of those controlling assignments; extra analytical values such as a project are
allowed. Actuals are no longer swept from unrelated dimension values on the same account.

Reservations retain the canonical set ID and combination-hash snapshot, and those assignments
participate in request/evaluation idempotency evidence. Scenarios with no declarations remain on
the legacy account/period/return-segment grain so existing adopted budgets are not reinterpreted.
Control declarations cannot change after worksheet entries exist; a changed grain requires a new
scenario version. Budget revisions copy the grain and existing entry assignments and fail closed
when an old revision line cannot identify one unique dimension-grained cell.

## Override evidence

A budget override is a separate `FinanceBudgetOverride` workflow entity. It records:

- the manual journal and immutable evaluation hash;
- the reason, requested amount, and shortfall;
- requester and request time;
- workflow instance;
- final approver and approval time, or rejection evidence.

An override is recognized only when the shared workflow is completed and the approved evidence
matches the journal evaluation. Editing the journal or changing its canonical budget cell
requires a new evaluation and, when applicable, a new override.

## Future module adapters

The following work is intentionally deferred:

- employee expenses: reserve at controlled submission/approval and consume when the resulting
  Finance posting commits;
- AP credit notes, voids, returns and other corrections: adjust or reverse budget evidence only
  through their governed accounting lifecycle;
- AR customer invoices: revenue budget warning only unless policy later changes;
- Procurement requisitions and purchase orders: reconcile the existing Procurement commitment
  ledger with the canonical Finance Budget Entry instead of replacing Procurement lifecycle
  controls;
- commitment release/reduction for PO closure, invoice matching, returns, cancellations, and
  supplier-return/debit-note corrections.

Procurement/Inventory ownership is unchanged. Any adapter must preserve its document workflow,
warehouse/item evidence, supplier-return controls, and existing segregation-of-duties rules.
