using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB CANDIDATE SERVICE
// ============================================================================

#region Job Candidate Service

public interface IJobCandidateService
{
    // Queries
    Task<JobCandidateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobCandidateDto?> GetByCandidateNumberAsync(string candidateNumber, CancellationToken cancellationToken = default);
    Task<JobCandidateDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<JobCandidateSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateSummaryDto>> GetTalentPoolAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<JobCandidateDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    // CRUD
    Task<JobCandidateDto> CreateAsync(CreateJobCandidateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobCandidateDto> UpdateAsync(UpdateJobCandidateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Talent pool — basic (kept for backwards compat)
    Task<bool> AddToTalentPoolAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFromTalentPoolAsync(Guid candidateId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Talent pool — rich operations
    Task<bool> AddToTalentPoolRichAsync(Guid candidateId, AddToTalentPoolDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFromTalentPoolRichAsync(Guid candidateId, RemoveFromTalentPoolDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<TalentPoolPagedResultDto> GetTalentPoolFilteredAsync(TalentPoolFilterDto filter, CancellationToken cancellationToken = default);
    Task<TalentPoolAnalyticsDto> GetTalentPoolAnalyticsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TalentPoolCandidateDto> GetTalentPoolCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<bool> UpdateTalentPoolStatusAsync(Guid candidateId, ErpSystem.Core.Enums.TalentPoolCandidateStatus status, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> UpdateTalentPoolReviewDateAsync(Guid candidateId, DateTime reviewDate, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<RecruitmentBulkOperationResultDto> BulkTalentPoolOperationAsync(BulkTalentPoolOperationDto dto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<List<TalentPoolVacancyMatchResultDto>> MatchToVacancyAsync(Guid vacancyId, int topN, CancellationToken cancellationToken = default);
    Task<List<CandidateVacancyMatchResultDto>> MatchCandidateToVacanciesAsync(Guid candidateId, int topN = 10, CancellationToken cancellationToken = default);

    // Qualification operations
    Task<JobCandidateQualificationDto> AddQualificationAsync(CreateJobCandidateQualificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateQualificationDto>> GetQualificationsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<JobCandidateQualificationDto> UpdateQualificationAsync(UpdateJobCandidateQualificationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default);

    // Work history operations
    Task<JobCandidateWorkHistoryDto> AddWorkHistoryAsync(CreateJobCandidateWorkHistoryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateWorkHistoryDto>> GetWorkHistoriesAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<JobCandidateWorkHistoryDto> UpdateWorkHistoryAsync(UpdateJobCandidateWorkHistoryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteWorkHistoryAsync(Guid workHistoryId, CancellationToken cancellationToken = default);

    // Referee operations
    Task<JobCandidateRefereeDto> AddRefereeAsync(CreateJobCandidateRefereeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateRefereeDto>> GetRefereesAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<JobCandidateRefereeDto> UpdateRefereeAsync(UpdateJobCandidateRefereeDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRefereeAsync(Guid refereeId, CancellationToken cancellationToken = default);

    // Skill operations
    Task<JobCandidateSkillDto> AddSkillAsync(CreateJobCandidateSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateSkillDto>> GetSkillsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<JobCandidateSkillDto> UpdateSkillAsync(UpdateJobCandidateSkillDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSkillAsync(Guid skillId, CancellationToken cancellationToken = default);

    // Interest operations
    Task<JobCandidateInterestDto> AddInterestAsync(CreateJobCandidateInterestDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateInterestDto>> GetInterestsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<JobCandidateInterestDto> UpdateInterestAsync(UpdateJobCandidateInterestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInterestAsync(Guid interestId, CancellationToken cancellationToken = default);

    // Document operations
    /// <summary>
    /// Records a document the HR side has already pushed through the controlled-upload gate.
    ///
    /// <para>Replaces a <c>CreateJobCandidateDocumentDto</c> overload that took a caller-supplied
    /// <c>filePath</c>: nothing was scanned, nothing was stored, and the row recorded a path to a file
    /// the server had never received. The DTO is deleted rather than ignored so it cannot drift back —
    /// the same treatment the three recruitment attachment types got. Mirrors
    /// <c>ICandidatePortalService.AddDocumentAsync</c>, which is how the candidate's own uploads have
    /// always been written.</para>
    /// </summary>
    Task<JobCandidateDocumentDto> AddDocumentAsync(
        Guid candidateId,
        JobCandidateDocumentType documentType,
        string fileName,
        Guid tenantId,
        Guid createdByUserId,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null);
    Task<IEnumerable<JobCandidateDocumentDto>> GetDocumentsAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    // Note operations
    Task<JobCandidateNoteDto> AddNoteAsync(CreateJobCandidateNoteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCandidateNoteDto>> GetNotesAsync(Guid candidateId, bool includePrivate = false, CancellationToken cancellationToken = default);
    Task<JobCandidateNoteDto> UpdateNoteAsync(UpdateJobCandidateNoteDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// CANDIDATE TALENT SEGMENT SERVICE
// ============================================================================

#region Candidate Talent Segment Service

public interface ICandidateTalentSegmentService
{
    Task<IEnumerable<CandidateTalentSegmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CandidateTalentSegmentDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<CandidateTalentSegmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CandidateTalentSegmentDto> CreateAsync(CreateCandidateTalentSegmentDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<CandidateTalentSegmentDto> UpdateAsync(UpdateCandidateTalentSegmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Membership
    Task<CandidateSegmentMembershipDto> AddCandidateAsync(Guid candidateId, AddCandidateToSegmentDto dto, Guid tenantId, Guid addedByEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> RemoveCandidateAsync(Guid candidateId, Guid segmentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CandidateSegmentMembershipDto>> GetCandidateSegmentsAsync(Guid candidateId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// CANDIDATE ENGAGEMENT EVENT SERVICE
// ============================================================================

#region Candidate Engagement Event Service

public interface ICandidateEngagementEventService
{
    Task<IEnumerable<CandidateEngagementEventDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<CandidateEngagementEventDto> LogEventAsync(CreateCandidateEngagementEventDto dto, Guid tenantId, Guid recordedByEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
