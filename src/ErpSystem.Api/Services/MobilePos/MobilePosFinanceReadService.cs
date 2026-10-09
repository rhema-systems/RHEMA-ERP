using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Procurement;
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

    Task<MobilePosCustomerChangePageDto> GetCustomerChangesAsync(
        string installationId,
        DateTime? sinceUtc,
        string? cursor,
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

    public async Task<MobilePosCustomerChangePageDto> GetCustomerChangesAsync(
        string installationId,
        DateTime? sinceUtc,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        var tenantId = TenantId;
        var take = Math.Clamp(limit, 1, 500);
        var state = string.IsNullOrWhiteSpace(cursor)
            ? MobilePosChangeCursorCodec.Create(tenantId, bootstrap.Store.Id, sinceUtc)
            : MobilePosChangeCursorCodec.Decode(cursor, tenantId, bootstrap.Store.Id);

        var customerRoles = _db.BusinessPartnerRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(role => role.TenantId == tenantId && role.RoleType == BusinessPartnerRoleType.Customer);
        var roleMarkers = customerRoles.Select(role => new
        {
            BusinessPartnerRoleId = role.Id,
            ChangedAtUtc = role.UpdatedAt ?? role.CreatedAt
        });
        var partnerMarkers = from role in customerRoles
            join partner in _db.BusinessPartners.IgnoreQueryFilters().AsNoTracking()
                on new { role.TenantId, Id = role.BusinessPartnerId }
                equals new { partner.TenantId, partner.Id }
            select new
            {
                BusinessPartnerRoleId = role.Id,
                ChangedAtUtc = partner.UpdatedAt ?? partner.CreatedAt
            };
        var profileMarkers = _db.BusinessPartnerArProfileVersions.IgnoreQueryFilters().AsNoTracking()
            .Where(profile => profile.TenantId == tenantId)
            .Select(profile => new
            {
                BusinessPartnerRoleId = profile.BusinessPartnerRoleId,
                ChangedAtUtc = profile.UpdatedAt ?? profile.CreatedAt
            });
        var markers = roleMarkers.Concat(partnerMarkers).Concat(profileMarkers);
        var pageMarkers = await markers
            .Where(marker => marker.ChangedAtUtc > state.SinceUtc
                && marker.ChangedAtUtc <= state.SnapshotAtUtc)
            .GroupBy(marker => marker.BusinessPartnerRoleId)
            .Select(group => new
            {
                BusinessPartnerRoleId = group.Key,
                ChangedAtUtc = group.Max(marker => marker.ChangedAtUtc)
            })
            .OrderBy(marker => marker.ChangedAtUtc)
            .ThenBy(marker => marker.BusinessPartnerRoleId)
            .Skip(state.Offset)
            .Take(take + 1)
            .ToListAsync(cancellationToken);
        var hasMore = pageMarkers.Count > take;
        var selectedMarkers = pageMarkers.Take(take).ToArray();
        var roleIds = selectedMarkers.Select(marker => marker.BusinessPartnerRoleId).ToArray();
        var eligibleRoles = await _db.EligibleMobilePosCustomerRoles(tenantId, state.SnapshotAtUtc)
            .AsNoTracking()
            .Include(role => role.BusinessPartner)
            .Where(role => roleIds.Contains(role.Id))
            .ToDictionaryAsync(role => role.Id, cancellationToken);
        var upserts = new List<MobilePosCustomerCacheItemDto>(eligibleRoles.Count);
        var tombstones = new List<Guid>();
        foreach (var marker in selectedMarkers)
        {
            if (!eligibleRoles.TryGetValue(marker.BusinessPartnerRoleId, out var role))
            {
                tombstones.Add(marker.BusinessPartnerRoleId);
                continue;
            }
            upserts.Add(new MobilePosCustomerCacheItemDto
            {
                BusinessPartnerId = role.BusinessPartnerId,
                BusinessPartnerRoleId = role.Id,
                Code = role.BusinessPartner.PartnerCode,
                Name = role.BusinessPartner.PartnerName,
                Email = role.BusinessPartner.PrimaryEmail,
                Phone = role.BusinessPartner.PrimaryPhone,
                CurrencyCode = string.IsNullOrWhiteSpace(role.BusinessPartner.Currency)
                    ? bootstrap.Store.CurrencyCode
                    : role.BusinessPartner.Currency,
                IsDefaultWalkInCustomer = role.BusinessPartnerId == bootstrap.Store.DefaultWalkInBusinessPartnerId
                    && role.Id == bootstrap.Store.DefaultWalkInBusinessPartnerRoleId,
                ChangedAtUtc = marker.ChangedAtUtc
            });
        }

        return new MobilePosCustomerChangePageDto
        {
            SnapshotAtUtc = state.SnapshotAtUtc,
            HasMore = hasMore,
            NextCursor = hasMore
                ? MobilePosChangeCursorCodec.Encode(state with { Offset = state.Offset + selectedMarkers.Length })
                : null,
            Upserts = upserts,
            TombstoneBusinessPartnerRoleIds = tombstones
        };
    }

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
