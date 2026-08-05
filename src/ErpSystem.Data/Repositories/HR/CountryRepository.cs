using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class CountryRepository : GenericRepository<Country>, ICountryRepository
{
    public CountryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Country>> GetActiveCountriesAsync()
        => await _dbSet
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<Country?> GetByCodeAsync(string code)
        => await _dbSet
            .FirstOrDefaultAsync(c => c.Code == code && !c.IsDeleted);

    public async Task<Country?> GetByAlpha2CodeAsync(string alpha2Code)
        => await _dbSet
            .FirstOrDefaultAsync(c => c.Alpha2Code == alpha2Code && !c.IsDeleted);

    public async Task<bool> CodeExistsAsync(string code)
        => await _dbSet.AnyAsync(c => c.Code == code && !c.IsDeleted);

    public async Task<bool> Alpha2CodeExistsAsync(string alpha2Code)
        => await _dbSet.AnyAsync(c => c.Alpha2Code == alpha2Code && !c.IsDeleted);
}
