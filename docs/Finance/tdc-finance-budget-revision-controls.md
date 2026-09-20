# TDC Finance Budget Revision Controls

## Purpose and requirement fit

This slice implements the Finance-owned part of TDC requirements FR-BG-007 and
FR-BG-011 through FR-BG-013. It provides controlled virements, reallocations and
supplementary budgets without changing Procurement, contract or project commitment
records.

An adopted budget is treated as an authority record. It is never edited in place.
An approved revision is applied by creating a locked successor `BudgetScenario`,
copying every approved return and entry, applying the authorized changes to that
copy, adopting the successor, and marking the former official scenario
`Superseded` in the same serializable transaction.

## Lifecycle

1. A Budget Officer raises a `Virement` or `Supplementary` request against the
   current `Approved` and `IsActive` scenario.
2. The request records its effective date, Board resolution reference/date,
   business justification and signed base-currency adjustment lines.
3. Submission revalidates the official source, tenant-owned accounts, fiscal-year
   periods, reporting segments and resulting non-negative balances.
4. The shared workflow routes the request to Finance Manager review and Managing
   Director final authority.
5. Workflow approval changes only the request status. It does not change budget
   balances.
6. A separately authorized user applies the request. The preparer cannot apply
   their own request.
7. Budget-versus-actual reporting automatically uses the new official scenario;
   the old and new versions remain available for comparison.

## Arithmetic policy

- Virements must contain at least one release and one increase and must net to
  exactly GHS 0.00 in the scenario base currency.
- Supplementary budgets contain positive increases only. A reduction must use the
  virement route so the source and destination authority are explicit.
- A revision cannot reduce any cost-centre/account/period cell below zero.
- A supplementary request can introduce a previously unbudgeted cell or reporting
  segment; its generated return is already Approved because its authority is the
  approved revision rather than a new departmental submission.

## Permissions

- `Finance.BudgetRevisions.Read`
- `Finance.BudgetRevisions.Write`
- `Finance.BudgetRevisions.Submit`
- `Finance.BudgetRevisions.Apply`

The seeded TDC default gives preparation/submission to Budget Officers, review to
Finance Managers, final workflow authority to the Managing Director, and Apply to
the Financial Controller through the complete Finance permission set. Workflow
assignment checks still govern who can decide each approval task.

## API and UI

- UI register: `/finance/budgeting/revisions`
- New request: `/finance/budgeting/revisions/new`
- Request detail: `/finance/budgeting/revisions/{id}`
- API base: `/api/Budget/revisions`
- Shared approval inbox: `/api/finance/approvals`

The request detail shows current, signed adjustment and revised values for every
budget cell. Once applied, it links directly to the resulting official scenario.

## Concurrency and audit

`BudgetRevision` and `BudgetScenario` use SQL row-version concurrency. Apply uses a
serializable database transaction and deliberately deactivates the old official
row before inserting the successor, respecting the filtered unique index that
allows one active scenario per tenant and fiscal year.

Finance audit events cover creation, editing, submission, approval, rejection and
application, plus the scenario supersession and adoption events. The request keeps
direct links to both source and result scenarios.

## Deliberate boundary

This slice changes Finance budget authority and Finance budget-versus-actual
reporting only. It does not claim Procurement requisition/PO/contract commitment
checks or project-budget consumption. Those integrations must consume the official
Finance scenario through separately agreed module contracts.

## Post-demo follow-up: revision dimension selector

**Disposition:** Deferred until after the demo. Do not treat the current selector
as the intended final budgeting model.

### Current behaviour

The New Budget Revision page labels its reporting-segment selector `Cost centre`,
but it does not currently restrict the choices to the source scenario's configured
budget-control dimension. The page loads every active segment structure marked
`IsReportingDimension`, fetches every active lookup value for those structures,
and flattens the values into one dropdown.

For a scenario configured with both `DEPT` and `PROJECT`, this produces a mixed
list such as:

- department values: `000`, `100`, `200`, and similar codes; and
- project values: `0000`, `P101`, `P102`, and similar codes.

Selecting any item stores its lookup-value ID in the revision line's single
`SegmentValueId`. The UI does not identify the owning dimension, and the backend
currently verifies tenant ownership but does not restrict the value to the source
scenario's control-dimension assignments. Consequently, a project value can be
recorded in a field presented to the user as a cost centre.

Relevant implementation:

- `frontend/src/app/finance/budgeting/revisions/new/page.tsx`
- `src/ErpSystem.Api/Services/Finance/Budget/BudgetService.Revisions.cs`

### Required design decision

Choose and implement one consistent model after the demo:

1. If revisions are controlled by one organizational dimension, restrict the
   selector to the source scenario's applicable control dimension (for example,
   `DEPT`) and label it with that dimension's configured name.
2. If budget cells may be controlled by multiple dimensions, replace the single
   `SegmentValueId` input with dimension-aware assignments and render a separate
   selector for each control dimension (for example, Department and Project).

The second option must preserve the full dimension grain used by the official
source budget so that the revised cell can be resolved without ambiguity.

### Acceptance criteria

- The available dimensions come from the selected official scenario's control
  assignments, not from all tenant-wide reporting dimensions.
- Each selector displays the configured dimension name and only values belonging
  to that dimension.
- Changing the source scenario refreshes and clears incompatible selections.
- A revision line resolves to exactly one existing source budget cell, except for
  a permitted supplementary line that intentionally introduces a new cell.
- The API rejects lookup values or dimension assignments outside the source
  scenario's configured control grain.
- Current, adjustment and revised figures are calculated at the complete budget
  dimension grain.
- Automated tests cover a scenario containing both `DEPT` and `PROJECT` and prove
  that their values cannot be mixed or mislabelled.
