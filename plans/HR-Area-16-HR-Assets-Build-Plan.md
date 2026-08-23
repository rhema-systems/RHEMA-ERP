# HR Area 16 — Staff / Company Assets: Build Plan

**Opened 2026-08-23.** Area chosen by the user immediately after areas 19–23 closed. Like area 14,
this area's requirements come **almost entirely from a TDC change document** rather than from the
FRD — the FRD contributes one requirement, and it is a requirement *about* assets owned by another
area (FR-HR-183, exit clearance).

Unlike area 14, the ported code here is **not** obviously dead: every service is DI-registered, the
requisition surface already takes its actor from the token, and the assignment/return paths already
move asset status. But **every table holds 0 rows**, so nothing has been *executed*. Treat it as
harden-and-render, and prove it in slice 0 before believing any of it.

---

## 1. How to use this document

Read **section 3 (ground truth)** and **section 4 (the change document)** before writing any code.
Section 3 is measured, not assumed — every number came from the live DEFAULT tenant on `ErpSystemDB`
or from the source on 2026-08-23. Section 4 is the requirement source.

Section 5 holds the decisions. **All eight were settled with the user on 2026-08-23**, before any
code was written.

Section 8 is the running log. Each slice appends its own entry with its assertion count, what it
found and what it changed. Do not edit an earlier slice's entry to make it agree with a later
decision — record the change where it happened.

---

## 2. Status at a glance

