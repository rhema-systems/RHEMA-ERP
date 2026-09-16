using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Installs the tender-fee payment verification permission and grants it to the
/// two operational TDC procurement roles that own independent payment checks.
/// The SQL is idempotent so it also repairs an existing deployment where the
/// application catalogue was updated before the database permission catalogue.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260831194500_GrantTenderPaymentVerificationPermission")]
public class GrantTenderPaymentVerificationPermission : Migration
{
    private const string MigrationActor =
        "Tender payment verification permission migration";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DECLARE @PermissionId uniqueidentifier =
                (SELECT TOP (1) [Id]
                 FROM [Permissions]
                 WHERE [Name] = N'procurement.tender.payment.verify');

            IF @PermissionId IS NULL
            BEGIN
                SET @PermissionId = NEWID();
                INSERT INTO [Permissions]
                    ([Id], [Name], [DisplayName], [Description], [Category],
                     [IsSystemPermission], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@PermissionId,
                     N'procurement.tender.payment.verify',
                     N'Verify tender fee payments',
                     N'Independently verify or reject supplier tender-fee payment claims before bid submission.',
                     N'TDC Procurement',
                     1,
                     SYSUTCDATETIME(),
                     N'{{MigrationActor}}',
                     0);
            END
            ELSE
            BEGIN
                UPDATE [Permissions]
                SET [DisplayName] = N'Verify tender fee payments',
                    [Description] = N'Independently verify or reject supplier tender-fee payment claims before bid submission.',
                    [Category] = N'TDC Procurement',
                    [IsSystemPermission] = 1,
                    [IsDeleted] = 0,
                    [DeletedAt] = NULL,
                    [DeletedBy] = NULL
                WHERE [Id] = @PermissionId;
            END;

            INSERT INTO [RolePermissions]
                ([RoleId], [PermissionId], [GrantedAt], [GrantedBy])
            SELECT [role].[Id], @PermissionId, SYSUTCDATETIME(), N'{{MigrationActor}}'
            FROM [AspNetRoles] AS [role]
            WHERE [role].[Name] IN
                  (N'TDC_PROCUREMENT_OFFICER', N'TDC_SENIOR_PROCUREMENT_OFFICER')
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [RolePermissions] AS [existing]
                  WHERE [existing].[RoleId] = [role].[Id]
                    AND [existing].[PermissionId] = @PermissionId
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DECLARE @PermissionId uniqueidentifier =
                (SELECT TOP (1) [Id]
                 FROM [Permissions]
                 WHERE [Name] = N'procurement.tender.payment.verify');

            IF @PermissionId IS NOT NULL
            BEGIN
                DELETE [rolePermission]
                FROM [RolePermissions] AS [rolePermission]
                INNER JOIN [AspNetRoles] AS [role]
                    ON [role].[Id] = [rolePermission].[RoleId]
                WHERE [rolePermission].[PermissionId] = @PermissionId
                  AND [role].[Name] IN
                      (N'TDC_PROCUREMENT_OFFICER', N'TDC_SENIOR_PROCUREMENT_OFFICER')
                  AND [rolePermission].[GrantedBy] = N'{{MigrationActor}}';

                DELETE FROM [Permissions]
                WHERE [Id] = @PermissionId
                  AND [CreatedBy] = N'{{MigrationActor}}'
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM [RolePermissions]
                      WHERE [PermissionId] = @PermissionId
                  );
            END;
            """);
    }
}
