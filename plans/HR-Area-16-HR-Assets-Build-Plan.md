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
| **Backend today** | 10 entities, **84 endpoints** on one controller (82 ported + 2 added in slice 2b), 1,634 lines of service, 1,193 lines of DTOs, 939 lines of mapping, 584 lines of repositories |
| **Backend proven** | *(at survey)* **Nothing** — all ten stores held 0 rows. Slice 0 executed all 82 routes and found **two sub-surfaces that can never have worked** (D-k, D-l) |
| **Frontend today** | **none** — no `asset*.service.ts`, no screen under `/hr` or `/administration/hr` |
| **Authorization today** | *(at survey)* one bare `[Authorize]`, **no gate on any of the 82 routes**. **Closed in slice 1**: 71 routes `[Authorize(Roles = HrRoles)]`, 11 self-service routes gated by `AssetActor` on the service side |
| **Status** | 🔨 In progress. Decisions D1–D8 settled. **Slices 0–4 green twice — 481 assertions.** The register and the asset now agree with each other: an asset cannot be in two people's hands, a return cannot contradict itself, and a completed transfer moves the custody record rather than a pointer |
| **Harness** | `D:\Rhema\TDC ERPS\dev-harness\hr-assets\` — `api.mjs`, `setup.mjs`, `workflow-definition.mjs`, `run-slice0.mjs` … `run-slice4.mjs` |
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
  ✅ **Fixed in slice 4.** The guard lives in `AssetIntegrity.RequireAssignable`, shared by the two
  paths that issue assets — a direct assignment and a requisition fulfilment — so the refusal is the
  same sentence whichever door the user came through, and a rule added later cannot land on only one
  of them. It also asks the `IsAssignable` question, which existed from the port and which
  **neither** path had ever read.
- **D-b — acknowledgement has no actor.** `AcknowledgeAssignmentAsync` (`:737`) sets
  `EmployeeAcknowledged = true` for whoever calls it. `AcknowledgeAssignmentDto` carries only
  `AssignmentId`. Nothing checks that the caller is the assignee, and nothing records who
  acknowledged. The area-9 "acknowledge-with-no-actor" shape, again.
- **D-c — return can contradict itself.** `ReturnAssetAsync` (`:752`) writes
  `ReturnedInGoodCondition` and `DamageReported` straight from the payload with no consistency
  check, then sets asset status from `DamageReported` alone. A return can be simultaneously "in
  good condition" and "damaged".
  ✅ **Fixed in slice 4** by `AssetIntegrity.RequireConsistentReturn`, which refuses five internal
  contradictions: both-at-once, damage with no description, liability without damage, a repair or
  replacement cost without damage, and "good condition" alongside a Poor or NonFunctional condition.
  The rules are deliberately about the record contradicting *itself* — whether an employee should be
  charged is slice 7's decision, but whether an asset came back both fine and broken is not a
  decision at all.
- **D-d — the damage fields are inert.** `EmployeeLiable`, `RepairCost` and `ReplacementCost` are
  stored and never read by anything. There is no surcharge, no recovery, no route to payroll or to
  the separation settlement. **AST-3 has a field but no feature.**
- **D-e — `FulfillAssetRequisitionDto.AssignedAssetIds` is a `List<Guid>`; the entity has a single
  `AssignedAssetId`.** Fulfilling with three assets creates three assignments but the requisition
  can only remember one of them. ⚠ **Reclassified by slice 0 to a SOURCE claim, not a measured
  one**: fulfilment answers 400 because it requires an approved requisition, and D-k means no
  requisition can ever be approved. The mismatch is real in the source but cannot be demonstrated
  by execution until slice 3 lands. ✅ **Fixed in slice 3** — `AssignedAssetId` is dropped and each
  assignment cites its `RequisitionId`, so a fulfilment of three assets is three facts.
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
  ✅ **Fixed in slice 3**: both stamps take the actor from the token, and an actor whose login is
  not linked to an employee is refused in words rather than silently stamped as nobody.
- **D-l — ⚠ the transfer sub-surface has never created a row.**
  `AssetTransferService.CreateAsync` (`:1536`) calls
  `dto.ToEntity(tenantId, userId, userId, transferNumber)`, passing the **ApplicationUser** id as
  `InitiatedById` — an **Employee** FK. FK error 547 again, every time. Nothing can be created, so
  the other ten transfer routes have no subject: slice 0 called all eleven and the ten reads and
  writes answer 400/404 on a well-formed id. This is the same token-actor confusion as D-k, in a
  second place, and it is the shape [[hr-attendance-actor-conventions]] already warned about —
  *`ApprovedById` is an Employee FK, unlike Leave's*.
  ✅ **Fixed in slice 3b**, pulled forward from slice 4 because a surface whose records cannot be
  created cannot have its approvals wired to anything. **Two sites, not one**: `ApproveAsync` put
  the user id into `ApprovedById`, an Employee FK as well, so even a transfer created by some other
  route could never have been approved.

- **D-m — every domain refusal in this area is mute.** Found while writing slice 1.
  `GlobalExceptionHandlingMiddleware` **discards** an `InvalidOperationException`'s message and
  substitutes the fixed string *"The operation is not valid for the current state of the object."*;
  `ArgumentException` becomes *"Invalid argument provided."* — which also means every "not found" in
  these services arrives as an opaque 400. So a rule that fires correctly still cannot tell the user
  what to do about it. `UnauthorizedAccessException` is the exception: it is passed through **with
  its own message**, which is why slice 1's 403s can be asserted on their wording.
  ✅ **Fixed in slice 2** by `AssetsWorkflowException` + a middleware case, mirroring what area 14
  did for awards. This is the **sixth** HR area to need that same remedy — worth raising at
  finalization, because the middleware swallowing these messages is a platform default every module
  has had to work around one at a time.

- **D-n — seven repository reads fill the same DTO differently.** `GetByAssetTypeAsync`,
  `GetByStatusAsync`, `GetByLocationAsync`, `GetByEmployeeAsync`, `GetAvailableForAssignmentAsync`,
  `GetDueForMaintenanceAsync` and `GetWarrantyExpiringAsync` all project into
  `CompanyAssetSummaryDto`, exactly as `GetByTenantAsync` does, but loaded fewer navigations — so
  `locationName`, `unitName` and `currentAssignedToName` came back **null on some filters and
  populated on others**, with nothing in the payload for a screen to tell which list it held.
  ✅ Evened up in slice 2b.
- **D-o — a write response returned names it had never loaded.** `CompanyAssetService.CreateAsync`
  and `UpdateAsync` mapped the entity straight from memory, where no navigation had been loaded, so
  `assetTypeName`, `locationName` and `unitName` were blank on every create and edit — while the
  very next GET filled them in. A screen rendering what it just saved showed empty columns until the
  user refreshed. ✅ Fixed in slice 2b by re-reading with details before mapping.
- **D-o(b) — and the same shape inverted, on two by-id READS.** `GET attributes/{id}` and
  `GET attribute-values/{id}` went through the generic `GetByIdAsync`, which loads no navigations,
  while the list reads beside them carried the `.Include` all along. The second is worse than a lost
  label: `AssetAttributeValueDto.DataType` is projected **off the navigation**, so a null one did not
  merely blank a name — it **reported the wrong data type for the value being returned**. ✅ Fixed in
  slice 2b with two repository reads that load the navigation.

- **D-p — a converted rule that could never fire.** Fulfilment asked `Status != Available` before
  `IsCurrentlyAssigned`, and assigning an asset sets **both** flags — so an asset in somebody's hands
  was always refused as *"not available; its status is Assigned"*, and the `Conflict` rule beside it
  had no path to it at all. It was one of the four slice 2 converted and printed as unreachable;
  it turned out to be unreachable for a second reason nobody had noticed. ✅ Fixed in slice 3 by
  asking the specific question first. Found only because an assertion about one refusal was answered
  by a different one.

- **D-q — a requisition could be created already Approved.** Found while wiring D3.
  `CreateAssetRequisitionDto` carried a `Status` that `ToEntity` wrote straight onto the record, and
  `UpdateAssetRequisitionDto` carried a nullable one that `UpdateEntity` applied to an **existing**
  record. So `POST requisitions {"status": 3}` produced an approved requisition with no approver, no
  approval date and no workflow instance — and HR's *"only an approved requisition can be
  fulfilled"* gate was satisfied by it. The edit path was worse: the requester could set their own
  draft to `Fulfilled`. ✅ **Fixed in slice 3b** — the field is gone from both DTOs and the status
  belongs to the approval workflow.
- **D-r — `BeneficiaryEmployeeId` on the update DTO was read by nothing.** Slice 3 added AST-6b's
  beneficiary to both the create and the update DTO, with the same documentation on each;
  `UpdateEntity` never assigned it. Correcting who a request was for silently did nothing and the
  form showed the old name back on the next read — the area-14 shape of a field that exists,
  type-checks, serialises and carries nothing. ✅ **Fixed in slice 3b**, with the same on-behalf
  authorization the create path runs.
- **D-s — a transfer had no destination rule, and one transfer type has nowhere to go.**
  Nothing checked that an employee-to-employee move named an employee; `CompleteAsync` would then
  have set the asset's holder to **null**, leaving the register saying nobody holds it and no record
  of who did. And `HRAssetTransferType.DepartmentToDepartment` has no column on either side of
  `AssetTransfer` and no branch in `CompleteAsync` — completing one moved nothing and reported
  success. ✅ **Fixed in slice 3b**: each type's destination is required and checked to exist, and
  the department type is refused *in words* pointing at unit-to-unit, which does work. Never
  observable before, because D-l meant no transfer existed.
- **D-t — three transfer reads fed a summary they could not fill.** `GetByAssetIdAsync`,
  `GetByStatusAsync` and `GetPendingTransfersAsync` loaded fewer navigations than
  `GetByTenantAsync`, while all four project into `AssetTransferSummaryDto` — which reads the
  asset's name, and then reads From/To off the employee, the location **or** the unit depending on
  the transfer's type. So `assetName` was blank everywhere and `fromName`/`toName` blank for every
  location and unit transfer. The **fifth** instance of the uneven-`.Include` family in this area
  (D-n, D-o, D-o(b), `requisitionNumber` in slice 3). ✅ Evened up in slice 3b.

- **D-u — deleting an assignment left its asset stuck, permanently.** Found while writing slice 4.
  `DeleteAsync` soft-deleted the assignment and left the asset carrying `IsCurrentlyAssigned = true`,
  `Status = Assigned` and a `CurrentAssignedToId` pointing at a holder whose assignment no longer
  existed. Harmless while D-a was open — anyone could assign over it. **Closing D-a is what turned it
  into a trap**: the new guard refuses to issue such an asset to anybody, and no return can free it
  because the record a return acts on has gone. ✅ Fixed in slice 4: deleting the only thing that
  says an asset is held now says it is not held, guarded so that deleting a *historical* assignment
  cannot release an asset somebody holds today.
- **D-v — an assignment could be returned twice.** `ReturnAssetAsync` had no status guard at all, so
  a second return overwrote the first one's condition, notes, damage description, liability and
  costs, and put a Damaged asset back to Available. ✅ Fixed in slice 4. Its two employee foreign
  keys were unchecked too (`ReturnedToId`, and `ApprovedById` on create), which is the D-k/D-l shape
  a third time: an id that does not exist reached SQL and came back as a 500.

**The transfer-completion gap, recorded in slice 3b, is closed.** Completing an employee-to-employee
transfer used to move `CurrentAssignedToId` and nothing else. It now closes the outgoing assignment
and opens one for the recipient — carrying the terms over rather than inventing them, citing the
transfer in a new `AssetAssignment.TransferId` column, and **not** carrying the previous holder's
acknowledgement, which would forge a signature (D-b). A transfer whose "from" employee no longer
holds the asset is refused rather than completed against stale facts.

**Still open, and which slice owns each:** **D-d** (slice 7 — the damage fields the return now
records coherently are still read by nothing) and **D-h** (slice 9).

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
| **0** | ✅ **Prove the ground** | Call all 82 routes. Confirm or kill D-a … D-i. Diagnostic only, no production code |
| **1** | ✅ Authorization + the actor | Role/permission gates over all 82 routes; acknowledgement bound to the assignee (D-b); self-or-HR on the ownership reads |
| **2** | ✅ Refusals that speak | D-m — `AssetsWorkflowException`, 404/409/400, no migration |
| **2b** | ✅ The register, and where an asset comes from | `AssetSource` + `FixedAssetId` link and the picker that sets it, `AdditionalRemarks`, `InsuranceExpiryDate`, `UnitId` end to end (AST-4, AST-7, AST-11, D-i(b)), **and D-j**. **First migration of the area** |
| **3** | ✅ The requisition pipeline runs | D-k (a real approver), AST-6b on-behalf, D-e (fulfilment recorded on the assignments), D-p |
| **3b** | ✅ Both approvals on the workflow engine | D3 — the four-step recipe on `HrAssetRequisition` and `HrAssetTransfer`, Draft → submit → decide → recall; **D-l** pulled forward so transfers exist at all; D-q, D-r, D-s, D-t |
| **4** | ✅ Assignment integrity | The availability guard (AST-2, **D-a**), the return consistency check (**D-c**), the transfer-completion gap, and two holes that made an asset unusable: a deleted assignment that never released it, and a return that could be taken twice. Migration `AddAssetAssignmentTransferLink` |
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

---

### Slice 2 — refusals that speak. 2026-08-23, **85/85, run twice** (stamps 162001, 162003). No migration.

Three files: a new `AssetsWorkflowException`, one case in `GlobalExceptionHandlingMiddleware`, and
28 converted throw sites in `AssetsServices.cs`.

**The number that made this a slice of its own: eighteen of the area's twenty-eight refusals were
"not found" answering 400.** The middleware discards an `ArgumentException`'s message and
substitutes *"Invalid argument provided."*, and an `InvalidOperationException`'s and substitutes
*"The operation is not valid for the current state of the object."* So a caller could not
distinguish a disposed asset from a malformed payload, and no screen could decide whether to
re-fetch, show "no longer available", or highlight a field. Every one of those is now a **404 that
names what is missing**; state clashes are **409**; a payload wrong on its own terms is **400**.

**The messages were rewritten, not just re-typed.** The ported text named CLR types —
*"AssetTypeAttribute 3f2… not found."* Text that reaches a user is written for the user: *"No asset
type attribute was found with id …"*. A message only became displayable when it started being
displayed, so this was the moment to fix it.

**Ten guards were deliberately left alone.** The `"No tenant is associated with the current user."`
checks are not domain rules a caller can act on — the tenant comes from the token, so reaching one
means the token itself is malformed. A generic 400 is the right answer to a condition that should
be unreachable. Asserted in the patch script rather than trusted: after conversion, every surviving
`InvalidOperationException` in the file was checked to be one of those ten.

**The harness declares what it cannot reach.** Four of the ten converted rules cannot be provoked
today — two live inside fulfilment, which D-k blocks, and two need a transfer to exist, which D-l
blocks. `run-slice2.mjs` prints them with the slice that will own them (3 and 4) instead of omitting
them, because a suite that silently skips what it cannot reach reads as coverage it does not have.
A fifth, *"At least one asset must be assigned"*, is unreachable for a better reason —
`[MinLength(1)]` on the DTO refuses an empty list before the service runs — and that redundancy is
asserted rather than assumed.

**Slice 1's one mute assertion was flipped here**, from a 400 with the fixed string to a 409 with
the real sentence, and slice 1 re-run twice at 45/45. Unlike slice 0, slice 1 stays a live
regression suite: only that line moved, and the comment on it says why.

⚠ **A known, correct piece of litter:** each slice-2 run leaves one rejected requisition behind,
because "a decided requisition cannot be withdrawn" is exactly the rule the slice asserts and it
binds HR too. The row is the rule working, not a cleanup failure.

---

### Slice 2b — the register, and where an asset comes from. 2026-08-23, **94/94, run twice** (stamps 163004, 163005). Migration `AddHrAssetSourceAndFixedAssetLink`.

Delivers **AST-11**, **AST-7**, **AST-4**, and closes **D-i(b)**, **D-j**, **D-n**, **D-o** and
**D-o(b)**. Two new routes (84 total, all gated). Slices 1 and 2 re-run at 45/45 and 85/85.

**AST-11 is a boundary, so it is built and tested from both sides.** HR can list Finance's
fixed assets (`GET fixed-assets/linkable`) and register one (`POST from-fixed-asset`), which
stamps `Source = FixedAssetsModule` and a real FK to `FixedAssets`. The detail read then pulls
Finance's figures **live** — a net book value moves at every depreciation run, so a stored copy is
wrong within the month. And HR is *refused*, by name, any edit to the purchase cost, asset number or
purchase date of a linked asset, and refused its disposal outright, each refusal naming the module
to go to instead. A boundary that is only documented is not a boundary.

**The dependency is the statement.** HR reads Finance through `IFixedAssetService`, Finance's own
service, not its tables — so everything HR can see is something Finance chose to expose, and nothing
here can write. ⚠ It has to be imported as a **using alias**: `Core.Interfaces.Finance` also declares
an `IAssetTransferService`, and so does `Core.Interfaces.HR`, so a namespace import made every
mention of that type in the file ambiguous — including HR's own service at the bottom of it. That is
the §3.3 collision at a third level, after the entities and the tables. The entity file takes an
alias too, for the same reason one step removed.

**The picker returns already-linked assets rather than filtering them out**, flagged, with the id of
the HR asset holding the link. A picker that silently omits them leaves a user hunting for something
that is right there; one that shows it greyed answers the question they actually have.

⚠ **There is deliberately no unique index on `FixedAssetId`**, though the rule is one HR entry per
fixed asset. Every delete here is a **soft** delete, so a unique index would hold the slot after an
HR entry was removed and refuse the re-link forever, with a constraint violation no user could read.
The rule lives in the service where it can see `IsDeleted` and name the asset already holding the
link. The harness proves the point directly: link, soft-delete, link again — which is the exact
sequence an index would have made impossible.

**Provenance is not user-editable.** `Source` and `FixedAssetId` are absent from both write mappings,
so the only way into `FixedAssetsModule` is the linking endpoint. Asserted, not assumed: a PUT
carrying `source: 'HrCreated'` is proved to leave both untouched — otherwise anyone could relabel a
Finance-owned asset and walk past every guard that reads the flag.

**D-j turned out to be a mischaracterisation, and the harness says so.** The PUT is full-replace *by
design*; slice 0 "lost" fields only because the probe sent a partial body. What actually mattered is
whether the read returns everything the write accepts, so a load-then-save cannot destroy data —
and before this slice it did not, because `unitId` was on no DTO a form could round-trip. That is now
asserted as a 20-field read-edit-save round-trip, which is the honest form of the test.

**Three defects found while building, all fixed here.** D-n: seven reads filling one DTO differently.
D-o: create and update returning names they had never loaded. D-o(b): the inverse on two by-id reads
— and the attribute-value one was reporting the **wrong data type**, not merely a blank name.

**Two lessons, both about method rather than about assets.** First, *do not predict which half of a
pair is broken*. D-o(b) was written expecting the create response to be blank, because that is what
the register did; the opposite was true. The assertion that finds it either way — *the by-id read and
the list read agree* — is the one now in the file. Second, the repair that took an extra build was
mine: a patch script assigned the second `old`/`new` inside a branch that did not run, so the first
replacement's text leaked into the second and one service was handed another's method body. The
compiler caught it only because the return type stopped matching its interface.

---

### Slice 3 — the requisition pipeline runs, end to end, for the first time. 2026-08-23, **49/49, run twice** (stamps 164002, 164003). Migration `AddAssetRequisitionBeneficiaryAndFulfilmentLink`.

Closes **D-k**, **D-e**, **D-p** and delivers **AST-6b**. Slices 1, 2 and 2b re-run at 45/45, 85/85
and 94/94. ⚠ D3 — the workflow engine — is **not** in this slice; it is slice 3b. This one is about
making the ported pipeline work at all, and that is a separate thing from routing its approvals.

**D-k is dead.** `ApproveAsync` and `FulfillAsync` both carried
`Guid.Parse("D1D0261F-934D-4809-95EF-CD76156694A5")` behind a `// TODO`, for an employee that has
never existed on this database. `ApprovedById` and `FulfilledById` are **Employee** FKs, so SQL
rejected every UPDATE with error 547 and both endpoints answered 500 — always, for everyone, since
the port. Both now take the actor from the token. An actor whose login is **not** linked to an
employee is refused *in words* rather than silently stamped as nobody: that silent stamp is what the
hard-coded GUID was a clumsy attempt to avoid, and it would have been the same hole with better
manners.

