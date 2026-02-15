using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <inheritdoc />
public partial class _20260208120000_AddSystemFlagsToNotificationTopics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsRequired",
            table: "NotificationTopics",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsSystem",
            table: "NotificationTopics",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsSystem",
            table: "NotificationTopicRecipients",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsRequired",
            table: "NotificationTopics");

        migrationBuilder.DropColumn(
            name: "IsSystem",
            table: "NotificationTopics");

        migrationBuilder.DropColumn(
            name: "IsSystem",
            table: "NotificationTopicRecipients");
    }
}
