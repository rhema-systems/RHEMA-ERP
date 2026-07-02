using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Route("api/finance/[controller]")]
[Authorize]
public class PaymentTermsController : ControllerBase
{
    private readonly IPaymentTermService _paymentTermService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PaymentTermsController> _logger;

    public PaymentTermsController(
        IPaymentTermService paymentTermService,
        ICurrentUserProvider currentUserProvider,
        ILogger<PaymentTermsController> logger)
    {
        _paymentTermService = paymentTermService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get all payment terms
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentTermDto>>> GetAll()
    {
        _logger.LogInformation("GetAll PaymentTerms called. Current TenantId: {TenantId}, UserId: {UserId}", 
            _currentUserProvider.TenantId, _currentUserProvider.UserId);
        var paymentTerms = await _paymentTermService.GetAllAsync();
        _logger.LogInformation("GetAll PaymentTerms returned {Count} items", paymentTerms.Count());
        return Ok(paymentTerms);
    }

    /// <summary>
    /// Get active payment terms
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<PaymentTermDto>>> GetActive()
    {
        var paymentTerms = await _paymentTermService.GetActiveAsync();
        return Ok(paymentTerms);
    }

    /// <summary>
    /// Get payment terms by applicable type (Supplier, Customer, Contractor, All)
    /// </summary>
    [HttpGet("by-type/{applicableTo}")]
    [HttpGet("applicable/{applicableTo}")]
    public async Task<ActionResult<IEnumerable<PaymentTermDto>>> GetByApplicableTo(string applicableTo)
    {
        var paymentTerms = await _paymentTermService.GetByApplicableToAsync(applicableTo);
        return Ok(paymentTerms);
    }

    /// <summary>
    /// Get default payment term
    /// </summary>
    [HttpGet("default")]
    public async Task<ActionResult<PaymentTermDto>> GetDefault([FromQuery] string? applicableTo = null)
    {
        var paymentTerm = await _paymentTermService.GetDefaultAsync(applicableTo);
        if (paymentTerm == null)
        {
            return NotFound("No default payment term found.");
        }
        return Ok(paymentTerm);
    }

    /// <summary>
    /// Get payment term by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentTermDto>> GetById(Guid id)
    {
        var paymentTerm = await _paymentTermService.GetByIdAsync(id);
        if (paymentTerm == null)
        {
            return NotFound($"Payment term with ID {id} not found.");
        }
        return Ok(paymentTerm);
    }

    /// <summary>
    /// Get payment term by code
    /// </summary>
    [HttpGet("code/{code}")]
    public async Task<ActionResult<PaymentTermDto>> GetByCode(string code)
    {
        var paymentTerm = await _paymentTermService.GetByCodeAsync(code);
        if (paymentTerm == null)
        {
            return NotFound($"Payment term with code '{code}' not found.");
        }
        return Ok(paymentTerm);
    }

    /// <summary>
    /// Create a new payment term
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PaymentTermDto>> Create([FromBody] CreatePaymentTermDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var paymentTerm = await _paymentTermService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = paymentTerm.Id }, paymentTerm);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update a payment term
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PaymentTermDto>> Update(Guid id, [FromBody] UpdatePaymentTermDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var paymentTerm = await _paymentTermService.UpdateAsync(id, dto);
            return Ok(paymentTerm);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete a payment term
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var result = await _paymentTermService.DeleteAsync(id);
        if (!result)
        {
            return NotFound($"Payment term with ID {id} not found.");
        }
        return NoContent();
    }

    /// <summary>
    /// Set a payment term as default
    /// </summary>
    [HttpPost("{id:guid}/set-default")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> SetDefault(Guid id)
    {
        var result = await _paymentTermService.SetDefaultAsync(id);
        if (!result)
        {
            return NotFound($"Payment term with ID {id} not found.");
        }
        return Ok(new { message = "Payment term set as default successfully." });
    }

    /// <summary>
    /// Check if a code is unique
    /// </summary>
    [HttpGet("check-code/{code}")]
    public async Task<ActionResult<bool>> CheckCodeUnique(string code, [FromQuery] Guid? excludeId = null)
    {
        var isUnique = await _paymentTermService.IsCodeUniqueAsync(code, excludeId);
        return Ok(new { isUnique });
    }
}
