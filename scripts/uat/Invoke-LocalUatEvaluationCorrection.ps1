[CmdletBinding()]
param(
    [ValidateSet('Preview','Rehearse','Apply')][string]$Mode = 'Preview',
    [string]$ExpectedFingerprint
)
# Exact operator-approved local UAT repair. Never deploy, seed or generalize.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:COMPUTERNAME -ne 'RHEMA-MICHAEL') { throw 'Local UAT host only.' }
$taskSettings = Get-Content -Raw -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/10483e62-e5b2-4652-8963-50f9500d3d5d/secrets.json') | ConvertFrom-Json
$taskConnection = [Data.SqlClient.SqlConnection]::new([string]$taskSettings.'ConnectionStrings:DefaultConnection')
$taskSettings = $null
try {
    $taskConnection.Open()
    $taskCommand = $taskConnection.CreateCommand()
    $taskCommand.CommandTimeout = 90
    $taskCommand.CommandText = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'local-uat-evaluation-correction-20260905.sql')
    [void]$taskCommand.Parameters.Add('@Mode',[Data.SqlDbType]::NVarChar,20)
    $taskCommand.Parameters['@Mode'].Value = $Mode
    [void]$taskCommand.Parameters.Add('@ExpectedFingerprint',[Data.SqlDbType]::VarChar,64)
    $taskCommand.Parameters['@ExpectedFingerprint'].Value = if ($ExpectedFingerprint) { $ExpectedFingerprint } else { [DBNull]::Value }
    $taskReader = $taskCommand.ExecuteReader()
    try {
        while ($taskReader.Read()) {
            $taskRow = [ordered]@{}
            for ($taskIndex=0; $taskIndex -lt $taskReader.FieldCount; $taskIndex++) {
                $taskRow[$taskReader.GetName($taskIndex)] = if ($taskReader.IsDBNull($taskIndex)) { $null } else { $taskReader.GetValue($taskIndex) }
            }
            [pscustomobject]$taskRow | ConvertTo-Json -Depth 8
        }
    } finally { $taskReader.Dispose(); $taskCommand.Dispose() }
} finally { $taskConnection.Dispose() }
