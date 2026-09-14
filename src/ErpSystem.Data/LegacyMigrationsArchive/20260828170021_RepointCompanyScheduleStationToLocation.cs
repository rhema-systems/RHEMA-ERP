using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Company Schedule's three site references move from <c>WorkStations</c> to <c>Locations</c>:
    /// <c>CompanyEvents.StationId</c>, <c>MeetingRooms.StationId</c> and
    /// <c>BusinessClosures.StationId</c> become <c>LocationId</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> <c>MeetingRooms.StationId</c> is NOT NULL and pointed at <c>WorkStation</c> — an
    /// entity with an empty table, no repository implementation (still commented out in
    /// <c>ServiceCollectionExtensions</c>), no controller, no seed and no screen. A required foreign
    /// key with no lookup endpoint makes its own create form unfillable, so the meeting-room screen
    /// could not have been built at all. <c>Location</c> (structure → level → location) is the live
    /// tree the rest of HR already uses — the employee form binds to it, and <c>PayrollService</c>
    /// already reads <c>Employee.Location</c> first and falls back to <c>Employee.Station</c> only
    /// when it is null. <c>Employee.StationId</c> is deliberately left alone; it is unused by any
    /// form and is not this migration's business.
    /// </para>
    /// <para>
    /// <b>⚠ THE SCAFFOLD WAS NOT SAFE TO RUN AND IS REPLACED HERE.</b> Two separate problems:
    /// </para>
    /// <para>
    /// <b>1. It was not idempotent.</b> <c>rebuild-db</c> builds from the EF model, not from the
    /// migration chain, so a database rebuilt after the entity change already has <c>LocationId</c>
    /// and already points at <c>Locations</c>. Every <c>DropForeignKey</c> and <c>RenameColumn</c>
    /// in the generated body would then fail on an object that is not there. Each step below is
    /// guarded on <c>sys.foreign_keys</c> / <c>sys.indexes</c> / <c>COL_LENGTH</c>, so the migration
    /// is a no-op against a database that is already in the target shape.
    /// </para>
    /// <para>
    /// <b>2. It would have created a foreign key over orphan values.</b> A rename keeps the data.
    /// Any surviving <c>StationId</c> holds a <c>WorkStations.Id</c>, and those GUIDs do not exist
    /// in <c>Locations</c> — so <c>AddForeignKey</c> would fail on the first such row with an
    /// opaque constraint error. The two nullable columns are therefore cleared where they do not
    /// resolve to a real location, which is the honest answer: the old value pointed at a table
    /// nobody ever populated, so there is nothing to preserve and nothing to translate.
    /// </para>
    /// <para>
    /// <c>MeetingRooms.LocationId</c> is NOT NULL and cannot be cleared, so an orphan there is
    /// raised as an explicit error naming the rows rather than left to surface as a constraint
    /// violation. In practice <c>WorkStations</c> is unseeded and these tables are expected to be
    /// empty; the check exists so that if that assumption is wrong on some tenant, the failure says
    /// what to do about it.
    /// </para>
    /// </remarks>
    public partial class RepointCompanyScheduleStationToLocation : Migration
    {
        /// <summary>The three tables, and whether their site column tolerates NULL.</summary>
        private static readonly (string Table, bool Nullable)[] Targets =
        {
            ("CompanyEvents", true),
            ("MeetingRooms", false),
            ("BusinessClosures", true),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, nullable) in Targets)
            {
                // 1. Release the old foreign key. Guarded: absent on a rebuild-db database.
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE [name] = N'FK_{table}_WorkStations_StationId'
             AND [parent_object_id] = OBJECT_ID(N'[dbo].[{table}]'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [FK_{table}_WorkStations_StationId];");

                // 2. Rename the column, then the index that covers it. sp_rename on a column keeps
                //    the index pointing at it, but leaves the index's own name stale — hence both.
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'StationId') IS NOT NULL
   AND COL_LENGTH('dbo.{table}', 'LocationId') IS NULL
    EXEC sp_rename N'dbo.{table}.StationId', N'LocationId', N'COLUMN';");

                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE [name] = N'IX_{table}_StationId'
             AND [object_id] = OBJECT_ID(N'[dbo].[{table}]'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE [name] = N'IX_{table}_LocationId'
                     AND [object_id] = OBJECT_ID(N'[dbo].[{table}]'))
    EXEC sp_rename N'dbo.{table}.IX_{table}_StationId', N'IX_{table}_LocationId', N'INDEX';");

                // 3. Neutralise values that cannot satisfy the new foreign key. The old value was a
                //    WorkStations.Id; it will not be found in Locations.
                if (nullable)
                {
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'LocationId') IS NOT NULL
    UPDATE t SET t.[LocationId] = NULL
    FROM [dbo].[{table}] t
    WHERE t.[LocationId] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Locations] l WHERE l.[Id] = t.[LocationId]);");
                }
                else
                {
                    // Cannot be nulled, so refuse loudly and say which rows and what to do.
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'LocationId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [dbo].[{table}] t
               WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Locations] l WHERE l.[Id] = t.[LocationId]))
