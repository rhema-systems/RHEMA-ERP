# Partner and item posting defaults

Baseline posting defaults were deployed to UAT and rehearsal on 13 September 2026; the historical checks below remain unchanged. The new receipt editor, invoice Distribution, transaction WHT choices and item-account lookup correction are candidate changes, not yet deployed or browser-accepted. Do not create the fresh rehearsal copy until acceptance is complete.

## Business partner

1. Open Procurement → Business Partners → Edit.
2. Details: set **Subject To Withholding Deduction**, select the purchase **WHT Configuration**, enter the supplier's default **WHT Rate**, and select the standard **Tax** schedule if applicable.
3. Options: select payment terms and ChequeBook ID; enter TIN and credit limit.
4. Accounts: select the required GL accounts. Blank mappings retain the existing Finance fallback where one exists.
5. Save. If the existing master-data approval process is active, complete that existing process.

New POs capture account, payment and standard-tax defaults. Editing the supplier master does not rewrite those snapshots or posted journals. The invoice WHT offer uses the current supplier's eligibility, configured rule and default rate; the invoice's saved decision and rate are retained on later edits.

## Inventory item

1. Open Inventory → Items → Edit → Accounts.
2. Load the current tenant's GL accounts, set the required mappings and save. The candidate lookup recognises SuperAdmin and authorised internal users; it still excludes external users and other tenants' accounts.
3. Leave unused purposes blank.

Mappings affect future postings. They do not move existing stock value between GL accounts or rewrite previous journals. Existing balances requiring reclassification must be handled separately.

Current integration scope: inventory control for receipts, landed costs, issues, adjustments, supplier-return dispatch and direct AP inventory lines; purchase-price variance for receipts/landed costs; consumption expense for In Use/In Service; damaged/variance expense for adjustments. Stores returns restore the original issued GL accounts. Supplier-return credit and retry processing uses the original dispatch journal accounts.

The remaining account slots are persisted for their named accounting operations; this change does not claim end-to-end verification of sales, assembly, drop-shipping, or standard-cost revaluation.

## Receipt Distribution

1. Open an unposted Purchase Receipt and select **Distribution** beside the tabs. The button is available on every receipt tab.
2. Change an account using its searchable selector. Use **Split line**, **Add line** and the trash icon as needed.
3. Keep the net amount for each item/posting type unchanged and total debits equal to total credits. Select **Save**, then reopen to check the saved lines.
4. Use **Full page** and **Restore** for analysis. **Reset defaults** replaces the draft overrides after confirmation. Closing with unsaved edits asks before discarding them.
5. Post through the normal receipt process. Final posting uses accepted quantities and actual valuation movements; changed source quantities/types may require distribution review. Landed costs post separately.
6. After posting, Distribution shows the original journal and amounts, read-only.

Overrides apply to this receipt only. They do not change the item master. Later stock postings continue to use item defaults and the existing original-journal clearing/reversal rules.

Account precedence for receipt postings:

| Purpose | First | Next | Fallback |
| --- | --- | --- | --- |
| Inventory debit | Item Inventory | — | Finance Inventory control |
| Accrual credit | Captured PO supplier Accrued Purchases; live supplier only for legacy POs without a snapshot | Item Inventory Offset | Finance GRV accrual |
| Purchase-price variance | Item Purchase Price Variance | PO supplier Purchase Price Variance | Finance write-off expense |

AP invoices linked to receipts clear the original posted receipt accrual account(s). They must not choose a different accrual account merely because master defaults changed later.

For partial invoices, only the remaining uncleared receipt balances determine the account split. Posted or reversed historical journals are never rewritten. If older records contain multiple accounts without enough item lineage to identify the correct clearing, the system reports that ambiguity instead of guessing.

Foreign-currency invoices split across several accrual accounts still require the existing per-line exchange-rate and journal-balance checks to pass. A difference caused by FX rounding is not silently charged to an arbitrary account; this release does not introduce a new FX-rounding policy.

The inventory-receipt GRV route does not yet support an invoice mixing received stock and non-stock/service PO lines. It reports the unsupported line rather than assigning its charge to an unrelated stock receipt account.

