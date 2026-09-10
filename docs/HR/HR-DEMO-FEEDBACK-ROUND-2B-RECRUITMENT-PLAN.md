# HR demo feedback, round 2b — Recruitment: budget, establishment, requisition

> **Status: LANES R1 (36 ×2) and R2 (57 ×2, `hr-jobarch/run-r2.mjs`) DONE 2026-09-10; R3 next.** Source: the feedback document *HR Demo Meetings —
> Changes and Additions 2* (2 pages, section RECRUITMENT), brought by the user on 2026-09-10 after
> the round-2 HR demo, plus one follow-up from the user the same day (Finance must approve and
> process requisition costs). Every bullet of the document is accounted for in § 2 — as a build
> item, a decision, a default, or a record. If a bullet is missing, that is an error in this
> document.
>
> **Vetting block.** Every "what exists" claim was verified against the working tree on
> 2026-09-10 (branch `hrdev`, head `231c74e2`) by five read-only code surveys plus direct reads.
> The area-17 build plan (`plans/HR-Area-17-Job-Architecture-Competency-Establishment-Build-Plan.md`,
> slices 7, 8, 12) is the ground truth for what the budget and the establishment already do; this
> document does not restate it. Read § 1 (decisions) before § 4 (the plan).
>
> **Companions.** `HR-DEMO-FEEDBACK-ROUND-2-PLAN.md` (round 2 proper — this is its lane R),
> `../HR-FINANCE-INTEGRATION-BACKLOG.md` (the deferral rule R7/R8 obey),
> `HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`, `HR-WORKFLOW-ENGINE-INTEGRATION.md`,
> `../HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md` (the ask R8 waits on).

---

## 0. How to read this document

| Section | What it holds |
|---|---|
| § 1 | The decisions the user made on 2026-09-10, the defaults I took, and why the estate pattern is only half applicable |
| § 2 | The register: every PDF bullet, what exists (file:line), the gap, the slice |
| § 3 | Defects the survey found, each fixed inside a slice and asserted |
| § 4 | The build plan: nine slices, sequenced, with harness locations, and the per-slice log |
| § 5 | Design notes |
| § 6 | Records owed to other documents, and the ask to the Finance owner |
| § 7 | Verification |
| § 8 | Open questions, each with a default |

House rules that bind every slice: the user runs `dotnet build` and scaffolds migrations (the
agent edits them and lists them in `FastBuildMigrationMetadata`); the agent stages, the user
commits; every slice's harness (`D:\Rhema\TDC ERPS\dev-harness\`) is green twice in Staging with
the JWT key; every new column a user can fill is in `demo-coverage-manifest.csv` with seeding.

---

## 1. Decisions (2026-09-10)

| # | Decision | Effect |
|---|---|---|
| D-1 | **Line salary from the scale, amount editable.** | `ManpowerBudgetLine` gains `SalaryGradeId/SalaryLevelId/SalaryNotchId` (nullable), pre-selected from `EmployeePosition.SalaryGradeId`. Amount fills from notch → level midpoint → grade minimum; stays editable; the source is stored. R3. |
| D-2 | **The approver sees a snapshot at submit and the live figure beside it.** | Five snapshot columns on `StaffRequisition` stamped at submit; the detail card shows both and flags drift. R5. |
| D-3 | **Establishment → budget: in-app first, then Excel.** | R4a (in-app "Plan a budget from the establishment"), R4b (xlsx export/import onto a draft budget). |
| D-4 | **A requisition with no approved budget is allowed only when `BudgetEnforcementMode ≠ Block`.** | Under Block an unlinked requisition is refused at submit/approve with a sentence; under Warn/Off it passes with a required exception justification. R5. |
| D-10 | **Finance on requisition costs: master data now, AP hand-off after the Finance owner signs.** | Currency from `HrCurrenciesController`, rate through `HrCurrencyBridge`, payee = Procurement `Supplier` through a new HR read door, an HR approve step on the cost, voucher number stays a typed record (R7). The AP hand-off — HR-approved cost → Finance vendor invoice → Finance approves and pays → HR reads the voucher back — is raised with the Finance owner as a new FIN-INT row and built as R8 only after they answer. Keeps the 2026-08-17 deferral (`HR-FINANCE-INTEGRATION-BACKLOG.md:24-35`) and the 2026-08-31 governance gate. |