| | |
|---|---|
| **Area** | 16 — Staff / Company Assets |
| **Branch** | `hrdev` |
| **FRD requirements** | **FR-HR-183 (M)** — exit clearance across "outstanding loans, salary advances, **company property, office equipment**, duty-post keys, documents and payroll recoveries". That is the only FRD line that touches this area. |
| **Primary requirement source** | `Staff Assets Changes.pdf` (supplied by the user 2026-08-23) — extracted in section 4 |
| **Backend today** | 10 entities, **82 endpoints** on one controller, 1,634 lines of service, 1,193 lines of DTOs, 939 lines of mapping, 584 lines of repositories |
| **Backend proven** | *(at survey)* **Nothing** — all ten stores held 0 rows. Slice 0 executed all 82 routes and found **two sub-surfaces that can never have worked** (D-k, D-l) |
| **Frontend today** | **none** — no `asset*.service.ts`, no screen under `/hr` or `/administration/hr` |
| **Authorization today** | *(at survey)* one bare `[Authorize]`, **no gate on any of the 82 routes**. **Closed in slice 1**: 71 routes `[Authorize(Roles = HrRoles)]`, 11 self-service routes gated by `AssetActor` on the service side |
| **Status** | 🔨 In progress. Decisions D1–D8 settled. **Slices 0–1 green twice — 86 assertions.** All 82 routes gated |
| **Harness** | `D:\Rhema\TDC ERPS\dev-harness\hr-assets\` — `api.mjs`, `setup.mjs`, `run-slice0.mjs` |
| **Blocks / unblocks** | Unblocks area 9b **D4** — asset return becomes an enforced clearance gate |

---

## 3. Ground truth — measured 2026-08-23

### 3.1 What exists

**Entities** — `src/ErpSystem.Core/Entities/HR/AssetsEntities.cs`, 511 lines, namespace
`ErpSystem.Core.Entities.HR.Assets`:

| Entity | Table | Rows |
|---|---|---|
| `AssetType` | `HrAssetTypes` | 0 |
| `AssetTypeAttribute` | `AssetTypeAttributes` | 0 |
| `CompanyAsset` | `CompanyAssets` | 0 |
| `AssetAttributeValue` | `AssetAttributeValues` | 0 |
| `AssetImage` | `AssetImages` | 0 |
| `AssetAssignment` | `AssetAssignments` | 0 |
| `AssetMaintenance` | `AssetMaintenances` | 0 |
| `AssetAttachment` | **`AssetAttachment`** (singular — no DbSet) | 0 |
| `AssetRequisition` | `AssetRequisitions` | 0 |
| `AssetTransfer` | **`AssetTransfer`** (singular — no DbSet) | 0 |

**Wiring is complete.** All ten services are DI-registered
(`src/ErpSystem.Api/Extensions/HrModuleServiceRegistration.cs:492-501`) and the controller's
ten-argument constructor therefore resolves. EF configuration runs from
`src/ErpSystem.Data/ApplicationDbContext.HR.cs:4104-4362`. Repositories exist. **This is not a
resurrection job.** No shadow `*Id1` FK columns were found on any of the ten tables.

**Endpoints** — `src/ErpSystem.Api/Controllers/HR/AssetsController.cs`, 850 lines, **82 routes**
(43 GET, 19 POST, 8 PUT, 10 DELETE, plus 2 attribute-less `[HttpGet]`/`[HttpPost]`), ten
sub-surfaces: asset types · type attributes · assets · attribute values · assignments · maintenance
· images · attachments · requisitions · transfers.

**Already better than the ported average** — do not assume otherwise without measuring:

- Every service scopes reads and writes by `_currentUserService.TenantId` explicitly, with the
  tenancy-stamping-gap comment in place. The dead auto-stamp is correctly assumed dead.
- `AssetRequisitionService.CreateAsync` takes `RequestedById` from
  `_currentUserService.EmployeeId` — the token-actor work is already done on that surface.
- `AssetAssignmentService.CreateAsync` sets `IsCurrentlyAssigned`, `CurrentAssignedToId` and
  `Status = Assigned` on the asset; `ReturnAssetAsync` clears them.
- `FulfillAsync` on a requisition really does create assignments and move asset status.

**Fixture data available** — Employees 6,806 · Locations 11 · OrganizationUnits 285 ·
SeparationClearanceTemplates 41.

### 3.2 The neighbours — measured, because this was the open question

The area-16 starting-point note flagged three asset worlds and warned that the row counts had **not**
been taken. They have now been:

| World | Controllers | Rows |
|---|---|---|
| **HR** (this area) | `AssetsController` — 82 routes | **0 everywhere** |
| **Finance / Fixed Assets** | `FixedAssetsController`, `FixedAssetCategoriesController` | `FixedAssets` **4**, `FixedAssetCategories` **3**; disposals, valuations, depreciation schedules, verification sessions, book values, transactions all **0** |
| **Maintenance** | `MaintenanceAssetsController`, `AssetTypesController`, `AssetConditionController`, `AssetUsageTrackingController`, `AssetAdmissions`, `AssetDischarges`, + Fleet | `MaintenanceAssets` **1**, `MaintenanceAssetCategories` **1**, `AssetTypes` **0**, everything else 0 (`FleetTrips` 19) |
| **Estate** | `EstateManagedAssetsController` | `EstateManagedAssets` 0 |

**Conclusion: no neighbour holds a populated register.** The risk the note raised — that HR would
duplicate a live store — does not exist today. The boundary is therefore a *design* decision
(section 5, D1), not a data rescue.

**The integration pattern already exists in this codebase and should be copied, not invented.**
`Finance.FixedAssets.FixedAsset` carries a nullable `MaintenanceAssetId` + navigation
(`FixedAsset.cs:116-122`, commented "Link to the physical asset/equipment record in Operations
module") and a `CurrentCustodianId` → `Employee`. That is exactly the shape AST-11 asks HR for.

### 3.3 ⚠ Two name collisions that will mislead a careless query

1. **`AssetType` is declared twice** — `Entities/HR/AssetsEntities.cs:10` and
   `Entities/Maintenance/MaintenanceEntities.cs:266`, both `TenantEntity`, different namespaces.
   The `DbSet<AssetType> AssetTypes` in the **main** `ApplicationDbContext.cs` is **Maintenance's**.
   HR's is fully qualified in the HR partial and maps to **`HrAssetTypes`**.
   `SELECT COUNT(*) FROM AssetTypes` measures Maintenance, not HR.
2. **`AssetTransfer` is declared twice** — `Entities/HR/AssetsEntities.cs` (HR) and
   `Entities/Finance/FixedAssets/AssetTransfer.cs` (Finance). Finance has
   `DbSet<AssetTransfer> AssetTransfers` → table **`AssetTransfers`** (plural,
   `ApplicationDbContext.cs:130,3148`). HR's has **no DbSet**, so EF names its table by the entity
   type → **`AssetTransfer`** (singular, configured at `ApplicationDbContext.HR.cs:4318`).
   `SELECT COUNT(*) FROM AssetTransfers` measures **Finance**.

The same trap applies to `AssetAttachment` (singular table, no DbSet). Any slice that counts rows
must name the singular tables deliberately.

### 3.4 Defects — all confirmed by execution, slice 0, 2026-08-23

D-a … D-i were read off the source before slice 0 ran. **Slice 0 confirmed every one of them and
found three more** (D-j, D-k, D-l) that were invisible from the source, because two of them are a
`Guid` literal and an argument order that only a 500 reveals. D-e had to be reclassified: it cannot
be reached, for the reason D-k gives.

- **D-a — nothing stops a double assignment.** `AssetAssignmentService.CreateAsync`
  (`AssetsServices.cs:690`) loads the asset and immediately overwrites `CurrentAssignedToId`. It
  never checks `IsCurrentlyAssigned`, `IsAssignable`, or `Status` (Disposed / InMaintenance /
  LostStolen). Assigning an already-assigned asset silently orphans the previous assignment, which
  stays `Active`. **This is exactly what AST-2 asks for and it is absent.**
- **D-b — acknowledgement has no actor.** `AcknowledgeAssignmentAsync` (`:737`) sets
  `EmployeeAcknowledged = true` for whoever calls it. `AcknowledgeAssignmentDto` carries only
  `AssignmentId`. Nothing checks that the caller is the assignee, and nothing records who
  acknowledged. The area-9 "acknowledge-with-no-actor" shape, again.
- **D-c — return can contradict itself.** `ReturnAssetAsync` (`:752`) writes
  `ReturnedInGoodCondition` and `DamageReported` straight from the payload with no consistency
  check, then sets asset status from `DamageReported` alone. A return can be simultaneously "in
  good condition" and "damaged".
- **D-d — the damage fields are inert.** `EmployeeLiable`, `RepairCost` and `ReplacementCost` are
  stored and never read by anything. There is no surcharge, no recovery, no route to payroll or to
  the separation settlement. **AST-3 has a field but no feature.**
- **D-e — `FulfillAssetRequisitionDto.AssignedAssetIds` is a `List<Guid>`; the entity has a single
  `AssignedAssetId`.** Fulfilling with three assets creates three assignments but the requisition
  can only remember one of them. ⚠ **Reclassified by slice 0 to a SOURCE claim, not a measured
  one**: fulfilment answers 400 because it requires an approved requisition, and D-k means no
  requisition can ever be approved. The mismatch is real in the source but cannot be demonstrated
  by execution until slice 3 lands.
- **D-f — 82 routes, zero authorization.** One bare `[Authorize]` on the class. A plain employee can
  create asset types, dispose of assets, approve requisitions and delete assignments. This is the
  W3 sweep's shape, landing inside this area.
- **D-g — no insurance expiry.** `CompanyAsset` has `IsInsured`, `InsurancePolicyNumber`,
  `InsuredValue` — and no expiry date. **AST-4.**
- **D-h — nothing acts on the maintenance schedule.** `NextMaintenanceDate` /
  `MaintenanceIntervalDays` / `RequiresRegularMaintenance` exist and
  `GetDueForMaintenanceAsync(daysAhead)` reads them, but nothing writes `NextMaintenanceDate` from
  the interval on completion, and there is no reminder sweep. **AST-1.**
- **D-i — no "Additional Remarks" field of any name.** `AdditionalDescription` does not exist
  anywhere in the asset model (the only hits in the repo are on a payroll component). **AST-7 needs
  a decision, see D6.** Slice 0 also confirmed **D-i(b)**: `CreateCompanyAssetDto` has no `UnitId`
  at all, though `CompanyAsset` does — an asset cannot be placed in an organisation unit on create.

**Found by execution, not by reading:**

- **D-j — a PUT erases every field it omits.** `UpdateCompanyAssetDto` is a full-replace payload
  and `UpdateEntity` assigns every property from it. Slice 0 created an asset with a 90-day
  maintenance interval and a GHS 5,000 insured value, then issued an unrelated edit that did not
  mention either — and both came back **null**. Any screen that PATCHes a subset through this PUT
  silently destroys the maintenance schedule and the insurance record. This is the
  [[replace-set-payload-convention]] shape applied to scalars rather than child collections, and it
  is the reason the harness now reads the register *before* editing it.
- **D-k — ⚠ requisition approval is wired to an employee who does not exist, and never has.**
  `AssetRequisitionService.ApproveAsync` (`AssetsServices.cs:1295-1296`) carries
  `// TODO: Replace with actual employee ID lookup - using temporary approver employee ID` and then
  `var approverId = Guid.Parse("D1D0261F-934D-4809-95EF-CD76156694A5");`. `FulfillAsync` (`:1331`)
  repeats the same literal for `FulfilledById`. `ApprovedById` is an **Employee** FK; that employee
  is not on this database (`SELECT COUNT(*) = 0`), so SQL rejects the UPDATE with error 547 and the
  endpoint 500s **for every actor, on every tenant, always**. Because approval is fulfilment's
  precondition, **the entire requisition → assignment pipeline has never once run end to end** —
  which is precisely the pipeline AST-6 asks to be put on the portal.
