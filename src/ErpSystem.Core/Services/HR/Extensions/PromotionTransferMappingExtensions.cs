using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class PromotionTransferMappingExtensions
{
    // ========================================================================
    // STAFF MOVEMENT
    // ========================================================================

    #region StaffMovement

    public static StaffMovementDto ToDto(this StaffMovement entity)
    {
        return new StaffMovementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,

            MovementNumber = entity.MovementNumber,

            // Employee
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,

            // Classification
            MovementType = entity.MovementType,
            Category = entity.Category,

            // Current Position
            CurrentPositionId = entity.CurrentPositionId,
            CurrentPositionTitle = entity.CurrentPosition?.Title ?? string.Empty,
            CurrentOrganizationUnitId = entity.CurrentOrganizationUnitId,
            CurrentOrganizationUnitName = entity.CurrentOrganizationUnit?.Name ?? string.Empty,
            CurrentOrganizationLevelId = entity.CurrentOrganizationLevelId,
            CurrentOrganizationLevelName = entity.CurrentOrganizationLevel?.Name,
            CurrentLocationId = entity.CurrentLocationId,
            CurrentLocationName = entity.CurrentLocation?.Name,
            CurrentLocationLevelId = entity.CurrentLocationLevelId,
            CurrentLocationLevelName = entity.CurrentLocationLevel?.Name,
            CurrentSupervisorId = entity.CurrentSupervisorId,
            CurrentSupervisorName = entity.CurrentSupervisor?.FullName,

            // Current Salary & Grade
            CurrentSalary = entity.CurrentSalary,
            CurrentSalaryGradeId = entity.CurrentSalaryGradeId,
            CurrentSalaryGradeCode = entity.CurrentSalaryGrade?.Code,
            CurrentSalaryGradeName = entity.CurrentSalaryGrade?.Name,
            CurrentSalaryLevelId = entity.CurrentSalaryLevelId,
            CurrentSalaryLevelName = entity.CurrentSalaryLevel?.Name,
            CurrentSalaryNotchId = entity.CurrentSalaryNotchId,
            CurrentSalaryNotchNumber = entity.CurrentSalaryNotch?.NotchNumber,

            // New Position
            NewPositionId = entity.NewPositionId,
            NewPositionTitle = entity.NewPosition?.Title ?? string.Empty,
            NewOrganizationUnitId = entity.NewOrganizationUnitId,
            NewOrganizationUnitName = entity.NewOrganizationUnit?.Name ?? string.Empty,
            NewOrganizationLevelId = entity.NewOrganizationLevelId,
            NewOrganizationLevelName = entity.NewOrganizationLevel?.Name,
            NewLocationId = entity.NewLocationId,
            NewLocationName = entity.NewLocation?.Name,
            NewLocationLevelId = entity.NewLocationLevelId,
            NewLocationLevelName = entity.NewLocationLevel?.Name,
            NewSupervisorId = entity.NewSupervisorId,
            NewSupervisorName = entity.NewSupervisor?.FullName,

            // New Salary & Grade
            NewSalary = entity.NewSalary,
            NewSalaryGradeId = entity.NewSalaryGradeId,
            NewSalaryGradeCode = entity.NewSalaryGrade?.Code,
            NewSalaryGradeName = entity.NewSalaryGrade?.Name,
            NewSalaryLevelId = entity.NewSalaryLevelId,
            NewSalaryLevelName = entity.NewSalaryLevel?.Name,
            NewSalaryNotchId = entity.NewSalaryNotchId,
            NewSalaryNotchNumber = entity.NewSalaryNotch?.NotchNumber,
            SalaryIncreaseAmount = entity.SalaryIncreaseAmount,
            SalaryIncreasePercentage = entity.SalaryIncreasePercentage,

            // Reason
            Reason = entity.Reason,
            Justification = entity.Justification,
            IsReorganization = entity.IsReorganization,
            IsSuccessionPlan = entity.IsSuccessionPlan,
            SuccessionPlanId = entity.SuccessionPlanId,
            SuccessionPlanNumber = entity.SuccessionPlan?.PlanNumber,

            // Dates
            RequestDate = entity.RequestDate,
            EffectiveDate = entity.EffectiveDate,

            // Temporary
            IsTemporary = entity.IsTemporary,
            TemporaryEndDate = entity.TemporaryEndDate,
            TemporaryArrangementDetails = entity.TemporaryArrangementDetails,
            ReturnProcessed = entity.ReturnProcessed,
            ActualReturnDate = entity.ActualReturnDate,
            ReturnMovementId = entity.ReturnMovementId,
            ReturnMovementNumber = entity.ReturnMovement?.MovementNumber,

            // Requester
            RequestedById = entity.RequestedById,
            RequestedByName = entity.RequestedBy?.FullName ?? string.Empty,
            RequestSubmissionDate = entity.RequestSubmissionDate,

            // Authorization
            AuthorizedById = entity.AuthorizedById,
            AuthorizedByName = entity.AuthorizedBy?.FullName,
            AuthorizationDate = entity.AuthorizationDate,

            // Status
            Status = entity.Status,

            // Rejection
            RejectionReason = entity.RejectionReason,
            RejectedById = entity.RejectedById,
            RejectedByName = entity.RejectedBy?.FullName,
            RejectionDate = entity.RejectionDate,

            // Cancellation
            CancellationReason = entity.CancellationReason,
            CancelledById = entity.CancelledById,
            CancelledByName = entity.CancelledBy?.FullName,
            CancellationDate = entity.CancellationDate,

            // Employee Acceptance
            RequiresEmployeeAcceptance = entity.RequiresEmployeeAcceptance,
            EmployeeAccepted = entity.EmployeeAccepted,
            EmployeeResponseDate = entity.EmployeeResponseDate,
            EmployeeComments = entity.EmployeeComments,

            // Handover
            RequiresHandover = entity.RequiresHandover,
            HandoverCompletionDate = entity.HandoverCompletionDate,
            HandoverNotes = entity.HandoverNotes,

            // Performance Basis
            BasedOnAppraisalId = entity.BasedOnAppraisalId,
            BasedOnAppraisalNumber = entity.BasedOnAppraisal?.AppraisalNumber,

            AdditionalNotes = entity.AdditionalNotes,
        };
    }

    public static StaffMovementSummaryDto ToSummaryDto(this StaffMovement entity)
    {
        return new StaffMovementSummaryDto
        {
            Id = entity.Id,
            MovementNumber = entity.MovementNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            MovementType = entity.MovementType,
            Category = entity.Category,
            CurrentPositionTitle = entity.CurrentPosition?.Title ?? string.Empty,
            CurrentOrganizationUnitName = entity.CurrentOrganizationUnit?.Name ?? string.Empty,
            NewPositionTitle = entity.NewPosition?.Title ?? string.Empty,
            NewOrganizationUnitName = entity.NewOrganizationUnit?.Name ?? string.Empty,
            RequestDate = entity.RequestDate,
            EffectiveDate = entity.EffectiveDate,
            Status = entity.Status,
            IsTemporary = entity.IsTemporary,
            TemporaryEndDate = entity.TemporaryEndDate,
            ReturnProcessed = entity.ReturnProcessed,
            PromotionType = entity.Promotion?.Type,
            GradeLevelIncrease = entity.Promotion?.GradeLevelIncrease,
            IsActingPromotion = entity.Promotion?.IsActingPromotion ?? false,
            TransferType = entity.Transfer?.Type,
            RequiresRelocation = entity.Transfer?.RequiresRelocation ?? false,
            IsInterCompany = entity.Transfer?.IsInterCompany ?? false,
            IsInTransition = IsTransferInTransition(entity.Transfer),
        };
    }

    private static bool IsTransferInTransition(StaffTransfer? transfer)
    {
        if (transfer?.TransitionStartDate is null || transfer.TransitionEndDate is null)
            return false;

        var today = DateTime.UtcNow.Date;
        return transfer.TransitionStartDate.Value.Date <= today
            && transfer.TransitionEndDate.Value.Date >= today;
    }

    public static StaffMovementDetailDto ToDetailDto(this StaffMovement entity)
    {
        var dto = new StaffMovementDetailDto();

        // Copy all base fields by delegating to ToDto and re-using the logic
        var baseDto = entity.ToDto();
        dto.Id = baseDto.Id;
        dto.TenantId = baseDto.TenantId;
        dto.CreatedAt = baseDto.CreatedAt;
        dto.CreatedBy = baseDto.CreatedBy;
        dto.UpdatedAt = baseDto.UpdatedAt;
        dto.UpdatedBy = baseDto.UpdatedBy;
        dto.MovementNumber = baseDto.MovementNumber;
        dto.EmployeeId = baseDto.EmployeeId;
        dto.EmployeeName = baseDto.EmployeeName;
        dto.EmployeeNumber = baseDto.EmployeeNumber;
        dto.MovementType = baseDto.MovementType;
        dto.Category = baseDto.Category;
        dto.CurrentPositionId = baseDto.CurrentPositionId;
        dto.CurrentPositionTitle = baseDto.CurrentPositionTitle;
        dto.CurrentOrganizationUnitId = baseDto.CurrentOrganizationUnitId;
        dto.CurrentOrganizationUnitName = baseDto.CurrentOrganizationUnitName;
        dto.CurrentOrganizationLevelId = baseDto.CurrentOrganizationLevelId;
        dto.CurrentOrganizationLevelName = baseDto.CurrentOrganizationLevelName;
        dto.CurrentLocationId = baseDto.CurrentLocationId;
        dto.CurrentLocationName = baseDto.CurrentLocationName;
        dto.CurrentLocationLevelId = baseDto.CurrentLocationLevelId;
        dto.CurrentLocationLevelName = baseDto.CurrentLocationLevelName;
        dto.CurrentSupervisorId = baseDto.CurrentSupervisorId;
        dto.CurrentSupervisorName = baseDto.CurrentSupervisorName;
        dto.CurrentSalary = baseDto.CurrentSalary;
        dto.CurrentSalaryGradeId = baseDto.CurrentSalaryGradeId;
        dto.CurrentSalaryGradeCode = baseDto.CurrentSalaryGradeCode;
        dto.CurrentSalaryGradeName = baseDto.CurrentSalaryGradeName;
        dto.CurrentSalaryLevelId = baseDto.CurrentSalaryLevelId;
        dto.CurrentSalaryLevelName = baseDto.CurrentSalaryLevelName;
        dto.CurrentSalaryNotchId = baseDto.CurrentSalaryNotchId;
        dto.CurrentSalaryNotchNumber = baseDto.CurrentSalaryNotchNumber;
        dto.NewPositionId = baseDto.NewPositionId;
        dto.NewPositionTitle = baseDto.NewPositionTitle;
        dto.NewOrganizationUnitId = baseDto.NewOrganizationUnitId;
        dto.NewOrganizationUnitName = baseDto.NewOrganizationUnitName;
        dto.NewOrganizationLevelId = baseDto.NewOrganizationLevelId;
        dto.NewOrganizationLevelName = baseDto.NewOrganizationLevelName;
        dto.NewLocationId = baseDto.NewLocationId;
        dto.NewLocationName = baseDto.NewLocationName;
        dto.NewLocationLevelId = baseDto.NewLocationLevelId;
        dto.NewLocationLevelName = baseDto.NewLocationLevelName;
        dto.NewSupervisorId = baseDto.NewSupervisorId;
        dto.NewSupervisorName = baseDto.NewSupervisorName;
        dto.NewSalary = baseDto.NewSalary;
        dto.NewSalaryGradeId = baseDto.NewSalaryGradeId;
        dto.NewSalaryGradeCode = baseDto.NewSalaryGradeCode;
        dto.NewSalaryGradeName = baseDto.NewSalaryGradeName;
        dto.NewSalaryLevelId = baseDto.NewSalaryLevelId;
        dto.NewSalaryLevelName = baseDto.NewSalaryLevelName;
        dto.NewSalaryNotchId = baseDto.NewSalaryNotchId;
        dto.NewSalaryNotchNumber = baseDto.NewSalaryNotchNumber;
        dto.SalaryIncreaseAmount = baseDto.SalaryIncreaseAmount;
        dto.SalaryIncreasePercentage = baseDto.SalaryIncreasePercentage;
        dto.Reason = baseDto.Reason;
        dto.Justification = baseDto.Justification;
        dto.IsReorganization = baseDto.IsReorganization;
        dto.IsSuccessionPlan = baseDto.IsSuccessionPlan;
        dto.SuccessionPlanId = baseDto.SuccessionPlanId;
        dto.SuccessionPlanNumber = baseDto.SuccessionPlanNumber;
        dto.RequestDate = baseDto.RequestDate;
        dto.EffectiveDate = baseDto.EffectiveDate;
        dto.IsTemporary = baseDto.IsTemporary;
        dto.TemporaryEndDate = baseDto.TemporaryEndDate;
        dto.TemporaryArrangementDetails = baseDto.TemporaryArrangementDetails;
        dto.ReturnProcessed = baseDto.ReturnProcessed;
        dto.ActualReturnDate = baseDto.ActualReturnDate;
        dto.ReturnMovementId = baseDto.ReturnMovementId;
        dto.ReturnMovementNumber = baseDto.ReturnMovementNumber;
        dto.RequestedById = baseDto.RequestedById;
        dto.RequestedByName = baseDto.RequestedByName;
        dto.RequestSubmissionDate = baseDto.RequestSubmissionDate;
        dto.AuthorizedById = baseDto.AuthorizedById;
        dto.AuthorizedByName = baseDto.AuthorizedByName;
        dto.AuthorizationDate = baseDto.AuthorizationDate;
        dto.Status = baseDto.Status;
        dto.RejectionReason = baseDto.RejectionReason;
        dto.RejectedById = baseDto.RejectedById;
        dto.RejectedByName = baseDto.RejectedByName;
        dto.RejectionDate = baseDto.RejectionDate;
        dto.CancellationReason = baseDto.CancellationReason;
        dto.CancelledById = baseDto.CancelledById;
        dto.CancelledByName = baseDto.CancelledByName;
        dto.CancellationDate = baseDto.CancellationDate;
        dto.RequiresEmployeeAcceptance = baseDto.RequiresEmployeeAcceptance;
        dto.EmployeeAccepted = baseDto.EmployeeAccepted;
        dto.EmployeeResponseDate = baseDto.EmployeeResponseDate;
        dto.EmployeeComments = baseDto.EmployeeComments;
        dto.RequiresHandover = baseDto.RequiresHandover;
        dto.HandoverCompletionDate = baseDto.HandoverCompletionDate;
        dto.HandoverNotes = baseDto.HandoverNotes;
        dto.BasedOnAppraisalId = baseDto.BasedOnAppraisalId;
        dto.BasedOnAppraisalNumber = baseDto.BasedOnAppraisalNumber;
        dto.AdditionalNotes = baseDto.AdditionalNotes;

        // Child collections
        dto.ApprovalLevels = entity.ApprovalLevels.Select(a => a.ToDto()).ToList();
        dto.StatusHistory = entity.StatusHistory.Select(h => h.ToDto()).ToList();
        dto.Attachments = entity.Attachments.Select(a => a.ToDto()).ToList();
        dto.ChecklistItems = entity.ChecklistItems.Select(c => c.ToDto()).ToList();

        // Subtype details (populated via EF Include or external repo)
        dto.Promotion  = entity.Promotion?.ToDto();
        dto.Transfer   = entity.Transfer?.ToDto();
        dto.Demotion   = entity.Demotion?.ToDto();
        dto.Secondment = entity.Secondment?.ToDto();

        return dto;
    }

    public static StaffMovement ToEntity(this CreateStaffMovementDto dto, Guid tenantId, Guid userId)
    {
        return new StaffMovement
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            MovementType = dto.MovementType,
            Category = dto.Category,
            // ── Current-state snapshot ────────────────────────────────────────
            CurrentPositionId = dto.CurrentPositionId,
            CurrentOrganizationUnitId = dto.CurrentOrganizationUnitId,
            CurrentOrganizationLevelId = dto.CurrentOrganizationLevelId,
            CurrentLocationId = dto.CurrentLocationId,
            CurrentLocationLevelId = dto.CurrentLocationLevelId,
            CurrentSupervisorId = dto.CurrentSupervisorId,
            CurrentSalary = dto.CurrentSalary,
            CurrentSalaryGradeId = dto.CurrentSalaryGradeId,
            CurrentSalaryLevelId = dto.CurrentSalaryLevelId,
            CurrentSalaryNotchId = dto.CurrentSalaryNotchId,
            // ── New position & salary ─────────────────────────────────────────
            NewPositionId = dto.NewPositionId,
            NewOrganizationUnitId = dto.NewOrganizationUnitId,
            NewOrganizationLevelId = dto.NewOrganizationLevelId,
            NewLocationId = dto.NewLocationId,
            NewLocationLevelId = dto.NewLocationLevelId,
            NewSupervisorId = dto.NewSupervisorId,
            NewSalary = dto.NewSalary,
            NewSalaryGradeId = dto.NewSalaryGradeId,
            NewSalaryLevelId = dto.NewSalaryLevelId,
            NewSalaryNotchId = dto.NewSalaryNotchId,
            Reason = dto.Reason,
            Justification = dto.Justification,
            IsReorganization = dto.IsReorganization,
            IsSuccessionPlan = dto.IsSuccessionPlan,
            SuccessionPlanId = dto.SuccessionPlanId,
            EffectiveDate = dto.EffectiveDate,
            RequestDate = DateTime.UtcNow,
            RequestSubmissionDate = DateTime.UtcNow,
            IsTemporary = dto.IsTemporary,
            TemporaryEndDate = dto.TemporaryEndDate,
            TemporaryArrangementDetails = dto.TemporaryArrangementDetails,
            RequiresEmployeeAcceptance = dto.RequiresEmployeeAcceptance,
            RequiresHandover = dto.RequiresHandover,
            BasedOnAppraisalId = dto.BasedOnAppraisalId,
            AdditionalNotes = dto.AdditionalNotes,
            Status = StaffMovementStatus.Draft,
            RequestedById = userId,
            CreatedBy = userId.ToString(),
            SalaryIncreaseAmount = dto.CurrentSalary > 0
                ? dto.NewSalary - dto.CurrentSalary : null,
            SalaryIncreasePercentage = dto.CurrentSalary > 0
                ? Math.Round((dto.NewSalary - dto.CurrentSalary) / dto.CurrentSalary * 100, 2) : null,
        };
    }

    public static void UpdateEntity(this StaffMovement entity, UpdateStaffMovementDto dto, Guid userId)
    {
        entity.NewPositionId = dto.NewPositionId;
        entity.NewOrganizationUnitId = dto.NewOrganizationUnitId;
        entity.NewOrganizationLevelId = dto.NewOrganizationLevelId;
        entity.NewLocationId = dto.NewLocationId;
        entity.NewLocationLevelId = dto.NewLocationLevelId;
        entity.NewSupervisorId = dto.NewSupervisorId;
        entity.NewSalary = dto.NewSalary;
        entity.NewSalaryGradeId = dto.NewSalaryGradeId;
        entity.NewSalaryLevelId = dto.NewSalaryLevelId;
        entity.NewSalaryNotchId = dto.NewSalaryNotchId;
        entity.Reason = dto.Reason;
        entity.Justification = dto.Justification;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.IsTemporary = dto.IsTemporary;
        entity.TemporaryEndDate = dto.TemporaryEndDate;
        entity.TemporaryArrangementDetails = dto.TemporaryArrangementDetails;
        entity.RequiresEmployeeAcceptance = dto.RequiresEmployeeAcceptance;
        entity.RequiresHandover = dto.RequiresHandover;
        entity.IsReorganization = dto.IsReorganization;
        entity.IsSuccessionPlan = dto.IsSuccessionPlan;
        entity.SuccessionPlanId = dto.SuccessionPlanId;
        entity.BasedOnAppraisalId = dto.BasedOnAppraisalId;
        entity.AdditionalNotes = dto.AdditionalNotes;
        // Recompute salary deltas whenever NewSalary changes
        entity.SalaryIncreaseAmount = entity.CurrentSalary > 0
            ? dto.NewSalary - entity.CurrentSalary : null;
        entity.SalaryIncreasePercentage = entity.CurrentSalary > 0
            ? Math.Round((dto.NewSalary - entity.CurrentSalary) / entity.CurrentSalary * 100, 2) : null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffMovementSummaryDto> ToSummaryDtoList(this IEnumerable<StaffMovement> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // STAFF MOVEMENT APPROVAL LEVEL
    // ========================================================================

    #region StaffMovementApprovalLevel

    public static StaffMovementApprovalLevelDto ToDto(this StaffMovementApprovalLevel entity)
    {
        return new StaffMovementApprovalLevelDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber ?? string.Empty,
            Level = entity.Level,
            RoleName = entity.RoleName,
            ApproverId = entity.ApproverId,
            ApproverName = entity.Approver?.FullName ?? string.Empty,
            Status = entity.Status,
            ActionDate = entity.ActionDate,
            Comments = entity.Comments,
            DelegatedToId = entity.DelegatedToId,
            DelegatedToName = entity.DelegatedTo?.FullName,
            DelegationDate = entity.DelegationDate,
            DelegationReason = entity.DelegationReason,
        };
    }

    public static StaffMovementApprovalLevel ToEntity(this CreateStaffMovementApprovalLevelDto dto, Guid tenantId, Guid userId)
    {
        return new StaffMovementApprovalLevel
        {
            TenantId = tenantId,
            MovementId = dto.MovementId,
            Level = dto.Level,
            RoleName = dto.RoleName,
            ApproverId = dto.ApproverId,
            Status = ApprovalStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffMovementApprovalLevelDto> ToDtoList(this IEnumerable<StaffMovementApprovalLevel> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // STAFF MOVEMENT STATUS HISTORY  (immutable — read only)
    // ========================================================================

    #region StaffMovementStatusHistory

    public static StaffMovementStatusHistoryDto ToDto(this StaffMovementStatusHistory entity)
    {
        return new StaffMovementStatusHistoryDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber ?? string.Empty,
            FromStatus = entity.FromStatus,
            ToStatus = entity.ToStatus,
            ChangedDate = entity.ChangedDate,
            ChangedById = entity.ChangedById,
            ChangedByName = entity.ChangedBy?.FullName ?? string.Empty,
            Reason = entity.Reason,
        };
    }

    public static IEnumerable<StaffMovementStatusHistoryDto> ToDtoList(this IEnumerable<StaffMovementStatusHistory> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // STAFF MOVEMENT ATTACHMENT
    // ========================================================================

    #region StaffMovementAttachment

    public static StaffMovementAttachmentDto ToDto(this StaffMovementAttachment entity)
    {
        return new StaffMovementAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber ?? string.Empty,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Type = entity.Type,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
        };
    }

    public static StaffMovementAttachment ToEntity(this CreateStaffMovementAttachmentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffMovementAttachment
        {
            TenantId = tenantId,
            MovementId = dto.MovementId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            FileUploadRecordId = dto.FileUploadRecordId,
            DocumentRecordId = dto.DocumentRecordId,
            DocumentVersionId = dto.DocumentVersionId,
            Type = dto.Type,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            UploadedById = new Guid(userId.ToString()),
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffMovementAttachmentDto> ToDtoList(this IEnumerable<StaffMovementAttachment> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // STAFF MOVEMENT CHECKLIST ITEM
    // ========================================================================

    #region StaffMovementChecklistItem

    public static StaffMovementChecklistItemDto ToDto(this StaffMovementChecklistItem entity)
    {
        return new StaffMovementChecklistItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            TaskDescription = entity.TaskDescription,
            Category = entity.Category,
            IsRequired = entity.IsRequired,
            ResponsiblePersonId = entity.ResponsiblePersonId,
            ResponsiblePersonName = entity.ResponsiblePerson?.FullName,
            DueDate = entity.DueDate,
            IsCompleted = entity.IsCompleted,
            CompletionDate = entity.CompletionDate,
            CompletionNotes = entity.CompletionNotes,
            DisplayOrder = entity.DisplayOrder,
        };
    }

    public static StaffMovementChecklistItem ToEntity(this CreateStaffMovementChecklistItemDto dto, Guid tenantId, Guid userId)
    {
        return new StaffMovementChecklistItem
        {
            TenantId = tenantId,
            MovementId = dto.MovementId,
            TaskDescription = dto.TaskDescription,
            Category = dto.Category,
            IsRequired = dto.IsRequired,
            ResponsiblePersonId = dto.ResponsiblePersonId,
            DueDate = dto.DueDate,
            DisplayOrder = dto.DisplayOrder,
            IsCompleted = false,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<StaffMovementChecklistItemDto> ToDtoList(this IEnumerable<StaffMovementChecklistItem> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // STAFF PROMOTION
    // ========================================================================

    #region StaffPromotion

    public static StaffPromotionDto ToDto(this StaffPromotion entity)
    {
        return new StaffPromotionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber ?? string.Empty,
            Type = entity.Type,
            GradeLevelIncrease = entity.GradeLevelIncrease,
            IsActingPromotion = entity.IsActingPromotion,
            ActingPeriodEndDate = entity.ActingPeriodEndDate,
            ActingConditions = entity.ActingConditions,
            AdditionalResponsibilities = entity.AdditionalResponsibilities,
            RequiresTraining = entity.RequiresTraining,
            RequiredTraining = entity.RequiredTraining,
        };
    }

    public static StaffPromotion ToEntity(this CreateStaffPromotionDto dto, Guid tenantId, Guid userId)
    {
        return new StaffPromotion
        {
            TenantId = tenantId,
            MovementId = dto.MovementId,
            Type = dto.Type,
            GradeLevelIncrease = dto.GradeLevelIncrease,
            IsActingPromotion = dto.IsActingPromotion,
            ActingPeriodEndDate = dto.ActingPeriodEndDate,
            ActingConditions = dto.ActingConditions,
            AdditionalResponsibilities = dto.AdditionalResponsibilities,
            RequiresTraining = dto.RequiresTraining,
            RequiredTraining = dto.RequiredTraining,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffPromotion entity, UpdateStaffPromotionDto dto, Guid userId)
    {
        entity.Type = dto.Type;
        entity.GradeLevelIncrease = dto.GradeLevelIncrease;
        entity.IsActingPromotion = dto.IsActingPromotion;
        entity.ActingPeriodEndDate = dto.ActingPeriodEndDate;
        entity.ActingConditions = dto.ActingConditions;
        entity.AdditionalResponsibilities = dto.AdditionalResponsibilities;
        entity.RequiresTraining = dto.RequiresTraining;
        entity.RequiredTraining = dto.RequiredTraining;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF TRANSFER
    // ========================================================================

    #region StaffTransfer

    public static StaffTransferDto ToDto(this StaffTransfer entity)
    {
        return new StaffTransferDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber ?? string.Empty,
            Type = entity.Type,
            ReasonCategory = entity.ReasonCategory,
            RequiresRelocation = entity.RequiresRelocation,
            RelocationAssistanceProvided = entity.RelocationAssistanceProvided,
            RelocationAllowance = entity.RelocationAllowance,
            RelocationDetails = entity.RelocationDetails,
            HousingAssistanceProvided = entity.HousingAssistanceProvided,
            HousingDetails = entity.HousingDetails,
            TransitionPeriodDays = entity.TransitionPeriodDays,
            TransitionStartDate = entity.TransitionStartDate,
            TransitionEndDate = entity.TransitionEndDate,
            ReplacementEmployeeId = entity.ReplacementEmployeeId,
            ReplacementEmployeeName = entity.ReplacementEmployee?.FullName,
            IsInterCompany = entity.IsInterCompany,
            DestinationCompanyId = entity.DestinationCompanyId,
            EmploymentContinues = entity.EmploymentContinues,
            InterCompanyTransferDetails = entity.InterCompanyTransferDetails,
        };
    }

    public static StaffTransfer ToEntity(this CreateStaffTransferDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTransfer
        {
            TenantId = tenantId,
            MovementId = dto.MovementId,
            Type = dto.Type,
            ReasonCategory = dto.ReasonCategory,
            RequiresRelocation = dto.RequiresRelocation,
            RelocationAssistanceProvided = dto.RelocationAssistanceProvided,
            RelocationAllowance = dto.RelocationAllowance,
            RelocationDetails = dto.RelocationDetails,
            HousingAssistanceProvided = dto.HousingAssistanceProvided,
            HousingDetails = dto.HousingDetails,
            TransitionPeriodDays = dto.TransitionPeriodDays,
            TransitionStartDate = dto.TransitionStartDate,
            TransitionEndDate = dto.TransitionEndDate,
            ReplacementEmployeeId = dto.ReplacementEmployeeId,
            IsInterCompany = dto.IsInterCompany,
            DestinationCompanyId = dto.DestinationCompanyId,
            EmploymentContinues = dto.EmploymentContinues,
            InterCompanyTransferDetails = dto.InterCompanyTransferDetails,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTransfer entity, UpdateStaffTransferDto dto, Guid userId)
    {
        entity.Type = dto.Type;
        entity.ReasonCategory = dto.ReasonCategory;
        entity.RequiresRelocation = dto.RequiresRelocation;
        entity.RelocationAssistanceProvided = dto.RelocationAssistanceProvided;
        entity.RelocationAllowance = dto.RelocationAllowance;
        entity.RelocationDetails = dto.RelocationDetails;
        entity.HousingAssistanceProvided = dto.HousingAssistanceProvided;
        entity.HousingDetails = dto.HousingDetails;
        entity.TransitionPeriodDays = dto.TransitionPeriodDays;
        entity.TransitionStartDate = dto.TransitionStartDate;
        entity.TransitionEndDate = dto.TransitionEndDate;
        entity.ReplacementEmployeeId = dto.ReplacementEmployeeId;
        entity.IsInterCompany = dto.IsInterCompany;
        entity.DestinationCompanyId = dto.DestinationCompanyId;
        entity.EmploymentContinues = dto.EmploymentContinues;
        entity.InterCompanyTransferDetails = dto.InterCompanyTransferDetails;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF DEMOTION
    // ========================================================================

    #region StaffDemotion

    public static StaffDemotionDto ToDto(this StaffDemotion entity)
    {
        return new StaffDemotionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber ?? string.Empty,
            Reason = entity.Reason,
            GradeLevelDecrease = entity.GradeLevelDecrease,
            IsDisciplinaryAction = entity.IsDisciplinaryAction,
            DisciplinaryActionId = entity.DisciplinaryActionId,
            DisciplinaryCaseNumber = entity.DisciplinaryAction?.CaseNumber,
            IsPerformanceRelated = entity.IsPerformanceRelated,
            PerformanceImprovementPlanId = entity.PerformanceImprovementPlanId,
            PipNumber = entity.PerformanceImprovementPlan?.PipNumber,
            EmployeeNotified = entity.EmployeeNotified,
            NotificationDate = entity.NotificationDate,
            RightToAppeal = entity.RightToAppeal,
            AppealDeadline = entity.AppealDeadline,
            EmployeeResponse = entity.EmployeeResponse,
            EmployeeResponseDate = entity.EmployeeResponseDate,
        };
    }

    public static StaffDemotion ToEntity(this CreateStaffDemotionDto dto, Guid tenantId, Guid userId)
    {
        return new StaffDemotion
        {
            TenantId = tenantId,
            MovementId = dto.MovementId,
            Reason = dto.Reason,
            GradeLevelDecrease = dto.GradeLevelDecrease,
            IsDisciplinaryAction = dto.IsDisciplinaryAction,
            DisciplinaryActionId = dto.DisciplinaryActionId,
            IsPerformanceRelated = dto.IsPerformanceRelated,
            PerformanceImprovementPlanId = dto.PerformanceImprovementPlanId,
            RightToAppeal = dto.RightToAppeal,
            AppealDeadline = dto.AppealDeadline,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffDemotion entity, UpdateStaffDemotionDto dto, Guid userId)
    {
        entity.Reason = dto.Reason;
        entity.GradeLevelDecrease = dto.GradeLevelDecrease;
        entity.EmployeeNotified = dto.EmployeeNotified;
        entity.NotificationDate = dto.NotificationDate;
        entity.RightToAppeal = dto.RightToAppeal;
        entity.AppealDeadline = dto.AppealDeadline;
        entity.EmployeeResponse = dto.EmployeeResponse;
        entity.EmployeeResponseDate = dto.EmployeeResponseDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF SECONDMENT
    // ========================================================================

    #region StaffSecondment

    public static StaffSecondmentDto ToDto(this StaffSecondment entity)
    {
        return new StaffSecondmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber ?? string.Empty,
            Type = entity.Type,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            // DurationMonths is [NotMapped] computed — read directly from the entity
            DurationMonths = entity.DurationMonths,
            IsExternal = entity.IsExternal,
            HostOrganization = entity.HostOrganization,
            HostOrganizationContact = entity.HostOrganizationContact,
            TermsAndConditions = entity.TermsAndConditions,
            SalaryPaidByHomeOrganization = entity.SalaryPaidByHomeOrganization,
            AllowancesPaidByHostOrganization = entity.AllowancesPaidByHostOrganization,
            SecondmentAllowance = entity.SecondmentAllowance,
            Objectives = entity.Objectives,
            ExpectedOutcomes = entity.ExpectedOutcomes,
            ReturnGuaranteed = entity.ReturnGuaranteed,
            ReturnArrangements = entity.ReturnArrangements,
            ExtensionAllowed = entity.ExtensionAllowed,
            MaxExtensionMonths = entity.MaxExtensionMonths,
        };
    }

    public static StaffSecondment ToEntity(this CreateStaffSecondmentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffSecondment
        {
            TenantId = tenantId,
            MovementId = dto.MovementId,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            // DurationMonths is [NotMapped] — not set here; derived from dates at runtime
            IsExternal = dto.IsExternal,
            HostOrganization = dto.HostOrganization,
            HostOrganizationContact = dto.HostOrganizationContact,
            TermsAndConditions = dto.TermsAndConditions,
            SalaryPaidByHomeOrganization = dto.SalaryPaidByHomeOrganization,
            AllowancesPaidByHostOrganization = dto.AllowancesPaidByHostOrganization,
            SecondmentAllowance = dto.SecondmentAllowance,
            Objectives = dto.Objectives,
            ExpectedOutcomes = dto.ExpectedOutcomes,
            ReturnGuaranteed = dto.ReturnGuaranteed,
            ReturnArrangements = dto.ReturnArrangements,
            ExtensionAllowed = dto.ExtensionAllowed,
            MaxExtensionMonths = dto.MaxExtensionMonths,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffSecondment entity, UpdateStaffSecondmentDto dto, Guid userId)
    {
        entity.Type = dto.Type;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.IsExternal = dto.IsExternal;
        entity.HostOrganization = dto.HostOrganization;
        entity.HostOrganizationContact = dto.HostOrganizationContact;
        entity.TermsAndConditions = dto.TermsAndConditions;
        entity.SalaryPaidByHomeOrganization = dto.SalaryPaidByHomeOrganization;
        entity.AllowancesPaidByHostOrganization = dto.AllowancesPaidByHostOrganization;
        entity.SecondmentAllowance = dto.SecondmentAllowance;
        entity.Objectives = dto.Objectives;
        entity.ExpectedOutcomes = dto.ExpectedOutcomes;
        entity.ReturnGuaranteed = dto.ReturnGuaranteed;
        entity.ReturnArrangements = dto.ReturnArrangements;
        entity.ExtensionAllowed = dto.ExtensionAllowed;
        entity.MaxExtensionMonths = dto.MaxExtensionMonths;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF ACTING APPOINTMENT
    // ========================================================================

    #region StaffActingAppointment

    public static StaffActingAppointmentDto ToDto(this StaffActingAppointment entity)
    {
        return new StaffActingAppointmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AppointmentNumber = entity.AppointmentNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            ActingPositionId = entity.ActingPositionId,
            ActingPositionTitle = entity.ActingPosition?.Title ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Reason = entity.Reason,
            ActingForEmployeeId = entity.ActingForEmployeeId,
            ActingForEmployeeName = entity.ActingForEmployee?.FullName,
            ReceivesActingAllowance = entity.ReceivesActingAllowance,
            ActingAllowance = entity.ActingAllowance,
            AllowanceCalculation = entity.AllowanceCalculation,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber,
            Status = entity.Status,
            CompletionDate = entity.CompletionDate,
            ConvertedToPermanent = entity.ConvertedToPermanent,
            ConversionDate = entity.ConversionDate,
            ConversionMovementId = entity.ConversionMovementId,
            ConversionMovementNumber = entity.ConversionMovement?.MovementNumber,
            Notes = entity.Notes,
        };
    }

    public static StaffActingAppointmentSummaryDto ToSummaryDto(this StaffActingAppointment entity)
    {
        return new StaffActingAppointmentSummaryDto
        {
            Id = entity.Id,
            AppointmentNumber = entity.AppointmentNumber,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            ActingPositionTitle = entity.ActingPosition?.Title ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Reason = entity.Reason,
            Status = entity.Status,
            ConvertedToPermanent = entity.ConvertedToPermanent,
            MovementId = entity.MovementId,
        };
    }

    public static StaffActingAppointment ToEntity(this CreateStaffActingAppointmentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffActingAppointment
        {
            TenantId = tenantId,
            // AppointmentNumber is generated in the service layer
            AppointmentNumber = string.Empty,
            EmployeeId = dto.EmployeeId,
            ActingPositionId = dto.ActingPositionId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason,
            ActingForEmployeeId = dto.ActingForEmployeeId,
            ReceivesActingAllowance = dto.ReceivesActingAllowance,
            ActingAllowance = dto.ActingAllowance,
            AllowanceCalculation = dto.AllowanceCalculation,
            MovementId = dto.MovementId,
            Status = StaffActingStatus.Active,
            ConvertedToPermanent = false,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffActingAppointment entity, UpdateStaffActingAppointmentDto dto, Guid userId)
    {
        entity.EndDate = dto.EndDate;
        entity.ReceivesActingAllowance = dto.ReceivesActingAllowance;
        entity.ActingAllowance = dto.ActingAllowance;
        entity.AllowanceCalculation = dto.AllowanceCalculation;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffActingAppointmentSummaryDto> ToSummaryDtoList(this IEnumerable<StaffActingAppointment> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // EMPLOYEE CAREER PATH
    // ========================================================================

    #region EmployeeCareerPath

    public static EmployeeCareerPathDto ToDto(this EmployeeCareerPath entity)
    {
        return new EmployeeCareerPathDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name ?? string.Empty,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            LocationLevelId = entity.LocationLevelId,
            LocationLevelName = entity.LocationLevel?.Name,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent,
            MovementId = entity.MovementId,
            MovementNumber = entity.Movement?.MovementNumber,
            MovementType = entity.Movement?.MovementType,
            Salary = entity.Salary,
            SalaryGradeId = entity.SalaryGradeId,
            SalaryGradeCode = entity.SalaryGrade?.Code,
            SalaryGradeName = entity.SalaryGrade?.Name,
            SalaryLevelId = entity.SalaryLevelId,
            SalaryLevelName = entity.SalaryLevel?.Name,
            SalaryNotchId = entity.SalaryNotchId,
            SalaryNotchNumber = entity.SalaryNotch?.NotchNumber,
            Achievements = entity.Achievements,
            KeyProjects = entity.KeyProjects,
        };
    }

    public static EmployeeCareerPathSummaryDto ToSummaryDto(this EmployeeCareerPath entity)
    {
        return new EmployeeCareerPathSummaryDto
        {
            Id = entity.Id,
            PositionTitle = entity.Position?.Title ?? string.Empty,
            OrganizationUnitName = entity.OrganizationUnit?.Name ?? string.Empty,
            LocationName = entity.Location?.Name,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent,
            DurationMonths = entity.IsCurrent
                ? (int)((DateTime.UtcNow - entity.StartDate).TotalDays / 30.44)
                : entity.EndDate.HasValue
                    ? (int)((entity.EndDate.Value - entity.StartDate).TotalDays / 30.44)
                    : 0,
            Salary = entity.Salary,
            SalaryGradeName = entity.SalaryGrade?.Name,
            MovementType = entity.Movement?.MovementType,
        };
    }

    public static EmployeeCareerPath ToEntity(this CreateEmployeeCareerPathDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeCareerPath
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            PositionId = dto.PositionId,
            OrganizationUnitId = dto.OrganizationUnitId,
            OrganizationLevelId = dto.OrganizationLevelId,
            LocationId = dto.LocationId,
            LocationLevelId = dto.LocationLevelId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsCurrent = dto.IsCurrent,
            MovementId = dto.MovementId,
            Salary = dto.Salary,
            SalaryGradeId = dto.SalaryGradeId,
            SalaryLevelId = dto.SalaryLevelId,
            SalaryNotchId = dto.SalaryNotchId,
            Achievements = dto.Achievements,
            KeyProjects = dto.KeyProjects,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeCareerPath entity, UpdateEmployeeCareerPathDto dto, Guid userId)
    {
        entity.EndDate = dto.EndDate;
        entity.IsCurrent = dto.IsCurrent;
        entity.Achievements = dto.Achievements;
        entity.KeyProjects = dto.KeyProjects;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeCareerPathSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeCareerPath> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion
}
