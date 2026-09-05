[CmdletBinding()]
param(
    [Guid]$PurchaseOrderId = 'b1848885-fba4-4289-bd7c-466675c7e676',
    [Guid]$ResumeReceiptId = [Guid]::Empty,
    [string]$ApiBaseUrl = 'http://127.0.0.1:5000',
    [string]$ServiceConfigurationPath = 'C:\RhemaERP\services\api\RhemaERPAPI.xml'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$tenantId = [Guid]'00000000-0000-0000-0000-000000000001'
$actors = @(
    @{ Key = 'admin'; Id = [Guid]'58cafd8b-42ce-4f67-0dbb-08de862e82ee' },
    @{ Key = 'receiver'; Id = [Guid]'77af28cf-66d3-49c0-0dbd-08de862e82ee' },
    @{ Key = 'storesManager'; Id = [Guid]'9e475ce9-34ad-4a6d-0dbc-08de862e82ee' },
    @{ Key = 'financeMaker'; Id = [Guid]'3057b383-9a7a-459c-c40d-08deed5e48d0' },
    @{ Key = 'financeApprover'; Id = [Guid]'8452d8d8-ace3-4f42-f160-08deea6c9143' }
)

function Add-DbParameter {
    param([System.Data.SqlClient.SqlCommand]$Command, [string]$Name, $Value)
    [void]$Command.Parameters.AddWithValue(
        $Name,
        $(if ($null -eq $Value) { [DBNull]::Value } else { $Value }))
}

function Invoke-DbTable {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql,
        [hashtable]$Parameters = @{}
    )
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 120
    foreach ($entry in $Parameters.GetEnumerator()) {
        Add-DbParameter $command "@$($entry.Key)" $entry.Value
    }
    $table = [System.Data.DataTable]::new()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    [void]$adapter.Fill($table)
    Write-Output -NoEnumerate $table
}

function Invoke-DbNonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql,
        [hashtable]$Parameters = @{}
    )
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 120
    foreach ($entry in $Parameters.GetEnumerator()) {
        Add-DbParameter $command "@$($entry.Key)" $entry.Value
    }
    $command.ExecuteNonQuery()
}

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body,
        [int[]]$ExpectedStatus = @(200)
    )
    $headers = @{
        'X-Correlation-ID' = "LIVE-PO-RECEIPT-INVOICE-$([Guid]::NewGuid().ToString('N'))"
    }
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    $parameters = @{
        Uri = "$ApiBaseUrl$Path"
        Method = $Method
        Headers = $headers
        TimeoutSec = 180
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 30 -Compress
    }
    $statusCode = 0
    $content = ''
    try {
        $response = Invoke-WebRequest @parameters -UseBasicParsing
        $statusCode = [int]$response.StatusCode
        $content = if ($response.Content -is [byte[]]) {
            [Text.Encoding]::UTF8.GetString($response.Content)
        } else {
            [string]$response.Content
        }
    }
    catch [System.Net.WebException] {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) { throw }
        $statusCode = [int]$errorResponse.StatusCode
        $stream = $errorResponse.GetResponseStream()
        try {
            $reader = [IO.StreamReader]::new($stream)
            try { $content = $reader.ReadToEnd() }
            finally { $reader.Dispose() }
        }
        finally { if ($null -ne $stream) { $stream.Dispose() } }
    }
    if ($statusCode -notin $ExpectedStatus) {
        throw "$Method $Path returned ${statusCode}: $content"
    }
    if ([string]::IsNullOrWhiteSpace($content)) { return $null }
    $content | ConvertFrom-Json
}

function Login-Actor([string]$UserName, [string]$Password) {
    $result = Invoke-JsonApi POST '/api/auth/login' '' @{
        username = $UserName
        password = $Password
        rememberMe = $false
    }
    if ([string]::IsNullOrWhiteSpace($result.token)) {
        throw "Login did not issue a token for $UserName."
    }
    [string]$result.token
}

function Write-NetworkUInt32 {
    param([byte[]]$Buffer, [int]$Offset, [uint32]$Value)
    $Buffer[$Offset] = [byte](($Value -shr 24) -band 0xff)
    $Buffer[$Offset + 1] = [byte](($Value -shr 16) -band 0xff)
    $Buffer[$Offset + 2] = [byte](($Value -shr 8) -band 0xff)
    $Buffer[$Offset + 3] = [byte]($Value -band 0xff)
}

function New-IdentityV3PasswordHash([string]$Password) {
    [byte[]]$salt = New-Object byte[] 16
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    $random.GetBytes($salt)
    $random.Dispose()
    $derive = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $Password,
        $salt,
        100000,
        [Security.Cryptography.HashAlgorithmName]::SHA512)
    [byte[]]$subkey = $derive.GetBytes(32)
    $derive.Dispose()
    [byte[]]$output = New-Object byte[] 61
    $output[0] = 1
    Write-NetworkUInt32 $output 1 2
    Write-NetworkUInt32 $output 5 100000
    Write-NetworkUInt32 $output 9 16
    [Array]::Copy($salt, 0, $output, 13, $salt.Length)
    [Array]::Copy($subkey, 0, $output, 13 + $salt.Length, $subkey.Length)
    [Convert]::ToBase64String($output)
}