- **D-l — ⚠ the transfer sub-surface has never created a row.**
  `AssetTransferService.CreateAsync` (`:1536`) calls
  `dto.ToEntity(tenantId, userId, userId, transferNumber)`, passing the **ApplicationUser** id as
  `InitiatedById` — an **Employee** FK. FK error 547 again, every time. Nothing can be created, so
  the other ten transfer routes have no subject: slice 0 called all eleven and the ten reads and
  writes answer 400/404 on a well-formed id. This is the same token-actor confusion as D-k, in a
  second place, and it is the shape [[hr-attendance-actor-conventions]] already warned about —
  *`ApprovedById` is an Employee FK, unlike Leave's*.

- **D-m — every domain refusal in this area is mute.** Found while writing slice 1.
  `GlobalExceptionHandlingMiddleware` **discards** an `InvalidOperationException`'s message and
  substitutes the fixed string *"The operation is not valid for the current state of the object."*;
  `ArgumentException` becomes *"Invalid argument provided."* — which also means every "not found" in
  these services arrives as an opaque 400. So a rule that fires correctly still cannot tell the user
  what to do about it. `UnauthorizedAccessException` is the exception: it is passed through **with
  its own message**, which is why slice 1's 403s can be asserted on their wording.
  The fix is an `AssetsException` case in the middleware mirroring the `AwardsException` one area 14
  added (`response.Detail = ex.Message; // safe to display by design`), plus converting this file's
  domain throws onto it. **Scheduled into slice 2.**

