using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 9 slice 7 — FR-HR-181's grievance ladder.
    ///
    /// Two tables. <c>StaffGrievances</c> is the complaint: who raised it, their statement in their
    /// own words, and which rung of the escalation route it currently sits at.
    /// <c>StaffGrievanceSteps</c> is the trail — one row per rung REACHED, created when the grievance
    /// arrives there rather than when it is answered, so an unanswered step records who currently owes
    /// a response. Steps are append-only: escalating never amends the level below, because FR-HR-181
    /// requires each level's response be retained.
    ///
    /// The step cascade-deletes from its grievance, unlike the Restrict used across the disciplinary
    /// case: a step has no meaning apart from its grievance, whereas a disciplinary sub-entity is a
    /// record in its own right that must survive.
    ///
    /// ⚠ The rungs are NOT resolved to people. TDC's org data cannot support it — measured
    /// 2026-08-16, 0 of 41 organisation units had a head recorded and 175 of 1,486 active employees
    /// had a manager — so <c>AssignedToId</c> is named explicitly by HR per step rather than derived.
    /// See [[hr-deferred-modules]] #3.
    ///
    /// The scaffolded CreateTable/CreateIndex bodies are replaced with guarded SQL (repo convention):
    /// local dev DBs are built from the EF model by rebuild-db, so a DB can already carry these
    /// objects without this migration being stamped — every operation checks before it acts. The
    /// generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddStaffGrievances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffGrievances]', N'U') IS NULL
BEGIN
    CREATE TABLE [StaffGrievances] (
        [Id] uniqueidentifier NOT NULL,
        [GrievanceNumber] nvarchar(50) NOT NULL,
        [EmployeeId] uniqueidentifier NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Statement] nvarchar(max) NOT NULL,
        [FiledDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [CurrentLevel] int NOT NULL,
        [ResolvedDate] datetime2 NULL,
        [ResolutionSummary] nvarchar(4000) NULL,
        [WithdrawnDate] datetime2 NULL,
        [WithdrawalReason] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_StaffGrievances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffGrievances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
            REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StaffGrievances_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffGrievanceSteps]', N'U') IS NULL
BEGIN
    CREATE TABLE [StaffGrievanceSteps] (
        [Id] uniqueidentifier NOT NULL,
        [GrievanceId] uniqueidentifier NOT NULL,
        [Level] int NOT NULL,
        [Sequence] int NOT NULL,
        [ReachedDate] datetime2 NOT NULL,
        [AssignedToId] uniqueidentifier NULL,
        [Response] nvarchar(4000) NULL,
        [RespondedDate] datetime2 NULL,
        [RespondedById] uniqueidentifier NULL,
        [Outcome] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_StaffGrievanceSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffGrievanceSteps_Employees_AssignedToId] FOREIGN KEY ([AssignedToId])
            REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StaffGrievanceSteps_Employees_RespondedById] FOREIGN KEY ([RespondedById])
            REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StaffGrievanceSteps_StaffGrievances_GrievanceId] FOREIGN KEY ([GrievanceId])
            REFERENCES [StaffGrievances] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StaffGrievanceSteps_Tenants_TenantId] FOREIGN KEY ([TenantId])
            REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END
");

            // Indexes, each guarded independently — a DB built from the EF model may already carry
            // some of them.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievances_CurrentLevel' AND [object_id] = OBJECT_ID(N'[StaffGrievances]'))
    CREATE INDEX [IX_StaffGrievances_CurrentLevel] ON [StaffGrievances] ([CurrentLevel]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievances_EmployeeId' AND [object_id] = OBJECT_ID(N'[StaffGrievances]'))
    CREATE INDEX [IX_StaffGrievances_EmployeeId] ON [StaffGrievances] ([EmployeeId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievances_Status' AND [object_id] = OBJECT_ID(N'[StaffGrievances]'))
    CREATE INDEX [IX_StaffGrievances_Status] ON [StaffGrievances] ([Status]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievances_TenantId_GrievanceNumber' AND [object_id] = OBJECT_ID(N'[StaffGrievances]'))
    CREATE UNIQUE INDEX [IX_StaffGrievances_TenantId_GrievanceNumber] ON [StaffGrievances] ([TenantId], [GrievanceNumber]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievanceSteps_AssignedToId' AND [object_id] = OBJECT_ID(N'[StaffGrievanceSteps]'))
    CREATE INDEX [IX_StaffGrievanceSteps_AssignedToId] ON [StaffGrievanceSteps] ([AssignedToId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievanceSteps_GrievanceId' AND [object_id] = OBJECT_ID(N'[StaffGrievanceSteps]'))
    CREATE INDEX [IX_StaffGrievanceSteps_GrievanceId] ON [StaffGrievanceSteps] ([GrievanceId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievanceSteps_Outcome' AND [object_id] = OBJECT_ID(N'[StaffGrievanceSteps]'))
    CREATE INDEX [IX_StaffGrievanceSteps_Outcome] ON [StaffGrievanceSteps] ([Outcome]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievanceSteps_RespondedById' AND [object_id] = OBJECT_ID(N'[StaffGrievanceSteps]'))
    CREATE INDEX [IX_StaffGrievanceSteps_RespondedById] ON [StaffGrievanceSteps] ([RespondedById]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_StaffGrievanceSteps_TenantId' AND [object_id] = OBJECT_ID(N'[StaffGrievanceSteps]'))
    CREATE INDEX [IX_StaffGrievanceSteps_TenantId] ON [StaffGrievanceSteps] ([TenantId]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Steps first — the FK from step to grievance would otherwise block the drop.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[StaffGrievanceSteps]', N'U') IS NOT NULL DROP TABLE [StaffGrievanceSteps];
IF OBJECT_ID(N'[StaffGrievances]', N'U') IS NOT NULL DROP TABLE [StaffGrievances];
");
        }
    }
}