**The four rules slice 2 could not reach are now proved.** Slice 2 converted them to speaking
refusals and then printed them as unreachable, because nothing could get past approval to provoke
them. Three passed on the first attempt. The fourth found **D-p**: it could never fire at all.

**AST-6b, and the consequence that actually matters.** `BeneficiaryEmployeeId` sits beside
`RequestedById` — two columns, because one cannot answer "who did this, and to whom". The visible
payoff is at fulfilment: **the asset is assigned to the beneficiary, not to whoever typed the form**,
and the harness asserts it is emphatically not the HR officer who raised it. The read gate also had
to widen to both actors: gating on the requester alone would have hidden an employee's own
requisition from them the moment somebody raised it on their behalf.

**On-behalf is HR, or the beneficiary's recorded line manager — and the second branch will rarely
fire.** Measured while writing it: **181 of 6,822** live employees carry a `ManagerId`, 2.7%. The
rule is written against the data model rather than against today's data, so it starts working the
day the org chart is maintained. The harness mints a fourth actor who genuinely reports to another
so the branch is *proved* rather than assumed dead — the only honest way to test a rule the live
data cannot exercise. Same unmaintained-org-data seam that keeps FR-HR-080/181 deferred.

⚠ **The scaffolded migration was wrong and was rewritten.** EF saw one nullable `Guid` column leave
and another arrive on the same table and inferred a **rename** of `AssignedAssetId` to
`BeneficiaryEmployeeId`. A rename keeps the data: the old column held a `CompanyAssets.Id` and the
new one holds an `Employees.Id`, so on any database where fulfilment had ever run, every populated
row would carry an asset's key in an employee foreign key — and the FK added three lines later would
fail with 547. It renamed the index too. Measured before deciding: 14 rows, **0 with an
`AssignedAssetId`**, precisely because D-k meant none was ever fulfilled — so the rename would have
been harmless *here*. Rewritten as drop-then-add anyway: the next database is not promised to be
this one. The `Down` says plainly that the old column returns empty, because the fact now lives on
the assignments and more completely than it ever lived in one column.

