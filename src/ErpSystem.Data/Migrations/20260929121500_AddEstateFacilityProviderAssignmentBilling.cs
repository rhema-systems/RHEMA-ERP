using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260929121500_AddEstateFacilityProviderAssignmentBilling")]
public partial class AddEstateFacilityProviderAssignmentBilling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "BillingQuantity",
            table: "EstateFacilityProviderAssignments",
            type: "decimal(18,4)",
            precision: 18,
            scale: 4,
            nullable: false,
            defaultValue: 1m);

        migrationBuilder.AddColumn<string>(
            name: "BillingFrequency",
            table: "EstateFacilityProviderAssignments",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastInvoiceDate",
            table: "EstateFacilityProviderAssignments",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextInvoiceDate",
            table: "EstateFacilityProviderAssignments",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderRateId",
            table: "EstateFacilityProviderAssignments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityProviderAssignments_TenantId_BillingFrequency_NextInvoiceDate",
            table: "EstateFacilityProviderAssignments",
            columns: new[] { "TenantId", "BillingFrequency", "NextInvoiceDate" });

        migrationBuilder.CreateIndex(
            name: "IX_EstateFacilityProviderAssignments_TenantId_ProviderRateId",
            table: "EstateFacilityProviderAssignments",
            columns: new[] { "TenantId", "ProviderRateId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EstateFacilityProviderAssignments_TenantId_BillingFrequency_NextInvoiceDate",
            table: "EstateFacilityProviderAssignments");

        migrationBuilder.DropIndex(
            name: "IX_EstateFacilityProviderAssignments_TenantId_ProviderRateId",
            table: "EstateFacilityProviderAssignments");

        migrationBuilder.DropColumn(
            name: "BillingFrequency",
            table: "EstateFacilityProviderAssignments");

        migrationBuilder.DropColumn(
            name: "BillingQuantity",
            table: "EstateFacilityProviderAssignments");

        migrationBuilder.DropColumn(
            name: "LastInvoiceDate",
            table: "EstateFacilityProviderAssignments");

        migrationBuilder.DropColumn(
            name: "NextInvoiceDate",
            table: "EstateFacilityProviderAssignments");

        migrationBuilder.DropColumn(
            name: "ProviderRateId",
            table: "EstateFacilityProviderAssignments");
    }
}
