using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementTenderStatutoryControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementTenderControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcingCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    MethodRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorityRouteReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdvertisementReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublicationChannel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenderDocumentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenderDocumentVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentFee = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AdvertisementEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AdvertisedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmissionDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpeningSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpeningIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OpeningEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TechnicalEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicalEvaluationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalEvaluationIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TechnicalEvaluationEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinancialEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinancialEvaluationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinancialEvaluationIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FinancialEvaluationEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecommendedBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorityApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PpaApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ApprovalActorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedForApprovalAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedForApprovalById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AwardBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AwardReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AwardEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContractedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BidderAcceptanceReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BidderAcceptanceEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LifecycleSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderControls_ProcurementPolicyMethodRules_MethodRuleId",
                        column: x => x.MethodRuleId,
                        principalTable: "ProcurementPolicyMethodRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderControls_ProcurementRequisitionAuthorityRoutes_AuthorityRouteId",
                        column: x => x.AuthorityRouteId,
                        principalTable: "ProcurementRequisitionAuthorityRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderControls_ProcurementSourcingCases_SourcingCaseId",
                        column: x => x.SourcingCaseId,
                        principalTable: "ProcurementSourcingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderControls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderControls_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderControls_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderControls_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderDocumentIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    RecipientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IssueReceiptNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ProcurementTenderDocumentIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssues_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssues_ProcurementTenderControls_TenderControlId",
                        column: x => x.TenderControlId,
                        principalTable: "ProcurementTenderControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderDocumentIssues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementTenderSubmissionReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmissionDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    SealedSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementTenderSubmissionReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSubmissionReceipts_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSubmissionReceipts_ProcurementTenderControls_TenderControlId",
                        column: x => x.TenderControlId,
                        principalTable: "ProcurementTenderControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSubmissionReceipts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementTenderSubmissionReceipts_TenderBids_TenderBidId",
                        column: x => x.TenderBidId,
                        principalTable: "TenderBids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_AuthorityRouteId",
                table: "ProcurementTenderControls",
                column: "AuthorityRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_MethodRuleId",
                table: "ProcurementTenderControls",
                column: "MethodRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_SourcingCaseId",
                table: "ProcurementTenderControls",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_TenantId_SourcingCaseId",
                table: "ProcurementTenderControls",
                columns: new[] { "TenantId", "SourcingCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_TenantId_TenderId",
                table: "ProcurementTenderControls",
                columns: new[] { "TenantId", "TenderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_TenderId",
                table: "ProcurementTenderControls",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_WorkflowDefinitionId",
                table: "ProcurementTenderControls",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderControls_WorkflowInstanceId",
                table: "ProcurementTenderControls",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssues_BusinessPartnerId",
                table: "ProcurementTenderDocumentIssues",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssues_TenantId_TenderControlId_IssueReceiptNumber",
                table: "ProcurementTenderDocumentIssues",
                columns: new[] { "TenantId", "TenderControlId", "IssueReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderDocumentIssues_TenderControlId",
                table: "ProcurementTenderDocumentIssues",
                column: "TenderControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSubmissionReceipts_BusinessPartnerId",
                table: "ProcurementTenderSubmissionReceipts",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSubmissionReceipts_TenantId_ReceiptNumber",
                table: "ProcurementTenderSubmissionReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSubmissionReceipts_TenantId_TenderControlId_TenderBidId",
                table: "ProcurementTenderSubmissionReceipts",
                columns: new[] { "TenantId", "TenderControlId", "TenderBidId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSubmissionReceipts_TenderBidId",
                table: "ProcurementTenderSubmissionReceipts",
                column: "TenderBidId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementTenderSubmissionReceipts_TenderControlId",
                table: "ProcurementTenderSubmissionReceipts",
                column: "TenderControlId");

            migrationBuilder.Sql("""
                ALTER TABLE [dbo].[ProcurementTenderControls]
                ADD CONSTRAINT [CK_ProcurementTenderControls_State]
                CHECK ([Method] IN (1, 2) AND [Status] BETWEEN 0 AND 9
                    AND [DocumentFee] >= 0
                    AND [OpeningScheduledAtUtc] >= [SubmissionDeadlineUtc]
                    AND LEN([IntegrityHash]) = 64
                    AND ISJSON([LifecycleSnapshotJson]) = 1);

                ALTER TABLE [dbo].[ProcurementTenderDocumentIssues]
                ADD CONSTRAINT [CK_ProcurementTenderDocumentIssues_Evidence]
                CHECK ([AmountPaid] >= 0 AND LEN([IntegrityHash]) = 64);

                ALTER TABLE [dbo].[ProcurementTenderSubmissionReceipts]
                ADD CONSTRAINT [CK_ProcurementTenderSubmissionReceipts_Classification]
                CHECK ([Disposition] IN (0, 1)
                    AND [Disposition] = CASE WHEN [ReceivedAtUtc] <= [SubmissionDeadlineUtc] THEN 0 ELSE 1 END
                    AND LEN([IntegrityHash]) = 64
                    AND ISJSON([SealedSnapshotJson]) = 1);
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderControls_Lifecycle]
                ON [dbo].[ProcurementTenderControls]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51100, 'Statutory NCT/ICT tender controls cannot be deleted.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[Tenders] t ON t.[Id] = i.[TenderId] AND t.[TenantId] = i.[TenantId] AND t.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementSourcingCases] sc ON sc.[Id] = i.[SourcingCaseId] AND sc.[TenantId] = i.[TenantId] AND sc.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] mr ON mr.[Id] = i.[MethodRuleId] AND mr.[TenantId] = i.[TenantId] AND mr.[IsDeleted] = 0
                        LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] ar ON ar.[Id] = i.[AuthorityRouteId] AND ar.[TenantId] = i.[TenantId] AND ar.[IsDeleted] = 0
                        WHERE i.[IsDeleted] = 1 OR t.[Id] IS NULL OR sc.[Id] IS NULL OR mr.[Id] IS NULL OR ar.[Id] IS NULL
                           OR t.[SourcingCaseId] <> i.[SourcingCaseId]
                           OR t.[SourcePurchaseRequisitionId] <> sc.[PurchaseRequisitionId]
                           OR t.[SourcingReleaseId] <> sc.[SourcingReleaseId]
                           OR t.[EstimatedValue] <> sc.[EstimatedValue]
                           OR UPPER(LTRIM(RTRIM(t.[Currency]))) <> sc.[CurrencyCode]
                           OR sc.[SelectedMethod] NOT IN (1, 2) OR sc.[SelectedMethod] <> i.[Method]
                           OR sc.[MethodRuleId] <> i.[MethodRuleId] OR sc.[MethodRuleCode] <> i.[MethodRuleCode]
                           OR sc.[AuthorityRouteId] <> i.[AuthorityRouteId] OR sc.[AuthorityRouteReference] <> i.[AuthorityRouteReference]
                           OR mr.[Method] <> i.[Method] OR mr.[RuleCode] <> i.[MethodRuleCode]
                           OR mr.[IsAllowed] = 0 OR mr.[IsEnabled] = 0
                           OR ar.[RouteReference] <> i.[AuthorityRouteReference]
                           OR ar.[PurchaseRequisitionId] <> sc.[PurchaseRequisitionId]
                    )
                        THROW 51101, 'Statutory tender tenant, sourcing-case, method-rule, or authority-route lineage is invalid.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE (d.[Id] IS NULL AND
                               (i.[Status] <> 0 OR i.[OpenedAtUtc] IS NOT NULL
                                OR i.[TechnicalEvaluatedAtUtc] IS NOT NULL OR i.[FinancialEvaluatedAtUtc] IS NOT NULL
                                OR i.[SubmittedForApprovalAtUtc] IS NOT NULL OR i.[ApprovedAtUtc] IS NOT NULL
                                OR i.[AwardedAtUtc] IS NOT NULL OR i.[ContractedAtUtc] IS NOT NULL OR i.[AcceptedAtUtc] IS NOT NULL))
                           OR (d.[Id] IS NOT NULL AND
                               (i.[TenantId] <> d.[TenantId] OR i.[TenderId] <> d.[TenderId]
                                OR i.[SourcingCaseId] <> d.[SourcingCaseId] OR i.[MethodRuleId] <> d.[MethodRuleId]
                                OR i.[AuthorityRouteId] <> d.[AuthorityRouteId] OR i.[Method] <> d.[Method]
                                OR i.[MethodRuleCode] <> d.[MethodRuleCode] OR i.[AuthorityRouteReference] <> d.[AuthorityRouteReference]
                                OR i.[AdvertisementReference] <> d.[AdvertisementReference] OR i.[PublicationChannel] <> d.[PublicationChannel]
                                OR i.[TenderDocumentReference] <> d.[TenderDocumentReference] OR i.[TenderDocumentVersion] <> d.[TenderDocumentVersion]
                                OR i.[DocumentFee] <> d.[DocumentFee] OR i.[AdvertisementEvidenceReference] <> d.[AdvertisementEvidenceReference]
                                OR i.[AdvertisedAtUtc] <> d.[AdvertisedAtUtc] OR i.[SubmissionDeadlineUtc] <> d.[SubmissionDeadlineUtc]
                                OR i.[OpeningScheduledAtUtc] <> d.[OpeningScheduledAtUtc] OR i.[CreatedAt] <> d.[CreatedAt]
                                OR i.[IsDeleted] <> d.[IsDeleted]
                                OR NOT (i.[Status] = d.[Status]
                                    OR (d.[Status] = 0 AND i.[Status] = 1)
                                    OR (d.[Status] = 1 AND i.[Status] = 2)
                                    OR (d.[Status] = 2 AND i.[Status] = 3)
                                    OR (d.[Status] = 3 AND i.[Status] = 4)
                                    OR (d.[Status] = 4 AND i.[Status] IN (5, 6))
                                    OR (d.[Status] = 5 AND i.[Status] = 7)
                                    OR (d.[Status] = 7 AND i.[Status] = 8)
                                    OR (d.[Status] = 8 AND i.[Status] = 9))
                                OR (d.[Status] >= 1 AND
                                    (ISNULL(i.[OpenedAtUtc], '19000101') <> ISNULL(d.[OpenedAtUtc], '19000101')
                                     OR ISNULL(i.[OpeningSnapshotJson], '') <> ISNULL(d.[OpeningSnapshotJson], '')
                                     OR ISNULL(i.[OpeningIntegrityHash], '') <> ISNULL(d.[OpeningIntegrityHash], '')
                                     OR ISNULL(i.[OpeningEvidenceReference], '') <> ISNULL(d.[OpeningEvidenceReference], '')))
                                OR (d.[Status] >= 2 AND
                                    (ISNULL(i.[TechnicalEvaluatedAtUtc], '19000101') <> ISNULL(d.[TechnicalEvaluatedAtUtc], '19000101')
                                     OR ISNULL(i.[TechnicalEvaluationSnapshotJson], '') <> ISNULL(d.[TechnicalEvaluationSnapshotJson], '')
                                     OR ISNULL(i.[TechnicalEvaluationIntegrityHash], '') <> ISNULL(d.[TechnicalEvaluationIntegrityHash], '')
                                     OR ISNULL(i.[TechnicalEvaluationEvidenceReference], '') <> ISNULL(d.[TechnicalEvaluationEvidenceReference], '')))
                                OR (d.[Status] >= 3 AND
                                    (ISNULL(i.[FinancialEvaluatedAtUtc], '19000101') <> ISNULL(d.[FinancialEvaluatedAtUtc], '19000101')
                                     OR ISNULL(i.[FinancialEvaluationSnapshotJson], '') <> ISNULL(d.[FinancialEvaluationSnapshotJson], '')
                                     OR ISNULL(i.[FinancialEvaluationIntegrityHash], '') <> ISNULL(d.[FinancialEvaluationIntegrityHash], '')
                                     OR ISNULL(i.[FinancialEvaluationEvidenceReference], '') <> ISNULL(d.[FinancialEvaluationEvidenceReference], '')
                                     OR ISNULL(i.[RecommendedBidId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[RecommendedBidId], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 4 AND
                                    (ISNULL(i.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000')
                                     OR (d.[WorkflowInstanceId] IS NOT NULL AND
                                         ISNULL(i.[WorkflowInstanceId], '00000000-0000-0000-0000-000000000000') <> d.[WorkflowInstanceId])
                                     OR (d.[WorkflowInstanceId] IS NULL AND i.[WorkflowInstanceId] IS NOT NULL
                                         AND NOT (d.[Status] = 4 AND i.[Status] = 4))
                                     OR ISNULL(i.[SubmittedForApprovalAtUtc], '19000101') <> ISNULL(d.[SubmittedForApprovalAtUtc], '19000101')
                                     OR ISNULL(i.[SubmittedForApprovalById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SubmittedForApprovalById], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 5 AND
                                    (ISNULL(i.[AuthorityApprovalReference], '') <> ISNULL(d.[AuthorityApprovalReference], '')
                                     OR ISNULL(i.[PpaApprovalReference], '') <> ISNULL(d.[PpaApprovalReference], '')
                                     OR ISNULL(i.[ApprovedAtUtc], '19000101') <> ISNULL(d.[ApprovedAtUtc], '19000101')
                                     OR ISNULL(i.[ApprovedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[ApprovedById], '00000000-0000-0000-0000-000000000000')))
                                OR (d.[Status] >= 7 AND
                                    (ISNULL(i.[AwardBidId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[AwardBidId], '00000000-0000-0000-0000-000000000000')
                                     OR ISNULL(i.[AwardReference], '') <> ISNULL(d.[AwardReference], '')
                                     OR ISNULL(i.[AwardEvidenceReference], '') <> ISNULL(d.[AwardEvidenceReference], '')
                                     OR ISNULL(i.[AwardedAtUtc], '19000101') <> ISNULL(d.[AwardedAtUtc], '19000101')))
                                OR (d.[Status] >= 8 AND
                                    (ISNULL(i.[ContractReference], '') <> ISNULL(d.[ContractReference], '')
                                     OR ISNULL(i.[ContractEvidenceReference], '') <> ISNULL(d.[ContractEvidenceReference], '')
                                     OR ISNULL(i.[ContractedAtUtc], '19000101') <> ISNULL(d.[ContractedAtUtc], '19000101')))
                                OR (d.[Status] >= 9 AND
                                    (ISNULL(i.[BidderAcceptanceReference], '') <> ISNULL(d.[BidderAcceptanceReference], '')
                                     OR ISNULL(i.[BidderAcceptanceEvidenceReference], '') <> ISNULL(d.[BidderAcceptanceEvidenceReference], '')
                                     OR ISNULL(i.[AcceptedAtUtc], '19000101') <> ISNULL(d.[AcceptedAtUtc], '19000101')))))
                    )
                        THROW 51102, 'Statutory tender immutable fields or lifecycle transition are invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderDocumentIssues_Immutable]
                ON [dbo].[ProcurementTenderDocumentIssues]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51103, 'Statutory tender document issue and sale records are append-only.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN [dbo].[ProcurementTenderControls] c ON c.[Id] = i.[TenderControlId] AND c.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[BusinessPartners] bp ON bp.[Id] = i.[BusinessPartnerId] AND bp.[TenantId] = i.[TenantId] AND bp.[IsDeleted] = 0
                        WHERE i.[IsDeleted] = 1 OR c.[Id] IS NULL OR c.[Status] <> 0
                           OR i.[IssuedAtUtc] > c.[SubmissionDeadlineUtc]
                           OR (i.[BusinessPartnerId] IS NOT NULL AND bp.[Id] IS NULL)
                           OR i.[AmountPaid] <> c.[DocumentFee]
                           OR (c.[DocumentFee] > 0 AND LEN(LTRIM(RTRIM(ISNULL(i.[PaymentReference], '')))) = 0)
                    )
                        THROW 51104, 'Tender document issue tenant, supplier, fee, payment, or submission-window control is invalid.', 1;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementTenderSubmissionReceipts_Immutable]
                ON [dbo].[ProcurementTenderSubmissionReceipts]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[Id] IS NULL)
                        THROW 51105, 'Statutory tender submission receipts cannot be deleted.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN deleted d ON d.[Id] = i.[Id]
                        LEFT JOIN [dbo].[ProcurementTenderControls] c ON c.[Id] = i.[TenderControlId] AND c.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[TenderBids] b ON b.[Id] = i.[TenderBidId] AND b.[TenantId] = i.[TenantId]
                        LEFT JOIN [dbo].[BusinessPartners] bp ON bp.[Id] = i.[BusinessPartnerId] AND bp.[TenantId] = i.[TenantId] AND bp.[IsDeleted] = 0
                        WHERE c.[Id] IS NULL OR b.[Id] IS NULL OR bp.[Id] IS NULL
                           OR b.[TenderId] <> c.[TenderId] OR b.[BusinessPartnerId] <> i.[BusinessPartnerId]
                           OR (d.[Id] IS NULL AND
                               (c.[Status] <> 0 OR i.[SubmissionDeadlineUtc] <> c.[SubmissionDeadlineUtc]
                                OR NOT EXISTS
                                   (SELECT 1 FROM [dbo].[ProcurementTenderDocumentIssues] di
                                    WHERE di.[TenderControlId] = c.[Id] AND di.[BusinessPartnerId] = i.[BusinessPartnerId]
                                      AND di.[TenantId] = i.[TenantId] AND di.[IsDeleted] = 0)))
                    )
                        THROW 51106, 'Tender submission receipt tenant, bid, supplier, document-issue, or deadline lineage is invalid.', 1;
                    IF EXISTS
                    (
                        SELECT 1
                        FROM inserted i
                        JOIN deleted d ON d.[Id] = i.[Id]
                        WHERE d.[OpenedAtUtc] IS NOT NULL OR i.[OpenedAtUtc] IS NULL
                           OR i.[OpenedAtUtc] < i.[SubmissionDeadlineUtc]
                           OR i.[TenantId] <> d.[TenantId] OR i.[TenderControlId] <> d.[TenderControlId]
                           OR i.[TenderBidId] <> d.[TenderBidId] OR i.[BusinessPartnerId] <> d.[BusinessPartnerId]
                           OR i.[ReceiptNumber] <> d.[ReceiptNumber] OR i.[ReceivedAtUtc] <> d.[ReceivedAtUtc]
                           OR i.[SubmissionDeadlineUtc] <> d.[SubmissionDeadlineUtc] OR i.[Disposition] <> d.[Disposition]
                           OR i.[SealedSnapshotJson] <> d.[SealedSnapshotJson] OR i.[IntegrityHash] <> d.[IntegrityHash]
                           OR i.[CreatedAt] <> d.[CreatedAt] OR i.[IsDeleted] <> d.[IsDeleted]
                    )
                        THROW 51107, 'Only the first controlled public opening may update a sealed tender receipt.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementTenderDocumentIssues");

            migrationBuilder.DropTable(
                name: "ProcurementTenderSubmissionReceipts");

            migrationBuilder.DropTable(
                name: "ProcurementTenderControls");
        }
    }
}
