# TDC Procurement and Inventory Implementation Handover

Last updated: 2026-07-23

Document status: `TDC-0206` complete; `TDC-0207` not started

## Handover Objective

This document transfers the TDC Procurement and Inventory implementation from completed requirement and gap analysis into controlled delivery. The next agent should be able to open this repository, verify the live state, and continue the next tracker slice without repeating the analysis or inventing a parallel architecture.

The governing delivery ledger is `docs/tdc-procurement-inventory-gap-implementation-tracker.md`. It currently contains 93 roadmap tasks across phases 0 through 9 and 27 mandatory end-to-end acceptance scenarios. Phase 0 (`TDC-0001` through `TDC-0007`), Phase 1 (`TDC-0101` through `TDC-0108`), and Phase-2 slices `TDC-0201` through `TDC-0206` are verified `Done`; later roadmap tasks and end-to-end scenarios retain the status recorded in the tracker and must change only as verified implementation work progresses.

## Immediate Starting Decision

Start with `TDC-0207` only: add reusable controlled tender-document templates and an immutable issuance/addenda/extension register, reusing the delivered sourcing-case, RFQ, NCT/ICT, exceptional-sourcing, prequalification, workflow, evidence, identity, notification, SOD, and audit architecture.

Phase 1 is complete. `TDC-0201` derives one tenant-safe sourcing case from a current immutable PR sourcing release and enforces that current case at RFQ/tender entry. `TDC-0202` makes the method recommendation server-derived from the exact current policy and protects any override with eligible exception, capability, independent workflow, evidence, actors, and SOD. `TDC-0203` completes the RFQ vertical flow. `TDC-0204` completes NCT/ICT from approved advertisement and controlled paid/free document issue through sealed/late submissions, signed public opening, separate technical/financial evaluation, exact authority/PPA workflow, approved award, executed-contract reference, bidder acceptance, immutable SQL records, and `DEC-001` through `DEC-014` control-event lineage. `TDC-0205` completes Restricted Tendering and Single Source from exact method/exception/evidence/supplier lineage through Board/MD/PPA approval, negotiation, recommendation, award, contract/acceptance, PPA filing, exception reporting, immutable SQL protection, and all fourteen decision events. `TDC-0206` completes tenant-safe prequalification from exact current policy/configuration and workflow lineage through advertisement, controlled supplier evidence, independent criterion evaluation, exact decision, reusable qualified-list validity/expiry, deterministic category eligibility, SQL protection, and all fourteen decision events. Complete and expired history remains readable; final acceptance preserved `TDC-0207` and later statutory workflows plus general PO, receiving, and inventory runtime behavior.

Do not begin `TDC-0208` or later committee/GHANEPS expansion, broadly redesign PO/receiving/inventory behavior, or recreate the delivered configuration, compliance, sourcing-release, sourcing-case, RFQ/NCT/ICT/exceptional-sourcing/prequalification, workflow, evidence, identity, notification, SOD, supplier-validation, or control-event architecture. `TDC-0207` must track reusable document templates, exact approved version, issued recipient, paid/free fee and receipt, immutable issue history, addendum acknowledgement, deadline extension, and bid-validity extension required by the tracker. Every hard stop must be server-enforced and tenant safe; the UI must explain readiness and retain history. The slice must pass every applicable tracker Delivery Contract gate before it can be marked `Done`.

## Authoritative Inputs And Precedence

Use the following sources in this order:

1. `C:\Users\micha\Downloads\Requirement Documents\TDC SRS Procurement ERP.docx` is the minimum client contractual baseline.
2. `C:\Users\micha\Downloads\Questionnairs and SOPS\Questionnairs and SOPS\Procurement\Procurement ERP.docx` supplies additional operating detail.
3. `docs/tdc-procurement-inventory-gap-implementation-tracker.md` is the live implementation ledger and requirement traceability record.
4. `docs/procurement-planning-tasklist.md` records procurement-planning capabilities already delivered. Do not recreate or reopen those capabilities unless current code verification proves a regression or a tracker gap.
5. `docs/enterprise-workflow-module-integration-guide.md` governs reuse of the shared workflow platform.
6. The live source code and database migrations decide what currently exists. Reverify code before changing a tracker baseline classification.

If the SRS and questionnaire differ, preserve the SRS requirement and record the questionnaire detail as configuration or an extension. Do not silently remove statutory or client requirements.

## Verified Repository Snapshot

This snapshot was refreshed on 2026-07-23 after `TDC-0206` acceptance and must be reverified at the beginning of the next slice.

