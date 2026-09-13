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
    for ($index = 0; $index -lt $phaseFiles.Count; $index++) {
        if ($phaseFiles[$index].Name -cne ("phase-{0:D2}.json" -f ($index + 1))) {
            throw 'Disposable-reset phase markers are not a contiguous monotonic sequence.'
        }
        $marker = Get-Content -Raw -LiteralPath $phaseFiles[$index].FullName | ConvertFrom-Json
        if ($marker.schema -cne 'RHEMA_DISPOSABLE_RESET_PHASE_V1' -or [int]$marker.ordinal -ne ($index + 1)) {
            throw 'Disposable-reset phase marker schema or ordinal is invalid.'
        }
        $phases += [string]$marker.phase
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
    if (($phases -ccontains 'BACKUP_VERIFIED') -ne [bool]$reset.backupVerified) {
        throw 'Disposable-reset status and durable BACKUP_VERIFIED marker disagree.'
    }
    if (($phases -ccontains 'RESET_STARTED') -ne [bool]$reset.resetStarted) {
        throw 'Disposable-reset RESET_STARTED marker and terminal status disagree.'
    }
    if ($reset.status -eq 'PASS' -and (($phases -join "`n") -cne ($expectedPhases -join "`n") -or
        $reset.backupCreated -ne $true -or $reset.backupVerified -ne $true -or $reset.resetStarted -ne $true)) {
        throw 'Disposable-reset PASS does not contain the complete monotonic phase sequence.'
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
        $expectedMarker = "RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=$($entry.Value)"
        $failurePattern = '^RHEMA_NATIVE_COMMAND_EVIDENCE_V1\|STATUS=FAILURE\|EXIT_CODE=[1-9]\d*\|COMMAND=' + [regex]::Escape($entry.Value) + '$'
        $validTerminalMarker = $lines[-1] -ceq $expectedMarker -or
            ($reset.status -eq 'FAILED_NO_AUTOMATIC_RETRY' -and $lines[-1] -cmatch $failurePattern)
        if ($lines.Count -eq 0 -or -not $validTerminalMarker -or
            @($lines | Where-Object { $_ -like 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|*' }).Count -ne 1) {
            throw "Disposable-reset native evidence is empty or marker-invalid: $($entry.Key)"
        }
    }

    if (Test-Path -LiteralPath (Join-Path $root 'migration-discovery.log') -PathType Leaf) {
        $repositoryIds = @(Get-Content -LiteralPath (Join-Path $root 'migration-discovery.log') | ForEach-Object {
            if ($_.Trim() -match '^(?<id>\d{14}_[^\s]+)') { $Matches.id }
        })
        if ($repositoryIds.Count -ne 456 -or $repositoryIds[-1] -cne '20260908120000_AddProducerIntentGroupsC8' -or
            @($repositoryIds | Sort-Object -Unique).Count -ne 456 -or
            (@($repositoryIds | Sort-Object) -join "`n") -cne ($repositoryIds -join "`n")) {
            throw 'Disposable-reset repository history is not the exact unique ordered 456/C8 list.'
        }
        $repositoryHistoryPath = Join-Path $root 'repository-migration-history.txt'
        if (-not (Test-Path -LiteralPath $repositoryHistoryPath -PathType Leaf) -or
            ((Get-Content -LiteralPath $repositoryHistoryPath) -join "`n") -cne ($repositoryIds -join "`n")) {
            throw 'Disposable-reset repository migration identity evidence is missing or inconsistent.'
        }
        if (Test-Path -LiteralPath (Join-Path $root 'target-migration-history.txt') -PathType Leaf) {
            $targetIds = @(Get-Content -LiteralPath (Join-Path $root 'target-migration-history.txt'))
            if (($targetIds -join "`n") -cne ($repositoryIds -join "`n")) {
                throw 'Disposable-reset target history is not exactly repository 456/C8 with zero orphans.'
            }
        }
    }
    if (Test-Path -LiteralPath (Join-Path $root 'source-migration-history.txt') -PathType Leaf) {
        $sourceIds = @(Get-Content -LiteralPath (Join-Path $root 'source-migration-history.txt') |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if (@($sourceIds | Sort-Object -Unique).Count -ne $sourceIds.Count -or
            (@($sourceIds | Sort-Object) -join "`n") -cne ($sourceIds -join "`n") -or
            @($sourceIds | Where-Object { $_ -notmatch '^\d{14}_[A-Za-z0-9_]+$' }).Count -ne 0) {
            throw 'Disposable-reset source history is duplicate, out of order, or malformed.'
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

    if ($reset.backupCreated -eq $true) {
        $createPath = Join-Path $root 'backup-create.txt'
        $hashPath = Join-Path $root 'backup.sha256'
        if (-not (Test-Path -LiteralPath $createPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $hashPath -PathType Leaf)) { throw 'Material backup creation/hash evidence is missing.' }
        $create = @(Get-Content -LiteralPath $createPath | ForEach-Object { $_.Trim() })
        $hashLine = (Get-Content -Raw -LiteralPath $hashPath).Trim()
        if ([string]$reset.backupMediaId -notmatch '^[0-9a-f]{32}$' -or
            $create -cnotcontains "BACKUP_MEDIA_ID=$($reset.backupMediaId)" -or
            $hashLine -notmatch '^(?<hash>[0-9A-F]{64})  RhemaERP_DISPOSABLE_RESET_COPYONLY\.bak$' -or
            $Matches.hash -cne [string]$reset.backupSha256) {
            throw 'Disposable-reset backup creation markers or media identity are invalid.'
        }
        if (($reset.status -eq 'PASS' -or $reset.backupCompleted -eq $true) -and
            ($create -cnotcontains 'BACKUP_PATH_ATOMICALLY_RESERVED' -or
             $create -cnotcontains 'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE')) {
            throw 'Disposable-reset status claims a completed backup without completion markers.'
        }
    }
    if ($reset.backupVerified -eq $true) {
        $verifyPath = Join-Path $root 'backup-verify.txt'
        $hashPath = Join-Path $root 'backup.sha256'
        if (-not (Test-Path -LiteralPath $verifyPath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $hashPath -PathType Leaf)) { throw 'BACKUP_VERIFIED proof is incomplete.' }
        $verify = @(Get-Content -LiteralPath $verifyPath | ForEach-Object { $_.Trim() })
        $hashLine = (Get-Content -Raw -LiteralPath $hashPath).Trim()
        if ($reset.backupCompleted -ne $true -or $verify -cnotcontains 'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE' -or
            $verify -cnotcontains "BACKUP_MEDIA_ID=$($reset.backupMediaId)" -or
            $hashLine -notmatch '^(?<hash>[0-9A-F]{64})  RhemaERP_DISPOSABLE_RESET_COPYONLY\.bak$' -or
            $Matches.hash -cne [string]$reset.backupSha256) {
            throw 'Disposable-reset VERIFYONLY, media identity, or backup hash proof is invalid.'
        }
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
            'backup-verify.txt','backup.sha256','reset-database.log','reset-apply-migrations.log','target-migration-history.txt','reset-seed-pass-1.log',
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
        $resetLines = @(Get-Content -LiteralPath (Join-Path $root 'reset-database.log') | ForEach-Object { $_.Trim() })
        if ($sourceFingerprint -cne [string]$reset.sourceFingerprint -or
            $resetLines -cnotcontains "SOURCE_FINAL_FINGERPRINT=$sourceFingerprint") {
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
