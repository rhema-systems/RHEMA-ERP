using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260929103000_AddEstateFacilityProviderAssignments")]
public partial class AddEstateFacilityProviderAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EstateFacilityProviderAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EstateManagedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ServiceScope = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                ServiceArea = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                AssignmentStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                SchedulePattern = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                SupervisorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SlaReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_EstateFacilityProviderAssignments", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstateFacilityProviderAssignments_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityProviderAssignments_TenantId_BusinessPartnerId_AssignmentStatus",
            table: "EstateFacilityProviderAssignments",
            columns: new[] { "TenantId", "BusinessPartnerId", "AssignmentStatus" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityProviderAssignments_TenantId_ContractId",
            table: "EstateFacilityProviderAssignments",
            columns: new[] { "TenantId", "ContractId" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityProviderAssignments_TenantId_EstateManagedAssetId_AssignmentStatus",
            table: "EstateFacilityProviderAssignments",
            columns: new[] { "TenantId", "EstateManagedAssetId", "AssignmentStatus" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EstateFacilityProviderAssignments");
    }
}
