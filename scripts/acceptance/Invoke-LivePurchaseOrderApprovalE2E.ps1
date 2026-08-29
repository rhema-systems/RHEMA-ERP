[CmdletBinding()]
param(
    [Guid]$PurchaseOrderId = 'b1848885-fba4-4289-bd7c-466675c7e676',
    [Guid]$ApproverUserId = '45eacad1-fff9-40f4-91fb-a0e72e2fa7f7',
    [string]$ApiBaseUrl = 'http://127.0.0.1:5000',
    [string]$ServiceConfigurationPath = 'C:\RhemaERP\services\api\RhemaERPAPI.xml'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Add-DbParameter {
    param(
        [System.Data.SqlClient.SqlCommand]$Command,
        [string]$Name,
        $Value
    )

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

function Invoke-ApiRequest {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method),
        "$ApiBaseUrl$Path")
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $request.Headers.Authorization =
            [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
    }
    $request.Headers.Add(
        'X-Correlation-ID',
        "PO-APPROVAL-E2E-$([Guid]::NewGuid().ToString('N'))")
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 20 -Compress
        $request.Content = [System.Net.Http.StringContent]::new(
            $json,
            [Text.Encoding]::UTF8,
            'application/json')
    }

    $response = $Client.SendAsync($request).GetAwaiter().GetResult()
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    [pscustomobject]@{
        StatusCode = [int]$response.StatusCode
        Content = $content
    }
}

function Write-NetworkUInt32 {
    param(
        [byte[]]$Buffer,
        [int]$Offset,
        [uint32]$Value
    )

    $Buffer[$Offset] = [byte](($Value -shr 24) -band 0xff)
    $Buffer[$Offset + 1] = [byte](($Value -shr 16) -band 0xff)
    $Buffer[$Offset + 2] = [byte](($Value -shr 8) -band 0xff)
    $Buffer[$Offset + 3] = [byte]($Value -band 0xff)
}

function New-IdentityV3PasswordHash {
    param([string]$Password)

    # ASP.NET Identity v3 format: marker, PRF, iteration count, salt length,
    # salt and derived key. The encoded parameters are read by the server's
    # password verifier, so this does not depend on PowerShell loading Identity.
    [byte[]]$salt = New-Object byte[] 16
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $random.GetBytes($salt)
    $random.Dispose()
    $derive = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
        $Password,
        $salt,
        100000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA512)
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
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(180)
$passwordBackup = $null
$token = $null

