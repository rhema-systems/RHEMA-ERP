# Rhema ERP VPS Deployment Runbook

Last updated: 2026-07-01

This note captures the VPS deployment details that have caused repeat failures. Read it before deploying to the live VPS.

## Live VPS Shape

- Public app URL: `https://149.102.145.190:8443`
- Public API entrypoint for browsers: `https://149.102.145.190:8443/api`
- Windows deployment root: `C:\RhemaERP`
- Frontend runtime: `C:\RhemaERP\frontend`
- Package drop folder: `C:\RhemaERP\packages`
- Deployment/log folder: `C:\RhemaERP\logs`
- Windows services:
  - `RhemaERPAPI`
  - `RhemaERPFrontend`
  - `RhemaERPHTTPSIPProxy`

The browser must never be sent to `http://localhost:5000`, `https://localhost:53484`, or `https://localhost:7095` in a deployed build.

## Critical Rules

1. Build the frontend with production API variables before packaging.

   `NEXT_PUBLIC_*` variables are compiled into Next.js client chunks at build time. Updating `.env.production` after `npm run build` does not rewrite already-built JavaScript.

   ```powershell
   cd frontend
   $env:NEXT_PUBLIC_API_URL = 'https://149.102.145.190:8443/api'
   $env:API_URL = 'https://149.102.145.190:8443/api'
   $env:NEXTAUTH_URL = 'https://149.102.145.190:8443'
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

4. Route through the HTTPS proxy only.

   The API service should bind locally on `http://127.0.0.1:5000`. Public direct access to `http://149.102.145.190:5000` should be refused. Browsers should use the HTTPS proxy on port `8443`.

5. The HTTPS proxy must route health paths to the API.

   In `C:\RhemaERP\proxy\https-ip-proxy.js`, `/api`, `/health`, and `/health/*` must proxy to the API. Otherwise `/health/ready` and `/health/live` may hit the frontend and fail even when the API is healthy.

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
     /p:PublishSingleFile=false
   ```

   A framework-dependent API publish can fail at service start with fragmented log lines such as `.NET location`, `Framework: 'Microsoft.NETCore.App', version '8.0.0'`, and `App: C:\RhemaERP\api\ErpSystem.Api.exe`.

9. Do not publish or mirror runtime uploads.

   `src/ErpSystem.Api/wwwroot/uploads` contains runtime data and can include stale/missing upload references from local development. The project excludes that folder from publish. On the VPS, preserve `C:\RhemaERP\api\wwwroot\uploads` and do not wipe it during API deployments.

## Packaging Notes

The deployed frontend package should contain:

- `.next`
- `public`
- `server.js`
- `package.json`

In this repo, `server.js` is generated under `.next\standalone\server.js`. Put that file at the package root if the deployment script expects `server.js` beside `.next`.

Avoid packaging `.next\cache` and `.next\standalone\node_modules`; the VPS already has runtime dependencies and including them can turn a small deployment into a very large zip.

Do not live-mirror `.next` over SMB while the frontend service is running. Use the package-and-apply flow instead:

1. Build locally with the production API variables.
2. Stage `.next`, `public`, root `server.js` from `.next\standalone\server.js`, and `package.json`.
3. Copy the package to `C:\RhemaERP\packages`.
4. Stop `RhemaERPFrontend`.
5. Apply the package on the VPS or copy the staged tree as one consistent set.
6. Start `RhemaERPFrontend`.
7. Verify the live HTML and every CSS file referenced by that HTML.

If a browser reports a missing old CSS chunk after deployment, first verify the live `/login` HTML. If live HTML no longer references that old chunk, add a temporary compatibility copy only as a bridge for already-cached browser pages, then restart `RhemaERPFrontend` so the standalone server sees the copied file.

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
$base = 'https://149.102.145.190:8443'
Invoke-WebRequest -Uri "$base/api/tenant" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri "$base/health" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri "$base/health/ready" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri "$base/login" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri "$base/sw.js" -SkipCertificateCheck -UseBasicParsing
Invoke-WebRequest -Uri 'http://149.102.145.190:5000/health' -UseBasicParsing -TimeoutSec 10
```

Expected results:

- `/api/tenant` returns HTTP 200.
- `/health` returns `Healthy`.
- `/health/ready` returns `Healthy`.
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
$base = 'https://149.102.145.190:8443'
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
- The deployed API was meant to be accessed through `https://149.102.145.190:8443/api`, not directly through localhost or public port `5000`.

Fix applied:

- Frontend production env set to `https://149.102.145.190:8443/api`.
- Shared API fallback changed to `/api`.
- SignalR and token-refresh fallbacks changed away from localhost.
- Service-worker cache names bumped.
- HTTPS proxy routes `/api`, `/health`, and `/health/*` to the API.
- API service bound locally on `127.0.0.1:5000`; direct public `:5000` access verified refused.

Verification from that fix:

- `https://149.102.145.190:8443/api/tenant` returned HTTP 200.
- `https://149.102.145.190:8443/health` returned `Healthy`.
- `https://149.102.145.190:8443/health/ready` returned `Healthy`.
- `https://149.102.145.190:8443/login` returned HTTP 200.
- 22 login-loaded scripts were scanned and had zero localhost API URL matches.
- `http://149.102.145.190:5000/health` was refused from outside.

## Do Not Leak Secrets

Do not paste service XML, `.env.production`, database connection strings, JWT secrets, or NextAuth secrets into chat output. Report setting names and verification results only.
