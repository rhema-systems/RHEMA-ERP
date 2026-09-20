# SHE inspection checklist builder — design record

> **Status: BUILT 2026-09-07.** Slices 1–4 done: migration `20260907003611_AddSheChecklistBuilder`
> (guarded, listed), builder UI, run tab + print, harness `run-slice18.mjs` green twice (209
> assertions) and slice 4 updated (67). Three defects the harness found in the first cut, all fixed:
> re-attaching a tracked inspection graph after materialising rows issued UPDATEs for new rows
> (`DbUpdateConcurrencyException`; rows are now added through the repository); a soft-deleted
> version still holds its slot in the number+version unique index (next-version and duplicate
> checks now include deleted rows); an item update that omitted `SectionId` stripped the section
> (omitted now means unchanged). Demo templates CHK-002/003 seed only on a rebuilt database
> (`seed-hr-demo` skips SHE once any incident exists). The user chose the *structured builder* over a generic
> form designer, hybrid signatures (system users sign in-app, external parties are typed name +
> date), and **Organization Unit everywhere the paper says "Department"** — that is the HR
> convention for this whole solution, not just this feature.
> Requirement: FR-SHE010 (`frd-v03.txt:1904` — inspections "using configurable checklists"),
> FR-SHE227 (`:2653` — Yes/No, Pass/Fail, observation entry), FR-SHE228 (risk class per finding),
> SRS SHE §4.2 FR-INSP-002/003 and §4.3 "Department Compliance Score".
> Source forms: the two TDC SHE scans supplied 2026-09-06 — *Food Vendor Screening & Inspection
> Checklist* (Community 27 site; 55 items in 10 lettered sections, 8 critical non-conformities,
> compliance % with four approval bands, vendor status, three signatories) and *Cafeteria Inspection
> Checklist* (48 items in 9 sections, qualitative four-level rating, three signatories).

## 1. What exists today, and the gap

| Piece | State (verified in code 2026-09-06) |
|---|---|
| `SheInspectionChecklist` + `SheInspectionChecklistItem` (`StaffSafetyEntities.cs:911–952`) | Template header (number, name, type, version, active) and a flat item list with a **free-text `Category`** per item, mandatory flag, regulatory reference, risk level. Builder screen at `/administration/safety/checklists/[id]` is a one-table `ResourceCollectionTab`. |
| `SafetyInspection` + `SafetyInspectionItem` (`:954–1063`) | An inspection may point at a checklist, but **`CreateAsync` never copies the items in** (`SafetyHazardInspectionServices.cs:874`). Findings are typed by hand. `ComplianceScore` is an `int?` the inspector types on the edit form. The close-out gate (unresolved findings / open actions) exists and is good. |
| Statuses | `SheInspectionStatus {Scheduled, InProgress, PendingCorrectiveActions, Completed, Closed, Overdue}`; `SheComplianceStatus {Compliant, NonCompliant, PartiallyCompliant, NotApplicable, NotAssessed}`. Both reusable as-is. |
| Readers of `ComplianceScore` | `SheKpiComputationService.cs:224–236` (average score + housekeeping score). Keep the column; write the rounded computed percentage into it. |
| Seed | `SheDataSeeder.cs:546–612` — one checklist `CHK-001` with three section-less items, two inspections. |
| Harness | `dev-harness/hr-safety/run-slice4.mjs` covers the flat template and the inspection lifecycle. |
| Demo manifest | `dev-harness/hr-demo-smoke/demo-coverage-manifest.csv` lists the seven inspection tables as `required`. New tables must be added there or the UAT rebuild gate reports them missing. |

Missing against the two forms: **sections**, **header fields per template**, **critical
non-conformities**, **computed scoring with decision bands / qualitative rating**, **outcome with a
recommended value and an override**, **signatories**, **template versioning that leaves past
inspections on the form they were done on**, and **a printable form**.

## 2. Decisions

1. **Extend, don't replace.** The existing two template tables and the inspection item table stay
   and gain columns; everything new hangs off them. The free-form inspection (no checklist) keeps
   working unchanged.
