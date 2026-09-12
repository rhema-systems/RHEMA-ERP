using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// The exit register — one separation record per employee leaving, by any route (area 9b
    /// slice 1, FR-HR-090 and FR-HR-182).
    /// </summary>
    /// <remarks>
    /// <para>Before this table the only way out of the organisation was a disciplinary case:
    /// <c>StaffDisciplineTermination</c> and <c>StaffDisciplineSeparation</c> both hang off a
    /// <c>DisciplinaryActionId</c>, so resignation, retirement, contract expiry and death had enum
    /// members and no route. Measured on the live tenant 2026-08-20: 3,693 employees, none ever
    /// terminated, and 29 disciplinary terminations whose employees were all still
    /// <c>StaffStatus = Active</c>.</para>
    ///
    /// <para>The disciplinary route does not keep a parallel store — it writes here too, through
    /// <c>DisciplinaryActionId</c>. Two exit stores that can disagree is how those 29 orphans came
    /// about.</para>
    ///
    /// <para>The unique index on the separation number is <b>filtered on IsDeleted</b>. Deletes in
    /// this codebase are soft, and a soft-deleted row still occupies an unfiltered unique index —
    /// the area-13 lesson.</para>
    ///
    /// <para>Hand-written; every statement guarded so it is safe on a database built from the EF
    /// model as well as one migrated. Discovery attributes inline; deliberately NOT listed in
    /// <c>FastBuildMigrationMetadata</c>.</para>
    /// </remarks>
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260820090000_AddEmployeeSeparationRegister")]
    public partial class AddEmployeeSeparationRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.EmployeeSeparations'))
BEGIN
    CREATE TABLE [dbo].[EmployeeSeparations] (
        [Id]                       uniqueidentifier NOT NULL,
        [TenantId]                 uniqueidentifier NOT NULL,
        [SeparationNumber]         nvarchar(30)     NOT NULL,
        [EmployeeId]               uniqueidentifier NOT NULL,
        [SeparationType]           int              NOT NULL,
        [Status]                   int              NOT NULL,
        [ReasonCategory]           int              NULL,
        [ReasonNotes]              nvarchar(2000)   NULL,
        [InitiatedOn]              date             NOT NULL,
        [NoticeGivenOn]            date             NULL,
        [NoticeDays]               int              NULL,
        [LastWorkingDay]           date             NULL,
        [EffectiveDate]            date             NULL,
        [InitiatedById]            uniqueidentifier NULL,
        [IsSystemInitiated]        bit              NOT NULL CONSTRAINT [DF_EmployeeSeparations_IsSystemInitiated] DEFAULT (0),
        [IsProcedural]             bit              NOT NULL CONSTRAINT [DF_EmployeeSeparations_IsProcedural] DEFAULT (0),
        [ApprovedById]             uniqueidentifier NULL,
        [ApprovedOn]               datetime2        NULL,
        [ApprovalNotes]            nvarchar(2000)   NULL,
        [WorkflowInstanceId]       uniqueidentifier NULL,
        [IsEligibleForRehire]      bit              NOT NULL CONSTRAINT [DF_EmployeeSeparations_IsEligibleForRehire] DEFAULT (1),
        [EligibleForRehireDate]    date             NULL,
        [RehireRestrictions]       nvarchar(1000)   NULL,
        [DisciplinaryActionId]     uniqueidentifier NULL,
        [EmployeeRecordUpdatedOn]  datetime2        NULL,
        [CancelledOn]              datetime2        NULL,
        [CancelledById]            uniqueidentifier NULL,
        [CancellationReason]       nvarchar(1000)   NULL,
        [CreatedAt]                datetime2        NOT NULL,
        [UpdatedAt]                datetime2        NULL,
        [CreatedBy]                nvarchar(max)    NULL,
        [UpdatedBy]                nvarchar(max)    NULL,
        [CreatedById]              uniqueidentifier NULL,
        [LastModifiedById]         uniqueidentifier NULL,
        [IsDeleted]                bit              NOT NULL CONSTRAINT [DF_EmployeeSeparations_IsDeleted] DEFAULT (0),
        [DeletedAt]                datetime2        NULL,
        [DeletedBy]                nvarchar(max)    NULL,
        CONSTRAINT [PK_EmployeeSeparations] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Tenants_TenantId')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_EmployeeId')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Employees_EmployeeId]
        FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_InitiatedById')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Employees_InitiatedById]
        FOREIGN KEY ([InitiatedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_ApprovedById')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Employees_ApprovedById]
        FOREIGN KEY ([ApprovedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_CancelledById')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Employees_CancelledById]
        FOREIGN KEY ([CancelledById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_EmployeeSeparation_Tenant_Number' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE UNIQUE INDEX [UX_EmployeeSeparation_Tenant_Number] ON [dbo].[EmployeeSeparations] ([TenantId], [SeparationNumber]) WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparation_EmployeeId' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparation_EmployeeId] ON [dbo].[EmployeeSeparations] ([EmployeeId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparation_Status' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparation_Status] ON [dbo].[EmployeeSeparations] ([Status]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparation_Type' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparation_Type] ON [dbo].[EmployeeSeparations] ([SeparationType]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparation_EffectiveDate' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparation_EffectiveDate] ON [dbo].[EmployeeSeparations] ([EffectiveDate]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparation_DisciplinaryActionId' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparation_DisciplinaryActionId] ON [dbo].[EmployeeSeparations] ([DisciplinaryActionId]);");

            // The three actor foreign keys. EF's foreign-key index convention creates one of these
            // per FK automatically, so a database built from the model has them and — until this
            // was added — a database built from this migration did not. The scratch-scaffold check
            // cannot catch that: it diffs the model against the SNAPSHOT, never against the
            // database. Verified against sys.indexes instead.
            //
            // Names follow EF's convention (IX_<table>_<property>), because that is what the model
            // produces and the two must agree.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_ApprovedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparations_ApprovedById] ON [dbo].[EmployeeSeparations] ([ApprovedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_CancelledById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparations_CancelledById] ON [dbo].[EmployeeSeparations] ([CancelledById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_InitiatedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparations_InitiatedById] ON [dbo].[EmployeeSeparations] ([InitiatedById]);");

            // ⚠ Deliberately NO index on TenantId alone. EF's convention skips the foreign-key
            // index when an existing index already leads with that column, and
            // UX_EmployeeSeparation_Tenant_Number does. Adding one here would put the database
            // permanently ahead of the model — which is exactly what the scratch scaffold caught
            // when the hand-written snapshot claimed it.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    DROP TABLE [dbo].[EmployeeSeparations];");
        }
    }
}
