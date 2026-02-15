using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class AddEmailCampaigns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EmailCampaigns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                HtmlContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                TextContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                FromName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                FromEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ReplyTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: true),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                TemplateData = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                table.PrimaryKey("PK_EmailCampaigns", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmailCampaigns_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EmailCampaignRecipients",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmailCampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SourceRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_EmailCampaignRecipients", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmailCampaignRecipients_EmailCampaigns_EmailCampaignId",
                    column: x => x.EmailCampaignId,
                    principalTable: "EmailCampaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EmailCampaignRecipients_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EmailCampaignRecipients_EmailCampaignId",
            table: "EmailCampaignRecipients",
            column: "EmailCampaignId");

        migrationBuilder.CreateIndex(
            name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Email",
            table: "EmailCampaignRecipients",
            columns: new[] { "TenantId", "EmailCampaignId", "Email" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmailCampaignRecipients_TenantId_EmailCampaignId_Status",
            table: "EmailCampaignRecipients",
            columns: new[] { "TenantId", "EmailCampaignId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_EmailCampaigns_TenantId_Name",
            table: "EmailCampaigns",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EmailCampaigns_TenantId_Status",
            table: "EmailCampaigns",
            columns: new[] { "TenantId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EmailCampaignRecipients");

        migrationBuilder.DropTable(
            name: "EmailCampaigns");
    }
}

