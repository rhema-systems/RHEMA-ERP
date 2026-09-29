using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane M — an orientation session's external facilitator can be picked from the training
    /// vendor register (decision D-8: the existing <c>TrainingVendor</c> + <c>TrainerProfile</c>
    /// register, not a new one).
    /// </summary>
    /// <remarks>
    /// <para><b>Two nullable references on <c>OrientationSessionFacilitators</c></b>, each indexed and
    /// each a foreign key: <c>ExternalFacilitatorVendorId</c> → <c>TrainingVendors</c> and
    /// <c>ExternalFacilitatorTrainerProfileId</c> → <c>TrainerProfiles</c>. The three existing
    /// <c>ExternalFacilitator…</c> text columns stay, as the snapshot the service writes from the
    /// register when a pick is made or changed. No data changes: every existing facilitator keeps its
    /// typed details and simply has no register pick.</para>
    ///
    /// <para><b>The keys do not act on delete</b> (<c>NO ACTION</c>, EF's Restrict), and in practice
    /// never fire at all: the register's deletes are soft. What keeps a booked vendor in the register
    /// is the service check (lane M3), which refuses to delete a vendor or trainer named on a session
    /// that has not happened yet.</para>
    ///
    /// <para><b>⚠ Guarded SQL throughout</b>, as on every HR migration: <c>rebuild-db</c> and the UAT
    /// builder create the schema from the EF model, so a rebuilt database already has all of this and
    /// a bare <c>AddColumn</c> would stop the chain for everyone. Each step is its own batch, so a
    /// column exists before the index and key that name it are created.</para>
    /// </remarks>
    public partial class AddOrientationFacilitatorRegisterPick : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrientationSessionFacilitators', 'ExternalFacilitatorVendorId') IS NULL
    ALTER TABLE [dbo].[OrientationSessionFacilitators] ADD [ExternalFacilitatorVendorId] uniqueidentifier NULL;
IF COL_LENGTH('dbo.OrientationSessionFacilitators', 'ExternalFacilitatorTrainerProfileId') IS NULL
    ALTER TABLE [dbo].[OrientationSessionFacilitators] ADD [ExternalFacilitatorTrainerProfileId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationSessionFacilitators_ExternalFacilitatorVendorId'
               AND object_id = OBJECT_ID('dbo.OrientationSessionFacilitators'))
    CREATE INDEX [IX_OrientationSessionFacilitators_ExternalFacilitatorVendorId]
        ON [dbo].[OrientationSessionFacilitators] ([ExternalFacilitatorVendorId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrientationSessionFacilitators_ExternalFacilitatorTrainerProfileId'
               AND object_id = OBJECT_ID('dbo.OrientationSessionFacilitators'))
    CREATE INDEX [IX_OrientationSessionFacilitators_ExternalFacilitatorTrainerProfileId]
        ON [dbo].[OrientationSessionFacilitators] ([ExternalFacilitatorTrainerProfileId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrientationSessionFacilitators_TrainingVendors_ExternalFacilitatorVendorId'
               AND parent_object_id = OBJECT_ID('dbo.OrientationSessionFacilitators'))
    ALTER TABLE [dbo].[OrientationSessionFacilitators]
        ADD CONSTRAINT [FK_OrientationSessionFacilitators_TrainingVendors_ExternalFacilitatorVendorId]
        FOREIGN KEY ([ExternalFacilitatorVendorId]) REFERENCES [dbo].[TrainingVendors] ([Id]);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrientationSessionFacilitators_TrainerProfiles_ExternalFacilitatorTrainerProfileId'
               AND parent_object_id = OBJECT_ID('dbo.OrientationSessionFacilitators'))
    ALTER TABLE [dbo].[OrientationSessionFacilitators]
        ADD CONSTRAINT [FK_OrientationSessionFacilitators_TrainerProfiles_ExternalFacilitatorTrainerProfileId]
        FOREIGN KEY ([ExternalFacilitatorTrainerProfileId]) REFERENCES [dbo].[TrainerProfiles] ([Id]);");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Keys and indexes are found by COLUMN, not by name, so Down clears whatever a model-built
        /// database called them — SQL Server refuses to drop a column while either still names it.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[OrientationSessionFacilitators] DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.OrientationSessionFacilitators')
  AND COL_NAME(fc.parent_object_id, fc.parent_column_id) IN ('ExternalFacilitatorVendorId', 'ExternalFacilitatorTrainerProfileId');
IF LEN(@sql) > 0 EXEC sp_executesql @sql;");

            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON [dbo].[OrientationSessionFacilitators];'
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.OrientationSessionFacilitators')
  AND i.is_primary_key = 0
  AND EXISTS (SELECT 1 FROM sys.index_columns ic
              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                AND COL_NAME(ic.object_id, ic.column_id) IN ('ExternalFacilitatorVendorId', 'ExternalFacilitatorTrainerProfileId'));
IF LEN(@sql) > 0 EXEC sp_executesql @sql;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrientationSessionFacilitators', 'ExternalFacilitatorTrainerProfileId') IS NOT NULL
    ALTER TABLE [dbo].[OrientationSessionFacilitators] DROP COLUMN [ExternalFacilitatorTrainerProfileId];
IF COL_LENGTH('dbo.OrientationSessionFacilitators', 'ExternalFacilitatorVendorId') IS NOT NULL
    ALTER TABLE [dbo].[OrientationSessionFacilitators] DROP COLUMN [ExternalFacilitatorVendorId];");
        }
    }
}
