using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260811022852_AddQuantitySurveyVariationApplications")]
public partial class AddQuantitySurveyVariationApplications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("ApplicationClientRequestId", "ProjectVariationOrders", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("ApplicationHash", "ProjectVariationOrders", "varchar(64)", unicode: false, maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>("ApplicationRequestHash", "ProjectVariationOrders", "varchar(64)", unicode: false, maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<DateTime>("AppliedAt", "ProjectVariationOrders", "datetime2", nullable: true);
        migrationBuilder.AddColumn<Guid>("AppliedById", "ProjectVariationOrders", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("BudgetRevisionId", "ProjectVariationOrders", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ContractAmendmentId", "ProjectVariationOrders", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("DownstreamApplicationStatus", "ProjectVariationOrders", "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "NotApplied");
        migrationBuilder.AddColumn<Guid>("ForecastVersionId", "ProjectVariationOrders", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("RevisedBoqVersionId", "ProjectVariationOrders", "uniqueidentifier", nullable: true);

        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_BudgetRevisionId", "ProjectVariationOrders", "BudgetRevisionId");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_ContractAmendmentId", "ProjectVariationOrders", "ContractAmendmentId");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_ForecastVersionId", "ProjectVariationOrders", "ForecastVersionId");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_RevisedBoqVersionId", "ProjectVariationOrders", "RevisedBoqVersionId");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_TenantId_ApplicationClientRequestId", "ProjectVariationOrders", new[] { "TenantId", "ApplicationClientRequestId" }, unique: true, filter: "[IsQuantitySurveyGoverned] = 1 AND [ApplicationClientRequestId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_TenantId_BudgetRevisionId", "ProjectVariationOrders", new[] { "TenantId", "BudgetRevisionId" }, unique: true, filter: "[BudgetRevisionId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_TenantId_ContractAmendmentId", "ProjectVariationOrders", new[] { "TenantId", "ContractAmendmentId" }, unique: true, filter: "[ContractAmendmentId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_TenantId_ForecastVersionId", "ProjectVariationOrders", new[] { "TenantId", "ForecastVersionId" }, unique: true, filter: "[ForecastVersionId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_ProjectVariationOrders_TenantId_RevisedBoqVersionId", "ProjectVariationOrders", new[] { "TenantId", "RevisedBoqVersionId" }, unique: true, filter: "[RevisedBoqVersionId] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.AddForeignKey("FK_ProjectVariationOrders_ProjectBudgetRevisions_BudgetRevisionId", "ProjectVariationOrders", "BudgetRevisionId", "ProjectBudgetRevisions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_ProjectVariationOrders_ContractAmendments_ContractAmendmentId", "ProjectVariationOrders", "ContractAmendmentId", "ContractAmendments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_ProjectVariationOrders_ProjectForecastVersions_ForecastVersionId", "ProjectVariationOrders", "ForecastVersionId", "ProjectForecastVersions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_ProjectVariationOrders_ProjectBoqVersions_RevisedBoqVersionId", "ProjectVariationOrders", "RevisedBoqVersionId", "ProjectBoqVersions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddCheckConstraint("CK_QsVariation_Application", "ProjectVariationOrders",
            "[IsQuantitySurveyGoverned] = 0 OR (([DownstreamApplicationStatus] = 'NotApplied' AND [ApplicationClientRequestId] IS NULL AND [ApplicationRequestHash] IS NULL AND [ApplicationHash] IS NULL AND [AppliedById] IS NULL AND [AppliedAt] IS NULL AND [ContractAmendmentId] IS NULL AND [RevisedBoqVersionId] IS NULL AND [BudgetRevisionId] IS NULL AND [ForecastVersionId] IS NULL) OR ([DownstreamApplicationStatus] IN ('AppliedPendingBoqApproval','Applied') AND [Status] IN ('Approved','Implemented','Closed') AND [ApplicationClientRequestId] IS NOT NULL AND LEN([ApplicationRequestHash]) = 64 AND LEN([ApplicationHash]) = 64 AND [AppliedById] IS NOT NULL AND [AppliedAt] IS NOT NULL AND [ContractAmendmentId] IS NOT NULL AND [RevisedBoqVersionId] IS NOT NULL AND (([UpdateBudgetOnApplication] = 1 AND [BudgetRevisionId] IS NOT NULL) OR ([UpdateBudgetOnApplication] = 0 AND [BudgetRevisionId] IS NULL)) AND (([UpdateForecastOnApplication] = 1 AND [ForecastVersionId] IS NOT NULL) OR ([UpdateForecastOnApplication] = 0 AND [ForecastVersionId] IS NULL))))");

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER [dbo].[TR_QS0511_VariationApplication_Governance]
            ON [dbo].[ProjectVariationOrders]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN deleted d ON d.Id = i.Id
                    WHERE i.IsQuantitySurveyGoverned = 1
                      AND (i.DownstreamApplicationStatus <> d.DownstreamApplicationStatus
                        OR ISNULL(i.ApplicationClientRequestId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ApplicationClientRequestId, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.ApplicationRequestHash, '') <> ISNULL(d.ApplicationRequestHash, '')
                        OR ISNULL(i.ApplicationHash, '') <> ISNULL(d.ApplicationHash, '')
                        OR ISNULL(i.AppliedById, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.AppliedById, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.AppliedAt, '19000101') <> ISNULL(d.AppliedAt, '19000101')
                        OR ISNULL(i.ContractAmendmentId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ContractAmendmentId, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.RevisedBoqVersionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.RevisedBoqVersionId, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.BudgetRevisionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.BudgetRevisionId, '00000000-0000-0000-0000-000000000000')
                        OR ISNULL(i.ForecastVersionId, '00000000-0000-0000-0000-000000000000') <> ISNULL(d.ForecastVersionId, '00000000-0000-0000-0000-000000000000'))
                      AND NOT (
                        d.DownstreamApplicationStatus = 'NotApplied'
                        AND i.DownstreamApplicationStatus = 'AppliedPendingBoqApproval'
                        AND i.Status = 'Approved'
                        AND TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'qs_variation_application_id')) = i.Id
                        AND TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT(N'qs_variation_application_actor')) = i.AppliedById
                        AND CONVERT(nvarchar(64), SESSION_CONTEXT(N'qs_variation_application_hash')) = i.ApplicationHash))
                    THROW 51931, 'Approved variation application lineage is immutable or lacks the governed application capability.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    LEFT JOIN dbo.ContractAmendments ca ON ca.Id = i.ContractAmendmentId AND ca.TenantId = i.TenantId
                        AND ca.ContractId = i.ContractId AND ca.Status = 'Approved' AND ca.IsDeleted = 0
                    LEFT JOIN dbo.ProjectBoqVersions bv ON bv.Id = i.RevisedBoqVersionId AND bv.TenantId = i.TenantId
                        AND bv.ProjectId = i.ProjectId AND bv.SourceVersionId = i.ApprovedBoqVersionId AND bv.VersionType = 3
                        AND bv.Status IN ('Draft','PendingApproval','Approved','Rejected') AND bv.IsDeleted = 0
                    LEFT JOIN dbo.ProjectBudgetRevisions br ON br.Id = i.BudgetRevisionId AND br.TenantId = i.TenantId
                        AND br.ProjectId = i.ProjectId AND br.Status = 'Approved' AND br.IsDeleted = 0
                    LEFT JOIN dbo.ProjectForecastVersions fv ON fv.Id = i.ForecastVersionId AND fv.TenantId = i.TenantId
                        AND fv.ProjectId = i.ProjectId AND fv.IsActive = 1 AND fv.IsDeleted = 0
                    WHERE i.IsQuantitySurveyGoverned = 1 AND i.DownstreamApplicationStatus <> 'NotApplied'
                      AND (i.ApplicationClientRequestId IS NULL OR LEN(i.ApplicationRequestHash) <> 64 OR LEN(i.ApplicationHash) <> 64
                        OR i.AppliedById IS NULL OR i.AppliedAt IS NULL OR ca.Id IS NULL OR bv.Id IS NULL
                        OR ca.ApprovedById <> i.AppliedById
                        OR ca.PreviousValue <> i.OriginalContractSumSnapshot
                        OR ca.NewValue <> CASE WHEN i.UpdateContractSumOnApplication = 1 THEN i.RevisedContractSumSnapshot ELSE i.OriginalContractSumSnapshot END
                        OR ca.ValueChange <> CASE WHEN i.UpdateContractSumOnApplication = 1 THEN i.ApprovedAmount ELSE 0 END
                        OR (i.UpdateBudgetOnApplication = 1 AND br.Id IS NULL) OR (i.UpdateBudgetOnApplication = 0 AND br.Id IS NOT NULL)
                        OR (i.UpdateForecastOnApplication = 1 AND fv.Id IS NULL) OR (i.UpdateForecastOnApplication = 0 AND fv.Id IS NOT NULL)
                        OR ROUND((SELECT COALESCE(SUM(l.LineAmount), 0) FROM dbo.ProjectBoqVersionLines l WHERE l.ProjectBoqVersionId = bv.Id AND l.TenantId = i.TenantId AND l.IsDeleted = 0)
                           - (SELECT COALESCE(SUM(l.LineAmount), 0) FROM dbo.ProjectBoqVersionLines l WHERE l.ProjectBoqVersionId = i.ApprovedBoqVersionId AND l.TenantId = i.TenantId AND l.IsDeleted = 0), 2) <> i.ApprovedAmount))
                    THROW 51932, 'Approved variation downstream contract, BoQ, budget or forecast lineage is incomplete or inconsistent.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QS0511_VariationApplication_Governance];");
        migrationBuilder.DropCheckConstraint("CK_QsVariation_Application", "ProjectVariationOrders");
        migrationBuilder.DropForeignKey("FK_ProjectVariationOrders_ProjectBudgetRevisions_BudgetRevisionId", "ProjectVariationOrders");
        migrationBuilder.DropForeignKey("FK_ProjectVariationOrders_ContractAmendments_ContractAmendmentId", "ProjectVariationOrders");
        migrationBuilder.DropForeignKey("FK_ProjectVariationOrders_ProjectForecastVersions_ForecastVersionId", "ProjectVariationOrders");
        migrationBuilder.DropForeignKey("FK_ProjectVariationOrders_ProjectBoqVersions_RevisedBoqVersionId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_BudgetRevisionId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_ContractAmendmentId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_ForecastVersionId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_RevisedBoqVersionId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_TenantId_ApplicationClientRequestId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_TenantId_BudgetRevisionId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_TenantId_ContractAmendmentId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_TenantId_ForecastVersionId", "ProjectVariationOrders");
        migrationBuilder.DropIndex("IX_ProjectVariationOrders_TenantId_RevisedBoqVersionId", "ProjectVariationOrders");
        migrationBuilder.DropColumn("ApplicationClientRequestId", "ProjectVariationOrders");
        migrationBuilder.DropColumn("ApplicationHash", "ProjectVariationOrders");
        migrationBuilder.DropColumn("ApplicationRequestHash", "ProjectVariationOrders");
        migrationBuilder.DropColumn("AppliedAt", "ProjectVariationOrders");
        migrationBuilder.DropColumn("AppliedById", "ProjectVariationOrders");
        migrationBuilder.DropColumn("BudgetRevisionId", "ProjectVariationOrders");
        migrationBuilder.DropColumn("ContractAmendmentId", "ProjectVariationOrders");
        migrationBuilder.DropColumn("DownstreamApplicationStatus", "ProjectVariationOrders");
        migrationBuilder.DropColumn("ForecastVersionId", "ProjectVariationOrders");
        migrationBuilder.DropColumn("RevisedBoqVersionId", "ProjectVariationOrders");
    }
}
