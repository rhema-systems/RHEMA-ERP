using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IAssetTypeService _assetTypeService;
    private readonly IAssetTypeAttributeService _assetTypeAttributeService;
    private readonly ICompanyAssetService _companyAssetService;
    private readonly IAssetAttributeValueService _attributeValueService;
    private readonly IAssetImageService _assetImageService;
    private readonly IAssetAssignmentService _assignmentService;
    private readonly IAssetMaintenanceService _maintenanceService;
    private readonly IAssetAttachmentService _attachmentService;
    private readonly IAssetRequisitionService _requisitionService;
    private readonly IAssetTransferService _transferService;

    public AssetsController(
        IAssetTypeService assetTypeService,
        IAssetTypeAttributeService assetTypeAttributeService,
        ICompanyAssetService companyAssetService,
        IAssetAttributeValueService attributeValueService,
        IAssetImageService assetImageService,
        IAssetAssignmentService assignmentService,
        IAssetMaintenanceService maintenanceService,
        IAssetAttachmentService attachmentService,
        IAssetRequisitionService requisitionService,
        IAssetTransferService transferService)
    {
        _assetTypeService = assetTypeService;
        _assetTypeAttributeService = assetTypeAttributeService;
        _companyAssetService = companyAssetService;
        _attributeValueService = attributeValueService;
        _assetImageService = assetImageService;
        _assignmentService = assignmentService;
        _maintenanceService = maintenanceService;
        _attachmentService = attachmentService;
        _requisitionService = requisitionService;
        _transferService = transferService;
    }

    #region Asset Types

    /// <summary>List asset types for a tenant.</summary>
    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<AssetTypeSummaryDto>>> GetAssetTypes()
    {
        var result = await _assetTypeService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Get asset types paged.</summary>
    [HttpGet("types/paged")]
    public async Task<ActionResult<PagedResult<AssetTypeSummaryDto>>> GetAssetTypesPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null)
    {
        var result = await _assetTypeService.GetPagedAsync(pageNumber, pageSize, searchTerm);
        return Ok(result);
    }

    /// <summary>Get asset type by id.</summary>
    [HttpGet("types/{id:guid}")]
    public async Task<ActionResult<AssetTypeDto>> GetAssetType(Guid id)
    {
        var result = await _assetTypeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Get asset type with attributes.</summary>
    [HttpGet("types/{id:guid}/with-attributes")]
    public async Task<ActionResult<AssetTypeDetailDto>> GetAssetTypeWithAttributes(Guid id)
    {
        var result = await _assetTypeService.GetWithAttributesAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create asset type.</summary>
    [HttpPost("types")]
    public async Task<ActionResult<AssetTypeDto>> CreateAssetType([FromBody] CreateAssetTypeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _assetTypeService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAssetType), new { id = created.Id }, created);
    }

    /// <summary>Update asset type.</summary>
    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<AssetTypeDto>> UpdateAssetType(
        Guid id,
        [FromBody] UpdateAssetTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _assetTypeService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete asset type.</summary>
    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteAssetType(Guid id)
    {
        await _assetTypeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Asset Type Attributes

    /// <summary>List attributes for an asset type.</summary>
    [HttpGet("types/{assetTypeId:guid}/attributes")]
    public async Task<ActionResult<IEnumerable<AssetTypeAttributeDto>>> GetAssetTypeAttributes(Guid assetTypeId)
    {
        var result = await _assetTypeAttributeService.GetByAssetTypeIdAsync(assetTypeId);
        return Ok(result);
    }

    /// <summary>Get attribute by id.</summary>
    [HttpGet("attributes/{id:guid}")]
    public async Task<ActionResult<AssetTypeAttributeDto>> GetAssetTypeAttribute(Guid id)
    {
        var result = await _assetTypeAttributeService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create asset type attribute.</summary>
    [HttpPost("types/{assetTypeId:guid}/attributes")]
    public async Task<ActionResult<AssetTypeAttributeDto>> CreateAssetTypeAttribute(
        Guid assetTypeId,
        [FromBody] CreateAssetTypeAttributeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.AssetTypeId = assetTypeId;
        var created = await _assetTypeAttributeService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAssetTypeAttribute), new { id = created.Id }, created);
    }

    /// <summary>Update asset type attribute.</summary>
    [HttpPut("attributes/{id:guid}")]
    public async Task<ActionResult<AssetTypeAttributeDto>> UpdateAssetTypeAttribute(
        Guid id,
        [FromBody] UpdateAssetTypeAttributeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _assetTypeAttributeService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete asset type attribute.</summary>
    [HttpDelete("attributes/{id:guid}")]
    public async Task<IActionResult> DeleteAssetTypeAttribute(Guid id)
    {
        await _assetTypeAttributeService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Company Assets

    /// <summary>List all assets for a tenant.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssets()
    {
        var result = await _companyAssetService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged assets.</summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<CompanyAssetSummaryDto>>> GetAssetsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] CompanyAssetStatus? status = null,
        [FromQuery] Guid? assetTypeId = null)
    {
        var result = await _companyAssetService.GetPagedAsync(pageNumber, pageSize, searchTerm, status, assetTypeId);
        return Ok(result);
    }

    /// <summary>Get asset by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyAssetDto>> GetAsset(Guid id)
    {
        var result = await _companyAssetService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Get asset with details.</summary>
    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<CompanyAssetDetailDto>> GetAssetDetails(Guid id)
    {
        var result = await _companyAssetService.GetWithDetailsAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Assets by status.</summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssetsByStatus(CompanyAssetStatus status)
    {
        var result = await _companyAssetService.GetByStatusAsync(status);
        return Ok(result);
    }

    /// <summary>Assets by type.</summary>
    [HttpGet("types/{assetTypeId:guid}/assets")]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssetsByType(Guid assetTypeId)
    {
        var result = await _companyAssetService.GetByAssetTypeAsync(assetTypeId);
        return Ok(result);
    }

    /// <summary>Available assets for assignment.</summary>
    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAvailableAssets()
    {
        var result = await _companyAssetService.GetAvailableForAssignmentAsync();
        return Ok(result);
    }

    /// <summary>Assets by employee.</summary>
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetAssetsByEmployee(Guid employeeId)
    {
        var result = await _companyAssetService.GetByEmployeeAsync(employeeId);
        return Ok(result);
    }

    /// <summary>Assets due for maintenance.</summary>
    [HttpGet("due-maintenance")]
    public async Task<ActionResult<IEnumerable<CompanyAssetSummaryDto>>> GetDueForMaintenance([FromQuery] int daysAhead = 30)
    {
        var result = await _companyAssetService.GetDueForMaintenanceAsync(daysAhead);
        return Ok(result);
    }

    /// <summary>Create asset.</summary>
    [HttpPost]
    public async Task<ActionResult<CompanyAssetDto>> CreateAsset([FromBody] CreateCompanyAssetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _companyAssetService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAsset), new { id = created.Id }, created);
    }

    /// <summary>Update asset.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyAssetDto>> UpdateAsset(
        Guid id,
        [FromBody] UpdateCompanyAssetDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _companyAssetService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete asset.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsset(Guid id)
    {
        await _companyAssetService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Dispose asset.</summary>
    [HttpPost("{id:guid}/dispose")]
    public async Task<IActionResult> DisposeAsset(
        Guid id,
        [FromBody] DisposeAssetDto dto)
    {
        if (id != dto.AssetId) return BadRequest("ID mismatch");
        await _companyAssetService.DisposeAssetAsync(dto);
        return Ok(new { message = "Asset disposed" });
    }

    #endregion

    #region Asset Attribute Values

    /// <summary>Get attribute values for asset.</summary>
    [HttpGet("{assetId:guid}/attribute-values")]
    public async Task<ActionResult<IEnumerable<AssetAttributeValueDto>>> GetAssetAttributeValues(Guid assetId)
    {
        var result = await _attributeValueService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Get attribute value by id.</summary>
    [HttpGet("attribute-values/{id:guid}")]
    public async Task<ActionResult<AssetAttributeValueDto>> GetAssetAttributeValue(Guid id)
    {
        var result = await _attributeValueService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create attribute value.</summary>
    [HttpPost("{assetId:guid}/attribute-values")]
    public async Task<ActionResult<AssetAttributeValueDto>> CreateAttributeValue(
        Guid assetId,
        [FromBody] CreateAssetAttributeValueDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _attributeValueService.CreateAsync(assetId, dto);
        return CreatedAtAction(nameof(GetAssetAttributeValue), new { id = created.Id }, created);
    }

    /// <summary>Update attribute value.</summary>
    [HttpPut("attribute-values/{id:guid}")]
    public async Task<ActionResult<AssetAttributeValueDto>> UpdateAttributeValue(
        Guid id,
        [FromBody] UpdateAssetAttributeValueDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _attributeValueService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete attribute value.</summary>
    [HttpDelete("attribute-values/{id:guid}")]
    public async Task<IActionResult> DeleteAttributeValue(Guid id)
    {
        await _attributeValueService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Assignments

    /// <summary>Get assignment by id.</summary>
    [HttpGet("assignments/{id:guid}")]
    public async Task<ActionResult<AssetAssignmentDto>> GetAssignment(Guid id)
    {
        var result = await _assignmentService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List assignments for tenant.</summary>
    [HttpGet("assignments")]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetAssignments()
    {
        var result = await _assignmentService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged assignments.</summary>
    [HttpGet("assignments/paged")]
    public async Task<ActionResult<PagedResult<AssetAssignmentSummaryDto>>> GetAssignmentsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] AssignmentStatus? status = null)
    {
        var result = await _assignmentService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Assignments by asset.</summary>
    [HttpGet("assignments/asset/{assetId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetAssignmentsByAsset(Guid assetId)
    {
        var result = await _assignmentService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Current active assignment for asset.</summary>
    [HttpGet("assignments/asset/{assetId:guid}/current")]
    public async Task<ActionResult<AssetAssignmentDto>> GetCurrentAssignmentByAsset(Guid assetId)
    {
        var result = await _assignmentService.GetActiveAssignmentForAssetAsync(assetId);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Assignments by employee.</summary>
    [HttpGet("assignments/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetAssignmentsByEmployee(Guid employeeId)
    {
        var result = await _assignmentService.GetByEmployeeIdAsync(employeeId);
        return Ok(result);
    }

    /// <summary>Active assignments for employee.</summary>
    [HttpGet("assignments/employee/{employeeId:guid}/active")]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetActiveAssignments(Guid employeeId)
    {
        var result = await _assignmentService.GetActiveAssignmentsForEmployeeAsync(employeeId);
        return Ok(result);
    }

    /// <summary>Overdue assignments.</summary>
    [HttpGet("assignments/overdue")]
    public async Task<ActionResult<IEnumerable<AssetAssignmentSummaryDto>>> GetOverdueAssignments()
    {
        var result = await _assignmentService.GetOverdueAssignmentsAsync();
        return Ok(result);
    }

    /// <summary>Create assignment.</summary>
    [HttpPost("assignments")]
    public async Task<ActionResult<AssetAssignmentDto>> CreateAssignment([FromBody] CreateAssetAssignmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _assignmentService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAssignment), new { id = created.Id }, created);
    }

    /// <summary>Update assignment.</summary>
    [HttpPut("assignments/{id:guid}")]
    public async Task<ActionResult<AssetAssignmentDto>> UpdateAssignment(
        Guid id,
        [FromBody] UpdateAssetAssignmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _assignmentService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete assignment.</summary>
    [HttpDelete("assignments/{id:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid id)
    {
        await _assignmentService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Employee acknowledges assignment.</summary>
    [HttpPost("assignments/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAssignment(
        Guid id,
        [FromBody] AcknowledgeAssignmentDto dto)
    {
        dto.AssignmentId = id;
        await _assignmentService.AcknowledgeAssignmentAsync(dto);
        return Ok(new { message = "Assignment acknowledged" });
    }

    /// <summary>Return assigned asset.</summary>
    [HttpPost("assignments/{id:guid}/return")]
    public async Task<IActionResult> ReturnAsset(
        Guid id,
        [FromBody] ReturnAssetDto dto)
    {
        dto.AssignmentId = id;
        await _assignmentService.ReturnAssetAsync(dto);
        return Ok(new { message = "Asset returned" });
    }

    #endregion

    #region Maintenance

    /// <summary>Get maintenance by id.</summary>
    [HttpGet("maintenance/{id:guid}")]
    public async Task<ActionResult<AssetMaintenanceDto>> GetMaintenance(Guid id)
    {
        var result = await _maintenanceService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List maintenance records.</summary>
    [HttpGet("maintenance")]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceSummaryDto>>> GetMaintenanceList()
    {
        var result = await _maintenanceService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged maintenance records.</summary>
    [HttpGet("maintenance/paged")]
    public async Task<ActionResult<PagedResult<AssetMaintenanceSummaryDto>>> GetMaintenancePaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] MaintenanceStatus? status = null)
    {
        var result = await _maintenanceService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Maintenance by asset.</summary>
    [HttpGet("maintenance/asset/{assetId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceSummaryDto>>> GetMaintenanceByAsset(Guid assetId)
    {
        var result = await _maintenanceService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Scheduled maintenance in range.</summary>
    [HttpGet("maintenance/schedule")]
    public async Task<ActionResult<IEnumerable<AssetMaintenanceSummaryDto>>> GetScheduledMaintenance(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var result = await _maintenanceService.GetScheduledMaintenanceAsync(from, to);
        return Ok(result);
    }

    /// <summary>Create maintenance.</summary>
    [HttpPost("maintenance")]
    public async Task<ActionResult<AssetMaintenanceDto>> CreateMaintenance([FromBody] CreateAssetMaintenanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _maintenanceService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetMaintenance), new { id = created.Id }, created);
    }

    /// <summary>Update maintenance.</summary>
    [HttpPut("maintenance/{id:guid}")]
    public async Task<ActionResult<AssetMaintenanceDto>> UpdateMaintenance(
        Guid id,
        [FromBody] UpdateAssetMaintenanceDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _maintenanceService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Complete maintenance.</summary>
    [HttpPost("maintenance/{id:guid}/complete")]
    public async Task<IActionResult> CompleteMaintenance(
        Guid id,
        [FromBody] string? completionNotes = null)
    {
        await _maintenanceService.CompleteMaintenanceAsync(id, completionNotes);
        return Ok(new { message = "Maintenance completed" });
    }

    /// <summary>Delete maintenance.</summary>
    [HttpDelete("maintenance/{id:guid}")]
    public async Task<IActionResult> DeleteMaintenance(Guid id)
    {
        await _maintenanceService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Asset Images

    /// <summary>Get image by id.</summary>
    [HttpGet("images/{id:guid}")]
    public async Task<ActionResult<AssetImageDto>> GetImage(Guid id)
    {
        var result = await _assetImageService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List images for asset.</summary>
    [HttpGet("{assetId:guid}/images")]
    public async Task<ActionResult<IEnumerable<AssetImageDto>>> GetImages(Guid assetId)
    {
        var result = await _assetImageService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Add image record to asset.</summary>
    [HttpPost("{assetId:guid}/images")]
    public async Task<ActionResult<AssetImageDto>> AddImage(
        Guid assetId,
        [FromBody] CreateAssetImageDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.AssetId = assetId;
        var created = await _assetImageService.CreateAsync(assetId, dto);
        return CreatedAtAction(nameof(GetImage), new { id = created.Id }, created);
    }

    /// <summary>Delete image.</summary>
    [HttpDelete("images/{id:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id)
    {
        await _assetImageService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Attachments

    /// <summary>Get attachment by id.</summary>
    [HttpGet("attachments/{id:guid}")]
    public async Task<ActionResult<AssetAttachmentDto>> GetAttachment(Guid id)
    {
        var result = await _attachmentService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List attachments for asset.</summary>
    [HttpGet("{assetId:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<AssetAttachmentDto>>> GetAttachments(Guid assetId)
    {
        var result = await _attachmentService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Add attachment to asset.</summary>
    [HttpPost("{assetId:guid}/attachments")]
    public async Task<ActionResult<AssetAttachmentDto>> AddAttachment(
        Guid assetId,
        [FromBody] CreateAssetAttachmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.AssetId = assetId;
        var created = await _attachmentService.CreateAsync(assetId, dto);
        return CreatedAtAction(nameof(GetAttachment), new { id = created.Id }, created);
    }

    /// <summary>Delete attachment.</summary>
    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id)
    {
        await _attachmentService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Requisitions

    /// <summary>Get requisition by id.</summary>
    [HttpGet("requisitions/{id:guid}")]
    public async Task<ActionResult<AssetRequisitionDto>> GetRequisition(Guid id)
    {
        var result = await _requisitionService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List requisitions.</summary>
    [HttpGet("requisitions")]
    public async Task<ActionResult<IEnumerable<AssetRequisitionSummaryDto>>> GetRequisitions()
    {
        var result = await _requisitionService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged requisitions.</summary>
    [HttpGet("requisitions/paged")]
    public async Task<ActionResult<PagedResult<AssetRequisitionSummaryDto>>> GetRequisitionsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] AssetRequisitionStatus? status = null)
    {
        var result = await _requisitionService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Requisitions by requester.</summary>
    [HttpGet("requisitions/requested-by/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetRequisitionSummaryDto>>> GetRequisitionsByRequester(Guid employeeId)
    {
        var result = await _requisitionService.GetByRequestedByIdAsync(employeeId);
        return Ok(result);
    }

    /// <summary>Pending approvals.</summary>
    [HttpGet("requisitions/pending-approvals")]
    public async Task<ActionResult<IEnumerable<AssetRequisitionSummaryDto>>> GetPendingRequisitionApprovals()
    {
        var result = await _requisitionService.GetPendingApprovalsAsync();
        return Ok(result);
    }

    /// <summary>Create requisition.</summary>
    [HttpPost("requisitions")]
    public async Task<ActionResult<AssetRequisitionDto>> CreateRequisition([FromBody] CreateAssetRequisitionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _requisitionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetRequisition), new { id = created.Id }, created);
    }

    /// <summary>Update requisition.</summary>
    [HttpPut("requisitions/{id:guid}")]
    public async Task<ActionResult<AssetRequisitionDto>> UpdateRequisition(
        Guid id,
        [FromBody] UpdateAssetRequisitionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _requisitionService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete requisition.</summary>
    [HttpDelete("requisitions/{id:guid}")]
    public async Task<IActionResult> DeleteRequisition(Guid id)
    {
        await _requisitionService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Approve requisition.</summary>
    [HttpPost("requisitions/{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequisition(
        Guid id,
        [FromBody] ApproveAssetRequisitionDto dto)
    {
        await _requisitionService.ApproveAsync(id, dto);
        return Ok(new { message = "Requisition approved" });
    }

    /// <summary>Reject requisition.</summary>
    [HttpPost("requisitions/{id:guid}/reject")]
    public async Task<IActionResult> RejectRequisition(
        Guid id,
        [FromBody] RejectAssetRequisitionDto dto)
    {
        await _requisitionService.RejectAsync(id, dto);
        return Ok(new { message = "Requisition rejected" });
    }

    /// <summary>Fulfill requisition.</summary>
    [HttpPost("requisitions/{id:guid}/fulfill")]
    public async Task<IActionResult> FulfillRequisition(
        Guid id,
        [FromBody] FulfillAssetRequisitionDto dto)
    {
        await _requisitionService.FulfillAsync(id, dto);
        return Ok(new { message = "Requisition fulfilled" });
    }

    #endregion

    #region Transfers

    /// <summary>Get transfer by id.</summary>
    [HttpGet("transfers/{id:guid}")]
    public async Task<ActionResult<AssetTransferDto>> GetTransfer(Guid id)
    {
        var result = await _transferService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>List transfers.</summary>
    [HttpGet("transfers")]
    public async Task<ActionResult<IEnumerable<AssetTransferSummaryDto>>> GetTransfers()
    {
        var result = await _transferService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>Paged transfers.</summary>
    [HttpGet("transfers/paged")]
    public async Task<ActionResult<PagedResult<AssetTransferSummaryDto>>> GetTransfersPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] HRAssetTransferStatus? status = null)
    {
        var result = await _transferService.GetPagedAsync(pageNumber, pageSize, searchTerm, status);
        return Ok(result);
    }

    /// <summary>Transfers by asset.</summary>
    [HttpGet("transfers/asset/{assetId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetTransferSummaryDto>>> GetTransfersByAsset(Guid assetId)
    {
        var result = await _transferService.GetByAssetIdAsync(assetId);
        return Ok(result);
    }

    /// <summary>Pending transfers.</summary>
    [HttpGet("transfers/pending")]
    public async Task<ActionResult<IEnumerable<AssetTransferSummaryDto>>> GetPendingTransfers()
    {
        var result = await _transferService.GetPendingTransfersAsync();
        return Ok(result);
    }

    /// <summary>Create transfer.</summary>
    [HttpPost("transfers")]
    public async Task<ActionResult<AssetTransferDto>> CreateTransfer([FromBody] CreateAssetTransferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _transferService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetTransfer), new { id = created.Id }, created);
    }

    /// <summary>Update transfer.</summary>
    [HttpPut("transfers/{id:guid}")]
    public async Task<ActionResult<AssetTransferDto>> UpdateTransfer(
        Guid id,
        [FromBody] UpdateAssetTransferDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _transferService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>Delete transfer.</summary>
    [HttpDelete("transfers/{id:guid}")]
    public async Task<IActionResult> DeleteTransfer(Guid id)
    {
        await _transferService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Approve transfer.</summary>
    [HttpPost("transfers/{id:guid}/approve")]
    public async Task<IActionResult> ApproveTransfer(Guid id)
    {
        await _transferService.ApproveAsync(id);
        return Ok(new { message = "Transfer approved" });
    }

    /// <summary>Complete transfer.</summary>
    [HttpPost("transfers/{id:guid}/complete")]
    public async Task<IActionResult> CompleteTransfer(Guid id)
    {
        await _transferService.CompleteAsync(id);
        return Ok(new { message = "Transfer completed" });
    }

    /// <summary>Reject transfer.</summary>
    [HttpPost("transfers/{id:guid}/reject")]
    public async Task<IActionResult> RejectTransfer(Guid id)
    {
        await _transferService.RejectAsync(id);
        return Ok(new { message = "Transfer rejected" });
    }

    #endregion
}