| Item | Verified state |
| --- | --- |
| Repository root | `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2` |
| Branch | `agent/tdc-procurement-phase-0-controls`, tracking its upstream with zero divergence at the `TDC-0206` kickoff |
| Baseline commit | `41d6e4fff98856150f8ddb0699eeb5d9825dea43` (pushed accepted Phase-0/Phase-1/`TDC-0201` through `TDC-0203` checkpoint before uncommitted `TDC-0204` through `TDC-0206`) |
| .NET SDK | `8.0.206` from `global.json` |
| Frontend | Next.js `15.5.3`, React `19.1.0`, TypeScript 5, React Query 5, Vitest 3, Lucide icons, shared Radix/shadcn controls |
| Backend build | Core, Data, and API builds pass with zero errors and only existing warnings |
| Focused Core tests | Full procurement filter passes 201/201; focused prequalification controls pass 8/8 |
| Focused API tests | Full discovered procurement controller suite passes 117/117, including seven prequalification controller cases |
| Targeted frontend verification | Twenty-three test files pass 92/92 Vitest tests; the focused prequalification helper passes 5/5 and all changed prequalification files pass targeted ESLint |
| Full frontend type-check | Retains 108 documented unrelated baseline diagnostics; none matches a changed `TDC-0206` prequalification file and no new procurement work may add errors |
| Test database | `RhemaERP` is current through `20260723161619_AddProcurementPrequalificationPolicyLineage`; the five prequalification tables, 32 named indexes, 17 restrictive foreign keys, six trusted checks, six enabled lifecycle/immutability triggers, and all legacy sourcing/RFQ/tender guards are enabled, and EF reports no pending model changes |

### Dirty Worktree Protection

The worktree is not clean. The accepted Phase-0, Phase-1, and `TDC-0201` through `TDC-0203` baseline is pushed at `41d6e4fff98856150f8ddb0699eeb5d9825dea43`. At this handover point the tree contains the completed, uncommitted `TDC-0204` through `TDC-0206` implementations and evidence updates, plus team-owned changes that predated the slices:

- Modified active procurement tracker and handover documents, including the supplier-onboarding token clarification.
- Untracked Fleet, Sales/CRM, Quantity Survey, and Civil Engineering tracker documents under `docs`.
- Untracked `src/ErpSystem.Api/full_database.sql`.
- The focused `TDC-0204` through `TDC-0206` backend, API, migration, frontend, test, and smoke-evidence files listed in the procurement tracker's Current Code Evidence Anchors and Verification Log.

Treat these as user/team changes. Do not reset, revert, delete, stash, commit, or reformat them merely to obtain a clean tree. Before editing a file that is already modified, inspect its current diff and preserve the existing change. Keep procurement implementation commits narrowly scoped and never add `full_database.sql` unless the user explicitly requests it.

## Delivery Contract

A tracker task is `Done` only when all applicable parts of the tracker Delivery Rule are present and verified:

- Backend model and enforced business rules.
- Database migration and required backfill or tenant seeding.
- Tenant-safe API endpoints and authorization.
- Frontend route, controls, validation, loading/empty/error states, and status visibility.
- Shared workflow, evidence, notification, audit, and reporting behavior where required.
- Happy-path, hard-stop, authorization, tenant-isolation, concurrency, and direct-API tests.
- Migration applied to the test database and schema checked.
- Browser/API smoke verification.
- Tracker status, coverage matrix, and Verification Log updated with real evidence.

Do not mark a task complete because an entity, field, endpoint, or screen exists. Do not leave backend-only or UI-only slices described as complete.

## Engineering Guardrails

- Reuse existing module boundaries, repositories, `IUnitOfWork`, `ICurrentUserProvider`, tenant entities, service registration, API response conventions, shared controls, and workflow services.
- Keep the existing `ProcurementSettings` behavior backward compatible. Do not convert that mutable singleton directly into the new policy/versioning model in the first slice.
- Use typed DTOs and `System.Text.Json` serialization/validation for configurable decision payloads. Do not parse policy values with string splitting or ad hoc JSON traversal.
- Keep executable policy rules relational in `TDC-0002`; the `TDC-0001` decision register may store typed, schema-versioned decision snapshots and approval evidence without becoming the runtime rules engine.
- Enforce tenant isolation in queries and unique indexes. Never trust a tenant ID from the request body.
- Published and retired configuration versions are immutable. Changes are made by cloning a new draft.
- Use row-version concurrency for profile and editable-decision updates.
- Use the shared file-storage, evidence-validation, workflow, notification, and audit infrastructure. Do not create a second blob store, workflow engine, approval dialog, or checklist implementation.
- Never use JavaScript `prompt`, `confirm`, or `alert`. Use shared dialogs and audited confirmation actions.
- Prefer dedicated, history-first administration routes over adding more sections to the already large Purchase Order Settings page.
- Keep UI styling consistent with the current application: compact operational layouts, Lucide icons, responsive tables, proper select controls, toggles for booleans, and no nested cards.
- Every transaction hard stop must be enforced in the service/API layer and covered by direct-API tests. Hiding a button is not enforcement.
- Do not mix unrelated refactoring into a procurement slice.

