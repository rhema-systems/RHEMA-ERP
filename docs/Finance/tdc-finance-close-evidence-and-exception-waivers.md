# TDC Finance Close Evidence and Exception Waivers

**Date:** 2026-08-03  
**Work package:** WP3 — Finance period close, fifth controlled vertical slice  
**Migration:** `20260803075605_AddFinanceCloseEvidenceAndWaivers`  
**Database status:** Applied to `RHEMAERP`; 206 migrations recorded

## Outcome

The existing numbered close cycle now retains controlled file evidence for automated and manual
tasks and supports a deliberately narrow maker-checker waiver for eligible close exceptions. This
extends the existing Fiscal Period aggregate and shared file-upload service; it does not create a
parallel close, storage, or generic workflow mechanism.

## Evidence control

- Files first pass through the shared controlled-upload boundary under category
  `finance-close-evidence`. Tenant upload policy, extension/MIME checks, size limits, SHA-256
  checksum, storage metadata, and mandatory malware scanning therefore remain centralized.
- `FinanceCloseEvidenceAttachment` links the resulting `FileUploadRecord` to one task and numbered
  cycle with an evidence type and description.
- Supported evidence types are `SupportingDocument`, `Reconciliation`, and `ManagementApproval`.
- Cross-tenant files, deleted files, wrong-category files, infected/error/pending scans, files over
  20 MB, and duplicate task/file links are rejected by the Finance service.
- A completed manual task's evidence is immutable. Evidence cited by any waiver request is also
  immutable. Removing an unused link does not delete the shared file record.
- The shared file deletion service rejects deletion while an active close-evidence link exists.
- Public/signed evidence URLs are resolved through the configured storage provider when the
  workspace is loaded; URL resolution failure does not hide the retained file metadata.

## Exception-waiver control

The following sequence is mandatory:

1. Run the latest close evaluation.
2. Attach supporting evidence to the automated task that owns the failed or warning check.
3. A user with `Finance.PeriodClose.Waivers.Request` submits at least 50 characters explaining the
   business need and mitigating control.
4. A different user with `Finance.PeriodClose.Waivers.Approve` records an approval or rejection and
   a review comment of at least 20 characters.
5. Approval immediately re-runs the complete close evaluation. Only an exact evidence-fingerprint
   match changes the effective result to `Waived`.

Each snapshot stores a SHA-256 fingerprint over the check code, template severity, pre-waiver
status, result summary, invariant exception count/amount, and provider evidence JSON. A changed
count, amount, message, or evidence payload produces a different fingerprint and restores the
check to `Failed` or `Warning`. The earlier approval remains visible for audit but is not applied.

One waiver request is allowed per immutable snapshot. A rejected request can be reconsidered only
after a new evaluation creates a new snapshot. An approved waiver is cycle-specific and cannot
cross tenant, period, task, or close-cycle boundaries.

## Controls that remain non-waivable

The service rejects waiver requests for:

- `POSTING_INTEGRITY`
- `TRIAL_BALANCE`
- `AP_CONTROL_RECONCILIATION`
- `AR_CONTROL_RECONCILIATION`
- `RECURRING_JOURNAL_EXCEPTIONS`
- `FIXED_ASSET_DEPRECIATION`

This preserves the `FIN-LIM-0034` resolution. Missing depreciation, failed runs, and invalid
same-tenant journal/posting-event evidence still require correction; neither template
configuration nor the new waiver workflow can downgrade that blocker.

## Permissions and TDC default role grants

- `Finance.PeriodClose.Workspace.Maintain`: Accounts Officer, Senior Accountant, Finance Manager,
  Chief Accountant, Financial Controller, Tenant Administrator, and Super Administrator.
- `Finance.PeriodClose.Waivers.Request`: Accounts Officer, Senior Accountant, Finance Manager,
  Financial Controller, Tenant Administrator, and Super Administrator.
- `Finance.PeriodClose.Waivers.Approve`: Finance Manager, Chief Accountant, Financial Controller,
  Tenant Administrator, and Super Administrator.
- `Finance.PeriodClose` remains the separate final-close authority. Workspace access does not
  implicitly grant final closure.

The domain service enforces different-user review even when a role has both request and approval
permissions.

## API and UI

- `POST /api/finance/periods/{periodId}/close-workspace/tasks/{taskId}/evidence`
- `DELETE /api/finance/periods/{periodId}/close-workspace/tasks/{taskId}/evidence/{attachmentId}`
- `POST /api/finance/periods/{periodId}/close-workspace/checks/{snapshotId}/waivers`
- `POST /api/finance/periods/{periodId}/close-workspace/waivers/{waiverId}/review`

The fiscal-period close dialog now uploads/displays/removes task evidence, collects waiver
justification, shows pending decisions, permits approval/rejection, displays applied waivers, and
marks a server-confirmed match as `Waived`. The client never computes fingerprints or turns a
failure into a pass.

## Audit and verification

Audit events cover evidence link/removal, waiver request, approval/rejection, and application to a
matching evaluation. Focused tests cover tenant isolation, non-waivable controls, evidence
retention, maker-checker, approval application, and automatic invalidation when the exception
changes.

- 103 focused close, controlled-upload, and Finance-controller security tests pass.
- API/solution builds complete with zero errors.
- Frontend ESLint passes for all changed Finance close files.
- Frontend type checking reports only the two pre-existing Syncfusion PDF-viewer module-resolution
  errors and no error in this slice.
- EF reports no model changes after the migration.
- `RHEMAERP` contains both new tables and all three new permissions with the documented grants.
- Pre-change backup:
  `RHEMAERP_PreCloseEvidenceWaivers_20260803_080329.bak`.

## Remaining WP3 work

The sixth slice now implements notification/escalation delivery for aged tasks, waiver reviews,
and prepared close cycles. See `docs/Finance/tdc-finance-close-aging-and-escalations.md`.
Signed/printable close packs and higher-tier reopen approval remain. The identified Finance-only
automated provider set, templates, task evidence, controlled exception-waiver foundation, and
aging/escalation delivery are implemented.
