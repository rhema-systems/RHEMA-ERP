$connectionString = "Server=LOCALHOST\SQL2017;Database=RhemaERP;User Id=sa;Password=sa;TrustServerCertificate=true;"
$userId = '0CFD30D8-C4E4-43BB-99E3-8A6C6E8175E7'
$tenderId = '75E33134-6794-42E5-BE3E-4AAF3F3F3537'
$bidId = '06A536EC-6DA5-48C1-88F3-C616FEC5D9EF'

$connection = New-Object System.Data.SqlClient.SqlConnection
$connection.ConnectionString = $connectionString
$connection.Open()

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "CHECKING TENANT IDS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Get User's TenantId
$query = "SELECT TOP 1 TenantId FROM UserTenants WHERE UserId = '$userId'"
$command = $connection.CreateCommand()
$command.CommandText = $query
$userTenantId = $command.ExecuteScalar()
Write-Host "User's TenantId: $userTenantId" -ForegroundColor Yellow

# Get BusinessPartner's TenantId
$query = @"
SELECT bp.TenantId
FROM BusinessPartnerUsers bpu
INNER JOIN BusinessPartners bp ON bpu.BusinessPartnerId = bp.Id
WHERE bpu.UserId = '$userId' AND bpu.IsDeleted = 0
"@
$command.CommandText = $query
$bpTenantId = $command.ExecuteScalar()
Write-Host "BusinessPartner's TenantId: $bpTenantId" -ForegroundColor Yellow

# Get TenderAssignment's TenantId
$query = "SELECT TOP 1 TenantId FROM TenderAssignments WHERE TenderId = '$tenderId' AND AssignedToUserId = '$userId' AND IsDeleted = 0"
$command.CommandText = $query
$taTenantId = $command.ExecuteScalar()
Write-Host "TenderAssignment's TenantId: $taTenantId" -ForegroundColor Yellow

# Get Bid's TenantId
$query = "SELECT TenantId FROM TenderBids WHERE Id = '$bidId'"
$command.CommandText = $query
$bidTenantId = $command.ExecuteScalar()
Write-Host "Bid's TenantId: $bidTenantId" -ForegroundColor Yellow

Write-Host ""
if ($userTenantId -eq $bpTenantId -and $bpTenantId -eq $taTenantId -and $taTenantId -eq $bidTenantId) {
    Write-Host "[OK] All TenantIds MATCH!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] TenantIds DO NOT MATCH!" -ForegroundColor Red
}

$connection.Close()

