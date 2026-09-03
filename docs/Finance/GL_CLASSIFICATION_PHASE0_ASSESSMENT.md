# Finance GL Classification Refactor — Phase 0 Assessment

Status: proposed architecture and integration gate for Finance-owner approval

Assessment base: `4034bfc8f0e95a99de9f53f7c2d776735826ae3d`

Primary integration branch: `codex/finance-budget-posting-evidence`

Phase 0 branch: `codex/finance-gl-classification-phase0`

Prepared: 2026-09-03

## Executive decision

Proceed in controlled phases. The handoff's direction is sound, with these corrections:

1. `AccountingBook` remains the book master and `AccountAccountingBook` becomes the authoritative account/book assignment. Put the book-specific classification FK on that assignment.
2. Revaluation policy must be book-specific from its first normalized version. A single `AccountCurrencyLink.RevaluationRequired` cannot represent different IFRS and statutory treatment.
3. Published layouts must own immutable, resolved account-membership snapshots. Retaining only a live `ClassificationId` is not reproducible.
4. An inactive-book/account exception is permitted only through a trusted, server-built exact reversal of a posted event. A request DTO must not grant the exception.
5. `SystemRole` is a small, controlled behavioural vocabulary. Report captions and ordinary presentation groupings remain configurable classifications/layout rows.
6. Rename `BookClassification` in one coordinated V2 cutover, not through an indefinite dual property. Persisted historical columns can be renamed separately from the public-contract cutover.
7. Phase 1 must include the central posting guard and startup SQL removal/reconciliation. UI filtering is not an accounting control.
8. No Phase 0 production code or schema change is justified. Existing tests already characterize much of the posting boundary; desired behaviours that currently fail belong in the matrix below, not as skipped tests.

The clean development-data reset remains appropriate, but only after forward migrations, deterministic seeds, and every producer consumer have been integrated at one known commit.

## Critique of the supplied handoff

### Confirmed

- Accounting books and detailed account classifications are different concepts.
- Core account types (`Asset`, `Liability`, `Equity`, `Revenue`, `Expense`) must remain controlled.
- The existing layout engine should be extended, not replaced.
- The three account flags and three line-item strings duplicate and mislabel the real accounting-book model.
- Configurable classification names make literal category comparisons unsafe.
- The central posting engine does not currently prove accounting-book existence, posting status, or account membership.
- The current FX candidate query excludes Equity, Revenue, and Expense.
- Account creation does not prove exact active-segment completeness.
- Recent Transactions is a static placeholder.
- Local data is disposable, while shared code contracts and EF history are not.

### Corrected or made more precise

- `AccountClassification.SystemRole` should be nullable and governed. `CostOfSales`, `OtherIncome`, and `OtherExpenses` are normally reporting configuration, not behavioural roles.
- A classification default is not enough to determine revaluation. The effective policy key is account-book assignment plus currency.
- A published layout's reproducibility cannot be achieved merely by versioning row definitions. The accounts resolved from a classification hierarchy must also be frozen at publication.
- Revaluation can be simpler and safer than an asset/liability branch: calculate every exposure in one signed debit-minus-credit coordinate system. The account's normal balance remains useful for display and validation, but not for changing the arithmetic formula.
- The present reversal implementation establishes original-journal lineage but still consumes caller-supplied lines and links them by order after checking only the line count. The future exception must compare or derive account, side, amounts, currency/rate evidence, dimensions, and line identity from the original.
- `full_database.sql` is a tracked historical migration script with no runtime/project reference found. It is not the authoritative deployment schema. `Program.cs` startup repair SQL is active and is therefore the more important dependency.
- `ALL_ACTIVE_BOOKS` is not merely documentation. Inventory still recognizes it, while manual journals reject it and a migration introduced it for opening stock. It must be expanded by an orchestrator before single-book posting or retired through an owner-coordinated contract change.

## Evidence summary

| Finding | Evidence at the assessed base | Consequence |
|---|---|---|
| Real book model exists | `AccountingBook.cs:10-36`; unique tenant/code mapping in `ApplicationDbContext.cs:2813-2824` | Extend, do not invent a parallel scheme |
| Join is present but incomplete | `AccountAccountingBook.cs:9-22`; unique tenant/account/book index at `ApplicationDbContext.cs:2827-2831` | Add classification and authoritative lifecycle here |
| Legacy flags drive the join | `AccountingBookService.cs:134-141`; `AccountService.cs:198-200,314-316` | DTO collection is currently not authoritative |
| Public account DTO already exposes mappings | `AccountDtos.cs:193-194,521-522,680-681`; mapping projection in `AccountService.cs:508-518` | A clean replacement contract is feasible |
| Central engine omits book guard | It normalizes `request.BookClassification` at `FinancePostingEngine.cs:932`, then loads only tenant accounts at `1204-1221` | External/bypass callers can post an account into an unassigned book |
| Currency policy is not book-scoped | `AccountCurrencyLink.cs:57`; link lookup in `FinancePostingEngine.cs:1470-1487` | One link cannot inherit two book classifications |
| Revaluation excludes P&L/equity | `CurrencyRevaluationService.cs:1434-1447` | Stakeholder request is currently blocked |
| Revaluation branches account kind | `CurrencyRevaluationService.cs:1465-1468,1531-1551` | Replace with signed debit-minus-credit basis |
| FX posting book is hardcoded | `CurrencyRevaluationService.cs:730,951,1155,1664` | Per-book revaluation cannot be correct yet |
| Layout execution uses live mappings | `FinancialStatementLayoutExecutionService.cs:128-168,185-192` | Published results can drift after reclassification |
| Mapping types lack classification | `FinancialStatementLayout.cs:128-145`; DTO at `FinancialStatementLayoutDtos.cs:67-76` | Extend existing layout contract/import/export |
| Segment optionality is live | New-account UI reads `seg.isMandatory` at `accounts/new/page.tsx:302-307`; create service checks only non-empty collection at `AccountService.cs:173-180` | Exact active-definition equality must be server enforced |
| Account inquiry is a placeholder | `accounts/[id]/page.tsx:728-734` | Requires a real tenant-scoped read endpoint |
| Startup SQL writes legacy columns | `Program.cs:1152-1212` and creates books/join at `1584-1683` | Remove/reconcile during forward migration cutover |
| HR writes Finance accounts directly | `PayrollService.cs:69-76,2983-3027` | HR-owner change required before removing strings |
| External evidence hashes book field | `FinanceExternalPostingEvidence.cs:15-33` | V2 rename changes canonical evidence and contract version |

