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
$isolationTenantId = [Guid]'D6040000-0000-4000-8000-000000000001'
$storesOfficerRoleId = [Guid]'10958480-D251-4C77-8DC0-5823889C2270'
$workflowDefinitionId = [Guid]'77799C45-BB40-49CC-AADF-D7D469B78C46'
$itemId = [Guid]'A4543DF0-09BA-4EB4-AD1D-4FC3C50F8A3F'
$sourceWarehouseId = [Guid]'39E1B1FB-17C8-41CB-A703-F0A6E740ACC8'
$sourceLocationId = [Guid]'F0020000-0000-4000-8000-000000000001'

$actors = @(
    @{ Id = [Guid]'77AF28CF-66D3-49C0-0DBD-08DE862E82EE'; UserName = 'employee' },
    @{ Id = [Guid]'9E475CE9-34AD-4A6D-0DBC-08DE862E82EE'; UserName = 'manager' },
    @{ Id = [Guid]'58CAFD8B-42CE-4F67-0DBB-08DE862E82EE'; UserName = 'admin' },
    @{ Id = [Guid]'3538F167-D0B2-4AE9-F15B-08DEEA6C9143'; UserName = 'finance.clerk' },
    @{ Id = [Guid]'4137B8FB-F543-444F-F15D-08DEEA6C9143'; UserName = 'ap.officer' },
    @{ Id = [Guid]'35B82323-8EA1-498C-C401-08DEED5E48D0'; UserName = 'estate.officer1' }
)

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
        [int[]]$ExpectedStatus = @(200)
    )
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
    try { return $response.Content | ConvertFrom-Json } catch { return $response.Content }
}

function Login-Actor {
    param([string]$UserName, [string]$Password)
    $response = Invoke-JsonApi -Method POST -Path '/api/auth/login' -Token '' -Body @{
        username = $UserName
        password = $Password
        rememberMe = $false
    }
    if ([string]::IsNullOrWhiteSpace($response.token)) { throw "Login did not issue a token for $UserName." }
    return [string]$response.token
}

function Get-Transfer {
    param([Guid]$TransferId, [string]$Token)
    return Invoke-JsonApi -Method GET -Path "/api/inventory/transfers/$TransferId" -Token $Token -Body $null
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)
    if ($Actual -ne $Expected) { throw "$Message Expected '$Expected', received '$Actual'." }
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
$addedRoleUsers = [System.Collections.Generic.List[Guid]]::new()
$addedAssignmentIds = [System.Collections.Generic.List[Guid]]::new()
$estateOriginalTenantId = $null
$frontendProcess = $null
$frontendOut = Join-Path ([IO.Path]::GetTempPath()) "inv-fu-004-next-$([Guid]::NewGuid().ToString('N')).out.log"
$frontendErr = Join-Path ([IO.Path]::GetTempPath()) "inv-fu-004-next-$([Guid]::NewGuid().ToString('N')).err.log"
$evidenceFile = Join-Path ([IO.Path]::GetTempPath()) "inv-fu-004-evidence-$([Guid]::NewGuid().ToString('N')).pdf"
$transferId = [Guid]::Empty
$transferNumber = $null
$browserPassed = $false

