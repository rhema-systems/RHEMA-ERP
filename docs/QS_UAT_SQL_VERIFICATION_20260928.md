# QS UAT isolated SQL verification

Prepared database: `RhemaERP_QsUatVerify_20260928_005614_eb68eed8` on `RHEMA-MICHAEL\SQL2017`.

The exact source was `RhemaERP_ReceiptUpgrade_20260927_023608_918225d0`, with 62 migrations ending in `20260927211546_InventoryIssueOptionalWorkflowApproval`. Preparation used a unique COPY_ONLY compressed backup with checksum, RESTORE VERIFYONLY, separate data/log files without overwrite, and a physical CHECKDB. Source and restored preservation digests matched; the source remained unchanged.

Evidence: `tmp/qs-uat-sql-clone-20260928_005614_eb68eed8.json`. A read-only verification of the unchanged clone against this baseline also passed: `tmp/qs-uat-sql-verify-20260928_005718_41021c9b.json`.

The reusable `scripts/acceptance/Invoke-QsUatSqlVerification.ps1` uses integrated authentication. It never emits connection strings, credentials, stored password hashes or profile contents. Evidence contains only row identities, SHA256 digests, counts and safe target metadata.

Before running the CLI seed, set its opt-in and exact database guard to this isolated target. Parent-owned CLI wiring supplies the DEFAULT bootstrap context; this script does not alter app configuration or start the CLI.

After the first seed:

```powershell
$clone = 'tmp/qs-uat-sql-clone-20260928_005614_eb68eed8.json'
./scripts/acceptance/Invoke-QsUatSqlVerification.ps1 -Mode Verify -CloneEvidence $clone -Baseline $clone
```

Use the successful verification's emitted evidence path as the baseline for the second identical CLI invocation:

```powershell
./scripts/acceptance/Invoke-QsUatSqlVerification.ps1 -Mode Verify -CloneEvidence $clone -Baseline '<first successful verification evidence>' -RequireIdempotent
```

Checks retain every pre-existing user, partner profile, Estate land/demarcation and Project development profile row exactly. No Finance, AP/AR, payment, stock, purchasing or payment-certificate transaction may be added, deleted or changed. First run may add new master rows; repeated runs must also preserve their counts and identity/workflow link counts. Users are compared as complete row digests: even an identity concurrency-stamp update is reported and must be explained separately from password changes.

`-Mode Capture -CloneEvidence $clone` records an additional read-only checkpoint. All Capture/Verify SQL is read-only. Only Clone creates a new database; it refuses an existing target and never restores over the source.

Seed execution, rerun preservation and live Estate selector verification remain pending the parent-coordinated CLI run. The initial read-only pass alone does not certify those actions.

