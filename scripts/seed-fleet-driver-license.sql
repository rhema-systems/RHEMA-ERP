SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @EmployeeId uniqueidentifier;
DECLARE @IdentificationTypeId uniqueidentifier;
DECLARE @Today date = CONVERT(date, SYSUTCDATETIME());
DECLARE @ExpiryDate date = DATEADD(year, 5, @Today);

SELECT TOP (1)
    @EmployeeId = e.Id
FROM Employees e
INNER JOIN EmployeePositions p
    ON p.Id = e.PositionId
    AND p.TenantId = e.TenantId
    AND p.IsDeleted = 0
WHERE e.TenantId = @TenantId
  AND e.IsDeleted = 0
  AND e.IsActive = 1
  AND LOWER(p.Title) LIKE '%driver%'
ORDER BY e.EmployeeNumber, e.Id;

IF @EmployeeId IS NULL
BEGIN
    THROW 50001, 'No active employee in a Driver position was found for the default tenant.', 1;
END;

BEGIN TRANSACTION;

SELECT TOP (1)
    @IdentificationTypeId = Id
FROM IdentificationTypes
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (LOWER(Name) LIKE '%driver%' OR Code = 'DRIVERS_LICENSE')
ORDER BY CASE WHEN Code = 'DRIVERS_LICENSE' THEN 0 ELSE 1 END, CreatedAt;

IF @IdentificationTypeId IS NULL
BEGIN
    SET @IdentificationTypeId = NEWID();

    INSERT INTO IdentificationTypes
    (
        Id,
        Name,
        Code,
        Description,
        IssuingAuthorityName,
        HasExpiryDate,
        IsActive,
        CreatedAt,
        CreatedBy,
        IsDeleted,
        TenantId
    )
    VALUES
    (
        @IdentificationTypeId,
        'Driver''s License',
        'DRIVERS_LICENSE',
        'Driver licence used for Fleet trip assignment and dispatch validation.',
        'Driver and Vehicle Licensing Authority',
        1,
        1,
        SYSUTCDATETIME(),
        'System Seed',
        0,
        @TenantId
    );
END;

IF EXISTS
(
    SELECT 1
    FROM EmployeeIdentificationCards
    WHERE TenantId = @TenantId
      AND EmployeeId = @EmployeeId
      AND IsDeleted = 0
      AND LOWER(DocumentType) LIKE '%driver%'
)
BEGIN
    UPDATE EmployeeIdentificationCards
    SET IdentificationTypeId = @IdentificationTypeId,
        DocumentType = 'Driver''s License',
        IssueDate = @Today,
        ExpiryDate = @ExpiryDate,
        IssuingAuthority = 'Driver and Vehicle Licensing Authority',
        IsVerified = 1,
        VerifiedDate = SYSUTCDATETIME(),
        Notes = 'Seeded for Fleet trip driver-license dispatch enforcement.',
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = 'System Seed'
    WHERE TenantId = @TenantId
      AND EmployeeId = @EmployeeId
      AND IsDeleted = 0
      AND LOWER(DocumentType) LIKE '%driver%';
END;
ELSE
BEGIN
    INSERT INTO EmployeeIdentificationCards
    (
        Id,
        EmployeeId,
        IdentificationTypeId,
        DocumentType,
        DocumentNumber,
        IssueDate,
        ExpiryDate,
        IssuingAuthority,
        IsVerified,
        VerifiedDate,
        Notes,
        CreatedAt,
        CreatedBy,
        IsDeleted,
        TenantId
    )
    SELECT
        NEWID(),
        e.Id,
        @IdentificationTypeId,
        'Driver''s License',
        CONCAT('DRV-', e.EmployeeNumber, '-', YEAR(@Today)),
        @Today,
        @ExpiryDate,
        'Driver and Vehicle Licensing Authority',
        1,
        SYSUTCDATETIME(),
        'Seeded for Fleet trip driver-license dispatch enforcement.',
        SYSUTCDATETIME(),
        'System Seed',
        0,
        @TenantId
    FROM Employees e
    WHERE e.Id = @EmployeeId
      AND e.TenantId = @TenantId;
END;

COMMIT TRANSACTION;

SELECT
    e.EmployeeNumber,
    CONCAT(e.FirstName, ' ', e.LastName) AS EmployeeName,
    c.DocumentNumber,
    CONVERT(varchar(10), c.IssueDate, 23) AS IssueDate,
    CONVERT(varchar(10), c.ExpiryDate, 23) AS ExpiryDate,
    c.IsVerified
FROM Employees e
INNER JOIN EmployeeIdentificationCards c
    ON c.EmployeeId = e.Id
    AND c.TenantId = e.TenantId
    AND c.IsDeleted = 0
    AND LOWER(c.DocumentType) LIKE '%driver%'
WHERE e.Id = @EmployeeId
  AND e.TenantId = @TenantId;
