# VPS deployment performance audit

Date: 2026-10-01

## Scope and verified architecture

The active VPS path is Windows-native. `Build-RhemaRelease.ps1` creates a self-contained `win-x64` API ZIP and a regular Next.js runtime ZIP. `Deploy-RhemaVps.ps1` transfers those immutable artifacts, and `Invoke-RhemaVpsRemote.ps1` activates them under the `RhemaERPAPI` and `RhemaERPFrontend` Windows services. It retains versioned releases and a verified application/database backup for rollback.

Dockerfiles, Compose files and Docker CI jobs exist, but they are not used by this Windows VPS release path. Docker timings must not be presented as the cause of a Windows service deployment unless a deployment is explicitly switched to that separate Docker workflow.

Phase 1 already supports the preferred build-once flow:

1. Build on a controlled Windows x64 host with `Build-RhemaRelease.ps1`.
2. Produce versioned `api.zip`, `frontend.zip` and `release-manifest.json` files.
3. Deploy them with `Deploy-RhemaVps.ps1 -DeployOnly -ArtifactDirectory ...`.
4. Back up the application and database.
5. activate the API, run startup migrations, activate the frontend, and run health/browser checks.
6. Roll back application files automatically after a failed post-activation verification. Database backups are retained and are not automatically restored by an application-only rollback.

`Deploy-QsUatVps.ps1` now performs the build-once/deploy-only flow itself. Its default path creates one immutable release, activates that verified artifact, and records build, activation, QS preparation, and readiness as separate durations. Passing the completed release directory with `-ArtifactDirectory` retries activation without rebuilding. `-LegacyFullBuild` retains the prior all-in-one path for an explicit full deployment.

## Baseline evidence

The repository contains a completed deployment record for commit `2659e506` with 2,061.36 seconds (34 minutes 21 seconds) accounted for:

| Stage | Seconds |
|---|---:|
| Build and package immutable release artifacts | 1,634.40 |
| Apply API, migrations and frontend | 224.46 |
| Headless browser smoke | 58.51 |
| Public API, asset and CORS smoke | 57.40 |
| Application and SQL backups | 46.27 |
| Package upload | 23.99 |
| Remote helper upload | 6.28 |
| Migration preflight | 5.37 |
| Service and database verification | 4.68 |

The more recent console evidence supplied by the operator records:

| Build sub-stage | Seconds |
|---|---:|
| Next.js production build | 1,227.4 |
| Production dependency preparation | 294.8 |
| Artifact compression | 309.3 |

Those three build sub-stages total 1,831.5 seconds (30 minutes 31.5 seconds), before npm restore, Syncfusion preparation, API publish, hashing and activation. They do not explain a four-hour end-to-end run. No retained evidence currently accounts for the remaining hours, so attributing them to Docker, migrations, backup, network, or QS seeding would be speculation. The added instrumentation is intended to produce that missing evidence on the next real deployment.

## Findings

### Frontend build and page-data collection

- The App Router contains 1,408 `page.tsx` routes and 25 layouts.
- No `generateStaticParams` or `generateMetadata` implementation was found under `frontend/src/app`, so there is no evidence of application API calls being made by those build hooks.
- “Collecting page data” still has to traverse and classify this unusually large route graph and produce server/static output. The route count, Windows filesystem cost, antivirus scanning and available RAM/disk throughput are more credible causes than a hidden build-time API fetch.
- `.next-production/cache` is preserved by default. It is removed only when the operator explicitly uses `-CleanBuild`.
- The release builder executes one `npm ci`. Its postinstall Syncfusion copy is suppressed during restore, then the licensed asset preparation runs once explicitly.
- Production dependency staging copies the installed dependency tree and prunes the copy. It does not destroy the build host dependency tree, but the measured 294.8 seconds makes it a candidate for a later standalone-runtime experiment.
- Regular Windows service releases use `next start` and a regular `.next` tree. Standalone output is already supported for the Docker image, but switching the Windows service to standalone changes its runtime layout and requires a dedicated deployment rehearsal, asset check and rollback test.

### .NET build

- The active release builder runs one `dotnet publish` operation. It does not separately restore, build and publish the API, so `--no-restore` or `--no-build` cannot safely be added without first introducing and verifying a preceding restore/build stage.
- Some CI workflows restore and build before publishing without `--no-build`; that is CI-specific duplication and does not explain the active Windows VPS activation time.
- The migration directory contains 197 tracked files totalling about 609.38 MB. Generated EF designer/model files dominate the repository and materially increase checkout, antivirus, compilation and Docker-context work.

### Artifact transfer, backup, migration, seed and activation

