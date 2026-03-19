-- Populate Department Lookup Values for Account Creation Testing
-- This script adds sample department codes to the SegmentLookupValues table

SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET ANSI_NULLS ON;
SET CONCAT_NULL_YIELDS_NULL ON;

-- First, get the SegmentStructureId for the Department segment
DECLARE @SegmentStructureId UNIQUEIDENTIFIER;
DECLARE @TenantId UNIQUEIDENTIFIER;

-- Get the DEFAULT tenant ID
SELECT @TenantId = Id FROM Tenants WHERE Code = 'DEFAULT';

-- Get the Department segment structure ID
-- Assuming the segment is named 'Department' or similar
SELECT TOP 1 @SegmentStructureId = Id 
FROM AccountSegmentStructures 
WHERE TenantId = @TenantId 
  AND (SegmentName LIKE '%Department%' OR SegmentName LIKE '%Dept%')
ORDER BY SegmentNumber;

-- If no Department segment found, try to get the first segment
IF @SegmentStructureId IS NULL
BEGIN
    SELECT TOP 1 @SegmentStructureId = Id 
    FROM AccountSegmentStructures 
    WHERE TenantId = @TenantId 
    ORDER BY SegmentNumber;
END

-- Display what we found
SELECT 
    @SegmentStructureId AS SegmentStructureId,
    @TenantId AS TenantId,
    SegmentName,
    SegmentLength,
    RequiresLookup
FROM AccountSegmentStructures
WHERE Id = @SegmentStructureId;

-- Insert sample department lookup values (3-character codes to match typical segment length)
-- Only insert if we found a valid segment structure
IF @SegmentStructureId IS NOT NULL
BEGIN
    -- Delete existing lookup values for this segment (for clean re-run)
    DELETE FROM SegmentLookupValues WHERE SegmentStructureId = @SegmentStructureId;

    -- Insert department codes
    INSERT INTO SegmentLookupValues (
        Id,
        SegmentStructureId,
        SegmentValue,
        Description,
        EffectiveDate,
        IsActive,
        DisplayOrder,
        TenantId,
        CreatedAt,
        CreatedBy
    )
    VALUES
    -- 3-digit numeric codes (100-999 range for testing)
    (NEWID(), @SegmentStructureId, '100', 'General Administration', GETUTCDATE(), 1, 1, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '200', 'Finance Department', GETUTCDATE(), 1, 2, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '300', 'Human Resources', GETUTCDATE(), 1, 3, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '400', 'Information Technology', GETUTCDATE(), 1, 4, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '500', 'Sales Department', GETUTCDATE(), 1, 5, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '600', 'Marketing Department', GETUTCDATE(), 1, 6, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '700', 'Operations', GETUTCDATE(), 1, 7, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '800', 'Customer Service', GETUTCDATE(), 1, 8, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001'),
    (NEWID(), @SegmentStructureId, '900', 'Research & Development', GETUTCDATE(), 1, 9, @TenantId, GETUTCDATE(), '00000000-0000-0000-0000-000000000001');

    -- Display inserted values
    SELECT 
        SegmentValue,
        Description,
        IsActive,
        DisplayOrder
    FROM SegmentLookupValues
    WHERE SegmentStructureId = @SegmentStructureId
    ORDER BY DisplayOrder;

    PRINT 'Successfully inserted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' department lookup values';
END
ELSE
BEGIN
    PRINT 'ERROR: Could not find segment structure for Department';
END
