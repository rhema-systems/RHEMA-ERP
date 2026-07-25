using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class FixedAssetCategoryService : IFixedAssetCategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;

    public FixedAssetCategoryService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
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
        await ValidateCategoryDtoAsync(dto);

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
            DisposalProceedsClearingAccountId = dto.DisposalProceedsClearingAccountId,
            RevaluationSurplusAccountId = dto.RevaluationSurplusAccountId,
            RevaluationLossAccountId = dto.RevaluationLossAccountId,
            ImpairmentLossAccountId = dto.ImpairmentLossAccountId,
            AccumulatedImpairmentAccountId = dto.AccumulatedImpairmentAccountId,
            ImpairmentReversalAccountId = dto.ImpairmentReversalAccountId,
            AucAccountId = dto.AucAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.FixedAssetCategories.Add(category);
        await _context.SaveChangesAsync();
        await RecordCategoryAuditAsync(
            FinanceAuditEvents.FixedAssetCategoryCreated,
            category,
            afterValues: new { category.Code, category.Name },
            comment: "Fixed asset category created.");
        await RecordCategoryAuditAsync(
            FinanceAuditEvents.FixedAssetCategoryAccountMappingChanged,
            category,
            afterValues: BuildAccountMappingSnapshot(category),
            comment: "Fixed asset category account mappings configured.");

        return await GetByIdAsync(category.Id) ?? throw new InvalidOperationException("Failed to create category.");
    }

    public async Task<FixedAssetCategoryDto> UpdateAsync(Guid id, UpdateFixedAssetCategoryDto dto)
    {
        var category = await _context.FixedAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == id)
            ?? throw new KeyNotFoundException("Fixed asset category not found.");

        await ValidateCategoryDtoAsync(dto, id);
        var beforeMapping = BuildAccountMappingSnapshot(category);

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
        category.DisposalProceedsClearingAccountId = dto.DisposalProceedsClearingAccountId;
        category.RevaluationSurplusAccountId = dto.RevaluationSurplusAccountId;
        category.RevaluationLossAccountId = dto.RevaluationLossAccountId;
        category.ImpairmentLossAccountId = dto.ImpairmentLossAccountId;
        category.AccumulatedImpairmentAccountId = dto.AccumulatedImpairmentAccountId;
        category.ImpairmentReversalAccountId = dto.ImpairmentReversalAccountId;
        category.AucAccountId = dto.AucAccountId;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = UserName;

        await _context.SaveChangesAsync();
        await RecordCategoryAuditAsync(
            FinanceAuditEvents.FixedAssetCategoryUpdated,
            category,
            afterValues: new { category.Code, category.Name },
            comment: "Fixed asset category updated.");

        var afterMapping = BuildAccountMappingSnapshot(category);
        if (!Equals(beforeMapping, afterMapping))
        {
            await RecordCategoryAuditAsync(
                FinanceAuditEvents.FixedAssetCategoryAccountMappingChanged,
                category,
                beforeValues: beforeMapping,
                afterValues: afterMapping,
                comment: "Fixed asset category account mappings changed.");
        }

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
            DisposalProceedsClearingAccountId = category.DisposalProceedsClearingAccountId,
            RevaluationSurplusAccountId = category.RevaluationSurplusAccountId,
            RevaluationLossAccountId = category.RevaluationLossAccountId,
            ImpairmentLossAccountId = category.ImpairmentLossAccountId,
            AccumulatedImpairmentAccountId = category.AccumulatedImpairmentAccountId,
            ImpairmentReversalAccountId = category.ImpairmentReversalAccountId,
            AucAccountId = category.AucAccountId,
            CreatedAt = category.CreatedAt,
            CreatedBy = category.CreatedBy
        };
    }

    private Task ValidateCategoryDtoAsync(CreateFixedAssetCategoryDto dto, Guid? existingCategoryId = null)
        => ValidateCategoryValuesAsync(
            dto.Name,
            dto.Code,
            dto.AssetAccountId,
            dto.AccumulatedDepreciationAccountId,
            dto.DepreciationExpenseAccountId,
            dto.GainOnDisposalAccountId,
            dto.LossOnDisposalAccountId,
            dto.DisposalProceedsClearingAccountId,
            dto.RevaluationSurplusAccountId,
            dto.RevaluationLossAccountId,
            dto.ImpairmentLossAccountId,
            dto.AccumulatedImpairmentAccountId,
            dto.ImpairmentReversalAccountId,
            dto.AucAccountId,
            existingCategoryId);

    private Task ValidateCategoryDtoAsync(UpdateFixedAssetCategoryDto dto, Guid? existingCategoryId = null)
        => ValidateCategoryValuesAsync(
            dto.Name,
            dto.Code,
            dto.AssetAccountId,
            dto.AccumulatedDepreciationAccountId,
            dto.DepreciationExpenseAccountId,
            dto.GainOnDisposalAccountId,
            dto.LossOnDisposalAccountId,
            dto.DisposalProceedsClearingAccountId,
            dto.RevaluationSurplusAccountId,
            dto.RevaluationLossAccountId,
            dto.ImpairmentLossAccountId,
            dto.AccumulatedImpairmentAccountId,
            dto.ImpairmentReversalAccountId,
            dto.AucAccountId,
            existingCategoryId);

    private async Task ValidateCategoryValuesAsync(
        string name,
        string code,
        Guid assetAccountId,
        Guid accumulatedDepreciationAccountId,
        Guid depreciationExpenseAccountId,
        Guid? gainOnDisposalAccountId,
        Guid? lossOnDisposalAccountId,
        Guid? disposalProceedsClearingAccountId,
        Guid? revaluationSurplusAccountId,
        Guid? revaluationLossAccountId,
        Guid? impairmentLossAccountId,
        Guid? accumulatedImpairmentAccountId,
        Guid? impairmentReversalAccountId,
        Guid? aucAccountId,
        Guid? existingCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Fixed asset category name is required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Fixed asset category code is required.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var duplicateCode = await _context.FixedAssetCategories.AnyAsync(c =>
            c.TenantId == TenantId &&
            c.Code.ToUpper() == normalizedCode &&
            (!existingCategoryId.HasValue || c.Id != existingCategoryId.Value));
        if (duplicateCode)
        {
            throw new InvalidOperationException($"Fixed asset category code '{code}' already exists.");
        }

        await ResolveCategoryAccountAsync(assetAccountId, "fixed asset cost account", AccountType.Asset);
        await ResolveCategoryAccountAsync(accumulatedDepreciationAccountId, "accumulated depreciation account", AccountType.Asset);
        await ResolveCategoryAccountAsync(depreciationExpenseAccountId, "depreciation expense account", AccountType.Expense);
        if (gainOnDisposalAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(gainOnDisposalAccountId.Value, "gain on disposal account", AccountType.Expense);
        }

        if (lossOnDisposalAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(lossOnDisposalAccountId.Value, "loss on disposal account", AccountType.Expense);
        }

        if (disposalProceedsClearingAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(disposalProceedsClearingAccountId.Value, "disposal proceeds clearing account", AccountType.Asset);
        }

        if (revaluationSurplusAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(revaluationSurplusAccountId.Value, "revaluation surplus account", AccountType.Equity);
        }

        if (revaluationLossAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(revaluationLossAccountId.Value, "revaluation loss account", AccountType.Expense);
        }

        if (impairmentLossAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(impairmentLossAccountId.Value, "impairment loss account", AccountType.Expense);
        }

        if (accumulatedImpairmentAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(accumulatedImpairmentAccountId.Value, "accumulated impairment account", AccountType.Asset);
        }

        if (impairmentReversalAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(impairmentReversalAccountId.Value, "impairment reversal account", AccountType.Revenue, AccountType.Expense);
        }

        if (aucAccountId.HasValue)
        {
            await ResolveCategoryAccountAsync(aucAccountId.Value, "AUC/CIP clearing account", AccountType.Asset);
        }
    }

    private async Task<Account> ResolveCategoryAccountAsync(Guid accountId, string role, params AccountType[] allowedTypes)
    {
        if (accountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Fixed asset category {role} is required.");
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == accountId && !a.IsDeleted);

        if (account == null)
        {
            throw new InvalidOperationException($"Fixed asset category {role} was not found for this tenant.");
        }

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"Fixed asset category {role} account '{account.AccountNumber}' is not active.");
        }

        if (!account.AllowDirectPosting)
        {
            throw new InvalidOperationException($"Fixed asset category {role} account '{account.AccountNumber}' does not allow posting.");
        }

        if (allowedTypes.Length > 0 && !allowedTypes.Contains(account.AccountType))
        {
            throw new InvalidOperationException($"Fixed asset category {role} account '{account.AccountNumber}' has an invalid account type.");
        }

        return account;
    }

    private static object BuildAccountMappingSnapshot(FixedAssetCategory category)
        => new
        {
            category.AssetAccountId,
            category.AccumulatedDepreciationAccountId,
            category.DepreciationExpenseAccountId,
            category.GainOnDisposalAccountId,
            category.LossOnDisposalAccountId,
            category.DisposalProceedsClearingAccountId,
            category.RevaluationSurplusAccountId,
            category.RevaluationLossAccountId,
            category.ImpairmentLossAccountId,
            category.AccumulatedImpairmentAccountId,
            category.ImpairmentReversalAccountId,
            category.AucAccountId
        };

    private async Task RecordCategoryAuditAsync(
        string eventType,
        FixedAssetCategory category,
        object? beforeValues = null,
        object? afterValues = null,
        string? comment = null)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = category.TenantId,
            SourceModule = "FA",
            SourceDocumentType = "FixedAssetCategory",
            SourceDocumentId = category.Id,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Comment = comment,
            Resource = "Finance.FixedAssetCategory",
            ResourceId = category.Id.ToString()
        });
    }
}