**Two more uneven-`.Include` instances, same family as D-n.** `GetByRequestedByIdAsync` never loaded
`RequestedBy`, so `requestedByName` was blank on an employee's own list of their own requests — the
one list where it matters most. And the assignment detail read gained `requisitionNumber` in its DTO
and mapping without the `.Include` that feeds it, so it came back null. That shape has now appeared
**four times** in this area; the standing lesson is that a mapping and a read are one change.

### Slice 3b — both approvals on the workflow engine, and transfers made to exist. 2026-08-23, **135/135, run twice** (stamps 165005, 167001). No migration.

Delivers **D3** and closes **D-l**, **D-q**, **D-r**, **D-s**, **D-t**. Slices 1, 2, 2b and 3 re-run
at 45/45, 85/85, 94/94 and 49/49 — 449 assertions for the area.

**Why D-l came forward from slice 4.** You cannot put a surface's approvals on a workflow engine
while nothing on that surface can be created. `CreateAsync` passed the caller's **user** id into
`InitiatedById`, an **Employee** FK, so every create answered 500 and all eleven transfer routes had
never touched a real row. Fixing it here meant transfers could be wired once, with requisitions,
instead of being wired twice. ⚠ It turned out to be **two sites**: `ApproveAsync` did the same thing
to `ApprovedById`, so even a transfer created some other way could never have been approved.

