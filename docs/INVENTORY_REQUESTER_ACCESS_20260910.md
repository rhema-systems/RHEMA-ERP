# Inventory requester navigation - 10 September 2026

## Fix

`employee` already has server-authorized access to their own inventory requisitions. The Inventory sidebar required warehouse roles or Inventory read permission, so it incorrectly hid that route.

- Employee-only navigation now exposes **Inventory → My requisitions**.
- Existing Stores staff retain their full Inventory menu.
- Issue/return controls require the existing issue permission. A requester or approver is not offered the issue action on the same requisition.
- Partial-issue vouchers remain accessible for recipient acknowledgement, without querying issuer-only accounting/receiver settings.
- No new role, permission, responsibility scope, database migration or backend change was made for this fix.
- Walkthrough B17/B18 are updated in both Markdown and HTML.

## Verification

- 40 focused tests passed: navigation filtering, existing navigation behavior, issue permission/independence and voucher acknowledgement (including partial issue).
- `npx tsc --project tsconfig.inventory-requester.json --noEmit` passed. The request-dialog display-items array was explicitly typed to resolve existing optional tracking-field type errors.
- Rehearsal browser, Jane Employee: Inventory/My requisitions visible; own requisition register loads; no Issue Items or Return Items action; existing partial-issue voucher opens with retained acknowledgement.
- Rehearsal new-request form: Operations, Project Demo Warehouse, LOC-001 and PVC Pipe 50mm available. No requisition was saved, submitted, approved or issued during this check.
- Main UAT: the user signed in as Jane Employee. After the frontend switch, Inventory/My requisitions is visible, the own-requisition register loads with Issue vouchers rather than Issue Items/Return Items, and New Requisition opens. Operations, Project Demo Warehouse, LOC-001, Barcode Device Kit (21 available) and PVC Pipe 50mm (19 available) all load. The unsaved test dialog was cancelled; no UAT business record was created or changed.
- Full production build passed; page and four generated JavaScript/CSS assets returned HTTP 200 after startup warm-up.
- Main UAT partial-issue voucher `SIV-20260906-00001` also opened for the employee with the existing acknowledgement and no issuer-only fields or access error. The read-only dialog was dismissed and the requester register left open, with Inventory/My requisitions expanded.

## Deployment

Both frontends are updated. Main UAT is running the successful production build `kNvo5FeGrArFodm8gLmcv` from the regular `frontend` folder on port 3000 (PID 13628 at verification). Rehearsal remains on port 3002 (PID 50764). The original APIs on ports 5000/5002 were not restarted.

The build staging and rollback helper are local-only under `local-artifacts/stores-requester-20260910`. The old production build is retained at `frontend/.next-before-stores-requester-20260910`; nothing was deleted. Main UAT retains API target `http://localhost:5000/api`. Rehearsal retains `http://127.0.0.1:5002/api`. Neither database was changed by this frontend deployment.

This is acceptance of the requester-navigation/access correction, not re-certification of every procurement or Finance flow. The build includes the current frontend workspace; promotion/testing of other rehearsal backend features remains separate.