**Why the estate pattern is only half applicable.** Estate has two Finance seams. Ground rent,
facilities and property management bill tenants through `IInvoiceService` + `IPaymentService` —
**receivables**, the wrong shape for recruitment spend. Land acquisition pays vendors through
`IVendorInvoiceService` (`LandAcquisitionsController.cs:1989-2016`, `SubmitForApprovalAsync :2242`,
status pulled back `:2308-2419`) — **payables**, the right shape. Quantity Survey's payment
certificate uses the same door with a cleaner back-reference model
(`ProjectManagementEntities.cs:1612, 1700-1703`: `VendorInvoiceId`, `ApHandoffStatus {NotReady,
Ready, Created, Failed}`, `ApHandoffAt`, `ApHandoffFailure`, `PaymentStatusSnapshot`), and that is
the template R8 copies. Neither precedent has a catalogue row: FIN-INT-016's declared producer is
Finance AP itself, and `FinanceDimensionRouteCatalog.cs:64-76` says *external producers using the
same service require their own route* — which is the ask.

**Defaults taken** (overridable; also in § 8):

- **D-5** Cost validation: requisition costs are validated against the linked budget's
  `RecruitmentBudget`, aggregated across every requisition linked to that budget, under the same
  Warn/Block mode. The vacancy half is a no-op — `JobVacancy` carries no cost fields at all (only
  the advertised `SalaryRangeMin/Max`), so "vacancy costs within requisition costs" has nothing to
  validate. `ManpowerBudget.ActualSpent` stays unwritten (the Finance backlog's decision); the
  budget shows *committed recruitment spend* as a read instead.
- **D-6** A unit's "current headcount" is the **subtree** (unit + descendants, walked by
  `ParentUnitId` because seeded `Path` is empty), with one serving predicate:
  `!IsDeleted && IsActive && StaffStatus ∉ {Terminated, Retired, Inactive}`. Three different
  predicates exist today (§ 3); the new reads use this one, the old three are left and recorded.
- **D-7** "Exits due" for a budget period = retirements due (`HrPolicyCalculations.RetirementDate`)
  + contract expiries due + separations in flight, within `PeriodStartDate..PeriodEndDate`, for
  the subtree. Pre-fills `PlannedTerminations` (editable). Terminations not yet raised cannot be
  known; the screen says so.
- **D-8** A budget line's drawdown = posts on requisitions linked to that line that are not
  Cancelled/Rejected. The budget check reads `remaining = PlannedNewPositions − drawdown` instead
  of the typed `CurrentFilled`.
- **D-9** The payroll budget (PDF: "IN PHASE 2") is recorded, not built.

---

## 2. Register — every PDF bullet

