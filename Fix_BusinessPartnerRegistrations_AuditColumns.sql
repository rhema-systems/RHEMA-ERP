-- Fix BusinessPartnerRegistrations table to add missing audit columns
-- Run this if the audit columns are missing from the table

USE RhemaERP;
GO

-- Add missing audit columns if they don't exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE();
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'CreatedBy')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD CreatedBy nvarchar(256) NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'CreatedById')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD CreatedById uniqueidentifier NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'UpdatedAt')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD UpdatedAt datetime2 NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'UpdatedBy')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD UpdatedBy nvarchar(256) NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'LastModifiedById')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD LastModifiedById uniqueidentifier NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'IsDeleted')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD IsDeleted bit NOT NULL DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'DeletedAt')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD DeletedAt datetime2 NULL;
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BusinessPartnerRegistrations' AND COLUMN_NAME = 'DeletedBy')
BEGIN
    ALTER TABLE BusinessPartnerRegistrations ADD DeletedBy nvarchar(256) NULL;
END

-- Verify the columns were added
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'BusinessPartnerRegistrations' 
AND COLUMN_NAME IN ('CreatedAt', 'CreatedBy', 'CreatedById', 'UpdatedAt', 'UpdatedBy', 'LastModifiedById', 'IsDeleted', 'DeletedAt', 'DeletedBy')
ORDER BY COLUMN_NAME;
GO