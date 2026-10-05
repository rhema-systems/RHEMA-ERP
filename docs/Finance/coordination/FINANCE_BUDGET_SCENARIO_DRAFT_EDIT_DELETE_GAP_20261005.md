# Finance budgeting scenario and return-management gaps - 2026-10-05

## Objective and scope

Assess safe edit/delete management for genuinely unused Draft budget scenarios and reconcile the Budget Return training flow with the current scenario-control-dimension implementation.

## Branch and worktree

- Branch: `codex/finance-uat-remediation-20261004`
- Worktree: `.w/fin-uat-remediation-20261004`
- Investigation starting commit: `e1c25df65`
- Pull request: no PR creation, update, or push is authorized for this workstream yet.

## Product decisions and evidence

- A Draft scenario should be editable while it has no budget returns, lines, allocations, workflow instances, approvals, derived versions, or other dependent evidence.
- An unused Draft should be deletable under the same dependency rule, using a server-authoritative atomic eligibility check and an audited soft delete/tombstone rather than client-only hiding or physical erasure.
- Once dependent evidence exists, structural fields such as fiscal year, currency, and dimension structure must lock.
- The current Create Budget Return dialog exposes `Worksheet distribution dimension` and then a value from that scenario control dimension.
- The current client does not expose `segmentValueId`, although that optional property remains in the client/server DTOs, entity, duplicate key, reporting, and budget-control logic.
- Therefore the training instruction to select `Segment Type COMPANY` and then `Department / Unit DEFAULT` describes two selections that the current UI cannot reproduce. A scenario configured with the DEPARTMENT control dimension can select its DEFAULT value, but cannot also select COMPANY as a separate segment value.
- Classification: confirmed training/product contract drift. A product decision is required before implementation: retire the legacy segment axis and update the guide, or restore an explicit governed segment selector and define its relationship to Finance dimensions.

## Changed files

- This coordination ledger only. No budgeting product code has changed.

## Migrations and application state

- Unknown until the edit/delete and segment/dimension product decisions are implemented.
- No migration has been created or applied.
- No UAT data has been changed.

## Verification evidence

- Inspected `CreateBudgetReturnDto` in client and server contracts.
- Inspected the Create Budget Return dialog in `frontend/src/app/finance/budgeting/scenarios/[id]/page.tsx`.
- Inspected `BudgetService.CreateReturnAsync` validation and persistence behavior.
- Inspected budget hardening tests proving the current required distribution control is a scenario Finance dimension value.

## Remaining work

- Inspect scenario update/delete routes, permissions, entity dependencies, and UI actions.
- Decide whether `SegmentValueId` is an active business axis or deprecated legacy residue.
- Align the training guide and UI/API contract to the chosen model.
- Add backend authorization/dependency tests and frontend visibility/selection tests before implementation.

## Authorization boundaries

- Read-only investigation and documentation are authorized.
- The earlier request to implement Draft edit/delete authorizes local implementation and tests once the dependency contract is confirmed.
- Do not push, create or update a PR, deploy, restart services, apply a migration, or mutate UAT data without separate authorization.