**The status was a payload field, and that is the finding that matters most (D-q).**
`CreateAssetRequisitionDto` carried a `Status` written straight onto the record. `{"status": 3}`
therefore created a requisition that was already **Approved** — no approver, no approval date, no
workflow instance — and HR's *"only an approved requisition can be fulfilled"* gate accepted it.
The update DTO carried a nullable one applied to an existing record, so a requester could push their
own draft to `Fulfilled`. Neither is reachable now: a requisition is born a Draft and every state
after that belongs to the engine. The harness asserts the flip directly — a create that asks to be
born Approved comes back a Draft, and fulfilling it is refused.

**The lifecycle both surfaces now share.** Draft → `submit` → Submitted/Pending → Approved or
Rejected, with `recall` returning it to Draft for the person who raised it. Four consequences worth
recording: a draft is the requester's to edit or withdraw and a submitted one is not (*"recall it
first"*, rather than a record changing under the approver reading it); rejection stays **terminal**
here rather than returning to Draft, because slice 2 already settled that a decided requisition is
the record of a decision; recall is the requester's alone, and HR is told to reject instead; and
`HRAssetTransferStatus` gained a `Draft = 0` because it had no state before `Pending` — without it
"pending" meant both "nobody has sent this" and "an approver is holding it", and a recall had
nowhere to land. `AssetRequisitionStatus` needed nothing: `Submitted` already meant exactly that.

