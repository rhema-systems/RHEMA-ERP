using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class WorkOrderLaborService : IWorkOrderLaborService
{
    private readonly IWorkOrderLaborRepository _laborRepository;
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly ErpSystem.Core.Interfaces.HR.IEmployeeRepository _employeeRepository;
    private readonly IUserService _userService;
    private readonly ILogger<WorkOrderLaborService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public WorkOrderLaborService(
        IWorkOrderLaborRepository laborRepository,
        IWorkOrderRepository workOrderRepository,
        ErpSystem.Core.Interfaces.HR.IEmployeeRepository employeeRepository,
        IUserService userService,
        ILogger<WorkOrderLaborService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _laborRepository = laborRepository;
        _workOrderRepository = workOrderRepository;
        _employeeRepository = employeeRepository;
        _userService = userService;
        _logger = logger;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<WorkOrderLaborDto> StartLaborAsync(CreateWorkOrderLaborDto createDto)
    {
        try
        {
            _logger.LogInformation("Starting labor for work order {WorkOrderId} with technician {TechnicianId}",
                createDto.WorkOrderId, createDto.TechnicianId);

            var workOrder = await _workOrderRepository.GetByIdAsync(createDto.WorkOrderId)
                ?? throw new ArgumentException($"Work order with ID {createDto.WorkOrderId} not found");

            // Use the technician ID from the DTO (selected by the user in the UI)
            var technicianId = createDto.TechnicianId;
            if (technicianId == Guid.Empty)
            {
                throw new ArgumentException("Technician ID is required to log labor");
            }

            // Verify the technician (employee) exists
            var technician = await _employeeRepository.GetByIdAsync(technicianId, e => e.OrganizationUnit)
                ?? throw new ArgumentException($"Technician with ID {technicianId} not found in HR system");
            if (!technician.IsActive)
                throw new InvalidOperationException($"Technician {technician.FullName} is not active");
            if (!technician.CanBeAssignedToMaintenance)
                throw new InvalidOperationException($"Employee {technician.FullName} is not qualified for maintenance assignments");

            var labor = new WorkOrderLabor
            {
                Id = Guid.NewGuid(),
                WorkOrderId = createDto.WorkOrderId,
                TechnicianId = technicianId,
                StartTime = createDto.StartTime,
                HourlyRate = createDto.HourlyRate,
                Notes = createDto.Notes,
                LaborType = createDto.LaborType,
                TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required"),
                CreatedAt = DateTime.UtcNow
            };

            await _laborRepository.AddAsync(labor);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Started labor {LaborId} for work order {WorkOrderId} by technician {TechnicianId}",
                labor.Id, createDto.WorkOrderId, technicianId);

            return await MapToDto(labor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting labor for work order {WorkOrderId}", createDto.WorkOrderId);
            throw;
        }
    }

    public async Task<WorkOrderLaborDto> EndLaborAsync(Guid id, DateTime endTime, string? notes = null)
    {
        try
        {
            var labor = await _laborRepository.GetByIdAsync(id)
                ?? throw new ArgumentException($"Labor record with ID {id} not found");

            labor.EndTime = endTime;
            labor.Hours = (endTime - labor.StartTime).TotalHours;
            labor.TotalCost = (decimal)labor.Hours * labor.HourlyRate;
            labor.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(notes))
            {
                labor.Notes = string.IsNullOrWhiteSpace(labor.Notes)
                    ? notes
                    : labor.Notes + "\n" + notes;
            }

            await _laborRepository.UpdateAsync(labor);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Ended labor {LaborId} with {Hours} hours", id, labor.Hours);

            return await MapToDto(labor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending labor {LaborId}", id);
            throw;
        }
    }

    public async Task<WorkOrderLaborDto> UpdateLaborAsync(Guid id, UpdateWorkOrderLaborDto updateDto)
    {
        try
        {
            var labor = await _laborRepository.GetByIdAsync(id)
                ?? throw new ArgumentException($"Labor record with ID {id} not found");

            labor.StartTime = updateDto.StartTime;
            labor.EndTime = updateDto.EndTime;
            labor.Hours = updateDto.Hours;
            labor.HourlyRate = updateDto.HourlyRate;
            labor.TotalCost = (decimal)updateDto.Hours * updateDto.HourlyRate;
            labor.Notes = updateDto.Notes;
            labor.LaborType = updateDto.LaborType;
            labor.UpdatedAt = DateTime.UtcNow;

            await _laborRepository.UpdateAsync(labor);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated labor {LaborId}", id);

            return await MapToDto(labor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating labor {LaborId}", id);
            throw;
        }
    }

    public async Task DeleteLaborAsync(Guid id)
    {
        try
        {
            var labor = await _laborRepository.GetByIdAsync(id)
                ?? throw new ArgumentException($"Labor record with ID {id} not found");

            await _laborRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted labor {LaborId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting labor {LaborId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderLaborDto>> GetLaborByWorkOrderAsync(Guid workOrderId)
    {
        try
        {
            var laborRecords = await _laborRepository.GetByWorkOrderIdAsync(workOrderId);
            var dtos = new List<WorkOrderLaborDto>();
            foreach (var labor in laborRecords)
            {
                dtos.Add(await MapToDto(labor));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting labor for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<IEnumerable<WorkOrderLaborDto>> GetLaborByTechnicianAsync(Guid technicianId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var laborRecords = await _laborRepository.GetByTechnicianIdAsync(technicianId);

            if (startDate.HasValue)
                laborRecords = laborRecords.Where(l => l.StartTime >= startDate.Value);
            if (endDate.HasValue)
                laborRecords = laborRecords.Where(l => l.StartTime <= endDate.Value);

            var dtos = new List<WorkOrderLaborDto>();
            foreach (var labor in laborRecords)
            {
                dtos.Add(await MapToDto(labor));
            }
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting labor for technician {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<decimal> GetTotalLaborCostAsync(Guid workOrderId)
    {
        try
        {
            return await _laborRepository.GetTotalCostByWorkOrderAsync(workOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total labor cost for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<LaborReportDto> GetLaborReportAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // TechnicianId references Employee (HR)
            var technician = await _employeeRepository.GetByIdAsync(technicianId);
            var technicianName = technician?.FullName ?? "Unknown";

            var totalHours = await _laborRepository.GetTotalHoursByTechnicianAsync(technicianId, startDate, endDate);
            var totalCost = await _laborRepository.GetTotalLaborCostByTechnicianAsync(technicianId, startDate, endDate);
            var laborRecords = await _laborRepository.GetByTechnicianIdAsync(technicianId);
            var filteredRecords = laborRecords.Where(l => l.StartTime >= startDate && l.StartTime <= endDate).ToList();

            return new LaborReportDto
            {
                TechnicianId = technicianId,
                TechnicianName = technicianName,
                StartDate = startDate,
                EndDate = endDate,
                TotalHours = totalHours,
                TotalCost = totalCost,
                WorkOrdersCompleted = filteredRecords.Select(l => l.WorkOrderId).Distinct().Count(),
                RegularHours = filteredRecords.Where(l => l.LaborType == "Regular").Sum(l => l.Hours),
                OvertimeHours = filteredRecords.Where(l => l.LaborType == "Overtime").Sum(l => l.Hours),
                EmergencyHours = filteredRecords.Where(l => l.LaborType == "Emergency").Sum(l => l.Hours),
                UtilizationPercentage = totalHours > 0 ? (totalHours / ((endDate - startDate).TotalDays * 8)) * 100 : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting labor report for technician {TechnicianId}", technicianId);
            throw;
        }
    }

    private async Task<WorkOrderLaborDto> MapToDto(WorkOrderLabor labor)
    {
        // TechnicianId is employee-driven (HR Employees table).
        // Backward compatibility: if an old labor record still has a user-based TechnicianId, fall back to IUserService.
        EmployeeDto? technicianDto = null;
        try
        {
            var employee = await _employeeRepository.GetByIdAsync(labor.TechnicianId);
            if (employee != null)
            {
                technicianDto = new EmployeeDto
                {
                    Id = employee.Id,
                    FullName = employee.FullName,
                    EmployeeNumber = employee.EmployeeNumber
                };
            }
            else
            {
                var user = await _userService.GetUserByIdAsync(labor.TechnicianId);
                if (user != null)
                {
                    technicianDto = new EmployeeDto
                    {
                        Id = user.Id,
                        FullName = $"{user.FirstName} {user.LastName}",
                        EmployeeNumber = user.UserName ?? string.Empty
                    };
                }
            }
        }
        catch { }

        return new WorkOrderLaborDto
        {
            Id = labor.Id,
            WorkOrderId = labor.WorkOrderId,
            TechnicianId = labor.TechnicianId,
            StartTime = labor.StartTime,
            EndTime = labor.EndTime,
            Hours = labor.Hours,
            HourlyRate = labor.HourlyRate,
            TotalCost = labor.TotalCost,
            Notes = labor.Notes,
            LaborType = labor.LaborType ?? "Regular",
            Technician = technicianDto,
            CreatedAt = labor.CreatedAt,
            UpdatedAt = labor.UpdatedAt
        };
    }
}

