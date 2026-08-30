using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/medical-insurance")]
// Medical records are special-category personal data. This controller previously carried a
// bare [Authorize], so any authenticated employee could read them. Read is the class-level
// floor; write and delete are tightened per action.
[Authorize(Policy = HrPermissions.MedicalReadPolicy)]
public class MedicalInsuranceController : MedicalControllerBase
{
    private readonly IMedicalInsuranceService _service;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;

    public MedicalInsuranceController(
        IMedicalInsuranceService service,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
    }

    // =========================================================================
    // PROVIDERS
    // =========================================================================

    [HttpGet("providers")]
    public async Task<ActionResult<IEnumerable<MedicalInsuranceProviderSummaryDto>>> GetProviders(
        [FromQuery] MedicalInsuranceProviderType? type = null,
        [FromQuery] bool onlyActive = false,
        CancellationToken ct = default)
    {
        if (type.HasValue)
            return Ok(await _service.GetProvidersByTypeAsync(type.Value, ct));

        return Ok(onlyActive
            ? await _service.GetActiveProvidersAsync(ct)
            : await _service.GetAllProvidersAsync(ct));
    }

    [HttpGet("providers/search")]
    public async Task<ActionResult<IEnumerable<MedicalInsuranceProviderSummaryDto>>> SearchProviders(
        [FromQuery] string term,
        CancellationToken ct)
        => Ok(await _service.SearchProvidersAsync(term, ct));

    [HttpGet("providers/{id:guid}")]
    public async Task<ActionResult<MedicalInsuranceProviderDto>> GetProvider(Guid id, CancellationToken ct)
        => Ok(await _service.GetProviderByIdAsync(id, ct));

    [HttpGet("providers/{id:guid}/details")]
    public async Task<ActionResult<MedicalInsuranceProviderDetailDto>> GetProviderWithDetails(Guid id, CancellationToken ct)
        => Ok(await _service.GetProviderWithDetailsAsync(id, ct));