- The active source transfer sends two versioned ZIP artifacts rather than copying the repository, `.git`, local `node_modules`, uploads or logs.
- Historical artifacts are roughly 126–128 MB for the API and 80–83 MB for the frontend. Upload and hash validation were 23.99 seconds in the retained baseline.
- API and frontend application backups exclude runtime logs, uploads and protected document storage. The frontend backup also excludes `node_modules`.
- SQL backup uses `COPY_ONLY`, compression and checksums, followed by `RESTORE VERIFYONLY`. These controls remain in place. Backup creation and restore verification are now timed separately.
- API activation previously combined service stop, file copy, migration startup and readiness into one opaque duration. These are now split into file activation and startup/migration/readiness timings.
- Operational seeding and its verification are now timed separately.
- Fixed waits were not found in the active release path. Existing waits poll service state or health endpoints with bounded timeouts.
- No routine `docker system prune`, builder prune, image prune, `--no-cache`, npm cache clean, NuGet cache clear, recursive permission rewrite, or other cache-destroying command exists in the active Windows path.

### Resource usage

Build and remote timing records now capture boundary snapshots for CPU load, physical memory, virtual memory, page-file allocation/use, disk free space, disk throughput/latency counters and network throughput. These snapshots will show whether slow stages coincide with memory pressure or page-file use. They are boundary samples, not a continuous profiler; if the next run shows a stage lasting much longer than expected, Windows Performance Monitor sampling should be enabled for that isolated stage before changing concurrency.

Parallel builds are not recommended until that evidence exists. The current API publish deliberately uses `-m:1`, and the Next.js build uses a 12 GB heap default. Running them concurrently on a memory-constrained VPS could increase swap and total elapsed time.

### Docker audit

- The root and API Dockerfiles copy project files before restore, which is the right cache-layer pattern.
- Both .NET Dockerfiles run `dotnet build` and then `dotnet publish` without `--no-build`, so their Docker path can compile twice. This was not changed because Docker is not the measured VPS deployment path.
- The frontend Dockerfile copies lock files before dependencies, but uses `npm install` instead of `npm ci` for `package-lock.json`.
- The root `.dockerignore` excludes `.git`, `node_modules`, `bin`, `obj`, IDE files and environment files. It does not exclude artifacts, uploads, coverage, `.next`, logs, backup archives, test results or temporary files.
- The tracked repository is about 2.44 GB, and EF migration files alone are about 609.38 MB. A root Docker build context is therefore inherently large even before untracked runtime folders. Future Docker work should measure the actual context transfer and add safe exclusions, but it is separate from the current Windows artifact deployment.
- The CI Docker job already uses GitHub Actions layer caching. A later Docker cleanup should use `dotnet build --no-restore`, `dotnet publish --no-build --no-restore`, and `npm ci` only after those images are built and tested in their own pipeline.

## Instrumentation implemented

- Every recorded stage now includes start time, completion time, duration and status.
- Build stages include resource snapshots before and after each operation.
- Remote backup, package hashing, extraction, staging, API file activation, migration/startup, frontend activation, seed and verification operations emit structured timing records with resource snapshots.
- Deployment evidence imports remote timing records and includes a `slowestSteps` array.
- Build, deployment and QS wrapper scripts print a slowest-first summary.
- QS deployment, QS data preparation and QS readiness reporting are measured independently and saved under `artifacts\qs-uat`.
- `Deploy-QsUatVps.ps1 -UpdateSource` provides an opt-in, clean-worktree, fast-forward-only source update and records its duration. Without that switch the wrapper only validates the current checkout, preserving its previous behavior.

## Next measured run and decision gate

Run the normal controlled deployment once with this instrumentation and retain both the release manifest and deployment/QS timing JSON. Compare the slowest five stages, resource snapshots and total duration. Only then optimize the largest measured bottleneck.

Likely follow-up decisions, contingent on that evidence, are:

1. If build-host work dominates, build outside the application VPS and use deploy-only artifacts.
2. If frontend production dependency staging or ZIP compression dominates, rehearse Next.js standalone packaging or a faster versioned archive format and compare transfer size/time.
3. If API startup dominates, inspect the individually timed migrations and startup initialization log rather than disabling migrations or health gates.
4. If QS preparation dominates, profile its seed stages and stop reprocessing already reconciled data.
5. If page-file use rises materially during build, increase RAM or move compilation off the VPS before adding parallelism.

No production VPS deployment was run during this audit, so a before-versus-after production duration cannot yet be claimed. The next real run supplies the baseline needed for a justified optimization pass while preserving backups, health checks and rollback.

## Files changed

- `scripts/Build-RhemaRelease.ps1`
- `scripts/Deploy-RhemaVps.ps1`
- `scripts/Deploy-QsUatVps.ps1`
- `scripts/vps/Invoke-RhemaVpsRemote.ps1`
- `scripts/vps/Test-RhemaReleaseArtifactFlow.ps1`
- `docs/VPS_DEPLOYMENT_PERFORMANCE_AUDIT.md`
