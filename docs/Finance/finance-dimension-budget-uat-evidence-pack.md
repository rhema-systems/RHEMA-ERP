# Finance dimension-budget UAT and evidence pack

## Purpose and release boundary

This pack certifies two separately gated Finance releases:

- **Phase A (merged):** the canonical transaction-dimension budget grain from PR `#114` and the
  dimension-aware Finance budget worksheet from PR `#115`;
- **Phase B (promotion pending):** direct Accounts Payable expense reservation and consumption,
  originally reviewed in PR `#116` and promoted to `master` by PR `#120`.

Phase A may be executed after the controlled deployment has applied migration
`20260825235055_AddFinanceBudgetControlDimensions` and rebuilt the API and frontend from a commit
containing PRs `#114` and `#115`. Do not execute Phase B until PR `#120` is in `master`, migration
`20260826013000_AddApVendorInvoiceBudgetEvidence` has been applied, and both applications have been
rebuilt from that later commit. Do not use production data for the first execution.

The pack does not certify Procurement commitments, employee expenses, supplier returns, standalone
supplier debit notes, or AP correction/reversal budget evidence. Those remain separate lifecycle
contracts.

Primary implementation anchors:

- `frontend/src/app/finance/budgeting/scenarios/page.tsx` — scenario control-dimension selection;
- `frontend/src/app/finance/budgeting/returns/[id]/page.tsx` — dimension-combination worksheets;
- `frontend/src/lib/finance/budget-dimension-grid.ts` — stable combination and effective-date rules;
- `frontend/src/app/finance/ap/invoices/create/page.tsx` — AP BudgetEntry selection;
- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs` — AP reservation lifecycle;
- `src/ErpSystem.Api/Services/Finance/Budget/FinanceBudgetCommitmentService.cs` — authoritative
  position, reservation, release, and consumption;
- `src/ErpSystem.Api/Services/Finance/GL/FinancePostingEngine.cs` — transaction-bound consumption.

## Required actors and evidence

Use distinct authenticated users where the configured workflow requires maker/checker separation.
Record usernames or user IDs, but never passwords or tokens.

| Actor | Minimum capability | Expected action |
| --- | --- | --- |
| Budget preparer | `Finance.Budgeting.Write` | Create the scenario, return, combinations, and cells |
| Budget reviewer/adopter | Tenant-configured Budget workflow permissions | Approve returns and adopt the scenario |
| AP preparer | AP invoice create/edit and workflow-submit permissions | Create and submit the direct expense invoice |
| AP reviewer/poster | Tenant-configured AP approval and posting permissions | Approve and post the invoice |
| Auditor | Finance read/report access | Capture read-only position, journal, and audit evidence |

Retain the following evidence for every case:

1. tenant code and actor identity;
2. scenario, return, budget-entry, invoice, reservation, posting-event, and journal IDs where applicable;
3. before/after screenshots of the relevant Finance UI;
4. API response or controlled database read showing lifecycle status and amounts;
5. the exact account, fiscal period, dimension combination, functional currency, and exchange-rate ID;
6. a pass/fail result and any controlled error code/message.

## Controlled fixture

Use one open fiscal period and one active, direct-posting expense account with
`BudgetTrackingEnabled = true`. Configure two active analytical dimensions, for example:

- `DEPT`: `FIN` and `OPS`;
- `PROJECT`: `P100` and `P200`.

The values selected for the positive case must be effective for the whole fiscal period. Prepare an
adopted budget cell for `DEPT=FIN + PROJECT=P100` with a functional-currency amount of `GHS 10,000`.
Do not reuse this cell for unrelated test cases unless the expected available balance is adjusted.

## Deployment gates

| Gate | Required evidence | Permitted execution |
| --- | --- | --- |
| Phase A source | PRs `#114` and `#115` are ancestors of the deployed commit | Worksheet cases `UAT-BUD-001` through `UAT-BUD-005` |
| Phase A schema | Migration history contains `20260825235055_AddFinanceBudgetControlDimensions` and the physical schema matches it | Worksheet persistence and reload |
| Phase B source | PR `#120` is an ancestor of the deployed commit | AP cases `UAT-APB-001` through `UAT-APB-008` |
| Phase B schema | Migration history contains `20260826013000_AddApVendorInvoiceBudgetEvidence` and `VendorInvoiceLineItem.BudgetEntryId` has its governed FK/indexes | AP selection, reservation, and posting |
| Runtime | API and frontend build identifiers match the recorded deployed commit | Any sign-off assertion |

