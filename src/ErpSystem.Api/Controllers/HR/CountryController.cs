using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Controller for managing countries
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
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
}

