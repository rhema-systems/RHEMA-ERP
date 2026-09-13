using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    // Keep discovery metadata on the executable migration because routine Debug builds omit the
    // multi-megabyte generated designer. Application startup must still discover and apply this
    // control table before the reopen endpoints can be used.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803110029_AddFinancePeriodReopenApprovals")]
    /// <inheritdoc />
    public partial class AddFinancePeriodReopenApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinancePeriodReopenRequestId",
                table: "FinanceCloseAlertDeliveries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancePeriodReopenRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AffectedPeriodAssessment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImpactSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImpactFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AffectedPeriodCount = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResultingFinanceCloseCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_FinancePeriodReopenRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancePeriodReopenRequests_FinanceCloseCycles_FinanceCloseCycleId",
                        column: x => x.FinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePeriodReopenRequests_FinanceCloseCycles_ResultingFinanceCloseCycleId",
                        column: x => x.ResultingFinanceCloseCycleId,
                        principalTable: "FinanceCloseCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePeriodReopenRequests_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancePeriodReopenRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCloseAlertDeliveries_FinancePeriodReopenRequestId",
                table: "FinanceCloseAlertDeliveries",
                column: "FinancePeriodReopenRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeriodReopenRequests_FinanceCloseCycleId",
                table: "FinancePeriodReopenRequests",
                column: "FinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeriodReopenRequests_FiscalPeriodId",
                table: "FinancePeriodReopenRequests",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeriodReopenRequests_ResultingFinanceCloseCycleId",
                table: "FinancePeriodReopenRequests",
                column: "ResultingFinanceCloseCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeriodReopenRequests_TenantId_FiscalPeriodId",
                table: "FinancePeriodReopenRequests",
                columns: new[] { "TenantId", "FiscalPeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] = 'PendingApproval'");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePeriodReopenRequests_TenantId_Status_RequestedAt",
                table: "FinancePeriodReopenRequests",
                columns: new[] { "TenantId", "Status", "RequestedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceCloseAlertDeliveries_FinancePeriodReopenRequests_FinancePeriodReopenRequestId",
                table: "FinanceCloseAlertDeliveries",
                column: "FinancePeriodReopenRequestId",
                principalTable: "FinancePeriodReopenRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Seed the new action permission in the same deployment that introduces its endpoint.
            // The ordinary application seeder remains the source for future tenants/roles; this
            // idempotent SQL also upgrades an existing TDC database before the next API restart.
            migrationBuilder.Sql("""
                DECLARE @PermissionId uniqueidentifier =
                    (SELECT TOP (1) [Id] FROM [Permissions] WHERE [Name] = N'Finance.PeriodReopen.Approve');

                IF @PermissionId IS NULL
                BEGIN
                    SET @PermissionId = NEWID();
                    INSERT INTO [Permissions]
                        ([Id], [Name], [DisplayName], [Description], [Category], [IsSystemPermission],
                         [CreatedAt], [CreatedBy], [IsDeleted])
                    VALUES
                        (@PermissionId, N'Finance.PeriodReopen.Approve', N'Approve Accounting Period Reopens',
                         N'Independently approve or reject controlled accounting-period reopen requests.',
                         N'Finance - Period Close', 1, SYSUTCDATETIME(), N'Finance reopen approval migration', 0);
                END;

                INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [GrantedAt], [GrantedBy])
                SELECT [role].[Id], @PermissionId, SYSUTCDATETIME(), N'Finance reopen approval migration'
                FROM [AspNetRoles] AS [role]
                WHERE [role].[Name] IN (N'Chief Accountant', N'Financial Controller', N'SuperAdmin', N'TenantAdmin')
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [RolePermissions] AS [existing]
                      WHERE [existing].[RoleId] = [role].[Id]
                        AND [existing].[PermissionId] = @PermissionId
                  );

                -- TDC's maker role already uses the long-standing reopen permission. Seed the
                -- role grant explicitly for an existing database; future provisioning repeats the
                -- same mapping through DatabaseSeedingService.
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [GrantedAt], [GrantedBy])
                SELECT [role].[Id], [permission].[Id], SYSUTCDATETIME(), N'Finance reopen approval migration'
                FROM [AspNetRoles] AS [role]
                CROSS JOIN [Permissions] AS [permission]
                WHERE [role].[Name] = N'Finance Manager'
                  AND [permission].[Name] = N'Finance.PeriodReopen'
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [RolePermissions] AS [existing]
                      WHERE [existing].[RoleId] = [role].[Id]
                        AND [existing].[PermissionId] = [permission].[Id]
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @PermissionId uniqueidentifier =
                    (SELECT TOP (1) [Id] FROM [Permissions] WHERE [Name] = N'Finance.PeriodReopen.Approve');
                IF @PermissionId IS NOT NULL
                BEGIN
                    DELETE FROM [RolePermissions] WHERE [PermissionId] = @PermissionId;
                    DELETE FROM [Permissions] WHERE [Id] = @PermissionId;
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceCloseAlertDeliveries_FinancePeriodReopenRequests_FinancePeriodReopenRequestId",
                table: "FinanceCloseAlertDeliveries");

            migrationBuilder.DropTable(
                name: "FinancePeriodReopenRequests");

            migrationBuilder.DropIndex(
                name: "IX_FinanceCloseAlertDeliveries_FinancePeriodReopenRequestId",
                table: "FinanceCloseAlertDeliveries");

            migrationBuilder.DropColumn(
                name: "FinancePeriodReopenRequestId",
                table: "FinanceCloseAlertDeliveries");
        }
    }
}
