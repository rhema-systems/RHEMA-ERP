[CmdletBinding()]
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$secretLines = @(dotnet user-secrets list --project (Join-Path $repositoryPath 'src/ErpSystem.Api/ErpSystem.Api.csproj') --json)
if ($LASTEXITCODE -ne 0) { throw 'Cannot load local API database configuration.' }
$jsonStart = [Array]::FindIndex($secretLines, [Predicate[string]]{ param($line) $line.Trim() -eq '{' })
$jsonEnd = [Array]::FindLastIndex($secretLines, [Predicate[string]]{ param($line) $line.Trim() -eq '}' })
if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) { throw 'Local configuration could not be parsed.' }
$settings = (($secretLines[$jsonStart..$jsonEnd]) -join "`n") | ConvertFrom-Json
Add-Type -AssemblyName System.Data
$connectionBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new([string]$settings.'ConnectionStrings:DefaultConnection')
if ($connectionBuilder['Initial Catalog'] -ne 'RhemaERP') { throw 'Unexpected source database.' }
$repairSql = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Repair-LocalUatInspectionApprover.sql') -Raw

foreach ($databaseName in @('RhemaERP', 'RhemaERP_PO_Rehearsal_20260909')) {
    $connectionBuilder['Initial Catalog'] = $databaseName
    $connection = [System.Data.SqlClient.SqlConnection]::new($connectionBuilder.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 45
        $command.CommandText = $repairSql
        [void]$command.Parameters.Add('@Apply', [System.Data.SqlDbType]::Bit)
        $command.Parameters['@Apply'].Value = $Apply.IsPresent
        $result = [string]$command.ExecuteScalar()
        Write-Output $result
    }
    finally { $connection.Dispose() }
}