## Existing Architecture To Reuse

### Backend Anchors

| Concern | Existing code anchor and instruction |
| --- | --- |
| Tenant/audit base | `src/ErpSystem.Core/Entities/BaseEntity.cs`; new persisted procurement configuration records derive from `TenantEntity`. |
| Current settings | `src/ErpSystem.Core/Entities/Procurement/ProcurementSettings.cs`, DTOs, service, repository, controller, and frontend service. Preserve this compatibility surface. |
| DI registration | `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`; repository registrations are grouped near line 275 and services near line 803. |
| EF context | `src/ErpSystem.Data/ApplicationDbContext.cs`; add DbSets and explicit model configuration/indexes consistently. |
| Version lifecycle | `WorkflowDefinition`, `WorkflowApprovalPolicySet`, and `WorkflowDefinitionServiceAdapter` demonstrate Draft/Published/Retired, clone, publish, retirement, and immutable history. Reuse the behavior, not workflow-specific entity coupling. |
| Lifecycle tests | `tests/ErpSystem.Core.Tests/Services/Workflow/WorkflowDefinitionLifecyclePolicyTests.cs` is the focused policy-test pattern. |
| Concurrency | Finance fixed-asset lease/capital-project entities demonstrate `[Timestamp] byte[] RowVersion`. |
| API tests | `tests/ErpSystem.Api.Tests/Controllers/Procurement/BusinessPartnersControllerContactRouteTests.cs` demonstrates the current `WebApplicationFactory` procurement route pattern. Extend it with real authorization-negative coverage instead of only permissive test handlers. |
| Shared workflow | `IWorkflowIntegrationService`, workflow status adapters, `WorkflowApprovalActions`, and `useWorkflowRecord` are mandatory integration points when approval execution is added. |
| Evidence/storage | `WorkflowEvidenceService`, `FileUploadController`, and `IFileStorageService` own evidence validation/storage behavior. Store references and business linkage, not duplicate file content. |
| Completed PR sourcing boundary | `ProcurementRequisitionSourcingReleaseService`, `ProcurementRequisitionSourcingReleaseStore`, the sourcing routes in `PurchaseRequisitionsController`, direct RFQ/tender/plan service guards, and `PurchaseRequisitionSourcingReleaseControl` are the verified `TDC-0107` boundary. Calendar work must not weaken, duplicate, or couple scheduling state into this release aggregate. |

### Frontend Anchors

| Concern | Existing code anchor and instruction |
| --- | --- |
| Current page | `frontend/src/app/administration/procurement/purchase-order-settings/page.tsx` is a compatibility/settings page, not the new policy-profile workbench. |
| Current service | `frontend/src/services/procurementSettingsService.ts` uses a legacy raw-fetch pattern. For new work, follow the shared authenticated API wrapper used by `workflow-api.service.ts` and nearby modern services. |

Shared administration anchors:

- Workflow administration: `frontend/src/app/administration/workflow/page.tsx` shows lifecycle/history administration patterns, but it currently has uncommitted team changes. Read its diff before reuse or edits.
- Shared controls: use existing `Button`, `Dialog`, `AlertDialog`, `Tabs`, `Select`, `Switch`, `Input`, `Textarea`, table/data-grid, badges, toast, loading, and empty-state components.
- Navigation: add the new route to the existing Administration/Procurement navigation group in `frontend/src/components/layout/sidebar.tsx` only after verifying the current group structure.

## Dependency-Safe Delivery Sequence

### Foundation Through Award

| Delivery wave | Tracker tasks | Outcome before the next wave |
| --- | --- | --- |
| A. Configuration lifecycle | `TDC-0001` | Typed decision register, draft/publish/retire lifecycle, evidence references, validation, history, admin UI, tests, and migration. No transaction behavior changes. |
| B. Executable policy model | `TDC-0002`, then `TDC-0003` | Relational methods/thresholds/authorities/evidence/exception/SOD rules plus one explainable central decision service. |
| C. Cross-cutting controls | `TDC-0006`, `TDC-0004`, `TDC-0005`, `TDC-0007` | Immutable control decisions, SOD hard stops, roles/workflows, and maker-checker reusable across later slices. |
| D. Planning and PR vertical flow | `TDC-0101` through `TDC-0108` | APP, specifications, budget, requisition, exception, workflow, and commitment gates work end to end without reopening delivered planning features. |
| E. Sourcing through award | `TDC-0201` through `TDC-0212` | Method selection, tendering, evaluation, committees, exceptions, award, and statutory evidence. |

