using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane O — the technician-role flag: one answer to "is this person a technician?".
    /// </summary>
    /// <remarks>
    /// <para><b><c>EmployeePositions.IsTechnicianRole</c></b> — the rule: every holder of the post is
    /// available to Maintenance. <b><c>Employees.MaintenanceAssignmentSetByHand</c></b> — the exception:
    /// HR set this person's <c>CanBeAssignedToMaintenance</c> by hand, so the post no longer decides it.
    /// The save-time rule in <c>ApplicationDbContext.HrTechnicianRole.cs</c> keeps the stored column
    /// equal to the post's flag for everyone else.</para>
    ///
    /// <para>The backfill runs in its own batch, after the columns exist. It is keyed on VALUES, never on
    /// a row id, and leaves every database in the state that rule maintains:</para>
    /// <list type="number">
    /// <item><b>The retired <c>Department.Code = 'MAINT'</c> rule, applied one last time.</b> HR's
    /// technician door used to add anyone in a MAINT department, whatever the column said. They become
    /// by-hand inclusions, so retiring the magic string drops nobody from the pool.</item>
    /// <item><b>Every other tick that its post does not explain is an exception.</b> No position is a
    /// technician role before this migration, so on a migrated database every existing tick is one —
    /// the legacy department backfill of <c>AddHRModule</c>'s, or a hand edit.</item>
    /// <item><b>Everyone else follows their post.</b> A no-op on a migrated database; on one built from
    /// the model, where a seeder may already have flagged posts, it lines the holders up.</item>
    /// </list>
    /// <para>Measured on <c>ErpSystemDB_UAT</c> before writing it: 0 ticks, 0 MAINT-department
    /// employees, so every statement matches no rows there.</para>
    ///
    /// <para>⚠ Guarded SQL throughout, as on every HR migration: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has both columns. The
    /// scaffold's <c>defaultValue: false</c> is kept as a real named <c>DEFAULT (0)</c>, which fills every
    /// existing row.</para>
    /// </remarks>
    public partial class AddTechnicianRoleFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.EmployeePositions', 'IsTechnicianRole') IS NULL
    ALTER TABLE [dbo].[EmployeePositions] ADD [IsTechnicianRole] bit NOT NULL
        CONSTRAINT [DF_EmployeePositions_IsTechnicianRole] DEFAULT (0);
IF COL_LENGTH('dbo.Employees', 'MaintenanceAssignmentSetByHand') IS NULL
    ALTER TABLE [dbo].[Employees] ADD [MaintenanceAssignmentSetByHand] bit NOT NULL
        CONSTRAINT [DF_Employees_MaintenanceAssignmentSetByHand] DEFAULT (0);");

            // A separate batch: SQL Server compiles a batch before running it, so these could not name
            // the columns in the same batch that adds them.
            migrationBuilder.Sql(@"
-- 1. The retired MAINT-department rule, one last time, as by-hand inclusions.
UPDATE e SET e.[CanBeAssignedToMaintenance] = 1, e.[MaintenanceAssignmentSetByHand] = 1
FROM [dbo].[Employees] e
INNER JOIN [dbo].[Departments] d ON d.[Id] = e.[DepartmentId]
INNER JOIN [dbo].[EmployeePositions] p ON p.[Id] = e.[PositionId]
WHERE d.[Code] = 'MAINT' AND e.[IsDeleted] = 0
  AND e.[MaintenanceAssignmentSetByHand] = 0 AND p.[IsTechnicianRole] = 0;

-- 2. A tick its post does not explain is somebody's decision.
UPDATE e SET e.[MaintenanceAssignmentSetByHand] = 1
FROM [dbo].[Employees] e
INNER JOIN [dbo].[EmployeePositions] p ON p.[Id] = e.[PositionId]
WHERE e.[CanBeAssignedToMaintenance] = 1 AND e.[MaintenanceAssignmentSetByHand] = 0
  AND p.[IsTechnicianRole] = 0;

-- 3. Everyone else follows their post.
UPDATE e SET e.[CanBeAssignedToMaintenance] = p.[IsTechnicianRole]
FROM [dbo].[Employees] e
INNER JOIN [dbo].[EmployeePositions] p ON p.[Id] = e.[PositionId]
WHERE e.[MaintenanceAssignmentSetByHand] = 0
  AND e.[CanBeAssignedToMaintenance] <> p.[IsTechnicianRole];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The default constraints first, found by column rather than trusted by name: a database built
            // from the model has none, and one built by this migration has the named ones. ⚠ The
            // CanBeAssignedToMaintenance values the backfill set are left as they are: the code this
            // rolls back to reads the column as its answer too.
            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].' + QUOTENAME(OBJECT_NAME(dc.parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints dc
WHERE (dc.parent_object_id = OBJECT_ID('dbo.EmployeePositions')
       AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = 'IsTechnicianRole')
   OR (dc.parent_object_id = OBJECT_ID('dbo.Employees')
       AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = 'MaintenanceAssignmentSetByHand');
IF LEN(@sql) > 0 EXEC sp_executesql @sql;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'MaintenanceAssignmentSetByHand') IS NOT NULL
    ALTER TABLE [dbo].[Employees] DROP COLUMN [MaintenanceAssignmentSetByHand];
IF COL_LENGTH('dbo.EmployeePositions', 'IsTechnicianRole') IS NOT NULL
    ALTER TABLE [dbo].[EmployeePositions] DROP COLUMN [IsTechnicianRole];");
        }
    }
}
