using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Application.HR.Extensions;

// ============================================================================
// SHE mapping — Hazard Register & Risk Assessment (C) and Inspections (D).
// ============================================================================

public static class SafetyHazardInspectionMappingExtensions
{
    // Risk-score → risk-level helpers (derive a consistent level from likelihood × severity).
    private static SheHazardRiskLevel MapHazardRiskLevel(int score) => score switch
    {
        <= 3 => SheHazardRiskLevel.VeryLow,
        <= 6 => SheHazardRiskLevel.Low,
        <= 10 => SheHazardRiskLevel.Medium,
        <= 15 => SheHazardRiskLevel.High,
        <= 20 => SheHazardRiskLevel.VeryHigh,
        _ => SheHazardRiskLevel.Critical,
    };

    private static SheRiskLevel MapRiskLevel(int score) => score switch
    {
        <= 4 => SheRiskLevel.Negligible,
        <= 8 => SheRiskLevel.Low,
        <= 12 => SheRiskLevel.Medium,
        <= 16 => SheRiskLevel.High,
        _ => SheRiskLevel.Critical,
    };

    // ========================================================================
    // C. HAZARD REGISTER
    // ========================================================================

    #region Hazard

    public static SheHazardDto ToDto(this SheHazard e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Code = e.Code,
        Name = e.Name,
        Category = e.Category,
        Description = e.Description,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        InherentLikelihood = e.InherentLikelihood,
        InherentSeverity = e.InherentSeverity,
        InherentRiskScore = e.InherentRiskScore,
        ResidualLikelihood = e.ResidualLikelihood,
        ResidualSeverity = e.ResidualSeverity,
        ResidualRiskScore = e.ResidualRiskScore,
        ResidualRiskLevel = e.ResidualRiskLevel,
        Status = e.Status,
        OwnerId = e.OwnerId,
        OwnerName = e.Owner?.FullName,
        ReviewDueDate = e.ReviewDueDate,
        LastReviewedDate = e.LastReviewedDate,
        LastReviewedById = e.LastReviewedById,
        LastReviewedByName = e.LastReviewedBy?.FullName,
        ReportedById = e.ReportedById,
        ReportedByName = e.ReportedBy?.FullName,
        ReportedDate = e.ReportedDate,
        IsActive = e.IsActive,
        Controls = e.Controls.Select(c => c.ToDto()).ToList(),
        CorrectiveActions = e.CorrectiveActions.Select(a => a.ToDto()).ToList(),
    };

    public static SheHazardSummaryDto ToSummaryDto(this SheHazard e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        Category = e.Category,
        LocationName = e.Location?.Name,
        InherentRiskScore = e.InherentRiskScore,
        ResidualRiskScore = e.ResidualRiskScore,
        ResidualRiskLevel = e.ResidualRiskLevel,
        Status = e.Status,
        OwnerName = e.Owner?.FullName,
        ReportedByName = e.ReportedBy?.FullName,
        ReviewDueDate = e.ReviewDueDate,
        IsActive = e.IsActive,
    };

