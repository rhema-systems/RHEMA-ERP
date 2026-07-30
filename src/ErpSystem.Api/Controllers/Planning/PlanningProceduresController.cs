using ErpSystem.Core.Interfaces.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Planning;

[Authorize]
[ApiController]
[Route("api/development/planning/procedures")]
public sealed class PlanningProceduresController : ControllerBase
{
    private readonly IPlanningProcedureCatalogService _procedureCatalog;

    public PlanningProceduresController(IPlanningProcedureCatalogService procedureCatalog)
    {
        _procedureCatalog = procedureCatalog;
    }

    [HttpGet]
    public IActionResult GetProcedures()
    {
        return Ok(new
        {
            success = true,
            data = _procedureCatalog.GetProcedures()
        });
    }

    [HttpGet("{entityType}")]
    public IActionResult GetProcedureWorkspace(string entityType)
    {
        var workspace = _procedureCatalog.GetProcedureWorkspace(entityType);

        if (workspace is null)
        {
            return NotFound(new
            {
                success = false,
                message = $"Planning procedure workspace '{entityType}' was not found."
            });
        }

        return Ok(new
        {
            success = true,
            data = workspace
        });
    }
}
