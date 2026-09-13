[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EvidenceDirectory,
    [ValidateSet('StageA1', 'FinalClone')]
    [string]$PackageKind = 'StageA1',
    [switch]$WriteManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$required = if ($PackageKind -eq 'FinalClone') {
    $summary = Get-Content -Raw -LiteralPath (Join-Path $root 'summary.json') | ConvertFrom-Json
    if ($summary.status -notin @('PASS', 'NO_GO_PREFLIGHT', 'NO_GO')) {
        throw "Unsupported final-clone status '$($summary.status)'."
    }
    $common = @(
        'summary.json',
        'reviewed-git-state.json',
        'feature-flags.json',
        'git-diff-check.log',
        'commit-ancestry.txt',
        'git-head-tree.txt',
        'clone-build.log',
        'ef-no-pending-model.log',
        'migration-discovery.log',
        'source-migration-history.txt',
        'pending-migrations.txt',
        'orphan-history.txt',
        'source-readiness.txt',
        'idempotent-script-generation.log',
        'pending-migrations-idempotent.sql',
        'pending-migrations-idempotent.sha256',
        'source-fingerprint-before.txt',
        'source-fingerprint-after.txt'
    )
    if ($summary.status -eq 'PASS') {
        $common += @(
            'backup-restore-checkdb.txt',
            'backup.sha256',
            'clone-apply-migrations.log',
            'target-migration-history.txt',
            'seed-pass-1.log',
            'seed-pass-2.log',
            'invariants-pass-1.txt',
            'invariants-pass-2.txt',
            'checksums.sha256'
        )
    }
    $common
}
else {
    @(
        'commit-ancestry.txt',
        'tree-equivalence.txt',
        'empty-a5\summary.json',
        'empty-a5\migration-apply.log',
        'empty-a5\migration-discovery.log',
        'empty-a5\ef-no-pending-model.log',
        'empty-a5\invariants-pass-1.txt',
        'empty-a5\invariants-pass-2.txt',
        'empty-a5\checksums.sha256',
        'empty-a5\cleanup.json',
        'clone-a2\summary.json',
        'clone-a2\source-fingerprint-before.txt',
        'clone-a2\source-fingerprint-after.txt',
        'clone-a2\backup-restore-checkdb.txt',
        'clone-a2\clone-build.log',
        'clone-a2\clone-apply-migrations.log',
        'clone-a2\clone-state-at-stop.txt',
        'clone-a2\cleanup.json',
        'sql-full-chain\test-result.log'
    )
}

foreach ($relative in $required) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required rehearsal evidence is missing: $relative"
    }
}

