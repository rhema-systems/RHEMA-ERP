using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskBranchPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisplayName", "IsDeleted", "IsSystemPermission", "LastModifiedById", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000023"), "Helpdesk Branch Access", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, null, null, "Access the internal enquiry backoffice branch", "Access Internal Enquiry", false, true, null, "enquiry.internal.access", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000024"), "Helpdesk Branch Access", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, null, null, "Access the external enquiry backoffice branch", "Access External Enquiry", false, true, null, "enquiry.external.access", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000025"), "Helpdesk Branch Access", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, null, null, "Access the internal helpdesk and complaints backoffice branch", "Access Internal Helpdesk & Complaints", false, true, null, "support.internal.access", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000026"), "Helpdesk Branch Access", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, null, null, "Access the external helpdesk and complaints backoffice branch", "Access External Helpdesk & Complaints", false, true, null, "support.external.access", null, null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId", "GrantedAt", "GrantedBy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000025"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000025"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000025"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000025"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000026"));
        }
    }
}
