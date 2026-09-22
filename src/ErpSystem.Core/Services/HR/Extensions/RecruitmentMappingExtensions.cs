using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
// For JobVacancyService.AllowedNextStatuses (G-5.7). ⚠ The folder is Services/HR/Extensions but the
// namespace is ErpSystem.Application.HR.Extensions, so nothing under Core.Services.HR resolves here
// implicitly — the path does not imply the namespace in this file.
using ErpSystem.Core.Services.HR;

namespace ErpSystem.Application.HR.Extensions;

public static class RecruitmentMappingExtensions
{
    // ========================================================================
    // JOB VACANCY
    // ========================================================================

    #region JobVacancy

    public static JobVacancyDto ToDto(this JobVacancy entity)
    {
        return new JobVacancyDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            VacancyNumber = entity.VacancyNumber,
            CustomAdvertTitle = entity.CustomAdvertTitle,
            JobTitle = entity.JobTitle,
            StaffRequisitionId = entity.StaffRequisitionId,
            RequisitionNumber = entity.Requisition?.RequisitionNumber ?? string.Empty,
            OrgUnitName = entity.Requisition?.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            NumberOfPositions = entity.NumberOfPositions,
            HiringManagerId = entity.HiringManagerId,
            HiringManagerName = entity.HiringManager?.FullName,
            RecruiterId = entity.RecruiterId,
            RecruiterName = entity.Recruiter?.FullName,
            VacancyStatus = entity.VacancyStatus,
            PublishDate = entity.PublishDate,
            ActualPublishDate = entity.ActualPublishDate,
            ApplicationDeadline = entity.ApplicationDeadline,
            ShortlistingDeadline = entity.ShortlistingDeadline,
            NumberOfInterviewRounds = entity.NumberOfInterviewRounds,
            ClosedDate = entity.ClosedDate,
            FilledDate = entity.FilledDate,
            ClosureReason = entity.ClosureReason,
            ClosureNotes = entity.ClosureNotes,
            IsSalaryVisible = entity.IsSalaryVisible,
            EmploymentType = entity.EmploymentType,
            WorkMode = entity.WorkMode,
            SalaryRangeMin = entity.SalaryRangeMin,
            SalaryRangeMax = entity.SalaryRangeMax,
            SalaryCurrencyCode = entity.SalaryCurrencyCode,
            RequiredMinExperienceYears = entity.RequiredMinExperienceYears,
            KeyBenefitsSummary = entity.KeyBenefitsSummary,
            TargetStartDate = entity.TargetStartDate,
            RequiresWrittenTest = entity.RequiresWrittenTest,
            RequiresPracticalTest = entity.RequiresPracticalTest,
            RecruitmentPipelineId = entity.RecruitmentPipelineId,
            PipelineName = entity.Pipeline?.Name,
            AllowInternalCandidates = entity.AllowInternalCandidates,
            AllowExternalCandidates = entity.AllowExternalCandidates,
            ApplicationCount = entity.ApplicationCount,
            ShortlistedCount = entity.ShortlistedCount,
            InterviewCount = entity.InterviewCount,
            OfferCount = entity.OfferCount,
            HireCount = entity.HireCount,
            ShortlistCompletedAt = entity.ShortlistCompletedAt,
            TimeToShortlistDays = entity.TimeToShortlistDays,
            ShortlistingSlaBreached = entity.ShortlistingSlaBreached,
            TestScoreWeight = entity.TestScoreWeight,
            InternalCandidateBoostPoints = entity.InternalCandidateBoostPoints,
            IsBlindScreeningEnabled = entity.IsBlindScreeningEnabled,
            // G-5.7: the picker asks the server what is legal rather than guessing.
            AllowedNextStatuses = JobVacancyService.AllowedNextStatuses(entity.VacancyStatus).ToList(),
            WorkflowInstanceId = entity.WorkflowInstanceId,
            ShortlistApprovalStatus  = entity.ShortlistApprovalStatus,
            ShortlistSubmittedAt     = entity.ShortlistSubmittedAt,
            ShortlistSubmittedByName = entity.ShortlistSubmittedBy?.FullName,
            ShortlistApprovedAt      = entity.ShortlistApprovedAt,
            ShortlistApprovedByName  = entity.ShortlistApprovedBy?.FullName,
            ShortlistApprovalNotes   = entity.ShortlistApprovalNotes,
            AutoShortlistMinScore               = entity.AutoShortlistMinScore,
            AutoShortlistRequireAllMandatory    = entity.AutoShortlistRequireAllMandatory,
        };
    }

    public static JobVacancySummaryDto ToSummaryDto(this JobVacancy entity)
    {
        return new JobVacancySummaryDto
        {
            Id = entity.Id,
            VacancyNumber = entity.VacancyNumber,
            JobTitle = entity.JobTitle,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            StaffRequisitionId = entity.StaffRequisitionId,
            RequisitionNumber = entity.Requisition?.RequisitionNumber,
            OrgUnitName = entity.Requisition?.OrganizationUnit?.Name,
            VacancyStatus = entity.VacancyStatus,
            EmploymentType = entity.EmploymentType,
            PublishDate = entity.PublishDate,
            ApplicationDeadline = entity.ApplicationDeadline,
            ShortlistingDeadline = entity.ShortlistingDeadline,
            NumberOfPositions = entity.NumberOfPositions,
            HiringManagerName = entity.HiringManager?.FullName,
            RecruiterName = entity.Recruiter?.FullName,
            AllowInternalCandidates = entity.AllowInternalCandidates,
            AllowExternalCandidates = entity.AllowExternalCandidates,
            ApplicationCount = entity.ApplicationCount,
            ShortlistedCount = entity.ShortlistedCount,
            // G-15.1: the dashboard's funnel needs a people-count for the interview stage.
            InterviewCount = entity.InterviewCount,
            OfferCount = entity.OfferCount,
            CreatedAt = entity.CreatedAt,
        };
    }

    public static JobVacancyDetailDto ToDetailDto(this JobVacancy entity)
    {
        var dto = new JobVacancyDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            VacancyNumber = entity.VacancyNumber,
            CustomAdvertTitle = entity.CustomAdvertTitle,
            JobTitle = entity.JobTitle,
            StaffRequisitionId = entity.StaffRequisitionId,
            RequisitionNumber = entity.Requisition?.RequisitionNumber ?? string.Empty,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            NumberOfPositions = entity.NumberOfPositions,
            HiringManagerId = entity.HiringManagerId,
            HiringManagerName = entity.HiringManager?.FullName,
            RecruiterId = entity.RecruiterId,
            RecruiterName = entity.Recruiter?.FullName,
            VacancyStatus = entity.VacancyStatus,
            PublishDate = entity.PublishDate,
            ActualPublishDate = entity.ActualPublishDate,
            ApplicationDeadline = entity.ApplicationDeadline,
            ShortlistingDeadline = entity.ShortlistingDeadline,
            NumberOfInterviewRounds = entity.NumberOfInterviewRounds,
            ClosedDate = entity.ClosedDate,
            FilledDate = entity.FilledDate,
            ClosureReason = entity.ClosureReason,
            ClosureNotes = entity.ClosureNotes,
            IsSalaryVisible = entity.IsSalaryVisible,
            EmploymentType = entity.EmploymentType,
            WorkMode = entity.WorkMode,
            SalaryRangeMin = entity.SalaryRangeMin,
            SalaryRangeMax = entity.SalaryRangeMax,
            SalaryCurrencyCode = entity.SalaryCurrencyCode,
            RequiredMinExperienceYears = entity.RequiredMinExperienceYears,
            KeyBenefitsSummary = entity.KeyBenefitsSummary,
            TargetStartDate = entity.TargetStartDate,
            RequiresWrittenTest = entity.RequiresWrittenTest,
            RequiresPracticalTest = entity.RequiresPracticalTest,
            RecruitmentPipelineId = entity.RecruitmentPipelineId,
            PipelineName = entity.Pipeline?.Name,
            AllowInternalCandidates = entity.AllowInternalCandidates,
            AllowExternalCandidates = entity.AllowExternalCandidates,
            ApplicationCount = entity.ApplicationCount,
            ShortlistedCount = entity.ShortlistedCount,
            InterviewCount = entity.InterviewCount,
            OfferCount = entity.OfferCount,
            HireCount = entity.HireCount,
            ShortlistCompletedAt = entity.ShortlistCompletedAt,
            TimeToShortlistDays = entity.TimeToShortlistDays,
            ShortlistingSlaBreached = entity.ShortlistingSlaBreached,
            TestScoreWeight = entity.TestScoreWeight,
            InternalCandidateBoostPoints = entity.InternalCandidateBoostPoints,
            IsBlindScreeningEnabled = entity.IsBlindScreeningEnabled,
            // G-5.7: the picker asks the server what is legal rather than guessing.
            AllowedNextStatuses = JobVacancyService.AllowedNextStatuses(entity.VacancyStatus).ToList(),
            WorkflowInstanceId = entity.WorkflowInstanceId,
            Attachments = entity.Attachments?.Select(a => a.ToDto()).ToList() ?? new(),
            JobPostings = entity.JobPostings?.Select(p => p.ToSummaryDto()).ToList() ?? new(),
            ShortlistingCriteria = entity.ShortlistingCriteria?.Select(c => c.ToDto()).ToList() ?? new(),
            Applications = entity.JobApplications?.Select(a => a.ToSummaryDto()).ToList() ?? new(),
            Interviews = entity.Interviews?.Select(i => i.ToSummaryDto()).ToList() ?? new(),
            StatusHistory = entity.StatusHistory?.Select(h => h.ToDto()).ToList() ?? new(),
        };
        return dto;
    }

    public static JobVacancy ToEntity(this CreateJobVacancyDto dto, Guid tenantId, Guid userId)
    {
        return new JobVacancy
        {
            TenantId = tenantId,
            StaffRequisitionId = dto.StaffRequisitionId,
            CustomAdvertTitle = dto.CustomAdvertTitle,
            NumberOfPositions = dto.NumberOfPositions,
            HiringManagerId = dto.HiringManagerId,
            RecruiterId = dto.RecruiterId,
            VacancyStatus = JobVacancyStatus.Draft,
            ApplicationDeadline = dto.ApplicationDeadline,
            ShortlistingDeadline = dto.ShortlistingDeadline,
            NumberOfInterviewRounds = dto.NumberOfInterviewRounds,
            TargetStartDate = dto.TargetStartDate,
            IsSalaryVisible = dto.IsSalaryVisible,
            WorkMode = dto.WorkMode,
            SalaryRangeMin = dto.SalaryRangeMin,
            SalaryRangeMax = dto.SalaryRangeMax,
            SalaryCurrencyCode = dto.SalaryCurrencyCode,
            RequiredMinExperienceYears = dto.RequiredMinExperienceYears,
            KeyBenefitsSummary = dto.KeyBenefitsSummary,
            RequiresWrittenTest = dto.RequiresWrittenTest,
            RequiresPracticalTest = dto.RequiresPracticalTest,
            IsBlindScreeningEnabled = dto.IsBlindScreeningEnabled,
            // G-5.2: carried on the write DTOs since 2026-09-15. Before that these two lived on the
            // read DTO alone, so both scoring branches were permanently switched off at 0.
            TestScoreWeight = dto.TestScoreWeight,
            InternalCandidateBoostPoints = dto.InternalCandidateBoostPoints,
            RecruitmentPipelineId = dto.RecruitmentPipelineId,
            AutoShortlistMinScore            = dto.AutoShortlistMinScore,
            AutoShortlistRequireAllMandatory = dto.AutoShortlistRequireAllMandatory,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobVacancy entity, UpdateJobVacancyDto dto, Guid userId)
    {
        // G-5.2: see the note in the create mapping above.
        entity.TestScoreWeight = dto.TestScoreWeight;
        entity.InternalCandidateBoostPoints = dto.InternalCandidateBoostPoints;
        entity.CustomAdvertTitle = dto.CustomAdvertTitle;
        entity.NumberOfPositions = dto.NumberOfPositions;
        entity.HiringManagerId = dto.HiringManagerId;
        entity.RecruiterId = dto.RecruiterId;
        entity.ApplicationDeadline = dto.ApplicationDeadline;
        entity.ShortlistingDeadline = dto.ShortlistingDeadline;
        entity.NumberOfInterviewRounds = dto.NumberOfInterviewRounds;
        entity.TargetStartDate = dto.TargetStartDate;
        entity.IsSalaryVisible = dto.IsSalaryVisible;
        entity.EmploymentType = dto.EmploymentType;
        entity.WorkMode = dto.WorkMode;
        entity.SalaryRangeMin = dto.SalaryRangeMin;
        entity.SalaryRangeMax = dto.SalaryRangeMax;
        entity.SalaryCurrencyCode = dto.SalaryCurrencyCode;
        entity.RequiredMinExperienceYears = dto.RequiredMinExperienceYears;
        entity.KeyBenefitsSummary = dto.KeyBenefitsSummary;
        entity.RequiresWrittenTest = dto.RequiresWrittenTest;
        entity.RequiresPracticalTest = dto.RequiresPracticalTest;
        entity.IsBlindScreeningEnabled = dto.IsBlindScreeningEnabled;
        entity.PublishDate = dto.PublishDate;
        entity.RecruitmentPipelineId = dto.RecruitmentPipelineId;
        entity.AutoShortlistMinScore            = dto.AutoShortlistMinScore;
        entity.AutoShortlistRequireAllMandatory = dto.AutoShortlistRequireAllMandatory;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    /// <summary>
    /// Applies field updates from a <see cref="TransitionJobVacancyDto"/> to the entity.
    /// Does NOT touch VacancyStatus — the caller (TransitionAsync) manages that separately.
    /// </summary>
    public static void UpdateEntity(this JobVacancy entity, TransitionJobVacancyDto dto, Guid userId)
    {
        // G-5.2: see the note in the create mapping above.
        entity.TestScoreWeight = dto.TestScoreWeight;
        entity.InternalCandidateBoostPoints = dto.InternalCandidateBoostPoints;
        entity.CustomAdvertTitle = dto.CustomAdvertTitle;
        entity.NumberOfPositions = dto.NumberOfPositions;
        entity.HiringManagerId = dto.HiringManagerId;
        entity.RecruiterId = dto.RecruiterId;
        entity.ApplicationDeadline = dto.ApplicationDeadline;
        entity.ShortlistingDeadline = dto.ShortlistingDeadline;
        entity.NumberOfInterviewRounds = dto.NumberOfInterviewRounds;
        entity.TargetStartDate = dto.TargetStartDate;
        entity.IsSalaryVisible = dto.IsSalaryVisible;
        entity.EmploymentType = dto.EmploymentType;
        entity.WorkMode = dto.WorkMode;
        entity.SalaryRangeMin = dto.SalaryRangeMin;
        entity.SalaryRangeMax = dto.SalaryRangeMax;
        entity.SalaryCurrencyCode = dto.SalaryCurrencyCode;
        entity.RequiredMinExperienceYears = dto.RequiredMinExperienceYears;
        entity.KeyBenefitsSummary = dto.KeyBenefitsSummary;
        entity.RequiresWrittenTest = dto.RequiresWrittenTest;
        entity.RequiresPracticalTest = dto.RequiresPracticalTest;
        entity.IsBlindScreeningEnabled = dto.IsBlindScreeningEnabled;
        entity.PublishDate = dto.PublishDate;
        entity.RecruitmentPipelineId = dto.RecruitmentPipelineId;
        entity.AutoShortlistMinScore            = dto.AutoShortlistMinScore;
        entity.AutoShortlistRequireAllMandatory = dto.AutoShortlistRequireAllMandatory;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<JobVacancySummaryDto> ToSummaryDtoList(this IEnumerable<JobVacancy> entities)
        => entities.Select(e => e.ToSummaryDto());

    /// <summary>
    /// Maps a <see cref="JobVacancy"/> to the safe public-facing <see cref="PublicVacancyDto"/>.
    /// Sensitive internal fields (recruiter names, application counts, pipeline, score config) are excluded.
    /// Salary is included only when <see cref="JobVacancy.IsSalaryVisible"/> is true.
    /// </summary>
    public static PublicVacancyDto ToPublicDto(this JobVacancy entity)
    {
        return new PublicVacancyDto
        {
            Id                         = entity.Id,
            VacancyNumber              = entity.VacancyNumber,
            JobTitle                   = entity.JobTitle,
            PositionTitle              = entity.Position?.Title ?? string.Empty,
            DepartmentName             = entity.Requisition?.OrganizationUnit?.Name ?? string.Empty,
            LocationName               = entity.Requisition?.Location?.Name ?? string.Empty,
            EmploymentType             = entity.EmploymentType,
            WorkMode                   = entity.WorkMode,
            NumberOfPositions          = entity.NumberOfPositions,
            RequiredMinExperienceYears = entity.RequiredMinExperienceYears,
            KeyBenefitsSummary         = entity.KeyBenefitsSummary,
            TargetStartDate            = entity.TargetStartDate,
            IsSalaryVisible            = entity.IsSalaryVisible,
            SalaryRangeMin             = entity.IsSalaryVisible ? entity.SalaryRangeMin : null,
            SalaryRangeMax             = entity.IsSalaryVisible ? entity.SalaryRangeMax : null,
            SalaryCurrencyCode         = entity.IsSalaryVisible ? entity.SalaryCurrencyCode : null,
            PublishDate                = entity.PublishDate,
            ApplicationDeadline        = entity.ApplicationDeadline,
            RequiresWrittenTest        = entity.RequiresWrittenTest,
            RequiresPracticalTest      = entity.RequiresPracticalTest,
            JobDescription             = entity.Requisition?.JobDescription?.JobSummary,
            // Round 3, lane A: the live adverts, so a posting link can name one. Only what an
            // applicant may come through — published, not removed or expired.
            Postings                   = (entity.JobPostings ?? Array.Empty<JobPosting>())
                .Where(p => !p.IsDeleted && p.Status == JobPostingStatus.Published)
                .OrderBy(p => p.Channel)
                .Select(p => new PublicVacancyPostingDto { Id = p.Id, Channel = p.Channel, Title = p.Title })
                .ToList(),
        };
    }

    public static IEnumerable<PublicVacancyDto> ToPublicDtoList(this IEnumerable<JobVacancy> entities)
        => entities.Select(e => e.ToPublicDto());

    #endregion

    // ========================================================================
    // JOB VACANCY ATTACHMENT
    // ========================================================================

    #region JobVacancyAttachment

    public static JobVacancyAttachmentDto ToDto(this JobVacancyAttachment entity)
    {
        return new JobVacancyAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobVacancyId = entity.JobVacancyId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
        };
    }

    // The ToEntity mapper for vacancy attachments is gone with its DTO: the row is now built in
    // JobVacancyService.AddAttachmentAsync from the scanned document the upload gate returns, so
    // there is no caller payload left to map.

    #endregion

    // ========================================================================
    // JOB VACANCY STATUS HISTORY
    // ========================================================================

    #region JobVacancyStatusHistory

    public static JobVacancyStatusHistoryDto ToDto(this JobVacancyStatusHistory entity)
    {
        return new JobVacancyStatusHistoryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobVacancyId = entity.JobVacancyId,
            FromStatus = entity.FromStatus,
            ToStatus = entity.ToStatus,
            ChangedDate = entity.ChangedDate,
            ChangedById = entity.ChangedById,
            ChangedByName = entity.ChangedBy?.FullName ?? string.Empty,
            Reason = entity.Reason,
            Comments = entity.Comments,
        };
    }

    #endregion

    // ========================================================================
    // VACANCY PIPELINE STAGE ASSIGNMENT
    // ========================================================================

    #region VacancyPipelineStageAssignment

    public static VacancyPipelineStageAssignmentDto ToDto(this VacancyPipelineStageAssignment entity)
    {
        return new VacancyPipelineStageAssignmentDto
        {
            Id                    = entity.Id,
            CreatedAt             = entity.CreatedAt,
            CreatedBy             = entity.CreatedBy ?? string.Empty,
            UpdatedAt             = entity.UpdatedAt,
            UpdatedBy             = entity.UpdatedBy,
            JobVacancyId          = entity.JobVacancyId,
            PipelineStageId       = entity.PipelineStageId,
            StageName             = entity.PipelineStage?.Name ?? string.Empty,
            StageOrder            = entity.PipelineStage?.Order ?? 0,
            StageType             = entity.PipelineStage?.StageType ?? RecruitmentPipelineStageType.Other,
            AssignedToId          = entity.AssignedToId,
            AssignedToName        = entity.AssignedTo?.FullName ?? string.Empty,
            AssignedById          = entity.AssignedById,
            AssignedByName        = entity.AssignedBy?.FullName ?? string.Empty,
            AssignedAt            = entity.AssignedAt,
            DueDate               = entity.DueDate,
            Status                = entity.Status,
            CompletedAt           = entity.CompletedAt,
            CompletedById         = entity.CompletedById,
            CompletedByName       = entity.CompletedBy?.FullName,
            CompletionNotes       = entity.CompletionNotes,
            EscalationEnabled     = entity.EscalationEnabled,
            EscalationDaysAfterDue = entity.EscalationDaysAfterDue,
            EscalateToId          = entity.EscalateToId,
            EscalateToName        = entity.EscalateTo?.FullName,
            EscalatedAt           = entity.EscalatedAt,
            EscalationNotes       = entity.EscalationNotes,
        };
    }

    #endregion

    // ========================================================================
    // JOB POSTING
    // ========================================================================

    #region JobPosting

    public static JobPostingDto ToDto(this JobPosting entity)
    {
        return new JobPostingDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobVacancyId = entity.JobVacancyId,
            VacancyNumber = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            Channel = entity.Channel,
            Title = entity.Title,
            Description = entity.Description,
            PostingUrl = entity.PostingUrl,
            ExternalPostingId = entity.ExternalPostingId,
            PublishDate = entity.PublishDate,
            ActualPublishDate = entity.ActualPublishDate,
            ExpiryDate = entity.ExpiryDate,
            Status = entity.Status,
            IsActive = entity.IsActive,
            ApplicationCount = entity.ApplicationCount,
            PostedById = entity.PostedById,
            PostedByName = entity.PostedBy?.FullName,
            AdvertHeadline = entity.AdvertHeadline,
            AdvertBody = entity.AdvertBody,
            HowToApply = entity.HowToApply,
            ClosingDateText = entity.ClosingDateText,
            ShowSalaryInAdvert = entity.ShowSalaryInAdvert,
            ContactDetails = entity.ContactDetails,
        };
    }

    public static JobPostingSummaryDto ToSummaryDto(this JobPosting entity)
    {
        return new JobPostingSummaryDto
        {
            Id = entity.Id,
            JobVacancyId = entity.JobVacancyId,
            VacancyNumber = entity.JobVacancy?.VacancyNumber,
            Channel = entity.Channel,
            Title = entity.Title,
            Status = entity.Status,
            IsActive = entity.IsActive,
            PublishDate = entity.PublishDate,
            ActualPublishDate = entity.ActualPublishDate,
            ExpiryDate = entity.ExpiryDate,
            ApplicationCount = entity.ApplicationCount,
            PostingUrl = entity.PostingUrl,
        };
    }

    public static JobPosting ToEntity(this CreateJobPostingDto dto, Guid tenantId, Guid userId)
    {
        return new JobPosting
        {
            TenantId = tenantId,
            JobVacancyId = dto.JobVacancyId,
            Channel = dto.Channel,
            Title = dto.Title,
            Description = dto.Description,
            PostingUrl = dto.PostingUrl,
            ExternalPostingId = dto.ExternalPostingId,
            PublishDate = dto.PublishDate,
            ExpiryDate = dto.ExpiryDate,
            Status = JobPostingStatus.Draft,
            IsActive = false,
            PostedById = userId,
            AdvertHeadline = dto.AdvertHeadline,
            AdvertBody = dto.AdvertBody,
            HowToApply = dto.HowToApply,
            ClosingDateText = dto.ClosingDateText,
            ShowSalaryInAdvert = dto.ShowSalaryInAdvert,
            ContactDetails = dto.ContactDetails,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobPosting entity, UpdateJobPostingDto dto, Guid userId)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.PostingUrl = dto.PostingUrl;
        entity.ExternalPostingId = dto.ExternalPostingId;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Status = dto.Status;
        entity.IsActive = dto.IsActive;
        entity.AdvertHeadline = dto.AdvertHeadline;
        entity.AdvertBody = dto.AdvertBody;
        entity.HowToApply = dto.HowToApply;
        entity.ClosingDateText = dto.ClosingDateText;
        entity.ShowSalaryInAdvert = dto.ShowSalaryInAdvert;
        entity.ContactDetails = dto.ContactDetails;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<JobPostingSummaryDto> ToSummaryDtoList(this IEnumerable<JobPosting> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static JobPostingAttachmentDto ToDto(this JobPostingAttachment entity)
    {
        return new JobPostingAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobPostingId = entity.JobPostingId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
        };
    }

    #endregion

    // ========================================================================
    // RECRUITMENT PIPELINE
    // ========================================================================

    #region RecruitmentPipeline

    public static RecruitmentPipelineDto ToDto(this RecruitmentPipeline entity)
    {
        return new RecruitmentPipelineDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            DefaultTimeToCompleteDays = entity.DefaultTimeToCompleteDays,
            Stages = entity.Stages.Select(s => s.ToDto()).ToList(),
        };
    }

    public static RecruitmentPipelineSummaryDto ToSummaryDto(this RecruitmentPipeline entity)
    {
        return new RecruitmentPipelineSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            StageCount = entity.Stages?.Count ?? 0,
            DefaultTimeToCompleteDays = entity.DefaultTimeToCompleteDays,
        };
    }

    public static RecruitmentPipeline ToEntity(this CreateRecruitmentPipelineDto dto, Guid tenantId, Guid userId)
    {
        return new RecruitmentPipeline
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            IsDefault = dto.IsDefault,
            IsActive = dto.IsActive,
            DefaultTimeToCompleteDays = dto.DefaultTimeToCompleteDays,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this RecruitmentPipeline entity, UpdateRecruitmentPipelineDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.IsDefault = dto.IsDefault;
        entity.IsActive = dto.IsActive;
        entity.DefaultTimeToCompleteDays = dto.DefaultTimeToCompleteDays;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<RecruitmentPipelineSummaryDto> ToSummaryDtoList(this IEnumerable<RecruitmentPipeline> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // RECRUITMENT PIPELINE STAGE
    // ========================================================================

    #region RecruitmentPipelineStage

    public static RecruitmentPipelineStageDto ToDto(this RecruitmentPipelineStage entity)
    {
        return new RecruitmentPipelineStageDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RecruitmentPipelineId = entity.RecruitmentPipelineId,
            PipelineName = entity.Pipeline?.Name ?? string.Empty,
            Name = entity.Name,
            Description = entity.Description,
            Order = entity.Order,
            StageType = entity.StageType,
            IsActive = entity.IsActive,
            IsFinalStage = entity.IsFinalStage,
            IsRequired = entity.IsRequired,
            DefaultTimeToCompleteDays = entity.DefaultTimeToCompleteDays,
            CanSkip = entity.CanSkip,
            CanRepeat = entity.CanRepeat,
            MaxAttempts = entity.MaxAttempts,
            Instructions = entity.Instructions,
        };
    }

    public static RecruitmentPipelineStage ToEntity(this CreateRecruitmentPipelineStageDto dto, Guid tenantId, Guid userId)
    {
        return new RecruitmentPipelineStage
        {
            TenantId = tenantId,
            RecruitmentPipelineId = dto.RecruitmentPipelineId,
            Name = dto.Name,
            Description = dto.Description,
            Order = dto.Order,
            StageType = dto.StageType,
            IsFinalStage = dto.IsFinalStage,
            IsActive = dto.IsActive,
            // Round 3, lane G (D-13): one switch — a stage is required exactly when it cannot be skipped.
            IsRequired = !dto.CanSkip,
            DefaultTimeToCompleteDays = dto.DefaultTimeToCompleteDays,
            CanSkip = dto.CanSkip,
            CanRepeat = dto.CanRepeat,
            MaxAttempts = dto.MaxAttempts,
            Instructions = dto.Instructions,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this RecruitmentPipelineStage entity, UpdateRecruitmentPipelineStageDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Order = dto.Order;
        entity.StageType = dto.StageType;
        entity.IsActive = dto.IsActive;
        entity.IsFinalStage = dto.IsFinalStage;
        entity.IsRequired = !dto.CanSkip; // D-13: derived, never typed
        entity.DefaultTimeToCompleteDays = dto.DefaultTimeToCompleteDays;
        entity.CanSkip = dto.CanSkip;
        entity.CanRepeat = dto.CanRepeat;
        entity.MaxAttempts = dto.MaxAttempts;
        entity.Instructions = dto.Instructions;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB SHORTLISTING CRITERIA
    // ========================================================================

    #region JobShortlistingCriteria

    public static JobShortlistingCriteriaDto ToDto(this JobShortlistingCriteria entity)
    {
        return new JobShortlistingCriteriaDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobVacancyId = entity.JobVacancyId,
            CriteriaName = entity.CriteriaName,
            Description = entity.Description,
            Type = entity.Type,
            RequiredValue = entity.RequiredValue,
            MinValue = entity.MinValue,
            MaxValue = entity.MaxValue,
            IsMandatory = entity.IsMandatory,
            MatchMode = entity.MatchMode,
            MatchStrategy = entity.MatchStrategy,
            RequiredSkillId = entity.RequiredSkillId,
            RequiredQualificationId = entity.RequiredQualificationId,
            Weight = entity.Weight,
            ComparisonOperator = entity.ComparisonOperator,
            // Filtered here as well as on the read: a replace-set save retires rows in the SAME
            // context it then maps, and fixup re-attaches the just-retired children (the candidate
            // profile lesson).
            Values = entity.Values
                .Where(v => !v.IsDeleted)
                .OrderBy(v => v.SortOrder)
                .Select(v => new JobShortlistingCriteriaValueDto
                {
                    Id = v.Id, Kind = v.Kind, ReferenceId = v.ReferenceId, Label = v.Label, SortOrder = v.SortOrder,
                })
                .ToList(),
        };
    }

    public static JobShortlistingCriteria ToEntity(this CreateJobShortlistingCriteriaDto dto, Guid tenantId, Guid userId)
    {
        return new JobShortlistingCriteria
        {
            TenantId = tenantId,
            JobVacancyId = dto.JobVacancyId,
            CriteriaName = dto.CriteriaName,
            Description = dto.Description,
            Type = dto.Type,
            RequiredValue = dto.RequiredValue,
            MinValue = dto.MinValue,
            MaxValue = dto.MaxValue,
            IsMandatory = dto.IsMandatory,
            MatchMode = dto.MatchMode,
            MatchStrategy = dto.MatchStrategy,
            RequiredSkillId = dto.RequiredSkillId,
            RequiredQualificationId = dto.RequiredQualificationId,
            Weight = dto.Weight,
            // ⚠ This assignment was missing. The create DTO carries ComparisonOperator, ToDto
            // returns it and UpdateEntity assigns it — only the create path dropped it, so a
            // criterion could be created with an operator and come back with none, and the
            // numeric arm of the scoring switch fell to its `?? Between` default. Found by
            // dev-harness/hr-recruitment/probe-lane5-criteria.mjs (lane 5b), which sent
            // "Equals" on create and read null back.
            ComparisonOperator = dto.ComparisonOperator,
        };
    }

    public static void UpdateEntity(this JobShortlistingCriteria entity, UpdateJobShortlistingCriteriaDto dto, Guid userId)
    {
        entity.CriteriaName = dto.CriteriaName;
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.RequiredValue = dto.RequiredValue;
        entity.MinValue = dto.MinValue;
        entity.MaxValue = dto.MaxValue;
        entity.IsMandatory = dto.IsMandatory;
        entity.MatchMode = dto.MatchMode;
        entity.MatchStrategy = dto.MatchStrategy;
        entity.RequiredSkillId = dto.RequiredSkillId;
        entity.RequiredQualificationId = dto.RequiredQualificationId;
        entity.Weight = dto.Weight;
        entity.ComparisonOperator = dto.ComparisonOperator;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB CANDIDATE
    // ========================================================================

    #region JobCandidate

    public static JobCandidateDto ToDto(this JobCandidate entity)
    {
        return new JobCandidateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CandidateNumber = entity.CandidateNumber,
            FirstName = entity.FirstName,
            MiddleName = entity.MiddleName,
            LastName = entity.LastName,
            DateOfBirth = entity.DateOfBirth,
            Gender = entity.Gender,
            Email = entity.Email,
            Phone = entity.Phone,
            AlternatePhone = entity.AlternatePhone,
            PostalAddress = entity.PostalAddress,
            DigitalAddress = entity.DigitalAddress,
            City = entity.City,
            Region = entity.Region,
            GeoAreaId = entity.GeoAreaId,
            Nationality = entity.Nationality,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name ?? string.Empty,
            IsInTalentPool = entity.IsInTalentPool,
            TalentPoolAddedDate = entity.TalentPoolAddedDate,
            LinkedInProfile = entity.LinkedInProfile,
            PortfolioUrl = entity.PortfolioUrl,
            GitHubUrl = entity.GitHubUrl,
            Headline = entity.Headline,
            ProfessionalSummary = entity.ProfessionalSummary,
            CurrentJobTitle = entity.CurrentJobTitle,
            CurrentEmployer = entity.CurrentEmployer,
            TotalYearsExperience = entity.TotalYearsExperience,
            NoticePeriodDays = entity.NoticePeriodDays,
            AvailableFrom = entity.AvailableFrom,
            PreferredWorkArrangement = entity.PreferredWorkArrangement,
            ExpectedSalaryMin = entity.ExpectedSalaryMin,
            ExpectedSalaryMax = entity.ExpectedSalaryMax,
            ExpectedSalaryCurrency = entity.ExpectedSalaryCurrency,
            WorkAuthorizationStatus = entity.WorkAuthorizationStatus,
            NationalIdTypeId = entity.NationalIdTypeId,
            NationalIdTypeName = entity.NationalIdTypeRef?.Name,
            NationalIdNumber = entity.NationalIdNumber,
            NationalIdExpiryDate = entity.NationalIdExpiryDate,
            CvFilePath = entity.CvFilePath,
            ProfilePhotoUrl = entity.ProfilePhotoUrl,
            HasPhoto = entity.HasPhotoOnFile(),
            ApplicationCount = entity.Applications?.Count ?? 0,
        };
    }

    public static JobCandidateSummaryDto ToSummaryDto(this JobCandidate entity)
    {
        return new JobCandidateSummaryDto
        {
            Id = entity.Id,
            CandidateNumber = entity.CandidateNumber,
            FullName = entity.FullName,
            Email = entity.Email,
            Phone = entity.Phone,
            City = entity.City,
            Region = entity.Region,
            CountryName = entity.Country?.Name ?? string.Empty,
            IsInTalentPool = entity.IsInTalentPool,
            HasPhoto = entity.HasPhotoOnFile(),
            ApplicationCount = entity.Applications?.Count ?? 0,
        };
    }

    /// <summary>
    /// Round 3, lane C2: one answer to "is there a photograph" for every candidate DTO. The gated
    /// upload record is the live column; the legacy public URL still counts for rows written before
    /// photos went private (the download endpoint serves neither, but the flag must not lie about
    /// what the record holds).
    /// </summary>
    public static bool HasPhotoOnFile(this JobCandidate entity)
        => entity.ProfilePhotoFileUploadRecordId != null || !string.IsNullOrWhiteSpace(entity.ProfilePhotoUrl);

    public static JobCandidateDetailDto ToDetailDto(this JobCandidate entity)
    {
        var dto = new JobCandidateDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CandidateNumber = entity.CandidateNumber,
            FirstName = entity.FirstName,
            MiddleName = entity.MiddleName,
            LastName = entity.LastName,
            DateOfBirth = entity.DateOfBirth,
            Gender = entity.Gender,
            Email = entity.Email,
            Phone = entity.Phone,
            AlternatePhone = entity.AlternatePhone,
            PostalAddress = entity.PostalAddress,
            DigitalAddress = entity.DigitalAddress,
            City = entity.City,
            Region = entity.Region,
            GeoAreaId = entity.GeoAreaId,
            Nationality = entity.Nationality,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name ?? string.Empty,
            IsInTalentPool = entity.IsInTalentPool,
            TalentPoolAddedDate = entity.TalentPoolAddedDate,
            LinkedInProfile = entity.LinkedInProfile,
            PortfolioUrl = entity.PortfolioUrl,
            GitHubUrl = entity.GitHubUrl,
            Headline = entity.Headline,
            ProfessionalSummary = entity.ProfessionalSummary,
            CurrentJobTitle = entity.CurrentJobTitle,
            CurrentEmployer = entity.CurrentEmployer,
            TotalYearsExperience = entity.TotalYearsExperience,
            NoticePeriodDays = entity.NoticePeriodDays,
            AvailableFrom = entity.AvailableFrom,
            PreferredWorkArrangement = entity.PreferredWorkArrangement,
            ExpectedSalaryMin = entity.ExpectedSalaryMin,
            ExpectedSalaryMax = entity.ExpectedSalaryMax,
            ExpectedSalaryCurrency = entity.ExpectedSalaryCurrency,
            WorkAuthorizationStatus = entity.WorkAuthorizationStatus,
            NationalIdTypeId = entity.NationalIdTypeId,
            NationalIdTypeName = entity.NationalIdTypeRef?.Name,
            NationalIdNumber = entity.NationalIdNumber,
            NationalIdExpiryDate = entity.NationalIdExpiryDate,
            CvFilePath = entity.CvFilePath,
            ProfilePhotoUrl = entity.ProfilePhotoUrl,
            HasPhoto = entity.HasPhotoOnFile(),
            ApplicationCount = entity.Applications?.Count ?? 0,
            Qualifications = entity.Qualifications.Select(q => q.ToDto()).ToList(),
            WorkHistories = entity.WorkHistories.Select(w => w.ToDto()).ToList(),
            Referees = entity.Referees.Select(r => r.ToDto()).ToList(),
            Skills = entity.Skills.Select(s => s.ToDto()).ToList(),
            Languages = entity.Languages.Select(l => l.ToDto()).ToList(),
            Interests = entity.Interests.Select(i => i.ToDto()).ToList(),
            Documents = entity.Documents.Select(d => d.ToDto()).ToList(),
            Notes = entity.Notes.Select(n => n.ToDto()).ToList(),
            Applications = entity.Applications?.Select(a => a.ToSummaryDto()).ToList() ?? new(),
        };
        return dto;
    }

    public static JobCandidate ToEntity(this CreateJobCandidateDto dto, Guid tenantId, Guid userId)
    {
        return new JobCandidate
        {
            TenantId = tenantId,
            FirstName = dto.FirstName,
            MiddleName = dto.MiddleName,
            LastName = dto.LastName,
            DateOfBirth = dto.DateOfBirth,
            Gender = dto.Gender,
            Email = dto.Email,
            Phone = dto.Phone,
            AlternatePhone = dto.AlternatePhone,
            PostalAddress = dto.PostalAddress,
            DigitalAddress = dto.DigitalAddress,
            // Round 4, lane A: City stopped being [Required] when the geography cascade arrived, so
            // a payload that supplies an area legitimately carries no city. The service overwrites
            // this from the tree straight afterwards; the empty string is what it writes over.
            City = dto.City ?? string.Empty,
            GeoAreaId = dto.GeoAreaId,
            // G-7.3: settable since 2026-09-15. Before that the demo seeder was its only writer.
            Nationality = string.IsNullOrWhiteSpace(dto.Nationality) ? null : dto.Nationality.Trim(),
            // Guid.Empty is read as "no country", not refused: a client written against the old
            // [Required] Guid contract sent it, and that used to be an FK 547 / 500.
            CountryId = dto.CountryId == Guid.Empty ? null : dto.CountryId,
            LinkedInProfile = dto.LinkedInProfile,
            PortfolioUrl = dto.PortfolioUrl,
            GitHubUrl = dto.GitHubUrl,
            NationalIdTypeId = dto.NationalIdTypeId,
            NationalIdNumber = string.IsNullOrWhiteSpace(dto.NationalIdNumber) ? null : dto.NationalIdNumber.Trim(),
            NationalIdExpiryDate = dto.NationalIdExpiryDate,
            IsInTalentPool = dto.IsInTalentPool,
            TalentPoolAddedDate = dto.IsInTalentPool ? DateTime.UtcNow : null,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobCandidate entity, UpdateJobCandidateDto dto, Guid userId)
    {
        entity.FirstName = dto.FirstName;
        entity.MiddleName = dto.MiddleName;
        entity.LastName = dto.LastName;
        entity.DateOfBirth = dto.DateOfBirth;
        entity.Gender = dto.Gender;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.AlternatePhone = dto.AlternatePhone;
        entity.PostalAddress = dto.PostalAddress;
        entity.DigitalAddress = dto.DigitalAddress;
        // Round 4, lane A — see the create mapper for why City may legitimately arrive empty.
        // ⚠ A null GeoAreaId here means "no area", not "leave it": this DTO replaces the address
        // wholesale, which is why it needs no ClearGeoArea flag of the kind the employee's
        // patch-style update carries.
        entity.City = dto.City ?? string.Empty;
        entity.GeoAreaId = dto.GeoAreaId;
        // G-7.3: settable since 2026-09-15. Before that the demo seeder was its only writer.
        entity.Nationality = string.IsNullOrWhiteSpace(dto.Nationality) ? null : dto.Nationality.Trim();
        entity.CountryId = dto.CountryId == Guid.Empty ? null : dto.CountryId;
        entity.NationalIdTypeId = dto.NationalIdTypeId;
        entity.NationalIdNumber = string.IsNullOrWhiteSpace(dto.NationalIdNumber) ? null : dto.NationalIdNumber.Trim();
        entity.NationalIdExpiryDate = dto.NationalIdExpiryDate;
        entity.LinkedInProfile = dto.LinkedInProfile;
        entity.PortfolioUrl = dto.PortfolioUrl;
        entity.GitHubUrl = dto.GitHubUrl;
        if (dto.IsInTalentPool && !entity.IsInTalentPool)
            entity.TalentPoolAddedDate = DateTime.UtcNow;
        entity.IsInTalentPool = dto.IsInTalentPool;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<JobCandidateSummaryDto> ToSummaryDtoList(this IEnumerable<JobCandidate> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // JOB CANDIDATE QUALIFICATION
    // ========================================================================

    #region JobCandidateQualification

    public static JobCandidateQualificationDto ToDto(this JobCandidateQualification entity)
    {
        return new JobCandidateQualificationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            QualificationType = entity.QualificationType,
            QualificationId = entity.QualificationId,
            QualificationName = entity.Qualification?.Name ?? entity.QualificationFreeText ?? string.Empty,
            Institution = entity.Institution,
            DateAwarded = entity.DateAwarded,
            Grade = entity.Grade,
        };
    }

    public static JobCandidateQualification ToEntity(this CreateJobCandidateQualificationDto dto, Guid tenantId, Guid userId)
    {
        return new JobCandidateQualification
        {
            TenantId = tenantId,
            JobCandidateId = dto.JobCandidateId,
            QualificationType = dto.QualificationType,
            QualificationId = dto.QualificationId,
            Institution = dto.Institution,
            DateAwarded = dto.DateAwarded,
            Grade = dto.Grade,
            QualificationFreeText = dto.QualificationFreeText,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobCandidateQualification entity, UpdateJobCandidateQualificationDto dto, Guid userId)
    {
        entity.QualificationType = dto.QualificationType;
        entity.QualificationId = dto.QualificationId;
        entity.Institution = dto.Institution;
        entity.DateAwarded = dto.DateAwarded;
        entity.Grade = dto.Grade;
        entity.QualificationFreeText = dto.QualificationFreeText;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB CANDIDATE WORK HISTORY
    // ========================================================================

    #region JobCandidateWorkHistory

    public static JobCandidateWorkHistoryDto ToDto(this JobCandidateWorkHistory entity)
    {
        return new JobCandidateWorkHistoryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            InstitutionName = entity.InstitutionName,
            PositionHeld = entity.PositionHeld,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Responsibilities = entity.Responsibilities,
            ReasonForLeaving = entity.ReasonForLeaving,
        };
    }

    public static JobCandidateWorkHistory ToEntity(this CreateJobCandidateWorkHistoryDto dto, Guid tenantId, Guid userId)
    {
        return new JobCandidateWorkHistory
        {
            TenantId = tenantId,
            JobCandidateId = dto.JobCandidateId,
            InstitutionName = dto.InstitutionName,
            PositionHeld = dto.PositionHeld,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Responsibilities = dto.Responsibilities,
            ReasonForLeaving = dto.ReasonForLeaving,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobCandidateWorkHistory entity, UpdateJobCandidateWorkHistoryDto dto, Guid userId)
    {
        entity.InstitutionName = dto.InstitutionName;
        entity.PositionHeld = dto.PositionHeld;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.Responsibilities = dto.Responsibilities;
        entity.ReasonForLeaving = dto.ReasonForLeaving;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB CANDIDATE REFEREE
    // ========================================================================

    #region JobCandidateReferee

    public static JobCandidateRefereeDto ToDto(this JobCandidateReferee entity)
    {
        return new JobCandidateRefereeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            FullName = entity.FullName,
            Position = entity.Position,
            Organization = entity.Organization,
            Email = entity.Email,
            Phone = entity.Phone,
            Relationship = entity.Relationship,
            RelationshipTypeId = entity.RelationshipTypeId,
            YearsKnown = entity.YearsKnown,
        };
    }

    public static JobCandidateReferee ToEntity(this CreateJobCandidateRefereeDto dto, Guid tenantId, Guid userId)
    {
        return new JobCandidateReferee
        {
            TenantId = tenantId,
            JobCandidateId = dto.JobCandidateId,
            FullName = dto.FullName,
            Position = dto.Position,
            Organization = dto.Organization,
            Email = dto.Email,
            Phone = dto.Phone,
            Relationship = dto.Relationship,
            RelationshipTypeId = dto.RelationshipTypeId,
            YearsKnown = dto.YearsKnown,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobCandidateReferee entity, UpdateJobCandidateRefereeDto dto, Guid userId)
    {
        entity.FullName = dto.FullName;
        entity.Position = dto.Position;
        entity.Organization = dto.Organization;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.Relationship = dto.Relationship;
        // Full replace, like every other field on this DTO: null clears the catalogue link.
        entity.RelationshipTypeId = dto.RelationshipTypeId;
        entity.YearsKnown = dto.YearsKnown;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB CANDIDATE SKILL
    // ========================================================================

    #region JobCandidateSkill

    public static JobCandidateSkillDto ToDto(this JobCandidateSkill entity)
    {
        return new JobCandidateSkillDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            SkillId = entity.SkillId,
            SkillCatalogueName = entity.Skill?.Name,
            SkillName = entity.SkillName,
            Proficiency = entity.Proficiency,
            YearsOfExperience = entity.YearsOfExperience,
            IsCertified = entity.IsCertified,
            CertificationName = entity.CertificationName,
            CertificationNumber = entity.CertificationNumber,
            CertifyingBody = entity.CertifyingBody,
            CertificationExpiryDate = entity.CertificationExpiryDate,
        };
    }

    public static JobCandidateSkill ToEntity(this CreateJobCandidateSkillDto dto, Guid tenantId, Guid userId)
    {
        return new JobCandidateSkill
        {
            TenantId = tenantId,
            JobCandidateId = dto.JobCandidateId,
            SkillId = dto.SkillId,
            SkillName = dto.SkillName,
            Proficiency = dto.Proficiency,
            YearsOfExperience = dto.YearsOfExperience,
            IsCertified = dto.IsCertified,
            CertificationName = dto.IsCertified ? dto.CertificationName : null,
            CertificationNumber = dto.IsCertified ? dto.CertificationNumber : null,
            CertifyingBody = dto.IsCertified ? dto.CertifyingBody : null,
            CertificationExpiryDate = dto.IsCertified ? dto.CertificationExpiryDate : null,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobCandidateSkill entity, UpdateJobCandidateSkillDto dto, Guid userId)
    {
        entity.SkillId = dto.SkillId;
        entity.SkillName = dto.SkillName;
        entity.Proficiency = dto.Proficiency;
        entity.YearsOfExperience = dto.YearsOfExperience;
        entity.IsCertified = dto.IsCertified;
        // An unticked skill carries no certificate: the four fields are cleared together.
        entity.CertificationName = dto.IsCertified ? dto.CertificationName : null;
        entity.CertificationNumber = dto.IsCertified ? dto.CertificationNumber : null;
        entity.CertifyingBody = dto.IsCertified ? dto.CertifyingBody : null;
        entity.CertificationExpiryDate = dto.IsCertified ? dto.CertificationExpiryDate : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB CANDIDATE INTEREST
    // ========================================================================

    #region JobCandidateInterest

    public static JobCandidateInterestDto ToDto(this JobCandidateInterest entity)
    {
        return new JobCandidateInterestDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            Detail = entity.Detail,
        };
    }

    public static JobCandidateInterest ToEntity(this CreateJobCandidateInterestDto dto, Guid tenantId, Guid userId)
    {
        return new JobCandidateInterest
        {
            TenantId = tenantId,
            JobCandidateId = dto.JobCandidateId,
            Detail = dto.Detail,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobCandidateInterest entity, UpdateJobCandidateInterestDto dto, Guid userId)
    {
        entity.Detail = dto.Detail;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB CANDIDATE DOCUMENT
    // ========================================================================

    #region JobCandidateDocument

    /// <summary>Round 3, lane C1: the catalogue link and code ride beside the mirrored name.</summary>
    public static JobCandidateLanguageDto ToDto(this JobCandidateLanguage l)
    {
        return new JobCandidateLanguageDto
        {
            Id             = l.Id,
            TenantId       = l.TenantId,
            CreatedAt      = l.CreatedAt,
            CreatedBy      = l.CreatedBy ?? string.Empty,
            UpdatedAt      = l.UpdatedAt,
            UpdatedBy      = l.UpdatedBy,
            JobCandidateId = l.JobCandidateId,
            LanguageId     = l.LanguageId,
            LanguageCode   = l.Language?.Code,
            LanguageName   = l.LanguageName,
            Proficiency    = l.Proficiency,
        };
    }

    public static JobCandidateDocumentDto ToDto(this JobCandidateDocument entity)
    {
        return new JobCandidateDocumentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            DocumentType = entity.DocumentType,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            UploadDate = entity.UploadDate,
            Description = entity.Description,
        };
    }

    // No ToEntity for candidate documents: the row is written by JobCandidateService.AddDocumentAsync
    // from what the controlled-upload gate returns, not from a payload. The Create DTO this mapped is
    // deleted — it carried a caller-supplied FilePath.

    #endregion

    // ========================================================================
    // JOB CANDIDATE NOTE
    // ========================================================================

    #region JobCandidateNote

    public static JobCandidateNoteDto ToDto(this JobCandidateNote entity)
    {
        return new JobCandidateNoteDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            NoteText = entity.NoteText,
            IsPrivate = entity.IsPrivate,
        };
    }

    public static JobCandidateNote ToEntity(this CreateJobCandidateNoteDto dto, Guid tenantId, Guid userId)
    {
        return new JobCandidateNote
        {
            TenantId = tenantId,
            JobCandidateId = dto.JobCandidateId,
            NoteText = dto.NoteText,
            IsPrivate = dto.IsPrivate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobCandidateNote entity, UpdateJobCandidateNoteDto dto, Guid userId)
    {
        entity.NoteText = dto.NoteText;
        entity.IsPrivate = dto.IsPrivate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TALENT POOL
    // ========================================================================

    #region Talent Pool

    // Every JobCandidateDto field must be populated here, not just the ones the first screen
    // happened to bind — this DTO *is* the per-candidate read the pool panels edit from. The
    // first cut mapped 21 of ~40 and silently served no CV link, city, country, salary
    // expectations or work authorisation (the D-09 per-parent-read shape).
    public static TalentPoolCandidateDto ToTalentPoolDto(this JobCandidate entity)
    {
        var now = DateTime.UtcNow;
        var daysInPool = entity.TalentPoolAddedDate.HasValue
            ? (int)(now - entity.TalentPoolAddedDate.Value).TotalDays
            : 0;

        return new TalentPoolCandidateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CandidateNumber = entity.CandidateNumber,
            FirstName = entity.FirstName,
            MiddleName = entity.MiddleName,
            LastName = entity.LastName,
            DateOfBirth = entity.DateOfBirth,
            Gender = entity.Gender,
            Email = entity.Email,
            Phone = entity.Phone,
            AlternatePhone = entity.AlternatePhone,
            PostalAddress = entity.PostalAddress,
            DigitalAddress = entity.DigitalAddress,
            City = entity.City,
            Region = entity.Region,
            GeoAreaId = entity.GeoAreaId,
            Nationality = entity.Nationality,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name ?? string.Empty,
            LinkedInProfile = entity.LinkedInProfile,
            PortfolioUrl = entity.PortfolioUrl,
            GitHubUrl = entity.GitHubUrl,
            Headline = entity.Headline,
            ProfessionalSummary = entity.ProfessionalSummary,
            CurrentJobTitle = entity.CurrentJobTitle,
            CurrentEmployer = entity.CurrentEmployer,
            TotalYearsExperience = entity.TotalYearsExperience,
            NoticePeriodDays = entity.NoticePeriodDays,
            AvailableFrom = entity.AvailableFrom,
            PreferredWorkArrangement = entity.PreferredWorkArrangement,
            ExpectedSalaryMin = entity.ExpectedSalaryMin,
            ExpectedSalaryMax = entity.ExpectedSalaryMax,
            ExpectedSalaryCurrency = entity.ExpectedSalaryCurrency,
            WorkAuthorizationStatus = entity.WorkAuthorizationStatus,
            CvFilePath = entity.CvFilePath,
            ProfilePhotoUrl = entity.ProfilePhotoUrl,
            HasPhoto = entity.HasPhotoOnFile(),
            ApplicationCount = entity.Applications?.Count ?? 0,
            IsInTalentPool = entity.IsInTalentPool,
            TalentPoolAddedDate = entity.TalentPoolAddedDate,
            TalentPoolSource = entity.TalentPoolSource,
            TalentPoolStatus = entity.TalentPoolStatus,
            TalentPoolNotes = entity.TalentPoolNotes,
            TalentPoolReviewDate = entity.TalentPoolReviewDate,
            TalentPoolRemovalReason = entity.TalentPoolRemovalReason,
            LastEngagedDate = entity.LastEngagedDate,
            DaysInPool = daysInPool,
            EngagementCount = entity.EngagementEvents?.Count(e => !e.IsDeleted) ?? 0,
            // ⚠ The segment's own deletion is checked too: a soft-deleted segment left its
            // membership rows live, so a retired grouping kept showing on the candidate (lane V).
            Segments = entity.SegmentMemberships?
                .Where(m => !m.IsDeleted && m.Segment?.IsDeleted != true)
                .Select(m => m.ToDto())
                .ToList() ?? new()
        };
    }

    public static CandidateTalentSegmentDto ToDto(this CandidateTalentSegment entity)
    {
        return new CandidateTalentSegmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            Color = entity.Color,
            IsActive = entity.IsActive,
            // ⚠ 0 unless the caller included Memberships — the segment repository's reads do
            // since lane V; a bare GetByIdAsync still would not.
            MemberCount = entity.Memberships?.Count(m => !m.IsDeleted) ?? 0,
            OwnerEmployeeId = entity.OwnerEmployeeId,
            OwnerEmployeeName = entity.OwnerEmployee?.FullName,
            Purpose = entity.Purpose,
            TargetPositionId = entity.TargetPositionId,
            TargetPositionTitle = entity.TargetPosition?.Title,
            JobFamilyId = entity.JobFamilyId,
            JobFamilyName = entity.JobFamily?.Name
        };
    }

    public static CandidateSegmentMembershipDto ToDto(this CandidateSegmentMembership entity)
    {
        return new CandidateSegmentMembershipDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            SegmentId = entity.SegmentId,
            SegmentName = entity.Segment?.Name ?? string.Empty,
            SegmentColor = entity.Segment?.Color,
            AddedDate = entity.AddedDate,
            Notes = entity.Notes
        };
    }

    // RecordedByEmployeeId has no Employee navigation, so the names arrive as parameters the
    // service resolves — a mapper defaulting them to blank is how the timeline shipped unable
    // to say who logged a contact.
    public static CandidateEngagementEventDto ToEngagementEventDto(
        this CandidateEngagementEvent entity, string? candidateName = null, string? recordedByName = null)
    {
        return new CandidateEngagementEventDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobCandidateId = entity.JobCandidateId,
            CandidateName = candidateName ?? entity.JobCandidate?.FullName ?? string.Empty,
            EventType = entity.EventType,
            EventDate = entity.EventDate,
            Subject = entity.Subject,
            Notes = entity.Notes,
            RecordedByName = recordedByName,
            IsInternal = entity.IsInternal
        };
    }

    public static CandidateTalentSegment ToEntity(this CreateCandidateTalentSegmentDto dto, Guid tenantId, Guid userId)
    {
        return new CandidateTalentSegment
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            IsActive = true,
            OwnerEmployeeId = dto.OwnerEmployeeId,
            Purpose = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim(),
            TargetPositionId = dto.TargetPositionId,
            JobFamilyId = dto.JobFamilyId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this CandidateTalentSegment entity, UpdateCandidateTalentSegmentDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Color = dto.Color;
        entity.IsActive = dto.IsActive;
        // Lane V: the four are replaced, not merged — a cleared owner on the form clears the column.
        entity.OwnerEmployeeId = dto.OwnerEmployeeId;
        entity.Purpose = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim();
        entity.TargetPositionId = dto.TargetPositionId;
        entity.JobFamilyId = dto.JobFamilyId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static CandidateEngagementEvent ToEntity(this CreateCandidateEngagementEventDto dto, Guid tenantId, Guid employeeId)
    {
        return new CandidateEngagementEvent
        {
            TenantId = tenantId,
            JobCandidateId = dto.JobCandidateId,
            EventType = dto.EventType,
            EventDate = dto.EventDate,
            Subject = dto.Subject,
            Notes = dto.Notes,
            RecordedByEmployeeId = employeeId,
            IsInternal = dto.IsInternal,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = employeeId.ToString()
        };
    }

    #endregion

    // ========================================================================
    // JOB APPLICATION
    // ========================================================================

    #region JobApplication

    public static JobApplicationDto ToDto(this JobApplication entity)
    {
        var currentStage = entity.StageHistories
            .Where(h => h.IsCurrent && !h.IsDeleted)
            .OrderByDescending(h => h.EnteredAt)
            .FirstOrDefault();

        return new JobApplicationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ApplicationNumber = entity.ApplicationNumber,
            JobVacancyId = entity.JobVacancyId,
            VacancyNumber = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            JobTitle = entity.JobVacancy?.JobTitle ?? string.Empty,
            JobCandidateId = entity.JobCandidateId,
            CandidateNumber = entity.JobCandidate?.CandidateNumber ?? string.Empty,
            CandidateName = entity.JobCandidate?.FullName ?? string.Empty,
            CandidateEmail = entity.JobCandidate?.Email ?? string.Empty,
            CandidatePhone = entity.JobCandidate?.Phone ?? string.Empty,
            ApplicationDate = entity.ApplicationDate,
            Status = entity.Status,
            Source = entity.Source,
            JobPostingId = entity.JobPostingId,
            JobPostingChannel = entity.JobPosting?.Channel.ToString(),
            JobPostingTitle = entity.JobPosting?.Title,
            YearsOfExperience = entity.YearsOfExperience,
            AvailableFrom = entity.AvailableFrom,
            CoverLetter = entity.CoverLetter,
            AutoScore = entity.AutoScore,
            AutoScoreBreakdown = entity.AutoScoreBreakdown,
            ScoredAt = entity.ScoredAt,
            ScoreIsStale = entity.ScoreIsStale,
            SnapshotAvailable = entity.ProfileSnapshotJson != null,
            DecisionSource = entity.DecisionSource,
            ShortlistedDate = entity.ShortlistedDate,
            ShortlistedById = entity.ShortlistedById,
            ShortlistedByName = entity.ShortlistedBy?.FullName,
            ShortlistingNotes = entity.ShortlistingNotes,
            IsShortlisted = entity.ShortlistedDate.HasValue,
            CurrentStageId = currentStage?.PipelineStageId,
            CurrentStageName = currentStage?.PipelineStage?.Name,
            WaitlistedDate = entity.WaitlistedDate,
            WaitlistReason = entity.WaitlistReason,
            WithdrawnDate = entity.WithdrawnDate,
            WithdrawalReason = entity.WithdrawalReason,
            RejectedDate = entity.RejectedDate,
            RejectedById = entity.RejectedById,
            RejectedByName = entity.RejectedBy?.FullName,
            RejectionReason = entity.RejectionReason,
            IsInternalCandidate = entity.IsInternalCandidate,
            InternalEmployeeId = entity.InternalEmployeeId,
            AggregatedReviewScore = entity.AggregatedReviewScore,
            HasOffer = entity.Offer != null,
            HasHireRecord = entity.HireRecord != null,
        };
    }

    public static JobApplicationSummaryDto ToSummaryDto(this JobApplication entity)
    {
        var currentStage = entity.StageHistories
            .Where(h => h.IsCurrent && !h.IsDeleted)
            .OrderByDescending(h => h.EnteredAt)
            .FirstOrDefault();

        return new JobApplicationSummaryDto
        {
            Id = entity.Id,
            ApplicationNumber = entity.ApplicationNumber,
            JobVacancyId = entity.JobVacancyId,
            VacancyNumber = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            JobTitle = entity.JobVacancy?.JobTitle ?? string.Empty,
            JobCandidateId = entity.JobCandidateId,
            CandidateName = entity.JobCandidate?.FullName ?? string.Empty,
            CandidateEmail = entity.JobCandidate?.Email ?? string.Empty,
            ApplicationDate = entity.ApplicationDate,
            Status = entity.Status,
            Source = entity.Source,
            YearsOfExperience = entity.YearsOfExperience,
            AutoScore = entity.AutoScore,
            ScoreIsStale = entity.ScoreIsStale,
            SnapshotAvailable = entity.ProfileSnapshotJson != null,
            ScoredAt = entity.ScoredAt,
            ShortlistedDate = entity.ShortlistedDate,
            IsShortlisted = entity.ShortlistedDate.HasValue,
            IsInternalCandidate = entity.IsInternalCandidate,
            AggregatedReviewScore = entity.AggregatedReviewScore,
            CurrentStageId = currentStage?.PipelineStageId,
            CurrentStageName = currentStage?.PipelineStage?.Name,
        };
    }

    public static JobApplicationDetailDto ToDetailDto(this JobApplication entity)
    {
        return new JobApplicationDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ApplicationNumber = entity.ApplicationNumber,
            JobVacancyId = entity.JobVacancyId,
            VacancyNumber = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            JobTitle = entity.JobVacancy?.JobTitle ?? string.Empty,
            JobCandidateId = entity.JobCandidateId,
            CandidateNumber = entity.JobCandidate?.CandidateNumber ?? string.Empty,
            CandidateName = entity.JobCandidate?.FullName ?? string.Empty,
            CandidateEmail = entity.JobCandidate?.Email ?? string.Empty,
            CandidatePhone = entity.JobCandidate?.Phone ?? string.Empty,
            ApplicationDate = entity.ApplicationDate,
            Status = entity.Status,
            Source = entity.Source,
            JobPostingId = entity.JobPostingId,
            JobPostingChannel = entity.JobPosting?.Channel.ToString(),
            JobPostingTitle = entity.JobPosting?.Title,
            YearsOfExperience = entity.YearsOfExperience,
            AvailableFrom = entity.AvailableFrom,
            CoverLetter = entity.CoverLetter,
            AutoScore = entity.AutoScore,
            AutoScoreBreakdown = entity.AutoScoreBreakdown,
            ShortlistedDate = entity.ShortlistedDate,
            ShortlistedById = entity.ShortlistedById,
            ShortlistedByName = entity.ShortlistedBy?.FullName,
            ShortlistingNotes = entity.ShortlistingNotes,
            WithdrawnDate = entity.WithdrawnDate,
            WithdrawalReason = entity.WithdrawalReason,
            RejectedDate = entity.RejectedDate,
            RejectedById = entity.RejectedById,
            RejectedByName = entity.RejectedBy?.FullName,
            RejectionReason = entity.RejectionReason,
            IsInternalCandidate = entity.IsInternalCandidate,
            InternalEmployeeId = entity.InternalEmployeeId,
            AggregatedReviewScore = entity.AggregatedReviewScore,
            HasOffer = entity.Offer != null,
            HasHireRecord = entity.HireRecord != null,
            StageHistories = entity.StageHistories.Select(s => s.ToDto()).ToList(),
            TestResults = entity.TestResults.Select(t => t.ToDto()).ToList(),
            InterviewSlots = entity.InterviewSlots.Select(i => i.ToSummaryDto()).ToList(),
            Communications = entity.Communications.Select(c => c.ToDto()).ToList(),
            Offer = entity.Offer?.ToSummaryDto(),
            HireRecord = entity.HireRecord?.ToSummaryDto(),
        };
    }

    public static JobApplication ToEntity(this CreateJobApplicationDto dto, Guid tenantId, Guid userId)
    {
        return new JobApplication
        {
            TenantId = tenantId,
            JobVacancyId = dto.JobVacancyId,
            JobCandidateId = dto.JobCandidateId,
            ApplicationDate = DateTime.UtcNow,
            Status = ApplicationStatus.New,
            Source = dto.Source,
            JobPostingId = dto.JobPostingId,
            YearsOfExperience = dto.YearsOfExperience,
            AvailableFrom = dto.AvailableFrom,
            CoverLetter = dto.CoverLetter,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<JobApplicationSummaryDto> ToSummaryDtoList(this IEnumerable<JobApplication> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // JOB APPLICATION STAGE HISTORY
    // ========================================================================

    #region JobApplicationStageHistory

    public static JobApplicationStageHistoryDto ToDto(this JobApplicationStageHistory entity)
    {
        return new JobApplicationStageHistoryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobApplicationId = entity.JobApplicationId,
            ApplicationNumber = entity.JobApplication?.ApplicationNumber ?? string.Empty,
            PipelineStageId = entity.PipelineStageId,
            StageName = entity.PipelineStage?.Name ?? string.Empty,
            StageType = entity.PipelineStage?.StageType ?? default,
            EnteredAt = entity.EnteredAt,
            ExitedAt = entity.ExitedAt,
            ExitReason = entity.ExitReason,
            Notes = entity.Notes,
            MovedById = entity.MovedById,
            MovedByName = entity.MovedBy?.FullName ?? string.Empty,
            IsCurrent = entity.IsCurrent,
        };
    }

    #endregion

    // ========================================================================
    // JOB APPLICANT TEST RESULT
    // ========================================================================

    #region JobApplicantTestResult

    public static JobApplicantTestResultDto ToDto(this JobApplicantTestResult entity)
    {
        return new JobApplicantTestResultDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobApplicationId = entity.JobApplicationId,
            ApplicationNumber = entity.JobApplication?.ApplicationNumber ?? string.Empty,
            CandidateName = entity.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            TestType = entity.TestType,
            TestName = entity.TestName,
            TestDate = entity.TestDate,
            Venue = entity.Venue,
            Score = entity.Score,
            MaxScore = entity.MaxScore,
            ScorePercentage = entity.ScorePercentage,
            Passed = entity.Passed,
            Remarks = entity.Remarks,
            InvigilatedById = entity.InvigilatedById,
            InvigilatedByName = entity.InvigilatedBy?.FullName,
            MarkedById = entity.MarkedById,
            MarkedByName = entity.MarkedBy?.FullName,
            MarkedDate = entity.MarkedDate,
        };
    }

    public static JobApplicantTestResult ToEntity(this CreateJobApplicantTestResultDto dto, Guid tenantId, Guid userId)
    {
        return new JobApplicantTestResult
        {
            TenantId = tenantId,
            JobApplicationId = dto.JobApplicationId,
            TestType = dto.TestType,
            TestName = dto.TestName,
            TestDate = dto.TestDate,
            Venue = dto.Venue,
            Score = dto.Score,
            MaxScore = dto.MaxScore,
            Passed = dto.Passed,
            Remarks = dto.Remarks,
            InvigilatedById = dto.InvigilatedById,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobApplicantTestResult entity, UpdateJobApplicantTestResultDto dto, Guid userId)
    {
        entity.Score = dto.Score;
        entity.MaxScore = dto.MaxScore;
        entity.Passed = dto.Passed;
        entity.Remarks = dto.Remarks;
        entity.MarkedById = dto.MarkedById;
        entity.MarkedDate = dto.MarkedDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB APPLICANT COMMUNICATION
    // ========================================================================

    #region JobApplicantCommunication

    public static JobApplicantCommunicationDto ToDto(this JobApplicantCommunication entity)
    {
        return new JobApplicantCommunicationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobApplicationId = entity.JobApplicationId,
            ApplicationNumber = entity.JobApplication?.ApplicationNumber ?? string.Empty,
            CandidateName = entity.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            Type = entity.Type,
            Direction = entity.Direction,
            Subject = entity.Subject,
            Body = entity.Body,
            SentAt = entity.SentAt,
            SentById = entity.SentById,
            SentByName = entity.SentBy?.FullName,
            TemplateId = entity.TemplateId,
            ExternalMessageId = entity.ExternalMessageId,
        };
    }

    public static JobApplicantCommunication ToEntity(this CreateJobApplicantCommunicationDto dto, Guid tenantId, Guid userId)
    {
        return new JobApplicantCommunication
        {
            TenantId = tenantId,
            JobApplicationId = dto.JobApplicationId,
            Type = dto.Type,
            Direction = dto.Direction,
            Subject = dto.Subject,
            Body = dto.Body,
            SentAt = DateTime.UtcNow,
            SentById = userId,
            TemplateId = dto.TemplateId,
            ExternalMessageId = dto.ExternalMessageId,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW QUESTION TYPE
    // ========================================================================

    #region JobInterviewQuestionType

    public static JobInterviewQuestionTypeDto ToDto(this JobInterviewQuestionType entity)
    {
        return new JobInterviewQuestionTypeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TypeName = entity.TypeName,
            Code = entity.Code,
            Description = entity.Description,
            IsActive = entity.IsActive,
            QuestionCount = entity.QuestionDetails?.Count ?? 0,
        };
    }

    public static JobInterviewQuestionTypeSummaryDto ToSummaryDto(this JobInterviewQuestionType entity)
    {
        return new JobInterviewQuestionTypeSummaryDto
        {
            Id = entity.Id,
            TypeName = entity.TypeName,
            Code = entity.Code,
            IsActive = entity.IsActive,
            QuestionCount = entity.QuestionDetails?.Count ?? 0,
        };
    }

    public static JobInterviewQuestionType ToEntity(this CreateJobInterviewQuestionTypeDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewQuestionType
        {
            TenantId = tenantId,
            TypeName = dto.TypeName,
            Code = dto.Code,
            Description = dto.Description,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobInterviewQuestionType entity, UpdateJobInterviewQuestionTypeDto dto, Guid userId)
    {
        entity.TypeName = dto.TypeName;
        entity.Code = dto.Code;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW QUESTION DETAIL
    // ========================================================================

    #region JobInterviewQuestionDetail

    public static JobInterviewQuestionDetailDto ToDto(this JobInterviewQuestionDetail entity)
    {
        return new JobInterviewQuestionDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            QuestionText = entity.QuestionText,
            Weight = entity.Weight,
            MinScore = entity.MinScore,
            MaxScore = entity.MaxScore,
            ScoringGuide = entity.ScoringGuide,
            QuestionTypeId = entity.QuestionTypeId,
            QuestionTypeName = entity.QuestionType?.TypeName ?? string.Empty,
            IsActive = entity.IsActive,
        };
    }

    public static JobInterviewQuestionDetail ToEntity(this CreateJobInterviewQuestionDetailDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewQuestionDetail
        {
            TenantId = tenantId,
            QuestionText = dto.QuestionText,
            Weight = dto.Weight,
            MinScore = dto.MinScore,
            MaxScore = dto.MaxScore,
            ScoringGuide = dto.ScoringGuide,
            QuestionTypeId = dto.QuestionTypeId,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobInterviewQuestionDetail entity, UpdateJobInterviewQuestionDetailDto dto, Guid userId)
    {
        entity.QuestionText = dto.QuestionText;
        entity.Weight = dto.Weight;
        entity.MinScore = dto.MinScore;
        entity.MaxScore = dto.MaxScore;
        entity.ScoringGuide = dto.ScoringGuide;
        entity.QuestionTypeId = dto.QuestionTypeId;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // INTERVIEW QUESTION PRESET
    // ========================================================================

    #region InterviewQuestionPreset

    public static InterviewQuestionPresetDto ToDto(this InterviewQuestionPreset entity)
    {
        return new InterviewQuestionPresetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            Items = entity.Items?.Select(i => i.ToDto()).ToList() ?? new(),
        };
    }

    public static InterviewQuestionPresetSummaryDto ToSummaryDto(this InterviewQuestionPreset entity)
    {
        return new InterviewQuestionPresetSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            ItemCount = entity.Items?.Count ?? 0,
        };
    }

    public static InterviewQuestionPreset ToEntity(this CreateInterviewQuestionPresetDto dto, Guid tenantId, Guid userId)
    {
        return new InterviewQuestionPreset
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this InterviewQuestionPreset entity, UpdateInterviewQuestionPresetDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static InterviewQuestionPresetItemDto ToDto(this InterviewQuestionPresetItem entity)
    {
        return new InterviewQuestionPresetItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PresetId = entity.PresetId,
            QuestionTypeId = entity.QuestionTypeId,
            QuestionTypeName = entity.QuestionType?.TypeName ?? string.Empty,
            RequiredQuestionCount = entity.RequiredQuestionCount,
            AllowedPoolSize = entity.AllowedPoolSize,
            DisplayOrder = entity.DisplayOrder,
        };
    }

    public static InterviewQuestionPresetItem ToEntity(this CreateInterviewQuestionPresetItemDto dto, Guid tenantId, Guid userId)
    {
        return new InterviewQuestionPresetItem
        {
            TenantId = tenantId,
            PresetId = dto.PresetId,
            QuestionTypeId = dto.QuestionTypeId,
            RequiredQuestionCount = dto.RequiredQuestionCount,
            AllowedPoolSize = dto.AllowedPoolSize,
            DisplayOrder = dto.DisplayOrder,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this InterviewQuestionPresetItem entity, UpdateInterviewQuestionPresetItemDto dto, Guid userId)
    {
        entity.QuestionTypeId = dto.QuestionTypeId;
        entity.RequiredQuestionCount = dto.RequiredQuestionCount;
        entity.AllowedPoolSize = dto.AllowedPoolSize;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW
    // ========================================================================

    #region JobInterview

    public static JobInterviewDto ToDto(this JobInterview entity)
    {
        return new JobInterviewDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            InterviewNumber = entity.InterviewNumber,
            JobVacancyId = entity.JobVacancyId,
            VacancyNumber = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            JobTitle = entity.JobVacancy?.JobTitle ?? string.Empty,
            Round = entity.Round,
            Type = entity.Type,
            Mode = entity.Mode,
            Status = entity.Status,
            ScheduledDate = entity.ScheduledDate,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            LocationOrLink = entity.LocationOrLink,
            Instructions = entity.Instructions,
            RescheduleReason = entity.RescheduleReason,
            OriginalDate = entity.OriginalDate,
            CancellationReason = entity.CancellationReason,
            IntervieweeCount = entity.Interviewees?.Count ?? 0,
            PanelistCount = (entity.Panelists?.Count ?? 0) + (entity.ExternalPanelists?.Count ?? 0),
            QuestionPresetId = entity.QuestionPresetId,
        };
    }

    public static JobInterviewSummaryDto ToSummaryDto(this JobInterview entity)
    {
        return new JobInterviewSummaryDto
        {
            Id              = entity.Id,
            InterviewNumber = entity.InterviewNumber,
            JobVacancyId    = entity.JobVacancyId,
            VacancyNumber   = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            JobTitle        = entity.JobVacancy?.JobTitle ?? string.Empty,
            Round           = entity.Round,
            Type            = entity.Type,
            Mode            = entity.Mode,
            Status          = entity.Status,
            ScheduledDate   = entity.ScheduledDate,
            StartTime       = entity.StartTime,
            EndTime         = entity.EndTime,
            LocationOrLink  = entity.LocationOrLink,
            IntervieweeCount = entity.Interviewees?.Count ?? 0,
            PanelistCount   = (entity.Panelists?.Count ?? 0) + (entity.ExternalPanelists?.Count ?? 0),
            QuestionPresetId = entity.QuestionPresetId,
            CandidateNames  = entity.Interviewees?
                .Select(ie => ie.JobApplication?.JobCandidate?.FullName)
                .Where(n => n is not null)
                .Cast<string>()
                .ToList() ?? new(),
            PanelistNames   = (entity.Panelists?
                .Select(p => p.Employee?.FullName)
                .Where(n => n is not null)
                .Cast<string>() ?? Enumerable.Empty<string>())
                .Concat(entity.ExternalPanelists?
                    .Select(ep => ep.ExternalAssociate is null ? null
                        : $"{ep.ExternalAssociate.FirstName} {ep.ExternalAssociate.LastName}".Trim())
                    .Where(n => n is not null)
                    .Cast<string>() ?? Enumerable.Empty<string>())
                .ToList(),
        };
    }

    public static JobInterviewDetailDto ToDetailDto(this JobInterview entity)
    {
        return new JobInterviewDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            InterviewNumber = entity.InterviewNumber,
            JobVacancyId = entity.JobVacancyId,
            VacancyNumber = entity.JobVacancy?.VacancyNumber ?? string.Empty,
            JobTitle = entity.JobVacancy?.JobTitle ?? string.Empty,
            Round = entity.Round,
            Type = entity.Type,
            Mode = entity.Mode,
            Status = entity.Status,
            ScheduledDate = entity.ScheduledDate,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            LocationOrLink = entity.LocationOrLink,
            Instructions = entity.Instructions,
            RescheduleReason = entity.RescheduleReason,
            OriginalDate = entity.OriginalDate,
            CancellationReason = entity.CancellationReason,
            IntervieweeCount = entity.Interviewees?.Count ?? 0,
            PanelistCount = (entity.Panelists?.Count ?? 0) + (entity.ExternalPanelists?.Count ?? 0),
            QuestionPresetId = entity.QuestionPresetId,
            Interviewees = entity.Interviewees?
                .OrderBy(i => i.SlotStartTime ?? TimeSpan.MaxValue)
                .Select(i => i.ToDto()).ToList() ?? new(),
            Panelists = entity.Panelists?.Select(p => p.ToDto()).ToList() ?? new(),
            ExternalPanelists = entity.ExternalPanelists?.Select(p => p.ToDto()).ToList() ?? new(),
            Questions = entity.Questions.OrderBy(q => q.DisplayOrder).Select(q => q.ToDto()).ToList(),
        };
    }

    public static JobInterview ToEntity(this CreateJobInterviewDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterview
        {
            TenantId = tenantId,
            JobVacancyId = dto.JobVacancyId,
            Round = dto.Round,
            Type = dto.Type,
            Status = JobInterviewStatus.Scheduled,
            ScheduledDate = dto.ScheduledDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            LocationOrLink = dto.LocationOrLink,
            Instructions = dto.Instructions,
            Mode = dto.Mode,
            QuestionPresetId = dto.QuestionPresetId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobInterview entity, UpdateJobInterviewDto dto, Guid userId)
    {
        entity.Round = dto.Round;
        entity.Type = dto.Type;
        entity.Mode = dto.Mode;
        // Status is owned by reschedule / cancel / complete — see UpdateJobInterviewDto.
        entity.ScheduledDate = dto.ScheduledDate;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.LocationOrLink = dto.LocationOrLink;
        entity.Instructions = dto.Instructions;
        entity.QuestionPresetId = dto.QuestionPresetId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<JobInterviewSummaryDto> ToSummaryDtoList(this IEnumerable<JobInterview> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // JOB INTERVIEW PANELIST
    // ========================================================================

    #region JobInterviewPanelist

    public static JobInterviewPanelistDto ToDto(this JobInterviewPanelist entity)
    {
        return new JobInterviewPanelistDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobInterviewId = entity.JobInterviewId,
            InterviewNumber = entity.JobInterview?.InterviewNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeePositionTitle = entity.Employee?.Position?.Title,
            Role = entity.Role,
            IsRequired = entity.IsRequired,
            Attended = entity.Attended,
            NoShowReason = entity.NoShowReason,
            InvitationSentDate = entity.InvitationSentDate,
            IsConfirmed = entity.IsConfirmed,
            ConfirmationDate = entity.ConfirmationDate,
        };
    }

    public static JobInterviewPanelist ToEntity(this AddJobInterviewPanelistDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewPanelist
        {
            TenantId = tenantId,
            JobInterviewId = dto.JobInterviewId,
            EmployeeId = dto.EmployeeId,
            Role = dto.Role,
            IsRequired = dto.IsRequired,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW EXTERNAL PANELIST
    // ========================================================================

    #region JobInterviewExternalPanelist

    public static JobInterviewExternalPanelistDto ToDto(this JobInterviewExternalPanelist entity)
    {
        var assoc = entity.ExternalAssociate;
        return new JobInterviewExternalPanelistDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobInterviewId = entity.JobInterviewId,
            InterviewNumber = entity.JobInterview?.InterviewNumber ?? string.Empty,
            AssociateId = entity.AssociateId,
            AssociateName = assoc != null
                ? $"{assoc.FirstName} {assoc.MiddleName} {assoc.LastName}".Trim()
                : string.Empty,
            AssociateOrganization = assoc?.CompanyName,
            Role = entity.Role,
            IsRequired = entity.IsRequired,
            Attended = entity.Attended,
            NoShowReason = entity.NoShowReason,
            InvitationSentDate = entity.InvitationSentDate,
            IsConfirmed = entity.IsConfirmed,
            ConfirmationDate = entity.ConfirmationDate,
        };
    }

    public static JobInterviewExternalPanelist ToEntity(this AddJobInterviewExternalPanelistDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewExternalPanelist
        {
            TenantId = tenantId,
            JobInterviewId = dto.JobInterviewId,
            AssociateId = dto.AssociateId,
            Role = dto.Role,
            IsRequired = dto.IsRequired,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEWEE
    // ========================================================================

    #region JobInterviewee

    public static JobIntervieweeDto ToDto(this JobInterviewee entity)
    {
        return new JobIntervieweeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobInterviewId = entity.JobInterviewId,
            InterviewNumber = entity.JobInterview?.InterviewNumber ?? string.Empty,
            JobApplicationId = entity.JobApplicationId,
            ApplicationNumber = entity.JobApplication?.ApplicationNumber ?? string.Empty,
            CandidateName = entity.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            CandidateEmail = entity.JobApplication?.JobCandidate?.Email ?? string.Empty,
            JobCandidateId = entity.JobApplication?.JobCandidateId,
            SlotStartTime = entity.SlotStartTime,
            SlotEndTime = entity.SlotEndTime,
            InvitationSentDate = entity.InvitationSentDate,
            ConfirmedAttendance = entity.ConfirmedAttendance,
            ConfirmationDate = entity.ConfirmationDate,
            CandidateAttended = entity.CandidateAttended,
            NoShowReason = entity.NoShowReason,
            Outcome = entity.Outcome,
        };
    }

    public static JobIntervieweeSummaryDto ToSummaryDto(this JobInterviewee entity)
    {
        return new JobIntervieweeSummaryDto
        {
            Id = entity.Id,
            JobInterviewId = entity.JobInterviewId,
            InterviewNumber = entity.JobInterview?.InterviewNumber ?? string.Empty,
            ScheduledDate = entity.JobInterview?.ScheduledDate ?? default,
            InterviewStatus = entity.JobInterview?.Status ?? default,
            CandidateAttended = entity.CandidateAttended,
            Outcome = entity.Outcome,
        };
    }

    public static JobInterviewee ToEntity(this AddJobIntervieweeDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewee
        {
            TenantId = tenantId,
            JobInterviewId = dto.JobInterviewId,
            JobApplicationId = dto.JobApplicationId,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW QUESTION (per-interview plan)
    // ========================================================================

    #region JobInterviewQuestion

    public static JobInterviewQuestionDto ToDto(this JobInterviewQuestion entity)
    {
        return new JobInterviewQuestionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobInterviewId = entity.JobInterviewId,
            InterviewNumber = entity.JobInterview?.InterviewNumber ?? string.Empty,
            QuestionTypeId = entity.QuestionTypeId,
            QuestionTypeName = entity.QuestionType?.TypeName ?? string.Empty,
            RequiredQuestionCount = entity.RequiredQuestionCount,
            AllowedPoolSize = entity.AllowedPoolSize,
            DisplayOrder = entity.DisplayOrder,
            // Sorted here as well as in the query: this mapper is the single choke point both the
            // plan list and the interview detail read go through, and an unordered Include hands back
            // whatever order the database chose. DisplayOrder is the sequence the panel committed.
            SelectedQuestions = entity.SelectedQuestions
                .OrderBy(q => q.DisplayOrder)
                .Select(q => q.ToDto())
                .ToList(),
        };
    }

    public static JobInterviewQuestion ToEntity(this CreateJobInterviewQuestionDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewQuestion
        {
            TenantId = tenantId,
            JobInterviewId = dto.JobInterviewId,
            QuestionTypeId = dto.QuestionTypeId,
            RequiredQuestionCount = dto.RequiredQuestionCount,
            AllowedPoolSize = dto.AllowedPoolSize,
            DisplayOrder = dto.DisplayOrder,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobInterviewQuestion entity, UpdateJobInterviewQuestionDto dto, Guid userId)
    {
        entity.RequiredQuestionCount = dto.RequiredQuestionCount;
        entity.AllowedPoolSize = dto.AllowedPoolSize;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW SELECTED QUESTION
    // ========================================================================

    #region JobInterviewSelectedQuestion

    public static JobInterviewSelectedQuestionDto ToDto(this JobInterviewSelectedQuestion entity)
    {
        return new JobInterviewSelectedQuestionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobInterviewQuestionId = entity.JobInterviewQuestionId,
            QuestionDetailId = entity.QuestionDetailId,
            QuestionText = entity.Question?.QuestionText ?? string.Empty,
            Weight = entity.Question?.Weight ?? 0,
            MinScore = entity.Question?.MinScore ?? 0,
            MaxScore = entity.Question?.MaxScore ?? 0,
            ScoringGuide = entity.Question?.ScoringGuide,
            DisplayOrder = entity.DisplayOrder,
        };
    }

    public static JobInterviewSelectedQuestion ToEntity(this CreateJobInterviewSelectedQuestionDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewSelectedQuestion
        {
            TenantId = tenantId,
            JobInterviewQuestionId = dto.JobInterviewQuestionId,
            QuestionDetailId = dto.QuestionDetailId,
            DisplayOrder = dto.DisplayOrder,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW SCORE SUMMARY
    // ========================================================================

    #region JobInterviewScoreSummary

    public static JobInterviewScoreSummaryDto ToDto(this JobInterviewScoreSummary entity)
    {
        return new JobInterviewScoreSummaryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobIntervieweeId = entity.JobIntervieweeId,
            CandidateName = entity.JobInterviewee?.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            ApplicationNumber = entity.JobInterviewee?.JobApplication?.ApplicationNumber ?? string.Empty,
            InternalPanelistId = entity.InternalPanelistId,
            InternalPanelistName = entity.InternalPanelist?.Employee?.FullName,
            ExternalPanelistId = entity.ExternalPanelistId,
            ExternalPanelistName = entity.ExternalPanelist != null
                ? $"{entity.ExternalPanelist.ExternalAssociate?.FirstName} {entity.ExternalPanelist.ExternalAssociate?.LastName}".Trim()
                : null,
            TotalRawScore = entity.TotalRawScore,
            TotalWeightedScore = entity.TotalWeightedScore,
            Recommendation = entity.Recommendation,
            Comments = entity.Comments,
            EvaluationDate = entity.EvaluationDate,
            IsFinalized = entity.IsFinalized,
            FinalizedDate = entity.FinalizedDate,
            ScoreSource = entity.ScoreSource,
            FiledByHrOnBehalfOfEmployeeId = entity.FiledByHrOnBehalfOfEmployeeId,
            FiledByHrOnBehalfOfName = entity.FiledByHrOnBehalfOf?.FullName,
        };
    }

    public static JobInterviewScoreSummaryDetailDto ToDetailDto(this JobInterviewScoreSummary entity)
    {
        return new JobInterviewScoreSummaryDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobIntervieweeId = entity.JobIntervieweeId,
            CandidateName = entity.JobInterviewee?.JobApplication?.JobCandidate?.FullName ?? string.Empty,
            ApplicationNumber = entity.JobInterviewee?.JobApplication?.ApplicationNumber ?? string.Empty,
            InternalPanelistId = entity.InternalPanelistId,
            InternalPanelistName = entity.InternalPanelist?.Employee?.FullName,
            ExternalPanelistId = entity.ExternalPanelistId,
            ExternalPanelistName = entity.ExternalPanelist != null
                ? $"{entity.ExternalPanelist.ExternalAssociate?.FirstName} {entity.ExternalPanelist.ExternalAssociate?.LastName}".Trim()
                : null,
            TotalRawScore = entity.TotalRawScore,
            TotalWeightedScore = entity.TotalWeightedScore,
            Recommendation = entity.Recommendation,
            Comments = entity.Comments,
            EvaluationDate = entity.EvaluationDate,
            IsFinalized = entity.IsFinalized,
            FinalizedDate = entity.FinalizedDate,
            ScoreSource = entity.ScoreSource,
            FiledByHrOnBehalfOfEmployeeId = entity.FiledByHrOnBehalfOfEmployeeId,
            FiledByHrOnBehalfOfName = entity.FiledByHrOnBehalfOf?.FullName,
            ScoreEntries = entity.ScoreEntries.Select(e => e.ToDto()).ToList(),
        };
    }

    public static JobInterviewScoreSummary ToEntity(this CreateJobInterviewScoreSummaryDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewScoreSummary
        {
            TenantId = tenantId,
            JobIntervieweeId = dto.JobIntervieweeId,
            InternalPanelistId = dto.InternalPanelistId,
            ExternalPanelistId = dto.ExternalPanelistId,
            TotalRawScore = 0,
            TotalWeightedScore = 0,
            Recommendation = dto.Recommendation,
            Comments = dto.Comments,
            EvaluationDate = dto.EvaluationDate,
            IsFinalized = false,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW SCORE ENTRY
    // ========================================================================

    #region JobInterviewScoreEntry

    public static JobInterviewScoreEntryDto ToDto(this JobInterviewScoreEntry entity)
    {
        return new JobInterviewScoreEntryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScoreSummaryId = entity.ScoreSummaryId,
            QuestionDetailId = entity.QuestionDetailId,
            QuestionText = entity.Question?.QuestionText ?? string.Empty,
            RawScore = entity.RawScore,
            WeightedScore = entity.WeightedScore,
            Remarks = entity.Remarks,
        };
    }

    public static JobInterviewScoreEntry ToEntity(this CreateJobInterviewScoreEntryDto dto, Guid tenantId, Guid userId)
    {
        return new JobInterviewScoreEntry
        {
            TenantId = tenantId,
            ScoreSummaryId = dto.ScoreSummaryId,
            QuestionDetailId = dto.QuestionDetailId,
            RawScore = dto.RawScore,
            WeightedScore = 0,
            Remarks = dto.Remarks,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // JOB INTERVIEW SCORE DRAFT
    // ========================================================================

    #region JobInterviewScoreDraft

    public static InterviewScoreDraftDto ToDraftDto(this JobInterviewScoreDraft entity)
    {
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<DraftScoreEntryDto>>(
            entity.DraftJson) ?? new List<DraftScoreEntryDto>();

        return new InterviewScoreDraftDto
        {
            Id                = entity.Id,
            CreatedAt         = entity.CreatedAt,
            CreatedBy         = entity.CreatedBy ?? string.Empty,
            UpdatedAt         = entity.UpdatedAt,
            UpdatedBy         = entity.UpdatedBy,
            JobInterviewId    = entity.JobInterviewId,
            JobIntervieweeId  = entity.JobIntervieweeId,
            InternalPanelistId = entity.InternalPanelistId,
            ExternalPanelistId = entity.ExternalPanelistId,
            ScoreEntries      = entries,
            Comments          = entity.Comments,
            Recommendation    = entity.Recommendation,
            LastModified      = entity.LastModified,
        };
    }

    #endregion

    // ========================================================================
    // JOB OFFER
    // ========================================================================

    #region JobOffer

    public static JobOfferDto ToDto(this JobOffer entity)
    {
        return new JobOfferDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            OfferNumber = entity.OfferNumber,
            JobApplicationId = entity.JobApplicationId,
            ApplicationNumber = entity.Application?.ApplicationNumber ?? string.Empty,
            CandidateName = entity.Application?.JobCandidate?.FullName ?? string.Empty,
            OfferStatus = entity.OfferStatus,
            PositionId = entity.PositionId,
            PositionTitle = entity.PositionTitle,
            ReportsToTitle = entity.ReportsToTitle,
            GradeTitle = entity.GradeTitle,
            DepartmentName = entity.DepartmentName,
            WorkMode = entity.WorkMode,
            SalaryGradeMin = entity.SalaryGradeMin,
            SalaryGradeMax = entity.SalaryGradeMax,
            SalaryLevelId     = entity.SalaryLevelId,
            SalaryLevelName   = entity.SalaryLevel?.Name,
            SalaryNotchId     = entity.SalaryNotchId,
            SalaryNotchNumber = entity.SalaryNotch?.NotchNumber,
            SalaryNotchAmount = entity.SalaryNotch?.SalaryAmount,
            ProbationPeriodMonths = entity.ProbationPeriodMonths,
            NoticePeriodMonths = entity.NoticePeriodMonths,
            AnnualLeaveDays = entity.AnnualLeaveDays,
            WeeklyHours = entity.WeeklyHours,
            NdaRequired = entity.NdaRequired,
            LocationLevelId = entity.LocationLevelId,
            LocationLevelName = entity.LocationLevel?.Name,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            EmploymentType = entity.EmploymentType,
            ContractDurationMonths = entity.ContractDurationMonths,
            BaseSalary = entity.BaseSalary,
            CurrencyCode = entity.CurrencyCode,
            Bonus = entity.Bonus,
            BonusTerms = entity.BonusTerms,
            Commission = entity.Commission,
            CommissionStructure = entity.CommissionStructure,
            Benefits = entity.Benefits.Select(b => b.ToDto()),
            ProposedStartDate = entity.ProposedStartDate,
            AdditionalTerms = entity.AdditionalTerms,
            PreparedById = entity.PreparedById,
            PreparedByName = entity.PreparedBy?.FullName,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovedDate = entity.ApprovedDate,
            OfferDate = entity.OfferDate,
            ExpiryDate = entity.ExpiryDate,
            OfferLetterPath = entity.OfferLetterPath,
            SignedOfferLetterPath = entity.SignedOfferLetterPath,
            AcceptedDate = entity.AcceptedDate,
            CounteredDate = entity.CounteredDate,
            CandidateResponseNotes = entity.CandidateResponseNotes,
            DeclinedDate = entity.DeclinedDate,
            DeclineReason = entity.DeclineReason,
            RevokedDate = entity.RevokedDate,
            RevocationReason = entity.RevocationReason,
            ApprovalRejectionReason = entity.ApprovalRejectionReason,
            Version = entity.Version,
            PreviousOfferId = entity.PreviousOfferId,
            IsConditional = entity.IsConditional,
            PreEmploymentCheckId = entity.PreEmploymentCheck?.Id,
            PreEmploymentCheckStatus = entity.PreEmploymentCheck?.OverallStatus,
            CandidateId = entity.Application?.JobCandidateId ?? Guid.Empty,
        };
    }

    public static JobOfferSummaryDto ToSummaryDto(this JobOffer entity)
    {
        return new JobOfferSummaryDto
        {
            Id = entity.Id,
            OfferNumber = entity.OfferNumber,
            JobApplicationId = entity.JobApplicationId,
            ApplicationNumber = entity.Application?.ApplicationNumber ?? string.Empty,
            CandidateName = entity.Application?.JobCandidate?.FullName ?? string.Empty,
            OfferStatus = entity.OfferStatus,
            PositionTitle = entity.PositionTitle,
            EmploymentType = entity.EmploymentType,
            BaseSalary = entity.BaseSalary,
            CurrencyCode = entity.CurrencyCode,
            ProposedStartDate = entity.ProposedStartDate,
            OfferDate = entity.OfferDate,
            ExpiryDate = entity.ExpiryDate,
            Version = entity.Version,
            IsLatestVersion = entity.IsLatestVersion,
            CreatedAt = entity.CreatedAt,
        };
    }

    public static JobOffer ToEntity(this CreateJobOfferDto dto, Guid tenantId, Guid userId)
    {
        return new JobOffer
        {
            TenantId = tenantId,
            JobApplicationId = dto.JobApplicationId,
            OfferStatus = JobOfferStatus.Draft,
            // PositionId, PositionTitle, ReportsToTitle, GradeTitle and EmploymentType are set by
            // CreateAsync from the application's vacancy and position — they are snapshots of the
            // role, not negotiable terms, and are no longer on the payload at all.

            LocationLevelId = dto.LocationLevelId,
            LocationId = dto.LocationId,
            ContractDurationMonths = dto.ContractDurationMonths,
            ProbationPeriodMonths = dto.ProbationPeriodMonths,
            NoticePeriodMonths = dto.NoticePeriodMonths,
            AnnualLeaveDays = dto.AnnualLeaveDays,
            WeeklyHours = dto.WeeklyHours,
            NdaRequired = dto.NdaRequired,
            IsConditional = dto.IsConditional,
            ExpiryDate = dto.ExpiryDate,
            BaseSalary = dto.BaseSalary,
            SalaryLevelId = dto.SalaryLevelId,
            SalaryNotchId = dto.SalaryNotchId,
            CurrencyCode = dto.CurrencyCode,
            Bonus = dto.Bonus,
            BonusTerms = dto.BonusTerms,
            Commission = dto.Commission,
            CommissionStructure = dto.CommissionStructure,
            ProposedStartDate = dto.ProposedStartDate,
            AdditionalTerms = dto.AdditionalTerms,
            PreparedById = userId,
            Version = 1,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this JobOffer entity, UpdateJobOfferDto dto, Guid userId)
    {
        // The role snapshot — position title, reporting line, grade, employment type and work mode —
        // belongs to the position and the vacancy, and is set once at create. It used to be written
        // from this payload, which let an edit falsify the role the offer describes.
        entity.LocationLevelId = dto.LocationLevelId;
        entity.LocationId = dto.LocationId;
        entity.ContractDurationMonths = dto.ContractDurationMonths;
        entity.ProbationPeriodMonths = dto.ProbationPeriodMonths;
        entity.NoticePeriodMonths = dto.NoticePeriodMonths;
        entity.AnnualLeaveDays = dto.AnnualLeaveDays;
        entity.WeeklyHours = dto.WeeklyHours;
        entity.NdaRequired = dto.NdaRequired;
        entity.IsConditional = dto.IsConditional;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.BaseSalary = dto.BaseSalary;
        entity.SalaryLevelId = dto.SalaryLevelId;
        entity.SalaryNotchId = dto.SalaryNotchId;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.Bonus = dto.Bonus;
        entity.BonusTerms = dto.BonusTerms;
        entity.Commission = dto.Commission;
        entity.CommissionStructure = dto.CommissionStructure;
        entity.ProposedStartDate = dto.ProposedStartDate;
        entity.AdditionalTerms = dto.AdditionalTerms;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<JobOfferSummaryDto> ToSummaryDtoList(this IEnumerable<JobOffer> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static JobOfferBenefitDto ToDto(this JobOfferBenefit entity)
    {
        return new JobOfferBenefitDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobOfferId = entity.JobOfferId,
            BenefitName = entity.BenefitName,
            Description = entity.Description,
            MonetaryValue = entity.MonetaryValue,
            CurrencyCode = entity.CurrencyCode,
            IsMonetary = entity.IsMonetary,
            DisplayOrder = entity.DisplayOrder,
        };
    }

    public static JobOfferNoteDto ToDto(this JobOfferNote entity)
    {
        return new JobOfferNoteDto
        {
            Id         = entity.Id,
            CreatedAt  = entity.CreatedAt,
            CreatedBy  = entity.CreatedBy ?? string.Empty,
            UpdatedAt  = entity.UpdatedAt,
            UpdatedBy  = entity.UpdatedBy,
            JobOfferId = entity.JobOfferId,
            Body       = entity.Body,
            AuthorName = entity.AuthorName,
        };
    }

    #endregion

    // ========================================================================
    // JOB HIRE RECORD
    // ========================================================================

    #region JobHireRecord

    public static JobHireRecordDto ToDto(this JobHireRecord entity)
    {
        return new JobHireRecordDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            HireNumber = entity.HireNumber,
            ApplicationId = entity.ApplicationId,
            ApplicationNumber = entity.Application?.ApplicationNumber ?? string.Empty,
            CandidateName = entity.Application?.JobCandidate?.FullName ?? string.Empty,
            OfferId = entity.OfferId,
            OfferNumber = entity.Offer?.OfferNumber ?? string.Empty,
            Status = entity.Status,
            ExpectedStartDate = entity.ExpectedStartDate,
            ActualStartDate = entity.ActualStartDate,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            EmployeeName = entity.Employee?.FullName,
            ConfirmedById = entity.ConfirmedById,
            ConfirmedByName = entity.ConfirmedBy?.FullName ?? string.Empty,
            ConfirmedDate = entity.ConfirmedDate,
            Notes = entity.Notes,
        };
    }

    public static JobHireRecordSummaryDto ToSummaryDto(this JobHireRecord entity)
    {
        return new JobHireRecordSummaryDto
        {
            Id = entity.Id,
            HireNumber = entity.HireNumber,
            CandidateName = entity.Application?.JobCandidate?.FullName ?? string.Empty,
            PositionTitle = entity.Application?.JobVacancy?.Position?.Title ?? string.Empty,
            Status = entity.Status,
            ExpectedStartDate = entity.ExpectedStartDate,
            ActualStartDate = entity.ActualStartDate,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
        };
    }

    public static JobHireRecord ToEntity(this CreateJobHireRecordDto dto, Guid tenantId, Guid userId)
    {
        return new JobHireRecord
        {
            TenantId = tenantId,
            ApplicationId = dto.ApplicationId,
            OfferId = dto.OfferId,
            Status = JobHireStatus.PendingOnboarding,
            ExpectedStartDate = dto.ExpectedStartDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<JobHireRecordSummaryDto> ToSummaryDtoList(this IEnumerable<JobHireRecord> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // PRE-EMPLOYMENT CHECK
    // ========================================================================

    #region PreEmploymentCheck

    public static PreEmploymentCheckDto ToDto(this PreEmploymentCheck entity)
    {
        return new PreEmploymentCheckDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobOfferId = entity.JobOfferId,
            OfferNumber = entity.JobOffer?.OfferNumber ?? string.Empty,
            OfferStatus = entity.JobOffer?.OfferStatus ?? JobOfferStatus.Draft,
            CandidateName = entity.JobOffer?.Application?.JobCandidate?.FullName ?? string.Empty,
            OverallStatus = entity.OverallStatus,
            CoordinatedById = entity.CoordinatedById,
            CoordinatedByName = entity.CoordinatedBy?.FullName,
            CompletedDate = entity.CompletedDate,
            Notes = entity.Notes,
            TotalItems = entity.Items?.Count ?? 0,
            CompletedItems = entity.Items?.Count(i => i.Status == CheckItemStatus.Verified) ?? 0,
            PassedItems = entity.Items?.Count(i => i.Passed == true) ?? 0,
            FailedItems = entity.Items?.Count(i => i.Passed == false) ?? 0,
        };
    }

    public static PreEmploymentCheckDetailDto ToDetailDto(this PreEmploymentCheck entity)
    {
        return new PreEmploymentCheckDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            JobOfferId = entity.JobOfferId,
            OfferNumber = entity.JobOffer?.OfferNumber ?? string.Empty,
            OfferStatus = entity.JobOffer?.OfferStatus ?? JobOfferStatus.Draft,
            CandidateName = entity.JobOffer?.Application?.JobCandidate?.FullName ?? string.Empty,
            OverallStatus = entity.OverallStatus,
            CoordinatedById = entity.CoordinatedById,
            CoordinatedByName = entity.CoordinatedBy?.FullName,
            CompletedDate = entity.CompletedDate,
            Notes = entity.Notes,
            TotalItems = entity.Items?.Count ?? 0,
            CompletedItems = entity.Items?.Count(i => i.Status == CheckItemStatus.Verified) ?? 0,
            PassedItems = entity.Items?.Count(i => i.Passed == true) ?? 0,
            FailedItems = entity.Items?.Count(i => i.Passed == false) ?? 0,
            Items = entity.Items?.Select(i => i.ToDto()).ToList() ?? new(),
        };
    }

    public static PreEmploymentCheck ToEntity(this CreatePreEmploymentCheckDto dto, Guid tenantId, Guid userId)
    {
        return new PreEmploymentCheck
        {
            TenantId = tenantId,
            JobOfferId = dto.JobOfferId,
            OverallStatus = PreEmploymentCheckStatus.Pending,
            CoordinatedById = dto.CoordinatedById,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // PRE-EMPLOYMENT CHECK ITEM
    // ========================================================================

    #region PreEmploymentCheckItem

    public static PreEmploymentCheckItemDto ToDto(this PreEmploymentCheckItem entity)
    {
        return new PreEmploymentCheckItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PreEmploymentCheckId = entity.PreEmploymentCheckId,
            CheckType = entity.CheckType,
            Name = entity.Name,
            ServiceProviderName = entity.ServiceProviderName,
            ServiceProviderSupplierId = entity.ServiceProviderSupplierId,
            Status = entity.Status,
            RequestedDate = entity.RequestedDate,
            ReceivedDate = entity.ReceivedDate,
            ExpiryDate = entity.ExpiryDate,
            Passed = entity.Passed,
            Instructions = entity.Instructions,
            Remarks = entity.Remarks,
            HasDocument = entity.DocumentFileUploadRecordId.HasValue
                          || !string.IsNullOrWhiteSpace(entity.DocumentPath),
            DocumentFileName = entity.DocumentFileName,
            ExpectedDays = entity.ExpectedDays,
            IsMandatory = entity.IsMandatory,
            IsBlockingOnFail = entity.IsBlockingOnFail,
            ReviewedById = entity.ReviewedById,
            ReviewedByName = entity.ReviewedBy?.FullName,
            ReviewedDate = entity.ReviewedDate,
            HasReferenceResponse = entity.ReferenceResponse != null,
        };
    }

    public static PreEmploymentCheckItem ToEntity(this CreatePreEmploymentCheckItemDto dto, Guid tenantId, Guid userId)
    {
        return new PreEmploymentCheckItem
        {
            TenantId = tenantId,
            PreEmploymentCheckId = dto.PreEmploymentCheckId,
            CheckType = dto.CheckType,
            Name = dto.Name,
            ServiceProviderName = dto.ServiceProviderName,
            ServiceProviderSupplierId = dto.ServiceProviderSupplierId,
            Instructions = dto.Instructions,
            Status = CheckItemStatus.Pending,
            IsMandatory = dto.IsMandatory,
            IsBlockingOnFail = dto.IsBlockingOnFail,
            ExpectedDays = dto.ExpectedDays,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this PreEmploymentCheckItem entity, UpdatePreEmploymentCheckItemDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.ServiceProviderName = dto.ServiceProviderName;
        entity.ServiceProviderSupplierId = dto.ServiceProviderSupplierId;
        entity.Status = dto.Status;
        entity.RequestedDate = dto.RequestedDate;
        entity.ReceivedDate = dto.ReceivedDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Passed = dto.Passed;
        // Auto-derive status from the recorded result so Completed count stays accurate.
        // Only override when Passed is explicitly set; otherwise honour the chosen Status.
        if (dto.Passed.HasValue)
            entity.Status = dto.Passed.Value ? CheckItemStatus.Verified : CheckItemStatus.Failed;
        entity.Instructions = dto.Instructions;
        entity.Remarks = dto.Remarks;
        // DocumentPath is no longer settable from a payload — see UpdatePreEmploymentCheckItemDto.
        entity.ExpectedDays = dto.ExpectedDays;
        entity.ReviewedById = dto.ReviewedById;
        entity.ReviewedDate = dto.ReviewedDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // REFERENCE CHECK RESPONSE
    // ========================================================================

    #region ReferenceCheckResponse

    public static ReferenceCheckResponseDto ToDto(this ReferenceCheckResponse entity)
    {
        return new ReferenceCheckResponseDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CheckItemId = entity.CheckItemId,
            RefereeId = entity.RefereeId,
            RefereeName = entity.RefereeName,
            RefereeOrganisation = entity.RefereeOrganisation,
            RefereePosition = entity.RefereePosition,
            RefereeEmail = entity.RefereeEmail,
            RefereePhone = entity.RefereePhone,
            ResponseDate = entity.ResponseDate,
            ResponseMethod = entity.ResponseMethod,
            OverallRating = entity.OverallRating,
            Comments = entity.Comments,
            WouldRehire = entity.WouldRehire,
            ConfirmedDatesOfEmployment = entity.ConfirmedDatesOfEmployment,
            ConfirmedPositionHeld = entity.ConfirmedPositionHeld,
            ConfirmedReasonForLeaving = entity.ConfirmedReasonForLeaving,
            HasDocument = entity.DocumentFileUploadRecordId.HasValue
                          || !string.IsNullOrWhiteSpace(entity.DocumentPath),
            DocumentFileName = entity.DocumentFileName,
        };
    }

    public static ReferenceCheckResponse ToEntity(this CreateReferenceCheckResponseDto dto, Guid tenantId, Guid userId)
    {
        return new ReferenceCheckResponse
        {
            TenantId = tenantId,
            CheckItemId = dto.CheckItemId,
            RefereeId = dto.RefereeId,
            RefereeName = dto.RefereeName,
            RefereeOrganisation = dto.RefereeOrganisation,
            RefereePosition = dto.RefereePosition,
            RefereeEmail = dto.RefereeEmail,
            RefereePhone = dto.RefereePhone,
            ResponseDate = DateTime.UtcNow,
            ResponseMethod = dto.ResponseMethod,
            OverallRating = dto.OverallRating,
            Comments = dto.Comments,
            WouldRehire = dto.WouldRehire,
            ConfirmedDatesOfEmployment = dto.ConfirmedDatesOfEmployment,
            ConfirmedPositionHeld = dto.ConfirmedPositionHeld,
            ConfirmedReasonForLeaving = dto.ConfirmedReasonForLeaving,
            // DocumentPath arrives through the upload gate, not the payload.
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ReferenceCheckResponse entity, UpdateReferenceCheckResponseDto dto, Guid userId)
    {
        entity.OverallRating = dto.OverallRating;
        entity.Comments = dto.Comments;
        entity.WouldRehire = dto.WouldRehire;
        entity.ConfirmedDatesOfEmployment = dto.ConfirmedDatesOfEmployment;
        entity.ConfirmedPositionHeld = dto.ConfirmedPositionHeld;
        entity.ConfirmedReasonForLeaving = dto.ConfirmedReasonForLeaving;
        // DocumentPath arrives through the upload gate, not the payload.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // PRE-EMPLOYMENT CHECK TEMPLATE
    // ========================================================================

    #region PreEmploymentCheckTemplate

    public static PreEmploymentCheckTemplateDto ToDto(this PreEmploymentCheckTemplate entity)
    {
        return new PreEmploymentCheckTemplateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            ItemCount = entity.Items.Count,
        };
    }

    public static PreEmploymentCheckTemplateDetailDto ToDetailDto(this PreEmploymentCheckTemplate entity)
    {
        return new PreEmploymentCheckTemplateDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            ItemCount = entity.Items.Count,
            Items = entity.Items.Select(i => i.ToDto()).ToList(),
        };
    }

    public static PreEmploymentCheckTemplateItemDto ToDto(this PreEmploymentCheckTemplateItem entity)
    {
        return new PreEmploymentCheckTemplateItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TemplateId = entity.TemplateId,
            CheckType = entity.CheckType,
            DefaultServiceProvider = entity.DefaultServiceProvider,
            DefaultServiceProviderSupplierId = entity.DefaultServiceProviderSupplierId,
            Instructions = entity.Instructions,
            IsMandatory = entity.IsMandatory,
            IsBlockingOnFail = entity.IsBlockingOnFail,
            ExpectedDays = entity.ExpectedDays,
        };
    }

    public static PreEmploymentCheckTemplate ToEntity(this CreatePreEmploymentCheckTemplateDto dto, Guid tenantId, Guid userId)
    {
        return new PreEmploymentCheckTemplate
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true,
            CreatedBy = userId.ToString(),
        };
    }

    public static PreEmploymentCheckTemplateItem ToEntity(this CreatePreEmploymentCheckTemplateItemDto dto, Guid tenantId, Guid userId)
    {
        return new PreEmploymentCheckTemplateItem
        {
            TenantId = tenantId,
            TemplateId = dto.TemplateId,
            CheckType = dto.CheckType,
            DefaultServiceProvider = dto.DefaultServiceProvider,
            DefaultServiceProviderSupplierId = dto.DefaultServiceProviderSupplierId,
            Instructions = dto.Instructions,
            IsMandatory = dto.IsMandatory,
            IsBlockingOnFail = dto.IsBlockingOnFail,
            ExpectedDays = dto.ExpectedDays,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this PreEmploymentCheckTemplate entity, UpdatePreEmploymentCheckTemplateDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static void UpdateEntity(this PreEmploymentCheckTemplateItem entity, UpdatePreEmploymentCheckTemplateItemDto dto, Guid userId)
    {
        entity.DefaultServiceProvider = dto.DefaultServiceProvider;
        entity.DefaultServiceProviderSupplierId = dto.DefaultServiceProviderSupplierId;
        entity.Instructions = dto.Instructions;
        entity.IsMandatory = dto.IsMandatory;
        entity.IsBlockingOnFail = dto.IsBlockingOnFail;
        entity.ExpectedDays = dto.ExpectedDays;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // ONBOARDING PLAN TEMPLATE
    // ========================================================================

    #region OnboardingPlanTemplate

    public static OnboardingPlanTemplateDto ToDto(this OnboardingPlanTemplate entity)
    {
        return new OnboardingPlanTemplateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            TaskTemplateCount = entity.TaskTemplates?.Count ?? 0,
        };
    }

    public static OnboardingPlanTemplateSummaryDto ToSummaryDto(this OnboardingPlanTemplate entity)
    {
        return new OnboardingPlanTemplateSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            TaskTemplateCount = entity.TaskTemplates?.Count ?? 0,
        };
    }

    public static OnboardingPlanTemplateDetailDto ToDetailDto(this OnboardingPlanTemplate entity)
    {
        return new OnboardingPlanTemplateDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            TaskTemplateCount = entity.TaskTemplates?.Count ?? 0,
            TaskTemplates = entity.TaskTemplates?.Select(t => t.ToDto()).ToList() ?? new(),
        };
    }

    public static OnboardingPlanTemplate ToEntity(this CreateOnboardingPlanTemplateDto dto, Guid tenantId, Guid userId)
    {
        return new OnboardingPlanTemplate
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            IsDefault = dto.IsDefault,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OnboardingPlanTemplate entity, UpdateOnboardingPlanTemplateDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.IsDefault = dto.IsDefault;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<OnboardingPlanTemplateSummaryDto> ToSummaryDtoList(this IEnumerable<OnboardingPlanTemplate> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // ONBOARDING TASK TEMPLATE
    // ========================================================================

    #region OnboardingTaskTemplate

    public static OnboardingTaskTemplateDto ToDto(this OnboardingTaskTemplate entity)
    {
        return new OnboardingTaskTemplateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PlanTemplateId = entity.PlanTemplateId,
            PlanTemplateName = entity.PlanTemplate?.Name ?? string.Empty,
            TaskName = entity.TaskName,
            Description = entity.Description,
            Category = entity.Category,
            DueDaysFromStartDate = entity.DueDaysFromStartDate,
            IsMandatory = entity.IsMandatory,
            DisplayOrder = entity.DisplayOrder,
            InstructionsUrl = entity.InstructionsUrl,
            OwnerPositionId = entity.OwnerPositionId,
            OwnerPositionTitle = entity.OwnerPosition?.Title,
        };
    }

    public static OnboardingTaskTemplate ToEntity(this CreateOnboardingTaskTemplateDto dto, Guid tenantId, Guid userId)
    {
        return new OnboardingTaskTemplate
        {
            TenantId = tenantId,
            PlanTemplateId = dto.PlanTemplateId,
            TaskName = dto.TaskName,
            Description = dto.Description,
            Category = dto.Category,
            DueDaysFromStartDate = dto.DueDaysFromStartDate,
            IsMandatory = dto.IsMandatory,
            DisplayOrder = dto.DisplayOrder,
            InstructionsUrl = dto.InstructionsUrl,
            OwnerPositionId = dto.OwnerPositionId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OnboardingTaskTemplate entity, UpdateOnboardingTaskTemplateDto dto, Guid userId)
    {
        entity.TaskName = dto.TaskName;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.DueDaysFromStartDate = dto.DueDaysFromStartDate;
        entity.IsMandatory = dto.IsMandatory;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.InstructionsUrl = dto.InstructionsUrl;
        entity.OwnerPositionId = dto.OwnerPositionId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // ONBOARDING PLAN
    // ========================================================================

    #region OnboardingPlan

    public static OnboardingPlanDto ToDto(this OnboardingPlan entity)
    {
        return new OnboardingPlanDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            TemplatePlanId = entity.TemplatePlanId,
            TemplatePlanName = entity.TemplatePlan?.Name,
            Status = entity.Status,
            StartDate = entity.StartDate,
            TargetCompletionDate = entity.TargetCompletionDate,
            ActualCompletionDate = entity.ActualCompletionDate,
            AssignedBuddyId = entity.AssignedBuddyId,
            AssignedBuddyName = entity.AssignedBuddy?.FullName,
            OnboardingCoordinatorId = entity.OnboardingCoordinatorId,
            OnboardingCoordinatorName = entity.OnboardingCoordinator?.FullName,
            Notes = entity.Notes,
            TotalTasks = entity.Tasks?.Count ?? 0,
            CompletedTasks = entity.Tasks?.Count(t => t.Status == OnboardingTaskStatus.Completed) ?? 0,
            OverdueTasks = entity.Tasks?.Count(t => t.Status != OnboardingTaskStatus.Completed && t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow)) ?? 0,
        };
    }

    public static OnboardingPlanSummaryDto ToSummaryDto(this OnboardingPlan entity)
    {
        return new OnboardingPlanSummaryDto
        {
            Id = entity.Id,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Status = entity.Status,
            StartDate = entity.StartDate,
            TargetCompletionDate = entity.TargetCompletionDate,
            TotalTasks = entity.Tasks?.Count ?? 0,
            CompletedTasks = entity.Tasks?.Count(t => t.Status == OnboardingTaskStatus.Completed) ?? 0,
            OverdueTasks = entity.Tasks?.Count(t => t.Status != OnboardingTaskStatus.Completed && t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow)) ?? 0,
        };
    }

    public static OnboardingPlanDetailDto ToDetailDto(this OnboardingPlan entity)
    {
        return new OnboardingPlanDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            TemplatePlanId = entity.TemplatePlanId,
            TemplatePlanName = entity.TemplatePlan?.Name,
            Status = entity.Status,
            StartDate = entity.StartDate,
            TargetCompletionDate = entity.TargetCompletionDate,
            ActualCompletionDate = entity.ActualCompletionDate,
            AssignedBuddyId = entity.AssignedBuddyId,
            AssignedBuddyName = entity.AssignedBuddy?.FullName,
            OnboardingCoordinatorId = entity.OnboardingCoordinatorId,
            OnboardingCoordinatorName = entity.OnboardingCoordinator?.FullName,
            Notes = entity.Notes,
            TotalTasks = entity.Tasks?.Count ?? 0,
            CompletedTasks = entity.Tasks?.Count(t => t.Status == OnboardingTaskStatus.Completed) ?? 0,
            OverdueTasks = entity.Tasks?.Count(t => t.Status != OnboardingTaskStatus.Completed && t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow)) ?? 0,
            Tasks = entity.Tasks?.Select(t => t.ToDto()).ToList() ?? new(),
            Assets = entity.Assets.Select(a => a.ToDto()).ToList(),
        };
    }

    public static OnboardingPlan ToEntity(this CreateOnboardingPlanDto dto, Guid tenantId, Guid userId)
    {
        return new OnboardingPlan
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            TemplatePlanId = dto.TemplatePlanId,
            Status = OnboardingStatus.NotStarted,
            StartDate = dto.StartDate,
            TargetCompletionDate = dto.TargetCompletionDate,
            AssignedBuddyId = dto.AssignedBuddyId,
            OnboardingCoordinatorId = dto.OnboardingCoordinatorId,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OnboardingPlan entity, UpdateOnboardingPlanDto dto, Guid userId)
    {
        entity.Status = dto.Status;
        entity.TargetCompletionDate = dto.TargetCompletionDate;
        entity.ActualCompletionDate = dto.ActualCompletionDate;
        entity.AssignedBuddyId = dto.AssignedBuddyId;
        entity.OnboardingCoordinatorId = dto.OnboardingCoordinatorId;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<OnboardingPlanSummaryDto> ToSummaryDtoList(this IEnumerable<OnboardingPlan> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // ONBOARDING TASK
    // ========================================================================

    #region OnboardingTask

    public static OnboardingTaskDto ToDto(this OnboardingTask entity)
    {
        return new OnboardingTaskDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            OnboardingPlanId = entity.OnboardingPlanId,
            TaskTemplateId = entity.TaskTemplateId,
            TaskName = entity.TaskName,
            Description = entity.Description,
            Category = entity.Category,
            Status = entity.Status,
            DueDate = entity.DueDate,
            CompletedDate = entity.CompletedDate,
            IsMandatory = entity.IsMandatory,
            AssignedToId = entity.AssignedToId,
            AssignedToName = entity.AssignedTo?.FullName,
            AssignedOrganizationUnitId = entity.AssignedOrganizationUnitId,
            AssignedOrganizationUnitName = entity.AssignedOrganizationUnit?.Name,
            OwnerPositionId = entity.OwnerPositionId,
            OwnerPositionTitle = entity.OwnerPosition?.Title,
            CompletedById = entity.CompletedById,
            CompletedByName = entity.CompletedBy?.FullName,
            CompletionNotes = entity.CompletionNotes,
            EvidenceFilePath = entity.EvidenceFilePath,
            RequiresVerification = entity.RequiresVerification,
            VerifiedById = entity.VerifiedById,
            VerifiedByName = entity.VerifiedBy?.FullName,
            VerifiedDate = entity.VerifiedDate,
            DisplayOrder = entity.DisplayOrder,
        };
    }

    public static OnboardingTaskSummaryDto ToSummaryDto(this OnboardingTask entity)
    {
        return new OnboardingTaskSummaryDto
        {
            Id = entity.Id,
            TaskName = entity.TaskName,
            Category = entity.Category,
            Status = entity.Status,
            DueDate = entity.DueDate,
            IsMandatory = entity.IsMandatory,
            AssignedToName = entity.AssignedTo?.FullName,
        };
    }

    public static OnboardingTaskDetailDto ToDetailDto(this OnboardingTask entity)
    {
        return new OnboardingTaskDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            OnboardingPlanId = entity.OnboardingPlanId,
            TaskTemplateId = entity.TaskTemplateId,
            TaskName = entity.TaskName,
            Description = entity.Description,
            Category = entity.Category,
            Status = entity.Status,
            DueDate = entity.DueDate,
            CompletedDate = entity.CompletedDate,
            IsMandatory = entity.IsMandatory,
            AssignedToId = entity.AssignedToId,
            AssignedToName = entity.AssignedTo?.FullName,
            AssignedOrganizationUnitId = entity.AssignedOrganizationUnitId,
            AssignedOrganizationUnitName = entity.AssignedOrganizationUnit?.Name,
            OwnerPositionId = entity.OwnerPositionId,
            OwnerPositionTitle = entity.OwnerPosition?.Title,
            CompletedById = entity.CompletedById,
            CompletedByName = entity.CompletedBy?.FullName,
            CompletionNotes = entity.CompletionNotes,
            EvidenceFilePath = entity.EvidenceFilePath,
            RequiresVerification = entity.RequiresVerification,
            VerifiedById = entity.VerifiedById,
            VerifiedByName = entity.VerifiedBy?.FullName,
            VerifiedDate = entity.VerifiedDate,
            DisplayOrder = entity.DisplayOrder,
            Comments = entity.Comments.Select(c => c.ToDto()).ToList(),
        };
    }

    public static OnboardingTask ToEntity(this CreateOnboardingTaskDto dto, Guid tenantId, Guid userId)
    {
        return new OnboardingTask
        {
            TenantId = tenantId,
            OnboardingPlanId = dto.OnboardingPlanId,
            TaskTemplateId = dto.TaskTemplateId,
            TaskName = dto.TaskName,
            Description = dto.Description,
            Category = dto.Category,
            Status = OnboardingTaskStatus.Pending,
            DueDate = dto.DueDate,
            IsMandatory = dto.IsMandatory,
            AssignedToId = dto.AssignedToId,
            AssignedOrganizationUnitId = dto.AssignedOrganizationUnitId,
            RequiresVerification = dto.RequiresVerification,
            DisplayOrder = dto.DisplayOrder,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OnboardingTask entity, UpdateOnboardingTaskDto dto, Guid userId)
    {
        entity.TaskName = dto.TaskName;
        entity.Description = dto.Description;
        entity.DueDate = dto.DueDate;
        entity.AssignedToId = dto.AssignedToId;
        entity.AssignedOrganizationUnitId = dto.AssignedOrganizationUnitId;
        entity.RequiresVerification = dto.RequiresVerification;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<OnboardingTaskSummaryDto> ToSummaryDtoList(this IEnumerable<OnboardingTask> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // ONBOARDING TASK COMMENT
    // ========================================================================

    #region OnboardingTaskComment

    public static OnboardingTaskCommentDto ToDto(this OnboardingTaskComment entity)
    {
        return new OnboardingTaskCommentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TaskId = entity.TaskId,
            Comment = entity.Comment,
            AuthorId = entity.AuthorId,
            AuthorName = entity.Author?.FullName ?? string.Empty,
            CommentDate = entity.CommentDate,
        };
    }

    public static OnboardingTaskComment ToEntity(this CreateOnboardingTaskCommentDto dto, Guid tenantId, Guid userId)
    {
        return new OnboardingTaskComment
        {
            TenantId = tenantId,
            TaskId = dto.TaskId,
            Comment = dto.Comment,
            AuthorId = userId,
            CommentDate = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // ONBOARDING ASSET
    // ========================================================================

    #region OnboardingAsset

    public static OnboardingAssetDto ToDto(this OnboardingAsset entity)
    {
        return new OnboardingAssetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            OnboardingPlanId = entity.OnboardingPlanId,
            AssetType = entity.AssetType,
            AssetName = entity.AssetName,
            Description = entity.Description,
            AssetTag = entity.AssetTag,
            SerialNumber = entity.SerialNumber,
            Status = entity.Status,
            RequiredByDate = entity.RequiredByDate,
            ProvisionedDate = entity.ProvisionedDate,
            ProvisionedById = entity.ProvisionedById,
            ProvisionedByName = entity.ProvisionedBy?.FullName,
            IssuedToEmployeeDate = entity.IssuedToEmployeeDate,
            AcknowledgedByEmployee = entity.AcknowledgedByEmployee,
            AcknowledgementDate = entity.AcknowledgementDate,
            AcknowledgementDocumentPath = entity.AcknowledgementDocumentPath,
            Notes = entity.Notes,
        };
    }

    public static OnboardingAsset ToEntity(this CreateOnboardingAssetDto dto, Guid tenantId, Guid userId)
    {
        return new OnboardingAsset
        {
            TenantId = tenantId,
            OnboardingPlanId = dto.OnboardingPlanId,
            AssetType = dto.AssetType,
            AssetName = dto.AssetName,
            Description = dto.Description,
            AssetTag = dto.AssetTag,
            SerialNumber = dto.SerialNumber,
            Status = OnboardingAssetProvisionStatus.Pending,
            RequiredByDate = dto.RequiredByDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OnboardingAsset entity, UpdateOnboardingAssetDto dto, Guid userId)
    {
        entity.Status = dto.Status;
        entity.AssetTag = dto.AssetTag;
        entity.SerialNumber = dto.SerialNumber;
        entity.RequiredByDate = dto.RequiredByDate;
        entity.ProvisionedDate = dto.ProvisionedDate;
        entity.ProvisionedById = dto.ProvisionedById;
        entity.IssuedToEmployeeDate = dto.IssuedToEmployeeDate;
        entity.AcknowledgedByEmployee = dto.AcknowledgedByEmployee;
        entity.AcknowledgementDate = dto.AcknowledgementDate;
        entity.AcknowledgementDocumentPath = dto.AcknowledgementDocumentPath;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // PROBATION PERIOD
    // ========================================================================

    #region ProbationPeriod

    public static ProbationPeriodDto ToDto(this ProbationPeriod entity)
    {
        return new ProbationPeriodDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ContractDetailId = entity.ContractDetailId,
            StartDate = entity.StartDate,
            OriginalEndDate = entity.OriginalEndDate,
            CurrentEndDate = entity.CurrentEndDate,
            DurationMonths = entity.DurationMonths,
            Status = entity.Status,
            OutcomeNotes = entity.OutcomeNotes,
            ExtensionCount = entity.ExtensionCount,
            ReviewCount = entity.Reviews?.Count ?? 0,
        };
    }

    public static ProbationPeriodSummaryDto ToSummaryDto(this ProbationPeriod entity)
    {
        return new ProbationPeriodSummaryDto
        {
            Id = entity.Id,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            StartDate = entity.StartDate,
            CurrentEndDate = entity.CurrentEndDate,
            DurationMonths = entity.DurationMonths,
            Status = entity.Status,
            ExtensionCount = entity.ExtensionCount,
            ReviewCount = entity.Reviews?.Count ?? 0,
        };
    }

    public static ProbationPeriodDetailDto ToDetailDto(this ProbationPeriod entity)
    {
        return new ProbationPeriodDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ContractDetailId = entity.ContractDetailId,
            StartDate = entity.StartDate,
            OriginalEndDate = entity.OriginalEndDate,
            CurrentEndDate = entity.CurrentEndDate,
            DurationMonths = entity.DurationMonths,
            Status = entity.Status,
            OutcomeNotes = entity.OutcomeNotes,
            ExtensionCount = entity.ExtensionCount,
            ReviewCount = entity.Reviews?.Count ?? 0,
            Reviews = entity.Reviews?.Select(r => r.ToSummaryDto()).ToList() ?? new(),
        };
    }

    /// <summary>
    /// Builds the probation row. <paramref name="durationMonths"/> is resolved by the service from
    /// the employee's staff category (FR-HR-031), so it is passed in rather than read off the DTO,
    /// which may legitimately omit it.
    /// </summary>
    public static ProbationPeriod ToEntity(this CreateProbationPeriodDto dto, Guid tenantId, Guid userId, int durationMonths)
    {
        return new ProbationPeriod
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            ContractDetailId = dto.ContractDetailId,
            StartDate = dto.StartDate,
            OriginalEndDate = dto.StartDate.AddMonths(durationMonths),
            CurrentEndDate = dto.StartDate.AddMonths(durationMonths),
            DurationMonths = durationMonths,
            Status = ProbationStatus.Active,
            OutcomeNotes = dto.OutcomeNotes,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<ProbationPeriodSummaryDto> ToSummaryDtoList(this IEnumerable<ProbationPeriod> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // PROBATION REVIEW
    // ========================================================================

    #region ProbationReview

    public static ProbationReviewDto ToDto(this ProbationReview entity)
    {
        return new ProbationReviewDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProbationPeriodId = entity.ProbationPeriodId,
            EmployeeName = entity.ProbationPeriod?.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.ProbationPeriod?.Employee?.EmployeeNumber ?? string.Empty,
            ReviewNumber = entity.ReviewNumber,
            ScheduledDate = entity.ScheduledDate,
            ActualDate = entity.ActualDate,
            Status = entity.Status,
            PerformanceRating = entity.PerformanceRating,
            ConductRating = entity.ConductRating,
            AttitudeRating = entity.AttitudeRating,
            StrengthsObserved = entity.StrengthsObserved,
            AreasForImprovement = entity.AreasForImprovement,
            ReviewerComments = entity.ReviewerComments,
            EmployeeResponse = entity.EmployeeResponse,
            Recommendation = entity.Recommendation,
            ProposedExtensionMonths = entity.ProposedExtensionMonths,
            ReviewedById = entity.ReviewedById,
            ReviewedByName = entity.ReviewedBy?.FullName ?? string.Empty,
            SecondReviewerId = entity.SecondReviewerId,
            SecondReviewerName = entity.SecondReviewer?.FullName,
            EmployeeAcknowledged = entity.EmployeeAcknowledged,
            EmployeeAcknowledgementDate = entity.EmployeeAcknowledgementDate,
            HrApproved = entity.HrApproved,
            HrApprovedById = entity.HrApprovedById,
            HrApprovedByName = entity.HrApprovedBy?.FullName,
            HrApprovalDate = entity.HrApprovalDate,
            SignedDocumentPath = entity.SignedDocumentPath,
        };
    }

    public static ProbationReviewSummaryDto ToSummaryDto(this ProbationReview entity)
    {
        return new ProbationReviewSummaryDto
        {
            Id = entity.Id,
            ReviewNumber = entity.ReviewNumber,
            ScheduledDate = entity.ScheduledDate,
            ActualDate = entity.ActualDate,
            Status = entity.Status,
            Recommendation = entity.Recommendation,
            ReviewedByName = entity.ReviewedBy?.FullName ?? string.Empty,
            HrApproved = entity.HrApproved,
        };
    }

    public static ProbationReview ToEntity(this CreateProbationReviewDto dto, Guid tenantId, Guid userId)
    {
        return new ProbationReview
        {
            TenantId = tenantId,
            ProbationPeriodId = dto.ProbationPeriodId,
            ReviewNumber = dto.ReviewNumber,
            ScheduledDate = dto.ScheduledDate,
            Status = ProbationReviewStatus.Scheduled,
            ReviewedById = dto.ReviewedById,
            SecondReviewerId = dto.SecondReviewerId,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<ProbationReviewSummaryDto> ToSummaryDtoList(this IEnumerable<ProbationReview> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion
}

