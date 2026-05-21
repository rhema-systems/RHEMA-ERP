using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260519121500_PointToolCheckoutsToEmployees")]
    public partial class PointToolCheckoutsToEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FK_ToolCheckouts_Users_CheckedInById]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ToolCheckouts] DROP CONSTRAINT [FK_ToolCheckouts_Users_CheckedInById];

                IF OBJECT_ID(N'[dbo].[FK_ToolCheckouts_Users_CheckedOutById]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ToolCheckouts] DROP CONSTRAINT [FK_ToolCheckouts_Users_CheckedOutById];

                IF OBJECT_ID(N'[dbo].[FK_ToolCheckouts_Employees_CheckedInById]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ToolCheckouts] DROP CONSTRAINT [FK_ToolCheckouts_Employees_CheckedInById];

                IF OBJECT_ID(N'[dbo].[FK_ToolCheckouts_Employees_CheckedOutById]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ToolCheckouts] DROP CONSTRAINT [FK_ToolCheckouts_Employees_CheckedOutById];

                UPDATE tc
                SET CheckedOutById = u.EmployeeId
                FROM dbo.ToolCheckouts tc
                INNER JOIN dbo.Users u ON u.Id = tc.CheckedOutById
                WHERE u.EmployeeId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.Employees emp
                      WHERE emp.Id = tc.CheckedOutById
                  );

                UPDATE tc
                SET CheckedInById = u.EmployeeId
                FROM dbo.ToolCheckouts tc
                INNER JOIN dbo.Users u ON u.Id = tc.CheckedInById
                WHERE tc.CheckedInById IS NOT NULL
                  AND u.EmployeeId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.Employees emp
                      WHERE emp.Id = tc.CheckedInById
                  );

                UPDATE tc
                SET CheckedOutById = fallback.Id
                FROM dbo.ToolCheckouts tc
                CROSS APPLY (
                    SELECT TOP (1) emp.Id
                    FROM dbo.Employees emp
                    WHERE emp.TenantId = tc.TenantId
                      AND emp.IsDeleted = 0
                    ORDER BY emp.IsActive DESC, emp.CreatedAt
                ) fallback
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.Employees emp
                    WHERE emp.Id = tc.CheckedOutById
                );

                UPDATE tc
                SET CheckedInById = NULL
                FROM dbo.ToolCheckouts tc
                WHERE tc.CheckedInById IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.Employees emp
                      WHERE emp.Id = tc.CheckedInById
                  );

                ALTER TABLE [dbo].[ToolCheckouts]
                    ADD CONSTRAINT [FK_ToolCheckouts_Employees_CheckedOutById]
                    FOREIGN KEY ([CheckedOutById]) REFERENCES [dbo].[Employees] ([Id]);

                ALTER TABLE [dbo].[ToolCheckouts]
                    ADD CONSTRAINT [FK_ToolCheckouts_Employees_CheckedInById]
                    FOREIGN KEY ([CheckedInById]) REFERENCES [dbo].[Employees] ([Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FK_ToolCheckouts_Employees_CheckedInById]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ToolCheckouts] DROP CONSTRAINT [FK_ToolCheckouts_Employees_CheckedInById];

                IF OBJECT_ID(N'[dbo].[FK_ToolCheckouts_Employees_CheckedOutById]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ToolCheckouts] DROP CONSTRAINT [FK_ToolCheckouts_Employees_CheckedOutById];

                UPDATE tc
                SET CheckedOutById = u.Id
                FROM dbo.ToolCheckouts tc
                INNER JOIN dbo.Users u ON u.EmployeeId = tc.CheckedOutById
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM dbo.Users existingUser
                    WHERE existingUser.Id = tc.CheckedOutById
                );

                UPDATE tc
                SET CheckedInById = u.Id
                FROM dbo.ToolCheckouts tc
                INNER JOIN dbo.Users u ON u.EmployeeId = tc.CheckedInById
                WHERE tc.CheckedInById IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.Users existingUser
                      WHERE existingUser.Id = tc.CheckedInById
                  );

                UPDATE tc
                SET CheckedInById = NULL
                FROM dbo.ToolCheckouts tc
                WHERE tc.CheckedInById IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.Users u
                      WHERE u.Id = tc.CheckedInById
                  );

                ALTER TABLE [dbo].[ToolCheckouts]
                    ADD CONSTRAINT [FK_ToolCheckouts_Users_CheckedOutById]
                    FOREIGN KEY ([CheckedOutById]) REFERENCES [dbo].[Users] ([Id]);

                ALTER TABLE [dbo].[ToolCheckouts]
                    ADD CONSTRAINT [FK_ToolCheckouts_Users_CheckedInById]
                    FOREIGN KEY ([CheckedInById]) REFERENCES [dbo].[Users] ([Id]);
                """);
        }
    }
}
