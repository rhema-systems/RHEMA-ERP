# TDC Finance Signed Period-Close Pack

## Purpose

This slice implements WP3 build item 6: a printable Finance-owned evidence pack for every
approved numbered period-close cycle. It extends the existing generic document-output service and
QuestPDF Finance builders; it does not create a second close workflow or reporting store.

The pack is identified by `FinanceCloseCycle.Id`, not only by fiscal period. This is essential for
TDC because reopening marks the signed cycle as historical and the next close starts a new cycle.
Every prior certificate remains independently reproducible.

## Output and access

- Document type: `Finance.ClosePack`.
- API: `GET /api/documents/Finance.ClosePack/{financeCloseCycleId}?format=pdf&copyType=Original`.
- Supported copy labels: `Original` and `Reprint`; unrecognised labels safely normalize to
  `Original`.
- Permission: `Finance.Reports.Export`.
- UI: the Fiscal Periods row displays **Close pack** when the user has the export permission and
  an approved closed or reopened cycle exists.
- Tenant isolation is rechecked inside the builder even though the data context already applies
  tenant filters. Knowing another tenant's cycle ID is insufficient to render its pack.

## Pack contents

The generated PDF includes:

1. TDC/tenant identity, period, fiscal year, cycle, approved template version and final evaluation.
2. The system-recorded preparer and approver electronic signatures: names, immutable user IDs,
   declarations and UTC timestamps.
3. The final immutable control results and measured exception count/amount.
4. A focused reconciliation summary for posting integrity, trial balance, AP/AR and other
   reconciliation checks.
5. The complete copied close checklist, owner/completer, due/completion state and evidence count.
6. Final exceptions plus the complete request/review history of every eligible waiver.
7. Controlled file metadata without leaking physical storage paths.
8. Due-soon, overdue and approval-escalation delivery audit.
9. All numbered close, reopen and re-close cycles for the period.
10. A deterministic SHA-256 evidence digest and the final status of non-waivable integrity checks.

The PDF says explicitly that the attestations are system-authenticated electronic signatures, not
scanned handwriting or a third-party cryptographic signing certificate. This avoids overstating
the control while preserving the authoritative maker-checker evidence already recorded by the
close service.

## Evidence integrity

The printed SHA-256 digest is generated from an explicitly ordered canonical stream containing the
cycle, period, certificate, tasks, final snapshots, file metadata, waivers, alerts and cycle
history. Strings are JSON-escaped before hashing. Generation timestamp, PDF metadata and copy
label are excluded, so an unchanged Original and Reprint produce the same evidence digest.

Every render also records `Finance.AccountingPeriod.ClosePackGenerated` with:

- document number and copy type;
- cycle and period identity;
- printed evidence digest;
- SHA-256 hash of the particular emitted PDF bytes; and
- generator identity/timestamp supplied by the central Finance audit service.

## FIN-LIM-0034 preservation

The pack does not implement or relax waiver decisions. The existing close service remains the
only policy boundary. It rejects generic waivers for:

- `POSTING_INTEGRITY`;
- `TRIAL_BALANCE`;
- `AP_CONTROL_RECONCILIATION`;
- `AR_CONTROL_RECONCILIATION`;
- `RECURRING_JOURNAL_EXCEPTIONS`; and
- `FIXED_ASSET_DEPRECIATION`.

The final section prints these results and states that an applicable mandatory failure blocks both
certification and pack generation. This makes the `FIN-LIM-0034` resolution reviewable in the
signed close artifact itself.

## Validation rules

Rendering fails when:

- the cycle is outside the authenticated tenant;
- the cycle is not `Closed` or historical `Reopened`;
- either maker or checker identity/time is missing;
- maker and checker are the same user;
- no final immutable evaluation exists; or
- the retained final evaluation contains a mandatory failed control.

No schema migration is required. All source evidence was introduced in the preceding WP3 slices.

## Verification

`FinanceClosePackDocumentBuilderTests` covers PDF generation, stable evidence digest across repeat
renders, export auditing, authorization mapping and cross-tenant rejection. The test supports an
opt-in `TDC_CLOSE_PACK_QA_OUTPUT` path so the production QuestPDF output can be rendered to images
and inspected without writing artifacts during ordinary test runs.

Business UAT should close a pilot period, download the pack, compare the certificate to the two
users' declarations, verify exception/evidence references and then reopen/re-close the period to
confirm that both numbered packs remain available.
