# TDC Procurement and Inventory Implementation Handover

Last updated: 2026-07-20

Document status: Ready for the next implementation agent

## Handover Objective

This document transfers the TDC Procurement and Inventory implementation from completed requirement and gap analysis into controlled delivery. The next agent should be able to open this repository, verify the live state, and begin the first implementation slice without repeating the analysis or inventing a parallel architecture.

The governing delivery ledger is `docs/tdc-procurement-inventory-gap-implementation-tracker.md`. It currently contains 92 roadmap tasks across phases 0 through 9 and 26 mandatory end-to-end acceptance scenarios. All roadmap tasks and scenarios remain `Not started`; the next agent must update them only as verified implementation work progresses.

## Immediate Starting Decision

Start with `TDC-0001`: the procurement configuration-profile and business-decision lifecycle foundation.

Do not begin by adding hard stops to Purchase Requisitions, Purchase Orders, receiving, invoice matching, or stock transactions. Those controls depend on approved, effective-dated policy configuration and the central compliance decision service planned in `TDC-0002` and `TDC-0003`.

The first slice must add a safe configuration foundation beside the existing mutable `ProcurementSettings` record. Existing procurement behavior must remain unchanged until a policy profile is published and a later slice explicitly consumes it.

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

This snapshot was verified on 2026-07-20 and must be refreshed at the beginning of the next chat.

| Item | Verified state |
| --- | --- |
| Repository root | `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2` |
| Branch | `master`, tracking `origin/master` |
| Baseline commit | `d133850568d57deb9b436f29544ef4f38a70d96b` (`Replace workflow browser prompts with audited dialogs`) |
| .NET SDK | `8.0.206` from `global.json` |
| Frontend | Next.js `15.5.3`, React `19.1.0`, TypeScript 5, React Query 5, Vitest 3, Lucide icons, shared Radix/shadcn controls |
| Backend build | `dotnet build ErpSystem.sln --no-restore` passed with 0 warnings and 0 errors |
| Focused Core tests | 9 procurement/workflow policy tests passed |
| Focused API tests | 3 procurement controller tests passed; unrelated test-project warnings remain |
| Targeted frontend lint | Existing Procurement Settings page and service passed ESLint |
| Full frontend type-check | Fails in existing unrelated Finance, tax, mock-data, and session-timeout files; no new procurement work may add errors |

### Dirty Worktree Protection

The worktree is not clean. At handover time it contains:

- Modified `docs/enterprise-workflow-module-integration-guide.md`.
- Modified `docs/tdc-procurement-inventory-gap-implementation-tracker.md`.
- Modified `frontend/src/app/administration/workflow/page.tsx`.
- Untracked gap-analysis and management-pack Markdown files in `docs`.
- Untracked `src/ErpSystem.Api/full_database.sql`.

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

## First Slice Specification: TDC-0001

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

If any item is incomplete, keep `TDC-0001` as `In progress`; do not create a misleading `Done` state.

## Step-By-Step Start Procedure

1. Re-run `git status --short --branch` and `git diff --` for every already modified file that the slice may touch.
2. Read the tracker sections for `DEC-001` through `DEC-014`, phase 0, Delivery Rule, and Tracker Maintenance.
3. Read the current `ProcurementSettings` entity/service/controller/UI and the workflow lifecycle policy/service/tests.
4. Confirm the baseline build and focused tests still pass. Record any drift before editing.
5. Change only `TDC-0001` from `Not started` to `In progress`; add a dated Verification Log entry that the slice began and name its scope.
6. Implement the lifecycle policy and registry tests first, then entities/DTOs/services, EF mapping, controller, migration, frontend service/routes, UI, authorization tests, and smoke verification.
7. Keep each intermediate change buildable. Do not wire PR/PO/stock enforcement into this slice.
8. Apply and inspect the migration against the test database only after code tests pass.
9. Run browser/API smoke checks with representative Draft, validation failure, publish, immutable Published, and clone flows.
10. Review `git diff --check`, confirm only intended files changed, update tracker evidence, and mark `TDC-0001` `Done` only if its full definition is met.
11. Commit only the slice files when the user requests a commit. Do not include unrelated dirty/untracked files.
12. Begin `TDC-0002` only after the first slice has passed and the tracker reflects reality.

## Verification Commands

Run from the repository root unless a command says otherwise:

```powershell
git status --short --branch
git diff --check
dotnet build ErpSystem.sln --no-restore
dotnet test tests/ErpSystem.Core.Tests/ErpSystem.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~ProcurementConfiguration|FullyQualifiedName~ProcurementPolicy"
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~Controllers.Procurement.ConfigurationProfiles"
dotnet ef migrations script --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext
dotnet ef database update --project src/ErpSystem.Data --startup-project src/ErpSystem.Api --context ApplicationDbContext
```

Run from `frontend`:

```powershell
npx eslint <all changed procurement TypeScript and TSX files>
npx tsc --noEmit 2>&1 | Select-String "policy-profiles|procurementConfiguration|configurationProfiles"
npm run test -- --run <focused procurement configuration tests>
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

> Continue the TDC Procurement and Inventory implementation using `docs/tdc-procurement-inventory-implementation-handover.md` and `docs/tdc-procurement-inventory-gap-implementation-tracker.md` as the active delivery documents. Start with `TDC-0001` only. Reverify the dirty worktree and baseline first, preserve all existing team changes, update `TDC-0001` to `In progress`, then implement the complete procurement configuration-profile lifecycle and `DEC-001` through `DEC-014` decision register across backend, migration, tenant-safe API, dedicated shared-control frontend, authorization, audit, tests, test-database apply, browser/API smoke, and tracker evidence. Do not change PR/PO/receiving/inventory runtime behavior yet, do not recreate workflow/evidence controls, and do not mark the task `Done` until every acceptance gate in the handover passes.

## Handover Completion Signal

The next agent should consider this handover successfully picked up when it has:

- Reverified repository and worktree state.
- Confirmed the tracker and existing planning tracker were read.
- Named `TDC-0001` as the only active task.
- Reported the intended entity/API/UI/test files before editing.
- Started implementation without touching unrelated dirty files or transaction enforcement.
