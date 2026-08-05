using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class JobFamilyRepository : GenericRepository<JobFamily>, IJobFamilyRepository
{
    public JobFamilyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobFamily>> GetAllWithSubFamiliesAsync()
        => await _dbSet.Include(f => f.SubFamilies).OrderBy(f => f.Name).ToListAsync();

    public async Task<IEnumerable<JobFamily>> GetActiveAsync()
        => await _dbSet.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync();
}

public class JobSubFamilyRepository : GenericRepository<JobSubFamily>, IJobSubFamilyRepository
{
    public JobSubFamilyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobSubFamily>> GetByFamilyIdAsync(Guid jobFamilyId)
        => await _dbSet.Include(s => s.JobFamily).Where(s => s.JobFamilyId == jobFamilyId).OrderBy(s => s.Name).ToListAsync();

    public async Task<IEnumerable<JobSubFamily>> GetActiveAsync()
        => await _dbSet.Include(s => s.JobFamily).Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
}

public class JobLevelRepository : GenericRepository<CareerLevel>, IJobLevelRepository
{
    public JobLevelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CareerLevel>> GetAllOrderedAsync()
        => await _dbSet.Include(l => l.SalaryGrade).OrderBy(l => l.Rank).ToListAsync();

    public async Task<IEnumerable<CareerLevel>> GetActiveAsync()
        => await _dbSet.Include(l => l.SalaryGrade).Where(l => l.IsActive).OrderBy(l => l.Rank).ToListAsync();
}