    [HttpGet("providers/code/{code}")]
    public async Task<ActionResult<MedicalInsuranceProviderDto?>> GetProviderByCode(string code, CancellationToken ct)
        => Ok(await _service.GetProviderByCodeAsync(code, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("providers")]
    public async Task<ActionResult<MedicalInsuranceProviderDto>> CreateProvider(
        [FromBody] CreateMedicalInsuranceProviderDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateProviderAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetProvider), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("providers/{id:guid}")]
    public async Task<ActionResult<MedicalInsuranceProviderDto>> UpdateProvider(
        Guid id,
        [FromBody] UpdateMedicalInsuranceProviderDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateProviderAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("providers/{id:guid}")]
    public async Task<IActionResult> DeleteProvider(Guid id, CancellationToken ct)
    {
        await _service.DeleteProviderAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // PLANS
    // =========================================================================

    [HttpGet("plans/{id:guid}")]
    public async Task<ActionResult<MedicalInsurancePlanDto>> GetPlan(Guid id, CancellationToken ct)
        => Ok(await _service.GetPlanByIdAsync(id, ct));

    [HttpGet("providers/{providerId:guid}/plans")]
    public async Task<ActionResult<IEnumerable<MedicalInsurancePlanDto>>> GetPlansByProvider(
        Guid providerId,
        CancellationToken ct)
        => Ok(await _service.GetPlansByProviderAsync(providerId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("plans")]
    public async Task<ActionResult<MedicalInsurancePlanDto>> CreatePlan(
        [FromBody] CreateMedicalInsurancePlanDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreatePlanAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetPlan), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<MedicalInsurancePlanDto>> UpdatePlan(
        Guid id,
        [FromBody] UpdateMedicalInsurancePlanDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdatePlanAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct)
    {
        await _service.DeletePlanAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // EMPLOYEE POLICIES
    // =========================================================================

    [HttpGet("policies")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>>> GetAllPolicies(CancellationToken ct)
        => Ok(await _service.GetAllPoliciesAsync(ct));

    [HttpGet("policies/{id:guid}")]
    public async Task<ActionResult<EmployeeMedicalInsurancePolicyDto>> GetPolicy(Guid id, CancellationToken ct)
        => Ok(await _service.GetPolicyByIdAsync(id, ct));

    [HttpGet("policies/{id:guid}/details")]
    public async Task<ActionResult<EmployeeMedicalInsurancePolicyDetailDto>> GetPolicyWithDetails(Guid id, CancellationToken ct)
        => Ok(await _service.GetPolicyWithDetailsAsync(id, ct));

    [HttpGet("employees/{employeeId:guid}/policies")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>>> GetPoliciesByEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetPoliciesByEmployeeAsync(employeeId, ct));

    [HttpGet("employees/{employeeId:guid}/policies/active")]
    public async Task<ActionResult<EmployeeMedicalInsurancePolicyDto?>> GetActivePolicyForEmployee(
        Guid employeeId,
        CancellationToken ct)
        => Ok(await _service.GetActivePolicyForEmployeeAsync(employeeId, ct));

    [HttpGet("policies/expiring")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>>> GetExpiringPolicies(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetExpiringPoliciesAsync(daysAhead, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("policies")]
    public async Task<ActionResult<EmployeeMedicalInsurancePolicyDto>> CreatePolicy(
        [FromBody] CreateEmployeeMedicalInsurancePolicyDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreatePolicyAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetPolicy), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("policies/{id:guid}")]
    public async Task<ActionResult<EmployeeMedicalInsurancePolicyDto>> UpdatePolicy(
        Guid id,
        [FromBody] UpdateEmployeeMedicalInsurancePolicyDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdatePolicyAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("policies/{id:guid}/cancel")]
    public async Task<IActionResult> CancelPolicy(Guid id, [FromBody] CancelEmployeeMedicalInsurancePolicyDto dto, CancellationToken ct)
    {
        dto.PolicyId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        await _service.CancelPolicyAsync(dto, userId, ct);
        return Ok(new { message = "Policy cancelled." });
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("policies/{id:guid}")]
    public async Task<IActionResult> DeletePolicy(Guid id, CancellationToken ct)
    {
        await _service.DeletePolicyAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // POLICY DEPENDENTS
    // =========================================================================

    [HttpGet("policies/{policyId:guid}/dependents")]
    public async Task<ActionResult<IEnumerable<MedicalInsurancePolicyDependentDto>>> GetPolicyDependents(
        Guid policyId,
        CancellationToken ct)
        => Ok(await _service.GetPolicyDependentsAsync(policyId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("policies/{policyId:guid}/dependents")]
    public async Task<ActionResult<MedicalInsurancePolicyDependentDto>> AddPolicyDependent(
        Guid policyId,
        [FromBody] AddMedicalInsurancePolicyDependentDto dto,
        CancellationToken ct)
    {
        dto.PolicyId = policyId;
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddPolicyDependentAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetPolicyDependents), new { policyId }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("dependents/{id:guid}")]
    public async Task<ActionResult<MedicalInsurancePolicyDependentDto>> UpdatePolicyDependent(
        Guid id,
        [FromBody] UpdateMedicalInsurancePolicyDependentDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdatePolicyDependentAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("dependents/{id:guid}")]
    public async Task<IActionResult> DeletePolicyDependent(Guid id, CancellationToken ct)
    {
        await _service.DeletePolicyDependentAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // INSURANCE CLAIMS (TO PROVIDER)
    // =========================================================================

    [HttpGet("insurance-claims/{id:guid}")]
    public async Task<ActionResult<MedicalInsuranceClaimDto>> GetInsuranceClaim(Guid id, CancellationToken ct)
        => Ok(await _service.GetInsuranceClaimByIdAsync(id, ct));

    [HttpGet("policies/{policyId:guid}/insurance-claims")]
    public async Task<ActionResult<IEnumerable<MedicalInsuranceClaimSummaryDto>>> GetInsuranceClaimsByPolicy(
        Guid policyId,
        CancellationToken ct)
        => Ok(await _service.GetInsuranceClaimsByPolicyAsync(policyId, ct));

    [HttpGet("expense-claims/{expenseClaimId:guid}/insurance-claims")]
    public async Task<ActionResult<IEnumerable<MedicalInsuranceClaimSummaryDto>>> GetInsuranceClaimsByExpenseClaim(
        Guid expenseClaimId,
        CancellationToken ct)
        => Ok(await _service.GetInsuranceClaimsByExpenseClaimAsync(expenseClaimId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("insurance-claims")]
    public async Task<ActionResult<MedicalInsuranceClaimDto>> CreateInsuranceClaim(
        [FromBody] CreateMedicalInsuranceClaimDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateInsuranceClaimAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetInsuranceClaim), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("insurance-claims/{id:guid}/status")]
    public async Task<ActionResult<MedicalInsuranceClaimDto>> UpdateInsuranceClaimStatus(
        Guid id,
        [FromBody] UpdateMedicalInsuranceClaimStatusDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        return Ok(await _service.UpdateInsuranceClaimStatusAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("insurance-claims/{id:guid}/payment")]
    public async Task<ActionResult<MedicalInsuranceClaimDto>> RecordInsuranceClaimPayment(
        Guid id,
        [FromBody] RecordMedicalInsuranceClaimPaymentDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        return Ok(await _service.RecordInsuranceClaimPaymentAsync(dto, userId, ct));
    }

    // =========================================================================
    // NETWORK FACILITIES
    // =========================================================================

    [HttpGet("providers/{providerId:guid}/network-facilities")]
    public async Task<ActionResult<IEnumerable<MedicalInsuranceProviderFacilityDto>>> GetNetworkFacilities(
        Guid providerId,
        CancellationToken ct)
        => Ok(await _service.GetNetworkFacilitiesByProviderAsync(providerId, ct));

    [HttpGet("providers/{providerId:guid}/facilities/{facilityId:guid}/in-network")]
    public async Task<ActionResult<bool>> IsFacilityInNetwork(Guid providerId, Guid facilityId, CancellationToken ct)
        => Ok(await _service.IsFacilityInNetworkAsync(providerId, facilityId, ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("network-facilities")]
    public async Task<ActionResult<MedicalInsuranceProviderFacilityDto>> AddNetworkFacility(
        [FromBody] AddMedicalInsuranceProviderFacilityDto dto,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddNetworkFacilityAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("network-facilities/{id:guid}")]
    public async Task<ActionResult<MedicalInsuranceProviderFacilityDto>> UpdateNetworkFacility(
        Guid id,
        [FromBody] UpdateMedicalInsuranceProviderFacilityDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateNetworkFacilityAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("network-facilities/{id:guid}")]
    public async Task<IActionResult> RemoveNetworkFacility(Guid id, CancellationToken ct)
    {
        await _service.RemoveNetworkFacilityAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // PROVIDER DOCUMENTS
    // =========================================================================

    [HttpGet("providers/{providerId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<MedicalInsuranceProviderDocumentDto>>> GetProviderDocuments(
        Guid providerId,
        CancellationToken ct)
        => Ok(await _service.GetProviderDocumentsAsync(providerId, ct));

    /// <summary>Records a provider document. Metadata only — files arrive through the upload route.</summary>
    /// <remarks>
    /// A caller-supplied path would let anyone point a document row at arbitrary bytes on disk,
    /// including another tenant's. This was the third instance of that defect in the medical
    /// module: <see cref="MedicalExpenseDocument"/> and <c>EmployeeMedicalExamDocument</c> were
    /// both fixed for it, and provider documents were missed. Files now arrive through the upload
    /// endpoint below, which routes them past the malware scanner into private storage.
    /// </remarks>
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("provider-documents")]
    public async Task<ActionResult<MedicalInsuranceProviderDocumentDto>> AddProviderDocument(
        [FromBody] CreateMedicalInsuranceProviderDocumentDto dto,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        if (!string.IsNullOrWhiteSpace(dto.FilePath) ||
            dto.FileUploadRecordId.HasValue ||
            dto.DocumentRecordId.HasValue ||
            dto.DocumentVersionId.HasValue)
        {
            return BadRequest(new
            {
                message = "File locations cannot be supplied directly. " +
                          "Use POST provider-documents/upload to attach a file."
            });
        }

        var created = await _service.AddProviderDocumentAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    /// <summary>Attaches a file to a provider through the controlled boundary.</summary>
    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("provider-documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<MedicalInsuranceProviderDocumentDto>> UploadProviderDocument(
        [FromForm] Guid providerId,
        [FromForm] IFormFile file,
        [FromForm] MedicalInsuranceProviderDocumentType documentType,
        [FromForm] string? description = null,
        [FromForm] DateTime? expiryDate = null,
        CancellationToken ct = default)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        // Refuse a provider from another tenant before any bytes are stored.
        var provider = await _db.Set<MedicalInsuranceProvider>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == providerId && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (provider is null) return NotFound("That insurance provider could not be found.");

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = userId,
                ActorName = CurrentUser.UserName,
                Category = ControlledFileUploadCategories.HrMedicalInsuranceProviderDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Medical insurance provider document",
                    SourceEntityType = nameof(MedicalInsuranceProvider),
                    SourceRecordId = providerId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = documentType.ToString(),
                    ChangeSummary = description
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            // The gate's own {code, message} contract — a refused file is not a server fault.
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var created = await _service.AddProviderDocumentAsync(
                new CreateMedicalInsuranceProviderDocumentDto
                {
                    ProviderId = providerId,
                    FileName = document.OriginalFileName,
                    FilePath = string.Empty,
                    FileUploadRecordId = document.FileUploadRecordId,
                    DocumentRecordId = document.DocumentRecordId,
                    DocumentVersionId = document.DocumentVersionId,
                    DocumentType = documentType,
                    Description = description,
                    ExpiryDate = expiryDate
                },
                tenantId, userId, ct);

            return Ok(created);
        }
        catch
        {
            // Leave no scanned-and-registered document behind pointing at a row never written.
            await _hrDocuments.RollbackAsync(document, tenantId, userId, ct);
            throw;
        }
    }

    /// <summary>Streams a provider document to a caller entitled to see it.</summary>
    [Authorize(Policy = HrPermissions.MedicalReadPolicy)]
    [HttpGet("provider-documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadProviderDocument(Guid id, CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } error) return error;

        var document = await _db.Set<MedicalInsuranceProviderDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
        if (document is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("provider-documents/{id:guid}")]
    public async Task<IActionResult> DeleteProviderDocument(Guid id, CancellationToken ct)
    {
        await _service.DeleteProviderDocumentAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // PREMIUM RECORDS
    // =========================================================================

    [HttpGet("premium-records/{id:guid}")]
    public async Task<ActionResult<MedicalInsurancePremiumRecordDto>> GetPremiumRecord(Guid id, CancellationToken ct)
        => Ok(await _service.GetPremiumRecordByIdAsync(id, ct));

    [HttpGet("providers/{providerId:guid}/premium-records")]
    public async Task<ActionResult<IEnumerable<MedicalInsurancePremiumRecordDto>>> GetPremiumRecordsByProvider(
        Guid providerId,
        CancellationToken ct)
        => Ok(await _service.GetPremiumRecordsByProviderAsync(providerId, ct));

    [HttpGet("premium-records/overdue")]
    public async Task<ActionResult<IEnumerable<MedicalInsurancePremiumRecordSummaryDto>>> GetOverduePremiums(CancellationToken ct)
        => Ok(await _service.GetOverduePremiumsAsync(ct));

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("premium-records")]
    public async Task<ActionResult<MedicalInsurancePremiumRecordDto>> CreatePremiumRecord(
        [FromBody] CreateMedicalInsurancePremiumRecordDto dto,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreatePremiumRecordAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetPremiumRecord), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("premium-records/{id:guid}/payment")]
    public async Task<ActionResult<MedicalInsurancePremiumRecordDto>> RecordPremiumPayment(
        Guid id,
        [FromBody] RecordMedicalInsurancePremiumPaymentDto dto,
        CancellationToken ct)
    {
        dto.PremiumRecordId = id;
        if (TryGetWriteContext(out _, out var userId) is { } writeError) return writeError;
        return Ok(await _service.RecordPremiumPaymentAsync(dto, userId, ct));
    }
}
