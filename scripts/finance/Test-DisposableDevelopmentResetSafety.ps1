Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script = Join-Path $PSScriptRoot 'Invoke-GlCutoverRehearsal.ps1'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$connectionVariable = 'RHEMA_GL_DISPOSABLE_DEVELOPMENT_CONNECTION'
$attestationVariable = 'RHEMA_GL_DISPOSABLE_DEVELOPMENT_RESET_ATTESTATION'
$attestationValue = 'I_ATTEST_RHEMAERP_DEVELOPMENT_DATA_IS_DISPOSABLE'
$flags = @('Finance__AccountingEvents__Enabled','Finance__ProducerIntents__Enabled','Finance__ProducerIntentGroups__Enabled')
$reviewVariables = @('RHEMA_GL_REVIEWED_COMMIT','RHEMA_GL_REVIEWED_TREE')
$temporaryRoots = [System.Collections.Generic.List[string]]::new()
$priorPath = [Environment]::GetEnvironmentVariable('PATH', 'Process')

function Invoke-Refusal([string]$name, [string]$expected, [string[]]$arguments) {
    $output = & pwsh -NoProfile -File $script -Mode ResetDisposableDevelopment @arguments 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw "Safety case '$name' unexpectedly succeeded." }
    if ($output -notmatch [regex]::Escape($expected)) {
        throw "Safety case '$name' did not return '$expected'. Output: $output"
    }
    Write-Host "PASS: $name"
}

function New-ExternalEvidencePath([string]$label) {
    $path = Join-Path ([System.IO.Path]::GetTempPath()) "RHEMAERP_DISPOSABLE_RESET_${label}_$([Guid]::NewGuid().ToString('N'))"
    $temporaryRoots.Add($path)
    $path
}

