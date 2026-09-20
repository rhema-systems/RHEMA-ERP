# PO descriptive lines — 8 September 2026

## Behaviour

- PO creation and draft editing allow descriptive stock, non-stock and service lines without an inventory ID.
- Line type is explicit and retained. Existing rows remain stock; no historic commercial values or approvals are changed.
- Catalogue selections must be active, tenant-owned and compatible with the selected line type.
- Stock mapping/creation and location are required when receiving stock, not when preparing the PO.
- Non-stock/service acceptance does not post inventory. Service lines retain a completion/acceptance evidence reference and independent acceptance controls.
- Approved-source quantities, descriptions, prices, units, supplier, currency and cumulative capacity remain enforced.
- No Finance implementation files changed. This is not certification of the full service-invoicing workflow.

## Verification

- 54 frontend tests passed, including unmapped draft-save request wiring for stock, service and non-stock, plus existing EACH source terms.
- 19 backend tests passed, including mixed stock/non-stock acceptance totals and no inventory dependency calls for non-stock/services.
- Focused TypeScript check passed for all changed pages and their imported dependencies. The full-repository type check was stopped; it is not reported as passed.
- API build passed with zero errors.
- Production frontend build passed. Build ID: `iMAKwllsrTzlmZ_cvZ-DL`. All nine changed frontend runtime files were hash-checked against the build input.
- Disposable SQL Server regression suite passed: unmapped classifications save and retain commercial capacity checks. Test database removed after completion.
- Additive migration `20260908170000_ClassifyPurchaseOrderLines` applied to the verified local UAT database with historic line count/classification checks.

## Deployment boundary

The automated backend restart was blocked by local tool policy. The old live backend and frontend were left running. The new behaviour must not be treated as live or browser-verified until both prepared runtimes are started and the PO save/reload is checked.

Prepared backend: `local-artifacts/po-line-types-api-20260908`.

Prepared frontend: `local-artifacts/frontend-production-po-line-types-20260908` (successful production build).

### Manual restart

Stop only the existing local ERP processes listening on ports 5000 and 3000. The verified old owners were API PID 56720 and frontend PID 52604; recheck ownership before stopping any process. Do not stop other projects.

Start the backend in one PowerShell terminal:

```powershell
Set-Location -LiteralPath 'D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\local-artifacts\po-line-types-api-20260908'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
.\ErpSystem.Api.exe --urls http://localhost:5000
```

Start the frontend in a second PowerShell terminal:

```powershell
Set-Location -LiteralPath 'D:\DEVELOPMENTS\ASP.NET\TDC\DEV\erp-system\erp-system - Aug2\local-artifacts\frontend-production-po-line-types-20260908'
node node_modules/next/dist/bin/next start --port 3000
```

The API runtime retains its existing configuration and upload folder; no re-upload is required. Keep both old runtime folders for rollback. Do not remove the additive database column on rollback.

### Browser verification still required

After restart, sign in as `procurementofficer`. Open a draft PO, edit a line and confirm the line-type selector and **Ad hoc — no catalogue item** choice. Save the draft with its approved description, quantity, unit and price, then reload and verify the line remains unmapped with its chosen type. Do not change Barcode/PVC into services merely to evade stock receiving; they remain stock goods in this UAT.

Do not mark live UAT or the complete service-receipt/invoicing flow passed based solely on the automated tests above.

Keep PO-2026-0003 (`a7393eec-565c-4ff5-8346-184315d8bc95`) as Draft. It was not resubmitted or duplicated by this fix. The retained stock mappings and commercial values have not been edited.
