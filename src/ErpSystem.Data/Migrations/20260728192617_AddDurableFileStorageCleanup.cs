using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableFileStorageCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StorageDeleteAttemptCount",
                table: "FileUploadRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StorageDeleteLastAttemptAtUtc",
                table: "FileUploadRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageDeleteLastError",
                table: "FileUploadRecords",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StorageDeleteNextAttemptAtUtc",
                table: "FileUploadRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StorageDeletedAtUtc",
                table: "FileUploadRecords",
                type: "datetime2",
                nullable: true);

            // Existing soft-deleted rows predate durable cleanup tracking.
            // Treat them as reconciled so deployment does not unexpectedly
            // purge historical storage objects.
            migrationBuilder.Sql(
                """
                UPDATE [dbo].[FileUploadRecords]
                SET [StorageDeletedAtUtc] =
                    COALESCE([DeletedAt], [UpdatedAt], [CreatedAt], SYSUTCDATETIME())
                WHERE [IsDeleted] = 1
                  AND [StorageDeletedAtUtc] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FileUploadRecords_IsDeleted_StorageDeletedAtUtc_StorageDeleteNextAttemptAtUtc",
                table: "FileUploadRecords",
                columns: new[]
                {
                    "IsDeleted",
                    "StorageDeletedAtUtc",
                    "StorageDeleteNextAttemptAtUtc"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileUploadRecords_IsDeleted_StorageDeletedAtUtc_StorageDeleteNextAttemptAtUtc",
                table: "FileUploadRecords");

            migrationBuilder.DropColumn(
                name: "StorageDeleteAttemptCount",
                table: "FileUploadRecords");

            migrationBuilder.DropColumn(
                name: "StorageDeleteLastAttemptAtUtc",
                table: "FileUploadRecords");

            migrationBuilder.DropColumn(
                name: "StorageDeleteLastError",
                table: "FileUploadRecords");

            migrationBuilder.DropColumn(
                name: "StorageDeleteNextAttemptAtUtc",
                table: "FileUploadRecords");

            migrationBuilder.DropColumn(
                name: "StorageDeletedAtUtc",
                table: "FileUploadRecords");
        }
    }
}
