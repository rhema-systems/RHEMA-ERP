using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// The lookup <c>.Include</c> chain for every area-17/18 read, in one place per entity.
/// </summary>
/// <remarks>
/// <para>Written for slice 1 after a read probe measured the alternative. Each of these entities
/// projects to a DTO carrying resolved <c>*Name</c> fields — <c>PositionTitle</c>,
/// <c>PreparedByName</c>, <c>JobFamilyName</c>, <c>CompetencyName</c>, <c>ApprovedByName</c> and
/// the rest — and a read that omits the navigation still returns <b>200 with those fields blank</b>.
/// Nothing fails; a screen built on it simply shows a column of dashes.</para>
///
/// <para>The chains had drifted apart exactly as that failure mode predicts: the by-id reads
/// carried all ten job-description lookups while the list, paged, by-position and by-status reads
/// carried two, so the same row rendered differently depending on which endpoint fetched it. The
/// fix is not "add the missing includes" — that decays again the next time a lookup is added — it
/// is one chain per entity that every read calls.</para>
///
/// <para>⚠ When a new lookup FK is added to one of these entities, add it here. A `*Name` field on
/// a DTO with no matching `.Include` in this file is a blank column waiting to ship.</para>
/// </remarks>
public static class JobArchitectureQueryExtensions
{
    /// <summary>Position, the three actors, and the five classification lookups.</summary>
    public static IQueryable<JobDescription> WithLookups(this IQueryable<JobDescription> query)
        => query
            .Include(jd => jd.Position)
            .Include(jd => jd.PreparedBy)
            .Include(jd => jd.ReviewedBy)
            .Include(jd => jd.ApprovedBy)
            .Include(jd => jd.StaffLevel)
            .Include(jd => jd.Union)
            .Include(jd => jd.SuggestedSalaryGrade)
            .Include(jd => jd.JobFamily)
            .Include(jd => jd.JobSubFamily)
            .Include(jd => jd.JobLevel);

    /// <summary>Both sides of the requirement: which position, and which competency.</summary>
    public static IQueryable<PositionCompetency> WithLookups(this IQueryable<PositionCompetency> query)
        => query
            .Include(pc => pc.Position)
            .Include(pc => pc.Competency);

    /// <summary>The competency an indicator belongs to, and the skill it names.</summary>
    /// <remarks>
    /// Added in slice 5. The repository reads had drifted apart in the usual way: the by-competency
    /// list included the skill but not the competency, the by-skill list the reverse, and the
    /// service's by-id helper carried neither. Slice 1 missed it because the read probe's fixture
    /// had no skill indicators in it - there were none in the database to have.
    /// </remarks>
    public static IQueryable<CompetencySkillIndicator> WithLookups(this IQueryable<CompetencySkillIndicator> query)
        => query
            .Include(i => i.Competency)
            .Include(i => i.Skill);

    /// <summary>Who was assessed, on what, and by whom.</summary>
    public static IQueryable<EmployeeCompetency> WithLookups(this IQueryable<EmployeeCompetency> query)
        => query
            .Include(ec => ec.Employee)
            .Include(ec => ec.Competency)
            .Include(ec => ec.AssessedBy);

    /// <summary>Where the budget sits in the organisation, and who approved it.</summary>
    public static IQueryable<ManpowerBudget> WithLookups(this IQueryable<ManpowerBudget> query)
        => query
            .Include(b => b.OrganizationLevel)
            .Include(b => b.OrganizationUnit)
            .Include(b => b.ApprovedBy);
}
