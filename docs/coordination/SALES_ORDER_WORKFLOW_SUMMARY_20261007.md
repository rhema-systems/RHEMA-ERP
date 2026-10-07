# Sales Order Workflow Summary Error Remediation

## Objective

Remove the persistent generic error and `Workflow status unavailable` state on the Sales Order details page, first reported for `SO-000009`, and prevent the same workflow-summary failure for tenants that retain legacy workflow entity aliases.

## Source and ownership

- Workstream type: Sales Order / shared Workflow (outside Finance coordination)
- Branch: `codex/fix-sales-order-workflow-summary`
- Isolated worktree: `D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\.worktrees\sales-order-workflow-summary`
- Exact base: `22fd949a26e5bbe39bbee4af09a64ab0c6f3c7e3` (`origin/master`, PR #370 merge)
- Implementation commit: `555cc4bf3b8` (`Fix Sales Order workflow summary lookup`)

## Verified request and failure path

1. `frontend/src/app/sales/orders/[id]/page.tsx` owns the Sales Order workflow summary through `useWorkflowRecord`, which calls `GET /api/workflow/entity-summary?entityType=SalesOrder&entityId=<order-id>`.
2. The page passes `loadWorkflowSummary: false` to both shared workflow components. `WorkflowApprovalHistoryPanel` declared that prop but ignored it, so the mounted history panel issued a second identical entity-summary request plus `GET /api/workflow/entity-audit?...`. This accounts for separate reference IDs in the header and history area and allowed concurrent summary reads to enter workflow self-healing code.
3. `WorkflowController.GetEntitySummary` resolves the page alias and then calls workflow services with the configured canonical code. After PR #370, a tenant with both legacy `SalesOrder` spelling and catalog `SALES_ORDER` spelling could resolve the first alias, then fail on the canonical lookup: `WorkflowEntityTypeRepository.GetByNameAsync` checked exact Name but not exact Code before its normalized ambiguity guard.
4. The entity-type seeder matched only literal names/codes. It could therefore create a catalog row alongside a normalized legacy alias and produce the ambiguity above.

## Implemented changes

1. `WorkflowApprovalHistoryPanel` now honors `loadWorkflowSummary`. When the page owns the summary, the panel loads audit history only, consumes the supplied summary state, and does not reload audit history when that supplied summary arrives.
2. `WorkflowEntityTypeRepository.GetByNameAsync` now resolves an exact configured Code after exact Name and before normalized alias fallback. Canonical `SALES_ORDER` therefore selects its row even when a legacy normalized alias is also present.
3. Workflow entity-type seeding now uses the controller's normalized entity matching so future seed runs reuse a compatible legacy row instead of creating another normalized duplicate.
4. Added focused frontend coverage for external summary ownership and standalone panel summary loading.
5. Added focused backend coverage proving exact canonical-code resolution wins in the presence of a legacy normalized alias collision. The earlier PR #370 controller test remains green.

## Migration and application status

- No database migration is required.
- No database rows were changed.
- No deployment, service restart, push, pull request, or merge was performed.
- The local `RhemaERP` database contains no `SO-000009` record and no Sales Order workflow instance, so it cannot reproduce the deployed record.

## Verification evidence

- Frontend focused Vitest: `2/2` passed for `WorkflowApprovalHistoryPanel.test.tsx`.
- Frontend TypeScript: `tsc --noEmit --incremental false` passed.
- Targeted ESLint: passed for the changed workflow component and test.
- Backend focused xUnit: `2/2` passed for `WorkflowEntitySummaryTests`, including the new real-repository alias collision case and the existing controller canonical-code case.
- Shared/Core/Data/API/API-test projects compiled through the focused backend test with existing repository warnings only.
- `git diff --check`: passed before ledger creation and must be repeated before handoff.

## Known constraints

- The visible browser-control bridge crashed twice during initialization, so an authenticated UI/network replay of `SO-000009` could not be captured from this task.
- Read-only SSH access to the test VPS was attempted to correlate the supplied reference IDs, but the available environment had no authorized key.
- The supplied screenshot and source trace identify the duplicate frontend request and the deterministic backend alias-collision path. Live deployment verification remains necessary because deployed logs and the deployed database were unavailable.

## Remaining work

1. Integrate the isolated commit into the parent branch.
2. Deploy through the established VPS release process.
3. Reopen `SO-000009` in the authenticated visible browser and verify one entity-summary request, one entity-audit request when history is shown, no generic banner, and an available workflow status.
4. If the deployed request still fails, correlate its new reference ID in the VPS API log before changing another code path.

## Authorization boundaries

- This workstream is limited to Sales Order workflow-summary lookup, shared workflow request ownership, focused tests, and this non-Finance ledger.
- Existing workflow authorization and tenant scoping remain in force.
- No destructive database reconciliation for duplicate entity-type rows was added.
- Do not push, open, or merge a pull request from this worktree; the parent task owns integration.
