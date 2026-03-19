using CorePagedResult = ErpSystem.Core.DTOs.Common.PagedResult<ErpSystem.Core.Entities.Projects.Project>;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Projects;

public class ProjectRepository : GenericRepository<Project>, IProjectRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public override async Task<Project?> GetByIdAsync(Guid id)
        => await _dbSet
            .Include(x => x.ProjectType)
            .Include(x => x.ProjectPriority)
            .Include(x => x.Template)
            .Include(x => x.Portfolio)
            .Include(x => x.Program)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

    public async Task<Project?> GetDetailByIdAsync(Guid id)
        => await _dbSet
            .Include(x => x.ProjectType)
            .Include(x => x.ProjectPriority)
            .Include(x => x.Template)
            .Include(x => x.Portfolio)
            .Include(x => x.Program)
            .Include(x => x.Members)
            .Include(x => x.Milestones)
            .Include(x => x.ResourceAllocations)
            .Include(x => x.Risks)
            .Include(x => x.Issues)
            .Include(x => x.ChangeRequests)
            .Include(x => x.BillingSchedules)
            .Include(x => x.InvoiceRequests)
            .Include(x => x.Documents)
            .Include(x => x.Comments)
            .Include(x => x.InitiationVersions)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

    public async Task<Project?> GetByProjectCodeAsync(string projectCode)
        => await _dbSet
            .Include(x => x.ProjectType)
            .Include(x => x.ProjectPriority)
            .Include(x => x.Portfolio)
            .Include(x => x.Program)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

    public async Task<CorePagedResult> GetPagedAsync(int page, int pageSize, string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null)
    {
        var query = _dbSet
            .Include(x => x.ProjectType)
            .Include(x => x.ProjectPriority)
            .Include(x => x.Portfolio)
            .Include(x => x.Program)
            .Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.ProjectCode.Contains(search) || x.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (projectTypeId.HasValue)
        {
            query = query.Where(x => x.ProjectTypeId == projectTypeId.Value);
        }

        if (portfolioId.HasValue)
        {
            query = query.Where(x => x.PortfolioId == portfolioId.Value);
        }

        if (programId.HasValue)
        {
            query = query.Where(x => x.ProgramId == programId.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CorePagedResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<Project>> LookupAsync(string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null, int take = 20)
    {
        var query = _dbSet
            .Include(x => x.ProjectType)
            .Include(x => x.ProjectPriority)
            .Include(x => x.Portfolio)
            .Include(x => x.Program)
            .Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.ProjectCode.Contains(search) || x.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (projectTypeId.HasValue)
        {
            query = query.Where(x => x.ProjectTypeId == projectTypeId.Value);
        }

        if (portfolioId.HasValue)
        {
            query = query.Where(x => x.PortfolioId == portfolioId.Value);
        }

        if (programId.HasValue)
        {
            query = query.Where(x => x.ProgramId == programId.Value);
        }

        return await query.OrderBy(x => x.ProjectCode).Take(take).ToListAsync();
    }

    public async Task<Project> CreateAsync(Project project)
    {
        await AddAsync(project);
        return project;
    }

    public new async Task<Project> UpdateAsync(Project project)
    {
        await base.UpdateAsync(project);
        return project;
    }

    public override async Task DeleteAsync(Guid id) => await base.DeleteAsync(id);

    public async Task<string> GenerateProjectCodeAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _dbSet.CountAsync(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted && x.CreatedAt.Year == year);
        return $"PRJ-{year}-{(count + 1):D4}";
    }
}

public class ProjectTypeRepository : GenericRepository<ProjectType>, IProjectTypeRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectTypeRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public async Task<ProjectType?> GetByCodeAsync(string code)
        => await _dbSet.FirstOrDefaultAsync(x => x.TenantId == _currentUserProvider.TenantId && x.Code == code && !x.IsDeleted);

    public async Task<IEnumerable<ProjectType>> GetActiveAsync()
        => await _dbSet.Where(x => x.TenantId == _currentUserProvider.TenantId && x.IsActive && !x.IsDeleted).OrderBy(x => x.Name).ToListAsync();

    public override async Task<IEnumerable<ProjectType>> GetAllAsync()
        => await _dbSet.Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted).OrderBy(x => x.Name).ToListAsync();

    public async Task<ProjectType> CreateAsync(ProjectType entity)
    {
        await AddAsync(entity);
        return entity;
    }

    public new async Task<ProjectType> UpdateAsync(ProjectType entity)
    {
        await base.UpdateAsync(entity);
        return entity;
    }

    public override async Task DeleteAsync(Guid id) => await base.DeleteAsync(id);
}

