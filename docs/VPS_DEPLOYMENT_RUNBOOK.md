# Rhema ERP VPS Deployment Runbook

Last updated: 2026-09-26

This note captures the VPS deployment details that have caused repeat failures. Read it before deploying to the current Windows VPS. The server is presently a **test server**, so development-data seeding is intentionally enabled. It must be disabled before this host is promoted to production.

## Current VPS Shape

- Public app URL: `https://63.141.230.56`
- Public API entrypoint for browsers: `https://63.141.230.56/api`
- SSH endpoint: `63.141.230.56:2222`
- SSH user: `Administrator`
- Working SSH identity from the deployment workstation: `%USERPROFILE%\.ssh\id_rsa`
- Windows deployment root: `C:\RhemaERP`
- API runtime: `C:\RhemaERP\api`
- Frontend runtime: `C:\RhemaERP\frontend`
- API local port: `http://127.0.0.1:5000`
- Frontend local port: `http://127.0.0.1:3001`
- Package drop folder: `C:\RhemaERP\packages`
- Deployment/log folder: `C:\RhemaERP\logs`
- Rollback snapshots: `C:\RhemaERP\backups`
- Windows services:
  - `RhemaERPAPI`
  - `RhemaERPFrontend`
  - `RhemaERPCaddy`

The browser must never be sent to `http://localhost:5000`, `https://localhost:53484`, or `https://localhost:7095` in a deployed build.

## Preferred Automated Workflow

Use the repository deployment command for normal test-VPS releases. It turns the
manual procedure in this runbook into fail-fast, timed stages and records evidence
for each run:

```powershell
# Validate the current VPS without building or changing the deployed application
pwsh -File .\scripts\Deploy-RhemaVps.ps1 -Environment Test -DryRun

# Deploy the current clean origin/master commit. Reuse the package only when its
# commit and SHA-256 manifest both match; otherwise build a fresh package.
pwsh -File .\scripts\Deploy-RhemaVps.ps1 -Environment Test -ReuseVerifiedArtifacts
```

The command performs these controls automatically:

- requires a clean, exact `origin/master` commit unless an explicit diagnostic
  override is supplied;
- runs VPS/service/configuration/database preflight checks before spending time
  on a build;
- compares repository and deployed EF migration IDs and refuses unprobed pending
  migrations that contain SQL `THROW` guards;
- publishes the self-contained API with the full build and builds a clean production
  frontend using the public HTTPS origin;
- creates commit-keyed packages, verifies their hashes, and reuses them safely;
- creates and verifies application and SQL backups before changing services;
- applies the API and frontend with rollback on readiness failure;
- verifies services, migrations, foreign keys, public routes, static assets,
  CORS, direct-port isolation, and the required browser journeys;
- writes JSON evidence with the duration and result of every stage under
  `artifacts\vps-releases\<short-sha>` and uploads successful deployment evidence
  to `C:\RhemaERP\logs`.

The normal deployment target is 15-30 minutes when a build is required and 5-15
minutes when verified artifacts are reused. Stop and investigate a stage when it
exceeds its normal range; do not restart the whole deployment speculatively. API
readiness may legitimately use several minutes while EF migrations apply, but is
bounded by `-ApiReadyTimeoutSeconds` (1800 seconds by default).

`-AllowDirtyWorktree` and `-AllowNonRemoteHead` are diagnostics/emergency
overrides, not normal release options. They prevent a validation run from being
blocked while scripts are being developed, but they weaken release traceability.
For a private HTTPS remote, the command first tries non-interactive Git and then
uses an authenticated GitHub CLI session to verify the remote commit. It never
opens an interactive credential prompt during a deployment.

The remainder of this document is the detailed recovery and manual procedure.

### September 2026 canonical Finance preflight

The 59-migration release adds read-only preflight probes for the 20 guarded
post-baseline migrations. `CanonicalMigrationPreflight.json` binds each reviewed
migration to its normalized source SHA-256. The deployment command embeds the
three SQL probes in its content-addressed remote helper, so local-VPS and SSH
deployment use the same checked bundle without loose mutable SQL sidecars.
Changing a covered migration requires reviewing and updating its probe/hash.

These checks run even when the disposable current-model baseline is already
applied. That baseline bypasses only archived predecessor guards. The Finance
cutover still rejects retained accounting books and transaction rows according
to each migration's own `Up` conditions. Trigger-only and `Down`-only guards are
distinguished from upgrade-time blockers; existing trigger definitions and C4
authority schema are checked without altering them.

If preflight reports `FinanceCanonical...AccountingBooks`, `VendorInvoice`,
`Invoices` or another retained-data count, leave the existing services running.
The release is not a data-preserving conversion of those rows. Review a
data-preserving cutover or provision a separately authorized fresh database;
never remove rows or stamp migration history to bypass the check. A successful
local fresh-database rehearsal does not authorize resetting the VPS database.

