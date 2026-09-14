using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenProtectedSystemRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @ProtectedRoles TABLE
                (
                    [Name] nvarchar(256) NOT NULL,
                    [Description] nvarchar(500) NULL
                );

                INSERT INTO @ProtectedRoles ([Name], [Description])
                VALUES
                    (N'SuperAdmin', N'System Super Administrator with full access'),
                    (N'TenantAdmin', N'Tenant Administrator with tenant-wide access'),
                    (N'Manager', N'Manager with departmental access'),
                    (N'Employee', N'Standard employee with limited access'),
                    (N'ReadOnly', N'Read-only user for restricted system access'),
                    (N'ExternalUser', N'External portal user (customers/vendors/partners/citizens)'),
                    (N'HelpdeskAgent', N'Helpdesk agent for managing tickets'),
                    (N'HelpdeskSupervisor', N'Helpdesk supervisor for assignment and escalation'),
                    (N'HelpdeskManager', N'Helpdesk manager for dashboards and configuration');

                UPDATE roleRecord
                SET
                    [Name] = protectedRole.[Name],
                    [NormalizedName] = UPPER(protectedRole.[Name]),
                    [Description] = COALESCE(roleRecord.[Description], protectedRole.[Description]),
                    [IsSystemRole] = 1,
                    [UpdatedAt] = SYSUTCDATETIME(),
                    [UpdatedBy] = N'System'
                FROM [AspNetRoles] roleRecord
                INNER JOIN @ProtectedRoles protectedRole
                    ON UPPER(LTRIM(RTRIM(COALESCE(roleRecord.[NormalizedName], roleRecord.[Name])))) = UPPER(protectedRole.[Name]);

                INSERT INTO [AspNetRoles]
                    ([Id], [Name], [NormalizedName], [Description], [IsSystemRole], [CreatedAt], [CreatedBy], [ConcurrencyStamp])
                SELECT
                    NEWID(),
                    protectedRole.[Name],
                    UPPER(protectedRole.[Name]),
                    protectedRole.[Description],
                    1,
                    SYSUTCDATETIME(),
                    N'System',
                    CONVERT(nvarchar(36), NEWID())
                FROM @ProtectedRoles protectedRole
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [AspNetRoles] existingRole
                    WHERE UPPER(LTRIM(RTRIM(COALESCE(existingRole.[NormalizedName], existingRole.[Name])))) = UPPER(protectedRole.[Name])
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
