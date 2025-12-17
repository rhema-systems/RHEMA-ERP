using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class MaintenanceStaffScheduleService : IMaintenanceStaffScheduleService
{
    private readonly IMaintenanceStaffScheduleRepository _scheduleRepository;
    private readonly ErpSystem.Core.Interfaces.HR.IEmployeeRepository _employeeRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MaintenanceStaffScheduleService> _logger;
    private readonly IUserService _userService;

    public MaintenanceStaffScheduleService(
        IMaintenanceStaffScheduleRepository scheduleRepository,
        ErpSystem.Core.Interfaces.HR.IEmployeeRepository employeeRepository,
        IWorkOrderRepository workOrderRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<MaintenanceStaffScheduleService> logger,
        IUserService userService)
    {
        _scheduleRepository = scheduleRepository;
        _employeeRepository = employeeRepository;
        _workOrderRepository = workOrderRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _userService = userService;
    }

    public async Task<MaintenanceStaffScheduleDto> CreateScheduleAsync(CreateMaintenanceStaffScheduleDto createDto)
    {
        try
        {
            var schedule = new MaintenanceStaffSchedule
            {
                TechnicianId = createDto.TechnicianId,
                StartDateTime = createDto.StartDateTime,
                EndDateTime = createDto.EndDateTime,
                ScheduleType = createDto.ScheduleType,
                Status = createDto.Status,
                WorkOrderId = createDto.WorkOrderId,
                JobCardId = createDto.JobCardId,
                TeamId = createDto.TeamId,
                WorkLocation = createDto.WorkLocation,
                Address = createDto.Address,
                Latitude = createDto.Latitude,
                Longitude = createDto.Longitude,
                RequiresTravel = createDto.RequiresTravel,
                DepartureTime = createDto.DepartureTime,
                ArrivalTime = createDto.ArrivalTime,
                EstimatedTravelMinutes = createDto.EstimatedTravelMinutes,
                AssignedVehicleId = createDto.AssignedVehicleId,
                TransportationType = createDto.TransportationType,
                Notes = createDto.Notes,
                TenantId = _currentUserService.TenantId ?? Guid.Empty
            };

            await _scheduleRepository.AddAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance staff schedule");
            throw;
        }
    }

    public async Task<MaintenanceStaffScheduleDto> UpdateScheduleAsync(Guid id, UpdateMaintenanceStaffScheduleDto updateDto)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Schedule {id} not found");
            if (updateDto.StartDateTime.HasValue)
            {
                schedule.StartDateTime = updateDto.StartDateTime.Value;
            }

            if (updateDto.EndDateTime.HasValue)
            {
                schedule.EndDateTime = updateDto.EndDateTime.Value;
            }

            if (!string.IsNullOrEmpty(updateDto.Status))
            {
                schedule.Status = updateDto.Status;
            }

            if (!string.IsNullOrEmpty(updateDto.WorkLocation))
            {
                schedule.WorkLocation = updateDto.WorkLocation;
            }

            if (!string.IsNullOrEmpty(updateDto.Address))
            {
                schedule.Address = updateDto.Address;
            }

            if (updateDto.Latitude.HasValue)
            {
                schedule.Latitude = updateDto.Latitude;
            }

            if (updateDto.Longitude.HasValue)
            {
                schedule.Longitude = updateDto.Longitude;
            }

            if (updateDto.RequiresTravel.HasValue)
            {
                schedule.RequiresTravel = updateDto.RequiresTravel.Value;
            }

            if (updateDto.DepartureTime.HasValue)
            {
                schedule.DepartureTime = updateDto.DepartureTime;
            }

            if (updateDto.ArrivalTime.HasValue)
            {
                schedule.ArrivalTime = updateDto.ArrivalTime;
            }

            if (updateDto.EstimatedTravelMinutes.HasValue)
            {
                schedule.EstimatedTravelMinutes = updateDto.EstimatedTravelMinutes;
            }

            if (updateDto.ActualTravelMinutes.HasValue)
            {
                schedule.ActualTravelMinutes = updateDto.ActualTravelMinutes;
            }

            if (updateDto.AssignedVehicleId.HasValue)
            {
                schedule.AssignedVehicleId = updateDto.AssignedVehicleId;
            }

            if (!string.IsNullOrEmpty(updateDto.TransportationType))
            {
                schedule.TransportationType = updateDto.TransportationType;
            }

            if (!string.IsNullOrEmpty(updateDto.Notes))
            {
                schedule.Notes = updateDto.Notes;
            }

            if (updateDto.ActualStartTime.HasValue)
            {
                schedule.ActualStartTime = updateDto.ActualStartTime;
            }

            if (updateDto.ActualEndTime.HasValue)
            {
                schedule.ActualEndTime = updateDto.ActualEndTime;
            }

            schedule.UpdatedAt = DateTime.UtcNow;

            await _scheduleRepository.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", id);
            throw;
        }
    }

    public async Task DeleteScheduleAsync(Guid id)
    {
        try
        {
            await _scheduleRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", id);
            throw;
        }
    }

    public async Task<MaintenanceStaffScheduleDto?> GetScheduleByIdAsync(Guid id)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdWithDetailsAsync(id);
            return schedule == null ? null : await MapToDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule {ScheduleId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceStaffScheduleDto>> GetSchedulesByTechnicianIdAsync(Guid technicianId)
    {
        try
        {
            var schedules = await _scheduleRepository.GetByTechnicianIdAsync(technicianId);

            // Map sequentially to avoid concurrent DbContext operations from parallel MapToDto calls
            var result = new List<MaintenanceStaffScheduleDto>();
            foreach (var schedule in schedules)
            {
                result.Add(await MapToDto(schedule));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules for technician {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceStaffScheduleDto>> GetSchedulesByWorkOrderIdAsync(Guid workOrderId)
    {
        try
        {
            var schedules = await _scheduleRepository.GetByWorkOrderIdAsync(workOrderId);

            // Map sequentially to avoid concurrent DbContext operations from parallel MapToDto calls
            var result = new List<MaintenanceStaffScheduleDto>();
            foreach (var schedule in schedules)
            {
                result.Add(await MapToDto(schedule));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceStaffScheduleDto>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var schedules = await _scheduleRepository.GetByDateRangeAsync(startDate, endDate);

            // Map sequentially to avoid concurrent DbContext operations from parallel MapToDto calls
            var result = new List<MaintenanceStaffScheduleDto>();
            foreach (var schedule in schedules)
            {
                result.Add(await MapToDto(schedule));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedules for date range");
            throw;
        }
    }

    public async Task<MaintenanceStaffScheduleDto> StartScheduleAsync(Guid id)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Schedule {id} not found");
            schedule.Status = "InProgress";
            schedule.ActualStartTime = DateTime.UtcNow;
            schedule.UpdatedAt = DateTime.UtcNow;

            await _scheduleRepository.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting schedule {ScheduleId}", id);
            throw;
        }
    }

    public async Task<MaintenanceStaffScheduleDto> CompleteScheduleAsync(Guid id, int? actualTravelMinutes = null)
    {
        try
        {
            var schedule = await _scheduleRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Schedule {id} not found");
            schedule.Status = "Completed";
            schedule.ActualEndTime = DateTime.UtcNow;
            if (actualTravelMinutes.HasValue)
            {
                schedule.ActualTravelMinutes = actualTravelMinutes;
            }

            schedule.UpdatedAt = DateTime.UtcNow;

            await _scheduleRepository.UpdateAsync(schedule);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDto(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing schedule {ScheduleId}", id);
            throw;
        }
    }

    private async Task<MaintenanceStaffScheduleDto> MapToDto(MaintenanceStaffSchedule schedule)
    {
        string technicianName = string.Empty;

        // TechnicianId now references ApplicationUser (Users table) instead of Employee.
        // Resolve via IUserService (which queries identity users) so we get names from the Users table.
        try
        {
            var user = await _userService.GetUserByIdAsync(schedule.TechnicianId);
            if (user != null)
            {
                technicianName = $"{user.FirstName} {user.LastName}";
            }
        }
        catch { }

        return new MaintenanceStaffScheduleDto
        {
            Id = schedule.Id,
            TechnicianId = schedule.TechnicianId,
            TechnicianName = technicianName,
            StartDateTime = schedule.StartDateTime,
            EndDateTime = schedule.EndDateTime,
            ScheduleType = schedule.ScheduleType,
            Status = schedule.Status,
            WorkOrderId = schedule.WorkOrderId,
            WorkOrderNumber = schedule.WorkOrder?.WorkOrderNumber,
            JobCardId = schedule.JobCardId,
            JobCardNumber = schedule.JobCard?.JobCardNumber,
            TeamId = schedule.TeamId,
            TeamName = schedule.Team?.Name,
            WorkLocation = schedule.WorkLocation,
            Address = schedule.Address,
            Latitude = schedule.Latitude,
            Longitude = schedule.Longitude,
            RequiresTravel = schedule.RequiresTravel,
            DepartureTime = schedule.DepartureTime,
            ArrivalTime = schedule.ArrivalTime,
            EstimatedTravelMinutes = schedule.EstimatedTravelMinutes,
            ActualTravelMinutes = schedule.ActualTravelMinutes,
            AssignedVehicleId = schedule.AssignedVehicleId,
            VehicleName = schedule.AssignedVehicle?.Name,
            TransportationType = schedule.TransportationType,
            Notes = schedule.Notes,
            ActualStartTime = schedule.ActualStartTime,
            ActualEndTime = schedule.ActualEndTime,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }
}