**What slice 0 proved DOES work**, so later slices do not re-litigate it: asset types and their
attributes; the asset register including attribute values, images and attachments; assignment
create / read / edit / acknowledge / return; the maintenance log including complete; requisition
create (with the actor correctly taken from the token) and reject; and all ten DELETEs, which are
soft deletes.

---

## 4. The change document — requirement extraction

Source: `Staff Assets Changes.pdf`, supplied 2026-08-23. It is a page of terse notes; each is given
an ID below so slices can cite it. Nothing is inferred that the document does not say.

| ID | Requirement | Today |
|---|---|---|
| **AST-1** | If an asset requires maintenance, there must be a way to **monitor** it | Fields + one read exist; no scheduling, no reminder (D-h) |
| **AST-2** | Once assigned, an asset is **no longer available for further assignment** | ❌ absent (D-a) |
| **AST-3** | An employee may be **surcharged for damage** | Fields exist, inert (D-d) |
| **AST-4** | **Insurance expiry** field on the asset details | ❌ absent (D-g) |
| **AST-5** | **Print** the responsibility-and-terms document for physical signature, for employees who cannot use the portal | ❌ absent |
| **AST-5b** | …or **email** that document to the employee's email address | ❌ absent |
| **AST-6** | The employee can **request assets from the portal**; the existing asset requisition must be doable from the portal | Backend actor is already token-derived; **no portal surface, no screen** |
| **AST-6b** | A **manager or another person may request on behalf** of an employee | ❌ absent — `AssetRequisition` has only `RequestedById` |
| **AST-7** | Rename **"Additional Description" → "Additional Remarks"** | No such field exists (D-i, decision D6) |
| **AST-8** | On the portal, the employee **acknowledges receipt** of the asset | Endpoint exists but any caller can acknowledge for anyone (D-b); no screen |
| **AST-9** | **Rentable** company assets (e.g. staff housing): a flag distinguishing a rental property/asset, with financial implications; Finance **deducts at source** | ❌ absent entirely |
| **AST-10** | Assigning a rentable asset feeds **payroll** for benefit-in-kind, tax assessment or deduction, carrying the **deductible rental amount** | ❌ absent entirely |
| **AST-11** | **Integrate with and read from the Fixed Assets module** so effort is not duplicated; when HR picks an asset from that module, a **flag/detail marks it as sourced there**, distinct from assets HR creates itself | ❌ absent — no link of any kind between `CompanyAsset` and `FixedAsset` |

