# Customer account reference review — 25 September 2026

The user supplied a Dynamics GP Debtor Maintenance / Debtor Account Maintenance screenshot during procurement acceptance. It shows customer master data separately from a customer-specific account setup. The user then specified: **"every account showing must have posting integration"**. This rules out treating displayed account mappings as layout-only or save-only fields. Requirement 1 is reopened for end-to-end account integration.

Latest parallel follow-up: supplier input-tax fallback, governed supplier write-offs and account-change posting retries are implemented. All 332 focused API cases, 45 Core cases, 40 UI cases and changed-source TypeScript checks pass. The user has signed in, and all six AR mappings for Kwesi Owusu were saved through the visible editor and reloaded on the detail page. SQL confirms the six IDs and unchanged values across all eighteen supplier-default fields (`tmp/procurement-validation-20260925/customer-ar-before.json` and `customer-ar-after.json`). Customer-only AP and ChequeBook controls are hidden, and the existing Approved status is displayed correctly. AR transaction posting acceptance remains open. Data/API verification builds disabled analyzers after compiler failures, so the normal-analyzer gate remains open. See [the follow-up evidence](partner-posting-parallel-followup-20260925.md). Earlier gap tables are historical audits. Full lifecycle acceptance remains open.

## Initial audit (before the integration work below)

- One business partner can carry supplier and customer roles, retaining separate AP and AR control accounts.
- `BusinessPartnerReceivablesDefaultsDto` and `BusinessPartnerReceivablesFields` currently expose only `DefaultArAccountId`. They do not implement the complete customer account set in the screenshot.
- Supplier posting defaults already include cash source, bank, discounts, finance charges and write-off fields. Those supplier fields must not be reused as customer settings on a dual-role partner.
- Normal AR invoice posting in `InvoiceService` selects the partner AR control account before the tenant fallback. Revenue comes from the invoice line account; the inventory-cost branch uses tenant COGS and inventory settings. Extending customer account setup therefore needs explicit account precedence and posting integration, not only extra selectors.

## Screenshot comparison

| GP customer account setting | Current customer-specific setup |
|---|---|
| Chequebook and cash source (Chequebook / Customer), cash account | Not exposed in the AR defaults form |
| Accounts Receivable | Implemented as customer AR control account |
| Sales | No customer default in the AR defaults DTO; normal invoice posting requires a line account |
| Cost of Sales and Inventory | No customer defaults in the AR defaults DTO; normal invoice inventory-cost branch uses Finance settings |
| Terms Discounts Taken and Terms Discounts Available | No separate customer defaults in the AR defaults DTO |
| Finance Charges | No customer default in the AR defaults DTO |
| Writeoffs and Overpayment Writeoffs | No separate customer defaults in the AR defaults DTO |
| Sales Order Returns | No customer default in the AR defaults DTO |

The screenshot also contains customer profile fields such as debtor ID, name, addresses, class, payment terms and price level. It does not by itself establish a requirement to reproduce every GP master-data feature or its desktop layout.

## Required acceptance

Every account offered in AP or AR setup must have a named transaction posting path that consumes it, with tenant/account eligibility validation, documented precedence, and regression evidence asserting the resulting ledger account and amount. Saving and reloading the selection is insufficient. Verify AP/AR independence on one dual-role partner, fallback behavior, and that retry/reversal retains the original posted accounting evidence. Existing administrator mappings and posted transactions must be preserved.

## Integration work following the audit

The user delegated the discount-policy choice. Retain the existing net trade-discount treatment and recognize settlement discounts when taken. Do not invent entries for merely available discounts. The supplier form no longer offers the two inapplicable standalone mappings (Trade Discount and Terms Discounts Available); persisted legacy values are retained and not rewritten.

Implemented account integration:

