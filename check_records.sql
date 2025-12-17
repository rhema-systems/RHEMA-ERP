DECLARE @UserId UNIQUEIDENTIFIER = '0CFD30D8-C4E4-43BB-99E3-8A6C6E8175E7'
DECLARE @TenderId UNIQUEIDENTIFIER = '75E33134-6794-42E5-BE3E-4AAF3F3F3537'
DECLARE @BidId UNIQUEIDENTIFIER = '06A536EC-6DA5-48C1-88F3-C616FEC5D9EF'

PRINT '1. USER INFORMATION'
SELECT Id, FullName, Email, AuthenticationProvider
FROM Users
WHERE Id = @UserId

PRINT ''
PRINT '2. BUSINESS PARTNER (Main Owner)'
SELECT Id, PartnerCode, PartnerName, UserId
FROM BusinessPartners
WHERE UserId = @UserId AND IsDeleted = 0

PRINT ''
PRINT '3. BUSINESS PARTNER USER (Sub-User)'
SELECT bpu.Id, bpu.BusinessPartnerId, bp.PartnerName, bpu.UserId, bpu.Role, bpu.IsActive
FROM BusinessPartnerUsers bpu
INNER JOIN BusinessPartners bp ON bpu.BusinessPartnerId = bp.Id
WHERE bpu.UserId = @UserId AND bpu.IsDeleted = 0

PRINT ''
PRINT '4. TENDER ASSIGNMENTS FOR THIS TENDER'
SELECT ta.Id, ta.TenderId, ta.BusinessPartnerId, bp.PartnerName, ta.AssignedToUserId, ta.AssignmentType
FROM TenderAssignments ta
INNER JOIN BusinessPartners bp ON ta.BusinessPartnerId = bp.Id
WHERE ta.TenderId = @TenderId AND ta.IsDeleted = 0

PRINT ''
PRINT '5. BID INFORMATION'
SELECT b.Id, b.BidNumber, b.TenderId, b.BusinessPartnerId, bp.PartnerName, b.Status
FROM TenderBids b
INNER JOIN BusinessPartners bp ON b.BusinessPartnerId = bp.Id
WHERE b.Id = @BidId

PRINT ''
PRINT '6. ALL BIDS FOR THIS TENDER'
SELECT b.Id, b.BidNumber, b.BusinessPartnerId, bp.PartnerName, b.Status
FROM TenderBids b
INNER JOIN BusinessPartners bp ON b.BusinessPartnerId = bp.Id
WHERE b.TenderId = @TenderId AND b.IsDeleted = 0