| PDF bullet | What exists (file:line) | Gap | Slice |
|---|---|---|---|
| Retirements ↔ recruitment budget; see who retires in the year; does the budget cover retirements/terminations | `SeparationService.GetUpcomingRetirementsForTenantAsync` (`SeparationService.cs:1093`), `GET api/hr/separations/retirements/upcoming?withinDays=`; `HrPolicyCalculations.RetirementDate` (`HrPolicyCalculations.cs:37`); `ManpowerBudget.PlannedTerminations` (dead, no reader) | `UpcomingRetirementDto` has names only, no `OrganizationUnitId/PositionId`; nothing joins it to the budget | **R2** |
| Org unit → level cascading dropdown on the budget form | `OrganizationUnitPicker.tsx` (lane B1) exists; `manpower-budgets/new/page.tsx:101-117` was a flat Select; `ManpowerBudget.OrganizationLevelId` exists but the form never sent it | Swap; capture the level | **R1** |
| Which form values can be auto-specified (current headcount etc.) | `CurrentHeadcount`, `CurrentSalaryCost` typed by hand; `OrganizationStructureRepositories.cs:360` counts leavers | Planning-baseline read; pre-fill headcount, salary cost, exits, period | **R2** |
| Create-form details don't show in edit / "Correct" | `[id]/page.tsx:449-552` Correct dialog: 12 inputs; omitted `fiscalYear`, unit, `plannedPromotions`, `plannedTransfers`, the justification textarea; `UpdateEntity` (`JobAnalysisMappingExtensions.cs:558-573`) assigned all → **a correction zeroed promotions/transfers/actualSpent**. `UpdateManpowerBudgetDto` (`JobAnalysisDTOs.cs:832`) lacked year/unit/level. The detail rendered 4 tiles of ~20 fields. Correct was hidden on Rejected — and a Rejected budget could be neither resubmitted nor deleted | Full edit form; DTO parity; detail shows everything; Rejected is correctable and resubmittable | **R1** |
| Budget line salary from the scale; selecting a position shows its salary | `PlannedAverageSalary` typed (`[id]/page.tsx:399-405`); `EmployeePosition.SalaryGradeId` (`HREntities.cs:974`) read by nobody in HR; the scale is `SalaryGrade/Level/Notch` (`SalaryEntities.cs`), cascade UI inline in `SalaryAssignmentsTab.tsx:164-247` | D-1 | **R3** |
| Payroll budget (phase 2) | — | Record only | D-9 |
| Use the establishment to initiate budget creation; export to Excel, edit, import back | `GET api/position-vacancies/establishment` (`PositionVacancyRepository.cs:65-132`); the establishment screens have no action rows and no export; ClosedXML toolkit in `EmployeeImportWorkbooks.cs`; no HR .xlsx export exists yet | D-3 | **R4a / R4b** |
| Gaps → budget → requisitions; raise without budget?; approver must know; justify when no budget or no gap; establishment as at the time of the requisition | `StaffRequisition.IsBudgeted` (self-declared), `BudgetCode` (free text, **zero readers**); no `ManpowerBudgetLineId`; `BuildBudgetCheckAsync` (`StaffRequisitionService.cs:883`) matches by position + calendar year and reads the typed `CurrentFilled`; `CheckEstablishmentAsync` (`:855`) is **never surfaced** to the panel; `BudgetCheckPanel` only on Draft/Rejected/Submitted (`requisitions/[id]/page.tsx:238`) | D-2, D-4, D-8 | **R5** |
| Flag set dynamically; budget code auto; hide the textbox | as above | `IsBudgeted` derived from the link; `BudgetCode` = budget number snapshot; the textbox becomes a budget-line picker | **R5** |
| Validate requisition costs within the budget, vacancy costs within requisition costs | `StaffRequisitionCost` totals compared to nothing (`AddCostAsync :605`); `JobVacancy` has no costs | D-5 | **R6** |
| *(user follow-up)* Finance approves and processes requisition costs; voucher details; currencies and rates from Finance; the estate invoice integration as the model | `StaffRequisitionCost.Currency` free text, `ExchangeRate` typed (`StaffRequisitionDTOs.cs:401/427`) — **the last caller-supplied rate in HR** (entity sweep row 68, decision 14, sequencing 1b); `PaymentVoucherNumber` free text, no readers; no payee; no status; `HrCurrencyBridge` (`HrCurrencyBridge.cs:35`) used by five areas but not here; `RequisitionCostsPanel.tsx:242-259` bare inputs; no HR read door for `Supplier` | D-10 | **R7** now, **R8** on the Finance owner's answer |

---

## 3. Defects the survey found (fixed inside the slices, each asserted)

