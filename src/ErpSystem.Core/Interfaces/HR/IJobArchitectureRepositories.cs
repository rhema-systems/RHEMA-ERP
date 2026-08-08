using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IJobFamilyRepository : IGenericRepository<JobFamily>
{
    Task<IEnumerable<JobFamily>> GetAllWithSubFamiliesAsync();
    Task<IEnumerable<JobFamily>> GetActiveAsync();
}

public interface IJobSubFamilyRepository : IGenericRepository<JobSubFamily>
{
    Task<IEnumerable<JobSubFamily>> GetByFamilyIdAsync(Guid jobFamilyId);
    Task<IEnumerable<JobSubFamily>> GetActiveAsync();
}

public interface IJobLevelRepository : IGenericRepository<CareerLevel>
{
    Task<IEnumerable<CareerLevel>> GetAllOrderedAsync();
    Task<IEnumerable<CareerLevel>> GetActiveAsync();
}