function Add-TemporaryRole {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [Guid]$UserId,
        [string]$RoleName,
        [Collections.Generic.List[object]]$AddedMemberships
    )
    $role = Invoke-DbTable $Connection 'SELECT Id FROM AspNetRoles WHERE Name=@name;' @{
        name = $RoleName
    }
    if ($role.Rows.Count -ne 1) { throw "Required role $RoleName is unavailable." }
    $roleId = [Guid]$role.Rows[0].Id
    $membership = Invoke-DbTable $Connection @'
SELECT UserId FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;
'@ @{ userId = $UserId; roleId = $roleId }
    if ($membership.Rows.Count -eq 0) {
        [void](Invoke-DbNonQuery $Connection @'
INSERT INTO UserRoles(UserId,RoleId) VALUES(@userId,@roleId);
'@ @{ userId = $UserId; roleId = $roleId })
        $AddedMemberships.Add([pscustomobject]@{ UserId = $UserId; RoleId = $roleId })
    }
    $roleId
}

function Add-TemporaryResponsibility {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [Guid]$UserId,
        [Guid]$RoleId,
        [string]$RoleName,
        [Collections.Generic.List[Guid]]$AddedAssignments
    )
    $existing = Invoke-DbTable $Connection @'
SELECT TOP(1) Id
FROM ProcurementResponsibilityAssignments
WHERE TenantId=@tenantId AND UserId=@userId AND RoleName=@roleName
  AND IsDeleted=0 AND IsActive=1 AND EffectiveFrom<=SYSUTCDATETIME()
  AND (EffectiveTo IS NULL OR EffectiveTo>=SYSUTCDATETIME());
'@ @{ tenantId = $tenantId; userId = $UserId; roleName = $RoleName }
    if ($existing.Rows.Count -gt 0) { return }
    $id = [Guid]::NewGuid()
    [void](Invoke-DbNonQuery $Connection @'
INSERT INTO ProcurementResponsibilityAssignments(
    Id,UserId,RoleId,RoleName,WarehouseScopeMode,LocationScopeMode,
    EffectiveFrom,EffectiveTo,IsActive,Reason,CreatedAt,CreatedBy,
    CreatedById,IsDeleted,TenantId)
VALUES(
    @id,@userId,@roleId,@roleName,1,1,
    DATEADD(day,-1,SYSUTCDATETIME()),NULL,1,@reason,SYSUTCDATETIME(),
    'Live receipt acceptance harness',@adminId,0,@tenantId);
'@ @{
        id = $id
        userId = $UserId
        roleId = $RoleId
        roleName = $RoleName
        reason = 'Temporary live receipt-to-invoice acceptance responsibility; removed automatically.'
        adminId = $actors[0].Id
        tenantId = $tenantId
    })
    $AddedAssignments.Add($id)
}

