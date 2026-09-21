using ErpSystem.Core.Entities.HR.Recruitment;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB CANDIDATE
// ============================================================================

#region Job Candidate

public interface IJobCandidateRepository : IGenericRepository<JobCandidate>
{
    /// <summary>Returns the candidate matching the unique candidate number.</summary>
    Task<JobCandidate?> GetByCandidateNumberAsync(string candidateNumber);

    /// <summary>
    /// Returns the candidate matching the given email address.
    /// <para><paramref name="tenantId"/> MUST be supplied by anonymous callers (public careers portal,
    /// portal registration): on an anonymous request there is no tenant claim, so the DbContext's global
    /// tenant filter cannot scope this query and it would otherwise match a candidate in ANY tenant.
    /// Authenticated callers may omit it — the global filter already scopes them.</para>
    /// </summary>
    Task<JobCandidate?> GetByEmailAsync(string email, Guid? tenantId = null);

    /// <summary>
    /// Returns a fully-loaded candidate including qualifications, work histories,
    /// referees, skills, interests, documents, and all applications.
    /// </summary>
    Task<JobCandidate?> GetWithFullDetailsAsync(Guid id);

    /// <summary>
    /// Returns the tenant's candidates currently in the active talent pool, with segment
    /// memberships loaded. Tenant-explicit because the DbContext's global tenant filter is inert
    /// in this solution — the parameterless form counted and paged every tenant's pool.
    /// </summary>
    Task<IEnumerable<JobCandidate>> GetTalentPoolCandidatesAsync(Guid tenantId);

    /// <summary>Returns candidates who have applied to a specific vacancy (via their applications).</summary>
    Task<IEnumerable<JobCandidate>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns the next candidate number for auto-generation.</summary>
    Task<string> GetNextCandidateNumberAsync();

    /// <summary>
    /// Tenant-explicit overload for anonymous callers (public career portal), which have no
    /// authenticated tenant claim for the underlying number sequence to resolve.
    /// </summary>
    Task<string> GetNextCandidateNumberAsync(Guid tenantId);

    // ── Talent pool — filtered queries ────────────────────────────────────────

