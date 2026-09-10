# HR demo feedback, round 2 — findings, decisions and build plan

> **Status: LANES A, B1, B2, C1, C2, C3a, D1, D2, E1, E1b and G BUILT (D2 on 2026-09-10)** (A: 88 ×2, `hr-employee-docs/run-round2-laneA.mjs`; B1: 140 ×2, `hr-organization/run-b1.mjs`; C1: 36 ×2, `hr-jobarch/run-c1.mjs`; C2: 143 ×2, `hr-jobarch/run-c2.mjs`; D1: 79 ×2, `hr-probation/run-d1.mjs`; E1: 49 ×2, `hr-payroll-membership/run-e1.mjs`). Lanes B2, B3, C3, D2, E2, F planned, not built. Source: the feedback
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

### 1.6 Salary structure tiers and source (DECIDED 2026-09-09 — not in the PDF)

Raised by the user after lane E1b: a client organisation may run a **three-tier** scale (grade →
level → notch) or a **two-tier** one (grade → notch), and must be able to switch between them; it
belongs with the HR policy settings. Two facts settled the shape. HR's three tables are already a
superset — a two-tier scale is one implicit level per grade, carrying the grade's own code, which
is exactly what the payroll projection has synthesised since lane 3a — so **the tier count is a
policy setting, not a schema change.** And payroll's structure has no level tier at all, so a
three-tier client cannot be served while payroll is the master. Two ways out were put to the user:

- **(a) payroll grows a level tier** — an ask to the payroll owner (§ 7.1 item 6; **not
  decided**, the user will raise it with the payroll owner later);
