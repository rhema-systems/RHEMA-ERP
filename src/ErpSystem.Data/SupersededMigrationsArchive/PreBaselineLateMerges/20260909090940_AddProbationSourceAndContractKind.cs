using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Demo feedback round 2, lane D1 (docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 5 lane D, § 6.7):
    /// where an employee's probation term came from, the contract-kind link that Q-4 chose to
    /// surface rather than delete, and the retire flag that kind needs — plus the one-off repair of
    /// <c>IsCurrent</c> without which fixing defect X-4 would make the live data worse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, the same rewrite as
    /// <c>20260909013533_AddOrganizationUnitHistoryNotes</c>. <c>rebuild-db</c> builds from the EF
    /// model rather than from the migration chain, so a rebuilt database already has these columns
    /// and a bare <c>AddColumn</c> fails on it — and an unguarded failure stops the whole chain at
    /// its first statement.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ Two corrections to what EF scaffolded.</b> First, it wrote
    /// <c>defaultValue: false</c> for <c>EmployeeContractTypes.IsActive</c>, because it reads the
    /// CLR default of a non-nullable bool and not the property's <c>= true</c> initialiser. That
    /// would have retired all seven of TDC's seeded contract kinds the moment the column arrived —
    /// a screen shipped empty on the day it shipped. It is <c>DEFAULT 1</c> below. Second, the
    /// foreign key is left at NO ACTION on purpose: a contract kind that contracts are written
    /// under must not be deletable out from under them, and retiring is the only withdrawal the
    /// service offers.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ The data repair is the load-bearing half of this migration.</b>
    /// <c>EmployeeContractDetail.IsCurrent</c> has always defaulted to <c>true</c> on the entity and
    /// nothing has ever maintained it: <c>AddContractAsync</c> left it at the default on every row
    /// it inserted, and both termination paths cleared <c>IsActive</c> without clearing this. So
    /// EVERY contract row in a live database currently claims to be the terms in force, terminated
    /// ones included. Lane D1 makes that flag load-bearing — it is what the current-contract read,
    /// the header write-through and the supersede rule all key off — so shipping the code without
    /// this repair would turn a dormant inconsistency into a terminated contract being returned as
    /// somebody's current terms. The three statements below clear it everywhere it is not true, in
    /// increasing order of how much they have to decide:
    /// </para>
    /// <list type="number">
    ///   <item>a soft-deleted row is not anybody's terms;</item>
    ///   <item>nor is one that is inactive or whose status is not Active — that is what those
    ///         columns were already saying while this one contradicted them;</item>
    ///   <item>where an employee still has more than one row claiming to be current, the latest by
    ///         effective date then start date then creation wins, and the rest are cleared. ⚠ It
    ///         does NOT close them with an <c>EndDate</c>: the closing date is derived from a
    ///         successor's effective date, and rows written before this lane carry
    ///         <c>0001-01-01</c> there, so inventing end dates from it would write worse data than
    ///         it repaired. They are simply no longer current.</item>
    /// </list>
    /// <para>
    /// Nothing backfills <c>ProbationSource</c>. Null is the honest answer for a term that was
    /// typed into a free field: the number is there and its provenance is not, and defaulting the
    /// column to <c>Position</c> would claim agreement with the post that nobody measured.
    /// </para>
    /// </remarks>
    public partial class AddProbationSourceAndContractKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── schema ────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'ProbationSource') IS NULL
    ALTER TABLE [dbo].[Employees] ADD [ProbationSource] int NULL;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeContractTypes', 'IsActive') IS NULL
    ALTER TABLE [dbo].[EmployeeContractTypes]
        ADD [IsActive] bit NOT NULL
        CONSTRAINT [DF_EmployeeContractTypes_IsActive] DEFAULT (1);");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeContractDetails', 'ContractTypeId') IS NULL
    ALTER TABLE [dbo].[EmployeeContractDetails] ADD [ContractTypeId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_EmployeeContractDetails_ContractTypeId'
                 AND object_id = OBJECT_ID('dbo.EmployeeContractDetails'))
    CREATE INDEX [IX_EmployeeContractDetails_ContractTypeId]
        ON [dbo].[EmployeeContractDetails] ([ContractTypeId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_EmployeeContractDetails_EmployeeContractTypes_ContractTypeId'
                 AND parent_object_id = OBJECT_ID('dbo.EmployeeContractDetails'))
    ALTER TABLE [dbo].[EmployeeContractDetails]
        ADD CONSTRAINT [FK_EmployeeContractDetails_EmployeeContractTypes_ContractTypeId]
        FOREIGN KEY ([ContractTypeId]) REFERENCES [dbo].[EmployeeContractTypes] ([Id]);");

            // ── the repair ────────────────────────────────────────────────────
            // 1 · a soft-deleted row is nobody's terms.
            migrationBuilder.Sql(@"
UPDATE [dbo].[EmployeeContractDetails]
   SET [IsCurrent] = 0
 WHERE [IsCurrent] = 1 AND [IsDeleted] = 1;");

            // 2 · nor is one the other two columns already called finished. ContractStatus is the
            //     enum: 1 = Active, 2 = Expired, 3 = Terminated.
            migrationBuilder.Sql(@"
UPDATE [dbo].[EmployeeContractDetails]
   SET [IsCurrent] = 0
 WHERE [IsCurrent] = 1 AND ([IsActive] = 0 OR [ContractStatus] <> 1);");

            // 3 · and where an employee is left holding several, the latest one wins.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT [Id],
           ROW_NUMBER() OVER (
               PARTITION BY [TenantId], [EmployeeId]
               ORDER BY [EffectiveDate] DESC, [StartDate] DESC, [CreatedAt] DESC, [Id] DESC
           ) AS rn
      FROM [dbo].[EmployeeContractDetails]
     WHERE [IsCurrent] = 1 AND [IsDeleted] = 0
)
UPDATE cd
   SET cd.[IsCurrent] = 0
  FROM [dbo].[EmployeeContractDetails] cd
  JOIN ranked r ON r.[Id] = cd.[Id]
 WHERE r.rn > 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ The IsCurrent repair is NOT undone. Down restores the schema, not the wrong data:
            // re-setting the flag on terminated contracts would be a deliberate corruption, and the
            // pre-migration state is not recoverable anyway — the flag carried no information.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = 'FK_EmployeeContractDetails_EmployeeContractTypes_ContractTypeId'
             AND parent_object_id = OBJECT_ID('dbo.EmployeeContractDetails'))
    ALTER TABLE [dbo].[EmployeeContractDetails]
        DROP CONSTRAINT [FK_EmployeeContractDetails_EmployeeContractTypes_ContractTypeId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_EmployeeContractDetails_ContractTypeId'
             AND object_id = OBJECT_ID('dbo.EmployeeContractDetails'))
    DROP INDEX [IX_EmployeeContractDetails_ContractTypeId] ON [dbo].[EmployeeContractDetails];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeeContractDetails', 'ContractTypeId') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeContractDetails] DROP COLUMN [ContractTypeId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_EmployeeContractTypes_IsActive')
    ALTER TABLE [dbo].[EmployeeContractTypes] DROP CONSTRAINT [DF_EmployeeContractTypes_IsActive];
IF COL_LENGTH('dbo.EmployeeContractTypes', 'IsActive') IS NOT NULL
    ALTER TABLE [dbo].[EmployeeContractTypes] DROP COLUMN [IsActive];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'ProbationSource') IS NOT NULL
    ALTER TABLE [dbo].[Employees] DROP COLUMN [ProbationSource];");
        }
    }
}
