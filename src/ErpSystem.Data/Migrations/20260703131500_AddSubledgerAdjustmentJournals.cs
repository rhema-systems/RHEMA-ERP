using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260703131500_AddSubledgerAdjustmentJournals")]
    public partial class AddSubledgerAdjustmentJournals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SubledgerAdjustmentJournals] (
                        [Id] uniqueidentifier NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [Module] nvarchar(2) NOT NULL,
                        [AdjustmentNumber] nvarchar(50) NOT NULL,
                        [CustomerId] uniqueidentifier NULL,
                        [SupplierId] uniqueidentifier NULL,
                        [AdjustmentDate] datetime2 NOT NULL,
                        [DueDate] datetime2 NULL,
                        [AdjustmentType] nvarchar(10) NOT NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [CurrencyCode] nvarchar(3) NOT NULL,
                        [ExchangeRate] decimal(18,6) NOT NULL,
                        [BaseCurrencyAmount] decimal(18,2) NOT NULL,
                        [ContraAccountId] uniqueidentifier NOT NULL,
                        [JournalEntryId] uniqueidentifier NULL,
                        [Status] nvarchar(20) NOT NULL,
                        [Reference] nvarchar(100) NULL,
                        [Reason] nvarchar(500) NOT NULL,
                        [Notes] nvarchar(2000) NULL,
                        [OriginalAdjustmentId] uniqueidentifier NULL,
                        [ReversalAdjustmentId] uniqueidentifier NULL,
                        [ReversalReason] nvarchar(500) NULL,
                        [ReversedAt] datetime2 NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_SubledgerAdjustmentJournals] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_SubledgerAdjustmentJournals_Accounts_ContraAccountId] FOREIGN KEY ([ContraAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_SubledgerAdjustmentJournals_BusinessPartners_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[BusinessPartners] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_SubledgerAdjustmentJournals_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_SubledgerAdjustmentJournals_SubledgerAdjustmentJournals_OriginalAdjustmentId] FOREIGN KEY ([OriginalAdjustmentId]) REFERENCES [dbo].[SubledgerAdjustmentJournals] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_SubledgerAdjustmentJournals_SubledgerAdjustmentJournals_ReversalAdjustmentId] FOREIGN KEY ([ReversalAdjustmentId]) REFERENCES [dbo].[SubledgerAdjustmentJournals] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_SubledgerAdjustmentJournals_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_SubledgerAdjustmentJournals_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
                    );

                    CREATE INDEX [IX_SubledgerAdjustmentJournals_ContraAccountId] ON [dbo].[SubledgerAdjustmentJournals] ([ContraAccountId]);
                    CREATE INDEX [IX_SubledgerAdjustmentJournals_CustomerId] ON [dbo].[SubledgerAdjustmentJournals] ([CustomerId]);
                    CREATE INDEX [IX_SubledgerAdjustmentJournals_JournalEntryId] ON [dbo].[SubledgerAdjustmentJournals] ([JournalEntryId]);
                    CREATE INDEX [IX_SubledgerAdjustmentJournals_OriginalAdjustmentId] ON [dbo].[SubledgerAdjustmentJournals] ([OriginalAdjustmentId]);
                    CREATE INDEX [IX_SubledgerAdjustmentJournals_ReversalAdjustmentId] ON [dbo].[SubledgerAdjustmentJournals] ([ReversalAdjustmentId]);
                    CREATE INDEX [IX_SubledgerAdjustmentJournals_SupplierId] ON [dbo].[SubledgerAdjustmentJournals] ([SupplierId]);
                    CREATE INDEX [IX_SubledgerAdjustmentJournals_TenantId_Module_AdjustmentDate] ON [dbo].[SubledgerAdjustmentJournals] ([TenantId], [Module], [AdjustmentDate]);
                    CREATE UNIQUE INDEX [IX_SubledgerAdjustmentJournals_TenantId_Module_AdjustmentNumber] ON [dbo].[SubledgerAdjustmentJournals] ([TenantId], [Module], [AdjustmentNumber]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[SubledgerAdjustmentJournals]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[SubledgerAdjustmentJournals];
                END
                """);
        }
    }
}
