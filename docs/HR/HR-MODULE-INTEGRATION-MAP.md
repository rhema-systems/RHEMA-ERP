# HR & SHE Module Integration Map — Every Other Module That Needs a Real Link

**Generated:** 2026-09-02
**Purpose:** Beyond Finance (already covered in this folder), a comprehensive sweep of every
other module in the system that HR and Safety/SHE's entities/functionality genuinely touch,
verdict on whether each integration is built-and-working, one-directional, planned-but-unbuilt,
or two unlinked systems modeling the same real-world thing.

---

## 0. Relationship to the other docs in this folder

| Document | How it relates |
|---|---|
| [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) | Covers the Finance integration in depth (money-carrying entities). Already flagged the "external payee modelling" gap (decision #7) for `TrainingVendor`/`MedicalInsuranceProvider`/`HealthcareFacility` — this document confirms that gap with hard evidence (no FK exists anywhere) and extends it to Procurement's `Supplier` and to SHE's `SheContractor`. |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md) | About **patterns** HR should copy from other modules. This document is about **live data links** HR needs with other modules — a different question (how HR should build things vs. what HR needs to talk to). |
| [`HR-IMPORT-EXPORT-CATALOGUE.md`](HR-IMPORT-EXPORT-CATALOGUE.md) | Flagged `CompanyAsset` bulk import as reusing Finance's pattern; this document adds the finding that a company vehicle may need to exist correctly in *three* places (HR, Maintenance/Fleet, Inventory) for that import to be meaningful. |
| [`HR-UAT-DEMO-PRESENTATION-PLAN.md`](HR-UAT-DEMO-PRESENTATION-PLAN.md) | The Assets and Safety/SHE demo segments should be walked through with this document's caveats in hand — several "it works in HR" moments have a "but it doesn't know about Maintenance/Fleet/Inventory's copy of the same thing" caveat attached. |

---

## ⚠ Vetting pass, 2026-09-02 — corrections and additions

Every row of §2 and every claim in §3–§9 was re-verified against the entities, the
`ApplicationDbContext` fluent configuration and the services. Corrections are applied inline;
this block is the audit trail. Two whole sections were added: **§12** (modules the first pass
omitted — Workflow, Identity reconciliation, Payroll, DMS, Finance fixed assets, Estate's duty
roster) and **§13** (the reverse direction: which other modules depend on HR master data).

| Where | Verdict | Corrected fact |
|---|---|---|
| Row 4 / §3 — "Technician skill data is duplicated, not shared … never resynced" | **Half wrong.** | `Maintenance/Technician.cs` **has** `[Required] Guid EmployeeId` with an `Employee` navigation and a `LastSyncDate` ("Last synchronization date with HR system"). It is a linked cache by design, not an orphan. The duplicated columns are real; whether any sync code actually runs was **not** verified — that is the open question, not the FK. |
| Row 5 — `Employee.Specialization/.CertificationLevel/…` | Confirmed — these six "Maintenance-specific properties" live on HR's `Employee` (`HREntities.cs:260-290`). | The duplication is HR→Maintenance, and both sides have the fields. |
| Row 8 / §3 — `StaffTravelGroundTransport.FleetTripId` "a real, working FK" | Nuance. | It is a **bare `Guid?`** — no `[ForeignKey]`, no navigation on the HR side. But HR *creates* the trip through `IFleetTripService.CreateTripAsync` (`StaffTravelBookingService.cs:473-496`), so the seam is stronger than "linked" and weaker than "FK". |
| §3 / §10 — "the planned HR→Maintenance push (`AssetAdmission`) still isn't built" | **WRONG — built.** | `AssetAdmission` is a Maintenance entity with its own service and controller; HR's `AssetsServices.cs:2270 SendForMaintenanceAsync` calls `CreateAdmissionAsync`, stores the id on `AssetMaintenance.MaintenanceAdmissionId` (deliberately non-FK), endpoint `POST api/assets/{id}/send-for-maintenance`. What is blocked is the **work-order/job-card** leg: cross-module defect #9 (`MaintenanceFollowThrough` throws because `MaintenanceType`/`PriorityLevel`/`WorkOrderType` have 0 rows on every tenant). |
| §4 / row 11 — "No `SupplierId` exists anywhere in HR or SHE" | **WRONG.** | Six travel entities carry `VendorId → Procurement.Supplier` with a `Supplier? Vendor` navigation and fluent config (Flight, Hotel, GroundTransport, CarRental, VisaApplication, InsurancePolicy). **HR already has the Procurement-owns-the-vendor pattern**; the four named entities (`TrainingVendor`, `HealthcareFacility`, `MedicalInsuranceProvider`, `SheContractor`) simply don't use it yet. The doc's own §10 item 4 is therefore a copy, not a design. |
| §4 — `HealthcareFacility` "the only one with bank details" | Wrong. | `MedicalInsuranceProvider` has the same four bank fields. `TrainingVendor` and `SheContractor` have none. |
| §6 — "`ProjectService.MaintenanceFollowThrough.cs` … this pass's search did not turn it up" | Resolved. | It exists, 432 lines, referenced by `ProjectsController`, `IProjectServices` and HR's `AssetsEntities.cs` comment. It is Projects→**Maintenance**, not Projects→HR. |
| Row 19 / §6 — `ProjectAssetLink.CompanyAssetId` "a real FK" | Nuance. | A bare `Guid?` with no `[ForeignKey]`/navigation (only `ProjectId` is configured). Also: `ProjectResourceAllocation.UserId` and `ProjectTimesheetEntry.UserId` key on **`ApplicationUser`**, not `Employee` — any reconciliation with HR goes through `ApplicationUser.EmployeeId`. |
| Row 21 / §7 — `EstateManagedAsset` "with `Building`/`FloorLabel`/`BlockName`" | One field wrong. | `BlockName` and `FloorLabel` exist; there is **no `Building`** field (free-text `Location`, `Region/District/Town`, `ProjectId` instead). Conclusion unchanged: no cross-reference to HR `Location`. |
| Rows 23–24 / §8 — EHC "bridge concept `HrIdentityWorkflowIssue` … confirmed not wired up" | **WRONG — misidentified.** | `HrIdentityWorkflowIssue` is an **Identity** entity (`Identity/HrIdentityReconciliationEntities.cs:122`; types: ManagerReplacementUnavailable, SegregationOfDutiesConflict, DepartmentOwnershipChanged, InactiveApprover) created by `HrIdentityReconciliationService.cs:933-965` and resolved through `HrIdentityReconciliationController`. It **is wired** and has **zero** relation to EHC. The correct EHC finding is simpler: no HR→EHC ticket creation exists anywhere, and `EhcTicket` reaches the employee only indirectly (`RequesterUserId` → user; `EhcTicketService.cs:549-558` defaults the department from the current user's employee). |
| Row 1 — "a dedicated reconciliation service" | Two, not one. | `EmployeeLinkResolutionService` (the exact-match user↔employee rules shared by login, LDAP auto-link and the unlinked-users queue) and `HrIdentityReconciliationService` (reconciles HR state changes into Identity/workflow). LDAP auto-link is `AuthController.cs:418 TryAutoLinkProvisionedLdapUserAsync` (AD mail → `Employee.EmailAddress`, else sAMAccountName → `EmployeeNumber`). Manual linking: `UserEmployeeLinkController` (link/unlink/bulk-link/unlinked-users). |
| §3 — "`docs/tdc-fleet-management-gap-implementation-tracker.md` … across HR, Maintenance, Inventory, Procurement, and Finance" | Confirmed; the row is wider. | INT-001 lists HR employees, licence data, Maintenance assets, work orders, Inventory parts, Procurement vendors, Fixed Assets, Workflow, Notifications, Reports. |
| Whole document — modules omitted | **Workflow engine, Identity reconciliation, Payroll, DocumentManagement, Finance FixedAssets custodian FKs, Estate duty roster** all have real touchpoints and were not mentioned. | Added as §12 and rows 28–36. |

---

## 1. Executive summary

**Nine other modules were checked in the first pass, and six more touchpoints were added on
2026-09-02. Two integrations are genuinely solid (Identity, and the HR→Maintenance admission
push). Everything else ranges from partial to a real duplicate-registry risk.**

| Module | Overall verdict |
|---|---|
| **Identity** (+ LDAP) | ✅ **Working, bidirectional.** `ApplicationUser.EmployeeId` ↔ `Employee`, with a dedicated reconciliation service and an auto-link-on-LDAP-provision flow. The one integration in this document that needs no remedial work. |
| **Maintenance** (incl. Fleet) | ⚠️ **Partial.** Asset linkage is built (read-only link + a working **send-for-maintenance push** that creates an `AssetAdmission`); the work-order leg is blocked by Maintenance's own empty lookup tables (defect #9). `Technician` is a linked cache (`EmployeeId` + `LastSyncDate`) whose sync has not been verified to run. Vehicles can exist as two unlinked records. A vehicle accident can produce two unlinked incident records (Fleet's and SHE's). Maintenance is also the heaviest *consumer* of HR master data in the system (~60 FKs to `Employee`, §13). |
| **Procurement** | 🟠 **Pattern exists, applied in one area only.** Six travel booking entities carry `VendorId → Supplier` (Procurement owns the vendor master). `TrainingVendor`, `HealthcareFacility`, `MedicalInsuranceProvider` and `SheContractor` do not use it. No hand-off from an HR asset requisition to Procurement's purchase pipeline. ⚠ Procurement's `SuppliersController` is itself dead (cross-module defect #1), so the travel FK cannot be exercised through the UI until that is fixed. |
| **Inventory** | 🔴 **No integration found at all.** SHE's PPE stock tracking and HR's/SHE's physical-item registers (`CompanyAsset`, `SafetyEquipment`) each duplicate what Inventory's `InventoryItem` already does, with zero FK connection. |
| **Projects** | ⚠️ **Partial, two real duplicate-tracking risks.** Consultant billing (HR) and project timesheets (Projects) are unlinked; percentage allocation (HR) and project staffing (Projects) are unlinked. Asset linkage is one-directional and working. Client/consultant modelling is confirmed cleanly separate from Projects' own customer concept — not a risk. |
| **Estate** | 🔴 **No integration found; the clearest duplicate-registry risk in this document.** HR's meeting-room/location model and Estate's building/facility register are two separate schemas for the same physical spaces. |
| **EHC** (Enquiry/Helpdesk/Complaints) | ⚠️ **Partial.** Tickets route by department (five EHC entities FK to HR `Department`), not by employee; the requester is a user id. No HR→EHC ticket creation exists anywhere. *(The "bridge concept" the first pass named was an Identity entity, not EHC — corrected.)* |
| **Workflow engine** *(added)* | ✅ **Heavy, working, with three platform defects HR inherits.** 15 HR adapter files; 7 HR entities carry `WorkflowInstanceId`; Medical, Benefits and SHE approvals are not on the engine. Defects #3 (conditional routing never routes), #14 (pending feeds die once non-empty), #15 (generic-surface approvals strand the record). See `HR-WORKFLOW-ENGINE-INTEGRATION.md`. |
| **Identity reconciliation** *(added)* | ✅ **Working.** `HrIdentityReconciliationState/Item/WorkflowIssue` carry `EmployeeId`/`DepartmentId`/`ManagerEmployeeId` with fluent FKs; HR org changes are reconciled into Identity and open workflow approvals. |
| **Payroll** *(added)* | ⚠️ **Another developer's module, integrated read-only through three HR-owned bridges** (salary structure, pay components, payroll membership) and one defect-driven fallback (#23). See `HR-PAYROLL-BOUNDARY.md`. |
| **Document management** *(added)* | ✅ **One FK.** `SheControlledDocument.DocumentRecordId → CentralDocumentRecord`; everything else reaches the DMS through `IHrControlledDocumentService`/`ISheControlledDocumentService` at service level. |
| **Finance fixed assets** *(added; Finance is otherwise out of scope here)* | ✅ FixedAssets depends on HR: `FixedAsset.CurrentCustodianId`, `AssetTransfer` custodians/requesters/approvers, `AssetDisposal`, `AssetVerificationSession.VerifiedById` all FK to `Employee`. |
| **Finance chart of accounts** *(added 2026-09-10, round 2 lane B2)* | ✅ **HR depends on Finance, read-only.** `OrganizationUnit.FinanceAccountId` and `Team.FinanceAccountId` FK to `Account` with **Restrict**; `AccountCode` / `CostCenterCode` are snapshots the HR service writes from the chosen account and deliberately never chases. HR reads the chart through its own narrow projection `api/hr/finance-accounts` (code, number, name, type, active — no balances), because `api/finance/accounts` is `ViewFinance`-gated and an HR user gets 403. Same shape and same reason as `HrCurrenciesController`. ⚠ Finance owes a delete guard for an account an HR unit references (round-2 plan § 7.2); until then the FK fails the delete loudly, which is the right failure. |
| **Sales** | ⚪ **Confirmed cleanly separate**, not obviously a gap — flag for TDC to confirm intent, not an engineering defect. |
| **Pricing, Quantity Survey** | ⚪ **No meaningful integration surface found.** Confirmed briefly, not worth further investigation unless a specific need arises. |

**The one theme running through every real gap in this document:** the same real-world thing —
a vehicle, a technician's certification, a piece of safety equipment, an external contractor, a
meeting room — is modelled independently in two or three modules with no FK between them. This
system has already solved this exact problem once, correctly: `CompanyAsset.FixedAssetId` (HR
reads, Finance owns capitalization/depreciation/disposal, decision D1). That pattern — one module
owns the record, others hold a read-only reference to it — is the template every gap below should
be resolved with, rather than each pair of modules inventing its own answer.

---

## 2. Master table

Legend — **Verdict**: ✅ built & working · 🟡 one-directional/read-only · 🟠 planned, not built ·
🔴 unlinked, duplicate-registry risk. **Priority**: same scale as the rest of this folder.

| # | Module | HR/SHE entity | Other-module entity | Verdict | Priority |
|---|---|---|---|---|---|
| 1 | Identity | `Employee` | `ApplicationUser.EmployeeId` | ✅ | — |
| 2 | Identity (LDAP) | `Employee` (email/number match) | LDAP-provisioned user | ✅ | — |
| 3 | Maintenance | `CompanyAsset.MaintenanceAssetId` (read-only link) **+ `AssetMaintenance.MaintenanceAdmissionId`** via `SendForMaintenanceAsync` | `MaintenanceAsset`, `AssetAdmission` | ✅ link + admission push built; work-order leg blocked by defect #9 | 🟡 (outside HR) |
| 4 | Maintenance | `Employee` (`WorkOrder`/`JobCard.AssignedTechnicianId` → HR `Employee`, fluent-configured) | `WorkOrder`/`JobCard` | ✅ targets the real employee | — |
| 5 | Maintenance | `Employee.Specialization`/`.CertificationLevel`/`.CurrentWorkload`/`.MaxWorkload` | `Technician` (has `EmployeeId` FK + `LastSyncDate`, duplicates name/contact/skill columns) | 🟡 linked cache; **sync not verified to run** | 🟡 |
| 6 | Maintenance (Fleet) | `CompanyAsset` (vehicle, `Source=HrCreated`) | `MaintenanceAsset` (`IsFleetAsset=true`) | 🔴 no auto-link | 🔴 |
| 7 | Maintenance (Fleet) | — | `FleetTrip.DriverEmployeeId` → `Employee` | ✅ | — |
| 8 | Maintenance (Fleet) | `StaffTravelGroundTransport.FleetTripId` (bare `Guid?`, no nav) — HR **creates** the trip via `IFleetTripService.CreateTripAsync` | `FleetTrip` | 🟡 service-level push, no FK, no conflict-detection | 🟡 |
| 9 | Maintenance (Fleet) vs SHE | `SafetyIncident` | `FleetIncident` | 🔴 unlinked | 🔴 |
| 10 | Maintenance vs SHE | `SafetyEquipmentMaintenance.MaintenanceRecordId` | Maintenance work order | 🔴 deliberately no FK | 🟡 |
| 11 | Procurement | `TrainingVendor`, `HealthcareFacility`, `MedicalInsuranceProvider`, `SheContractor` | `Supplier` | 🔴 unlinked — **but the pattern exists in HR**: six travel booking entities carry `VendorId → Supplier` (row 11b) | 🔴 |
| 11b | Procurement | `StaffTravel{Flight,Hotel,GroundTransport,CarRental}Booking.VendorId`, `StaffTravelVisaApplication.VendorId`, `StaffTravelInsurancePolicy.VendorId` | `Supplier` (Procurement owns the vendor master) | ✅ FK + nav + fluent config — **the template for row 11**; ⚠ unusable through the UI while Procurement's `SuppliersController` is dead (defect #1) | — |
| 12 | Procurement | `AssetRequisition` | `PurchaseRequisition` | 🔴 no hand-off | 🟡 |
| 13 | Procurement | HR policy-based auth | `IProcurementAccessControlService`/SoD guard | 🔴 not reused | 🟢 |
| 14 | Inventory | `PpeInventory` (SHE) | `InventoryItem` | 🔴 duplicated stock tracking | 🔴 |
| 15 | Inventory | `CompanyAsset`, `SafetyEquipment` | `InventoryItem` (serial/lot tracking) | 🔴 up to 3 unlinked registers for one physical item | 🔴 |
| 16 | Inventory | Training materials/consumables | `InventoryItem` | 🔴 no linkage | 🟢 |
| 17 | Projects | `ConsultantTimesheet`/`TimesheetInvoice` | `ProjectTimesheetEntry` (keyed on `UserId` → `ApplicationUser`, not `Employee`) | 🔴 two billing systems, no reconciliation; any join goes through `ApplicationUser.EmployeeId` | 🔴 |
| 18 | Projects | `TeamMember` (binary membership) | `ProjectResourceAllocation` (`UserId` → `ApplicationUser`) | 🔴 no cross-visibility of overallocation | 🟡 |
| 19 | Projects | `CompanyAsset` | `ProjectAssetLink.CompanyAssetId` (bare `Guid?`, no FK config) | 🟡 one-directional (Projects → HR), unenforced | 🟢 |
| 19b | Projects | HR `Department`, `Location` | `Project.DepartmentId`, `Project.LocationId` (bare, no nav, no config) | 🟡 HR-shaped ids with no referential integrity | 🟢 |
| 20 | Projects | `ConsultantClient`/`ClientEngagement` | Customer/BusinessPartner | ✅ confirmed cleanly separate | — |
| 21 | Estate | `MeetingRoom.LocationId` (→ HR `Location`) | `EstateManagedAsset` (`BlockName`, `FloorLabel`, free-text `Location`, `Region/District/Town`; no `Building` field) | 🔴 two unlinked physical-space registries | 🔴 |
| 22 | Estate vs SHE | `EmergencyPlan.LocationId`/`SheAssemblyPoint.LocationId` (→ HR `Location`) | Estate's building register | 🟡 works, but points at the "wrong" (HR's own, possibly duplicate) location model | 🟡 |
| 22b | Estate | `Employee` | `EstateFacilityDutyRoster.EmployeeProfileId` + `EmployeeNumber` + `StaffName` (bare, unvalidated, copied through by the controller) | 🟡 HR-shaped ids with no FK — a duty roster can name an employee who does not exist | 🟢 |
| 23 | EHC | `Department` | `EhcTicket.AssignedDepartmentId`, `EhcWorkflowRoutingRule`, `EhcCapaTask`, `EhcProblem.DepartmentId`, `EhcEscalationPolicy.DepartmentId` (nav + fluent) | ✅ department routing is a real FK; requester is `RequesterUserId` (user, not employee) — `EhcTicketService` defaults the department from the current user's employee | 🟡 no employee-level link |
| 24 | EHC | `EmployeeSeparation`/onboarding | Auto-ticket creation (revoke access, provision equipment) | 🔲 nothing exists (the "bridge concept" named earlier was `HrIdentityWorkflowIssue`, an Identity entity unrelated to EHC — corrected) | 🟡 |
| 25 | Sales | `ConsultantClient` | `Customer` | ⚪ confirmed separate, flag for TDC | 🟢 |
| 26 | Pricing | Training/benefit/PPE cost fields | Price lists | ⚪ no meaningful surface found | — |
| 27 | Quantity Survey | HR `Location` | QS rate-library entities `LocationId` (nav) | ✅ QS depends on HR locations (first pass said none) | — |
| 28 | Workflow engine | 7 HR entities with `WorkflowInstanceId`; 15 adapter families via the registry | `WorkflowInstance`, approvals, `WorkflowEntityDisplayService` deep-links | ✅ working; defects #3/#14/#15 inherited; Medical/Benefits/SHE not on it | 🔴 (#15 is severe) |
| 29 | Identity | `Employee`, `Department` | `HrIdentityReconciliationState/Item/WorkflowIssue` (`EmployeeId`, `DepartmentId`, `ManagerEmployeeId`, fluent FKs) | ✅ HR org changes reconciled into Identity/workflow approvals | — |
| 30 | Payroll (other dev) | `SalaryGrade` ← projection of `PayrollGrade`; `PayComponent` ← projection of payroll components; `Employee.IsOnPayroll` ↔ `PayrollEmployeeProfile.PayrollActive` | `PayrollGrade/Notch`, payroll components, `PayrollEmployeeProfile` | 🟡 three HR-pull bridges + defect #23 fallback; `PayrollEntities.cs:1900 JournalEntryId` → Finance | 🔴 (#23) |
| 31 | DocumentManagement | `SheControlledDocument.DocumentRecordId` | `CentralDocumentRecord` | ✅ | — |
| 32 | Finance FixedAssets | `Employee` | `FixedAsset.CurrentCustodianId`; `AssetTransfer.From/ToCustodianId/RequestedById/ApprovedById`; `AssetDisposal.RequestedById/ApprovedById`; `AssetVerificationSession.VerifiedById` (navs) | ✅ Finance depends on HR for custody | — |
| 33 | Inventory | HR `Department` | `InventoryAllocation`, `InventoryIssueVoucher` (+ `DepartmentName` string), `InventoryRequisition` `DepartmentId` (bare) | 🟡 HR-shaped ids, unenforced | 🟢 |
| 34 | Procurement | HR `Department` | `ProcurementPlan`, `ProcurementBudget`, `ProcurementSchedule`, `EmergencyProcurementPlan` `DepartmentId` (nav) | ✅ | — |
| 35 | Workflow | HR `Location`? | `WorkflowApprovalPolicySet.LocationId` (bare; target unverified) | ⚪ | — |
| 36 | Numbering / File uploads / Notifications | — | `INumberSequenceService` (10 HR services), `IFileStorageService` (76 refs) + `IControlledFileUploadService`, `IAppEventBus` topics | ✅ service-level, no entity FK | — |

---

## 3. Maintenance & Fleet — the module with the most surface area, and the most gaps

**What already works:** `CompanyAsset.MaintenanceAssetId` is a genuine, deliberate read-only link
(HR never writes capitalization/valuation/disposal — the same D1 pattern used for Finance).
`WorkOrder`/`JobCard.AssignedTechnicianId` correctly points at HR's real `Employee`, not a
shadow record. `FleetTrip.DriverEmployeeId` does the same. `StaffTravelGroundTransport.FleetTripId`
is a real, working FK for company-vehicle travel legs.

**What doesn't:**
- **Technician skill data is duplicated in a linked cache whose sync is unverified** *(corrected
  2026-09-02)*. HR's `Employee` carries `CanBeAssignedToMaintenance`, `Specialization`,
  `CertificationLevel`, `ExperienceLevel`, `CurrentWorkload`/`MaxWorkload` under a
  "Maintenance-specific properties" heading (`HREntities.cs:260-290`) — and Maintenance's
  `Technician` entity carries the **same fields again**, plus `FirstName`/`LastName`/`Email`/
  `Phone`/`Department`/`Position`. But `Technician` has a **required `EmployeeId` FK, an `Employee`
  navigation, and a `LastSyncDate`** ("Last synchronization date with HR system") — it was
  designed as a cache of the HR record. The open question is whether anything ever writes
  `LastSyncDate` (not verified). If nothing does, the drift risk is real; if something does, the
  fix is to make HR's copy the source and the cache read-only.
- **A company vehicle can exist as two unrelated records.** Fleet vehicles are modelled as
  `MaintenanceAsset` rows with `IsFleetAsset=true` — but an HR-created `CompanyAsset` representing
  the same car has no field or process that creates (or checks for) the matching
  `MaintenanceAsset`. Nothing stops the same vehicle being registered once in each place with two
  different asset numbers.
- **A vehicle accident can produce two disconnected incident records.** `FleetIncident` (vehicle
  damage, repair cost, insurance claim) and SHE's `SafetyIncident` (injury, investigation, root
  cause) have no FK between them. The same real event — a crash — is two audit trails with nothing
  correlating them beyond a human noticing the date and vehicle match.
- **`SafetyEquipmentMaintenance.MaintenanceRecordId` is deliberately unlinked** — the code comment
  says so explicitly ("no FK to avoid cross-module coupling"). This may be the right call for now,
  but it means SHE's equipment-maintenance history is invisible to Maintenance's own work-order
  system, and "MaintenanceRecordId" is a slightly misleading field name for something that points
  nowhere real yet.
- **The HR→Maintenance push IS built, on the admission leg** *(corrected 2026-09-02)*.
  `POST api/assets/{id}/send-for-maintenance` → `AssetsServices.SendForMaintenanceAsync` →
  Maintenance's `IAssetAdmissionService.CreateAdmissionAsync`; the admission id is stored on
  `AssetMaintenance.MaintenanceAdmissionId` (deliberately non-FK, per the entity comment). The
  service chose admission precisely because the **work-order** leg is blocked: cross-module
  defect #9 — `ProjectService.MaintenanceFollowThrough.cs` resolves `MaintenanceType`,
  `PriorityLevel` and `WorkOrderType` by name and throws when none exists; the reference database
  has 0 rows in all three, so Projects' own follow-through has never succeeded either. Fix is
  Maintenance's (seed the lookups: "Corrective", "Medium", "High", "Standard"). Note also that
  `CreateAdmissionAsync` will admit the same asset twice without complaint (`AssetsServices.cs:2289`).

### Fleet-specific document already exists
`docs/tdc-fleet-management-gap-implementation-tracker.md` already states plainly: *"the full
Fleet interface contract is not documented or acceptance-tested"* across HR, Maintenance,
Inventory, Procurement, and Finance. This document's findings are consistent with, not
contradicting, that tracker — read it alongside this section rather than instead of it.

---

## 4. Procurement — the pattern exists in HR travel and is applied nowhere else (corrected 2026-09-02)

The first pass said no `SupplierId` exists anywhere in HR or SHE. That is wrong: **six travel
entities carry `VendorId // FK -> Supplier (Procurement owns the vendor master)` with a
`Supplier? Vendor` navigation and fluent configuration** — `StaffTravelFlightBooking`,
`StaffTravelHotelBooking`, `StaffTravelGroundTransport`, `StaffTravelCarRentalBooking`,
`StaffTravelVisaApplication`, `StaffTravelInsurancePolicy`. Area 12 retired travel's own vendor
master onto Procurement's in 2026-08 — exactly the ownership move §10 recommends. (⚠ It cannot be
exercised through the UI yet: Procurement's `SuppliersController` is entirely non-functional,
cross-module defect #1, so the travel harness seeds a supplier in SQL.)

Four other entities each still independently model an external party the organisation pays or
is paid through — `TrainingVendor`, `HealthcareFacility` and `MedicalInsuranceProvider` (**both**
of the medical ones carry `BankId`/`BranchId`/`AccountNumber`/`AccountName`; the first pass said
only the facility did), and SHE's `SheContractor` — and none of them link to `Supplier` (which
already has payment terms, credit limits, and a rating). If the same organisation is engaged for
both training delivery and, say, office supplies, there is no way for this system to know it's
the same legal entity in two places. The fix is a copy of the travel column, not a design.
Free-text `Supplier` strings also sit on `CompanyAsset` and SHE's `PpeInventory`.