When copying commands into an interactive PowerShell prompt, enclose the whole
sequence in `& { ... }` or run a saved `.ps1`. A `throw` typed at the prompt stops
that statement, but later pasted statements can still run. The deployment
command itself repeats preflight before any build/backup/apply stage.

### Separate fresh database on the test VPS

Use this only when a separate fresh test database has been approved. It preserves
the current database and its users, settings and transactions; those records are
**not copied into the new application database**. The fresh database receives the
current canonical development seed plus the operational module baseline below.
Obtain administrator sign-in details through the existing protected process;
the deploy script does not print credentials.
This is a one-time cutover option. For later releases, omit `-FreshDatabaseName`
and use the normal deployment path against the newly configured database.

Run from an elevated Windows PowerShell on the VPS, using the release checkout.
The example is one complete block so a failed check stops all subsequent steps:

```powershell
& {
    $ErrorActionPreference = 'Stop'
    Set-Location 'C:\Users\Administrator\Documents\ERP\RHEMA-ERP'
    $changes = @(git status --porcelain)
    if ($LASTEXITCODE -ne 0 -or $changes.Count) { throw 'Review repository status before deploying.' }
    git pull --ff-only origin master
    if ($LASTEXITCODE -ne 0) { throw 'Git update failed.' }
    $deploy = Get-Command .\scripts\Deploy-RhemaVps.ps1
    if (-not $deploy.Parameters.ContainsKey('FreshDatabaseName')) { throw 'Merge the fresh-database deployment PR first.' }
    $freshName = 'RhemaERP_VpsTest_' + (Get-Date -Format 'yyyyMMdd_HHmmss')
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-RhemaVps.ps1 `
        -Environment Test -LocalVps -FreshDatabaseName $freshName -DryRun
    if ($LASTEXITCODE -ne 0) { throw 'Fresh target preflight failed; no database was created.' }
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Deploy-RhemaVps.ps1 `
        -Environment Test -LocalVps -FreshDatabaseName $freshName -ReuseVerifiedArtifacts
    if ($LASTEXITCODE -ne 0) { throw 'Deployment stopped. Review its reported stage and rollback evidence before retrying.' }
}
```

`-FreshDatabaseName` requires `-LocalVps`, a new `RhemaERP_` name, SQL permission
to inspect/create databases, and the same SQL principal for provisioning and the
API. Integrated authentication is accepted only when the deployment runs under
the API service's Windows identity. The script never grants privileges itself.

Fresh `-DryRun` checks the host, protected configuration, SQL permissions and
target-name availability. It creates nothing and does not claim the old database
has passed migration parity. The regular deployment path still enforces all
existing migration guards when `-FreshDatabaseName` is omitted.

The apply run builds and checks the release, creates verified application/SQL
backups, then provisions the separate database while the old app remains running.
It applies the exact release migration list, seeds twice, checks canonical schema
and seed repeatability, trusted constraints and SQL physical integrity. CLI output
is kept out of persistent logs because it can include seeded credentials.
Each migration/seed command initializes the full ERP model and can take several
minutes. The console reports each command's start and completion; the existing
application stays running throughout this preparation.

Only after these checks pass are both services stopped and the connection's
database name changed. JWT/encryption keys and other protected runtime values
are retained. Both services, release files and the original connection are
restored together on apply, readiness, migration parity, public smoke or browser
smoke failure. Both databases remain available; a failed target is never dropped
or reused. Choose a new name only after reviewing a failed attempt. Application
uploads/DMS storage are retained; their old database references are not imported.

Evidence is in `C:\RhemaERP\backups\deploy-<deployment-id>\fresh-provisioning.json`
and `fresh-cutover.json`, plus the normal release evidence. If rollback itself
fails, keep both databases and the protected backup, and review the reported
failure before restarting services. The matching generated helper supports
`-Action RollbackFresh -DeploymentId <id> -ExpectedCommit <full-sha>
-FreshDatabaseName <name>` for an interrupted, uncommitted cutover. It refuses a
different release/database or an already committed cutover.

### Procurement, Inventory and QS deployment seeds

Test deployments automatically run the operational baseline. Fresh cutovers run
`seed-deployment-uat` (base plus operational seeds) twice before changing services,
and verify stable identities and master data. Later ordinary deployments run
`seed-operational-uat` after migrations and verify its database results.

| Area | Dedicated usernames |
|---|---|
| Procurement | `procurementofficer`, `procurementapprover`, `procurementevaluator`, `tdc0102-checker-201531` |
| Inventory / Stores | `storesofficer`, `storesmanager` |
| Quantity Survey | `uat.qs.preparer`, `uat.qs.reviewer`, `uat.qs.approver` |
| Finance review for procurement | `financereviewer`, `financeapprover` |