### Supplier Through Deployment

| Delivery wave | Tracker tasks | Outcome before the next wave |
| --- | --- | --- |
| F. Supplier controls | `TDC-0301` through `TDC-0308` | Portal/registration, fees, due diligence, AVL, risk, performance, sanctions, and controlled master changes. |
| G. Frameworks/contracts/POs | `TDC-0401` through `TDC-0409` | Commitments, call-offs, amendments, dispatch, acknowledgements, and contract controls. |
| H. Receipt through payment | `TDC-0501` through `TDC-0509` | Receipt inspection, GRN/MRN, rejection, landed cost, three-way matching, exception approval, payment, and GL. |
| I. Inventory control | `TDC-0601` through `TDC-0616` | Stores, traceability, reservation, issue/return/transfer, counts, valuation, replenishment, barcode/mobile, disposal, and item master. |
| J. Assurance and deployment | Phases 7 through 9 | Reports, archive, integrations, migration, data quality, training, NFRs, cutover, and success measures. |

Do not combine multiple waves into one unreviewable change. Within a wave, deliver one coherent vertical slice at a time and keep the tracker current.

## Portfolio Continuity After Procurement

Procurement and Inventory is the first tracker-driven implementation programme. Complete this tracker fully before moving the active delivery programme to another module. Completion means all applicable roadmap tasks, end-to-end scenarios, integrations, migrations, permissions, UI, reports, UAT evidence, and release gates satisfy the tracker Delivery Rule; reaching the end of a phase or completing only P0 items is not enough.

After Procurement and Inventory is fully completed and certified, repeat the same controlled method using each module's own tracker as its authoritative delivery ledger:

- `docs/tdc-fleet-management-gap-implementation-tracker.md`.
- `docs/tdc-sales-marketing-crm-gap-implementation-tracker.md`.
- `docs/tdc-quantity-survey-gap-implementation-tracker.md`.
- `docs/tdc-civil-engineering-gap-implementation-tracker.md`.

For every later module, create a fresh module-specific implementation handover from the then-current repository state. Reuse shared platform capabilities such as workflow, evidence, notifications, audit, documents, identity, Finance, and master data, but never copy Procurement task IDs, statuses, policy rules, or assumptions into another module. Do not run two tracker programmes as one blended implementation unless the user explicitly approves a cross-module dependency slice.

## Historical First Slice Specification: TDC-0001

The following specification is retained as delivery history for the completed configuration foundation. It is not the next implementation instruction; current work resumes at `TDC-0207` under the live tracker row and Delivery Contract.

### Slice Name

Procurement Configuration Profile Lifecycle and Decision Register

### Slice Intent

Create the versioned, tenant-scoped administration foundation for `DEC-001` through `DEC-014`. TDC values may remain pending while development proceeds, but only approved, validated values with required evidence can be published. The slice records configuration decisions; it does not yet enforce them on PR, PO, receipt, payment, or stock transactions.

### Required Domain Model

Use names sympathetic to the repository, but preserve these responsibilities:

| Record | Required responsibility |
| --- | --- |
| `ProcurementConfigurationProfile` | Tenant, stable profile key, name, version, Draft/Published/Retired lifecycle, effective period, change summary, publication/retirement actors and dates, default flag, and row version. |
| `ProcurementConfigurationDecision` | Profile link, one allowed `DEC-*` key, schema version, owner, status, typed value JSON, decision/effective dates, approval/evidence state, source lineage, notes, and row version. |
| `ProcurementConfigurationEvidenceLink` | Decision/profile link to shared uploaded/evidence metadata, evidence type, file/reference identifier, checksum/reference metadata where available, uploader, and date. Do not duplicate file bytes. |
| `ProcurementConfigurationRevision` | Append-only before/after snapshot, actor, action, timestamp, correlation ID, source decision/profile, and reason. |

Use explicit unique indexes at minimum for profile family/version per tenant, a single published active profile per family/tenant, and one decision key per profile. Use check/validation rules for dates and lifecycle state. Tenant ID comes from the authenticated context.

### Decision Schema Registry