**Also confirmed missing:** a hand-off from HR's `AssetRequisition` into Procurement's
`PurchaseRequisition` when internal stock can't fulfil a request — today, an unfulfillable asset
requisition is simply an HR record with no path to actually buying the thing. And HR does not
reuse Procurement's segregation-of-duties/access-control services (`IProcurementAccessControlService`,
`IProcurementSodGuardService`) — HR relies on simpler policy-based authorization throughout,
which is a smaller gap (lower priority) than the vendor and requisition ones above.

---

## 5. Inventory — SHE and HR each run a parallel stock system

`PpeInventory` (SHE) tracks `QuantityInStock`/`ReorderLevel`/a free-text `Supplier` string — this
is, field-for-field, the same problem Inventory's `InventoryItem` already solves
(`CurrentStock`/`AvailableStock`/`ReorderLevel`), with zero connection between them. The same
applies to physical-item registers more broadly: a company laptop could plausibly be tracked as
an HR `CompanyAsset`, an Inventory `InventoryItem` (with real serial/lot tracking), **and** a SHE
`SafetyEquipment` record if it has any safety relevance — three registers, no reconciliation,
each one someone's "source of truth" until the day it isn't.

---

## 6. Projects — two real reconciliation gaps, one confirmed non-issue

- **Consultant billing vs. project timesheets.** HR's `ConsultantTimesheet`/`TimesheetInvoice`
  (external client billing) and Projects' own `ProjectTimesheetEntry` (internal project work,
  hours × rate, billable flag) are unrelated systems. The same consultant's time could be recorded
  in both with nothing reconciling the two totals.
