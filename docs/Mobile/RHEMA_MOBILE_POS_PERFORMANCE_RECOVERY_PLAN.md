# RHEMA Mobile POS Performance and Recovery Plan

## Purpose

This plan defines the repeatable MPOS-0805 checks and separates repository-host evidence from claims that require a signed Android build, a physical Z92S, controlled networks, and a measured operating shift.

## Automated host rehearsal

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/acceptance/Invoke-MobilePosRecoveryRehearsal.ps1
```

Use `-SkipInstall` only for a local iteration that already has the lockfile dependencies restored. The release evidence run must omit it.

The rehearsal fails unless all of these controls pass:

1. 1,000 distinct sale-shaped payloads canonicalize and hash without identity collisions in less than 5 seconds on the build host.
2. The real `MobilePosOutboxDispatcher` processes a 1,000 command in-memory backlog once per mutation in less than 10 seconds on the build host.
3. A response lost after server completion is retried with the same client mutation ID and payload hash, and only the canonical replay is marked synced.
4. Retry backoff remains capped at 15 minutes.
5. Stale `Syncing` records are returned to `Pending` with the original scope and an explicit interruption reason.

These generous time limits detect accidental algorithmic regressions. They are not Android, network, API, SQL Server, or end-to-end service-level objectives. Each run writes ignored local JSON and logs under `.artifacts/mobile-pos/recovery-rehearsal/<UTC timestamp>/`.

## UAT and device matrix

The following evidence must be captured on the signed release candidate and the physical Z92S before MPOS-0805 can be complete.

| Area | Scenario | Required evidence |
| --- | --- | --- |
| Online performance | Login/MFA, bootstrap, catalogue search, preview, sale completion, receipt load, session reconciliation, day-end submit | At least 30 measured operations per route on the UAT network; report median, p95, maximum, failures, API correlation IDs, and test-data window |
| Local performance | Cold start, warm start, cached catalogue search, 100-line cart render, 1,000 queued command list, receipt render | Android timing capture on the target Z92S build with firmware/build identifiers |
| Network loss | Disconnect before submit, during request, after server commit/before response, and during catalogue paging | Screen recording plus device/API/SQL evidence proving retained work, stable mutation identity, canonical replay, and no duplicate invoice/payment |
| Recovery | Process kill during dispatch, device reboot with pending work, expired grant, revoked device, stale assignment, rejected/conflicting command | Before/after outbox state, operator message, audit/correlation evidence, and Finance document counts |
| Upgrade | Install the next signed APK over a build containing cached catalogue, retained grant, pending, rejected, and synced records | Database migration/version evidence and record-by-record preservation result; rollback decision recorded |
| Queue volume | 100, 500, and 1,000 pending commands under throttled and intermittent connectivity | Drain duration, successful/rejected/review counts, retry schedule, memory use, and duplicate-document query |
| Battery | Representative eight-hour cashier shift with configured screen/scanner/printer/network use | Start/end battery, charging periods, thermal warnings, crashes, Android battery report, and observed transaction volume |
| Z92S hardware | Repeated print, out-of-paper, cover/open or printer fault where supported, scanner trigger/decode/cancel, recovery after app/device restart | Physical test record tied to device serial, Android/firmware, SDK hash, APK hash, and adapter result codes |
| Resource pressure | Low storage, background/foreground transitions, OS reclaim, clock/time-zone change | Operator-visible behavior, retained data result, diagnostic reference, and recovery steps |

## Acceptance rules

- Every server write must retain its original mutation identity across retry and must reconcile to no more than one canonical business result.
- A retryable transport failure remains pending with bounded backoff. Explicit rejection, conflict, and manual-review states are never reposted automatically.
- App restart, device restart, and approved APK upgrade must preserve governed local state. Uninstall/clear-data is destructive and must be recorded as such.
- Failed or ambiguous work must remain visible to the operator and HQ review; a day end cannot silently finalize ambiguous Finance work.
- Battery, hardware, and network figures must name the APK commit/hash, device, firmware, environment, network profile, sample size, and measurement method.
- Production readiness cannot be inferred from the host rehearsal alone.

## Current evidence boundary

The repository can prove deterministic serialization, bounded retry, stable replay identity, dispatch state handling, and the interrupted-claim SQL contract. The current workstation has no Android SDK/Java runtime or physical Z92S, so native resource use, battery life, real network transitions, SQLite durability after process/device restart, APK upgrade preservation, and printer/scanner recovery remain pending.
