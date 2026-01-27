-- Add missing columns to ToolCheckouts table
USE rhemaerp;
GO

-- Check and add each column if it doesn't exist
IF COL_LENGTH('dbo.ToolCheckouts', 'CheckoutDate') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [CheckoutDate] [datetime2] NOT NULL DEFAULT GETUTCDATE();
    PRINT 'Added CheckoutDate column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'ExpectedReturnDate') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [ExpectedReturnDate] [datetime2] NULL;
    PRINT 'Added ExpectedReturnDate column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'ActualReturnDate') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [ActualReturnDate] [datetime2] NULL;
    PRINT 'Added ActualReturnDate column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'Status') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [Status] [nvarchar](20) NOT NULL DEFAULT 'CheckedOut';
    PRINT 'Added Status column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'CheckoutNotes') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [CheckoutNotes] [nvarchar](1000) NULL;
    PRINT 'Added CheckoutNotes column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'ReturnNotes') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [ReturnNotes] [nvarchar](1000) NULL;
    PRINT 'Added ReturnNotes column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'ConditionOnCheckout') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [ConditionOnCheckout] [nvarchar](20) NULL;
    PRINT 'Added ConditionOnCheckout column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'ConditionOnReturn') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [ConditionOnReturn] [nvarchar](20) NULL;
    PRINT 'Added ConditionOnReturn column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'DamageReported') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [DamageReported] [bit] NOT NULL DEFAULT CAST(0 AS bit);
    PRINT 'Added DamageReported column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'DamageDescription') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [DamageDescription] [nvarchar](2000) NULL;
    PRINT 'Added DamageDescription column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'DamageCost') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [DamageCost] [decimal](18, 2) NULL;
    PRINT 'Added DamageCost column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'CreatedById') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [CreatedById] [uniqueidentifier] NULL;
    PRINT 'Added CreatedById column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'LastModifiedById') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [LastModifiedById] [uniqueidentifier] NULL;
    PRINT 'Added LastModifiedById column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'DeletedAt') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [DeletedAt] [datetime2] NULL;
    PRINT 'Added DeletedAt column';
END

IF COL_LENGTH('dbo.ToolCheckouts', 'DeletedBy') IS NULL
BEGIN
    ALTER TABLE [ToolCheckouts] ADD [DeletedBy] [nvarchar](max) NULL;
    PRINT 'Added DeletedBy column';
END

-- Verify all columns exist
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ToolCheckouts'
ORDER BY COLUMN_NAME;
GO
