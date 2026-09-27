using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.QuantitySurvey;

[ApiController]
[Authorize]
[Route("api/quantity-survey/catalogues")]
public sealed class QuantitySurveyCataloguesController(IProjectSetupService projectSetupService) : ControllerBase
{
    [HttpGet("search"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> Search([FromQuery] string search, [FromQuery] int take = 8) => ExecuteAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length is < 2 or > 100)
            return Ok(Array.Empty<ProjectCatalogEntryDto>());
        var values = await projectSetupService.GetQuantitySurveyCatalogEntriesAsync(null, search.Trim(), null, null, true);
        return Ok(values.Take(Math.Clamp(take, 1, 50)));
    });

    [HttpGet("{id:guid}"), Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> Get(Guid id) => ExecuteAsync(async () =>
    {
        var values = await projectSetupService.GetQuantitySurveyCatalogEntriesAsync(null, null, null, null, true);
        var value = values.SingleOrDefault(value => value.Id == id);
        return value is null ? NotFound() : Ok(value);
    });

    [HttpGet("lookups/units-of-measure")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> UnitOfMeasureOptions()
        => ExecuteAsync(async () => Ok(await projectSetupService.GetQuantitySurveyUnitOfMeasureOptionsAsync()));

    [HttpGet]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.Read)]
    public Task<IActionResult> List(
        [FromQuery] string? catalogType,
        [FromQuery] string? search,
        [FromQuery] string? standardCode,
        [FromQuery] DateTime? effectiveAt,
        [FromQuery] bool includeInactive = true)
        => ExecuteAsync(async () => Ok(await projectSetupService.GetQuantitySurveyCatalogEntriesAsync(
            catalogType,
            search,
            standardCode,
            effectiveAt,
            includeInactive)));

    [HttpPost]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Create([FromBody] CreateProjectCatalogEntryDto request)
        => ExecuteAsync(async () => Ok(await projectSetupService.CreateQuantitySurveyCatalogEntryAsync(request)));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Update(Guid id, [FromBody] CreateProjectCatalogEntryDto request)
        => ExecuteAsync(async () => Ok(await projectSetupService.UpdateQuantitySurveyCatalogEntryAsync(id, request)));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = QuantitySurveyAccessControlRegistry.Manage)]
    public Task<IActionResult> Delete(Guid id)
        => ExecuteAsync(async () =>
        {
            await projectSetupService.DeleteQuantitySurveyCatalogEntryAsync(id);
            return NoContent();
        });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(Problem(
                StatusCodes.Status400BadRequest,
                "Quantity-survey catalogue request rejected",
                exception.Message));
        }
    }

    private ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://tdc.gov.gh/problems/quantity-survey-catalogue-{status}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = $"QS_CATALOGUE_{status}",
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}
