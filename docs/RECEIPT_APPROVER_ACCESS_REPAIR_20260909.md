# Receipt inspection approver configuration — 9 September 2026

User authorized correcting the eligible active approver in main UAT and rehearsal. Applied at **17:43 UTC** on `RHEMA-MICHAEL\SQL2017`, separately to `RhemaERP` and `RhemaERP_PO_Rehearsal_20260909`.

## Cause and repair

The published inspection workflow requires `TDC_STORES_MANAGER`. Both databases had zero Security role members; `procurementapprover`'s old responsibility assignment was inactive and expired on 7 September at 01:00 UTC.

In each database:

- Added the existing `TDC_STORES_MANAGER` role to active DEFAULT account `procurementapprover` (`45eacad1-fff9-40f4-91fb-a0e72e2fa7f7`). Existing role-permission definitions were not changed.
- Created assignment `70b5d776-e296-458b-bd9c-ef05897f65eb`, restricted to **DEMO-PM** and its locations, active from the actual repair time. No automatic expiry was set for ongoing UAT; review/remove this assignment and the added role after demonstrations, after checking for other ongoing use.
- Preserved the old inactive assignment `ae389e55-49c8-4f39-b128-5c1dd793298d` and all its history/scopes. No dates were backdated.
- Recorded `LOCAL_UAT_STORES_APPROVER_CONFIGURED` in SecurityLogs, explicitly identifying this user-authorized local repair rather than impersonating an application user.

`manager` remains the receiver/inspection maker. The repaired approver is distinct from the PO creator, receiver and inspection maker for PO-2026-0003.

## Verification and boundaries

- Preflight passed against both allow-listed databases.
- Apply added exactly **one role membership, one assignment and one warehouse link per database**.
- An immediate second apply added **zero rows** in both databases (idempotency).
- Active tenant-role lookup, excluding initiator `manager`, returns `procurementapprover` in both databases; the new effective warehouse assignment was independently checked by the repair's postconditions.
- SHA-256 before/after checks passed for **16 business/control tables**, including PO/items, receipts, inspection cases/lines/evidence/actions, GRN/MRN documents/signatures/actions, invoices, workflow definitions/steps/instances and role-permission definitions.
- No receipt was submitted, approved, signed or posted by this repair. The existing rehearsal inspection remained Draft. No main-UAT receipt was created. No Finance code/configuration or workflow definition was edited. No server restart was needed.
- This verifies the access/configuration fix, **not a successful post-fix Submit/Approve browser test**; those business actions remain for the operator.

## Operator next steps

1. In the intended environment, as `manager`, reopen the saved receipt → **Quality Inspection** → confirm quantities and required evidence → **Submit**.
2. Confirm the inspection shows **Pending approval**.
3. Sign out/in as `procurementapprover` to load the new role. Open the same receipt → **Quality Inspection**, review, enter approval comments, then **Approve**. Do not self-approve as `manager`.

The customer walkthrough's account card and B13 steps are updated in both Markdown and HTML.

## Guarded repair scripts

`scripts/procurement/Repair-LocalUatInspectionApprover.ps1` is read-only by default; `-Apply` applies the allow-listed, transactional SQL script. It refuses unexpected users, roles, historical assignments, conflicting active scopes, maker/approver overlap or protected-data changes. Do not reuse it for production or other users/warehouses.