**⚠ The entity types are `HrAssetRequisition` and `HrAssetTransfer`.** `AssetTransfer` as a workflow
key already means **Finance's fixed-asset transfer** — `WorkflowEntityDisplayService` resolves it to
that entity and sets a fixed-asset `ActionUrl`. Registering HR's under the same key would have sent
an HR approver's notification to a fixed-asset screen and reported no error at all. §3.3's collision
now reaches a **fourth** level: entity, table, interface, and workflow entity-type key.

**Who may not sign, and why it is not `preventInitiatorApproval`.** Both definitions leave that flag
**false**, deliberately. It guards "nobody approves their own request", where the initiator is the
one who benefits — but HR raises requisitions on a new joiner's behalf (AST-6b) and raises every
transfer about somebody else's equipment, so set true it would silently have meant "a second HR
officer must". The real conflict is guarded on the record instead, by employee id:
`RequireNotTheBeneficiary` refuses anyone approving the issue of an asset to **themselves** —
computed as `BeneficiaryEmployeeId ?? RequestedById`, the same expression fulfilment uses to decide
who receives it, so the rule and its consequence cannot drift apart — and `RequireNotTheRecipient`
does the same for a transfer into the caller's own hands. This is area 9b's lesson applied rather
than rediscovered.

**The two assertions that separate "wired" against "appears wired".** The first full run was
129/129 on the first attempt, which on this area is a warning rather than a result: every status in
it would read the same if the engine were bypassed and the adapter simply wrote them. Two more were
added and they are the ones worth keeping. With **no published definition**, a submit must be
refused *in words* and the requisition must still be a Draft — it is not, if the engine was never in
the path. And with a definition routed to an authority the HR officer does **not** hold, the
approve must be refused by the **engine** (`CanUserApproveAsync`) after the controller's role gate
has already let them through. Both pass; the first one **failed** when written, and that failure was
the harness's own — see below.