- **Resource allocation.** HR's `TeamMember` percentage allocation and Projects'
  `ProjectResourceAllocation` (with proper start/end dates and substitution tracking, already
  flagged in `HR-CROSS-MODULE-PATTERNS-SWEEP.md` §7.3 as the more mature model) don't share data,
  so nothing can detect an employee over-allocated across a project and an HR-side assignment at
  the same time.
- **Asset linkage exists, one-directionally and unenforced** *(corrected 2026-09-02)*.
  `ProjectAssetLink.CompanyAssetId` is a bare `Guid?` — no `[ForeignKey]`, no navigation, no
  fluent config (only `ProjectId` is configured). `ProjectService.MaintenanceFollowThrough.cs`
  **does exist** (432 lines) and is Projects→Maintenance, not Projects→HR; it is also the file
  that throws for every tenant (defect #9). Also: `ProjectResourceAllocation.UserId` and
  `ProjectTimesheetEntry.UserId` key on **`ApplicationUser`**, not `Employee`, so any
  reconciliation with HR's consultant timesheets or team membership must join through
  `ApplicationUser.EmployeeId`. `Project.DepartmentId`/`.LocationId` are likewise bare ids.
- **Confirmed fine as-is:** HR's `ConsultantClient`/`ClientEngagement` model has no overlap with
  Projects' own customer concept — a genuinely clean separation, not a gap.

---

## 7. Estate — the clearest duplicate-registry risk in this whole document

HR's `MeetingRoom` entity (used for company-event/meeting bookings, linked to HR's own `Location`
hierarchy) and Estate's building/facility register (`EstateManagedAsset`, with `BlockName`/
`FloorLabel`, a free-text `Location`, `Region`/`District`/`Town` and a `ProjectId` — no `Building`
field, corrected 2026-09-02) are two entirely separate schemas describing the same physical
spaces, with zero cross-reference. Estate does reach toward HR in one other place:
`EstateFacilityDutyRoster` carries `EmployeeProfileId`, `EmployeeNumber` and `StaffName` as bare,
unvalidated columns the controller copies straight through — a roster can name a non-existent
employee. Facilities management (Estate) has no visibility into what HR
has booked, and HR's booking system has no visibility into facility maintenance/availability
Estate might already know about. **This is worth prioritising precisely because it's the easiest
kind of gap to explain to a non-technical stakeholder** ("if Estate closes a room for repairs, HR
can still book it") and because SHE's emergency-preparedness records (assembly points, evacuation
plans) currently point at HR's location model rather than Estate's — meaning if Estate is ever
made authoritative for premises data, SHE's emergency plans need to be re-pointed too, not just
HR's meeting rooms.