2. **Structured builder.** A template is: header fields · lettered sections of numbered items ·
   optional *critical* sections answered Yes/No · a scoring mode · outcomes (with optional % bands)
   · signatories · print title/subtitle/instructions. No arbitrary widget designer.
3. **Published templates are structurally immutable.** Draft → Published → Retired. Editing a
   published template's structure is refused; "New version" clones it into Draft v+1 with the same
   checklist number. Publishing v+1 retires v. Inspections pin the exact version row they were
   created from, so no snapshot copy is needed. The unique index moves from
   `(TenantId, ChecklistNumber)` to `(TenantId, ChecklistNumber, Version)`.
4. **Choosing a template materialises its items** on the inspection at creation (all
   `NotAssessed`), in display order. Existing scheduled inspections can apply a checklist once
   (`apply-checklist`, refused when items already exist).
5. **Scoring is computed, never typed.** Applicable = standard-section items not NA and not
   unassessed. Compliant / applicable → percentage (2 dp), rounded into the existing
   `ComplianceScore` int for the KPI layer. *Partially compliant* counts as **not** compliant for
   the percentage and is reported separately. Critical items: Yes = `NonCompliant`.
6. **Outcome = recommended + confirmed.** In `CompliancePercentage` mode the band containing the
   percentage is the recommendation; any critical non-conformity forces the outcome flagged
   `IsDisqualifying`. The inspector confirms or overrides with a reason. In `QualitativeRating`
   mode the outcomes are the rating scale and the inspector picks one (no recommendation). In
   `None` mode there is no outcome block.
7. **Completing an inspection** (new action) requires every item assessed, every required header
   field filled, and — for percentage/qualitative modes — an outcome. It persists the score, moves
   the status to `PendingCorrectiveActions` when any NC exists, else `Completed`. The existing
   close-out gate is untouched and still runs after corrective actions.
8. **Signatures are hybrid.** Signatory rows of kind `SystemUser` are signed by the logged-in
   employee (`UserId` from the token, never from the body — the HR actor rule); `External` rows
   take a typed name and date. Signing is allowed from `InProgress` onward and is not a gate on
   completion (the paper is signed after the walk, and the vendor may refuse).
9. **Header field types** are the closed set `Text, LongText, Number, Date, Time, YesNo, Choice,
   Employee, Location, OrganizationUnit`. Reference kinds store the id and the read resolves the
   name. "Department" on the paper becomes an `OrganizationUnit` field.
10. **Print** is a client-side A4 view over the full read (the `printing-*` body-class pattern from
    `hr/assets/report`), reproducing the paper: document information, scoring guide, sections,
    critical block, summary, outcome, corrective actions, comments, signatures, instructions.
11. **Legacy rows.** The migration marks existing checklists `Published` (so `CHK-001` and the
    seeded inspections keep working). Section-less items are grouped on read under an implicit
    "General" section by their `Category`; publishing a *new* template requires every item to sit
    in a section.

## 3. Data model

New enums (`Enums/Safety/SafetyEnums.cs`):
`SheChecklistStatus {Draft=1, Published=2, Retired=3}` ·
`SheChecklistScoringMode {None=1, CompliancePercentage=2, QualitativeRating=3}` ·
`SheChecklistSectionKind {Standard=1, Critical=2}` ·
`SheChecklistFieldType {Text=1, LongText=2, Number=3, Date=4, Time=5, YesNo=6, Choice=7, Employee=8, Location=9, OrganizationUnit=10}` ·
`SheChecklistSignatoryKind {SystemUser=1, External=2}`.

**`SheInspectionChecklist`** gains: `Status`, `ScoringMode`, `AllowPartialCompliance` (bool),
`PrintTitle` (200), `PrintSubtitle` (200), `Instructions` (4000), `CriticalSectionNote` (500),
`PublishedAt`, `PublishedById`, `RetiredAt`, `PreviousVersionId` (self FK, nullable); collections
`Fields`, `Sections`, `Outcomes`, `Signatories`.

