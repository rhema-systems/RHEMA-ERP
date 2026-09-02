# TDC Civil Engineering Interface Control Document

- Control ID: `TDC-CIV-ICD-001`
- Tracker task: `CIV-0603`
- Status: implementation baseline; authenticated integration acceptance remains open
- Applies to: Civil Engineering, Projects, Maintenance and Assets, DMS, Workflow, QS, Finance, Building Inspectorate, contractors, consultants, notifications, reporting and audit.

## Purpose

This document assigns an authoritative owner to every record Civil Engineering reads or affects. It is a control boundary, not a second integration engine. Existing owner services, identifiers, workflows, DMS records, audit events and exception handling remain authoritative.

## Non-negotiable controls

1. The authenticated server context supplies tenant, actor and permissions. The client never supplies trusted tenant, role, approval, file path, scan result or posting status.
2. Every supplied identifier is re-resolved in the current tenant and operation scope at the mutation point. A selector result never becomes an authorization grant.
3. Projects owns project membership, assignments and the base project work item. Civil may add its governed overlay and immutable feedback, but may not create a parallel task engine.
4. Central DMS owns file bytes, controlled upload, checksum, malware scanning, metadata, retention, legal hold, version publication, preview/download and access policy. Civil retains only approved DMS references and snapshots.
5. Shared Workflow owns definition versions, instances, assignments and final outcome. Civil must not manufacture an approval outcome or let a maker decide their own record.
6. QS owns QS estimates, valuations, certificates and retention. Finance owns budget, AP, payment, tax and GL posting. Civil may review a QS-owned IPC but cannot post, pay or reverse it.
7. Maintenance and Assets own the maintained asset, work-order execution and asset/financial outcome. Civil owns only governed intake, assessment, costing handoff, execution reference and completion-control lineage.
8. Cross-owner operations use a stable source/reference key, request ID and canonical hash. A timeout is recovered by querying the owner before retrying create; changed-payload retries fail closed.
9. Central audit and exception middleware own operational diagnostics. Domain revisions add redacted business lineage and never expose a stack trace, secret or physical path.

## Authoritative owner registry

| Object or responsibility | Authoritative owner | Civil may do | Civil must not do |
| --- | --- | --- | --- |
| Tenant, users, Security roles and permissions | Identity and Security | Read authenticated context and enforce Civil privileges | Accept client actor/tenant/role, duplicate a role store or bypass authorization |
| Project, membership, phases, packages and work items | Projects | Select an authorized project; create a governed Civil control over an existing project work item | Create a second project/task master or infer access from a project ID |
| Project design case, drafting case and Civil revision | Civil Engineering | Own governed design lifecycle, controlled assignments, review and immutable revisions | Bypass Projects scope or overwrite approved lineage |
| RFI and site-instruction business headers | Projects | Add Civil routing, controlled response/evidence and review lineage | Replace the Projects record or directly close a non-Civil routing |
| Weekly supervision and quality-test control | Civil Engineering | Own Civil report/test lifecycle and DMS evidence references | Store a local evidence repository or treat an unscanned file as approved evidence |
| Works contract, tender, award, supplier and contractor master | Procurement | Read tenant-safe project-linked operational references | Create, activate, approve or mutate owner procurement records |
| BOQ, valuation, IPC/certificate and retention | Quantity Survey | Submit an independent Civil IPC endorsement against an eligible certificate | Recalculate QS certificate balances, change approval/payment state or create a duplicate certificate |
| Budget, AP invoice, supplier payment, tax and GL posting | Finance | Read owner statuses and reconciliation references | Create a direct Finance posting, approve an invoice/payment or mark a certificate paid |
| Asset, maintenance work order and execution | Maintenance and Assets | Raise governed Civil intake/assessment/costing/execution-completion links | Rewrite work-order, asset, stock, ledger or closure outcome |
| Development application and Building Inspectorate movement | Building Inspectorate / Development approval owner | Maintain controlled file registration, section handoff, Civil review and HOD decision lineage | Treat a handoff as a permit approval or bypass Building Inspectorate ownership |
| Physical engineering files | Central DMS | Require current Published clean versions with approved Civil metadata | Store bytes, public URLs, executable files, a local path or unverified checksum |
| Workflow assignments and maker-checker outcome | Shared Workflow | Bind an approved/effective Civil workflow and process the current assigned step | Self-approve, edit workflow tables or map `Completed` to a rejection |
| Contractors and consultants | Procurement Business Partner plus Projects external access | Resolve the exact active linked partner and permitted project artifact | Infer identity from email or expose another partner/project |
| Notifications | Central notification service | Publish durable Civil events with recipient reference and correlation ID | Treat delivery as approval, publish credentials, or create a second outbox |
| Reports and exports | Shared Reports framework | Supply read-only tenant/project-filtered Civil report data | Create a separate report store/export engine or let client filters bypass server scope |
| Audit and unexpected errors | Central audit and exception middleware | Append redacted Civil action/revision references | Return a stack trace, suppress an exception or aggregate distinct incidents |

## Controlled selector contract

All selectors are server-produced and may only narrow already-authorized results. Free text may describe an instruction, finding, site note, risk or reason; it may never identify a governed master.

