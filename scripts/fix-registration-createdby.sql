-- Check existing registrations with NULL CreatedById
SELECT 
    Id,
    RegistrationNumber,
    ApplicantName,
    ApplicantEmail,
    Status,
    CreatedAt,
    CreatedById,
    TenantId
FROM BusinessPartnerRegistrations
WHERE IsDeleted = 0
ORDER BY CreatedAt DESC;

-- If you need to fix a specific registration, you can update it with the correct user ID
-- Replace @UserId with the actual user ID from the AspNetUsers table
-- 
-- DECLARE @UserId UNIQUEIDENTIFIER = 'YOUR-USER-ID-HERE';
-- 
-- UPDATE BusinessPartnerRegistrations
-- SET CreatedById = @UserId,
--     CreatedBy = (SELECT UserName FROM AspNetUsers WHERE Id = @UserId)
-- WHERE CreatedById IS NULL
--   AND IsDeleted = 0;

