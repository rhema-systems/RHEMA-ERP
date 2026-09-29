using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using Microsoft.EntityFrameworkCore;

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
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;
    private readonly IWarehouseDefaultLocationService _defaultLocations;

    public WarehousesController(
        IWarehouseRepository warehouseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<WarehousesController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null,
        IWarehouseDefaultLocationService? defaultLocations = null)
    {
        _warehouseRepository = warehouseRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
        _defaultLocations = defaultLocations ?? new WarehouseDefaultLocationService(unitOfWork, currentUserProvider);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<WarehouseDto>>> Search(
        [FromQuery] string search, [FromQuery] int take = 8)
    {
        if (_currentUserProvider.TenantId == Guid.Empty) return Unauthorized();
        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length is < 2 or > 100)
            return Ok(Array.Empty<WarehouseDto>());
        var term = search.Trim();
        var warehouses = await _warehouseRepository.GetQueryable(value =>
                value.TenantId == _currentUserProvider.TenantId && !value.IsDeleted &&
                (value.Code.Contains(term) || value.Name.Contains(term)))
            .AsNoTracking().OrderBy(value => value.Code).ThenBy(value => value.Id)
            .Take(Math.Clamp(take, 1, 50)).ToListAsync(HttpContext.RequestAborted);
        return Ok(warehouses.Select(MapToDto).ToList());
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
            var dtos = warehouses.Where(x => !InventoryTransitProtection.IsProtected(x)).Select(MapToDto).ToList();
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
            var dtos = warehouses.Where(x => !InventoryTransitProtection.IsProtected(x)).Select(MapToDto).ToList();
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
            var protection = await GuardDirectMutationAsync(null, "Warehouse.Create");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context is required");

            if (InventoryTransitProtection.IsTransitType(dto.WarehouseType))
                return BadRequest(InventoryTransitProtection.Message);

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
                IsConsignmentWarehouse = dto.IsConsignmentWarehouse,
                ContactPerson = dto.ContactPerson,
                Phone = dto.Phone,
                Email = dto.Email,
                IsDefault = dto.IsDefault,
                IsActive = true,
                TenantId = tenantId,
                CreatedById = _currentUserProvider.UserId
            };

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _warehouseRepository.AddAsync(warehouse);
                await _unitOfWork.SaveChangesAsync(ct);
                await _defaultLocations.GetOrCreateAsync(warehouse.Id, _currentUserProvider.UserId, ct);
            }, HttpContext.RequestAborted);

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
            var protection = await GuardDirectMutationAsync(id, "Warehouse.Update");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var warehouse = await _warehouseRepository.GetByIdAsync(id);
            if (warehouse == null)
                return NotFound($"Warehouse with ID {id} not found");

            if (InventoryTransitProtection.IsProtected(warehouse) || InventoryTransitProtection.IsTransitType(dto.WarehouseType))
                return BadRequest(InventoryTransitProtection.Message);

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
            warehouse.IsConsignmentWarehouse = dto.IsConsignmentWarehouse;
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
            var protection = await GuardDirectMutationAsync(id, "Warehouse.Delete");
            if (protection is not null) return protection;
            var warehouse = await _warehouseRepository.GetByIdAsync(id);
            if (warehouse == null)
                return NotFound($"Warehouse with ID {id} not found");

            if (InventoryTransitProtection.IsProtected(warehouse))
                return BadRequest(InventoryTransitProtection.Message);

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

    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid? id, string action)
    {
        if (_masterDataChanges is null) return null;
        var decision = await _masterDataChanges.CheckDirectMutationAsync(
            new[] { ProcurementMasterDataResourceType.Warehouse }, id, action,
            HttpContext.TraceIdentifier, HttpContext.RequestAborted);
        return decision.Allowed ? null : Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Staged warehouse change required",
            Detail = decision.Message,
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
        });
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
            IsConsignmentWarehouse = entity.IsConsignmentWarehouse,
            ContactPerson = entity.ContactPerson,
            Phone = entity.Phone,
            Email = entity.Email
        };
    }
}

