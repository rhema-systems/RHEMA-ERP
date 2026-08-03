using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0609ControlledCycleCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_TenantId",
                table: "PhysicalCounts");

            migrationBuilder.AddColumn<string>(
                name: "ABCClass",
                table: "PhysicalCounts",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AuditAttestedAtUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AuditAttestedById",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CalendarOccurrenceId",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CutoffAtUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CutoffOccurrenceId",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CycleCountScheduleId",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinanceApprovedAtUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceApprovedById",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FreezeReleasedAtUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FreezeStartedAtUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvestigationSummary",
                table: "PhysicalCounts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PhysicalCounts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledForUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockAdjustmentId",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoresApprovedAtUtc",
                table: "PhysicalCounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoresApprovedById",
                table: "PhysicalCounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstCountQuantity",
                table: "PhysicalCountItems",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvestigationNotes",
                table: "PhysicalCountItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecountedAtUtc",
                table: "PhysicalCountItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecountedById",
                table: "PhysicalCountItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecountedQuantity",
                table: "PhysicalCountItems",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PhysicalCountItems",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "InventoryCycleCountSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ABCClass = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    FrequencyDays = table.Column<int>(type: "int", nullable: false),
                    NextDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalendarOccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CutoffOccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CutoffAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FreezeInventory = table.Column<bool>(type: "bit", nullable: false),
                    BlindCount = table.Column<bool>(type: "bit", nullable: false),
                    RecountQuantityThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RecountValueThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastGeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPhysicalCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_InventoryCycleCountSchedules", x => x.Id);
                    table.CheckConstraint("CK_InventoryCycleCountSchedules_ABCClass", "[ABCClass] IN ('A','B','C')");
                    table.CheckConstraint("CK_InventoryCycleCountSchedules_Cutoff", "[NextDueAtUtc] <= [CutoffAtUtc]");
                    table.CheckConstraint("CK_InventoryCycleCountSchedules_Frequency", "[FrequencyDays] BETWEEN 1 AND 366");
                    table.CheckConstraint("CK_InventoryCycleCountSchedules_Thresholds", "[RecountQuantityThreshold] >= 0 AND [RecountValueThreshold] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryCycleCountSchedules_PhysicalCounts_LastPhysicalCountId",
                        column: x => x.LastPhysicalCountId,
                        principalTable: "PhysicalCounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryCycleCountSchedules_ProcurementCalendarOccurrences_CalendarOccurrenceId",
                        column: x => x.CalendarOccurrenceId,
                        principalTable: "ProcurementCalendarOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCycleCountSchedules_ProcurementCalendarOccurrences_CutoffOccurrenceId",
                        column: x => x.CutoffOccurrenceId,
                        principalTable: "ProcurementCalendarOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCycleCountSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCycleCountSchedules_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCycleCountSchedules_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCountActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhysicalCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_PhysicalCountActions", x => x.Id);
                    table.CheckConstraint("CK_PhysicalCountActions_ActionType", "[ActionType] BETWEEN 1 AND 13");
                    table.ForeignKey(
                        name: "FK_PhysicalCountActions_PhysicalCounts_PhysicalCountId",
                        column: x => x.PhysicalCountId,
                        principalTable: "PhysicalCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_AuditAttestedById",
                table: "PhysicalCounts",
                column: "AuditAttestedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CalendarOccurrenceId",
                table: "PhysicalCounts",
                column: "CalendarOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CutoffOccurrenceId",
                table: "PhysicalCounts",
                column: "CutoffOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_CycleCountScheduleId",
                table: "PhysicalCounts",
                column: "CycleCountScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_FinanceApprovedById",
                table: "PhysicalCounts",
                column: "FinanceApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_StockAdjustmentId",
                table: "PhysicalCounts",
                column: "StockAdjustmentId",
                unique: true,
                filter: "[StockAdjustmentId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_StoresApprovedById",
                table: "PhysicalCounts",
                column: "StoresApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_TenantId_CycleCountScheduleId_ScheduledForUtc",
                table: "PhysicalCounts",
                columns: new[] { "TenantId", "CycleCountScheduleId", "ScheduledForUtc" },
                unique: true,
                filter: "[CycleCountScheduleId] IS NOT NULL AND [ScheduledForUtc] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCounts_ABCClass",
                table: "PhysicalCounts",
                sql: "[ABCClass] IS NULL OR [ABCClass] IN ('A','B','C')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCounts_Cutoff",
                table: "PhysicalCounts",
                sql: "[ScheduledForUtc] IS NULL OR [CutoffAtUtc] IS NULL OR [ScheduledForUtc] <= [CutoffAtUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountItems_RecountedById",
                table: "PhysicalCountItems",
                column: "RecountedById");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCountItems_CountAttempts",
                table: "PhysicalCountItems",
                sql: "[CountAttempts] BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalCountItems_CountQuantities",
                table: "PhysicalCountItems",
                sql: "[SystemQuantity] >= 0 AND [CountedQuantity] >= 0 AND ([FirstCountQuantity] IS NULL OR [FirstCountQuantity] >= 0) AND ([RecountedQuantity] IS NULL OR [RecountedQuantity] >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCycleCountSchedules_CalendarOccurrenceId",
                table: "InventoryCycleCountSchedules",
                column: "CalendarOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCycleCountSchedules_CutoffOccurrenceId",
                table: "InventoryCycleCountSchedules",
                column: "CutoffOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCycleCountSchedules_LastPhysicalCountId",
                table: "InventoryCycleCountSchedules",
                column: "LastPhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCycleCountSchedules_LocationId",
                table: "InventoryCycleCountSchedules",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCycleCountSchedules_TenantId_IsActive_NextDueAtUtc",
                table: "InventoryCycleCountSchedules",
                columns: new[] { "TenantId", "IsActive", "NextDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCycleCountSchedules_TenantId_WarehouseId_LocationId_ABCClass",
                table: "InventoryCycleCountSchedules",
                columns: new[] { "TenantId", "WarehouseId", "LocationId", "ABCClass" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCycleCountSchedules_WarehouseId",
                table: "InventoryCycleCountSchedules",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountActions_ActorUserId",
                table: "PhysicalCountActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountActions_PhysicalCountId",
                table: "PhysicalCountActions",
                column: "PhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountActions_TenantId_PhysicalCountId_ActionType_IdempotencyKey",
                table: "PhysicalCountActions",
                columns: new[] { "TenantId", "PhysicalCountId", "ActionType", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountActions_TenantId_PhysicalCountId_Sequence",
                table: "PhysicalCountActions",
                columns: new[] { "TenantId", "PhysicalCountId", "Sequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCountItems_Users_RecountedById",
                table: "PhysicalCountItems",
                column: "RecountedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_InventoryCycleCountSchedules_CycleCountScheduleId",
                table: "PhysicalCounts",
                column: "CycleCountScheduleId",
                principalTable: "InventoryCycleCountSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_ProcurementCalendarOccurrences_CalendarOccurrenceId",
                table: "PhysicalCounts",
                column: "CalendarOccurrenceId",
                principalTable: "ProcurementCalendarOccurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_ProcurementCalendarOccurrences_CutoffOccurrenceId",
                table: "PhysicalCounts",
                column: "CutoffOccurrenceId",
                principalTable: "ProcurementCalendarOccurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_StockAdjustments_StockAdjustmentId",
                table: "PhysicalCounts",
                column: "StockAdjustmentId",
                principalTable: "StockAdjustments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_Users_AuditAttestedById",
                table: "PhysicalCounts",
                column: "AuditAttestedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_Users_FinanceApprovedById",
                table: "PhysicalCounts",
                column: "FinanceApprovedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysicalCounts_Users_StoresApprovedById",
                table: "PhysicalCounts",
                column: "StoresApprovedById",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryCycleCountSchedules_ControlledLifecycle]
                ON [dbo].[InventoryCycleCountSchedules]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51901, 'INV_COUNT_SCHEDULE_DELETE_BLOCKED: cycle-count schedules are retained as controlled history.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId AND w.IsDeleted = 0 AND w.IsActive = 1
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                            AND CASE WHEN l.IsConsignmentBin = 1 AND l.ConsignmentWarehouseId IS NOT NULL THEN l.ConsignmentWarehouseId ELSE l.WarehouseId END = i.WarehouseId
                        LEFT JOIN ProcurementCalendarOccurrences c ON c.Id = i.CalendarOccurrenceId AND c.TenantId = i.TenantId AND c.IsDeleted = 0 AND c.EventType = 3 AND c.Status NOT IN (4,5,6)
                        LEFT JOIN ProcurementCalendarOccurrences y ON y.Id = i.CutoffOccurrenceId AND y.TenantId = i.TenantId AND y.IsDeleted = 0 AND y.EventType = 4 AND y.Status NOT IN (4,5,6)
                        WHERE w.Id IS NULL OR l.Id IS NULL OR c.Id IS NULL OR y.Id IS NULL
                           OR i.ABCClass NOT IN (N'A',N'B',N'C') OR i.FrequencyDays NOT BETWEEN 1 AND 366
                           OR i.NextDueAtUtc > i.CutoffAtUtc OR i.CutoffAtUtc <> y.DueAtUtc
                           OR i.RecountQuantityThreshold < 0 OR i.RecountValueThreshold < 0
                           OR i.FreezeInventory = 0 OR i.BlindCount = 0 OR i.IsDeleted = 1)
                        THROW 51902, 'INV_COUNT_SCHEDULE_INVALID: schedule scope, calendar ownership, cut-off, blind count and freeze controls are invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_PhysicalCountActions_AppendOnly]
                ON [dbo].[PhysicalCountActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51911, 'INV_COUNT_ACTION_IMMUTABLE: physical-count action history cannot be changed or deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN PhysicalCounts p ON p.Id = i.PhysicalCountId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                        LEFT JOIN Users u ON u.Id = i.ActorUserId AND u.TenantId = i.TenantId AND u.IsActive = 1
                        WHERE p.Id IS NULL OR u.Id IS NULL OR i.Sequence < 1 OR i.ActionType NOT BETWEEN 1 AND 13
                           OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0 OR LEN(i.PayloadHash) <> 64
                           OR LEN(i.IntegrityHash) <> 64 OR LEN(LTRIM(RTRIM(i.ActorRole))) = 0)
                        THROW 51912, 'INV_COUNT_ACTION_INVALID: action tenant, actor, sequence, replay key or integrity lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_PhysicalCountItems_ControlledMutation]
                ON [dbo].[PhysicalCountItems]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id
                        LEFT JOIN PhysicalCounts p ON p.Id = d.PhysicalCountId AND p.TenantId = d.TenantId
                        WHERE i.Id IS NULL AND ISNULL(p.Status,N'') <> N'Draft')
                        THROW 51921, 'INV_COUNT_LINE_DELETE_BLOCKED: started count lines are immutable controlled history.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN PhysicalCounts p ON p.Id = i.PhysicalCountId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                        LEFT JOIN InventoryItems item ON item.Id = i.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                        WHERE p.Id IS NULL OR item.Id IS NULL OR i.SystemQuantity < 0 OR i.CountedQuantity < 0 OR i.CountAttempts NOT BETWEEN 0 AND 2
                           OR (i.FirstCountQuantity IS NOT NULL AND i.FirstCountQuantity < 0) OR (i.RecountedQuantity IS NOT NULL AND i.RecountedQuantity < 0)
                           OR (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id) AND p.Status <> N'Draft'))
                        THROW 51922, 'INV_COUNT_LINE_INVALID: line scope, initial state or quantity controls are invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.PhysicalCountId <> d.PhysicalCountId OR i.InventoryItemId <> d.InventoryItemId
                           OR ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId,'00000000-0000-0000-0000-000000000000')
                           OR i.SystemQuantity <> d.SystemQuantity OR i.UnitCost <> d.UnitCost OR i.IsDeleted <> d.IsDeleted)
                        THROW 51923, 'INV_COUNT_LINE_SOURCE_IMMUTABLE: count-line scope, system snapshot and cost cannot be changed.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        INNER JOIN PhysicalCounts p ON p.Id = i.PhysicalCountId AND p.TenantId = i.TenantId
                        WHERE (i.CountedQuantity <> d.CountedQuantity OR ISNULL(i.FirstCountQuantity,-1) <> ISNULL(d.FirstCountQuantity,-1)
                              OR i.IsCounted <> d.IsCounted OR ISNULL(i.CountedById,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.CountedById,'00000000-0000-0000-0000-000000000000'))
                          AND (p.Status <> N'InProgress' OR i.CountedById <> p.CountedById
                               OR NOT EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId = p.Id AND a.TenantId = p.TenantId AND a.ActionType = 4 AND a.ActorUserId = i.CountedById)))
                        THROW 51924, 'INV_COUNT_LINE_FIRST_COUNT_UNGOVERNED: first count requires the active counter and immutable action.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        INNER JOIN PhysicalCounts p ON p.Id = i.PhysicalCountId AND p.TenantId = i.TenantId
                        WHERE (ISNULL(i.RecountedQuantity,-1) <> ISNULL(d.RecountedQuantity,-1)
                              OR ISNULL(i.RecountedById,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.RecountedById,'00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.InvestigationNotes,N'') <> ISNULL(d.InvestigationNotes,N''))
                          AND (p.Status NOT IN (N'RecountRequired',N'PendingStoresApproval') OR i.RecountedById IN (p.InitiatedById,p.CountedById)
                               OR LEN(LTRIM(RTRIM(ISNULL(i.InvestigationNotes,N'')))) = 0
                               OR NOT EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId = p.Id AND a.TenantId = p.TenantId AND a.ActionType = 6 AND a.ActorUserId = i.RecountedById)))
                        THROW 51925, 'INV_COUNT_LINE_RECOUNT_UNGOVERNED: recount requires an independent actor, investigation and immutable action.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_PhysicalCounts_ControlledLifecycle]
                ON [dbo].[PhysicalCounts]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51931, 'INV_COUNT_DELETE_BLOCKED: physical counts cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId AND w.IsDeleted = 0 AND w.IsActive = 1
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                            AND CASE WHEN l.IsConsignmentBin = 1 AND l.ConsignmentWarehouseId IS NOT NULL THEN l.ConsignmentWarehouseId ELSE l.WarehouseId END = i.WarehouseId
                        WHERE w.Id IS NULL OR (i.LocationId IS NOT NULL AND l.Id IS NULL) OR i.InitiatedById IS NULL
                           OR i.Status NOT IN (N'Draft',N'InProgress',N'RecountRequired',N'PendingStoresApproval',N'PendingFinanceApproval',N'PendingAuditAttestation',N'ReadyToPost',N'Posted',N'Cancelled',N'PendingApproval',N'Approved',N'Completed')
                           OR (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id) AND i.Status <> N'Draft')
                           OR (i.CountType = 2 AND (i.FreezeInventory = 0 OR i.BlindCount = 0 OR i.ABCClass NOT IN (N'A',N'B',N'C'))))
                        THROW 51932, 'INV_COUNT_SCOPE_INVALID: tenant warehouse/location, initiator, initial state or cycle-count controls are invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryCycleCountSchedules s ON s.Id = i.CycleCountScheduleId AND s.TenantId = i.TenantId AND s.IsDeleted = 0
                        LEFT JOIN ProcurementCalendarOccurrences c ON c.Id = i.CalendarOccurrenceId AND c.TenantId = i.TenantId AND c.EventType = 3 AND c.IsDeleted = 0
                        LEFT JOIN ProcurementCalendarOccurrences y ON y.Id = i.CutoffOccurrenceId AND y.TenantId = i.TenantId AND y.EventType = 4 AND y.IsDeleted = 0
                        WHERE i.CycleCountScheduleId IS NOT NULL AND (s.Id IS NULL OR c.Id IS NULL OR y.Id IS NULL
                           OR i.CalendarOccurrenceId <> s.CalendarOccurrenceId OR i.CutoffOccurrenceId <> s.CutoffOccurrenceId
                           OR i.WarehouseId <> s.WarehouseId OR i.LocationId <> s.LocationId OR i.ABCClass <> s.ABCClass
                           OR i.CutoffAtUtc <> s.CutoffAtUtc OR i.ScheduledForUtc > i.CutoffAtUtc))
                        THROW 51933, 'INV_COUNT_SCHEDULE_LINEAGE_INVALID: scheduled count scope and calendar cut-off must match its schedule.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status <> N'Draft' AND (i.TenantId <> d.TenantId OR i.CountNumber <> d.CountNumber OR i.WarehouseId <> d.WarehouseId
                           OR ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId,'00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.CycleCountScheduleId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.CycleCountScheduleId,'00000000-0000-0000-0000-000000000000')
                           OR i.InitiatedById <> d.InitiatedById OR i.FreezeInventory <> d.FreezeInventory OR i.BlindCount <> d.BlindCount OR i.IsDeleted <> d.IsDeleted))
                        THROW 51934, 'INV_COUNT_SOURCE_IMMUTABLE: started count identity, scope, snapshot controls and initiator cannot change.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status AND NOT (
                            (d.Status = N'Draft' AND i.Status = N'InProgress' AND i.CountedById IS NOT NULL AND i.FreezeStartedAtUtc IS NOT NULL AND (i.CutoffAtUtc IS NULL OR i.StartedDate <= i.CutoffAtUtc) AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=3 AND a.ActorUserId=i.CountedById))
                         OR (d.Status = N'InProgress' AND i.Status IN (N'RecountRequired',N'PendingStoresApproval') AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType IN (5,7) AND a.ActorUserId=i.CountedById))
                         OR (d.Status = N'RecountRequired' AND i.Status = N'PendingStoresApproval' AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=6))
                         OR (d.Status = N'PendingStoresApproval' AND i.Status = N'PendingFinanceApproval' AND i.StoresApprovedById IS NOT NULL AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=8 AND a.ActorUserId=i.StoresApprovedById))
                         OR (d.Status = N'PendingFinanceApproval' AND i.Status = N'PendingAuditAttestation' AND i.FinanceApprovedById IS NOT NULL AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=9 AND a.ActorUserId=i.FinanceApprovedById))
                         OR (d.Status = N'PendingAuditAttestation' AND i.Status = N'ReadyToPost' AND i.AuditAttestedById IS NOT NULL AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=10 AND a.ActorUserId=i.AuditAttestedById))
                         OR (d.Status IN (N'PendingStoresApproval',N'PendingFinanceApproval',N'PendingAuditAttestation') AND i.Status=N'RecountRequired' AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=11))
                         OR (d.Status = N'ReadyToPost' AND i.Status=N'Posted' AND i.PostedById=i.FinanceApprovedById AND i.FreezeReleasedAtUtc IS NOT NULL AND (i.CutoffAtUtc IS NULL OR i.PostedDate <= i.CutoffAtUtc) AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=12 AND a.ActorUserId=i.PostedById))
                         OR (d.Status IN (N'Draft',N'InProgress',N'RecountRequired') AND i.Status=N'Cancelled' AND EXISTS (SELECT 1 FROM PhysicalCountActions a WHERE a.PhysicalCountId=i.Id AND a.TenantId=i.TenantId AND a.ActionType=13))))
                        THROW 51935, 'INV_COUNT_TRANSITION_INVALID: lifecycle transition requires the matching immutable action, cut-off and actor.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE (i.Status IN (N'PendingFinanceApproval',N'PendingAuditAttestation',N'ReadyToPost',N'Posted') AND (i.StoresApprovedById IS NULL OR i.StoresApprovedById IN (i.InitiatedById,i.CountedById)))
                           OR (i.Status IN (N'PendingAuditAttestation',N'ReadyToPost',N'Posted') AND (i.FinanceApprovedById IS NULL OR i.FinanceApprovedById IN (i.InitiatedById,i.CountedById,i.StoresApprovedById)))
                           OR (i.Status IN (N'ReadyToPost',N'Posted') AND (i.AuditAttestedById IS NULL OR i.AuditAttestedById IN (i.InitiatedById,i.CountedById,i.StoresApprovedById,i.FinanceApprovedById)))
                           OR (i.TotalVarianceQuantity <> 0 AND i.Status IN (N'PendingStoresApproval',N'PendingFinanceApproval',N'PendingAuditAttestation',N'ReadyToPost',N'Posted') AND i.StockAdjustmentId IS NULL)
                           OR (i.Status = N'ReadyToPost' AND i.TotalVarianceQuantity <> 0 AND NOT EXISTS (SELECT 1 FROM StockAdjustments a WHERE a.Id=i.StockAdjustmentId AND a.TenantId=i.TenantId AND a.Status=N'Approved'))
                           OR (i.Status = N'Posted' AND i.TotalVarianceQuantity <> 0 AND NOT EXISTS (SELECT 1 FROM StockAdjustments a WHERE a.Id=i.StockAdjustmentId AND a.TenantId=i.TenantId AND a.Status=N'Posted' AND a.FinancePostingEventId IS NOT NULL)))
                        THROW 51936, 'INV_COUNT_SOD_OR_POSTING_INVALID: independent approvals and governed stock-adjustment Finance lineage are required.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_WarehouseQuantities_PhysicalCountFreeze]
                ON [dbo].[WarehouseQuantities]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id=i.Id
                        WHERE (i.CurrentStock<>d.CurrentStock OR i.AvailableStock<>d.AvailableStock OR i.AllocatedStock<>d.AllocatedStock)
                          AND EXISTS (SELECT 1 FROM PhysicalCounts p INNER JOIN PhysicalCountItems pci ON pci.PhysicalCountId=p.Id AND pci.TenantId=p.TenantId AND pci.InventoryItemId=i.InventoryItemId AND pci.IsDeleted=0
                                      WHERE p.TenantId=i.TenantId AND p.WarehouseId=i.WarehouseId AND p.FreezeInventory=1 AND p.FreezeStartedAtUtc IS NOT NULL AND p.FreezeReleasedAtUtc IS NULL
                                        AND p.Status IN (N'InProgress',N'RecountRequired',N'PendingStoresApproval',N'PendingFinanceApproval',N'PendingAuditAttestation',N'ReadyToPost'))
                          AND NOT EXISTS (SELECT 1 FROM PhysicalCounts p INNER JOIN StockAdjustments a ON a.Id=p.StockAdjustmentId AND a.TenantId=p.TenantId AND a.Status=N'Posted'
                                          INNER JOIN StockAdjustmentItems ai ON ai.AdjustmentId=a.Id AND ai.TenantId=a.TenantId AND ai.InventoryItemId=i.InventoryItemId
                                          WHERE p.TenantId=i.TenantId AND p.WarehouseId=i.WarehouseId AND p.Status=N'ReadyToPost'
                                            AND i.CurrentStock-d.CurrentStock=ai.AdjustmentQuantity
                                            AND NOT EXISTS (SELECT 1 FROM StockMovements m WHERE m.TenantId=a.TenantId AND m.ReferenceType=5 AND m.ReferenceId=a.Id AND m.InventoryItemId=ai.InventoryItemId AND m.IsDeleted=0)))
                        THROW 51941, 'INV_COUNT_WAREHOUSE_FROZEN: warehouse quantity cannot change during an active governed count.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryItems_PhysicalCountFreeze]
                ON [dbo].[InventoryItems]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id=i.Id
                        WHERE (i.CurrentStock<>d.CurrentStock OR i.AvailableStock<>d.AvailableStock OR i.AllocatedStock<>d.AllocatedStock)
                          AND EXISTS (SELECT 1 FROM PhysicalCounts p INNER JOIN PhysicalCountItems pci ON pci.PhysicalCountId=p.Id AND pci.TenantId=p.TenantId AND pci.InventoryItemId=i.Id AND pci.IsDeleted=0
                                      WHERE p.TenantId=i.TenantId AND p.FreezeInventory=1 AND p.FreezeStartedAtUtc IS NOT NULL AND p.FreezeReleasedAtUtc IS NULL
                                        AND p.Status IN (N'InProgress',N'RecountRequired',N'PendingStoresApproval',N'PendingFinanceApproval',N'PendingAuditAttestation',N'ReadyToPost'))
                          AND NOT EXISTS (SELECT 1 FROM PhysicalCounts p INNER JOIN StockAdjustments a ON a.Id=p.StockAdjustmentId AND a.TenantId=p.TenantId AND a.Status=N'Posted'
                                          INNER JOIN StockAdjustmentItems ai ON ai.AdjustmentId=a.Id AND ai.TenantId=a.TenantId AND ai.InventoryItemId=i.Id
                                          WHERE p.TenantId=i.TenantId AND p.Status=N'ReadyToPost' AND i.CurrentStock-d.CurrentStock=ai.AdjustmentQuantity
                                            AND NOT EXISTS (SELECT 1 FROM StockMovements m WHERE m.TenantId=a.TenantId AND m.ReferenceType=5 AND m.ReferenceId=a.Id AND m.InventoryItemId=ai.InventoryItemId AND m.IsDeleted=0)))
                        THROW 51942, 'INV_COUNT_ITEM_FROZEN: item quantity cannot change during an active governed count.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_PhysicalCountFreeze]
                ON [dbo].[StockMovements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE EXISTS (SELECT 1 FROM PhysicalCounts p INNER JOIN PhysicalCountItems pci ON pci.PhysicalCountId=p.Id AND pci.TenantId=p.TenantId AND pci.InventoryItemId=i.InventoryItemId AND pci.IsDeleted=0
                                      WHERE p.TenantId=i.TenantId AND p.WarehouseId=i.WarehouseId AND p.FreezeInventory=1 AND p.FreezeStartedAtUtc IS NOT NULL AND p.FreezeReleasedAtUtc IS NULL
                                        AND p.Status IN (N'InProgress',N'RecountRequired',N'PendingStoresApproval',N'PendingFinanceApproval',N'PendingAuditAttestation',N'ReadyToPost'))
                          AND NOT EXISTS (SELECT 1 FROM PhysicalCounts p INNER JOIN StockAdjustments a ON a.Id=p.StockAdjustmentId AND a.TenantId=p.TenantId AND a.Status=N'Posted'
                                          INNER JOIN StockAdjustmentItems ai ON ai.AdjustmentId=a.Id AND ai.TenantId=a.TenantId AND ai.InventoryItemId=i.InventoryItemId
                                          WHERE p.TenantId=i.TenantId AND p.WarehouseId=i.WarehouseId AND p.Status=N'ReadyToPost'
                                            AND i.ReferenceType=5 AND i.ReferenceId=a.Id AND i.ReferenceNumber=a.AdjustmentNumber
                                            AND ISNULL(i.LocationId,'00000000-0000-0000-0000-000000000000')=ISNULL(ai.LocationId,'00000000-0000-0000-0000-000000000000')
                                            AND i.Quantity=ai.AdjustmentQuantity AND i.ProcessedById=p.FinanceApprovedById))
                        THROW 51943, 'INV_COUNT_MOVEMENT_FROZEN: stock movement requires the linked approved count adjustment and Finance actor.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_PhysicalCountFreeze];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryItems_PhysicalCountFreeze];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_WarehouseQuantities_PhysicalCountFreeze];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PhysicalCounts_ControlledLifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PhysicalCountItems_ControlledMutation];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PhysicalCountActions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryCycleCountSchedules_ControlledLifecycle];");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCountItems_Users_RecountedById",
                table: "PhysicalCountItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_InventoryCycleCountSchedules_CycleCountScheduleId",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_ProcurementCalendarOccurrences_CalendarOccurrenceId",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_ProcurementCalendarOccurrences_CutoffOccurrenceId",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_StockAdjustments_StockAdjustmentId",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_Users_AuditAttestedById",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_Users_FinanceApprovedById",
                table: "PhysicalCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalCounts_Users_StoresApprovedById",
                table: "PhysicalCounts");

            migrationBuilder.DropTable(
                name: "InventoryCycleCountSchedules");

            migrationBuilder.DropTable(
                name: "PhysicalCountActions");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_AuditAttestedById",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_CalendarOccurrenceId",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_CutoffOccurrenceId",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_CycleCountScheduleId",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_FinanceApprovedById",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_StockAdjustmentId",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_StoresApprovedById",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCounts_TenantId_CycleCountScheduleId_ScheduledForUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCounts_ABCClass",
                table: "PhysicalCounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCounts_Cutoff",
                table: "PhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCountItems_RecountedById",
                table: "PhysicalCountItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCountItems_CountAttempts",
                table: "PhysicalCountItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalCountItems_CountQuantities",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "ABCClass",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "AuditAttestedAtUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "AuditAttestedById",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "CalendarOccurrenceId",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "CutoffAtUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "CutoffOccurrenceId",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "CycleCountScheduleId",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "FinanceApprovedAtUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "FinanceApprovedById",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "FreezeReleasedAtUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "FreezeStartedAtUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "InvestigationSummary",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "ScheduledForUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "StockAdjustmentId",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "StoresApprovedAtUtc",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "StoresApprovedById",
                table: "PhysicalCounts");

            migrationBuilder.DropColumn(
                name: "FirstCountQuantity",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "InvestigationNotes",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "RecountedAtUtc",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "RecountedById",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "RecountedQuantity",
                table: "PhysicalCountItems");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PhysicalCountItems");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCounts_TenantId",
                table: "PhysicalCounts",
                column: "TenantId");
        }
    }
}
