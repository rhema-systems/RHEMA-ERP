using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationProgramService : IOrientationProgramService
{
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IOrientationModuleRepository _moduleRepository;
    private readonly IOrientationContentItemRepository _contentItemRepository;
    private readonly IOrientationPrerequisiteRepository _prerequisiteRepository;
    private readonly IOrientationAudienceRuleRepository _audienceRuleRepository;
    private readonly IOrientationAssessmentQuestionRepository _questionRepository;
    private readonly IOrientationAssessmentOptionRepository _optionRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrientationProgramService> _logger;

    public OrientationProgramService(
        IOrientationProgramRepository programRepository,
        IOrientationModuleRepository moduleRepository,
        IOrientationContentItemRepository contentItemRepository,
        IOrientationPrerequisiteRepository prerequisiteRepository,
        IOrientationAudienceRuleRepository audienceRuleRepository,
        IOrientationAssessmentQuestionRepository questionRepository,
        IOrientationAssessmentOptionRepository optionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<OrientationProgramService> logger)
    {
        _programRepository = programRepository;
        _moduleRepository = moduleRepository;
        _contentItemRepository = contentItemRepository;
        _prerequisiteRepository = prerequisiteRepository;
        _audienceRuleRepository = audienceRuleRepository;
        _questionRepository = questionRepository;
        _optionRepository = optionRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<OrientationProgram> GetOwnedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation program with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationProgram> GetOwnedProgramWithDetailsAsync(Guid id)
    {
        var entity = await _programRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation program with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationModule> GetOwnedModuleAsync(Guid id)
    {
        var entity = await _moduleRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation module with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationContentItem> GetOwnedContentItemAsync(Guid id)
    {
        var entity = await _contentItemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation content item with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationPrerequisite> GetOwnedPrerequisiteAsync(Guid id)
    {
        var entity = await _prerequisiteRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation prerequisite with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationAudienceRule> GetOwnedAudienceRuleAsync(Guid id)
    {
        var entity = await _audienceRuleRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation audience rule with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationAssessmentQuestion> GetOwnedQuestionAsync(Guid id)
    {
        var entity = await _questionRepository.GetWithOptionsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation assessment question with ID '{id}' not found.");
        return entity;
    }

    // ====================================================================
    // PROGRAM QUERIES
    // ====================================================================

    public async Task<OrientationProgramDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<OrientationProgramDto?> GetByProgramCodeAsync(string programCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _programRepository.GetByProgramCodeAsync(programCode);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Category)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<OrientationProgramSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _programRepository.GetQueryable().Where(p => p.TenantId == tenantId).Include(p => p.Category);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrientationProgramSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByStatusAsync(OrientationProgramStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetByStatusAsync(status))
            .Where(p => p.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetByCategoryAsync(categoryId))
            .Where(p => p.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByTypeAsync(OrientationProgramType programType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetByTypeAsync(programType))
            .Where(p => p.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetActiveProgramsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetActiveProgramsAsync())
            .Where(p => p.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByOwnerOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _programRepository.GetByOwnerOrganizationUnitAsync(organizationUnitId))
            .Where(p => p.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    // ====================================================================
    // PROGRAM CRUD + LIFECYCLE
    // ====================================================================

    public async Task<OrientationProgramDto> CreateAsync(CreateOrientationProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        entity.ProgramCode = string.IsNullOrWhiteSpace(createDto.ProgramCode)
            ? await GenerateProgramCodeAsync(tenantId, cancellationToken)
            : createDto.ProgramCode.Trim();

        var codeExists = await _programRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == tenantId && p.ProgramCode == entity.ProgramCode && !p.IsDeleted, cancellationToken);
        if (codeExists)
            throw new InvalidOperationException($"Program code '{entity.ProgramCode}' is already in use.");

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program created: {Code}", entity.ProgramCode);

        return (await _programRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<OrientationProgramDto> UpdateAsync(UpdateOrientationProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program updated: {Code}", entity.ProgramCode);

        return (await _programRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> ChangeStatusAsync(ChangeOrientationProgramStatusDto changeDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(changeDto.ProgramId);

        entity.Status = changeDto.NewStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program {Code} status changed to {Status}", entity.ProgramCode, changeDto.NewStatus);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(id);

        if (entity.Status == OrientationProgramStatus.Active)
            throw new InvalidOperationException("An active program cannot be deleted. Suspend or retire it first.");

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program deleted: {Code}", entity.ProgramCode);
        return true;
    }

    // ====================================================================
    // MODULES
    // ====================================================================

    public async Task<OrientationModuleDto> AddModuleAsync(CreateOrientationModuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await EnsureProgramExistsAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        if (entity.SequenceOrder <= 0)
            entity.SequenceOrder = await _moduleRepository.GetMaxSequenceOrderAsync(createDto.ProgramId) + 1;

        await _moduleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationModuleDto>> GetModulesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _moduleRepository.GetByProgramIdAsync(programId))
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.ToDto());
    }

    public async Task<OrientationModuleDto> UpdateModuleAsync(UpdateOrientationModuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedModuleAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _moduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteModuleAsync(Guid moduleId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedModuleAsync(moduleId);

        await _moduleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // CONTENT ITEMS
    // ====================================================================

    public async Task<OrientationContentItemDto> AddContentItemAsync(CreateOrientationContentItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var module = await GetOwnedModuleAsync(createDto.ModuleId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        if (entity.SequenceOrder <= 0)
            entity.SequenceOrder = await _contentItemRepository.GetMaxSequenceOrderAsync(module.Id) + 1;

        await _contentItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationContentItemDto>> GetContentItemsAsync(Guid moduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _contentItemRepository.GetByModuleIdAsync(moduleId))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToDto());
    }

    public async Task<OrientationContentItemDto> UpdateContentItemAsync(UpdateOrientationContentItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContentItemAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _contentItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteContentItemAsync(Guid contentItemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContentItemAsync(contentItemId);

        await _contentItemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // PREREQUISITES
    // ====================================================================

    public async Task<OrientationPrerequisiteDto> AddPrerequisiteAsync(CreateOrientationPrerequisiteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        if (createDto.ProgramId == createDto.PrerequisiteProgramId)
            throw new InvalidOperationException("A program cannot be a prerequisite of itself.");

        await EnsureProgramExistsAsync(createDto.ProgramId);
        await EnsureProgramExistsAsync(createDto.PrerequisiteProgramId);

        if (await _prerequisiteRepository.ExistsAsync(createDto.ProgramId, createDto.PrerequisiteProgramId))
            throw new InvalidOperationException("This prerequisite is already configured for the program.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _prerequisiteRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _prerequisiteRepository.FirstOrDefaultAsync(p => p.Id == entity.Id, p => p.PrerequisiteProgram))!.ToDto();
    }

    public async Task<IEnumerable<OrientationPrerequisiteDto>> GetPrerequisitesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _prerequisiteRepository.GetByProgramIdAsync(programId))
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.ToDto());
    }

    public async Task<bool> DeletePrerequisiteAsync(Guid prerequisiteId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPrerequisiteAsync(prerequisiteId);

        await _prerequisiteRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // AUDIENCE RULES
    // ====================================================================

    public async Task<OrientationAudienceRuleDto> AddAudienceRuleAsync(CreateOrientationAudienceRuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await EnsureProgramExistsAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _audienceRuleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationAudienceRuleDto>> GetAudienceRulesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _audienceRuleRepository.GetByProgramIdAsync(programId))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto());
    }

    public async Task<OrientationAudienceRuleDto> UpdateAudienceRuleAsync(UpdateOrientationAudienceRuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAudienceRuleAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _audienceRuleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAudienceRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAudienceRuleAsync(ruleId);

        await _audienceRuleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // ASSESSMENT QUESTIONS (+ options)
    // ====================================================================

    public async Task<OrientationAssessmentQuestionDto> AddQuestionAsync(CreateOrientationAssessmentQuestionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await EnsureProgramExistsAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        if (entity.SequenceOrder <= 0)
            entity.SequenceOrder = await _questionRepository.GetMaxSequenceOrderAsync(createDto.ProgramId) + 1;

        await _questionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _questionRepository.GetWithOptionsAsync(entity.Id))!.ToDto();
    }

    public async Task<IEnumerable<OrientationAssessmentQuestionDto>> GetQuestionsAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _questionRepository.GetByProgramIdAsync(programId))
            .Where(q => q.TenantId == tenantId)
            .Select(q => q.ToDto());
    }

    public async Task<OrientationAssessmentQuestionDto> UpdateQuestionAsync(UpdateOrientationAssessmentQuestionDto updateDto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await GetOwnedQuestionAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        // Replace the option set wholesale.
        if (entity.Options.Any())
            await _optionRepository.DeleteRangeAsync(entity.Options.ToList());

        var newOptions = updateDto.Options
            .Select(o => o.ToEntity(tenantId, updatedByUserId))
            .ToList();
        foreach (var opt in newOptions)
            opt.QuestionId = entity.Id;

        if (newOptions.Count > 0)
            await _optionRepository.AddRangeAsync(newOptions);

        await _questionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _questionRepository.GetWithOptionsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionAsync(questionId);

        if (entity.Options.Any())
            await _optionRepository.DeleteRangeAsync(entity.Options.ToList());

        await _questionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // HELPERS
    // ====================================================================

    private async Task EnsureProgramExistsAsync(Guid programId)
    {
        await GetOwnedProgramAsync(programId);
    }

    private async Task<string> GenerateProgramCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"ORI-{DateTime.UtcNow.Year}-";
        var codes = await _programRepository.GetQueryable()
            .Where(p => p.TenantId == tenantId && p.ProgramCode.StartsWith(prefix))
            .Select(p => p.ProgramCode)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in codes)
        {
            var suffix = code.Substring(prefix.Length);
            if (int.TryParse(suffix, out var n) && n > max) max = n;
        }
        return $"{prefix}{(max + 1):D4}";
    }
}
