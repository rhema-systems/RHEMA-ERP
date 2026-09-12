Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Test-GlCutoverEvidencePackage.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_FINAL_PACKAGE_$([guid]::NewGuid().ToString('N'))"
$head = 'a' * 40
$tree = 'b' * 40

function Write-Manifest([string]$directory) {
    $manifest = Join-Path $directory 'manifest.sha256'
    Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object FullName -ne $manifest |
        Sort-Object FullName | ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($directory, $_.FullName).Replace('\', '/')
            "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)  $relative"
        } | Set-Content -Encoding ascii -LiteralPath $manifest
}

function Write-Summary([string]$status, [string[]]$pending) {
    $summary = [ordered]@{
        status=$status; gitHead=$head; gitTree=$tree; reviewedCommit=$head; reviewedTree=$tree; repositoryClean=$true
        sourceDatabase='RhemaERP'; targetDatabase='RHEMAERP_GL_REHEARSAL_FINAL_TEST'
        sourceServer='<REDACTED_SAME_SERVER>'; targetServer='<REDACTED_SAME_SERVER>'; sameServer=$true
        repositoryMigrationCount=456; latestMigration='20260908120000_AddProducerIntentGroupsC8'
        pendingMigrationCount=$pending.Count; pendingMigrations=@($pending)
        sourceFingerprint='456|20260908120000_AddProducerIntentGroupsC8|1|9|28'
        cutoverFlagsExplicitlyFalse=$true; targetCreated=($status -eq 'PASS'); backupCreated=($status -eq 'PASS')
    }
    if ($status -eq 'PASS') {
        $summary.backupSha256 = 'C' * 64
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
    $migrationIds = @(1..455 | ForEach-Object { '{0:D14}_SyntheticMigration{1:D3}' -f $_,$_ }) + '20260908120000_AddProducerIntentGroupsC8'
    $orphanId = '20260817030000_AddFixedAssetLocationMasterLinks'
    $sourceIds = @(@($migrationIds[0..442]) + $orphanId | Sort-Object)
    $pending = @($migrationIds | Where-Object { $_ -notin $sourceIds })
    $migrationIds | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'migration-discovery.log')
    $sourceIds | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'source-migration-history.txt')
    $pending | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations.txt')
    $orphanId | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'orphan-history.txt')
    $head | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'commit-ancestry.txt')
    @($head,$tree) | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'git-head-tree.txt')
    [ordered]@{ reviewedCommit=$head; reviewedTree=$tree; executedCommit=$head; executedTree=$tree; repositoryClean=$true } |
        ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'reviewed-git-state.json')
    foreach ($file in @('git-diff-check.log','clone-build.log','ef-no-pending-model.log')) { 'offline validated' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root $file) }
    'BLOCKER historical FX evidence' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-readiness.txt')
    'offline generation passed' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'idempotent-script-generation.log')
    '-- synthetic exact pending-range idempotent SQL' | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')
    $idempotentHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sha256')
    '456|20260908120000_AddProducerIntentGroupsC8|1|9|28' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    '456|20260908120000_AddProducerIntentGroupsC8|1|9|28' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')
    [ordered]@{ accountingEvents=$false; producerIntents=$false; producerIntentGroups=$false; source='explicit process environment variables' } |
        ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'feature-flags.json')
    Write-Summary 'NO_GO_PREFLIGHT' $pending
    & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Valid final 456/C8 preflight NO-GO package was rejected.' }

    $flagsPath = Join-Path $root 'feature-flags.json'; $validFlags = Get-Content -Raw -LiteralPath $flagsPath
    $flags = $validFlags | ConvertFrom-Json; $flags.producerIntentGroups=$true; $flags | ConvertTo-Json | Set-Content -Encoding utf8 $flagsPath
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'does not prove C6, C7 and C8 explicitly false'; $validFlags | Set-Content -Encoding utf8 $flagsPath

    $badPending = @($pending[1..($pending.Count-1)])
    $badPending | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations.txt')
    Write-Summary 'NO_GO_PREFLIGHT' $badPending; Assert-Refused 'repository history minus source history'
    $pending | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations.txt')

    Clear-Content -LiteralPath (Join-Path $root 'orphan-history.txt')
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'source history minus repository history'
    $orphanId | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'orphan-history.txt')

    $reviewed = Get-Content -Raw (Join-Path $root 'reviewed-git-state.json') | ConvertFrom-Json
    $reviewed.executedTree = 'd' * 40; $reviewed | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $root 'reviewed-git-state.json')
    Write-Summary 'NO_GO_PREFLIGHT' $pending; Assert-Refused 'exact reviewed and clean executed HEAD/tree'
    $reviewed.executedTree = $tree; $reviewed | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $root 'reviewed-git-state.json')

    'READY' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-readiness.txt')
    @('SOURCE_DATABASE=RhemaERP','TARGET_DATABASE=RHEMAERP_GL_REHEARSAL_FINAL_TEST','BACKUP_COPY_ONLY_CHECKSUM_START','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE','RESTORE_VERIFYONLY_CHECKSUM_COMPLETE','RESTORE_TARGET_COMPLETE','DBCC_CHECKDB_PHYSICAL_ONLY_COMPLETE') |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'backup-restore-checkdb.txt')
    (('C' * 64) + '  RHEMAERP_GL_REHEARSAL_FINAL_TEST_COPYONLY.bak') | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'backup.sha256')
    foreach ($file in @('clone-apply-migrations.log','seed-pass-1.log','seed-pass-2.log')) { 'PASS' | Set-Content -Encoding utf8 (Join-Path $root $file) }
    $targetIds = @(@($sourceIds) + @($pending) | Sort-Object -Unique)
    $targetIds | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'target-migration-history.txt')
    @('ACCOUNT|1|1000|Cash','ACCOUNT_SEGMENT_VALUE|1|1000|00','PRODUCER_INTENT_GROUP|1|Approved') | Set-Content -Encoding utf8 (Join-Path $root 'invariants-pass-1.txt')
    Copy-Item (Join-Path $root 'invariants-pass-1.txt') (Join-Path $root 'invariants-pass-2.txt')
    $invariantHash=(Get-FileHash -Algorithm SHA256 (Join-Path $root 'invariants-pass-1.txt')).Hash
    @("$invariantHash  invariants-pass-1.txt","$invariantHash  invariants-pass-2.txt") | Set-Content -Encoding ascii (Join-Path $root 'checksums.sha256')
    Write-Summary 'PASS' $pending
    & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Valid final 456/C8 PASS package was rejected.' }

    $badTarget=@($targetIds); $badTarget[0]='00000000000000_SameCountMutation'; $badTarget | Sort-Object | Set-Content -Encoding ascii (Join-Path $root 'target-migration-history.txt')
    Write-Summary 'PASS' $pending; Assert-Refused 'exactly source history union the ordered pending delta'
    $targetIds | Set-Content -Encoding ascii (Join-Path $root 'target-migration-history.txt')

    $markers=Get-Content (Join-Path $root 'backup-restore-checkdb.txt'); $markers[4]='RESTORE_TARGET_COMPLETE'; $markers | Set-Content -Encoding utf8 (Join-Path $root 'backup-restore-checkdb.txt')
    Write-Summary 'PASS' $pending; Assert-Refused 'identity-inconsistent'
    $markers[4]='RESTORE_VERIFYONLY_CHECKSUM_COMPLETE'; $markers | Set-Content -Encoding utf8 (Join-Path $root 'backup-restore-checkdb.txt')

    Write-Summary 'PASS' $pending
    Add-Content -Encoding utf8 -LiteralPath (Join-Path $root 'backup-restore-checkdb.txt') -Value ' '
    Assert-Refused 'backup/restore/DBCC evidence hash disagrees'
    $markers | Set-Content -Encoding utf8 (Join-Path $root 'backup-restore-checkdb.txt')

    @('ACCOUNT|1|1001|Cash','ACCOUNT_SEGMENT_VALUE|1|1000|00','PRODUCER_INTENT_GROUP|1|Approved') | Set-Content -Encoding utf8 (Join-Path $root 'invariants-pass-2.txt')
    Write-Summary 'PASS' $pending; Assert-Refused 'two-pass invariant hashes are not identical'
    Copy-Item (Join-Path $root 'invariants-pass-1.txt') (Join-Path $root 'invariants-pass-2.txt') -Force

    'SELECT 1; -- Server=secret-host' | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sql')
    $changed=(Get-FileHash -Algorithm SHA256 (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    "$changed  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sha256')
    Write-Summary 'PASS' $pending; Assert-Refused 'forbidden machine/connection marker'

    '-- synthetic exact pending-range idempotent SQL' | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sql')
    $idempotentHash=(Get-FileHash -Algorithm SHA256 (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii (Join-Path $root 'pending-migrations-idempotent.sha256')
    'BLOCKER historical FX evidence' | Set-Content -Encoding utf8 (Join-Path $root 'source-readiness.txt')
    Write-Summary 'NO_GO_PREFLIGHT' $pending
    'tampered after summary' | Set-Content -Encoding utf8 (Join-Path $root 'clone-build.log')
    Assert-Refused 'artifact hash binding is missing or invalid'
    'offline validated' | Set-Content -Encoding utf8 (Join-Path $root 'clone-build.log')
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
