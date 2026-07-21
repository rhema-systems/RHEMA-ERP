using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class StaffDisciplineMappingExtensions
{
    // ========================================================================
    // STAFF OFFENSE
    // ========================================================================

    #region StaffOffense

    public static StaffOffenseDto ToDto(this StaffOffense entity)
    {
        return new StaffOffenseDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            OffenseCode = entity.OffenseCode,
            OffenseName = entity.OffenseName,
            OffenseDescription = entity.OffenseDescription,
            IsActive = entity.IsActive,
            Procedures = entity.OffenseProcedures.Select(p => p.ToDto()).ToList(),
        };
    }

    public static StaffOffenseSummaryDto ToSummaryDto(this StaffOffense entity)
    {
        return new StaffOffenseSummaryDto
        {
            Id = entity.Id,
            OffenseCode = entity.OffenseCode,
            OffenseName = entity.OffenseName,
            IsActive = entity.IsActive,
            ProcedureCount = entity.OffenseProcedures.Count,
        };
    }

    public static StaffOffense ToEntity(this CreateStaffOffenseDto dto, Guid tenantId, Guid userId)
    {
        return new StaffOffense
        {
            TenantId = tenantId,
            OffenseCode = dto.OffenseCode,
            OffenseName = dto.OffenseName,
            OffenseDescription = dto.OffenseDescription,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffOffense entity, UpdateStaffOffenseDto dto, Guid userId)
    {
        entity.OffenseCode = dto.OffenseCode;
        entity.OffenseName = dto.OffenseName;
        entity.OffenseDescription = dto.OffenseDescription;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffOffenseSummaryDto> ToSummaryDtoList(this IEnumerable<StaffOffense> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF OFFENSE PROCEDURE
    // ========================================================================

    #region StaffOffenseProcedure

    public static StaffOffenseProcedureDto ToDto(this StaffOffenseProcedure entity)
    {
        return new StaffOffenseProcedureDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            OffenseId = entity.OffenseId,
            OffenseName = entity.Offense?.OffenseName ?? string.Empty,
            StepName = entity.StepName,
            StepDescription = entity.StepDescription,
            Sequence = entity.Sequence,
            ExpectedCompletionDays = entity.ExpectedCompletionDays,
        };
    }

    public static StaffOffenseProcedure ToEntity(this CreateStaffOffenseProcedureDto dto, Guid tenantId, Guid userId)
    {
        return new StaffOffenseProcedure
        {
            TenantId = tenantId,
            OffenseId = dto.OffenseId,
            StepName = dto.StepName,
            StepDescription = dto.StepDescription,
            Sequence = dto.Sequence,
            ExpectedCompletionDays = dto.ExpectedCompletionDays,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffOffenseProcedure entity, UpdateStaffOffenseProcedureDto dto, Guid userId)
    {
        entity.StepName = dto.StepName;
        entity.StepDescription = dto.StepDescription;
        entity.Sequence = dto.Sequence;
        entity.ExpectedCompletionDays = dto.ExpectedCompletionDays;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINARY ACTION TYPE
    // ========================================================================

    #region StaffDisciplinaryActionType

    public static StaffDisciplinaryActionTypeDto ToDto(this StaffDisciplinaryActionType entity)
    {
        return new StaffDisciplinaryActionTypeDto
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
            IsActive = entity.IsActive,
            DefaultSuspensionDays = entity.DefaultSuspensionDays,
            DefaultFineAmount = entity.DefaultFineAmount,
        };
    }

    public static StaffDisciplinaryActionTypeSummaryDto ToSummaryDto(this StaffDisciplinaryActionType entity)
    {
        return new StaffDisciplinaryActionTypeSummaryDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            IsActive = entity.IsActive,
        };
    }

    public static StaffDisciplinaryActionType ToEntity(this CreateStaffDisciplinaryActionTypeDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplinaryActionType
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            IsActive = dto.IsActive,
            DefaultSuspensionDays = dto.DefaultSuspensionDays,
            DefaultFineAmount = dto.DefaultFineAmount,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplinaryActionType entity, UpdateStaffDisciplinaryActionTypeDto dto, Guid userId)
    {
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.DefaultSuspensionDays = dto.DefaultSuspensionDays;
        entity.DefaultFineAmount = dto.DefaultFineAmount;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDisciplinaryActionTypeSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDisciplinaryActionType> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINARY ACTION  (case header)
    // ========================================================================

    #region StaffDisciplinaryAction

    public static StaffDisciplinaryActionDto ToDto(this StaffDisciplinaryAction entity)
    {
        return new StaffDisciplinaryActionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CaseNumber = entity.CaseNumber,
            Status = entity.Status,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            EmployeeDepartment = entity.Employee?.Department?.Name,
            StaffOffenseId = entity.StaffOffenseId,
            OffenseName = entity.StaffOffense?.OffenseName ?? string.Empty,
            OffenseCode = entity.StaffOffense?.OffenseCode,
            Severity = entity.Severity,
            IncidentDate = entity.IncidentDate,
            IncidentDescription = entity.IncidentDescription,
            ReportedById = entity.ReportedById,
            ReportedByName = entity.ReportedBy?.FullName ?? string.Empty,
            ReportedDate = entity.ReportedDate,
            ReportedToId = entity.ReportedToId,
            ReportedToName = entity.ReportedTo?.FullName,
            RequiresInvestigation = entity.RequiresInvestigation,
            HearingRequired = entity.HearingRequired,
            ActionTypeId = entity.ActionTypeId,
            ActionTypeName = entity.ActionType?.Name,
            ActionDetails = entity.ActionDetails,
            DecisionDate = entity.DecisionDate,
            DecisionById = entity.DecisionById,
            DecisionByName = entity.DecisionBy?.FullName,
            DecisionRationale = entity.DecisionRationale,
            ClosedDate = entity.ClosedDate,
            ClosureNotes = entity.ClosureNotes,
            ClosedById = entity.ClosedById,
            ClosedByName = entity.ClosedBy?.FullName,
            HasWarning = entity.Warning != null,
            HasSuspension = entity.Suspension != null,
            HasFine = entity.Fine != null,
            HasTermination = entity.Termination != null,
            HasDemotion = false, // resolved externally via StaffDemotion.DisciplinaryActionId
            Investigation = entity.Investigation?.ToDto(),
            Hearing = entity.Hearing?.ToDto(),
            Warning = entity.Warning?.ToDto(),
            Suspension = entity.Suspension?.ToDto(),
            Fine = entity.Fine?.ToDto(),
            Termination = entity.Termination?.ToDto(),
            Separation = entity.Separation?.ToDto(),
            Appeal = entity.Appeal?.ToDto(),
            CorrectiveAction = entity.CorrectiveAction?.ToDto(),
            ActionSteps = entity.ActionSteps.Select(s => s.ToDto()).ToList(),
            Witnesses = entity.Witnesses.Select(w => w.ToSummaryDto()).ToList(),
            Documents = entity.Documents.Select(d => d.ToSummaryDto()).ToList(),
            Notes = entity.Notes.Select(n => n.ToSummaryDto()).ToList(),
            Notifications = entity.Notifications.Select(n => n.ToSummaryDto()).ToList(),
            LegalReviews = entity.LegalReviews.Select(l => l.ToSummaryDto()).ToList(),
        };
    }

    public static StaffDisciplinaryActionSummaryDto ToSummaryDto(this StaffDisciplinaryAction entity)
    {
        return new StaffDisciplinaryActionSummaryDto
        {
            Id = entity.Id,
            CaseNumber = entity.CaseNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            OffenseName = entity.StaffOffense?.OffenseName ?? string.Empty,
            Severity = entity.Severity,
            Status = entity.Status,
            IncidentDate = entity.IncidentDate,
            ReportedDate = entity.ReportedDate,
            HasWarning = entity.Warning != null,
            HasSuspension = entity.Suspension != null,
            HasFine = entity.Fine != null,
            HasTermination = entity.Termination != null,
            AppealFiled = entity.Appeal != null,
            ClosedDate = entity.ClosedDate,
        };
    }

    public static StaffDisciplinaryAction ToEntity(this CreateStaffDisciplinaryActionDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplinaryAction
        {
            TenantId = tenantId,
            CaseNumber = dto.CaseNumber,
            Status = DisciplinaryStatus.Draft,
            EmployeeId = dto.EmployeeId,
            StaffOffenseId = dto.StaffOffenseId,
            Severity = dto.Severity,
            IncidentDate = dto.IncidentDate,
            IncidentDescription = dto.IncidentDescription,
            ReportedById = dto.ReportedById,
            ReportedDate = dto.ReportedDate,
            ReportedToId = dto.ReportedToId,
            RequiresInvestigation = dto.RequiresInvestigation,
            HearingRequired = dto.HearingRequired,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplinaryAction entity, UpdateStaffDisciplinaryActionDto dto, Guid userId)
    {
        entity.StaffOffenseId = dto.StaffOffenseId;
        entity.Severity = dto.Severity;
        entity.IncidentDate = dto.IncidentDate;
        entity.IncidentDescription = dto.IncidentDescription;
        entity.ReportedById = dto.ReportedById;
        entity.ReportedDate = dto.ReportedDate;
        entity.ReportedToId = dto.ReportedToId;
        entity.RequiresInvestigation = dto.RequiresInvestigation;
        entity.HearingRequired = dto.HearingRequired;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDisciplinaryActionSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDisciplinaryAction> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE INVESTIGATION
    // ========================================================================

    #region StaffDisciplineInvestigation

    public static StaffDisciplineInvestigationDto ToDto(this StaffDisciplineInvestigation entity)
    {
        return new StaffDisciplineInvestigationDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            InvestigatorId = entity.InvestigatorId,
            InvestigatorName = entity.Investigator?.FullName,
            InvestigationStartDate = entity.InvestigationStartDate,
            InvestigationEndDate = entity.InvestigationEndDate,
            InvestigationFindings = entity.InvestigationFindings,
            EvidenceCollected = entity.EvidenceCollected,
        };
    }

    public static StaffDisciplineInvestigation ToEntity(this OpenInvestigationDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineInvestigation
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            InvestigatorId = dto.InvestigatorId,
            InvestigationStartDate = dto.InvestigationStartDate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineInvestigation entity, UpdateInvestigationDto dto, Guid userId)
    {
        entity.InvestigatorId = dto.InvestigatorId;
        entity.InvestigationStartDate = dto.InvestigationStartDate;
        entity.InvestigationEndDate = dto.InvestigationEndDate;
        entity.InvestigationFindings = dto.InvestigationFindings;
        entity.EvidenceCollected = dto.EvidenceCollected;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE HEARING
    // ========================================================================

    #region StaffDisciplineHearing

    public static StaffDisciplineHearingDto ToDto(this StaffDisciplineHearing entity)
    {
        return new StaffDisciplineHearingDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.DisciplinaryAction?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.DisciplinaryAction?.Employee?.FullName ?? string.Empty,
            HearingDate = entity.HearingDate,
            HearingVenue = entity.HearingVenue,
            HearingNotes = entity.HearingNotes,
            HearingOfficerId = entity.HearingOfficerId,
            HearingOfficerName = entity.HearingOfficer?.FullName,
            EmployeeAttendedHearing = entity.EmployeeAttendedHearing,
            EmployeeStatement = entity.EmployeeStatement,
            EmployeeHadRepresentation = entity.EmployeeHadRepresentation,
            RepresentativeType = entity.RepresentativeType,
            RepresentativeEmployeeId = entity.RepresentativeEmployeeId,
            RepresentativeEmployeeName = entity.RepresentativeEmployee?.FullName,
            RepresentativeName = entity.RepresentativeName,
            RepresentativePosition = entity.RepresentativePosition,
            RepresentativeContactInfo = entity.RepresentativeContactInfo,
        };
    }

    public static StaffDisciplineHearing ToEntity(this ScheduleHearingDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineHearing
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            HearingDate = dto.HearingDate,
            HearingVenue = dto.HearingVenue,
            HearingOfficerId = dto.HearingOfficerId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineHearing entity, RecordHearingOutcomeDto dto, Guid userId)
    {
        entity.EmployeeAttendedHearing = dto.EmployeeAttendedHearing;
        entity.EmployeeStatement = dto.EmployeeStatement;
        entity.EmployeeHadRepresentation = dto.EmployeeHadRepresentation;
        entity.RepresentativeType = dto.RepresentativeType;
        entity.RepresentativeEmployeeId = dto.RepresentativeEmployeeId;
        entity.RepresentativeName = dto.RepresentativeName;
        entity.RepresentativePosition = dto.RepresentativePosition;
        entity.RepresentativeContactInfo = dto.RepresentativeContactInfo;
        entity.HearingNotes = dto.HearingNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE WARNING
    // ========================================================================

    #region StaffDisciplineWarning

    public static StaffDisciplineWarningDto ToDto(this StaffDisciplineWarning entity)
    {
        return new StaffDisciplineWarningDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.DisciplinaryAction?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.DisciplinaryAction?.Employee?.FullName ?? string.Empty,
            WarningType = entity.WarningType,
            WarningExpiryDate = entity.WarningExpiryDate,
            WarningLetterReference = entity.WarningLetterReference,
        };
    }

    public static StaffDisciplineWarning ToEntity(this RecordWarningPenaltyDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineWarning
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            WarningType = dto.WarningType,
            WarningExpiryDate = dto.WarningExpiryDate,
            WarningLetterReference = dto.WarningLetterReference,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineWarning entity, UpdateWarningPenaltyDto dto, Guid userId)
    {
        entity.WarningType = dto.WarningType;
        entity.WarningExpiryDate = dto.WarningExpiryDate;
        entity.WarningLetterReference = dto.WarningLetterReference;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE SUSPENSION
    // ========================================================================

    #region StaffDisciplineSuspension

    public static StaffDisciplineSuspensionDto ToDto(this StaffDisciplineSuspension entity)
    {
        return new StaffDisciplineSuspensionDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.DisciplinaryAction?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.DisciplinaryAction?.Employee?.FullName ?? string.Empty,
            SuspensionStartDate = entity.SuspensionStartDate,
            SuspensionEndDate = entity.SuspensionEndDate,
            SuspensionWithPay = entity.SuspensionWithPay,
        };
    }

    public static StaffDisciplineSuspension ToEntity(this RecordSuspensionPenaltyDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineSuspension
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            SuspensionStartDate = dto.SuspensionStartDate,
            SuspensionEndDate = dto.SuspensionEndDate,
            SuspensionWithPay = dto.SuspensionWithPay,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineSuspension entity, UpdateSuspensionPenaltyDto dto, Guid userId)
    {
        entity.SuspensionStartDate = dto.SuspensionStartDate;
        entity.SuspensionEndDate = dto.SuspensionEndDate;
        entity.SuspensionWithPay = dto.SuspensionWithPay;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE FINE
    // ========================================================================

    #region StaffDisciplineFine

    public static StaffDisciplineFineDto ToDto(this StaffDisciplineFine entity)
    {
        return new StaffDisciplineFineDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.DisciplinaryAction?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.DisciplinaryAction?.Employee?.FullName ?? string.Empty,
            FineAmount = entity.FineAmount,
            FinePaymentStatus = entity.FinePaymentStatus,
            FineDueDate = entity.FineDueDate,
            FinePaidAmount = entity.FinePaidAmount,
            FinePaymentDate = entity.FinePaymentDate,
        };
    }

    public static StaffDisciplineFine ToEntity(this RecordFinePenaltyDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineFine
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            FineAmount = dto.FineAmount,
            FineDueDate = dto.FineDueDate,
            FinePaymentStatus = DisciplinaryFinePaymentStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineFine entity, RecordFinePaymentDto dto, Guid userId)
    {
        entity.FinePaidAmount = (entity.FinePaidAmount ?? 0m) + dto.AmountPaid;
        entity.FinePaymentDate = dto.PaymentDate;
        entity.FinePaymentStatus = dto.PaymentStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE ACTION STEP
    // ========================================================================

    #region StaffDisciplineActionStep

    public static StaffDisciplineActionStepDto ToDto(this StaffDisciplineActionStep entity)
    {
        return new StaffDisciplineActionStepDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            OffenseProcedureId = entity.OffenseProcedureId,
            StepName = entity.OffenseProcedure?.StepName ?? string.Empty,
            StepDescription = entity.OffenseProcedure?.StepDescription,
            Sequence = entity.OffenseProcedure?.Sequence ?? 0,
            ExpectedCompletionDays = entity.OffenseProcedure?.ExpectedCompletionDays,
            DueDate = entity.DueDate,
            StartedDate = entity.StartedDate,
            CompletedDate = entity.CompletedDate,
            Status = entity.Status,
            ActionedById = entity.ActionedById,
            ActionedByName = entity.ActionedBy?.FullName,
            Notes = entity.Notes,
            Documents = entity.Documents.Select(d => d.ToSummaryDto()).ToList(),
        };
    }

    public static StaffDisciplineActionStep ToEntity(Guid disciplinaryActionId, Guid offenseProcedureId, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineActionStep
        {
            TenantId = tenantId,
            DisciplinaryActionId = disciplinaryActionId,
            OffenseProcedureId = offenseProcedureId,
            Status = DisciplinaryActionStepStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineActionStep entity, UpdateActionStepDto dto, Guid userId)
    {
        entity.DueDate = dto.DueDate;
        entity.StartedDate = dto.StartedDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.Status = dto.Status;
        entity.ActionedById = dto.ActionedById;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDisciplineActionStepDto> ToDtoList(this IEnumerable<StaffDisciplineActionStep> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE WITNESS
    // ========================================================================

    #region StaffDisciplineWitness

    public static StaffDisciplineWitnessDto ToDto(this StaffDisciplineWitness entity)
    {
        return new StaffDisciplineWitnessDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            Name = entity.Name,
            IsEmployee = entity.IsEmployee,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            ContactInfo = entity.ContactInfo,
            Statement = entity.Statement,
            StatementDate = entity.StatementDate,
        };
    }

    public static StaffDisciplineWitnessSummaryDto ToSummaryDto(this StaffDisciplineWitness entity)
    {
        return new StaffDisciplineWitnessSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            IsEmployee = entity.IsEmployee,
            EmployeeId = entity.EmployeeId,
            Statement = entity.Statement,
            StatementDate = entity.StatementDate,
        };
    }

    public static StaffDisciplineWitness ToEntity(this CreateStaffDisciplineWitnessDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineWitness
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.DisciplinaryActionId,
            Name = dto.Name,
            IsEmployee = dto.IsEmployee,
            EmployeeId = dto.EmployeeId,
            ContactInfo = dto.ContactInfo,
            Statement = dto.Statement,
            StatementDate = dto.StatementDate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineWitness entity, UpdateStaffDisciplineWitnessDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.IsEmployee = dto.IsEmployee;
        entity.EmployeeId = dto.EmployeeId;
        entity.ContactInfo = dto.ContactInfo;
        entity.Statement = dto.Statement;
        entity.StatementDate = dto.StatementDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDisciplineWitnessSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDisciplineWitness> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE DOCUMENT
    // ========================================================================

    #region StaffDisciplineDocument

    public static StaffDisciplineDocumentDto ToDto(this StaffDisciplineDocument entity)
    {
        return new StaffDisciplineDocumentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            Scope = entity.Scope,
            ActionStepId = entity.ActionStepId,
            ActionStepName = entity.ActionStep?.OffenseProcedure?.StepName,
            AppealId = entity.AppealId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Category = entity.Category,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
        };
    }

    public static StaffDisciplineDocumentSummaryDto ToSummaryDto(this StaffDisciplineDocument entity)
    {
        return new StaffDisciplineDocumentSummaryDto
        {
            Id = entity.Id,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Category = entity.Category,
            Scope = entity.Scope,
            ActionStepId = entity.ActionStepId,
            AppealId = entity.AppealId,
            UploadDate = entity.UploadDate,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
        };
    }

    public static StaffDisciplineDocument ToEntity(this CreateStaffDisciplineDocumentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineDocument
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.DisciplinaryActionId,
            Scope = dto.Scope,
            ActionStepId = dto.ActionStepId,
            AppealId = dto.AppealId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            Category = dto.Category,
            Description = dto.Description,
            UploadDate = dto.UploadDate,
            UploadedById = dto.UploadedById,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffDisciplineDocumentSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDisciplineDocument> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE NOTE
    // ========================================================================

    #region StaffDisciplineNote

    public static StaffDisciplineNoteDto ToDto(this StaffDisciplineNote entity)
    {
        return new StaffDisciplineNoteDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            CreatedByEmployeeId = entity.CreatedByEmployeeId,
            CreatedByEmployeeName = entity.CreatedByEmployee?.FullName ?? string.Empty,
            Note = entity.Note,
            IsConfidential = entity.IsConfidential,
            NoteDate = entity.NoteDate,
        };
    }

    public static StaffDisciplineNoteSummaryDto ToSummaryDto(this StaffDisciplineNote entity)
    {
        return new StaffDisciplineNoteSummaryDto
        {
            Id = entity.Id,
            CreatedByEmployeeName = entity.CreatedByEmployee?.FullName ?? string.Empty,
            NoteExcerpt = entity.Note.Length > 100 ? entity.Note[..100] + "…" : entity.Note,
            IsConfidential = entity.IsConfidential,
            NoteDate = entity.NoteDate,
        };
    }

    public static StaffDisciplineNote ToEntity(this CreateStaffDisciplineNoteDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineNote
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.DisciplinaryActionId,
            CreatedByEmployeeId = dto.CreatedByEmployeeId,
            Note = dto.Note,
            IsConfidential = dto.IsConfidential,
            NoteDate = dto.NoteDate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineNote entity, UpdateStaffDisciplineNoteDto dto, Guid userId)
    {
        entity.Note = dto.Note;
        entity.IsConfidential = dto.IsConfidential;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDisciplineNoteSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDisciplineNote> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE NOTIFICATION
    // ========================================================================

    #region StaffDisciplineNotification

    public static StaffDisciplineNotificationDto ToDto(this StaffDisciplineNotification entity)
    {
        return new StaffDisciplineNotificationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.DisciplinaryAction?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.DisciplinaryAction?.Employee?.FullName ?? string.Empty,
            NotificationType = entity.NotificationType,
            SentDate = entity.SentDate,
            Content = entity.Content,
            SentById = entity.SentById,
            SentByName = entity.SentBy?.FullName ?? string.Empty,
            AcknowledgedDate = entity.AcknowledgedDate,
            IsFollowupSent = entity.IsFollowupSent,
            FollowupDate = entity.FollowupDate,
        };
    }

    public static StaffDisciplineNotificationSummaryDto ToSummaryDto(this StaffDisciplineNotification entity)
    {
        return new StaffDisciplineNotificationSummaryDto
        {
            Id = entity.Id,
            NotificationType = entity.NotificationType,
            SentDate = entity.SentDate,
            IsAcknowledged = entity.AcknowledgedDate.HasValue,
            IsFollowupSent = entity.IsFollowupSent,
        };
    }

    public static StaffDisciplineNotification ToEntity(this CreateStaffDisciplineNotificationDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineNotification
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.DisciplinaryActionId,
            NotificationType = dto.NotificationType,
            SentDate = dto.SentDate,
            Content = dto.Content,
            SentById = dto.SentById,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffDisciplineNotificationSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDisciplineNotification> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE APPEAL
    // ========================================================================

    #region StaffDisciplineAppeal

    public static StaffDisciplineAppealDto ToDto(this StaffDisciplineAppeal entity)
    {
        return new StaffDisciplineAppealDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            FiledDate = entity.FiledDate,
            Reason = entity.Reason,
            AppealStatus = entity.AppealStatus,
            AppealOfficerId = entity.AppealOfficerId,
            AppealOfficerName = entity.AppealOfficer?.FullName,
            HearingDate = entity.HearingDate,
            HearingVenue = entity.HearingVenue,
            HearingNotes = entity.HearingNotes,
            AppealOutcome = entity.AppealOutcome,
            AppealOutcomeNotes = entity.AppealOutcomeNotes,
            AppealOutcomeDate = entity.AppealOutcomeDate,
            AppealOutcomeById = entity.AppealOutcomeById,
            AppealOutcomeByName = entity.AppealOutcomeBy?.FullName,
            Documents = entity.Documents.Select(d => d.ToSummaryDto()).ToList(),
        };
    }

    public static StaffDisciplineAppeal ToEntity(this FileAppealDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineAppeal
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            EmployeeId = dto.EmployeeId,
            FiledDate = dto.FiledDate,
            Reason = dto.Reason,
            AppealStatus = DisciplineAppealStatus.Filed,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineAppeal entity, RecordAppealOutcomeDto dto, Guid userId)
    {
        entity.AppealOutcome = dto.AppealOutcome;
        entity.AppealOutcomeNotes = dto.AppealOutcomeNotes;
        entity.AppealOutcomeDate = dto.AppealOutcomeDate;
        entity.AppealOutcomeById = dto.AppealOutcomeById;
        entity.HearingNotes = dto.HearingNotes;
        entity.AppealStatus = DisciplineAppealStatus.AwaitingDecision;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE CORRECTIVE ACTION
    // ========================================================================

    #region StaffDisciplineCorrectiveAction

    public static StaffDisciplineCorrectiveActionDto ToDto(this StaffDisciplineCorrectiveAction entity)
    {
        return new StaffDisciplineCorrectiveActionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            SupervisorId = entity.SupervisorId,
            SupervisorName = entity.Supervisor?.FullName ?? string.Empty,
            Objective = entity.Objective,
            StartDate = entity.StartDate,
            ReviewDate = entity.ReviewDate,
            CompletedDate = entity.CompletedDate,
            Status = entity.Status,
            Notes = entity.Notes,
            ItemCount = entity.Items.Count,
            CompletedItemCount = entity.Items.Count(i => i.Status == DisciplineCorrectiveActionStatus.Completed),
            Items = entity.Items.Select(i => i.ToDto()).ToList(),
        };
    }

    public static StaffDisciplineCorrectiveAction ToEntity(this CreateStaffDisciplineCorrectiveActionDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineCorrectiveAction
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.DisciplinaryActionId,
            EmployeeId = dto.EmployeeId,
            SupervisorId = dto.SupervisorId,
            Objective = dto.Objective,
            StartDate = dto.StartDate,
            ReviewDate = dto.ReviewDate,
            Status = DisciplineCorrectiveActionStatus.Pending,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineCorrectiveAction entity, UpdateStaffDisciplineCorrectiveActionDto dto, Guid userId)
    {
        entity.SupervisorId = dto.SupervisorId;
        entity.Objective = dto.Objective;
        entity.StartDate = dto.StartDate;
        entity.ReviewDate = dto.ReviewDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE CORRECTIVE ACTION ITEM
    // ========================================================================

    #region StaffDisciplineCorrectiveActionItem

    public static StaffDisciplineCorrectiveActionItemDto ToDto(this StaffDisciplineCorrectiveActionItem entity)
    {
        return new StaffDisciplineCorrectiveActionItemDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CorrectiveActionId = entity.CorrectiveActionId,
            Description = entity.Description,
            TargetDate = entity.TargetDate,
            CompletedDate = entity.CompletedDate,
            Status = entity.Status,
            CompletionNotes = entity.CompletionNotes,
        };
    }

    public static StaffDisciplineCorrectiveActionItem ToEntity(this CreateStaffDisciplineCorrectiveActionItemDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineCorrectiveActionItem
        {
            TenantId = tenantId,
            CorrectiveActionId = dto.CorrectiveActionId,
            Description = dto.Description,
            TargetDate = dto.TargetDate,
            Status = DisciplineCorrectiveActionStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineCorrectiveActionItem entity, UpdateStaffDisciplineCorrectiveActionItemDto dto, Guid userId)
    {
        entity.Description = dto.Description;
        entity.TargetDate = dto.TargetDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.Status = dto.Status;
        entity.CompletionNotes = dto.CompletionNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDisciplineCorrectiveActionItemDto> ToDtoList(this IEnumerable<StaffDisciplineCorrectiveActionItem> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE LEGAL REVIEW
    // ========================================================================

    #region StaffDisciplineLegalReview

    public static StaffDisciplineLegalReviewDto ToDto(this StaffDisciplineLegalReview entity)
    {
        return new StaffDisciplineLegalReviewDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            ReferredToLegalDate = entity.ReferredToLegalDate,
            ReferredById = entity.ReferredById,
            ReferredByName = entity.ReferredBy?.FullName,
            LegalReviewCompleteDate = entity.LegalReviewCompleteDate,
            LegalRiskLevel = entity.LegalRiskLevel,
            LegalAdvice = entity.LegalAdvice,
            RequiresExternalCounsel = entity.RequiresExternalCounsel,
            ExternalCounselId = entity.ExternalCounselId,
            ExternalCounselName = entity.ExternalCounselName,
            ExternalCounselFirm = entity.ExternalCounselFirm,
            ExternalCounselReviewDate = entity.ExternalCounselReviewDate,
            ExternalCounselOpinion = entity.ExternalCounselOpinion,
            LegalCostsIncurred = entity.LegalCostsIncurred,
            IsConfidential = entity.IsConfidential,
        };
    }

    public static StaffDisciplineLegalReviewSummaryDto ToSummaryDto(this StaffDisciplineLegalReview entity)
    {
        return new StaffDisciplineLegalReviewSummaryDto
        {
            Id = entity.Id,
            ReferredToLegalDate = entity.ReferredToLegalDate,
            LegalRiskLevel = entity.LegalRiskLevel,
            LegalReviewCompleteDate = entity.LegalReviewCompleteDate,
            RequiresExternalCounsel = entity.RequiresExternalCounsel,
            IsConfidential = entity.IsConfidential,
        };
    }

    public static StaffDisciplineLegalReview ToEntity(this CreateStaffDisciplineLegalReviewDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineLegalReview
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.DisciplinaryActionId,
            ReferredToLegalDate = dto.ReferredToLegalDate,
            ReferredById = dto.ReferredById,
            LegalRiskLevel = dto.LegalRiskLevel,
            RequiresExternalCounsel = dto.RequiresExternalCounsel,
            ExternalCounselId = dto.ExternalCounselId,
            ExternalCounselName = dto.ExternalCounselName,
            ExternalCounselFirm = dto.ExternalCounselFirm,
            IsConfidential = dto.IsConfidential,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineLegalReview entity, UpdateStaffDisciplineLegalReviewDto dto, Guid userId)
    {
        entity.LegalRiskLevel = dto.LegalRiskLevel;
        entity.LegalReviewCompleteDate = dto.LegalReviewCompleteDate;
        entity.LegalAdvice = dto.LegalAdvice;
        entity.RequiresExternalCounsel = dto.RequiresExternalCounsel;
        entity.ExternalCounselId = dto.ExternalCounselId;
        entity.ExternalCounselName = dto.ExternalCounselName;
        entity.ExternalCounselFirm = dto.ExternalCounselFirm;
        entity.ExternalCounselReviewDate = dto.ExternalCounselReviewDate;
        entity.ExternalCounselOpinion = dto.ExternalCounselOpinion;
        entity.LegalCostsIncurred = dto.LegalCostsIncurred;
        entity.IsConfidential = dto.IsConfidential;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffDisciplineLegalReviewSummaryDto> ToSummaryDtoList(this IEnumerable<StaffDisciplineLegalReview> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE TERMINATION
    // ========================================================================

    #region StaffDisciplineTermination

    public static StaffDisciplineTerminationDto ToDto(this StaffDisciplineTermination entity)
    {
        return new StaffDisciplineTerminationDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.DisciplinaryAction?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.DisciplinaryAction?.Employee?.FullName ?? string.Empty,
            Type = entity.Type,
            IsEligibleForRehire = entity.IsEligibleForRehire,
            EligibleForRehireDate = entity.EligibleForRehireDate,
            RehireRestrictions = entity.RehireRestrictions,
            FinalPaycheckProcessed = entity.FinalPaycheckProcessed,
            FinalPaycheckDate = entity.FinalPaycheckDate,
            FinalPaycheckAmount = entity.FinalPaycheckAmount,
            SeparationNotes = entity.SeparationNotes,
        };
    }

    public static StaffDisciplineTermination ToEntity(this RecordTerminationDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineTermination
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            Type = dto.Type,
            IsEligibleForRehire = dto.IsEligibleForRehire,
            EligibleForRehireDate = dto.EligibleForRehireDate,
            RehireRestrictions = dto.RehireRestrictions,
            SeparationNotes = dto.SeparationNotes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineTermination entity, UpdateTerminationDto dto, Guid userId)
    {
        entity.Type = dto.Type;
        entity.IsEligibleForRehire = dto.IsEligibleForRehire;
        entity.EligibleForRehireDate = dto.EligibleForRehireDate;
        entity.RehireRestrictions = dto.RehireRestrictions;
        entity.FinalPaycheckProcessed = dto.FinalPaycheckProcessed;
        entity.FinalPaycheckDate = dto.FinalPaycheckDate;
        entity.FinalPaycheckAmount = dto.FinalPaycheckAmount;
        entity.SeparationNotes = dto.SeparationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DISCIPLINE SEPARATION
    // ========================================================================

    #region StaffDisciplineSeparation

    public static StaffDisciplineSeparationDto ToDto(this StaffDisciplineSeparation entity)
    {
        var checklistItems = new[]
        {
            entity.ExitInterviewCompleted,
            entity.EquipmentReturned,
            entity.AccessRevoked,
            entity.FinalPayrollProcessed,
            entity.BenefitsTerminated,
            entity.ExitChecklistCompleted,
        };
        var completedCount = checklistItems.Count(x => x);

        return new StaffDisciplineSeparationDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            CaseNumber = entity.DisciplinaryAction?.CaseNumber ?? string.Empty,
            EmployeeId = entity.DisciplinaryAction?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.DisciplinaryAction?.Employee?.FullName ?? string.Empty,
            ExitInterviewCompleted = entity.ExitInterviewCompleted,
            ExitInterviewDate = entity.ExitInterviewDate,
            ExitInterviewNotes = entity.ExitInterviewNotes,
            ExitInterviewerId = entity.ExitInterviewerId,
            ExitInterviewerName = entity.ExitInterviewer?.FullName,
            EquipmentReturned = entity.EquipmentReturned,
            EquipmentReturnedDate = entity.EquipmentReturnedDate,
            MissingEquipment = entity.MissingEquipment,
            AccessRevoked = entity.AccessRevoked,
            AccessRevokedDate = entity.AccessRevokedDate,
            AccessRevokedById = entity.AccessRevokedById,
            AccessRevokedByName = entity.AccessRevokedBy?.FullName,
            FinalPayrollProcessed = entity.FinalPayrollProcessed,
            FinalPayrollDate = entity.FinalPayrollDate,
            BenefitsTerminated = entity.BenefitsTerminated,
            BenefitsTerminationDate = entity.BenefitsTerminationDate,
            ExitChecklistCompleted = entity.ExitChecklistCompleted,
            ExitChecklistCompletedDate = entity.ExitChecklistCompletedDate,
            AdditionalNotes = entity.AdditionalNotes,
            ChecklistCompletionPercent = (int)Math.Round(completedCount / (double)checklistItems.Length * 100),
        };
    }

    public static StaffDisciplineSeparation ToEntity(this InitiateSeparationDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDisciplineSeparation
        {
            TenantId = tenantId,
            DisciplinaryActionId = dto.CaseId,
            ExitInterviewerId = dto.ExitInterviewerId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDisciplineSeparation entity, UpdateSeparationDto dto, Guid userId)
    {
        entity.ExitInterviewCompleted = dto.ExitInterviewCompleted;
        entity.ExitInterviewDate = dto.ExitInterviewDate;
        entity.ExitInterviewNotes = dto.ExitInterviewNotes;
        entity.ExitInterviewerId = dto.ExitInterviewerId;
        entity.EquipmentReturned = dto.EquipmentReturned;
        entity.EquipmentReturnedDate = dto.EquipmentReturnedDate;
        entity.MissingEquipment = dto.MissingEquipment;
        entity.AccessRevoked = dto.AccessRevoked;
        entity.AccessRevokedDate = dto.AccessRevokedDate;
        entity.AccessRevokedById = dto.AccessRevokedById;
        entity.FinalPayrollProcessed = dto.FinalPayrollProcessed;
        entity.FinalPayrollDate = dto.FinalPayrollDate;
        entity.BenefitsTerminated = dto.BenefitsTerminated;
        entity.BenefitsTerminationDate = dto.BenefitsTerminationDate;
        entity.ExitChecklistCompleted = dto.ExitChecklistCompleted;
        entity.ExitChecklistCompletedDate = dto.ExitChecklistCompletedDate;
        entity.AdditionalNotes = dto.AdditionalNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion
}