Line numbers are evidence anchors at the exact base, not permanent API references.

The complete fixed-string file index used for this assessment is recorded in
`docs/Finance/GL_CLASSIFICATION_PHASE0_CONSUMER_INVENTORY.md`. It excludes migration
Designer output so generated schema history is not mistaken for active runtime use.

## Target domain model

### `AccountingBooks` (existing, canonical)

Retain current primary key and tenant ownership.

Required constraints:

- PK `Id`.
- FK `TenantId -> Tenants.Id`, restrict delete.
- unique `(TenantId, Code)` over non-deleted rows; code normalized uppercase.
- at most one live default per tenant (filtered unique index on `TenantId` where `IsDefault = 1 AND IsDeleted = 0`).
- `Code` immutable after any account mapping, posting, layout, or fixed-asset book usage.
- `AllowsPosting = false` is distinct from `IsActive = false`; both reject ordinary postings.

### `AccountClassifications` (new)

| Column | Rule |
|---|---|
| `Id` | PK |
| `TenantId` | required FK, tenant boundary |
| `AccountingBookId` | required FK to `AccountingBooks`, restrict delete |
| `ParentClassificationId` | nullable self-FK, restrict delete |
| `Code` | required normalized immutable code, max 50 |
| `Name` | required editable display name, max 200 |
| `Description` | optional |
| `CoreAccountType` | required controlled enum |
| `DefaultRevaluationTreatment` | required enum: Include/Exclude; default Exclude unless Finance approves a code-specific seed |
| `SystemRole` | nullable controlled enum |
| `IsPostingClassification` | true only for assignable leaves |
| `Status` | Draft/Active/Retired |
| `DisplayOrder` | deterministic sibling order |
| `RowVersion` | optimistic concurrency token |
| audit fields | creator/modifier/timestamps; retirement reason and actor |

Constraints and guards:

- unique live `(TenantId, AccountingBookId, Code)`.
- parent and child must share tenant/book and `CoreAccountType`.
- no cycles; enforce in service and add database protection where practical.
- active enabled account mappings may reference only Active, posting/leaf classifications.
- a used code and `CoreAccountType` are immutable; rename/reorder is allowed.
- retire rather than hard-delete any used classification.
- a `SystemRole` must be valid for the selected core type. Exact cardinality is role-specific: control roles may be zero/one per tenant/book; ordinary cash/bank families may be many.

### `AccountAccountingBooks` (existing, authoritative assignment)

Add:

- `AccountClassificationId` required whenever the mapping is enabled.
- `EnabledFrom`, `EnabledTo` if historical membership periods are required; otherwise retain audit timestamps and soft deletion for the first cutover.
- `RowVersion` for concurrent account editing.

Retain unique `(TenantId, AccountId, AccountingBookId)`. Add indexes `(TenantId, AccountingBookId, AccountClassificationId, IsEnabled)` and `(TenantId, AccountId, IsEnabled)`. Add FKs to Account and Classification with restrict delete. Service validation must prove all three rows share one tenant and that `Account.AccountType == AccountClassification.CoreAccountType`.

`FinancialStatementLineItem` is transitional only and is removed after layout mappings and snapshot publication are live.

### `AccountCurrencyLinks` (existing posting-currency capability)

Keep this as the account/currency capability and rate-policy record. Remove `RevaluationRequired` only after policy backfill. Its unique live key should remain `(TenantId, AccountId, LinkedCurrencyCode)`.

### `AccountBookCurrencyPolicies` (new)

This is the normalized revaluation decision, not another currency link.

| Column | Rule |
|---|---|
| `Id` | PK |
| `TenantId` | required |
| `AccountAccountingBookId` | required FK |
| `AccountCurrencyLinkId` | required FK |
| `RevaluationOverride` | nullable Boolean; null means inherit |
| `OverrideReason` | required when override is non-null and differs from inherited result |
| `OverrideEvidenceId` | optional/required according to approval policy |
| `ApprovedByUserId`, `ApprovedAtUtc` | required for governed non-standard overrides |
| `RowVersion` | concurrency token |
| audit fields | required |

Unique `(TenantId, AccountAccountingBookId, AccountCurrencyLinkId)`. Service validation proves that both referenced records belong to the same account and tenant. No cached `EffectiveRevaluationRequired` is persisted; compute it from override or the assignment's current classification. Preview/posting evidence freezes the resolved source, classification code, and effective result.

If a policy is included in IFRS and excluded in Local Statutory, the same foreign transaction may produce an IFRS-only closing adjustment. The underlying transaction remains shared only if the application's parallel-book model intentionally posts it to both books. Revaluation batches, journals, gains/losses, reversals, close evidence, and reports must all carry the selected book code. Cross-book netting is forbidden.

### Published-layout membership snapshots (new)

Add `FinancialStatementPublishedMappingSnapshots`:

- `Id`, `TenantId`.
- `FinancialStatementLayoutVersionId`.
- `FinancialStatementRowId` and source `FinancialStatementRowMappingId`.
- source `AccountClassificationId` nullable and frozen `ClassificationCode`.
- `IncludeDescendants`.
- `HierarchyFingerprint` and `ResolutionFingerprint`.
- `PublishedAtUtc`, `PublishedByUserId`.

Add child `FinancialStatementPublishedMappingAccounts`:

- `Id`, `TenantId`, `PublishedMappingSnapshotId`, `AccountId`.
- frozen `AccountNumber`, `AccountName`, `CoreAccountType` for explainability.
- frozen `AccountAccountingBookId` and `AccountClassificationId`.

Unique `(TenantId, PublishedMappingSnapshotId, AccountId)`. Publication resolves the classification tree and enabled assignments in one transaction, writes snapshots, computes a canonical ordered SHA-256 fingerprint, validates overlap rules, then changes version status. Published execution reads snapshot accounts only. Draft preview reads live mappings and explicitly labels itself as live. Cloning a published version to a draft returns to live resolution until republished.

## Dependency graph

```text
AccountingBook
  ├─ AccountAccountingBook ─ Account
  │    ├─ AccountClassification (book-scoped presentation/default policy)
  │    └─ AccountBookCurrencyPolicy ─ AccountCurrencyLink ─ Currency
  ├─ FinancialStatementLayout ─ Version ─ Row ─ Mapping
  │                                      └─ PublishedMappingSnapshot ─ frozen accounts
  ├─ JournalEntry / AccountTransaction / PostingEvent / AccountBalance
  └─ Fixed Asset book records

Producer module
  └─ Finance V2 posting contract (AccountingBookCode + AccountId + dimensions)
       └─ External adapter / central FinancePostingEngine
            ├─ resolves active posting book
            ├─ validates enabled account/book assignments
            ├─ validates currency links/dimensions/period
            └─ persists canonical book code and posting evidence
```

External producers never submit `AccountClassificationId`. Classification is Finance-owned metadata resolved from `AccountId + AccountingBookCode`.

## Complete active-consumer inventory by owner

### Finance — account and book master

- Entity/configuration: `Account.cs`, `AccountingBook.cs`, `AccountAccountingBook.cs`, `ApplicationDbContext.cs`.
- DTO/interface/controller: `AccountDtos.cs`, `AccountingBookDtos.cs`, `IAccountService.cs`, `IAccountingBookService.cs`, `AccountController.cs`, `AccountingBooksController.cs`.
- Services: `AccountService.cs`, `AccountingBookService.cs`, `GeneralLedgerService.cs`, `AccountCombinationService.cs`.
- UI/types/data access: account list/detail/new/edit pages, `frontend/src/types/finance.ts`, `finance-data.service.ts`.
- Duplicated hardcoded detailed catalogue: both `accounts/new/page.tsx` and `accounts/[id]/edit/page.tsx` define `DETAILED_ACCOUNT_TYPES`.
- Other literal presentation consumers: finance dashboard grouping and cash-account creation filters.

### Finance — posting and persisted book code

- Boundary: `FinancePostingDtos.cs`, `IFinancePostingEngine.cs`, `FinancePostingEngine.cs`, `FinanceSystemPostingEngine.cs`.
- External boundary: `FinanceExternalProducerDtos.cs`, `IExternalFinancePostingAdapter.cs`, `ExternalFinancePostingAdapter.cs`, `FinanceExternalPostingEvidence.cs`, `FinanceIntegrationContractCatalog.cs`, `FinanceExternalProducerContractCatalog.cs`.
- Core persisted records: `JournalEntry`, `AccountTransaction`, `AccountBalance`, `FinancePostingEvent`, `JournalBatch`, `RecurringJournal`, `OpeningBalanceBatch`, `AllocationRunBatch`.
- Finance producers: manual/general/recurring journals, journal batches, opening balances, AP invoice/debit note/payment/receipt posting, AR invoice/credit note/payment/receipt posting, Cash/Bank, Budget allocation, FX, Inventory Finance adapters, Unit Accounting, and Fixed Assets services.
- Frontend book consumers: manual journal create/edit/detail, batches, recurring journals, opening balances, trial balance, detailed ledger, balance sheet, income statement, cash flow, layouts, and fixed-asset transfers.
- Hardcoded fallback risk: many DTO/entity defaults and services use `"IFRS"`; report screens fall back to `DEFAULT_ACCOUNTING_BOOKS`; batch creation still renders three literal options.

### Finance — revaluation

- `AccountCurrencyLink.cs`, `CurrencyLinkDtos.cs`, `CurrencyService.cs`, `AccountService.cs` currency-link commands.
- `CurrencyRevaluationService.cs` candidate selection, exposure math, preview, batch, posting, reversal, and link state.
- Account detail currency policy UI at `frontend/src/app/finance/accounts/[id]/page.tsx`.
- Existing coverage: `AccountCurrencyLinkPersistenceTests`, `FinancePostingEngineTests`, `ControlledOpeningBalancePostingTests`, `FixedAssetDisposalFoundationTests`, `FxRealizedUnrealizedRevaluationTests`.

### Finance — financial reporting

- Entities/DTOs: `FinancialStatementLayout.cs`, `FinancialStatementLayoutDtos.cs`, account-book `FinancialStatementLineItem`.
- Services: `FinancialStatementLayoutService`, `FinancialStatementLayoutImportService`, `FinancialStatementLayoutExecutionService`, `FinanceReportExportService`, `FinanceAdHocReportCatalog`, document builders.
- UI: layouts plus five main financial report pages and tests.
- Imports/exports: workbook template/parser and definition hash currently understand Account, AccountRange, and AccountHierarchy only; Classification must be added consistently to workbook lookups, normalized definition/hash, validation, persistence, export, and execution.
- Literal legacy report logic: `GeneralLedgerService.cs:1204-1243` separates Cost of Sales/Other Expenses by `AccountCategory`; `FinanceAdHocReportCatalog` exposes category fields; dashboard groups by category text.

