using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/warehouses")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<WarehousesController> _logger;

    public WarehousesController(
        IWarehouseRepository warehouseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<WarehousesController> logger)
    {
        _warehouseRepository = warehouseRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets all warehouses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WarehouseDto>>> GetAll()
    {
        try
        {
            var warehouses = await _warehouseRepository.GetAllAsync();
            var dtos = warehouses.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouses");
            return StatusCode(500, "An error occurred while retrieving warehouses");
        }
    }

    /// <summary>
    /// Gets active warehouses
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<WarehouseDto>>> GetActive()
    {
        try
        {
            var warehouses = await _warehouseRepository.GetActiveWarehousesAsync();
            var dtos = warehouses.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active warehouses");
            return StatusCode(500, "An error occurred while retrieving warehouses");
        }
    }

    /// <summary>
    /// Gets a warehouse by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WarehouseDto>> GetById(Guid id)
    {
        try
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(id);
            if (warehouse == null)
                return NotFound($"Warehouse with ID {id} not found");

            return Ok(MapToDto(warehouse));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the warehouse");
        }
    }

    /// <summary>
    /// Creates a new warehouse
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WarehouseDto>> Create([FromBody] CreateWarehouseDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context is required");

            // Check for duplicate code
            var existing = await _warehouseRepository.GetByCodeAsync(dto.Code);
            if (existing != null)
                return BadRequest($"Warehouse with code '{dto.Code}' already exists");

            var warehouse = new Warehouse
            {
                Name = dto.Name,
                Code = dto.Code,
                Description = dto.Description,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                ZipCode = dto.ZipCode,
                Country = dto.Country,
                WarehouseType = dto.WarehouseType,
                ContactPerson = dto.ContactPerson,
                Phone = dto.Phone,
                Email = dto.Email,
                IsDefault = dto.IsDefault,
                IsActive = true,
                TenantId = tenantId,
                CreatedById = _currentUserProvider.UserId
            };

            await _warehouseRepository.AddAsync(warehouse);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created warehouse {Code} for tenant {TenantId}", warehouse.Code, tenantId);
            return CreatedAtAction(nameof(GetById), new { id = warehouse.Id }, MapToDto(warehouse));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating warehouse");
            return StatusCode(500, "An error occurred while creating the warehouse");
        }
    }

    /// <summary>
    /// Updates a warehouse
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WarehouseDto>> Update(Guid id, [FromBody] UpdateWarehouseDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var warehouse = await _warehouseRepository.GetByIdAsync(id);
            if (warehouse == null)
                return NotFound($"Warehouse with ID {id} not found");

            // Check for duplicate code (if changed)
            if (warehouse.Code != dto.Code)
            {
                var existing = await _warehouseRepository.GetByCodeAsync(dto.Code);
                if (existing != null)
                    return BadRequest($"Warehouse with code '{dto.Code}' already exists");
            }

            warehouse.Name = dto.Name;
            warehouse.Code = dto.Code;
            warehouse.Description = dto.Description;
            warehouse.Address = dto.Address;
            warehouse.City = dto.City;
            warehouse.State = dto.State;
            warehouse.ZipCode = dto.ZipCode;
            warehouse.Country = dto.Country;
            warehouse.WarehouseType = dto.WarehouseType;
            warehouse.ContactPerson = dto.ContactPerson;
            warehouse.Phone = dto.Phone;
            warehouse.Email = dto.Email;
            warehouse.IsDefault = dto.IsDefault;
            warehouse.IsActive = dto.IsActive;
            warehouse.LastModifiedById = _currentUserProvider.UserId;
            warehouse.UpdatedAt = DateTime.UtcNow;

            await _warehouseRepository.UpdateAsync(warehouse);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated warehouse {Id}", id);
            return Ok(MapToDto(warehouse));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse {Id}", id);
            return StatusCode(500, "An error occurred while updating the warehouse");
        }
    }

    /// <summary>
    /// Deletes a warehouse (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(id);
            if (warehouse == null)
                return NotFound($"Warehouse with ID {id} not found");

            warehouse.IsDeleted = true;
            warehouse.DeletedAt = DateTime.UtcNow;
            warehouse.DeletedBy = _currentUserProvider.UserId.ToString();
            await _warehouseRepository.UpdateAsync(warehouse);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted warehouse {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting warehouse {Id}", id);
            return StatusCode(500, "An error occurred while deleting the warehouse");
        }
    }

    private static WarehouseDto MapToDto(Warehouse entity)
    {
        return new WarehouseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            Address = entity.Address,
            City = entity.City,
            State = entity.State,
            ZipCode = entity.ZipCode,
            Country = entity.Country,
            IsActive = entity.IsActive,
            IsDefault = entity.IsDefault,
            WarehouseType = entity.WarehouseType,
            ContactPerson = entity.ContactPerson,
            Phone = entity.Phone,
            Email = entity.Email
        };
    }
}

