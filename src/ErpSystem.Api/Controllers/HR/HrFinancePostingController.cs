using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Finance;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// HR → Finance posting: the account-role mappings, the per-event rules and the posting register
/// (HR finish plan lane 8; <c>docs/HR/integration/HR-FINANCE-POSTING-DESIGN.md</c>).
/// </summary>
/// <remarks>
/// <para><b>Read and write are gated differently, as on the policy settings.</b> HR desks need to
/// SEE whether a claim reached Finance (the register, the per-claim card) — <c>HR.Company.Read</c>,
/// which the HR role's Read+Write grant satisfies. Choosing which GL account HR posts to, enabling
/// an event, retrying a refused posting and reversing a journal are administration's —
/// <c>HR.Company.Admin</c>. A reversal in particular creates a Finance journal; it is not a desk action.</para>
///
/// <para><b>Nothing here writes to Finance directly.</b> Mappings and rules are HR tables; retry and
/// reverse go through <c>IFinancePostingEngine</c>, the boundary the Finance owner published.</para>
/// </remarks>
[ApiController]
[Route("api/hr/finance-posting")]
[HrFinancePostingBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class HrFinancePostingController : ControllerBase
{
    private readonly IHrFinancePostingAdminService _service;

    public HrFinancePostingController(IHrFinancePostingAdminService service) => _service = service;

    // ── Catalogue & settings ─────────────────────────────────────────────────────────────────

    /// <summary>The events HR can post, the roles each uses and what each does — code-backed, no tenant state.</summary>
    [HttpGet("events")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<HrFinancePostingEventDefinition>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<HrFinancePostingEventDefinition>> GetEvents()
        => Ok(HrFinancePostingEventCatalog.Events);

    /// <summary>Mappings, rules and readiness for the current tenant.</summary>
    [HttpGet("settings")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HrFinancePostingSettingsDto>> GetSettings(CancellationToken cancellationToken)
        => Ok(await _service.GetSettingsAsync(cancellationToken));

    /// <summary>Map one account role to a Finance account (chosen through <c>api/hr/finance-accounts</c>).</summary>
    [HttpPut("mappings")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HrFinancePostingSettingsDto>> UpsertMapping(
        [FromBody] UpsertHrFinanceAccountMappingDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpsertMappingAsync(dto, cancellationToken));
    }

    /// <summary>Remove a role's mapping. Refused while an enabled rule still uses the role.</summary>
    [HttpDelete("mappings/{role}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HrFinancePostingSettingsDto>> ClearMapping(HrFinanceAccountRole role, CancellationToken cancellationToken)
        => Ok(await _service.ClearMappingAsync(role, cancellationToken));

    /// <summary>Enable or disable one event. Enabling is refused until every role it uses is mapped.</summary>
    [HttpPut("rules")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HrFinancePostingSettingsDto>> UpsertRule(
        [FromBody] UpsertHrFinancePostingRuleDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.UpsertRuleAsync(dto, cancellationToken));
    }

    // ── Register ─────────────────────────────────────────────────────────────────────────────

    [HttpGet("records/summary")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<HrFinancePostingSummaryDto>> GetSummary(CancellationToken cancellationToken)
        => Ok(await _service.GetSummaryAsync(cancellationToken));

    [HttpGet("records")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(PagedResult<HrFinancePostingRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<HrFinancePostingRecordDto>>> GetRecords(
        [FromQuery] HrFinancePostingRecordQueryDto query, CancellationToken cancellationToken)
        => Ok(await _service.GetRecordsAsync(query, cancellationToken));

    [HttpGet("records/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HrFinancePostingRecordDto>> GetRecord(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.GetRecordAsync(id, cancellationToken));

    /// <summary>Every posting row for one source document — what a claim or advance detail card shows.</summary>
    [HttpGet("records/source/{sourceDocumentId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<HrFinancePostingRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HrFinancePostingRecordDto>>> GetRecordsForSource(Guid sourceDocumentId, CancellationToken cancellationToken)
        => Ok(await _service.GetRecordsForSourceAsync(sourceDocumentId, cancellationToken));

    /// <summary>Post an Unposted or Failed row again, from the source document as it stands now.</summary>
    [HttpPost("records/{id:guid}/retry")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HrFinancePostingRecordDto>> Retry(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.RetryAsync(id, cancellationToken));

    /// <summary>Finance's exact reversal of a Posted row, with a reason. The source document is not changed.</summary>
    /// <summary>Pull Finance's status (and the payment voucher, once paid) onto an AP hand-off row.</summary>
    [HttpPost("records/{id:guid}/refresh")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<HrFinancePostingRecordDto>> Refresh(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.RefreshAsync(id, cancellationToken));

    [HttpPost("records/{id:guid}/reverse")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [ProducesResponseType(typeof(HrFinancePostingRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<HrFinancePostingRecordDto>> Reverse(
        Guid id, [FromBody] ReverseHrFinancePostingDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.ReverseAsync(id, dto, cancellationToken));
    }
}
