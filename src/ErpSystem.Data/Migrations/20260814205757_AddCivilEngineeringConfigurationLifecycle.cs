using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class AddCivilEngineeringConfigurationLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CivilEngineeringConfigurationProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProfileKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProfileCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                LifecycleStatus = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                ChangeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IsDefault = table.Column<bool>(type: "bit", nullable: false),
                SupersedesProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_CivilEngineeringConfigurationProfiles", x => x.Id);
                table.CheckConstraint("CK_CivilConfigurationProfiles_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
                table.CheckConstraint("CK_CivilConfigurationProfiles_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.CheckConstraint("CK_CivilConfigurationProfiles_Version", "[Version] > 0");
                table.ForeignKey("FK_CivilEngineeringConfigurationProfiles_CivilEngineeringConfigurationProfiles_SupersedesProfileId", x => x.SupersedesProfileId, "CivilEngineeringConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationProfiles_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CivilEngineeringConfigurationDecisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationKey = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: false),
                SchemaVersion = table.Column<int>(type: "int", nullable: false),
                OwnerGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                EvidenceStatus = table.Column<int>(type: "int", nullable: false),
                ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DecisionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovalWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SourceLineage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                SourceDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                table.PrimaryKey("PK_CivilEngineeringConfigurationDecisions", x => x.Id);
                table.CheckConstraint("CK_CivilConfigurationDecisions_Key", "[ConfigurationKey] LIKE 'CIV-CFG-[0-9][0-9][0-9]'");
                table.CheckConstraint("CK_CivilConfigurationDecisions_Period", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.CheckConstraint("CK_CivilConfigurationDecisions_SchemaVersion", "[SchemaVersion] > 0");
                table.CheckConstraint("CK_CivilConfigurationDecisions_Status", "[Status] IN (0, 1, 2, 3)");
                table.ForeignKey("FK_CivilEngineeringConfigurationDecisions_CivilEngineeringConfigurationDecisions_SourceDecisionId", x => x.SourceDecisionId, "CivilEngineeringConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationDecisions_CivilEngineeringConfigurationProfiles_ProfileId", x => x.ProfileId, "CivilEngineeringConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationDecisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CivilEngineeringConfigurationEvidenceLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EvidenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                table.PrimaryKey("PK_CivilEngineeringConfigurationEvidenceLinks", x => x.Id);
                table.ForeignKey("FK_CivilEngineeringConfigurationEvidenceLinks_CentralDocumentRecords_CentralDocumentRecordId", x => x.CentralDocumentRecordId, "CentralDocumentRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationEvidenceLinks_CentralDocumentVersions_CentralDocumentVersionId", x => x.CentralDocumentVersionId, "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationEvidenceLinks_CivilEngineeringConfigurationDecisions_DecisionId", x => x.DecisionId, "CivilEngineeringConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationEvidenceLinks_CivilEngineeringConfigurationProfiles_ProfileId", x => x.ProfileId, "CivilEngineeringConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationEvidenceLinks_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CivilEngineeringConfigurationRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Result = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                table.PrimaryKey("PK_CivilEngineeringConfigurationRevisions", x => x.Id);
                table.ForeignKey("FK_CivilEngineeringConfigurationRevisions_CivilEngineeringConfigurationDecisions_DecisionId", x => x.DecisionId, "CivilEngineeringConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationRevisions_CivilEngineeringConfigurationProfiles_ProfileId", x => x.ProfileId, "CivilEngineeringConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CivilEngineeringConfigurationRevisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationProfiles_SupersedesProfileId", "CivilEngineeringConfigurationProfiles", "SupersedesProfileId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationProfiles_TenantId_LifecycleStatus_EffectiveFrom", "CivilEngineeringConfigurationProfiles", new[] { "TenantId", "LifecycleStatus", "EffectiveFrom" });
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationProfiles_TenantId_ProfileCode_Version", "CivilEngineeringConfigurationProfiles", new[] { "TenantId", "ProfileCode", "Version" }, unique: true);
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationProfiles_TenantId_ProfileKey_LifecycleStatus", "CivilEngineeringConfigurationProfiles", new[] { "TenantId", "ProfileKey", "LifecycleStatus" });
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationProfiles_TenantId_ProfileKey_Version", "CivilEngineeringConfigurationProfiles", new[] { "TenantId", "ProfileKey", "Version" }, unique: true);
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationDecisions_ProfileId", "CivilEngineeringConfigurationDecisions", "ProfileId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationDecisions_SourceDecisionId", "CivilEngineeringConfigurationDecisions", "SourceDecisionId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationDecisions_TenantId_ConfigurationKey_Status", "CivilEngineeringConfigurationDecisions", new[] { "TenantId", "ConfigurationKey", "Status" });
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationDecisions_TenantId_ProfileId_ConfigurationKey", "CivilEngineeringConfigurationDecisions", new[] { "TenantId", "ProfileId", "ConfigurationKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationEvidenceLinks_CentralDocumentRecordId", "CivilEngineeringConfigurationEvidenceLinks", "CentralDocumentRecordId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationEvidenceLinks_CentralDocumentVersionId", "CivilEngineeringConfigurationEvidenceLinks", "CentralDocumentVersionId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationEvidenceLinks_DecisionId", "CivilEngineeringConfigurationEvidenceLinks", "DecisionId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationEvidenceLinks_ProfileId", "CivilEngineeringConfigurationEvidenceLinks", "ProfileId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationEvidenceLinks_TenantId_DecisionId", "CivilEngineeringConfigurationEvidenceLinks", new[] { "TenantId", "DecisionId" });
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationEvidenceLinks_TenantId_DecisionId_CentralDocumentVersionId", "CivilEngineeringConfigurationEvidenceLinks", new[] { "TenantId", "DecisionId", "CentralDocumentVersionId" }, unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationRevisions_DecisionId", "CivilEngineeringConfigurationRevisions", "DecisionId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationRevisions_ProfileId", "CivilEngineeringConfigurationRevisions", "ProfileId");
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationRevisions_TenantId_CorrelationId", "CivilEngineeringConfigurationRevisions", new[] { "TenantId", "CorrelationId" });
        migrationBuilder.CreateIndex("IX_CivilEngineeringConfigurationRevisions_TenantId_ProfileId_CreatedAt", "CivilEngineeringConfigurationRevisions", new[] { "TenantId", "ProfileId", "CreatedAt" });

        migrationBuilder.Sql("""
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            INSERT INTO [CivilEngineeringConfigurationProfiles]
                ([Id],[ProfileKey],[ProfileCode],[Name],[Version],[LifecycleStatus],[EffectiveFrom],[ChangeSummary],[IsDefault],[CreatedAt],[CreatedBy],[IsDeleted],[TenantId])
            SELECT NEWID(),NEWID(),'TDC-CIVIL-ENGINEERING','TDC Civil Engineering Configuration',1,0,CONVERT(date,@Now),
                   'Initial CIV-0001/CIV-0002 tenant draft. Controlled selections, central-DMS evidence and independent approval are required.',1,@Now,'System',0,t.[Id]
            FROM [Tenants] t
            WHERE t.[IsDeleted]=0
              AND NOT EXISTS (SELECT 1 FROM [CivilEngineeringConfigurationProfiles] p WHERE p.[TenantId]=t.[Id] AND p.[ProfileCode]='TDC-CIVIL-ENGINEERING');

            INSERT INTO [CivilEngineeringConfigurationDecisions]
                ([Id],[ProfileId],[ConfigurationKey],[SchemaVersion],[OwnerGroup],[Status],[ApprovalStatus],[EvidenceStatus],[ValueJson],[SourceLineage],[CreatedAt],[CreatedBy],[IsDeleted],[TenantId])
            SELECT NEWID(),p.[Id],d.[ConfigurationKey],1,d.[OwnerGroup],0,0,0,'{}',CONCAT(d.[ConfigurationKey],' tenant initializer; unapproved draft'),@Now,'System',0,p.[TenantId]
            FROM [CivilEngineeringConfigurationProfiles] p CROSS JOIN (VALUES
                ('CIV-CFG-001','Civil Engineering + Development + ICT'),
                ('CIV-CFG-002','Civil Engineering + Development'),
                ('CIV-CFG-003','Civil Engineering + Architecture + Geodetic + Town Planning'),
                ('CIV-CFG-004','Civil Engineering + ICT'),
                ('CIV-CFG-005','Civil Engineering + Project Management + QS/Finance'),
                ('CIV-CFG-006','Civil Engineering + Development'),
                ('CIV-CFG-007','Civil Engineering + Maintenance + Finance'),
                ('CIV-CFG-008','Civil Engineering + QS + Finance + Procurement'),
                ('CIV-CFG-009','Building Inspectorate + Development + Civil Engineering'),
                ('CIV-CFG-010','Civil Engineering + HR + ICT'),
                ('CIV-CFG-011','Civil Engineering + QA/QC + Consultants'),
                ('CIV-CFG-012','Civil Engineering + Management'),
                ('CIV-CFG-013','Civil Engineering + Records + ICT')
            ) d([ConfigurationKey],[OwnerGroup])
            WHERE p.[ProfileCode]='TDC-CIVIL-ENGINEERING' AND p.[Version]=1 AND p.[IsDeleted]=0
              AND NOT EXISTS (SELECT 1 FROM [CivilEngineeringConfigurationDecisions] x WHERE x.[TenantId]=p.[TenantId] AND x.[ProfileId]=p.[Id] AND x.[ConfigurationKey]=d.[ConfigurationKey]);

            INSERT INTO [CivilEngineeringConfigurationRevisions]
                ([Id],[ProfileId],[Action],[Result],[CorrelationId],[ActorUserId],[ActorName],[ActorRoles],[Reason],[AfterJson],[CreatedAt],[CreatedBy],[IsDeleted],[TenantId])
            SELECT NEWID(),p.[Id],'SeedDraft','Succeeded',CONCAT('civil-migration-',CONVERT(nvarchar(36),p.[TenantId])),
                   '00000000-0000-0000-0000-000000000000','System','System','Idempotent CIV-0001/CIV-0002 tenant draft seed.',
                   '{"profileCode":"TDC-CIVIL-ENGINEERING","version":1,"lifecycleStatus":"draft","configurationCount":13}',@Now,'System',0,p.[TenantId]
            FROM [CivilEngineeringConfigurationProfiles] p
            WHERE p.[ProfileCode]='TDC-CIVIL-ENGINEERING' AND p.[Version]=1 AND p.[IsDeleted]=0
              AND NOT EXISTS (SELECT 1 FROM [CivilEngineeringConfigurationRevisions] x WHERE x.[TenantId]=p.[TenantId] AND x.[ProfileId]=p.[Id] AND x.[Action]='SeedDraft');

            INSERT INTO [Permissions]
                ([Id],[Name],[DisplayName],[Description],[Category],[IsSystemPermission],[CreatedAt],[CreatedBy],[IsDeleted])
            SELECT NEWID(),p.[Name],p.[DisplayName],p.[Description],'Civil Engineering',1,@Now,'System',0
            FROM (VALUES
                ('civil-engineering.configuration.read','Read Civil Engineering configuration','View effective and historical Civil Engineering configuration profiles.'),
                ('civil-engineering.configuration.manage','Manage Civil Engineering configuration','Create and edit draft Civil Engineering configuration profiles and evidence.'),
                ('civil-engineering.configuration.approve','Approve Civil Engineering configuration','Approve decisions and publish or retire Civil Engineering configuration profiles.'),
                ('civil-engineering.audit.read','Read Civil Engineering audit','View immutable Civil Engineering configuration audit history.')
            ) p([Name],[DisplayName],[Description])
            WHERE NOT EXISTS (SELECT 1 FROM [Permissions] existing WHERE existing.[Name]=p.[Name]);

            EXEC('CREATE TRIGGER [TR_CivilEngineeringConfigurationRevisions_AppendOnly]
                  ON [CivilEngineeringConfigurationRevisions]
                  INSTEAD OF UPDATE, DELETE
                  AS BEGIN SET NOCOUNT ON;
                  THROW 51930, ''Civil Engineering configuration history is append-only.'', 1;
                  END');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_CivilEngineeringConfigurationRevisions_AppendOnly]");
        migrationBuilder.Sql("DELETE p FROM [Permissions] p WHERE p.[Name] IN ('civil-engineering.configuration.read','civil-engineering.configuration.manage','civil-engineering.configuration.approve','civil-engineering.audit.read') AND NOT EXISTS (SELECT 1 FROM [RolePermissions] rp WHERE rp.[PermissionId]=p.[Id])");
        migrationBuilder.DropTable("CivilEngineeringConfigurationEvidenceLinks");
        migrationBuilder.DropTable("CivilEngineeringConfigurationRevisions");
        migrationBuilder.DropTable("CivilEngineeringConfigurationDecisions");
        migrationBuilder.DropTable("CivilEngineeringConfigurationProfiles");
    }
}
