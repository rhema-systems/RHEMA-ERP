# Enterprise Workflow Platform Tracker

Last updated: 2026-07-07

## Purpose

This tracker governs workflow as a shared ERP platform capability. Module teams must integrate through the shared backend interfaces, status adapters, frontend hooks, and workflow controls instead of implementing separate approval engines or dialogs.

## Delivery Rule

A capability is `Done` only when configuration, engine enforcement, authorization, audit history, shared UI, direct-API protection, and focused tests are present.

## Foundation And Reuse

| ID | Status | Capability |
| --- | --- | --- |
| WF-0001 | Done | Shared `IWorkflowIntegrationService` for submit, approve/reject, recall, cancel, and permission checks. |
| WF-0002 | Done | Shared `IWorkflowStatusAdapter` contract and registry for module-specific status transitions. |
| WF-0003 | Done | Automatic status-adapter discovery from registered assemblies. |
| WF-0004 | Done | Duplicate workflow entity aliases fail at startup instead of silently selecting the last adapter. |
| WF-0005 | Done | Shared detail-page controls, history panel, record tab, batch summary hook, and `useWorkflowRecord` integration hook. |
| WF-0006 | Done | Purchase Requisition, Purchase Order, Procurement Plan, Tender, Sales Order, Sales Agreement, and Sales Allocation detail pages use the shared hook; list surfaces use the shared action control and batch summary hook where record summaries are displayed. |
| WF-0007 | Done | Active entity types are audited by a tenant-scoped conformance endpoint and admin UI; default seeding activates only concrete adapters, preserves configured legacy gaps for migration, and alias/catalog tests prevent silent drift. |

## Approval Policy And Separation Of Duties

| ID | Status | Capability |
| --- | --- | --- |
| WF-0101 | Done | Required/optional approval checklist items. |
| WF-0102 | Done | Checklist document type/name, per-item evidence upload, hard stop, and audit history. |
| WF-0103 | Done | Configurable initiator self-approval prevention, enforced in capability checks and engine execution. |
| WF-0104 | Done | Configurable distinct approvers for multiple approval slots. |
| WF-0105 | Done | Any, all, majority, and minimum-number approval modes exposed in the designer. |
| WF-0106 | Done | Previous-step actor, any-prior-approver, named-step actor, and workflow-context-user conflicts are enforced in capability checks and execution; the designer includes an ERP SOD preset for requester/creator/evaluator/receiver conflicts. |
| WF-0107 | Done | Effective-dated reusable approval policy sets resolve by module, entity, category, value, currency, location, and legal entity; published policies are immutable, selected server-side at execution, and administered through a shared workflow tab. |
| WF-0108 | Done | Parallel or sequential approval groups are configurable; later groups persist as queued, receive no due date or notification before activation, activate in order, and remain visible in shared audit history. |

## Lifecycle, Delegation, And SLA

| ID | Status | Capability |
| --- | --- | --- |
| WF-0201 | Done | Effective-dated delegation has shared administration UI, required reason, selectable principal/delegate users, module/entity/workflow/step authority scope, monetary and currency limits, re-delegation control, engine enforcement, and audit. |
| WF-0202 | Done | Out-of-office substitution uses effective dates, scoped authority, self/circular/re-delegation conflict validation, and automatic runtime resolution. |
| WF-0203 | Done | Tenant working calendars calculate due dates outside holidays/non-working hours; the idempotent SLA worker executes notify, reassign, auto-approve, and cancel escalation rules with activity history, and governance UI exposes open SLA breaches and recent escalations. |
| WF-0204 | Done | Request-information and targeted send-back support correction owner, target step, instructions, due time, resubmission, status restoration, and audit. |
| WF-0205 | Done | Authorized administrators can add or remove ad hoc user/role approvers with group, due time, mandatory reason, notification, and audit history. |

## Versioning, Documents, And Signatures

| ID | Status | Capability |
| --- | --- | --- |
| WF-0301 | Done | Definitions use immutable Draft, Published, and Retired versions; publishing retires the prior version and every instance remains pinned by `WorkflowDefinitionId`. |
| WF-0302 | Done | Version history and structural comparison identify breaking changes; non-persistent path simulation and controlled rollback-to-new-draft are available in shared administration. |
| WF-0303 | Done | Evidence records persist issue/expiry dates, owner, SHA-256, replacement lineage, version/current state, verification status, reviewer, and notes; shared administration loads workflow instances/steps, evidence counts, review details, and verification actions. |
| WF-0304 | Done | Per-step electronic-signature policy enforces method, signing role, attestation, signing window, external reference, and X.509 certificate validity/chain rules; committed evidence stores signer, hash, certificate, IP, and user agent. |
| WF-0305 | Done | Tenant evidence policy controls extensions, size, malware content scanning, seven-year minimum retention, legal holds, and retention/hold-protected deletion through shared administration. |

