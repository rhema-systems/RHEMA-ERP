using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

public class CountryService : ICountryService
{
    private readonly ICountryRepository _repo;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CountryService(ICountryRepository repo, ICurrentUserProvider currentUserProvider)
    {
        _repo = repo;
        _currentUserProvider = currentUserProvider;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant
    // query-filter and TenantId auto-stamp are inert. Following the RHEMA convention,
    // this service scopes reads/writes to the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<CountryDto?> GetByIdAsync(Guid id)
    {
        var country = await _repo.GetByIdAsync(id);
        return country == null ? null : MapToDto(country);
    }

    public async Task<IEnumerable<CountryDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var countries = await _repo.GetAllAsync();
        return countries.Where(c => c.TenantId == tenantId).OrderBy(c => c.Name).Select(MapToDto);
    }

    public Task<IEnumerable<CountryDto>> GetActiveCountriesAsync()
        => GetActiveCountriesAsync(GetTenantId());

    // Anonymous callers (public career portal) have no tenant claim, so the tenant is passed in
    // from the X-Tenant-Id header instead. Both overloads share one filter so they cannot drift.
    public async Task<IEnumerable<CountryDto>> GetActiveCountriesAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id is required.", nameof(tenantId));

        var countries = await _repo.GetActiveCountriesAsync();
        return countries.Where(c => c.TenantId == tenantId).OrderBy(c => c.Name).Select(MapToDto);
    }

    public async Task<CountryDto?> GetByCodeAsync(string code)
    {
        var country = await _repo.GetByCodeAsync(code);
        return country == null ? null : MapToDto(country);
    }

    public async Task<CountryDto?> GetByAlpha2CodeAsync(string alpha2Code)
    {
        var country = await _repo.GetByAlpha2CodeAsync(alpha2Code);
        return country == null ? null : MapToDto(country);
    }

    public async Task<CountryDto> CreateAsync(CreateCountryDto dto)
    {
        if (await _repo.CodeExistsAsync(dto.Code))
            throw new InvalidOperationException($"Country code '{dto.Code}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.Alpha2Code) && await _repo.Alpha2CodeExistsAsync(dto.Alpha2Code))
            throw new InvalidOperationException($"Alpha-2 code '{dto.Alpha2Code}' already exists.");

        var entity = new Country
        {
            TenantId = GetTenantId(),
            Name = dto.Name,
            Code = dto.Code,
            Alpha2Code = dto.Alpha2Code,
            IsActive = dto.IsActive,
        };

        await _repo.AddAsync(entity);
        await _repo.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<CountryDto> UpdateAsync(Guid id, UpdateCountryDto dto)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new InvalidOperationException($"Country with ID {id} not found.");

        entity.Name = dto.Name;
        entity.Code = dto.Code;
        entity.Alpha2Code = dto.Alpha2Code;
        entity.IsActive = dto.IsActive;

        await _repo.UpdateAsync(entity);
        await _repo.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            return false;

        await _repo.DeleteAsync(id);
        await _repo.SaveChangesAsync();
        return true;
    }

    private static CountryDto MapToDto(Country c) => new()
    {
        Id         = c.Id,
        Name       = c.Name,
        Code       = c.Code,
        Alpha2Code = c.Alpha2Code,
        IsActive   = c.IsActive,
    };
}
