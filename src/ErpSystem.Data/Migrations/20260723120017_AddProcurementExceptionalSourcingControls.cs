using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementExceptionalSourcingControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementExceptionalSourcingControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourcingCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExceptionRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityRouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    MethodRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExceptionRuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthorityRouteReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    JustificationEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupplierSelectionEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupplierSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceChecklistJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuppliersInvitedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BoardApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    ManagingDirectorApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    PpaApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BoardApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ManagingDirectorApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PpaApprovalReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ApprovalActorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedForApprovalAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedForApprovalById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NegotiationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NegotiationPlanReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NegotiationMinutesEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NegotiationOutcomeReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NegotiatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NegotiatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecommendedBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecommendationReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendationEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecommendedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecommendedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    PostAwardFilingReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PostAwardFilingEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExceptionReportReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ExceptionReportEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FiledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FiledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ProcurementExceptionalSourcingControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_ProcurementPolicyExceptionRules_ExceptionRuleId",
                        column: x => x.ExceptionRuleId,
                        principalTable: "ProcurementPolicyExceptionRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_ProcurementPolicyMethodRules_MethodRuleId",
                        column: x => x.MethodRuleId,
                        principalTable: "ProcurementPolicyMethodRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_ProcurementRequisitionAuthorityRoutes_AuthorityRouteId",
                        column: x => x.AuthorityRouteId,
                        principalTable: "ProcurementRequisitionAuthorityRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_ProcurementSourcingCases_SourcingCaseId",
                        column: x => x.SourcingCaseId,
                        principalTable: "ProcurementSourcingCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_TenderNegotiations_NegotiationId",
                        column: x => x.NegotiationId,
                        principalTable: "TenderNegotiations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementExceptionalSourcingControls_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_AuthorityRouteId",
                table: "ProcurementExceptionalSourcingControls",
                column: "AuthorityRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_ExceptionRuleId",
                table: "ProcurementExceptionalSourcingControls",
                column: "ExceptionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_MethodRuleId",
                table: "ProcurementExceptionalSourcingControls",
                column: "MethodRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_NegotiationId",
                table: "ProcurementExceptionalSourcingControls",
                column: "NegotiationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_SourcingCaseId",
                table: "ProcurementExceptionalSourcingControls",
                column: "SourcingCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_TenantId_ExceptionRuleId",
                table: "ProcurementExceptionalSourcingControls",
                columns: new[] { "TenantId", "ExceptionRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_TenantId_NegotiationId",
                table: "ProcurementExceptionalSourcingControls",
                columns: new[] { "TenantId", "NegotiationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_TenantId_SourcingCaseId",
                table: "ProcurementExceptionalSourcingControls",
                columns: new[] { "TenantId", "SourcingCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_TenantId_TenderId",
                table: "ProcurementExceptionalSourcingControls",
                columns: new[] { "TenantId", "TenderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_TenantId_WorkflowInstanceId",
                table: "ProcurementExceptionalSourcingControls",
                columns: new[] { "TenantId", "WorkflowInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_TenderId",
                table: "ProcurementExceptionalSourcingControls",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_WorkflowDefinitionId",
                table: "ProcurementExceptionalSourcingControls",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementExceptionalSourcingControls_WorkflowInstanceId",
                table: "ProcurementExceptionalSourcingControls",
                column: "WorkflowInstanceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementExceptionalSourcingControls_Core",
                table: "ProcurementExceptionalSourcingControls",
                sql: "[Method] IN (3,4) AND [Status] BETWEEN 0 AND 9 AND ISJSON([SupplierSnapshotJson]) = 1 AND ISJSON([EvidenceChecklistJson]) = 1 AND ISJSON([ApprovalActorsJson]) = 1 AND LEN([IntegrityHash]) = 64");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProcurementExceptionalSourcingControls_Lifecycle",
                table: "ProcurementExceptionalSourcingControls",
                sql: @"([Status] = 0 OR ([WorkflowInstanceId] IS NOT NULL AND [SubmittedForApprovalAtUtc] IS NOT NULL AND [SubmittedForApprovalById] IS NOT NULL))
AND ([Status] NOT IN (2,3,4,5,6,7,8) OR ([ApprovedAtUtc] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [SuppliersInvitedAtUtc] IS NOT NULL AND ([PpaApprovalRequired] = 0 OR NULLIF(LTRIM(RTRIM([PpaApprovalReference])), '') IS NOT NULL) AND ([BoardApprovalRequired] = 0 OR NULLIF(LTRIM(RTRIM([BoardApprovalReference])), '') IS NOT NULL) AND ([ManagingDirectorApprovalRequired] = 0 OR NULLIF(LTRIM(RTRIM([ManagingDirectorApprovalReference])), '') IS NOT NULL))
AND ([Status] NOT IN (3,4,5,6,7,8) OR ([NegotiationId] IS NOT NULL AND NULLIF(LTRIM(RTRIM([NegotiationPlanReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([NegotiationMinutesEvidenceReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([NegotiationOutcomeReference])), '') IS NOT NULL AND [NegotiatedAmount] IS NOT NULL AND [NegotiatedAtUtc] IS NOT NULL))
AND ([Status] NOT IN (4,5,6,7,8) OR ([RecommendedBidId] IS NOT NULL AND NULLIF(LTRIM(RTRIM([RecommendationReason])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([RecommendationEvidenceReference])), '') IS NOT NULL AND [RecommendedAtUtc] IS NOT NULL AND [RecommendedById] IS NOT NULL))
AND ([Status] NOT IN (5,6,7,8) OR ([AwardBidId] = [RecommendedBidId] AND NULLIF(LTRIM(RTRIM([AwardReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([AwardEvidenceReference])), '') IS NOT NULL AND [AwardedAtUtc] IS NOT NULL))
AND ([Status] NOT IN (6,7,8) OR (NULLIF(LTRIM(RTRIM([ContractReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([ContractEvidenceReference])), '') IS NOT NULL AND [ContractedAtUtc] IS NOT NULL))
AND ([Status] NOT IN (7,8) OR (NULLIF(LTRIM(RTRIM([BidderAcceptanceReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([BidderAcceptanceEvidenceReference])), '') IS NOT NULL AND [AcceptedAtUtc] IS NOT NULL))
AND ([Status] <> 8 OR (NULLIF(LTRIM(RTRIM([PostAwardFilingReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([PostAwardFilingEvidenceReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([ExceptionReportReference])), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM([ExceptionReportEvidenceReference])), '') IS NOT NULL AND [FiledAtUtc] IS NOT NULL AND [FiledById] IS NOT NULL)))");

            migrationBuilder.Sql(@"
CREATE TRIGGER [dbo].[TR_ProcurementExceptionalSourcingControls_Lifecycle]
ON [dbo].[ProcurementExceptionalSourcingControls]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
        THROW 51120, 'Exceptional sourcing statutory records cannot be deleted.', 1;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN [dbo].[Tenders] t ON t.[Id] = i.[TenderId] AND t.[TenantId] = i.[TenantId] AND t.[IsDeleted] = 0
        LEFT JOIN [dbo].[ProcurementSourcingCases] c ON c.[Id] = i.[SourcingCaseId] AND c.[TenantId] = i.[TenantId] AND c.[IsDeleted] = 0
        LEFT JOIN [dbo].[ProcurementPolicyMethodRules] m ON m.[Id] = i.[MethodRuleId] AND m.[TenantId] = i.[TenantId]
        LEFT JOIN [dbo].[ProcurementPolicyExceptionRules] e ON e.[Id] = i.[ExceptionRuleId] AND e.[TenantId] = i.[TenantId]
        LEFT JOIN [dbo].[ProcurementRequisitionAuthorityRoutes] a ON a.[Id] = i.[AuthorityRouteId] AND a.[TenantId] = i.[TenantId]
        WHERE t.[Id] IS NULL OR c.[Id] IS NULL OR m.[Id] IS NULL OR e.[Id] IS NULL OR a.[Id] IS NULL
           OR t.[SourcingCaseId] <> i.[SourcingCaseId] OR c.[MethodRuleId] <> i.[MethodRuleId]
           OR c.[AuthorityRouteId] <> i.[AuthorityRouteId] OR c.[SelectedMethod] <> i.[Method]
           OR e.[PolicySetId] <> c.[PolicySetId] OR m.[PolicySetId] <> c.[PolicySetId]
    ) THROW 51121, 'Exceptional sourcing tenant, case, method, exception, or authority lineage is invalid.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE i.[TenantId] <> d.[TenantId] OR i.[TenderId] <> d.[TenderId]
           OR i.[SourcingCaseId] <> d.[SourcingCaseId] OR i.[MethodRuleId] <> d.[MethodRuleId]
           OR i.[ExceptionRuleId] <> d.[ExceptionRuleId] OR i.[AuthorityRouteId] <> d.[AuthorityRouteId]
           OR i.[Method] <> d.[Method] OR i.[MethodRuleCode] <> d.[MethodRuleCode]
           OR i.[ExceptionRuleCode] <> d.[ExceptionRuleCode] OR i.[AuthorityRouteReference] <> d.[AuthorityRouteReference]
           OR i.[Justification] <> d.[Justification] OR i.[JustificationEvidenceReference] <> d.[JustificationEvidenceReference]
           OR i.[SupplierSelectionEvidenceReference] <> d.[SupplierSelectionEvidenceReference]
           OR i.[SupplierSnapshotJson] <> d.[SupplierSnapshotJson] OR i.[EvidenceChecklistJson] <> d.[EvidenceChecklistJson]
           OR i.[PreparedAtUtc] <> d.[PreparedAtUtc] OR i.[PreparedById] <> d.[PreparedById]
           OR i.[BoardApprovalRequired] <> d.[BoardApprovalRequired]
           OR i.[ManagingDirectorApprovalRequired] <> d.[ManagingDirectorApprovalRequired]
           OR i.[PpaApprovalRequired] <> d.[PpaApprovalRequired]
           OR i.[WorkflowDefinitionId] <> d.[WorkflowDefinitionId]
    ) THROW 51122, 'Exceptional sourcing lineage, justification, supplier identity, and evidence checklist are immutable.', 1;

    IF EXISTS (
        SELECT 1 FROM inserted i JOIN deleted d ON d.[Id] = i.[Id]
        WHERE d.[Status] IN (8,9)
           OR NOT (
                i.[Status] = d.[Status]
                OR (d.[Status] = 0 AND i.[Status] = 1)
                OR (d.[Status] = 1 AND i.[Status] IN (2,9))
                OR (d.[Status] = 2 AND i.[Status] = 3)
                OR (d.[Status] = 3 AND i.[Status] = 4)
                OR (d.[Status] = 4 AND i.[Status] = 5)
                OR (d.[Status] = 5 AND i.[Status] = 6)
                OR (d.[Status] = 6 AND i.[Status] = 7)
                OR (d.[Status] = 7 AND i.[Status] = 8)
           )
    ) THROW 51123, 'Exceptional sourcing lifecycle transition is invalid or the final record is immutable.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementExceptionalSourcingControls");
        }
    }
}
