# Stores requisition form — 10 September 2026

## Changes

- Removed the manual Cost Center field from the requisition Details tab.
- On creation, the server resolves the selected active, tenant-owned department and assigns its accounting code, falling back to its department code. This follows the existing plan-linked procurement requisition rule; it does not choose a GL account or change Finance configuration.
- A department without either code produces a clear save error before a new requisition is persisted, unless the requisition has a project cost object.
- Existing stored cost centres survive unrelated draft edits. A department change or a legacy blank draft triggers automatic resolution. Approved records and their history are not rewritten.
- Items → Add Item now uses a searchable popover, with item name, code, available quantity and location. Search lives inside the dropdown. Selection uses the full scoped item list, not the current search results.
- Warehouse/location changes reset the item selection. Requisition save errors retain the server's useful message.
- Walkthrough B17 has the direct select/search/quantity/Add/Save/Submit sequence.
- Requisition dialogs now keep Details, Items and Workflow in the same 680 px frame (limited to 90% of the available viewport height). Only the active panel scrolls; the title, tabs and footer remain fixed, including when Add Item expands. Create and view dialogs share the same layout.

## Validation

- Backend Core and Data compiled; 15 InventoryRequisitionDraftTests passed, including account-code precedence, department-code fallback, invalid/inactive/cross-tenant departments, missing setup, existing-value preservation, changed-department re-derivation and approved-history preservation.
- Four searchable-dropdown tests passed (name/code search, selection, correct empty state and loading).
- Four dialog layout tests passed: edit/view tab switching, expanded Add Item and the two-tab create dialog.
- Live rehearsal browser check on `REQ-20260910-0001`: Details, Items and Workflow each measured 680 px high, with a 470 px content panel and Save at the same vertical position (735 px in the 912 px-high viewport). Expanding Add Item produced 629 px of content; scrolling moved the panel by 159 px while the dialog remained stationary and Save stayed visible. No Save, Add, Submit, approval or other business-data action was invoked during this check.
- Existing requester access and issue-dialog tests: 25 passed.
- Focused frontend TypeScript check passed: `npx tsc --project tsconfig.inventory-requester.json --noEmit`.
- Read-only SQL checks found active Operations with department code `OPS` and blank AccountCode in both `RhemaERP` and `RhemaERP_PO_Rehearsal_20260909`. New requests therefore resolve to `OPS`; historical `Operations` values are preserved.
- No migration, role change, Finance code edit or UAT/rehearsal business-data write was performed for this change.

## Rollout status

Rehearsal is deployed on **http://127.0.0.1:3002** in production (`next start`) mode. The current build is `UEy2n_T7xlhn5VIjIh7CQ`, served by frontend PID 63280 at verification from `local-artifacts/rehearsal-requisition-height-20260910/frontend`. It retains the fixed-height dialog and issue-time/default location, quantity-fill helper, advanced tracking accordion and automatic requester/receiver changes documented in `INVENTORY_ISSUE_LOCATION_20260910.md`. It also includes optional return details/comments and the approver-only return review documented in `INVENTORY_RETURN_UX_20260910.md`. The API is now PID 7148 under `local-artifacts/rehearsal-production-20260910/api`, restarted to load the tested return validation change. It still uses `RhemaERP_PO_Rehearsal_20260909`, the existing rehearsal-only upload paths, disabled background/outbound integrations and the existing browser isolation guard. Main UAT was not restarted.

During the original fixed-height validation, the page and all 12 referenced stylesheets plus two JavaScript assets returned HTTP 200. Jane Employee's existing session survived the frontend restart, and all three tabs were checked in the visible rehearsal browser. No business record was created or changed during that layout validation. The subsequent authorized two-unit issue is recorded separately in `INVENTORY_ISSUE_LOCATION_20260910.md`. `Start-LocalPoRehearsalProduction.ps1` selects the updated build by default and accepts the previous prepared frontend path for rollback.

Main UAT remains unchanged: frontend PID 13628 on 3000 and API PID 42148 on 5000. The new requisition change is **not yet deployed to main UAT**.

Deploy the matching backend and frontend together to main UAT before using the revised walkthrough there. Do not copy only the new requisition form into a frontend backed by the old API: that API still relies on client-supplied cost-centre text. Verify the selected release's existing migrations and other pending changes separately; this form change introduces no migration.

After deployment, use employee → Inventory → My requisitions → New Requisition. Confirm no manual cost-centre input, select Operations/DEMO-PM/LOC-001, then Items → Add Item → search PVC by name and code. In rehearsal, save a clearly labelled test draft and verify its stored cost centre is OPS. Do not create or issue test stock transactions in the main UAT data without agreement.
