using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosFoundationService
{
    Task<MobilePosAdministrationReferencesDto> GetAdministrationReferencesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MobilePosStoreDto>> GetStoresAsync(CancellationToken cancellationToken);
    Task<MobilePosStoreDto> SaveStoreAsync(Guid? id, MobilePosStoreUpsertDto dto, CancellationToken cancellationToken);
    Task<IReadOnlyList<MobilePosOfflinePolicyDto>> GetOfflinePoliciesAsync(CancellationToken cancellationToken);
    Task<MobilePosOfflinePolicyDto> SaveOfflinePolicyAsync(Guid? id, MobilePosOfflinePolicyUpsertDto dto, CancellationToken cancellationToken);
    Task<IReadOnlyList<MobilePosTillDto>> GetTillsAsync(Guid? storeId, CancellationToken cancellationToken);
    Task<MobilePosTillDto> SaveTillAsync(Guid? id, MobilePosTillUpsertDto dto, CancellationToken cancellationToken);
    Task SaveUserStoreAssignmentAsync(MobilePosUserStoreAssignmentUpsertDto dto, CancellationToken cancellationToken);
    Task<IReadOnlyList<MobilePosDeviceDto>> GetDevicesAsync(MobilePosDeviceStatus? status, CancellationToken cancellationToken);
    Task<MobilePosDeviceDto> RequestEnrollmentAsync(MobilePosDeviceEnrollmentRequestDto dto, CancellationToken cancellationToken);
    Task<MobilePosDeviceDto> ApproveDeviceAsync(Guid id, MobilePosDeviceApprovalDto dto, CancellationToken cancellationToken);
    Task<MobilePosDeviceDto> RevokeDeviceAsync(Guid id, MobilePosDeviceStatusChangeDto dto, CancellationToken cancellationToken);
    Task<MobilePosDeviceDto> RecordHeartbeatAsync(MobilePosHeartbeatDto dto, CancellationToken cancellationToken);
    Task<MobilePosBootstrapDto> GetBootstrapAsync(string installationId, CancellationToken cancellationToken);
    Task<MobilePosOfflineGrantDto> IssueOfflineGrantAsync(
        MobilePosOfflineGrantRequestDto dto,
        IReadOnlyCollection<string> authorizedPermissions,
        CancellationToken cancellationToken);
}

public sealed class MobilePosFoundationService : IMobilePosFoundationService
{
    private static readonly JsonSerializerOptions GrantJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHostEnvironment _environment;
    private readonly IMobilePosOfflineGrantTokenService _offlineGrantTokens;