BEGIN
    DECLARE @orphans int =
        (SELECT COUNT(*) FROM [dbo].[{table}] t
         WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Locations] l WHERE l.[Id] = t.[LocationId]));
    DECLARE @msg nvarchar(400) =
        CONCAT(N'{table} has ', @orphans,
               N' row(s) whose site is not a Location. These held a WorkStations.Id, which was ',
               N'never populated. Point each row at a real Location (or delete it) and re-run: ',
               N'SELECT Id, RoomCode, RoomName, LocationId FROM {table} t WHERE NOT EXISTS ',
               N'(SELECT 1 FROM Locations l WHERE l.Id = t.LocationId);');
    THROW 51610, @msg, 1;
END;");
                }

                // 4. Attach the new foreign key. Restrict, matching the model configuration.
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'LocationId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE [name] = N'FK_{table}_Locations_LocationId'
                     AND [parent_object_id] = OBJECT_ID(N'[dbo].[{table}]'))
    ALTER TABLE [dbo].[{table}]
        ADD CONSTRAINT [FK_{table}_Locations_LocationId]
        FOREIGN KEY ([LocationId]) REFERENCES [dbo].[Locations] ([Id])
        ON DELETE NO ACTION;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, nullable) in Targets)
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE [name] = N'FK_{table}_Locations_LocationId'
             AND [parent_object_id] = OBJECT_ID(N'[dbo].[{table}]'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [FK_{table}_Locations_LocationId];");

                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'LocationId') IS NOT NULL
   AND COL_LENGTH('dbo.{table}', 'StationId') IS NULL
    EXEC sp_rename N'dbo.{table}.LocationId', N'StationId', N'COLUMN';");

                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE [name] = N'IX_{table}_LocationId'
             AND [object_id] = OBJECT_ID(N'[dbo].[{table}]'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE [name] = N'IX_{table}_StationId'
                     AND [object_id] = OBJECT_ID(N'[dbo].[{table}]'))
    EXEC sp_rename N'dbo.{table}.IX_{table}_LocationId', N'IX_{table}_StationId', N'INDEX';");

                // Going back means the value must be a WorkStations.Id again. Nothing here can
                // invent one, so the nullable columns are cleared and the NOT NULL one refuses —
                // the same shape as Up, mirrored. WorkStations being empty, a populated MeetingRooms
                // cannot be rolled back, and saying so beats a constraint error.
                if (nullable)
                {
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'StationId') IS NOT NULL
    UPDATE t SET t.[StationId] = NULL
    FROM [dbo].[{table}] t
    WHERE t.[StationId] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[WorkStations] w WHERE w.[Id] = t.[StationId]);");
                }
                else
                {
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'StationId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [dbo].[{table}] t
               WHERE NOT EXISTS (SELECT 1 FROM [dbo].[WorkStations] w WHERE w.[Id] = t.[StationId]))
    THROW 51611, N'{table} cannot be rolled back to WorkStations: its rows reference Locations, and no matching WorkStations rows exist. Empty the table first, or stay on this migration.', 1;");
                }

                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'StationId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE [name] = N'FK_{table}_WorkStations_StationId'
                     AND [parent_object_id] = OBJECT_ID(N'[dbo].[{table}]'))
    ALTER TABLE [dbo].[{table}]
        ADD CONSTRAINT [FK_{table}_WorkStations_StationId]
        FOREIGN KEY ([StationId]) REFERENCES [dbo].[WorkStations] ([Id])
        ON DELETE NO ACTION;");
            }
        }
    }
}
