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

// Slice 4 names `Employee` as a return type for the first time in this file. An ALIAS again, not
// `using ErpSystem.Core.Entities.HR` — that namespace carries a hundred-odd HR entities and pulling
// all of them in beside `Entities.HR.Assets` is how this file's `AssetType` and `AssetTransfer`
// become ambiguous. One type, named once.
using Employee = ErpSystem.Core.Entities.HR.Employee;
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

/// <summary>
/// Whether an asset can be put into somebody's hands, and whether a return makes sense — the two
/// integrity questions this module was never asking. Area 16, slice 4.
/// </summary>
/// <remarks>
/// <para>These live here rather than in one service because <b>two paths issue assets</b>: HR
/// assigning one directly, and a requisition being fulfilled. Slice 3 gave fulfilment its own
/// version of the availability rule and slice 0 proved the direct path had none at all — so the
/// same question was being asked in two places, in different words, with different answers. One
/// helper means the refusal a user meets is the same sentence whichever door they came through,
/// and a rule added later cannot land on only one of them.</para>
/// </remarks>
internal static class AssetIntegrity
{
    /// <summary>
    /// Refuses an asset that cannot be issued — <b>defect D-a</b>, and AST-2 as written.
    /// </summary>
    /// <remarks>
    /// <para>Slice 0 measured what its absence meant: an already-held asset was assigned to a
    /// second employee, the first assignment was left <c>Active</c> and orphaned, the asset
    /// silently changed hands, and two "current" assignments existed for one asset — with nothing
    /// to say which was true.</para>
    ///
    /// <para>⚠ <b>The order of these three questions is load-bearing</b>, and getting it wrong is
    /// defect D-p, which slice 3 had to fix on the fulfilment path. Assigning an asset sets
    /// <b>both</b> <c>IsCurrentlyAssigned</c> and <c>Status = Assigned</c>, so asking about status
    /// first answers every held asset with "not available; its status is Assigned" — true, useless,
    /// and it leaves the specific rule below it permanently unreachable. The specific question goes
    /// first; the status rule then answers for what it is actually about: disposed, damaged, lost,
    /// in maintenance.</para>
    /// </remarks>
    internal static void RequireAssignable(CompanyAsset asset)
    {
        if (asset.IsCurrentlyAssigned)
            throw AssetsWorkflowException.Conflict(
                $"Asset {asset.AssetNumber} is already assigned to someone.");

        // An asset the register says is not for issue — a shared printer, a fixture, a vehicle on
        // the fleet's books. The flag existed from the port and nothing had ever read it.
        if (!asset.IsAssignable)
            throw AssetsWorkflowException.InvalidState(
                $"Asset {asset.AssetNumber} is not marked as assignable, so it cannot be issued to an employee.");

        if (asset.Status != CompanyAssetStatus.Available)
            throw AssetsWorkflowException.InvalidState(
                $"Asset {asset.AssetNumber} is not available; its status is {asset.Status}.");
    }

