[CmdletBinding()]
param(
    [switch]$SkipInstall,
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$mobileRoot = Join-Path $repoRoot 'apps\mobile'
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $repoRoot '.artifacts\mobile-pos\recovery-rehearsal'
}
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$runDirectory = Join-Path $EvidenceDirectory $stamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

function Resolve-Npm {
    $command = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command npm -ErrorAction SilentlyContinue }
    if ($command) { return $command.Source }

    $userProfile = [Environment]::GetFolderPath('UserProfile')
    foreach ($candidate in @(
        (Join-Path $userProfile '.codex\tools\node-portable\npm.cmd'),
        (Join-Path $userProfile 'AppData\Roaming\npm\npm.cmd'),
        (Join-Path $env:ProgramFiles 'nodejs\npm.cmd')
    )) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            $nodeDirectory = Split-Path -Parent $candidate
            [Environment]::SetEnvironmentVariable('PATH', "$nodeDirectory;$env:PATH", 'Process')
            return $candidate
        }
    }
    throw 'Required npm executable was not found.'
}

function Get-RepoRelativePath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $repoRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ($fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($prefix.Length)
    }
    return $fullPath
}

function Invoke-LoggedNpm([string]$Name, [string[]]$Arguments, [string]$LogPath) {
    Write-Host "`n==> $Name"
    $started = [DateTime]::UtcNow
    Push-Location $mobileRoot
    try {
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = @(& $script:npm @Arguments 2>&1)
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousPreference
        }
        $output | ForEach-Object { $_.ToString() } | Set-Content -LiteralPath $LogPath -Encoding utf8
        if ($exitCode -ne 0) {
            $tail = ($output | Select-Object -Last 30 | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
            throw "$Name failed with exit code $exitCode.$([Environment]::NewLine)$tail"
        }
        return [pscustomobject]@{
            Name = $Name
            Status = 'Passed'
            DurationSeconds = [Math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 2)
            Log = Get-RepoRelativePath $LogPath
        }
    }
    finally {
        Pop-Location
    }
}

$startedUtc = [DateTime]::UtcNow
$script:npm = Resolve-Npm
$stages = [Collections.Generic.List[object]]::new()
$metrics = @()
$failure = $null

try {
    if (-not $SkipInstall) {
        $stages.Add((Invoke-LoggedNpm 'Restore locked mobile dependencies' @('ci', '--no-audit', '--no-fund') (Join-Path $runDirectory 'npm-ci.log')))
    }
    $stages.Add((Invoke-LoggedNpm 'Mobile TypeScript contract' @('run', 'typecheck') (Join-Path $runDirectory 'typecheck.log')))
    $rehearsalLog = Join-Path $runDirectory 'recovery-rehearsal.log'
    $stages.Add((Invoke-LoggedNpm 'Mobile POS recovery rehearsal' @('run', 'test:recovery') $rehearsalLog))

    $metrics = @(
        Get-Content -LiteralPath $rehearsalLog |
            Where-Object { $_ -match 'MOBILE_POS_RECOVERY_METRIC\|' } |
            ForEach-Object { ($_ -replace '^.*?(MOBILE_POS_RECOVERY_METRIC\|)', '$1').Trim() }
    )
    if ($metrics.Count -ne 2) {
        throw "Expected two Mobile POS recovery metrics but found $($metrics.Count)."
    }
}
catch {
    $failure = $_.Exception.Message
}
finally {
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if (-not $git) { $git = Get-Command git -ErrorAction SilentlyContinue }
    $commit = if ($git) { @(& $git.Source -C $repoRoot rev-parse HEAD 2>$null)[0] } else { $null }
    $evidence = [ordered]@{
        SchemaVersion = 1
        StartedAtUtc = $startedUtc.ToString('o')
        CompletedAtUtc = [DateTime]::UtcNow.ToString('o')
        Status = if ($failure) { 'Failed' } else { 'Passed' }
        Commit = $commit
        Scope = 'Host-side queue, interruption, response-loss replay, and recovery rehearsal; no physical-device claim.'
        QueueSize = 1000
        CanonicalizationLimitMilliseconds = 5000
        DispatchLimitMilliseconds = 10000
        Metrics = @($metrics)
        Stages = @($stages)
        Failure = $failure
    }
    $evidencePath = Join-Path $runDirectory 'mobile-pos-recovery-evidence.json'
    $evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8
    Write-Host "`nRecovery evidence: $evidencePath"
}

if ($failure) { throw $failure }
Write-Host 'PASS Mobile POS host recovery rehearsal.'