**The instrument lied, quietly, and that is the transferable lesson.**
`retireActiveDefinitions` swallowed every failure into an empty `catch` and returned 0, which the
run read as *"nothing to retire"* rather than *"nothing was retired"*. It was running as the HR
actor, and `POST definitions/{id}/retire` is gated
`SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager` — so every call 403'd, definitions piled
up across runs, and the one assertion that needed there to be **no** active definition failed while
the helper reported success. It now borrows `admin`, hands the caller's token back, and **throws**
when it cannot do its job. Third area running in which a harness helper cried wolf or lied on its
first outing.

**Two response envelopes, recorded because they cost a run each.** `POST Workflow/definitions`
answers `{ success, data }`, not the definition — reading `.id` off the envelope yields `undefined`
and the next call goes to `/definitions/undefined`. The admin listing answers
`{ success, data: [...], metadata }` — not a bare array and not `items`.

**⚠ Entity types and definitions are TENANT-scoped, so the harness seeds and publishes as the HR
actor**, not as `admin`. Seeding on the wrong tenant registers the types where nothing will look for
them, and every submit then refuses with *"is not configured"* while the seed call itself returns
200.

**What slices 2 and 3 had to change, and what they did not.** Both decide a requisition, so both now
publish a definition and submit before deciding, and neither passes `status` on a create. Nothing
either of them **asserts** was touched: D-k's real approver, D-e's two facts and AST-6b's beneficiary
all still prove themselves. The engine's refusals are carried through as `AssetsWorkflowException`
rather than left to the middleware, so *"No active workflow definition found for entity type
'HrAssetRequisition'"* reaches an administrator instead of becoming *"The operation is not valid for
the current state of the object."* — D-m's complaint, arriving from outside the area.