Stop at the first failed gate. Never stamp migration history, edit budget evidence, or substitute a
different tenant/account/dimension combination to make the pack pass.

## Pre-UAT automated baseline — 27 August 2026

This preparation run used merged `master` commit `eacaf677` (PRs `#114` and `#115`) and made no
database or business-document changes.

| Verification | Result |
| --- | --- |
| `BudgetServiceHardeningTests` | Pass — 13/13 |
| `FinanceDimensionAdministrationServiceTests` | Pass — 8/8 |
| `FinanceBudgetCommitmentServiceTests` | Pass — 11/11 |
| `budget-dimension-grid.test.ts` | Pass — 6/6 |
| Phase A source gate | Pass — merged source present |
| Phase A schema/runtime gate | **Hold** — the configured environment reports three pending migrations; no migration was applied by this preparation run |
| Phase B source gate | Hold — PR `#120` is not yet in `master` |

Automated regression success is a prerequisite, not business UAT sign-off. The cases below still
require authenticated UI/API execution and retained evidence in the designated non-production
tenant.

Read-only migration inventory for the configured environment:

1. `20260825141500_AddFinanceControlledDocumentRetention` — pending;
2. `20260825190000_AddFinanceDimensionRuleScope` — pending;
3. `20260825235055_AddFinanceBudgetControlDimensions` — pending.

Phase A is therefore not executable yet. Apply the complete reviewed pending sequence through the
controlled deployment process, re-run the inventory and require zero pending migrations, then
rebuild/restart the API before beginning `UAT-BUD-001`. This preparation did not apply migrations,
start services, seed data, or create budget documents.

## Phase A — worksheet and immutable budget grain

### UAT-BUD-001: create a dimension-controlled scenario

1. Open `/finance/budgeting/scenarios`.
2. Create a scenario for the controlled fiscal year.
3. Select `DEPT` and `PROJECT` as budget-control dimensions.
4. Save and reopen the scenario.

Expected:

- the scenario displays both dimensions in configured display order;
- derived or inactive dimensions are not selectable;
- the persisted scenario returns the exact two immutable control-dimension IDs.

Evidence: scenario ID, screenshot of the selected grain, and scenario API response.

### UAT-BUD-002: reject incomplete and ineffective combinations

1. Open the assigned return at `/finance/budgeting/returns/{returnId}`.
2. Attempt to add a combination with only `DEPT=FIN`.
3. Attempt a complete combination containing an inactive value or one not effective for the full
   fiscal period.

Expected:

- the incomplete combination is blocked with “Select one value for every budget-control dimension”;
- an ineffective combination cannot be used to save an affected period cell;
- no incomplete budget entry is persisted.

Evidence: validation message and a read proving zero incomplete entries.

### UAT-BUD-003: save and reload independent dimensional cells

1. Add `DEPT=FIN + PROJECT=P100` and enter `GHS 10,000` for the controlled expense account/period.
2. Add `DEPT=OPS + PROJECT=P200` and enter a different amount for the same account/period.
3. Save, reload, and switch between both combinations.

Expected:

- each combination retains its own account/period amount and row version;
- selecting the same values in a different order selects the existing combination instead of
  creating a duplicate;
- readable labels show both dimension codes and values.

Evidence: both BudgetEntry IDs, combination hashes, assignments, and reload screenshots.

### UAT-BUD-004: preserve the legacy budget grain

Open a pre-existing scenario with no control dimensions.

Expected:

- the worksheet remains one account/fiscal-period grid;
- the UI labels it `Account and period (legacy)`;
- no dimension assignment is invented for an existing BudgetEntry.

### UAT-BUD-005: submit, approve, and adopt

Submit and approve the controlled return, then adopt the scenario using the configured workflow.

Expected:

- only an active, approved return in the adopted and unlocked scenario becomes eligible for budget
  control;
- the scenario’s control-dimension policy cannot be silently changed after entries exist;
- audit history identifies the actors and lifecycle timestamps.

## Phase B — direct AP expense control

**Hold point:** this phase is documentation-only until PR `#120` and its migration are deployed. A
successful Phase A result does not authorize or imply AP budget-control sign-off.

### UAT-APB-001: list only eligible budget cells

1. Open `/finance/ap/invoices/create`.
2. Create an ordinary, non-opening invoice with no purchase order.
3. Add an `Expense` line and select the controlled budget-tracked expense account.
4. Set the invoice date inside the controlled fiscal period.

Expected:

- the line loads selectable adopted Finance budget cells;
- each option identifies its fiscal period and dimension combination and shows approved, actual,
  reserved, and available functional-currency amounts;