### Finance — segments and dimensions

- Account identity: `AccountSegmentStructure`, `AccountSegmentValue`, lookup values, segment DTOs/controllers/services and `AccountCombinationService`.
- Transaction analysis: separate `FinanceDimensionDefinition`, values, sets, items, account rules, producer routes, filters, and reporting UI already exist.
- `IsMandatory` relevant to this refactor is the Finance account-segment property. Unrelated HR, Procurement, Maintenance, and workflow properties with the same name are out of scope.
- Agreed initial identity segments: Legal Entity/Company and Natural Account. Agreed transaction dimensions: Department/Cost Centre, Project/Development, Estate/Property/Site, Contract, Funding Source, Activity/Programme.

### Finance — Fixed Assets (Finance-owned)

- `AccountingBookId` and/or `BookClassification` are present throughout asset book values, depreciation schedules/runs, disposals, transfers, valuations and reports.
- Fixed Assets may be updated in the same Finance integration window, but should resolve books by stable code and persist the FK where available.
- Do not infer that every stored historical `BookClassification` column is a public contract; separate persistence renames from DTO V2.

### HR/Payroll owner

- `PayrollService.cs` directly queries and adds `Account` entities and writes `AccountCategory`/`AccountSubCategory` in account provisioning.
- Required change: call a Finance-owned account-provisioning contract, or supply stable Finance classification codes to such a contract. HR must not select raw classification IDs or continue direct Finance-table mutation.
- Payroll account lookups by linkage/account number must be regression tested after reset.

### Procurement owner

- `ProcurementSupplierOnboardingTokenService` builds `FinancePostingRequestDto` directly.
- `TenderBidService` builds tender-fee Finance requests directly.
- `ProcurementSupplierOnboardingTestSeeder` creates Finance accounts with category strings and legacy book flags.
- Required changes: V2 book field, updated consumer tests, and removal of Finance account creation from Procurement-owned seed code. Finance seed/provisioning should own the accounts.

### Inventory owner

- `StockAdjustmentService` consumes `FinancePostingRequestDto` and recognizes `ALL_ACTIVE_BOOKS`.
- `InventoryDisposalService` posts through a Finance interface with the same DTO.
- Inventory entity/DTO records carry `BookClassification` for opening-stock/accounting lineage.
- Required changes: V2 field, explicit expansion to one posting per resolved active book (or selection of one required book), and consumer tests proving no pseudo-code reaches the single-book engine.

### Sales owner

- `ReturnOrderService` posts sales credit-note requests through `IFinancePostingEngine`.
- Required change: V2 field and consumer test. Finance continues to resolve account classification.

### Other named producer owners

- The external producer catalogue covers Procurement, Inventory, Sales, Quantity Survey, Estate, Legal, Maintenance, HR and Finance/Fixed Assets routes through `IExternalFinancePostingAdapter`.
- Even where no current source file constructs the envelope, the external JSON contract is published surface area. Each adopted producer needs a V2 fixture/evidence test before enforcement.

### Generated/historical artifacts (not active consumers)

- Hundreds of term hits are migration `.Designer.cs` model snapshots. They document schema history and must not be edited.
- Active migration bodies that introduced or later merged these fields also remain immutable history.
- `src/ErpSystem.Api/full_database.sql`, `src/ErpSystem.Data/schema.sql`, and `verify_current_schema.sql` are migration-script snapshots. No build, startup, or deployment reference to `full_database.sql` was found. Mark them generated/reference-only unless deployment ownership proves otherwise; regenerate only through an agreed process after migrations.
- `Program.cs` repair routines are active runtime code and are not generated artifacts.

## Producer contract transition

| Producer module | Contract/DTO | Current field | V2 field | Evidence/hash impact | Owner | Required integration test |
|---|---|---|---|---|---|---|
| All Finance callers | `FinancePostingRequestDto` / `IFinancePostingEngine` | `BookClassification` | `AccountingBookCode` | Internal idempotency/audit snapshots using the field change shape/name; economic value unchanged | Finance | engine rejects unknown/inactive/non-posting/unmapped book and accepts mapped account |
| External route adapter | `FinanceExternalPostingEnvelopeDto` | `BookClassification` | `AccountingBookCode` | Canonical SHA-256 input field occupies same ordered position, but V2 contract version must domain-separate old/new evidence | Finance | known V2 evidence vector; tampered book fails; V1 rejected after cutover |
| Procurement onboarding | direct `FinancePostingRequestDto` | `BookClassification` | `AccountingBookCode` | No external envelope hash on direct path; compiled contract break | Procurement | approved token posting reaches mapped book |
| Procurement tender | direct `FinancePostingRequestDto` | `BookClassification` | `AccountingBookCode` | compiled contract break | Procurement | tender fee posting and idempotent retry |
| Inventory stock adjustment | direct request and opening-stock DTO | `BookClassification`/`ALL_ACTIVE_BOOKS` | explicit `AccountingBookCode` per post | one evidence/idempotency identity per concrete book | Inventory | pseudo-book expanded; each book posting balanced and idempotent |
| Inventory disposal | direct request | `BookClassification` | `AccountingBookCode` | compiled contract break | Inventory | disposal posts only to enabled mapping |
| Sales return | direct request | `BookClassification` | `AccountingBookCode` | compiled contract break | Sales | credit note posts and reversal retains original book |
| Adopted external routes | `FinanceExternalPostingEnvelopeDto` JSON | `bookClassification` | `accountingBookCode` | breaking JSON and canonical evidence change | respective producer | serialized V2 fixture accepted with exact evidence hash |
| Fixed Assets | Finance DTOs/entities | mixed book ID and code | `AccountingBookId` internally; `AccountingBookCode` at posting boundary | Finance-owned persisted/API migration | Finance | capitalization/depreciation/valuation/transfer/disposal per book |

