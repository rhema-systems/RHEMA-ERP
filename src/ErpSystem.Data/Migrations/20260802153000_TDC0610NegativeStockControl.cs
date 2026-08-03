using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260802153000_TDC0610NegativeStockControl")]
public partial class TDC0610NegativeStockControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InventoryNegativeStockOverrides",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferenceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReferenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                AuthorizedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                DecisionSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ConsumedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ConsumedByReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ConsumptionTransactionId = table.Column<long>(type: "bigint", nullable: true),
                IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryNegativeStockOverrides", x => x.Id);
                table.CheckConstraint("CK_InventoryNegativeStockOverrides_Consumption", "([ConsumedAtUtc] IS NULL AND [ConsumedByUserId] IS NULL AND [ConsumedByReferenceId] IS NULL AND [ConsumptionTransactionId] IS NULL) OR ([ConsumedAtUtc] IS NOT NULL AND [ConsumedByUserId] IS NOT NULL AND [ConsumedByReferenceId] IS NOT NULL AND [ConsumptionTransactionId] IS NOT NULL)");
                table.CheckConstraint("CK_InventoryNegativeStockOverrides_Expiry", "[ExpiresAtUtc] > [ApprovedAtUtc]");
                table.CheckConstraint("CK_InventoryNegativeStockOverrides_Integrity", "LEN([IntegrityHash]) = 64 AND LEN([DecisionSnapshotHash]) = 64");
                table.CheckConstraint("CK_InventoryNegativeStockOverrides_Quantity", "[AuthorizedQuantity] > 0");
                table.ForeignKey("FK_InventoryNegativeStockOverrides_CentralDocumentVersions_CentralDocumentVersionId", x => x.CentralDocumentVersionId, "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_FileUploadRecords_FileUploadRecordId", x => x.FileUploadRecordId, "FileUploadRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_InventoryItems_InventoryItemId", x => x.InventoryItemId, "InventoryItems", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_ProcurementConfigurationDecisions_ConfigurationDecisionId", x => x.ConfigurationDecisionId, "ProcurementConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_ProcurementConfigurationProfiles_ConfigurationProfileId", x => x.ConfigurationProfileId, "ProcurementConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_WarehouseLocations_LocationId", x => x.LocationId, "WarehouseLocations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_Warehouses_WarehouseId", x => x.WarehouseId, "Warehouses", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryNegativeStockOverrides_WorkflowInstances_WorkflowInstanceId", x => x.WorkflowInstanceId, "WorkflowInstances", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_CentralDocumentVersionId", "InventoryNegativeStockOverrides", "CentralDocumentVersionId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_ConfigurationDecisionId", "InventoryNegativeStockOverrides", "ConfigurationDecisionId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_ConfigurationProfileId", "InventoryNegativeStockOverrides", "ConfigurationProfileId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_FileUploadRecordId", "InventoryNegativeStockOverrides", "FileUploadRecordId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_InventoryItemId", "InventoryNegativeStockOverrides", "InventoryItemId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_LocationId", "InventoryNegativeStockOverrides", "LocationId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_WarehouseId", "InventoryNegativeStockOverrides", "WarehouseId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_WorkflowInstanceId", "InventoryNegativeStockOverrides", "WorkflowInstanceId");
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_TenantId_WorkflowInstanceId", "InventoryNegativeStockOverrides", new[] { "TenantId", "WorkflowInstanceId" }, unique: true);
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_TenantId_ReferenceId_ReferenceLineId", "InventoryNegativeStockOverrides", new[] { "TenantId", "ReferenceId", "ReferenceLineId" });
        migrationBuilder.CreateIndex("IX_InventoryNegativeStockOverrides_TenantId_InventoryItemId_WarehouseId_ExpiresAtUtc", "InventoryNegativeStockOverrides", new[] { "TenantId", "InventoryItemId", "WarehouseId", "ExpiresAtUtc" });

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_WarehouseQuantities_NegativeStockGuard]
            ON [dbo].[WarehouseQuantities]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF NOT EXISTS (SELECT 1 FROM inserted WHERE [CurrentStock] < 0 OR [AvailableStock] < 0 OR [AllocatedStock] < 0)
                    RETURN;

                -- Allocation represents a reservation, never physical stock. An emergency
                -- DEC-010 override can authorize a physical shortage but cannot authorize
                -- a negative reservation balance.
                IF EXISTS (SELECT 1 FROM inserted WHERE [AllocatedStock] < 0)
                    THROW 51064, 'INV_NEGATIVE_STOCK_ALLOCATION_PROHIBITED: allocated stock cannot be negative.', 1;

                DECLARE @overrideId uniqueidentifier = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0610_OVERRIDE_ID'));
                DECLARE @contextTransactionId bigint = TRY_CONVERT(bigint, SESSION_CONTEXT(N'TDC0610_TRANSACTION_ID'));
                DECLARE @currentTransactionId bigint = CONVERT(bigint, CURRENT_TRANSACTION_ID());

                IF @overrideId IS NULL OR @contextTransactionId IS NULL OR @contextTransactionId <> @currentTransactionId
                    THROW 51060, 'INV_NEGATIVE_STOCK_SQL_PROHIBITED: transaction-bound DEC-010 override context is required.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE (i.[CurrentStock] < 0 OR i.[AvailableStock] < 0)
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [dbo].[InventoryNegativeStockOverrides] o
                          WHERE o.[Id] = @overrideId
                            AND o.[TenantId] = i.[TenantId]
                            AND o.[InventoryItemId] = i.[InventoryItemId]
                            AND o.[WarehouseId] = i.[WarehouseId]
                            AND o.[IsDeleted] = 0
                            AND o.[ConsumedAtUtc] IS NOT NULL
                            AND o.[ConsumedByReferenceId] = o.[ReferenceId]
                            AND o.[ConsumptionTransactionId] = @currentTransactionId
                            AND o.[ConsumedAtUtc] <= o.[ExpiresAtUtc]
                            AND (d.[Id] IS NOT NULL)
                            AND (d.[CurrentStock] - i.[CurrentStock]) <= o.[AuthorizedQuantity]
                            AND (d.[AvailableStock] - i.[AvailableStock]) <= o.[AuthorizedQuantity]
                      )
                )
                    THROW 51061, 'INV_NEGATIVE_STOCK_SQL_SCOPE_INVALID: DEC-010 override does not cover the exact tenant warehouse item and decrement.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryItems_NegativeStockGuard]
            ON [dbo].[InventoryItems]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF NOT EXISTS (SELECT 1 FROM inserted WHERE [CurrentStock] < 0 OR [AvailableStock] < 0 OR [AllocatedStock] < 0)
                    RETURN;

                IF EXISTS (SELECT 1 FROM inserted WHERE [AllocatedStock] < 0)
                    THROW 51065, 'INV_NEGATIVE_STOCK_ITEM_ALLOCATION_PROHIBITED: allocated stock cannot be negative.', 1;

                DECLARE @overrideId uniqueidentifier = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0610_OVERRIDE_ID'));
                DECLARE @contextTransactionId bigint = TRY_CONVERT(bigint, SESSION_CONTEXT(N'TDC0610_TRANSACTION_ID'));
                DECLARE @currentTransactionId bigint = CONVERT(bigint, CURRENT_TRANSACTION_ID());

                IF @overrideId IS NULL OR @contextTransactionId IS NULL OR @contextTransactionId <> @currentTransactionId
                    THROW 51062, 'INV_NEGATIVE_STOCK_ITEM_SQL_PROHIBITED: transaction-bound DEC-010 override context is required.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE (i.[CurrentStock] < 0 OR i.[AvailableStock] < 0)
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [dbo].[InventoryNegativeStockOverrides] o
                          WHERE o.[Id] = @overrideId
                            AND o.[TenantId] = i.[TenantId]
                            AND o.[InventoryItemId] = i.[Id]
                            AND o.[IsDeleted] = 0
                            AND o.[ConsumedAtUtc] IS NOT NULL
                            AND o.[ConsumedByReferenceId] = o.[ReferenceId]
                            AND o.[ConsumptionTransactionId] = @currentTransactionId
                            AND o.[ConsumedAtUtc] <= o.[ExpiresAtUtc]
                            AND (d.[Id] IS NOT NULL)
                            AND (d.[CurrentStock] - i.[CurrentStock]) <= o.[AuthorizedQuantity]
                            AND (d.[AvailableStock] - i.[AvailableStock]) <= o.[AuthorizedQuantity]
                      )
                )
                    THROW 51063, 'INV_NEGATIVE_STOCK_ITEM_SQL_SCOPE_INVALID: DEC-010 override does not cover the exact tenant item decrement.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryItems_NegativeStockGuard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_WarehouseQuantities_NegativeStockGuard];");
        migrationBuilder.DropTable(name: "InventoryNegativeStockOverrides");
    }
}