**`SheInspectionChecklistItem`** gains: `SectionId` (nullable FK). `Category` becomes optional
(defaults to the section title when blank).

New tables, all `TenantEntity`, all cascade-deleted with the template:

| Table | Columns |
|---|---|
| `SheInspectionChecklistFields` | ChecklistId, DisplayOrder, Label (150), FieldType, IsRequired, ChoiceOptions (1000, `|`-separated), HelpText (300) |
| `SheInspectionChecklistSections` | ChecklistId, DisplayOrder, Code (10, e.g. "A"), Title (200), Description (500), Kind |
| `SheInspectionChecklistOutcomes` | ChecklistId, DisplayOrder, Label (150), Description (500), MinPercent / MaxPercent (decimal(5,2), nullable), ReinspectionWithinDays (int?), IsDisqualifying (bool) |
| `SheInspectionChecklistSignatories` | ChecklistId, DisplayOrder, RoleLabel (150), Kind, IsRequired |

**`SafetyInspection`** gains: `TotalApplicableItems`, `TotalCompliantItems`,
`TotalNonCompliantItems`, `TotalPartiallyCompliantItems`, `CriticalNonConformityCount` (all
`int?`), `CompliancePercentage` (decimal(5,2)?), `RecommendedOutcomeId`, `OutcomeId` (FKs to
outcomes, `Restrict`), `OutcomeOverrideReason` (500), `SubjectComments` (2000 — the vendor's /
operator's comments; `FindingsAndObservations` is the inspector's), `CompletedAt`,
`CompletedById`; collections `FieldValues`, `Signatures`.

**`SafetyInspectionItem`** gains: `DisplayOrder` (int).

| Table | Columns |
|---|---|
| `SafetyInspectionFieldValues` | InspectionId, ChecklistFieldId (Restrict), ValueText (2000, canonical: ISO date/time, number, "true"/"false", choice label), ValueReferenceId (Guid?, the employee/location/org-unit id) |
| `SafetyInspectionSignatures` | InspectionId, ChecklistSignatoryId (Restrict), RoleLabel (150, snapshot), SignedByEmployeeId (nullable FK), SignedName (200), SignedAt, Notes (500) |

Indexes: `(TenantId, ChecklistNumber, Version)` unique on checklists (replacing the two-column
one); `(InspectionId, ChecklistFieldId)` unique on field values; `(InspectionId,
ChecklistSignatoryId)` unique on signatures; `(ChecklistId, DisplayOrder)` on the four child tables.

## 4. Behaviour (service rules → HTTP through `SafetyBusinessRulesAttribute`: 404 / 422 / 403)

Builder (`api/safety/inspection-checklists`, `SheAdminPolicy` for writes):

- Structure writes (fields, sections, items, outcomes, signatories, and the header's
  scoring/print columns) are refused with 422 on a `Published` or `Retired` template. Name,
  description and `IsActive` stay editable.
- `POST {id}/publish` validates: at least one `Standard` section with at least one item; every
  item in a section; `CompliancePercentage` mode → outcomes with bands must cover 0–100
  contiguously without overlap and every band-less outcome must be `IsDisqualifying`; a `Critical`
  section requires exactly one `IsDisqualifying` outcome; `QualitativeRating` mode → at least two
  outcomes, no bands; `None` mode → no outcomes. Sets `PublishedAt/ById`, retires the
  `PreviousVersionId` row if it is still published.
- `POST {id}/new-version` (Published or Retired source) deep-clones into Draft v+1 with
  `PreviousVersionId`; refused if a draft of that number already exists.
- `POST {id}/retire`; retired templates are not offered for new inspections.
- `DELETE {id}` refused (422) when any inspection references the template.
- Reorder endpoints: `PUT {id}/sections/order`, `PUT sections/{id}/items/order`, and the same for
  fields / outcomes / signatories — body is the ordered id list (a replace-set; a missing id is a
  422, not a delete).

