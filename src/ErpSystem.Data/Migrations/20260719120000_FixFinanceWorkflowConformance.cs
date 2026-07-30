using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260719120000_FixFinanceWorkflowConformance")]
public partial class FixFinanceWorkflowConformance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[FinancePurchaseOrderReceipts]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'Status') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [Status] int NOT NULL CONSTRAINT [DF_FinancePurchaseOrderReceipts_Status] DEFAULT 1;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'WorkflowInstanceId') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [WorkflowInstanceId] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'SubmittedAt') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [SubmittedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'SubmittedById') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [SubmittedById] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'ApprovedAt') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [ApprovedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'ApprovedById') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [ApprovedById] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'RejectedAt') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [RejectedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'RejectedById') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [RejectedById] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.FinancePurchaseOrderReceipts', N'RejectionReason') IS NULL
                    ALTER TABLE [dbo].[FinancePurchaseOrderReceipts] ADD [RejectionReason] nvarchar(1000) NULL;

                EXEC(N'
                    UPDATE [dbo].[FinancePurchaseOrderReceipts]
                    SET [Status] = 3,
                        [ApprovedAt] = COALESCE([ApprovedAt], [UpdatedAt], [CreatedAt])
                    WHERE [Status] = 1
                      AND [IsDeleted] = 0;');
            END;
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[UnitAccountBudgets]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.UnitAccountBudgets', N'Status') IS NULL
            BEGIN
                ALTER TABLE [dbo].[UnitAccountBudgets] ADD [Status] nvarchar(30) NOT NULL CONSTRAINT [DF_UnitAccountBudgets_Status] DEFAULT N'Draft';
                EXEC(N'
                    UPDATE [dbo].[UnitAccountBudgets]
                    SET [Status] = CASE WHEN [IsActive] = 1 THEN N''Approved'' ELSE N''Draft'' END
                    WHERE [IsDeleted] = 0;');
            END;

            IF OBJECT_ID(N'[dbo].[AllocationRules]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.AllocationRules', N'ApprovalStatus') IS NULL
            BEGIN
                ALTER TABLE [dbo].[AllocationRules] ADD [ApprovalStatus] nvarchar(30) NOT NULL CONSTRAINT [DF_AllocationRules_ApprovalStatus] DEFAULT N'Draft';
                EXEC(N'
                    UPDATE [dbo].[AllocationRules]
                    SET [ApprovalStatus] = CASE WHEN [IsActive] = 1 THEN N''Approved'' ELSE N''Draft'' END
                    WHERE [IsDeleted] = 0;');
            END;
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'ApprovalStatus') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [ApprovalStatus] nvarchar(30) NOT NULL CONSTRAINT [DF_AssetDepreciationSchedules_ApprovalStatus] DEFAULT N'Draft';
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'SubmittedAt') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [SubmittedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'SubmittedById') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [SubmittedById] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'ApprovedAt') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [ApprovedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'ApprovedById') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [ApprovedById] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'RejectedAt') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [RejectedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'RejectedById') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [RejectedById] uniqueidentifier NULL;
                IF COL_LENGTH(N'dbo.AssetDepreciationSchedules', N'RejectionReason') IS NULL
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [RejectionReason] nvarchar(1000) NULL;

                EXEC(N'
                    UPDATE [dbo].[AssetDepreciationSchedules]
                    SET [ApprovalStatus] = CASE WHEN [IsPosted] = 1 THEN N''Approved'' ELSE [ApprovalStatus] END,
                        [ApprovedAt] = CASE WHEN [IsPosted] = 1 THEN COALESCE([ApprovedAt], [PostedDate], [UpdatedAt], [CreatedAt]) ELSE [ApprovedAt] END
                    WHERE [IsDeleted] = 0;');
            END;
            """);

        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[WorkflowEntityTypes]', N'U') IS NOT NULL
            BEGIN
                UPDATE [dbo].[WorkflowEntityTypes]
                SET [Code] = N'SupplierReturn',
                    [Name] = N'Supplier Return',
                    [Description] = COALESCE([Description], N'Supplier return approval before goods are shipped back, debit notes are issued, or refunds are tracked.'),
                    [EntityClassName] = N'ErpSystem.Core.Entities.Finance.SupplierReturn',
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Code] = N'PurchaseReturn'
                  AND [Name] = N'Supplier Return'
                  AND [IsDeleted] = 0;

                DECLARE @RetiredEntityTypes TABLE ([Id] uniqueidentifier NOT NULL);

                UPDATE [dbo].[WorkflowEntityTypes]
                SET [IsActive] = 0,
                    [UpdatedAt] = SYSUTCDATETIME()
                OUTPUT inserted.[Id] INTO @RetiredEntityTypes
                WHERE [IsDeleted] = 0
                  AND (
                        [Code] IN (N'VendorPayment', N'Cheque', N'ExchangeRate')
                        OR [Name] IN (N'Vendor Payment', N'Cheque', N'Exchange Rate')
                      );

                IF OBJECT_ID(N'[dbo].[WorkflowDefinitions]', N'U') IS NOT NULL
                BEGIN
                    UPDATE wd
                    SET wd.[IsActive] = 0,
                        wd.[LifecycleStatus] = 2,
                        wd.[RetiredAt] = COALESCE(wd.[RetiredAt], SYSUTCDATETIME()),
                        wd.[UpdatedAt] = SYSUTCDATETIME()
                    FROM [dbo].[WorkflowDefinitions] wd
                    WHERE wd.[EntityTypeId] IN (SELECT [Id] FROM @RetiredEntityTypes)
                      AND wd.[IsDeleted] = 0
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM [dbo].[WorkflowInstances] wi
                          WHERE wi.[WorkflowDefinitionId] = wd.[Id]
                            AND wi.[IsDeleted] = 0
                            AND wi.[Status] IN (0, 1, 5, 6)
                      );
                END;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[WorkflowEntityTypes]', N'U') IS NOT NULL
            BEGIN
                UPDATE [dbo].[WorkflowEntityTypes]
                SET [Code] = N'PurchaseReturn',
                    [EntityClassName] = N'ErpSystem.Core.Entities.Inventory.PurchaseReturn',
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Code] = N'SupplierReturn'
                  AND [Name] = N'Supplier Return'
                  AND [IsDeleted] = 0;

                UPDATE [dbo].[WorkflowEntityTypes]
                SET [IsActive] = 1,
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [IsDeleted] = 0
                  AND [Code] IN (N'VendorPayment', N'Cheque', N'ExchangeRate');
            END;
            """);

        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "RejectionReason");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "RejectedById");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "RejectedAt");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "ApprovedById");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "ApprovedAt");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "SubmittedById");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "SubmittedAt");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "WorkflowInstanceId");
        DropColumnIfExists(migrationBuilder, "FinancePurchaseOrderReceipts", "Status");

        DropColumnIfExists(migrationBuilder, "UnitAccountBudgets", "Status");
        DropColumnIfExists(migrationBuilder, "AllocationRules", "ApprovalStatus");

        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "RejectionReason");
        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "RejectedById");
        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "RejectedAt");
        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "ApprovedById");
        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "ApprovedAt");
        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "SubmittedById");
        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "SubmittedAt");
        DropColumnIfExists(migrationBuilder, "AssetDepreciationSchedules", "ApprovalStatus");
    }

    private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string tableName, string columnName)
    {
        migrationBuilder.Sql(
            $"""
            IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.{tableName}', N'{columnName}') IS NOT NULL
            BEGIN
                DECLARE @constraintName nvarchar(200);
                SELECT @constraintName = dc.[name]
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON c.[default_object_id] = dc.[object_id]
                WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[{tableName}]')
                  AND c.[name] = N'{columnName}';

                IF @constraintName IS NOT NULL
                    EXEC(N'ALTER TABLE [dbo].[{tableName}] DROP CONSTRAINT [' + @constraintName + N']');

                ALTER TABLE [dbo].[{tableName}] DROP COLUMN [{columnName}];
            END;
            """);
    }
}
