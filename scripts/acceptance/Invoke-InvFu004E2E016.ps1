[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5100'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$isolationTenantId = [Guid]'D6040000-0000-4000-8000-000000000001'
$adminId = [Guid]'58CAFD8B-42CE-4F67-0DBB-08DE862E82EE'
$isolationActorId = [Guid]'35B82323-8EA1-498C-C401-08DEED5E48D0'
$storesOfficerRoleId = [Guid]'10958480-D251-4C77-8DC0-5823889C2270'

function Add-DbParameter {
    param([System.Data.SqlClient.SqlCommand]$Command, [string]$Name, $Value)
    $parameter = $Command.Parameters.AddWithValue($Name, $(if ($null -eq $Value) { [DBNull]::Value } else { $Value }))
    return $parameter
}

function Invoke-DbScalar {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    foreach ($entry in $Parameters.GetEnumerator()) { $null = Add-DbParameter $command "@$($entry.Key)" $entry.Value }
    return $command.ExecuteScalar()
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
    $parameters = @{ Uri = "$ApiBaseUrl$Path"; Method = $Method; Headers = $headers; SkipHttpErrorCheck = $true; TimeoutSec = 90 }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }
    $response = Invoke-WebRequest @parameters
    if ($response.StatusCode -notin $ExpectedStatus) {
        throw "$Method $Path returned $($response.StatusCode): $($response.Content)"
    }
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return $response.Content | ConvertFrom-Json
}

function Login-Actor([string]$UserName, [string]$Password) {
    $response = Invoke-JsonApi POST '/api/auth/login' '' @{ username=$UserName; password=$Password; rememberMe=$false }
    if ([string]::IsNullOrWhiteSpace($response.token)) { throw "Login did not issue a token for $UserName." }
    return [string]$response.token
}

$secretOutput = @(dotnet user-secrets list --project $apiProject --json)
$jsonStart = [Array]::FindIndex($secretOutput, [Predicate[string]]{ param($line) $line.Trim() -eq '{' })
$jsonEnd = [Array]::FindLastIndex($secretOutput, [Predicate[string]]{ param($line) $line.Trim() -eq '}' })
if ($jsonStart -lt 0 -or $jsonEnd -le $jsonStart) { throw 'The user-secrets command did not return a JSON object.' }
$secretRows = (($secretOutput[$jsonStart..$jsonEnd]) -join "`n") | ConvertFrom-Json
$secretObject = $secretRows | Where-Object { $_ -and $_.PSObject.Properties.Name -contains 'ConnectionStrings:DefaultConnection' } | Select-Object -First 1
if (-not $secretObject) { throw 'The configured API database connection was not found in user secrets.' }

Add-Type -AssemblyName System.Data
$connection = [System.Data.SqlClient.SqlConnection]::new([string]$secretObject.'ConnectionStrings:DefaultConnection')
$connection.Open()

$identityAssembly = Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' -Recurse -Filter Microsoft.Extensions.Identity.Core.dll |
    Where-Object { $_.Directory.Name -like '8.*' } | Sort-Object { [version]$_.Directory.Name } -Descending | Select-Object -First 1
if (-not $identityAssembly) { throw 'Microsoft.Extensions.Identity.Core.dll was not found.' }
try { Add-Type -Path $identityAssembly.FullName -ErrorAction Stop } catch [System.Management.Automation.RuntimeException] { }
$passwordHasher = [Microsoft.AspNetCore.Identity.PasswordHasher[object]]::new()
$temporaryPassword = "Inv!$([Guid]::NewGuid().ToString('N'))aA7"
$temporaryHash = $passwordHasher.HashPassword([object]::new(), $temporaryPassword)
$backups = @{}
$createdResponsibilityAssignmentIds = [System.Collections.Generic.List[Guid]]::new()
$createdAdminRoleMembership = $false

