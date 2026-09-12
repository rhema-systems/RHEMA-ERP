using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 7. Two tables: <c>AssetSurcharges</c> — a charge raised against an employee for
    /// a company asset they damaged, lost or never returned (AST-3, defect D-d) — and
    /// <c>AssetSurchargeRecoveries</c>, one row per amount actually collected against it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a table and not five more columns on <c>AssetAssignments</c>.</b> That row already
    /// carries <c>EmployeeLiable</c>, <c>RepairCost</c> and <c>ReplacementCost</c> — facts about an
    /// <i>asset</i>: what it would cost to mend or replace. A surcharge is a decision about a
    /// <i>person</i>: that this employee owes this amount, taken by somebody, on a date, after they
    /// were given the chance to answer. An employer routinely charges less than the repair cost, so
    /// the two are not the same number and neither is derivable from the other. Both cost figures
    /// are copied onto the charge as <c>BasisRepairCost</c> / <c>BasisReplacementCost</c> so that a
    /// decision to charge less stays visible as one.
    /// </para>
    /// <para>
    /// <b>The right of reply is columns, not convention.</b> <c>NotifiedAt</c>,
    /// <c>EmployeeResponse</c>, <c>EmployeeRespondedAt</c> and <c>EmployeeResponseComments</c>
    /// record that the charge was put to the employee and what they said;
    /// <c>ProceededWithoutResponseReason</c> records the decision to go for approval when they never
    /// answered. Silence must not be a veto, and must not be invisible either — decision D9.
    /// </para>
    /// <para>
    /// <b>No deduction lives here.</b> <c>RecoveryMethod</c>, <c>InstalmentCount</c> and
    /// <c>RecoveryStartDate</c> are a declaration payroll consumes; the recoveries table records
    /// what somebody else collected. No GL posting — registered in
    /// <c>docs/HR-FINANCE-INTEGRATION-BACKLOG.md</c> (16.1–16.5) for the sweep after the module.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — two <c>CreateTable</c> calls, nine indexes and nine
    /// foreign keys, with nothing inferred and nothing dropped. (⚠ Worth confirming rather than
    /// assuming: EF inferred a wrong <c>RENAME</c> in this very area at slice 3.) Rewritten as
    /// guarded SQL only to match the surrounding HR migrations, so it is safe to re-run against a
    /// database at either state — including one built from the EF model rather than from the chain,
    /// which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddAssetSurchargeAndIncident : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetSurcharges', 'U') IS NULL
