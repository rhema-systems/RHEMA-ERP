Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Test-GlCutoverEvidencePackage.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_FINAL_PACKAGE_$([guid]::NewGuid().ToString('N'))"
$head = 'a' * 40
$tree = 'b' * 40
$fixtureFingerprint = '1|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28'

function Write-HistoryFixture([string]$name, [string[]]$ids) {
    $state = if ($ids.Count -eq 0) { 'EMPTY' } else { 'POPULATED' }
    @("RHEMA_MIGRATION_HISTORY_V1|COUNT=$($ids.Count)|STATE=$state") + @($ids) |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $root $name)
}

function Write-Manifest([string]$directory) {
    $manifest = Join-Path $directory 'manifest.sha256'
    Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object FullName -ne $manifest |
        Sort-Object FullName | ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($directory, $_.FullName).Replace('\', '/')
            "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)  $relative"
        } | Set-Content -Encoding ascii -LiteralPath $manifest
}

function Write-CommandEvidenceFixture([string]$file, [string]$command, [string[]]$output = @()) {
    @($output) + "RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=$command" |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $root $file)
}

function Write-Summary([string]$status, [string[]]$pending) {
    $summary = [ordered]@{
        status=$status; gitHead=$head; gitTree=$tree; reviewedCommit=$head; reviewedTree=$tree; repositoryClean=$true
        sourceDatabase='RhemaERP'; targetDatabase='RHEMAERP_GL_REHEARSAL_FINAL_TEST'
        sourceServer='<REDACTED_SAME_SERVER>'; targetServer='<REDACTED_SAME_SERVER>'; sameServer=$true
        repositoryMigrationCount=1; latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline'
        migrationHistoryEvidenceSchema='RHEMA_MIGRATION_HISTORY_V1'
        pendingMigrationCount=$pending.Count; pendingMigrations=@($pending)
        sourceFingerprint=$script:fixtureFingerprint
        cutoverFlagsExplicitlyFalse=$true; targetCreated=($status -eq 'PASS'); backupCreated=($status -eq 'PASS')
    }
    if ($status -eq 'PASS') {
        $summary.backupSha256 = 'C' * 64
        $summary.backupMediaId = 'd' * 32
        $summary.backupReservation = 'FILEMODE_CREATE_NEW'
        $summary.pendingMigrationScriptSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
        $summary.invariantPass1Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'invariants-pass-1.txt')).Hash
        $summary.invariantPass2Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'invariants-pass-2.txt')).Hash
        $summary.backupRestoreEvidenceSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'backup-restore-checkdb.txt')).Hash
        $summary.targetMigrationHistorySha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'target-migration-history.txt')).Hash
    }
    $artifacts = [ordered]@{}
    Get-ChildItem -LiteralPath $root -File | Where-Object Name -notin @('summary.json','manifest.sha256') |
        Sort-Object Name | ForEach-Object { $artifacts[$_.Name]=(Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash }
    $summary.artifactSha256 = $artifacts
    $summary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'summary.json')
}

function Assert-Refused([string]$expected) {
    Write-Manifest $root
    $output = & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "Invalid evidence unexpectedly passed: $expected" }
    if ($output -notmatch [regex]::Escape($expected)) { throw "Expected refusal '$expected'. Output: $output" }
}