    public static SheHazard ToEntity(this CreateSheHazardDto dto, Guid tenantId, Guid userId)
    {
        var inherent = dto.InherentLikelihood * dto.InherentSeverity;
        var residual = dto.ResidualLikelihood * dto.ResidualSeverity;
        return new SheHazard
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
            LocationId = dto.LocationId,
            SpecificArea = dto.SpecificArea,
            InherentLikelihood = dto.InherentLikelihood,
            InherentSeverity = dto.InherentSeverity,
            InherentRiskScore = inherent,
            ResidualLikelihood = dto.ResidualLikelihood,
            ResidualSeverity = dto.ResidualSeverity,
            ResidualRiskScore = residual,
            ResidualRiskLevel = MapHazardRiskLevel(residual),
            Status = SheHazardStatus.Identified,
            OwnerId = dto.OwnerId,
            ReviewDueDate = dto.ReviewDueDate,
            IsActive = dto.IsActive,
            ReportedById = dto.ReportedById,
            ReportedDate = dto.ReportedById is null ? null : DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SheHazard e, UpdateSheHazardDto dto, Guid userId)
    {
        e.Code = dto.Code;
        e.Name = dto.Name;
        e.Category = dto.Category;
        e.Description = dto.Description;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.InherentLikelihood = dto.InherentLikelihood;
        e.InherentSeverity = dto.InherentSeverity;
        e.InherentRiskScore = dto.InherentLikelihood * dto.InherentSeverity;
        e.ResidualLikelihood = dto.ResidualLikelihood;
        e.ResidualSeverity = dto.ResidualSeverity;
        e.ResidualRiskScore = dto.ResidualLikelihood * dto.ResidualSeverity;
        e.ResidualRiskLevel = MapHazardRiskLevel(e.ResidualRiskScore);
        e.Status = dto.Status;
        e.OwnerId = dto.OwnerId;
        e.ReviewDueDate = dto.ReviewDueDate;
        e.LastReviewedDate = dto.LastReviewedDate;
        e.LastReviewedById = dto.LastReviewedById;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheHazardSummaryDto> ToSummaryDtoList(this IEnumerable<SheHazard> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheHazardControlDto ToDto(this SheHazardControl e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        HazardId = e.HazardId,
        ControlLevel = e.ControlLevel,
        ControlDescription = e.ControlDescription,
        Status = e.Status,
        ResponsiblePersonId = e.ResponsiblePersonId,
        ResponsiblePersonName = e.ResponsiblePerson?.FullName,
        ImplementationDate = e.ImplementationDate,
        ReviewDate = e.ReviewDate,
    };

    public static SheHazardControl ToEntity(this CreateSheHazardControlDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        HazardId = dto.HazardId,
        ControlLevel = dto.ControlLevel,
        ControlDescription = dto.ControlDescription,
        Status = dto.Status,
        ResponsiblePersonId = dto.ResponsiblePersonId,
        ImplementationDate = dto.ImplementationDate,
        ReviewDate = dto.ReviewDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheHazardControl e, UpdateSheHazardControlDto dto, Guid userId)
    {
        e.ControlLevel = dto.ControlLevel;
        e.ControlDescription = dto.ControlDescription;
        e.Status = dto.Status;
        e.ResponsiblePersonId = dto.ResponsiblePersonId;
        e.ImplementationDate = dto.ImplementationDate;
        e.ReviewDate = dto.ReviewDate;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SheHazardCorrectiveActionDto ToDto(this SheHazardCorrectiveAction e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        HazardId = e.HazardId,
        CorrectiveActionTemplateId = e.CorrectiveActionTemplateId,
        CorrectiveActionTemplateTitle = e.CorrectiveActionTemplate?.Title ?? string.Empty,
        DeadlineDays = e.DeadlineDays,
        IsMandatory = e.IsMandatory,
        DisplayOrder = e.DisplayOrder,
    };

    public static SheHazardCorrectiveAction ToEntity(this CreateSheHazardCorrectiveActionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        HazardId = dto.HazardId,
        CorrectiveActionTemplateId = dto.CorrectiveActionTemplateId,
        DeadlineDays = dto.DeadlineDays,
        IsMandatory = dto.IsMandatory,
        DisplayOrder = dto.DisplayOrder,
        CreatedBy = userId.ToString(),
    };

    #endregion

    #region RiskAssessment

    public static SheRiskAssessmentDto ToDto(this SheRiskAssessment e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        AssessmentNumber = e.AssessmentNumber,
        Title = e.Title,
        Type = e.Type,
        Scope = e.Scope,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificActivity = e.SpecificActivity,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        Status = e.Status,
        PreparedById = e.PreparedById,
        PreparedByName = e.PreparedBy?.FullName ?? string.Empty,
        PreparedDate = e.PreparedDate,
        ReviewedById = e.ReviewedById,
        ReviewedByName = e.ReviewedBy?.FullName,
        ReviewedDate = e.ReviewedDate,
        ApprovedById = e.ApprovedById,
        ApprovedByName = e.ApprovedBy?.FullName,
        ApprovedDate = e.ApprovedDate,
        ValidFrom = e.ValidFrom,
        ValidUntil = e.ValidUntil,
        NextReviewDate = e.NextReviewDate,
        Version = e.Version,
        DocumentPath = e.DocumentPath,
        AssessedHazards = e.AssessedHazards.Select(h => h.ToDto()).ToList(),
        Acknowledgements = e.Acknowledgements.Select(a => a.ToDto()).ToList(),
    };

    public static SheRiskAssessmentSummaryDto ToSummaryDto(this SheRiskAssessment e) => new()
    {
        Id = e.Id,
        AssessmentNumber = e.AssessmentNumber,
        Title = e.Title,
        Type = e.Type,
        Status = e.Status,
        LocationName = e.Location?.Name,
        PreparedByName = e.PreparedBy?.FullName ?? string.Empty,
        PreparedDate = e.PreparedDate,
        ValidUntil = e.ValidUntil,
        NextReviewDate = e.NextReviewDate,
        Version = e.Version,
        HazardCount = e.AssessedHazards.Count,
    };

    public static SheRiskAssessment ToEntity(this CreateSheRiskAssessmentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Title = dto.Title,
        Type = dto.Type,
        Scope = dto.Scope,
        LocationId = dto.LocationId,
        SpecificActivity = dto.SpecificActivity,
        OrganizationUnitId = dto.OrganizationUnitId,
        Status = SheRiskAssessmentStatus.Draft,
        PreparedById = dto.PreparedById,
        PreparedDate = dto.PreparedDate,
        ValidFrom = dto.ValidFrom,
        ValidUntil = dto.ValidUntil,
        NextReviewDate = dto.NextReviewDate,
        Version = 1,
        DocumentPath = dto.DocumentPath,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheRiskAssessment e, UpdateSheRiskAssessmentDto dto, Guid userId)
    {
        e.Title = dto.Title;
        e.Type = dto.Type;
        e.Scope = dto.Scope;
        e.LocationId = dto.LocationId;
        e.SpecificActivity = dto.SpecificActivity;
        e.OrganizationUnitId = dto.OrganizationUnitId;
        e.Status = dto.Status;
        e.ValidFrom = dto.ValidFrom;
        e.ValidUntil = dto.ValidUntil;
        e.NextReviewDate = dto.NextReviewDate;
        e.DocumentPath = dto.DocumentPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheRiskAssessmentSummaryDto> ToSummaryDtoList(this IEnumerable<SheRiskAssessment> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheRiskAssessmentHazardDto ToDto(this SheRiskAssessmentHazard e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        RiskAssessmentId = e.RiskAssessmentId,
        HazardId = e.HazardId,
        ItemNumber = e.ItemNumber,
        HazardDescription = e.HazardDescription,
        PotentialConsequences = e.PotentialConsequences,
        AffectedPersons = e.AffectedPersons,
        InherentLikelihood = e.InherentLikelihood,
        InherentSeverity = e.InherentSeverity,
        InherentRiskScore = e.InherentRiskScore,
        InherentRiskLevel = e.InherentRiskLevel,
        ControlMeasures = e.ControlMeasures,
        ResidualLikelihood = e.ResidualLikelihood,
        ResidualSeverity = e.ResidualSeverity,
        ResidualRiskScore = e.ResidualRiskScore,
        ResidualRiskLevel = e.ResidualRiskLevel,
        ResponsiblePerson = e.ResponsiblePerson,
        TargetDate = e.TargetDate,
    };

    public static SheRiskAssessmentHazard ToEntity(this CreateSheRiskAssessmentHazardDto dto, Guid tenantId, Guid userId)
    {
        var inherent = dto.InherentLikelihood * dto.InherentSeverity;
        var residual = dto.ResidualLikelihood * dto.ResidualSeverity;
        return new SheRiskAssessmentHazard
        {
            TenantId = tenantId,
            RiskAssessmentId = dto.RiskAssessmentId,
            HazardId = dto.HazardId,
            ItemNumber = dto.ItemNumber,
            HazardDescription = dto.HazardDescription,
            PotentialConsequences = dto.PotentialConsequences,
            AffectedPersons = dto.AffectedPersons,
            InherentLikelihood = dto.InherentLikelihood,
            InherentSeverity = dto.InherentSeverity,
            InherentRiskScore = inherent,
            InherentRiskLevel = MapRiskLevel(inherent),
            ControlMeasures = dto.ControlMeasures,
            ResidualLikelihood = dto.ResidualLikelihood,
            ResidualSeverity = dto.ResidualSeverity,
            ResidualRiskScore = residual,
            ResidualRiskLevel = MapRiskLevel(residual),
            ResponsiblePerson = dto.ResponsiblePerson,
            TargetDate = dto.TargetDate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this SheRiskAssessmentHazard e, UpdateSheRiskAssessmentHazardDto dto, Guid userId)
    {
        e.ItemNumber = dto.ItemNumber;
        e.HazardDescription = dto.HazardDescription;
        e.PotentialConsequences = dto.PotentialConsequences;
        e.AffectedPersons = dto.AffectedPersons;
        e.InherentLikelihood = dto.InherentLikelihood;
        e.InherentSeverity = dto.InherentSeverity;
        e.InherentRiskScore = dto.InherentLikelihood * dto.InherentSeverity;
        e.InherentRiskLevel = MapRiskLevel(e.InherentRiskScore);
        e.ControlMeasures = dto.ControlMeasures;
        e.ResidualLikelihood = dto.ResidualLikelihood;
        e.ResidualSeverity = dto.ResidualSeverity;
        e.ResidualRiskScore = dto.ResidualLikelihood * dto.ResidualSeverity;
        e.ResidualRiskLevel = MapRiskLevel(e.ResidualRiskScore);
        e.ResponsiblePerson = dto.ResponsiblePerson;
        e.TargetDate = dto.TargetDate;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SheRiskAssessmentAcknowledgementDto ToDto(this SheRiskAssessmentAcknowledgement e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        RiskAssessmentId = e.RiskAssessmentId,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        AcknowledgedDate = e.AcknowledgedDate,
        SignaturePath = e.SignaturePath,
        Comments = e.Comments,
    };

    public static SheRiskAssessmentAcknowledgement ToEntity(this CreateSheRiskAssessmentAcknowledgementDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        RiskAssessmentId = dto.RiskAssessmentId,
        EmployeeId = dto.EmployeeId,
        AcknowledgedDate = dto.AcknowledgedDate,
        SignaturePath = dto.SignaturePath,
        Comments = dto.Comments,
        CreatedBy = userId.ToString(),
    };

    #endregion

    // ========================================================================
    // D. SAFETY INSPECTIONS & AUDITS
    // ========================================================================

    #region InspectionChecklist

    // ── Fields ──
    public static SheInspectionChecklistFieldDto ToDto(this SheInspectionChecklistField e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ChecklistId = e.ChecklistId,
        DisplayOrder = e.DisplayOrder,
        Label = e.Label,
        FieldType = e.FieldType,
        IsRequired = e.IsRequired,
        ChoiceOptions = e.ChoiceOptions,
        HelpText = e.HelpText,
    };

    public static SheInspectionChecklistField ToEntity(this CreateSheInspectionChecklistFieldDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ChecklistId = dto.ChecklistId,
        DisplayOrder = dto.DisplayOrder,
        Label = dto.Label,
        FieldType = dto.FieldType,
        IsRequired = dto.IsRequired,
        ChoiceOptions = dto.ChoiceOptions,
        HelpText = dto.HelpText,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheInspectionChecklistField e, UpdateSheInspectionChecklistFieldDto dto, Guid userId)
    {
        e.DisplayOrder = dto.DisplayOrder;
        e.Label = dto.Label;
        e.FieldType = dto.FieldType;
        e.IsRequired = dto.IsRequired;
        e.ChoiceOptions = dto.ChoiceOptions;
        e.HelpText = dto.HelpText;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    // ── Sections ──
    public static SheInspectionChecklistSectionDto ToDto(this SheInspectionChecklistSection e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ChecklistId = e.ChecklistId,
        DisplayOrder = e.DisplayOrder,
        Code = e.Code,
        Title = e.Title,
        Description = e.Description,
        Kind = e.Kind,
    };

    public static SheInspectionChecklistSection ToEntity(this CreateSheInspectionChecklistSectionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ChecklistId = dto.ChecklistId,
        DisplayOrder = dto.DisplayOrder,
        Code = dto.Code,
        Title = dto.Title,
        Description = dto.Description,
        Kind = dto.Kind,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheInspectionChecklistSection e, UpdateSheInspectionChecklistSectionDto dto, Guid userId)
    {
        e.DisplayOrder = dto.DisplayOrder;
        e.Code = dto.Code;
        e.Title = dto.Title;
        e.Description = dto.Description;
        e.Kind = dto.Kind;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    // ── Outcomes ──
    public static SheInspectionChecklistOutcomeDto ToDto(this SheInspectionChecklistOutcome e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ChecklistId = e.ChecklistId,
        DisplayOrder = e.DisplayOrder,
        Label = e.Label,
        Description = e.Description,
        MinPercent = e.MinPercent,
        MaxPercent = e.MaxPercent,
        ReinspectionWithinDays = e.ReinspectionWithinDays,
        IsDisqualifying = e.IsDisqualifying,
    };

    public static SheInspectionChecklistOutcome ToEntity(this CreateSheInspectionChecklistOutcomeDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ChecklistId = dto.ChecklistId,
        DisplayOrder = dto.DisplayOrder,
        Label = dto.Label,
        Description = dto.Description,
        MinPercent = dto.MinPercent,
        MaxPercent = dto.MaxPercent,
        ReinspectionWithinDays = dto.ReinspectionWithinDays,
        IsDisqualifying = dto.IsDisqualifying,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheInspectionChecklistOutcome e, UpdateSheInspectionChecklistOutcomeDto dto, Guid userId)
    {
        e.DisplayOrder = dto.DisplayOrder;
        e.Label = dto.Label;
        e.Description = dto.Description;
        e.MinPercent = dto.MinPercent;
        e.MaxPercent = dto.MaxPercent;
        e.ReinspectionWithinDays = dto.ReinspectionWithinDays;
        e.IsDisqualifying = dto.IsDisqualifying;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    // ── Signatories ──
    public static SheInspectionChecklistSignatoryDto ToDto(this SheInspectionChecklistSignatory e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ChecklistId = e.ChecklistId,
        DisplayOrder = e.DisplayOrder,
        RoleLabel = e.RoleLabel,
        Kind = e.Kind,
        IsRequired = e.IsRequired,
    };

    public static SheInspectionChecklistSignatory ToEntity(this CreateSheInspectionChecklistSignatoryDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ChecklistId = dto.ChecklistId,
        DisplayOrder = dto.DisplayOrder,
        RoleLabel = dto.RoleLabel,
        Kind = dto.Kind,
        IsRequired = dto.IsRequired,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheInspectionChecklistSignatory e, UpdateSheInspectionChecklistSignatoryDto dto, Guid userId)
    {
        e.DisplayOrder = dto.DisplayOrder;
        e.RoleLabel = dto.RoleLabel;
        e.Kind = dto.Kind;
        e.IsRequired = dto.IsRequired;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    // ── Items ──
    public static SheInspectionChecklistItemDto ToDto(this SheInspectionChecklistItem e, int itemNumber = 0) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ChecklistId = e.ChecklistId,
        SectionId = e.SectionId,
        ItemOrder = e.ItemOrder,
        ItemNumber = itemNumber,
        Category = e.Category,
        ItemDescription = e.ItemDescription,
        IsMandatory = e.IsMandatory,
        RegulatoryReference = e.RegulatoryReference,
        AssociatedRiskLevel = e.AssociatedRiskLevel,
    };

    public static SheInspectionChecklistItem ToEntity(this CreateSheInspectionChecklistItemDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ChecklistId = dto.ChecklistId,
        SectionId = dto.SectionId,
        ItemOrder = dto.ItemOrder,
        Category = dto.Category,
        ItemDescription = dto.ItemDescription,
        IsMandatory = dto.IsMandatory,
        RegulatoryReference = dto.RegulatoryReference,
        AssociatedRiskLevel = dto.AssociatedRiskLevel,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheInspectionChecklistItem e, UpdateSheInspectionChecklistItemDto dto, Guid userId)
    {
        // An omitted section means "unchanged": callers that predate the builder (harness slice 4,
        // the old flat item dialog) never send one, and stripping it would make the template
        // unpublishable. Items move between sections by naming the target section.
        if (dto.SectionId != null)
            e.SectionId = dto.SectionId;
        e.ItemOrder = dto.ItemOrder;
        e.Category = dto.Category;
        e.ItemDescription = dto.ItemDescription;
        e.IsMandatory = dto.IsMandatory;
        e.RegulatoryReference = dto.RegulatoryReference;
        e.AssociatedRiskLevel = dto.AssociatedRiskLevel;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    // ── Template ──

    /// <summary>
    /// The template's items laid out as the form prints them: real sections in DisplayOrder, each
    /// with its items in ItemOrder; then any legacy section-less items grouped by Category into
    /// synthetic sections (Id = Guid.Empty). Standard-section items get a running ItemNumber.
    /// </summary>
    public static List<SheInspectionChecklistSectionDto> LayOutSections(this SheInspectionChecklist e)
    {
        var live = e.Items.Where(i => !i.IsDeleted).ToList();
        var sections = e.Sections.Where(s => !s.IsDeleted).OrderBy(s => s.DisplayOrder).ThenBy(s => s.CreatedAt)
            .Select(s =>
            {
                var dto = s.ToDto();
                dto.Items = live.Where(i => i.SectionId == s.Id)
                    .OrderBy(i => i.ItemOrder).ThenBy(i => i.CreatedAt)
                    .Select(i => i.ToDto()).ToList();
                return dto;
            }).ToList();

        var orphans = live.Where(i => i.SectionId == null).OrderBy(i => i.ItemOrder).ThenBy(i => i.CreatedAt).ToList();
        foreach (var group in orphans.GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "General" : i.Category!.Trim()))
        {
            sections.Add(new SheInspectionChecklistSectionDto
            {
                Id = Guid.Empty,
                ChecklistId = e.Id,
                DisplayOrder = sections.Count + 1,
                Title = group.Key,
                Kind = SheChecklistSectionKind.Standard,
                Items = group.Select(i => i.ToDto()).ToList(),
            });
        }

        var number = 0;
        foreach (var s in sections)
            foreach (var i in s.Items)
                i.ItemNumber = s.Kind == SheChecklistSectionKind.Standard ? ++number : 0;

        return sections;
    }

    public static SheInspectionChecklistDto ToDto(this SheInspectionChecklist e)
    {
        var sections = e.LayOutSections();
        return new()
        {
            Id = e.Id,
            TenantId = e.TenantId,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? string.Empty,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy,
            ChecklistNumber = e.ChecklistNumber,
            Name = e.Name,
            Description = e.Description,
            Type = e.Type,
            Version = e.Version,
            IsActive = e.IsActive,
            Status = e.Status,
            ScoringMode = e.ScoringMode,
            AllowPartialCompliance = e.AllowPartialCompliance,
            PrintTitle = e.PrintTitle,
            PrintSubtitle = e.PrintSubtitle,
            Instructions = e.Instructions,
            CriticalSectionNote = e.CriticalSectionNote,
            PublishedAt = e.PublishedAt,
            PublishedById = e.PublishedById,
            PublishedByName = e.PublishedBy?.FullName,
            RetiredAt = e.RetiredAt,
            PreviousVersionId = e.PreviousVersionId,
            ItemCount = e.Items.Count(i => !i.IsDeleted),
            Fields = e.Fields.Where(f => !f.IsDeleted).OrderBy(f => f.DisplayOrder).ThenBy(f => f.CreatedAt).Select(f => f.ToDto()).ToList(),
            Sections = sections,
            Items = sections.SelectMany(s => s.Items).ToList(),
            Outcomes = e.Outcomes.Where(o => !o.IsDeleted).OrderBy(o => o.DisplayOrder).ThenBy(o => o.CreatedAt).Select(o => o.ToDto()).ToList(),
            Signatories = e.Signatories.Where(s => !s.IsDeleted).OrderBy(s => s.DisplayOrder).ThenBy(s => s.CreatedAt).Select(s => s.ToDto()).ToList(),
        };
    }

    public static SheInspectionChecklist ToEntity(this CreateSheInspectionChecklistDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ChecklistNumber = dto.ChecklistNumber,
        Name = dto.Name,
        Description = dto.Description,
        Type = dto.Type,
        Version = dto.Version,
        IsActive = dto.IsActive,
        Status = SheChecklistStatus.Draft,
        ScoringMode = dto.ScoringMode,
        AllowPartialCompliance = dto.AllowPartialCompliance,
        PrintTitle = dto.PrintTitle,
        PrintSubtitle = dto.PrintSubtitle,
        Instructions = dto.Instructions,
        CriticalSectionNote = dto.CriticalSectionNote,
        CreatedBy = userId.ToString(),
    };

    /// <summary>True when the update would change a field that is frozen once the template is published.</summary>
    public static bool ChangesLockedStructure(this SheInspectionChecklist e, UpdateSheInspectionChecklistDto dto) =>
        e.Type != dto.Type
        || e.Version != dto.Version
        || e.ScoringMode != dto.ScoringMode
        || e.AllowPartialCompliance != dto.AllowPartialCompliance
        || (e.PrintTitle ?? string.Empty) != (dto.PrintTitle ?? string.Empty)
        || (e.PrintSubtitle ?? string.Empty) != (dto.PrintSubtitle ?? string.Empty)
        || (e.Instructions ?? string.Empty) != (dto.Instructions ?? string.Empty)
        || (e.CriticalSectionNote ?? string.Empty) != (dto.CriticalSectionNote ?? string.Empty);

    public static void UpdateEntity(this SheInspectionChecklist e, UpdateSheInspectionChecklistDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Description = dto.Description;
        e.IsActive = dto.IsActive;
        if (e.Status == SheChecklistStatus.Draft)
        {
            e.Type = dto.Type;
            e.Version = dto.Version;
            e.ScoringMode = dto.ScoringMode;
            e.AllowPartialCompliance = dto.AllowPartialCompliance;
            e.PrintTitle = dto.PrintTitle;
            e.PrintSubtitle = dto.PrintSubtitle;
            e.Instructions = dto.Instructions;
            e.CriticalSectionNote = dto.CriticalSectionNote;
        }
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region SafetyInspection

    public static SafetyInspectionDto ToDto(this SafetyInspection e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InspectionNumber = e.InspectionNumber,
        InspectionDate = e.InspectionDate,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        Type = e.Type,
        Category = e.Category,
        ChecklistId = e.ChecklistId,
        ChecklistName = e.Checklist?.Name,
        InspectorId = e.InspectorId,
        InspectorName = e.Inspector?.FullName ?? string.Empty,
        ExternalInspectorName = e.ExternalInspectorName,
        ExternalInspectorOrganization = e.ExternalInspectorOrganization,
        FindingsAndObservations = e.FindingsAndObservations,
        RecommendedActions = e.RecommendedActions,
        PositiveObservations = e.PositiveObservations,
        Status = e.Status,
        OverallRiskRating = e.OverallRiskRating,
        ComplianceScore = e.ComplianceScore,
        ComplianceDeadline = e.ComplianceDeadline,
        NextInspectionDueDate = e.NextInspectionDueDate,
        ClosedDate = e.ClosedDate,
        ClosedById = e.ClosedById,
        ClosedByName = e.ClosedBy?.FullName,
        // ── Checklist run ──
        ChecklistNumber = e.Checklist?.ChecklistNumber,
        ChecklistVersion = e.Checklist?.Version,
        ScoringMode = e.Checklist?.ScoringMode,
        TotalApplicableItems = e.TotalApplicableItems,
        TotalCompliantItems = e.TotalCompliantItems,
        TotalNonCompliantItems = e.TotalNonCompliantItems,
        TotalPartiallyCompliantItems = e.TotalPartiallyCompliantItems,
        CriticalNonConformityCount = e.CriticalNonConformityCount,
        CompliancePercentage = e.CompliancePercentage,
        RecommendedOutcomeId = e.RecommendedOutcomeId,
        RecommendedOutcomeLabel = e.RecommendedOutcome?.Label,
        OutcomeId = e.OutcomeId,
        OutcomeLabel = e.Outcome?.Label,
        OutcomeIsDisqualifying = e.Outcome?.IsDisqualifying,
        OutcomeReinspectionWithinDays = e.Outcome?.ReinspectionWithinDays,
        OutcomeOverrideReason = e.OutcomeOverrideReason,
        SubjectComments = e.SubjectComments,
        CompletedAt = e.CompletedAt,
        CompletedById = e.CompletedById,
        CompletedByName = e.CompletedBy?.FullName,
        Checklist = e.Checklist?.ToDto(),
        FieldValues = e.FieldValues.Where(v => !v.IsDeleted).OrderBy(v => v.ChecklistField?.DisplayOrder ?? 0).ThenBy(v => v.CreatedAt).Select(v => v.ToDto()).ToList(),
        Signatures = e.Signatures.Where(x => !x.IsDeleted).OrderBy(x => x.ChecklistSignatory?.DisplayOrder ?? 0).ThenBy(x => x.SignedAt).Select(x => x.ToDto()).ToList(),
        // Materialised items in form order first, hand-added findings (DisplayOrder 0) after.
        Items = e.Items.Where(i => !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder == 0 ? int.MaxValue : i.DisplayOrder).ThenBy(i => i.CreatedAt)
            .Select(i => i.ToDto()).NumberStandardItems(),
        Hazards = e.Hazards.Select(h => h.ToDto()).ToList(),
        Documents = e.Documents.Select(d => d.ToDto()).ToList(),
    };

    public static SafetyInspectionSummaryDto ToSummaryDto(this SafetyInspection e) => new()
    {
        Id = e.Id,
        InspectionNumber = e.InspectionNumber,
        InspectionDate = e.InspectionDate,
        Type = e.Type,
        Category = e.Category,
        LocationName = e.Location?.Name,
        InspectorName = e.Inspector?.FullName ?? string.Empty,
        Status = e.Status,
        OverallRiskRating = e.OverallRiskRating,
        ComplianceScore = e.ComplianceScore,
        NextInspectionDueDate = e.NextInspectionDueDate,
        OpenItemCount = e.Items.Count(i => !i.IsResolved),
    };

    public static SafetyInspection ToEntity(this CreateSafetyInspectionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        InspectionDate = dto.InspectionDate,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        OrganizationUnitId = dto.OrganizationUnitId,
        Type = dto.Type,
        Category = dto.Category,
        ChecklistId = dto.ChecklistId,
        InspectorId = dto.InspectorId,
        ExternalInspectorName = dto.ExternalInspectorName,
        ExternalInspectorOrganization = dto.ExternalInspectorOrganization,
        Status = SheInspectionStatus.Scheduled,
        NextInspectionDueDate = dto.NextInspectionDueDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyInspection e, UpdateSafetyInspectionDto dto, Guid userId)
    {
        e.InspectionDate = dto.InspectionDate;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.OrganizationUnitId = dto.OrganizationUnitId;
        e.Type = dto.Type;
        e.Category = dto.Category;
        e.ChecklistId = dto.ChecklistId;
        e.InspectorId = dto.InspectorId;
        e.ExternalInspectorName = dto.ExternalInspectorName;
        e.ExternalInspectorOrganization = dto.ExternalInspectorOrganization;
        e.FindingsAndObservations = dto.FindingsAndObservations;
        e.RecommendedActions = dto.RecommendedActions;
        e.PositiveObservations = dto.PositiveObservations;
        e.Status = dto.Status;
        e.OverallRiskRating = dto.OverallRiskRating;
        e.ComplianceScore = dto.ComplianceScore;
        e.ComplianceDeadline = dto.ComplianceDeadline;
        e.NextInspectionDueDate = dto.NextInspectionDueDate;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SafetyInspectionSummaryDto> ToSummaryDtoList(this IEnumerable<SafetyInspection> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SafetyInspectionItemDto ToDto(this SafetyInspectionItem e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InspectionId = e.InspectionId,
        ChecklistItemId = e.ChecklistItemId,
        DisplayOrder = e.DisplayOrder,
        SectionId = e.ChecklistItem?.SectionId,
        SectionCode = e.ChecklistItem?.Section?.Code,
        SectionTitle = e.ChecklistItem?.Section?.Title ?? e.ChecklistItem?.Category,
        SectionKind = e.ChecklistItem == null ? null : (e.ChecklistItem.Section?.Kind ?? SheChecklistSectionKind.Standard),
        ItemDescription = e.ItemDescription,
        Status = e.Status,
        DeficiencyNoted = e.DeficiencyNoted,
        ActionRequired = e.ActionRequired,
        RiskLevel = e.RiskLevel,
        TargetDate = e.TargetDate,
        ResponsiblePersonId = e.ResponsiblePersonId,
        ResponsiblePersonName = e.ResponsiblePerson?.FullName,
        IsResolved = e.IsResolved,
        ResolvedDate = e.ResolvedDate,
        ResolutionNotes = e.ResolutionNotes,
        ResolvedById = e.ResolvedById,
        ResolvedByName = e.ResolvedBy?.FullName,
    };

    public static SafetyInspectionItem ToEntity(this CreateSafetyInspectionItemDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        InspectionId = dto.InspectionId,
        ChecklistItemId = dto.ChecklistItemId,
        ItemDescription = dto.ItemDescription,
        Status = dto.Status,
        DeficiencyNoted = dto.DeficiencyNoted,
        ActionRequired = dto.ActionRequired,
        RiskLevel = dto.RiskLevel,
        TargetDate = dto.TargetDate,
        ResponsiblePersonId = dto.ResponsiblePersonId,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyInspectionItem e, UpdateSafetyInspectionItemDto dto, Guid userId)
    {
        e.ItemDescription = dto.ItemDescription;
        e.Status = dto.Status;
        e.DeficiencyNoted = dto.DeficiencyNoted;
        e.ActionRequired = dto.ActionRequired;
        e.RiskLevel = dto.RiskLevel;
        e.TargetDate = dto.TargetDate;
        e.ResponsiblePersonId = dto.ResponsiblePersonId;
        e.IsResolved = dto.IsResolved;
        e.ResolvedDate = dto.ResolvedDate;
        e.ResolutionNotes = dto.ResolutionNotes;
        e.ResolvedById = dto.ResolvedById;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyInspectionHazardDto ToDto(this SafetyInspectionHazard e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InspectionId = e.InspectionId,
        HazardId = e.HazardId,
        HazardDescription = e.HazardDescription,
        Status = e.Status,
        InitialRiskLevel = e.InitialRiskLevel,
        ResidualRiskLevel = e.ResidualRiskLevel,
        ReviewDueDate = e.ReviewDueDate,
        OwnerId = e.OwnerId,
        OwnerName = e.Owner?.FullName,
        Actions = e.Actions.Select(a => a.ToDto()).ToList(),
    };

    public static SafetyInspectionHazard ToEntity(this CreateSafetyInspectionHazardDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        InspectionId = dto.InspectionId,
        HazardId = dto.HazardId,
        HazardDescription = dto.HazardDescription,
        Status = dto.Status,
        InitialRiskLevel = dto.InitialRiskLevel,
        ResidualRiskLevel = dto.ResidualRiskLevel,
        ReviewDueDate = dto.ReviewDueDate,
        OwnerId = dto.OwnerId,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyInspectionHazard e, UpdateSafetyInspectionHazardDto dto, Guid userId)
    {
        e.HazardId = dto.HazardId;
        e.HazardDescription = dto.HazardDescription;
        e.Status = dto.Status;
        e.InitialRiskLevel = dto.InitialRiskLevel;
        e.ResidualRiskLevel = dto.ResidualRiskLevel;
        e.ReviewDueDate = dto.ReviewDueDate;
        e.OwnerId = dto.OwnerId;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyInspectionHazardActionDto ToDto(this SafetyInspectionHazardAction e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InspectionHazardId = e.InspectionHazardId,
        CorrectiveActionTemplateId = e.CorrectiveActionTemplateId,
        CorrectiveActionTemplateTitle = e.CorrectiveActionTemplate?.Title ?? string.Empty,
        Status = e.Status,
        DueDate = e.DueDate,
        CompletionDate = e.CompletionDate,
        CompletionNotes = e.CompletionNotes,
        AssignedToId = e.AssignedToId,
        AssignedToName = e.AssignedTo?.FullName,
    };

    public static SafetyInspectionHazardAction ToEntity(this CreateSafetyInspectionHazardActionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        InspectionHazardId = dto.InspectionHazardId,
        CorrectiveActionTemplateId = dto.CorrectiveActionTemplateId,
        Status = dto.Status,
        DueDate = dto.DueDate,
        AssignedToId = dto.AssignedToId,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyInspectionHazardAction e, UpdateSafetyInspectionHazardActionDto dto, Guid userId)
    {
        e.Status = dto.Status;
        e.DueDate = dto.DueDate;
        e.CompletionDate = dto.CompletionDate;
        e.CompletionNotes = dto.CompletionNotes;
        e.AssignedToId = dto.AssignedToId;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyInspectionDocumentDto ToDto(this SafetyInspectionDocument e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InspectionId = e.InspectionId,
        FileName = e.FileName,
        FilePath = e.FilePath,
        Description = e.Description,
        UploadDate = e.UploadDate,
        UploadedById = e.UploadedById,
        UploadedByName = e.UploadedBy?.FullName ?? string.Empty,
    };

    public static SafetyInspectionDocument ToEntity(this CreateSafetyInspectionDocumentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        InspectionId = dto.InspectionId,
        FileName = dto.FileName,
        FilePath = dto.FilePath,
        Description = dto.Description,
        UploadDate = DateTime.UtcNow,
        UploadedById = dto.UploadedById,
        CreatedBy = userId.ToString(),
    };

    #endregion
    #region ChecklistRun

    public static SafetyInspectionFieldValueDto ToDto(this SafetyInspectionFieldValue e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InspectionId = e.InspectionId,
        ChecklistFieldId = e.ChecklistFieldId,
        Label = e.ChecklistField?.Label ?? string.Empty,
        FieldType = e.ChecklistField?.FieldType ?? SheChecklistFieldType.Text,
        ValueText = e.ValueText,
        ValueReferenceId = e.ValueReferenceId,
        // Reference fields are resolved to a name by the service after mapping.
        ValueDisplay = e.ValueText,
    };

    public static SafetyInspectionSignatureDto ToDto(this SafetyInspectionSignature e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        InspectionId = e.InspectionId,
        ChecklistSignatoryId = e.ChecklistSignatoryId,
        RoleLabel = e.RoleLabel,
        Kind = e.ChecklistSignatory?.Kind ?? SheChecklistSignatoryKind.External,
        SignedByEmployeeId = e.SignedByEmployeeId,
        SignedByName = e.SignedBy?.FullName,
        SignedName = e.SignedName,
        SignedAt = e.SignedAt,
        Notes = e.Notes,
    };

    /// <summary>Assigns the printed running number to materialised standard-section items, in the order given.</summary>
    public static List<SafetyInspectionItemDto> NumberStandardItems(this IEnumerable<SafetyInspectionItemDto> items)
    {
        var list = items.ToList();
        var n = 0;
        foreach (var i in list)
            i.ItemNumber = i.DisplayOrder > 0 && i.SectionKind == SheChecklistSectionKind.Standard ? ++n : 0;
        return list;
    }

    #endregion
}