---

## 8. EHC (Enquiry, Helpdesk & Complaints) — a natural HR-lifecycle hook that isn't wired

EHC tickets route to a `Department` (a real FK on `EhcTicket.AssignedDepartmentId`,
`EhcWorkflowRoutingRule`, `EhcCapaTask`, `EhcProblem` and `EhcEscalationPolicy`), and the
requester is `RequesterUserId` — an `ApplicationUser`, not an `Employee`. `EhcTicketService.cs:549-558`
defaults a ticket's department from the current user's linked employee, so EHC *does* read HR,
one hop away. But there is no `EmployeeId` on a ticket, so there's no way today to see "this
employee has raised 3 complaints" from HR's side.

*(Corrected 2026-09-02.)* The first pass named `HrIdentityWorkflowIssue` as an un-wired HR→EHC
bridge. It is nothing of the kind: it is an **Identity** entity
(`Identity/HrIdentityReconciliationEntities.cs:122`, types ManagerReplacementUnavailable /
SegregationOfDutiesConflict / DepartmentOwnershipChanged / InactiveApprover) that
`HrIdentityReconciliationService` creates when an HR org change leaves a workflow approval with a
stale assignee, and it **is** wired and resolved through its own controller. It has no relation to
EHC. The accurate EHC finding is simpler and starker: **no HR→EHC ticket creation exists anywhere**
— no `EhcTicket` reference in any HR service, no Separation/Onboarding reference in any EHC
service. The obvious use case (auto-raise an IT ticket to revoke access when an
`EmployeeSeparation` completes, or to provision equipment when onboarding starts) would be new
code, and the natural place to raise it is the same `IAppEventBus` event the separation reminder
sweep should already be publishing (see the patterns sweep §3.1).

