using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class INVREQFU002MaintenanceReservationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderParts_InventoryAllocations_AllocationId",
                table: "WorkOrderParts");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderParts_TenantId",
                table: "WorkOrderParts");

            migrationBuilder.CreateTable(
                name: "InventoryWorkOrderReservationActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderPartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PreviousRequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewRequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_InventoryWorkOrderReservationActions", x => x.Id);
                    table.CheckConstraint(
                        name: "CK_InventoryWorkOrderReservationActions_Hashes",
                        sql: "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ([PreviousHash] IS NULL OR LEN([PreviousHash]) = 64)");
                    table.CheckConstraint(
                        name: "CK_InventoryWorkOrderReservationActions_Quantity",
                        sql: "[Quantity] >= 0");
                    table.CheckConstraint(
                        name: "CK_InventoryWorkOrderReservationActions_Sequence",
                        sql: "[Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryWorkOrderReservationActions_InventoryAllocations_InventoryAllocationId",
                        column: x => x.InventoryAllocationId,
                        principalTable: "InventoryAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryWorkOrderReservationActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryWorkOrderReservationActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryWorkOrderReservationActions_WorkOrderParts_WorkOrderPartId",
                        column: x => x.WorkOrderPartId,
                        principalTable: "WorkOrderParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_TenantId_AllocationId",
                table: "WorkOrderParts",
                columns: new[] { "TenantId", "AllocationId" },
                unique: true,
                filter: "[AllocationId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWorkOrderReservationActions_ActorUserId",
                table: "InventoryWorkOrderReservationActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWorkOrderReservationActions_InventoryAllocationId",
                table: "InventoryWorkOrderReservationActions",
                column: "InventoryAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWorkOrderReservationActions_TenantId",
                table: "InventoryWorkOrderReservationActions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWorkOrderReservationActions_WorkOrderPartId",
                table: "InventoryWorkOrderReservationActions",
                column: "WorkOrderPartId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWorkOrderReservationActions_TenantId_InventoryAllocationId_IdempotencyKey",
                table: "InventoryWorkOrderReservationActions",
                columns: new[] { "TenantId", "InventoryAllocationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWorkOrderReservationActions_TenantId_InventoryAllocationId_Sequence",
                table: "InventoryWorkOrderReservationActions",
                columns: new[] { "TenantId", "InventoryAllocationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWorkOrderReservationActions_TenantId_WorkOrderPartId_OccurredAtUtc",
                table: "InventoryWorkOrderReservationActions",
                columns: new[] { "TenantId", "WorkOrderPartId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocations_TenantId_WorkOrderIdempotencyKey",
                table: "InventoryAllocations",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[AllocationType] = 'WorkOrder' AND [IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocations_TenantId_WorkOrder_Status_RequiredDate",
                table: "InventoryAllocations",
                columns: new[] { "TenantId", "ReferenceId", "Status", "RequiredDate" },
                filter: "[AllocationType] = 'WorkOrder'");

            // Rows written by the former maintenance services did not carry governance hashes.
            // Preserve those historical rows, but require every newly governed WorkOrder row to
            // carry complete exact-location and idempotency lineage. The insert trigger below
            // prevents creation of any new legacy-shaped row.
            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryAllocations_WorkOrderLineage",
                table: "InventoryAllocations",
                sql: "[AllocationType] <> 'WorkOrder' OR [IdempotencyKey] IS NULL OR ([ReferenceId] IS NOT NULL AND [LocationId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryAllocations_WorkOrderHashes",
                table: "InventoryAllocations",
                sql: "[AllocationType] <> 'WorkOrder' OR [IdempotencyKey] IS NULL OR (LEN([IdempotencyKey]) > 0 AND LEN([PayloadHash]) = 64 AND LEN([CorrelationId]) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryAllocations_WorkOrderQuantities",
                table: "InventoryAllocations",
                sql: "[AllocationType] <> 'WorkOrder' OR ([AllocatedQuantity] > 0 AND [ConsumedQuantity] >= 0 AND [RemainingQuantity] >= 0 AND [ConsumedQuantity] + [RemainingQuantity] <= [AllocatedQuantity])");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderParts_InventoryAllocations_AllocationId",
                table: "WorkOrderParts",
                column: "AllocationId",
                principalTable: "InventoryAllocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryWorkOrderReservationActions_Immutable]
                ON [dbo].[InventoryWorkOrderReservationActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51950, 'INV_WORK_ORDER_RESERVATION_ACTION_IMMUTABLE: reservation actions are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[InventoryAllocations] allocation
                          ON allocation.[Id] = i.[InventoryAllocationId]
                         AND allocation.[TenantId] = i.[TenantId]
                         AND allocation.[AllocationType] = 'WorkOrder'
                         AND allocation.[IsDeleted] = 0
                        LEFT JOIN [dbo].[WorkOrderParts] part
                          ON part.[Id] = i.[WorkOrderPartId]
                         AND part.[TenantId] = i.[TenantId]
                         AND part.[AllocationId] = i.[InventoryAllocationId]
                         AND part.[WorkOrderId] = allocation.[ReferenceId]
                        LEFT JOIN [dbo].[Users] actor
                          ON actor.[Id] = i.[ActorUserId]
                         AND actor.[TenantId] = i.[TenantId]
                         AND actor.[IsActive] = 1
                        WHERE allocation.[Id] IS NULL
                           OR part.[Id] IS NULL
                           OR actor.[Id] IS NULL
                           OR i.[NewStatus] <> allocation.[Status]
                           OR (i.[Sequence] = 1 AND i.[PreviousHash] IS NOT NULL)
                           OR (i.[Sequence] > 1 AND NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[InventoryWorkOrderReservationActions] previousAction
                               WHERE previousAction.[TenantId] = i.[TenantId]
                                 AND previousAction.[InventoryAllocationId] = i.[InventoryAllocationId]
                                 AND previousAction.[Sequence] = i.[Sequence] - 1
                                 AND previousAction.[IntegrityHash] = i.[PreviousHash]
                           ))
                    )
                        THROW 51958, 'INV_WORK_ORDER_RESERVATION_ACTION_LINEAGE_INVALID: action tenant, actor, subject, state or hash-chain lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryAllocations_WorkOrderReservationGuard]
                ON [dbo].[InventoryAllocations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE d.[AllocationType] = 'WorkOrder' AND i.[Id] IS NULL
                    )
                        THROW 51951, 'INV_WORK_ORDER_RESERVATION_DELETE_PROHIBITED: work-order reservations cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[AllocationType] = 'WorkOrder'
                          AND d.[Id] IS NULL
                          AND (
                              i.[ReferenceId] IS NULL OR i.[LocationId] IS NULL
                              OR NULLIF(i.[IdempotencyKey], '') IS NULL
                              OR LEN(i.[PayloadHash]) <> 64 OR NULLIF(i.[CorrelationId], '') IS NULL
                              OR i.[AllocatedById] IS NULL
                          )
                    )
                        THROW 51952, 'INV_WORK_ORDER_RESERVATION_GOVERNANCE_REQUIRED: new work-order reservations require exact location, actor and idempotency lineage.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[AllocationType] = 'WorkOrder'
                          AND (
                              i.[AllocationType] <> d.[AllocationType]
                              OR i.[TenantId] <> d.[TenantId]
                              OR i.[InventoryItemId] <> d.[InventoryItemId]
                              OR i.[WarehouseId] <> d.[WarehouseId]
                              OR ISNULL(i.[ReferenceId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[ReferenceId], '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.[ReferenceNumber], '') <> ISNULL(d.[ReferenceNumber], '')
                              OR ISNULL(i.[AllocatedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[AllocatedById], '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.[IdempotencyKey], '') <> ISNULL(d.[IdempotencyKey], '')
                              OR ISNULL(i.[PayloadHash], '') <> ISNULL(d.[PayloadHash], '')
                              OR ISNULL(i.[CorrelationId], '') <> ISNULL(d.[CorrelationId], '')
                              OR i.[IsDeleted] <> d.[IsDeleted]
                          )
                    )
                        THROW 51953, 'INV_WORK_ORDER_RESERVATION_LINEAGE_IMMUTABLE: work-order reservation source and idempotency lineage cannot be changed.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[AllocationType] = 'WorkOrder'
                          AND NOT (
                              (d.[Status] = 'Active' AND i.[Status] IN ('Active','PartiallyConsumed','Consumed','Cancelled'))
                              OR (d.[Status] = 'PartiallyConsumed' AND i.[Status] IN ('PartiallyConsumed','Consumed','Cancelled'))
                              OR (d.[Status] IN ('Consumed','Cancelled') AND i.[Status] = d.[Status])
                          )
                    )
                        THROW 51954, 'INV_WORK_ORDER_RESERVATION_TRANSITION_INVALID: work-order reservation status transition is not allowed.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[AllocationType] = 'WorkOrder'
                          AND (
                              i.[AllocatedQuantity] <= 0 OR i.[ConsumedQuantity] < 0 OR i.[RemainingQuantity] < 0
                              OR i.[ConsumedQuantity] + i.[RemainingQuantity] > i.[AllocatedQuantity]
                              OR (d.[Id] IS NOT NULL AND i.[ConsumedQuantity] < d.[ConsumedQuantity])
                              OR (i.[Status] = 'Active' AND (i.[ConsumedQuantity] <> 0 OR i.[RemainingQuantity] <= 0))
                              OR (i.[Status] = 'PartiallyConsumed' AND (i.[ConsumedQuantity] <= 0 OR i.[RemainingQuantity] <= 0))
                              OR (i.[Status] = 'Consumed' AND (i.[ConsumedQuantity] <= 0 OR i.[RemainingQuantity] <> 0))
                              OR (i.[Status] = 'Cancelled' AND i.[RemainingQuantity] <> 0)
                              OR i.[Status] NOT IN ('Active','PartiallyConsumed','Consumed','Cancelled')
                              OR NOT EXISTS (
                                  SELECT 1 FROM [dbo].[WarehouseLocations] location
                                  WHERE location.[Id] = i.[LocationId]
                                    AND location.[TenantId] = i.[TenantId]
                                    AND (CASE
                                        WHEN location.[IsConsignmentBin] = 1
                                         AND location.[ConsignmentWarehouseId] IS NOT NULL
                                         AND location.[ConsignmentWarehouseId] <> '00000000-0000-0000-0000-000000000000'
                                        THEN location.[ConsignmentWarehouseId]
                                        ELSE location.[WarehouseId]
                                    END) = i.[WarehouseId]
                                    AND location.[IsDeleted] = 0
                              )
                          )
                    )
                        THROW 51955, 'INV_WORK_ORDER_RESERVATION_STATE_INVALID: quantities, status or exact warehouse location are inconsistent.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_WorkOrderParts_ReservationLineageGuard]
                ON [dbo].[WorkOrderParts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[AllocationId] IS NOT NULL
                          AND ISNULL(i.[AllocationId], '00000000-0000-0000-0000-000000000000') <> d.[AllocationId]
                    )
                        THROW 51956, 'INV_WORK_ORDER_PART_ALLOCATION_IMMUTABLE: an assigned work-order reservation cannot be replaced or detached.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [dbo].[InventoryAllocations] allocation ON allocation.[Id] = i.[AllocationId]
                        WHERE allocation.[AllocationType] = 'WorkOrder'
                          AND (
                              allocation.[TenantId] <> i.[TenantId]
                              OR allocation.[InventoryItemId] <> i.[InventoryItemId]
                              OR allocation.[ReferenceId] <> i.[WorkOrderId]
                              OR i.[WarehouseLocationId] IS NULL
                              OR NOT EXISTS (
                                  SELECT 1 FROM [dbo].[WarehouseLocations] location
                                  WHERE location.[Id] = i.[WarehouseLocationId]
                                    AND location.[TenantId] = i.[TenantId]
                                    AND (CASE
                                        WHEN location.[IsConsignmentBin] = 1
                                         AND location.[ConsignmentWarehouseId] IS NOT NULL
                                         AND location.[ConsignmentWarehouseId] <> '00000000-0000-0000-0000-000000000000'
                                        THEN location.[ConsignmentWarehouseId]
                                        ELSE location.[WarehouseId]
                                    END) = allocation.[WarehouseId]
                                    AND location.[IsDeleted] = 0
                              )
                              OR (i.[IsDeleted] = 1 AND allocation.[Status] <> 'Cancelled')
                          )
                    )
                        THROW 51957, 'INV_WORK_ORDER_PART_LINEAGE_INVALID: the work-order part and exact-bin reservation lineage do not match.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_WorkOrderParts_ReservationLineageGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryAllocations_WorkOrderReservationGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryWorkOrderReservationActions_Immutable];");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderParts_InventoryAllocations_AllocationId",
                table: "WorkOrderParts");

            migrationBuilder.DropTable(
                name: "InventoryWorkOrderReservationActions");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderParts_TenantId_AllocationId",
                table: "WorkOrderParts");

            migrationBuilder.DropIndex(
                name: "IX_InventoryAllocations_TenantId_WorkOrderIdempotencyKey",
                table: "InventoryAllocations");

            migrationBuilder.DropIndex(
                name: "IX_InventoryAllocations_TenantId_WorkOrder_Status_RequiredDate",
                table: "InventoryAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryAllocations_WorkOrderLineage",
                table: "InventoryAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryAllocations_WorkOrderHashes",
                table: "InventoryAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryAllocations_WorkOrderQuantities",
                table: "InventoryAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_TenantId",
                table: "WorkOrderParts",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderParts_InventoryAllocations_AllocationId",
                table: "WorkOrderParts",
                column: "AllocationId",
                principalTable: "InventoryAllocations",
                principalColumn: "Id");
        }
    }
}
