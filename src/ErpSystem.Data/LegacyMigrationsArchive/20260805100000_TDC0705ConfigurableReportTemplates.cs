using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260805100000_TDC0705ConfigurableReportTemplates")]
public sealed class TDC0705ConfigurableReportTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "ReportId", table: "ReportTemplates", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TemplateKey", table: "ReportTemplates", type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<int>(name: "Version", table: "ReportTemplates", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<string>(name: "Audience", table: "ReportTemplates", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Finance");
        migrationBuilder.AddColumn<string>(name: "Cadence", table: "ReportTemplates", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "AdHoc");
        migrationBuilder.AddColumn<string>(name: "Status", table: "ReportTemplates", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft");
        migrationBuilder.AddColumn<string>(name: "DefaultOutputFormat", table: "ReportTemplates", type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Online");
        migrationBuilder.AddColumn<string>(name: "OutputFormats", table: "ReportTemplates", type: "nvarchar(max)", nullable: true, defaultValue: "[\"Online\"]");
        migrationBuilder.AddColumn<string>(name: "SavedFilters", table: "ReportTemplates", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "GenerationMetadata", table: "ReportTemplates", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "LastGeneratedAt", table: "ReportTemplates", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "LastGeneratedBy", table: "ReportTemplates", nullable: true);
        migrationBuilder.AddColumn<string>(name: "LastGenerationFormat", table: "ReportTemplates", type: "nvarchar(10)", maxLength: 10, nullable: true);
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "ReportTemplates", type: "rowversion", rowVersion: true, nullable: false);

        migrationBuilder.Sql("""
            UPDATE [dbo].[ReportTemplates]
            SET [TemplateKey] = N'LEGACY-' + UPPER(CONVERT(nvarchar(36), [Id]))
            WHERE [TemplateKey] = N'';
            """);

        migrationBuilder.CreateIndex(name: "IX_ReportTemplates_ReportId", table: "ReportTemplates", column: "ReportId");
        migrationBuilder.CreateIndex(
            name: "IX_ReportTemplates_TenantId_TemplateKey_Version",
            table: "ReportTemplates",
            columns: new[] { "TenantId", "TemplateKey", "Version" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReportTemplates_TenantId_Status_Audience_Cadence",
            table: "ReportTemplates",
            columns: new[] { "TenantId", "Status", "Audience", "Cadence" });
        migrationBuilder.AddForeignKey(
            name: "FK_ReportTemplates_Reports_ReportId",
            table: "ReportTemplates",
            column: "ReportId",
            principalTable: "Reports",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

        migrationBuilder.Sql("""
            ALTER TABLE [dbo].[ReportTemplates] WITH CHECK ADD CONSTRAINT [CK_ReportTemplates_Audience]
                CHECK ([Audience] IN (N'PPA/GHANEPS', N'Finance', N'Audit', N'Board'));
            ALTER TABLE [dbo].[ReportTemplates] WITH CHECK ADD CONSTRAINT [CK_ReportTemplates_Cadence]
                CHECK ([Cadence] IN (N'Monthly', N'Quarterly', N'AdHoc'));
            ALTER TABLE [dbo].[ReportTemplates] WITH CHECK ADD CONSTRAINT [CK_ReportTemplates_Status]
                CHECK ([Status] IN (N'Draft', N'Published', N'Archived'));
            ALTER TABLE [dbo].[ReportTemplates] WITH CHECK ADD CONSTRAINT [CK_ReportTemplates_DefaultOutputFormat]
                CHECK ([DefaultOutputFormat] IN (N'Online', N'XLSX', N'PDF'));
            ALTER TABLE [dbo].[ReportTemplates] WITH CHECK ADD CONSTRAINT [CK_ReportTemplates_Version]
                CHECK ([Version] > 0);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE [dbo].[ReportTemplates] DROP CONSTRAINT [CK_ReportTemplates_Audience];
            ALTER TABLE [dbo].[ReportTemplates] DROP CONSTRAINT [CK_ReportTemplates_Cadence];
            ALTER TABLE [dbo].[ReportTemplates] DROP CONSTRAINT [CK_ReportTemplates_Status];
            ALTER TABLE [dbo].[ReportTemplates] DROP CONSTRAINT [CK_ReportTemplates_DefaultOutputFormat];
            ALTER TABLE [dbo].[ReportTemplates] DROP CONSTRAINT [CK_ReportTemplates_Version];
            """);
        migrationBuilder.DropForeignKey(name: "FK_ReportTemplates_Reports_ReportId", table: "ReportTemplates");
        migrationBuilder.DropIndex(name: "IX_ReportTemplates_ReportId", table: "ReportTemplates");
        migrationBuilder.DropIndex(name: "IX_ReportTemplates_TenantId_TemplateKey_Version", table: "ReportTemplates");
        migrationBuilder.DropIndex(name: "IX_ReportTemplates_TenantId_Status_Audience_Cadence", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "ReportId", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "TemplateKey", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "Version", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "Audience", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "Cadence", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "Status", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "DefaultOutputFormat", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "OutputFormats", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "SavedFilters", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "GenerationMetadata", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "LastGeneratedAt", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "LastGeneratedBy", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "LastGenerationFormat", table: "ReportTemplates");
        migrationBuilder.DropColumn(name: "RowVersion", table: "ReportTemplates");
    }
}
