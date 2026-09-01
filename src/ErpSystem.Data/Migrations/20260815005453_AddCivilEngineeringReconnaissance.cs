using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCivilEngineeringReconnaissance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCivilReconnaissanceReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ReportNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SiteLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SiteConditions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SiteReconnaissanceTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrossSectionTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProjectCivilReconnaissanceReports", x => x.Id);
                    table.CheckConstraint("CK_ProjectCivilReconnaissanceReports_Status", "[Status] IN ('Draft','Completed')");
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceReports_CentralDocumentMetadataTemplates_CrossSectionTemplateId",
                        column: x => x.CrossSectionTemplateId,
                        principalTable: "CentralDocumentMetadataTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceReports_CentralDocumentMetadataTemplates_SiteReconnaissanceTemplateId",
                        column: x => x.SiteReconnaissanceTemplateId,
                        principalTable: "CentralDocumentMetadataTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceReports_ProjectCivilDesignCases_DesignCaseId",
                        column: x => x.DesignCaseId,
                        principalTable: "ProjectCivilDesignCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceReports_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCivilReconnaissanceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    InformationSourceSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConstraintCategory = table.Column<int>(type: "int", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    ResolutionStatus = table.Column<int>(type: "int", nullable: true),
                    BlocksDesign = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ProjectCivilReconnaissanceItems", x => x.Id);
                    table.CheckConstraint("CK_ProjectCivilReconnaissanceItems_Order", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_ProjectCivilReconnaissanceItems_Shape", "([Kind] = 0 AND [InformationSourceSectionId] IS NULL AND [CentralDocumentRecordId] IS NULL AND [CentralDocumentVersionId] IS NULL AND [ConstraintCategory] IS NOT NULL AND [Severity] IS NOT NULL AND [ResolutionStatus] IS NOT NULL) OR ([Kind] = 1 AND [InformationSourceSectionId] IS NOT NULL AND [CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL AND [ConstraintCategory] IS NULL AND [Severity] IS NULL AND [ResolutionStatus] IS NULL AND [BlocksDesign] = 0) OR ([Kind] = 2 AND [InformationSourceSectionId] IS NULL AND [CentralDocumentRecordId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL AND [ConstraintCategory] IS NULL AND [Severity] IS NULL AND [ResolutionStatus] IS NULL AND [BlocksDesign] = 0)");
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceItems_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceItems_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceItems_ProjectCivilReconnaissanceReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ProjectCivilReconnaissanceReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceItems_Sections_InformationSourceSectionId",
                        column: x => x.InformationSourceSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCivilReconnaissanceRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProjectCivilReconnaissanceRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceRevisions_ProjectCivilReconnaissanceReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ProjectCivilReconnaissanceReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilReconnaissanceRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceItems_CentralDocumentRecordId",
                table: "ProjectCivilReconnaissanceItems",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceItems_CentralDocumentVersionId",
                table: "ProjectCivilReconnaissanceItems",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceItems_InformationSourceSectionId",
                table: "ProjectCivilReconnaissanceItems",
                column: "InformationSourceSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceItems_ReportId",
                table: "ProjectCivilReconnaissanceItems",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceItems_TenantId_CentralDocumentVersionId",
                table: "ProjectCivilReconnaissanceItems",
                columns: new[] { "TenantId", "CentralDocumentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceItems_TenantId_ReportId_DisplayOrder",
                table: "ProjectCivilReconnaissanceItems",
                columns: new[] { "TenantId", "ReportId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceReports_CrossSectionTemplateId",
                table: "ProjectCivilReconnaissanceReports",
                column: "CrossSectionTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceReports_DesignCaseId",
                table: "ProjectCivilReconnaissanceReports",
                column: "DesignCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceReports_SiteReconnaissanceTemplateId",
                table: "ProjectCivilReconnaissanceReports",
                column: "SiteReconnaissanceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceReports_TenantId_ClientRequestId",
                table: "ProjectCivilReconnaissanceReports",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceReports_TenantId_DesignCaseId_ReportNumber",
                table: "ProjectCivilReconnaissanceReports",
                columns: new[] { "TenantId", "DesignCaseId", "ReportNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceReports_TenantId_DesignCaseId_Status_VisitDate",
                table: "ProjectCivilReconnaissanceReports",
                columns: new[] { "TenantId", "DesignCaseId", "Status", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceRevisions_ReportId",
                table: "ProjectCivilReconnaissanceRevisions",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceRevisions_TenantId_CorrelationId",
                table: "ProjectCivilReconnaissanceRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilReconnaissanceRevisions_TenantId_ReportId_CreatedAt",
                table: "ProjectCivilReconnaissanceRevisions",
                columns: new[] { "TenantId", "ReportId", "CreatedAt" });

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilReconnaissanceReports_Lifecycle]
                ON [dbo].[ProjectCivilReconnaissanceReports]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProjectCivilDesignCases] c
                            ON c.[Id] = i.[DesignCaseId]
                           AND c.[TenantId] = i.[TenantId]
                           AND c.[Stage] = 'SceInformationGathering'
                        LEFT JOIN [dbo].[CentralDocumentMetadataTemplates] st
                            ON st.[Id] = i.[SiteReconnaissanceTemplateId]
                           AND st.[TenantId] = i.[TenantId]
                           AND st.[IsActive] = 1
                           AND st.[PublishedAt] IS NOT NULL
                           AND st.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CentralDocumentMetadataTemplates] ct
                            ON ct.[Id] = i.[CrossSectionTemplateId]
                           AND ct.[TenantId] = i.[TenantId]
                           AND ct.[IsActive] = 1
                           AND ct.[PublishedAt] IS NOT NULL
                           AND ct.[IsDeleted] = 0
                        WHERE c.[Id] IS NULL OR st.[Id] IS NULL OR ct.[Id] IS NULL
                           OR NOT EXISTS
                              (SELECT 1 FROM [dbo].[Users] u
                               WHERE u.[Id] = i.[PreparedByUserId]
                                 AND u.[TenantId] = i.[TenantId]
                                 AND u.[IsActive] = 1)
                           OR (i.[CompletedByUserId] IS NOT NULL AND NOT EXISTS
                              (SELECT 1 FROM [dbo].[Users] u
                               WHERE u.[Id] = i.[CompletedByUserId]
                                 AND u.[TenantId] = i.[TenantId]
                                 AND u.[IsActive] = 1))
                    )
                        THROW 51910, 'Civil reconnaissance tenant, design-case, DMS-template, or actor lineage is invalid.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[Status] = 'Completed'
                    )
                        THROW 51911, 'Completed Civil reconnaissance reports are immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[DesignCaseId] <> d.[DesignCaseId]
                           OR i.[TenantId] <> d.[TenantId]
                           OR i.[ClientRequestId] <> d.[ClientRequestId]
                           OR i.[RequestHash] <> d.[RequestHash]
                           OR i.[ReportNumber] <> d.[ReportNumber]
                           OR i.[SiteReconnaissanceTemplateId] <> d.[SiteReconnaissanceTemplateId]
                           OR i.[CrossSectionTemplateId] <> d.[CrossSectionTemplateId]
                           OR i.[PolicyHash] <> d.[PolicyHash]
                           OR i.[PreparedByUserId] <> d.[PreparedByUserId]
                           OR i.[IsDeleted] <> d.[IsDeleted]
                    )
                        THROW 51912, 'Civil reconnaissance subject, request, policy, and ownership lineage is immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (d.[Id] IS NULL AND
                               (i.[Status] <> 'Draft' OR i.[CompletedByUserId] IS NOT NULL OR i.[CompletedAt] IS NOT NULL))
                           OR (d.[Id] IS NOT NULL AND
                               ((i.[Status] <> d.[Status] AND NOT (d.[Status] = 'Draft' AND i.[Status] = 'Completed'))
                                OR (i.[Status] = 'Draft' AND (i.[CompletedByUserId] IS NOT NULL OR i.[CompletedAt] IS NOT NULL))
                                OR (i.[Status] = 'Completed' AND (i.[CompletedByUserId] IS NULL OR i.[CompletedAt] IS NULL))))
                    )
                        THROW 51913, 'Invalid Civil reconnaissance lifecycle transition or completion state.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilReconnaissanceReports_Delete]
                ON [dbo].[ProjectCivilReconnaissanceReports]
                INSTEAD OF DELETE
                AS
                BEGIN
                    THROW 51914, 'Civil reconnaissance reports cannot be physically deleted.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilReconnaissanceItems_Lineage]
                ON [dbo].[ProjectCivilReconnaissanceItems]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE i.[ReportId] <> d.[ReportId] OR i.[TenantId] <> d.[TenantId]
                    )
                        THROW 51916, 'Civil reconnaissance item report and tenant lineage is immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN [dbo].[ProjectCivilReconnaissanceReports] r
                            ON r.[Id] = d.[ReportId] AND r.[TenantId] = d.[TenantId]
                        WHERE r.[Id] IS NULL OR r.[Status] <> 'Draft'
                    )
                        THROW 51917, 'Items on completed Civil reconnaissance reports are immutable.', 1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProjectCivilReconnaissanceReports] r
                            ON r.[Id] = i.[ReportId]
                           AND r.[TenantId] = i.[TenantId]
                           AND r.[Status] = 'Draft'
                        LEFT JOIN [dbo].[Sections] s
                            ON s.[Id] = i.[InformationSourceSectionId]
                           AND s.[TenantId] = i.[TenantId]
                           AND s.[IsActive] = 1
                           AND s.[IsDeleted] = 0
                        LEFT JOIN [dbo].[Departments] dep
                            ON dep.[Id] = s.[DepartmentId]
                           AND dep.[TenantId] = i.[TenantId]
                           AND dep.[IsActive] = 1
                           AND dep.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CentralDocumentRecords] dr
                            ON dr.[Id] = i.[CentralDocumentRecordId]
                           AND dr.[TenantId] = i.[TenantId]
                           AND dr.[LifecycleStatus] = 'Active'
                           AND dr.[VersionStatus] = 'Published'
                           AND dr.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CentralDocumentVersions] dv
                            ON dv.[Id] = i.[CentralDocumentVersionId]
                           AND dv.[DocumentRecordId] = i.[CentralDocumentRecordId]
                           AND dv.[TenantId] = i.[TenantId]
                           AND dv.[Status] = 'Published'
                           AND dv.[PublishedAt] IS NOT NULL
                           AND dv.[IsDeleted] = 0
                        LEFT JOIN [dbo].[CentralDocumentMetadataTemplates] st
                            ON st.[Id] = r.[SiteReconnaissanceTemplateId]
                           AND st.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[CentralDocumentMetadataTemplates] ct
                            ON ct.[Id] = r.[CrossSectionTemplateId]
                           AND ct.[TenantId] = i.[TenantId]
                        WHERE r.[Id] IS NULL
                           OR (i.[Kind] = 1 AND (s.[Id] IS NULL OR dep.[Id] IS NULL))
                           OR (i.[Kind] IN (1, 2) AND
                               (dr.[Id] IS NULL OR dv.[Id] IS NULL OR dr.[CurrentVersion] <> dv.[VersionNumber]))
                           OR (i.[Kind] = 1 AND dr.[MetadataTemplateCode] <> ct.[TemplateCode])
                           OR (i.[Kind] = 2 AND dr.[MetadataTemplateCode] <> st.[TemplateCode])
                    )
                        THROW 51915, 'Civil reconnaissance item tenant, section, or current Published DMS lineage is invalid.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilReconnaissanceRevisions_Lineage]
                ON [dbo].[ProjectCivilReconnaissanceRevisions]
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProjectCivilReconnaissanceReports] r
                            ON r.[Id] = i.[ReportId] AND r.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[Users] u
                            ON u.[Id] = i.[ActorUserId]
                           AND u.[TenantId] = i.[TenantId]
                           AND u.[IsActive] = 1
                        WHERE r.[Id] IS NULL OR u.[Id] IS NULL
                    )
                        THROW 51918, 'Civil reconnaissance revision tenant, report, or actor lineage is invalid.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_ProjectCivilReconnaissanceRevisions_AppendOnly]
                ON [dbo].[ProjectCivilReconnaissanceRevisions]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    THROW 51919, 'Civil reconnaissance revision history is append-only.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilReconnaissanceRevisions_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilReconnaissanceRevisions_Lineage];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilReconnaissanceItems_Lineage];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilReconnaissanceReports_Delete];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectCivilReconnaissanceReports_Lifecycle];");

            migrationBuilder.DropTable(
                name: "ProjectCivilReconnaissanceItems");

            migrationBuilder.DropTable(
                name: "ProjectCivilReconnaissanceRevisions");

            migrationBuilder.DropTable(
                name: "ProjectCivilReconnaissanceReports");
        }
    }
}
