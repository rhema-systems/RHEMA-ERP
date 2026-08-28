using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260807202000_AddQuantitySurveyConfigurationRegister")]
public partial class AddQuantitySurveyConfigurationRegister : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QuantitySurveyConfigurationProfiles",
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
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false), UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false), DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true), DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyConfigurationProfiles", x => x.Id);
                table.CheckConstraint("CK_QsConfigurationProfiles_Version", "[Version] > 0");
                table.CheckConstraint("CK_QsConfigurationProfiles_Period", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.CheckConstraint("CK_QsConfigurationProfiles_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
                table.ForeignKey("FK_QuantitySurveyConfigurationProfiles_QuantitySurveyConfigurationProfiles_SupersedesProfileId", x => x.SupersedesProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationProfiles_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyConfigurationDecisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false), ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DecisionKey = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false), SchemaVersion = table.Column<int>(type: "int", nullable: false),
                OwnerGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false), Status = table.Column<int>(type: "int", nullable: false),
                ApprovalStatus = table.Column<int>(type: "int", nullable: false), EvidenceStatus = table.Column<int>(type: "int", nullable: false), ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DecisionDate = table.Column<DateTime>(type: "datetime2", nullable: true), EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true), EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true), ApprovalWorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true), SourceLineage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true), SourceDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true), Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false), UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true), CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), IsDeleted = table.Column<bool>(type: "bit", nullable: false), DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true), DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyConfigurationDecisions", x => x.Id);
                table.CheckConstraint("CK_QsConfigurationDecisions_Key", "[DecisionKey] LIKE 'QS-DEC-[0-9][0-9][0-9]'"); table.CheckConstraint("CK_QsConfigurationDecisions_SchemaVersion", "[SchemaVersion] > 0"); table.CheckConstraint("CK_QsConfigurationDecisions_Period", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"); table.CheckConstraint("CK_QsConfigurationDecisions_Status", "[Status] IN (0, 1, 2, 3)");
                table.ForeignKey("FK_QuantitySurveyConfigurationDecisions_QuantitySurveyConfigurationDecisions_SourceDecisionId", x => x.SourceDecisionId, "QuantitySurveyConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationDecisions_QuantitySurveyConfigurationProfiles_ProfileId", x => x.ProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationDecisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyConfigurationEvidenceLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false), ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true), CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true), EvidenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false), ExternalReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true), Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true), LinkedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false), LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false), UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true), CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), IsDeleted = table.Column<bool>(type: "bit", nullable: false), DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true), DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyConfigurationEvidenceLinks", x => x.Id);
                table.ForeignKey("FK_QuantitySurveyConfigurationEvidenceLinks_CentralDocumentRecords_CentralDocumentRecordId", x => x.CentralDocumentRecordId, "CentralDocumentRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationEvidenceLinks_CentralDocumentVersions_CentralDocumentVersionId", x => x.CentralDocumentVersionId, "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationEvidenceLinks_QuantitySurveyConfigurationDecisions_DecisionId", x => x.DecisionId, "QuantitySurveyConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationEvidenceLinks_QuantitySurveyConfigurationProfiles_ProfileId", x => x.ProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationEvidenceLinks_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyConfigurationRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false), ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true), Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false), Result = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false), CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false), ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false), ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false), ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true), Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true), BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true), AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false), UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true), CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true), IsDeleted = table.Column<bool>(type: "bit", nullable: false), DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true), DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true), TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyConfigurationRevisions", x => x.Id);
                table.ForeignKey("FK_QuantitySurveyConfigurationRevisions_QuantitySurveyConfigurationDecisions_DecisionId", x => x.DecisionId, "QuantitySurveyConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationRevisions_QuantitySurveyConfigurationProfiles_ProfileId", x => x.ProfileId, "QuantitySurveyConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyConfigurationRevisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_QsConfigProfiles_Supersedes", "QuantitySurveyConfigurationProfiles", "SupersedesProfileId");
        migrationBuilder.CreateIndex("IX_QsConfigProfiles_Tenant_Status_Effective", "QuantitySurveyConfigurationProfiles", new[] { "TenantId", "LifecycleStatus", "EffectiveFrom" });
        migrationBuilder.CreateIndex("UX_QsConfigProfiles_Tenant_Code_Version", "QuantitySurveyConfigurationProfiles", new[] { "TenantId", "ProfileCode", "Version" }, unique: true);
        migrationBuilder.CreateIndex("UX_QsConfigProfiles_Tenant_Key_Version", "QuantitySurveyConfigurationProfiles", new[] { "TenantId", "ProfileKey", "Version" }, unique: true);
        migrationBuilder.CreateIndex("IX_QsConfigProfiles_Tenant_Key_Lifecycle", "QuantitySurveyConfigurationProfiles", new[] { "TenantId", "ProfileKey", "LifecycleStatus" });
        migrationBuilder.CreateIndex("IX_QsConfigDecisions_Profile", "QuantitySurveyConfigurationDecisions", "ProfileId"); migrationBuilder.CreateIndex("IX_QsConfigDecisions_Source", "QuantitySurveyConfigurationDecisions", "SourceDecisionId");
        migrationBuilder.CreateIndex("UX_QsConfigDecisions_Tenant_Profile_Key", "QuantitySurveyConfigurationDecisions", new[] { "TenantId", "ProfileId", "DecisionKey" }, unique: true); migrationBuilder.CreateIndex("IX_QsConfigDecisions_Tenant_Key_Status", "QuantitySurveyConfigurationDecisions", new[] { "TenantId", "DecisionKey", "Status" });
        migrationBuilder.CreateIndex("IX_QsConfigEvidence_Profile", "QuantitySurveyConfigurationEvidenceLinks", "ProfileId"); migrationBuilder.CreateIndex("IX_QsConfigEvidence_Decision", "QuantitySurveyConfigurationEvidenceLinks", "DecisionId"); migrationBuilder.CreateIndex("IX_QsConfigEvidence_DmsRecord", "QuantitySurveyConfigurationEvidenceLinks", "CentralDocumentRecordId"); migrationBuilder.CreateIndex("IX_QsConfigEvidence_DmsVersion", "QuantitySurveyConfigurationEvidenceLinks", "CentralDocumentVersionId"); migrationBuilder.CreateIndex("IX_QsConfigEvidence_Tenant_Decision", "QuantitySurveyConfigurationEvidenceLinks", new[] { "TenantId", "DecisionId" }); migrationBuilder.CreateIndex("UX_QsConfigEvidence_Tenant_Decision_Version", "QuantitySurveyConfigurationEvidenceLinks", new[] { "TenantId", "DecisionId", "CentralDocumentVersionId" }, unique: true, filter: "[IsDeleted] = 0 AND [CentralDocumentVersionId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_QsConfigRevisions_Profile", "QuantitySurveyConfigurationRevisions", "ProfileId"); migrationBuilder.CreateIndex("IX_QsConfigRevisions_Decision", "QuantitySurveyConfigurationRevisions", "DecisionId"); migrationBuilder.CreateIndex("IX_QsConfigRevisions_Tenant_Profile_Created", "QuantitySurveyConfigurationRevisions", new[] { "TenantId", "ProfileId", "CreatedAt" }); migrationBuilder.CreateIndex("IX_QsConfigRevisions_Tenant_Correlation", "QuantitySurveyConfigurationRevisions", new[] { "TenantId", "CorrelationId" });

        migrationBuilder.Sql("""
            DECLARE @Now datetime2 = SYSUTCDATETIME();
            INSERT INTO [QuantitySurveyConfigurationProfiles] ([Id],[ProfileKey],[ProfileCode],[Name],[Version],[LifecycleStatus],[EffectiveFrom],[ChangeSummary],[IsDefault],[CreatedAt],[CreatedBy],[IsDeleted],[TenantId])
            SELECT NEWID(),NEWID(),'TDC-QUANTITY-SURVEY','TDC Quantity Survey Configuration',1,0,CONVERT(date,@Now),'Initial QS-0001/QS-0002 tenant draft. Values require controlled selection, central-DMS evidence and approval.',1,@Now,'System',0,t.[Id]
            FROM [Tenants] t WHERE t.[IsDeleted]=0 AND NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationProfiles] p WHERE p.[TenantId]=t.[Id] AND p.[ProfileCode]='TDC-QUANTITY-SURVEY' AND p.[IsDeleted]=0);

            INSERT INTO [QuantitySurveyConfigurationDecisions] ([Id],[ProfileId],[DecisionKey],[SchemaVersion],[OwnerGroup],[Status],[ApprovalStatus],[EvidenceStatus],[ValueJson],[SourceLineage],[CreatedAt],[CreatedBy],[IsDeleted],[TenantId])
            SELECT NEWID(),p.[Id],d.[DecisionKey],1,d.[OwnerGroup],0,0,0,'{}',CONCAT(d.[ConfigurationKey],' tenant initializer; unapproved draft'),@Now,'System',0,p.[TenantId]
            FROM [QuantitySurveyConfigurationProfiles] p CROSS JOIN (VALUES
              ('QS-DEC-001','QS-CFG-001','QS + Development + ICT'),('QS-DEC-002','QS-CFG-002','QS + Development'),('QS-DEC-003','QS-CFG-003','QS + Legal/Contracts'),
              ('QS-DEC-004','QS-CFG-004','QS + Finance'),('QS-DEC-005','QS-CFG-005','QS + Procurement'),('QS-DEC-006','QS-CFG-006','QS + Finance + Legal/Contracts'),
              ('QS-DEC-007','QS-CFG-007','QS + Development + Contractors/Consultants'),('QS-DEC-008','QS-CFG-008','QS + Finance'),('QS-DEC-009','QS-CFG-009','QS + Legal/Contracts + Finance'),
              ('QS-DEC-010','QS-CFG-010','QS + Stores + Finance'),('QS-DEC-011','QS-CFG-011','QS + Development + Legal/Contracts'),('QS-DEC-012','QS-CFG-012','QS + Procurement + Legal'),
              ('QS-DEC-013','QS-CFG-013','QS + ICT + Contractors'),('QS-DEC-014','QS-CFG-014','QS + ICT + Development'),('QS-DEC-015','QS-CFG-015','QS + Management'),
              ('QS-DEC-016','QS-CFG-016','ICT + Finance + QS'),('QS-DEC-017','QS-CFG-017','QS + ICT')
            ) d([DecisionKey],[ConfigurationKey],[OwnerGroup])
            WHERE p.[ProfileCode]='TDC-QUANTITY-SURVEY' AND p.[Version]=1 AND p.[IsDeleted]=0 AND NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationDecisions] x WHERE x.[TenantId]=p.[TenantId] AND x.[ProfileId]=p.[Id] AND x.[DecisionKey]=d.[DecisionKey] AND x.[IsDeleted]=0);

            INSERT INTO [QuantitySurveyConfigurationRevisions] ([Id],[ProfileId],[Action],[Result],[CorrelationId],[ActorUserId],[ActorName],[ActorRoles],[Reason],[AfterJson],[CreatedAt],[CreatedBy],[IsDeleted],[TenantId])
            SELECT NEWID(),p.[Id],'SeedDraft','Succeeded',CONCAT('qs-migration-',CONVERT(nvarchar(36),p.[TenantId])),'00000000-0000-0000-0000-000000000000','System','System','Idempotent QS-0001/QS-0002 tenant seed plan.','{"profileCode":"TDC-QUANTITY-SURVEY","version":1,"lifecycleStatus":"draft","decisionCount":17}',@Now,'System',0,p.[TenantId]
            FROM [QuantitySurveyConfigurationProfiles] p WHERE p.[ProfileCode]='TDC-QUANTITY-SURVEY' AND p.[Version]=1 AND p.[IsDeleted]=0 AND NOT EXISTS (SELECT 1 FROM [QuantitySurveyConfigurationRevisions] x WHERE x.[TenantId]=p.[TenantId] AND x.[ProfileId]=p.[Id] AND x.[Action]='SeedDraft');

            EXEC('CREATE TRIGGER [TR_QsConfigurationRevisions_AppendOnly] ON [QuantitySurveyConfigurationRevisions] INSTEAD OF UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51000, ''Quantity-survey configuration history is append-only.'', 1; END');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsConfigurationRevisions_AppendOnly]");
        migrationBuilder.DropTable("QuantitySurveyConfigurationEvidenceLinks"); migrationBuilder.DropTable("QuantitySurveyConfigurationRevisions"); migrationBuilder.DropTable("QuantitySurveyConfigurationDecisions"); migrationBuilder.DropTable("QuantitySurveyConfigurationProfiles");
    }
}
