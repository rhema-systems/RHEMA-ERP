using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class AddQuantitySurveyValuationWorksheets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QuantitySurveyValuationWorksheets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectInterimValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectBoqVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                LastMutationClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastMutationRequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                RetentionPercentage = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                MeasuredToDateValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                PreviouslyCertifiedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentClaimedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentCertifiedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentPeriodCertifiedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                DisputedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RetentionToDateValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentRetentionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                NetCurrentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                LineCount = table.Column<int>(type: "int", nullable: false),
                PreparedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreparedByName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                PreparedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                AuditAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_QuantitySurveyValuationWorksheets", x => x.Id);
                table.CheckConstraint("CK_QsValuationWorksheets_Amounts", "[MeasuredToDateValue] >= 0 AND [PreviouslyCertifiedValue] >= 0 AND [CurrentClaimedValue] >= 0 AND [CurrentCertifiedValue] >= [PreviouslyCertifiedValue] AND [CurrentCertifiedValue] <= [CurrentClaimedValue] AND [CurrentPeriodCertifiedValue] = [CurrentCertifiedValue] - [PreviouslyCertifiedValue] AND [DisputedValue] = [CurrentClaimedValue] - [CurrentCertifiedValue] AND [RetentionToDateValue] >= [CurrentRetentionValue] AND [CurrentRetentionValue] >= 0 AND [NetCurrentValue] = [CurrentPeriodCertifiedValue] - [CurrentRetentionValue]");
                table.CheckConstraint("CK_QsValuationWorksheets_Counts", "[LineCount] > 0");
                table.CheckConstraint("CK_QsValuationWorksheets_Hashes", "LEN([RequestHash]) = 64 AND ([LastMutationRequestHash] IS NULL OR LEN([LastMutationRequestHash]) = 64)");
                table.CheckConstraint("CK_QsValuationWorksheets_Retention", "[RetentionPercentage] >= 0 AND [RetentionPercentage] <= 100");
                table.CheckConstraint("CK_QsValuationWorksheets_Status", "[Status] = 'Draft'");
                table.ForeignKey("FK_QuantitySurveyValuationWorksheets_ProjectBoqVersions_ProjectBoqVersionId", x => x.ProjectBoqVersionId, "ProjectBoqVersions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheets_ProjectInterimValuations_ProjectInterimValuationId", x => x.ProjectInterimValuationId, "ProjectInterimValuations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheets_Projects_ProjectId", x => x.ProjectId, "Projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheets_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheets_Users_PreparedById", x => x.PreparedById, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyValuationWorksheetLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorksheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectBoqVersionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BoqLineKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                LineNumberSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ItemCodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                DescriptionSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                UnitOfMeasureSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                CurrencySnapshot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                BoqQuantitySnapshot = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                UnitRateSnapshot = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                MeasuredToDateQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                PreviouslyCertifiedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                CurrentClaimedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                CurrentCertifiedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                DisputedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                MeasuredToDateValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                PreviouslyCertifiedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentClaimedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentCertifiedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentPeriodCertifiedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                DisputedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                PreviousRetentionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RetentionToDateValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CurrentRetentionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                NetCurrentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_QuantitySurveyValuationWorksheetLines", x => x.Id);
                table.CheckConstraint("CK_QsValuationWorksheetLines_Amounts", "[MeasuredToDateValue] >= 0 AND [PreviouslyCertifiedValue] >= 0 AND [CurrentClaimedValue] >= [CurrentCertifiedValue] AND [CurrentCertifiedValue] >= [PreviouslyCertifiedValue] AND [CurrentPeriodCertifiedValue] = [CurrentCertifiedValue] - [PreviouslyCertifiedValue] AND [DisputedValue] = [CurrentClaimedValue] - [CurrentCertifiedValue] AND [PreviousRetentionValue] >= 0 AND [RetentionToDateValue] >= [PreviousRetentionValue] AND [CurrentRetentionValue] = [RetentionToDateValue] - [PreviousRetentionValue] AND [NetCurrentValue] = [CurrentPeriodCertifiedValue] - [CurrentRetentionValue]");
                table.CheckConstraint("CK_QsValuationWorksheetLines_DisputeNote", "[DisputedQuantity] = 0 OR LEN(LTRIM(RTRIM([ReviewNote]))) > 0");
                table.CheckConstraint("CK_QsValuationWorksheetLines_Quantities", "[BoqQuantitySnapshot] >= 0 AND [UnitRateSnapshot] >= 0 AND [MeasuredToDateQuantity] >= 0 AND [PreviouslyCertifiedQuantity] >= 0 AND [CurrentClaimedQuantity] >= [CurrentCertifiedQuantity] AND [CurrentCertifiedQuantity] >= [PreviouslyCertifiedQuantity] AND [CurrentClaimedQuantity] <= [MeasuredToDateQuantity] AND [DisputedQuantity] = [CurrentClaimedQuantity] - [CurrentCertifiedQuantity]");
                table.ForeignKey("FK_QuantitySurveyValuationWorksheetLines_ProjectBoqVersionLines_ProjectBoqVersionLineId", x => x.ProjectBoqVersionLineId, "ProjectBoqVersionLines", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheetLines_QuantitySurveyValuationWorksheets_WorksheetId", x => x.WorksheetId, "QuantitySurveyValuationWorksheets", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheetLines_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "QuantitySurveyValuationWorksheetRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorksheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_QuantitySurveyValuationWorksheetRevisions", x => x.Id);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheetRevisions_QuantitySurveyValuationWorksheets_WorksheetId", x => x.WorksheetId, "QuantitySurveyValuationWorksheets", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheetRevisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_QuantitySurveyValuationWorksheetRevisions_Users_ActorUserId", x => x.ActorUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_TenantId_ProjectInterimValuationId", "QuantitySurveyValuationWorksheets", new[] { "TenantId", "ProjectInterimValuationId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_TenantId_ClientRequestId", "QuantitySurveyValuationWorksheets", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_TenantId_LastMutationClientRequestId", "QuantitySurveyValuationWorksheets", new[] { "TenantId", "LastMutationClientRequestId" }, unique: true, filter: "[LastMutationClientRequestId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_TenantId_ProjectId_Status_PreparedAt", "QuantitySurveyValuationWorksheets", new[] { "TenantId", "ProjectId", "Status", "PreparedAt" });
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_ProjectId", "QuantitySurveyValuationWorksheets", "ProjectId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_ProjectInterimValuationId", "QuantitySurveyValuationWorksheets", "ProjectInterimValuationId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_ProjectBoqVersionId", "QuantitySurveyValuationWorksheets", "ProjectBoqVersionId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_PreparedById", "QuantitySurveyValuationWorksheets", "PreparedById");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheets_TenantId", "QuantitySurveyValuationWorksheets", "TenantId");

        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetLines_TenantId_WorksheetId_ProjectBoqVersionLineId", "QuantitySurveyValuationWorksheetLines", new[] { "TenantId", "WorksheetId", "ProjectBoqVersionLineId" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetLines_TenantId_WorksheetId_BoqLineKey", "QuantitySurveyValuationWorksheetLines", new[] { "TenantId", "WorksheetId", "BoqLineKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetLines_TenantId_WorksheetId_Sequence", "QuantitySurveyValuationWorksheetLines", new[] { "TenantId", "WorksheetId", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetLines_ProjectBoqVersionLineId", "QuantitySurveyValuationWorksheetLines", "ProjectBoqVersionLineId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetLines_WorksheetId", "QuantitySurveyValuationWorksheetLines", "WorksheetId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetLines_TenantId", "QuantitySurveyValuationWorksheetLines", "TenantId");

        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetRevisions_TenantId_WorksheetId_CreatedAt", "QuantitySurveyValuationWorksheetRevisions", new[] { "TenantId", "WorksheetId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetRevisions_TenantId_CorrelationId", "QuantitySurveyValuationWorksheetRevisions", new[] { "TenantId", "CorrelationId" });
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetRevisions_ActorUserId", "QuantitySurveyValuationWorksheetRevisions", "ActorUserId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetRevisions_WorksheetId", "QuantitySurveyValuationWorksheetRevisions", "WorksheetId");
        migrationBuilder.CreateIndex("IX_QuantitySurveyValuationWorksheetRevisions_TenantId", "QuantitySurveyValuationWorksheetRevisions", "TenantId");

        migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheets_Guard]
ON [dbo].[QuantitySurveyValuationWorksheets]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51120, 'Valuation worksheets cannot be deleted outside their governed lifecycle.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN Projects p ON p.Id=i.ProjectId AND p.TenantId=i.TenantId AND p.IsDeleted=0
        LEFT JOIN ProjectInterimValuations v ON v.Id=i.ProjectInterimValuationId AND v.ProjectId=i.ProjectId AND v.TenantId=i.TenantId AND v.IsDeleted=0
        LEFT JOIN ProjectBoqVersions b ON b.Id=i.ProjectBoqVersionId AND b.ProjectId=i.ProjectId AND b.TenantId=i.TenantId AND b.IsDeleted=0
        LEFT JOIN Users u ON u.Id=i.PreparedById AND u.TenantId=i.TenantId
        WHERE p.Id IS NULL OR v.Id IS NULL OR v.Status <> 'Draft' OR b.Id IS NULL OR b.Status <> 'Approved' OR b.PublishedAt IS NULL OR u.Id IS NULL)
        THROW 51120, 'Invalid valuation worksheet tenant, project, interim valuation, approved BoQ, or actor lineage.', 1;
END
""");
        migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheetLines_Guard]
ON [dbo].[QuantitySurveyValuationWorksheetLines]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
        THROW 51120, 'Valuation worksheet lines cannot be deleted outside their governed lifecycle.', 1;
    IF EXISTS (
        SELECT 1 FROM inserted i
        LEFT JOIN QuantitySurveyValuationWorksheets w ON w.Id=i.WorksheetId AND w.TenantId=i.TenantId AND w.IsDeleted=0
        LEFT JOIN ProjectBoqVersionLines l ON l.Id=i.ProjectBoqVersionLineId AND l.ProjectBoqVersionId=w.ProjectBoqVersionId AND l.ProjectId=w.ProjectId AND l.TenantId=i.TenantId AND l.LineKey=i.BoqLineKey AND l.IsDeleted=0
        WHERE w.Id IS NULL OR l.Id IS NULL)
        THROW 51120, 'Invalid valuation worksheet line tenant, worksheet, approved BoQ, project, or stable-line lineage.', 1;
END
""");
        migrationBuilder.Sql("""
CREATE OR ALTER TRIGGER [dbo].[TR_QsValuationWorksheetRevisions_AppendOnly]
ON [dbo].[QuantitySurveyValuationWorksheetRevisions]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51122, 'Valuation worksheet revision history is append-only.', 1;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsValuationWorksheetRevisions_AppendOnly];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsValuationWorksheetLines_Guard];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_QsValuationWorksheets_Guard];");
        migrationBuilder.DropTable(name: "QuantitySurveyValuationWorksheetRevisions");
        migrationBuilder.DropTable(name: "QuantitySurveyValuationWorksheetLines");
        migrationBuilder.DropTable(name: "QuantitySurveyValuationWorksheets");
    }
}