Accounts receive DEFAULT tenant membership and their existing module roles.
The baseline also augments the established administrator, manager, employee,
AP officer and Finance manager actors for the UAT workflow. Existing passwords
are not reset. New dedicated accounts use `UatBootstrap__SharedPassword` from
protected VPS configuration or the current deployment process. If absent,
`-LocalVps` prompts securely after package creation and verified backups, immediately
before applying the release. The input is inherited by the deployment helper and
passed through the isolated seed process environment. Native build, dependency
and browser tools have this setting removed from their environment, including
when it was supplied to the deployment process in advance. It is not written
to service settings, command arguments or deployment evidence. Dry run never
prompts or seeds; it reports only whether this setting is configured. SSH mode
requires the protected setting to exist on the VPS. The initial password needs
at least 8 characters, including uppercase, lowercase, a digit and a symbol.
Distribute it through your protected process.

Inventory fixtures include 5 units of measure, 6 categories, 2 warehouses
(`DEMO-PM`, `WH-02`), 3 locations and 9 items with base/purchase/stocking UOM
links. Stores access includes both warehouses. The seeder adds the canonical
supplier fixtures and required warehouse responsibilities. It creates no stock
balances, invoices, payments or journals. Existing master records and draft
supplier profiles are preserved; readiness conflicts stop verification for review
instead of silently changing user-owned data.

Fresh provisioning evidence includes `operationalSeed` counts and a fingerprint.
Normal deployment evidence is under
`C:\RhemaERP\packages\operational-<deployment-id>\verification.json`.
Neither includes passwords, hashes of passwords or contact details.
Provisioning failures retain sanitized stage, exit/SQL codes and output hashes in
`C:\RhemaERP\packages\fresh-<deployment-id>\failure.json`; raw CLI output and
connection strings are excluded.

Regression checks (no deployment):

```powershell
powershell.exe -NoProfile -File scripts\vps\Test-CanonicalMigrationPreflight.ps1
powershell.exe -NoProfile -File scripts\vps\Test-CurrentBaselineMigrationGuardRouting.ps1
powershell.exe -NoProfile -File scripts\vps\Test-RhemaVpsReleasePrerequisites.ps1
powershell.exe -NoProfile -File scripts\vps\Test-FreshDatabaseCutover.ps1
```

The first test optionally accepts `-SqlServer`, `-CanonicalDatabase` and
`-LegacyDatabase` for read-only SQL verification using integrated authentication.
The canonical fixture must already have the full migration chain; the legacy
fixture must contain Finance data which the pending canonical migrations reject.

## One-Time SSH Setup and the Port 22 Conflict

Do not move or stop the existing service on port `22`. On this VPS, IPv4 port `22` belongs to Rebex Tiny SFTP Server:

```text
C:\Users\emmanuel\Documents\RebexTinySftpServer-Binaries-Latest\RebexTinySftpServer.exe
```

OpenSSH therefore runs on port `2222`. The OpenSSH executable is installed at `C:\Program Files\OpenSSH\sshd.exe`, not under `C:\Windows\System32\OpenSSH`.

The effective `C:\ProgramData\ssh\sshd_config` must include:

```text
Port 2222
PubkeyAuthentication yes
AuthorizedKeysFile .ssh/authorized_keys
```

For the Administrator account, keep the public key in:

```text
C:\Users\Administrator\.ssh\authorized_keys
```

Save `authorized_keys` as plain UTF-8 without a BOM or as ASCII. Restrict its ACL to the Administrator account and SYSTEM:

```powershell
$sshHome = 'C:\Users\Administrator\.ssh'
$authorizedKeys = Join-Path $sshHome 'authorized_keys'

icacls.exe $sshHome /inheritance:r
icacls.exe $sshHome /grant:r 'RSSVR\Administrator:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F'
icacls.exe $authorizedKeys /inheritance:r
icacls.exe $authorizedKeys /grant:r 'RSSVR\Administrator:F' 'NT AUTHORITY\SYSTEM:F'
```

Create and verify the firewall rule, validate the effective configuration, and restart OpenSSH:

```powershell
if (-not (Get-NetFirewallRule -DisplayName 'OpenSSH TCP 2222' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule `
        -DisplayName 'OpenSSH TCP 2222' `
        -Direction Inbound `
        -Protocol TCP `
        -LocalPort 2222 `
        -Action Allow
}

& 'C:\Program Files\OpenSSH\sshd.exe' -t
& 'C:\Program Files\OpenSSH\sshd.exe' -T |
    Select-String 'port|pubkeyauthentication|authorizedkeysfile'
Restart-Service sshd
Get-NetTCPConnection -State Listen -LocalPort 22,2222
```

Expected result:

- Rebex remains on IPv4 port `22`.
- OpenSSH listens on IPv4 and IPv6 port `2222`.
- `sshd` is `Running`.

Test non-interactive access from the deployment workstation before building:

```powershell
ssh `
    -p 2222 `
    -i "$env:USERPROFILE\.ssh\id_rsa" `
    -o IdentitiesOnly=yes `
    -o BatchMode=yes `
    -o ConnectTimeout=15 `
    -o StrictHostKeyChecking=yes `
    Administrator@63.141.230.56 whoami
```

The RSA identity above is the verified working key. Do not assume the Ed25519 identity works without testing it.

## Repeatable Release Workflow