---

## 9. Sales, Pricing, Quantity Survey — checked, no significant finding

- **Sales:** HR's `ConsultantClient` is a fully separate model from Sales' `Customer`. Not
  necessarily wrong — a training/staffing client and a sales customer may legitimately be
  different things — but worth a one-line confirmation from TDC on whether they should ever be
  the same external party, rather than assuming either way.
- **Pricing:** no meaningful integration surface found with HR/SHE cost fields (training cost,
  benefit valuation, PPE unit cost). Not worth further investigation unless a specific need
  surfaces later.
- **Quantity Survey:** no employee/surveyor-assignment modelling found — but QS's rate-library
  entities do FK to HR `Location` (`QuantitySurveyRateLibraryEntities.cs:93/133`), so QS is a
  consumer of HR location master data (corrected 2026-09-02).

---

## 10. The one pattern to apply everywhere in this document

`CompanyAsset.FixedAssetId` → Finance's `FixedAsset` is the one link in this whole sweep that is
unambiguously done right: one module (Finance) owns the authoritative record and its lifecycle
(capitalization, depreciation, disposal); the other module (HR) holds a nullable FK, reads
through it, and is explicitly refused write access to the fields it doesn't own. Every 🔴 finding
in this document is a version of the same underlying question — **which module owns this record,
and does everyone else just hold a reference to it** — and none of them need a novel solution,
just the discipline to apply the one this system has already proven works:

