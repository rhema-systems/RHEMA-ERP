using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810005756_AddQuantitySurveyEscalationDisputes")]
public partial class AddQuantitySurveyEscalationDisputes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QuantitySurveyEscalationDisputes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CalculationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractorNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DisputeReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                CalculationSnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DisputeReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                OpenedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ContractorResponseClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ContractorResponseHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                ContractorResponse = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                ContractorRespondedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ContractorRespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ResolutionClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ResolutionRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                Outcome = table.Column<int>(type: "int", nullable: true),
                ResolutionNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyEscalationDisputes", x => x.Id);
                table.CheckConstraint("CK_QsEscalationDisputes_Status", "[Status] IN ('Open','ContractorResponded','Resolved')");
                table.CheckConstraint("CK_QsEscalationDisputes_Outcome", "[Outcome] IS NULL OR [Outcome] IN (0,1,2)");
                table.CheckConstraint("CK_QsEscalationDisputes_Sod", "[ResolvedById] IS NULL OR ([ResolvedById] <> [OpenedById] AND [ResolvedById] <> [ContractorRespondedById])");
                table.CheckConstraint("CK_QsEscalationDisputes_Lifecycle", "([Status] = 'Open' AND [ContractorResponseClientRequestId] IS NULL AND [ContractorResponse] IS NULL AND [ContractorRespondedById] IS NULL AND [ContractorRespondedAt] IS NULL AND [ResolutionClientRequestId] IS NULL AND [Outcome] IS NULL AND [ResolutionNotes] IS NULL AND [ResolvedById] IS NULL AND [ResolvedAt] IS NULL) OR ([Status] = 'ContractorResponded' AND [ContractorResponseClientRequestId] IS NOT NULL AND [ContractorResponseHash] IS NOT NULL AND [ContractorResponse] IS NOT NULL AND [ContractorRespondedById] IS NOT NULL AND [ContractorRespondedAt] IS NOT NULL AND [ResolutionClientRequestId] IS NULL AND [Outcome] IS NULL AND [ResolutionNotes] IS NULL AND [ResolvedById] IS NULL AND [ResolvedAt] IS NULL) OR ([Status] = 'Resolved' AND [ContractorResponseClientRequestId] IS NOT NULL AND [ContractorResponseHash] IS NOT NULL AND [ContractorResponse] IS NOT NULL AND [ContractorRespondedById] IS NOT NULL AND [ContractorRespondedAt] IS NOT NULL AND [ResolutionClientRequestId] IS NOT NULL AND [ResolutionRequestHash] IS NOT NULL AND [Outcome] IS NOT NULL AND [ResolutionNotes] IS NOT NULL AND [ResolvedById] IS NOT NULL AND [ResolvedAt] IS NOT NULL)");
                table.ForeignKey("FK_QsEscalationDisputes_CalculationRun", x => x.CalculationRunId, "QuantitySurveyEscalationCalculationRuns", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputes_Project", x => x.ProjectId, "Projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputes_Contract", x => x.ContractId, "Contracts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputes_Contractor", x => x.ContractorBusinessPartnerId, "BusinessPartners", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputes_OpenedBy", x => x.OpenedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputes_RespondedBy", x => x.ContractorRespondedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputes_ResolvedBy", x => x.ResolvedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputes_Tenant", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyEscalationDisputeAttachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisputeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                AttachmentType = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                ChecksumSha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CentralDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UploadedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyEscalationDisputeAttachments", x => x.Id);
                table.CheckConstraint("CK_QsEscalationDisputeAttachments_Type", "[AttachmentType] IN (0,1,2)");
                table.CheckConstraint("CK_QsEscalationDisputeAttachments_File", "[FileSize] > 0 AND LEN([ChecksumSha256]) = 64 AND LEN([RequestHash]) = 64");
                table.ForeignKey("FK_QsEscalationDisputeAttachments_Dispute", x => x.DisputeId, "QuantitySurveyEscalationDisputes", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputeAttachments_Upload", x => x.FileUploadRecordId, "FileUploadRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputeAttachments_Document", x => x.CentralDocumentRecordId, "CentralDocumentRecords", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputeAttachments_Version", x => x.CentralDocumentVersionId, "CentralDocumentVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputeAttachments_UploadedBy", x => x.UploadedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputeAttachments_Tenant", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyEscalationDisputeRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisputeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuantitySurveyEscalationDisputeRevisions", x => x.Id);
                table.ForeignKey("FK_QsEscalationDisputeRevisions_Dispute", x => x.DisputeId, "QuantitySurveyEscalationDisputes", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputeRevisions_Actor", x => x.ActorUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QsEscalationDisputeRevisions_Tenant", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        CreateIndexes(migrationBuilder);
        CreateGuards(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationDisputeRevisions_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationDisputeAttachments_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QsEscalationDisputes_Guard];");
        migrationBuilder.DropTable(name: "QuantitySurveyEscalationDisputeAttachments");
        migrationBuilder.DropTable(name: "QuantitySurveyEscalationDisputeRevisions");
        migrationBuilder.DropTable(name: "QuantitySurveyEscalationDisputes");
    }

    private static void CreateIndexes(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "CalculationRunId", "ProjectId", "ContractId", "ContractorBusinessPartnerId", "OpenedById", "ContractorRespondedById", "ResolvedById" })
            migrationBuilder.CreateIndex(name: $"IX_QuantitySurveyEscalationDisputes_{column}", table: "QuantitySurveyEscalationDisputes", column: column);
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputes_TenantId_CalculationRunId", table: "QuantitySurveyEscalationDisputes", columns: new[] { "TenantId", "CalculationRunId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputes_TenantId_DisputeReference", table: "QuantitySurveyEscalationDisputes", columns: new[] { "TenantId", "DisputeReference" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputes_TenantId_ClientRequestId", table: "QuantitySurveyEscalationDisputes", columns: new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputes_TenantId_ContractorResponseClientRequestId", table: "QuantitySurveyEscalationDisputes", columns: new[] { "TenantId", "ContractorResponseClientRequestId" }, unique: true, filter: "[ContractorResponseClientRequestId] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputes_TenantId_ResolutionClientRequestId", table: "QuantitySurveyEscalationDisputes", columns: new[] { "TenantId", "ResolutionClientRequestId" }, unique: true, filter: "[ResolutionClientRequestId] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputes_TenantId_ProjectId_Status_OpenedAt", table: "QuantitySurveyEscalationDisputes", columns: new[] { "TenantId", "ProjectId", "Status", "OpenedAt" });
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputes_TenantId_ContractId_Status", table: "QuantitySurveyEscalationDisputes", columns: new[] { "TenantId", "ContractId", "Status" });

        foreach (var column in new[] { "DisputeId", "FileUploadRecordId", "CentralDocumentRecordId", "CentralDocumentVersionId", "UploadedById" })
            migrationBuilder.CreateIndex(name: $"IX_QuantitySurveyEscalationDisputeAttachments_{column}", table: "QuantitySurveyEscalationDisputeAttachments", column: column);
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputeAttachments_TenantId_ClientRequestId", table: "QuantitySurveyEscalationDisputeAttachments", columns: new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputeAttachments_TenantId_CentralDocumentVersionId", table: "QuantitySurveyEscalationDisputeAttachments", columns: new[] { "TenantId", "CentralDocumentVersionId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputeAttachments_TenantId_DisputeId_AttachmentType_CreatedAt", table: "QuantitySurveyEscalationDisputeAttachments", columns: new[] { "TenantId", "DisputeId", "AttachmentType", "CreatedAt" });

        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputeRevisions_DisputeId", table: "QuantitySurveyEscalationDisputeRevisions", column: "DisputeId");
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputeRevisions_ActorUserId", table: "QuantitySurveyEscalationDisputeRevisions", column: "ActorUserId");
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputeRevisions_TenantId_DisputeId_CreatedAt", table: "QuantitySurveyEscalationDisputeRevisions", columns: new[] { "TenantId", "DisputeId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_QuantitySurveyEscalationDisputeRevisions_TenantId_CorrelationId", table: "QuantitySurveyEscalationDisputeRevisions", columns: new[] { "TenantId", "CorrelationId" });
    }

    private static void CreateGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEscalationDisputes_Guard]
            ON [QuantitySurveyEscalationDisputes]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                    THROW 51070, 'Escalation disputes cannot be deleted; retain their governed history.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE (d.[Id] IS NULL AND (i.[Status] <> 'Open' OR i.[IsDeleted] = 1))
                       OR (d.[Id] IS NOT NULL AND NOT ((d.[Status] = 'Open' AND i.[Status] = 'ContractorResponded') OR (d.[Status] = 'ContractorResponded' AND i.[Status] = 'Resolved'))))
                    THROW 51071, 'Invalid escalation dispute lifecycle transition.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
                    WHERE EXISTS (
                        SELECT d.[TenantId],d.[CalculationRunId],d.[ProjectId],d.[ContractId],d.[ContractorBusinessPartnerId],d.[ContractorNameSnapshot],d.[DisputeReference],d.[ClientRequestId],d.[RequestHash],d.[CalculationSnapshotHash],d.[Subject],d.[DisputeReason],d.[OpenedById],d.[OpenedAt],d.[CreatedAt],d.[CreatedById],d.[IsDeleted]
                        EXCEPT
                        SELECT i.[TenantId],i.[CalculationRunId],i.[ProjectId],i.[ContractId],i.[ContractorBusinessPartnerId],i.[ContractorNameSnapshot],i.[DisputeReference],i.[ClientRequestId],i.[RequestHash],i.[CalculationSnapshotHash],i.[Subject],i.[DisputeReason],i.[OpenedById],i.[OpenedAt],i.[CreatedAt],i.[CreatedById],i.[IsDeleted]))
                    THROW 51070, 'Escalation dispute calculation, project, Works contract, contractor and opening lineage is immutable.', 1;

                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE NOT EXISTS (SELECT 1 FROM [QuantitySurveyEscalationCalculationRuns] r WHERE r.[Id] = i.[CalculationRunId] AND r.[TenantId] = i.[TenantId] AND r.[ProjectId] = i.[ProjectId] AND r.[ContractId] = i.[ContractId] AND r.[SnapshotHash] = i.[CalculationSnapshotHash] AND r.[Status] = 'ApprovedPendingApplication' AND r.[ApprovalStatus] = 'Approved' AND r.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [Projects] p WHERE p.[Id] = i.[ProjectId] AND p.[TenantId] = i.[TenantId] AND p.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [Contracts] c WHERE c.[Id] = i.[ContractId] AND c.[TenantId] = i.[TenantId] AND c.[BusinessPartnerId] = i.[ContractorBusinessPartnerId] AND c.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [BusinessPartners] bp WHERE bp.[Id] = i.[ContractorBusinessPartnerId] AND bp.[TenantId] = i.[TenantId] AND bp.[IsDeleted] = 0)
                       OR (NOT EXISTS (SELECT 1 FROM deleted d WHERE d.[Id] = i.[Id]) AND NOT EXISTS (SELECT 1 FROM [BusinessPartners] bp WHERE bp.[Id] = i.[ContractorBusinessPartnerId] AND bp.[TenantId] = i.[TenantId] AND bp.[PartnerName] = i.[ContractorNameSnapshot] AND bp.[IsDeleted] = 0))
                       OR (i.[ResolvedById] IS NOT NULL AND (i.[ResolvedById] = i.[OpenedById] OR i.[ResolvedById] = i.[ContractorRespondedById])))
                    THROW 51070, 'Escalation dispute tenant, approved calculation, Works contract, contractor or separation-of-duties lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEscalationDisputeAttachments_Guard]
            ON [QuantitySurveyEscalationDisputeAttachments]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51072, 'Escalation dispute evidence is append-only.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i
                    WHERE i.[IsDeleted] = 1
                       OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyEscalationDisputes] d WHERE d.[Id] = i.[DisputeId] AND d.[TenantId] = i.[TenantId] AND d.[Status] <> 'Resolved' AND d.[IsDeleted] = 0)
                       OR (SELECT COUNT_BIG(*) FROM [QuantitySurveyEscalationDisputeAttachments] a WHERE a.[TenantId] = i.[TenantId] AND a.[DisputeId] = i.[DisputeId] AND a.[IsDeleted] = 0) > 10
                       OR NOT EXISTS (SELECT 1 FROM [FileUploadRecords] f WHERE f.[Id] = i.[FileUploadRecordId] AND f.[TenantId] = i.[TenantId] AND f.[Category] = 'quantity-survey-escalation-dispute-evidence' AND f.[VirusScanStatus] = 2 AND f.[FileSize] = i.[FileSize] AND f.[IsDeleted] = 0)
                       OR NOT EXISTS (SELECT 1 FROM [CentralDocumentRecords] r JOIN [CentralDocumentVersions] v ON v.[DocumentRecordId] = r.[Id] AND v.[TenantId] = r.[TenantId] WHERE r.[Id] = i.[CentralDocumentRecordId] AND r.[TenantId] = i.[TenantId] AND r.[SourceModule] = 'QuantitySurvey' AND r.[SourceEntityType] = 'QuantitySurveyEscalationDisputeAttachment' AND r.[SourceRecordId] = i.[Id] AND r.[LifecycleStatus] = 'Active' AND r.[IsDeleted] = 0 AND v.[Id] = i.[CentralDocumentVersionId] AND v.[FileUploadRecordId] = i.[FileUploadRecordId] AND v.[FileSize] = i.[FileSize] AND v.[Status] = 'Validated' AND v.[VersionNumber] = r.[CurrentVersion] AND v.[IsDeleted] = 0))
                    THROW 51072, 'Escalation dispute evidence tenant, clean-upload or central-DMS lineage is invalid.', 1;
            END
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER [TR_QsEscalationDisputeRevisions_Guard]
            ON [QuantitySurveyEscalationDisputeRevisions]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted)
                    THROW 51072, 'Escalation dispute revision history is append-only.', 1;
                IF EXISTS (SELECT 1 FROM inserted i WHERE i.[IsDeleted] = 1 OR NOT EXISTS (SELECT 1 FROM [QuantitySurveyEscalationDisputes] d WHERE d.[Id] = i.[DisputeId] AND d.[TenantId] = i.[TenantId] AND d.[IsDeleted] = 0))
                    THROW 51072, 'Escalation dispute revision tenant lineage is invalid.', 1;
            END
            """);
    }
}