- Supplier Terms Discounts Taken now resolves the partner via the existing Finance identity bridge and overrides the tenant discount-received fallback when posting a payment. Legacy suppliers without a partner retain Finance defaults. Invalid/missing/ambiguous partner identity cannot silently select another partner.
- At payment creation, an explicit bank wins; otherwise use the supplier ChequeBook, or resolve the creditor Cash GL account to one active same-tenant bank. The effective bank is authorized and persisted before later workflow/posting. An ambiguous bank mapping requires an explicit bank selection; a cash GL override must not diverge from the bank ledger.
- Added independent customer Sales, Cost of Sales, Inventory, Terms Discounts Taken and Sales Returns mappings. Customer AR control remains unchanged. The create/edit/detail forms expose these mappings, with same-tenant/active/postable account validation and account-type restrictions.
- AR invoice posting and authoritative dimension account resolution use explicit line account, then customer Sales default. Customer COGS/Inventory defaults precede tenant defaults for captured inventory costs. Receipt discounts and credit-note/return posting use their respective customer defaults before existing tenant fallbacks.
- Returned-cheque discount reversal reads the original receipt's tagged discount account, preserving the posted account after master/default changes. Historical untagged receipts retain the pre-existing fallback. Multiple original discount accounts fail closed for reconciliation.
- Partial DTO updates preserve omitted customer mappings, including updates from older clients; explicit null clears only the supplied field. Supplier mappings remain independent.
- Supplier Freight, Miscellaneous and Finance Charges now have explicit AP invoice line types. With supplier defaults enabled, the draft captures the applicable account, with an explicit line account taking priority. The form displays the chosen account and supplies it to the same budget/dimension workflow as other expenses. Saved drafts/approved invoices retain the captured account. Receipt and landed-cost lines keep their source-owned clearing accounts; expense freight does not replace the landed-cost capitalization flow. Freight and Finance Charge use the service tax transaction category without guessing exemption or tax rates.
- Invoice defaults and supplier eligibility now recognize durable AP identity links in addition to exact IDs/codes. Ambiguous, missing or ineligible linked partners cannot be silently treated as standalone suppliers.

Migration `20260925110645_CustomerPostingAccounts` adds five nullable columns, indexes and restricted-delete account FKs. It does not populate or replace existing mappings. Rollback is guarded while any new mapping is in use. The generated EF snapshot and target model are retained.

The initial matrix below describes the pre-change audit. Supplier Tax override and Writeoffs remain open integration work. Customer cash and unsupported adjustment/charge workflows from the GP reference also remain open. This requirement is not complete.

### Verified customer/payment slice

- Core/Data/API and API tests built successfully. The focused posting run passed 201 of 202 cases; the remaining returned-cheque fixture omitted its new account's required accounting-book mapping. After adding that fixture mapping, all 11 banking cases passed. Latest outcomes across those 202 distinct cases pass; this is not a clean full-suite claim. Evidence: `partner-account-posting.trx` and `partner-account-banking-followup.trx` under `tmp/procurement-validation-20260925`.
- The test-only banking follow-up reused the already built production references, with analyzers disabled for that test compilation. Production sources were built with normal analyzers.
- Customer/supplier form regressions: 14 tests passed across three files. TypeScript verification passed for the changed source set.
- The isolated database accepted migration `20260925110645_CustomerPostingAccounts`: 623 to 624 migrations, model parity confirmed, five nullable columns and five restricted account FKs verified. Existing Finance mapping hashes are unchanged and no customer mappings were seeded. Source `RhemaERP` was not migrated by this step. Evidence: `clone-customer-accounts-migrations-result.json`, `customer-accounts-schema-checks.json`, and `tmp/customer-posting-model-parity.log`.
- At this earlier checkpoint, authenticated browser acceptance was pending sign-in. The latest signed-in customer mapping save/reload and SQL checks are recorded above; transaction lifecycle acceptance remains open and is separate from automated posting assertions.

### Verified typed-charge follow-up

