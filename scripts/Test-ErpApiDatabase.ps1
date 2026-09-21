<#
.SYNOPSIS
    Tells you which database the API on a port is really serving. Run it before any demo.

.DESCRIPTION
    A row count cannot answer this any more: the development database and the demo database both
    carry TDC staff numbers, and other teams' fixtures move the totals every time master is merged.
    This asks the only question that settles it -- does the API recognise an employee id that exists
    only in the named database? Employee ids are minted per rebuild, so the answer is yes for exactly
    one database.

.EXAMPLE
    powershell -File .\scripts\Test-ErpApiDatabase.ps1                 # is port 5000 on ErpSystemDB_UAT?
    powershell -File .\scripts\Test-ErpApiDatabase.ps1 -Database ErpSystemDB -Port 5010
#>
[CmdletBinding()]
param(
    [string]$Database = 'ErpSystemDB_UAT',
    [int]$Port = 5000,
    [string]$Server = '.',
    [string]$UserId = 'sa',
    [string]$Password
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

. (Join-Path $PSScriptRoot 'ErpDbCredential.ps1')
$credential = Resolve-ErpDbCredential -UserId $UserId -Password $Password -RepoRoot $repoRoot

. (Join-Path $PSScriptRoot 'ErpApiProbe.ps1')
$probe = Test-ErpApiServesDatabase -Port $Port -Database $Database -Server $Server -UserId $credential.UserId -Password $credential.Password

Write-Host ""
if ($probe.Serves) {
    Write-Host "  YES  The API on port $Port is serving $Database." -ForegroundColor Green
    Write-Host "       ($($probe.Reason))" -ForegroundColor DarkGray
    Write-Host ""
    exit 0
}
else {
    Write-Host "  NO   The API on port $Port is NOT serving $Database." -ForegroundColor Red
    Write-Host "       $($probe.Reason)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Stop it and start one against the demo database:" -ForegroundColor Yellow
    Write-Host "    Get-CimInstance Win32_Process -Filter `"Name='dotnet.exe'`" | Where-Object { `$_.CommandLine -like '*ErpSystem.Api*' } | ForEach-Object { Stop-Process -Id `$_.ProcessId -Force }"
    Write-Host "    powershell -File .\scripts\Start-ErpApi.ps1 -Database Uat"
    Write-Host ""
    exit 1
}
