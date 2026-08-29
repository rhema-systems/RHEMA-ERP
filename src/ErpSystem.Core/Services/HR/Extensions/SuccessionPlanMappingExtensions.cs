using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.HR;

namespace ErpSystem.Application.HR.Extensions;

public static class SuccessionPlanMappingExtensions
{
    // ========================================================================
    // SUCCESSION PLAN
    // ========================================================================

    #region SuccessionPlan

    public static SuccessionPlanDto ToDto(this SuccessionPlan entity)
    {
        return new SuccessionPlanDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PlanNumber = entity.PlanNumber,
            PlanName = entity.PlanName,
            Description = entity.Description,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            CurrentIncumbentId = entity.CurrentIncumbentId,
            CurrentIncumbentName = entity.CurrentIncumbent?.FullName,
            CurrentIncumbentNumber = entity.CurrentIncumbent?.EmployeeNumber,
            PlanYear = entity.PlanYear,
            VersionNumber = entity.VersionNumber,
            IsActiveVersion = entity.IsActiveVersion,
            SupersededByPlanId = entity.SupersededByPlanId,
            SupersededByPlanNumber = entity.SupersededByPlan?.PlanNumber,
            Status = entity.Status,
            Criticality = entity.Criticality,
            RiskLevel = entity.RiskLevel,
            RiskAssessmentNotes = entity.RiskAssessmentNotes,
            BusinessImpactIfVacant = entity.BusinessImpactIfVacant,
            IncumbentRetirementDate = entity.IncumbentRetirementDate,
            AnticipatedVacancyDate = entity.AnticipatedVacancyDate,
            AnticipatedVacancyReason = entity.AnticipatedVacancyReason,
            IncumbentSuccessionNotes = entity.IncumbentSuccessionNotes,
            HasReadyNowSuccessor = entity.HasReadyNowSuccessor,
            NumberOfIdentifiedSuccessors = entity.NumberOfIdentifiedSuccessors,
            HasEmergencySuccessor = entity.HasEmergencySuccessor,
            EmergencySuccessorId = entity.EmergencySuccessorId,
            EmergencySuccessorName = entity.EmergencySuccessor?.FullName,
            EmergencyProtocol = entity.EmergencyProtocol,
            TargetSuccessionDate = entity.TargetSuccessionDate,
            EstimatedTimeToReadyMonths = entity.EstimatedTimeToReadyMonths,
            ReviewedById = entity.ReviewedById,
            ReviewedByName = entity.ReviewedBy?.FullName,
            ReviewDate = entity.ReviewDate,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            ReviewFrequencyMonths = entity.ReviewFrequencyMonths,
            NextReviewDate = entity.NextReviewDate,
            CompetencyRequirements = entity.CompetencyRequirements.Select(r => r.ToDto()).ToList(),
            Candidates = entity.Candidates.Select(c => c.ToSummaryDto()).ToList(),
            Actions = entity.Actions.Select(a => a.ToSummaryDto()).ToList(),
            Documents = entity.Documents.Select(d => d.ToDto()).ToList(),
        };
    }

    public static SuccessionPlanSummaryDto ToSummaryDto(this SuccessionPlan entity)
    {
        return new SuccessionPlanSummaryDto
        {
            Id = entity.Id,
            PlanNumber = entity.PlanNumber,
            PlanName = entity.PlanName,
            PlanYear = entity.PlanYear,
            VersionNumber = entity.VersionNumber,
            IsActiveVersion = entity.IsActiveVersion,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            CurrentIncumbentName = entity.CurrentIncumbent?.FullName,
            Status = entity.Status,
            Criticality = entity.Criticality,
            RiskLevel = entity.RiskLevel,
            HasReadyNowSuccessor = entity.HasReadyNowSuccessor,
            NumberOfIdentifiedSuccessors = entity.NumberOfIdentifiedSuccessors,
            HasEmergencySuccessor = entity.HasEmergencySuccessor,
            NextReviewDate = entity.NextReviewDate,
        };
    }

    public static SuccessionPlan ToEntity(this CreateSuccessionPlanDto dto, Guid tenantId, Guid userId)
    {
        return new SuccessionPlan
        {
            TenantId = tenantId,
            PlanName = dto.PlanName,
            Description = dto.Description,
            PositionId = dto.PositionId,
            CurrentIncumbentId = dto.CurrentIncumbentId,
            PlanYear = dto.PlanYear,
            VersionNumber = 1,
            // A draft holds no active-version slot; approval raises it. The service sets this
            // explicitly too — see SuccessionPlanService.CreateAsync and section 3.9 of the plan.
            IsActiveVersion = false,
            Status = SuccessionPlanStatus.Draft,
            Criticality = dto.Criticality,
            RiskLevel = dto.RiskLevel,
            RiskAssessmentNotes = dto.RiskAssessmentNotes,
            BusinessImpactIfVacant = dto.BusinessImpactIfVacant,
            IncumbentRetirementDate = dto.IncumbentRetirementDate,
            AnticipatedVacancyDate = dto.AnticipatedVacancyDate,
            AnticipatedVacancyReason = dto.AnticipatedVacancyReason,
            IncumbentSuccessionNotes = dto.IncumbentSuccessionNotes,
            EmergencySuccessorId = dto.EmergencySuccessorId,
            EmergencyProtocol = dto.EmergencyProtocol,
            TargetSuccessionDate = dto.TargetSuccessionDate,
            EstimatedTimeToReadyMonths = dto.EstimatedTimeToReadyMonths,
            ReviewFrequencyMonths = dto.ReviewFrequencyMonths,
            NextReviewDate = dto.NextReviewDate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SuccessionPlan entity, UpdateSuccessionPlanDto dto, Guid userId)
    {
        entity.PlanName = dto.PlanName;
        entity.Description = dto.Description;
        entity.CurrentIncumbentId = dto.CurrentIncumbentId;
        entity.Criticality = dto.Criticality;
        entity.RiskLevel = dto.RiskLevel;
        entity.RiskAssessmentNotes = dto.RiskAssessmentNotes;
        entity.BusinessImpactIfVacant = dto.BusinessImpactIfVacant;
        entity.IncumbentRetirementDate = dto.IncumbentRetirementDate;
        entity.AnticipatedVacancyDate = dto.AnticipatedVacancyDate;
        entity.AnticipatedVacancyReason = dto.AnticipatedVacancyReason;
        entity.IncumbentSuccessionNotes = dto.IncumbentSuccessionNotes;
        entity.EmergencySuccessorId = dto.EmergencySuccessorId;
        entity.EmergencyProtocol = dto.EmergencyProtocol;
        entity.TargetSuccessionDate = dto.TargetSuccessionDate;
        entity.EstimatedTimeToReadyMonths = dto.EstimatedTimeToReadyMonths;
        entity.ReviewFrequencyMonths = dto.ReviewFrequencyMonths;
        entity.NextReviewDate = dto.NextReviewDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SuccessionPlanSummaryDto> ToSummaryDtoList(this IEnumerable<SuccessionPlan> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // SUCCESSION COMPETENCY REQUIREMENT
    // ========================================================================

    #region SuccessionCompetencyRequirement

    public static SuccessionCompetencyRequirementDto ToDto(this SuccessionCompetencyRequirement entity)
    {
        return new SuccessionCompetencyRequirementDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SuccessionPlanId = entity.SuccessionPlanId,
            CompetencyId = entity.CompetencyId,
            CompetencyCode = entity.Competency?.Code ?? string.Empty,
            CompetencyName = entity.Competency?.Name ?? string.Empty,
            CompetencyCategory = entity.Competency?.CompetencyCategory ?? default,
            RequiredLevel = entity.RequiredLevel,
            ProficiencyScaleMax = entity.Competency?.ProficiencyScaleMax ?? 5,
        };
    }

    public static SuccessionCompetencyRequirement ToEntity(this CreateSuccessionCompetencyRequirementDto dto, Guid tenantId, Guid userId)
    {
        return new SuccessionCompetencyRequirement
        {
            TenantId = tenantId,
            SuccessionPlanId = dto.SuccessionPlanId,
            CompetencyId = dto.CompetencyId,
            RequiredLevel = dto.RequiredLevel,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SuccessionCompetencyRequirement entity, UpdateSuccessionCompetencyRequirementDto dto, Guid userId)
    {
        entity.RequiredLevel = dto.RequiredLevel;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // SUCCESSION CANDIDATE
    // ========================================================================

    #region SuccessionCandidate

    public static SuccessionCandidateDto ToDto(this SuccessionCandidate entity)
    {
        return new SuccessionCandidateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SuccessionPlanId = entity.SuccessionPlanId,
            PlanNumber = entity.SuccessionPlan?.PlanNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            DateOfBirth = entity.Employee?.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
            Age = HrPolicyCalculations.Age(entity.Employee?.DateOfBirth),
            YearsOfService = entity.Employee?.YearsOfService,
            // RetirementDate / ServiceYearsLeft need HR policy settings — enriched in the service layer.
            TalentPoolMemberId = entity.TalentPoolMemberId,
            TalentPoolName = entity.TalentPoolMember?.TalentPool?.Name,
            Type = entity.Type,
            Rank = entity.Rank,
            CurrentReadiness = entity.CurrentReadiness,
            ReadyByDate = entity.ReadyByDate,
            MonthsToReady = entity.MonthsToReady,
            IsEmergencyOnly = entity.IsEmergencyOnly,
            LatestPerformanceRating = entity.LatestPerformanceRating,
            PotentialRating = entity.PotentialRating,
            TalentReviewRatingId = entity.TalentReviewRatingId,
            Strengths = entity.Strengths,
            DevelopmentGaps = entity.DevelopmentGaps,
            DevelopmentPlan = entity.DevelopmentPlan,
            YearsInCurrentRole = entity.YearsInCurrentRole,
            YearsWithCompany = entity.YearsWithCompany,
            HasRelevantExperience = entity.HasRelevantExperience,
            RelevantExperienceDetails = entity.RelevantExperienceDetails,
            WillingToRelocate = entity.WillingToRelocate,
            AvailableForPromotion = entity.AvailableForPromotion,
            AvailableFrom = entity.AvailableFrom,
            RetentionRisk = entity.RetentionRisk,
            RiskMitigationPlan = entity.RiskMitigationPlan,
            AssessedById = entity.AssessedById,
            AssessedByName = entity.AssessedBy?.FullName,
            AssessmentDate = entity.AssessmentDate,
            AssessmentNotes = entity.AssessmentNotes,
            IsRecommended = entity.IsRecommended,
            RecommendationNotes = entity.RecommendationNotes,
            RecommendationDate = entity.RecommendationDate,
            RecommendedById = entity.RecommendedById,
            RecommendedByName = entity.RecommendedBy?.FullName,
            IsSelected = entity.IsSelected,
            SelectionDate = entity.SelectionDate,
            SuccessionCompleted = entity.SuccessionCompleted,
            SuccessionDate = entity.SuccessionDate,
            DevelopmentActivities = entity.DevelopmentActivities.Select(a => a.ToSummaryDto()).ToList(),
            CompetencyGaps = entity.CompetencyGaps.Select(g => g.ToDto()).ToList(),
            Feedback = entity.Feedback.OrderByDescending(f => f.CreatedAt).Select(f => f.ToDto()).ToList(),
        };
    }

    public static SuccessionCandidateFeedbackDto ToDto(this SuccessionCandidateFeedback entity)
    {
        return new SuccessionCandidateFeedbackDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            CandidateId = entity.CandidateId,
            ReviewerId = entity.ReviewerId,
            ReviewerName = entity.Reviewer?.FullName ?? string.Empty,
            Note = entity.Note,
            Disposition = entity.Disposition,
        };
    }

    public static SuccessionCandidateSummaryDto ToSummaryDto(this SuccessionCandidate entity)
    {
        return new SuccessionCandidateSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Age = HrPolicyCalculations.Age(entity.Employee?.DateOfBirth),
            // ServiceYearsLeft needs HR policy settings — enriched in the service layer.
            Type = entity.Type,
            Rank = entity.Rank,
            CurrentReadiness = entity.CurrentReadiness,
            IsEmergencyOnly = entity.IsEmergencyOnly,
            FeedbackCount = entity.Feedback.Count,
            SupportCount = entity.Feedback.Count(f => f.Disposition == FeedbackDisposition.Support),
            OpposeCount = entity.Feedback.Count(f => f.Disposition == FeedbackDisposition.Oppose),
            LatestPerformanceRating = entity.LatestPerformanceRating,
            PotentialRating = entity.PotentialRating,
            RetentionRisk = entity.RetentionRisk,
            IsRecommended = entity.IsRecommended,
            IsSelected = entity.IsSelected,
            SuccessionCompleted = entity.SuccessionCompleted,
        };
    }

    public static SuccessionCandidate ToEntity(this CreateSuccessionCandidateDto dto, Guid tenantId, Guid userId)
    {
        return new SuccessionCandidate
        {
            TenantId = tenantId,
            SuccessionPlanId = dto.SuccessionPlanId,
            EmployeeId = dto.EmployeeId,
            TalentPoolMemberId = dto.TalentPoolMemberId,
            Type = dto.Type,
            Rank = dto.Rank,
            CurrentReadiness = dto.CurrentReadiness,
            ReadyByDate = dto.ReadyByDate,
            MonthsToReady = dto.MonthsToReady,
            IsEmergencyOnly = dto.IsEmergencyOnly,
            LatestPerformanceRating = dto.LatestPerformanceRating,
            PotentialRating = dto.PotentialRating,
            TalentReviewRatingId = dto.TalentReviewRatingId,
            Strengths = dto.Strengths,
            DevelopmentGaps = dto.DevelopmentGaps,
            DevelopmentPlan = dto.DevelopmentPlan,
            YearsInCurrentRole = dto.YearsInCurrentRole,
            YearsWithCompany = dto.YearsWithCompany,
            HasRelevantExperience = dto.HasRelevantExperience,
            RelevantExperienceDetails = dto.RelevantExperienceDetails,
            WillingToRelocate = dto.WillingToRelocate,
            AvailableForPromotion = dto.AvailableForPromotion,
            AvailableFrom = dto.AvailableFrom,
            RetentionRisk = dto.RetentionRisk,
            RiskMitigationPlan = dto.RiskMitigationPlan,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SuccessionCandidate entity, UpdateSuccessionCandidateDto dto, Guid userId)
    {
        entity.Type = dto.Type;
        entity.Rank = dto.Rank;
        entity.CurrentReadiness = dto.CurrentReadiness;
        entity.ReadyByDate = dto.ReadyByDate;
        entity.MonthsToReady = dto.MonthsToReady;
        entity.IsEmergencyOnly = dto.IsEmergencyOnly;
        entity.LatestPerformanceRating = dto.LatestPerformanceRating;
        entity.PotentialRating = dto.PotentialRating;
        entity.TalentReviewRatingId = dto.TalentReviewRatingId;
        entity.Strengths = dto.Strengths;
        entity.DevelopmentGaps = dto.DevelopmentGaps;
        entity.DevelopmentPlan = dto.DevelopmentPlan;
        entity.YearsInCurrentRole = dto.YearsInCurrentRole;
        entity.YearsWithCompany = dto.YearsWithCompany;
        entity.HasRelevantExperience = dto.HasRelevantExperience;
        entity.RelevantExperienceDetails = dto.RelevantExperienceDetails;
        entity.WillingToRelocate = dto.WillingToRelocate;
        entity.AvailableForPromotion = dto.AvailableForPromotion;
        entity.AvailableFrom = dto.AvailableFrom;
        entity.RetentionRisk = dto.RetentionRisk;
        entity.RiskMitigationPlan = dto.RiskMitigationPlan;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SuccessionCandidateSummaryDto> ToSummaryDtoList(this IEnumerable<SuccessionCandidate> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // SUCCESSION CANDIDATE GAP
    // ========================================================================

    #region SuccessionCandidateGap

    public static SuccessionCandidateGapDto ToDto(this SuccessionCandidateGap entity)
    {
        return new SuccessionCandidateGapDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CandidateId = entity.CandidateId,
            CompetencyId = entity.CompetencyId,
            CompetencyCode = entity.Competency?.Code ?? string.Empty,
            CompetencyName = entity.Competency?.Name ?? string.Empty,
            CompetencyCategory = entity.Competency?.CompetencyCategory ?? default,
            RequiredLevel = entity.RequiredLevel,
            CurrentLevel = entity.CurrentLevel,
            GapSize = entity.GapSize,
            Status = entity.Status,
            GapNotes = entity.GapNotes,
            Addressed = entity.Addressed,
            AddressedDate = entity.AddressedDate,
            AddressedByActivityId = entity.AddressedByActivityId,
            AddressedByActivityName = entity.AddressedByActivity?.ActivityName,
        };
    }

    public static SuccessionCandidateGap ToEntity(this CreateSuccessionCandidateGapDto dto, Guid tenantId, Guid userId)
    {
        return new SuccessionCandidateGap
        {
            TenantId = tenantId,
            CandidateId = dto.CandidateId,
            CompetencyId = dto.CompetencyId,
            RequiredLevel = dto.RequiredLevel,
            CurrentLevel = dto.CurrentLevel,
            GapNotes = dto.GapNotes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SuccessionCandidateGap entity, UpdateSuccessionCandidateGapDto dto, Guid userId)
    {
        entity.CurrentLevel = dto.CurrentLevel;
        entity.GapNotes = dto.GapNotes;
        entity.Addressed = dto.Addressed;
        entity.AddressedDate = dto.AddressedDate;
        entity.AddressedByActivityId = dto.AddressedByActivityId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // SUCCESSION DEVELOPMENT ACTIVITY
    // ========================================================================

    #region SuccessionDevelopmentActivity

    public static SuccessionDevelopmentActivityDto ToDto(this SuccessionDevelopmentActivity entity)
    {
        return new SuccessionDevelopmentActivityDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CandidateId = entity.CandidateId,
            CandidateEmployeeName = entity.Candidate?.Employee?.FullName,
            TalentPoolMemberId = entity.TalentPoolMemberId,
            TalentPoolMemberName = entity.TalentPoolMember?.Employee?.FullName,
            ActivityName = entity.ActivityName,
            Type = entity.Type,
            Description = entity.Description,
            PlannedStartDate = entity.PlannedStartDate,
            PlannedEndDate = entity.PlannedEndDate,
            ActualStartDate = entity.ActualStartDate,
            ActualEndDate = entity.ActualEndDate,
            Status = entity.Status,
            Outcome = entity.Outcome,
            CompetencyGained = entity.CompetencyGained,
            EstimatedCost = entity.EstimatedCost,
            ActualCost = entity.ActualCost,
            CurrencyCode = entity.CurrencyCode,
            SupervisorId = entity.SupervisorId,
            SupervisorName = entity.Supervisor?.FullName,
            ExternalProviderContactId = entity.ExternalProviderContactId,
            ExternalProviderName = entity.ExternalProviderName,
            Notes = entity.Notes,
            Milestones = entity.Milestones.Select(m => m.ToDto()).ToList(),
            AddressedGaps = entity.AddressedGaps.Select(g => g.ToDto()).ToList(),
        };
    }

    public static SuccessionDevelopmentActivitySummaryDto ToSummaryDto(this SuccessionDevelopmentActivity entity)
    {
        return new SuccessionDevelopmentActivitySummaryDto
        {
            Id = entity.Id,
            ActivityName = entity.ActivityName,
            Type = entity.Type,
            Status = entity.Status,
            PlannedStartDate = entity.PlannedStartDate,
            PlannedEndDate = entity.PlannedEndDate,
            CompetencyGained = entity.CompetencyGained,
        };
    }

    public static SuccessionDevelopmentActivity ToEntity(this CreateSuccessionDevelopmentActivityDto dto, Guid tenantId, Guid userId)
    {
        return new SuccessionDevelopmentActivity
        {
            TenantId = tenantId,
            CandidateId = dto.CandidateId,
            TalentPoolMemberId = dto.TalentPoolMemberId,
            ActivityName = dto.ActivityName,
            Type = dto.Type,
            Description = dto.Description,
            PlannedStartDate = dto.PlannedStartDate,
            PlannedEndDate = dto.PlannedEndDate,
            Status = dto.Status,
            EstimatedCost = dto.EstimatedCost,
            CurrencyCode = dto.CurrencyCode,
            SupervisorId = dto.SupervisorId,
            ExternalProviderContactId = dto.ExternalProviderContactId,
            ExternalProviderName = dto.ExternalProviderName,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SuccessionDevelopmentActivity entity, UpdateSuccessionDevelopmentActivityDto dto, Guid userId)
    {
        entity.ActivityName = dto.ActivityName;
        entity.Type = dto.Type;
        entity.Description = dto.Description;
        entity.PlannedStartDate = dto.PlannedStartDate;
        entity.PlannedEndDate = dto.PlannedEndDate;
        entity.ActualStartDate = dto.ActualStartDate;
        entity.ActualEndDate = dto.ActualEndDate;
        entity.Status = dto.Status;
        entity.Outcome = dto.Outcome;
        entity.CompetencyGained = dto.CompetencyGained;
        entity.EstimatedCost = dto.EstimatedCost;
        entity.ActualCost = dto.ActualCost;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.SupervisorId = dto.SupervisorId;
        entity.ExternalProviderContactId = dto.ExternalProviderContactId;
        entity.ExternalProviderName = dto.ExternalProviderName;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SuccessionDevelopmentActivitySummaryDto> ToSummaryDtoList(this IEnumerable<SuccessionDevelopmentActivity> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // SUCCESSION DEVELOPMENT MILESTONE
    // ========================================================================

    #region SuccessionDevelopmentMilestone

    public static SuccessionDevelopmentMilestoneDto ToDto(this SuccessionDevelopmentMilestone entity)
    {
        return new SuccessionDevelopmentMilestoneDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ActivityId = entity.ActivityId,
            MilestoneName = entity.MilestoneName,
            TargetDate = entity.TargetDate,
            CompletedDate = entity.CompletedDate,
            IsCompleted = entity.IsCompleted,
            Notes = entity.Notes,
        };
    }

    public static SuccessionDevelopmentMilestone ToEntity(this CreateSuccessionDevelopmentMilestoneDto dto, Guid tenantId, Guid userId)
    {
        return new SuccessionDevelopmentMilestone
        {
            TenantId = tenantId,
            ActivityId = dto.ActivityId,
            MilestoneName = dto.MilestoneName,
            TargetDate = dto.TargetDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SuccessionDevelopmentMilestone entity, UpdateSuccessionDevelopmentMilestoneDto dto, Guid userId)
    {
        entity.MilestoneName = dto.MilestoneName;
        entity.TargetDate = dto.TargetDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.IsCompleted = dto.IsCompleted;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // SUCCESSION ACTION
    // ========================================================================

    #region SuccessionAction

    public static SuccessionActionDto ToDto(this SuccessionAction entity)
    {
        return new SuccessionActionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SuccessionPlanId = entity.SuccessionPlanId,
            PlanNumber = entity.SuccessionPlan?.PlanNumber ?? string.Empty,
            CandidateId = entity.CandidateId,
            CandidateEmployeeName = entity.Candidate?.Employee?.FullName,
            ActionDescription = entity.ActionDescription,
            Type = entity.Type,
            Priority = entity.Priority,
            ResponsiblePersonId = entity.ResponsiblePersonId,
            ResponsiblePersonName = entity.ResponsiblePerson?.FullName,
            AssignedById = entity.AssignedById,
            AssignedByName = entity.AssignedBy?.FullName,
            DueDate = entity.DueDate,
            StartedDate = entity.StartedDate,
            Status = entity.Status,
            CompletionDate = entity.CompletionDate,
            CompletionNotes = entity.CompletionNotes,
            DependsOnActionId = entity.DependsOnActionId,
            DependsOnActionDescription = entity.DependsOnAction?.ActionDescription,
            WasSuccessful = entity.WasSuccessful,
            OutcomeNotes = entity.OutcomeNotes,
        };
    }

    public static SuccessionActionSummaryDto ToSummaryDto(this SuccessionAction entity)
    {
        return new SuccessionActionSummaryDto
        {
            Id = entity.Id,
            ActionDescription = entity.ActionDescription,
            Type = entity.Type,
            Priority = entity.Priority,
            Status = entity.Status,
            DueDate = entity.DueDate,
            ResponsiblePersonName = entity.ResponsiblePerson?.FullName,
        };
    }

    public static SuccessionAction ToEntity(this CreateSuccessionActionDto dto, Guid tenantId, Guid userId)
    {
        return new SuccessionAction
        {
            TenantId = tenantId,
            SuccessionPlanId = dto.SuccessionPlanId,
            CandidateId = dto.CandidateId,
            ActionDescription = dto.ActionDescription,
            Type = dto.Type,
            Priority = dto.Priority,
            ResponsiblePersonId = dto.ResponsiblePersonId,
            AssignedById = dto.AssignedById,
            DueDate = dto.DueDate,
            Status = ActionStatus.NotStarted,
            DependsOnActionId = dto.DependsOnActionId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SuccessionAction entity, UpdateSuccessionActionDto dto, Guid userId)
    {
        entity.ActionDescription = dto.ActionDescription;
        entity.Type = dto.Type;
        entity.Priority = dto.Priority;
        entity.ResponsiblePersonId = dto.ResponsiblePersonId;
        entity.AssignedById = dto.AssignedById;
        entity.DueDate = dto.DueDate;
        entity.StartedDate = dto.StartedDate;
        entity.Status = dto.Status;
        entity.CompletionDate = dto.CompletionDate;
        entity.CompletionNotes = dto.CompletionNotes;
        entity.DependsOnActionId = dto.DependsOnActionId;
        entity.WasSuccessful = dto.WasSuccessful;
        entity.OutcomeNotes = dto.OutcomeNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SuccessionActionSummaryDto> ToSummaryDtoList(this IEnumerable<SuccessionAction> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // SUCCESSION PLAN HISTORY  (immutable — read only)
    // ========================================================================

    #region SuccessionPlanHistory

    public static SuccessionPlanHistoryDto ToDto(this SuccessionPlanHistory entity)
    {
        return new SuccessionPlanHistoryDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SuccessionPlanId = entity.SuccessionPlanId,
            PlanNumber = entity.SuccessionPlan?.PlanNumber ?? string.Empty,
            VersionNumber = entity.VersionNumber,
            PlanYear = entity.PlanYear,
            StatusAtSnapshot = entity.StatusAtSnapshot,
            RiskLevelAtSnapshot = entity.RiskLevelAtSnapshot,
            HasReadyNowSuccessorAtSnapshot = entity.HasReadyNowSuccessorAtSnapshot,
            NumberOfSuccessorsAtSnapshot = entity.NumberOfSuccessorsAtSnapshot,
            NotesAtSnapshot = entity.NotesAtSnapshot,
            PlanSnapshot = entity.PlanSnapshot,
            SnapshotDate = entity.SnapshotDate,
            SnapshotCreatedById = entity.SnapshotCreatedById,
            SnapshotCreatedByName = entity.SnapshotCreatedBy?.FullName,
            ChangeReason = entity.ChangeReason,
        };
    }

    public static SuccessionPlanHistorySummaryDto ToSummaryDto(this SuccessionPlanHistory entity)
    {
        return new SuccessionPlanHistorySummaryDto
        {
            Id = entity.Id,
            VersionNumber = entity.VersionNumber,
            PlanYear = entity.PlanYear,
            StatusAtSnapshot = entity.StatusAtSnapshot,
            RiskLevelAtSnapshot = entity.RiskLevelAtSnapshot,
            SnapshotDate = entity.SnapshotDate,
            SnapshotCreatedByName = entity.SnapshotCreatedBy?.FullName,
            ChangeReason = entity.ChangeReason,
        };
    }

    public static IEnumerable<SuccessionPlanHistorySummaryDto> ToSummaryDtoList(this IEnumerable<SuccessionPlanHistory> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // SUCCESSION DOCUMENT
    // ========================================================================

    #region SuccessionDocument

    public static SuccessionDocumentDto ToDto(this SuccessionDocument entity)
    {
        return new SuccessionDocumentDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SuccessionPlanId = entity.SuccessionPlanId,
            PlanNumber = entity.SuccessionPlan?.PlanNumber,
            DocumentName = entity.DocumentName,
            DocumentType = entity.DocumentType,
            DocumentUrl = entity.DocumentUrl,
            FileUploadRecordId = entity.FileUploadRecordId,
            DocumentRecordId = entity.DocumentRecordId,
            DocumentVersionId = entity.DocumentVersionId,
            Description = entity.Description,
            CandidateId = entity.CandidateId,
            CandidateEmployeeName = entity.Candidate?.Employee?.FullName,
            TalentPoolMemberId = entity.TalentPoolMemberId,
            TalentPoolMemberName = entity.TalentPoolMember?.Employee?.FullName,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
            FileSizeBytes = entity.FileSizeBytes,
            FileHash = entity.FileHash,
            IsConfidential = entity.IsConfidential,
            RetentionDate = entity.RetentionDate,
        };
    }

    /// <param name="uploadedByEmployeeId">
    /// D-15: the authenticated employee. <c>UploadedById</c> is an <c>Employee</c> FK the screens
    /// render as "Uploaded by", and it used to be copied from the request body while the token's
    /// id went only to <c>CreatedBy</c> — so a document could be attributed to a colleague. It is
    /// a parameter rather than a DTO field precisely so it cannot be asserted by a caller.
    /// </param>
    public static SuccessionDocument ToEntity(
        this CreateSuccessionDocumentDto dto, Guid tenantId, Guid userId, Guid uploadedByEmployeeId)
    {
        return new SuccessionDocument
        {
            TenantId = tenantId,
            SuccessionPlanId = dto.SuccessionPlanId,
            CandidateId = dto.CandidateId,
            TalentPoolMemberId = dto.TalentPoolMemberId,
            DocumentName = dto.DocumentName,
            DocumentType = dto.DocumentType,
            DocumentUrl = dto.DocumentUrl,
            FileUploadRecordId = dto.FileUploadRecordId,
            DocumentRecordId = dto.DocumentRecordId,
            DocumentVersionId = dto.DocumentVersionId,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedByEmployeeId,
            FileSizeBytes = dto.FileSizeBytes,
            FileHash = dto.FileHash,
            IsConfidential = dto.IsConfidential,
            RetentionDate = dto.RetentionDate,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TALENT POOL
    // ========================================================================

    #region TalentPool

    public static TalentPoolDto ToDto(this TalentPool entity)
    {
        return new TalentPoolDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            PoolTypeId = entity.PoolTypeId,
            PoolTypeName = entity.PoolType?.Name ?? string.Empty,
            PoolTypeColor = entity.PoolType?.ColorHex,
            TargetPositionId = entity.TargetPositionId,
            TargetPositionTitle = entity.TargetPosition?.Title,
            TargetSize = entity.TargetSize,
            ValidFrom = entity.ValidFrom,
            ValidTo = entity.ValidTo,
            IsActive = entity.IsActive,
            OwnerId = entity.OwnerId,
            OwnerName = entity.Owner?.FullName ?? string.Empty,
            CurrentMemberCount = entity.Members.Count(m => m.IsActive),
            Members = entity.Members.Select(m => m.ToSummaryDto()).ToList(),
        };
    }

    public static TalentPoolSummaryDto ToSummaryDto(this TalentPool entity)
    {
        return new TalentPoolSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            PoolTypeId = entity.PoolTypeId,
            PoolTypeName = entity.PoolType?.Name ?? string.Empty,
            PoolTypeColor = entity.PoolType?.ColorHex,
            TargetSize = entity.TargetSize,
            IsActive = entity.IsActive,
            OwnerName = entity.Owner?.FullName ?? string.Empty,
            CurrentMemberCount = entity.Members.Count(m => m.IsActive),
            ValidFrom = entity.ValidFrom,
            ValidTo = entity.ValidTo,
        };
    }

    public static TalentPool ToEntity(this CreateTalentPoolDto dto, Guid tenantId, Guid userId)
    {
        return new TalentPool
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            PoolTypeId = dto.PoolTypeId,
            TargetPositionId = dto.TargetPositionId,
            TargetSize = dto.TargetSize,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
            IsActive = dto.IsActive,
            OwnerId = dto.OwnerId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TalentPool entity, UpdateTalentPoolDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.PoolTypeId = dto.PoolTypeId;
        entity.TargetPositionId = dto.TargetPositionId;
        entity.TargetSize = dto.TargetSize;
        entity.ValidFrom = dto.ValidFrom;
        entity.ValidTo = dto.ValidTo;
        entity.IsActive = dto.IsActive;
        entity.OwnerId = dto.OwnerId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TalentPoolSummaryDto> ToSummaryDtoList(this IEnumerable<TalentPool> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TALENT POOL TYPE DEFINITION
    // ========================================================================

    #region TalentPoolTypeDefinition

    public static TalentPoolTypeDefinitionDto ToDto(this TalentPoolTypeDefinition entity)
    {
        return new TalentPoolTypeDefinitionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            ColorHex = entity.ColorHex,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            IsSystemDefault = entity.IsSystemDefault,
            PoolCount = entity.Pools?.Count ?? 0,
        };
    }

    public static IEnumerable<TalentPoolTypeDefinitionDto> ToDtoList(this IEnumerable<TalentPoolTypeDefinition> entities)
        => entities.Select(e => e.ToDto());

    public static TalentPoolTypeDefinition ToEntity(this CreateTalentPoolTypeDefinitionDto dto, Guid tenantId, Guid userId)
    {
        return new TalentPoolTypeDefinition
        {
            TenantId = tenantId,
            Code = string.IsNullOrWhiteSpace(dto.Code) ? SlugifyCode(dto.Name) : dto.Code.Trim(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            ColorHex = dto.ColorHex,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            IsSystemDefault = false,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TalentPoolTypeDefinition entity, UpdateTalentPoolTypeDefinitionDto dto, Guid userId)
    {
        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.ColorHex = dto.ColorHex;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    private static string SlugifyCode(string name)
    {
        var cleaned = new string((name ?? string.Empty).Where(ch => char.IsLetterOrDigit(ch)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned)
            ? "TYPE_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()
            : cleaned;
    }

    #endregion

    // ========================================================================
    // TALENT POOL MEMBER
    // ========================================================================

    #region TalentPoolMember

    public static TalentPoolMemberDto ToDto(this TalentPoolMember entity)
    {
        return new TalentPoolMemberDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TalentPoolId = entity.TalentPoolId,
            TalentPoolName = entity.TalentPool?.Name ?? string.Empty,
            TalentPoolTypeName = entity.TalentPool?.PoolType?.Name ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Rank = entity.Rank,
            Readiness = entity.Readiness,
            ReadyByDate = entity.ReadyByDate,
            Justification = entity.Justification,
            Strengths = entity.Strengths,
            DevelopmentGaps = entity.DevelopmentGaps,
            EnrolledDate = entity.EnrolledDate,
            NominatedById = entity.NominatedById,
            NominatedByName = entity.NominatedBy?.FullName,
            NominationNotes = entity.NominationNotes,
            LastReviewDate = entity.LastReviewDate,
            NextReviewDate = entity.NextReviewDate,
            ReviewNotes = entity.ReviewNotes,
            LatestPerformanceRating = entity.LatestPerformanceRating,
            LatestPotentialRating = entity.LatestPotentialRating,
            RatingLastUpdated = entity.RatingLastUpdated,
            RemovedDate = entity.RemovedDate,
            RemovalReason = entity.RemovalReason,
            IsActive = entity.IsActive,
            ReviewRatings = entity.ReviewRatings.Select(r => r.ToSummaryDto()).ToList(),
            DevelopmentActivities = entity.DevelopmentActivities.Select(a => a.ToSummaryDto()).ToList(),
            Documents = entity.Documents.Select(d => d.ToDto()).ToList(),
        };
    }

    public static TalentPoolMemberSummaryDto ToSummaryDto(this TalentPoolMember entity)
    {
        return new TalentPoolMemberSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Rank = entity.Rank,
            Readiness = entity.Readiness,
            LatestPerformanceRating = entity.LatestPerformanceRating,
            LatestPotentialRating = entity.LatestPotentialRating,
            IsActive = entity.IsActive,
            EnrolledDate = entity.EnrolledDate,
        };
    }

    public static TalentPoolMember ToEntity(this CreateTalentPoolMemberDto dto, Guid tenantId, Guid userId)
    {
        return new TalentPoolMember
        {
            TenantId = tenantId,
            TalentPoolId = dto.TalentPoolId,
            EmployeeId = dto.EmployeeId,
            Rank = dto.Rank,
            Readiness = dto.Readiness,
            ReadyByDate = dto.ReadyByDate,
            Justification = dto.Justification,
            Strengths = dto.Strengths,
            DevelopmentGaps = dto.DevelopmentGaps,
            EnrolledDate = dto.EnrolledDate,
            // NominatedById is set by the service from the authenticated employee, not the body.
            NominationNotes = dto.NominationNotes,
            IsActive = true,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TalentPoolMember entity, UpdateTalentPoolMemberDto dto, Guid userId)
    {
        entity.Rank = dto.Rank;
        entity.Readiness = dto.Readiness;
        entity.ReadyByDate = dto.ReadyByDate;
        entity.Justification = dto.Justification;
        entity.Strengths = dto.Strengths;
        entity.DevelopmentGaps = dto.DevelopmentGaps;
        entity.LastReviewDate = dto.LastReviewDate;
        entity.NextReviewDate = dto.NextReviewDate;
        entity.ReviewNotes = dto.ReviewNotes;
        entity.IsActive = dto.IsActive;
        entity.RemovedDate = dto.RemovedDate;
        entity.RemovalReason = dto.RemovalReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TalentPoolMemberSummaryDto> ToSummaryDtoList(this IEnumerable<TalentPoolMember> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TALENT REVIEW SESSION
    // ========================================================================

    #region TalentReviewSession

    public static TalentReviewSessionDto ToDto(this TalentReviewSession entity)
    {
        return new TalentReviewSessionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SessionName = entity.SessionName,
            ReviewYear = entity.ReviewYear,
            SessionDate = entity.SessionDate,
            Location = entity.Location,
            FacilitatedById = entity.FacilitatedById,
            FacilitatedByName = entity.FacilitatedBy?.FullName,
            Agenda = entity.Agenda,
            SessionNotes = entity.SessionNotes,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            IsFinalized = entity.IsFinalized,
            FinalizedDate = entity.FinalizedDate,
            FinalizedById = entity.FinalizedById,
            FinalizedByName = entity.FinalizedBy?.FullName,
            Ratings = entity.Ratings.Select(r => r.ToSummaryDto()).ToList(),
        };
    }

    public static TalentReviewSessionSummaryDto ToSummaryDto(this TalentReviewSession entity)
    {
        return new TalentReviewSessionSummaryDto
        {
            Id = entity.Id,
            SessionName = entity.SessionName,
            ReviewYear = entity.ReviewYear,
            SessionDate = entity.SessionDate,
            FacilitatedByName = entity.FacilitatedBy?.FullName,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            IsFinalized = entity.IsFinalized,
            RatingCount = entity.Ratings.Count,
        };
    }

    public static TalentReviewSession ToEntity(this CreateTalentReviewSessionDto dto, Guid tenantId, Guid userId)
    {
        return new TalentReviewSession
        {
            TenantId = tenantId,
            SessionName = dto.SessionName,
            ReviewYear = dto.ReviewYear,
            SessionDate = dto.SessionDate,
            Location = dto.Location,
            FacilitatedById = dto.FacilitatedById,
            Agenda = dto.Agenda,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            IsFinalized = false,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TalentReviewSession entity, UpdateTalentReviewSessionDto dto, Guid userId)
    {
        entity.SessionName = dto.SessionName;
        entity.SessionDate = dto.SessionDate;
        entity.Location = dto.Location;
        entity.FacilitatedById = dto.FacilitatedById;
        entity.Agenda = dto.Agenda;
        entity.SessionNotes = dto.SessionNotes;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TalentReviewSessionSummaryDto> ToSummaryDtoList(this IEnumerable<TalentReviewSession> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TALENT REVIEW RATING
    // ========================================================================

    #region TalentReviewRating

    public static TalentReviewRatingDto ToDto(this TalentReviewRating entity)
    {
        return new TalentReviewRatingDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SessionId = entity.SessionId,
            SessionName = entity.Session?.SessionName ?? string.Empty,
            ReviewYear = entity.Session?.ReviewYear ?? 0,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            TalentPoolMemberId = entity.TalentPoolMemberId,
            TalentPoolName = entity.TalentPoolMember?.TalentPool?.Name,
            Performance = entity.Performance,
            Potential = entity.Potential,
            PreviousRatingSessionId = entity.PreviousRatingSessionId,
            PreviousSessionName = entity.PreviousRatingSession?.SessionName,
            PreviousPerformance = entity.PreviousPerformance,
            PreviousPotential = entity.PreviousPotential,
            Justification = entity.Justification,
            KeyStrengths = entity.KeyStrengths,
            DevelopmentPriorities = entity.DevelopmentPriorities,
            RatedById = entity.RatedById,
            RatedByName = entity.RatedBy?.FullName,
            CalibrationConfirmed = entity.CalibrationConfirmed,
            CalibrationConfirmedById = entity.CalibrationConfirmedById,
            CalibrationConfirmedByName = entity.CalibrationConfirmedBy?.FullName,
            CalibrationConfirmedDate = entity.CalibrationConfirmedDate,
            CalibrationNotes = entity.CalibrationNotes,
        };
    }

    public static TalentReviewRatingSummaryDto ToSummaryDto(this TalentReviewRating entity)
    {
        return new TalentReviewRatingSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            Performance = entity.Performance,
            Potential = entity.Potential,
            PreviousPerformance = entity.PreviousPerformance,
            PreviousPotential = entity.PreviousPotential,
            CalibrationConfirmed = entity.CalibrationConfirmed,
        };
    }

    public static TalentReviewRating ToEntity(this CreateTalentReviewRatingDto dto, Guid tenantId, Guid userId)
    {
        return new TalentReviewRating
        {
            TenantId = tenantId,
            SessionId = dto.SessionId,
            EmployeeId = dto.EmployeeId,
            TalentPoolMemberId = dto.TalentPoolMemberId,
            Performance = dto.Performance,
            Potential = dto.Potential,
            Justification = dto.Justification,
            KeyStrengths = dto.KeyStrengths,
            DevelopmentPriorities = dto.DevelopmentPriorities,
            RatedById = dto.RatedById,
            CalibrationConfirmed = false,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TalentReviewRating entity, UpdateTalentReviewRatingDto dto, Guid userId)
    {
        entity.Performance = dto.Performance;
        entity.Potential = dto.Potential;
        entity.Justification = dto.Justification;
        entity.KeyStrengths = dto.KeyStrengths;
        entity.DevelopmentPriorities = dto.DevelopmentPriorities;
        entity.RatedById = dto.RatedById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TalentReviewRatingSummaryDto> ToSummaryDtoList(this IEnumerable<TalentReviewRating> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion
}
