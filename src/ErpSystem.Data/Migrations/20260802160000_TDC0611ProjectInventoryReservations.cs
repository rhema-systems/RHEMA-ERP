using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class TDC0611ProjectInventoryReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("CorrelationId", "InventoryAllocations", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<Guid>("DepartmentId", "InventoryAllocations", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("IdempotencyKey", "InventoryAllocations", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<Guid>("InventoryRequisitionId", "InventoryAllocations", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("InventoryRequisitionItemId", "InventoryAllocations", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("PayloadHash", "InventoryAllocations", type: "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<Guid>("ProjectId", "InventoryAllocations", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<byte[]>("RowVersion", "InventoryAllocations", type: "rowversion", rowVersion: true, nullable: false);
        migrationBuilder.AddColumn<Guid>("SubstitutedFromAllocationId", "InventoryAllocations", type: "uniqueidentifier", nullable: true);

        migrationBuilder.CreateTable(
            name: "InventoryProjectReservationActions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InventoryAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                ActionType = table.Column<int>(type: "int", nullable: false),
                PreviousStatus = table.Column<int>(type: "int", nullable: true),
                NewStatus = table.Column<int>(type: "int", nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                PreviousInventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                NewInventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_InventoryProjectReservationActions", x => x.Id);
                table.CheckConstraint("CK_InventoryProjectReservationActions_Hashes", "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ([PreviousHash] IS NULL OR LEN([PreviousHash]) = 64)");
                table.CheckConstraint("CK_InventoryProjectReservationActions_Quantity", "[Quantity] >= 0");
                table.CheckConstraint("CK_InventoryProjectReservationActions_Sequence", "[Sequence] > 0");
                table.ForeignKey("FK_InventoryProjectReservationActions_InventoryAllocations_InventoryAllocationId", x => x.InventoryAllocationId, "InventoryAllocations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryProjectReservationActions_Notifications_NotificationId", x => x.NotificationId, "Notifications", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryProjectReservationActions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventoryProjectReservationActions_Users_ActorUserId", x => x.ActorUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_InventoryAllocations_InventoryRequisitionId", "InventoryAllocations", "InventoryRequisitionId");
        migrationBuilder.CreateIndex("IX_InventoryAllocations_InventoryRequisitionItemId", "InventoryAllocations", "InventoryRequisitionItemId");
        migrationBuilder.CreateIndex("IX_InventoryAllocations_ProjectId", "InventoryAllocations", "ProjectId");
        migrationBuilder.CreateIndex("IX_InventoryAllocations_SubstitutedFromAllocationId", "InventoryAllocations", "SubstitutedFromAllocationId", unique: true, filter: "[SubstitutedFromAllocationId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_InventoryAllocations_TenantId_DepartmentId_Status_ExpirationDate", "InventoryAllocations", new[] { "TenantId", "DepartmentId", "Status", "ExpirationDate" });
        migrationBuilder.CreateIndex("IX_InventoryAllocations_TenantId_IdempotencyKey", "InventoryAllocations", new[] { "TenantId", "IdempotencyKey" }, unique: true, filter: "[AllocationType] = 'ProjectRequisition' AND [IdempotencyKey] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_InventoryAllocations_TenantId_InventoryRequisitionItemId_Status", "InventoryAllocations", new[] { "TenantId", "InventoryRequisitionItemId", "Status" }, unique: true, filter: "[IsDeleted] = 0 AND [AllocationType] = 'ProjectRequisition' AND [Status] IN ('Active','PartiallyFulfilled')");
        migrationBuilder.CreateIndex("IX_InventoryAllocations_TenantId_ProjectId_Status_ExpirationDate", "InventoryAllocations", new[] { "TenantId", "ProjectId", "Status", "ExpirationDate" });
        migrationBuilder.CreateIndex("IX_InventoryProjectReservationActions_ActorUserId", "InventoryProjectReservationActions", "ActorUserId");
        migrationBuilder.CreateIndex("IX_InventoryProjectReservationActions_InventoryAllocationId", "InventoryProjectReservationActions", "InventoryAllocationId");
        migrationBuilder.CreateIndex("IX_InventoryProjectReservationActions_NotificationId", "InventoryProjectReservationActions", "NotificationId", unique: true, filter: "[NotificationId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_InventoryProjectReservationActions_TenantId_InventoryAllocationId_IdempotencyKey", "InventoryProjectReservationActions", new[] { "TenantId", "InventoryAllocationId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_InventoryProjectReservationActions_TenantId_InventoryAllocationId_Sequence", "InventoryProjectReservationActions", new[] { "TenantId", "InventoryAllocationId", "Sequence" }, unique: true);

        migrationBuilder.AddCheckConstraint("CK_InventoryAllocations_ProjectHashes", "InventoryAllocations", "[AllocationType] <> 'ProjectRequisition' OR (LEN([IdempotencyKey]) > 0 AND LEN([PayloadHash]) = 64 AND LEN([CorrelationId]) > 0)");
        migrationBuilder.AddCheckConstraint("CK_InventoryAllocations_ProjectLineage", "InventoryAllocations", "[AllocationType] <> 'ProjectRequisition' OR ([InventoryRequisitionId] IS NOT NULL AND [InventoryRequisitionItemId] IS NOT NULL AND [ProjectId] IS NOT NULL AND [DepartmentId] IS NOT NULL AND [LocationId] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint("CK_InventoryAllocations_Quantities", "InventoryAllocations", "[AllocationType] <> 'ProjectRequisition' OR ([AllocatedQuantity] > 0 AND [ConsumedQuantity] >= 0 AND [RemainingQuantity] >= 0 AND [ConsumedQuantity] + [RemainingQuantity] <= [AllocatedQuantity])");

        migrationBuilder.AddForeignKey("FK_InventoryAllocations_InventoryAllocations_SubstitutedFromAllocationId", "InventoryAllocations", "SubstitutedFromAllocationId", "InventoryAllocations", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_InventoryAllocations_InventoryRequisitionItems_InventoryRequisitionItemId", "InventoryAllocations", "InventoryRequisitionItemId", "InventoryRequisitionItems", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_InventoryAllocations_InventoryRequisitions_InventoryRequisitionId", "InventoryAllocations", "InventoryRequisitionId", "InventoryRequisitions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_InventoryAllocations_Projects_ProjectId", "InventoryAllocations", "ProjectId", "Projects", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryProjectReservationActions_Immutable]
            ON [dbo].[InventoryProjectReservationActions]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51070, 'INV_PROJECT_RESERVATION_ACTION_IMMUTABLE: reservation actions are append-only.', 1;
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_InventoryAllocations_ProjectReservationGuard]
            ON [dbo].[InventoryAllocations]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM deleted d
                    LEFT JOIN inserted i ON i.[Id] = d.[Id]
                    WHERE d.[AllocationType] = 'ProjectRequisition' AND i.[Id] IS NULL
                )
                    THROW 51071, 'INV_PROJECT_RESERVATION_DELETE_PROHIBITED: project reservations cannot be deleted.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE d.[AllocationType] = 'ProjectRequisition'
                      AND (
                        i.[AllocationType] <> d.[AllocationType] OR i.[TenantId] <> d.[TenantId]
                        OR i.[InventoryItemId] <> d.[InventoryItemId] OR i.[WarehouseId] <> d.[WarehouseId]
                        OR ISNULL(i.[LocationId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[LocationId], '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.[InventoryRequisitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[InventoryRequisitionId], '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.[InventoryRequisitionItemId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[InventoryRequisitionItemId], '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.[ProjectId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[ProjectId], '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.[DepartmentId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[DepartmentId], '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.[SubstitutedFromAllocationId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SubstitutedFromAllocationId], '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.[ReferenceId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[ReferenceId], '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.[ReferenceNumber], '') <> ISNULL(d.[ReferenceNumber], '')
                        OR ISNULL(i.[AllocatedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[AllocatedById], '00000000-0000-0000-0000-000000000000')
                        OR i.[AllocatedQuantity] <> d.[AllocatedQuantity] OR i.[AllocationDate] <> d.[AllocationDate]
                        OR ISNULL(i.[RequiredDate], '19000101') <> ISNULL(d.[RequiredDate], '19000101')
                        OR ISNULL(i.[ExpirationDate], '19000101') <> ISNULL(d.[ExpirationDate], '19000101')
                        OR ISNULL(i.[IdempotencyKey], '') <> ISNULL(d.[IdempotencyKey], '')
                        OR ISNULL(i.[PayloadHash], '') <> ISNULL(d.[PayloadHash], '')
                        OR ISNULL(i.[CorrelationId], '') <> ISNULL(d.[CorrelationId], '')
                      )
                )
                    THROW 51072, 'INV_PROJECT_RESERVATION_LINEAGE_IMMUTABLE: project reservation source and quantity lineage cannot be changed.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE d.[AllocationType] = 'ProjectRequisition'
                      AND NOT (
                        (d.[Status] = 'Active' AND i.[Status] IN ('Active','PartiallyFulfilled','Consumed','Cancelled','Expired','Substituted'))
                        OR (d.[Status] = 'PartiallyFulfilled' AND i.[Status] IN ('PartiallyFulfilled','Consumed','Cancelled','Expired'))
                        OR (d.[Status] IN ('Consumed','Cancelled','Expired','Substituted') AND i.[Status] = d.[Status])
                      )
                )
                    THROW 51073, 'INV_PROJECT_RESERVATION_TRANSITION_INVALID: project reservation status transition is not allowed.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE i.[AllocationType] = 'ProjectRequisition'
                      AND (
                        i.[ConsumedQuantity] < 0 OR i.[RemainingQuantity] < 0
                        OR i.[ConsumedQuantity] + i.[RemainingQuantity] > i.[AllocatedQuantity]
                        OR (d.[Id] IS NOT NULL AND i.[ConsumedQuantity] < d.[ConsumedQuantity])
                        OR (d.[Id] IS NOT NULL AND i.[RemainingQuantity] > d.[RemainingQuantity])
                        OR (i.[Status] = 'Active' AND (i.[ConsumedQuantity] <> 0 OR i.[RemainingQuantity] <= 0))
                        OR (i.[Status] = 'PartiallyFulfilled' AND (i.[ConsumedQuantity] <= 0 OR i.[RemainingQuantity] <= 0))
                        OR (i.[Status] = 'Consumed' AND (i.[ConsumedQuantity] <= 0 OR i.[RemainingQuantity] <> 0))
                        OR (i.[Status] IN ('Cancelled','Expired','Substituted') AND i.[RemainingQuantity] <> 0)
                        OR i.[Status] NOT IN ('Active','PartiallyFulfilled','Consumed','Cancelled','Expired','Substituted')
                      )
                )
                    THROW 51074, 'INV_PROJECT_RESERVATION_QUANTITY_INVALID: project reservation quantities and status are inconsistent.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryAllocations_ProjectReservationGuard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryProjectReservationActions_Immutable];");

        migrationBuilder.DropForeignKey("FK_InventoryAllocations_InventoryAllocations_SubstitutedFromAllocationId", "InventoryAllocations");
        migrationBuilder.DropForeignKey("FK_InventoryAllocations_InventoryRequisitionItems_InventoryRequisitionItemId", "InventoryAllocations");
        migrationBuilder.DropForeignKey("FK_InventoryAllocations_InventoryRequisitions_InventoryRequisitionId", "InventoryAllocations");
        migrationBuilder.DropForeignKey("FK_InventoryAllocations_Projects_ProjectId", "InventoryAllocations");
        migrationBuilder.DropTable("InventoryProjectReservationActions");

        migrationBuilder.DropIndex("IX_InventoryAllocations_InventoryRequisitionId", "InventoryAllocations");
        migrationBuilder.DropIndex("IX_InventoryAllocations_InventoryRequisitionItemId", "InventoryAllocations");
        migrationBuilder.DropIndex("IX_InventoryAllocations_ProjectId", "InventoryAllocations");
        migrationBuilder.DropIndex("IX_InventoryAllocations_SubstitutedFromAllocationId", "InventoryAllocations");
        migrationBuilder.DropIndex("IX_InventoryAllocations_TenantId_DepartmentId_Status_ExpirationDate", "InventoryAllocations");
        migrationBuilder.DropIndex("IX_InventoryAllocations_TenantId_IdempotencyKey", "InventoryAllocations");
        migrationBuilder.DropIndex("IX_InventoryAllocations_TenantId_InventoryRequisitionItemId_Status", "InventoryAllocations");
        migrationBuilder.DropIndex("IX_InventoryAllocations_TenantId_ProjectId_Status_ExpirationDate", "InventoryAllocations");
        migrationBuilder.DropCheckConstraint("CK_InventoryAllocations_ProjectHashes", "InventoryAllocations");
        migrationBuilder.DropCheckConstraint("CK_InventoryAllocations_ProjectLineage", "InventoryAllocations");
        migrationBuilder.DropCheckConstraint("CK_InventoryAllocations_Quantities", "InventoryAllocations");

        migrationBuilder.DropColumn("CorrelationId", "InventoryAllocations");
        migrationBuilder.DropColumn("DepartmentId", "InventoryAllocations");
        migrationBuilder.DropColumn("IdempotencyKey", "InventoryAllocations");
        migrationBuilder.DropColumn("InventoryRequisitionId", "InventoryAllocations");
        migrationBuilder.DropColumn("InventoryRequisitionItemId", "InventoryAllocations");
        migrationBuilder.DropColumn("PayloadHash", "InventoryAllocations");
        migrationBuilder.DropColumn("ProjectId", "InventoryAllocations");
        migrationBuilder.DropColumn("RowVersion", "InventoryAllocations");
        migrationBuilder.DropColumn("SubstitutedFromAllocationId", "InventoryAllocations");
    }
}
