using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 25 slice 13b. Filters <c>IX_JobApplication_Vacancy_Candidate</c> so that a WITHDRAWN
    /// (or soft-deleted) application releases the candidate's slot on that vacancy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The index and the service disagreed, and the database won.</b> Unfiltered, this unique
    /// index on <c>(JobVacancyId, JobCandidateId)</c> said "one application ever". But
    /// <c>JobApplicationService.InternalApplyAsync</c>'s duplicate guard deliberately excludes
    /// <c>Withdrawn</c> — re-applying after withdrawing was the intended behaviour. The insert
    /// died here on a 2601 before the guard's intent could ever matter, so that predicate was
    /// unreachable code and the user got an opaque 500 rather than a refusal.
    /// </para>
    /// <para>
    /// This is the succession area's lesson arriving from the other direction: <b>a soft delete
    /// does not release a unique index</b> — and neither, it turns out, does a withdrawal.
    /// <c>IsDeleted</c> is excluded here for exactly the same reason.
    /// </para>
    /// <para>
    /// <b>Only <c>Withdrawn</c> (13) is released.</b> <c>Rejected</c> (12) and <c>Hired</c> (14)
    /// are deliberately still blocking: a candidate the organisation has turned down should not be
    /// able to re-enter the same vacancy by pressing Apply again, and a hired one has nothing left
    /// to apply for. The rule is "one LIVE application per candidate per vacancy", and withdrawing
    /// is the one exit the candidate themselves controls.
    /// </para>
    /// <para>
    /// Found by <c>run-slice13b.mjs</c> §7, which applies, withdraws, and re-applies. That leg
    /// exists because the portal's own withdraw dialog promises "you can apply again later while
    /// the vacancy is still open" — a promise the index was breaking.
    /// </para>
    /// <para>
    /// Written as guarded SQL like the rest of the HR chain, so it is safe to re-run against a
    /// database at either state — including one built from the EF model rather than from the
    /// chain, which is how this repo's <c>rebuild-db</c> works. No data changes: filtering a
    /// unique index only ever removes rows from its scope, so an index that built before will
    /// build after.
    /// </para>
    /// </remarks>
    public partial class FilterJobApplicationVacancyCandidateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobApplication_Vacancy_Candidate'
           AND object_id = OBJECT_ID('dbo.JobApplications'))
    DROP INDEX [IX_JobApplication_Vacancy_Candidate] ON [dbo].[JobApplications];

CREATE UNIQUE INDEX [IX_JobApplication_Vacancy_Candidate]
    ON [dbo].[JobApplications] ([JobVacancyId], [JobCandidateId])
    WHERE [Status] <> 13 AND [IsDeleted] = 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ Reversing this can FAIL, and that is honest rather than a defect: any vacancy that
            // has taken a withdrawn application plus a later one from the same candidate now holds
            // a pair the unfiltered index forbids. Rebuilding it would have to choose which of the
            // two real applications to destroy, so it refuses instead and leaves that decision to
            // whoever is reversing the migration.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobApplication_Vacancy_Candidate'
           AND object_id = OBJECT_ID('dbo.JobApplications'))
    DROP INDEX [IX_JobApplication_Vacancy_Candidate] ON [dbo].[JobApplications];

CREATE UNIQUE INDEX [IX_JobApplication_Vacancy_Candidate]
    ON [dbo].[JobApplications] ([JobVacancyId], [JobCandidateId]);
");
        }
    }
}
