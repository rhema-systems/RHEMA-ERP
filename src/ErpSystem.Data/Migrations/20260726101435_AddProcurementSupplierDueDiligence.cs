using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierDueDiligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementSupplierDueDiligenceReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    ReviewType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    ReviewPeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewPeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextReviewDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PolicyDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyProfileVersion = table.Column<int>(type: "int", nullable: false),
                    ReviewFrequencyMonths = table.Column<int>(type: "int", nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersedesReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededByReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierDueDiligenceReviews", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierDueDiligenceReviews_State", "[CycleNumber] >= 1 AND [ReviewType] BETWEEN 0 AND 1 AND [Status] BETWEEN 0 AND 5 AND [Outcome] BETWEEN 0 AND 2 AND [ReviewPeriodEndUtc] > [ReviewPeriodStartUtc] AND [ReviewFrequencyMonths] BETWEEN 1 AND 120 AND [PolicyProfileVersion] >= 1 AND LEN([PolicyValueHash]) = 64 AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 AND ISJSON([SnapshotJson]) = 1 AND (([Status] = 0 AND [Outcome] = 0) OR [Status] <> 0) AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL) OR [Status] <> 1) AND (([Status] = 2 AND [ApprovedById] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL) OR [Status] <> 2) AND (([Status] = 3 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 3) AND (([Status] = 4 AND [ExpiredAtUtc] IS NOT NULL) OR [Status] <> 4) AND (([Status] = 5 AND [SupersededByReviewId] IS NOT NULL) OR [Status] <> 5)");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceReviews_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceReviews_ProcurementConfigurationDecisions_PolicyDecisionId",
                        column: x => x.PolicyDecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceReviews_ProcurementConfigurationProfiles_PolicyProfileId",
                        column: x => x.PolicyProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceReviews_ProcurementSupplierDueDiligenceReviews_SupersedesReviewId",
                        column: x => x.SupersedesReviewId,
                        principalTable: "ProcurementSupplierDueDiligenceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceReviews_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceReviews_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceReviews_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierDueDiligenceChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementSupplierDueDiligenceChecks", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierDueDiligenceChecks_State", "[CheckType] BETWEEN 0 AND 5 AND [Status] BETWEEN 0 AND 3 AND LEN([SourceName]) > 0 AND LEN([SourceReference]) > 0 AND ([ValidUntilUtc] IS NULL OR [CheckedAtUtc] IS NULL OR [ValidUntilUtc] > [CheckedAtUtc]) AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceChecks_ProcurementSupplierDueDiligenceReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "ProcurementSupplierDueDiligenceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceChecks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierDueDiligenceEvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceKind = table.Column<int>(type: "int", nullable: false),
                    WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequirementKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierDueDiligenceEvidenceLinks", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierDueDiligenceEvidenceLinks_State", "[ReferenceKind] BETWEEN 0 AND 2 AND LEN([Reference]) > 0 AND LEN([RequirementKey]) > 0 AND LEN([IntegrityHash]) = 64 AND (([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [FileUploadRecordId] IS NOT NULL AND [WorkflowEvidenceDocumentId] IS NULL) OR ([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceEvidenceLinks_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceEvidenceLinks_ProcurementSupplierDueDiligenceChecks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "ProcurementSupplierDueDiligenceChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceEvidenceLinks_ProcurementSupplierDueDiligenceReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "ProcurementSupplierDueDiligenceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceEvidenceLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierDueDiligenceEvidenceLinks_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceChecks_ReviewId",
                table: "ProcurementSupplierDueDiligenceChecks",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceChecks_TenantId_ReviewId_CheckType",
                table: "ProcurementSupplierDueDiligenceChecks",
                columns: new[] { "TenantId", "ReviewId", "CheckType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceChecks_TenantId_Status_ValidUntilUtc",
                table: "ProcurementSupplierDueDiligenceChecks",
                columns: new[] { "TenantId", "Status", "ValidUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceEvidenceLinks_CheckId",
                table: "ProcurementSupplierDueDiligenceEvidenceLinks",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceEvidenceLinks_FileUploadRecordId",
                table: "ProcurementSupplierDueDiligenceEvidenceLinks",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceEvidenceLinks_ReviewId",
                table: "ProcurementSupplierDueDiligenceEvidenceLinks",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceEvidenceLinks_TenantId_CheckId_ReferenceKind_Reference",
                table: "ProcurementSupplierDueDiligenceEvidenceLinks",
                columns: new[] { "TenantId", "CheckId", "ReferenceKind", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceEvidenceLinks_TenantId_ReviewId",
                table: "ProcurementSupplierDueDiligenceEvidenceLinks",
                columns: new[] { "TenantId", "ReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceEvidenceLinks_WorkflowEvidenceDocumentId",
                table: "ProcurementSupplierDueDiligenceEvidenceLinks",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_BusinessPartnerId",
                table: "ProcurementSupplierDueDiligenceReviews",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_PolicyDecisionId",
                table: "ProcurementSupplierDueDiligenceReviews",
                column: "PolicyDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_PolicyProfileId",
                table: "ProcurementSupplierDueDiligenceReviews",
                column: "PolicyProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_SupersedesReviewId",
                table: "ProcurementSupplierDueDiligenceReviews",
                column: "SupersedesReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_TenantId_BusinessPartnerId",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "BusinessPartnerId" },
                unique: true,
                filter: "[Status] IN (0, 1) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_TenantId_BusinessPartnerId_CycleNumber",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "BusinessPartnerId", "CycleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_TenantId_CreationCorrelationId",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_TenantId_PolicyDecisionId",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "PolicyDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_TenantId_ReviewReference",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "ReviewReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_TenantId_Status_ReviewPeriodEndUtc",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "Status", "ReviewPeriodEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_TenantId_WorkflowInstanceId",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_WorkflowDefinitionId",
                table: "ProcurementSupplierDueDiligenceReviews",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierDueDiligenceReviews_WorkflowInstanceId",
                table: "ProcurementSupplierDueDiligenceReviews",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcurementSupplierDueDiligenceReviews_CurrentApproved",
                table: "ProcurementSupplierDueDiligenceReviews",
                columns: new[] { "TenantId", "BusinessPartnerId", "Status" },
                unique: true,
                filter: "[Status] = 2 AND [IsDeleted] = 0");

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierDueDiligenceReviews_Lifecycle]
                ON [dbo].[ProcurementSupplierDueDiligenceReviews]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL
                    )
                        THROW 51000, 'Supplier due-diligence reviews cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
                        LEFT JOIN ProcurementConfigurationDecisions pd ON pd.Id = i.PolicyDecisionId
                        LEFT JOIN ProcurementConfigurationProfiles pp ON pp.Id = i.PolicyProfileId
                        LEFT JOIN WorkflowDefinitions wd ON wd.Id = i.WorkflowDefinitionId
                        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
                        LEFT JOIN ProcurementSupplierDueDiligenceReviews sr ON sr.Id = i.SupersedesReviewId
                        LEFT JOIN ProcurementSupplierDueDiligenceReviews sbr ON sbr.Id = i.SupersededByReviewId
                        WHERE bp.Id IS NULL OR bp.TenantId <> i.TenantId
                           OR pd.Id IS NULL OR pd.TenantId <> i.TenantId
                           OR pp.Id IS NULL OR pp.TenantId <> i.TenantId
                           OR wd.Id IS NULL OR wd.TenantId <> i.TenantId
                           OR (i.WorkflowInstanceId IS NOT NULL
                               AND (wi.Id IS NULL OR wi.TenantId <> i.TenantId))
                           OR (i.SupersedesReviewId IS NOT NULL
                               AND (sr.Id IS NULL OR sr.TenantId <> i.TenantId
                                    OR sr.BusinessPartnerId <> i.BusinessPartnerId))
                           OR (i.SupersededByReviewId IS NOT NULL
                               AND (sbr.Id IS NULL OR sbr.TenantId <> i.TenantId
                                    OR sbr.BusinessPartnerId <> i.BusinessPartnerId))
                    )
                        THROW 51001, 'Supplier due-diligence review references must belong to the same tenant.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status <> i.Status
                          AND NOT (
                              (d.Status = 0 AND i.Status = 1)
                              OR (d.Status = 1 AND i.Status IN (2, 3))
                              OR (d.Status = 2 AND i.Status IN (4, 5))
                          )
                    )
                        THROW 51002, 'Invalid supplier due-diligence lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status IN (3, 4, 5)
                    )
                        THROW 51003, 'Rejected, expired, and superseded supplier due-diligence reviews are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.BusinessPartnerId <> d.BusinessPartnerId
                           OR i.ReviewReference <> d.ReviewReference
                           OR i.CycleNumber <> d.CycleNumber
                           OR i.ReviewType <> d.ReviewType
                           OR i.ReviewPeriodStartUtc <> d.ReviewPeriodStartUtc
                           OR i.ReviewPeriodEndUtc <> d.ReviewPeriodEndUtc
                           OR ISNULL(i.NextReviewDueAtUtc, '19000101') <>
                              ISNULL(d.NextReviewDueAtUtc, '19000101')
                           OR i.PolicyDecisionId <> d.PolicyDecisionId
                           OR i.PolicyProfileId <> d.PolicyProfileId
                           OR i.PolicyProfileCode <> d.PolicyProfileCode
                           OR i.PolicyProfileVersion <> d.PolicyProfileVersion
                           OR i.ReviewFrequencyMonths <> d.ReviewFrequencyMonths
                           OR i.PolicySnapshotJson <> d.PolicySnapshotJson
                           OR i.PolicyValueHash <> d.PolicyValueHash
                           OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           OR ISNULL(i.SupersedesReviewId, '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.SupersedesReviewId, '00000000-0000-0000-0000-000000000000')
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.CreatedAt <> d.CreatedAt
                    )
                        THROW 51004, 'Supplier due-diligence identity, policy, workflow, period, and cycle lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                          AND NOT (
                              d.Status = 1 AND i.Status = 1
                              AND d.WorkflowInstanceId IS NULL
                              AND i.WorkflowInstanceId IS NOT NULL
                          )
                    )
                        THROW 51005, 'Workflow instance lineage can only be attached once after submission.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierDueDiligenceChecks_Protected]
                ON [dbo].[ProcurementSupplierDueDiligenceChecks]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementSupplierDueDiligenceReviews r ON r.Id = i.ReviewId
                        WHERE r.Id IS NULL OR r.TenantId <> i.TenantId OR r.Status <> 0
                    )
                        THROW 51010, 'Due-diligence checks can only be written for a same-tenant Draft review.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        LEFT JOIN ProcurementSupplierDueDiligenceReviews r ON r.Id = d.ReviewId
                        WHERE i.Id IS NULL AND (r.Id IS NULL OR r.Status <> 0)
                    )
                        THROW 51011, 'Due-diligence checks cannot be deleted after submission.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.ReviewId <> d.ReviewId
                           OR i.CheckType <> d.CheckType
                           OR i.CreatedAt <> d.CreatedAt
                    )
                        THROW 51012, 'Due-diligence check identity and review lineage are immutable.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierDueDiligenceEvidenceLinks_Protected]
                ON [dbo].[ProcurementSupplierDueDiligenceEvidenceLinks]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementSupplierDueDiligenceReviews r ON r.Id = i.ReviewId
                        LEFT JOIN ProcurementSupplierDueDiligenceChecks c ON c.Id = i.CheckId
                        LEFT JOIN WorkflowEvidenceDocuments we ON we.Id = i.WorkflowEvidenceDocumentId
                        LEFT JOIN FileUploadRecords fu ON fu.Id = i.FileUploadRecordId
                        WHERE r.Id IS NULL OR c.Id IS NULL
                           OR r.Status <> 0
                           OR r.TenantId <> i.TenantId
                           OR c.TenantId <> i.TenantId
                           OR c.ReviewId <> i.ReviewId
                           OR (i.WorkflowEvidenceDocumentId IS NOT NULL
                               AND (we.Id IS NULL OR we.TenantId <> i.TenantId))
                           OR (i.FileUploadRecordId IS NOT NULL
                               AND (fu.Id IS NULL OR fu.TenantId <> i.TenantId))
                    )
                        THROW 51020, 'Due-diligence evidence must belong to the same tenant, check, and Draft review.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        LEFT JOIN ProcurementSupplierDueDiligenceReviews r ON r.Id = d.ReviewId
                        WHERE i.Id IS NULL AND (r.Id IS NULL OR r.Status <> 0)
                    )
                        THROW 51021, 'Due-diligence evidence cannot be deleted after submission.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.ReviewId <> d.ReviewId
                           OR i.CheckId <> d.CheckId
                           OR i.ReferenceKind <> d.ReferenceKind
                           OR ISNULL(i.WorkflowEvidenceDocumentId, '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.WorkflowEvidenceDocumentId, '00000000-0000-0000-0000-000000000000')
                           OR ISNULL(i.FileUploadRecordId, '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.FileUploadRecordId, '00000000-0000-0000-0000-000000000000')
                           OR i.Reference <> d.Reference
                           OR i.RequirementKey <> d.RequirementKey
                           OR i.IntegrityHash <> d.IntegrityHash
                           OR i.CreatedAt <> d.CreatedAt
                    )
                        THROW 51022, 'Due-diligence evidence references and integrity lineage are immutable.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementSupplierDueDiligenceEvidenceLinks");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierDueDiligenceChecks");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierDueDiligenceReviews");
        }
    }
}