try {
    foreach ($name in @($connectionVariable,$attestationVariable) + $flags + $reviewVariables) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    Invoke-Refusal 'missing explicit confirmation' '-ConfirmDisposableDevelopmentReset' @()

    Invoke-Refusal 'missing disposable attestation' 'requires exact process attestation' `
        @('-ConfirmDisposableDevelopmentReset')
    [Environment]::SetEnvironmentVariable($attestationVariable, $attestationValue, 'Process')
    Invoke-Refusal 'missing external evidence path' 'requires an explicit new empty external -EvidenceDirectory' `
        @('-ConfirmDisposableDevelopmentReset')

    $external = New-ExternalEvidencePath 'MISSING_REVIEW'
    Invoke-Refusal 'missing reviewed commit/tree before connection parse' 'requires exact 40-hex' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',$external)

    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $tree = (& git -C $repositoryRoot rev-parse 'HEAD^{tree}').Trim()
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_COMMIT', ('0' * 40), 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_TREE', $tree, 'Process')
    Invoke-Refusal 'descendant or unreviewed HEAD before connection parse' 'does not exactly match' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'UNREVIEWED'))

    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_COMMIT', $head, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_REVIEWED_TREE', $tree, 'Process')
    $dirtyProbe = Join-Path $repositoryRoot 'disposable-reset-dirty-probe.ps1'
    try {
        'dirty' | Set-Content -Encoding ascii -LiteralPath $dirtyProbe
        Invoke-Refusal 'dirty repository before connection parse' 'requires a completely clean tracked and untracked repository' `
            @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'DIRTY'))
    }
    finally { if (Test-Path -LiteralPath $dirtyProbe) { Remove-Item -LiteralPath $dirtyProbe -Force } }

    foreach ($flag in $flags) {
        foreach ($name in $flags) { [Environment]::SetEnvironmentVariable($name, 'false', 'Process') }
        foreach ($unsafeValue in @($null,'true')) {
            [Environment]::SetEnvironmentVariable($flag, $unsafeValue, 'Process')
            Invoke-Refusal "$flag refuses '$unsafeValue'" "'$flag' must be explicitly set to false" `
                @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'FLAG'))
        }
    }
    foreach ($flag in $flags) { [Environment]::SetEnvironmentVariable($flag, 'false', 'Process') }

    $insideRepository = Join-Path $repositoryRoot '.artifacts\disposable-reset-forbidden'
    Invoke-Refusal 'repository-local evidence path' 'evidence must be external to the repository' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',$insideRepository)

    $nonempty = New-ExternalEvidencePath 'NONEMPTY'
    New-Item -ItemType Directory -Path $nonempty | Out-Null
    'stale' | Set-Content -Encoding ascii -LiteralPath (Join-Path $nonempty 'stale.txt')
    Invoke-Refusal 'existing external evidence path' 'evidence directory must be new and absent' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',$nonempty)

    [Environment]::SetEnvironmentVariable($connectionVariable, 'Server=localhost;Database=WrongDatabase;Integrated Security=true', 'Process')
    Invoke-Refusal 'wrong disposable database' 'only the exact case-sensitive local database RhemaERP' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'WRONG_DB'))
    [Environment]::SetEnvironmentVariable($connectionVariable, 'Server=localhost;Database=rhemaerp;Integrated Security=true', 'Process')
    Invoke-Refusal 'wrong database casing' 'only the exact case-sensitive local database RhemaERP' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'WRONG_CASE'))
    [Environment]::SetEnvironmentVariable($connectionVariable, 'Server=remote-sql;Database=RhemaERP;Integrated Security=true', 'Process')
    Invoke-Refusal 'nonlocal SQL Server' 'requires an explicitly local SQL Server data source' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'REMOTE'))
    [Environment]::SetEnvironmentVariable($connectionVariable, 'Server=localhost;Database=RhemaERP;Integrated Security=true;Failover Partner=localhost\other', 'Process')
    Invoke-Refusal 'failover partner routing' 'forbids Failover Partner routing' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'FAILOVER'))
    [Environment]::SetEnvironmentVariable($connectionVariable, 'Server=localhost;Database=RhemaERP;Integrated Security=true;MultiSubnetFailover=true', 'Process')
    Invoke-Refusal 'multisubnet routing' 'forbids MultiSubnetFailover routing' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'MULTISUBNET'))
    [Environment]::SetEnvironmentVariable($connectionVariable, 'Server=localhost;Database=RhemaERP;Integrated Security=true;Application Intent=ReadOnly', 'Process')
    Invoke-Refusal 'readonly routing intent' 'forbids Application Intent routing overrides' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'APP_INTENT'))
    [Environment]::SetEnvironmentVariable($connectionVariable, 'Server=localhost;Database=RhemaERP;Integrated Security=true;Network Library=dbmssocn', 'Process')
    Invoke-Refusal 'network-library routing override' 'forbids Network Library routing overrides' `
        @('-ConfirmDisposableDevelopmentReset','-EvidenceDirectory',(New-ExternalEvidencePath 'NETWORK_LIBRARY'))

    $text = Get-Content -Raw -LiteralPath $script
    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($script,[ref]$tokens,[ref]$parseErrors)
    if ($parseErrors.Count -ne 0) { throw 'Could not parse rehearsal harness for function-availability exercise.' }
    $dispatcherOffset = $text.IndexOf("if (`$Mode -eq 'ResetDisposableDevelopment')", [StringComparison]::Ordinal)
    $reservationAst = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'New-AtomicBackupReservation' }, $true))
    if ($reservationAst.Count -ne 1 -or $reservationAst[0].Extent.StartOffset -ge $dispatcherOffset) {
        throw 'New-AtomicBackupReservation is not uniquely defined before reset dispatch.'
    }
    . ([ScriptBlock]::Create($reservationAst[0].Extent.Text))
    $collisionPath = Join-Path ([System.IO.Path]::GetTempPath()) "RhemaERP_DISPOSABLE_RESET_COLLISION_$([Guid]::NewGuid().ToString('N')).bak"
    try {
        New-AtomicBackupReservation $collisionPath
        $refused = $false
        try { New-AtomicBackupReservation $collisionPath }
        catch [System.IO.IOException] { $refused = $true }
        if (-not $refused) { throw 'Actual pre-dispatch backup reservation helper did not refuse the occupied path.' }
    }
    finally { if (Test-Path -LiteralPath $collisionPath) { Remove-Item -LiteralPath $collisionPath -Force } }
    Write-Host 'PASS: actual pre-dispatch backup reservation helper is available and refuses collisions'

    foreach ($functionName in @('ConvertTo-SanitizedEvidenceLine','Get-SqlEvidenceTokens',
        'Assert-UniqueOrderedSqlEvidenceTokens','Invoke-Native','Assert-SqlcmdOutputWidth','Invoke-Sql',
        'Invoke-SqlWithSanitizedEvidence','Get-DisposableBackupFileName','Join-DisposableBackupPath',
        'Write-AtomicTextFile','Write-DisposablePhaseMarker','Get-DisposableLastDurablePhase',
        'Get-TextSha256','Write-DisposableResetStatus','Write-DisposableRecoveryInstructions','Assert-DisposableServerSideLocality',
        'Test-DisposableSourceFingerprint','Get-DisposableMaterialBackupState','Get-DisposableBackupRecoveryState')) {
        $functionAst = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq $functionName }, $true))
        if ($functionAst.Count -ne 1 -or $functionAst[0].Extent.StartOffset -ge $dispatcherOffset) {
            throw "Durable reset state helper is not uniquely available before dispatch: $functionName"
        }
        . ([ScriptBlock]::Create($functionAst[0].Extent.Text))
    }
    $uniqueBackupRoot = New-ExternalEvidencePath 'UNIQUE_BACKUPS'
    New-Item -ItemType Directory -Path $uniqueBackupRoot | Out-Null
    $legacyBackupPath = Join-Path $uniqueBackupRoot 'RhemaERP_DISPOSABLE_RESET_COPYONLY.bak'
    'preserved legacy backup' | Set-Content -Encoding ascii -LiteralPath $legacyBackupPath
    $firstMediaId = '1' * 32
    $secondMediaId = '2' * 32
    $firstAttemptPath = Join-DisposableBackupPath $uniqueBackupRoot $firstMediaId
    $secondAttemptPath = Join-DisposableBackupPath $uniqueBackupRoot $secondMediaId
    if ($firstAttemptPath -ceq $secondAttemptPath -or $firstAttemptPath -ceq $legacyBackupPath -or
        (Split-Path -Leaf $firstAttemptPath) -cne "RhemaERP_DISPOSABLE_RESET_COPYONLY_${firstMediaId}.bak" -or
        (Split-Path -Leaf $secondAttemptPath) -cne "RhemaERP_DISPOSABLE_RESET_COPYONLY_${secondMediaId}.bak") {
        throw 'Media-bound disposable backup paths are not exact and attempt-unique.'
    }
    New-AtomicBackupReservation $firstAttemptPath
    New-AtomicBackupReservation $secondAttemptPath
    $collisionRefused = $false
    try { New-AtomicBackupReservation $firstAttemptPath } catch [System.IO.IOException] { $collisionRefused = $true }
    if (-not $collisionRefused -or (Get-Content -Raw -LiteralPath $legacyBackupPath).Trim() -cne 'preserved legacy backup') {
        throw 'Attempt-unique reservation did not refuse collision or preserve the prior fixed backup.'
    }
    foreach ($invalidMediaId in @('',('../' + ('a' * 29)),('A' * 32),('a' * 31),('a' * 33))) {
        $refused = $false
        try { Join-DisposableBackupPath $uniqueBackupRoot $invalidMediaId } catch { $refused = $true }
        if (-not $refused) { throw 'Disposable backup filename accepted malformed/path-ambiguous media identity.' }
    }
    Write-Host 'PASS: prior fixed backup coexists with two unique media-bound attempts; collision and path spoofing are refused'
    $collisionHashBefore = (Get-FileHash -Algorithm SHA256 -LiteralPath $firstAttemptPath).Hash
    $attemptOwnedBackup = $false
    try { New-AtomicBackupReservation $firstAttemptPath; $attemptOwnedBackup = $true } catch [System.IO.IOException] {}
    $collisionRecovery = if ($attemptOwnedBackup) { Get-DisposableBackupRecoveryState $firstAttemptPath $uniqueBackupRoot } else {
        [pscustomobject]@{ materialized=$false; backupVerified=$false }
    }
    if ($attemptOwnedBackup -or $collisionRecovery.materialized -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $firstAttemptPath).Hash -cne $collisionHashBefore) {
        throw 'Collision catch flow claimed or changed a backup not owned by this attempt.'
    }
    Write-Host 'PASS: collision catch leaves pre-existing bytes unchanged and reports no attempt-owned backup'
    $sqlcmdMaxVariableWidth = 8000
    $sqlcmdScreenWidth = 8000
    $script:sensitiveEvidenceTokens = [System.Collections.Generic.List[string]]::new()
    $collapsedTransportRoot = New-ExternalEvidencePath 'COLLAPSED_SQLCMD_TRANSPORT'
    New-Item -ItemType Directory -Path $collapsedTransportRoot | Out-Null
    @'
