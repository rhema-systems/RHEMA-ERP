# Inventory actual receipt fresh-install acceptance

**Passed on 27 September 2026 at 09:22 UTC.** The complete uninterrupted migration chain applied from an empty database through `20260927021852_InventoryIssueActualReceipts`. Receipt schema, lifecycle/replay guards, trusted constraints and physical integrity checks passed. This closes the fresh-install gate only; it does not substitute for visible receipt lifecycle acceptance.

The repeatable verification entry point is `scripts/acceptance/Test-InventoryReceiptFreshInstall.ps1`. It requires PowerShell 7 and an existing normal API build; it does not build, seed, drop databases, edit user secrets, or change application configuration. It creates a unique `RhemaERP_ReceiptFresh_<timestamp>_<suffix>` database on the approved local SQL2017 instance and retains that database for inspection.

The acceptance gate applies the complete migration chain from an empty database, compares all 60 history entries with the source chain ending in `20260927021852_InventoryIssueActualReceipts`, verifies the receipt schema and lifecycle/replay guards, then runs a physical integrity check. Safe evidence is written to `tmp/rf-<timestamp>_<suffix>/fresh-install.json`; connection strings and raw subprocess output are not persisted.

## Attempt on 27 September 2026

The first execution was cancelled before any completion output because concurrent host work made even small shell reads take several minutes. Only the verification command's own session received Ctrl+C; the SQL service was not stopped. This cancelled attempt is **not** fresh-install acceptance evidence. No completed migration, created target, or schema verification is claimed for that attempt.

Run only one heavy EF process at a time. Use the explicit heap-limit command below, informed by the subsequent memory-limit diagnosis. The subsequent successful evidence manifest is recorded below.

## Full-chain attempt and diagnostic continuation

The subsequent fresh run created `RhemaERP_ReceiptFresh_20260927_083708_c9269256` and failed in the migration CLI with exit `-532462766` (`0xE0434352`). Evidence: `tmp/rf-20260927_083708_c9269256/fresh-install.json`. The target was retained. Windows Application Error and WER records confirm a managed process crash, but contain no managed exception type, out-of-memory marker, or stack-overflow marker. Memory exhaustion is therefore not established as the cause.

The harness now records child process ID, configured heap limit, hexadecimal exit code, peak working set, known runtime-failure labels, and the owned target's migration history after failure. It still discards raw child output after hashing and extracting allowlisted facts. `-HeapLimitGiB` defaults to 4; an increased limit is an explicit diagnostic choice, not a silent change to runtime configuration.

`-DiagnosticResumeEvidence <failed-manifest>` allows diagnosis against the retained target only when the original evidence proves creation from an empty database and the API/Data assembly hashes are unchanged. It validates server, target naming and the exact incomplete migration-history prefix before applying remaining migrations. This mode sets `DiagnosticOnly=true`, `AcceptanceEligible=false`, and leaves `Passed=false`, even if the remaining migrations succeed. A successful diagnostic resume cannot substitute for a complete fresh run.

The guarded diagnostic completed successfully at 08:57 UTC with the same binaries and 4 GiB heap limit. History advanced from the verified first 55 migrations to all 60; receipt schema, trusted constraints, replay uniqueness, enabled lifecycle guards and physical integrity checks passed. Evidence: `tmp/rf-20260927_085001_68125600/fresh-install.json`. No runtime-failure labels were detected. This demonstrates that the remaining migrations can apply to the preserved empty-origin target; it does not identify the earlier crash or satisfy the uninterrupted fresh-install gate. The diagnostic's `ExistingDatabaseWrites=false` field predates the distinction between protected databases and its owned retained target: that target was intentionally written, while the source/application databases were not. The harness now records those scopes separately.

## Reproduced full-chain memory limit

A serialized new fresh run on `RhemaERP_ReceiptFresh_20260927_085827_1f1256bb` reproduced the crash after 55 of 60 migrations with the same 4 GiB managed heap cap. This time the safe output extraction detected **OutOfMemory**, establishing memory exhaustion for this attempt. The last applied migration was `20260925194500_AddScopedApWithholdingThresholds`; no SQL error or migration guard code was reported. The 4 GiB cap applies to the managed heap, so total process working set can exceed it.

- Evidence: `tmp/rf-20260927_085827_1f1256bb/fresh-install.json`.
- Runtime: 08:58:27–09:08:39 UTC, approximately 10 minutes 12 seconds.
- Exit: `0xE0434352`; observed peak working set: 5,104,947,200 bytes (approximately 4.75 GiB).
- Target retained; source/application databases and runtime settings were not changed.

At this point, the remaining migrations had passed on diagnostic resume, while uninterrupted fresh installation still required a successful new run. The next attempt increased only the harness subprocess's heap limit and preserved the same complete migration chain; no migration or validation bypass was used.

For the current large model, use an explicit 8 GiB managed heap limit on a host with adequate free memory, with other heavy EF builds/migrations stopped:

```powershell
pwsh -NoProfile -File .\scripts\acceptance\Test-InventoryReceiptFreshInstall.ps1 -HeapLimitGiB 8 -Execute
```

This option applies only to the migration subprocess; it does not change the running API environment or application configuration.

## Successful uninterrupted fresh installation

The 8 GiB run used the same prebuilt API/Data assemblies and a new isolated target, `RhemaERP_ReceiptFresh_20260927_091026_ec5d1d0c`. It started with zero user tables and zero migrations, then applied the entire chain successfully in one process. Evidence: `tmp/rf-20260927_091026_ec5d1d0c/fresh-install.json`.

| Verification | Result |
| --- | --- |
| Exact ordered migration history | All 60 source migration IDs match, ending in `20260927021852_InventoryIssueActualReceipts` |
| Receipt schema | Receipt sequence and replay fields, receipt-line table, 14 columns, and required decimal quantity shape present |
| Receipt relationships and replay indexes | Three trusted restrictive foreign keys; unique action replay and receipt-line indexes enabled |
| Lifecycle enforcement | All three receipt/voucher/action triggers enabled, including the receipt sequence guard patch |
| Check constraints | Quantity, receipt sequence and action type constraints enabled and trusted |
| Physical integrity | `DBCC CHECKDB ... WITH PHYSICAL_ONLY` passed |
| CLI result | Exit 0; no extracted exception, SQL error, guard code or runtime-failure marker |
| Isolation | No source/application database writes, runtime configuration changes or seeding; new target retained |

Elapsed time: 09:10:26–09:22:01 UTC, 694.81 seconds (11 minutes 34.81 seconds). Observed peak working set: 5,285,335,040 bytes (approximately 4.92 GiB). The successful run establishes that 8 GiB is sufficient for this build's full chain on the verification host; the earlier 4 GiB limit was insufficient.

API SHA-256: `A7652385B2E75B976F5FB293F2251EB2DAB36CA60F5668436B29CFEB2D9B3CC6`.

Data SHA-256: `6FA236C748D222C327A293617FF981B71E064D3488B13CC89A23DE1FD8AB79D2`.