function Upload-Waybill {
    param([Guid]$ReceiptId, [string]$Token, [string]$Reference)
    # Small valid one-page PDF used only as controlled UAT Waybill evidence.
    $pdf = [Text.Encoding]::ASCII.GetBytes(@'
%PDF-1.4
1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj
4 0 obj<</Length 61>>stream
BT
/F1 12 Tf
72 770 Td
(Controlled UAT Waybill Evidence) Tj
ET
endstream
endobj
5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj
xref
0 6
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000241 00000 n 
0000000351 00000 n 
trailer<</Size 6/Root 1 0 R>>
startxref
421
%%EOF
'@)
    $path = Join-Path ([IO.Path]::GetTempPath()) "uat-waybill-$ReceiptId.pdf"
    try {
        [IO.File]::WriteAllBytes($path, $pdf)
        $client = [Net.Http.HttpClient]::new()
        $client.Timeout = [TimeSpan]::FromSeconds(180)
        $client.DefaultRequestHeaders.Authorization =
            [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
        $multipart = [Net.Http.MultipartFormDataContent]::new()
        $stream = [IO.File]::OpenRead($path)
        $content = [Net.Http.StreamContent]::new($stream)
        $content.Headers.ContentType =
            [Net.Http.Headers.MediaTypeHeaderValue]::new('application/pdf')
        $multipart.Add($content, 'file', "waybill-$ReceiptId.pdf")
        foreach ($field in @{
            EvidenceKind = 'Waybill'
            ReferenceNumber = $Reference
            DocumentDate = (Get-Date).ToUniversalTime().ToString('o')
            ClientRequestId = [Guid]::NewGuid().ToString()
        }.GetEnumerator()) {
            $multipart.Add([Net.Http.StringContent]::new([string]$field.Value), $field.Key)
        }
        try {
            $response = $client.PostAsync(
                "$ApiBaseUrl/api/procurement/purchase-order-receipts/$ReceiptId/source-evidence",
                $multipart).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        } finally {
            $multipart.Dispose()
            $stream.Dispose()
            $client.Dispose()
        }
        if ([int]$response.StatusCode -notin @(200, 201)) {
            throw "Waybill upload returned $([int]$response.StatusCode): $body"
        }
    } finally {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path -LiteralPath $ServiceConfigurationPath)) {
    throw "Service configuration is missing: $ServiceConfigurationPath"
}
[xml]$serviceConfiguration = Get-Content -LiteralPath $ServiceConfigurationPath
$connectionString = [string](($serviceConfiguration.service.env | Where-Object {
    $_.name -eq 'ConnectionStrings__DefaultConnection'
}).value)
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'The API database connection setting is unavailable.'
}

Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Net.Http
$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$passwordBackups = @{}
$addedMemberships = [Collections.Generic.List[object]]::new()
$addedAssignments = [Collections.Generic.List[Guid]]::new()
$tokens = @{}
$temporaryPassword = "Receipt!$([Guid]::NewGuid().ToString('N'))aA7"
$temporaryHash = New-IdentityV3PasswordHash $temporaryPassword

try {
    $connection.Open()
    foreach ($actor in $actors) {
        $row = Invoke-DbTable $connection @'
SELECT UserName,PasswordHash,AccessFailedCount,LockoutEnd
FROM Users
WHERE Id=@id AND TenantId=@tenantId AND IsActive=1;
'@ @{ id = $actor.Id; tenantId = $tenantId }
        if ($row.Rows.Count -ne 1) { throw "Acceptance actor $($actor.Key) is unavailable." }
        $actor.UserName = [string]$row.Rows[0].UserName
        $passwordBackups[$actor.Id] = @{
            PasswordHash = if ($row.Rows[0].IsNull('PasswordHash')) { $null } else { [string]$row.Rows[0].PasswordHash }
            AccessFailedCount = [int]$row.Rows[0].AccessFailedCount
            LockoutEnd = if ($row.Rows[0].IsNull('LockoutEnd')) { $null } else { $row.Rows[0].LockoutEnd }
        }
        [void](Invoke-DbNonQuery $connection @'
UPDATE Users SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL WHERE Id=@id;
'@ @{ id = $actor.Id; hash = $temporaryHash })
    }

    $receiverRoleId = Add-TemporaryRole $connection $actors[1].Id 'TDC_STORES_OFFICER' $addedMemberships
    Add-TemporaryResponsibility $connection $actors[1].Id $receiverRoleId 'TDC_STORES_OFFICER' $addedAssignments
    $managerRoleId = Add-TemporaryRole $connection $actors[2].Id 'TDC_STORES_MANAGER' $addedMemberships
    Add-TemporaryResponsibility $connection $actors[2].Id $managerRoleId 'TDC_STORES_MANAGER' $addedAssignments
    [void](Add-TemporaryRole $connection $actors[3].Id 'Accounts Payable Officer' $addedMemberships)
    [void](Add-TemporaryRole $connection $actors[4].Id 'Finance Manager' $addedMemberships)
    [void](Add-TemporaryRole $connection $actors[4].Id 'Accounts Officer' $addedMemberships)
    [void](Add-TemporaryRole $connection $actors[4].Id 'Financial Controller' $addedMemberships)

    foreach ($actor in $actors) {
        $tokens[$actor.Key] = Login-Actor $actor.UserName $temporaryPassword
    }

    $po = Invoke-JsonApi GET "/api/PurchaseOrders/$PurchaseOrderId" $tokens.admin $null
    if ($po.status -notin @('Approved', 'Partially Received', 'Received')) {
        throw "Purchase order $($po.orderNumber) is not receivable; status is $($po.status)."
    }
    $openLines = @($po.items | Where-Object {
        [decimal]$_.orderedQuantity -gt [decimal]$_.receivedQuantity
    })
    if ($openLines.Count -eq 0 -and $ResumeReceiptId -eq [Guid]::Empty) {
        throw "Purchase order $($po.orderNumber) has no remaining quantity to receive."
    }

    $location = Invoke-DbTable $connection @'
SELECT TOP(1) wl.Id,wl.WarehouseId,wl.LocationCode,w.Code WarehouseCode
FROM WarehouseLocations wl
JOIN Warehouses w ON w.Id=wl.WarehouseId AND w.TenantId=wl.TenantId
WHERE wl.TenantId=@tenantId AND wl.IsDeleted=0 AND wl.IsActive=1
  AND wl.IsReceivingLocation=1 AND wl.IsConsignmentBin=0
  AND w.IsDeleted=0 AND w.IsActive=1 AND w.IsConsignmentWarehouse=0
ORDER BY w.IsDefault DESC,wl.LocationCode;
'@ @{ tenantId = $tenantId }
    if ($location.Rows.Count -ne 1) { throw 'No active receiving location is configured.' }
    $locationId = [Guid]$location.Rows[0].Id
    $warehouseId = [Guid]$location.Rows[0].WarehouseId

    if ($ResumeReceiptId -ne [Guid]::Empty) {
        $resume = Invoke-DbTable $connection @'
SELECT r.Id,r.DeliveryNote,ri.LocationId,wl.WarehouseId,
       (SELECT COUNT(*) FROM PurchaseOrderReceiptItems x
        WHERE x.ReceiptId=r.Id AND x.TenantId=r.TenantId AND x.IsDeleted=0) ReceiptLineCount
FROM PurchaseOrderReceipts r
JOIN PurchaseOrderReceiptItems ri ON ri.ReceiptId=r.Id AND ri.TenantId=r.TenantId AND ri.IsDeleted=0
JOIN WarehouseLocations wl ON wl.Id=ri.LocationId AND wl.TenantId=ri.TenantId AND wl.IsDeleted=0
WHERE r.Id=@receiptId AND r.PurchaseOrderId=@purchaseOrderId
  AND r.TenantId=@tenantId AND r.IsDeleted=0;
'@ @{ receiptId = $ResumeReceiptId; purchaseOrderId = $PurchaseOrderId; tenantId = $tenantId }
        if ($resume.Rows.Count -eq 0) { throw 'The requested resume receipt does not belong to this purchase order.' }
        $locationId = [Guid]$resume.Rows[0].LocationId
        $warehouseId = [Guid]$resume.Rows[0].WarehouseId
        $expectedReceiptLineCount = [int]$resume.Rows[0].ReceiptLineCount
        $deliveryReference = [string]$resume.Rows[0].DeliveryNote
        $receiptId = $ResumeReceiptId
    }

    $category = Invoke-DbTable $connection @'
SELECT TOP(1) Id,Code,Name
FROM InventoryCategories
WHERE TenantId=@tenantId AND IsDeleted=0 AND IsActive=1
ORDER BY CASE WHEN Code IN ('IT','ICT','GOODS') THEN 0 ELSE 1 END,Name;
'@ @{ tenantId = $tenantId }
    if ($category.Rows.Count -ne 1) { throw 'No active inventory category is configured.' }
    $categoryId = [Guid]$category.Rows[0].Id

    if ($ResumeReceiptId -eq [Guid]::Empty) {
        $deliveryReference = "UAT-$($po.orderNumber)-$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"
        $idempotencyKey = "live-receipt-$($PurchaseOrderId.ToString('N'))-$([Guid]::NewGuid().ToString('N'))"
        $receiveBody = @{
        purchaseOrderId = $PurchaseOrderId
        deliveryNote = $deliveryReference
        carrierName = 'Controlled UAT delivery'
        trackingNumber = $deliveryReference
        receivedById = $actors[1].Id
        inspectedById = $null
        notes = 'Live end-to-end procurement receipt pending independent inspection.'
        requiresInspection = $true
        idempotencyKey = $idempotencyKey
        items = @($openLines | ForEach-Object {
            $missingItem = [Guid]$_.inventoryItemId -eq [Guid]::Empty
            @{
                purchaseOrderItemId = [Guid]$_.id
                receivedQuantity = [decimal]$_.orderedQuantity - [decimal]$_.receivedQuantity
                acceptedQuantity = 0
                rejectedQuantity = 0
                warehouseId = $warehouseId
                locationId = $locationId
                createInventoryItemIfMissing = $missingItem
                inventoryCategoryId = $(if ($missingItem) { $categoryId } else { $null })
                proposedItemCode = $(if ($missingItem) { "UAT-$($po.orderNumber)-$(([string]$_.itemDescription -replace '[^A-Za-z0-9]','-').Trim('-'))" } else { $null })
                lotNumber = "LOT-$($po.orderNumber)"
                notes = 'Controlled receipt line pending inspection.'
                qualityStatus = 'Passed'
                qualityNotes = 'Quantity and condition to be confirmed independently.'
            }
        })
        }
        $receipt = Invoke-JsonApi POST "/api/PurchaseOrders/$PurchaseOrderId/receive" $tokens.receiver $receiveBody @(200, 201)
        $receiptId = [Guid]$receipt.id
        $expectedReceiptLineCount = $openLines.Count
    }

    $itemProof = Invoke-DbTable $connection @'
SELECT poi.Id PurchaseOrderItemId,poi.InventoryItemId,i.ItemCode,i.Name ItemName,i.CategoryId,
       wl.WarehouseId,pori.LocationId,
       COALESCE(iq.CurrentStock,0) QuantityOnHandBeforeInspection,
       CASE WHEN EXISTS (
           SELECT 1 FROM ProcurementReceiptInspectionCases inspection
           WHERE inspection.PurchaseOrderReceiptId=@receiptId
             AND inspection.TenantId=poi.TenantId
             AND inspection.IsDeleted=0
             AND inspection.Status=8
             AND inspection.StockPostedQuantity>0
       ) THEN 1 ELSE 0 END InspectionStockAlreadyPosted
FROM PurchaseOrderItems poi
JOIN InventoryItems i ON i.Id=poi.InventoryItemId AND i.TenantId=poi.TenantId AND i.IsDeleted=0
JOIN PurchaseOrderReceiptItems pori ON pori.PurchaseOrderItemId=poi.Id AND pori.ReceiptId=@receiptId AND pori.IsDeleted=0
JOIN WarehouseLocations wl ON wl.Id=pori.LocationId AND wl.TenantId=pori.TenantId AND wl.IsDeleted=0
LEFT JOIN WarehouseQuantities iq ON iq.InventoryItemId=i.Id AND iq.WarehouseId=wl.WarehouseId AND iq.IsDeleted=0
WHERE poi.TenantId=@tenantId AND poi.PurchaseOrderId=@purchaseOrderId AND poi.IsDeleted=0;
'@ @{ receiptId = $receiptId; tenantId = $tenantId; purchaseOrderId = $PurchaseOrderId }
    if ($itemProof.Rows.Count -ne $expectedReceiptLineCount) {
        throw 'Not every received PO line is linked to a controlled inventory item.'
    }
    foreach ($row in $itemProof.Rows) {
        if ([Guid]$row.CategoryId -eq [Guid]::Empty -or
            [Guid]$row.WarehouseId -ne $warehouseId -or
            [Guid]$row.LocationId -ne $locationId) {
            throw 'The created item or receipt destination does not match the receiver decision.'
        }
        if ([int]$row.InspectionStockAlreadyPosted -eq 0 -and
            [decimal]$row.QuantityOnHandBeforeInspection -ne 0) {
            throw 'Stock increased before the independent inspection decision.'
        }
    }

    $sourceEvidence = Invoke-JsonApi GET "/api/procurement/purchase-order-receipts/$receiptId/source-evidence" $tokens.receiver $null
    if (-not $sourceEvidence.waybillReady) {
        Upload-Waybill $receiptId $tokens.receiver $deliveryReference
        $sourceEvidence = Invoke-JsonApi GET "/api/procurement/purchase-order-receipts/$receiptId/source-evidence" $tokens.receiver $null
    }
    if (-not $sourceEvidence.waybillReady) {
        throw 'The controlled Waybill did not reach clean DMS readiness.'
    }
    $waybill = @($sourceEvidence.evidence | Where-Object {
        "$($_.evidenceKind)" -in @('Waybill', '1') -and $_.isCurrent
    }) | Select-Object -First 1
    $upload = Invoke-DbTable $connection @'
SELECT FileUploadRecordId
FROM CentralDocumentVersions
WHERE TenantId=@tenantId AND Id=@versionId AND IsDeleted=0;
'@ @{ tenantId = $tenantId; versionId = [Guid]$waybill.centralDocumentVersionId }
    if ($upload.Rows.Count -ne 1) { throw 'Waybill upload lineage is missing.' }
    $fileUploadRecordId = [Guid]$upload.Rows[0].FileUploadRecordId

    $overview = Invoke-JsonApi GET "/api/PurchaseOrderReceipts/$receiptId/inspection-control" $tokens.receiver $null
    $inspection = $overview.current
    if (-not $inspection) {
        $inspection = Invoke-JsonApi POST "/api/PurchaseOrderReceipts/$receiptId/inspection-control/initialize" $tokens.receiver $null @(200, 201)
        $overview = Invoke-JsonApi GET "/api/PurchaseOrderReceipts/$receiptId/inspection-control" $tokens.receiver $null
    }
    if ("$($inspection.status)" -in @('Draft', '0')) {
        $inspection = Invoke-JsonApi PUT "/api/PurchaseOrderReceipts/$receiptId/inspection-control" $tokens.receiver @{
            comment = 'All delivered quantities passed independent receipt inspection.'
            idempotencyKey = "live-inspection-save-$($receiptId.ToString('N'))"
            rowVersion = $inspection.rowVersion
            lines = @($inspection.lines | ForEach-Object {
                @{
                    purchaseOrderReceiptItemId = $_.purchaseOrderReceiptItemId
                    acceptedQuantity = $_.receivedQuantity
                    rejectedQuantity = 0
                    rejectionReason = $null
                    inspectionNotes = 'Quantity, specification and condition accepted.'
                    quarantineLocationId = $null
                }
            })
        }
        $evidence = @($overview.evidenceRequirementKeys | ForEach-Object {
            @{
                actionKey = 'SubmitReceiptInspection'
                requirementKey = $_
                referenceKind = 'CentralDocumentUpload'
                workflowEvidenceDocumentId = $null
                fileUploadRecordId = $fileUploadRecordId
                evidenceReference = "$deliveryReference / $_"
            }
        })
        $inspection = Invoke-JsonApi POST "/api/PurchaseOrderReceipts/inspection-control/$($inspection.id)/submit" $tokens.receiver @{
            comment = 'Submit accepted receipt for independent Stores Manager approval.'
            rowVersion = $inspection.rowVersion
            evidence = $evidence
        }
    }
    if ("$($inspection.status)" -in @('PendingApproval', '1')) {
        $inspection = Invoke-JsonApi POST "/api/PurchaseOrderReceipts/inspection-control/$($inspection.id)/decision" $tokens.storesManager @{
            approved = $true
            comment = 'Independent Stores Manager approval of accepted stock.'
            rowVersion = $inspection.rowVersion
        }
    }
    if ("$($inspection.status)" -notin @('Closed', '8')) {
        throw "Inspection did not close; current status is $($inspection.status)."
    }

    $stockProof = Invoke-DbTable $connection @'
SELECT poi.InventoryItemId,wl.WarehouseId,pori.LocationId,
       COALESCE(iq.CurrentStock,0) WarehouseQuantity,
       COALESCE(il.Quantity,0) LocationQuantity,
       (SELECT COUNT(*) FROM InventoryMovements m
        WHERE m.TenantId=poi.TenantId AND m.ReferenceId=@receiptId
          AND m.InventoryItemId=poi.InventoryItemId AND m.IsPosted=1 AND m.IsDeleted=0) MovementCount
FROM PurchaseOrderItems poi
JOIN PurchaseOrderReceiptItems pori ON pori.PurchaseOrderItemId=poi.Id AND pori.ReceiptId=@receiptId AND pori.IsDeleted=0
JOIN WarehouseLocations wl ON wl.Id=pori.LocationId AND wl.TenantId=pori.TenantId AND wl.IsDeleted=0
LEFT JOIN WarehouseQuantities iq ON iq.InventoryItemId=poi.InventoryItemId AND iq.WarehouseId=wl.WarehouseId AND iq.IsDeleted=0
LEFT JOIN InventoryLocations il ON il.InventoryItemId=poi.InventoryItemId AND il.LocationId=pori.LocationId AND il.IsDeleted=0
WHERE poi.TenantId=@tenantId AND poi.PurchaseOrderId=@purchaseOrderId AND poi.IsDeleted=0;
'@ @{ receiptId = $receiptId; tenantId = $tenantId; purchaseOrderId = $PurchaseOrderId }
    foreach ($row in $stockProof.Rows) {
        if ([decimal]$row.WarehouseQuantity -le 0 -or
            [decimal]$row.LocationQuantity -le 0 -or
            [int]$row.MovementCount -ne 1) {
            throw 'Accepted stock was not posted exactly once to the selected warehouse and location.'
        }
    }

    $documents = Invoke-JsonApi GET "/api/ProcurementReceiptDocuments/receipt/$receiptId" $tokens.admin $null
    foreach ($document in @($documents.documents | Sort-Object documentKind)) {
        $current = $document
        foreach ($requiredRole in @($current.requiredSignatures)) {
            if (@($current.signatures | Where-Object { $_.requiredRole -eq $requiredRole }).Count -gt 0) { continue }
            $normalized = ($requiredRole -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
            $signerToken = if ($normalized -eq 'STORES') { $tokens.receiver } elseif ($normalized -eq 'APPROVINGOFFICER') { $tokens.storesManager } else { $tokens.admin }
            $current = Invoke-JsonApi POST "/api/ProcurementReceiptDocuments/$($current.id)/sign" $signerToken @{
                requiredRole = $requiredRole
                comment = "Live UAT $requiredRole receipt attestation."
                rowVersion = $current.rowVersion
            }
        }
        if ("$($current.status)" -notin @('Issued', '2')) {
            $current = Invoke-JsonApi POST "/api/ProcurementReceiptDocuments/$($current.id)/issue" $tokens.storesManager @{
                comment = 'Issue after inspection, evidence and signatures.'
                rowVersion = $current.rowVersion
            }
        }
    }
    $documents = Invoke-JsonApi POST "/api/ProcurementReceiptDocuments/receipt/$receiptId/reconcile" $tokens.storesManager $null
    $incompleteDocuments = @($documents.documents | Where-Object {
        "$($_.status)" -notin @('Issued', '2') -or
        "$($_.reconciliationStatus)" -notin @('Reconciled', '1')
    })
    if ($incompleteDocuments.Count -gt 0) {
        throw 'The GRN/MRN register did not reach issued and reconciled state.'
    }

    $supply = Invoke-JsonApi GET "/api/ap/invoices/accepted-supply-options?purchaseOrderId=$PurchaseOrderId" $tokens.financeMaker $null
    $acceptedSupply = @($supply.options | Where-Object {
        "$($_.kind)" -in @('GoodsReceiptInspection', '1') -and [Guid]$_.sourceId -eq $PurchaseOrderId
    }) | Select-Object -First 1
    if (-not $acceptedSupply) {
        throw "Finance/AP cannot resolve accepted supply: $($supply.blockedReasons -join '; ')."
    }

    $commercial = Invoke-DbTable $connection @'
SELECT TOP(1) po.BusinessPartnerId,poi.Id PurchaseOrderItemId,poi.ItemDescription,
       poi.OrderedQuantity,poi.UnitPrice,poi.UnitOfMeasure,
       po.BusinessPartnerId SupplierProjectionId
FROM PurchaseOrders po
JOIN PurchaseOrderItems poi ON poi.PurchaseOrderId=po.Id AND poi.TenantId=po.TenantId AND poi.IsDeleted=0
JOIN BusinessPartners bp ON bp.Id=po.BusinessPartnerId AND bp.TenantId=po.TenantId AND bp.IsDeleted=0
WHERE po.Id=@purchaseOrderId AND po.TenantId=@tenantId AND po.IsDeleted=0;
'@ @{ purchaseOrderId = $PurchaseOrderId; tenantId = $tenantId }
    if ($commercial.Rows.Count -ne 1) {
        throw 'The PO supplier or commercial line is unavailable to Finance/AP.'
    }
    $line = $commercial.Rows[0]
    $existingInvoice = Invoke-DbTable $connection @'
SELECT TOP(1) Id,Status
FROM VendorInvoice
WHERE TenantId=@tenantId AND PurchaseOrderId=@purchaseOrderId
  AND Reference=@reference AND IsDeleted=0 AND Status<>7
ORDER BY CreatedAt DESC;
'@ @{
        tenantId = $tenantId
        purchaseOrderId = $PurchaseOrderId
        reference = $deliveryReference
    }
    if ($existingInvoice.Rows.Count -gt 0) {
        $invoice = Invoke-JsonApi GET "/api/ap/invoices/$([Guid]$existingInvoice.Rows[0].Id)" $tokens.financeMaker $null
    } else {
        $supplierInvoiceNumber = "UAT-$($po.orderNumber)-$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"
        $invoice = Invoke-JsonApi POST '/api/ap/invoices' $tokens.financeMaker @{
        supplierInvoiceNumber = $supplierInvoiceNumber
        supplierId = [Guid]$line.SupplierProjectionId
        purchaseOrderId = $PurchaseOrderId
        invoiceDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
        receivedDate = (Get-Date).ToUniversalTime().ToString('o')
        dueDate = (Get-Date).ToUniversalTime().AddDays(30).ToString('yyyy-MM-dd')
        currencyCode = 'GHS'
        exchangeRate = 1
        paymentTermsDays = 30
        matchingType = 2
        notes = 'Live UAT PO, accepted receipt and supplier invoice match.'
        reference = $deliveryReference
        isOpeningBalance = $false
        lineItems = @(@{
            lineItemType = 'Inventory'
            purchaseOrderItemId = [Guid]$line.PurchaseOrderItemId
            description = [string]$line.ItemDescription
            quantity = [decimal]$line.OrderedQuantity
            unitPrice = [decimal]$line.UnitPrice
            discountPercentage = 0
            unit = [string]$line.UnitOfMeasure
        })
        } @(201)
    }

    $invoiceState = [int](Invoke-DbTable $connection 'SELECT Status FROM VendorInvoice WHERE Id=@id AND TenantId=@tenantId AND IsDeleted=0;' @{
        id = [Guid]$invoice.id
        tenantId = $tenantId
    }).Rows[0].Status
    if ($invoiceState -eq 1) {
        $match = Invoke-JsonApi POST "/api/ap/invoices/$($invoice.id)/match/three-way" $tokens.financeMaker $null
        if (-not $match.isMatched -or -not $match.approvalReady) {
            throw "Three-way match failed: $($match.message)"
        }
        $invoice = Invoke-JsonApi POST "/api/ap/invoices/$($invoice.id)/submit" $tokens.financeMaker @{}
        $invoiceState = 2
    }
    for ($approvalAttempt = 1; $invoiceState -eq 2 -and $approvalAttempt -le 4; $approvalAttempt++) {
        $invoice = Invoke-JsonApi POST "/api/ap/invoices/$($invoice.id)/approve" $tokens.financeApprover "Independent Finance workflow approval stage $approvalAttempt after successful three-way match."
        $invoiceState = [int](Invoke-DbTable $connection 'SELECT Status FROM VendorInvoice WHERE Id=@id AND TenantId=@tenantId AND IsDeleted=0;' @{
            id = [Guid]$invoice.id
            tenantId = $tenantId
        }).Rows[0].Status
    }
    if ($invoiceState -ne 3) {
        throw "The supplier invoice did not reach Approved status; current status is $invoiceState."
    }

    $financeProof = Invoke-DbTable $connection @'
SELECT r.ReceiptNumber,r.Status ReceiptStatus,c.Status InspectionStatus,
       c.StockPostedQuantity,c.ApEligibleQuantity,
       pe.PostingStatus ReceiptPostingStatus,pe.TotalDebitAmount ReceiptDebit,
       pe.TotalCreditAmount ReceiptCredit,je.IsBalanced ReceiptJournalBalanced,
       vi.Id VendorInvoiceId,vi.InvoiceNumber,vi.Status InvoiceStatus,
       vi.MatchingStatus,vi.JournalEntryId InvoiceJournalEntryId,
       vije.IsBalanced InvoiceJournalBalanced,vije.BalanceDifference InvoiceBalanceDifference
FROM PurchaseOrderReceipts r
JOIN ProcurementReceiptInspectionCases c ON c.PurchaseOrderReceiptId=r.Id AND c.IsDeleted=0
LEFT JOIN FinancePostingEvents pe ON pe.TenantId=r.TenantId
  AND pe.SourceDocumentType='ProcurementPurchaseOrderReceipt'
  AND pe.SourceDocumentId=r.Id AND pe.PostingAction='PostAcceptedInventoryReceipt'
  AND pe.IsDeleted=0
LEFT JOIN JournalEntries je ON je.Id=pe.JournalEntryId AND je.TenantId=r.TenantId AND je.IsDeleted=0
JOIN VendorInvoice vi ON vi.Id=@invoiceId AND vi.TenantId=r.TenantId AND vi.IsDeleted=0
LEFT JOIN JournalEntries vije ON vije.Id=vi.JournalEntryId AND vije.TenantId=vi.TenantId AND vije.IsDeleted=0
WHERE r.TenantId=@tenantId AND r.Id=@receiptId;
'@ @{ tenantId = $tenantId; receiptId = $receiptId; invoiceId = [Guid]$invoice.id }
    if ($financeProof.Rows.Count -ne 1) { throw 'Receipt and invoice Finance proof is incomplete.' }
    $proof = $financeProof.Rows[0]
    if ([string]$proof.ReceiptPostingStatus -ne 'Posted' -or
        [decimal]$proof.ReceiptDebit -ne [decimal]$proof.ReceiptCredit -or
        -not [bool]$proof.ReceiptJournalBalanced -or
        -not [bool]$proof.InvoiceJournalBalanced -or
        [decimal]$proof.InvoiceBalanceDifference -ne 0) {
        throw 'Receipt or supplier-invoice journal proof is unbalanced.'
    }

    [pscustomobject]@{
        Passed = $true
        PurchaseOrderId = $PurchaseOrderId
        PurchaseOrderNumber = $po.orderNumber
        ReceiptId = $receiptId
        ReceiptNumber = $proof.ReceiptNumber
        ReceiptStatus = $proof.ReceiptStatus
        InspectionId = $inspection.id
        InspectionStatus = $proof.InspectionStatus
        WarehouseId = $warehouseId
        WarehouseCode = [string]$location.Rows[0].WarehouseCode
        LocationId = $locationId
        LocationCode = [string]$location.Rows[0].LocationCode
        CreatedItems = @($itemProof.Rows | ForEach-Object {
            @{
                Id = $_.InventoryItemId
                Code = $_.ItemCode
                Name = $_.ItemName
                CategoryId = $_.CategoryId
            }
        })
        ReceiptDocuments = @($documents.documents | ForEach-Object {
            @{
                Id = $_.id
                Kind = $_.documentKind
                Number = $_.documentNumber
                Status = $_.status
                Reconciliation = $_.reconciliationStatus
            }
        })
        ReceiptDebit = $proof.ReceiptDebit
        ReceiptCredit = $proof.ReceiptCredit
        ReceiptJournalBalanced = $proof.ReceiptJournalBalanced
        VendorInvoiceId = $proof.VendorInvoiceId
        VendorInvoiceNumber = $proof.InvoiceNumber
        VendorInvoiceStatus = $proof.InvoiceStatus
        MatchingStatus = $proof.MatchingStatus
        InvoiceJournalEntryId = $proof.InvoiceJournalEntryId
        InvoiceJournalBalanced = $proof.InvoiceJournalBalanced
    } | ConvertTo-Json -Depth 10 -Compress
} finally {
    if ($connection.State -eq [System.Data.ConnectionState]::Open) {
        foreach ($assignmentId in $addedAssignments) {
            try {
                [void](Invoke-DbNonQuery $connection @'
DELETE FROM ProcurementResponsibilityAssignments WHERE Id=@id;
'@ @{ id = $assignmentId })
            } catch {}
        }
        foreach ($membership in $addedMemberships) {
            try {
                [void](Invoke-DbNonQuery $connection @'
DELETE FROM UserRoles WHERE UserId=@userId AND RoleId=@roleId;
'@ @{ userId = $membership.UserId; roleId = $membership.RoleId })
            } catch {}
        }
        foreach ($actorId in $passwordBackups.Keys) {
            $backup = $passwordBackups[$actorId]
            try {
                [void](Invoke-DbNonQuery $connection @'
UPDATE Users
SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout
WHERE Id=@id;
'@ @{
                    id = $actorId
                    hash = $backup.PasswordHash
                    failed = $backup.AccessFailedCount
                    lockout = $backup.LockoutEnd
                })
            } catch {}
        }
        $connection.Close()
    }
    $connection.Dispose()
}
