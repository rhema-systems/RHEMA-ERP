using ErpSystem.Core.Interfaces.Estate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/property-management/procedures")]
[Authorize]
public sealed class PropertyManagementProceduresController : ControllerBase
{
    private readonly IPropertyManagementProcedureCatalogService _procedureCatalog;

    public PropertyManagementProceduresController(IPropertyManagementProcedureCatalogService procedureCatalog)
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
                message = $"Property Management procedure workspace '{entityType}' was not found."
            });
        }

        return Ok(new
        {
            success = true,
            data = workspace
        });
    }
}