1. **A correction zeroed three columns** (`plannedPromotions`, `plannedTransfers`, `actualSpent`) because the dialog omitted them and the PUT is a replace. R1.
2. **The establishment gap read ignores `EstablishmentApprovedOn`** and floors `ExpectedHeadcount` at 1 (`PositionVacancyRepository.cs:117`), so ~132 of 146 positions show a phantom gap of 0/1 — the opposite of the rule every enforcement path uses (`StaffRequisitionService.cs:868`). R4a.
3. **Fiscal year = calendar year** in `BuildBudgetCheckAsync :888` while `CompanyHrPolicySettings.FiscalYearStartMonth` exists. R5.
4. **`ManpowerBudgetStatus.Active/Closed` are never written** but `Active` is preferred by the check (`:917`). Not fixed; recorded.
5. **The organisation-unit headcount reads count leavers** (`OrganizationStructureRepositories.cs:360-364`). Not rewritten (organogram/detail consumers); recorded; the new reads use D-6.
6. **`rejectBudgetOnWorkflow` sent the constant `'Not approved'`** (`[id]/page.tsx:219`) — the reason column area 17 added was never filled from the screen. R1.
7. **Withdraw establishment has no confirmation and a hard-coded reason** (`administration/hr/establishment/page.tsx:112`). R4a.
8. **The admin establishment page is N+1** (`:65-74`). R4a.
9. **`RequisitionCostsPanel.tsx:39` seeds `category: 'Advertising'`**, not one of the ten `StaffRequisitionCostCategory` names. R7.
10. **`HrCurrencyBridge.cs:90-104` still says Finance's conversion is inverted.** Defect #2 was resolved by Finance PR #99 (`CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md:26`). R7's harness asserts the direction before trusting the bridge; then the comment is corrected.
11. **`GetTotalCostAsync :632` sums `Amount × ExchangeRate` with the typed rate.** R7 makes the rate server-derived first; R6 follows R7 for that reason.
12. *(found while building R1)* **A budget could be corrected while out for approval.** `UpdateAsync` refused only `Approved`, so a Submitted budget — the one three approvers are reading — was still editable by its author. Now Draft or Rejected only. R1.
13. *(found while building R1)* **A Rejected budget was stuck.** Submit admitted Draft only and delete admitted Draft only, so a rejected budget could be neither corrected-and-resubmitted nor removed. R1.

---

## 4. The build plan

