using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/facilities/budget")]
[Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
public sealed class FacilitiesBudgetController(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IBudgetService budgetService) : ControllerBase
{
    [HttpGet("fiscal-years")]
    public async Task<IActionResult> GetFiscalYears(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var years = await db.FiscalYears.AsNoTracking()
            .Where(year => year.TenantId == tenantId && !year.IsDeleted)
            .OrderByDescending(year => year.StartDate)
            .Select(year => new
            {
                year.Id,
                year.FiscalYearName,
                year.FiscalYearCode,
                year.StartDate,
                year.EndDate,
                HasOfficialBudget = db.BudgetScenarios.Any(scenario =>
                    scenario.TenantId == tenantId && scenario.FiscalYearId == year.Id
                    && !scenario.IsDeleted && scenario.IsActive)
            })
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = years });
    }

    [HttpGet("fiscal-years/{fiscalYearId:guid}")]
    public async Task<IActionResult> GetReport(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        if (!await db.FiscalYears.AsNoTracking().AnyAsync(year =>
                year.TenantId == tenantId && !year.IsDeleted && year.Id == fiscalYearId,
                cancellationToken))
            return NotFound();

        try
        {
            var official = await budgetService.GetActiveBudgetVsActualAsync(fiscalYearId);
            return Ok(new { success = true, data = FacilitiesBudgetProjection.FromOfficialBudget(official) });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "No official budget has been adopted for this fiscal year." });
        }
    }

    private Guid GetTenantId()
        => currentUser.TenantId is { } id && id != Guid.Empty
            ? id : throw new UnauthorizedAccessException("Tenant context is required.");
}
