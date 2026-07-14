using ErpSystem.Core.Interfaces.Estate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Estate;

[ApiController]
[Route("api/estate/procedures")]
[Authorize]
public sealed class EstateProceduresController : ControllerBase
{
    private readonly IEstateProcedureCatalogService _procedureCatalog;

    public EstateProceduresController(IEstateProcedureCatalogService procedureCatalog)
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
}