public class ProjectPriorityRepository : GenericRepository<ProjectPriority>, IProjectPriorityRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectPriorityRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public async Task<ProjectPriority?> GetByCodeAsync(string code)
        => await _dbSet.FirstOrDefaultAsync(x => x.TenantId == _currentUserProvider.TenantId && x.Code == code && !x.IsDeleted);

    public async Task<IEnumerable<ProjectPriority>> GetActiveAsync()
        => await _dbSet.Where(x => x.TenantId == _currentUserProvider.TenantId && x.IsActive && !x.IsDeleted).OrderBy(x => x.SortOrder).ToListAsync();

    public override async Task<IEnumerable<ProjectPriority>> GetAllAsync()
        => await _dbSet.Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted).OrderBy(x => x.SortOrder).ToListAsync();

    public async Task<ProjectPriority> CreateAsync(ProjectPriority entity)
    {
        await AddAsync(entity);
        return entity;
    }

    public new async Task<ProjectPriority> UpdateAsync(ProjectPriority entity)
    {
        await base.UpdateAsync(entity);
        return entity;
    }

    public override async Task DeleteAsync(Guid id) => await base.DeleteAsync(id);
}

public class ProjectTemplateRepository : GenericRepository<ProjectTemplate>, IProjectTemplateRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectTemplateRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public override async Task<ProjectTemplate?> GetByIdAsync(Guid id)
        => await _dbSet.Include(x => x.ProjectType).FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

    public async Task<ProjectTemplate?> GetByCodeAsync(string code)
        => await _dbSet.Include(x => x.ProjectType).FirstOrDefaultAsync(x => x.TenantId == _currentUserProvider.TenantId && x.Code == code && !x.IsDeleted);

    public async Task<IEnumerable<ProjectTemplate>> GetActiveAsync()
        => await _dbSet.Include(x => x.ProjectType).Where(x => x.TenantId == _currentUserProvider.TenantId && x.IsActive && !x.IsDeleted).OrderBy(x => x.Name).ToListAsync();

    public async Task<IEnumerable<ProjectTemplate>> GetByProjectTypeAsync(Guid projectTypeId)
        => await _dbSet.Include(x => x.ProjectType).Where(x => x.TenantId == _currentUserProvider.TenantId && x.ProjectTypeId == projectTypeId && !x.IsDeleted).OrderBy(x => x.Name).ToListAsync();

    public async Task<ProjectTemplate> CreateAsync(ProjectTemplate entity)
    {
        await AddAsync(entity);
        return entity;
    }

    public new async Task<ProjectTemplate> UpdateAsync(ProjectTemplate entity)
    {
        await base.UpdateAsync(entity);
        return entity;
    }

    public override async Task DeleteAsync(Guid id) => await base.DeleteAsync(id);
}

public class ProjectPortfolioRepository : GenericRepository<ProjectPortfolio>, IProjectPortfolioRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectPortfolioRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public override async Task<ProjectPortfolio?> GetByIdAsync(Guid id)
        => await _dbSet
            .Include(x => x.Programs)
            .Include(x => x.Projects)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

    public async Task<ProjectPortfolio?> GetByCodeAsync(string code)
        => await _dbSet
            .Include(x => x.Programs)
            .Include(x => x.Projects)
            .FirstOrDefaultAsync(x => x.TenantId == _currentUserProvider.TenantId && x.Code == code && !x.IsDeleted);

    public override async Task<IEnumerable<ProjectPortfolio>> GetAllAsync()
        => await _dbSet
            .Include(x => x.Programs)
            .Include(x => x.Projects)
            .Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync();

    public async Task<ProjectPortfolio> CreateAsync(ProjectPortfolio entity)
    {
        await AddAsync(entity);
        return entity;
    }

    public new async Task<ProjectPortfolio> UpdateAsync(ProjectPortfolio entity)
    {
        await base.UpdateAsync(entity);
        return entity;
    }

    public override async Task DeleteAsync(Guid id) => await base.DeleteAsync(id);
}

