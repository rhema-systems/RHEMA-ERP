using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[ApiController]
[Route("api/finance/[controller]")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CurrenciesController> _logger;

    public CurrenciesController(
        ICurrencyService currencyService,
        ICurrentUserProvider currentUserProvider,
        ILogger<CurrenciesController> logger)
    {
        _currencyService = currencyService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get all currencies
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CurrencyDto>>> GetAll()
    {
        _logger.LogInformation("GetAll Currencies called. Current TenantId: {TenantId}, UserId: {UserId}", 
            _currentUserProvider.TenantId, _currentUserProvider.UserId);
        var currencies = await _currencyService.GetAllAsync();
        _logger.LogInformation("GetAll Currencies returned {Count} items", currencies.Count());
        return Ok(currencies);
    }

    /// <summary>
    /// Get active currencies
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<CurrencyDto>>> GetActive()
    {
        var currencies = await _currencyService.GetActiveAsync();
        return Ok(currencies);
    }

    /// <summary>
    /// Get base currency
    /// </summary>
    [HttpGet("base")]
    public async Task<ActionResult<CurrencyDto>> GetBaseCurrency()
    {
        var currency = await _currencyService.GetBaseCurrencyAsync();
        if (currency == null)
        {
            return NotFound("No base currency configured.");
        }
        return Ok(currency);
    }

    /// <summary>
    /// Get currency by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CurrencyDto>> GetById(Guid id)
    {
        var currency = await _currencyService.GetByIdAsync(id);
        if (currency == null)
        {
            return NotFound($"Currency with ID {id} not found.");
        }
        return Ok(currency);
    }

    /// <summary>
    /// Get currency by code
    /// </summary>
    [HttpGet("code/{code}")]
    public async Task<ActionResult<CurrencyDto>> GetByCode(string code)
    {
        var currency = await _currencyService.GetByCodeAsync(code);
        if (currency == null)
        {
            return NotFound($"Currency with code '{code}' not found.");
        }
        return Ok(currency);
    }

    /// <summary>
    /// Create a new currency
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<CurrencyDto>> Create([FromBody] CreateCurrencyDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currency = await _currencyService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = currency.Id }, currency);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update a currency
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<CurrencyDto>> Update(Guid id, [FromBody] UpdateCurrencyDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currency = await _currencyService.UpdateAsync(id, dto);
            return Ok(currency);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete a currency
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _currencyService.DeleteAsync(id);
            if (!result)
            {
                return NotFound($"Currency with ID {id} not found.");
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Set a currency as base currency
    /// </summary>
    [HttpPost("{id:guid}/set-base")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> SetBaseCurrency(Guid id)
    {
        var result = await _currencyService.SetBaseCurrencyAsync(id);
        if (!result)
        {
            return NotFound($"Currency with ID {id} not found.");
        }
        return Ok(new { message = "Currency set as base currency successfully." });
    }

    /// <summary>
    /// Update exchange rate for a currency
    /// </summary>
    [HttpPut("{id:guid}/exchange-rate")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> UpdateExchangeRate(Guid id, [FromBody] UpdateExchangeRateDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _currencyService.UpdateExchangeRateAsync(id, dto.Rate);
            if (!result)
            {
                return NotFound($"Currency with ID {id} not found.");
            }
            return Ok(new { message = "Exchange rate updated successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Convert amount between currencies
    /// </summary>
    [HttpGet("convert")]
    public async Task<ActionResult<decimal>> Convert(
        [FromQuery] decimal amount,
        [FromQuery] string fromCurrency,
        [FromQuery] string toCurrency)
    {
        try
        {
            var convertedAmount = await _currencyService.ConvertAsync(amount, fromCurrency, toCurrency);
            return Ok(new { 
                originalAmount = amount, 
                fromCurrency, 
                toCurrency, 
                convertedAmount 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Check if a code is unique
    /// </summary>
    [HttpGet("check-code/{code}")]
    public async Task<ActionResult<bool>> CheckCodeUnique(string code, [FromQuery] Guid? excludeId = null)
    {
        var isUnique = await _currencyService.IsCodeUniqueAsync(code, excludeId);
        return Ok(new { isUnique });
    }
}
