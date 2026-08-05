using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Public holiday queries and persistence. Calendar-scoped CRUD is exposed via <see cref="HolidayCalendarService"/>.
/// </summary>
public class PublicHolidayService : IPublicHolidayService
{
    private readonly IPublicHolidayRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PublicHolidayService> _logger;

    public PublicHolidayService(
        IPublicHolidayRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<PublicHolidayService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    // A holiday owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PublicHoliday> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Public holiday '{id}' not found.");
        return entity;
    }

    public async Task<PublicHolidayDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<PublicHolidayDto>> GetByCalendarIdAsync(Guid calendarId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetByCalendarIdAsync(calendarId);
        return entities.Where(h => h.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<PublicHolidayDto>> GetByCalendarAndYearAsync(Guid calendarId, int year, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetByCalendarAndYearAsync(calendarId, year);
        return entities.Where(h => h.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<PublicHolidayDto>> GetByYearAsync(int year, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetByYearAsync(year);
        return entities.Where(h => h.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<PublicHolidayDto>> GetInRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetHolidaysInRangeAsync(from, to);
        return entities.Where(h => h.TenantId == tenantId).ToDtoList();
    }

    public async Task<IEnumerable<PublicHolidayDto>> GetRecurringHolidaysAsync(Guid calendarId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = await _repository.GetRecurringHolidaysAsync(calendarId);
        return entities.Where(h => h.TenantId == tenantId).ToDtoList();
    }

    public async Task<PublicHolidayDto> CreateAsync(CreatePublicHolidayDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        if (dto.DateTo < dto.DateFrom)
            throw new InvalidOperationException("DateTo must be greater than or equal to DateFrom.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Public holiday created: {Name}", entity.HolidayName);
        return entity.ToDto();
    }

    public async Task<PublicHolidayDto> UpdateAsync(UpdatePublicHolidayDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        if (dto.DateTo < dto.DateFrom)
            throw new InvalidOperationException("DateTo must be greater than or equal to DateFrom.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
