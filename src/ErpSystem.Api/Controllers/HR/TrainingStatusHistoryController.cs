using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-status-history")]
[Authorize]
public class TrainingStatusHistoryController : ControllerBase
{
    private static readonly HashSet<string> AllowedEntityTypes =
        new(StringComparer.OrdinalIgnoreCase) { "TrainingNomination", "TrainingSchedule", "TrainingCompletion" };

    private readonly ITrainingStatusHistoryService _service;

    public TrainingStatusHistoryController(ITrainingStatusHistoryService service)
    {
        _service = service;
    }

    /// <summary>Status-transition timeline for a single training entity (oldest change first).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingStatusHistoryDto>>> Get(
        [FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entityType) || !AllowedEntityTypes.Contains(entityType))
            return BadRequest($"entityType must be one of: {string.Join(", ", AllowedEntityTypes)}.");
        if (entityId == Guid.Empty)
            return BadRequest("entityId is required.");

        return Ok(await _service.GetForEntityAsync(entityType, entityId, ct));
    }
}