1. **Vehicles** → probably Maintenance/Fleet owns the vehicle record; HR's `CompanyAsset` becomes
   a reference the same way it already is for Finance's fixed assets.
2. **Technician skills** → probably HR's `Employee` owns qualifications/certifications (it's HR
   data); Maintenance's `Technician` becomes a thin reference, not a parallel profile.
3. **PPE and safety equipment stock** → probably Inventory owns physical stock/quantity; SHE's PPE
   entities become the *policy* layer (who must have what, when it expires) referencing
   Inventory's stock records rather than re-counting them.
4. **External parties (training vendors, medical providers, contractors)** → Procurement's
   `Supplier` — **already decided and built for travel** (six `VendorId → Supplier` columns, area
   12). Copy that column onto `TrainingVendor`, `HealthcareFacility`, `MedicalInsuranceProvider`
   and `SheContractor`; they keep their domain-specific fields (accreditation, pre-qualification)
   but reference the shared party rather than re-declaring bank details independently. Blocked in
   practice on defect #1 (dead `SuppliersController`).
5. **Meeting rooms / physical spaces** → probably Estate owns the premises register once it
   exists in enough detail; HR's `MeetingRoom` becomes a thin booking-calendar layer over it.
6. **Vehicle/safety incidents** → this one is different in kind: neither module should "own" a
   crash. What's missing here isn't ownership, it's a shared correlation key (an optional FK each
   way) so the two records can be found together rather than left to a human to notice.