## Integration, Monitoring, And Operations

| ID | Status | Capability |
| --- | --- | --- |
| WF-0401 | Done | Integration executions enforce tenant-scoped idempotency, endpoint validation, exponential retry, attempt limits, dead-letter state, response/error capture, manual retry, and reconciliation. |
| WF-0402 | Done | Shared monitoring and analytics show active/completed/failed totals, duration, SLA compliance, overdue approvals, rejection rate, and bottleneck steps. |
| WF-0403 | Done | Published definitions form a template catalogue with tenant-neutral export, validated graph import, entity-type remapping, and imported Draft isolation. |
| WF-0404 | Done | Responsive mobile approval inbox supports push subscription, local offline queueing, explicit approve/reject validation, user-bound idempotent replay, and sync status. |
| WF-0405 | Done | Workflow audit events are immutable, default retention is 2,555 days, evidence honors retention/legal hold, and date-bounded archive retrieval returns a deterministic SHA-256 digest. |

## Completion State

All capabilities in this tracker are implemented. New module teams should follow `enterprise-workflow-module-integration-guide.md` and must not create parallel approval state machines, evidence stores, dialogs, or module-specific workflow APIs.

## Verification Log

| Date | Check | Result |
| --- | --- | --- |
| 2026-07-07 | Core and API build | Passed; no compile errors. |
| 2026-07-07 | Workflow lifecycle migration generation | Passed; legacy active definitions backfill to Published, used inactive definitions to Retired, and unused inactive definitions to Draft. |
| 2026-07-07 | Workflow frontend semantic scan | Passed for workflow administration, designer, API service, and workflow types; repository-wide type check remains blocked by unrelated existing Finance and stale `.next` errors. |
| 2026-07-07 | Workflow lifecycle policy smoke harness | Passed; draft editability, published/retired immutability, runtime eligibility, next-version calculation, and immutable edit guard executed successfully. |
| 2026-07-07 | Permanent workflow test project | Passed after repairing the stale project-display test constructor; 34 workflow-focused tests pass. |
| 2026-07-07 | Shared module integration adoption | Passed for Purchase Requisition, Purchase Order, Procurement Plan, Tender, Sales Order, Sales Agreement, and Sales Allocation detail pages. |
| 2026-07-07 | Entity-type conformance and transactional adapters | Passed; tenant conformance API/admin visibility, supported-only default activation, safe legacy reconciliation, and Work Order/RFQ/quote/bid/evaluation/customer adapters compile and pass focused tests. |
| 2026-07-07 | Sequential approval groups | Passed; designer configuration, engine queue/activation enforcement, persisted group state, notifications, audit visibility, migration, and sequence tests are present. |
| 2026-07-07 | Governance and SLA operations | Passed; effective delegation/substitution, working calendar, send-back/resubmission, ad hoc approvers, escalation worker, shared UI, migration, and policy tests are present. |
| 2026-07-07 | Evidence and signatures | Passed; metadata/versioning, verification UI, policy controls, SHA-256, malware scan, retention/legal hold, signature staging/commit, role/attestation/certificate validation, and migration are present. |
| 2026-07-07 | Platform operations | Passed; simulation, rollback draft, templates, analytics, integration retry/dead-letter/reconciliation, mobile/offline actions, push subscription, and audit archive are implemented. |
| 2026-07-07 | Evidence/platform migration SQL | Passed; all five new workflow tables and seven-year retention column are present with unique idempotency/signature/evidence indexes and no unrelated EHC foreign-key drift. |
| 2026-07-07 | Final workflow test suite | Passed; 47 focused workflow tests, including platform graph, offline action, retry/dead-letter, archive hash, SOD, delegation, lifecycle, sequence, checklist, signature, adapter, and conformance policies. |
| 2026-07-07 | Final frontend semantic check | Passed for all workflow administration, governance, evidence, analytics, template, mobile, shared component, service, and type files; repository-wide check still reports unrelated existing errors. |
| 2026-07-07 | Final API build | Passed with zero errors and no workflow-specific warnings. |
| 2026-07-07 | Delegation, evidence, and SLA admin refinement | Passed; backend build, delegation policy tests, targeted ESLint, and diff whitespace checks passed. Repository-wide TypeScript still fails on unrelated Finance AR/Cash pages. |
