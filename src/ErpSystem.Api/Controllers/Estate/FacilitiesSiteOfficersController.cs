using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/facilities/sites")]
[Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
public sealed class FacilitiesSiteOfficersController(
    ApplicationDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPut("{assetId:guid}/responsible-officer")]
    public async Task<IActionResult> AssignResponsibleOfficer(
        Guid assetId, [FromBody] AssignFacilitiesSiteOfficerRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId is { } id && id != Guid.Empty
            ? id : throw new UnauthorizedAccessException("Tenant context is required.");
        var asset = await db.EstateManagedAssets.FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == assetId && !item.IsDeleted,
            cancellationToken);
        if (asset is null) return NotFound("The Estate site was not found.");
        if (asset.AssetType == EstateManagedAssetType.Land)
            return BadRequest("A responsible Facilities officer can be assigned only to a property or facility site.");

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == request.EmployeeId
                && !item.IsDeleted && item.IsActive, cancellationToken);
        if (employee is null) return BadRequest("Select an active HR employee as the responsible officer.");

        asset.ResponsibleOfficerEmployeeId = employee.Id;
        asset.ResponsibleOfficerEmployeeNumber = employee.EmployeeNumber;
        asset.ResponsibleOfficerName = string.Join(" ",
            new[] { employee.FirstName, employee.MiddleName, employee.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = currentUser.UserName ?? "System";
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            asset.Id, asset.ResponsibleOfficerEmployeeId,
            asset.ResponsibleOfficerEmployeeNumber, asset.ResponsibleOfficerName
        });
    }
}

public sealed record AssignFacilitiesSiteOfficerRequest(Guid EmployeeId);