Order: **R1 → R2 → R3 → R4a → R5 → R7 → R6 → R4b**, with **R8 on its trigger** (the Finance
owner's written answer to § 6a). R7 before R6 because R6 compares in base currency and must use
the server-derived rate. R4b last because it is the largest and depends on R4a's line builder.
**The § 6a ask is written when R1 starts**, so the answer has time to arrive.

Harness folders: `dev-harness/hr-jobarch/run-r1.mjs … run-r4.mjs` (budget/establishment),
`dev-harness/hr-recruitment/run-r5.mjs`, `run-r6.mjs`, `run-r7.mjs` (requisition). Regression
after each: `hr-jobarch` slices 7, 8, 12-ui; `hr-recruitment/run.mjs`. ⚠ `hr-jobarch/run-slice0.mjs`
and the `hr-probation` slices already crash on workflow-engine 409s (environmental, recorded) —
not a regression signal.

### R1 — The budget form, edit parity, cascading unit · no migration · ✅ **DONE 2026-09-10** · 36 ×2

- [x] `UpdateManpowerBudgetDto` gains `FiscalYear`, `OrganizationLevelId`, `OrganizationUnitId` — all optional, **null means unchanged**, so callers written before R1 keep working; `ActualSpent` removed from it (an old client still sending it is ignored).
- [x] `UpdateEntity` stops assigning `ActualSpent`; assigns the scope when supplied.
- [x] `ManpowerBudgetService.UpdateAsync`: **Draft or Rejected only** (defect 12); the unit is checked to exist in the tenant, the level follows the unit unless named; `Variance = TotalBudget − stored ActualSpent`; the response is re-read so a scope change answers with the new unit's name. `CreateAsync` applies the same unit/level rule and re-reads (its response used to carry no unit name at all — the tracked entity had no navigations).
- [x] `SubmitForApprovalAsync` admits **Draft or Rejected** (defect 13).
- [x] `components/hr/manpower/ManpowerBudgetFormFields.tsx` — one form, two hosts: unit via `OrganizationUnitPicker` with the **level captured** (from the unit summary or the level callback), year, period, six headcount figures, six cost figures + computed total, justification.
- [x] `manpower-budgets/new/page.tsx` rewritten onto it; `manpower-budgets/[id]/edit/page.tsx` (new) seeds from the budget, editable on Draft/Rejected, shows the rejection reason while it is being fixed.
- [x] `manpower-budgets/[id]/page.tsx`: the Correct dialog is gone; **Edit** on Draft/Rejected; **Submit / Resubmit** on Draft/Rejected; **Reject prompts for a reason** (defect 6); four cards render every field (Scope, Headcount, Money, Justification and decision). `actualSpent`/`variance` still deliberately not shown.
- [x] Harness `hr-jobarch/run-r1.mjs`: **36 ×2**, green on two consecutive runs.
- [x] Regression: `run-slice8` 40, `run-slice12-ui` 41, `run-slice7` 47 after one harness correction (below; it was 49, and the two dropped are the two "says why" sub-assertions).
- [x] Round-2 plan § 6.1.3: `manpower-budgets/new/page.tsx:110` struck from the B3 sweep list.

**What the build changed from the plan, and what it found.**

1. **The scope fields on the update DTO are optional, null = unchanged**, rather than required as
   the create DTO has them. The update is otherwise a replace; making the scope required would have
   400'd every harness and client written before R1 (slices 9 and 12, the tier-B probe), and the
   form always sends all three anyway. Asserted both ways: an edit naming a new unit moves the
   budget, an edit naming no scope leaves it alone.
2. **Two defects the plan did not list** (§ 3 items 12 and 13): a Submitted budget was correctable
   while its approvers were reading it, and a Rejected budget could be neither resubmitted nor
   deleted. Both fixed here; both asserted.
3. **The create response carried no unit name.** `CreateAsync` mapped the entity it had just built
   from the DTO, with no navigations loaded — the lane F2 shape. Both create and update now re-read.
4. ⚠ **Slice 7's regression run had two red "says why" assertions that were not R1's.** The
   engine's "not assigned as an approver" no longer reaches the client: master's #38 ("Harden …
   exception handling", 2026-08-07, merged into hrdev after slice 7 was last green on 2026-08-19)
   makes `GlobalExceptionHandlingMiddleware` answer every `UnauthorizedAccessException` with the
   fixed sentence "You do not have permission to access this resource." A 403 that does not
   explain itself is the hardening's point, so the harness now asserts the status only (47 green, two sub-assertions fewer).
   **Any other HR harness asserting text on a 403 will fail the same way** — expect it, do not
   "fix" the middleware.
5. **Not browser-walked.** The forms type-check clean under `tsconfig.hr-slice.json` (the 33 errors
   left are pre-existing, in `medical/ClinicalRecordActions.tsx` and `types/finance.ts`) and the
   harness proves the API; nobody has clicked through the new edit page yet.

### R2 — The planning baseline: headcount, salary cost, exits due · no migration · ✅ **DONE 2026-09-10** · 57 ×2

Built as specified below, with these deviations and findings:

- [x] `GET api/JobAnalysis/budgets/planning-baseline` (`ManpowerBudgetReadPolicy`), `ManpowerPlanningBaselineDto` + exit and position rows, `HrServingEmployees` (new file, its remark names the three older predicates), `HrBasicPay.ResolveForPlanning` (= `Resolve` with no payroll figure), `UpcomingRetirementDto.OrganizationUnitId/PositionId`.
- [x] `PlanningBaselinePanel.tsx`; the form fetches the baseline once unit + period are set and pre-fills headcount, salary cost and planned terminations — **automatically once per result on the create page, by button on the edit page** (never over something typed); each of the three fields carries a "System: …" hint. The detail page hosts the panel live, "as of today", with drift badges against the budget's own typed headcount and terminations.
- [x] Period defaults follow `CompanyHrPolicySettings.FiscalYearStartMonth` (`fiscalPeriodFor` in the form module; a fiscal year is labelled by the year it starts in).
- [x] Harness `run-r2.mjs`: own structure + two levels + two units; one exit of each kind; an established and an unestablished post; the child unit alone; a quiet period; refusals; the retirement read's new ids. Regression after: R1 36, slice 7 47, slice 8 40, slice 12 41.

**What the build changed from the plan, and what it found.**

1. **The subtree is walked in memory** from one read of the tenant's units, not through the repository's recursive `GetDescendantsAsync` (a query per node).
2. **A separation in flight counts whatever the period** — it is happening now, so it is an exit the plan must expect; the harness's "quiet period" asserts exactly that. Retirements and contract ends already PAST for someone still on strength are included and flagged `isOverdue` (a backlog, not a projection), the same reading the separation screen takes.
3. **`SuggestedPlannedTerminations` is the distinct-people count** across the three lists — one person can be both retiring and under a separation.
4. ⚠ **A position's `OrganizationUnitId` is a non-nullable `Guid`; an employee's is `Guid?`.** The first cut null-checked both and the build refused the position side. Two shapes, deliberately, in the same query.
5. ⚠ **Patch-script trap, twice in one slice:** an insert whose block ends with its own anchor doubles the anchor — a doubled class header (build error) and a doubled interface (type error). The helper now strips it. **Read the join point of every insert before building.**
6. **Not browser-walked.** The panel and the pre-fill type-check clean; nobody has watched the create form fill itself yet.

The plan as written:

`GET api/JobAnalysis/budgets/planning-baseline?organizationUnitId=&periodStart=&periodEnd=` →
`ManpowerPlanningBaselineDto`: unit + subtree ids; `CurrentHeadcount` (D-6); `CurrentSalaryCost`
(notch → level midpoint → `Employee.Salary`, via a pure `HrBasicPay.ResolveForPlanning`; an
estimate, and labelled as one); `RetirementsDue[]`, `ContractExpiriesDue[]`, `SeparationsInFlight[]`
(each with employee, position, unit, date, "already has a separation"); `ExitsDueTotal`;
`Positions[]` (filled, expected, `IsEstablished`, gap — null when not established — exits due,
suggested new hires). Reuses `HrPolicyCalculations`; `UpcomingRetirementDto` gains
`OrganizationUnitId`/`PositionId` (additive). One `HrServingEmployees.Predicate` beside
`HrBasicPay`. Form pre-fills headcount, salary cost and terminations with "from the system" hints
and a re-fetch; `PlanningBaselinePanel.tsx` on the form and as a detail tab. Harness
`run-r2.mjs` with its own unit tree (never a second root).

### R3 — Salary from the scale on the budget line · migration `AddManpowerBudgetLineSalaryScale`

`ManpowerBudgetLine.SalaryGradeId/SalaryLevelId/SalaryNotchId` (Guid?, Restrict, one-way navs) +
`PlannedSalarySource {Notch=1, LevelMidpoint=2, GradeMinimum=3, Manual=4}` (⚠ migration default
`4`, not the scaffolded 0). Service validates grade ∈ tenant, level ∈ grade, notch ∈ level; fills
the amount from the scale when omitted and stamps the source, `Manual` when supplied.
`GET api/JobAnalysis/positions/{positionId}/salary-reference`. `SalaryScalePicker.tsx` extracted
from `SalaryAssignmentsTab.tsx:164-247` (the tab consumes it). Line dialog also exposes quarter,
target fill date, priority, critical, notes. ⚠ Check how many live positions carry a
`SalaryGradeId` before promising a pre-select.

### R4a — Plan a budget from the establishment · no migration

`PositionEstablishmentDto` gains `IsEstablished`, `EstablishmentApprovedOn`,
`EstablishmentSourceBudgetNumber`; the repository stops flooring to 1; `VacantCount` is a gap only
for established posts; reconcile opens vacancies only for established posts (check the live
`PositionVacancies` table first). `POST api/JobAnalysis/budgets/from-establishment` creates a
Draft with one line per position in the subtree (409 on an existing Draft/Submitted for the unit
+ year). `GET api/JobAnalysis/establishment` list read replaces the admin page's N+1. Establishment
screen: unit picker, "not established" rendered as such, exits-due column, **Plan a budget**
button; admin screen: confirmation + reason on withdraw; budget detail: "Refresh lines from the
establishment" (Draft only, never overwrites). Harness `run-r4.mjs`.

### R5 — The requisition draws down from the budget · migration `AddStaffRequisitionBudgetLink`

`StaffRequisition.ManpowerBudgetLineId` (Restrict), `ExceptionJustification`, five establishment
snapshot columns stamped at submit (D-2). `IsBudgeted` derived; `BudgetCode` = budget number;
both leave the write DTOs. Validation: line's position = requisition's position; budget
Approved/Active; year covers the desired start. `BuildBudgetCheckAsync`: linked line first, else
position + **policy fiscal year**; `ProjectedHeadcount = drawdown + requested` (D-8);
`RequisitionBudgetCheckDto` gains the link, drawdown, remaining, an `Establishment` block
(surfacing `CheckEstablishmentAsync`), `ExceptionRequired`/`ExceptionReason`. D-4 in
`EnforceBudgetAsync`. `GET budgets/lines/for-position/{id}?fiscalYear=`; `POST lines/{id}/raise-requisition`;
`ManpowerBudgetLineDto.RequisitionedCount/Remaining`. Form: budget-line picker replaces the
checkbox + textbox; exception textarea when needed; `BudgetCheckPanel` at every status with a
preview endpoint on the create form (Q-R3); detail "Budget and establishment" card (snapshot vs
live, drift badge). Harness `hr-recruitment/run-r5.mjs`; policy flipped and **restored in `finally`**.

### R6 — Requisition costs within the budget · no migration

Approved costs (Q-R5) aggregated across every requisition on the budget vs
`ManpowerBudget.RecruitmentBudget`, Warn/Block per the mode; `GET budgets/{id}/recruitment-spend`;
`RequisitionCostsPanel` shows the figure and wires the existing `updateCost`. Vacancy half:
recorded in the Finance backlog, not built. Harness `run-r6.mjs`.

### R7 — Requisition costs read Finance's master data; HR approves a cost · migration `AddStaffRequisitionCostPayeeAndApproval`

`StaffRequisitionCost.SupplierId` (→ Procurement `Supplier`, Restrict) + `PayeeName` snapshot;
`Status {Recorded=1, Approved=2, Rejected=3}` (migration default 1), `ApprovedById/On/Note`;
`AmountBaseCurrency` stored; `CostDate`. Write DTOs lose `ExchangeRate`; `Currency` validated and
the rate resolved by `HrCurrencyBridge`. Approved costs immutable except voucher/description.
`POST costs/{id}/approve|reject` (two-actor rule). New `HrSuppliersController` read door on the
`HrCurrenciesController` precedent. Panel: currency `Select` from `/hr/currencies`, rate input
removed, `SupplierPicker`, status badge, approve/reject; defect 9 fixed. **These are HR facts
about HR's own approval, not a payment-status machine**; `PaymentVoucherNumber` stays the typed
record until R8. Harness `run-r7.mjs` asserts the rate direction against Finance's own read.

### R8 — AP hand-off · **TRIGGERED by the Finance owner's answer**, not scheduled · migration `AddStaffRequisitionCostApHandoff`

Source event: `StaffRequisitionCost.Status = Approved`. Adapter `StaffRequisitionCostApHandoffService`
→ `IVendorInvoiceService.CreateAsync` (supplier required, `ExchangeRateId` not a number, one
expense line on `CompanyHrPolicySettings.RecruitmentExpenseAccountId`, `Reference =
"HR-REQ-COST:{costId:N}"`, idempotent by reference) → `SubmitForApprovalAsync`. Back-references on
the cost in QS's shape (`VendorInvoiceId/Number`, `ApHandoffStatus`, `ApHandoffAt/Failure`,
`PaymentStatusSnapshot`); `PaymentVoucherNumber` written from `VendorPayment.PaymentNumber` on
refresh; status sync is **pull**. Consumer-contract tests + the CI gate entry; catalogue row
**FIN-INT-017** added with the Finance owner's sign-off. The first HR AP adapter and the template
for medical, travel and separation.

