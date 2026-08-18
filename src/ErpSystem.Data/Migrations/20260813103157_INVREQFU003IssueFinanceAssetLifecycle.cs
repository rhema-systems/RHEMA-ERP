using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class INVREQFU003IssueFinanceAssetLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinanceJournalEntryId",
                table: "InventoryIssueVouchers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancePostingEventId",
                table: "InventoryIssueVouchers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MovementReasonCode",
                table: "InventoryIssueVouchers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE v
                SET MovementReasonCode = CASE
                    WHEN EXISTS (
                        SELECT 1 FROM InventoryIssueVoucherLines line
                        INNER JOIN InventoryItems item ON item.Id = line.InventoryItemId
                        WHERE line.InventoryIssueVoucherId = v.Id AND line.TenantId = v.TenantId
                          AND line.IsDeleted = 0 AND item.IsDeleted = 0)
                     AND NOT EXISTS (
                        SELECT 1 FROM InventoryIssueVoucherLines line
                        INNER JOIN InventoryItems item ON item.Id = line.InventoryItemId
                        WHERE line.InventoryIssueVoucherId = v.Id AND line.TenantId = v.TenantId
                          AND line.IsDeleted = 0 AND item.IsDeleted = 0 AND item.ItemType <> 4)
                        THEN 'ASSET_CUSTODY'
                    WHEN v.ProjectId IS NOT NULL THEN 'PROJECT_CONSUMPTION'
                    ELSE 'DEPARTMENT_CONSUMPTION'
                END
                FROM InventoryIssueVouchers v;
                """);

            migrationBuilder.CreateTable(
                name: "InventoryIssueAccountingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    MovementReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Treatment = table.Column<int>(type: "int", nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FixedAssetCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryIssueAccountingRules", x => x.Id);
                    table.CheckConstraint("CK_InventoryIssueAccountingRules_Effective", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                    table.CheckConstraint("CK_InventoryIssueAccountingRules_ItemType", "[ItemType] IN (1,2,3,4)");
                    table.CheckConstraint("CK_InventoryIssueAccountingRules_Owner", "([Treatment] = 1 AND [ExpenseAccountId] IS NOT NULL AND [FixedAssetCategoryId] IS NULL AND [ItemType] <> 4) OR ([Treatment] = 2 AND [ExpenseAccountId] IS NULL AND [FixedAssetCategoryId] IS NOT NULL AND [ItemType] = 4 AND [MovementReasonCode] = 'ASSET_CUSTODY')");
                    table.CheckConstraint("CK_InventoryIssueAccountingRules_Treatment", "[Treatment] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_InventoryIssueAccountingRules_Accounts_ExpenseAccountId",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueAccountingRules_FixedAssetCategories_FixedAssetCategoryId",
                        column: x => x.FixedAssetCategoryId,
                        principalTable: "FixedAssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueAccountingRules_InventoryCategories_InventoryCategoryId",
                        column: x => x.InventoryCategoryId,
                        principalTable: "InventoryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueAccountingRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryIssueFinanceLineages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryIssueVoucherLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryIssueAccountingRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Treatment = table.Column<int>(type: "int", nullable: false),
                    MovementReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IssuedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryIssueFinanceLineages", x => x.Id);
                    table.CheckConstraint("CK_InventoryIssueFinanceLineages_Asset", "([Treatment] = 1 AND [FixedAssetId] IS NULL) OR ([Treatment] = 2 AND [FixedAssetId] IS NOT NULL AND [IssuedQuantity] = 1)");
                    table.CheckConstraint("CK_InventoryIssueFinanceLineages_Integrity", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_InventoryIssueFinanceLineages_Quantity", "[IssuedQuantity] > 0 AND [ReturnedQuantity] >= 0 AND [ReturnedQuantity] <= [IssuedQuantity]");
                    table.CheckConstraint("CK_InventoryIssueFinanceLineages_Status", "[Status] IN (1,2,3)");
                    table.CheckConstraint("CK_InventoryIssueFinanceLineages_Treatment", "[Treatment] IN (1,2)");
                    table.CheckConstraint("CK_InventoryIssueFinanceLineages_Value", "[IssuedValue] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryIssueFinanceLineages_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueFinanceLineages_InventoryIssueAccountingRules_InventoryIssueAccountingRuleId",
                        column: x => x.InventoryIssueAccountingRuleId,
                        principalTable: "InventoryIssueAccountingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueFinanceLineages_InventoryIssueVoucherLines_InventoryIssueVoucherLineId",
                        column: x => x.InventoryIssueVoucherLineId,
                        principalTable: "InventoryIssueVoucherLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueFinanceLineages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryIssueReturnAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryReturnVoucherLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryIssueFinanceLineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReturnPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReversalPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversalJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryIssueReturnAllocations", x => x.Id);
                    table.CheckConstraint("CK_InventoryIssueReturnAllocations_Integrity", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_InventoryIssueReturnAllocations_Quantity", "[Quantity] > 0 AND [Value] > 0");
                    table.CheckConstraint("CK_InventoryIssueReturnAllocations_Reversal", "([ReversalPostingEventId] IS NULL AND [ReversalJournalEntryId] IS NULL AND [ReversedAtUtc] IS NULL) OR ([ReversalPostingEventId] IS NOT NULL AND [ReversalJournalEntryId] IS NOT NULL AND [ReversedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InventoryIssueReturnAllocations_InventoryIssueFinanceLineages_InventoryIssueFinanceLineageId",
                        column: x => x.InventoryIssueFinanceLineageId,
                        principalTable: "InventoryIssueFinanceLineages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueReturnAllocations_InventoryReturnVoucherLines_InventoryReturnVoucherLineId",
                        column: x => x.InventoryReturnVoucherLineId,
                        principalTable: "InventoryReturnVoucherLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueReturnAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueAccountingRules_ExpenseAccountId",
                table: "InventoryIssueAccountingRules",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueAccountingRules_FixedAssetCategoryId",
                table: "InventoryIssueAccountingRules",
                column: "FixedAssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueAccountingRules_InventoryCategoryId",
                table: "InventoryIssueAccountingRules",
                column: "InventoryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueAccountingRules_TenantId_InventoryCategoryId_ItemType_MovementReasonCode",
                table: "InventoryIssueAccountingRules",
                columns: new[] { "TenantId", "InventoryCategoryId", "ItemType", "MovementReasonCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueAccountingRules_TenantId_IsActive_EffectiveFromUtc_EffectiveToUtc",
                table: "InventoryIssueAccountingRules",
                columns: new[] { "TenantId", "IsActive", "EffectiveFromUtc", "EffectiveToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueFinanceLineages_FixedAssetId",
                table: "InventoryIssueFinanceLineages",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueFinanceLineages_InventoryIssueAccountingRuleId",
                table: "InventoryIssueFinanceLineages",
                column: "InventoryIssueAccountingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueFinanceLineages_InventoryIssueVoucherLineId",
                table: "InventoryIssueFinanceLineages",
                column: "InventoryIssueVoucherLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueFinanceLineages_TenantId_FixedAssetId",
                table: "InventoryIssueFinanceLineages",
                columns: new[] { "TenantId", "FixedAssetId" },
                unique: true,
                filter: "[FixedAssetId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueFinanceLineages_TenantId_InventoryIssueVoucherLineId",
                table: "InventoryIssueFinanceLineages",
                columns: new[] { "TenantId", "InventoryIssueVoucherLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueFinanceLineages_TenantId_PostingEventId",
                table: "InventoryIssueFinanceLineages",
                columns: new[] { "TenantId", "PostingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueReturnAllocations_InventoryIssueFinanceLineageId",
                table: "InventoryIssueReturnAllocations",
                column: "InventoryIssueFinanceLineageId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueReturnAllocations_InventoryReturnVoucherLineId",
                table: "InventoryIssueReturnAllocations",
                column: "InventoryReturnVoucherLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueReturnAllocations_TenantId_InventoryReturnVoucherLineId_InventoryIssueFinanceLineageId",
                table: "InventoryIssueReturnAllocations",
                columns: new[] { "TenantId", "InventoryReturnVoucherLineId", "InventoryIssueFinanceLineageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueReturnAllocations_TenantId_ReturnPostingEventId",
                table: "InventoryIssueReturnAllocations",
                columns: new[] { "TenantId", "ReturnPostingEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueReturnAllocations_TenantId_ReversalPostingEventId",
                table: "InventoryIssueReturnAllocations",
                columns: new[] { "TenantId", "ReversalPostingEventId" },
                filter: "[ReversalPostingEventId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryIssueVouchers_MovementReason",
                table: "InventoryIssueVouchers",
                sql: "[MovementReasonCode] IN ('DEPARTMENT_CONSUMPTION','PROJECT_CONSUMPTION','MAINTENANCE_CONSUMPTION','ASSET_CUSTODY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryIssueVouchers_FinanceLineage",
                table: "InventoryIssueVouchers",
                sql: "([FinancePostingEventId] IS NULL AND [FinanceJournalEntryId] IS NULL) OR ([FinancePostingEventId] IS NOT NULL AND [FinanceJournalEntryId] IS NOT NULL)");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueAccountingRules_TenantOwner]
                ON [dbo].[InventoryIssueAccountingRules]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryCategories c ON c.Id = i.InventoryCategoryId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        LEFT JOIN Accounts expense ON expense.Id = i.ExpenseAccountId AND expense.TenantId = i.TenantId AND expense.IsDeleted = 0
                        LEFT JOIN FixedAssetCategories fac ON fac.Id = i.FixedAssetCategoryId AND fac.TenantId = i.TenantId AND fac.IsDeleted = 0
                        LEFT JOIN Accounts asset ON asset.Id = fac.AssetAccountId AND asset.TenantId = i.TenantId AND asset.IsDeleted = 0
                        WHERE c.Id IS NULL
                           OR i.MovementReasonCode NOT IN ('DEPARTMENT_CONSUMPTION','PROJECT_CONSUMPTION','MAINTENANCE_CONSUMPTION','ASSET_CUSTODY')
                           OR (i.Treatment = 1 AND (expense.Id IS NULL OR expense.AccountType <> 5 OR expense.Status <> 1 OR expense.AllowDirectPosting = 0))
                           OR (i.Treatment = 2 AND (fac.Id IS NULL OR asset.Id IS NULL OR asset.AccountType <> 1 OR asset.Status <> 1 OR asset.AllowDirectPosting = 0)))
                        THROW 51841, 'INV_ISSUE_ACCOUNTING_OWNER_INVALID: use active tenant-owned Inventory, Finance and Fixed Assets owners.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueVouchers_ControlledLifecycle]
                ON [dbo].[InventoryIssueVouchers]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51601, 'INV_ISSUE_VOUCHER_DELETE_BLOCKED: Store Issue Vouchers are immutable evidence.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.InventoryRequisitionId <> d.InventoryRequisitionId
                           OR i.VoucherNumber <> d.VoucherNumber OR i.WarehouseId <> d.WarehouseId
                           OR ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId,'00000000-0000-0000-0000-000000000000')
                           OR i.DepartmentId <> d.DepartmentId OR ISNULL(i.DepartmentName,N'') <> ISNULL(d.DepartmentName,N'')
                           OR ISNULL(i.CostCenter,N'') <> ISNULL(d.CostCenter,N'')
                           OR ISNULL(i.ProjectId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProjectId,'00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.ProjectCode,N'') <> ISNULL(d.ProjectCode,N'')
                           OR i.RequestedById <> d.RequestedById OR i.ApprovedById <> d.ApprovedById
                           OR i.IssuedById <> d.IssuedById OR i.ReceiverUserId <> d.ReceiverUserId
                           OR i.IssuedAtUtc <> d.IssuedAtUtc OR i.MovementReasonCode <> d.MovementReasonCode
                           OR i.IdempotencyKey <> d.IdempotencyKey OR ISNULL(i.Notes,N'') <> ISNULL(d.Notes,N'')
                           OR i.PayloadHash <> d.PayloadHash OR i.CorrelationId <> d.CorrelationId
                           OR i.SourceSnapshotJson <> d.SourceSnapshotJson OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedBy,N'') <> ISNULL(d.CreatedBy,N'')
                           OR ISNULL(i.CreatedById,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.CreatedById,'00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted OR ISNULL(i.DeletedAt,'0001-01-01') <> ISNULL(d.DeletedAt,'0001-01-01')
                           OR ISNULL(i.DeletedBy,N'') <> ISNULL(d.DeletedBy,N''))
                        THROW 51602, 'INV_ISSUE_VOUCHER_IMMUTABLE: voucher source lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            (d.FinancePostingEventId IS NULL AND d.FinanceJournalEntryId IS NULL
                             AND i.FinancePostingEventId IS NOT NULL AND i.FinanceJournalEntryId IS NOT NULL
                             AND i.TenantId = d.TenantId AND i.InventoryRequisitionId = d.InventoryRequisitionId
                             AND i.VoucherNumber = d.VoucherNumber AND i.WarehouseId = d.WarehouseId
                             AND ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000') = ISNULL(d.LocationId,'00000000-0000-0000-0000-000000000000')
                             AND i.DepartmentId = d.DepartmentId AND ISNULL(i.CostCenter,N'') = ISNULL(d.CostCenter,N'')
                             AND ISNULL(i.ProjectId,'00000000-0000-0000-0000-000000000000') = ISNULL(d.ProjectId,'00000000-0000-0000-0000-000000000000')
                             AND i.RequestedById = d.RequestedById AND i.ApprovedById = d.ApprovedById
                             AND i.IssuedById = d.IssuedById AND i.ReceiverUserId = d.ReceiverUserId
                             AND i.Status = d.Status AND ISNULL(i.AcknowledgedById,'00000000-0000-0000-0000-000000000000') = ISNULL(d.AcknowledgedById,'00000000-0000-0000-0000-000000000000')
                             AND ISNULL(i.AcknowledgedAtUtc,'0001-01-01') = ISNULL(d.AcknowledgedAtUtc,'0001-01-01')
                             AND ISNULL(i.ReceiverComment,N'') = ISNULL(d.ReceiverComment,N'')
                             AND i.IssuedAtUtc = d.IssuedAtUtc AND i.MovementReasonCode = d.MovementReasonCode
                             AND i.IdempotencyKey = d.IdempotencyKey AND i.PayloadHash = d.PayloadHash
                             AND i.CorrelationId = d.CorrelationId AND i.SourceSnapshotJson = d.SourceSnapshotJson
                             AND i.IntegrityHash <> d.IntegrityHash AND i.IsDeleted = d.IsDeleted)
                            OR
                            (d.Status = 1 AND i.Status = 2
                             AND i.FinancePostingEventId = d.FinancePostingEventId AND i.FinanceJournalEntryId = d.FinanceJournalEntryId
                             AND d.AcknowledgedById IS NULL AND i.AcknowledgedById = i.ReceiverUserId
                             AND d.AcknowledgedAtUtc IS NULL AND i.AcknowledgedAtUtc IS NOT NULL
                             AND d.ReceiverComment IS NULL AND LEN(LTRIM(RTRIM(i.ReceiverComment))) > 0
                             AND i.IntegrityHash <> d.IntegrityHash AND i.UpdatedAt IS NOT NULL
                             AND i.LastModifiedById = i.ReceiverUserId)))
                        THROW 51602, 'INV_ISSUE_VOUCHER_IMMUTABLE: only Finance binding or receiver acknowledgement is permitted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryRequisitions r ON r.Id = i.InventoryRequisitionId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                        WHERE r.Id IS NULL OR r.WarehouseId <> i.WarehouseId OR r.DepartmentId <> i.DepartmentId
                           OR r.RequestedById <> i.RequestedById OR r.ApprovedById <> i.ApprovedById OR r.Status NOT IN (3,4,5,6)
                           OR i.MovementReasonCode NOT IN ('DEPARTMENT_CONSUMPTION','PROJECT_CONSUMPTION','MAINTENANCE_CONSUMPTION','ASSET_CUSTODY')
                           OR (i.ProjectId IS NULL AND (i.CostCenter IS NULL OR LEN(LTRIM(RTRIM(i.CostCenter))) = 0))
                           OR (i.LocationId IS NOT NULL AND (l.Id IS NULL OR l.WarehouseId <> i.WarehouseId)))
                        THROW 51603, 'INV_ISSUE_APPROVED_SOURCE_REQUIRED: voucher lineage must match an approved tenant requisition and cost object.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE EXISTS (
                            SELECT required.UserId
                            FROM (VALUES (i.RequestedById),(i.ApprovedById),(i.IssuedById),(i.ReceiverUserId)) required(UserId)
                            WHERE NOT EXISTS (
                                SELECT 1 FROM UserTenants ut
                                INNER JOIN Users u ON u.Id = ut.UserId AND u.IsActive = 1
                                WHERE ut.TenantId = i.TenantId AND ut.UserId = required.UserId AND ut.IsDeleted = 0
                                  AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME()))))
                        THROW 51604, 'INV_ISSUE_ACTOR_INVALID: all issue actors must be active users in the same tenant.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.FinancePostingEventId IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM FinancePostingEvents pe
                            INNER JOIN JournalEntries j ON j.Id = i.FinanceJournalEntryId AND j.TenantId = i.TenantId AND j.IsDeleted = 0
                            WHERE pe.Id = i.FinancePostingEventId AND pe.TenantId = i.TenantId AND pe.IsDeleted = 0
                              AND pe.SourceDocumentType = N'InventoryIssueVoucher' AND pe.SourceDocumentId = i.Id
                              AND pe.JournalEntryId = i.FinanceJournalEntryId AND pe.PostingStatus = N'Posted'
                              AND j.SourceDocumentType = N'InventoryIssueVoucher' AND j.SourceDocumentId = i.Id
                              AND j.PostingStatus = N'Posted' AND j.IsBalanced = 1
                              AND j.TotalDebitAmount = j.TotalCreditAmount AND j.BalanceDifference = 0))
                        THROW 51842, 'INV_ISSUE_FINANCE_BINDING_INVALID: voucher Finance lineage must reference its posted balanced journal.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueFinanceLineages_Lifecycle]
                ON [dbo].[InventoryIssueFinanceLineages]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51843, 'INV_ISSUE_FINANCE_LINEAGE_DELETE_BLOCKED: posting lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.InventoryIssueVoucherLineId <> d.InventoryIssueVoucherLineId
                           OR i.InventoryIssueAccountingRuleId <> d.InventoryIssueAccountingRuleId OR i.Treatment <> d.Treatment
                           OR i.MovementReasonCode <> d.MovementReasonCode OR i.IssuedQuantity <> d.IssuedQuantity
                           OR i.IssuedValue <> d.IssuedValue OR i.PostingEventId <> d.PostingEventId
                           OR i.JournalEntryId <> d.JournalEntryId
                           OR ISNULL(i.FixedAssetId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.FixedAssetId,'00000000-0000-0000-0000-000000000000')
                           OR i.IntegrityHash <> d.IntegrityHash OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                           OR ISNULL(i.DeletedAt,'0001-01-01') <> ISNULL(d.DeletedAt,'0001-01-01')
                           OR ISNULL(i.DeletedBy,N'') <> ISNULL(d.DeletedBy,N''))
                        THROW 51844, 'INV_ISSUE_FINANCE_LINEAGE_IMMUTABLE: only returned quantity and derived status may change.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryIssueVoucherLines line ON line.Id = i.InventoryIssueVoucherLineId AND line.TenantId = i.TenantId AND line.IsDeleted = 0
                        LEFT JOIN InventoryIssueVouchers v ON v.Id = line.InventoryIssueVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        LEFT JOIN InventoryItems item ON item.Id = line.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                        LEFT JOIN InventoryIssueAccountingRules rmap ON rmap.Id = i.InventoryIssueAccountingRuleId AND rmap.TenantId = i.TenantId AND rmap.IsDeleted = 0
                        LEFT JOIN FinancePostingEvents pe ON pe.Id = i.PostingEventId AND pe.TenantId = i.TenantId AND pe.IsDeleted = 0
                        LEFT JOIN JournalEntries j ON j.Id = i.JournalEntryId AND j.TenantId = i.TenantId AND j.IsDeleted = 0
                        LEFT JOIN FixedAssets fa ON fa.Id = i.FixedAssetId AND fa.TenantId = i.TenantId AND fa.IsDeleted = 0
                        WHERE line.Id IS NULL OR v.Id IS NULL OR item.Id IS NULL OR rmap.Id IS NULL
                           OR rmap.InventoryCategoryId <> item.CategoryId OR rmap.ItemType <> item.ItemType
                           OR rmap.MovementReasonCode <> v.MovementReasonCode OR rmap.MovementReasonCode <> i.MovementReasonCode
                           OR rmap.Treatment <> i.Treatment OR rmap.IsActive = 0
                           OR rmap.EffectiveFromUtc > v.IssuedAtUtc OR (rmap.EffectiveToUtc IS NOT NULL AND rmap.EffectiveToUtc <= v.IssuedAtUtc)
                           OR v.FinancePostingEventId <> i.PostingEventId OR v.FinanceJournalEntryId <> i.JournalEntryId
                           OR pe.Id IS NULL OR pe.SourceDocumentType <> N'InventoryIssueVoucher' OR pe.SourceDocumentId <> v.Id
                           OR pe.JournalEntryId <> i.JournalEntryId OR pe.PostingStatus <> N'Posted'
                           OR j.Id IS NULL OR j.SourceDocumentType <> N'InventoryIssueVoucher' OR j.SourceDocumentId <> v.Id
                           OR j.PostingStatus <> N'Posted' OR j.IsBalanced = 0 OR j.TotalDebitAmount <> j.TotalCreditAmount OR j.BalanceDifference <> 0
                           OR (i.Treatment = 1 AND i.FixedAssetId IS NOT NULL)
                           OR (i.Treatment = 2 AND (fa.Id IS NULL OR fa.SourceDocumentType <> N'InventoryIssueVoucher'
                               OR fa.SourceDocumentId <> v.Id OR fa.SourceDocumentLineId <> line.Id))
                           OR (i.ReturnedQuantity = 0 AND i.Status <> 1)
                           OR (i.ReturnedQuantity > 0 AND i.ReturnedQuantity < i.IssuedQuantity AND i.Status <> 2)
                           OR (i.ReturnedQuantity = i.IssuedQuantity AND i.Status <> 3))
                        THROW 51845, 'INV_ISSUE_FINANCE_LINEAGE_INVALID: tenant, rule, posted journal, asset and return status must agree.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueReturnAllocations_Immutable]
                ON [dbo].[InventoryIssueReturnAllocations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51846, 'INV_RETURN_ALLOCATION_DELETE_BLOCKED: return-to-issue allocation is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.InventoryReturnVoucherLineId <> d.InventoryReturnVoucherLineId
                           OR i.InventoryIssueFinanceLineageId <> d.InventoryIssueFinanceLineageId
                           OR i.Quantity <> d.Quantity OR i.Value <> d.Value OR i.ReturnPostingEventId <> d.ReturnPostingEventId
                           OR i.ReturnJournalEntryId <> d.ReturnJournalEntryId OR i.PostedAtUtc <> d.PostedAtUtc
                           OR i.IntegrityHash <> d.IntegrityHash OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                           OR d.ReversalPostingEventId IS NOT NULL OR d.ReversalJournalEntryId IS NOT NULL OR d.ReversedAtUtc IS NOT NULL
                           OR i.ReversalPostingEventId IS NULL OR i.ReversalJournalEntryId IS NULL OR i.ReversedAtUtc IS NULL)
                        THROW 51847, 'INV_RETURN_ALLOCATION_IMMUTABLE: only one complete reversal binding is permitted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryReturnVoucherLines line ON line.Id = i.InventoryReturnVoucherLineId AND line.TenantId = i.TenantId AND line.IsDeleted = 0
                        LEFT JOIN InventoryReturnVouchers v ON v.Id = line.InventoryReturnVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        LEFT JOIN InventoryIssueFinanceLineages lineage ON lineage.Id = i.InventoryIssueFinanceLineageId AND lineage.TenantId = i.TenantId AND lineage.IsDeleted = 0
                        LEFT JOIN FinancePostingEvents pe ON pe.Id = i.ReturnPostingEventId AND pe.TenantId = i.TenantId AND pe.IsDeleted = 0
                        LEFT JOIN JournalEntries j ON j.Id = i.ReturnJournalEntryId AND j.TenantId = i.TenantId AND j.IsDeleted = 0
                        LEFT JOIN FinancePostingEvents rpe ON rpe.Id = i.ReversalPostingEventId AND rpe.TenantId = i.TenantId AND rpe.IsDeleted = 0
                        LEFT JOIN JournalEntries rj ON rj.Id = i.ReversalJournalEntryId AND rj.TenantId = i.TenantId AND rj.IsDeleted = 0
                        WHERE line.Id IS NULL OR v.Id IS NULL OR lineage.Id IS NULL OR v.Status NOT IN (4,5)
                           OR i.Quantity > lineage.IssuedQuantity
                           OR (i.ReversalPostingEventId IS NULL AND i.Quantity > lineage.ReturnedQuantity)
                           OR (SELECT COALESCE(SUM(a.Quantity),0) FROM InventoryIssueReturnAllocations a
                               WHERE a.InventoryIssueFinanceLineageId = lineage.Id AND a.TenantId = i.TenantId
                                 AND a.IsDeleted = 0 AND a.ReversalPostingEventId IS NULL) > lineage.ReturnedQuantity
                           OR i.Value <> ROUND((lineage.IssuedValue / lineage.IssuedQuantity) * i.Quantity, 2)
                           OR pe.Id IS NULL OR pe.SourceDocumentType <> N'InventoryReturnVoucher' OR pe.SourceDocumentId <> v.Id
                           OR pe.JournalEntryId <> i.ReturnJournalEntryId OR pe.PostingStatus <> N'Posted'
                           OR j.Id IS NULL OR j.SourceDocumentType <> N'InventoryReturnVoucher' OR j.SourceDocumentId <> v.Id
                           OR j.PostingStatus <> N'Posted' OR j.IsBalanced = 0 OR j.TotalDebitAmount <> j.TotalCreditAmount OR j.BalanceDifference <> 0
                           OR (i.ReversalPostingEventId IS NOT NULL AND (rpe.Id IS NULL OR rpe.SourceDocumentType <> N'InventoryReturnVoucher'
                               OR rpe.SourceDocumentId <> v.Id OR rpe.JournalEntryId <> i.ReversalJournalEntryId OR rpe.PostingStatus <> N'Posted'
                               OR rj.Id IS NULL OR rj.OriginalJournalEntryId <> i.ReturnJournalEntryId OR rj.PostingStatus <> N'Posted'
                               OR rj.IsBalanced = 0 OR rj.TotalDebitAmount <> rj.TotalCreditAmount OR rj.BalanceDifference <> 0)))
                        THROW 51848, 'INV_RETURN_ALLOCATION_INVALID: allocation must reference its posted balanced return or reversal lineage.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_GovernedRequisitionIssue]
                ON [dbo].[StockMovements]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE (i.MovementType = N'Issue' OR d.MovementType = N'Issue')
                          AND (i.TenantId <> d.TenantId OR i.InventoryItemId <> d.InventoryItemId OR i.WarehouseId <> d.WarehouseId
                               OR ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId,'00000000-0000-0000-0000-000000000000')
                               OR i.Quantity <> d.Quantity OR i.ReferenceType <> d.ReferenceType
                               OR ISNULL(i.ReferenceId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.ReferenceId,'00000000-0000-0000-0000-000000000000')
                               OR ISNULL(i.InventoryIssueVoucherId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.InventoryIssueVoucherId,'00000000-0000-0000-0000-000000000000')
                               OR ISNULL(i.ProcessedById,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProcessedById,'00000000-0000-0000-0000-000000000000')))
                        THROW 51631, 'INV_ISSUE_MOVEMENT_IMMUTABLE: requisition issue movement lineage and quantity are immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryIssueVouchers v ON v.Id = i.InventoryIssueVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        OUTER APPLY (
                            SELECT COALESCE(SUM(line.Quantity),0) Quantity
                            FROM InventoryIssueVoucherLines line
                            WHERE line.InventoryIssueVoucherId = i.InventoryIssueVoucherId AND line.TenantId = i.TenantId
                              AND line.InventoryItemId = i.InventoryItemId AND line.WarehouseId = i.WarehouseId
                              AND ISNULL(line.LocationId,'00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000')
                              AND line.IsDeleted = 0) allowed
                        OUTER APPLY (
                            SELECT COALESCE(SUM(ABS(movement.Quantity)),0) Quantity
                            FROM StockMovements movement WITH (UPDLOCK,HOLDLOCK)
                            WHERE movement.InventoryIssueVoucherId = i.InventoryIssueVoucherId AND movement.TenantId = i.TenantId
                              AND movement.InventoryItemId = i.InventoryItemId AND movement.WarehouseId = i.WarehouseId
                              AND ISNULL(movement.LocationId,'00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000')
                              AND movement.MovementType = N'Issue' AND movement.IsDeleted = 0) posted
                        WHERE i.IsDeleted = 0 AND i.MovementType = N'Issue'
                          AND (i.ReferenceType <> 11 OR i.ReferenceId IS NULL OR i.Quantity >= 0 OR v.Id IS NULL
                               OR v.InventoryRequisitionId <> i.ReferenceId OR v.WarehouseId <> i.WarehouseId
                               OR v.IssuedById <> i.ProcessedById OR v.FinancePostingEventId IS NULL OR v.FinanceJournalEntryId IS NULL
                               OR posted.Quantity > allowed.Quantity
                               OR EXISTS (
                                   SELECT 1 FROM InventoryIssueVoucherLines vl
                                   WHERE vl.InventoryIssueVoucherId = v.Id AND vl.TenantId = v.TenantId AND vl.IsDeleted = 0
                                     AND NOT EXISTS (
                                         SELECT 1 FROM InventoryIssueFinanceLineages fl
                                         WHERE fl.InventoryIssueVoucherLineId = vl.Id AND fl.TenantId = v.TenantId AND fl.IsDeleted = 0
                                           AND fl.PostingEventId = v.FinancePostingEventId AND fl.JournalEntryId = v.FinanceJournalEntryId))))
                        THROW 51632, 'INV_ISSUE_APPROVED_VOUCHER_REQUIRED: stock issue requires complete posted Finance/asset lineage.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryIssueReturnAllocations_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryIssueFinanceLineages_Lifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryIssueAccountingRules_TenantOwner];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_GovernedRequisitionIssue];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryIssueVouchers_ControlledLifecycle];");
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryIssueVouchers_FinanceLineage",
                table: "InventoryIssueVouchers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryIssueVouchers_MovementReason",
                table: "InventoryIssueVouchers");
            migrationBuilder.DropTable(
                name: "InventoryIssueReturnAllocations");

            migrationBuilder.DropTable(
                name: "InventoryIssueFinanceLineages");

            migrationBuilder.DropTable(
                name: "InventoryIssueAccountingRules");

            migrationBuilder.DropColumn(
                name: "FinanceJournalEntryId",
                table: "InventoryIssueVouchers");

            migrationBuilder.DropColumn(
                name: "FinancePostingEventId",
                table: "InventoryIssueVouchers");

            migrationBuilder.DropColumn(
                name: "MovementReasonCode",
                table: "InventoryIssueVouchers");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueVouchers_ControlledLifecycle]
                ON [dbo].[InventoryIssueVouchers]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51601, 'INV_ISSUE_VOUCHER_DELETE_BLOCKED: Store Issue Vouchers are immutable evidence.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.InventoryRequisitionId <> d.InventoryRequisitionId
                           OR i.VoucherNumber <> d.VoucherNumber OR i.WarehouseId <> d.WarehouseId
                           OR ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId,'00000000-0000-0000-0000-000000000000')
                           OR i.DepartmentId <> d.DepartmentId OR ISNULL(i.DepartmentName,N'') <> ISNULL(d.DepartmentName,N'')
                           OR ISNULL(i.CostCenter,N'') <> ISNULL(d.CostCenter,N'')
                           OR ISNULL(i.ProjectId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProjectId,'00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.ProjectCode,N'') <> ISNULL(d.ProjectCode,N'') OR i.RequestedById <> d.RequestedById
                           OR i.ApprovedById <> d.ApprovedById OR i.IssuedById <> d.IssuedById OR i.ReceiverUserId <> d.ReceiverUserId
                           OR i.IssuedAtUtc <> d.IssuedAtUtc OR i.IdempotencyKey <> d.IdempotencyKey
                           OR ISNULL(i.Notes,N'') <> ISNULL(d.Notes,N'') OR i.PayloadHash <> d.PayloadHash
                           OR i.CorrelationId <> d.CorrelationId OR i.SourceSnapshotJson <> d.SourceSnapshotJson
                           OR i.CreatedAt <> d.CreatedAt OR i.IsDeleted <> d.IsDeleted
                           OR NOT (d.Status = 1 AND i.Status = 2 AND d.AcknowledgedById IS NULL
                               AND i.AcknowledgedById = i.ReceiverUserId AND d.AcknowledgedAtUtc IS NULL
                               AND i.AcknowledgedAtUtc IS NOT NULL AND d.ReceiverComment IS NULL
                               AND LEN(LTRIM(RTRIM(i.ReceiverComment))) > 0 AND i.IntegrityHash <> d.IntegrityHash
                               AND i.UpdatedAt IS NOT NULL AND i.LastModifiedById = i.ReceiverUserId))
                        THROW 51602, 'INV_ISSUE_VOUCHER_IMMUTABLE: only the pending-to-acknowledged receiver transition is permitted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryRequisitions r ON r.Id = i.InventoryRequisitionId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                        WHERE r.Id IS NULL OR r.WarehouseId <> i.WarehouseId OR r.DepartmentId <> i.DepartmentId
                           OR r.RequestedById <> i.RequestedById OR r.ApprovedById <> i.ApprovedById OR r.Status NOT IN (3,4,5,6)
                           OR (i.ProjectId IS NULL AND (i.CostCenter IS NULL OR LEN(LTRIM(RTRIM(i.CostCenter))) = 0))
                           OR (i.LocationId IS NOT NULL AND (l.Id IS NULL OR l.WarehouseId <> i.WarehouseId)))
                        THROW 51603, 'INV_ISSUE_APPROVED_SOURCE_REQUIRED: voucher lineage must match an approved tenant requisition and cost object.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i WHERE EXISTS (
                            SELECT required.UserId FROM (VALUES (i.RequestedById),(i.ApprovedById),(i.IssuedById),(i.ReceiverUserId)) required(UserId)
                            WHERE NOT EXISTS (
                                SELECT 1 FROM UserTenants ut INNER JOIN Users u ON u.Id = ut.UserId AND u.IsActive = 1
                                WHERE ut.TenantId = i.TenantId AND ut.UserId = required.UserId AND ut.IsDeleted = 0
                                  AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME()))))
                        THROW 51604, 'INV_ISSUE_ACTOR_INVALID: all issue actors must be active users in the same tenant.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_GovernedRequisitionIssue]
                ON [dbo].[StockMovements]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE (i.MovementType = N'Issue' OR d.MovementType = N'Issue')
                          AND (i.TenantId <> d.TenantId OR i.InventoryItemId <> d.InventoryItemId OR i.WarehouseId <> d.WarehouseId
                               OR ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId,'00000000-0000-0000-0000-000000000000')
                               OR i.Quantity <> d.Quantity OR i.ReferenceType <> d.ReferenceType
                               OR ISNULL(i.ReferenceId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.ReferenceId,'00000000-0000-0000-0000-000000000000')
                               OR ISNULL(i.InventoryIssueVoucherId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.InventoryIssueVoucherId,'00000000-0000-0000-0000-000000000000')
                               OR ISNULL(i.ProcessedById,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProcessedById,'00000000-0000-0000-0000-000000000000')))
                        THROW 51631, 'INV_ISSUE_MOVEMENT_IMMUTABLE: requisition issue movement lineage and quantity are immutable.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryIssueVouchers v ON v.Id = i.InventoryIssueVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        OUTER APPLY (
                            SELECT COALESCE(SUM(line.Quantity),0) Quantity FROM InventoryIssueVoucherLines line
                            WHERE line.InventoryIssueVoucherId = i.InventoryIssueVoucherId AND line.TenantId = i.TenantId
                              AND line.InventoryItemId = i.InventoryItemId AND line.WarehouseId = i.WarehouseId
                              AND ISNULL(line.LocationId,'00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000')
                              AND line.IsDeleted = 0) allowed
                        OUTER APPLY (
                            SELECT COALESCE(SUM(ABS(movement.Quantity)),0) Quantity FROM StockMovements movement WITH (UPDLOCK,HOLDLOCK)
                            WHERE movement.InventoryIssueVoucherId = i.InventoryIssueVoucherId AND movement.TenantId = i.TenantId
                              AND movement.InventoryItemId = i.InventoryItemId AND movement.WarehouseId = i.WarehouseId
                              AND ISNULL(movement.LocationId,'00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000')
                              AND movement.MovementType = N'Issue' AND movement.IsDeleted = 0) posted
                        WHERE i.IsDeleted = 0 AND i.MovementType = N'Issue'
                          AND (i.ReferenceType <> 11 OR i.ReferenceId IS NULL OR i.Quantity >= 0 OR v.Id IS NULL
                               OR v.InventoryRequisitionId <> i.ReferenceId OR v.WarehouseId <> i.WarehouseId
                               OR v.IssuedById <> i.ProcessedById OR posted.Quantity > allowed.Quantity))
                        THROW 51632, 'INV_ISSUE_APPROVED_VOUCHER_REQUIRED: requisition issue stock requires matching controlled voucher quantity and issuer.', 1;
                END
                """);
        }
    }
}
