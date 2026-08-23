using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;

// ⚠ An ALIAS, not `using ErpSystem.Core.Interfaces.Finance`. That namespace also declares an
// `IAssetTransferService`, and so does `ErpSystem.Core.Interfaces.HR` — the same collision that
// makes `AssetType` and `AssetTransfer` mean two different things depending on the file (build plan
// §3.3), now at the interface level. Importing the whole Finance namespace made every mention of
// `IAssetTransferService` in this file ambiguous, including the HR service declared at the bottom
// of it. Aliasing the one type we actually want keeps the collision from ever arising.
using IFixedAssetService = ErpSystem.Core.Interfaces.Finance.IFixedAssetService;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Who the caller is, and what that lets them do, for every asset service in this file.
/// </summary>
/// <remarks>
/// <para><b>Area 16, slice 1.</b> Before it, <c>AssetsController</c> carried a single bare
/// <c>[Authorize]</c> over 82 routes and no service checked anything beyond the tenant. Slice 0
/// proved what that meant by execution: a plain <c>Employee</c> could create asset types, create
/// company assets, dispose of a company asset, reach the requisition approval endpoint and read
/// every employee's assignments.</para>
///
/// <para><b>The controller's role attributes are the outer gate; these helpers are the inner
/// one, and both are needed.</b> An attribute cannot express "this employee, on this record" —
/// it does not know whose record it is — so the self-service routes carry only <c>[Authorize]</c>
/// and are gated here instead. Neither half is a gate on its own.</para>
///
/// <para><c>UnauthorizedAccessException</c> is deliberate: <c>GlobalExceptionHandlingMiddleware</c>
/// turns it into a <b>403 carrying its own message</b>, so a refusal can explain itself rather than
/// arriving as an opaque error the user cannot act on.</para>
/// </remarks>
internal static class AssetActor
{
    /// <summary>HR and the two admin roles act on anyone's assets. Nobody else does.</summary>
    /// <remarks>
    /// Both HR spellings are checked. The seeded role is renamed from "HR User" to "HR" on startup,
    /// but a tenant that has not run that migration still holds the old name.
    /// </remarks>
    internal static bool IsHr(ICurrentUserService user) =>
        user.IsInRole(Constants.Roles.Hr)
        || user.IsInRole(Constants.Roles.LegacyHrUser)
        || user.IsInRole(Constants.Roles.SuperAdmin)
        || user.IsInRole(Constants.Roles.TenantAdmin);

    /// <summary>The caller's own employee record, or null where their login is not linked to one.</summary>
    /// <remarks>
    /// Null is never treated as a match. An unlinked login — <c>admin</c> is one — is nobody's
    /// subject, so it fails every self check and passes only on the HR branch.
    /// </remarks>
    internal static Guid? CallerEmployeeId(ICurrentUserService user) =>
        user.EmployeeId is { } id && id != Guid.Empty ? id : null;

    /// <summary>The subject of the record, or HR acting for them.</summary>
    internal static void EnsureSelfOrHr(ICurrentUserService user, Guid subjectEmployeeId, string action)
    {
        if (IsHr(user)) return;
        if (CallerEmployeeId(user) == subjectEmployeeId) return;
        throw new UnauthorizedAccessException($"Only HR and the employee concerned can {action}.");
    }

    /// <summary>
    /// The subject of the record and nobody else — <b>HR included</b>.
    /// </summary>
    /// <remarks>
    /// Reserved for the acts that are a person's own signature rather than an administrative step.
    /// Acknowledging receipt of an asset is the only one today.
    /// </remarks>
    internal static void EnsureIsSubject(ICurrentUserService user, Guid subjectEmployeeId, string action)
    {
        if (CallerEmployeeId(user) == subjectEmployeeId) return;
        throw new UnauthorizedAccessException($"Only the employee concerned can {action}.");
    }
}

#region Asset Type Services

public class AssetTypeService : IAssetTypeService
{
    private readonly IAssetTypeRepository _assetTypeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetTypeService(
        IAssetTypeRepository assetTypeRepo, 
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _assetTypeRepo = assetTypeRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    // An asset type owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<AssetType> GetOwnedAssetTypeAsync(Guid id)
    {
        var entity = await _assetTypeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset type was found with id {id}.");
        return entity;
    }

    public async Task<AssetTypeDto?> GetByIdAsync(Guid id)
    {
        var entity = await _assetTypeRepo.GetByIdAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<AssetTypeDetailDto?> GetWithAttributesAsync(Guid id)
    {
        var entity = await _assetTypeRepo.GetWithAttributesAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDetailDto();
    }

    public async Task<IEnumerable<AssetTypeSummaryDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var types = await _assetTypeRepo.GetByTenantAsync(tenantId);
        var result = new List<AssetTypeSummaryDto>();
        foreach (var type in types)
        {
            var count = await _assetTypeRepo.GetAssetCountByTypeAsync(type.Id);
            result.Add(type.ToSummaryDto(count));
        }
        return result;
    }

    public async Task<PagedResult<AssetTypeSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null)
    {
        var tenantId = GetTenantId();
        var all = await _assetTypeRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(at => at.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) 
                || at.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        
        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        
        var dtos = new List<AssetTypeSummaryDto>();
        foreach (var type in items)
        {
            var count = await _assetTypeRepo.GetAssetCountByTypeAsync(type.Id);
            dtos.Add(type.ToSummaryDto(count));
        }
        
        return new PagedResult<AssetTypeSummaryDto> 
        { 
            Items = dtos, 
            TotalCount = totalCount, 
            Page = page, 
            PageSize = pageSize 
        };
    }

