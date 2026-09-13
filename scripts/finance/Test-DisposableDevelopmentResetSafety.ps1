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

    foreach ($functionName in @('Write-AtomicTextFile','Write-DisposablePhaseMarker','Get-DisposableLastDurablePhase',
        'Write-DisposableResetStatus','Write-DisposableRecoveryInstructions','Assert-DisposableServerSideLocality',
        'Test-DisposableSourceFingerprint')) {
        $functionAst = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq $functionName }, $true))
        if ($functionAst.Count -ne 1 -or $functionAst[0].Extent.StartOffset -ge $dispatcherOffset) {
            throw "Durable reset state helper is not uniquely available before dispatch: $functionName"
        }
        . ([ScriptBlock]::Create($functionAst[0].Extent.Text))
    }
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
        @('BACKUP_CREATED_VERIFY_PENDING',$false),@('BACKUP_VERIFIED',$true),@('RESET_STARTED',$true),
        @('PASS',$true),@('FAILED_OUTER_CATCH',$false))) {
        Write-DisposableRecoveryInstructions $stateRoot $transition[1] $transition[0]
        $recovery = Get-Content -Raw -LiteralPath (Join-Path $stateRoot 'RECOVERY.md')
        if ($recovery -notmatch [regex]::Escape("Status phase: $($transition[0])")) {
            throw "Actual recovery helper did not atomically publish transition $($transition[0])."
        }
    }
    Write-Host 'PASS: durable terminal phase/status and every recovery transition execute under StrictMode'

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
        'RESTORE VERIFYONLY',
        "`$backupSha256 = (Get-FileHash",
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
    foreach ($name in @($connectionVariable,$attestationVariable) + $flags + $reviewVariables) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    foreach ($path in $temporaryRoots) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
}

Write-Host 'All disposable development reset offline safety refusals passed.'
