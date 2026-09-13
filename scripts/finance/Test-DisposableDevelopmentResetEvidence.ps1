Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Test-GlCutoverEvidencePackage.ps1'
$temporaryRoots = [System.Collections.Generic.List[string]]::new()

function New-PackageRoot([string]$label) {
    $path = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMA_RESET_EVIDENCE_${label}_$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $path | Out-Null
    $temporaryRoots.Add($path)
    $path
}

function Write-Json([string]$path, $value) {
    $value | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 -LiteralPath $path
}

function Write-HistoryFixture([string]$root, [string]$name, [string[]]$ids) {
    $state = if ($ids.Count -eq 0) { 'EMPTY' } else { 'POPULATED' }
    @("RHEMA_MIGRATION_HISTORY_V1|COUNT=$($ids.Count)|STATE=$state") + @($ids) |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $root $name)
}

function Complete-Status([string]$root, [hashtable]$status) {
    $failedOperation = if ($status.status -eq 'PASS') { 'NOT_APPLICABLE' } else { [string]$status.failedOperation }
    @('# Disposable RhemaERP reset recovery','',"Last durable phase: $($status.phase)",
        "Failed operation: $failedOperation","Verified backup available: $(([bool]$status.backupVerified).ToString().ToLowerInvariant())",'',
        'Do not rerun this reset automatically.') | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'RECOVERY.md')
    $hashes = [ordered]@{}
    Get-ChildItem -LiteralPath $root -File | Where-Object Name -notin @('reset-status.json','manifest.sha256') |
        Sort-Object Name | ForEach-Object { $hashes[$_.Name] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash }
    $status.artifactSha256 = $hashes
    Write-Json (Join-Path $root 'reset-status.json') $status
}

function New-Common([string]$root) {
    $reviewed = [ordered]@{
        reviewedCommit=('a' * 40); reviewedTree=('b' * 40); executedCommit=('a' * 40);
        executedTree=('b' * 40); repositoryClean=$true
    }
    Write-Json (Join-Path $root 'reviewed-git-state.json') $reviewed
    Write-Json (Join-Path $root 'feature-flags.json') ([ordered]@{
        accountingEvents=$false; producerIntents=$false; producerIntentGroups=$false;
        source='explicit process environment variables'
    })
    '# Recovery pending terminal status.' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $root 'RECOVERY.md')
    $reviewed
}

function Add-OfflineGateEvidence([string]$root) {
    $reviewed = Get-Content -Raw -LiteralPath (Join-Path $root 'reviewed-git-state.json') | ConvertFrom-Json
    $nativeGit = 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=git'
    $nativeDotnet = 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=dotnet'
    @($reviewed.executedCommit,$reviewed.executedTree,$nativeGit) |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'git-head-tree.txt')
    @("$($reviewed.executedCommit) $('c' * 40)",$nativeGit) |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'commit-ancestry.txt')
    $nativeGit | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'git-diff-check.log')
    foreach ($name in @('reset-build.log','ef-no-pending-model.log')) {
        $nativeDotnet | Set-Content -Encoding ascii -LiteralPath (Join-Path $root $name)
    }
    $repositoryIds = @('20260913162402_DisposableDevelopmentCurrentModelBaseline')
    @($repositoryIds + $nativeDotnet) |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'migration-discovery.log')
    Write-HistoryFixture $root 'repository-migration-history.txt' $repositoryIds
}

