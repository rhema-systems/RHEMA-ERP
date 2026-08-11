using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyDesignRevisionImpacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectDrawings_ProjectId_DrawingNumber",
                table: "ProjectDrawings");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDrawings_TenantId",
                table: "ProjectDrawings");

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesDrawingId",
                table: "ProjectDrawings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuantitySurveyDesignRevisionImpacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousDrawingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisedDrawingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ImpactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Route = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowEntityTypeCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PreviousDrawingNumberSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreviousRevisionSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RevisedDrawingNumberSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RevisedRevisionSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyDesignRevisionImpacts", x => x.Id);
                    table.CheckConstraint("CK_QsDesignImpact_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                    table.CheckConstraint("CK_QsDesignImpact_Drawings", "[PreviousDrawingId] <> [RevisedDrawingId] AND LEN([RevisedRevisionSnapshot]) > 0");
                    table.CheckConstraint("CK_QsDesignImpact_Hashes", "LEN([RequestHash]) = 64 AND LEN([PolicyHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                    table.CheckConstraint("CK_QsDesignImpact_Lifecycle", "([Status] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'PendingApproval' AND [WorkflowInstanceId] IS NOT NULL AND [SubmittedAt] IS NOT NULL) OR ([Status] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_QsDesignImpact_Route", "[Route] IN (0,1)");
                    table.CheckConstraint("CK_QsDesignImpact_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_ProjectDrawings_PreviousDrawingId",
                        column: x => x.PreviousDrawingId,
                        principalTable: "ProjectDrawings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_ProjectDrawings_RevisedDrawingId",
                        column: x => x.RevisedDrawingId,
                        principalTable: "ProjectDrawings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_QuantitySurveyConfigurationDecisions_ConfigurationDecisionId",
                        column: x => x.ConfigurationDecisionId,
                        principalTable: "QuantitySurveyConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "QuantitySurveyConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpacts_Users_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyDesignRevisionImpactLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectBoqVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectBoqVersionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoqLineKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpactType = table.Column<int>(type: "int", nullable: false),
                    LineReferenceSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UnitSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PreviousQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IndicativeQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ImpactReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyDesignRevisionImpactLines", x => x.Id);
                    table.CheckConstraint("CK_QsDesignImpactLine_Quantity", "[PreviousQuantity] >= 0 AND ([IndicativeQuantity] IS NULL OR [IndicativeQuantity] >= 0)");
                    table.CheckConstraint("CK_QsDesignImpactLine_Type", "[ImpactType] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpactLines_ProjectBoqVersionLines_ProjectBoqVersionLineId",
                        column: x => x.ProjectBoqVersionLineId,
                        principalTable: "ProjectBoqVersionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpactLines_ProjectBoqVersions_ProjectBoqVersionId",
                        column: x => x.ProjectBoqVersionId,
                        principalTable: "ProjectBoqVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpactLines_QuantitySurveyDesignRevisionImpacts_ImpactId",
                        column: x => x.ImpactId,
                        principalTable: "QuantitySurveyDesignRevisionImpacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpactLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyDesignRevisionImpactRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyDesignRevisionImpactRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpactRevisions_QuantitySurveyDesignRevisionImpacts_ImpactId",
                        column: x => x.ImpactId,
                        principalTable: "QuantitySurveyDesignRevisionImpacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpactRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyDesignRevisionImpactRevisions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectId_DrawingNumber_Revision",
                table: "ProjectDrawings",
                columns: new[] { "ProjectId", "DrawingNumber", "Revision" },
                unique: true,
                filter: "[Revision] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_SupersedesDrawingId",
                table: "ProjectDrawings",
                column: "SupersedesDrawingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_TenantId_SupersedesDrawingId",
                table: "ProjectDrawings",
                columns: new[] { "TenantId", "SupersedesDrawingId" },
                unique: true,
                filter: "[SupersedesDrawingId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactLines_ImpactId",
                table: "QuantitySurveyDesignRevisionImpactLines",
                column: "ImpactId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactLines_ProjectBoqVersionId",
                table: "QuantitySurveyDesignRevisionImpactLines",
                column: "ProjectBoqVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactLines_ProjectBoqVersionLineId",
                table: "QuantitySurveyDesignRevisionImpactLines",
                column: "ProjectBoqVersionLineId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactLines_TenantId_ImpactId_ProjectBoqVersionLineId",
                table: "QuantitySurveyDesignRevisionImpactLines",
                columns: new[] { "TenantId", "ImpactId", "ProjectBoqVersionLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactLines_TenantId_ProjectBoqVersionLineId_CreatedAt",
                table: "QuantitySurveyDesignRevisionImpactLines",
                columns: new[] { "TenantId", "ProjectBoqVersionLineId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactRevisions_ActorUserId",
                table: "QuantitySurveyDesignRevisionImpactRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactRevisions_ImpactId",
                table: "QuantitySurveyDesignRevisionImpactRevisions",
                column: "ImpactId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactRevisions_TenantId_CorrelationId",
                table: "QuantitySurveyDesignRevisionImpactRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpactRevisions_TenantId_ImpactId_CreatedAt",
                table: "QuantitySurveyDesignRevisionImpactRevisions",
                columns: new[] { "TenantId", "ImpactId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_ApprovedById",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_ConfigurationDecisionId",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "ConfigurationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_ConfigurationProfileId",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_CreatedByUserId",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_PreviousDrawingId",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "PreviousDrawingId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_ProjectId",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_RevisedDrawingId",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "RevisedDrawingId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_SubmittedById",
                table: "QuantitySurveyDesignRevisionImpacts",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_TenantId_ClientRequestId",
                table: "QuantitySurveyDesignRevisionImpacts",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_TenantId_ImpactNumber",
                table: "QuantitySurveyDesignRevisionImpacts",
                columns: new[] { "TenantId", "ImpactNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_TenantId_LastMutationClientRequestId",
                table: "QuantitySurveyDesignRevisionImpacts",
                columns: new[] { "TenantId", "LastMutationClientRequestId" },
                unique: true,
                filter: "[LastMutationClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_TenantId_ProjectId_RevisedDrawingId_Route",
                table: "QuantitySurveyDesignRevisionImpacts",
                columns: new[] { "TenantId", "ProjectId", "RevisedDrawingId", "Route" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyDesignRevisionImpacts_TenantId_ProjectId_Status_CreatedAt",
                table: "QuantitySurveyDesignRevisionImpacts",
                columns: new[] { "TenantId", "ProjectId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDrawings_ProjectDrawings_SupersedesDrawingId",
                table: "ProjectDrawings",
                column: "SupersedesDrawingId",
                principalTable: "ProjectDrawings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectDrawings_QsRevisionLineage]
                ON [dbo].[ProjectDrawings]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN [dbo].[ProjectDrawings] p ON p.[Id] = i.[SupersedesDrawingId]
                        WHERE i.[SupersedesDrawingId] IS NOT NULL AND
                              (p.[TenantId] <> i.[TenantId] OR p.[ProjectId] <> i.[ProjectId] OR
                               UPPER(LTRIM(RTRIM(p.[DrawingNumber]))) <> UPPER(LTRIM(RTRIM(i.[DrawingNumber]))) OR
                               ISNULL(UPPER(LTRIM(RTRIM(p.[Revision]))), '') = ISNULL(UPPER(LTRIM(RTRIM(i.[Revision]))), '') OR
                               p.[Id] = i.[Id] OR p.[IsDeleted] = 1)
                    ) THROW 51131, 'Invalid project drawing revision lineage.', 1;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE EXISTS (SELECT 1 FROM [dbo].[QuantitySurveyDesignRevisionImpacts] q
                                      WHERE q.[IsDeleted] = 0 AND (q.[PreviousDrawingId] = d.[Id] OR q.[RevisedDrawingId] = d.[Id]))
                          AND (i.[Id] IS NULL OR i.[IsDeleted] = 1 OR i.[TenantId] <> d.[TenantId] OR
                               i.[ProjectId] <> d.[ProjectId] OR i.[DrawingNumber] <> d.[DrawingNumber] OR
                               ISNULL(i.[Revision], '') <> ISNULL(d.[Revision], '') OR
                               ISNULL(i.[SupersedesDrawingId], '00000000-0000-0000-0000-000000000000') <>
                               ISNULL(d.[SupersedesDrawingId], '00000000-0000-0000-0000-000000000000'))
                    ) THROW 51132, 'Drawing lineage referenced by a QS design impact is immutable.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QsDesignRevisionImpacts_Guard]
                ON [dbo].[QuantitySurveyDesignRevisionImpacts]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51133, 'QS design revision impacts cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[ProjectDrawings] p ON p.[Id] = i.[PreviousDrawingId]
                        LEFT JOIN [dbo].[ProjectDrawings] r ON r.[Id] = i.[RevisedDrawingId]
                        LEFT JOIN [dbo].[QuantitySurveyConfigurationProfiles] cp ON cp.[Id] = i.[ConfigurationProfileId]
                        LEFT JOIN [dbo].[QuantitySurveyConfigurationDecisions] cd ON cd.[Id] = i.[ConfigurationDecisionId]
                        LEFT JOIN [dbo].[WorkflowDefinitions] wd ON wd.[Id] = i.[ApprovalWorkflowDefinitionId]
                        LEFT JOIN [dbo].[WorkflowEntityTypes] wet ON wet.[Id] = wd.[EntityTypeId]
                        WHERE p.[Id] IS NULL OR r.[Id] IS NULL OR cp.[Id] IS NULL OR cd.[Id] IS NULL OR wd.[Id] IS NULL OR wet.[Id] IS NULL OR
                              p.[TenantId] <> i.[TenantId] OR r.[TenantId] <> i.[TenantId] OR cp.[TenantId] <> i.[TenantId] OR
                              cd.[TenantId] <> i.[TenantId] OR wd.[TenantId] <> i.[TenantId] OR wet.[TenantId] <> i.[TenantId] OR
                              p.[ProjectId] <> i.[ProjectId] OR r.[ProjectId] <> i.[ProjectId] OR r.[SupersedesDrawingId] <> p.[Id] OR
                              UPPER(LTRIM(RTRIM(p.[DrawingNumber]))) <> UPPER(LTRIM(RTRIM(r.[DrawingNumber]))) OR
                              p.[DrawingNumber] <> i.[PreviousDrawingNumberSnapshot] OR ISNULL(p.[Revision], '') <> ISNULL(i.[PreviousRevisionSnapshot], '') OR
                              r.[DrawingNumber] <> i.[RevisedDrawingNumberSnapshot] OR r.[Revision] <> i.[RevisedRevisionSnapshot] OR
                              cd.[ProfileId] <> cp.[Id] OR
                              (i.[Route] = 0 AND (cd.[DecisionKey] <> 'QS-DEC-007' OR wet.[Code] <> 'QS_MEASUREMENT' OR i.[WorkflowEntityTypeCode] <> 'QS_MEASUREMENT')) OR
                              (i.[Route] = 1 AND (cd.[DecisionKey] <> 'QS-DEC-011' OR wet.[Code] <> 'QS_VARIATION' OR i.[WorkflowEntityTypeCode] <> 'QS_VARIATION')) OR
                              NOT EXISTS (SELECT 1 FROM [dbo].[Users] u WHERE u.[Id] = i.[CreatedByUserId] AND u.[TenantId] = i.[TenantId]) OR
                              (i.[SubmittedById] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Users] u WHERE u.[Id] = i.[SubmittedById] AND u.[TenantId] = i.[TenantId])) OR
                              (i.[ApprovedById] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[Users] u WHERE u.[Id] = i.[ApprovedById] AND u.[TenantId] = i.[TenantId]))
                    ) THROW 51134, 'Invalid tenant, drawing, configuration, workflow, or actor lineage for QS design impact.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[TenantId] <> d.[TenantId] OR i.[ProjectId] <> d.[ProjectId] OR
                              i.[PreviousDrawingId] <> d.[PreviousDrawingId] OR i.[RevisedDrawingId] <> d.[RevisedDrawingId] OR
                              i.[ClientRequestId] <> d.[ClientRequestId] OR i.[RequestHash] <> d.[RequestHash] OR
                              i.[Route] <> d.[Route] OR i.[ConfigurationProfileId] <> d.[ConfigurationProfileId] OR
                              i.[ConfigurationDecisionId] <> d.[ConfigurationDecisionId] OR
                              i.[ApprovalWorkflowDefinitionId] <> d.[ApprovalWorkflowDefinitionId] OR
                              i.[WorkflowEntityTypeCode] <> d.[WorkflowEntityTypeCode] OR i.[PolicyHash] <> d.[PolicyHash] OR
                              i.[PreviousDrawingNumberSnapshot] <> d.[PreviousDrawingNumberSnapshot] OR
                              ISNULL(i.[PreviousRevisionSnapshot], '') <> ISNULL(d.[PreviousRevisionSnapshot], '') OR
                              i.[RevisedDrawingNumberSnapshot] <> d.[RevisedDrawingNumberSnapshot] OR
                              i.[RevisedRevisionSnapshot] <> d.[RevisedRevisionSnapshot] OR
                              NOT ((d.[Status] = i.[Status]) OR (d.[Status] = 'Draft' AND i.[Status] = 'PendingApproval') OR
                                   (d.[Status] = 'PendingApproval' AND i.[Status] IN ('Approved','Rejected')))
                    ) THROW 51135, 'QS design impact lineage or lifecycle transition is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QsDesignRevisionImpactLines_Guard]
                ON [dbo].[QuantitySurveyDesignRevisionImpactLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted) THROW 51136, 'QS design impact line history is immutable.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[QuantitySurveyDesignRevisionImpacts] h ON h.[Id] = i.[ImpactId]
                        LEFT JOIN [dbo].[ProjectBoqVersionLines] l ON l.[Id] = i.[ProjectBoqVersionLineId]
                        LEFT JOIN [dbo].[ProjectBoqVersions] v ON v.[Id] = i.[ProjectBoqVersionId]
                        WHERE h.[Id] IS NULL OR l.[Id] IS NULL OR v.[Id] IS NULL OR h.[Status] <> 'Draft' OR
                              h.[TenantId] <> i.[TenantId] OR l.[TenantId] <> i.[TenantId] OR v.[TenantId] <> i.[TenantId] OR
                              h.[ProjectId] <> l.[ProjectId] OR l.[ProjectBoqVersionId] <> v.[Id] OR l.[LineKey] <> i.[BoqLineKey] OR
                              v.[ProjectId] <> h.[ProjectId] OR v.[Status] <> 'Approved' OR v.[PublishedAt] IS NULL OR
                              l.[Quantity] <> i.[PreviousQuantity] OR
                              (h.[Route] = 0 AND i.[ImpactType] IN (3,4,5)) OR
                              (h.[Route] = 1 AND i.[ImpactType] = 0) OR
                              (i.[ImpactType] = 1 AND (i.[IndicativeQuantity] IS NULL OR i.[IndicativeQuantity] <= i.[PreviousQuantity])) OR
                              (i.[ImpactType] = 2 AND (i.[IndicativeQuantity] IS NULL OR i.[IndicativeQuantity] >= i.[PreviousQuantity])) OR
                              (i.[ImpactType] = 4 AND ISNULL(i.[IndicativeQuantity], 0) <> 0)
                    ) THROW 51137, 'Invalid approved-BoQ or route lineage for QS design impact line.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QsDesignRevisionImpactRevisions_Immutable]
                ON [dbo].[QuantitySurveyDesignRevisionImpactRevisions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted) THROW 51138, 'QS design impact audit revisions are append-only.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN [dbo].[QuantitySurveyDesignRevisionImpacts] h ON h.[Id] = i.[ImpactId]
                        LEFT JOIN [dbo].[Users] u ON u.[Id] = i.[ActorUserId]
                        WHERE h.[Id] IS NULL OR u.[Id] IS NULL OR h.[TenantId] <> i.[TenantId] OR u.[TenantId] <> i.[TenantId]
                    ) THROW 51139, 'Invalid tenant or actor lineage for QS design impact audit revision.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsDesignRevisionImpactRevisions_Immutable]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsDesignRevisionImpactLines_Guard]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsDesignRevisionImpacts_Guard]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectDrawings_QsRevisionLineage]");
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDrawings_ProjectDrawings_SupersedesDrawingId",
                table: "ProjectDrawings");

            migrationBuilder.DropTable(
                name: "QuantitySurveyDesignRevisionImpactLines");

            migrationBuilder.DropTable(
                name: "QuantitySurveyDesignRevisionImpactRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyDesignRevisionImpacts");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDrawings_ProjectId_DrawingNumber_Revision",
                table: "ProjectDrawings");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDrawings_SupersedesDrawingId",
                table: "ProjectDrawings");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDrawings_TenantId_SupersedesDrawingId",
                table: "ProjectDrawings");

            migrationBuilder.DropColumn(
                name: "SupersedesDrawingId",
                table: "ProjectDrawings");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectId_DrawingNumber",
                table: "ProjectDrawings",
                columns: new[] { "ProjectId", "DrawingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_TenantId",
                table: "ProjectDrawings",
                column: "TenantId");
        }
    }
}
