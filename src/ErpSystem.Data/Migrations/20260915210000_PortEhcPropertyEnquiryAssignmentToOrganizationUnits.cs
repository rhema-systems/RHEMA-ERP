using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260915210000_PortEhcPropertyEnquiryAssignmentToOrganizationUnits")]
public sealed class PortEhcPropertyEnquiryAssignmentToOrganizationUnits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AssignedOrganizationUnitId",
            table: "EhcTickets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_EhcTickets_AssignedOrganizationUnitId",
            table: "EhcTickets",
            column: "AssignedOrganizationUnitId");

        migrationBuilder.CreateIndex(
            name: "IX_EhcTickets_TenantId_AssignedOrganizationUnitId",
            table: "EhcTickets",
            columns: new[] { "TenantId", "AssignedOrganizationUnitId" });

        migrationBuilder.AddForeignKey(
            name: "FK_EhcTickets_OrganizationUnits_AssignedOrganizationUnitId",
            table: "EhcTickets",
            column: "AssignedOrganizationUnitId",
            principalTable: "OrganizationUnits",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

        migrationBuilder.Sql("""
            UPDATE ticket
            SET AssignedOrganizationUnitId = salesAndMarketingUnit.Id
            FROM EhcTickets ticket
            INNER JOIN Departments legacyDepartment
                ON legacyDepartment.Id = ticket.AssignedDepartmentId
            CROSS APPLY (
                SELECT TOP (1) organizationUnit.Id
                FROM OrganizationUnits organizationUnit
                WHERE organizationUnit.TenantId = ticket.TenantId
                  AND organizationUnit.Code = N'UNIT-MKT'
                  AND organizationUnit.IsDeleted = 0
                  AND organizationUnit.IsActive = 1
                ORDER BY organizationUnit.Sequence, organizationUnit.Id
            ) salesAndMarketingUnit
            WHERE ticket.AssignedOrganizationUnitId IS NULL
              AND ticket.PropertyListingContextJson IS NOT NULL
              AND ticket.IsDeleted = 0
              AND legacyDepartment.TenantId = ticket.TenantId
              AND legacyDepartment.DepartmentType = 11
              AND legacyDepartment.IsDeleted = 0
              AND legacyDepartment.IsActive = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_EhcTickets_OrganizationUnits_AssignedOrganizationUnitId",
            table: "EhcTickets");

        migrationBuilder.DropIndex(
            name: "IX_EhcTickets_AssignedOrganizationUnitId",
            table: "EhcTickets");

        migrationBuilder.DropIndex(
            name: "IX_EhcTickets_TenantId_AssignedOrganizationUnitId",
            table: "EhcTickets");

        migrationBuilder.DropColumn(
            name: "AssignedOrganizationUnitId",
            table: "EhcTickets");
    }
}