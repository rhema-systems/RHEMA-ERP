using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmCampaignEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_DataRetentionJobRuns_Tenants_TenantId",
                table: "DataRetentionJobRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_DataRetentionPolicies_Tenants_TenantId",
                table: "DataRetentionPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailSettings_Tenants_TenantId",
                table: "EmailSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Tenants_TenantId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_Tenants_TenantId",
                table: "Opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLineItems_Tenants_TenantId",
                table: "QuoteLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Tenants_TenantId",
                table: "Quotes");

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CampaignType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CampaignStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ActualRevenue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetAudience = table.Column<int>(type: "int", nullable: false),
                    ActualAudience = table.Column<int>(type: "int", nullable: false),
                    ResponseCount = table.Column<int>(type: "int", nullable: false),
                    LeadsGenerated = table.Column<int>(type: "int", nullable: false),
                    OpportunitiesGenerated = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Campaigns_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CampaignMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateAdded = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_CampaignMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId_LeadId",
                table: "CampaignMembers",
                columns: new[] { "CampaignId", "LeadId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_LeadId",
                table: "CampaignMembers",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_TenantId_MemberStatus",
                table: "CampaignMembers",
                columns: new[] { "TenantId", "MemberStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_ManagerId",
                table: "Campaigns",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_TenantId_CampaignStatus",
                table: "Campaigns",
                columns: new[] { "TenantId", "CampaignStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_TenantId_CampaignType",
                table: "Campaigns",
                columns: new[] { "TenantId", "CampaignType" });

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DataRetentionJobRuns_Tenants_TenantId",
                table: "DataRetentionJobRuns",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DataRetentionPolicies_Tenants_TenantId",
                table: "DataRetentionPolicies",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailSettings_Tenants_TenantId",
                table: "EmailSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Tenants_TenantId",
                table: "Leads",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_Tenants_TenantId",
                table: "Opportunities",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLineItems_Tenants_TenantId",
                table: "QuoteLineItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Tenants_TenantId",
                table: "Quotes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_DataRetentionJobRuns_Tenants_TenantId",
                table: "DataRetentionJobRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_DataRetentionPolicies_Tenants_TenantId",
                table: "DataRetentionPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailSettings_Tenants_TenantId",
                table: "EmailSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Tenants_TenantId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_Tenants_TenantId",
                table: "Opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLineItems_Tenants_TenantId",
                table: "QuoteLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Tenants_TenantId",
                table: "Quotes");

            migrationBuilder.DropTable(
                name: "CampaignMembers");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DataRetentionJobRuns_Tenants_TenantId",
                table: "DataRetentionJobRuns",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DataRetentionPolicies_Tenants_TenantId",
                table: "DataRetentionPolicies",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailSettings_Tenants_TenantId",
                table: "EmailSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Tenants_TenantId",
                table: "Leads",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_Tenants_TenantId",
                table: "Opportunities",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLineItems_Tenants_TenantId",
                table: "QuoteLineItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Tenants_TenantId",
                table: "Quotes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
