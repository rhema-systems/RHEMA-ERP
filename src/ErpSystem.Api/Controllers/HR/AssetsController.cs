using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The staff / company asset register: what the organisation owns, who holds it, and everything
/// that follows from a person holding it.
/// </summary>
/// <remarks>
/// <para><b>Authorization, area 16 slice 1.</b> Until this slice the class carried one bare
/// <c>[Authorize]</c> over all 82 routes and nothing else. Slice 0 measured what that meant rather
/// than assuming it: a plain <c>Employee</c> created an asset type, created a company asset,
/// <b>disposed of a company asset</b>, reached the requisition approval endpoint and listed every
/// employee's assignments. None of it was refused.</para>
///
/// <para><b>The surface is HR-only except where the change document says otherwise.</b> Two
/// requirements put ordinary staff on this controller — AST-6 (an employee requests an asset from
/// the portal) and AST-8 (an employee acknowledges receipt) — and they bring the reads those
/// screens need with them. Every route is now in exactly one of two groups:</para>
///
/// <list type="bullet">
///   <item><description><b><c>[Authorize(Roles = HrRoles)]</c></b> — the administrative surface.
///   The role alone decides, and the answer does not depend on which record is being touched.</description></item>
///   <item><description><b><c>[Authorize]</c> plus a check in the service</b> — the self-service
///   routes, where the answer <i>does</i> depend on whose record it is. An attribute cannot say
///   "this employee, on this record", so those are gated by <c>AssetActor</c> in
///   <c>AssetsServices.cs</c>. A bare <c>[Authorize]</c> here is therefore a deliberate marker,
///   not an oversight — <b>every one of them has a matching service-side rule</b>.</description></item>
/// </list>
///
/// <para>⚠ Stacked <c>[Authorize]</c> attributes are <b>ANDed</b>, so the class cannot carry the
/// role list and be widened per action. It stays at bare <c>[Authorize]</c> and each action states
/// its own gate. That is more attributes, but it means no route inherits a gate by accident, and a
/// new action with no attribute is visibly ungated rather than quietly protected.</para>
///
/// <para>Acknowledgement is the one action HR cannot perform for someone else — see
/// <c>AssetActor.EnsureIsSubject</c> and AST-5, the printed responsibility form, for the case where
/// an employee cannot reach the portal at all.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AssetsController : ControllerBase
{
    /// <summary>Who may act on anyone's assets: HR, and the two admin roles above it.</summary>
    /// <remarks>
    /// Both HR spellings are absent here on purpose — the legacy "HR User" name is migrated to "HR"
    /// on startup, and <c>AssetActor.IsHr</c> covers the un-migrated tenant on the service side.
    /// </remarks>
    internal const string HrRoles =
        Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin + "," + Constants.Roles.Hr;

    private readonly IAssetTypeService _assetTypeService;
    private readonly IAssetTypeAttributeService _assetTypeAttributeService;
    private readonly ICompanyAssetService _companyAssetService;
    private readonly IAssetAttributeValueService _attributeValueService;
    private readonly IAssetImageService _assetImageService;
    private readonly IAssetAssignmentService _assignmentService;
    private readonly IAssetMaintenanceService _maintenanceService;
    private readonly IAssetAttachmentService _attachmentService;
    private readonly IAssetRequisitionService _requisitionService;
    private readonly IAssetTransferService _transferService;
    private readonly IAssetTermsLetterService _termsLetterService;
    private readonly IAssetSurchargeService _surchargeService;

    // ── the controlled upload gate — slice 12b ────────────────────────────────────────────
    // Scanning + central-DMS registration on the way in, an authorized stream on the way out.
    // The files live outside the web root, so the download endpoints are the ONLY way to them.
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AssetsController> _logger;

    public AssetsController(
        IAssetTypeService assetTypeService,
        IAssetTypeAttributeService assetTypeAttributeService,
        ICompanyAssetService companyAssetService,
        IAssetAttributeValueService attributeValueService,
        IAssetImageService assetImageService,
        IAssetAssignmentService assignmentService,
        IAssetMaintenanceService maintenanceService,
        IAssetAttachmentService attachmentService,
        IAssetRequisitionService requisitionService,
        IAssetTransferService transferService,
        IAssetTermsLetterService termsLetterService,
        IAssetSurchargeService surchargeService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        ApplicationDbContext db,
        ILogger<AssetsController> logger)
    {
        _assetTypeService = assetTypeService;
        _assetTypeAttributeService = assetTypeAttributeService;
        _companyAssetService = companyAssetService;
        _attributeValueService = attributeValueService;
        _assetImageService = assetImageService;
        _assignmentService = assignmentService;
        _maintenanceService = maintenanceService;
        _attachmentService = attachmentService;
        _requisitionService = requisitionService;
        _transferService = transferService;
        _termsLetterService = termsLetterService;
        _surchargeService = surchargeService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _db = db;
        _logger = logger;
    }

    #region Asset Types

    /// <summary>List asset types for a tenant.</summary>
    [HttpGet("types")]
    // Self-service: the picker every asset-request form needs; the type catalogue is not sensitive and the
    // writes beside it are HR-only.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<IEnumerable<AssetTypeSummaryDto>>> GetAssetTypes()
    {
        var result = await _assetTypeService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Get asset types paged.</summary>
    [HttpGet("types/paged")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<AssetTypeSummaryDto>>> GetAssetTypesPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null)
    {
        var result = await _assetTypeService.GetPagedAsync(pageNumber, pageSize, searchTerm);
        return Ok(result);
    }

    /// <summary>Get asset type by id.</summary>
    [HttpGet("types/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTypeDto>> GetAssetType(Guid id)
    {
        var result = await _assetTypeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Get asset type with attributes.</summary>
    [HttpGet("types/{id:guid}/with-attributes")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTypeDetailDto>> GetAssetTypeWithAttributes(Guid id)
    {
        var result = await _assetTypeService.GetWithAttributesAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create asset type.</summary>
    [HttpPost("types")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTypeDto>> CreateAssetType([FromBody] CreateAssetTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _assetTypeService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAssetType), new { id = created.Id }, created);
    }

    /// <summary>Update asset type.</summary>
    [HttpPut("types/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTypeDto>> UpdateAssetType(
        Guid id,
        [FromBody] UpdateAssetTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _assetTypeService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete asset type.</summary>
    [HttpDelete("types/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteAssetType(Guid id)
    {
        await _assetTypeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Asset Type Attributes

    /// <summary>List attributes for an asset type.</summary>
    [HttpGet("types/{assetTypeId:guid}/attributes")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetTypeAttributeDto>>> GetAssetTypeAttributes(Guid assetTypeId)
    {
        var result = await _assetTypeAttributeService.GetByAssetTypeIdAsync(assetTypeId);
        return Ok(result);
    }

    /// <summary>Get attribute by id.</summary>
    [HttpGet("attributes/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTypeAttributeDto>> GetAssetTypeAttribute(Guid id)
    {
        var result = await _assetTypeAttributeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create asset type attribute.</summary>
    [HttpPost("types/{assetTypeId:guid}/attributes")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTypeAttributeDto>> CreateAssetTypeAttribute(
        Guid assetTypeId,
        [FromBody] CreateAssetTypeAttributeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.AssetTypeId = assetTypeId;
        var created = await _assetTypeAttributeService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAssetTypeAttribute), new { id = created.Id }, created);
    }

    /// <summary>Update asset type attribute.</summary>
    [HttpPut("attributes/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTypeAttributeDto>> UpdateAssetTypeAttribute(
        Guid id,
        [FromBody] UpdateAssetTypeAttributeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _assetTypeAttributeService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete asset type attribute.</summary>
    [HttpDelete("attributes/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteAssetTypeAttribute(Guid id)
    {
        await _assetTypeAttributeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Company Assets

    /// <summary>List all assets for a tenant.</summary>
    [HttpGet]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssets()
    {
        var result = await _companyAssetService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged assets.</summary>
    [HttpGet("paged")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<CompanyAssetSummaryDto>>> GetAssetsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] CompanyAssetStatus? status = null,
        [FromQuery] Guid? assetTypeId = null)
    {
        var result = await _companyAssetService.GetPagedAsync(pageNumber, pageSize, searchTerm, status, assetTypeId);
        return Ok(result);
    }

    /// <summary>Get asset by id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CompanyAssetDto>> GetAsset(Guid id)
    {
        var result = await _companyAssetService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Get asset with details.</summary>
    [HttpGet("{id:guid}/details")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CompanyAssetDetailDto>> GetAssetDetails(Guid id)
    {
        var result = await _companyAssetService.GetWithDetailsAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Assets by status.</summary>
    [HttpGet("status/{status}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssetsByStatus(CompanyAssetStatus status)
    {
        var result = await _companyAssetService.GetByStatusAsync(status);
        return Ok(result);
    }

    /// <summary>Assets by type.</summary>
    [HttpGet("types/{assetTypeId:guid}/assets")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssetsByType(Guid assetTypeId)
    {
        var result = await _companyAssetService.GetByAssetTypeAsync(assetTypeId);
        return Ok(result);
    }

    /// <summary>Available assets for assignment.</summary>
    [HttpGet("available")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAvailableAssets()
    {
        var result = await _companyAssetService.GetAvailableForAssignmentAsync();
        return Ok(result);
    }

    /// <summary>Assets by employee.</summary>
    [HttpGet("employee/{employeeId:guid}")]
    // Self-service: self-or-HR in CompanyAssetService.GetByEmployeeAsync.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssetsByEmployee(Guid employeeId)
    {
        var result = await _companyAssetService.GetByEmployeeAsync(employeeId);
        return Ok(result);
    }

    /// <summary>AST-1 — assets whose maintenance falls due within the horizon.</summary>
    /// <remarks>
    /// <para>Answers <see cref="AssetMaintenanceDueDto"/>, not the register summary. Until slice 9
    /// this route returned <c>CompanyAssetSummaryDto</c>, which carries no maintenance date of any
    /// kind — so the only monitoring read the module had could say that assets needed attention and
    /// never when, how late, or in what order (D-ff).</para>
    ///
    /// <para><c>asOf</c> reads only and claims nothing. It exists because the schedule is written by
    /// the server from the interval, so without it a test can assert only what happens to be true
    /// today — the same seam the reminder preview offers, for the same reason.</para>
    /// </remarks>
    [HttpGet("due-maintenance")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceDueDto>>> GetDueForMaintenance(
        [FromQuery] int daysAhead = 30,
        [FromQuery] DateOnly? asOf = null)
    {
        var result = await _companyAssetService.GetDueForMaintenanceAsync(daysAhead, asOf);
        return Ok(result);
    }

    /// <summary>AST-1 — assets whose maintenance date has already passed, most overdue first.</summary>
    /// <remarks>
    /// Its own read rather than a filter on the one above, because it answers a different question.
    /// "What should I book this month" and "what have we let slip" are different jobs for different
    /// people, and eleven overdue rows inside a list of two hundred due ones are lost rows.
    /// </remarks>
    [HttpGet("overdue-maintenance")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceDueDto>>> GetOverdueMaintenance(
        [FromQuery] DateOnly? asOf = null)
    {
        var result = await _companyAssetService.GetOverdueMaintenanceAsync(asOf);
        return Ok(result);
    }

    /// <summary>AST-1 — assets that require regular maintenance and have never been scheduled.</summary>
    /// <remarks>
    /// The rows every other maintenance read filters away: each of them requires
    /// <c>NextMaintenanceDate != null</c>, so an asset flagged as needing regular servicing that
    /// nobody has ever given a date appeared on no list anywhere. That is the one asset shape a
    /// monitoring feature must not lose, since nothing about it will ever become due.
    /// </remarks>
    [HttpGet("unscheduled-maintenance")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceDueDto>>> GetUnscheduledMaintenance()
    {
        var result = await _companyAssetService.GetUnscheduledMaintenanceAsync();
        return Ok(result);
    }

    // ── insurance — area 16 slice 11 ──────────────────────────────────────────
    //
    // The same three jobs the maintenance reads answer, over the field slice 2b added and nothing
    // has ever read back: a renewal plan, an exception list, and the data-quality list of assets
    // ticked as insured that nobody ever dated.

    /// <summary>Insured assets whose cover lapses inside the window — the renewal plan.</summary>
    /// <remarks>
    /// The default horizon is 60 days, not maintenance's 30: a service can be booked in a
    /// fortnight, and a renewal is a quotation, an approval and a payment. ⚠ An assumed number
    /// rather than TDC's — flagged with the area's other assumed windows.
    /// </remarks>
    [HttpGet("insurance/expiring")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetInsuranceWatchItemDto>>> GetInsuranceExpiring(
        [FromQuery] int daysAhead = 60,
        [FromQuery] DateOnly? asOf = null)
    {
        var result = await _companyAssetService.GetInsuranceExpiringAsync(daysAhead, asOf);
        return Ok(result);
    }

    /// <summary>Assets whose cover has already lapsed, worst first.</summary>
    /// <remarks>
    /// Its own read rather than a filter on the one above, for the reason the overdue-maintenance
    /// read gives: "what do I renew this quarter" and "what are we running uninsured" are different
    /// jobs for different people, and four lapsed rows inside a list of eighty upcoming ones are
    /// lost rows.
    /// </remarks>
    [HttpGet("insurance/expired")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetInsuranceWatchItemDto>>> GetInsuranceExpired(
        [FromQuery] DateOnly? asOf = null)
    {
        var result = await _companyAssetService.GetInsuranceExpiredAsync(asOf);
        return Ok(result);
    }

    /// <summary>Assets marked insured that have never been given an expiry date.</summary>
    /// <remarks>
    /// The rows both reads above filter away — each needs <c>InsuranceExpiryDate != null</c> — so
    /// an asset somebody ticked as insured and never dated appears on no list anywhere and can
    /// never become due. The insurance twin of <c>unscheduled-maintenance</c>, and the shape most
    /// likely to be a claim nobody can actually make.
    /// </remarks>
    [HttpGet("insurance/undated")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetInsuranceWatchItemDto>>> GetInsuranceUndated()
    {
        var result = await _companyAssetService.GetInsuranceUndatedAsync();
        return Ok(result);
    }

    /// <summary>The register counted and totalled, with what needs attention — area 16 slice 11.</summary>
    /// <remarks>
    /// A summary, not a listing: the rows are already on <c>GET api/Assets/paged</c> under the same
    /// filters, and repeating them would make the document unbounded in exactly the case it is most
    /// wanted. ⚠ The watchlist counts honour the filter, so a report about one location carries
    /// that location's overdue count and not the organisation's.
    /// </remarks>
    [HttpGet("reports/register")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetRegisterReportDto>> GetRegisterReport(
        [FromQuery] Guid? assetTypeId = null,
        [FromQuery] Guid? unitId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] CompanyAssetStatus? status = null,
        [FromQuery] DateOnly? asOf = null)
    {
        var result = await _companyAssetService.GetRegisterReportAsync(
            assetTypeId, unitId, locationId, status, asOf);
        return Ok(result);
    }

    // ── the seam with the Maintenance module — slice 9b, decision D10 ──────────────────────

    /// <summary>The Maintenance module's assets HR can point at, already-linked ones flagged.</summary>
    /// <remarks>
    /// Mirrors <c>fixed-assets/linkable</c> from slice 2b, including the part that matters: rows
    /// another HR asset already claims are <b>returned and flagged</b>, not filtered out, so the
    /// user can see why the one they want is unavailable instead of hunting for a row that is on
    /// screen nowhere.
    /// </remarks>
    [HttpGet("maintenance-assets/linkable")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<MaintenanceAssetPickDto>>> GetLinkableMaintenanceAssets(
        [FromQuery] string? searchTerm = null)
    {
        var result = await _companyAssetService.GetLinkableMaintenanceAssetsAsync(searchTerm);
        return Ok(result);
    }

    /// <summary>Names this asset's counterpart in the Maintenance register.</summary>
    /// <remarks>
    /// Its own endpoint rather than a field on the asset PUT, because that PUT is <b>full-replace</b>
    /// (D-j): a screen that let somebody attach a link through it would silently null every field it
    /// did not resend. Linking is one fact and gets one door.
    /// </remarks>
    [HttpPost("{id:guid}/maintenance-link")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CompanyAssetDto>> LinkMaintenanceAsset(
        Guid id,
        [FromBody] LinkMaintenanceAssetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _companyAssetService.LinkMaintenanceAssetAsync(id, dto.MaintenanceAssetId);
        return Ok(result);
    }

    /// <summary>Severs the link. Refused while the asset is at the workshop.</summary>
    [HttpDelete("{id:guid}/maintenance-link")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CompanyAssetDto>> UnlinkMaintenanceAsset(Guid id)
    {
        var result = await _companyAssetService.UnlinkMaintenanceAssetAsync(id);
        return Ok(result);
    }

    /// <summary>Sends an asset to the workshop — AST-1's other half, decision D10.</summary>
    /// <remarks>
    /// Raises an <c>AssetAdmission</c> in the Maintenance module and opens the HR maintenance record
    /// that tracks it, in one act. Refused if the asset is not linked, if it is already at the
    /// workshop, or if it is disposed or lost — each in words.
    /// </remarks>
    [HttpPost("{id:guid}/send-for-maintenance")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetMaintenanceDto>> SendForMaintenance(
        Guid id,
        [FromBody] SendAssetForMaintenanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _maintenanceService.SendForMaintenanceAsync(id, dto);
        return Ok(result);
    }

    /// <summary>Every workshop admission raised for an asset, newest first.</summary>
    [HttpGet("{id:guid}/workshop-history")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceSummaryDto>>> GetWorkshopHistory(Guid id)
    {
        var result = await _maintenanceService.GetWorkshopHistoryAsync(id);
        return Ok(result);
    }

    /// <summary>AST-11 — the Finance fixed assets HR could register, already-linked ones flagged.</summary>
    [HttpGet("fixed-assets/linkable")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<FixedAssetPickDto>>> GetLinkableFixedAssets(
        [FromQuery] string? searchTerm = null)
    {
        var result = await _companyAssetService.GetLinkableFixedAssetsAsync(searchTerm);
        return Ok(result);
    }

    /// <summary>AST-11 — register an HR asset that stands for an existing Finance fixed asset.</summary>
    [HttpPost("from-fixed-asset")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CompanyAssetDto>> CreateAssetFromFixedAsset(
        [FromBody] CreateAssetFromFixedAssetDto dto)
    {
        var result = await _companyAssetService.CreateFromFixedAssetAsync(dto);
        return CreatedAtAction(nameof(GetAsset), new { id = result.Id }, result);
    }

    /// <summary>Create asset.</summary>
    [HttpPost]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CompanyAssetDto>> CreateAsset([FromBody] CreateCompanyAssetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _companyAssetService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAsset), new { id = created.Id }, created);
    }

    /// <summary>Update asset.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<CompanyAssetDto>> UpdateAsset(
        Guid id,
        [FromBody] UpdateCompanyAssetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _companyAssetService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete asset.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteAsset(Guid id)
    {
        await _companyAssetService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Dispose asset.</summary>
    [HttpPost("{id:guid}/dispose")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DisposeAsset(
        Guid id,
        [FromBody] DisposeAssetDto dto)
    {
        if (id != dto.AssetId) return BadRequest("ID mismatch");
        await _companyAssetService.DisposeAssetAsync(dto);
        return Ok(new { message = "Asset disposed" });
    }

    #endregion

    #region Asset Attribute Values

    /// <summary>Get attribute values for asset.</summary>
    [HttpGet("{assetId:guid}/attribute-values")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetAttributeValueDto>>> GetAssetAttributeValues(Guid assetId)
    {
        var result = await _attributeValueService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Get attribute value by id.</summary>
    [HttpGet("attribute-values/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAttributeValueDto>> GetAssetAttributeValue(Guid id)
    {
        var result = await _attributeValueService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create attribute value.</summary>
    [HttpPost("{assetId:guid}/attribute-values")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAttributeValueDto>> CreateAttributeValue(
        Guid assetId,
        [FromBody] CreateAssetAttributeValueDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _attributeValueService.CreateAsync(assetId, dto);
        return CreatedAtAction(nameof(GetAssetAttributeValue), new { id = created.Id }, created);
    }

    /// <summary>Update attribute value.</summary>
    [HttpPut("attribute-values/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAttributeValueDto>> UpdateAttributeValue(
        Guid id,
        [FromBody] UpdateAssetAttributeValueDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _attributeValueService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete attribute value.</summary>
    [HttpDelete("attribute-values/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteAttributeValue(Guid id)
    {
        await _attributeValueService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Assignments

    /// <summary>Get assignment by id.</summary>
    [HttpGet("assignments/{id:guid}")]
    // Self-service: self-or-HR in AssetAssignmentService.GetByIdAsync — the holder may read their own.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetAssignmentDto>> GetAssignment(Guid id)
    {
        var result = await _assignmentService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List assignments for tenant.</summary>
    [HttpGet("assignments")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetAssignments()
    {
        var result = await _assignmentService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged assignments.</summary>
    [HttpGet("assignments/paged")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<AssetAssignmentSummaryDto>>> GetAssignmentsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] AssignmentStatus? status = null)
    {
        var result = await _assignmentService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Assignments by asset.</summary>
    [HttpGet("assignments/asset/{assetId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetAssignmentsByAsset(Guid assetId)
    {
        var result = await _assignmentService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Current active assignment for asset.</summary>
    [HttpGet("assignments/asset/{assetId:guid}/current")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAssignmentDto>> GetCurrentAssignmentByAsset(Guid assetId)
    {
        var result = await _assignmentService.GetActiveAssignmentForAssetAsync(assetId);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Assignments by employee.</summary>
    [HttpGet("assignments/employee/{employeeId:guid}")]
    // Self-service: self-or-HR in AssetAssignmentService.GetByEmployeeIdAsync.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetAssignmentsByEmployee(Guid employeeId)
    {
        var result = await _assignmentService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    /// <summary>Active assignments for employee.</summary>
    [HttpGet("assignments/employee/{employeeId:guid}/active")]
    // Self-service: self-or-HR in AssetAssignmentService.GetActiveAssignmentsForEmployeeAsync.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetActiveAssignments(Guid employeeId)
    {
        var result = await _assignmentService.GetActiveAssignmentsForEmployeeAsync(employeeId);
        return Ok(result);
    }

    /// <summary>Custodies past their expected return date, most overdue first.</summary>
    /// <remarks>
    /// Slice 11 gave this an <c>asOf</c> and an ordering. It had neither, and an exception list
    /// that cannot put its worst row at the top is a list somebody reads once.
    /// </remarks>
    [HttpGet("assignments/overdue")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetOverdueAssignments(
        [FromQuery] DateOnly? asOf = null)
    {
        var result = await _assignmentService.GetOverdueAssignmentsAsync(asOf);
        return Ok(result);
    }

    /// <summary>Custodies coming due back inside a window — area 16 slice 11.</summary>
    /// <remarks>
    /// The plan to the read above's exception list. Until this, a return appeared on exactly one
    /// list and only once it was already late, so "what is coming back this fortnight" — the
    /// question somebody arranging a handover actually has — had no answer anywhere.
    /// </remarks>
    [HttpGet("assignments/due-for-return")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetAssignmentsDueForReturn(
        [FromQuery] int daysAhead = 14,
        [FromQuery] DateOnly? asOf = null)
    {
        var result = await _assignmentService.GetAssignmentsDueForReturnAsync(daysAhead, asOf);
        return Ok(result);
    }

    /// <summary>Create assignment.</summary>
    [HttpPost("assignments")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAssignmentDto>> CreateAssignment([FromBody] CreateAssetAssignmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _assignmentService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAssignment), new { id = created.Id }, created);
    }

    /// <summary>Update assignment.</summary>
    [HttpPut("assignments/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAssignmentDto>> UpdateAssignment(
        Guid id,
        [FromBody] UpdateAssetAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _assignmentService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete assignment.</summary>
    [HttpDelete("assignments/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteAssignment(Guid id)
    {
        await _assignmentService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Employee acknowledges assignment.</summary>
    [HttpPost("assignments/{id:guid}/acknowledge")]
    // Self-service: AST-8. The ASSIGNEE ONLY — HR included in the exclusion. See
    // AssetActor.EnsureIsSubject and defect D-b.
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> AcknowledgeAssignment(
        Guid id,
        [FromBody] AcknowledgeAssignmentDto dto)
    {
        dto.AssignmentId = id;
        await _assignmentService.AcknowledgeAssignmentAsync(dto);
        return Ok(new { message = "Assignment acknowledged" });
    }

    /// <summary>The responsibility-and-terms document, to display and print for signature (AST-5).</summary>
    /// <remarks>
    /// <para>Rendered on demand from the HR-editable <c>Assets/AssetResponsibilityTerms</c> template
    /// as a self-contained HTML document — the same shape as the probation confirmation letter, and
    /// printable to PDF from the browser for physical signature.</para>
    ///
    /// <para>Self-service: self-or-HR in the service. The document states what one named employee is
    /// responsible for, and it is theirs to print as much as it is HR's to serve. It records nothing
    /// on the assignment — printing a copy is not serving it on somebody.</para>
    /// </remarks>
    [HttpGet("assignments/{id:guid}/terms-document")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetTermsLetterDto>> GetAssignmentTermsDocument(Guid id)
    {
        var letter = await _termsLetterService.GenerateAsync(id);
        return Ok(letter);
    }

    /// <summary>Email the responsibility-and-terms document to the holder (AST-5b).</summary>
    [HttpPost("assignments/{id:guid}/terms-document/email")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetTermsLetterSendResultDto>> EmailAssignmentTermsDocument(Guid id)
    {
        var result = await _termsLetterService.EmailAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Report an assigned asset lost or damaged beyond return — slice 7.
    /// </summary>
    /// <remarks>
    /// The counterpart of the return below, for the assets that never come back. HR-only for the
    /// same reason the return is: it closes a custody and changes what the register says an asset is.
    /// </remarks>
    [HttpPost("assignments/{id:guid}/report-incident")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ReportAssignmentIncident(
        Guid id,
        [FromBody] ReportAssetIncidentDto dto)
    {
        dto.AssignmentId = id;
        await _assignmentService.ReportIncidentAsync(dto);
        return Ok(new { message = "Incident recorded" });
    }

    /// <summary>Return assigned asset.</summary>
    [HttpPost("assignments/{id:guid}/return")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ReturnAsset(
        Guid id,
        [FromBody] ReturnAssetDto dto)
    {
        dto.AssignmentId = id;
        await _assignmentService.ReturnAssetAsync(dto);
        return Ok(new { message = "Asset returned" });
    }

    #endregion

    #region Maintenance

    /// <summary>Get maintenance by id.</summary>
    [HttpGet("maintenance/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetMaintenanceDto>> GetMaintenance(Guid id)
    {
        var result = await _maintenanceService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List maintenance records.</summary>
    [HttpGet("maintenance")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceSummaryDto>>> GetMaintenanceList()
    {
        var result = await _maintenanceService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged maintenance records.</summary>
    [HttpGet("maintenance/paged")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<AssetMaintenanceSummaryDto>>> GetMaintenancePaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] MaintenanceStatus? status = null)
    {
        var result = await _maintenanceService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Maintenance by asset.</summary>
    [HttpGet("maintenance/asset/{assetId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceSummaryDto>>> GetMaintenanceByAsset(Guid assetId)
    {
        var result = await _maintenanceService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Scheduled maintenance in range.</summary>
    [HttpGet("maintenance/schedule")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceSummaryDto>>> GetScheduledMaintenance(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var result = await _maintenanceService.GetScheduledMaintenanceAsync(from, to);
        return Ok(result);
    }

    /// <summary>Create maintenance.</summary>
    [HttpPost("maintenance")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetMaintenanceDto>> CreateMaintenance([FromBody] CreateAssetMaintenanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _maintenanceService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetMaintenance), new { id = created.Id }, created);
    }

    /// <summary>Update maintenance.</summary>
    [HttpPut("maintenance/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetMaintenanceDto>> UpdateMaintenance(
        Guid id,
        [FromBody] UpdateAssetMaintenanceDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _maintenanceService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Complete maintenance.</summary>
    [HttpPost("maintenance/{id:guid}/complete")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> CompleteMaintenance(
        Guid id,
        [FromBody] string? completionNotes = null)
    {
        await _maintenanceService.CompleteMaintenanceAsync(id, completionNotes);
        return Ok(new { message = "Maintenance completed" });
    }

    /// <summary>Delete maintenance.</summary>
    [HttpDelete("maintenance/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteMaintenance(Guid id)
    {
        await _maintenanceService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Asset Images

    /// <summary>Get image by id.</summary>
    [HttpGet("images/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetImageDto>> GetImage(Guid id)
    {
        var result = await _assetImageService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List images for asset.</summary>
    [HttpGet("{assetId:guid}/images")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetImageDto>>> GetImages(Guid assetId)
    {
        var result = await _assetImageService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Add image record to asset.</summary>
    /// <summary>
    /// Uploads a photograph of an asset through the controlled gate — area 16 slice 12b.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Multipart, and it replaces a JSON endpoint that stored nothing.</b> The old route
    /// took <c>fileName</c> and <c>filePath</c> in a body: the caller named a path, the server wrote
    /// the string down, and no file existed anywhere. Every other HR attachment surface goes through
    /// <see cref="HrAttachmentUpload"/> — scanning, central-DMS registration, and a rollback if the
    /// row write then fails — and there was no reason for this one not to.</para>
    ///
    /// <para>The category is <c>hr-asset-documents</c> and it is scan-mandatory. Most of what lands
    /// here is mundane, but the same category carries photographs of damage taken at a return, which
    /// are evidence in a money claim against a named employee.</para>
    /// </remarks>
    [HttpPost("{assetId:guid}/images")]
    [Authorize(Roles = HrRoles)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> AddImage(
        Guid assetId,
        IFormFile file,
        [FromForm] string? caption,
        CancellationToken cancellationToken = default)
    {
        // ⚠ **Resolve the asset FIRST, before a single byte is stored.**
        //
        // The first version of this ran the upload straight away and let the service's own
        // "no asset was found" refusal come out of the persist callback, on the theory that
        // `HrAttachmentUpload` would then roll the stored document back. It does not reach that
        // far: DMS registration requires a non-empty source-record id and throws inside
        // `UploadAsync`, which is *before* the persist try — so the bytes were written to disk,
        // nothing rolled them back, and the caller met a generic 400 with the service's words
        // discarded. An orphaned file for an asset that never existed.
        //
        // Every other HR upload site resolves its parent first (see `CheckInsController`). This one
        // now does too, and the service keeps its own check as the guard against a race.
        var asset = await _companyAssetService.GetByIdAsync(assetId);
        if (asset is null)
            return NotFound(new { message = $"No asset was found with id {assetId}." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "CompanyAsset",
            sourceRecordId: assetId,
            sourceLabel: "Company asset photograph",
            documentType: "CompanyAssetImage",
            description: caption,
            persist: (uploadedById, document) => _assetImageService.CreateUploadedAsync(
                assetId,
                new CreateAssetImageDto { Caption = caption },
                uploadedById,
                document.OriginalFileName,
                document.FilePath,
                document.FileSize,
                document.FileUploadRecordId,
                document.DocumentRecordId,
                document.DocumentVersionId),
            cancellationToken,
            category: ControlledFileUploadCategories.HrAssetDocuments);
    }

    /// <summary>
    /// Streams an asset photograph. The file lives outside the web root, so this is the only way
    /// to it — <c>filePath</c> on the DTO is a stored location, never a URL.
    /// </summary>
    [HttpGet("images/{id:guid}/download")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DownloadImage(Guid id, CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        // ⚠ The entitlement check is the CALLING endpoint's, always: neither the download helper
        // nor the DMS performs one. Going through the service rather than the DbSet is what applies
        // this area's tenant scoping.
        var image = await _assetImageService.GetByIdAsync(id);
        if (image is null) return NotFound(new { message = "Image not found" });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            image.DocumentRecordId, image.DocumentVersionId, image.FileUploadRecordId,
            image.FilePath, image.FileName, fallbackContentType: null,
            inline: true, cancellationToken);
    }

    /// <summary>Delete image.</summary>
    [HttpDelete("images/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        await _assetImageService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Attachments

    /// <summary>Get attachment by id.</summary>
    [HttpGet("attachments/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAttachmentDto>> GetAttachment(Guid id)
    {
        var result = await _attachmentService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List attachments for asset.</summary>
    [HttpGet("{assetId:guid}/attachments")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetAttachmentDto>>> GetAttachments(Guid assetId)
    {
        var result = await _attachmentService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Add attachment to asset.</summary>
    /// <summary>
    /// Uploads a document against an asset through the controlled gate — the invoice, the warranty
    /// certificate, the manual. Multipart; see <see cref="AddImage"/> for what it replaced.
    /// </summary>
    [HttpPost("{assetId:guid}/attachments")]
    [Authorize(Roles = HrRoles)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> AddAttachment(
        Guid assetId,
        IFormFile file,
        [FromForm] string? description,
        CancellationToken cancellationToken = default)
    {
        // ⚠ **Resolve the asset FIRST, before a single byte is stored.**
        //
        // The first version of this ran the upload straight away and let the service's own
        // "no asset was found" refusal come out of the persist callback, on the theory that
        // `HrAttachmentUpload` would then roll the stored document back. It does not reach that
        // far: DMS registration requires a non-empty source-record id and throws inside
        // `UploadAsync`, which is *before* the persist try — so the bytes were written to disk,
        // nothing rolled them back, and the caller met a generic 400 with the service's words
        // discarded. An orphaned file for an asset that never existed.
        //
        // Every other HR upload site resolves its parent first (see `CheckInsController`). This one
        // now does too, and the service keeps its own check as the guard against a race.
        var asset = await _companyAssetService.GetByIdAsync(assetId);
        if (asset is null)
            return NotFound(new { message = $"No asset was found with id {assetId}." });

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUser, _logger, file,
            sourceEntityType: "CompanyAsset",
            sourceRecordId: assetId,
            sourceLabel: "Company asset document",
            documentType: "CompanyAssetAttachment",
            description: description,
            persist: (uploadedById, document) => _attachmentService.CreateUploadedAsync(
                assetId,
                new CreateAssetAttachmentDto { Description = description },
                uploadedById,
                document.OriginalFileName,
                document.FilePath,
                document.FileSize,
                document.FileUploadRecordId,
                document.DocumentRecordId,
                document.DocumentVersionId),
            cancellationToken,
            category: ControlledFileUploadCategories.HrAssetDocuments);
    }

    /// <summary>Streams an asset document. The only way to the bytes — see <see cref="DownloadImage"/>.</summary>
    [HttpGet("attachments/{id:guid}/download")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DownloadAttachment(Guid id, CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _attachmentService.GetByIdAsync(id);
        if (attachment is null) return NotFound(new { message = "Attachment not found" });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId, attachment.FileUploadRecordId,
            attachment.FilePath, attachment.FileName, fallbackContentType: null,
            inline: false, cancellationToken);
    }

    /// <summary>Delete attachment.</summary>
    [HttpDelete("attachments/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteAttachment(Guid id)
    {
        await _attachmentService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Requisitions

    /// <summary>Get requisition by id.</summary>
    [HttpGet("requisitions/{id:guid}")]
    // Self-service: self-or-HR in AssetRequisitionService.GetByIdAsync.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetRequisitionDto>> GetRequisition(Guid id)
    {
        var result = await _requisitionService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List requisitions.</summary>
    [HttpGet("requisitions")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetRequisitionSummaryDto>>> GetRequisitions()
    {
        var result = await _requisitionService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged requisitions.</summary>
    [HttpGet("requisitions/paged")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<AssetRequisitionSummaryDto>>> GetRequisitionsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] AssetRequisitionStatus? status = null)
    {
        var result = await _requisitionService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Requisitions by requester.</summary>
    [HttpGet("requisitions/requested-by/{employeeId:guid}")]
    // Self-service: self-or-HR in AssetRequisitionService.GetByRequestedByIdAsync.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<IEnumerable<AssetRequisitionSummaryDto>>> GetRequisitionsByRequester(Guid employeeId)
    {
        var result = await _requisitionService.GetByRequestedByIdAsync(employeeId);
        return Ok(result);
    }

    /// <summary>Pending approvals.</summary>
    [HttpGet("requisitions/pending-approvals")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetRequisitionSummaryDto>>> GetPendingRequisitionApprovals()
    {
        var result = await _requisitionService.GetPendingApprovalsAsync();
        return Ok(result);
    }

    /// <summary>Create requisition.</summary>
    [HttpPost("requisitions")]
    // Self-service: AST-6. Anyone may request an asset for themselves; the requester is taken from the
    // token, never from the payload.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetRequisitionDto>> CreateRequisition([FromBody] CreateAssetRequisitionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _requisitionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetRequisition), new { id = created.Id }, created);
    }

    /// <summary>Update requisition.</summary>
    [HttpPut("requisitions/{id:guid}")]
    // Self-service: the requester may correct their own request while it is undecided; HR may correct anyone's.
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetRequisitionDto>> UpdateRequisition(
        Guid id,
        [FromBody] UpdateAssetRequisitionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _requisitionService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete requisition.</summary>
    [HttpDelete("requisitions/{id:guid}")]
    // Self-service: the requester may withdraw their own undecided request; HR may withdraw anyone's.
    [Authorize(Policy = "InternalOnly")]
    public async Task<IActionResult> DeleteRequisition(Guid id)
    {
        await _requisitionService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Submit a draft requisition for approval.</summary>
    /// <remarks>
    /// Self-service: the requester sends their own request, HR sends anyone's — the same rule as
    /// editing one. Where it goes next is the tenant's published workflow definition's business,
    /// not this controller's.
    /// </remarks>
    [HttpPost("requisitions/{id:guid}/submit")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetRequisitionDto>> SubmitRequisition(Guid id)
    {
        var result = await _requisitionService.SubmitAsync(id);
        return Ok(result);
    }

    /// <summary>Recall a submitted requisition back to draft.</summary>
    /// <remarks>Self-service: only the person who raised it, checked on the record.</remarks>
    [HttpPost("requisitions/{id:guid}/recall")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetRequisitionDto>> RecallRequisition(
        Guid id,
        [FromBody] RecallAssetRequestDto? dto = null)
    {
        var result = await _requisitionService.RecallAsync(id, dto?.Reason);
        return Ok(result);
    }

    /// <summary>Approve requisition.</summary>
    [HttpPost("requisitions/{id:guid}/approve")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ApproveRequisition(
        Guid id,
        [FromBody] ApproveAssetRequisitionDto dto)
    {
        await _requisitionService.ApproveAsync(id, dto);
        return Ok(new { message = "Requisition approved" });
    }

    /// <summary>Reject requisition.</summary>
    [HttpPost("requisitions/{id:guid}/reject")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> RejectRequisition(
        Guid id,
        [FromBody] RejectAssetRequisitionDto dto)
    {
        await _requisitionService.RejectAsync(id, dto);
        return Ok(new { message = "Requisition rejected" });
    }

    /// <summary>Fulfill requisition.</summary>
    [HttpPost("requisitions/{id:guid}/fulfill")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> FulfillRequisition(
        Guid id,
        [FromBody] FulfillAssetRequisitionDto dto)
    {
        await _requisitionService.FulfillAsync(id, dto);
        return Ok(new { message = "Requisition fulfilled" });
    }

    #endregion

    #region Transfers

    /// <summary>Get transfer by id.</summary>
    [HttpGet("transfers/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTransferDto>> GetTransfer(Guid id)
    {
        var result = await _transferService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List transfers.</summary>
    [HttpGet("transfers")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetTransferSummaryDto>>> GetTransfers()
    {
        var result = await _transferService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged transfers.</summary>
    [HttpGet("transfers/paged")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<AssetTransferSummaryDto>>> GetTransfersPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] HRAssetTransferStatus? status = null)
    {
        var result = await _transferService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Transfers by asset.</summary>
    [HttpGet("transfers/asset/{assetId:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetTransferSummaryDto>>> GetTransfersByAsset(Guid assetId)
    {
        var result = await _transferService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Pending transfers.</summary>
    [HttpGet("transfers/pending")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetTransferSummaryDto>>> GetPendingTransfers()
    {
        var result = await _transferService.GetPendingTransfersAsync();
        return Ok(result);
    }

    /// <summary>Create transfer.</summary>
    [HttpPost("transfers")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTransferDto>> CreateTransfer([FromBody] CreateAssetTransferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _transferService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetTransfer), new { id = created.Id }, created);
    }

    /// <summary>Update transfer.</summary>
    [HttpPut("transfers/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTransferDto>> UpdateTransfer(
        Guid id,
        [FromBody] UpdateAssetTransferDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _transferService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete transfer.</summary>
    [HttpDelete("transfers/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteTransfer(Guid id)
    {
        await _transferService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Submit a draft transfer for approval.</summary>
    [HttpPost("transfers/{id:guid}/submit")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTransferDto>> SubmitTransfer(Guid id)
    {
        var result = await _transferService.SubmitAsync(id);
        return Ok(result);
    }

    /// <summary>Recall a submitted transfer back to draft.</summary>
    [HttpPost("transfers/{id:guid}/recall")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetTransferDto>> RecallTransfer(
        Guid id,
        [FromBody] RecallAssetRequestDto? dto = null)
    {
        var result = await _transferService.RecallAsync(id, dto?.Reason);
        return Ok(result);
    }

    /// <summary>Approve transfer.</summary>
    [HttpPost("transfers/{id:guid}/approve")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ApproveTransfer(Guid id)
    {
        await _transferService.ApproveAsync(id);
        return Ok(new { message = "Transfer approved" });
    }

    /// <summary>Complete transfer.</summary>
    [HttpPost("transfers/{id:guid}/complete")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> CompleteTransfer(Guid id)
    {
        await _transferService.CompleteAsync(id);
        return Ok(new { message = "Transfer completed" });
    }

    /// <summary>Reject transfer.</summary>
    [HttpPost("transfers/{id:guid}/reject")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> RejectTransfer(Guid id)
    {
        await _transferService.RejectAsync(id);
        return Ok(new { message = "Transfer rejected" });
    }

    #endregion

    #region Surcharges — AST-3, defect D-d, decision D9 (slice 7)

    // Charging an employee money is HR's act throughout, with exactly two exceptions: an employee
    // may READ a charge once it has been put to them, and only they may ANSWER it. Both are gated
    // in the service, on the record — an attribute cannot express "the person this is about".

    /// <summary>One surcharge, in full. Self-or-HR, and invisible to the employee until served.</summary>
    [HttpGet("surcharges/{id:guid}")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetSurchargeDto>> GetSurcharge(Guid id)
    {
        var result = await _surchargeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Every surcharge on the tenant.</summary>
    [HttpGet("surcharges")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetSurchargeSummaryDto>>> GetSurcharges()
        => Ok(await _surchargeService.GetAllAsync());

    [HttpGet("surcharges/paged")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<PagedResult<AssetSurchargeSummaryDto>>> GetSurchargesPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] AssetSurchargeStatus? status = null)
        => Ok(await _surchargeService.GetPagedAsync(page, pageSize, searchTerm, status));

    /// <summary>Charges raised against one employee. Self-or-HR.</summary>
    [HttpGet("surcharges/employee/{employeeId:guid}")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<IEnumerable<AssetSurchargeSummaryDto>>> GetSurchargesForEmployee(Guid employeeId)
        => Ok(await _surchargeService.GetByEmployeeIdAsync(employeeId));

    [HttpGet("assignments/{assignmentId:guid}/surcharges")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetSurchargeSummaryDto>>> GetSurchargesForAssignment(Guid assignmentId)
        => Ok(await _surchargeService.GetByAssignmentIdAsync(assignmentId));

    /// <summary>Decided charges that still carry a balance — what this module says is owed.</summary>
    [HttpGet("surcharges/outstanding")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetSurchargeSummaryDto>>> GetOutstandingSurcharges()
        => Ok(await _surchargeService.GetOutstandingAsync());

    /// <summary>
    /// The read-only projection payroll consumes — AST-3's second half.
    /// </summary>
    /// <remarks>
    /// HR declares what is owed and how it was said to be recovered; payroll runs the deduction.
    /// Nothing here computes a payslip. See the payroll ownership boundary and decision D2.
    /// </remarks>
    [HttpGet("surcharges/payroll-deductions")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetSurchargePayrollLineDto>>> GetSurchargePayrollLines()
        => Ok(await _surchargeService.GetPayrollDeductionLinesAsync());

    [HttpPost("surcharges")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> CreateSurcharge([FromBody] CreateAssetSurchargeDto dto)
    {
        var created = await _surchargeService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetSurcharge), new { id = created.Id }, created);
    }

    [HttpPut("surcharges/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> UpdateSurcharge(
        Guid id,
        [FromBody] UpdateAssetSurchargeDto dto)
        => Ok(await _surchargeService.UpdateAsync(id, dto));

    [HttpDelete("surcharges/{id:guid}")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> DeleteSurcharge(Guid id)
    {
        await _surchargeService.DeleteAsync(id);
        return Ok(new { message = "Surcharge deleted" });
    }

    /// <summary>Puts the charge to the employee. Until this, they cannot see it — decision D9.</summary>
    [HttpPost("surcharges/{id:guid}/notify-employee")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> NotifySurchargeEmployee(Guid id)
        => Ok(await _surchargeService.NotifyEmployeeAsync(id));

    /// <summary>
    /// The employee's own answer — refused for everybody else, <b>HR included</b>.
    /// </summary>
    /// <remarks>
    /// The same shape as acknowledging receipt of an asset, and for the same reason: HR entering an
    /// employee's acceptance on their behalf is not a right of reply, it is the absence of one.
    /// </remarks>
    [HttpPost("surcharges/{id:guid}/respond")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<AssetSurchargeDto>> RespondToSurcharge(
        Guid id,
        [FromBody] RespondToAssetSurchargeDto dto)
        => Ok(await _surchargeService.RespondAsync(id, dto));

    /// <summary>Sends the charge for approval — refused until the employee has been asked.</summary>
    [HttpPost("surcharges/{id:guid}/submit")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> SubmitSurcharge(
        Guid id,
        [FromBody] SubmitAssetSurchargeDto? dto = null)
        => Ok(await _surchargeService.SubmitAsync(id, dto ?? new SubmitAssetSurchargeDto()));

    [HttpPost("surcharges/{id:guid}/recall")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> RecallSurcharge(
        Guid id,
        [FromBody] RecallAssetRequestDto? dto = null)
        => Ok(await _surchargeService.RecallAsync(id, dto?.Reason));

    [HttpPost("surcharges/{id:guid}/approve")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> ApproveSurcharge(
        Guid id,
        [FromBody] ApproveAssetSurchargeDto dto)
    {
        await _surchargeService.ApproveAsync(id, dto);
        return Ok(new { message = "Surcharge approved" });
    }

    [HttpPost("surcharges/{id:guid}/reject")]
    [Authorize(Roles = HrRoles)]
    public async Task<IActionResult> RejectSurcharge(
        Guid id,
        [FromBody] RejectAssetSurchargeDto dto)
    {
        await _surchargeService.RejectAsync(id, dto);
        return Ok(new { message = "Surcharge rejected" });
    }

    /// <summary>How the approved amount is to be recovered — declared, not deducted.</summary>
    [HttpPut("surcharges/{id:guid}/recovery-plan")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> SetSurchargeRecoveryPlan(
        Guid id,
        [FromBody] SetAssetSurchargeRecoveryPlanDto dto)
        => Ok(await _surchargeService.SetRecoveryPlanAsync(id, dto));

    /// <summary>Records money actually collected. Cannot take more than is outstanding.</summary>
    [HttpPost("surcharges/{id:guid}/recoveries")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> RecordSurchargeRecovery(
        Guid id,
        [FromBody] RecordAssetSurchargeRecoveryDto dto)
        => Ok(await _surchargeService.RecordRecoveryAsync(id, dto));

    /// <summary>Forgives what is still outstanding. What was collected stays collected.</summary>
    [HttpPost("surcharges/{id:guid}/waive")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> WaiveSurcharge(
        Guid id,
        [FromBody] WaiveAssetSurchargeDto dto)
        => Ok(await _surchargeService.WaiveAsync(id, dto));

    /// <summary>Withdraws a charge raised in error, before anybody has ruled on it.</summary>
    [HttpPost("surcharges/{id:guid}/cancel")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetSurchargeDto>> CancelSurcharge(
        Guid id,
        [FromBody] CancelAssetSurchargeDto dto)
        => Ok(await _surchargeService.CancelAsync(id, dto));

    #endregion

    #region Rental and the payroll seam — AST-9, AST-10, decision D2 (slice 8)

    /// <summary>
    /// Declare what an employee is charged for holding a rentable asset — AST-10.
    /// </summary>
    /// <remarks>
    /// Refused unless the asset is marked rentable. Nothing here deducts anything: payroll pulls
    /// the projection below and runs its own deduction (decision D2).
    /// </remarks>
    [HttpPut("assignments/{id:guid}/rental-terms")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAssignmentDto>> SetAssignmentRentalTerms(
        Guid id,
        [FromBody] SetAssetRentalTermsDto dto)
        => Ok(await _assignmentService.SetRentalTermsAsync(id, dto));

    /// <summary>Remove rental terms declared in error — not the way to end a tenancy that ran.</summary>
    [HttpDelete("assignments/{id:guid}/rental-terms")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<AssetAssignmentDto>> ClearAssignmentRentalTerms(Guid id)
        => Ok(await _assignmentService.ClearRentalTermsAsync(id));

    /// <summary>
    /// The read-only rental projection payroll pulls — AST-10, decision D2.
    /// </summary>
    /// <remarks>
    /// <para>Give either a <c>period</c> of <c>YYYY-MM</c>, or an explicit <c>from</c> and
    /// <c>to</c>. Defaults to the current calendar month.</para>
    ///
    /// <para>⚠ The figures are <b>not prorated</b>. Each line carries the full periodic rate and
    /// the window it applies to; how much of it falls in a given pay run is payroll's calculation,
    /// made with payroll's calendar. HR computing a part-month here would be guessing at another
    /// module's period boundaries in the one place a mistake reaches somebody's take-home pay.</para>
    /// </remarks>
    [HttpGet("payroll/rental-deductions")]
    [Authorize(Roles = HrRoles)]
    public async Task<ActionResult<IEnumerable<AssetRentalPayrollLineDto>>> GetRentalPayrollLines(
        [FromQuery] string? period = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        DateOnly start, end;

        if (!string.IsNullOrWhiteSpace(period))
        {
            // ⚠ Parsed rather than trusted. `period=2026-13` and `period=banana` both used to be a
            // silent fall-through to "this month" in surfaces like this one, which answers 200 with
            // the wrong month's money in it — the worst possible failure for a payroll input.
            if (!DateTime.TryParseExact(period.Trim(), "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
            {
                return Problem(
                    detail: $"'{period}' is not a period. Use YYYY-MM, or give explicit from and to dates.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Period");
            }

            start = new DateOnly(parsed.Year, parsed.Month, 1);
            end = start.AddMonths(1).AddDays(-1);
        }
        else if (from is { } f && to is { } t)
        {
            start = f;
            end = t;
        }
        else if (from is null && to is null)
        {
            var now = DateTime.UtcNow;
            start = new DateOnly(now.Year, now.Month, 1);
            end = start.AddMonths(1).AddDays(-1);
        }
        else
        {
            return Problem(
                detail: "Give both from and to, or neither.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Incomplete Period");
        }

        return Ok(await _assignmentService.GetRentalPayrollLinesAsync(start, end));
    }

    #endregion
}
