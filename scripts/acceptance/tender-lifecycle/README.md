# Tender browser lifecycle fixture

This fixture supports the real SQL Server, authenticated Playwright tender lifecycle. It is intentionally fail closed and is not a production-data utility.

## Safety and ownership

- The database name must match `RhemaERP_TenderBrowser_<32 lowercase hex characters>`.
- `seed-tender-e2e` supplies disposable users, suppliers, budgets, approved PRs, reference configuration, and four scenario families: supplier lifecycle, no committee, draft committee, and active committee.
- `Invoke-TenderLifecyclePrepare.ps1` obtains every release and locked sourcing case through the running API as the Procurement Officer. Only after the exact disposable-database guard and real case linkage succeed, it adds the minimum advertised NCT control, document-issue register, and sealed-submission receipts needed to start the browser-owned public-opening lifecycle.
- The fresh PR-to-tender branch binds its exact Published document template, creates the supplier invitation, and issues the controlled document through the normal authenticated APIs after the browser creates and independently approves the tender. Its publication test cannot skip these prerequisites, and SQL verification requires the resulting register, issuance, invitation, dates, and statutory control to agree.
- Preparation supplies a clearly labelled, independently approved alternate-award prerequisite and a real second-tenant Tender only inside the exact disposable Testing database; browser/API calls must create the PO and prove tenant denial.
- `Invoke-TenderLifecycleVerify.ps1` is SQL read-only. It validates tenant ownership, unique numbers and IDs, requisition/release/case registration, payment posting, immutable controlled-opening/evaluation evidence, Head-of-Procurement to ETC approval separation, award, executed-contract evidence, and bidder acceptance.

## Runner

From the repository root:

```powershell
pwsh -File .\scripts\acceptance\Invoke-TenderLifecycleBrowserAcceptance.ps1
```

The runner creates the uniquely named disposable database files in the configured SQL data directory (default `D:\SQLDATA`) and later drops that database. The historical fresh-database migration chain currently fails before its later baseline migration, so this harness creates the current EF model and stamps the matching migration set through the guarded `seed-tender-e2e` command. That is sufficient for lifecycle/browser verification but does **not** close the separate fresh-migration regression gate. The runner builds services serially, seeds the Testing-only fixture, starts the API before preparation, runs Chromium with one worker, and performs final SQL verification.

Success requires all three markers:

```text
TENDER-LIFECYCLE-PREPARED|...
TENDER-LIFECYCLE-VERIFIED|Complete|...
TENDER_BROWSER_E2E_PASS
```

## Manifest and result contract

Preparation writes the non-secret `tender-lifecycle.fixture.json` consumed through `TENDER_E2E_FIXTURE`. Playwright writes `tender-lifecycle.result.json` through `TENDER_E2E_RESULT`. Both use `schemaVersion: 1` and the same `runId` and tenant.

The opposite award handoff and foreign-tenant probe are mandatory manifest inputs. `-Stage Complete` requires the browser-captured PO identity, one-time source consumption, complete recovered source lineage, controlled foreign read/mutation denial, and zero foreign revisions before it can emit the completion marker.

The prepare/verify scripts can be parsed without connecting to SQL:

```powershell
$errors = $null
[void][System.Management.Automation.Language.Parser]::ParseFile(
  '.\scripts\acceptance\tender-lifecycle\Invoke-TenderLifecycleVerify.ps1',
  [ref]$null,
  [ref]$errors)
$errors
```
