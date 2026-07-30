using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementGhanepsExchangeControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProcurementConfigurationProfiles_TenantId_Id",
                table: "ProcurementConfigurationProfiles",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProcurementConfigurationDecisions_TenantId_ProfileId_Id",
                table: "ProcurementConfigurationDecisions",
                columns: new[] { "TenantId", "ProfileId", "Id" });

            migrationBuilder.CreateTable(
                name: "ProcurementGhanepsExchangeEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceVariant = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceOccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventFamily = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    MappingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalEventCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgementContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgementPermissionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReconciliationPermissionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfigurationProfileVersion = table.Column<int>(type: "int", nullable: false),
                    ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationDecisionSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ExchangeProfileCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigurationValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfigurationValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MappingSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MappingIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConfigurationEffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfigurationEffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcknowledgementRule = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReconciliationRule = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AcknowledgementRequired = table.Column<bool>(type: "bit", nullable: false),
                    ReconciliationRequired = table.Column<bool>(type: "bit", nullable: false),
                    MaximumRetryAttempts = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ProcurementGhanepsExchangeEvents", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementGhanepsExchangeEvents_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ProcurementGhanepsExchangeEvents_State", "[SourceType] BETWEEN 0 AND 2 AND [EventFamily] BETWEEN 0 AND 2 AND [Direction] BETWEEN 0 AND 1 AND [Status] BETWEEN 0 AND 6 AND [ConfigurationProfileVersion] >= 1 AND [ConfigurationDecisionSchemaVersion] >= 1 AND [MaximumRetryAttempts] BETWEEN 0 AND 100 AND LEN([RequestFingerprint]) = 64 AND ([ConfigurationEffectiveToUtc] IS NULL OR [ConfigurationEffectiveToUtc] >= [ConfigurationEffectiveFromUtc]) AND LEN([SourceIntegrityHash]) = 64 AND LEN([ConfigurationValueHash]) = 64 AND LEN([MappingIntegrityHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([SourceSnapshotJson]) = 1 AND ISJSON([ConfigurationValueJson]) = 1 AND ISJSON([MappingSnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeEvents_ProcurementConfigurationDecisions_TenantId_ConfigurationProfileId_ConfigurationDecisionId",
                        columns: x => new { x.TenantId, x.ConfigurationProfileId, x.ConfigurationDecisionId },
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumns: new[] { "TenantId", "ProfileId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeEvents_ProcurementConfigurationProfiles_TenantId_ConfigurationProfileId",
                        columns: x => new { x.TenantId, x.ConfigurationProfileId },
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementGhanepsExchangePayloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    TemplateReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchemaReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalPayloadVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    PayloadContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementGhanepsExchangePayloads", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementGhanepsExchangePayloads_TenantId_ExchangeEventId_Id", x => new { x.TenantId, x.ExchangeEventId, x.Id });
                    table.CheckConstraint("CK_ProcurementGhanepsExchangePayloads_State", "[Version] >= 1 AND [Direction] BETWEEN 0 AND 1 AND LEN([PayloadChecksumSha256]) = 64 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangePayloads_ProcurementGhanepsExchangeEvents_TenantId_ExchangeEventId",
                        columns: x => new { x.TenantId, x.ExchangeEventId },
                        principalTable: "ProcurementGhanepsExchangeEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangePayloads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementGhanepsExchangeAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    IsRetry = table.Column<bool>(type: "bit", nullable: false),
                    SupersedesAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    TransportReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PayloadChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AttemptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementGhanepsExchangeAttempts", x => x.Id);
                    table.UniqueConstraint("AK_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_Id", x => new { x.TenantId, x.ExchangeEventId, x.Id });
                    table.UniqueConstraint("AK_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_Id_PayloadId", x => new { x.TenantId, x.ExchangeEventId, x.Id, x.PayloadId });
                    table.CheckConstraint("CK_ProcurementGhanepsExchangeAttempts_State", "[AttemptNumber] >= 1 AND [Outcome] BETWEEN 0 AND 1 AND LEN([PayloadChecksumSha256]) = 64 AND LEN([RequestFingerprint]) = 64 AND LEN([IntegrityHash]) = 64 AND (([Outcome] = 0 AND [TransportReference] IS NOT NULL AND [FailureCode] IS NULL AND [FailureMessage] IS NULL) OR ([Outcome] = 1 AND [FailureCode] IS NOT NULL AND [FailureMessage] IS NOT NULL)) AND (([IsRetry] = 0 AND [SupersedesAttemptId] IS NULL) OR ([IsRetry] = 1 AND [SupersedesAttemptId] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAttempts_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_SupersedesAttemptId",
                        columns: x => new { x.TenantId, x.ExchangeEventId, x.SupersedesAttemptId },
                        principalTable: "ProcurementGhanepsExchangeAttempts",
                        principalColumns: new[] { "TenantId", "ExchangeEventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAttempts_ProcurementGhanepsExchangeEvents_TenantId_ExchangeEventId",
                        columns: x => new { x.TenantId, x.ExchangeEventId },
                        principalTable: "ProcurementGhanepsExchangeEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAttempts_ProcurementGhanepsExchangePayloads_TenantId_ExchangeEventId_PayloadId",
                        columns: x => new { x.TenantId, x.ExchangeEventId, x.PayloadId },
                        principalTable: "ProcurementGhanepsExchangePayloads",
                        principalColumns: new[] { "TenantId", "ExchangeEventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAttempts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementGhanepsExchangeAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    AcknowledgementReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalStatusCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgementContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AcknowledgementChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementGhanepsExchangeAcknowledgements", x => x.Id);
                    table.CheckConstraint("CK_ProcurementGhanepsExchangeAcknowledgements_State", "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 1 AND LEN([AcknowledgementChecksumSha256]) = 64 AND LEN([RequestFingerprint]) = 64 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAcknowledgements_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_AttemptId_PayloadId",
                        columns: x => new { x.TenantId, x.ExchangeEventId, x.AttemptId, x.PayloadId },
                        principalTable: "ProcurementGhanepsExchangeAttempts",
                        principalColumns: new[] { "TenantId", "ExchangeEventId", "Id", "PayloadId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAcknowledgements_ProcurementGhanepsExchangeEvents_TenantId_ExchangeEventId",
                        columns: x => new { x.TenantId, x.ExchangeEventId },
                        principalTable: "ProcurementGhanepsExchangeEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAcknowledgements_ProcurementGhanepsExchangePayloads_TenantId_ExchangeEventId_PayloadId",
                        columns: x => new { x.TenantId, x.ExchangeEventId, x.PayloadId },
                        principalTable: "ProcurementGhanepsExchangePayloads",
                        principalColumns: new[] { "TenantId", "ExchangeEventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeAcknowledgements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementGhanepsExchangeReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    ExpectedReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActualReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpectedChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActualChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReconciledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciledByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReconciledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementGhanepsExchangeReconciliations", x => x.Id);
                    table.CheckConstraint("CK_ProcurementGhanepsExchangeReconciliations_State", "[Sequence] >= 1 AND [Outcome] BETWEEN 0 AND 2 AND LEN([ExpectedChecksumSha256]) = 64 AND ([ActualChecksumSha256] IS NULL OR LEN([ActualChecksumSha256]) = 64) AND LEN([RequestFingerprint]) = 64 AND LEN([IntegrityHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeReconciliations_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_AttemptId_PayloadId",
                        columns: x => new { x.TenantId, x.ExchangeEventId, x.AttemptId, x.PayloadId },
                        principalTable: "ProcurementGhanepsExchangeAttempts",
                        principalColumns: new[] { "TenantId", "ExchangeEventId", "Id", "PayloadId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeReconciliations_ProcurementGhanepsExchangeEvents_TenantId_ExchangeEventId",
                        columns: x => new { x.TenantId, x.ExchangeEventId },
                        principalTable: "ProcurementGhanepsExchangeEvents",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeReconciliations_ProcurementGhanepsExchangePayloads_TenantId_ExchangeEventId_PayloadId",
                        columns: x => new { x.TenantId, x.ExchangeEventId, x.PayloadId },
                        principalTable: "ProcurementGhanepsExchangePayloads",
                        principalColumns: new[] { "TenantId", "ExchangeEventId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementGhanepsExchangeReconciliations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAcknowledgements_TenantId_AttemptId",
                table: "ProcurementGhanepsExchangeAcknowledgements",
                columns: new[] { "TenantId", "AttemptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAcknowledgements_TenantId_ExchangeEventId_AttemptId_PayloadId",
                table: "ProcurementGhanepsExchangeAcknowledgements",
                columns: new[] { "TenantId", "ExchangeEventId", "AttemptId", "PayloadId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAcknowledgements_TenantId_ExchangeEventId_IdempotencyKey",
                table: "ProcurementGhanepsExchangeAcknowledgements",
                columns: new[] { "TenantId", "ExchangeEventId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAcknowledgements_TenantId_ExchangeEventId_PayloadId",
                table: "ProcurementGhanepsExchangeAcknowledgements",
                columns: new[] { "TenantId", "ExchangeEventId", "PayloadId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAcknowledgements_TenantId_ExchangeEventId_Sequence",
                table: "ProcurementGhanepsExchangeAcknowledgements",
                columns: new[] { "TenantId", "ExchangeEventId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_AttemptNumber",
                table: "ProcurementGhanepsExchangeAttempts",
                columns: new[] { "TenantId", "ExchangeEventId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_IdempotencyKey",
                table: "ProcurementGhanepsExchangeAttempts",
                columns: new[] { "TenantId", "ExchangeEventId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_PayloadId",
                table: "ProcurementGhanepsExchangeAttempts",
                columns: new[] { "TenantId", "ExchangeEventId", "PayloadId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAttempts_TenantId_ExchangeEventId_SupersedesAttemptId",
                table: "ProcurementGhanepsExchangeAttempts",
                columns: new[] { "TenantId", "ExchangeEventId", "SupersedesAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeAttempts_TenantId_Outcome_AttemptedAtUtc",
                table: "ProcurementGhanepsExchangeAttempts",
                columns: new[] { "TenantId", "Outcome", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeEvents_TenantId_ConfigurationProfileId_ConfigurationDecisionId",
                table: "ProcurementGhanepsExchangeEvents",
                columns: new[] { "TenantId", "ConfigurationProfileId", "ConfigurationDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeEvents_TenantId_SourceType_SourceId_EventFamily_Direction_MappingKey_EventReference",
                table: "ProcurementGhanepsExchangeEvents",
                columns: new[] { "TenantId", "SourceType", "SourceId", "EventFamily", "Direction", "MappingKey", "EventReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeEvents_TenantId_SourceType_SourceId_IdempotencyKey",
                table: "ProcurementGhanepsExchangeEvents",
                columns: new[] { "TenantId", "SourceType", "SourceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeEvents_TenantId_Status_PreparedAtUtc",
                table: "ProcurementGhanepsExchangeEvents",
                columns: new[] { "TenantId", "Status", "PreparedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangePayloads_TenantId_ExchangeEventId_IdempotencyKey",
                table: "ProcurementGhanepsExchangePayloads",
                columns: new[] { "TenantId", "ExchangeEventId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangePayloads_TenantId_ExchangeEventId_Version",
                table: "ProcurementGhanepsExchangePayloads",
                columns: new[] { "TenantId", "ExchangeEventId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangePayloads_TenantId_PayloadChecksumSha256",
                table: "ProcurementGhanepsExchangePayloads",
                columns: new[] { "TenantId", "PayloadChecksumSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeReconciliations_TenantId_ExchangeEventId_AttemptId_PayloadId",
                table: "ProcurementGhanepsExchangeReconciliations",
                columns: new[] { "TenantId", "ExchangeEventId", "AttemptId", "PayloadId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeReconciliations_TenantId_ExchangeEventId_IdempotencyKey",
                table: "ProcurementGhanepsExchangeReconciliations",
                columns: new[] { "TenantId", "ExchangeEventId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeReconciliations_TenantId_ExchangeEventId_PayloadId",
                table: "ProcurementGhanepsExchangeReconciliations",
                columns: new[] { "TenantId", "ExchangeEventId", "PayloadId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeReconciliations_TenantId_ExchangeEventId_Sequence",
                table: "ProcurementGhanepsExchangeReconciliations",
                columns: new[] { "TenantId", "ExchangeEventId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementGhanepsExchangeReconciliations_TenantId_Outcome_ReconciledAtUtc",
                table: "ProcurementGhanepsExchangeReconciliations",
                columns: new[] { "TenantId", "Outcome", "ReconciledAtUtc" });

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementGhanepsExchangeEvents_Guard]
                ON [dbo].[ProcurementGhanepsExchangeEvents]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted d WHERE NOT EXISTS (
                        SELECT 1 FROM inserted i WHERE i.Id = d.Id))
                        THROW 51600, 'GHANEPS exchange events cannot be deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id)
                          AND (
                              i.IsDeleted = 1
                              OR i.TenantId = '00000000-0000-0000-0000-000000000000'
                              OR i.SourceId = '00000000-0000-0000-0000-000000000000'
                              OR i.ConfigurationProfileId = '00000000-0000-0000-0000-000000000000'
                              OR i.ConfigurationDecisionId = '00000000-0000-0000-0000-000000000000'
                              OR i.PreparedByUserId = '00000000-0000-0000-0000-000000000000'
                              OR NULLIF(LTRIM(RTRIM(i.SourceReference)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.SourceVariant)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.EventReference)), '') IS NULL
                              OR i.EventReference <> i.SourceReference
                              OR NULLIF(LTRIM(RTRIM(i.MappingKey)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.ExternalEventCode)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.ReferenceField)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.PayloadContentType)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.AcknowledgementContentType)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.AcknowledgementPermissionCode)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.ReconciliationPermissionCode)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.ConfigurationProfileCode)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.ExchangeProfileCode)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.Frequency)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.Owner)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.AcknowledgementRule)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.ReconciliationRule)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.CorrelationId)), '') IS NULL
                              OR NULLIF(LTRIM(RTRIM(i.PreparedByName)), '') IS NULL
                              OR i.RequestFingerprint COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                              OR i.SourceIntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                              OR i.ConfigurationValueHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                              OR i.MappingIntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                              OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                              OR NOT (
                                  (i.Direction = 0 AND i.Status = 0)
                                  OR (i.Direction = 1 AND i.AcknowledgementRequired = 0 AND i.Status = 1)
                                  OR (i.Direction = 1 AND i.AcknowledgementRequired = 1 AND i.Status = 2)
                              )
                              OR NOT EXISTS (
                                  SELECT 1
                                  FROM [dbo].[ProcurementConfigurationProfiles] p
                                  JOIN [dbo].[ProcurementConfigurationDecisions] d
                                    ON d.Id = i.ConfigurationDecisionId
                                   AND d.ProfileId = p.Id
                                   AND d.TenantId = p.TenantId
                                  WHERE p.Id = i.ConfigurationProfileId
                                    AND p.TenantId = i.TenantId
                                    AND p.ProfileCode = i.ConfigurationProfileCode
                                    AND p.Version = i.ConfigurationProfileVersion
                                    AND p.LifecycleStatus = 1
                                    AND p.PublishedAt IS NOT NULL
                                    AND p.IsDeleted = 0
                                    AND p.EffectiveFrom <= i.PreparedAtUtc
                                    AND (p.EffectiveTo IS NULL OR p.EffectiveTo >= i.PreparedAtUtc)
                                    AND d.DecisionKey = 'DEC-009'
                                    AND d.SchemaVersion = i.ConfigurationDecisionSchemaVersion
                                    AND d.Status = 2
                                    AND d.ApprovalStatus = 1
                                    AND d.EvidenceStatus <> 0
                                    AND d.DecisionDate IS NOT NULL
                                     AND d.IsDeleted = 0
                                     AND (d.EffectiveFrom IS NULL OR d.EffectiveFrom <= i.PreparedAtUtc)
                                     AND (d.EffectiveTo IS NULL OR d.EffectiveTo >= i.PreparedAtUtc)
                                     AND DATALENGTH(i.ConfigurationValueJson) = DATALENGTH(d.ValueJson)
                                     AND i.ConfigurationValueJson COLLATE Latin1_General_100_BIN2 =
                                         d.ValueJson COLLATE Latin1_General_100_BIN2
                                     AND LTRIM(RTRIM(COALESCE(
                                             JSON_VALUE(i.ConfigurationValueJson, '$.profileCode'),
                                             JSON_VALUE(i.ConfigurationValueJson, '$.ProfileCode')))) =
                                         i.ExchangeProfileCode
                                     AND LTRIM(RTRIM(COALESCE(
                                             JSON_VALUE(i.ConfigurationValueJson, '$.frequency'),
                                             JSON_VALUE(i.ConfigurationValueJson, '$.Frequency')))) =
                                         i.Frequency
                                     AND LTRIM(RTRIM(COALESCE(
                                             JSON_VALUE(i.ConfigurationValueJson, '$.owner'),
                                             JSON_VALUE(i.ConfigurationValueJson, '$.Owner')))) =
                                         i.Owner
                                     AND LTRIM(RTRIM(COALESCE(
                                             JSON_VALUE(i.ConfigurationValueJson, '$.acknowledgementRule'),
                                             JSON_VALUE(i.ConfigurationValueJson, '$.AcknowledgementRule')))) =
                                         i.AcknowledgementRule
                                     AND LTRIM(RTRIM(COALESCE(
                                             JSON_VALUE(i.ConfigurationValueJson, '$.reconciliationRule'),
                                             JSON_VALUE(i.ConfigurationValueJson, '$.ReconciliationRule')))) =
                                         i.ReconciliationRule
                                     AND COALESCE(
                                             NULLIF(COALESCE(
                                                 TRY_CONVERT(datetime2(7), COALESCE(
                                                     JSON_VALUE(i.ConfigurationValueJson, '$.effectiveFrom'),
                                                     JSON_VALUE(i.ConfigurationValueJson, '$.EffectiveFrom')), 127),
                                                 CONVERT(datetime2(7), TRY_CONVERT(datetimeoffset(7),
                                                     COALESCE(
                                                         JSON_VALUE(i.ConfigurationValueJson, '$.effectiveFrom'),
                                                         JSON_VALUE(i.ConfigurationValueJson, '$.EffectiveFrom')),
                                                     127))),
                                                 CONVERT(datetime2(7), '0001-01-01T00:00:00')),
                                             d.EffectiveFrom,
                                             p.EffectiveFrom) = i.ConfigurationEffectiveFromUtc
                                     AND (
                                         (i.ConfigurationEffectiveToUtc IS NULL AND COALESCE(
                                             TRY_CONVERT(datetime2(7), COALESCE(
                                                 JSON_VALUE(i.ConfigurationValueJson, '$.effectiveTo'),
                                                 JSON_VALUE(i.ConfigurationValueJson, '$.EffectiveTo')), 127),
                                             CONVERT(datetime2(7), TRY_CONVERT(datetimeoffset(7),
                                                 COALESCE(
                                                     JSON_VALUE(i.ConfigurationValueJson, '$.effectiveTo'),
                                                     JSON_VALUE(i.ConfigurationValueJson, '$.EffectiveTo')),
                                                 127)),
                                             d.EffectiveTo,
                                             p.EffectiveTo) IS NULL)
                                         OR i.ConfigurationEffectiveToUtc = COALESCE(
                                             TRY_CONVERT(datetime2(7), COALESCE(
                                                 JSON_VALUE(i.ConfigurationValueJson, '$.effectiveTo'),
                                                 JSON_VALUE(i.ConfigurationValueJson, '$.EffectiveTo')), 127),
                                             CONVERT(datetime2(7), TRY_CONVERT(datetimeoffset(7),
                                                 COALESCE(
                                                     JSON_VALUE(i.ConfigurationValueJson, '$.effectiveTo'),
                                                     JSON_VALUE(i.ConfigurationValueJson, '$.EffectiveTo')),
                                                 127)),
                                             d.EffectiveTo,
                                             p.EffectiveTo)
                                     )
                                     AND (
                                         (p.IsDefault = 1 AND 1 = (
                                             SELECT COUNT(*)
                                             FROM [dbo].[ProcurementConfigurationDecisions] selectedDecision
                                             JOIN [dbo].[ProcurementConfigurationProfiles] selectedProfile
                                               ON selectedProfile.Id = selectedDecision.ProfileId
                                              AND selectedProfile.TenantId = selectedDecision.TenantId
                                             WHERE selectedDecision.TenantId = i.TenantId
                                               AND selectedDecision.DecisionKey = 'DEC-009'
                                               AND selectedDecision.Status = 2
                                               AND selectedDecision.ApprovalStatus = 1
                                               AND selectedDecision.EvidenceStatus <> 0
                                               AND selectedDecision.DecisionDate IS NOT NULL
                                               AND selectedDecision.IsDeleted = 0
                                               AND (selectedDecision.EffectiveFrom IS NULL
                                                    OR selectedDecision.EffectiveFrom <= i.PreparedAtUtc)
                                               AND (selectedDecision.EffectiveTo IS NULL
                                                    OR selectedDecision.EffectiveTo >= i.PreparedAtUtc)
                                               AND selectedProfile.LifecycleStatus = 1
                                               AND selectedProfile.PublishedAt IS NOT NULL
                                               AND selectedProfile.IsDeleted = 0
                                               AND selectedProfile.IsDefault = 1
                                               AND selectedProfile.EffectiveFrom <= i.PreparedAtUtc
                                               AND (selectedProfile.EffectiveTo IS NULL
                                                    OR selectedProfile.EffectiveTo >= i.PreparedAtUtc)
                                         ))
                                         OR (p.IsDefault = 0
                                             AND 0 = (
                                                 SELECT COUNT(*)
                                                 FROM [dbo].[ProcurementConfigurationDecisions] defaultDecision
                                                 JOIN [dbo].[ProcurementConfigurationProfiles] defaultProfile
                                                   ON defaultProfile.Id = defaultDecision.ProfileId
                                                  AND defaultProfile.TenantId = defaultDecision.TenantId
                                                 WHERE defaultDecision.TenantId = i.TenantId
                                                   AND defaultDecision.DecisionKey = 'DEC-009'
                                                   AND defaultDecision.Status = 2
                                                   AND defaultDecision.ApprovalStatus = 1
                                                   AND defaultDecision.EvidenceStatus <> 0
                                                   AND defaultDecision.DecisionDate IS NOT NULL
                                                   AND defaultDecision.IsDeleted = 0
                                                   AND (defaultDecision.EffectiveFrom IS NULL
                                                        OR defaultDecision.EffectiveFrom <= i.PreparedAtUtc)
                                                   AND (defaultDecision.EffectiveTo IS NULL
                                                        OR defaultDecision.EffectiveTo >= i.PreparedAtUtc)
                                                   AND defaultProfile.LifecycleStatus = 1
                                                   AND defaultProfile.PublishedAt IS NOT NULL
                                                   AND defaultProfile.IsDeleted = 0
                                                   AND defaultProfile.IsDefault = 1
                                                   AND defaultProfile.EffectiveFrom <= i.PreparedAtUtc
                                                   AND (defaultProfile.EffectiveTo IS NULL
                                                        OR defaultProfile.EffectiveTo >= i.PreparedAtUtc)
                                             )
                                             AND 1 = (
                                                 SELECT COUNT(*)
                                                 FROM [dbo].[ProcurementConfigurationDecisions] soleDecision
                                                 JOIN [dbo].[ProcurementConfigurationProfiles] soleProfile
                                                   ON soleProfile.Id = soleDecision.ProfileId
                                                  AND soleProfile.TenantId = soleDecision.TenantId
                                                 WHERE soleDecision.TenantId = i.TenantId
                                                   AND soleDecision.DecisionKey = 'DEC-009'
                                                   AND soleDecision.Status = 2
                                                   AND soleDecision.ApprovalStatus = 1
                                                   AND soleDecision.EvidenceStatus <> 0
                                                   AND soleDecision.DecisionDate IS NOT NULL
                                                   AND soleDecision.IsDeleted = 0
                                                   AND (soleDecision.EffectiveFrom IS NULL
                                                        OR soleDecision.EffectiveFrom <= i.PreparedAtUtc)
                                                   AND (soleDecision.EffectiveTo IS NULL
                                                        OR soleDecision.EffectiveTo >= i.PreparedAtUtc)
                                                   AND soleProfile.LifecycleStatus = 1
                                                   AND soleProfile.PublishedAt IS NOT NULL
                                                   AND soleProfile.IsDeleted = 0
                                                   AND soleProfile.EffectiveFrom <= i.PreparedAtUtc
                                                   AND (soleProfile.EffectiveTo IS NULL
                                                        OR soleProfile.EffectiveTo >= i.PreparedAtUtc)
                                             ))
                                     )
                                     AND EXISTS (
                                         SELECT 1
                                         FROM [dbo].[ProcurementConfigurationEvidenceLinks] e
                                        WHERE e.TenantId = i.TenantId
                                          AND e.ProfileId = i.ConfigurationProfileId
                                           AND e.DecisionId = i.ConfigurationDecisionId
                                           AND e.IsDeleted = 0
                                     )
                                     AND 1 = (
                                         SELECT COUNT(*)
                                         FROM OPENJSON(COALESCE(
                                             JSON_QUERY(d.ValueJson, '$.fileTemplateMappings'),
                                             JSON_QUERY(d.ValueJson, '$.FileTemplateMappings'))) mappingKeyEntry
                                         WHERE mappingKeyEntry.[type] = 1
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(mappingKeyEntry.[value], '$.mappingKey'),
                                               JSON_VALUE(mappingKeyEntry.[value], '$.MappingKey'))))
                                               COLLATE Latin1_General_100_CI_AS =
                                               i.MappingKey COLLATE Latin1_General_100_CI_AS
                                     )
                                     AND EXISTS (
                                         SELECT 1
                                         FROM OPENJSON(COALESCE(
                                             JSON_QUERY(d.ValueJson, '$.fileTemplateMappings'),
                                             JSON_QUERY(d.ValueJson, '$.FileTemplateMappings'))) configuredMapping
                                         WHERE configuredMapping.[type] = 1
                                           AND ISJSON(configuredMapping.[value]) = 1
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.mappingKey'),
                                               JSON_VALUE(configuredMapping.[value], '$.MappingKey'))))
                                               COLLATE Latin1_General_100_CI_AS =
                                               i.MappingKey COLLATE Latin1_General_100_CI_AS
                                           AND CASE LOWER(COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.eventFamily'),
                                                   JSON_VALUE(configuredMapping.[value], '$.EventFamily')))
                                               WHEN 'tenderpublication' THEN 0
                                               WHEN 'tenderreference' THEN 1
                                               WHEN 'awardnotification' THEN 2
                                               ELSE TRY_CONVERT(int, COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.eventFamily'),
                                                   JSON_VALUE(configuredMapping.[value], '$.EventFamily')))
                                               END = i.EventFamily
                                           AND CASE LOWER(COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.direction'),
                                                   JSON_VALUE(configuredMapping.[value], '$.Direction')))
                                               WHEN 'export' THEN 0
                                               WHEN 'import' THEN 1
                                               WHEN 'bidirectional' THEN 2
                                               ELSE TRY_CONVERT(int, COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.direction'),
                                                   JSON_VALUE(configuredMapping.[value], '$.Direction')))
                                               END IN (i.Direction, 2)
                                           AND EXISTS (
                                               SELECT 1
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceTypes'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceTypes'))) configuredSourceType
                                               WHERE CASE LOWER(CONVERT(nvarchar(100), configuredSourceType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int, configuredSourceType.[value])
                                               END = i.SourceType
                                           )
                                           AND (
                                               NOT EXISTS (
                                                   SELECT 1
                                                   FROM OPENJSON(COALESCE(
                                                       JSON_QUERY(configuredMapping.[value], '$.sourceVariants'),
                                                       JSON_QUERY(configuredMapping.[value], '$.SourceVariants')))
                                               )
                                               OR EXISTS (
                                                   SELECT 1
                                                   FROM OPENJSON(COALESCE(
                                                       JSON_QUERY(configuredMapping.[value], '$.sourceVariants'),
                                                       JSON_QUERY(configuredMapping.[value], '$.SourceVariants'))) configuredVariant
                                                   WHERE CONVERT(nvarchar(50), configuredVariant.[value])
                                                       COLLATE Latin1_General_100_CI_AS =
                                                       i.SourceVariant COLLATE Latin1_General_100_CI_AS
                                               )
                                           )
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.externalEventCode'),
                                               JSON_VALUE(configuredMapping.[value], '$.ExternalEventCode')))) =
                                               i.ExternalEventCode
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.referenceField'),
                                               JSON_VALUE(configuredMapping.[value], '$.ReferenceField')))) =
                                               i.ReferenceField
                                           AND LOWER(LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.payloadContentType'),
                                               JSON_VALUE(configuredMapping.[value], '$.PayloadContentType'))))) =
                                               i.PayloadContentType
                                           AND LOWER(LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.acknowledgementContentType'),
                                               JSON_VALUE(configuredMapping.[value], '$.AcknowledgementContentType'))))) =
                                               i.AcknowledgementContentType
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.acknowledgementPermissionCode'),
                                               JSON_VALUE(configuredMapping.[value], '$.AcknowledgementPermissionCode')))) =
                                               i.AcknowledgementPermissionCode
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.reconciliationPermissionCode'),
                                               JSON_VALUE(configuredMapping.[value], '$.ReconciliationPermissionCode')))) =
                                               i.ReconciliationPermissionCode
                                           AND COALESCE(CASE LOWER(COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.acknowledgementRequired'),
                                                   JSON_VALUE(configuredMapping.[value], '$.AcknowledgementRequired')))
                                               WHEN 'true' THEN 1 WHEN 'false' THEN 0
                                               ELSE TRY_CONVERT(bit, COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.acknowledgementRequired'),
                                                   JSON_VALUE(configuredMapping.[value], '$.AcknowledgementRequired')))
                                               END, 1) = i.AcknowledgementRequired
                                           AND COALESCE(CASE LOWER(COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.reconciliationRequired'),
                                                   JSON_VALUE(configuredMapping.[value], '$.ReconciliationRequired')))
                                               WHEN 'true' THEN 1 WHEN 'false' THEN 0
                                               ELSE TRY_CONVERT(bit, COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.reconciliationRequired'),
                                                   JSON_VALUE(configuredMapping.[value], '$.ReconciliationRequired')))
                                               END, 1) = i.ReconciliationRequired
                                           AND COALESCE(TRY_CONVERT(int, COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.maximumRetryAttempts'),
                                               JSON_VALUE(configuredMapping.[value], '$.MaximumRetryAttempts'))), 3) =
                                               i.MaximumRetryAttempts
                                           AND JSON_VALUE(i.MappingSnapshotJson, '$.mappingKey')
                                               COLLATE Latin1_General_100_CI_AS =
                                               i.MappingKey COLLATE Latin1_General_100_CI_AS
                                           AND CASE LOWER(JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.eventFamily'))
                                               WHEN 'tenderpublication' THEN 0
                                               WHEN 'tenderreference' THEN 1
                                               WHEN 'awardnotification' THEN 2
                                               ELSE TRY_CONVERT(int, JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.eventFamily'))
                                               END = i.EventFamily
                                           AND CASE LOWER(JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.direction'))
                                               WHEN 'export' THEN 0
                                               WHEN 'import' THEN 1
                                               WHEN 'bidirectional' THEN 2
                                               ELSE TRY_CONVERT(int, JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.direction'))
                                               END = CASE LOWER(COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.direction'),
                                                   JSON_VALUE(configuredMapping.[value], '$.Direction')))
                                               WHEN 'export' THEN 0
                                               WHEN 'import' THEN 1
                                               WHEN 'bidirectional' THEN 2
                                               ELSE TRY_CONVERT(int, COALESCE(
                                                   JSON_VALUE(configuredMapping.[value], '$.direction'),
                                                   JSON_VALUE(configuredMapping.[value], '$.Direction')))
                                               END
                                           AND LTRIM(RTRIM(JSON_VALUE(
                                               i.MappingSnapshotJson, '$.externalEventCode'))) =
                                               i.ExternalEventCode
                                           AND LTRIM(RTRIM(JSON_VALUE(
                                               i.MappingSnapshotJson, '$.referenceField'))) =
                                               i.ReferenceField
                                           AND JSON_VALUE(i.MappingSnapshotJson, '$.payloadContentType') =
                                               i.PayloadContentType
                                           AND JSON_VALUE(i.MappingSnapshotJson, '$.acknowledgementContentType') =
                                               i.AcknowledgementContentType
                                           AND LTRIM(RTRIM(JSON_VALUE(
                                               i.MappingSnapshotJson, '$.acknowledgementPermissionCode'))) =
                                               i.AcknowledgementPermissionCode
                                           AND LTRIM(RTRIM(JSON_VALUE(
                                               i.MappingSnapshotJson, '$.reconciliationPermissionCode'))) =
                                               i.ReconciliationPermissionCode
                                           AND CASE LOWER(JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.acknowledgementRequired'))
                                               WHEN 'true' THEN 1 WHEN 'false' THEN 0
                                               ELSE TRY_CONVERT(bit, JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.acknowledgementRequired'))
                                               END = i.AcknowledgementRequired
                                           AND CASE LOWER(JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.reconciliationRequired'))
                                               WHEN 'true' THEN 1 WHEN 'false' THEN 0
                                               ELSE TRY_CONVERT(bit, JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.reconciliationRequired'))
                                               END = i.ReconciliationRequired
                                           AND TRY_CONVERT(int, JSON_VALUE(
                                               i.MappingSnapshotJson, '$.maximumRetryAttempts')) =
                                               i.MaximumRetryAttempts
                                           AND 17 = (
                                               SELECT COUNT(*)
                                               FROM OPENJSON(i.MappingSnapshotJson)
                                           )
                                           AND NOT EXISTS (
                                               SELECT 1
                                               FROM OPENJSON(i.MappingSnapshotJson) snapshotProperty
                                               WHERE snapshotProperty.[key]
                                                   COLLATE Latin1_General_100_CI_AS NOT IN (
                                                       'mappingKey', 'eventFamily', 'direction',
                                                       'sourceTypes', 'sourceVariants',
                                                       'externalEventCode', 'templateReference',
                                                       'schemaReference', 'payloadVersion',
                                                       'referenceField', 'payloadContentType',
                                                       'acknowledgementContentType',
                                                       'acknowledgementPermissionCode',
                                                       'reconciliationPermissionCode',
                                                       'acknowledgementRequired',
                                                       'reconciliationRequired',
                                                       'maximumRetryAttempts')
                                           )
                                           AND (
                                               SELECT COUNT(*)
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceTypes'))
                                           ) = (
                                               SELECT COUNT(DISTINCT CASE
                                                   LOWER(CONVERT(nvarchar(100),
                                                       uniqueSnapshotType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int,
                                                       uniqueSnapshotType.[value]) END)
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceTypes'))
                                                   uniqueSnapshotType
                                           )
                                           AND (
                                               SELECT COUNT(*)
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceVariants'))
                                           ) = (
                                               SELECT COUNT(DISTINCT CONVERT(
                                                   nvarchar(50), uniqueSnapshotVariant.[value])
                                                   COLLATE Latin1_General_100_CI_AS)
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceVariants'))
                                                   uniqueSnapshotVariant
                                           )
                                           AND i.PayloadContentType IN (
                                               'application/json', 'text/csv', 'application/csv',
                                               'application/xml', 'text/xml', 'text/plain')
                                           AND i.AcknowledgementContentType IN (
                                               'application/json', 'text/csv', 'application/csv',
                                               'application/xml', 'text/xml', 'text/plain')
                                           AND NULLIF(LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.templateReference'),
                                               JSON_VALUE(configuredMapping.[value], '$.TemplateReference')))),
                                               '') IS NOT NULL
                                           AND NULLIF(LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.schemaReference'),
                                               JSON_VALUE(configuredMapping.[value], '$.SchemaReference')))),
                                               '') IS NOT NULL
                                           AND NULLIF(LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.payloadVersion'),
                                               JSON_VALUE(configuredMapping.[value], '$.PayloadVersion')))),
                                               '') IS NOT NULL
                                           AND NOT EXISTS (
                                               SELECT 1
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceTypes'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceTypes'))) validType
                                               WHERE COALESCE(CASE
                                                   LOWER(CONVERT(nvarchar(100), validType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int, validType.[value])
                                               END, -1) NOT BETWEEN 0 AND 2
                                           )
                                           AND (
                                               SELECT COUNT(*)
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceTypes'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceTypes')))
                                           ) = (
                                               SELECT COUNT(DISTINCT CASE
                                                   LOWER(CONVERT(nvarchar(100), uniqueType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int, uniqueType.[value]) END)
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceTypes'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceTypes')))
                                                   uniqueType
                                           )
                                           AND NOT EXISTS (
                                               SELECT 1
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceVariants'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceVariants')))
                                                   validVariant
                                               WHERE NULLIF(LTRIM(RTRIM(CONVERT(
                                                   nvarchar(50), validVariant.[value]))), '') IS NULL
                                           )
                                           AND (
                                               SELECT COUNT(*)
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceVariants'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceVariants')))
                                           ) = (
                                               SELECT COUNT(DISTINCT CONVERT(
                                                   nvarchar(50), uniqueVariant.[value])
                                                   COLLATE Latin1_General_100_CI_AS)
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceVariants'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceVariants')))
                                                   uniqueVariant
                                           )
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.templateReference'),
                                               JSON_VALUE(configuredMapping.[value], '$.TemplateReference')))) =
                                               LTRIM(RTRIM(JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.templateReference')))
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.schemaReference'),
                                               JSON_VALUE(configuredMapping.[value], '$.SchemaReference')))) =
                                               LTRIM(RTRIM(JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.schemaReference')))
                                           AND LTRIM(RTRIM(COALESCE(
                                               JSON_VALUE(configuredMapping.[value], '$.payloadVersion'),
                                               JSON_VALUE(configuredMapping.[value], '$.PayloadVersion')))) =
                                               LTRIM(RTRIM(JSON_VALUE(
                                                   i.MappingSnapshotJson, '$.payloadVersion')))
                                           AND NOT EXISTS (
                                               SELECT CASE LOWER(CONVERT(nvarchar(100), rawType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int, rawType.[value]) END
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceTypes'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceTypes'))) rawType
                                               EXCEPT
                                               SELECT CASE LOWER(CONVERT(nvarchar(100), snapshotType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int, snapshotType.[value]) END
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceTypes')) snapshotType
                                           )
                                           AND NOT EXISTS (
                                               SELECT CASE LOWER(CONVERT(nvarchar(100), snapshotType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int, snapshotType.[value]) END
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceTypes')) snapshotType
                                               EXCEPT
                                               SELECT CASE LOWER(CONVERT(nvarchar(100), rawType.[value]))
                                                   WHEN 'requestforquotation' THEN 0
                                                   WHEN 'tender' THEN 1
                                                   WHEN 'exceptionalsourcing' THEN 2
                                                   ELSE TRY_CONVERT(int, rawType.[value]) END
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceTypes'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceTypes'))) rawType
                                           )
                                           AND NOT EXISTS (
                                               SELECT CONVERT(nvarchar(50), rawVariant.[value])
                                                   COLLATE Latin1_General_100_CI_AS
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceVariants'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceVariants'))) rawVariant
                                               EXCEPT
                                               SELECT CONVERT(nvarchar(50), snapshotVariant.[value])
                                                   COLLATE Latin1_General_100_CI_AS
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceVariants')) snapshotVariant
                                           )
                                           AND NOT EXISTS (
                                               SELECT CONVERT(nvarchar(50), snapshotVariant.[value])
                                                   COLLATE Latin1_General_100_CI_AS
                                               FROM OPENJSON(JSON_QUERY(
                                                   i.MappingSnapshotJson, '$.sourceVariants')) snapshotVariant
                                               EXCEPT
                                               SELECT CONVERT(nvarchar(50), rawVariant.[value])
                                                   COLLATE Latin1_General_100_CI_AS
                                               FROM OPENJSON(COALESCE(
                                                   JSON_QUERY(configuredMapping.[value], '$.sourceVariants'),
                                                   JSON_QUERY(configuredMapping.[value], '$.SourceVariants'))) rawVariant
                                           )
                                     )
                               )
                              OR (i.SourceType = 0 AND (
                                  i.SourceVariant <> 'RequestForQuotation'
                                  OR NOT EXISTS (
                                      SELECT 1 FROM [dbo].[RequestForQuotations] r
                                      WHERE r.Id = i.SourceId
                                        AND r.TenantId = i.TenantId
                                        AND r.RfqNumber = i.SourceReference
                                        AND r.IsDeleted = 0
                                        AND TRY_CONVERT(uniqueidentifier,
                                            JSON_VALUE(i.SourceSnapshotJson, '$.id')) = r.Id
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.rfqNumber') =
                                            r.RfqNumber
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.status') =
                                            r.Status
                                        AND (
                                            (i.EventFamily IN (0, 1)
                                             AND r.Status IN ('Sent', 'Closed', 'Awarded')
                                             AND r.SentAt IS NOT NULL
                                             AND i.SourceOccurredAtUtc = r.SentAt)
                                            OR
                                            (i.EventFamily = 2
                                             AND r.Status = 'Awarded'
                                             AND r.AwardedAt IS NOT NULL
                                             AND i.SourceOccurredAtUtc = r.AwardedAt
                                             AND EXISTS (
                                                 SELECT 1
                                                 FROM [dbo].[RequestForQuotationAwardLines] awardLine
                                                 WHERE awardLine.TenantId = i.TenantId
                                                   AND awardLine.RfqId = r.Id
                                                   AND awardLine.IsDeleted = 0
                                             )
                                             AND EXISTS (
                                                 SELECT 1
                                                 FROM [dbo].[ProcurementAwardReadinessDecisions] readiness
                                                 JOIN [dbo].[ProcurementBidderCommunicationRegisters] communication
                                                   ON communication.TenantId = readiness.TenantId
                                                  AND communication.SourceType = readiness.SourceType
                                                  AND communication.SourceId = readiness.SourceId
                                                  AND communication.AwardReadinessDecisionId = readiness.Id
                                                  AND communication.AwardReadinessDecisionSequence =
                                                      readiness.DecisionSequence
                                                  AND communication.AwardReadinessIntegrityHash =
                                                      readiness.IntegrityHash
                                                  AND communication.AwardReadinessSourceIntegrityHash =
                                                      readiness.SourceIntegrityHash
                                                 WHERE readiness.TenantId = i.TenantId
                                                   AND readiness.SourceType = 0
                                                   AND readiness.SourceId = r.Id
                                                   AND readiness.SourceReference = r.RfqNumber
                                                   AND readiness.IsDeleted = 0
                                                   AND readiness.Status = 1
                                                   AND readiness.DecisionSequence = (
                                                       SELECT MAX(latest.DecisionSequence)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] latest
                                                       WHERE latest.TenantId = i.TenantId
                                                         AND latest.SourceType = 0
                                                         AND latest.SourceId = r.Id
                                                         AND latest.IsDeleted = 0
                                                   )
                                                   AND readiness.DecisionSequence = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] contiguous
                                                       WHERE contiguous.TenantId = i.TenantId
                                                         AND contiguous.SourceType = 0
                                                         AND contiguous.SourceId = r.Id
                                                         AND contiguous.IsDeleted = 0
                                                   )
                                                   AND readiness.SourceIntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND readiness.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND communication.SourceReference = r.RfqNumber
                                                   AND communication.AwardFamily = 0
                                                   AND communication.AwardId = r.Id
                                                   AND communication.AwardReference =
                                                       CONCAT(r.RfqNumber, '-AWARD')
                                                   AND communication.AwardedAtUtc = r.AwardedAt
                                                   AND communication.IsDeleted = 0
                                                   AND communication.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND 1 = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementBidderCommunicationRegisters]
                                                           uniqueCommunication
                                                       WHERE uniqueCommunication.TenantId = i.TenantId
                                                         AND uniqueCommunication.SourceType = 0
                                                         AND uniqueCommunication.SourceId = r.Id
                                                         AND uniqueCommunication.IsDeleted = 0
                                                   )
                                             ))
                                        )
                                  )
                              ))
                              OR (i.SourceType = 1 AND (
                                  i.SourceVariant NOT IN ('FormalTender', 'LegacyTender')
                                  OR NOT EXISTS (
                                      SELECT 1 FROM [dbo].[Tenders] t
                                      WHERE t.Id = i.SourceId
                                        AND t.TenantId = i.TenantId
                                        AND t.TenderNumber = i.SourceReference
                                        AND t.IsDeleted = 0
                                        AND TRY_CONVERT(uniqueidentifier,
                                            JSON_VALUE(i.SourceSnapshotJson, '$.id')) = t.Id
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.tenderNumber') =
                                            t.TenderNumber
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.status') =
                                            t.Status
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.variant') =
                                            i.SourceVariant
                                  )
                                  OR EXISTS (
                                      SELECT 1 FROM [dbo].[ProcurementExceptionalSourcingControls] x
                                      WHERE x.TenderId = i.SourceId
                                        AND x.TenantId = i.TenantId
                                        AND x.IsDeleted = 0
                                  )
                                  OR (i.SourceVariant = 'FormalTender' AND NOT EXISTS (
                                      SELECT 1
                                      FROM [dbo].[ProcurementTenderControls] c
                                      JOIN [dbo].[Tenders] formalTender
                                        ON formalTender.Id = c.TenderId
                                       AND formalTender.TenantId = c.TenantId
                                      WHERE c.TenderId = i.SourceId
                                        AND c.TenantId = i.TenantId
                                        AND c.IsDeleted = 0
                                        AND formalTender.IsDeleted = 0
                                        AND (
                                            (i.EventFamily IN (0, 1)
                                             AND c.Status >= 0
                                             AND c.AdvertisedAtUtc <> CONVERT(datetime2, '0001-01-01T00:00:00')
                                             AND i.SourceOccurredAtUtc = c.AdvertisedAtUtc
                                             AND NULLIF(LTRIM(RTRIM(c.AdvertisementReference)), '') IS NOT NULL
                                             AND NULLIF(LTRIM(RTRIM(
                                                 c.AdvertisementEvidenceReference)), '') IS NOT NULL
                                             AND c.IntegrityHash COLLATE Latin1_General_100_BIN2
                                                 NOT LIKE '%[^0-9A-F]%'
                                             AND formalTender.Status IN ('Published', 'Closed', 'Awarded'))
                                            OR
                                            (i.EventFamily = 2
                                             AND c.Status >= 7
                                             AND c.AwardedAtUtc IS NOT NULL
                                             AND i.SourceOccurredAtUtc = c.AwardedAtUtc
                                             AND c.AwardBidId IS NOT NULL
                                             AND NULLIF(LTRIM(RTRIM(c.AwardReference)), '') IS NOT NULL
                                             AND EXISTS (
                                                 SELECT 1
                                                 FROM [dbo].[ProcurementAwardReadinessDecisions] readiness
                                                 JOIN [dbo].[ProcurementBidderCommunicationRegisters] communication
                                                   ON communication.TenantId = readiness.TenantId
                                                  AND communication.SourceType = readiness.SourceType
                                                  AND communication.SourceId = readiness.SourceId
                                                  AND communication.AwardReadinessDecisionId = readiness.Id
                                                  AND communication.AwardReadinessDecisionSequence =
                                                      readiness.DecisionSequence
                                                  AND communication.AwardReadinessIntegrityHash =
                                                      readiness.IntegrityHash
                                                  AND communication.AwardReadinessSourceIntegrityHash =
                                                      readiness.SourceIntegrityHash
                                                 WHERE readiness.TenantId = i.TenantId
                                                   AND readiness.SourceType = 1
                                                   AND readiness.SourceId = formalTender.Id
                                                   AND readiness.SourceReference =
                                                       formalTender.TenderNumber
                                                   AND readiness.IsDeleted = 0
                                                   AND readiness.Status = 1
                                                   AND readiness.DecisionSequence = (
                                                       SELECT MAX(latest.DecisionSequence)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] latest
                                                       WHERE latest.TenantId = i.TenantId
                                                         AND latest.SourceType = 1
                                                         AND latest.SourceId = formalTender.Id
                                                         AND latest.IsDeleted = 0
                                                   )
                                                   AND readiness.DecisionSequence = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] contiguous
                                                       WHERE contiguous.TenantId = i.TenantId
                                                         AND contiguous.SourceType = 1
                                                         AND contiguous.SourceId = formalTender.Id
                                                         AND contiguous.IsDeleted = 0
                                                   )
                                                   AND readiness.SourceIntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND readiness.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND communication.SourceReference =
                                                       formalTender.TenderNumber
                                                   AND communication.AwardFamily = 1
                                                   AND communication.AwardId = c.Id
                                                   AND communication.AwardReference = c.AwardReference
                                                   AND communication.AwardedAtUtc = c.AwardedAtUtc
                                                   AND communication.IsDeleted = 0
                                                   AND communication.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND 1 = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementBidderCommunicationRegisters]
                                                           uniqueCommunication
                                                       WHERE uniqueCommunication.TenantId = i.TenantId
                                                         AND uniqueCommunication.SourceType = 1
                                                         AND uniqueCommunication.SourceId =
                                                             formalTender.Id
                                                         AND uniqueCommunication.IsDeleted = 0
                                                   )
                                             ))
                                        )
                                  ))
                                  OR (i.SourceVariant = 'LegacyTender' AND EXISTS (
                                      SELECT 1 FROM [dbo].[ProcurementTenderControls] c
                                      WHERE c.TenderId = i.SourceId
                                        AND c.TenantId = i.TenantId
                                        AND c.IsDeleted = 0
                                  ))
                                  OR (i.SourceVariant = 'LegacyTender' AND NOT EXISTS (
                                      SELECT 1
                                      FROM [dbo].[Tenders] legacyTender
                                      WHERE legacyTender.Id = i.SourceId
                                        AND legacyTender.TenantId = i.TenantId
                                        AND legacyTender.IsDeleted = 0
                                        AND (
                                            (i.EventFamily IN (0, 1)
                                             AND legacyTender.Status IN ('Published', 'Closed', 'Awarded')
                                             AND i.SourceOccurredAtUtc = COALESCE(
                                                 legacyTender.PublishDate,
                                                 legacyTender.CreatedAt))
                                            OR
                                            (i.EventFamily = 2
                                             AND legacyTender.Status = 'Awarded'
                                             AND i.SourceOccurredAtUtc = COALESCE(
                                                 legacyTender.AwardDate,
                                                 (SELECT MAX(retained.AwardDate)
                                                  FROM [dbo].[TenderAwards] retained
                                                  WHERE retained.TenantId = i.TenantId
                                                    AND retained.TenderId = legacyTender.Id
                                                    AND retained.Status <> 'Cancelled'
                                                    AND retained.IsDeleted = 0))
                                             AND EXISTS (
                                                 SELECT 1
                                                 FROM [dbo].[TenderAwards] retainedAward
                                                 WHERE retainedAward.TenantId = i.TenantId
                                                   AND retainedAward.TenderId = legacyTender.Id
                                                   AND retainedAward.Status <> 'Cancelled'
                                                   AND retainedAward.IsDeleted = 0
                                             )
                                             AND EXISTS (
                                                 SELECT 1
                                                 FROM [dbo].[ProcurementAwardReadinessDecisions] readiness
                                                 JOIN [dbo].[ProcurementBidderCommunicationRegisters] communication
                                                   ON communication.TenantId = readiness.TenantId
                                                  AND communication.SourceType = readiness.SourceType
                                                  AND communication.SourceId = readiness.SourceId
                                                  AND communication.AwardReadinessDecisionId = readiness.Id
                                                  AND communication.AwardReadinessDecisionSequence =
                                                      readiness.DecisionSequence
                                                  AND communication.AwardReadinessIntegrityHash =
                                                      readiness.IntegrityHash
                                                  AND communication.AwardReadinessSourceIntegrityHash =
                                                      readiness.SourceIntegrityHash
                                                 WHERE readiness.TenantId = i.TenantId
                                                   AND readiness.SourceType = 1
                                                   AND readiness.SourceId = legacyTender.Id
                                                   AND readiness.SourceReference =
                                                       legacyTender.TenderNumber
                                                   AND readiness.IsDeleted = 0
                                                   AND readiness.Status = 1
                                                   AND readiness.DecisionSequence = (
                                                       SELECT MAX(latest.DecisionSequence)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] latest
                                                       WHERE latest.TenantId = i.TenantId
                                                         AND latest.SourceType = 1
                                                         AND latest.SourceId = legacyTender.Id
                                                         AND latest.IsDeleted = 0
                                                   )
                                                   AND readiness.DecisionSequence = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] contiguous
                                                       WHERE contiguous.TenantId = i.TenantId
                                                         AND contiguous.SourceType = 1
                                                         AND contiguous.SourceId = legacyTender.Id
                                                         AND contiguous.IsDeleted = 0
                                                   )
                                                   AND readiness.SourceIntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND readiness.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND communication.SourceReference =
                                                       legacyTender.TenderNumber
                                                   AND communication.AwardFamily = 3
                                                   AND communication.AwardId = legacyTender.Id
                                                   AND communication.AwardReference =
                                                       CONCAT(legacyTender.TenderNumber,
                                                           '-LEGACY-AWARD')
                                                   AND communication.AwardedAtUtc = COALESCE(
                                                       legacyTender.AwardDate,
                                                       (SELECT MAX(retained.AwardDate)
                                                        FROM [dbo].[TenderAwards] retained
                                                        WHERE retained.TenantId = i.TenantId
                                                          AND retained.TenderId = legacyTender.Id
                                                          AND retained.Status <> 'Cancelled'
                                                          AND retained.IsDeleted = 0))
                                                   AND communication.IsDeleted = 0
                                                   AND communication.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND 1 = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementBidderCommunicationRegisters]
                                                           uniqueCommunication
                                                       WHERE uniqueCommunication.TenantId = i.TenantId
                                                         AND uniqueCommunication.SourceType = 1
                                                         AND uniqueCommunication.SourceId =
                                                             legacyTender.Id
                                                         AND uniqueCommunication.IsDeleted = 0
                                                   )
                                             ))
                                        )
                                  ))
                              ))
                              OR (i.SourceType = 2 AND (
                                  i.SourceVariant <> 'ExceptionalSourcing'
                                  OR NOT EXISTS (
                                      SELECT 1
                                      FROM [dbo].[Tenders] t
                                      JOIN [dbo].[ProcurementExceptionalSourcingControls] x
                                        ON x.TenderId = t.Id
                                       AND x.TenantId = t.TenantId
                                      WHERE t.Id = i.SourceId
                                        AND t.TenantId = i.TenantId
                                        AND t.TenderNumber = i.SourceReference
                                        AND t.IsDeleted = 0
                                        AND x.IsDeleted = 0
                                        AND TRY_CONVERT(uniqueidentifier,
                                            JSON_VALUE(i.SourceSnapshotJson, '$.id')) = t.Id
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.tenderNumber') =
                                            t.TenderNumber
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.status') =
                                            t.Status
                                        AND JSON_VALUE(i.SourceSnapshotJson, '$.variant') =
                                            'ExceptionalSourcing'
                                        AND (
                                            (i.EventFamily IN (0, 1)
                                             AND x.Status BETWEEN 2 AND 8
                                             AND i.SourceOccurredAtUtc = COALESCE(
                                                 x.ApprovedAtUtc, x.PreparedAtUtc))
                                            OR
                                            (i.EventFamily = 2
                                             AND x.Status BETWEEN 5 AND 8
                                             AND x.AwardedAtUtc IS NOT NULL
                                             AND i.SourceOccurredAtUtc = x.AwardedAtUtc
                                             AND x.AwardBidId IS NOT NULL
                                             AND NULLIF(LTRIM(RTRIM(x.AwardReference)), '') IS NOT NULL
                                             AND EXISTS (
                                                 SELECT 1
                                                 FROM [dbo].[ProcurementAwardReadinessDecisions] readiness
                                                 JOIN [dbo].[ProcurementBidderCommunicationRegisters] communication
                                                   ON communication.TenantId = readiness.TenantId
                                                  AND communication.SourceType = readiness.SourceType
                                                  AND communication.SourceId = readiness.SourceId
                                                  AND communication.AwardReadinessDecisionId = readiness.Id
                                                  AND communication.AwardReadinessDecisionSequence =
                                                      readiness.DecisionSequence
                                                  AND communication.AwardReadinessIntegrityHash =
                                                      readiness.IntegrityHash
                                                  AND communication.AwardReadinessSourceIntegrityHash =
                                                      readiness.SourceIntegrityHash
                                                 WHERE readiness.TenantId = i.TenantId
                                                   AND readiness.SourceType = 2
                                                   AND readiness.SourceId = t.Id
                                                   AND readiness.SourceReference = t.TenderNumber
                                                   AND readiness.IsDeleted = 0
                                                   AND readiness.Status = 1
                                                   AND readiness.DecisionSequence = (
                                                       SELECT MAX(latest.DecisionSequence)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] latest
                                                       WHERE latest.TenantId = i.TenantId
                                                         AND latest.SourceType = 2
                                                         AND latest.SourceId = t.Id
                                                         AND latest.IsDeleted = 0
                                                   )
                                                   AND readiness.DecisionSequence = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementAwardReadinessDecisions] contiguous
                                                       WHERE contiguous.TenantId = i.TenantId
                                                         AND contiguous.SourceType = 2
                                                         AND contiguous.SourceId = t.Id
                                                         AND contiguous.IsDeleted = 0
                                                   )
                                                   AND readiness.SourceIntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND readiness.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND communication.SourceReference = t.TenderNumber
                                                   AND communication.AwardFamily = 2
                                                   AND communication.AwardId = x.Id
                                                   AND communication.AwardReference = x.AwardReference
                                                   AND communication.AwardedAtUtc = x.AwardedAtUtc
                                                   AND communication.IsDeleted = 0
                                                   AND communication.IntegrityHash
                                                       COLLATE Latin1_General_100_BIN2
                                                       NOT LIKE '%[^0-9A-F]%'
                                                   AND 1 = (
                                                       SELECT COUNT(*)
                                                       FROM [dbo].[ProcurementBidderCommunicationRegisters]
                                                           uniqueCommunication
                                                       WHERE uniqueCommunication.TenantId = i.TenantId
                                                         AND uniqueCommunication.SourceType = 2
                                                         AND uniqueCommunication.SourceId = t.Id
                                                         AND uniqueCommunication.IsDeleted = 0
                                                   )
                                             ))
                                        )
                                  )
                              ))
                          )
                    )
                        THROW 51601, 'GHANEPS exchange source, DEC-009, actor, hash, or initial-state lineage is invalid.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE EXISTS (
                            SELECT
                                i.Id, i.SourceType, i.SourceId, i.SourceReference, i.SourceVariant,
                                i.SourceOccurredAtUtc, i.SourceSnapshotJson, i.SourceIntegrityHash,
                                i.EventFamily, i.Direction, i.MappingKey, i.ExternalEventCode,
                                i.EventReference, i.ReferenceField, i.PayloadContentType,
                                i.AcknowledgementContentType, i.AcknowledgementPermissionCode,
                                i.ReconciliationPermissionCode, i.ConfigurationProfileId,
                                i.ConfigurationProfileCode, i.ConfigurationProfileVersion,
                                i.ConfigurationDecisionId, i.ConfigurationDecisionSchemaVersion,
                                i.ExchangeProfileCode, i.ConfigurationValueJson,
                                i.ConfigurationValueHash, i.MappingSnapshotJson,
                                i.MappingIntegrityHash, i.ConfigurationEffectiveFromUtc,
                                i.ConfigurationEffectiveToUtc, i.Frequency, i.Owner,
                                i.AcknowledgementRule, i.ReconciliationRule,
                                i.AcknowledgementRequired, i.ReconciliationRequired,
                                i.MaximumRetryAttempts, i.RequestFingerprint, i.IdempotencyKey,
                                i.CorrelationId, i.PreparedByUserId, i.PreparedByName,
                                i.PreparedAtUtc, i.EvidenceReference, i.IntegrityHash,
                                i.CreatedAt, i.CreatedBy, i.CreatedById, i.IsDeleted,
                                i.DeletedAt, i.DeletedBy, i.TenantId
                            EXCEPT
                            SELECT
                                d.Id, d.SourceType, d.SourceId, d.SourceReference, d.SourceVariant,
                                d.SourceOccurredAtUtc, d.SourceSnapshotJson, d.SourceIntegrityHash,
                                d.EventFamily, d.Direction, d.MappingKey, d.ExternalEventCode,
                                d.EventReference, d.ReferenceField, d.PayloadContentType,
                                d.AcknowledgementContentType, d.AcknowledgementPermissionCode,
                                d.ReconciliationPermissionCode, d.ConfigurationProfileId,
                                d.ConfigurationProfileCode, d.ConfigurationProfileVersion,
                                d.ConfigurationDecisionId, d.ConfigurationDecisionSchemaVersion,
                                d.ExchangeProfileCode, d.ConfigurationValueJson,
                                d.ConfigurationValueHash, d.MappingSnapshotJson,
                                d.MappingIntegrityHash, d.ConfigurationEffectiveFromUtc,
                                d.ConfigurationEffectiveToUtc, d.Frequency, d.Owner,
                                d.AcknowledgementRule, d.ReconciliationRule,
                                d.AcknowledgementRequired, d.ReconciliationRequired,
                                d.MaximumRetryAttempts, d.RequestFingerprint, d.IdempotencyKey,
                                d.CorrelationId, d.PreparedByUserId, d.PreparedByName,
                                d.PreparedAtUtc, d.EvidenceReference, d.IntegrityHash,
                                d.CreatedAt, d.CreatedBy, d.CreatedById, d.IsDeleted,
                                d.DeletedAt, d.DeletedBy, d.TenantId
                        )
                    )
                        THROW 51602, 'GHANEPS exchange immutable configuration, source, identity, and audit lineage cannot change.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE NOT (
                            (d.Status = 0
                             AND (
                                 (i.Status IN (1, 2)
                                  AND EXISTS (
                                      SELECT 1
                                      FROM [dbo].[ProcurementGhanepsExchangeAttempts] cause
                                      WHERE cause.TenantId = i.TenantId
                                        AND cause.ExchangeEventId = i.Id
                                        AND cause.Outcome = 0
                                        AND cause.IsDeleted = 0
                                        AND cause.AttemptNumber = (
                                            SELECT MAX(latest.AttemptNumber)
                                            FROM [dbo].[ProcurementGhanepsExchangeAttempts] latest
                                            WHERE latest.TenantId = i.TenantId
                                              AND latest.ExchangeEventId = i.Id
                                              AND latest.IsDeleted = 0)
                                  )
                                  AND ((i.AcknowledgementRequired = 0 AND i.Status = 1)
                                       OR (i.AcknowledgementRequired = 1 AND i.Status = 2)))
                                 OR
                                 (i.Status = 4
                                  AND EXISTS (
                                      SELECT 1
                                      FROM [dbo].[ProcurementGhanepsExchangeAttempts] cause
                                      WHERE cause.TenantId = i.TenantId
                                        AND cause.ExchangeEventId = i.Id
                                        AND cause.Outcome = 1
                                        AND cause.IsDeleted = 0
                                        AND cause.AttemptNumber = (
                                            SELECT MAX(latest.AttemptNumber)
                                            FROM [dbo].[ProcurementGhanepsExchangeAttempts] latest
                                            WHERE latest.TenantId = i.TenantId
                                              AND latest.ExchangeEventId = i.Id
                                              AND latest.IsDeleted = 0)
                                  ))
                             ))
                            OR
                            (d.Status = 4
                             AND i.Status IN (1, 2, 4)
                             AND EXISTS (
                                 SELECT 1
                                 FROM [dbo].[ProcurementGhanepsExchangeAttempts] cause
                                 WHERE cause.TenantId = i.TenantId
                                   AND cause.ExchangeEventId = i.Id
                                   AND cause.IsRetry = 1
                                   AND cause.IsDeleted = 0
                                   AND cause.AttemptNumber = (
                                       SELECT MAX(latest.AttemptNumber)
                                       FROM [dbo].[ProcurementGhanepsExchangeAttempts] latest
                                       WHERE latest.TenantId = i.TenantId
                                         AND latest.ExchangeEventId = i.Id
                                         AND latest.IsDeleted = 0)
                                   AND ((cause.Outcome = 1 AND i.Status = 4)
                                        OR (cause.Outcome = 0
                                            AND ((i.AcknowledgementRequired = 0 AND i.Status = 1)
                                                 OR (i.AcknowledgementRequired = 1
                                                     AND i.Status = 2))))
                             ))
                            OR
                            (d.Status = 2
                             AND i.Status IN (3, 4)
                             AND EXISTS (
                                 SELECT 1
                                 FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] cause
                                 WHERE cause.TenantId = i.TenantId
                                   AND cause.ExchangeEventId = i.Id
                                   AND cause.IsDeleted = 0
                                   AND cause.Sequence = (
                                       SELECT MAX(latest.Sequence)
                                       FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] latest
                                       WHERE latest.TenantId = i.TenantId
                                         AND latest.ExchangeEventId = i.Id
                                         AND latest.IsDeleted = 0)
                                   AND ((cause.Outcome = 0 AND i.Status = 3)
                                        OR (cause.Outcome = 1 AND i.Status = 4))
                             ))
                            OR
                            (d.Status IN (1, 3)
                             AND i.Status IN (5, 6)
                             AND EXISTS (
                                 SELECT 1
                                 FROM [dbo].[ProcurementGhanepsExchangeReconciliations] cause
                                 WHERE cause.TenantId = i.TenantId
                                   AND cause.ExchangeEventId = i.Id
                                   AND cause.IsDeleted = 0
                                   AND cause.Sequence = (
                                       SELECT MAX(latest.Sequence)
                                       FROM [dbo].[ProcurementGhanepsExchangeReconciliations] latest
                                       WHERE latest.TenantId = i.TenantId
                                         AND latest.ExchangeEventId = i.Id
                                         AND latest.IsDeleted = 0)
                                   AND ((cause.Outcome IN (0, 2) AND i.Status = 5)
                                        OR (cause.Outcome = 1 AND i.Status = 6))
                             ))
                            OR
                            (d.Status = 6
                             AND i.Status = 5
                             AND EXISTS (
                                 SELECT 1
                                 FROM [dbo].[ProcurementGhanepsExchangeReconciliations] cause
                                 WHERE cause.TenantId = i.TenantId
                                   AND cause.ExchangeEventId = i.Id
                                   AND cause.Outcome = 2
                                   AND cause.IsDeleted = 0
                                   AND cause.Sequence = (
                                       SELECT MAX(latest.Sequence)
                                       FROM [dbo].[ProcurementGhanepsExchangeReconciliations] latest
                                       WHERE latest.TenantId = i.TenantId
                                         AND latest.ExchangeEventId = i.Id
                                         AND latest.IsDeleted = 0)
                             ))
                        )
                    )
                        THROW 51603, 'The GHANEPS exchange lifecycle transition is invalid or terminal.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementGhanepsExchangePayloads_Immutable]
                ON [dbo].[ProcurementGhanepsExchangePayloads]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51610, 'GHANEPS exchange payloads are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.RecordedByUserId = '00000000-0000-0000-0000-000000000000'
                           OR NULLIF(LTRIM(RTRIM(i.RecordedByName)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.TemplateReference)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.SchemaReference)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.ExternalPayloadVersion)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.ContentType)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), '') IS NULL
                           OR i.PayloadChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                               WHERE e.Id = i.ExchangeEventId
                                 AND e.TenantId = i.TenantId
                                 AND e.Direction = i.Direction
                                 AND e.PayloadContentType = i.ContentType
                                 AND LTRIM(RTRIM(JSON_VALUE(
                                     e.MappingSnapshotJson, '$.templateReference'))) =
                                     i.TemplateReference
                                 AND LTRIM(RTRIM(JSON_VALUE(
                                     e.MappingSnapshotJson, '$.schemaReference'))) =
                                     i.SchemaReference
                                 AND LTRIM(RTRIM(JSON_VALUE(
                                     e.MappingSnapshotJson, '$.payloadVersion'))) =
                                     i.ExternalPayloadVersion
                                 AND e.IsDeleted = 0
                           )
                           OR i.Version <> (
                               SELECT COUNT(*)
                               FROM [dbo].[ProcurementGhanepsExchangePayloads] p
                               WHERE p.TenantId = i.TenantId
                                 AND p.ExchangeEventId = i.ExchangeEventId
                                 AND p.Version <= i.Version
                                 AND p.IsDeleted = 0
                           )
                    )
                        THROW 51611, 'GHANEPS payload event, mapping, version, actor, or hash lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementGhanepsExchangeAttempts_Immutable]
                ON [dbo].[ProcurementGhanepsExchangeAttempts]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51620, 'GHANEPS exchange attempts are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.AttemptedByUserId = '00000000-0000-0000-0000-000000000000'
                           OR NULLIF(LTRIM(RTRIM(i.AttemptedByName)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), '') IS NULL
                           OR (i.Outcome = 0
                               AND NULLIF(LTRIM(RTRIM(i.TransportReference)), '') IS NULL)
                           OR (i.Outcome = 1
                               AND (NULLIF(LTRIM(RTRIM(i.FailureCode)), '') IS NULL
                                    OR NULLIF(LTRIM(RTRIM(i.FailureMessage)), '') IS NULL))
                           OR i.PayloadChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.RequestFingerprint COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangePayloads] p
                               WHERE p.Id = i.PayloadId
                                 AND p.ExchangeEventId = i.ExchangeEventId
                                 AND p.TenantId = i.TenantId
                                 AND p.PayloadChecksumSha256 = i.PayloadChecksumSha256
                                 AND p.RecordedAtUtc <= i.AttemptedAtUtc
                                 AND p.IsDeleted = 0
                           )
                           OR i.AttemptNumber <> (
                               SELECT COUNT(*)
                               FROM [dbo].[ProcurementGhanepsExchangeAttempts] a
                               WHERE a.TenantId = i.TenantId
                                 AND a.ExchangeEventId = i.ExchangeEventId
                                 AND a.AttemptNumber <= i.AttemptNumber
                                 AND a.IsDeleted = 0
                           )
                           OR (i.AttemptNumber = 1 AND (i.IsRetry = 1 OR i.SupersedesAttemptId IS NOT NULL))
                           OR (i.AttemptNumber > 1 AND (
                               i.IsRetry = 0
                               OR NOT EXISTS (
                                   SELECT 1
                                   FROM [dbo].[ProcurementGhanepsExchangeAttempts] prior
                                   WHERE prior.Id = i.SupersedesAttemptId
                                     AND prior.TenantId = i.TenantId
                                     AND prior.ExchangeEventId = i.ExchangeEventId
                                     AND prior.AttemptNumber = i.AttemptNumber - 1
                                     AND prior.IsDeleted = 0
                               )
                            ))
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                               WHERE e.Id = i.ExchangeEventId
                                 AND e.TenantId = i.TenantId
                                 AND e.IsDeleted = 0
                                 AND (
                                     (i.IsRetry = 0
                                      AND i.AttemptNumber = 1
                                      AND (
                                          (e.Direction = 1
                                           AND i.Outcome = 0
                                           AND ((e.AcknowledgementRequired = 0 AND e.Status = 1)
                                                OR (e.AcknowledgementRequired = 1
                                                    AND e.Status = 2)))
                                          OR
                                          (e.Direction = 0
                                           AND (
                                               e.Status = 0
                                               OR (i.Outcome = 1 AND e.Status = 4)
                                               OR (i.Outcome = 0
                                                   AND ((e.AcknowledgementRequired = 0
                                                         AND e.Status = 1)
                                                        OR (e.AcknowledgementRequired = 1
                                                            AND e.Status = 2)))
                                           ))
                                      ))
                                     OR
                                     (i.IsRetry = 1
                                      AND i.AttemptNumber > 1
                                      AND (
                                          e.Status = 4
                                          OR (e.Status = 0
                                              AND EXISTS (
                                                  SELECT 1
                                                  FROM inserted batchPriorFailure
                                                  WHERE batchPriorFailure.Id =
                                                      i.SupersedesAttemptId
                                                    AND batchPriorFailure.TenantId =
                                                        i.TenantId
                                                    AND batchPriorFailure.ExchangeEventId =
                                                        i.ExchangeEventId
                                                    AND batchPriorFailure.Outcome = 1
                                              ))
                                          OR (i.Outcome = 1 AND e.Status = 4)
                                          OR (i.Outcome = 0
                                              AND ((e.AcknowledgementRequired = 0
                                                    AND e.Status = 1)
                                                   OR (e.AcknowledgementRequired = 1
                                                       AND e.Status = 2)))
                                      )
                                      AND (
                                          EXISTS (
                                              SELECT 1
                                              FROM [dbo].[ProcurementGhanepsExchangeAttempts] priorFailure
                                              WHERE priorFailure.Id = i.SupersedesAttemptId
                                                AND priorFailure.TenantId = i.TenantId
                                                AND priorFailure.ExchangeEventId = i.ExchangeEventId
                                                AND priorFailure.Outcome = 1
                                                AND priorFailure.IsDeleted = 0
                                          )
                                          OR EXISTS (
                                              SELECT 1
                                              FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements]
                                                  rejectedAcknowledgement
                                              WHERE rejectedAcknowledgement.TenantId = i.TenantId
                                                AND rejectedAcknowledgement.ExchangeEventId =
                                                    i.ExchangeEventId
                                                AND rejectedAcknowledgement.AttemptId =
                                                    i.SupersedesAttemptId
                                                AND rejectedAcknowledgement.Outcome = 1
                                                AND rejectedAcknowledgement.IsDeleted = 0
                                          )
                                      )
                                      AND (
                                          SELECT COUNT(*)
                                          FROM [dbo].[ProcurementGhanepsExchangeAttempts] retry
                                          WHERE retry.TenantId = i.TenantId
                                            AND retry.ExchangeEventId = i.ExchangeEventId
                                            AND retry.IsRetry = 1
                                            AND retry.IsDeleted = 0
                                      ) <= e.MaximumRetryAttempts)
                                 )
                           )
                     )
                        THROW 51621, 'GHANEPS attempt payload, sequence, supersession, actor, or hash lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementGhanepsExchangeAcknowledgements_Immutable]
                ON [dbo].[ProcurementGhanepsExchangeAcknowledgements]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51630, 'GHANEPS exchange acknowledgements are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.AcknowledgedByUserId = '00000000-0000-0000-0000-000000000000'
                           OR NULLIF(LTRIM(RTRIM(i.AcknowledgedByName)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.AcknowledgementReference)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.ContentType)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.EvidenceReference)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), '') IS NULL
                           OR i.AcknowledgementChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.RequestFingerprint COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                               JOIN [dbo].[ProcurementGhanepsExchangeAttempts] a
                                 ON a.Id = i.AttemptId
                                AND a.ExchangeEventId = e.Id
                                AND a.TenantId = e.TenantId
                               JOIN [dbo].[ProcurementGhanepsExchangePayloads] p
                                 ON p.Id = i.PayloadId
                                AND p.ExchangeEventId = e.Id
                                AND p.TenantId = e.TenantId
                               WHERE e.Id = i.ExchangeEventId
                                 AND e.TenantId = i.TenantId
                                 AND e.AcknowledgementRequired = 1
                                  AND e.AcknowledgementContentType = i.ContentType
                                  AND a.PayloadId = p.Id
                                  AND a.Outcome = 0
                                  AND a.AttemptNumber = (
                                      SELECT MAX(latest.AttemptNumber)
                                      FROM [dbo].[ProcurementGhanepsExchangeAttempts] latest
                                      WHERE latest.TenantId = i.TenantId
                                        AND latest.ExchangeEventId = i.ExchangeEventId
                                        AND latest.IsDeleted = 0
                                  )
                                  AND a.AttemptedAtUtc <= i.AcknowledgedAtUtc
                                  AND (
                                      e.Status = 2
                                      OR (i.Outcome = 0 AND e.Status = 3)
                                      OR (i.Outcome = 1 AND e.Status = 4)
                                  )
                                 AND e.IsDeleted = 0
                                 AND a.IsDeleted = 0
                                 AND p.IsDeleted = 0
                           )
                           OR i.Sequence <> (
                               SELECT COUNT(*)
                               FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] x
                               WHERE x.TenantId = i.TenantId
                                 AND x.ExchangeEventId = i.ExchangeEventId
                                 AND x.Sequence <= i.Sequence
                                 AND x.IsDeleted = 0
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                               WHERE e.Id = i.ExchangeEventId
                                 AND e.TenantId = i.TenantId
                                 AND e.PreparedByUserId = i.AcknowledgedByUserId
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangePayloads] p
                               WHERE p.ExchangeEventId = i.ExchangeEventId
                                 AND p.TenantId = i.TenantId
                                 AND p.RecordedByUserId = i.AcknowledgedByUserId
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeAttempts] a
                               WHERE a.ExchangeEventId = i.ExchangeEventId
                                 AND a.TenantId = i.TenantId
                                 AND a.AttemptedByUserId = i.AcknowledgedByUserId
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] priorActor
                               WHERE priorActor.ExchangeEventId = i.ExchangeEventId
                                 AND priorActor.TenantId = i.TenantId
                                 AND priorActor.Id <> i.Id
                                 AND priorActor.AcknowledgedByUserId = i.AcknowledgedByUserId
                                 AND priorActor.IsDeleted = 0
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] prior
                               WHERE prior.ExchangeEventId = i.ExchangeEventId
                                 AND prior.TenantId = i.TenantId
                                 AND prior.Id <> i.Id
                                 AND prior.Outcome = 0
                                 AND prior.IsDeleted = 0
                           )
                    )
                        THROW 51631, 'GHANEPS acknowledgement event, attempt, payload, sequence, SOD, actor, or hash lineage is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementGhanepsExchangeReconciliations_Immutable]
                ON [dbo].[ProcurementGhanepsExchangeReconciliations]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51640, 'GHANEPS exchange reconciliations are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE i.IsDeleted = 1
                           OR i.ReconciledByUserId = '00000000-0000-0000-0000-000000000000'
                           OR NULLIF(LTRIM(RTRIM(i.ReconciledByName)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.ActualReference)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.ActualChecksumSha256)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.EvidenceReference)), '') IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.IdempotencyKey)), '') IS NULL
                           OR i.ExpectedChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.ActualChecksumSha256 COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.RequestFingerprint COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR i.IntegrityHash COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-F]%'
                           OR NOT EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                               JOIN [dbo].[ProcurementGhanepsExchangeAttempts] a
                                 ON a.Id = i.AttemptId
                                AND a.ExchangeEventId = e.Id
                                AND a.TenantId = e.TenantId
                               JOIN [dbo].[ProcurementGhanepsExchangePayloads] p
                                 ON p.Id = i.PayloadId
                                AND p.ExchangeEventId = e.Id
                                AND p.TenantId = e.TenantId
                               WHERE e.Id = i.ExchangeEventId
                                 AND e.TenantId = i.TenantId
                                  AND e.ReconciliationRequired = 1
                                  AND a.PayloadId = p.Id
                                  AND a.Outcome = 0
                                  AND (
                                      (i.Outcome IN (0, 1)
                                       AND (
                                           e.Status IN (1, 3)
                                           OR (i.Outcome = 0 AND e.Status = 5)
                                           OR (i.Outcome = 1 AND e.Status = 6)
                                       ))
                                      OR (i.Outcome = 2
                                          AND (
                                              e.Status IN (5, 6)
                                              OR (e.Status IN (1, 3)
                                                  AND EXISTS (
                                                      SELECT 1
                                                      FROM inserted batchMismatch
                                                      WHERE batchMismatch.TenantId = i.TenantId
                                                        AND batchMismatch.ExchangeEventId =
                                                            i.ExchangeEventId
                                                        AND batchMismatch.Sequence = i.Sequence - 1
                                                        AND batchMismatch.Outcome = 1
                                                  ))
                                          ))
                                  )
                                  AND i.ExpectedReference = e.EventReference
                                 AND i.ExpectedChecksumSha256 = p.PayloadChecksumSha256
                                 AND a.AttemptNumber = (
                                     SELECT MAX(latest.AttemptNumber)
                                     FROM [dbo].[ProcurementGhanepsExchangeAttempts] latest
                                     WHERE latest.TenantId = i.TenantId
                                       AND latest.ExchangeEventId = i.ExchangeEventId
                                       AND latest.Outcome = 0
                                       AND latest.IsDeleted = 0
                                 )
                                 AND (
                                     e.AcknowledgementRequired = 0
                                     OR EXISTS (
                                         SELECT 1
                                         FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] ack
                                         WHERE ack.TenantId = i.TenantId
                                           AND ack.ExchangeEventId = i.ExchangeEventId
                                           AND ack.AttemptId = i.AttemptId
                                           AND ack.PayloadId = i.PayloadId
                                           AND ack.Outcome = 0
                                           AND ack.IsDeleted = 0
                                     )
                                 )
                                 AND e.IsDeleted = 0
                                 AND a.IsDeleted = 0
                                 AND p.IsDeleted = 0
                           )
                           OR i.Sequence <> (
                               SELECT COUNT(*)
                               FROM [dbo].[ProcurementGhanepsExchangeReconciliations] x
                               WHERE x.TenantId = i.TenantId
                                 AND x.ExchangeEventId = i.ExchangeEventId
                                 AND x.Sequence <= i.Sequence
                                 AND x.IsDeleted = 0
                           )
                           OR (i.Outcome = 0 AND (
                               i.Sequence <> 1
                               OR i.ActualReference <> i.ExpectedReference
                               OR i.ActualChecksumSha256 <> i.ExpectedChecksumSha256
                           ))
                           OR (i.Outcome = 1 AND (
                               i.Sequence <> 1
                               OR (i.ActualReference = i.ExpectedReference
                                   AND i.ActualChecksumSha256 = i.ExpectedChecksumSha256)
                           ))
                           OR (i.Outcome = 2 AND (
                               i.Sequence <= 1
                               OR NOT EXISTS (
                                   SELECT 1
                                   FROM [dbo].[ProcurementGhanepsExchangeReconciliations] prior
                                   WHERE prior.TenantId = i.TenantId
                                     AND prior.ExchangeEventId = i.ExchangeEventId
                                     AND prior.Sequence = i.Sequence - 1
                                     AND prior.Outcome = 1
                                     AND prior.ReconciledByUserId <> i.ReconciledByUserId
                                     AND prior.IsDeleted = 0
                               )
                           ))
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeReconciliations] terminal
                               WHERE terminal.TenantId = i.TenantId
                                 AND terminal.ExchangeEventId = i.ExchangeEventId
                                  AND terminal.Id <> i.Id
                                  AND terminal.Sequence < i.Sequence
                                  AND terminal.Outcome IN (0, 2)
                                 AND terminal.IsDeleted = 0
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeEvents] e
                               WHERE e.Id = i.ExchangeEventId
                                 AND e.TenantId = i.TenantId
                                 AND e.PreparedByUserId = i.ReconciledByUserId
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangePayloads] p
                               WHERE p.ExchangeEventId = i.ExchangeEventId
                                 AND p.TenantId = i.TenantId
                                 AND p.RecordedByUserId = i.ReconciledByUserId
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeAttempts] a
                               WHERE a.ExchangeEventId = i.ExchangeEventId
                                 AND a.TenantId = i.TenantId
                                 AND a.AttemptedByUserId = i.ReconciledByUserId
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeAcknowledgements] ack
                               WHERE ack.ExchangeEventId = i.ExchangeEventId
                                 AND ack.TenantId = i.TenantId
                                 AND ack.AcknowledgedByUserId = i.ReconciledByUserId
                           )
                           OR EXISTS (
                               SELECT 1
                               FROM [dbo].[ProcurementGhanepsExchangeReconciliations] priorActor
                               WHERE priorActor.ExchangeEventId = i.ExchangeEventId
                                 AND priorActor.TenantId = i.TenantId
                                 AND priorActor.Id <> i.Id
                                 AND priorActor.ReconciledByUserId = i.ReconciledByUserId
                                 AND priorActor.IsDeleted = 0
                           )
                    )
                        THROW 51641, 'GHANEPS reconciliation authoritative lineage, outcome, sequence, terminal state, SOD, actor, or hash is invalid.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementGhanepsExchangeReconciliations_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementGhanepsExchangeAcknowledgements_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementGhanepsExchangeAttempts_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementGhanepsExchangePayloads_Immutable];
                DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementGhanepsExchangeEvents_Guard];
                """);

            migrationBuilder.DropTable(
                name: "ProcurementGhanepsExchangeReconciliations");

            migrationBuilder.DropTable(
                name: "ProcurementGhanepsExchangeAcknowledgements");

            migrationBuilder.DropTable(
                name: "ProcurementGhanepsExchangeAttempts");

            migrationBuilder.DropTable(
                name: "ProcurementGhanepsExchangePayloads");

            migrationBuilder.DropTable(
                name: "ProcurementGhanepsExchangeEvents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProcurementConfigurationProfiles_TenantId_Id",
                table: "ProcurementConfigurationProfiles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProcurementConfigurationDecisions_TenantId_ProfileId_Id",
                table: "ProcurementConfigurationDecisions");

        }
    }
}