### Slice 4 — assignment integrity: the register and the asset made to agree. 2026-08-23, **73/73, run twice** (stamps 170001, 171001). Migration `AddAssetAssignmentTransferLink`.

Closes **D-a**, **D-c**, **D-u**, **D-v** and the transfer-completion gap slice 3b left here.
Slices 1, 2, 2b, 3 and 3b re-run at 45/45, 85/85, 94/94, 49/49 and 135/135 — 481 assertions.

**One guard, two doors.** Slice 3 gave fulfilment an availability rule and slice 0 proved the direct
assignment path had none, so the same question was being asked in two places, in different words,
with different answers. Both now call `AssetIntegrity.RequireAssignable`. The order of its three
questions is load-bearing and is defect D-p written down: assigning sets **both**
`IsCurrentlyAssigned` and `Status = Assigned`, so asking about status first answers every held asset
with *"not available; its status is Assigned"* — true, useless, and it leaves the specific rule
below it permanently unreachable. Sharing the guard also gave fulfilment the `IsAssignable` check,
which **neither** path had ever made.

**Closing one defect turned a dormant one into a trap.** `DeleteAsync` left the asset marked
assigned to a holder whose assignment no longer existed (D-u). That was survivable while D-a was
open — anyone could assign over it. With D-a closed the asset becomes **unusable**: nothing can
issue it, and no return can free it because the record a return acts on is gone. Worth stating
generally: a guard that makes the system stricter can make an existing hole load-bearing, so the
question after closing a defect is what *used to* paper over it.

