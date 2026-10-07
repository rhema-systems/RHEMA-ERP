using ErpSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/config")]
public sealed class PublicApplicationConfigurationController : ControllerBase
{
    private readonly IApplicationEnvironmentService _applicationEnvironment;

    public PublicApplicationConfigurationController(
        IApplicationEnvironmentService applicationEnvironment)
    {
        _applicationEnvironment = applicationEnvironment;
    }

    [HttpGet("environment")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<PublicApplicationEnvironment>(StatusCodes.Status200OK)]
    public ActionResult<PublicApplicationEnvironment> GetEnvironment() =>
        Ok(_applicationEnvironment.GetPublicDescriptor());
}
