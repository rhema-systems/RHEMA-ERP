$connectionString = "Server=LOCALHOST\SQL2017;Database=RhemaERP;User Id=sa;Password=sa;TrustServerCertificate=true;"
$userId = '0CFD30D8-C4E4-43BB-99E3-8A6C6E8175E7'
$tenderId = '75E33134-6794-42E5-BE3E-4AAF3F3F3537'
$bidId = '06A536EC-6DA5-48C1-88F3-C616FEC5D9EF'

$connection = New-Object System.Data.SqlClient.SqlConnection
$connection.ConnectionString = $connectionString
$connection.Open()

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "1. USER INFORMATION" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
$query = "SELECT Id, FullName, Email, AuthenticationProvider FROM Users WHERE Id = '$userId'"
$command = $connection.CreateCommand()
$command.CommandText = $query
$reader = $command.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Id: $($reader['Id'])"
    Write-Host "FullName: $($reader['FullName'])"
    Write-Host "Email: $($reader['Email'])"
    Write-Host "AuthenticationProvider: $($reader['AuthenticationProvider'])"
}
$reader.Close()

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "2. BUSINESS PARTNER (Main Owner)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
$query = "SELECT Id, PartnerCode, PartnerName, UserId FROM BusinessPartners WHERE UserId = '$userId' AND IsDeleted = 0"
$command.CommandText = $query
$reader = $command.ExecuteReader()
$hasMainOwner = $false
while ($reader.Read()) {
    $hasMainOwner = $true
    Write-Host "Id: $($reader['Id'])"
    Write-Host "PartnerCode: $($reader['PartnerCode'])"
    Write-Host "PartnerName: $($reader['PartnerName'])"
    Write-Host "UserId: $($reader['UserId'])"
}
if (-not $hasMainOwner) {
    Write-Host "NO MAIN OWNER RECORD FOUND" -ForegroundColor Yellow
}
$reader.Close()

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "3. BUSINESS PARTNER USER (Sub-User)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
$query = @"
SELECT bpu.Id, bpu.BusinessPartnerId, bp.PartnerName, bpu.UserId, bpu.Role, bpu.IsActive
FROM BusinessPartnerUsers bpu
INNER JOIN BusinessPartners bp ON bpu.BusinessPartnerId = bp.Id
WHERE bpu.UserId = '$userId' AND bpu.IsDeleted = 0
"@
$command.CommandText = $query
$reader = $command.ExecuteReader()
$hasSubUser = $false
$businessPartnerId = $null
while ($reader.Read()) {
    $hasSubUser = $true
    $businessPartnerId = $reader['BusinessPartnerId']
    Write-Host "Id: $($reader['Id'])"
    Write-Host "BusinessPartnerId: $($reader['BusinessPartnerId'])"
    Write-Host "PartnerName: $($reader['PartnerName'])"
    Write-Host "UserId: $($reader['UserId'])"
    Write-Host "Role: $($reader['Role'])"
    Write-Host "IsActive: $($reader['IsActive'])"
}
if (-not $hasSubUser) {
    Write-Host "NO SUB-USER RECORD FOUND" -ForegroundColor Yellow
}
$reader.Close()

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "4. TENDER ASSIGNMENTS FOR THIS TENDER" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
$query = @"
SELECT ta.Id, ta.TenderId, ta.BusinessPartnerId, bp.PartnerName, ta.AssignedToUserId, ta.AssignmentType
FROM TenderAssignments ta
INNER JOIN BusinessPartners bp ON ta.BusinessPartnerId = bp.Id
WHERE ta.TenderId = '$tenderId' AND ta.IsDeleted = 0
"@
$command.CommandText = $query
$reader = $command.ExecuteReader()
$hasAssignments = $false
while ($reader.Read()) {
    $hasAssignments = $true
    Write-Host "Id: $($reader['Id'])"
    Write-Host "TenderId: $($reader['TenderId'])"
    Write-Host "BusinessPartnerId: $($reader['BusinessPartnerId'])"
    Write-Host "PartnerName: $($reader['PartnerName'])"
    Write-Host "AssignedToUserId: $($reader['AssignedToUserId'])"
    Write-Host "AssignmentType: $($reader['AssignmentType'])"
    Write-Host "---"
}
if (-not $hasAssignments) {
    Write-Host "NO TENDER ASSIGNMENTS FOUND" -ForegroundColor Yellow
}
$reader.Close()

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "5. BID INFORMATION" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
$query = @"
SELECT b.Id, b.BidNumber, b.TenderId, b.BusinessPartnerId, bp.PartnerName, b.Status
FROM TenderBids b
INNER JOIN BusinessPartners bp ON b.BusinessPartnerId = bp.Id
WHERE b.Id = '$bidId'
"@
$command.CommandText = $query
$reader = $command.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Id: $($reader['Id'])"
    Write-Host "BidNumber: $($reader['BidNumber'])"
    Write-Host "TenderId: $($reader['TenderId'])"
    Write-Host "BusinessPartnerId: $($reader['BusinessPartnerId'])"
    Write-Host "PartnerName: $($reader['PartnerName'])"
    Write-Host "Status: $($reader['Status'])"
}
$reader.Close()

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "6. DIAGNOSIS" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if ($hasSubUser -and $businessPartnerId) {
    Write-Host "User is a SUB-USER linked to BusinessPartnerId: $businessPartnerId" -ForegroundColor Green
    
    # Check if there's an assignment for this business partner
    $query = @"
    SELECT COUNT(*) as Count
    FROM TenderAssignments
    WHERE TenderId = '$tenderId' 
      AND BusinessPartnerId = '$businessPartnerId'
      AND IsDeleted = 0
      AND (AssignmentType = 'AllUsers' OR AssignedToUserId = '$userId')
"@
    $command.CommandText = $query
    $count = [int]$command.ExecuteScalar()
    
    if ($count -gt 0) {
        Write-Host "[OK] User HAS assignment to this tender" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] User DOES NOT have assignment to this tender" -ForegroundColor Red
        Write-Host "  The TenderAssignment record might be for a different BusinessPartnerId" -ForegroundColor Yellow
    }
} elseif ($hasMainOwner) {
    Write-Host "User is a MAIN OWNER" -ForegroundColor Green
} else {
    Write-Host "User is NOT linked to any Business Partner" -ForegroundColor Red
}

$connection.Close()

