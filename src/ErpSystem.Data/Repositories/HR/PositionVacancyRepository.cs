using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class PositionVacancyRepository : GenericRepository<PositionVacancy>, IPositionVacancyRepository
{
    private static readonly PositionVacancyStatus[] OpenStatuses =
    {
        PositionVacancyStatus.Anticipated,
        PositionVacancyStatus.Open,
        PositionVacancyStatus.UnderReview,
        PositionVacancyStatus.RequisitionRaised,
    };

    public PositionVacancyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<PositionVacancy?> GetWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.OrganizationUnit)
            .Include(v => v.VacatedByEmployee)
            .Include(v => v.StaffRequisition)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
    }

    public async Task<IEnumerable<PositionVacancy>> GetVacanciesAsync(
        PositionVacancyStatus? status = null,
        Guid? organizationUnitId = null,
        VacancyReason? reason = null,
        VacancyClassification? classification = null,
        bool includeClosed = false)
    {
        var query = _dbSet
            .Include(v => v.Position)
            .Include(v => v.OrganizationUnit)
            .Include(v => v.VacatedByEmployee)
            .Include(v => v.StaffRequisition)
            .Where(v => !v.IsDeleted);

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);
        else if (!includeClosed)
            query = query.Where(v => OpenStatuses.Contains(v.Status));

        if (organizationUnitId.HasValue)
            query = query.Where(v => v.OrganizationUnitId == organizationUnitId.Value);

        if (reason.HasValue)
            query = query.Where(v => v.Reason == reason.Value);

        if (classification.HasValue)
            query = query.Where(v => v.Classification == classification.Value);

        return await query
            .OrderByDescending(v => v.VacatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PositionEstablishmentDto>> GetEstablishmentOverviewAsync(
        Guid? organizationUnitId = null, bool onlyVacant = false)
    {
        var positionsQuery = _context.Set<EmployeePosition>().AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted);

        if (organizationUnitId.HasValue)
            positionsQuery = positionsQuery.Where(p => p.OrganizationUnitId == organizationUnitId.Value);

        var positions = await positionsQuery
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Code,
                p.OrganizationUnitId,
                OrgUnitName = p.OrganizationUnit.Name,
                p.ExpectedHeadcount,
            })
            .ToListAsync();

        var positionIds = positions.Select(p => p.Id).ToList();

        var filledCounts = await _context.Set<Employee>().AsNoTracking()
            .Where(e => positionIds.Contains(e.PositionId) && !e.IsDeleted
                     && e.StaffStatus != StaffStatus.Terminated
                     && e.StaffStatus != StaffStatus.Retired
                     && e.StaffStatus != StaffStatus.Inactive)
            .GroupBy(e => e.PositionId)
            .Select(g => new { PositionId = g.Key, Count = g.Count() })
            .ToListAsync();
        var filledMap = filledCounts.ToDictionary(x => x.PositionId, x => x.Count);

        var openVacancies = await _dbSet.AsNoTracking()
            .Where(v => positionIds.Contains(v.PositionId) && !v.IsDeleted && OpenStatuses.Contains(v.Status))
            .Select(v => new { v.Id, v.PositionId, v.Status, v.VacatedDate })
            .ToListAsync();
        var vacancyMap = openVacancies
            .GroupBy(v => v.PositionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.VacatedDate).First());

        var result = positions.Select(p =>
        {
            filledMap.TryGetValue(p.Id, out var filled);
            vacancyMap.TryGetValue(p.Id, out var vac);
            return new PositionEstablishmentDto
            {
                PositionId = p.Id,
                PositionTitle = p.Title,
                PositionCode = p.Code,
                OrganizationUnitId = p.OrganizationUnitId,
                OrganizationUnitName = p.OrgUnitName,
                ExpectedHeadcount = p.ExpectedHeadcount <= 0 ? 1 : p.ExpectedHeadcount,
                FilledCount = filled,
                OpenVacancyId = vac?.Id,
                OpenVacancyStatus = vac?.Status,
                OldestOpenVacancyDate = vac?.VacatedDate,
            };
        });

        if (onlyVacant)
            result = result.Where(r => r.VacantCount > 0 || r.OpenVacancyId != null);

        return result
            .OrderBy(r => r.OrganizationUnitName)
            .ThenBy(r => r.PositionTitle)
            .ToList();
    }

    public async Task<int> CountActiveOnPositionAsync(Guid tenantId, Guid positionId)
    {
        return await _context.Set<Employee>().AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && e.PositionId == positionId && !e.IsDeleted
                          && e.StaffStatus != StaffStatus.Terminated
                          && e.StaffStatus != StaffStatus.Retired
                          && e.StaffStatus != StaffStatus.Inactive);
    }

    public async Task<PositionVacancyStatsDto> GetStatsAsync()
    {
        var open = await _dbSet.AsNoTracking()
            .Where(v => !v.IsDeleted && OpenStatuses.Contains(v.Status))
            .Select(v => new { v.Status, v.Classification, v.PositionId })
            .ToListAsync();

        var totalPositions = await _context.Set<EmployeePosition>().AsNoTracking()
            .CountAsync(p => p.IsActive && !p.IsDeleted);

        return new PositionVacancyStatsDto
        {
            TotalOpen = open.Count,
            Anticipated = open.Count(v => v.Status == PositionVacancyStatus.Anticipated),
            UnderReview = open.Count(v => v.Status == PositionVacancyStatus.UnderReview),
            RequisitionRaised = open.Count(v => v.Status == PositionVacancyStatus.RequisitionRaised),
            WithinEstablishment = open.Count(v => v.Classification == VacancyClassification.WithinEstablishment),
            NoShortfallOrOver = open.Count(v => v.Classification != VacancyClassification.WithinEstablishment),
            TotalPositions = totalPositions,
            PositionsWithVacancy = open.Select(v => v.PositionId).Distinct().Count(),
        };
    }
}