- cells for another tenant, account, date, unapproved return, inactive scenario, or mismatched
  dimension combination are absent;
- changing the invoice date, account, opening-balance flag, or purchase-order context clears the
  prior BudgetEntry selection.

Evidence: request to `GET /api/ap/invoices/budget-cells`, response, and selected BudgetEntry ID.

### UAT-APB-002: fail closed without a required budget cell

Submit a direct expense invoice on the budget-tracked account without selecting a BudgetEntry, then
repeat with a stale or mismatched BudgetEntry ID.

Expected:

- submission fails before workflow starts;
- no active reservation, journal, or posting event is created;
- the controlled error distinguishes missing selection from account/period/dimension mismatch.

### UAT-APB-003: reserve on submission

Create a `GHS 2,000` direct expense invoice against the `GHS 10,000` cell and submit it.

Expected:

- the invoice becomes pending approval;
- exactly one active reservation exists for the invoice and BudgetEntry;
- reserved amount is `GHS 2,000`, available amount becomes `GHS 8,000`, and the idempotency key is
  stable;
- retrying submission does not create a second reservation or workflow.

### UAT-APB-004: reject and release

Reject the pending invoice through its configured workflow.

Expected:

- the reservation becomes `Released` with reason, actor, and timestamp;
- available amount returns to `GHS 10,000`;
- no journal or posted actual exists.

### UAT-APB-005: approve, post, and consume atomically

Submit a replacement `GHS 2,000` invoice, complete approval, and post it.

Expected:

- the reservation becomes `Consumed` with the exact journal and posting-event IDs;
- the expense AccountTransaction is `GHS 2,000` debit and carries the selected immutable Finance
  dimension-set ID;
- posted actual is `GHS 2,000`, active reservations are zero, and available amount is `GHS 8,000`;
- invoice, reservation operation, posting event, journal, and AccountTransaction commit together;
- a posting retry returns the existing event and does not create or consume another reservation.

### UAT-APB-006: enforce concurrency and availability

With only `GHS 8,000` available, submit two independent invoices whose combined functional amount
exceeds `GHS 8,000`.

Expected:

- serializable Finance budget control allows only amounts within the authoritative available
  balance;
- at least one request is rejected rather than oversubscribing the cell;
- no partial workflow or orphan reservation remains for the rejected request.

### UAT-APB-007: controlled foreign currency

Create a foreign-currency direct expense invoice whose exact approved exchange-rate snapshot converts
the line to `GHS 1,500`.

Expected:

- availability and reservation use `GHS 1,500`, not the transaction-currency amount;
- the exact ExchangeRate ID is retained and revalidated at submission and posting;
- the GL expense line retains transaction currency, functional amount, and the selected dimension set.

### UAT-APB-008: prove exclusions and ownership boundaries

Repeat the entry flow for each excluded source:

- opening AP invoice;
- PO/GRV-backed invoice;
- Inventory or Product line;
- direct fixed-asset line.

Expected:

- the direct-AP adapter creates no Finance budget reservation for these sources;
- existing opening, Procurement/GRV, Inventory, and fixed-asset controls remain authoritative;
- no source is counted twice merely because its invoice reaches AP.

## Release stop-lines

Do not sign off or work around any of the following:

- an incomplete or ineffective dimension combination is persisted;
- two entries occupy the same scenario/return/account/period/dimension hash;
- an unapproved or unlocked-ineligible cell appears in AP;
- a budget-tracked direct expense invoice submits without its exact BudgetEntry;
- rejection leaves an active reservation;
- posting succeeds without consumed reservation evidence or without the GL dimension set;
- duplicate submission/posting creates additional workflow, reservation, journal, or event rows;
- any excluded source acquires a direct-AP reservation;
- tenant, period, account, currency, or dimension evidence can be substituted client-side.

## Sign-off record

| Field | Value |
| --- | --- |
| Tenant | |
| Build/commit | |
| Phase A migration head | `20260825235055_AddFinanceBudgetControlDimensions` / Not applied |
| Phase B migration head | `20260826013000_AddApVendorInvoiceBudgetEvidence` / Not applied / Not required |
| Phase executed | A / B / Both |
| Scenario / return | |
| BudgetEntry / combination hash | |
| AP invoice | |
| Reservation / operation | |
| Posting event / journal | |
| Preparer | |
| Reviewer/poster | |
| Auditor | |
| Execution date | |
| Result | Pass / Fail |
| Evidence location | |
| Exceptions accepted | None |
