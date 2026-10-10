[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SqlServer,
    [Parameter(Mandatory)][string]$DatabaseName,
    [Parameter(Mandatory)][Guid]$TenantId,
    [Parameter(Mandatory)][DateTime]$FromUtc,
    [Parameter(Mandatory)][DateTime]$ToUtc,
    [switch]$AllowNoCompletedSales,
    [string]$EvidenceDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sqlPath = Join-Path $PSScriptRoot 'MobilePosAccountingReconciliation.sql'

if ($SqlServer -notmatch '^[A-Za-z0-9._,\\-]+$') { throw 'SqlServer contains unsupported characters.' }
if ($DatabaseName -notmatch '^[A-Za-z0-9_-]+$') { throw 'DatabaseName contains unsupported characters.' }
if ($DatabaseName -in @('master', 'model', 'msdb', 'tempdb')) { throw 'A business database is required.' }
if ($TenantId -eq [Guid]::Empty) { throw 'A non-empty tenant ID is required.' }
$from = $FromUtc.ToUniversalTime()
$to = $ToUtc.ToUniversalTime()
if ($from -ge $to) { throw 'FromUtc must be earlier than ToUtc.' }

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) { throw 'sqlcmd is required for the accounting reconciliation.' }

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $repoRoot '.artifacts\mobile-pos\accounting-reconciliation'
}
$runDirectory = Join-Path ([IO.Path]::GetFullPath($EvidenceDirectory)) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$outputPath = Join-Path $runDirectory 'reconciliation-output.txt'
$evidencePath = Join-Path $runDirectory 'reconciliation.json'
$started = [DateTime]::UtcNow
$exitCode = -1
$failure = $null

try {
    $arguments = @(
        '-S', $SqlServer,
        '-d', $DatabaseName,
        '-E', '-C', '-b', '-r', '1', '-W', '-s', '|',
        '-i', $sqlPath,
        '-v',
        "TenantId=$($TenantId.ToString('D'))",
        "FromUtc=$($from.ToString('o'))",
        "ToUtc=$($to.ToString('o'))",
        "AllowNoCompletedSales=$([int][bool]$AllowNoCompletedSales)",
        "ExpectedDatabase=$DatabaseName"
    )
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = @(& $sqlcmd.Source @arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    $output | ForEach-Object { $_.ToString() } | Set-Content -LiteralPath $outputPath -Encoding utf8
    if ($exitCode -ne 0) {
        $failure = (($output | Select-Object -Last 40 | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine)
    }
}
catch {
    $failure = $_.Exception.Message
}
finally {
    $evidence = [ordered]@{
        Gate = 'mobile-pos-accounting-reconciliation'
        Passed = $exitCode -eq 0 -and [string]::IsNullOrWhiteSpace($failure)
        StartedUtc = $started.ToString('o')
        FinishedUtc = [DateTime]::UtcNow.ToString('o')
        SqlServer = $SqlServer
        DatabaseName = $DatabaseName
        TenantId = $TenantId.ToString('D')
        FromUtc = $from.ToString('o')
        ToUtc = $to.ToString('o')
        AllowNoCompletedSales = [bool]$AllowNoCompletedSales
        SqlSha256 = (Get-FileHash -LiteralPath $sqlPath -Algorithm SHA256).Hash
        ExitCode = $exitCode
        Failure = $failure
        Output = $outputPath
    }
    $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidencePath -Encoding utf8
    Write-Host "EVIDENCE $evidencePath"
}

if ($failure) { Write-Error $failure; exit 1 }
Write-Host 'PASS mobile-pos-accounting-reconciliation'