    public async Task<AssetTypeDto> CreateAsync(CreateAssetTypeDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = dto.ToEntity(tenantId, userId);
        await _assetTypeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AssetTypeDto> UpdateAsync(Guid id, UpdateAssetTypeDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedAssetTypeAsync(id);
        entity.UpdateEntity(dto, userId);
        await _assetTypeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAssetTypeAsync(id);
        await _assetTypeRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AssetTypeAttributeService : IAssetTypeAttributeService
{
    private readonly IAssetTypeAttributeRepository _attributeRepo;
    private readonly IAssetTypeRepository _assetTypeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetTypeAttributeService(
        IAssetTypeAttributeRepository attributeRepo,
        IAssetTypeRepository assetTypeRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _attributeRepo = attributeRepo;
        _assetTypeRepo = assetTypeRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetTypeAttribute> GetOwnedAttributeAsync(Guid id)
    {
        var entity = await _attributeRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset type attribute was found with id {id}.");
        return entity;
    }

    private async Task EnsureOwnedAssetTypeAsync(Guid assetTypeId)
    {
        var assetType = await _assetTypeRepo.GetByIdAsync(assetTypeId);
        if (assetType == null || assetType.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset type was found with id {assetTypeId}.");
    }

    public async Task<AssetTypeAttributeDto?> GetByIdAsync(Guid id)
    {
        // D-o(b). Was `GetByIdAsync`, which loads no navigations, so this read answered with a blank
        // assetTypeName while GetByAssetTypeIdAsync next door filled it in — the same DTO, two
        // fillings, and nothing in the payload to say which one a screen had.
        var entity = await _attributeRepo.GetWithTypeAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<IEnumerable<AssetTypeAttributeDto>> GetByAssetTypeIdAsync(Guid assetTypeId)
    {
        await EnsureOwnedAssetTypeAsync(assetTypeId);
        var attributes = await _attributeRepo.GetByAssetTypeIdAsync(assetTypeId);
        var tenantId = GetTenantId();
        return attributes.Where(a => a.TenantId == tenantId).ToDtoList();
    }

    public async Task<AssetTypeAttributeDto> CreateAsync(CreateAssetTypeAttributeDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        await EnsureOwnedAssetTypeAsync(dto.AssetTypeId);
        
        var entity = dto.ToEntity(tenantId, userId);
        await _attributeRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AssetTypeAttributeDto> UpdateAsync(Guid id, UpdateAssetTypeAttributeDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedAttributeAsync(id);
        entity.UpdateEntity(dto, userId);
        await _attributeRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAttributeAsync(id);
        await _attributeRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Company Asset Services

public class CompanyAssetService : ICompanyAssetService
{
    // AST-11 / decision D1. HR reads the fixed-asset register through FINANCE'S OWN SERVICE rather
    // than querying its tables, so the boundary is visible in the dependency: everything HR can see
    // is something Finance chose to expose, and nothing here can write. If this ever needs a method
    // Finance does not offer, that is a conversation with Finance, not a DbContext injection.

    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IAssetAttributeValueRepository _attributeValueRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CompanyAssetService> _logger;
    private readonly IFixedAssetService _fixedAssetService;

    public CompanyAssetService(
        ICompanyAssetRepository assetRepo,
        IAssetAttributeValueRepository attributeValueRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CompanyAssetService> logger,
        IFixedAssetService fixedAssetService)
    {
        _assetRepo = assetRepo;
        _attributeValueRepo = attributeValueRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _fixedAssetService = fixedAssetService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<CompanyAsset> GetOwnedAssetAsync(Guid id)
    {
        var entity = await _assetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {id}.");
        return entity;
    }

    public async Task<CompanyAssetDto?> GetByIdAsync(Guid id)
    {
        var entity = await _assetRepo.GetWithDetailsAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<CompanyAssetDetailDto?> GetWithDetailsAsync(Guid id)
    {
        var entity = await _assetRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;

        var dto = entity.ToDetailDto();

        // AST-11. Read the Finance figures LIVE rather than trusting the copy taken at link time —
        // a net book value moves at every depreciation run, so a stored copy is wrong within the
        // month. Only this read pays for the extra call; no list does.
        if (entity.FixedAssetId is { } fixedAssetId)
        {
            dto.FixedAsset = await ReadFixedAssetAsync(fixedAssetId);
        }

        return dto;
    }

    /// <summary>
    /// What Finance says about a linked fixed asset, or null where it can no longer be read.
    /// </summary>
    /// <remarks>
    /// ⚠ Null is a real answer, not a failure to handle: a fixed asset can be deleted in Finance
    /// while HR still holds the link, and an HR detail screen must still render. The link id stays
    /// on the DTO either way, so "linked to something that is gone" is visible rather than silently
    /// looking like an unlinked asset.
    /// </remarks>
    private async Task<FixedAssetLinkDto?> ReadFixedAssetAsync(Guid fixedAssetId)
    {
        var fa = await _fixedAssetService.GetByIdAsync(fixedAssetId);
        if (fa is null) return null;

        return new FixedAssetLinkDto
        {
            Id = fa.Id,
            AssetCode = fa.AssetCode,
            Name = fa.Name,
            CategoryName = fa.FixedAssetCategoryName,
            PurchaseDate = fa.PurchaseDate,
            AcquisitionCost = fa.AcquisitionCost,
            NetBookValue = fa.NetBookValue,
            StatusName = fa.Status.ToString(),
            CurrentCustodianName = fa.CurrentCustodianName,
            SerialNumber = fa.SerialNumber
        };
    }

    public async Task<IEnumerable<CompanyAssetSummaryDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var assets = await _assetRepo.GetByTenantAsync(tenantId);
        return assets.ToSummaryDtoList();
    }

    public async Task<PagedResult<CompanyAssetSummaryDto>> GetPagedAsync(
        int page, int pageSize, 
        string? searchTerm = null, 
        CompanyAssetStatus? status = null, 
        Guid? assetTypeId = null)
    {
        var tenantId = GetTenantId();
        var all = await _assetRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(a => a.AssetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || a.AssetNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || a.AssetTag.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

        if (status.HasValue)
            q = q.Where(a => a.Status == status.Value);

        if (assetTypeId.HasValue)
            q = q.Where(a => a.AssetTypeId == assetTypeId.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<CompanyAssetSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<CompanyAssetSummaryDto>> GetByStatusAsync(CompanyAssetStatus status)
    {
        var tenantId = GetTenantId();
        var assets = await _assetRepo.GetByStatusAsync(tenantId, status);
        return assets.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyAssetSummaryDto>> GetByAssetTypeAsync(Guid assetTypeId)
    {
        var tenantId = GetTenantId();
        var assets = await _assetRepo.GetByAssetTypeAsync(assetTypeId);
        return assets.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyAssetSummaryDto>> GetAvailableForAssignmentAsync()
    {
        var tenantId = GetTenantId();
        var assets = await _assetRepo.GetAvailableForAssignmentAsync(tenantId);
        return assets.ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyAssetSummaryDto>> GetByEmployeeAsync(Guid employeeId)
    {
        AssetActor.EnsureSelfOrHr(_currentUserService, employeeId,
            "see which company assets an employee holds");

        var tenantId = GetTenantId();
        var assets = await _assetRepo.GetByEmployeeAsync(employeeId);
        return assets.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<CompanyAssetSummaryDto>> GetDueForMaintenanceAsync(int daysAhead = 30)
    {
        var tenantId = GetTenantId();
        var assets = await _assetRepo.GetDueForMaintenanceAsync(tenantId, daysAhead);
        return assets.ToSummaryDtoList();
    }

    public async Task<CompanyAssetDto> CreateAsync(CreateCompanyAssetDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        _logger.LogInformation("Creating asset. Received {Count} attribute values", dto.AttributeValues?.Count ?? 0);
        dto.AssetNumber = string.IsNullOrWhiteSpace(dto.AssetNumber)
            ? $"AST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
            : dto.AssetNumber.Trim();

        var entity = dto.ToEntity(tenantId, userId);
        await _assetRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Asset {AssetId} created successfully", entity.Id);

        // Add attribute values if provided
        if (dto.AttributeValues != null && dto.AttributeValues.Any())
        {
            _logger.LogInformation("Processing {Count} attribute values for asset {AssetId}", dto.AttributeValues.Count, entity.Id);
            foreach (var attrDto in dto.AttributeValues)
            {
                _logger.LogInformation("Adding attribute value: {AttrId} = {Value}", attrDto.AssetTypeAttributeId, attrDto.Value);
                var attrValue = attrDto.ToEntity(tenantId, entity.Id, userId);
                await _attributeValueRepo.AddAsync(attrValue);
            }
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Successfully saved {Count} attribute values for asset {AssetId}", dto.AttributeValues.Count, entity.Id);
        }
        else
        {
            _logger.LogWarning("No attribute values to process for asset {AssetId}", entity.Id);
        }

        // ⚠ D-o. Re-read WITH the navigations before mapping. `entity` here was either just
        // constructed or loaded by `GetByIdAsync`, neither of which loads AssetType, Location, Unit
        // or CurrentAssignedTo — so mapping it produced a response whose assetTypeName, locationName
        // and unitName were blank, while the very next GET filled them in. A screen that renders
        // what it just saved showed empty columns until the user refreshed. Found by slice 2b, which
        // asserted a unit name on a create response and got null.
        var saved = await _assetRepo.GetWithDetailsAsync(entity.Id);
        return saved!.ToDto();
    }

    public async Task<IEnumerable<FixedAssetPickDto>> GetLinkableFixedAssetsAsync(string? searchTerm = null)
    {
        var tenantId = GetTenantId();

        var fixedAssets = await _fixedAssetService.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            fixedAssets = fixedAssets.Where(f =>
                f.AssetCode.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || f.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (f.SerialNumber ?? string.Empty).Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        // One pass over the HR register to work out what is already spoken for. Read from the
        // tenant list rather than per-asset so the picker is one query, not N.
        var linked = (await _assetRepo.GetByTenantAsync(tenantId))
            .Where(a => a.FixedAssetId.HasValue)
            .GroupBy(a => a.FixedAssetId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

        return fixedAssets.Select(f => new FixedAssetPickDto
        {
            Id = f.Id,
            AssetCode = f.AssetCode,
            Name = f.Name,
            CategoryName = f.FixedAssetCategoryName,
            SerialNumber = f.SerialNumber,
            Location = f.Location,
            NetBookValue = f.NetBookValue,
            StatusName = f.Status.ToString(),
            AlreadyLinked = linked.ContainsKey(f.Id),
            LinkedCompanyAssetId = linked.TryGetValue(f.Id, out var companyAssetId) ? companyAssetId : null
        }).OrderBy(f => f.AssetCode).ToList();
    }

    public async Task<CompanyAssetDto> CreateFromFixedAssetAsync(CreateAssetFromFixedAssetDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        var fa = await _fixedAssetService.GetByIdAsync(dto.FixedAssetId)
            ?? throw AssetsWorkflowException.NotFound(
                $"No fixed asset was found with id {dto.FixedAssetId} in the Fixed Assets module.");

        // One HR entry per fixed asset. Enforced here rather than by a unique index, because every
        // delete in this area is a SOFT delete: an index would hold the slot after an HR entry was
        // removed and refuse the re-link forever, with a constraint violation no user could read.
        // Doing it in code means the check can see IsDeleted, and can say what is wrong.
        var existing = (await _assetRepo.GetByTenantAsync(tenantId))
            .FirstOrDefault(a => a.FixedAssetId == dto.FixedAssetId);
        if (existing is not null)
        {
            throw AssetsWorkflowException.Conflict(
                $"Fixed asset {fa.AssetCode} is already registered in HR as {existing.AssetNumber} ({existing.AssetName}).");
        }

        var entity = new CompanyAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,

            // Identity is COPIED so the HR register reads sensibly on its own and in a list. It is
            // a snapshot for display; the live truth stays in Finance and is read through on the
            // detail screen.
            AssetNumber = fa.AssetCode,
            AssetName = fa.Name,
            Description = fa.Description,
            SerialNumber = fa.SerialNumber,
            PurchaseDate = DateOnly.FromDateTime(fa.PurchaseDate),
            PurchaseCost = fa.AcquisitionCost,
            LocationDetails = fa.Location,

            AssetTag = dto.AssetTag,
            AssetTypeId = dto.AssetTypeId,
            Condition = dto.Condition,
            LocationId = dto.LocationId,
            UnitId = dto.UnitId,
            IsAssignable = dto.IsAssignable,
            AdditionalRemarks = dto.AdditionalRemarks,

            Status = CompanyAssetStatus.Available,
            Source = AssetSource.FixedAssetsModule,
            FixedAssetId = fa.Id,

            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };

        await _assetRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _assetRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    /// <summary>
    /// Refuses an edit that would rewrite a figure Finance owns — AST-11, decision D1.
    /// </summary>
    /// <remarks>
    /// <b>Refuses rather than silently ignoring.</b> Quietly dropping the changed fields would let a
    /// screen show a save that appeared to work and did not, which is the harder defect to find.
    /// The message names the module to go to, because "you may not do that" without "here is where
    /// you can" is only half an answer.
    /// </remarks>
    private static void EnsureFinanceOwnedFieldsUnchanged(CompanyAsset entity, UpdateCompanyAssetDto dto)
    {
        if (entity.Source != AssetSource.FixedAssetsModule || entity.FixedAssetId is null) return;

        var changed = new List<string>();
        if (dto.AssetNumber != entity.AssetNumber) changed.Add("asset number");
        if (dto.PurchaseDate != entity.PurchaseDate) changed.Add("purchase date");
        if (dto.PurchaseCost != entity.PurchaseCost) changed.Add("purchase cost");

        if (changed.Count == 0) return;

        throw AssetsWorkflowException.InvalidState(
            $"This asset comes from the Fixed Assets module, which owns its {string.Join(", ", changed)}. " +
            "Change it there; everything else on this form can be edited in HR.");
    }

    public async Task<CompanyAssetDto> UpdateAsync(Guid id, UpdateCompanyAssetDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        _logger.LogInformation("Updating asset {AssetId}. Received {Count} attribute values", id, dto.AttributeValues?.Count ?? 0);
        
        var entity = await GetOwnedAssetAsync(id);

        EnsureFinanceOwnedFieldsUnchanged(entity, dto);

        entity.UpdateEntity(dto, userId);
        await _assetRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        // Update attribute values if provided
        if (dto.AttributeValues != null)
        {
            _logger.LogInformation("Processing attribute values update for asset {AssetId}", id);
            
            // Get existing attribute values
            var existingValues = await _attributeValueRepo.GetByAssetIdAsync(id);
            existingValues = existingValues.Where(v => v.TenantId == tenantId).ToList();
            _logger.LogInformation("Found {Count} existing attribute values for asset {AssetId}", existingValues.Count(), id);
            
            // Delete all existing attribute values
            foreach (var existingValue in existingValues)
            {
                await _attributeValueRepo.DeleteAsync(existingValue.Id);
            }
            
            // Add new attribute values
            if (dto.AttributeValues.Any())
            {
                _logger.LogInformation("Adding {Count} new attribute values for asset {AssetId}", dto.AttributeValues.Count, id);
                foreach (var attrDto in dto.AttributeValues)
                {
                    _logger.LogInformation("Adding attribute value: {AttrId} = {Value}", attrDto.AssetTypeAttributeId, attrDto.Value);
                    var attrValue = attrDto.ToEntity(tenantId, id, userId);
                    await _attributeValueRepo.AddAsync(attrValue);
                }
            }
            
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Successfully updated attribute values for asset {AssetId}", id);
        }
        else
        {
            _logger.LogInformation("No attribute values to update for asset {AssetId}", id);
        }

        // ⚠ D-o. Re-read WITH the navigations before mapping. `entity` here was either just
        // constructed or loaded by `GetByIdAsync`, neither of which loads AssetType, Location, Unit
        // or CurrentAssignedTo — so mapping it produced a response whose assetTypeName, locationName
        // and unitName were blank, while the very next GET filled them in. A screen that renders
        // what it just saved showed empty columns until the user refreshed. Found by slice 2b, which
        // asserted a unit name on a create response and got null.
        var saved = await _assetRepo.GetWithDetailsAsync(entity.Id);
        return saved!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAssetAsync(id);
        await _assetRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DisposeAssetAsync(DisposeAssetDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedAssetAsync(dto.AssetId);

        // Disposal accounting belongs to Finance (decision D1). Disposing here would leave the
        // fixed-asset register still carrying the thing at book value, and there is no reason for
        // HR to be the place that happens.
        if (entity.Source == AssetSource.FixedAssetsModule && entity.FixedAssetId.HasValue)
        {
            throw AssetsWorkflowException.InvalidState(
                "This asset is capitalised in the Fixed Assets module, which owns its disposal. " +
                "Dispose of it there; the HR record follows.");
        }

        entity.Status = CompanyAssetStatus.Disposed;
        entity.DisposalDate = dto.DisposalDate;
        entity.DisposalMethod = dto.DisposalMethod;
        entity.DisposalNotes = dto.DisposalNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _assetRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AssetAttributeValueService : IAssetAttributeValueService
{
    private readonly IAssetAttributeValueRepository _valueRepo;
    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetAttributeValueService(
        IAssetAttributeValueRepository valueRepo,
        ICompanyAssetRepository assetRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _valueRepo = valueRepo;
        _assetRepo = assetRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetAttributeValue> GetOwnedValueAsync(Guid id)
    {
        var entity = await _valueRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset attribute value was found with id {id}.");
        return entity;
    }

    private async Task EnsureOwnedAssetAsync(Guid assetId)
    {
        var asset = await _assetRepo.GetByIdAsync(assetId);
        if (asset == null || asset.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {assetId}.");
    }

    public async Task<AssetAttributeValueDto?> GetByIdAsync(Guid id)
    {
        // D-o(b). Was `GetByIdAsync`, which loads no navigations. Without the AssetTypeAttribute
        // this read lost the attribute's name AND reported the wrong dataType — the DTO projects the
        // type off the navigation, so a null one silently became the enum's default rather than the
        // value's actual type. The list read beside it had the Include all along.
        var entity = await _valueRepo.GetWithAttributeAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<IEnumerable<AssetAttributeValueDto>> GetByAssetIdAsync(Guid assetId)
    {
        await EnsureOwnedAssetAsync(assetId);
        var values = await _valueRepo.GetByAssetIdAsync(assetId);
        var tenantId = GetTenantId();
        return values.Where(v => v.TenantId == tenantId).ToDtoList();
    }

    public async Task<AssetAttributeValueDto> CreateAsync(Guid assetId, CreateAssetAttributeValueDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        await EnsureOwnedAssetAsync(assetId);
        
        var entity = dto.ToEntity(tenantId, assetId, userId);
        await _valueRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task<AssetAttributeValueDto> UpdateAsync(Guid id, UpdateAssetAttributeValueDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedValueAsync(id);
        entity.UpdateEntity(dto, userId);
        await _valueRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedValueAsync(id);
        await _valueRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Asset Assignment Services

public class AssetAssignmentService : IAssetAssignmentService
{
    private readonly IAssetAssignmentRepository _assignmentRepo;
    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetAssignmentService(
        IAssetAssignmentRepository assignmentRepo,
        ICompanyAssetRepository assetRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _assignmentRepo = assignmentRepo;
        _assetRepo = assetRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetAssignment> GetOwnedAssignmentAsync(Guid id)
    {
        var entity = await _assignmentRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset assignment was found with id {id}.");
        return entity;
    }

    private async Task<CompanyAsset> GetOwnedAssetAsync(Guid id)
    {
        var entity = await _assetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {id}.");
        return entity;
    }

    public async Task<AssetAssignmentDto?> GetByIdAsync(Guid id)
    {
        var entity = await _assignmentRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;

        // An assignment names a person and what they were given, in what condition, on what terms,
        // and what they owe if they break it. Its holder may read it; nobody else outside HR may.
        // The refusal is a 403 rather than a 404 on purpose — the record exists and the caller is
        // being told they are not entitled to it, which is a different fact from "no such thing".
        AssetActor.EnsureSelfOrHr(_currentUserService, entity.EmployeeId, "read an asset assignment");

        return entity.ToDto();
    }

    public async Task<IEnumerable<AssetAssignmentSummaryDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var assignments = await _assignmentRepo.GetByTenantAsync(tenantId);
        return assignments.ToSummaryDtoList();
    }

    public async Task<PagedResult<AssetAssignmentSummaryDto>> GetPagedAsync(
        int page, int pageSize, 
        string? searchTerm = null, 
        AssignmentStatus? status = null)
    {
        var tenantId = GetTenantId();
        var all = await _assignmentRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(a => a.AssignmentNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (a.Asset != null && a.Asset.AssetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                || (a.Employee != null && (a.Employee.FirstName + " " + a.Employee.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (status.HasValue)
            q = q.Where(a => a.Status == status.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AssetAssignmentSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AssetAssignmentSummaryDto>> GetByAssetIdAsync(Guid assetId)
    {
        await GetOwnedAssetAsync(assetId);
        var tenantId = GetTenantId();
        var assignments = await _assignmentRepo.GetByAssetIdAsync(assetId);
        return assignments.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<AssetAssignmentDto?> GetActiveAssignmentForAssetAsync(Guid assetId)
    {
        await GetOwnedAssetAsync(assetId);
        var assignment = await _assignmentRepo.GetActiveAssignmentForAssetAsync(assetId);
        return assignment == null || assignment.TenantId != GetTenantId() ? null : assignment.ToDto();
    }

    public async Task<IEnumerable<AssetAssignmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId)
    {
        AssetActor.EnsureSelfOrHr(_currentUserService, employeeId,
            "list an employee's asset assignments");

        var tenantId = GetTenantId();
        var assignments = await _assignmentRepo.GetByEmployeeIdAsync(employeeId);
        return assignments.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AssetAssignmentSummaryDto>> GetActiveAssignmentsForEmployeeAsync(Guid employeeId)
    {
        AssetActor.EnsureSelfOrHr(_currentUserService, employeeId,
            "list what an employee currently holds");

        var tenantId = GetTenantId();
        var assignments = await _assignmentRepo.GetActiveAssignmentsForEmployeeAsync(employeeId);
        return assignments.Where(a => a.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AssetAssignmentSummaryDto>> GetOverdueAssignmentsAsync()
    {
        var tenantId = GetTenantId();
        var assignments = await _assignmentRepo.GetOverdueAssignmentsAsync(tenantId);
        return assignments.ToSummaryDtoList();
    }

    public async Task<AssetAssignmentDto> CreateAsync(CreateAssetAssignmentDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        var asset = await GetOwnedAssetAsync(dto.AssetId);
        
        // Generate assignment number
        var assignmentNumber = $"ASN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var entity = dto.ToEntity(tenantId, userId, assignmentNumber);
        await _assignmentRepo.AddAsync(entity);

        // Update the asset status
        asset.IsCurrentlyAssigned = true;
        asset.CurrentAssignedToId = dto.EmployeeId;
        asset.Status = CompanyAssetStatus.Assigned;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = userId.ToString();
        await _assetRepo.UpdateAsync(asset);

        await _unitOfWork.SaveChangesAsync();

        var created = await _assignmentRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AssetAssignmentDto> UpdateAsync(Guid id, UpdateAssetAssignmentDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedAssignmentAsync(id);
        entity.UpdateEntity(dto, userId);
        await _assignmentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        
        var updated = await _assignmentRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAssignmentAsync(id);
        await _assignmentRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task AcknowledgeAssignmentAsync(AcknowledgeAssignmentDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedAssignmentAsync(dto.AssignmentId);

        // ⚠ AREA 16 D-b. Acknowledgement is the one act on this record that only its subject may
        // perform: it is the employee's word that they received the asset and accept the terms
        // printed on the assignment. Slice 0 proved that until this line ANY authenticated caller
        // could sign for anybody — a third party acknowledged an assignment they had nothing to do
        // with, and the record then read as though the holder had.
        //
        // HR deliberately gets no override. Where an employee cannot reach the portal the answer is
        // the printed, physically signed responsibility form (AST-5, slice 5) recorded as what it
        // is — not HR quietly ticking the box in the employee's name, which is the same defect with
        // better manners.
        AssetActor.EnsureIsSubject(_currentUserService, entity.EmployeeId,
            "acknowledge receipt of an asset");

        if (entity.Status != AssignmentStatus.Active)
            throw AssetsWorkflowException.InvalidState(
                "Only an active assignment can be acknowledged; this one has already been closed.");

        entity.EmployeeAcknowledged = true;
        entity.AcknowledgementDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _assignmentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ReturnAssetAsync(ReturnAssetDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedAssignmentAsync(dto.AssignmentId);

        entity.Status = AssignmentStatus.Returned;
        entity.ReturnDate = DateTime.UtcNow;
        entity.ConditionAtReturn = dto.ConditionAtReturn;
        entity.ReturnNotes = dto.ReturnNotes;
        entity.ReturnedInGoodCondition = dto.ReturnedInGoodCondition;
        entity.ReturnedToId = dto.ReturnedToId;
        entity.DamageReported = dto.DamageReported;
        entity.DamageDescription = dto.DamageDescription;
        entity.EmployeeLiable = dto.EmployeeLiable;
        entity.RepairCost = dto.RepairCost;
        entity.ReplacementCost = dto.ReplacementCost;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _assignmentRepo.UpdateAsync(entity);

        // Update asset status
        var asset = await GetOwnedAssetAsync(entity.AssetId);
        asset.IsCurrentlyAssigned = false;
        asset.CurrentAssignedToId = null;
        asset.Status = dto.DamageReported ? CompanyAssetStatus.Damaged : CompanyAssetStatus.Available;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = userId.ToString();
        await _assetRepo.UpdateAsync(asset);

        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Asset Maintenance Services

public class AssetMaintenanceService : IAssetMaintenanceService
{
    private readonly IAssetMaintenanceRepository _maintenanceRepo;
    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetMaintenanceService(
        IAssetMaintenanceRepository maintenanceRepo,
        ICompanyAssetRepository assetRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _maintenanceRepo = maintenanceRepo;
        _assetRepo = assetRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetMaintenance> GetOwnedMaintenanceAsync(Guid id)
    {
        var entity = await _maintenanceRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No maintenance record was found with id {id}.");
        return entity;
    }

    private async Task<CompanyAsset> GetOwnedAssetAsync(Guid id)
    {
        var entity = await _assetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {id}.");
        return entity;
    }

    public async Task<AssetMaintenanceDto?> GetByIdAsync(Guid id)
    {
        var entity = await _maintenanceRepo.GetWithDetailsAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<IEnumerable<AssetMaintenanceSummaryDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var maintenances = await _maintenanceRepo.GetByTenantAsync(tenantId);
        return maintenances.ToSummaryDtoList();
    }

    public async Task<PagedResult<AssetMaintenanceSummaryDto>> GetPagedAsync(
        int page, int pageSize, 
        string? searchTerm = null, 
        MaintenanceStatus? status = null)
    {
        var tenantId = GetTenantId();
        var all = await _maintenanceRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(m => m.MaintenanceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (m.Asset != null && m.Asset.AssetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (status.HasValue)
            q = q.Where(m => m.Status == status.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AssetMaintenanceSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AssetMaintenanceSummaryDto>> GetByAssetIdAsync(Guid assetId)
    {
        await GetOwnedAssetAsync(assetId);
        var tenantId = GetTenantId();
        var maintenances = await _maintenanceRepo.GetByAssetIdAsync(assetId);
        return maintenances.Where(m => m.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AssetMaintenanceSummaryDto>> GetScheduledMaintenanceAsync(DateTime from, DateTime to)
    {
        var tenantId = GetTenantId();
        var maintenances = await _maintenanceRepo.GetScheduledMaintenanceAsync(tenantId, from, to);
        return maintenances.ToSummaryDtoList();
    }

    public async Task<AssetMaintenanceDto> CreateAsync(CreateAssetMaintenanceDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        var asset = await GetOwnedAssetAsync(dto.AssetId);
        
        // Generate maintenance number
        var maintenanceNumber = $"MNT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var entity = dto.ToEntity(tenantId, userId, maintenanceNumber);
        await _maintenanceRepo.AddAsync(entity);

        // Update asset status if maintenance is in progress
        if (dto.Status == MaintenanceStatus.InProgress)
        {
            asset.Status = CompanyAssetStatus.InMaintenance;
            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = userId.ToString();
            await _assetRepo.UpdateAsync(asset);
        }

        await _unitOfWork.SaveChangesAsync();

        var created = await _maintenanceRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AssetMaintenanceDto> UpdateAsync(Guid id, UpdateAssetMaintenanceDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedMaintenanceAsync(id);
        entity.UpdateEntity(dto, userId);
        await _maintenanceRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _maintenanceRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedMaintenanceAsync(id);
        await _maintenanceRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CompleteMaintenanceAsync(Guid id, string? completionNotes = null)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedMaintenanceAsync(id);

        entity.Status = MaintenanceStatus.Completed;
        entity.Notes = completionNotes ?? entity.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _maintenanceRepo.UpdateAsync(entity);

        // Update asset status and maintenance dates
        var asset = await GetOwnedAssetAsync(entity.AssetId);
        asset.Status = CompanyAssetStatus.Available;
        asset.LastMaintenanceDate = DateOnly.FromDateTime(entity.MaintenanceDate);
        if (asset.MaintenanceIntervalDays.HasValue)
        {
            asset.NextMaintenanceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(asset.MaintenanceIntervalDays.Value));
        }
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = userId.ToString();
        await _assetRepo.UpdateAsync(asset);

        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Asset Attachment Services

public class AssetImageService : IAssetImageService
{
    private readonly IAssetImageRepository _imageRepo;
    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;

    public AssetImageService(
        IAssetImageRepository imageRepo,
        ICompanyAssetRepository assetRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService)
    {
        _imageRepo = imageRepo;
        _assetRepo = assetRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetImage> GetOwnedImageAsync(Guid id)
    {
        var entity = await _imageRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset image was found with id {id}.");
        return entity;
    }

    private async Task EnsureOwnedAssetAsync(Guid assetId)
    {
        var asset = await _assetRepo.GetByIdAsync(assetId);
        if (asset == null || asset.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {assetId}.");
    }

    public async Task<AssetImageDto?> GetByIdAsync(Guid id)
    {
        var entity = await _imageRepo.GetByIdAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<IEnumerable<AssetImageDto>> GetByAssetIdAsync(Guid assetId)
    {
        await EnsureOwnedAssetAsync(assetId);
        var images = await _imageRepo.GetByAssetIdAsync(assetId);
        var tenantId = GetTenantId();
        return images.Where(i => i.TenantId == tenantId).ToDtoList();
    }

    public async Task<AssetImageDto> CreateAsync(Guid assetId, CreateAssetImageDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        await EnsureOwnedAssetAsync(assetId);

        var entity = dto.ToEntity(tenantId, assetId, userId);
        await _imageRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedImageAsync(id);
        await _fileStorageService.DeleteFileAsync(entity.FilePath);
        await _imageRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class AssetAttachmentService : IAssetAttachmentService
{
    private readonly IAssetAttachmentRepository _attachmentRepo;
    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetAttachmentService(
        IAssetAttachmentRepository attachmentRepo,
        ICompanyAssetRepository assetRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _attachmentRepo = attachmentRepo;
        _assetRepo = assetRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetAttachment> GetOwnedAttachmentAsync(Guid id)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No attachment was found with id {id}.");
        return entity;
    }

    private async Task EnsureOwnedAssetAsync(Guid assetId)
    {
        var asset = await _assetRepo.GetByIdAsync(assetId);
        if (asset == null || asset.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {assetId}.");
    }

    public async Task<AssetAttachmentDto?> GetByIdAsync(Guid id)
    {
        var entity = await _attachmentRepo.GetByIdAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<IEnumerable<AssetAttachmentDto>> GetByAssetIdAsync(Guid assetId)
    {
        await EnsureOwnedAssetAsync(assetId);
        var attachments = await _attachmentRepo.GetByAssetIdAsync(assetId);
        var tenantId = GetTenantId();
        return attachments.Where(a => a.TenantId == tenantId).ToDtoList();
    }

    public async Task<AssetAttachmentDto> CreateAsync(Guid assetId, CreateAssetAttachmentDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        await EnsureOwnedAssetAsync(assetId);
        
        var entity = dto.ToEntity(tenantId, assetId, userId);
        await _attachmentRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedAttachmentAsync(id);
        await _attachmentRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Asset Requisition Services

public class AssetRequisitionService : IAssetRequisitionService
{
    private readonly IAssetRequisitionRepository _requisitionRepo;
    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IAssetAssignmentRepository _assignmentRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetRequisitionService(
        IAssetRequisitionRepository requisitionRepo,
        ICompanyAssetRepository assetRepo,
        IAssetAssignmentRepository assignmentRepo,
        IEmployeeRepository employeeRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _requisitionRepo = requisitionRepo;
        _assetRepo = assetRepo;
        _assignmentRepo = assignmentRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// The caller's own employee record — the actor for every stamp on this surface.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Defect D-k lived exactly here.</b> <c>ApproveAsync</c> and <c>FulfillAsync</c> both
    /// carried the literal <c>Guid.Parse("D1D0261F-934D-4809-95EF-CD76156694A5")</c> behind a
    /// <c>// TODO: Replace with actual employee ID lookup</c>. <c>ApprovedById</c> and
    /// <c>FulfilledById</c> are <b>Employee</b> foreign keys and that employee has never existed on
    /// this database, so SQL rejected every UPDATE with error 547 and both endpoints answered 500 —
    /// for every actor, on every tenant, since the port. Because approval is fulfilment's
    /// precondition, the requisition pipeline had never once run end to end.
    ///
    /// <para>The refusal here is deliberate and explicit rather than a silent null: an approver who
    /// is not an employee cannot be recorded as one, and quietly stamping nobody would recreate the
    /// same hole with better manners.</para>
    /// </remarks>
    private Guid RequireCallerEmployeeId(string action)
    {
        return AssetActor.CallerEmployeeId(_currentUserService)
            ?? throw new UnauthorizedAccessException(
                $"Your user account is not linked to an employee record, so it cannot {action}.");
    }

    /// <summary>
    /// Resolves and authorises the employee a request is being raised FOR — AST-6b.
    /// </summary>
    /// <remarks>
    /// <para>HR may raise a request for anybody. Anyone else may raise one only for themselves or
    /// for an employee whose <c>ManagerId</c> names them.</para>
    ///
    /// <para>⚠ <b>The manager branch is correct and will rarely fire.</b> Measured on this database
    /// while writing it: <b>181 of 6,822</b> live employees carry a <c>ManagerId</c> — 2.7%. The rule
    /// is written against the data model rather than against the data, so it starts working the day
    /// the org chart is maintained; until then almost every on-behalf request comes through HR. That
    /// is a data gap, not a rule to weaken, and it is the same unmaintained-org-data seam that keeps
    /// FR-HR-080/181 deferred.</para>
    /// </remarks>
    private async Task<Guid?> ResolveBeneficiaryAsync(Guid? requested, Guid requesterEmployeeId, Guid tenantId)
    {
        if (requested is not { } beneficiaryId || beneficiaryId == requesterEmployeeId) return null;

        var beneficiary = await _employeeRepo.GetByIdAsync(beneficiaryId);
        if (beneficiary is null || beneficiary.TenantId != tenantId || beneficiary.IsDeleted)
            throw AssetsWorkflowException.NotFound(
                $"No employee was found with id {beneficiaryId}.");

        if (!AssetActor.IsHr(_currentUserService) && beneficiary.ManagerId != requesterEmployeeId)
            throw new UnauthorizedAccessException(
                "You can raise a request on behalf of an employee only if you are their recorded "
                + "line manager. HR can raise one for anybody.");

        return beneficiaryId;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetRequisition> GetOwnedRequisitionAsync(Guid id)
    {
        var entity = await _requisitionRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset requisition was found with id {id}.");
        return entity;
    }

    private async Task<CompanyAsset> GetOwnedAssetAsync(Guid id)
    {
        var entity = await _assetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {id}.");
        return entity;
    }

    public async Task<AssetRequisitionDto?> GetByIdAsync(Guid id)
    {
        var entity = await _requisitionRepo.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId()) return null;

        // Self-or-HR on BOTH actors: the person a request was raised for may read it as surely as
        // the person who raised it. Reading only RequestedById would hide an employee's own
        // requisition from them the moment somebody raised it on their behalf.
        if (!AssetActor.IsHr(_currentUserService))
        {
            var caller = AssetActor.CallerEmployeeId(_currentUserService);
            if (caller != entity.RequestedById && caller != entity.BeneficiaryEmployeeId)
                throw new UnauthorizedAccessException(
                    "Only HR and the employee concerned can read an asset requisition.");
        }

        var dto = entity.ToDto();

        // D-e. What the requisition produced, read from the assignments that cite it — the only
        // record of the fact now that the single-asset column is gone.
        var fulfilments = await _assignmentRepo.GetByRequisitionIdAsync(entity.Id);
        dto.FulfilledWith = fulfilments
            .Where(a => a.TenantId == entity.TenantId)
            .Select(a => new RequisitionFulfilmentDto
            {
                AssignmentId = a.Id,
                AssignmentNumber = a.AssignmentNumber,
                AssetId = a.AssetId,
                AssetNumber = a.Asset?.AssetNumber ?? string.Empty,
                AssetName = a.Asset?.AssetName ?? string.Empty,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee != null ? $"{a.Employee.FirstName} {a.Employee.LastName}" : string.Empty,
                AssignmentDate = a.AssignmentDate
            })
            .ToList();

        return dto;
    }

    public async Task<IEnumerable<AssetRequisitionSummaryDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var requisitions = await _requisitionRepo.GetByTenantAsync(tenantId);
        return requisitions.ToSummaryDtoList();
    }

    public async Task<PagedResult<AssetRequisitionSummaryDto>> GetPagedAsync(
        int page, int pageSize, 
        string? searchTerm = null, 
        AssetRequisitionStatus? status = null)
    {
        var tenantId = GetTenantId();
        var all = await _requisitionRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(r => r.RequisitionNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (r.RequestedBy != null && (r.RequestedBy.FirstName + " " + r.RequestedBy.LastName).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (status.HasValue)
            q = q.Where(r => r.Status == status.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AssetRequisitionSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AssetRequisitionSummaryDto>> GetByRequestedByIdAsync(Guid employeeId)
    {
        AssetActor.EnsureSelfOrHr(_currentUserService, employeeId,
            "list the asset requisitions an employee has raised");

        var tenantId = GetTenantId();
        var requisitions = await _requisitionRepo.GetByRequestedByIdAsync(employeeId);
        return requisitions.Where(r => r.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AssetRequisitionSummaryDto>> GetPendingApprovalsAsync()
    {
        var tenantId = GetTenantId();
        var requisitions = await _requisitionRepo.GetPendingApprovalsAsync(tenantId);
        return requisitions.ToSummaryDtoList();
    }

    public async Task<AssetRequisitionDto> CreateAsync(CreateAssetRequisitionDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        // Generate requisition number
        var requisitionNumber = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var requestedById = RequireCallerEmployeeId("raise an asset requisition");
        var beneficiaryId = await ResolveBeneficiaryAsync(dto.BeneficiaryEmployeeId, requestedById, tenantId);
        
        var entity = dto.ToEntity(tenantId, requestedById, userId, requisitionNumber);
        entity.BeneficiaryEmployeeId = beneficiaryId;
        await _requisitionRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _requisitionRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AssetRequisitionDto> UpdateAsync(Guid id, UpdateAssetRequisitionDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedRequisitionAsync(id);

        // The requester may correct their own request; HR may correct anyone's. The status rule
        // below is a separate question from the actor rule above and neither substitutes for the
        // other: an employee editing someone else's draft is refused here, not there.
        AssetActor.EnsureSelfOrHr(_currentUserService, entity.RequestedById, "edit an asset requisition");

        if (entity.Status != AssetRequisitionStatus.Submitted && entity.Status != AssetRequisitionStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                "Only a draft or submitted requisition can be edited; this one has already been decided.");

        entity.UpdateEntity(dto, userId);
        await _requisitionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _requisitionRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedRequisitionAsync(id);

        AssetActor.EnsureSelfOrHr(_currentUserService, entity.RequestedById,
            "withdraw an asset requisition");

        // A decided requisition is a record of a decision. Withdrawing it after the fact would
        // erase the approval or the rejection along with the request, so it stops being possible
        // once anyone has answered it.
        if (entity.Status != AssetRequisitionStatus.Submitted && entity.Status != AssetRequisitionStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                "Only a draft or submitted requisition can be withdrawn; this one has already been decided.");

        await _requisitionRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ApproveAsync(Guid id, ApproveAssetRequisitionDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedRequisitionAsync(id);

        // D-k. The approver is the person approving. See RequireCallerEmployeeId for what was here.
        var approverId = RequireCallerEmployeeId("approve an asset requisition");

        entity.Status = AssetRequisitionStatus.Approved;
        entity.ApprovedById = approverId;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.ApprovalComments = dto.ApprovalComments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid id, RejectAssetRequisitionDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedRequisitionAsync(id);

        entity.Status = AssetRequisitionStatus.Rejected;
        entity.RejectedDate = DateTime.UtcNow;
        entity.RejectionReason = dto.RejectionReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _requisitionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task FulfillAsync(Guid id, FulfillAssetRequisitionDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        var tenantId = GetTenantId();
        
        // D-k, second site.
        var fulfilledById = RequireCallerEmployeeId("fulfil an asset requisition");
        
        var requisition = await GetOwnedRequisitionAsync(id);

        if (requisition.Status != AssetRequisitionStatus.Approved)
            throw AssetsWorkflowException.InvalidState(
                "Only an approved requisition can be fulfilled; this one has not been approved.");

        if (dto.AssignedAssetIds == null || dto.AssignedAssetIds.Count == 0)
            throw AssetsWorkflowException.Invalid("At least one asset must be assigned.");

        if (dto.AssignedAssetIds.Count > requisition.Quantity)
            throw AssetsWorkflowException.Invalid(
                $"Cannot assign more assets ({dto.AssignedAssetIds.Count}) than the {requisition.Quantity} requested.");

        // Validate all assets exist, are owned, and are available
        var assets = new List<CompanyAsset>();
        foreach (var assetId in dto.AssignedAssetIds)
        {
            var asset = await GetOwnedAssetAsync(assetId);
            
            // ⚠ Order matters, and the ported order made one of these rules unreachable.
            // Assigning an asset sets BOTH IsCurrentlyAssigned and Status = Assigned, so asking
            // about status first meant an asset already in somebody's hands was always refused
            // as "not available; its status is Assigned" - true, but it buries the fact the user
            // needs and it left the Conflict rule below dead. The specific question goes first;
            // the status rule then answers for what it is actually about: disposed, damaged,
            // lost, in maintenance.
            if (asset.IsCurrentlyAssigned)
                throw AssetsWorkflowException.Conflict(
                    $"Asset {asset.AssetNumber} is already assigned to someone.");
            
            if (asset.Status != CompanyAssetStatus.Available)
                throw AssetsWorkflowException.InvalidState(
                    $"Asset {asset.AssetNumber} is not available; its status is {asset.Status}.");
            
            assets.Add(asset);
        }

        // Who actually receives them: the beneficiary where the request was raised on someone's
        // behalf, the requester otherwise. The same rule the DTO exposes as ForEmployeeId, so a
        // screen and the server cannot disagree about it.
        var issueTo = requisition.BeneficiaryEmployeeId ?? requisition.RequestedById;

        // Create asset assignments and update assets
        foreach (var asset in assets)
        {
            // Create assignment
            var assignment = new AssetAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AssignmentNumber = $"ASN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}",
                AssetId = asset.Id,

                // AST-6b. The asset goes to the BENEFICIARY, not to whoever filled in the form.
                // Before the beneficiary column existed these were always the same person, so the
                // ported code could not have been wrong — it simply had no way to be right.
                EmployeeId = issueTo,

                // D-e. Each assignment cites the requisition that produced it. This replaces the
                // single AssignedAssetId column, which remembered one asset however many were
                // issued against the quantity.
                RequisitionId = requisition.Id,
                AssignmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Type = AssignmentType.Permanent,
                Purpose = AssignmentPurpose.RegularWork,
                AssignmentNotes = $"Fulfilled from requisition {requisition.RequisitionNumber}",
                IsPrimaryUser = true,
                ConditionAtAssignment = asset.Condition,
                ApprovedById = fulfilledById,
                ApprovalDate = DateTime.UtcNow,
                ResponsibleForLoss = true,
                ResponsibleForDamage = true,
                Status = AssignmentStatus.Active,
                EmployeeAcknowledged = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = fulfilledById.ToString()
            };

            await _assignmentRepo.AddAsync(assignment);

            // Update asset
            asset.Status = CompanyAssetStatus.Assigned;
            asset.IsCurrentlyAssigned = true;
            asset.CurrentAssignedToId = issueTo;
            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = fulfilledById.ToString();
            
            await _assetRepo.UpdateAsync(asset);
        }

        // Update requisition
        requisition.Status = AssetRequisitionStatus.Fulfilled;
        requisition.IsFulfilled = true;
        requisition.FulfilledDate = DateTime.UtcNow;
        requisition.FulfilledById = fulfilledById;
        
        // D-e. Nothing is written back here any more. What the requisition produced is recorded on
        // the assignments themselves, each citing RequisitionId, so a fulfilment of three assets is
        // three facts rather than one fact and two silences.
        
        requisition.UpdatedAt = DateTime.UtcNow;
        requisition.UpdatedBy = fulfilledById.ToString();

        await _requisitionRepo.UpdateAsync(requisition);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion

#region Asset Transfer Services

public class AssetTransferService : IAssetTransferService
{
    private readonly IAssetTransferRepository _transferRepo;
    private readonly ICompanyAssetRepository _assetRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetTransferService(
        IAssetTransferRepository transferRepo,
        ICompanyAssetRepository assetRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _transferRepo = transferRepo;
        _assetRepo = assetRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId.Value;
    }

    private async Task<AssetTransfer> GetOwnedTransferAsync(Guid id)
    {
        var entity = await _transferRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset transfer was found with id {id}.");
        return entity;
    }

    private async Task<CompanyAsset> GetOwnedAssetAsync(Guid id)
    {
        var entity = await _assetRepo.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw AssetsWorkflowException.NotFound(
                $"No asset was found with id {id}.");
        return entity;
    }

    public async Task<AssetTransferDto?> GetByIdAsync(Guid id)
    {
        var entity = await _transferRepo.GetWithDetailsAsync(id);
        return entity == null || entity.TenantId != GetTenantId() ? null : entity.ToDto();
    }

    public async Task<IEnumerable<AssetTransferSummaryDto>> GetAllAsync()
    {
        var tenantId = GetTenantId();
        var transfers = await _transferRepo.GetByTenantAsync(tenantId);
        return transfers.ToSummaryDtoList();
    }

    public async Task<PagedResult<AssetTransferSummaryDto>> GetPagedAsync(
        int page, int pageSize, 
        string? searchTerm = null, 
        HRAssetTransferStatus? status = null)
    {
        var tenantId = GetTenantId();
        var all = await _transferRepo.GetByTenantAsync(tenantId);
        var q = all.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
            q = q.Where(t => t.TransferNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (t.Asset != null && t.Asset.AssetName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

        if (status.HasValue)
            q = q.Where(t => t.Status == status.Value);

        var totalCount = q.Count();
        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<AssetTransferSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<AssetTransferSummaryDto>> GetByAssetIdAsync(Guid assetId)
    {
        await GetOwnedAssetAsync(assetId);
        var tenantId = GetTenantId();
        var transfers = await _transferRepo.GetByAssetIdAsync(assetId);
        return transfers.Where(t => t.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<AssetTransferSummaryDto>> GetPendingTransfersAsync()
    {
        var tenantId = GetTenantId();
        var transfers = await _transferRepo.GetPendingTransfersAsync(tenantId);
        return transfers.ToSummaryDtoList();
    }

    public async Task<AssetTransferDto> CreateAsync(CreateAssetTransferDto dto)
    {
        var tenantId = GetTenantId();
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        await GetOwnedAssetAsync(dto.AssetId);
        
        // Generate transfer number
        var transferNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var entity = dto.ToEntity(tenantId, userId, userId, transferNumber);
        await _transferRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _transferRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AssetTransferDto> UpdateAsync(Guid id, UpdateAssetTransferDto dto)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedTransferAsync(id);

        if (entity.Status != HRAssetTransferStatus.Pending)
            throw AssetsWorkflowException.InvalidState(
                "Only a pending transfer can be edited; this one has already been actioned.");

        entity.UpdateEntity(dto, userId);
        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _transferRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedTransferAsync(id);
        await _transferRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ApproveAsync(Guid id)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedTransferAsync(id);

        entity.Status = HRAssetTransferStatus.Approved;
        entity.ApprovedById = userId;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CompleteAsync(Guid id)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedTransferAsync(id);

        if (entity.Status != HRAssetTransferStatus.Approved && entity.Status != HRAssetTransferStatus.InTransit)
            throw AssetsWorkflowException.InvalidState(
                "Only an approved or in-transit transfer can be completed.");

        entity.Status = HRAssetTransferStatus.Completed;
        entity.CompletionDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _transferRepo.UpdateAsync(entity);

        // Update asset location/assignment
        var asset = await GetOwnedAssetAsync(entity.AssetId);
        if (entity.Type == HRAssetTransferType.EmployeeToEmployee)
        {
            asset.CurrentAssignedToId = entity.ToEmployeeId;
        }
        else if (entity.Type == HRAssetTransferType.LocationToLocation)
        {
            asset.LocationId = entity.ToLocationId;
        }
        else if (entity.Type == HRAssetTransferType.UnitToUnit)
        {
            asset.UnitId = entity.ToUnitId;
        }
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = userId.ToString();
        await _assetRepo.UpdateAsync(asset);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid id)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));
        
        var entity = await GetOwnedTransferAsync(id);

        entity.Status = HRAssetTransferStatus.Rejected;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }
}

#endregion
