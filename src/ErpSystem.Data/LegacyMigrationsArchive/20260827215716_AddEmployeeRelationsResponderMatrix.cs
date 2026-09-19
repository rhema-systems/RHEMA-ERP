using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 5, decision D-3 — <b>FR-HR-084</b>: "model a grievance hierarchy defining
    /// reporting lines." Who answers which rung of FR-HR-181's ladder, for which part of the
    /// organisation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Maintained, not derived, and that is the whole decision.</b> The obvious implementation
    /// resolves the Supervisor and HOD rungs from <c>Employees.ManagerId</c> and
    /// <c>OrganizationUnits.HeadEmployeeId</c>. Measured on the DEFAULT tenant on 2026-08-27:
    /// <b>486 of 8,353 employees have a manager (5.8%)</b> and <b>2 of 48 units have a head</b> —
    /// both WORSE than the same measurement six weeks earlier. A derived hierarchy resolves to
    /// nobody for 94% of staff. This is the fourth requirement to hit that wall (FR-HR-080,
    /// FR-HR-173, FR-HR-181, now FR-HR-084), and rather than work around it a fourth time this is
    /// the explicit alternative already put to TDC in <c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c> §4.
    /// ⚠ If reporting lines are ever genuinely maintained, this table does not need removing — it
    /// is the override layer a derived lookup would need anyway.
    /// </para>
    /// <para>
    /// <b>A NULL <c>OrganizationUnitId</c> is the tenant-wide default, not a missing value.</b>
    /// Resolution is unit row → tenant default → nobody, and "nobody" is a supported outcome: a case
    /// in an uncovered unit is still filed and simply arrives unassigned, exactly as every case did
    /// before this table existed. Nothing here may be made NOT NULL.
    /// </para>
    /// <para>
    /// <b>⚠ There is deliberately NO unique index on (TenantId, OrganizationUnitId, Level), and one
    /// must not be added.</b> A slot legitimately holds several rows over time — acting cover while
    /// the usual responder is on leave is precisely what the effective window is for. What must be
    /// refused is two rows <i>in force on the same day</i>, which is a temporal overlap no unique
    /// index can express; <c>EmployeeRelationsResponderService.ValidateAsync</c> enforces it. A
    /// unique index here would not tighten the rule, it would delete the feature.
    /// </para>
    /// <para>
    /// <b>Every leg is <c>NO ACTION</c>.</b> Neither an employee nor an organisation unit may be
    /// deleted out from under an assignment: who answered a rung is a fact about the cases decided
    /// while they did, and the matrix is read by the audit trail as well as by the router. Removal
    /// is a soft delete in the service for the same reason.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — one <c>CreateTable</c>, three foreign keys and three
    /// indexes, with nothing inferred and nothing renamed. Rewritten as guarded SQL only to match
    /// the surrounding HR migrations, so it is safe to re-run against a database at either state —
    /// including one built from the EF model rather than from the chain, which is how this repo's
    /// <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeRelationsResponderMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeRelationsResponders', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeRelationsResponders] (
    [Id]                    uniqueidentifier NOT NULL,
    -- NULL = the tenant-wide default. A value, not a gap. See the remarks.
    [OrganizationUnitId]    uniqueidentifier NULL,
    [Level]                 int              NOT NULL,
    [ResponderEmployeeId]   uniqueidentifier NOT NULL,
    -- NULL from = since always; NULL to = until further notice. The acting-cover window.
    [EffectiveFrom]         datetime2        NULL,
    [EffectiveTo]           datetime2        NULL,
    [Notes]                 nvarchar(500)    NULL,
    [CreatedAt]             datetime2        NOT NULL,
    [UpdatedAt]             datetime2        NULL,
    [CreatedBy]             nvarchar(max)    NULL,
    [UpdatedBy]             nvarchar(max)    NULL,
    [CreatedById]           uniqueidentifier NULL,
    [LastModifiedById]      uniqueidentifier NULL,
    [IsDeleted]             bit              NOT NULL,
    [DeletedAt]             datetime2        NULL,
    [DeletedBy]             nvarchar(max)    NULL,
    [TenantId]              uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeRelationsResponders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeRelationsResponders_Employees_ResponderEmployeeId] FOREIGN KEY ([ResponderEmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeRelationsResponders_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId])
        REFERENCES [dbo].[OrganizationUnits] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeRelationsResponders_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the table so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the table exists and these may not.
            //
            // ⚠ None of these is UNIQUE, and the composite one must not become so. See the remarks:
            // several rows per slot over time is the acting-cover feature, not a data error.
            foreach (var (name, columns) in new[]
            {
                // Exactly what ResolveAsync filters on: the rung, then the scope.
                ("IX_EmployeeRelationsResponders_TenantId_Level_OrganizationUnitId", "[TenantId], [Level], [OrganizationUnitId]"),
                ("IX_EmployeeRelationsResponders_OrganizationUnitId", "[OrganizationUnitId]"),
                ("IX_EmployeeRelationsResponders_ResponderEmployeeId", "[ResponderEmployeeId]"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.EmployeeRelationsResponders', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.EmployeeRelationsResponders'))
    CREATE INDEX [{name}] ON [dbo].[EmployeeRelationsResponders] ({columns});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ Dropping this does NOT break the ladder — cases still file and escalate, they just
            // arrive unassigned again, which is how the module behaved before slice 5. What is lost
            // is the record of who was responsible for answering which rung, and when: that is read
            // by the audit trail as well as by the router, and nothing else in the schema holds it.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeRelationsResponders', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmployeeRelationsResponders];");
        }
    }
}