$captured = @($args)
$outputIndex = [Array]::IndexOf($captured, '-o')
if ($outputIndex -lt 0 -or $outputIndex + 1 -ge $captured.Count) { exit 91 }
$collapsed = [Environment]::GetEnvironmentVariable('RHEMA_GL_COLLAPSED_SQLCMD_OUTPUT', 'Process')
[System.IO.File]::WriteAllText($captured[$outputIndex + 1], $collapsed + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
exit 0
'@ | Set-Content -Encoding utf8 -LiteralPath (Join-Path $collapsedTransportRoot 'sqlcmd.ps1')
    [Environment]::SetEnvironmentVariable('PATH', "$collapsedTransportRoot$([System.IO.Path]::PathSeparator)$priorPath", 'Process')
    $collapsedMedia = '9' * 32
    $expectedCollapsedTokens = @('DATABASE=RhemaERP',"BACKUP_MEDIA_ID=$collapsedMedia",
        'BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_START','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE')
    $collapsedGood = "DATABASE=RhemaERP BACKUP_MEDIA_ID=$collapsedMedia BACKUP_PATH_ATOMICALLY_RESERVED " +
        "BACKUP_COPY_ONLY_CHECKSUM_START Processed 55296 pages for database 'RhemaERP', file 'RhemaERP' on file 1. " +
        "100 percent processed. Processed 2 pages for database 'RhemaERP', file 'RhemaERP_log' on file 1. " +
        "BACKUP DATABASE successfully processed 55298 pages in 3.141 seconds. BACKUP_COPY_ONLY_CHECKSUM_COMPLETE"
    $collapsedBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new(
        'Server=synthetic-local;Database=RhemaERP;Integrated Security=true')
    $collapsedGoodEvidence = Join-Path $collapsedTransportRoot 'collapsed-good.txt'
    [Environment]::SetEnvironmentVariable('RHEMA_GL_COLLAPSED_SQLCMD_OUTPUT', $collapsedGood, 'Process')
    Invoke-SqlWithSanitizedEvidence $collapsedBuilder 'master' 'SELECT 1' '' $collapsedGoodEvidence
    $collapsedPhysicalLines = @(Get-Content -LiteralPath $collapsedGoodEvidence)
    if ($collapsedPhysicalLines.Count -ne 1 -or $collapsedPhysicalLines[0].Length -lt 300) {
        throw 'Actual sanitized sqlcmd transport did not reproduce the long single-line backup evidence shape.'
    }
    Assert-UniqueOrderedSqlEvidenceTokens $collapsedGoodEvidence $expectedCollapsedTokens 'Synthetic collapsed backup'
    foreach ($case in @(
        @{ label='marker absence'; text=$collapsedGood.Replace('BACKUP_PATH_ATOMICALLY_RESERVED ', '') },
        @{ label='marker reordering'; text=$collapsedGood.Replace(
            'BACKUP_PATH_ATOMICALLY_RESERVED BACKUP_COPY_ONLY_CHECKSUM_START',
            'BACKUP_COPY_ONLY_CHECKSUM_START BACKUP_PATH_ATOMICALLY_RESERVED') },
        @{ label='embedded marker forgery'; text=$collapsedGood.Replace(
            'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE','FORGED_BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') },
        @{ label='duplicate marker'; text=$collapsedGood.Replace(
            'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE BACKUP_COPY_ONLY_CHECKSUM_COMPLETE') }
    )) {
        $caseEvidence = Join-Path $collapsedTransportRoot ("collapsed-$($case.label.Replace(' ','-')).txt")
        [Environment]::SetEnvironmentVariable('RHEMA_GL_COLLAPSED_SQLCMD_OUTPUT', $case.text, 'Process')
        Invoke-SqlWithSanitizedEvidence $collapsedBuilder 'master' 'SELECT 1' '' $caseEvidence
        $refused = $false
        try { Assert-UniqueOrderedSqlEvidenceTokens $caseEvidence $expectedCollapsedTokens 'Synthetic collapsed backup' }
        catch { $refused = $true }
        if (-not $refused) { throw "Collapsed sqlcmd evidence unexpectedly accepted $($case.label)." }
    }
    Write-Host 'PASS: actual single-line sqlcmd transport accepts unique ordered tokens and refuses absence, reordering, forgery and duplication'
    [Environment]::SetEnvironmentVariable('PATH', $priorPath, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_COLLAPSED_SQLCMD_OUTPUT', $null, 'Process')

    $stateRoot = New-ExternalEvidencePath 'STATE_MACHINE'
    New-Item -ItemType Directory -Path $stateRoot | Out-Null
    $script:finalReviewedGitState = [pscustomobject]@{ reviewedCommit=$head; reviewedTree=$tree }
    Write-DisposablePhaseMarker $stateRoot 1 'OFFLINE_GATES_COMPLETE'
    foreach ($entry in @(@(2,'SOURCE_CAPTURE_COMPLETE'),@(3,'BACKUP_CREATED'),@(4,'BACKUP_VERIFIED'),@(5,'RESET_STARTED'))) {
        Write-DisposablePhaseMarker $stateRoot $entry[0] $entry[1]
    }
    Write-DisposableResetStatus $stateRoot 'FAILED_NO_AUTOMATIC_RETRY' 'RESET_STARTED' $true $true $true
    $durableState = Get-Content -Raw -LiteralPath (Join-Path $stateRoot 'reset-status.json') | ConvertFrom-Json
    if ($durableState.status -cne 'FAILED_NO_AUTOMATIC_RETRY' -or $durableState.phase -cne 'RESET_STARTED' -or
        $durableState.resetStarted -ne $true -or
        -not (Test-Path -LiteralPath (Join-Path $stateRoot 'phase-05.json') -PathType Leaf)) {
        throw 'Atomic terminal status did not bind the durable RESET_STARTED phase.'
    }
    $gapRefused = $false
    try { Write-DisposablePhaseMarker $stateRoot 7 'INVALID_GAP' } catch { $gapRefused = $true }
    if (-not $gapRefused) { throw 'Monotonic phase writer accepted a phase gap.' }
    $mismatchRefused = $false
    try { Write-DisposableResetStatus $stateRoot 'FAILED_NO_AUTOMATIC_RETRY' 'BACKUP_VERIFIED' $true $true $true }
    catch { $mismatchRefused = $true }
    if (-not $mismatchRefused) { throw 'Terminal status accepted a phase other than the last durable marker.' }
    foreach ($transition in @(
        @('BACKUP_CREATED',$false,'NOT_APPLICABLE'),@('BACKUP_VERIFIED',$true,'NOT_APPLICABLE'),
        @('RESET_STARTED',$true,'NOT_APPLICABLE'),@('COMPLETE',$true,'NOT_APPLICABLE'),
        @('RESET_STARTED',$false,'FAILED_OUTER_CATCH'))) {
        Write-DisposableRecoveryInstructions $stateRoot $transition[1] $transition[0] $transition[2]
        $recovery = Get-Content -Raw -LiteralPath (Join-Path $stateRoot 'RECOVERY.md')
        if ($recovery -notmatch [regex]::Escape("Last durable phase: $($transition[0])") -or
            $recovery -notmatch [regex]::Escape("Failed operation: $($transition[2])")) {
            throw "Actual recovery helper did not atomically publish transition $($transition[0])."
        }
    }
    Write-Host 'PASS: durable terminal phase/status and every recovery transition execute under StrictMode'

    $markerFailureRoot = New-ExternalEvidencePath 'MARKER_FAILURE_STATE'
    New-Item -ItemType Directory -Path $markerFailureRoot | Out-Null
    Write-DisposablePhaseMarker $markerFailureRoot 1 'OFFLINE_GATES_COMPLETE'
    Write-DisposablePhaseMarker $markerFailureRoot 2 'SOURCE_CAPTURE_COMPLETE'
    $materialBackupPath = Join-Path $markerFailureRoot 'material-backup.bak'
    [System.IO.File]::WriteAllBytes($materialBackupPath, [System.Text.Encoding]::UTF8.GetBytes('material backup bytes'))
    $materialState = Get-DisposableMaterialBackupState $materialBackupPath
    if (-not $materialState.materialized -or $materialState.byteLength -le 0 -or
        $materialState.sha256 -notmatch '^[0-9A-F]{64}$') {
        throw 'Actual material-backup helper did not capture nonempty file state and SHA-256.'
    }
    New-Item -ItemType Directory -Path (Join-Path $markerFailureRoot 'phase-03.json') | Out-Null
    $publicationRefused = $false
    try { Write-DisposablePhaseMarker $markerFailureRoot 3 'BACKUP_CREATED' }
    catch { $publicationRefused = $true }
    if (-not $publicationRefused -or (Get-DisposableLastDurablePhase $markerFailureRoot) -cne 'SOURCE_CAPTURE_COMPLETE') {
        throw 'Synthetic phase-03 publication failure did not preserve the actual last durable phase.'
    }
    Write-DisposableRecoveryInstructions $markerFailureRoot $false 'SOURCE_CAPTURE_COMPLETE' 'PHASE_03_PUBLICATION'
    Write-DisposableResetStatus $markerFailureRoot 'FAILED_NO_AUTOMATIC_RETRY' 'SOURCE_CAPTURE_COMPLETE' $true $false $false @{
        failedOperation='PHASE_03_PUBLICATION'; backupPhaseMarkerPublished=$false;
        backupMaterialStateReconciled=$true; backupByteLength=$materialState.byteLength;
        backupSha256=$materialState.sha256; backupPreserved=$true
    }
    $markerFailureStatus = Get-Content -Raw -LiteralPath (Join-Path $markerFailureRoot 'reset-status.json') | ConvertFrom-Json
    if ($markerFailureStatus.backupCreated -ne $true -or $markerFailureStatus.backupPreserved -ne $true -or
        $markerFailureStatus.backupVerified -ne $false -or $markerFailureStatus.phase -cne 'SOURCE_CAPTURE_COMPLETE') {
        throw 'Marker-publication failure terminal evidence did not truthfully preserve material backup state.'
    }
    Write-Host 'PASS: material backup remains truthful when actual phase-03 publication fails'

    $mutationRoot = New-ExternalEvidencePath 'POST_VERIFY_MUTATION'
    New-Item -ItemType Directory -Path $mutationRoot | Out-Null
    $recoveryMediaId = '8' * 32
    $recoveryBackupFileName = Get-DisposableBackupFileName $recoveryMediaId
    $mutationBackupPath = Join-Path $mutationRoot $recoveryBackupFileName
    [System.IO.File]::WriteAllBytes($mutationBackupPath, [System.Text.Encoding]::UTF8.GetBytes('verified bytes'))
    $originalState = Get-DisposableMaterialBackupState $mutationBackupPath
    $recoveryPathHash = Get-TextSha256 $mutationBackupPath
    Write-DisposablePhaseMarker $mutationRoot 1 'OFFLINE_GATES_COMPLETE'
    Write-DisposablePhaseMarker $mutationRoot 2 'SOURCE_CAPTURE_COMPLETE'
    Write-DisposablePhaseMarker $mutationRoot 3 'BACKUP_CREATED' @{
        database='RhemaERP'; backupMediaId=$recoveryMediaId; backupFileName=$recoveryBackupFileName; backupCompleted=$true;
        backupByteLength=$originalState.byteLength; currentMaterialSha256=$originalState.sha256; backupPathSha256=$recoveryPathHash
    }
    Write-DisposablePhaseMarker $mutationRoot 4 'BACKUP_VERIFIED' @{
        backupSha256=$originalState.sha256; backupMediaId=$recoveryMediaId; backupFileName=$recoveryBackupFileName; backupPathSha256=$recoveryPathHash
    }
    "$($originalState.sha256)  $recoveryBackupFileName" |
        Set-Content -Encoding ascii -LiteralPath (Join-Path $mutationRoot 'backup.sha256')
    $coalescedVerifyEvidence = "DATABASE=RhemaERP BACKUP_MEDIA_ID=$recoveryMediaId " +
        "The backup set on file 1 is valid. RESTORE_VERIFYONLY_CHECKSUM_COMPLETE"
    $verifyEvidencePath = Join-Path $mutationRoot 'backup-verify.txt'
    $coalescedVerifyEvidence | Set-Content -Encoding ascii -LiteralPath $verifyEvidencePath
    $reconciledVerified = Get-DisposableBackupRecoveryState $mutationBackupPath $mutationRoot
    if ($reconciledVerified.backupVerified -ne $true -or $reconciledVerified.verifyEvidencePresent -ne $true -or
        $reconciledVerified.hashMatchesVerified -ne $true -or
        $reconciledVerified.verifiedBackupSha256 -cne $originalState.sha256 -or
        $reconciledVerified.currentMaterialSha256 -cne $originalState.sha256) {
        throw 'Actual recovery helper did not accept exact unique ordered tokens from coalesced VERIFYONLY evidence.'
    }
    $otherRoot = New-ExternalEvidencePath 'WRONG_FULL_PATH'
    New-Item -ItemType Directory -Path $otherRoot | Out-Null
    $sameNameOtherPath = Join-Path $otherRoot $recoveryBackupFileName
    Copy-Item -LiteralPath $mutationBackupPath -Destination $sameNameOtherPath
    $wrongPathRecovery = Get-DisposableBackupRecoveryState $sameNameOtherPath $mutationRoot
    if ($wrongPathRecovery.backupVerified -ne $false -or $wrongPathRecovery.verifyEvidencePresent -ne $false) {
        throw 'Recovery trusted the same basename under a different full path despite path-hash mismatch.'
    }
    Write-Host 'PASS: recovery refuses same basename under a different full path hash'
    foreach ($case in @(
        @{ label='missing completion'; text=$coalescedVerifyEvidence.Replace(' RESTORE_VERIFYONLY_CHECKSUM_COMPLETE','') },
        @{ label='duplicate completion'; text=$coalescedVerifyEvidence.Replace(
            'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE','RESTORE_VERIFYONLY_CHECKSUM_COMPLETE RESTORE_VERIFYONLY_CHECKSUM_COMPLETE') },
        @{ label='reordered completion'; text="DATABASE=RhemaERP RESTORE_VERIFYONLY_CHECKSUM_COMPLETE BACKUP_MEDIA_ID=$recoveryMediaId" },
        @{ label='embedded completion forgery'; text=$coalescedVerifyEvidence.Replace(
            'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE','FORGED_RESTORE_VERIFYONLY_CHECKSUM_COMPLETE') },
        @{ label='database identity tamper'; text=$coalescedVerifyEvidence.Replace('DATABASE=RhemaERP','DATABASE=Other') },
        @{ label='media identity tamper'; text=$coalescedVerifyEvidence.Replace($recoveryMediaId, ('7' * 32)) }
    )) {
        $case.text | Set-Content -Encoding ascii -LiteralPath $verifyEvidencePath
        $refusedRecovery = Get-DisposableBackupRecoveryState $mutationBackupPath $mutationRoot
        if ($refusedRecovery.verifyEvidencePresent -ne $false -or $refusedRecovery.backupVerified -ne $false -or
            $refusedRecovery.hashMatchesVerified -ne $true -or
            $refusedRecovery.verifiedBackupSha256 -cne $originalState.sha256) {
            throw "Actual recovery helper did not fail closed for coalesced VERIFYONLY $($case.label)."
        }
    }
    $phaseThreePath = Join-Path $mutationRoot 'phase-03.json'
    $phaseThreeOriginal = Get-Content -Raw -LiteralPath $phaseThreePath
    foreach ($case in @(
        @{ label='durable database identity'; property='database'; value='Other' },
        @{ label='durable media identity'; property='backupMediaId'; value=('6' * 32) }
    )) {
        $phaseThreeTamper = $phaseThreeOriginal | ConvertFrom-Json
        $phaseThreeTamper.($case.property) = $case.value
        $phaseThreeTamper | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath $phaseThreePath
        $refusedRecovery = Get-DisposableBackupRecoveryState $mutationBackupPath $mutationRoot
        if ($refusedRecovery.verifyEvidencePresent -ne $false -or $refusedRecovery.backupVerified -ne $false) {
            throw "Actual recovery helper trusted invalid $($case.label) from phase-03."
        }
    }
    $phaseThreeOriginal | Set-Content -Encoding utf8 -LiteralPath $phaseThreePath
    Write-Host 'PASS: coalesced recovery binds durable database/media and refuses missing, duplicate, reordered, embedded or mismatched tokens'
    $coalescedVerifyEvidence | Set-Content -Encoding ascii -LiteralPath $verifyEvidencePath
    [System.IO.File]::WriteAllBytes($mutationBackupPath, [System.Text.Encoding]::UTF8.GetBytes('mutated bytes after verify'))
    $reconciledMutation = Get-DisposableBackupRecoveryState $mutationBackupPath $mutationRoot
    if ($reconciledMutation.backupVerified -ne $false -or $reconciledMutation.verifyEvidencePresent -ne $true -or
        $reconciledMutation.hashMatchesVerified -ne $false -or
        $reconciledMutation.verifiedBackupSha256 -cne $originalState.sha256 -or
        $reconciledMutation.currentMaterialSha256 -ceq $originalState.sha256) {
        throw 'Actual recovery helper overwrote verified-hash semantics after post-VERIFY backup mutation.'
    }
    Write-Host 'PASS: actual recovery helper preserves phase-04 hash and marks mutated current bytes unverified'

    Assert-DisposableServerSideLocality 'localhost' 'LOCALHOST' '' 'LOCALHOST' '' '' 'localhost'
    foreach ($case in @(
        @('remote engine','localhost','REMOTEHOST','','REMOTEHOST','','','LOCALHOST'),
        @('named-instance drift','localhost\SQLEXPRESS','LOCALHOST','','LOCALHOST','','','LOCALHOST'),
        @('server-name alias drift','localhost','LOCALHOST','','STALE_ALIAS','','','LOCALHOST'),
        @('port-forward drift','localhost,1433','LOCALHOST','','LOCALHOST','127.0.0.1','1555','LOCALHOST'),
        @('non-loopback endpoint','tcp:localhost,1433','LOCALHOST','','LOCALHOST','10.10.1.20','1433','LOCALHOST'))) {
        $refused = $false
        try { Assert-DisposableServerSideLocality $case[1] $case[2] $case[3] $case[4] $case[5] $case[6] $case[7] }
        catch { $refused = $true }
        if (-not $refused) { throw "Server-side locality case unexpectedly passed: $($case[0])" }
    }
    if (-not (Test-DisposableSourceFingerprint '446|20260902140000_AddFixedAssetDepreciationConventionEvidence|1|9|28')) {
        throw 'Valid restricted source fingerprint was refused.'
    }
    foreach ($invalidFingerprint in @('', '446|latest|1|9|28', '446|20260902140000_Good|1|9|28|extra',
        "446|20260902140000_Good|1|9|28`nsecret")) {
        if (Test-DisposableSourceFingerprint $invalidFingerprint) { throw 'Invalid source fingerprint was accepted.' }
    }
    Write-Host 'PASS: server-side host/instance/endpoint and restricted fingerprint helpers refuse synthetic ambiguity'

    $start = $text.IndexOf('function Invoke-DisposableDevelopmentReset', [StringComparison]::Ordinal)
    $end = $text.IndexOf("if (`$Mode -eq 'ResetDisposableDevelopment')", $start, [StringComparison]::Ordinal)
    if ($start -lt 0 -or $end -le $start) { throw 'Could not isolate disposable reset implementation.' }
    $reset = $text.Substring($start, $end - $start)
    $orderedMarkers = @(
        'New-AtomicBackupReservation $backupPath',
        'BACKUP DATABASE [RhemaERP]',
        '$materialBackupState = Get-DisposableMaterialBackupState $backupPath',
        "Write-DisposablePhaseMarker `$evidenceDirectory 3 'BACKUP_CREATED'",
        'RESTORE VERIFYONLY',
        '$postVerifyBackupState = Get-DisposableMaterialBackupState $backupPath',
        "`$backupVerified = `$true",
        "Assert-FinalReviewedGitState 'ResetDisposableDevelopment'",
        'DISPOSABLE_RESET_IDENTITY_DRIFT',
        'ALTER DATABASE [RhemaERP] SET SINGLE_USER WITH ROLLBACK IMMEDIATE',
        'DROP DATABASE [RhemaERP]',
        'CREATE DATABASE [RhemaERP]',
        "'apply-migrations'",
        "'seed-db'",
        'DBCC CHECKDB'
    )
    $prior = -1
    foreach ($marker in $orderedMarkers) {
        $index = $reset.IndexOf($marker, [StringComparison]::Ordinal)
        if ($index -le $prior) { throw "Disposable reset safety boundary is missing or out of order: $marker" }
        $prior = $index
    }
    foreach ($required in @(
        'COPY_ONLY, CHECKSUM, NOINIT, NOSKIP, MEDIANAME=',
        'BACKUP_PATH_ATOMICALLY_RESERVED',
        'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE',
        'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE',
        'Verified reset backup disappeared before mutation',
        'Disposable reset cannot start without verified backup markers and SHA-256 evidence',
        'Disposable reset backup SHA-256 proof changed before mutation',
        'DISPOSABLE_RESET_VERIFIED_BACKUP_MISSING',
        'pre-mutation-reviewed-git-state.json',
        'DISPOSABLE_RESET_FINAL_HISTORY_DRIFT',
        'DISPOSABLE_RESET_FINAL_FINGERPRINT_DRIFT',
        'DISPOSABLE_RESET_SERVER_IDENTITY_DRIFT',
        'RHEMAERP_DISPOSABLE_DEVELOPMENT_RESET',
        "Write-DisposablePhaseMarker `$evidenceDirectory 5 'RESET_STARTED'",
        'Reset RhemaERP history is not exactly authoritative repository 456/C8 with zero orphans',
        'Second disposable reset seed changed canonical Finance invariants',
        "'FAILED_NO_AUTOMATIC_RETRY'",
        'no retry, restore, or cleanup was attempted',
        'Get-SanitizedExceptionMessage'
    )) {
        if (-not $reset.Contains($required)) { throw "Disposable reset contract is missing: $required" }
    }
    if ([regex]::Matches($reset, 'DROP DATABASE \[RhemaERP\]').Count -ne 1 -or
        $reset -match '(?i)retry\s*\(' -or $reset -match 'Remove-Item[^\r\n]+backup') {
        throw 'Disposable reset contains an extra destructive, retry, or backup-cleanup path.'
    }
    if (-not $text.Contains("server = '<REDACTED_LOCAL_SERVER>'")) {
        throw 'Disposable reset status does not redact the local machine/server identity.'
    }
    $resetStartedIndex = $reset.IndexOf("Write-DisposablePhaseMarker `$evidenceDirectory 5 'RESET_STARTED'", [StringComparison]::Ordinal)
    $invokeBoundaryIndex = $reset.IndexOf("Invoke-SqlWithSanitizedEvidence `$databaseTarget.Builder 'master' '' `$destructiveSqlPath", [StringComparison]::Ordinal)
    if ($resetStartedIndex -lt 0 -or $invokeBoundaryIndex -le $resetStartedIndex) {
        throw 'Durable RESET_STARTED phase is not atomically written before the destructive SQL call.'
    }
    $singleUserIndex = $reset.IndexOf('ALTER DATABASE [RhemaERP] SET SINGLE_USER', [StringComparison]::Ordinal)
    $finalHistoryIndex = $reset.IndexOf('DISPOSABLE_RESET_FINAL_HISTORY_DRIFT', [StringComparison]::Ordinal)
    $finalFingerprintIndex = $reset.IndexOf('DISPOSABLE_RESET_FINAL_FINGERPRINT_DRIFT', [StringComparison]::Ordinal)
    $dropIndex = $reset.IndexOf('DROP DATABASE [RhemaERP]', [StringComparison]::Ordinal)
    if ($singleUserIndex -lt 0 -or $finalHistoryIndex -le $singleUserIndex -or
        $finalFingerprintIndex -le $finalHistoryIndex -or $dropIndex -le $finalFingerprintIndex) {
        throw 'Final history/fingerprint checks are not inside the quiescent destructive SQL boundary immediately before DROP.'
    }
    Write-Host 'PASS: backup proof, identity-drift, partial-failure, no-retry, history, seed, DBCC and sanitization contracts'
}
finally {
    [Environment]::SetEnvironmentVariable('PATH', $priorPath, 'Process')
    [Environment]::SetEnvironmentVariable('RHEMA_GL_COLLAPSED_SQLCMD_OUTPUT', $null, 'Process')
    foreach ($name in @($connectionVariable,$attestationVariable) + $flags + $reviewVariables) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    foreach ($path in $temporaryRoots) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
}

Write-Host 'All disposable development reset offline safety refusals passed.'
