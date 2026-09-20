using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0502ReceiptInspectionClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementReceiptInspectionCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PendingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QualityHold = table.Column<bool>(type: "bit", nullable: false),
                    QualityHoldReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QualityHoldReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionNoteNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierAcknowledgementStatus = table.Column<int>(type: "int", nullable: false),
                    ResolutionKind = table.Column<int>(type: "int", nullable: false),
                    ResolutionStatus = table.Column<int>(type: "int", nullable: false),
                    StockEligibleQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    StockPostedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    StockPostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApEligibleQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ApBlockedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    AuthorityRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DecisionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementReceiptInspectionCases", x => x.Id);
                    table.CheckConstraint("CK_ProcurementReceiptInspectionCases_Hashes", "LEN([SourceSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([SourceSnapshotJson]) = 1 AND ISJSON([DecisionSnapshotJson]) = 1");
                    table.CheckConstraint("CK_ProcurementReceiptInspectionCases_Quantities", "[ReceivedQuantity] > 0 AND [AcceptedQuantity] >= 0 AND [RejectedQuantity] >= 0 AND [PendingQuantity] >= 0 AND [AcceptedQuantity] + [RejectedQuantity] + [PendingQuantity] = [ReceivedQuantity] AND [StockEligibleQuantity] >= 0 AND [StockPostedQuantity] >= 0 AND [StockPostedQuantity] <= [StockEligibleQuantity] AND [ApEligibleQuantity] >= 0 AND [ApBlockedQuantity] >= 0");
                    table.CheckConstraint("CK_ProcurementReceiptInspectionCases_Status", "[Status] BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionCases_PurchaseOrderReceipts_PurchaseOrderReceiptId",
                        column: x => x.PurchaseOrderReceiptId,
                        principalTable: "PurchaseOrderReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionCases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionCases_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionCases_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementReceiptInspectionActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    StatusAfter = table.Column<int>(type: "int", nullable: false),
                    ResolutionKind = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementReceiptInspectionActions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementReceiptInspectionActions_Hash", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_ProcurementReceiptInspectionActions_Sequence", "[Sequence] >= 1");
                    table.CheckConstraint("CK_ProcurementReceiptInspectionActions_Type", "[ActionType] BETWEEN 0 AND 14");
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionActions_ProcurementReceiptInspectionCases_InspectionCaseId",
                        column: x => x.InspectionCaseId,
                        principalTable: "ProcurementReceiptInspectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementReceiptInspectionEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequirementKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceKind = table.Column<int>(type: "int", nullable: false),
                    WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementReceiptInspectionEvidence", x => x.Id);
                    table.CheckConstraint("CK_ProcurementReceiptInspectionEvidence_Hash", "LEN([EvidenceHash]) = 64");
                    table.CheckConstraint("CK_ProcurementReceiptInspectionEvidence_Reference", "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionEvidence_ProcurementReceiptInspectionCases_InspectionCaseId",
                        column: x => x.InspectionCaseId,
                        principalTable: "ProcurementReceiptInspectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionEvidence_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementReceiptInspectionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderReceiptItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PendingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InspectionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QuarantineLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementReceiptInspectionLines", x => x.Id);
                    table.CheckConstraint("CK_ProcurementReceiptInspectionLines_Disposition", "[Disposition] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_ProcurementReceiptInspectionLines_Hash", "LEN([IntegrityHash]) = 64");
                    table.CheckConstraint("CK_ProcurementReceiptInspectionLines_Quantities", "[ReceivedQuantity] > 0 AND [AcceptedQuantity] >= 0 AND [RejectedQuantity] >= 0 AND [PendingQuantity] >= 0 AND [AcceptedQuantity] + [RejectedQuantity] + [PendingQuantity] = [ReceivedQuantity]");
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionLines_ProcurementReceiptInspectionCases_InspectionCaseId",
                        column: x => x.InspectionCaseId,
                        principalTable: "ProcurementReceiptInspectionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionLines_PurchaseOrderReceiptItems_PurchaseOrderReceiptItemId",
                        column: x => x.PurchaseOrderReceiptItemId,
                        principalTable: "PurchaseOrderReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementReceiptInspectionLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionActions_InspectionCaseId",
                table: "ProcurementReceiptInspectionActions",
                column: "InspectionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionActions_TenantId_IdempotencyKey",
                table: "ProcurementReceiptInspectionActions",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionActions_TenantId_InspectionCaseId_Sequence",
                table: "ProcurementReceiptInspectionActions",
                columns: new[] { "TenantId", "InspectionCaseId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionCases_PurchaseOrderReceiptId",
                table: "ProcurementReceiptInspectionCases",
                column: "PurchaseOrderReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionCases_TenantId_IdempotencyKey",
                table: "ProcurementReceiptInspectionCases",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionCases_TenantId_PurchaseOrderReceiptId_Sequence",
                table: "ProcurementReceiptInspectionCases",
                columns: new[] { "TenantId", "PurchaseOrderReceiptId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionCases_TenantId_PurchaseOrderReceiptId_Status",
                table: "ProcurementReceiptInspectionCases",
                columns: new[] { "TenantId", "PurchaseOrderReceiptId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionCases_WorkflowDefinitionId",
                table: "ProcurementReceiptInspectionCases",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionCases_WorkflowInstanceId",
                table: "ProcurementReceiptInspectionCases",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionEvidence_FileUploadRecordId",
                table: "ProcurementReceiptInspectionEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionEvidence_InspectionCaseId",
                table: "ProcurementReceiptInspectionEvidence",
                column: "InspectionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionEvidence_TenantId_InspectionCaseId_ActionKey_RequirementKey",
                table: "ProcurementReceiptInspectionEvidence",
                columns: new[] { "TenantId", "InspectionCaseId", "ActionKey", "RequirementKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionEvidence_WorkflowEvidenceDocumentId",
                table: "ProcurementReceiptInspectionEvidence",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionLines_InspectionCaseId",
                table: "ProcurementReceiptInspectionLines",
                column: "InspectionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionLines_PurchaseOrderReceiptItemId",
                table: "ProcurementReceiptInspectionLines",
                column: "PurchaseOrderReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementReceiptInspectionLines_TenantId_InspectionCaseId_PurchaseOrderReceiptItemId",
                table: "ProcurementReceiptInspectionLines",
                columns: new[] { "TenantId", "InspectionCaseId", "PurchaseOrderReceiptItemId" },
                unique: true);

            AddGovernedLifecycleTriggers(migrationBuilder);
            CorrectReceiptCapacityTriggers(migrationBuilder, useNetReceivedQuantity: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS [dbo].[TR_StockMovements_TDC0502InspectionContext];
                DROP TRIGGER IF EXISTS [dbo].[TR_InventoryMovements_TDC0502InspectionContext];
                DROP TRIGGER IF EXISTS [dbo].[TR_GoodsReceiptNoteItems_TDC0502AcceptanceProtected];
                DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseOrderItems_TDC0502AcceptedQuantity];
                DROP TRIGGER IF EXISTS [dbo].[TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptInspectionActions_TDC0502Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptInspectionEvidence_TDC0502Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptInspectionLines_TDC0502Protected];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementReceiptInspectionCases_TDC0502Protected];
                """);
            CorrectReceiptCapacityTriggers(migrationBuilder, useNetReceivedQuantity: false);

            migrationBuilder.DropTable(
                name: "ProcurementReceiptInspectionActions");

            migrationBuilder.DropTable(
                name: "ProcurementReceiptInspectionEvidence");

            migrationBuilder.DropTable(
                name: "ProcurementReceiptInspectionLines");

            migrationBuilder.DropTable(
                name: "ProcurementReceiptInspectionCases");
        }

        private static void AddGovernedLifecycleTriggers(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptInspectionCases_TDC0502Protected]
                ON [dbo].[ProcurementReceiptInspectionCases]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51501, 'RCV_INSPECTION_DELETE_FORBIDDEN: receipt-inspection cases cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN PurchaseOrderReceipts r ON r.Id = i.PurchaseOrderReceiptId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN WorkflowDefinitions w ON w.Id = i.WorkflowDefinitionId AND w.TenantId = i.TenantId AND w.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND (r.Id IS NULL OR w.Id IS NULL))
                        THROW 51502, 'RCV_INSPECTION_TENANT_MISMATCH: receipt and workflow must belong to the inspection tenant.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.PurchaseOrderReceiptId <> d.PurchaseOrderReceiptId
                           OR i.Sequence <> d.Sequence
                           OR i.ConfigurationProfileId <> d.ConfigurationProfileId
                           OR i.ConfigurationProfileVersion <> d.ConfigurationProfileVersion
                           OR i.PolicySetId <> d.PolicySetId
                           OR i.PolicyVersion <> d.PolicyVersion
                           OR i.AuthorityRuleId <> d.AuthorityRuleId
                           OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           OR i.CreatedByUserId <> d.CreatedByUserId
                           OR i.IdempotencyKey <> d.IdempotencyKey
                           OR i.SourceSnapshotJson <> d.SourceSnapshotJson
                           OR i.SourceSnapshotHash <> d.SourceSnapshotHash)
                        THROW 51503, 'RCV_INSPECTION_LINEAGE_IMMUTABLE: source and governance lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status AND NOT (
                            (d.Status = 0 AND i.Status = 1) OR
                            (d.Status = 1 AND i.Status IN (3,4,8,9)) OR
                            (d.Status = 4 AND i.Status IN (5,6)) OR
                            (d.Status = 5 AND i.Status = 7) OR
                            (d.Status = 6 AND i.Status = 7) OR
                            (d.Status = 7 AND i.Status = 8)))
                        THROW 51504, 'RCV_INSPECTION_TRANSITION_INVALID: receipt-inspection status transition is not allowed.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN WorkflowInstances wi
                          ON wi.Id = i.WorkflowInstanceId
                         AND wi.TenantId = i.TenantId
                         AND wi.EntityId = i.Id
                         AND wi.WorkflowDefinitionId = i.WorkflowDefinitionId
                        WHERE d.Status = 1 AND i.Status IN (4,8)
                          AND (TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID')) <> i.Id
                               OR wi.Id IS NULL OR wi.Status <> 2
                               OR i.DecidedByUserId IS NULL
                               OR i.DecidedByUserId = i.SubmittedByUserId
                               OR i.DecidedByUserId = i.CreatedByUserId))
                        THROW 51505, 'RCV_INSPECTION_APPROVAL_FORBIDDEN: final acceptance requires the exact completed workflow, independent approver, and protected transaction context.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN WorkflowInstances wi
                          ON wi.Id = i.WorkflowInstanceId
                         AND wi.TenantId = i.TenantId
                         AND wi.EntityId = i.Id
                        WHERE d.Status = 1 AND i.Status = 3
                          AND (wi.Id IS NULL OR wi.Status NOT IN (3,4)))
                        THROW 51506, 'RCV_INSPECTION_REJECTION_FORBIDDEN: only a rejected shared-workflow outcome may reject an inspection.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptInspectionLines_TDC0502Protected]
                ON [dbo].[ProcurementReceiptInspectionLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                        THROW 51511, 'RCV_INSPECTION_LINE_DELETE_FORBIDDEN: inspection lines cannot be deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN ProcurementReceiptInspectionCases c ON c.Id = i.InspectionCaseId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        LEFT JOIN PurchaseOrderReceiptItems r ON r.Id = i.PurchaseOrderReceiptItemId AND r.ReceiptId = c.PurchaseOrderReceiptId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND (c.Id IS NULL OR r.Id IS NULL OR i.ReceivedQuantity <> r.ReceivedQuantity))
                        THROW 51512, 'RCV_INSPECTION_LINE_SOURCE_INVALID: inspection line must match the same-tenant governed receipt line.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId OR i.InspectionCaseId <> d.InspectionCaseId
                           OR i.PurchaseOrderReceiptItemId <> d.PurchaseOrderReceiptItemId
                           OR i.ReceivedQuantity <> d.ReceivedQuantity)
                        THROW 51513, 'RCV_INSPECTION_LINE_SOURCE_IMMUTABLE: inspection line source and received quantity are immutable.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                        JOIN ProcurementReceiptInspectionCases c ON c.Id = i.InspectionCaseId AND c.TenantId = i.TenantId
                        WHERE c.Status <> 0 AND (
                            i.AcceptedQuantity <> d.AcceptedQuantity OR i.RejectedQuantity <> d.RejectedQuantity
                            OR i.PendingQuantity <> d.PendingQuantity OR i.Disposition <> d.Disposition
                            OR ISNULL(i.RejectionReason,N'') <> ISNULL(d.RejectionReason,N'')
                            OR ISNULL(i.QuarantineLocationId,'00000000-0000-0000-0000-000000000000') <> ISNULL(d.QuarantineLocationId,'00000000-0000-0000-0000-000000000000')))
                        THROW 51514, 'RCV_INSPECTION_LINE_LOCKED: inspection quantities are locked after submission.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptInspectionEvidence_TDC0502Immutable]
                ON [dbo].[ProcurementReceiptInspectionEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51521, 'RCV_INSPECTION_EVIDENCE_IMMUTABLE: controlled inspection evidence cannot be changed or deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN ProcurementReceiptInspectionCases c ON c.Id = i.InspectionCaseId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        WHERE c.Id IS NULL)
                        THROW 51522, 'RCV_INSPECTION_EVIDENCE_TENANT_MISMATCH: evidence must belong to the same-tenant inspection case.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptInspectionActions_TDC0502Immutable]
                ON [dbo].[ProcurementReceiptInspectionActions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51531, 'RCV_INSPECTION_ACTION_IMMUTABLE: inspection action history cannot be changed or deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN ProcurementReceiptInspectionCases c ON c.Id = i.InspectionCaseId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        WHERE c.Id IS NULL)
                        THROW 51532, 'RCV_INSPECTION_ACTION_TENANT_MISMATCH: action must belong to the same-tenant inspection case.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected]
                ON [dbo].[PurchaseOrderReceiptItems]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN ProcurementReceiptInspectionCases c
                          ON c.Id = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID'))
                         AND c.TenantId = i.TenantId AND c.PurchaseOrderReceiptId = i.ReceiptId AND c.IsDeleted = 0
                        LEFT JOIN ProcurementReceiptInspectionLines l
                          ON l.InspectionCaseId = c.Id AND l.PurchaseOrderReceiptItemId = i.Id AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                        LEFT JOIN WorkflowInstances wi ON wi.Id = c.WorkflowInstanceId AND wi.TenantId = c.TenantId
                        WHERE (i.AcceptedQuantity <> d.AcceptedQuantity OR i.RejectedQuantity <> d.RejectedQuantity)
                          AND (c.Id IS NULL OR l.Id IS NULL OR wi.Id IS NULL OR wi.Status <> 2
                               OR c.Status <> 1 OR i.AcceptedQuantity <> l.AcceptedQuantity
                               OR i.RejectedQuantity <> l.RejectedQuantity))
                        THROW 51541, 'RCV_DIRECT_ACCEPTANCE_BLOCKED: receipt acceptance requires the exact approved TDC-0502 inspection context.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrderItems_TDC0502AcceptedQuantity]
                ON [dbo].[PurchaseOrderItems]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i JOIN deleted d ON d.Id = i.Id
                        LEFT JOIN ProcurementReceiptInspectionCases c
                          ON c.Id = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID'))
                         AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                        LEFT JOIN PurchaseOrderReceipts r ON r.Id = c.PurchaseOrderReceiptId AND r.PurchaseOrderId = i.PurchaseOrderId AND r.TenantId = i.TenantId
                        OUTER APPLY (
                            SELECT COALESCE(SUM(l.AcceptedQuantity),0) Quantity
                            FROM ProcurementReceiptInspectionLines l
                            JOIN PurchaseOrderReceiptItems ri ON ri.Id = l.PurchaseOrderReceiptItemId AND ri.PurchaseOrderItemId = i.Id AND ri.TenantId = i.TenantId
                            WHERE l.InspectionCaseId = c.Id AND l.TenantId = i.TenantId AND l.IsDeleted = 0
                        ) accepted
                        WHERE i.ReceivedQuantity > d.ReceivedQuantity
                          AND (c.Id IS NULL OR r.Id IS NULL OR c.Status <> 1
                               OR i.ReceivedQuantity - d.ReceivedQuantity <> accepted.Quantity))
                        THROW 51542, 'RCV_PO_ACCEPTED_QUANTITY_BLOCKED: PO received quantity may increase only by the exact governed accepted quantity.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_GoodsReceiptNoteItems_TDC0502AcceptanceProtected]
                ON [dbo].[GoodsReceiptNoteItems]
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM inserted i JOIN deleted d ON d.Id = i.Id
                        JOIN GoodsReceiptNotes g ON g.Id = i.GoodsReceiptNoteId AND g.TenantId = i.TenantId AND g.PurchaseOrderReceiptId IS NOT NULL
                        LEFT JOIN ProcurementReceiptInspectionCases c
                          ON c.Id = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID'))
                         AND c.TenantId = i.TenantId AND c.PurchaseOrderReceiptId = g.PurchaseOrderReceiptId AND c.IsDeleted = 0
                        LEFT JOIN PurchaseOrderReceiptItems r ON r.ReceiptId = g.PurchaseOrderReceiptId AND r.PurchaseOrderItemId = i.PurchaseOrderItemId AND r.TenantId = i.TenantId AND r.IsDeleted = 0
                        LEFT JOIN ItemUnitsOfMeasure u ON u.Id = r.ItemUnitOfMeasureId AND u.TenantId = i.TenantId AND u.IsDeleted = 0
                        WHERE (i.AcceptedQuantity <> d.AcceptedQuantity OR i.RejectedQuantity <> d.RejectedQuantity)
                          AND (c.Id IS NULL OR r.Id IS NULL
                               OR i.AcceptedQuantity <> r.AcceptedQuantity * COALESCE(NULLIF(u.ConversionToBase,0),1)
                               OR i.RejectedQuantity <> r.RejectedQuantity * COALESCE(NULLIF(u.ConversionToBase,0),1)))
                        THROW 51543, 'RCV_GRN_DIRECT_ACCEPTANCE_BLOCKED: linked GRN acceptance must be the exact TDC-0502 receipt projection.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_InventoryMovements_TDC0502InspectionContext]
                ON [dbo].[InventoryMovements]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN ProcurementReceiptInspectionCases c
                          ON c.Id = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID'))
                         AND c.TenantId = i.TenantId AND c.PurchaseOrderReceiptId = i.ReferenceId AND c.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND i.MovementType = 1 AND i.Direction = 1 AND i.ReferenceType = 1
                          AND c.Id IS NULL)
                        THROW 51551, 'RCV_STOCK_INSPECTION_CONTEXT_REQUIRED: purchase receipt inventory movement requires the exact TDC-0502 context.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_StockMovements_TDC0502InspectionContext]
                ON [dbo].[StockMovements]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN GoodsReceiptNotes g ON g.Id = i.ReferenceId AND g.TenantId = i.TenantId AND g.PurchaseOrderReceiptId IS NOT NULL
                        LEFT JOIN ProcurementReceiptInspectionCases c
                          ON c.Id = TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'TDC0502_RECEIPT_INSPECTION_CASE_ID'))
                         AND c.TenantId = i.TenantId AND c.PurchaseOrderReceiptId = g.PurchaseOrderReceiptId AND c.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND i.MovementType = N'Receipt' AND i.ReferenceType = 1 AND c.Id IS NULL)
                        THROW 51552, 'RCV_GRN_STOCK_INSPECTION_CONTEXT_REQUIRED: linked GRN stock movement requires the exact TDC-0502 context.', 1;
                END
                """);
        }

        private static void CorrectReceiptCapacityTriggers(
            MigrationBuilder migrationBuilder,
            bool useNetReceivedQuantity)
        {
            var purchaseOld = useNetReceivedQuantity
                ? "SUM(otherLine.ReceivedQuantity)"
                : "SUM(otherLine.ReceivedQuantity - otherLine.RejectedQuantity)";
            var purchaseNew = useNetReceivedQuantity
                ? "SUM(otherLine.ReceivedQuantity - otherLine.RejectedQuantity)"
                : "SUM(otherLine.ReceivedQuantity)";
            var grnOld = useNetReceivedQuantity
                ? "SUM(grnLine.ReceivedQuantity)"
                : "SUM(grnLine.ReceivedQuantity - grnLine.RejectedQuantity)";
            var grnNew = useNetReceivedQuantity
                ? "SUM(grnLine.ReceivedQuantity - grnLine.RejectedQuantity)"
                : "SUM(grnLine.ReceivedQuantity)";
            var porOld = useNetReceivedQuantity
                ? "SUM(porLine.ReceivedQuantity)"
                : "SUM(porLine.ReceivedQuantity - porLine.RejectedQuantity)";
            var porNew = useNetReceivedQuantity
                ? "SUM(porLine.ReceivedQuantity - porLine.RejectedQuantity)"
                : "SUM(porLine.ReceivedQuantity)";

            migrationBuilder.Sql($$"""
                DECLARE @purchase nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_PurchaseOrderReceiptItems_GovernedCapacity]'));
                IF @purchase IS NULL THROW 51561, 'RCV_TDC0501_TRIGGER_MISSING: purchase receipt capacity trigger is required.', 1;
                SET @purchase = REPLACE(@purchase, N'{{purchaseOld}}', N'{{purchaseNew}}');
                SET @purchase = REPLACE(@purchase, N'{{grnOld}}', N'{{grnNew}}');
                IF CHARINDEX(N'{{purchaseNew}}', @purchase) = 0 OR CHARINDEX(N'{{grnNew}}', @purchase) = 0
                    THROW 51562, 'RCV_TDC0501_TRIGGER_DRIFT: purchase receipt capacity trigger did not match the verified baseline.', 1;
                DECLARE @purchaseTrigger int = CHARINDEX(N'TRIGGER', UPPER(@purchase));
                SET @purchase = N'ALTER ' + SUBSTRING(@purchase, @purchaseTrigger, LEN(@purchase));
                EXEC sys.sp_executesql @purchase;

                DECLARE @grn nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[TR_GoodsReceiptNoteItems_GovernedCapacity]'));
                IF @grn IS NULL THROW 51563, 'RCV_TDC0501_TRIGGER_MISSING: GRN capacity trigger is required.', 1;
                SET @grn = REPLACE(@grn, N'{{porOld}}', N'{{porNew}}');
                SET @grn = REPLACE(@grn, N'{{purchaseOld}}', N'{{purchaseNew}}');
                IF CHARINDEX(N'{{porNew}}', @grn) = 0 OR CHARINDEX(N'{{purchaseNew}}', @grn) = 0
                    THROW 51564, 'RCV_TDC0501_TRIGGER_DRIFT: GRN capacity trigger did not match the verified baseline.', 1;
                DECLARE @grnTrigger int = CHARINDEX(N'TRIGGER', UPPER(@grn));
                SET @grn = N'ALTER ' + SUBSTRING(@grn, @grnTrigger, LEN(@grn));
                EXEC sys.sp_executesql @grn;
                """);
        }
    }
}