- API build passed with normal analyzers (38 warnings, zero errors); unchanged Core/Data references were reused. The test project then compiled against those binaries with analyzers disabled and passed all 30 selected cases: 12 new typed-charge/tax/identity cases plus 18 existing supplier projection/eligibility cases. Evidence: `tmp/partner-charge-api-build.log`, `tmp/partner-charge-posting.log`, and `tmp/procurement-validation-20260925/partner-charge-posting.trx`.
- Actual ledger assertions cover all three charge mappings, explicit line override, disabled defaults, durable identity links, saved-account retention after master changes, rejected draft posting, net trade discount, and idempotent retry. Invalid/inactive and foreign-tenant accounts are rejected. PO charge-account snapshot and service-tax routing are also covered.
- Invoice UI tests exposed a retained-array-identity issue in React Hook Form: changing line type did not refresh untouched defaults. The effect now observes the relevant line values. All 22 UI/default-planning tests pass, including visible account changes, saved charge types/accounts and dimension account agreement. Changed-source TypeScript verification passed for 61 files. Evidence: `tmp/partner-charge-ui.log` and `tmp/partner-charge-types.log`.
- The isolated API was restarted as PID 55024; readiness returned 200 after EF model warm-up. Frontend PID 37352 remains on port 3003. Runtime metadata, hashes and health evidence are in `tmp/procurement-validation-20260925`. No schema change was needed for typed AP line charges.
- Read-only SQL confirmed `ap.officer` has an active `TDC_STORES_MANAGER` assignment with restricted scope explicitly including the existing GRN warehouse. Use `admin` for account configuration acceptance, then `ap.officer` for the existing receipt/Auto Invoice journey. Neither role nor warehouse permissions were changed.

## Supplier account audit

Customer screen follow-up (25 September, 16:50 UTC): the authenticated customer detail/editor displayed all six implemented AR mappings, and temporary picker selections were cancelled with SQL confirming unchanged saved mappings. Customer-only create/edit forms now hide AP settings while retaining any stored supplier mappings; all eight focused create/edit tests and changed-source TypeScript checks pass. Dual-role account tabs remain covered. The account catalogue recovered after transient navigation/fetch failures. See `procurement-verification-20260925.md` for evidence and runtime recovery details. Saved-mapping-to-posting acceptance and the remaining integration gaps are still open.

| Currently displayed supplier setting | Integration evidence / open gap |
|---|---|
| Accounts Payable, Purchases | `VendorInvoiceService.PartnerDefaults` applies these to new invoices when partner defaults are requested; invoice posting resolves document/supplier/Finance accounts. Requires lifecycle proof from the displayed partner setting through supplier identity and posting. |
| Accrued Purchases, Purchase Price Variance | `ProcurementReceiptDistributionService` consumes the captured PO supplier defaults; original receipt accrual is retained by invoice posting. Requires lifecycle proof from the displayed setting. |
| Terms Discounts Taken | Gap: `VendorPaymentService.BuildApPaymentPostingRequestAsync` currently uses tenant `DiscountReceivedAccountId`, not the displayed partner override. |
| Cash account / cash source / ChequeBook default | Gap: payment posting selects payment/tenant bank and its GL account; the displayed partner cash-source override is not consumed by that path. Any integration must also preserve bank authorization, bank ledger and reconciliation consistency. |
| Terms Discounts Available | Gap: saved and validated but no posting consumer found. An available discount is not automatically a realized settlement discount. A supported recognition event is required. |
| Trade Discount | Gap: saved and validated but normal AP/AR invoice posting calculates net purchases/revenue. A separate discount account requires an explicit supported gross-posting policy; do not silently alter the existing net policy or double count the discount. |
| Tax | Gap: the partner tax GL override is not consumed by the normal invoice posting path, which resolves tax rule/breakdown accounts. Preserve tax-rule ownership and recoverability rules when determining applicable fallback. |
| Finance Charges, Miscellaneous, Freight, Writeoffs | Gap: saved and validated but no posting consumer of these partner fields found in the Finance services. Typed source charges/adjustments and their governed posting paths must be identified or implemented; free-text line descriptions are not a safe routing mechanism. |

The service search covered current source, excluding generated migration/archive and build files. This audit is not posting-test evidence. No financial configuration, permissions, balances or posting policy was changed during the audit. Additional customer fields from the screenshot remain unimplemented; this report must not be used as a completion claim or to justify silently removing requested accounting capabilities.
