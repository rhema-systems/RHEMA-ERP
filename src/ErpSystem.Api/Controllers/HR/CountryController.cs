using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing countries
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class CountryController : ControllerBase
{
    private readonly ICountryService _countryService;
    private readonly ILogger<CountryController> _logger;

    public CountryController(
        ICountryService countryService,
        ILogger<CountryController> logger)
    {
        _countryService = countryService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all active countries
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CountryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var countries = await _countryService.GetActiveCountriesAsync();
            return Ok(countries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving countries");
            return StatusCode(500, "An error occurred while retrieving countries");
        }
    }

    /// <summary>
    /// Retrieves all countries (including inactive) for management screens.
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<CountryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllIncludingInactive()
    {
        try
        {
            var countries = await _countryService.GetAllAsync();
            return Ok(countries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all countries");
            return StatusCode(500, "An error occurred while retrieving countries");
        }
    }

    /// <summary>
    /// Retrieves a country by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var country = await _countryService.GetByIdAsync(id);

            if (country == null)
                return NotFound(new { message = $"Country with ID '{id}' not found." });

            return Ok(country);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving country {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the country");
        }
    }

    /// <summary>
    /// Creates a new country.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CountryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCountryDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _countryService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing country.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCountryDto dto)
    {
        if (id != dto.Id)
            return BadRequest(new { message = "ID mismatch." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _countryService.UpdateAsync(id, dto);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a country.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _countryService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound(new { message = "Country not found." });
    }
}