    /// <summary>
    /// Refuses a return that contradicts itself — <b>defect D-c</b>.
    /// </summary>
    /// <remarks>
    /// <para>Slice 0 sent a return that was <c>returnedInGoodCondition: true</c> <b>and</b>
    /// <c>damageReported: true</c>, with a cracked screen described, a liable employee and a repair
    /// cost — and it was accepted and stored exactly as sent. Both facts then went on the record,
    /// so nothing downstream could decide whether to raise a surcharge (slice 7) or to hold the
    /// exit clearance (slice 10): the record answered "yes" to both questions.</para>
    ///
    /// <para>The rules are deliberately about <i>internal contradiction</i> rather than about
    /// policy. Whether an employee should be charged is slice 7's decision; whether a return can
    /// say the asset came back fine and came back broken is not a decision at all.</para>
    /// </remarks>
    internal static void RequireConsistentReturn(ReturnAssetDto dto)
    {
        if (dto.ReturnedInGoodCondition && dto.DamageReported)
            throw AssetsWorkflowException.Invalid(
                "A return cannot be both in good condition and damaged. Clear one of the two.");

        if (dto.DamageReported && string.IsNullOrWhiteSpace(dto.DamageDescription))
            throw AssetsWorkflowException.Invalid(
                "Describe the damage. A damage report with no description cannot be acted on.");

        // Liability, a repair cost and a replacement cost are all consequences OF damage. Recorded
        // without it they are the inert fields of D-d waiting to mislead somebody: slice 7 reads
        // them to raise a surcharge, and a surcharge for damage nobody reported is indefensible.
        if (!dto.DamageReported)
        {
            if (dto.EmployeeLiable)
                throw AssetsWorkflowException.Invalid(
                    "An employee cannot be held liable on a return that reports no damage.");

            if (dto.RepairCost is > 0 || dto.ReplacementCost is > 0)
                throw AssetsWorkflowException.Invalid(
                    "A repair or replacement cost needs the damage that caused it to be reported.");
        }

        // The condition and the verdict are two ways of saying the same thing, so they must agree.
        if (dto.ReturnedInGoodCondition
            && dto.ConditionAtReturn is HRAssetCondition.Poor or HRAssetCondition.NonFunctional)
            throw AssetsWorkflowException.Invalid(
                $"An asset returned in {dto.ConditionAtReturn} condition cannot also be recorded as "
                + "returned in good condition.");
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
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssetAssignmentService(
        IAssetAssignmentRepository assignmentRepo,
        ICompanyAssetRepository assetRepo,
        IEmployeeRepository employeeRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _assignmentRepo = assignmentRepo;
        _assetRepo = assetRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// An employee this record is about to name, or a 404 saying which id was not found.
    /// </summary>
    /// <remarks>
    /// Every one of these is an <b>Employee</b> foreign key, so an id that does not exist reaches
    /// SQL and comes back as error 547 — a 500 with an opaque body, which is exactly how D-k and
    /// D-l presented. Three columns on this surface take an employee id from the payload
    /// (<c>EmployeeId</c>, <c>ApprovedById</c>, <c>ReturnedToId</c>) and not one of them was
    /// checked.
    /// </remarks>
    private async Task<Employee> RequireEmployeeAsync(Guid employeeId, string role)
    {
        var employee = await _employeeRepo.GetByIdAsync(employeeId);
        if (employee is null || employee.TenantId != GetTenantId() || employee.IsDeleted)
            throw AssetsWorkflowException.NotFound(
                $"No employee was found with id {employeeId} to be {role}.");
        return employee;
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

        // D-a / AST-2. Nothing asked this before, and slice 0 measured what that meant.
        AssetIntegrity.RequireAssignable(asset);

        await RequireEmployeeAsync(dto.EmployeeId, "the holder");
        if (dto.ApprovedById is { } approvedById)
            await RequireEmployeeAsync(approvedById, "the approver");

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

        // The terms of an assignment — when it is due back, who is responsible for what — describe
        // a live custody. Once the asset is back they are history, and editing them rewrites what
        // the holder signed for after the fact.
        if (entity.Status != AssignmentStatus.Active)
            throw AssetsWorkflowException.InvalidState(
                $"Only an active assignment can be edited; this one is {entity.Status}.");

        entity.UpdateEntity(dto, userId);
        await _assignmentRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _assignmentRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    /// <summary>
    /// Withdraws an assignment recorded in error — and puts the asset back on the shelf.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The release is the point.</b> This is a soft delete, and it used to remove the
    /// assignment while leaving the asset carrying <c>IsCurrentlyAssigned = true</c>,
    /// <c>Status = Assigned</c> and a <c>CurrentAssignedToId</c> pointing at a holder whose
    /// assignment no longer existed. The asset was then <b>stuck</b>: the new D-a guard would
    /// refuse to assign it to anybody, and no return could free it because the record a return acts
    /// on had gone. Deleting the only thing that says an asset is held has to say it is not held.
    ///
    /// <para>The guard against stomping is <c>CurrentAssignedToId == entity.EmployeeId</c>: with
    /// D-a in place an asset has at most one active assignment, but a deletion of a *historical*
    /// assignment must not release an asset somebody else is holding today.</para>
    /// </remarks>
    public async Task DeleteAsync(Guid id)
    {
        var userId = Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

        var entity = await GetOwnedAssignmentAsync(id);
        await _assignmentRepo.DeleteAsync(entity);

        if (entity.Status == AssignmentStatus.Active)
        {
            var asset = await GetOwnedAssetAsync(entity.AssetId);
            if (asset.CurrentAssignedToId == entity.EmployeeId)
            {
                asset.IsCurrentlyAssigned = false;
                asset.CurrentAssignedToId = null;
                asset.Status = CompanyAssetStatus.Available;
                asset.UpdatedAt = DateTime.UtcNow;
                asset.UpdatedBy = userId.ToString();
                await _assetRepo.UpdateAsync(asset);
            }
        }

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

        // Returning an already-returned assignment used to be accepted, and it did real damage: it
        // overwrote the original return's condition, notes and damage record with the second
        // caller's, and put the asset back to Available even where the first return had marked it
        // Damaged. An asset can only come back once.
        if (entity.Status != AssignmentStatus.Active)
            throw AssetsWorkflowException.InvalidState(
                $"Only an active assignment can be returned; this one is {entity.Status}.");

        // D-c. The return must not contradict itself.
        AssetIntegrity.RequireConsistentReturn(dto);

        await RequireEmployeeAsync(dto.ReturnedToId, "the person receiving it back");

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
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;

    /// <summary>
    /// ⚠ <c>HrAssetRequisition</c>, not <c>AssetRequisition</c>. The prefix is not decoration: the
    /// workflow entity-type keys are a flat namespace shared with Finance's fixed assets and the
    /// Inventory <c>Asset</c>, and the build plan's §3.3 collision reaches them too.
    /// </summary>
    private const string EntityType = "HrAssetRequisition";

    public AssetRequisitionService(
        IAssetRequisitionRepository requisitionRepo,
        ICompanyAssetRepository assetRepo,
        IAssetAssignmentRepository assignmentRepo,
        IEmployeeRepository employeeRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters)
    {
        _requisitionRepo = requisitionRepo;
        _assetRepo = assetRepo;
        _assignmentRepo = assignmentRepo;
        _employeeRepo = employeeRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
    }

    /// <summary>The authenticated login, as the workflow engine wants it.</summary>
    /// <remarks>
    /// ⚠ <b>Two different actor ids run through this service and they are not interchangeable.</b>
    /// The engine resolves approvers by <c>ApplicationUser</c>, so every call into it takes this
    /// one; everything the requisition stores — <c>RequestedById</c>, <c>ApprovedById</c>,
    /// <c>FulfilledById</c>, <c>BeneficiaryEmployeeId</c> — is an <b>Employee</b> foreign key and
    /// takes <see cref="RequireCallerEmployeeId"/> instead. Confusing the two is precisely defect
    /// D-l on the transfer surface, where a user id was written into an Employee FK and every
    /// create answered 500. See <c>hr-attendance-actor-conventions</c>.
    /// </remarks>
    private Guid RequireCallerUserId() =>
        Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

    /// <summary>
    /// The caller's own employee record    /// <summary>
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

    /// <summary>
    /// The employee's own requisitions — raised by them, or raised for them. AST-6, AST-6b.
    /// </summary>
    /// <remarks>
    /// <para>Not the same question as <c>GetByRequestedByIdAsync</c>, and the difference is the
    /// point of the portal. Filtering on the requester alone means an employee cannot see the
    /// request their manager raised to get them a laptop — the record exists precisely for them and
    /// names them, and they are the last to know. Both actor columns, one list, one gate.</para>
    /// </remarks>
    public async Task<IEnumerable<AssetRequisitionSummaryDto>> GetForEmployeeAsync(Guid employeeId)
    {
        AssetActor.EnsureSelfOrHr(_currentUserService, employeeId,
            "list the asset requisitions an employee is a party to");

        var tenantId = GetTenantId();
        var requisitions = await _requisitionRepo.GetForEmployeeAsync(employeeId);
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
        var userId = RequireCallerUserId();

        // Generate requisition number
        var requisitionNumber = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var requestedById = RequireCallerEmployeeId("raise an asset requisition");
        var beneficiaryId = await ResolveBeneficiaryAsync(dto.BeneficiaryEmployeeId, requestedById, tenantId);

        var entity = dto.ToEntity(tenantId, requestedById, userId, requisitionNumber);
        entity.BeneficiaryEmployeeId = beneficiaryId;

        // Slice 3b. A new requisition is a DRAFT, always, and the payload has no say in it.
        // `CreateAssetRequisitionDto` used to carry a `Status` that was written straight onto the
        // record, so `{"status": 3}` created a requisition that was already Approved - no approver,
        // no approval date, no workflow, and HR's fulfilment gate satisfied. The status of an
        // approval record belongs to the thing that approves it.
        entity.Status = AssetRequisitionStatus.Draft;

        await _requisitionRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _requisitionRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    public async Task<AssetRequisitionDto> UpdateAsync(Guid id, UpdateAssetRequisitionDto dto)
    {
        var tenantId = GetTenantId();
        var userId = RequireCallerUserId();

        var entity = await GetOwnedRequisitionAsync(id);

        // The requester may correct their own request; HR may correct anyone's. The status rule
        // below is a separate question from the actor rule above and neither substitutes for the
        // other: an employee editing someone else's draft is refused here, not there.
        AssetActor.EnsureSelfOrHr(_currentUserService, entity.RequestedById, "edit an asset requisition");

        RequireEditableDraft(entity, "edited");

        entity.UpdateEntity(dto, userId);

        // AST-6b, and a field that used to be read by nothing. `UpdateAssetRequisitionDto` carried
        // `BeneficiaryEmployeeId` with the same documentation as the create DTO, and `UpdateEntity`
        // never assigned it - so correcting who a request was for silently did nothing and the form
        // showed the old name back. The same authorization runs as on create: naming somebody else
        // still takes HR or being their recorded line manager.
        entity.BeneficiaryEmployeeId =
            await ResolveBeneficiaryAsync(dto.BeneficiaryEmployeeId, entity.RequestedById, tenantId);

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

        RequireEditableDraft(entity, "withdrawn");

        await _requisitionRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// The one gate for editing and withdrawing, and the reason it answers in three ways.
    /// </summary>
    /// <remarks>
    /// <para>A <b>draft</b> is the requester's own: theirs to change or throw away. A
    /// <b>submitted</b> one is the engine's - somebody has it in their queue - so it must be
    /// recalled first, and the refusal says so rather than leaving the requester to guess. A
    /// <b>decided</b> one is a record of a decision: withdrawing it after the fact would erase the
    /// approval or the rejection along with the request.</para>
    ///
    /// <para>Before slice 3b a submitted requisition was freely editable, which under a workflow
    /// means the record an approver is reading can change under them between opening it and
    /// signing it.</para>
    /// </remarks>
    private static void RequireEditableDraft(AssetRequisition entity, string verb)
    {
        if (entity.Status == AssetRequisitionStatus.Draft) return;

        if (entity.Status is AssetRequisitionStatus.Submitted or AssetRequisitionStatus.UnderReview)
            throw AssetsWorkflowException.InvalidState(
                $"This requisition is out for approval and cannot be {verb}; recall it first.");

        throw AssetsWorkflowException.InvalidState(
            $"Only a draft requisition can be {verb}; this one has already been decided.");
    }

    /// <summary>
    /// Sends a draft requisition for approval - D3, the workflow engine.
    /// </summary>
    /// <remarks>
    /// The ported surface had no such act: a requisition was born "Submitted" and an HR officer
    /// wrote Approved onto it directly. Routing now belongs to the tenant's published definition,
    /// which decides whose queue this lands in; what does not belong there is who may raise the
    /// request, which is checked here, first, off the record itself.
    /// </remarks>
    public async Task<AssetRequisitionDto> SubmitAsync(Guid id)
    {
        var entity = await GetOwnedRequisitionAsync(id);

        AssetActor.EnsureSelfOrHr(_currentUserService, entity.RequestedById,
            "submit an asset requisition for approval");

        if (entity.Status != AssetRequisitionStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                entity.Status is AssetRequisitionStatus.Submitted or AssetRequisitionStatus.UnderReview
                    ? "This requisition is already out for approval."
                    : $"Only a draft requisition can be submitted; this one is {entity.Status}.");

        // The engine builds its routing context by reading the entity back out of the database, so
        // anything set-but-unsaved would be invisible to it. Nothing is pending here - the record
        // was saved by create or update - but the order is the rule, not the accident.
        var result = await RunWorkflowAsync(
            () => _workflow.SubmitAsync(EntityType, entity.Id),
            "start the requisition approval workflow");

        var actingUserId = RequireCallerUserId();
        _workflowAdapters.GetAdapter(EntityType).ApplySubmitOutcome(entity, result.Outcome, actingUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _requisitionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var submitted = await _requisitionRepo.GetWithDetailsAsync(id);
        return submitted!.ToDto();
    }

    public async Task ApproveAsync(Guid id, ApproveAssetRequisitionDto dto)
    {
        var entity = await GetOwnedRequisitionAsync(id);

        // D-k. The approver is the person approving. See RequireCallerEmployeeId for what was here.
        var approverId = RequireCallerEmployeeId("approve an asset requisition");
        var actingUserId = RequireCallerUserId();

        RequireDecidable(entity);
        RequireNotTheBeneficiary(entity, approverId, "approve");

        var result = await ProcessApprovalAsync(entity, "Approve", dto.ApprovalComments);

        _workflowAdapters.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, actingUserId);

        // Stamped only when the chain has actually finished. A definition with two approval steps
        // leaves the record Submitted after the first signature, and writing ApprovedById there
        // would name one signatory as *the* approver of something not yet approved.
        if (result.Outcome == WorkflowOutcome.Approved)
        {
            entity.ApprovedById = approverId;
            entity.ApprovalDate = DateTime.UtcNow;
        }
        if (!string.IsNullOrWhiteSpace(dto.ApprovalComments))
            entity.ApprovalComments = dto.ApprovalComments;

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _requisitionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid id, RejectAssetRequisitionDto dto)
    {
        var entity = await GetOwnedRequisitionAsync(id);

        var approverId = RequireCallerEmployeeId("reject an asset requisition");
        var actingUserId = RequireCallerUserId();

        RequireDecidable(entity);
        RequireNotTheBeneficiary(entity, approverId, "reject");

        var reason = string.IsNullOrWhiteSpace(dto.RejectionReason) ? "Rejected" : dto.RejectionReason.Trim();
        var result = await ProcessApprovalAsync(entity, "Reject", reason);

        _workflowAdapters.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, actingUserId, reason);

        entity.RejectionReason = reason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _requisitionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Withdraws a requisition the requester has sent but nobody has ruled on yet, returning it to
    /// Draft so they can change it and send it again.
    /// </summary>
    /// <remarks>
    /// The generic recall button in <c>WorkflowApprovalActions</c> calls the engine directly, so
    /// the "only the requester may recall" rule below binds API callers and not that button. That
    /// is the module's existing split - PIP and staff requisitions have it too - recorded rather
    /// than introduced here.
    /// </remarks>
    public async Task<AssetRequisitionDto> RecallAsync(Guid id, string? reason)
    {
        var entity = await GetOwnedRequisitionAsync(id);

        var callerEmployeeId = AssetActor.CallerEmployeeId(_currentUserService);
        if (callerEmployeeId != entity.RequestedById)
            throw new UnauthorizedAccessException(
                "Only the person who raised a requisition can recall it. HR can reject it instead.");

        if (entity.Status is not (AssetRequisitionStatus.Submitted or AssetRequisitionStatus.UnderReview))
            throw AssetsWorkflowException.InvalidState(
                $"Only a requisition still awaiting approval can be recalled; this one is {entity.Status}.");

        var actingUserId = RequireCallerUserId();
        var result = await RunWorkflowAsync(
            () => _workflow.RecallAsync(EntityType, entity.Id, actingUserId, reason),
            "recall the requisition");

        _workflowAdapters.GetAdapter(EntityType).ApplyRecallOutcome(entity, actingUserId, reason);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _requisitionRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var recalled = await _requisitionRepo.GetWithDetailsAsync(id);
        return recalled!.ToDto();
    }

    /// <summary>Only a requisition that is actually out for approval can be decided.</summary>
    private static void RequireDecidable(AssetRequisition entity)
    {
        if (entity.Status is AssetRequisitionStatus.Submitted or AssetRequisitionStatus.UnderReview) return;

        throw AssetsWorkflowException.InvalidState(
            entity.Status == AssetRequisitionStatus.Draft
                ? "This requisition has not been submitted for approval yet."
                : $"Only a requisition awaiting approval can be decided; this one is {entity.Status}.");
    }

    /// <summary>
    /// Nobody approves the issue of an asset to themselves.
    /// </summary>
    /// <remarks>
    /// <para>The subject is <c>BeneficiaryEmployeeId ?? RequestedById</c> - the same expression
    /// fulfilment uses to decide who receives the asset, so the rule and the consequence cannot
    /// drift apart.</para>
    ///
    /// <para>This deliberately does <b>not</b> bar an HR officer from approving a request they
    /// raised <i>on somebody else's behalf</i>. The conflict a self-approval rule guards against is
    /// gaining by your own signature, and an officer who raises a starter kit for a new joiner
    /// gains nothing - the area-9b lesson about <c>preventInitiatorApproval</c> being the wrong
    /// control when the record is about a third party. It is the beneficiary, not the initiator,
    /// who must not sign.</para>
    /// </remarks>
    private static void RequireNotTheBeneficiary(AssetRequisition entity, Guid actorEmployeeId, string verb)
    {
        var issueTo = entity.BeneficiaryEmployeeId ?? entity.RequestedById;
        if (actorEmployeeId != issueTo) return;

        throw new UnauthorizedAccessException(
            $"You cannot {verb} a requisition for an asset that would be issued to you.");
    }

    /// <summary>
    /// Runs one approval action through the engine, refusing first in the record's own terms.
    /// </summary>
    /// <remarks>
    /// <c>CanUserApproveAsync</c> answers "are you on this step", which is a fact about the
    /// definition. The rules above answer "may this be decided, and by you", which are facts about
    /// the requisition - so they run first and their refusals name the record. If the definition is
    /// missing or was authored wrongly, the record's own rules still hold.
    /// </remarks>
    private async Task<WorkflowIntegrationResult> ProcessApprovalAsync(
        AssetRequisition entity, string action, string? comments)
    {
        var actingUserId = RequireCallerUserId();

        if (!await _workflow.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current step of this requisition's approval workflow.");

        return await RunWorkflowAsync(
            () => _workflow.ProcessApprovalAsync(EntityType, entity.Id, actingUserId, action, comments),
            $"process the {action.ToLowerInvariant()}");
    }

    /// <summary>
    /// Calls the engine and turns anything it refuses into a refusal this area's callers can read.
    /// </summary>
    /// <remarks>
    /// The engine reports a missing entity type or an unpublished definition as an
    /// <see cref="InvalidOperationException"/>, whose message
    /// <c>GlobalExceptionHandlingMiddleware</c> throws away - the D-m complaint again, arriving
    /// from outside the area. "Workflow entity type 'HrAssetRequisition' is not configured" is
    /// exactly what an administrator needs to see, so it is carried through as a 409 instead of
    /// becoming "The operation is not valid for the current state of the object."
    /// </remarks>
    internal static async Task<WorkflowIntegrationResult> RunWorkflowAsync(
        Func<Task<WorkflowIntegrationResult>> call, string what)
    {
        WorkflowIntegrationResult result;
        try
        {
            result = await call();
        }
        catch (InvalidOperationException ex)
        {
            throw AssetsWorkflowException.InvalidState($"Could not {what}: {ex.Message}");
        }

        if (!result.ExecutionResult.Success)
            throw AssetsWorkflowException.InvalidState(
                result.ExecutionResult.Message ?? $"Could not {what}.");

        return result;
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

            // Slice 4. The same guard the direct assignment path runs, from one place — including
            // the `IsAssignable` question, which neither path was asking. The comment about the
            // order of these checks (defect D-p) now lives with the rule itself.
            AssetIntegrity.RequireAssignable(asset);

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
    private readonly IAssetAssignmentRepository _assignmentRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly ILocationRepository _locationRepo;
    private readonly IOrganizationUnitRepository _unitRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;

    /// <summary>
    /// The workflow entity-type key. <b>Prefixed, and it has to be</b>: "AssetTransfer" already
    /// means Finance's fixed-asset transfer to <c>WorkflowEntityDisplayService</c>. Registering
    /// this surface under that key would have pointed an HR approver's notification at a
    /// fixed-asset screen, and nothing would have reported an error. Build plan §3.3.
    /// </summary>
    private const string EntityType = "HrAssetTransfer";

    public AssetTransferService(
        IAssetTransferRepository transferRepo,
        ICompanyAssetRepository assetRepo,
        IAssetAssignmentRepository assignmentRepo,
        IEmployeeRepository employeeRepo,
        ILocationRepository locationRepo,
        IOrganizationUnitRepository unitRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IWorkflowIntegrationService workflow,
        IWorkflowStatusAdapterRegistry workflowAdapters)
    {
        _transferRepo = transferRepo;
        _assetRepo = assetRepo;
        _assignmentRepo = assignmentRepo;
        _employeeRepo = employeeRepo;
        _locationRepo = locationRepo;
        _unitRepo = unitRepo;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _workflow = workflow;
        _workflowAdapters = workflowAdapters;
    }

    /// <summary>The authenticated login, for the workflow engine.</summary>
    private Guid RequireCallerUserId() =>
        Guid.Parse(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User ID not found"));

    /// <summary>
    /// The caller's own employee record - <b>defect D-l lived here</b>.
    /// </summary>
    /// <remarks>
    /// <para><c>CreateAsync</c> passed the caller's <b>user</b> id into <c>InitiatedById</c>, and
    /// <c>ApproveAsync</c> passed it into <c>ApprovedById</c>. Both are <b>Employee</b> foreign
    /// keys. An <c>ApplicationUser</c> id is not an <c>Employee</c> id, so SQL rejected the INSERT
    /// with error 547 and <b>every transfer create answered 500, for every actor, since the
    /// port</b> - which meant all eleven transfer routes had never touched a real row: not one
    /// list, not one approval, not one completed move.</para>
    ///
    /// <para>The same shape as D-k on the requisition surface, and the same remedy: an actor whose
    /// login is not linked to an employee is refused in words rather than stamped as nobody.</para>
    /// </remarks>
    private Guid RequireCallerEmployeeId(string action)
    {
        return AssetActor.CallerEmployeeId(_currentUserService)
            ?? throw new UnauthorizedAccessException(
                $"Your user account is not linked to an employee record, so it cannot {action}.");
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
        var userId = RequireCallerUserId();

        // D-l. An Employee id, because InitiatedById is an Employee foreign key.
        var initiatedById = RequireCallerEmployeeId("raise an asset transfer");

        var asset = await GetOwnedAssetAsync(dto.AssetId);

        if (asset.Status is CompanyAssetStatus.Disposed or CompanyAssetStatus.LostStolen)
            throw AssetsWorkflowException.InvalidState(
                $"Asset {asset.AssetNumber} cannot be transferred; its status is {asset.Status}.");

        await RequireDestinationAsync(dto, tenantId);

        // Generate transfer number
        var transferNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var entity = dto.ToEntity(tenantId, initiatedById, userId, transferNumber);

        // Where the asset is coming FROM is a fact the register already holds, so it is taken from
        // the asset unless the caller states it. A transfer whose "from" side is blank cannot be
        // read back as a movement afterwards - it says where something went and not where it was.
        entity.FromEmployeeId ??= asset.CurrentAssignedToId;
        entity.FromLocationId ??= asset.LocationId;
        entity.FromUnitId ??= asset.UnitId;

        await _transferRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var created = await _transferRepo.GetWithDetailsAsync(entity.Id);
        return created!.ToDto();
    }

    /// <summary>
    /// Every transfer type needs a destination of its own kind, and it must exist.
    /// </summary>
    /// <remarks>
    /// <para>Nothing checked this before, because nothing could create a transfer at all (D-l). An
    /// employee-to-employee move with no <c>ToEmployeeId</c> would have been accepted, approved and
    /// completed, and <c>CompleteAsync</c> would then have set the asset's holder to null - the
    /// register would show it held by nobody with no record of who had it.</para>
    ///
    /// <para><b>⚠ <c>DepartmentToDepartment</c> is refused, and that is a finding rather than a
    /// decision.</b> The enum offers it, but <c>AssetTransfer</c> has no department column on
    /// either side and <c>CompanyAsset</c> has no department at all - so a department transfer had
    /// nowhere to record where it came from, nowhere to record where it went, and
    /// <c>CompleteAsync</c>'s type switch has no branch for it, meaning completing one moved
    /// nothing and reported success. Refusing it in words is honest; the alternative is a feature
    /// that silently does nothing. Units are the structure this module actually holds, and
    /// <c>UnitToUnit</c> does work.</para>
    /// </remarks>
    private async Task RequireDestinationAsync(CreateAssetTransferDto dto, Guid tenantId)
    {
        switch (dto.Type)
        {
            case HRAssetTransferType.EmployeeToEmployee:
                if (dto.ToEmployeeId is not { } toEmployeeId)
                    throw AssetsWorkflowException.Invalid(
                        "An employee-to-employee transfer needs the employee it is going to.");
                var employee = await _employeeRepo.GetByIdAsync(toEmployeeId);
                if (employee is null || employee.TenantId != tenantId || employee.IsDeleted)
                    throw AssetsWorkflowException.NotFound($"No employee was found with id {toEmployeeId}.");
                break;

            case HRAssetTransferType.LocationToLocation:
                if (dto.ToLocationId is not { } toLocationId)
                    throw AssetsWorkflowException.Invalid(
                        "A location-to-location transfer needs the location it is going to.");
                var location = await _locationRepo.GetByIdAsync(toLocationId);
                if (location is null || location.TenantId != tenantId || location.IsDeleted)
                    throw AssetsWorkflowException.NotFound($"No location was found with id {toLocationId}.");
                break;

            case HRAssetTransferType.UnitToUnit:
                if (dto.ToUnitId is not { } toUnitId)
                    throw AssetsWorkflowException.Invalid(
                        "A unit-to-unit transfer needs the unit it is going to.");
                var unit = await _unitRepo.GetByIdAsync(toUnitId);
                if (unit is null || unit.TenantId != tenantId || unit.IsDeleted)
                    throw AssetsWorkflowException.NotFound($"No organization unit was found with id {toUnitId}.");
                break;

            default:
                throw AssetsWorkflowException.Invalid(
                    "Department-to-department transfers are not supported: an asset records a unit "
                    + "and a location, not a department. Use a unit-to-unit transfer instead.");
        }
    }

    public async Task<AssetTransferDto> UpdateAsync(Guid id, UpdateAssetTransferDto dto)
    {
        var userId = RequireCallerUserId();

        var entity = await GetOwnedTransferAsync(id);

        RequireEditableDraft(entity, "edited");

        entity.UpdateEntity(dto, userId);
        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var updated = await _transferRepo.GetWithDetailsAsync(id);
        return updated!.ToDto();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedTransferAsync(id);

        // There was no guard here at all: a completed transfer - the record of an asset having
        // moved - could be deleted by anyone who could reach the route.
        RequireEditableDraft(entity, "withdrawn");

        await _transferRepo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>Editing and withdrawing, and the three ways this answers. See the requisition twin.</summary>
    private static void RequireEditableDraft(AssetTransfer entity, string verb)
    {
        if (entity.Status == HRAssetTransferStatus.Draft) return;

        if (entity.Status == HRAssetTransferStatus.Pending)
            throw AssetsWorkflowException.InvalidState(
                $"This transfer is out for approval and cannot be {verb}; recall it first.");

        throw AssetsWorkflowException.InvalidState(
            $"Only a draft transfer can be {verb}; this one is {entity.Status}.");
    }

    /// <summary>Sends a draft transfer for approval - D3, the workflow engine.</summary>
    public async Task<AssetTransferDto> SubmitAsync(Guid id)
    {
        var entity = await GetOwnedTransferAsync(id);

        if (entity.Status != HRAssetTransferStatus.Draft)
            throw AssetsWorkflowException.InvalidState(
                entity.Status == HRAssetTransferStatus.Pending
                    ? "This transfer is already out for approval."
                    : $"Only a draft transfer can be submitted; this one is {entity.Status}.");

        var result = await AssetRequisitionService.RunWorkflowAsync(
            () => _workflow.SubmitAsync(EntityType, entity.Id),
            "start the transfer approval workflow");

        var actingUserId = RequireCallerUserId();
        _workflowAdapters.GetAdapter(EntityType).ApplySubmitOutcome(entity, result.Outcome, actingUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var submitted = await _transferRepo.GetWithDetailsAsync(id);
        return submitted!.ToDto();
    }

    public async Task ApproveAsync(Guid id)
    {
        var entity = await GetOwnedTransferAsync(id);

        // D-l, second site: ApprovedById is an Employee foreign key too.
        var approverId = RequireCallerEmployeeId("approve an asset transfer");
        var actingUserId = RequireCallerUserId();

        RequireDecidable(entity);
        RequireNotTheRecipient(entity, approverId, "approve");

        var result = await ProcessApprovalAsync(entity, "Approve", null);

        _workflowAdapters.GetAdapter(EntityType).ApplyApprovalOutcome(entity, result.Outcome, actingUserId);

        if (result.Outcome == WorkflowOutcome.Approved)
        {
            entity.ApprovedById = approverId;
            entity.ApprovalDate = DateTime.UtcNow;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RejectAsync(Guid id)
    {
        var entity = await GetOwnedTransferAsync(id);

        var approverId = RequireCallerEmployeeId("reject an asset transfer");
        var actingUserId = RequireCallerUserId();

        RequireDecidable(entity);
        RequireNotTheRecipient(entity, approverId, "reject");

        var result = await ProcessApprovalAsync(entity, "Reject", "Rejected");

        _workflowAdapters.GetAdapter(EntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, actingUserId, "Rejected");
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>Withdraws a transfer that is out for approval, returning it to Draft.</summary>
    public async Task<AssetTransferDto> RecallAsync(Guid id, string? reason)
    {
        var entity = await GetOwnedTransferAsync(id);

        var callerEmployeeId = AssetActor.CallerEmployeeId(_currentUserService);
        if (callerEmployeeId != entity.InitiatedById)
            throw new UnauthorizedAccessException(
                "Only the person who raised a transfer can recall it. An approver can reject it instead.");

        if (entity.Status != HRAssetTransferStatus.Pending)
            throw AssetsWorkflowException.InvalidState(
                $"Only a transfer still awaiting approval can be recalled; this one is {entity.Status}.");

        var actingUserId = RequireCallerUserId();
        var result = await AssetRequisitionService.RunWorkflowAsync(
            () => _workflow.RecallAsync(EntityType, entity.Id, actingUserId, reason),
            "recall the transfer");

        _workflowAdapters.GetAdapter(EntityType).ApplyRecallOutcome(entity, actingUserId, reason);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId.ToString();

        await _transferRepo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var recalled = await _transferRepo.GetWithDetailsAsync(id);
        return recalled!.ToDto();
    }

    private static void RequireDecidable(AssetTransfer entity)
    {
        if (entity.Status == HRAssetTransferStatus.Pending) return;

        throw AssetsWorkflowException.InvalidState(
            entity.Status == HRAssetTransferStatus.Draft
                ? "This transfer has not been submitted for approval yet."
                : $"Only a transfer awaiting approval can be decided; this one is {entity.Status}.");
    }

    /// <summary>
    /// Nobody approves an asset being moved into their own hands.
    /// </summary>
    /// <remarks>
    /// The requisition twin of this rule bars the beneficiary; here it is the receiving employee.
    /// Both say the same thing: the signature that matters is the one from somebody who does not
    /// gain by it. The initiator is not barred - HR raises these about other people.
    /// </remarks>
    private static void RequireNotTheRecipient(AssetTransfer entity, Guid actorEmployeeId, string verb)
    {
        if (entity.ToEmployeeId != actorEmployeeId) return;

        throw new UnauthorizedAccessException(
            $"You cannot {verb} a transfer of an asset to yourself.");
    }

    private async Task<WorkflowIntegrationResult> ProcessApprovalAsync(
        AssetTransfer entity, string action, string? comments)
    {
        var actingUserId = RequireCallerUserId();

        if (!await _workflow.CanUserApproveAsync(EntityType, entity.Id, actingUserId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current step of this transfer's approval workflow.");

        return await AssetRequisitionService.RunWorkflowAsync(
            () => _workflow.ProcessApprovalAsync(EntityType, entity.Id, actingUserId, action, comments),
            $"process the {action.ToLowerInvariant()}");
    }

    public async Task CompleteAsync(Guid id)
    {
        var userId = RequireCallerUserId();

        var entity = await GetOwnedTransferAsync(id);

        if (entity.Status != HRAssetTransferStatus.Approved && entity.Status != HRAssetTransferStatus.InTransit)
            throw AssetsWorkflowException.InvalidState(
                "Only an approved or in-transit transfer can be completed.");

        var asset = await GetOwnedAssetAsync(entity.AssetId);

        // ⚠ The asset may have moved since this transfer was raised — returned, reassigned, or
        // carried by another transfer that completed first. Completing on top of that would
        // silently take it out of the current holder's hands with no record on their assignment.
        // Approval was given for a move FROM somebody; if that is no longer true, the transfer has
        // to be raised again against the facts.
        if (entity.Type == HRAssetTransferType.EmployeeToEmployee
            && entity.FromEmployeeId is { } expectedHolder
            && asset.CurrentAssignedToId != expectedHolder)
        {
            throw AssetsWorkflowException.Conflict(
                $"Asset {asset.AssetNumber} is no longer held by the employee this transfer moves it "
                + "from, so the transfer cannot be completed. Raise a new one against who holds it now.");
        }

        entity.Status = HRAssetTransferStatus.Completed;
        entity.CompletionDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _transferRepo.UpdateAsync(entity);

        // Completion is the module carrying out what was approved, so it stays a direct action and
        // is deliberately NOT a workflow step - the same boundary the two outcome proposals draw.
        if (entity.Type == HRAssetTransferType.EmployeeToEmployee)
        {
            await MoveCustodyAsync(entity, asset, userId);
            asset.CurrentAssignedToId = entity.ToEmployeeId;
            asset.IsCurrentlyAssigned = entity.ToEmployeeId != null;
            if (entity.ToEmployeeId != null) asset.Status = CompanyAssetStatus.Assigned;
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

    /// <summary>
    /// Closes the outgoing assignment and opens the incoming one, so the custody register and the
    /// asset tell the same story — the gap slice 3b found and left for slice 4.
    /// </summary>
    /// <remarks>
    /// <para>Completing an employee-to-employee transfer used to move <c>CurrentAssignedToId</c>
    /// and nothing else. The <b>old assignment stayed Active</b>, so the outgoing employee's "what
    /// I hold" list still showed the asset, the exit-clearance hook (FR-HR-183, slice 10) would
    /// have held their exit over an item they no longer had, and the recipient had no assignment at
    /// all — no terms, no acknowledgement to give, nothing to return. This is D-a's shape from the
    /// other end: the asset said one thing and the register another.</para>
    ///
    /// <para><b>The outgoing assignment is closed as <c>Transferred</c>, not <c>Returned</c>.</b>
    /// Nobody took the asset back and the next custody starts the same day, so <c>Returned</c>
    /// would have been a lie in the one place somebody looks to find out what happened to it.
    /// <c>AssignmentStatus</c> is declared in the HRApi-owned <c>HREnums.cs</c>, which is why this
    /// was first written as a compromise — but that file already carries two RHEMA-added members
    /// with the same justification, and the header of <c>HREnums.Rhema.cs</c> now lists all three so
    /// a sync has a checklist. Behaviourally nothing moves: every query in the module asks whether
    /// an assignment is <c>Active</c>.</para>
    ///
    /// <para><c>ReturnedToId</c> carries the <b>recipient</b>. Under a <c>Transferred</c> status
    /// "returned to" can only mean "handed to", so the row is self-describing without opening the
    /// transfer — and the reason it named the processing officer instead, on the first cut, was to
    /// keep that field from lying while the status still said Returned.</para>
    /// </remarks>
    private async Task MoveCustodyAsync(AssetTransfer transfer, CompanyAsset asset, Guid userId)
    {
        var outgoing = await _assignmentRepo.GetActiveAssignmentForAssetAsync(asset.Id);
        if (outgoing is not null && outgoing.TenantId == transfer.TenantId)
        {
            outgoing.Status = AssignmentStatus.Transferred;
            outgoing.ReturnDate = DateTime.UtcNow;
            outgoing.ConditionAtReturn = asset.Condition;
            outgoing.ReturnedInGoodCondition = true;
            outgoing.ReturnedToId = transfer.ToEmployeeId ?? transfer.InitiatedById;
            outgoing.ReturnNotes = Truncate(
                $"Closed by transfer {transfer.TransferNumber}: custody passed to another employee."
                + (string.IsNullOrWhiteSpace(transfer.TransferReason) ? string.Empty : $" {transfer.TransferReason}"),
                1000);
            outgoing.UpdatedAt = DateTime.UtcNow;
            outgoing.UpdatedBy = userId.ToString();
            await _assignmentRepo.UpdateAsync(outgoing);
        }

        if (transfer.ToEmployeeId is not { } recipientId) return;

        var incoming = new AssetAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = transfer.TenantId,
            AssignmentNumber = $"ASN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            AssetId = asset.Id,
            EmployeeId = recipientId,

            // Where this custody came from, the same way a fulfilment records its requisition (D-e).
            // Without it the recipient's assignment appears from nowhere and the transfer that
            // authorised it cannot be found from the record it produced.
            TransferId = transfer.Id,
            AssignmentDate = DateOnly.FromDateTime(DateTime.UtcNow),

            // The terms are carried over from the assignment that just closed rather than invented:
            // the asset is the same asset on the same footing, and defaulting a fresh set here
            // would quietly change what somebody is responsible for.
            Type = outgoing?.Type ?? AssignmentType.Permanent,
            Purpose = outgoing?.Purpose ?? AssignmentPurpose.RegularWork,
            ExpectedReturnDate = outgoing?.ExpectedReturnDate,
            IsPrimaryUser = outgoing?.IsPrimaryUser ?? true,
            ResponsibleForLoss = outgoing?.ResponsibleForLoss ?? true,
            ResponsibleForDamage = outgoing?.ResponsibleForDamage ?? true,
            TermsAndConditions = outgoing?.TermsAndConditions,
            ConditionAtAssignment = asset.Condition,
            AssignmentNotes = Truncate($"Received by transfer {transfer.TransferNumber}.", 1000),

            ApprovedById = transfer.ApprovedById,
            ApprovalDate = transfer.ApprovalDate,
            Status = AssignmentStatus.Active,

            // ⚠ NOT carried over. Acknowledgement is the holder's own signature that they received
            // this asset and accept these terms (D-b) — the recipient has not given it, and copying
            // the previous holder's would forge it.
            EmployeeAcknowledged = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };

        await _assignmentRepo.AddAsync(incoming);
    }

    private static string Truncate(string value, int max)
        => value.Length > max ? value[..max] : value;
}

#endregion
