[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EvidenceDirectory,
    [ValidateSet('StageA1', 'FinalClone', 'DisposableReset')]
    [string]$PackageKind = 'StageA1',
    [switch]$WriteManifest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$migrationHistoryEvidenceSchema = 'RHEMA_MIGRATION_HISTORY_V1'

function Get-RequiredNonNullJsonProperty($value, [string]$name, [string]$context) {
    $property = $value.PSObject.Properties[$name]
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "$context requires non-null property '$name'."
    }
    return $property.Value
}

function Test-DisposableSourceFingerprintShape([string]$fingerprint) {
    if ([string]::IsNullOrWhiteSpace($fingerprint) -or
        $fingerprint -cnotmatch '^(?<count>\d+)\|(?<latest>EMPTY|\d{14}_[A-Za-z0-9_]+)\|\d+\|\d+\|\d+$') {
        return $false
    }
    $migrationCount = [uint64]$Matches.count
    return ($migrationCount -eq 0 -and $Matches.latest -ceq 'EMPTY') -or
        ($migrationCount -gt 0 -and $Matches.latest -cne 'EMPTY')
}

function Get-SqlEvidenceTokens([string]$evidenceFile) {
    if (-not (Test-Path -LiteralPath $evidenceFile -PathType Leaf)) { return @() }
    $raw = Get-Content -Raw -LiteralPath $evidenceFile
    return @([regex]::Matches($raw, '\S+') | ForEach-Object { $_.Value })
}

function Assert-UniqueOrderedSqlEvidenceTokens([string]$evidenceFile, [string[]]$expectedTokens,
    [string]$context) {
    $actualTokens = @(Get-SqlEvidenceTokens $evidenceFile)
    $priorIndex = -1
    foreach ($expectedToken in $expectedTokens) {
        if ([string]::IsNullOrWhiteSpace($expectedToken) -or $expectedToken -match '\s') {
            throw "Internal $context evidence-token contract is malformed."
        }
        $matchingIndexes = @(for ($index = 0; $index -lt $actualTokens.Count; $index++) {
            if ([string]::Equals($actualTokens[$index], $expectedToken, [StringComparison]::Ordinal)) { $index }
        })
        if ($matchingIndexes.Count -ne 1 -or $matchingIndexes[0] -le $priorIndex) {
            throw "$context evidence lacks a unique ordered token: $expectedToken"
        }
        $priorIndex = $matchingIndexes[0]
    }
}

function Test-NativeFailureEvidenceMarker([string]$line, [string]$command) {
    $pattern = '^RHEMA_NATIVE_COMMAND_EVIDENCE_V1\|STATUS=FAILURE\|EXIT_CODE=(?<exit>-?(?:0|[1-9]\d*))\|COMMAND=' +
        [regex]::Escape($command) + '$'
    if ($line -cnotmatch $pattern) { return $false }
    $exitCode = 0
    if (-not [int]::TryParse([string]$Matches.exit, [ref]$exitCode)) { return $false }
    return $exitCode -ne 0
}

function Test-NativeSuccessEvidenceMarker([string]$line, [string]$command) {
    return $line -ceq "RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=$command"
}

function Assert-DisposableRequiredArtifactBinding($reset, [string]$root, [string]$name,
    [string]$context) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$context requires retained artifact: $name"
    }
    $artifactMap = $reset.PSObject.Properties['artifactSha256']
    $binding = if ($null -eq $artifactMap -or $null -eq $artifactMap.Value) {
        $null
    } else {
        $artifactMap.Value.PSObject.Properties[$name]
    }
    if ($null -eq $binding -or $null -eq $binding.Value -or
        [string]$binding.Value -cne (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash) {
        throw "$context requires the exact artifact hash binding: $name"
    }
}

function Read-MigrationHistoryEvidence([string]$path, [string]$context, [bool]$allowReviewedLegacy = $false) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "$context evidence is missing." }
    $raw = [System.IO.File]::ReadAllText($path, [System.Text.UTF8Encoding]::new($false))
    if ($allowReviewedLegacy) {
        $legacyIds = @($raw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if (@($legacyIds | Where-Object { $_ -cnotmatch '^\d{14}_[A-Za-z0-9_]+$' }).Count -ne 0 -or
            @($legacyIds | Sort-Object -Unique).Count -ne $legacyIds.Count -or
            (@($legacyIds | Sort-Object) -join "`n") -cne ($legacyIds -join "`n")) {
            throw "$context reviewed legacy evidence is malformed."
        }
        return [pscustomobject]@{ schema='REVIEWED_LEGACY_RAW'; count=[long]$legacyIds.Count; ids=[string[]]$legacyIds }
    }
    if ([string]::IsNullOrEmpty($raw) -or -not $raw.EndsWith("`n", [StringComparison]::Ordinal)) {
        throw "$context evidence is empty or lacks its deterministic terminal newline."
    }
    $normalized = $raw.Replace("`r`n", "`n")
    if ($normalized.Contains("`r", [StringComparison]::Ordinal)) { throw "$context evidence has a noncanonical line ending." }
    $lines = @($normalized.Substring(0, $normalized.Length - 1).Split("`n"))
    if ($lines[0] -cnotmatch '^RHEMA_MIGRATION_HISTORY_V1\|COUNT=(?<count>0|[1-9]\d*)\|STATE=(?<state>EMPTY|POPULATED)$') {
        throw "$context evidence has a missing or malformed migration-history marker."
    }
    $count = [long]$Matches.count
    $state = [string]$Matches.state
    $ids = @($lines | Select-Object -Skip 1)
    if ($ids.Count -ne $count -or ($count -eq 0 -and $state -cne 'EMPTY') -or
        ($count -gt 0 -and $state -cne 'POPULATED') -or
        @($ids | Where-Object { $_ -cnotmatch '^\d{14}_[A-Za-z0-9_]+$' }).Count -ne 0 -or
        @($ids | Sort-Object -Unique).Count -ne $ids.Count -or
        (@($ids | Sort-Object) -join "`n") -cne ($ids -join "`n")) {
        throw "$context evidence count, state, or exact ordered IDs are inconsistent."
    }
    [pscustomobject]@{ schema=$migrationHistoryEvidenceSchema; count=$count; ids=[string[]]$ids }
}