### R4b — Excel round-trip of the establishment onto a draft budget · no migration

`ManpowerBudgetWorkbooks.cs` on the `EmployeeImportWorkbooks.cs` idiom (ClosedXML, column-level
number formats). `GET budgets/{id}/establishment-workbook` (Draft only): *Lines* sheet, one row
per position with the editable planned columns, *Lists*, *Read Me*, hidden `_meta`. `POST`
multipart (Draft only, `.xlsx`, 15 MB, **`SpreadsheetSecurityInspector` runs** — the employee
importer skips it, this one must not); `_meta` must match; **all-or-nothing**. Harness `run-r4b.mjs`.

---

## 5. Design notes

- **Subtree, one predicate, walk `ParentUnitId`** (D-6). `HrServingEmployees` beside `HrBasicPay`,
  its remark naming the three older predicates and why they were left.
- **Budgeted is a fact about a link, not a claim.** `IsBudgeted`/`BudgetCode` stay as columns (the
  workflow routing context and list screens read them) but only the service writes them. The
  routing context gains `manpowerBudgetNumber`, `hasBudgetLine`, `exceptionRequired`.
- **Snapshot vs live** on the requisition: the snapshot is the record; the live block is a
  courtesy. Drift is shown, never used to refuse.
- **Drawdown** counts posts requested, not hires made; `PositionsFilled` stays separate.
- **Fiscal year:** one `HrFiscalYear.For(date, settings)` used by R2, R4a and R5.
- **Scope on the update DTO is optional and null means unchanged** (R1) — the rest of the DTO is
  a replace. The form always sends all three, so from the screen the distinction is invisible; it
  exists for the harnesses and clients written before R1.