---

## 11. Suggested priority order

1. **Settle the ownership question for each 🔴 item in §10** — this is a design decision each
   module owner needs to make once, not an engineering task to start blind.
2. **External-party consolidation (Procurement)** — highest count of duplicated entities (four)
   pointing at the same underlying gap; also the one already partly anticipated by the Finance
   sweep's decision #7, so it isn't a new conversation to start.
3. **Vehicle/incident correlation (Maintenance/Fleet ↔ SHE)** — safety and insurance consequences
   make this worth prioritising over the lower-stakes duplications.
4. **Meeting-room/premises consolidation (Estate)** — the cheapest to explain and likely cheapest
   to fix once Estate's register is confirmed as the intended long-term owner.
5. **PPE/asset stock consolidation (Inventory)** — larger scope (three overlapping registers), so
   sequence after the smaller wins above establish the pattern.
6. **Technician skill consolidation (Maintenance)**, **consultant-timesheet reconciliation
   (Projects)**, and **EHC employee-linking** — real but lower urgency than the above.
7. **Confirm with TDC**: the Sales/ConsultantClient question. (The `MaintenanceFollowThrough`
   discrepancy is resolved — the file exists and is the subject of defect #9.)
8. **Keep three cross-module defects on the finalization list because they gate rows above:**
   #1 (dead `SuppliersController` gates every Supplier FK), #9 (empty Maintenance lookups gate the
   work-order leg of row 3), #15 (generic-surface approvals strand any HR record approved from
   the workflow inbox rather than the module's own endpoint).

---

## 12. Modules the first pass omitted (added 2026-09-02)

