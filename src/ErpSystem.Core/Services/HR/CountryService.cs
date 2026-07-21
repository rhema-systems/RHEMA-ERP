using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

public class CountryService : ICountryService
{
    private readonly ICountryRepository _repo;

    public CountryService(ICountryRepository repo)
    {
        _repo = repo;
    }

    public async Task<CountryDto?> GetByIdAsync(Guid id)
    {
        var country = await _repo.GetByIdAsync(id);
        return country == null ? null : MapToDto(country);
    }

    public async Task<IEnumerable<CountryDto>> GetAllAsync()
    {
        var countries = await _repo.GetAllAsync();
        return countries.Select(MapToDto);
    }

    public async Task<IEnumerable<CountryDto>> GetActiveCountriesAsync()
    {
        var countries = await _repo.GetActiveCountriesAsync();
        return countries.Select(MapToDto);
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

    private static CountryDto MapToDto(Entities.HR.Country c) => new()
    {
        Id         = c.Id,
        Name       = c.Name,
        Code       = c.Code,
        Alpha2Code = c.Alpha2Code,
        IsActive   = c.IsActive,
    };
}
