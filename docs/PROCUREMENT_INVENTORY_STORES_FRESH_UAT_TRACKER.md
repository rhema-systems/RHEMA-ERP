# Fresh Supplier, Procurement, Inventory and Stores UAT

## Campaign control

| Field | Value |
| --- | --- |
| Campaign | `PROC-STORE-UAT-20260905-01` |
| Environment | Local `RhemaERP`, tenant `DEFAULT` only |
| Requirements | TDC ERP Architecture and Design Document (002), existing procurement and Inventory/Stores UAT scripts |
| Scope | Fresh Goods supplier onboarding; budget and plan through sourcing, tender/bidding, award, PO/commitment, contract activation, receipt, inventory and Stores |
| Current position | R00 reset verified; U01 fresh Goods supplier onboarding is next. Fresh business tests have not started. |
| Backup | User confirmed an existing backup and explicitly declined another |
| Historical evidence | [Previous campaign](PROCUREMENT_PRE_INVENTORY_E2E_TRACKER.md); its completed observations remain historical, not acceptance of this fresh campaign |
| Configuration carry-in | Preserve users, roles, tenant mappings, published policies/workflows, document templates, accounting setup, item/warehouse masters and unrelated records |
| Additional reset scope | User explicitly included older Operations/IT procurement, related stock/Finance postings, QS transaction data and `PRJ-DEMO-*` projects |
| Exclusions | Payroll, other tenants, customer/contractor masters, non-demo projects, unrelated maintenance work orders/assets/allocations, shared configuration and physical DMS files |

## Execution tracker

| ID | Stage | Status | Record / evidence |
| --- | --- | --- | --- |
| R00 | Verify reset and preserved configuration | Passed | `EV-R00-001` through `EV-R00-004` below; committed 2026-09-05 00:54:55 UTC |
| U01 | Goods supplier contact verification, application, token/payment, documents and independent onboarding approval | Not started | New email to be supplied when needed; never record credentials or OTPs |
| U02 | Procurement budget and independent approval | Not started | |
| U03 | Market analysis, plan, approvals and publication | Not started | |
| U04 | Manual GHANEPS export, response, rejection/retry and acknowledgement | Not started | |
| U05 | Purchase requisition, evidence, validation and independent approval | Not started | |
| U06 | Sourcing release, policy-derived method and sourcing case | Not started | |
| U07 | Tender preparation, controlled document reuse, approval and publication | Not started | |
| U08 | Supplier invitation/acceptance and on-time sealed bidding | Not started | |
| U09 | Evaluation committee, member acceptance/COI; after closing, meeting, attendance, quorum and formal opening | Not started | |
| U10 | Evaluation, award verification and independent award approval | Not started | |
| U11 | Purchase order and budget commitment reconciliation | Not started | |
| U12 | Contract approval/activation | Not started | |
| U13 | Goods receipt, inspection and accepted/rejected quantities | Not started | |
| U14 | Inventory quantities, valuation, movements and accounting reconciliation | Not started | |
| U15 | Stores requisition, reservation, approval, issue, return and traceability | Not started | |
| U16 | Physical count/adjustment and closing cross-module reconciliation | Not started | |

Standard RFQ, petty purchase, QBS, QCBS and emergency routes remain separate acceptance scenarios from the original procurement plan. A passing Goods tender route must not be reported as covering those branches.

## Evidence rules

- Use the visible browser for the actual journey, supported by API/database reconciliation and relevant console/network observations.
- Retain maker/checker independence, configured TDC authority, scan safety, tenant isolation, sealed-bid/deadline rules and Finance/audit safeguards.
- Record IDs, actor display names, times, expected/actual outcomes and defects at each transition. Never store passwords, OTPs, connection strings or supplier bank credentials here.
- Stop for a genuine business decision or new destructive scope. Do not fabricate statutory dates or acceptance evidence to accelerate the UAT.
- Commit/push the completed repair or evidence slice and publish its PR before advancing to the next listed test where the delivery gate applies.

## Reset evidence

- `EV-R00-001` — The exact reviewed scope passed a rolled-back transactional rehearsal, then committed at **2026-09-05 00:54:55 UTC**: **2,883 rows across 151 tables**. This included local DEFAULT supplier applications/supplier masters, procurement transactions, related inventory/Stores and Finance postings, four `PRJ-DEMO-*` projects and their QS/project transactions. The fingerprint was `79CCAA265176D69487167C00449E8C977B8D5B4C1DE21EB1B056399FDA787C9A`. Local manifests and execution reports remain in ignored `local-artifacts/uat-reset-20260905/`; they are not committed. Recovery requires the user's existing backup.
- `EV-R00-002` — Before commit, row counts and SHA-256 content fingerprints verified preserved rows across **467 populated tables**; **1,066 originally empty tables** remained empty. All **5,585 foreign-key states** and **92 affected trigger states** matched their original enabled/trust states. Users, roles, tenant mappings, policies, workflows, templates, other tenants and unrelated data were retained. Two non-demo projects remain. Ten maintenance WorkOrder inventory allocations and maintenance stock remain; one retained maintenance asset was detached only from its deleted demo project. Physical DMS files were not deleted.
- `EV-R00-003` — Removed 10 selected journals and 20 related account transactions, and subtracted only their posted GL movements from cached account balances using the existing Finance posting convention. The unrelated PAYROLL/PayrollRun journal for **GHS 54,346.77** was retained. Cleared stock/cost caches only for `PM-BARCODE-DEVICE`, `SKU-001` and `UAT-PO-2026-0001-WIRELESS-KEYBOARD`; shared maintenance items and opening balances were retained. Restarted the existing API build with local `StartupInitialization:SeedDevelopmentData=false` (also persisted in the local development secret store, outside Git). Readiness returned HTTP 200. Post-restart SQL independently confirmed zero supplier applications, supplier masters, procurement budgets/plans/PRs/tenders/contracts/POs, demo projects and inventory movements; two non-demo projects still remain.
- `EV-R00-004` — Visible browser verification as **Procurement Officer**, tenant **DEFAULT**, on 2026-09-05 after restart: `/administration/procurement/registrations` showed **0 registrations**, `/procurement/planning/budgets` showed **0 budgets**, and `/procurement/tenders` showed **0 tenders**. Controlled template `TDC-NCT-GOODS-STD` v1 remained **Published**, bound to `TDC-F05B-NCT` v5 and its exact workflow, with current **Verified / Scan clean** evidence. The template's separate audit-history request returned a permission error because Procurement Officer lacks `procurement.audit.read`; audit-history UI acceptance is not claimed for this actor. This reset verification is not a fresh supplier/procurement end-to-end pass.

The [reset script](../scripts/uat/Invoke-DefaultTenantUatReset.ps1) is a one-off local maintenance tool, restricted to the inspected host/database and exact reviewed row-content fingerprint. It is not an application migration or an automatic startup action. No production application source or schema migration changed in this reset slice, so no backend rebuild was required.
