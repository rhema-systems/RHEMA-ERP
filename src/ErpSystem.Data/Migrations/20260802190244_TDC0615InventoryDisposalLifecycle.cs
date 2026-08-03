using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    public partial class TDC0615InventoryDisposalLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryDisposalCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisposalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IdentificationDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditVerifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuditVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditFindings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CommitteeMeetingAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommitteeReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CommitteeScheduledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommitteeRecommendedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorityRoute = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StockAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProceedsAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProceedsAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BuyerOrRecipient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExecutionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProceedsPostingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProceedsJournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_InventoryDisposalCases", x => x.Id);
                    table.CheckConstraint("CK_InventoryDisposalCases_Amounts", "[TotalQuantity] > 0 AND [TotalValue] > 0 AND [ProceedsAmount] >= 0");
                    table.CheckConstraint("CK_InventoryDisposalCases_Method", "[Method] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_InventoryDisposalCases_Status", "[Status] BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_InventoryDisposalCases_StockAdjustments_StockAdjustmentId",
                        column: x => x.StockAdjustmentId,
                        principalTable: "StockAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalCases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalCases_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalCases_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDisposalActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryDisposalCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_InventoryDisposalActions", x => x.Id);
                    table.CheckConstraint("CK_InventoryDisposalActions_Sequence", "[Sequence] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryDisposalActions_InventoryDisposalCases_InventoryDisposalCaseId",
                        column: x => x.InventoryDisposalCaseId,
                        principalTable: "InventoryDisposalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDisposalCommitteeMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryDisposalCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecommendApproval = table.Column<bool>(type: "bit", nullable: true),
                    ConflictDeclared = table.Column<bool>(type: "bit", nullable: false),
                    VotedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_InventoryDisposalCommitteeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalCommitteeMembers_InventoryDisposalCases_InventoryDisposalCaseId",
                        column: x => x.InventoryDisposalCaseId,
                        principalTable: "InventoryDisposalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalCommitteeMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalCommitteeMembers_Users_MemberUserId",
                        column: x => x.MemberUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDisposalEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryDisposalCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_InventoryDisposalEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalEvidence_InventoryDisposalCases_InventoryDisposalCaseId",
                        column: x => x.InventoryDisposalCaseId,
                        principalTable: "InventoryDisposalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryDisposalLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryDisposalCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConditionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_InventoryDisposalLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryDisposalLines_Amounts", "[Quantity] > 0 AND [UnitCost] > 0 AND [TotalValue] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryDisposalLines_InventoryDisposalCases_InventoryDisposalCaseId",
                        column: x => x.InventoryDisposalCaseId,
                        principalTable: "InventoryDisposalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryDisposalLines_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalActions_ActorUserId",
                table: "InventoryDisposalActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalActions_InventoryDisposalCaseId_IdempotencyKey",
                table: "InventoryDisposalActions",
                columns: new[] { "InventoryDisposalCaseId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalActions_InventoryDisposalCaseId_Sequence",
                table: "InventoryDisposalActions",
                columns: new[] { "InventoryDisposalCaseId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalActions_TenantId",
                table: "InventoryDisposalActions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCases_RequestedById",
                table: "InventoryDisposalCases",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCases_StockAdjustmentId",
                table: "InventoryDisposalCases",
                column: "StockAdjustmentId",
                unique: true,
                filter: "[StockAdjustmentId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCases_TenantId_DisposalNumber",
                table: "InventoryDisposalCases",
                columns: new[] { "TenantId", "DisposalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCases_TenantId_IdempotencyKey",
                table: "InventoryDisposalCases",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCases_TenantId_Status_RequestedAtUtc",
                table: "InventoryDisposalCases",
                columns: new[] { "TenantId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCases_WarehouseId",
                table: "InventoryDisposalCases",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCommitteeMembers_InventoryDisposalCaseId_MemberUserId",
                table: "InventoryDisposalCommitteeMembers",
                columns: new[] { "InventoryDisposalCaseId", "MemberUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCommitteeMembers_MemberUserId",
                table: "InventoryDisposalCommitteeMembers",
                column: "MemberUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalCommitteeMembers_TenantId",
                table: "InventoryDisposalCommitteeMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalEvidence_CentralDocumentVersionId",
                table: "InventoryDisposalEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalEvidence_FileUploadRecordId",
                table: "InventoryDisposalEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalEvidence_InventoryDisposalCaseId_CentralDocumentVersionId",
                table: "InventoryDisposalEvidence",
                columns: new[] { "InventoryDisposalCaseId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalEvidence_TenantId",
                table: "InventoryDisposalEvidence",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalLines_InventoryDisposalCaseId_InventoryItemId_LocationId",
                table: "InventoryDisposalLines",
                columns: new[] { "InventoryDisposalCaseId", "InventoryItemId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalLines_InventoryItemId",
                table: "InventoryDisposalLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalLines_LocationId",
                table: "InventoryDisposalLines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryDisposalLines_TenantId",
                table: "InventoryDisposalLines",
                column: "TenantId");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalActions_AppendOnly]
ON [dbo].[InventoryDisposalActions]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51100, 'INV_DISPOSAL_ACTION_APPEND_ONLY', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN dbo.InventoryDisposalCases c ON c.Id = i.InventoryDisposalCaseId AND c.TenantId = i.TenantId
        LEFT JOIN dbo.Users u ON u.Id = i.ActorUserId AND u.TenantId = i.TenantId
        WHERE c.Id IS NULL OR u.Id IS NULL)
        THROW 51101, 'INV_DISPOSAL_ACTION_TENANT_LINEAGE_INVALID', 1;
END;
""");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalEvidence_AppendOnly]
ON [dbo].[InventoryDisposalEvidence]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51102, 'INV_DISPOSAL_EVIDENCE_APPEND_ONLY', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN dbo.InventoryDisposalCases c ON c.Id = i.InventoryDisposalCaseId AND c.TenantId = i.TenantId
        LEFT JOIN dbo.CentralDocumentVersions v ON v.Id = i.CentralDocumentVersionId AND v.TenantId = i.TenantId
        LEFT JOIN dbo.FileUploadRecords f ON f.Id = i.FileUploadRecordId AND f.TenantId = i.TenantId
        WHERE c.Id IS NULL OR v.Id IS NULL OR f.Id IS NULL OR v.FileUploadRecordId <> i.FileUploadRecordId)
        THROW 51103, 'INV_DISPOSAL_EVIDENCE_TENANT_LINEAGE_INVALID', 1;
END;
""");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalLines_Immutable]
ON [dbo].[InventoryDisposalLines]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
        THROW 51104, 'INV_DISPOSAL_LINE_IMMUTABLE', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN dbo.InventoryDisposalCases c ON c.Id = i.InventoryDisposalCaseId AND c.TenantId = i.TenantId
        LEFT JOIN dbo.InventoryItems p ON p.Id = i.InventoryItemId AND p.TenantId = i.TenantId
        LEFT JOIN dbo.WarehouseLocations l ON l.Id = i.LocationId AND l.TenantId = i.TenantId AND l.WarehouseId = c.WarehouseId
        WHERE c.Id IS NULL OR p.Id IS NULL OR l.Id IS NULL)
        THROW 51105, 'INV_DISPOSAL_LINE_TENANT_LOCATION_LINEAGE_INVALID', 1;
END;
""");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalCommitteeMembers_Guard]
ON [dbo].[InventoryDisposalCommitteeMembers]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
        THROW 51106, 'INV_DISPOSAL_COMMITTEE_MEMBER_DELETE_PROHIBITED', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN dbo.InventoryDisposalCases c ON c.Id = i.InventoryDisposalCaseId AND c.TenantId = i.TenantId
        LEFT JOIN dbo.Users u ON u.Id = i.MemberUserId AND u.TenantId = i.TenantId AND u.IsActive = 1
        WHERE c.Id IS NULL OR u.Id IS NULL OR NOT EXISTS (
            SELECT 1 FROM dbo.UserRoles ur
            JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
            WHERE ur.UserId = i.MemberUserId
              AND (r.Name = 'TDC_DISPOSAL_COMMITTEE_MEMBER' OR r.NormalizedName = 'TDC_DISPOSAL_COMMITTEE_MEMBER')))
        THROW 51107, 'INV_DISPOSAL_COMMITTEE_MEMBER_ROLE_LINEAGE_INVALID', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
        WHERE i.TenantId <> d.TenantId OR i.InventoryDisposalCaseId <> d.InventoryDisposalCaseId
           OR i.MemberUserId <> d.MemberUserId OR d.VotedAtUtc IS NOT NULL OR i.VotedAtUtc IS NULL
           OR (i.ConflictDeclared = 1 AND i.RecommendApproval IS NOT NULL)
           OR (i.ConflictDeclared = 0 AND i.RecommendApproval IS NULL))
        THROW 51108, 'INV_DISPOSAL_COMMITTEE_VOTE_TRANSITION_INVALID', 1;
END;
""");

            migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_InventoryDisposalCases_Guard]
