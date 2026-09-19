using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 1, decisions D-4 and D-7. Turns area 9 slice 7's grievance store into the
    /// employee-relations register: a <c>CaseType</c> discriminator on <c>StaffGrievances</c>, and
    /// a new <c>StaffGrievanceParties</c> table for everybody on a case besides the primary party.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The <c>CaseType</c> default is load-bearing, not cosmetic.</b> 69 grievances and 112 steps
    /// already exist (measured on the DEFAULT tenant, 2026-08-27) and every one of them IS a
    /// grievance. <c>EmployeeRelationsCaseType.Grievance</c> is deliberately enum member 1 so the
    /// <c>DEFAULT 1</c> constraint below makes all of them correct as the column is added — there is
    /// no back-fill statement here because none is needed, and none may be added later on the
    /// assumption that there is one. ⚠ Nothing in that enum may be renumbered for the same reason.
    /// </para>
    /// <para>
    /// <b>The table is not renamed</b> (decision D-5). <c>StaffGrievances</c> keeps its name and its
    /// rows; the register's correct name lives on the route (<c>api/hr/employee-relations</c>), not
    /// on the table. A rename would have meant an EF-config rewrite, a snapshot regeneration and a
    /// rename across nine files, for no functional gain and real regression risk on a working
    /// portal.
    /// </para>
    /// <para>
    /// <b>A party may be external, so <c>EmployeeId</c> is nullable and carries no NOT NULL twin.</b>
    /// Representation at a grievance is very often by a union official or a lawyer who is not on the
    /// payroll, and FR-HR-181 requires union consultation be retained. The "exactly one of employee
    /// and external name" rule is enforced by the service rather than by a CHECK constraint,
    /// consistent with how every other conditional rule in this module is expressed — a CHECK here
    /// would refuse rows with a SQL error the caller cannot read.
    /// </para>
    /// <para>
    /// <b>Three Employee legs, all <c>NO ACTION</c>.</b> <c>EmployeeId</c>,
    /// <c>RepresentsEmployeeId</c> and <c>AddedById</c> all point at <c>Employees</c>; SQL Server
    /// refuses multiple cascade paths to one table, and an employee must never be deleted out from
    /// under a case file in any event. Only the leg to the case itself cascades — a party has no
    /// meaning apart from the case it is a party TO.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — one <c>AddColumn</c>, one <c>CreateTable</c>, six
    /// foreign keys and seven indexes, with nothing inferred and nothing renamed. (⚠ Worth
    /// confirming rather than assuming: EF inferred a wrong <c>RENAME</c> in the assets area at
    /// slice 3.) Rewritten as guarded SQL only to match the surrounding HR migrations, so it is safe
    /// to re-run against a database at either state — including one built from the EF model rather
    /// than from the chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeRelationsCaseTypeAndParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The discriminator. DEFAULT 1 = EmployeeRelationsCaseType.Grievance, which is what
            // every existing row is — see the remarks. The named constraint is so the Down can
            // drop it deterministically rather than hunting sys.default_constraints.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.StaffGrievances', 'CaseType') IS NULL
    ALTER TABLE [dbo].[StaffGrievances]
        ADD [CaseType] int NOT NULL
        CONSTRAINT [DF_StaffGrievances_CaseType] DEFAULT (1);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceParties', 'U') IS NULL
CREATE TABLE [dbo].[StaffGrievanceParties] (
    [Id]                    uniqueidentifier NOT NULL,
    [GrievanceId]           uniqueidentifier NOT NULL,
    [Role]                  int              NOT NULL,
    [EmployeeId]            uniqueidentifier NULL,
    [ExternalName]          nvarchar(200)    NULL,
    [ExternalOrganisation]  nvarchar(200)    NULL,
    [RepresentsEmployeeId]  uniqueidentifier NULL,
    [UnionId]               uniqueidentifier NULL,
    [AddedDate]             datetime2        NOT NULL,
    [AddedById]             uniqueidentifier NULL,
    [Notes]                 nvarchar(1000)   NULL,
    [RemovedDate]           datetime2        NULL,
    [RemovalReason]         nvarchar(500)    NULL,
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
    CONSTRAINT [PK_StaffGrievanceParties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffGrievanceParties_StaffGrievances_GrievanceId] FOREIGN KEY ([GrievanceId])
        REFERENCES [dbo].[StaffGrievances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StaffGrievanceParties_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceParties_Employees_RepresentsEmployeeId] FOREIGN KEY ([RepresentsEmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceParties_Employees_AddedById] FOREIGN KEY ([AddedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceParties_Unions_UnionId] FOREIGN KEY ([UnionId])
        REFERENCES [dbo].[Unions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceParties_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the table so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the table exists and these may not.
            foreach (var (name, table, columns) in new[]
            {
                // The register's first filter, and the one every non-grievance type is read through.
                ("IX_StaffGrievances_TenantId_CaseType", "StaffGrievances", "[TenantId], [CaseType]"),
                ("IX_StaffGrievanceParties_GrievanceId", "StaffGrievanceParties", "[GrievanceId]"),
                ("IX_StaffGrievanceParties_EmployeeId", "StaffGrievanceParties", "[EmployeeId]"),
                ("IX_StaffGrievanceParties_RepresentsEmployeeId", "StaffGrievanceParties", "[RepresentsEmployeeId]"),
                ("IX_StaffGrievanceParties_AddedById", "StaffGrievanceParties", "[AddedById]"),
                ("IX_StaffGrievanceParties_UnionId", "StaffGrievanceParties", "[UnionId]"),
                ("IX_StaffGrievanceParties_Role", "StaffGrievanceParties", "[Role]"),
                ("IX_StaffGrievanceParties_TenantId", "StaffGrievanceParties", "[TenantId]"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ({columns});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This discards who was involved in every employee-relations case — respondents,
            // representatives, union officials, witnesses, mediators — and it is not recoverable
            // from anywhere else in the schema. Dropping CaseType additionally makes every
            // non-grievance case indistinguishable from a grievance, silently: the rows survive
            // and start reading as something they are not, which is worse than losing them.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceParties', 'U') IS NOT NULL
    DROP TABLE [dbo].[StaffGrievanceParties];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_StaffGrievances_TenantId_CaseType'
             AND object_id = OBJECT_ID('dbo.StaffGrievances'))
    DROP INDEX [IX_StaffGrievances_TenantId_CaseType] ON [dbo].[StaffGrievances];");

            // The default constraint has to go before the column it defaults.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_StaffGrievances_CaseType')
    ALTER TABLE [dbo].[StaffGrievances] DROP CONSTRAINT [DF_StaffGrievances_CaseType];

IF COL_LENGTH('dbo.StaffGrievances', 'CaseType') IS NOT NULL
    ALTER TABLE [dbo].[StaffGrievances] DROP COLUMN [CaseType];");
        }
    }
}