try {
    foreach ($actor in @(@{Id=$adminId;UserName='admin'},@{Id=$isolationActorId;UserName='estate.officer1'})) {
        $row = Invoke-DbTable $connection 'SELECT PasswordHash,AccessFailedCount,LockoutEnd,TenantId,EmployeeId FROM Users WHERE Id=@id AND IsActive=1;' @{id=$actor.Id}
        if ($row.Rows.Count -ne 1) { throw "Acceptance actor $($actor.UserName) is unavailable." }
        $backups[$actor.Id] = @{
            PasswordHash=if($row.Rows[0].IsNull('PasswordHash')){$null}else{[string]$row.Rows[0].PasswordHash}
            AccessFailedCount=[int]$row.Rows[0].AccessFailedCount
            LockoutEnd=if($row.Rows[0].IsNull('LockoutEnd')){$null}else{$row.Rows[0].LockoutEnd}
            TenantId=[Guid]$row.Rows[0].TenantId
            EmployeeId=if($row.Rows[0].IsNull('EmployeeId')){$null}else{[Guid]$row.Rows[0].EmployeeId}
        }
        $null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;' @{id=$actor.Id;hash=$temporaryHash}
    }

    $adminToken = Login-Actor 'admin' $temporaryPassword

    # Report reproduction is tested through the same warehouse-scope control as
    # the operational UI. The temporary grant is acceptance-only and is removed
    # in finally; audit-governance administration alone must not bypass it.
    $adminHasStoresRole=[int](Invoke-DbScalar $connection 'SELECT COUNT(1) FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$adminId;roleId=$storesOfficerRoleId})
    if($adminHasStoresRole -eq 0){
        $null=Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{userId=$adminId;roleId=$storesOfficerRoleId}
        $createdAdminRoleMembership=$true
    }
    $adminAssignmentId=[Guid]::NewGuid()
    $null=Invoke-DbNonQuery $connection @'
INSERT INTO ProcurementResponsibilityAssignments
    (Id,UserId,RoleId,RoleName,WarehouseScopeMode,LocationScopeMode,EffectiveFrom,EffectiveTo,IsActive,Reason,
     CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
VALUES
    (@id,@userId,@roleId,'TDC_STORES_OFFICER',1,1,SYSUTCDATETIME(),NULL,1,
     'Temporary INV-REQ-FU-004 E2E-016 report-reproduction grant',SYSUTCDATETIME(),'INV-REQ-FU-004',NULL,0,@tenantId);
'@ @{id=$adminAssignmentId;userId=$adminId;roleId=$storesOfficerRoleId;tenantId=$tenantId}
    $createdResponsibilityAssignmentIds.Add($adminAssignmentId)
    $adminToken = Login-Actor 'admin' $temporaryPassword
    $transfer = Invoke-DbTable $connection @'
SELECT TOP (1) Id,TransferNumber
FROM InventoryTransfers
WHERE TenantId=@tenantId AND Status=7 AND IsDeleted=0
  AND Notes LIKE '%INV-REQ-FU-004 runtime acceptance%'
ORDER BY CreatedAt DESC;
'@ @{tenantId=$tenantId}
    if ($transfer.Rows.Count -ne 1) { throw 'Run the verified E2E-011 acceptance before E2E-016.' }
    $transferId=[Guid]$transfer.Rows[0].Id
    $transferNumber=[string]$transfer.Rows[0].TransferNumber
    $event = Invoke-DbTable $connection @'
SELECT TOP (1) e.Id,e.OccurredAtUtc,e.ActorUserId,e.ActorName,e.ActorRolesJson,e.IntegrityHash,
       l.Id LifecycleEventId,l.RequestKey LifecycleRequestKey,l.Reason LifecycleReason
FROM ProcurementControlEvents e
LEFT JOIN AuditRecordLifecycleEvents l
  ON l.TenantId=e.TenantId AND l.StoreKey='procurement-inventory-control-event'
 AND l.RecordId=e.Id AND l.Action=2 AND l.IsDeleted=0
WHERE e.TenantId=@tenantId AND e.SourceType='InventoryTransfer' AND e.SourceId=@sourceId AND e.IsDeleted=0
ORDER BY CASE WHEN l.Id IS NULL THEN 1 ELSE 0 END,e.OccurredAtUtc DESC,e.Id DESC;
'@ @{tenantId=$tenantId;sourceId=$transferId}
    if ($event.Rows.Count -ne 1) { throw 'The completed transfer has no immutable control event.' }
    $eventId=[Guid]$event.Rows[0].Id
    $eventOccurred=[DateTime]$event.Rows[0].OccurredAtUtc

    $initial=Invoke-JsonApi GET "/api/admin/audit-governance/records/procurement-inventory-control-event/$eventId" $adminToken $null
    if($initial.retentionDays -lt 2555 -or -not $initial.isImmutable){throw 'Seven-year immutable retention was not reported.'}
    if([DateTime]$initial.retainUntilUtc -ne $eventOccurred.AddDays([int]$initial.retentionDays)){throw 'Retention horizon does not derive from source occurrence time.'}

    if($event.Rows[0].IsNull('LifecycleEventId')){
        $archiveKey="e2e016-archive-$([Guid]::NewGuid().ToString('N'))"
        $archiveReason='E2E-016 statutory archive retrieval proof'
        $archive=Invoke-JsonApi POST "/api/admin/audit-governance/records/procurement-inventory-control-event/$eventId/archive" $adminToken @{
            requestKey=$archiveKey;reason=$archiveReason;correlationId="E2E-016-$eventId"
        }
    } else {
        $archiveKey=[string]$event.Rows[0].LifecycleRequestKey
        $archiveReason=[string]$event.Rows[0].LifecycleReason
        $archive=$initial
    }
    if(-not $archive.isArchived -or $archive.archiveReference -notlike 'audit-archive://*'){throw 'Archive transition did not produce a governed archive reference.'}
    if(-not $archive.actions[-1].integrityValid){throw 'Archive hash-chain validation failed.'}

    $replay=Invoke-JsonApi POST "/api/admin/audit-governance/records/procurement-inventory-control-event/$eventId/archive" $adminToken @{
        requestKey=$archiveKey;reason=$archiveReason;correlationId="E2E-016-$eventId"
    }
    if(@($replay.actions).Count -ne 1){throw 'Archive replay created a duplicate lifecycle event.'}
    $retrieved=Invoke-JsonApi GET "/api/admin/audit-governance/records/procurement-inventory-control-event/$eventId" $adminToken $null
    if(-not $retrieved.isArchived -or -not $retrieved.actions[-1].integrityValid){throw 'Archived record was not retrievable with a valid hash.'}

    $actions=Invoke-DbTable $connection 'SELECT COUNT(*) ActionCount,COUNT(DISTINCT ActorUserId) ActorCount FROM InventoryTransferActions WHERE TenantId=@tenantId AND InventoryTransferId=@transferId AND IsDeleted=0;' @{tenantId=$tenantId;transferId=$transferId}
    if([int]$actions.Rows[0].ActionCount -ne 6 -or [int]$actions.Rows[0].ActorCount -lt 5){throw 'Actor or approval-chain reproduction failed.'}
    $evidenceCount=[int](Invoke-DbScalar $connection @'
SELECT COUNT(*) FROM InventoryTransferDiscrepancyEvidence e
JOIN InventoryTransferDiscrepancies d ON d.Id=e.InventoryTransferDiscrepancyId AND d.TenantId=e.TenantId
JOIN CentralDocumentVersions v ON v.Id=e.CentralDocumentVersionId AND v.TenantId=e.TenantId
WHERE d.TenantId=@tenantId AND d.InventoryTransferId=@transferId AND v.IsDeleted=0;
'@ @{tenantId=$tenantId;transferId=$transferId})
    if($evidenceCount -lt 1){throw 'Central-DMS evidence cannot be reproduced from the archived transaction.'}

    $detail=Invoke-JsonApi GET "/api/inventory/transfers/$transferId" $adminToken $null
    if($detail.transferNumber -ne $transferNumber -or $detail.status -ne 'Completed'){throw 'The operational report read-back cannot reproduce the completed transfer.'}

    $originalTenant=$backups[$isolationActorId].TenantId
    $null=Invoke-DbNonQuery $connection 'UPDATE Users SET TenantId=@tenantId,EmployeeId=NULL WHERE Id=@id;' @{tenantId=$isolationTenantId;id=$isolationActorId}
    $isolationToken=Login-Actor 'estate.officer1' $temporaryPassword
    $null=Invoke-JsonApi GET "/api/admin/audit-governance/records/procurement-inventory-control-event/$eventId" $isolationToken $null @(403,404)
    $null=Invoke-DbNonQuery $connection 'UPDATE Users SET TenantId=@tenantId,EmployeeId=@employeeId WHERE Id=@id;' @{tenantId=$originalTenant;employeeId=$backups[$isolationActorId].EmployeeId;id=$isolationActorId}

    [pscustomobject]@{
        ControlEventId=$eventId;TransferNumber=$transferNumber;RetentionDays=[int]$retrieved.retentionDays
        ArchiveReference=[string]$retrieved.archiveReference;HashValid=[bool]$retrieved.actions[-1].integrityValid
        EvidenceLinks=$evidenceCount;ActionCount=[int]$actions.Rows[0].ActionCount;DistinctActors=[int]$actions.Rows[0].ActorCount
        ReportReproduction='Passed';TenantIsolation='403/404';IdempotentReplay='Passed'
    }|ConvertTo-Json -Compress
}
finally {
    foreach($id in $createdResponsibilityAssignmentIds){
        try{$null=Invoke-DbNonQuery $connection 'DELETE FROM ProcurementResponsibilityAssignments WHERE Id=@id;' @{id=$id}}catch{}
    }
    if($createdAdminRoleMembership){
        try{$null=Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{userId=$adminId;roleId=$storesOfficerRoleId}}catch{}
    }
    foreach($actorId in $backups.Keys){
        $b=$backups[$actorId]
        try{$null=Invoke-DbNonQuery $connection 'UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout,TenantId=@tenantId,EmployeeId=@employeeId WHERE Id=@id;' @{id=$actorId;hash=$b.PasswordHash;failed=$b.AccessFailedCount;lockout=$b.LockoutEnd;tenantId=$b.TenantId;employeeId=$b.EmployeeId}}catch{}
    }
    if($connection.State -eq [System.Data.ConnectionState]::Open){$connection.Close()}
    $connection.Dispose()
}
