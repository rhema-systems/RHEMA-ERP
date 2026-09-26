using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class StaffRequisitionMappingExtensions
{
    // ========================================================================
    // STAFF REQUISITION
    // ========================================================================

    #region StaffRequisition

    public static StaffRequisitionDto ToDto(this StaffRequisition entity)
    {
        return new StaffRequisitionDto
        {
            Id                          = entity.Id,
            TenantId                    = entity.TenantId,
            CreatedAt                   = entity.CreatedAt,
            CreatedBy                   = entity.CreatedBy ?? string.Empty,
            UpdatedAt                   = entity.UpdatedAt,
            UpdatedBy                   = entity.UpdatedBy,
            RequisitionNumber           = entity.RequisitionNumber,
            RequisitionTitle            = entity.RequisitionTitle,
            Description                 = entity.Description,

            // Location & Organisation
            LocationLevelId             = entity.LocationLevelId,
            LocationLevelName           = entity.LocationLevel?.Name,
            LocationId                  = entity.LocationId,
            LocationName                = entity.Location?.Name,
            OrganizationLevelId         = entity.OrganizationLevelId,
            OrganizationLevelName       = entity.OrganizationLevel?.Name,
            OrganizationUnitId          = entity.OrganizationUnitId,
            OrganizationUnitName        = entity.OrganizationUnit?.Name,

            // Position & Job Description
            PositionId                  = entity.PositionId,
            PositionTitle               = entity.Position?.Title ?? string.Empty,
            JobDescriptionId            = entity.JobDescriptionId,
            JobDescriptionTitle         = entity.JobDescription?.JobTitle,

            // Classification
            Type                        = entity.Type,
            Priority                    = entity.Priority,
            Status                      = entity.Status,

            // Headcount
            NumberOfPositions           = entity.NumberOfPositions,
            PositionsFilled             = entity.PositionsFilled,

            // Replacement
            ReplacementForEmployeeId    = entity.ReplacementForEmployeeId,
            ReplacementForEmployeeName  = entity.ReplacementForEmployee?.FullName,
            ReplacementReason           = entity.ReplacementReason,
            EmployeeDepartureDate       = entity.EmployeeDepartureDate,

            // Timeline
            RequestDate                 = entity.RequestDate,
            DesiredStartDate            = entity.DesiredStartDate,
            LatestAcceptableStartDate   = entity.LatestAcceptableStartDate,
            TargetFillDate              = entity.TargetFillDate,
            ExpectedOfferDate           = entity.ExpectedOfferDate,
            DaysToFill                  = entity.DaysToFill,
            TargetStartDateReason       = entity.TargetStartDateReason,

            // Justification
            BusinessJustification       = entity.BusinessJustification,
            ImpactIfNotFilled           = entity.ImpactIfNotFilled,

            // Budget
            IsBudgeted                  = entity.IsBudgeted,
            BudgetCode                  = entity.BudgetCode,
            ManpowerBudgetLineId        = entity.ManpowerBudgetLineId,
            ExceptionJustification      = entity.ExceptionJustification,
            EstablishmentSnapshotOn     = entity.EstablishmentSnapshotOn,
            EstablishmentSnapshotIsEstablished = entity.EstablishmentSnapshotIsEstablished,
            EstablishmentSnapshotExpected = entity.EstablishmentSnapshotExpected,
            EstablishmentSnapshotFilled = entity.EstablishmentSnapshotFilled,
            EstablishmentSnapshotSourceBudgetNumber = entity.EstablishmentSnapshotSourceBudgetNumber,

            // Recruitment Strategy
            AllowInternalCandidates     = entity.AllowInternalCandidates,
            AllowExternalCandidates     = entity.AllowExternalCandidates,

            // Requestor
            RequestedById               = entity.RequestedById,
            RequestedByName             = entity.RequestedBy?.FullName ?? string.Empty,

            // Fulfillment
            IsFulfilled                 = entity.IsFulfilled,
            FulfilledDate               = entity.FulfilledDate,

            // Cancellation
            CancelledById               = entity.CancelledById,
            CancelledByName             = entity.CancelledBy?.FullName,
            CancelledDate               = entity.CancelledDate,
            CancellationReason          = entity.CancellationReason,

            // Workflow & Vacancy
            WorkflowInstanceId          = entity.WorkflowInstanceId,
            JobVacancyId                = entity.JobVacancyId,
            JobVacancyNumber            = entity.JobVacancy?.VacancyNumber,

            Notes                       = entity.Notes,
        };
    }

    public static StaffRequisitionDetailDto ToDetailDto(this StaffRequisition entity)
    {
        return new StaffRequisitionDetailDto
        {
            Id                          = entity.Id,
            TenantId                    = entity.TenantId,
            CreatedAt                   = entity.CreatedAt,
            CreatedBy                   = entity.CreatedBy ?? string.Empty,
            UpdatedAt                   = entity.UpdatedAt,
            UpdatedBy                   = entity.UpdatedBy,
            RequisitionNumber           = entity.RequisitionNumber,
            RequisitionTitle            = entity.RequisitionTitle,
            Description                 = entity.Description,
            LocationLevelId             = entity.LocationLevelId,
            LocationLevelName           = entity.LocationLevel?.Name,
            LocationId                  = entity.LocationId,
            LocationName                = entity.Location?.Name,
            OrganizationLevelId         = entity.OrganizationLevelId,
            OrganizationLevelName       = entity.OrganizationLevel?.Name,
            OrganizationUnitId          = entity.OrganizationUnitId,
            OrganizationUnitName        = entity.OrganizationUnit?.Name,
            PositionId                  = entity.PositionId,
            PositionTitle               = entity.Position?.Title ?? string.Empty,
            JobDescriptionId            = entity.JobDescriptionId,
            JobDescriptionTitle         = entity.JobDescription?.JobTitle,
            Type                        = entity.Type,
            Priority                    = entity.Priority,
            Status                      = entity.Status,
            NumberOfPositions           = entity.NumberOfPositions,
            PositionsFilled             = entity.PositionsFilled,
            ReplacementForEmployeeId    = entity.ReplacementForEmployeeId,
            ReplacementForEmployeeName  = entity.ReplacementForEmployee?.FullName,
            ReplacementReason           = entity.ReplacementReason,
            EmployeeDepartureDate       = entity.EmployeeDepartureDate,
            RequestDate                 = entity.RequestDate,
            DesiredStartDate            = entity.DesiredStartDate,
            LatestAcceptableStartDate   = entity.LatestAcceptableStartDate,
            TargetFillDate              = entity.TargetFillDate,
            ExpectedOfferDate           = entity.ExpectedOfferDate,
            DaysToFill                  = entity.DaysToFill,
            TargetStartDateReason       = entity.TargetStartDateReason,
            BusinessJustification       = entity.BusinessJustification,
            ImpactIfNotFilled           = entity.ImpactIfNotFilled,
            IsBudgeted                  = entity.IsBudgeted,
            BudgetCode                  = entity.BudgetCode,
            ManpowerBudgetLineId        = entity.ManpowerBudgetLineId,
            ExceptionJustification      = entity.ExceptionJustification,
            EstablishmentSnapshotOn     = entity.EstablishmentSnapshotOn,
            EstablishmentSnapshotIsEstablished = entity.EstablishmentSnapshotIsEstablished,
            EstablishmentSnapshotExpected = entity.EstablishmentSnapshotExpected,
            EstablishmentSnapshotFilled = entity.EstablishmentSnapshotFilled,
            EstablishmentSnapshotSourceBudgetNumber = entity.EstablishmentSnapshotSourceBudgetNumber,
            AllowInternalCandidates     = entity.AllowInternalCandidates,
            AllowExternalCandidates     = entity.AllowExternalCandidates,
            RequestedById               = entity.RequestedById,
            RequestedByName             = entity.RequestedBy?.FullName ?? string.Empty,
            IsFulfilled                 = entity.IsFulfilled,
            FulfilledDate               = entity.FulfilledDate,
            CancelledById               = entity.CancelledById,
            CancelledByName             = entity.CancelledBy?.FullName,
            CancelledDate               = entity.CancelledDate,
            CancellationReason          = entity.CancellationReason,
            WorkflowInstanceId          = entity.WorkflowInstanceId,
            JobVacancyId                = entity.JobVacancyId,
            JobVacancyNumber            = entity.JobVacancy?.VacancyNumber,
            Notes                       = entity.Notes,
            Costs                       = entity.Costs.Select(c => c.ToDto()).ToList(),
            Attachments                 = entity.Attachments.Select(a => a.ToDto()).ToList(),
            Comments                    = entity.Comments.Where(c => c.ParentCommentId == null).Select(c => c.ToDto()).ToList(),
            History                     = entity.History.Select(h => h.ToDto()).ToList(),
        };
    }

    public static StaffRequisitionSummaryDto ToSummaryDto(this StaffRequisition entity)
    {
        return new StaffRequisitionSummaryDto
        {
            Id                   = entity.Id,
            RequisitionNumber    = entity.RequisitionNumber,
            RequisitionTitle     = entity.RequisitionTitle,
            PositionTitle        = entity.Position?.Title ?? string.Empty,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            LocationName         = entity.Location?.Name,
            Type                 = entity.Type,
            Priority             = entity.Priority,
            Status               = entity.Status,
            NumberOfPositions    = entity.NumberOfPositions,
            PositionsFilled      = entity.PositionsFilled,
            RequestDate          = entity.RequestDate,
            DesiredStartDate     = entity.DesiredStartDate,
            RequestedByName      = entity.RequestedBy?.FullName ?? string.Empty,
        };
    }

    public static IEnumerable<StaffRequisitionSummaryDto> ToSummaryDtoList(this IEnumerable<StaffRequisition> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static StaffRequisition ToEntity(this CreateStaffRequisitionDto dto, Guid tenantId, Guid requestedById)
    {
        return new StaffRequisition
        {
            TenantId                    = tenantId,
            PositionId                  = dto.PositionId,
            JobDescriptionId            = dto.JobDescriptionId,
            LocationLevelId             = dto.LocationLevelId,
            LocationId                  = dto.LocationId,
            OrganizationLevelId         = dto.OrganizationLevelId,
            OrganizationUnitId          = dto.OrganizationUnitId,
            Type                        = dto.Type,
            Priority                    = dto.Priority,
            RequisitionTitle            = dto.RequisitionTitle,
            Description                 = dto.Description,
            NumberOfPositions           = dto.NumberOfPositions,
            ReplacementForEmployeeId    = dto.ReplacementForEmployeeId,
            ReplacementReason           = dto.ReplacementReason,
            EmployeeDepartureDate       = dto.EmployeeDepartureDate,
            DesiredStartDate            = dto.DesiredStartDate,
            LatestAcceptableStartDate   = dto.LatestAcceptableStartDate,
            TargetFillDate              = dto.TargetFillDate,
            ExpectedOfferDate           = dto.ExpectedOfferDate,
            TargetStartDateReason       = dto.TargetStartDateReason,
            BusinessJustification       = dto.BusinessJustification,
            ImpactIfNotFilled           = dto.ImpactIfNotFilled,
            // ⚠ IsBudgeted / BudgetCode are NOT taken from the DTO since round 2b, R5: the service
            // derives both from ManpowerBudgetLineId (ApplyBudgetLinkAsync). A client still sending
            // them is ignored.
            ExceptionJustification      = dto.ExceptionJustification,
            AllowInternalCandidates     = dto.AllowInternalCandidates,
            AllowExternalCandidates     = dto.AllowExternalCandidates,
            RequestedById               = requestedById,
            Notes                       = dto.Notes,
            Status                      = StaffRequisitionStatus.Draft,
            CreatedBy                   = requestedById.ToString(),
        };
    }

    public static void UpdateEntity(this StaffRequisition entity, UpdateStaffRequisitionDto dto, Guid userId)
    {
        entity.PositionId               = dto.PositionId;
        entity.JobDescriptionId         = dto.JobDescriptionId;
        entity.LocationLevelId          = dto.LocationLevelId;
        entity.LocationId               = dto.LocationId;
        entity.OrganizationLevelId      = dto.OrganizationLevelId;
        entity.OrganizationUnitId       = dto.OrganizationUnitId;
        entity.Type                     = dto.Type;
        entity.Priority                 = dto.Priority;
        entity.RequisitionTitle         = dto.RequisitionTitle;
        entity.Description              = dto.Description;
        entity.NumberOfPositions        = dto.NumberOfPositions;
        entity.ReplacementForEmployeeId = dto.ReplacementForEmployeeId;
        entity.ReplacementReason        = dto.ReplacementReason;
        entity.EmployeeDepartureDate    = dto.EmployeeDepartureDate;
        entity.DesiredStartDate         = dto.DesiredStartDate;
        entity.LatestAcceptableStartDate= dto.LatestAcceptableStartDate;
        entity.TargetFillDate           = dto.TargetFillDate;
        entity.ExpectedOfferDate        = dto.ExpectedOfferDate;
        entity.TargetStartDateReason    = dto.TargetStartDateReason;
        entity.BusinessJustification    = dto.BusinessJustification;
        entity.ImpactIfNotFilled        = dto.ImpactIfNotFilled;
        // ⚠ IsBudgeted / BudgetCode: derived by the service from the budget link (R5), not here.
        entity.ExceptionJustification   = dto.ExceptionJustification;
        entity.AllowInternalCandidates  = dto.AllowInternalCandidates;
        entity.AllowExternalCandidates  = dto.AllowExternalCandidates;
        entity.Notes                    = dto.Notes;
        entity.UpdatedAt                = DateTime.UtcNow;
        entity.UpdatedBy                = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF REQUISITION COST
    // ========================================================================

    #region StaffRequisitionCost

    public static StaffRequisitionCostDto ToDto(this StaffRequisitionCost entity)
    {
        return new StaffRequisitionCostDto
        {
            Id                   = entity.Id,
            TenantId             = entity.TenantId,
            CreatedAt            = entity.CreatedAt,
            CreatedBy            = entity.CreatedBy ?? string.Empty,
            UpdatedAt            = entity.UpdatedAt,
            UpdatedBy            = entity.UpdatedBy,
            RequisitionId        = entity.RequisitionId,
            RequisitionNumber    = entity.Requisition?.RequisitionNumber ?? string.Empty,
            Category             = entity.Category,
            Purpose              = entity.Purpose,
            Amount               = entity.Amount,
            Currency             = entity.Currency,
            ExchangeRate         = entity.ExchangeRate,
            Description          = entity.Description,
            PaymentVoucherNumber = entity.PaymentVoucherNumber,
            RecordedById         = entity.RecordedById,
            RecordedByName       = entity.RecordedBy?.FullName ?? string.Empty,
            RecordedDate         = entity.RecordedDate,
            CostDate             = entity.CostDate,
            AmountBaseCurrency   = entity.AmountBaseCurrency,
            SupplierId           = entity.SupplierId,
            SupplierName         = entity.Supplier?.Name,
            PayeeName            = entity.PayeeName,
            Status               = entity.Status,
            ApprovedById         = entity.ApprovedById,
            ApprovedByName       = entity.ApprovedBy?.FullName,
            ApprovedOn           = entity.ApprovedOn,
            ApprovalNote         = entity.ApprovalNote,
        };
    }

    public static StaffRequisitionCost ToEntity(this CreateStaffRequisitionCostDto dto, Guid tenantId, Guid recordedById)
    {
        return new StaffRequisitionCost
        {
            TenantId             = tenantId,
            RequisitionId        = dto.RequisitionId,
            Category             = dto.Category,
            Purpose              = dto.Purpose,
            Amount               = dto.Amount,
            Currency             = dto.Currency,
            // ⚠ ExchangeRate / AmountBaseCurrency / PayeeName-from-supplier: written by the
            // service (ApplyCostMoneyAsync) from Finance's masters, not from the caller.
            CostDate             = dto.CostDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            SupplierId           = dto.SupplierId,
            PayeeName            = dto.PayeeName,
            Description          = dto.Description,
            PaymentVoucherNumber = dto.PaymentVoucherNumber,
            RecordedById         = recordedById,
            CreatedBy            = recordedById.ToString(),
        };
    }

    public static void UpdateEntity(this StaffRequisitionCost entity, UpdateStaffRequisitionCostDto dto, Guid userId)
    {
        entity.Category             = dto.Category;
        entity.Purpose              = dto.Purpose;
        entity.Amount               = dto.Amount;
        entity.Currency             = dto.Currency;
        // ⚠ ExchangeRate / AmountBaseCurrency: the service's ApplyCostMoneyAsync, not here.
        if (dto.CostDate.HasValue) entity.CostDate = dto.CostDate.Value;
        entity.SupplierId           = dto.SupplierId;
        entity.PayeeName            = dto.PayeeName;
        entity.Description          = dto.Description;
        entity.PaymentVoucherNumber = dto.PaymentVoucherNumber;
        entity.UpdatedAt            = DateTime.UtcNow;
        entity.UpdatedBy            = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF REQUISITION ATTACHMENT
    // ========================================================================

    #region StaffRequisitionAttachment

    public static StaffRequisitionAttachmentDto ToDto(this StaffRequisitionAttachment entity)
    {
        return new StaffRequisitionAttachmentDto
        {
            Id                = entity.Id,
            TenantId          = entity.TenantId,
            CreatedAt         = entity.CreatedAt,
            CreatedBy         = entity.CreatedBy ?? string.Empty,
            UpdatedAt         = entity.UpdatedAt,
            UpdatedBy         = entity.UpdatedBy,
            RequisitionId     = entity.RequisitionId,
            RequisitionNumber = entity.Requisition?.RequisitionNumber ?? string.Empty,
            FileName          = entity.FileName,
            FilePath          = entity.FilePath,
            Description       = entity.Description,
            UploadDate        = entity.UploadDate,
            UploadedById      = entity.UploadedById,
            UploadedByName    = entity.UploadedBy?.FullName ?? string.Empty,
        };
    }

    // The ToEntity mapper for attachments is gone with its DTO: the row is now built in
    // StaffRequisitionService.AddAttachmentAsync from the scanned document the upload gate returns,
    // so there is no caller payload left to map.

    #endregion

    // ========================================================================
    // STAFF REQUISITION COMMENT
    // ========================================================================

    #region StaffRequisitionComment

    public static StaffRequisitionCommentDto ToDto(this StaffRequisitionComment entity)
    {
        return new StaffRequisitionCommentDto
        {
            Id              = entity.Id,
            TenantId        = entity.TenantId,
            CreatedAt       = entity.CreatedAt,
            CreatedBy       = entity.CreatedBy ?? string.Empty,
            UpdatedAt       = entity.UpdatedAt,
            UpdatedBy       = entity.UpdatedBy,
            RequisitionId   = entity.RequisitionId,
            ParentCommentId = entity.ParentCommentId,
            Body            = entity.Body,
            AuthorId        = entity.AuthorId,
            AuthorName      = entity.Author?.FullName ?? string.Empty,
            PostedDate      = entity.PostedDate,
            Replies         = entity.Replies.Select(r => r.ToDto()).ToList(),
        };
    }

    public static StaffRequisitionComment ToEntity(this CreateStaffRequisitionCommentDto dto, Guid tenantId, Guid authorId)
    {
        return new StaffRequisitionComment
        {
            TenantId        = tenantId,
            RequisitionId   = dto.RequisitionId,
            ParentCommentId = dto.ParentCommentId,
            Body            = dto.Body,
            AuthorId        = authorId,
            CreatedBy       = authorId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffRequisitionComment entity, UpdateStaffRequisitionCommentDto dto, Guid userId)
    {
        entity.Body      = dto.Body;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // STAFF REQUISITION HISTORY
    // ========================================================================

    #region StaffRequisitionHistory

    public static StaffRequisitionHistoryDto ToDto(this StaffRequisitionHistory entity)
    {
        return new StaffRequisitionHistoryDto
        {
            Id                = entity.Id,
            TenantId          = entity.TenantId,
            CreatedAt         = entity.CreatedAt,
            CreatedBy         = entity.CreatedBy ?? string.Empty,
            UpdatedAt         = entity.UpdatedAt,
            UpdatedBy         = entity.UpdatedBy,
            RequisitionId     = entity.RequisitionId,
            RequisitionNumber = entity.Requisition?.RequisitionNumber ?? string.Empty,
            FromStatus        = entity.FromStatus,
            ToStatus          = entity.ToStatus,
            ChangedById       = entity.ChangedById,
            ChangedByName     = entity.ChangedBy?.FullName ?? string.Empty,
            Comments          = entity.Comments,
            ActionDate        = entity.ActionDate,
        };
    }

    public static StaffRequisitionHistory ToEntity(
        Guid tenantId,
        Guid requisitionId,
        StaffRequisitionStatus fromStatus,
        StaffRequisitionStatus toStatus,
        Guid changedById,
        string? comments = null)
    {
        return new StaffRequisitionHistory
        {
            TenantId      = tenantId,
            RequisitionId = requisitionId,
            FromStatus    = fromStatus,
            ToStatus      = toStatus,
            ChangedById   = changedById,
            Comments      = comments,
            CreatedBy     = changedById.ToString(),
        };
    }

    #endregion
}