Implement an explicit registry for all 14 keys. Each entry must define its display metadata, owner group, schema version, typed request/value DTO, validator, and publication requirements.

| Decision | Minimum typed value shape |
| --- | --- |
| `DEC-001` | Procurement category/service class, method, currency, lower/upper bounds, inclusivity, effective period, and statutory reference. |
| `DEC-002` | Authority level, currency, bounds/inclusivity, escalation authority, category applicability, and effective period. |
| `DEC-003` | Transaction/entity type, policy selector, workflow definition reference, applicability conditions, and effective period. |
| `DEC-004` | Authority/committee/observer role, quorum, evidence, amount/category conditions, sequence/group, and escalation. |
| `DEC-005` | Petty threshold, waiver eligibility, justification/evidence, approver, expiry, and effective period. |
| `DEC-006` | Restricted/single-source prerequisite, approval authority, mandatory evidence checklist, filing reference, and expiry. |
| `DEC-007` | Fee type, amount/currency, tax, payment channel, receipt format, exemption/refund/renewal rules, and effective period. |
| `DEC-008` | Document type, allowed signature mode, signatory role/order, verification/evidence rules, and effective period. |
| `DEC-009` | GHANEPS profile, file/template/reference mapping, frequency, owner, acknowledgement/reconciliation rule, and effective period. |
| `DEC-010` | Negative-stock default, emergency-override eligibility, permission/workflow/evidence, duration, and audit requirement. |
| `DEC-011` | AVL review frequency, risk dimensions/bands, concentration limit, minimum score, resulting eligibility action, and effective period. |
| `DEC-012` | Cutover date, dual-run period, data owner, acceptance signatories, release status, and evidence. This is a deployment gate, not a runtime policy. |
| `DEC-013` | Receipt document type, GRN/MRN applicability, coexistence rule, number format, template, signature/evidence requirements, and effective period. |
| `DEC-014` | Workload scenario, availability/response target, backup/RPO/RTO, authentication, monitoring, usability/accessibility target, and acceptance method. |

Do not turn the registry into executable threshold routing yet. `TDC-0002` owns normalized runtime policy entities; `TDC-0003` owns rule resolution.

### Lifecycle Invariants

1. Only Draft profiles and Draft/Proposed decisions are editable.
2. Published and Retired profiles and their decision snapshots are immutable.
3. Publishing validates every required decision in the profile, typed payloads, owners, decision/effective dates, evidence, date overlaps, and version uniqueness.
4. Publishing and retirement of the previously published version occur atomically.
5. Clone-to-draft creates the next version, preserves lineage and values, and resets approvals for decisions that require renewed approval.
6. Runtime queries must eventually resolve only Published, effective records; first-slice APIs may expose this query but no transaction service may consume it yet.
7. Deleting Published/Retired history is prohibited. Draft deletion is allowed only when no workflow/evidence dependency prevents it and must be audited.
8. Row-version mismatch returns a conflict response, not last-write-wins behavior.
9. Cross-tenant IDs return not found/forbidden without leaking record existence.

### Backend Deliverables

- Add the new entities and enums under `src/ErpSystem.Core/Entities/Procurement` and `src/ErpSystem.Core/Enums` or the existing procurement enum location.
- Add typed DTOs and request contracts under `src/ErpSystem.Core/DTOs/Procurement`.
- Add lifecycle policy, decision registry/validators, service interface, and service implementation under the established Procurement namespaces.
- Add repositories only where repository-specific queries are needed; use existing generic repository conventions otherwise.
- Register repositories/services in `ServiceCollectionExtensions.cs`.
- Add DbSets and explicit EF configuration/indexes in `ApplicationDbContext` or the repository's established model-configuration location.
- Add a tenant-scoped controller under `src/ErpSystem.Api/Controllers/Procurement`.
- Add append-only audit/revision writes in the same unit-of-work operation as state changes.
- Use shared file/evidence services for uploads/references and reject unsupported or unsafe evidence through existing policy.

### API Contract

Use REST names consistent with the repository. The following capability set is required even if final route spelling changes:

| Method and route | Behavior |
| --- | --- |
| `GET /api/procurement/configuration-profiles` | Tenant-scoped paged list with lifecycle/search filters and decision completeness summary. |
| `GET /api/procurement/configuration-profiles/{id}` | Full profile, decision values, evidence summaries, validation state, and history summary. |
| `POST /api/procurement/configuration-profiles` | Create a Draft and seed the allowed `DEC-*` decision rows idempotently. |
| `PUT /api/procurement/configuration-profiles/{id}` | Update Draft metadata with row-version concurrency. |
| `PUT /api/procurement/configuration-profiles/{id}/decisions/{decisionKey}` | Validate and save one typed Draft decision. |
| `POST /api/procurement/configuration-profiles/{id}/validate` | Return structured errors/warnings by decision key without publishing. |
| `POST /api/procurement/configuration-profiles/{id}/publish` | Authorize, validate, atomically publish, retire prior version, and audit. |
| `POST /api/procurement/configuration-profiles/{id}/clone-draft` | Create the next Draft version with lineage and approval reset rules. |
| `GET /api/procurement/configuration-profiles/{id}/history` | Return immutable revision/activity history. |
| Evidence routes | Link/unlink shared evidence only while Draft, with authorization, malware/file-policy validation, and audit. |

Return structured validation problems. Do not catch every exception and flatten it into an untyped 500 string. Never combine `[AllowAnonymous]` and `[Authorize]`; the existing settings check endpoints contain that contradictory pattern and must not be copied.

### Authorization And Tenant Rules

- Begin with the narrowest existing safe administration boundary, normally `SuperAdmin` and `TenantAdmin`, while `TDC-0005` introduces approved TDC procurement permissions.
- Add the authenticated user's tenant and user IDs server-side.
- Read/list/detail APIs are tenant-scoped; create/update/publish/clone/evidence actions require explicit authorization.
- Publish must require a separate privileged action from ordinary draft editing.
- Add negative controller/service tests for unauthenticated, unauthorized role, cross-tenant ID, and direct publish attempts.

### Frontend Deliverables

Create a dedicated administration workspace rather than extending the current Purchase Order Settings page:

- List route: `/administration/procurement/policy-profiles`.
- Detail/editor route: `/administration/procurement/policy-profiles/[id]`.
- History-first list with search, lifecycle filter, version/effective status, completeness, updated actor/date, and row actions.
- Detail tabs for Overview, Decisions, Validation, Evidence, and History.
- Decision forms rendered from an explicit typed registry, using selects for enumerations/owners, date controls for effective periods, amount/currency controls, toggles for booleans, and shared evidence upload controls.
- Shared `Dialog` for create/clone and shared `AlertDialog` for publish/retire/delete confirmations. No browser prompts.
- Clear loading, empty, error, validation, conflict, immutable, and success states.
- Compact responsive design consistent with the current administration pages; avoid cards inside cards and avoid one giant form.
- Add navigation only after route and authorization are working.
- Use the shared authenticated API service rather than duplicating local-storage token/header code.

### Migration And Seed Plan

1. Generate one focused EF Core migration after the model is stable.
2. Inspect the generated migration and SQL for only the intended profile/decision/evidence/revision tables, indexes, foreign keys, row versions, and seed/backfill behavior.
3. Seed the 14 decision definitions in code through an idempotent registry; do not store display metadata as duplicated tenant rows unless required.
4. Create one Draft TDC profile per existing tenant through an idempotent backfill/seeder. Do not publish guessed values.
5. Ensure newly created tenants receive the same Draft initialization.
6. Apply the migration to the configured test `RhemaERP` database only after reviewing the active connection source and keeping secrets out of logs.
7. Verify migration history, table/index presence, tenant counts, profile version uniqueness, and that no existing procurement settings or transaction rows changed.

### Required Tests For TDC-0001

| Test area | Minimum cases |
| --- | --- |
| Lifecycle policy | Draft editable; Published/Retired immutable; clone increments version; delete restrictions; published runtime eligibility. |
| Registry/validation | All `DEC-001` through `DEC-014` registered; unknown key rejected; schema version and typed payload validation; effective-date validation. |
| Publication | Pending/invalid/missing-evidence decisions block publish; valid profile publishes; previous version retires atomically; publish is idempotent or safely rejected. |
| Tenant isolation | List/detail/update/clone/publish cannot cross tenant; unique indexes include tenant. |
| Authorization | Anonymous and non-admin mutation attempts fail; direct API publish bypass fails. |
| Concurrency | Stale row version returns conflict and preserves the newer edit. |
| Audit/history | Create, edit, evidence link, validate, publish, retire, clone, rejected bypass, and delete actions record actor/result/correlation and before/after where applicable. |
| Seeding/migration | Existing tenant receives one Draft with all 14 keys; rerun creates no duplicates; existing procurement data remains unchanged. |
| Frontend | Service mapping and principal forms/components have focused Vitest coverage where repository patterns support it; targeted ESLint/type-check are clean. |

### TDC-0001 Definition Of Done

`TDC-0001` may be changed to `Done` only after all of the following are true:

- All 14 decision schemas are represented and validated.
- Draft/publish/retire/clone lifecycle works and Published/Retired data is immutable.
- Required owner/date/effective/evidence/publication protections are enforced server-side.
- Tenant-safe API and dedicated administration UI are complete.
- Migration and idempotent tenant initialization are applied and verified.
- Authorization, tenant-isolation, concurrency, lifecycle, audit, and bypass tests pass.
- Backend build, focused tests, targeted frontend lint/type-check, API smoke, and browser smoke pass.
- Existing procurement behavior is unchanged.
- Tracker task, coverage matrix, code anchors, and Verification Log contain exact evidence.

If any applicable item is incomplete, keep the active tracker task `In progress`; do not create a misleading `Done` state.

## Step-By-Step Start Procedure

1. Re-run `git status --short --branch`, upstream/divergence checks, `git diff --check`, and the diff for every already modified file the slice may touch.
2. Read `SRC-006`, `SRC-011` through `SRC-013`, `TDC-0207`, `PROC-005` through `PROC-007`, `E2E-001` through `E2E-003`, the Delivery Rule, Tracker Maintenance, and the completed `TDC-0201` through `TDC-0206` evidence before changing procurement behavior.
3. Preserve the pushed `TDC-0101` through `TDC-0203` baseline, completed uncommitted `TDC-0204` through `TDC-0206`, and all team-owned dirty/untracked files; do not commit `full_database.sql` or unrelated module trackers.
4. Confirm the 201 Core/117 API/92 frontend procurement baseline, verify `RhemaERP` remains current through `20260723161619_AddProcurementPrequalificationPolicyLineage` with all sourcing, RFQ, tender, exceptional-sourcing, and prequalification guards enabled, and record any drift before editing.
5. Change only `TDC-0207` from `Not started` to `In progress`; add a dated Verification Log kickoff entry with the exact tender-document/template/issuance boundary.
6. Inventory existing tender documents, NCT/ICT issue/sale receipts, RFQ dispatch records, advertisement references, fee handling, addenda or revision fields, bid deadlines/validity, shared workflow/evidence/storage, capabilities/SOD, notifications, and control events before adding behavior.
7. Model reusable tenant-safe tender-document templates and immutable approved versions for the applicable procurement methods, preserving exact source policy/configuration, workflow, approver, evidence, checksum, effective dates, and supersession lineage.
8. Bind each controlled issue to one exact approved document version, tender/sourcing case, recipient, paid-or-free fee decision, payment/receipt, channel, issuer, timestamp, and acknowledgement; prevent duplicate or stale-version issue and retain all historical issues.
9. Implement governed addenda with exact changed version, reason, approval/evidence, affected recipients, dispatch/acknowledgement, and deadline consequences. Do not mutate an already issued document version or recreate the existing evidence/storage platform.
10. Implement deadline and bid-validity extensions with authorized reason, exact shared-workflow outcome where required, evidence, previous/new instants, affected bidders, notification/acknowledgement, and immutable history without weakening sealed receipt/opening rules.
11. Define fail-closed states for missing/unapproved/stale/foreign document versions, fee/receipt mismatch, duplicate issue, unacknowledged mandatory addendum, invalid or retroactive extension, unauthorized actors, SOD conflicts, concurrency/idempotency, and direct API/SQL bypass.
12. Expose tenant-safe authenticated history-first API/UI surfaces for templates, versions, approvals, tender bindings, issue/fee receipts, addenda, acknowledgements, deadline/validity extensions, evidence, actors, blocked reasons, and immutable timeline. Reuse shared dialogs, grids, evidence controls, and status patterns.
13. Add service/direct-API/frontend tests for applicable method happy paths and every hard stop, authorization, tenant isolation, concurrency/idempotency, exact-version issue, fee handling, addenda acknowledgement, deadline/validity extension, workflow/evidence/SOD, and unchanged `TDC-0201` through `TDC-0206` boundaries. Add a focused migration only for verified missing persistence/SQL enforcement, apply it to `RhemaERP`, and run SQL/API/browser smoke plus protected-count cleanup.
14. Review `git diff --check`, synchronize all genuinely changed coverage/traceability rows, code anchors, and exact Verification Log evidence, and mark `TDC-0207` `Done` only when every applicable Delivery Contract gate passes. Commit only when the user requests it; do not begin `TDC-0208` in the same slice.

## Verification Commands

Run from the repository root unless a command says otherwise:

```powershell
git status --short --branch
git diff --check
dotnet build ErpSystem.sln --no-restore
dotnet test tests/ErpSystem.Core.Tests/ErpSystem.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~Procurement"
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Controllers.Procurement"
dotnet ef migrations script --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext
```

