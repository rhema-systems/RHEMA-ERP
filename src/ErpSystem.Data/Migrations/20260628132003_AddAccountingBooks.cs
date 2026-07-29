using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260628132003_AddAccountingBooks")]
    public partial class AddAccountingBooks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AccountingBooks] (
                        [Id] uniqueidentifier NOT NULL,
                        [Code] nvarchar(20) NOT NULL,
                        [Name] nvarchar(100) NOT NULL,
                        [Description] nvarchar(500) NULL,
                        [Purpose] nvarchar(50) NOT NULL,
                        [IsActive] bit NOT NULL,
                        [IsDefault] bit NOT NULL,
                        [AllowsPosting] bit NOT NULL,
                        [IsSystemDefined] bit NOT NULL,
                        [SortOrder] int NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [UpdatedAt] datetime2 NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_AccountingBooks] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_AccountingBooks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
                    );
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = N'IX_AccountingBooks_TenantId_Code'
                        AND [object_id] = OBJECT_ID(N'[dbo].[AccountingBooks]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_AccountingBooks_TenantId_Code]
                        ON [dbo].[AccountingBooks] ([TenantId], [Code]);
                END

                IF OBJECT_ID(N'[dbo].[AccountAccountingBooks]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[AccountAccountingBooks] (
                        [Id] uniqueidentifier NOT NULL,
                        [AccountId] uniqueidentifier NOT NULL,
                        [AccountingBookId] uniqueidentifier NOT NULL,
                        [IsEnabled] bit NOT NULL,
                        [FinancialStatementLineItem] nvarchar(100) NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [UpdatedAt] datetime2 NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_AccountAccountingBooks] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_AccountAccountingBooks_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_AccountAccountingBooks_AccountingBooks_AccountingBookId] FOREIGN KEY ([AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_AccountAccountingBooks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
                    );
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = N'IX_AccountAccountingBooks_AccountId'
                        AND [object_id] = OBJECT_ID(N'[dbo].[AccountAccountingBooks]')
                )
                BEGIN
                    CREATE INDEX [IX_AccountAccountingBooks_AccountId]
                        ON [dbo].[AccountAccountingBooks] ([AccountId]);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = N'IX_AccountAccountingBooks_AccountingBookId'
                        AND [object_id] = OBJECT_ID(N'[dbo].[AccountAccountingBooks]')
                )
                BEGIN
                    CREATE INDEX [IX_AccountAccountingBooks_AccountingBookId]
                        ON [dbo].[AccountAccountingBooks] ([AccountingBookId]);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = N'IX_AccountAccountingBooks_TenantId_AccountId_AccountingBookId'
                        AND [object_id] = OBJECT_ID(N'[dbo].[AccountAccountingBooks]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_AccountAccountingBooks_TenantId_AccountId_AccountingBookId]
                        ON [dbo].[AccountAccountingBooks] ([TenantId], [AccountId], [AccountingBookId]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AccountAccountingBooks]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[AccountAccountingBooks];

                IF OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NOT NULL
                    DROP TABLE [dbo].[AccountingBooks];
                """);
        }
    }
}
