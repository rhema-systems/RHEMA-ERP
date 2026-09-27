# Finance returns and customer adjustment account-source audit

Date: 2026-09-27. Scope: reachable supplier/customer credit and return routes, finance charges, writeoffs and overpayment writeoffs following the canonical Business Partner Finance profile merge. Source inspection and focused regression changes only; this document does not claim a live posting test passed.

## Confirmed inconsistency and correction

Sales credit-note posting in `ReturnOrderService` formerly selected `BusinessPartner.DefaultArAccountId` before `FinanceSettings.ControlAccountArId`. Current Finance AR invoice and receipt services use Finance settings. A customer carrying an obsolete partner override could therefore invoice into one AR account and credit another. This is an observable integration gap, not evidence that restoring the Accounts tab damaged posting.

Correction in this working tree:

- A credit linked to an original invoice uses that exact invoice journal's unique posted debit `AR-Control` transaction. It requires tenant ownership, the invoice posting event linked to that same journal, a posted unreversed journal for that invoice and one unambiguous control line. Missing or ambiguous historical evidence fails closed; it does not guess from current defaults.
- A standalone credit uses `FinanceSettings.ControlAccountArId`.
- Neither selects the legacy Business Partner AR-control override. Existing posted credit replay/reversal still uses its immutable central Finance authority.

Regression coverage added to `ArCreditNotePostingMigrationTests`: linked and standalone cases with three deliberately different original/current/legacy control accounts; missing, duplicate, cross-tenant control evidence; reversed or wrong-source journal; mismatched posting event. The existing cross-tenant account case now corrupts actual original ledger evidence. Unit and relational fixtures now carry original invoice journal/control evidence; relational fixed counts include the preserved original journal. These tests require execution by the coordinated root build.

## Reachable owners and policies

| Business action | Route and owner | Account authority / controls |
| --- | --- | --- |
| Sales customer credit note | `/api/sales/credit-notes`, create/submit/approval/post/apply/reverse; `ReturnOrderService` | Governed producer intent preparation, independent Finance approval/execution and replay verification. Linked AR account correction above. Debit remains explicit customer Sales Returns account, falling back to Finance `DiscountAllowedAccountId`. Tax still uses current Finance tax control. Posting is balanced before preparation. |
| Reverse posted Sales credit note | `/api/sales/credit-notes/{id}/reverse` | `IFinanceProducerReversalPreparationService` reconstructs reversal from original accounting-event identity, then independently approved execution; Sales does not rebuild original accounts or select another book. Exact retries verify durable source identity. |
| AP supplier debit note | `/api/ap/supplier-debit-notes`; `SupplierDebitNoteService` | Create/update resolve effective approved canonical AP profile and supplier/contractor role. Source-linked notes read the original invoice's AP-Control and original line/tax/discount AccountTransactions. Standalone uses central Finance AP control and explicit line GL accounts. |
| Reverse AP supplier debit note | `/api/ap/supplier-debit-notes/{id}/reverse` | Existing central posting engine reversal plan supplies original journal and reversal lines. Applied settlement must be reversed first; duplicate posted reversal returns original result. |
| Existing Inventory dispatched-return AP credit | `/api/ap/supplier-debit-notes/inventory-returns/{returnId}/credit` | Validated shipped return, exact GRN/PO/invoice line source, original posted invoice, quantity conversion and capacity. Creates governed debit-note draft. Existing dispatch clearing owner debits configured return clearing and credits item Inventory; commercial credit clears actual dispatch carrying value plus configured variance, avoiding a second inventory credit. |
| Legacy Finance supplier return | `/api/ap/supplier-returns` | GET remains available for audit. POST and approve intentionally return 409 `FIN-INT-012-013-PLANNED`; no goods, AP or GL mutation. This is an existing explicit limitation, not a missing account tab regression. |
| Finance charge, customer writeoff, overpayment writeoff | `/api/finance/subledger-adjustment-journals`; `SubledgerAdjustmentJournalService` | Effective approved AR profile required, central Finance AR control and explicit transaction contra. Legacy customer defaults do not supply contra. FinanceCharge/OverpaymentWriteoff require Revenue; Writeoff requires Expense; active tenant-owned direct-posting non-control account. Functional currency at rate 1; positive amounts; writeoff/overpayment bounded by reconciled posted balance. Stable request ID prevents duplicate posting. |
| Reverse customer adjustment | same route `/{id}/reverse` | Stored original control and contra accounts retained; compensating adjustment restores balance. Current canonical counterparty readiness is still evaluated by shared creation; changed/inactive profile historical-reversal behavior needs further acceptance coverage. |

Controller attributes are not the complete security policy: Finance controllers also use `FinancePermissionPolicyMap` (including `SubledgerAdjustmentJournal`). Do not remove authorization because a controller shows only `[Authorize]`. Sales credit-note routes have explicit manage/post/apply/reverse permissions.

