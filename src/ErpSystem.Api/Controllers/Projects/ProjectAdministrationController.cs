using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Interfaces.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Projects;

[Authorize(Policy = "InternalOnly")]
[ApiController]
[Route("api/projects/admin")]
public class ProjectAdministrationController : ControllerBase
{
    private readonly IProjectSetupService _projectSetupService;
    private readonly IProjectManagementSettingsService _settingsService;

    public ProjectAdministrationController(IProjectSetupService projectSetupService, IProjectManagementSettingsService settingsService)
    {
        _projectSetupService = projectSetupService;
        _settingsService = settingsService;
    }

    [HttpGet("master-data-overview")]
    public async Task<ActionResult<ProjectMasterDataOverviewDto>> GetMasterDataOverview()
        => Ok(await _projectSetupService.GetMasterDataOverviewAsync());

    [HttpGet("catalogs")]
    public async Task<ActionResult<IEnumerable<ProjectCatalogEntryDto>>> GetCatalogEntries([FromQuery] string catalogType)
        => Ok(await _projectSetupService.GetCatalogEntriesAsync(catalogType));

    [HttpPost("catalogs")]
    public async Task<ActionResult<ProjectCatalogEntryDto>> CreateCatalogEntry([FromBody] CreateProjectCatalogEntryDto dto)
        => Ok(await _projectSetupService.CreateCatalogEntryAsync(dto));

    [HttpPut("catalogs/{id:guid}")]
    public async Task<ActionResult<ProjectCatalogEntryDto>> UpdateCatalogEntry(Guid id, [FromBody] CreateProjectCatalogEntryDto dto)
        => Ok(await _projectSetupService.UpdateCatalogEntryAsync(id, dto));

    [HttpDelete("catalogs/{id:guid}")]
    public async Task<IActionResult> DeleteCatalogEntry(Guid id)
    {
        await _projectSetupService.DeleteCatalogEntryAsync(id);
        return NoContent();
    }

