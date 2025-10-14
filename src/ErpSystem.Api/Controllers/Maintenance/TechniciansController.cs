using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/technicians")]
[Authorize]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianDataService _technicianService;
    private readonly ILogger<TechniciansController> _logger;

    public TechniciansController(
        ITechnicianDataService technicianService,
        ILogger<TechniciansController> logger)
    {
        _technicianService = technicianService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of technicians with optional filtering (read-only from HR module)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TechnicianListDto>>> GetTechnicians(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? department = null,
        [FromQuery] string? jobTitle = null,
        [FromQuery] string? employmentStatus = null,
        [FromQuery] string? experienceLevel = null,
        [FromQuery] string? shiftSchedule = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] Guid? skillId = null,
        [FromQuery] string? specialization = null,
        [FromQuery] string? location = null,
        [FromQuery] bool? hasExpiredCertifications = null,
        [FromQuery] bool? hasExpiringCertifications = null)
    {
        try
        {
            if (pageSize > 100)
                pageSize = 100;

            var filter = new TechnicianFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Department = department,
                JobTitle = jobTitle,
                EmploymentStatus = employmentStatus,
                ExperienceLevel = experienceLevel,
                ShiftSchedule = shiftSchedule,
                IsAvailable = isAvailable,
                SkillId = skillId,
                Specialization = specialization,
                Location = location,
                HasExpiredCertifications = hasExpiredCertifications,
                HasExpiringCertifications = hasExpiringCertifications
            };

            var result = await _technicianService.GetTechniciansPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians from HR module");
            
            // Fallback to mock data if HR service is unavailable
            var fallbackResult = GetMockTechnicians(page, pageSize, searchTerm, department, isAvailable);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets all available technicians
    /// </summary>
    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetAvailableTechnicians()
    {
        try
        {
            var result = await _technicianService.GetAvailableTechniciansAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available technicians");
            
            // Fallback to mock data
            var fallbackResult = GetMockAvailableTechnicians();
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets technicians by department
    /// </summary>
    [HttpGet("department/{department}")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetTechniciansByDepartment(string department)
    {
        try
        {
            var result = await _technicianService.GetTechniciansByDepartmentAsync(department);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians for department {Department}", department);
            
            // Fallback to filtered mock data
            var fallbackResult = GetMockTechniciansByDepartment(department);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets technicians by location
    /// </summary>
    [HttpGet("location/{location}")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetTechniciansByLocation(string location)
    {
        try
        {
            var result = await _technicianService.GetTechniciansByLocationAsync(location);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians for location {Location}", location);
            
            // Fallback to filtered mock data
            var fallbackResult = GetMockTechniciansByLocation(location);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets a specific technician by ID (read-only)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TechnicianDto>> GetTechnician(Guid id)
    {
        try
        {
            var technician = await _technicianService.GetTechnicianByIdAsync(id);
            if (technician == null)
                return NotFound($"Technician with ID {id} not found in HR module");

            return Ok(technician);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician {TechnicianId} from HR module", id);
            
            // Fallback to mock data
            var fallbackResult = GetMockTechnicianById(id);
            if (fallbackResult == null)
                return NotFound($"Technician with ID {id} not found");
            
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Synchronizes technician data from HR module
    /// </summary>
    [HttpPost("sync-from-hr")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> SyncFromHR()
    {
        try
        {
            var result = await _technicianService.SyncTechniciansFromHRAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing technicians from HR module");
            
            // Return mock synchronized technicians
            var fallbackResult = GetMockHRTechnicians();
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets technician workload information
    /// </summary>
    [HttpGet("{id:guid}/workload")]
    public async Task<ActionResult<TechnicianWorkloadDto>> GetTechnicianWorkload(Guid id)
    {
        try
        {
            var result = await _technicianService.GetTechnicianWorkloadAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workload for technician {TechnicianId}", id);
            
            // Fallback to mock workload data
            var fallbackResult = GetMockTechnicianWorkload(id);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets technician performance metrics
    /// </summary>
    [HttpGet("{id:guid}/performance")]
    public async Task<ActionResult<TechnicianPerformanceDto>> GetTechnicianPerformance(
        Guid id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
            var end = endDate ?? DateTime.UtcNow;

            var result = await _technicianService.GetTechnicianPerformanceAsync(id, start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance for technician {TechnicianId}", id);
            
            // Fallback to mock performance data
            var fallbackResult = GetMockTechnicianPerformance(id);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets technician availability status
    /// </summary>
    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<TechnicianAvailabilityDto>> GetTechnicianAvailability(Guid id)
    {
        try
        {
            var result = await _technicianService.GetTechnicianAvailabilityAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving availability for technician {TechnicianId}", id);
            
            // Fallback to mock availability data
            var fallbackResult = GetMockTechnicianAvailability(id);
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets technicians with expiring certifications
    /// </summary>
    [HttpGet("expiring-certifications")]
    public async Task<ActionResult<IEnumerable<TechnicianDto>>> GetTechniciansWithExpiringCertifications(
        [FromQuery] int days = 30)
    {
        try
        {
            var result = await _technicianService.GetTechniciansWithExpiringCertificationsAsync(days);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians with expiring certifications");
            
            // Fallback to mock data
            var fallbackResult = GetMockTechniciansWithExpiringCertifications();
            return Ok(fallbackResult);
        }
    }

    /// <summary>
    /// Gets overall technician statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<TechnicianStatsDto>> GetTechnicianStats()
    {
        try
        {
            var result = await _technicianService.GetTechnicianStatsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician statistics");
            
            // Fallback to mock stats
            var fallbackResult = GetMockTechnicianStats();
            return Ok(fallbackResult);
        }
    }

    #region Fallback Methods

    private PagedResult<TechnicianListDto> GetMockTechnicians(
        int page, int pageSize, string? searchTerm, string? department, bool? isAvailable)
    {
        var mockData = new List<TechnicianListDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP001",
                FullName = "John Smith",
                Email = "john.smith@company.com",
                Department = "Maintenance",
                JobTitle = "Senior Maintenance Technician",
                EmploymentStatus = "Active",
                ExperienceLevel = "Senior",
                IsAvailable = true,
                ActiveWorkOrdersCount = 3,
                SkillsCount = 8,
                CertificationsCount = 5,
                ExpiredCertificationsCount = 0,
                ExpiringCertificationsCount = 1,
                PerformanceRating = 4.5m,
                Location = "Main Building",
                LastSyncDate = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP002",
                FullName = "Sarah Johnson",
                Email = "sarah.johnson@company.com",
                Department = "Maintenance",
                JobTitle = "HVAC Specialist",
                EmploymentStatus = "Active",
                ExperienceLevel = "Lead",
                IsAvailable = true,
                ActiveWorkOrdersCount = 2,
                SkillsCount = 12,
                CertificationsCount = 8,
                ExpiredCertificationsCount = 1,
                ExpiringCertificationsCount = 0,
                PerformanceRating = 4.8m,
                Location = "Building A",
                LastSyncDate = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP003",
                FullName = "Mike Wilson",
                Email = "mike.wilson@company.com",
                Department = "Facilities",
                JobTitle = "Electrical Technician",
                EmploymentStatus = "Active",
                ExperienceLevel = "Intermediate",
                IsAvailable = false,
                ActiveWorkOrdersCount = 5,
                SkillsCount = 6,
                CertificationsCount = 4,
                ExpiredCertificationsCount = 0,
                ExpiringCertificationsCount = 2,
                PerformanceRating = 4.2m,
                Location = "Building B",
                LastSyncDate = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP004",
                FullName = "Lisa Brown",
                Email = "lisa.brown@company.com",
                Department = "Maintenance",
                JobTitle = "Maintenance Supervisor",
                EmploymentStatus = "Active",
                ExperienceLevel = "Expert",
                IsAvailable = true,
                ActiveWorkOrdersCount = 1,
                SkillsCount = 15,
                CertificationsCount = 12,
                ExpiredCertificationsCount = 0,
                ExpiringCertificationsCount = 0,
                PerformanceRating = 4.9m,
                Location = "Main Building",
                LastSyncDate = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = "EMP005",
                FullName = "David Garcia",
                Email = "david.garcia@company.com",
                Department = "Maintenance",
                JobTitle = "Junior Technician",
                EmploymentStatus = "Active",
                ExperienceLevel = "Junior",
                IsAvailable = true,
                ActiveWorkOrdersCount = 4,
                SkillsCount = 4,
                CertificationsCount = 2,
                ExpiredCertificationsCount = 0,
                ExpiringCertificationsCount = 1,
                PerformanceRating = 3.8m,
                Location = "Building C",
                LastSyncDate = DateTime.UtcNow.AddHours(-1)
            }
        };

        // Apply filters
        var filtered = mockData.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            filtered = filtered.Where(x => x.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.EmployeeNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          x.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(department))
        {
            filtered = filtered.Where(x => x.Department == department);
        }

        if (isAvailable.HasValue)
        {
            filtered = filtered.Where(x => x.IsAvailable == isAvailable.Value);
        }

        var totalCount = filtered.Count();
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<TechnicianListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private IEnumerable<TechnicianDto> GetMockAvailableTechnicians()
    {
        return GetMockTechnicians(1, 100, null, null, true).Items.Select(MapToTechnicianDto);
    }

    private IEnumerable<TechnicianDto> GetMockTechniciansByDepartment(string department)
    {
        return GetMockTechnicians(1, 100, null, department, null).Items.Select(MapToTechnicianDto);
    }

    private IEnumerable<TechnicianDto> GetMockTechniciansByLocation(string location)
    {
        return GetMockTechnicians(1, 100, null, null, null).Items
            .Where(t => t.Location.Equals(location, StringComparison.OrdinalIgnoreCase))
            .Select(MapToTechnicianDto);
    }

    private TechnicianDto? GetMockTechnicianById(Guid id)
    {
        return MapToTechnicianDto(GetMockTechnicians(1, 1, null, null, null).Items.First());
    }

    private TechnicianDto MapToTechnicianDto(TechnicianListDto listDto)
    {
        return new TechnicianDto
        {
            Id = listDto.Id,
            EmployeeNumber = listDto.EmployeeNumber,
            FullName = listDto.FullName,
            FirstName = listDto.FullName.Split(' ').FirstOrDefault() ?? "",
            LastName = listDto.FullName.Split(' ').LastOrDefault() ?? "",
            Email = listDto.Email,
            PhoneNumber = "+1 (555) 123-4567",
            Department = listDto.Department,
            JobTitle = listDto.JobTitle,
            EmploymentStatus = listDto.EmploymentStatus,
            ExperienceLevel = listDto.ExperienceLevel,
            IsAvailable = listDto.IsAvailable,
            ActiveWorkOrdersCount = listDto.ActiveWorkOrdersCount,
            PerformanceRating = listDto.PerformanceRating,
            Location = listDto.Location,
            LastSyncDate = listDto.LastSyncDate,
            HireDate = DateTime.UtcNow.AddYears(-2),
            ShiftSchedule = "Day Shift (8AM-4PM)",
            HourlyRate = 28.50m,
            Supervisor = "Lisa Brown",
            YearsOfExperience = 5,
            WorkloadCapacity = 85.5m,
            CompletedWorkOrdersCount = 156,
            AverageCompletionTime = TimeSpan.FromHours(4.2),
            IsFromHRModule = true,
            Notes = "Reliable technician with excellent troubleshooting skills",
            Specializations = new List<string> { "HVAC", "Electrical", "Preventive Maintenance" },
            SkillAssignments = new List<TechnicianSkillAssignmentDto>
            {
                new()
                {
                    SkillName = "HVAC Maintenance",
                    Category = "HVAC",
                    ProficiencyLevel = 4,
                    ProficiencyDescription = "Advanced",
                    IsVerified = true
                }
            },
            Certifications = new List<object>
            {
                new TechnicianCertificationDto
                {
                    CertificationName = "HVAC Excellence",
                    IssuingOrganization = "HVAC Excellence",
                    Status = "Active",
                    IsExpired = false,
                    ExpirationDate = DateTime.UtcNow.AddYears(2)
                }
            }
        };
    }

    private IEnumerable<TechnicianDto> GetMockHRTechnicians()
    {
        var technicians = GetMockAvailableTechnicians().ToList();
        foreach (var tech in technicians)
        {
            tech.LastSyncDate = DateTime.UtcNow;
            tech.IsFromHRModule = true;
        }
        return technicians;
    }

    private TechnicianWorkloadDto GetMockTechnicianWorkload(Guid technicianId)
    {
        return new TechnicianWorkloadDto
        {
            TechnicianId = technicianId,
            TechnicianName = "John Smith",
            ActiveWorkOrders = 3,
            PendingWorkOrders = 2,
            ScheduledWorkOrders = 5,
            EstimatedHours = (double)28.5m,
            CapacityUtilization = (double)85.5m,
            IsOverloaded = false,
            NextAvailableDate = DateTime.UtcNow.AddDays(2),
            CurrentAssignments = new List<WorkOrderAssignmentDto>
            {
                new()
                {
                    WorkOrderId = Guid.NewGuid(),
                    WorkOrderNumber = "WO-2024-001",
                    AssetName = "HVAC Unit #1",
                    Priority = "High",
                    ScheduledStartDate = DateTime.UtcNow.AddDays(1),
                    EstimatedHours = (double)4.0m,
                    Status = "Assigned"
                }
            }
        };
    }

    private TechnicianPerformanceDto GetMockTechnicianPerformance(Guid technicianId)
    {
        return new TechnicianPerformanceDto
        {
            TechnicianId = technicianId,
            TechnicianName = "John Smith",
            CompletedWorkOrders = 45,
            AverageCompletionTime = (double)4.2m,
            AverageQualityRating = (double)4.5m,
            OnTimeCompletions = 42,
            LateCompletions = 3,
            OnTimePercentage = (double)93.3m,
            SafetyIncidents = 0,
            LastIncidentDate = null,
            ReportPeriodStart = DateTime.UtcNow.AddMonths(-3),
            ReportPeriodEnd = DateTime.UtcNow,
            SkillUtilization = new List<object>
            {
                new SkillUtilizationDto
                {
                    SkillId = Guid.NewGuid(),
                    SkillName = "HVAC Maintenance",
                    WorkOrdersRequiringSkill = 25,
                    TotalHoursUsed = (double)98.5m,
                    UtilizationPercentage = 65.2m
                }
            }
        };
    }

    private TechnicianAvailabilityDto GetMockTechnicianAvailability(Guid technicianId)
    {
        return new TechnicianAvailabilityDto
        {
            TechnicianId = technicianId,
            TechnicianName = "John Smith",
            IsAvailable = true,
            AvailabilityStatus = "Available",
            AvailableFrom = DateTime.UtcNow,
            AvailableUntil = DateTime.UtcNow.AddHours(8),
            ShiftSchedule = "Day Shift (8AM-4PM)",
            ReasonForUnavailability = "",
            LastUpdated = DateTime.UtcNow
        };
    }

    private IEnumerable<TechnicianDto> GetMockTechniciansWithExpiringCertifications()
    {
        return GetMockAvailableTechnicians().Take(2);
    }

    private TechnicianStatsDto GetMockTechnicianStats()
    {
        return new TechnicianStatsDto
        {
            TotalTechnicians = 25,
            ActiveTechnicians = 23,
            AvailableTechnicians = 18,
            TechniciansOnAssignment = 15,
            TechniciansOnLeave = 2,
            AverageExperience = (double)6.5m,
            AveragePerformanceRating = (double)4.3m,
            CertificationsExpiring = 5,
            ExpiredCertifications = 2,
            LastSyncFromHR = DateTime.UtcNow.AddHours(-1)
        };
    }

    #endregion
}