using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
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

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;

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
            AucAccounts = (aucAccounts.Count > 0 ? aucAccounts : assetAccounts)
                .OrderBy(a => a.AccountNumber)
                .ToList()
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<FixedAssetCategoryDto>> Create(CreateFixedAssetCategoryDto dto)
    {
        var result = await _categoryService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
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
