# Safety, Health & Environment (SHE) — Shape, Boundaries and Integrations

**Created:** 2026-09-02. **Why:** SHE is the largest sub-module in HR by endpoint count and the
one the other documents in this folder kept getting wrong (route prefixes that don't exist, a
sanction enum member that doesn't exist, "no reports" when it has three report artefacts, "60+
entities" when it has 88). This is the one page that says what SHE is, where its edges are, and
what it needs from the rest of the system. It complements, not replaces, the SHE rows in the
Finance sweep (60–63), the integration map (9, 10, 14, 15, 22, 22b, 31), the reports catalogue
(§3.17) and the import/export catalogue (§3.10).

---

## 1. Shape (as built, area 10, closed 2026-08-15; W3 gating 2026-08-25)

| Fact | Value |
|---|---|
| Entities | **88** classes, all in `src/ErpSystem.Core/Entities/HR/StaffSafetyEntities.cs` (there is no `Safety/` folder); DTOs in `DTOs/HR/Safety*DTOs.cs` + `StaffSafetyDTOs.cs`; enums in `Enums/Safety/SafetyEnums.cs` |
| Controllers | **28** + `SheApiControllerBase`, all in `Controllers/HR/`, every route under **`api/safety/*`** — there is no `api/she/` and no `she-*` prefix anywhere |
| Route groups | `reference`, `dashboard`, `performance`, `incidents`, `hazards`, `risk-assessments`, `inspections`, `inspection-checklists`, `permits`, `ppe`, `equipment`, `signs`, `occupational-health`, `return-to-work`, `environmental` (shared by three controllers: environmental, compliance, review), `waste`, `emergency`, `committees`, `contractors`, `regulatory`, `training`, `audits`, `documents`, `corrective-actions`, `stop-work`, `reminders` |
| Endpoints | ~405 at port; 95 of HR's 799 `{id}/<verb>` action routes are SHE |
| Frontend | `frontend/src/app/hr/safety/**` — 23 route folders (audits, committees, contractors, corrective-actions, dashboard, documents, emergency, environmental, equipment, hazards, incidents, inspections, occupational-health, performance, permits, ppe, regulatory, return-to-work, risk-assessments, signs, stop-work, training, waste) + the SHE landing; admin lookups under `administration/hr/safety/*` |
| Harness | `D:\Rhema\TDC ERPS\dev-harness\hr-safety\` — slices 0–17, **1,633 assertions**; plus W3 slice 12 (82) |
| Requirement namespaces | `FR-SHE-###` (20 distinct), `FR-ENV-###` (26), `FR-CON-###` (1), `FR-PTW-###`, `FR-INC-###` — **only in `src/`**; the SHE build plan lives outside the repo |
| Spec PDFs | `D:\Rhema\TDC ERPS\Safety Spec Docs` |

**Number formats:** incidents are **D5** (`INC-2026-00001`, FR-INC-001); inspections, risk
assessments, permits, hazards and audits are D4. Every SHE number generator uses numeric max
including soft-deleted rows — the string-ordered version re-issued taken numbers once the
seeder's 3-digit rows met the generator's 4-digit ones. `ShePermitType.RoadClosure = 10` was added
(schema-safe enum add).

**Two base-class facts every SHE controller inherits:** `SheApiControllerBase` maps an
unresolvable tenant or an employee-unlinked user to **403 with a real message** ("not linked to an
employee record") — distinguish that from a permission refusal by the body, not the status; and
`SafetyBusinessRulesAttribute` (`Api/Filters/`) maps business-rule exceptions to 422 with the
rule's own text, so a refusal explains itself.

**A read trap specific to SHE's fat aggregates:** full-detail reads of incident, risk assessment,
permit, inspection, contractor, committee and return-to-work exceed SQL Server's 8,060-byte
worktable row limit once children exist and 500 without `AsSplitQuery()`. All seven have it;
any new many-include read in this area needs it too.

---

## 2. Authorization

- **Permission family:** `HR.She.{Read,Write,Admin}` (`HrPermissions.cs:171-177`;
  `SheReadPolicy`/`SheWritePolicy`/`SheAdminPolicy`), applied verb-mechanically across the 28
  controllers in W3 slice 12 (485 scripted operations): reads → Read, **all desk decisions
  including the environmental-review statutory ladder → Write**, deletes → Admin. TenantAdmin
  gains the area; a future SHE-officer role can hold the family without the HR role.
- **Two controllers are gated on Medical, not SHE:** `api/safety/occupational-health` and
  `api/safety/return-to-work` use `HrPermissions.Medical{Read,Write,Admin}Policy` because they
  carry medical-grade data (exam results, restrictions, clearance notes). See §3.1.
- **Open by design (any authenticated internal employee, actor forced to the token):** report an
  incident, report a hazard, sign a risk-assessment acknowledgement, raise a stop-work order
  (+ `mine`), read own PPE issuances (`issuances/mine`), report an environmental incident (+
  `mine`). `CreateSafetyIncidentDto.ReportedById` and the acknowledgement's `EmployeeId` were
  body-supplied at port (anyone could file as anyone) — fixed: non-HR forced to token, HR may act
  on behalf via `HoldsPolicyAsync(SheWrite)`. Acknowledgements have a duplicate guard.
- **Two reference reads are open** (`reference/incident-types` and one sibling) because the
  employee incident form feeds on them — desk-gating them made every employee incident file
  unclassified (W3 slice 12 defect).
- **The first layer:** `ExternalUserAccessMiddleware` refuses external-user tokens on every path
  outside a prefix allowlist; SHE is not on it.
- **DR-10 (SHE roles) — DELIVERED 2026-09-03.** Two seeded roles: **Safety Officer**
  (`HR.She.Read/Write` + `HR.Medical.Read/Write`, because the occupational-health registers SHE
  owns are gated on the medical family — §3.1) and **SHE Manager** (the same plus `HR.She.Admin`).
  Neither holds the HR role. **HR dropped to `HR.She.Read`**: it sees every SHE register and can
  edit none (`HrPermissions.RoleRevocations` deletes the old Write grant on existing tenants).
  The SHE menu and the `/hr/safety` layout gate on the module's own `she.access` (held by the
  two SHE roles, HR and the administrators — not by Employee/Manager; staff report from
  `/me/safety`). SHE reminder topics are addressed to the two SHE roles, with the seeded HR
  recipient re-addressed on existing tenants. The permission category label on the roles
  screen is now "Safety (SHE)"; the permission NAMES stay `HR.She.*`. The demo persona
  `she.officer` carries Safety Officer + Employee. Proven by `run-slice12-she.mjs`.

---

## 3. Ownership boundaries — decided, do not re-litigate

### 3.1 SHE ↔ Medical (area 11) — decided 2026-08-14

**SHE keeps all four health registers** — occupational-health surveillance, first-aid stations,
wellness programmes, return-to-work plans (phases + reviews). Medical keeps clinical records
(`EmployeeHealthProfile`, `EmployeeMedicalExam`, facilities, physicians, insurance, claims).
**Bridge by reference only:** `SheOccupationalHealthSurveillance.HealthcareFacilityId` FKs Medical's
facility register; `SafetyIncidentInvolvedPerson.MedicalExpenseClaimId` FKs Medical's claim;
`SafetyIncident.InsuranceProviderId` FKs `MedicalInsuranceProvider`. Never merge surveillance rows
into `EmployeeMedicalExam`. `ExaminingPhysician` stays a string (normalising onto Medical's
`Physician` is a later candidate). Both health controllers gate on the Medical policies (§2).
**Money consequence:** `SafetyIncident.ClaimAmount/.AmountPaid/.InsuranceClaimFiled` is the same
unposted insurance-claim shape as Medical's — solve them together (Finance sweep row 60).

### 3.2 SHE ↔ Training (area 7) — decided 2026-08-14 (FR-SHE-121)

**SHE keeps its own training store** (`SheTrainingPlan/Program/Attendance`, `api/safety/training`,
screens `hr/safety/training*`) because SHE attendance supports **non-employees** (contractor
workers and visitors sign by name + company, `IsEmployee=false`) and per-attendee **certificate
expiry** — neither fits area 7's employee-only enrolment. Area 7 is closed and untouched.
FR-SHE-121's "shared with HR training" becomes a later **read-only projection** of an employee's
SHE certificates into their area-7 record — never a write across the boundary. The trigger to
reopen: TDC asking for one editable training transcript per employee across both stores.

### 3.3 GNFS liaison (FR-SHE-084) — decided 2026-08-14

**No new entity.** The Ghana National Fire Service is a `SheRegulatoryBody` row (seeded, with
contact fields); statutory fire inspections/certifications are `SheRegulatoryObligation` rows with
`Domain = FireSafety` and `RegulatoryBodyId = GNFS`, evidenced by `SheRegulatoryComplianceEvidence`
rows (evidence date, expiry, document). Same shape for EPA and the Labour Commission. If TDC wants
richer liaison records, extend `SheRegulatoryBody`; don't fork a register.

### 3.4 SHE ↔ Employee Relations (area 9c)

Area 9c cross-links ER cases to SHE incidents (and to PIPs and disciplinary cases). The forward
link works; the reverse panel ("this incident has ER cases", `getCasesForSource`) has an API and a
client method but **no screen in SHE** — recorded in the HR-wide deferred list.

### 3.5 SHE ↔ Maintenance / Fleet — unlinked, by decision and by omission

- `SafetyEquipmentMaintenance.MaintenanceRecordId` is a loose `Guid?` with the comment "no FK to
  avoid cross-module coupling"; it is only ever copied into a DTO, never resolved. The field name
  overpromises.
- `SafetyIncident` and Maintenance's `FleetIncident` have no link in either direction: a vehicle
  crash is two audit trails correlated by a human noticing the date. Neither should "own" a crash;
  what is missing is an optional correlation key each way (integration map §10 item 6).
- `SafetyEquipment` is a third physical-item register beside HR's `CompanyAsset` and Inventory's
  `InventoryItem`.

### 3.6 SHE ↔ Inventory — a parallel stock system

`PpeInventory` (`QuantityInStock`, `MinimumStockLevel`, `ReorderLevel`, free-text `Supplier`,
`UnitCost`) re-solves what `InventoryItem` already solves, with no `InventoryItemId` anywhere in
SHE. The recommended ownership: Inventory owns stock and quantity; SHE keeps the *policy* layer
(`PpeType`, `JobRolePpeRequirement`, issuance and return) referencing Inventory's stock. Not built.

### 3.7 SHE ↔ Estate — points at HR's location model

`EmergencyPlan.LocationId` and `SheAssemblyPoint.LocationId` FK HR's `Location`, not Estate's
`EstateManagedAsset`. If Estate ever becomes authoritative for premises, SHE's emergency
preparedness must be re-pointed alongside HR's `MeetingRoom`.

### 3.8 SHE ↔ Procurement — `SheContractor` has no `Supplier` link

`SheContractor` (pre-qualification, compliance score, non-compliance records) models a commercial
counterparty with no `SupplierId`. HR's travel bookings already carry `VendorId → Supplier`; copy
that column. `SheContractorNonCompliance.SanctionApplied` has **no fine concept** (VerbalWarning /
WrittenWarning / WorkSuspension / PartialSuspension / ContractTermination) — if TDC expects a
monetary penalty, it is unbuilt.

### 3.9 SHE ↔ Projects — the project-gating seam (residue)

The permit-to-work / risk-assessment gate on project work (a project cannot start without an
approved RA / active permit) awaits Projects and DR-09. Recorded as area-10 residue; not built.

### 3.10 SHE ↔ Document management

`SheControlledDocument.DocumentRecordId → CentralDocumentRecord` is a real FK (the only SHE entity
FK into the DMS). Every SHE upload goes through `ISheControlledDocumentService` onto the
controlled-upload gate; the `hr-she-controlled-documents` category **cannot opt out of a clean
scan**, so without a scanner (`clamd-stub.mjs` in the harness folder) every upload 422s. Version
control is real (v1.0 → v1.1 → v1.2, byte-exact per-version downloads, FR-SHE-246).

---

## 4. What SHE already has that the catalogues under-credit

| Capability | Where | Note |
|---|---|---|
| Register reads with filters | `SafetyIncidentController` (12 filtered GETs + `number/{n}`), `SheAuditController`, `SheEnvironmentalReviewController`, hazard/RA/inspection/permit registers with quick views | Ahead of most HR areas |
| Report artefacts | `POST api/safety/audits/{id}/issue-report`; `GET api/safety/environmental/reviews/{id}/clearance-report`; **`SheMonthlyEnvironmentalReport`** auto-generated for the previous month by the sweep + manual `generate`/`submit`, two screens under `hr/safety/environmental/monthly-reports` | The most complete auto-generated report anywhere in HR |
| KPI computation | `SheKpiComputationService`; `ShePerformanceController` `kpis/departmental`, `kpis/contractor-ranking`, `kpis/hazard-heatmap`; `ShePerformanceSnapshot` figures are **reported, not computed** unless the KPI service is run (screens say so) | FR-SHE-230/232/248, FR-CON-001 |
| Statutory trail | `SheStatutoryIncidentSubmission` (FR-SHE-103); reportable-pending queue | |
| Permit approval gate | `ShePermitToWorkController` approve refuses until hazards + control measures are recorded, plus gas-test results for HotWork/ConfinedSpaceEntry, naming the missing sections (FR-PTW-002); close only from Active/Suspended; extensions Active-only with server-assigned sequence | |
| Incident lifecycle | Auto-population of default corrective actions from the incident type (FR-INC-004); closure gated on open CAs; reopen via review; lost-time roll-up | |
| Environmental core (Part D) | Permit & licence register (EPR numbering), statutory renewal ladder 180/90/60/30/14/7 with expiry flip + tier-2 escalation, monitoring schedules, regulatory-updates register, sustainability KPIs, the review lifecycle with clearance gates (screening → management approval + EPA submission → clearance, immutable trail) | FR-ENV-001…034 |
| Reminder engine | `SheReminderService` — **hourly** (the other seven HR sweeps are daily), publishes on `IAppEventBus` with `EnsureTopicsAsync`; covers document review due, permit/licence renewal rungs, monitoring due, regulatory deadlines, project-review rungs, and the monthly report generation | Delivers — it is one of the five engines that do |
| Controlled documents | Register with SHE-DOC numbering, FR-SHE-170 retrieval filters, real versioning, approval lifecycle | |

---

## 5. What SHE lacks (all confirmed 2026-09-02)

| Gap | Detail | Tracked in |
|---|---|---|
| **No `RowVersion` on any of the 88 entities** | Including approval-bearing `ShePermitToWork`, `SheRiskAssessment`, `SheEnvironmentalReview` — two officers editing the same permit overwrite each other silently | Patterns sweep §3.2 |
| **No workflow-engine adapter** | No `HrShe*WorkflowStatusAdapter`; permit, RA and environmental-review approvals are bare `ApprovedById`/`ApprovedDate` pairs with service-level state rules. Same fix as Medical/Benefits; SHE's statutory ladders are the strongest case for the engine's audit trail | Patterns sweep §3.3; `HR-WORKFLOW-ENGINE-INTEGRATION.md` |
| **No file import or export** | Zero CSV/XLSX in any SHE controller, service or screen; the one `window.print()` is on the environmental review detail. Registers exist; getting one into a file does not | Import/export §3.10; reports §3.17 |
| ~~**No SHE-officer role** (DR-10)~~ | **Delivered 2026-09-03** — Safety Officer + SHE Manager roles, HR read-only in SHE, `she.access` module gate | §2 |
| **No SMS channel** | Recorded residue of area 10 | — |
| **Money unposted** | Incident insurance claims (row 60), equipment/PPE cost (61); sustainability savings are reporting figures (62); contractor fines do not exist (63) | Finance sweep; backlog has a placeholder row only |
| **Duplicate registers** | PPE stock vs Inventory; safety equipment vs company assets vs inventory; incidents vs fleet incidents; contractor vs supplier | Integration map rows 9, 10, 14, 15 |
| **Heat-map rendering** (FR-SHE-232) | Data endpoint exists; visual deferred | — |
| **Project gating seam** | §3.9 | Area-10 residue |

---

## 6. Rules for anyone touching SHE

1. **Routes are `api/safety/*`.** If a document, a type file or a client says otherwise, the
   document is wrong.
2. **Occupational health and return-to-work are Medical-gated.** A new SHE endpoint touching exam
   results, restrictions or clearance gets the Medical policies, not `HR.She.*`.
3. **Employees' open actions stay open, actor from the token.** Do not desk-gate a lookup those
   forms feed on without opening the read.
4. **Bridge to Medical, Training, Maintenance, Inventory, Estate by reference.** Never write
   across the boundary; never merge stores.
5. **Any new many-include read gets `AsSplitQuery()`.**
6. **Any new number generator uses numeric max including soft-deleted rows** (the repo's soft
   delete is a manual `Where` in `GetQueryable()`, so `IgnoreQueryFilters()` is a no-op).
7. **Run the harness with `clamd-stub.mjs` alongside** for any slice that uploads.
8. **Seed the hourly sweep's topics before asserting a notification** — `EnsureTopicsAsync` is
   self-healing but runs on the sweep's schedule.
