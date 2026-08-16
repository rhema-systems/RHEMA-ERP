using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 9 slice 4 — the recorded override on the natural-justice gate.
    ///
    /// A disciplinary decision cannot be proposed until the employee has been issued a written query
    /// and given a chance to answer it. Real cases exist where that chance cannot be given — the
    /// employee has absconded, is detained, or refuses service — so HR may record why and proceed.
    /// These three columns are that record: when, by whom, and the reason, which is required.
    ///
    /// The override is deliberately visible rather than silent. An exception path that leaves a trace
    /// is standard; one that does not exist at all just means people work around the system, and then
    /// there is no trace of anything.
    ///
    /// The scaffolded AddColumn/CreateIndex/AddForeignKey bodies are replaced with guarded SQL (repo
    /// convention): local dev DBs are built from the EF model by rebuild-db, so a DB can already carry
    /// these objects without the migration being stamped — every operation checks before it acts. The
    /// generated Designer and the regenerated snapshot are kept as scaffolded.
    ///
    /// All three columns are nullable, so unlike the MinimumAuthority migration there is no default
    /// constraint to name and no existing-row backfill to get right.
    /// </summary>
    public partial class AddDisciplineQueryOpportunityWaiver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffDisciplinaryActions]', N'QueryOpportunityWaivedAt') IS NULL
    ALTER TABLE [StaffDisciplinaryActions] ADD [QueryOpportunityWaivedAt] datetime2 NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffDisciplinaryActions]', N'QueryOpportunityWaivedById') IS NULL
    ALTER TABLE [StaffDisciplinaryActions] ADD [QueryOpportunityWaivedById] uniqueidentifier NULL;
");
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffDisciplinaryActions]', N'QueryOpportunityWaivedReason') IS NULL
    ALTER TABLE [StaffDisciplinaryActions] ADD [QueryOpportunityWaivedReason] nvarchar(1000) NULL;
");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE [name] = N'IX_StaffDisciplinaryActions_QueryOpportunityWaivedById'
                 AND [object_id] = OBJECT_ID(N'[StaffDisciplinaryActions]'))
    CREATE INDEX [IX_StaffDisciplinaryActions_QueryOpportunityWaivedById]
        ON [StaffDisciplinaryActions] ([QueryOpportunityWaivedById]);
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_StaffDisciplinaryActions_Employees_QueryOpportunityWaivedById]', N'F') IS NULL
    ALTER TABLE [StaffDisciplinaryActions]
        ADD CONSTRAINT [FK_StaffDisciplinaryActions_Employees_QueryOpportunityWaivedById]
        FOREIGN KEY ([QueryOpportunityWaivedById]) REFERENCES [Employees] ([Id]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Foreign key, then index, then the columns — a column cannot be dropped while either
            // still references it.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_StaffDisciplinaryActions_Employees_QueryOpportunityWaivedById]', N'F') IS NOT NULL
    ALTER TABLE [StaffDisciplinaryActions]
        DROP CONSTRAINT [FK_StaffDisciplinaryActions_Employees_QueryOpportunityWaivedById];
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE [name] = N'IX_StaffDisciplinaryActions_QueryOpportunityWaivedById'
             AND [object_id] = OBJECT_ID(N'[StaffDisciplinaryActions]'))
    DROP INDEX [IX_StaffDisciplinaryActions_QueryOpportunityWaivedById] ON [StaffDisciplinaryActions];
IF COL_LENGTH(N'[StaffDisciplinaryActions]', N'QueryOpportunityWaivedReason') IS NOT NULL
    ALTER TABLE [StaffDisciplinaryActions] DROP COLUMN [QueryOpportunityWaivedReason];
IF COL_LENGTH(N'[StaffDisciplinaryActions]', N'QueryOpportunityWaivedById') IS NOT NULL
    ALTER TABLE [StaffDisciplinaryActions] DROP COLUMN [QueryOpportunityWaivedById];
IF COL_LENGTH(N'[StaffDisciplinaryActions]', N'QueryOpportunityWaivedAt') IS NOT NULL
    ALTER TABLE [StaffDisciplinaryActions] DROP COLUMN [QueryOpportunityWaivedAt];
");
        }
    }
}
