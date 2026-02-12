-- SQL Script to add missing columns to Customers and Invoices tables
-- Run this script in SQL Server Management Studio or Azure Data Studio

PRINT 'Starting database schema fix...';
PRINT '';

-- ============================================
-- CUSTOMERS TABLE - Missing Columns
-- ============================================
PRINT '=== CUSTOMERS TABLE ===';

-- ContactPerson
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'ContactPerson')
BEGIN
    ALTER TABLE [Customers] ADD [ContactPerson] NVARCHAR(100) NULL;
    PRINT 'Added ContactPerson column';
END
ELSE
    PRINT 'ContactPerson column already exists';

-- CustomerType
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'CustomerType')
BEGIN
    ALTER TABLE [Customers] ADD [CustomerType] NVARCHAR(20) NOT NULL DEFAULT 'Individual';
    PRINT 'Added CustomerType column';
END
ELSE
    PRINT 'CustomerType column already exists';

-- EffectiveDate (from BusinessEntity base class)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'EffectiveDate')
BEGIN
    ALTER TABLE [Customers] ADD [EffectiveDate] DATETIME2 NULL;
    PRINT 'Added EffectiveDate column';
END
ELSE
    PRINT 'EffectiveDate column already exists';

-- ExpirationDate (from BusinessEntity base class)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'ExpirationDate')
BEGIN
    ALTER TABLE [Customers] ADD [ExpirationDate] DATETIME2 NULL;
    PRINT 'Added ExpirationDate column';
END
ELSE
    PRINT 'ExpirationDate column already exists';

-- IsActive
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'IsActive')
BEGIN
    ALTER TABLE [Customers] ADD [IsActive] BIT NOT NULL DEFAULT 1;
    PRINT 'Added IsActive column';
END
ELSE
    PRINT 'IsActive column already exists';

-- LastOrderDate
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'LastOrderDate')
BEGIN
    ALTER TABLE [Customers] ADD [LastOrderDate] DATETIME2 NULL;
    PRINT 'Added LastOrderDate column';
END
ELSE
    PRINT 'LastOrderDate column already exists';

-- Metadata (from BusinessEntity base class)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'Metadata')
BEGIN
    ALTER TABLE [Customers] ADD [Metadata] NVARCHAR(MAX) NULL;
    PRINT 'Added Metadata column';
END
ELSE
    PRINT 'Metadata column already exists';

-- Notes
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'Notes')
BEGIN
    ALTER TABLE [Customers] ADD [Notes] NVARCHAR(1000) NULL;
    PRINT 'Added Notes column';
END
ELSE
    PRINT 'Notes column already exists';

-- LastPaymentDate
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'LastPaymentDate')
BEGIN
    ALTER TABLE [Customers] ADD [LastPaymentDate] DATETIME2 NULL;
    PRINT 'Added LastPaymentDate column';
END
ELSE
    PRINT 'LastPaymentDate column already exists';

-- PostalCode
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'PostalCode')
BEGIN
    ALTER TABLE [Customers] ADD [PostalCode] NVARCHAR(20) NULL;
    PRINT 'Added PostalCode column';
END
ELSE
    PRINT 'PostalCode column already exists';

-- Priority
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'Priority')
BEGIN
    ALTER TABLE [Customers] ADD [Priority] INT NOT NULL DEFAULT 0;
    PRINT 'Added Priority column';
END
ELSE
    PRINT 'Priority column already exists';

-- ReferenceNumber
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'ReferenceNumber')
BEGIN
    ALTER TABLE [Customers] ADD [ReferenceNumber] NVARCHAR(100) NULL;
    PRINT 'Added ReferenceNumber column';
END
ELSE
    PRINT 'ReferenceNumber column already exists';

-- State
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'State')
BEGIN
    ALTER TABLE [Customers] ADD [State] NVARCHAR(50) NULL;
    PRINT 'Added State column';
END
ELSE
    PRINT 'State column already exists';

-- Status
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'Status')
BEGIN
    ALTER TABLE [Customers] ADD [Status] NVARCHAR(20) NOT NULL DEFAULT 'Active';
    PRINT 'Added Status column';
END
ELSE
    PRINT 'Status column already exists';

-- Tags
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'Tags')
BEGIN
    ALTER TABLE [Customers] ADD [Tags] NVARCHAR(500) NULL;
    PRINT 'Added Tags column';
END
ELSE
    PRINT 'Tags column already exists';

-- TaxId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'TaxId')
BEGIN
    ALTER TABLE [Customers] ADD [TaxId] NVARCHAR(50) NULL;
    PRINT 'Added TaxId column';
END
ELSE
    PRINT 'TaxId column already exists';

-- CurrencyCode
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'CurrencyCode')
BEGIN
    ALTER TABLE [Customers] ADD [CurrencyCode] NVARCHAR(3) NOT NULL DEFAULT 'GHS';
    PRINT 'Added CurrencyCode column';
END
ELSE
    PRINT 'CurrencyCode column already exists';

PRINT '';

-- ============================================
-- INVOICES TABLE - Missing Columns
-- ============================================
PRINT '=== INVOICES TABLE ===';

-- CustomerAddress
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'CustomerAddress')
BEGIN
    ALTER TABLE [Invoices] ADD [CustomerAddress] NVARCHAR(500) NULL;
    PRINT 'Added CustomerAddress column';
END
ELSE
    PRINT 'CustomerAddress column already exists';

-- DiscountAmount
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'DiscountAmount')
BEGIN
    ALTER TABLE [Invoices] ADD [DiscountAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added DiscountAmount column';
END
ELSE
    PRINT 'DiscountAmount column already exists';

-- Notes
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'Notes')
BEGIN
    ALTER TABLE [Invoices] ADD [Notes] NVARCHAR(500) NULL;
    PRINT 'Added Notes column';
END
ELSE
    PRINT 'Notes column already exists';

-- Reference
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'Reference')
BEGIN
    ALTER TABLE [Invoices] ADD [Reference] NVARCHAR(100) NULL;
    PRINT 'Added Reference column';
END
ELSE
    PRINT 'Reference column already exists';

-- SubTotal
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'SubTotal')
BEGIN
    ALTER TABLE [Invoices] ADD [SubTotal] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added SubTotal column';
END
ELSE
    PRINT 'SubTotal column already exists';

-- TaxAmount
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'TaxAmount')
BEGIN
    ALTER TABLE [Invoices] ADD [TaxAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added TaxAmount column';
END
ELSE
    PRINT 'TaxAmount column already exists';

-- BaseCurrencyAmount
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'BaseCurrencyAmount')
BEGIN
    ALTER TABLE [Invoices] ADD [BaseCurrencyAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added BaseCurrencyAmount column';
END
ELSE
    PRINT 'BaseCurrencyAmount column already exists';

-- PaymentTermsDays
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'PaymentTermsDays')
BEGIN
    ALTER TABLE [Invoices] ADD [PaymentTermsDays] INT NOT NULL DEFAULT 30;
    PRINT 'Added PaymentTermsDays column';
END
ELSE
    PRINT 'PaymentTermsDays column already exists';

PRINT '';
PRINT '=== Script completed successfully! ===';
PRINT '';
PRINT 'Please restart your API server after running this script.';