| Module | Touchpoint with HR/SHE | Verdict |
|---|---|---|
| **Workflow engine** | 7 HR entities carry `WorkflowInstanceId` (three Leave entities, `JobVacancy`, `EmployeeSeparation`, `StaffRequisition`, `TrainingNomination`); 15 `Hr*WorkflowStatusAdapters.cs` families are wired through the adapter registry; HR code holds 54 references to `IWorkflowIntegrationService` and 50 to `IWorkflowStatusAdapterRegistry`. `HrIdentityWorkflowIssue` bridges HR org changes to stale workflow approvals. Medical, Benefits and SHE approvals are **not** on the engine. | ✅ working; inherits defects #3/#14/#15. Recipe and traps: `HR-WORKFLOW-ENGINE-INTEGRATION.md` |
| **Identity reconciliation** | `HrIdentityReconciliationState` (`EmployeeId`, `DepartmentId`, `ManagerEmployeeId`), `HrIdentityReconciliationItem` (previous/current department and manager), `HrIdentityWorkflowIssue` (`EmployeeId`) — fluent FKs at `ApplicationDbContext.cs:8536-8585`; `UserEmployeeLinkController` for manual linking. | ✅ working, beyond the `ApplicationUser.EmployeeId` link in row 1 |
| **Payroll** (another developer's) | `SalaryGrade` is referenced by Benefit, Employee, JobArchitecture, PromotionTransfer and Salary entities and is itself a one-way projection of `PayrollGrade`; `PayComponent` is a partial projection of payroll components; `Employee.IsOnPayroll` is HR's statement beside `PayrollEmployeeProfile.PayrollActive`; `PayrollEntities.cs:1900 JournalEntryId` is payroll's own link to Finance. | 🟡 three HR-pull bridges + the defect #23 fallback. `HR-PAYROLL-BOUNDARY.md` |
| **DocumentManagement** | `SheControlledDocument.DocumentRecordId → CentralDocumentRecord`; everything else via `IHrControlledDocumentService` (35 controllers) / `ISheControlledDocumentService` (28). | ✅ |
| **Finance FixedAssets** | `FixedAsset.CurrentCustodianId`, `AssetTransfer` custodians/requester/approver, `AssetDisposal.RequestedById/ApprovedById`, `AssetVerificationSession.VerifiedById` → `Employee` (navs). | ✅ Finance depends on HR for custody — the reverse of row 36 in the Finance sweep |
| **Estate** (duty roster) | `EstateFacilityDutyRoster.EmployeeProfileId/.EmployeeNumber/.StaffName` — bare, no config, no validation. | 🟡 unenforced |
| **Numbering / file uploads / notifications** | `INumberSequenceService` (10 HR services), `IFileStorageService` (76 refs) + `IControlledFileUploadService`, `IAppEventBus` topics (five reminder engines). | ✅ service-level |
| Legal, Planning, Procedures, CRM, Audit governance, Tenant, Reports, Security, Email campaigns | Checked: user-id references only (`OpenedById`, `SalesRepId`), no `EmployeeId`/`DepartmentId`/HR `LocationId`. | ⚪ no touchpoint |

Unresolved: `Employee.EstablishmentSourceBudgetId` (`HREntities.cs:892`, bare Guid) — target not
confirmed (presumably `ManpowerBudget`).

## 13. The reverse direction — who depends on HR master data (added 2026-09-02)

The first pass mapped what HR needs from other modules. This is what other modules already take
from HR, which is the list of things that break if HR's `Employee`, `Department` or `Location`
tables change shape. "nav" = declared navigation/attribute FK; "fluent" = configured in
`ApplicationDbContext`; "bare" = a Guid column with no navigation or configuration found.

| Module | Entity → HR field | Target | Kind |
|---|---|---|---|
| Identity | `ApplicationUser.EmployeeId` | Employee | nav |
| Identity | `HrIdentityReconciliationState/Item/WorkflowIssue` (employee, department, manager ids) | Employee / Department | fluent |
| EHC | `EhcTicket`, `EhcWorkflowRoutingRule`, `EhcCapaTask` `.AssignedDepartmentId`; `EhcProblem`, `EhcEscalationPolicy` `.DepartmentId` | Department | nav + fluent |
| Finance FixedAssets | `FixedAsset.CurrentCustodianId`; `AssetTransfer` (4 ids); `AssetDisposal` (2); `AssetVerificationSession.VerifiedById` | Employee | nav |
| Maintenance | `Technician.EmployeeId`, `UserTechnicianSkill.EmployeeId`, `MaintenanceAsset.EmployeeId` (custodian) + `.CurrentSiteLocationId` (**HR `Location`**), `WorkOrder.AssignedTechnicianId`, `WorkOrderTask/Labor/Document`, `JobCard` (5 ids), `JobCardComment/Document/ApprovalStep/Certificate`, `AssetTaskTemplate`, `AssetTypeTaskTemplate`, `MaintenanceTaskTemplate`, `MaintenanceVehicle`, `MaintenanceSchedule` (nav + one bare), `AssetInspection.InspectorId`, `TechnicianTeam/Member`, `SafetyComplianceRecord` (3), `TechnicianSkillAssignment`, `TechnicianCertification`, `MaintenanceAttachment/Access` (typed `Employee` despite "UserId" names), QC: `InspectionApproval`, `WorkOrderQualitySignOff`, `QualitySignOffChecklist`, `WorkOrderRejection` (5), `RejectionFollowUp`; Resources: `ToolCheckout`, `MaintenanceExpense`; Contractor: `ContractorInvoiceApproval`, `ContractorLogisticsExpense`; Fleet: `FleetTrip`, `FleetIncident`, `FleetVehicleAssignment`, `FleetTripInspection`, `FleetDefect` | Employee (≈60 FKs), Location (1) | nav (+ fluent on WorkOrder/JobCard) |
| Procurement | `ProcurementPlan`, `ProcurementBudget`, `ProcurementSchedule`, `EmergencyProcurementPlan` `.DepartmentId` | Department | nav (+ fluent on Plan) |
| Quantity Survey | rate-library entities `.LocationId` | HR Location | nav |
| Projects | `Project.DepartmentId`, `.LocationId`; `ProjectAssetLink.CompanyAssetId` | Department / Location / CompanyAsset | **bare** |
| Projects | `ProjectResourceAllocation.UserId`, `ProjectTimesheetEntry.UserId` | ApplicationUser (not Employee) | — |
| Inventory | `InventoryAllocation`, `InventoryIssueVoucher`, `InventoryRequisition` `.DepartmentId` | Department (presumably) | **bare** |
| Estate | `EstateFacilityDutyRoster.EmployeeProfileId/.EmployeeNumber` | Employee (presumably) | **bare** |
| Workflow | `WorkflowApprovalPolicySet.LocationId` | unverified | bare |
| Finance, Inventory, Sales, Procurement `LocationId`s | — | **`WarehouseLocation`/`InventoryLocation`, not HR** | — |

**Net:** Maintenance, Identity, EHC, Finance fixed assets, Procurement planning and QS depend on HR
master data through real FKs; Projects, Inventory, Workflow and Estate carry HR-shaped ids as
unenforced Guids. Two consequences: (a) HR must never hard-delete an `Employee`, `Department` or
`Location` row (soft delete is already the convention, and the unfiltered unique indexes on
staff numbers are why), and (b) the four "bare" modules are where an HR id can dangle silently —
worth a periodic orphan check rather than an FK migration into someone else's module.
