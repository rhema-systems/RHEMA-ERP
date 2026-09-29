using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using ErpSystem.Data;
using ErpSystem.Api.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/fixed-asset-categories")]
public class FixedAssetCategoriesController : ControllerBase
{
    private readonly IFixedAssetCategoryService _categoryService;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FixedAssetCategoriesController(
        IFixedAssetCategoryService categoryService,
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _categoryService = categoryService;
        _context = context;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FixedAssetCategoryDto>>> GetAll()
    {
        var result = await _categoryService.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FixedAssetCategoryDto>> GetById(Guid id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category == null) return NotFound();
        return Ok(category);
    }

    /// <summary>
    /// Finance-owned, read-only integration feed for Maintenance. Only assets whose category is
    /// explicitly marked as requiring maintenance are returned; no financial amounts or GL
    /// mappings are disclosed.
    /// </summary>
    [HttpGet("maintenance-assets")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceEligibleFixedAssetDto>>> GetMaintenanceAssets(
        CancellationToken cancellationToken)
        => Ok(await _categoryService.GetMaintenanceEligibleAssetsAsync(cancellationToken));

    [HttpGet("gl-accounts")]
    public async Task<ActionResult<FixedAssetGlAccountOptionsDto>> GetGlAccounts()
    {
        var accounts = await _context.Accounts
            .Where(a => a.TenantId == TenantId && a.Status == AccountStatus.Active)
            .Select(a => new FixedAssetGlAccountOptionDto
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber,
                AccountName = a.AccountName,
                AccountType = a.AccountType.ToString(),
                AccountCategory = a.AccountCategory,
                AccountSubCategory = a.AccountSubCategory
            })
            .ToListAsync();

        var assetAccounts = accounts
            .Where(a => a.AccountType == AccountType.Asset.ToString())
            .ToList();
        var expenseAccounts = accounts
            .Where(a => a.AccountType == AccountType.Expense.ToString())
            .ToList();
        var revenueAccounts = accounts
            .Where(a => a.AccountType == AccountType.Revenue.ToString())
            .ToList();

        var fixedAssetAccounts = assetAccounts
            .Where(a =>
                (a.AccountCategory ?? string.Empty).Contains("Fixed", StringComparison.OrdinalIgnoreCase) ||
                (a.AccountSubCategory ?? string.Empty).Contains("Fixed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var accumulatedDepAccounts = assetAccounts
            .Where(a =>
                a.AccountName.Contains("Accumulated", StringComparison.OrdinalIgnoreCase) &&
                a.AccountName.Contains("Depreci", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var depreciationExpenseAccounts = expenseAccounts
            .Where(a => a.AccountName.Contains("Depreci", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var equityAccounts = accounts
            .Where(a => a.AccountType == AccountType.Equity.ToString())
            .ToList();

        var revaluationSurplusAccounts = equityAccounts
            .Where(a =>
                (a.AccountName ?? string.Empty).Contains("Revaluation", StringComparison.OrdinalIgnoreCase) ||
                (a.AccountName ?? string.Empty).Contains("Surplus", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var revaluationLossAccounts = expenseAccounts
            .Where(a =>
                (a.AccountName ?? string.Empty).Contains("Revaluation", StringComparison.OrdinalIgnoreCase) ||
                (a.AccountName ?? string.Empty).Contains("Loss", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var impairmentLossAccounts = expenseAccounts
            .Where(a =>
                (a.AccountName ?? string.Empty).Contains("Impair", StringComparison.OrdinalIgnoreCase) ||
                (a.AccountName ?? string.Empty).Contains("Loss", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var accumulatedImpairmentAccounts = assetAccounts
            .Where(a =>
                (a.AccountName ?? string.Empty).Contains("Accumulated", StringComparison.OrdinalIgnoreCase) &&
                (a.AccountName ?? string.Empty).Contains("Impair", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var impairmentReversalAccounts = revenueAccounts
            .Where(a =>
                (a.AccountName ?? string.Empty).Contains("Impair", StringComparison.OrdinalIgnoreCase) ||
                (a.AccountName ?? string.Empty).Contains("Reversal", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var aucAccounts = assetAccounts
            .Where(a =>
                (a.AccountName ?? string.Empty).Contains("Construction", StringComparison.OrdinalIgnoreCase) ||
                (a.AccountName ?? string.Empty).Contains("AUC", StringComparison.OrdinalIgnoreCase) ||
                (a.AccountName ?? string.Empty).Contains("CIP", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = new FixedAssetGlAccountOptionsDto
        {
            AssetAccounts = (fixedAssetAccounts.Count > 0 ? fixedAssetAccounts : assetAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            AccumulatedDepreciationAccounts = (accumulatedDepAccounts.Count > 0 ? accumulatedDepAccounts : assetAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            DepreciationExpenseAccounts = (depreciationExpenseAccounts.Count > 0 ? depreciationExpenseAccounts : expenseAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            GainOnDisposalAccounts = (revenueAccounts.Count > 0 ? revenueAccounts : expenseAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            LossOnDisposalAccounts = expenseAccounts
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            RevaluationSurplusAccounts = (revaluationSurplusAccounts.Count > 0 ? revaluationSurplusAccounts : equityAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            RevaluationLossAccounts = (revaluationLossAccounts.Count > 0 ? revaluationLossAccounts : expenseAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            ImpairmentLossAccounts = (impairmentLossAccounts.Count > 0 ? impairmentLossAccounts : expenseAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            AccumulatedImpairmentAccounts = (accumulatedImpairmentAccounts.Count > 0 ? accumulatedImpairmentAccounts : assetAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            ImpairmentReversalAccounts = (impairmentReversalAccounts.Count > 0 ? impairmentReversalAccounts : revenueAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList(),
            AucAccounts = (aucAccounts.Count > 0 ? aucAccounts : assetAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList()
        };

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = FinancePermissions.ConfigureFixedAssetCategoriesPolicy)]
    public async Task<ActionResult<FixedAssetCategoryDto>> Create(CreateFixedAssetCategoryDto dto)
    {
        var result = await _categoryService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = FinancePermissions.ConfigureFixedAssetCategoriesPolicy)]
    public async Task<ActionResult<FixedAssetCategoryDto>> Update(Guid id, UpdateFixedAssetCategoryDto dto)
    {
        try
        {
            var result = await _categoryService.UpdateAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = FinancePermissions.ConfigureFixedAssetCategoriesPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _categoryService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
