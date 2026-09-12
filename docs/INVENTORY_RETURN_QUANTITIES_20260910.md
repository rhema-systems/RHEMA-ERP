# Requisition issue and return quantities

## Expected result

For REQ-20260910-0002, requested and approved stay **2**, gross issued stays **2**, posted returned is **1**, and net issued is **1**. Fulfilment remains **Issued**. A return does not authorize replacement stock; that requires a new requisition.

## Implementation

- Keep the existing persisted `InventoryRequisitionItem.IssuedQuantity` as the net balance used by return eligibility, valuation and Finance. Do not rewrite original approved quantities, issue vouchers, return vouchers, stock balances or accounting entries.
- Derive effective returned quantity from non-deleted **Posted** return vouchers in the same tenant and requisition. Pending, approved-but-unposted, rejected and reversed returns are excluded.
- Gross issued = net issued + effective posted returns. Remaining approval to issue = max(0, approved - gross issued).
- Requisition detail and list reads derive fulfilment from these quantities, including records whose old saved status was downgraded after a return. Reads do not modify saved records. Pending-issue results exclude fully fulfilled requests.
- The issue service enforces gross fulfilment even if a client submits an old partially-issued status. Subsequent issues, return posts and return reversals save the gross-fulfilment status. Completed/cancelled and approval workflow statuses are preserved.
- The Items tab displays **Requested, Approved, Issued, Returned, Net issued**. Return entry continues using the net quantity available to return.
- No database schema migration or GL mapping change is needed. Main UAT remains untouched during rehearsal verification.

## Verification

- Read-only rehearsal SQL confirmed requested 2, approved 2, net issued 1; immutable SIV quantity 2; posted SRV quantity 1 at original unit cost 1,900.
- 45 focused UI tests passed with one worker after the initial parallel run hit machine-load timeouts. Tests cover the separate quantity columns and prevent returned units from appearing as remaining issue capacity.
- Scoped TypeScript check passed.
- 48 focused backend tests passed, including full and partial returns, unchanged approval/net balances, reversal, terminal states, tenant scope and pending-issue filtering.
- Rehearsal API updated (PID 59736), Core SHA-256 `49B283802B66A66409163BA21B892AF015DDA3C4A0202A8A45C93E2561D111A3`. The first startup readiness window expired and restored the backup; a guarded retry with a longer cold-start allowance passed. Browser login succeeded after the cold EF model initialization.
- Live browser requisition list now shows REQ-20260910-0002 **Issued**, with **0 Pending Issue**. Repeat read-only SQL confirmed unchanged approved/net quantities, issue voucher, posted return quantity and original posting timestamp. Legacy saved status remains untouched; the API derives the corrected read status from retained return history as described above.
- Production build `sP1QXbQtacns927Dkbqv5` passed and was loaded on 3002. Live manager-session Items-tab verification confirmed **Requested 2, Approved 2, Issued 2, Returned 1, Net issued 1**, status **Issued**. No extra issue/return/approval/post action was executed.
- Visual QA identified a cramped item-name column. The follow-up layout expands the dialog to `max-w-6xl`, retains its 680px bounded height, and gives the table/item name a minimum width with horizontal scrolling on narrow screens. All five dialog layout tests passed again. This layout-only production rebuild is in progress; the verified quantity build remains available in the guarded serving copy.
- Handoff while layout compilation continues: frontend PID 55000 serves the verified `sP1QXbQtacns927Dkbqv5` build from `local-artifacts/requisition-return-quantities-20260910/frontend-serving-layout`; API PID 59736 runs the tested quantity logic. The wider-layout build is writing `frontend-layout-build.log` (exec session 30315). Do not claim the width adjustment is live until the build exits successfully, the guarded frontend launcher switches back to `local-artifacts/rehearsal-requisition-height-20260910/frontend`, and the dialog is visually checked. Do not restart the API again for this layout-only change.
- Main UAT PIDs 13628 (3000) and 42148 (5000) were unchanged. No migration was applied. See `local-artifacts/requisition-return-quantities-20260910` for logs and rollback files.

The concise B19 check is updated in both the Markdown and HTML customer walkthroughs.