ON [dbo].[InventoryDisposalCases]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
        THROW 51109, 'INV_DISPOSAL_CASE_DELETE_PROHIBITED', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN dbo.Warehouses w ON w.Id = i.WarehouseId AND w.TenantId = i.TenantId
        LEFT JOIN dbo.Users u ON u.Id = i.RequestedById AND u.TenantId = i.TenantId
        LEFT JOIN dbo.StockAdjustments a ON a.Id = i.StockAdjustmentId AND a.TenantId = i.TenantId
        WHERE w.Id IS NULL OR u.Id IS NULL OR (i.StockAdjustmentId IS NOT NULL AND a.Id IS NULL))
        THROW 51110, 'INV_DISPOSAL_CASE_TENANT_LINEAGE_INVALID', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
        WHERE d.Id IS NULL AND (i.Status <> 1 OR i.AuditVerifiedById IS NOT NULL OR i.WorkflowInstanceId IS NOT NULL
           OR i.ApprovedById IS NOT NULL OR i.StockAdjustmentId IS NOT NULL OR i.CompletedById IS NOT NULL))
        THROW 51111, 'INV_DISPOSAL_CASE_INITIAL_STATE_INVALID', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
        WHERE i.TenantId <> d.TenantId OR i.DisposalNumber <> d.DisposalNumber OR i.WarehouseId <> d.WarehouseId
           OR i.Method <> d.Method OR i.Reason <> d.Reason OR i.IdentificationDetails <> d.IdentificationDetails
           OR i.RequestedById <> d.RequestedById OR i.RequestedAtUtc <> d.RequestedAtUtc
           OR i.TotalQuantity <> d.TotalQuantity OR i.TotalValue <> d.TotalValue
           OR i.IdempotencyKey <> d.IdempotencyKey OR i.PayloadHash <> d.PayloadHash OR i.CorrelationId <> d.CorrelationId
           OR i.IsDeleted <> d.IsDeleted)
        THROW 51112, 'INV_DISPOSAL_CASE_CORE_IMMUTABLE', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
        WHERE i.Status <> d.Status AND NOT (
               (d.Status = 1 AND i.Status IN (2, 9, 10))
            OR (d.Status = 2 AND i.Status IN (3, 10))
            OR (d.Status = 3 AND i.Status IN (4, 9, 10))
            OR (d.Status = 4 AND i.Status IN (5, 10))
            OR (d.Status = 5 AND i.Status IN (6, 9))
            OR (d.Status = 6 AND i.Status = 7)
            OR (d.Status = 7 AND i.Status = 8)))
        THROW 51113, 'INV_DISPOSAL_CASE_TRANSITION_INVALID', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE Status = 5 AND WorkflowInstanceId IS NULL)
        THROW 51114, 'INV_DISPOSAL_WORKFLOW_LINEAGE_REQUIRED', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE Status IN (6, 7, 8) AND (WorkflowInstanceId IS NULL OR ApprovedById IS NULL OR ApprovedAtUtc IS NULL))
        THROW 51115, 'INV_DISPOSAL_APPROVAL_LINEAGE_REQUIRED', 1;
    IF EXISTS (SELECT 1 FROM inserted WHERE Status IN (7, 8) AND (StockAdjustmentId IS NULL OR ExecutionReference IS NULL))
        THROW 51116, 'INV_DISPOSAL_ADJUSTMENT_LINEAGE_REQUIRED', 1;
    IF EXISTS (
        SELECT 1 FROM inserted WHERE Status = 8 AND (
            CompletedById IS NULL OR CompletedAtUtc IS NULL
            OR (Method IN (1, 2) AND (ProceedsAmount <= 0 OR ProceedsAccountId IS NULL OR BuyerOrRecipient IS NULL
                OR ProceedsPostingEventId IS NULL OR ProceedsJournalEntryId IS NULL))
            OR (Method NOT IN (1, 2) AND ProceedsAmount <> 0)))
        THROW 51117, 'INV_DISPOSAL_COMPLETION_LINEAGE_INVALID', 1;
END;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryDisposalCases_Guard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryDisposalCommitteeMembers_Guard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryDisposalLines_Immutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryDisposalEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryDisposalActions_AppendOnly];");
            migrationBuilder.DropTable(name: "InventoryDisposalActions");
            migrationBuilder.DropTable(name: "InventoryDisposalCommitteeMembers");
            migrationBuilder.DropTable(name: "InventoryDisposalEvidence");
            migrationBuilder.DropTable(name: "InventoryDisposalLines");
            migrationBuilder.DropTable(name: "InventoryDisposalCases");
        }
    }
}