    public MobilePosFoundationService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IHostEnvironment environment,
        IMobilePosOfflineGrantTokenService offlineGrantTokens)
    {
        _db = db;
        _currentUser = currentUser;
        _environment = environment;
        _offlineGrantTokens = offlineGrantTokens;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required for Mobile POS.");

    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? "Unknown"
        : _currentUser.UserName.Trim();

    public async Task<MobilePosAdministrationReferencesDto> GetAdministrationReferencesAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var now = DateTime.UtcNow;

        var eligibleCustomerRoles = await _db.EligibleMobilePosCustomerRoles(tenantId, now).AsNoTracking()
            .OrderBy(role => role.BusinessPartner.PartnerName)
            .Select(role => new MobilePosCustomerReferenceDto
            {
                BusinessPartnerId = role.BusinessPartnerId,
                BusinessPartnerRoleId = role.Id,
                Code = role.BusinessPartner.PartnerCode,
                Name = role.BusinessPartner.PartnerName
            })
            .ToListAsync(cancellationToken);

        var currencies = await _db.Currencies.AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
            .OrderByDescending(item => item.IsBaseCurrency)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.CurrencyName)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id,
                Code = item.CurrencyCode,
                Name = item.CurrencyName,
                Secondary = item.IsBaseCurrency
                    ? "Base currency"
                    : item.CurrencySymbol
            })
            .ToListAsync(cancellationToken);

        var locations = await _db.Locations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id, Code = item.Code, Name = item.Name, Secondary = item.City
            }).ToListAsync(cancellationToken);

        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id, Code = item.Code, Name = item.Name, Secondary = item.City
            }).ToListAsync(cancellationToken);

        var companyProfiles = await _db.CompanyProfiles.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.LegalName)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id,
                Code = item.RegistrationNumber ?? string.Empty,
                Name = item.TradingName ?? item.LegalName,
                Secondary = item.TradingName == null ? null : item.LegalName
            }).ToListAsync(cancellationToken);

        var cashTills = await _db.LiquidityAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive &&
                           item.AccountType == LiquidityAccountType.CashTill)
            .OrderBy(item => item.Code)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id, Code = item.Code, Name = item.Name, Secondary = item.Currency
            }).ToListAsync(cancellationToken);

        var bankAccounts = await _db.BankAccounts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.BankName).ThenBy(item => item.AccountName)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id,
                Code = item.BankName,
                Name = item.AccountName,
                Secondary = item.Currency + " · " + (item.AccountNumber.Length > 4
                    ? item.AccountNumber.Substring(item.AccountNumber.Length - 4)
                    : item.AccountNumber)
            }).ToListAsync(cancellationToken);

        var paymentMethods = await _db.PaymentMethods.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id,
                Code = item.Code ?? item.Name,
                Name = item.Name,
                Secondary = item.Type.ToString()
            }).ToListAsync(cancellationToken);

        var users = await _db.Users.AsNoTracking()
            .Where(item => item.IsActive &&
                           (item.TenantId == tenantId || item.UserTenants.Any(link =>
                               link.TenantId == tenantId && !link.IsDeleted &&
                               link.Status == UserTenantStatus.Active &&
                               (!link.ExpiresAt.HasValue || link.ExpiresAt > now))))
            .OrderBy(item => item.FirstName).ThenBy(item => item.LastName)
            .Select(item => new MobilePosReferenceOptionDto
            {
                Id = item.Id,
                Code = item.UserName ?? item.Email ?? string.Empty,
                Name = (item.FirstName + " " + item.LastName).Trim(),
                Secondary = item.Email
            }).ToListAsync(cancellationToken);

        var definitions = await _db.FinanceDimensionDefinitions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Code, item.Name })
            .ToListAsync(cancellationToken);
        var dimensionValues = await _db.FinanceDimensionValues.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive &&
                           item.EffectiveDate <= now &&
                           (!item.ExpiryDate.HasValue || item.ExpiryDate > now))
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Name)
            .Select(item => new
            {
                item.FinanceDimensionDefinitionId,
                Option = new MobilePosReferenceOptionDto
                {
                    Id = item.Id, Code = item.Code, Name = item.Name
                }
            }).ToListAsync(cancellationToken);

        return new MobilePosAdministrationReferencesDto
        {
            Customers = eligibleCustomerRoles,
            Currencies = currencies,
            Locations = locations,
            Warehouses = warehouses,
            CompanyProfiles = companyProfiles,
            CashTills = cashTills,
            BankAccounts = bankAccounts,
            PaymentMethods = paymentMethods,
            Users = users,
            Dimensions = definitions.Select(definition => new MobilePosDimensionReferenceDto
            {
                DefinitionId = definition.Id,
                Code = definition.Code,
                Name = definition.Name,
                Values = dimensionValues
                    .Where(value => value.FinanceDimensionDefinitionId == definition.Id)
                    .Select(value => value.Option)
                    .ToList()
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<MobilePosStoreDto>> GetStoresAsync(CancellationToken cancellationToken)
    {
        var stores = await StoreQuery()
            .Where(item => item.TenantId == TenantId)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return stores.Select(MapStore).ToList();
    }

    public async Task<MobilePosStoreDto> SaveStoreAsync(
        Guid? id,
        MobilePosStoreUpsertDto dto,
        CancellationToken cancellationToken)
    {
        ValidateRequiredIds(dto.LocationId, dto.DefaultWalkInBusinessPartnerId, dto.DefaultWalkInBusinessPartnerRoleId);
        var tenantId = TenantId;
        var code = Required(dto.Code, "Store code").ToUpperInvariant();
        var name = Required(dto.Name, "Store name");
        var currency = Required(dto.CurrencyCode, "Currency").ToUpperInvariant();
        if (currency.Length != 3)
            throw new InvalidOperationException("Currency must be a three-character code.");

        await ValidateStoreReferencesAsync(dto, currency, cancellationToken);

        var duplicate = await _db.MobilePosStores.AnyAsync(item =>
            item.TenantId == tenantId && item.Code == code && (!id.HasValue || item.Id != id.Value), cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A Mobile POS store with code '{code}' already exists.");

        MobilePosStore store;
        object? before = null;
        if (id.HasValue)
        {
            store = await _db.MobilePosStores
                .Include(item => item.DimensionDefaults)
                .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("The Mobile POS store was not found.");
            ApplyRowVersion(store, dto.RowVersion);
            before = StoreAuditSnapshot(store);
            _db.MobilePosStoreDimensionDefaults.RemoveRange(store.DimensionDefaults);
        }
        else
        {
            store = new MobilePosStore
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            _db.MobilePosStores.Add(store);
        }

        store.Code = code;
        store.Name = name;
        store.Status = dto.Status;
        store.CompanyProfileId = dto.CompanyProfileId;
        store.LocationId = dto.LocationId;
        store.WarehouseId = dto.WarehouseId;
        store.CurrencyCode = currency;
        store.TimeZoneId = Required(dto.TimeZoneId, "Time zone");
        store.DefaultWalkInBusinessPartnerId = dto.DefaultWalkInBusinessPartnerId;
        store.DefaultWalkInBusinessPartnerRoleId = dto.DefaultWalkInBusinessPartnerRoleId;
        store.OfflinePolicyId = dto.OfflinePolicyId;
        store.Notes = Clean(dto.Notes);
        StampUpdated(store);

        foreach (var dimension in dto.DimensionDefaults)
        {
            store.DimensionDefaults.Add(new MobilePosStoreDimensionDefault
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MobilePosStoreId = store.Id,
                FinanceDimensionDefinitionId = dimension.FinanceDimensionDefinitionId,
                FinanceDimensionValueId = dimension.FinanceDimensionValueId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            });
        }

        await AddAuditAsync(
            id.HasValue ? "MobilePOS.Store.Updated" : "MobilePOS.Store.Created",
            nameof(MobilePosStore),
            store.Id,
            before,
            StoreAuditSnapshot(store));
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(
                "MOBILE_POS_STORE_VERSION_CONFLICT",
                "This Mobile POS store changed after it was opened. Reload the latest store details, review your changes, and save again.",
                StatusCodes.Status409Conflict);
        }
        return await GetStoreRequiredAsync(store.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<MobilePosOfflinePolicyDto>> GetOfflinePoliciesAsync(
        CancellationToken cancellationToken)
    {
        var items = await _db.MobilePosOfflinePolicies.AsNoTracking()
            .Where(item => item.TenantId == TenantId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return items.Select(MapPolicy).ToList();
    }

    public async Task<MobilePosOfflinePolicyDto> SaveOfflinePolicyAsync(
        Guid? id,
        MobilePosOfflinePolicyUpsertDto dto,
        CancellationToken cancellationToken)
    {
        ValidatePolicy(dto);
        var tenantId = TenantId;
        var name = Required(dto.Name, "Policy name");
        var duplicate = await _db.MobilePosOfflinePolicies.AnyAsync(item =>
            item.TenantId == tenantId && item.Name == name && (!id.HasValue || item.Id != id.Value), cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"An offline policy named '{name}' already exists.");

        MobilePosOfflinePolicy policy;
        object? before = null;
        if (id.HasValue)
        {
            policy = await _db.MobilePosOfflinePolicies.SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("The Mobile POS offline policy was not found.");
            ApplyRowVersion(policy, dto.RowVersion);
            before = PolicyAuditSnapshot(policy);
        }
        else
        {
            policy = new MobilePosOfflinePolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            _db.MobilePosOfflinePolicies.Add(policy);
        }

        policy.Name = name;
        policy.IsActive = dto.IsActive;
        policy.AuthorizationWindowMinutes = dto.AuthorizationWindowMinutes;
        policy.MaximumTransactionAmount = dto.MaximumTransactionAmount;
        policy.MaximumAggregateAmount = dto.MaximumAggregateAmount;
        policy.MaximumTransactionCount = dto.MaximumTransactionCount;
        policy.MaximumOfflineAgeMinutes = dto.MaximumOfflineAgeMinutes;
        policy.AllowCashSale = dto.AllowCashSale;
        policy.AllowCashReceipt = dto.AllowCashReceipt;
        policy.AllowPartialPayment = dto.AllowPartialPayment;
        policy.AllowReturns = dto.AllowReturns;
        policy.AllowReversals = dto.AllowReversals;
        policy.AllowProvisionalReceipt = dto.AllowProvisionalReceipt;
        policy.AllowDayEndSubmissionWithPendingSync = dto.AllowDayEndSubmissionWithPendingSync;
        policy.RequireExternalReferenceForElectronicTender = dto.RequireExternalReferenceForElectronicTender;
        StampUpdated(policy);

        await AddAuditAsync(
            id.HasValue ? "MobilePOS.OfflinePolicy.Updated" : "MobilePOS.OfflinePolicy.Created",
            nameof(MobilePosOfflinePolicy),
            policy.Id,
            before,
            PolicyAuditSnapshot(policy));
        await _db.SaveChangesAsync(cancellationToken);
        return MapPolicy(policy);
    }

    public async Task<IReadOnlyList<MobilePosTillDto>> GetTillsAsync(
        Guid? storeId,
        CancellationToken cancellationToken)
    {
        var query = TillQuery().Where(item => item.TenantId == TenantId);
        if (storeId.HasValue)
            query = query.Where(item => item.MobilePosStoreId == storeId.Value);
        var tills = await query.OrderBy(item => item.TillNumber).ToListAsync(cancellationToken);
        return tills.Select(MapTill).ToList();
    }

    public async Task<MobilePosTillDto> SaveTillAsync(
        Guid? id,
        MobilePosTillUpsertDto dto,
        CancellationToken cancellationToken)
    {
        ValidateRequiredIds(dto.MobilePosStoreId, dto.LiquidityAccountId);
        var tenantId = TenantId;
        var tillNumber = Required(dto.TillNumber, "Till number").ToUpperInvariant();
        var name = Required(dto.Name, "Till name");
        var store = await _db.MobilePosStores.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == dto.MobilePosStoreId, cancellationToken)
            ?? throw new InvalidOperationException("Select a Mobile POS store belonging to the current tenant.");
        var liquidity = await _db.LiquidityAccounts.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == dto.LiquidityAccountId &&
                    item.AccountType == LiquidityAccountType.CashTill && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Select an active CashTill liquidity account belonging to the current tenant.");
        if (!string.Equals(store.CurrencyCode, liquidity.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The till liquidity account currency must match the store currency.");
        if (dto.Status == MobilePosTillStatus.Active && store.Status != MobilePosStoreStatus.Active)
            throw new InvalidOperationException("A till can become active only when its store is active.");

        var paymentMethodIds = dto.PaymentMethods.Select(item => item.PaymentMethodId).ToArray();
        if (paymentMethodIds.Length != paymentMethodIds.Distinct().Count())
            throw new InvalidOperationException("A payment method can appear only once on a till.");
        var validMethods = await _db.PaymentMethods.AsNoTracking().CountAsync(
            item => item.TenantId == tenantId && paymentMethodIds.Contains(item.Id) && item.IsActive, cancellationToken);
        if (validMethods != paymentMethodIds.Length)
            throw new InvalidOperationException("Every till payment method must be active and belong to the current tenant.");

        var duplicateNumber = await _db.MobilePosTills.AnyAsync(item =>
            item.TenantId == tenantId && item.TillNumber == tillNumber && (!id.HasValue || item.Id != id.Value), cancellationToken);
        if (duplicateNumber)
            throw new InvalidOperationException($"A Mobile POS till numbered '{tillNumber}' already exists.");
        var duplicateLiquidity = await _db.MobilePosTills.AnyAsync(item =>
            item.TenantId == tenantId && item.LiquidityAccountId == dto.LiquidityAccountId &&
            item.Status != MobilePosTillStatus.Retired && (!id.HasValue || item.Id != id.Value), cancellationToken);
        if (duplicateLiquidity)
            throw new InvalidOperationException("This CashTill liquidity account is already mapped to another Mobile POS till.");

        MobilePosTill till;
        object? before = null;
        if (id.HasValue)
        {
            till = await _db.MobilePosTills.Include(item => item.PaymentMethods).SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("The Mobile POS till was not found.");
            ApplyRowVersion(till, dto.RowVersion);
            before = TillAuditSnapshot(till);
            _db.MobilePosTillPaymentMethods.RemoveRange(till.PaymentMethods);
        }
        else
        {
            till = new MobilePosTill
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            };
            _db.MobilePosTills.Add(till);
        }

        till.MobilePosStoreId = dto.MobilePosStoreId;
        till.TillNumber = tillNumber;
        till.Name = name;
        till.Status = dto.Status;
        till.LiquidityAccountId = dto.LiquidityAccountId;
        till.Notes = Clean(dto.Notes);
        if (dto.Status == MobilePosTillStatus.Active && till.LastActivatedAtUtc == null)
            till.LastActivatedAtUtc = DateTime.UtcNow;
        StampUpdated(till);

        foreach (var method in dto.PaymentMethods)
        {
            till.PaymentMethods.Add(new MobilePosTillPaymentMethod
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MobilePosTillId = till.Id,
                PaymentMethodId = method.PaymentMethodId,
                AllowOnline = method.AllowOnline,
                AllowOffline = method.AllowOffline,
                RequireExternalAuthorizationReference = method.RequireExternalAuthorizationReference,
                DisplayOrder = method.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            });
        }

        await AddAuditAsync(
            id.HasValue ? "MobilePOS.Till.Updated" : "MobilePOS.Till.Created",
            nameof(MobilePosTill),
            till.Id,
            before,
            TillAuditSnapshot(till));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetTillRequiredAsync(till.Id, cancellationToken);
    }

    public async Task SaveUserStoreAssignmentAsync(
        MobilePosUserStoreAssignmentUpsertDto dto,
        CancellationToken cancellationToken)
    {
        ValidateRequiredIds(dto.UserId, dto.MobilePosStoreId);
        if (dto.EffectiveToUtc.HasValue && dto.EffectiveToUtc <= dto.EffectiveFromUtc)
            throw new InvalidOperationException("Assignment end must be later than its start.");
        var tenantId = TenantId;
        var userExists = await _db.Users.AsNoTracking().AnyAsync(
            item => item.Id == dto.UserId && item.IsActive &&
                    (item.TenantId == tenantId || item.UserTenants.Any(link =>
                        link.TenantId == tenantId && !link.IsDeleted && link.Status == UserTenantStatus.Active &&
                        (!link.ExpiresAt.HasValue || link.ExpiresAt > DateTime.UtcNow))), cancellationToken);
        if (!userExists)
            throw new InvalidOperationException("Select an active user with access to the current tenant.");
        var storeExists = await _db.MobilePosStores.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId && item.Id == dto.MobilePosStoreId &&
                    item.Status == MobilePosStoreStatus.Active, cancellationToken);
        if (!storeExists)
            throw new InvalidOperationException("Select an active Mobile POS store.");

        var current = await _db.MobilePosUserStoreAssignments
            .Where(item => item.TenantId == tenantId && item.UserId == dto.UserId && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var item in current)
        {
            item.IsActive = false;
            item.EffectiveToUtc ??= DateTime.UtcNow;
            StampUpdated(item);
        }

        var assignment = new MobilePosUserStoreAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = dto.UserId,
            MobilePosStoreId = dto.MobilePosStoreId,
            EffectiveFromUtc = dto.EffectiveFromUtc,
            EffectiveToUtc = dto.EffectiveToUtc,
            IsActive = true,
            Reason = Required(dto.Reason, "Assignment reason"),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        _db.MobilePosUserStoreAssignments.Add(assignment);
        await AddAuditAsync("MobilePOS.UserStore.Assigned", nameof(MobilePosUserStoreAssignment), assignment.Id,
            current.Select(item => new { item.Id, item.MobilePosStoreId, item.EffectiveFromUtc, item.EffectiveToUtc }),
            new { assignment.UserId, assignment.MobilePosStoreId, assignment.EffectiveFromUtc, assignment.EffectiveToUtc, assignment.Reason });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobilePosDeviceDto>> GetDevicesAsync(
        MobilePosDeviceStatus? status,
        CancellationToken cancellationToken)
    {
        var query = DeviceQuery().Where(item => item.TenantId == TenantId);
        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);
        var devices = await query.OrderByDescending(item => item.RequestedAtUtc).ToListAsync(cancellationToken);
        return devices.Select(MapDevice).ToList();
    }

    public async Task<MobilePosDeviceDto> RequestEnrollmentAsync(
        MobilePosDeviceEnrollmentRequestDto dto,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var hash = HashInstallationId(dto.InstallationId);
        var existing = await DeviceQuery().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.InstallationIdHash == hash, cancellationToken);
        if (existing != null)
        {
            existing.DeviceName = Required(dto.DeviceName, "Device name");
            existing.Manufacturer = Clean(dto.Manufacturer);
            existing.Model = Clean(dto.Model);
            existing.OperatingSystemVersion = Clean(dto.OperatingSystemVersion);
            existing.AppVersion = Clean(dto.AppVersion);
            existing.PublicKeyThumbprint = Clean(dto.PublicKeyThumbprint);
            existing.PrinterAdapterKey = Clean(dto.PrinterAdapterKey);
            existing.ScannerAdapterKey = Clean(dto.ScannerAdapterKey);
            existing.LastSeenAtUtc = DateTime.UtcNow;
            StampUpdated(existing);
            await _db.SaveChangesAsync(cancellationToken);
            return MapDevice(existing);
        }

        var device = new MobilePosDevice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InstallationIdHash = hash,
            DeviceName = Required(dto.DeviceName, "Device name"),
            Manufacturer = Clean(dto.Manufacturer),
            Model = Clean(dto.Model),
            OperatingSystemVersion = Clean(dto.OperatingSystemVersion),
            AppVersion = Clean(dto.AppVersion),
            PublicKeyThumbprint = Clean(dto.PublicKeyThumbprint),
            PrinterAdapterKey = Clean(dto.PrinterAdapterKey),
            ScannerAdapterKey = Clean(dto.ScannerAdapterKey),
            Status = MobilePosDeviceStatus.Pending,
            RequestedByUserId = UserId,
            RequestedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        _db.MobilePosDevices.Add(device);
        await AddAuditAsync("MobilePOS.Device.EnrollmentRequested", nameof(MobilePosDevice), device.Id,
            null, DeviceAuditSnapshot(device));
        await _db.SaveChangesAsync(cancellationToken);
        return MapDevice(device);
    }

    public async Task<MobilePosDeviceDto> ApproveDeviceAsync(
        Guid id,
        MobilePosDeviceApprovalDto dto,
        CancellationToken cancellationToken)
    {
        var device = await _db.MobilePosDevices.SingleOrDefaultAsync(
            item => item.TenantId == TenantId && item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS device was not found.");
        ApplyRowVersion(device, dto.RowVersion);
        var before = DeviceAuditSnapshot(device);
        var till = await _db.MobilePosTills.AsNoTracking()
            .Include(item => item.MobilePosStore)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == dto.MobilePosTillId &&
                                          item.MobilePosStoreId == dto.MobilePosStoreId, cancellationToken)
            ?? throw new InvalidOperationException("The selected till does not belong to the selected store.");
        if (till.Status != MobilePosTillStatus.Active || till.MobilePosStore.Status != MobilePosStoreStatus.Active)
            throw new InvalidOperationException("The device can be assigned only to an active store and active till.");
        var inUse = await _db.MobilePosDevices.AnyAsync(item =>
            item.TenantId == TenantId && item.Id != device.Id && item.MobilePosTillId == till.Id &&
            item.Status == MobilePosDeviceStatus.Active, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("The selected till is already assigned to another active device.");

        var now = DateTime.UtcNow;
        var previousStoreId = device.MobilePosStoreId;
        var previousTillId = device.MobilePosTillId;
        device.MobilePosStoreId = dto.MobilePosStoreId;
        device.MobilePosTillId = dto.MobilePosTillId;
        device.Status = MobilePosDeviceStatus.Active;
        device.ApprovedByUserId = UserId;
        device.ApprovedAtUtc = now;
        device.RevokedByUserId = null;
        device.RevokedAtUtc = null;
        device.StatusReason = Required(dto.Reason, "Approval reason");
        StampUpdated(device);
        _db.MobilePosDeviceAssignmentHistories.Add(new MobilePosDeviceAssignmentHistory
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            MobilePosDeviceId = device.Id,
            PreviousStoreId = previousStoreId,
            PreviousTillId = previousTillId,
            NewStoreId = device.MobilePosStoreId,
            NewTillId = device.MobilePosTillId,
            ChangedByUserId = UserId,
            ChangedAtUtc = now,
            Reason = device.StatusReason,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        });
        await AddAuditAsync("MobilePOS.Device.Approved", nameof(MobilePosDevice), device.Id,
            before, DeviceAuditSnapshot(device));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetDeviceRequiredAsync(device.Id, cancellationToken);
    }

    public async Task<MobilePosDeviceDto> RevokeDeviceAsync(
        Guid id,
        MobilePosDeviceStatusChangeDto dto,
        CancellationToken cancellationToken)
    {
        var device = await _db.MobilePosDevices.SingleOrDefaultAsync(
            item => item.TenantId == TenantId && item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS device was not found.");
        ApplyRowVersion(device, dto.RowVersion);
        if (device.Status == MobilePosDeviceStatus.Revoked)
            return await GetDeviceRequiredAsync(device.Id, cancellationToken);
        var before = DeviceAuditSnapshot(device);
        device.Status = MobilePosDeviceStatus.Revoked;
        device.RevokedByUserId = UserId;
        device.RevokedAtUtc = DateTime.UtcNow;
        device.StatusReason = Required(dto.Reason, "Revocation reason");
        device.RevocationEpoch++;
        StampUpdated(device);

        var activeGrants = await _db.MobilePosOfflineGrants.Where(item =>
            item.TenantId == TenantId && item.MobilePosDeviceId == device.Id &&
            item.Status == MobilePosOfflineGrantStatus.Active).ToListAsync(cancellationToken);
        foreach (var grant in activeGrants)
        {
            grant.Status = MobilePosOfflineGrantStatus.Revoked;
            grant.RevokedAtUtc = DateTime.UtcNow;
            grant.RevokedByUserId = UserId;
            grant.RevocationReason = device.StatusReason;
            grant.UpdatedAt = DateTime.UtcNow;
            grant.UpdatedBy = UserName;
            grant.LastModifiedById = UserId;
        }

        await AddAuditAsync("MobilePOS.Device.Revoked", nameof(MobilePosDevice), device.Id,
            before, DeviceAuditSnapshot(device));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetDeviceRequiredAsync(device.Id, cancellationToken);
    }

    public async Task<MobilePosDeviceDto> RecordHeartbeatAsync(
        MobilePosHeartbeatDto dto,
        CancellationToken cancellationToken)
    {
        var hash = HashInstallationId(dto.InstallationId);
        var device = await _db.MobilePosDevices.SingleOrDefaultAsync(
            item => item.TenantId == TenantId && item.InstallationIdHash == hash, cancellationToken)
            ?? throw new KeyNotFoundException("This mobile installation is not enrolled.");
        if (device.Status != MobilePosDeviceStatus.Active)
            throw new UnauthorizedAccessException($"This mobile device is {device.Status} and cannot operate.");
        device.AppVersion = Clean(dto.AppVersion) ?? device.AppVersion;
        device.OperatingSystemVersion = Clean(dto.OperatingSystemVersion) ?? device.OperatingSystemVersion;
        device.PrinterAdapterKey = Clean(dto.PrinterAdapterKey) ?? device.PrinterAdapterKey;
        device.ScannerAdapterKey = Clean(dto.ScannerAdapterKey) ?? device.ScannerAdapterKey;
        device.LastSeenAtUtc = DateTime.UtcNow;
        if (dto.LastSyncAtUtc.HasValue && (!device.LastSyncAtUtc.HasValue || dto.LastSyncAtUtc > device.LastSyncAtUtc))
            device.LastSyncAtUtc = dto.LastSyncAtUtc.Value;
        StampUpdated(device);
        if (device.MobilePosTillId.HasValue)
        {
            var till = await _db.MobilePosTills.SingleAsync(item =>
                item.TenantId == TenantId && item.Id == device.MobilePosTillId.Value, cancellationToken);
            till.LastHeartbeatAtUtc = device.LastSeenAtUtc;
            StampUpdated(till);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return await GetDeviceRequiredAsync(device.Id, cancellationToken);
    }

    public async Task<MobilePosOfflineGrantDto> IssueOfflineGrantAsync(
        MobilePosOfflineGrantRequestDto dto,
        IReadOnlyCollection<string> authorizedPermissions,
        CancellationToken cancellationToken)
    {
        var bootstrap = await GetBootstrapAsync(dto.InstallationId, cancellationToken);
        if (!bootstrap.CurrentTillSessionId.HasValue)
            throw new InvalidOperationException("Open your assigned cashier till session before requesting offline authorization.");
        if (bootstrap.OfflinePolicy == null || !bootstrap.Store.OfflinePolicyId.HasValue)
            throw new InvalidOperationException("The assigned store does not have an active offline policy.");

        var policy = await _db.MobilePosOfflinePolicies.AsNoTracking().SingleAsync(item =>
            item.TenantId == TenantId && item.Id == bootstrap.Store.OfflinePolicyId.Value && item.IsActive,
            cancellationToken);
        var permissionSet = authorizedPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var canCashSale = policy.AllowCashSale && HasPermissions(permissionSet,
            MobilePosPermissions.CreateInvoice,
            MobilePosPermissions.PostInvoice,
            MobilePosPermissions.CollectPayment,
            FinancePermissions.CreateArInvoices,
            FinancePermissions.ApprovePostArInvoices,
            FinancePermissions.ReceiveCustomerPayments);
        var canCashReceipt = policy.AllowCashReceipt && permissionSet.Contains(MobilePosPermissions.CollectPayment);
        var canPartialPayment = policy.AllowPartialPayment && canCashReceipt;
        var canReturn = policy.AllowReturns && permissionSet.Contains(MobilePosPermissions.CreateReturn);
        var canReversal = policy.AllowReversals && permissionSet.Contains(MobilePosPermissions.CreateReversal);
        var canPendingDayEnd = policy.AllowDayEndSubmissionWithPendingSync &&
                               permissionSet.Contains(MobilePosPermissions.CloseTill);

        var allowedCommands = new List<string>();
        if (canCashSale) allowedCommands.Add("CashSale");
        if (canCashReceipt) allowedCommands.Add("CashReceipt");
        if (canPartialPayment) allowedCommands.Add("PartialPayment");
        if (canReturn) allowedCommands.Add("Return");
        if (canReversal) allowedCommands.Add("Reversal");
        if (canPendingDayEnd) allowedCommands.Add("DayEndSubmissionWithPendingSync");
        if (allowedCommands.Count == 0)
            throw new UnauthorizedAccessException(
                "Your current permissions and store policy do not authorize any offline Mobile POS operations.");

        var paymentMethods = await _db.MobilePosTillPaymentMethods.AsNoTracking()
            .Include(item => item.PaymentMethod)
            .Where(item => item.TenantId == TenantId && item.MobilePosTillId == bootstrap.Till.Id &&
                           item.AllowOffline && item.PaymentMethod.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new MobilePosOfflinePaymentMethodSnapshotDto
            {
                PaymentMethodId = item.PaymentMethodId,
                Code = item.PaymentMethod.Code ?? item.PaymentMethod.Name,
                Name = item.PaymentMethod.Name,
                Type = item.PaymentMethod.Type.ToString(),
                RequiresReference = item.PaymentMethod.RequiresReference,
                RequireExternalAuthorizationReference = item.RequireExternalAuthorizationReference ||
                    (policy.RequireExternalReferenceForElectronicTender &&
                     item.PaymentMethod.Type != PaymentMethodType.Cash)
            })
            .ToListAsync(cancellationToken);
        if ((canCashSale || canCashReceipt) && paymentMethods.Count == 0)
            throw new InvalidOperationException(
                "The assigned till has no active payment method authorized for offline use.");

        var now = DateTime.UtcNow;
        var windowMinutes = Math.Min(policy.AuthorizationWindowMinutes, policy.MaximumOfflineAgeMinutes);
        var expiresAt = now.AddMinutes(windowMinutes);
        var snapshot = new MobilePosOfflineGrantPolicySnapshotDto
        {
            PolicyId = policy.Id,
            PolicyName = policy.Name,
            PolicyVersionUtc = (policy.UpdatedAt ?? policy.CreatedAt).ToUniversalTime(),
            CurrencyCode = bootstrap.Store.CurrencyCode,
            DefaultWalkInBusinessPartnerId = bootstrap.Store.DefaultWalkInBusinessPartnerId,
            DefaultWalkInBusinessPartnerRoleId = bootstrap.Store.DefaultWalkInBusinessPartnerRoleId,
            MaximumTransactionAmount = policy.MaximumTransactionAmount,
            MaximumAggregateAmount = policy.MaximumAggregateAmount,
            MaximumTransactionCount = policy.MaximumTransactionCount,
            MaximumOfflineAgeMinutes = policy.MaximumOfflineAgeMinutes,
            AllowPartialPayment = canPartialPayment,
            AllowDiscounts = permissionSet.Contains(MobilePosPermissions.ApplyDiscount),
            AllowProvisionalReceipt = policy.AllowProvisionalReceipt && (canCashSale || canCashReceipt),
            AllowDayEndSubmissionWithPendingSync = canPendingDayEnd,
            AllowedCommandTypes = allowedCommands,
            AllowedPaymentMethods = paymentMethods
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot, GrantJsonOptions);
        var snapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));

        var previousGrants = await _db.MobilePosOfflineGrants.Where(item =>
            item.TenantId == TenantId && item.MobilePosDeviceId == bootstrap.Device.Id &&
            item.Status == MobilePosOfflineGrantStatus.Active).ToListAsync(cancellationToken);
        foreach (var previous in previousGrants)
        {
            previous.Status = previous.ExpiresAtUtc <= now
                ? MobilePosOfflineGrantStatus.Expired
                : MobilePosOfflineGrantStatus.Revoked;
            if (previous.Status == MobilePosOfflineGrantStatus.Revoked)
            {
                previous.RevokedAtUtc = now;
                previous.RevokedByUserId = UserId;
                previous.RevocationReason = "Superseded by a newly issued offline grant.";
            }
            StampUpdated(previous);
        }

        var grant = new MobilePosOfflineGrant
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = UserId,
            MobilePosDeviceId = bootstrap.Device.Id,
            MobilePosStoreId = bootstrap.Store.Id,
            MobilePosTillId = bootstrap.Till.Id,
            CashierTillSessionId = bootstrap.CurrentTillSessionId.Value,
            MobilePosOfflinePolicyId = policy.Id,
            IssuedAtUtc = now,
            ExpiresAtUtc = expiresAt,
            Status = MobilePosOfflineGrantStatus.Active,
            RevocationEpoch = bootstrap.Device.RevocationEpoch,
            PolicySnapshotJson = snapshotJson,
            PolicySnapshotHash = snapshotHash,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = UserId
        };
        _db.MobilePosOfflineGrants.Add(grant);

        var token = _offlineGrantTokens.Sign(new MobilePosOfflineGrantTokenPayload(
            MobilePosOfflineGrantTokenService.CurrentVersion,
            grant.Id,
            TenantId,
            UserId,
            grant.MobilePosDeviceId,
            grant.MobilePosStoreId,
            grant.MobilePosTillId,
            grant.CashierTillSessionId,
            grant.MobilePosOfflinePolicyId,
            snapshotHash,
            grant.RevocationEpoch,
            now,
            expiresAt));

        await AddAuditAsync("MobilePOS.OfflineGrant.Issued", nameof(MobilePosOfflineGrant), grant.Id, null, new
        {
            grant.UserId,
            grant.MobilePosDeviceId,
            grant.MobilePosStoreId,
            grant.MobilePosTillId,
            grant.CashierTillSessionId,
            grant.MobilePosOfflinePolicyId,
            grant.IssuedAtUtc,
            grant.ExpiresAtUtc,
            grant.RevocationEpoch,
            grant.PolicySnapshotHash,
            allowedCommands,
            paymentMethodIds = paymentMethods.Select(item => item.PaymentMethodId).ToArray(),
            supersededGrantIds = previousGrants.Select(item => item.Id).ToArray()
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new MobilePosOfflineGrantDto
        {
            Id = grant.Id,
            Version = MobilePosOfflineGrantTokenService.CurrentVersion,
            Token = token,
            TenantId = TenantId,
            UserId = UserId,
            MobilePosDeviceId = grant.MobilePosDeviceId,
            MobilePosStoreId = grant.MobilePosStoreId,
            MobilePosTillId = grant.MobilePosTillId,
            CashierTillSessionId = grant.CashierTillSessionId,
            MobilePosOfflinePolicyId = grant.MobilePosOfflinePolicyId,
            IssuedAtUtc = grant.IssuedAtUtc,
            ExpiresAtUtc = grant.ExpiresAtUtc,
            RevocationEpoch = grant.RevocationEpoch,
            PolicySnapshotHash = grant.PolicySnapshotHash,
            Policy = snapshot
        };
    }

    public async Task<MobilePosBootstrapDto> GetBootstrapAsync(
        string installationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hash = HashInstallationId(installationId);
        var device = await DeviceQuery().SingleOrDefaultAsync(
            item => item.TenantId == TenantId && item.InstallationIdHash == hash, cancellationToken)
            ?? throw new UnauthorizedAccessException("This mobile installation has not been enrolled.");
        if (device.Status != MobilePosDeviceStatus.Active || !device.MobilePosStoreId.HasValue || !device.MobilePosTillId.HasValue)
            throw new UnauthorizedAccessException("This mobile device is not active and fully assigned by HQ.");

        var assignment = await _db.MobilePosUserStoreAssignments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.UserId == UserId && item.IsActive &&
            item.EffectiveFromUtc <= now && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc > now), cancellationToken)
            ?? throw new UnauthorizedAccessException("The current user has no active Mobile POS store assignment.");
        if (assignment.MobilePosStoreId != device.MobilePosStoreId.Value)
            throw new UnauthorizedAccessException("The user and device are assigned to different Mobile POS stores.");

        var store = await GetStoreRequiredAsync(device.MobilePosStoreId.Value, cancellationToken);
        var till = await GetTillRequiredAsync(device.MobilePosTillId.Value, cancellationToken);
        if (store.Status != MobilePosStoreStatus.Active || till.Status != MobilePosTillStatus.Active)
            throw new UnauthorizedAccessException("The assigned Mobile POS store or till is not active.");
        await ValidateApprovedWalkInCustomerAsync(
            store.DefaultWalkInBusinessPartnerId,
            store.DefaultWalkInBusinessPartnerRoleId,
            now,
            cancellationToken);

        var currentSessionId = await _db.CashierTillSessions.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.LiquidityAccountId == till.LiquidityAccountId &&
                           item.CashierUserId == UserId && item.Status == CashierTillSessionStatus.Open)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);

        MobilePosOfflinePolicyDto? policy = null;
        if (store.OfflinePolicyId.HasValue)
        {
            var entity = await _db.MobilePosOfflinePolicies.AsNoTracking().SingleOrDefaultAsync(
                item => item.TenantId == TenantId && item.Id == store.OfflinePolicyId.Value && item.IsActive,
                cancellationToken);
            if (entity != null)
                policy = MapPolicy(entity);
        }

        device.LastSeenAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return new MobilePosBootstrapDto
        {
            EnvironmentName = NormalizeEnvironment(_environment.EnvironmentName),
            UserId = UserId,
            UserName = UserName,
            Device = MapDevice(device),
            Store = store,
            Till = till,
            OfflinePolicy = policy,
            CurrentTillSessionId = currentSessionId,
            ServerTimeUtc = now
        };
    }

    private IQueryable<MobilePosStore> StoreQuery() => _db.MobilePosStores.AsNoTracking()
        .Include(item => item.Location)
        .Include(item => item.Warehouse)
        .Include(item => item.DefaultWalkInBusinessPartner)
        .Include(item => item.DefaultWalkInBusinessPartnerRole)
        .Include(item => item.OfflinePolicy)
        .Include(item => item.DimensionDefaults).ThenInclude(item => item.FinanceDimensionDefinition)
        .Include(item => item.DimensionDefaults).ThenInclude(item => item.FinanceDimensionValue);

    private IQueryable<MobilePosTill> TillQuery() => _db.MobilePosTills.AsNoTracking()
        .Include(item => item.MobilePosStore)
        .Include(item => item.LiquidityAccount)
        .Include(item => item.PaymentMethods).ThenInclude(item => item.PaymentMethod);

    private IQueryable<MobilePosDevice> DeviceQuery() => _db.MobilePosDevices
        .Include(item => item.MobilePosStore)
        .Include(item => item.MobilePosTill);

    private async Task<MobilePosStoreDto> GetStoreRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var store = await StoreQuery().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS store was not found.");
        return MapStore(store);
    }

    private async Task<MobilePosTillDto> GetTillRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var till = await TillQuery().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS till was not found.");
        return MapTill(till);
    }

    private async Task<MobilePosDeviceDto> GetDeviceRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var device = await DeviceQuery().SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("The Mobile POS device was not found.");
        return MapDevice(device);
    }

    private async Task ValidateStoreReferencesAsync(
        MobilePosStoreUpsertDto dto,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var now = DateTime.UtcNow;
        if (!await _db.Currencies.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && !item.IsDeleted &&
                        item.CurrencyCode == currencyCode && item.IsActive, cancellationToken))
            throw new InvalidOperationException("Select an active Finance currency belonging to the current tenant.");
        if (!await _db.Locations.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && item.Id == dto.LocationId && item.IsActive, cancellationToken))
            throw new InvalidOperationException("Select an active operating location belonging to the current tenant.");
        if (dto.CompanyProfileId.HasValue && !await _db.CompanyProfiles.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && item.Id == dto.CompanyProfileId.Value, cancellationToken))
            throw new InvalidOperationException("Select a company profile belonging to the current tenant.");
        if (dto.WarehouseId.HasValue && !await _db.Warehouses.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && item.Id == dto.WarehouseId.Value && item.IsActive, cancellationToken))
            throw new InvalidOperationException("Select an active warehouse belonging to the current tenant.");
        if (dto.OfflinePolicyId.HasValue && !await _db.MobilePosOfflinePolicies.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && item.Id == dto.OfflinePolicyId.Value && item.IsActive, cancellationToken))
            throw new InvalidOperationException("Select an active Mobile POS offline policy.");
        await ValidateApprovedWalkInCustomerAsync(
            dto.DefaultWalkInBusinessPartnerId,
            dto.DefaultWalkInBusinessPartnerRoleId,
            now,
            cancellationToken);

        if (dto.DimensionDefaults.Count != dto.DimensionDefaults.Select(item => item.FinanceDimensionDefinitionId).Distinct().Count())
            throw new InvalidOperationException("A Finance dimension can have only one store default.");
        foreach (var input in dto.DimensionDefaults)
        {
            var valid = await _db.FinanceDimensionValues.AsNoTracking()
                .Include(item => item.FinanceDimensionDefinition)
                .AnyAsync(item => item.TenantId == tenantId && item.Id == input.FinanceDimensionValueId &&
                                  item.FinanceDimensionDefinitionId == input.FinanceDimensionDefinitionId &&
                                  item.IsActive && item.FinanceDimensionDefinition.IsActive &&
                                  item.EffectiveDate <= now &&
                                  (!item.ExpiryDate.HasValue || item.ExpiryDate > now), cancellationToken);
            if (!valid)
                throw new InvalidOperationException("Every store Finance dimension value must be active, effective, and belong to its selected dimension.");
        }
    }

    private async Task ValidateApprovedWalkInCustomerAsync(
        Guid businessPartnerId,
        Guid roleId,
        DateTime effectiveAt,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var partner = await _db.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == businessPartnerId, cancellationToken)
            ?? throw new InvalidOperationException("Select a walk-in Business Partner belonging to the current tenant.");
        var operationalRegistration =
            string.Equals(partner.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(partner.RegistrationStatus, "Active", StringComparison.OrdinalIgnoreCase);
        if (!partner.IsActive || partner.IsDeleted || partner.IsBlacklisted || !operationalRegistration ||
            !string.Equals(partner.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The default walk-in Business Partner must be active, approved, and not blacklisted.");

        var role = await _db.BusinessPartnerRoles.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == roleId && item.BusinessPartnerId == businessPartnerId,
            cancellationToken)
            ?? throw new InvalidOperationException("The selected walk-in customer role does not belong to this Business Partner.");
        if (role.RoleType != BusinessPartnerRoleType.Customer || role.Status != BusinessPartnerRoleStatus.Active ||
            role.ActiveFromUtc > effectiveAt || (role.InactiveFromUtc.HasValue && role.InactiveFromUtc <= effectiveAt))
            throw new InvalidOperationException("The default walk-in Business Partner requires an active Customer role.");

        var hasApprovedProfile = await _db.BusinessPartnerArProfileVersions.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId && item.BusinessPartnerRoleId == roleId &&
            item.Status == BusinessPartnerFinanceProfileStatus.Approved && item.EffectiveFrom <= effectiveAt &&
            (!item.EffectiveTo.HasValue || item.EffectiveTo > effectiveAt), cancellationToken);
        if (!hasApprovedProfile)
            throw new InvalidOperationException("The default walk-in customer requires an approved effective AR profile.");
    }

    private static MobilePosStoreDto MapStore(MobilePosStore item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        Name = item.Name,
        Status = item.Status,
        CompanyProfileId = item.CompanyProfileId,
        LocationId = item.LocationId,
        LocationName = item.Location.Name,
        WarehouseId = item.WarehouseId,
        WarehouseName = item.Warehouse?.Name,
        CurrencyCode = item.CurrencyCode,
        TimeZoneId = item.TimeZoneId,
        DefaultWalkInBusinessPartnerId = item.DefaultWalkInBusinessPartnerId,
        DefaultWalkInBusinessPartnerRoleId = item.DefaultWalkInBusinessPartnerRoleId,
        DefaultWalkInCustomerCode = item.DefaultWalkInBusinessPartner.PartnerCode,
        DefaultWalkInCustomerName = item.DefaultWalkInBusinessPartner.PartnerName,
        OfflinePolicyId = item.OfflinePolicyId,
        OfflinePolicyName = item.OfflinePolicy?.Name,
        Notes = item.Notes,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        DimensionDefaults = item.DimensionDefaults.OrderBy(value => value.FinanceDimensionDefinition.DisplayOrder)
            .Select(value => new MobilePosStoreDimensionDefaultDto
            {
                FinanceDimensionDefinitionId = value.FinanceDimensionDefinitionId,
                DimensionCode = value.FinanceDimensionDefinition.Code,
                FinanceDimensionValueId = value.FinanceDimensionValueId,
                ValueCode = value.FinanceDimensionValue.Code,
                ValueName = value.FinanceDimensionValue.Name
            }).ToList()
    };

    private static MobilePosOfflinePolicyDto MapPolicy(MobilePosOfflinePolicy item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        IsActive = item.IsActive,
        AuthorizationWindowMinutes = item.AuthorizationWindowMinutes,
        MaximumTransactionAmount = item.MaximumTransactionAmount,
        MaximumAggregateAmount = item.MaximumAggregateAmount,
        MaximumTransactionCount = item.MaximumTransactionCount,
        MaximumOfflineAgeMinutes = item.MaximumOfflineAgeMinutes,
        AllowCashSale = item.AllowCashSale,
        AllowCashReceipt = item.AllowCashReceipt,
        AllowPartialPayment = item.AllowPartialPayment,
        AllowReturns = item.AllowReturns,
        AllowReversals = item.AllowReversals,
        AllowProvisionalReceipt = item.AllowProvisionalReceipt,
        AllowDayEndSubmissionWithPendingSync = item.AllowDayEndSubmissionWithPendingSync,
        RequireExternalReferenceForElectronicTender = item.RequireExternalReferenceForElectronicTender,
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    private static MobilePosTillDto MapTill(MobilePosTill item) => new()
    {
        Id = item.Id,
        MobilePosStoreId = item.MobilePosStoreId,
        StoreCode = item.MobilePosStore.Code,
        StoreName = item.MobilePosStore.Name,
        TillNumber = item.TillNumber,
        Name = item.Name,
        Status = item.Status,
        LiquidityAccountId = item.LiquidityAccountId,
        LiquidityAccountCode = item.LiquidityAccount.Code,
        CurrencyCode = item.LiquidityAccount.Currency,
        Notes = item.Notes,
        LastHeartbeatAtUtc = item.LastHeartbeatAtUtc,
        RowVersion = Convert.ToBase64String(item.RowVersion),
        PaymentMethods = item.PaymentMethods.OrderBy(value => value.DisplayOrder).Select(value => new MobilePosPaymentMethodDto
        {
            PaymentMethodId = value.PaymentMethodId,
            Code = value.PaymentMethod.Code ?? value.PaymentMethod.Name,
            Name = value.PaymentMethod.Name,
            Type = value.PaymentMethod.Type.ToString(),
            RequiresBankAccount = value.PaymentMethod.RequiresBankAccount,
            RequiresReference = value.PaymentMethod.RequiresReference,
            AllowOnline = value.AllowOnline,
            AllowOffline = value.AllowOffline,
            RequireExternalAuthorizationReference = value.RequireExternalAuthorizationReference,
            DisplayOrder = value.DisplayOrder
        }).ToList()
    };

    private static MobilePosDeviceDto MapDevice(MobilePosDevice item) => new()
    {
        Id = item.Id,
        DeviceName = item.DeviceName,
        Manufacturer = item.Manufacturer,
        Model = item.Model,
        OperatingSystemVersion = item.OperatingSystemVersion,
        AppVersion = item.AppVersion,
        PrinterAdapterKey = item.PrinterAdapterKey,
        ScannerAdapterKey = item.ScannerAdapterKey,
        Status = item.Status,
        RequestedByUserId = item.RequestedByUserId,
        RequestedAtUtc = item.RequestedAtUtc,
        MobilePosStoreId = item.MobilePosStoreId,
        StoreName = item.MobilePosStore?.Name,
        MobilePosTillId = item.MobilePosTillId,
        TillNumber = item.MobilePosTill?.TillNumber,
        ApprovedAtUtc = item.ApprovedAtUtc,
        RevokedAtUtc = item.RevokedAtUtc,
        StatusReason = item.StatusReason,
        LastSeenAtUtc = item.LastSeenAtUtc,
        LastSyncAtUtc = item.LastSyncAtUtc,
        RevocationEpoch = item.RevocationEpoch,
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    private void ApplyRowVersion(BaseEntity entity, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("The current row version is required.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); }
        catch (FormatException) { throw new InvalidOperationException("The row version is invalid."); }
        var property = _db.Entry(entity).Property<byte[]>("RowVersion");
        var current = property.CurrentValue;
        if (bytes.Length == 0 || current == null || current.Length == 0 || !current.AsSpan().SequenceEqual(bytes))
            throw new BusinessRuleException(
                "MOBILE_POS_VERSION_CONFLICT",
                "This Mobile POS record changed after it was opened. Reload the latest details, review your changes, and save again.",
                StatusCodes.Status409Conflict);
        property.OriginalValue = bytes;
    }

    private async Task AddAuditAsync(
        string action,
        string resource,
        Guid resourceId,
        object? before,
        object after)
    {
        await _db.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = UserId,
            Username = UserName,
            Action = action,
            Resource = resource,
            ResourceId = resourceId.ToString(),
            OldValues = before == null ? null : JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            IpAddress = string.IsNullOrWhiteSpace(_currentUser.IpAddress) ? "Unknown" : _currentUser.IpAddress,
            UserAgent = Clean(_currentUser.UserAgent),
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = UserId
        });
    }

    private static void ValidatePolicy(MobilePosOfflinePolicyUpsertDto dto)
    {
        if (dto.AuthorizationWindowMinutes is < 15 or > 1440)
            throw new InvalidOperationException("Offline authorization must be between 15 minutes and 24 hours.");
        if (dto.MaximumOfflineAgeMinutes is < 15 or > 1440)
            throw new InvalidOperationException("Maximum offline age must be between 15 minutes and 24 hours.");
        if (dto.MaximumTransactionAmount <= 0m || dto.MaximumAggregateAmount <= 0m || dto.MaximumTransactionCount <= 0)
            throw new InvalidOperationException("Configured offline limits must be positive.");
        if (dto.MaximumTransactionAmount.HasValue && dto.MaximumAggregateAmount.HasValue &&
            dto.MaximumTransactionAmount > dto.MaximumAggregateAmount)
            throw new InvalidOperationException("Maximum transaction amount cannot exceed the aggregate offline amount.");
    }

    private static string HashInstallationId(string installationId)
    {
        var normalized = Required(installationId, "Installation ID");
        if (normalized.Length < 16)
            throw new InvalidOperationException("Installation ID is too short.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string NormalizeEnvironment(string value)
    {
        if (value.Equals("Production", StringComparison.OrdinalIgnoreCase)) return "PRODUCTION";
        if (value.Equals("UAT", StringComparison.OrdinalIgnoreCase) || value.Equals("Staging", StringComparison.OrdinalIgnoreCase)) return "UAT";
        return "TEST";
    }

    private void StampUpdated(BaseEntity item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = UserName;
        item.LastModifiedById = UserId;
    }

    private static string Required(string? value, string label)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{label} is required.")
            : value.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateRequiredIds(params Guid[] values)
    {
        if (values.Any(value => value == Guid.Empty))
            throw new InvalidOperationException("All required selections must be provided.");
    }

    private static bool HasPermissions(IReadOnlySet<string> permissionSet, params string[] permissions)
        => permissions.All(permissionSet.Contains);

    private static object StoreAuditSnapshot(MobilePosStore item) => new
    {
        item.Code, item.Name, item.Status, item.CompanyProfileId, item.LocationId, item.WarehouseId,
        item.CurrencyCode, item.TimeZoneId, item.DefaultWalkInBusinessPartnerId,
        item.DefaultWalkInBusinessPartnerRoleId, item.OfflinePolicyId,
        dimensions = item.DimensionDefaults.Select(value => new
        {
            value.FinanceDimensionDefinitionId,
            value.FinanceDimensionValueId
        }).ToArray()
    };

    private static object TillAuditSnapshot(MobilePosTill item) => new
    {
        item.MobilePosStoreId, item.TillNumber, item.Name, item.Status, item.LiquidityAccountId,
        payments = item.PaymentMethods.Select(value => new
        {
            value.PaymentMethodId, value.AllowOnline, value.AllowOffline,
            value.RequireExternalAuthorizationReference, value.DisplayOrder
        }).ToArray()
    };

    private static object PolicyAuditSnapshot(MobilePosOfflinePolicy item) => new
    {
        item.Name, item.IsActive, item.AuthorizationWindowMinutes, item.MaximumTransactionAmount,
        item.MaximumAggregateAmount, item.MaximumTransactionCount, item.MaximumOfflineAgeMinutes,
        item.AllowCashSale, item.AllowCashReceipt, item.AllowPartialPayment, item.AllowReturns,
        item.AllowReversals, item.AllowProvisionalReceipt, item.AllowDayEndSubmissionWithPendingSync,
        item.RequireExternalReferenceForElectronicTender
    };

    private static object DeviceAuditSnapshot(MobilePosDevice item) => new
    {
        item.DeviceName, item.Manufacturer, item.Model, item.OperatingSystemVersion, item.AppVersion,
        item.Status, item.RequestedByUserId, item.MobilePosStoreId, item.MobilePosTillId,
        item.ApprovedByUserId, item.ApprovedAtUtc, item.RevokedByUserId, item.RevokedAtUtc,
        item.StatusReason, item.RevocationEpoch
    };
}
