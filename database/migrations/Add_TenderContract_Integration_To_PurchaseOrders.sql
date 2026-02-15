-- Migration: Add Tender/Contract Integration to Purchase Orders
-- Description: Adds fields to link purchase orders to tender awards and contracts
-- Date: 2026-01-26

-- Add new columns to PurchaseOrders table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseOrders]') AND name = 'TenderAwardId')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrders]
    ADD [TenderAwardId] UNIQUEIDENTIFIER NULL;
    
    PRINT 'Added TenderAwardId column to PurchaseOrders table';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseOrders]') AND name = 'ContractId')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrders]
    ADD [ContractId] UNIQUEIDENTIFIER NULL;
    
    PRINT 'Added ContractId column to PurchaseOrders table';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseOrders]') AND name = 'TenderNumber')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrders]
    ADD [TenderNumber] NVARCHAR(50) NULL;
    
    PRINT 'Added TenderNumber column to PurchaseOrders table';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseOrders]') AND name = 'ContractNumber')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrders]
    ADD [ContractNumber] NVARCHAR(50) NULL;
    
    PRINT 'Added ContractNumber column to PurchaseOrders table';
END

-- Add foreign key constraint for TenderAwardId
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PurchaseOrders_TenderAwards_TenderAwardId')
BEGIN
    ALTER TABLE [dbo].[PurchaseOrders]
    ADD CONSTRAINT [FK_PurchaseOrders_TenderAwards_TenderAwardId]
    FOREIGN KEY ([TenderAwardId]) REFERENCES [dbo].[TenderAwards]([Id])
    ON DELETE NO ACTION;
    
    PRINT 'Added foreign key constraint FK_PurchaseOrders_TenderAwards_TenderAwardId';
END

-- Add index for TenderAwardId for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseOrders_TenderAwardId' AND object_id = OBJECT_ID(N'[dbo].[PurchaseOrders]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseOrders_TenderAwardId]
    ON [dbo].[PurchaseOrders] ([TenderAwardId])
    WHERE [TenderAwardId] IS NOT NULL;
    
    PRINT 'Created index IX_PurchaseOrders_TenderAwardId';
END

-- Add index for ContractId for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PurchaseOrders_ContractId' AND object_id = OBJECT_ID(N'[dbo].[PurchaseOrders]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PurchaseOrders_ContractId]
    ON [dbo].[PurchaseOrders] ([ContractId])
    WHERE [ContractId] IS NOT NULL;
    
    PRINT 'Created index IX_PurchaseOrders_ContractId';
END

PRINT 'Migration completed successfully: Add_TenderContract_Integration_To_PurchaseOrders';
GO
