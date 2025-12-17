-- Check TenderAssignment records for the user
DECLARE @UserId UNIQUEIDENTIFIER = '0CFD30D8-C4E4-43BB-99E3-8A6C6E8175E7'
DECLARE @TenderId UNIQUEIDENTIFIER = '75E33134-6794-42E5-BE3E-4AAF3F3F3537'
DECLARE @BidId UNIQUEIDENTIFIER = '06A536EC-6DA5-48C1-88F3-C616FEC5D9EF'

PRINT '========================================='
PRINT 'CHECKING USER INFORMATION'
PRINT '========================================='
SELECT 
    Id,
    FullName,
    Email,
    AuthenticationProvider,
    EmployeeId
FROM AspNetUsers
WHERE Id = @UserId

PRINT ''
PRINT '========================================='
PRINT 'CHECKING BUSINESS PARTNER (Main Account Owner)'
PRINT '========================================='
SELECT
    Id,
    PartnerCode,
    PartnerName,
    UserId,
    ApprovalStatus,
    IsDeleted
FROM BusinessPartners
WHERE UserId = @UserId AND IsDeleted = 0

PRINT ''
PRINT '========================================='
PRINT 'CHECKING BUSINESS PARTNER USER (Sub-User Link)'
PRINT '========================================='
SELECT 
    bpu.Id,
    bpu.BusinessPartnerId,
    bp.PartnerName,
    bpu.UserId,
    bpu.Role,
    bpu.IsActive,
    bpu.IsDeleted
FROM BusinessPartnerUsers bpu
INNER JOIN BusinessPartners bp ON bpu.BusinessPartnerId = bp.Id
WHERE bpu.UserId = @UserId AND bpu.IsDeleted = 0

PRINT ''
PRINT '========================================='
PRINT 'CHECKING TENDER ASSIGNMENT FOR THIS USER'
PRINT '========================================='
SELECT 
    ta.Id,
    ta.TenderId,
    t.TenderNumber,
    t.Title AS TenderTitle,
    ta.BusinessPartnerId,
    bp.PartnerName,
    ta.AssignedToUserId,
    u.FullName AS AssignedToUserName,
    ta.AssignmentType,
    ta.AssignedAt,
    ta.IsDeleted
FROM TenderAssignments ta
INNER JOIN Tenders t ON ta.TenderId = t.Id
INNER JOIN BusinessPartners bp ON ta.BusinessPartnerId = bp.Id
LEFT JOIN AspNetUsers u ON ta.AssignedToUserId = u.Id
WHERE ta.TenderId = @TenderId 
  AND ta.IsDeleted = 0
  AND (ta.AssignedToUserId = @UserId OR ta.AssignmentType = 'AllUsers')

PRINT ''
PRINT '========================================='
PRINT 'CHECKING ALL TENDER ASSIGNMENTS FOR THIS TENDER'
PRINT '========================================='
SELECT 
    ta.Id,
    ta.TenderId,
    ta.BusinessPartnerId,
    bp.PartnerName,
    ta.AssignedToUserId,
    u.FullName AS AssignedToUserName,
    ta.AssignmentType,
    ta.IsDeleted
FROM TenderAssignments ta
INNER JOIN BusinessPartners bp ON ta.BusinessPartnerId = bp.Id
LEFT JOIN AspNetUsers u ON ta.AssignedToUserId = u.Id
WHERE ta.TenderId = @TenderId AND ta.IsDeleted = 0

PRINT ''
PRINT '========================================='
PRINT 'CHECKING BID INFORMATION'
PRINT '========================================='
SELECT 
    b.Id,
    b.BidNumber,
    b.TenderId,
    t.TenderNumber,
    b.BusinessPartnerId,
    bp.PartnerName,
    b.Status,
    b.SubmittedDate,
    b.TotalBidAmount,
    b.IsDeleted
FROM TenderBids b
INNER JOIN Tenders t ON b.TenderId = t.Id
INNER JOIN BusinessPartners bp ON b.BusinessPartnerId = bp.Id
WHERE b.Id = @BidId

PRINT ''
PRINT '========================================='
PRINT 'CHECKING ALL BIDS FOR THIS TENDER'
PRINT '========================================='
SELECT 
    b.Id,
    b.BidNumber,
    b.BusinessPartnerId,
    bp.PartnerName,
    b.Status,
    b.SubmittedDate,
    b.IsDeleted
FROM TenderBids b
INNER JOIN BusinessPartners bp ON b.BusinessPartnerId = bp.Id
WHERE b.TenderId = @TenderId AND b.IsDeleted = 0

