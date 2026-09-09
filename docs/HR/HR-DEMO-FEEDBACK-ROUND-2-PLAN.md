# HR demo feedback, round 2 — findings, decisions and build plan

> **Status: PLANNED 2026-09-08. Nothing in this document is built yet.** Source: the feedback
> document *HR Demo Meetings — Changes and Additions* (4 pages; sections Organization Structure,
> Job Position, Skills Setup, Employee Profile), brought by the user on 2026-09-08 after the HR
> module demo. Every bullet of that document is accounted for below — as a bug, a build item, a
> decision, or a question — with the code it lands on. Nothing was left out on purpose; if a
> bullet is missing, that is an error in this document.
>
> **Vetting block.** Every "what exists" claim was verified against the working tree on
> 2026-09-08 (branch `hrdev`, head `6ace98bc`) by four read-only code surveys plus direct greps.
> Two claims in `docs/HR-FINISH-PLAN.md` § Lane 3a were found to be **overstated** and are
> corrected in § 8 of this document. Read § 1 (decisions) before § 5 (the plan); the plan is
> sequenced on those decisions.
>
> **Companions.** `docs/HR-FINISH-PLAN.md` (everything else HR owes), `docs/HR-CLOSURE-LEDGER.md`
> § F (the round-1 demo-feedback backlog — this is round 2), `HR-PAYROLL-BOUNDARY.md` (which
> § 1.2 of this document amends), `HR-WORKFLOW-ENGINE-INTEGRATION.md` (the recipe § 6.6 uses).

---

## 0. How to read this document

| Section | What it holds |
|---|---|
| § 1 | The five decisions the user made on 2026-09-08, and the two pieces of advice they asked for |
| § 2 | The item-by-item register: every PDF bullet, what exists (file:line), what is missing, disposition |
| § 3 | Bugs — things the demo showed that are simply broken, with the exact defect |
| § 4 | Findings outside the PDF that the survey turned up and that the same slices must fix |
| § 5 | The build plan: six lanes, sequenced, with slices and harness locations |
| § 6 | Design notes for the items that need more than a form change |
| § 7 | Asks to other owners (payroll, finance) |
| § 8 | Corrections owed to other documents |
| § 9 | Open questions — the few things still needing an answer, each with a default |

Dispositions used in § 2: **BUG** (wrong today, fix in lane A) · **BUILD** (new work, lane named) ·
**DECIDED** (a choice was made, see § 1) · **ADVISED** (I recommended, the user accepted in
principle) · **ASK** (needs another owner, § 7) · **Q** (still open, § 9).

---

## 1. Decisions taken 2026-09-08

### 1.1 Account code source → the Finance chart of accounts (DECIDED)

The organization unit's `AccountCode` becomes a reference to a Finance `Account`
(`src/ErpSystem.Core/Entities/Finance/Account.cs:16`), chosen from a picker, not typed.
Finance has no cost-centre master; cost centres exist only as chart-of-account segments and as
finance dimensions, so the chart of accounts is the right target. Design in § 6.2.

### 1.2 Salary tab shows payroll's salary window inside HR (DECIDED — amends the payroll boundary)

The payroll developer has agreed that HR may display payroll's per-employee salary window on the
employee's Salary tab, and let the user input into it. The user's instruction is to do "a clean and
proper job of integrating the display". This amends `HR-PAYROLL-BOUNDARY.md` § 1 in exactly one
respect: **HR may now build a shared frontend component out of payroll's employee-profile editor
and host it on the HR Salary tab.** Everything else in § 1 of that document stands — HR still does
not edit `PayrollService.cs`, `PayrollController.cs` or `PayrollEntities.cs`; what the embed needs
from the backend that does not exist is an **ask** (§ 7.1). Design in § 6.5.

### 1.3 Certification model (ADVISED — a dedicated credential catalogue under the certifying body)

The user asked what I advise for an enterprise HR solution. **Advice: build a dedicated
`Certification` catalogue, owned by `CertifyingBody`, separate from `Qualification`.** Reasons:

