using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/rfqs")]
public class RfqsController : ControllerBase
{
    private readonly IRfqService _rfqService;
    private readonly IRfqInvitationDocumentService _rfqInvitationDocumentService;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IBusinessPartnerUserRepository _businessPartnerUserRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<RfqsController> _logger;

    public RfqsController(
        IRfqService rfqService,
        IRfqInvitationDocumentService rfqInvitationDocumentService,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<RfqsController> logger)
    {
        _rfqService = rfqService;
        _rfqInvitationDocumentService = rfqInvitationDocumentService;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // -----------------------------
    // Internal RFQ management
    // -----------------------------

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PagedResult<RfqDto>>> GetRfqs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        try
        {
            if (pageSize > 100) pageSize = 100;
            var result = await _rfqService.GetRfqsAsync(page, pageSize, search, status);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RFQs");
            return StatusCode(500, "An error occurred while retrieving RFQs");
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<RfqDetailDto>> GetRfq(Guid id)
    {
        try
        {
            var rfq = await _rfqService.GetRfqByIdAsync(id);
            if (rfq == null) return NotFound($"RFQ with ID {id} not found");
            return Ok(rfq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RFQ {RfqId}", id);
            return StatusCode(500, "An error occurred while retrieving the RFQ");
        }
    }

    /// <summary>
    /// Generates an RFQ PDF (for printing / emailing) even before sending it to suppliers.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<IActionResult> GetRfqPdf(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var (content, fileName) = await _rfqInvitationDocumentService.GenerateRfqInvitationPdfAsync(id, cancellationToken);
            return File(content, "application/pdf", fileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RFQ PDF for {RfqId}", id);
            return StatusCode(500, "An error occurred while generating the RFQ PDF");
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<RfqDetailDto>> UpdateRfq(Guid id, [FromBody] UpdateRfqDto dto)
    {
        try
        {
            var updated = await _rfqService.UpdateRfqAsync(id, dto);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RFQ {RfqId}", id);
            return StatusCode(500, "An error occurred while updating the RFQ");
        }
    }

    [HttpPost("{id:guid}/award")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<CreatePurchaseOrdersFromRfqResponseDto>> AwardRfqAndCreatePurchaseOrders(Guid id, [FromBody] CreatePurchaseOrdersFromRfqDto dto)
    {
        try
        {
            var result = await _rfqService.CreatePurchaseOrdersFromAwardAsync(id, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error awarding RFQ {RfqId}", id);
            return StatusCode(500, "An error occurred while awarding the RFQ");
        }
    }

    [HttpPost("{id:guid}/send")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<IActionResult> SendRfq(Guid id, [FromBody] SendRfqDto dto)
    {
        try
        {
            await _rfqService.SendRfqAsync(id, dto);
            return Ok(new { success = true, message = "RFQ sent" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending RFQ {RfqId}", id);
            return StatusCode(500, "An error occurred while sending the RFQ");
        }
    }

    // -----------------------------
    // Supplier portal endpoints
    // -----------------------------

    [HttpGet("my-rfqs")]
    public async Task<ActionResult<List<RfqDto>>> GetMyRfqs()
    {
        try
        {
            if (!_currentUserProvider.IsExternalUser)
            {
                return Forbid();
            }

            var businessPartner = await ResolveCurrentBusinessPartnerAsync();
            if (businessPartner == null)
                return Ok(new List<RfqDto>());

            var list = await _rfqService.GetSupplierRfqsAsync(businessPartner.Id, _currentUserProvider.TenantId);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier RFQs");
            return StatusCode(500, "An error occurred while retrieving RFQs");
        }
    }

    [HttpGet("{id:guid}/my-view")]
    public async Task<ActionResult<RfqDetailDto>> GetMyRfqDetail(Guid id)
    {
        try
        {
            if (!_currentUserProvider.IsExternalUser)
            {
                return Forbid();
            }

            var businessPartner = await ResolveCurrentBusinessPartnerAsync();
            if (businessPartner == null)
                return NotFound();

            var detail = await _rfqService.GetSupplierRfqDetailAsync(id, businessPartner.Id, _currentUserProvider.TenantId);
            if (detail == null) return NotFound();

            return Ok(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier RFQ detail {RfqId}", id);
            return StatusCode(500, "An error occurred while retrieving the RFQ");
        }
    }

    [HttpPost("{id:guid}/quote")]
    public async Task<ActionResult<RfqQuoteDto>> SubmitQuote(Guid id, [FromBody] SubmitRfqQuoteDto dto)
    {
        try
        {
            if (!_currentUserProvider.IsExternalUser)
            {
                return Forbid();
            }

            var businessPartner = await ResolveCurrentBusinessPartnerAsync();
            if (businessPartner == null)
                return NotFound();

            var quote = await _rfqService.SubmitQuoteAsync(
                id,
                businessPartner.Id,
                _currentUserProvider.UserId,
                _currentUserProvider.TenantId,
                dto);

            return Ok(quote);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DbUpdateException ex) when (IsDuplicateRfqQuoteItem(ex))
        {
            throw new ConflictException(
                "We couldn’t save your quote because pricing for one or more RFQ lines already exists. Please refresh the page and try submitting again.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting RFQ quote for {RfqId}", id);
            throw;
        }
    }

    private static bool IsDuplicateRfqQuoteItem(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message ?? string.Empty;
        return message.Contains("IX_RequestForQuotationQuoteItems_TenantId_QuoteId_RfqItemId", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("RequestForQuotationQuoteItems", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Cannot insert duplicate key row", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Core.Entities.Procurement.BusinessPartner?> ResolveCurrentBusinessPartnerAsync()
    {
        // First, try to resolve as main portal owner.
        var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

        // If not found, try sub-user mapping.
        if (businessPartner == null)
        {
            var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
            if (businessPartnerUser != null && businessPartnerUser.IsActive)
            {
                businessPartner = businessPartnerUser.BusinessPartner;
            }
        }

        return businessPartner;
    }
}
