using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralDocumentGenerationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CentralDocumentGenerationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    TitleTemplate = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MetadataTemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AccessProfile = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MergeFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalRole = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SignatureRole = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DefaultDispatchChannel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_CentralDocumentGenerationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentGenerationTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentGenerationTemplates_TenantId_Module_DocumentType",
                table: "CentralDocumentGenerationTemplates",
                columns: new[] { "TenantId", "Module", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentGenerationTemplates_TenantId_TemplateCode",
                table: "CentralDocumentGenerationTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CentralDocumentGenerationTemplates");
        }
    }
}
