using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/compliance")]
[Authorize]
public class StaffTravelComplianceController : ControllerBase
{
    private readonly IStaffTravelComplianceService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTravelComplianceController(IStaffTravelComplianceService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private (Guid tenantId, Guid userId)? ResolveContext()
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null || userId is null) return null;
        return (tenantId.Value, userId.Value);
    }

    // =========================================================================
    // TRAVEL DOCUMENTS
    // =========================================================================

    [HttpGet("documents")]
    public async Task<ActionResult<IEnumerable<StaffTravelDocumentDto>>> GetAllDocuments()
        => Ok(await _service.GetAllDocumentsAsync());

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<StaffTravelDocumentDto>> GetDocumentById(Guid id)
        => Ok(await _service.GetDocumentByIdAsync(id));

    [HttpGet("documents/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelDocumentDto>>> GetDocumentsByEmployee(Guid employeeId)
        => Ok(await _service.GetDocumentsByEmployeeAsync(employeeId));

    [HttpGet("documents/expiring")]
    public async Task<ActionResult<IEnumerable<StaffTravelDocumentDto>>> GetExpiringDocuments([FromQuery] int daysAhead = 90)
        => Ok(await _service.GetExpiringDocumentsAsync(daysAhead));

    [HttpPost("documents")]
    public async Task<ActionResult<StaffTravelDocumentDto>> CreateDocument([FromBody] CreateStaffTravelDocumentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateDocumentAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetDocumentById), new { id = created.Id }, created);
    }

    [HttpPut("documents/{id:guid}")]
    public async Task<ActionResult<StaffTravelDocumentDto>> UpdateDocument(Guid id, [FromBody] UpdateStaffTravelDocumentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateDocumentAsync(dto, ctx.Value.userId));
    }

    [HttpPost("documents/{id:guid}/verify")]
    public async Task<IActionResult> VerifyDocument(Guid id)
    {
        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        await _service.VerifyDocumentAsync(new VerifyStaffTravelDocumentDto { DocumentId = id, VerifiedById = userId.Value });
        return Ok(new { message = "Document verified." });
    }

    [HttpDelete("documents/{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        await _service.DeleteDocumentAsync(id);
        return NoContent();
    }

    // =========================================================================
    // VISA REQUIREMENTS
    // =========================================================================

    [HttpGet("visa-requirements")]
    public async Task<ActionResult<StaffTravelVisaRequirementDto?>> GetVisaRequirement(
        [FromQuery] Guid passportCountryId, [FromQuery] Guid destinationCountryId)
        => Ok(await _service.GetVisaRequirementAsync(passportCountryId, destinationCountryId));

    [HttpGet("visa-requirements/destination/{destinationCountryId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelVisaRequirementDto>>> GetVisaRequirementsByDestination(Guid destinationCountryId)
        => Ok(await _service.GetVisaRequirementsByDestinationAsync(destinationCountryId));

    [HttpPost("visa-requirements")]
    public async Task<ActionResult<StaffTravelVisaRequirementDto>> CreateVisaRequirement([FromBody] CreateStaffTravelVisaRequirementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.CreateVisaRequirementAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [HttpPut("visa-requirements/{id:guid}")]
    public async Task<ActionResult<StaffTravelVisaRequirementDto>> UpdateVisaRequirement(Guid id, [FromBody] UpdateStaffTravelVisaRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateVisaRequirementAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("visa-requirements/{id:guid}")]
    public async Task<IActionResult> DeleteVisaRequirement(Guid id)
    {
        await _service.DeleteVisaRequirementAsync(id);
        return NoContent();
    }

    // =========================================================================
    // VISA APPLICATIONS
    // =========================================================================

    [HttpGet("visa-applications")]
    public async Task<ActionResult<IEnumerable<StaffTravelVisaApplicationSummaryDto>>> GetAllVisaApplications()
        => Ok(await _service.GetAllVisaApplicationsAsync());

    [HttpGet("visa-applications/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffTravelVisaApplicationSummaryDto>>> GetVisaApplicationsByStatus(VisaApplicationStatus status)
        => Ok(await _service.GetVisaApplicationsByStatusAsync(status));

    [HttpGet("visa-applications/{id:guid}")]
    public async Task<ActionResult<StaffTravelVisaApplicationDto>> GetVisaApplicationById(Guid id)
        => Ok(await _service.GetVisaApplicationByIdAsync(id));

    [HttpGet("visa-applications/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelVisaApplicationSummaryDto>>> GetVisaApplicationsByRequest(Guid requestId)
        => Ok(await _service.GetVisaApplicationsByRequestAsync(requestId));

    [HttpGet("visa-applications/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelVisaApplicationSummaryDto>>> GetVisaApplicationsByEmployee(Guid employeeId)
        => Ok(await _service.GetVisaApplicationsByEmployeeAsync(employeeId));

    [HttpGet("visa-applications/expiring")]
    public async Task<ActionResult<IEnumerable<StaffTravelVisaApplicationSummaryDto>>> GetExpiringVisas([FromQuery] int daysAhead = 90)
        => Ok(await _service.GetExpiringVisasAsync(daysAhead));

    [HttpPost("visa-applications")]
    public async Task<ActionResult<StaffTravelVisaApplicationDto>> CreateVisaApplication([FromBody] CreateStaffTravelVisaApplicationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateVisaApplicationAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetVisaApplicationById), new { id = created.Id }, created);
    }

    [HttpPut("visa-applications/{id:guid}")]
    public async Task<ActionResult<StaffTravelVisaApplicationDto>> UpdateVisaApplication(Guid id, [FromBody] UpdateStaffTravelVisaApplicationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateVisaApplicationAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("visa-applications/{id:guid}")]
    public async Task<IActionResult> DeleteVisaApplication(Guid id)
    {
        await _service.DeleteVisaApplicationAsync(id);
        return NoContent();
    }

    // =========================================================================
    // RISK ASSESSMENTS
    // =========================================================================

    [HttpGet("risk-assessments/{id:guid}")]
    public async Task<ActionResult<StaffTravelRiskAssessmentDto>> GetRiskAssessmentById(Guid id)
        => Ok(await _service.GetRiskAssessmentByIdAsync(id));

    [HttpGet("risk-assessments/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelRiskAssessmentDto>>> GetRiskAssessmentsByRequest(Guid requestId)
        => Ok(await _service.GetRiskAssessmentsByRequestAsync(requestId));

    [HttpGet("risk-assessments/request/{requestId:guid}/current")]
    public async Task<ActionResult<StaffTravelRiskAssessmentDto?>> GetCurrentRiskAssessment(Guid requestId)
        => Ok(await _service.GetCurrentRiskAssessmentAsync(requestId));

    [HttpGet("risk-assessments/requiring-acknowledgement")]
    public async Task<ActionResult<IEnumerable<StaffTravelRiskAssessmentDto>>> GetAssessmentsRequiringAcknowledgement()
        => Ok(await _service.GetAssessmentsRequiringAcknowledgementAsync());

    [HttpPost("risk-assessments")]
    public async Task<ActionResult<StaffTravelRiskAssessmentDto>> CreateRiskAssessment([FromBody] CreateStaffTravelRiskAssessmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateRiskAssessmentAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetRiskAssessmentById), new { id = created.Id }, created);
    }

    [HttpPut("risk-assessments/{id:guid}")]
    public async Task<ActionResult<StaffTravelRiskAssessmentDto>> UpdateRiskAssessment(Guid id, [FromBody] UpdateStaffTravelRiskAssessmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateRiskAssessmentAsync(dto, ctx.Value.userId));
    }

    [HttpPost("risk-assessments/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeRiskAssessment(Guid id)
    {
        await _service.AcknowledgeRiskAssessmentAsync(new AcknowledgeStaffTravelRiskAssessmentDto { RiskAssessmentId = id });
        return Ok(new { message = "Risk assessment acknowledged." });
    }

    [HttpDelete("risk-assessments/{id:guid}")]
    public async Task<IActionResult> DeleteRiskAssessment(Guid id)
    {
        await _service.DeleteRiskAssessmentAsync(id);
        return NoContent();
    }

    // =========================================================================
    // ALERTS
    // =========================================================================

    [HttpGet("alerts/{id:guid}")]
    public async Task<ActionResult<StaffTravelAlertDto>> GetAlertById(Guid id)
        => Ok(await _service.GetAlertByIdAsync(id));

    [HttpGet("alerts/active")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertSummaryDto>>> GetActiveAlerts()
        => Ok(await _service.GetActiveAlertsAsync());

    [HttpGet("alerts/country/{countryId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertSummaryDto>>> GetAlertsByCountry(Guid countryId)
        => Ok(await _service.GetAlertsByCountryAsync(countryId));

    [HttpGet("alerts/country/{countryId:guid}/current")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertSummaryDto>>> GetCurrentAlertsForCountry(Guid countryId)
        => Ok(await _service.GetCurrentAlertsForCountryAsync(countryId));

    [HttpPost("alerts")]
    public async Task<ActionResult<StaffTravelAlertDto>> CreateAlert([FromBody] CreateStaffTravelAlertDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateAlertAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetAlertById), new { id = created.Id }, created);
    }

    [HttpPut("alerts/{id:guid}")]
    public async Task<ActionResult<StaffTravelAlertDto>> UpdateAlert(Guid id, [FromBody] UpdateStaffTravelAlertDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateAlertAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("alerts/{id:guid}")]
    public async Task<IActionResult> DeleteAlert(Guid id)
    {
        await _service.DeleteAlertAsync(id);
        return NoContent();
    }

    // ---- Alert notifications -----------------------------------------------

    [HttpGet("alert-notifications/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertNotificationDto>>> GetNotificationsByEmployee(Guid employeeId)
        => Ok(await _service.GetNotificationsByEmployeeAsync(employeeId));

    [HttpGet("alert-notifications/employee/{employeeId:guid}/unacknowledged")]
    public async Task<ActionResult<IEnumerable<StaffTravelAlertNotificationDto>>> GetUnacknowledgedNotifications(Guid employeeId)
        => Ok(await _service.GetUnacknowledgedNotificationsAsync(employeeId));

    [HttpPost("alert-notifications")]
    public async Task<ActionResult<StaffTravelAlertNotificationDto>> CreateAlertNotification([FromBody] CreateStaffTravelAlertNotificationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.CreateAlertNotificationAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [HttpPost("alert-notifications/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeNotification(Guid id)
    {
        await _service.AcknowledgeNotificationAsync(new AcknowledgeStaffTravelAlertNotificationDto { NotificationId = id });
        return Ok(new { message = "Notification acknowledged." });
    }

    // =========================================================================
    // INSURANCE POLICIES
    // =========================================================================

    [HttpGet("insurance/{id:guid}")]
    public async Task<ActionResult<StaffTravelInsurancePolicyDto>> GetInsuranceById(Guid id)
        => Ok(await _service.GetInsuranceByIdAsync(id));

    [HttpGet("insurance/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelInsurancePolicyDto>>> GetInsuranceByRequest(Guid requestId)
        => Ok(await _service.GetInsuranceByRequestAsync(requestId));

    [HttpPost("insurance")]
    public async Task<ActionResult<StaffTravelInsurancePolicyDto>> CreateInsurance([FromBody] CreateStaffTravelInsurancePolicyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateInsuranceAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetInsuranceById), new { id = created.Id }, created);
    }

    [HttpPut("insurance/{id:guid}")]
    public async Task<ActionResult<StaffTravelInsurancePolicyDto>> UpdateInsurance(Guid id, [FromBody] UpdateStaffTravelInsurancePolicyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateInsuranceAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("insurance/{id:guid}")]
    public async Task<IActionResult> DeleteInsurance(Guid id)
    {
        await _service.DeleteInsuranceAsync(id);
        return NoContent();
    }

    // =========================================================================
    // HEALTH REQUIREMENTS
    // =========================================================================

    [HttpGet("health-requirements/{id:guid}")]
    public async Task<ActionResult<StaffTravelHealthRequirementDto>> GetHealthRequirementById(Guid id)
        => Ok(await _service.GetHealthRequirementByIdAsync(id));

    [HttpGet("health-requirements/country/{countryId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelHealthRequirementDto>>> GetHealthRequirementsByCountry(Guid countryId)
        => Ok(await _service.GetHealthRequirementsByCountryAsync(countryId));

    [HttpGet("health-requirements/country/{countryId:guid}/mandatory")]
    public async Task<ActionResult<IEnumerable<StaffTravelHealthRequirementDto>>> GetMandatoryHealthRequirements(Guid countryId)
        => Ok(await _service.GetMandatoryHealthRequirementsAsync(countryId));

    [HttpGet("health-requirements/active")]
    public async Task<ActionResult<IEnumerable<StaffTravelHealthRequirementDto>>> GetActiveHealthRequirements()
        => Ok(await _service.GetActiveHealthRequirementsAsync());

    [HttpPost("health-requirements")]
    public async Task<ActionResult<StaffTravelHealthRequirementDto>> CreateHealthRequirement([FromBody] CreateStaffTravelHealthRequirementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateHealthRequirementAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetHealthRequirementById), new { id = created.Id }, created);
    }

    [HttpPut("health-requirements/{id:guid}")]
    public async Task<ActionResult<StaffTravelHealthRequirementDto>> UpdateHealthRequirement(Guid id, [FromBody] UpdateStaffTravelHealthRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateHealthRequirementAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("health-requirements/{id:guid}")]
    public async Task<IActionResult> DeleteHealthRequirement(Guid id)
    {
        await _service.DeleteHealthRequirementAsync(id);
        return NoContent();
    }
}
