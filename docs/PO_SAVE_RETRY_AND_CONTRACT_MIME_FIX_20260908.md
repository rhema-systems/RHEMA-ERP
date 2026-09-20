# PO draft save retry and contract MIME-type fix

## Cause and correction

The PO creation endpoint already executed its source reservation and transaction through
the SQL Server execution strategy. Draft editing opened a serializable transaction without
that wrapper. The first SQL query inside source reservation therefore failed when retries
were enabled.

Draft editing now runs the full attempt through the existing unit-of-work execution strategy.
The attempt rolls back with a non-cancelled cleanup token and clears failed tracked changes
before retry. Source validation still returns 422; authorization remains enforced.
The response is read before commit so a failed response query cannot replay a committed update.
No Finance implementation files were changed.

## Contract document migration

Migration: `20260908200000_WidenContractDocumentContentType`.

- Entity annotation and model snapshot: nullable `nvarchar(255)`.
- Up and Down guard against truncating stored MIME types.
- Local UAT had already been manually widened to `nvarchar(max)`, while the model still declared 50.
- Applied only this migration to local `RHEMA-MICHAEL\SQL2017 / RhemaERP` using the target-guarded
  `scripts/procurement/Apply-LocalUatContractDocumentContentType.ps1`.
- Verified nullable column length 510 bytes, migration history recorded, and repeat application safe.
- All three existing document rows remain. Their largest stored MIME type remains 30 bytes.

## Verification

- API build: passed, zero errors (`local-artifacts/po-update-retry-build.log`).
- Five focused tests passed (`local-artifacts/po-update-retry-tests.log`):
  - Existing create/source reservation rollback guard.
  - Real SQL retry-enabled controller saves with unmapped stock, service and non-stock lines.
  - Injected transient failure after line write: rollback, retry, one retained line and one committed save.
  - Source rejection: 422 with rollback and no committed mutation.
  - Actual migration SQL: PDF/null preserved, Word MIME type retained, downgrade truncation refused.
- SQL tests used isolated disposable probe databases and the actual PO controller/provider strategy,
  with mocked business repositories. They are not a substitute for the final live-browser save.
- All disposable fixture databases were removed.
- Existing PO-2026-0003 remains Draft, GHS 52,000, two active stock lines; it was not edited,
  submitted, approved or duplicated by these tests.

## Backend handoff

Prepared runtime: `local-artifacts/po-update-retry-api-20260908`.
The four application assemblies match the successful build. Configuration and storage are
retained; uploads link to the original storage folder. Keep older runtime folders.

The frontend does not need rebuilding or restarting. The existing backend process has not
been stopped by this fix. In its terminal, press Ctrl+C, then run:

```powershell
Set-Location -LiteralPath 'D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\local-artifacts\po-update-retry-api-20260908'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
.\ErpSystem.Api.exe --urls http://localhost:5000 --SkipStartupInitialization true
```

The startup flag is for this existing local UAT database: the targeted migration has already
been applied, and unrelated startup migrations/seeding should not be rerun for this hotfix.
It is not a replacement for normal migration deployment to other environments.

After the backend is listening, sign in as procurementofficer, open existing PO
`a7393eec-565c-4ff5-8346-184315d8bc95`, and click Save Changes without changing its approved
commercial terms. Reload and confirm Draft, GHS 52,000, Barcode Device Kit 20 EA at 700,
PVC Pipe 50mm 20 EACH at 1,900. A live Save Changes pass is still pending.