CREATE TABLE [dbo].[AssetSurcharges] (
    [Id]                             uniqueidentifier NOT NULL,
    [SurchargeNumber]                nvarchar(70)     NOT NULL,
    [AssignmentId]                   uniqueidentifier NOT NULL,
    [EmployeeId]                     uniqueidentifier NOT NULL,
    [Reason]                         int              NOT NULL,
    [Description]                    nvarchar(2000)   NOT NULL,
    [AssessedAmount]                 decimal(18,2)    NOT NULL,
    [CurrencyCode]                   nvarchar(3)      NOT NULL,
    [BasisRepairCost]                decimal(18,2)    NULL,
    [BasisReplacementCost]           decimal(18,2)    NULL,
    [AmountRecovered]                decimal(18,2)    NOT NULL,
    [Status]                         int              NOT NULL,
    [RaisedById]                     uniqueidentifier NULL,
    [RaisedAt]                       datetime2        NOT NULL,
    [NotifiedAt]                     datetime2        NULL,
    [EmployeeResponse]               int              NOT NULL,
    [EmployeeRespondedAt]            datetime2        NULL,
    [EmployeeResponseComments]       nvarchar(2000)   NULL,
    [ProceededWithoutResponseReason] nvarchar(1000)   NULL,
    [ApprovedById]                   uniqueidentifier NULL,
    [ApprovalDate]                   datetime2        NULL,
    [ApprovalComments]               nvarchar(1000)   NULL,
    [RejectedDate]                   datetime2        NULL,
    [RejectionReason]                nvarchar(1000)   NULL,
    [RecoveryMethod]                 int              NULL,
    [InstalmentCount]                int              NULL,
    [RecoveryStartDate]              date             NULL,
    [WaivedById]                     uniqueidentifier NULL,
    [WaivedAt]                       datetime2        NULL,
    [WaiverReason]                   nvarchar(1000)   NULL,
    [CancelledAt]                    datetime2        NULL,
    [CancellationReason]             nvarchar(1000)   NULL,
    [CreatedAt]                      datetime2        NOT NULL,
    [UpdatedAt]                      datetime2        NULL,
    [CreatedBy]                      nvarchar(max)    NULL,
    [UpdatedBy]                      nvarchar(max)    NULL,
    [CreatedById]                    uniqueidentifier NULL,
    [LastModifiedById]               uniqueidentifier NULL,
    [IsDeleted]                      bit              NOT NULL,
    [DeletedAt]                      datetime2        NULL,
    [DeletedBy]                      nvarchar(max)    NULL,
    [TenantId]                       uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetSurcharges] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetSurcharges_AssetAssignments_AssignmentId] FOREIGN KEY ([AssignmentId])
        REFERENCES [dbo].[AssetAssignments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetSurcharges_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetSurcharges_Employees_RaisedById] FOREIGN KEY ([RaisedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetSurcharges_Employees_ApprovedById] FOREIGN KEY ([ApprovedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetSurcharges_Employees_WaivedById] FOREIGN KEY ([WaivedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetSurcharges_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetSurchargeRecoveries', 'U') IS NULL
CREATE TABLE [dbo].[AssetSurchargeRecoveries] (
    [Id]               uniqueidentifier NOT NULL,
    [SurchargeId]      uniqueidentifier NOT NULL,
    [Amount]           decimal(18,2)    NOT NULL,
    [RecoveredOn]      date             NOT NULL,
    [Method]           int              NOT NULL,
    [Reference]        nvarchar(200)    NULL,
    [Notes]            nvarchar(1000)   NULL,
    [RecordedById]     uniqueidentifier NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AssetSurchargeRecoveries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AssetSurchargeRecoveries_AssetSurcharges_SurchargeId] FOREIGN KEY ([SurchargeId])
        REFERENCES [dbo].[AssetSurcharges] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetSurchargeRecoveries_Employees_RecordedById] FOREIGN KEY ([RecordedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AssetSurchargeRecoveries_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the table so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the tables exist and these may not.
            foreach (var (name, table, column) in new[]
            {
                ("IX_AssetSurcharges_SurchargeNumber", "AssetSurcharges", "SurchargeNumber"),
                ("IX_AssetSurcharges_AssignmentId", "AssetSurcharges", "AssignmentId"),
                ("IX_AssetSurcharges_EmployeeId", "AssetSurcharges", "EmployeeId"),
                ("IX_AssetSurcharges_Status", "AssetSurcharges", "Status"),
                ("IX_AssetSurcharges_RaisedById", "AssetSurcharges", "RaisedById"),
                ("IX_AssetSurcharges_ApprovedById", "AssetSurcharges", "ApprovedById"),
                ("IX_AssetSurcharges_WaivedById", "AssetSurcharges", "WaivedById"),
                ("IX_AssetSurcharges_TenantId", "AssetSurcharges", "TenantId"),
                ("IX_AssetSurchargeRecoveries_SurchargeId", "AssetSurchargeRecoveries", "SurchargeId"),
                ("IX_AssetSurchargeRecoveries_RecoveredOn", "AssetSurchargeRecoveries", "RecoveredOn"),
                ("IX_AssetSurchargeRecoveries_RecordedById", "AssetSurchargeRecoveries", "RecordedById"),
                ("IX_AssetSurchargeRecoveries_TenantId", "AssetSurchargeRecoveries", "TenantId"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ([{column}]);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropped child-first: the recoveries reference the charge they were collected against.
            //
            // ⚠ This discards the record of every charge raised against an employee and every amount
            // collected from them — including money already deducted from somebody's pay. Nothing
            // else in the schema carries it: the assignment keeps only the repair and replacement
            // COSTS, which are facts about the asset and were never the amount charged.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetSurchargeRecoveries', 'U') IS NOT NULL DROP TABLE [dbo].[AssetSurchargeRecoveries];");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AssetSurcharges', 'U') IS NOT NULL DROP TABLE [dbo].[AssetSurcharges];");
        }
    }
}
