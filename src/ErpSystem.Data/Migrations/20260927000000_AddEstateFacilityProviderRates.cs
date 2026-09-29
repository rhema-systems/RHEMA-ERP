using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260927000000_AddEstateFacilityProviderRates")]
public partial class AddEstateFacilityProviderRates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EstateFacilityProviderRates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ServiceName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                UnitOfMeasure = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                table.PrimaryKey("PK_EstateFacilityProviderRates", x => x.Id);
                table.ForeignKey(
                    name: "FK_EstateFacilityProviderRates_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityProviderRates_TenantId_BusinessPartnerId_IsActive",
            table: "EstateFacilityProviderRates",
            columns: new[] { "TenantId", "BusinessPartnerId", "IsActive" });
        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityProviderRates_TenantId_ContractId",
            table: "EstateFacilityProviderRates",
            columns: new[] { "TenantId", "ContractId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EstateFacilityProviderRates");
    }
}
