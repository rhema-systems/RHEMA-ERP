using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class TDC0612InventoryReplenishment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InventoryReplenishmentRecommendations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecommendationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                WarehouseQuantityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ItemSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PreferredSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                DemandWindowDays = table.Column<int>(type: "int", nullable: false),
                DemandFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                DemandToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                DemandQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                AverageDailyDemand = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                SafetyLeadTimeDays = table.Column<int>(type: "int", nullable: false),
                CurrentStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                AvailableStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                AllocatedStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                OnOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                OpenRecommendationQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                MinimumLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                MaximumLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                ReorderLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                ReorderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                SafetyStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                MinimumOrderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                OrderMultiple = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                LeadTimeDemand = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                ProjectedAvailableAtReceipt = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                RecommendedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                EstimatedUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RequiredDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ValidUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                CalculationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CalculationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                GeneratedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                AlertNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DecisionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                PurchaseRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PurchaseRequisitionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ConvertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                table.PrimaryKey("PK_InventoryReplenishmentRecommendations", value => value.Id);
                table.CheckConstraint("CK_InventoryReplenishment_Dates", "[RequiredDateUtc] >= [GeneratedAtUtc] AND [ValidUntilUtc] > [GeneratedAtUtc]");
                table.CheckConstraint("CK_InventoryReplenishment_DemandWindow", "[DemandWindowDays] BETWEEN 1 AND 730 AND [DemandFromUtc] < [DemandToUtc]");
                table.CheckConstraint("CK_InventoryReplenishment_Hashes", "LEN([CalculationHash]) = 64 AND LEN([PayloadHash]) = 64 AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0");
                table.CheckConstraint("CK_InventoryReplenishment_Levels", "[MinimumLevel] >= 0 AND [MaximumLevel] >= 0 AND [ReorderLevel] >= 0 AND [ReorderQuantity] >= 0 AND [SafetyStock] >= 0 AND ([MaximumLevel] = 0 OR [MaximumLevel] >= [MinimumLevel])");
                table.CheckConstraint("CK_InventoryReplenishment_Quantities", "[AllocatedStock] >= 0 AND [OnOrderQuantity] >= 0 AND [OpenRecommendationQuantity] >= 0 AND [RecommendedQuantity] > 0 AND [MinimumOrderQuantity] > 0 AND [OrderMultiple] > 0");
                table.CheckConstraint("CK_InventoryReplenishment_Status", "[Status] BETWEEN 1 AND 7");
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_InventoryItems_InventoryItemId", value => value.InventoryItemId, "InventoryItems", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_ItemSuppliers_ItemSupplierId", value => value.ItemSupplierId, "ItemSuppliers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_Notifications_AlertNotificationId", value => value.AlertNotificationId, "Notifications", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_PurchaseRequisitions_PurchaseRequisitionId", value => value.PurchaseRequisitionId, "PurchaseRequisitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_Tenants_TenantId", value => value.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_Users_DecidedById", value => value.DecidedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_Users_GeneratedById", value => value.GeneratedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_Users_SubmittedById", value => value.SubmittedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_WarehouseQuantities_WarehouseQuantityId", value => value.WarehouseQuantityId, "WarehouseQuantities", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentRecommendations_Warehouses_WarehouseId", value => value.WarehouseId, "Warehouses", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InventoryReplenishmentActions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecommendationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                ActionType = table.Column<int>(type: "int", nullable: false),
                PreviousStatus = table.Column<int>(type: "int", nullable: true),
                NewStatus = table.Column<int>(type: "int", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                table.PrimaryKey("PK_InventoryReplenishmentActions", value => value.Id);
                table.CheckConstraint("CK_InventoryReplenishmentActions_Hashes", "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ([PreviousHash] IS NULL OR LEN([PreviousHash]) = 64)");
                table.CheckConstraint("CK_InventoryReplenishmentActions_Sequence", "[Sequence] > 0");
                table.ForeignKey("FK_InventoryReplenishmentActions_InventoryReplenishmentRecommendations_RecommendationId", value => value.RecommendationId, "InventoryReplenishmentRecommendations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentActions_Tenants_TenantId", value => value.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryReplenishmentActions_Users_ActorUserId", value => value.ActorUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_InventoryReplenishmentActions_ActorUserId", "InventoryReplenishmentActions", "ActorUserId");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentActions_RecommendationId", "InventoryReplenishmentActions", "RecommendationId");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentActions_TenantId_RecommendationId_IdempotencyKey", "InventoryReplenishmentActions", new[] { "TenantId", "RecommendationId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentActions_TenantId_RecommendationId_Sequence", "InventoryReplenishmentActions", new[] { "TenantId", "RecommendationId", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_AlertNotificationId", "InventoryReplenishmentRecommendations", "AlertNotificationId", unique: true, filter: "[AlertNotificationId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_DecidedById", "InventoryReplenishmentRecommendations", "DecidedById");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_GeneratedById", "InventoryReplenishmentRecommendations", "GeneratedById");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_InventoryItemId", "InventoryReplenishmentRecommendations", "InventoryItemId");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_ItemSupplierId", "InventoryReplenishmentRecommendations", "ItemSupplierId");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_PurchaseRequisitionId", "InventoryReplenishmentRecommendations", "PurchaseRequisitionId", unique: true, filter: "[PurchaseRequisitionId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_SubmittedById", "InventoryReplenishmentRecommendations", "SubmittedById");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_TenantId_IdempotencyKey", "InventoryReplenishmentRecommendations", new[] { "TenantId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_TenantId_RecommendationNumber", "InventoryReplenishmentRecommendations", new[] { "TenantId", "RecommendationNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_TenantId_Status_ValidUntilUtc", "InventoryReplenishmentRecommendations", new[] { "TenantId", "Status", "ValidUntilUtc" });
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_TenantId_WarehouseId_InventoryItemId_Status", "InventoryReplenishmentRecommendations", new[] { "TenantId", "WarehouseId", "InventoryItemId", "Status" }, unique: true, filter: "[IsDeleted] = 0 AND [Status] IN (1,2,3)");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_WarehouseId", "InventoryReplenishmentRecommendations", "WarehouseId");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_WarehouseQuantityId", "InventoryReplenishmentRecommendations", "WarehouseQuantityId");
        migrationBuilder.CreateIndex("IX_InventoryReplenishmentRecommendations_WorkflowInstanceId", "InventoryReplenishmentRecommendations", "WorkflowInstanceId", unique: true, filter: "[WorkflowInstanceId] IS NOT NULL");

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryReplenishmentActions_Immutable]
            ON [dbo].[InventoryReplenishmentActions]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51080, 'INV_REPLENISHMENT_ACTION_IMMUTABLE', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryReplenishmentRecommendations_Guard]
            ON [dbo].[InventoryReplenishmentRecommendations]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 51081, 'INV_REPLENISHMENT_DELETE_PROHIBITED', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    LEFT JOIN deleted d ON d.Id = i.Id
                    WHERE (d.Id IS NULL AND i.Status <> 1)
                       OR (d.Id IS NOT NULL AND (
                            i.TenantId <> d.TenantId OR i.RecommendationNumber <> d.RecommendationNumber
                            OR i.WarehouseQuantityId <> d.WarehouseQuantityId OR i.WarehouseId <> d.WarehouseId
                            OR i.InventoryItemId <> d.InventoryItemId
                            OR ISNULL(i.ItemSupplierId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ItemSupplierId, '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(i.PreferredSupplierId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.PreferredSupplierId, '00000000-0000-0000-0000-000000000000')
                            OR i.DemandWindowDays <> d.DemandWindowDays OR i.DemandFromUtc <> d.DemandFromUtc OR i.DemandToUtc <> d.DemandToUtc
                            OR i.DemandQuantity <> d.DemandQuantity OR i.AverageDailyDemand <> d.AverageDailyDemand
                            OR i.LeadTimeDays <> d.LeadTimeDays OR i.SafetyLeadTimeDays <> d.SafetyLeadTimeDays
                            OR i.CurrentStock <> d.CurrentStock OR i.AvailableStock <> d.AvailableStock OR i.AllocatedStock <> d.AllocatedStock
                            OR i.OnOrderQuantity <> d.OnOrderQuantity OR i.OpenRecommendationQuantity <> d.OpenRecommendationQuantity
                            OR i.MinimumLevel <> d.MinimumLevel OR i.MaximumLevel <> d.MaximumLevel OR i.ReorderLevel <> d.ReorderLevel
                            OR i.ReorderQuantity <> d.ReorderQuantity OR i.SafetyStock <> d.SafetyStock
                            OR i.MinimumOrderQuantity <> d.MinimumOrderQuantity OR i.OrderMultiple <> d.OrderMultiple
                            OR i.LeadTimeDemand <> d.LeadTimeDemand OR i.ProjectedAvailableAtReceipt <> d.ProjectedAvailableAtReceipt
                            OR i.RecommendedQuantity <> d.RecommendedQuantity OR i.EstimatedUnitCost <> d.EstimatedUnitCost
                            OR i.RequiredDateUtc <> d.RequiredDateUtc OR i.ValidUntilUtc <> d.ValidUntilUtc
                            OR i.Explanation <> d.Explanation OR i.CalculationSnapshotJson <> d.CalculationSnapshotJson
                            OR i.CalculationHash <> d.CalculationHash OR i.GeneratedById <> d.GeneratedById OR i.GeneratedAtUtc <> d.GeneratedAtUtc
                            OR i.IdempotencyKey <> d.IdempotencyKey OR i.PayloadHash <> d.PayloadHash OR i.IsDeleted <> d.IsDeleted
                       )))
                    THROW 51082, 'INV_REPLENISHMENT_CALCULATION_IMMUTABLE', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE i.Status <> d.Status AND NOT (
                        (d.Status = 1 AND i.Status IN (2,6,7)) OR
                        (d.Status = 2 AND i.Status IN (3,4,6,7)) OR
                        (d.Status = 3 AND i.Status IN (5,6,7))))
                    THROW 51083, 'INV_REPLENISHMENT_TRANSITION_INVALID', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE (i.Status = 1 AND (i.WorkflowInstanceId IS NOT NULL OR i.SubmittedById IS NOT NULL OR i.DecidedById IS NOT NULL OR i.PurchaseRequisitionId IS NOT NULL))
                       OR (i.Status = 2 AND (i.WorkflowInstanceId IS NULL OR i.SubmittedById IS NULL OR i.SubmittedAtUtc IS NULL OR i.DecidedById IS NOT NULL OR i.PurchaseRequisitionId IS NOT NULL))
                       OR (i.Status IN (3,4) AND (i.WorkflowInstanceId IS NULL OR i.SubmittedById IS NULL OR i.SubmittedAtUtc IS NULL OR i.DecidedById IS NULL OR i.DecidedAtUtc IS NULL OR i.PurchaseRequisitionId IS NOT NULL))
                       OR (i.Status = 5 AND (i.WorkflowInstanceId IS NULL OR i.DecidedById IS NULL OR i.PurchaseRequisitionId IS NULL OR i.PurchaseRequisitionNumber IS NULL OR i.ConvertedAtUtc IS NULL)))
                    THROW 51084, 'INV_REPLENISHMENT_LIFECYCLE_INVALID', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.DecidedById IS NOT NULL AND (i.DecidedById = i.GeneratedById OR i.DecidedById = i.SubmittedById))
                    THROW 51085, 'INV_REPLENISHMENT_INDEPENDENT_DECISION_REQUIRED', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                    WHERE (d.AlertNotificationId IS NOT NULL AND ISNULL(i.AlertNotificationId, '00000000-0000-0000-0000-000000000000') <> d.AlertNotificationId)
                       OR (d.WorkflowInstanceId IS NOT NULL AND ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000') <> d.WorkflowInstanceId)
                       OR (d.SubmittedById IS NOT NULL AND (ISNULL(i.SubmittedById, '00000000-0000-0000-0000-000000000000') <> d.SubmittedById OR ISNULL(i.SubmittedAtUtc, '19000101') <> d.SubmittedAtUtc))
                       OR (d.DecidedById IS NOT NULL AND (ISNULL(i.DecidedById, '00000000-0000-0000-0000-000000000000') <> d.DecidedById OR ISNULL(i.DecidedAtUtc, '19000101') <> d.DecidedAtUtc OR ISNULL(i.DecisionComment, '') <> ISNULL(d.DecisionComment, '')))
                       OR (d.PurchaseRequisitionId IS NOT NULL AND (ISNULL(i.PurchaseRequisitionId, '00000000-0000-0000-0000-000000000000') <> d.PurchaseRequisitionId OR ISNULL(i.PurchaseRequisitionNumber, '') <> ISNULL(d.PurchaseRequisitionNumber, '') OR ISNULL(i.ConvertedAtUtc, '19000101') <> d.ConvertedAtUtc)))
                    THROW 51086, 'INV_REPLENISHMENT_ESTABLISHED_LINEAGE_IMMUTABLE', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryReplenishmentActions_Immutable];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryReplenishmentRecommendations_Guard];");
        migrationBuilder.DropTable(name: "InventoryReplenishmentActions");
        migrationBuilder.DropTable(name: "InventoryReplenishmentRecommendations");
    }
}
