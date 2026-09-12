# Required issue and receipt locations — 10 September 2026

## Rule

- Requester: storage location is optional; Stores selects it at fulfilment.
- Issuer: every positive issue line must specify an active location in the approved warehouse and current tenant. A default can fill lines, with per-line overrides. There is no unbinned/warehouse-level issue option.
- Stock receipt: every stock line requires a storage location. Acceptance into stock also rechecks that the location is active. Service/non-stock receipt lines do not require an inventory bin.
- Existing issued quantities, return history and stock movements are not changed by this correction. No migration is needed.

## Reported record

Read-only rehearsal inspection found that REQ-20260910-0002 and SIV-20260910-00002 already retain LOC-001 / Main on the issued line. The requester header location is null, which is valid. The detail mapper relied on an unloaded item-location navigation, so the frontend incorrectly displayed “Warehouse level”.

The mapper now resolves the saved location IDs within the current tenant. Historical location labels remain readable even if a location becomes inactive. Missing historical locations are not labelled as warehouse-level stock.

## Scope and verification

Source changes are in the regular checkout. Only rehearsal ports 3002/5002 are to be refreshed for this test; main UAT processes and databases are not to be changed. The UAT walkthrough B18 has been updated and supersedes the earlier optional-unbinned guidance in INVENTORY_ISSUE_LOCATION_20260910.md.

Focused regression evidence and runtime checks are retained under `local-artifacts/requisition-location-required-20260910`.

## Tested and loaded

- 58 focused backend tests passed; 32 focused UI tests passed; scoped TypeScript validation passed.
- Backend compiler input checksums match both updated services. Core runtime SHA-256: `9ABED86DEFA3EEC52C257BEB794A7B643E156DECCE262CA03ACE5CCDFF5E7A25`.
- Rehearsal production frontend build: `ssMz17vAFtvm2nkFudhnL`; port 3002 process 14616. Rehearsal API port 5002 process 22376.
- Main UAT stayed on the same processes: 3000 / 13628 and 5000 / 42148. No database migration or stock transaction was performed.
- Read-only SQL after deployment still shows the original two-unit SIV at LOC-001 / Main and the optional requester-header location as null.
- Visible browser verification as manager on rehearsal: REQ-20260910-0002 → Items shows **LOC-001 - Main**, Requested **2**, Approved **2**, Issued **2**, Returned **1**, Net issued **1**, status **Issued**. The wider fixed-height dialog displays every column without clipping at the current viewport. The corrected Items tab was left open for the user.