Run from `frontend`:

```powershell
npx eslint <all changed TDC-0207 TypeScript and TSX files>
npx tsc --noEmit 2>&1 | Select-String "<TDC-0207 tender-document file names>"
npm run test -- --run <focused tender-document and issuance tests>
npm run dev
```

The full `npm run type-check` currently fails in unrelated Finance/tax/mock-data files. The next agent must still run it, retain the baseline distinction, and prove there are no new errors in changed procurement files. Do not fix unrelated Finance errors in this slice.

## Known Traps And Required Responses

| Trap | Required response |
| --- | --- |
| Treating business decisions as code blockers | Build Draft configuration and typed schemas now; gate only publication/UAT of the affected rule. |
| Extending the mutable `ProcurementSettings` row | Add the versioned policy profile beside it; preserve compatibility until controlled migration is planned. |
| Copying broad/contradictory authorization | Use explicit authenticated administration actions; never combine `[AllowAnonymous]` and `[Authorize]`. |
| Enforcing UI-only restrictions | Put every hard stop in service/API code and add direct-API negative tests. |
| Building a new workflow/evidence implementation | Reuse the shared workflow, file storage, evidence policies, notifications, and audit history. |
| Adding a large all-in-one settings screen | Use dedicated list/detail routes and shared dialogs/controls. |
| Publishing seeded questionnaire values | Seed them only as clearly unapproved Draft suggestions; never activate them without TDC approval evidence. |
| Editing or deleting the dirty worktree | Preserve team changes and keep procurement commits scoped. |
| Marking a task Done after backend completion | Require UI, migration, authorization, tests, database apply, smoke evidence, and tracker update. |
| Expanding scope into planning features already delivered | Use `docs/procurement-planning-tasklist.md` and current code to reuse the existing plan/version/workflow/publish/report surfaces. |

## Tracker Update Protocol

- Start each slice by naming explicit `TDC-*` IDs in the tracker and implementation notes.
- Update a task to `In progress` when code work actually starts.
- Keep the Current Coverage and Gap Matrix aligned when behavior changes.
- Add exact migration name, API routes, UI routes, test commands/counts, database evidence, and browser/API smoke results to the Verification Log.
- Record approved configuration decisions with owner, date, effective date, values, and evidence; do not rewrite history.
- Do not mark a phase complete until all its tasks and applicable end-to-end scenarios meet the Delivery Rule.
- Preserve success-target measurement definitions; do not claim business outcomes before go-live measurement.

## Next-Agent Kickoff Prompt

Use this prompt in the new chat after opening the repository:

> Continue the TDC Procurement and Inventory implementation using `docs/tdc-procurement-inventory-implementation-handover.md` and `docs/tdc-procurement-inventory-gap-implementation-tracker.md` as the active delivery documents. Start with `TDC-0207` only. Reverify the dirty worktree and completed Phase-0/Phase-1/`TDC-0201` through `TDC-0206` baseline first, preserve all existing team changes, update `TDC-0207` to `In progress`, then add controlled tender-document templates and the issuance/addenda/extension register across backend, focused migration and SQL protection where required, tenant-safe API, explicit authorization/SOD, shared workflow/evidence/notifications, immutable audit, dedicated history-first UI, tests, test-database apply, and SQL/API/browser smoke. Enforce reusable approved document versions, exact tender/version/recipient issue lineage, paid-or-free fee and receipt, immutable issuance history, governed addenda with acknowledgement, and authorized deadline/bid-validity extensions; reuse the delivered policy, sourcing-release/case, RFQ/tender/exceptional-sourcing/prequalification, workflow, evidence, identity, notification, supplier-validation, SOD, and control-event architecture; do not recreate those controls, begin `TDC-0208`, or broadly change PO, receiving, or inventory runtime behavior, and do not mark the task `Done` until every applicable acceptance gate passes.

## Handover Completion Signal

The next agent should consider this handover successfully picked up when it has:

- Reverified repository and worktree state.
- Confirmed the tracker and existing planning tracker were read.
- Named `TDC-0207` as the only active task.
- Reported the intended tender-document/template/issuance domain, migration, API, UI, and test files before editing.
- Started implementation without touching unrelated dirty files, recreating shared compliance/release/case/method-selection/RFQ/NCT/ICT/exceptional-sourcing/prequalification/workflow/evidence/identity/audit controls, beginning `TDC-0208`, or changing completed Phase-1/`TDC-0201` through `TDC-0206` and unrelated PO/receiving/inventory behavior.