try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $migrationIds = @('20260913162402_DisposableDevelopmentCurrentModelBaseline')
    $orphanId = '20260902140000_AddFixedAssetDepreciationConventionEvidence'
    $sourceIds = @($orphanId)
    $pending = @($migrationIds | Where-Object { $_ -notin $sourceIds })
    Write-CommandEvidenceFixture 'migration-discovery.log' 'dotnet' $migrationIds
    Write-HistoryFixture 'source-migration-history.txt' $sourceIds
    Write-HistoryFixture 'pending-migrations.txt' $pending
    Write-HistoryFixture 'orphan-history.txt' @($orphanId)
    Write-CommandEvidenceFixture 'commit-ancestry.txt' 'git' @($head)
    Write-CommandEvidenceFixture 'git-head-tree.txt' 'git' @($head,$tree)
    [ordered]@{ reviewedCommit=$head; reviewedTree=$tree; executedCommit=$head; executedTree=$tree; repositoryClean=$true } |
        ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'reviewed-git-state.json')
    Write-CommandEvidenceFixture 'git-diff-check.log' 'git'
    foreach ($file in @('clone-build.log','ef-no-pending-model.log')) { Write-CommandEvidenceFixture $file 'dotnet' @('offline validated') }
    'BLOCKER historical FX evidence' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-readiness.txt')
    Write-CommandEvidenceFixture 'idempotent-script-generation.log' 'dotnet' @('offline generation passed')
    '-- synthetic exact pending-range idempotent SQL' | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')
    $idempotentHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sha256')
    $fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    $fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')
    [ordered]@{ accountingEvents=$false; producerIntents=$false; producerIntentGroups=$false; source='explicit process environment variables' } |
        ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'feature-flags.json')
    Write-Summary 'NO_GO_PREFLIGHT' $pending
    & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Valid final baseline preflight NO-GO package was rejected.' }

    foreach ($schemaTamper in @(
        @{label='singleton-array'; value=[object[]]@('RHEMA_MIGRATION_HISTORY_V1'); expected='exact scalar JSON String'},
        @{label='multi-array'; value=[object[]]@('RHEMA_MIGRATION_HISTORY_V1','EXTRA'); expected='exact scalar JSON String'},
        @{label='boolean'; value=$true; expected='exact scalar JSON String'},
        @{label='number'; value=[long]1; expected='exact scalar JSON String'},
        @{label='object'; value=[pscustomobject]@{name='RHEMA_MIGRATION_HISTORY_V1'}; expected='exact scalar JSON String'},
        @{label='null'; value=$null; expected="requires non-null property 'migrationHistoryEvidenceSchema'"},
        @{label='missing'; missing=$true; expected="requires non-null property 'migrationHistoryEvidenceSchema'"}
    )) {
        $summary = Get-Content -Raw -LiteralPath (Join-Path $root 'summary.json') | ConvertFrom-Json
        if ($schemaTamper.ContainsKey('missing')) { $summary.PSObject.Properties.Remove('migrationHistoryEvidenceSchema') }
        else { $summary.migrationHistoryEvidenceSchema = $schemaTamper.value }
        $summary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'summary.json')
        Assert-Refused $schemaTamper.expected
        Write-Summary 'NO_GO_PREFLIGHT' $pending
    }
    Write-Host 'PASS: FinalClone migration-history schema requires one exact scalar JSON String'

    foreach ($summaryTamper in @(
        @{property='repositoryMigrationCount';value=2;expected='summary repository migration count/latest'},
        @{property='latestMigration';value='20260913162403_Unreviewed';expected='summary repository migration count/latest'},
        @{property='sourceFingerprint';value='2|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28';expected='source fingerprint is not stable'}
    )) {
        $summary=Get-Content -Raw -LiteralPath (Join-Path $root 'summary.json') | ConvertFrom-Json
        $summary.($summaryTamper.property)=$summaryTamper.value
        $summary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'summary.json')
        Assert-Refused $summaryTamper.expected
        Write-Summary 'NO_GO_PREFLIGHT' $pending
    }
    Write-Host 'PASS: final-clone summary migration identity and source fingerprint remanifest tampering is refused'

    $gitDiffPath = Join-Path $root 'git-diff-check.log'
    $validGitDiffEvidence = @(Get-Content -LiteralPath $gitDiffPath)
    Remove-Item -LiteralPath $gitDiffPath
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'Required rehearsal evidence is missing: git-diff-check.log'
    $validGitDiffEvidence | Set-Content -Encoding utf8 -LiteralPath $gitDiffPath
    Clear-Content -LiteralPath $gitDiffPath
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'Required command evidence is empty: git-diff-check.log'
    @('RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=1|COMMAND=git') |
        Set-Content -Encoding utf8 -LiteralPath $gitDiffPath
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'Required command evidence has a missing or invalid success marker: git-diff-check.log'
    $validGitDiffEvidence | Set-Content -Encoding utf8 -LiteralPath $gitDiffPath

    $generationPath = Join-Path $root 'idempotent-script-generation.log'
    $idempotentScriptPath = Join-Path $root 'pending-migrations-idempotent.sql'
    $idempotentHashPath = Join-Path $root 'pending-migrations-idempotent.sha256'
    $zeroPending = @()
    Write-HistoryFixture 'source-migration-history.txt' $migrationIds
    $script:fixtureFingerprint = "1|$($migrationIds[0])|1|9|28"
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')
    Write-HistoryFixture 'pending-migrations.txt' @()
    Write-HistoryFixture 'orphan-history.txt' @()
    '-- NO PENDING MIGRATIONS AT FRESH DISCOVERY' | Set-Content -Encoding ascii -LiteralPath $idempotentScriptPath
    $idempotentHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $idempotentScriptPath).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii -LiteralPath $idempotentHashPath
    $notRequiredMarker = 'RHEMA_IDEMPOTENT_SCRIPT_GENERATION_V1|STATUS=NOT_REQUIRED|REASON=ZERO_PENDING_MIGRATIONS|PENDING_COUNT=0'
    $notRequiredMarker | Set-Content -Encoding utf8 -LiteralPath $generationPath
    Write-Summary 'NO_GO_PREFLIGHT' $zeroPending
    & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Valid zero-pending final preflight NO-GO package was rejected.' }
    foreach ($historyName in @('pending-migrations.txt','orphan-history.txt')) {
        $historyPath = Join-Path $root $historyName
        Remove-Item -LiteralPath $historyPath -Force
        Write-Summary 'NO_GO_PREFLIGHT' $zeroPending
        Assert-Refused "Required rehearsal evidence is missing: $historyName"
        '' | Set-Content -NoNewline -Encoding utf8 -LiteralPath $historyPath
        Write-Summary 'NO_GO_PREFLIGHT' $zeroPending
        Assert-Refused 'evidence is empty or lacks its deterministic terminal newline'
        'RHEMA_MIGRATION_HISTORY_V1|COUNT=1|STATE=EMPTY' | Set-Content -Encoding utf8 -LiteralPath $historyPath
        Write-Summary 'NO_GO_PREFLIGHT' $zeroPending
        Assert-Refused 'evidence count, state, or exact ordered IDs are inconsistent'
        Write-HistoryFixture $historyName @()
    }
    Write-Host 'PASS: FinalClone explicit empty migration sets refuse missing, empty and tampered markers'
    Remove-Item -LiteralPath $generationPath
    Write-Summary 'NO_GO_PREFLIGHT' $zeroPending; Assert-Refused 'Required rehearsal evidence is missing: idempotent-script-generation.log'
    'RHEMA_IDEMPOTENT_SCRIPT_GENERATION_V1|STATUS=NOT_REQUIRED|REASON=ZERO_PENDING_MIGRATIONS|PENDING_COUNT=1' |
        Set-Content -Encoding utf8 -LiteralPath $generationPath
    Write-Summary 'NO_GO_PREFLIGHT' $zeroPending; Assert-Refused 'Zero-pending idempotent-script evidence is missing or invalid'
    Write-CommandEvidenceFixture 'idempotent-script-generation.log' 'dotnet' @('fabricated generation')
    Write-Summary 'NO_GO_PREFLIGHT' $zeroPending; Assert-Refused 'Zero-pending idempotent-script evidence is missing or invalid'

    Write-HistoryFixture 'source-migration-history.txt' $sourceIds
    $script:fixtureFingerprint = "1|$orphanId|1|9|28"
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')
    Write-HistoryFixture 'pending-migrations.txt' $pending
    Write-HistoryFixture 'orphan-history.txt' @($orphanId)
    '-- synthetic exact pending-range idempotent SQL' | Set-Content -Encoding ascii -LiteralPath $idempotentScriptPath
    $idempotentHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $idempotentScriptPath).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii -LiteralPath $idempotentHashPath
    Write-CommandEvidenceFixture 'idempotent-script-generation.log' 'dotnet' @('offline generation passed')

    $flagsPath = Join-Path $root 'feature-flags.json'; $validFlags = Get-Content -Raw -LiteralPath $flagsPath
    $flags = $validFlags | ConvertFrom-Json; $flags.producerIntentGroups=$true; $flags | ConvertTo-Json | Set-Content -Encoding utf8 $flagsPath
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'does not prove C6, C7 and C8 explicitly false'; $validFlags | Set-Content -Encoding utf8 $flagsPath

    $badPending = @()
    Write-HistoryFixture 'pending-migrations.txt' @()
    Write-Summary 'NO_GO_PREFLIGHT' $badPending; Assert-Refused 'repository history minus source history'
    Write-HistoryFixture 'pending-migrations.txt' $pending

    Write-HistoryFixture 'orphan-history.txt' @()
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'source history minus repository history'
    Write-HistoryFixture 'orphan-history.txt' @($orphanId)

    $reviewed = Get-Content -Raw (Join-Path $root 'reviewed-git-state.json') | ConvertFrom-Json
    $reviewed.executedTree = 'd' * 40; $reviewed | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $root 'reviewed-git-state.json')
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'exact reviewed and clean executed HEAD/tree'
    $reviewed.executedTree = $tree; $reviewed | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $root 'reviewed-git-state.json')

    'READY' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-readiness.txt')
    @('SOURCE_DATABASE=RhemaERP','TARGET_DATABASE=RHEMAERP_GL_REHEARSAL_FINAL_TEST',('BACKUP_MEDIA_ID=' + ('d' * 32)),'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE','RESTORE_VERIFYONLY_CHECKSUM_COMPLETE','RESTORE_TARGET_COMPLETE','DBCC_CHECKDB_PHYSICAL_ONLY_COMPLETE') |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'backup-restore-checkdb.txt')
    (('C' * 64) + '  RHEMAERP_GL_REHEARSAL_FINAL_TEST_COPYONLY.bak') | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'backup.sha256')
    foreach ($file in @('clone-apply-migrations.log','seed-pass-1.log','seed-pass-2.log')) { Write-CommandEvidenceFixture $file 'dotnet' @('PASS') }
    $targetIds = @(@($sourceIds) + @($pending) | Sort-Object -Unique)
    Write-HistoryFixture 'target-migration-history.txt' $targetIds
    $canonicalInvariantRows = @(
        'ACCOUNT|1|1000|Cash',
        'ACCOUNT_SEGMENT_VALUE|1|1000|00',
        'ACCOUNT_BALANCE|1|1000|10.00',
        'ACCOUNT_CURRENCY_EXPOSURE|1|USD|20.00',
        'ACCOUNTING_BOOK_PERIOD|1|Open',
        'ACCOUNTING_BOOK_INITIALIZATION|1|Approved',
        'ACCOUNTING_BOOK_INITIALIZATION_LINE|1|10.00|10.00',
        'JOURNAL_ENTRY|1|JE-001|Posted',
        'ACCOUNT_TRANSACTION|1|1000|10.00',
        'FINANCE_POSTING_EVENT|1|INV|Posted',
        'PRODUCER_INTENT_GROUP|1|Approved'
    )
    $canonicalInvariantRows | Set-Content -Encoding utf8 (Join-Path $root 'invariants-pass-1.txt')
    Copy-Item (Join-Path $root 'invariants-pass-1.txt') (Join-Path $root 'invariants-pass-2.txt')
    $invariantHash=(Get-FileHash -Algorithm SHA256 (Join-Path $root 'invariants-pass-1.txt')).Hash
    @("$invariantHash  invariants-pass-1.txt","$invariantHash  invariants-pass-2.txt") | Set-Content -Encoding ascii (Join-Path $root 'checksums.sha256')
    Write-Summary 'PASS' $pending
    & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Valid final baseline PASS package was rejected.' }

    Write-HistoryFixture 'source-migration-history.txt' $migrationIds
    $script:fixtureFingerprint = "1|$($migrationIds[0])|1|9|28"
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')
    Write-HistoryFixture 'pending-migrations.txt' @()
    Write-HistoryFixture 'orphan-history.txt' @()
    Write-HistoryFixture 'target-migration-history.txt' $migrationIds
    '-- NO PENDING MIGRATIONS AT FRESH DISCOVERY' | Set-Content -Encoding ascii -LiteralPath $idempotentScriptPath
    $idempotentHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $idempotentScriptPath).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii -LiteralPath $idempotentHashPath
    $notRequiredMarker | Set-Content -Encoding utf8 -LiteralPath $generationPath
    Write-Summary 'PASS' $zeroPending
    & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Valid zero-pending final PASS package was rejected.' }

    Write-HistoryFixture 'source-migration-history.txt' $sourceIds
    $script:fixtureFingerprint = "1|$orphanId|1|9|28"
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    $script:fixtureFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')
    Write-HistoryFixture 'pending-migrations.txt' $pending
    Write-HistoryFixture 'orphan-history.txt' @($orphanId)
    Write-HistoryFixture 'target-migration-history.txt' $targetIds
    '-- synthetic exact pending-range idempotent SQL' | Set-Content -Encoding ascii -LiteralPath $idempotentScriptPath
    $idempotentHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $idempotentScriptPath).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii -LiteralPath $idempotentHashPath
    Write-CommandEvidenceFixture 'idempotent-script-generation.log' 'dotnet' @('offline generation passed')

    $badTarget=@($targetIds); $badTarget[0]='00000000000000_SameCountMutation'; Write-HistoryFixture 'target-migration-history.txt' @($badTarget | Sort-Object)
    Write-Summary 'PASS' $pending; Assert-Refused 'exactly source history union the ordered pending delta'
    Write-HistoryFixture 'target-migration-history.txt' $targetIds

    $markers=Get-Content (Join-Path $root 'backup-restore-checkdb.txt'); $verifyIndex=[Array]::IndexOf($markers,'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE'); $markers[$verifyIndex]='RESTORE_TARGET_COMPLETE'; $markers | Set-Content -Encoding utf8 (Join-Path $root 'backup-restore-checkdb.txt')
    Write-Summary 'PASS' $pending; Assert-Refused 'identity-inconsistent'
    $markers[$verifyIndex]='RESTORE_VERIFYONLY_CHECKSUM_COMPLETE'; $markers | Set-Content -Encoding utf8 (Join-Path $root 'backup-restore-checkdb.txt')

    Write-Summary 'PASS' $pending
    Add-Content -Encoding utf8 -LiteralPath (Join-Path $root 'backup-restore-checkdb.txt') -Value ' '
    Assert-Refused 'backup/restore/DBCC evidence hash disagrees'
    $markers | Set-Content -Encoding utf8 (Join-Path $root 'backup-restore-checkdb.txt')

    foreach ($prefix in @('ACCOUNT_BALANCE|','ACCOUNT_CURRENCY_EXPOSURE|','ACCOUNTING_BOOK_PERIOD|',
        'ACCOUNTING_BOOK_INITIALIZATION|','ACCOUNTING_BOOK_INITIALIZATION_LINE|','JOURNAL_ENTRY|',
        'ACCOUNT_TRANSACTION|','FINANCE_POSTING_EVENT|')) {
        $mutatedRows = @($canonicalInvariantRows)
        $index = 0..($mutatedRows.Count-1) | Where-Object { $mutatedRows[$_].StartsWith($prefix, [StringComparison]::Ordinal) } | Select-Object -First 1
        $mutatedRows[$index] = $mutatedRows[$index] + '|SAME_COUNT_MUTATION'
        $mutatedRows | Set-Content -Encoding utf8 (Join-Path $root 'invariants-pass-2.txt')
        Write-Summary 'PASS' $pending
        Assert-Refused 'two-pass invariant hashes are not identical'
    }
    $canonicalInvariantRows | Set-Content -Encoding utf8 (Join-Path $root 'invariants-pass-2.txt')

    'SELECT 1; -- Server=secret-host' | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sql')
    $changed=(Get-FileHash -Algorithm SHA256 (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    "$changed  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sha256')
    Write-Summary 'PASS' $pending; Assert-Refused 'forbidden machine/connection marker'

    '-- synthetic exact pending-range idempotent SQL' | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sql')
    $idempotentHash=(Get-FileHash -Algorithm SHA256 (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sha256')
    'BLOCKER historical FX evidence' | Set-Content -Encoding utf8 (Join-Path $root 'source-readiness.txt')
    Write-Summary 'NO_GO_PREFLIGHT' $pending
    @('tampered after summary','RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=dotnet') |
        Set-Content -Encoding utf8 (Join-Path $root 'clone-build.log')
    Assert-Refused 'artifact hash binding is missing or invalid'
    Write-CommandEvidenceFixture 'clone-build.log' 'dotnet' @('offline validated')
    Write-Summary 'NO_GO_PREFLIGHT' $pending
    $unsafeSummary=Get-Content -Raw (Join-Path $root 'summary.json') | ConvertFrom-Json
    $unsafeSummary | Add-Member -NotePropertyName failure -NotePropertyValue 'C:\Users\Operator\source-host failure'
    $unsafeSummary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $root 'summary.json')
    Assert-Refused 'forbidden machine/connection marker'
    Write-Host 'PASS: exact-history, marker, invariant, reviewed-state, and SQL sanitization tampering is refused'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