- **Excel round-trip is all-or-nothing and Draft-only.**
- **Finance boundary:** HR reads currency, rate and supplier canonically (backlog rule 4); HR
  approves its own cost (governance: producers own approvals); HR creates no journal, invoice or
  payment until R8, and R8 goes through Finance's own AP service with Finance's sign-off. No
  HR-side payment status exists at any point.

---

## 6. Records owed to other documents

- `HR-DEMO-FEEDBACK-ROUND-2-PLAN.md` § 5: pointer to this document (done with this commit); § 6.1.3: strike `manpower-budgets/new/page.tsx:110` when R1 lands.
- `../HR-FINISH-PLAN.md`: payroll budget (phase 2, D-9); `ManpowerBudgetStatus.Active/Closed` never written; unit headcount counts leavers; vacancy has no costs.
- `../HR-FINANCE-INTEGRATION-BACKLOG.md`: a new area-6 section (there is none — recruitment appears only as the `RecruitmentBudget` overlap at `:205`): the cost's money event, the HR approve step, the R8 design, D-5. Area 17/18: recruitment spend becomes a computed read; `ActualSpent` still Finance's.
- `HR-FINANCE-ENTITY-SWEEP.md` row 68 + decision 14 + sequencing 1b: closed by R7. `HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md:107-110`: no violation left. `HR-CROSS-MODULE-PATTERNS-SWEEP.md` rows 4, 22, § 4.1.
- `HR-MODULE-INTEGRATION-MAP.md`: HR → Procurement `Supplier` (read-only payee); after R8, HR → Finance AP.
- `HR-IMPORT-EXPORT-CATALOGUE.md:103`: establishment export/import ✅ after R4b.
- `dev-harness/hr-demo-smoke/demo-coverage-manifest.csv`: columns only; R3/R5/R7 columns need the recruitment scenario to fill them.