Run (`api/safety/inspections`, existing `SheWritePolicy` on writes):

- `POST` with `ChecklistId` → template must be `Published` and `IsActive` (422 otherwise);
  materialises one `SafetyInspectionItem` per template item, status `NotAssessed`, `DisplayOrder`
  by section order then item order; status moves to `InProgress`.
- `POST {id}/apply-checklist {checklistId}` — same, for an existing inspection; refused when items
  exist or status is beyond `InProgress`.
- `PUT {id}/responses [{itemId, status, deficiencyNoted, riskLevel}]` — bulk answer; unknown item
  → 422; refused after completion.
- `PUT {id}/field-values [{checklistFieldId, valueText, valueReferenceId}]` — **replace-set**;
  type-checked per field (date parses, number parses, choice ∈ options, reference resolves within
  the tenant); refused after completion.
- `GET {id}/score` — the live computation (§2.5–2.6) without persisting.
- `POST {id}/complete {outcomeId?, outcomeOverrideReason?, subjectComments?}` — the gate in §2.7;
  persists totals, `ComplianceScore`, recommended + chosen outcome; override without a reason →
  422; a disqualifying recommendation cannot be overridden to a non-disqualifying outcome.
- `POST {id}/signatures {checklistSignatoryId, signedName?, signedAt?, notes?}` — `SystemUser`
  rows take the actor from the token and ignore `signedName`; `External` rows require it; one
  signature per signatory (422 on a second).
- The detail read (`GET {id}`) now carries `checklist` (the template's structure), `fieldValues`
  (with resolved display names), `signatures`, the score block and both outcomes. List reads are
  unchanged.
- Materialised items still flow through the existing findings/close-out path, so an NC item's
  responsible person, target date and resolution are the corrective-actions table of the paper.

## 5. Frontend

- **Builder** `/administration/safety/checklists/[id]` becomes a tabbed workspace: *Form* (print
  title/subtitle, scoring mode, partial-compliance switch, instructions, critical note; status
  badge; Publish / New version / Retire with confirmation) · *Header fields* · *Sections & items*
  (section cards, inline item table, up/down reordering — no drag library is installed) ·
  *Outcomes* · *Signatories* · *Preview* (blank form as it will print). Published templates render
  read-only with the version actions.
- **Run** `/hr/safety/inspections/[id]` gains a first tab *Checklist* when the inspection has one:
  header-field form; each section as a card with a C / NC / NA segmented control per item (Yes /
  No in critical sections) and a remarks field; live score panel; outcome picker showing the
  recommendation; subject comments; signature block; *Complete inspection*; *Print*.
- **Print view**: A4 component reproducing the paper form, driven by the detail read.
- `new` page: no change beyond wording ("N items will be loaded").
- Nav: no new leaves. The admin landing already links "Inspection Checklists".

## 6. Seeding, harness, manifest

- `SheDataSeeder.SeedInspectionsAsync`: `CHK-001` gains sections and is published; add
  `CHK-002` *Food Vendor Screening & Inspection* and `CHK-003` *Cafeteria Inspection* transcribed
  from the scans (they are TDC's own content — the best demo data available); one completed
  inspection against CHK-002 with answers, score, outcome and signatures.
- Harness: `dev-harness/hr-safety/run-slice18.mjs` (builder rules, publish validation, version
  immutability, materialisation, bulk responses, scoring maths including NA exclusion, partial and
  critical override, completion gates, hybrid signatures, delete guard, non-HR 403s).
- Manifest: add the six new tables as `required` (the harness folder is outside the repo — owed at
  the end of the slice, not staged).

## 7. Slices

| # | Content | Hand-over point |
|---|---|---|
| 1 | Entities, EF config, DTOs, mappers, services, controller endpoints, seeder | user scaffolds `AddSheChecklistBuilder` migration; I guard it and list it; user builds + updates |
| 2 | Builder UI | scoped type-check |
| 3 | Runner tab + print view | scoped type-check |
| 4 | Harness slice 18, manifest, this doc's status line, README row | run in Staging |
