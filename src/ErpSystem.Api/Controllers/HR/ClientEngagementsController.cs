using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/client-engagements")]
[Authorize(Policy = "InternalOnly")]
public class ClientEngagementsController : AttendanceControllerBase
{
    private readonly IClientEngagementService _service;

    public ClientEngagementsController(IClientEngagementService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<ClientEngagementSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientEngagementDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("code/{engagementCode}")]
    public async Task<ActionResult<ClientEngagementDto?>> GetByEngagementCode(
        string engagementCode, CancellationToken ct = default)
        => Ok(await _service.GetByEngagementCodeAsync(engagementCode, ct));

    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IEnumerable<ClientEngagementSummaryDto>>> GetByClientId(
        Guid clientId, CancellationToken ct = default)
        => Ok(await _service.GetByClientIdAsync(clientId, ct));

    [HttpGet("consultant/{consultantEmployeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ClientEngagementSummaryDto>>> GetByConsultantId(
        Guid consultantEmployeeId, CancellationToken ct = default)
        => Ok(await _service.GetByConsultantIdAsync(consultantEmployeeId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<ClientEngagementSummaryDto>>> GetByStatus(
        ClientEngagementStatus status, CancellationToken ct = default)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ClientEngagementSummaryDto>>> GetActiveEngagements(
        CancellationToken ct = default)
        => Ok(await _service.GetActiveEngagementsAsync(ct));

    [HttpGet("billing-cycle/{cycle}")]
    public async Task<ActionResult<IEnumerable<ClientEngagementSummaryDto>>> GetByBillingCycle(
        BillingCycle cycle, CancellationToken ct = default)
        => Ok(await _service.GetByBillingCycleAsync(cycle, ct));

    [HttpGet("ending-within")]
    public async Task<ActionResult<IEnumerable<ClientEngagementSummaryDto>>> GetEngagementsEndingWithin(
        [FromQuery] int days = 30, CancellationToken ct = default)
        => Ok(await _service.GetEngagementsEndingWithinAsync(days, ct));

    [HttpPost]
    public async Task<ActionResult<ClientEngagementDto>> Create(
        [FromBody] CreateClientEngagementDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClientEngagementDto>> Update(
        Guid id, [FromBody] UpdateClientEngagementDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ClientEngagementDto>> Activate(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ActivateAsync(id, employeeId, ct));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ClientEngagementDto>> Complete(
        Guid id, [FromBody] CompleteEngagementRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.CompleteAsync(id, request.ActualEndDate, employeeId, ct));
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<ClientEngagementDto>> Suspend(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.SuspendAsync(id, employeeId, ct));
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<ClientEngagementDto>> Resume(Guid id, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.ResumeAsync(id, employeeId, ct));
    }

    [HttpPost("{id:guid}/terminate")]
    public async Task<ActionResult<ClientEngagementDto>> Terminate(
        Guid id, [FromBody] TerminateEngagementRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.TerminateAsync(id, request.Reason, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    public sealed class CompleteEngagementRequest
    {
        public DateOnly ActualEndDate { get; set; }
    }

    public sealed class TerminateEngagementRequest
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}
