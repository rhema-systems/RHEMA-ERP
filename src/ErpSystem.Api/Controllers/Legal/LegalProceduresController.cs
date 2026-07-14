using ErpSystem.Core.Interfaces.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Legal;

[ApiController]
[Route("api/legal/procedures")]
[Authorize]
public sealed class LegalProceduresController : ControllerBase
{
    private readonly ILegalProcedureCatalogService _procedureCatalog;

    public LegalProceduresController(ILegalProcedureCatalogService procedureCatalog)
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
                message = $"Legal procedure workspace '{entityType}' was not found."
            });
        }

        return Ok(new
        {
            success = true,
            data = workspace
        });
    }
}
