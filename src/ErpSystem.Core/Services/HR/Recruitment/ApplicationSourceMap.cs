using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// The one map from an advert's channel to the application source it produces (round 3, lane A;
/// register row R-4; plan § 5.5). An application made from a posting link carries the posting and
/// takes its source from here; an application with no posting takes the source the door typed
/// (the careers site is the company website; HR's "Record an application" says what it was).
/// </summary>
/// <remarks>
/// <para>The two enums overlap without being the same list — <c>JobPostingChannel</c> names WHERE
/// the advert ran, <c>ApplicationSource</c> names HOW the applicant arrived — so the map is explicit
/// rather than a name match. The three job boards collapse onto <c>JobBoard</c>; the internal
/// portal has no source of its own (an internal applicant is flagged by <c>IsInternalCandidate</c>,
/// not by source) and lands on <c>Other</c>.</para>
/// </remarks>
public static class ApplicationSourceMap
{
    public static ApplicationSource FromChannel(JobPostingChannel channel) => channel switch
    {
        JobPostingChannel.CompanyWebsite => ApplicationSource.CompanyWebsite,
        JobPostingChannel.LinkedIn => ApplicationSource.LinkedIn,
        JobPostingChannel.JobBoard => ApplicationSource.JobBoard,
        JobPostingChannel.Indeed => ApplicationSource.JobBoard,
        JobPostingChannel.Glassdoor => ApplicationSource.JobBoard,
        JobPostingChannel.Agency => ApplicationSource.RecruitmentAgency,
        JobPostingChannel.Newspaper => ApplicationSource.NewspaperAd,
        JobPostingChannel.InternalPortal => ApplicationSource.Other,
        _ => ApplicationSource.Other,
    };
}
