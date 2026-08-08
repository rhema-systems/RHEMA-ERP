using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalTemplatesController : ControllerBase
{
    private readonly IAppraisalTemplateService _templateService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AppraisalTemplatesController> _logger;

    public AppraisalTemplatesController(
        IAppraisalTemplateService templateService,
        ICurrentUserService currentUser,
        ILogger<AppraisalTemplatesController> logger)
    {
        _templateService = templateService;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>Get all appraisal templates</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetAllAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal templates");
            return StatusCode(500, "An error occurred while retrieving appraisal templates");
        }
    }

    /// <summary>Get appraisal templates with pagination</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<AppraisalTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged appraisal templates");
            return StatusCode(500, "An error occurred while retrieving appraisal templates");
        }
    }

    /// <summary>Get an appraisal template by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal template {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal template");
        }
    }

    /// <summary>Get appraisal templates by position</summary>
    [HttpGet("by-position/{positionId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPosition(Guid positionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetByPositionIdAsync(positionId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal templates for position {PositionId}", positionId);
            return StatusCode(500, "An error occurred while retrieving appraisal templates");
        }
    }

    /// <summary>Get appraisal templates by organisation unit</summary>
    [HttpGet("by-org-unit/{orgUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrgUnit(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetByOrganizationUnitIdAsync(orgUnitId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal templates for org unit {OrgUnitId}", orgUnitId);
            return StatusCode(500, "An error occurred while retrieving appraisal templates");
        }
    }

    /// <summary>Get appraisal templates by organisation level</summary>
    [HttpGet("by-org-level/{orgLevelId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrgLevel(Guid orgLevelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetByOrganizationLevelIdAsync(orgLevelId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal templates for org level {OrgLevelId}", orgLevelId);
            return StatusCode(500, "An error occurred while retrieving appraisal templates");
        }
    }

    /// <summary>Get all active appraisal templates</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetActiveTemplatesAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active appraisal templates");
            return StatusCode(500, "An error occurred while retrieving active appraisal templates");
        }
    }

    /// <summary>Get summary list with section / item counts and cycle-assignment flag</summary>
    [HttpGet("summaries")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaries(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetSummariesAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal template summaries");
            return StatusCode(500, "An error occurred while retrieving appraisal template summaries");
        }
    }


    [HttpPost]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalTemplateDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal template");
            return StatusCode(500, "An error occurred while creating the appraisal template");
        }
    }

    /// <summary>Update an existing appraisal template</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalTemplateDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // The service keys off the body's Id, so a mismatch would edit a different template.
            if (id != updateDto.Id)
                return BadRequest(new { message = "ID mismatch" });

            var result = await _templateService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal template {Id}", id);
            return StatusCode(500, "An error occurred while updating the appraisal template");
        }
    }

    /// <summary>Delete an appraisal template</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Appraisal template not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the appraisal template");
        }
    }

    /// <summary>Set the active status of an appraisal template</summary>
    [HttpPatch("{id:guid}/active-status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActiveStatus(Guid id, [FromBody] bool isActive, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.SetActiveStatusAsync(id, isActive, cancellationToken);
            if (!result) return NotFound(new { message = "Appraisal template not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active status for appraisal template {Id}", id);
            return StatusCode(500, "An error occurred while updating the appraisal template");
        }
    }

    /// <summary>Copy an appraisal template into a new Level/Unit/Position scope</summary>
    [HttpPost("{sourceTemplateId:guid}/clone")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Clone(Guid sourceTemplateId, [FromBody] CopyAppraisalTemplateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.CloneAsync(sourceTemplateId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying appraisal template {SourceTemplateId}", sourceTemplateId);
            return StatusCode(500, "An error occurred while copying the appraisal template");
        }
    }

    // ── Approval workflow ───────────────────────────────────────────────────
    // These four endpoints are thin pass-throughs to the generic workflow engine. Approval
    // authority comes from the published AppraisalTemplate workflow definition — a role
    // attribute here would silently override that configuration, so there is none. A caller
    // who is not an approver for the current step gets 403 from the service.

    /// <summary>
    /// The acting employee, used for the template's own SubmittedBy / ApprovedBy stamps.
    /// The engine resolves the acting user separately, from the token.
    /// </summary>
    private Guid? GetEmployeeId() => _currentUser.EmployeeId;

    /// <summary>Submit a template for approval, starting its workflow</summary>
    [HttpPost("{id:guid}/submit-for-approval")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitForApproval(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.SubmitForApprovalAsync(id, GetEmployeeId() ?? Guid.Empty, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting template {Id} for approval", id);
            return StatusCode(500, "An error occurred while submitting the template for approval");
        }
    }

    /// <summary>Approve the current workflow step of a pending template</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.ApproveAsync(id, GetEmployeeId() ?? Guid.Empty, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving template {Id}", id);
            return StatusCode(500, "An error occurred while approving the template");
        }
    }

    /// <summary>Reject a pending template at the current workflow step</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectAppraisalTemplateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.RejectAsync(id, GetEmployeeId() ?? Guid.Empty, dto?.Reason, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting template {Id}", id);
            return StatusCode(500, "An error occurred while rejecting the template");
        }
    }

    /// <summary>Recall a still-pending template back to Draft</summary>
    [HttpPost("{id:guid}/recall")]
    [ProducesResponseType(typeof(AppraisalTemplateDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recall(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.RecallAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalling template {Id}", id);
            return StatusCode(500, "An error occurred while recalling the template");
        }
    }

    // ── Sections ──────────────────────────────────────────────────────────

    /// <summary>Add a section to an appraisal template</summary>
    [HttpPost("{templateId:guid}/sections")]
    [ProducesResponseType(typeof(AppraisalTemplateSectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddSection(Guid templateId, [FromBody] CreateAppraisalTemplateSectionDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.AddSectionAsync(templateId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding section to appraisal template {TemplateId}", templateId);
            return StatusCode(500, "An error occurred while adding the section");
        }
    }

    /// <summary>Get sections for an appraisal template</summary>
    [HttpGet("{templateId:guid}/sections")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateSectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSections(Guid templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetSectionsAsync(templateId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sections for appraisal template {TemplateId}", templateId);
            return StatusCode(500, "An error occurred while retrieving sections");
        }
    }

    /// <summary>Update a section in an appraisal template</summary>
    [HttpPut("{templateId:guid}/sections/{sectionId:guid}")]
    [ProducesResponseType(typeof(AppraisalTemplateSectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSection(Guid templateId, Guid sectionId, [FromBody] UpdateAppraisalTemplateSectionDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (sectionId != dto.Id)
                return BadRequest(new { message = "ID mismatch" });

            var result = await _templateService.UpdateSectionAsync(templateId, dto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating section {SectionId} in appraisal template {TemplateId}", sectionId, templateId);
            return StatusCode(500, "An error occurred while updating the section");
        }
    }

    /// <summary>Delete a section from an appraisal template</summary>
    [HttpDelete("{templateId:guid}/sections/{sectionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSection(Guid templateId, Guid sectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.DeleteSectionAsync(templateId, sectionId, cancellationToken);
            if (!result) return NotFound(new { message = "Section not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting section {SectionId} from appraisal template {TemplateId}", sectionId, templateId);
            return StatusCode(500, "An error occurred while deleting the section");
        }
    }

    /// <summary>Reorder sections in an appraisal template</summary>
    [HttpPatch("{templateId:guid}/sections/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderSections(Guid templateId, [FromBody] IEnumerable<Guid> orderedSectionIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.ReorderSectionsAsync(templateId, orderedSectionIds, cancellationToken);
            if (!result) return NotFound(new { message = "Template not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering sections for appraisal template {TemplateId}", templateId);
            return StatusCode(500, "An error occurred while reordering sections");
        }
    }

    // ── Items ─────────────────────────────────────────────────────────────

    /// <summary>Add an item to a template section</summary>
    [HttpPost("sections/{sectionId:guid}/items")]
    [ProducesResponseType(typeof(AppraisalTemplateItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem(Guid sectionId, [FromBody] CreateAppraisalTemplateItemDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.AddItemAsync(sectionId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to section {SectionId}", sectionId);
            return StatusCode(500, "An error occurred while adding the item");
        }
    }

    /// <summary>Get items for a template section</summary>
    [HttpGet("sections/{sectionId:guid}/items")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalTemplateItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(Guid sectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.GetItemsAsync(sectionId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving items for section {SectionId}", sectionId);
            return StatusCode(500, "An error occurred while retrieving items");
        }
    }

    /// <summary>Update an item in a template section</summary>
    [HttpPut("sections/{sectionId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(AppraisalTemplateItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(Guid sectionId, Guid itemId, [FromBody] UpdateAppraisalTemplateItemDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (itemId != dto.Id)
                return BadRequest(new { message = "ID mismatch" });

            var result = await _templateService.UpdateItemAsync(sectionId, dto, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item {ItemId} in section {SectionId}", itemId, sectionId);
            return StatusCode(500, "An error occurred while updating the item");
        }
    }

    /// <summary>Delete an item from a template section</summary>
    [HttpDelete("sections/{sectionId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(Guid sectionId, Guid itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.DeleteItemAsync(sectionId, itemId, cancellationToken);
            if (!result) return NotFound(new { message = "Item not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item {ItemId} from section {SectionId}", itemId, sectionId);
            return StatusCode(500, "An error occurred while deleting the item");
        }
    }

    /// <summary>Reorder items in a template section</summary>
    [HttpPatch("sections/{sectionId:guid}/items/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderItems(Guid sectionId, [FromBody] IEnumerable<Guid> orderedItemIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _templateService.ReorderItemsAsync(sectionId, orderedItemIds, cancellationToken);
            if (!result) return NotFound(new { message = "Section not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering items for section {SectionId}", sectionId);
            return StatusCode(500, "An error occurred while reordering items");
        }
    }

    // ─── Item Grade-Range Endpoints ──────────────────────────────────────────

    /// <summary>Get all grade ranges for a template item</summary>
    [HttpGet("items/{itemId:guid}/grade-ranges")]
    [ProducesResponseType(typeof(IEnumerable<TemplateItemGradeRangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemGradeRanges(Guid itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var ranges = await _templateService.GetItemGradeRangesAsync(itemId, cancellationToken);
            return Ok(ranges);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading grade ranges for item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while loading grade ranges");
        }
    }

    /// <summary>Replace all grade ranges for a template item (replace-all pattern)</summary>
    [HttpPut("items/{itemId:guid}/grade-ranges")]
    [ProducesResponseType(typeof(IEnumerable<TemplateItemGradeRangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItemGradeRanges(Guid itemId, [FromBody] UpsertTemplateItemGradeRangesDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var ranges = await _templateService.UpdateItemGradeRangesAsync(itemId, dto, cancellationToken);
            return Ok(ranges);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating grade ranges for item {ItemId}", itemId);
            return StatusCode(500, "An error occurred while updating grade ranges");
        }
    }

    /// <summary>Get all active grade definitions (for dropdowns in the template editor)</summary>
    [HttpGet("grade-definitions/active")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalGradeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveGradeDefinitions(CancellationToken cancellationToken = default)
    {
        try
        {
            var defs = await _templateService.GetActiveGradeDefinitionsAsync(cancellationToken);
            return Ok(defs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active grade definitions");
            return StatusCode(500, "An error occurred while loading grade definitions");
        }
    }
}