function Add-EmptySourceCaptureEvidence([string]$root) {
    Write-HistoryFixture $root 'source-migration-history.txt' @()
    '0|EMPTY|0|0|0' | Set-Content -Encoding ascii -LiteralPath (Join-Path $root 'source-fingerprint-before.txt')
    $phaseTwoPath = Join-Path $root 'phase-02.json'
    if (Test-Path -LiteralPath $phaseTwoPath -PathType Leaf) {
        $phaseTwo = Get-Content -Raw -LiteralPath $phaseTwoPath | ConvertFrom-Json
        $phaseTwo | Add-Member -NotePropertyName sourceHistorySha256 -NotePropertyValue `
            (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'source-migration-history.txt')).Hash -Force
        Write-Json $phaseTwoPath $phaseTwo
    }
}

function New-Status([string]$terminalStatus, [string]$phase, [bool]$backupCreated,
    [bool]$backupVerified, [bool]$resetStarted) {
    $result = @{
        evidenceSchema='RHEMA_DISPOSABLE_RESET_EVIDENCE_V2'; backupIdentityVersion='MEDIA_BOUND_V1';
        migrationHistoryEvidenceSchema='RHEMA_MIGRATION_HISTORY_V1';
        mode='ResetDisposableDevelopment'; status=$terminalStatus; phase=$phase; database='RhemaERP';
        server='<REDACTED_LOCAL_SERVER>'; repositoryClean=$true; reviewedCommit=('a' * 40);
        reviewedTree=('b' * 40); backupCreated=$backupCreated; backupVerified=$backupVerified;
        resetStarted=$resetStarted; automaticRetry=$false; automaticCleanup=$false
        backupPhaseMarkerPublished=$backupCreated; backupMaterialStateReconciled=$true
        backupByteLength=if($backupCreated){1024}else{0}
        backupPreserved=$backupCreated; backupCompleted=$backupCreated
        currentMaterialSha256=if($backupCreated){'E' * 64}else{''}
        backupSha256=''; backupHashMatchesVerified=$false; verifyEvidencePresent=$false
        backupMediaId=''; backupFileName=''; backupPathSha256=''; attemptOwnedBackup=$backupCreated
        repositoryMigrationCount=1; finalMigrationCount=if($phase -in @('MIGRATIONS_APPLIED','SEED_INVARIANTS_VERIFIED','DBCC_COMPLETE','COMPLETE')){1}else{0}
        latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline'; orphanMigrationCount=0
        sourceFingerprint='0|EMPTY|0|0|0'
        failedOperation=if($terminalStatus -eq 'PASS'){'NOT_APPLICABLE'}else{'SYNTHETIC_OPERATION'}
    }
    $result
}

function Invoke-ExpectedFailure([string]$root, [string]$label) {
    $output = & pwsh -NoProfile -File $validator -EvidenceDirectory $root -PackageKind DisposableReset 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "Tamper case unexpectedly passed: $label" }
    Write-Host "PASS: $label refused"
}

function Invoke-ExpectedSemanticFailure([string]$root, [string]$label) {
    $output = & pwsh -NoProfile -File $validator -EvidenceDirectory $root -PackageKind DisposableReset -WriteManifest 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "Semantic tamper case unexpectedly passed after re-manifesting: $label" }
    if ($output -cnotmatch [regex]::Escape('unique ordered database/media')) {
        throw "Semantic tamper case failed for the wrong reason: $label"
    }
    Write-Host "PASS: $label refused before manifest publication"
}

function Invoke-ExpectedRemanifestFailure([string]$root, [string]$label) {
    $output = & pwsh -NoProfile -File $validator -EvidenceDirectory $root -PackageKind DisposableReset -WriteManifest 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "Bound-identity tamper unexpectedly passed after re-manifesting: $label" }
    Write-Host "PASS: $label refused before manifest publication"
}

function Update-ArtifactBinding([string]$root, [string]$name) {
    $status = Get-Content -Raw -LiteralPath (Join-Path $root 'reset-status.json') | ConvertFrom-Json
    $status.artifactSha256.PSObject.Properties[$name].Value =
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $name)).Hash
    Write-Json (Join-Path $root 'reset-status.json') $status
}

try {
    $failed = New-PackageRoot 'FAILED'
    $null = New-Common $failed
    Complete-Status $failed (New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'NOT_STARTED' $false $false $false)
    & pwsh -NoProfile -File $validator -EvidenceDirectory $failed -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Minimal terminal failure package did not validate.' }
    Write-Host 'PASS: terminal failure package validates and writes a manifest'

    $resolvedUnowned = $null
    $sourceCapturedEarly = $null
    foreach ($earlyCase in @(
        @{ label='OFFLINE'; phase='OFFLINE_GATES_COMPLETE'; count=1; resolved=$false },
        @{ label='SOURCE'; phase='SOURCE_CAPTURE_COMPLETE'; count=2; resolved=$false },
        @{ label='PATH_FAILURE'; phase='SOURCE_CAPTURE_COMPLETE'; count=2; resolved=$true }
    )) {
        $early = New-PackageRoot $earlyCase.label
        $null = New-Common $early
        if ($earlyCase.count -ge 1) {
            Add-OfflineGateEvidence $early
            Write-Json (Join-Path $early 'phase-01.json') ([ordered]@{schema='RHEMA_DISPOSABLE_RESET_PHASE_V1';ordinal=1;phase='OFFLINE_GATES_COMPLETE';repositoryMigrationCount=1;latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline'})
        }
        if ($earlyCase.count -ge 2) { Write-Json (Join-Path $early 'phase-02.json') ([ordered]@{schema='RHEMA_DISPOSABLE_RESET_PHASE_V1';ordinal=2;phase='SOURCE_CAPTURE_COMPLETE'}) }
        if ($earlyCase.count -ge 2) { Add-EmptySourceCaptureEvidence $early }
        $earlyStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' $earlyCase.phase $false $false $false
        if ($earlyCase.resolved) {
            $earlyStatus.backupMediaId='4' * 32
            $earlyStatus.backupFileName="RhemaERP_DISPOSABLE_RESET_COPYONLY_$('4' * 32).bak"
            $earlyStatus.backupPathSha256='4' * 64
            $resolvedUnowned = $early
        }
        elseif ($earlyCase.phase -eq 'SOURCE_CAPTURE_COMPLETE') { $sourceCapturedEarly = $early }
        Complete-Status $early $earlyStatus
        & pwsh -NoProfile -File $validator -EvidenceDirectory $early -PackageKind DisposableReset -WriteManifest
        if ($LASTEXITCODE -ne 0) { throw "Valid early V2 state failed: $($earlyCase.label)" }
    }
    Write-Host 'PASS: V2 NOT_STARTED, OFFLINE, SOURCE_CAPTURE and resolved-path failure states validate truthfully'
    foreach($offlineArtifact in @('git-diff-check.log','commit-ancestry.txt','git-head-tree.txt',
        'reset-build.log','ef-no-pending-model.log','migration-discovery.log','repository-migration-history.txt')){
        $tamperRoot=New-PackageRoot ('OFFLINE_REQUIRED_'+($offlineArtifact -replace '[^A-Za-z0-9]','_'))
        Copy-Item -Path (Join-Path $sourceCapturedEarly '*') -Destination $tamperRoot
        Remove-Item -LiteralPath (Join-Path $tamperRoot $offlineArtifact) -Force
        $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json -AsHashtable
        Complete-Status $tamperRoot $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "completed offline phase missing $offlineArtifact"
    }
    Write-Host 'PASS: every completed offline-gate command/history artifact is mandatory and hash-bound'
    foreach ($schemaTamper in @(
        @{label='singleton-array'; value=[object[]]@('RHEMA_MIGRATION_HISTORY_V1')},
        @{label='multi-array'; value=[object[]]@('RHEMA_MIGRATION_HISTORY_V1','EXTRA')},
        @{label='boolean'; value=$true},
        @{label='number'; value=[long]1},
        @{label='object'; value=[pscustomobject]@{name='RHEMA_MIGRATION_HISTORY_V1'}},
        @{label='null'; value=$null},
        @{label='missing'; missing=$true}
    )) {
        $tamperRoot = New-PackageRoot ("RESET_HISTORY_SCHEMA_" + $schemaTamper.label)
        Copy-Item -Path (Join-Path $sourceCapturedEarly '*') -Destination $tamperRoot
        $tamperStatus = Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json') | ConvertFrom-Json
        if ($schemaTamper.ContainsKey('missing')) { $tamperStatus.PSObject.Properties.Remove('migrationHistoryEvidenceSchema') }
        else { $tamperStatus.migrationHistoryEvidenceSchema = $schemaTamper.value }
        Write-Json (Join-Path $tamperRoot 'reset-status.json') $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "reset migration-history schema $($schemaTamper.label)"
    }
    Write-Host 'PASS: reset migration-history schema requires one exact scalar JSON String'
    foreach ($historyTamper in @('missing','empty','missing-marker','wrong-count','wrong-state')) {
        $tamperRoot = New-PackageRoot ("SOURCE_HISTORY_" + $historyTamper)
        Copy-Item -Path (Join-Path $sourceCapturedEarly '*') -Destination $tamperRoot
        $historyPath = Join-Path $tamperRoot 'source-migration-history.txt'
        if ($historyTamper -eq 'missing') { Remove-Item -LiteralPath $historyPath -Force }
        elseif ($historyTamper -eq 'empty') { Clear-Content -LiteralPath $historyPath }
        elseif ($historyTamper -eq 'missing-marker') { '0|EMPTY|0|0|0' | Set-Content -Encoding utf8 -LiteralPath $historyPath }
        elseif ($historyTamper -eq 'wrong-count') { 'RHEMA_MIGRATION_HISTORY_V1|COUNT=1|STATE=POPULATED' | Set-Content -Encoding utf8 -LiteralPath $historyPath }
        else { 'RHEMA_MIGRATION_HISTORY_V1|COUNT=0|STATE=POPULATED' | Set-Content -Encoding utf8 -LiteralPath $historyPath }
        $tamperStatus = Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json') | ConvertFrom-Json
        if ($historyTamper -eq 'missing') { $tamperStatus.artifactSha256.PSObject.Properties.Remove('source-migration-history.txt') }
        else { $tamperStatus.artifactSha256.PSObject.Properties['source-migration-history.txt'].Value = (Get-FileHash -Algorithm SHA256 -LiteralPath $historyPath).Hash }
        Write-Json (Join-Path $tamperRoot 'reset-status.json') $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "zero-row source history $historyTamper"
    }
    Write-Host 'PASS: zero-row source history missing, empty and marker tampering is refused after re-manifesting'
    foreach ($property in @('backupHashMatchesVerified','backupMaterialStateReconciled')) {
        $contradiction = New-PackageRoot ("RESOLVED_UNOWNED_" + $property)
        Copy-Item -Path (Join-Path $resolvedUnowned '*') -Destination $contradiction
        $status = Get-Content -Raw -LiteralPath (Join-Path $contradiction 'reset-status.json') | ConvertFrom-Json
        $status.$property = if ($property -eq 'backupHashMatchesVerified') { $true } else { $false }
        Write-Json (Join-Path $contradiction 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $contradiction "resolved-unowned contradiction $property"
    }
    $ownedEmpty = New-PackageRoot 'OWNED_EMPTY'
    $null = New-Common $ownedEmpty
    Add-OfflineGateEvidence $ownedEmpty
    Write-Json (Join-Path $ownedEmpty 'phase-01.json') ([ordered]@{schema='RHEMA_DISPOSABLE_RESET_PHASE_V1';ordinal=1;phase='OFFLINE_GATES_COMPLETE';repositoryMigrationCount=1;latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline'})
    Write-Json (Join-Path $ownedEmpty 'phase-02.json') ([ordered]@{schema='RHEMA_DISPOSABLE_RESET_PHASE_V1';ordinal=2;phase='SOURCE_CAPTURE_COMPLETE'})
    Add-EmptySourceCaptureEvidence $ownedEmpty
    $ownedEmptyStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'SOURCE_CAPTURE_COMPLETE' $false $false $false
    $ownedEmptyStatus.backupMediaId='6' * 32
    $ownedEmptyStatus.backupFileName="RhemaERP_DISPOSABLE_RESET_COPYONLY_$('6' * 32).bak"
    $ownedEmptyStatus.backupPathSha256='6' * 64
    $ownedEmptyStatus.attemptOwnedBackup=$true
    Complete-Status $ownedEmpty $ownedEmptyStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $ownedEmpty -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Owned empty reservation failure did not validate.' }
    Write-Host 'PASS: owned zero-byte reservation is distinct from material backup creation and preservation'
    foreach ($property in @('backupCompleted','backupPreserved','backupVerified','verifyEvidencePresent',
        'backupHashMatchesVerified','backupMaterialStateReconciled')) {
        $contradiction = New-PackageRoot ("OWNED_EMPTY_" + $property)
        Copy-Item -Path (Join-Path $ownedEmpty '*') -Destination $contradiction
        $status = Get-Content -Raw -LiteralPath (Join-Path $contradiction 'reset-status.json') | ConvertFrom-Json
        $status.$property=if($property -eq 'backupMaterialStateReconciled'){$false}else{$true}
        Write-Json (Join-Path $contradiction 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $contradiction "owned-empty contradiction $property"
    }

    $legacy = New-PackageRoot 'APPROVED_LEGACY_22B'
    $null = New-Common $legacy
    $legacyCommit='22b27ab18a19a92fa6b1222c05add817574e74fe'; $legacyTree='13f1eb03a24af14ff23998de72e3ddac9992981d'
    Write-Json (Join-Path $legacy 'reviewed-git-state.json') ([ordered]@{reviewedCommit=$legacyCommit;reviewedTree=$legacyTree;executedCommit=$legacyCommit;executedTree=$legacyTree;repositoryClean=$true})
    Write-Json (Join-Path $legacy 'phase-01.json') ([ordered]@{schema='RHEMA_DISPOSABLE_RESET_PHASE_V1';ordinal=1;phase='OFFLINE_GATES_COMPLETE'})
    Write-Json (Join-Path $legacy 'phase-02.json') ([ordered]@{schema='RHEMA_DISPOSABLE_RESET_PHASE_V1';ordinal=2;phase='SOURCE_CAPTURE_COMPLETE'})
    $legacyMedia='cc918da6ac23465497e00ca210a4c2c6'; $legacyHash='7F07CD03EED8F178ED45208936C3CC8F6E9F8D1336FFB3BF371C076D8F343743'
    @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$legacyMedia",'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') | Set-Content -Encoding ascii -LiteralPath (Join-Path $legacy 'backup-create.txt')
    "$legacyHash  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $legacy 'backup-current.sha256')
    $legacyStatus=New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'SOURCE_CAPTURE_COMPLETE' $true $false $false
    foreach($name in @('evidenceSchema','backupIdentityVersion','migrationHistoryEvidenceSchema','backupFileName','backupPathSha256','attemptOwnedBackup')){$legacyStatus.Remove($name)}
    $legacyStatus.reviewedCommit=$legacyCommit;$legacyStatus.reviewedTree=$legacyTree;$legacyStatus.backupMediaId=$legacyMedia
    $legacyStatus.backupCompleted=$false;$legacyStatus.backupPreserved=$true;$legacyStatus.backupByteLength=453042176
    $legacyStatus.backupPhaseMarkerPublished=$false;$legacyStatus.backupMaterialStateReconciled=$true
    $legacyStatus.currentMaterialSha256=$legacyHash;$legacyStatus.backupSha256='';$legacyStatus.backupHashMatchesVerified=$false;$legacyStatus.verifyEvidencePresent=$false
    $legacyStatus.sourceFingerprint='446|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28'
    Complete-Status $legacy $legacyStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $legacy -PackageKind DisposableReset -WriteManifest
    if($LASTEXITCODE -ne 0){throw 'Exact approved historical 22b package did not validate.'}
    Write-Host 'PASS: exact documented 22b legacy failure package validates under its strict discriminator'
    foreach($legacyMutation in @('backupCompleted','backupVerified','backupMediaId','backupByteLength','currentMaterialSha256',
        'sourceFingerprint','sourceFingerprintMissing','backupMaterialStateReconciled','backupMaterialStateReconciledType')){
        $tamper=New-PackageRoot ("LEGACY_"+$legacyMutation);Copy-Item -Path (Join-Path $legacy '*') -Destination $tamper
        $status=Get-Content -Raw (Join-Path $tamper 'reset-status.json')|ConvertFrom-Json
        if($legacyMutation -in @('backupCompleted','backupVerified')){$status.$legacyMutation=$true}
        elseif($legacyMutation -eq 'backupMediaId'){$status.$legacyMutation='0'*32}
        elseif($legacyMutation -eq 'backupByteLength'){$status.$legacyMutation=1}
        elseif($legacyMutation -eq 'sourceFingerprint'){$status.$legacyMutation='445|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28'}
        elseif($legacyMutation -eq 'sourceFingerprintMissing'){$status.PSObject.Properties.Remove('sourceFingerprint')}
        elseif($legacyMutation -eq 'backupMaterialStateReconciled'){$status.$legacyMutation=$false}
        elseif($legacyMutation -eq 'backupMaterialStateReconciledType'){$status.backupMaterialStateReconciled='true'}
        else{$status.$legacyMutation='0'*64}
        Write-Json (Join-Path $tamper 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $tamper "exact legacy mutation $legacyMutation"
    }
    foreach ($property in @('backupMediaId','backupFileName','backupPathSha256')) {
        $mixed = New-PackageRoot ("MIXED_" + $property)
        Copy-Item -Path (Join-Path $failed '*') -Destination $mixed
        $mixedStatus = Get-Content -Raw -LiteralPath (Join-Path $mixed 'reset-status.json') | ConvertFrom-Json
        if ($property -eq 'backupMediaId') { $mixedStatus.$property = '5' * 32 }
        elseif ($property -eq 'backupFileName') { $mixedStatus.$property = "RhemaERP_DISPOSABLE_RESET_COPYONLY_$('5' * 32).bak" }
        else { $mixedStatus.$property = '5' * 64 }
        Write-Json (Join-Path $mixed 'reset-status.json') $mixedStatus
        Invoke-ExpectedRemanifestFailure $mixed "mixed unresolved V2 $property"
    }
    $allV2StateFields=@('backupMediaId','backupFileName','backupPathSha256','attemptOwnedBackup','backupCreated',
        'backupCompleted','backupPreserved','backupVerified','resetStarted','backupPhaseMarkerPublished',
        'backupHashMatchesVerified','verifyEvidencePresent','backupMaterialStateReconciled','backupByteLength',
        'currentMaterialSha256','backupSha256')
    foreach ($property in $allV2StateFields) {
        $nullState = New-PackageRoot ("NULL_" + $property)
        Copy-Item -Path (Join-Path $failed '*') -Destination $nullState
        $status = Get-Content -Raw -LiteralPath (Join-Path $nullState 'reset-status.json') | ConvertFrom-Json
        $status.$property=$null
        Write-Json (Join-Path $nullState 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $nullState "unresolved V2 null $property"
    }
    foreach ($property in $allV2StateFields) {
        $missingState=New-PackageRoot ("MISSING_"+$property);Copy-Item -Path (Join-Path $failed '*') -Destination $missingState
        $status=Get-Content -Raw (Join-Path $missingState 'reset-status.json')|ConvertFrom-Json
        $status.PSObject.Properties.Remove($property);Write-Json (Join-Path $missingState 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $missingState "unresolved V2 missing $property"
    }
    foreach($property in @('backupCreated','backupCompleted','backupPreserved','backupVerified','resetStarted',
        'backupPhaseMarkerPublished','backupHashMatchesVerified','verifyEvidencePresent','attemptOwnedBackup')){
        $trueState=New-PackageRoot ("TRUE_"+$property);Copy-Item -Path (Join-Path $failed '*') -Destination $trueState
        $status=Get-Content -Raw (Join-Path $trueState 'reset-status.json')|ConvertFrom-Json;$status.$property=$true
        Write-Json (Join-Path $trueState 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $trueState "unresolved V2 true $property"
    }
    $unreconciled=New-PackageRoot 'FALSE_backupMaterialStateReconciled';Copy-Item -Path (Join-Path $failed '*') -Destination $unreconciled
    $unreconciledStatus=Get-Content -Raw (Join-Path $unreconciled 'reset-status.json')|ConvertFrom-Json
    $unreconciledStatus.backupMaterialStateReconciled=$false
    Write-Json (Join-Path $unreconciled 'reset-status.json') $unreconciledStatus
    Invoke-ExpectedRemanifestFailure $unreconciled 'unresolved V2 false backupMaterialStateReconciled'
    foreach($case in @(@('backupCreated','false'),@('backupMaterialStateReconciled','true'),
        @('backupByteLength','0'),@('currentMaterialSha256',$false))){
        $typed=New-PackageRoot ("TYPE_"+$case[0]);Copy-Item -Path (Join-Path $failed '*') -Destination $typed
        $status=Get-Content -Raw (Join-Path $typed 'reset-status.json')|ConvertFrom-Json;$status.($case[0])=$case[1]
        Write-Json (Join-Path $typed 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $typed "V2 wrong JSON type $($case[0])"
    }

    $partialBackup = New-PackageRoot 'VERIFY_FAILURE'
    $null = New-Common $partialBackup
    Add-OfflineGateEvidence $partialBackup
    Add-EmptySourceCaptureEvidence $partialBackup
    $partialMedia = 'e' * 32
    $partialFileName = "RhemaERP_DISPOSABLE_RESET_COPYONLY_${partialMedia}.bak"
    @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$partialMedia",'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START',
        'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $partialBackup 'backup-create.txt')
    "$('E' * 64)  $partialFileName" | Set-Content -Encoding ascii -LiteralPath (Join-Path $partialBackup 'backup-current.sha256')
    foreach ($entry in @(@(1,'OFFLINE_GATES_COMPLETE'),@(2,'SOURCE_CAPTURE_COMPLETE'),@(3,'BACKUP_CREATED'))) {
        $marker = [ordered]@{ schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=$entry[0]; phase=$entry[1] }
        if ($entry[0] -eq 1) { $marker.repositoryMigrationCount=1; $marker.latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline' }
        if ($entry[0] -eq 2) { $marker.sourceHistorySha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $partialBackup 'source-migration-history.txt')).Hash }
        if ($entry[0] -eq 3) {
            $marker.database='RhemaERP'; $marker.backupMediaId=$partialMedia; $marker.backupFileName=$partialFileName; $marker.backupPathSha256=('E' * 64)
            $marker.backupCompleted=$true; $marker.backupByteLength=1024; $marker.currentMaterialSha256=('E' * 64)
        }
        Write-Json (Join-Path $partialBackup ("phase-{0:D2}.json" -f $entry[0])) $marker
    }
    $partialStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'BACKUP_CREATED' $true $false $false
    $partialStatus.backupMediaId=$partialMedia; $partialStatus.backupFileName=$partialFileName; $partialStatus.backupPathSha256=('E' * 64); $partialStatus.backupCompleted=$true; $partialStatus.backupPreserved=$true
    $partialStatus.currentMaterialSha256=('E' * 64); $partialStatus.backupSha256=''
    Complete-Status $partialBackup $partialStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $partialBackup -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Completed backup/VERIFYONLY-failed recovery package did not validate.' }
    Write-Host 'PASS: completed backup remains preserved but unverified when VERIFYONLY did not complete'
    $partialTamper = Get-Content -Raw -LiteralPath (Join-Path $partialBackup 'reset-status.json') | ConvertFrom-Json
    $partialTamper.backupCreated=$false
    Write-Json (Join-Path $partialBackup 'reset-status.json') $partialTamper
    Invoke-ExpectedFailure $partialBackup 'backup-created marker downgraded after partial failure'

    $markerFailure = New-PackageRoot 'MARKER_FAILURE'
    $null = New-Common $markerFailure
    Add-OfflineGateEvidence $markerFailure
    Add-EmptySourceCaptureEvidence $markerFailure
    foreach ($entry in @(@(1,'OFFLINE_GATES_COMPLETE'),@(2,'SOURCE_CAPTURE_COMPLETE'))) {
        $marker=[ordered]@{schema='RHEMA_DISPOSABLE_RESET_PHASE_V1';ordinal=$entry[0];phase=$entry[1]}
        if($entry[0] -eq 1){$marker.repositoryMigrationCount=1;$marker.latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline'}
        if($entry[0] -eq 2){$marker.sourceHistorySha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $markerFailure 'source-migration-history.txt')).Hash}
        Write-Json (Join-Path $markerFailure ("phase-{0:D2}.json" -f $entry[0])) $marker
    }
    $markerMedia = 'f' * 32
    $markerFileName = "RhemaERP_DISPOSABLE_RESET_COPYONLY_${markerMedia}.bak"
    $collapsedMarkerFailureEvidence = @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$markerMedia",
        'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START',
        "Processed 55296 pages for database 'RhemaERP', file 'RhemaERP' on file 1.",
        'BACKUP DATABASE successfully processed 55298 pages in 3.141 seconds.',
        'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') -join ' '
    $collapsedMarkerFailureEvidence |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $markerFailure 'backup-create.txt')
    "$('F' * 64)  $markerFileName" | Set-Content -Encoding ascii -LiteralPath (Join-Path $markerFailure 'backup-current.sha256')
    $markerStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'SOURCE_CAPTURE_COMPLETE' $true $false $false
    $markerStatus.backupPhaseMarkerPublished=$false; $markerStatus.backupMaterialStateReconciled=$true
    $markerStatus.backupByteLength=4096; $markerStatus.backupMediaId=$markerMedia; $markerStatus.backupFileName=$markerFileName; $markerStatus.backupPathSha256=('F' * 64); $markerStatus.backupSha256=''
    $markerStatus.currentMaterialSha256=('F' * 64)
    $markerStatus.backupCompleted=$true; $markerStatus.backupPreserved=$true; $markerStatus.failedOperation='PHASE_03_PUBLICATION'
    Complete-Status $markerFailure $markerStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $markerFailure -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Material-backup/phase-03-publication-failure package did not validate.' }
    Write-Host 'PASS: material backup remains truthfully preserved when phase-03 publication fails'

    foreach ($requiredBackupMarker in @('BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE')) {
        $markerTamper = New-PackageRoot ("MARKER_FAILURE_MISSING_" + $requiredBackupMarker)
        Copy-Item -Path (Join-Path $markerFailure '*') -Destination $markerTamper
        [regex]::Replace((Get-Content -Raw -LiteralPath (Join-Path $markerTamper 'backup-create.txt')),
            '(?<!\S)' + [regex]::Escape($requiredBackupMarker) + '(?!\S)', '').Trim() |
            Set-Content -Encoding ascii -LiteralPath (Join-Path $markerTamper 'backup-create.txt')
        Update-ArtifactBinding $markerTamper 'backup-create.txt'
        Invoke-ExpectedSemanticFailure $markerTamper "phase-03-publication-failure backupCompleted without $requiredBackupMarker"
    }

    $durableTamper = New-PackageRoot 'RECOVERY_DURABLE_TAMPER'
    Copy-Item -Path (Join-Path $markerFailure '*') -Destination $durableTamper
    (Get-Content -Raw -LiteralPath (Join-Path $durableTamper 'RECOVERY.md')).Replace(
        'Last durable phase: SOURCE_CAPTURE_COMPLETE','Last durable phase: BACKUP_CREATED') |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $durableTamper 'RECOVERY.md')
    Update-ArtifactBinding $durableTamper 'RECOVERY.md'
    Invoke-ExpectedFailure $durableTamper 'recovery last-durable-phase mismatch'

    $operationTamper = New-PackageRoot 'RECOVERY_OPERATION_TAMPER'
    Copy-Item -Path (Join-Path $markerFailure '*') -Destination $operationTamper
    (Get-Content -Raw -LiteralPath (Join-Path $operationTamper 'RECOVERY.md')).Replace(
        'Failed operation: PHASE_03_PUBLICATION','Failed operation: VERIFYONLY') |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $operationTamper 'RECOVERY.md')
    Update-ArtifactBinding $operationTamper 'RECOVERY.md'
    Invoke-ExpectedFailure $operationTamper 'recovery failed-operation mismatch'

    $postVerifyMutation = New-PackageRoot 'POST_VERIFY_MUTATION'
    $null = New-Common $postVerifyMutation
    Add-OfflineGateEvidence $postVerifyMutation
    Add-EmptySourceCaptureEvidence $postVerifyMutation
    $mutatedMedia = '1' * 32
    $mutatedFileName = "RhemaERP_DISPOSABLE_RESET_COPYONLY_${mutatedMedia}.bak"
    $verifiedHash = 'A' * 64
    $currentHash = 'B' * 64
    @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$mutatedMedia",'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START',
        'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') | Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup-create.txt')
    (@('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$mutatedMedia",'The backup set on file 1 is valid.',
        'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE') -join ' ') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup-verify.txt')
    "$verifiedHash  $mutatedFileName" | Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup.sha256')
    "$currentHash  $mutatedFileName" | Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup-current.sha256')
    foreach ($entry in @(@(1,'OFFLINE_GATES_COMPLETE'),@(2,'SOURCE_CAPTURE_COMPLETE'),@(3,'BACKUP_CREATED'),@(4,'BACKUP_VERIFIED'))) {
        $marker = [ordered]@{ schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=$entry[0]; phase=$entry[1] }
        if ($entry[0] -eq 1) { $marker.repositoryMigrationCount=1; $marker.latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline' }
        if ($entry[0] -eq 2) { $marker.sourceHistorySha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $postVerifyMutation 'source-migration-history.txt')).Hash }
        if ($entry[0] -eq 3) {
            $marker.database='RhemaERP'; $marker.backupMediaId=$mutatedMedia; $marker.backupFileName=$mutatedFileName; $marker.backupPathSha256=('1' * 64)
            $marker.backupCompleted=$true; $marker.backupByteLength=2048; $marker.currentMaterialSha256=$verifiedHash
        }
        if ($entry[0] -eq 4) {
            $marker.backupSha256=$verifiedHash; $marker.backupMediaId=$mutatedMedia; $marker.backupFileName=$mutatedFileName; $marker.backupPathSha256=('1' * 64)
        }
        Write-Json (Join-Path $postVerifyMutation ("phase-{0:D2}.json" -f $entry[0])) $marker
    }
    $mutationStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'BACKUP_VERIFIED' $true $false $false
    $mutationStatus.backupMediaId=$mutatedMedia; $mutationStatus.backupFileName=$mutatedFileName; $mutationStatus.backupPathSha256=('1' * 64); $mutationStatus.backupSha256=$verifiedHash
    $mutationStatus.currentMaterialSha256=$currentHash; $mutationStatus.backupHashMatchesVerified=$false
    $mutationStatus.verifyEvidencePresent=$true; $mutationStatus.backupCompleted=$true; $mutationStatus.backupPreserved=$true
    $mutationStatus.failedOperation='PRE_MUTATION_HASH_RECHECK'
    Complete-Status $postVerifyMutation $mutationStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $postVerifyMutation -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Post-VERIFY backup-mutation recovery package did not validate truthfully.' }
    Write-Host 'PASS: post-VERIFY backup mutation preserves original verified hash and reports current bytes unverified'
    foreach ($case in @('wrong media','wrong filename','phase-03 filename','current-hash filename')) {
        $identityTamper = New-PackageRoot ("BACKUP_IDENTITY_" + $case.Replace(' ','_'))
        Copy-Item -Path (Join-Path $postVerifyMutation '*') -Destination $identityTamper
        if ($case -eq 'wrong media') {
            $status = Get-Content -Raw -LiteralPath (Join-Path $identityTamper 'reset-status.json') | ConvertFrom-Json
            $status.backupMediaId = '2' * 32
            Write-Json (Join-Path $identityTamper 'reset-status.json') $status
        }
        elseif ($case -eq 'wrong filename') {
            $status = Get-Content -Raw -LiteralPath (Join-Path $identityTamper 'reset-status.json') | ConvertFrom-Json
            $status.backupFileName = '..\spoof.bak'
            Write-Json (Join-Path $identityTamper 'reset-status.json') $status
        }
        elseif ($case -eq 'phase-03 filename') {
            $phaseThree = Get-Content -Raw -LiteralPath (Join-Path $identityTamper 'phase-03.json') | ConvertFrom-Json
            $phaseThree.backupFileName = "RhemaERP_DISPOSABLE_RESET_COPYONLY_$('3' * 32).bak"
            Write-Json (Join-Path $identityTamper 'phase-03.json') $phaseThree
            Update-ArtifactBinding $identityTamper 'phase-03.json'
        }
        else {
            "$currentHash  spoof.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $identityTamper 'backup-current.sha256')
            Update-ArtifactBinding $identityTamper 'backup-current.sha256'
        }
        Invoke-ExpectedRemanifestFailure $identityTamper "media-bound backup $case tamper"
    }
    foreach ($deletedProperty in @('evidenceSchema','backupIdentityVersion','backupFileName','backupPathSha256')) {
        $downgradeTamper = New-PackageRoot ("IDENTITY_DELETE_" + $deletedProperty)
        Copy-Item -Path (Join-Path $postVerifyMutation '*') -Destination $downgradeTamper
        $status = Get-Content -Raw -LiteralPath (Join-Path $downgradeTamper 'reset-status.json') | ConvertFrom-Json
        $status.PSObject.Properties.Remove($deletedProperty)
        Write-Json (Join-Path $downgradeTamper 'reset-status.json') $status
        Invoke-ExpectedRemanifestFailure $downgradeTamper "V2 identity field deletion $deletedProperty"
    }
    $falseClaim = Get-Content -Raw -LiteralPath (Join-Path $postVerifyMutation 'reset-status.json') | ConvertFrom-Json
    $falseClaim.backupVerified=$true; $falseClaim.backupHashMatchesVerified=$true
    (Get-Content -Raw -LiteralPath (Join-Path $postVerifyMutation 'RECOVERY.md')).Replace(
        'Verified backup available: false','Verified backup available: true') |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $postVerifyMutation 'RECOVERY.md')
    $falseClaim.artifactSha256.PSObject.Properties['RECOVERY.md'].Value =
        (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $postVerifyMutation 'RECOVERY.md')).Hash
    Write-Json (Join-Path $postVerifyMutation 'reset-status.json') $falseClaim
    Invoke-ExpectedFailure $postVerifyMutation 'post-VERIFY mutated bytes falsely claimed verified'

    Add-Content -Encoding utf8 -LiteralPath (Join-Path $failed 'RECOVERY.md') -Value 'tamper'
    Invoke-ExpectedFailure $failed 'content tamper'

    $missingBinding = New-PackageRoot 'MISSING_BINDING'
    $null = New-Common $missingBinding
    Complete-Status $missingBinding (New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'NOT_STARTED' $false $false $false)
    $statusObject = Get-Content -Raw -LiteralPath (Join-Path $missingBinding 'reset-status.json') | ConvertFrom-Json
    $statusObject.artifactSha256.PSObject.Properties.Remove('RECOVERY.md')
    Write-Json (Join-Path $missingBinding 'reset-status.json') $statusObject
    Invoke-ExpectedFailure $missingBinding 'missing artifact binding'

    $phaseMismatch = New-PackageRoot 'PHASE_MISMATCH'
    $null = New-Common $phaseMismatch
    Write-Json (Join-Path $phaseMismatch 'phase-01.json') ([ordered]@{
        schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=1; phase='OFFLINE_GATES_COMPLETE'; repositoryMigrationCount=1
        latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline'
    })
    Complete-Status $phaseMismatch (New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'SOURCE_CAPTURE_COMPLETE' $false $false $false)
    Invoke-ExpectedFailure $phaseMismatch 'terminal status/final durable phase mismatch'

    $sanitization = New-PackageRoot 'SANITIZATION'
    $null = New-Common $sanitization
    'Server=RHEMA-AKWASI;Password=secret' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $sanitization 'leak.sql')
    Complete-Status $sanitization (New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'NOT_STARTED' $false $false $false)
    $output = & pwsh -NoProfile -File $validator -EvidenceDirectory $sanitization -PackageKind DisposableReset -WriteManifest 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw 'Sanitization tamper unexpectedly passed.' }
    Write-Host 'PASS: .sql machine/secret evidence is refused'

    $pass = New-PackageRoot 'PASS'
    $reviewed = New-Common $pass
    $native = 'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND='
    @($reviewed.executedCommit,$reviewed.executedTree,"${native}git") | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'git-head-tree.txt')
    @("$($reviewed.executedCommit) $('c' * 40)","${native}git") | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'commit-ancestry.txt')
    "${native}git" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'git-diff-check.log')
    foreach ($name in @('reset-build.log','ef-no-pending-model.log')) {
        "${native}dotnet" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass $name)
    }
    $repositoryIds = @('20260913162402_DisposableDevelopmentCurrentModelBaseline')
    @($repositoryIds + "${native}dotnet") | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'migration-discovery.log')
    Write-HistoryFixture $pass 'repository-migration-history.txt' $repositoryIds
    Write-HistoryFixture $pass 'source-migration-history.txt' @()
    $fingerprint = '0|EMPTY|0|0|0'
    $fingerprint | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'source-fingerprint-before.txt')
    "RHEMAERP_DATABASE_IDENTITY_SHA256=$('A' * 64)" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'source-database-identity.sha256')
    "LOCAL_SQL_INSTANCE_IDENTITY_SHA256=$('B' * 64)" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'source-server-identity.sha256')
    Write-Json (Join-Path $pass 'pre-mutation-reviewed-git-state.json') $reviewed
    $mediaId = 'c' * 32
    $passBackupFileName = "RhemaERP_DISPOSABLE_RESET_COPYONLY_${mediaId}.bak"
    @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$mediaId",'BACKUP_PATH_ATOMICALLY_RESERVED',
        'BACKUP_COPY_ONLY_CHECKSUM_START','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup-create.txt')
    (@('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$mediaId",'The backup set on file 1 is valid.',
        'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE') -join ' ') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup-verify.txt')
    "$('D' * 64)  $passBackupFileName" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup.sha256')
    "$('D' * 64)  $passBackupFileName" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup-current.sha256')
    @('DISPOSABLE_RESET_SOURCE_QUIESCED','DISPOSABLE_RESET_FINAL_SOURCE_RECHECK_COMPLETE',
        "SOURCE_FINAL_FINGERPRINT=$fingerprint",'DISPOSABLE_RESET_EMPTY_DATABASE_RECREATED') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'reset-database.log')
    foreach ($name in @('reset-apply-migrations.log','reset-seed-pass-1.log','reset-seed-pass-2.log')) {
        "${native}dotnet" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass $name)
    }
    Write-HistoryFixture $pass 'target-migration-history.txt' $repositoryIds
    'CANONICAL_FIXED_HASH_ROWS' | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'reset-invariants-pass-1.txt')
    Copy-Item -LiteralPath (Join-Path $pass 'reset-invariants-pass-1.txt') -Destination (Join-Path $pass 'reset-invariants-pass-2.txt')
    $invariantHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $pass 'reset-invariants-pass-1.txt')).Hash
    @("$invariantHash  reset-invariants-pass-1.txt","$invariantHash  reset-invariants-pass-2.txt") |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'reset-invariants.sha256')
    'DBCC_CHECKDB_COMPLETE' | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'reset-dbcc.txt')
    $phaseNames = @('OFFLINE_GATES_COMPLETE','SOURCE_CAPTURE_COMPLETE','BACKUP_CREATED','BACKUP_VERIFIED',
        'RESET_STARTED','DATABASE_RECREATED','MIGRATIONS_APPLIED','SEED_INVARIANTS_VERIFIED','DBCC_COMPLETE','COMPLETE')
    for ($index=0; $index -lt $phaseNames.Count; $index++) {
        $phaseMarker = [ordered]@{ schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=($index + 1); phase=$phaseNames[$index] }
        if (($index + 1) -eq 1) {
            $phaseMarker.repositoryMigrationCount=1
            $phaseMarker.latestMigration='20260913162402_DisposableDevelopmentCurrentModelBaseline'
        }
        if (($index + 1) -eq 3) {
            $phaseMarker.database='RhemaERP'; $phaseMarker.backupMediaId=$mediaId; $phaseMarker.backupFileName=$passBackupFileName; $phaseMarker.backupPathSha256=('C' * 64)
            $phaseMarker.backupCompleted=$true; $phaseMarker.backupByteLength=1024
            $phaseMarker.currentMaterialSha256=('D' * 64)
        }
        if (($index + 1) -eq 2) {
            $phaseMarker.sourceHistorySha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $pass 'source-migration-history.txt')).Hash
        }
        if (($index + 1) -eq 4) {
            $phaseMarker.backupSha256=('D' * 64); $phaseMarker.backupMediaId=$mediaId; $phaseMarker.backupFileName=$passBackupFileName; $phaseMarker.backupPathSha256=('C' * 64)
        }
        if (($index + 1) -eq 7) { $phaseMarker.finalMigrationCount=1 }
        Write-Json (Join-Path $pass ("phase-{0:D2}.json" -f ($index + 1))) $phaseMarker
    }
    $passStatus = New-Status 'PASS' 'COMPLETE' $true $true $true
    $passStatus.backupMediaId=$mediaId; $passStatus.backupFileName=$passBackupFileName; $passStatus.backupPathSha256=('C' * 64); $passStatus.backupSha256=('D' * 64)
    $passStatus.invariantSha256=$invariantHash; $passStatus.sourceFingerprint=$fingerprint; $passStatus.backupCompleted=$true
    $passStatus.currentMaterialSha256=('D' * 64); $passStatus.backupHashMatchesVerified=$true
    $passStatus.verifyEvidencePresent=$true; $passStatus.backupPreserved=$true
    Complete-Status $pass $passStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $pass -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Complete disposable-reset PASS package did not validate.' }
    Write-Host 'PASS: complete disposable-development baseline reset package validates and writes a manifest'

    foreach($completedPhaseArtifact in @('reset-seed-pass-1.log','reset-seed-pass-2.log',
        'reset-invariants-pass-1.txt','reset-invariants-pass-2.txt','reset-invariants.sha256','reset-dbcc.txt')){
        $tamperRoot=New-PackageRoot ('COMPLETED_PHASE_'+($completedPhaseArtifact -replace '[^A-Za-z0-9]','_'))
        Copy-Item -Path (Join-Path $pass '*') -Destination $tamperRoot
        Remove-Item -LiteralPath (Join-Path $tamperRoot $completedPhaseArtifact) -Force
        $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json -AsHashtable
        Complete-Status $tamperRoot $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "completed phase missing $completedPhaseArtifact"
    }
    Write-Host 'PASS: completed seed/invariant/DBCC evidence is mandatory and hash-bound'

    foreach ($migrationTamper in @(
        @{label='repository count';property='repositoryMigrationCount';value=2},
        @{label='latest migration';property='latestMigration';value='20260913162403_Unreviewed'},
        @{label='terminal final count';property='finalMigrationCount';value=0},
        @{label='orphan count';property='orphanMigrationCount';value=1}
    )) {
        $tamperRoot=New-PackageRoot ('MIGRATION_'+$migrationTamper.property)
        Copy-Item -Path (Join-Path $pass '*') -Destination $tamperRoot
        $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json') | ConvertFrom-Json -AsHashtable
        $tamperStatus[$migrationTamper.property]=$migrationTamper.value
        Complete-Status $tamperRoot $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "baseline $($migrationTamper.label) mismatch"
    }

    $identityDriftFailure=New-PackageRoot 'RESET_STARTED_IDENTITY_DRIFT'
    Copy-Item -Path (Join-Path $pass '*') -Destination $identityDriftFailure
    foreach($ordinal in 6..10){
        $phasePath=Join-Path $identityDriftFailure ("phase-{0:D2}.json" -f $ordinal)
        if(Test-Path -LiteralPath $phasePath){Remove-Item -LiteralPath $phasePath -Force}
    }
    foreach($lateArtifact in @('target-migration-history.txt','reset-apply-migrations.log','reset-seed-pass-1.log',
        'reset-seed-pass-2.log','reset-invariants-pass-1.txt','reset-invariants-pass-2.txt',
        'reset-invariants.sha256','reset-dbcc.txt')){
        $latePath=Join-Path $identityDriftFailure $lateArtifact
        if(Test-Path -LiteralPath $latePath){Remove-Item -LiteralPath $latePath -Force}
    }
    'DISPOSABLE_RESET_IDENTITY_DRIFT' |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $identityDriftFailure 'reset-database.log')
    $identityDriftStatus=Get-Content -Raw -LiteralPath (Join-Path $identityDriftFailure 'reset-status.json')|ConvertFrom-Json -AsHashtable
    $identityDriftStatus.status='FAILED_NO_AUTOMATIC_RETRY';$identityDriftStatus.phase='RESET_STARTED'
    $identityDriftStatus.failedOperation='RESET_STARTED';$identityDriftStatus.finalMigrationCount=0
    Complete-Status $identityDriftFailure $identityDriftStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $identityDriftFailure -PackageKind DisposableReset -WriteManifest
    if($LASTEXITCODE -ne 0){throw 'Truthful pre-final-fingerprint identity-drift package was rejected.'}
    Write-Host 'PASS: standalone exact RESET_STARTED identity drift may omit the not-yet-produced final fingerprint token'

    $coalescedIdentityDrift=New-PackageRoot 'RESET_STARTED_IDENTITY_DRIFT_COALESCED'
    Copy-Item -Path (Join-Path $identityDriftFailure '*') -Destination $coalescedIdentityDrift
    'Sqlcmd: Error: DISPOSABLE_RESET_IDENTITY_DRIFT transport-stopped' |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $coalescedIdentityDrift 'reset-database.log')
    $coalescedStatus=Get-Content -Raw -LiteralPath (Join-Path $coalescedIdentityDrift 'reset-status.json')|ConvertFrom-Json -AsHashtable
    Complete-Status $coalescedIdentityDrift $coalescedStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $coalescedIdentityDrift -PackageKind DisposableReset -WriteManifest
    if($LASTEXITCODE -ne 0){throw 'Truthful coalesced pre-final-fingerprint identity-drift package was rejected.'}
    Write-Host 'PASS: coalesced transport accepts one exact case-sensitive early-failure token'

    foreach($signalTamper in @(
        @{label='prefix';log='PREFIX_DISPOSABLE_RESET_IDENTITY_DRIFT'},
        @{label='suffix';log='DISPOSABLE_RESET_IDENTITY_DRIFT_SUFFIX'},
        @{label='hyphen';log='DISPOSABLE-RESET-IDENTITY-DRIFT'},
        @{label='duplicate';log='DISPOSABLE_RESET_IDENTITY_DRIFT DISPOSABLE_RESET_IDENTITY_DRIFT'},
        @{label='mixed';log='DISPOSABLE_RESET_IDENTITY_DRIFT DISPOSABLE_RESET_FINAL_HISTORY_DRIFT'},
        @{label='case mutation';log='disposable_reset_identity_drift'}
    )){
        $tamperRoot=New-PackageRoot ('EARLY_SIGNAL_'+($signalTamper.label -replace ' ','_'))
        Copy-Item -Path (Join-Path $identityDriftFailure '*') -Destination $tamperRoot
        $signalTamper.log|Set-Content -Encoding ascii -LiteralPath (Join-Path $tamperRoot 'reset-database.log')
        Update-ArtifactBinding $tamperRoot 'reset-database.log'
        Invoke-ExpectedRemanifestFailure $tamperRoot "early failure signal $($signalTamper.label)"
    }

    foreach($earlyFailureTamper in @(
        @{label='later failed operation';operation='PHASE_07_PUBLICATION';log='Sqlcmd: Error: DISPOSABLE_RESET_IDENTITY_DRIFT'},
        @{label='claimed final recheck completion';operation='RESET_STARTED';log='DISPOSABLE_RESET_FINAL_SOURCE_RECHECK_COMPLETE Sqlcmd: Error: DISPOSABLE_RESET_IDENTITY_DRIFT'}
    )){
        $tamperRoot=New-PackageRoot ('EARLY_BOUNDARY_'+($earlyFailureTamper.label -replace ' ','_'))
        Copy-Item -Path (Join-Path $identityDriftFailure '*') -Destination $tamperRoot
        $earlyFailureTamper.log|Set-Content -Encoding ascii -LiteralPath (Join-Path $tamperRoot 'reset-database.log')
        $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json -AsHashtable
        $tamperStatus.failedOperation=$earlyFailureTamper.operation
        Complete-Status $tamperRoot $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "missing final fingerprint with $($earlyFailureTamper.label)"
    }

    $phaseSevenFailure=New-PackageRoot 'PHASE_07_PUBLICATION_FAILURE'
    Copy-Item -Path (Join-Path $pass '*') -Destination $phaseSevenFailure
    "SQLCMD_COALESCED SOURCE_FINAL_FINGERPRINT=$fingerprint PHASE_07_PUBLICATION_FAILED" |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $phaseSevenFailure 'reset-database.log')
    foreach($ordinal in 7..10){
        $phasePath=Join-Path $phaseSevenFailure ("phase-{0:D2}.json" -f $ordinal)
        if(Test-Path -LiteralPath $phasePath){Remove-Item -LiteralPath $phasePath -Force}
    }
    $phaseSevenStatus=Get-Content -Raw -LiteralPath (Join-Path $phaseSevenFailure 'reset-status.json')|ConvertFrom-Json -AsHashtable
    $phaseSevenStatus.status='FAILED_NO_AUTOMATIC_RETRY';$phaseSevenStatus.phase='DATABASE_RECREATED'
    $phaseSevenStatus.failedOperation='PHASE_07_PUBLICATION';$phaseSevenStatus.finalMigrationCount=1
    Complete-Status $phaseSevenFailure $phaseSevenStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $phaseSevenFailure -PackageKind DisposableReset -WriteManifest
    if($LASTEXITCODE -ne 0){throw 'Valid phase-07-publication-failure package was rejected.'}
    Write-Host 'PASS: failed phase-07 publication package binds validated applied target history'

    foreach($phaseSevenTamper in @(
        @{label='final count downgrade';kind='status';property='finalMigrationCount';value=0},
        @{label='same-count target identity';kind='target';property='';value='20260913162403_Unreviewed'},
        @{label='missing validated target history';kind='missing';property='';value=''}
    )){
        $tamperRoot=New-PackageRoot ('PHASE_07_'+$phaseSevenTamper.kind)
        Copy-Item -Path (Join-Path $phaseSevenFailure '*') -Destination $tamperRoot
        if($phaseSevenTamper.kind -eq 'status'){
            $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json -AsHashtable
            $tamperStatus[$phaseSevenTamper.property]=$phaseSevenTamper.value
            Complete-Status $tamperRoot $tamperStatus
        }elseif($phaseSevenTamper.kind -eq 'target'){
            Write-HistoryFixture $tamperRoot 'target-migration-history.txt' @($phaseSevenTamper.value)
            Update-ArtifactBinding $tamperRoot 'target-migration-history.txt'
        }else{
            Remove-Item -LiteralPath (Join-Path $tamperRoot 'target-migration-history.txt') -Force
            $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json
            $tamperStatus.artifactSha256.PSObject.Properties.Remove('target-migration-history.txt')
            Write-Json (Join-Path $tamperRoot 'reset-status.json') $tamperStatus
        }
        Invoke-ExpectedRemanifestFailure $tamperRoot "phase-07 publication $($phaseSevenTamper.label)"
    }

    $applyMigrationFailure=New-PackageRoot 'APPLY_MIGRATIONS_NEGATIVE_EXIT'
    Copy-Item -Path (Join-Path $pass '*') -Destination $applyMigrationFailure
    foreach($ordinal in 7..10){
        $phasePath=Join-Path $applyMigrationFailure ("phase-{0:D2}.json" -f $ordinal)
        if(Test-Path -LiteralPath $phasePath){Remove-Item -LiteralPath $phasePath -Force}
    }
    foreach($lateArtifact in @('target-migration-history.txt','reset-seed-pass-1.log','reset-seed-pass-2.log',
        'reset-invariants-pass-1.txt','reset-invariants-pass-2.txt','reset-invariants.sha256','reset-dbcc.txt')){
        $latePath=Join-Path $applyMigrationFailure $lateArtifact
        if(Test-Path -LiteralPath $latePath){Remove-Item -LiteralPath $latePath -Force}
    }
    @('Sanitized .NET migration failure: SQL error 102 near THROW.',
        'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=-532462766|COMMAND=dotnet') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $applyMigrationFailure 'reset-apply-migrations.log')
    $applyFailureStatus=Get-Content -Raw -LiteralPath (Join-Path $applyMigrationFailure 'reset-status.json')|ConvertFrom-Json -AsHashtable
    $applyFailureStatus.status='FAILED_NO_AUTOMATIC_RETRY';$applyFailureStatus.phase='DATABASE_RECREATED'
    $applyFailureStatus.failedOperation='APPLY_MIGRATIONS';$applyFailureStatus.finalMigrationCount=0
    Complete-Status $applyMigrationFailure $applyFailureStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $applyMigrationFailure -PackageKind DisposableReset -WriteManifest
    if($LASTEXITCODE -ne 0){throw 'Truthful APPLY_MIGRATIONS failure with signed negative Int32 exit was rejected.'}
    if(Test-Path -LiteralPath (Join-Path $applyMigrationFailure 'target-migration-history.txt')){
        throw 'APPLY_MIGRATIONS failure fixture unexpectedly retained target history.'
    }
    Write-Host 'PASS: phase-06 APPLY_MIGRATIONS failure accepts exact signed negative Int32 native exit and no target history'

    foreach($nativeExitTamper in @(
        @{label='zero-as-failure';marker='RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=0|COMMAND=dotnet'},
        @{label='negative-zero';marker='RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=-0|COMMAND=dotnet'},
        @{label='nonnumeric';marker='RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=FAILED|COMMAND=dotnet'},
        @{label='positive-overflow';marker='RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=2147483648|COMMAND=dotnet'},
        @{label='negative-overflow';marker='RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=-2147483649|COMMAND=dotnet'},
        @{label='success-downgrade';marker='RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=dotnet'},
        @{label='duplicate';marker="RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=-532462766|COMMAND=dotnet`nRHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=FAILURE|EXIT_CODE=-532462766|COMMAND=dotnet"}
    )){
        $tamperRoot=New-PackageRoot ('NATIVE_EXIT_'+($nativeExitTamper.label -replace '-','_'))
        Copy-Item -Path (Join-Path $applyMigrationFailure '*') -Destination $tamperRoot
        @('Sanitized migration failure.',[string]$nativeExitTamper.marker) |
            Set-Content -Encoding ascii -LiteralPath (Join-Path $tamperRoot 'reset-apply-migrations.log')
        Update-ArtifactBinding $tamperRoot 'reset-apply-migrations.log'
        Invoke-ExpectedRemanifestFailure $tamperRoot "native failure exit $($nativeExitTamper.label)"
    }
    Write-Host 'PASS: zero, nonnumeric, overflow, success-downgrade and duplicate failure markers are refused'

    $postSuccessHistoryFailure=New-PackageRoot 'POST_SUCCESS_HISTORY_CAPTURE_FAILURE'
    Copy-Item -Path (Join-Path $applyMigrationFailure '*') -Destination $postSuccessHistoryFailure
    'RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=SUCCESS|EXIT_CODE=0|COMMAND=dotnet' |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $postSuccessHistoryFailure 'reset-apply-migrations.log')
    $postSuccessStatus=Get-Content -Raw -LiteralPath (Join-Path $postSuccessHistoryFailure 'reset-status.json')|ConvertFrom-Json -AsHashtable
    $postSuccessStatus.failedOperation='CAPTURE_TARGET_MIGRATION_HISTORY';$postSuccessStatus.finalMigrationCount=0
    Complete-Status $postSuccessHistoryFailure $postSuccessStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $postSuccessHistoryFailure -PackageKind DisposableReset -WriteManifest
    if($LASTEXITCODE -ne 0){throw 'Truthful post-success target-history capture failure was rejected.'}
    Write-Host 'PASS: successful migration command followed by target-history capture failure remains distinguishable'

    foreach($applyBindingTamper in @('missing-log','missing-hash')){
        $tamperRoot=New-PackageRoot ('POST_SUCCESS_'+($applyBindingTamper -replace '-','_'))
        Copy-Item -Path (Join-Path $postSuccessHistoryFailure '*') -Destination $tamperRoot
        if($applyBindingTamper -eq 'missing-log'){
            Remove-Item -LiteralPath (Join-Path $tamperRoot 'reset-apply-migrations.log') -Force
            $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json -AsHashtable
            Complete-Status $tamperRoot $tamperStatus
        }else{
            $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json
            $tamperStatus.artifactSha256.PSObject.Properties.Remove('reset-apply-migrations.log')
            Write-Json (Join-Path $tamperRoot 'reset-status.json') $tamperStatus
        }
        Invoke-ExpectedRemanifestFailure $tamperRoot "post-success migration evidence $applyBindingTamper"
    }

    foreach($operationDowngrade in @(
        @{label='success-log-as-native-failure';source=$postSuccessHistoryFailure;operation='APPLY_MIGRATIONS'},
        @{label='failure-log-as-post-success';source=$applyMigrationFailure;operation='CAPTURE_TARGET_MIGRATION_HISTORY'}
    )){
        $tamperRoot=New-PackageRoot ('MIGRATION_OPERATION_'+($operationDowngrade.label -replace '-','_'))
        Copy-Item -Path (Join-Path $operationDowngrade.source '*') -Destination $tamperRoot
        $tamperStatus=Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json')|ConvertFrom-Json -AsHashtable
        $tamperStatus.failedOperation=$operationDowngrade.operation
        Complete-Status $tamperRoot $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "migration operation downgrade $($operationDowngrade.label)"
    }
    Write-Host 'PASS: migration log deletion, hash deletion, and command/history failure downgrades are refused'

    $fingerprintFileMismatch=New-PackageRoot 'FAILED_FINGERPRINT_FILE_STATUS'
    Copy-Item -Path (Join-Path $phaseSevenFailure '*') -Destination $fingerprintFileMismatch
    '0|EMPTY|1|0|0'|Set-Content -Encoding ascii -LiteralPath (Join-Path $fingerprintFileMismatch 'source-fingerprint-before.txt')
    Update-ArtifactBinding $fingerprintFileMismatch 'source-fingerprint-before.txt'
    Invoke-ExpectedRemanifestFailure $fingerprintFileMismatch 'failed source fingerprint file/status mismatch'

    $fingerprintBoundaryMismatch=New-PackageRoot 'FAILED_FINGERPRINT_BOUNDARY'
    Copy-Item -Path (Join-Path $phaseSevenFailure '*') -Destination $fingerprintBoundaryMismatch
    $changedFingerprint='0|EMPTY|1|0|0'
    $changedFingerprint|Set-Content -Encoding ascii -LiteralPath (Join-Path $fingerprintBoundaryMismatch 'source-fingerprint-before.txt')
    $changedStatus=Get-Content -Raw -LiteralPath (Join-Path $fingerprintBoundaryMismatch 'reset-status.json')|ConvertFrom-Json -AsHashtable
    $changedStatus.sourceFingerprint=$changedFingerprint
    Complete-Status $fingerprintBoundaryMismatch $changedStatus
    Invoke-ExpectedRemanifestFailure $fingerprintBoundaryMismatch 'failed non-history fingerprint drift from destructive boundary'

    foreach ($resetLogTamper in @(
        @{ label='MISSING'; value='SQLCMD_COALESCED PHASE_07_PUBLICATION_FAILED' },
        @{ label='DUPLICATE'; value="SOURCE_FINAL_FINGERPRINT=$fingerprint SQLCMD_COALESCED SOURCE_FINAL_FINGERPRINT=$fingerprint" },
        @{ label='EMBEDDED'; value="PREFIX_SOURCE_FINAL_FINGERPRINT=$fingerprint PHASE_07_PUBLICATION_FAILED" }
    )) {
        $tamperRoot=New-PackageRoot ('FAILED_FINGERPRINT_TOKEN_'+$resetLogTamper.label)
        Copy-Item -Path (Join-Path $phaseSevenFailure '*') -Destination $tamperRoot
        $resetLogTamper.value|Set-Content -Encoding ascii -LiteralPath (Join-Path $tamperRoot 'reset-database.log')
        Update-ArtifactBinding $tamperRoot 'reset-database.log'
        Invoke-ExpectedRemanifestFailure $tamperRoot "failed fingerprint token $($resetLogTamper.label.ToLowerInvariant())"
    }

    foreach ($fingerprintTamper in @(
        @{ label='EMPTY_WITH_NONZERO_COUNT'; value='1|EMPTY|0|0|0' },
        @{ label='HISTORY_COUNT_MISMATCH'; value='1|20260913162402_DisposableDevelopmentCurrentModelBaseline|0|0|0' }
    )) {
        $tamperRoot = New-PackageRoot ("FINGERPRINT_" + $fingerprintTamper.label)
        Copy-Item -Path (Join-Path $pass '*') -Destination $tamperRoot
        $fingerprintTamper.value | Set-Content -Encoding ascii -LiteralPath (Join-Path $tamperRoot 'source-fingerprint-before.txt')
        $resetLogPath = Join-Path $tamperRoot 'reset-database.log'
        (Get-Content -Raw -LiteralPath $resetLogPath).Replace(
            "SOURCE_FINAL_FINGERPRINT=$fingerprint", "SOURCE_FINAL_FINGERPRINT=$($fingerprintTamper.value)") |
            Set-Content -Encoding ascii -LiteralPath $resetLogPath
        $tamperStatus = Get-Content -Raw -LiteralPath (Join-Path $tamperRoot 'reset-status.json') |
            ConvertFrom-Json -AsHashtable
        $tamperStatus.sourceFingerprint = $fingerprintTamper.value
        Complete-Status $tamperRoot $tamperStatus
        Invoke-ExpectedRemanifestFailure $tamperRoot "baseline source fingerprint $($fingerprintTamper.label)"
    }

    Add-Content -Encoding ascii -LiteralPath (Join-Path $pass 'reset-invariants-pass-2.txt') -Value 'tamper'
    Invoke-ExpectedFailure $pass 'PASS invariant/content tamper'
}
finally {
    foreach ($path in $temporaryRoots) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
}

Write-Host 'All disposable development reset evidence tests passed.'