- **(b) a per-tenant structure source** — `Payroll` (HR mirrors, writes are 409) or `Hr` (HR
  maintains the structure itself; the projection stops; HR's grade/level/notch CRUD opens).

**Decided: (b), built as lane G**, with (a) recorded as the ask. Three-tier is refused while the
source is Payroll, with a message that names both ways out. The 2026 salary scale
(`records shared/ERP Salary Scale 2026.xlsx`) is seeded into payroll's tables with the column
rules the user gave: S1–S3's second (rationalised) column is the 2026 figure; M1–M4's single
"2025" column IS the 2026 figure; M5 is already rationalised; **M4 has no notch 19** and is seeded
as it stands.

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
| S-4 | ~~On the employee's skills tab, show the skills attached to the position (via set or individually) and tick the ones held~~ **BUILT C3b** — `GET Employees/{id}/skill-requirements` and `PositionSkillsCard` at the head of the tab; held / below level / not held, naming the set that asks, with a Record that opens the add dialog pre-filled | `SkillsTab.tsx`, `PositionSkillsCard.tsx`, `EmployeeService.GetSkillRequirementsAsync` | C3b ✅ |

### 2.4 Employee profile

| # | PDF bullet | What exists (verified) | What is missing | Disposition |
|---|---|---|---|---|
| E-1 | Move the gender description box closer to the gender dropdown | Gender at `EmployeeForm.tsx:470-478` (row 2, cell 3 of *Identity & Personal*); "Describe gender" at `:512-516` (row 4, cell 3), conditional on `Other`. Two grid rows apart. ⚠ **And the value is never sent** — see § 3.1. | Placement + the mapper fix. | BUG — lane A |
| E-2a | Confirmation date is not on the create form | Correct: no `confirmationDate` in `EmployeeForm.tsx` or `employeeFormMapper.ts`. It is on `CreateEmployeeDto` (`HRDTOs.cs:255`) and `UpdateEmployeeDto` (`:373`) so the API accepts it. Its only writer is `ProbationService.MarkEmployeeConfirmed` (`ProbationService.cs:372`). Not on the profile Overview either (`[id]/page.tsx:232-234` shows probation days and on-probation only). | See E-2b. | BUILD — lane D1, § 6.7 |
| E-2b | How does it relate to the probation period? Should both be specifiable? Read probation from the position? Disable user input? | Three homes, two units: `EmployeePosition.ProbationPeriodMonths` (`int?`, `:935-939`), `Employee.ProbationPeriodDays` (`int`, default 90, `:169`), `EmployeeContractDetail.ProbationPeriodDays` (`int?`, `:1453`) + its own `ConfirmationDate` (`:1455`). Resolution already prefers the position (`ProbationService.cs:250-263`, `Source = "Position"|"PolicyDefault"`); the hire path converts months×30 (`JobOfferHireService.cs:1461`). Create form shows "Probation (days)" free input (`EmployeeForm.tsx:775-777`). No code derives a confirmation date from hire date + probation. | The rule in § 6.7: position is the source, form shows it read-only with the expected confirmation date derived, confirmation date itself is written only by the probation confirm action, override only where the position is silent. | ADVISED — lane D1, § 6.7 |
| E-3 | Geography selection for the employee address and other sub-details | The main address is already on the tree: `Employee.GeoAreaId` (`:141`), `AddressFields.tsx` used at `EmployeeForm.tsx:566-606`, snapshot written by `ApplyGeoAreaSnapshotAsync` (`EmployeeService.cs:86`). **No sub-record carries `GeoAreaId`**: `EmployeeContact` (`:1049-1077`), `EmployeeEmergencyContact` (`:1082`), `EmployeeGuarantor` (`:1713`), `EmployeeWorkHistory` (`:1349`), `EmployeeDependent` (`:1143`, has only `DigitalAddress`). `ContactsTab.tsx:115-135` uses plain text fields. | `GeoAreaId` + `CountryId` + city/region snapshot on the four address-bearing sub-records, `AddressFields` on their forms, consumer registration for the delete guard (`GeoAreaConsumers.cs`). | BUILT — lane D2, § 6.8 ✅ |
| E-4 | Dependant benefit enrolment modal: names not showing in the dropdown | `DependentBenefitsDialog.tsx:73` builds labels from `p.name`; the endpoint `GET api/hr/benefit-policies/active` returns `BenefitPolicyDto` whose field is `PolicyName` (`BenefitPolicyDTOs.cs:361`) — there is no `Name`. Every other consumer reads `policyName` (`hr/benefits/page.tsx:92-99`). The dialog bypasses the typed service and calls `apiService.get` with a local interface, which is why TypeScript did not catch it. | One-line fix + use `benefitPolicyService.getActive()`. | BUG — lane A, § 3.2 |
| E-5 | With a skills set or individual position skills, the employee skills tab shows those and the user ticks the held ones | Same as S-4. | Same as S-4. | BUILD — lane C3 |
| E-6 | Geography for the work-history record? | `EmployeeWorkHistory.CompanyAddress` is one free-text line, `[MaxLength(200)]` (`:1349-1388`); no country, city or geo. ⚠ Zod allows 300 (`WorkHistoryTab.tsx:20`) — a 201–300 character address 400s on the server. | `CountryId`, `GeoAreaId`, `City` snapshot; `AddressFields`; length fix. | BUILT — lane D2 ✅ |
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
| E-11a | Relationship for referee / supervisor / guarantor defined somewhere and offered as a dropdown | Four free-text `Relationship` columns: referee (`:1668-1670`, 200), guarantor (`:1726-1728`, 100), emergency contact / next of kin (`:1097-1099`, 50), job-candidate referee (`RecruitmentEntities.cs:1009`); plus medical `EmergencyContactRelationship` (`MedicalEntities.cs:907`). Dependants use the `DependentRelationship` enum (`HREnums.cs:240-273`), which `ExpatriateFamilyMember` reuses on purpose (`:40-51`). **No relationship lookup entity, no generic `HrLookup` table** — HR reference data is one table per concept (catalogue in `hr-setup-nav.ts:245-320`). "Supervisor" on work history is two free-text columns (`SupervisorName`, `SupervisorPhone`, `:1378-1382`), no relationship. | `RelationshipType` lookup with a category. | BUILT — lane D2, § 6.9 ✅ |
| E-11b | Differentiate familial from professional relationships, to know which values to populate | Nothing. | `RelationshipCategory { Familial, Professional, Other }` on the lookup; each screen filters by category. | BUILT — lane D2 ✅ |
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
| X-1 | ~~Position form allows the same skill on two rows; server keeps the first silently~~ **FIXED C3a** — refused, not collapsed; the skills picker gained the `takenIds` guard the benefits picker always had, widened to cover what an attached set provides | `EmployeePositionForm.tsx`, `EmployeePositionService.cs` | C3a ✅ |
| X-2 | ~~Duplicate benefits/skills are collapsed, never refused~~ **FIXED C3a.** ⚠ The `GroupBy(...).First()` idiom appears **ten** times, not two: six in the position service, four in the certification service. Only the **desired-side** ones were removed — the four on the **stored** side defend against duplicate rows already in the table, which the unfiltered unique indexes make possible, and deleting those would have been a different bug | `EmployeePositionService.cs`, `CertificationService.cs` | C3a ✅ |
| X-3 | ~~`EmployeePositionBenefitDto.IsActive` exists with no column behind it~~ **FIXED C3a** — dropped, not backed by a column: the row's presence IS its activeness and its absence is the soft delete. The mapper had hard-coded `true` on every read. ⚠ The line number in this table had drifted by ~250 lines by the time the lane ran; resolve these by NAME | `HRDTOs.cs` (by name, not line) | C3a ✅ |
| X-4 | ~~`AddContractAsync` inserts `EffectiveDate = 0001-01-01`, `IsCurrent = true` always; nothing closes the previous current contract~~ **FIXED D1** — one `FileContractAsync` helper every door goes through, plus a migration that repairs the `IsCurrent` every existing row wrongly claims | `EmployeeService.cs:1552-1604` | D1 ✅ |
| X-5 | Work-history zod max (300) exceeds the column (200). **Three findings, not one** — `jobTitle` said 200 against a 100 column and `jobDescription` 2000 against 1000; and `UpdateEmployeeWorkHistoryDto` carried NO lengths at all, so the update 500'd on truncation where the create 400'd. **FIXED D2** | `WorkHistoryTab.tsx:20` vs `HREntities.cs:1351` | D2 ✅ |
| X-6 | ~~`EmployeeContractType` lookup is seeded with seven TDC rows and has no DTO, service, controller or screen~~ **FIXED D1** — surfaced as the contract kind, with its own master screen | `HREntities.cs:1028-1039`, `TdcDemoLegacyOrgSeeder.cs:99-140` | D1 ✅ |
| X-7 | Referee list DTO omits the letter fields; guarantor list DTO omits `HasPhoto` | `HRDTOs.cs:1819-1832`, `:1905-1917` | A |
| X-8 | `GuarantorFormPath` still accepted on the JSON create — a caller-supplied path, the exact shape lane 3a-ii removed elsewhere | `HRDTOs.cs:2037-2038`, `:2098` | A — retire with the document collection |
| X-9 | Bank reads never `Include` `Bank`/`Branch`; branch-in-bank unvalidated | `EmployeeService.cs:2353-2365`, `:2367-2416` | A |
| X-10 | Unit summary DTO has no `LevelNumber`/`StructureId`; the picker cannot rank without them | `OrganizationStructureDTOs.cs:287` | B1 |
| X-11 | Locations require exactly one level below, units allow skipping — two rules for one idiom | `LocationStructureServices.cs:589`, `OrganizationStructureServices.cs:562` | B1 — keep both (documented), the picker reads the rule per family |
| X-12 | `Team.CostCenterCode` and the four legacy `AccountCode` columns are the same free-text problem as O-2 | `OrganizationStructureEntities.cs:245`; `HREntities.cs:558-666` | B2 — `Team` gets the FK; the legacy four are deprecated tables and stay |
| X-13 | Payroll upsert writes `employee.Overtime` back into HR — a second writer on an HR column | `PayrollService.cs:2385` | E — recorded for the payroll owner (§ 7.1) |
| X-15 | **Found during the D1 build, not by the original survey.** `EmployeeMappingExtensions.Apply` wrote `Employee.ConfirmationDate` from the update DTO with **no guard**, so the ordinary employee edit could confirm, un-confirm or re-date anybody — bypassing the confirming authority, the probation record and the letter. Lane 3d guarded the CONTRACT's copy, and that guard reads this column, so the protected copy was the one nothing wrote. **FIXED D1** | `EmployeeMappingExtensions.cs:436` | D1 ✅ |
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

Lane G (salary structure tiers and source, § 1.6) was added on 2026-09-09 between E1b and C3; it
is independent of the rest. **C3 was split into C3a and C3b** the same day (see the lane block);
C3a is built. **D2 was built on 2026-09-10, out of order** — ahead of C3b, which is still owed.
**What remains: C3b, F, B3, E2**, plus lane H on its trigger. **Lane H** (a movement changes the pay but not the placement) was
added the same day out of lane G's closing note, and is deferred with a trigger rather than a
position in the order: **before the next demo**, because Staff Movements is session 14 of the
demo walk. Everything else keeps the order below.

Harness folders under `D:\Rhema\TDC ERPS\dev-harness\` (outside the repo, per the demo-pack
boundary): existing `hr-employee-docs`, `hr-jobarch`, `hr-payroll-membership`; new
`hr-organization`, `hr-teams` and `hr-salary-structure`. Every new table goes into
`dev-harness/hr-demo-smoke/demo-coverage-manifest.csv` or the UAT rebuild gate reports it. Every
new migration is guarded and listed in `FastBuildMigrationMetadata` or it is inert on the fast
build. Every reference-data screen goes into `frontend/src/config/hr-setup-nav.ts` under the
group it belongs to, and the runbook path must name the group.

### Lane A — Bugs and unreachable features · ✅ **DONE 2026-09-09** · 88 assertions ×2

Migration `20260909004030_AddGuarantorIdTypeAndDocuments` (guarded, listed). Harness
`hr-employee-docs/run-round2-laneA.mjs`; lane 3a (47) and 3c (47) re-run green. Three things the
build turned up that the plan did not have:

- **The referee LIST never carried `isContacted`**, so the tab's "Contacted" badge had never shown
  for anyone. Added to the list projection with the letter fields.
- **`hr-employee-docs/setup.mjs` still supplied a staff number** and the register (lane 3b) refuses
  one for permanent staff with 400 — every suite in that folder had been un-runnable since the
  register shipped. The minter now lets the register issue the number. ⚠ Nine other harness
  minters still supply one (`grep -l employeeNumber dev-harness/*/setup.mjs`); each will fail the
  same way the next time it is run and needs the same one-line fix.
- **§1 of the harness reads the frontend source.** It asserts the mapper's keys, the dialog's field
  name, and that each gated route has a caller — the exact shapes that were wrong. Cheap, static,
  and it is the regression that got through lane 3a.

- [x] A-1 `employeeFormMapper.ts`: send `genderDescription`, `hometown`, `hasDisability`, `disabilityDescription` (§ 3.1). Probe: submit the form with all four, read `/details`.
- [x] A-2 `DependentBenefitsDialog.tsx`: `policyName` + typed service (§ 3.2).
- [x] A-3 Gender description adjacent to Gender (§ 3.4).
- [x] A-4 Referee letter: upload + download on `RefereesTab.tsx`; `hasLetter`/`letterFileName` on the list DTO and TS type; `employee.service.ts` gains `uploadRefereeLetter`/`refereeLetterUrl` (§ 3.3).
- [x] A-5 Guarantor photo: same on `GuarantorsTab.tsx`; `hasPhoto` on the list DTO.
- [x] A-6 Guarantor documents: `EmployeeGuarantorDocument` (gate columns, `DocumentTypeId → EmployeeDocumentType`, `Title`, `UploadedById`), `POST/GET/DELETE api/hr/employee-documents/guarantors/{id}/documents`, a documents strip inside the guarantor row. Retire `GuarantorFormPath` from both write DTOs (keep the column, read-only, until the data pass). ⚠ This is a second migration item in the same slice — acceptable, one migration file.
- [x] A-7 Dependant photo and employee photo: upload controls on `DependentsTab.tsx` and the employee Overview header (§ 3.3).
- [x] A-8 Guarantor edit hydration via `loadForEdit` (§ 3.5).
- [x] A-9 Guarantor `NationalIdTypeId` (nullable FK → `IdentificationType`, Restrict) + dropdown; free text kept and shown when the FK is null; DTOs carry both; masking of the number unchanged.
- [x] A-10 Bank/branch dropdowns from `bank.service.ts`; branch ∈ bank validated in `AddBankDetailAsync`/`UpdateBankDetailAsync`; `Include(Bank).Include(Branch)` on both reads; free text remains for rows with no FK, and the form offers "Bank not in the list" that reveals the text boxes.
- [x] A-11 Harness `hr-employee-docs/run-round2-laneA.mjs`: the four mapper fields round-trip through the **screen payload shape**; dropdown label non-empty; each upload door reachable from its tab's service method; guarantor edit hydrates; branch-of-another-bank refused 400; national ID type FK round-trips.

### Lane B — Organization structure · 3 slices

**B1 — The cascading picker, history dates, the initial history row.** · ✅ **DONE 2026-09-09** · 140 assertions ×2 · migration `20260909013533_AddOrganizationUnitHistoryNotes` (guarded, listed) · harness `hr-organization/run-b1.mjs` (new folder, README inside).

What the build changed from the plan, and what it found:

- **One history row per SERIES on create, not one row carrying parent and head together.** The per-series closing rule (a reparent closes the open parent row, a change of head closes the open head row) only stays true if no row belongs to both series. So a unit created with a head gets two rows; a root unit's placement row carries no ids and classifies as `Other`, and the change-log sentence for `Other` now says so. The plan's "PreviousParentId = null, NewParentId = parent, head likewise" is what each row carries, split.
- **A backdated change is refused when it would start before the open row in its series** (400, naming the date and the remedy: correct that row through the new PUT first). The alternative — closing the open row at a date before its own start — would state two arrangements for one day in the wrong order.
- **One `CascadingPicker` implementation, two thin wrappers** (`OrganizationUnitPicker`, `LocationPicker`), plus `HistoryStampFields` shared by the move dialog, the change-of-head dialog and the new `UnitHistoryEntryDialog`. The register's unit filter uses the picker too.
- **Three pre-existing defects the first run exposed, all fixed here:** (1) `OrganizationLevel.IsRootLevel` is computed from the structure's sibling levels and was **false on every read** — `GET api/OrganizationLevel` reported no root in any structure and **no root unit could be created through the API at all** ("Root units must be created under a root level" on every parentless create; the live roots are seeded). The level repository's bare reads now load the sibling levels and the unit service's three root branches ask the repository for the structure's root. (2) The location summary never loaded the level (level name null, tier 0). (3) **Every seeded unit (40 of 41) and location (11 of 11) has an EMPTY `Path`** — the seeders never wrote one — so any path-based descendant test offers a unit its own children. The picker walks `parentUnitId` instead; `Path` is asserted only on API-created rows. ⚠ The seeders still write no path; a backfill (recursive CTE over `ParentUnitId`) belongs with B3 or the next data pass, and the organogram's `Depth` is wrong for seeded rows until then.
- **Not browser-walked.** The forms type-check clean under `tsconfig.hr-slice.json` and the harness reads their source for the shapes that matter, but nobody has clicked through the new picker yet.
- [x] `OrganizationUnitSummaryDto` gains `LevelNumber`, `StructureId` (X-10); `LocationSummary` likewise if absent.
- [x] `frontend/src/components/hr/common/OrganizationUnitPicker.tsx` (§ 6.1.1) and `LocationPicker.tsx` (§ 6.1.2).
- [x] `OrganizationUnitForm.tsx`: parent via the picker with `maxLevelNumber = chosenLevel − 1`; `LocationForm.tsx`: parent via the picker with `exactLevelNumber = chosenLevel − 1`; `UnitRestructureDialogs.tsx` move dialog same; `TeamForm.tsx` owning unit; `EmployeePositionForm.tsx` unit (already cascaded inline — swap to the shared component).
- [x] `CreateAsync` writes the initial history row (`PreviousParentId = null`, `NewParentId = parent`, head likewise, `EffectiveFrom` from the DTO or today, reason from the DTO).
- [x] `EffectiveFrom`, `EffectiveTo`, `ChangeReason`, `Notes` on create/update DTOs and both dialogs; `RecordHistoryAsync` takes them instead of `UtcNow`; validation `EffectiveTo ≥ EffectiveFrom`; a row's `EffectiveTo` is still auto-closed by the next row in its series **unless** the user set it.
- [x] `OrganizationUnitHistoryController`: `POST` (manual entry, `ChangeType = Other`, requires reason) and `PUT {id}` (dates, reason, notes only), both `EmployeeAdminPolicy`; `UnitChangeLog.tsx` gains an edit action and a "Record an entry" button.
- [x] Harness `hr-organization/run-b1.mjs`: create → one history row with the given dates; parent at a lower level refused; level skipped accepted; location parent two levels up refused; manual entry; date edit; `EffectiveTo < EffectiveFrom` refused.

**B2 — Account code from the chart of accounts** (§ 6.2). ✅ **BUILT 2026-09-10 — 28 ×2, `hr-organization/run-b2.mjs`.** Migration `20260910001423_AddUnitFinanceAccount` (two nullable columns, two indexes, two Restrict FKs; guarded, listed). Regression after it: B1 140, C3 74, C1 36.
- [x] `HrFinanceAccountsController` (`GET api/hr/finance-accounts?search=&take=&includeInactive=`), read-only projection `{id, accountCode, accountNumber, accountName, accountType, isActive}` — the `HrCurrenciesController` precedent, same justification comment.
- [x] `OrganizationUnit.FinanceAccountId` and `Team.FinanceAccountId` (Guid?, FK → `Account`, Restrict, no navigation either way); `AccountCode` / `CostCenterCode` kept as the **snapshot**, written by the service on every save.
- [x] Service validation: exists, same tenant, active. Not a history-worthy event — it is not parent or head.
- [x] `FinanceAccountPicker.tsx` on `OrganizationUnitForm.tsx` and `TeamForm.tsx`, storing the id.
- [x] Registered in `HR-MODULE-INTEGRATION-MAP.md` (master table) and `HR-FINANCE-ENTITY-SWEEP.md` § 2b, and § 7.2 updated now the FK is live.
- [x] Harness as specified, including the 200/403 pair and the stale snapshot asserted as design.

**What the build changed from the plan, and what it found.**

1. **A by-id read was needed and is not in the plan** (`GET api/hr/finance-accounts/{id}`). Without it the picker cannot name the account a unit is ALREADY charged to when a search excludes it, or when Finance has since deactivated it — the box would render empty over a real value. It deliberately answers for an inactive account.
2. **⚠ The two doors answered the same mistake with two status codes.** An account id typed wrongly inside a payload gave 400 on the unit door and **404** on the team door, because `TeamsController` documents `ArgumentException → 404`. Both now throw `InvalidOperationException` for an unknown account and answer **400**: a bad reference inside a payload is a bad request, not a missing resource. Caught by the harness, not by reading.
3. **⚠ `Account.Status` SHADOWS `BusinessEntity.Status`** — a string on the base, an `AccountStatus` enum on `Account`. The first cut compared it as a string, which would not compile; had it compiled it would have been silently always-false and let inactive accounts straight through. `AccountDto.Status` *is* a string, so the read door's comparison is right and the service's is not — the same property name meaning two things across a boundary.
4. **An inactive account is refused on assignment but never stripped** from a unit already holding it. Finance deactivating an account must not silently un-charge every unit pointed at it.
5. **The legacy four** (`Division`/`Department`/`Section`/`Unit.AccountCode`) are untouched, as § 6.2 says: deprecated in favour of `OrganizationUnit`, and not extended.
6. **The organogram drawer was not touched.** It reads `AccountCode`, which still holds the code — now Finance's rather than free text — so it keeps working unchanged. Showing code *and name* there needs a name the HR read does not carry, and inventing a field nothing fills is what X-3 was. Left as it is.

**B3 — The picker sweep.** No migration. Replace every flat unit dropdown in § 6.1.3 with the shared picker, screen by screen, each with a screen-payload probe that the chosen id still reaches the API. Split into two halves if it runs long (HR/admin screens, then SHE/training/performance screens). Harness: extend each area's existing UI-payload probe rather than a new one.

### Lane C — Positions, certifications, sets · 3 slices

**C1 — Reports-to filtered and validated.** No migration. · ✅ **DONE 2026-09-09** · 36 assertions ×2 · harness `hr-jobarch/run-c1.mjs`.

Notes from the build: the ancestry is walked over `ParentUnitId` on the server (`GetAncestorsAsync`), **not** read off `Path` as § 6.1.4 proposed — B1 measured every seeded unit's path empty. The position controller now answers refusals with the rule's own sentence (it had let the global middleware collapse every `InvalidOperationException` into a canned one). The cycle refusal names the chain; an existing loop above the target is reported rather than walked forever. The stored reports-to is always offered on edit, even outside the ancestry, so an edit never silently clears it.
- [x] Form: reports-to options = positions in the chosen unit **or any ancestor unit** (walk `Path`), labelled `title · unit · code`; a "Show all positions" toggle for matrix cases; disabled until a unit is chosen (§ 6.1.4).
- [x] Service: `ReportsToPositionId` must exist, ≠ self, and must not create a cycle (walk `ReportsToPositionId` upwards, bounded); on both create and update. Unit mismatch is **allowed** (soft rule) — Q-2.
- [x] Harness `hr-jobarch/run-c1.mjs`: self refused; A→B→A refused; cross-unit accepted; unknown id 404/400.

**C2 — Certification catalogue, skill cascade, position requirements, employee credentials, expiry sweep** (§ 6.3). · ✅ **DONE 2026-09-09** · 143 assertions ×2 · migration `20260909071641_AddCertificationModel` (guarded, listed) · harness `hr-jobarch/run-c2.mjs`.

Six tables, not the five the plan counted: the sweep needs its own run/dispatch PAIR, like every other HR engine. What the build settled, and what it turned up:

- **The switches keep their meaning and gain teeth.** `RequiresCertification` / `RequiresLicense` on the position and `RequiresCertification` on the skill stay as the user's statement of intent; the new rows are what the statement means, and a switched-on record that names nothing is **refused** (400, with the sentence). ⚠ **Measured on DEFAULT after the migration: 5 seeded skills carry the flag with no accepted credential** (0 positions do). Those five cannot be re-saved from the skill form until someone names a credential or turns the flag off — the rule meeting legacy data. The form shows the empty panel and says so; a data pass is not owed, but the friction is real and belongs in the demo runbook if any of the five is walked.
- **Recording is not gating** (the plan's own instruction): an `EmployeeSkill` against a skill that needs certification is **allowed** without one and comes back `isCompliant: false`. The tab shows a "Certification missing" badge. Only the compliance read reports it.
- **The migration's lead-days column takes a REAL default of 60**, not the scaffold's `defaultValue: 0`, and the scaffold's `UpdateData` is gone — it repaired only the seeded row by id and cannot run on the fast EF build at all. A tenant left at zero would be told about a lapsed licence on the day it lapsed and never before.
- **Expiry is the catalogue's default, not its rule**: `ExpiresOn` omitted is computed from `IssuedOn + ValidityMonths`, but an explicit date wins — a body may have issued this one for longer.
- **Verification is somebody else's**: `VerifiedById` comes from the token, and verifying your own credential is refused.
- **A cited credential cannot be deleted** (422 naming how many skills, positions and holders cite it); retiring it is the way, and holders keep it.
- ⚠ **`hr-jobarch/api.mjs` still carried the pre-lane-A `rejects()`**, whose `??` chain stopped at ASP.NET's canned `"One or more validation errors occurred."` and reported a perfectly explicit DataAnnotations refusal as mute. Ported the fixed version across (it only widens what is searched). **The same stale copy is likely in the other harness folders.**
- ⚠ **Not a C2 defect, found while regressing: `hr-jobarch/run-slice0.mjs` crashes** on `POST descriptions/{id}/approve` with 409 *"This tenant approves job descriptions through the workflow engine"*. The guard is from area 17 slice 3; the suite predates the workflow definition being published on this database. Environmental, pre-existing, untouched here.
- **Not browser-walked.** Type-check clean, and the harness reads the screens for the shapes that matter, but nobody has clicked the picker, the tab or the panel.
- [x] `Certification`, `SkillCertification`, `PositionCertificationRequirement`, `EmployeeCertification` (+ `EmployeeSkill.EmployeeCertificationId`).
- [x] Reference API under `api/hr/reference/certifications` (+ `certifying-bodies/{id}/certifications`); position sub-resource `api/EmployeePositions/{id}/certification-requirements`; employee sub-resource `api/hr/Employees/{id}/certifications` with the upload-gate evidence door on `EmployeeDocumentsController`.
- [x] Screens: nested certifications list on the certifying-body page; `SkillForm.tsx` gains a body→certification cascade list shown when `requiresCertification`; `EmployeePositionForm.tsx` gains a certification requirements panel (shown when either switch is on; the two switches stay, the panel gives them meaning); employee `CertificationsTab.tsx`; compliance strip on the employee Overview (required vs held vs expired).
- [x] `CertificationExpiryReminderService` on the `IdentificationExpiryReminderService` pattern; `Certification.ExpiryNotificationLeadDays`.
- [x] Setup nav: *People Reference Data → Certifications*; employee-tab and permission gates on `HR.Employee.*` / `HR.Competency.*` (the skill side).
- [x] Harness `hr-jobarch/run-c2.mjs`: a certification must belong to the chosen body (mismatch 400); a skill with `requiresCertification` and no accepted credential refused on save; an `EmployeeSkill` added against such a skill without an `EmployeeCertification` is **allowed but flagged** (`isCompliant = false`) — recording is not gating, the position compliance read does the reporting; expiry computed from `ValidityMonths` when `ExpiresOn` omitted; the sweep emits for a credential inside lead days (log-checked, not harness-inferred — the lane-1 lesson).

**C3 — Benefit groups, skill sets, certification sets, the duplicate rule, employee skills from the position** (§ 6.4).

⚠ **Split in two on 2026-09-09.** As written this was nine tables, three master screens, three
position panels, the employee skills tab and a harness — two builds in one commit. **C3a** is the
sets end to end; **C3b** is the skills tab, which consumes C3a's effective-skills read and cannot
precede it.

**C3a — the sets, the rule, the effective reads.** ✅ **BUILT 2026-09-09 — 74 ×2, `hr-jobarch/run-c3.mjs`.** Migration `20260909225805_AddNamedSets` (nine tables + one column). Regression after it: C1 36, C2 143, E1 73, membership 86, D1 79, lane G 69.
- [x] `BenefitGroup`/`BenefitGroupMember`, `SkillSet`/`SkillSetMember`, `CertificationSet`/`CertificationSetMember`; `EmployeePositionBenefitGroup`, `PositionSkillSet`, `PositionCertificationSet`. Three parallel classes, not a base class — a shared base would be a TPH hierarchy in EF, and the member rows point at three unrelated catalogues.
- [x] Effective reads: `GET api/EmployeePositions/{id}/effective-{benefits|skills|certifications}` → the union, each line carrying every `source`.
- [x] The rule of § 6.4.2 stated once in `PositionNamedSetService.ValidateAsync`, for all three kinds, before anything is written. X-1, X-2 and X-3 closed.
- [x] Screens: three masters under one shared shell (`components/hr/named-sets/NamedSetPage.tsx`), a list of sets beside the selected set's members; nav under *Jobs & Establishment → Skill Sets*, *People Reference Data → Certification Sets*, *Pay & Benefits → Benefit Groups*. The position form gained an attach strip above each of its three individual lists.
- [x] Demo data: scenario `146-named-sets.mjs` builds three TDC sets and attaches them, and all nine tables are listed `required` in `demo-coverage-manifest.csv`. ⚠ **C2 added neither**, for its own six tables; recorded in § 8 rather than fixed blind, because a `required` row without seeding fails the UAT rebuild.

**⚠ WHAT THE PLAN GOT WRONG, AND IT IS THE WHOLE LANE.** § 6.4.3 describes the effective reads as
a new endpoint for the position form. They are not an extra — they are the feature. Attaching a set
writes an attachment row and **nothing else**; no benefit row, no skill requirement, no
certification requirement is materialised. So every existing consumer that read the individual
table directly would have gone on seeing only the individual rows:

| Consumer | What it read | What would have happened |
|---|---|---|
| `EmployeeBenefitEnrollmentService.ReconcilePositionEnrollmentsAsync` | `EmployeePositionBenefit` | a benefit reaching a post through a group **enrols nobody** |
| `SuccessionCandidateSearchService` | `PositionSkillRequirement` | candidates scored against a shorter list than the post has |
| `CertificationService.GetEmployeeComplianceAsync` | `PositionCertificationRequirement` | a person reads compliant while missing everything a set asks |
| `JobOfferService` (offer benefit seeding) | `position.PositionBenefits` | the offer letter lists none of the package |

All four now read the union. **A green effective-read test with a dead consumer is a feature that
looks finished and does nothing** — which is why the harness's § 5 asserts an actual enrolment row,
not just the read.

**What else the build changed or found.**

1. **A tenth change: `EmployeeBenefitEnrollment.SourceBenefitGroupId`.** The existing provenance link points at an individual position-benefit row; a group-provided benefit has none, so without this the link would silently have gone null. A bare nullable id with **no** FK and no navigation — provenance, not a live reference, so retiring a group or changing its membership cannot be blocked by an enrolment already made.
2. **⚠ Lane C2's switch rule counted the wrong thing.** "A post whose certification/licence switch is on must name at least one credential" counted the INDIVIDUAL rows only, so a post whose whole regulatory bundle arrived through a set was refused for naming nothing. Same shape as the four consumers above, in a rule rather than a read. `SyncPositionRequirementsAsync` now takes the count the attached sets provide. **Found by running the demo scenario, not by the harness** — the harness had only ever attached credential sets to posts with the switch off. Asserted now (3.9–3.11).
3. **The tier refusal comes before the band rules** — as in lane G. The two-tier lesson generalised: when a rule says "this cannot exist here", check it before rules about its shape.
4. **The duplicate rule is one check, not two.** § 6.4.2 asks for separate messages for "individual already in a set" and "set contains an attached individual". At save time both arrive together and there is no way to tell which the user added, so there is one refusal naming the row AND the set and saying which to remove.
5. **Strongest-wins is a decision the plan did not state.** Two sets can require the same skill differently. The union takes the highest level, required over preferred, highest priority; mandatory anywhere is mandatory. A post cannot need a skill *less* because a second set asked for less.
6. **A retired set stays where it is and cannot be newly attached.** Retiring stops it being offered; it does not strip entitlements from posts already on it.
7. **New tables get `HasFilter("[IsDeleted] = 0")` on every unique index**, following C2. The older `PositionSkillRequirements` and `EmployeePositionBenefits` indexes lack it, which is exactly why those two syncs must find a soft-deleted row and revive it instead of inserting.

**C3b — the employee skills tab.** ✅ **BUILT 2026-09-09 — 33 ×2, `hr-jobarch/run-c3b.mjs`.** No migration.
- [x] `GET api/hr/Employees/{id}/skill-requirements` — the post's EFFECTIVE skills set against what the person holds, each line carrying the sources C3a made part of the effective read.
- [x] `PositionSkillsCard` leads the skills tab: held / below the level asked for / not held, the level needed beside the level held, which set asks for it, and a "Record" beside each gap.
- [x] The card's Record opens the tab's own add dialog **pre-filled** with the skill and the required level.
- [x] A held skill whose credential is missing is flagged and not barred — the existing rule, surfaced rather than restated.

**What the build settled that § 6.4.4 did not say.**

1. **Three states, not two.** The plan says "held / not held". Recorded *below* the level the post asks for is neither: it reads `BelowLevel`, counts as held, and does not count as met. Above the level asked for is `Held` — **a requirement is a floor, not a target**.
2. **A preferred skill is never a gap.** The counts and `isCompliant` are over the REQUIRED lines only.
3. **A post that asks for nothing is vacuously compliant**, and the card renders nothing at all — the same rule as the certification compliance card, so a strip saying "nothing to say" does not appear on every profile.
4. **The credential rule is not restated.** The read maps held skills through the same mapper the skills list already uses, so "needs a credential and nothing valid evidences it" is decided in one place.
5. **The shared collection tab learned a ONE-SHOT `prefill`**, not a controlled `open`. The tab owns its dialog; two owners of one boolean is how a dialog ends up flickering or refusing to close. The caller sets a value, the tab consumes it once and calls back so the caller can clear it. Available to every sub-resource tab now, not just skills.

### Lane D — Employee profile · 2 slices

**D1 — Probation, confirmation, contract-on-create** (§ 6.7). **BUILT 2026-09-09 — 79 ×2, `hr-probation/run-d1.mjs`.** Migration `20260909090940_AddProbationSourceAndContractKind`.
- [x] Create form: probation shown read-only with its source and the expected confirmation date; editable only where the position states nothing; **`probationPeriodDays` is sent as `null` when derived**, not as the derived number — see the deviation below.
- [x] `ConfirmationDate` on the Overview (read-only, beside the expected date) and on the create form **only in import mode**.
- [x] `CreateWithNumberAsync` opens the first `EmployeeContractDetail` (number from `INumberSequenceService`, contract kind from `EmployeeContractType`) and, for `Permanent`, the `ProbationPeriod` row.
- [x] `AddContractAsync` sets `EffectiveDate`/`IsCurrent` and closes the previous current row (X-4); `ContractEndDate` and `AnnualLeaveEntitlementDays` exposed on both write DTOs.
- [x] Header write-through, one direction only, skipped once the employee is confirmed.
- [x] `StaffMovementService` opens a contract on a **pay** change, on implement and on temporary-assignment reversal. ⚠ Employment-type conversion cannot be driven from a movement — see below.
- [x] Q-4 adopted: `EmployeeContractType` surfaced with full CRUD, a setup screen and an `IsActive` retire flag.
- [x] Harness `hr-probation/run-d1.mjs`, 79 assertions ×2.

**What the build changed from the design.**

1. **The confirmation-date guard did not exist.** This plan said "guard already exists — assert it stays". It did not: `EmployeeMappingExtensions.Apply` wrote `Employee.ConfirmationDate` straight from the update DTO with **no guard of any kind**, so anyone who could edit an employee could confirm them — bypassing the confirming authority, the probation record and the letter. Lane 3d had guarded the *contract's* copy, and that guard READS this column, so the protected copy was the one nothing wrote and the authoritative one was open. Now: refused on the ordinary create, refused on the ordinary update (echoing the stored value back is allowed, so a round-tripping form still saves), accepted on the import door only.
2. **`CreateEmployeeDto.ProbationPeriodDays` became `int?`.** It was `int` defaulting to 90, so a caller saying nothing about probation was indistinguishable from one asking for ninety days — and 90 is nobody's term at TDC. Null now means "derive it". A value that contradicts the post is **refused with a sentence naming the post and both numbers**, not silently ignored; silent ignoring is the failure this lane exists to fix.
3. **`ContractEndDate` and `EndDate` were given distinct meanings** rather than exposing a duplicate. `ContractEndDate` = scheduled end (defaulted from the kind's duration), `EndDate` = actual end — which is already what `TerminateContractAsync` writes into it. Same for `AnnualLeaveEntitlementDays`, which was written and read by **nothing**: it is now the contract's stated entitlement, beside `VacationDaysPerYear`, which the DTOs already exposed.
4. **The migration is larger than "ProbationSource only"**, and its data half matters more than its schema half. `IsCurrent` defaults to `true` on the entity and **nothing has ever maintained it** — `AddContractAsync` left it at the default on every row, and both termination paths cleared `IsActive` without clearing it. So every contract row in a live database claimed to be the terms in force. Harmless while nothing read it; a terminated contract returned as somebody's current terms the moment D1 made it load-bearing. The migration clears it on deleted rows, on rows the other columns already called finished, and on all but the latest where an employee still holds several. It deliberately writes **no `EndDate`** on those rows: the closing date derives from a successor's effective date, and pre-D1 rows carry `0001-01-01` there.
5. **The "one active contract" refusal was removed.** `AddContractAsync` refused a second contract outright, which is the wrong answer to the commonest thing that happens to terms of employment — they get replaced. Superseding closes the previous row instead. A row filed inactive or expired supersedes nothing, which is the door a backfill goes through; a current row dated *before* the one in force is refused (the lane B1 rule, for the same reason).
6. **The employee create now sets `StaffStatus = Probation`** when it opens a probation row and the caller left the status at `Active` — mirroring the hire path. `IsOnProbation` is computed from `StaffStatus`, and a live probation row beside an `Active` status would tell the benefit rules the person is past it.
7. **The bulk importer no longer ADDS a contract**; it names the one the create opened. Otherwise every imported employee would end with two rows, one closed the day it opened. `ContractNumber` became writable on the update DTO to make that possible.

**What it found, beyond the plan.**

- **`StaffMovement` has no `NewEmploymentType`.** It carries a new position, unit, location, supervisor and pay — nothing else. So a movement can open a contract on a pay change, but the contract→permanent **conversion** the feedback named cannot be driven from a movement without a schema change. Converting somebody today goes through the employment type on the employee header, which writes through. Recorded as owed, not smuggled into this lane.
- **The movement CALL SITE is unasserted.** Getting a movement to `Implemented` needs an approval chain, employee acceptance, a handover and a checklist — a fixture several times the size of the harness. `SupersedeCurrentContractAsync` itself is covered through the manual add.
- **`Employee.CurrentTerms` had been arbitrary.** It reads `IsCurrent`, which nothing maintained, so it returned whichever row EF materialised first.
- Two stale harnesses in `hr-probation` were repaired: `run-lane3d.mjs` was **walking through the confirmation hole** to set up its own fixture (now uses the import door, 35/35), and its D block asserted a create contract retired by lane 3b.
- ⚠ **`hr-employee-import`'s two suites fail on the staff-number register format** — their synthetic numbers do not match the configured `TDC/00001` pattern, so rows come back `Warning` instead of `Ready`. Pre-existing and environmental, the same root cause as `run-lane3d`'s D block; not repaired here because the fix touches that suite's own count assertions.

**D2 — Geography on sub-records, relationship lookup, work history** (§ 6.8, § 6.9). **BUILT 2026-09-10 — 121 ×2, `hr-employee-docs/run-d2.mjs`.** Migration `20260910013025_AddSubRecordGeographyAndRelationshipTypes` (guarded, listed). Re-run green in the same pass: lane 3a (47), lane 3c (47), lane A (88), D1 (79), E1/E1b (73), payroll membership (86), and all four `reference-geography` suites (42 / 25 / 31 / 15) — the last of those because this lane refactored the address-snapshot helper that Location, CompanyProfile and HealthcareFacility all use.
- [x] `GeoAreaId`, `CountryId` (where absent), `City`/`Region` snapshots on `EmployeeContact`, `EmployeeEmergencyContact`, `EmployeeGuarantor`, `EmployeeWorkHistory`; `AddressFields` on their tabs; four consumers registered in `GeoAreaConsumers.cs`; snapshot helper shared.
- [x] `RelationshipType` + seed (27 rows); `RelationshipTypeId` on referee, guarantor, emergency contact, job-candidate referee; free text kept and MIRRORED; each screen's set enforced on the write (§ 6.9).
- [x] Work history: `CompanyAddress` length aligned — and two more found beside it (X-5).
- [x] Harness `hr-employee-docs/run-d2.mjs`.

**What the build changed from the design.**

1. **The shared helper is `GeoAddressSnapshot`, and it absorbed a fifth rule the plan did not have — the country.** § 6.8 said "one shared `GeoSnapshotHelper` lifted from `ApplyGeoAreaSnapshotAsync`". Lifting it exposed that the four existing copies had **already drifted**: `EmployeeService` and `CompanyProfileService` wrote the region, `LocationStructureServices` and `MedicalServices` silently dropped it. All four now call the one helper (its region setter is nullable, which is what the two region-less tables need). And nothing anywhere checked the area against the `CountryId` stored beside it, so an employee could be filed as living in Nigeria while pointing at a Ghanaian district — with `City` and `Region`, written from the area, then contradicting the country on the same row. `ReconcileCountryAsync` refuses the contradiction and **fills the country in when it is silent**; `IGeographyService.GetCountryForAreaAsync` is the one hop that makes it possible. Applied to the employee's own address too, not only the four new ones.
2. **`EmployeeEmergencyContact.Relationship` was widened 50 → 100.** It was the narrowest of the four columns spelling out the same idea (referee 200, guarantor 100, candidate 100), its create DTO carried **no length at all**, and the screen's schema allowed 100 — so a 51-character relationship reached SQL Server and failed as a truncation 500. It also had to hold a `RelationshipType.Name`, which is 100.
3. **X-5 was three findings, not one.** `companyAddress` said 300 against a 200 column, `jobTitle` 200 against 100, `jobDescription` 2000 against 1000. Worse, `UpdateEmployeeWorkHistoryDto` carried **no `[MaxLength]` on any string**, so the payload the create refused with a 400 the update passed straight to SQL Server as a truncation 500 — one payload, two doors, two different answers, neither of them the screen's. Every length is now stated on both DTOs and in the zod schema.
4. **The free text is mirrored, not replaced.** Sending a `RelationshipTypeId` makes the server overwrite the record's own `Relationship` column with the catalogue row's name, so every existing consumer — letters, exports, the list projections — keeps working with no change, and a caller may still send only words. ⚠ **Renaming a catalogue row does not rewrite the records already pointing at it**: the record is a statement of what was said at the time.
5. **The clear-flag convention splits by DTO, not by lane.** The guarantor and referee update DTOs mean "not supplied" when null, so they gained `ClearGeoArea` / `ClearRelationshipType` (the `ClearNationalIdType` shape lane A established). The contact, emergency-contact and work-history DTOs are already full-replace on their address fields, so a bare null clears there. Both are asserted, because guessing wrong means an emptied picker saves successfully and changes nothing.
6. **The referee's accepted set is computed from the ENTITY on the update, not the DTO.** An edit that changes only the referee type has to re-examine the relationship already on the row, and one that changes only the relationship has to check it against the kind already stored. A create-only check is the shape lane 3d met on the confirmation date.
7. **The catalogue can be deleted, not only retired — while nothing names it.** Both acts exist, and the delete is refused with a 422 and a count of the records standing in the way. A soft delete would be worse than a hard one here: it releases nothing, and the row would vanish from every read while live records still held its id.
8. **A professional row cannot carry a dependant mapping.** The dependant enum has no professional members, so the pairing would be one nothing could ever use — refused, not silently dropped, for the reason lane D1 gave about the probation form.

**What it found, beyond the plan.**

- **Four more geography columns needed four more `IGeoAreaConsumer` probes**, and the foreign keys give them nothing: geography deletes are SOFT, so the constraints are never consulted. Without the probes an area in use would have deleted cleanly and taken a next of kin's address, a guarantor's and a previous employer's with it — the exact loss measured on 2026-09-03.
- **`EmployeeWorkHistory` had no country, city or geography at all** (E-6), so "who worked at a Tema firm" was unanswerable and the whole address was one 200-character line.
- **The candidate referee is the only consumer outside the employee record**, and its rule is the tightest: professional and other only. A candidate may name a pastor or a family friend; they may not name their mother.
- **The harness's first run found two faults in ITSELF, and both are worth naming.** § 3 deliberately unlinks the address and the guarantor it made, to prove the two clear paths; § 7 then asserted the delete guard counted all four consumers, against a fixture set that had only two left. The refusal was right and the assertion was wrong — a message that enumerates only the consumers actually holding rows is the correct behaviour. And the candidate fixture was minted as the SuperAdmin `admin` login, which is **not employee-linked**, so the create was refused before any rule under test ran. Both are the shape that produces a red that looks like a product defect; § 7 now makes fresh linked rows, and the candidate is minted by the employee-linked TenantAdmin actor.
- ⚠ **`RelationshipField` clears a selection the screen no longer accepts**, because the referee tab's accepted set is *live* — it depends on the referee type. Without it, switching a professional referee to Personal would leave "Former manager" in the form invisibly and the save would 400 from a field that looked empty.
- ⚠ **The same rule answers 400 on the employee doors and 422 on the candidate door**, and neither is this lane's defect. Recruitment carries a `RecruitmentBusinessRulesAttribute` that maps a business-rule refusal to 422 **with the rule's own message** — and all **nine** `*BusinessRulesAttribute` filters in the codebase do the same, so 422 is the house answer for "the request is well-formed, the data will not allow it". `EmployeesController` has a hand-rolled `ToClientError` that predates those filters and answers 400. The harness asserts what each door actually says rather than papering over it: making one door lie about its own area's convention to match the other would be worse than recording that two conventions exist. **Not fixed here** — changing `ToClientError` would move every refusal on every employee sub-resource endpoint and re-baseline a dozen harnesses. Recorded for a closure sweep. (Same shape as E1's finding 1, one level up: there it was one door with two codes, here it is one rule with two doors.)
- **`ErpSystem.Core.Enums.RelationshipType` already existed, and was dead.** It collided with the new table on the first build. Its members — "Internal - Same Department", "External - Clients", "External - Regulators" — describe how far outside the organisation a job description's working relationships reach, which is a different idea entirely from how one person is tied to another. It had **no typed consumer anywhere**: no property, no parameter, no column, no frontend reference (job descriptions record their relationships through `ReportingRelationshipType` instead, which is why it was never wired up). **Renamed to `WorkingRelationshipScope`**, with the reasoning on the enum, rather than threading a `using` alias through the seven files that touch either name. ⚠ Nothing was serialized against the old name and no data carries it — verified before the rename, and that verification is the only thing that made it safe. `JobReportingRelationship.RelationshipType` is a *property* of type `ReportingRelationshipType` and is untouched.

### Lane E — Salary tab · 2 slices (§ 6.5)

**E1 — The embed, the pay-basis flag, the contract tab trimmed.** **BUILT 2026-09-09 — 49 ×2, `hr-payroll-membership/run-e1.mjs`.** Migration `20260909133438_AddEmployeePayBasis`.
- [x] `PayrollEmployeeProfileEditor.tsx` extracted into `components/hr/payroll/`; payroll's page hosts it in its dialog (683 lines out of that page, its employee search kept). Own lookups through `payrollService`; own save through payroll's upsert; the replace-set guard inside it (every method with its id; re-read before save, abort on a changed row count).
- [x] `GET api/hr/Employees/{id}/payroll-profile` (Compensation.Read) through `IPayrollMembershipService.GetPayrollProfileAsync` — payroll's search by staff number, filtered to the exact employee.
- [x] `Employee.PayBasis` + `PayBasisNote`; `PUT {id}/pay-basis` (Compensation.Write) — its own door, not the create or the ordinary update; `RequireOnScaleAsync` on both placement writes; `BasicPayMismatch` in `IssueFor`, the reconciliation counter, the row (both figures) and the frontend tiles.
- [x] `SalaryTab.tsx`: Pay basis → Grade placement (hidden while negotiated) → Payroll profile (the editor, read-only off payroll, save gated on Compensation.Write) → Payroll items.
- [x] Contract dialog trimmed of the seven pay fields (the request type made them optional; the list keeps a "Salary (as recorded)" history column). `SeparationService.DailyRateAsync` reads `ResolveMonthlyBasicPayAsync`; the contract salary is the fallback for rows that predate this.
- [x] Create form: the amount and the five switches removed; membership + reason stay (Q-3).
- [x] Harness, 49 ×2. Membership suite re-run at its original 84; D1 at 79.

**What the build changed from the design.**

1. **Placement-while-negotiated refuses with 400, not the 409 written above.** The off-payroll refusal on the *same* endpoint — the same shape of rule — is a 400 through `ToClientError`. One door answering one kind of refusal with two codes seemed worse than the plan's number.
2. **The editor's client-side guard is built but not harness-asserted.** An API harness cannot drive a React component. `run-e1.mjs` asserts the guard's precondition (HR's door returns every payment method with its id) and says the guard itself is read, not run.
3. **`BasicPayMismatch` fires only from a placed NOTCH.** A level mid-point is an estimate and the record's flat figure is what payroll was seeded from; neither is a claim that payroll is wrong. A notch is — somebody put the person there on purpose.
4. **`HrBasicPay` became pay-basis aware and returns its source in words** (`HrBasicPaySource` on the status DTO): notch / level mid-point / record figure on the scale; payroll's basis, else the record figure, when negotiated. A negotiated person's placement is deliberately never consulted — a stale one left from before the switch must not resurface as their pay.
5. **The mapper stopped sending the five switches at all.** It used to re-send them on every employee edit, so an edit of a phone number could re-assert `PayTax`/`SSFund` values the Salary tab had since changed. Absent means untouched on the update DTO; the request type made them optional.
6. **The flat `Employee.Salary` stays on the create DTO** for the import path, exactly as § 6.5.4 said; only the form stopped sending it. A person created from the form is on payroll with no basis and the reconciliation says `NoPayBasis` — asserted (D1–D3 of the harness).

**What it found, beyond the plan.**

- **The same-day placement edge.** Closing a placement runs "as of yesterday" and keeps the window non-negative, so a placement that starts TODAY is closed on its own start date and still reads as in force for the rest of today (`hasActiveSalaryAssignment` true; the reconciliation's as-of read includes it). The model cannot express "made and withdrawn the same day" without a negative window or a delete. Pre-existing — the off-payroll flip closes the same way — and now shared through `CloseOpenSalaryAssignmentsAsync`. Asserted as the limit it is (`run-e1.mjs` B18–B21), not hidden; the negotiated figure ignores the row regardless.
- **EF scaffolded `defaultValue: 0` for `PayBasis`, and 0 is not an enum member** — the D1 `IsActive` trap again, from a different direction. Every existing employee would have carried a basis nothing names, and the mismatch rule (`== SalaryScale`) would have been false for all of them. `DEFAULT (1)` in the migration.
- Tax reliefs and component exceptions have **no employee filter on payroll's routes** (loans and advances do). The tab filters those two client-side; recorded under § 7.1 as the by-employee reads payroll still owes.
- Two stale fixtures repaired in `hr-payroll-membership`: `api.mjs`'s actor minter sent a staff number on the ordinary create (repointed to the import door); `run-payroll-membership.mjs`'s `base()` sent both a number and `probationPeriodDays: 90` against a six-month post — the latter refused by **D1's** rule, correctly.

**E1b — Withdrawal of a grade placement becomes a fact of its own.** **BUILT 2026-09-09 — 73 ×2, the same `hr-payroll-membership/run-e1.mjs` (section E); membership suite 86 ×2.** Migration `20260909161346_AddSalaryAssignmentWithdrawal`.

Not in the original plan. It came out of explaining E1's "same-day placement edge" to the user, which turned out to be the harmless instance of a real defect.

**The defect.** One date interval was carrying two questions — *when were these terms in force* and *was this row withdrawn*. Withdrawal was expressed by pulling `EffectiveTo` back to yesterday, which cannot be done to a placement that has not started without ending it before it begins; so the code clamped the end to the row's own start date. **A one-day window in the future is not a closed placement — it is a scheduled one.** A promotion booked for 1 October and withdrawn on 15 September matched the as-of predicate again *on 1 October*, and `EmolumentService` — and through it benefit enrolment — read that notch as basic pay for the day.

- [x] `EmployeeSalaryAssignment.WithdrawnAt` + `WithdrawnReason`. The window now says only when the terms were in force; a placement withdrawn before it took effect carries no window at all.
- [x] **Four copies of the as-of predicate had drifted**, and two of them (`GetCurrentAsync`, the `IsActive` projection) never asked whether the placement had *started* — so a future-dated placement read as "current" and rendered "Active" today. All four now agree; `IsInForceOn()` is the in-memory twin beside the projections, and the query sites still spell it out because EF cannot translate a method.
- [x] `AssignSalaryAsync` had a third instance: two placements sharing an effective date — the ordinary way a mistake is corrected — ended the first the day before the second, i.e. before it began. Withdrawn instead.
- [x] **There were two basic-pay resolvers and E1 only fixed one.** `EmolumentService` had its own copy that had never heard of `PayBasis`, so a negotiated employee's benefit contribution came from a notch HR had said was not their pay. One `HrBasicPay.Resolve` now serves the reconciliation, emoluments and the separation settlement.
- [x] The Salary tab distinguishes Active / Scheduled / Ended / Withdrawn, and prints the withdrawal reason.
- [x] Migration repair, **measured first**: 10 live placements, 6 carrying the clamp's fingerprint, 4 still able to fire, 0 negative windows — all 4 harness litter on deleted fixtures. The repair is a heuristic (`EffectiveTo = EffectiveDate` cannot be told from a deliberate one-day placement), so it is scoped to rows that can *still* fire plus any negative window. Past clamped rows are left alone: rewriting history to tidy a flag is the worse trade.

⚠ `run-payroll-membership.mjs` § 4 was **asserting the defect** ("placement is closed (effectiveTo set)") and was rewritten to assert withdrawal.

**E2 — Benefits, allowances, deductions inside HR.** Blocked on § 7.1 (payroll builds the per-employee components editor and a by-employee read). When it lands, host it as a fourth section of the same tab. Until then the read-only "Payroll items" section stands in.

### Lane F — Teams and committees sub-module · 3 slices (§ 6.6)

**F1 — Terms of reference, objectives, tasks.** Migration: `TeamTermsOfReference`, `TeamObjective`, `TeamTask`, `TeamTaskChecklistItem`, `TeamTaskAttachment`.
**F2 — Meetings, reviews, dashboard, reminders, self-service.** Migration: `TeamMeeting`, `TeamMeetingAttendee`, `TeamMeetingDecision`, `TeamReview`, `TeamReviewLine`; `CompanyHrPolicySettings.TeamTaskReminderLeadDays`.
**F3 — Workflow.** Entity types `HrTeamObjective`, `HrTeamTermsOfReference` through the four-step recipe; no migration beyond the status enums.

Harness `hr-teams/run-f1..f3.mjs`. Details and assertions in § 6.6.

### Lane G — Salary structure tiers and source · 1 slice (§ 1.6) · ✅ **BUILT 2026-09-09** · 69 ×2, `hr-salary-structure/run-g.mjs`

Migration `20260909204655_AddSalaryStructurePolicy` (two columns on `CompanyHrPolicySettings`,
guarded, with the entity's own defaults — EF scaffolded `defaultValue: 0` for both enum columns,
the third time this round). Regression after it: `run-e1.mjs` 73, membership 86, `run-d1.mjs` 79.

- [x] `CompanyHrPolicySettings.SalaryStructureTiers { GradeAndNotch = 2, GradeLevelAndNotch = 3 }`
  and `SalaryStructureSource { Payroll = 1, Hr = 2 }`, defaults two-tier + Payroll — what every
  tenant already was. Both on the policy DTOs and the policy screen ("Salary structure" card).
- [x] Policy validation (`CompanyHrPolicySettingsService`): three-tier under Payroll is refused;
  a switch to two-tier is refused while any active grade holds more than one active level, and
  the refusal names the grades.
- [x] Source = HR stops the projection: `EnsureCurrentAsync` returns without touching anything;
  `ReconcileAsync` (the sync endpoint) answers 409. **Nothing is deleted on the switch** — the
  mirrored rows simply become HR's rows, and the read no longer overwrites them (asserted F8, F9).
- [x] Grades, levels and notches controllers: every write goes through the service when HR is the
  source and stays 409 otherwise; the 409 text now says how to lift it. Argument → 404,
  InvalidOperation → 400 (`ClientError`).
- [x] `CreateGradeAsync` creates the grade's implicit level in two-tier (code, name and band of
  the grade, sequence 1); `CreateLevelAsync` refuses a second level in two-tier.
- [x] `EmployeeService.ResolvePlacementLevelAsync` on both placement writes: a notch names its
  level (a notch of another grade, or of a level other than the one supplied, is refused); in
  two-tier a placement that names only the grade resolves to its one level.
- [x] `HrBasicPay` names the level only when it is a real tier ("notch 20 of M1", not "notch 20,
  level M1 of M1").
- [x] `TdcSalaryScaleSeeder` — the 2026 scale, **185 notches generated from the workbook** (M1–M3
  ×20, M4 ×22 without notch 19, M5 ×22, S1–S3 ×27), written into **payroll's** tables as an
  ensure, so the five invented notches from demo-smoke scenario 141 are corrected rather than
  duplicated. A demo-orchestrator step ("Salary scale 2026") keyed on M1 notch 20.
- [x] Frontend: `SalaryAssignmentsTab` hides the level picker in two-tier and sets the sole level
  itself; new *Pay & Benefits → Salary Structure* screen (grades → levels in three-tier → notches),
  read-only with "Refresh from Payroll" under Payroll, full CRUD under HR.

**What the build changed from the design, and what it found.**

1. **The seed lives in `seed-hr-demo`, not `seed-hr-all`.** It is demo data for the DEFAULT
   tenant, so it sits with the workforce and benefits steps; the reference-data orchestrator does
   not run it. The first attempt used the wrong command and seeded nothing.
2. **The tier refusal must come before the band rules.** `CreateLevelAsync` first refused a
   second level in two-tier as "salary ranges cannot overlap" — true of any band that fits the
   grade, and beside the point. The two-tier check now runs straight after the grade lookup.
3. **In three-tier the implicit level holds the whole grade band.** A second level cannot be
   added until the first is narrowed — the standing no-overlap rule, not a lane G one. The harness
   narrows it (G2a) and asserts the second lands. The screen's users will meet the same rule; the
   overlap message says what to do.
4. **Two demo-orchestrator steps re-run on every pass** ("Benefit enrolments and enterprise data",
   "HR awards report definitions"): their skip keys do not hold. Not lane G's, harmless in effect
   (their seeders are ensures) — recorded here for the demo-pack owner (§ 8).

**Recorded, not built.**

- **The hire-from-offer placement write does not go through the level resolver.**
  `JobOfferHireService.cs:1584` builds the `EmployeeSalaryAssignment` by hand and copies the
  offer's level and notch straight across, so a two-tier offer naming a notch and no level
  produces a placement with an empty level, and nothing checks the notch belongs to the grade
  (which is the offer's level's grade, else the position's default — the two need not agree).
  Pay is unaffected: the notch amount is what `HrBasicPay` quotes either way. **Lane H.**
  ⚠ An earlier note here said "hire *and movement*". The movement half was wrong and is
  corrected in lane H — movements never write a placement at all, which is the larger finding.
- **Switching the source back from HR to Payroll** re-enables the projection, which adopts by
  code: an HR-authored amount on a code that payroll also has is overwritten on the next read,
  and a grade only HR knows is left alone, as the projection has always left rows it does not
  recognise. The
  harness only asserts the restore path. A client that has authored in HR should not switch back
  without reading that sentence; the policy screen's help text says so.
- **Option (a)** — payroll grows a level tier — stands as § 7.1 item 6 for the user to raise.

---

### Lane H — A movement changes the pay but not the placement · 1 slice · **DEFERRED 2026-09-09, before the next demo**

Found while explaining lane G's level-resolver note to the user, and it is not what that note
said. **`StaffMovementService` never writes an `EmployeeSalaryAssignment` at all.** It resolves
four repositories — `Employee`, `EmployeeCareerPath`, `EmployeePosition`,
`EmployeePositionHistory` — and borrows `IEmployeeService` for one thing only,
`SupersedeCurrentContractAsync` (the contract seam D1 built, `:833` and `:1049`). The movement's
`NewSalaryGradeId` / `NewSalaryLevelId` / `NewSalaryNotchId` land in the career-path history row
and nowhere else, and no pay resolver reads that table.

**So a promotion moves the position, the unit, the manager, `Employee.Salary` and the contract,
and leaves the grade placement saying the old grade.** For anyone whose pay basis is the salary
scale, `HrBasicPay.Resolve` reads the placement's notch first, so the stale notch is quoted and
it *shadows* the new figure the movement just wrote onto the employee record. The person reads as
promoted everywhere except in what they are paid.

**Why it is not urgent enough to interrupt lane C3, and not comfortable enough to leave open.**
Pre-existing, and untouched by lane G — deferring makes nothing worse. But **Staff Movements is
session 14 of the demo walk** (`HR-UAT-DEMO-PRESENTATION-PLAN.md:277`, 30 minutes, promotion and
transfer and secondment), and round-2's feedback came from an audience watching exactly that
closely. Open the Salary tab after the demonstrated promotion and the old grade is on screen.
**Trigger: before the next demo, not after lane E2.**

**Not yet investigated, and the slice starts here.** Whether the process expects HR to correct the
placement by hand on the Salary tab after a movement. If it does, the gap is a missing prompt, not
a missing write. Nothing has been run; the finding is from reading the service's dependencies.

**The decision the slice needs** (user's, and the two answers build different things):

1. **A movement's pay change writes the placement itself**, through `AssignSalaryAsync` so it
   inherits lane G's level resolver, lane E1b's withdrawal rule and the payroll-membership gate.
   Symmetrical with the contract seam D1 already built, and the temporary-assignment reversal has
   to unwind it the same way the contract does.
2. **The movement refuses to implement a pay change until the placement is updated**, naming the
   Salary tab. Safer, and it makes an HR officer look at the notch rather than having one chosen
   for them; worse as a demo, because implement now has a wall in it.

Recommendation: **(1)**, with the movement's notch as the source and a refusal only where the
movement names a notch that does not belong to the grade it names. Then hire-from-offer
(lane G's remaining bullet) is routed through the same door in the same slice, and every placement
in the system is written by one method.

Harness: `hr-movements/` exists. Assert the promotion end to end — placement withdrawn and
reopened, `HrBasicPay` quoting the new notch, the reversal unwinding both rows.

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
6. **A level tier between grade and notch** (§ 1.6, option (a)). Payroll's structure is
   `PayrollGrade` → `PayrollGradeNotch` only. A client on a three-tier scale (grade → level →
   notch) cannot be served while payroll is the master; HR ships the per-tenant structure source
   (option (b), lane G) so such a client maintains the scale in HR and payroll reads nothing.
   **Not decided** — the user will raise it with the payroll owner. If payroll grows the tier,
   the projection gains a level mapping and the three-tier-under-Payroll refusal is lifted; the
   policy setting stays.

### 7.2 Finance owner

1. Deactivating or deleting a chart-of-accounts `Account` that an `OrganizationUnit` or `Team`
   references — Finance's delete guard should refuse or warn; HR's `Restrict` FK makes it fail
   loudly meanwhile. Record in `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` as a request, not a
   defect. **LIVE since 2026-09-10 (lane B2)** — the FK now exists, so this is no longer
   hypothetical. HR's own half is done: an inactive account is refused on assignment and left
   alone where it is already assigned, so Finance deactivating one cannot silently un-charge a
   unit. What Finance still owes is the message: today a delete fails as a constraint error.
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

| `dev-harness/hr-demo-smoke/demo-coverage-manifest.csv` | — | ⚠ **Lane C2's six tables are absent** (`Certifications`, `SkillCertifications`, `PositionCertificationRequirements`, `EmployeeCertifications`, and the two expiry log tables), and so is any entry in `scenarios/005-employee-master.mjs`'s coverage array. The rule agreed 2026-09-04 (`AGENT-BRIEF.md:8-18`) says every entity a user can create carries at least one seeded row. **Not fixed by C3a on purpose**: a `required` row with no seeding fails `Invoke-UatDemoScenarios.ps1:245` and takes the whole UAT rebuild down, and it could not be confirmed from a dev database whether a fresh rebuild seeds them. Confirm against a rebuilt UAT database, then add the rows and the seeding together. C3a's own nine tables were added WITH their scenario (`146-named-sets.mjs`). |

The first four are corrections to **claims**, not to code; make them when lane A lands so the
plan and the ledger flip in the same commit as the fix.

---

## 9. Open questions, each with a default

| Q | Question | Default if unanswered | Lane |
|---|---|---|---|
| Q-1 | ~~Offer every active Finance account in the unit's account picker, or only a subset (by `AccountType`/category)?~~ **DECIDED, BUILT B2**: every active account, no `AccountType` filter. Finance's chart decides what a departmental code is and HR does not second-guess it; if Finance later marks cost-centre accounts distinctly, the projection gains a filter and nothing else changes | Every active account; Finance's chart decides | B2 ✅ |
| Q-2 | Reports-to outside the unit's ancestry: soft (filter with "show all") or hard (server refuses)? | Soft — matrix and dotted lines exist | C1 |
| Q-3 | ~~Move the salary amount and payroll flags off the create form onto the Salary tab, leaving only the on-payroll switch and reason?~~ **DECIDED, BUILT E1**: the form sends neither; the create DTO keeps `Salary` for the import path; `NoPayBasis` is the posture until the tab is filled | Yes | E1 ✅ |
| Q-4 | ~~Surface the dead `EmployeeContractType` lookup (seven TDC rows, with `Duration`) as the contract kind on the contract row, or delete the seed?~~ **DECIDED, BUILT D1**: surfaced, with full CRUD, a *People Reference Data → Contract Types* screen and an `IsActive` retire flag (there is no delete — contracts name their kind by FK). `Duration` defaults `ContractEndDate`. | Surface it; `Duration` gives `ContractEndDate` a default | D1 ✅ |
| Q-5 | ~~Set members with no per-position override … and should editing a set's membership refuse when a position holds a redundant individual?~~ **DECIDED, BUILT C3a**: both defaults taken. A benefit group member carries no amount and no expiry, so a post needing its own figure takes that benefit individually; and a set's membership can be edited freely afterwards — the post is not broken, its next save trips the duplicate rule and the effective read marks the row meanwhile | No override; allow and mark | C3a ✅ |
| Q-6 | Create `Certification` catalogue rows from existing `Qualification` rows of type Certification/Licence by a one-time data pass? | Look at the real rows first; do not automate blind | C2 |
| Q-7 | A generic checklist-template engine shared by teams, SHE, procurement and maintenance? | **No.** Four module-bound checklists already exist; a fifth generic one would be a migration project across other owners' modules. Team tasks get a flat tick list. Revisit if a second HR consumer appears | F1 |
| Q-8 | Team objective and terms-of-reference approvals — who is the default approver in the shipped workflow definition (unit head of the owning unit? HR?) | Owning unit's head, falling back to HR | F3 |