### 6a. The ask to the Finance owner

`../HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md` — written 2026-09-10 with R1, per D-10. What it
asks: a `FinanceDimensionRouteId` for HR AP; a catalogue row; confirmation that `Status = Approved`
on the cost is an acceptable authorising event; whether a payee that is not a `Supplier` (a
reimbursed candidate) has any AP path; whether the expense account is one setting or one per
category. R8 starts when it is answered.

---

## 7. Verification

Per slice: (1) type-check the touched frontend under `tsconfig.hr-slice.json`; (2) the user
builds — the running `ErpSystem.Api` is stopped first; migrations scaffolded by the user, edited
and listed by the agent, applied by the user; (3) API in Staging with the JWT key
(`hr-jobarch/README.md`), `node run-rN.mjs` twice, then the regression set; (4) a screen walk by
the user for R1, R4a and R5 — the harness cannot drive React; (5) stage, hand over the message.

---

## 8. Open questions, each with a default

| Q | Question | Default |
|---|---|---|
| Q-R1 | Does a division's budget include units already covered by a child unit's own budget for the same year? | Refuse the overlap at submit (409 naming the other budget); R4a checks it |
| Q-R2 | Exits due: count the voluntary retirement age (55) as a "may retire" tier? | Compulsory only in the count; voluntary-eligible listed separately |
| Q-R3 | Preview the budget check on the create form, or save-then-check? | Preview endpoint |
| Q-R4 | A linked budget is later rejected or withdrawn — what happens to Draft requisitions on it? | Link kept, `isBudgeted` recomputed on next write; submit refuses under Block with the sentence |
| Q-R5 | Do Recorded (not yet HR-approved) costs count against the recruitment budget in R6? | Approved only; Recorded shown as pending |
| Q-R6 | A cost whose payee is a person (candidate travel reimbursement) — no `Supplier`, so no AP invoice | Allowed in R7 with `PayeeName`; R8 refuses to hand it off and says why; the route is the Finance owner's answer |
| Q-R7 | Expense GL account for recruitment costs: one setting or one per category? | One setting (`RecruitmentExpenseAccountId`) until Finance says otherwise |
