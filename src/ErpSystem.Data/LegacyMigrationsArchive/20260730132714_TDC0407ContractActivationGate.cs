using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class TDC0407ContractActivationGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Contracts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentRecordId",
                table: "ContractDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CentralDocumentVersionId",
                table: "ContractDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FileUploadRecordId",
                table: "ContractDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcurementContractActivations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    AuthorityRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AwardReadinessDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardReadinessSequence = table.Column<int>(type: "int", nullable: false),
                    AwardReadinessIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GhanepsConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GhanepsConfigurationValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GhanepsRequired = table.Column<bool>(type: "bit", nullable: false),
                    GhanepsCompliant = table.Column<bool>(type: "bit", nullable: false),
                    PerformanceSecurityRequired = table.Column<bool>(type: "bit", nullable: false),
                    PerformanceBondRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivatedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecisionComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContractSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReadinessSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementContractActivations", x => x.Id);
                    table.CheckConstraint("CK_ProcurementContractActivations_Hashes", "LEN([AwardReadinessIntegrityHash]) = 64 AND LEN([GhanepsConfigurationValueHash]) = 64 AND LEN([ContractSnapshotHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([ContractSnapshotJson]) = 1 AND ISJSON([ReadinessSnapshotJson]) = 1");
                    table.CheckConstraint("CK_ProcurementContractActivations_Sequence", "[Sequence] >= 1");
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivations_PerformanceBondRequests_PerformanceBondRequestId",
                        column: x => x.PerformanceBondRequestId,
                        principalTable: "PerformanceBondRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivations_ProcurementAwardReadinessDecisions_AwardReadinessDecisionId",
                        column: x => x.AwardReadinessDecisionId,
                        principalTable: "ProcurementAwardReadinessDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivations_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivations_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementContractActivationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequirementKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequirementLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReferenceKind = table.Column<int>(type: "int", nullable: false),
                    WorkflowEvidenceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUploadRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementContractActivationEvidence", x => x.Id);
                    table.CheckConstraint("CK_ProcurementContractActivationEvidence_Hash", "LEN([EvidenceHash]) = 64");
                    table.CheckConstraint("CK_ProcurementContractActivationEvidence_Reference", "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR ([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivationEvidence_FileUploadRecords_FileUploadRecordId",
                        column: x => x.FileUploadRecordId,
                        principalTable: "FileUploadRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivationEvidence_ProcurementContractActivations_ActivationId",
                        column: x => x.ActivationId,
                        principalTable: "ProcurementContractActivations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivationEvidence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementContractActivationEvidence_WorkflowEvidenceDocuments_WorkflowEvidenceDocumentId",
                        column: x => x.WorkflowEvidenceDocumentId,
                        principalTable: "WorkflowEvidenceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_CentralDocumentRecordId",
                table: "ContractDocuments",
                column: "CentralDocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_CentralDocumentVersionId",
                table: "ContractDocuments",
                column: "CentralDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_FileUploadRecordId",
                table: "ContractDocuments",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_TenantId_CentralDocumentRecordId",
                table: "ContractDocuments",
                columns: new[] { "TenantId", "CentralDocumentRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivationEvidence_ActivationId",
                table: "ProcurementContractActivationEvidence",
                column: "ActivationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivationEvidence_FileUploadRecordId",
                table: "ProcurementContractActivationEvidence",
                column: "FileUploadRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivationEvidence_TenantId_ActivationId_RequirementKey",
                table: "ProcurementContractActivationEvidence",
                columns: new[] { "TenantId", "ActivationId", "RequirementKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivationEvidence_WorkflowEvidenceDocumentId",
                table: "ProcurementContractActivationEvidence",
                column: "WorkflowEvidenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_AwardReadinessDecisionId",
                table: "ProcurementContractActivations",
                column: "AwardReadinessDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_ContractId",
                table: "ProcurementContractActivations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_PerformanceBondRequestId",
                table: "ProcurementContractActivations",
                column: "PerformanceBondRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_TenantId_ContractId_Sequence",
                table: "ProcurementContractActivations",
                columns: new[] { "TenantId", "ContractId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_TenantId_ContractId_Status",
                table: "ProcurementContractActivations",
                columns: new[] { "TenantId", "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_TenantId_IdempotencyKey",
                table: "ProcurementContractActivations",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_WorkflowDefinitionId",
                table: "ProcurementContractActivations",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementContractActivations_WorkflowInstanceId",
                table: "ProcurementContractActivations",
                column: "WorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "ContractDocuments",
                column: "CentralDocumentRecordId",
                principalTable: "CentralDocumentRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "ContractDocuments",
                column: "CentralDocumentVersionId",
                principalTable: "CentralDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractDocuments_FileUploadRecords_FileUploadRecordId",
                table: "ContractDocuments",
                column: "FileUploadRecordId",
                principalTable: "FileUploadRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_Contracts_TDC0407ActivationGuard]
                ON [Contracts]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status = N'Active'
                          AND (d.Id IS NULL OR ISNULL(d.Status, N'') <> N'Active')
                          AND NOT EXISTS (
                              SELECT 1
                              FROM ProcurementContractActivations activation
                              WHERE activation.Id =
                                    TRY_CONVERT(uniqueidentifier,
                                        SESSION_CONTEXT(N'TDC0407_CONTRACT_ACTIVATION_ID'))
                                AND activation.TenantId = i.TenantId
                                AND activation.ContractId = i.Id
                                AND activation.Status IN (1, 3)
                                AND activation.WorkflowInstanceId IS NOT NULL
                                AND activation.DecidedAtUtc IS NOT NULL
                                AND activation.DecidedById IS NOT NULL
                                AND activation.IsDeleted = 0
                          ))
                        THROW 51400,
                            'Contract activation requires the exact approved TDC-0407 activation context.',
                            1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ContractDocuments_TDC0407DmsRequired]
                ON [ContractDocuments]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN FileUploadRecords upload
                          ON upload.Id = i.FileUploadRecordId
                         AND upload.TenantId = i.TenantId
                         AND upload.VirusScanStatus = 2
                         AND upload.IsDeleted = 0
                        LEFT JOIN CentralDocumentRecords record
                          ON record.Id = i.CentralDocumentRecordId
                         AND record.TenantId = i.TenantId
                         AND record.SourceEntityType = N'Contract'
                         AND record.SourceRecordId = i.ContractId
                         AND record.IsDeleted = 0
                        LEFT JOIN CentralDocumentVersions version
                          ON version.Id = i.CentralDocumentVersionId
                         AND version.TenantId = i.TenantId
                         AND version.DocumentRecordId = record.Id
                         AND version.FileUploadRecordId = upload.Id
                         AND version.IsDeleted = 0
                        WHERE i.IsDeleted = 0
                          AND (
                               i.FileUploadRecordId IS NULL
                            OR i.CentralDocumentRecordId IS NULL
                            OR i.CentralDocumentVersionId IS NULL
                            OR upload.Id IS NULL
                            OR record.Id IS NULL
                            OR version.Id IS NULL
                          ))
                        THROW 51401,
                            'Current contract documents require an exact malware-clean central-DMS record and version.',
                            1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementContractActivations_TDC0407Protected]
                ON [ProcurementContractActivations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL)
                        THROW 51410, 'Contract activation history cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE d.Id IS NULL
                          AND (
                               i.Status <> 0
                            OR i.Sequence < 1
                            OR i.SubmittedById IS NULL
                            OR i.SubmittedAtUtc IS NULL
                            OR NULLIF(LTRIM(RTRIM(i.Reason)), N'') IS NULL
                            OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), N'') IS NULL
                            OR NULLIF(LTRIM(RTRIM(i.CorrelationId)), N'') IS NULL
                            OR LEN(i.AwardReadinessIntegrityHash) <> 64
                            OR LEN(i.GhanepsConfigurationValueHash) <> 64
                            OR LEN(i.ContractSnapshotHash) <> 64
                            OR LEN(i.IntegrityHash) <> 64
                            OR ISJSON(i.ContractSnapshotJson) <> 1
                            OR ISJSON(i.ReadinessSnapshotJson) <> 1
                          ))
                        THROW 51411,
                            'A contract activation must begin as a complete immutable pending request.',
                            1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.ContractId <> d.ContractId
                           OR i.Sequence <> d.Sequence
                           OR i.ConfigurationProfileId <> d.ConfigurationProfileId
                           OR i.ConfigurationProfileVersion <> d.ConfigurationProfileVersion
                           OR i.PolicySetId <> d.PolicySetId
                           OR i.PolicyVersion <> d.PolicyVersion
                           OR i.AuthorityRuleId <> d.AuthorityRuleId
                           OR i.AuthorityName <> d.AuthorityName
                           OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           OR i.AwardReadinessDecisionId <> d.AwardReadinessDecisionId
                           OR i.AwardReadinessSequence <> d.AwardReadinessSequence
                           OR i.AwardReadinessIntegrityHash <> d.AwardReadinessIntegrityHash
                           OR i.GhanepsConfigurationDecisionId <> d.GhanepsConfigurationDecisionId
                           OR i.GhanepsConfigurationValueHash <> d.GhanepsConfigurationValueHash
                           OR i.GhanepsRequired <> d.GhanepsRequired
                           OR i.GhanepsCompliant <> d.GhanepsCompliant
                           OR i.PerformanceSecurityRequired <> d.PerformanceSecurityRequired
                           OR ISNULL(i.PerformanceBondRequestId,
                                   '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.PerformanceBondRequestId,
                                   '00000000-0000-0000-0000-000000000000')
                           OR i.SubmittedById <> d.SubmittedById
                           OR i.SubmittedByName <> d.SubmittedByName
                           OR i.SubmittedAtUtc <> d.SubmittedAtUtc
                           OR i.Reason <> d.Reason
                           OR i.IdempotencyKey <> d.IdempotencyKey
                           OR i.CorrelationId <> d.CorrelationId
                           OR i.ContractSnapshotJson <> d.ContractSnapshotJson
                           OR i.ContractSnapshotHash <> d.ContractSnapshotHash
                           OR i.CreatedAt <> d.CreatedAt
                           OR ISNULL(i.CreatedById,
                                   '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.CreatedById,
                                   '00000000-0000-0000-0000-000000000000')
                           OR i.IsDeleted <> d.IsDeleted)
                        THROW 51412,
                            'Contract activation configuration, authority, award, evidence lineage, and submission data are immutable.',
                            1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.Status <> d.Status
                          AND NOT (
                               (d.Status = 0 AND i.Status IN (1, 2, 4))
                            OR (d.Status = 1 AND i.Status IN (3, 4))
                            OR (d.Status = 4 AND i.Status IN (3, 5))
                          ))
                        THROW 51413, 'The contract activation status transition is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN WorkflowInstances workflow
                          ON workflow.Id = i.WorkflowInstanceId
                         AND workflow.TenantId = i.TenantId
                         AND workflow.WorkflowDefinitionId = i.WorkflowDefinitionId
                         AND workflow.IsDeleted = 0
                        WHERE i.Status IN (1, 3, 4)
                          AND (
                               workflow.Id IS NULL
                            OR workflow.Status <> 2
                            OR i.DecidedAtUtc IS NULL
                            OR i.DecidedById IS NULL
                          ))
                        THROW 51414,
                            'Approved, activated, or revalidation-failed contract activations require the exact completed shared workflow.',
                            1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN WorkflowInstances workflow
                          ON workflow.Id = i.WorkflowInstanceId
                         AND workflow.TenantId = i.TenantId
                         AND workflow.WorkflowDefinitionId = i.WorkflowDefinitionId
                         AND workflow.IsDeleted = 0
                        WHERE i.Status = 2
                          AND (
                               workflow.Id IS NULL
                            OR workflow.Status NOT IN (3, 4)
                            OR i.DecidedAtUtc IS NULL
                            OR i.DecidedById IS NULL
                          ))
                        THROW 51416,
                            'Rejected contract activations require a cancelled or failed shared-workflow outcome.',
                            1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.Status = 3
                          AND (
                               i.Id <>
                                   TRY_CONVERT(uniqueidentifier,
                                       SESSION_CONTEXT(N'TDC0407_CONTRACT_ACTIVATION_ID'))
                            OR i.ActivatedAtUtc IS NULL
                            OR i.ActivatedById IS NULL
                          ))
                        THROW 51415,
                            'Final contract activation requires the exact TDC-0407 mutation context.',
                            1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [TR_ProcurementContractActivationEvidence_TDC0407Immutable]
                ON [ProcurementContractActivationEvidence]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51420,
                            'Contract activation evidence is append-only and immutable.',
                            1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementContractActivations activation
                          ON activation.Id = i.ActivationId
                         AND activation.TenantId = i.TenantId
                         AND activation.Status = 0
                         AND activation.IsDeleted = 0
                        LEFT JOIN WorkflowEvidenceDocuments workflowEvidence
                          ON workflowEvidence.Id = i.WorkflowEvidenceDocumentId
                         AND workflowEvidence.TenantId = i.TenantId
                         AND workflowEvidence.IsCurrent = 1
                         AND workflowEvidence.VerificationStatus = 1
                         AND workflowEvidence.MalwareScanStatus = 1
                         AND workflowEvidence.IsDeleted = 0
                        LEFT JOIN FileUploadRecords upload
                          ON upload.Id = i.FileUploadRecordId
                         AND upload.TenantId = i.TenantId
                         AND upload.VirusScanStatus = 2
                         AND upload.IsDeleted = 0
                        WHERE activation.Id IS NULL
                           OR LEN(i.EvidenceHash) <> 64
                           OR NULLIF(LTRIM(RTRIM(i.EvidenceReference)), N'') IS NULL
                           OR (
                                i.ReferenceKind = 0
                                AND workflowEvidence.Id IS NULL
                           )
                           OR (
                                i.ReferenceKind = 1
                                AND upload.Id IS NULL
                           ))
                        THROW 51421,
                            'Activation evidence must be tenant-safe, current, verified, and malware-clean.',
                            1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS [TR_ProcurementContractActivationEvidence_TDC0407Immutable];
                DROP TRIGGER IF EXISTS [TR_ProcurementContractActivations_TDC0407Protected];
                DROP TRIGGER IF EXISTS [TR_ContractDocuments_TDC0407DmsRequired];
                DROP TRIGGER IF EXISTS [TR_Contracts_TDC0407ActivationGuard];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ContractDocuments_CentralDocumentRecords_CentralDocumentRecordId",
                table: "ContractDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractDocuments_CentralDocumentVersions_CentralDocumentVersionId",
                table: "ContractDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractDocuments_FileUploadRecords_FileUploadRecordId",
                table: "ContractDocuments");

            migrationBuilder.DropTable(
                name: "ProcurementContractActivationEvidence");

            migrationBuilder.DropTable(
                name: "ProcurementContractActivations");

            migrationBuilder.DropIndex(
                name: "IX_ContractDocuments_CentralDocumentRecordId",
                table: "ContractDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ContractDocuments_CentralDocumentVersionId",
                table: "ContractDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ContractDocuments_FileUploadRecordId",
                table: "ContractDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ContractDocuments_TenantId_CentralDocumentRecordId",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CentralDocumentRecordId",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "CentralDocumentVersionId",
                table: "ContractDocuments");

            migrationBuilder.DropColumn(
                name: "FileUploadRecordId",
                table: "ContractDocuments");
        }
    }
}
