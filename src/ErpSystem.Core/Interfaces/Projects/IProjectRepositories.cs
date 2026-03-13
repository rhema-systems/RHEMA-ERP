using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Interfaces.Projects;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id);
    Task<Project?> GetDetailByIdAsync(Guid id);
    Task<Project?> GetByProjectCodeAsync(string projectCode);
    Task<PagedResult<Project>> GetPagedAsync(int page, int pageSize, string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null);
    Task<IEnumerable<Project>> LookupAsync(string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null, int take = 20);
    Task<Project> CreateAsync(Project project);
    Task<Project> UpdateAsync(Project project);
    Task DeleteAsync(Guid id);
    Task<string> GenerateProjectCodeAsync();
}

public interface IProjectTypeRepository
{
    Task<ProjectType?> GetByIdAsync(Guid id);
    Task<ProjectType?> GetByCodeAsync(string code);
    Task<IEnumerable<ProjectType>> GetActiveAsync();
    Task<IEnumerable<ProjectType>> GetAllAsync();
    Task<ProjectType> CreateAsync(ProjectType entity);
    Task<ProjectType> UpdateAsync(ProjectType entity);
    Task DeleteAsync(Guid id);
}

public interface IProjectPriorityRepository
{
    Task<ProjectPriority?> GetByIdAsync(Guid id);
    Task<ProjectPriority?> GetByCodeAsync(string code);
    Task<IEnumerable<ProjectPriority>> GetActiveAsync();
    Task<IEnumerable<ProjectPriority>> GetAllAsync();
    Task<ProjectPriority> CreateAsync(ProjectPriority entity);
    Task<ProjectPriority> UpdateAsync(ProjectPriority entity);
    Task DeleteAsync(Guid id);
}

public interface IProjectTemplateRepository
{
    Task<ProjectTemplate?> GetByIdAsync(Guid id);
    Task<ProjectTemplate?> GetByCodeAsync(string code);
    Task<IEnumerable<ProjectTemplate>> GetActiveAsync();
    Task<IEnumerable<ProjectTemplate>> GetByProjectTypeAsync(Guid projectTypeId);
    Task<ProjectTemplate> CreateAsync(ProjectTemplate entity);
    Task<ProjectTemplate> UpdateAsync(ProjectTemplate entity);
    Task DeleteAsync(Guid id);
}

public interface IProjectPortfolioRepository
{
    Task<ProjectPortfolio?> GetByIdAsync(Guid id);
    Task<ProjectPortfolio?> GetByCodeAsync(string code);
    Task<IEnumerable<ProjectPortfolio>> GetAllAsync();
    Task<ProjectPortfolio> CreateAsync(ProjectPortfolio entity);
    Task<ProjectPortfolio> UpdateAsync(ProjectPortfolio entity);
    Task DeleteAsync(Guid id);
}

public interface IProjectProgramRepository
{
    Task<ProjectProgram?> GetByIdAsync(Guid id);
    Task<ProjectProgram?> GetByCodeAsync(string code);
    Task<IEnumerable<ProjectProgram>> GetAllAsync();
    Task<IEnumerable<ProjectProgram>> GetByPortfolioIdAsync(Guid portfolioId);
    Task<ProjectProgram> CreateAsync(ProjectProgram entity);
    Task<ProjectProgram> UpdateAsync(ProjectProgram entity);
    Task DeleteAsync(Guid id);
}

public interface IProjectManagementSettingsRepository
{
    Task<ProjectManagementSettings> GetOrCreateDefaultAsync(Guid tenantId, Guid userId);
    Task<ProjectManagementSettings?> GetByTenantIdAsync(Guid tenantId);
    Task<ProjectManagementSettings> UpdateAsync(ProjectManagementSettings entity);
}

public interface IProjectCatalogRepository
{
    Task<ProjectCatalogEntry?> GetByIdAsync(Guid id);
    Task<ProjectCatalogEntry?> GetByCodeAsync(string catalogType, string code);
    Task<IEnumerable<ProjectCatalogEntry>> GetAllAsync();
    Task<IEnumerable<ProjectCatalogEntry>> GetByCatalogTypeAsync(string catalogType);
    Task<ProjectCatalogEntry> CreateAsync(ProjectCatalogEntry entity);
    Task<ProjectCatalogEntry> UpdateAsync(ProjectCatalogEntry entity);
    Task DeleteAsync(Guid id);
}
