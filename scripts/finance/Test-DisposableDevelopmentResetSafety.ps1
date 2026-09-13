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

    $collisionPath = Join-Path ([System.IO.Path]::GetTempPath()) "RhemaERP_DISPOSABLE_RESET_COLLISION_$([Guid]::NewGuid().ToString('N')).bak"
    try {
        $first = [System.IO.File]::Open($collisionPath,[System.IO.FileMode]::CreateNew,[System.IO.FileAccess]::Write,[System.IO.FileShare]::None)
        $first.Dispose()
        $refused = $false
        try {
            $second = [System.IO.File]::Open($collisionPath,[System.IO.FileMode]::CreateNew,[System.IO.FileAccess]::Write,[System.IO.FileShare]::None)
            $second.Dispose()
        }
        catch [System.IO.IOException] { $refused = $true }
        if (-not $refused) { throw 'CreateNew did not refuse the occupied backup path.' }
    }
    finally { if (Test-Path -LiteralPath $collisionPath) { Remove-Item -LiteralPath $collisionPath -Force } }
    Write-Host 'PASS: atomic backup collision is refused'

    $text = Get-Content -Raw -LiteralPath $script
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
        'Disposable RhemaERP source changed between capture and verified backup',
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
    $identityIndex = $reset.IndexOf('DISPOSABLE_RESET_IDENTITY_DRIFT', [StringComparison]::Ordinal)
    $singleUserIndex = $reset.IndexOf('ALTER DATABASE [RhemaERP] SET SINGLE_USER', [StringComparison]::Ordinal)
    if ($singleUserIndex - $identityIndex -gt 350) {
        throw 'Exact database identity is not rechecked immediately before SINGLE_USER/drop.'
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