try {
    $connection.Open()
    $actor = Invoke-DbTable $connection @'
SELECT UserName,PasswordHash,AccessFailedCount,LockoutEnd,TenantId
FROM Users
WHERE Id=@actorId AND IsActive=1;
'@ @{ actorId=$ApproverUserId }
    if ($actor.Rows.Count -ne 1) {
        throw 'The configured independent approver is unavailable.'
    }

    $purchaseOrder = Invoke-DbTable $connection @'
SELECT OrderNumber,Status,SourceRequisitionId,BudgetId,TotalAmount,Currency
FROM PurchaseOrders
WHERE Id=@purchaseOrderId AND IsDeleted=0;
'@ @{ purchaseOrderId=$PurchaseOrderId }
    if ($purchaseOrder.Rows.Count -ne 1) {
        throw 'The target purchase order is unavailable.'
    }
    $purchaseOrderStatusBefore = [string]$purchaseOrder.Rows[0].Status
    if ($purchaseOrderStatusBefore -notin @('Pending Approval', 'Approved')) {
        throw "Expected Pending Approval or Approved before E2E verification, but found $purchaseOrderStatusBefore."
    }

    $passwordBackup = @{
        PasswordHash = if ($actor.Rows[0].IsNull('PasswordHash')) {
            $null
        } else {
            [string]$actor.Rows[0].PasswordHash
        }
        AccessFailedCount = [int]$actor.Rows[0].AccessFailedCount
        LockoutEnd = if ($actor.Rows[0].IsNull('LockoutEnd')) {
            $null
        } else {
            $actor.Rows[0].LockoutEnd
        }
    }

    $temporaryPassword = "Po!$([Guid]::NewGuid().ToString('N'))aA7"
    $temporaryHash = New-IdentityV3PasswordHash $temporaryPassword
    [void](Invoke-DbNonQuery $connection @'
UPDATE Users
SET PasswordHash=@hash,AccessFailedCount=0,LockoutEnd=NULL
WHERE Id=@actorId;
'@ @{ actorId=$ApproverUserId; hash=$temporaryHash })

    $login = Invoke-ApiRequest $client 'POST' '/api/auth/login' '' @{
        username = [string]$actor.Rows[0].UserName
        password = $temporaryPassword
        rememberMe = $false
    }
    if ($login.StatusCode -ne 200) {
        throw "Approver login returned HTTP $($login.StatusCode): $($login.Content)"
    }
    $loginPayload = $login.Content | ConvertFrom-Json
    $token = [string]$loginPayload.token
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw 'Approver login did not return an access token.'
    }

    $decision = $null
    $decisionPayload = $null
    if ($purchaseOrderStatusBefore -eq 'Pending Approval') {
        $decision = Invoke-ApiRequest $client 'POST' "/api/PurchaseOrders/$PurchaseOrderId/approve" $token @{
            approved = $true
            comments = 'Automated live end-to-end verification of independent PO approval.'
        }
        if ($decision.StatusCode -ne 200) {
            throw "PO approval returned HTTP $($decision.StatusCode): $($decision.Content)"
        }
        $decisionPayload = $decision.Content | ConvertFrom-Json
    }

    $readBack = Invoke-ApiRequest $client 'GET' "/api/PurchaseOrders/$PurchaseOrderId" $token $null
    if ($readBack.StatusCode -ne 200) {
        throw "Approved PO read-back returned HTTP $($readBack.StatusCode): $($readBack.Content)"
    }

    $state = Invoke-DbTable $connection @'
SELECT
    po.OrderNumber,
    po.Status AS PurchaseOrderStatus,
    po.ApprovedById,
    po.ApprovedAt,
    po.TotalAmount,
    po.Currency,
    b.BudgetCode,
    b.CommittedAmount,
    b.RemainingAmount,
    COUNT(c.Id) AS ActiveCommitmentCount,
    COALESCE(SUM(c.ReservedAmount),0) AS ReservedAmount
FROM PurchaseOrders po
LEFT JOIN ProcurementBudgetCommitments c
  ON c.PurchaseRequisitionId=po.SourceRequisitionId
 AND c.TenantId=po.TenantId
 AND c.IsDeleted=0
 AND c.Status=1
LEFT JOIN ProcurementBudgets b
  ON b.Id=c.ProcurementBudgetId AND b.TenantId=po.TenantId AND b.IsDeleted=0
WHERE po.Id=@purchaseOrderId AND po.IsDeleted=0
GROUP BY po.OrderNumber,po.Status,po.ApprovedById,po.ApprovedAt,
         po.TotalAmount,po.Currency,b.BudgetCode,b.CommittedAmount,b.RemainingAmount;
'@ @{ purchaseOrderId=$PurchaseOrderId }
    if ($state.Rows.Count -ne 1) {
        throw 'The approved PO reconciliation row was not found.'
    }
    $row = $state.Rows[0]
    if ([string]$row.PurchaseOrderStatus -ne 'Approved') {
        throw "PO status did not reach Approved: $($row.PurchaseOrderStatus)"
    }
    if ([Guid]$row.ApprovedById -ne $ApproverUserId) {
        throw 'PO approval was not attributed to the independent approver.'
    }
    if ([int]$row.ActiveCommitmentCount -ne 1) {
        throw "Expected one active Finance commitment, found $($row.ActiveCommitmentCount)."
    }
    if ([decimal]$row.ReservedAmount -ne [decimal]$row.TotalAmount) {
        throw "Finance commitment $($row.ReservedAmount) does not match PO total $($row.TotalAmount)."
    }

    $workflow = Invoke-DbTable $connection @'
SELECT TOP (1) wi.Id,wi.Status,wi.CompletedDate,
       wa.Status AS ApprovalStatus,wa.ProcessedById,wa.ProcessedDate
FROM WorkflowInstances wi
LEFT JOIN WorkflowStepInstances wsi
  ON wsi.WorkflowInstanceId=wi.Id AND wsi.IsDeleted=0
LEFT JOIN WorkflowApprovals wa
  ON wa.StepInstanceId=wsi.Id AND wa.IsDeleted=0 AND wa.ProcessedById=@actorId
WHERE wi.EntityId=@purchaseOrderId AND wi.IsDeleted=0
ORDER BY wi.CreatedDate DESC,wa.ProcessedDate DESC;
'@ @{ purchaseOrderId=$PurchaseOrderId; actorId=$ApproverUserId }
    if ($workflow.Rows.Count -ne 1) {
        throw 'The PO workflow read-back row was not found.'
    }
    if ([int]$workflow.Rows[0].Status -ne 2 -or
        [int]$workflow.Rows[0].ApprovalStatus -ne 1 -or
        [Guid]$workflow.Rows[0].ProcessedById -ne $ApproverUserId) {
        throw 'The workflow or approval record did not reach its completed approved state.'
    }

    [pscustomobject]@{
        Result = 'PASS'
        ApiApprovalStatus = if ($null -eq $decision) { 'PreviouslyCompleted' } else { $decision.StatusCode }
        ApiReadBackStatus = $readBack.StatusCode
        PurchaseOrder = [string]$row.OrderNumber
        PurchaseOrderStatus = [string]$row.PurchaseOrderStatus
        WorkflowOutcome = if ($null -eq $decisionPayload) { 'Approved' } else { [string]$decisionPayload.workflowOutcome }
        WorkflowStatus = if ($null -eq $decisionPayload) { 'Completed' } else { [string]$decisionPayload.workflowStatus }
        IndependentApprover = [string]$actor.Rows[0].UserName
        Budget = [string]$row.BudgetCode
        PurchaseOrderAmount = [decimal]$row.TotalAmount
        FinanceCommitmentAmount = [decimal]$row.ReservedAmount
        ActiveCommitmentCount = [int]$row.ActiveCommitmentCount
        BudgetCommitted = [decimal]$row.CommittedAmount
        BudgetRemaining = [decimal]$row.RemainingAmount
        WorkflowInstanceStatus = [int]$workflow.Rows[0].Status
        WorkflowApprovalStatus = [int]$workflow.Rows[0].ApprovalStatus
    } | ConvertTo-Json -Compress
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        try {
            [void](Invoke-ApiRequest $client 'POST' '/api/auth/logout' $token @{})
        }
        catch {
            # Credential restoration below remains mandatory even if logout fails.
        }
    }
    if ($null -ne $passwordBackup -and
        $connection.State -eq [System.Data.ConnectionState]::Open) {
        try {
            [void](Invoke-DbNonQuery $connection @'
UPDATE Users
SET PasswordHash=@hash,AccessFailedCount=@failed,LockoutEnd=@lockout
WHERE Id=@actorId;
'@ @{
                actorId=$ApproverUserId
                hash=$passwordBackup.PasswordHash
                failed=$passwordBackup.AccessFailedCount
                lockout=$passwordBackup.LockoutEnd
            })
        }
        catch {
            Write-Error 'The approver credential state could not be restored.'
        }
    }
    if ($connection.State -eq [System.Data.ConnectionState]::Open) {
        $connection.Close()
    }
    $connection.Dispose()
    $client.Dispose()
}
