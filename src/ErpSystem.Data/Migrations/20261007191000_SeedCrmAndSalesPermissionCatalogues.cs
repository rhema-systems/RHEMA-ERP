using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Makes the CRM and Sales policy permissions available in production databases so an
/// administrator can assign them to any dynamically named role through Identity Management.
/// Development seeding is deliberately not relied on because production hosts do not run the
/// development-data seeder.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261007191000_SeedCrmAndSalesPermissionCatalogues")]
public sealed class SeedCrmAndSalesPermissionCatalogues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @PermissionCatalogue TABLE
            (
                [Name] nvarchar(100) NOT NULL,
                [DisplayName] nvarchar(200) NOT NULL,
                [Description] nvarchar(500) NOT NULL,
                [Category] nvarchar(100) NOT NULL
            );

            INSERT INTO @PermissionCatalogue ([Name], [DisplayName], [Description], [Category])
            VALUES
                (N'crm.access', N'Access CRM', N'Access CRM workspaces and navigation.', N'CRM'),
                (N'crm.read', N'View CRM', N'View CRM accounts, leads, opportunities, activities, and opportunity stages.', N'CRM'),
                (N'crm.manage', N'Manage CRM', N'Manage CRM records and opportunity-stage configuration.', N'CRM'),
                (N'sales.access', N'Access Sales', N'Access Sales workspaces and navigation.', N'Sales'),
                (N'sales.read', N'View Sales', N'View Sales records, transactions, and operational status.', N'Sales'),
                (N'sales.manage', N'Manage Sales', N'Create and maintain Sales records and transactions.', N'Sales'),
                (N'sales.approve', N'Approve Sales', N'Approve governed Sales transactions and commercial decisions.', N'Sales'),
                (N'sales.configure', N'Configure Sales', N'Manage Sales setup, sources, templates, and reference configuration.', N'Sales'),
                (N'sales.reports.read', N'View Sales Reports', N'View Sales reports, summaries, forecasts, and analytics.', N'Sales');

            INSERT INTO [Permissions]
                ([Id], [Name], [DisplayName], [Description], [Category], [IsSystemPermission], [IsDeleted], [CreatedAt], [CreatedBy])
            SELECT NEWID(), source.[Name], source.[DisplayName], source.[Description], source.[Category],
                   1, 0, SYSUTCDATETIME(), N'CRM and Sales permission catalogue migration'
            FROM @PermissionCatalogue source
            WHERE NOT EXISTS
            (
                SELECT 1 FROM [Permissions] existing WHERE existing.[Name] = source.[Name]
            );

            UPDATE existing
            SET existing.[DisplayName] = source.[DisplayName],
                existing.[Description] = source.[Description],
                existing.[Category] = source.[Category],
                existing.[IsSystemPermission] = 1,
                existing.[IsDeleted] = 0,
                existing.[DeletedAt] = NULL,
                existing.[DeletedBy] = NULL,
                existing.[UpdatedAt] = SYSUTCDATETIME(),
                existing.[UpdatedBy] = N'CRM and Sales permission catalogue migration'
            FROM [Permissions] existing
            JOIN @PermissionCatalogue source ON source.[Name] = existing.[Name];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Role grants may have been assigned by administrators after this migration. Retain the
        // catalogue and those grants during rollback rather than silently revoking live access.
    }
}