| Selector | Required server filter | Mutation-time revalidation |
| --- | --- | --- |
| Project | Current tenant, active/not deleted and Civil operation-level Projects access | Tenant, membership/section authority and current project state |
| Civil assignee / reviewer | Active tenant user, active project member and permitted configured Civil/Project role | Active identity, membership, configured role, independent reviewer and task state |
| Contractor / consultant / partner | Current tenant, active operational Business Partner, exact project/contract linkage | Partner status, tenant, project/contract line and external access policy |
| Works contract / tender / award / PO | Current tenant, project-linked and owner-approved/operational state | Exact project and owner lifecycle still eligible |
| QS payment certificate / IPC | Current tenant, selected project, eligible owner certificate and independently permitted Civil endorsement | Certificate/project/amount lineage, duplicate endorsement, QS state and maker-checker separation |
| Asset / building / maintenance work | Current tenant, active owner asset/work order and operation scope | Asset/building/project linkage and current owner status |
| DMS evidence | Current tenant, current Published version, controlled-upload relation, clean scan and permitted template | Scan/publication/metadata/version/checksum/access policy still valid |
| Workflow definition | Current tenant/entity type, Published/effective and expected Civil group | Current definition, assignment, actor authority and outcome mapping |
| Report project / date range | Civil report read/export permission and project read scope | Provider reapplies tenant/project predicate before date filtering |

## Interface flows

### Projects design, task and supervision

1. Projects owns the project, memberships and the authoritative work-item header.
2. Civil creates its governed design/routing/task overlay only after all tenant, membership, configured-role, DMS and workflow checks pass.
3. Civil feedback and review append immutable history and shared audit evidence; generic Projects mutation endpoints reject only governed Civil overlays.
4. Due date, overdue status and report filters are read-only projections and never alter the source lifecycle.

### Engineering files and external parties

1. A contractor or consultant authenticates through the exact active Business Partner and project-access policy.
2. Engineering evidence is created through the central controlled upload and is accepted only when its current DMS version is Published, clean, tenant-safe and carries the approved Civil metadata template.
3. Civil stores DMS record/version/checksum/policy snapshots and links them to the governed record; it never stores the physical file.
4. Quarantine, withdrawal, supersession or unauthorized access blocks dependent Civil submission/review while preserving historical lineage.

### Works, QS and Finance

1. Procurement owns the active Works contract, tender/award and contractor relationship.
2. QS owns the payment certificate, valuation, retention, deductions, Finance AP handoff and reconciliation.
3. Civil may submit/review an IPC endorsement with controlled evidence, but must not change QS approval, AP invoice, payment, tax or GL status.
4. Finance recalculates and owns posting. Mismatch, closed period, reversal or payment failure remains an owner-state reconciliation exception, never a Civil balancing transaction.

### Maintenance, assets and complaints

1. Civil owns governed maintenance/complaint intake, assessment, cost handoff and completion-control evidence.
2. Maintenance/Assets owns owner work execution, asset linkage, stock/ledger implications and final owner closure.
3. Each cross-owner link is idempotent and retains only the owner reference plus redacted state/hash; a retry reuses the same owner result.

### Permitting and Building Inspectorate

1. The development-approval file is registered against an authorized project and asset.
2. Building Inspectorate and technical-section handoffs remain controlled section-routing events.
3. Civil Engineering records technical review/recommendation; the shared workflow and HOD decision own its outcome.
4. A file handoff, recommendation or DMS upload is not a permit approval.

### Notification, reporting and error handling

1. Civil commits the business state and audit/revision first, then invokes the central notification owner with an idempotent correlation key.
2. Notification failure can be retried and cannot alter approval or completion status.
3. Civil reports use the shared report provider, export and report-audit pipeline. Every query re-applies tenant, permission and project scope before user date filters.
4. Expected business failures return structured Problem Details. Unexpected errors reach central exception logging and return only a friendly correlation reference.

## Recovery, cancellation and reversal

| Situation | Required action |
| --- | --- |
| Owner call times out | Query the owner by stable source/request key, link a matching completed result, then retry only if no owner result exists |
| Same request, changed payload | Reject with a conflict; do not overwrite the original request or its audit evidence |
| DMS file becomes quarantined / superseded | Block dependent Civil submit/approve/apply action and retain the immutable historical reference |
| Workflow cancels / fails | Map only the defined owner outcome; preserve an editable/controlled recovery state without pretending it was approved |
| Procurement contract/partner becomes ineligible | Block new Civil/QS handoff; retain historical snapshots and surface reconciliation exposure |
| QS/Finance certificate is rejected, voided or reversed | QS/Finance owns the correction; Civil refreshes its endorsement/reconciliation state and does not create a compensating financial record |
| Maintenance execution is cancelled or closed elsewhere | Refresh the owner reference and require a controlled Civil follow-up/reopen path where policy allows |
| Notification delivery fails | Preserve committed Civil state; retry central delivery independently with the same correlation key |

## Security and audit acceptance boundary

- Read, manage, decide, assign, external-access and export permissions are separate and cumulative with project/section/asset scope.
- Decision points recheck current actor, project membership, configured role, workflow assignment and maker-checker separation.
- Every create, update, submit, accept, return, approve, reject, handoff, export and recovery action retains tenant, actor, resource, correlation, client request/hash where applicable and redacted before/after lineage.
- `CIV-0604` owns the authenticated direct-API denial matrix: missing role, wrong project/section/asset, wrong tenant, wrong partner, stale row-version, maker-checker conflict, file access and report drilldown/export denial.

## CIV-0603 acceptance checklist

- [x] Projects, Maintenance/Assets, Workflow, DMS, QS, Finance, Building Inspectorate, contractors/consultants, notifications and reports have named owners.
- [x] Allowed Civil reads, retained references and prohibited parallel mutations are explicit.
- [x] Controlled selector, idempotency, retry, cancellation, reversal, DMS and external-party rules are defined.
- [x] Civil-to-QS/Finance, maintenance/asset, permitting and DMS flows identify the authoritative posting/approval owner.
- [ ] Authenticated API/browser evidence proves the owner boundaries, direct-API denials, retry behavior, DMS state rejection and report export/project isolation.

`CIV-0603` remains `In progress` until the final acceptance item is evidenced. It does not authorize parallel integrations in the Civil module.
