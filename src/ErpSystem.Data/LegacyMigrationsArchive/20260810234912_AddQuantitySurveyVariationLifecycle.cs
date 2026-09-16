using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyVariationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_TenantId",
                table: "ProjectVariationOrders");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "ProjectVariationOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalWorkflowDefinitionId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ProjectVariationOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBoqVersionId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetImpactAmount",
                table: "ProjectVariationOrders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChangeRequestId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigurationProfileId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractorBusinessPartnerId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "ProjectVariationOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceMetadataTemplateId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ForecastImpactAmount",
                table: "ProjectVariationOrders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuantitySurveyGoverned",
                table: "ProjectVariationOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMutationClientRequestId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMutationRequestHash",
                table: "ProjectVariationOrders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalContractSumSnapshot",
                table: "ProjectVariationOrders",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyHash",
                table: "ProjectVariationOrders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreparedById",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ProjectVariationOrders",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "ProjectVariationOrders",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RevisedContractSumSnapshot",
                table: "ProjectVariationOrders",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectVariationOrders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteInstructionId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "ProjectVariationOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedById",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateBudgetOnApplication",
                table: "ProjectVariationOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateCertificateOnApplication",
                table: "ProjectVariationOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateContractSumOnApplication",
                table: "ProjectVariationOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateForecastOnApplication",
                table: "ProjectVariationOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VariationDecisionId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VariationSourceType",
                table: "ProjectVariationOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "ProjectVariationOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuantitySurveyVariationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariationOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyVariationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyVariationEvidence_ProjectVariationOrders_VariationOrderId",
                        column: x => x.VariationOrderId,
                        principalTable: "ProjectVariationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyVariationEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyVariationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariationOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyVariationRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyVariationRevisions_ProjectVariationOrders_VariationOrderId",
                        column: x => x.VariationOrderId,
                        principalTable: "ProjectVariationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyVariationRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyVariationValuationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariationOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectBoqVersionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoqLineKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    LineReferenceSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UnitSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    QuantityChange = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValuationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyVariationValuationLines", x => x.Id);
                    table.CheckConstraint("CK_QsVariationLine_Value", "[Sequence] > 0 AND [QuantityChange] <> 0 AND [UnitRate] >= 0 AND [Amount] = ROUND([QuantityChange] * [UnitRate], 2) AND LEN([SourceHash]) = 64");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyVariationValuationLines_ProjectBoqVersionLines_ProjectBoqVersionLineId",
                        column: x => x.ProjectBoqVersionLineId,
                        principalTable: "ProjectBoqVersionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyVariationValuationLines_ProjectVariationOrders_VariationOrderId",
                        column: x => x.VariationOrderId,
                        principalTable: "ProjectVariationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyVariationValuationLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ApprovedBoqVersionId",
                table: "ProjectVariationOrders",
                column: "ApprovedBoqVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ChangeRequestId",
                table: "ProjectVariationOrders",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ConfigurationProfileId",
                table: "ProjectVariationOrders",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_ContractorBusinessPartnerId",
                table: "ProjectVariationOrders",
                column: "ContractorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_SiteInstructionId",
                table: "ProjectVariationOrders",
                column: "SiteInstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_TenantId_ClientRequestId",
                table: "ProjectVariationOrders",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[IsQuantitySurveyGoverned] = 1 AND [ClientRequestId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_TenantId_ReferenceNumber",
                table: "ProjectVariationOrders",
                columns: new[] { "TenantId", "ReferenceNumber" },
                unique: true,
                filter: "[IsQuantitySurveyGoverned] = 1 AND [ReferenceNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_VariationDecisionId",
                table: "ProjectVariationOrders",
                column: "VariationDecisionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsVariation_Governance",
                table: "ProjectVariationOrders",
                sql: "[IsQuantitySurveyGoverned] = 0 OR ([ContractId] IS NOT NULL AND [ContractorBusinessPartnerId] IS NOT NULL AND [ApprovedBoqVersionId] IS NOT NULL AND [ConfigurationProfileId] IS NOT NULL AND [VariationDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [EvidenceMetadataTemplateId] IS NOT NULL AND [PreparedById] IS NOT NULL AND LEN([PolicyHash]) = 64 AND LEN([RequestHash]) = 64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_QsVariation_Source",
                table: "ProjectVariationOrders",
                sql: "[IsQuantitySurveyGoverned] = 0 OR (([SiteInstructionId] IS NOT NULL AND [ChangeRequestId] IS NULL AND [VariationSourceType] = 0) OR ([SiteInstructionId] IS NULL AND [ChangeRequestId] IS NOT NULL AND [VariationSourceType] = 1) OR ([SiteInstructionId] IS NULL AND [ChangeRequestId] IS NULL AND [VariationSourceType] IN (2,3)))");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationEvidence_TenantId_ClientRequestId",
                table: "QuantitySurveyVariationEvidence",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationEvidence_TenantId_VariationOrderId_CreatedAt",
                table: "QuantitySurveyVariationEvidence",
                columns: new[] { "TenantId", "VariationOrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationEvidence_VariationOrderId",
                table: "QuantitySurveyVariationEvidence",
                column: "VariationOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationRevisions_TenantId_ClientRequestId",
                table: "QuantitySurveyVariationRevisions",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationRevisions_TenantId_VariationOrderId_CreatedAt",
                table: "QuantitySurveyVariationRevisions",
                columns: new[] { "TenantId", "VariationOrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationRevisions_VariationOrderId",
                table: "QuantitySurveyVariationRevisions",
                column: "VariationOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationValuationLines_ProjectBoqVersionLineId",
                table: "QuantitySurveyVariationValuationLines",
                column: "ProjectBoqVersionLineId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationValuationLines_TenantId_VariationOrderId_ProjectBoqVersionLineId",
                table: "QuantitySurveyVariationValuationLines",
                columns: new[] { "TenantId", "VariationOrderId", "ProjectBoqVersionLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationValuationLines_TenantId_VariationOrderId_Sequence",
                table: "QuantitySurveyVariationValuationLines",
                columns: new[] { "TenantId", "VariationOrderId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyVariationValuationLines_VariationOrderId",
                table: "QuantitySurveyVariationValuationLines",
                column: "VariationOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectVariationOrders_BusinessPartners_ContractorBusinessPartnerId",
                table: "ProjectVariationOrders",
                column: "ContractorBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectVariationOrders_ProjectBoqVersions_ApprovedBoqVersionId",
                table: "ProjectVariationOrders",
                column: "ApprovedBoqVersionId",
                principalTable: "ProjectBoqVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectVariationOrders_ProjectChangeRequests_ChangeRequestId",
                table: "ProjectVariationOrders",
                column: "ChangeRequestId",
                principalTable: "ProjectChangeRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectVariationOrders_ProjectSiteInstructions_SiteInstructionId",
                table: "ProjectVariationOrders",
                column: "SiteInstructionId",
                principalTable: "ProjectSiteInstructions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectVariationOrders_QuantitySurveyConfigurationDecisions_VariationDecisionId",
                table: "ProjectVariationOrders",
                column: "VariationDecisionId",
                principalTable: "QuantitySurveyConfigurationDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectVariationOrders_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                table: "ProjectVariationOrders",
                column: "ConfigurationProfileId",
                principalTable: "QuantitySurveyConfigurationProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0508_ProjectVariation_Governance]
                ON [dbo].[ProjectVariationOrders]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE d.IsQuantitySurveyGoverned = 1 AND i.Id IS NULL)
                        THROW 51891, 'Governed Quantity Survey variations cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.IsQuantitySurveyGoverned = 0
                          AND EXISTS (
                              SELECT 1
                              FROM dbo.QuantitySurveyConfigurationProfiles p
                              JOIN dbo.QuantitySurveyConfigurationDecisions d
                                ON d.ProfileId = p.Id AND d.TenantId = p.TenantId
                              WHERE p.TenantId = i.TenantId AND p.LifecycleStatus = 1
                                AND p.PublishedAt IS NOT NULL AND p.EffectiveFrom <= SYSUTCDATETIME()
                                AND (p.EffectiveTo IS NULL OR p.EffectiveTo >= SYSUTCDATETIME())
                                AND p.IsDeleted = 0 AND d.DecisionKey = 'QS-DEC-011'
                                AND d.Status = 2 AND d.ApprovalStatus = 1 AND d.EvidenceStatus = 2
                                AND (d.EffectiveFrom IS NULL OR d.EffectiveFrom <= SYSUTCDATETIME())
                                AND (d.EffectiveTo IS NULL OR d.EffectiveTo >= SYSUTCDATETIME())
                                AND d.IsDeleted = 0))
                        THROW 51892, 'Direct legacy variation mutation is disabled while QS-DEC-011 is effective.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN dbo.Projects p
                          ON p.Id = i.ProjectId AND p.TenantId = i.TenantId AND p.IsDeleted = 0
                        LEFT JOIN dbo.Contracts c
                          ON c.Id = i.ContractId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                         AND c.ContractType = 'Works' AND c.Status = 'Active'
                        LEFT JOIN dbo.Tenders t
                          ON t.Id = c.TenderId AND t.TenantId = i.TenantId AND t.IsDeleted = 0
                        LEFT JOIN dbo.PurchaseRequisitions pr
                          ON pr.Id = t.SourcePurchaseRequisitionId AND pr.TenantId = i.TenantId
                         AND pr.ProjectId = i.ProjectId AND pr.IsDeleted = 0
                        LEFT JOIN dbo.BusinessPartners bp
                          ON bp.Id = i.ContractorBusinessPartnerId AND bp.TenantId = i.TenantId AND bp.IsDeleted = 0
                        LEFT JOIN dbo.ProjectBoqVersions bv
                          ON bv.Id = i.ApprovedBoqVersionId AND bv.TenantId = i.TenantId
                         AND bv.ProjectId = i.ProjectId AND bv.Status = 'Approved'
                         AND bv.PublishedAt IS NOT NULL AND bv.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyConfigurationProfiles cp
                          ON cp.Id = i.ConfigurationProfileId AND cp.TenantId = i.TenantId
                         AND cp.LifecycleStatus = 1 AND cp.PublishedAt IS NOT NULL AND cp.IsDeleted = 0
                        LEFT JOIN dbo.QuantitySurveyConfigurationDecisions cd
                          ON cd.Id = i.VariationDecisionId AND cd.TenantId = i.TenantId
                         AND cd.ProfileId = cp.Id AND cd.DecisionKey = 'QS-DEC-011'
                         AND cd.Status = 2 AND cd.ApprovalStatus = 1 AND cd.EvidenceStatus = 2 AND cd.IsDeleted = 0
                        LEFT JOIN dbo.WorkflowDefinitions wd
                          ON wd.Id = i.ApprovalWorkflowDefinitionId AND wd.TenantId = i.TenantId
                         AND wd.LifecycleStatus = 1 AND wd.IsActive = 1 AND wd.IsDeleted = 0
                        LEFT JOIN dbo.WorkflowEntityTypes wet
                          ON wet.Id = wd.EntityTypeId AND wet.TenantId = i.TenantId
                         AND wet.Code = 'QS_VARIATION' AND wet.IsActive = 1 AND wet.IsDeleted = 0
                        LEFT JOIN dbo.CentralDocumentMetadataTemplates mt
                          ON mt.Id = i.EvidenceMetadataTemplateId AND mt.TenantId = i.TenantId
                         AND mt.IsActive = 1 AND mt.PublishedAt IS NOT NULL AND mt.IsDeleted = 0
                        WHERE i.IsQuantitySurveyGoverned = 1 AND (
                            p.Id IS NULL OR c.Id IS NULL OR t.Id IS NULL OR pr.Id IS NULL OR bp.Id IS NULL OR bv.Id IS NULL OR cp.Id IS NULL
                            OR cd.Id IS NULL OR wd.Id IS NULL OR wet.Id IS NULL OR mt.Id IS NULL
                            OR c.BusinessPartnerId <> i.ContractorBusinessPartnerId
                            OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(cd.ValueJson, '$.variationWorkflowDefinitionId')) <> i.ApprovalWorkflowDefinitionId
                            OR TRY_CONVERT(uniqueidentifier, JSON_VALUE(cd.ValueJson, '$.variationEvidenceMetadataTemplateId')) <> i.EvidenceMetadataTemplateId
                            OR (i.VariationSourceType = 0 AND NOT EXISTS (
                                SELECT 1 FROM dbo.ProjectSiteInstructions si
                                WHERE si.Id = i.SiteInstructionId AND si.TenantId = i.TenantId
                                  AND si.ProjectId = i.ProjectId AND si.Status NOT IN ('Draft','Cancelled') AND si.IsDeleted = 0))
                            OR (i.VariationSourceType = 1 AND NOT EXISTS (
                                SELECT 1 FROM dbo.ProjectChangeRequests cr
                                WHERE cr.Id = i.ChangeRequestId AND cr.TenantId = i.TenantId
                                  AND cr.ProjectId = i.ProjectId AND cr.Status = 'Approved' AND cr.IsDeleted = 0))))
                        THROW 51893, 'The variation must retain tenant-owned Works contract, contractor, approved BoQ, source, workflow, DMS and QS-DEC-011 lineage.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE i.IsQuantitySurveyGoverned = 1 AND (
                            (d.Id IS NULL AND i.Status <> 'Draft')
                            OR (d.Id IS NOT NULL AND (
                                i.TenantId <> d.TenantId OR i.ProjectId <> d.ProjectId
                                OR i.ClientRequestId <> d.ClientRequestId OR i.ReferenceNumber <> d.ReferenceNumber
                                OR i.PreparedById <> d.PreparedById
                                OR (i.Status <> d.Status AND NOT (
                                    (d.Status = 'Draft' AND i.Status = 'PendingApproval')
                                    OR (d.Status = 'Rejected' AND i.Status IN ('Draft','PendingApproval'))
                                    OR (d.Status = 'PendingApproval' AND i.Status IN ('Approved','Rejected'))))
                                OR (d.Status NOT IN ('Draft','Rejected') AND (
                                    i.ContractId <> d.ContractId OR i.ContractorBusinessPartnerId <> d.ContractorBusinessPartnerId
                                    OR i.ApprovedBoqVersionId <> d.ApprovedBoqVersionId
                                    OR ISNULL(i.SiteInstructionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.SiteInstructionId, '00000000-0000-0000-0000-000000000000')
                                    OR ISNULL(i.ChangeRequestId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ChangeRequestId, '00000000-0000-0000-0000-000000000000')
                                    OR i.ConfigurationProfileId <> d.ConfigurationProfileId OR i.VariationDecisionId <> d.VariationDecisionId
                                    OR i.ApprovalWorkflowDefinitionId <> d.ApprovalWorkflowDefinitionId
                                    OR i.EvidenceMetadataTemplateId <> d.EvidenceMetadataTemplateId
                                    OR i.PolicyHash <> d.PolicyHash OR i.EstimatedAmount <> d.EstimatedAmount))))))
                        THROW 51894, 'Invalid governed variation lifecycle or immutable-lineage mutation.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.IsQuantitySurveyGoverned = 1 AND (
                            i.EstimatedAmount IS NULL OR i.OriginalContractSumSnapshot IS NULL
                            OR (i.Status = 'Draft' AND i.ApprovalStatus <> 'Draft')
                            OR (i.Status = 'PendingApproval' AND (i.ApprovalStatus <> 'Pending' OR i.SubmittedById IS NULL OR i.SubmittedAt IS NULL OR i.WorkflowInstanceId IS NULL))
                            OR (i.Status = 'Approved' AND (i.ApprovalStatus <> 'Approved' OR i.ApprovedById IS NULL OR i.ApprovedAt IS NULL OR i.ApprovedAmount <> i.EstimatedAmount OR i.RevisedContractSumSnapshot <> ROUND(i.OriginalContractSumSnapshot + i.ApprovedAmount, 2)))
                            OR (i.Status = 'Rejected' AND (i.ApprovalStatus <> 'Rejected' OR NULLIF(LTRIM(RTRIM(i.RejectionReason)), '') IS NULL))
                            OR i.Status NOT IN ('Draft','PendingApproval','Approved','Rejected')
                            OR (i.ApprovedById IS NOT NULL AND (i.ApprovedById = i.PreparedById OR i.ApprovedById = i.SubmittedById))))
                        THROW 51895, 'Variation state, workflow outcome, calculated impact or maker-checker evidence is invalid.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE i.IsQuantitySurveyGoverned = 1 AND i.Status <> 'Draft'
                          AND i.EstimatedAmount <> (
                              SELECT ROUND(COALESCE(SUM(l.Amount), 0), 2)
                              FROM dbo.QuantitySurveyVariationValuationLines l
                              WHERE l.VariationOrderId = i.Id AND l.TenantId = i.TenantId AND l.IsDeleted = 0))
                        THROW 51896, 'The frozen variation amount must equal its governed valuation lines.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0508_VariationLines_Governance]
                ON [dbo].[QuantitySurveyVariationValuationLines]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        JOIN dbo.ProjectVariationOrders v ON v.Id = d.VariationOrderId AND v.TenantId = d.TenantId
                        WHERE v.Status NOT IN ('Draft','Rejected'))
                        THROW 51897, 'Variation valuation lines are immutable outside Draft or Rejected state.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.ProjectVariationOrders v
                          ON v.Id = i.VariationOrderId AND v.TenantId = i.TenantId
                         AND v.IsQuantitySurveyGoverned = 1 AND v.Status IN ('Draft','Rejected') AND v.IsDeleted = 0
                        LEFT JOIN dbo.ProjectBoqVersionLines l
                          ON l.Id = i.ProjectBoqVersionLineId AND l.TenantId = i.TenantId
                         AND l.ProjectId = v.ProjectId AND l.ProjectBoqVersionId = v.ApprovedBoqVersionId
                         AND l.LineKey = i.BoqLineKey AND l.ItemType = 'Item' AND l.IsDeleted = 0
                        WHERE i.IsDeleted = 0 AND (v.Id IS NULL OR l.Id IS NULL))
                        THROW 51898, 'Variation lines must use the selected tenant-owned published Approved BoQ.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0508_VariationEvidence_AppendOnly]
                ON [dbo].[QuantitySurveyVariationEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51899, 'Variation evidence is append-only.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.ProjectVariationOrders v
                          ON v.Id = i.VariationOrderId AND v.TenantId = i.TenantId
                         AND v.IsQuantitySurveyGoverned = 1 AND v.Status IN ('Draft','Rejected') AND v.IsDeleted = 0
                        WHERE v.Id IS NULL OR i.ClientRequestId = '00000000-0000-0000-0000-000000000000'
                           OR LEN(i.RequestHash) <> 64 OR LEN(i.ChecksumSha256) <> 64 OR i.FileSize <= 0
                           OR i.FileUploadRecordId = '00000000-0000-0000-0000-000000000000'
                           OR i.CentralDocumentRecordId = '00000000-0000-0000-0000-000000000000'
                           OR i.CentralDocumentVersionId = '00000000-0000-0000-0000-000000000000')
                        THROW 51900, 'Variation evidence must retain clean central DMS and idempotency lineage.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_QS0508_VariationRevisions_AppendOnly]
                ON [dbo].[QuantitySurveyVariationRevisions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51901, 'Variation revisions are append-only.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        LEFT JOIN dbo.ProjectVariationOrders v
                          ON v.Id = i.VariationOrderId AND v.TenantId = i.TenantId
                         AND v.IsQuantitySurveyGoverned = 1 AND v.IsDeleted = 0
                        WHERE v.Id IS NULL OR i.ClientRequestId = '00000000-0000-0000-0000-000000000000'
                           OR i.ActorUserId = '00000000-0000-0000-0000-000000000000'
                           OR LEN(i.RequestHash) <> 64 OR NULLIF(LTRIM(RTRIM(i.Action)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.CorrelationId)), '') IS NULL)
                        THROW 51902, 'Variation revisions must retain tenant, actor, idempotency and correlation lineage.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0508_VariationRevisions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0508_VariationEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0508_VariationLines_Governance];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0508_ProjectVariation_Governance];");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectVariationOrders_BusinessPartners_ContractorBusinessPartnerId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectVariationOrders_ProjectBoqVersions_ApprovedBoqVersionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectVariationOrders_ProjectChangeRequests_ChangeRequestId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectVariationOrders_ProjectSiteInstructions_SiteInstructionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectVariationOrders_QuantitySurveyConfigurationDecisions_VariationDecisionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectVariationOrders_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropTable(
                name: "QuantitySurveyVariationEvidence");

            migrationBuilder.DropTable(
                name: "QuantitySurveyVariationRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyVariationValuationLines");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_ApprovedBoqVersionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_ChangeRequestId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_ConfigurationProfileId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_ContractorBusinessPartnerId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_SiteInstructionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_TenantId_ClientRequestId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_TenantId_ReferenceNumber",
                table: "ProjectVariationOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProjectVariationOrders_VariationDecisionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsVariation_Governance",
                table: "ProjectVariationOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QsVariation_Source",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ApprovalWorkflowDefinitionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedBoqVersionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "BudgetImpactAmount",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ChangeRequestId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ConfigurationProfileId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ContractorBusinessPartnerId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "EvidenceMetadataTemplateId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "ForecastImpactAmount",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "IsQuantitySurveyGoverned",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "LastMutationClientRequestId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "LastMutationRequestHash",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "OriginalContractSumSnapshot",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "PolicyHash",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "PreparedById",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "RevisedContractSumSnapshot",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "SiteInstructionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "SubmittedById",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "UpdateBudgetOnApplication",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "UpdateCertificateOnApplication",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "UpdateContractSumOnApplication",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "UpdateForecastOnApplication",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "VariationDecisionId",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "VariationSourceType",
                table: "ProjectVariationOrders");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "ProjectVariationOrders");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVariationOrders_TenantId",
                table: "ProjectVariationOrders",
                column: "TenantId");
        }
    }
}
