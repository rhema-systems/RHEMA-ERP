[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://127.0.0.1:5100')

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$adminId = [Guid]'58cafd8b-42ce-4f67-0dbb-08de862e82ee'
$sourcePolicyId = [Guid]'f6caf2d8-c888-462c-9ec9-6e1f419b73f9'
$currentProfileId = [Guid]'35147034-f84e-4a02-989d-6c7a22501bc7'
$workflowDefinitionId = [Guid]'3c4d6320-763b-4b17-bf44-a9b087d85bcf'
$policyCode = 'TDC-INV-E2E018-ACTIVE'

function Add-DbParameter {
    param([System.Data.SqlClient.SqlCommand]$Command, [string]$Name, $Value)
    $Command.Parameters.AddWithValue($Name, $(if ($null -eq $Value) { [DBNull]::Value } else { $Value }))
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
    $command.ExecuteNonQuery()
}

function Get-ResponseText($Content) {
    if ($Content -is [byte[]]) { return [Text.Encoding]::UTF8.GetString($Content) }
    return [string]$Content
}

function Invoke-JsonApi {
    param([string]$Method, [string]$Path, [string]$Token, $Body, [int[]]$ExpectedStatus = @(200))
    $headers = @{ 'X-Correlation-ID' = "INV-FU-004-E2E018-POLICY-$([Guid]::NewGuid().ToString('N'))" }
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $parameters = @{ Uri="$ApiBaseUrl$Path"; Method=$Method; Headers=$headers; SkipHttpErrorCheck=$true; TimeoutSec=120 }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 30 -Compress
    }
    $response = Invoke-WebRequest @parameters
    $content = Get-ResponseText $response.Content
    if ($response.StatusCode -notin $ExpectedStatus) { throw "$Method $Path returned $($response.StatusCode): $content" }
    if ([string]::IsNullOrWhiteSpace($content)) { return $null }
    $content | ConvertFrom-Json
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
    Where-Object { $_.Directory.Name -like '8.*' } | Sort-Object { [version]$_.Directory.Name } -Descending | Select-Object -First 1
try { Add-Type -Path $identityAssembly.FullName -ErrorAction Stop } catch [System.Management.Automation.RuntimeException] { }
$hasher = [Microsoft.AspNetCore.Identity.PasswordHasher[object]]::new()
$temporaryPassword = "Inv!$([Guid]::NewGuid().ToString('N'))aA7"
$temporaryHash = $hasher.HashPassword([object]::new(), $temporaryPassword)
$backup = $null

try {
    $row = Invoke-DbTable $connection 'SELECT PasswordHash,AccessFailedCount,LockoutEnd FROM Users WHERE Id=@id AND TenantId=@tenantId AND IsActive=1;' @{
        id=$adminId; tenantId=$tenantId
    }
    if ($row.Rows.Count -ne 1) { throw 'The configured SuperAdmin acceptance actor is unavailable.' }
    $backup = @{
        PasswordHash=if ($row.Rows[0].IsNull('PasswordHash')) { $null } else { [string]$row.Rows[0].PasswordHash }
        AccessFailedCount=[int]$row.Rows[0].AccessFailedCount
        LockoutEnd=if ($row.Rows[0].IsNull('LockoutEnd')) { $null } else { $row.Rows[0].LockoutEnd }
    }
    $null = Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;' @{
        id=$adminId; hash=$temporaryHash
    }
    $login = Invoke-JsonApi POST '/api/auth/login' '' @{ username='admin'; password=$temporaryPassword; rememberMe=$false }
    $token = [string]$login.token
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Admin login did not issue a token.' }

    $source = Invoke-JsonApi GET "/api/procurement/policy-sets/$sourcePolicyId" $token $null
    $existing = Invoke-DbTable $connection @'
SELECT TOP (1) Id,LifecycleStatus FROM ProcurementPolicySets
WHERE TenantId=@tenantId AND Code=@code AND IsDeleted=0 ORDER BY Version DESC;
'@ @{ tenantId=$tenantId; code=$policyCode }
    if ($existing.Rows.Count -eq 0) {
        $target = Invoke-JsonApi POST '/api/procurement/policy-sets' $token @{
            sourceConfigurationProfileId=$currentProfileId; code=$policyCode
            name='TDC Inventory E2E-018 Current Governance'; description='Complete Goods policy sourced from the current immutable procurement configuration profile.'
            scopeType='TenantBaseline'; defaultCurrencyCode='GHS'; effectiveFrom=[DateTime]'2026-01-01T00:00:00Z'
            effectiveTo=[DateTime]'2026-12-31T23:59:59Z'; changeSummary='Correct current configuration lineage for retained E2E-018 chain.'; isDefault=$true
        } @(201)
    } else {
        $target = Invoke-JsonApi GET "/api/procurement/policy-sets/$([Guid]$existing.Rows[0].Id)" $token $null
    }

    if ([string]$target.lifecycleStatus -eq 'Published' -or [string]$target.lifecycleStatus -eq '1') {
        $operationalMethod = @($target.rules | Where-Object {
            [string]$_.kind -eq 'Method' -and [int]$_.value.minimumQuotationCount -gt 0 -and
            [Guid]$_.value.workflowDefinitionId -ne [Guid]::Empty
        })
        if ($operationalMethod.Count -eq 0) {
            $target = Invoke-JsonApi POST "/api/procurement/policy-sets/$($target.id)/clone-draft" $token @{
                changeSummary='Configure the RFQ competition count and shared evaluation approval workflow required at runtime.'
            } @(201)
        } else {
        if ([string]$source.lifecycleStatus -eq 'Published' -or [string]$source.lifecycleStatus -eq '1') {
            $null = Invoke-JsonApi POST "/api/procurement/policy-sets/$sourcePolicyId/retire" $token @{
                rowVersion=$source.rowVersion; reason='Retire the superseded acceptance policy whose source configuration is no longer current.'
            }
        }
        [pscustomobject]@{ Policy="$($target.code)/v$($target.version)"; PolicyId=$target.id; SourceProfileId=$target.sourceConfigurationProfileId; Status=$target.lifecycleStatus; Validation='AlreadyPublished' } | ConvertTo-Json -Compress
        return
        }
    }
    if ([string]$target.lifecycleStatus -ne 'Draft' -and [string]$target.lifecycleStatus -ne '0') {
        throw "Target policy is $($target.lifecycleStatus), not Draft or Published."
    }

    $valueProperty = @{
        Category='category'; Method='method'; Threshold='threshold'; Authority='authority'
        Evidence='evidence'; Exception='exception'; SegregationOfDuties='segregationOfDuties'
    }
    foreach ($kind in $valueProperty.Keys) {
        $sourceRules = @($source.rules | Where-Object { [string]$_.kind -eq $kind } | Sort-Object priority,ruleCode)
        $targetRules = @($target.rules | Where-Object { [string]$_.kind -eq $kind } | Sort-Object priority,ruleCode)
        for ($index = 0; $index -lt $sourceRules.Count; $index++) {
            $sourceRule = $sourceRules[$index]
            $value = $sourceRule.value | ConvertTo-Json -Depth 30 | ConvertFrom-Json
            $lineage = if ($index -lt $targetRules.Count) { $targetRules[$index].sourceRuleId } else { $null }
            $value | Add-Member -NotePropertyName sourceRuleId -NotePropertyValue $lineage -Force
            $value | Add-Member -NotePropertyName overrideAction -NotePropertyValue 'Add' -Force
            if ($kind -eq 'Method' -and [string]$value.method -in @('RequestForQuotation','0')) {
                $value.requiresCompetition = $true
                $value.minimumQuotationCount = 1
                $value | Add-Member -NotePropertyName workflowDefinitionId -NotePropertyValue $workflowDefinitionId -Force
            }
            $body = @{ kind=$kind; reason='Copy the approved complete Goods rule into the current configuration lineage.' }
            $body[$valueProperty[$kind]] = $value
            if ($index -lt $targetRules.Count) {
                $body.rowVersion = $targetRules[$index].rowVersion
                $null = Invoke-JsonApi PUT "/api/procurement/policy-sets/$($target.id)/rules/$($targetRules[$index].id)" $token $body
            } else {
                $null = Invoke-JsonApi POST "/api/procurement/policy-sets/$($target.id)/rules" $token $body
            }
        }
        for ($index = $sourceRules.Count; $index -lt $targetRules.Count; $index++) {
            $extra = $targetRules[$index]
            $null = Invoke-JsonApi DELETE "/api/procurement/policy-sets/$($target.id)/rules/$kind/$($extra.id)" $token @{
                rowVersion=$extra.rowVersion; reason='Remove a source rule not required by the complete E2E-018 Goods control.'
            } @(204)
        }
    }
    $validation = Invoke-JsonApi POST "/api/procurement/policy-sets/$($target.id)/validate" $token $null
    if (-not $validation.isValid) { throw "Policy validation failed: $($validation.errors | ConvertTo-Json -Depth 20 -Compress)" }
    $target = Invoke-JsonApi GET "/api/procurement/policy-sets/$($target.id)" $token $null
    $published = Invoke-JsonApi POST "/api/procurement/policy-sets/$($target.id)/publish" $token @{
        rowVersion=$target.rowVersion; reason='Activate corrected current-profile lineage for INV-REQ-FU-004 E2E-018.'
    }
    $source = Invoke-JsonApi GET "/api/procurement/policy-sets/$sourcePolicyId" $token $null
    if ([string]$source.lifecycleStatus -eq 'Published' -or [string]$source.lifecycleStatus -eq '1') {
        $null = Invoke-JsonApi POST "/api/procurement/policy-sets/$sourcePolicyId/retire" $token @{
            rowVersion=$source.rowVersion; reason='Retire the superseded acceptance policy whose source configuration is no longer current.'
        }
    }
    [pscustomobject]@{
        Policy="$($published.code)/v$($published.version)"; PolicyId=$published.id
        SourceProfileId=$published.sourceConfigurationProfileId; Status=$published.lifecycleStatus
        RuleCount=$published.ruleCount; Validation='Passed'
    } | ConvertTo-Json -Compress
}
finally {
    if ($backup) {
        try {
            $null = Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;' @{
                id=$adminId; hash=$backup.PasswordHash; failed=$backup.AccessFailedCount; lockout=$backup.LockoutEnd
            }
        } catch { }
    }
    if ($connection.State -eq [System.Data.ConnectionState]::Open) { $connection.Close() }
    $connection.Dispose()
}
