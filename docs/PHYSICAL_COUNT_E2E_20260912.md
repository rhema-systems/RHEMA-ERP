# Physical-count rehearsal verification — 12 September 2026

**Result: PC-20260911-0004 and ADJ260001 posted successfully through the live rehearsal UI. All 52 final database assertions passed. Main UAT counts remain unchanged.**

## Scope and preservation

Rehearsal: `RhemaERP_PO_Rehearsal_20260909`, port 3002. Main UAT: `RhemaERP`, ports 3000/5000 remain stopped. Both use the local `RHEMA-MICHAEL\SQL2017` instance and tenant `00000000-0000-0000-0000-000000000001`.

The user authorized approval and posting of rehearsal **PC-20260911-0004** only. No main-UAT count has been submitted, approved or posted in this run.

## Completed setup

- Main UAT's four pending migrations were applied and independently verified: **462 applied, none pending**. The guarded migration helper preserved its 50-table operational fingerprints.
- Six legacy item aggregate totals were reconciled in each database to that database's own warehouse records, with six audit entries per copy. No rehearsal quantities were copied into UAT. Main UAT stock movements, bin balances, valuation records, counts and journals were preserved.
- Superseded rehearsal **PC-20260910-0001** and **PC-20260911-0002** were cancelled through the normal API with the user's authorization. Their saved lines and history remain.
- Through the normal administrator APIs, both databases received dedicated **000-6800-0000 — Stock Count Losses** and **000-4940-0000 — Stock Count Gains**, and only the two corresponding Finance Settings mappings were changed.
- `financereviewer` retains `TDC_FINANCE_REVIEWER`; `employee` additionally has `TDC_INTERNAL_AUDIT`. New active responsibilities in both copies are restricted to **Project Demo Warehouse**, covering all its locations. Existing security roles, profiles, passwords and prior assignment history were preserved.
- Finance setup compared exact before/after fingerprints for counts, stock, movements, adjustments, journals and migrations; all were unchanged.

Verified COPY_ONLY setup backups:

| Database | Backup filename in the SQL instance's Backup directory |
| --- | --- |
| Rehearsal | `RhemaERP_PO_Rehearsal_20260909_before_count_finance_20260912_021221_635.bak` |
| Main UAT | `RhemaERP_before_count_finance_20260912_021846_344.bak` |

## Live browser results

Target count: `cf9c91fc-95be-4ce2-b56e-ae27932a89a8`; adjustment **ADJ260001**, `44c7b4aa-21cd-4a21-8879-eb7174c209e0`.

1. `procurementapprover` opened the saved count, loaded its decision options and saved **Approve adjustment**. Status advanced to **Finance Approval**.
2. A fresh `financereviewer` sign-in opened the same count and saved **Approve adjustment**. Status advanced to **Audit Attestation**.
3. A fresh `employee` sign-in opened the same count and saved the separate Audit decision. Status advanced to **Ready to Post**.
4. `financereviewer` selected **Post**. The first attempt returned HTTP 403 in inventory tracking: adjustment-in/out directions incorrectly fell back to the requester permission instead of the posting approver permission. The enclosing transaction rolled back: count stayed **Ready to Post**, adjustment stayed **Approved**, and **zero** related movements, posting events, journals or posted actions were retained. This attempt is **not** a successful posting result.
5. The retry on the corrected tracking build passed authorization but returned `INV_COUNT_WAREHOUSE_FROZEN`. Quantity updates and movement inserts had been staged in one EF batch; the movement insert could run first and invalidate the freeze guard's approved-adjustment allowance. That retry also rolled back completely: count/adjustment stayed Ready to Post/Approved, with zero posting events, journals, movements or posted actions and all 13 preservation checks passing at that point.
6. After the ordering correction passed its regression tests and was deployed, a fresh `financereviewer` login opened the same saved count and selected **Post**. The browser showed **Posted**, removed the Post action, and retained the separate Stores, Finance, Audit and posting events in descending Control history. The final read-only probe passed **52/52 assertions** with zero failures.

