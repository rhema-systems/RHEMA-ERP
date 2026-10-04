using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261004010000_LinkOpportunityCustomerToBusinessPartner")]
public sealed class LinkOpportunityCustomerToBusinessPartner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Older CRM code left CustomerId unconstrained. Stop deployment rather than
        // changing an existing customer association when it is not a tenant-owned BP.
        migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM dbo.Opportunities AS opportunity
    WHERE opportunity.CustomerId IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.BusinessPartners AS partner
          WHERE partner.Id = opportunity.CustomerId
            AND partner.TenantId = opportunity.TenantId
      )
)
BEGIN
    THROW 51041, 'Opportunity CustomerId has a value without a matching Business Partner in the same tenant. Reconcile these records before applying the CRM relationship migration.', 1;
END");

        migrationBuilder.CreateIndex(
            name: "IX_Opportunities_CustomerId",
            table: "Opportunities",
            column: "CustomerId");

        migrationBuilder.AddForeignKey(
            name: "FK_Opportunities_BusinessPartners_CustomerId",
            table: "Opportunities",
            column: "CustomerId",
            principalTable: "BusinessPartners",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Opportunities_BusinessPartners_CustomerId",
            table: "Opportunities");

        migrationBuilder.DropIndex(
            name: "IX_Opportunities_CustomerId",
            table: "Opportunities");
    }
}
