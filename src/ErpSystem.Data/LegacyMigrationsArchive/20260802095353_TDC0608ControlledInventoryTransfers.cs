using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0608ControlledInventoryTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClosedById",
                table: "InventoryTransfers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOpenDiscrepancy",
                table: "InventoryTransfers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventoryTransfers",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortageQuantity",
                table: "InventoryTransferItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "InventoryTransferActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_InventoryTransferActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransferActions_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferActionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispatchedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DamagedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ShortageQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryTransferActionLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryTransferActionLines_Quantity", "[DispatchedQuantity] >= 0 AND [ReceivedQuantity] >= 0 AND [DamagedQuantity] >= 0 AND [ShortageQuantity] >= 0 AND ([DispatchedQuantity] + [ReceivedQuantity] + [DamagedQuantity] + [ShortageQuantity]) > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferActionLines_InventoryTransferActions_InventoryTransferActionId",
                        column: x => x.InventoryTransferActionId,
                        principalTable: "InventoryTransferActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferActionLines_InventoryTransferItems_InventoryTransferItemId",
                        column: x => x.InventoryTransferItemId,
                        principalTable: "InventoryTransferItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferActionLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DamagedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ShortageQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResolutionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_InventoryTransferDiscrepancies", x => x.Id);
                    table.CheckConstraint("CK_InventoryTransferDiscrepancies_Quantity", "[DamagedQuantity] >= 0 AND [ShortageQuantity] >= 0 AND ([DamagedQuantity] + [ShortageQuantity]) > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancies_InventoryTransferActions_ReceiptActionId",
                        column: x => x.ReceiptActionId,
                        principalTable: "InventoryTransferActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancies_InventoryTransferItems_InventoryTransferItemId",
                        column: x => x.InventoryTransferItemId,
                        principalTable: "InventoryTransferItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancies_InventoryTransfers_InventoryTransferId",
                        column: x => x.InventoryTransferId,
                        principalTable: "InventoryTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransferDiscrepancyEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryTransferDiscrepancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_InventoryTransferDiscrepancyEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyEvidence_InventoryTransferDiscrepancies_InventoryTransferDiscrepancyId",
                        column: x => x.InventoryTransferDiscrepancyId,
                        principalTable: "InventoryTransferDiscrepancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransferDiscrepancyEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransferItems_ControlledQuantity",
                table: "InventoryTransferItems",
                sql: "[RequestedQuantity] > 0 AND [ShippedQuantity] >= 0 AND [ReceivedQuantity] >= 0 AND [DamagedQuantity] >= 0 AND [ShortageQuantity] >= 0 AND [ShippedQuantity] <= [RequestedQuantity] AND ([ReceivedQuantity] + [DamagedQuantity] + [ShortageQuantity]) <= [ShippedQuantity]");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferActionLines_InventoryTransferActionId",
                table: "InventoryTransferActionLines",
                column: "InventoryTransferActionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferActionLines_InventoryTransferItemId",
                table: "InventoryTransferActionLines",
                column: "InventoryTransferItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferActionLines_TenantId_InventoryTransferActionId_InventoryTransferItemId",
                table: "InventoryTransferActionLines",
                columns: new[] { "TenantId", "InventoryTransferActionId", "InventoryTransferItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferActions_ActorUserId",
                table: "InventoryTransferActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferActions_InventoryTransferId",
                table: "InventoryTransferActions",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferActions_TenantId_InventoryTransferId_ActionType_IdempotencyKey",
                table: "InventoryTransferActions",
                columns: new[] { "TenantId", "InventoryTransferId", "ActionType", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferActions_TenantId_InventoryTransferId_Sequence",
                table: "InventoryTransferActions",
                columns: new[] { "TenantId", "InventoryTransferId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancies_InventoryTransferId",
                table: "InventoryTransferDiscrepancies",
                column: "InventoryTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancies_InventoryTransferItemId",
                table: "InventoryTransferDiscrepancies",
                column: "InventoryTransferItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancies_ReceiptActionId",
                table: "InventoryTransferDiscrepancies",
                column: "ReceiptActionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancies_TenantId_InventoryTransferId_Status",
                table: "InventoryTransferDiscrepancies",
                columns: new[] { "TenantId", "InventoryTransferId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyEvidence_CentralDocumentVersionId",
                table: "InventoryTransferDiscrepancyEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyEvidence_FileUploadRecordId",
                table: "InventoryTransferDiscrepancyEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyEvidence_InventoryTransferDiscrepancyId",
                table: "InventoryTransferDiscrepancyEvidence",
                column: "InventoryTransferDiscrepancyId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransferDiscrepancyEvidence_TenantId_InventoryTransferDiscrepancyId_CentralDocumentVersionId",
                table: "InventoryTransferDiscrepancyEvidence",
                columns: new[] { "TenantId", "InventoryTransferDiscrepancyId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryTransferActions_AppendOnly]
                ON [dbo].[InventoryTransferActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51801, 'INV_TRANSFER_ACTION_IMMUTABLE: transfer actions cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryTransfers t ON t.Id = i.InventoryTransferId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        LEFT JOIN UserTenants ut ON ut.TenantId = i.TenantId AND ut.UserId = i.ActorUserId
                            AND ut.IsDeleted = 0 AND ut.Status = 0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt > SYSUTCDATETIME())
                        LEFT JOIN Users u ON u.Id = i.ActorUserId AND u.IsActive = 1
                        WHERE t.Id IS NULL OR ut.Id IS NULL OR u.Id IS NULL OR i.Sequence <= 0
                           OR i.ActionType NOT BETWEEN 1 AND 10
                           OR LEN(LTRIM(RTRIM(i.IdempotencyKey))) = 0 OR LEN(LTRIM(RTRIM(i.CorrelationId))) = 0
                           OR LEN(i.PayloadHash) <> 64 OR LEN(i.IntegrityHash) <> 64 OR ISJSON(i.SnapshotJson) <> 1)
                        THROW 51802, 'INV_TRANSFER_ACTION_INVALID: action identity, actor, sequence and integrity are invalid.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryTransferActionLines_AppendOnly]
                ON [dbo].[InventoryTransferActionLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51811, 'INV_TRANSFER_ACTION_LINE_IMMUTABLE: transfer action lines cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryTransferActions a ON a.Id = i.InventoryTransferActionId AND a.TenantId = i.TenantId AND a.IsDeleted = 0
                        LEFT JOIN InventoryTransferItems line ON line.Id = i.InventoryTransferItemId AND line.TenantId = i.TenantId AND line.IsDeleted = 0
                        WHERE a.Id IS NULL OR line.Id IS NULL OR line.InventoryTransferId <> a.InventoryTransferId OR LEN(i.IntegrityHash) <> 64
                           OR (a.ActionType IN (1,2,3,4,8,10))
                           OR (a.ActionType IN (5,9) AND NOT (i.DispatchedQuantity > 0 AND i.ReceivedQuantity = 0 AND i.DamagedQuantity = 0 AND i.ShortageQuantity = 0))
                           OR (a.ActionType = 6 AND NOT (i.DispatchedQuantity = 0 AND (i.ReceivedQuantity + i.DamagedQuantity + i.ShortageQuantity) > 0))
                           OR (a.ActionType = 7 AND NOT (i.DispatchedQuantity = 0 AND i.ReceivedQuantity = 0 AND (i.DamagedQuantity + i.ShortageQuantity) > 0)))
                        THROW 51812, 'INV_TRANSFER_ACTION_LINE_INVALID: action line quantities must match the governed action and transfer line.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryTransferItems_ControlledMutation]
                ON [dbo].[InventoryTransferItems]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        INNER JOIN InventoryTransfers t ON t.Id = d.InventoryTransferId AND t.TenantId = d.TenantId
                        WHERE i.Id IS NULL AND t.Status <> 1)
                        THROW 51821, 'INV_TRANSFER_LINE_DELETE_BLOCKED: only Draft transfer lines may be physically removed.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        INNER JOIN deleted d ON d.Id = i.Id
                        INNER JOIN InventoryTransfers t ON t.Id = i.InventoryTransferId AND t.TenantId = i.TenantId
                        WHERE t.Status <> 1 AND
                            (i.TenantId <> d.TenantId OR i.InventoryTransferId <> d.InventoryTransferId
                             OR i.InventoryItemId <> d.InventoryItemId OR i.RequestedQuantity <> d.RequestedQuantity
                             OR i.UnitCost <> d.UnitCost OR i.LineValue <> d.LineValue OR i.IsDeleted <> d.IsDeleted))
                        THROW 51822, 'INV_TRANSFER_LINE_SOURCE_IMMUTABLE: submitted transfer source lines and value cannot change.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        INNER JOIN InventoryTransfers t ON t.Id = i.InventoryTransferId AND t.TenantId = i.TenantId
                        OUTER APPLY (
                            SELECT
                                COALESCE(SUM(CASE WHEN a.ActionType = 5 THEN al.DispatchedQuantity WHEN a.ActionType = 9 THEN -al.DispatchedQuantity ELSE 0 END), 0) Shipped,
                                COALESCE(SUM(CASE WHEN a.ActionType = 6 THEN al.ReceivedQuantity WHEN a.ActionType = 7 AND JSON_VALUE(a.SnapshotJson, '$.Metadata.ResolutionCode') = N'REPLACEMENT_RECEIVED' THEN al.DamagedQuantity + al.ShortageQuantity ELSE 0 END), 0) Received,
                                COALESCE(SUM(CASE WHEN a.ActionType = 6 THEN al.DamagedQuantity WHEN a.ActionType = 7 AND JSON_VALUE(a.SnapshotJson, '$.Metadata.ResolutionCode') = N'REPLACEMENT_RECEIVED' THEN -al.DamagedQuantity ELSE 0 END), 0) Damaged,
                                COALESCE(SUM(CASE WHEN a.ActionType = 6 THEN al.ShortageQuantity WHEN a.ActionType = 7 AND JSON_VALUE(a.SnapshotJson, '$.Metadata.ResolutionCode') = N'REPLACEMENT_RECEIVED' THEN -al.ShortageQuantity ELSE 0 END), 0) Shortage
                            FROM InventoryTransferActionLines al
                            INNER JOIN InventoryTransferActions a ON a.Id = al.InventoryTransferActionId AND a.TenantId = al.TenantId AND a.IsDeleted = 0
                            WHERE al.InventoryTransferItemId = i.Id AND al.TenantId = i.TenantId AND al.IsDeleted = 0) governed
                        WHERE t.Status <> 1 AND (i.ShippedQuantity <> governed.Shipped OR i.ReceivedQuantity <> governed.Received
                            OR i.DamagedQuantity <> governed.Damaged OR i.ShortageQuantity <> governed.Shortage))
                        THROW 51823, 'INV_TRANSFER_LINE_QUANTITY_UNGOVERNED: transfer quantities must reconcile exactly to immutable dispatch, receipt, reversal and resolution actions.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryTransferDiscrepancies_ControlledLifecycle]
                ON [dbo].[InventoryTransferDiscrepancies]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51831, 'INV_TRANSFER_DISCREPANCY_DELETE_BLOCKED: discrepancy history cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN InventoryTransfers t ON t.Id = i.InventoryTransferId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        LEFT JOIN InventoryTransferItems line ON line.Id = i.InventoryTransferItemId AND line.InventoryTransferId = i.InventoryTransferId AND line.TenantId = i.TenantId
                        LEFT JOIN InventoryTransferActions a ON a.Id = i.ReceiptActionId AND a.InventoryTransferId = i.InventoryTransferId AND a.TenantId = i.TenantId AND a.ActionType = 6
                        LEFT JOIN InventoryTransferActionLines al ON al.InventoryTransferActionId = a.Id AND al.InventoryTransferItemId = i.InventoryTransferItemId AND al.TenantId = i.TenantId
                        WHERE d.Id IS NULL AND (t.Id IS NULL OR line.Id IS NULL OR a.Id IS NULL OR al.Id IS NULL
                            OR i.Status <> 1 OR i.ResolutionCode IS NOT NULL OR i.ResolutionNotes IS NOT NULL OR i.ResolvedById IS NOT NULL OR i.ResolvedAtUtc IS NOT NULL
                            OR i.ReasonCode NOT IN (N'SHORTAGE', N'DAMAGED', N'LOST_IN_TRANSIT', N'WRONG_ITEM', N'OTHER')
                            OR LEN(LTRIM(RTRIM(i.Reason))) = 0 OR LEN(i.IntegrityHash) <> 64
                            OR i.DamagedQuantity <> al.DamagedQuantity OR i.ShortageQuantity <> al.ShortageQuantity))
                        THROW 51832, 'INV_TRANSFER_DISCREPANCY_INVALID: discrepancies require exact governed receipt quantities, reason and integrity.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        INNER JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN InventoryTransfers t ON t.Id = i.InventoryTransferId AND t.TenantId = i.TenantId
                        WHERE i.TenantId <> d.TenantId OR i.InventoryTransferId <> d.InventoryTransferId OR i.InventoryTransferItemId <> d.InventoryTransferItemId
                           OR i.ReceiptActionId <> d.ReceiptActionId OR i.DamagedQuantity <> d.DamagedQuantity OR i.ShortageQuantity <> d.ShortageQuantity
                           OR i.ReasonCode <> d.ReasonCode OR i.Reason <> d.Reason OR i.IntegrityHash <> d.IntegrityHash OR i.IsDeleted <> d.IsDeleted
                           OR d.Status <> 1 OR i.Status <> 2 OR i.ResolutionCode NOT IN (N'CONFIRMED_LOSS', N'RETURNED_TO_SOURCE', N'REPLACEMENT_RECEIVED')
                           OR LEN(LTRIM(RTRIM(i.ResolutionNotes))) = 0 OR i.ResolvedById IS NULL OR i.ResolvedAtUtc IS NULL
                           OR i.ResolvedById IN (t.RequestedById, t.ApprovedById, t.ShippedById, t.ReceivedById)
                           OR NOT EXISTS (SELECT 1 FROM InventoryTransferActions ra INNER JOIN InventoryTransferActionLines ral ON ral.InventoryTransferActionId = ra.Id
                                WHERE ra.InventoryTransferId = i.InventoryTransferId AND ra.TenantId = i.TenantId AND ra.ActionType = 7
                                  AND ra.ActorUserId = i.ResolvedById AND ral.InventoryTransferItemId = i.InventoryTransferItemId
                                  AND ral.DamagedQuantity >= i.DamagedQuantity AND ral.ShortageQuantity >= i.ShortageQuantity))
                        THROW 51833, 'INV_TRANSFER_DISCREPANCY_RESOLUTION_INVALID: only an independent evidence-backed governed resolution may close a discrepancy.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryTransferDiscrepancyEvidence_AppendOnly]
                ON [dbo].[InventoryTransferDiscrepancyEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51841, 'INV_TRANSFER_EVIDENCE_IMMUTABLE: linked discrepancy evidence cannot be changed or deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryTransferDiscrepancies d ON d.Id = i.InventoryTransferDiscrepancyId AND d.TenantId = i.TenantId AND d.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions dv ON dv.Id = i.CentralDocumentVersionId AND dv.TenantId = i.TenantId AND dv.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords dr ON dr.Id = dv.DocumentRecordId AND dr.TenantId = i.TenantId AND dr.IsDeleted = 0
                        LEFT JOIN FileUploadRecords f ON f.Id = i.FileUploadRecordId AND f.TenantId = i.TenantId AND f.IsDeleted = 0
                        WHERE d.Id IS NULL OR dv.Id IS NULL OR dr.Id IS NULL OR f.Id IS NULL
                           OR dv.FileUploadRecordId <> i.FileUploadRecordId OR dv.Status <> N'Published' OR dv.PublishedAt IS NULL
                           OR dr.LifecycleStatus <> N'Active' OR dr.VersionStatus <> N'Published' OR dr.CurrentVersion <> dv.VersionNumber
                           OR LEN(LTRIM(RTRIM(i.EvidenceReference))) = 0 OR LEN(i.IntegrityHash) <> 64)
                        THROW 51842, 'INV_TRANSFER_EVIDENCE_INVALID: evidence must be the tenant current published central-DMS version and repository file.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryTransfers_ControlledLifecycle]
                ON [dbo].[InventoryTransfers]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51851, 'INV_TRANSFER_DELETE_BLOCKED: inventory transfers cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN Warehouses sw ON sw.Id = i.SourceWarehouseId AND sw.TenantId = i.TenantId AND sw.IsDeleted = 0 AND sw.IsActive = 1
                        LEFT JOIN Warehouses dw ON dw.Id = i.DestinationWarehouseId AND dw.TenantId = i.TenantId AND dw.IsDeleted = 0 AND dw.IsActive = 1
                        WHERE sw.Id IS NULL OR dw.Id IS NULL OR i.Status NOT BETWEEN 1 AND 9 OR i.RequestedById IS NULL
                           OR (d.Id IS NULL AND (i.Status <> 1 OR i.IsDeleted = 1 OR LEN(LTRIM(RTRIM(i.TransferNumber))) = 0)))
                        THROW 51852, 'INV_TRANSFER_SCOPE_INVALID: transfer scope, requester, warehouse and initial state are invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status <> 1 AND (i.TenantId <> d.TenantId OR i.TransferNumber <> d.TransferNumber
                            OR i.SourceWarehouseId <> d.SourceWarehouseId OR i.DestinationWarehouseId <> d.DestinationWarehouseId
                            OR i.RequestedById <> d.RequestedById OR i.RequestDate <> d.RequestDate
                            OR i.TotalItems <> d.TotalItems OR i.TotalQuantity <> d.TotalQuantity OR i.TotalValue <> d.TotalValue
                            OR i.IsDeleted <> d.IsDeleted))
                        THROW 51853, 'INV_TRANSFER_SOURCE_IMMUTABLE: submitted transfer identity, scope and requested value cannot change.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status AND NOT (
                            (d.Status = 1 AND i.Status = 2 AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 1 AND a.ActorUserId = i.RequestedById))
                         OR (d.Status = 2 AND i.Status = 3 AND i.ApprovedById IS NOT NULL AND i.ApprovedById <> i.RequestedById AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 2 AND a.ActorUserId = i.ApprovedById))
                         OR (d.Status = 2 AND i.Status = 9 AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 3))
                         OR (d.Status = 3 AND i.Status = 5 AND i.ShippedById IS NOT NULL AND i.ShippedById NOT IN (i.RequestedById, i.ApprovedById) AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 5 AND a.ActorUserId = i.ShippedById))
                         OR (d.Status = 5 AND i.Status = 6 AND i.ReceivedById IS NOT NULL AND i.ReceivedById NOT IN (i.RequestedById, i.ApprovedById, i.ShippedById) AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 6 AND a.ActorUserId = i.ReceivedById))
                         OR (d.Status = 6 AND i.Status = 7 AND i.ClosedById IS NOT NULL AND i.ClosedById NOT IN (i.RequestedById, i.ApprovedById, i.ShippedById, i.ReceivedById) AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 8 AND a.ActorUserId = i.ClosedById))
                         OR (d.Status IN (1,2,3) AND i.Status = 8 AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 10))
                         OR (d.Status = 5 AND i.Status = 8 AND EXISTS (SELECT 1 FROM InventoryTransferActions a WHERE a.InventoryTransferId = i.Id AND a.TenantId = i.TenantId AND a.ActionType = 9))))
                        THROW 51854, 'INV_TRANSFER_TRANSITION_INVALID: transfer lifecycle transitions require the matching immutable action and independent actors.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE (i.Status >= 3 AND i.Status NOT IN (8,9) AND (i.ApprovedById IS NULL OR i.ApprovedById = i.RequestedById))
                           OR (i.Status = 5 AND (i.ShippedById IS NULL OR i.ShippedDate IS NULL OR i.ShippedById IN (i.RequestedById, i.ApprovedById)))
                           OR (i.Status IN (6,7) AND (i.ReceivedById IS NULL OR i.ReceivedDate IS NULL OR i.ReceivedById IN (i.RequestedById, i.ApprovedById, i.ShippedById)))
                           OR (i.Status = 7 AND (i.ClosedById IS NULL OR i.CompletedDate IS NULL OR i.HasOpenDiscrepancy = 1 OR i.ClosedById IN (i.RequestedById, i.ApprovedById, i.ShippedById, i.ReceivedById)))
                           OR (i.HasOpenDiscrepancy <> CASE WHEN EXISTS (SELECT 1 FROM InventoryTransferDiscrepancies x WHERE x.InventoryTransferId = i.Id AND x.TenantId = i.TenantId AND x.Status = 1 AND x.IsDeleted = 0) THEN 1 ELSE 0 END)
                           OR (i.Status IN (6,7) AND EXISTS (SELECT 1 FROM InventoryTransferItems line WHERE line.InventoryTransferId = i.Id AND line.TenantId = i.TenantId AND (line.ShippedQuantity <> line.RequestedQuantity OR line.ReceivedQuantity + line.DamagedQuantity + line.ShortageQuantity <> line.ShippedQuantity))))
                        THROW 51855, 'INV_TRANSFER_STATE_INVALID: actors, dates, quantity reconciliation and discrepancy state are inconsistent.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_GovernedInventoryTransfer]
                ON [dbo].[StockMovements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL AND d.MovementType IN (N'TransferOut', N'TransferIn', N'TransferReversal', N'TransferDiscrepancyReturn', N'TransferReplacementIn'))
                        THROW 51861, 'INV_TRANSFER_MOVEMENT_DELETE_BLOCKED: governed transfer movements cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.Id = i.Id
                        WHERE (i.MovementType IN (N'TransferOut', N'TransferIn', N'TransferReversal', N'TransferDiscrepancyReturn', N'TransferReplacementIn')
                            OR d.MovementType IN (N'TransferOut', N'TransferIn', N'TransferReversal', N'TransferDiscrepancyReturn', N'TransferReplacementIn'))
                          AND (i.TenantId <> d.TenantId OR i.InventoryItemId <> d.InventoryItemId OR i.WarehouseId <> d.WarehouseId
                            OR ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.LocationId, '00000000-0000-0000-0000-000000000000')
                            OR i.MovementType <> d.MovementType OR i.Quantity <> d.Quantity OR i.UnitCost <> d.UnitCost OR i.TotalValue <> d.TotalValue
                            OR i.ReferenceType <> d.ReferenceType OR ISNULL(i.ReferenceNumber, N'') <> ISNULL(d.ReferenceNumber, N'')
                            OR ISNULL(i.ReferenceId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ReferenceId, '00000000-0000-0000-0000-000000000000')
                            OR ISNULL(i.ProcessedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ProcessedById, '00000000-0000-0000-0000-000000000000') OR i.IsDeleted <> d.IsDeleted))
                        THROW 51862, 'INV_TRANSFER_MOVEMENT_IMMUTABLE: governed transfer movement lineage is immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN InventoryTransfers t ON t.Id = i.ReferenceId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        OUTER APPLY (
                            SELECT CASE
                                WHEN i.MovementType IN (N'TransferOut', N'TransferReversal') THEN COALESCE(SUM(al.DispatchedQuantity), 0)
                                WHEN i.MovementType = N'TransferIn' THEN COALESCE(SUM(al.ReceivedQuantity), 0)
                                ELSE COALESCE(SUM(al.DamagedQuantity + al.ShortageQuantity), 0) END Quantity
                            FROM InventoryTransferActionLines al
                            INNER JOIN InventoryTransferActions a ON a.Id = al.InventoryTransferActionId AND a.TenantId = al.TenantId AND a.IsDeleted = 0
                            INNER JOIN InventoryTransferItems line ON line.Id = al.InventoryTransferItemId AND line.TenantId = al.TenantId
                            WHERE a.InventoryTransferId = i.ReferenceId AND a.TenantId = i.TenantId AND line.InventoryItemId = i.InventoryItemId
                              AND ISNULL(CASE WHEN i.MovementType IN (N'TransferOut', N'TransferReversal', N'TransferDiscrepancyReturn') THEN line.SourceLocationId ELSE line.DestinationLocationId END, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000')
                              AND ((i.MovementType = N'TransferOut' AND a.ActionType = 5) OR (i.MovementType = N'TransferIn' AND a.ActionType = 6)
                                OR (i.MovementType = N'TransferReversal' AND a.ActionType = 9)
                                OR (i.MovementType = N'TransferDiscrepancyReturn' AND a.ActionType = 7 AND JSON_VALUE(a.SnapshotJson, '$.Metadata.ResolutionCode') = N'RETURNED_TO_SOURCE')
                                OR (i.MovementType = N'TransferReplacementIn' AND a.ActionType = 7 AND JSON_VALUE(a.SnapshotJson, '$.Metadata.ResolutionCode') = N'REPLACEMENT_RECEIVED'))) allowed
                        OUTER APPLY (
                            SELECT COALESCE(SUM(ABS(m.Quantity)), 0) Quantity
                            FROM StockMovements m WITH (UPDLOCK, HOLDLOCK)
                            WHERE m.ReferenceType = 4 AND m.ReferenceId = i.ReferenceId AND m.TenantId = i.TenantId AND m.InventoryItemId = i.InventoryItemId
                              AND m.MovementType = i.MovementType AND ISNULL(m.LocationId, '00000000-0000-0000-0000-000000000000') = ISNULL(i.LocationId, '00000000-0000-0000-0000-000000000000') AND m.IsDeleted = 0) posted
                        WHERE i.MovementType IN (N'TransferOut', N'TransferIn', N'TransferReversal', N'TransferDiscrepancyReturn', N'TransferReplacementIn')
                          AND (t.Id IS NULL OR i.ReferenceType <> 4 OR i.ReferenceNumber <> t.TransferNumber OR i.ProcessedById IS NULL
                            OR (i.MovementType = N'TransferOut' AND (i.Quantity >= 0 OR i.WarehouseId <> t.SourceWarehouseId))
                            OR (i.MovementType = N'TransferIn' AND (i.Quantity <= 0 OR i.WarehouseId <> t.DestinationWarehouseId))
                            OR (i.MovementType IN (N'TransferReversal', N'TransferDiscrepancyReturn') AND (i.Quantity <= 0 OR i.WarehouseId <> t.SourceWarehouseId))
                            OR (i.MovementType = N'TransferReplacementIn' AND (i.Quantity <= 0 OR i.WarehouseId <> t.DestinationWarehouseId))
                            OR NOT EXISTS (SELECT 1 FROM InventoryTransferActions actor INNER JOIN InventoryTransferActionLines actorLine ON actorLine.InventoryTransferActionId = actor.Id
                                INNER JOIN InventoryTransferItems transferLine ON transferLine.Id = actorLine.InventoryTransferItemId
                                WHERE actor.InventoryTransferId = i.ReferenceId AND actor.TenantId = i.TenantId AND actor.ActorUserId = i.ProcessedById AND transferLine.InventoryItemId = i.InventoryItemId
                                  AND ((i.MovementType = N'TransferOut' AND actor.ActionType = 5) OR (i.MovementType = N'TransferIn' AND actor.ActionType = 6) OR (i.MovementType = N'TransferReversal' AND actor.ActionType = 9)
                                    OR (i.MovementType IN (N'TransferDiscrepancyReturn', N'TransferReplacementIn') AND actor.ActionType = 7)))
                            OR posted.Quantity > allowed.Quantity))
                        THROW 51863, 'INV_TRANSFER_MOVEMENT_UNGOVERNED: transfer movements require matching action quantity, actor, scope and lineage.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_GovernedInventoryTransfer];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryTransfers_ControlledLifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryTransferDiscrepancyEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryTransferDiscrepancies_ControlledLifecycle];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryTransferItems_ControlledMutation];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryTransferActionLines_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_InventoryTransferActions_AppendOnly];");

            migrationBuilder.DropTable(
                name: "InventoryTransferActionLines");

            migrationBuilder.DropTable(
                name: "InventoryTransferDiscrepancyEvidence");

            migrationBuilder.DropTable(
                name: "InventoryTransferDiscrepancies");

            migrationBuilder.DropTable(
                name: "InventoryTransferActions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransferItems_ControlledQuantity",
                table: "InventoryTransferItems");

            migrationBuilder.DropColumn(
                name: "ClosedById",
                table: "InventoryTransfers");

            migrationBuilder.DropColumn(
                name: "HasOpenDiscrepancy",
                table: "InventoryTransfers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryTransfers");

            migrationBuilder.DropColumn(
                name: "ShortageQuantity",
                table: "InventoryTransferItems");
        }
    }
}
