using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
// ⚠ This file sits in ErpSystem.Application.Extensions, NOT under Services.HR, so LeaveYear
// needs an explicit using — namespace lookup does not walk into an unrelated tree.
using ErpSystem.Core.Services.HR;

namespace ErpSystem.Application.Extensions
{
    /// <summary>
    /// Manual mapping extensions for Leave entities and DTOs
    /// </summary>
    public static class LeaveMappingExtensions
    {
        // ===== LEAVE TYPE =====

        public static LeaveTypeDto ToDto(this LeaveType entity) => new LeaveTypeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            IsPaid = entity.IsPaid,
            DefaultDaysPerYear = entity.DefaultDaysPerYear,
            MaxDaysPerYear = entity.MaxDaysPerYear,
            MinDaysNotice = entity.MinDaysNotice,
            RequiresApproval = entity.RequiresApproval,
            CalendarColor = entity.CalendarColor,
            HasSubTypes = entity.HasSubTypes,
            AllowCarryOver = entity.AllowCarryOver,
            MaxCarryOverDays = entity.MaxCarryOverDays,
            CountWeekendsAsLeave = entity.CountWeekendsAsLeave,
            CountHolidaysAsLeave = entity.CountHolidaysAsLeave,
            AllowCashConversion = entity.AllowCashConversion,
            RequiresReliever = entity.RequiresReliever,
            MinServiceMonthsToAccess = entity.MinServiceMonthsToAccess,
            CarryOverExpiryMonths = entity.CarryOverExpiryMonths,
            ForfeitUnusedAfterMonths = entity.ForfeitUnusedAfterMonths,
            YearEndBasis = entity.YearEndBasis,
            ProRateFirstYearEntitlement = entity.ProRateFirstYearEntitlement,
            Category = entity.Category,
            AllowOffsetAgainstAnnual = entity.AllowOffsetAgainstAnnual,
            EncashmentRateBasis = entity.EncashmentRateBasis,
            EncashmentRatePerDay = entity.EncashmentRatePerDay,
            EncashmentWorkingDaysPerMonth = entity.EncashmentWorkingDaysPerMonth,
            RequiresMedicalCertificate = entity.RequiresMedicalCertificate,
            SelfCertificationDays = entity.SelfCertificationDays,
            MedicalBoardThresholdDays = entity.MedicalBoardThresholdDays,
            IsActive = entity.IsActive
        };

        public static LeaveType ToEntity(this CreateLeaveTypeDto dto) => new LeaveType
        {
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            IsPaid = dto.IsPaid,
            DefaultDaysPerYear = dto.DefaultDaysPerYear,
            MaxDaysPerYear = dto.MaxDaysPerYear,
            MinDaysNotice = dto.MinDaysNotice,
            RequiresApproval = dto.RequiresApproval,
            CalendarColor = dto.CalendarColor,
            // Derived from the sub-types (lane N2); a new type has none yet.
            HasSubTypes = false,
            AllowCarryOver = dto.AllowCarryOver,
            MaxCarryOverDays = dto.MaxCarryOverDays,
            CountWeekendsAsLeave = dto.CountWeekendsAsLeave,
            CountHolidaysAsLeave = dto.CountHolidaysAsLeave,
            AllowCashConversion = dto.AllowCashConversion,
            RequiresReliever = dto.RequiresReliever,
            MinServiceMonthsToAccess = dto.MinServiceMonthsToAccess,
            CarryOverExpiryMonths = dto.CarryOverExpiryMonths,
            ForfeitUnusedAfterMonths = dto.ForfeitUnusedAfterMonths,
            YearEndBasis = dto.YearEndBasis,
            ProRateFirstYearEntitlement = dto.ProRateFirstYearEntitlement,
            Category = dto.Category ?? LeaveTypeCategory.Other,
            AllowOffsetAgainstAnnual = dto.AllowOffsetAgainstAnnual ?? false,
            EncashmentRateBasis = dto.EncashmentRateBasis,
            EncashmentRatePerDay = dto.EncashmentRatePerDay,
            EncashmentWorkingDaysPerMonth = dto.EncashmentWorkingDaysPerMonth,
            RequiresMedicalCertificate = dto.RequiresMedicalCertificate,
            SelfCertificationDays = dto.SelfCertificationDays,
            MedicalBoardThresholdDays = dto.MedicalBoardThresholdDays
        };

