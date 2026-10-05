---
integration_cycle: FIN-AUTH-AUDIT-2026-10-05
integration_status: planned
integration_decision: pending-audit
candidate_branch: codex/finance-uat-remediation-20261004
candidate_head: b9ff2e302
base_commit: 415fe5fbd
target_ref: origin/master
depends_on: FIN-UAT-2026-10-04-A
migration_status: none
verification_status: initial-evidence-confirmed
pull_request: https://github.com/rhema-systems/RHEMA-ERP/pull/352
---

# Finance transaction, approval route, role, and permission audit

## Objective and scope

Build and verify a complete Finance authorization matrix from transaction lifecycle action to API route, policy/permission, baseline role, workflow entity type, workflow stage assignment, approval workbench projection, outcome handler, and destination link. Classify each mismatch as code defect, provisioning drift, data remediation, or deliberate policy.

## Workspace and Git state

- Repository: `C:\Users\Akwas\Documents\DEV WORK\RHEMA ERP\RHEMA-ERP`
- Current evidence worktree: `.w/fin-uat-remediation-20261004`
- Current branch: `codex/finance-uat-remediation-20261004`
- Exact starting commit for this evidence pass: `415fe5fbd`
- A fresh dedicated worktree/branch should be created before broad implementation begins.

## Initial confirmed evidence

- Journal Batch: Accounts Officer is an intended first-stage reviewer. The exact UAT database grants that role `Finance.JournalBatches.Approve`, `Finance.Workflow.Approve`, and `Finance.Workflow.Reject`, yet the review action returned ACCESS_FORBIDDEN. This is not an intended role restriction.
- Recurring Journal: the workbench recognizes template, occurrence, and waiver entity keys, but the exact UAT database contains none of their workflow entity types/definitions. One template is already `PendingApproval`, so it has no workflow instance and cannot appear for any approver.
- Recurring Journal direct approve/reject routes require `Finance.JournalEntries.Approve`, while the first workflow stage includes roles that rely on generic workflow approval permissions. This is a route-to-stage authority mismatch to adjudicate.
- Exchange Rate exposed the same class of split persistence/workflow-start defect; the current atomic remediation and orphan reconciliation provide a reference pattern.

## Audit method

1. Inventory every Finance transactional controller action, service lifecycle transition, and generic approval-workbench outcome route.
2. Derive the expected route-to-permission-to-role-to-workflow-stage matrix from policy maps, role seeds/revocations, workflow definitions, and domain invariants.
3. Compare startup provisioning, existing-tenant convergence, workbench entity allowlists, detail links, outcome handlers, and maker-checker enforcement.
4. Add contract tests that fail for missing entity keys, unreachable stages, endpoint permission mismatches, and workbench/action disagreement.
5. Validate representative role journeys against the exact UAT database without mutating it unless separate authorization is granted.
6. Record each result as code defect, provisioning drift, safe data remediation, or intentional policy, with an explicit fix owner and verification path.

## Verification evidence

- Read-only SQL against `RHEMAERP_BOOKV2_UAT_20260922` confirmed the Journal Batch grants above.
- Read-only SQL against the same database confirmed ExchangeRate has one active definition, while RecurringJournalTemplate, RecurringJournalOccurrence, and RecurringJournalOccurrenceWaiver are absent.
- The database contains one recurring template with status value `1`, which maps to `PendingApproval`, and no recurring workflow instances.

## Remaining work

- Create the complete Finance authority matrix and automated contract suite.
- Reproduce Journal Batch denial with authenticated claims and workflow-step evidence.
- Implement recurring workflow critical provisioning, atomic submission, orphan reconciliation, and permission alignment.
- Audit all remaining Finance entities and lifecycle actions before declaring the matrix complete.

## Authorization boundaries

- Authorized: read-only repository/database inspection, coordination documentation, and focused local verification.
- Not yet authorized in this workstream: broad implementation, push, pull-request mutation, merge, deployment, API restart, migration application, or database/data repair.
