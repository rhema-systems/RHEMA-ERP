using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0607ControlledInventoryReturnsAndAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_TenantId",
                table: "StockAdjustments");

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryReturnVoucherId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "StockAdjustments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceJournalEntryId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancePostingEventId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "StockAdjustments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityHash",
                table: "StockAdjustments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "StockAdjustments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedAtUtc",
                table: "StockAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostedById",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtUtc",
                table: "StockAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedById",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "StockAdjustments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedIssueVoucherId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedById",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalFinanceJournalEntryId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalFinancePostingEventId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "StockAdjustments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "StockAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedById",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StockAdjustments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "StockAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedById",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryReturnVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InventoryRequisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PostedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryReturnVouchers", x => x.Id);
                    table.CheckConstraint("CK_InventoryReturnVouchers_Status", "[Status] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_InventoryReturnVouchers_TotalValue", "[TotalValue] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryReturnVouchers_InventoryRequisitions_InventoryRequisitionId",
                        column: x => x.InventoryRequisitionId,
                        principalTable: "InventoryRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVouchers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVouchers_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVouchers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_StockAdjustmentActions", x => x.Id);
                    table.CheckConstraint("CK_StockAdjustmentActions_Sequence", "[Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_StockAdjustmentActions_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalTable: "StockAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_StockAdjustmentEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentEvidence_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalTable: "StockAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReturnVoucherActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryReturnVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_InventoryReturnVoucherActions", x => x.Id);
                    table.CheckConstraint("CK_InventoryReturnVoucherActions_Sequence", "[Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherActions_InventoryReturnVouchers_InventoryReturnVoucherId",
                        column: x => x.InventoryReturnVoucherId,
                        principalTable: "InventoryReturnVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReturnVoucherEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryReturnVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_InventoryReturnVoucherEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherEvidence_InventoryReturnVouchers_InventoryReturnVoucherId",
                        column: x => x.InventoryReturnVoucherId,
                        principalTable: "InventoryReturnVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReturnVoucherLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryReturnVoucherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryRequisitionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryReturnVoucherLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryReturnVoucherLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryReturnVoucherLines_Value", "[UnitCost] >= 0 AND [TotalValue] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherLines_InventoryRequisitionItems_InventoryRequisitionItemId",
                        column: x => x.InventoryRequisitionItemId,
                        principalTable: "InventoryRequisitionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherLines_InventoryReturnVouchers_InventoryReturnVoucherId",
                        column: x => x.InventoryReturnVoucherId,
                        principalTable: "InventoryReturnVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReturnVoucherLines_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryReturnVoucherId",
                table: "StockMovements",
                column: "InventoryReturnVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_TenantId_IdempotencyKey",
                table: "StockAdjustments",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherActions_ActorUserId",
                table: "InventoryReturnVoucherActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherActions_InventoryReturnVoucherId_Sequence",
                table: "InventoryReturnVoucherActions",
                columns: new[] { "InventoryReturnVoucherId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherActions_TenantId_IdempotencyKey",
                table: "InventoryReturnVoucherActions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherEvidence_CentralDocumentVersionId",
                table: "InventoryReturnVoucherEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherEvidence_FileUploadRecordId",
                table: "InventoryReturnVoucherEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherEvidence_InventoryReturnVoucherId_CentralDocumentVersionId",
                table: "InventoryReturnVoucherEvidence",
                columns: new[] { "InventoryReturnVoucherId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherEvidence_TenantId",
                table: "InventoryReturnVoucherEvidence",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherLines_InventoryItemId",
                table: "InventoryReturnVoucherLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherLines_InventoryRequisitionItemId",
                table: "InventoryReturnVoucherLines",
                column: "InventoryRequisitionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherLines_InventoryReturnVoucherId_InventoryRequisitionItemId",
                table: "InventoryReturnVoucherLines",
                columns: new[] { "InventoryReturnVoucherId", "InventoryRequisitionItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherLines_LocationId",
                table: "InventoryReturnVoucherLines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVoucherLines_TenantId",
                table: "InventoryReturnVoucherLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVouchers_InventoryRequisitionId",
                table: "InventoryReturnVouchers",
                column: "InventoryRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVouchers_RequestedById",
                table: "InventoryReturnVouchers",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVouchers_TenantId_IdempotencyKey",
                table: "InventoryReturnVouchers",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVouchers_TenantId_InventoryRequisitionId_Status",
                table: "InventoryReturnVouchers",
                columns: new[] { "TenantId", "InventoryRequisitionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVouchers_TenantId_VoucherNumber",
                table: "InventoryReturnVouchers",
                columns: new[] { "TenantId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReturnVouchers_WarehouseId",
                table: "InventoryReturnVouchers",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentActions_ActorUserId",
                table: "StockAdjustmentActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentActions_StockAdjustmentId_Sequence",
                table: "StockAdjustmentActions",
                columns: new[] { "StockAdjustmentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentActions_TenantId_IdempotencyKey",
                table: "StockAdjustmentActions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentEvidence_CentralDocumentVersionId",
                table: "StockAdjustmentEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentEvidence_FileUploadRecordId",
                table: "StockAdjustmentEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentEvidence_StockAdjustmentId_CentralDocumentVersionId",
                table: "StockAdjustmentEvidence",
                columns: new[] { "StockAdjustmentId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentEvidence_TenantId",
                table: "StockAdjustmentEvidence",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_InventoryReturnVouchers_InventoryReturnVoucherId",
                table: "StockMovements",
                column: "InventoryReturnVoucherId",
                principalTable: "InventoryReturnVouchers",
                principalColumn: "Id");

            // Preserve legacy rows without silently assigning a fabricated maker. Rows that
            // cannot be attributed remain quarantined from controlled lifecycle transitions.
            migrationBuilder.Sql(
                """
                UPDATE sa
                SET RequestedById = COALESCE(sa.CreatedById, sa.ApprovedById)
                FROM StockAdjustments sa
                WHERE sa.RequestedById = '00000000-0000-0000-0000-000000000000'
                  AND COALESCE(sa.CreatedById, sa.ApprovedById) IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryReturnVouchers_ControlledLifecycle]
                ON [dbo].[InventoryReturnVouchers]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51641, 'INV_RETURN_DELETE_BLOCKED: Store Return Vouchers are immutable evidence.', 1;

                    IF EXISTS (SELECT 1 FROM inserted WHERE IsDeleted = 1)
                        THROW 51642, 'INV_RETURN_SOFT_DELETE_BLOCKED: Store Return Vouchers cannot be soft deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE d.Id IS NULL AND
                            (i.Status <> 1 OR i.RequestedById = '00000000-0000-0000-0000-000000000000'
                             OR LEN(LTRIM(RTRIM(i.VoucherNumber))) = 0 OR LEN(LTRIM(RTRIM(i.ReasonCode))) = 0
                             OR LEN(LTRIM(RTRIM(i.Reason))) = 0 OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0
                             OR LEN(i.PayloadHash) <> 64 OR LEN(i.IntegrityHash) <> 64 OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0
                             OR i.WorkflowInstanceId IS NOT NULL OR i.ApprovedById IS NOT NULL OR i.RejectedById IS NOT NULL
                             OR i.PostedById IS NOT NULL OR i.ReversedById IS NOT NULL))
                        THROW 51643, 'INV_RETURN_INITIAL_STATE_INVALID: returns must enter as complete pending-approval evidence.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.InventoryRequisitionId <> d.InventoryRequisitionId
                           OR i.WarehouseId <> d.WarehouseId OR i.RequestedById <> d.RequestedById
                           OR i.VoucherNumber <> d.VoucherNumber OR i.ReasonCode <> d.ReasonCode OR i.Reason <> d.Reason
                           OR ISNULL(i.Notes, N'') <> ISNULL(d.Notes, N'') OR i.IdempotencyKey <> d.IdempotencyKey
                           OR i.PayloadHash <> d.PayloadHash OR i.CorrelationId <> d.CorrelationId OR i.TotalValue <> d.TotalValue
                           OR i.CreatedAt <> d.CreatedAt OR ISNULL(i.CreatedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.CreatedById, '00000000-0000-0000-0000-000000000000'))
                        THROW 51644, 'INV_RETURN_SOURCE_IMMUTABLE: return source, reason, value and idempotency lineage cannot change.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            (d.Status = 1 AND i.Status = 1
                             AND d.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NOT NULL
                             AND ISNULL(d.ApprovedById, '00000000-0000-0000-0000-000000000000') = ISNULL(i.ApprovedById, '00000000-0000-0000-0000-000000000000')
                             AND ISNULL(d.RejectedById, '00000000-0000-0000-0000-000000000000') = ISNULL(i.RejectedById, '00000000-0000-0000-0000-000000000000')
                             AND ISNULL(d.PostedById, '00000000-0000-0000-0000-000000000000') = ISNULL(i.PostedById, '00000000-0000-0000-0000-000000000000')
                             AND ISNULL(d.ReversedById, '00000000-0000-0000-0000-000000000000') = ISNULL(i.ReversedById, '00000000-0000-0000-0000-000000000000'))
                         OR (d.Status = 1 AND i.Status = 2 AND i.WorkflowInstanceId IS NOT NULL
                             AND i.ApprovedById IS NOT NULL AND i.ApprovedAtUtc IS NOT NULL AND i.ApprovedById <> i.RequestedById)
                         OR (d.Status = 1 AND i.Status = 3 AND i.WorkflowInstanceId IS NOT NULL
                             AND i.RejectedById IS NOT NULL AND i.RejectedAtUtc IS NOT NULL
                             AND LEN(LTRIM(RTRIM(i.RejectionReason))) > 0 AND i.RejectedById <> i.RequestedById)
                         OR (d.Status = 2 AND i.Status = 4 AND i.PostedById IS NOT NULL
                             AND i.PostedAtUtc IS NOT NULL AND i.PostedById <> i.RequestedById)
                         OR (d.Status = 4 AND i.Status = 5 AND i.ReversedById IS NOT NULL
                             AND i.ReversedAtUtc IS NOT NULL AND LEN(LTRIM(RTRIM(i.ReversalReason))) > 0
                             AND i.ReversedById <> i.RequestedById)))
                        THROW 51645, 'INV_RETURN_TRANSITION_INVALID: only pending workflow, independent decision, post and reversal transitions are allowed.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryRequisitions r ON r.Id = i.InventoryRequisitionId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId AND w.IsDeleted = 0 AND w.IsActive = 1
                        WHERE r.Id IS NULL OR w.Id IS NULL OR r.WarehouseId <> i.WarehouseId OR r.Status NOT IN (5,6,7))
                        THROW 51646, 'INV_RETURN_SOURCE_INVALID: return lineage requires an issued tenant requisition and active source warehouse.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i CROSS APPLY (VALUES
                            (i.RequestedById), (i.ApprovedById), (i.RejectedById), (i.PostedById), (i.ReversedById)) actor(UserId)
                        WHERE actor.UserId IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM UserTenants ut INNER JOIN Users u ON u.Id = ut.UserId AND u.IsActive = 1
                            WHERE ut.TenantId = i.TenantId AND ut.UserId = actor.UserId AND ut.IsDeleted = 0
                              AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())))
                        THROW 51647, 'INV_RETURN_ACTOR_INVALID: every return actor must be active in the same tenant.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryReturnVoucherEvidence_AppendOnly]
                ON [dbo].[InventoryReturnVoucherEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51661, 'INV_RETURN_EVIDENCE_IMMUTABLE: linked return evidence cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryReturnVouchers v ON v.Id = i.InventoryReturnVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions dv ON dv.Id = i.CentralDocumentVersionId AND dv.TenantId = i.TenantId AND dv.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords dr ON dr.Id = dv.DocumentRecordId AND dr.TenantId = i.TenantId AND dr.IsDeleted = 0
                        LEFT JOIN FileUploadRecords f ON f.Id = i.FileUploadRecordId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                        WHERE v.Id IS NULL OR v.Status <> 1 OR dv.Id IS NULL OR dr.Id IS NULL OR f.Id IS NULL
                           OR dv.FileUploadRecordId <> i.FileUploadRecordId OR dv.Status <> N'Published' OR dv.PublishedAt IS NULL
                           OR dr.LifecycleStatus <> N'Active' OR dr.VersionStatus <> N'Published' OR dr.CurrentVersion <> dv.VersionNumber
                           OR LEN(LTRIM(RTRIM(i.EvidenceReference))) = 0 OR LEN(i.IntegrityHash) <> 64)
                        THROW 51662, 'INV_RETURN_EVIDENCE_INVALID: evidence must be the tenant current published central-DMS version and file.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryReturnVoucherActions_AppendOnly]
                ON [dbo].[InventoryReturnVoucherActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51671, 'INV_RETURN_ACTION_IMMUTABLE: return actions cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryReturnVouchers v ON v.Id = i.InventoryReturnVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        LEFT JOIN UserTenants ut ON ut.TenantId = i.TenantId AND ut.UserId = i.ActorUserId
                            AND ut.IsDeleted = 0 AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())
                        LEFT JOIN Users u ON u.Id = i.ActorUserId AND u.IsActive = 1
                        WHERE v.Id IS NULL OR ut.Id IS NULL OR u.Id IS NULL OR i.Sequence <= 0
                           OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0 OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0
                           OR LEN(i.IntegrityHash) <> 64
                           OR (i.ActionType = 1 AND (i.Sequence <> 1 OR v.Status <> 1 OR i.ActorUserId <> v.RequestedById))
                           OR (i.ActionType = 2 AND (v.Status <> 2 OR i.ActorUserId <> v.ApprovedById))
                           OR (i.ActionType = 3 AND (v.Status <> 3 OR i.ActorUserId <> v.RejectedById))
                           OR (i.ActionType = 4 AND (v.Status <> 4 OR i.ActorUserId <> v.PostedById))
                           OR (i.ActionType = 5 AND (v.Status <> 5 OR i.ActorUserId <> v.ReversedById))
                           OR i.ActionType NOT BETWEEN 1 AND 5)
                        THROW 51672, 'INV_RETURN_ACTION_INVALID: action actor, sequence and lifecycle state must match the controlled return.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockAdjustments_ControlledLifecycle]
                ON [dbo].[StockAdjustments]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51681, 'INV_ADJUSTMENT_DELETE_BLOCKED: use the controlled Draft soft-delete operation.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE d.Id IS NULL AND
                            (i.Status <> N'Draft' OR i.IsDeleted = 1 OR i.RequestedById = '00000000-0000-0000-0000-000000000000'
                             OR LEN(LTRIM(RTRIM(i.AdjustmentNumber))) = 0 OR LEN(LTRIM(RTRIM(i.ReasonCode))) = 0
                             OR LEN(LTRIM(RTRIM(i.Description))) = 0 OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0
                             OR LEN(i.PayloadHash) <> 64 OR LEN(i.IntegrityHash) <> 64 OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0))
                        THROW 51682, 'INV_ADJUSTMENT_INITIAL_STATE_INVALID: adjustments must enter as attributable complete Drafts.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.IsDeleted = 1 AND (d.Status <> N'Draft' OR i.Status <> N'Draft'))
                        THROW 51683, 'INV_ADJUSTMENT_DELETE_STATE: only a Draft adjustment may be soft deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status <> N'Draft' AND
                            (i.TenantId <> d.TenantId OR i.AdjustmentNumber <> d.AdjustmentNumber OR i.WarehouseId <> d.WarehouseId
                             OR i.Reference <> d.Reference OR i.AdjustmentDate <> d.AdjustmentDate OR i.ReasonCode <> d.ReasonCode
                             OR ISNULL(i.Description, N'') <> ISNULL(d.Description, N'') OR i.TotalAdjustmentValue <> d.TotalAdjustmentValue
                             OR i.RequestedById <> d.RequestedById
                             OR ISNULL(i.RelatedIssueVoucherId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.RelatedIssueVoucherId, '00000000-0000-0000-0000-000000000000')
                             OR ISNULL(i.IdempotencyKey, N'') <> ISNULL(d.IdempotencyKey, N'')
                             OR ISNULL(i.PayloadHash, N'') <> ISNULL(d.PayloadHash, N'') OR ISNULL(i.CorrelationId, N'') <> ISNULL(d.CorrelationId, N'')
                             OR i.IsDeleted <> d.IsDeleted))
                        THROW 51684, 'INV_ADJUSTMENT_SOURCE_IMMUTABLE: submitted adjustment source and value cannot change.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status AND NOT (
                            (d.Status = N'Draft' AND i.Status = N'PendingApproval'
                             AND i.WorkflowInstanceId IS NOT NULL AND i.SubmittedById IS NOT NULL AND i.SubmittedAtUtc IS NOT NULL)
                         OR (d.Status = N'Draft' AND i.Status = N'Cancelled')
                         OR (d.Status = N'PendingApproval' AND i.Status = N'Approved'
                             AND i.ApprovedById IS NOT NULL AND i.ApprovedAt IS NOT NULL AND i.ApprovedById <> i.RequestedById)
                         OR (d.Status = N'PendingApproval' AND i.Status = N'Rejected'
                             AND i.RejectedById IS NOT NULL AND i.RejectedAtUtc IS NOT NULL
                             AND LEN(LTRIM(RTRIM(i.RejectionReason))) > 0 AND i.RejectedById <> i.RequestedById)
                         OR (d.Status = N'Approved' AND i.Status = N'Posted'
                             AND i.PostedById IS NOT NULL AND i.PostedAtUtc IS NOT NULL AND i.PostedById <> i.RequestedById
                             AND i.FinancePostingEventId IS NOT NULL AND i.FinanceJournalEntryId IS NOT NULL)
                         OR (d.Status = N'Posted' AND i.Status = N'Reversed'
                             AND i.ReversedById IS NOT NULL AND i.ReversedAtUtc IS NOT NULL AND i.ReversedById <> i.RequestedById
                             AND LEN(LTRIM(RTRIM(i.ReversalReason))) > 0
                             AND i.ReversalFinancePostingEventId IS NOT NULL AND i.ReversalFinanceJournalEntryId IS NOT NULL)))
                        THROW 51685, 'INV_ADJUSTMENT_TRANSITION_INVALID: submit, independent decision, finance-backed post and reversal are required.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId AND w.IsDeleted = 0 AND w.IsActive = 1
                        WHERE w.Id IS NULL OR i.Status NOT IN (N'Draft', N'PendingApproval', N'Approved', N'Rejected', N'Posted', N'Reversed', N'Cancelled'))
                        THROW 51686, 'INV_ADJUSTMENT_SCOPE_INVALID: adjustment requires an active tenant warehouse and controlled status.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i CROSS APPLY (VALUES
                            (i.RequestedById), (i.SubmittedById), (i.ApprovedById), (i.RejectedById), (i.PostedById), (i.ReversedById)) actor(UserId)
                        WHERE actor.UserId IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM UserTenants ut INNER JOIN Users u ON u.Id = ut.UserId AND u.IsActive = 1
                            WHERE ut.TenantId = i.TenantId AND ut.UserId = actor.UserId AND ut.IsDeleted = 0
                              AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())))
                        THROW 51687, 'INV_ADJUSTMENT_ACTOR_INVALID: every adjustment actor must be active in the same tenant.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.Status = N'Posted' AND NOT EXISTS (
                            SELECT 1 FROM FinancePostingEvents f
                            WHERE f.Id = i.FinancePostingEventId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                              AND f.SourceModule = N'Inventory' AND f.SourceDocumentType = N'StockAdjustment'
                              AND f.SourceDocumentId = i.Id AND f.PostingAction = N'PostStockAdjustment'
                              AND f.PostingStatus = N'Posted' AND f.JournalEntryId = i.FinanceJournalEntryId))
                        THROW 51688, 'INV_ADJUSTMENT_FINANCE_LINEAGE: posted adjustment must reference its successful Finance posting and journal.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.Status = N'Reversed' AND NOT EXISTS (
                            SELECT 1 FROM FinancePostingEvents f
                            WHERE f.Id = i.ReversalFinancePostingEventId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                              AND f.SourceModule = N'Inventory' AND f.SourceDocumentType = N'StockAdjustment'
                              AND f.SourceDocumentId = i.Id AND f.PostingAction = N'ReverseStockAdjustment'
                              AND f.PostingStatus = N'Posted' AND f.JournalEntryId = i.ReversalFinanceJournalEntryId))
                        THROW 51689, 'INV_ADJUSTMENT_REVERSAL_FINANCE_LINEAGE: reversal must reference its successful Finance posting and journal.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockAdjustmentItems_ControlledMutation]
                ON [dbo].[StockAdjustmentItems]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN StockAdjustments a ON a.Id = d.AdjustmentId AND a.TenantId = d.TenantId
                        WHERE a.Id IS NULL OR a.Status <> N'Draft')
                        THROW 51691, 'INV_ADJUSTMENT_LINE_IMMUTABLE: only Draft adjustment lines can be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN StockAdjustments a ON a.Id = i.AdjustmentId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                        LEFT JOIN InventoryItems item ON item.Id = i.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0 AND l.IsActive = 1
                        WHERE a.Id IS NULL OR a.Status <> N'Draft' OR item.Id IS NULL OR i.LocationId IS NULL
                           OR l.Id IS NULL OR l.WarehouseId <> a.WarehouseId OR i.AdjustmentQuantity = 0
                           OR i.PhysicalQuantity <> i.SystemQuantity + i.AdjustmentQuantity
                           OR i.UnitCost <= 0 OR i.UnitCost <> CASE WHEN item.AverageCost > 0 THEN item.AverageCost
                                WHEN item.StandardCost > 0 THEN item.StandardCost ELSE item.LastPurchaseCost END
                           OR i.AdjustmentValue <> ROUND(i.AdjustmentQuantity * i.UnitCost, 2))
                        THROW 51692, 'INV_ADJUSTMENT_LINE_INVALID: lines require a non-zero server-valued delta at an exact active location.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockAdjustmentEvidence_AppendOnly]
                ON [dbo].[StockAdjustmentEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51701, 'INV_ADJUSTMENT_EVIDENCE_IMMUTABLE: linked adjustment evidence cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions dv ON dv.Id = i.CentralDocumentVersionId AND dv.TenantId = i.TenantId AND dv.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords dr ON dr.Id = dv.DocumentRecordId AND dr.TenantId = i.TenantId AND dr.IsDeleted = 0
                        LEFT JOIN FileUploadRecords f ON f.Id = i.FileUploadRecordId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                        WHERE a.Id IS NULL OR a.Status <> N'Draft' OR dv.Id IS NULL OR dr.Id IS NULL OR f.Id IS NULL
                           OR dv.FileUploadRecordId <> i.FileUploadRecordId OR dv.Status <> N'Published' OR dv.PublishedAt IS NULL
                           OR dr.LifecycleStatus <> N'Active' OR dr.VersionStatus <> N'Published' OR dr.CurrentVersion <> dv.VersionNumber
                           OR LEN(LTRIM(RTRIM(i.EvidenceReference))) = 0 OR LEN(i.IntegrityHash) <> 64)
                        THROW 51702, 'INV_ADJUSTMENT_EVIDENCE_INVALID: evidence must be the tenant current published central-DMS version and file.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockAdjustmentActions_AppendOnly]
                ON [dbo].[StockAdjustmentActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51711, 'INV_ADJUSTMENT_ACTION_IMMUTABLE: adjustment actions cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
                        LEFT JOIN UserTenants ut ON ut.TenantId = i.TenantId AND ut.UserId = i.ActorUserId
                            AND ut.IsDeleted = 0 AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())
                        LEFT JOIN Users u ON u.Id = i.ActorUserId AND u.IsActive = 1
                        WHERE a.Id IS NULL OR ut.Id IS NULL OR u.Id IS NULL OR i.Sequence <= 0
                           OR LEN(LTRIM(RTRIM(i.ActionType))) = 0 OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0
                           OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0 OR LEN(i.IntegrityHash) <> 64
                           OR (i.ActionType IN (N'Created', N'Updated', N'LineDeleted') AND a.Status <> N'Draft')
                           OR (i.ActionType = N'Submitted' AND a.Status <> N'PendingApproval')
                           OR (i.ActionType = N'Approved' AND (a.Status <> N'Approved' OR i.ActorUserId <> a.ApprovedById))
                           OR (i.ActionType = N'Rejected' AND (a.Status <> N'Rejected' OR i.ActorUserId <> a.RejectedById))
                           OR (i.ActionType = N'Posted' AND (a.Status <> N'Posted' OR i.ActorUserId <> a.PostedById))
                           OR (i.ActionType = N'Reversed' AND (a.Status <> N'Reversed' OR i.ActorUserId <> a.ReversedById))
                           OR (i.ActionType = N'Cancelled' AND a.Status <> N'Cancelled')
                           OR i.ActionType NOT IN (N'Created', N'Updated', N'LineDeleted', N'Submitted', N'Approved', N'Rejected', N'Posted', N'Reversed', N'Cancelled'))
                        THROW 51712, 'INV_ADJUSTMENT_ACTION_INVALID: action actor and lifecycle state must match the controlled adjustment.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_GovernedInventoryReturnAdjustment]
                ON [dbo].[StockMovements]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE (i.MovementType IN (N'Return', N'ReturnReversal', N'Adjustment+', N'Adjustment-', N'AdjustmentReversal')
                            OR d.MovementType IN (N'Return', N'ReturnReversal', N'Adjustment+', N'Adjustment-', N'AdjustmentReversal'))
                          AND (i.TenantId <> d.TenantId OR i.InventoryItemId <> d.InventoryItemId OR i.WarehouseId <> d.WarehouseId
                            OR ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId, '00000000-0000-0000-0000-000000000000')
                            OR i.MovementType <> d.MovementType OR i.Quantity <> d.Quantity OR i.UnitCost <> d.UnitCost OR i.TotalValue <> d.TotalValue
                            OR i.ReferenceType <> d.ReferenceType OR ISNULL(i.ReferenceNumber, N'') <> ISNULL(d.ReferenceNumber, N'')
                            OR ISNULL(i.ReferenceId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ReferenceId, '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(i.InventoryReturnVoucherId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.InventoryReturnVoucherId, '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(i.ProcessedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProcessedById, '00000000-0000-0000-0000-000000000000')))
                        THROW 51721, 'INV_CONTROLLED_MOVEMENT_IMMUTABLE: governed return and adjustment movement lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryReturnVouchers v ON v.Id = i.InventoryReturnVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        OUTER APPLY (
                            SELECT COALESCE(SUM(line.Quantity), 0) Quantity, COALESCE(SUM(line.TotalValue), 0) TotalValue
                            FROM InventoryReturnVoucherLines line
                            WHERE line.InventoryReturnVoucherId = i.InventoryReturnVoucherId AND line.TenantId = i.TenantId
                              AND line.InventoryItemId = i.InventoryItemId
                              AND ISNULL(line.LocationId, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000')) allowed
                        OUTER APPLY (
                            SELECT COALESCE(SUM(ABS(m.Quantity)), 0) Quantity, COALESCE(SUM(ABS(m.TotalValue)), 0) TotalValue
                            FROM StockMovements m WITH (UPDLOCK, HOLDLOCK)
                            WHERE m.InventoryReturnVoucherId = i.InventoryReturnVoucherId AND m.TenantId = i.TenantId
                              AND m.InventoryItemId = i.InventoryItemId AND m.MovementType = i.MovementType
                              AND ISNULL(m.LocationId, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000')
                              AND m.IsDeleted = 0) posted
                        WHERE i.IsDeleted = 0 AND i.MovementType IN (N'Return', N'ReturnReversal')
                          AND (v.Id IS NULL OR i.ReferenceType <> 11 OR i.ReferenceId <> v.InventoryRequisitionId
                               OR i.ReferenceNumber <> v.VoucherNumber OR i.WarehouseId <> v.WarehouseId
                               OR (i.MovementType = N'Return' AND (v.Status <> 4 OR i.Quantity <= 0 OR i.ProcessedById <> v.PostedById))
                               OR (i.MovementType = N'ReturnReversal' AND (v.Status <> 5 OR i.Quantity >= 0 OR i.ProcessedById <> v.ReversedById))
                               OR posted.Quantity > allowed.Quantity OR posted.TotalValue > allowed.TotalValue))
                        THROW 51722, 'INV_RETURN_POSTED_VOUCHER_REQUIRED: return movements require matching controlled voucher value, quantity and actor.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN StockAdjustments a ON a.Id = i.ReferenceId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                        OUTER APPLY (
                            SELECT COALESCE(SUM(ABS(line.AdjustmentQuantity)), 0) Quantity,
                                   COALESCE(SUM(ABS(line.AdjustmentValue)), 0) TotalValue
                            FROM StockAdjustmentItems line
                            WHERE line.AdjustmentId = i.ReferenceId AND line.TenantId = i.TenantId
                              AND line.InventoryItemId = i.InventoryItemId
                              AND ISNULL(line.LocationId, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000')
                              AND line.IsDeleted = 0) allowed
                        OUTER APPLY (
                            SELECT COALESCE(SUM(ABS(m.Quantity)), 0) Quantity, COALESCE(SUM(ABS(m.TotalValue)), 0) TotalValue
                            FROM StockMovements m WITH (UPDLOCK, HOLDLOCK)
                            WHERE m.ReferenceType = 5 AND m.ReferenceId = i.ReferenceId AND m.TenantId = i.TenantId
                              AND m.InventoryItemId = i.InventoryItemId AND m.MovementType = i.MovementType
                              AND ISNULL(m.LocationId, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000')
                              AND m.IsDeleted = 0) posted
                        WHERE i.IsDeleted = 0 AND i.MovementType IN (N'Adjustment+', N'Adjustment-', N'AdjustmentReversal')
                          AND (a.Id IS NULL OR i.ReferenceType <> 5 OR i.ReferenceNumber <> a.AdjustmentNumber
                               OR i.InventoryReturnVoucherId IS NOT NULL
                               OR (i.MovementType IN (N'Adjustment+', N'Adjustment-') AND (a.Status <> N'Posted' OR i.ProcessedById <> a.PostedById))
                               OR (i.MovementType = N'AdjustmentReversal' AND (a.Status <> N'Reversed' OR i.ProcessedById <> a.ReversedById))
                               OR posted.Quantity > allowed.Quantity OR posted.TotalValue > allowed.TotalValue))
                        THROW 51723, 'INV_ADJUSTMENT_POSTED_CONTROL_REQUIRED: adjustment movements require finance-backed controlled parent value, quantity and actor.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryReturnVoucherLines_AppendOnly]
                ON [dbo].[InventoryReturnVoucherLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51651, 'INV_RETURN_LINE_IMMUTABLE: return voucher lines cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryReturnVouchers v ON v.Id = i.InventoryReturnVoucherId AND v.TenantId = i.TenantId AND v.IsDeleted = 0
                        LEFT JOIN InventoryRequisitionItems r ON r.Id = i.InventoryRequisitionItemId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN InventoryItems item ON item.Id = i.InventoryItemId AND item.TenantId = i.TenantId AND item.IsDeleted = 0
                        LEFT JOIN WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.IsDeleted = 0 AND l.IsActive = 1
                        OUTER APPLY (
                            SELECT COALESCE(SUM(line.Quantity), 0) Quantity
                            FROM InventoryReturnVoucherLines line
                            INNER JOIN InventoryReturnVouchers existingVoucher ON existingVoucher.Id = line.InventoryReturnVoucherId
                            WHERE line.TenantId = i.TenantId AND line.InventoryRequisitionItemId = i.InventoryRequisitionItemId
                              AND line.IsDeleted = 0 AND existingVoucher.IsDeleted = 0 AND existingVoucher.Status IN (1,2,4)) reserved
                        WHERE v.Id IS NULL OR v.Status <> 1 OR r.Id IS NULL OR item.Id IS NULL
                           OR r.InventoryRequisitionId <> v.InventoryRequisitionId OR r.InventoryItemId <> i.InventoryItemId
                           OR i.LocationId IS NULL OR l.Id IS NULL OR l.WarehouseId <> v.WarehouseId
                           OR i.Quantity <= 0 OR i.UnitCost <= 0 OR i.UnitCost <> r.UnitCost
                           OR i.TotalValue <> ROUND(i.Quantity * i.UnitCost, 2) OR LEN(i.IntegrityHash) <> 64
                           OR reserved.Quantity > r.IssuedQuantity)
                        THROW 51652, 'INV_RETURN_LINE_SOURCE_INVALID: lines must match issued quantity, item, cost and exact active location.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_GovernedInventoryReturnAdjustment];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockAdjustmentActions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockAdjustmentEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockAdjustmentItems_ControlledMutation];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockAdjustments_ControlledLifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryReturnVoucherActions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryReturnVoucherEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryReturnVoucherLines_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryReturnVouchers_ControlledLifecycle];");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryReturnVouchers_InventoryReturnVoucherId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "InventoryReturnVoucherActions");

            migrationBuilder.DropTable(
                name: "InventoryReturnVoucherEvidence");

            migrationBuilder.DropTable(
                name: "InventoryReturnVoucherLines");

            migrationBuilder.DropTable(
                name: "StockAdjustmentActions");

            migrationBuilder.DropTable(
                name: "StockAdjustmentEvidence");

            migrationBuilder.DropTable(
                name: "InventoryReturnVouchers");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_InventoryReturnVoucherId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_TenantId_IdempotencyKey",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "InventoryReturnVoucherId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "FinanceJournalEntryId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "FinancePostingEventId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "IntegrityHash",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "PostedAtUtc",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "PostedById",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "RelatedIssueVoucherId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "RequestedById",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ReversalFinanceJournalEntryId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ReversalFinancePostingEventId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ReversedById",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "StockAdjustments");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_TenantId",
                table: "StockAdjustments",
                column: "TenantId");
        }
    }
}
