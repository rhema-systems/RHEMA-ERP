# UAT database rebuild ledger — 2026-09-27

## Authority and scope

- Authorized by the user to back up, reset/recreate, migrate, reseed, and verify `RHEMAERP_BOOKV2_UAT_20260922`.
- SQL Server: `RHEMA-AKWASI\EXPRESS22`.
- No other persistent database was in scope.
- Source was pinned to clean `origin/master` commit `a818d13d34b56cddecdef6e2232dd8ca28752f5c`.

## Pre-cutover evidence

- Original database ID: `47`.
- Original database GUID: `63c6d15d-0948-4473-b52e-f3d54d897900`.
- Original migration history: 27 rows, ending at `20260925051637_CanonicalCollectionBusinessPartnerIdentity`.
- Original business evidence: one tenant and three accounting books.
- The original history diverged from the current master migration chain, so in-place migration was rejected.

## Backups and restore proof

- Initial verified backup:
  - `C:\Program Files\Microsoft SQL Server\MSSQL16.EXPRESS22\MSSQL\Backup\RHEMAERP_BOOKV2_UAT_20260922_pre_rebuild_20260927_085539.bak`
  - Size: 125,886,464 bytes.
  - SHA-256: `E2017F3B73561A4747AF8D1FAFFD2CCEC5FC5CF7552262C69D7683DBA8626566`.
- The initial backup was restored under the disposable name `RHEMAERP_BOOKV2_RESTORE_DRILL_20260927_0900`, passed `DBCC CHECKDB`, reproduced the 27-migration/three-book/one-tenant fingerprint, and was removed.
- Final quiescent backup immediately before reset:
  - `C:\Program Files\Microsoft SQL Server\MSSQL16.EXPRESS22\MSSQL\Backup\RHEMAERP_BOOKV2_UAT_20260922_final_pre_rebuild_20260927_092849.bak`
  - Size: 125,751,296 bytes.
  - SHA-256: `495010BE57F75DE49277B45A70643CFA2B5BFA6B36E53AB9D10F0BF883506B33`.
  - SQL backup-set UUID: `0decae45-85ae-4e2a-94a2-066e85e6397e`.
  - `COPY_ONLY`, checksum, and `RESTORE VERIFYONLY` all passed.

## Rehearsal

- Disposable database: `RHEMAERP_BOOKV2_REHEARSAL_20260927_0915`.
- Applied the compiled migration assembly and ran full seeding twice.
- `DBCC CHECKDB` passed.
- Migration count: 60.
- Migration tail: `20260927021852_InventoryIssueActualReceipts`.
- Ordered migration manifest SHA-256: `B210ABBE4799C6A1B7114098EF6C30DF3BAA94D5D1210D85737C6AF944A29B19`.
- Rehearsal was removed after UAT comparison.

## Cutover result

- Guarded destructive SQL asserted the exact server, database name, database ID, database GUID, session quiescence, migration count, and migration tail before dropping anything.
- UAT was recreated with its original owner `RHEMA-AKWASI\Akwas`, collation `SQL_Latin1_General_CP1_CI_AS`, and `SIMPLE` recovery.
- All 60 current migrations applied successfully.
- Full database seeding completed successfully.
- Post-cutover `DBCC CHECKDB` passed.
- UAT migration manifest exactly matches rehearsal.

## Semantic verification

- One valid perpetual/default GHS `BASE` Primary book.
- One active `IFRS_ADJUSTMENTS` Delta based on `BASE`.
- One active USD `USD_PARALLEL` book based on `BASE`, with replication start metadata.
- One tenant and three accounting books.
- Eleven canonical Business Partners, including three `TDC-DEMO-SUP-*` and three `TDC-DEMO-CUS-*` records.
- Three AP profile versions, three AR profile versions, and two AP WHT defaults.
- Zero orphan Business Partner roles, AP profiles, or AR profiles.

## Runtime verification and known warnings

- API started from the pinned master build at `http://127.0.0.1:5013`.
- `/health/live` returned Healthy.
- Database, startup, and self checks returned Healthy.
- Aggregate readiness remained Unhealthy only because the local file-virus-scanner dependency was unavailable; memory also reported Degraded.
- Three demo bank-account seed records were skipped because GL codes `100-1001-0000`, `100-1002-0000`, and `100-1003-0000` are absent. This warning was identical in rehearsal and UAT.
- Build completed with 0 errors and 663 existing warnings.

## Rollback

If rollback is required, stop the API, drop only the rebuilt `RHEMAERP_BOOKV2_UAT_20260922`, restore the final verified backup with recovery to the exact original name, restore owner `RHEMA-AKWASI\Akwas`, and rerun `DBCC CHECKDB` plus the original 27-migration/three-book/one-tenant fingerprint checks before reopening access.
