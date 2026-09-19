using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260720193000_AddProcurementConfigurationProfiles")]
    public partial class AddProcurementConfigurationProfiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementConfigurationProfiles",
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
                    table.PrimaryKey("PK_ProcurementConfigurationProfiles", x => x.Id);
                    table.CheckConstraint("CK_ProcurementConfigurationProfiles_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementConfigurationProfiles_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
                    table.CheckConstraint("CK_ProcurementConfigurationProfiles_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationProfiles_ProcurementConfigurationProfiles_SupersedesProfileId",
                        column: x => x.SupersedesProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementConfigurationDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionKey = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementConfigurationDecisions", x => x.Id);
                    table.CheckConstraint("CK_ProcurementConfigurationDecisions_DecisionKey", "[DecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_ProcurementConfigurationDecisions_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ProcurementConfigurationDecisions_SchemaVersion", "[SchemaVersion] > 0");
                    table.CheckConstraint("CK_ProcurementConfigurationDecisions_Status", "[Status] IN (0, 1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationDecisions_ProcurementConfigurationDecisions_SourceDecisionId",
                        column: x => x.SourceDecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationDecisions_ProcurementConfigurationProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationDecisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementConfigurationEvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReferenceMetadataJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementConfigurationEvidenceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationEvidenceLinks_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationEvidenceLinks_ProcurementConfigurationDecisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationEvidenceLinks_ProcurementConfigurationProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationEvidenceLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementConfigurationRevisions",
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
                    table.PrimaryKey("PK_ProcurementConfigurationRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationRevisions_ProcurementConfigurationDecisions_DecisionId",
                        column: x => x.DecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationRevisions_ProcurementConfigurationProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementConfigurationRevisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationDecisions_ProfileId", table: "ProcurementConfigurationDecisions", column: "ProfileId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationDecisions_SourceDecisionId", table: "ProcurementConfigurationDecisions", column: "SourceDecisionId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationDecisions_TenantId_DecisionKey_Status", table: "ProcurementConfigurationDecisions", columns: new[] { "TenantId", "DecisionKey", "Status" });
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationDecisions_TenantId_ProfileId_DecisionKey", table: "ProcurementConfigurationDecisions", columns: new[] { "TenantId", "ProfileId", "DecisionKey" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationEvidenceLinks_DecisionId", table: "ProcurementConfigurationEvidenceLinks", column: "DecisionId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationEvidenceLinks_FileUploadRecordId", table: "ProcurementConfigurationEvidenceLinks", column: "FileUploadRecordId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationEvidenceLinks_ProfileId", table: "ProcurementConfigurationEvidenceLinks", column: "ProfileId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationEvidenceLinks_TenantId_DecisionId", table: "ProcurementConfigurationEvidenceLinks", columns: new[] { "TenantId", "DecisionId" });
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationEvidenceLinks_TenantId_FileUploadRecordId", table: "ProcurementConfigurationEvidenceLinks", columns: new[] { "TenantId", "FileUploadRecordId" });
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationProfiles_SupersedesProfileId", table: "ProcurementConfigurationProfiles", column: "SupersedesProfileId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationProfiles_TenantId_LifecycleStatus_EffectiveFrom", table: "ProcurementConfigurationProfiles", columns: new[] { "TenantId", "LifecycleStatus", "EffectiveFrom" });
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationProfiles_TenantId_ProfileCode_Version", table: "ProcurementConfigurationProfiles", columns: new[] { "TenantId", "ProfileCode", "Version" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationProfiles_TenantId_ProfileKey_LifecycleStatus", table: "ProcurementConfigurationProfiles", columns: new[] { "TenantId", "ProfileKey", "LifecycleStatus" }, unique: true, filter: "[LifecycleStatus] = 1 AND [IsDeleted] = 0");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationProfiles_TenantId_ProfileKey_Version", table: "ProcurementConfigurationProfiles", columns: new[] { "TenantId", "ProfileKey", "Version" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationRevisions_DecisionId", table: "ProcurementConfigurationRevisions", column: "DecisionId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationRevisions_ProfileId", table: "ProcurementConfigurationRevisions", column: "ProfileId");
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationRevisions_TenantId_CorrelationId", table: "ProcurementConfigurationRevisions", columns: new[] { "TenantId", "CorrelationId" });
            migrationBuilder.CreateIndex(name: "IX_ProcurementConfigurationRevisions_TenantId_ProfileId_CreatedAt", table: "ProcurementConfigurationRevisions", columns: new[] { "TenantId", "ProfileId", "CreatedAt" });

            migrationBuilder.Sql("""
                DECLARE @Now datetime2 = SYSUTCDATETIME();

                INSERT INTO [ProcurementConfigurationProfiles]
                    ([Id], [ProfileKey], [ProfileCode], [Name], [Version], [LifecycleStatus], [EffectiveFrom],
                     [ChangeSummary], [IsDefault], [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
                SELECT NEWID(), NEWID(), 'TDC-PROCUREMENT', 'TDC Procurement Configuration', 1, 0, CONVERT(date, @Now),
                       'Initial tenant draft created by the idempotent TDC-0001 migration. Values remain unapproved.',
                       CAST(1 AS bit), @Now, 'System', CAST(0 AS bit), tenant.[Id]
                FROM [Tenants] tenant
                WHERE tenant.[IsDeleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [ProcurementConfigurationProfiles] existing
                      WHERE existing.[TenantId] = tenant.[Id]
                        AND existing.[ProfileCode] = 'TDC-PROCUREMENT'
                        AND existing.[IsDeleted] = 0);

                INSERT INTO [ProcurementConfigurationDecisions]
                    ([Id], [ProfileId], [DecisionKey], [SchemaVersion], [OwnerGroup], [Status], [ApprovalStatus],
                     [EvidenceStatus], [ValueJson], [SourceLineage], [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
                SELECT NEWID(), profile.[Id], decision.[DecisionKey], 1, decision.[OwnerGroup], 0, 0, 0, '{}',
                       'TDC-0001 tenant migration; unapproved draft', @Now, 'System', CAST(0 AS bit), profile.[TenantId]
                FROM [ProcurementConfigurationProfiles] profile
                CROSS JOIN (VALUES
                    ('DEC-001', 'TDC Procurement + Legal/PPA'),
                    ('DEC-002', 'TDC Procurement + Legal/PPA'),
                    ('DEC-003', 'MD + Procurement + Finance'),
                    ('DEC-004', 'TDC Procurement + Legal/Internal Audit'),
                    ('DEC-005', 'TDC Procurement + Finance'),
                    ('DEC-006', 'TDC Procurement + Legal/PPA'),
                    ('DEC-007', 'Procurement + Finance'),
                    ('DEC-008', 'Legal + ICT + Procurement'),
                    ('DEC-009', 'Procurement + ICT/PPA'),
                    ('DEC-010', 'Stores + Finance + Internal Audit'),
                    ('DEC-011', 'Procurement + Internal Audit'),
                    ('DEC-012', 'Steering Committee'),
                    ('DEC-013', 'Stores + Procurement + Finance'),
                    ('DEC-014', 'ICT + Procurement + Stores')
                ) decision([DecisionKey], [OwnerGroup])
                WHERE profile.[ProfileCode] = 'TDC-PROCUREMENT'
                  AND profile.[Version] = 1
                  AND profile.[LifecycleStatus] = 0
                  AND profile.[IsDeleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [ProcurementConfigurationDecisions] existing
                      WHERE existing.[TenantId] = profile.[TenantId]
                        AND existing.[ProfileId] = profile.[Id]
                        AND existing.[DecisionKey] = decision.[DecisionKey]
                        AND existing.[IsDeleted] = 0);

                INSERT INTO [ProcurementConfigurationRevisions]
                    ([Id], [ProfileId], [Action], [Result], [CorrelationId], [ActorUserId], [ActorName], [ActorRoles],
                     [Reason], [AfterJson], [CreatedAt], [CreatedBy], [IsDeleted], [TenantId])
                SELECT NEWID(), profile.[Id], 'SeedDraft', 'Succeeded', CONCAT('migration-', CONVERT(nvarchar(36), profile.[TenantId])),
                       '00000000-0000-0000-0000-000000000000', 'System', 'System',
                       'Idempotent initialization of the TDC procurement configuration decision register.',
                       '{"profileCode":"TDC-PROCUREMENT","version":1,"lifecycleStatus":"draft","decisionCount":14}',
                       @Now, 'System', CAST(0 AS bit), profile.[TenantId]
                FROM [ProcurementConfigurationProfiles] profile
                WHERE profile.[ProfileCode] = 'TDC-PROCUREMENT'
                  AND profile.[Version] = 1
                  AND profile.[LifecycleStatus] = 0
                  AND profile.[IsDeleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [ProcurementConfigurationRevisions] existing
                      WHERE existing.[TenantId] = profile.[TenantId]
                        AND existing.[ProfileId] = profile.[Id]
                        AND existing.[Action] = 'SeedDraft'
                        AND existing.[IsDeleted] = 0);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProcurementConfigurationEvidenceLinks");
            migrationBuilder.DropTable(name: "ProcurementConfigurationRevisions");
            migrationBuilder.DropTable(name: "ProcurementConfigurationDecisions");
            migrationBuilder.DropTable(name: "ProcurementConfigurationProfiles");
        }
    }
}
