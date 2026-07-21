using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Salary Structure service.
/// 
/// Responsibilities:
/// - Orchestrates Salary Grade/Level/Notch use cases
/// - Enforces module invariants (duplicates, ordering, range validation)
/// - Uses repositories for data access only
/// - Uses UnitOfWork for transactional integrity
/// </summary>
public class SalaryStructureService :
    ISalaryStructureService,
    ISalaryGradeService,
    ISalaryLevelService,
    ISalaryNotchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SalaryStructureService> _logger;

    private readonly IGenericRepository<SalaryGrade> _salaryGradeRepository;
    private readonly IGenericRepository<SalaryLevel> _salaryLevelRepository;
    private readonly IGenericRepository<SalaryNotch> _salaryNotchRepository;

    public SalaryStructureService(
        IGenericRepository<SalaryGrade> salaryGradeRepository,
        IGenericRepository<SalaryLevel> salaryLevelRepository,
        IGenericRepository<SalaryNotch> salaryNotchRepository,
        IUnitOfWork unitOfWork,
        ILogger<SalaryStructureService> logger)
    {
        _salaryGradeRepository = salaryGradeRepository;
        _salaryLevelRepository = salaryLevelRepository;
        _salaryNotchRepository = salaryNotchRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    #region Salary Grades

    public async Task<SalaryGradeDto> CreateGradeAsync(Guid tenantId, CreateSalaryGradeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureTenantMatches(dto.TenantId, tenantId, nameof(CreateSalaryGradeDto.TenantId));

        var code = NormalizeCode(dto.Code);

        var codeExists = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Code == code)
            .AnyAsync(cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A salary grade with code '{code}' already exists.");
        }

        EnsureGradeBandIsValid(dto.MinSalary, dto.MaxSalary);
        EnsureDateRange(dto.EffectiveDate, dto.EndDate);

        var entity = dto.ToEntity(tenantId);
        entity.Code = code;

        await _salaryGradeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary grade created: {GradeId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<SalaryGradeDto> UpdateGradeAsync(Guid tenantId, UpdateSalaryGradeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureTenantMatches(dto.TenantId, tenantId, nameof(UpdateSalaryGradeDto.TenantId));

        var entity = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Id == dto.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            throw new ArgumentException($"Salary grade with ID '{dto.Id}' not found.");
        }

        var code = NormalizeCode(dto.Code);
        var codeExists = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Code == code && g.Id != dto.Id)
            .AnyAsync(cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A salary grade with code '{code}' already exists.");
        }

        EnsureGradeBandIsValid(dto.MinSalary, dto.MaxSalary);
        EnsureDateRange(dto.EffectiveDate, dto.EndDate);

        dto.UpdateEntity(entity);
        entity.Code = code;

        await _salaryGradeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary grade updated: {GradeId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<SalaryGradeDto> SetGradeActiveAsync(Guid tenantId, Guid gradeId, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Id == gradeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Salary grade with ID '{gradeId}' not found.");
        }

        entity.IsActive = isActive;
        await _salaryGradeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary grade active status set: {GradeId} -> {IsActive}", gradeId, isActive);

        return entity.ToDto();
    }

    public async Task<bool> DeleteGradeAsync(Guid tenantId, Guid gradeId, CancellationToken cancellationToken = default)
    {
        var entity = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Id == gradeId)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            throw new ArgumentException($"Salary grade with ID '{gradeId}' not found.");
        }

        // Prevent deletion if levels exist
        var hasLevels = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.SalaryGradeId == gradeId)
            .AnyAsync(cancellationToken);

        if (hasLevels)
        {
            throw new InvalidOperationException("Cannot delete a salary grade that has salary levels.");
        }

        await _salaryGradeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary grade deleted (soft): {GradeId}", gradeId);

        return true;
    }

    public async Task<SalaryGradeDetailDto> GetGradeHierarchyAsync(Guid tenantId, Guid gradeId, CancellationToken cancellationToken = default)
    {
        var entity = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Id == gradeId)
            .AsNoTracking()
            .Include(g => g.Levels.Where(l => !l.IsDeleted && l.TenantId == tenantId).OrderBy(l => l.Sequence))
                .ThenInclude(l => l.Notches.Where(n => !n.IsDeleted && n.TenantId == tenantId).OrderBy(n => n.NotchNumber))
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            throw new ArgumentException($"Salary grade with ID '{gradeId}' not found.");
        }

        // Detail mapping orders Levels by Sequence.
        return entity.ToDetailDto();
    }

    public async Task<IReadOnlyList<SalaryGradeDto>> GetAllGradesAsync(Guid tenantId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(g => g.IsActive);
        }

        var grades = await query
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Code)
            .ToListAsync(cancellationToken);

        return grades.Select(g => g.ToDto()).ToList();
    }

    public async Task<PagedResult<SalaryGradeDto>> GetGradesPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var query = _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId)
            .AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(g => g.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(g => g.Code.Contains(term) || g.Name.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SalaryGradeDto>
        {
            Items = items.Select(g => g.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    #endregion

    #region Salary Levels

    public async Task<SalaryLevelDto> CreateLevelAsync(Guid tenantId, CreateSalaryLevelDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureTenantMatches(dto.TenantId, tenantId, nameof(CreateSalaryLevelDto.TenantId));

        // Ensure parent grade exists
        var grade = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Id == dto.SalaryGradeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (grade == null)
        {
            throw new ArgumentException($"Salary grade with ID '{dto.SalaryGradeId}' not found.");
        }

        EnsureLevelWithinGradeBounds(grade, dto.MinSalary, dto.MaxSalary);
        EnsureLevelBandIsValid(dto.MinSalary, dto.MidSalary, dto.MaxSalary);

        var code = NormalizeCode(dto.Code);
        var codeExists = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.SalaryGradeId == dto.SalaryGradeId && l.Code == code)
            .AnyAsync(cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A salary level with code '{code}' already exists within this grade.");
        }

        await EnsureNoOverlappingRangesAsync(tenantId, dto.SalaryGradeId, dto.MinSalary, dto.MaxSalary, excludeLevelId: null, cancellationToken);

        var entity = dto.ToEntity(tenantId);
        entity.Code = code;

        await _salaryLevelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary level created: {LevelId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<SalaryLevelDto> UpdateLevelAsync(Guid tenantId, UpdateSalaryLevelDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureTenantMatches(dto.TenantId, tenantId, nameof(UpdateSalaryLevelDto.TenantId));

        var entity = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.Id == dto.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            throw new ArgumentException($"Salary level with ID '{dto.Id}' not found.");
        }

        // Parent grade must exist
        var grade = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Id == dto.SalaryGradeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (grade == null)
        {
            throw new ArgumentException($"Salary grade with ID '{dto.SalaryGradeId}' not found.");
        }

        EnsureLevelWithinGradeBounds(grade, dto.MinSalary, dto.MaxSalary);
        EnsureLevelBandIsValid(dto.MinSalary, dto.MidSalary, dto.MaxSalary);

        var code = NormalizeCode(dto.Code);
        var codeExists = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.SalaryGradeId == dto.SalaryGradeId && l.Code == code && l.Id != dto.Id)
            .AnyAsync(cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A salary level with code '{code}' already exists within this grade.");
        }

        await EnsureNoOverlappingRangesAsync(tenantId, dto.SalaryGradeId, dto.MinSalary, dto.MaxSalary, excludeLevelId: dto.Id, cancellationToken);

        dto.UpdateEntity(entity);
        entity.Code = code;

        await _salaryLevelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary level updated: {LevelId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<SalaryLevelDetailDto> GetLevelDetailAsync(Guid tenantId, Guid levelId, CancellationToken cancellationToken = default)
    {
        var entity = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.Id == levelId)
            .AsNoTracking()
            .Include(l => l.Notches.Where(n => !n.IsDeleted && n.TenantId == tenantId).OrderBy(n => n.NotchNumber))
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            throw new ArgumentException($"Salary level with ID '{levelId}' not found.");
        }

        // Detail mapping orders Notches by NotchNumber.
        return entity.ToDetailDto();
    }

    public async Task<IReadOnlyList<SalaryLevelDto>> GetLevelsByGradeAsync(Guid tenantId, Guid gradeId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.SalaryGradeId == gradeId)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(l => l.IsActive);
        }

        var levels = await query
            .OrderBy(l => l.Sequence)
            .ThenBy(l => l.Code)
            .ToListAsync(cancellationToken);

        return levels.Select(l => l.ToDto()).ToList();
    }

    public async Task<bool> DeleteLevelAsync(Guid tenantId, Guid levelId, CancellationToken cancellationToken = default)
    {
        var level = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.Id == levelId)
            .FirstOrDefaultAsync(cancellationToken);

        if (level == null)
        {
            throw new ArgumentException($"Salary level with ID '{levelId}' not found.");
        }

        var hasNotches = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.SalaryLevelId == levelId)
            .AnyAsync(cancellationToken);

        if (hasNotches)
        {
            throw new InvalidOperationException("Cannot delete a salary level that has salary notches.");
        }

        await _salaryLevelRepository.DeleteAsync(level);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary level deleted (soft): {LevelId}", levelId);

        return true;
    }

    public async Task<SalaryLevelDto> SetLevelActiveAsync(Guid tenantId, Guid levelId, bool isActive, CancellationToken cancellationToken = default)
    {
        var level = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.Id == levelId)
            .FirstOrDefaultAsync(cancellationToken);

        if (level == null)
        {
            throw new ArgumentException($"Salary level with ID '{levelId}' not found.");
        }

        level.IsActive = isActive;
        level.UpdatedAt = DateTime.UtcNow;

        await _salaryLevelRepository.UpdateAsync(level);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return level.ToDto();
    }

    public async Task ResequenceLevelsAsync(Guid tenantId, Guid gradeId, IReadOnlyList<Guid> orderedLevelIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderedLevelIds);

        if (orderedLevelIds.Count == 0)
        {
            return;
        }

        // Ensure no duplicates
        if (orderedLevelIds.Count != orderedLevelIds.Distinct().Count())
        {
            throw new InvalidOperationException("The ordered level list contains duplicate IDs.");
        }

        // Ensure grade exists
        var grade = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId && g.Id == gradeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (grade == null)
        {
            throw new ArgumentException($"Salary grade with ID '{gradeId}' not found.");
        }

        // Ensure all provided levels belong to the grade
        var existingLevels = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.SalaryGradeId == gradeId)
            .AsNoTracking()
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var existingIds = existingLevels.ToHashSet();

        foreach (var id in orderedLevelIds)
        {
            if (!existingIds.Contains(id))
            {
                throw new InvalidOperationException("One or more levels do not belong to the specified grade.");
            }
        }

        var sequences = new Dictionary<Guid, int>(orderedLevelIds.Count);
        for (var i = 0; i < orderedLevelIds.Count; i++)
        {
            sequences[orderedLevelIds[i]] = i + 1;
        }

        var levels = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.SalaryGradeId == gradeId && existingIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        foreach (var level in levels)
        {
            if (sequences.TryGetValue(level.Id, out var sequence))
            {
                level.Sequence = sequence;
            }
        }

        await _salaryLevelRepository.UpdateRangeAsync(levels);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary levels resequenced for grade: {GradeId}", gradeId);
    }

    #endregion

    #region Salary Notches

    public async Task<SalaryNotchDto> CreateNotchAsync(Guid tenantId, CreateSalaryNotchDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureTenantMatches(dto.TenantId, tenantId, nameof(CreateSalaryNotchDto.TenantId));

        // Ensure parent level exists
        var level = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.Id == dto.SalaryLevelId)
            .Include(l => l.Grade)
            .FirstOrDefaultAsync(cancellationToken);
        if (level == null)
        {
            throw new ArgumentException($"Salary level with ID '{dto.SalaryLevelId}' not found.");
        }

        EnsureNotchWithinGradeBounds(level.Grade, dto.SalaryAmount);
        EnsureNotchSalaryWithinLevel(level, dto.SalaryAmount);

        // Notch number must be unique
        var notchNumberExists = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.SalaryLevelId == dto.SalaryLevelId && n.NotchNumber == dto.NotchNumber)
            .AnyAsync(cancellationToken);

        if (notchNumberExists)
        {
            throw new InvalidOperationException($"Notch number '{dto.NotchNumber}' already exists within this level.");
        }

        // Enforce sequential numbering: next notch must be (max + 1)
        var max = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.SalaryLevelId == dto.SalaryLevelId)
            .Select(n => (int?)n.NotchNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var expected = max + 1;
        if (dto.NotchNumber != expected)
        {
            throw new InvalidOperationException($"Notch numbers must be sequential. Expected next notch number is {expected}.");
        }

        var entity = dto.ToEntity(tenantId);

        await _salaryNotchRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary notch created: {NotchId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<SalaryNotchDto> UpdateNotchAsync(Guid tenantId, UpdateSalaryNotchDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureTenantMatches(dto.TenantId, tenantId, nameof(UpdateSalaryNotchDto.TenantId));

        var entity = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.Id == dto.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            throw new ArgumentException($"Salary notch with ID '{dto.Id}' not found.");
        }

        // Ensure parent level exists
        var level = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.Id == dto.SalaryLevelId)
            .Include(l => l.Grade)
            .FirstOrDefaultAsync(cancellationToken);
        if (level == null)
        {
            throw new ArgumentException($"Salary level with ID '{dto.SalaryLevelId}' not found.");
        }

        EnsureNotchWithinGradeBounds(level.Grade, dto.SalaryAmount);
        EnsureNotchSalaryWithinLevel(level, dto.SalaryAmount);

        // Notch number must be unique
        var notchNumberExists = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.SalaryLevelId == dto.SalaryLevelId && n.NotchNumber == dto.NotchNumber && n.Id != dto.Id)
            .AnyAsync(cancellationToken);

        if (notchNumberExists)
        {
            throw new InvalidOperationException($"Notch number '{dto.NotchNumber}' already exists within this level.");
        }

        // Enforce sequential set: after update, notch numbers must be 1..N
        var existing = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.SalaryLevelId == dto.SalaryLevelId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var numbers = existing
            .Where(n => n.Id != dto.Id)
            .Select(n => n.NotchNumber)
            .Append(dto.NotchNumber)
            .OrderBy(n => n)
            .ToList();

        for (var i = 0; i < numbers.Count; i++)
        {
            if (numbers[i] != i + 1)
            {
                throw new InvalidOperationException("Notch numbers must remain sequential (1..N) within a level.");
            }
        }

        dto.UpdateEntity(entity);

        await _salaryNotchRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Salary notch updated: {NotchId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteNotchAsync(Guid tenantId, Guid notchId, CancellationToken cancellationToken = default)
    {
        // Hard delete via EF Core translated DELETE. No DbContext dependency needed here.
        var deleted = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.Id == notchId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new ArgumentException($"Salary notch with ID '{notchId}' not found.");
        }

        _logger.LogInformation("Salary notch hard deleted: {NotchId}", notchId);

        return true;
    }

    public async Task<IReadOnlyList<SalaryNotchDto>> GetNotchesByLevelAsync(Guid tenantId, Guid levelId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.SalaryLevelId == levelId)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(n => n.IsActive);
        }

        var notches = await query
            .OrderBy(n => n.NotchNumber)
            .ToListAsync(cancellationToken);

        return notches.Select(n => n.ToDto()).ToList();
    }

    public async Task<SalaryNotchDto> GetNotchByIdAsync(Guid tenantId, Guid notchId, CancellationToken cancellationToken = default)
    {
        var notch = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.Id == notchId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (notch == null)
        {
            throw new ArgumentException($"Salary notch with ID '{notchId}' not found.");
        }

        return notch.ToDto();
    }

    public async Task<SalaryNotchDto> SetNotchActiveAsync(Guid tenantId, Guid notchId, bool isActive, CancellationToken cancellationToken = default)
    {
        var notch = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && n.Id == notchId)
            .FirstOrDefaultAsync(cancellationToken);

        if (notch == null)
        {
            throw new ArgumentException($"Salary notch with ID '{notchId}' not found.");
        }

        notch.IsActive = isActive;
        notch.UpdatedAt = DateTime.UtcNow;

        await _salaryNotchRepository.UpdateAsync(notch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notch.ToDto();
    }

    #endregion

    #region Validation helpers

    private static void EnsureTenantMatches(Guid dtoTenantId, Guid tenantId, string fieldName)
    {
        // For safety, allow empty GUID (callers may omit) but forbid mismatch.
        if (dtoTenantId != Guid.Empty && dtoTenantId != tenantId)
        {
            throw new InvalidOperationException($"Tenant mismatch for '{fieldName}'.");
        }
    }

    private static string NormalizeCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Code is required.");
        }
        return normalized;
    }

    private static void EnsureDateRange(DateTime effectiveDate, DateTime? endDate)
    {
        if (endDate.HasValue && endDate.Value.Date < effectiveDate.Date)
        {
            throw new InvalidOperationException("EndDate cannot be earlier than EffectiveDate.");
        }
    }

    private static void EnsureLevelBandIsValid(decimal min, decimal mid, decimal max)
    {
        if (min < 0 || mid < 0 || max < 0)
        {
            throw new InvalidOperationException("Salary values cannot be negative.");
        }

        if (min > max)
        {
            throw new InvalidOperationException("MinSalary cannot be greater than MaxSalary.");
        }

        if (mid < min || mid > max)
        {
            throw new InvalidOperationException("MidSalary must be between MinSalary and MaxSalary.");
        }
    }

    private async Task EnsureNoOverlappingRangesAsync(
        Guid tenantId,
        Guid gradeId,
        decimal newMin,
        decimal newMax,
        Guid? excludeLevelId,
        CancellationToken cancellationToken)
    {
        var levels = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && l.SalaryGradeId == gradeId && l.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var level in levels)
        {
            if (excludeLevelId.HasValue && level.Id == excludeLevelId.Value)
            {
                continue;
            }

            var overlaps = newMin <= level.MaxSalary && newMax >= level.MinSalary;
            if (overlaps)
            {
                throw new InvalidOperationException("Salary ranges cannot overlap within the same grade.");
            }
        }
    }

    private static void EnsureNotchSalaryWithinLevel(SalaryLevel level, decimal notchAmount)
    {
        if (notchAmount < level.MinSalary || notchAmount > level.MaxSalary)
        {
            throw new InvalidOperationException("Salary amount must be within the level's MinSalary and MaxSalary.");
        }
    }

    private static void EnsureGradeBandIsValid(decimal min, decimal max)
    {
        if (min < 0 || max < 0)
        {
            throw new InvalidOperationException("Salary values cannot be negative.");
        }

        if (min > max)
        {
            throw new InvalidOperationException("MinSalary cannot be greater than MaxSalary.");
        }
    }

    private static void EnsureLevelWithinGradeBounds(SalaryGrade grade, decimal levelMin, decimal levelMax)
    {
        if (levelMin < grade.MinSalary || levelMax > grade.MaxSalary)
        {
            throw new InvalidOperationException($"Salary level range ({levelMin} - {levelMax}) must fall within the grade's salary range ({grade.MinSalary} - {grade.MaxSalary}).");
        }
    }

    private static void EnsureNotchWithinGradeBounds(SalaryGrade grade, decimal notchAmount)
    {
        if (notchAmount < grade.MinSalary || notchAmount > grade.MaxSalary)
        {
            throw new InvalidOperationException($"Salary notch amount ({notchAmount}) must fall within the grade's salary range ({grade.MinSalary} - {grade.MaxSalary}).");
        }
    }

    #endregion
}
