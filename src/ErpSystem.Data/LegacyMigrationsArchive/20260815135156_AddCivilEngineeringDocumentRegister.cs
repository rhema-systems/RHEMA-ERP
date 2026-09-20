using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCivilEngineeringDocumentRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCivilEngineeringDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    SupersedesDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Discipline = table.Column<int>(type: "int", nullable: false),
                    FileCategory = table.Column<int>(type: "int", nullable: false),
                    FileExtension = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ExpectedDocumentReference = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    DocumentReferenceSnapshot = table.Column<string>(type: "varchar(120)", unicode: false, maxLength: 120, nullable: false),
                    DocumentTitleSnapshot = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DmsVersionSnapshot = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetadataTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetadataTemplateCodeSnapshot = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    PolicyHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProjectCivilEngineeringDocuments", x => x.Id);
                    table.CheckConstraint("CK_ProjectCivilEngineeringDocuments_OwnerReviewer", "[OwnerUserId] <> [ReviewerUserId]");
                    table.CheckConstraint("CK_ProjectCivilEngineeringDocuments_Sequence", "[SequenceNumber] BETWEEN 1 AND 999999 AND [RevisionNumber] BETWEEN 0 AND 9999");
                    table.CheckConstraint("CK_ProjectCivilEngineeringDocuments_Status", "[Status] IN (0,1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_CentralDocumentMetadataTemplates_MetadataTemplateId",
                        column: x => x.MetadataTemplateId,
                        principalTable: "CentralDocumentMetadataTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                        column: x => x.CentralDocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                        column: x => x.CentralDocumentVersionId,
                        principalTable: "CentralDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_CivilEngineeringConfigurationDecisions_ConfigurationDecisionId",
                        column: x => x.ConfigurationDecisionId,
                        principalTable: "CivilEngineeringConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_CivilEngineeringConfigurationProfiles_ConfigurationProfileId",
                        column: x => x.ConfigurationProfileId,
                        principalTable: "CivilEngineeringConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_ProjectCivilDesignCases_DesignCaseId",
                        column: x => x.DesignCaseId,
                        principalTable: "ProjectCivilDesignCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_ProjectCivilEngineeringDocuments_SupersedesDocumentId",
                        column: x => x.SupersedesDocumentId,
                        principalTable: "ProjectCivilEngineeringDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_ProjectPackages_ProjectPackageId",
                        column: x => x.ProjectPackageId,
                        principalTable: "ProjectPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocuments_Users_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCivilEngineeringDocumentRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EngineeringDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProjectCivilEngineeringDocumentRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocumentRevisions_ProjectCivilEngineeringDocuments_EngineeringDocumentId",
                        column: x => x.EngineeringDocumentId,
                        principalTable: "ProjectCivilEngineeringDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocumentRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCivilEngineeringDocumentRevisions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocumentRevisions_ActorUserId",
                table: "ProjectCivilEngineeringDocumentRevisions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocumentRevisions_EngineeringDocumentId",
                table: "ProjectCivilEngineeringDocumentRevisions",
                column: "EngineeringDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocumentRevisions_TenantId_CorrelationId",
                table: "ProjectCivilEngineeringDocumentRevisions",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocumentRevisions_TenantId_EngineeringDocumentId_CreatedAt",
                table: "ProjectCivilEngineeringDocumentRevisions",
                columns: new[] { "TenantId", "EngineeringDocumentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_CentralDocumentRecordId",
                table: "ProjectCivilEngineeringDocuments",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_CentralDocumentVersionId",
                table: "ProjectCivilEngineeringDocuments",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_ConfigurationDecisionId",
                table: "ProjectCivilEngineeringDocuments",
                column: "ConfigurationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_ConfigurationProfileId",
                table: "ProjectCivilEngineeringDocuments",
                column: "ConfigurationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_DesignCaseId",
                table: "ProjectCivilEngineeringDocuments",
                column: "DesignCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_MetadataTemplateId",
                table: "ProjectCivilEngineeringDocuments",
                column: "MetadataTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_OwnerUserId",
                table: "ProjectCivilEngineeringDocuments",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_ProjectPackageId",
                table: "ProjectCivilEngineeringDocuments",
                column: "ProjectPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_ReviewedById",
                table: "ProjectCivilEngineeringDocuments",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_ReviewerUserId",
                table: "ProjectCivilEngineeringDocuments",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_SubmittedById",
                table: "ProjectCivilEngineeringDocuments",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_SupersedesDocumentId",
                table: "ProjectCivilEngineeringDocuments",
                column: "SupersedesDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_TenantId_CentralDocumentVersionId",
                table: "ProjectCivilEngineeringDocuments",
                columns: new[] { "TenantId", "CentralDocumentVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_TenantId_ClientRequestId",
                table: "ProjectCivilEngineeringDocuments",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_TenantId_DesignCaseId_Status",
                table: "ProjectCivilEngineeringDocuments",
                columns: new[] { "TenantId", "DesignCaseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_TenantId_DocumentKey_RevisionNumber",
                table: "ProjectCivilEngineeringDocuments",
                columns: new[] { "TenantId", "DocumentKey", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCivilEngineeringDocuments_TenantId_SupersedesDocumentId",
                table: "ProjectCivilEngineeringDocuments",
                columns: new[] { "TenantId", "SupersedesDocumentId" },
                unique: true,
                filter: "[SupersedesDocumentId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.Sql("""
                INSERT INTO CentralDocumentMetadataTemplates
                    (Id, Module, DocumentType, TemplateCode, SourceLabel, RequiredFieldsJson,
                     RelationshipsJson, RetentionRule, AccessProfile, IsActive, PublishedAt,
                     CreatedAt, CreatedBy, IsDeleted, TenantId)
                SELECT NEWID(), 'Civil Engineering', 'Engineering file', 'TDC-CIV-ENGINEERING-FILE',
                       'Civil Engineering design case -> Central DMS',
                       '["Discipline","Revision","Owner","Reviewer","Approval status"]',
                       '["Project","Civil design case","Work package","Superseded version"]',
                       'Minimum seven years or effective CIV-CFG-004 retention, whichever is longer',
                       'Civil Engineering restricted', 1, SYSUTCDATETIME(), SYSUTCDATETIME(),
                       'CIV-0105 migration', 0, tenant.Id
                FROM Tenants tenant
                WHERE tenant.IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM CentralDocumentMetadataTemplates template
                      WHERE template.TenantId = tenant.Id
                        AND template.TemplateCode = 'TDC-CIV-ENGINEERING-FILE'
                        AND template.IsDeleted = 0);
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER TR_ProjectCivilEngineeringDocuments_Lineage
                ON ProjectCivilEngineeringDocuments
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1 FROM inserted value
                        LEFT JOIN ProjectCivilDesignCases designCase
                          ON designCase.Id = value.DesignCaseId
                         AND designCase.TenantId = value.TenantId
                         AND designCase.IsDeleted = 0
                        LEFT JOIN Projects project
                          ON project.Id = designCase.ProjectId
                         AND project.TenantId = value.TenantId
                         AND project.IsDeleted = 0
                        LEFT JOIN ProjectPackages package
                          ON package.Id = value.ProjectPackageId
                         AND package.TenantId = value.TenantId
                         AND package.ProjectId = designCase.ProjectId
                         AND package.IsDeleted = 0
                        LEFT JOIN Users ownerUser
                          ON ownerUser.Id = value.OwnerUserId
                         AND ownerUser.TenantId = value.TenantId
                         AND ownerUser.IsActive = 1
                        LEFT JOIN Users reviewerUser
                          ON reviewerUser.Id = value.ReviewerUserId
                         AND reviewerUser.TenantId = value.TenantId
                         AND reviewerUser.IsActive = 1
                        LEFT JOIN CivilEngineeringConfigurationProfiles profile
                          ON profile.Id = value.ConfigurationProfileId
                         AND profile.TenantId = value.TenantId
                         AND profile.LifecycleStatus = 1
                         AND profile.IsDeleted = 0
                        LEFT JOIN CivilEngineeringConfigurationDecisions decision
                          ON decision.Id = value.ConfigurationDecisionId
                         AND decision.TenantId = value.TenantId
                         AND decision.ProfileId = value.ConfigurationProfileId
                         AND decision.ConfigurationKey = 'CIV-CFG-004'
                         AND decision.Status = 2
                         AND decision.ApprovalStatus = 1
                         AND decision.EvidenceStatus = 2
                         AND decision.IsDeleted = 0
                        LEFT JOIN CentralDocumentMetadataTemplates template
                          ON template.Id = value.MetadataTemplateId
                         AND template.TenantId = value.TenantId
                         AND template.TemplateCode = value.MetadataTemplateCodeSnapshot
                         AND template.IsActive = 1
                         AND template.PublishedAt IS NOT NULL
                         AND template.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords documentRecord
                          ON documentRecord.Id = value.CentralDocumentRecordId
                         AND documentRecord.TenantId = value.TenantId
                         AND documentRecord.MetadataTemplateCode = value.MetadataTemplateCodeSnapshot
                         AND documentRecord.LifecycleStatus = 'Active'
                         AND documentRecord.VersionStatus = 'Published'
                         AND documentRecord.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions documentVersion
                          ON documentVersion.Id = value.CentralDocumentVersionId
                         AND documentVersion.TenantId = value.TenantId
                         AND documentVersion.DocumentRecordId = value.CentralDocumentRecordId
                         AND documentVersion.VersionNumber = documentRecord.CurrentVersion
                         AND documentVersion.Status = 'Published'
                         AND documentVersion.PublishedAt IS NOT NULL
                         AND documentVersion.IsDeleted = 0
                        WHERE designCase.Id IS NULL OR project.Id IS NULL
                           OR (value.ProjectPackageId IS NOT NULL AND package.Id IS NULL)
                           OR ownerUser.Id IS NULL OR reviewerUser.Id IS NULL
                           OR profile.Id IS NULL OR decision.Id IS NULL OR template.Id IS NULL
                           OR documentRecord.Id IS NULL OR documentVersion.Id IS NULL)
                    BEGIN
                        THROW 51930, 'Civil engineering document tenant, project, policy, user, template, and current Published DMS lineage is invalid.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1 FROM inserted value
                        JOIN ProjectCivilEngineeringDocuments prior ON prior.Id = value.SupersedesDocumentId
                        WHERE prior.TenantId <> value.TenantId
                           OR prior.DesignCaseId <> value.DesignCaseId
                           OR prior.DocumentKey <> value.DocumentKey
                           OR prior.CentralDocumentRecordId <> value.CentralDocumentRecordId
                           OR prior.CentralDocumentVersionId = value.CentralDocumentVersionId
                           OR prior.Discipline <> value.Discipline
                           OR prior.SequenceNumber <> value.SequenceNumber
                           OR prior.RevisionNumber >= value.RevisionNumber
                           OR prior.Status <> 4
                           OR prior.IsDeleted = 1)
                    BEGIN
                        THROW 51931, 'Civil engineering document supersession lineage is invalid.', 1;
                    END;

                    IF EXISTS (
                        SELECT 1 FROM inserted value
                        JOIN deleted prior ON prior.Id = value.Id
                        WHERE value.DesignCaseId <> prior.DesignCaseId
                           OR ISNULL(value.ProjectPackageId, '00000000-0000-0000-0000-000000000000') <> ISNULL(prior.ProjectPackageId, '00000000-0000-0000-0000-000000000000')
                           OR value.DocumentKey <> prior.DocumentKey
                           OR value.ClientRequestId <> prior.ClientRequestId
                           OR value.RequestHash <> prior.RequestHash
                           OR ISNULL(value.SupersedesDocumentId, '00000000-0000-0000-0000-000000000000') <> ISNULL(prior.SupersedesDocumentId, '00000000-0000-0000-0000-000000000000')
                           OR value.Discipline <> prior.Discipline OR value.FileCategory <> prior.FileCategory
                           OR value.FileExtension <> prior.FileExtension OR value.SequenceNumber <> prior.SequenceNumber
                           OR value.RevisionNumber <> prior.RevisionNumber
                           OR value.ExpectedDocumentReference <> prior.ExpectedDocumentReference
                           OR value.DocumentReferenceSnapshot <> prior.DocumentReferenceSnapshot
                           OR value.DocumentTitleSnapshot <> prior.DocumentTitleSnapshot
                           OR value.DmsVersionSnapshot <> prior.DmsVersionSnapshot
                           OR value.OwnerUserId <> prior.OwnerUserId OR value.ReviewerUserId <> prior.ReviewerUserId
                           OR value.ConfigurationProfileId <> prior.ConfigurationProfileId
                           OR value.ConfigurationDecisionId <> prior.ConfigurationDecisionId
                           OR value.MetadataTemplateId <> prior.MetadataTemplateId
                           OR value.MetadataTemplateCodeSnapshot <> prior.MetadataTemplateCodeSnapshot
                           OR value.PolicyHash <> prior.PolicyHash
                           OR value.CentralDocumentRecordId <> prior.CentralDocumentRecordId
                           OR value.CentralDocumentVersionId <> prior.CentralDocumentVersionId
                           OR value.CorrelationId <> prior.CorrelationId
                           OR value.TenantId <> prior.TenantId OR value.IsDeleted <> prior.IsDeleted)
                    BEGIN
                        THROW 51932, 'Civil engineering document identity, policy, metadata, and DMS lineage is immutable.', 1;
                    END;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER TR_ProjectCivilEngineeringDocuments_Lifecycle
                ON ProjectCivilEngineeringDocuments
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted prior LEFT JOIN inserted value ON value.Id = prior.Id WHERE value.Id IS NULL)
                        THROW 51933, 'Civil engineering document registrations cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id = value.Id
                        WHERE value.Status <> prior.Status
                          AND NOT ((prior.Status = 0 AND value.Status = 1)
                                OR (prior.Status = 1 AND value.Status IN (2,3))
                                OR (prior.Status = 3 AND value.Status = 1)
                                OR (prior.Status = 2 AND value.Status IN (4,5))))
                        THROW 51934, 'Invalid civil engineering document lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id = value.Id
                        WHERE value.Status = 1 AND prior.Status <> 1
                          AND (value.SubmittedAt IS NULL OR value.SubmittedById <> value.OwnerUserId))
                        THROW 51935, 'Engineering document submission must be performed by its assigned owner.', 1;

                    IF EXISTS (
                        SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id = value.Id
                        WHERE value.Status IN (2,3) AND prior.Status <> value.Status
                          AND (value.ReviewedAt IS NULL OR value.ReviewedById <> value.ReviewerUserId
                               OR value.ReviewedById = value.OwnerUserId OR LEN(LTRIM(RTRIM(ISNULL(value.ReviewReason,'')))) < 5))
                        THROW 51936, 'Engineering document review requires the assigned independent reviewer and a reason.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER TR_ProjectCivilEngineeringDocumentRevisions_Lineage
                ON ProjectCivilEngineeringDocumentRevisions
                AFTER INSERT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted revision
                        LEFT JOIN ProjectCivilEngineeringDocuments document
                          ON document.Id = revision.EngineeringDocumentId
                         AND document.TenantId = revision.TenantId
                        LEFT JOIN Users actor
                          ON actor.Id = revision.ActorUserId
                         AND actor.TenantId = revision.TenantId
                        WHERE document.Id IS NULL OR actor.Id IS NULL)
                        THROW 51937, 'Civil engineering document revision tenant, document, or actor lineage is invalid.', 1;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER TR_ProjectCivilEngineeringDocumentRevisions_AppendOnly
                ON ProjectCivilEngineeringDocumentRevisions
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51938, 'Civil engineering document revision history is append-only.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilEngineeringDocumentRevisions_AppendOnly;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilEngineeringDocumentRevisions_Lineage;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilEngineeringDocuments_Lifecycle;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilEngineeringDocuments_Lineage;");

            migrationBuilder.DropTable(
                name: "ProjectCivilEngineeringDocumentRevisions");

            migrationBuilder.DropTable(
                name: "ProjectCivilEngineeringDocuments");

            migrationBuilder.Sql("""
                DELETE template
                FROM CentralDocumentMetadataTemplates template
                WHERE template.TemplateCode = 'TDC-CIV-ENGINEERING-FILE'
                  AND template.CreatedBy = 'CIV-0105 migration'
                  AND NOT EXISTS (
                      SELECT 1 FROM CentralDocumentRecords record
                      WHERE record.TenantId = template.TenantId
                        AND record.MetadataTemplateCode = template.TemplateCode
                        AND record.IsDeleted = 0);
                """);
        }
    }
}
