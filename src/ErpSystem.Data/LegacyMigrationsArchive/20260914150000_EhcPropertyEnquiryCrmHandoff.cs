using ErpSystem.Data.Seeders;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260914150000_EhcPropertyEnquiryCrmHandoff")]
public sealed class EhcPropertyEnquiryCrmHandoff : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CrmLeadId",
            table: "EhcTickets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CrmOpportunityId",
            table: "EhcTickets",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "EhcCrmEngagementLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CrmActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EngagementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                table.PrimaryKey("PK_EhcCrmEngagementLinks", x => x.Id);
                table.ForeignKey(
                    name: "FK_EhcCrmEngagementLinks_CrmActivities_CrmActivityId",
                    column: x => x.CrmActivityId,
                    principalTable: "CrmActivities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.NoAction);
                table.ForeignKey(
                    name: "FK_EhcCrmEngagementLinks_EhcTickets_TicketId",
                    column: x => x.TicketId,
                    principalTable: "EhcTickets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EhcCrmEngagementLinks_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EhcTickets_TenantId_CrmOpportunityId",
            table: "EhcTickets",
            columns: new[] { "TenantId", "CrmOpportunityId" });

        migrationBuilder.CreateIndex(
            name: "IX_EhcCrmEngagementLinks_CrmActivityId",
            table: "EhcCrmEngagementLinks",
            column: "CrmActivityId");

        migrationBuilder.CreateIndex(
            name: "IX_EhcCrmEngagementLinks_TenantId_CrmActivityId",
            table: "EhcCrmEngagementLinks",
            columns: new[] { "TenantId", "CrmActivityId" });

        migrationBuilder.CreateIndex(
            name: "IX_EhcCrmEngagementLinks_TenantId_SourceKey",
            table: "EhcCrmEngagementLinks",
            columns: new[] { "TenantId", "SourceKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EhcCrmEngagementLinks_TenantId_TicketId",
            table: "EhcCrmEngagementLinks",
            columns: new[] { "TenantId", "TicketId" });

        migrationBuilder.CreateIndex(
            name: "IX_EhcCrmEngagementLinks_TicketId",
            table: "EhcCrmEngagementLinks",
            column: "TicketId");

        migrationBuilder.CreateIndex(
            name: "IX_EhcTickets_CrmLeadId",
            table: "EhcTickets",
            column: "CrmLeadId");

        migrationBuilder.CreateIndex(
            name: "IX_EhcTickets_CrmOpportunityId",
            table: "EhcTickets",
            column: "CrmOpportunityId");

        migrationBuilder.AddForeignKey(
            name: "FK_EhcTickets_Leads_CrmLeadId",
            table: "EhcTickets",
            column: "CrmLeadId",
            principalTable: "Leads",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

        migrationBuilder.AddForeignKey(
            name: "FK_EhcTickets_Opportunities_CrmOpportunityId",
            table: "EhcTickets",
            column: "CrmOpportunityId",
            principalTable: "Opportunities",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

        migrationBuilder.Sql(EhcPropertyEnquiryCrmConfiguration.Sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_EhcTickets_Leads_CrmLeadId", table: "EhcTickets");
        migrationBuilder.DropForeignKey(name: "FK_EhcTickets_Opportunities_CrmOpportunityId", table: "EhcTickets");
        migrationBuilder.DropTable(name: "EhcCrmEngagementLinks");
        migrationBuilder.DropIndex(name: "IX_EhcTickets_TenantId_CrmOpportunityId", table: "EhcTickets");
        migrationBuilder.DropIndex(name: "IX_EhcTickets_CrmLeadId", table: "EhcTickets");
        migrationBuilder.DropIndex(name: "IX_EhcTickets_CrmOpportunityId", table: "EhcTickets");
        migrationBuilder.DropColumn(name: "CrmLeadId", table: "EhcTickets");
        migrationBuilder.DropColumn(name: "CrmOpportunityId", table: "EhcTickets");
    }
}