$root = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$required = if ($PackageKind -eq 'DisposableReset') {
    @('reset-status.json','RECOVERY.md','reviewed-git-state.json','feature-flags.json')
}
elseif ($PackageKind -eq 'FinalClone') {
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

if ($PackageKind -eq 'DisposableReset') {
    $reset = Get-Content -Raw -LiteralPath (Join-Path $root 'reset-status.json') | ConvertFrom-Json
    if ($reset.mode -cne 'ResetDisposableDevelopment' -or $reset.database -cne 'RhemaERP' -or
        $reset.server -cne '<REDACTED_LOCAL_SERVER>' -or $reset.repositoryClean -ne $true -or
        $reset.automaticRetry -ne $false -or $reset.automaticCleanup -ne $false -or
        $reset.status -notin @('FAILED_NO_AUTOMATIC_RETRY','PASS')) {
        throw 'Disposable-reset status identity, safety flags, or terminal status is invalid.'
    }
    $reviewedState = Get-Content -Raw -LiteralPath (Join-Path $root 'reviewed-git-state.json') | ConvertFrom-Json
    if ($reviewedState.repositoryClean -ne $true -or
        [string]$reviewedState.reviewedCommit -notmatch '^[0-9a-f]{40}$' -or
        [string]$reviewedState.reviewedTree -notmatch '^[0-9a-f]{40}$' -or
        $reviewedState.reviewedCommit -cne $reviewedState.executedCommit -or
        $reviewedState.reviewedTree -cne $reviewedState.executedTree -or
        [string]$reset.reviewedCommit -cne [string]$reviewedState.reviewedCommit -or
        [string]$reset.reviewedTree -cne [string]$reviewedState.reviewedTree) {
        throw 'Disposable-reset evidence does not bind one exact reviewed clean HEAD/tree.'
    }
    $historySchemaProperty = $reset.PSObject.Properties['migrationHistoryEvidenceSchema']
    $isReviewedAttempt03Or04EarlyPackage =
        [string]$reset.reviewedCommit -ceq '2de5f800c47e58fe62ca1715db0ac4aef5001327' -and
        [string]$reset.reviewedTree -ceq '8a2f30bead3500138657c85a8447765330500515' -and
        [string]$reset.status -ceq 'FAILED_NO_AUTOMATIC_RETRY' -and
        [string]$reset.phase -ceq 'OFFLINE_GATES_COMPLETE' -and
        -not (Test-Path -LiteralPath (Join-Path $root 'source-migration-history.txt') -PathType Leaf)
    $isPotentialReviewed22Legacy =
        [string]$reset.reviewedCommit -ceq '22b27ab18a19a92fa6b1222c05add817574e74fe' -and
        [string]$reset.reviewedTree -ceq '13f1eb03a24af14ff23998de72e3ddac9992981d'
    $allowReviewedLegacyMigrationHistory = $null -eq $historySchemaProperty -and
        ($isReviewedAttempt03Or04EarlyPackage -or $isPotentialReviewed22Legacy)
    if (-not $allowReviewedLegacyMigrationHistory) {
        if ($null -eq $historySchemaProperty -or $null -eq $historySchemaProperty.Value) {
            throw "Disposable-reset status requires non-null property 'migrationHistoryEvidenceSchema'."
        }
        if ($historySchemaProperty.Value -isnot [string] -or
            $historySchemaProperty.Value -cne $migrationHistoryEvidenceSchema) {
            throw 'Disposable-reset migration-history evidence schema must be the exact scalar JSON String contract.'
        }
    }
    if (Test-Path -LiteralPath (Join-Path $root 'git-head-tree.txt') -PathType Leaf) {
        $headTree = @(Get-Content -LiteralPath (Join-Path $root 'git-head-tree.txt') |
            Where-Object { $_ -notlike 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|*' })
        if ($headTree.Count -ne 2 -or $headTree[0] -cne [string]$reviewedState.executedCommit -or
            $headTree[1] -cne [string]$reviewedState.executedTree) {
            throw 'Disposable-reset git transport evidence disagrees with reviewed HEAD/tree.'
        }
    }
    if (Test-Path -LiteralPath (Join-Path $root 'pre-mutation-reviewed-git-state.json') -PathType Leaf) {
        $preMutationState = Get-Content -Raw -LiteralPath (Join-Path $root 'pre-mutation-reviewed-git-state.json') | ConvertFrom-Json
        if ($preMutationState.repositoryClean -ne $true -or
            [string]$preMutationState.executedCommit -cne [string]$reviewedState.executedCommit -or
            [string]$preMutationState.executedTree -cne [string]$reviewedState.executedTree) {
            throw 'Disposable-reset pre-mutation reviewed state disagrees with initial reviewed HEAD/tree.'
        }
    }
    $flags = Get-Content -Raw -LiteralPath (Join-Path $root 'feature-flags.json') | ConvertFrom-Json
    if ($flags.accountingEvents -ne $false -or $flags.producerIntents -ne $false -or
        $flags.producerIntentGroups -ne $false -or $flags.source -cne 'explicit process environment variables') {
        throw 'Disposable-reset evidence does not prove C6, C7 and C8 explicitly false.'
    }

    $phaseFiles = @(Get-ChildItem -LiteralPath $root -File -Filter 'phase-*.json' | Sort-Object Name)
    $phases = @()
    $phaseMarkers = @()
    for ($index = 0; $index -lt $phaseFiles.Count; $index++) {
        if ($phaseFiles[$index].Name -cne ("phase-{0:D2}.json" -f ($index + 1))) {
            throw 'Disposable-reset phase markers are not a contiguous monotonic sequence.'
        }
        $marker = Get-Content -Raw -LiteralPath $phaseFiles[$index].FullName | ConvertFrom-Json
        if ($marker.schema -cne 'RHEMA_DISPOSABLE_RESET_PHASE_V1' -or [int]$marker.ordinal -ne ($index + 1)) {
            throw 'Disposable-reset phase marker schema or ordinal is invalid.'
        }
        $phases += [string]$marker.phase
        $phaseMarkers += $marker
    }
    $expectedPhases = @('OFFLINE_GATES_COMPLETE','SOURCE_CAPTURE_COMPLETE','BACKUP_CREATED','BACKUP_VERIFIED',
        'RESET_STARTED','DATABASE_RECREATED','MIGRATIONS_APPLIED','SEED_INVARIANTS_VERIFIED','DBCC_COMPLETE','COMPLETE')
    for ($index = 0; $index -lt $phases.Count; $index++) {
        if ($index -ge $expectedPhases.Count -or $phases[$index] -cne $expectedPhases[$index]) {
            throw 'Disposable-reset phase markers contain an unknown or out-of-order phase.'
        }
    }
    $lastDurablePhase = if ($phases.Count -eq 0) { 'NOT_STARTED' } else { $phases[-1] }
    if ([string]$reset.phase -cne $lastDurablePhase) {
        throw 'Disposable-reset terminal status phase does not exactly equal the final durable phase marker.'
    }
    $recoveryLines = @(Get-Content -LiteralPath (Join-Path $root 'RECOVERY.md'))
    $recoveryDurable = @($recoveryLines | Where-Object { $_ -match '^Last durable phase: (?<value>[A-Z0-9_]+)$' })
    $recoveryOperation = @($recoveryLines | Where-Object { $_ -match '^Failed operation: (?<value>[A-Z0-9_]+)$' })
    $recoveryVerified = @($recoveryLines | Where-Object { $_ -match '^Verified backup available: (?:true|false)$' })
    if ($recoveryDurable.Count -ne 1 -or $recoveryOperation.Count -ne 1 -or $recoveryVerified.Count -ne 1) {
        throw 'Disposable-reset recovery evidence must contain one restricted durable-phase and failed-operation field.'
    }
    $recoveryDurableValue = ($recoveryDurable[0] -replace '^Last durable phase: ','')
    $recoveryOperationValue = ($recoveryOperation[0] -replace '^Failed operation: ','')
    $recoveryVerifiedValue = ($recoveryVerified[0] -replace '^Verified backup available: ','')
    if ($recoveryDurableValue -cne [string]$reset.phase) {
        throw 'Disposable-reset recovery last durable phase disagrees with terminal status/phase evidence.'
    }
    if ($reset.status -eq 'PASS') {
        if ($recoveryOperationValue -cne 'NOT_APPLICABLE') {
            throw 'Disposable-reset PASS recovery evidence must have no failed operation.'
        }
    }
    elseif ([string]$reset.failedOperation -notmatch '^[A-Z0-9_]+$' -or
        $recoveryOperationValue -cne [string]$reset.failedOperation) {
        throw 'Disposable-reset failure recovery operation disagrees with terminal status evidence.'
    }
    if ($recoveryVerifiedValue -cne ([bool]$reset.backupVerified).ToString().ToLowerInvariant()) {
        throw 'Disposable-reset recovery verified-backup field disagrees with terminal status evidence.'
    }
    $backupPhaseMarkerPublished = $phases -ccontains 'BACKUP_CREATED'
    if ($backupPhaseMarkerPublished -and $reset.backupCreated -ne $true) {
        throw 'Disposable-reset durable BACKUP_CREATED marker cannot be downgraded by terminal status.'
    }
    if (-not $backupPhaseMarkerPublished -and $reset.backupCreated -eq $true -and
        ($reset.status -ne 'FAILED_NO_AUTOMATIC_RETRY' -or $reset.backupPhaseMarkerPublished -ne $false -or
         $reset.backupMaterialStateReconciled -ne $true -or [long]$reset.backupByteLength -le 0)) {
        throw 'Disposable-reset material backup without phase-03 lacks truthful reconciliation evidence.'
    }
    if ([bool]$reset.backupPhaseMarkerPublished -ne $backupPhaseMarkerPublished) {
        throw 'Disposable-reset phase-03 publication status disagrees with the durable marker set.'
    }
    if ($phases -cnotcontains 'BACKUP_VERIFIED' -and $reset.backupVerified -eq $true) {
        throw 'Disposable-reset cannot claim verification without durable phase-04.'
    }
    if (($phases -ccontains 'RESET_STARTED') -ne [bool]$reset.resetStarted) {
        throw 'Disposable-reset RESET_STARTED marker and terminal status disagree.'
    }
    if ($reset.status -eq 'PASS' -and (($phases -join "`n") -cne ($expectedPhases -join "`n") -or
        $reset.backupCreated -ne $true -or $reset.backupVerified -ne $true -or $reset.resetStarted -ne $true)) {
        throw 'Disposable-reset PASS does not contain the complete monotonic phase sequence.'
    }

    $durableOrdinal = $phases.Count
    $failedOperation = if ($reset.status -ceq 'FAILED_NO_AUTOMATIC_RETRY') {
        [string]$reset.failedOperation
    } else { 'NOT_APPLICABLE' }
    $offlineCommandEvidence = @('git-diff-check.log','commit-ancestry.txt','git-head-tree.txt',
        'reset-build.log','ef-no-pending-model.log','migration-discovery.log')
    if ($durableOrdinal -ge 1 -and -not $allowReviewedLegacyMigrationHistory) {
        foreach ($name in $offlineCommandEvidence) {
            Assert-DisposableRequiredArtifactBinding $reset $root $name `
                'Disposable-reset OFFLINE_GATES_COMPLETE phase'
        }
        Assert-DisposableRequiredArtifactBinding $reset $root 'repository-migration-history.txt' `
            'Disposable-reset OFFLINE_GATES_COMPLETE phase'
    }
    $migrationOperationClaimed = $durableOrdinal -ge 7 -or $failedOperation -in @(
        'APPLY_MIGRATIONS','CAPTURE_TARGET_MIGRATION_HISTORY','PHASE_07_PUBLICATION')
    if ($migrationOperationClaimed) {
        Assert-DisposableRequiredArtifactBinding $reset $root 'reset-apply-migrations.log' `
            'Disposable-reset migration operation'
    }
    if ($durableOrdinal -ge 7 -or $failedOperation -ceq 'PHASE_07_PUBLICATION') {
        Assert-DisposableRequiredArtifactBinding $reset $root 'target-migration-history.txt' `
            'Disposable-reset validated target migration state'
    }
    if ($durableOrdinal -ge 8) {
        foreach ($name in @('reset-seed-pass-1.log','reset-seed-pass-2.log',
            'reset-invariants-pass-1.txt','reset-invariants-pass-2.txt','reset-invariants.sha256')) {
            Assert-DisposableRequiredArtifactBinding $reset $root $name `
                'Disposable-reset SEED_INVARIANTS_VERIFIED phase'
        }
    }
    if ($durableOrdinal -ge 9) {
        Assert-DisposableRequiredArtifactBinding $reset $root 'reset-dbcc.txt' `
            'Disposable-reset DBCC_COMPLETE phase'
    }

    $commandEvidence = [ordered]@{
        'git-diff-check.log'='git'; 'commit-ancestry.txt'='git'; 'git-head-tree.txt'='git';
        'reset-build.log'='dotnet'; 'ef-no-pending-model.log'='dotnet'; 'migration-discovery.log'='dotnet';
        'reset-apply-migrations.log'='dotnet'; 'reset-seed-pass-1.log'='dotnet'; 'reset-seed-pass-2.log'='dotnet'
    }
    foreach ($entry in $commandEvidence.GetEnumerator()) {
        $path = Join-Path $root $entry.Key
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $lines = @(Get-Content -LiteralPath $path)
        if ($lines.Count -eq 0) {
            throw "Disposable-reset native evidence is empty: $($entry.Key)"
        }
        $successMarker = Test-NativeSuccessEvidenceMarker ([string]$lines[-1]) ([string]$entry.Value)
        $failureMarker = Test-NativeFailureEvidenceMarker ([string]$lines[-1]) ([string]$entry.Value)
        $validTerminalMarker = $successMarker -or
            ($reset.status -eq 'FAILED_NO_AUTOMATIC_RETRY' -and
             $failureMarker)
        if ($entry.Key -ceq 'reset-apply-migrations.log' -and $migrationOperationClaimed) {
            if ($failedOperation -ceq 'APPLY_MIGRATIONS' -and -not $failureMarker) {
                throw 'Disposable-reset native APPLY_MIGRATIONS failure must bind its exact failed command evidence.'
            }
            if ($failedOperation -cne 'APPLY_MIGRATIONS' -and -not $successMarker) {
                throw 'Disposable-reset post-migration state requires exact successful apply-migrations command evidence.'
            }
        }
        if ($durableOrdinal -ge 1 -and $entry.Key -in $offlineCommandEvidence -and -not $successMarker) {
            throw "Disposable-reset completed offline gate requires exact successful command evidence: $($entry.Key)"
        }
        if ($durableOrdinal -ge 8 -and $entry.Key -in @('reset-seed-pass-1.log','reset-seed-pass-2.log') -and
            -not $successMarker) {
            throw "Disposable-reset completed seed phase requires exact successful command evidence: $($entry.Key)"
        }
        if (-not $validTerminalMarker -or
            @($lines | Where-Object { $_ -like 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|*' }).Count -ne 1) {
            throw "Disposable-reset native evidence is empty or marker-invalid: $($entry.Key)"
        }
    }

    $repositoryIds = @()
    $validatedTargetHistoryCount = [long]0
    $validatedTargetHistoryPresent = $false
    if (Test-Path -LiteralPath (Join-Path $root 'migration-discovery.log') -PathType Leaf) {
        $repositoryIds = @(Get-Content -LiteralPath (Join-Path $root 'migration-discovery.log') | ForEach-Object {
            if ($_.Trim() -match '^(?<id>\d{14}_[^\s]+)') { $Matches.id }
        })
        if ($repositoryIds.Count -ne 1 -or $repositoryIds[-1] -cne '20260916132000_DisposableDevelopmentCurrentModelBaseline' -or
            @($repositoryIds | Sort-Object -Unique).Count -ne 1 -or
            (@($repositoryIds | Sort-Object) -join "`n") -cne ($repositoryIds -join "`n")) {
            throw 'Disposable-reset repository history is not the exact authoritative disposable-development baseline.'
        }
        $repositoryHistoryPath = Join-Path $root 'repository-migration-history.txt'
        $repositoryHistory = Read-MigrationHistoryEvidence $repositoryHistoryPath `
            'Disposable-reset repository history' $allowReviewedLegacyMigrationHistory
        if ((@($repositoryHistory.ids) -join "`n") -cne ($repositoryIds -join "`n")) {
            throw 'Disposable-reset repository migration identity evidence is missing or inconsistent.'
        }
        if (Test-Path -LiteralPath (Join-Path $root 'target-migration-history.txt') -PathType Leaf) {
            $targetHistoryEvidence = Read-MigrationHistoryEvidence (Join-Path $root 'target-migration-history.txt') `
                'Disposable-reset target history' $allowReviewedLegacyMigrationHistory
            $targetIds = @($targetHistoryEvidence.ids)
            if (($targetIds -join "`n") -cne ($repositoryIds -join "`n") -or
                [long]$reset.finalMigrationCount -ne $targetIds.Count -or [long]$reset.orphanMigrationCount -ne 0) {
                throw 'Disposable-reset target history is not exactly the repository baseline with zero orphans.'
            }
            $validatedTargetHistoryPresent = $true
            $validatedTargetHistoryCount = [long]$targetIds.Count
        }
    }
    if ($failedOperation -ceq 'APPLY_MIGRATIONS' -and
        ($durableOrdinal -ne 6 -or $validatedTargetHistoryPresent -or [long]$reset.finalMigrationCount -ne 0)) {
        throw 'Native APPLY_MIGRATIONS failure must stop at DATABASE_RECREATED without validated target history.'
    }
    if ($failedOperation -ceq 'CAPTURE_TARGET_MIGRATION_HISTORY' -and
        ($durableOrdinal -ne 6 -or $validatedTargetHistoryPresent -or [long]$reset.finalMigrationCount -ne 0)) {
        throw 'Post-success migration-history capture failure must retain DATABASE_RECREATED and no validated target history.'
    }
    if ($failedOperation -ceq 'PHASE_07_PUBLICATION' -and
        ($durableOrdinal -ne 6 -or -not $validatedTargetHistoryPresent -or
         $validatedTargetHistoryCount -ne 1 -or [long]$reset.finalMigrationCount -ne 1)) {
        throw 'Phase-07 publication failure must bind the exact validated target migration history.'
    }
    $sourceIds = @()
    if (Test-Path -LiteralPath (Join-Path $root 'source-migration-history.txt') -PathType Leaf) {
        $sourceHistoryEvidence = Read-MigrationHistoryEvidence (Join-Path $root 'source-migration-history.txt') `
            'Disposable-reset source history' $allowReviewedLegacyMigrationHistory
        $sourceIds = @($sourceHistoryEvidence.ids)
        if (-not $allowReviewedLegacyMigrationHistory -and $phases -ccontains 'SOURCE_CAPTURE_COMPLETE') {
            $sourcePhaseIndex = [Array]::IndexOf($phases, 'SOURCE_CAPTURE_COMPLETE')
            $sourceHistoryHash = (Get-FileHash -Algorithm SHA256 -LiteralPath `
                (Join-Path $root 'source-migration-history.txt')).Hash
            if ($null -eq $phaseMarkers[$sourcePhaseIndex].PSObject.Properties['sourceHistorySha256'] -or
                [string]$phaseMarkers[$sourcePhaseIndex].sourceHistorySha256 -cne $sourceHistoryHash) {
                throw 'Disposable-reset SOURCE_CAPTURE phase does not bind the exact migration-history artifact hash.'
            }
        }
    }
    foreach ($identityFile in @('source-database-identity.sha256','source-server-identity.sha256')) {
        $identityPath = Join-Path $root $identityFile
        if (Test-Path -LiteralPath $identityPath -PathType Leaf) {
            $identityLine = (Get-Content -Raw -LiteralPath $identityPath).Trim()
            if ($identityLine -notmatch '^[A-Z_]+_SHA256=[0-9A-F]{64}$') {
                throw "Disposable-reset identity binding is malformed: $identityFile"
            }
        }
    }

    $approvedLegacyCommit = '22b27ab18a19a92fa6b1222c05add817574e74fe'
    $approvedLegacyTree = '13f1eb03a24af14ff23998de72e3ddac9992981d'
    $approvedLegacyMedia = 'cc918da6ac23465497e00ca210a4c2c6'
    $approvedLegacyHash = '7F07CD03EED8F178ED45208936C3CC8F6E9F8D1336FFB3BF371C076D8F343743'
    $approvedLegacyFingerprint = '446|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28'
    $identityVersionProperty = $reset.PSObject.Properties['backupIdentityVersion']
    $evidenceSchemaProperty = $reset.PSObject.Properties['evidenceSchema']
    $legacyTypesExact = $reset.backupCreated -is [bool] -and $reset.backupPreserved -is [bool] -and
        $reset.backupCompleted -is [bool] -and $reset.backupVerified -is [bool] -and $reset.resetStarted -is [bool] -and
        $reset.backupPhaseMarkerPublished -is [bool] -and $reset.backupHashMatchesVerified -is [bool] -and
        $reset.verifyEvidencePresent -is [bool] -and $reset.backupMaterialStateReconciled -is [bool] -and
        $reset.backupByteLength -is [long]
    $isApprovedLegacy = $legacyTypesExact -and $reset.reviewedCommit -ceq $approvedLegacyCommit -and $reset.reviewedTree -ceq $approvedLegacyTree -and
        $reset.status -ceq 'FAILED_NO_AUTOMATIC_RETRY' -and $reset.phase -ceq 'SOURCE_CAPTURE_COMPLETE' -and
        [string]$reset.backupMediaId -ceq $approvedLegacyMedia -and [long]$reset.backupByteLength -eq 453042176 -and
        [string]$reset.currentMaterialSha256 -ceq $approvedLegacyHash -and [string]$reset.backupSha256 -ceq '' -and
        $reset.backupCreated -eq $true -and $reset.backupPreserved -eq $true -and $reset.backupCompleted -eq $false -and
        $reset.backupVerified -eq $false -and $reset.resetStarted -eq $false -and
        $reset.backupPhaseMarkerPublished -eq $false -and $reset.backupHashMatchesVerified -eq $false -and
        $reset.verifyEvidencePresent -eq $false -and $reset.backupMaterialStateReconciled -eq $true -and
        $null -ne $reset.PSObject.Properties['sourceFingerprint'] -and $null -ne $reset.sourceFingerprint -and
        $reset.sourceFingerprint -is [string] -and $reset.sourceFingerprint -ceq $approvedLegacyFingerprint -and
        $null -eq $identityVersionProperty -and $null -eq $evidenceSchemaProperty -and
        $null -eq $reset.PSObject.Properties['backupFileName'] -and
        $null -eq $reset.PSObject.Properties['backupPathSha256'] -and
        $null -eq $reset.PSObject.Properties['attemptOwnedBackup']
    if (-not $isApprovedLegacy -and
        ($null -eq $identityVersionProperty -or [string]$identityVersionProperty.Value -cne 'MEDIA_BOUND_V1' -or
         $null -eq $evidenceSchemaProperty -or [string]$evidenceSchemaProperty.Value -cne 'RHEMA_DISPOSABLE_RESET_EVIDENCE_V2')) {
        throw 'Disposable-reset evidence schema/backup identity version is missing or unsupported.'
    }
    if (-not $isApprovedLegacy) {
        $v2BooleanFields = @('backupCreated','backupCompleted','backupPreserved','backupVerified','resetStarted',
            'backupPhaseMarkerPublished','backupHashMatchesVerified','verifyEvidencePresent','attemptOwnedBackup',
            'backupMaterialStateReconciled')
        foreach ($field in $v2BooleanFields) {
            $fieldValue = Get-RequiredNonNullJsonProperty $reset $field 'Disposable-reset V2 status'
            if ($fieldValue -isnot [bool]) { throw "Disposable-reset V2 property '$field' must be Boolean." }
        }
        $byteLengthValue = Get-RequiredNonNullJsonProperty $reset 'backupByteLength' 'Disposable-reset V2 status'
        if ($byteLengthValue -isnot [long] -or $byteLengthValue -lt 0) {
            throw 'Disposable-reset V2 backupByteLength must be a nonnegative Int64.'
        }
        foreach ($field in @('backupMediaId','backupFileName','backupPathSha256','currentMaterialSha256','backupSha256')) {
            if ((Get-RequiredNonNullJsonProperty $reset $field 'Disposable-reset V2 status') -isnot [string]) {
                throw "Disposable-reset V2 property '$field' must be String."
            }
        }
        if ($reset.backupMaterialStateReconciled -ne $true) {
            throw 'Disposable-reset V2 backup material state must be reconciled before terminal publication.'
        }
        foreach ($field in @('repositoryMigrationCount','finalMigrationCount','orphanMigrationCount')) {
            $fieldValue = Get-RequiredNonNullJsonProperty $reset $field 'Disposable-reset V2 status'
            if ($fieldValue -isnot [long] -or $fieldValue -lt 0) {
                throw "Disposable-reset V2 property '$field' must be a nonnegative Int64."
            }
        }
        $latestMigrationValue = Get-RequiredNonNullJsonProperty $reset 'latestMigration' 'Disposable-reset V2 status'
        if ($latestMigrationValue -isnot [string] -or
            $latestMigrationValue -cne '20260916132000_DisposableDevelopmentCurrentModelBaseline' -or
            [long]$reset.repositoryMigrationCount -ne 1 -or [long]$reset.orphanMigrationCount -ne 0) {
            throw 'Disposable-reset V2 repository migration count/latest/orphan identity is not the exact baseline contract.'
        }
        $expectedFinalMigrationCount = if ($phases -ccontains 'MIGRATIONS_APPLIED' -or $validatedTargetHistoryPresent) {
            $validatedTargetHistoryCount
        } else { [long]0 }
        if ([long]$reset.finalMigrationCount -ne $expectedFinalMigrationCount) {
            throw 'Disposable-reset V2 final migration count is inconsistent with its terminal durable phase.'
        }
        if ($repositoryIds.Count -gt 0 -and
            ([long]$reset.repositoryMigrationCount -ne $repositoryIds.Count -or
             [string]$reset.latestMigration -cne $repositoryIds[-1])) {
            throw 'Disposable-reset V2 status disagrees with independently derived repository migration evidence.'
        }
        if ($phaseMarkers.Count -ge 1 -and
            ([long]$phaseMarkers[0].repositoryMigrationCount -ne 1 -or
             [string]$phaseMarkers[0].latestMigration -cne '20260916132000_DisposableDevelopmentCurrentModelBaseline')) {
            throw 'Disposable-reset OFFLINE_GATES phase does not bind the exact baseline repository identity.'
        }
        if ($phases -ccontains 'MIGRATIONS_APPLIED') {
            $migrationPhaseIndex = [Array]::IndexOf($phases, 'MIGRATIONS_APPLIED')
            if ([long]$phaseMarkers[$migrationPhaseIndex].finalMigrationCount -ne 1) {
                throw 'Disposable-reset MIGRATIONS_APPLIED phase does not bind the exact baseline count.'
            }
        }
        if ($sourceIds.Count -gt 0 -or (Test-Path -LiteralPath (Join-Path $root 'source-migration-history.txt') -PathType Leaf)) {
            $fingerprintValue = Get-RequiredNonNullJsonProperty $reset 'sourceFingerprint' 'Disposable-reset V2 status'
            $fingerprintParts = @([string]$fingerprintValue -split '\|')
            $expectedSourceLatest = if ($sourceIds.Count -eq 0) { 'EMPTY' } else { $sourceIds[-1] }
            if (-not (Test-DisposableSourceFingerprintShape ([string]$fingerprintValue)) -or
                [uint64]$fingerprintParts[0] -ne $sourceIds.Count -or $fingerprintParts[1] -cne $expectedSourceLatest) {
                throw 'Disposable-reset V2 source fingerprint count/latest disagrees with independently derived source history.'
            }
        }
        if ($reset.status -ceq 'FAILED_NO_AUTOMATIC_RETRY' -and $phases -ccontains 'SOURCE_CAPTURE_COMPLETE') {
            $sourceFingerprintPath = Join-Path $root 'source-fingerprint-before.txt'
            $sourceHistoryPath = Join-Path $root 'source-migration-history.txt'
            if (-not (Test-Path -LiteralPath $sourceFingerprintPath -PathType Leaf) -or
                -not (Test-Path -LiteralPath $sourceHistoryPath -PathType Leaf)) {
                throw 'Failed disposable-reset evidence after source capture lacks source history/fingerprint evidence.'
            }
            $capturedFingerprint = (Get-Content -Raw -LiteralPath $sourceFingerprintPath).Trim()
            if ($capturedFingerprint -cne [string]$reset.sourceFingerprint) {
                throw 'Failed disposable-reset source fingerprint file disagrees with terminal status.'
            }
            $capturedParts = @($capturedFingerprint -split '\|')
            $capturedLatest = if ($sourceIds.Count -eq 0) { 'EMPTY' } else { $sourceIds[-1] }
            if (-not (Test-DisposableSourceFingerprintShape $capturedFingerprint) -or
                [uint64]$capturedParts[0] -ne $sourceIds.Count -or $capturedParts[1] -cne $capturedLatest) {
                throw 'Failed disposable-reset source fingerprint file disagrees with independently derived source history.'
            }
            $resetLogPath = Join-Path $root 'reset-database.log'
            if (Test-Path -LiteralPath $resetLogPath -PathType Leaf) {
                $expectedFinalFingerprintToken = "SOURCE_FINAL_FINGERPRINT=$capturedFingerprint"
                $resetBoundaryTokens = @(Get-SqlEvidenceTokens $resetLogPath)
                $finalFingerprintMarkers = @($resetBoundaryTokens |
                    Where-Object { $_ -ceq $expectedFinalFingerprintToken })
                $fingerprintMarkerLikeTokens = @($resetBoundaryTokens |
                    Where-Object { $_.Contains('SOURCE_FINAL_FINGERPRINT=', [StringComparison]::Ordinal) })
                $boundaryCompletionClaimed =
                    $phases -ccontains 'DATABASE_RECREATED' -or $validatedTargetHistoryPresent -or
                    [long]$reset.finalMigrationCount -gt 0 -or
                    $resetBoundaryTokens -ccontains 'DISPOSABLE_RESET_FINAL_SOURCE_RECHECK_COMPLETE' -or
                    $resetBoundaryTokens -ccontains 'DISPOSABLE_RESET_EMPTY_DATABASE_RECREATED'
                $earlyBoundaryFailure =
                    $reset.status -ceq 'FAILED_NO_AUTOMATIC_RETRY' -and
                    [string]$reset.phase -ceq 'RESET_STARTED' -and
                    [string]$reset.failedOperation -ceq 'RESET_STARTED' -and
                    $phases.Count -eq 5 -and $phases[-1] -ceq 'RESET_STARTED' -and
                    [bool]$reset.resetStarted -and -not $boundaryCompletionClaimed
                $allowedEarlyFailureTokens = @(
                    'DISPOSABLE_RESET_SERVER_IDENTITY_DRIFT',
                    'DISPOSABLE_RESET_IDENTITY_DRIFT',
                    'DISPOSABLE_RESET_POST_QUIESCENCE_IDENTITY_DRIFT',
                    'DISPOSABLE_RESET_FINAL_HISTORY_DRIFT',
                    'DISPOSABLE_RESET_FINAL_FINGERPRINT_DRIFT'
                )
                $earlyFailureSignals = @($resetBoundaryTokens |
                    Where-Object { $allowedEarlyFailureTokens -ccontains $_ })
                $exactMarkerValid = $finalFingerprintMarkers.Count -eq 1 -and
                    $fingerprintMarkerLikeTokens.Count -eq 1
                if (($boundaryCompletionClaimed -or $finalFingerprintMarkers.Count -gt 0) -and -not $exactMarkerValid) {
                    throw 'Failed disposable-reset final destructive-boundary fingerprint disagrees with captured source evidence.'
                }
                if (-not $boundaryCompletionClaimed -and $finalFingerprintMarkers.Count -eq 0 -and
                    (-not $earlyBoundaryFailure -or $fingerprintMarkerLikeTokens.Count -ne 0 -or
                     $earlyFailureSignals.Count -ne 1)) {
                    throw 'Failed disposable-reset lacks an exact final fingerprint or an explicit pre-marker destructive-boundary failure.'
                }
            }
        }
    }
    $ownedProperty = $reset.PSObject.Properties['attemptOwnedBackup']
    if (-not $isApprovedLegacy -and ($null -eq $ownedProperty -or $null -eq $ownedProperty.Value)) {
        throw 'Disposable-reset V2 attemptOwnedBackup is missing.'
    }

    $createPath = Join-Path $root 'backup-create.txt'
    $backupFileProperty = $reset.PSObject.Properties['backupFileName']
    if (-not $isApprovedLegacy -and $null -eq $backupFileProperty) {
        throw 'Disposable-reset V2 status is missing backupFileName.'
    }
    $mediaProperty = $reset.PSObject.Properties['backupMediaId']
    if (-not $isApprovedLegacy -and ($null -eq $mediaProperty -or $null -eq $mediaProperty.Value -or
        $null -eq $backupFileProperty -or $null -eq $backupFileProperty.Value -or
        $null -eq $reset.PSObject.Properties['backupPathSha256'] -or
        $null -eq $reset.PSObject.Properties['backupPathSha256'].Value)) {
        throw 'Disposable-reset V2 path identity fields must be present.'
    }
    $mediaText = if ($null -eq $mediaProperty) { '' } else { [string]$mediaProperty.Value }
    $fileText = if ($null -eq $backupFileProperty) { '' } else { [string]$backupFileProperty.Value }
    $pathHashProperty = $reset.PSObject.Properties['backupPathSha256']
    $pathHashText = if ($null -eq $pathHashProperty) { '' } else { [string]$pathHashProperty.Value }
    $v2PathUnresolved = -not $isApprovedLegacy -and [string]::IsNullOrEmpty($mediaText) -and
        [string]::IsNullOrEmpty($fileText) -and [string]::IsNullOrEmpty($pathHashText)
    if ($v2PathUnresolved -and ($reset.backupCreated -eq $true -or $reset.backupCompleted -eq $true -or
        $reset.attemptOwnedBackup -eq $true -or $backupPhaseMarkerPublished -or $reset.backupVerified -eq $true -or
        $reset.resetStarted -eq $true -or $reset.phase -notin @('NOT_STARTED','OFFLINE_GATES_COMPLETE','SOURCE_CAPTURE_COMPLETE'))) {
        throw 'Disposable-reset unresolved V2 backup path is inconsistent with terminal state.'
    }
    if ($v2PathUnresolved -and ($reset.backupPreserved -eq $true -or [long]$reset.backupByteLength -ne 0 -or
        -not [string]::IsNullOrEmpty([string]$reset.currentMaterialSha256) -or
        -not [string]::IsNullOrEmpty([string]$reset.backupSha256) -or $reset.backupHashMatchesVerified -eq $true -or
        $reset.verifyEvidencePresent -eq $true)) {
        throw 'Disposable-reset unresolved V2 state contains material or verification claims.'
    }
    $expectedBackupFileName = if ($isApprovedLegacy) {
        'RhemaERP_DISPOSABLE_RESET_COPYONLY.bak'
    }
    elseif ($v2PathUnresolved) { '' }
    else {
        $candidateBackupFileName = [string]$backupFileProperty.Value
        $derivedBackupFileName = "RhemaERP_DISPOSABLE_RESET_COPYONLY_$($reset.backupMediaId).bak"
        if ([string]$reset.backupMediaId -cnotmatch '^[0-9a-f]{32}$' -or
            $candidateBackupFileName -cne $derivedBackupFileName -or
            [System.IO.Path]::GetFileName($candidateBackupFileName) -cne $candidateBackupFileName) {
            throw 'Disposable-reset media-bound backup filename is malformed or path-ambiguous.'
        }
        $candidateBackupFileName
    }
    $expectedBackupPathHash = if ($isApprovedLegacy -or $v2PathUnresolved) { '' } else {
        if ($null -eq $pathHashProperty -or [string]$pathHashProperty.Value -notmatch '^[0-9A-F]{64}$') {
            throw 'Disposable-reset media-bound backup path hash is missing or malformed.'
        }
        [string]$pathHashProperty.Value
    }
    $ownedEmptyReservation = -not $isApprovedLegacy -and -not $v2PathUnresolved -and
        $reset.attemptOwnedBackup -eq $true -and $reset.backupCreated -ne $true
    $resolvedUnowned = -not $isApprovedLegacy -and -not $v2PathUnresolved -and
        $reset.attemptOwnedBackup -ne $true -and $reset.backupCreated -ne $true
    if ($ownedEmptyReservation -and ($reset.backupCompleted -eq $true -or $reset.backupPreserved -eq $true -or
        [long]$reset.backupByteLength -ne 0 -or -not [string]::IsNullOrEmpty([string]$reset.currentMaterialSha256) -or
        -not [string]::IsNullOrEmpty([string]$reset.backupSha256) -or $reset.backupVerified -eq $true -or
        $reset.backupHashMatchesVerified -ne $false -or $reset.verifyEvidencePresent -eq $true -or
        $backupPhaseMarkerPublished -or $reset.resetStarted -eq $true)) {
        throw 'Disposable-reset owned empty reservation contains material, completion, verification, or reset claims.'
    }
    if ($resolvedUnowned -and ($reset.backupCompleted -eq $true -or $reset.backupPreserved -eq $true -or
        [long]$reset.backupByteLength -ne 0 -or -not [string]::IsNullOrEmpty([string]$reset.currentMaterialSha256) -or
        -not [string]::IsNullOrEmpty([string]$reset.backupSha256) -or $reset.backupVerified -eq $true -or
        $reset.backupHashMatchesVerified -ne $false -or $reset.verifyEvidencePresent -eq $true -or
        $backupPhaseMarkerPublished -or $reset.resetStarted -eq $true)) {
        throw 'Disposable-reset resolved unowned path contains backup or reset claims.'
    }
    if (-not $isApprovedLegacy -and $reset.backupCreated -eq $true -and $reset.attemptOwnedBackup -ne $true) {
        throw 'Disposable-reset material backup is not owned by this attempt.'
    }
    if (-not $isApprovedLegacy -and -not $v2PathUnresolved -and -not $ownedEmptyReservation -and -not $resolvedUnowned -and
        $reset.backupCreated -ne $true) {
        throw 'Disposable-reset resolved V2 path is neither an owned empty reservation nor material backup.'
    }
    $create = if (Test-Path -LiteralPath $createPath -PathType Leaf) {
        @(Get-SqlEvidenceTokens $createPath)
    }
    else { @() }
    if ($reset.backupCompleted -eq $true) {
        try {
            Assert-UniqueOrderedSqlEvidenceTokens $createPath @(
                'DATABASE=RhemaERP',
                "BACKUP_MEDIA_ID=$($reset.backupMediaId)",
                'BACKUP_PATH_ATOMICALLY_RESERVED',
                'BACKUP_COPY_ONLY_CHECKSUM_START',
                'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE'
            ) 'Completed disposable-reset backup'
        }
        catch {
            throw 'Completed disposable-reset backup requires unique ordered database/media, atomic-reservation and COPY_ONLY CHECKSUM markers.'
        }
    }

    if ($reset.backupCreated -eq $true) {
        $currentHashPath = Join-Path $root 'backup-current.sha256'
        if (-not (Test-Path -LiteralPath $createPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $currentHashPath -PathType Leaf)) { throw 'Material backup creation/current-hash evidence is missing.' }
        $currentHashLine = (Get-Content -Raw -LiteralPath $currentHashPath).Trim()
        if ([string]$reset.backupMediaId -notmatch '^[0-9a-f]{32}$' -or
            $create -cnotcontains "BACKUP_MEDIA_ID=$($reset.backupMediaId)" -or
            $currentHashLine -notmatch '^(?<hash>[0-9A-F]{64})  (?<file>[^\\/]+\.bak)$' -or
            $Matches.hash -cne [string]$reset.currentMaterialSha256 -or $Matches.file -cne $expectedBackupFileName -or
            [long]$reset.backupByteLength -le 0 -or
            $reset.backupPreserved -ne $true -or $reset.backupMaterialStateReconciled -ne $true) {
            throw 'Disposable-reset material backup identity/current-hash/preservation evidence is invalid.'
        }
        if ($backupPhaseMarkerPublished) {
            $phaseThree = $phaseMarkers[2]
            $phaseThreeFileProperty = $phaseThree.PSObject.Properties['backupFileName']
            $phaseThreeFileName = if ($null -eq $phaseThreeFileProperty) {
                'RhemaERP_DISPOSABLE_RESET_COPYONLY.bak'
            } else { [string]$phaseThreeFileProperty.Value }
            if ($reset.backupCompleted -ne $true -or $phaseThree.backupCompleted -ne $true -or
                [string]$phaseThree.database -cne 'RhemaERP' -or
                [string]$phaseThree.backupMediaId -cne [string]$reset.backupMediaId -or
            $phaseThreeFileName -cne $expectedBackupFileName -or
                (-not $isApprovedLegacy -and
                 ([string]$phaseThree.backupPathSha256 -cne $expectedBackupPathHash)) -or
                [long]$phaseThree.backupByteLength -le 0 -or
                [string]$phaseThree.currentMaterialSha256 -notmatch '^[0-9A-F]{64}$') {
                throw 'Durable BACKUP_CREATED requires database/media identity, completed SQL markers, positive length and hash reconciliation.'
            }
        }
    }
    elseif ($reset.backupPreserved -eq $true) {
        throw 'Disposable-reset cannot claim backup preservation without current material evidence.'
    }
    $phaseFourPublished = $phases -ccontains 'BACKUP_VERIFIED'
    if ($phaseFourPublished) {
        $verifyPath = Join-Path $root 'backup-verify.txt'
        $hashPath = Join-Path $root 'backup.sha256'
        if (-not (Test-Path -LiteralPath $verifyPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $hashPath -PathType Leaf)) { throw 'BACKUP_VERIFIED proof is incomplete.' }
        $verify = @(Get-SqlEvidenceTokens $verifyPath)
        $hashLine = (Get-Content -Raw -LiteralPath $hashPath).Trim()
        $phaseFourHash = [string]$phaseMarkers[3].backupSha256
        $phaseFourFileProperty = $phaseMarkers[3].PSObject.Properties['backupFileName']
        $phaseFourMediaProperty = $phaseMarkers[3].PSObject.Properties['backupMediaId']
        $phaseFourPathHashProperty = $phaseMarkers[3].PSObject.Properties['backupPathSha256']
        $currentMatchesVerified = [string]$reset.currentMaterialSha256 -ceq $phaseFourHash
        try {
            Assert-UniqueOrderedSqlEvidenceTokens $verifyPath @(
                'DATABASE=RhemaERP',
                "BACKUP_MEDIA_ID=$($reset.backupMediaId)",
                'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE'
            ) 'Disposable-reset VERIFYONLY'
        }
        catch { throw 'Disposable-reset VERIFYONLY database/media/completion markers are not unique and ordered.' }
        if ($phaseFourHash -notmatch '^[0-9A-F]{64}$' -or
            [string]$phaseMarkers[2].currentMaterialSha256 -cne $phaseFourHash -or
            $reset.backupCompleted -ne $true -or $verify -cnotcontains 'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE' -or
            $verify -cnotcontains "BACKUP_MEDIA_ID=$($reset.backupMediaId)" -or
            $hashLine -notmatch '^(?<hash>[0-9A-F]{64})  (?<file>[^\\/]+\.bak)$' -or
            $Matches.hash -cne $phaseFourHash -or $Matches.file -cne $expectedBackupFileName -or
            ($null -ne $backupFileProperty -and
             ($null -eq $phaseFourFileProperty -or [string]$phaseFourFileProperty.Value -cne $expectedBackupFileName -or
              $null -eq $phaseFourMediaProperty -or [string]$phaseFourMediaProperty.Value -cne [string]$reset.backupMediaId)) -or
            (-not $isApprovedLegacy -and
             ($null -eq $phaseFourPathHashProperty -or [string]$phaseFourPathHashProperty.Value -cne $expectedBackupPathHash)) -or
            [string]$reset.backupSha256 -cne $phaseFourHash -or
            $reset.verifyEvidencePresent -ne $true -or [bool]$reset.backupHashMatchesVerified -ne $currentMatchesVerified -or
            [bool]$reset.backupVerified -ne $currentMatchesVerified) {
            throw 'Disposable-reset VERIFYONLY, media identity, or backup hash proof is invalid.'
        }
    }
    elseif ($reset.backupVerified -eq $true -or -not [string]::IsNullOrWhiteSpace([string]$reset.backupSha256)) {
        throw 'Disposable-reset terminal status claims a verified hash without durable phase-04.'
    }
    if ($reset.resetStarted -eq $true) {
        $resetLog = Join-Path $root 'reset-database.log'
        if (-not (Test-Path -LiteralPath $resetLog -PathType Leaf)) {
            throw 'RESET_STARTED status requires retained destructive-boundary evidence.'
        }
        if ($reset.status -eq 'PASS') {
            $resetLines = @(Get-Content -LiteralPath $resetLog | ForEach-Object { $_.Trim() })
            foreach ($marker in @('DISPOSABLE_RESET_SOURCE_QUIESCED','DISPOSABLE_RESET_FINAL_SOURCE_RECHECK_COMPLETE',
                'DISPOSABLE_RESET_EMPTY_DATABASE_RECREATED')) {
                if ($resetLines -cnotcontains $marker) { throw "Disposable-reset PASS lacks boundary marker: $marker" }
            }
        }
    }
    if ($reset.status -eq 'PASS') {
        foreach ($name in @('git-diff-check.log','commit-ancestry.txt','git-head-tree.txt','reset-build.log',
            'ef-no-pending-model.log','migration-discovery.log','repository-migration-history.txt',
            'source-migration-history.txt','source-fingerprint-before.txt','source-database-identity.sha256',
            'source-server-identity.sha256','pre-mutation-reviewed-git-state.json','backup-create.txt',
            'backup-verify.txt','backup.sha256','backup-current.sha256','reset-database.log','reset-apply-migrations.log','target-migration-history.txt','reset-seed-pass-1.log',
            'reset-seed-pass-2.log','reset-invariants-pass-1.txt','reset-invariants-pass-2.txt',
            'reset-invariants.sha256','reset-dbcc.txt')) {
            if (-not (Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) { throw "Disposable-reset PASS is missing: $name" }
        }
        $firstHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'reset-invariants-pass-1.txt')).Hash
        $secondHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'reset-invariants-pass-2.txt')).Hash
        $hashLines = @("$firstHash  reset-invariants-pass-1.txt","$secondHash  reset-invariants-pass-2.txt")
        if ($firstHash -cne $secondHash -or [string]$reset.invariantSha256 -cne $firstHash -or
            ((Get-Content -LiteralPath (Join-Path $root 'reset-invariants.sha256')) -join "`n") -cne ($hashLines -join "`n") -or
            (Get-Content -Raw -LiteralPath (Join-Path $root 'reset-dbcc.txt')) -notmatch '(?m)^DBCC_CHECKDB_COMPLETE\s*$') {
            throw 'Disposable-reset PASS invariant or DBCC evidence is invalid.'
        }
        $sourceFingerprint = (Get-Content -Raw -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')).Trim()
        $resetTokens = @(Get-SqlEvidenceTokens (Join-Path $root 'reset-database.log'))
        $sourceHistory = @((Read-MigrationHistoryEvidence `
            (Join-Path $root 'source-migration-history.txt') 'Disposable-reset PASS source history' `
            $allowReviewedLegacyMigrationHistory).ids)
        $fingerprintParts = @($sourceFingerprint -split '\|')
        $expectedLatest = if ($sourceHistory.Count -eq 0) { 'EMPTY' } else { $sourceHistory[-1] }
        if (-not (Test-DisposableSourceFingerprintShape $sourceFingerprint) -or
            [uint64]$fingerprintParts[0] -ne $sourceHistory.Count -or $fingerprintParts[1] -cne $expectedLatest -or
            $sourceFingerprint -cne [string]$reset.sourceFingerprint -or
            @($resetTokens | Where-Object { $_ -ceq "SOURCE_FINAL_FINGERPRINT=$sourceFingerprint" }).Count -ne 1) {
            throw 'Disposable-reset PASS does not bind the captured fingerprint to the final quiescent recheck.'
        }
    }

    if ($null -eq $reset.artifactSha256) { throw 'Disposable-reset status lacks artifact hash bindings.' }
    $actualArtifacts = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object Name -notin @('reset-status.json','manifest.sha256'))
    foreach ($file in $actualArtifacts) {
        $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\','/')
        $property = $reset.artifactSha256.PSObject.Properties[$relative]
        if ($null -eq $property -or [string]$property.Value -cne (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash) {
            throw "Disposable-reset artifact hash binding is missing or invalid: $relative"
        }
    }
    foreach ($property in $reset.artifactSha256.PSObject.Properties) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $property.Name) -PathType Leaf)) {
            throw "Disposable-reset status binds a missing artifact: $($property.Name)"
        }
    }
}

