# Windows VPS deployment optimization

## Runtime architecture

The active test VPS runs the ERP as three Windows services:

- `RhemaERPAPI` from `C:\RhemaERP\api` on port 5000;
- `RhemaERPFrontend` from `C:\RhemaERP\frontend` on port 3001;
- `RhemaERPCaddy` as the public HTTPS gateway.

The release process builds immutable API and frontend ZIP files, verifies and
backs up the application and SQL database, applies migrations, activates the
Windows services, seeds missing operational UAT data, and runs API, browser,
asset, CORS, and database checks. Docker files in the repository are not part
of this deployment path.

## Measured baseline

The successful `86e9e883-20261001-011531-20261001-050346` deployment reported:

| Stage | Duration |
| --- | ---: |
| Build immutable release once | 11,814.0 s (3 h 16 m 54 s) |
| Activate verified ERP release | 3,925.2 s (1 h 5 m 25 s) |
| Expand immutable API and frontend artifacts | 2,877.7 s (47 m 58 s) |
| Create disposable activation stage | 238.7 s (3 m 59 s) |
| Start API, run migrations, and wait for liveness | 229.1 s (3 m 49 s) |
| Seed and verify Procurement, Inventory and QS baseline | 170.9 s (2 m 51 s) |
| Activate frontend and wait for readiness | 140.0 s (2 m 20 s) |
| Create and verify application and SQL backups | 83.2 s (1 m 23 s) |

The build was reused during the final successful QS pass, so the one-hour
activation was independent of the Next.js compile. PowerShell `Expand-Archive`
and a single-threaded copy of a high-file-count frontend tree were the largest
measured activation costs.

## Changes

- Release creation uses native Windows `tar.exe` ZIP support and now maps
  `NoCompression`, `Fastest`, and `Optimal` to explicit ZIP compression modes.
- VPS activation uses native `tar.exe` extraction instead of PowerShell
  `Expand-Archive`.
- VPS preflight checks for `tar.exe` before backups or service interruption.
- Release-tree copies use a conservative 16-thread `robocopy` mode.
- Remote stages emit start, pass, and fail markers immediately, so a long step
  is visible rather than appearing frozen.
- Backups, migration guards, service rollback, database integrity checks,
  operational seeding, and public/browser health checks remain in place.

An existing 83.4 MB frontend release containing 7,599 files and expanding to
0.514 GB was extracted with the new native path in 9.65 seconds on the
development workstation. The same package shape must be timed on the VPS in
the next deployment; workstation timing is evidence of the implementation
gain, not a promise of identical VPS performance.

## Validation

The focused release tests cover:

- native Fastest and uncompressed ZIP creation and extraction;
- PowerShell parsing for all changed deployment scripts;
- immutable artifact build and deploy-only contracts;
- migration preflight coverage;
- QS wrapper failure, success, and artifact-reuse paths;
- active migration discovery metadata;
- frontend runtime layout.

The next deployment timing evidence is the acceptance check. Compare its
`Expand immutable API and frontend artifacts`, `Create disposable activation
stage`, `Activate verified ERP release`, and total durations with this baseline.

## Current QS readiness result

The completed deployment reported the operational baseline as passed and three
ready land records. It also reported `QS_CONFIGURATION|OWNER_REVIEW_REQUIRED`
and `QS_END_TO_END|NOT_YET_VERIFIED`. The generated prerequisite JSON and HTML
walkthrough must therefore be reviewed before calling the QS UAT fully ready.
