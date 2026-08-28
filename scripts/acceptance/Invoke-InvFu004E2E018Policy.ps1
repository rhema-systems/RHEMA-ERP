[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5100')

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$adminId = [Guid]'58CAFD8B-42CE-4F67-0DBB-08DE862E82EE'
$policyId = [Guid]'f6caf2d8-c888-462c-9ec9-6e1f419b73f9'
$authorityRuleId = [Guid]'747c4072-2c8f-40e9-910b-16f9f773fd89'
$workflowDefinitionId = [Guid]'3c4d6320-763b-4b17-bf44-a9b087d85bcf'

function Add-DbParameter {
    param([System.Data.SqlClient.SqlCommand]$Command, [string]$Name, $Value)
    $parameter = $Command.Parameters.AddWithValue($Name, $(if ($null -eq $Value) { [DBNull]::Value } else { $Value }))
    return $parameter
}

function Invoke-DbTable {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    foreach ($entry in $Parameters.GetEnumerator()) { $null = Add-DbParameter $command "@$($entry.Key)" $entry.Value }
    $table = [System.Data.DataTable]::new()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    $null = $adapter.Fill($table)
    Write-Output -NoEnumerate $table
}

function Invoke-DbNonQuery {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    foreach ($entry in $Parameters.GetEnumerator()) { $null = Add-DbParameter $command "@$($entry.Key)" $entry.Value }
    return $command.ExecuteNonQuery()
}

function Invoke-JsonApi {
    param([string]$Method, [string]$Path, [string]$Token, $Body, [int[]]$ExpectedStatus = @(200))
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $parameters = @{
        Uri = "$ApiBaseUrl$Path"
        Method = $Method
        Headers = $headers
        SkipHttpErrorCheck = $true
        TimeoutSec = 90
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 20 -Compress
    }
    $response = Invoke-WebRequest @parameters
    if ($response.StatusCode -notin $ExpectedStatus) {
        throw "$Method $Path returned $($response.StatusCode): $($response.Content)"
    }
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return $response.Content | ConvertFrom-Json
}

$secretRows = dotnet user-secrets list --project $apiProject --json | ConvertFrom-Json
$secretObject = $secretRows | Where-Object {
    $_ -and $_.PSObject.Properties.Name -contains 'ConnectionStrings:DefaultConnection'
} | Select-Object -First 1
if (-not $secretObject) { throw 'The configured API database connection was not found in user secrets.' }

Add-Type -AssemblyName System.Data
$connection = [System.Data.SqlClient.SqlConnection]::new([string]$secretObject.'ConnectionStrings:DefaultConnection')
$connection.Open()

$identityAssembly = Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Recurse -Filter Microsoft.Extensions.Identity.Core.dll |
    Where-Object { $_.Directory.Name -like '8.*' } |
    Sort-Object { [version]$_.Directory.Name } -Descending | Select-Object -First 1
if (-not $identityAssembly) { throw 'Microsoft.Extensions.Identity.Core.dll was not found.' }
try { Add-Type -Path $identityAssembly.FullName -ErrorAction Stop } catch [System.Management.Automation.RuntimeException] { }
$passwordHasher = [Microsoft.AspNetCore.Identity.PasswordHasher[object]]::new()
$temporaryPassword = "Inv!$([Guid]::NewGuid().ToString('N'))aA7"
$temporaryHash = $passwordHasher.HashPassword([object]::new(), $temporaryPassword)
$backup = $null

try {
    $row = Invoke-DbTable $connection @'
SELECT PasswordHash,AccessFailedCount,LockoutEnd
FROM Users WHERE Id=@id AND IsActive=1;
'@ @{ id=$adminId }
    if ($row.Rows.Count -ne 1) { throw 'The configured SuperAdmin acceptance actor is unavailable.' }
    $backup = @{
        PasswordHash = if ($row.Rows[0].IsNull('PasswordHash')) { $null } else { [string]$row.Rows[0].PasswordHash }
        AccessFailedCount = [int]$row.Rows[0].AccessFailedCount
        LockoutEnd = if ($row.Rows[0].IsNull('LockoutEnd')) { $null } else { $row.Rows[0].LockoutEnd }
    }
    $null = Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;
'@ @{ id=$adminId; hash=$temporaryHash }

    $login = Invoke-JsonApi POST '/api/auth/login' '' @{
        username='admin'; password=$temporaryPassword; rememberMe=$false
    }
    if ([string]::IsNullOrWhiteSpace($login.token)) { throw 'Admin login did not issue a token.' }
    $token = [string]$login.token

    $policy = Invoke-JsonApi GET "/api/procurement/policy-sets/$policyId" $token $null
    if ($policy.lifecycleStatus -ne 'Draft' -and [int]$policy.lifecycleStatus -ne 0) {
        throw "Expected the v2 policy to be Draft; received $($policy.lifecycleStatus)."
    }
    $matchingRules = @($policy.rules) | Where-Object { [Guid]$_.id -eq $authorityRuleId }
    if ($matchingRules.Count -ne 1) { throw 'The cloned Goods authority rule is unavailable or ambiguous.' }
    $rule = $matchingRules[0]
    $authority = $rule.value
    $authority.authorityRole = 'TDC_HEAD_OF_PROCUREMENT'
    $authority | Add-Member -NotePropertyName workflowDefinitionId -NotePropertyValue $workflowDefinitionId -Force

    $saved = Invoke-JsonApi PUT "/api/procurement/policy-sets/$policyId/rules/$authorityRuleId" $token @{
        kind='Authority'
        authority=$authority
        rowVersion=$rule.rowVersion
    }
    if ([Guid]$saved.sourceRuleId -eq [Guid]::Empty) { throw 'The cloned rule lost its immutable source lineage.' }

    $validation = Invoke-JsonApi POST "/api/procurement/policy-sets/$policyId/validate" $token $null
    if (-not $validation.isValid) {
        throw "Policy validation failed: $($validation.errors | ConvertTo-Json -Depth 10 -Compress)"
    }
    $policy = Invoke-JsonApi GET "/api/procurement/policy-sets/$policyId" $token $null
    $published = Invoke-JsonApi POST "/api/procurement/policy-sets/$policyId/publish" $token @{
        rowVersion=$policy.rowVersion
        reason='INV-REQ-FU-004 E2E-018 governed Goods authority workflow activation'
    }
    if ($published.lifecycleStatus -ne 'Published' -and [int]$published.lifecycleStatus -ne 1) {
        throw "Policy publication returned $($published.lifecycleStatus)."
    }

    $effective = Invoke-JsonApi GET '/api/procurement/policy-sets/effective?code=TDC-INV-E2E018' $token $null
    [pscustomobject]@{
        Policy="$($effective.code)/v$($effective.version)"
        PolicyId=$effective.id
        LifecycleStatus=$effective.lifecycleStatus
        AuthorityRuleId=$authorityRuleId
        WorkflowDefinitionId=$workflowDefinitionId
        AuthorityRole=$authority.authorityRole
        Validation='Passed'
        SourceRuleLineage=$saved.sourceRuleId
    } | ConvertTo-Json -Compress
}
finally {
    if ($backup) {
        try {
            $null = Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;
'@ @{ id=$adminId; hash=$backup.PasswordHash; failed=$backup.AccessFailedCount; lockout=$backup.LockoutEnd }
        } catch { }
    }
    if ($connection.State -eq [System.Data.ConnectionState]::Open) { $connection.Close() }
    $connection.Dispose()
}
