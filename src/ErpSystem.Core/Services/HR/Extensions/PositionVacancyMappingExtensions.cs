using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>Hand-written mapping between <see cref="PositionVacancy"/> and its DTOs.</summary>
public static class PositionVacancyMappingExtensions
{
    public static PositionVacancyDto ToDto(this PositionVacancy e, int currentActiveHeadcount = 0)
    {
        return new PositionVacancyDto
        {
            Id = e.Id,
            TenantId = e.TenantId,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? string.Empty,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy,

            PositionId = e.PositionId,
            PositionTitle = e.Position?.Title ?? string.Empty,
            PositionCode = e.Position?.Code,

            OrganizationUnitId = e.OrganizationUnitId,
            OrganizationUnitName = e.OrganizationUnit?.Name,

            VacatedByEmployeeId = e.VacatedByEmployeeId,
            VacatedByEmployeeName = e.VacatedByEmployee?.FullName,

            Reason = e.Reason,
            VacatedDate = e.VacatedDate,
            IsAnticipated = e.IsAnticipated,
            ExpectedVacancyDate = e.ExpectedVacancyDate,
            Status = e.Status,
            Classification = e.Classification,
            ExpectedHeadcount = e.ExpectedHeadcount,
            ActiveHeadcountAtDetection = e.ActiveHeadcountAtDetection,
            CurrentActiveHeadcount = currentActiveHeadcount,

            StaffRequisitionId = e.StaffRequisitionId,
            StaffRequisitionNumber = e.StaffRequisition?.RequisitionNumber,

            Notes = e.Notes,
            ClosedDate = e.ClosedDate,
            ClosedReason = e.ClosedReason,
            AgeInDays = ComputeAge(e),
        };
    }

    public static PositionVacancySummaryDto ToSummaryDto(this PositionVacancy e)
    {
        return new PositionVacancySummaryDto
        {
            Id = e.Id,
            PositionId = e.PositionId,
            PositionTitle = e.Position?.Title ?? string.Empty,
            OrganizationUnitName = e.OrganizationUnit?.Name,
            VacatedByEmployeeName = e.VacatedByEmployee?.FullName,
            Reason = e.Reason,
            VacatedDate = e.VacatedDate,
            IsAnticipated = e.IsAnticipated,
            ExpectedVacancyDate = e.ExpectedVacancyDate,
            Status = e.Status,
            Classification = e.Classification,
            StaffRequisitionId = e.StaffRequisitionId,
            StaffRequisitionNumber = e.StaffRequisition?.RequisitionNumber,
            AgeInDays = ComputeAge(e),
        };
    }

    public static IEnumerable<PositionVacancySummaryDto> ToSummaryDtoList(this IEnumerable<PositionVacancy> entities)
        => entities.Select(e => e.ToSummaryDto());

    private static int ComputeAge(PositionVacancy e)
    {
        var end = e.ClosedDate ?? DateTime.UtcNow;
        var days = (end.Date - e.VacatedDate.Date).Days;
        return days < 0 ? 0 : days;
    }
}