if ($PackageKind -eq 'FinalClone') {
    if (-not [string]::Equals([string]$summary.sourceDatabase, 'RhemaERP', [StringComparison]::OrdinalIgnoreCase) -or
        [string]$summary.targetDatabase -notmatch '^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$' -or
        $summary.sameServer -ne $true -or $summary.sourceServer -ne '<REDACTED_SAME_SERVER>' -or
        $summary.targetServer -ne '<REDACTED_SAME_SERVER>') {
        throw 'Final-clone summary does not bind exact RhemaERP source and prefix-safe target identities.'
    }
    $reviewedState = Get-Content -Raw -LiteralPath (Join-Path $root 'reviewed-git-state.json') | ConvertFrom-Json
    $gitHeadTree = @(Get-Content -LiteralPath (Join-Path $root 'git-head-tree.txt') |
        ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($gitHeadTree.Count -ne 2 -or $gitHeadTree[0] -notmatch '^[0-9a-fA-F]{40}$' -or
        $gitHeadTree[1] -notmatch '^[0-9a-fA-F]{40}$' -or
        $reviewedState.repositoryClean -ne $true -or $summary.repositoryClean -ne $true -or
        $reviewedState.reviewedCommit -ne $reviewedState.executedCommit -or
        $reviewedState.reviewedTree -ne $reviewedState.executedTree -or
        $gitHeadTree[0] -ne $reviewedState.executedCommit -or $gitHeadTree[1] -ne $reviewedState.executedTree -or
        [string]$summary.gitHead -ne $reviewedState.executedCommit -or [string]$summary.gitTree -ne $reviewedState.executedTree -or
        [string]$summary.reviewedCommit -ne $reviewedState.reviewedCommit -or [string]$summary.reviewedTree -ne $reviewedState.reviewedTree) {
        throw 'Final-clone evidence does not bind one exact reviewed and clean executed HEAD/tree.'
    }
    $ancestryFirst = (Get-Content -LiteralPath (Join-Path $root 'commit-ancestry.txt') | Select-Object -First 1).Trim()
    if ($ancestryFirst -notmatch ('^' + [regex]::Escape($reviewedState.executedCommit) + '(\s|$)')) {
        throw 'Final-clone ancestry evidence does not start at the exact executed commit.'
    }
    $flags = Get-Content -Raw -LiteralPath (Join-Path $root 'feature-flags.json') | ConvertFrom-Json
    if ($flags.accountingEvents -ne $false -or $flags.producerIntents -ne $false -or
        $flags.producerIntentGroups -ne $false -or
        $flags.source -ne 'explicit process environment variables') {
        throw 'Final-clone evidence does not prove C6, C7 and C8 explicitly false.'
    }
    $migrationIds = @(
        Get-Content -LiteralPath (Join-Path $root 'migration-discovery.log') | ForEach-Object {
            if ($_.Trim() -match '^(?<id>\d{14}_[^\s]+)') { $Matches.id }
        }
    )
    if ($migrationIds.Count -ne 456 -or $migrationIds[-1] -ne '20260908120000_AddProducerIntentGroupsC8') {
        throw "Final-clone migration evidence is not authoritative 456/C8. Count=$($migrationIds.Count); Latest=$($migrationIds[-1])."
    }
    if (@($migrationIds | Sort-Object -Unique).Count -ne 456 -or
        (@($migrationIds | Sort-Object) -join "`n") -ne ($migrationIds -join "`n")) {
        throw 'Final-clone migration evidence contains duplicate or out-of-order migration IDs.'
    }
    $sourceHistory = @(Get-Content -LiteralPath (Join-Path $root 'source-migration-history.txt') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if (@($sourceHistory | Sort-Object -Unique).Count -ne $sourceHistory.Count -or
        (@($sourceHistory | Sort-Object) -join "`n") -ne ($sourceHistory -join "`n")) {
        throw 'Final-clone source history contains duplicate or out-of-order migration IDs.'
    }
    $pending = @(Get-Content -LiteralPath (Join-Path $root 'pending-migrations.txt') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $orphan = @(Get-Content -LiteralPath (Join-Path $root 'orphan-history.txt') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $derivedPending = @($migrationIds | Where-Object { $_ -notin $sourceHistory })
    $derivedOrphan = @($sourceHistory | Where-Object { $_ -notin $migrationIds })
    if (($pending -join "`n") -ne ($derivedPending -join "`n")) {
        throw 'Final-clone pending migrations do not equal repository history minus source history.'
    }
    if (($orphan -join "`n") -ne ($derivedOrphan -join "`n")) {
        throw 'Final-clone orphan history does not equal source history minus repository history.'
    }
    if ($pending.Count -ne [int]$summary.pendingMigrationCount -or
        (@($summary.pendingMigrations) -join "`n") -ne ($pending -join "`n")) {
        throw 'Final-clone pending-migration evidence does not match summary.json.'
    }
    $before = (Get-Content -Raw -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')).Trim()
    $after = (Get-Content -Raw -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')).Trim()
    if ($before -ne $after -or $after -ne [string]$summary.sourceFingerprint) {
        throw 'Final-clone source fingerprint is not stable across the rehearsal.'
    }
    $idempotentScriptHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sql')).Hash
    $idempotentHashLine = (Get-Content -Raw -LiteralPath (Join-Path $root 'pending-migrations-idempotent.sha256')).Trim()
    if ($idempotentHashLine -ne "$idempotentScriptHash  pending-migrations-idempotent.sql") {
        throw 'Final-clone pending-migration idempotent SQL checksum is invalid.'
    }
    if ($summary.status -eq 'NO_GO_PREFLIGHT' -and ($summary.targetCreated -ne $false -or $summary.backupCreated -ne $false)) {
        throw 'Preflight NO-GO evidence must prove that no target or backup was created.'
    }
    if ($summary.status -eq 'NO_GO_PREFLIGHT' -and
        (Get-Content -Raw -LiteralPath (Join-Path $root 'source-readiness.txt')) -notmatch '(?im)\b(BLOCKER|REVIEW)\b') {
        throw 'Preflight NO-GO package does not contain the blocker or review-required evidence that caused the refusal.'
    }
    if ($summary.status -eq 'PASS') {
        $first = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'invariants-pass-1.txt')).Hash
        $second = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'invariants-pass-2.txt')).Hash
        if ($first -ne $second -or $first -ne [string]$summary.invariantPass1Sha256 -or
            $second -ne [string]$summary.invariantPass2Sha256) {
            throw 'Final-clone two-pass invariant hashes are not identical to summary evidence.'
        }
        $checksumLines = @(Get-Content -LiteralPath (Join-Path $root 'checksums.sha256'))
        if (($checksumLines -join "`n") -ne (@("$first  invariants-pass-1.txt", "$second  invariants-pass-2.txt") -join "`n")) {
            throw 'Final-clone invariant checksum file is inconsistent with the canonical snapshots.'
        }
        $backupLine = (Get-Content -Raw -LiteralPath (Join-Path $root 'backup.sha256')).Trim()
        if ($backupLine -notmatch '^(?<hash>[0-9A-F]{64})  RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}_COPYONLY\.bak$' -or
            $Matches.hash -ne [string]$summary.backupSha256) {
            throw 'Final-clone backup SHA-256 evidence is malformed or disagrees with summary.json.'
        }
        if ([string]$summary.pendingMigrationScriptSha256 -ne $idempotentScriptHash) {
            throw 'Final-clone idempotent SQL checksum disagrees with summary.json.'
        }
        $targetHistory = @(Get-Content -LiteralPath (Join-Path $root 'target-migration-history.txt') |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $expectedTargetHistory = @(@($sourceHistory) + @($derivedPending) | Sort-Object -Unique)
        if (@($targetHistory | Sort-Object -Unique).Count -ne $targetHistory.Count -or
            ($targetHistory -join "`n") -ne ($expectedTargetHistory -join "`n")) {
            throw 'PASS target history is not exactly source history union the ordered pending delta.'
        }
        $targetHistoryHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'target-migration-history.txt')).Hash
        if ($targetHistoryHash -ne [string]$summary.targetMigrationHistorySha256) {
            throw 'PASS target migration-history hash disagrees with summary.json.'
        }
        $backupEvidencePath = Join-Path $root 'backup-restore-checkdb.txt'
        $backupEvidenceLines = @(Get-Content -LiteralPath $backupEvidencePath | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        $requiredMarkers = @(
            'SOURCE_DATABASE=RhemaERP',
            "TARGET_DATABASE=$($summary.targetDatabase)",
            "BACKUP_MEDIA_ID=$($summary.backupMediaId)",
            'BACKUP_PATH_ATOMICALLY_RESERVED',
            'BACKUP_COPY_ONLY_CHECKSUM_START',
            'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE',
            'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE',
            'RESTORE_TARGET_COMPLETE',
            'DBCC_CHECKDB_PHYSICAL_ONLY_COMPLETE'
        )
        if ([string]$summary.backupMediaId -notmatch '^[0-9a-f]{32}$' -or
            [string]$summary.backupReservation -ne 'FILEMODE_CREATE_NEW') {
            throw 'PASS backup media identity is missing or malformed.'
        }
        $priorMarkerIndex = -1
        foreach ($marker in $requiredMarkers) {
            $matching = @($backupEvidenceLines | Where-Object { $_ -eq $marker })
            $markerIndex = [Array]::IndexOf($backupEvidenceLines, $marker)
            if ($matching.Count -ne 1 -or $markerIndex -le $priorMarkerIndex) {
                throw "PASS backup/VERIFYONLY/restore/DBCC evidence marker is missing, duplicated, out of order, or identity-inconsistent: $marker"
            }
            $priorMarkerIndex = $markerIndex
        }
        $backupEvidenceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupEvidencePath).Hash
        if ($backupEvidenceHash -ne [string]$summary.backupRestoreEvidenceSha256) {
            throw 'PASS backup/restore/DBCC evidence hash disagrees with summary.json.'
        }
    }

    if ($null -eq $summary.artifactSha256) {
        throw 'Final-clone summary is missing independently checkable artifact SHA-256 bindings.'
    }
    foreach ($relative in $required | Where-Object { $_ -ne 'summary.json' }) {
        $property = $summary.artifactSha256.PSObject.Properties[$relative]
        $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $relative)).Hash
        if ($null -eq $property -or [string]$property.Value -ne $actualHash) {
            throw "Final-clone artifact hash binding is missing or invalid: $relative"
        }
    }
}

