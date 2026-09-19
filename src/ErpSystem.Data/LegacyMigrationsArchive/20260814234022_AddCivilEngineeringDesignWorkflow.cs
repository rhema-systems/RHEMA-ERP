using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCivilEngineeringDesignWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCivilDesignCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Directive = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RequireSiteReconnaissance = table.Column<bool>(type: "bit", nullable: false),
                    RequireVersionedReview = table.Column<bool>(type: "bit", nullable: false),
                    RequireHodApproval = table.Column<bool>(type: "bit", nullable: false),
                    HodUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupervisingCivilEngineerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CivilEngineerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DraftsmanUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentAssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentDueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectCivilDesignCases", x => x.Id);
                    table.CheckConstraint("CK_ProjectCivilDesignCases_Assignment", "[HodUserId] <> [SupervisingCivilEngineerUserId] AND ([CivilEngineerUserId] IS NULL OR ([CivilEngineerUserId] <> [HodUserId] AND [CivilEngineerUserId] <> [SupervisingCivilEngineerUserId])) AND ([DraftsmanUserId] IS NULL OR ([DraftsmanUserId] <> [HodUserId] AND [DraftsmanUserId] <> [SupervisingCivilEngineerUserId] AND ([CivilEngineerUserId] IS NULL OR [DraftsmanUserId] <> [CivilEngineerUserId])))");
                    table.CheckConstraint("CK_ProjectCivilDesignCases_Stage", "[Stage] IN ('DraftDirective','SceInformationGathering','CivilEngineerDesign','SceDesignReview','Drafting','SceDrawingReview','HodFinalReview','Approved','Rejected','Cancelled')");
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignCases_CivilEngineeringConfigurationDecisions_ConfigurationDecisionId",
                        column: x => x.ConfigurationDecisionId,
                        principalTable: "CivilEngineeringConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignCases_CivilEngineeringConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "CivilEngineeringConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignCases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignCases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCivilDesignEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StageSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LinkedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ProjectCivilDesignEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignEvidence_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignEvidence_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignEvidence_ProjectCivilDesignCases_DesignCaseId",
                        column: x => x.DesignCaseId,
                        principalTable: "ProjectCivilDesignCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCivilDesignRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ToStage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_ProjectCivilDesignRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignRevisions_ProjectCivilDesignCases_DesignCaseId",
                        column: x => x.DesignCaseId,
                        principalTable: "ProjectCivilDesignCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignCases_ConfigurationDecisionId",
                table: "ProjectCivilDesignCases",
                column: "ConfigurationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignCases_ConfigurationProfileId",
                table: "ProjectCivilDesignCases",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignCases_ProjectId",
                table: "ProjectCivilDesignCases",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignCases_TenantId_ClientRequestId",
                table: "ProjectCivilDesignCases",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignCases_TenantId_CurrentAssigneeUserId_Stage",
                table: "ProjectCivilDesignCases",
                columns: new[] { "TenantId", "CurrentAssigneeUserId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignCases_TenantId_ProjectId_ReferenceNumber",
                table: "ProjectCivilDesignCases",
                columns: new[] { "TenantId", "ProjectId", "ReferenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignCases_TenantId_ProjectId_Stage",
                table: "ProjectCivilDesignCases",
                columns: new[] { "TenantId", "ProjectId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignEvidence_CentralDocumentRecordId",
                table: "ProjectCivilDesignEvidence",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignEvidence_CentralDocumentVersionId",
                table: "ProjectCivilDesignEvidence",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignEvidence_DesignCaseId",
                table: "ProjectCivilDesignEvidence",
                column: "DesignCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignEvidence_TenantId_DesignCaseId_CentralDocumentVersionId_EvidenceType",
                table: "ProjectCivilDesignEvidence",
                columns: new[] { "TenantId", "DesignCaseId", "CentralDocumentVersionId", "EvidenceType" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignRevisions_DesignCaseId",
                table: "ProjectCivilDesignRevisions",
                column: "DesignCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignRevisions_TenantId_CorrelationId",
                table: "ProjectCivilDesignRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignRevisions_TenantId_DesignCaseId_CreatedAt",
                table: "ProjectCivilDesignRevisions",
                columns: new[] { "TenantId", "DesignCaseId", "CreatedAt" });

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilDesignCases_Lifecycle]
                ON [dbo].[ProjectCivilDesignCases]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[Projects] p
                            ON p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[CivilEngineeringConfigurationProfiles] cp
                            ON cp.[Id] = i.[ConfigurationProfileId] AND cp.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[CivilEngineeringConfigurationDecisions] cd
                            ON cd.[Id] = i.[ConfigurationDecisionId]
                           AND cd.[ProfileId] = i.[ConfigurationProfileId]
                           AND cd.[TenantId] = i.[TenantId]
                           AND cd.[ConfigurationKey] = 'CIV-CFG-003'
                        LEFT JOIN [dbo].[WorkflowDefinitions] wd
                            ON wd.[Id] = i.[ApprovalWorkflowDefinitionId] AND wd.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[WorkflowEntityTypes] wet
                            ON wet.[Id] = wd.[EntityTypeId]
                           AND wet.[TenantId] = i.[TenantId]
                           AND wet.[Code] = 'PROJECT_DESIGN_REVIEW'
                        WHERE p.[Id] IS NULL OR cp.[Id] IS NULL OR cd.[Id] IS NULL
                           OR wd.[Id] IS NULL OR wet.[Id] IS NULL
                           OR NOT EXISTS
                              (SELECT 1 FROM [dbo].[Users] u WHERE u.[Id] = i.[HodUserId] AND u.[TenantId] = i.[TenantId] AND u.[IsActive] = 1)
                           OR NOT EXISTS
                              (SELECT 1 FROM [dbo].[Users] u WHERE u.[Id] = i.[SupervisingCivilEngineerUserId] AND u.[TenantId] = i.[TenantId] AND u.[IsActive] = 1)
                           OR (i.[CivilEngineerUserId] IS NOT NULL AND NOT EXISTS
                              (SELECT 1 FROM [dbo].[Users] u WHERE u.[Id] = i.[CivilEngineerUserId] AND u.[TenantId] = i.[TenantId] AND u.[IsActive] = 1))
                           OR (i.[DraftsmanUserId] IS NOT NULL AND NOT EXISTS
                              (SELECT 1 FROM [dbo].[Users] u WHERE u.[Id] = i.[DraftsmanUserId] AND u.[TenantId] = i.[TenantId] AND u.[IsActive] = 1))
                    )
                        THROW 51901, 'Civil design case tenant, policy, workflow, or actor lineage is invalid.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[Stage] IN ('Approved', 'Rejected', 'Cancelled')
                    )
                        THROW 51902, 'Terminal Civil design cases are immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[ProjectId] <> d.[ProjectId]
                           OR i.[TenantId] <> d.[TenantId]
                           OR i.[ConfigurationProfileId] <> d.[ConfigurationProfileId]
                           OR i.[ConfigurationDecisionId] <> d.[ConfigurationDecisionId]
                           OR i.[ApprovalWorkflowDefinitionId] <> d.[ApprovalWorkflowDefinitionId]
                           OR i.[PolicyHash] <> d.[PolicyHash]
                           OR i.[HodUserId] <> d.[HodUserId]
                           OR i.[SupervisingCivilEngineerUserId] <> d.[SupervisingCivilEngineerUserId]
                           OR i.[RequireSiteReconnaissance] <> d.[RequireSiteReconnaissance]
                           OR i.[RequireVersionedReview] <> d.[RequireVersionedReview]
                           OR i.[RequireHodApproval] <> d.[RequireHodApproval]
                           OR (ISNULL(CONVERT(varchar(36), i.[CivilEngineerUserId]), '') <> ISNULL(CONVERT(varchar(36), d.[CivilEngineerUserId]), '')
                               AND NOT (d.[Stage] = 'SceInformationGathering' AND i.[Stage] = 'CivilEngineerDesign'))
                           OR (ISNULL(CONVERT(varchar(36), i.[DraftsmanUserId]), '') <> ISNULL(CONVERT(varchar(36), d.[DraftsmanUserId]), '')
                               AND NOT (d.[Stage] = 'SceDesignReview' AND i.[Stage] = 'Drafting'))
                    )
                        THROW 51903, 'Civil design case policy, subject, or controlled assignment lineage is immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[Stage] <> d.[Stage]
                          AND NOT
                          (
                              (d.[Stage] = 'DraftDirective' AND i.[Stage] IN ('SceInformationGathering', 'Cancelled'))
                           OR (d.[Stage] = 'SceInformationGathering' AND i.[Stage] = 'CivilEngineerDesign')
                           OR (d.[Stage] = 'CivilEngineerDesign' AND i.[Stage] = 'SceDesignReview')
                           OR (d.[Stage] = 'SceDesignReview' AND i.[Stage] IN ('CivilEngineerDesign', 'Drafting'))
                           OR (d.[Stage] = 'Drafting' AND i.[Stage] = 'SceDrawingReview')
                           OR (d.[Stage] = 'SceDrawingReview' AND i.[Stage] IN ('Drafting', 'HodFinalReview'))
                           OR (d.[Stage] = 'HodFinalReview' AND i.[Stage] IN ('Approved', 'Rejected'))
                          )
                    )
                        THROW 51904, 'Invalid Civil design workflow transition.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilDesignEvidence_Lineage]
                ON [dbo].[ProjectCivilDesignEvidence]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProjectCivilDesignCases] c
                            ON c.[Id] = i.[DesignCaseId] AND c.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[CentralDocumentRecords] r
                            ON r.[Id] = i.[CentralDocumentRecordId] AND r.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[CentralDocumentVersions] v
                            ON v.[Id] = i.[CentralDocumentVersionId]
                           AND v.[DocumentRecordId] = i.[CentralDocumentRecordId]
                           AND v.[TenantId] = i.[TenantId]
                        WHERE c.[Id] IS NULL OR r.[Id] IS NULL OR v.[Id] IS NULL
                    )
                        THROW 51905, 'Civil design evidence tenant or central-DMS lineage is invalid.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilDesignEvidence_AppendOnly]
                ON [dbo].[ProjectCivilDesignEvidence]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    THROW 51906, 'Civil design evidence links are append-only.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilDesignRevisions_Lineage]
                ON [dbo].[ProjectCivilDesignRevisions]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProjectCivilDesignCases] c
                            ON c.[Id] = i.[DesignCaseId] AND c.[TenantId] = i.[TenantId]
                        WHERE c.[Id] IS NULL
                    )
                        THROW 51907, 'Civil design revision tenant lineage is invalid.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilDesignRevisions_AppendOnly]
                ON [dbo].[ProjectCivilDesignRevisions]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    THROW 51908, 'Civil design revision history is append-only.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilDesignRevisions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilDesignRevisions_Lineage];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilDesignEvidence_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilDesignEvidence_Lineage];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilDesignCases_Lifecycle];");

            migrationBuilder.DropTable(
                name: "ProjectCivilDesignEvidence");

            migrationBuilder.DropTable(
                name: "ProjectCivilDesignRevisions");

            migrationBuilder.DropTable(
                name: "ProjectCivilDesignCases");
        }
    }
}
