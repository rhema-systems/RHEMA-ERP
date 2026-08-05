using ErpSystem.Api.Services.HR;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// One-off administrative tooling that adopts pre-boundary HR files into scanned, private
/// storage and the central DMS.
/// </summary>
/// <remarks>
/// <para>Intended sequence: <c>scan</c> to see the scale, <c>run</c> with <c>dryRun</c> to confirm
/// the selection, <c>run</c> for real, then — only once downloads have been verified in the UI —
/// a final <c>run</c> with <c>deleteOriginals</c>.</para>
///
/// <para>SuperAdmin only. The job reads and rewrites file references across every HR document
/// family and can sweep all tenants at once, so it is not something a tenant administrator should
/// be able to trigger.</para>
/// </remarks>
[ApiController]
[Route("api/admin/hr-legacy-files")]
[Authorize(Roles = "SuperAdmin")]
[EnableRateLimiting("SensitivePolicy")]
public class HrLegacyFileMigrationController : ControllerBase
{
    private readonly HrLegacyFileMigrationService _migration;
    private readonly ICurrentUserService _currentUser;

    public HrLegacyFileMigrationController(
        HrLegacyFileMigrationService migration,
        ICurrentUserService currentUser)
    {
        _migration = migration;
        _currentUser = currentUser;
    }

    /// <summary>Reports how many legacy files are still awaiting adoption. Changes nothing.</summary>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(HrLegacyFileMigrationReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<HrLegacyFileMigrationReport>> Scan(
        [FromQuery] Guid? tenantId,
        CancellationToken ct)
        => Ok(await _migration.ScanAsync(tenantId, ct));

    /// <summary>
    /// Runs the adoption. Synchronous on purpose: this is an operator-supervised maintenance
    /// action, and a caller who can see the counts as they land is better served than one
    /// polling a job id.
    /// </summary>
    /// <param name="deleteOriginals">
    /// Removes the original public-tree files. Leave false on the first run — the originals are
    /// already unreachable via the blocked static paths, so deleting them is a separate,
    /// deliberate step once downloads have been verified.
    /// </param>
    [HttpPost("run")]
    [ProducesResponseType(typeof(HrLegacyFileMigrationRunResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<HrLegacyFileMigrationRunResult>> Run(
        [FromQuery] Guid? tenantId,
        [FromQuery] bool dryRun = true,
        [FromQuery] bool deleteOriginals = false,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var actorUserId))
            return BadRequest(new { message = "User context could not be resolved." });

        var result = await _migration.RunAsync(tenantId, dryRun, deleteOriginals, actorUserId, ct);
        return Ok(result);
    }
}
