# TDC Finance Close Templates and Manual Tasks

**Implementation date:** 2026-08-02  
**Work package:** WP3 — Finance period close, second controlled vertical slice  
**Migration:** `20260802172927_AddFinanceCloseTemplatesAndManualTasks`  
**RHEMAERP deployment:** Applied and verified on 2026-08-02

## Outcome

The period-close workspace now selects an approved, tenant-scoped template for each new month-,
quarter-, or year-end cycle. Template changes no longer require a code change and cannot rewrite a
cycle already in progress. The existing five automated Finance providers remain the only rules
path; templates control their ordering, dependencies, due dates, and approved mandatory/warning
classification.

> **Subsequent enhancement:** Control set v2 adds four providers through the same rules path:
> mandatory AP/AR control-account reconciliation and warning-level unapplied AP/AR balance review.
> See `tdc-finance-subledger-close-controls-and-baseline-seeding.md`.

TDC baseline templates are created lazily for each tenant so tenants created after a deployment
also receive complete defaults:

- `TDC-MONTH-END`, due five calendar days after period end;
- `TDC-QUARTER-END`, due ten calendar days after period end; and
- `TDC-YEAR-END`, due fifteen calendar days after period end.

All baseline automated checks are mandatory. Posting integrity, trial-balance integrity, and the
`FIN-LIM-0034` depreciation control cannot be downgraded by template configuration. The audited
tenant depreciation setting still controls whether the depreciation provider is applicable.

## Version and approval controls

- An approved template is immutable. A changed procedure creates a new version under the same
  template code.
- Only one approved version is active per tenant and close type.
- A draft author cannot approve the same version. A second Finance administrator must record a
  declaration of at least 20 characters.
- Activation supersedes, but does not delete, the prior active version.
- Task codes and sequence values are unique within a version; every dependency must exist and the
  dependency graph must be acyclic.
- Automated configuration accepts only the existing provider codes. It cannot name or execute an
  arbitrary service.
- Every approved version must contain each supported automated provider exactly once and must
  retain the mandatory manual `PREPARER_CERTIFICATION` task.

## Cycle evidence and manual tasks

When a numbered cycle starts, it stores the template ID, code, close type, and version, then copies
every definition into `FinanceCloseTask`. Later template activation affects new cycles only.

Configured manual tasks may be self-assigned or assigned to a specified user, given a due date,
and completed with an evidence summary of at least 20 characters. Completion requires any declared
dependency to be complete and is immutable. Mandatory manual tasks contribute to the workspace
blocker count and prevent preparation until completed. The certification task and automated tasks
remain owned by their existing dedicated operations.

This slice originally stored a controlled evidence summary/reference. The fifth WP3 slice now adds
controlled binary task evidence and evidence-fingerprinted, different-user exception waivers; see
`docs/Finance/tdc-finance-close-evidence-and-exception-waivers.md`.

## API and UI

- `GET /api/finance/close-templates`
- `POST /api/finance/close-templates/versions`
- `PUT /api/finance/close-templates/{id}`
- `POST /api/finance/close-templates/{id}/approve`
- `PUT /api/finance/periods/{periodId}/close-workspace/tasks/{taskId}`

Template reads require `Finance.Read`; draft creation/update/approval requires `Finance.Admin`;
manual cycle tasks and evidence require `Finance.PeriodClose.Workspace.Maintain`.

The administration screen is `/administration/finance/close-templates`. The fiscal-period screen
links administrators to it, and the close workspace displays the selected template plus manual task
due state and evidence-completion actions.

## Verification

- API build succeeds with no errors.
- Ten focused period-close tests pass, including baseline seeding, cycle version copying,
  maker-checker activation, manual evidence blocking/completion, and non-waivable depreciation.
- Sixty-five Finance controller security tests pass.
- EF reports no model changes after the migration.
- RHEMAERP contains both template tables and all cycle linkage columns, and has zero pending
  migrations after applying migration `20260802172927_AddFinanceCloseTemplatesAndManualTasks`.
- Frontend type checking reaches only the two pre-existing Syncfusion PDF-viewer module-resolution
  errors and reports no error in this slice.

## Remaining WP3 work

The identified Finance-only provider set, binary task evidence, approved exception waivers, and
aging/escalation delivery are implemented. See
`docs/Finance/tdc-finance-close-aging-and-escalations.md`. Remaining work is printable close packs
and higher-tier reopen approval.