$textFiles = Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object Extension -in @('.json', '.txt', '.log', '.sha256', '.sql')
$forbidden = @(
    '(?i)(Password|Pwd|User ID|UID|Data Source|Server|Integrated Security|Trusted_Connection)\s*=\s*(?!<REDACTED>)[^;\r\n]+',
    '(?i)\b[A-Z]:\\+',
    '(?i)RHEMA-AKWASI',
    '(?i)ClientConnectionId:[0-9a-f-]{36}'
)
foreach ($file in $textFiles) {
    $text = Get-Content -Raw -LiteralPath $file.FullName
    foreach ($pattern in $forbidden) {
        if ($text -match $pattern) {
            throw "Evidence contains a forbidden machine/connection marker: $($file.FullName)"
        }
    }
}

$manifestPath = Join-Path $root 'manifest.sha256'
if ($WriteManifest) {
    Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object FullName -ne $manifestPath |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
            "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)  $relative"
        } | Set-Content -Encoding ascii -LiteralPath $manifestPath
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'Evidence manifest is missing. Run with -WriteManifest after assembling the package.'
}

$manifestLines = @(Get-Content -LiteralPath $manifestPath)
$manifestRelativePaths = @()
foreach ($line in $manifestLines) {
    if ($line -notmatch '^(?<hash>[0-9A-F]{64})  (?<path>.+)$') {
        throw "Malformed evidence manifest line: $line"
    }
    $relativePath = $Matches.path.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    if ([System.IO.Path]::IsPathRooted($relativePath) -or $relativePath -split '[\\/]' -contains '..') {
        throw "Manifest path escapes the evidence package: $($Matches.path)"
    }
    $path = [System.IO.Path]::GetFullPath((Join-Path $root $relativePath))
    if (-not $path.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path escapes the evidence package: $($Matches.path)"
    }
    $manifestRelativePaths += $Matches.path.Replace('\','/')
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Manifest target is missing: $($Matches.path)"
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
    if ($actual -ne $Matches.hash) {
        throw "Evidence hash mismatch: $($Matches.path)"
    }
}
$expectedManifestPaths = @(Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object FullName -ne $manifestPath | ForEach-Object {
        [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\','/')
    } | Sort-Object)
if (@($manifestRelativePaths | Sort-Object -Unique).Count -ne $manifestRelativePaths.Count -or
    (($manifestRelativePaths | Sort-Object) -join "`n") -ne ($expectedManifestPaths -join "`n")) {
    throw 'Evidence manifest must list every package file exactly once and no external path.'
}

Write-Host "GL cutover evidence package is complete, sanitized and hash-valid: $root"
