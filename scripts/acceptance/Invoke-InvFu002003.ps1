[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://127.0.0.1:5100',
    [string]$FrontendBaseUrl = 'http://127.0.0.1:3001',
    [switch]$SkipBrowser
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$tenantCode = 'DEFAULT'
$actorNames = @('employee', 'manager', 'admin', 'finance.clerk', 'ap.officer', 'estate.officer1')

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

function Invoke-DbNonQuery {
    param([System.Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    foreach ($entry in $Parameters.GetEnumerator()) { $null = Add-DbParameter $command "@$($entry.Key)" $entry.Value }
    return $command.ExecuteNonQuery()
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

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body,
        [int[]]$ExpectedStatus = @(200),
        [hashtable]$AdditionalHeaders = @{}
    )
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    foreach ($entry in $AdditionalHeaders.GetEnumerator()) { $headers[$entry.Key] = $entry.Value }
    $parameters = @{
        Uri = "$ApiBaseUrl$Path"
        Method = $Method
        Headers = $headers
        SkipHttpErrorCheck = $true
        TimeoutSec = 120
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
    try { return $response.Content | ConvertFrom-Json } catch { return $response.Content }
}

function Login-Actor {
    param([string]$UserName, [string]$Password)
    $response = Invoke-JsonApi -Method POST -Path '/api/auth/login' -Token '' -Body @{
        username = $UserName
        password = $Password
        tenantCode = $tenantCode
        rememberMe = $false
    }
    if ([string]::IsNullOrWhiteSpace($response.token)) { throw "Login did not issue a token for $UserName." }
    return [string]$response.token
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)
    if ($Actual -ne $Expected) { throw "$Message Expected '$Expected', received '$Actual'." }
}

function Ensure-Workflow {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$AdminToken,
        [string]$EntityType,
        [string]$Name
    )
    $published = Invoke-DbScalar $Connection @'
SELECT TOP (1) d.Id
FROM WorkflowDefinitions d
JOIN WorkflowEntityTypes e ON e.Id=d.EntityTypeId AND e.TenantId=d.TenantId
WHERE d.TenantId=@tenantId AND d.IsDeleted=0 AND d.IsActive=1 AND d.LifecycleStatus=1
  AND (e.Code=@entityType OR e.Name=@entityType)
ORDER BY d.Version DESC, d.PublishedAt DESC;
'@ @{ tenantId=$tenantId; entityType=$EntityType }
    if ($null -ne $published -and $published -isnot [DBNull]) { return [Guid]$published }

    $created = Invoke-JsonApi -Method POST -Path '/api/Workflow/definitions' -Token $AdminToken -Body @{
        name=$Name
        description="Fresh Inventory/Stores acceptance workflow for $EntityType."
        entityType=$EntityType
        isActive=$true
        configuration='{"source":"INV-FU-002-003","approvalState":"approved"}'
        steps=@(
            @{ name='Submitted'; stepType=0; order=1; isRequired=$true; requiredRole='TDC_STORES_OFFICER' },
            @{ name='Independent approval'; stepType=2; order=2; isRequired=$true; requiredRole='TDC_STORES_MANAGER';
                configuration=@{ approvalConfig=@{ approvalType=0; approverRules=@(@{ approvalGroup=1; assignmentType=1; role='TDC_STORES_MANAGER'; priority=100 }); minApprovalsRequired=1; preventInitiatorApproval=$true; requireDistinctApprovers=$true } } },
            @{ name='Completed'; stepType=1; order=3; isRequired=$true }
        )
        transitions=@()
    } -ExpectedStatus @(201)
    $id = [Guid]$created.data.id
    if ($id -eq [Guid]::Empty) { throw "The $EntityType acceptance workflow was not created." }
    return $id
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

$passwordBackups = @{}
$addedRoleLinks = [System.Collections.Generic.List[object]]::new()
$addedAssignmentIds = [System.Collections.Generic.List[Guid]]::new()
$frontendProcess = $null
$frontendOut = Join-Path ([IO.Path]::GetTempPath()) "inv-fu-002003-next-$([Guid]::NewGuid().ToString('N')).out.log"
$frontendErr = Join-Path ([IO.Path]::GetTempPath()) "inv-fu-002003-next-$([Guid]::NewGuid().ToString('N')).err.log"

try {
    $actors = @{}
    foreach ($name in $actorNames) {
        $row = Invoke-DbTable $connection @'
SELECT TOP (1) Id,UserName,PasswordHash,AccessFailedCount,LockoutEnd,TenantId,EmployeeId
FROM Users WHERE TenantId=@tenantId AND UserName=@name AND IsActive=1 AND IsDeleted=0;
'@ @{ tenantId=$tenantId; name=$name }
        if ($row.Rows.Count -ne 1) { throw "Acceptance actor $name is not available in the DEFAULT tenant." }
        $actorId = [Guid]$row.Rows[0].Id
        $actors[$name] = $actorId
        $passwordBackups[$actorId] = @{
            PasswordHash = if ($row.Rows[0].IsNull('PasswordHash')) { $null } else { [string]$row.Rows[0].PasswordHash }
            AccessFailedCount = [int]$row.Rows[0].AccessFailedCount
            LockoutEnd = if ($row.Rows[0].IsNull('LockoutEnd')) { $null } else { $row.Rows[0].LockoutEnd }
        }
        $null = Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;
'@ @{ id=$actorId; hash=$temporaryHash }
    }

    $roles = @{}
    foreach ($roleName in @('TDC_STORES_OFFICER','TDC_STORES_MANAGER')) {
        $roleId = Invoke-DbScalar $connection 'SELECT TOP (1) Id FROM Roles WHERE NormalizedName=@name AND IsDeleted=0;' @{ name=$roleName }
        if ($null -eq $roleId -or $roleId -is [DBNull]) { throw "Required acceptance role $roleName is not seeded." }
        $roles[$roleName] = [Guid]$roleId
    }

    $rolePlan = @{
        'employee' = @('TDC_STORES_OFFICER')
        'manager' = @('TDC_STORES_MANAGER')
        'admin' = @('TDC_STORES_OFFICER')
        'finance.clerk' = @('TDC_STORES_OFFICER')
        'ap.officer' = @('TDC_STORES_MANAGER')
    }
    foreach ($entry in $rolePlan.GetEnumerator()) {
        foreach ($roleName in $entry.Value) {
            $userId = $actors[$entry.Key]
            $roleId = $roles[$roleName]
            $hasRole = [int](Invoke-DbScalar $connection 'SELECT COUNT(1) FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{ userId=$userId; roleId=$roleId })
            if ($hasRole -eq 0) {
                $null = Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{ userId=$userId; roleId=$roleId }
                $addedRoleLinks.Add([pscustomobject]@{ UserId=$userId; RoleId=$roleId })
            }
            $hasAssignment = [int](Invoke-DbScalar $connection @'
SELECT COUNT(1) FROM ProcurementResponsibilityAssignments
WHERE TenantId=@tenantId AND UserId=@userId AND RoleId=@roleId AND IsDeleted=0 AND IsActive=1
  AND WarehouseScopeMode=1 AND LocationScopeMode=1
  AND EffectiveFrom<=SYSUTCDATETIME() AND (EffectiveTo IS NULL OR EffectiveTo>=SYSUTCDATETIME());
'@ @{ tenantId=$tenantId; userId=$userId; roleId=$roleId })
            if ($hasAssignment -eq 0) {
                $assignmentId = [Guid]::NewGuid()
                $null = Invoke-DbNonQuery $connection @'
INSERT INTO ProcurementResponsibilityAssignments
    (Id,UserId,RoleId,RoleName,WarehouseScopeMode,LocationScopeMode,EffectiveFrom,EffectiveTo,IsActive,Reason,
     CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
VALUES(@id,@userId,@roleId,@roleName,1,1,SYSUTCDATETIME(),NULL,1,
    'Temporary INV-FU-002/003 acceptance grant',SYSUTCDATETIME(),'INV-FU-002-003',NULL,0,@tenantId);
'@ @{ id=$assignmentId; userId=$userId; roleId=$roleId; roleName=$roleName; tenantId=$tenantId }
                $addedAssignmentIds.Add($assignmentId)
            }
        }
    }

    $tokens = @{}
    foreach ($name in $actorNames) { $tokens[$name] = Login-Actor $name $temporaryPassword }
    $null = Invoke-JsonApi -Method GET -Path '/api/inventory/requisitions' -Token '' -Body $null -ExpectedStatus @(401)

    $null = Ensure-Workflow $connection $tokens['admin'] 'InventoryRequisition' "INV-FU-003 Inventory Requisition $([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
    $null = Ensure-Workflow $connection $tokens['admin'] 'InventoryReturnVoucher' "INV-FU-003 Store Return $([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"

    $maintenancePrerequisite = Invoke-DbTable $connection @'
SELECT TOP (1)
    a.Id AssetId,wt.Id WorkOrderTypeId,mt.Id MaintenanceTypeId,p.Id PriorityLevelId,
    i.Id ItemId,i.ItemCode,i.AverageCost,w.Id WarehouseId,l.Id LocationId
FROM MaintenanceAssets a
CROSS JOIN WorkOrderTypes wt
CROSS JOIN MaintenanceTypes mt
CROSS JOIN PriorityLevels p
CROSS APPLY (
    SELECT TOP (1) i0.Id,i0.ItemCode,i0.AverageCost,q.WarehouseId,l0.Id LocationId
    FROM InventoryItems i0
    JOIN WarehouseQuantities q ON q.InventoryItemId=i0.Id AND q.TenantId=i0.TenantId AND q.IsDeleted=0 AND q.AvailableStock>=3
    JOIN Warehouses w0 ON w0.Id=q.WarehouseId AND w0.TenantId=q.TenantId AND w0.IsDeleted=0 AND w0.IsActive=1
    JOIN WarehouseLocations l0 ON l0.TenantId=q.TenantId AND l0.WarehouseId=q.WarehouseId AND l0.IsDeleted=0 AND l0.IsActive=1
    JOIN InventoryLocations il ON il.TenantId=q.TenantId AND il.InventoryItemId=i0.Id AND il.LocationId=l0.Id AND il.IsDeleted=0 AND il.AvailableQuantity>=3
    WHERE i0.TenantId=@tenantId AND i0.IsDeleted=0 AND i0.Status=1
    ORDER BY i0.ItemCode,l0.LocationCode
) i
JOIN Warehouses w ON w.Id=i.WarehouseId
JOIN WarehouseLocations l ON l.Id=i.LocationId
WHERE a.TenantId=@tenantId AND a.IsDeleted=0
  AND wt.TenantId=@tenantId AND wt.IsDeleted=0 AND wt.IsActive=1
  AND mt.TenantId=@tenantId AND mt.IsDeleted=0 AND mt.IsActive=1
  AND p.TenantId=@tenantId AND p.IsDeleted=0 AND p.IsActive=1;
'@ @{ tenantId=$tenantId }
    if ($maintenancePrerequisite.Rows.Count -ne 1) { throw 'Fresh INV-FU-002 prerequisites are incomplete.' }
    $m = $maintenancePrerequisite.Rows[0]
    $workOrder = Invoke-JsonApi -Method POST -Path '/api/maintenance/work-orders' -Token $tokens['employee'] -Body @{
        title="INV-FU-002 fresh reservation $([DateTime]::UtcNow.ToString('O'))"
        description='Fresh governed work-order reservation acceptance.'
        assetId=[Guid]$m.AssetId
        workOrderTypeId=[Guid]$m.WorkOrderTypeId
        maintenanceTypeId=[Guid]$m.MaintenanceTypeId
        priorityLevelId=[Guid]$m.PriorityLevelId
        status='Draft'
        requestedStartDate=[DateTime]::UtcNow.AddDays(1)
        requestedCompletionDate=[DateTime]::UtcNow.AddDays(2)
        generateDefaultTasks=$false
    } -ExpectedStatus @(201)
    $workOrderId = [Guid]$workOrder.id
    $reserveKey = "invfu002-reserve-$([Guid]::NewGuid().ToString('N'))"
    $partBody = @{
        workOrderId=$workOrderId
        inventoryItemId=[Guid]$m.ItemId
        quantityRequired=3
        unitCost=[decimal]$m.AverageCost
        warehouseId=[Guid]$m.WarehouseId
        warehouseLocationId=[Guid]$m.LocationId
        notes='Fresh INV-FU-002 reservation.'
    }
    $null = Invoke-JsonApi -Method POST -Path '/api/maintenance/work-orders/parts' -Token $tokens['estate.officer1'] -Body $partBody `
        -ExpectedStatus @(403) -AdditionalHeaders @{ 'Idempotency-Key'="invfu002-denied-$([Guid]::NewGuid().ToString('N'))" }
    $part = Invoke-JsonApi -Method POST -Path '/api/maintenance/work-orders/parts' -Token $tokens['employee'] -Body $partBody -AdditionalHeaders @{ 'Idempotency-Key'=$reserveKey }
    $replayedPart = Invoke-JsonApi -Method POST -Path '/api/maintenance/work-orders/parts' -Token $tokens['employee'] -Body $partBody -AdditionalHeaders @{ 'Idempotency-Key'=$reserveKey }
    Assert-Equal ([Guid]$replayedPart.id) ([Guid]$part.id) 'Work-order reservation replay created a second part.'
    $updateKey = "invfu002-use-$([Guid]::NewGuid().ToString('N'))"
    $updateBody = @{ quantityRequired=3; quantityUsed=1; quantityReturned=0; unitCost=[decimal]$m.AverageCost; warehouseLocationId=[Guid]$m.LocationId; status='PartiallyUsed'; notes='One unit consumed.' }
    $part = Invoke-JsonApi -Method PUT -Path "/api/maintenance/work-orders/parts/$($part.id)" -Token $tokens['admin'] -Body $updateBody -AdditionalHeaders @{ 'Idempotency-Key'=$updateKey }
    $null = Invoke-JsonApi -Method PUT -Path "/api/maintenance/work-orders/parts/$($part.id)" -Token $tokens['admin'] -Body $updateBody -AdditionalHeaders @{ 'Idempotency-Key'=$updateKey }
    $returnKey = "invfu002-return-$([Guid]::NewGuid().ToString('N'))"
    $part = Invoke-JsonApi -Method POST -Path "/api/maintenance/work-orders/parts/$($part.id)/return" -Token $tokens['finance.clerk'] -Body $null -AdditionalHeaders @{ 'Idempotency-Key'=$returnKey }
    $null = Invoke-JsonApi -Method POST -Path "/api/maintenance/work-orders/parts/$($part.id)/return" -Token $tokens['finance.clerk'] -Body $null -AdditionalHeaders @{ 'Idempotency-Key'=$returnKey }
    Assert-Equal $part.status 'Returned' 'The work-order reservation did not close as Returned.'
    Assert-Equal ([decimal]$part.quantityUsed) ([decimal]1) 'The work-order consumption quantity is incorrect.'

    $fixedAssetPrerequisite = Invoke-DbTable $connection @'
SELECT TOP (1) i.Id ItemId,i.ItemCode,i.AverageCost,w.Id WarehouseId,l.Id LocationId,d.Id DepartmentId,
    q.CurrentStock WarehouseStock,il.Quantity LocationStock,trace.SerialNumber
FROM InventoryItems i
JOIN InventoryIssueAccountingRules r ON r.TenantId=i.TenantId AND r.InventoryCategoryId=i.CategoryId
  AND r.ItemType=i.ItemType AND r.ItemType=4 AND r.Treatment=2 AND r.MovementReasonCode='ASSET_CUSTODY'
  AND r.IsDeleted=0 AND r.IsActive=1 AND r.EffectiveFromUtc<=SYSUTCDATETIME()
  AND (r.EffectiveToUtc IS NULL OR r.EffectiveToUtc>SYSUTCDATETIME()) AND r.FixedAssetCategoryId IS NOT NULL
JOIN WarehouseQuantities q ON q.TenantId=i.TenantId AND q.InventoryItemId=i.Id AND q.IsDeleted=0 AND q.AvailableStock>=1
JOIN Warehouses w ON w.TenantId=q.TenantId AND w.Id=q.WarehouseId AND w.IsDeleted=0 AND w.IsActive=1
JOIN WarehouseLocations l ON l.TenantId=q.TenantId AND l.WarehouseId=w.Id AND l.IsDeleted=0 AND l.IsActive=1
JOIN InventoryLocations il ON il.TenantId=i.TenantId AND il.InventoryItemId=i.Id AND il.LocationId=l.Id AND il.IsDeleted=0 AND il.AvailableQuantity>=1
CROSS APPLY (SELECT TOP (1) Id FROM Departments WHERE TenantId=i.TenantId AND IsDeleted=0 AND IsActive=1 ORDER BY Name) d
CROSS APPLY (
    SELECT TOP (1) t.SerialNumber
    FROM InventoryTraceabilityEvents t
    WHERE t.TenantId=i.TenantId AND t.InventoryItemId=i.Id AND t.WarehouseId=w.Id
      AND t.LocationId=l.Id AND t.IsDeleted=0 AND t.SerialNumber IS NOT NULL
    GROUP BY t.SerialNumber
    HAVING SUM(CASE WHEN t.Direction IN (1,3,5,6) THEN t.Quantity ELSE -t.Quantity END)>=1
    ORDER BY MIN(t.OccurredAtUtc),t.SerialNumber
) trace
WHERE i.TenantId=@tenantId AND i.IsDeleted=0 AND i.Status=1 AND i.ItemType=4
  AND i.IsSerialTracked=1 AND i.AverageCost>0
ORDER BY i.ItemCode,l.LocationCode;
'@ @{ tenantId=$tenantId }
    if ($fixedAssetPrerequisite.Rows.Count -ne 1) { throw 'Fresh INV-FU-003 fixed-asset, accounting, stock or department prerequisites are incomplete.' }
    $f = $fixedAssetPrerequisite.Rows[0]
    $serial = [string]$f.SerialNumber
    $requisition = Invoke-JsonApi -Method POST -Path '/api/inventory/requisitions' -Token $tokens['employee'] -Body @{
        departmentId=[Guid]$f.DepartmentId
        departmentName='INV-FU-003 acceptance'
        costCenter='INV-FU-003'
        warehouseId=[Guid]$f.WarehouseId
        locationId=[Guid]$f.LocationId
        requisitionType=1
        priority='Normal'
        requiredDate=[DateTime]::UtcNow.AddDays(1)
        purpose='Fresh fixed-asset issue and governed return acceptance.'
        items=@(@{ inventoryItemId=[Guid]$f.ItemId; requestedQuantity=1; locationId=[Guid]$f.LocationId; serialNumber=$serial; notes='Serial-tracked fixed asset.' })
    } -ExpectedStatus @(201)
    $requisitionId = [Guid]$requisition.id
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/submit" -Token $tokens['employee'] -Body $null
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/approve" -Token $tokens['employee'] -Body @{ notes='Self-approval probe' } -ExpectedStatus @(403)
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/approve" -Token $tokens['manager'] -Body @{ notes='Independent Stores Manager approval.' }
    $approved = Invoke-JsonApi -Method GET -Path "/api/inventory/requisitions/$requisitionId" -Token $tokens['admin'] -Body $null
    $issueKey = "invfu003-issue-$([Guid]::NewGuid().ToString('N'))"
    $issueBody = @{
        idempotencyKey=$issueKey
        rowVersion=$approved.rowVersion
        receiverUserId=$actors['finance.clerk']
        movementReasonCode='ASSET_CUSTODY'
        notes='Independent serial-tracked fixed-asset handover.'
        items=@(@{ itemId=[Guid]$approved.items[0].id; issuedQuantity=1; locationId=[Guid]$f.LocationId; serialNumber=$serial })
    }
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/issue" -Token $tokens['estate.officer1'] -Body $issueBody `
        -ExpectedStatus @(403)
    $issued = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/issue" -Token $tokens['admin'] -Body $issueBody
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/issue" -Token $tokens['admin'] -Body $issueBody
    $issueVoucher = $issued.voucher
    $ackKey = "invfu003-ack-$([Guid]::NewGuid().ToString('N'))"
    $issueVoucher = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/issue-vouchers/$($issueVoucher.id)/acknowledge" -Token $tokens['finance.clerk'] -Body @{
        rowVersion=$issueVoucher.rowVersion
        comment='Fixed asset received into controlled custody.'
        idempotencyKey=$ackKey
    }
    $requisition = Invoke-JsonApi -Method GET -Path "/api/inventory/requisitions/$requisitionId" -Token $tokens['finance.clerk'] -Body $null
    $returnKey = "invfu003-return-$([Guid]::NewGuid().ToString('N'))"
    $returnBody = @{
        items=@(@{ itemId=[Guid]$requisition.items[0].id; returnedQuantity=1; locationId=[Guid]$f.LocationId; serialNumber=$serial })
        reasonCode='UNUSED'
        reason='The serial-tracked asset is being returned through the controlled custody route.'
        notes='Fresh INV-FU-003 controlled return.'
        idempotencyKey=$returnKey
        correlationId="INV-FU-003-$requisitionId"
        rowVersion=$requisition.rowVersion
        evidence=@()
    }
    $returnVoucher = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/return" -Token $tokens['finance.clerk'] -Body $returnBody
    $returnReplay = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/$requisitionId/return" -Token $tokens['finance.clerk'] -Body $returnBody
    Assert-Equal ([Guid]$returnReplay.id) ([Guid]$returnVoucher.id) 'Return request replay created a second voucher.'
    $decisionKey = "invfu003-approve-$([Guid]::NewGuid().ToString('N'))"
    $returnVoucher = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/return-vouchers/$($returnVoucher.id)/decision" -Token $tokens['manager'] -Body @{
        approved=$true; comment='Independent approval of the serial-tracked return.'; rowVersion=$returnVoucher.rowVersion; idempotencyKey=$decisionKey
    }
    $postKey = "invfu003-post-$([Guid]::NewGuid().ToString('N'))"
    $postBody = @{ rowVersion=$returnVoucher.rowVersion; idempotencyKey=$postKey }
    $returnVoucher = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/return-vouchers/$($returnVoucher.id)/post" -Token $tokens['admin'] -Body $postBody
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/return-vouchers/$($returnVoucher.id)/post" -Token $tokens['admin'] -Body $postBody
    $reverseKey = "invfu003-reverse-$([Guid]::NewGuid().ToString('N'))"
    $reverseBody = @{ rowVersion=$returnVoucher.rowVersion; reason='Acceptance reversal proves compensating stock, Finance and fixed-asset lineage.'; idempotencyKey=$reverseKey }
    $returnVoucher = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/return-vouchers/$($returnVoucher.id)/reverse" -Token $tokens['ap.officer'] -Body $reverseBody
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/requisitions/return-vouchers/$($returnVoucher.id)/reverse" -Token $tokens['ap.officer'] -Body $reverseBody
    Assert-Equal $returnVoucher.status 'Reversed' 'The Store Return Voucher did not close as Reversed.'

    $returnEvidence = Invoke-DbTable $connection @'
SELECT v.Status,v.TotalValue,COUNT(DISTINCT a.Id) ActionCount,COUNT(DISTINCT a.ActorUserId) ActorCount,
       COUNT(DISTINCT ra.Id) ReturnAllocationCount,COUNT(DISTINCT ra.ReversalPostingEventId) ReversalEventCount,
       COUNT(DISTINCT l.FixedAssetId) FixedAssetCount
FROM InventoryReturnVouchers v
LEFT JOIN InventoryReturnVoucherActions a ON a.InventoryReturnVoucherId=v.Id AND a.TenantId=v.TenantId AND a.IsDeleted=0
LEFT JOIN InventoryReturnVoucherLines vl ON vl.InventoryReturnVoucherId=v.Id AND vl.TenantId=v.TenantId AND vl.IsDeleted=0
LEFT JOIN InventoryIssueReturnAllocations ra ON ra.InventoryReturnVoucherLineId=vl.Id AND ra.TenantId=v.TenantId AND ra.IsDeleted=0
LEFT JOIN InventoryIssueFinanceLineages l ON l.Id=ra.InventoryIssueFinanceLineageId AND l.TenantId=v.TenantId AND l.IsDeleted=0
WHERE v.Id=@id AND v.TenantId=@tenantId AND v.IsDeleted=0
GROUP BY v.Status,v.TotalValue;
'@ @{ id=[Guid]$returnVoucher.id; tenantId=$tenantId }
    if ($returnEvidence.Rows.Count -ne 1) { throw 'The returned fixed-asset voucher was not persisted.' }
    $e = $returnEvidence.Rows[0]
    Assert-Equal ([int]$e.Status) 5 'The durable return-voucher state is not Reversed.'
    Assert-Equal ([int]$e.ActionCount) 4 'Return idempotency produced an unexpected action count.'
    if ([int]$e.ActorCount -lt 4) { throw 'The return lifecycle did not retain four distinct actors.' }
    Assert-Equal ([int]$e.ReturnAllocationCount) 1 'The return does not have one exact issue-line allocation.'
    Assert-Equal ([int]$e.ReversalEventCount) 1 'The return reversal does not have one Finance posting event.'
    Assert-Equal ([int]$e.FixedAssetCount) 1 'The issue/return does not retain fixed-asset lineage.'

    $reservationActions = [int](Invoke-DbScalar $connection @'
SELECT COUNT(*) FROM InventoryWorkOrderReservationActions
WHERE TenantId=@tenantId AND WorkOrderPartId=@partId AND IsDeleted=0;
'@ @{ tenantId=$tenantId; partId=[Guid]$part.id })
    Assert-Equal $reservationActions 3 'Reservation replay produced an unexpected immutable action count.'

    if (-not $SkipBrowser) {
        $frontendUri = [Uri]$FrontendBaseUrl
        $frontendPort = $frontendUri.Port
        $portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $frontendPort)
        try {
            $portProbe.Start()
        }
        catch {
            throw "Port $frontendPort is already in use; browser acceptance will not take ownership of an unrelated process."
        }
        finally {
            $portProbe.Stop()
        }
        $priorApiUrl = $env:NEXT_PUBLIC_API_URL
        $env:NEXT_PUBLIC_API_URL = "$ApiBaseUrl/api"
        $frontendProcess = Start-Process -FilePath 'npm.cmd' -ArgumentList @('run','dev','--','--port',"$frontendPort") `
            -WorkingDirectory (Join-Path $repoRoot 'frontend') -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput $frontendOut -RedirectStandardError $frontendErr
        $ready = $false
        for ($attempt=0; $attempt -lt 90; $attempt++) {
            Start-Sleep -Seconds 1
            try {
                $probe = Invoke-WebRequest -Uri "$FrontendBaseUrl/login" -SkipHttpErrorCheck -TimeoutSec 5
                if ($probe.StatusCode -eq 200) { $ready = $true; break }
            } catch { }
            if ($frontendProcess.HasExited) { break }
        }
        if (-not $ready) {
            $tail = if (Test-Path $frontendErr) { (Get-Content $frontendErr -Tail 30) -join "`n" } else { 'No frontend error log.' }
            throw "The acceptance frontend did not become ready. $tail"
        }
        $env:E2E_API_URL = $ApiBaseUrl
        $env:E2E_BASE_URL = $FrontendBaseUrl
        $env:INV_FU002_USERNAME = 'admin'
        $env:INV_FU002_PASSWORD = $temporaryPassword
        $env:INV_FU002_TENANT_CODE = $tenantCode
        $env:INV_FU002_WORK_ORDER_ID = $workOrderId.ToString()
        $env:INV_FU002_ITEM_CODE = [string]$m.ItemCode
        $env:INV_FU002_EXPECTED_USAGE = '1 / 3'
        $env:INV_FU003_USERNAME = 'admin'
        $env:INV_FU003_PASSWORD = $temporaryPassword
        $env:INV_FU003_TENANT_CODE = $tenantCode
        $env:INV_FU003_REQUISITION_NUMBER = [string]$requisition.requisitionNumber
        $env:INV_FU003_RETURN_VOUCHER_NUMBER = [string]$returnVoucher.voucherNumber
        $env:INV_FU003_EXPECTED_VALUE = ([decimal]$returnVoucher.totalValue).ToString('0.00',[Globalization.CultureInfo]::InvariantCulture)
        Push-Location (Join-Path $repoRoot 'e2e-tests')
        try {
            & npx.cmd playwright test tests/inv-fu-002-maintenance-reservation.spec.ts tests/inv-fu-003-issue-return-asset.spec.ts --project=chromium --workers=1
            if ($LASTEXITCODE -ne 0) { throw "Playwright INV-FU-002/003 acceptance failed with exit code $LASTEXITCODE." }
        } finally { Pop-Location }
        if ($null -ne $priorApiUrl) { $env:NEXT_PUBLIC_API_URL = $priorApiUrl } else { Remove-Item Env:NEXT_PUBLIC_API_URL -ErrorAction SilentlyContinue }
    }

    [pscustomobject]@{
        WorkOrderId=$workOrderId
        WorkOrderPartId=[Guid]$part.id
        ReservationStatus=$part.status
        ReservationActions=$reservationActions
        RequisitionNumber=[string]$requisition.requisitionNumber
        IssueVoucherNumber=[string]$issueVoucher.voucherNumber
        ReturnVoucherNumber=[string]$returnVoucher.voucherNumber
        ReturnStatus=$returnVoucher.status
        ReturnActions=[int]$e.ActionCount
        DistinctReturnActors=[int]$e.ActorCount
        FixedAssetLineage=[int]$e.FixedAssetCount
        Browser=if ($SkipBrowser) { 'Not run' } else { 'Passed' }
    } | ConvertTo-Json -Compress
}
finally {
    if ($frontendProcess -and -not $frontendProcess.HasExited) { & taskkill.exe /PID $frontendProcess.Id /T /F *> $null }
    foreach ($name in @(
        'E2E_API_URL','E2E_BASE_URL','INV_FU002_USERNAME','INV_FU002_PASSWORD','INV_FU002_TENANT_CODE',
        'INV_FU002_WORK_ORDER_ID','INV_FU002_ITEM_CODE','INV_FU002_EXPECTED_USAGE','INV_FU003_USERNAME',
        'INV_FU003_PASSWORD','INV_FU003_TENANT_CODE','INV_FU003_REQUISITION_NUMBER',
        'INV_FU003_RETURN_VOUCHER_NUMBER','INV_FU003_EXPECTED_VALUE')) {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
    foreach ($assignmentId in $addedAssignmentIds) {
        try { $null = Invoke-DbNonQuery $connection 'DELETE FROM ProcurementResponsibilityAssignments WHERE Id=@id;' @{ id=$assignmentId } } catch { }
    }
    foreach ($link in $addedRoleLinks) {
        try { $null = Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{ userId=$link.UserId; roleId=$link.RoleId } } catch { }
    }
    foreach ($entry in $passwordBackups.GetEnumerator()) {
        try {
            $null = Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout WHERE Id=@id;
'@ @{ id=$entry.Key; hash=$entry.Value.PasswordHash; failed=$entry.Value.AccessFailedCount; lockout=$entry.Value.LockoutEnd }
        } catch { }
    }
    if ($connection.State -eq [System.Data.ConnectionState]::Open) { $connection.Close() }
    $connection.Dispose()
    Remove-Item $frontendOut,$frontendErr -Force -ErrorAction SilentlyContinue
}
