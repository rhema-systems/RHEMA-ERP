using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

internal static class InventoryDisposalAccountingGuards
{
    public static void Install(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CaseGuard);
        migrationBuilder.Sql(LinkGuard);
        migrationBuilder.Sql(InvoiceGuard);
        migrationBuilder.Sql(InvoiceLineGuard);
    }

    public static void Remove(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.InventoryDisposalCases WHERE AccountingVersion=1 OR PreparedStockAdjustmentId IS NOT NULL) THROW 51960, 'INV_DISPOSAL_DOWNGRADE_HAS_DATA', 1;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_InventoryDisposalAuctionInvoices_Guard; DROP TRIGGER IF EXISTS dbo.TR_Invoices_DisposalEconomics; DROP TRIGGER IF EXISTS dbo.TR_InvoiceLineItem_DisposalEconomics;");
        migrationBuilder.Sql(LegacyCaseGuard);
    }

    public const string CaseGuard = """
        CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalCases_Guard]
        ON [dbo].[InventoryDisposalCases]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51109, 'INV_DISPOSAL_CASE_DELETE_PROHIBITED', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN dbo.Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId
                LEFT JOIN dbo.Users u ON u.Id = i.RequestedById AND u.TenantId = i.TenantId
                LEFT JOIN dbo.StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
                WHERE w.Id IS NULL OR u.Id IS NULL OR (i.StockAdjustmentId IS NOT NULL AND a.Id IS NULL))
                THROW 51110, 'INV_DISPOSAL_CASE_TENANT_LINEAGE_INVALID', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN deleted d ON d.Id = i.Id
                WHERE d.Id IS NULL AND (i.Status <> 1 OR i.ApprovalRequired <> 1 OR i.AuditVerifiedById IS NOT NULL OR i.WorkflowInstanceId IS NOT NULL
                   OR i.ApprovedById IS NOT NULL OR i.StockAdjustmentId IS NOT NULL OR i.PreparedStockAdjustmentId IS NOT NULL OR i.AccountingVersion <> 1 OR i.Method = 2 OR i.CompletedById IS NOT NULL))
                THROW 51111, 'INV_DISPOSAL_CASE_INITIAL_STATE_INVALID', 1;
            -- Only an identified draft may change its editable identification fields.
            -- Tenant, identity, warehouse, numbering, lineage and soft-delete fields
            -- remain immutable for the lifetime of the case.
            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.TenantId <> d.TenantId OR i.DisposalNumber <> d.DisposalNumber OR i.WarehouseId <> d.WarehouseId
                   OR i.RequestedById <> d.RequestedById OR i.RequestedAtUtc <> d.RequestedAtUtc
                   OR i.IdempotencyKey <> d.IdempotencyKey OR i.PayloadHash <> d.PayloadHash OR i.CorrelationId <> d.CorrelationId
                   OR i.IsDeleted <> d.IsDeleted OR i.AccountingVersion <> d.AccountingVersion
                   OR (d.PreparedStockAdjustmentId IS NOT NULL AND (i.PreparedStockAdjustmentId IS NULL OR i.PreparedStockAdjustmentId <> d.PreparedStockAdjustmentId))
                   OR (i.StockAdjustmentId IS NOT NULL AND i.PreparedStockAdjustmentId IS NOT NULL AND i.StockAdjustmentId <> i.PreparedStockAdjustmentId)
                   OR (d.StockAdjustmentId IS NOT NULL AND (i.StockAdjustmentId IS NULL OR i.StockAdjustmentId <> d.StockAdjustmentId))
                   OR (i.Method = 2 AND d.Method <> 2)
                   OR ((d.Status <> 1 OR i.Status <> 1) AND (
                        i.Method <> d.Method OR i.Reason <> d.Reason OR i.IdentificationDetails <> d.IdentificationDetails
                        OR i.TotalQuantity <> d.TotalQuantity OR i.TotalValue <> d.TotalValue))
                   OR (i.ApprovalRequired <> d.ApprovalRequired AND NOT (
                        d.Status IN (1, 2, 3, 4) AND i.Status = 11
                        AND d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                        AND d.WorkflowInstanceId IS NULL AND d.ApprovedById IS NULL
                        AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId, N'InventoryDisposal', i.Id) = 0)))
                THROW 51112, 'INV_DISPOSAL_CASE_CORE_IMMUTABLE', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.Status <> d.Status AND NOT (
                       (d.Status = 1 AND i.Status IN (2, 5, 9, 10, 11))
                    OR (d.Status = 2 AND i.Status IN (3, 5, 10, 11))
                    OR (d.Status = 3 AND i.Status IN (4, 5, 9, 10, 11))
                    OR (d.Status = 4 AND i.Status IN (5, 10, 11))
                    OR (d.Status = 5 AND i.Status IN (6, 9, 10))
                    OR (d.Status IN (6, 11) AND i.Status IN (7, 10))
                    OR (d.Status = 7 AND i.Status = 8)))
                THROW 51113, 'INV_DISPOSAL_CASE_TRANSITION_INVALID', 1;
            IF EXISTS (SELECT 1 FROM inserted WHERE Status = 5 AND (WorkflowInstanceId IS NULL OR ApprovalRequired <> 1))
                THROW 51114, 'INV_DISPOSAL_WORKFLOW_LINEAGE_REQUIRED', 1;
            IF EXISTS (SELECT 1 FROM inserted WHERE
                (ApprovalRequired = 1 AND Status IN (6, 7, 8) AND (WorkflowInstanceId IS NULL OR ApprovedById IS NULL OR ApprovedAtUtc IS NULL))
                OR (Status = 11 AND ApprovalRequired <> 0)
                OR (ApprovalRequired = 0 AND (Status NOT IN (7, 8, 10, 11) OR WorkflowInstanceId IS NOT NULL OR ApprovedById IS NOT NULL OR ApprovedAtUtc IS NOT NULL)))
                THROW 51115, 'INV_DISPOSAL_APPROVAL_LINEAGE_REQUIRED', 1;
            IF EXISTS (SELECT 1 FROM inserted WHERE (Status = 7 AND (COALESCE(StockAdjustmentId, PreparedStockAdjustmentId) IS NULL OR ExecutionReference IS NULL))
                OR (Status = 8 AND (StockAdjustmentId IS NULL OR ExecutionReference IS NULL)))
                THROW 51116, 'INV_DISPOSAL_ADJUSTMENT_LINEAGE_REQUIRED', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i LEFT JOIN dbo.StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
                WHERE i.Status = 8 AND (
                    i.CompletedById IS NULL OR i.CompletedAtUtc IS NULL OR a.Status <> 'Posted'
                    OR ((i.Method = 2 OR (i.Method = 1 AND i.AccountingVersion = 0)) AND (i.ProceedsAmount <= 0 OR i.ProceedsAccountId IS NULL OR i.BuyerOrRecipient IS NULL
                        OR i.ProceedsPostingEventId IS NULL OR i.ProceedsJournalEntryId IS NULL))
                    OR (i.Method NOT IN (1, 2) AND i.ProceedsAmount <> 0)
                    OR (i.Method = 1 AND i.AccountingVersion = 1 AND NOT EXISTS (
                        SELECT 1 FROM dbo.InventoryDisposalAuctionInvoices l JOIN dbo.Invoices v ON v.Id=l.InvoiceId AND v.TenantId=l.TenantId
                        WHERE l.InventoryDisposalCaseId=i.Id AND l.TenantId=i.TenantId AND l.IsDeleted=0 AND v.IsDeleted=0 AND v.Status<>6))))
                THROW 51117, 'INV_DISPOSAL_COMPLETION_LINEAGE_INVALID', 1;
            IF EXISTS(SELECT 1 FROM inserted i
                WHERE i.AccountingVersion=1 AND i.Method=1 AND (
                    (i.Status=7 AND NOT EXISTS(SELECT 1 FROM dbo.InventoryDisposalAuctionInvoices l
                        JOIN dbo.Invoices v ON v.Id=l.InvoiceId AND v.TenantId=l.TenantId
                        WHERE l.InventoryDisposalCaseId=i.Id AND l.TenantId=i.TenantId AND l.IsDeleted=0 AND v.IsDeleted=0 AND v.Status<>6))
                    OR (i.Status=10 AND EXISTS(SELECT 1 FROM dbo.InventoryDisposalAuctionInvoices l
                        JOIN dbo.Invoices v ON v.Id=l.InvoiceId WHERE l.InventoryDisposalCaseId=i.Id AND v.Status<>6))))
                THROW 51969, 'INV_DISPOSAL_AUCTION_INVOICE_STATE_INVALID', 1;
        END;
        """;

    public const string LegacyCaseGuard = """
        CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalCases_Guard]
        ON [dbo].[InventoryDisposalCases]
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                THROW 51109, 'INV_DISPOSAL_CASE_DELETE_PROHIBITED', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN dbo.Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId
                LEFT JOIN dbo.Users u ON u.Id = i.RequestedById AND u.TenantId = i.TenantId
                LEFT JOIN dbo.StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
                WHERE w.Id IS NULL OR u.Id IS NULL OR (i.StockAdjustmentId IS NOT NULL AND a.Id IS NULL))
                THROW 51110, 'INV_DISPOSAL_CASE_TENANT_LINEAGE_INVALID', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN deleted d ON d.Id = i.Id
                WHERE d.Id IS NULL AND (i.Status <> 1 OR i.ApprovalRequired <> 1 OR i.AuditVerifiedById IS NOT NULL OR i.WorkflowInstanceId IS NOT NULL
                   OR i.ApprovedById IS NOT NULL OR i.StockAdjustmentId IS NOT NULL OR i.CompletedById IS NOT NULL))
                THROW 51111, 'INV_DISPOSAL_CASE_INITIAL_STATE_INVALID', 1;
            -- Only an identified draft may change its editable identification fields.
            -- Tenant, identity, warehouse, numbering, lineage and soft-delete fields
            -- remain immutable for the lifetime of the case.
            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.TenantId <> d.TenantId OR i.DisposalNumber <> d.DisposalNumber OR i.WarehouseId <> d.WarehouseId
                   OR i.RequestedById <> d.RequestedById OR i.RequestedAtUtc <> d.RequestedAtUtc
                   OR i.IdempotencyKey <> d.IdempotencyKey OR i.PayloadHash <> d.PayloadHash OR i.CorrelationId <> d.CorrelationId
                   OR i.IsDeleted <> d.IsDeleted
                   OR ((d.Status <> 1 OR i.Status <> 1) AND (
                        i.Method <> d.Method OR i.Reason <> d.Reason OR i.IdentificationDetails <> d.IdentificationDetails
                        OR i.TotalQuantity <> d.TotalQuantity OR i.TotalValue <> d.TotalValue))
                   OR (i.ApprovalRequired <> d.ApprovalRequired AND NOT (
                        d.Status IN (1, 2, 3, 4) AND i.Status = 11
                        AND d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                        AND d.WorkflowInstanceId IS NULL AND d.ApprovedById IS NULL
                        AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId, N'InventoryDisposal', i.Id) = 0)))
                THROW 51112, 'INV_DISPOSAL_CASE_CORE_IMMUTABLE', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE i.Status <> d.Status AND NOT (
                       (d.Status = 1 AND i.Status IN (2, 5, 9, 10, 11))
                    OR (d.Status = 2 AND i.Status IN (3, 5, 10, 11))
                    OR (d.Status = 3 AND i.Status IN (4, 5, 9, 10, 11))
                    OR (d.Status = 4 AND i.Status IN (5, 10, 11))
                    OR (d.Status = 5 AND i.Status IN (6, 9, 10))
                    OR (d.Status IN (6, 11) AND i.Status IN (7, 10))
                    OR (d.Status = 7 AND i.Status = 8)))
                THROW 51113, 'INV_DISPOSAL_CASE_TRANSITION_INVALID', 1;
            IF EXISTS (SELECT 1 FROM inserted WHERE Status = 5 AND (WorkflowInstanceId IS NULL OR ApprovalRequired <> 1))
                THROW 51114, 'INV_DISPOSAL_WORKFLOW_LINEAGE_REQUIRED', 1;
            IF EXISTS (SELECT 1 FROM inserted WHERE
                (ApprovalRequired = 1 AND Status IN (6, 7, 8) AND (WorkflowInstanceId IS NULL OR ApprovedById IS NULL OR ApprovedAtUtc IS NULL))
                OR (Status = 11 AND ApprovalRequired <> 0)
                OR (ApprovalRequired = 0 AND (Status NOT IN (7, 8, 10, 11) OR WorkflowInstanceId IS NOT NULL OR ApprovedById IS NOT NULL OR ApprovedAtUtc IS NOT NULL)))
                THROW 51115, 'INV_DISPOSAL_APPROVAL_LINEAGE_REQUIRED', 1;
            IF EXISTS (SELECT 1 FROM inserted WHERE Status IN (7, 8) AND (StockAdjustmentId IS NULL OR ExecutionReference IS NULL))
                THROW 51116, 'INV_DISPOSAL_ADJUSTMENT_LINEAGE_REQUIRED', 1;
            IF EXISTS (
                SELECT 1 FROM inserted i LEFT JOIN dbo.StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
                WHERE i.Status = 8 AND (
                    i.CompletedById IS NULL OR i.CompletedAtUtc IS NULL OR a.Status <> 'Posted'
                    OR (i.Method IN (1, 2) AND (i.ProceedsAmount <= 0 OR i.ProceedsAccountId IS NULL OR i.BuyerOrRecipient IS NULL
                        OR i.ProceedsPostingEventId IS NULL OR i.ProceedsJournalEntryId IS NULL))
                    OR (i.Method NOT IN (1, 2) AND i.ProceedsAmount <> 0)))
                THROW 51117, 'INV_DISPOSAL_COMPLETION_LINEAGE_INVALID', 1;
        END;
        """;

    public const string LinkGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InventoryDisposalAuctionInvoices_Guard
        ON dbo.InventoryDisposalAuctionInvoices AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted)
                THROW 51961, 'INV_DISPOSAL_AUCTION_LINK_IMMUTABLE', 1;
            IF EXISTS(SELECT 1 FROM inserted l
                LEFT JOIN dbo.InventoryDisposalCases d ON d.Id=l.InventoryDisposalCaseId AND d.TenantId=l.TenantId
                LEFT JOIN dbo.Invoices v ON v.Id=l.InvoiceId AND v.TenantId=l.TenantId
                LEFT JOIN dbo.Users u ON u.Id=l.CreatedByUserId
                WHERE d.Id IS NULL OR d.IsDeleted=1 OR d.AccountingVersion<>1 OR d.Method<>1 OR d.Status NOT IN(6,11)
                  OR v.Id IS NULL OR v.IsDeleted=1 OR v.Status<>1 OR v.Reference<>d.DisposalNumber OR v.IsOpeningBalance=1
                  OR v.ExchangeRate<>1 OR v.SubTotal<=0 OR v.DiscountAmount<>0 OR l.IsDeleted=1 OR u.Id IS NULL
                  OR NOT EXISTS(SELECT 1 FROM dbo.UserTenants ut WHERE ut.TenantId=l.TenantId AND ut.UserId=l.CreatedByUserId AND ut.IsDeleted=0 AND ut.Status=0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt>SYSUTCDATETIME()))
                  OR ISJSON(l.InvoiceEconomicsJson)<>1)
                THROW 51962, 'INV_DISPOSAL_AUCTION_SOURCE_INVALID', 1;
            IF EXISTS(SELECT 1 FROM inserted l
                WHERE NOT EXISTS(SELECT 1 FROM dbo.InventoryDisposalLines d WHERE d.InventoryDisposalCaseId=l.InventoryDisposalCaseId AND d.TenantId=l.TenantId AND d.IsDeleted=0)
                OR (SELECT COUNT(*) FROM dbo.InvoiceLineItem v WHERE v.InvoiceId=l.InvoiceId AND v.IsDeleted=0)<>
                   (SELECT COUNT(*) FROM dbo.InventoryDisposalLines d WHERE d.InventoryDisposalCaseId=l.InventoryDisposalCaseId AND d.TenantId=l.TenantId AND d.IsDeleted=0))
                THROW 51963, 'INV_DISPOSAL_AUCTION_LINES_REQUIRED', 1;
            IF EXISTS(SELECT 1 FROM inserted l
                JOIN dbo.InventoryDisposalLines d ON d.InventoryDisposalCaseId=l.InventoryDisposalCaseId AND d.TenantId=l.TenantId AND d.IsDeleted=0
                LEFT JOIN dbo.InventoryItems item ON item.Id=d.InventoryItemId AND item.TenantId=l.TenantId AND item.IsDeleted=0
                LEFT JOIN dbo.InvoiceLineItem v ON v.InvoiceId=l.InvoiceId AND v.TenantId=l.TenantId AND v.IsDeleted=0 AND
                    v.Id=CONVERT(uniqueidentifier,SUBSTRING(HASHBYTES('SHA2_256',CONVERT(varchar(200),CONCAT('INVENTORY:DISPOSAL:AUCTION:LINE:V1:',
                        LOWER(REPLACE(CONVERT(varchar(36),l.TenantId),'-','')),':',LOWER(REPLACE(CONVERT(varchar(36),l.InventoryDisposalCaseId),'-','')),':',LOWER(REPLACE(CONVERT(varchar(36),d.Id),'-',''))))),1,16))
                LEFT JOIN dbo.Accounts a ON a.Id=v.GLAccountId AND a.TenantId=l.TenantId
                WHERE item.Id IS NULL OR v.Id IS NULL OR v.LineItemType<>2 OR v.ProductId IS NOT NULL OR v.InventoryItemId IS NOT NULL
                  OR v.Quantity<>d.Quantity OR v.UnitPrice<=0 OR v.GLAccountId<>item.InventoryDisposalAccountId OR item.InventoryDisposalAccountId IS NULL
                  OR a.Id IS NULL OR a.IsDeleted=1 OR a.AllowDirectPosting=0 OR a.IsControlAccount=1 OR a.Status<>1 OR a.AccountType NOT IN(4,5)
                  OR v.DiscountAmount<>0 OR v.DiscountPercentage<>0 OR v.TaxTreatment NOT IN(1,2,3,4))
                THROW 51964, 'INV_DISPOSAL_AUCTION_LINEAGE_INVALID', 1;
            IF EXISTS(SELECT 1 FROM inserted l JOIN dbo.Invoices v ON v.Id=l.InvoiceId
                OUTER APPLY OPENJSON(l.InvoiceEconomicsJson) WITH (
                    Id uniqueidentifier, TenantId uniqueidentifier, BusinessPartnerId uniqueidentifier,
                    BusinessPartnerRoleId uniqueidentifier, CurrencyCode nvarchar(3), ExchangeRate decimal(18,4),
                    ExchangeRateId uniqueidentifier, InvoiceDate date, DueDate date,
                    SubTotal decimal(18,2), TaxAmount decimal(18,2), DiscountAmount decimal(18,2),
                    TotalAmount decimal(18,2), Reference nvarchar(100), IsOpeningBalance bit) j
                WHERE j.Id IS NULL OR EXISTS(
                    SELECT v.Id,v.TenantId,v.BusinessPartnerId,v.BusinessPartnerRoleId,v.CurrencyCode,v.ExchangeRate,v.ExchangeRateId,
                        CONVERT(date,v.InvoiceDate),CONVERT(date,v.DueDate),v.SubTotal,v.TaxAmount,v.DiscountAmount,v.TotalAmount,v.Reference,v.IsOpeningBalance
                    EXCEPT
                    SELECT j.Id,j.TenantId,j.BusinessPartnerId,j.BusinessPartnerRoleId,j.CurrencyCode,j.ExchangeRate,j.ExchangeRateId,
                        j.InvoiceDate,j.DueDate,j.SubTotal,j.TaxAmount,j.DiscountAmount,j.TotalAmount,j.Reference,j.IsOpeningBalance))
                THROW 51970, 'INV_DISPOSAL_AUCTION_SNAPSHOT_INVALID', 1;
            IF EXISTS(SELECT 1 FROM inserted l WHERE
                (SELECT COUNT(*) FROM OPENJSON(l.InvoiceEconomicsJson,'$.Lines'))<>
                (SELECT COUNT(*) FROM dbo.InvoiceLineItem v WHERE v.InvoiceId=l.InvoiceId AND v.IsDeleted=0))
                THROW 51970, 'INV_DISPOSAL_AUCTION_SNAPSHOT_INVALID', 1;
            IF EXISTS(SELECT 1 FROM inserted l JOIN dbo.InvoiceLineItem v ON v.InvoiceId=l.InvoiceId AND v.IsDeleted=0
                WHERE NOT EXISTS(SELECT 1 FROM OPENJSON(l.InvoiceEconomicsJson,'$.Lines') WITH (
                    Id uniqueidentifier, LineItemType int, GLAccountId uniqueidentifier, ProductId uniqueidentifier,
                    Description nvarchar(200), Quantity decimal(18,4), UnitPrice decimal(18,2), TaxAmount decimal(18,2),
                    TaxRate decimal(18,4), TaxTreatment int, TaxGroupId uniqueidentifier,
                    DiscountAmount decimal(18,2), DiscountPercentage decimal(18,4)) j
                    WHERE j.Id=v.Id AND NOT EXISTS(
                        SELECT v.LineItemType,v.GLAccountId,v.ProductId,v.Description,v.Quantity,v.UnitPrice,v.TaxAmount,v.TaxRate,
                            v.TaxTreatment,v.TaxGroupId,v.DiscountAmount,v.DiscountPercentage
                        EXCEPT
                        SELECT j.LineItemType,j.GLAccountId,j.ProductId,j.Description,j.Quantity,j.UnitPrice,j.TaxAmount,j.TaxRate,
                            j.TaxTreatment,j.TaxGroupId,j.DiscountAmount,j.DiscountPercentage)))
                THROW 51970, 'INV_DISPOSAL_AUCTION_SNAPSHOT_INVALID', 1;
        END;
        """;

    public const string InvoiceGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_Invoices_DisposalEconomics
        ON dbo.Invoices AFTER UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted d JOIN dbo.InventoryDisposalAuctionInvoices l ON l.InvoiceId=d.Id
                LEFT JOIN inserted i ON i.Id=d.Id
                WHERE i.Id IS NULL OR EXISTS(
                    SELECT i.TenantId,i.BusinessPartnerId,i.BusinessPartnerRoleId,i.CurrencyCode,i.ExchangeRate,i.ExchangeRateId,
                        i.InvoiceDate,i.DueDate,i.SubTotal,i.TaxAmount,i.DiscountAmount,i.TotalAmount,i.Reference,i.IsOpeningBalance,i.IsDeleted
                    EXCEPT
                    SELECT d.TenantId,d.BusinessPartnerId,d.BusinessPartnerRoleId,d.CurrencyCode,d.ExchangeRate,d.ExchangeRateId,
                        d.InvoiceDate,d.DueDate,d.SubTotal,d.TaxAmount,d.DiscountAmount,d.TotalAmount,d.Reference,d.IsOpeningBalance,d.IsDeleted))
                THROW 51965, 'INV_DISPOSAL_AUCTION_INVOICE_ECONOMICS_IMMUTABLE', 1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                JOIN dbo.InventoryDisposalAuctionInvoices l ON l.InvoiceId=i.Id
                JOIN dbo.InventoryDisposalCases c ON c.Id=l.InventoryDisposalCaseId
                WHERE i.Status<>d.Status AND (c.IsDeleted=1 OR c.Status NOT IN(6,7,8,11)))
                THROW 51966, 'INV_DISPOSAL_AUCTION_INVOICE_SOURCE_CLOSED', 1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                JOIN dbo.InventoryDisposalAuctionInvoices l ON l.InvoiceId=i.Id
                JOIN dbo.InventoryDisposalCases c ON c.Id=l.InventoryDisposalCaseId
                WHERE i.Status=6 AND d.Status<>6 AND (c.Status NOT IN(6,11) OR d.Status NOT IN(1,9)
                    OR d.JournalEntryId IS NOT NULL OR d.PaidAmount<>0 OR d.CreditedAmount<>0))
                THROW 51968, 'INV_DISPOSAL_AUCTION_GOVERNED_REVERSAL_REQUIRED', 1;
        END;
        """;

    public const string InvoiceLineGuard = """
        CREATE OR ALTER TRIGGER dbo.TR_InvoiceLineItem_DisposalEconomics
        ON dbo.InvoiceLineItem AFTER INSERT, UPDATE, DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS(SELECT 1 FROM deleted d JOIN dbo.InventoryDisposalAuctionInvoices l ON l.InvoiceId=d.InvoiceId
                LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL OR EXISTS(
                    SELECT i.InvoiceId,i.TenantId,i.LineItemType,i.GLAccountId,i.ProductId,i.InventoryItemId,i.Description,
                        i.Quantity,i.UnitPrice,i.TaxGroupId,i.TaxTreatment,i.TaxRate,i.TaxAmount,i.DiscountPercentage,i.DiscountAmount,i.IsDeleted
                    EXCEPT
                    SELECT d.InvoiceId,d.TenantId,d.LineItemType,d.GLAccountId,d.ProductId,d.InventoryItemId,d.Description,
                        d.Quantity,d.UnitPrice,d.TaxGroupId,d.TaxTreatment,d.TaxRate,d.TaxAmount,d.DiscountPercentage,d.DiscountAmount,d.IsDeleted))
                THROW 51967, 'INV_DISPOSAL_AUCTION_LINE_ECONOMICS_IMMUTABLE', 1;
            IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.InventoryDisposalAuctionInvoices l ON l.InvoiceId=i.InvoiceId
                LEFT JOIN deleted d ON d.Id=i.Id WHERE d.Id IS NULL OR d.InvoiceId<>i.InvoiceId)
                THROW 51967, 'INV_DISPOSAL_AUCTION_LINE_ECONOMICS_IMMUTABLE', 1;
        END;
        """;
}
