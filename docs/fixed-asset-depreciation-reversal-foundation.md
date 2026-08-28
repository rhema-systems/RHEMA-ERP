# Fixed Asset Depreciation Reversal And Correction Foundation

Date: 2026-08-09

Requirements/limitations: `FR-GL-008`, `FR-GL-010`, `FR-FA-004`, `FR-FA-005`, `FR-FA-010`, `FIN-LIM-0033`

## Intent

This slice gives TDC Finance a controlled way to correct an incorrect posted depreciation run. It does not edit or delete the original run, schedule, journal, or posting event. An authorised maker submits a reason and impact assessment, a different authorised reviewer approves or rejects it, and the shared Finance posting engine creates a dated compensating journal. Only then may Finance calculate a corrected revision for the same asset, book, and period.

This builds on the existing depreciation, reversal-policy, posting-engine, fiscal-period, audit, and fixed-asset-book infrastructure. It does not create a parallel ledger or reversal subsystem.

## Control Flow

1. Finance selects a posted depreciation run.
2. The service verifies complete original journal/posting-event lineage and confirms that no existing correction is awaiting action.
3. The maker records a reversal date, reason, and impact assessment. The shared reversal-date policy validates open/closed-period treatment.
4. A different user with the approval permission independently reviews the request.
5. At posting, the service rechecks period policy and downstream accounting dependencies.
6. `IFinancePostingEngine` derives the compensating debit/credit lines from the immutable original posting snapshot and posts them idempotently.
7. The asset-book register is restored to the line's before-depreciation values, while the original schedule remains `IsPosted = true` and gains explicit reversal lineage.
8. A negative `DepreciationReversal` asset movement proves the subledger correction.
9. A corrected same-period run receives the next `CorrectionSequence`, retaining every earlier revision.

## Accounting Behaviour

The original depreciation entry remains:

- Dr depreciation expense
- Cr accumulated depreciation

The correction posts the exact inverse through the central posting engine:

- Dr accumulated depreciation
- Cr depreciation expense

The original and reversal journals are linked by the existing Finance reversal mechanism. Current-balance depreciation reports and period-close completeness checks exclude reversed schedules, but history and audit views retain both sides.

## Dependency And Immutability Guards

The workflow refuses a reversal when:

- a later unreversed depreciation schedule exists for an affected asset/book;
- a posted valuation or impairment depends on the carrying value;
- an active disposal depends on the depreciation;
- a posted GL reclassification exists;
- the current book snapshot no longer equals the run's posted after-depreciation snapshot;
- the original run has incomplete or inconsistent journal/posting-event lineage; or
- the request is not independently approved.

These checks run at request, approval, and posting where relevant. They prevent an approval prepared against stale accounting state from bypassing a newly created dependency.

## Security And Audit

Permissions:

- `Finance.FixedAssets.Depreciation.Reverse`
- `Finance.FixedAssets.Depreciation.Reversal.Approve`

The requester cannot review their own request. Audit events cover request, approval, rejection, blocked/failed posting, and successful reversal. The register retains maker, reviewer, dates, comments, failure evidence, original lineage, and reversal lineage.

## Schema

Migration `20260809122817_AddFixedAssetDepreciationReversalControls` adds:

- `FixedAssetDepreciationReversals`;
- correction sequence fields on run and schedule records;
- additive reversal status, date, journal, posting-event, and request lineage on schedules; and
- a revised uniqueness key that permits sequential corrected revisions without deleting history.

The migration is hand-scoped to the Finance change. The model snapshot contains only the related model additions; unrelated model drift exposed by Debug EF scaffolding was deliberately excluded.

## Verification

Focused trait: `Batch=FinanceGoLive-FixedAssetDepreciationReversal`.

Covered scenarios:

- approved correction posts a compensating journal and restores book values;
- the original posted schedule remains immutable and visibly reversed;
- a corrected same-period run receives revision one;
- requester self-approval is rejected; and
- later depreciation blocks unsafe out-of-order reversal.

## Scope Boundary

This resolves depreciation run reversal/correction under `FIN-LIM-0033`. It does not itself add
new depreciation calculation methods (`FIN-LIM-0031`), valuation/impairment correction
(`FIN-LIM-0037`), automatic partial-period depreciation on disposal (`FIN-LIM-0039`), or the
separately delivered Procurement capitalization adapter (`FIN-LIM-0028` / FIN-INT-007).