All nine original saved counted quantities and current clean count-sheet evidence remained intact. The read-only probe passed all 13 pre-post preservation checks.

The tracking permission mapping has since been corrected and deployed to the rehearsal API. Its production callers were audited: adjustment-in/out validation is used by posting/reversal, not draft entry. Regression results: **27/27 tracking tests**, **91/91 posting-eligibility and count-lifecycle tests**, and **64/64 UI tests** passed. The API build completed with zero errors. The updated UI uses server `CanPost` eligibility and the existing Procurement currency projection; no broad Finance permission was granted and no new migration was required.

A fresh `employee` Audit login on the updated build opened PC4 at Ready to Post and correctly displayed **no Post button**. The approving Finance reviewer had the Post action on the same build.

The freeze-ordering correction flushes each approved line's balance changes inside the existing transaction and defers all stock-movement inserts until every line succeeds. This also covers the same item in multiple bins. The database freeze and negative-stock guards remain enabled; the independently reviewed private-method change requires no schema migration or new public API contract. Its **63/63 ordering and lifecycle tests** passed. The initial new-test failures were missing fixture audit identity, corrected without relaxing audit validation.

## Verified posting outcome

- Count posted and freeze released at **2026-09-12 03:14:56 UTC** by the same independent Finance reviewer who approved it.
- Exactly **one** count post, adjustment post, Finance posting event and journal; **eight** exact-bin stock movements for the eight nonzero variances. The zero-variance ninth line remains intact.
- Journal ID: `a93039d7-2055-4c01-b328-928513a3bc50`; Finance event: `c16411e0-4dda-480d-ab7a-6aac2f936c85`.
- Journal debits and credits each total **GHS 850,444.23**. Net inventory increase is **GHS 846,524.23**: gains **GHS 848,484.23**, losses **GHS 1,960.00**.
- GL accounts: existing **1200 — Inventory**, **000-6800-0000 — Stock Count Losses**, and **000-4940-0000 — Stock Count Gains**.
- All nine saved counted quantities, exact-bin closing values, warehouse totals, global owned-stock totals, original snapshots and current clean evidence reconcile. No unexplained null-location valuation balance remains.
- Read-only evidence: `local-artifacts/physical-count-e2e-verification-opening.json` and `local-artifacts/physical-count-e2e-verification-posted.json`. Recheck with `local-artifacts/Test-PhysicalCountE2EResult.ps1 -ExpectPosted`.

## Runtime and main-UAT handoff

- Rehearsal frontend build **xQXJmmLr6G6T5uncCKYUW** runs on 3002 and targets API 5002.
- Main frontend clean build **g3LhtOLB-tJ97xpo4bIYN** passed, targets `localhost:5000/api`, and contains no rehearsal API references. Its incompatible earlier cache and prior output remain preserved.
- The regular API's Core/Data/Shared DLL/PDB dependencies were synchronized to the tested binaries, with previous files backed up at `C:/Users/micha/AppData/Local/Temp/tdc-main-api-dependencies-before-post-20260912-031413-c72ab179`. Its API DLL/PDB and configuration were unchanged.
- Main ports 3000/5000 remain stopped. Code, migrations and authorized setup are ready there; **main-UAT live browser acceptance has not been run**, and no main count was submitted, approved or posted.

Compiled/runtime DLL SHA256:

- API: `12BE246D876C0B450DA30BBADFC1C32BD8C61FE40AC26254793ED00E9C2737EC`
- Core: `ED74490F38987395E8F80023A8D451A7B8CCA6BF854A8733181BE62BF61C56D7`
- Data: `3F830F26968D15E4B97AC8E56199245D4F564308A92FB365BA9198F47A29F2C2`
- Shared: `06F41E8346E665B63DA1F42291AA7945009503EC17E139E3B5869857907EDAF8`