if ($PackageKind -eq 'FinalClone') {
    $requiredCommandEvidence = [ordered]@{
        'git-diff-check.log' = 'git'
        'commit-ancestry.txt' = 'git'
        'git-head-tree.txt' = 'git'
        'clone-build.log' = 'dotnet'
        'ef-no-pending-model.log' = 'dotnet'
        'migration-discovery.log' = 'dotnet'
    }
    if ($summary.status -eq 'PASS') {
        $requiredCommandEvidence['clone-apply-migrations.log'] = 'dotnet'
        $requiredCommandEvidence['seed-pass-1.log'] = 'dotnet'
        $requiredCommandEvidence['seed-pass-2.log'] = 'dotnet'
    }
    foreach ($entry in $requiredCommandEvidence.GetEnumerator()) {
        $commandEvidencePath = Join-Path $root $entry.Key
        $commandEvidenceLines = @(Get-Content -LiteralPath $commandEvidencePath)
        if ($commandEvidenceLines.Count -eq 0) {
            throw "Required command evidence is empty: $($entry.Key)"
        }
        $expectedMarker = "RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=$($entry.Value)"
        if ($commandEvidenceLines[-1] -cne $expectedMarker -or
            @($commandEvidenceLines | Where-Object { $_ -like 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|*' }).Count -ne 1) {
            throw "Required command evidence has a missing or invalid success marker: $($entry.Key)"
        }
    }

    if (-not [string]::Equals([string]$summary.sourceDatabase, 'RhemaERP', [StringComparison]::OrdinalIgnoreCase) -or
        [string]$summary.targetDatabase -notmatch '^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$' -or
        $summary.sameServer -ne $true -or $summary.sourceServer -ne '<REDACTED_SAME_SERVER>' -or
        $summary.targetServer -ne '<REDACTED_SAME_SERVER>') {
        throw 'Final-clone summary does not bind exact RhemaERP source and prefix-safe target identities.'
    }
    $reviewedState = Get-Content -Raw -LiteralPath (Join-Path $root 'reviewed-git-state.json') | ConvertFrom-Json
    $finalHistorySchemaProperty = $summary.PSObject.Properties['migrationHistoryEvidenceSchema']
    if ($null -eq $finalHistorySchemaProperty -or $null -eq $finalHistorySchemaProperty.Value) {
        throw "Final-clone summary requires non-null property 'migrationHistoryEvidenceSchema'."
    }
    if ($finalHistorySchemaProperty.Value -isnot [string] -or
        $finalHistorySchemaProperty.Value -cne $migrationHistoryEvidenceSchema) {
        throw 'Final-clone migration-history evidence schema must be the exact scalar JSON String contract.'
    }
    $allowReviewedLegacyFinalHistory = $false
    $gitHeadTree = @(Get-Content -LiteralPath (Join-Path $root 'git-head-tree.txt') |
        ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -notlike 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|*' })
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
    if ($migrationIds.Count -ne 1 -or $migrationIds[-1] -ne '20260916132000_DisposableDevelopmentCurrentModelBaseline') {
        throw "Final-clone migration evidence is not the authoritative disposable-development baseline. Count=$($migrationIds.Count); Latest=$($migrationIds[-1])."
    }
    if (@($migrationIds | Sort-Object -Unique).Count -ne 1 -or
        (@($migrationIds | Sort-Object) -join "`n") -ne ($migrationIds -join "`n")) {
        throw 'Final-clone migration evidence contains duplicate or out-of-order migration IDs.'
    }
    if ($summary.repositoryMigrationCount -isnot [long] -or [long]$summary.repositoryMigrationCount -ne $migrationIds.Count -or
        $summary.latestMigration -isnot [string] -or [string]$summary.latestMigration -cne $migrationIds[-1]) {
        throw 'Final-clone summary repository migration count/latest disagrees with independently derived discovery evidence.'
    }
    $sourceHistory = @((Read-MigrationHistoryEvidence (Join-Path $root 'source-migration-history.txt') `
        'Final-clone source history' $allowReviewedLegacyFinalHistory).ids)
    $pending = @((Read-MigrationHistoryEvidence (Join-Path $root 'pending-migrations.txt') `
        'Final-clone pending migrations' $allowReviewedLegacyFinalHistory).ids)
    $orphan = @((Read-MigrationHistoryEvidence (Join-Path $root 'orphan-history.txt') `
        'Final-clone orphan history' $allowReviewedLegacyFinalHistory).ids)
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
    $generationEvidence = @(Get-Content -LiteralPath (Join-Path $root 'idempotent-script-generation.log'))
    if ($generationEvidence.Count -eq 0) {
        throw 'Required idempotent-script generation evidence is empty.'
    }
    if ($derivedPending.Count -eq 0) {
        $expectedNotRequired = 'RHEMA_IDEMPOTENT_SCRIPT_GENERATION_V1|STATUS=NOT_REQUIRED|REASON=ZERO_PENDING_MIGRATIONS|PENDING_COUNT=0'
        if ($generationEvidence.Count -ne 1 -or $generationEvidence[0] -cne $expectedNotRequired) {
            throw 'Zero-pending idempotent-script evidence is missing or invalid.'
        }
    }
    else {
        $expectedGenerationMarker = 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=dotnet'
        if ($generationEvidence[-1] -cne $expectedGenerationMarker -or
            @($generationEvidence | Where-Object { $_ -like 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|*' }).Count -ne 1 -or
            @($generationEvidence | Where-Object { $_ -like 'RHEMA_IDEMPOTENT_SCRIPT_GENERATION_V1|*' }).Count -ne 0) {
            throw 'Pending-migration idempotent-script generation lacks valid native dotnet success evidence.'
        }
    }
    $before = (Get-Content -Raw -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')).Trim()
    $after = (Get-Content -Raw -LiteralPath (Join-Path $root 'source-fingerprint-after.txt')).Trim()
    $sourceFingerprintParts = @($before -split '\|')
    $expectedSourceLatest = if ($sourceHistory.Count -eq 0) { 'EMPTY' } else { $sourceHistory[-1] }
    if ($before -ne $after -or $after -ne [string]$summary.sourceFingerprint -or
        -not (Test-DisposableSourceFingerprintShape $before) -or
        [uint64]$sourceFingerprintParts[0] -ne $sourceHistory.Count -or
        $sourceFingerprintParts[1] -cne $expectedSourceLatest) {
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
        $targetHistory = @((Read-MigrationHistoryEvidence (Join-Path $root 'target-migration-history.txt') `
            'Final-clone target history' $allowReviewedLegacyFinalHistory).ids)
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
    Where-Object Extension -in @('.json', '.txt', '.log', '.sha256', '.sql', '.md')
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
    $manifestContent = @(Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object FullName -ne $manifestPath |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
            "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)  $relative"
        }) -join "`n"
    $manifestTemporaryPath = Join-Path $root ('.manifest.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = [System.IO.File]::Open($manifestTemporaryPath, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $writer = [System.IO.StreamWriter]::new($stream, [System.Text.ASCIIEncoding]::new())
            try { $writer.WriteLine($manifestContent); $writer.Flush(); $stream.Flush($true) }
            finally { $writer.Dispose() }
        }
        finally { if ($null -ne $stream) { $stream.Dispose() } }
        [System.IO.File]::Move($manifestTemporaryPath, $manifestPath, $true)
    }
    finally {
        if (Test-Path -LiteralPath $manifestTemporaryPath -PathType Leaf) { Remove-Item -LiteralPath $manifestTemporaryPath -Force }
    }
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
