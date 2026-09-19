using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralDocumentMetadataValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CentralDocumentMetadataValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapturedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_CentralDocumentMetadataValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralDocumentMetadataValues_CentralDocumentRecords_DocumentRecordId",
                        column: x => x.DocumentRecordId,
                        principalTable: "CentralDocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CentralDocumentMetadataValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentMetadataValues_DocumentRecordId",
                table: "CentralDocumentMetadataValues",
                column: "DocumentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentMetadataValues_TenantId_DocumentRecordId_FieldKey",
                table: "CentralDocumentMetadataValues",
                columns: new[] { "TenantId", "DocumentRecordId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CentralDocumentMetadataValues_TenantId_TemplateCode_FieldKey",
                table: "CentralDocumentMetadataValues",
                columns: new[] { "TenantId", "TemplateCode", "FieldKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CentralDocumentMetadataValues");
        }
    }
}
