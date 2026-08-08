using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Interfaces.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Administration;

[ApiController]
[Route("api/admin/audit-governance")]
[Authorize(Policy = "InternalOnly")]
public sealed class AuditGovernanceController(
    IAuditGovernanceService governance,
    IAuditEventCoverageService coverage) : ControllerBase
{
    [HttpGet("coverage")]
    [Authorize(Policy = "AuditGovernanceRead")]
    public ActionResult<AuditEventCoverageReportDto> GetCoverage() => Ok(coverage.GetReport());

    [HttpGet("records/{storeKey}/{recordId:guid}")]
    [Authorize(Policy = "AuditGovernanceRead")]
    public async Task<IActionResult> Get(
        string storeKey,
        Guid recordId,
        CancellationToken cancellationToken)
        => await ExecuteAsync(() => governance.GetAsync(storeKey, recordId, cancellationToken));

    [HttpPost("records/{storeKey}/{recordId:guid}/legal-hold")]
    [Authorize(Policy = "AuditGovernanceManage")]
    public async Task<IActionResult> PlaceLegalHold(
        string storeKey,
        Guid recordId,
        [FromBody] AuditLifecycleCommandDto request,
        CancellationToken cancellationToken)
        => await ExecuteAsync(() => governance.PlaceLegalHoldAsync(storeKey, recordId, request, cancellationToken));

    [HttpPost("records/{storeKey}/{recordId:guid}/legal-hold/release")]
    [Authorize(Policy = "AuditGovernanceManage")]
    public async Task<IActionResult> ReleaseLegalHold(
        string storeKey,
        Guid recordId,
        [FromBody] AuditLifecycleCommandDto request,
        CancellationToken cancellationToken)
        => await ExecuteAsync(() => governance.ReleaseLegalHoldAsync(storeKey, recordId, request, cancellationToken));

    [HttpPost("records/{storeKey}/{recordId:guid}/archive")]
    [Authorize(Policy = "AuditGovernanceManage")]
    public async Task<IActionResult> Archive(
        string storeKey,
        Guid recordId,
        [FromBody] AuditLifecycleCommandDto request,
        CancellationToken cancellationToken)
        => await ExecuteAsync(() => governance.ArchiveAsync(storeKey, recordId, request, cancellationToken));

    [HttpPost("records/{storeKey}/{recordId:guid}/restore")]
    [Authorize(Policy = "AuditGovernanceManage")]
    public async Task<IActionResult> Restore(
        string storeKey,
        Guid recordId,
        [FromBody] AuditLifecycleCommandDto request,
        CancellationToken cancellationToken)
        => await ExecuteAsync(() => governance.RestoreAsync(storeKey, recordId, request, cancellationToken));

    private static async Task<IActionResult> ExecuteAsync(Func<Task<AuditRecordGovernanceDto>> action)
    {
        try
        {
            return new OkObjectResult(await action());
        }
        catch (AuditGovernanceNotFoundException exception)
        {
            return new NotFoundObjectResult(new { code = "AUDIT_RECORD_NOT_FOUND", detail = exception.Message });
        }
        catch (AuditGovernanceConflictException exception)
        {
            return new ConflictObjectResult(new { code = "AUDIT_GOVERNANCE_CONFLICT", detail = exception.Message });
        }
        catch (AuditGovernanceValidationException exception)
        {
            return new BadRequestObjectResult(new { code = "AUDIT_GOVERNANCE_INVALID", detail = exception.Message });
        }
    }
}