Plus, from the FRD:

| ID | Requirement | Today |
|---|---|---|
| **FR-HR-183** | Exit clearance runs across **company property and office equipment** | Area 9b built the clearance form with `ClearanceItemKind.CompanyProperty` / `.OfficeEquipment` and 41 templates, deliberately un-sourced pending this area (9b **D4**) |

---

## 5. Decisions

### D1 — the ownership boundary between HR, Fixed Assets and Maintenance. ✅ **DECIDED 2026-08-23.**

*Measured context: no neighbour holds a populated register (§3.2), so this is a design call.*

*Recommendation taken.* **HR owns CUSTODY, and references rather than copies.**

- **HR owns**: who holds what, and everything that follows from a person holding it — requisition,
  approval, assignment, the responsibility-and-terms document, acknowledgement, transfer between
  employees, return, damage and surcharge, and the exit-clearance hook.
- **Finance / Fixed Assets owns**: capitalisation, depreciation, valuation, disposal accounting.
  HR **reads** it. A `CompanyAsset` may carry a nullable `FixedAssetId` plus an
  `AssetSource` enum (`HrCreated` | `FixedAssetsModule`) — copying the pattern Finance itself uses
  for `MaintenanceAssetId` (§3.2). Financial fields on a linked asset are read-through and
  read-only in HR; HR-created assets (uniforms, phones, tools — things below the capitalisation
  threshold) keep their own. This is AST-11, literally.
- **Maintenance owns** workshop servicing of plant and fleet. **HR keeps its own light maintenance
  log** for staff-issued items, because it already exists, because Maintenance's register holds 1
  row, and because the change document asks HR to monitor maintenance without mentioning that
  module at all.

*Rejected alternative:* HR's `maintenance/*` routes are deleted and every service
event is raised in the Maintenance module. Cheaper to reason about, but it puts a staff laptop's
battery replacement into a plant-maintenance workflow, and it deletes working code to solve a
problem the row counts say we do not have.

### D2 — how far HR goes on the rental / benefit-in-kind seam (AST-9, AST-10). ✅ **DECIDED 2026-08-23.**

