using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/contractors")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheContractorController : SheApiControllerBase
{
    private readonly ISheContractorService _service;

    public SheContractorController(ISheContractorService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheContractorDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("code/{contractorCode}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheContractorDto?>> GetByCode(string contractorCode)
        => Ok(await _service.GetByCodeAsync(contractorCode));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorSummaryDto>>> GetByStatus(SheContractorStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("expiring-prequalification")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorSummaryDto>>> GetExpiringPreQualification([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringPreQualificationAsync(daysAhead));

    [HttpGet("with-open-non-compliances")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorSummaryDto>>> GetWithOpenNonCompliances()
        => Ok(await _service.GetWithOpenNonCompliancesAsync());

    [HttpPost]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorDto>> Create([FromBody] CreateSheContractorDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorDto>> Update(Guid id, [FromBody] UpdateSheContractorDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/prequalify")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<IActionResult> PreQualify(Guid id, [FromBody] PreQualifySheContractorDto dto)
    {
        dto.ContractorId = id;
        await _service.PreQualifyAsync(dto, UserId);
        return Ok(new { message = "Contractor pre-qualification recorded." });
    }

    // ── Inductions ──
    [HttpPost("{id:guid}/inductions")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorInductionDto>> AddInduction(Guid id, [FromBody] CreateSheContractorInductionDto dto)
    {
        dto.ContractorId = id;
        return Ok(await _service.AddInductionAsync(dto, TenantId, UserId));
    }

    [HttpPut("inductions/{inductionId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorInductionDto>> UpdateInduction(Guid inductionId, [FromBody] UpdateSheContractorInductionDto dto)
    {
        if (inductionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInductionAsync(dto, UserId));
    }

    [HttpDelete("inductions/{inductionId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteInduction(Guid inductionId)
    {
        await _service.DeleteInductionAsync(inductionId);
        return NoContent();
    }

    // ── SHE inspections ──
    [HttpGet("{id:guid}/inspections")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorInspectionDto>>> GetInspections(Guid id)
        => Ok(await _service.GetInspectionsAsync(id));

    [HttpPost("{id:guid}/inspections")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorInspectionDto>> AddInspection(Guid id, [FromBody] CreateSheContractorInspectionDto dto)
    {
        dto.ContractorId = id;
        return Ok(await _service.AddInspectionAsync(dto, TenantId, UserId));
    }

    [HttpPut("inspections/{inspectionId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorInspectionDto>> UpdateInspection(Guid inspectionId, [FromBody] UpdateSheContractorInspectionDto dto)
    {
        if (inspectionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInspectionAsync(dto, UserId));
    }

    // ── Non-compliances ──
    [HttpGet("{id:guid}/non-compliances")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorNonComplianceDto>>> GetNonCompliances(Guid id)
        => Ok(await _service.GetNonCompliancesAsync(id));

    [HttpGet("non-compliances/open")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorNonComplianceDto>>> GetOpenNonCompliances()
        => Ok(await _service.GetOpenNonCompliancesAsync());

    [HttpGet("non-compliances/overdue")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorNonComplianceDto>>> GetOverdueNonCompliances()
        => Ok(await _service.GetOverdueNonCompliancesAsync());

    [HttpPost("{id:guid}/non-compliances")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorNonComplianceDto>> AddNonCompliance(Guid id, [FromBody] CreateSheContractorNonComplianceDto dto)
    {
        dto.ContractorId = id;
        return Ok(await _service.AddNonComplianceAsync(dto, TenantId, UserId));
    }

    [HttpPut("non-compliances/{nonComplianceId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorNonComplianceDto>> UpdateNonCompliance(Guid nonComplianceId, [FromBody] UpdateSheContractorNonComplianceDto dto)
    {
        if (nonComplianceId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateNonComplianceAsync(dto, UserId));
    }

    [HttpPost("non-compliances/{nonComplianceId:guid}/close")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<IActionResult> CloseNonCompliance(Guid nonComplianceId, [FromBody] CloseSheContractorNonComplianceDto dto)
    {
        dto.NonComplianceId = nonComplianceId;
        await _service.CloseNonComplianceAsync(dto, UserId);
        return Ok(new { message = "Non-compliance notice closed." });
    }

    // ── Documents ──
    [HttpGet("{id:guid}/documents")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorDocumentDto>>> GetDocuments(Guid id)
        => Ok(await _service.GetDocumentsAsync(id));

    [HttpGet("documents/expiring")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorDocumentDto>>> GetExpiringDocuments([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringDocumentsAsync(daysAhead));

    [HttpGet("documents/unverified")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheContractorDocumentDto>>> GetUnverifiedDocuments()
        => Ok(await _service.GetUnverifiedDocumentsAsync());

    [HttpPost("{id:guid}/documents")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheContractorDocumentDto>> AddDocument(Guid id, [FromBody] CreateSheContractorDocumentDto dto)
    {
        dto.ContractorId = id;
        return Ok(await _service.AddDocumentAsync(dto, TenantId, UserId));
    }

    [HttpPost("documents/{documentId:guid}/verify")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<IActionResult> VerifyDocument(Guid documentId, [FromBody] VerifySheContractorDocumentDto dto)
    {
        dto.DocumentId = documentId;
        await _service.VerifyDocumentAsync(dto, UserId);
        return Ok(new { message = "Document verified." });
    }

    [HttpDelete("documents/{documentId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}