## Remaining policy and lineage limitations

1. Sales return `CreditNoteLine` has no original invoice-line/tax-component IDs. A linked header alone is insufficient to safely reverse arbitrary original revenue and tax allocations. This correction deliberately does not rewrite that accounting policy or synthesize line lineage.
2. Canonical AR profile has payment terms, credit limit, reference and withholding-agent designation, but no Sales Returns GL account replacement. The still-active `CustomerSalesReturnsAccountId` is not equivalent to the retired AR control override. Display it accurately until Finance supplies a governed replacement.
3. Sales credit create/post currently checks active tenant-owned Business Partner but does not use canonical Customer role/effective approved AR profile readiness, unlike Finance AR invoices/AP debit notes/customer adjustments. Need an explicit new-document readiness rule and historical settlement/reversal exemption before extending it.
4. Source-linked supplier notes have strong original account lineage; end-to-end live return quantity allocation and mixed invoiced/uninvoiced GRN acceptance is not proven by this audit. The new Inventory requirements separately request that broader lifecycle.
5. Posted customer adjustment balance reconciliation is purposefully limited to supported posted AR sources and rejects mismatches. A customer advance is not automatically an overpayment writeoff source.

## Existing tests inspected (not rerun by this agent)

- `ArCreditNotePostingMigrationTests`: producer preparation/execution, accounting side-effect rollback, exact retry, cross-tenant accounts, posting prerequisites, credit limits, application, immutable reversal.
- `ArCreditNoteProducerRelationalFixtureTests`: independent SQLite scopes with production Finance engine, rollback and durable replay; not SQL Server parity evidence.
- `SupplierDebitNoteFoundationTests`: role/profile model, immutable settlement, source lineage, lifecycle permissions and workflow; some checks are source-contract assertions rather than transaction tests.
- `InventorySupplierReturnCreditTests`: real allocation helper behavior, original posted journal validation, inventory account grouping, clearing/variance and no second stock reversal; not a complete live lifecycle.
- `SupplierReturnsQuarantineTests`: both legacy mutations fail without tracked writes.
- `CustomerBalanceAdjustmentPostingTests`: explicit contra selection, balanced mocked posting, no duplicate balance impact, captured reversal accounts, wrong-direction/oversized/foreign-tenant/type/currency guards, missing approved profile failure.

Still needed before claiming this family verified: compile and run focused tests; real authorized/forbidden route checks; approved source transactions in the verification tenant; journal and subledger reconciliation; immutable reversal and replay evidence; SQL Server schema/transaction checks where required.

## Additional first-post AP profile correction

On request from the coordinating agent, the AP invoice and payment posting supplier resolvers now validate the exact captured Supplier/Contractor role and approved AP profile against the document date before first posting. The previous methods checked only active/non-blacklisted partner identity and could post after the captured profile became invalid. The corrected queries bind tenant, partner, role and profile IDs and include WHT defaults; they do not silently load a newer replacement. Existing posted documents skip this new first-post profile check so superseding a profile cannot change historical replay authority. Existing identity checks are unchanged.

Eight negative behavioral cases per producer were added to the existing AP invoice/payment test files (missing/draft/expired/future/foreign-tenant/wrong-role profile, inactive/wrong-type role). Each includes a separate approved replacement profile to prove it cannot be substituted, and verifies no journal or ledger writes. Existing duplicate-post tests now supersede the captured profile between first post and retry. Execution remains pending the coordinated build.

The AP expense fallback was also corrected: explicit line account, then invoice expense account, then exact captured AP profile default. The legacy supplier expense field is no longer a posting fallback. Initial posting requires the captured approved/effective profile. Posted invoices without custom distribution use their unique original posted expense source-line account before considering a profile; the parent journal must belong to that invoice and tenant, remain posted and unreversed. Added actual-post precedence tests with deliberately different accounts and a missing-default rejection, including historical replay after changed captured profile defaults, profile removal and changed legacy defaults. Inventory/fixed-asset account paths are unchanged.

Precise remaining historical limitation: a saved custom distribution stores final split rows and a hash of its pre-distribution basis, not the complete original basis. If its captured profile/default disappears or changes, final GL split accounts cannot recover that original basis. Such retries intentionally fail with `AP_INVOICE_DISTRIBUTION_HISTORY_REQUIRED`, retaining the original journal and directing Finance to review historical source evidence. They cannot be repaired by resetting a posted distribution. The existing basis validation remains enforced; transparent retry for that legacy scenario requires immutable original basis snapshots or a Finance-owned exact replay API. Two focused regressions cover changed and missing profile evidence after a posted custom split and assert no duplicate journal or transactions.
