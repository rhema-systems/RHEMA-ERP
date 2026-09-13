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

function New-Status([string]$terminalStatus, [string]$phase, [bool]$backupCreated,
    [bool]$backupVerified, [bool]$resetStarted) {
    $result = @{
        mode='ResetDisposableDevelopment'; status=$terminalStatus; phase=$phase; database='RhemaERP';
        server='<REDACTED_LOCAL_SERVER>'; repositoryClean=$true; reviewedCommit=('a' * 40);
        reviewedTree=('b' * 40); backupCreated=$backupCreated; backupVerified=$backupVerified;
        resetStarted=$resetStarted; automaticRetry=$false; automaticCleanup=$false
        backupPhaseMarkerPublished=$backupCreated; backupMaterialStateReconciled=$true
        backupByteLength=if($backupCreated){1024}else{0}
        backupPreserved=$backupCreated; backupCompleted=$backupCreated
        currentMaterialSha256=if($backupCreated){'E' * 64}else{''}
        backupSha256=''; backupHashMatchesVerified=$false; verifyEvidencePresent=$false
        failedOperation=if($terminalStatus -eq 'PASS'){'NOT_APPLICABLE'}else{'SYNTHETIC_OPERATION'}
    }
    $result
}

function Invoke-ExpectedFailure([string]$root, [string]$label) {
    $output = & pwsh -NoProfile -File $validator -EvidenceDirectory $root -PackageKind DisposableReset 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "Tamper case unexpectedly passed: $label" }
    Write-Host "PASS: $label refused"
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

    $partialBackup = New-PackageRoot 'VERIFY_FAILURE'
    $null = New-Common $partialBackup
    $partialMedia = 'e' * 32
    @("BACKUP_MEDIA_ID=$partialMedia",'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START',
        'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $partialBackup 'backup-create.txt')
    "$('E' * 64)  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $partialBackup 'backup-current.sha256')
    foreach ($entry in @(@(1,'OFFLINE_GATES_COMPLETE'),@(2,'SOURCE_CAPTURE_COMPLETE'),@(3,'BACKUP_CREATED'))) {
        $marker = [ordered]@{ schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=$entry[0]; phase=$entry[1] }
        if ($entry[0] -eq 3) {
            $marker.backupCompleted=$true; $marker.backupByteLength=1024; $marker.currentMaterialSha256=('E' * 64)
        }
        Write-Json (Join-Path $partialBackup ("phase-{0:D2}.json" -f $entry[0])) $marker
    }
    $partialStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'BACKUP_CREATED' $true $false $false
    $partialStatus.backupMediaId=$partialMedia; $partialStatus.backupCompleted=$true; $partialStatus.backupPreserved=$true
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
    foreach ($entry in @(@(1,'OFFLINE_GATES_COMPLETE'),@(2,'SOURCE_CAPTURE_COMPLETE'))) {
        Write-Json (Join-Path $markerFailure ("phase-{0:D2}.json" -f $entry[0])) ([ordered]@{
            schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=$entry[0]; phase=$entry[1]
        })
    }
    $markerMedia = 'f' * 32
    @("BACKUP_MEDIA_ID=$markerMedia",'BACKUP_PATH_ATOMICALLY_RESERVED',
        'BACKUP_COPY_ONLY_CHECKSUM_START','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $markerFailure 'backup-create.txt')
    "$('F' * 64)  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $markerFailure 'backup-current.sha256')
    $markerStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'SOURCE_CAPTURE_COMPLETE' $true $false $false
    $markerStatus.backupPhaseMarkerPublished=$false; $markerStatus.backupMaterialStateReconciled=$true
    $markerStatus.backupByteLength=4096; $markerStatus.backupMediaId=$markerMedia; $markerStatus.backupSha256=''
    $markerStatus.currentMaterialSha256=('F' * 64)
    $markerStatus.backupCompleted=$true; $markerStatus.backupPreserved=$true; $markerStatus.failedOperation='PHASE_03_PUBLICATION'
    Complete-Status $markerFailure $markerStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $markerFailure -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Material-backup/phase-03-publication-failure package did not validate.' }
    Write-Host 'PASS: material backup remains truthfully preserved when phase-03 publication fails'

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
    $mutatedMedia = '1' * 32
    $verifiedHash = 'A' * 64
    $currentHash = 'B' * 64
    @("BACKUP_MEDIA_ID=$mutatedMedia",'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START',
        'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') | Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup-create.txt')
    @("BACKUP_MEDIA_ID=$mutatedMedia",'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup-verify.txt')
    "$verifiedHash  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup.sha256')
    "$currentHash  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $postVerifyMutation 'backup-current.sha256')
    foreach ($entry in @(@(1,'OFFLINE_GATES_COMPLETE'),@(2,'SOURCE_CAPTURE_COMPLETE'),@(3,'BACKUP_CREATED'),@(4,'BACKUP_VERIFIED'))) {
        $marker = [ordered]@{ schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=$entry[0]; phase=$entry[1] }
        if ($entry[0] -eq 3) {
            $marker.backupCompleted=$true; $marker.backupByteLength=2048; $marker.currentMaterialSha256=$verifiedHash
        }
        if ($entry[0] -eq 4) { $marker.backupSha256=$verifiedHash }
        Write-Json (Join-Path $postVerifyMutation ("phase-{0:D2}.json" -f $entry[0])) $marker
    }
    $mutationStatus = New-Status 'FAILED_NO_AUTOMATIC_RETRY' 'BACKUP_VERIFIED' $true $false $false
    $mutationStatus.backupMediaId=$mutatedMedia; $mutationStatus.backupSha256=$verifiedHash
    $mutationStatus.currentMaterialSha256=$currentHash; $mutationStatus.backupHashMatchesVerified=$false
    $mutationStatus.verifyEvidencePresent=$true; $mutationStatus.backupCompleted=$true; $mutationStatus.backupPreserved=$true
    $mutationStatus.failedOperation='PRE_MUTATION_HASH_RECHECK'
    Complete-Status $postVerifyMutation $mutationStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $postVerifyMutation -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Post-VERIFY backup-mutation recovery package did not validate truthfully.' }
    Write-Host 'PASS: post-VERIFY backup mutation preserves original verified hash and reports current bytes unverified'
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
        schema='RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal=1; phase='OFFLINE_GATES_COMPLETE'
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
    $repositoryIds = @(1..455 | ForEach-Object { '{0:D14}_Migration{1:D3}' -f $_,$_ }) +
        '20260908120000_AddProducerIntentGroupsC8'
    @($repositoryIds + "${native}dotnet") | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'migration-discovery.log')
    $repositoryIds | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'repository-migration-history.txt')
    $sourceIds = @($repositoryIds | Select-Object -First 443)
    $sourceIds | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'source-migration-history.txt')
    $fingerprint = "443|$($sourceIds[-1])|0|0|0"
    $fingerprint | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'source-fingerprint-before.txt')
    "RHEMAERP_DATABASE_IDENTITY_SHA256=$('A' * 64)" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'source-database-identity.sha256')
    "LOCAL_SQL_INSTANCE_IDENTITY_SHA256=$('B' * 64)" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'source-server-identity.sha256')
    Write-Json (Join-Path $pass 'pre-mutation-reviewed-git-state.json') $reviewed
    $mediaId = 'c' * 32
    @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$mediaId",'BACKUP_PATH_ATOMICALLY_RESERVED',
        'BACKUP_COPY_ONLY_CHECKSUM_START','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup-create.txt')
    @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$mediaId",'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup-verify.txt')
    "$('D' * 64)  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup.sha256')
    "$('D' * 64)  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'backup-current.sha256')
    @('DISPOSABLE_RESET_SOURCE_QUIESCED','DISPOSABLE_RESET_FINAL_SOURCE_RECHECK_COMPLETE',
        "SOURCE_FINAL_FINGERPRINT=$fingerprint",'DISPOSABLE_RESET_EMPTY_DATABASE_RECREATED') |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'reset-database.log')
    foreach ($name in @('reset-apply-migrations.log','reset-seed-pass-1.log','reset-seed-pass-2.log')) {
        "${native}dotnet" | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass $name)
    }
    $repositoryIds | Set-Content -Encoding ascii -LiteralPath (Join-Path $pass 'target-migration-history.txt')
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
        if (($index + 1) -eq 3) {
            $phaseMarker.backupCompleted=$true; $phaseMarker.backupByteLength=1024
            $phaseMarker.currentMaterialSha256=('D' * 64)
        }
        if (($index + 1) -eq 4) { $phaseMarker.backupSha256=('D' * 64) }
        Write-Json (Join-Path $pass ("phase-{0:D2}.json" -f ($index + 1))) $phaseMarker
    }
    $passStatus = New-Status 'PASS' 'COMPLETE' $true $true $true
    $passStatus.backupMediaId=$mediaId; $passStatus.backupSha256=('D' * 64)
    $passStatus.invariantSha256=$invariantHash; $passStatus.sourceFingerprint=$fingerprint; $passStatus.backupCompleted=$true
    $passStatus.currentMaterialSha256=('D' * 64); $passStatus.backupHashMatchesVerified=$true
    $passStatus.verifyEvidencePresent=$true; $passStatus.backupPreserved=$true
    Complete-Status $pass $passStatus
    & pwsh -NoProfile -File $validator -EvidenceDirectory $pass -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Complete disposable-reset PASS package did not validate.' }
    Write-Host 'PASS: complete 456/C8 reset package validates and writes a manifest'

    Add-Content -Encoding ascii -LiteralPath (Join-Path $pass 'reset-invariants-pass-2.txt') -Value 'tamper'
    Invoke-ExpectedFailure $pass 'PASS invariant/content tamper'
}
finally {
    foreach ($path in $temporaryRoots) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
}

Write-Host 'All disposable development reset evidence tests passed.'
