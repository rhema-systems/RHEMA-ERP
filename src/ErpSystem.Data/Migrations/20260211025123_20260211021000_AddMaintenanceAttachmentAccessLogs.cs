using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <inheritdoc />
public partial class _20260211021000_AddMaintenanceAttachmentAccessLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MaintenanceAttachmentAccessLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccessedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                AccessType = table.Column<int>(type: "int", nullable: false),
                UserAgent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
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
                table.PrimaryKey("PK_MaintenanceAttachmentAccessLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_MaintenanceAttachmentAccessLogs_Employees_AccessedByUserId",
                    column: x => x.AccessedByUserId,
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MaintenanceAttachmentAccessLogs_MaintenanceAttachments_AttachmentId",
                    column: x => x.AttachmentId,
                    principalTable: "MaintenanceAttachments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceAttachmentAccessLogs_AccessedByUserId",
            table: "MaintenanceAttachmentAccessLogs",
            column: "AccessedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_MaintenanceAttachmentAccessLogs_AttachmentId",
            table: "MaintenanceAttachmentAccessLogs",
            column: "AttachmentId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MaintenanceAttachmentAccessLogs");
    }
}