public class ProjectProgramRepository : GenericRepository<ProjectProgram>, IProjectProgramRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectProgramRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public override async Task<ProjectProgram?> GetByIdAsync(Guid id)
        => await _dbSet
            .Include(x => x.Portfolio)
            .Include(x => x.Projects)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

    public async Task<ProjectProgram?> GetByCodeAsync(string code)
        => await _dbSet
            .Include(x => x.Portfolio)
            .Include(x => x.Projects)
            .FirstOrDefaultAsync(x => x.TenantId == _currentUserProvider.TenantId && x.Code == code && !x.IsDeleted);

    public override async Task<IEnumerable<ProjectProgram>> GetAllAsync()
        => await _dbSet
            .Include(x => x.Portfolio)
            .Include(x => x.Projects)
            .Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync();

    public async Task<IEnumerable<ProjectProgram>> GetByPortfolioIdAsync(Guid portfolioId)
        => await _dbSet
            .Include(x => x.Portfolio)
            .Include(x => x.Projects)
            .Where(x => x.TenantId == _currentUserProvider.TenantId && x.PortfolioId == portfolioId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync();

    public async Task<ProjectProgram> CreateAsync(ProjectProgram entity)
    {
        await AddAsync(entity);
        return entity;
    }

    public new async Task<ProjectProgram> UpdateAsync(ProjectProgram entity)
    {
        await base.UpdateAsync(entity);
        return entity;
    }

    public override async Task DeleteAsync(Guid id) => await base.DeleteAsync(id);
}

public class ProjectManagementSettingsRepository : GenericRepository<ProjectManagementSettings>, IProjectManagementSettingsRepository
{
    public ProjectManagementSettingsRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ProjectManagementSettings?> GetByTenantIdAsync(Guid tenantId)
        => await _dbSet.FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ProjectManagementSettings> GetOrCreateDefaultAsync(Guid tenantId, Guid userId)
    {
        var existing = await GetByTenantIdAsync(tenantId);
        if (existing != null)
        {
            return existing;
        }

        var settings = new ProjectManagementSettings
        {
            TenantId = tenantId,
            CreatedById = userId
        };

        await AddAsync(settings);
        await _context.SaveChangesAsync();
        return settings;
    }

    public new async Task<ProjectManagementSettings> UpdateAsync(ProjectManagementSettings entity)
    {
        await base.UpdateAsync(entity);
        return entity;
    }
}

public class ProjectCatalogRepository : GenericRepository<ProjectCatalogEntry>, IProjectCatalogRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public ProjectCatalogRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public override async Task<ProjectCatalogEntry?> GetByIdAsync(Guid id)
        => await _dbSet.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted);

    public async Task<ProjectCatalogEntry?> GetByCodeAsync(string catalogType, string code)
        => await _dbSet.FirstOrDefaultAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && x.CatalogType == catalogType
            && x.Code == code
            && !x.IsDeleted);

    public override async Task<IEnumerable<ProjectCatalogEntry>> GetAllAsync()
        => await _dbSet
            .Where(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted)
            .OrderBy(x => x.CatalogType)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

    public async Task<IEnumerable<ProjectCatalogEntry>> GetByCatalogTypeAsync(string catalogType)
        => await _dbSet
            .Where(x => x.TenantId == _currentUserProvider.TenantId && x.CatalogType == catalogType && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

    public async Task<ProjectCatalogEntry> CreateAsync(ProjectCatalogEntry entity)
    {
        await AddAsync(entity);
        return entity;
    }

    public new async Task<ProjectCatalogEntry> UpdateAsync(ProjectCatalogEntry entity)
    {
        await base.UpdateAsync(entity);
        return entity;
    }

    public override async Task DeleteAsync(Guid id) => await base.DeleteAsync(id);
}