## Invoice Distribution and withholding

1. Create an invoice for a WHT-enabled supplier. At **Apply withholding to this invoice?**, choose **Yes** or **No** before WHT is applied.
2. **Yes** fills the supplier's configuration and rate. Check the WHT deduction and Net Payable. The invoice's **Subject to withholding** toggle and **WHT Rate (%)** are editable; zero is a valid rate override.
3. **No** leaves WHT off for this invoice without changing the supplier. Changing supplier requires a fresh decision. Reopening a saved draft retains its decision and rate.
4. Auto-created landed-cost invoices may remain drafts with a pending WHT decision. Open **Edit invoice**, answer Yes/No, and save before submitting/posting the invoice. Receipt/landed-cost inventory posting is not blocked by the pending draft decision.
5. On the saved invoice, select **Distribution** to view the proposed entries or original posted journal. Use **Full page** and **Restore** as needed. This view is read-only; change permitted draft values through **Edit invoice**.
6. Verify WHT once at payment. The invoice journal records gross AP; WHT GL recognition occurs at payment, using the saved invoice choice/rate and applicable payment rules.

## Candidate checks — not deployed or browser-accepted

- Backend tests: 249 passed (196 API, 53 Core).
- Frontend tests: 111 passed across 21 files. Focused TypeScript, ESLint and diff checks passed.
- Candidate packages, migrations and authenticated browser/posting acceptance remain pending. These test results do not replace the historical deployed/browser evidence below.

## Verified baseline deployment — 13 September 2026

- Backend build: passed, zero errors.
- Backend tests: 181 passed (130 API, 51 Core), none failed or skipped.
- Frontend tests: 83 passed across 17 files; focused TypeScript and ESLint checks passed.
- Both staged API packages match the tested assemblies.
- Both production frontend builds passed and were deployed with the matching tested API packages.
- Both databases now have 578 migration-history entries. The two additive migrations passed rollback dry runs, fresh COPY_ONLY/CHECKSUM backups and RESTORE VERIFYONLY before application. Original business rows, including rowversion values, were unchanged by the migrations.
- Both APIs passed readiness and liveness checks. Four frontend routes and 67 JavaScript/CSS assets per copy returned successfully from the new builds.
- Visible rehearsal browser: sign-in, partner Details/Options/Accounts, WHT rate enable/disable, ChequeBook choices and Accounts Payable lookup verified without saving changes.
- Receipt REC260003 Distribution opened under Procurement Officer and displayed its original posted journal: Inventory `001-000-1200` debit GHS 52,000; GRV Accrual Control `000-2110-0000` credit GHS 52,000. Full page and Restore controls were checked visually.
- Visible browser save/reload and transaction-posting checks remain pending. Component tests and HTTP route checks are not full browser acceptance evidence.

## Running copies

| Copy | Frontend | API | Build ID |
| --- | --- | --- | --- |
| UAT | http://localhost:3000 | http://localhost:5000 | `1vsjA3I8R25gD4TfOxVaV` |
| Rehearsal | http://127.0.0.1:3002 | http://127.0.0.1:5002 | `Vnd_6Tthqp-wXm-TUKAVk` |

The first cold-start checks timed out while the application initialized; subsequent readiness, liveness, page and asset checks passed. Rehearsal sign-in was renewed after restart. No business transaction was saved, approved or posted during restart verification.

Deployment proof: `local-artifacts/posting-defaults-{main|rehearsal}-20260913/runtime-verification.json` and the per-database `posting-defaults-*-apply-20260913.json` files. Backups remain in the SQL Server Backup directory; no database was discarded or restored.

## Release gates still required

- Walk through partner/item save and reload, PO defaults, receipt Distribution and AP defaults in the visible rehearsal browser.
- Confirm account-specific postings and original-account clearing/reversal using journals and stock reconciliation.
- Only then take the UAT backup and create the fresh rehearsal copy.

Migrations: `20260913030000_BusinessPartnerPostingDefaults` and `20260913030500_InventoryItemPostingAccounts`. Both use inline EF migration metadata; no duplicate registry attributes should be added.
