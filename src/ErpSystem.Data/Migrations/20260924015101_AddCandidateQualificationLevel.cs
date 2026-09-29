using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 4, lane Q: the rung of the qualification ladder a candidate's qualification sits on,
    /// so that an "Education level" criterion can compare ranks instead of matching names.
    /// </summary>
    /// <remarks>
    /// <para><b><c>JobCandidateQualifications.QualificationLevelId</c></b>, nullable, restricted to
    /// <c>QualificationLevels</c>. It is the level the candidate or HR states (decision Q-D1). The
    /// effective level is this column, or else the catalogue entry's
    /// <c>Qualifications.QualificationLevelId</c>.</para>
    ///
    /// <para><b>The backfill (decision Q-D2)</b> fills the level of every existing typed Education
    /// row once, from its name. It runs in its own batch, after the column exists, and is keyed on
    /// VALUES — the name and the tenant's own rung CODE — never on a row id:</para>
    /// <list type="bullet">
    /// <item>BSc, BA, BEd, BEng, BTech, BBA, BCom, LLB, MBChB, Bachelor → <c>BDEG</c>;
    /// MSc, MBA, MPhil, MA, MEng, MEd, LLM, Master → <c>MDEG</c>; PhD, DPhil, Doctorate → <c>PHD</c>;
    /// HND; the postgraduate diploma and certificate; Diploma; WASSCE and SSSCE; BECE; a foundation
    /// certificate.</item>
    /// <item>Only rows with no level yet: a level anybody has set is never overwritten, and a second
    /// run touches nothing.</item>
    /// <item>A tenant whose ladder has no rung with that code gets nothing, rather than a guess.</item>
    /// <item>A catalogue-linked row is left alone. It takes its catalogue entry's level, which is the
    /// catalogue's job to hold (scenario 008 maps the demo tenant's).</item>
    /// </list>
    /// <para>Dry-run on <c>ErpSystemDB_UAT</c> before it was written: of 165 typed Education rows, 110
    /// read as Bachelor's, 37 as Master's and 5 as HND. 13 cannot be read, all of them the harness
    /// fixture "Something else entirely", and are left empty. The same rules are
    /// <c>TdcDemoRecruitmentHistorySeeder.LevelRules</c>, so a rebuilt database and a migrated one
    /// agree.</para>
    ///
    /// <para>⚠ Guarded SQL throughout, as on every HR migration: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has the column.</para>
    /// </remarks>
    public partial class AddCandidateQualificationLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.JobCandidateQualifications', 'QualificationLevelId') IS NULL
    ALTER TABLE [dbo].[JobCandidateQualifications] ADD [QualificationLevelId] uniqueidentifier NULL;");

            // Separate batches: SQL Server compiles a batch before running it, so nothing could name
            // the column in the batch that adds it.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_CandidateQualification_QualificationLevelId'
                 AND object_id = OBJECT_ID('dbo.JobCandidateQualifications'))
    CREATE INDEX [IX_CandidateQualification_QualificationLevelId]
        ON [dbo].[JobCandidateQualifications] ([QualificationLevelId]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_JobCandidateQualifications_QualificationLevels_QualificationLevelId')
    ALTER TABLE [dbo].[JobCandidateQualifications]
        ADD CONSTRAINT [FK_JobCandidateQualifications_QualificationLevels_QualificationLevelId]
        FOREIGN KEY ([QualificationLevelId]) REFERENCES [dbo].[QualificationLevels] ([Id]);");

            // The one-off backfill (Q-D2). First matching rule wins, most specific first: a
            // postgraduate diploma before a diploma. `[^a-z0-9]` after an abbreviation stands for a
            // word boundary, so 'ma' reads 'MA Economics' and not 'Management'.
            migrationBuilder.Sql(@"
;WITH typed AS (
    SELECT q.[Id], q.[TenantId], LOWER(LTRIM(RTRIM(q.[QualificationFreeText]))) AS n
    FROM [dbo].[JobCandidateQualifications] q
    WHERE q.[IsDeleted] = 0 AND q.[QualificationLevelId] IS NULL AND q.[QualificationId] IS NULL
      AND q.[QualificationType] = 1 AND q.[QualificationFreeText] IS NOT NULL
), coded AS (
    SELECT t.[Id], t.[TenantId], CASE
        WHEN t.n LIKE 'phd%' OR t.n LIKE 'ph.d%' OR t.n LIKE 'ph d%' OR t.n LIKE 'dphil%' OR t.n LIKE 'd.phil%'
          OR t.n LIKE 'doctor of philosophy%' OR t.n LIKE 'doctorate%' THEN 'PHD'
        WHEN t.n LIKE 'postgraduate diploma%' OR t.n LIKE 'post-graduate diploma%' OR t.n LIKE 'post graduate diploma%'
          OR t.n LIKE 'pgdip%' OR t.n LIKE 'pg dip%' OR t.n LIKE 'pg.dip%' THEN 'PGDIP'
        WHEN t.n LIKE 'postgraduate certificate%' OR t.n LIKE 'post-graduate certificate%' OR t.n LIKE 'post graduate certificate%'
          OR t.n LIKE 'pgcert%' OR t.n LIKE 'pg cert%' OR t.n LIKE 'pg.cert%' THEN 'PGCERT'
        WHEN t.n IN ('msc','m.sc','m sc','mba','mphil','m.phil','m phil','ma','m.a','m.a.','meng','m.eng','m eng','med','m.ed','m ed','llm','master','masters')
          OR t.n LIKE 'msc[^a-z0-9]%' OR t.n LIKE 'm.sc[^a-z0-9]%' OR t.n LIKE 'm sc[^a-z0-9]%' OR t.n LIKE 'mba[^a-z0-9]%'
          OR t.n LIKE 'mphil[^a-z0-9]%' OR t.n LIKE 'm.phil[^a-z0-9]%' OR t.n LIKE 'm phil[^a-z0-9]%'
          OR t.n LIKE 'ma[^a-z0-9]%' OR t.n LIKE 'm.a[^a-z0-9]%' OR t.n LIKE 'meng[^a-z0-9]%' OR t.n LIKE 'm.eng[^a-z0-9]%'
          OR t.n LIKE 'm eng[^a-z0-9]%' OR t.n LIKE 'med[^a-z0-9]%' OR t.n LIKE 'm.ed[^a-z0-9]%' OR t.n LIKE 'm ed[^a-z0-9]%'
          OR t.n LIKE 'llm[^a-z0-9]%' OR t.n LIKE 'master[^a-z0-9]%' OR t.n LIKE 'masters[^a-z0-9]%' THEN 'MDEG'
        WHEN t.n IN ('bsc','b.sc','b sc','ba','b.a','b.a.','bed','b.ed','b ed','beng','b.eng','b eng','btech','b.tech','b tech','bba','bcom','b.com','b com','llb','mbchb','bachelor','bachelors')
          OR t.n LIKE 'bsc[^a-z0-9]%' OR t.n LIKE 'b.sc[^a-z0-9]%' OR t.n LIKE 'b sc[^a-z0-9]%'
          OR t.n LIKE 'ba[^a-z0-9]%' OR t.n LIKE 'b.a[^a-z0-9]%' OR t.n LIKE 'bed[^a-z0-9]%' OR t.n LIKE 'b.ed[^a-z0-9]%' OR t.n LIKE 'b ed[^a-z0-9]%'
          OR t.n LIKE 'beng[^a-z0-9]%' OR t.n LIKE 'b.eng[^a-z0-9]%' OR t.n LIKE 'b eng[^a-z0-9]%'
          OR t.n LIKE 'btech[^a-z0-9]%' OR t.n LIKE 'b.tech[^a-z0-9]%' OR t.n LIKE 'b tech[^a-z0-9]%'
          OR t.n LIKE 'bba[^a-z0-9]%' OR t.n LIKE 'bcom[^a-z0-9]%' OR t.n LIKE 'b.com[^a-z0-9]%' OR t.n LIKE 'b com[^a-z0-9]%'
          OR t.n LIKE 'llb[^a-z0-9]%' OR t.n LIKE 'mbchb[^a-z0-9]%' OR t.n LIKE 'bachelor[^a-z0-9]%' OR t.n LIKE 'bachelors[^a-z0-9]%' THEN 'BDEG'
        WHEN t.n = 'hnd' OR t.n LIKE 'hnd[^a-z0-9]%' OR t.n LIKE 'higher national diploma%' THEN 'HND'
        WHEN t.n LIKE 'wassce%' OR t.n LIKE 'sssce%' OR t.n LIKE 'senior high%' OR t.n LIKE 'west african senior school certificate%'
          OR t.n LIKE 'high school diploma%' THEN 'WASSCE'
        WHEN t.n LIKE 'bece%' OR t.n LIKE 'basic education certificate%' THEN 'BECE'
        WHEN t.n = 'diploma' OR t.n LIKE 'diploma[^a-z0-9]%' OR t.n LIKE 'advanced diploma%' OR t.n LIKE 'ordinary national diploma%'
          OR t.n LIKE 'higher diploma%' THEN 'DIP'
        WHEN t.n LIKE 'foundation certificate%' OR t.n LIKE 'certificate in%' THEN 'CERT'
    END AS [Code]
    FROM typed t
)
UPDATE q
   SET q.[QualificationLevelId] = l.[Id],
       q.[UpdatedAt] = SYSUTCDATETIME(),
       q.[UpdatedBy] = 'AddCandidateQualificationLevel backfill'
FROM [dbo].[JobCandidateQualifications] q
INNER JOIN coded c ON c.[Id] = q.[Id]
INNER JOIN [dbo].[QualificationLevels] l
        ON l.[TenantId] = c.[TenantId] AND l.[Code] = c.[Code] AND l.[IsDeleted] = 0 AND l.[IsActive] = 1
WHERE c.[Code] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ The levels the backfill set go with the column; the code this rolls back to has no
            // use for them.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = 'FK_JobCandidateQualifications_QualificationLevels_QualificationLevelId')
    ALTER TABLE [dbo].[JobCandidateQualifications]
        DROP CONSTRAINT [FK_JobCandidateQualifications_QualificationLevels_QualificationLevelId];

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_CandidateQualification_QualificationLevelId'
             AND object_id = OBJECT_ID('dbo.JobCandidateQualifications'))
    DROP INDEX [IX_CandidateQualification_QualificationLevelId] ON [dbo].[JobCandidateQualifications];");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.JobCandidateQualifications', 'QualificationLevelId') IS NOT NULL
    ALTER TABLE [dbo].[JobCandidateQualifications] DROP COLUMN [QualificationLevelId];");
        }
    }
}