Exact V2 sequence:

1. Publish `FIN-INT-001` V2 and external adapter contract V2 in the code-backed catalogue and documentation. Define JSON as `accountingBookCode`; no dual fields in the final DTO.
2. Define V2 evidence canonicalization with an explicit domain/version prefix, for example `RHEMA-FIN-EXTERNAL-POSTING|2.0`, then the same ordered normalized values including accounting-book code.
3. Provide golden canonical-string/hash vectors and JSON fixtures to all owners.
4. On coordinated branches, rename Finance-owned adapters and all in-repository producer consumers. Compile failures are the migration checklist.
5. Update persisted/API projection names where warranted. A database column rename may be a later forward migration; it must not delay contract clarity.
6. Merge producer changes and Finance boundary change in a declared integration window at one exact commit train.
7. Reject V1 at the boundary after all in-repository consumers pass. Remove `BookClassification` from request/envelope and catalogue docs in that same train.

Do not bind V1 and V2 simultaneously to two writable properties. If release mechanics force temporary coexistence, isolate it at an HTTP versioned endpoint/adapter with a dated deletion commit; never allow both names in one DTO or hash ambiguously.

## Central posting and reversal rules

Ordinary posting validation, before currency/dimension work and before persistence:

1. Normalize `AccountingBookCode` to uppercase.
2. Load exactly one non-deleted tenant book by code.
3. Require `IsActive && AllowsPosting`.
4. Reject `ALL_ACTIVE_BOOKS` or any other pseudo-code.
5. Load every distinct line account in the tenant; require Active and direct-posting eligibility according to the existing control-account policy.
6. Load every `(AccountId, AccountingBookId)` mapping; require present, non-deleted, enabled, and classified with a valid active compatible leaf.
7. Use the resolved canonical book code for journals, transactions, balances, posting events, hashes, and audit.
8. Audit a stable denial reason without leaking cross-tenant existence.

Reversal exception:

- Entry is through a server command keyed by original `FinancePostingEventId` or immutable `JournalEntryId`, not a general request Boolean.
- Original event must be tenant-owned, Posted, unreversed, and internally consistent.
- Server derives the original book, accounts, line order, currency, foreign and functional amounts, rate evidence, dimension-set/snapshot IDs, and source lineage.
- Reversal lines are exact side-swaps of the immutable original. Compare all fields again in the posting transaction to prevent time-of-check/time-of-use changes.
- An inactive/non-posting book or disabled/retired account mapping is allowed only for this derived exact reversal. A deleted tenant/account, corrupted lineage, changed supplied line, or arbitrary compensating journal is rejected and escalated.
- The reversal posts to an allowed reversal date/open period but retains the original accounting book. It creates reciprocal journal/transaction/event links and is idempotent.
- External V1 currently rejects reversal actions; future external compensation needs its own explicit contract, not reuse of this exception.

## Revaluation mathematics

Use a signed debit-coordinate balance for every core account type:

```text
signedForeign   = Σ(transactionDebitCurrency - transactionCreditCurrency)
signedCarrying  = Σ(functionalDebit - functionalCredit) + signed prior adjustments
signedRevalued  = round(signedForeign × closingRate)
delta           = round(signedRevalued - signedCarrying)
```

- `delta > 0`: debit the revalued account and credit unrealized gain.
- `delta < 0`: debit unrealized loss and credit the revalued account.
- `delta = 0`: no line.

This works for debit-normal and credit-normal accounts, negative/reversing balances, and P&L/equity without treating every non-liability account as an asset. Normal-balance mapping remains:

- Debit normal: Asset, Expense.
- Credit normal: Liability, Equity, Revenue.

Use that mapping for labels, reasonableness warnings, close presentation, and classification compatibility—not to invert the signed arithmetic. Prior revaluation adjustments must be accumulated in the same signed coordinate. Batch identity and prior-adjustment lookup must include tenant, book, account, currency, revaluation date/period, and unreversed status.

Each batch/preview line freezes the classification code, default treatment, override, effective source, book code, signed balances, closing-rate ID/value/date/type/side, prior adjustments and policy warning. Any change after preview invalidates its fingerprint.

## `SystemRole` decision

Recommended controlled roles:

- `Cash`, `Bank`.
- `ReceivableControl`, `PayableControl`.
- `InventoryControl`.
- `FixedAssetCost`, `AccumulatedDepreciation`, `AssetUnderConstruction`.
- `InputTax`, `OutputTax`, `WhtReceivable`, `WhtPayable`.

These roles identify behaviour-driving families and enable stable candidate selection. Exact posting accounts still come from governed Finance settings/mappings; a role does not automatically select a unique control account.

Do not create roles for `Current Assets`, `Cost of Sales`, `Operating Revenue`, `Other Income`, `Other Expenses`, `Rental Income`, or report line captions. Express them as configurable classifications and layout mappings. Tax reporting and cash-flow presentation should retain their own explicit governed configuration where their semantics differ from account family behaviour.

Current literal replacements:

- cash-account selectors (`GeneralLedgerService.cs:2114-2115` and cash account UI): `Cash`/`Bank` role plus exact account controls.
- fixed-asset account families: fixed-asset roles above, with exact category/account configuration retained.
- Cost of Sales and Other Expenses report grouping: classification/layout mapping, not role.
- HR seeded category strings: Finance provisioning by stable classification code.
- dashboard grouping: classification name returned by the account/book projection, never raw legacy strings.

## Deterministic seed design

