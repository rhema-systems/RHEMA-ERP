using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing units of measure
/// </summary>
[ApiController]
[Route("api/inventory/units-of-measure")]
[Authorize]
public class UnitsOfMeasureController : ControllerBase
{
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly IUnitOfMeasureConversionRepository _conversionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<UnitsOfMeasureController> _logger;
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;

    public UnitsOfMeasureController(
        IUnitOfMeasureRepository uomRepository,
        IUnitOfMeasureConversionRepository conversionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<UnitsOfMeasureController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null)
    {
        _uomRepository = uomRepository;
        _conversionRepository = conversionRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
    }

    /// <summary>
    /// Gets all units of measure
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnitOfMeasureDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        try
        {
            var units = activeOnly
                ? await _uomRepository.GetActiveUnitsAsync()
                : await _uomRepository.GetAllAsync();

            var dtos = units.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving units of measure");
            return StatusCode(500, "An error occurred while retrieving units of measure");
        }
    }

    /// <summary>
    /// Gets a unit of measure by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UnitOfMeasureDto>> GetById(Guid id)
    {
        try
        {
            var unit = await _uomRepository.GetByIdAsync(id);
            if (unit == null)
                return NotFound($"Unit of measure with ID {id} not found");

            return Ok(MapToDto(unit));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit of measure {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the unit of measure");
        }
    }

    /// <summary>
    /// Gets units of measure by category
    /// </summary>
    [HttpGet("by-category/{category}")]
    public async Task<ActionResult<IEnumerable<UnitOfMeasureDto>>> GetByCategory(string category)
    {
        try
        {
            var units = await _uomRepository.GetByCategoryAsync(category);
            var dtos = units.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving units by category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving units of measure");
        }
    }

    /// <summary>
    /// Gets base units of measure
    /// </summary>
    [HttpGet("base-units")]
    public async Task<ActionResult<IEnumerable<UnitOfMeasureDto>>> GetBaseUnits()
    {
        try
        {
            var units = await _uomRepository.GetBaseUnitsAsync();
            var dtos = units.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving base units");
            return StatusCode(500, "An error occurred while retrieving base units");
        }
    }

    /// <summary>
    /// Creates a new unit of measure
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureDto>> Create([FromBody] CreateUnitOfMeasureDto dto)
    {
        try
        {
            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                return Unauthorized("Tenant context is required");
            }

            // Check for duplicate code
            var existing = await _uomRepository.GetByCodeAsync(dto.Code);
            if (existing != null)
                return BadRequest($"Unit of measure with code '{dto.Code}' already exists");

            var unit = new UnitOfMeasure
            {
                Code = dto.Code,
                Name = dto.Name,
                Symbol = dto.Symbol,
                Category = dto.Category ?? "Quantity",
                IsBaseUnit = dto.IsBaseUnit,
                IsActive = true,
                SortOrder = dto.SortOrder,
                TenantId = tenantId,
                CreatedById = _currentUserProvider.UserId
            };

            await _uomRepository.AddAsync(unit);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created unit of measure {Code} for tenant {TenantId}", unit.Code, tenantId);
            return CreatedAtAction(nameof(GetById), new { id = unit.Id }, MapToDto(unit));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating unit of measure");
            return StatusCode(500, "An error occurred while creating the unit of measure");
        }
    }

    /// <summary>
    /// Updates a unit of measure
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UnitOfMeasureDto>> Update(Guid id, [FromBody] UpdateUnitOfMeasureDto dto)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "UnitOfMeasure.Update");
            if (protection is not null) return protection;
            var unit = await _uomRepository.GetByIdAsync(id);
            if (unit == null)
                return NotFound($"Unit of measure with ID {id} not found");

            unit.Name = dto.Name;
            unit.Symbol = dto.Symbol;
            unit.Category = dto.Category ?? unit.Category;
            unit.IsBaseUnit = dto.IsBaseUnit;
            unit.IsActive = dto.IsActive;
            unit.SortOrder = dto.SortOrder;

            await _uomRepository.UpdateAsync(unit);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated unit of measure {Id}", id);
            return Ok(MapToDto(unit));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating unit of measure {Id}", id);
            return StatusCode(500, "An error occurred while updating the unit of measure");
        }
    }

    /// <summary>
    /// Deletes a unit of measure
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "UnitOfMeasure.Delete");
            if (protection is not null) return protection;
            var unit = await _uomRepository.GetByIdAsync(id);
            if (unit == null)
                return NotFound($"Unit of measure with ID {id} not found");

            await _uomRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted unit of measure {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting unit of measure {Id}", id);
            return StatusCode(500, "An error occurred while deleting the unit of measure");
        }
    }

    /// <summary>
    /// Gets conversions for a unit of measure
    /// </summary>
    [HttpGet("{id}/conversions")]
    public async Task<ActionResult<IEnumerable<UnitOfMeasureConversionDto>>> GetConversions(Guid id)
    {
        try
        {
            var conversions = await _conversionRepository.GetConversionsFromUnitAsync(id);
            var dtos = conversions.Select(c => new UnitOfMeasureConversionDto
            {
                Id = c.Id,
                FromUnitId = c.FromUnitId,
                FromUnitCode = c.FromUnit?.Code ?? string.Empty,
                ToUnitId = c.ToUnitId,
                ToUnitCode = c.ToUnit?.Code ?? string.Empty,
                ConversionFactor = c.ConversionFactor,
                IsActive = c.IsActive
            }).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversions for unit {Id}", id);
            return StatusCode(500, "An error occurred while retrieving conversions");
        }
    }

    /// <summary>
    /// Creates a unit of measure conversion
    /// </summary>
    [HttpPost("conversions")]
    public async Task<ActionResult<UnitOfMeasureConversionDto>> CreateConversion([FromBody] CreateUnitOfMeasureConversionDto dto)
    {
        try
        {
            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                return Unauthorized("Tenant context is required");
            }

            var fromUnit = await _uomRepository.GetByIdAsync(dto.FromUnitId);
            var toUnit = await _uomRepository.GetByIdAsync(dto.ToUnitId);

            if (fromUnit == null || toUnit == null)
                return BadRequest("Invalid from or to unit ID");

            var conversion = new UnitOfMeasureConversion
            {
                FromUnitId = dto.FromUnitId,
                ToUnitId = dto.ToUnitId,
                ConversionFactor = dto.ConversionFactor,
                IsActive = true,
                TenantId = tenantId,
                CreatedById = _currentUserProvider.UserId
            };

            await _conversionRepository.AddAsync(conversion);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new UnitOfMeasureConversionDto
            {
                Id = conversion.Id,
                FromUnitId = conversion.FromUnitId,
                FromUnitCode = fromUnit.Code,
                ToUnitId = conversion.ToUnitId,
                ToUnitCode = toUnit.Code,
                ConversionFactor = conversion.ConversionFactor,
                IsActive = conversion.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating conversion");
            return StatusCode(500, "An error occurred while creating the conversion");
        }
    }

    /// <summary>
    /// Deletes a unit of measure conversion
    /// </summary>
    [HttpDelete("conversions/{id}")]
    public async Task<ActionResult> DeleteConversion(Guid id)
    {
        try
        {
            await _conversionRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversion {Id}", id);
            return StatusCode(500, "An error occurred while deleting the conversion");
        }
    }

    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid id, string action)
    {
        if (_masterDataChanges is null) return null;
        var decision = await _masterDataChanges.CheckDirectMutationAsync(
            new[] { ProcurementMasterDataResourceType.UnitOfMeasure }, id, action,
            HttpContext.TraceIdentifier, HttpContext.RequestAborted);
        return decision.Allowed ? null : Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Staged unit-of-measure change required",
            Detail = decision.Message,
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
        });
    }

    private static UnitOfMeasureDto MapToDto(UnitOfMeasure unit) => new()
    {
        Id = unit.Id,
        Code = unit.Code,
        Name = unit.Name,
        Symbol = unit.Symbol,
        Category = unit.Category,
        IsBaseUnit = unit.IsBaseUnit,
        IsActive = unit.IsActive,
        SortOrder = unit.SortOrder
    };
}