    /// <summary>
    /// Returns paged talent pool candidates matching the given filter, scoped to the tenant.
    /// The tenant predicate belongs INSIDE this query: filtering after paging returned a
    /// cross-tenant TotalCount and short or empty pages.
    /// </summary>
    Task<(List<JobCandidate> Items, int TotalCount)> GetTalentPoolFilteredAsync(
        ErpSystem.Core.DTOs.HR.TalentPoolFilterDto filter,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns pool candidates whose last engagement was before the threshold date.</summary>
    Task<IEnumerable<JobCandidate>> GetDormantPoolCandidatesAsync(
        DateTime engagedBefore,
        CancellationToken cancellationToken = default);

    /// <summary>Returns pool candidates whose review date is in the past.</summary>
    Task<IEnumerable<JobCandidate>> GetOverdueReviewPoolCandidatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Returns a pool candidate with their segment memberships loaded.</summary>
    Task<JobCandidate?> GetWithSegmentsAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// CANDIDATE TALENT SEGMENT
// ============================================================================

#region Candidate Talent Segment

public interface ICandidateTalentSegmentRepository : IGenericRepository<CandidateTalentSegment>
{
    /// <summary>
    /// The tenant's segments, owner / target / job family and live memberships included, name
    /// order. Round 3 lane V: the read every list and detail goes through, so <c>MemberCount</c>
    /// and the three mirrored names are populated — before it, none of them were.
    /// </summary>
    Task<IEnumerable<CandidateTalentSegment>> GetForTenantAsync(Guid tenantId, bool activeOnly);

    /// <summary>One segment with the same graph as <see cref="GetForTenantAsync"/>.</summary>
    Task<CandidateTalentSegment?> GetDetailAsync(Guid segmentId);

    Task<IEnumerable<CandidateTalentSegment>> GetActiveByTenantAsync(Guid tenantId);
    Task<CandidateTalentSegment?> GetWithMembersAsync(Guid segmentId);
    Task<bool> NameExistsAsync(Guid tenantId, string name, Guid? excludeId = null);

    /// <summary>Live memberships on a segment — what a delete has to refuse over.</summary>
    Task<int> CountLiveMembersAsync(Guid segmentId);
}

#endregion

// ============================================================================
// CANDIDATE SEGMENT MEMBERSHIP
// ============================================================================

#region Candidate Segment Membership

public interface ICandidateSegmentMembershipRepository : IGenericRepository<CandidateSegmentMembership>
{
    Task<IEnumerable<CandidateSegmentMembership>> GetByCandidateIdAsync(Guid candidateId);
    Task<IEnumerable<CandidateSegmentMembership>> GetBySegmentIdAsync(Guid segmentId);
    Task<CandidateSegmentMembership?> GetByCandidateAndSegmentAsync(Guid candidateId, Guid segmentId);
}

#endregion

// ============================================================================
// CANDIDATE ENGAGEMENT EVENT
// ============================================================================

#region Candidate Engagement Event

public interface ICandidateEngagementEventRepository : IGenericRepository<CandidateEngagementEvent>
{
    Task<IEnumerable<CandidateEngagementEvent>> GetByCandidateIdAsync(Guid candidateId);
    Task<CandidateEngagementEvent?> GetLatestByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE QUALIFICATION
// ============================================================================

#region Job Candidate Qualification

public interface IJobCandidateQualificationRepository : IGenericRepository<JobCandidateQualification>
{
    /// <summary>Returns all qualifications for a candidate, ordered by year obtained descending.</summary>
    Task<IEnumerable<JobCandidateQualification>> GetByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE WORK HISTORY
// ============================================================================

#region Job Candidate Work History

public interface IJobCandidateWorkHistoryRepository : IGenericRepository<JobCandidateWorkHistory>
{
    /// <summary>Returns all work history entries for a candidate, ordered by start date descending.</summary>
    Task<IEnumerable<JobCandidateWorkHistory>> GetByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE REFEREE
// ============================================================================

#region Job Candidate Referee

public interface IJobCandidateRefereeRepository : IGenericRepository<JobCandidateReferee>
{
    /// <summary>Returns all referees for a candidate.</summary>
    Task<IEnumerable<JobCandidateReferee>> GetByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE SKILL
// ============================================================================

#region Job Candidate Skill

public interface IJobCandidateSkillRepository : IGenericRepository<JobCandidateSkill>
{
    /// <summary>Returns all skills for a candidate.</summary>
    Task<IEnumerable<JobCandidateSkill>> GetByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE INTEREST
// ============================================================================

#region Job Candidate Interest

public interface IJobCandidateInterestRepository : IGenericRepository<JobCandidateInterest>
{
    /// <summary>Returns all interest/preference entries for a candidate.</summary>
    Task<IEnumerable<JobCandidateInterest>> GetByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE DOCUMENT
// ============================================================================

#region Job Candidate Document

public interface IJobCandidateDocumentRepository : IGenericRepository<JobCandidateDocument>
{
    /// <summary>Returns all documents for a candidate, ordered by upload date descending.</summary>
    Task<IEnumerable<JobCandidateDocument>> GetByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE NOTE
// ============================================================================

#region Job Candidate Note

public interface IJobCandidateNoteRepository : IGenericRepository<JobCandidateNote>
{
    /// <summary>Returns all non-private notes for a candidate, ordered by creation date descending.</summary>
    Task<IEnumerable<JobCandidateNote>> GetByCandidateIdAsync(Guid candidateId);

    /// <summary>Returns all notes (including private) for a candidate — for recruiter/HR use only.</summary>
    Task<IEnumerable<JobCandidateNote>> GetAllByCandidateIdAsync(Guid candidateId);
}

#endregion

// ============================================================================
// JOB CANDIDATE LANGUAGE
// ============================================================================

#region Job Candidate Language

public interface IJobCandidateLanguageRepository : IGenericRepository<JobCandidateLanguage>
{
    /// <summary>Returns all language records for a candidate.</summary>
    Task<IEnumerable<JobCandidateLanguage>> GetByCandidateIdAsync(Guid candidateId);
}

#endregion