- Seed books by `(TenantCode, BookCode)`, never copied GUIDs.
- Seed classifications from a versioned manifest keyed by `(BookCode, ClassificationCode)`, with parent code, core type, default revaluation treatment, optional system role, and display order.
- Seed only approved defaults. Do not infer every asset/liability as monetary; make each leaf explicit.
- Seed GL accounts by stable account code/number; resolve book and classification IDs after lookup by code.
- Seed every enabled account/book assignment explicitly.
- Seed currency links by account code and ISO currency, then policy rows by account/book/currency codes.
- Seed identity segments separately from transaction dimensions. Initial identity segments are Company and Natural Account only.
- Seed report layouts as protected templates; publication generates membership snapshots through the same application service used at runtime.
- Make all seeders idempotent and fail on duplicate/ambiguous stable codes or core-type mismatch.
- Move Payroll/Procurement-created Finance accounts into Finance seeds or a Finance provisioning API; producer seeders should seed only their own source facts.

## Migration and reset sequence

No migrations or reset occur in Phase 0. Future sequence:

1. Add classification, snapshot, and account-book-currency policy tables and concurrency/audit fields.
2. Seed books/classifications deterministically.
3. Backfill `AccountAccountingBook.AccountClassificationId` from an explicitly reviewed mapping manifest—not fuzzy name matching.
4. Backfill policy rows from existing links with a Finance-approved rule; in reset-only local databases the final seed is authoritative.
5. Deploy code that reads new structures and validates parity while legacy columns still exist for the migration step.
6. Cut all in-repository consumers to V2 and new account contract.
7. Add central book/membership enforcement and exact reversal path.
8. Add classification layout mapping and publish snapshots before deleting legacy line-item semantics.
9. Remove flags, line-item strings, category/subcategory strings, `RevaluationRequired`, and active startup SQL that writes/creates them through forward migrations.
10. Preserve all historical migrations and the shared snapshot chain; add only new forward migrations.
11. Prove upgrade from an existing development DB and create/migrate/seed from an empty DB.
12. Commit a Development-only reset utility with exact database safeguards; establish an integration commit; developers recreate independent local databases.

Reset utility safeguards: require Development environment, explicit database name and typed confirmation; parse and display server/database; reject production-like names/hosts and broad/default targets; operate on the exact resolved database; apply the shared migration chain; run deterministic seeders; report migration and seed versions. Do not ship a transaction purge script.

## Phase-by-phase file impact

### Phase 1 — authoritative books, contract V2, central guard

- Core: Finance posting/external DTOs, interfaces, contract catalogues/evidence, Account DTOs/entities.
- API: posting engine, external/system adapters, Account/AccountingBook services/controllers, all Finance-owned request builders.
- Data: `ApplicationDbContext`, focused forward migration, Finance seeder, removal/reconciliation of `Program.cs` legacy repair SQL.
- Frontend: account book membership editor, shared accounting-book loader, removal of three flags/literal batch options.
- Other owners: Procurement, Inventory, Sales, HR producer updates in coordinated commits.
- Tests/docs: posting engine, external evidence golden vectors, adapters, account service, consumer-contract tests and catalogue.

### Phase 2 — configurable classifications

- New entity/configuration/DTO/service/controller/permissions/audit/frontend administration.
- Extend AccountAccountingBook/account forms and Finance seeder.
- Replace literal category logic in GL, Cash, Fixed Assets, reporting/dashboard and HR provisioning contract.
- Add hierarchy, cycle, tenant, code immutability, retirement, type and leaf-assignment tests.

### Phase 3 — report layout classification mappings

- Layout entity/DTO/configuration/service/import/export/execution/UI.
- New publication snapshot tables/migration and publication transaction.
- Import template/version bump and compatibility error for old invalid mapping values.
- Execution/reproducibility/overlap/fingerprint tests.

### Phase 4 — book-specific revaluation

- Policy entity/configuration/migration/DTOs, account currency UI, permissions/audit.
- Revaluation batch/line evidence, preview, math and book-specific posting/reversal.
- Close review/report changes and extensive FX test matrix.

### Phase 5 — account segments versus transaction dimensions

- Account segment DTO/controller/services/new/edit UI and seed manifests.
- Remove Finance segment `IsMandatory` input/behaviour after enforcing all active segments.
- Do not touch unrelated same-named fields in other modules.
- Account lifecycle controls for later structure changes.

### Phase 6 — journal UX and account inquiry

- Manual journal create/edit UI and focused React tests for copying description only.
- Account transaction query DTO/interface/service/controller and account detail UI states.
- Confirm Unit Journal scope separately.

## Legacy-removal ledger

