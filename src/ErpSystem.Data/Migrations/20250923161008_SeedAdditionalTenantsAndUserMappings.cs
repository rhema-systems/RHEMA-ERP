using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdditionalTenantsAndUserMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5b3f921d-2398-4f29-823e-a3b2e8599967"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("89bf11b7-396a-4ad4-8a1b-edf0415b92db"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c849318a-78b5-4352-8cc0-e54051706710"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c8b67e30-c868-4102-bf56-9d8b0656769d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("cd04d659-03db-43fe-bdfd-67d44972bca3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ea3115bb-a58d-4c0d-92f3-1eebaf5280bd"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fdfcf06f-57df-47d2-abae-83d44d1b9a97"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(546));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(549));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(552));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1da24716-fd56-44b0-8d2c-b4e7d6850c9b"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(671), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(671), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4643d21c-6433-49b7-9026-66c7039d7351"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(854), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(854), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a7673475-7689-4253-81ec-62894c352cb9"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(649), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(644), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ac1534db-0b3a-4f1a-8da4-600e8d3609ae"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(874), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(873), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("acdad383-4d21-4f5b-9fc3-2e16220d7102"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(710), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(709), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c5f28f1e-92ea-43c7-883c-03ed0485b45b"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("df39c1d7-5c64-415d-9485-fe50d7c4f821"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(891), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(891), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            // Insert additional tenants
            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "Address", "AllowSelfRegistration", "Code", "ContactEmail", "ContactPhone", "CreatedAt", "CreatedBy", "DefaultPriority", "DeletedAt", "DeletedBy", "Description", "Domain", "EnableAutoSelection", "IsDefaultForInternalUsers", "IsDefaultForPublicUsers", "IsDeleted", "LdapBaseDn", "LdapBindDn", "LdapBindPassword", "LdapEnabled", "LdapPort", "LdapServer", "LogoUrl", "Name", "PublicRegistrationDomains", "RequireEmailVerification", "Status", "SubscriptionEndDate", "SubscriptionStartDate", "UpdatedAt", "UpdatedBy", "UserAudience", "WelcomeMessage" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000002"), "456 Acme Ave, Business City, BC 67890", false, "ACME", "admin@acme.com", "+1-555-0200", new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207), "System", 10, null, null, "Acme Corporation - Multi-division enterprise", "acme.com", false, false, false, false, null, null, null, false, null, null, null, "Acme Corporation", null, false, 1, new DateTime(2026, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207), new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207), null, null, 2, null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "789 Tech Blvd, Innovation City, IC 12345", false, "TECHSTART", "admin@techstart.com", "+1-555-0300", new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207), "System", 10, null, null, "TechStart Inc - Growing technology startup", "techstart.com", false, false, false, false, null, null, null, false, null, null, null, "TechStart Inc", null, false, 1, new DateTime(2026, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207), new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207), null, null, 2, null }
                });

            // Insert tenant modules for Acme Corporation
            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(644), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(644), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(671), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(671), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(709), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(709), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000002"), null, null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(854), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(854), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000002"), null, null }
                });

            // Insert tenant modules for TechStart Inc
            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(644), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(644), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000003"), null, null },
                    { new Guid("20000000-0000-0000-0000-000000000002"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000003"), null, null },
                    { new Guid("20000000-0000-0000-0000-000000000003"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(874), "System", null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(873), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000003"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207));

            // Now we need to add the UserTenant mappings for the admin user
            // We'll use SQL to insert after the users are created by the seeding service
            migrationBuilder.Sql(@"
                -- Insert UserTenant mappings for admin user to all tenants
                DECLARE @AdminUserId UNIQUEIDENTIFIER;
                SELECT @AdminUserId = Id FROM Users WHERE UserName = 'admin';
                
                IF @AdminUserId IS NOT NULL
                BEGIN
                    -- Grant admin access to DEFAULT tenant (set as default)
                    IF NOT EXISTS (SELECT 1 FROM UserTenants WHERE UserId = @AdminUserId AND TenantId = '00000000-0000-0000-0000-000000000001')
                    BEGIN
                        INSERT INTO UserTenants (Id, UserId, TenantId, AccessLevel, Status, IsDefault, GrantedAt, GrantedBy, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @AdminUserId, '00000000-0000-0000-0000-000000000001', 2, 0, 1, GETUTCDATE(), 'System', GETUTCDATE(), 'System', 0);
                    END
                    
                    -- Grant admin access to ACME tenant
                    IF NOT EXISTS (SELECT 1 FROM UserTenants WHERE UserId = @AdminUserId AND TenantId = '00000000-0000-0000-0000-000000000002')
                    BEGIN
                        INSERT INTO UserTenants (Id, UserId, TenantId, AccessLevel, Status, IsDefault, GrantedAt, GrantedBy, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @AdminUserId, '00000000-0000-0000-0000-000000000002', 2, 0, 0, GETUTCDATE(), 'System', GETUTCDATE(), 'System', 0);
                    END
                    
                    -- Grant admin access to TECHSTART tenant
                    IF NOT EXISTS (SELECT 1 FROM UserTenants WHERE UserId = @AdminUserId AND TenantId = '00000000-0000-0000-0000-000000000003')
                    BEGIN
                        INSERT INTO UserTenants (Id, UserId, TenantId, AccessLevel, Status, IsDefault, GrantedAt, GrantedBy, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (NEWID(), @AdminUserId, '00000000-0000-0000-0000-000000000003', 2, 0, 0, GETUTCDATE(), 'System', GETUTCDATE(), 'System', 0);
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1da24716-fd56-44b0-8d2c-b4e7d6850c9b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4643d21c-6433-49b7-9026-66c7039d7351"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a7673475-7689-4253-81ec-62894c352cb9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ac1534db-0b3a-4f1a-8da4-600e8d3609ae"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("acdad383-4d21-4f5b-9fc3-2e16220d7102"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c5f28f1e-92ea-43c7-883c-03ed0485b45b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("df39c1d7-5c64-415d-9485-fe50d7c4f821"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2125));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2165));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2168));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2170));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("5b3f921d-2398-4f29-823e-a3b2e8599967"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2305), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2304), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("89bf11b7-396a-4ad4-8a1b-edf0415b92db"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2265), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2264), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c849318a-78b5-4352-8cc0-e54051706710"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2249), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2248), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c8b67e30-c868-4102-bf56-9d8b0656769d"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2316), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2316), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("cd04d659-03db-43fe-bdfd-67d44972bca3"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2232), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2228), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ea3115bb-a58d-4c0d-92f3-1eebaf5280bd"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2278), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2278), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fdfcf06f-57df-47d2-abae-83d44d1b9a97"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2291), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2291), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            // Remove UserTenant mappings for additional tenants
            migrationBuilder.Sql(@"
                DELETE FROM UserTenants WHERE TenantId IN (
                    '00000000-0000-0000-0000-000000000002',
                    '00000000-0000-0000-0000-000000000003'
                );
            ");

            // Delete additional tenant modules
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("10000000-0000-0000-0000-000000000001") });
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("10000000-0000-0000-0000-000000000002") });
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("10000000-0000-0000-0000-000000000003") });
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("10000000-0000-0000-0000-000000000004") });
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("10000000-0000-0000-0000-000000000005") });
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000001") });
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000002") });
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumns: new[] { "Id" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000003") });

            // Delete additional tenants
            migrationBuilder.DeleteData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));
            migrationBuilder.DeleteData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(1929));
        }
    }
}