**The consistency rules are about the record contradicting itself, not about policy.** Whether an
employee should be charged for a cracked screen is slice 7's decision. Whether a return can say the
asset came back in good condition *and* came back damaged is not a decision at all — and slice 0
measured that it was accepted and stored exactly as sent, so nothing downstream could decide whether
to raise a surcharge or hold an exit clearance: the record answered yes to both questions. Five
contradictions are now refused, and the four fields D-d will read (`EmployeeLiable`, `RepairCost`,
`ReplacementCost`, `DamageDescription`) can no longer be set on a return that reports no damage.

**Custody moves, not a pointer.** Completing an employee-to-employee transfer moved
`CurrentAssignedToId` and left the old assignment `Active`: the outgoing employee still held the
asset as far as every query was concerned — including the exit-clearance hook FR-HR-183 will hang on
— and the recipient had no assignment at all: no terms, no acknowledgement to give, nothing to
return. The completion now closes the old one and opens a new one, and three details are deliberate.
The terms are **carried over** rather than defaulted, because the asset is the same asset on the
same footing and inventing a fresh set would quietly change what somebody is responsible for. The
acknowledgement is **not** carried over — that is the holder's own signature (D-b), and copying it
would forge one. And a transfer whose "from" employee no longer holds the asset is **refused**
rather than completed against facts that have moved since it was approved.

**The closed assignment reads `Transferred`, and getting there is worth recording.** It was first
written as `Returned` on the reasoning that `AssignmentStatus` lives in the HRApi-owned `HREnums.cs`,
which is overwritten byte-for-byte on every sync, so a member added there would be lost. That
reasoning was half right: the file *is* owned, but it **already carries two RHEMA-added members** —
`AppraisalStatus.Open` and `ProbationStatus.ConfirmationApproved`, each with a doc comment naming the
area that added it. The established practice is to add in place, not to work around. So
`Transferred = 6` was added the same way, and the header of `HREnums.Rhema.cs` now lists all three,
so a sync has a checklist instead of three separate memories of why a member is there.

Behaviourally nothing moved — **every** consumer of `AssignmentStatus` in the module filters on
`Active`, so a transferred row drops out of "what does this employee still hold" exactly as a
returned one does. What changed is what the record says in the one place somebody looks to find out
what happened to an asset: nobody took it back, and the next custody starts the same day.
`ReturnedToId` now carries the **recipient** rather than the processing officer, because under a
`Transferred` status "returned to" can only mean "handed to" — the officer was there to stop that
field lying while the status still said Returned.

**The migration was clean this time, and the thing to check had moved.** No invented rename — but the
scaffold also dropped and re-added `FK_AssetTransfer_Tenants_TenantId`, flipping it Cascade →
Restrict. That is **not** drift introduced here: `ConfigureGlobalTenantRelationships` has always set
every tenant foreign key to Restrict "to avoid multiple cascade paths in SQL Server", and this one
table's constraint was left on Cascade by an earlier migration. Adding
`AssetAssignments → AssetTransfer` is what made it matter — SQL Server refuses the new foreign key
while the old one still cascades — so the correction is a **precondition** of the column rather than
a side effect. Kept, guarded on `delete_referential_action <> 0` so it is a no-op on a database
already in the right state, and deliberately **not** undone by `Down`, which would leave the database
in a state the model has never described.
