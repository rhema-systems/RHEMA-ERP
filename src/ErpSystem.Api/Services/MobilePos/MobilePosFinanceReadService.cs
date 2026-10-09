using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosFinanceReadService
{
    Task<IReadOnlyList<MobilePosCustomerSearchResultDto>> SearchCustomersAsync(
        string installationId,
        string? search,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(
        string installationId,
        Guid businessPartnerId,
        Guid businessPartnerRoleId,
        CancellationToken cancellationToken);
}

public sealed class MobilePosFinanceReadService : IMobilePosFinanceReadService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMobilePosFoundationService _foundation;
    private readonly IPaymentService _payments;

    public MobilePosFinanceReadService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IMobilePosFoundationService foundation,
        IPaymentService payments)
    {
        _db = db;
        _currentUser = currentUser;
        _foundation = foundation;
        _payments = payments;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    public async Task<IReadOnlyList<MobilePosCustomerSearchResultDto>> SearchCustomersAsync(
        string installationId,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 50)
            throw new InvalidOperationException("Customer search limit must be between 1 and 50.");

        var term = search?.Trim() ?? string.Empty;
        if (term.Length == 1)
            throw new InvalidOperationException("Enter at least two characters to search approved customers.");

        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var tenantId = TenantId;
        var query = _db.EligibleMobilePosCustomerRoles(tenantId, DateTime.UtcNow).AsNoTracking();

        if (term.Length == 0)
        {
            query = query.Where(role => role.Id == bootstrap.Store.DefaultWalkInBusinessPartnerRoleId &&
                                        role.BusinessPartnerId == bootstrap.Store.DefaultWalkInBusinessPartnerId);
        }
        else
        {
            var normalized = term.ToUpper();
            query = query.Where(role =>
                role.BusinessPartner.PartnerCode.ToUpper().Contains(normalized) ||
                role.BusinessPartner.PartnerName.ToUpper().Contains(normalized) ||
                (role.BusinessPartner.LegalName != null && role.BusinessPartner.LegalName.ToUpper().Contains(normalized)) ||
                (role.BusinessPartner.PrimaryEmail != null && role.BusinessPartner.PrimaryEmail.ToUpper().Contains(normalized)) ||
                (role.BusinessPartner.PrimaryPhone != null && role.BusinessPartner.PrimaryPhone.Contains(term)));
        }

        return await query
            .OrderByDescending(role => role.Id == bootstrap.Store.DefaultWalkInBusinessPartnerRoleId)
            .ThenBy(role => role.BusinessPartner.PartnerName)
            .Take(limit)
            .Select(role => new MobilePosCustomerSearchResultDto
            {
                BusinessPartnerId = role.BusinessPartnerId,
                BusinessPartnerRoleId = role.Id,
                Code = role.BusinessPartner.PartnerCode,
                Name = role.BusinessPartner.PartnerName,
                Email = role.BusinessPartner.PrimaryEmail,
                Phone = role.BusinessPartner.PrimaryPhone,
                CurrencyCode = role.BusinessPartner.Currency == null || role.BusinessPartner.Currency == string.Empty
                    ? bootstrap.Store.CurrencyCode
                    : role.BusinessPartner.Currency,
                IsDefaultWalkInCustomer = role.BusinessPartnerId == bootstrap.Store.DefaultWalkInBusinessPartnerId &&
                                          role.Id == bootstrap.Store.DefaultWalkInBusinessPartnerRoleId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(
        string installationId,
        Guid businessPartnerId,
        Guid businessPartnerRoleId,
        CancellationToken cancellationToken)
    {
        await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var eligible = await _db.EligibleMobilePosCustomerRoles(TenantId, DateTime.UtcNow)
            .AsNoTracking()
            .AnyAsync(role => role.BusinessPartnerId == businessPartnerId && role.Id == businessPartnerRoleId,
                cancellationToken);
        if (!eligible)
            throw new KeyNotFoundException("The selected customer is not active, approved, and transaction ready.");

        return await _payments.GetOutstandingInvoicesAsync(businessPartnerId, cancellationToken);
    }
}
