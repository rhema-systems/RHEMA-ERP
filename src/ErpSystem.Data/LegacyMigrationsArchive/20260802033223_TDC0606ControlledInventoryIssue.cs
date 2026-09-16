using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0606ControlledInventoryIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InventoryIssueVoucherId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventoryRequisitions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "InventoryIssueVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InventoryRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostCenter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceiverComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryIssueVouchers", x => x.Id);
                    table.CheckConstraint("CK_InventoryIssueVouchers_Acknowledgement", "([Status] = 1 AND [AcknowledgedById] IS NULL AND [AcknowledgedAtUtc] IS NULL) OR ([Status] = 2 AND [AcknowledgedById] = [ReceiverUserId] AND [AcknowledgedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryIssueVouchers_Hashes", "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_InventoryIssueVouchers_Sod", "[RequestedById] <> [ApprovedById] AND [RequestedById] <> [IssuedById] AND [ApprovedById] <> [IssuedById] AND [IssuedById] <> [ReceiverUserId]");
                    table.CheckConstraint("CK_InventoryIssueVouchers_Status", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_InventoryRequisitions_InventoryRequisitionId",
                        column: x => x.InventoryRequisitionId,
                        principalTable: "InventoryRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_Users_AcknowledgedById",
                        column: x => x.AcknowledgedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_Users_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_Users_ReceiverUserId",
                        column: x => x.ReceiverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVouchers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryIssueVoucherActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryIssueVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    StatusAfter = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_InventoryIssueVoucherActions", x => x.Id);
                    table.CheckConstraint("CK_InventoryIssueVoucherActions_ActionType", "[ActionType] IN (1,2)");
                    table.CheckConstraint("CK_InventoryIssueVoucherActions_IntegrityHash", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_InventoryIssueVoucherActions_Sequence", "[Sequence] > 0");
                    table.CheckConstraint("CK_InventoryIssueVoucherActions_StatusAfter", "[StatusAfter] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherActions_InventoryIssueVouchers_InventoryIssueVoucherId",
                        column: x => x.InventoryIssueVoucherId,
                        principalTable: "InventoryIssueVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryIssueVoucherLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryIssueVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRequisitionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufactureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InventoryTrackingExceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_InventoryIssueVoucherLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryIssueVoucherLines_IntegrityHash", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_InventoryIssueVoucherLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryIssueVoucherLines_Value", "[UnitCost] >= 0 AND [TotalValue] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherLines_InventoryIssueVouchers_InventoryIssueVoucherId",
                        column: x => x.InventoryIssueVoucherId,
                        principalTable: "InventoryIssueVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherLines_InventoryRequisitionItems_InventoryRequisitionItemId",
                        column: x => x.InventoryRequisitionItemId,
                        principalTable: "InventoryRequisitionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherLines_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherLines_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryIssueVoucherId",
                table: "StockMovements",
                column: "InventoryIssueVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherActions_InventoryIssueVoucherId",
                table: "InventoryIssueVoucherActions",
                column: "InventoryIssueVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherActions_TenantId_InventoryIssueVoucherId_Sequence",
                table: "InventoryIssueVoucherActions",
                columns: new[] { "TenantId", "InventoryIssueVoucherId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherActions_TenantId_OccurredAtUtc",
                table: "InventoryIssueVoucherActions",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherLines_InventoryIssueVoucherId",
                table: "InventoryIssueVoucherLines",
                column: "InventoryIssueVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherLines_InventoryItemId",
                table: "InventoryIssueVoucherLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherLines_InventoryRequisitionItemId",
                table: "InventoryIssueVoucherLines",
                column: "InventoryRequisitionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherLines_LocationId",
                table: "InventoryIssueVoucherLines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherLines_TenantId_InventoryIssueVoucherId_InventoryRequisitionItemId",
                table: "InventoryIssueVoucherLines",
                columns: new[] { "TenantId", "InventoryIssueVoucherId", "InventoryRequisitionItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherLines_TenantId_InventoryItemId_WarehouseId_LocationId",
                table: "InventoryIssueVoucherLines",
                columns: new[] { "TenantId", "InventoryItemId", "WarehouseId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherLines_WarehouseId",
                table: "InventoryIssueVoucherLines",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_AcknowledgedById",
                table: "InventoryIssueVouchers",
                column: "AcknowledgedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_ApprovedById",
                table: "InventoryIssueVouchers",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_InventoryRequisitionId",
                table: "InventoryIssueVouchers",
                column: "InventoryRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_IssuedById",
                table: "InventoryIssueVouchers",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_LocationId",
                table: "InventoryIssueVouchers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_ReceiverUserId",
                table: "InventoryIssueVouchers",
                column: "ReceiverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_RequestedById",
                table: "InventoryIssueVouchers",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_TenantId_InventoryRequisitionId_IdempotencyKey",
                table: "InventoryIssueVouchers",
                columns: new[] { "TenantId", "InventoryRequisitionId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_TenantId_InventoryRequisitionId_IssuedAtUtc",
                table: "InventoryIssueVouchers",
                columns: new[] { "TenantId", "InventoryRequisitionId", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_TenantId_ReceiverUserId_Status",
                table: "InventoryIssueVouchers",
                columns: new[] { "TenantId", "ReceiverUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_TenantId_VoucherNumber",
                table: "InventoryIssueVouchers",
                columns: new[] { "TenantId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVouchers_WarehouseId",
                table: "InventoryIssueVouchers",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_InventoryIssueVouchers_InventoryIssueVoucherId",
                table: "StockMovements",
                column: "InventoryIssueVoucherId",
                principalTable: "InventoryIssueVouchers",
                principalColumn: "Id");

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
                        SELECT 1 FROM inserted i
                        INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.InventoryRequisitionId <> d.InventoryRequisitionId
                           OR i.VoucherNumber <> d.VoucherNumber OR i.WarehouseId <> d.WarehouseId
                           OR ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId, '00000000-0000-0000-0000-000000000000')
                           OR i.DepartmentId <> d.DepartmentId OR ISNULL(i.DepartmentName, N'') <> ISNULL(d.DepartmentName, N'')
                           OR ISNULL(i.CostCenter, N'') <> ISNULL(d.CostCenter, N'')
                           OR ISNULL(i.ProjectId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProjectId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.ProjectCode, N'') <> ISNULL(d.ProjectCode, N'')
                           OR i.RequestedById <> d.RequestedById OR i.ApprovedById <> d.ApprovedById
                           OR i.IssuedById <> d.IssuedById OR i.ReceiverUserId <> d.ReceiverUserId
                           OR i.IssuedAtUtc <> d.IssuedAtUtc OR i.IdempotencyKey <> d.IdempotencyKey
                           OR ISNULL(i.Notes, N'') <> ISNULL(d.Notes, N'')
                           OR i.PayloadHash <> d.PayloadHash OR i.CorrelationId <> d.CorrelationId
                           OR i.SourceSnapshotJson <> d.SourceSnapshotJson
                           OR i.CreatedAt <> d.CreatedAt OR ISNULL(i.CreatedBy, N'') <> ISNULL(d.CreatedBy, N'')
                           OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted OR ISNULL(i.DeletedAt, '0001-01-01') <> ISNULL(d.DeletedAt, '0001-01-01')
                           OR ISNULL(i.DeletedBy, N'') <> ISNULL(d.DeletedBy, N'')
                           OR NOT (d.Status = 1 AND i.Status = 2
                                   AND d.AcknowledgedById IS NULL AND i.AcknowledgedById = i.ReceiverUserId
                                   AND d.AcknowledgedAtUtc IS NULL AND i.AcknowledgedAtUtc IS NOT NULL
                                   AND d.ReceiverComment IS NULL AND LEN(LTRIM(RTRIM(i.ReceiverComment))) > 0
                                   AND i.IntegrityHash <> d.IntegrityHash AND i.UpdatedAt IS NOT NULL
                                   AND i.LastModifiedById = i.ReceiverUserId))
                        THROW 51602, 'INV_ISSUE_VOUCHER_IMMUTABLE: only the pending-to-acknowledged receiver transition is permitted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryRequisitions r ON r.Id = i.InventoryRequisitionId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                        WHERE r.Id IS NULL OR r.WarehouseId <> i.WarehouseId OR r.DepartmentId <> i.DepartmentId
                           OR r.RequestedById <> i.RequestedById OR r.ApprovedById <> i.ApprovedById
                           OR r.Status NOT IN (3,4,5,6)
                           OR (i.ProjectId IS NULL AND (i.CostCenter IS NULL OR LEN(LTRIM(RTRIM(i.CostCenter))) = 0))
                           OR (i.LocationId IS NOT NULL AND (l.Id IS NULL OR l.WarehouseId <> i.WarehouseId)))
                        THROW 51603, 'INV_ISSUE_APPROVED_SOURCE_REQUIRED: voucher lineage must match an approved tenant requisition and cost object.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE EXISTS (
                            SELECT required.UserId
                            FROM (VALUES (i.RequestedById), (i.ApprovedById), (i.IssuedById), (i.ReceiverUserId)) required(UserId)
                            WHERE NOT EXISTS (
                                SELECT 1 FROM UserTenants ut
                                INNER JOIN Users u ON u.Id = ut.UserId AND u.IsActive = 1
                                WHERE ut.TenantId = i.TenantId AND ut.UserId = required.UserId AND ut.IsDeleted = 0
                                  AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME()))))
                        THROW 51604, 'INV_ISSUE_ACTOR_INVALID: all issue actors must be active users in the same tenant.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueVoucherLines_AppendOnly]
                ON [dbo].[InventoryIssueVoucherLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51611, 'INV_ISSUE_LINE_IMMUTABLE: issued voucher lines cannot be changed or deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryIssueVouchers v ON v.Id = i.InventoryIssueVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        LEFT JOIN InventoryRequisitionItems r ON r.Id = i.InventoryRequisitionItemId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN InventoryItems item ON item.Id = i.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                        WHERE v.Id IS NULL OR r.Id IS NULL OR item.Id IS NULL OR r.InventoryRequisitionId <> v.InventoryRequisitionId
                           OR r.InventoryItemId <> i.InventoryItemId OR i.WarehouseId <> v.WarehouseId
                           OR r.IssuedQuantity > r.ApprovedQuantity
                           OR (SELECT COALESCE(SUM(line.Quantity), 0)
                               FROM InventoryIssueVoucherLines line
                               WHERE line.TenantId = i.TenantId
                                 AND line.InventoryRequisitionItemId = i.InventoryRequisitionItemId
                                 AND line.IsDeleted = 0) > r.IssuedQuantity
                           OR (i.LocationId IS NOT NULL AND (l.Id IS NULL OR l.WarehouseId <> i.WarehouseId)))
                        THROW 51612, 'INV_ISSUE_LINE_SOURCE_INVALID: voucher lines must match their tenant requisition, item, warehouse and location.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryIssueVoucherActions_AppendOnly]
                ON [dbo].[InventoryIssueVoucherActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51621, 'INV_ISSUE_ACTION_IMMUTABLE: voucher actions cannot be changed or deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryIssueVouchers v ON v.Id = i.InventoryIssueVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        WHERE v.Id IS NULL OR i.StatusAfter <> v.Status
                           OR (i.ActionType = 1 AND (i.Sequence <> 1 OR i.ActorUserId <> v.IssuedById OR i.StatusAfter <> 1))
                           OR (i.ActionType = 2 AND (i.Sequence <> 2 OR i.ActorUserId <> v.ReceiverUserId OR i.StatusAfter <> 2)))
                        THROW 51622, 'INV_ISSUE_ACTION_INVALID: action actor, sequence and status must match the controlled voucher lifecycle.', 1;
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
                          AND (i.TenantId <> d.TenantId OR i.InventoryItemId <> d.InventoryItemId
                               OR i.WarehouseId <> d.WarehouseId OR ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId, '00000000-0000-0000-0000-000000000000')
                               OR i.Quantity <> d.Quantity OR i.ReferenceType <> d.ReferenceType
                               OR ISNULL(i.ReferenceId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ReferenceId, '00000000-0000-0000-0000-000000000000')
                               OR ISNULL(i.InventoryIssueVoucherId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.InventoryIssueVoucherId, '00000000-0000-0000-0000-000000000000')
                               OR ISNULL(i.ProcessedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProcessedById, '00000000-0000-0000-0000-000000000000')))
                        THROW 51631, 'INV_ISSUE_MOVEMENT_IMMUTABLE: requisition issue movement lineage and quantity are immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryIssueVouchers v ON v.Id = i.InventoryIssueVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        OUTER APPLY (
                            SELECT COALESCE(SUM(line.Quantity), 0) Quantity
                            FROM InventoryIssueVoucherLines line
                            WHERE line.InventoryIssueVoucherId = i.InventoryIssueVoucherId AND line.TenantId = i.TenantId
                              AND line.InventoryItemId = i.InventoryItemId AND line.WarehouseId = i.WarehouseId
                              AND ISNULL(line.LocationId, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000')
                              AND line.IsDeleted = 0) allowed
                        OUTER APPLY (
                            SELECT COALESCE(SUM(ABS(movement.Quantity)), 0) Quantity
                            FROM StockMovements movement WITH (UPDLOCK, HOLDLOCK)
                            WHERE movement.InventoryIssueVoucherId = i.InventoryIssueVoucherId AND movement.TenantId = i.TenantId
                              AND movement.InventoryItemId = i.InventoryItemId AND movement.WarehouseId = i.WarehouseId
                              AND ISNULL(movement.LocationId, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000')
                              AND movement.MovementType = N'Issue' AND movement.IsDeleted = 0) posted
                        WHERE i.IsDeleted = 0 AND i.MovementType = N'Issue'
                          AND (i.ReferenceType <> 11 OR i.ReferenceId IS NULL OR i.Quantity >= 0 OR v.Id IS NULL
                               OR v.InventoryRequisitionId <> i.ReferenceId OR v.WarehouseId <> i.WarehouseId
                               OR v.IssuedById <> i.ProcessedById OR posted.Quantity > allowed.Quantity))
                        THROW 51632, 'INV_ISSUE_APPROVED_VOUCHER_REQUIRED: requisition issue stock requires matching controlled voucher quantity and issuer.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_GovernedRequisitionIssue];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryIssueVoucherActions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryIssueVoucherLines_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryIssueVouchers_ControlledLifecycle];");
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryIssueVouchers_InventoryIssueVoucherId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "InventoryIssueVoucherActions");

            migrationBuilder.DropTable(
                name: "InventoryIssueVoucherLines");

            migrationBuilder.DropTable(
                name: "InventoryIssueVouchers");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_InventoryIssueVoucherId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "InventoryIssueVoucherId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryRequisitions");
        }
    }
}
