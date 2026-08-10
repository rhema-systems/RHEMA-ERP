# Fixed Asset GL Reclassification Foundation

Date: 9 August 2026
Limitation resolved: `FIN-LIM-0038`

## Outcome

The existing fixed-asset transfer workflow now supports a controlled `GlReclassification` transfer. An authorised maker can propose a new fixed-asset category and/or reporting segment, an independent checker approves it through the shared workflow, and the central Finance posting engine moves the asset's current GL balances before the asset master is changed.

The implementation deliberately builds on `AssetTransfer`, the existing workflow service, Finance permissions, `IFinancePostingEngine`, fiscal-period controls, document numbering and Finance audit services. It does not create a second transfer register or a direct journal writer.

## TDC requirement fit

- `FR-FA-001`: the fixed-asset register retains classification, custody and accounting history.
- `FR-FA-010`: the asset register and GL remain reconcilable by category, account, segment and accounting book.
- `FR-GL-009`: the transfer retains journal and posting-event lineage.
- `FR-GL-010`: historical journals are never edited; reclassification is a new balanced journal.
- `SRS-CONTROL-004`: request and approval are segregated.
- `FIN-LIM-0038`: current cost, accumulated depreciation, accumulated impairment and revaluation-surplus balances can now be moved safely between asset categories/accounts and/or reporting segments.

Representative TDC data UAT and migration deployment remain release gates; they are not missing product features.

## Operator workflow

1. Open **Finance > Fixed Assets > Asset Transfers**.
2. Choose **GL Reclassification (Finance)** and select the asset.
3. Select a target asset category and/or enter a target segment.
4. Confirm the posting accounting book and accounting date, then enter a substantive reason of at least 20 characters.
5. The server validates the open fiscal period, same-tenant configuration, posting accounts and current asset balances before creating the request.
6. The shared `AssetTransfer` workflow routes the request to an authorised independent checker.
7. On final approval, Finance rechecks the approved snapshots, posts the balanced journal, and only then changes the current category/segment on the asset master.
8. The transfer retains the journal entry, posting event, balance evidence, category/account evidence, requester, approver and timestamps.

If a transient dependency fails after approval, the transfer stays `Approved`, records the failure and can be retried through the same approval action. Posting idempotency prevents a second journal. If the asset balance, master classification, category account mapping, or posting-book configuration has changed, the now-stale approval is cancelled so the maker can submit a fresh evidence snapshot for a new checker decision.

## Accounting basis

The request freezes the current posting-book balances rather than using the asset's displayed net book value as a single reclassification amount:

| Balance | Snapshot basis | Reclassification entries |
|---|---|---|
| Gross asset carrying account | Book acquisition cost plus posted, uncorrected revaluation asset adjustments | Debit target asset account/segment; credit source asset account/segment |
| Accumulated depreciation | Current accounting-book accumulated depreciation | Debit source accumulated-depreciation account/segment; credit target |
| Accumulated impairment | Posted impairment loss less posted impairment reversal | Debit source accumulated-impairment account/segment; credit target |
| Revaluation surplus | Posted surplus less surplus already applied | Debit source revaluation-surplus account/segment; credit target |

An account-only category move, a segment-only move, or a combined category-and-segment move uses the same rules. Zero balances do not create empty lines. Historical capitalization, depreciation and valuation journals and schedules are never rewritten.

## Control model

- **Dedicated authority:** `Finance.FixedAssets.Reclassification.Request` and `Finance.FixedAssets.Reclassification.Approve` protect the financial transfer paths without changing ordinary physical-transfer access.
- **Strict maker-checker:** the employee who requests a GL reclassification cannot approve it.
- **Tenant isolation:** the asset, target category, accounting book, fiscal period and every posting account must belong to the active Finance tenant.
- **Account suitability:** required accounts must be active, direct-posting accounts of the expected Asset or Equity type.
- **Period control:** an open, unlocked period is required at request time; the central posting engine enforces the canonical `FIN` module lock again at posting.
- **Immutable approval evidence:** source and target categories/accounts, book, period, segment and four balance classes are retained on `AssetTransfer`.
- **Configuration-drift refusal:** changed category account mappings invalidate the approval and require a fresh request.
- **Balance-drift refusal:** depreciation or valuation after approval invalidates the frozen amounts and requires a fresh request.
- **Stale-approval retirement:** evidence or posting-book drift cancels the obsolete approval so it cannot be retried and does not block a replacement request.
- **Serializable completion:** on relational databases, balance recheck, journal posting and master-data update are one serializable accounting unit.
- **Single current posting book:** category reclassification fails closed unless the asset has exactly one active posting book. This matches the present TDC baseline and avoids changing a global category after moving only one of several ledgers.
- **No-op refusal:** the request must change an account mapping or reporting segment.
- **Retry safety:** the posting request has a stable transfer-scoped idempotency key and an approved failure remains retryable.

## Data contract and migration

Migration `20260809223000_AddFixedAssetGlReclassificationControls` extends `AssetTransfers` with:

- source/target fixed-asset category references;
- accounting-book and book-classification evidence;
- source/target asset, accumulated-depreciation, accumulated-impairment and revaluation-surplus account references;
- frozen current balances for those four classifications; and
- restricted foreign keys and tenant-oriented indexes.

Ordinary physical transfers leave the optional financial evidence empty and continue using the same table and services.

## Verification

The focused `FixedAssetTransferFoundationTests` suite covers 19 transfer scenarios, including:

- a successful category/segment reclassification with a balanced four-line journal;
- preservation of historical measurement evidence;
- strict maker-checker rejection;
- approved-balance drift refusal;
- posting-book and category-mapping drift cancellation;
- closed-period, cross-tenant asset/category/custodian/segment and invalid-segment controls;
- workflow failure behavior; and
- physical transfer idempotency and current-custody updates.

Frontend scoped ESLint passes for the Asset Transfers workspace and fixed-asset TypeScript contract. The Data project, including the EF model snapshot, builds with zero errors; the API and test projects compile; and all 19 focused backend scenarios pass. The repository-wide frontend type-check still reports pre-existing Inventory and Reports errors, with no error in either changed Finance frontend file.

## Demo highlights

- One workspace handles physical and financial asset moves without creating competing registers.
- The approval screen can explain exactly which gross and contra balances the checker authorised.
- The journal moves all related asset balance classes together, preventing cost from moving while accumulated depreciation is left behind.
- The asset category changes only after the central Finance posting succeeds.
- A timeout or temporary error leaves an approved, auditable, retry-safe request rather than a half-moved asset.
- Historical measurement and journal evidence remains intact, making the before/after classification easy to defend to Internal Audit and stakeholders.

## Scope boundary

This slice implements whole-asset current-balance reclassification for the current TDC single-posting-book baseline. It does not implement partial-asset transfers, multi-posting-book category changes, procurement/stores interfaces, or legacy-data conversion. Those are not inferred as part of `FIN-LIM-0038`.
