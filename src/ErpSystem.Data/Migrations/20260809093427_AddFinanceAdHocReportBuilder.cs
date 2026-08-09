using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Persists governed FR-RP-012 Finance builder definitions. The executable SQL catalogue remains
/// in application code; this table stores only declarative field/operator keys and ownership.
/// Attributes are kept inline because fast Debug builds intentionally omit EF designer files.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809093427_AddFinanceAdHocReportBuilder")]
public partial class AddFinanceAdHocReportBuilder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinanceAdHocReportDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DatasetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Visibility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                MaximumRows = table.Column<int>(type: "int", nullable: false),
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
                table.PrimaryKey("PK_FinanceAdHocReportDefinitions", x => x.Id);
                table.ForeignKey(
                    name: "FK_FinanceAdHocReportDefinitions_AspNetUsers_OwnerUserId",
                    column: x => x.OwnerUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_FinanceAdHocReportDefinitions_Reports_ReportId",
                    column: x => x.ReportId,
                    principalTable: "Reports",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_FinanceAdHocReportDefinitions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceAdHocReportDefinitions_OwnerUserId",
            table: "FinanceAdHocReportDefinitions",
            column: "OwnerUserId");
        migrationBuilder.CreateIndex(
            name: "IX_FinanceAdHocReportDefinitions_ReportId",
            table: "FinanceAdHocReportDefinitions",
            column: "ReportId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_FinanceAdHocReportDefinitions_TenantId_DatasetCode",
            table: "FinanceAdHocReportDefinitions",
            columns: new[] { "TenantId", "DatasetCode" });
        migrationBuilder.CreateIndex(
            name: "IX_FinanceAdHocReportDefinitions_TenantId_OwnerUserId_Visibility",
            table: "FinanceAdHocReportDefinitions",
            columns: new[] { "TenantId", "OwnerUserId", "Visibility" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "FinanceAdHocReportDefinitions");
}