        public static List<LeaveTypeDto> ToDtoList(this IEnumerable<LeaveType> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE SUB TYPE =====

        public static LeaveSubTypeDto ToDto(this LeaveSubType entity) => new LeaveSubTypeDto
        {
            Id = entity.Id,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
            SubTypeName = entity.SubTypeName,
            Description = entity.Description,
            MaxDaysAllowed = entity.MaxDaysAllowed,
            IsActive = entity.IsActive
        };

        public static LeaveSubType ToEntity(this CreateLeaveSubTypeDto dto) => new LeaveSubType
        {
            LeaveTypeId = dto.LeaveTypeId,
            SubTypeName = dto.SubTypeName,
            Description = dto.Description,
            MaxDaysAllowed = dto.MaxDaysAllowed,
            // Round 5, lane N (guide L-44): it was not mapped, so a sub-type created switched off
            // came out active and was offered to every request.
            IsActive = dto.IsActive
        };

        public static List<LeaveSubTypeDto> ToDtoList(this IEnumerable<LeaveSubType> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE CATEGORY ALLOCATION =====

        public static LeaveCategoryAllocationDto ToDto(this LeaveCategoryAllocation entity) => new LeaveCategoryAllocationDto
        {
            Id = entity.Id,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
            LeaveSubTypeId = entity.LeaveSubTypeId,
            LeaveSubTypeName = entity.LeaveSubType?.SubTypeName,
            StaffLevelId = entity.StaffLevelId,
            StaffLevelName = entity.StaffLevel?.Name ?? string.Empty,
            AllocationDays = entity.AllocationDays,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo
        };

        public static LeaveCategoryAllocation ToEntity(this CreateLeaveCategoryAllocationDto dto) => new LeaveCategoryAllocation
        {
            LeaveTypeId = dto.LeaveTypeId,
            // ⚠ Always the whole type (round 5, lane N2): a balance is kept per type, so an
            // allocation to one sub-type matched almost nothing. The field stays on the DTO for
            // older callers and is ignored.
            LeaveSubTypeId = null,
            StaffLevelId = dto.StaffLevelId,
            AllocationDays = dto.AllocationDays,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo
        };

        public static List<LeaveCategoryAllocationDto> ToDtoList(this IEnumerable<LeaveCategoryAllocation> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE TYPE ELIGIBILITY =====

        public static LeaveTypeEligibilityDto ToDto(this LeaveTypeEligibility entity) => new LeaveTypeEligibilityDto
        {
            Id = entity.Id,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
            EligibilityType = entity.EligibilityType,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionName = entity.Position?.Title,
            Gender = entity.Gender
        };

        public static LeaveTypeEligibility ToEntity(this CreateLeaveTypeEligibilityDto dto) => new LeaveTypeEligibility
        {
            LeaveTypeId = dto.LeaveTypeId,
            EligibilityType = dto.EligibilityType,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            Gender = dto.Gender
        };

        public static List<LeaveTypeEligibilityDto> ToDtoList(this IEnumerable<LeaveTypeEligibility> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE ACCRUAL POLICY =====

        public static LeaveAccrualPolicyDto ToDto(this LeaveAccrualPolicy entity) => new LeaveAccrualPolicyDto
        {
            Id = entity.Id,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
            Frequency = entity.Frequency,
            Mode = entity.Mode,
            AccrualRate = entity.AccrualRate,
            MinServiceMonths = entity.MinServiceMonths,
            ProRateOnJoin = entity.ProRateOnJoin,
            ProRateOnExit = entity.ProRateOnExit,
            IsActive = entity.IsActive
        };

        public static LeaveAccrualPolicy ToEntity(this CreateLeaveAccrualPolicyDto dto) => new LeaveAccrualPolicy
        {
            LeaveTypeId = dto.LeaveTypeId,
            Frequency = dto.Frequency,
            Mode = dto.Mode,
            AccrualRate = dto.AccrualRate,
            MinServiceMonths = dto.MinServiceMonths,
            ProRateOnJoin = dto.ProRateOnJoin,
            ProRateOnExit = dto.ProRateOnExit,
            IsActive = dto.IsActive ?? true
        };

        public static List<LeaveAccrualPolicyDto> ToDtoList(this IEnumerable<LeaveAccrualPolicy> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE PLAN =====

        public static LeavePlanDto ToDto(this LeavePlan entity) => new LeavePlanDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionName = entity.Position?.Title,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
            LeaveSubTypeId = entity.LeaveSubTypeId,
            LeaveSubTypeName = entity.LeaveSubType?.SubTypeName,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            RelieverId = entity.RelieverId,
            RelieverName = entity.RelieverEmployee?.FullName,
            SecondRelieverId = entity.SecondRelieverId,
            SecondRelieverName = entity.SecondRelieverEmployee?.FullName,
            Notes = entity.Notes,
            PlannedBy = entity.PlannedBy,
            PlannedByName = entity.PlannedByEmployee?.FullName ?? string.Empty,
            Year = entity.Year,
            Status = entity.Status,
            SuggestedStartDate = entity.SuggestedStartDate,
            SuggestedEndDate = entity.SuggestedEndDate,
            ManagerSuggestionNotes = entity.ManagerSuggestionNotes,
            WorkflowInstanceId = entity.WorkflowInstanceId,
            ApprovedById = entity.ApprovedById,
            ApprovedDate = entity.ApprovedDate,
            RejectionReason = entity.RejectionReason,
            CancellationDate = entity.CancellationDate,
            CancellationReason = entity.CancellationReason
        };

        public static LeavePlan ToEntity(this CreateLeavePlanDto dto) => new LeavePlan
        {
            EmployeeId = dto.EmployeeId,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            LeaveTypeId = dto.LeaveTypeId,
            LeaveSubTypeId = dto.LeaveSubTypeId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            RelieverId = dto.RelieverId,
            SecondRelieverId = dto.SecondRelieverId,
            Notes = dto.Notes,
            // PlannedBy is stamped by the service from the token (finish-plan lane 4).
            // ⚠ Year is NOT set here. It follows the dates (L-17), but WHICH year a date falls in
            // depends on the tenant's leave year, and a static mapper cannot read settings. The
            // service sets it on both the create and the update path — one owner rather than two
            // that can disagree (entitlement plan C1).
            Year = 0
        };

        public static List<LeavePlanDto> ToDtoList(this IEnumerable<LeavePlan> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE REQUEST =====

        public static LeaveRequest ToEntity(this CreateLeaveRequestDto dto) => new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            LeaveTypeId = dto.LeaveTypeId,
            LeaveSubTypeId = dto.LeaveSubTypeId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason,
            RelieverEmployeeId = dto.RelieverEmployeeId,
            SecondRelieverEmployeeId = dto.SecondRelieverEmployeeId,
            RelieverNotes = dto.RelieverNotes,
            HandoverNotes = dto.HandoverNotes,
            LeavePlanId = dto.LeavePlanId,
            ChargeExcessToAnnual = dto.ChargeExcessToAnnual
        };

        public static LeaveRequestDto ToDto(this LeaveRequest entity) => new LeaveRequestDto
        {
            Id = entity.Id,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
            // Null when the read did not load the type: unknown, not Other.
            LeaveTypeCategory = entity.LeaveType?.Category,
            IsPaidLeave = entity.LeaveType?.IsPaid ?? false,
            LeaveSubTypeId = entity.LeaveSubTypeId,
            LeaveSubTypeName = entity.LeaveSubType?.SubTypeName,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            TotalDays = entity.TotalDays,
            RequestDate = entity.RequestDate,
            Reason = entity.Reason,
            HandoverNotes = entity.HandoverNotes,
            Status = entity.Status,
            RelieverEmployeeId = entity.RelieverEmployeeId,
            RelieverEmployeeName = entity.RelieverEmployee?.FullName,
            SecondRelieverEmployeeId = entity.SecondRelieverEmployeeId,
            SecondRelieverEmployeeName = entity.SecondRelieverEmployee?.FullName,
            RelieverNotes = entity.RelieverNotes,
            LeavePlanId = entity.LeavePlanId,
            ChargeExcessToAnnual = entity.ChargeExcessToAnnual,
            SplitFromRequestId = entity.SplitFromRequestId,
            ApprovedById = entity.ApprovedById,
            ApprovedDate = entity.ApprovedDate,
            RejectionReason = entity.RejectionReason,
            SuggestedStartDate = entity.SuggestedStartDate,
            SuggestedEndDate = entity.SuggestedEndDate,
            ManagerSuggestionNotes = entity.ManagerSuggestionNotes,
            OriginalStartDate = entity.OriginalStartDate,
            OriginalEndDate = entity.OriginalEndDate,
            RescheduledDate = entity.RescheduledDate,
            RescheduledById = entity.RescheduledById,
            RescheduleReason = entity.RescheduleReason,
            RescheduleCount = entity.RescheduleCount,
            ObservanceConfirmedDate = entity.ObservanceConfirmedDate,
            ObservanceConfirmedById = entity.ObservanceConfirmedById,
            MedicalBoardId = entity.MedicalBoardId,
            RecallEffectiveDate = entity.RecallEffectiveDate,
            PreRecallEndDate = entity.PreRecallEndDate,
            RecalledDate = entity.RecalledDate,
            RecalledById = entity.RecalledById,
            RecallReason = entity.RecallReason,
            DaysRestored = entity.DaysRestored,
            // RescheduledByName / ObservanceConfirmedByName / RecalledByName are filled by the reads
            // that need them: the actor columns are bare Guids (no navigation — see the entity's
            // note on shadow FKs).
            ClosureDate = entity.ClosureDate,
            ClosureNotes = entity.ClosureNotes,
            CancellationDate = entity.CancellationDate,
            CancellationReason = entity.CancellationReason,
            ResumptionDate = entity.ResumptionDate,
            ResumptionReportedDate = entity.ResumptionReportedDate,
            ResumptionReportedById = entity.ResumptionReportedById,
            ClosureConfirmedById = entity.ClosureConfirmedById,
            OverstayDays = entity.OverstayDays,
            CreatedAt = entity.CreatedAt
        };

        public static List<LeaveRequestDto> ToDtoList(this IEnumerable<LeaveRequest> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE BALANCE =====

        public static LeaveBalanceDto ToDto(this LeaveBalance entity) => new LeaveBalanceDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            OrganizationUnitName = entity.Employee?.OrganizationUnit?.Name,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveType?.Name ?? string.Empty,
            LeaveTypeCategory = entity.LeaveType?.Category,
            LeaveSubTypeId = entity.LeaveSubTypeId,
            LeaveSubTypeName = entity.LeaveSubType?.SubTypeName,
            Year = entity.Year,
            EntitledDays = entity.EntitledDays,
            // Default accrued-to-date to the full entitlement; service getters override this with
            // the engine-computed accrued figure for accrual-governed leave types.
            AccruedToDateDays = entity.EntitledDays,
            UsedDays = entity.UsedDays,
            PendingDays = entity.PendingDays,
            CarriedOverDays = entity.CarriedOverDays,
            AdjustmentDays = entity.AdjustmentDays,
            EncashedDays = entity.EncashedDays,
            AvailableDays = entity.AvailableDays
        };

        public static List<LeaveBalanceDto> ToDtoList(this IEnumerable<LeaveBalance> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE ADJUSTMENT =====

        public static LeaveAdjustmentDto ToDto(this LeaveAdjustment entity) => new LeaveAdjustmentDto
        {
            Id               = entity.Id,
            LeaveBalanceId   = entity.LeaveBalanceId,
            EmployeeId       = entity.EmployeeId,
            EmployeeName     = entity.LeaveBalance?.Employee?.FullName ?? string.Empty,
            LeaveTypeId      = entity.LeaveTypeId,
            LeaveTypeName    = entity.LeaveBalance?.LeaveType?.Name ?? string.Empty,
            LeaveSubTypeId   = entity.LeaveSubTypeId,
            LeaveSubTypeName = entity.LeaveBalance?.LeaveSubType?.SubTypeName,
            Year             = entity.Year,
            Days             = entity.Days,
            ReasonCodeId     = entity.ReasonCodeId,
            ReasonCodeName   = entity.ReasonCode?.Name,
            Reason           = entity.Reason,
            AdjustmentDate   = entity.AdjustmentDate,
            PerformedBy      = entity.PerformedBy,
            PerformedByName  = entity.PerformedByEmployee?.FullName ?? string.Empty
        };

        public static List<LeaveAdjustmentDto> ToDtoList(this IEnumerable<LeaveAdjustment> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE REQUEST ATTACHMENT =====

        public static LeaveRequestAttachmentDto ToDto(this LeaveRequestAttachment entity) => new LeaveRequestAttachmentDto
        {
            Id = entity.Id,
            LeaveRequestId = entity.LeaveRequestId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            ContentType = entity.ContentType,
            FileSizeBytes = entity.FileSizeBytes,
            UploadedDate = entity.UploadedDate,
            UploadedBy = entity.UploadedBy,
            UploadedByName = entity.UploadedByEmployee?.FullName ?? string.Empty,
            // ⚠ Without this the DTO carried the field and nothing filled it, so every attachment
            // read back as "Other" however it was uploaded — the gate refusing on the truth while
            // the screen showed something else. Caught by slice 7, not by reading the code: the
            // column, the entity, the DTO and the enum were all correct in isolation.
            EvidenceKind = entity.EvidenceKind
        };

        public static List<LeaveRequestAttachmentDto> ToDtoList(this IEnumerable<LeaveRequestAttachment> entities)
            => entities.Select(e => e.ToDto()).ToList();

        // ===== LEAVE ENCASHMENT =====

        public static LeaveEncashmentDto ToDto(this LeaveEncashment entity) => new LeaveEncashmentDto
        {
            Id = entity.Id,
            LeaveRequestId = entity.LeaveRequestId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            LeaveTypeId = entity.LeaveTypeId,
            LeaveTypeName = entity.LeaveRequest?.LeaveType?.Name ?? string.Empty,
            Year = entity.Year,
            DaysEncashed = entity.DaysEncashed,
            AmountPaid = entity.AmountPaid,
            RateBasis = entity.RateBasis,
            Status = entity.Status,
            ProcessedDate = entity.ProcessedDate,
            ProcessedByEmployeeId = entity.ProcessedByEmployeeId,
            ProcessedByName = entity.ProcessedByEmployee?.FullName,
            PaymentReference = entity.PaymentReference,
            Notes = entity.Notes,
            WorkflowInstanceId = entity.WorkflowInstanceId,
            ApprovedById = entity.ApprovedById,
            ApprovedDate = entity.ApprovedDate,
            RejectionReason = entity.RejectionReason
        };

        public static List<LeaveEncashmentDto> ToDtoList(this IEnumerable<LeaveEncashment> entities)
            => entities.Select(e => e.ToDto()).ToList();

    }
}
