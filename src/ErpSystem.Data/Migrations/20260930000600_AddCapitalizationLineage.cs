using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260930000600_AddCapitalizationLineage")]
public partial class AddCapitalizationLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("CapitalizationWorkflowInstanceId", "CapitalProjects", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("CapitalizationSourceBookAuthorityId", "CapitalProjects", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("CapitalizationEvidenceHash", "CapitalProjects", "nvarchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<Guid>("SourceFinancePostingEventId", "ProjectCostLines", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("SourceBookAuthorityId", "ProjectCostLines", "uniqueidentifier", nullable: true);
        migrationBuilder.DropIndex("IX_CapitalProjects_TenantId", "CapitalProjects");
        migrationBuilder.DropIndex("IX_ProjectCostLines_TenantId", "ProjectCostLines");
        migrationBuilder.AddUniqueConstraint(
            name: "AK_FixedAssets_TenantId_Id",
            table: "FixedAssets",
            columns: new[] { "TenantId", "Id" });

        migrationBuilder.CreateTable(
            name: "FixedAssetCapitalizationCycles",
            columns: table => new
            {
                Id = table.Column<Guid>("uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>("uniqueidentifier", nullable: false),
                FixedAssetId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CycleNumber = table.Column<int>("int", nullable: false),
                WorkflowInstanceId = table.Column<Guid>("uniqueidentifier", nullable: true),
                SourceBookAuthorityId = table.Column<Guid>("uniqueidentifier", nullable: true),
                ApprovalEvidenceHash = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>("nvarchar(30)", maxLength: 30, nullable: false),
                EffectiveDate = table.Column<DateTime>("date", nullable: false),
                FunctionalCurrencyCode = table.Column<string>("nvarchar(3)", maxLength: 3, nullable: false),
                TransactionCurrencyCode = table.Column<string>("nvarchar(3)", maxLength: 3, nullable: false),
                DebitAccountId = table.Column<Guid>("uniqueidentifier", nullable: false),
                CreditAccountId = table.Column<Guid>("uniqueidentifier", nullable: false),
                DimensionEvidenceHash = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: false),
                OriginalFinancePostingEventId = table.Column<Guid>("uniqueidentifier", nullable: true),
                OriginalJournalEntryId = table.Column<Guid>("uniqueidentifier", nullable: true),
                ReversalFinancePostingEventId = table.Column<Guid>("uniqueidentifier", nullable: true),
                ReversalJournalEntryId = table.Column<Guid>("uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>("datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>("datetime2", nullable: true),
                CreatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>("uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>("bit", nullable: false),
                DeletedAt = table.Column<DateTime>("datetime2", nullable: true),
                DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FixedAssetCapitalizationCycles", x => x.Id);
                table.UniqueConstraint("AK_FixedAssetCapitalizationCycles_TenantId_Id", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_FixedAssetCapitalizationCycles_NoDelete", "[IsDeleted] = 0");
                table.CheckConstraint("CK_FixedAssetCapitalizationCycles_Number", "[CycleNumber] > 0");
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_FixedAssets_TenantId_FixedAssetId", x => new { x.TenantId, x.FixedAssetId }, "FixedAssets", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_WorkflowInstances_TenantId_WorkflowInstanceId", x => new { x.TenantId, x.WorkflowInstanceId }, "WorkflowInstances", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_FinanceSourceBookAuthorities_TenantId_SourceBookAuthorityId", x => new { x.TenantId, x.SourceBookAuthorityId }, "FinanceSourceBookAuthorities", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_Accounts_TenantId_DebitAccountId", x => new { x.TenantId, x.DebitAccountId }, "Accounts", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_Accounts_TenantId_CreditAccountId", x => new { x.TenantId, x.CreditAccountId }, "Accounts", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_FinancePostingEvents_TenantId_OriginalFinancePostingEventId", x => new { x.TenantId, x.OriginalFinancePostingEventId }, "FinancePostingEvents", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_JournalEntries_TenantId_OriginalJournalEntryId", x => new { x.TenantId, x.OriginalJournalEntryId }, "JournalEntries", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_FinancePostingEvents_TenantId_ReversalFinancePostingEventId", x => new { x.TenantId, x.ReversalFinancePostingEventId }, "FinancePostingEvents", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_FixedAssetCapitalizationCycles_JournalEntries_TenantId_ReversalJournalEntryId", x => new { x.TenantId, x.ReversalJournalEntryId }, "JournalEntries", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_FixedAssetId_CycleNumber", "FixedAssetCapitalizationCycles", new[] { "TenantId", "FixedAssetId", "CycleNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_WorkflowInstanceId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "WorkflowInstanceId" }, unique: true, filter: "[WorkflowInstanceId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_SourceBookAuthorityId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "SourceBookAuthorityId" }, unique: true, filter: "[SourceBookAuthorityId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_DebitAccountId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "DebitAccountId" });
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_CreditAccountId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "CreditAccountId" });
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_OriginalFinancePostingEventId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "OriginalFinancePostingEventId" }, unique: true, filter: "[OriginalFinancePostingEventId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_OriginalJournalEntryId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "OriginalJournalEntryId" }, unique: true, filter: "[OriginalJournalEntryId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_ReversalFinancePostingEventId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "ReversalFinancePostingEventId" }, unique: true, filter: "[ReversalFinancePostingEventId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_FixedAssetCapitalizationCycles_TenantId_ReversalJournalEntryId", "FixedAssetCapitalizationCycles", new[] { "TenantId", "ReversalJournalEntryId" }, unique: true, filter: "[ReversalJournalEntryId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_CapitalProjects_TenantId_CapitalizationWorkflowInstanceId", "CapitalProjects", new[] { "TenantId", "CapitalizationWorkflowInstanceId" }, unique: true, filter: "[CapitalizationWorkflowInstanceId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_CapitalProjects_TenantId_CapitalizationSourceBookAuthorityId", "CapitalProjects", new[] { "TenantId", "CapitalizationSourceBookAuthorityId" }, unique: true, filter: "[CapitalizationSourceBookAuthorityId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_ProjectCostLines_TenantId_SourceFinancePostingEventId", "ProjectCostLines", new[] { "TenantId", "SourceFinancePostingEventId" });
        migrationBuilder.CreateIndex("IX_ProjectCostLines_TenantId_SourceBookAuthorityId", "ProjectCostLines", new[] { "TenantId", "SourceBookAuthorityId" });
        migrationBuilder.AddForeignKey(name: "FK_CapitalProjects_WorkflowInstances_TenantId_CapitalizationWorkflowInstanceId", table: "CapitalProjects", columns: new[] { "TenantId", "CapitalizationWorkflowInstanceId" }, principalTable: "WorkflowInstances", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_CapitalProjects_FinanceSourceBookAuthorities_TenantId_CapitalizationSourceBookAuthorityId", table: "CapitalProjects", columns: new[] { "TenantId", "CapitalizationSourceBookAuthorityId" }, principalTable: "FinanceSourceBookAuthorities", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_ProjectCostLines_FinancePostingEvents_TenantId_SourceFinancePostingEventId", table: "ProjectCostLines", columns: new[] { "TenantId", "SourceFinancePostingEventId" }, principalTable: "FinancePostingEvents", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_ProjectCostLines_FinanceSourceBookAuthorities_TenantId_SourceBookAuthorityId", table: "ProjectCostLines", columns: new[] { "TenantId", "SourceBookAuthorityId" }, principalTable: "FinanceSourceBookAuthorities", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_FixedAssetCapitalizationCycles_Evidence]
            ON [dbo].[FixedAssetCapitalizationCycles] AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
                THROW 51061, 'FIXED_ASSET_CAPITALIZATION_CYCLE_APPEND_ONLY', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id=i.Id
                WHERE i.TenantId<>d.TenantId OR i.FixedAssetId<>d.FixedAssetId OR i.CycleNumber<>d.CycleNumber
                   OR d.WorkflowInstanceId IS NOT NULL AND (i.WorkflowInstanceId IS NULL OR i.WorkflowInstanceId<>d.WorkflowInstanceId)
                   OR d.SourceBookAuthorityId IS NOT NULL AND (i.SourceBookAuthorityId IS NULL OR i.SourceBookAuthorityId<>d.SourceBookAuthorityId)
                   OR i.ApprovalEvidenceHash<>d.ApprovalEvidenceHash OR i.EffectiveDate<>d.EffectiveDate
                   OR i.FunctionalCurrencyCode<>d.FunctionalCurrencyCode OR i.TransactionCurrencyCode<>d.TransactionCurrencyCode
                   OR i.DebitAccountId<>d.DebitAccountId OR i.CreditAccountId<>d.CreditAccountId
                   OR i.DimensionEvidenceHash<>d.DimensionEvidenceHash OR i.IsDeleted<>d.IsDeleted
                   OR d.OriginalFinancePostingEventId IS NOT NULL AND (i.OriginalFinancePostingEventId IS NULL OR i.OriginalFinancePostingEventId<>d.OriginalFinancePostingEventId)
                   OR d.OriginalJournalEntryId IS NOT NULL AND (i.OriginalJournalEntryId IS NULL OR i.OriginalJournalEntryId<>d.OriginalJournalEntryId)
                   OR d.ReversalFinancePostingEventId IS NOT NULL AND (i.ReversalFinancePostingEventId IS NULL OR i.ReversalFinancePostingEventId<>d.ReversalFinancePostingEventId)
                   OR d.ReversalJournalEntryId IS NOT NULL AND (i.ReversalJournalEntryId IS NULL OR i.ReversalJournalEntryId<>d.ReversalJournalEntryId))
                THROW 51062, 'FIXED_ASSET_CAPITALIZATION_CYCLE_EVIDENCE_IMMUTABLE', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[FixedAssetCapitalizationCycles])
               OR EXISTS (SELECT 1 FROM [dbo].[CapitalProjects] WHERE [CapitalizationWorkflowInstanceId] IS NOT NULL OR [CapitalizationSourceBookAuthorityId] IS NOT NULL OR [CapitalizationEvidenceHash] IS NOT NULL)
               OR EXISTS (SELECT 1 FROM [dbo].[ProjectCostLines] WHERE [SourceFinancePostingEventId] IS NOT NULL OR [SourceBookAuthorityId] IS NOT NULL)
                THROW 51063, 'CAPITALIZATION_LINEAGE_DOWN_BLOCKED: retained capitalization evidence exists.', 1;
            DROP TRIGGER IF EXISTS [dbo].[TR_FixedAssetCapitalizationCycles_Evidence];
            """);
        migrationBuilder.DropTable("FixedAssetCapitalizationCycles");
        migrationBuilder.DropUniqueConstraint("AK_FixedAssets_TenantId_Id", "FixedAssets");
        migrationBuilder.DropForeignKey("FK_CapitalProjects_WorkflowInstances_TenantId_CapitalizationWorkflowInstanceId", "CapitalProjects");
        migrationBuilder.DropForeignKey("FK_CapitalProjects_FinanceSourceBookAuthorities_TenantId_CapitalizationSourceBookAuthorityId", "CapitalProjects");
        migrationBuilder.DropForeignKey("FK_ProjectCostLines_FinancePostingEvents_TenantId_SourceFinancePostingEventId", "ProjectCostLines");
        migrationBuilder.DropForeignKey("FK_ProjectCostLines_FinanceSourceBookAuthorities_TenantId_SourceBookAuthorityId", "ProjectCostLines");
        migrationBuilder.DropIndex("IX_CapitalProjects_TenantId_CapitalizationWorkflowInstanceId", "CapitalProjects");
        migrationBuilder.DropIndex("IX_CapitalProjects_TenantId_CapitalizationSourceBookAuthorityId", "CapitalProjects");
        migrationBuilder.DropIndex("IX_ProjectCostLines_TenantId_SourceFinancePostingEventId", "ProjectCostLines");
        migrationBuilder.DropIndex("IX_ProjectCostLines_TenantId_SourceBookAuthorityId", "ProjectCostLines");
        migrationBuilder.CreateIndex("IX_CapitalProjects_TenantId", "CapitalProjects", "TenantId");
        migrationBuilder.CreateIndex("IX_ProjectCostLines_TenantId", "ProjectCostLines", "TenantId");
        migrationBuilder.DropColumn("CapitalizationWorkflowInstanceId", "CapitalProjects");
        migrationBuilder.DropColumn("CapitalizationSourceBookAuthorityId", "CapitalProjects");
        migrationBuilder.DropColumn("CapitalizationEvidenceHash", "CapitalProjects");
        migrationBuilder.DropColumn("SourceFinancePostingEventId", "ProjectCostLines");
        migrationBuilder.DropColumn("SourceBookAuthorityId", "ProjectCostLines");
    }
}
