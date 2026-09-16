using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPackageBoqFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // InitialBaseline used VendorInvoices, while this migration's target model and
            // every subsequent runtime mapping use VendorInvoice. Preserve all rows through
            // the missing compatibility rename and fail closed if both identities coexist or
            // neither lineage exists. The table rename retains its rows, keys and dependent
            // foreign keys. This migration may already be recorded as applied on developer
            // databases; do not simplify or remove this bridge without rerunning the full
            // SQL Server forward-and-downgrade chain tests.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[VendorInvoices]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL
                    THROW 51000, 'Vendor invoice migration compatibility failed: both VendorInvoices and VendorInvoice exist. Reconcile the duplicate tables before continuing.', 1;

                IF OBJECT_ID(N'[dbo].[VendorInvoices]', N'U') IS NULL
                   AND OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NULL
                    THROW 51000, 'Vendor invoice migration compatibility failed: neither VendorInvoices nor VendorInvoice exists. Restore the predecessor table before continuing.', 1;

                IF OBJECT_ID(N'[dbo].[VendorInvoices]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NULL
                    EXEC sys.sp_rename N'[dbo].[VendorInvoices]', N'VendorInvoice';
                """);

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ProjectDevelopmentProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectDevelopmentProfiles](
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [DeliveryStructure] nvarchar(30) NOT NULL,
        [DevelopmentType] nvarchar(100) NULL,
        [SiteName] nvarchar(200) NULL,
        [SiteAddress] nvarchar(2000) NULL,
        [LandReference] nvarchar(200) NULL,
        [ProcurementRoute] nvarchar(100) NULL,
        [ContractStrategy] nvarchar(100) NULL,
        [ConsultantTeam] nvarchar(1000) NULL,
        [FundingArrangement] nvarchar(200) NULL,
        [HandoverStrategy] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ProjectDevelopmentProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectDevelopmentProfiles_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectDevelopmentProfiles_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectDevelopmentProfiles_ProjectId' AND object_id = OBJECT_ID(N'[dbo].[ProjectDevelopmentProfiles]'))
    CREATE UNIQUE INDEX [IX_ProjectDevelopmentProfiles_ProjectId] ON [dbo].[ProjectDevelopmentProfiles]([ProjectId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectDevelopmentProfiles_TenantId_DeliveryStructure' AND object_id = OBJECT_ID(N'[dbo].[ProjectDevelopmentProfiles]'))
    CREATE INDEX [IX_ProjectDevelopmentProfiles_TenantId_DeliveryStructure] ON [dbo].[ProjectDevelopmentProfiles]([TenantId], [DeliveryStructure]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ProjectPhases]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectPhases](
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ParentPhaseId] uniqueidentifier NULL,
        [Code] nvarchar(50) NULL,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Status] nvarchar(30) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsOptional] bit NOT NULL,
        [IsStageGateRequired] bit NOT NULL,
        [IsTemplateSeeded] bit NOT NULL,
        [PlannedStartDate] datetime2 NULL,
        [PlannedEndDate] datetime2 NULL,
        [ActualStartDate] datetime2 NULL,
        [ActualEndDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ProjectPhases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectPhases_ProjectPhases_ParentPhaseId] FOREIGN KEY ([ParentPhaseId]) REFERENCES [dbo].[ProjectPhases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPhases_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectPhases_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPhases_ParentPhaseId' AND object_id = OBJECT_ID(N'[dbo].[ProjectPhases]'))
    CREATE INDEX [IX_ProjectPhases_ParentPhaseId] ON [dbo].[ProjectPhases]([ParentPhaseId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPhases_ProjectId_ParentPhaseId_SortOrder' AND object_id = OBJECT_ID(N'[dbo].[ProjectPhases]'))
    CREATE INDEX [IX_ProjectPhases_ProjectId_ParentPhaseId_SortOrder] ON [dbo].[ProjectPhases]([ProjectId], [ParentPhaseId], [SortOrder]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPhases_ProjectId_Status' AND object_id = OBJECT_ID(N'[dbo].[ProjectPhases]'))
    CREATE INDEX [IX_ProjectPhases_ProjectId_Status] ON [dbo].[ProjectPhases]([ProjectId], [Status]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPhases_TenantId' AND object_id = OBJECT_ID(N'[dbo].[ProjectPhases]'))
    CREATE INDEX [IX_ProjectPhases_TenantId] ON [dbo].[ProjectPhases]([TenantId]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ProjectPackages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectPackages](
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ProjectPhaseId] uniqueidentifier NULL,
        [Code] nvarchar(50) NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [PackageType] nvarchar(50) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [SortOrder] int NOT NULL,
        [ProcurementRoute] nvarchar(100) NULL,
        [ContractStrategy] nvarchar(100) NULL,
        [BusinessPartnerId] uniqueidentifier NULL,
        [TenderId] uniqueidentifier NULL,
        [ContractId] uniqueidentifier NULL,
        [ProcurementPlanItemId] uniqueidentifier NULL,
        [PurchaseRequisitionId] uniqueidentifier NULL,
        [PurchaseOrderId] uniqueidentifier NULL,
        [BudgetAmount] decimal(18,2) NULL,
        [CommittedAmount] decimal(18,2) NULL,
        [ActualAmount] decimal(18,2) NULL,
        [ForecastAmount] decimal(18,2) NULL,
        [Currency] nvarchar(10) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ProjectPackages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectPackages_ProjectPhases_ProjectPhaseId] FOREIGN KEY ([ProjectPhaseId]) REFERENCES [dbo].[ProjectPhases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectPackages_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectPackages_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPackages_ProjectId_Code' AND object_id = OBJECT_ID(N'[dbo].[ProjectPackages]'))
    CREATE INDEX [IX_ProjectPackages_ProjectId_Code] ON [dbo].[ProjectPackages]([ProjectId], [Code]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPackages_ProjectId_ProjectPhaseId' AND object_id = OBJECT_ID(N'[dbo].[ProjectPackages]'))
    CREATE INDEX [IX_ProjectPackages_ProjectId_ProjectPhaseId] ON [dbo].[ProjectPackages]([ProjectId], [ProjectPhaseId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPackages_ProjectId_SortOrder' AND object_id = OBJECT_ID(N'[dbo].[ProjectPackages]'))
    CREATE INDEX [IX_ProjectPackages_ProjectId_SortOrder] ON [dbo].[ProjectPackages]([ProjectId], [SortOrder]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPackages_ProjectId_Status' AND object_id = OBJECT_ID(N'[dbo].[ProjectPackages]'))
    CREATE INDEX [IX_ProjectPackages_ProjectId_Status] ON [dbo].[ProjectPackages]([ProjectId], [Status]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPackages_ProjectPhaseId' AND object_id = OBJECT_ID(N'[dbo].[ProjectPackages]'))
    CREATE INDEX [IX_ProjectPackages_ProjectPhaseId] ON [dbo].[ProjectPackages]([ProjectPhaseId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectPackages_TenantId' AND object_id = OBJECT_ID(N'[dbo].[ProjectPackages]'))
    CREATE INDEX [IX_ProjectPackages_TenantId] ON [dbo].[ProjectPackages]([TenantId]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ProjectBoqItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProjectBoqItems](
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ProjectPackageId] uniqueidentifier NOT NULL,
        [LineNumber] nvarchar(50) NULL,
        [ItemCode] nvarchar(50) NULL,
        [ItemType] nvarchar(50) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitOfMeasure] nvarchar(20) NULL,
        [UnitRate] decimal(18,4) NULL,
        [BudgetAmount] decimal(18,2) NULL,
        [CommittedAmount] decimal(18,2) NULL,
        [ActualAmount] decimal(18,2) NULL,
        [ForecastAmount] decimal(18,2) NULL,
        [Currency] nvarchar(10) NOT NULL,
        [InventoryItemId] uniqueidentifier NULL,
        [TenderItemId] uniqueidentifier NULL,
        [ProcurementPlanItemId] uniqueidentifier NULL,
        [PurchaseRequisitionItemId] uniqueidentifier NULL,
        [PurchaseOrderItemId] uniqueidentifier NULL,
        [Notes] nvarchar(2000) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ProjectBoqItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectBoqItems_ProjectPackages_ProjectPackageId] FOREIGN KEY ([ProjectPackageId]) REFERENCES [dbo].[ProjectPackages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectBoqItems_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectBoqItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectBoqItems_ProjectId_ItemType' AND object_id = OBJECT_ID(N'[dbo].[ProjectBoqItems]'))
    CREATE INDEX [IX_ProjectBoqItems_ProjectId_ItemType] ON [dbo].[ProjectBoqItems]([ProjectId], [ItemType]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectBoqItems_ProjectId_ProjectPackageId_LineNumber' AND object_id = OBJECT_ID(N'[dbo].[ProjectBoqItems]'))
    CREATE INDEX [IX_ProjectBoqItems_ProjectId_ProjectPackageId_LineNumber] ON [dbo].[ProjectBoqItems]([ProjectId], [ProjectPackageId], [LineNumber]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectBoqItems_ProjectId_ProjectPackageId_SortOrder' AND object_id = OBJECT_ID(N'[dbo].[ProjectBoqItems]'))
    CREATE INDEX [IX_ProjectBoqItems_ProjectId_ProjectPackageId_SortOrder] ON [dbo].[ProjectBoqItems]([ProjectId], [ProjectPackageId], [SortOrder]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectBoqItems_ProjectPackageId' AND object_id = OBJECT_ID(N'[dbo].[ProjectBoqItems]'))
    CREATE INDEX [IX_ProjectBoqItems_ProjectPackageId] ON [dbo].[ProjectBoqItems]([ProjectPackageId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectBoqItems_TenantId' AND object_id = OBJECT_ID(N'[dbo].[ProjectBoqItems]'))
    CREATE INDEX [IX_ProjectBoqItems_TenantId] ON [dbo].[ProjectBoqItems]([TenantId]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Up preserves the predecessor VendorInvoices rows by renaming the table. Validate
            // the complete singular lineage before dropping any Project-owned tables so a
            // conflicting or missing table fails without destructive mutation. The final rename
            // restores the exact predecessor table identity and all retained rows, keys and FKs.
            // Existing developer databases may already record this migration as applied; do not
            // remove this compatibility block without rerunning the full SQL Server chain tests.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NULL
                   OR OBJECT_ID(N'[dbo].[VendorInvoices]', N'U') IS NOT NULL
                    THROW 51000, 'Vendor invoice downgrade compatibility failed: expected only VendorInvoice. No Project tables were changed.', 1;
                """);

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ProjectBoqItems]', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectBoqItems];

IF OBJECT_ID(N'[dbo].[ProjectPackages]', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectPackages];

IF OBJECT_ID(N'[dbo].[ProjectPhases]', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectPhases];

IF OBJECT_ID(N'[dbo].[ProjectDevelopmentProfiles]', N'U') IS NOT NULL
    DROP TABLE [dbo].[ProjectDevelopmentProfiles];
");

            migrationBuilder.Sql("""
                EXEC sys.sp_rename N'[dbo].[VendorInvoice]', N'VendorInvoices';
                """);
        }
    }
}