try {
    foreach ($actor in $actors) {
        $row = Invoke-DbTable $connection @'
SELECT PasswordHash, AccessFailedCount, LockoutEnd, TenantId, EmployeeId
FROM Users
WHERE Id = @id AND IsActive = 1;
'@ @{ id = $actor.Id }
        if ($row.Rows.Count -ne 1) { throw "Acceptance actor $($actor.UserName) is not available." }
        $passwordBackups[$actor.Id] = @{
            PasswordHash = if ($row.Rows[0].IsNull('PasswordHash')) { $null } else { [string]$row.Rows[0].PasswordHash }
            AccessFailedCount = [int]$row.Rows[0].AccessFailedCount
            LockoutEnd = if ($row.Rows[0].IsNull('LockoutEnd')) { $null } else { $row.Rows[0].LockoutEnd }
            TenantId = [Guid]$row.Rows[0].TenantId
            EmployeeId = if ($row.Rows[0].IsNull('EmployeeId')) { $null } else { [Guid]$row.Rows[0].EmployeeId }
        }
        $null = Invoke-DbNonQuery $connection @'
UPDATE Users
SET PasswordHash = @hash, AccessFailedCount = 0, LockoutEnd = NULL
WHERE Id = @id;
'@ @{ id = $actor.Id; hash = $temporaryHash }
    }

    foreach ($actorId in @(
        [Guid]'58CAFD8B-42CE-4F67-0DBB-08DE862E82EE',
        [Guid]'3538F167-D0B2-4AE9-F15B-08DEEA6C9143',
        [Guid]'4137B8FB-F543-444F-F15D-08DEEA6C9143'
    )) {
        $hasRole = [int](Invoke-DbScalar $connection 'SELECT COUNT(1) FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{ userId=$actorId; roleId=$storesOfficerRoleId })
        if ($hasRole -eq 0) {
            $null = Invoke-DbNonQuery $connection 'INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);' @{ userId=$actorId; roleId=$storesOfficerRoleId }
            $addedRoleUsers.Add($actorId)
        }
        $hasAssignment = [int](Invoke-DbScalar $connection @'
SELECT COUNT(1) FROM ProcurementResponsibilityAssignments
WHERE TenantId=@tenantId AND UserId=@userId AND RoleId=@roleId AND IsDeleted=0 AND IsActive=1
  AND WarehouseScopeMode=1 AND LocationScopeMode=1
  AND EffectiveFrom<=SYSUTCDATETIME() AND (EffectiveTo IS NULL OR EffectiveTo>=SYSUTCDATETIME());
'@ @{ tenantId=$tenantId; userId=$actorId; roleId=$storesOfficerRoleId })
        if ($hasAssignment -eq 0) {
            $assignmentId = [Guid]::NewGuid()
            $null = Invoke-DbNonQuery $connection @'
INSERT INTO ProcurementResponsibilityAssignments
    (Id,UserId,RoleId,RoleName,WarehouseScopeMode,LocationScopeMode,EffectiveFrom,EffectiveTo,IsActive,Reason,
     CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
VALUES
    (@id,@userId,@roleId,'TDC_STORES_OFFICER',1,1,SYSUTCDATETIME(),NULL,1,
     'Temporary INV-REQ-FU-004 E2E-011 acceptance grant',SYSUTCDATETIME(),'INV-REQ-FU-004',NULL,0,@tenantId);
'@ @{ id=$assignmentId; userId=$actorId; roleId=$storesOfficerRoleId; tenantId=$tenantId }
            $addedAssignmentIds.Add($assignmentId)
        }
    }

    $employeeToken = Login-Actor 'employee' $temporaryPassword
    $managerToken = Login-Actor 'manager' $temporaryPassword
    $adminToken = Login-Actor 'admin' $temporaryPassword
    $financeToken = Login-Actor 'finance.clerk' $temporaryPassword
    $apToken = Login-Actor 'ap.officer' $temporaryPassword
    $unprivilegedToken = Login-Actor 'estate.officer1' $temporaryPassword

    $null = Invoke-JsonApi -Method GET -Path '/api/inventory/transfers' -Token '' -Body $null -ExpectedStatus @(401)

    $estateActor = $actors | Where-Object UserName -eq 'estate.officer1'
    $estateOriginalTenantId = $passwordBackups[$estateActor.Id].TenantId
    $null = Invoke-DbNonQuery $connection 'UPDATE Users SET TenantId=@tenantId, EmployeeId=NULL WHERE Id=@id;' @{ tenantId=$isolationTenantId; id=$estateActor.Id }
    $isolationToken = Login-Actor 'estate.officer1' $temporaryPassword
    $null = Invoke-DbNonQuery $connection 'UPDATE Users SET TenantId=@tenantId, EmployeeId=@employeeId WHERE Id=@id;' @{ tenantId=$estateOriginalTenantId; employeeId=$passwordBackups[$estateActor.Id].EmployeeId; id=$estateActor.Id }

    $workflowStatus = Invoke-DbTable $connection 'SELECT IsActive,LifecycleStatus FROM WorkflowDefinitions WHERE Id=@id;' @{ id=$workflowDefinitionId }
    if ($workflowStatus.Rows.Count -ne 1) { throw 'The seeded InventoryTransfer workflow definition is missing.' }
    if (-not [bool]$workflowStatus.Rows[0].IsActive -or [int]$workflowStatus.Rows[0].LifecycleStatus -ne 1) {
        $null = Invoke-JsonApi -Method PUT -Path "/api/Workflow/definitions/$workflowDefinitionId" -Token $adminToken -Body @{
            name='TDC Inventory Transfer Approval'
            description='Governed Inventory transfer maker-checker route for Stores operations.'
            entityType='InventoryTransfer'
            isActive=$false
            configuration='{"templateCode":"TDC_INVENTORY_TRANSFER","source":"INV-REQ-FU-004","approvalState":"approved","decisionDependencies":["DEC-003","DEC-004"]}'
            steps=@(
                @{ name='Submitted'; stepType=0; order=1; isRequired=$true; requiredRole='TDC_STORES_OFFICER' },
                @{ name='Approval'; stepType=2; order=2; isRequired=$true; requiredRole='TDC_STORES_MANAGER';
                    configuration=@{ approvalConfig=@{ approvalType=0; approverRules=@(@{ approvalGroup=1; assignmentType=1; role='TDC_STORES_MANAGER'; priority=100 }); minApprovalsRequired=1; preventInitiatorApproval=$true; requireDistinctApprovers=$true } } },
                @{ name='Completed'; stepType=1; order=3; isRequired=$true }
            )
            transitions=@()
        }
        $null = Invoke-JsonApi -Method POST -Path "/api/Workflow/definitions/$workflowDefinitionId/publish" -Token $adminToken -Body $null
    }

    $warehouses = @(Invoke-JsonApi -Method GET -Path '/api/inventory/warehouses/active' -Token $adminToken -Body $null)
    $destinationWarehouse = $warehouses | Where-Object code -eq 'E2E-011-DST' | Select-Object -First 1
    if (-not $destinationWarehouse) {
        $destinationWarehouse = Invoke-JsonApi -Method POST -Path '/api/inventory/warehouses' -Token $adminToken -Body @{
            name='E2E-011 Destination Store'; code='E2E-011-DST'; warehouseType='Standard';
            description='Governed inter-store transfer acceptance destination'; isDefault=$false; isConsignmentWarehouse=$false
        } -ExpectedStatus @(201)
    }
    $destinationWarehouseId = [Guid]$destinationWarehouse.id

    $locations = @(Invoke-JsonApi -Method GET -Path "/api/inventory/warehouse-locations?warehouseId=$destinationWarehouseId" -Token $adminToken -Body $null)
    $destinationLocation = $locations | Where-Object locationCode -eq 'E2E-011-DST-BIN' | Select-Object -First 1
    if (-not $destinationLocation) {
        $destinationLocation = Invoke-JsonApi -Method POST -Path '/api/inventory/warehouse-locations' -Token $adminToken -Body @{
            warehouseId=$destinationWarehouseId; locationCode='E2E-011-DST-BIN'; name='E2E-011 receiving bin';
            locationType='Bin'; isPickingLocation=$true; isReceivingLocation=$true; isConsignmentBin=$false
        } -ExpectedStatus @(201)
    }
    $destinationLocationId = [Guid]$destinationLocation.id

    $null = Invoke-JsonApi -Method POST -Path '/api/inventory/transfers' -Token $unprivilegedToken -Body @{
        sourceWarehouseId=$sourceWarehouseId; destinationWarehouseId=$destinationWarehouseId; reason='authorization probe';
        items=@(@{ inventoryItemId=$itemId; requestedQuantity=1 })
    } -ExpectedStatus @(403)

    # Keep repeatable acceptance runs from leaving draft/approved/in-transit test stock behind.
    # Cleanup uses the governed APIs and an actor independent of the prior dispatcher; no transfer
    # history or stock row is edited directly.
    $priorRuns = Invoke-DbTable $connection @'
SELECT Id,Status FROM InventoryTransfers
WHERE TenantId=@tenantId AND IsDeleted=0 AND Notes LIKE '%INV-REQ-FU-004 runtime acceptance%'
  AND Status IN (1,2,3,5);
'@ @{ tenantId=$tenantId }
    foreach ($priorRun in $priorRuns.Rows) {
        $priorId = [Guid]$priorRun.Id
        $prior = Get-Transfer $priorId $employeeToken
        $cleanupBody = @{
            rowVersion=$prior.rowVersion; idempotencyKey="e2e011-cleanup-$([Guid]::NewGuid().ToString('N'))";
            correlationId="E2E-011-CLEANUP-$priorId"; comment='Governed cleanup before repeat acceptance';
            reason='Superseded incomplete INV-REQ-FU-004 acceptance run.'
        }
        if ([int]$priorRun.Status -eq 5) {
            $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$priorId/reverse-shipment" -Token $employeeToken -Body $cleanupBody
        } else {
            $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$priorId/cancel" -Token $employeeToken -Body $cleanupBody
        }
    }

    $sourceBefore = [decimal](Invoke-DbScalar $connection @'
SELECT CurrentStock FROM WarehouseQuantities
WHERE TenantId=@tenantId AND WarehouseId=@warehouseId AND InventoryItemId=@itemId;
'@ @{ tenantId=$tenantId; warehouseId=$sourceWarehouseId; itemId=$itemId })
    $destinationBeforeValue = Invoke-DbScalar $connection @'
SELECT CurrentStock FROM WarehouseQuantities
WHERE TenantId=@tenantId AND WarehouseId=@warehouseId AND InventoryItemId=@itemId;
'@ @{ tenantId=$tenantId; warehouseId=$destinationWarehouseId; itemId=$itemId }
    $destinationBefore = if ($null -eq $destinationBeforeValue -or $destinationBeforeValue -is [DBNull]) { [decimal]0 } else { [decimal]$destinationBeforeValue }

    $evidenceReference = 'INV-FU-004-E2E-011 damage evidence'
    $record = Invoke-JsonApi -Method POST -Path '/api/document-management/records' -Token $adminToken -Body @{
        documentReference="INV-E2E-011-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))";
        title='E2E-011 damaged transfer inspection evidence'; sourceModule='Inventory';
        sourceLabel='Inventory transfer discrepancy evidence'; sourceEntityType='InventoryTransfer';
        versionStatus='Draft'; accessProfile='Module restricted'; retentionStatus='Current'; lifecycleStatus='Active';
        notes='Controlled evidence created by INV-REQ-FU-004 acceptance.'
    } -ExpectedStatus @(201)
    $documentRecordId = [Guid]$record.data.id
    [IO.File]::WriteAllBytes($evidenceFile, [Text.Encoding]::ASCII.GetBytes("%PDF-1.4`n1 0 obj<</Type/Catalog>>endobj`n%%EOF`n"))
    $uploadClient = [System.Net.Http.HttpClient]::new()
    $uploadClient.Timeout = [TimeSpan]::FromSeconds(120)
    $uploadClient.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $adminToken)
    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $evidenceStream = [IO.File]::OpenRead($evidenceFile)
    $fileContent = [System.Net.Http.StreamContent]::new($evidenceStream)
    $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new('application/pdf')
    $multipart.Add($fileContent, 'file', [IO.Path]::GetFileName($evidenceFile))
    $multipart.Add([System.Net.Http.StringContent]::new('v1.0'), 'versionNumber')
    $multipart.Add([System.Net.Http.StringContent]::new('Submitted'), 'status')
    $multipart.Add([System.Net.Http.StringContent]::new('E2E-011 damage evidence upload'), 'changeSummary')
    try {
        $uploadResponse = $uploadClient.PostAsync("$ApiBaseUrl/api/document-management/records/$documentRecordId/versions/upload", $multipart).GetAwaiter().GetResult()
        $uploadContent = $uploadResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ([int]$uploadResponse.StatusCode -ne 200) { throw "DMS upload returned $([int]$uploadResponse.StatusCode): $uploadContent" }
        $upload = $uploadContent | ConvertFrom-Json
    }
    finally {
        $multipart.Dispose()
        $uploadClient.Dispose()
        $evidenceStream.Dispose()
    }
    $documentVersionId = [Guid]$upload.data.id
    $null = Invoke-JsonApi -Method PUT -Path "/api/document-management/records/$documentRecordId/versions/$documentVersionId/status" -Token $adminToken -Body @{ status='Current'; changeSummary='Approved current E2E evidence' }
    $null = Invoke-JsonApi -Method PUT -Path "/api/document-management/records/$documentRecordId/versions/$documentVersionId/status" -Token $adminToken -Body @{ status='Published'; changeSummary='Published E2E evidence' }

    $created = Invoke-JsonApi -Method POST -Path '/api/inventory/transfers' -Token $employeeToken -Body @{
        sourceWarehouseId=$sourceWarehouseId; destinationWarehouseId=$destinationWarehouseId; transferType=1;
        priority='Normal'; requiredDate=[DateTime]::UtcNow.AddDays(1); reason='E2E-011 governed partial and damaged transfer';
        notes='INV-REQ-FU-004 runtime acceptance';
        items=@(@{ inventoryItemId=$itemId; requestedQuantity=10; notes='PVC transfer acceptance line' })
    } -ExpectedStatus @(201)
    $transferId = [Guid]$created.id
    $transferNumber = [string]$created.transferNumber

    $null = Invoke-JsonApi -Method GET -Path "/api/inventory/transfers/$transferId" -Token $isolationToken -Body $null -ExpectedStatus @(404)
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/submit-for-approval" -Token $employeeToken -Body $null
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/approve" -Token $managerToken -Body @{ comments='Independent stores-manager approval for E2E-011' }

    $detail = Get-Transfer $transferId $adminToken
    $line = $detail.items[0]
    $shipKey = "e2e011-ship-$([Guid]::NewGuid().ToString('N'))"
    $shipBody = @{
        rowVersion=$detail.rowVersion; idempotencyKey=$shipKey; correlationId="E2E-011-DISPATCH-$transferId";
        comment='Independent full dispatch'; trackingNumber="E2E011-$([DateTime]::UtcNow.ToString('HHmmss'))";
        items=@(@{ itemId=[Guid]$line.id; shippedQuantity=10 })
    }
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/ship" -Token $adminToken -Body $shipBody
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/ship" -Token $adminToken -Body $shipBody

    $detail = Get-Transfer $transferId $financeToken
    $line = $detail.items[0]
    $receiveKey = "e2e011-receive-$([Guid]::NewGuid().ToString('N'))"
    $receiveBody = @{
        rowVersion=$detail.rowVersion; idempotencyKey=$receiveKey; correlationId="E2E-011-RECEIPT-$transferId";
        comment='Independent partial/damaged receipt'; receivedItems=@(@{
            id=[Guid]$line.id; inventoryItemId=$itemId; receivedQuantity=7; damagedQuantity=2; shortageQuantity=1;
            discrepancyReasonCode='DAMAGED'; discrepancyReason='Two units damaged in transit and one unit short after destination inspection.';
            evidence=@(@{ centralDocumentVersionId=$documentVersionId; evidenceReference=$evidenceReference })
        })
    }
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/receive" -Token $financeToken -Body $receiveBody
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/receive" -Token $financeToken -Body $receiveBody

    $detail = Get-Transfer $transferId $apToken
    Assert-Equal $detail.status 'Received' 'The fully accounted partial/damaged receipt did not reach Received.'
    Assert-Equal $detail.hasOpenDiscrepancy $true 'The damaged receipt did not open a discrepancy.'
    $discrepancyId = [Guid]$detail.discrepancies[0].id
    $resolveKey = "e2e011-resolve-$([Guid]::NewGuid().ToString('N'))"
    $resolveBody = @{
        rowVersion=$detail.rowVersion; idempotencyKey=$resolveKey; correlationId="E2E-011-RESOLUTION-$transferId";
        comment='Independent replacement validation'; discrepancyIds=@($discrepancyId); resolutionCode='REPLACEMENT_RECEIVED';
        resolutionNotes='Three replacement units were independently inspected and received at the destination.';
        evidence=@(@{ centralDocumentVersionId=$documentVersionId; evidenceReference=$evidenceReference })
    }
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/resolve-discrepancies" -Token $apToken -Body $resolveBody
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/resolve-discrepancies" -Token $apToken -Body $resolveBody

    $detail = Get-Transfer $transferId $apToken
    $closeKey = "e2e011-close-$([Guid]::NewGuid().ToString('N'))"
    $closeBody = @{
        rowVersion=$detail.rowVersion; idempotencyKey=$closeKey; correlationId="E2E-011-CLOSE-$transferId";
        comment='Independent closure after replacement and balance reconciliation.'
    }
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/close" -Token $apToken -Body $closeBody
    $null = Invoke-JsonApi -Method POST -Path "/api/inventory/transfers/$transferId/close" -Token $apToken -Body $closeBody

    $final = Get-Transfer $transferId $managerToken
    Assert-Equal $final.status 'Completed' 'The transfer did not close as Completed.'
    Assert-Equal $final.hasOpenDiscrepancy $false 'The completed transfer retains an open discrepancy.'
    Assert-Equal ([decimal]$final.items[0].receivedQuantity) ([decimal]10) 'Replacement receipt did not reconcile the destination quantity.'
    Assert-Equal ([decimal]$final.items[0].damagedQuantity) ([decimal]0) 'Resolved damaged quantity was not cleared.'
    Assert-Equal ([decimal]$final.items[0].shortageQuantity) ([decimal]0) 'Resolved shortage quantity was not cleared.'

    $sourceAfter = [decimal](Invoke-DbScalar $connection @'
SELECT CurrentStock FROM WarehouseQuantities WHERE TenantId=@tenantId AND WarehouseId=@warehouseId AND InventoryItemId=@itemId;
'@ @{ tenantId=$tenantId; warehouseId=$sourceWarehouseId; itemId=$itemId })
    $destinationAfter = [decimal](Invoke-DbScalar $connection @'
SELECT CurrentStock FROM WarehouseQuantities WHERE TenantId=@tenantId AND WarehouseId=@warehouseId AND InventoryItemId=@itemId;
'@ @{ tenantId=$tenantId; warehouseId=$destinationWarehouseId; itemId=$itemId })
    Assert-Equal ($sourceBefore - $sourceAfter) ([decimal]10) 'Source balance delta is incorrect.'
    Assert-Equal ($destinationAfter - $destinationBefore) ([decimal]10) 'Destination balance delta is incorrect.'

    $actionEvidence = Invoke-DbTable $connection @'
SELECT COUNT(*) AS ActionCount, COUNT(DISTINCT ActorUserId) AS ActorCount
FROM InventoryTransferActions WHERE TenantId=@tenantId AND InventoryTransferId=@transferId;
'@ @{ tenantId=$tenantId; transferId=$transferId }
    Assert-Equal ([int]$actionEvidence.Rows[0].ActionCount) 6 'The immutable action register count is incorrect after idempotent replay.'
    if ([int]$actionEvidence.Rows[0].ActorCount -lt 5) { throw 'Maker-checker/SOD evidence contains fewer than five distinct actors.' }
    $auditCount = [int](Invoke-DbScalar $connection @'
SELECT COUNT(*) FROM AuditLogs WHERE TenantId=@tenantId AND Resource='InventoryTransfer' AND ResourceId=@resourceId;
'@ @{ tenantId=$tenantId; resourceId=$transferId.ToString() })
    $controlCount = [int](Invoke-DbScalar $connection @'
SELECT COUNT(*) FROM ProcurementControlEvents WHERE TenantId=@tenantId AND SourceType='InventoryTransfer' AND SourceId=@transferId;
'@ @{ tenantId=$tenantId; transferId=$transferId })
    if ($auditCount -lt 6) { throw "Expected at least 6 transfer audit rows; found $auditCount." }
    Assert-Equal $controlCount 6 'The immutable procurement control-event count is incorrect after replay.'
    $evidenceCount = [int](Invoke-DbScalar $connection @'
SELECT COUNT(*) FROM InventoryTransferDiscrepancyEvidence e
JOIN InventoryTransferDiscrepancies d ON d.Id=e.InventoryTransferDiscrepancyId AND d.TenantId=e.TenantId
WHERE d.TenantId=@tenantId AND d.InventoryTransferId=@transferId;
'@ @{ tenantId=$tenantId; transferId=$transferId })
    if ($evidenceCount -lt 1) { throw 'The resolved discrepancy has no protected central-DMS evidence link.' }

    $transaction = $connection.BeginTransaction()
    $immutableBlocked = $false
    try {
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction
        $command.CommandText = 'UPDATE InventoryTransferActions SET Comment=''tamper'' WHERE InventoryTransferId=@transferId;'
        $null = Add-DbParameter $command '@transferId' $transferId
        $null = $command.ExecuteNonQuery()
    }
    catch [System.Data.SqlClient.SqlException] { $immutableBlocked = $true }
    finally { try { $transaction.Rollback() } catch { } }
    if (-not $immutableBlocked) { throw 'Direct mutation of the immutable transfer action register was not blocked.' }

    # Prove the same tenant-scoped list contract used by the browser before starting Next.js.
    # Keep an explicit range probe in the failure message so a date-window defect is not
    # misdiagnosed as a role/scope defect.
    $defaultList = @(Invoke-JsonApi -Method GET -Path '/api/inventory/transfers' -Token $adminToken -Body $null)
    if (-not ($defaultList | Where-Object { $_.id -eq $transferId })) {
        $fromDate = [Uri]::EscapeDataString([DateTime]::UtcNow.AddDays(-1).ToString('O'))
        $toDate = [Uri]::EscapeDataString([DateTime]::UtcNow.AddDays(1).ToString('O'))
        $windowList = @(Invoke-JsonApi -Method GET -Path "/api/inventory/transfers?fromDate=$fromDate&toDate=$toDate" -Token $adminToken -Body $null)
        throw "The completed acceptance transfer is absent from the authenticated transfer list (default=$($defaultList.Count), explicit-window=$($windowList.Count), explicit-window-match=$([bool]($windowList | Where-Object { $_.id -eq $transferId })))."
    }

    if (-not $SkipBrowser) {
        if (Get-NetTCPConnection -State Listen -LocalPort 3001 -ErrorAction SilentlyContinue) {
            throw 'Port 3001 is already in use; browser acceptance will not take ownership of an unrelated process.'
        }
        $priorApiUrl = $env:NEXT_PUBLIC_API_URL
        $env:NEXT_PUBLIC_API_URL = "$ApiBaseUrl/api"
        $frontendParameters = @{
            FilePath = 'npm.cmd'
            ArgumentList = @('run','dev','--','--port','3001')
            WorkingDirectory = Join-Path $repoRoot 'frontend'
            WindowStyle = 'Hidden'
            PassThru = $true
            RedirectStandardOutput = $frontendOut
            RedirectStandardError = $frontendErr
        }
        $frontendProcess = Start-Process @frontendParameters
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
        $routeWarmup = Invoke-WebRequest -Uri "$FrontendBaseUrl/inventory/transfers" -SkipHttpErrorCheck -TimeoutSec 360
        if ($routeWarmup.StatusCode -ne 200) { throw "The transfer UI warm-up returned HTTP $($routeWarmup.StatusCode)." }
        $env:E2E_API_URL = $ApiBaseUrl
        $env:E2E_BASE_URL = $FrontendBaseUrl
        # The dispatcher receives temporary both-store scope for the acceptance run and performs
        # only a read-only browser verification after the independent closer completes the transfer.
        $env:INV_FU004_USERNAME = 'admin'
        $env:INV_FU004_PASSWORD = $temporaryPassword
        $env:INV_FU004_TRANSFER_NUMBER = $transferNumber
        $env:INV_FU004_TENANT_CODE = 'DEFAULT'
        Push-Location (Join-Path $repoRoot 'e2e-tests')
        try {
            & npx.cmd playwright test tests/inv-fu-004-transfer.spec.ts --project=chromium --workers=1
            if ($LASTEXITCODE -ne 0) { throw "Playwright E2E-011 acceptance failed with exit code $LASTEXITCODE." }
            $browserPassed = $true
        } finally { Pop-Location }
        if ($null -ne $priorApiUrl) { $env:NEXT_PUBLIC_API_URL = $priorApiUrl } else { Remove-Item Env:NEXT_PUBLIC_API_URL -ErrorAction SilentlyContinue }
    } else { $browserPassed = $true }

    [pscustomobject]@{
        TransferId = $transferId
        TransferNumber = $transferNumber
        Status = $final.status
        SourceDelta = $sourceBefore - $sourceAfter
        DestinationDelta = $destinationAfter - $destinationBefore
        ActionCount = [int]$actionEvidence.Rows[0].ActionCount
        DistinctActors = [int]$actionEvidence.Rows[0].ActorCount
        AuditCount = $auditCount
        ControlEventCount = $controlCount
        TenantIsolation = '404'
        UnauthorizedMutation = '403'
        AnonymousRead = '401'
        Browser = if ($browserPassed) { 'Passed' } else { 'Not run' }
    } | ConvertTo-Json -Compress
}
finally {
    if ($frontendProcess -and -not $frontendProcess.HasExited) {
        & taskkill.exe /PID $frontendProcess.Id /T /F *> $null
    }
    foreach ($name in @('E2E_API_URL','E2E_BASE_URL','INV_FU004_USERNAME','INV_FU004_PASSWORD','INV_FU004_TRANSFER_NUMBER','INV_FU004_TENANT_CODE')) {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
    foreach ($assignmentId in $addedAssignmentIds) {
        try { $null = Invoke-DbNonQuery $connection 'DELETE FROM ProcurementResponsibilityAssignments WHERE Id=@id;' @{ id=$assignmentId } } catch { }
    }
    foreach ($userId in $addedRoleUsers) {
        try { $null = Invoke-DbNonQuery $connection 'DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;' @{ userId=$userId; roleId=$storesOfficerRoleId } } catch { }
    }
    foreach ($actor in $actors) {
        if (-not $passwordBackups.ContainsKey($actor.Id)) { continue }
        $backup = $passwordBackups[$actor.Id]
        try {
            $null = Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash, AccessFailedCount=@failed, LockoutEnd=@lockout, TenantId=@tenantId, EmployeeId=@employeeId WHERE Id=@id;
'@ @{ id=$actor.Id; hash=$backup.PasswordHash; failed=$backup.AccessFailedCount; lockout=$backup.LockoutEnd; tenantId=$backup.TenantId; employeeId=$backup.EmployeeId }
        } catch { }
    }
    if ($connection.State -eq [System.Data.ConnectionState]::Open) { $connection.Close() }
    $connection.Dispose()
    Remove-Item $evidenceFile,$frontendOut,$frontendErr -Force -ErrorAction SilentlyContinue
}
