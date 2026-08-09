# Fixed Asset Impairment Reversal and Valuation Correction

Date: 2026-08-09

This Finance-only slice resolves `FIN-LIM-0035` and `FIN-LIM-0037` by extending the existing valuation service. It does not create a second asset ledger or edit posted accounting history.

## Impairment reversal

An impairment reversal must identify one active posted impairment for the same tenant, asset, and accounting book. The service derives that impairment's remaining balance after prior posted reversals. The requested post-reversal NBV cannot exceed either:

- current NBV plus the outstanding source impairment; or
- the accountant-supported carrying amount that would have existed without the impairment, net of depreciation.

The request also requires valuation method and report-reference evidence. Posting debits accumulated impairment and credits the category's impairment-reversal income account through `IFinancePostingEngine`. Bulk impairment reversal is intentionally prohibited because each asset requires separate source and ceiling evidence.

## Incorrect posted valuation

Finance selects **Correct** on a posted valuation and records both the error reason and downstream impact assessment. The original valuation remains posted and immutable.

1. A maker submits the correction request.
2. A different authorised user approves or rejects it with a substantive review comment.
3. Posting revalidates the common Finance reversal-date policy and latest accounting state.
4. The central posting engine creates a linked compensating journal from the original posting event.
5. The asset book restores the exact pre-valuation NBV and useful-life snapshots.
6. Both original and reversal journal lineage remain visible in `AssetValuationCorrections` and the Finance audit trail.

Correction is blocked if the valuation is not posted, is already corrected, its lineage is incomplete, its book no longer equals the recorded post-valuation NBV, or later active valuation/depreciation must be unwound first. An approved request remains retryable after a transient posting failure.

## Permissions

- `Finance.FixedAssets.Valuation.Reversal`: request and post the correction.
- `Finance.FixedAssets.Valuation.Reversal.Approve`: independently approve or reject it.

## Migration

`20260809193000_AddFixedAssetValuationCorrectionControls` adds the impairment lineage/cap snapshots and the correction register. Source integration does not apply this migration to a database automatically.

## Verification

Focused tests cover successful capped impairment reversal, rejection above the IAS 36 ceiling, linked debit/credit accounts, independent correction approval, compensating journal lineage, and exact book restoration.