*Constraint: payroll is another developer's module — integrate read-only, never modify. And every
money event is registered in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` and posted in one sweep after
the whole HR module.*

*Recommendation taken.* **HR *declares and exposes*, and stops there.** HR gains: `IsRentable` on the
asset; on the assignment, the rental amount, currency, deduction frequency, effective from/to, and
a benefit-in-kind flag; and a **read-only projection endpoint**
(`GET /api/assets/payroll/rental-deductions?period=`) that payroll pulls. HR never writes a payroll
deduction row and never posts to the GL; both are registered in the Finance backlog for the sweep.

*Rejected alternative:* HR writing directly into a payroll deduction table — it would be the first
time HR writes into payroll, and would need that module's owner to agree.

### D3 — approval routing for requisitions and transfers. ✅ **DECIDED 2026-08-23.**

*Recommendation taken.* **Both go on the workflow engine**, per the four-step recipe used by areas 8, 9
and 9b, rather than keeping the ported bespoke `ApproveAsync`/`RejectAsync`. Assets are the fifth
area to need an approval chain; a fifth bespoke one is a fifth thing to fix later. ⚠ Note the known
traps: a single-step definition silently auto-approves, and the entity-type seed must exist.

*Rejected alternative:* keeping it bespoke, as the goal-approval area deliberately does. Cheaper this week,
and defensible if TDC says a line manager's nod is the whole chain.

### D4 — what "the portal" means for AST-6 and AST-8. ✅ **DECIDED 2026-08-23.**

There is no separate employee portal application. The established convention is `/mine`-style pages
inside the main app (e.g. `/hr/movements/mine`) backed by `api/employee-portal` — and area 25
(employee self-service consolidation) is still deferred.

*Recommendation taken.* **Built the established way**: `/hr/assets/me` (my assets · acknowledge receipt
· request an asset · request on behalf) plus asset routes added to the existing
`EmployeePortalController`. Area 25 consolidates later; nothing is thrown away.

*Rejected alternative:* the `external-portal` app — that is for candidates, suppliers and
consultants, i.e. external users, and would need staff auth added to it.

### D5 — request on behalf (AST-6b). *Recommendation, taken unless contradicted.*

`AssetRequisition` gains `BeneficiaryEmployeeId` (**who the asset is for**) alongside the existing
`RequestedById` (**who raised it**, from the token). Self-service leaves the beneficiary null,
meaning "me". Raising on behalf of someone requires being their manager or holding the HR role.
Both ids are shown everywhere the requisition is shown — an area-9 lesson: a record with one actor
column cannot answer "who did this to whom".

### D6 — "Additional Description" → "Additional Remarks" (AST-7). *Recommendation, taken unless contradicted.*

No field of that name exists on any asset entity. `CompanyAsset` has `Description` (1000),
`Specifications` (300) and `LocationDetails` (500) — renaming `Description` would be wrong, since
the change document treats the two as different things. **Add `AdditionalRemarks` to `CompanyAsset`
and label it "Additional Remarks" on the form.** If TDC meant a field on a *different* screen, say
which and it moves — the cost is one column.

### D7 — the responsibility-and-terms document (AST-5, AST-5b). *Recommendation, taken unless contradicted.*

Generated as a **PDF with QuestPDF**, the way the repo already generates documents
(`Services/Documents/**`, `AwardLetterService`), from the assignment's `TermsAndConditions`,
`ResponsibleForLoss` and `ResponsibleForDamage`, with the asset, the holder and a signature block.
Two routes: download (print and sign) and email to the employee's address via the existing
`ITransactionalEmailQueue`. The emailed copy is recorded on the assignment so "was it sent?" has an
answer.

### D8 — the exit-clearance hook (FR-HR-183, closing 9b D4). *Recommendation, taken unless contradicted.*

When a separation's clearance form is generated, every `ClearanceItemKind.CompanyProperty` and
`.OfficeEquipment` line is **sourced from HR Assets**: one line per unreturned active assignment,
carrying the asset and, where damage was recorded, the surcharge amount into
`SeparationClearanceItem.OutstandingAmount` — which the FR-HR-184 settlement already deducts. A
manual line remains possible for property HR never registered.

---

## 6. Scope

**In scope:** the ten existing sub-surfaces hardened and proven; the thirteen change-document
requirements (AST-1 … AST-11); FR-HR-183's clearance hook; authorization over all 82 routes; the
Fixed Assets link; the rental declaration and its payroll projection; the responsibility-and-terms
document; the employee's own surface; a reminder sweep for maintenance, insurance expiry and
overdue returns; and the full screen set.

**Out of scope, deliberately:**

- **GL posting** of surcharges, rental deductions or disposal proceeds. Registered in
  `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`, posted in the one sweep after the whole HR module. Do
  **not** invent an HR-side posting mechanism here.
- **Computing payroll.** HR exposes the rental/surcharge amounts; payroll consumes them.
- **Depreciation, valuation, capitalisation and disposal accounting** — Finance's, read-only here.
- **Fleet and plant maintenance workflows** — Maintenance's.
- **The module-wide W3 authorization sweep.** This area gates its own 82 routes; the other ~100
  ungated HR controllers stay on the W3 list.

---

## 7. Slice plan

| # | Slice | Delivers |
|---|---|---|
| **0** | **Prove the ground** | Call all 82 routes. Confirm or kill D-a … D-i. Diagnostic only, no production code |
| **1** | Authorization + the actor | Role/permission gates over all 82 routes; acknowledgement bound to the assignee (D-b); self-or-HR on the ownership reads |
| **2** | The register, and where an asset comes from | **D-m first** — refusals that speak — then `AssetSource` + `FixedAssetId` link, read-through of Finance fields, `AdditionalRemarks`, `InsuranceExpiryDate`, `UnitId` on create (AST-4, AST-7, AST-11, D-i(b)), **and D-j**, the PUT that erases what it omits. First migration of the area |
| **3** | Requisition on the workflow engine, and on behalf | **D-k first** — a real approver from the token — then D3's workflow chain, `BeneficiaryEmployeeId` (AST-6b), and D-e's fulfilment set |
| **4** | Assignment integrity | The availability guard (AST-2, D-a), the return consistency check (D-c), **D-l** — transfers made creatable at all — then transfer onto the same engine |
| **5** | Responsibility and terms | QuestPDF document, download + email, recorded on the assignment (AST-5, AST-5b) |
| **6** | The employee's own surface | `/hr/assets/me` + `EmployeePortalController` routes: my assets, acknowledge, request, request on behalf (AST-6, AST-8) |
| **7** | Damage and surcharge | The surcharge record, its approval, its recovery route (AST-3, D-d) |
| **8** | Rental and the payroll seam | `IsRentable`, the assignment's rental terms, the read-only projection (AST-9, AST-10) |
| **9** | Maintenance monitoring | Schedule written from the interval, due/overdue reads, the reminder sweep (AST-1, D-h) |
| **10** | Exit clearance | FR-HR-183 — clearance lines sourced from unreturned assignments, closing 9b D4 |
| **11** | Reminders and reports | Insurance expiry, overdue returns, the asset register report |
| **12+** | Screens, then the content audit | Admin + HR screens, then the endpoint-by-endpoint content audit that areas 11–23 proved is not optional |

Slice numbering is indicative; it will move as slice 0 reports.

---

## 8. Log

### Slice 0 — prove the ground. 2026-08-23. **41/41, run twice** (stamps 160002, 160003). Diagnostic only, no production code changed.

**82 of 82 route templates executed**, 96 calls, against three purpose-minted employee-linked
actors (`hr` with role HR, `plain` and `other` with role Employee). Harness:
`D:\Rhema\TDC ERPS\dev-harness\hr-assets\run-slice0.mjs`.

**Every one of the nine source-read claims held.** In particular the assignment defect is worse in
practice than on paper: assigning an already-held asset to a second employee succeeds, the asset's
`CurrentAssignedToId` silently changes hands, and the first assignment is left `Active` — so the
register then reports **two active holders for one asset** and no read distinguishes them.

**Three defects were found that the source did not show**, and two of them are the significant
findings of this slice:

- **D-k** — requisition approval has a hard-coded approver GUID with a `// TODO` beside it, for an
  employee that does not exist. Every approve is a 500. Every fulfil is therefore a 400. The whole
  requisition pipeline is dead, and it is the pipeline the change document wants on the portal.
- **D-l** — transfer create passes a user id where an employee FK is required. Every create is a
  500, so all eleven transfer routes have never operated on a real row.
- **D-j** — the asset PUT is full-replace and erases any field the payload omits. Found because the
  harness's own D-h assertion failed: it measured the maintenance interval *after* an unrelated
  edit and read null where the create had written 90. The harness bug was real; so was what it
  exposed.

Both 500s are invisible from the status code alone — Staging returns an opaque body — and were
identified only by redirecting the API's stdout and reading the FK-547 stack. That is now written
into the harness README as the standing method.

**What works, and will not be re-litigated:** asset types and attributes; the register with
attribute values, images and attachments; assignment create / read / edit / acknowledge / return;
the maintenance log including complete; requisition create (its actor already comes from the token,
correctly) and reject; and all ten DELETEs. Every DELETE is soft — after three runs the fixture
rows are still present with `IsDeleted = 1` and `WHERE IsDeleted = 0` returns 0 on every table.

**Two notes for later slices.** (1) `AssignmentStatus` and the other asset enums serialise as
**strings** on the read DTOs, not ints — an assertion comparing to `1` measures nothing. (2) The
soft delete means a unique index added in a later slice will not release on delete; area 13 hit
that five times.

---

### Slice 1 — authorization, and the acknowledgement actor. 2026-08-23, **45/45, run twice** (stamps 161001, 161002). No migration.

Two files changed: `AssetsController.cs` and `AssetsServices.cs`.

**All 82 routes now carry an explicit gate.** 71 are `[Authorize(Roles = HrRoles)]` — SuperAdmin,
TenantAdmin, HR. Eleven are self-service and carry a bare `[Authorize]` **plus a matching check in
the service**, because an attribute cannot express *"this employee, on this record"*: it does not
know whose record it is. The eleven are the type picker, the four "what do I hold" reads, the
acknowledge action, and the five requisition routes AST-6 puts in an employee's hands.

⚠ **The class could not simply carry the role list.** Stacked `[Authorize]` attributes are **ANDed**,
so a class-level role gate cannot be widened per action. The class stays at bare `[Authorize]` and
every action states its own — more attributes, but no route inherits a gate by accident and a new
action with none is visibly ungated rather than quietly protected.

**The sweep is exhaustive, not sampled.** All 71 administrative routes are called with a plain
`Employee` token and must answer 403 — 0 leaks. It can be exhaustive because authorization runs
*before* model binding, so a nil guid and an empty body still reach the decision; no valid payload
has to be invented per route. The four writes slice 0 proved an employee could really perform
(create a type, create an asset, **dispose of an asset**, read everyone's assignments) are then
re-run with genuine payloads, because a 403 on a nonsense id could in principle come from somewhere
other than the gate.

**D-b is flipped and the flip is the proof.** A stranger acknowledging someone else's assignment
was asserted as *succeeding* in slice 0; it is asserted as a 403 here. **HR is refused too, on
purpose.** Acknowledgement is the employee's word that they received the asset and accept its terms,
so it is not an administrative step anyone can take for them — `AssetActor.EnsureIsSubject` is
reserved for exactly that kind of act, and it is the only one today. Where an employee cannot reach
the portal at all, AST-5's printed and physically signed responsibility form (slice 5) records that
as what it is, rather than HR ticking the box in the employee's name — the same defect with better
manners. Two refusals in a row are asserted to leave `employeeAcknowledged` false, so the rule is
proved to have *held* and not merely to have thrown.

**A guard came with it:** a returned assignment can no longer be acknowledged. Slice 0's fixture
could acknowledge a closed record, which would have let a signature appear after the asset was back
in the store.

**HR's own access is re-asserted throughout**, and the unlinked `admin` login is asserted separately:
it passes on the HR branch and never on the self branch, because `CallerEmployeeId` returns null for
a login with no employee record and null is never treated as a match. A gate that locks out the
people who run the process is a regression wearing a fix's clothes.

**Found, recorded, not fixed here — D-m.** See §3.4. The 403s speak; the domain refusals do not,
because the middleware discards `InvalidOperationException` messages. Slice 1's harness asserts the
mute text deliberately so the flip is visible when slice 2 lands.

**`run-slice0.mjs` is a historical record from here on.** Five of its assertions (one D-b, four D-f)
describe behaviour this slice fixed, so it now fails on exactly the lines it was written to prove.
That is the design working; its header lists which five.
