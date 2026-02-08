using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Inventory;

/// <summary>
/// API controller for managing unit of measure schedules
/// </summary>
[ApiController]
[Route("api/inventory/uom-schedules")]
[Authorize]
public class UnitOfMeasureSchedulesController : ControllerBase
{
    private readonly IUnitOfMeasureScheduleRepository _scheduleRepository;
    private readonly IUnitOfMeasureScheduleDetailRepository _detailRepository;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<UnitOfMeasureSchedulesController> _logger;

    public UnitOfMeasureSchedulesController(
        IUnitOfMeasureScheduleRepository scheduleRepository,
        IUnitOfMeasureScheduleDetailRepository detailRepository,
        IUnitOfMeasureRepository uomRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<UnitOfMeasureSchedulesController> logger)
    {
        _scheduleRepository = scheduleRepository;
        _detailRepository = detailRepository;
        _uomRepository = uomRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets all unit of measure schedules
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnitOfMeasureScheduleDto>>> GetAll([FromQuery] bool activeOnly = false)
    {
        try
        {
            var schedules = activeOnly
                ? await _scheduleRepository.GetActiveSchedulesAsync()
                : await _scheduleRepository.GetAllWithDetailsAsync();

            var dtos = schedules.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving UoM schedules");
            return StatusCode(500, "An error occurred while retrieving UoM schedules");
        }
    }

    /// <summary>
    /// Gets a UoM schedule by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UnitOfMeasureScheduleDto>> GetById(Guid id)
    {
        try
        {
            var schedule = await _scheduleRepository.GetWithDetailsAsync(id);
            if (schedule == null)
                return NotFound($"UoM schedule with ID {id} not found");

            return Ok(MapToDto(schedule));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving UoM schedule {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the UoM schedule");
        }
    }

    /// <summary>
    /// Creates a new UoM schedule
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UnitOfMeasureScheduleDto>> Create([FromBody] CreateUnitOfMeasureScheduleDto dto)
    {
        try
        {
            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
                return Unauthorized("Tenant context is required");

            // Check for duplicate schedule ID
            var existing = await _scheduleRepository.GetByScheduleIdAsync(dto.ScheduleId);
            if (existing != null)
                return BadRequest($"UoM schedule with ID '{dto.ScheduleId}' already exists");

            // Validate base unit exists
            var baseUnit = await _uomRepository.GetByIdAsync(dto.BaseUnitOfMeasureId);
            if (baseUnit == null)
                return BadRequest("Invalid base unit of measure ID");

            var schedule = new UnitOfMeasureSchedule
            {
                ScheduleId = dto.ScheduleId,
                Description = dto.Description,
                BaseUnitOfMeasureId = dto.BaseUnitOfMeasureId,
                QuantityDecimals = dto.QuantityDecimals,
                IsActive = true,
                TenantId = tenantId,
                CreatedById = _currentUserProvider.UserId
            };

            await _scheduleRepository.AddAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            // Add details
            foreach (var detailDto in dto.Details)
            {
                var detail = new UnitOfMeasureScheduleDetail
                {
                    ScheduleId = schedule.Id,
                    UnitOfMeasureId = detailDto.UnitOfMeasureId,
                    BaseQuantity = detailDto.BaseQuantity,
                    SortOrder = detailDto.SortOrder,
                    TenantId = tenantId,
                    CreatedById = _currentUserProvider.UserId
                };
                await _detailRepository.AddAsync(detail);
            }
            await _unitOfWork.SaveChangesAsync();

            // Reload with details
            var created = await _scheduleRepository.GetWithDetailsAsync(schedule.Id);
            _logger.LogInformation("Created UoM schedule {ScheduleId} for tenant {TenantId}", schedule.ScheduleId, tenantId);
            return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, MapToDto(created!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating UoM schedule");
            return StatusCode(500, "An error occurred while creating the UoM schedule");
        }
    }

    /// <summary>
    /// Updates a UoM schedule
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UnitOfMeasureScheduleDto>> Update(Guid id, [FromBody] UpdateUnitOfMeasureScheduleDto dto)
    {
        try
        {
            var schedule = await _scheduleRepository.GetWithDetailsAsync(id);
            if (schedule == null)
                return NotFound($"UoM schedule with ID {id} not found");

            // Validate base unit exists
            var baseUnit = await _uomRepository.GetByIdAsync(dto.BaseUnitOfMeasureId);
            if (baseUnit == null)
                return BadRequest("Invalid base unit of measure ID");

            schedule.Description = dto.Description;
            schedule.BaseUnitOfMeasureId = dto.BaseUnitOfMeasureId;
            schedule.QuantityDecimals = dto.QuantityDecimals;
            schedule.IsActive = dto.IsActive;

            await _scheduleRepository.UpdateAsync(schedule);

            // Delete existing details and re-add
            await _detailRepository.DeleteByScheduleAsync(id);
            await _unitOfWork.SaveChangesAsync();

            foreach (var detailDto in dto.Details)
            {
                var detail = new UnitOfMeasureScheduleDetail
                {
                    ScheduleId = schedule.Id,
                    UnitOfMeasureId = detailDto.UnitOfMeasureId,
                    BaseQuantity = detailDto.BaseQuantity,
                    SortOrder = detailDto.SortOrder,
                    TenantId = schedule.TenantId,
                    CreatedById = _currentUserProvider.UserId
                };
                await _detailRepository.AddAsync(detail);
            }
            await _unitOfWork.SaveChangesAsync();

            var updated = await _scheduleRepository.GetWithDetailsAsync(id);
            _logger.LogInformation("Updated UoM schedule {Id}", id);
            return Ok(MapToDto(updated!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UoM schedule {Id}", id);
            return StatusCode(500, "An error occurred while updating the UoM schedule");
        }
    }

    /// <summary>
    /// Deletes a UoM schedule
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(id);
            if (schedule == null)
                return NotFound($"UoM schedule with ID {id} not found");

            // Delete details first
            await _detailRepository.DeleteByScheduleAsync(id);
            await _scheduleRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted UoM schedule {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting UoM schedule {Id}", id);
            return StatusCode(500, "An error occurred while deleting the UoM schedule");
        }
    }

    private static UnitOfMeasureScheduleDto MapToDto(UnitOfMeasureSchedule schedule) => new()
    {
        Id = schedule.Id,
        ScheduleId = schedule.ScheduleId,
        Description = schedule.Description,
        BaseUnitOfMeasureId = schedule.BaseUnitOfMeasureId,
        BaseUnitOfMeasureName = schedule.BaseUnitOfMeasure?.Name,
        BaseUnitOfMeasureCode = schedule.BaseUnitOfMeasure?.Code,
        QuantityDecimals = schedule.QuantityDecimals,
        IsActive = schedule.IsActive,
        Details = schedule.Details.Select(d => new UnitOfMeasureScheduleDetailDto
        {
            Id = d.Id,
            ScheduleId = d.ScheduleId,
            UnitOfMeasureId = d.UnitOfMeasureId,
            UnitOfMeasureName = d.UnitOfMeasure?.Name,
            UnitOfMeasureCode = d.UnitOfMeasure?.Code,
            BaseQuantity = d.BaseQuantity,
            SortOrder = d.SortOrder
        }).OrderBy(d => d.SortOrder).ToList()
    };
}

