using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/consultant-clients")]
[Authorize(Policy = "InternalOnly")]
public class ConsultantClientsController : AttendanceControllerBase
{
    private readonly IConsultantClientService _service;
    private readonly IConsultantClientPortalAuthService _portalAuthService;

    public ConsultantClientsController(
        IConsultantClientService service,
        IConsultantClientPortalAuthService portalAuthService,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _portalAuthService = portalAuthService;
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantClientSummaryDto>>> GetAll(CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<ConsultantClientSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<ConsultantClientDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("code/{clientCode}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<ConsultantClientDto?>> GetByClientCode(
        string clientCode, CancellationToken ct = default)
        => Ok(await _service.GetByClientCodeAsync(clientCode, ct));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantClientSummaryDto>>> GetActiveClients(
        CancellationToken ct = default)
        => Ok(await _service.GetActiveClientsAsync(ct));

    [HttpGet("industry/{industry}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantClientSummaryDto>>> GetByIndustry(
        string industry, CancellationToken ct = default)
        => Ok(await _service.GetByIndustryAsync(industry, ct));

    [HttpGet("{id:guid}/engagements")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<ConsultantClientDto>> GetWithEngagements(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetWithEngagementsAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ConsultantClientDto>> Create(
        [FromBody] CreateConsultantClientDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ConsultantClientDto>> Update(
        Guid id, [FromBody] UpdateConsultantClientDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/engagements")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ClientEngagementDto>> AddEngagement(
        Guid id, [FromBody] CreateClientEngagementDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        dto.ClientId = id;
        return Ok(await _service.AddEngagementAsync(dto, tenantId, employeeId, ct));
    }

    [HttpGet("{id:guid}/engagements/list")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ClientEngagementSummaryDto>>> GetEngagements(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetEngagementsAsync(id, ct));

    [HttpGet("engagements/{engagementId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<ClientEngagementDto>> GetEngagementById(
        Guid engagementId, CancellationToken ct = default)
        => Ok(await _service.GetEngagementByIdAsync(engagementId, ct));

    [HttpPut("engagements/{engagementId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ClientEngagementDto>> UpdateEngagement(
        Guid engagementId, [FromBody] UpdateClientEngagementDto dto, CancellationToken ct = default)
    {
        if (engagementId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateEngagementAsync(dto, employeeId, ct));
    }

    [HttpDelete("engagements/{engagementId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> DeleteEngagement(Guid engagementId, CancellationToken ct = default)
    {
        await _service.DeleteEngagementAsync(engagementId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/portal-accounts")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ConsultantClientPortalAccountSummaryDto>>> GetPortalAccounts(
        Guid id,
        CancellationToken ct = default)
    {
        if (TryGetTenant(out var tenantId) is { } error) return error;
        return Ok(await _portalAuthService.GetPortalAccountsForClientAsync(id, tenantId, ct));
    }

    [HttpPost("{id:guid}/portal-invite")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ConsultantClientPortalAccountSummaryDto>> InvitePortalAccount(
        Guid id,
        [FromBody] ConsultantClientPortalInviteDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        try
        {
            var result = await _portalAuthService.InvitePortalAccountAsync(
                id, dto, tenantId, employeeId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/portal-invite/resend")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<IActionResult> ResendPortalInvite(
        Guid id,
        [FromBody] ConsultantClientPortalResendInviteDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenant(out var tenantId) is { } error) return error;

        try
        {
            await _portalAuthService.ResendPortalInviteAsync(id, dto.Email, tenantId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
