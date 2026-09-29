using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/facilities/providers")]
[Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Officer,Estate Manager,Facilities Officer,Facilities Manager")]
public sealed class FacilitiesProviderOptionsController(
    ApplicationDbContext db,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId is { } id && id != Guid.Empty
            ? id : throw new UnauthorizedAccessException("Tenant context is required.");
        var now = DateTime.UtcNow;
        var providers = (await db.BusinessPartners.AsNoTracking()
            .Include(item => item.Categories).ThenInclude(item => item.Category)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && (item.PartnerType == "Supplier" || item.PartnerType == "Contractor" || item.PartnerType == "Both"))
            .OrderBy(item => item.PartnerName)
            .ToListAsync(cancellationToken))
            .Where(item => BusinessPartnerLifecyclePolicy.IsOperationallyApproved(item)
                && (!item.ComplianceValidUntilUtc.HasValue || item.ComplianceValidUntilUtc.Value >= now))
            .ToList();
        var ids = providers.Select(item => item.Id).ToList();
        var today = now.Date;
        var contracts = await db.Contracts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && ids.Contains(item.BusinessPartnerId) && item.Status == "Active"
                && (!item.StartDate.HasValue || item.StartDate.Value.Date <= today)
                && (!item.EndDate.HasValue || item.EndDate.Value.Date >= today))
            .OrderBy(item => item.ContractNumber)
            .Select(item => new
            {
                item.Id,
                item.BusinessPartnerId,
                item.ContractNumber,
                item.ContractTitle,
                item.ContractValue,
                item.Currency,
                item.PaymentTerms,
                item.StartDate,
                item.EndDate
            })
            .ToListAsync(cancellationToken);
        var results = providers.Select(item => new
        {
            item.Id,
            item.PartnerCode,
            item.PartnerName,
            item.PartnerType,
            item.PerformanceRating,
            Phone = item.PrimaryPhone,
            Email = item.PrimaryEmail,
            Categories = item.Categories.Where(link => link.Category != null && link.Category.IsActive)
                .Select(link => link.Category.CategoryName).OrderBy(name => name).ToList(),
            Contracts = contracts.Where(contract => contract.BusinessPartnerId == item.Id).ToList()
        }).ToList();

        return Ok(new { success = true, data = results });
    }

    [HttpGet("{id:guid}/invoices")]
    public async Task<IActionResult> GetInvoices(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId is { } tenant && tenant != Guid.Empty
            ? tenant : throw new UnauthorizedAccessException("Tenant context is required.");
        var providerExists = await db.BusinessPartners.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.Id == id && !item.IsDeleted,
            cancellationToken);
        if (!providerExists)
            return NotFound();

        var invoices = await db.VendorInvoices.AsNoTracking()
            .Where(invoice => invoice.TenantId == tenantId && !invoice.IsDeleted
                && invoice.BusinessPartnerId == id)
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .ThenByDescending(invoice => invoice.CreatedAt)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.SupplierInvoiceNumber,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.PurchaseOrderId,
                invoice.TotalAmount,
                invoice.PaidAmount,
                invoice.CurrencyCode,
                invoice.Status
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = invoices });
    }

    [HttpGet("{id:guid}/rates")]
    public async Task<IActionResult> GetRates(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId();
        if (!await db.BusinessPartners.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.Id == id && !item.IsDeleted, cancellationToken))
            return NotFound();

        var rates = await db.EstateFacilityProviderRates.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.BusinessPartnerId == id && !item.IsDeleted)
            .OrderBy(item => item.ServiceName).ThenByDescending(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);
        var contractIds = rates.Where(item => item.ContractId.HasValue)
            .Select(item => item.ContractId!.Value).Distinct().ToList();
        var contractNumbers = await db.Contracts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && contractIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.ContractNumber, cancellationToken);
        return Ok(new { success = true, data = rates.Select(item => RateView(item,
            item.ContractId is { } contractId && contractNumbers.TryGetValue(contractId, out var number)
                ? number : null)).ToList() });
    }

    [HttpPost("{id:guid}/rates")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Manager,Facilities Manager")]
    public async Task<IActionResult> CreateRate(Guid id, [FromBody] ProviderRateRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateRate(id, null, request, cancellationToken);
        if (validation is not null) return validation;

        var rate = new EstateFacilityProviderRate { TenantId = TenantId(), BusinessPartnerId = id };
        Apply(rate, request);
        db.EstateFacilityProviderRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = RateView(rate) });
    }

    [HttpPut("{id:guid}/rates/{rateId:guid}")]
    [Authorize(Roles = "admin,Admin,SystemAdmin,SuperAdmin,TenantAdmin,Estate Manager,Facilities Manager")]
    public async Task<IActionResult> UpdateRate(Guid id, Guid rateId, [FromBody] ProviderRateRequest request,
        CancellationToken cancellationToken)
    {
        var rate = await db.EstateFacilityProviderRates.FirstOrDefaultAsync(item =>
            item.TenantId == TenantId() && item.BusinessPartnerId == id
            && item.Id == rateId && !item.IsDeleted, cancellationToken);
        if (rate is null) return NotFound();
        var validation = await ValidateRate(id, rateId, request, cancellationToken);
        if (validation is not null) return validation;

        Apply(rate, request);
        rate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = RateView(rate) });
    }

    private Guid TenantId() => currentUser.TenantId is { } tenant && tenant != Guid.Empty
        ? tenant : throw new UnauthorizedAccessException("Tenant context is required.");

    private async Task<IActionResult?> ValidateRate(Guid providerId, Guid? rateId,
        ProviderRateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = TenantId();
        var provider = await db.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == providerId && !item.IsDeleted, cancellationToken);
        if (provider is null) return NotFound("Provider not found.");
        if (provider.PartnerType is not ("Supplier" or "Contractor" or "Both")
            || !BusinessPartnerLifecyclePolicy.IsOperationallyApproved(provider)
            || provider.ComplianceValidUntilUtc is { } expiry && expiry < DateTime.UtcNow)
            return BadRequest("Provider must be approved and compliant.");
        if (string.IsNullOrWhiteSpace(request.ServiceName) || request.ServiceName.Trim().Length > 160
            || string.IsNullOrWhiteSpace(request.UnitOfMeasure) || request.UnitOfMeasure.Trim().Length > 40
            || string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3
            || request.Rate <= 0 || request.Rate > 99999999999999.9999m
            || request.EffectiveFrom == default
            || request.EffectiveTo is { } end && end.Date < request.EffectiveFrom.Date)
            return BadRequest("Enter a service, unit, positive rate, three-letter currency, and valid dates.");

        if (request.ContractId is { } contractId)
        {
            var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.Id == contractId && !item.IsDeleted
                && item.BusinessPartnerId == providerId, cancellationToken);
            if (contract is null) return BadRequest("Contract does not belong to this provider.");
            if (request.IsActive && (contract.Status != "Active"
                || contract.StartDate is { } start && request.EffectiveFrom.Date < start.Date
                || contract.EndDate is { } contractEnd &&
                    (request.EffectiveTo?.Date ?? DateTime.MaxValue.Date) > contractEnd.Date))
                return BadRequest("Active rate dates must fall within an active provider contract.");
        }

        if (!request.IsActive) return null;
        var candidates = await db.EstateFacilityProviderRates.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.BusinessPartnerId == providerId
                && item.Id != rateId && !item.IsDeleted && item.IsActive
                && item.ContractId == request.ContractId)
            .ToListAsync(cancellationToken);
        if (candidates.Any(item =>
            string.Equals(item.ServiceName, request.ServiceName.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.UnitOfMeasure, request.UnitOfMeasure.Trim(), StringComparison.OrdinalIgnoreCase)
            && item.EffectiveFrom.Date <= (request.EffectiveTo?.Date ?? DateTime.MaxValue.Date)
            && request.EffectiveFrom.Date <= (item.EffectiveTo?.Date ?? DateTime.MaxValue.Date)))
            return Conflict("An active rate for this service, unit, and contract already covers these dates.");
        return null;
    }

    private static void Apply(EstateFacilityProviderRate rate, ProviderRateRequest request)
    {
        rate.ContractId = request.ContractId;
        rate.ServiceName = request.ServiceName.Trim();
        rate.UnitOfMeasure = request.UnitOfMeasure.Trim();
        rate.Rate = request.Rate;
        rate.Currency = request.Currency.Trim().ToUpperInvariant();
        rate.EffectiveFrom = request.EffectiveFrom.Date;
        rate.EffectiveTo = request.EffectiveTo?.Date;
        rate.IsActive = request.IsActive;
    }

    private static object RateView(EstateFacilityProviderRate rate, string? contractNumber = null) => new
    {
        rate.Id, rate.BusinessPartnerId, rate.ContractId, ContractNumber = contractNumber, rate.ServiceName,
        rate.UnitOfMeasure, rate.Rate, rate.Currency, rate.EffectiveFrom,
        rate.EffectiveTo, rate.IsActive
    };
}

public sealed record ProviderRateRequest(Guid? ContractId, string ServiceName,
    string UnitOfMeasure, decimal Rate, string Currency, DateTime EffectiveFrom,
    DateTime? EffectiveTo, bool IsActive);