    [HttpPost("catalogs/seed-defaults")]
    public async Task<IActionResult> SeedCatalogDefaults([FromQuery] string? catalogType = null)
    {
        await _projectSetupService.SeedCatalogDefaultsAsync(catalogType);
        return NoContent();
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<ProjectTypeDto>>> GetProjectTypes() => Ok(await _projectSetupService.GetProjectTypesAsync());

    [HttpPost("types")]
    public async Task<ActionResult<ProjectTypeDto>> CreateProjectType([FromBody] CreateProjectTypeDto dto) => Ok(await _projectSetupService.CreateProjectTypeAsync(dto));

    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<ProjectTypeDto>> UpdateProjectType(Guid id, [FromBody] CreateProjectTypeDto dto) => Ok(await _projectSetupService.UpdateProjectTypeAsync(id, dto));

    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteProjectType(Guid id)
    {
        await _projectSetupService.DeleteProjectTypeAsync(id);
        return NoContent();
    }

    [HttpGet("priorities")]
    public async Task<ActionResult<IEnumerable<ProjectPriorityDto>>> GetProjectPriorities() => Ok(await _projectSetupService.GetProjectPrioritiesAsync());

    [HttpPost("priorities")]
    public async Task<ActionResult<ProjectPriorityDto>> CreateProjectPriority([FromBody] CreateProjectPriorityDto dto) => Ok(await _projectSetupService.CreateProjectPriorityAsync(dto));

    [HttpPut("priorities/{id:guid}")]
    public async Task<ActionResult<ProjectPriorityDto>> UpdateProjectPriority(Guid id, [FromBody] CreateProjectPriorityDto dto) => Ok(await _projectSetupService.UpdateProjectPriorityAsync(id, dto));

    [HttpDelete("priorities/{id:guid}")]
    public async Task<IActionResult> DeleteProjectPriority(Guid id)
    {
        await _projectSetupService.DeleteProjectPriorityAsync(id);
        return NoContent();
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<ProjectTemplateDto>>> GetProjectTemplates([FromQuery] Guid? projectTypeId = null) => Ok(await _projectSetupService.GetProjectTemplatesAsync(projectTypeId));

    [HttpPost("templates")]
    public async Task<ActionResult<ProjectTemplateDto>> CreateProjectTemplate([FromBody] CreateProjectTemplateDto dto) => Ok(await _projectSetupService.CreateProjectTemplateAsync(dto));

    [HttpPut("templates/{id:guid}")]
    public async Task<ActionResult<ProjectTemplateDto>> UpdateProjectTemplate(Guid id, [FromBody] CreateProjectTemplateDto dto) => Ok(await _projectSetupService.UpdateProjectTemplateAsync(id, dto));

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteProjectTemplate(Guid id)
    {
        await _projectSetupService.DeleteProjectTemplateAsync(id);
        return NoContent();
    }

    [HttpGet("phase-templates")]
    public async Task<ActionResult<IEnumerable<ProjectPhaseTemplateDto>>> GetProjectPhaseTemplates([FromQuery] Guid? projectTypeId = null)
        => Ok(await _projectSetupService.GetProjectPhaseTemplatesAsync(projectTypeId));

    [HttpPost("phase-templates")]
    public async Task<ActionResult<ProjectPhaseTemplateDto>> CreateProjectPhaseTemplate([FromBody] CreateProjectPhaseTemplateDto dto)
        => Ok(await _projectSetupService.CreateProjectPhaseTemplateAsync(dto));

    [HttpPut("phase-templates/{id:guid}")]
    public async Task<ActionResult<ProjectPhaseTemplateDto>> UpdateProjectPhaseTemplate(Guid id, [FromBody] UpdateProjectPhaseTemplateDto dto)
        => Ok(await _projectSetupService.UpdateProjectPhaseTemplateAsync(id, dto));

    [HttpDelete("phase-templates/{id:guid}")]
    public async Task<IActionResult> DeleteProjectPhaseTemplate(Guid id)
    {
        await _projectSetupService.DeleteProjectPhaseTemplateAsync(id);
        return NoContent();
    }

    [HttpGet("stage-gate-rules")]
    public async Task<ActionResult<IEnumerable<ProjectStageGateRuleDto>>> GetProjectStageGateRules([FromQuery] Guid? projectPhaseTemplateId = null)
        => Ok(await _projectSetupService.GetProjectStageGateRulesAsync(projectPhaseTemplateId));

    [HttpPost("stage-gate-rules")]
    public async Task<ActionResult<ProjectStageGateRuleDto>> CreateProjectStageGateRule([FromBody] CreateProjectStageGateRuleDto dto)
        => Ok(await _projectSetupService.CreateProjectStageGateRuleAsync(dto));

    [HttpPut("stage-gate-rules/{id:guid}")]
    public async Task<ActionResult<ProjectStageGateRuleDto>> UpdateProjectStageGateRule(Guid id, [FromBody] UpdateProjectStageGateRuleDto dto)
        => Ok(await _projectSetupService.UpdateProjectStageGateRuleAsync(id, dto));

    [HttpDelete("stage-gate-rules/{id:guid}")]
    public async Task<IActionResult> DeleteProjectStageGateRule(Guid id)
    {
        await _projectSetupService.DeleteProjectStageGateRuleAsync(id);
        return NoContent();
    }

    [HttpGet("portfolios")]
    public async Task<ActionResult<IEnumerable<ProjectPortfolioDto>>> GetPortfolios() => Ok(await _projectSetupService.GetPortfoliosAsync());

    [HttpPost("portfolios")]
    public async Task<ActionResult<ProjectPortfolioDto>> CreatePortfolio([FromBody] CreateProjectPortfolioDto dto) => Ok(await _projectSetupService.CreatePortfolioAsync(dto));

    [HttpPut("portfolios/{id:guid}")]
    public async Task<ActionResult<ProjectPortfolioDto>> UpdatePortfolio(Guid id, [FromBody] CreateProjectPortfolioDto dto) => Ok(await _projectSetupService.UpdatePortfolioAsync(id, dto));

    [HttpDelete("portfolios/{id:guid}")]
    public async Task<IActionResult> DeletePortfolio(Guid id)
    {
        await _projectSetupService.DeletePortfolioAsync(id);
        return NoContent();
    }

    [HttpGet("programs")]
    public async Task<ActionResult<IEnumerable<ProjectProgramDto>>> GetPrograms([FromQuery] Guid? portfolioId = null) => Ok(await _projectSetupService.GetProgramsAsync(portfolioId));

    [HttpPost("programs")]
    public async Task<ActionResult<ProjectProgramDto>> CreateProgram([FromBody] CreateProjectProgramDto dto) => Ok(await _projectSetupService.CreateProgramAsync(dto));

    [HttpPut("programs/{id:guid}")]
    public async Task<ActionResult<ProjectProgramDto>> UpdateProgram(Guid id, [FromBody] CreateProjectProgramDto dto) => Ok(await _projectSetupService.UpdateProgramAsync(id, dto));

    [HttpDelete("programs/{id:guid}")]
    public async Task<IActionResult> DeleteProgram(Guid id)
    {
        await _projectSetupService.DeleteProgramAsync(id);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ActionResult<ProjectManagementSettingsDto>> GetSettings() => Ok(await _settingsService.GetSettingsAsync());

    [HttpPut("settings")]
    public async Task<ActionResult<ProjectManagementSettingsDto>> UpdateSettings([FromBody] UpdateProjectManagementSettingsDto dto) => Ok(await _settingsService.UpdateSettingsAsync(dto));
}