1. **A credential is a compliance object, a qualification is an education object.** A licence
   expires, is renewed, is revoked, and its lapse is a regulatory or safety event ("who may drive
   the forklift after 30 June?"). A degree does none of that. Modelling both as `Qualification`
   rows with `Type = Certification | License` (which is what exists — `HREntities.cs:2270`,
   enum `HREnums.cs:5470-5474`) leaves the expiry, renewal, evidence and issuer relationship with
   nowhere to live except free text (`IssuingAuthority`, `HREntities.cs:2283`).
2. **One catalogue row is referenced from three places** — the skill that needs it, the position
   that requires it, and the employee who holds it. The PDF asks for all three. A catalogue that
   is *only* a qualification type cannot be the child of a certifying body, which is the cascade
   the PDF asks for twice (skills setup bullets 1 and 2).
3. **The expiry sweep already exists for one credential family** —
   `IdentificationExpiryReminderService` with per-type lead days. Certifications get the same
   treatment by the same pattern, which only works if the certification is a first-class row.
4. **`EmployeeSkill` already carries certification-shaped columns** (`IsCertified`,
   `CertificationDate`, `CertificationExpiryDate`, `CertificationNumber`, free-text
   `CertifyingBody`, `CertifyingBodyId` — `HREntities.cs:2049-2074`). That is a per-skill copy of
   what should be one employee credential. The new model gives those columns a target
   (`EmployeeCertificationId`) and keeps them alongside, the way lane 3b kept the free-text
   certifying body beside the FK.

What happens to `Qualification`: **nothing is removed.** The `Certification` and `License` values
of `QualificationType` stay valid for the rows that hold them. New qualifications stop offering
those two types once the catalogue exists (soft deprecation, screen-level), and a one-time data
pass may create catalogue rows from the distinct `(Title, IssuingAuthority)` pairs — that pass is
Q-6 in § 9 because it needs a look at the real data first. Full design in § 6.3.

### 1.4 Teams and committees — full sub-module, in scope now (DECIDED)

Objectives, task lists, terms of reference, deadlines, monitoring and evaluation on a team are in
scope, as a full version, not a lightweight one. The PDF's question "do we need a sub-module for
committee or team activities?" is answered **yes**, built as tabs on the existing `Team` record
(`TeamType` already distinguishes `Committee`, `HREnums.cs:447-456`), with a self-service face
under `/me`. `SafetyCommittee` and `AwardCommittee` stay separate — they are bounded contexts with
their own rules (meeting quorum, scoring) and merging them would break two closed areas. Design in
§ 6.6.

### 1.5 Certification groups (ADVISED — yes, as one of three identical "named set" masters)

Given § 1.3, **advice: build the certification group**, and build it with the same shape as the
benefit group and the skill set the PDF asks for, so there is one pattern, one duplicate rule and
one UI idiom rather than three. Regulatory bundles are real ("driver: licence class C + defensive
driving + first aid"; "site engineer: professional registration + working-at-height + confined
space"), and a position that inherits them from a set is maintained in one place when the
regulator changes the bundle. Full design in § 6.4, including the duplicate rule the PDF asks for
in square brackets under both the benefits and the skills bullets.

---

## 2. The register — every PDF bullet

### 2.1 Organization structure

| # | PDF bullet | What exists (verified) | What is missing | Disposition |
|---|---|---|---|---|
| O-1 | Parent selection as a cascading dropdown: pick the parent's level number, then the unit at that level; parent must be at a higher level; child at level 4 → parent levels 1, 2 or 3 | The server rule already exists and is exactly this: `OrganizationStructureServices.cs:562` refuses a parent whose `LevelNumber` is not strictly lower; level-skipping is allowed (comment `:559-561`); `UpdateAsync` `:649` and `MoveUnitAsync` `:792` same. `OrganizationLevel.LevelNumber` at `OrganizationStructureEntities.cs:52`. The form (`OrganizationUnitForm.tsx:168-186`) lists **every unit flat**, no filter, labelled `name · levelName`. A Level→Unit cascade already exists but only for scope filters: `OrganizationScopeFields.tsx:48-77`. | A shared required-unit picker; `LevelNumber` and `StructureId` on `OrganizationUnitSummaryDto` (`OrganizationStructureDTOs.cs:287` has neither, so the cascade cannot rank levels from `/summary`). | BUILD — lane B1, § 6.1 |
| O-2 | Account code from the Finance chart of accounts | `OrganizationUnit.AccountCode` is free text, `[MaxLength(100)]` (`OrganizationStructureEntities.cs:109-110`), no validation anywhere; only reader is `OrganogramService.cs:146`. Finance: `Account` with `AccountCode`/`AccountNumber`/`AccountName`/`ParentAccountId`; endpoint `GET /api/finance/accounts` (`AccountController.cs:147-166`) gated to `ViewFinance` by `FinancePermissionPolicyMap` — **an HR user gets 403** (the same trap `HrCurrenciesController.cs:11-16` records). Same free-text twin on `Team.CostCenterCode` (`:245`) and the four legacy `Division/Department/Section/Unit.AccountCode` columns (`HREntities.cs:558, 586, 634, 666`). | FK to `Account`, an HR-side read-only accounts projection, a picker, validation. | DECIDED § 1.1 — lane B2, § 6.2 |
| O-3a | Unit history: the initial assignment is not populated | `RecordHistoryAsync` (`OrganizationStructureServices.cs:908-968`) is called from `UpdateAsync` `:709-721`, `MoveUnitAsync` `:824`, `ChangeHeadEmployeeAsync` `:877`. **`CreateAsync` (`:540-606`) never writes a row.** Confirmed by reading, not inferred. | A history row on create. | BUG (design gap) — lane B1 |
| O-3b | User specifies EffectiveFrom, EffectiveTo, ChangeReason "and any other ones" | `EffectiveFrom` is always `DateOnly.FromDateTime(DateTime.UtcNow)` (`:920`); `EffectiveTo` only written when the next row in the same series closes it (`:932-951`); `ChangeReason` is the only user field, optional on the edit form, required in the two restructure dialogs (`UnitRestructureDialogs.tsx:185, :342`). Controller `OrganizationUnitHistoryController.cs` is read-only (no POST/PUT/DELETE). Screen `unit-history/page.tsx` + `UnitChangeLog.tsx` read-only. | Dates on the create form, edit form and both dialogs; a write door to correct a row's dates/reason and to record a manual entry; a `Notes` column for "any other ones". | BUILD — lane B1, § 6.1 |
| O-4a | Teams: a task list and terms of reference | `Team` (`OrganizationStructureEntities.cs:203-299`), `TeamMember` (`:304`), `TeamMemberHistory` (`:349`). `TeamService.cs` and `TeamsController.cs` are CRUD + members only. Team page tabs: Members / Details / History (`teams/[id]/page.tsx:197-202`). Nothing else is attached. | Everything. | DECIDED § 1.4 — lane F, § 6.6 |
| O-4b | Checklist template: procurement has one — is it generic? | **No.** `AwardVerificationChecklistTemplate` (`Procurement/AwardVerificationEntities.cs:9`) carries `MinContractValue`/`MaxContractValue` and its run record `TenderAwardVerification` has a hard FK to `Tender` (`:108`). No owner-type discriminator. The SHE builder (`StaffSafetyEntities.cs:918-1120`) is richer but every child FKs `SheInspectionChecklist`. Maintenance has two more, both module-bound. | A team task checklist of its own (small: `TeamTaskChecklistItem`). Not a generic engine — recorded in § 9 as Q-7 with the recommendation *not* to build one now. | DECIDED — lane F |
| O-4c | Team or committee activities may involve a workflow | No org-structure entity is on the workflow engine (no adapter, no catalogue entry). Recipe: `HR-WORKFLOW-ENGINE-INTEGRATION.md` § 3 lines 38-64. | Objectives and terms of reference on the engine. | BUILD — lane F3 |
| O-4d | Monitoring, evaluation — can a workflow help? | Reminder sweeps exist per area (`ProbationReminderService.cs`, `SheReminderService.cs`, …, eight of them under `Services/HR/`), lead days on `CompanyHrPolicySettings` (`:97-160`). | A team reminder sweep, a review/evaluation record, a dashboard read. Workflow handles *approval*; monitoring is the sweep + dashboard, not the engine. | BUILD — lane F2/F3 |
| O-4e | Objectives — done or not, how done, deadlines, monitoring | Nothing. | `TeamObjective`, `TeamTask`, `TeamMeeting`, `TeamReview`. | BUILD — lane F |
| O-4f | Sub-module for committee/team activities? | — | Yes: tabs on the team record + `/me/teams`. | DECIDED § 1.4 |
| O-5 | Locations: cascading parent selection | `Location` carries `StructureId`, `LocationLevelId`, `ParentLocationId` (`:489-500`). Server rule is **stricter than units**: exactly one level below (`LocationStructureServices.cs:589-591`, `:708-710`). Form filters parents by structure and self only, not by level (`LocationForm.tsx:183-185`). | Level-filtered parent list (the only valid level is `child − 1`, so the level select is one entry — still shown, for the same idiom). | BUILD — lane B1 |
| O-6 | Other places the Level→Unit cascade must replace a flat dropdown | About forty screens/components build their own flat `Select` from `organizationUnitService.getAll()`/`getSummary()` — the list is in § 6.1.3. | The shared picker rolled out. | BUILD — lane B1 (org + position forms) and B3 (the sweep) |

### 2.2 Job position

| # | PDF bullet | What exists (verified) | What is missing | Disposition |
|---|---|---|---|---|
| P-1 | Filter the parent position by the selected organization unit? | Entity is `EmployeePosition` (`HREntities.cs:857`), not `JobPosition`. It carries `OrganizationUnitId` (`:879`) and `ReportsToPositionId` (`:886`). Form (`EmployeePositionForm.tsx:284-303`) lists **every position by title only** — two "Manager" rows in different units are indistinguishable. Server does **no** existence, self, cycle or unit check on create (`EmployeePositionService.cs:106`) or update (`:185`); the only client guard is self-exclusion on the edit page (`positions/[id]/edit/page.tsx:88-92`), absent on create. A unit-scoped list endpoint already exists and is unused: `GET api/EmployeePositions/organization-unit/{id}` (`EmployeePositionsController.cs:86-91`). | Unit-scoped picker with an escape hatch; server self/cycle validation; unit-and-code in the label. | BUILD — lane C1, § 6.1.4 |
| P-2 | The actual licences or certificates required, when "requires certification" is ticked | `RequiresCertification` (`:947`) and `RequiresLicense` (`:983`) are bare bools rendered as two switches (`EmployeePositionForm.tsx:649-716`) with **no consumer anywhere**. No `Certification` entity exists; no `PositionCertificationRequirement`. Nearest shape: `PositionDocumentRequirement` (`EmployeeDocuments.cs:153-173`, "reports, does not block"). Commit `d73520b8` touched **job-description** requirements (`JobQualification`/`JobCompetency`, `JobAnalysisEntities.cs:240-333`), not position requirements, and added no certification link. | The catalogue (§ 1.3) and `PositionCertificationRequirement`. | ADVISED § 1.3 — lane C2, § 6.3 |
| P-3 | A named group of benefits selectable against a position, with the no-duplicate rule | `EmployeePositionBenefit` (`:1010-1026`: `PositionId`, `PolicyId`, `ExpiryDate`, `PositionAmount`). Benefit = `BenefitPolicy` (`:701-836`). **No grouping entity** — `BenefitGroup`/`Package`/`Plan` do not exist. Duplicate rows are prevented three ways today (unique index `ApplicationDbContext.HR.cs:1912-1916`; `GroupBy(...).First()` in the service `:130, :344`; `takenIds` filter in the form `:566-575`) but a duplicate is silently **collapsed**, never refused. | `BenefitGroup` + members + position link + the refuse-not-collapse rule. | BUILD — lane C3, § 6.4 |
| P-4 | A named group of certifications too? | Nothing. | `CertificationSet` — same pattern. | ADVISED § 1.5 — lane C3 |

### 2.3 Skills setup

| # | PDF bullet | What exists (verified) | What is missing | Disposition |
|---|---|---|---|---|
| S-1 | If a skill requires certification, select the certifying body and the corresponding certification | `Skill.RequiresCertification` (`:2026`) is stored and shown as a badge (`skills/page.tsx:158`); **no `CertifyingBodyId` or `CertificationId` on `Skill`**; nothing enforces the flag when an `EmployeeSkill` is added. | `SkillCertification` rows (body → certification cascade, one or more accepted credentials). | BUILD — lane C2, § 6.3 |
| S-2 | Certifying bodies carry the actual certifications they provide, so the body picker can cascade to a certification | `CertifyingBody` (`:2300-2325`) has `Name`, `Abbreviation`, `Description`, `CountryId`, `Website`, `IsActive`; its **only** child collection is `EmployeeSkills`. Setup screen `administration/hr/certifying-bodies/page.tsx` is a single `ResourceListPanel`, no nested list. API `api/hr/reference/certifying-bodies` (`ReferenceDimensionsController.cs:92-131`). | The `Certification` catalogue as a child of the body, with a nested list on the body screen. | BUILD — lane C2 |
| S-3 | A skills set, a named group of skills, selectable on the position beside individual skills, with the no-duplicate rule | `PositionSkillRequirement` (`:2089-2106`: `SkillId`, `RequiredLevel`, `IsRequired`, `Priority`); unique index `ApplicationDbContext.HR.cs:1698`; service collapses duplicates (`:114-116`, `:284-291`). **No `SkillSet`/`SkillGroup` entity**; `Skill.Category` is free text (`:2021-2022`). ⚠ The form's skill rows have **no** `takenIds` filter (unlike benefits): the same skill can be picked twice and the second silently vanishes on reload (`EmployeePositionForm.tsx:439-541`). | `SkillSet` + members + position link + the rule. | BUILD — lane C3, § 6.4 |
| S-4 | On the employee's skills tab, show the skills attached to the position (via set or individually) and tick the ones held | `SkillsTab.tsx:48-56` lists **every** active skill; nothing reads `PositionSkillRequirement`. `AddSkillAsync` (`EmployeeService.cs:1302-1303`) rejects only exact duplicates. | An "effective skills for this position" read and a tab that leads with it. | BUILD — lane C3, § 6.4.4 |

### 2.4 Employee profile

| # | PDF bullet | What exists (verified) | What is missing | Disposition |
|---|---|---|---|---|
| E-1 | Move the gender description box closer to the gender dropdown | Gender at `EmployeeForm.tsx:470-478` (row 2, cell 3 of *Identity & Personal*); "Describe gender" at `:512-516` (row 4, cell 3), conditional on `Other`. Two grid rows apart. ⚠ **And the value is never sent** — see § 3.1. | Placement + the mapper fix. | BUG — lane A |
| E-2a | Confirmation date is not on the create form | Correct: no `confirmationDate` in `EmployeeForm.tsx` or `employeeFormMapper.ts`. It is on `CreateEmployeeDto` (`HRDTOs.cs:255`) and `UpdateEmployeeDto` (`:373`) so the API accepts it. Its only writer is `ProbationService.MarkEmployeeConfirmed` (`ProbationService.cs:372`). Not on the profile Overview either (`[id]/page.tsx:232-234` shows probation days and on-probation only). | See E-2b. | BUILD — lane D1, § 6.7 |
| E-2b | How does it relate to the probation period? Should both be specifiable? Read probation from the position? Disable user input? | Three homes, two units: `EmployeePosition.ProbationPeriodMonths` (`int?`, `:935-939`), `Employee.ProbationPeriodDays` (`int`, default 90, `:169`), `EmployeeContractDetail.ProbationPeriodDays` (`int?`, `:1453`) + its own `ConfirmationDate` (`:1455`). Resolution already prefers the position (`ProbationService.cs:250-263`, `Source = "Position"|"PolicyDefault"`); the hire path converts months×30 (`JobOfferHireService.cs:1461`). Create form shows "Probation (days)" free input (`EmployeeForm.tsx:775-777`). No code derives a confirmation date from hire date + probation. | The rule in § 6.7: position is the source, form shows it read-only with the expected confirmation date derived, confirmation date itself is written only by the probation confirm action, override only where the position is silent. | ADVISED — lane D1, § 6.7 |
| E-3 | Geography selection for the employee address and other sub-details | The main address is already on the tree: `Employee.GeoAreaId` (`:141`), `AddressFields.tsx` used at `EmployeeForm.tsx:566-606`, snapshot written by `ApplyGeoAreaSnapshotAsync` (`EmployeeService.cs:86`). **No sub-record carries `GeoAreaId`**: `EmployeeContact` (`:1049-1077`), `EmployeeEmergencyContact` (`:1082`), `EmployeeGuarantor` (`:1713`), `EmployeeWorkHistory` (`:1349`), `EmployeeDependent` (`:1143`, has only `DigitalAddress`). `ContactsTab.tsx:115-135` uses plain text fields. | `GeoAreaId` + `CountryId` + city/region snapshot on the four address-bearing sub-records, `AddressFields` on their forms, consumer registration for the delete guard (`GeoAreaConsumers.cs`). | BUILD — lane D2, § 6.8 |
| E-4 | Dependant benefit enrolment modal: names not showing in the dropdown | `DependentBenefitsDialog.tsx:73` builds labels from `p.name`; the endpoint `GET api/hr/benefit-policies/active` returns `BenefitPolicyDto` whose field is `PolicyName` (`BenefitPolicyDTOs.cs:361`) — there is no `Name`. Every other consumer reads `policyName` (`hr/benefits/page.tsx:92-99`). The dialog bypasses the typed service and calls `apiService.get` with a local interface, which is why TypeScript did not catch it. | One-line fix + use `benefitPolicyService.getActive()`. | BUG — lane A, § 3.2 |
| E-5 | With a skills set or individual position skills, the employee skills tab shows those and the user ticks the held ones | Same as S-4. | Same as S-4. | BUILD — lane C3 |
| E-6 | Geography for the work-history record? | `EmployeeWorkHistory.CompanyAddress` is one free-text line, `[MaxLength(200)]` (`:1349-1388`); no country, city or geo. ⚠ Zod allows 300 (`WorkHistoryTab.tsx:20`) — a 201–300 character address 400s on the server. | `CountryId`, `GeoAreaId`, `City` snapshot; `AddressFields`; length fix. | BUILD — lane D2 |
| E-7a | Insert the contract detail once the employee is created? It reflects the current terms | **Not written on create.** `CreateWithNumberAsync` (`EmployeeService.cs:116-273`) writes `Employee`, `EmployeePositionHistory` and the payroll profile — no contract. Writers of `EmployeeContractDetail`: manual tab `AddContractAsync` (`:1552-1604`), hire-from-offer (`JobOfferHireService.cs:1509-1536`), import (`EmployeeImportService.cs:595-606`), plus closers on terminate/separate (`:584-593`, `:2836-2848`). `StaffMovementService` never touches it, so a promotion/conversion never updates the contract's `Salary` or `EmploymentType`. | A contract row on create; the header writes through to the current contract; movements that change terms open a new contract. | BUILD — lane D1, § 6.7 |
| E-7b | Read probation days and confirmation date from the employee header? | The contract has its own copies (`:1453`, `:1455`); `RequireUnconfirmedProbationAsync` (`:1617-1626`) gates edits to the contract's copies off `Employee.ConfirmationDate`. | Header is the source; contract copies are written by the header path, read-only on the tab. | ADVISED — lane D1 |
| E-7c | What about the other details? | ⚠ `AddContractAsync` never sets `EffectiveDate` (inserts `0001-01-01`), `ContractEndDate`, `IsCurrent` (entity default `true`, so **every added contract claims to be current**) or `AnnualLeaveEntitlementDays`; the create DTO does not expose them (`HRDTOs.cs:1065-1125`). | Fix the writer; close the previous current contract when a new one is added. | BUG — lane D1, § 4 |
| E-7d | When is the insertion made automatically, apart from manual update? | See E-7a. | On create; on hire-from-offer (exists); on import (exists); on a movement that changes employment type or terms (new). | ADVISED — lane D1 |
| E-8 | Remove salary details from the contract tab; handle them on the Salary tab | `ContractsTab.tsx:223-316` shows salary, currency, pay frequency, tax treatment, withholding rate, pension applicable, tax exempt. Columns on `EmployeeContractDetail` (`:1394-1487`). ⚠ `SeparationService.DailyRateAsync` (`:1875`) reads `contract.Salary` for settlement — the column cannot simply go dark. | Remove from the tab; keep columns; re-point the settlement reader to the pay basis. | BUILD — lane E1, § 6.5 |
| E-9a | Permanent and non-permanent salary both go to the Salary window | Salary tab = `SalaryAssignmentsTab.tsx` (grade/level/notch placement only; read-only banner when off payroll). Payroll's window = the dialog inline in `hr/payroll/employee-profiles/page.tsx:659-932` (Profile tab: basic salary + six flags; Payment Methods tab). | The embed. | DECIDED § 1.2 — lane E, § 6.5 |
| E-9b | Distinguish pure contract staff from permanent in terms of salary | `EmploymentType` enum on `Employee` (`:167`) and on the contract (`:1403`). No pay-basis distinction anywhere. | The pay-basis flag (E-9d). | BUILD — lane E1 |
| E-9c | Contract staff can be on payroll and pay PAYE, but their amount is not on the salary scale | Payroll computes basic pay **only** from `PayrollSalaryBasis.MonthlyBasicSalary` (`PayrollService.cs:11912-11928`, run loop `:4592`); grade/notch play no part in payroll. HR's scale (`EmployeeSalaryAssignment` → `SalaryNotch.SalaryAmount`) is a separate HR number payroll never reads. So payroll is *already* amount-based; "off the scale" is an HR fact. | The flag on HR's side; the reconciliation reports scale-vs-payroll mismatches. | BUILD — lane E1 |
| E-9d | Flag an employee that payroll should not apply the salary scale to; negotiated amount instead of a grade | No `IsOnSalaryScale`/`NegotiatedSalary`/`ExcludeFromSalaryScale` anywhere (grep-verified). Nearest is `Employee.IsOnPayroll` + `OffPayrollReason` (`:258-273`), which is membership, not basis. | `Employee.PayBasis { SalaryScale, Negotiated }` + note; grade placement refused when `Negotiated`; `BasicPayMismatch` in the reconciliation. | BUILD — lane E1, § 6.5.2 |
| E-9e | Payroll's benefits/allowances/deductions/loans view available in HR | Payroll's window has **no** components section — `employeeComponents: []` is hard-coded (`employee-profiles/page.tsx:721`), the TS type is `any[]` (`payrollService.ts:1165`), and no screen edits `PayrollEmployeeComponent`. Loans, advances, tax reliefs and component *exceptions* have their own pages with per-employee-number GET endpoints (`PayrollController.cs:245-292`). | HR can show read-only loans/advances/exceptions now; a per-employee components editor is **payroll's** to build. | ASK § 7.1 — lane E2 |
| E-9f | Clicking the Salary tab shows payroll's salary window for input | See E-9a. ⚠ No read-by-employee endpoint exists (`GET employee-profiles?searchTerm=` only, capped at 250 — `PayrollService.cs:2355`); the upsert is a **replace-set** over payment methods (`:13463-13467`) and writes `employee.Overtime` back into HR (`:2385`). `PayrollController` is a bare `[Authorize]` — any internal user may call it (defect #11, `HR-PAYROLL-BOUNDARY.md:159`). | The shared component, an HR-side by-employee read, the replace-set handled, an HR gate on the bridge. | DECIDED — lane E1, § 6.5 |
| E-10 | Upload the reference letter with the referee | Backend done: `EmployeeReferee.Letter*` six columns (`:1690-1700`), `POST api/hr/employee-documents/referees/{id}/letter` (`EmployeeDocumentsController.cs:415-439`), download `:441-460`, `AttachRefereeLetterAsync` (`EmployeeDocumentService.cs:438-464`). **Frontend: nothing.** `RefereesTab.tsx` has no file input, no download, no `hasLetter`; the list DTO omits the letter fields (`HRDTOs.cs:1819-1832`); the TS type omits them (`employee-subresources.ts:694-710`); no service method. | The screen. | BUG (finish plan overstated) — lane A, § 3.3 |
| E-11a | Relationship for referee / supervisor / guarantor defined somewhere and offered as a dropdown | Four free-text `Relationship` columns: referee (`:1668-1670`, 200), guarantor (`:1726-1728`, 100), emergency contact / next of kin (`:1097-1099`, 50), job-candidate referee (`RecruitmentEntities.cs:1009`); plus medical `EmergencyContactRelationship` (`MedicalEntities.cs:907`). Dependants use the `DependentRelationship` enum (`HREnums.cs:240-273`), which `ExpatriateFamilyMember` reuses on purpose (`:40-51`). **No relationship lookup entity, no generic `HrLookup` table** — HR reference data is one table per concept (catalogue in `hr-setup-nav.ts:245-320`). "Supervisor" on work history is two free-text columns (`SupervisorName`, `SupervisorPhone`, `:1378-1382`), no relationship. | `RelationshipType` lookup with a category. | BUILD — lane D2, § 6.9 |
| E-11b | Differentiate familial from professional relationships, to know which values to populate | Nothing. | `RelationshipCategory { Familial, Professional, Other }` on the lookup; each screen filters by category. | BUILD — lane D2 |
| E-12 | Upload the guarantor's picture and any documents pertaining to the guarantor | Photo backend done: `Photo*` six columns (`:1825-1835`), `POST .../guarantors/{id}/photo` (`:364-390`), download `:392-413`. **Frontend: no file control, no `hasPhoto` column**; TS type has the fields (`:734-738`) but nothing reads them. Documents: only the legacy `GuarantorFormPath` string (`:1842`, flagged at `:1820-1824` as a legacy sink with **no upload endpoint behind it**), still accepted on the JSON create (`HRDTOs.cs:2037-2038`). No `EmployeeGuarantorDocument` entity. | The photo screen; a guarantor document collection on the upload gate; retire `GuarantorFormPath`. | BUG + BUILD — lane A, § 3.3 |
| E-13 | Guarantor national ID type as a dropdown from the identification types | `EmployeeGuarantor.NationalIdType` is free text `[MaxLength(50)]` (`:1786-1787`); form is a text box (`GuarantorsTab.tsx:303-308`). `IdentificationType` exists (`:2388+`, required `IssuingAuthorityName`, `HasExpiryDate`, lead days) with CRUD + screen; its only consumers are `EmployeeIdentificationCard` and the expiry sweep; the only dropdown is `IdentificationTab.tsx:120-127`. | `NationalIdTypeId` FK + dropdown; keep the text column for old rows (the lane-3b idiom). | BUILD — lane A |
| E-14 | Bank and branch as dropdowns from the set-up banks and branches | Masters exist: `EmployeeBank` (`:1878-1901`), `EmployeeBankBranch` (`:1906-1941`); API `api/hr/banks` (+ `/active`, `/{bankId}/branches/active`; `EmployeeBanksController.cs`); setup screen `administration/hr/banks/`; client `bank.service.ts`. The account row already has `BankId`/`BranchId` (`:1956-1961`) beside the free-text `BankName`/`BranchName` (`:1968-1972`). **The tab sends free text only** (`BankDetailsTab.tsx:47-57`, never imports `bank.service.ts`). Reads never `Include` the navs (`EmployeeService.cs:2353-2365`) so `BankName = e.Bank?.Name ?? e.BankName` (`EmployeeMappingExtensions.cs:1585-1604`) always falls through; writes copy `BankId`/`BranchId` with **no** check that the branch belongs to the bank. | Wire the dropdowns; validate branch ∈ bank; `Include` on read; keep free text for legacy rows. | BUG (unfinished wiring) — lane A |

---

## 3. Bugs the demo exposed — the exact defects

### 3.1 Four employee fields are typed and thrown away

`employeeFormMapper.ts:21-77` builds the request by hand and has no key for `genderDescription`,
`hometown`, `hasDisability` or `disabilityDescription`. The form collects all four
(`EmployeeForm.tsx:51-54`, `:127-130`, `:507-537`), the edit page hydrates them from `/details`
(`edit/page.tsx:36-39`), and the request type carries them (`types/hr/employee.ts:196-203`,
`:253-260`). The backend accepts them. **Nothing typed into those boxes has ever reached the
database from the form** — the lane-3a harness proved the API, not the screen, which is the
"coverage of the API is not coverage of the product" lesson from area 16 again. Fix: four lines
in the mapper; assert with a screen-payload probe, not an API call.

### 3.2 Dependant benefit dropdown labels are empty

`DependentBenefitsDialog.tsx:43-46` declares a local `{ id; name }` type and `:73` reads `p.name`.
The API field is `policyName`. Fix: read `policyName` (and show `policyCode`), replace the raw
`apiService.get` with `benefitPolicyService.getActive()` so the type is the real one.

### 3.3 Four upload doors have no screen

`EmployeeDocumentsController.cs` serves employee photo (`:271/:297`), dependant photo
(`:319/:345`), guarantor photo (`:365/:393`) and referee letter (`:416/:442`). A grep of
`frontend/src` for any of those four routes finds **one comment** (`employee-subresources.ts:735`)
and no request. `docs/HR-FINISH-PLAN.md` § 3a lists "Screens: … the guarantor surety + currency,
… the compliance strip" — it never actually claimed the upload controls, but the row-level ticks
read as if the feature were reachable, and it is not. Fix: an upload control + download link on
`RefereesTab.tsx`, `GuarantorsTab.tsx`, `DependentsTab.tsx` and the employee Overview, following
`DocumentsTab.tsx:95-118` (mutation) and `:402-428` (file input), via
`hrDocumentService.upload` (`hr-document.service.ts:24-43`). Add `hasLetter`/`hasPhoto` to the
list DTOs so the row can show a paperclip without a detail read.

### 3.4 Gender description sits two rows from gender

`EmployeeForm.tsx:470-478` vs `:512-516`. Move the conditional box into the cell after Gender
(row 2 wraps to a fourth cell on `lg`, or the row becomes 2+2). Same section, adjacent.

### 3.5 Guarantor edit opens half-empty

`GuarantorsTab.tsx` lists through `getGuarantors` (the **List** DTO: id, names, relationship,
phone, email, flags — `HRDTOs.cs:1905-1917`) and does not pass `loadForEdit` (the hydration hook
on `ResourceCollectionTab.tsx:92-107`), so `toForm` (`:200-232`) reads address, employer,
national-ID and amount properties the list never carried. Fix: pass `loadForEdit` reading the
detail endpoint, the way `medical/insurance/page.tsx:178` does.

### 3.6 Bank details ignore the bank master (E-14) and guarantor ID type ignores the lookup (E-13)

Both are wiring, not schema, except the guarantor needs one nullable FK column.

---

## 4. Findings outside the PDF that the same slices must carry

| # | Finding | Where | Lane |
|---|---|---|---|
| X-1 | Position form allows the same skill on two rows; server keeps the first silently | `EmployeePositionForm.tsx:439-541` (no `takenIds`), `EmployeePositionService.cs:114-116` | C3 — the refuse rule covers it |
| X-2 | Duplicate benefits/skills are collapsed, never refused (no DTO validation) | `EmployeePositionService.cs:130-132`, `:344` | C3 |
| X-3 | `EmployeePositionBenefitDto.IsActive` exists with no column behind it | `HRDTOs.cs:2660` | C3 — drop the DTO field or add the column; recommend drop |
| X-4 | `AddContractAsync` inserts `EffectiveDate = 0001-01-01`, `IsCurrent = true` always; nothing closes the previous current contract | `EmployeeService.cs:1552-1604` | D1 |
| X-5 | Work-history zod max (300) exceeds the column (200) | `WorkHistoryTab.tsx:20` vs `HREntities.cs:1351` | D2 |
| X-6 | `EmployeeContractType` lookup is seeded with seven TDC rows and has no DTO, service, controller or screen | `HREntities.cs:1028-1039`, `TdcDemoLegacyOrgSeeder.cs:99-140` | D1 — either surface it as the contract-kind picker (recommended: it carries `Duration`, which drives `ContractEndDate`) or delete the seed; Q-4 |
| X-7 | Referee list DTO omits the letter fields; guarantor list DTO omits `HasPhoto` | `HRDTOs.cs:1819-1832`, `:1905-1917` | A |
| X-8 | `GuarantorFormPath` still accepted on the JSON create — a caller-supplied path, the exact shape lane 3a-ii removed elsewhere | `HRDTOs.cs:2037-2038`, `:2098` | A — retire with the document collection |
| X-9 | Bank reads never `Include` `Bank`/`Branch`; branch-in-bank unvalidated | `EmployeeService.cs:2353-2365`, `:2367-2416` | A |
| X-10 | Unit summary DTO has no `LevelNumber`/`StructureId`; the picker cannot rank without them | `OrganizationStructureDTOs.cs:287` | B1 |
| X-11 | Locations require exactly one level below, units allow skipping — two rules for one idiom | `LocationStructureServices.cs:589`, `OrganizationStructureServices.cs:562` | B1 — keep both (documented), the picker reads the rule per family |
| X-12 | `Team.CostCenterCode` and the four legacy `AccountCode` columns are the same free-text problem as O-2 | `OrganizationStructureEntities.cs:245`; `HREntities.cs:558-666` | B2 — `Team` gets the FK; the legacy four are deprecated tables and stay |
| X-13 | Payroll upsert writes `employee.Overtime` back into HR — a second writer on an HR column | `PayrollService.cs:2385` | E — recorded for the payroll owner (§ 7.1) |
| X-14 | Payroll's window hard-codes the salary-basis effective date to today and has no basis history/close | `employee-profiles/page.tsx:717`; `PayrollService.cs:13424` | § 7.1 |

---

## 5. The build plan

Six lanes, each cut into slices that fit the house rhythm (one migration where needed, one set of
screens, one harness run green twice). Order inside a lane is fixed; lanes A, B, C, D are
independent of each other; E depends on nothing in A–D but on § 7.1 for its second slice; F is
independent and the largest. **Recommended order: A → B1 → C1 → C2 → D1 → E1 → C3 → B2 → D2 → F →
B3 → E2.** A first because the demo audience saw those and they are cheap; C2 before C3 because
the sets need the certification catalogue; B3 (the forty-screen sweep) last because it is
mechanical and long.

Harness folders under `D:\Rhema\TDC ERPS\dev-harness\` (outside the repo, per the demo-pack
boundary): existing `hr-employee-docs`, `hr-jobarch`, `hr-payroll-membership`; new
`hr-organization` and `hr-teams`. Every new table goes into
`dev-harness/hr-demo-smoke/demo-coverage-manifest.csv` or the UAT rebuild gate reports it. Every
new migration is guarded and listed in `FastBuildMigrationMetadata` or it is inert on the fast
build. Every reference-data screen goes into `frontend/src/config/hr-setup-nav.ts` under the
group it belongs to, and the runbook path must name the group.

### Lane A — Bugs and unreachable features · 1 slice · migration: one nullable FK

- [ ] A-1 `employeeFormMapper.ts`: send `genderDescription`, `hometown`, `hasDisability`, `disabilityDescription` (§ 3.1). Probe: submit the form with all four, read `/details`.
- [ ] A-2 `DependentBenefitsDialog.tsx`: `policyName` + typed service (§ 3.2).
- [ ] A-3 Gender description adjacent to Gender (§ 3.4).
- [ ] A-4 Referee letter: upload + download on `RefereesTab.tsx`; `hasLetter`/`letterFileName` on the list DTO and TS type; `employee.service.ts` gains `uploadRefereeLetter`/`refereeLetterUrl` (§ 3.3).
- [ ] A-5 Guarantor photo: same on `GuarantorsTab.tsx`; `hasPhoto` on the list DTO.
- [ ] A-6 Guarantor documents: `EmployeeGuarantorDocument` (gate columns, `DocumentTypeId → EmployeeDocumentType`, `Title`, `UploadedById`), `POST/GET/DELETE api/hr/employee-documents/guarantors/{id}/documents`, a documents strip inside the guarantor row. Retire `GuarantorFormPath` from both write DTOs (keep the column, read-only, until the data pass). ⚠ This is a second migration item in the same slice — acceptable, one migration file.
- [ ] A-7 Dependant photo and employee photo: upload controls on `DependentsTab.tsx` and the employee Overview header (§ 3.3).
- [ ] A-8 Guarantor edit hydration via `loadForEdit` (§ 3.5).
- [ ] A-9 Guarantor `NationalIdTypeId` (nullable FK → `IdentificationType`, Restrict) + dropdown; free text kept and shown when the FK is null; DTOs carry both; masking of the number unchanged.
- [ ] A-10 Bank/branch dropdowns from `bank.service.ts`; branch ∈ bank validated in `AddBankDetailAsync`/`UpdateBankDetailAsync`; `Include(Bank).Include(Branch)` on both reads; free text remains for rows with no FK, and the form offers "Bank not in the list" that reveals the text boxes.
- [ ] A-11 Harness `hr-employee-docs/run-round2-laneA.mjs`: the four mapper fields round-trip through the **screen payload shape**; dropdown label non-empty; each upload door reachable from its tab's service method; guarantor edit hydrates; branch-of-another-bank refused 400; national ID type FK round-trips.

### Lane B — Organization structure · 3 slices

**B1 — The cascading picker, history dates, the initial history row.** Migration: `Notes` on `OrganizationUnitHistory`, nothing else.
- [ ] `OrganizationUnitSummaryDto` gains `LevelNumber`, `StructureId` (X-10); `LocationSummary` likewise if absent.
- [ ] `frontend/src/components/hr/common/OrganizationUnitPicker.tsx` (§ 6.1.1) and `LocationPicker.tsx` (§ 6.1.2).
- [ ] `OrganizationUnitForm.tsx`: parent via the picker with `maxLevelNumber = chosenLevel − 1`; `LocationForm.tsx`: parent via the picker with `exactLevelNumber = chosenLevel − 1`; `UnitRestructureDialogs.tsx` move dialog same; `TeamForm.tsx` owning unit; `EmployeePositionForm.tsx` unit (already cascaded inline — swap to the shared component).
- [ ] `CreateAsync` writes the initial history row (`PreviousParentId = null`, `NewParentId = parent`, head likewise, `EffectiveFrom` from the DTO or today, reason from the DTO).
- [ ] `EffectiveFrom`, `EffectiveTo`, `ChangeReason`, `Notes` on create/update DTOs and both dialogs; `RecordHistoryAsync` takes them instead of `UtcNow`; validation `EffectiveTo ≥ EffectiveFrom`; a row's `EffectiveTo` is still auto-closed by the next row in its series **unless** the user set it.
- [ ] `OrganizationUnitHistoryController`: `POST` (manual entry, `ChangeType = Other`, requires reason) and `PUT {id}` (dates, reason, notes only), both `EmployeeAdminPolicy`; `UnitChangeLog.tsx` gains an edit action and a "Record an entry" button.
- [ ] Harness `hr-organization/run-b1.mjs`: create → one history row with the given dates; parent at a lower level refused; level skipped accepted; location parent two levels up refused; manual entry; date edit; `EffectiveTo < EffectiveFrom` refused.

**B2 — Account code from the chart of accounts** (§ 6.2). Migration: `FinanceAccountId` on `OrganizationUnit` and `Team`.
- [ ] `HrFinanceAccountsController` (`GET api/hr/finance-accounts?search=&take=`), read-only projection `{id, accountCode, accountNumber, accountName, accountType, isActive}` — the `HrCurrenciesController` precedent, same justification comment.
- [ ] `OrganizationUnit.FinanceAccountId` (Guid?, FK → `Account`, Restrict); `AccountCode` kept as the **snapshot** of `Account.AccountCode` written by the service on every save (the organogram and any report keep working); `Team.FinanceAccountId` + `CostCenterCode` snapshot the same way.
- [ ] Service validation: account exists, `IsActive`, same tenant; a change is a history-worthy event? **No** — it is not parent or head; recorded on the unit's audit only.
- [ ] `FinanceAccountPicker.tsx` (searchable combobox, the `TaxAccountPicker` shape from `TaxFormDialog.tsx:58-116`, storing the id); on `OrganizationUnitForm.tsx` and `TeamForm.tsx`; the organogram detail drawer shows code + name.
- [ ] Register in `HR-MODULE-INTEGRATION-MAP.md` (new row HR → Finance, read-only) and § 7.2 (Finance owner: deactivating an account that HR references).
- [ ] Harness `hr-organization/run-b2.mjs`: HR user reads the projection 200 (and `api/finance/accounts` 403, to prove why the projection exists); unknown id 400; inactive account 400; snapshot equals the account's code after save and after the account is renamed (a stale snapshot is by design — assert it).

**B3 — The picker sweep.** No migration. Replace every flat unit dropdown in § 6.1.3 with the shared picker, screen by screen, each with a screen-payload probe that the chosen id still reaches the API. Split into two halves if it runs long (HR/admin screens, then SHE/training/performance screens). Harness: extend each area's existing UI-payload probe rather than a new one.

### Lane C — Positions, certifications, sets · 3 slices

**C1 — Reports-to filtered and validated.** No migration.
- [ ] Form: reports-to options = positions in the chosen unit **or any ancestor unit** (walk `Path`), labelled `title · unit · code`; a "Show all positions" toggle for matrix cases; disabled until a unit is chosen (§ 6.1.4).
- [ ] Service: `ReportsToPositionId` must exist, ≠ self, and must not create a cycle (walk `ReportsToPositionId` upwards, bounded); on both create and update. Unit mismatch is **allowed** (soft rule) — Q-2.
- [ ] Harness `hr-jobarch/run-c1.mjs`: self refused; A→B→A refused; cross-unit accepted; unknown id 404/400.

**C2 — Certification catalogue, skill cascade, position requirements, employee credentials, expiry sweep** (§ 6.3). Migration: five tables + columns.
- [ ] `Certification`, `SkillCertification`, `PositionCertificationRequirement`, `EmployeeCertification` (+ `EmployeeSkill.EmployeeCertificationId`).
- [ ] Reference API under `api/hr/reference/certifications` (+ `certifying-bodies/{id}/certifications`); position sub-resource `api/EmployeePositions/{id}/certification-requirements`; employee sub-resource `api/hr/Employees/{id}/certifications` with the upload-gate evidence door on `EmployeeDocumentsController`.
- [ ] Screens: nested certifications list on the certifying-body page; `SkillForm.tsx` gains a body→certification cascade list shown when `requiresCertification`; `EmployeePositionForm.tsx` gains a certification requirements panel (shown when either switch is on; the two switches stay, the panel gives them meaning); employee `CertificationsTab.tsx`; compliance strip on the employee Overview (required vs held vs expired).
- [ ] `CertificationExpiryReminderService` on the `IdentificationExpiryReminderService` pattern; `Certification.ExpiryNotificationLeadDays`.
- [ ] Setup nav: *People Reference Data → Certifications*; employee-tab and permission gates on `HR.Employee.*` / `HR.Competency.*` (the skill side).
- [ ] Harness `hr-jobarch/run-c2.mjs`: a certification must belong to the chosen body (mismatch 400); a skill with `requiresCertification` and no accepted credential refused on save; an `EmployeeSkill` added against such a skill without an `EmployeeCertification` is **allowed but flagged** (`isCompliant = false`) — recording is not gating, the position compliance read does the reporting; expiry computed from `ValidityMonths` when `ExpiresOn` omitted; the sweep emits for a credential inside lead days (log-checked, not harness-inferred — the lane-1 lesson).

**C3 — Benefit groups, skill sets, certification sets, the duplicate rule, employee skills from the position** (§ 6.4). Migration: six tables + three link tables.
- [ ] `BenefitGroup`/`BenefitGroupMember`, `SkillSet`/`SkillSetMember`, `CertificationSet`/`CertificationSetMember`; `EmployeePositionBenefitGroup`, `PositionSkillSet`, `PositionCertificationSet`.
- [ ] Effective reads: `GET api/EmployeePositions/{id}/effective-{benefits|skills|certifications}` → union with `source: "Individual" | "Set:<name>"`.
- [ ] The refuse rule in the position service, once, for all three (§ 6.4.2); X-1/X-2/X-3 closed by it.
- [ ] `SkillsTab.tsx` leads with the position's effective skills as tick-boxes (held / not held / add), then "other skills" (§ 6.4.4).
- [ ] Screens: three masters under setup nav (*Pay & Benefits → Benefit Groups*; *Competencies → Skill Sets*; *People Reference Data → Certification Sets*); three panels on the position form, each with "sets" above "individual".
- [ ] Harness `hr-jobarch/run-c3.mjs`: individual already in an attached set → 400 naming the set; attaching a set that contains an attached individual → 400 naming the row; two sets sharing a member → allowed (union); effective read shows the source; employee skills tab payload shape probed.

### Lane D — Employee profile · 2 slices

**D1 — Probation, confirmation, contract-on-create** (§ 6.7). Migration: `Employee.ProbationSource` (small enum or nullable bool), nothing else — the rest is behaviour.
- [ ] Create form: probation shown as *"6 months, from the position (expected confirmation 2027-03-08)"* read-only; editable only when the position has no value (then it is the policy default with an override); `probationPeriodDays` still sent (derived).
- [ ] `ConfirmationDate` on the Overview (read-only) and on the edit form **only in import mode** for staff already confirmed elsewhere.
- [ ] `CreateWithNumberAsync` opens the first `EmployeeContractDetail` (number from `INumberSequenceService`, `EmploymentType`, `StartDate = DateEmployed`, `EffectiveDate`, `IsCurrent = true`, probation days, contract kind from `EmployeeContractType` if X-6 is adopted) and, for `Permanent`, the `ProbationPeriod` row — mirroring `JobOfferHireService.cs:1509-1551`.
- [ ] `AddContractAsync` sets `EffectiveDate`/`IsCurrent`, closes the previous current row (X-4); update DTO exposes `ContractEndDate`, `AnnualLeaveEntitlementDays`.
- [ ] Header write-through: changing `EmploymentType`/probation on the employee updates the current contract's copies (one direction only).
- [ ] `StaffMovementService`: a movement that changes employment type or contract terms opens a new contract row and closes the old (conversion path).
- [ ] Harness `hr-probation/run-d1.mjs`: create → one current contract + probation row; second manual contract closes the first; header change reflected; confirmation date refused from the ordinary edit path (guard already exists — assert it stays).

**D2 — Geography on sub-records, relationship lookup, work history** (§ 6.8, § 6.9). Migration: geo columns on four tables, `RelationshipType` table + four nullable FKs.
- [ ] `GeoAreaId`, `CountryId` (where absent), `City`/`Region` snapshots on `EmployeeContact`, `EmployeeEmergencyContact`, `EmployeeGuarantor`, `EmployeeWorkHistory`; `AddressFields` on their tabs; consumers registered in `GeoAreaConsumers.cs`; snapshot helper shared with `ApplyGeoAreaSnapshotAsync`.
- [ ] `RelationshipType` + seed; `RelationshipTypeId` on referee, guarantor, emergency contact, job-candidate referee; free text kept; each tab's dropdown filtered by category (§ 6.9).
- [ ] Work history: `CompanyAddress` length aligned (X-5).
- [ ] Harness `hr-employee-docs/run-d2.mjs`: geo snapshot written on each sub-record; a geo area outside the chosen country refused; relationship of the wrong category refused per screen; lookup delete refused when in use.

### Lane E — Salary tab · 2 slices (§ 6.5)

**E1 — The embed, the pay-basis flag, the contract tab trimmed.** Migration: `Employee.PayBasis`, `Employee.PayBasisNote`.
- [ ] Extract `PayrollEmployeeProfileEditor.tsx` from `employee-profiles/page.tsx:659-932` into `frontend/src/components/hr/payroll/`, keyed by `employeeId`, owning its own lookups (base currency, bank branches, exchange rates via `payrollService`) and its own save; the payroll page hosts it unchanged in behaviour (the one permitted edit under § 1.2).
- [ ] `GET api/hr/Employees/{id}/payroll-profile` on HR's side (through `IPayrollService.GetEmployeeProfilesAsync(employeeNumber)` filtered to the exact employee — the by-employee read payroll lacks), gated `HR.Compensation.Read`; the save goes to payroll's own `POST api/hr/payroll/employee-profiles` with the **complete** payment-method list.
- [ ] `Employee.PayBasis { SalaryScale = 1, Negotiated = 2 }` default `SalaryScale`; `PayBasisNote`; on the Salary tab header, not the create form; `AssignSalaryAsync` refuses when `Negotiated` (`RequireOnScaleAsync`, next to `RequireOnPayrollAsync`); `PayrollMembershipService.IssueFor` gains `BasicPayMismatch` (scale amount ≠ payroll basis, `SalaryScale` only).
- [ ] Salary tab layout (§ 6.5.3): Pay basis → Grade placement (scale only) → Payroll profile (the embed) → Payroll items (read-only loans/advances/exceptions by employee number).
- [ ] `ContractsTab.tsx`: salary, currency, pay frequency, tax treatment, withholding, pension, tax-exempt removed; columns kept; `SeparationService.DailyRateAsync` reads the pay basis through a shared `HrBasicPay`-style helper (it already exists at `PayrollMembershipService.cs:347` — lift it).
- [ ] Create form: salary amount + the five payroll flags move to the Salary tab; `isOnPayroll` + off-payroll reason stay (membership is HR's) — Q-3.
- [ ] Harness `hr-payroll-membership/run-e1.mjs`: HR user reads the profile through HR's door; a save with a partial method list is **refused by HR's client-side guard** (the replace-set trap asserted, not just documented); `Negotiated` employee → grade placement 409; `SalaryScale` with notch 5,000 and basis 4,800 → `BasicPayMismatch`; contract create without salary still 201.

**E2 — Benefits, allowances, deductions inside HR.** Blocked on § 7.1 (payroll builds the per-employee components editor and a by-employee read). When it lands, host it as a fourth section of the same tab. Until then the read-only "Payroll items" section stands in.

### Lane F — Teams and committees sub-module · 3 slices (§ 6.6)

**F1 — Terms of reference, objectives, tasks.** Migration: `TeamTermsOfReference`, `TeamObjective`, `TeamTask`, `TeamTaskChecklistItem`, `TeamTaskAttachment`.
**F2 — Meetings, reviews, dashboard, reminders, self-service.** Migration: `TeamMeeting`, `TeamMeetingAttendee`, `TeamMeetingDecision`, `TeamReview`, `TeamReviewLine`; `CompanyHrPolicySettings.TeamTaskReminderLeadDays`.
**F3 — Workflow.** Entity types `HrTeamObjective`, `HrTeamTermsOfReference` through the four-step recipe; no migration beyond the status enums.

Harness `hr-teams/run-f1..f3.mjs`. Details and assertions in § 6.6.

---

## 6. Design notes

### 6.1 The cascading pickers

#### 6.1.1 `OrganizationUnitPicker`

One component, two selects, the `OrganizationScopeFields.tsx` mechanics generalised:

```
props: value, onChange, required, allowNone (label for the none row),
       structureId?          — restrict levels to one structure
       maxLevelNumber?       — parent-of-a-unit case: levels strictly below this number
       minLevelNumber?       — "units at or under this tier" case
       exactLevelNumber?     — locations: exactly this level
       excludeIds?           — self and descendants when re-parenting
       defaultLevelFrom?     — pre-select the level of the current value on edit
```

Level list from `organizationLevelService.getAll()` filtered by the bounds, sorted by
`levelNumber`; unit list from `/summary` filtered by `organizationLevelId`; changing the level
clears the unit. On edit the level is derived from the current value so the user sees where the
parent sits. The data the picker needs that is not on `/summary` today: `levelNumber`,
`structureId` (X-10). For a screen where any unit is valid (most of § 6.1.3) the bounds are
omitted and the picker is simply level → unit.

#### 6.1.2 `LocationPicker`

Same shape over `LocationLevel`/`Location`, with `exactLevelNumber` because the location service
demands exactly one level below (X-11). The level select therefore has one entry; it is still
rendered so the two screens look and behave alike.

#### 6.1.3 Where the flat unit dropdown lives today (the B3 sweep list)

From the survey, grouped by area. Each is a bespoke `Select` fed from the unit list:

- **Organization:** `OrganizationUnitForm.tsx:168-186`, `TeamForm.tsx:222-238`, `UnitRestructureDialogs.tsx:154-167`, `unit-history/page.tsx:106-119` (B1).
- **Positions / employees:** `EmployeePositionForm.tsx:249-277` (B1), `EmployeeForm.tsx:630` (read-only, derived from the position — leave), `hr/employees/[id]/edit/page.tsx`, `me/directory/page.tsx`.
- **Assets:** `AssetForm.tsx`, `assets/register/from-fixed-asset/page.tsx:262`, `assets/report/page.tsx:208`, `assets/transfers/new/page.tsx:234`.
- **Succession:** `CandidateSearchPanel.tsx`, `TalentReviewFormDialog.tsx:131`.
- **Performance:** `unit-goals/page.tsx`, `at-risk/page.tsx`, `cycles/[id]/page.tsx`, `administration/hr/performance/goal-library/page.tsx:97`, `templates/page.tsx:112`.
- **Probation / separation / awards / attendance:** `confirming-authorities/page.tsx:226`, `separation/clearance-form/page.tsx:174`, `awards/types/[id]/page.tsx`, `attendance/alert-rules/page.tsx`, `shift-rotations/[id]/page.tsx`.
- **Employee relations / manpower / movements:** `employee-relations/responders/page.tsx:158, :307`, `manpower-budgets/new/page.tsx:110`, `movements/career-paths/[employeeId]/page.tsx:393`, `movements/new/page.tsx`.
- **SHE:** `safety/inspections/new/page.tsx:164-170`, `inspections/[id]`, `risk-assessments/new` + `[id]`, `safety/documents/[id]`, `safety/training/page.tsx`, `safety/training/plans/[id]`, `safety/performance/analytics`, `InspectionChecklistRun.tsx`.
- **Training (already on `OrganizationScopeFields`):** `training/budgets/new` + `[id]`, `training/plans/new` + `[id]`, `administration/hr/training/compliance/[id]`, `learning-paths/[id]`, `calibration/page.tsx:263` — swap `OrganizationScopeFields` to wrap the new picker so it is one implementation.
- **Travel / leave / recruitment / announcements:** `TravelPolicyForm.tsx`, `TravelRequestForm.tsx`, `LeaveEligibilityTab.tsx`, `leave/plans/page.tsx`, `recruitment/requisitions/new/page.tsx:48` + `[id]/edit` (derived from position — leave), `announcements/page.tsx:129` (multi-select audience — the picker needs a multi mode or this one stays).

#### 6.1.4 Reports-to on the position

Default option set: positions whose `OrganizationUnitId` is the chosen unit or an ancestor of it
(the unit's `Path` gives the ancestor ids in one read). Label `title · unit · code`. A "Show all
positions" toggle widens to the tenant for matrix or dotted-line cases; the server does not
enforce the unit (Q-2, default soft). Server enforces existence, self, and no cycle. The unused
`GET api/EmployeePositions/organization-unit/{id}` endpoint becomes the option source, extended
with `includeAncestors=true`.

### 6.2 Account code from the chart of accounts

- **Column:** `OrganizationUnit.FinanceAccountId` (Guid?, FK → `Finance.Account`, `Restrict`).
  `AccountCode` stays as a **snapshot** of `Account.AccountCode` written on every save — the
  organogram (`OrganogramService.cs:146`) and any report keep reading a string; nothing in
  Finance can be reached by a join from an HR read. Same pair on `Team` (`FinanceAccountId` +
  `CostCenterCode` snapshot).
- **Read door:** `HrFinanceAccountsController` at `api/hr/finance-accounts`, `InternalOnly`,
  returning `{id, accountCode, accountNumber, accountName, accountType, isActive}` with `search`
  and `take`. This exists for the reason `HrCurrenciesController.cs:11-26` records: the Finance
  endpoint is `ViewFinance`-gated by a convention map and an HR user gets 403; granting HR
  `ViewFinance` to fill a dropdown would open ledgers. Account codes and names are not tenant
  secrets; balances are, and this projection carries none.
- **Which accounts:** the picker offers active accounts of any type — Finance's chart decides what
  a "departmental code" is; HR does not second-guess it by filtering on `AccountType`. If Finance
  later marks cost-centre accounts distinctly, the projection gains a filter. Q-1 notes this.
- **Validation:** exists, active, same tenant, else 400 with the account code in the message.
- **Finance owner:** deactivating or deleting an account that an HR unit references is Finance's
  delete guard to build; recorded in § 7.2. HR's `Restrict` FK will make the delete fail loudly
  in the meantime, which is the right failure.
- **Legacy four:** `Division/Department/Section/Unit.AccountCode` stay free text; those tables are
  deprecated in favour of `OrganizationUnit` and are not extended.

### 6.3 The certification model

```
CertifyingBody (exists)
 └── Certification                       — the catalogue row (new)
       CertifyingBodyId      required
       Name, Code            Code unique per body; Name unique per body (filtered on IsDeleted)
       Kind                  enum { Certification, Licence, Permit, Registration }
       Description
       ValidityMonths        int?  — null = does not expire
       RenewalRequired       bool
       ExpiryNotificationLeadDays int? — null = policy default (CompanyHrPolicySettings gains one)
       IsActive

SkillCertification                       — which credential(s) evidence a skill (new)
       SkillId, CertificationId, IsMandatory (default true), Notes
       unique (Tenant, SkillId, CertificationId)
       rule: if Skill.RequiresCertification, at least one row; the form cascades body → certification per row

PositionCertificationRequirement        — what a post must hold (new)
       PositionId, CertificationId, IsMandatory, Notes, Source (Individual | Set), CertificationSetId?
       unique (Tenant, PositionId, CertificationId)
       rule: RequiresCertification / RequiresLicense switches stay; the panel appears when either is on;
             saving with a switch on and no rows is refused ("say which one")

EmployeeCertification                    — what a person holds (new)
       EmployeeId, CertificationId, CertificateNumber, IssuedOn, ExpiresOn (default IssuedOn + ValidityMonths),
       Status computed { Valid, ExpiringSoon, Expired, Revoked } — Revoked is the only stored one
       evidence: FileUploadRecordId / DocumentRecordId / DocumentVersionId / FileName / MimeType / FileSizeBytes
       IsVerified, VerifiedById (Employee FK, from the token — the actor lesson), VerifiedOn, Notes
       unique (Tenant, EmployeeId, CertificationId, CertificateNumber) filtered

EmployeeSkill (exists) + EmployeeCertificationId (nullable FK) — the existing per-skill certification
       columns stay and are shown when the FK is null (lane-3b idiom)
```

**Reads:** `GET api/hr/Employees/{id}/certification-compliance` → for the employee's position:
required (individual + sets), held, expiring, expired, missing — the same shape the guarantor
compliance strip uses. `GET api/hr/reference/certifications?bodyId=` for the cascade.
**Sweep:** `CertificationExpiryReminderService` — candidates = `EmployeeCertification` with
`ExpiresOn` inside lead days, not revoked, employee live; notifies employee, supervisor and HR
through the notification service; dispatch-log checked in the harness by reading the log.
**Screens:** certifying-body page gains a nested "Certifications issued" panel; a flat
*Certifications* setup page for search across bodies; skill form cascade; position panel;
employee tab; Overview strip.
**Recruitment:** `JobQualification` (`JobAnalysisEntities.cs:240`) gains a nullable
`CertificationId` so a job description can name a credential from the same catalogue; nothing
else in job analysis changes. That is one column, included in C2's migration so the two catalogues
do not drift.

### 6.4 Named sets and the duplicate rule

#### 6.4.1 The three masters — one shape

```
BenefitGroup        { Name, Code, Description, IsActive }   BenefitGroupMember        { GroupId, PolicyId }
SkillSet            { Name, Code, Description, IsActive }   SkillSetMember            { SetId, SkillId, RequiredLevel, IsRequired, Priority }
CertificationSet    { Name, Code, Description, IsActive }   CertificationSetMember    { SetId, CertificationId, IsMandatory }
```
Members carry the same attributes the individual position row would (level/priority for skills,
mandatory for certifications) so attaching a set is equivalent to attaching its rows. Benefit
members carry **no** amount or expiry — a position that needs a different amount for one benefit
does not use the set for that benefit (Q-5, default: no per-position override on set members).

Position links: `EmployeePositionBenefitGroup { PositionId, GroupId }`, `PositionSkillSet
{ PositionId, SetId }`, `PositionCertificationSet { PositionId, SetId }`, each unique per pair.

#### 6.4.2 The rule, stated once

On every position save (create, update, sync of any of the three panels):

1. **Effective set** = ∪ members of attached sets ∪ individual rows.
2. An **individual row whose item is a member of any attached set → 400**, message
   *"Skill 'X' is already provided by skill set 'Y'; remove the individual row."*
3. **Attaching a set that contains an already-attached individual → 400**, message naming the row,
   so the user removes the individual first (the PDF's wording: "shouldn't be added as individual
   skill again").
4. **Two attached sets sharing a member → allowed**; the effective read shows both sources.
5. **Duplicate individual rows → 400**, not collapsed (closes X-1, X-2; the `GroupBy().First()`
   idiom is deleted).
6. **Editing a set's membership** after positions have attached it: allowed. A position that now
   holds a redundant individual row is not broken; its next save trips rule 2, and the position
   form marks the row *"covered by set Y — remove"* from the effective read. Q-5 covers whether
   the set editor should instead refuse; default is allow + mark.

#### 6.4.3 Effective reads

`GET api/EmployeePositions/{id}/effective-benefits | effective-skills | effective-certifications`
→ rows `{ item, attributes, source: "Individual" | "Set:<code>" [, "Set:<code2>"] }`. The
position form's three panels render sets above individuals and use the read to grey out covered
items in the individual pickers (the `takenIds` idea widened to set members).

#### 6.4.4 The employee skills tab

`SkillsTab.tsx` reads the employee's position's effective skills first and renders them as a
checklist: **held** (an `EmployeeSkill` exists — shows level, certified, verified), **not held**
(a tick opens the add row pre-filled with the skill and the required level), with the required
level beside each. Below it, "Other skills" is the existing free add. A skill whose catalogue row
has `RequiresCertification` shows the accepted credentials and whether the employee holds one
(`EmployeeCertification` join) — recording without a credential is allowed and flagged, the
compliance read reports it.

### 6.5 The Salary tab

#### 6.5.1 What the embed is, and what it is not

Payroll's window is the dialog at `employee-profiles/page.tsx:659-932`: a *Profile* tab (employee
number, SSF number, monthly basic salary, currency, six flags) and a *Payment methods* grid. It has
no basis history, no components, no loans. It is inline JSX closed over page state (`profileForm`,
`selectedPaymentMethodIndex`, `activeParameters.baseCurrency`, `bankBranches`, `bankCodeValues`,
`exchangeRates`, `runOperation`). "Clean and proper" therefore means:

1. **Extract, do not copy.** `PayrollEmployeeProfileEditor.tsx` under `components/hr/payroll/`
   takes `employeeId` (and optionally a preloaded profile), loads its own lookups through
   `payrollService`, validates payment methods (`validatePaymentMethods`, `:562`) and saves through
   payroll's upsert. The payroll page renders the same component in its dialog — one editor, two
   hosts. This is the single permitted edit to `frontend/src/app/hr/payroll/**` under § 1.2 and
   it removes code from that page rather than adding to it.
2. **Read by employee through HR's door.** Payroll has no `GET employee-profiles/{employeeId}`;
   HR adds `GET api/hr/Employees/{id}/payroll-profile` which calls the existing
   `IPayrollService.GetEmployeeProfilesAsync(employeeNumber)` and returns the exact match. That
   keeps the boundary: HR reads payroll through payroll's service, adds nothing to payroll's
   controller. The by-employee read is still **asked** of payroll (§ 7.1) because the search is
   capped at 250 and a search-then-filter is a stopgap.
3. **The replace-set trap is handled in the editor**, not documented away: the editor always
   sends the complete payment-method list with ids, and refuses to save if its loaded list is
   stale (profile re-fetched before save; a changed row count aborts with a message).
4. **Gate HR's door.** `HR.Compensation.Read` on the read, `HR.Compensation.Write` on the
   tab's save button; payroll's own controller remains as it is (defect #11, recorded, not
   HR's to fix).
5. **Same stack as the tab.** The editor uses react-query for its lookups and the shared
   `apiService` is *not* introduced into payroll's page — the editor calls `payrollService`, so
   payroll's `fetch` wrapper remains the transport in both hosts.

#### 6.5.2 The pay-basis flag

`Employee.PayBasis { SalaryScale, Negotiated }` + `PayBasisNote` (why negotiated: "contract
engagement, rate per agreement of 2026-07-01"). HR owns it because payroll is already
amount-based: the scale is HR's concept (`EmployeeSalaryAssignment` → `SalaryNotch.SalaryAmount`),
and payroll never reads it. Rules:

- `SalaryScale`: grade placement required for a live employee (reported, not blocked — the
  `NoPayBasis` posture); the reconciliation gains `BasicPayMismatch` when the placed notch amount
  differs from `PayrollSalaryBasis.MonthlyBasicSalary`.
- `Negotiated`: grade placement refused (409, "X is paid a negotiated amount; change the pay basis
  to place them on the scale"); the amount entered in the embedded editor **is** the negotiated
  salary; the Overview prints "Negotiated" beside the figure.
- Switching `SalaryScale → Negotiated` closes the open assignment (`EffectiveTo = today`) and
  records the reason; the reverse requires a new placement.
- Independent of `EmploymentType` and `IsOnPayroll`: a permanent employee can be negotiated
  (a retained specialist), a contractor can be on the scale. The PDF's "distinguish contract from
  permanent" is answered by `EmploymentType` (exists) + `PayBasis` (new), two axes.

#### 6.5.3 Tab layout

1. **Pay basis** — the flag, the note, and the current figure with its source (notch amount, or
   payroll basis when negotiated).
2. **Grade placement** — the existing `SalaryAssignmentsTab` content; hidden when `Negotiated`.
3. **Payroll profile** — the embedded editor (basic salary, currency, flags, payment methods);
   read-only banner when `IsOnPayroll` is false, the way the tab behaves today.
4. **Payroll items** — read-only loans, advances, tax reliefs and component exceptions for this
   employee number from payroll's existing GETs; becomes the components editor when § 7.1 lands.

#### 6.5.4 What leaves the contract tab and the create form

Contract tab: salary, currency, pay frequency, tax treatment, withholding rate, pension
applicable, tax exempt. Columns stay (history, settlement reader re-pointed — E-8). Create form:
salary amount and the five payroll flags move to the tab; `isOnPayroll` and the off-payroll
reason stay because membership is HR's fact and `PayrollMembershipService` needs it at create
(Q-3). The import path keeps its salary column (it is the one bulk door).

### 6.6 Teams and committees sub-module

#### 6.6.1 Entities (`Entities/HR/TeamActivityEntities.cs`)

```
TeamTermsOfReference     TeamId, Version, PreviousVersionId, Status { Draft, PendingApproval, Approved, Superseded },
                         Purpose, Scope, Authority, MembershipRules, MeetingCadence, ReportingLine, Deliverables,
                         EffectiveFrom, EffectiveTo, ApprovedById, ApprovedOn, document via upload gate (six columns)
                         — approved versions immutable; "New version" clones to Draft v+1 (the checklist-builder idiom)

TeamObjective            TeamId, Code, Title, Description, Measure, TargetValue, Unit, Weight (sum ≤ 100 per team, advisory),
                         StartDate, DueDate, OwnerMemberId (TeamMember FK), Status { Draft, PendingApproval, Active, OnHold,
                         Completed, Cancelled }, ProgressPercent, ProgressMode { FromTasks, Manual }, OutcomeSummary
                         ("how they did it"), CompletedOn, CancelledReason

TeamTask                 TeamId, ObjectiveId?, Title, Description, AssigneeMemberId, Priority { Low, Normal, High, Urgent },
                         StartDate, DueDate, Status { NotStarted, InProgress, Blocked, Completed, Cancelled }, BlockedReason,
                         CompletedOn, CompletionNotes, SourceMeetingDecisionId? (an action item raised in a meeting)
TeamTaskChecklistItem    TaskId, Order, Text, IsDone, DoneById, DoneAt
TeamTaskAttachment       TaskId, gate columns, Title, UploadedById

TeamMeeting              TeamId, Kind { Meeting, Workshop, SiteVisit, Other }, Title, ScheduledAt, HeldAt, Venue, Agenda,
                         Minutes, Status { Scheduled, Held, Cancelled }, ChairMemberId, minutes document via upload gate
TeamMeetingAttendee      MeetingId, MemberId, Attended, Apology
TeamMeetingDecision      MeetingId, Order, Text, ResponsibleMemberId?, DueDate?, RaisedTaskId?  ("Create task" materialises one)

TeamReview               TeamId, PeriodStart, PeriodEnd, ReviewedById (Employee FK from the token), OverallRating (1–5),
                         Summary, Recommendations, Status { Draft, Submitted, Acknowledged }, AcknowledgedById, AcknowledgedOn
TeamReviewLine           ReviewId, ObjectiveId, ProgressAtReview, Rating, Comment
```
All `TenantEntity`, `TenantId` stamped explicitly in the service (the tenancy lesson), child adds
through the repository not the tracked parent (the checklist-builder lesson), unique indexes
filtered on `IsDeleted` where a soft-deleted row would otherwise hold a slot.

#### 6.6.2 Rules

- **Who may write.** HR (`EmployeeWritePolicy`, the gate `TeamsController` already uses) and the
  team's lead or deputy (`TeamMember.Role ∈ {TeamLead, DeputyLead}`) for everything; a member may
  update the status, checklist and notes of a task assigned to them (ownership helper, plain
  `[Authorize]` on those routes — the "gate vertically, then horizontally" rule). Reads: HR and
  members of the team; `InternalOnly` for the list.
- **Objective progress.** `FromTasks` = completed tasks ÷ tasks under the objective (weighted
  equally); `Manual` = typed. Completing an objective requires either 100 % or an
  `OutcomeSummary` — "whether they have done it, how they did it".
- **Deadlines and monitoring.** `TeamReminderService` (the `ProbationReminderService` shape,
  nightly, per tenant, dispatch-logged): tasks due within `TeamTaskReminderLeadDays` (new policy
  setting, default 3) to the assignee; overdue tasks and objectives to the lead; a meeting tomorrow
  to attendees; a terms of reference reaching `EffectiveTo` within 30 days to the lead and HR.
  `GET api/hr/teams/{id}/dashboard` → objectives by status with progress, tasks overdue / due this
  week / blocked, next meeting, last review rating, ToR status.
- **Evaluation.** `TeamReview` is periodic (quarter, project end), lines per active objective;
  submitted reviews are immutable; the lead acknowledges. Not on the workflow engine — a review is
  a record, not an approval (the goal-approval precedent for keeping something bespoke is not
  invoked; there is simply nothing to approve).
- **Workflow (F3).** `HrTeamObjective` — `Draft → PendingApproval → Active` through the engine, so
  an objective can be routed to the unit head or a sponsor; `HrTeamTermsOfReference` —
  `Draft → PendingApproval → Approved`. Four-step recipe, `Hr` prefix on the entity-type key, the
  single-step-auto-approves trap noted; **ship a default definition in `seed-workflows`** for both
  types — the 2026-09-02 finding was that no HR type had one.
- **Checklist.** `TeamTaskChecklistItem` is a flat tick list on a task. No template engine (Q-7).

#### 6.6.3 Screens

- Team page (`teams/[id]/page.tsx`) gains tabs: **Dashboard · Terms of Reference · Objectives ·
  Tasks · Meetings · Reviews** beside Members / Details / History. Tasks as a board-or-table with
  filters (mine, overdue, by objective). A meeting's decision row has "Create task".
- `/me/teams`: the teams I belong to; a member view of each with my tasks first; the lead sees
  the full tabs. `/me/team` today is the manager's direct-reports page (area 25) — a different
  thing; name the new one `/me/teams` and cross-link.
- Setup nav: nothing new — teams are already under Organization.

### 6.7 Probation, confirmation and the contract row

- **Source of truth for the term:** `EmployeePosition.ProbationPeriodMonths`; fallback
  `CompanyHrPolicySettings.DefaultProbationMonths`. The create form shows it derived and read-only
  (*"6 months, from the position"* / *"6 months, company default"*), with the **expected**
  confirmation date (`DateEmployed + months`) beside it. `Employee.ProbationPeriodDays` stays the
  stored unit (months × 30, the hire-path convention) and is sent by the form as derived; a new
  `Employee.ProbationSource { Position, PolicyDefault, Override }` records where it came from.
  Override is offered only when the position is silent, or to `HR.Probation.Admin` with a reason.
- **Confirmation date:** written by the probation confirm action only. Shown on the Overview.
  Enterable on the form **only in import mode** (existing staff whose probation ended before the
  system) — the same door the staff-number import uses. Both specifiable? No: the term is
  specified, the date is an outcome.
- **Contract row on create** (E-7): the first `EmployeeContractDetail` is opened by
  `CreateWithNumberAsync` with the header's terms, `IsCurrent = true`, `EffectiveDate =
  DateEmployed`, and — for `Permanent` — the `ProbationPeriod` row (enforcement is already keyed to
  `Permanent`, `ProbationService.cs:268`). Header changes to employment type or probation write
  through to the current contract. A movement that changes terms opens a new current contract.
  Manual add closes the previous current (X-4).
- **Contract kind:** if Q-4 adopts `EmployeeContractType`, the contract row carries
  `ContractTypeId` and `ContractEndDate` defaults from its `Duration`.

### 6.8 Geography on sub-records

`EmployeeContact`, `EmployeeEmergencyContact`, `EmployeeGuarantor`, `EmployeeWorkHistory` each
gain `GeoAreaId` (FK → `GeoArea`, Restrict) and `CountryId` where missing; `City`/`Region`
snapshots overwritten from the tree when `GeoAreaId` is set (the `Employee` convention,
`HREntities.cs:102-116`). One shared `GeoSnapshotHelper` lifted from `ApplyGeoAreaSnapshotAsync`.
Each registered in `GeoAreaConsumers.cs` so a geo area in use cannot be deleted. Tabs use
`AddressFields` with `fallback` for countries without a scheme. Dependants stay as they are
(digital address only) — the PDF asks for "appropriate" sub-details, and a dependant's address is
the employee's.

### 6.9 The relationship lookup

```
RelationshipType   Name, Code, Category { Familial, Professional, Other }, SortOrder, IsActive,
                   MapsToDependentRelationship? (enum value, so screens can show one list)
```
Seed: the fourteen `DependentRelationship` values as Familial; Professional: Former manager,
Current manager, Colleague, Subordinate, Client, Business partner, Academic supervisor, Lecturer,
Pastor/Religious leader, Community leader, Family friend (Other), Friend (Other), Landlord (Other).
Adoption: `RelationshipTypeId` (nullable FK) on referee, guarantor, emergency contact and
job-candidate referee; free text kept for old rows. Screen filters: referee → category by
`RefereeType` (Professional → Professional, Academic → Professional, Personal → Familial + Other);
guarantor → all; next of kin → Familial + Other. **Dependants keep the enum** — eligibility logic
(`MaxChildAge` on Son/Daughter) is keyed to it and `ExpatriateFamilyMember` deliberately reuses it.
Work-history supervisor stays two text columns — a previous supervisor is not a relationship the
lookup needs.

---

## 7. Asks to other owners

### 7.1 Payroll owner (the § 1.2 agreement in writing)

Shape: `docs/HANDOFF-PAYROLL-*.md` (what is broken · what was proven · what it blocks · what a fix
needs). Items, in priority order:

1. **`GET api/hr/payroll/employee-profiles/{employeeId}`** — a by-employee read. Today only
   `?searchTerm=` exists, capped at 250 (`PayrollService.cs:2355`). Blocks a clean E1; HR ships a
   search-then-filter stopgap through its own door.
2. **A per-employee components editor + read** (`PayrollEmployeeComponent`). No screen edits it;
   the window sends `employeeComponents: []` (`page.tsx:721`); the type is `any[]`
   (`payrollService.ts:1165`). Blocks E2 entirely — "benefits, allowances, deductions … the same
   page or view in HR" cannot be shown until payroll has a page to show.
3. **Salary-basis history.** `UpsertSalaryBasis` mutates the single row in place (`:13424`);
   `EffectiveFrom` is hard-coded to today by the page (`:717`). A negotiated amount that changes
   on a contract renewal has no history. Not blocking; recorded.
4. **The upsert writes `employee.Overtime` into HR** (`:2385`) — two writers on one HR column
   (X-13). Ask: read HR's flag, do not write it.
5. **Defect #11** (87 bare `[Authorize]` actions) and **#23** (upsert cannot create) stand as
   recorded in `HR-PAYROLL-BOUNDARY.md:120-126, :159`. The embed does **not** rely on #11 —
   HR's door is gated — but the payroll routes it saves through are still open to any internal
   user until payroll gates them.

### 7.2 Finance owner

1. Deactivating or deleting a chart-of-accounts `Account` that an `OrganizationUnit` or `Team`
   references — Finance's delete guard should refuse or warn; HR's `Restrict` FK makes it fail
   loudly meanwhile. Record in `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` as a request, not a
   defect.
2. If Finance ever marks cost-centre accounts (a type or a flag), `api/hr/finance-accounts` gains
   the filter; until then HR offers every active account (§ 6.2).

---

## 8. Corrections owed to other documents

| Document | Row | Correction |
|---|---|---|
| `docs/HR-FINISH-PLAN.md` § 3a | "Referees cannot carry a reference letter — [x]" | Backend only. No screen calls `referees/{id}/letter`. Reopen as **lane A-4 here**. |
| `docs/HR-FINISH-PLAN.md` § 3a | "Guarantor has no … photograph — [x]" | Backend only. No screen calls `guarantors/{id}/photo`. Reopen as **A-5**. |
| `docs/HR-FINISH-PLAN.md` § 3a | "Gender Other has no description field — [x]" and "Hometown absent — [x]" and disability | The screen collects them and the mapper drops them (§ 3.1). The API assertions were green; the product was not. Reopen as **A-1**. |
| `docs/HR-CLOSURE-LEDGER.md` § F | the same four rows | Same correction; add a note that the 3a harness proved the API, not the form. |
| `docs/HR/HR-PAYROLL-BOUNDARY.md` § 1 | off-limits table | Add the § 1.2 amendment: HR may extract and host the employee-profile editor; the payroll page's dialog becomes a host of the shared component. Everything else unchanged. |
| `docs/HR/HR-MODULE-INTEGRATION-MAP.md` | — | New row: HR → Finance `Account` (read-only, unit/team account code). |
| `docs/HR/README.md` | index | This document added (done with this commit). |
| `docs/HR-FINISH-PLAN.md` § "Where to start next" | — | Point at this document's § 5 for the round-2 lanes. |

The first four are corrections to **claims**, not to code; make them when lane A lands so the
plan and the ledger flip in the same commit as the fix.

---

## 9. Open questions, each with a default

| Q | Question | Default if unanswered | Lane |
|---|---|---|---|
| Q-1 | Offer every active Finance account in the unit's account picker, or only a subset (by `AccountType`/category)? | Every active account; Finance's chart decides | B2 |
| Q-2 | Reports-to outside the unit's ancestry: soft (filter with "show all") or hard (server refuses)? | Soft — matrix and dotted lines exist | C1 |
| Q-3 | Move the salary amount and payroll flags off the create form onto the Salary tab, leaving only the on-payroll switch and reason? | Yes — the PDF says all remuneration lives on the Salary tab; the profile is created with no basis and reports `NoPayBasis` until the tab is filled | E1 |
| Q-4 | Surface the dead `EmployeeContractType` lookup (seven TDC rows, with `Duration`) as the contract kind on the contract row, or delete the seed? | Surface it; `Duration` gives `ContractEndDate` a default | D1 |
| Q-5 | Set members with no per-position override (a position needing a different amount for one benefit uses an individual row and therefore not the set for that benefit) — acceptable? And: should editing a set's membership refuse when a position holds a redundant individual, or allow and mark? | No override; allow and mark | C3 |
| Q-6 | Create `Certification` catalogue rows from existing `Qualification` rows of type Certification/Licence by a one-time data pass? | Look at the real rows first; do not automate blind | C2 |
| Q-7 | A generic checklist-template engine shared by teams, SHE, procurement and maintenance? | **No.** Four module-bound checklists already exist; a fifth generic one would be a migration project across other owners' modules. Team tasks get a flat tick list. Revisit if a second HR consumer appears | F1 |
| Q-8 | Team objective and terms-of-reference approvals — who is the default approver in the shipped workflow definition (unit head of the owning unit? HR?) | Owning unit's head, falling back to HR | F3 |