### 1. Freeze and identify the release

Never deploy an ambiguous dirty working tree. Preserve team changes and record the exact commit:

```powershell
git status -sb
git rev-parse HEAD
git log -1 --oneline
```

Build and name both packages from that exact commit. Record:

- full commit SHA;
- short SHA;
- branch;
- frontend build ID;
- service-worker cache version;
- SHA-256 of each ZIP.

### 2. Build the API

Publish a self-contained Windows x64 API:

```powershell
dotnet publish src\ErpSystem.Api\ErpSystem.Api.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o "artifacts\deployment-vps-<short-sha>\api" `
    /p:PublishSingleFile=false `
    -p:TdcFastEfBuild=true
```

The API package must not contain or overwrite:

- `appsettings.json`;
- `appsettings.Production.json`;
- `.env` or `.env.production`;
- `wwwroot\uploads`;
- runtime logs.

### 3. Build the frontend

Delete `.next`, set all production variables, build, and validate the build ID as described in the Critical Rules and Packaging Notes below. Before packaging:

- bump the cache names in `frontend/public/sw.js`;
- confirm `.next\BUILD_ID` is not `development`;
- confirm the middleware build ID matches `.next\BUILD_ID`;
- scan source and compiled chunks for localhost API URLs.

The frontend package contains only:

- `.next`, excluding `.next\cache`;
- `public`;
- `package.json`, `package-lock.json`, and `next.config.js`;
- production `node_modules` restored from the lock file with lifecycle scripts disabled.

The VPS frontend service runs `npm start -- -p 3001`, so the release uses the regular Next.js build. It does not require a standalone `server.js`.

### 4. Hash and upload packages

```powershell
Get-FileHash `
    -Algorithm SHA256 `
    -LiteralPath `
        "artifacts\deployment-vps-<short-sha>\rhema-erp-api-<short-sha>.zip", `
        "artifacts\deployment-vps-<short-sha>\rhema-erp-frontend-<short-sha>.zip"

scp `
    -P 2222 `
    -i "$env:USERPROFILE\.ssh\id_rsa" `
    -o IdentitiesOnly=yes `
    -o BatchMode=yes `
    -o StrictHostKeyChecking=yes `
    "artifacts\deployment-vps-<short-sha>\rhema-erp-api-<short-sha>.zip" `
    "artifacts\deployment-vps-<short-sha>\rhema-erp-frontend-<short-sha>.zip" `
    Administrator@63.141.230.56:C:/RhemaERP/packages/
```

Recompute both hashes on the VPS before extracting anything. Stop if either hash differs.

### 5. Back up before stopping services

Every deployment requires both types of rollback evidence:

1. A timestamped application snapshot under `C:\RhemaERP\backups\deploy-<short-sha>-<timestamp>` containing API, frontend, and service XML files. Exclude API uploads and logs from the copy, but preserve them in place.
2. A SQL Server `COPY_ONLY`, `COMPRESSION`, `CHECKSUM` backup followed by `RESTORE VERIFYONLY ... WITH CHECKSUM`.

Read the database connection string from `C:\RhemaERP\services\api\RhemaERPAPI.xml` only in memory. Never print it or write it to the deployment log.

Do not proceed unless the application snapshot exists and `RESTORE VERIFYONLY` passes.

### 6. Apply test-server configuration safely

For the current test VPS:

```text
StartupInitialization__SeedDevelopmentData=true
StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment=true
CorsSettings__AllowedOrigins__0=https://63.141.230.56
ALLOWED_ORIGINS=https://63.141.230.56
```

`ALLOWED_ORIGINS` currently takes precedence in the API. Keep it identical to
`CorsSettings__AllowedOrigins__0`; the deployment helper reconciles both before
restarting the API.

Syncfusion licensing is held in the protected API service configuration, not
in a deployed `appsettings.json` or `.env` file. The service XML must contain a
non-empty `Syncfusion__LicenseKey` environment entry. The release pipeline
reuses that protected value over SSH to run the local Syncfusion frontend
license activator before the Next.js production build. The value is retained
only in process memory and must never be printed, written to a release manifest,
or committed. Preflight and post-deployment verification report only
`SYNCFUSION_LICENSE|CONFIGURED` and fail closed when it is missing.

The key must be compatible with the deployed Syncfusion package version and
cover both the server document SDK and browser UI/PDF Viewer editions. Adding
the key only to the API service does not license an already-built React bundle;
a new licensed frontend artifact must be built and deployed.

Rules:

- Keep only the HTTPS CORS origin. Remove any HTTP origin.
- Keep both development-seeding settings `true` only while this host is explicitly an isolated test server.
- Set `SeedDevelopmentData=false` and remove or set `AllowDevelopmentDataSeedingOutsideDevelopment=false` before production promotion.
- Remove `.svg` and `image/svg+xml` from applicant-upload allowlists unless uploads are moved to a separate, forced-download origin.
- Provision a real ClamAV `clamd` endpoint before release. The default is the
  private loopback endpoint `127.0.0.1:3310`; if an approved central scanner is
  used instead, set `FileVirusScan__ClamAv__Host` and
  `FileVirusScan__ClamAv__Port` in the protected API service configuration.
  Never expose the unauthenticated clamd protocol publicly.
