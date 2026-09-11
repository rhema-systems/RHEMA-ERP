# HR demo feedback, round 3 — findings, decisions and build plan

> **Status: LANES Q (32 ×2), P1 (17 ×2), H (33 ×2) and S (65 ×2, `hr-payroll-membership/run-s.mjs`; E1 73, membership 86, H 33, D1 79 after) BUILT 2026-09-11. Fifteen slices remain; J1 next.** Source: the feedback document *HR Demo
> Changes – 101026* (4 pages; sections Employee Details, Job Description, Staff Unions, Staff
> Requisition, Recruitment), brought by the user on 2026-09-11 after the third HR module demo.
> Every bullet of that document is accounted for below — as a bug, a build item, a decision, a
> default, or a record — with the code it lands on. If a bullet is missing, that is an error in
> this document.
>
> **Vetting block.** Every "what exists" claim was verified against the working tree on
> 2026-09-11 (branch `hrdev`, head `a61d80bf`) by three read-only code surveys plus direct reads.
> Line numbers drift; resolve every reference by NAME before acting on it. Read § 1 (decisions)
> before § 4 (the lanes); the lanes are sequenced on those decisions.
>
> **Companions.** `HR-DEMO-FEEDBACK-ROUND-2-PLAN.md` (round 2 — lane H there is still owed and is
> slice 3 of this round's order), `HR-DEMO-FEEDBACK-ROUND-2B-RECRUITMENT-PLAN.md` (R8 is unchanged),
> `HR-PAYROLL-BOUNDARY.md` (lanes S and X reach payroll only through payroll's own service),
> `HR-WORKFLOW-ENGINE-INTEGRATION.md` (the recipe lane S uses, and TRAP 5),
> `../HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md` (the answer R8 waits on).

---

## 0. How to read this document

| Section | What it holds |
|---|---|
| § 1 | The eight decisions the user made on 2026-09-11 and the thirteen defaults taken |
| § 2 | The register: every PDF bullet, what exists (file:line), the gap, the lane |
| § 3 | Defects the survey found that the PDF did not name, each fixed inside a lane |
| § 4 | The build plan: nineteen slices, sequenced, with migrations and harness locations |
| § 5 | Design notes for the slices that need more than a form change |
| § 6 | Asks to other owners (Finance, payroll) |
| § 7 | Records owed to other documents |
| § 8 | Verification |
| § 9 | Open questions, each with a default |

House rules that bind every slice: the user runs `dotnet build` and scaffolds migrations (the agent
edits them, reads every scaffolded default against the entity initialiser — an enum `defaultValue:
0` has shipped four times — and lists them in `FastBuildMigrationMetadata`); the agent stages, the
user commits; every slice's harness (`D:\Rhema\TDC ERPS\dev-harness\`) is green twice in Staging
with the JWT key; every new column a user can fill is in `demo-coverage-manifest.csv` with seeding.

---

## 1. Decisions (user, 2026-09-11)

| # | Decision | Effect |
|---|---|---|
| D-1 | **A salary change is a new `EmployeeSalaryChangeRequest` on the workflow engine, and an approved request applies itself.** | Kinds: Placement (grade/level/notch), NegotiatedAmount, PayBasisSwitch. On Approved the service writes HR's side (placement through `AssignSalaryAsync`, pay basis through the existing path, the record figure) AND payroll's monthly basic through payroll's own `UpsertEmployeeProfileAsync` — the full profile is read back through `GetEmployeeProfilesAsync`, only the basis changes, the replace-set is carried through unchanged. If the payroll call fails the request lands on `AwaitingPayrollEntry` and says so on the tab. A new policy setting `SalaryChangeRequiresApproval` (default on) turns the three direct doors into "raise a request". Movements (round-2 lane H) and hire-from-offer keep writing directly: they are already approved. Lane S. |
| D-2 | **Banks: wait for Finance to build a bank master. No HR build this round.** | The PDF's premise — "the banks are set up in Finance" — is not true today. Finance has `BankAccount` rows (the company's own accounts, GL-linked, bank name free text — `Entities/Finance/BankAccount.cs:9`) and no bank catalogue. Payroll's payment methods use payroll's own `BNK` code values and `PayrollBankBranch`; payroll's paying account is `PayrollCompanyBanker`, unlinked to Finance. HR's `EmployeeBank`/`EmployeeBankBranch` is the only bank master with branches, an API and a setup screen. The ask is written (§ 6); `EmployeeBankDetail` and the bank tab stay as they are. |
| D-3 | **Allowances and deductions on the Salary tab: editable if a probe proves payroll's bulk save is per-employee-safe, otherwise read-only with a deep link and an ask.** | Lane X starts with the probe of `POST /hr/payroll/component-exceptions/bulk`. An HR read door `GET api/hr/Employees/{id}/payroll-component-exceptions` over payroll's tables (the grade-projection precedent) is built either way. |
| D-4 | **Employee profile: grouped navigation and ALL record tabs, read-only.** | Lane T, three slices, frontend only. |
| D-5 | **Drop the three contract leave columns** (`AnnualLeaveEntitlementDays`, `VacationDaysPerYear`, `SickDaysPerYear`). | Migration `DropContractLeaveColumns`. The reserved trio (`EffectiveDate`, `ContractEndDate`, `IsCurrent`) is untouched; `WorkingHoursPerWeek` and `WorkSchedule` stay — they are contract terms. Lane P3. |
| D-6 | **Talent pool: keep the flat pool with segments; enrich the segment** (owner, purpose, optional target position / job family) and give it a sidebar entry point. | Lane V. One tenant pool with segments is the common shape; no pool header. |
| D-7 | **Gender and Age criteria: keep, never mandatory, flagged.** | Lane K: `IsMandatory` refused for those two types on the server; the criteria panel carries a non-discrimination note; the vacancy shows a derived "uses a protected-characteristic criterion" flag. |
| D-8 | **A union contact is an employee OR an external person, with a role.** | Lane U: `UnionContact` rows. |

**Defaults taken** (overridable; repeated in § 9 with the reasoning):

- **D-9** The job's intrinsic value becomes DERIVED (Σ qualification values + Σ competency values). The typed `RoleIntrinsicValue` input goes; the benchmark stays typed; responsibilities get no value column this round.
- **D-10** Job description qualification `Title` and competency `CompetencyName`: the rule is *catalogue id OR text*. The text column stays NOT NULL and is mirrored from the catalogue name when blank — no migration.
- **D-11** The suggested salary grade stays a system output. A new `ProposedSalaryGradeId` (+ note) is the user's editable choice, defaulting to the suggestion; the position's actual grade is shown beside both.
- **D-12** Copying a job description to another position copies everything except reporting relationships; `PositionId` and `StaffLevelId` come from the target; the title is unchanged (no "(Copy)"); Draft; version = next for the target.
- **D-13** Pipeline stage flags: `IsRequired` ⇔ `!CanSkip`. One switch on the form ("Can be skipped"); the other is derived; both are enforced. `MaxAttempts` is shown only when `CanRepeat`.
- **D-14** A pre-employment check's service provider is a Procurement `Supplier` through the existing HR read door, plus an HR-side `PreEmploymentCheckProviderService` table (SupplierId × CheckType) so the type → provider cascade works without editing Procurement. The free text stays as a snapshot.
- **D-15** The candidate's national ID is the employee's trio (`NationalIdTypeId` → `IdentificationType`, `NationalIdNumber`, `NationalIdExpiryDate`), carried into the hire path. "Ghana Card" is a seeded identification type, not a column.
- **D-16** A `Language` master joins HR reference data (ISO 639 seed including Ghanaian languages); the candidate language row gets `LanguageId`, name mirrored. Employee-side languages are recorded as a follow-on, not built.
- **D-17** `ReferenceLetter` and `IdDocument` leave the generic candidate-document dropdown: a reference letter attaches to a `JobCandidateReferee` row, an ID scan attaches to the national-ID trio; generic documents gain `Description`.
- **D-18** Announcements get a seeded `NotificationTopic` (`HrAnnouncement.Published.Staff`; in-app on, email and SMS off — an administrator flips them in the notification setup screen, exactly as the PDF suggests). Recipients are the announcement's own resolved audience.
- **D-19** Sub-detail modal headers: title = `Edit dependent — Kofi Mensah` (the sub-record's own label when it has one); the description line = `On Ama Mensah's profile`. Both names, the title stays short.
- **D-20** The requisition's location is a standalone optional field (a position carries no location). The fix is display plus a default from the organisation unit's location when one exists.
- **D-21** Costs → Finance: unchanged from round 2b. The PDF's four questions, answered: (1) HR's approval of a cost is HR's step on the cost (R7, built); Finance's approval and payment is the AP hand-off (R8, waits on the Finance owner). (2) The supplier is chosen on the cost; a payee that is a Supplier goes to AP, a person payee (a reimbursed candidate) is paid outside AP (Q-R6). (3) It is not "always": it is per cost, decided by the payee kind. (4) It is not on the requisition's workflow — the requisition approves the *ask*, the cost approves the *spend*, Finance approves the *payment*.

---

## 2. Register — every PDF bullet

### 2.1 Employee details

| # | Bullet | What exists (verified) | Gap | Lane |
|---|---|---|---|---|
| E-1 | Changing a salary should go through workflow | Three ungated writes on `SalaryTab.tsx` (pay basis `:165-326`; placement via `SalaryAssignmentsTab`; payroll's monthly basic via `PayrollEmployeeProfileEditor.tsx:424-485` → `POST /hr/payroll/employee-profiles`). `UpdateEmployeeDto.Salary` still writes (`EmployeeMappingExtensions.cs:472`) — only the form mapper omits it. `SalaryReviewProposal` is on the engine (`HrProposalWorkflowStatusAdapters.cs:26-75`) but `MarkAppliedAsync` (`SalaryReviewProposalService.cs:218-236`) writes nothing to pay, and it is not on the profile. | D-1 | **S** |
| E-2 | Show allowances/deductions on the Salary tab, from `hr/payroll/allowances-deductions-exception` | That page is component-first (one component, every employee), everything inline (`page.tsx`, 872 lines). Entity `PayrollEmployeeComponent` (`PayrollEntities.cs:1370-1400`). Payroll's read `GetEmployeeComponentExceptionsAsync` (`PayrollService.cs:3386-3411`) filters by component ONLY. The tab already pulls the tenant's whole list and filters client-side (`SalaryTab.tsx:331-358`). | D-3 | **X** |
| E-3 | The employee's name in the header of every sub-detail modal | `ResourceCollectionTab.tsx:408-411` composes `Edit ${singular}`; no name reaches it; 17 tabs go through `EmployeeSubResourceTab.tsx` (`components/hr/employee/tabs/`). | `EmployeeProfileContext` (id, name, number) provided by `[id]/page.tsx`; `EmployeeSubResourceTab` reads it and passes new optional `subjectLabel` + `itemLabel(item)` props to `ResourceCollectionTab` (additive; ~30 non-employee callers untouched). D-19. | **P1** |
| E-4 | Remove duplicate details from the contract (sick days / vacation days per year) | `HREntities.cs:1670-1674`; writers `EmployeeService.cs:2288, 2360, 2220`; readers = the contract's own mapper `:939-941` and `ContractsTab.tsx` only; leave entitlement lives in `LeaveType` / `LeaveCategoryAllocation` / `LeaveBalance`; the ledger's probe found 0 of 24 rows off the default. | D-5 | **P3** |
| E-5 | Disability as a setup: tick → dropdown, notes beside; same for dependants | Free text on `Employee` (`:89-92`) and `EmployeeDependent` (`:1303-1306`); no catalogue anywhere (the medical module's `DisabilityStatus` is a severity, unrelated). Pattern: `RelationshipTypeEntity.cs` + `RelationshipTypesController.cs` + `administration/hr/relationship-types/page.tsx` (`ResourceListPanel`) + `RelationshipTypeSeeder` under `seed-hr-all`. | `DisabilityType` catalogue (name, code, category, sort, active; consumer-count delete guard); `DisabilityTypeId?` on both records, the description kept as notes; both forms: tick → dropdown + notes; the Overview shows it. Seed from the Persons with Disability Act categories. Migration `AddDisabilityTypes`. | **P2** |
| E-6 | Banks read from Finance; the company's paying banks; reconcile or retire HR's bank details against payroll's | See D-2. Also: the membership bridge's fallback writes a payroll bank method with NO bank data (`PayrollMembershipService.cs:360-372`) — the employee's HR account never reaches payroll. | D-2: asks only, § 6. | record |
| E-7 | More profile tabs from every sub-module; a cleaner tab layout | 19 tabs in one scrolling strip (`[id]/page.tsx:210-234`). By-employee reads already exist for assets, leave, discipline, training, appraisals, goals, movements, medical, travel, awards, probation, separation, benefits, attendance, orientation, succession (§ 5.3 lists the routes). None is surfaced. | D-4 | **T1–T3** |
| E-8 | Announcements: must staff log in to see them? Email / SMS through the notification setup? | `HrAnnouncementService.PublishAsync:215-245` writes the row and logs — no topic, no dispatch; pull-only on `/me/announcements`. The platform has `NotificationTopic` with per-channel switches and recipient rules; `AssetReminderService.EnsureTopicsAsync:529-568` is the seeding pattern; the audience resolver is `IHrAudienceResolver`. | D-18 | **P1** |

### 2.2 Job description

| # | Bullet | What exists | Gap | Lane |
|---|---|---|---|---|
| J-1 | Auto-specify details from the selected position (staff level already is) | Only `staffLevelId`, and only on the frontend (`JobDescriptionForm.tsx:182-189`); `JobAnalysisService.CreateAsync:478-589` never loads the position. `JobQualification.CertificationId` (`JobAnalysisEntities.cs:263`) is on NO DTO, mapper or TS type — unreachable. | `GET api/hr/job-analysis/positions/{id}/description-prefill` → title, summary, staff level, a ReportsTo relationship, competencies from `IPositionNamedSetService.GetEffectiveSkillsAsync`, qualifications from `GetEffectiveCertificationsAsync`; the form applies it on position select (create) and marks the rows "from the position"; `CertificationId` exposed end to end. | **J1** |
| J-2 | Intrinsic value = the sum of the values on qualifications, competencies, skills; benchmark typed | `ValueRoleAsync:1764-1838`: total = Σ qualification `MonetaryValue` + Σ competency `MonetaryValue` + the typed `RoleIntrinsicValue`; midpoint averaged with `IndustryBenchmarkSalary`; band ±10 %. No points model exists. | D-9 | **J2** |
| J-3 | Make the qualification "Title" and the competency text optional | `[Required]` on both DTOs (`JobAnalysisDTOs.cs:531/568`, `:623/661`), `[Required]` on the entity's `CompetencyName :324`, zod `min(1)` in both panels; each row also carries a catalogue id (`QualificationId`; `CompetencyId` / `SkillId`). | D-10 | **J1** |
| J-4 | Copy a job description to another position | `POST descriptions/{id}/clone` (`JobAnalysisController.cs:197-203`, `CloneAsync:1016-1122`) keeps `PositionId` (`:1041`) and appends "(Copy)"; the "Duplicate" button (`[id]/page.tsx:307-318`) has no dialog. One approved description per position is enforced at approval (`ApplyApprovalConsequencesAsync:814-836`). | `CloneJobDescriptionDto { TargetPositionId? }`; D-12; a dialog with a position picker. | **J1** |
| J-5 | Suggested salary grade not working; user-editable beside it | `ValueRoleAsync:1796-1803`: containment on `SalaryGrade.MinSalary..MaxSalary`, else nearest by MIN only, never null. On this tenant (two-tier, payroll projection) the grade min/max are very likely 0 → the first grade always. The API accepts `SuggestedSalaryGradeId` on create/update while the form deliberately hides it (`JobDescriptionForm.tsx:595-602`). | Confirm the 0..0 hypothesis on the database first; matcher = grade band, falling back to the grade's notch range, null + a sentence when nothing fits; D-11; show the position's own grade. Migration `AddJobDescriptionProposedGrade`. | **J2** |

### 2.3 Staff unions

| # | Bullet | What exists | Gap | Lane |
|---|---|---|---|---|
| U-1 | Contact = an internal employee, with a role; external too? | `Union.ContactPerson/ContactEmail/ContactPhone` free text (`UnionEntities.cs:23-29`); no membership entity; `EmployeePicker.tsx` exists. | D-8: `UnionContact { UnionId, EmployeeId?, ExternalName, Email, Phone, Role, IsPrimary }`; the three legacy columns are mirrored from the primary. | **U** |
| U-2 | Upload the CBA; other documents; a logo | `CollectiveBargainingAgreement.DocumentReference` is free text (`:59-61`) with no upload behind it; no union upload category. Pattern: `HrAttachmentUpload.ExecuteAsync` / `HrDocumentDownload.ServeAsync` / `AttachmentsPanel.tsx` (the requisition attachment, `StaffRequisitionsController.cs:526-585`, is the template); logo = `PhotoDialog.tsx` `GatedPhoto`. | The file triple on the agreement; `UnionDocument` (kind, optional `AgreementId`) on the gate; `Union.Logo*` triple; category `hr-union-documents`. Migration `AddUnionContactsDocumentsLogo`. | **U** |

### 2.4 Staff requisition

| # | Bullet | What exists | Gap | Lane |
|---|---|---|---|---|
| S-1 | Details not populated in the detail view: job description, location | `jobDescriptionId` is NEVER sent by the create or edit form (`new/page.tsx:46-68`, `[id]/edit/page.tsx:82`; no field in `RequisitionFormFields.tsx`), so the detail's "Job description" row is always "—". `GetByIdAsync` → `WithSummaryNavigations()` (`StaffRequisitionRepository.cs:56-63`) includes six navigations while the DTO reads ten: `LocationLevel`, `OrganizationLevel`, `ReplacementForEmployee` and `CancelledBy` come back null, so the replacement name and the whole cancellation block never render. `description`, `expectedOfferDate` and `targetStartDateReason` are mapped and never rendered. Location IS wired (`RequisitionFormFields.tsx:200-214`) but optional and undefaulted. | A job-description picker defaulting to `GetCurrentVersionForPositionAsync`; the four includes; render the three fields; D-20. | **Q** |
| S-2 | Does submitting with costs raise a request to Finance? vendor → payment? always or dynamic? on the workflow? | R6/R7 built (cost approval, payee, Finance currency); no AP hand-off (`VendorInvoiceId` / `ApHandoff` absent from HR); the Finance owner has not answered `../HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md`. | D-21. R8 stays triggered by the answer. | record |

### 2.5 Recruitment

| # | Bullet | What exists | Gap | Lane |
|---|---|---|---|---|
| R-1 | Where is the talent pool created? Is segment-only standard? | Flags on `JobCandidate` (`RecruitmentEntities.cs:787-812`) + the `CandidateTalentSegment` catalogue (`:1147-1162`: name, description, colour) + memberships. Succession has a real `TalentPool` header, for employees. | D-6. Migration `AddTalentSegmentOwnership`. | **V** |
| R-2 | The candidate's picture does not show at the HR view | The HR endpoint exists (`GET api/job-candidates/{id}/photo`, `JobCandidateController.cs:93-112`); the HR page renders NO photo at all; `downloadPhoto` is a dead client method (`recruitment-pipeline.service.ts:196`); the careers profile page has no photo widget either; no candidate DTO says whether a photo exists. Pattern: `PhotoDialog.tsx` (`useGatedImage`, `GatedPhoto`, `PhotoPanel`). | `hasPhoto` on the detail and list DTOs; `GatedPhoto` on the HR detail and list; `PhotoPanel` on the careers profile. | **C2** |
| R-3a | Ghana Card number on the candidate | Nothing on `JobCandidate`; the employee's trio is at `HREntities.cs:2111-2132`. | D-15 | **C1 / C2** |
| R-3b | Salary currency as a dropdown | `ExpectedSalaryCurrency` string(10); the careers input is plain text (`careers/profile/page.tsx:541`); `hrCurrencyService` is HR-gated; `PublicRecruitmentController` already serves anonymous catalogues (`:129` skills, `:143` qualifications). | `GET api/public/catalogue/currencies`; one shared `CurrencyPicker.tsx` replacing five ad-hoc selects; used on the careers and HR candidate forms. | **C2** |
| R-3c | Qualification as a dropdown from the setup; factor in the type | Hybrid `QualificationId?` + `QualificationFreeText` (`:928-960`); the HR form has the picker (`CandidateSubResourceTabs.tsx:114-142`), the careers form has free text only (`:567-579`). | Careers: qualification type → catalogue filtered by type → free text only under "Other". | **C2** |
| R-3d | Skills as a dropdown; certification name shown only when ticked, with a number | `SkillId?` + `SkillName` + `IsCertified` + `CertificationName` (`:1033-1063`); no number, issuer or expiry; the careers form is free text (`:581-593`). | `CertificationNumber`, `CertifyingBody`, `CertificationExpiryDate` on `JobCandidateSkill`; a skill picker on careers; the certification fields appear only when ticked. | **C1 / C2** |
| R-3e | A language setup so the candidate picks from a dropdown | `JobCandidateLanguage.LanguageName` free text (`:1068-1081`); NO `Language` master anywhere; no employee-side language; HR cannot view or edit candidate languages (no tab, no endpoint) although the `Language` criterion scores against them. | D-16; HR languages tab + endpoints on `JobCandidateController`. | **C1 / C2** |
| R-3f | Reference letter and ID document out of the Documents dropdown; a description on uploads | Enum members 7 and 8 (`HREnums.cs:2050-2078`); no `Description` on `JobCandidateDocument` (`:1097-1126`) or its DTO. ⚠ `careers/documents/page.tsx:44` defaults the type to `'CV'`, which is not a member: the first upload is refused with 400 unless the dropdown is touched. | D-17; `Description`; the default fixed. | **C1 / C2** |
| R-4 | How is the applicant's source and advert channel set correctly? | `JobApplication.Source` is hard-coded to `CompanyWebsite` by the careers client (`careers.service.ts:154-167`); `JobPostingId` is never assigned by any apply flow; the public board lists vacancies, not postings; no HR "record an application" screen (the client method has no caller); no update DTO, so a source cannot be corrected. `JobPostingChannel` and `ApplicationSource` overlap and are not reconciled. | The careers URL carries the posting; the apply and draft payloads carry `jobPostingId`; the source is derived from the posting's channel through a mapping; `UpdateJobApplicationSourceDto`; an HR "Record an application" form; the detail shows the posting and the source. | **A** |
| R-5 | Check the applicant scoring against the shortlisting criteria | `EvaluateCriterion` (`JobApplicationService.cs:890-1051`). The `default` arm passes with full marks, mandatory included (`:1027-1034`); a blank Language, Location or Gender value passes; no criteria at all scores 100 (`:659-678`); Certification has no catalogue path; values split on comma only; Gender ignores the match strategy; numeric partial credit is asymmetric; `JobVacancy.AutoShortlistMinScore` is mapped and never read; the panel's default strategy is `Contains` while the server's is `Exact`. | All nine fixed. `Other` cannot be mandatory and scores 0, not 1; no criteria → not auto-shortlistable. | **K** |
| R-6 | "Required" vs "Can be skipped"; show "Max entries" only when re-entry is allowed | `RecruitmentPipelineStage:474-515`. `IsRequired` and `CanSkip` are enforced NOWHERE (`SkipStageAssignmentAsync:1095-1108` never reads the flag); `CanRepeat` and `MaxAttempts` are enforced (`ApplicationPipelineService.cs:136-155`). Form `PipelineStagesPanel.tsx:333-341`. | D-13; XML docs; a skip refused unless `CanSkip`; the final stage refused while a required stage was never entered. | **G** |
| R-7 | Pre-employment service provider from a setup; cascade by check type | `PreEmploymentCheckItem.ServiceProviderName` and `PreEmploymentCheckTemplateItem.DefaultServiceProvider` are free text (`:2524`, `:2624`); `HrSuppliersController` + `SupplierPicker.tsx` are already used one panel over. | D-14. Migration `AddPreEmploymentCheckProviders`. | **G** |
| R-8 | Accepted values only from the HR setups; dynamic by criterion type | `RequiredValue` is a comma-separated string(500); `VacancyCriteriaPanel.tsx:494-509` is a free-text input. | `JobShortlistingCriteriaValue { CriteriaId, Kind, ReferenceId, Label }` child rows; the panel offers a multi-select from the Qualification / Skill / Certification / Language masters per type, the enum for Gender, numeric for Age and Years, text for Location and Other; scoring matches ids first, labels second. Migration `AddCriteriaCatalogueValues`. | **K** |
| R-9 | Move "Shortlist approval" and "Candidate notifications" below the tabbed section | `screening/page.tsx:251-275` (the two cards) sits above `Tabs` at `:277`. | Move the grid after the tabs. | **Q** |

---

## 3. Defects the survey found that the PDF did not name

1. **`UpdateEmployeeDto.Salary` still writes the record figure** — the form stopped sending it (round-2 E1) but the server never refused it. Lane S closes it under the policy setting.
2. **The requisition detail's cancellation block can never render** — `CancelledBy` is not included on the read the page uses. Lane Q.
3. **`JobQualification.CertificationId` is unreachable** — a column with an FK and no DTO. Lane J1 (the lane-3b shape again).
4. **The grade matcher never returns null and compares midpoints against minimums only.** Lane J2.
5. **The careers document upload defaults to a type that does not exist** (`'CV'`). Lane C2.
6. **Every self-service application is recorded as `CompanyWebsite`** regardless of the advert. Lane A.
7. **A vacancy with no criteria auto-scores every applicant 100**, so "auto-shortlist by score" admits everyone. Lane K.
8. **A mandatory `Other` criterion cannot disqualify** — the default arm passes it. Lane K.
9. **`CanSkip` is documented as a rule and enforced nowhere**; `IJobApplicationServices.cs:257` claims it is. Lane G.
10. **HR cannot see a candidate's languages** while the Language criterion scores them. Lane C2.
11. **The membership bridge's fallback payment method carries no bank** — recorded under D-2, not fixed (the bank master decides the shape).

---

## 4. The build plan — nineteen slices, in order

| Order | Lane | Migration | Harness | Why here |
|---|---|---|---|---|
| 1 | **Q** — requisition detail fixes + the screening card move · ✅ **DONE 2026-09-11 · 32 ×2** | none | `hr-jobarch/run-q.mjs` | demo-visible bugs, half a day |
| 2 | **P1** — modal subject labels, the announcement topic · ✅ **DONE 2026-09-11 · 17 ×2** | none | `hr-employee-docs/run-p1.mjs` (announcement half); the modal half is a screen walk | quick, demo-visible |
| 3 | **H** (round 2) — a movement writes the placement · ✅ **DONE 2026-09-11 · 33 ×2** (logged in the round-2 plan, lane H) | none | `hr-movements/run-h.mjs` | S's "already-approved writers" rule needs it; owed before the next demo regardless |
| 4 | **S** — the salary change request · ✅ **DONE 2026-09-11 · 65 ×2** | `20260911091344_AddEmployeeSalaryChangeRequest` (+ `CompanyHrPolicySettings.SalaryChangeRequiresApproval`) | `hr-payroll-membership/run-s.mjs` | the PDF's first bullet |
| 5 | **J1** — prefill from the position, optional text, clone to another position, `CertificationId` | none | `hr-jobarch/run-j1.mjs` | |
| 6 | **C1** — candidate identity trio, `Language` master, certification fields, document description | `AddCandidateIdentityLanguagesAndCertification` | `hr-recruitment/run-c1.mjs` | schema before screens |
| 7 | **C2** — careers + HR candidate screens, photo, currency picker, languages tab, document restructure | none | `hr-recruitment/run-c2.mjs` + screen walk | |
| 8 | **K** — catalogue-driven criteria values + the nine scoring fixes + D-7 | `AddCriteriaCatalogueValues` | `hr-recruitment/run-k.mjs` (extends `run-lane5b.mjs`) | |
| 9 | **A** — application source and posting | none | `hr-recruitment/run-a.mjs` | |
| 10 | **G** — stage flags + pre-employment providers | `AddPreEmploymentCheckProviders` | `hr-recruitment/run-g.mjs` | |
| 11 | **U** — union contacts, documents, logo | `AddUnionContactsDocumentsLogo` | new `hr-unions/run-u.mjs` | |
| 12 | **T1** — profile grouped navigation | none | screen walk + static assertions | |
| 13 | **T2** — record tabs: Movements, Probation, Separation, Leave, Attendance, Benefits, Salary changes | none | screen walk + a static harness asserting each tab's service call | |
| 14 | **T3** — record tabs: Training, Appraisals & goals, Discipline, Awards, Assets, Medical, Travel, Orientation, Succession | none | as T2 | |
| 15 | **J2** — derived intrinsic value, proposed grade, the matcher | `AddJobDescriptionProposedGrade` | `hr-jobarch/run-j2.mjs` | |
| 16 | **P2** — the disability catalogue | `AddDisabilityTypes` | `hr-employee-docs/run-p2.mjs` | |
| 17 | **P3** — drop the contract leave columns | `DropContractLeaveColumns` | `hr-probation/run-p3.mjs` | |
| 18 | **X** — allowances and deductions (probe → editor or read-only) | none | `hr-payroll-membership/run-x.mjs` | the probe decides; can move earlier |
| 19 | **V** — talent segment ownership | `AddTalentSegmentOwnership` | `hr-recruitment/run-v.mjs` | |

**Lane Q log (2026-09-11).** `WithSummaryNavigations()` now includes all ten navigations the DTO
projects (the list is in the DTO's order with a comment saying to keep them together).
`StaffRequisitionService.RequireJobDescriptionOfPositionAsync` runs on create and update: a named
description must exist, be the tenant's, and describe the requisition's position (422, the
controller's convention; `IJobDescriptionRepository` injected). The form fills the position's
current APPROVED description on position select (one-way; on edit the record's own is kept with
"use the current version" / "remove it" when it drifts); create and edit send it; the detail links
it, prints both levels beside unit and location, and now renders `description`, `expectedOfferDate`
and `targetStartDateReason`. The screening page's approval and notification cards sit below the
tabs. Type-check: baseline 33 only.

⚠ **Found beyond the plan, fixed here: a Replacement requisition could not be raised from the
form.** `StaffRequisition.ReplacementReason` is the enum `StaffReplacementReason` (eight members)
and the form offered a free-text "Why they left" box, so anything typed other than an exact member
name was refused with the middleware's canned 400. Now a dropdown over `STAFF_REPLACEMENT_REASONS`
(`types/hr/recruitment.ts`), humanised on the detail. The harness found it by sending `'Resigned.'`.

Deviations: the queue is a `StaffRequisitionSummaryDto` and does not carry the description — not
added (the list screen has no such column); the location keeps no default (D-20's "from the
organisation unit" is moot: `OrganizationUnit` carries no location). Harness lesson: requisition
DELETE is `HR.Recruitment.Admin`; an HR actor's cleanup 403s — delete as admin.

**Lane P1 log (2026-09-11).** *Announcements:* `HrAnnouncementService.PublishAsync` now raises
`HrAnnouncement.Published.Internal` through `IAppEventBus` after the commit, for the users linked to
the employees the audience rules resolve (chunked lookup through `UserManager`, the same reach the
publish already counted). The topic is seeded on first publish — in-app on, email and SMS off, one
`UsersFromData` recipient rule allowing every channel — so an administrator's switch on the topic is
all that is needed to reach staff who never open the portal. Best-effort: a dispatch failure is
logged, never a failed publish. *Modal headers:* `EmployeeProfileContext` provided by the profile
page; `ResourceCollectionTab` gained optional `subjectLabel` + `itemLabel(item)` (title "Edit
dependent — Kofi Mensah", description "On Ama Mensah's profile", remove confirmation names both);
`EmployeeSubResourceTab` reads the context; twelve tabs supply row labels. The ~30 non-employee
callers are untouched. Type-check: baseline 33 only.

Harness lessons: the admin login is not employee-linked, so archive/publish (attributed actions)
must run as the HR actor; a harness announcement targets a freshly minted POSITION, never
`AllEmployees` (that would notify the whole demo tenant). The modal half is a screen walk.

**Lane S log (2026-09-11).** Migration `20260911091344_AddEmployeeSalaryChangeRequest` — one table,
one policy column (`DEFAULT (1)`; the scaffold said false, fifth time this shape), the seeded row's
`UpdateData` replaced with plain SQL because the fast build strips migration models and the apply
failed with "no entity type mapped to the table". Built as § 5.1 describes, with these deviations:

- **Approve/reject take the caller's employee id and refuse the requester and the subject BEFORE
  the engine is asked.** The harness found the reason: every seeded HR definition names approver
  ROLES (HR among them) with no initiator prevention, so the HR officer who raised the request
  approved it — and, the definition being one step, applied it. Recorded for the workflow owner in
  § 6; the other eight surfaces on the recipe have the same exposure.
- **TRAP 5 is guarded but not asserted.** The seeder publishes a default definition for the type at
  start-up, so "no published definition" cannot be exercised without retiring a seeded one (the F3
  trap). The harness asserts instead that exactly one live definition exists, asked the engine's way.
- **A first salary figure on the header is not a change.** A record with no figure (someone just put
  back on payroll) may receive one directly; a request cannot be raised for someone off payroll, so
  refusing it would have left no door. Only a CHANGED figure is refused under the policy.
- **The policy defaults ON, and three suites that place pay directly now toggle it off for their
  run** (`run-e1`, `run-payroll-membership`, `run-h`) and restore it. ⚠ A crash mid-run leaves it
  OFF — the API was stopped by SQL Server saturation once during this lane's regression (execution
  timeout, pool exhaustion, a Procurement background service throwing, host configured to stop) and
  the restore never ran; the next runs then "restored" the polluted value. Check the setting after
  any red run. This tenant is two-tier: `ResolvePlacementLevelAsync` fills the implicit level.
- Q-10 taken as `HR.Compensation.Write` only (no line-manager raise yet); Q-11 as written.
- Frontend: the "Salary changes" card first on the tab (raise dialog with the shared scale picker,
  the engine's actions on the one live request, retry, history); placement list and pay-basis card
  lock under the policy with a sentence; the payroll editor's basic-salary input locks with a
  caption; the policy screen's switch; the approved salary-review proposal links to the tab with
  `?tab=salary&fromProposal=` (T1 will honour `?tab`). Screen walk owed.

Each slice gets a log block under its row when built: assertion count, harness, migration name,
deviations from this document, and what it found beyond it — the round-2 convention.

---

## 5. Design notes

### 5.1 Lane S — the salary change request

**Entity** `EmployeeSalaryChangeRequest`: `EmployeeId`; `Kind { Placement, NegotiatedAmount,
PayBasisSwitch }`; a current-terms snapshot taken at raise (pay basis, grade/level/notch, amount,
currency — so the approver reads what was true when it was asked); `ProposedGradeId /
ProposedLevelId / ProposedNotchId`, `ProposedAmount`, `ProposedCurrencyCode`, `ProposedPayBasis`;
`EffectiveDate`; `Reason`; `Status { Draft, PendingApproval, Approved, Rejected, Recalled, Applied,
AwaitingPayrollEntry }`; `SourceProposalId?` (a `SalaryReviewProposal` that led here);
`AppliedOn`, `AppliedById`, `PayrollWriteFailure`, `RejectionReason`.

**Workflow** — the four-step recipe (`HR-WORKFLOW-ENGINE-INTEGRATION.md`): entity type
`HrEmployeeSalaryChangeRequest` (the flat namespace collides — grep the catalogue first),
`HrSalaryChangeWorkflowStatusAdapter`, routing context with the amount delta and the kind, a display
resolver deep-linking to the Salary tab, a seeded default definition of at least two steps, and the
`RequireApprovalWasActuallySought` guard (TRAP 5 — without it the requester approves their own
raise the day no definition is published). `preventInitiatorApproval` stays true here: the
beneficiary is the subject, but the requester is usually the line manager or HR, and a manager
approving their own proposal is the conflict the flag exists for.

**Apply** — one method, `ApplySalaryChangeAsync`, run by the service when the adapter lands on
`Approved` (not by a button): Placement → `AssignSalaryAsync` (inherits lane G's level resolver,
E1b's withdrawal rule, the membership gate); PayBasisSwitch → the existing pay-basis path
(`EmployeeService.cs:3572-3599`); NegotiatedAmount → `Employee.Salary` + payroll. Payroll is written
through a new `IPayrollMembershipService.UpdateMonthlyBasicAsync`: read the full profile through
`GetEmployeeProfilesAsync`, change only `SalaryBasis.MonthlyBasicSalary` and `EffectiveFrom`,
re-send through `UpsertEmployeeProfileAsync`, then re-read and compare the payment-method count and
ids — any difference is logged as a defect and the request lands on `AwaitingPayrollEntry` with the
failure text; a retry endpoint re-runs only the payroll half. Nothing in payroll's files is edited.

**Gates** — `RequireSalaryChangeApprovalAsync(employeeId)` when
`SalaryChangeRequiresApproval` is on: `POST/PUT salary-assignments` and `PUT pay-basis` refuse with
"raise a salary change request" (400, the employee doors' convention); `UpdateEmployeeDto.Salary`
is refused server-side (today only the mapper omits it); the embedded editor's basic-salary input
is read-only with the same sentence. The apply path calls the same writers with an internal bypass
token, so the rule has one implementation and two callers. `StaffMovementService` (lane H) and
`JobOfferHireService` are the other two already-approved writers and are left alone.

**Screens** — Salary tab gains a "Salary changes" card first in the tab (open request with the
workflow actions, history); a raise dialog per kind; `SalaryReviewProposal` detail gains "Raise a
salary change request" pre-filled from the proposal. The policy setting joins the HR policy screen
(both DTOs, both mapping halves, the TS type, the zod schema, the payload, the field — the F2 rule).

**Harness must assert**: no published definition → submit refused; each direct door refused under
the setting and admitted with it off; approval writes the placement AND payroll's basis; the
payroll failure path (stub the upsert to throw) lands on `AwaitingPayrollEntry` and the retry
applies; a requester cannot approve their own request; recall returns to Draft.

### 5.2 Lane X — the probe decides the shape

`dev-harness/hr-payroll-membership/probe-component-exceptions.mjs`: create exceptions for two
employees on one component, save ONE employee's row through `component-exceptions/bulk`, assert
the other employee's row survives, then the reverse. If it survives, HR builds an employee-first
editor (every active `PayrollComponent` with the employee's exception, edit in place, save through
payroll's endpoint). If the bulk save is a replace-set per component, HR ships the read-only
per-employee view and a deep link into payroll's page, and the ask in § 6 gains a per-employee
upsert. Either way `GET api/hr/Employees/{id}/payroll-component-exceptions` (HR's door, joins
`PayrollComponent`, `HR.Compensation.Read`) replaces the tab's client-side filter of the tenant's
whole list.

### 5.3 Lane T — the profile

`EmployeeProfileContext` (id, full name, number, status, `isOnPayroll`) is provided by the page and
read by every tab; `profileTabGroups.ts` is the single data table the left rail (lg), the mobile
group pills and the `?tab=` deep link all read; `TabsContent` mounts lazily. Groups: Personal
(Overview, Addresses, Emergency, Dependents, Identification, Expatriate) · Employment (Contracts,
Position history, Movements, Probation, Teams, Relievers, Separation) · Pay & benefits (Salary,
Salary changes, Bank, Benefits) · Capability (Qualifications, Skills, Certifications, Work history,
Training) · Performance & conduct (Appraisals & goals, Discipline, Awards) · Time & leave (Leave,
Attendance) · Welfare & travel (Medical, Travel, Assets) · Records (Documents, Referees, Guarantors,
Orientation, Succession). Every record tab is one `RecordSummaryTab` (query fn, columns, empty text,
"Open in <module>" href) over an existing service. The by-employee routes each reads: assets
`api/Assets/assignments/employee/{id}`; leave `api/Leaves/employee/{id}/history` + `/balances`;
discipline `api/discipline/cases/employee/{id}`; training `api/training-completions/employee/{id}`
+ nominations; appraisals `api/PerformanceAppraisals/employee/{id}`; goals
`EmployeeGoals/by-employee/{id}`; movements `api/staff-movements/employee/{id}`; medical
`api/employee-health/employees/{id}/profile` + claims; travel `api/staff-travel/requests/employee/{id}`;
awards `api/Awards/employee/{id}`; probation `api/probations/employee/{id}`; separation
`api/hr/separations/employee/{id}`; benefits `api/hr/employee-benefit-enrollments/by-employee/{id}`;
attendance `StaffMonthlyAttendanceSummaries` by employee; orientation
`EmployeeOrientations/employee/{id}`; succession `SuccessionCandidates/employee/{id}`. Counts on
group headers only where a cheap count exists (discipline `open-count`).

### 5.4 Lane K — criteria values and scoring

`RequiredValue` stays as the mirrored label list (old rows keep working); scoring reads `Values`
(ids) first and labels second. The per-type value source lives in ONE server-side table
(`ShortlistingCriteriaShapes`: value kind, catalogue, numeric, mandatory-allowed) exposed as
`GET criteria/shapes`, so the panel's hand-copied `SHAPES` map stops drifting from the service.
`Other` → text, never mandatory, rawScore 0. Gender/Age → D-7. No criteria → `AutoScore = null`
and `AutoShortlistByScoreAsync` skips null. `AutoShortlistMinScore` becomes the default of the
auto-shortlist dialog. The client default strategy becomes `Exact`, matching the server.

### 5.5 Lane A — source and posting

A static `JobPostingChannel → ApplicationSource` map. `JobApplication.Source` is derived when
`JobPostingId` is present and typed otherwise. The public vacancy list gains `postings[]` (id,
channel) and each posting's own link (`PostingUrl` on `VacancyPostingsPanel`) is minted as
`/careers/{vacancyId}?posting={postingId}`; the apply and draft payloads carry it. HR's "Record an
application" form (walk-ins, agency submissions) sets source and posting by hand;
`UpdateJobApplicationSourceDto` lets HR correct either.

### 5.6 Lanes C1/C2 — the candidate

`IdentificationType` is reused as-is (Ghana Card is a seeded type). `Language`: entity,
`LanguagesController` (`api/hr/languages`), a `ResourceListPanel` screen under People Reference
Data, `LanguageSeeder` under `seed-hr-all`, and `GET api/public/catalogue/languages` for the
anonymous careers form — careers pickers use the PUBLIC catalogue routes, never the HR-gated ones
(`api/hr/currencies` 403s for an anonymous caller exactly as `api/finance/currencies` does for HR).
The hire path (`JobOfferHireService`) copies the national-ID trio onto the employee record. Photo:
`hasPhoto` is derived from `ProfilePhotoFileUploadRecordId != null` on the detail and list DTOs;
the HR list shows a `GatedPhoto` avatar, the detail a `PhotoDialog`, the careers profile a
`PhotoPanel` over the existing `careers.service.ts` upload/download methods.

### 5.7 Lane J2 — the grade matcher

Step 0 is a database read on the dev tenant — `SELECT Code, MinSalary, MaxSalary FROM
SalaryGrades WHERE IsDeleted = 0` — to confirm that projected grades carry 0..0 bands. The matcher
then reads the band from min/max when set, else from the grade's notch amounts (`SalaryNotch` min
and max under the grade), and returns null with a sentence when nothing fits. `ProposedSalaryGradeId`
is the user's; `SuggestedSalaryGradeId` becomes read-only on the DTOs (accepted today on create and
update, silently).

### 5.8 Lane U — union contacts and documents

`UnionContact` rows (D-8); the union form's three legacy contact fields become a read-only mirror
of the primary contact. Documents follow the requisition template exactly: a `UnionDocument` row
holds the file triple, `HrAttachmentUpload.ExecuteAsync` uploads under a new
`ControlledFileUploadCategories.HrUnionDocuments`, `HrDocumentDownload.ServeAsync` serves, the
frontend uses `AttachmentsPanel` and `hrDocumentService.download` — never an anchor to a gated
route. The CBA file is a `UnionDocument` of kind `CollectiveAgreement` with `AgreementId` set, so
one table serves both. The logo is a gated image on `Union` (`GatedPhoto`), not a public URL.

---

## 6. Asks to other owners

Shape: `docs/HANDOFF-*.md` (what is broken · what was proven · what it blocks · what a fix needs).

1. **Finance — a bank master** (`docs/HANDOFF-FINANCE-BANK-MASTER.md`, D-2): a `Bank` /
   `BankBranch` catalogue (or adopt HR's `EmployeeBank`/`EmployeeBankBranch`, which has branches, an
   API and a screen already); `BankAccount.BankId`; and confirmation that Finance's `BankAccount` is
   the source from which payroll picks the paying account. New FIN-INT row. HR builds the employee
   bank ↔ payroll payment-method bridge only after this lands.
2. **Payroll — appended to round 2's § 7.1:** (a) a per-employee component-exception read and
   upsert (lane X's probe says which is missing); (b) `PayrollPaymentMethod` to reference the bank
   master once Finance answers, and the paying account from Finance's `BankAccount` rather than
   `PayrollCompanyBanker`; (c) the by-employee profile read (item 1 there) is still owed; (d) lane S
   will call `UpsertEmployeeProfileAsync` read-modify-write — please confirm the upsert is lossless
   on a round-trip of payment methods and components.
3. **Finance — R8** (`../HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md`) is unchanged and still waiting.
4. **Workflow owner — seeded definitions do not bar the initiator** (found by lane S, 2026-09-11).
   `DatabaseSeedingService.EnsureHrWorkflowsSeededAsync` publishes one-step, role-routed definitions
   for every HR type; the approval configuration it builds names roles (HR is in most lists) and
   sets no initiator prevention, so on any surface where the requester holds one of those roles the
   ENGINE lets them approve their own record. Lane S closes it in its service (requester and subject
   refused before `CanUserApproveAsync`); lane F3 did the same for teams. The other eight HR surfaces
   on the recipe rely on the definition alone. Ask: `PreventInitiatorApproval = true` in the seeded
   approval configuration, or a per-type flag.

---

## 7. Records owed to other documents

- `HR-DEMO-FEEDBACK-ROUND-2-PLAN.md` § 5: lane H is built as slice 3 of this round — log it there.
  § 7.1: the payroll asks above appended. Lane E2 ("benefits, allowances, deductions inside HR")
  is superseded by lane X here.
- `HR-PAYROLL-BOUNDARY.md` § 2.3: lane S's `UpdateMonthlyBasicAsync` is a fourth reach into payroll
  through payroll's service, and the only writer of a payroll figure from HR; add it to § 3's table.
- `HR-WORKFLOW-ENGINE-INTEGRATION.md`: tenth application (lane S).
- `HR-MODULE-INTEGRATION-MAP.md`: HR → Procurement `Supplier` gains the pre-employment provider use
  (lane G); HR → platform notifications gains the announcement topic (P1).
- `../HR-FINISH-PLAN.md` § 3a / `../HR-CLOSURE-LEDGER.md` § F: the contract leave columns close
  (P3); `AnnualLeaveEntitlementDays` was the open half of the 2026-09-01 reservation note.
- `README.md`: this document's row (done with this commit).
- `dev-harness/hr-demo-smoke/demo-coverage-manifest.csv`: every new table above, WITH its scenario
  (C3a's rule — never a `required` row without seeding).

---

## 8. Verification

Per slice: (1) type-check the touched frontend under `tsconfig.hr-slice.json` (baseline: 33
pre-existing errors in `medical/ClinicalRecordActions.tsx` and `types/finance.ts`); (2) stop the
running `ErpSystem.Api`, the user builds; migrations scaffolded by the user, edited and listed by the
agent, applied by the user; (3) API in Staging with the JWT key (`hr-jobarch/README.md`),
`node run-<lane>.mjs` twice, then the regression set for the touched area (jobarch R1–R7 and C1–C3;
recruitment lane 5b; payroll-membership E1 and membership; probation D1; movements) — with the clamd
stub up for any upload-gated suite (U, C2); (4) a screen walk by the user for T1–T3, C2 (careers),
P1's modals, J1's dialog and U's forms — the harness cannot drive React; (5) stage, hand over the
commit message.

**Demo checks this round must pass end to end:** raise → approve → applied salary change visible on
the Salary tab AND in payroll's profile; a requisition created from the form shows its job
description and location; a candidate's photo on the HR list; an application made from a posting
link shows that posting and its source; a vacancy criterion picked from the qualification master
matching a candidate whose qualification is the same master row; a stage marked "cannot be skipped"
refusing a skip.

---

## 9. Open questions, each with a default

| Q | Question | Default if unanswered | Lane |
|---|---|---|---|
| Q-1 | Should responsibilities carry a monetary value too, so "etc." in the PDF has a home? | No this round; qualifications + competencies only (D-9) | J2 |
| Q-2 | A job-description row with a catalogue id AND different text — which wins? | The catalogue name is mirrored only when the text is blank; typed text stands (D-10) | J1 |
| Q-3 | Copy to another position: copy the reporting relationships? | No — they are position-specific (D-12) | J1 |
| Q-4 | Stage flags: keep both columns or drop one? | Keep both, derive `IsRequired`, enforce both; no migration (D-13) | G |
| Q-5 | Pre-employment providers: a Procurement `Supplier` or a free-standing HR provider table? | Supplier through the HR read door + an HR-side services table (D-14) | G |
| Q-6 | Employee-side languages now? | No — master + candidate only; employee tab is a follow-on (D-16) | C1 |
| Q-7 | Announcement email/SMS on by default? | Off; in-app on; the administrator flips them (D-18) | P1 |
| Q-8 | Modal titles: both names in the title? | Sub-record label in the title, employee in the description line (D-19) | P1 |
| Q-9 | Requisition location default? | From the organisation unit's location when set, else blank (D-20) | Q |
| Q-10 | Salary change: who may raise one? | `HR.Compensation.Write`, or the employee's line manager for their own reports | S |
| Q-11 | Salary change on an off-payroll employee? | Placement/negotiated refused (membership gate stands); pay-basis switch allowed | S |
| Q-12 | Lane X: if the probe says replace-set, do we still let HR save through payroll by carrying every other employee's rows? | No — read-only + ask; the clobber risk is payroll's data, not HR's | X |
