using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantitySurveyEscalationFormulaRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuantitySurveyEscalationFormulas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormulaKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractClauseReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FormulaType = table.Column<int>(type: "int", nullable: false),
                    BaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuthorityRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityRoleNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersedesFormulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    SnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_QuantitySurveyEscalationFormulas", x => x.Id);
                    table.CheckConstraint("CK_QsEscalationFormulas_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
                    table.CheckConstraint("CK_QsEscalationFormulas_Approved", "[Status] <> 'Approved' OR ([ApprovalStatus] = 'Approved' AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_QsEscalationFormulas_Period", "[BaseDate] <= [EffectiveFrom] AND ([EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom])");
                    table.CheckConstraint("CK_QsEscalationFormulas_Status", "[Status] IN ('Draft','PendingApproval','Approved','Rejected','Retired')");
                    table.CheckConstraint("CK_QsEscalationFormulas_Type", "[FormulaType] IN (0,1)");
                    table.CheckConstraint("CK_QsEscalationFormulas_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_AspNetRoles_AuthorityRoleId",
                        column: x => x.AuthorityRoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_QuantitySurveyConfigurationDecisions_ConfigurationDecisionId",
                        column: x => x.ConfigurationDecisionId,
                        principalTable: "QuantitySurveyConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_QuantitySurveyConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "QuantitySurveyConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_QuantitySurveyEscalationFormulas_SupersedesFormulaId",
                        column: x => x.SupersedesFormulaId,
                        principalTable: "QuantitySurveyEscalationFormulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulas_WorkflowDefinitions_ApprovalWorkflowDefinitionId",
                        column: x => x.ApprovalWorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyPriceIndexFamilies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Publisher = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyPriceIndexFamilies", x => x.Id);
                    table.CheckConstraint("CK_QsPriceIndexFamilies_Source", "[Source] IN (0,1,2)");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyPriceIndexFamilies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyEscalationFormulaRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_QuantitySurveyEscalationFormulaRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulaRevisions_QuantitySurveyEscalationFormulas_FormulaId",
                        column: x => x.FormulaId,
                        principalTable: "QuantitySurveyEscalationFormulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulaRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuantitySurveyEscalationFormulaComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Component = table.Column<int>(type: "int", nullable: false),
                    Coefficient = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IndexFamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndexSourceSnapshot = table.Column<int>(type: "int", nullable: false),
                    IndexFamilyCodeSnapshot = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    IndexFamilyNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
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
                    table.PrimaryKey("PK_QuantitySurveyEscalationFormulaComponents", x => x.Id);
                    table.CheckConstraint("CK_QsEscalationFormulaComponents_Coefficient", "[Coefficient] >= 0 AND [Coefficient] <= 100");
                    table.CheckConstraint("CK_QsEscalationFormulaComponents_Component", "[Component] IN (0,1,2,3)");
                    table.CheckConstraint("CK_QsEscalationFormulaComponents_Sequence", "[Sequence] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_QsEscalationFormulaComponents_Source", "[IndexSourceSnapshot] IN (0,1,2)");
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulaComponents_QuantitySurveyEscalationFormulas_FormulaId",
                        column: x => x.FormulaId,
                        principalTable: "QuantitySurveyEscalationFormulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulaComponents_QuantitySurveyPriceIndexFamilies_IndexFamilyId",
                        column: x => x.IndexFamilyId,
                        principalTable: "QuantitySurveyPriceIndexFamilies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantitySurveyEscalationFormulaComponents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaComponents_FormulaId",
                table: "QuantitySurveyEscalationFormulaComponents",
                column: "FormulaId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaComponents_IndexFamilyId",
                table: "QuantitySurveyEscalationFormulaComponents",
                column: "IndexFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaComponents_TenantId_FormulaId_Component",
                table: "QuantitySurveyEscalationFormulaComponents",
                columns: new[] { "TenantId", "FormulaId", "Component" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaComponents_TenantId_FormulaId_Sequence",
                table: "QuantitySurveyEscalationFormulaComponents",
                columns: new[] { "TenantId", "FormulaId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaComponents_TenantId_IndexFamilyId",
                table: "QuantitySurveyEscalationFormulaComponents",
                columns: new[] { "TenantId", "IndexFamilyId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaRevisions_FormulaId",
                table: "QuantitySurveyEscalationFormulaRevisions",
                column: "FormulaId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaRevisions_TenantId_CorrelationId",
                table: "QuantitySurveyEscalationFormulaRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulaRevisions_TenantId_FormulaId_CreatedAt",
                table: "QuantitySurveyEscalationFormulaRevisions",
                columns: new[] { "TenantId", "FormulaId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_ApprovalWorkflowDefinitionId",
                table: "QuantitySurveyEscalationFormulas",
                column: "ApprovalWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_AuthorityRoleId",
                table: "QuantitySurveyEscalationFormulas",
                column: "AuthorityRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_CentralDocumentRecordId",
                table: "QuantitySurveyEscalationFormulas",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_CentralDocumentVersionId",
                table: "QuantitySurveyEscalationFormulas",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_ConfigurationDecisionId",
                table: "QuantitySurveyEscalationFormulas",
                column: "ConfigurationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_ConfigurationProfileId",
                table: "QuantitySurveyEscalationFormulas",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_ContractId",
                table: "QuantitySurveyEscalationFormulas",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_ProjectId",
                table: "QuantitySurveyEscalationFormulas",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_SupersedesFormulaId",
                table: "QuantitySurveyEscalationFormulas",
                column: "SupersedesFormulaId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_TenantId_ClientRequestId",
                table: "QuantitySurveyEscalationFormulas",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_TenantId_ConfigurationDecisionId_Status",
                table: "QuantitySurveyEscalationFormulas",
                columns: new[] { "TenantId", "ConfigurationDecisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_TenantId_FormulaKey",
                table: "QuantitySurveyEscalationFormulas",
                columns: new[] { "TenantId", "FormulaKey" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN ('Draft','PendingApproval')");

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_TenantId_FormulaKey_Version",
                table: "QuantitySurveyEscalationFormulas",
                columns: new[] { "TenantId", "FormulaKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_TenantId_ProjectId_ContractId_Code_Version",
                table: "QuantitySurveyEscalationFormulas",
                columns: new[] { "TenantId", "ProjectId", "ContractId", "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyEscalationFormulas_TenantId_ProjectId_ContractId_Status_EffectiveFrom",
                table: "QuantitySurveyEscalationFormulas",
                columns: new[] { "TenantId", "ProjectId", "ContractId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyPriceIndexFamilies_TenantId_Code",
                table: "QuantitySurveyPriceIndexFamilies",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuantitySurveyPriceIndexFamilies_TenantId_Source_IsActive",
                table: "QuantitySurveyPriceIndexFamilies",
                columns: new[] { "TenantId", "Source", "IsActive" });

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_QsEscalationFormulas_TenantAndLineageGuard]
                ON [QuantitySurveyEscalationFormulas]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[Id] IS NULL AND i.[Status] <> 'Draft')
                        THROW 51020, 'A price-adjustment formula must be created as Draft.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (d.[Status] = 'Draft' AND i.[Status] NOT IN ('Draft','PendingApproval','Rejected'))
                           OR (d.[Status] = 'Rejected' AND i.[Status] NOT IN ('Draft','PendingApproval','Rejected'))
                           OR (d.[Status] = 'PendingApproval' AND i.[Status] NOT IN ('PendingApproval','Approved','Rejected'))
                           OR (d.[Status] = 'Approved' AND i.[Status] NOT IN ('Approved','Retired'))
                           OR d.[Status] = 'Retired')
                        THROW 51021, 'Invalid price-adjustment formula lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE (i.[Status] = 'Draft' AND i.[ApprovalStatus] <> 'Draft')
                           OR (i.[Status] = 'PendingApproval' AND i.[ApprovalStatus] <> 'Pending')
                           OR (i.[Status] = 'Approved' AND i.[ApprovalStatus] <> 'Approved')
                           OR (i.[Status] = 'Rejected' AND i.[ApprovalStatus] <> 'Rejected')
                           OR (i.[Status] = 'Retired' AND i.[ApprovalStatus] <> 'Approved'))
                        THROW 51021, 'Formula lifecycle and approval statuses are inconsistent.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[Status] IN ('Draft','PendingApproval','Approved')
                          AND (
                            NOT EXISTS (
                                SELECT 1 FROM [Projects] p
                                WHERE p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId]
                                  AND p.[IsDeleted] = 0)
                            OR NOT EXISTS (
                                SELECT 1 FROM [Contracts] c
                                WHERE c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId]
                                  AND c.[IsDeleted] = 0 AND UPPER(c.[ContractType]) = 'WORKS')
                            OR NOT EXISTS (
                                SELECT 1 FROM [Projects] p
                                WHERE p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId]
                                  AND p.[IsDeleted] = 0
                                  AND (p.[ContractId] = i.[ContractId] OR EXISTS (
                                      SELECT 1 FROM [ProjectPackages] pp
                                      WHERE pp.[TenantId] = i.[TenantId]
                                        AND pp.[ProjectId] = i.[ProjectId]
                                        AND pp.[ContractId] = i.[ContractId]
                                        AND pp.[IsDeleted] = 0)))
                            OR NOT EXISTS (
                                SELECT 1 FROM [QuantitySurveyConfigurationProfiles] p
                                WHERE p.[Id] = i.[ConfigurationProfileId]
                                  AND p.[TenantId] = i.[TenantId]
                                  AND p.[LifecycleStatus] = 1
                                  AND p.[IsDeleted] = 0
                                  AND p.[EffectiveFrom] <= i.[EffectiveFrom]
                                  AND (p.[EffectiveTo] IS NULL OR p.[EffectiveTo] >= COALESCE(i.[EffectiveTo], i.[EffectiveFrom])))
                            OR NOT EXISTS (
                                SELECT 1
                                FROM [QuantitySurveyConfigurationDecisions] d6
                                WHERE d6.[Id] = i.[ConfigurationDecisionId]
                                  AND d6.[ProfileId] = i.[ConfigurationProfileId]
                                  AND d6.[TenantId] = i.[TenantId]
                                  AND d6.[DecisionKey] = 'QS-DEC-006'
                                  AND d6.[Status] = 2 AND d6.[ApprovalStatus] = 1 AND d6.[EvidenceStatus] = 2
                                  AND d6.[IsDeleted] = 0
                                  AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(d6.[ValueJson], '$.approvalWorkflowDefinitionId')) = i.[ApprovalWorkflowDefinitionId]
                                  AND ((LOWER(JSON_VALUE(d6.[ValueJson], '$.formula')) = 'fixedcoefficientindexratio' AND i.[FormulaType] = 0)
                                    OR (LOWER(JSON_VALUE(d6.[ValueJson], '$.formula')) = 'contractdefinedformula' AND i.[FormulaType] = 1)))
                            OR NOT EXISTS (
                                SELECT 1
                                FROM [QuantitySurveyConfigurationDecisions] d1
                                CROSS APPLY OPENJSON(JSON_QUERY(d1.[ValueJson], '$.approverRoleIds')) roles
                                WHERE d1.[ProfileId] = i.[ConfigurationProfileId]
                                  AND d1.[TenantId] = i.[TenantId]
                                  AND d1.[DecisionKey] = 'QS-DEC-001'
                                  AND d1.[Status] = 2 AND d1.[ApprovalStatus] = 1 AND d1.[EvidenceStatus] = 2
                                  AND d1.[IsDeleted] = 0
                                  AND TRY_CONVERT(uniqueidentifier, roles.[value]) = i.[AuthorityRoleId])
                            OR NOT EXISTS (
                                SELECT 1
                                FROM [AspNetRoles] r
                                WHERE r.[Id] = i.[AuthorityRoleId] AND r.[Name] = i.[AuthorityRoleNameSnapshot])
                            OR NOT EXISTS (
                                SELECT 1
                                FROM [WorkflowDefinitions] wd
                                JOIN [WorkflowEntityTypes] wet ON wet.[Id] = wd.[EntityTypeId]
                                WHERE wd.[Id] = i.[ApprovalWorkflowDefinitionId]
                                  AND wd.[TenantId] = i.[TenantId]
                                  AND wd.[LifecycleStatus] = 1 AND wd.[IsActive] = 1 AND wd.[IsDeleted] = 0
                                  AND wet.[TenantId] = i.[TenantId] AND wet.[Code] = 'QS_ESCALATION'
                                  AND wet.[IsActive] = 1 AND wet.[IsDeleted] = 0)
                            OR NOT EXISTS (
                                SELECT 1
                                FROM [CentralDocumentRecords] dr
                                JOIN [CentralDocumentVersions] dv
                                  ON dv.[Id] = i.[CentralDocumentVersionId]
                                 AND dv.[DocumentRecordId] = dr.[Id]
                                 AND dv.[TenantId] = dr.[TenantId]
                                WHERE dr.[Id] = i.[CentralDocumentRecordId]
                                  AND dr.[TenantId] = i.[TenantId]
                                  AND dr.[SourceRecordId] = i.[ContractId]
                                  AND dr.[LifecycleStatus] = 'Active'
                                  AND dr.[VersionStatus] = 'Published'
                                  AND dr.[CurrentVersion] = dv.[VersionNumber]
                                  AND dr.[IsDeleted] = 0
                                  AND dv.[Status] = 'Published' AND dv.[PublishedAt] IS NOT NULL
                                  AND dv.[IsDeleted] = 0)
                            OR (i.[SupersedesFormulaId] IS NOT NULL AND NOT EXISTS (
                                SELECT 1 FROM [QuantitySurveyEscalationFormulas] s
                                WHERE s.[Id] = i.[SupersedesFormulaId]
                                  AND s.[TenantId] = i.[TenantId]
                                  AND s.[FormulaKey] = i.[FormulaKey]
                                  AND s.[ProjectId] = i.[ProjectId]
                                  AND s.[ContractId] = i.[ContractId]
                                  AND s.[Code] = i.[Code]
                                  AND s.[Version] < i.[Version]
                                  AND s.[Status] = 'Approved'
                                  AND s.[IsDeleted] = 0))
                            OR (i.[WorkflowInstanceId] IS NOT NULL AND NOT EXISTS (
                                SELECT 1 FROM [WorkflowInstances] wi
                                WHERE wi.[Id] = i.[WorkflowInstanceId]
                                  AND wi.[TenantId] = i.[TenantId]
                                  AND wi.[WorkflowDefinitionId] = i.[ApprovalWorkflowDefinitionId]
                                  AND wi.[EntityId] = i.[Id]
                                  AND wi.[IsDeleted] = 0))))
                        THROW 51022, 'Invalid tenant, project, Works contract, policy, authority, workflow, DMS, or revision lineage.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[Status] = 'Approved'
                          AND NOT EXISTS (
                              SELECT 1 FROM [WorkflowInstances] wi
                              WHERE wi.[Id] = i.[WorkflowInstanceId]
                                AND wi.[TenantId] = i.[TenantId]
                                AND wi.[WorkflowDefinitionId] = i.[ApprovalWorkflowDefinitionId]
                                AND wi.[EntityId] = i.[Id]
                                AND wi.[Status] = 2
                                AND wi.[IsDeleted] = 0))
                        THROW 51022, 'An Approved formula requires a completed matching workflow instance.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.[Status] IN ('PendingApproval','Approved')
                          AND (
                            (SELECT COUNT_BIG(*) FROM [QuantitySurveyEscalationFormulaComponents] c
                             WHERE c.[FormulaId] = i.[Id] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0) <> 4
                            OR (SELECT COUNT_BIG(DISTINCT c.[Component]) FROM [QuantitySurveyEscalationFormulaComponents] c
                                WHERE c.[FormulaId] = i.[Id] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0) <> 4
                            OR COALESCE((SELECT SUM(c.[Coefficient]) FROM [QuantitySurveyEscalationFormulaComponents] c
                                         WHERE c.[FormulaId] = i.[Id] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0), 0) <> 100
                            OR EXISTS (
                                SELECT 1
                                FROM [QuantitySurveyEscalationFormulaComponents] c
                                LEFT JOIN [QuantitySurveyPriceIndexFamilies] f ON f.[Id] = c.[IndexFamilyId]
                                LEFT JOIN [QuantitySurveyConfigurationDecisions] d6 ON d6.[Id] = i.[ConfigurationDecisionId]
                                WHERE c.[FormulaId] = i.[Id] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0
                                  AND (f.[Id] IS NULL OR f.[TenantId] <> i.[TenantId] OR f.[IsDeleted] = 1 OR f.[IsActive] = 0
                                    OR f.[Source] <> c.[IndexSourceSnapshot]
                                    OR f.[Code] <> c.[IndexFamilyCodeSnapshot]
                                    OR f.[Name] <> c.[IndexFamilyNameSnapshot]
                                    OR d6.[Id] IS NULL
                                    OR c.[Coefficient] <> CASE c.[Component]
                                        WHEN 0 THEN TRY_CONVERT(decimal(9,4), JSON_VALUE(d6.[ValueJson], '$.materialCoefficient'))
                                        WHEN 1 THEN TRY_CONVERT(decimal(9,4), JSON_VALUE(d6.[ValueJson], '$.labourCoefficient'))
                                        WHEN 2 THEN TRY_CONVERT(decimal(9,4), JSON_VALUE(d6.[ValueJson], '$.plantCoefficient'))
                                        WHEN 3 THEN TRY_CONVERT(decimal(9,4), JSON_VALUE(d6.[ValueJson], '$.otherCoefficient')) END
                                    OR NOT EXISTS (
                                        SELECT 1
                                        FROM OPENJSON(JSON_QUERY(d6.[ValueJson], '$.indexSources')) allowed
                                        WHERE LOWER(allowed.[value]) = CASE f.[Source]
                                            WHEN 0 THEN 'gsspbci'
                                            WHEN 1 THEN 'roadsinfrastructure'
                                            WHEN 2 THEN 'controlledmanualimport' END)))))
                        THROW 51023, 'Pending or Approved formulas require exactly four policy-matching controlled index components totaling 100 percent.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [QuantitySurveyEscalationFormulas] x
                          ON x.[TenantId] = i.[TenantId]
                         AND x.[ProjectId] = i.[ProjectId]
                         AND x.[ContractId] = i.[ContractId]
                         AND x.[Code] = i.[Code]
                         AND x.[Id] <> i.[Id]
                         AND x.[Status] = 'Approved' AND x.[IsDeleted] = 0
                         AND x.[FormulaKey] <> i.[FormulaKey]
                         AND x.[EffectiveFrom] <= COALESCE(i.[EffectiveTo], CONVERT(datetime2, '9999-12-31'))
                         AND COALESCE(x.[EffectiveTo], CONVERT(datetime2, '9999-12-31')) >= i.[EffectiveFrom]
                        WHERE i.[Status] = 'Approved')
                        THROW 51024, 'Approved formula periods cannot overlap for the same project, contract, and code.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_QsEscalationFormulas_ApprovedImmutable]
                ON [QuantitySurveyEscalationFormulas]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE d.[Status] = 'Retired'
                           OR (d.[Status] = 'Approved' AND i.[Id] IS NULL)
                           OR (d.[Status] = 'Approved' AND i.[Status] NOT IN ('Approved','Retired'))
                           OR (d.[Status] = 'Approved' AND EXISTS (
                               SELECT d.[FormulaKey],d.[Code],d.[Name],d.[Version],d.[ProjectId],d.[ContractId],
                                      d.[ContractClauseReference],d.[FormulaType],d.[BaseDate],d.[EffectiveFrom],d.[EffectiveTo],
                                      d.[AuthorityRoleId],d.[AuthorityRoleNameSnapshot],d.[ConfigurationProfileId],
                                      d.[ConfigurationDecisionId],d.[ApprovalWorkflowDefinitionId],d.[WorkflowInstanceId],
                                      d.[CentralDocumentRecordId],d.[CentralDocumentVersionId],d.[SupersedesFormulaId],
                                      d.[ClientRequestId],d.[RequestHash],d.[SnapshotHash],d.[PreparedById],d.[PreparedAt],
                                      d.[SubmittedById],d.[SubmittedAt],d.[ApprovedById],d.[ApprovedAt],d.[IsDeleted],d.[TenantId]
                               EXCEPT
                               SELECT i.[FormulaKey],i.[Code],i.[Name],i.[Version],i.[ProjectId],i.[ContractId],
                                      i.[ContractClauseReference],i.[FormulaType],i.[BaseDate],i.[EffectiveFrom],i.[EffectiveTo],
                                      i.[AuthorityRoleId],i.[AuthorityRoleNameSnapshot],i.[ConfigurationProfileId],
                                      i.[ConfigurationDecisionId],i.[ApprovalWorkflowDefinitionId],i.[WorkflowInstanceId],
                                      i.[CentralDocumentRecordId],i.[CentralDocumentVersionId],i.[SupersedesFormulaId],
                                      i.[ClientRequestId],i.[RequestHash],i.[SnapshotHash],i.[PreparedById],i.[PreparedAt],
                                      i.[SubmittedById],i.[SubmittedAt],i.[ApprovedById],i.[ApprovedAt],i.[IsDeleted],i.[TenantId])))
                        THROW 51025, 'Approved formula inputs are immutable; retire and create a governed revision.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_QsEscalationFormulaComponents_Guard]
                ON [QuantitySurveyEscalationFormulaComponents]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM [QuantitySurveyEscalationFormulas] f
                            JOIN [QuantitySurveyPriceIndexFamilies] p ON p.[Id] = i.[IndexFamilyId]
                            WHERE f.[Id] = i.[FormulaId]
                              AND f.[TenantId] = i.[TenantId]
                              AND f.[Status] IN ('Draft','Rejected')
                              AND f.[IsDeleted] = 0
                              AND p.[TenantId] = i.[TenantId]
                              AND p.[Source] = i.[IndexSourceSnapshot]
                              AND p.[Code] = i.[IndexFamilyCodeSnapshot]
                              AND p.[Name] = i.[IndexFamilyNameSnapshot]
                              AND p.[IsActive] = 1 AND p.[IsDeleted] = 0))
                        THROW 51026, 'Formula components require an editable same-tenant formula and an active matching index-family snapshot.', 1;

                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        WHERE NOT EXISTS (
                            SELECT 1 FROM [QuantitySurveyEscalationFormulas] f
                            WHERE f.[Id] = d.[FormulaId] AND f.[TenantId] = d.[TenantId]
                              AND f.[Status] IN ('Draft','Rejected') AND f.[IsDeleted] = 0))
                        THROW 51026, 'Components of submitted, Approved, or Retired formulas are immutable.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_QsPriceIndexFamilies_InUseGuard]
                ON [QuantitySurveyPriceIndexFamilies]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE (i.[Id] IS NULL OR i.[IsDeleted] = 1 OR i.[IsActive] = 0)
                          AND EXISTS (
                              SELECT 1
                              FROM [QuantitySurveyEscalationFormulaComponents] c
                              JOIN [QuantitySurveyEscalationFormulas] f ON f.[Id] = c.[FormulaId]
                              WHERE c.[IndexFamilyId] = d.[Id] AND c.[TenantId] = d.[TenantId]
                                AND c.[IsDeleted] = 0 AND f.[TenantId] = d.[TenantId]
                                AND f.[Status] <> 'Retired' AND f.[IsDeleted] = 0))
                        THROW 51027, 'An index family used by a current formula cannot be deleted or deactivated.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [TR_QsEscalationFormulaRevisions_AppendOnly]
                ON [QuantitySurveyEscalationFormulaRevisions]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51028, 'Formula revision history is append-only.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        WHERE NOT EXISTS (
                            SELECT 1 FROM [QuantitySurveyEscalationFormulas] f
                            WHERE f.[Id] = i.[FormulaId] AND f.[TenantId] = i.[TenantId]))
                        THROW 51029, 'Formula revision history must match the formula tenant.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationFormulaRevisions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsPriceIndexFamilies_InUseGuard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationFormulaComponents_Guard];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationFormulas_ApprovedImmutable];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationFormulas_TenantAndLineageGuard];");

            migrationBuilder.DropTable(
                name: "QuantitySurveyEscalationFormulaComponents");

            migrationBuilder.DropTable(
                name: "QuantitySurveyEscalationFormulaRevisions");

            migrationBuilder.DropTable(
                name: "QuantitySurveyPriceIndexFamilies");

            migrationBuilder.DropTable(
                name: "QuantitySurveyEscalationFormulas");
        }
    }
}
