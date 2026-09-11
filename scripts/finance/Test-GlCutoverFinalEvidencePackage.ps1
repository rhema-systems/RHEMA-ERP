Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Test-GlCutoverEvidencePackage.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_GL_FINAL_PACKAGE_$([guid]::NewGuid().ToString('N'))"

function Write-Manifest([string]$directory) {
    $manifest = Join-Path $directory 'manifest.sha256'
    Get-ChildItem -LiteralPath $directory -File -Recurse |
        Where-Object FullName -ne $manifest |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($directory, $_.FullName).Replace('\', '/')
            "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)  $relative"
        } | Set-Content -Encoding ascii -LiteralPath $manifest
}

function Assert-Refused([string]$expected) {
    Write-Manifest $root
    $output = & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "Invalid evidence unexpectedly passed: $expected" }
    if ($output -notmatch [regex]::Escape($expected)) {
        throw "Invalid evidence did not return expected refusal '$expected'. Output: $output"
    }
}

try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $migrationIds = @(
        1..455 | ForEach-Object { '{0:D14}_SyntheticMigration{1:D3}' -f $_,$_ }
    ) + '20260908120000_AddProducerIntentGroupsC8'
    $migrationIds | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'migration-discovery.log')
    $migrationIds[0..442] | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'source-migration-history.txt')
    $migrationIds[443..455] | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations.txt')
    @('20260817030000_AddFixedAssetLocationMasterLinks') | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'orphan-history.txt')
    foreach ($file in @('git-diff-check.log','commit-ancestry.txt','git-head-tree.txt','clone-build.log','ef-no-pending-model.log')) {
        'offline validated' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root $file)
    }
    'BLOCKER historical FX evidence' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-readiness.txt')
    'offline generation passed' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'idempotent-script-generation.log')
    '-- synthetic exact pending-range idempotent SQL' | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')
    $idempotentHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    "$idempotentHash  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sha256')
    '456|20260908120000_AddProducerIntentGroupsC8|1|9|28' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    '456|20260908120000_AddProducerIntentGroupsC8|1|9|28' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')
    [ordered]@{
        accountingEvents=$false; producerIntents=$false; producerIntentGroups=$false
        source='explicit process environment variables'
    } | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'feature-flags.json')
    $summary = [ordered]@{
        status='NO_GO_PREFLIGHT'; gitHead=('a' * 40); gitTree=('b' * 40)
        sourceDatabase='RhemaERP'; targetDatabase='RHEMAERP_GL_REHEARSAL_FINAL_TEST'
        sourceServer='<REDACTED_SAME_SERVER>'; targetServer='<REDACTED_SAME_SERVER>'; sameServer=$true
        repositoryMigrationCount=456; latestMigration='20260908120000_AddProducerIntentGroupsC8'
        pendingMigrationCount=13; pendingMigrations=@($migrationIds[443..455])
        sourceFingerprint='456|20260908120000_AddProducerIntentGroupsC8|1|9|28'
        cutoverFlagsExplicitlyFalse=$true; targetCreated=$false; backupCreated=$false
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'summary.json')

    & pwsh -NoProfile -File $validator -PackageKind FinalClone -EvidenceDirectory $root -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Valid final 456/C8 preflight NO-GO package was rejected.' }
    Write-Host 'PASS: final 456/C8 preflight NO-GO evidence package'

    $flagsPath = Join-Path $root 'feature-flags.json'
    $validFlags = Get-Content -Raw -LiteralPath $flagsPath
    ($validFlags | ConvertFrom-Json) | ForEach-Object { $_.producerIntentGroups=$true; $_ } |
        ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath $flagsPath
    Assert-Refused 'does not prove C6, C7 and C8 explicitly false'
    $validFlags | Set-Content -Encoding utf8 -LiteralPath $flagsPath

    $summaryPath = Join-Path $root 'summary.json'
    $validSummary = Get-Content -Raw -LiteralPath $summaryPath
    $changedSummary = $validSummary | ConvertFrom-Json
    $changedSummary.targetCreated = $true
    $changedSummary | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 -LiteralPath $summaryPath
    Assert-Refused 'must prove that no target or backup was created'
    $validSummary | Set-Content -Encoding utf8 -LiteralPath $summaryPath

    'readiness clear' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'source-readiness.txt')
    Assert-Refused 'does not contain the blocker or review-required evidence'
    Write-Host 'PASS: unsafe flags, target-created claim and missing blocker/review evidence are refused'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
