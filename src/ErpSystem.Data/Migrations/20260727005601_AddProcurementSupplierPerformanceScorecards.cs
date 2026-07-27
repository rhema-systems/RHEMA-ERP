using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierPerformanceScorecards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementSupplierPerformanceScorecards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScorecardReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScorecardSequence = table.Column<int>(type: "int", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextReviewDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyProfileCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PolicyProfileVersion = table.Column<int>(type: "int", nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PerformanceWindowMonths = table.Column<int>(type: "int", nullable: false),
                    MinimumScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MinimumDataCoveragePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ResponseTargetHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EligibilityAction = table.Column<int>(type: "int", nullable: false),
                    DataStatus = table.Column<int>(type: "int", nullable: false),
                    DataCoveragePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PerformanceBand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MinimumScoreBreached = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryTimelinessScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    GrnQualityScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RejectionRateScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PriceCompetitivenessScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ResponsivenessScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ComplaintResolutionScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ContractCompletionScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PurchaseOrderCount = table.Column<int>(type: "int", nullable: false),
                    ReceiptCount = table.Column<int>(type: "int", nullable: false),
                    ReceiptLineCount = table.Column<int>(type: "int", nullable: false),
                    PriceComparisonCount = table.Column<int>(type: "int", nullable: false),
                    ResponseObservationCount = table.Column<int>(type: "int", nullable: false),
                    ComplaintCount = table.Column<int>(type: "int", nullable: false),
                    ContractCount = table.Column<int>(type: "int", nullable: false),
                    MeasureResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceSnapshotHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SupplierControlSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierEligibilityDecisionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RiskAssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RiskAssessmentIntegrityHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CalculatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_ProcurementSupplierPerformanceScorecards", x => x.Id);
                    table.CheckConstraint(
                        "CK_ProcurementSupplierPerformanceScorecards_State",
                        "[ScorecardSequence] >= 1 AND [PeriodEndUtc] >= [PeriodStartUtc] " +
                        "AND [NextReviewDueAtUtc] > [CalculatedAtUtc] " +
                        "AND [PerformanceWindowMonths] BETWEEN 1 AND 120 " +
                        "AND [MinimumScore] BETWEEN 0 AND 100 " +
                        "AND [MinimumDataCoveragePercent] BETWEEN 1 AND 100 " +
                        "AND [ResponseTargetHours] BETWEEN 1 AND 8760 " +
                        "AND [EligibilityAction] BETWEEN 0 AND 2 AND [DataStatus] BETWEEN 0 AND 2 " +
                        "AND [DataCoveragePercent] BETWEEN 0 AND 100 " +
                        "AND ([OverallScore] IS NULL OR [OverallScore] BETWEEN 0 AND 100) " +
                        "AND ([DeliveryTimelinessScore] IS NULL OR [DeliveryTimelinessScore] BETWEEN 0 AND 100) " +
                        "AND ([GrnQualityScore] IS NULL OR [GrnQualityScore] BETWEEN 0 AND 100) " +
                        "AND ([RejectionRateScore] IS NULL OR [RejectionRateScore] BETWEEN 0 AND 100) " +
                        "AND ([PriceCompetitivenessScore] IS NULL OR [PriceCompetitivenessScore] BETWEEN 0 AND 100) " +
                        "AND ([ResponsivenessScore] IS NULL OR [ResponsivenessScore] BETWEEN 0 AND 100) " +
                        "AND ([ComplaintResolutionScore] IS NULL OR [ComplaintResolutionScore] BETWEEN 0 AND 100) " +
                        "AND ([ContractCompletionScore] IS NULL OR [ContractCompletionScore] BETWEEN 0 AND 100) " +
                        "AND [PurchaseOrderCount] >= 0 AND [ReceiptCount] >= 0 " +
                        "AND [ReceiptLineCount] >= 0 AND [PriceComparisonCount] >= 0 " +
                        "AND [ResponseObservationCount] >= 0 AND [ComplaintCount] >= 0 " +
                        "AND [ContractCount] >= 0 AND [PolicyProfileVersion] >= 1 " +
                        "AND LEN([PolicyValueHash]) = 64 AND LEN([SourceSnapshotHash]) = 64 " +
                        "AND LEN([SupplierEligibilityDecisionHash]) = 64 " +
                        "AND ([RiskAssessmentIntegrityHash] IS NULL OR LEN([RiskAssessmentIntegrityHash]) = 64) " +
                        "AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0 " +
                        "AND LEN([IntegrityHash]) = 64 AND ISJSON([PolicySnapshotJson]) = 1 " +
                        "AND ISJSON([MeasureResultsJson]) = 1 AND ISJSON([SourceSnapshotJson]) = 1 " +
                        "AND ISJSON([SupplierControlSnapshotJson]) = 1 " +
                        "AND ISJSON([FindingsJson]) = 1 AND ISJSON([SnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierPerformanceScorecards_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierPerformanceScorecards_ProcurementConfigurationDecisions_PolicyDecisionId",
                        column: x => x.PolicyDecisionId,
                        principalTable: "ProcurementConfigurationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierPerformanceScorecards_ProcurementConfigurationProfiles_PolicyProfileId",
                        column: x => x.PolicyProfileId,
                        principalTable: "ProcurementConfigurationProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierPerformanceScorecards_ProcurementSupplierRiskAssessments_RiskAssessmentId",
                        column: x => x.RiskAssessmentId,
                        principalTable: "ProcurementSupplierRiskAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementSupplierPerformanceScorecards_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_BusinessPartnerId",
                table: "ProcurementSupplierPerformanceScorecards",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_PolicyDecisionId",
                table: "ProcurementSupplierPerformanceScorecards",
                column: "PolicyDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_PolicyProfileId",
                table: "ProcurementSupplierPerformanceScorecards",
                column: "PolicyProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_RiskAssessmentId",
                table: "ProcurementSupplierPerformanceScorecards",
                column: "RiskAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_TenantId_BusinessPartnerId_CalculatedAtUtc",
                table: "ProcurementSupplierPerformanceScorecards",
                columns: new[] { "TenantId", "BusinessPartnerId", "CalculatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_TenantId_BusinessPartnerId_ScorecardSequence",
                table: "ProcurementSupplierPerformanceScorecards",
                columns: new[] { "TenantId", "BusinessPartnerId", "ScorecardSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_TenantId_IdempotencyKey",
                table: "ProcurementSupplierPerformanceScorecards",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_TenantId_PolicyDecisionId",
                table: "ProcurementSupplierPerformanceScorecards",
                columns: new[] { "TenantId", "PolicyDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementSupplierPerformanceScorecards_TenantId_ScorecardReference",
                table: "ProcurementSupplierPerformanceScorecards",
                columns: new[] { "TenantId", "ScorecardReference" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_ProcurementSupplierPerformanceScorecards_AppendOnly]
                ON [dbo].[ProcurementSupplierPerformanceScorecards]
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51900, 'Supplier-performance scorecards are append-only.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        LEFT JOIN BusinessPartners bp ON bp.Id = i.BusinessPartnerId
                        LEFT JOIN ProcurementConfigurationDecisions pd ON pd.Id = i.PolicyDecisionId
                        LEFT JOIN ProcurementConfigurationProfiles pp ON pp.Id = i.PolicyProfileId
                        LEFT JOIN ProcurementSupplierRiskAssessments ra ON ra.Id = i.RiskAssessmentId
                        WHERE i.IsDeleted <> 0
                           OR bp.Id IS NULL OR bp.TenantId <> i.TenantId
                           OR pd.Id IS NULL OR pd.TenantId <> i.TenantId
                           OR pd.DecisionKey <> 'DEC-011'
                           OR pd.ProfileId <> i.PolicyProfileId
                           OR pd.Status <> 2 OR pd.ApprovalStatus <> 1
                           OR pd.EvidenceStatus <> 2
                           OR pp.Id IS NULL OR pp.TenantId <> i.TenantId
                           OR pp.LifecycleStatus <> 1
                           OR pp.ProfileCode <> i.PolicyProfileCode
                           OR pp.Version <> i.PolicyProfileVersion
                           OR (i.RiskAssessmentId IS NULL
                               AND i.RiskAssessmentIntegrityHash IS NOT NULL)
                           OR (i.RiskAssessmentId IS NOT NULL
                               AND (
                                   ra.Id IS NULL OR ra.TenantId <> i.TenantId
                                   OR ra.BusinessPartnerId <> i.BusinessPartnerId
                                   OR ra.IntegrityHash <> i.RiskAssessmentIntegrityHash
                               ))
                    )
                        THROW 51901, 'Supplier-performance supplier, evidenced DEC-011 policy, and risk lineage must be exact and same-tenant.', 1;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        WHERE NOT (
                            (i.DataStatus = 0 AND i.OverallScore IS NOT NULL
                                AND i.DataCoveragePercent >= i.MinimumDataCoveragePercent)
                            OR (i.DataStatus = 1 AND i.OverallScore IS NULL
                                AND i.DataCoveragePercent > 0
                                AND i.DataCoveragePercent < i.MinimumDataCoveragePercent)
                            OR (i.DataStatus = 2 AND i.OverallScore IS NULL
                                AND i.DataCoveragePercent = 0)
                        )
                        OR (i.MinimumScoreBreached = 1
                            AND (i.OverallScore IS NULL OR i.OverallScore >= i.MinimumScore))
                        OR (i.MinimumScoreBreached = 0
                            AND i.OverallScore IS NOT NULL
                            AND i.OverallScore < i.MinimumScore)
                    )
                        THROW 51902, 'Supplier-performance data status, coverage, score, and minimum-score outcome are inconsistent.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_ProcurementSupplierPerformanceScorecards_AppendOnly];");

            migrationBuilder.DropTable(
                name: "ProcurementSupplierPerformanceScorecards");
        }
    }
}
