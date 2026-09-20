using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The final settlement — what a leaver is owed and owes back (area 9b slice 5, FR-HR-184).
    /// </summary>
    /// <remarks>
    /// <para>FR-HR-184: <i>"unpaid salary, notice pay, leave encashment, benefits, deductions,
    /// recoveries, loans and pension-related payments"</i>. One statement per separation, assembled
    /// from lines that each say where their amount came from.</para>
    ///
    /// <para>⚠ <b><c>SeparationSettlementLines.Amount</c> is NULLABLE, and that is the point of the
    /// design.</b> Measured on the live tenant 2026-08-20: <b>202 of 3,883</b> employees have a
    /// salary on file and <c>LeaveBalances</c> holds <b>zero</b> rows, so a settlement that only
    /// computed would print 0.00 for almost everybody — and somebody would sign it. A line the
    /// system cannot value carries <c>NULL</c> with <c>Computation = CannotCompute</c>, and holds
    /// the statement open until a person supplies the figure and names its source. Zero is a claim;
    /// null is the truth about what is known.</para>
    ///
    /// <para><b>No stored totals.</b> They are derived from the lines, and a finalised statement's
    /// lines are immutable — so a derived total is a frozen total, without a stored copy that can
    /// quietly disagree with the lines beneath it.</para>
    ///
    /// <para><b>Provenance without ownership:</b> <c>SourceClearanceItemId</c> and
    /// <c>SourceTravelAdvanceId</c> carry no foreign key, so tidying clearance or travel later
    /// cannot orphan a settlement somebody has signed. Same reasoning as clearance items and their
    /// templates.</para>
    ///
    /// <para><b>Currency is Finance's, not HR's.</b> <c>CurrencyCode</c> has no default here: the
    /// service sets it from HR's configured default, validated against Finance's currency master,
    /// falling back to Finance's base currency. Nothing in HR may state a settlement in a currency
    /// Finance does not hold.</para>
    ///
    /// <para>⚠ <b>Nothing here posts to the general ledger.</b> Per the standing HR↔Finance split
    /// the money event is registered in <c>docs/HR-FINANCE-INTEGRATION-BACKLOG.md</c> and posted in
    /// one sweep after the whole HR module.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL, listed in
    /// <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddSeparationSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationSettlements'))
