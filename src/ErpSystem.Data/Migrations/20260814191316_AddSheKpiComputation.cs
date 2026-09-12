using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 10 slice 14 — the computed-KPI columns on ShePerformanceSnapshots:
    /// six figures the KPI engine derives from live data (TRIR, near-miss rate,
    /// training completion, drill objectives-met, waste recycling, average
    /// inspection compliance) plus the KpisComputedAt/KpisComputedById stamp that
    /// separates computed snapshots from hand-reported ones.
    ///
    /// The scaffolded AddColumn/CreateIndex/AddForeignKey bodies are replaced with
    /// guarded SQL (repo convention): local dev DBs are built from the EF model by
    /// rebuild-db, so a DB can already carry these columns without this migration
    /// being stamped — every operation checks before it acts. The generated
    /// Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddSheKpiComputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'AverageInspectionComplianceScore') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [AverageInspectionComplianceScore] decimal(18,4) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'FireDrillObjectivesMetRate') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [FireDrillObjectivesMetRate] decimal(18,4) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'KpisComputedAt') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [KpisComputedAt] datetime2 NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'KpisComputedById') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [KpisComputedById] uniqueidentifier NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'NearMissFrequencyRate') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [NearMissFrequencyRate] decimal(18,4) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'TotalRecordableIncidentRate') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [TotalRecordableIncidentRate] decimal(18,2) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'TrainingCompletionRate') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [TrainingCompletionRate] decimal(18,4) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'WasteRecyclingRate') IS NULL
    ALTER TABLE [ShePerformanceSnapshots] ADD [WasteRecyclingRate] decimal(18,4) NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ShePerformanceSnapshots_KpisComputedById' AND [object_id] = OBJECT_ID(N'[ShePerformanceSnapshots]'))
    CREATE INDEX [IX_ShePerformanceSnapshots_KpisComputedById] ON [ShePerformanceSnapshots] ([KpisComputedById]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_ShePerformanceSnapshots_Employees_KpisComputedById]', N'F') IS NULL
    ALTER TABLE [ShePerformanceSnapshots]
        ADD CONSTRAINT [FK_ShePerformanceSnapshots_Employees_KpisComputedById]
        FOREIGN KEY ([KpisComputedById]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_ShePerformanceSnapshots_Employees_KpisComputedById]', N'F') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP CONSTRAINT [FK_ShePerformanceSnapshots_Employees_KpisComputedById];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_ShePerformanceSnapshots_KpisComputedById' AND [object_id] = OBJECT_ID(N'[ShePerformanceSnapshots]'))
    DROP INDEX [IX_ShePerformanceSnapshots_KpisComputedById] ON [ShePerformanceSnapshots];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'AverageInspectionComplianceScore') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [AverageInspectionComplianceScore];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'FireDrillObjectivesMetRate') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [FireDrillObjectivesMetRate];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'KpisComputedAt') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [KpisComputedAt];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'KpisComputedById') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [KpisComputedById];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'NearMissFrequencyRate') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [NearMissFrequencyRate];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'TotalRecordableIncidentRate') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [TotalRecordableIncidentRate];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'TrainingCompletionRate') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [TrainingCompletionRate];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ShePerformanceSnapshots]', N'WasteRecyclingRate') IS NOT NULL
    ALTER TABLE [ShePerformanceSnapshots] DROP COLUMN [WasteRecyclingRate];
");
        }
    }
}
