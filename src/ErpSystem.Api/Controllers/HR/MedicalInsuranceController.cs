using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public MedicalInsuranceController(IMedicalInsuranceService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
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
        await _service.CancelPolicyAsync(dto, ct);
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
        return Ok(await _service.UpdateInsuranceClaimStatusAsync(dto, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("insurance-claims/{id:guid}/payment")]
    public async Task<ActionResult<MedicalInsuranceClaimDto>> RecordInsuranceClaimPayment(
        Guid id,
        [FromBody] RecordMedicalInsuranceClaimPaymentDto dto,
        CancellationToken ct)
    {
        dto.ClaimId = id;
        return Ok(await _service.RecordInsuranceClaimPaymentAsync(dto, ct));
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

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost("provider-documents")]
    public async Task<ActionResult<MedicalInsuranceProviderDocumentDto>> AddProviderDocument(
        [FromBody] CreateMedicalInsuranceProviderDocumentDto dto,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddProviderDocumentAsync(dto, tenantId, userId, ct);
        return Ok(created);
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
    public async Task<ActionResult<IEnumerable<MedicalInsurancePremiumRecordSummaryDto>>> GetPremiumRecordsByProvider(
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
        return Ok(await _service.RecordPremiumPaymentAsync(dto, ct));
    }
}