BEGIN
    CREATE TABLE [dbo].[SeparationSettlements] (
        [Id]               uniqueidentifier NOT NULL,
        [SeparationId]     uniqueidentifier NOT NULL,
        [CurrencyCode]     nvarchar(3)      NOT NULL,
        [DailyRate]        decimal(18,4)    NULL,
        [DailyRateBasis]   nvarchar(500)    NULL,
        [PreparedById]     uniqueidentifier NULL,
        [PreparedOn]       datetime2        NULL,
        [FinalisedOn]      datetime2        NULL,
        [FinalisedById]    uniqueidentifier NULL,
        [Notes]            nvarchar(2000)   NULL,
        [CreatedAt]        datetime2        NOT NULL,
        [UpdatedAt]        datetime2        NULL,
        [CreatedBy]        nvarchar(max)    NULL,
        [UpdatedBy]        nvarchar(max)    NULL,
        [CreatedById]      uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted]        bit              NOT NULL CONSTRAINT [DF_SeparationSettlements_IsDeleted] DEFAULT (0),
        [DeletedAt]        datetime2        NULL,
        [DeletedBy]        nvarchar(max)    NULL,
        [TenantId]         uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SeparationSettlements] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlements_EmployeeSeparations_SeparationId')
    ALTER TABLE [dbo].[SeparationSettlements] ADD CONSTRAINT [FK_SeparationSettlements_EmployeeSeparations_SeparationId]
        FOREIGN KEY ([SeparationId]) REFERENCES [dbo].[EmployeeSeparations] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlements_Employees_PreparedById')
    ALTER TABLE [dbo].[SeparationSettlements] ADD CONSTRAINT [FK_SeparationSettlements_Employees_PreparedById]
        FOREIGN KEY ([PreparedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlements_Employees_FinalisedById')
    ALTER TABLE [dbo].[SeparationSettlements] ADD CONSTRAINT [FK_SeparationSettlements_Employees_FinalisedById]
        FOREIGN KEY ([FinalisedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlements_Tenants_TenantId')
    ALTER TABLE [dbo].[SeparationSettlements] ADD CONSTRAINT [FK_SeparationSettlements_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SeparationSettlement_SeparationId' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    CREATE UNIQUE INDEX [UX_SeparationSettlement_SeparationId] ON [dbo].[SeparationSettlements] ([SeparationId]) WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlements_PreparedById' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    CREATE INDEX [IX_SeparationSettlements_PreparedById] ON [dbo].[SeparationSettlements] ([PreparedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlements_FinalisedById' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    CREATE INDEX [IX_SeparationSettlements_FinalisedById] ON [dbo].[SeparationSettlements] ([FinalisedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlements_TenantId' AND object_id = OBJECT_ID('dbo.SeparationSettlements'))
    CREATE INDEX [IX_SeparationSettlements_TenantId] ON [dbo].[SeparationSettlements] ([TenantId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationSettlementLines'))
BEGIN
    CREATE TABLE [dbo].[SeparationSettlementLines] (
        [Id]                     uniqueidentifier NOT NULL,
        [SettlementId]           uniqueidentifier NOT NULL,
        [Category]               int              NOT NULL,
        [IsDeduction]            bit              NOT NULL CONSTRAINT [DF_SeparationSettlementLines_IsDeduction] DEFAULT (0),
        [Description]            nvarchar(300)    NOT NULL,
        [Amount]                 decimal(18,2)    NULL,
        [Computation]            int              NOT NULL CONSTRAINT [DF_SeparationSettlementLines_Computation] DEFAULT (2),
        [Basis]                  nvarchar(500)    NULL,
        [SourceReference]        nvarchar(300)    NULL,
        [SourceClearanceItemId]  uniqueidentifier NULL,
        [SourceTravelAdvanceId]  uniqueidentifier NULL,
        [IsSystemGenerated]      bit              NOT NULL CONSTRAINT [DF_SeparationSettlementLines_IsSystemGenerated] DEFAULT (0),
        [SortOrder]              int              NOT NULL CONSTRAINT [DF_SeparationSettlementLines_SortOrder] DEFAULT (0),
        [CreatedAt]              datetime2        NOT NULL,
        [UpdatedAt]              datetime2        NULL,
        [CreatedBy]              nvarchar(max)    NULL,
        [UpdatedBy]              nvarchar(max)    NULL,
        [CreatedById]            uniqueidentifier NULL,
        [LastModifiedById]       uniqueidentifier NULL,
        [IsDeleted]              bit              NOT NULL CONSTRAINT [DF_SeparationSettlementLines_IsDeleted] DEFAULT (0),
        [DeletedAt]              datetime2        NULL,
        [DeletedBy]              nvarchar(max)    NULL,
        [TenantId]               uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SeparationSettlementLines] PRIMARY KEY ([Id])
    );
END");

            // ⚠ No foreign key on SourceClearanceItemId or SourceTravelAdvanceId — provenance, not
            // ownership. See the remarks on this class.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlementLines_SeparationSettlements_SettlementId')
    ALTER TABLE [dbo].[SeparationSettlementLines] ADD CONSTRAINT [FK_SeparationSettlementLines_SeparationSettlements_SettlementId]
        FOREIGN KEY ([SettlementId]) REFERENCES [dbo].[SeparationSettlements] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SeparationSettlementLines_Tenants_TenantId')
    ALTER TABLE [dbo].[SeparationSettlementLines] ADD CONSTRAINT [FK_SeparationSettlementLines_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlementLine_SettlementId' AND object_id = OBJECT_ID('dbo.SeparationSettlementLines'))
    CREATE INDEX [IX_SeparationSettlementLine_SettlementId] ON [dbo].[SeparationSettlementLines] ([SettlementId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlementLine_Category' AND object_id = OBJECT_ID('dbo.SeparationSettlementLines'))
    CREATE INDEX [IX_SeparationSettlementLine_Category] ON [dbo].[SeparationSettlementLines] ([Category]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeparationSettlementLines_TenantId' AND object_id = OBJECT_ID('dbo.SeparationSettlementLines'))
    CREATE INDEX [IX_SeparationSettlementLines_TenantId] ON [dbo].[SeparationSettlementLines] ([TenantId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationSettlementLines'))
    DROP TABLE [dbo].[SeparationSettlementLines];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.SeparationSettlements'))
    DROP TABLE [dbo].[SeparationSettlements];");
        }
    }
}
