using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>
/// Finance-owned AP identity bridge. BusinessPartner remains the shared supplier master. AP's
/// historical transaction model still requires Supplier.Id, so a purpose-authorized AP command
/// may materialize that internal projection from an approved BusinessPartner and record the
/// durable pairing. Name-only matching is deliberately prohibited because names are mutable and
/// non-unique.
/// </summary>
public sealed class ApSupplierIdentityService : IApSupplierIdentityService
{
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ApSupplierIdentityService(
        ApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";
    private Guid? CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : null;

    public async Task<ApSupplierIdentityDto> LookupAsync(
        Guid businessPartnerOrSupplierId,
        CancellationToken cancellationToken = default)
    {
        var existing = await LinkQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(link =>
                link.BusinessPartnerId == businessPartnerOrSupplierId ||
                link.SupplierId == businessPartnerOrSupplierId,
                cancellationToken);
        if (existing != null)
        {
            EnsureEligibleLink(existing);
            return Map(existing);
        }

        // GET remains side-effect free. An exact-id/code candidate can be shown to the UI, while
        // the purpose-authorized debit-note/payment command creates the durable Finance link.
        var partner = await _db.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == businessPartnerOrSupplierId &&
            !item.IsDeleted && item.IsActive,
            cancellationToken);
        if (partner != null)
        {
            EnsureApEligiblePartner(partner);
            var suppliers = await ExactSupplierCandidates(partner.Id, partner.PartnerCode)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return MapCandidate(partner, RequireSingleCandidate(suppliers, partner.PartnerCode, "Business Partner"));
        }

        var supplier = await _db.Suppliers.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == businessPartnerOrSupplierId &&
            !item.IsDeleted && item.IsActive,
            cancellationToken)
            ?? throw new KeyNotFoundException("Neither an AP supplier nor supplier business partner was found for this tenant.");
        var partners = await ExactPartnerCandidates(supplier.Id, supplier.SupplierCode)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return MapCandidate(RequireSingleCandidate(partners, supplier.SupplierCode, "AP Supplier"), supplier);
    }

    public async Task<ApSupplierIdentityDto> ResolveAsync(
        Guid businessPartnerOrSupplierId,
        CancellationToken cancellationToken = default)
    {
        var existing = await LinkQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(link =>
                link.BusinessPartnerId == businessPartnerOrSupplierId ||
                link.SupplierId == businessPartnerOrSupplierId,
                cancellationToken);
        if (existing != null)
        {
            EnsureEligibleLink(existing);
            return Map(existing);
        }

        var isBusinessPartner = await _db.BusinessPartners.AsNoTracking().AnyAsync(item =>
            item.TenantId == TenantId && item.Id == businessPartnerOrSupplierId && !item.IsDeleted,
            cancellationToken);
        if (isBusinessPartner)
            return await ResolveByBusinessPartnerAsync(businessPartnerOrSupplierId, cancellationToken);

        return await ResolveBySupplierAsync(businessPartnerOrSupplierId, cancellationToken);
    }

    public Task<ApSupplierIdentityDto> ResolveByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default) =>
        ResolveByBusinessPartnerAsync(businessPartnerId, cancellationToken, executionStrategyScope: false);

    private async Task<ApSupplierIdentityDto> ResolveByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken,
        bool executionStrategyScope)
    {
        if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(
                () => ResolveByBusinessPartnerAsync(businessPartnerId, cancellationToken, executionStrategyScope: true),
                cancellationToken);
        }

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                ApSettlementLockKeys.SupplierIdentity(TenantId, businessPartnerId), cancellationToken);

            var existing = await LinkQuery().SingleOrDefaultAsync(
                link => link.BusinessPartnerId == businessPartnerId,
                cancellationToken);
            if (existing != null)
            {
                EnsureEligibleLink(existing);
                if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
                return Map(existing);
            }

            var partner = await _db.BusinessPartners.SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == businessPartnerId &&
                !item.IsDeleted && item.IsActive,
                cancellationToken)
                ?? throw new KeyNotFoundException("Supplier business partner was not found for this tenant.");
            EnsureApEligiblePartner(partner);

            var candidates = await ExactSupplierCandidates(partner.Id, partner.PartnerCode)
                .OrderBy(item => item.Id == partner.Id ? 0 : 1)
                .ToListAsync(cancellationToken);
            if (candidates.Count > 1)
                throw new InvalidOperationException(
                    $"Business Partner '{partner.PartnerCode}' matches multiple AP masters. Finance will not guess an identity; resolve the duplicate master data first.");

            var supplier = candidates.SingleOrDefault();
            var mappingSource = supplier == null
                ? "BusinessPartnerProjection"
                : supplier.Id == partner.Id ? "SharedId" : "ExactCode";
            if (supplier == null)
            {
                supplier = BuildFinanceSupplierProjection(partner);
                _db.Suppliers.Add(supplier);
            }

            var link = BuildLink(partner, supplier, mappingSource);
            _db.ApSupplierIdentityLinks.Add(link);
            await _db.SaveChangesAsync(cancellationToken);
            if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
            return Map(link);
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task<ApSupplierIdentityDto> ResolveBySupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default) =>
        ResolveBySupplierAsync(supplierId, cancellationToken, executionStrategyScope: false);

    private async Task<ApSupplierIdentityDto> ResolveBySupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken,
        bool executionStrategyScope)
    {
        if (!_unitOfWork.HasActiveTransaction && !executionStrategyScope)
        {
            return await _unitOfWork.ExecuteInStrategyAsync(
                () => ResolveBySupplierAsync(supplierId, cancellationToken, executionStrategyScope: true),
                cancellationToken);
        }

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            await _unitOfWork.AcquireTransactionLockAsync(
                ApSettlementLockKeys.SupplierIdentity(TenantId, supplierId), cancellationToken);

            var existing = await LinkQuery().SingleOrDefaultAsync(
                link => link.SupplierId == supplierId,
                cancellationToken);
            if (existing != null)
            {
                EnsureEligibleLink(existing);
                if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
                return Map(existing);
            }

            var supplier = await _db.Suppliers.SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == supplierId &&
                !item.IsDeleted && item.IsActive,
                cancellationToken)
                ?? throw new KeyNotFoundException("AP supplier was not found for this tenant.");
            var candidates = await ExactPartnerCandidates(supplier.Id, supplier.SupplierCode)
                .OrderBy(item => item.Id == supplier.Id ? 0 : 1)
                .ToListAsync(cancellationToken);
            var partner = RequireSingleCandidate(candidates, supplier.SupplierCode, "AP Supplier");
            EnsureApEligiblePartner(partner);
            var link = BuildLink(partner, supplier, supplier.Id == partner.Id ? "SharedId" : "ExactCode");
            _db.ApSupplierIdentityLinks.Add(link);
            await _db.SaveChangesAsync(cancellationToken);
            if (ownsTransaction) await _unitOfWork.CommitAsync(cancellationToken);
            return Map(link);
        }
        catch
        {
            if (ownsTransaction && _unitOfWork.HasActiveTransaction)
                await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private IQueryable<ApSupplierIdentityLink> LinkQuery() => _db.ApSupplierIdentityLinks
        .IgnoreQueryFilters()
        .Include(link => link.BusinessPartner)
        .Include(link => link.Supplier)
        .Where(link => link.TenantId == TenantId && !link.IsDeleted);

    private IQueryable<Supplier> ExactSupplierCandidates(Guid partnerId, string? partnerCode) =>
        _db.Suppliers.Where(item =>
            item.TenantId == TenantId &&
            !item.IsDeleted &&
            item.IsActive &&
            (item.Id == partnerId ||
             (!string.IsNullOrWhiteSpace(partnerCode) && item.SupplierCode == partnerCode)));

    private IQueryable<BusinessPartner> ExactPartnerCandidates(Guid supplierId, string? supplierCode) =>
        _db.BusinessPartners.Where(item =>
            item.TenantId == TenantId &&
            !item.IsDeleted &&
            item.IsActive &&
            !item.IsBlacklisted &&
            item.ApprovalStatus == BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus &&
            (item.RegistrationStatus == BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
             item.RegistrationStatus == BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus) &&
            (item.PartnerType == "Supplier" ||
             item.PartnerType == "Contractor" ||
             item.PartnerType == "Both") &&
            (item.Id == supplierId ||
             (!string.IsNullOrWhiteSpace(supplierCode) && item.PartnerCode == supplierCode)));

    private ApSupplierIdentityLink BuildLink(BusinessPartner partner, Supplier supplier, string source) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        BusinessPartnerId = partner.Id,
        SupplierId = supplier.Id,
        MappingSource = source,
        IsVerified = string.Equals(source, "SharedId", StringComparison.Ordinal),
        VerifiedAtUtc = string.Equals(source, "SharedId", StringComparison.Ordinal) ? DateTime.UtcNow : null,
        VerifiedById = string.Equals(source, "SharedId", StringComparison.Ordinal) ? CurrentUserId : null,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = CurrentUserId,
        BusinessPartner = partner,
        Supplier = supplier
    };

    private static void EnsureApEligiblePartner(BusinessPartner partner)
    {
        if (!string.Equals(partner.PartnerType, "Supplier", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(partner.PartnerType, "Contractor", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(partner.PartnerType, "Both", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only active Supplier, Contractor or Both business partners can be paired to an AP supplier.");
        }

        if (!BusinessPartnerLifecyclePolicy.IsOperationallyApproved(partner))
            throw new InvalidOperationException(
                "Only approved, active and non-blacklisted supplier business partners can be used by Accounts Payable.");
    }

    private static void EnsureEligibleLink(ApSupplierIdentityLink link)
    {
        var partner = link.BusinessPartner
            ?? throw new InvalidOperationException("The AP supplier identity link has no Business Partner lineage.");
        var supplier = link.Supplier
            ?? throw new InvalidOperationException("The AP supplier identity link has no Supplier lineage.");
        if (partner.TenantId != link.TenantId || supplier.TenantId != link.TenantId ||
            partner.IsDeleted)
        {
            throw new InvalidOperationException(
                "The AP supplier identity link does not have active same-tenant lineage.");
        }
        EnsureApEligiblePartner(partner);
        if (supplier.IsDeleted || !supplier.IsActive || supplier.IsBlacklisted ||
            !string.Equals(supplier.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only an active, non-blacklisted AP supplier can be used by Accounts Payable.");
        }
    }

    private Supplier BuildFinanceSupplierProjection(BusinessPartner partner) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        SupplierCode = string.IsNullOrWhiteSpace(partner.PartnerCode)
            ? $"BP-{partner.Id.ToString("N")[..8].ToUpperInvariant()}"
            : partner.PartnerCode.Trim(),
        Name = partner.PartnerName,
        SupplierType = "Vendor",
        Address = partner.PhysicalAddress ?? partner.MailingAddress,
        City = partner.PhysicalCity ?? partner.MailingCity,
        State = partner.PhysicalState ?? partner.MailingState,
        Country = partner.PhysicalCountry ?? partner.MailingCountry,
        ZipCode = partner.PhysicalPostalCode ?? partner.MailingPostalCode,
        Phone = partner.PrimaryPhone,
        Email = partner.PrimaryEmail,
        Website = partner.Website,
        PrimaryContactName = partner.PrimaryContactName,
        PrimaryContactTitle = partner.PrimaryContactTitle,
        PrimaryContactPhone = partner.PrimaryPhone,
        PrimaryContactEmail = partner.PrimaryEmail,
        TaxId = partner.TaxIdentificationNumber ?? partner.VATNumber,
        PaymentTerms = partner.PaymentTerms ?? "Net 30",
        PaymentTermId = partner.PaymentTermId,
        IsActive = true,
        IsPreferred = partner.IsPreferred,
        Status = "Active",
        Notes = $"Finance AP projection of approved Business Partner {partner.PartnerCode}.",
        CreatedAt = DateTime.UtcNow,
        CreatedBy = UserName,
        CreatedById = CurrentUserId
    };

    private static T RequireSingleCandidate<T>(IReadOnlyList<T> candidates, string? code, string sourceType)
    {
        if (candidates.Count == 1)
            return candidates[0];
        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"{sourceType} '{code}' has no unambiguous paired AP master. Complete the Finance supplier identity pairing before settlement.");
        throw new InvalidOperationException(
            $"{sourceType} '{code}' matches multiple AP masters. Finance will not guess an identity; resolve the duplicate master data first.");
    }

    private static ApSupplierIdentityDto Map(ApSupplierIdentityLink link) => new()
    {
        BusinessPartnerId = link.BusinessPartnerId,
        SupplierId = link.SupplierId,
        PartnerCode = link.BusinessPartner?.PartnerCode ?? string.Empty,
        SupplierCode = link.Supplier?.SupplierCode ?? string.Empty,
        DisplayName = link.BusinessPartner?.PartnerName ?? link.Supplier?.Name ?? string.Empty,
        IsVerified = link.IsVerified
    };

    private static ApSupplierIdentityDto MapCandidate(BusinessPartner partner, Supplier supplier) => new()
    {
        BusinessPartnerId = partner.Id,
        SupplierId = supplier.Id,
        PartnerCode = partner.PartnerCode ?? string.Empty,
        SupplierCode = supplier.SupplierCode ?? string.Empty,
        DisplayName = partner.PartnerName ?? supplier.Name ?? string.Empty,
        IsVerified = partner.Id == supplier.Id
    };
}
