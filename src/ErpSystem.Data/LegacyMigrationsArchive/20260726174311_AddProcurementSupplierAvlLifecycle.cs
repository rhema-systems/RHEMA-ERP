using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierAvlLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementSupplierAvlRegisters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ReviewYear = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledRetirementAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededByRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PolicyDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyProfileVersion = table.Column<int>(type: "int", nullable: false),
                    ReviewFrequencyMonths = table.Column<int>(type: "int", nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperationCorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastOperation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierAvlRegisters", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierAvlRegisters_State", "[Version] >= 1 AND [ReviewYear] BETWEEN 2000 AND 9999 AND [Status] BETWEEN 0 AND 5 AND [ExpiresAtUtc] > [EffectiveFromUtc] AND ([ScheduledRetirementAtUtc] IS NULL OR [ScheduledRetirementAtUtc] >= [EffectiveFromUtc]) AND [ReviewFrequencyMonths] BETWEEN 1 AND 120 AND [PolicyProfileVersion] >= 1 AND LEN([PolicyValueHash]) = 64 AND LEN([CreationCorrelationId]) > 0 AND LEN([LastOperationCorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 AND ISJSON([SnapshotJson]) = 1 AND (([Status] = 1 AND [SubmittedById] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL) OR [Status] <> 1) AND (([Status] IN (2,3,5) AND [ApprovedById] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL) OR [Status] NOT IN (2,3,5)) AND (([Status] = 3 AND [PublishedById] IS NOT NULL AND [PublishedAtUtc] IS NOT NULL) OR [Status] <> 3) AND (([Status] = 4 AND [RejectedById] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 4) AND (([Status] = 5 AND [RetiredAtUtc] IS NOT NULL) OR [Status] <> 5)");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlRegisters_ProcurementConfigurationDecisions_PolicyDecisionId",
                        column: x => x.PolicyDecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlRegisters_ProcurementConfigurationProfiles_PolicyProfileId",
                        column: x => x.PolicyProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlRegisters_ProcurementSupplierAvlRegisters_SupersededByRegisterId",
                        column: x => x.SupersededByRegisterId,
                        principalTable: "ProcurementSupplierAvlRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlRegisters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlRegisters_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlRegisters_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierAvlEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DueDiligenceReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidencePackVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QualifiedListEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuspendedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuspensionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReinstatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReinstatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EligibilitySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EligibilityDecisionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierAvlEntries", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierAvlEntries_State", "[Status] BETWEEN 0 AND 2 AND LEN([EligibilityDecisionHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ISJSON([EligibilitySnapshotJson]) = 1 AND (([Status] = 1 AND [SuspendedAtUtc] IS NOT NULL AND [SuspendedById] IS NOT NULL AND LEN([SuspensionReason]) > 0) OR [Status] <> 1) AND (([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL) OR [Status] <> 2)");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntries_BusinessPartnerRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "BusinessPartnerRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntries_ProcurementQualifiedListEntries_QualifiedListEntryId",
                        column: x => x.QualifiedListEntryId,
                        principalTable: "ProcurementQualifiedListEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntries_ProcurementSupplierAvlRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "ProcurementSupplierAvlRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntries_ProcurementSupplierDueDiligenceReviews_DueDiligenceReviewId",
                        column: x => x.DueDiligenceReviewId,
                        principalTable: "ProcurementSupplierDueDiligenceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntries_ProcurementSupplierEvidencePackVersions_EvidencePackVersionId",
                        column: x => x.EvidencePackVersionId,
                        principalTable: "ProcurementSupplierEvidencePackVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierAvlPublicationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierAvlPublicationSnapshots", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierAvlPublicationSnapshots_State", "[Sequence] >= 1 AND LEN([IntegrityHash]) = 64 AND ISJSON([SnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlPublicationSnapshots_ProcurementSupplierAvlRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "ProcurementSupplierAvlRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlPublicationSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementSupplierAvlEntryStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    BeforeStatus = table.Column<int>(type: "int", nullable: false),
                    AfterStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierAvlEntryStatusHistories", x => x.Id);
                    table.CheckConstraint("CK_ProcurementSupplierAvlEntryStatusHistories_State", "[Action] BETWEEN 0 AND 2 AND [BeforeStatus] BETWEEN 0 AND 2 AND [AfterStatus] BETWEEN 0 AND 2 AND [BeforeStatus] <> [AfterStatus] AND LEN([Reason]) > 0 AND LEN([CorrelationId]) > 0 AND LEN([IntegrityHash]) = 64 AND ISJSON([EvidenceJson]) = 1 AND ISJSON([SnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntryStatusHistories_ProcurementSupplierAvlEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "ProcurementSupplierAvlEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntryStatusHistories_ProcurementSupplierAvlRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "ProcurementSupplierAvlRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierAvlEntryStatusHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_BusinessPartnerId",
                table: "ProcurementSupplierAvlEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_DueDiligenceReviewId",
                table: "ProcurementSupplierAvlEntries",
                column: "DueDiligenceReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_EvidencePackVersionId",
                table: "ProcurementSupplierAvlEntries",
                column: "EvidencePackVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_QualifiedListEntryId",
                table: "ProcurementSupplierAvlEntries",
                column: "QualifiedListEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_RegisterId",
                table: "ProcurementSupplierAvlEntries",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_RegistrationId",
                table: "ProcurementSupplierAvlEntries",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_TenantId_BusinessPartnerId_Status",
                table: "ProcurementSupplierAvlEntries",
                columns: new[] { "TenantId", "BusinessPartnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_TenantId_DueDiligenceReviewId",
                table: "ProcurementSupplierAvlEntries",
                columns: new[] { "TenantId", "DueDiligenceReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntries_TenantId_RegisterId_BusinessPartnerId",
                table: "ProcurementSupplierAvlEntries",
                columns: new[] { "TenantId", "RegisterId", "BusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntryStatusHistories_EntryId",
                table: "ProcurementSupplierAvlEntryStatusHistories",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntryStatusHistories_RegisterId",
                table: "ProcurementSupplierAvlEntryStatusHistories",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntryStatusHistories_TenantId_CorrelationId",
                table: "ProcurementSupplierAvlEntryStatusHistories",
                columns: new[] { "TenantId", "CorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlEntryStatusHistories_TenantId_EntryId_OccurredAtUtc",
                table: "ProcurementSupplierAvlEntryStatusHistories",
                columns: new[] { "TenantId", "EntryId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlPublicationSnapshots_RegisterId",
                table: "ProcurementSupplierAvlPublicationSnapshots",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlPublicationSnapshots_TenantId_RegisterId_Sequence",
                table: "ProcurementSupplierAvlPublicationSnapshots",
                columns: new[] { "TenantId", "RegisterId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_PolicyDecisionId",
                table: "ProcurementSupplierAvlRegisters",
                column: "PolicyDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_PolicyProfileId",
                table: "ProcurementSupplierAvlRegisters",
                column: "PolicyProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_SupersededByRegisterId",
                table: "ProcurementSupplierAvlRegisters",
                column: "SupersededByRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_TenantId_CreationCorrelationId",
                table: "ProcurementSupplierAvlRegisters",
                columns: new[] { "TenantId", "CreationCorrelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_TenantId_PolicyDecisionId",
                table: "ProcurementSupplierAvlRegisters",
                columns: new[] { "TenantId", "PolicyDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_TenantId_RegisterCode_Version",
                table: "ProcurementSupplierAvlRegisters",
                columns: new[] { "TenantId", "RegisterCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_TenantId_Status_EffectiveFromUtc_ExpiresAtUtc",
                table: "ProcurementSupplierAvlRegisters",
                columns: new[] { "TenantId", "Status", "EffectiveFromUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_TenantId_WorkflowInstanceId",
                table: "ProcurementSupplierAvlRegisters",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_WorkflowDefinitionId",
                table: "ProcurementSupplierAvlRegisters",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierAvlRegisters_WorkflowInstanceId",
                table: "ProcurementSupplierAvlRegisters",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcurementSupplierAvlRegisters_OpenReview",
                table: "ProcurementSupplierAvlRegisters",
                columns: new[] { "TenantId", "ReviewYear" },
                unique: true,
                filter: "[Status] IN (0,1,2) AND [IsDeleted] = 0");

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierAvlRegisters_Lifecycle]
                ON [dbo].[ProcurementSupplierAvlRegisters]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        WHERE i.Id IS NULL
                    )
                        THROW 51200, 'Supplier AVL registers cannot be physically deleted.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementConfigurationDecisions pd ON pd.Id = i.PolicyDecisionId
                        LEFT JOIN ProcurementConfigurationProfiles pp ON pp.Id = i.PolicyProfileId
                        LEFT JOIN WorkflowDefinitions wd ON wd.Id = i.WorkflowDefinitionId
                        LEFT JOIN WorkflowInstances wi ON wi.Id = i.WorkflowInstanceId
                        LEFT JOIN ProcurementSupplierAvlRegisters sr ON sr.Id = i.SupersededByRegisterId
                        WHERE pd.Id IS NULL OR pd.TenantId <> i.TenantId
                           OR pd.DecisionKey <> 'DEC-011'
                           OR pd.ProfileId <> i.PolicyProfileId
                           OR pp.Id IS NULL OR pp.TenantId <> i.TenantId
                           OR pp.ProfileCode <> i.PolicyProfileCode
                           OR pp.Version <> i.PolicyProfileVersion
                           OR wd.Id IS NULL OR wd.TenantId <> i.TenantId
                           OR (i.WorkflowInstanceId IS NOT NULL
                               AND (wi.Id IS NULL OR wi.TenantId <> i.TenantId))
                           OR (i.SupersededByRegisterId IS NOT NULL
                               AND (sr.Id IS NULL OR sr.TenantId <> i.TenantId
                                    OR sr.Id = i.Id))
                           OR (i.Status = 3 AND i.ScheduledRetirementAtUtc IS NOT NULL
                               AND (sr.Id IS NULL
                                    OR sr.EffectiveFromUtc <> i.ScheduledRetirementAtUtc))
                    )
                        THROW 51201, 'Supplier AVL policy, workflow, and replacement references must belong to the same tenant.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        WHERE d.Id IS NULL AND (i.Status <> 0 OR i.IsDeleted <> 0)
                    )
                        THROW 51208, 'Supplier AVL registers must be created as active Draft records.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status <> i.Status
                          AND NOT (
                              (d.Status = 0 AND i.Status = 1)
                              OR (d.Status = 1 AND i.Status IN (2, 4))
                              OR (d.Status = 2 AND i.Status = 3)
                              OR (d.Status = 3 AND i.Status = 5)
                          )
                    )
                        THROW 51202, 'Invalid supplier AVL register lifecycle transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status IN (4, 5)
                    )
                        THROW 51203, 'Rejected and retired supplier AVL registers are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.RegisterCode <> d.RegisterCode
                           OR i.Version <> d.Version
                           OR i.ReviewYear <> d.ReviewYear
                           OR i.EffectiveFromUtc <> d.EffectiveFromUtc
                           OR i.ExpiresAtUtc <> d.ExpiresAtUtc
                           OR i.PolicyDecisionId <> d.PolicyDecisionId
                           OR i.PolicyProfileId <> d.PolicyProfileId
                           OR i.PolicyProfileCode <> d.PolicyProfileCode
                           OR i.PolicyProfileVersion <> d.PolicyProfileVersion
                           OR i.ReviewFrequencyMonths <> d.ReviewFrequencyMonths
                           OR i.PolicySnapshotJson <> d.PolicySnapshotJson
                           OR i.PolicyValueHash <> d.PolicyValueHash
                           OR i.WorkflowDefinitionId <> d.WorkflowDefinitionId
                           OR i.CreationCorrelationId <> d.CreationCorrelationId
                           OR i.CreatedAt <> d.CreatedAt
                           OR i.IsDeleted <> d.IsDeleted
                    )
                        THROW 51204, 'Supplier AVL identity, annual period, policy, workflow, and creation lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE ISNULL(i.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000') <>
                              ISNULL(d.WorkflowInstanceId, '00000000-0000-0000-0000-000000000000')
                          AND NOT (
                              d.Status = 1 AND i.Status = 1
                              AND d.WorkflowInstanceId IS NULL
                              AND i.WorkflowInstanceId IS NOT NULL
                          )
                    )
                        THROW 51205, 'Workflow instance lineage can only be attached once after supplier AVL submission.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE d.Status = 3 AND i.Status = 3
                          AND (
                              ISNULL(i.Notes, '') <> ISNULL(d.Notes, '')
                              OR ISNULL(i.SubmittedById, '00000000-0000-0000-0000-000000000000') <>
                                 ISNULL(d.SubmittedById, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.SubmittedAtUtc, '19000101') <>
                                 ISNULL(d.SubmittedAtUtc, '19000101')
                              OR ISNULL(i.ApprovedById, '00000000-0000-0000-0000-000000000000') <>
                                 ISNULL(d.ApprovedById, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.ApprovedAtUtc, '19000101') <>
                                 ISNULL(d.ApprovedAtUtc, '19000101')
                              OR ISNULL(i.PublishedById, '00000000-0000-0000-0000-000000000000') <>
                                 ISNULL(d.PublishedById, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.PublishedAtUtc, '19000101') <>
                                 ISNULL(d.PublishedAtUtc, '19000101')
                          )
                    )
                        THROW 51206, 'Published supplier AVL decision content and approval lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM ProcurementSupplierAvlRegisters a
                        JOIN ProcurementSupplierAvlRegisters b
                          ON b.TenantId = a.TenantId AND b.Id <> a.Id
                        WHERE a.Status = 3 AND b.Status = 3
                          AND a.IsDeleted = 0 AND b.IsDeleted = 0
                          AND a.Id < b.Id
                          AND a.EffectiveFromUtc <
                              CASE WHEN b.ScheduledRetirementAtUtc IS NOT NULL
                                   AND b.ScheduledRetirementAtUtc < b.ExpiresAtUtc
                                   THEN b.ScheduledRetirementAtUtc ELSE b.ExpiresAtUtc END
                          AND b.EffectiveFromUtc <
                              CASE WHEN a.ScheduledRetirementAtUtc IS NOT NULL
                                   AND a.ScheduledRetirementAtUtc < a.ExpiresAtUtc
                                   THEN a.ScheduledRetirementAtUtc ELSE a.ExpiresAtUtc END
                    )
                        THROW 51207, 'Published supplier AVL effective periods cannot overlap within a tenant.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierAvlEntries_Protected]
                ON [dbo].[ProcurementSupplierAvlEntries]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementSupplierAvlRegisters r ON r.Id = i.RegisterId
                        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
                        LEFT JOIN ProcurementSupplierDueDiligenceReviews dr ON dr.Id = i.DueDiligenceReviewId
                        LEFT JOIN BusinessPartnerRegistrations br ON br.Id = i.RegistrationId
                        LEFT JOIN ProcurementSupplierEvidencePackVersions ep ON ep.Id = i.EvidencePackVersionId
                        LEFT JOIN ProcurementQualifiedListEntries ql ON ql.Id = i.QualifiedListEntryId
                        WHERE r.Id IS NULL OR r.TenantId <> i.TenantId
                           OR bp.Id IS NULL OR bp.TenantId <> i.TenantId
                           OR dr.Id IS NULL OR dr.TenantId <> i.TenantId
                              OR dr.BusinessPartnerId <> i.BusinessPartnerId
                           OR (i.RegistrationId IS NOT NULL
                               AND (br.Id IS NULL OR br.TenantId <> i.TenantId
                                    OR br.BusinessPartnerId IS NULL
                                    OR br.BusinessPartnerId <> i.BusinessPartnerId))
                           OR (i.EvidencePackVersionId IS NOT NULL
                               AND (ep.Id IS NULL OR ep.TenantId <> i.TenantId))
                           OR (i.QualifiedListEntryId IS NOT NULL
                               AND (ql.Id IS NULL OR ql.TenantId <> i.TenantId
                                    OR ql.BusinessPartnerId <> i.BusinessPartnerId))
                    )
                        THROW 51210, 'Supplier AVL entries and eligibility lineage must belong to the same tenant and supplier.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.Id = i.Id
                        JOIN ProcurementSupplierAvlRegisters r ON r.Id = i.RegisterId
                        WHERE d.Id IS NULL AND (r.Status <> 0 OR i.Status <> 0
                            OR i.IsDeleted <> 0)
                    )
                        THROW 51211, 'Active supplier AVL entries can only be added to a Draft register.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM deleted d
                        LEFT JOIN inserted i ON i.Id = d.Id
                        LEFT JOIN ProcurementSupplierAvlRegisters r ON r.Id = d.RegisterId
                        WHERE i.Id IS NULL AND (r.Id IS NULL OR r.Status <> 0)
                    )
                        THROW 51212, 'Supplier AVL entries cannot be deleted after register submission.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        JOIN ProcurementSupplierAvlRegisters r ON r.Id = i.RegisterId
                        WHERE i.IsDeleted <> d.IsDeleted AND r.Status <> 0
                    )
                        THROW 51212, 'Supplier AVL entries cannot be deleted after register submission.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        WHERE i.TenantId <> d.TenantId
                           OR i.RegisterId <> d.RegisterId
                           OR i.BusinessPartnerId <> d.BusinessPartnerId
                           OR i.AddedAtUtc <> d.AddedAtUtc
                           OR i.AddedById <> d.AddedById
                           OR i.CreatedAt <> d.CreatedAt
                    )
                        THROW 51213, 'Supplier AVL entry identity, register, supplier, and creation lineage are immutable.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        JOIN ProcurementSupplierAvlRegisters r ON r.Id = i.RegisterId
                        WHERE d.Status <> i.Status
                          AND NOT (
                              r.Status IN (3, 5)
                              AND (
                                  (d.Status = 0 AND i.Status = 1)
                                  OR (d.Status = 1 AND i.Status = 0)
                                  OR (d.Status IN (0, 1) AND i.Status = 2)
                              )
                          )
                    )
                        THROW 51214, 'Invalid supplier AVL entry status transition.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.Id = i.Id
                        JOIN ProcurementSupplierAvlRegisters r ON r.Id = i.RegisterId
                        WHERE r.Status IN (3, 4, 5)
                          AND NOT (
                              r.Status = 3
                              AND d.Status = 1
                              AND i.Status = 0
                          )
                          AND (
                              i.DueDiligenceReviewId <> d.DueDiligenceReviewId
                              OR ISNULL(i.RegistrationId, '00000000-0000-0000-0000-000000000000') <>
                                 ISNULL(d.RegistrationId, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.EvidencePackVersionId, '00000000-0000-0000-0000-000000000000') <>
                                 ISNULL(d.EvidencePackVersionId, '00000000-0000-0000-0000-000000000000')
                              OR ISNULL(i.QualifiedListEntryId, '00000000-0000-0000-0000-000000000000') <>
                                 ISNULL(d.QualifiedListEntryId, '00000000-0000-0000-0000-000000000000')
                              OR i.EligibilitySnapshotJson <> d.EligibilitySnapshotJson
                              OR i.EligibilityDecisionHash <> d.EligibilityDecisionHash
                          )
                    )
                        THROW 51215, 'Published supplier AVL eligibility lineage is immutable.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierAvlEntryStatusHistories_AppendOnly]
                ON [dbo].[ProcurementSupplierAvlEntryStatusHistories]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51220, 'Supplier AVL entry status history is append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementSupplierAvlRegisters r ON r.Id = i.RegisterId
                        LEFT JOIN ProcurementSupplierAvlEntries e ON e.Id = i.EntryId
                        WHERE r.Id IS NULL OR e.Id IS NULL
                           OR r.TenantId <> i.TenantId
                           OR e.TenantId <> i.TenantId
                           OR e.RegisterId <> i.RegisterId
                           OR NOT (
                               (i.Action = 0 AND i.BeforeStatus = 0 AND i.AfterStatus = 1)
                               OR (i.Action = 1 AND i.BeforeStatus = 1 AND i.AfterStatus = 0)
                               OR (i.Action = 2 AND i.BeforeStatus IN (0, 1) AND i.AfterStatus = 2)
                           )
                    )
                        THROW 51221, 'Supplier AVL status history must match the same-tenant register, entry, and lifecycle transition.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierAvlPublicationSnapshots_AppendOnly]
                ON [dbo].[ProcurementSupplierAvlPublicationSnapshots]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51230, 'Supplier AVL publication snapshots are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN ProcurementSupplierAvlRegisters r ON r.Id = i.RegisterId
                        WHERE r.Id IS NULL OR r.TenantId <> i.TenantId OR r.Status <> 3
                    )
                        THROW 51231, 'Supplier AVL snapshots can only be appended to a same-tenant Published register.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementSupplierAvlEntryStatusHistories");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierAvlPublicationSnapshots");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierAvlEntries");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierAvlRegisters");
        }
    }
}
