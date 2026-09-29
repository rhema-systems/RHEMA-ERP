using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane A: a job candidate joins the shared administrative-geography tree.
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> The vacancy Location shortlisting criterion matched the criterion's typed
    /// labels against the candidate's typed city by ordinal substring, so "Greater Accra" found
    /// nobody recorded in Tema and "Accra" matched "Accra Central" by luck rather than by meaning.
    /// Both sides now reference <c>GeoArea</c> and matching is tree containment. The free-text
    /// <c>City</c> survives as the fallback for the candidates — most of them — whose country has no
    /// scheme loaded.</para>
    ///
    /// <para><b>The two columns.</b> <c>GeoAreaId</c> is <b>one FK, not one per tier</b>: it points
    /// at the lowest tier known and the ancestors come from <c>GeoArea.Path</c>, which is what lets
    /// a scheme gain a fifth tier without touching this table. <c>Region</c> is a display snapshot
    /// written from the tree, never typed — the same shape as <c>Employee.State</c>.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these columns, this
    /// index and this key — and a bare <c>AddColumn</c> stops the chain for everyone.</para>
    ///
    /// <para><b>⚠ No <c>defaultValue</c> anywhere here, and that is deliberate.</b> The recurring
    /// trap in this module is that EF's scaffolded default for a value type is zero and zero is
    /// almost never the real default — recorded three times, most recently on
    /// <c>LeaveYearStartMonth</c>, where it would have written month zero. Both columns below are
    /// genuinely nullable: a candidate recorded before the tree existed, or living in a country with
    /// no scheme, correctly has no area and no region. Null is the right answer, not a placeholder.</para>
    ///
    /// <para><b>⚠ <c>NO ACTION</c> on the foreign key, matching every other <c>GeoAreaId</c>.</b>
    /// Geography deletes are SOFT, so this key is never what protects an area in use — the
    /// <c>JobCandidateGeoAreaConsumer</c> probe registered in <c>GeoAreaConsumers.cs</c> is. A
    /// cascade here would be a promise the delete path never keeps.</para>
    ///
    /// <para>The snapshot frozen onto an application (<c>ApplicationCandidateSnapshot</c>) gained
    /// two fields in the same slice and needs <b>no schema</b>: it is an unmapped POCO serialised
    /// into the existing <c>ProfileSnapshotJson</c> column, and rows written before this deserialise
    /// with both fields null, which is exactly the fallback the scorer expects.</para>
    /// </remarks>
    public partial class AddRecruitmentGeography : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

        /// <remarks>
        /// The default constraint is looked up rather than named, so this works whether the column
        /// was created by this migration or by the EF model on a rebuilt database, where the name is
        /// server-generated. Guessing it would leave the column undroppable.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{column} sysname;
    SELECT @df_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private const string Candidates = "JobCandidates";
        private const string GeoAreaIndex = "IX_JobCandidates_GeoAreaId";
        private const string GeoAreaFk = "FK_JobCandidates_GeoAreas_GeoAreaId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(Candidates, "GeoAreaId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Candidates, "Region", "nvarchar(100) NULL"));
            migrationBuilder.Sql(CreateIndex(Candidates, GeoAreaIndex, "[GeoAreaId]"));
            migrationBuilder.Sql(AddForeignKey(Candidates, GeoAreaFk, "GeoAreaId", "GeoAreas"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Candidates, GeoAreaFk));
            migrationBuilder.Sql(DropIndex(Candidates, GeoAreaIndex));
            migrationBuilder.Sql(DropColumn(Candidates, "GeoAreaId"));
            migrationBuilder.Sql(DropColumn(Candidates, "Region"));
        }
    }
}