| Legacy field/behaviour | Active consumers | Replacement | Owner | Removal phase | Migration consequence | Verification test |
|---|---|---|---|---|---|---|
| `IsIFRSClassified` | Account entity/DTO/service, AccountingBookService, GL/segments, Program startup SQL, Finance/Procurement seeders | enabled IFRS `AccountAccountingBook` | Finance; Procurement seed owner | 1 | backfill/verify join, drop column | create/update round-trip and posting membership |
| `IsBaseClassified` / Base-framework aliases | same; maps to `LOCAL_STATUTORY` | enabled statutory mapping | Finance | 1 | backfill/verify/drop aliases/column | statutory mapping round-trip |
| `IsLocalClassified` / Local/Management aliases | same; maps to `MANAGEMENT` | enabled management mapping | Finance | 1 | backfill/verify/drop | management mapping round-trip |
| `IFRSLineItem` | Account, GL projections, layout import fallback, settings, seeds, startup SQL, tests | book classification + layout mapping/snapshot | Finance | 3 | map explicitly then drop | published report stable after reclassification |
| `BaseLineItem` | same statutory path | same | Finance | 3 | map/drop | statutory layout execution |
| `LocalLineItem` | same management path | same | Finance | 3 | map/drop | management layout execution |
| `FinancialStatementLineItem` | account-book entity/DTO/services/import/execution tests | classification and layout row mappings | Finance | 3 | explicit conversion/drop | imports and execution need no free text |
| `AccountCategory` | Account DTO/service/search, GL reports/cash selection, ad hoc reports, dashboard, HR/Finance/Procurement seeds, startup SQL | classification ID/code/name plus SystemRole where behavioural | Finance + HR/Procurement owners | 2 | explicit manifest backfill/drop | no runtime literal category comparisons |
| `AccountSubCategory` | same, including detailed dropdown | classification hierarchy | same | 2 | explicit manifest backfill/drop | dynamic hierarchy round-trip |
| hardcoded `DETAILED_ACCOUNT_TYPES` | account new/edit pages | book-scoped API hierarchy | Finance frontend | 2 | none | both forms use server data |
| `RevaluationRequired` Boolean | AccountCurrencyLink entity/DTO/service/UI/revaluation/tests | nullable book-specific override + effective policy | Finance | 4 | approved backfill/drop | inherited/explicit per book/currency |
| Asset/Liability candidate filter | CurrencyRevaluationService | effective policy for all core types | Finance | 4 | none | five core-type matrix |
| hardcoded FX `"IFRS"` | revaluation and realized FX request builders | selected/resolved AccountingBookCode | Finance | 4 | batch history mapping if renamed | book-specific batch/post/reversal |
| Finance segment `IsMandatory` | segment entity/DTO/services/controller/new-account UI/seeds | all active account segments required | Finance | 5 | set existing definitions; then drop/ignore | exact set validation |
| `BookClassification` public request/envelope name | all Finance and cross-module request builders, JSON, evidence | `AccountingBookCode` V2 | all named owners | 1 | persisted columns may rename forward | contract compile + JSON/hash golden tests |
| `ALL_ACTIVE_BOOKS` pseudo-book | Inventory stock adjustment/opening and docs/migration; manual journal rejects | orchestrator expansion to concrete books | Inventory + Finance | 1 | none or source-record rename | engine never receives pseudo-book |
| startup Finance schema repair SQL | `Program.cs` application startup/repair command | EF migrations + deterministic seeders | Finance | 1/2 | remove only after upgrade/empty DB proofs | startup applies migrations without schema mutation fallback |
| account inquiry placeholder | account detail UI | paginated posted transaction endpoint | Finance | 6 | none | populated/empty/error/tenant isolation |

Historical migration files and Designer snapshots are excluded from removal: they remain immutable even after runtime fields disappear.

## Test matrix and minimum integration gate

| Area | Required cases | Earliest phase |
|---|---|---|
| Book master | tenant uniqueness, one default, inactive/non-posting, code immutability | 1 |
| Account mapping | create/update exact memberships, cross-tenant reject, disabled mapping, missing mapping, classification/type compatibility | 1/2 |
| Central posting | UI-independent calls, external adapter calls, unknown book, pseudo-book, mixed mapped/unmapped lines, tenant isolation, audit denial | 1 |
| Reversal | server-derived exact lines, altered account/amount/currency/dimension rejected, inactive historical mapping allowed only by lineage, duplicate reversal/idempotency | 1 |
| Contract V2 | compile every in-repo producer, JSON fixture, canonical string/hash golden vector, tamper detection, catalogue major version | 1 |
| Classification | hierarchy, cycle, duplicate code, rename, used-code/type immutability, leaf assignment, retirement/where-used | 2 |
| Layout | classification import/export/validation, descendants, overlaps, snapshot fingerprint, reclassification after publication leaves old output unchanged | 3 |
| Revaluation policy | inherited include/exclude, explicit include/exclude, permission/reason/audit, per currency, per book, warning/badge/close evidence | 4 |
| Revaluation math | all five types, debit/credit/reversing balances, prior adjustment, repeat/idempotency, reversal, rate change, inactive link/book | 4 |
| Segments | missing, duplicate, unknown, inactive, wrong position/length/type, cross-tenant, exact all-active set | 5 |
| Journal UX | first line blank, appended line copies previous description only, edit page parity, stale-state regression | 6 |
| Account inquiry | posted-only default, ordering/pagination, book filter, currencies/dimensions, true empty, error, tenant isolation | 6 |
| Reset | existing DB migrate, empty DB migrate/seed, repeat seed, stable-code references, safety refusal cases | after schema integration |

Minimum gate for every phase:

1. `git diff --check` and staged-file ownership review.
2. Core, Data and API compile.
3. Focused Finance tests for changed services plus `FinancePostingEngineTests`, `FinanceIntegrationContractFoundationTests`, and external adapter/evidence tests when contracts are touched.
4. Full `ErpSystem.Api.Tests` Finance filter.
5. Relevant `ErpSystem.Core.Tests` producer consumer tests for Procurement, Inventory and Sales.
6. Frontend type-check/build and focused Finance Jest tests when UI/contracts change.
7. SQL Server migration apply from an existing development schema and from empty; EF pending-model-change check.
8. Deterministic seed repeat and tenant-isolation probes.
9. One end-to-end ordinary posting and one exact reversal for each affected producer route.

Phase 1 cannot merge until Finance plus every compile-time producer owner is green in the same integration revision. A documentation promise to update later is insufficient for a breaking DTO rename.

## Integration and rollback plan

### Integration

- Keep this assessment as the reviewed architecture baseline.
- Finance publishes V2 DTOs, evidence vectors, classification code manifest, and branch/commit coordinates before implementation removal work.
- Owners prepare consumer commits against the same Finance contract commit; do not merge a half-renamed DTO into the shared branch.
- Use coherent commits: tests/contract, schema/domain, central guard, Finance adapters, frontend, migration/seeds, owner consumers, acceptance evidence.
- Rebase each phase on the latest primary Finance branch before review. Resolve only phase-owned files.
- Record exact migration IDs, seed-manifest version, and all commit hashes in the integration handoff.

