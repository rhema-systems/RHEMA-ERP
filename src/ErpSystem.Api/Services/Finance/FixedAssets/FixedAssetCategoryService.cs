using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class FixedAssetCategoryService : IFixedAssetCategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FixedAssetCategoryService(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
    private string UserName => _currentUser.UserName ?? "system";

    public async Task<FixedAssetCategoryDto?> GetByIdAsync(Guid id)
    {
        var category = await _context.FixedAssetCategories
            .Where(c => c.TenantId == TenantId && c.Id == id)
            .FirstOrDefaultAsync();

        return category == null ? null : MapToDto(category);
    }

    public async Task<IEnumerable<FixedAssetCategoryDto>> GetAllAsync()
    {
        var categories = await _context.FixedAssetCategories
            .Where(c => c.TenantId == TenantId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return categories.Select(MapToDto).ToList();
    }

    public async Task<FixedAssetCategoryDto> CreateAsync(CreateFixedAssetCategoryDto dto)
    {
        var category = new FixedAssetCategory
        {
            TenantId = TenantId,
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            DefaultMethod = dto.DefaultMethod,
            DefaultUsefulLifeMonths = dto.DefaultUsefulLifeMonths,
            DefaultResidualValuePercent = dto.DefaultResidualValuePercent,
            AssetAccountId = dto.AssetAccountId,
            AccumulatedDepreciationAccountId = dto.AccumulatedDepreciationAccountId,
            DepreciationExpenseAccountId = dto.DepreciationExpenseAccountId,
            GainOnDisposalAccountId = dto.GainOnDisposalAccountId,
            LossOnDisposalAccountId = dto.LossOnDisposalAccountId,
            RevaluationSurplusAccountId = dto.RevaluationSurplusAccountId,
            AucAccountId = dto.AucAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.FixedAssetCategories.Add(category);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(category.Id) ?? throw new InvalidOperationException("Failed to create category.");
    }

    public async Task<FixedAssetCategoryDto> UpdateAsync(Guid id, UpdateFixedAssetCategoryDto dto)
    {
        var category = await _context.FixedAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset category not found.");

        category.Name = dto.Name;
        category.Code = dto.Code;
        category.Description = dto.Description;
        category.DefaultMethod = dto.DefaultMethod;
        category.DefaultUsefulLifeMonths = dto.DefaultUsefulLifeMonths;
        category.DefaultResidualValuePercent = dto.DefaultResidualValuePercent;
        category.AssetAccountId = dto.AssetAccountId;
        category.AccumulatedDepreciationAccountId = dto.AccumulatedDepreciationAccountId;
        category.DepreciationExpenseAccountId = dto.DepreciationExpenseAccountId;
        category.GainOnDisposalAccountId = dto.GainOnDisposalAccountId;
        category.LossOnDisposalAccountId = dto.LossOnDisposalAccountId;
        category.RevaluationSurplusAccountId = dto.RevaluationSurplusAccountId;
        category.AucAccountId = dto.AucAccountId;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = UserName;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(category.Id) ?? throw new InvalidOperationException("Failed to update category.");
    }

    public async Task DeleteAsync(Guid id)
    {
        var category = await _context.FixedAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset category not found.");

        var hasAssets = await _context.FixedAssets
            .AnyAsync(a => a.TenantId == TenantId && a.FixedAssetCategoryId == id);

        if (hasAssets)
        {
            throw new InvalidOperationException("Cannot delete a category with existing assets.");
        }

        _context.FixedAssetCategories.Remove(category);
        await _context.SaveChangesAsync();
    }

    private static FixedAssetCategoryDto MapToDto(FixedAssetCategory category)
    {
        return new FixedAssetCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Code = category.Code,
            Description = category.Description,
            DefaultMethod = category.DefaultMethod,
            DefaultUsefulLifeMonths = category.DefaultUsefulLifeMonths,
            DefaultResidualValuePercent = category.DefaultResidualValuePercent,
            AssetAccountId = category.AssetAccountId,
            AccumulatedDepreciationAccountId = category.AccumulatedDepreciationAccountId,
            DepreciationExpenseAccountId = category.DepreciationExpenseAccountId,
            GainOnDisposalAccountId = category.GainOnDisposalAccountId,
            LossOnDisposalAccountId = category.LossOnDisposalAccountId,
            RevaluationSurplusAccountId = category.RevaluationSurplusAccountId,
            AucAccountId = category.AucAccountId,
            CreatedAt = category.CreatedAt,
            CreatedBy = category.CreatedBy
        };
    }
}