- Treat a non-healthy `file-virus-scanner` entry on `/health/ready` as a release
  blocker. It intentionally blocks clean-scan-required DMS uploads rather than
  accepting unscanned content. Do not replace it with the no-op scanner to
  obtain a green health response.
- Preserve the existing database, JWT, email/SMS, NextAuth, and other secrets.
- `Security__RequireHttps=false` is expected behind the current TLS-terminating Caddy service; browser traffic is HTTPS on port `443`.

### 7. Deploy in service order

API:

1. Extract into a commit-specific staging directory under `C:\RhemaERP\packages`.
2. Validate `ErpSystem.Api.exe` and required assemblies.
3. Stop `RhemaERPAPI`.
4. Copy the staged API with `robocopy /E`, preserving protected configuration, uploads, and logs.
5. Start `RhemaERPAPI`.
6. Wait on `http://127.0.0.1:5000/health`.
7. Allow enough time for startup migrations; this deployment required about 3.5 minutes.

Frontend:

1. Validate the staged build ID and service-worker version.
2. Stop `RhemaERPFrontend`.
3. Replace live `.next` and `public` as complete directory sets.
4. Replace `node_modules`, `package.json`, `package-lock.json`, and `next.config.js` from the same package.
5. Start `RhemaERPFrontend`.
6. Verify `http://127.0.0.1:3001/login` returns HTTP 200.

Do not copy into a running `.next` directory. A mixed old/new Next.js tree causes missing chunks and unstyled pages.

Caddy normally stays running and is preserved by application deployments. After both app services pass locally, verify all three services are `Running`:

```powershell
Get-Service RhemaERPAPI,RhemaERPFrontend,RhemaERPCaddy
```

### 8. Verify the database, not only the services

Query `dbo.__EFMigrationsHistory` and record its count and latest migration. Also verify newly deployed foreign keys are neither disabled nor untrusted:

```sql
SELECT COUNT(*) AS MigrationCount, MAX(MigrationId) AS LatestMigration
FROM dbo.__EFMigrationsHistory;

SELECT
    fk.name,
    fk.is_disabled,
    fk.is_not_trusted
FROM sys.foreign_keys AS fk
WHERE fk.name IN
(
    N'FK_ReturnOrders_Invoices_InvoiceId',
    N'FK_ReturnOrderLines_InvoiceLineItem_InvoiceLineItemId'
);
```

Do not declare success if a migration is missing or a required foreign key is disabled/untrusted.

### 9. Run API, asset, and browser smoke

Required public checks:

- `/api/tenant` and `/api/auth/security-settings` through Caddy;
- `/login` and `/supplier-application`;
- `/sw.js` contains the new cache version;
- every Next.js JS/CSS asset referenced by the tested pages returns HTTP 200;
- compiled JavaScript contains the expected VPS API origin and no development API URLs;
- CORS preflight returns `Access-Control-Allow-Origin: https://63.141.230.56`;
- direct public `http://63.141.230.56:5000/health` is refused.

On the first dry run after moving from the retired port-8443 origin to Caddy,
`CONFIG_DRIFT` may be reported for CORS, candidate-portal, and frontend URL
settings. The normal backed-up apply stage reconciles those settings to
`https://63.141.230.56` before it restarts the API; post-deployment smoke still
requires the exact CORS origin.

Required browser assertions:

- login fields and `Sign In` are visible;
- supplier token application and token-login tabs are visible;
- unauthenticated `/supplier-application/portal` returns to `/supplier-application`;
- unauthenticated `/administration/procurement/supplier-applicant-access` redirects to `/login`;
- no page errors, HTTP 5xx responses, console errors, or failed requests.

If Kaspersky on the workstation intercepts the public IP with HTTP `499`, do not weaken CORS and do not treat the antivirus page as an ERP response. Use a temporary SSH tunnel while preserving the real browser origin:

```powershell
ssh `
    -p 2222 `
    -i "$env:USERPROFILE\.ssh\id_rsa" `
    -o IdentitiesOnly=yes `
    -o BatchMode=yes `
    -o ExitOnForwardFailure=yes `
    -N `
    -L 443:127.0.0.1:443 `
    Administrator@63.141.230.56
```

Launch the smoke browser with:

```text
--host-resolver-rules=MAP 63.141.230.56 127.0.0.1
--no-proxy-server
--ignore-certificate-errors
```

The page origin remains `https://63.141.230.56`, so the production CORS rule is genuinely tested. Stop the tunnel immediately after the smoke and confirm no local listener/process remains.

### 10. Roll back

If either application cannot pass readiness:

1. Stop `RhemaERPFrontend` and `RhemaERPAPI`.
2. Restore the matching API and frontend snapshot from `C:\RhemaERP\backups\deploy-<short-sha>-<timestamp>`.
3. Restore the backed-up service XML if it changed.
4. Start API, wait for local health, then start frontend and wait for local login.
5. Repeat the full smoke.

Restore the database backup only when migration/data rollback is required and explicitly approved. An application-file rollback does not automatically require a database restore.

## Critical Rules

1. Build the frontend with production API variables before packaging.

   `NEXT_PUBLIC_*` variables are compiled into Next.js client chunks at build time. Updating `.env.production` after `npm run build` does not rewrite already-built JavaScript.

   ```powershell
   cd frontend
   Remove-Item -LiteralPath .next -Recurse -Force -ErrorAction SilentlyContinue
   $env:NODE_ENV = 'production'
   $env:NEXT_PUBLIC_API_URL = 'https://63.141.230.56/api'
   $env:API_URL = 'https://63.141.230.56/api'
   $env:NEXTAUTH_URL = 'https://63.141.230.56'
   $env:NEXT_TELEMETRY_DISABLED = '1'
   npm run build
   ```

2. Keep browser-side fallbacks same-origin.

   Use `/api` or derive from `NEXT_PUBLIC_API_URL`. Do not use localhost fallbacks in code that can ship to the browser. Known places to check:

   - `frontend/src/services/api.service.ts`
   - `frontend/src/services/signalr.service.ts`
   - `frontend/src/services/token-refresh.service.ts`
   - `frontend/src/config/api.ts`

3. Bump the service-worker cache names for frontend deployments.

   If `frontend/public/sw.js` keeps the old cache names, users may keep stale chunks after deployment. The 2026-07-01 asset/grid fix used cache names ending in `2026-07-01-asset-grid-fix`.

   Do not precache app pages such as `/`, `/login`, `/dashboard`, `/mobile`, or authenticated module routes. Cache hashed static assets only. Precaching pages can leave browsers with HTML that points to CSS/JS chunks from an older build, which makes the app render as unstyled HTML after a deployment.

4. Route through Caddy only.

   The API service binds locally on `http://127.0.0.1:5000`. Public direct access to `http://63.141.230.56:5000` must be refused. Browsers use Caddy on `https://63.141.230.56` port `443`.

5. Preserve the existing Caddy gateway.

   `RhemaERPCaddy` owns the public certificate and routes `/api/*` to `127.0.0.1:5000`; remaining paths go to `127.0.0.1:3001`. Check API health directly on loopback because the current Caddyfile does not expose `/health*` publicly.

6. Deploy API and frontend together when DTOs or browser-facing contracts change.

   Example: asset `Year` persistence needs both the API DTO/mapping changes and the rebuilt frontend payload. Deploying only one side can leave the UI saving fields the API ignores, or the API returning fields the old UI never reads.

7. Put shared grid behavior in shared table controls.

   Compact rows and alternating row color belong in `frontend/src/components/ui/table.tsx`, `frontend/src/components/ui/DataTable/DataTable.tsx`, `frontend/src/components/admin/data-table.tsx`, `frontend/src/components/mobile/MobileDataTable.tsx`, and the global raw-table fallback in `frontend/src/app/globals.css`. Avoid page-only grid styling unless the page has a special layout need.

8. Publish the API as self-contained for the VPS.

   The `RhemaERPAPI` service runs `C:\RhemaERP\api\ErpSystem.Api.exe`. Publish with the same app-local runtime shape the VPS expects:

   ```powershell
   dotnet publish src\ErpSystem.Api\ErpSystem.Api.csproj `
     -c Release `
     -r win-x64 `
     --self-contained true `
     -o artifacts\api-publish-<stamp> `
     /p:PublishSingleFile=false `
     -p:TdcFastEfBuild=true
   ```

   A framework-dependent API publish can fail at service start with fragmented log lines such as `.NET location`, `Framework: 'Microsoft.NETCore.App', version '8.0.0'`, and `App: C:\RhemaERP\api\ErpSystem.Api.exe`.

   `TdcFastEfBuild=true` avoids recompiling the historical migration designer
   corpus while retaining migration discovery through
   `FastBuildMigrationMetadata.cs`. The automated deployment validates that every
   migration remains discoverable before publishing.

9. Do not publish or mirror runtime uploads.

   `src/ErpSystem.Api/wwwroot/uploads` contains runtime data and can include stale/missing upload references from local development. The project excludes that folder from publish. On the VPS, preserve `C:\RhemaERP\api\wwwroot\uploads` and do not wipe it during API deployments.

## Packaging Notes

The deployed frontend package should contain:

- `.next`
- `public`
- `package.json`
- `package-lock.json`
- `next.config.js`
- production `node_modules`

The NSSM service runs `npm start -- -p 3001`. Package the regular `.next` output and the exact production dependencies restored from `package-lock.json`. Exclude `.next\cache`.

Before packaging, verify the build and middleware IDs match and neither is `development`:

```powershell
$buildId = (Get-Content -LiteralPath .next\BUILD_ID -Raw).Trim()
$manifest = Get-Content -LiteralPath .next\server\middleware-manifest.json -Raw | ConvertFrom-Json
$middlewareBuildId = $manifest.middleware.'/'.env.__NEXT_BUILD_ID
if ($buildId -eq 'development' -or $buildId -ne $middlewareBuildId) {
  throw 'Frontend is not a clean production build.'
}
```

Do not live-mirror `.next` over SMB while the frontend service is running. Use the package-and-apply flow instead:

1. Build locally with the production API variables.
2. Stage `.next`, `public`, `package.json`, `package-lock.json`, `next.config.js`, and production `node_modules`.
3. Copy the package to `C:\RhemaERP\packages`.
4. Stop `RhemaERPFrontend`.
5. Apply the package on the VPS or copy the staged tree as one consistent set.
6. Start `RhemaERPFrontend`.
7. Verify `http://127.0.0.1:3001/login` on the VPS, then verify the live HTML and every CSS file referenced by that HTML.

If a browser reports a missing old CSS chunk after deployment, first verify the live `/login` HTML. If live HTML no longer references that old chunk, add a temporary compatibility copy only as a bridge for already-cached browser pages, then restart `RhemaERPFrontend` so Next.js sees the copied file.

The deployed API package should be copied over `C:\RhemaERP\api` while preserving:

- `appsettings.json`
- `appsettings.Production.json`
- `.env`
- `.env.production`
- `RhemaERPAPI.exe`
- `RhemaERPAPI.xml`
- `wwwroot\uploads`

Do not mirror-delete the API folder unless those preserved files and folders have first been backed up and restored. A normal `robocopy` copy with `/E` and explicit excludes is safer than `/MIR` for API deployments.

## Required Smoke Tests

Run these after every VPS deployment:

```powershell
$base = 'https://63.141.230.56'
Invoke-WebRequest -Uri "$base/api/tenant" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri 'http://127.0.0.1:5000/health' -UseBasicParsing
Invoke-WebRequest -Uri 'http://127.0.0.1:5000/health/ready' -UseBasicParsing
Invoke-WebRequest -Uri "$base/login" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri "$base/sw.js" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri 'http://63.141.230.56:5000/health' -UseBasicParsing -TimeoutSec 10
```

Expected results:

- `/api/tenant` returns HTTP 200.
- Local `/health` returns `Healthy`.
- Local `/health/ready` returns `Healthy`.
- `/login` returns HTTP 200.
- `/sw.js` contains the latest cache version.
- Direct `:5000/health` is refused or unreachable from outside.
- Live `/login` and `/dashboard` HTML reference CSS files that all return HTTP 200.

Also scan the source and deployed login chunks for bad client URLs:

```powershell
rg -n "localhost:5000|localhost:53484|localhost:7095" frontend/src frontend/public frontend/.env.production
```

For deployed login assets, fetch `/login`, request the referenced `/_next/static/*.js` files, and confirm none contain:

- `http://localhost:5000`
- `https://localhost:53484`
- `https://localhost:7095`

For CSS, parse the live HTML and check each referenced stylesheet:

```powershell
$base = 'https://63.141.230.56'
foreach ($route in @('/login', '/dashboard')) {
  $html = (Invoke-WebRequest -Uri "$base$route" -SkipCertificateCheck -UseBasicParsing).Content
  [regex]::Matches($html, 'href="([^"]+\.css[^"]*)"') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique |
    ForEach-Object {
      Invoke-WebRequest -Uri "$base$_" -SkipCertificateCheck -UseBasicParsing
    }
}
```

## 2026-07-01 Incident Summary

Symptom:

- Browser login failed with `NetworkError when attempting to fetch resource`.
- Network tab showed `GET http://localhost:5000/api/tenant` returning `503 Service Unavailable`.

Cause:

- A frontend build shipped localhost API fallbacks into client chunks.
- The deployed API was meant to be accessed through `https://63.141.230.56/api`, not directly through localhost or public port `5000`.

Fix applied:

- Frontend production env set to `https://63.141.230.56/api`.
- Shared API fallback changed to `/api`.
- SignalR and token-refresh fallbacks changed away from localhost.
- Service-worker cache names bumped.
- Caddy routes `/api/*` to the API and other public paths to the frontend.
- API service bound locally on `127.0.0.1:5000`; direct public `:5000` access verified refused.

Verification from that fix:

- `https://63.141.230.56/api/tenant` returned HTTP 200.
- local API `/health` and `/health/ready` returned healthy results.
- `https://63.141.230.56/login` returned HTTP 200.
- 22 login-loaded scripts were scanned and had zero localhost API URL matches.
- `http://63.141.230.56:5000/health` was refused from outside.

## 2026-07-28 Verified Test Deployment

Release:

- Branch: `agent/tdc-procurement-phase-0-controls`
- Commit: `f3fc4f4922699650aba8643b7e596b669c6de8d0`
- Frontend build ID: `hh9pI9Rn5hrMlsieFEDKb`
- Service-worker cache version: `2026-07-28-supplier-access-hardening`
- API ZIP SHA-256: `366B6E0DAC2713EB7779AA397A97362D0AD4E7A399A2246C5A046CCE2DF1222D`
- Frontend ZIP SHA-256: `8582FCC0E17F0EBFF9AD19F90EC96094E66DCFEB9AA6B51F66B0577CBBD80D8A`

Rollback evidence:

- Application snapshot: `C:\RhemaERP\backups\deploy-f3fc4f49-20260728-091857`
- Verified SQL backup: `C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\Backup\RhemaERP_pre_f3fc4f49_20260728-091857.bak`
- SQL backup size: `15,343,616` bytes
- `RESTORE VERIFYONLY ... WITH CHECKSUM`: passed

Database result:

- Migration count: `150`
- Latest migration: `20260727214500_EnsureArReturnInvoiceForeignKeys`
- `FK_ReturnOrders_Invoices_InvoiceId`: enabled and trusted
- `FK_ReturnOrderLines_InvoiceLineItem_InvoiceLineItemId`: enabled and trusted

Runtime result:

- `RhemaERPAPI`: Running
- `RhemaERPFrontend`: Running
- `RhemaERPCaddy`: Running
- Frontend local `/login`: HTTP 200
- CORS: HTTPS origin only
- Development-data seeding: enabled because this is a test server
- SVG extension/MIME upload allowlist entries: absent
- Browser smoke: passed all four required routes with no console errors, failed requests, page errors, or HTTP 5xx responses

Known non-green infrastructure signal:

- `/health` reports `Degraded` because the process working set was about `3,665 MB`, above the hard-coded `1 GB` memory-health threshold.
- Database connectivity, migrations, frontend readiness, API calls, and browser routes passed.
- Do not hide this warning by merely increasing the threshold. Inspect why the API working set is high and confirm acceptable VPS capacity before production promotion.

Deployment log:

```text
C:\RhemaERP\logs\deploy-f3fc4f49.log
```

### Problems encountered and the retained fixes

| Problem | Cause | Retained fix |
|---|---|---|
| SSH to port 22 did not reach OpenSSH | Rebex Tiny SFTP owns IPv4 port 22 | Keep Rebex on 22; use OpenSSH on 2222 |
| Assumed `sshd.exe` path did not exist | OpenSSH was installed under Program Files | Use `C:\Program Files\OpenSSH\sshd.exe` |
| Public-key authentication failed | Key file location/ACL/encoding and identity selection | Use Administrator `.ssh\authorized_keys`, strict Administrator/SYSTEM ACL, plain text encoding, and the verified RSA key |
| `icacls /grant:r` failed | Grant arguments were malformed | Quote each complete `account:permission` argument as shown above |
| API startup seemed stuck | Startup was applying a large migration set | Keep the service alive and allow several minutes while polling local health and logs |
| Risk of overwriting secrets/uploads | A complete folder replacement would replace runtime state | Exclude production config and preserve `wwwroot\uploads`; snapshot first |
| Risk of stale frontend chunks | Copying into a running/mixed `.next` tree | Stop frontend and replace `.next`/`public` as complete sets |
| Browser received HTTP 499 | Workstation Kaspersky intercepted the public IP | Use a temporary SSH tunnel plus Chrome host-resolver mapping |
| Tunnel smoke initially showed CORS failures | Browser origin was changed to `127.0.0.1` | Preserve the public origin and map only its transport to localhost |
| Windows PowerShell HTTPS probes were inconsistent | Windows PowerShell 5.1 lacks PowerShell 7's `-SkipCertificateCheck` behavior | Use local HTTP readiness on the VPS and the certificate-ignoring browser smoke for public HTTPS |

### Short go/no-go checklist

- [ ] Intended commit and clean release scope recorded
- [ ] API self-contained publish passed
- [ ] Clean production frontend build passed
- [ ] Build ID and middleware build ID match
- [ ] Service-worker cache version changed
- [ ] Packages exclude secrets, uploads, caches, and unnecessary node modules
- [ ] Local and remote package hashes match
- [ ] Application snapshot exists
- [ ] SQL `COPY_ONLY` backup and `RESTORE VERIFYONLY` passed
- [ ] Test/production seeding decision explicitly recorded
- [ ] HTTPS-only CORS and upload allowlist verified
- [ ] API started and migrations completed
- [ ] Frontend local login returned 200
- [ ] All three Windows services are Running
- [ ] Migration count/latest ID verified
- [ ] New foreign keys enabled and trusted
- [ ] Public API, health, service worker, JS, and CSS checks passed
- [ ] Real browser smoke passed with zero material errors
- [ ] Public port 5000 refused
- [ ] Temporary tunnels and test processes stopped
- [ ] Deployment log and rollback paths reported

## Do Not Leak Secrets

Do not paste service XML, `.env.production`, database connection strings, JWT secrets, or NextAuth secrets into chat output. Report setting names and verification results only.