### Rollback

- Before go-live, rollback is source rollback to the last accepted integration commit plus recreation of each developer's local DB.
- Once a forward migration has been shared, do not rewrite it. Correct it with another forward migration.
- Never roll back a posted accounting event by deleting rows; use the exact reversal path.
- If a phase fails acceptance, keep legacy runtime fields only until the corrective branch is ready; do not add a second indefinite compatibility contract.
- Published snapshots are append-only evidence. A faulty layout is retired and republished as a new version, never mutated in place.

## Answers to the eight required questions

1. **Yes.** Book-scoped classification on `AccountAccountingBook` is the best normalized fit because classification varies by book while the economic GL account remains shared. A separate assignment table would duplicate the existing join without adding meaning.
2. **Yes.** Revaluation must be Account + Currency + Book from the outset, implemented through the account-book assignment and currency link. Different book policies create legitimate book-specific closing journals and must never be cross-netted.
3. **Freeze resolved membership.** Publish an immutable mapping snapshot with resolved account IDs, frozen explanatory fields, classification code, hierarchy fingerprint, resolution fingerprint, actor and time. Published execution reads it; drafts read live hierarchy.
4. **Only exact trusted reversals.** The server derives all lines from an immutable posted event, permits historical disabled mappings solely for that path, compares lineage atomically, preserves the original book and posts into an allowed period. No client flag or arbitrary compensating entry receives the exception.
5. **Roles are behavioural families only.** Use the limited Cash/Bank/control/inventory/fixed-asset/tax roles listed above. Cost of Sales, Other Expenses, Rental Income and similar labels remain classifications/layout configuration. Exact control-account choice remains governed settings.
6. **One coordinated V2.** Rename to `AccountingBookCode`, bump catalogue major versions, domain-separate and update evidence hashing/golden fixtures, update all Finance and producer consumers in one integration train, then remove V1. Do not retain dual DTO properties.
7. **Minimum gate.** Compile Core/Data/API and all producers; run central posting, external contract/evidence, full Finance-filter, affected producer consumer and frontend tests; prove existing/empty DB migration and repeatable seed; execute ordinary and reversal E2E at one exact revision.
8. **Overlooked dependencies.** Active startup repair SQL writes legacy Account fields and creates books/joins; HR Payroll writes Accounts directly; Procurement test seeding creates Finance accounts; Inventory still recognizes `ALL_ACTIVE_BOOKS`; report screens and batch UI contain hardcoded fallbacks; FX posting hardcodes IFRS; reversal lines are not yet proven exact; external evidence hashes the misleading field. `full_database.sql` itself appears reference/generated rather than authoritative.

## Decisions requiring Finance-owner approval

1. Approve `DefaultRevaluationTreatment = Exclude` as the safe default for newly created classifications, with explicit seed values for known monetary leaves.
2. Approve whether only non-standard inclusion for Equity/Revenue/Expense needs elevated approval, or every explicit override (including exclusion) does. Recommendation: permission and reason for every override; extra visible amber warning for non-standard P&L/equity inclusion.
3. Approve whether account-book memberships need effective dates in Phase 1 or whether audited current state plus published layout snapshots is enough pre-live.
4. Approve SystemRole vocabulary/cardinality and the exact control-account boundary.
5. Approve that `ALL_ACTIVE_BOOKS` is expanded only by source orchestration and rejected by the central single-book engine.
6. Approve that server-generated exact reversal is the sole historical-mapping exception.
7. Confirm whether Unit Journal Entries should copy the previous line description in Phase 6.
8. Confirm ownership/process for regenerating or retiring the three large SQL snapshots; they should not block the runtime migration.
9. Name owners and integration window for Procurement, Inventory, Sales, and HR consumer commits.

## Phase 0 verification record

- `git diff --check`: passed.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --nologo`: passed with 0 errors and 1,089 existing compiler/analyzer warnings across Core, Data, and API.
- Focused contract command: `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --filter "FullyQualifiedName~FinanceIntegrationContractFoundationTests|FullyQualifiedName~ExternalFinanceDimensionAdapterContractTests"`.
- Focused result: 22 passed, 1 failed, 0 skipped, 23 total.
- The failure is the current-base assertion `FinanceIntegrationContractFoundationTests.DimensionRouteCatalogueShouldKeepTrustedIdentityAndOwnershipBoundariesCodeOwned`: it forbids a `CustomerPayment` route while `FinanceDimensionRouteCatalog` currently registers the Finance-owned AR customer-payment route. Phase 0 did not alter or weaken either side. Reconcile that catalogue/test expectation before using this test group as a required green Phase 1 gate.
- No database was connected, migrated, seeded, reset, or otherwise mutated.
- No production source, frontend, migration, model snapshot, or other module file was changed.

## Recommended exact Phase 1 scope

After explicit approval, Phase 1 should be limited to:

- publish and cut over `AccountingBookCode` V2, contract catalogue versions, evidence canonicalization and all in-repository producers;
- make account create/update consume `AccountingBooks` mappings as authoritative and remove the three Boolean runtime flags/aliases;
- add central book existence/status/account-membership enforcement;
- add the server-derived exact reversal exception;
- replace hardcoded accounting-book checkboxes/options with the active-book API;
- migrate/reseed books and assignments by stable codes;
- remove/reconcile active startup SQL for the migrated legacy fields;
- coordinate HR/Procurement seed ownership but defer configurable classification hierarchy, layout mappings, revaluation policy, segment enforcement and inquiry UI to their later approved phases.

Phase 1 should not begin until these decisions and producer owners are recorded. It should not include the Phase 2 classification tables merely to make the branch appear complete; if classification is required on enabled assignments immediately, combine Phases 1 and the minimal classification foundation explicitly and review that changed scope before implementation.
