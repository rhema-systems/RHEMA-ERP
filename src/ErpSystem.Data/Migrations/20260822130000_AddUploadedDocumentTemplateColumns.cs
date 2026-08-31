using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822130000_AddUploadedDocumentTemplateColumns")]
public partial class AddUploadedDocumentTemplateColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TemplateContentType",
            table: "CentralDocumentGenerationTemplates",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TemplateFileName",
            table: "CentralDocumentGenerationTemplates",
            type: "nvarchar(250)",
            maxLength: 250,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "TemplateFileSize",
            table: "CentralDocumentGenerationTemplates",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TemplateFileUploadRecordId",
            table: "CentralDocumentGenerationTemplates",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TemplateRepositoryPath",
            table: "CentralDocumentGenerationTemplates",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CentralDocumentGenerationTemplates_TenantId_TemplateFileUploadRecordId",
            table: "CentralDocumentGenerationTemplates",
            columns: new[] { "TenantId", "TemplateFileUploadRecordId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CentralDocumentGenerationTemplates_TenantId_TemplateFileUploadRecordId",
            table: "CentralDocumentGenerationTemplates");

        migrationBuilder.DropColumn(
            name: "TemplateContentType",
            table: "CentralDocumentGenerationTemplates");

        migrationBuilder.DropColumn(
            name: "TemplateFileName",
            table: "CentralDocumentGenerationTemplates");

        migrationBuilder.DropColumn(
            name: "TemplateFileSize",
            table: "CentralDocumentGenerationTemplates");

        migrationBuilder.DropColumn(
            name: "TemplateFileUploadRecordId",
            table: "CentralDocumentGenerationTemplates");

        migrationBuilder.DropColumn(
            name: "TemplateRepositoryPath",
            table: "CentralDocumentGenerationTemplates");
    }
}
