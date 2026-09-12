using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Application.HR.Extensions;

// ============================================================================
// SHE mapping — Reference Catalog (A) & Incident Management (B).
// Hazard/Inspection, Permit/PPE/Equipment, Contractor/Training,
// Environment/Health and Emergency/Governance mappings live in the sibling
// Safety*MappingExtensions.cs files (all in this same namespace).
// ============================================================================

public static class StaffSafetyMappingExtensions
{
    // ========================================================================
    // A. REFERENCE / LOOKUP CATALOG
    // ========================================================================

    #region IncidentType

    public static SheIncidentTypeDto ToDto(this SheIncidentType e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        Category = e.Category,
        IsReportable = e.IsReportable,
        RegulatoryBodyId = e.RegulatoryBodyId,
        RegulatoryBodyName = e.RegulatoryBody?.Name,
        ReportingWindowHours = e.ReportingWindowHours,
        IsActive = e.IsActive,
        DefaultCorrectiveActions = e.DefaultCorrectiveActions.Select(a => a.ToDto()).ToList(),
    };

    public static SheIncidentType ToEntity(this CreateSheIncidentTypeDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Code = dto.Code,
        Name = dto.Name,
        Description = dto.Description,
        Category = dto.Category,
        IsReportable = dto.IsReportable,
        RegulatoryBodyId = dto.RegulatoryBodyId,
        ReportingWindowHours = dto.ReportingWindowHours,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheIncidentType e, UpdateSheIncidentTypeDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Description = dto.Description;
        e.Category = dto.Category;
        e.IsReportable = dto.IsReportable;
        e.RegulatoryBodyId = dto.RegulatoryBodyId;
        e.ReportingWindowHours = dto.ReportingWindowHours;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SheIncidentTypeCorrectiveActionDto ToDto(this SheIncidentTypeCorrectiveAction e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentTypeId = e.IncidentTypeId,
        CorrectiveActionTemplateId = e.CorrectiveActionTemplateId,
        CorrectiveActionTemplateCode = e.CorrectiveActionTemplate?.Code ?? string.Empty,
        CorrectiveActionTemplateTitle = e.CorrectiveActionTemplate?.Title ?? string.Empty,
        DisplayOrder = e.DisplayOrder,
        DeadlineDays = e.DeadlineDays,
        IsMandatory = e.IsMandatory,
    };

    public static SheIncidentTypeCorrectiveAction ToEntity(this CreateSheIncidentTypeCorrectiveActionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentTypeId = dto.IncidentTypeId,
        CorrectiveActionTemplateId = dto.CorrectiveActionTemplateId,
        DisplayOrder = dto.DisplayOrder,
        DeadlineDays = dto.DeadlineDays,
        IsMandatory = dto.IsMandatory,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheIncidentTypeCorrectiveAction e, UpdateSheIncidentTypeCorrectiveActionDto dto, Guid userId)
    {
        e.DisplayOrder = dto.DisplayOrder;
        e.DeadlineDays = dto.DeadlineDays;
        e.IsMandatory = dto.IsMandatory;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region InjuryType

    public static SheInjuryTypeDto ToDto(this SheInjuryType e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        IsActive = e.IsActive,
    };

    public static SheInjuryType ToEntity(this CreateSheInjuryTypeDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Code = dto.Code,
        Name = dto.Name,
        Description = dto.Description,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheInjuryType e, UpdateSheInjuryTypeDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Description = dto.Description;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region BodyPart

    public static SheBodyPartDto ToDto(this SheBodyPart e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Code = e.Code,
        Name = e.Name,
        Region = e.Region,
        IsActive = e.IsActive,
    };

    public static SheBodyPart ToEntity(this CreateSheBodyPartDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Code = dto.Code,
        Name = dto.Name,
        Region = dto.Region,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheBodyPart e, UpdateSheBodyPartDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Region = dto.Region;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region CorrectiveActionTemplate

    public static SheCorrectiveActionTemplateDto ToDto(this SheCorrectiveActionTemplate e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Code = e.Code,
        Title = e.Title,
        Description = e.Description,
        Category = e.Category,
        DefaultDeadlineDays = e.DefaultDeadlineDays,
        IsActive = e.IsActive,
    };

    public static SheCorrectiveActionTemplate ToEntity(this CreateSheCorrectiveActionTemplateDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Code = dto.Code,
        Title = dto.Title,
        Description = dto.Description,
        Category = dto.Category,
        DefaultDeadlineDays = dto.DefaultDeadlineDays,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheCorrectiveActionTemplate e, UpdateSheCorrectiveActionTemplateDto dto, Guid userId)
    {
        e.Title = dto.Title;
        e.Description = dto.Description;
        e.Category = dto.Category;
        e.DefaultDeadlineDays = dto.DefaultDeadlineDays;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region RegulatoryBody

    public static SheRegulatoryBodyDto ToDto(this SheRegulatoryBody e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Name = e.Name,
        ShortName = e.ShortName,
        ContactAddress = e.ContactAddress,
        Phone = e.Phone,
        Email = e.Email,
        Website = e.Website,
        Domain = e.Domain,
        IsActive = e.IsActive,
    };

    public static SheRegulatoryBody ToEntity(this CreateSheRegulatoryBodyDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Name = dto.Name,
        ShortName = dto.ShortName,
        ContactAddress = dto.ContactAddress,
        Phone = dto.Phone,
        Email = dto.Email,
        Website = dto.Website,
        Domain = dto.Domain,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheRegulatoryBody e, UpdateSheRegulatoryBodyDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.ShortName = dto.ShortName;
        e.ContactAddress = dto.ContactAddress;
        e.Phone = dto.Phone;
        e.Email = dto.Email;
        e.Website = dto.Website;
        e.Domain = dto.Domain;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // B. INCIDENT MANAGEMENT & INVESTIGATION
    // ========================================================================

    #region SafetyIncident

    public static SafetyIncidentDto ToDto(this SafetyIncident e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentNumber = e.IncidentNumber,
        Category = e.Category,
        Severity = e.Severity,
        Status = e.Status,
        IncidentTypeId = e.IncidentTypeId,
        IncidentTypeName = e.IncidentType?.Name,
        IncidentDate = e.IncidentDate,
        IncidentTime = e.IncidentTime,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        SupervisorId = e.SupervisorId,
        SupervisorName = e.Supervisor?.FullName,
        Description = e.Description,
        ImmediateCause = e.ImmediateCause,
        UnderlyingCause = e.UnderlyingCause,
        ContributingFactors = e.ContributingFactors,
        CouldHaveCausedInjury = e.CouldHaveCausedInjury,
        PotentialConsequence = e.PotentialConsequence,
        ReportedById = e.ReportedById,
        ReportedByName = e.ReportedBy?.FullName ?? string.Empty,
        ReportedDate = e.ReportedDate,
        ImmediateActionTaken = e.ImmediateActionTaken,
        LikelihoodBefore = e.LikelihoodBefore,
        SeverityBefore = e.SeverityBefore,
        RiskScoreBefore = e.RiskScoreBefore,
        LikelihoodAfter = e.LikelihoodAfter,
        SeverityAfter = e.SeverityAfter,
        RiskScoreAfter = e.RiskScoreAfter,
        RequiresInvestigation = e.RequiresInvestigation,
        LeadInvestigatorId = e.LeadInvestigatorId,
        LeadInvestigatorName = e.LeadInvestigator?.FullName,
        InvestigationStartDate = e.InvestigationStartDate,
        InvestigationTargetDate = e.InvestigationTargetDate,
        InvestigationCompleteDate = e.InvestigationCompleteDate,
        RootCauseAnalysis = e.RootCauseAnalysis,
        InvestigationFindings = e.InvestigationFindings,
        RootCauseMethod = e.RootCauseMethod,
        ReportableToAuthority = e.ReportableToAuthority,
        ReportedToBodyId = e.ReportedToBodyId,
        ReportedToBodyName = e.ReportedToBody?.Name,
        AuthorityNotificationDate = e.AuthorityNotificationDate,
        AuthorityReferenceNumber = e.AuthorityReferenceNumber,
        AuthorityNotifiedById = e.AuthorityNotifiedById,
        AuthorityNotifiedByName = e.AuthorityNotifiedBy?.FullName,
        InsuranceClaimFiled = e.InsuranceClaimFiled,
        ClaimFiledDate = e.ClaimFiledDate,
        ClaimReferenceNumber = e.ClaimReferenceNumber,
        InsuranceProviderId = e.InsuranceProviderId,
        InsuranceProviderName = e.InsuranceProvider?.Name,
        ClaimAmount = e.ClaimAmount,
        ClaimApproved = e.ClaimApproved,
        AmountPaid = e.AmountPaid,
        TotalLostDays = e.TotalLostDays,
        IsLostTimeInjury = e.IsLostTimeInjury,
        ReviewedById = e.ReviewedById,
        ReviewedByName = e.ReviewedBy?.FullName,
        ReviewedDate = e.ReviewedDate,
        ReviewComments = e.ReviewComments,
        ClosedDate = e.ClosedDate,
        ClosedById = e.ClosedById,
        ClosedByName = e.ClosedBy?.FullName,
        ClosureNotes = e.ClosureNotes,
        LessonsLearned = e.LessonsLearned,
        InvolvedPersons = e.InvolvedPersons.Select(p => p.ToDto()).ToList(),
        Witnesses = e.Witnesses.Select(w => w.ToDto()).ToList(),
        InvestigationTeam = e.InvestigationTeam.Select(m => m.ToDto()).ToList(),
        CorrectiveActions = e.CorrectiveActions.Select(c => c.ToDto()).ToList(),
        FollowUps = e.FollowUps.Select(f => f.ToDto()).ToList(),
        Documents = e.Documents.Select(d => d.ToDto()).ToList(),
        StatutorySubmissions = e.StatutorySubmissions.Select(s => s.ToDto()).ToList(),
    };

    public static SafetyIncidentSummaryDto ToSummaryDto(this SafetyIncident e) => new()
    {
        Id = e.Id,
        IncidentNumber = e.IncidentNumber,
        Category = e.Category,
        Severity = e.Severity,
        Status = e.Status,
        IncidentTypeName = e.IncidentType?.Name,
        IncidentDate = e.IncidentDate,
        LocationName = e.Location?.Name,
        ReportedByName = e.ReportedBy?.FullName ?? string.Empty,
        RequiresInvestigation = e.RequiresInvestigation,
        IsLostTimeInjury = e.IsLostTimeInjury,
        TotalLostDays = e.TotalLostDays,
        InvolvedPersonCount = e.InvolvedPersons.Count,
        OpenCorrectiveActionCount = e.CorrectiveActions.Count(c =>
            c.Status != SheCorrectiveActionStatus.Completed &&
            c.Status != SheCorrectiveActionStatus.Verified &&
            c.Status != SheCorrectiveActionStatus.Cancelled),
    };

    public static SafetyIncident ToEntity(this CreateSafetyIncidentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Category = dto.Category,
        Severity = dto.Severity,
        Status = SheIncidentStatus.Reported,
        IncidentTypeId = dto.IncidentTypeId,
        IncidentDate = dto.IncidentDate,
        IncidentTime = dto.IncidentTime,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        OrganizationUnitId = dto.OrganizationUnitId,
        SupervisorId = dto.SupervisorId,
        Description = dto.Description,
        ImmediateCause = dto.ImmediateCause,
        ContributingFactors = dto.ContributingFactors,
        CouldHaveCausedInjury = dto.CouldHaveCausedInjury,
        PotentialConsequence = dto.PotentialConsequence,
        ReportedById = dto.ReportedById,
        ReportedDate = dto.ReportedDate,
        ImmediateActionTaken = dto.ImmediateActionTaken,
        LikelihoodBefore = dto.LikelihoodBefore,
        SeverityBefore = dto.SeverityBefore,
        RiskScoreBefore = dto.LikelihoodBefore * dto.SeverityBefore,
        RequiresInvestigation = dto.RequiresInvestigation,
        ReportableToAuthority = dto.ReportableToAuthority,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyIncident e, UpdateSafetyIncidentDto dto, Guid userId)
    {
        e.Category = dto.Category;
        e.Severity = dto.Severity;
        e.IncidentTypeId = dto.IncidentTypeId;
        e.IncidentDate = dto.IncidentDate;
        e.IncidentTime = dto.IncidentTime;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.OrganizationUnitId = dto.OrganizationUnitId;
        e.SupervisorId = dto.SupervisorId;
        e.Description = dto.Description;
        e.ImmediateCause = dto.ImmediateCause;
        e.UnderlyingCause = dto.UnderlyingCause;
        e.ContributingFactors = dto.ContributingFactors;
        e.CouldHaveCausedInjury = dto.CouldHaveCausedInjury;
        e.PotentialConsequence = dto.PotentialConsequence;
        e.ImmediateActionTaken = dto.ImmediateActionTaken;
        e.LikelihoodBefore = dto.LikelihoodBefore;
        e.SeverityBefore = dto.SeverityBefore;
        e.RiskScoreBefore = dto.LikelihoodBefore * dto.SeverityBefore;
        e.LikelihoodAfter = dto.LikelihoodAfter;
        e.SeverityAfter = dto.SeverityAfter;
        e.RiskScoreAfter = dto.LikelihoodAfter * dto.SeverityAfter;
        e.RequiresInvestigation = dto.RequiresInvestigation;
        e.ReportableToAuthority = dto.ReportableToAuthority;
        e.LessonsLearned = dto.LessonsLearned;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SafetyIncidentSummaryDto> ToSummaryDtoList(this IEnumerable<SafetyIncident> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheStatutoryIncidentSubmissionDto ToDto(this SheStatutoryIncidentSubmission e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentId = e.IncidentId,
        IncidentNumber = e.Incident?.IncidentNumber,
        RegulatoryBodyId = e.RegulatoryBodyId,
        RegulatoryBodyName = e.RegulatoryBody?.Name ?? string.Empty,
        Type = e.Type,
        Method = e.Method,
        SubmissionDate = e.SubmissionDate,
        ReferenceNumber = e.ReferenceNumber,
        SubmittedById = e.SubmittedById,
        SubmittedByName = e.SubmittedBy?.FullName ?? string.Empty,
        DocumentPath = e.DocumentPath,
        AcknowledgementReceived = e.AcknowledgementReceived,
        AcknowledgementDate = e.AcknowledgementDate,
        AcknowledgementReference = e.AcknowledgementReference,
        Notes = e.Notes,
    };

    #endregion

    #region SafetyIncidentInvolvedPerson

    public static SafetyIncidentInvolvedPersonDto ToDto(this SafetyIncidentInvolvedPerson e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentId = e.IncidentId,
        IsEmployee = e.IsEmployee,
        EmployeeId = e.EmployeeId,
        EmployeeNumber = e.Employee?.EmployeeNumber,
        FullName = e.FullName,
        OrganizationOrCompany = e.OrganizationOrCompany,
        RoleInIncident = e.RoleInIncident,
        WasOnDuty = e.WasOnDuty,
        ActivityBeingPerformed = e.ActivityBeingPerformed,
        WasUsingPpe = e.WasUsingPpe,
        PpeUsed = e.PpeUsed,
        PpeWasAdequate = e.PpeWasAdequate,
        WasInjured = e.WasInjured,
        InjuryDescription = e.InjuryDescription,
        InjuryClassification = e.InjuryClassification,
        InjuryTypeId = e.InjuryTypeId,
        InjuryTypeName = e.InjuryType?.Name,
        IsFatal = e.IsFatal,
        InjuredBodyParts = e.InjuredBodyParts.Select(b => b.ToDto()).ToList(),
        FirstAidGiven = e.FirstAidGiven,
        FirstAidDetails = e.FirstAidDetails,
        FirstAidProviderId = e.FirstAidProviderId,
        FirstAidProviderName = e.FirstAidProvider?.FullName,
        MedicalTreatmentRequired = e.MedicalTreatmentRequired,
        HealthcareFacilityId = e.HealthcareFacilityId,
        HealthcareFacilityName = e.HealthcareFacility?.FacilityName,
        TreatmentDate = e.TreatmentDate,
        DiagnosisGiven = e.DiagnosisGiven,
        MedicalExpenseClaimId = e.MedicalExpenseClaimId,
        ResultedInTimeOff = e.ResultedInTimeOff,
        TimeOffStartDate = e.TimeOffStartDate,
        TimeOffEndDate = e.TimeOffEndDate,
        LostDays = e.LostDays,
        OnLightDuty = e.OnLightDuty,
        LightDutyRestrictions = e.LightDutyRestrictions,
        ReturnToWorkPlanId = e.ReturnToWorkPlanId,
    };

    public static SafetyIncidentInvolvedPerson ToEntity(this CreateSafetyIncidentInvolvedPersonDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentId = dto.IncidentId,
        IsEmployee = dto.IsEmployee,
        EmployeeId = dto.EmployeeId,
        FullName = dto.FullName,
        OrganizationOrCompany = dto.OrganizationOrCompany,
        RoleInIncident = dto.RoleInIncident,
        WasOnDuty = dto.WasOnDuty,
        ActivityBeingPerformed = dto.ActivityBeingPerformed,
        WasUsingPpe = dto.WasUsingPpe,
        PpeUsed = dto.PpeUsed,
        PpeWasAdequate = dto.PpeWasAdequate,
        WasInjured = dto.WasInjured,
        InjuryDescription = dto.InjuryDescription,
        InjuryClassification = dto.InjuryClassification,
        InjuryTypeId = dto.InjuryTypeId,
        IsFatal = dto.IsFatal,
        FirstAidGiven = dto.FirstAidGiven,
        FirstAidDetails = dto.FirstAidDetails,
        FirstAidProviderId = dto.FirstAidProviderId,
        MedicalTreatmentRequired = dto.MedicalTreatmentRequired,
        HealthcareFacilityId = dto.HealthcareFacilityId,
        TreatmentDate = dto.TreatmentDate,
        DiagnosisGiven = dto.DiagnosisGiven,
        ResultedInTimeOff = dto.ResultedInTimeOff,
        TimeOffStartDate = dto.TimeOffStartDate,
        TimeOffEndDate = dto.TimeOffEndDate,
        LostDays = dto.LostDays,
        OnLightDuty = dto.OnLightDuty,
        LightDutyRestrictions = dto.LightDutyRestrictions,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyIncidentInvolvedPerson e, UpdateSafetyIncidentInvolvedPersonDto dto, Guid userId)
    {
        e.FullName = dto.FullName;
        e.OrganizationOrCompany = dto.OrganizationOrCompany;
        e.RoleInIncident = dto.RoleInIncident;
        e.WasOnDuty = dto.WasOnDuty;
        e.ActivityBeingPerformed = dto.ActivityBeingPerformed;
        e.WasUsingPpe = dto.WasUsingPpe;
        e.PpeUsed = dto.PpeUsed;
        e.PpeWasAdequate = dto.PpeWasAdequate;
        e.WasInjured = dto.WasInjured;
        e.InjuryDescription = dto.InjuryDescription;
        e.InjuryClassification = dto.InjuryClassification;
        e.InjuryTypeId = dto.InjuryTypeId;
        e.IsFatal = dto.IsFatal;
        e.FirstAidGiven = dto.FirstAidGiven;
        e.FirstAidDetails = dto.FirstAidDetails;
        e.FirstAidProviderId = dto.FirstAidProviderId;
        e.MedicalTreatmentRequired = dto.MedicalTreatmentRequired;
        e.HealthcareFacilityId = dto.HealthcareFacilityId;
        e.TreatmentDate = dto.TreatmentDate;
        e.DiagnosisGiven = dto.DiagnosisGiven;
        e.MedicalExpenseClaimId = dto.MedicalExpenseClaimId;
        e.ResultedInTimeOff = dto.ResultedInTimeOff;
        e.TimeOffStartDate = dto.TimeOffStartDate;
        e.TimeOffEndDate = dto.TimeOffEndDate;
        e.LostDays = dto.LostDays;
        e.OnLightDuty = dto.OnLightDuty;
        e.LightDutyRestrictions = dto.LightDutyRestrictions;
        e.ReturnToWorkPlanId = dto.ReturnToWorkPlanId;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyIncidentInjuredBodyPartDto ToDto(this SafetyIncidentInjuredBodyPart e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InvolvedPersonId = e.InvolvedPersonId,
        BodyPartId = e.BodyPartId,
        BodyPartName = e.BodyPart?.Name ?? string.Empty,
        Side = e.Side,
        Notes = e.Notes,
    };

    public static SafetyIncidentInjuredBodyPart ToEntity(this CreateSafetyIncidentInjuredBodyPartDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        InvolvedPersonId = dto.InvolvedPersonId,
        BodyPartId = dto.BodyPartId,
        Side = dto.Side,
        Notes = dto.Notes,
        CreatedBy = userId.ToString(),
    };

    #endregion

    #region SafetyIncidentWitness

    public static SafetyIncidentWitnessDto ToDto(this SafetyIncidentWitness e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentId = e.IncidentId,
        IsEmployee = e.IsEmployee,
        EmployeeId = e.EmployeeId,
        Name = e.Name,
        EmailAddress = e.EmailAddress,
        PhoneNumber = e.PhoneNumber,
        Statement = e.Statement,
        StatementDate = e.StatementDate,
        StatementSigned = e.StatementSigned,
        StatementDocumentPath = e.StatementDocumentPath,
        InterviewedById = e.InterviewedById,
        InterviewedByName = e.InterviewedBy?.FullName,
        InterviewDate = e.InterviewDate,
    };

    public static SafetyIncidentWitness ToEntity(this CreateSafetyIncidentWitnessDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentId = dto.IncidentId,
        IsEmployee = dto.IsEmployee,
        EmployeeId = dto.EmployeeId,
        Name = dto.Name,
        EmailAddress = dto.EmailAddress,
        PhoneNumber = dto.PhoneNumber,
        Statement = dto.Statement,
        StatementDate = dto.StatementDate,
        StatementSigned = dto.StatementSigned,
        StatementDocumentPath = dto.StatementDocumentPath,
        InterviewedById = dto.InterviewedById,
        InterviewDate = dto.InterviewDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyIncidentWitness e, UpdateSafetyIncidentWitnessDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.EmailAddress = dto.EmailAddress;
        e.PhoneNumber = dto.PhoneNumber;
        e.Statement = dto.Statement;
        e.StatementDate = dto.StatementDate;
        e.StatementSigned = dto.StatementSigned;
        e.StatementDocumentPath = dto.StatementDocumentPath;
        e.InterviewedById = dto.InterviewedById;
        e.InterviewDate = dto.InterviewDate;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region SafetyIncidentInvestigationTeamMember

    public static SafetyIncidentInvestigationTeamMemberDto ToDto(this SafetyIncidentInvestigationTeamMember e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentId = e.IncidentId,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        Role = e.Role,
        JoinedDate = e.JoinedDate,
    };

    public static SafetyIncidentInvestigationTeamMember ToEntity(this CreateSafetyIncidentInvestigationTeamMemberDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentId = dto.IncidentId,
        EmployeeId = dto.EmployeeId,
        Role = dto.Role,
        JoinedDate = dto.JoinedDate,
        CreatedBy = userId.ToString(),
    };

    #endregion

    #region SafetyIncidentCorrectiveAction

    public static SafetyIncidentCorrectiveActionDto ToDto(this SafetyIncidentCorrectiveAction e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentId = e.IncidentId,
        IncidentNumber = e.Incident?.IncidentNumber,
        IncidentTypeCorrectiveActionId = e.IncidentTypeCorrectiveActionId,
        ActionDescription = e.ActionDescription,
        Priority = e.Priority,
        Status = e.Status,
        ResponsiblePersonId = e.ResponsiblePersonId,
        ResponsiblePersonName = e.ResponsiblePerson?.FullName ?? string.Empty,
        DueDate = e.DueDate,
        CompletionDate = e.CompletionDate,
        CompletionNotes = e.CompletionNotes,
        EffectivenessVerified = e.EffectivenessVerified,
        VerificationDate = e.VerificationDate,
        EffectivenessReviewNotes = e.EffectivenessReviewNotes,
        VerifiedById = e.VerifiedById,
        VerifiedByName = e.VerifiedBy?.FullName,
    };

    public static SafetyIncidentCorrectiveAction ToEntity(this CreateSafetyIncidentCorrectiveActionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentId = dto.IncidentId,
        IncidentTypeCorrectiveActionId = dto.IncidentTypeCorrectiveActionId,
        ActionDescription = dto.ActionDescription,
        Priority = dto.Priority,
        Status = SheCorrectiveActionStatus.Pending,
        ResponsiblePersonId = dto.ResponsiblePersonId,
        DueDate = dto.DueDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyIncidentCorrectiveAction e, UpdateSafetyIncidentCorrectiveActionDto dto, Guid userId)
    {
        e.ActionDescription = dto.ActionDescription;
        e.Priority = dto.Priority;
        e.Status = dto.Status;
        e.ResponsiblePersonId = dto.ResponsiblePersonId;
        e.DueDate = dto.DueDate;
        e.CompletionDate = dto.CompletionDate;
        e.CompletionNotes = dto.CompletionNotes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region SafetyIncidentFollowUp

    public static SafetyIncidentFollowUpDto ToDto(this SafetyIncidentFollowUp e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentId = e.IncidentId,
        FollowUpDate = e.FollowUpDate,
        ActionsTaken = e.ActionsTaken,
        PersonCondition = e.PersonCondition,
        Notes = e.Notes,
        FurtherFollowUpRequired = e.FurtherFollowUpRequired,
        NextFollowUpDate = e.NextFollowUpDate,
        ConductedById = e.ConductedById,
        ConductedByName = e.ConductedBy?.FullName ?? string.Empty,
    };

    public static SafetyIncidentFollowUp ToEntity(this CreateSafetyIncidentFollowUpDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentId = dto.IncidentId,
        FollowUpDate = dto.FollowUpDate,
        ActionsTaken = dto.ActionsTaken,
        PersonCondition = dto.PersonCondition,
        Notes = dto.Notes,
        FurtherFollowUpRequired = dto.FurtherFollowUpRequired,
        NextFollowUpDate = dto.NextFollowUpDate,
        ConductedById = dto.ConductedById,
        CreatedBy = userId.ToString(),
    };

    #endregion

    #region SafetyIncidentDocument

    public static SafetyIncidentDocumentDto ToDto(this SafetyIncidentDocument e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentId = e.IncidentId,
        FileName = e.FileName,
        FilePath = e.FilePath,
        Type = e.Type,
        Description = e.Description,
        UploadDate = e.UploadDate,
        UploadedById = e.UploadedById,
        UploadedByName = e.UploadedBy?.FullName ?? string.Empty,
    };

    public static SafetyIncidentDocument ToEntity(this CreateSafetyIncidentDocumentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentId = dto.IncidentId,
        FileName = dto.FileName,
        FilePath = dto.FilePath,
        Type = dto.Type,
        Description = dto.Description,
        UploadDate = DateTime.UtcNow,
        UploadedById = dto.UploadedById,
        CreatedBy = userId.ToString(),
    };

    #endregion
}
