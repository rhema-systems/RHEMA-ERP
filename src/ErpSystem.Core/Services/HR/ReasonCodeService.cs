using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class ReasonCodeService : IReasonCodeService
{
    private readonly IGenericRepository<ReasonCode> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReasonCodeService> _logger;

    public ReasonCodeService(
        IGenericRepository<ReasonCode> repository,
        IUnitOfWork unitOfWork,
        ILogger<ReasonCodeService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<ReasonCodeDto>> GetAllAsync(ReasonCodeCategory? category = null, bool activeOnly = false)
    {
        var query = _repository.GetQueryable();

        if (category.HasValue)
            query = query.Where(r => r.Category == category.Value);
        if (activeOnly)
            query = query.Where(r => r.IsActive);

        var items = await query
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToListAsync();

        return items.Select(ToDto);
    }

    public async Task<ReasonCodeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<ReasonCodeDto> CreateAsync(CreateReasonCodeDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var exists = await _repository.GetQueryable().AnyAsync(r => r.Code == dto.Code);
            if (exists)
                throw new InvalidOperationException($"A reason code '{dto.Code}' already exists.");
        }

        var entity = new ReasonCode
        {
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            IsActive = dto.IsActive
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Reason code created: {Code} ({Category})", entity.Code, entity.Category);
        return ToDto(entity);
    }

    public async Task<ReasonCodeDto> UpdateAsync(Guid id, UpdateReasonCodeDto dto)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Reason code '{id}' not found.");

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code != entity.Code)
        {
            var clash = await _repository.GetQueryable().AnyAsync(r => r.Code == dto.Code && r.Id != id);
            if (clash)
                throw new InvalidOperationException($"A reason code '{dto.Code}' already exists.");
        }

        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.IsActive = dto.IsActive;

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Reason code '{id}' not found.");

        entity.IsActive = false;
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private static ReasonCodeDto ToDto(ReasonCode e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        Category = e.Category,
        IsActive = e.IsActive
    };
}
