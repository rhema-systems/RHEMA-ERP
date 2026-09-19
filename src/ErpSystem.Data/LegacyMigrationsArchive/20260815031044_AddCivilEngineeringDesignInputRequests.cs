using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCivilEngineeringDesignInputRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectRfis_TenantId",
                table: "ProjectRfis");

            migrationBuilder.AddColumn<bool>(
                name: "BlocksCivilDesignReadiness",
                table: "ProjectRfis",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CivilDesignCaseId",
                table: "ProjectRfis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "ProjectRfis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMutationClientRequestId",
                table: "ProjectRfis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMutationRequestHash",
                table: "ProjectRfis",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "ProjectRfis",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                table: "ProjectRfis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedSectionId",
                table: "ProjectRfis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectRfis",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "ProjectCivilDesignInputResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectRfiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseSequence = table.Column<int>(type: "int", nullable: false),
                    ResponseText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProjectCivilDesignInputResponses", x => x.Id);
                    table.CheckConstraint("CK_ProjectCivilDesignInputResponses_Sequence", "[ResponseSequence] > 0");
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignInputResponses_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignInputResponses_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignInputResponses_ProjectRfis_ProjectRfiId",
                        column: x => x.ProjectRfiId,
                        principalTable: "ProjectRfis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignInputResponses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilDesignInputResponses_Users_RespondedByUserId",
                        column: x => x.RespondedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_CivilDesignCaseId",
                table: "ProjectRfis",
                column: "CivilDesignCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_RequestedByUserId",
                table: "ProjectRfis",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_RequestedSectionId",
                table: "ProjectRfis",
                column: "RequestedSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_TenantId_CivilDesignCaseId_Status_ResponseDueDate",
                table: "ProjectRfis",
                columns: new[] { "TenantId", "CivilDesignCaseId", "Status", "ResponseDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_TenantId_ClientRequestId",
                table: "ProjectRfis",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[CivilDesignCaseId] IS NOT NULL AND [ClientRequestId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_TenantId_RequestedSectionId_Status",
                table: "ProjectRfis",
                columns: new[] { "TenantId", "RequestedSectionId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectRfis_CivilDesignInputShape",
                table: "ProjectRfis",
                sql: "([CivilDesignCaseId] IS NULL AND [RequestedSectionId] IS NULL AND [RequestedByUserId] IS NULL AND [ClientRequestId] IS NULL AND [RequestHash] IS NULL) OR ([CivilDesignCaseId] IS NOT NULL AND [RequestedSectionId] IS NOT NULL AND [RequestedByUserId] IS NOT NULL AND [ClientRequestId] IS NOT NULL AND [RequestHash] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignInputResponses_CentralDocumentRecordId",
                table: "ProjectCivilDesignInputResponses",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignInputResponses_CentralDocumentVersionId",
                table: "ProjectCivilDesignInputResponses",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignInputResponses_ProjectRfiId",
                table: "ProjectCivilDesignInputResponses",
                column: "ProjectRfiId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignInputResponses_RespondedByUserId",
                table: "ProjectCivilDesignInputResponses",
                column: "RespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignInputResponses_TenantId_CentralDocumentVersionId",
                table: "ProjectCivilDesignInputResponses",
                columns: new[] { "TenantId", "CentralDocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilDesignInputResponses_TenantId_ProjectRfiId_ResponseSequence",
                table: "ProjectCivilDesignInputResponses",
                columns: new[] { "TenantId", "ProjectRfiId", "ResponseSequence" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectRfis_ProjectCivilDesignCases_CivilDesignCaseId",
                table: "ProjectRfis",
                column: "CivilDesignCaseId",
                principalTable: "ProjectCivilDesignCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectRfis_Sections_RequestedSectionId",
                table: "ProjectRfis",
                column: "RequestedSectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectRfis_Users_RequestedByUserId",
                table: "ProjectRfis",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectRfis_CivilDesignInputLifecycle]
                ON [dbo].[ProjectRfis]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.[Id] = d.[Id]
                        WHERE d.[CivilDesignCaseId] IS NOT NULL AND i.[Id] IS NULL
                    )
                        THROW 51930, 'Civil design-input requests cannot be physically deleted.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProjectCivilDesignCases] c
                            ON c.[Id] = i.[CivilDesignCaseId]
                           AND c.[TenantId] = i.[TenantId]
                           AND c.[ProjectId] = i.[ProjectId]
                           AND c.[IsDeleted] = 0
                        LEFT JOIN [dbo].[Projects] p
                            ON p.[Id] = i.[ProjectId]
                           AND p.[TenantId] = i.[TenantId]
                           AND p.[IsDeleted] = 0
                        LEFT JOIN [dbo].[Sections] s
                            ON s.[Id] = i.[RequestedSectionId]
                           AND s.[TenantId] = i.[TenantId]
                           AND s.[IsActive] = 1
                           AND s.[IsDeleted] = 0
                        LEFT JOIN [dbo].[Departments] dep
                            ON dep.[Id] = s.[DepartmentId]
                           AND dep.[TenantId] = i.[TenantId]
                           AND dep.[IsActive] = 1
                           AND dep.[IsDeleted] = 0
                        LEFT JOIN [dbo].[Users] u
                            ON u.[Id] = i.[RequestedByUserId]
                           AND u.[TenantId] = i.[TenantId]
                           AND u.[IsActive] = 1
                        WHERE i.[CivilDesignCaseId] IS NOT NULL
                          AND
                          (
                              c.[Id] IS NULL OR p.[Id] IS NULL OR s.[Id] IS NULL
                              OR dep.[Id] IS NULL OR u.[Id] IS NULL
                              OR i.[Status] NOT IN ('Submitted','Answered','Closed')
                              OR i.[Priority] NOT IN ('Low','Medium','High','Critical')
                              OR i.[ResponseDueDate] IS NULL
                          )
                    )
                        THROW 51931, 'Civil design-input request tenant, project, section, actor, or controlled-value lineage is invalid.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[CivilDesignCaseId] IS NOT NULL
                          AND d.[Id] IS NULL
                          AND i.[Status] <> 'Submitted'
                    )
                        THROW 51932, 'A Civil design-input request must begin in Submitted status.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (i.[CivilDesignCaseId] IS NOT NULL OR d.[CivilDesignCaseId] IS NOT NULL)
                          AND
                          (
                              d.[CivilDesignCaseId] IS NULL OR i.[CivilDesignCaseId] IS NULL
                              OR i.[TenantId] <> d.[TenantId]
                              OR i.[ProjectId] <> d.[ProjectId]
                              OR i.[CivilDesignCaseId] <> d.[CivilDesignCaseId]
                              OR i.[RequestedSectionId] <> d.[RequestedSectionId]
                              OR i.[RequestedByUserId] <> d.[RequestedByUserId]
                              OR i.[ClientRequestId] <> d.[ClientRequestId]
                              OR i.[RequestHash] <> d.[RequestHash]
                              OR ISNULL(i.[ReferenceNumber], '') <> ISNULL(d.[ReferenceNumber], '')
                              OR i.[Subject] <> d.[Subject]
                              OR i.[Question] <> d.[Question]
                              OR i.[Priority] <> d.[Priority]
                              OR i.[RaisedDate] <> d.[RaisedDate]
                              OR i.[ResponseDueDate] <> d.[ResponseDueDate]
                              OR i.[BlocksCivilDesignReadiness] <> d.[BlocksCivilDesignReadiness]
                              OR i.[IsDeleted] <> d.[IsDeleted]
                          )
                    )
                        THROW 51933, 'Civil design-input request tenant, subject, section, due-date, and readiness lineage is immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[CivilDesignCaseId] IS NOT NULL
                          AND i.[Status] <> d.[Status]
                          AND NOT
                          (
                              (d.[Status] = 'Submitted' AND i.[Status] = 'Answered')
                           OR (d.[Status] = 'Answered' AND i.[Status] IN ('Submitted','Closed'))
                          )
                    )
                        THROW 51934, 'Invalid Civil design-input request lifecycle transition.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilDesignInputResponses_Lineage]
                ON [dbo].[ProjectCivilDesignInputResponses]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProjectRfis] r
                            ON r.[Id] = i.[ProjectRfiId]
                           AND r.[TenantId] = i.[TenantId]
                           AND r.[CivilDesignCaseId] IS NOT NULL
                           AND r.[Status] = 'Answered'
                           AND r.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProjectCivilDesignCases] c
                            ON c.[Id] = r.[CivilDesignCaseId]
                           AND c.[TenantId] = i.[TenantId]
                           AND c.[ProjectId] = r.[ProjectId]
                           AND c.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CivilEngineeringConfigurationDecisions] cd
                            ON cd.[Id] = c.[ConfigurationDecisionId]
                           AND cd.[TenantId] = i.[TenantId]
                           AND cd.[ConfigurationKey] = 'CIV-CFG-003'
                           AND cd.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CentralDocumentMetadataTemplates] mt
                            ON mt.[Id] = TRY_CONVERT(uniqueidentifier, JSON_VALUE(cd.[ValueJson], '$.crossSectionTemplateId'))
                           AND mt.[TenantId] = i.[TenantId]
                           AND mt.[IsActive] = 1
                           AND mt.[PublishedAt] IS NOT NULL
                           AND mt.[IsDeleted] = 0
                        LEFT JOIN [dbo].[Users] u
                            ON u.[Id] = i.[RespondedByUserId]
                           AND u.[TenantId] = i.[TenantId]
                           AND u.[IsActive] = 1
                        LEFT JOIN [dbo].[Employees] e
                            ON e.[Id] = u.[EmployeeId]
                           AND e.[TenantId] = i.[TenantId]
                           AND e.[SectionId] = r.[RequestedSectionId]
                           AND e.[IsActive] = 1
                           AND e.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CentralDocumentRecords] dr
                            ON dr.[Id] = i.[CentralDocumentRecordId]
                           AND dr.[TenantId] = i.[TenantId]
                           AND dr.[LifecycleStatus] = 'Active'
                           AND dr.[VersionStatus] = 'Published'
                           AND dr.[MetadataTemplateCode] = mt.[TemplateCode]
                           AND dr.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CentralDocumentVersions] dv
                            ON dv.[Id] = i.[CentralDocumentVersionId]
                           AND dv.[DocumentRecordId] = i.[CentralDocumentRecordId]
                           AND dv.[TenantId] = i.[TenantId]
                           AND dv.[Status] = 'Published'
                           AND dv.[PublishedAt] IS NOT NULL
                           AND dv.[IsDeleted] = 0
                        WHERE r.[Id] IS NULL OR c.[Id] IS NULL OR cd.[Id] IS NULL OR mt.[Id] IS NULL
                           OR u.[Id] IS NULL OR e.[Id] IS NULL OR dr.[Id] IS NULL OR dv.[Id] IS NULL
                           OR dr.[CurrentVersion] <> dv.[VersionNumber]
                           OR i.[RespondedByUserId] = r.[RequestedByUserId]
                           OR i.[ResponseSequence] < 1
                           OR NULLIF(LTRIM(RTRIM(i.[ResponseText])), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.[CorrelationId])), '') IS NULL
                    )
                        THROW 51935, 'Civil design-input response actor is outside the requested HR section, or its current Published DMS lineage is invalid.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilDesignInputResponses_AppendOnly]
                ON [dbo].[ProjectCivilDesignInputResponses]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    THROW 51936, 'Civil design-input responses are append-only.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilDesignInputResponses_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilDesignInputResponses_Lineage];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectRfis_CivilDesignInputLifecycle];");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectRfis_ProjectCivilDesignCases_CivilDesignCaseId",
                table: "ProjectRfis");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectRfis_Sections_RequestedSectionId",
                table: "ProjectRfis");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectRfis_Users_RequestedByUserId",
                table: "ProjectRfis");

            migrationBuilder.DropTable(
                name: "ProjectCivilDesignInputResponses");

            migrationBuilder.DropIndex(
                name: "IX_ProjectRfis_CivilDesignCaseId",
                table: "ProjectRfis");

            migrationBuilder.DropIndex(
                name: "IX_ProjectRfis_RequestedByUserId",
                table: "ProjectRfis");

            migrationBuilder.DropIndex(
                name: "IX_ProjectRfis_RequestedSectionId",
                table: "ProjectRfis");

            migrationBuilder.DropIndex(
                name: "IX_ProjectRfis_TenantId_CivilDesignCaseId_Status_ResponseDueDate",
                table: "ProjectRfis");

            migrationBuilder.DropIndex(
                name: "IX_ProjectRfis_TenantId_ClientRequestId",
                table: "ProjectRfis");

            migrationBuilder.DropIndex(
                name: "IX_ProjectRfis_TenantId_RequestedSectionId_Status",
                table: "ProjectRfis");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectRfis_CivilDesignInputShape",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "BlocksCivilDesignReadiness",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "CivilDesignCaseId",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "LastMutationClientRequestId",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "LastMutationRequestHash",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "RequestedSectionId",
                table: "ProjectRfis");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectRfis");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRfis_TenantId",
                table: "ProjectRfis",
                column: "TenantId");
        }
    }
}
