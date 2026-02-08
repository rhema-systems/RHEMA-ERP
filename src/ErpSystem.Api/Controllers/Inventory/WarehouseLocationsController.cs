using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/warehouse-locations")]
[Authorize]
public class WarehouseLocationsController : ControllerBase
{
    private readonly IWarehouseLocationRepository _locationRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<WarehouseLocationsController> _logger;

    public WarehouseLocationsController(
        IWarehouseLocationRepository locationRepository,
        IWarehouseRepository warehouseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<WarehouseLocationsController> logger)
    {
        _locationRepository = locationRepository;
        _warehouseRepository = warehouseRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets all warehouse locations, optionally filtered by warehouse
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WarehouseLocationDto>>> GetAll([FromQuery] Guid? warehouseId = null)
    {
        try
        {
            IEnumerable<WarehouseLocation> locations;
            if (warehouseId.HasValue)
                locations = await _locationRepository.GetLocationsByWarehouseAsync(warehouseId.Value);
            else
                locations = await _locationRepository.GetAllAsync();

            var dtos = locations.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse locations");
            return StatusCode(500, "An error occurred while retrieving warehouse locations");
        }
    }

    /// <summary>
    /// Gets a warehouse location by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WarehouseLocationDto>> GetById(Guid id)
    {
        try
        {
            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null)
                return NotFound($"Warehouse location with ID {id} not found");

            return Ok(MapToDto(location));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse location {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the warehouse location");
        }
    }

    /// <summary>
    /// Creates a new warehouse location
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WarehouseLocationDto>> Create([FromBody] CreateWarehouseLocationDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context is required");

            // Verify warehouse exists
            var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId);
            if (warehouse == null)
                return BadRequest($"Warehouse with ID {dto.WarehouseId} not found");

            // Check for duplicate location code
            var existing = await _locationRepository.GetByLocationCodeAsync(dto.LocationCode);
            if (existing != null)
                return BadRequest($"Location with code '{dto.LocationCode}' already exists");

            var location = new WarehouseLocation
            {
                WarehouseId = dto.WarehouseId,
                LocationCode = dto.LocationCode,
                Name = dto.Name,
                Description = dto.Description,
                LocationType = dto.LocationType,
                ParentLocationId = dto.ParentLocationId,
                IsPickingLocation = dto.IsPickingLocation,
                IsReceivingLocation = dto.IsReceivingLocation,
                MaxWeight = dto.MaxWeight,
                MaxVolume = dto.MaxVolume,
                MaxItems = dto.MaxItems,
                IsActive = true,
                TenantId = tenantId,
                CreatedById = _currentUserProvider.UserId
            };

            await _locationRepository.AddAsync(location);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created warehouse location {Code} for tenant {TenantId}", location.LocationCode, tenantId);
            return CreatedAtAction(nameof(GetById), new { id = location.Id }, MapToDto(location));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating warehouse location");
            return StatusCode(500, "An error occurred while creating the warehouse location");
        }
    }

    /// <summary>
    /// Updates a warehouse location
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WarehouseLocationDto>> Update(Guid id, [FromBody] UpdateWarehouseLocationDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null)
                return NotFound($"Warehouse location with ID {id} not found");

            // Check for duplicate code (if changed)
            if (location.LocationCode != dto.LocationCode)
            {
                var existing = await _locationRepository.GetByLocationCodeAsync(dto.LocationCode);
                if (existing != null)
                    return BadRequest($"Location with code '{dto.LocationCode}' already exists");
            }

            location.WarehouseId = dto.WarehouseId;
            location.LocationCode = dto.LocationCode;
            location.Name = dto.Name;
            location.Description = dto.Description;
            location.LocationType = dto.LocationType;
            location.ParentLocationId = dto.ParentLocationId;
            location.IsPickingLocation = dto.IsPickingLocation;
            location.IsReceivingLocation = dto.IsReceivingLocation;
            location.MaxWeight = dto.MaxWeight;
            location.MaxVolume = dto.MaxVolume;
            location.MaxItems = dto.MaxItems;
            location.IsActive = dto.IsActive;
            location.LastModifiedById = _currentUserProvider.UserId;
            location.UpdatedAt = DateTime.UtcNow;

            await _locationRepository.UpdateAsync(location);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated warehouse location {Id}", id);
            return Ok(MapToDto(location));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse location {Id}", id);
            return StatusCode(500, "An error occurred while updating the warehouse location");
        }
    }

    /// <summary>
    /// Deletes a warehouse location (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null)
                return NotFound($"Warehouse location with ID {id} not found");

            location.IsDeleted = true;
            location.DeletedAt = DateTime.UtcNow;
            location.DeletedBy = _currentUserProvider.UserId.ToString();
            await _locationRepository.UpdateAsync(location);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted warehouse location {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting warehouse location {Id}", id);
            return StatusCode(500, "An error occurred while deleting the warehouse location");
        }
    }

    private static WarehouseLocationDto MapToDto(WarehouseLocation entity)
    {
        return new WarehouseLocationDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            LocationCode = entity.LocationCode,
            Name = entity.Name,
            Description = entity.Description,
            LocationType = entity.LocationType,
            ParentLocationId = entity.ParentLocationId,
            IsActive = entity.IsActive,
            IsPickingLocation = entity.IsPickingLocation,
            IsReceivingLocation = entity.IsReceivingLocation,
            MaxWeight = entity.MaxWeight,
            MaxVolume = entity.MaxVolume,
            MaxItems = entity.MaxItems,
            CurrentWeight = entity.CurrentWeight,
            CurrentVolume = entity.CurrentVolume,
            CurrentItemCount = entity.CurrentItemCount
        };
    }
}

