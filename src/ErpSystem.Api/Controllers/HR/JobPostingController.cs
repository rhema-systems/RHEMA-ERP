using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/job-postings")]
[Authorize]
public class JobPostingController : ControllerBase
{
    private readonly IJobPostingService _service;
    private readonly ICurrentUserService _currentUser;

    public JobPostingController(IJobPostingService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobPostingDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("vacancy/{vacancyId:guid}")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetByVacancy(Guid vacancyId)
        => Ok(await _service.GetByVacancyIdAsync(vacancyId));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetActive()
        => Ok(await _service.GetActivePostingsAsync());

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetByStatus(JobPostingStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("channel/{channel}")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetByChannel(JobPostingChannel channel)
        => Ok(await _service.GetByChannelAsync(channel));

    [HttpGet("expired-active")]
    public async Task<ActionResult<IEnumerable<JobPostingSummaryDto>>> GetExpiredActive()
        => Ok(await _service.GetExpiredActivePostingsAsync());

    [HttpGet("external/{externalPostingId}")]
    public async Task<ActionResult<JobPostingDto?>> GetByExternalId(string externalPostingId)
        => Ok(await _service.GetByExternalPostingIdAsync(externalPostingId));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<JobPostingDto>> Create([FromBody] CreateJobPostingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobPostingDto>> Update(Guid id, [FromBody] UpdateJobPostingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/expire")]
    public async Task<IActionResult> Expire(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ExpireAsync(id, employeeId.Value);
        return Ok(new { message = "Posting expired." });
    }

    /// <summary>Records that the posting was actually published (optionally on a specific date).</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<JobPostingDto>> Publish(Guid id, [FromBody] PublishJobPostingDto? dto)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        try
        {
            return Ok(await _service.PublishAsync(id, dto?.ActualPublishDate, employeeId.Value));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // =========================================================================
    // ATTACHMENTS
    // =========================================================================

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<JobPostingAttachmentDto>>> GetAttachments(Guid id)
        => Ok(await _service.GetAttachmentsAsync(id));

    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult<JobPostingAttachmentDto>> AddAttachment(Guid id, [FromBody] CreateJobPostingAttachmentDto dto)
    {
        if (id != dto.JobPostingId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.AddAttachmentAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        var ok = await _service.DeleteAttachmentAsync(attachmentId);
        return ok ? NoContent() : NotFound();
    }
}
