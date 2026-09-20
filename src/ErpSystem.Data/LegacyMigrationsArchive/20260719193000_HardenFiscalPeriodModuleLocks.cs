using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260719193000_HardenFiscalPeriodModuleLocks")]
public partial class HardenFiscalPeriodModuleLocks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsGlobalLockSuspended",
            table: "FiscalPeriods",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "AutoRelockedDate",
            table: "PeriodModuleLock",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExpiryWarningSentAtUtc",
            table: "PeriodModuleLock",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReopenExpiresAtUtc",
            table: "PeriodModuleLock",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OriginModuleCode",
            table: "JournalEntries",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OriginModuleCode",
            table: "FinancePostingEvents",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE [dbo].[ModuleDefinitions]
            SET [IsActive] = 0,
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'System'
            WHERE [ModuleCode] IN (N'FA', N'MNT', N'PROJ', N'MFG')
              AND [IsActive] = 1;

            UPDATE [dbo].[TransactionDocumentModuleMappings]
            SET [IsActive] = 0,
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'System'
            WHERE [IsActive] = 1;

            MERGE [dbo].[ModuleDefinitions] AS target
            USING
            (
                SELECT tenant.[Id] AS [TenantId], module.[ModuleCode], module.[ModuleName],
                       module.[Description], module.[IconClass], module.[SortOrder]
                FROM [dbo].[Tenants] tenant
                CROSS APPLY (VALUES
                    (N'FIN', N'Finance', N'Finance-originated postings including GL, AP, AR, Cash & Bank, Fixed Assets, tax and FX.', N'fa-calculator', 1),
                    (N'INV', N'Inventory', N'Accounting postings originating from the Inventory module.', N'fa-boxes', 2),
                    (N'PROC', N'Procurement', N'Accounting postings originating from the Procurement module.', N'fa-shopping-cart', 3),
                    (N'SALES', N'Sales', N'Accounting postings originating from the Sales module.', N'fa-chart-line', 4),
                    (N'HR', N'Human Resources', N'Accounting postings originating from Human Resources, including payroll.', N'fa-users', 5)
                ) module([ModuleCode], [ModuleName], [Description], [IconClass], [SortOrder])
                WHERE tenant.[IsDeleted] = 0
            ) AS source
            ON target.[TenantId] = source.[TenantId]
               AND target.[ModuleCode] = source.[ModuleCode]
            WHEN MATCHED THEN
                UPDATE SET target.[ModuleName] = source.[ModuleName],
                           target.[Description] = source.[Description],
                           target.[IconClass] = source.[IconClass],
                           target.[SortOrder] = source.[SortOrder],
                           target.[IsActive] = 1,
                           target.[IsSystem] = 1,
                           target.[IsDeleted] = 0,
                           target.[DeletedAt] = NULL,
                           target.[DeletedBy] = NULL,
                           target.[UpdatedAt] = SYSUTCDATETIME(),
                           target.[UpdatedBy] = N'System'
            WHEN NOT MATCHED BY TARGET THEN
                INSERT ([Id], [TenantId], [ModuleCode], [ModuleName], [Description], [SortOrder],
                        [IsActive], [IsSystem], [IconClass], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES (NEWID(), source.[TenantId], source.[ModuleCode], source.[ModuleName], source.[Description],
                        source.[SortOrder], 1, 1, source.[IconClass], SYSUTCDATETIME(), N'System', 0);

            UPDATE [dbo].[FinancePostingEvents]
            SET [OriginModuleCode] = CASE
                WHEN UPPER([SourceDocumentType]) LIKE N'SALES%' THEN N'SALES'
                WHEN UPPER([SourceModule]) = N'PAYROLL' THEN N'HR'
                WHEN UPPER([SourceModule]) IN (N'INV', N'INVENTORY') THEN N'INV'
                WHEN UPPER([SourceModule]) IN (N'PROC', N'PROCUREMENT') THEN N'PROC'
                WHEN UPPER([SourceModule]) = N'SALES' THEN N'SALES'
                ELSE N'FIN'
            END
            WHERE [OriginModuleCode] IS NULL;

            UPDATE [dbo].[JournalEntries]
            SET [OriginModuleCode] = CASE
                WHEN UPPER([SourceDocumentType]) LIKE N'SALES%' THEN N'SALES'
                WHEN UPPER([SourceModule]) = N'PAYROLL' THEN N'HR'
                WHEN UPPER([SourceModule]) IN (N'INV', N'INVENTORY') THEN N'INV'
                WHEN UPPER([SourceModule]) IN (N'PROC', N'PROCUREMENT') THEN N'PROC'
                WHEN UPPER([SourceModule]) = N'SALES' THEN N'SALES'
                ELSE N'FIN'
            END
            WHERE [OriginModuleCode] IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_ModuleDefinitions_TenantId_ModuleCode",
            table: "ModuleDefinitions",
            columns: new[] { "TenantId", "ModuleCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PeriodModuleLock_IsLocked_ReopenExpiresAtUtc",
            table: "PeriodModuleLock",
            columns: new[] { "IsLocked", "ReopenExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_PeriodModuleLock_TenantId_FiscalPeriodId_ModuleDefinitionId",
            table: "PeriodModuleLock",
            columns: new[] { "TenantId", "FiscalPeriodId", "ModuleDefinitionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TransactionDocumentModuleMappings_TenantId_DocumentType",
            table: "TransactionDocumentModuleMappings",
            columns: new[] { "TenantId", "DocumentType" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ModuleDefinitions_TenantId_ModuleCode",
            table: "ModuleDefinitions");

        migrationBuilder.DropIndex(
            name: "IX_PeriodModuleLock_IsLocked_ReopenExpiresAtUtc",
            table: "PeriodModuleLock");

        migrationBuilder.DropIndex(
            name: "IX_PeriodModuleLock_TenantId_FiscalPeriodId_ModuleDefinitionId",
            table: "PeriodModuleLock");

        migrationBuilder.DropIndex(
            name: "IX_TransactionDocumentModuleMappings_TenantId_DocumentType",
            table: "TransactionDocumentModuleMappings");

        migrationBuilder.DropColumn(name: "IsGlobalLockSuspended", table: "FiscalPeriods");
        migrationBuilder.DropColumn(name: "AutoRelockedDate", table: "PeriodModuleLock");
        migrationBuilder.DropColumn(name: "ExpiryWarningSentAtUtc", table: "PeriodModuleLock");
        migrationBuilder.DropColumn(name: "ReopenExpiresAtUtc", table: "PeriodModuleLock");
        migrationBuilder.DropColumn(name: "OriginModuleCode", table: "JournalEntries");
        migrationBuilder.DropColumn(name: "OriginModuleCode", table: "FinancePostingEvents");
    }
}
