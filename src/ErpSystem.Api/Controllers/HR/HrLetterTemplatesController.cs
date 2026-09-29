using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// HR's letter and email templates (round 4, lane N): every email the HR modules send, and the
/// tenant's own wording for any of them.
/// </summary>
/// <remarks>
/// <para>The screen the ported solution had (<c>RecruitmentEmailTemplates.razor</c>) and this one lacked:
/// the platform's email designer lists stored rows only, shows no event, and is closed to the HR role.
/// <c>ITemplatedEmailService.RenderInline</c> was written for exactly this preview and had no caller.</para>
///
/// <para>Gated on the HR <b>Company</b> family: reading on Read; saving, resetting and sending a test on
/// <b>Write</b> — which the HR role holds. Admin would be a door HR cannot open, the company-schedule
/// finding again.</para>
///
/// <para>Scope: the <c>EmailTemplate</c> + <c>IEmailEventCatalog</c> system only. In-app notifications
/// and the platform designer's database-field templates are separate systems.</para>
/// </remarks>
[ApiController]
[HrLetterTemplateBusinessRules]
[Route("api/hr/letter-templates")]
[Authorize(Policy = "InternalOnly")]
public class HrLetterTemplatesController : ControllerBase
{
    private readonly IHrLetterTemplateService _service;

    public HrLetterTemplatesController(IHrLetterTemplateService service) => _service = service;

    /// <summary>Every HR email, and whether the tenant has its own wording for it.</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IReadOnlyList<HrLetterTemplateSummaryDto>>> List(CancellationToken ct = default)
        => Ok(await _service.ListAsync(ct));

    [HttpGet("{module}/{eventKey}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<HrLetterTemplateDto>> Get(string module, string eventKey, CancellationToken ct = default)
        => Ok(await _service.GetAsync(module, eventKey, ct));

    [HttpPut("{module}/{eventKey}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<HrLetterTemplateDto>> Save(
        string module, string eventKey, [FromBody] SaveHrLetterTemplateDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.SaveAsync(module, eventKey, dto, ct));
    }

    /// <summary>Renders unsaved wording with sample values, and says what a save would be refused for.</summary>
    [HttpPost("{module}/{eventKey}/preview")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<HrLetterTemplatePreviewDto>> Preview(
        string module, string eventKey, [FromBody] PreviewHrLetterTemplateDto dto, CancellationToken ct = default)
        => Ok(await _service.PreviewAsync(module, eventKey, dto, ct));

    /// <summary>Emails a sample to the signed-in officer themselves.</summary>
    [HttpPost("{module}/{eventKey}/test-send")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<HrLetterTemplateTestSendResultDto>> TestSend(
        string module, string eventKey, [FromBody] PreviewHrLetterTemplateDto dto, CancellationToken ct = default)
        => Ok(await _service.TestSendAsync(module, eventKey, dto, ct));

    [HttpPost("{module}/{eventKey}/reset")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<HrLetterTemplateDto>> Reset(string module, string eventKey, CancellationToken ct = default)
        => Ok(await _service.ResetAsync(module, eventKey, ct));
}
