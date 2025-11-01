using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;
using System;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing technician data with read-only HR module integration
/// Technician core data comes from HR module, with maintenance-specific extensions
/// </summary>
public class TechnicianService : ITechnicianService
{
    private readonly ITechnicianRepository _technicianRepository;
    private readonly ITechnicianSkillAssignmentRepository _skillAssignmentRepository;
    private readonly ILogger<TechnicianService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public TechnicianService(
        ITechnicianRepository technicianRepository,
        ITechnicianSkillAssignmentRepository skillAssignmentRepository,
        ILogger<TechnicianService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _technicianRepository = technicianRepository;
        _skillAssignmentRepository = skillAssignmentRepository;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    #region Read-Only Operations (HR Integration)

    public async Task<TechnicianDto?> GetTechnicianByIdAsync(Guid id)
    {
        try
        {
            // This would typically call HR API first, then enrich with maintenance data
            var technician = await _technicianRepository.GetByIdAsync(id);
            if (technician == null)
            {
                // Try to fetch from HR module as fallback
                _logger.LogInformation("Technician {TechnicianId} not found locally, attempting HR lookup", id);
                return await GetTechnicianFromHRAsync(id);
            }

            return await MapToDto(technician);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician: {TechnicianId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetAllTechniciansAsync()
    {
        try
        {
            // Sync with HR first to ensure we have latest data
            await SyncTechniciansFromHRAsync();
            
            var technicians = await _technicianRepository.GetAllAsync();
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all technicians");
            throw;
        }
    }

    public async Task<PagedResult<TechnicianDto>> GetTechniciansPagedAsync(TechnicianFilterDto filter)
    {
        try
        {
            var allTechnicians = await _technicianRepository.GetAllAsync();
            var filtered = allTechnicians.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                filtered = filtered.Where(t => t.FirstName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                              t.LastName.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                              t.EmployeeId.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filter.Department))
                filtered = filtered.Where(t => t.Department.Name == filter.Department);

            if (!string.IsNullOrEmpty(filter.Specialization))
                filtered = filtered.Where(t => t.Specialization == filter.Specialization);

            if (!string.IsNullOrEmpty(filter.CertificationLevel))
                filtered = filtered.Where(t => t.CertificationLevel == filter.CertificationLevel);

            if (filter.IsActive.HasValue)
                filtered = filtered.Where(t => t.IsActive == filter.IsActive.Value);

            if (filter.IsAvailable.HasValue && filter.IsAvailable.Value)
                filtered = filtered.Where(t => t.CurrentWorkload < t.MaxWorkload);

            var totalCount = filtered.Count();
            var techniciansPage = filtered
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            var items = new List<TechnicianDto>();
            foreach (var technician in techniciansPage)
            {
                items.Add(await MapToDto(technician));
            }

            return new PagedResult<TechnicianDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged technicians");
            throw;
        }
    }

    #endregion

    #region Business Logic

    public async Task<IEnumerable<TechnicianDto>> GetActiveTechniciansAsync()
    {
        try
        {
            var technicians = await _technicianRepository.GetActiveAsync();
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active technicians");
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetTechniciansByDepartmentAsync(string department)
    {
        try
        {
            var technicians = await _technicianRepository.GetByDepartmentAsync(department);
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians for department: {Department}", department);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetTechniciansBySpecializationAsync(string specialization)
    {
        try
        {
            var technicians = await _technicianRepository.GetBySpecializationAsync(specialization);
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians for specialization: {Specialization}", specialization);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetAvailableTechniciansAsync()
    {
        try
        {
            var technicians = await _technicianRepository.GetAvailableAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available technicians");
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId, int? minProficiencyLevel = null)
    {
        try
        {
            var assignments = await _skillAssignmentRepository.GetBySkillIdAsync(skillId);
            
            if (minProficiencyLevel.HasValue)
            {
                assignments = assignments.Where(a => a.ProficiencyLevel >= minProficiencyLevel.Value);
            }

            var result = new List<TechnicianDto>();
            foreach (var assignment in assignments)
            {
                var technician = await GetTechnicianByIdAsync(assignment.TechnicianId);
                if (technician != null)
                {
                    result.Add(technician);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians with skill: {SkillId}", skillId);
            return new List<TechnicianDto>();
        }
    }

    #endregion

    #region HR Integration

    public async Task<IEnumerable<TechnicianDto>> SyncTechniciansFromHRAsync()
    {
        try
        {
            _logger.LogInformation("Starting synchronization of technicians from HR module");

            // This would typically call the HR API to fetch technicians
            // For now, we'll simulate the sync with mock data
            var hrTechnicians = await _technicianRepository.SyncFromHRAsync();
            var result = new List<TechnicianDto>();
            
            foreach (var technician in hrTechnicians)
            {
                technician.LastSyncDate = DateTime.UtcNow;
                await _technicianRepository.UpdateAsync(technician);
                result.Add(await MapToDto(technician));
            }

            _logger.LogInformation("Synchronized {Count} technicians from HR module", result.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing technicians from HR module");
            throw;
        }
    }

    public async Task<TechnicianDto?> GetTechnicianFromHRAsync(Guid hrEmployeeId)
    {
        try
        {
            // This would typically call the HR API
            var technicians = await _technicianRepository.GetFromHRModuleAsync();
            var hrTechnician = technicians.FirstOrDefault(t => t.Id == hrEmployeeId);
            return hrTechnician != null ? await MapToDto(hrTechnician) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician from HR: {EmployeeId}", hrEmployeeId);
            return null;
        }
    }

    public async Task UpdateTechnicianFromHRAsync(TechnicianDto hrTechnician)
    {
        try
        {
            // TODO: Implement proper lookup when EmployeeId type issues are resolved
            if (hrTechnician.EmployeeId.HasValue)
            {
                var existingTechnician = await _technicianRepository.GetByIdAsync(hrTechnician.Id);
                if (existingTechnician != null)
                {
                    // Update existing technician with HR data
                    existingTechnician.FirstName = hrTechnician.FirstName;
                    existingTechnician.LastName = hrTechnician.LastName;
                    existingTechnician.Email = hrTechnician.Email;
                    existingTechnician.Phone = hrTechnician.Phone;
                    existingTechnician.IsActive = hrTechnician.IsActive;
                    existingTechnician.LastSyncDate = DateTime.UtcNow;
                    
                    await _technicianRepository.UpdateAsync(existingTechnician);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating technician from HR: {EmployeeId}", hrTechnician.EmployeeId);
        }
    }

    #endregion

    #region Analytics

    public async Task<TechnicianAnalyticsDto> GetTechnicianAnalyticsAsync()
    {
        try
        {
            var technicians = await _technicianRepository.GetAllAsync();
            var assignments = await _skillAssignmentRepository.GetAllAsync();

            return new TechnicianAnalyticsDto
            {
                TotalTechnicians = technicians.Count(),
                ActiveTechnicians = technicians.Count(t => t.IsActive),
                TechniciansWithSkills = assignments.Select(a => a.TechnicianId).Distinct().Count(),
                AverageSkillsPerTechnician = technicians.Any() ? 
                    (decimal)assignments.GroupBy(a => a.TechnicianId).Average(g => g.Count()) : 0,
                DepartmentBreakdown = technicians.GroupBy(t => t.Department?.Name ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count()),
                SpecializationBreakdown = technicians.GroupBy(t => t.Specialization)
                    .ToDictionary(g => g.Key, g => g.Count()),
                WorkloadAnalysis = new WorkloadAnalysisDto
                {
                    // Note: Remove these properties if they don't exist in WorkloadAnalysisDto
                    // OverutilizedTechnicians = technicians.Count(t => t.CurrentWorkload > t.MaxWorkload),
                    // OptimallyUtilizedTechnicians = technicians.Count(t => 
                    //     t.CurrentWorkload >= t.MaxWorkload * 0.8m && t.CurrentWorkload <= t.MaxWorkload),
                    // UnderutilizedTechnicians = technicians.Count(t => t.CurrentWorkload < t.MaxWorkload * 0.8m),
                    AverageUtilization = technicians.Any() && technicians.Sum(t => t.MaxWorkload) > 0 ? 
                        (decimal)(technicians.Sum(t => t.CurrentWorkload) / technicians.Sum(t => t.MaxWorkload) * 100) : 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating technician analytics");
            throw;
        }
    }

    #endregion

    #region Private Methods

    private async Task<TechnicianDto> MapToDto(Core.Entities.HR.Employee employee)
    {
        // Get skill assignments for this employee/technician
        var skillAssignments = await _skillAssignmentRepository.GetByTechnicianIdAsync(employee.Id);

        return new TechnicianDto
        {
            Id = employee.Id,
            EmployeeId = employee.Id, // Use the Employee's ID, not EmployeeNumber
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            FullName = employee.FullName,
            Email = employee.Email,
            Phone = employee.Phone,
            Department = employee.Department?.Name ?? "",
            Position = employee.Position?.Title ?? "",
            Specialization = employee.Specialization ?? "",
            CertificationLevel = employee.CertificationLevel ?? "",
            ExperienceLevel = employee.ExperienceLevel ?? "",
            HireDate = employee.DateEmployed?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue,
            IsActive = employee.IsActive,
            CurrentWorkload = employee.CurrentWorkload,
            MaxWorkload = employee.MaxWorkload,
            SkillsCount = skillAssignments.Count(),
            AverageRating = 0, // Would need to calculate from work orders
            CompletedWorkOrders = employee.AssignedWorkOrders.Count(wo => wo.Status == "Completed"),
            LastSyncDate = employee.LastSyncDate
        };
    }

    private async Task<TechnicianDto> MapToDto(Core.Entities.Maintenance.Technician technician)
    {
        // Get skill assignments for this technician
        var skillAssignments = await _skillAssignmentRepository.GetByTechnicianIdAsync(technician.Id);

        return new TechnicianDto
        {
            Id = technician.Id,
            EmployeeId = technician.EmployeeId,
            FirstName = technician.FirstName,
            LastName = technician.LastName,
            FullName = $"{technician.FirstName} {technician.LastName}",
            Email = technician.Email,
            Phone = technician.Phone,
            Department = technician.Department,
            Position = technician.Position,
            Specialization = technician.Specialization,
            CertificationLevel = technician.CertificationLevel,
            ExperienceLevel = technician.ExperienceLevel,
            HireDate = technician.HireDate,
            IsActive = technician.IsActive,
            CurrentWorkload = technician.CurrentWorkload,
            MaxWorkload = technician.MaxWorkload,
            SkillsCount = skillAssignments.Count(),
            AverageRating = technician.AverageRating,
            CompletedWorkOrders = technician.CompletedWorkOrders,
            LastSyncDate = technician.LastSyncDate
        };
    }


    #endregion

    #region Missing Interface Methods Implementation

    public async Task<TechnicianDto> CreateTechnicianAsync(CreateTechnicianDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating technician: {EmployeeId}", createDto.EmployeeId);

            // Create Employee entity with maintenance-specific properties
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeNumber = createDto.EmployeeNumber,
                FirstName = createDto.FirstName,
                LastName = createDto.LastName,
                EmailAddress = createDto.Email,
                MobileNumber = createDto.Phone,
                // Note: Department and Position should be set by ID in a real implementation
                // For now, we'll leave these as required fields that need to be set
                DepartmentId = Guid.NewGuid(), // This should come from a lookup by department name
                PositionId = Guid.NewGuid(),   // This should come from a lookup by position title
                Specialization = createDto.Specialization,
                CertificationLevel = createDto.CertificationLevel,
                ExperienceLevel = createDto.ExperienceLevel,
                DateEmployed = createDto.HireDate.HasValue ? DateOnly.FromDateTime(createDto.HireDate.Value) : DateOnly.FromDateTime(DateTime.UtcNow),
                IsActive = createDto.IsActive,
                MaxWorkload = createDto.MaxWorkload > 0 ? createDto.MaxWorkload : 100m,
                Notes = createDto.Notes,
                CreatedById = _currentUserProvider.UserId,
                TenantId = _currentUserProvider.TenantId
            };

            await _technicianRepository.AddAsync(employee);
            
            _logger.LogInformation("Created technician {TechnicianId} successfully", employee.Id);
            
            return await MapToDto(employee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating technician: {EmployeeId}", createDto.EmployeeId);
            throw;
        }
    }

    public async Task<TechnicianDto> UpdateTechnicianAsync(Guid id, UpdateTechnicianDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating technician: {TechnicianId}", id);

            var employee = await _technicianRepository.GetByIdAsync(id);
            if (employee == null)
                throw new ArgumentException($"Technician with ID {id} not found");

            employee.FirstName = updateDto.FirstName;
            employee.LastName = updateDto.LastName;
            employee.EmailAddress = updateDto.Email;
            employee.MobileNumber = updateDto.Phone;
            // Note: Department and Position updates would require lookups by name to get IDs
            // For now, we'll skip these updates to avoid errors
            employee.Specialization = updateDto.Specialization;
            employee.CertificationLevel = updateDto.CertificationLevel;
            employee.ExperienceLevel = updateDto.ExperienceLevel;
            employee.IsActive = updateDto.IsActive;
            employee.MaxWorkload = updateDto.MaxWorkload ?? employee.MaxWorkload;
            employee.Notes = updateDto.Notes;
            employee.LastModifiedById = _currentUserProvider.UserId;

            await _technicianRepository.UpdateAsync(employee);
            
            _logger.LogInformation("Updated technician {TechnicianId} successfully", id);
            
            return await MapToDto(employee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating technician: {TechnicianId}", id);
            throw;
        }
    }

    public async Task DeleteTechnicianAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting technician: {TechnicianId}", id);

            var technician = await _technicianRepository.GetByIdAsync(id);
            if (technician == null)
                throw new ArgumentException($"Technician with ID {id} not found");

            await _technicianRepository.DeleteAsync(id);
            
            _logger.LogInformation("Deleted technician {TechnicianId} successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting technician: {TechnicianId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetTechniciansBySkillAsync(Guid skillId)
    {
        try
        {
            // Use the repository method that directly returns technicians by skill
            var technicians = await _technicianRepository.GetTechniciansBySkillAsync(skillId);
            
            var result = new List<TechnicianDto>();
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technicians by skill: {SkillId}", skillId);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetAvailableTechniciansAsync(DateTime startTime, DateTime endTime)
    {
        try
        {
            var technicians = await _technicianRepository.GetAvailableAsync(startTime, endTime);
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available technicians from {StartTime} to {EndTime}", startTime, endTime);
            throw;
        }
    }

    public async Task<TechnicianDto?> GetTechnicianWithSkillsAsync(Guid technicianId)
    {
        try
        {
            var technician = await _technicianRepository.GetTechnicianWithSkillsAsync(technicianId);
            return technician != null ? await MapToDto(technician) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician with skills: {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startTime, DateTime endTime)
    {
        try
        {
            return await _technicianRepository.IsTechnicianAvailableAsync(technicianId, startTime, endTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking technician availability: {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetTechniciansByTeamAsync(Guid teamId)
    {
        try
        {
            var technicians = await _technicianRepository.GetTechniciansByTeamAsync(teamId);
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technicians by team: {TeamId}", teamId);
            throw;
        }
    }

    public async Task<TechnicianWorkloadDto> GetTechnicianWorkloadAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var workloadValue = await _technicianRepository.GetTechnicianWorkloadAsync(technicianId, startDate, endDate);
            return new TechnicianWorkloadDto
            {
                TechnicianId = technicianId,
                StartDate = startDate,
                EndDate = endDate,
                TotalHours = workloadValue,
                ScheduledHours = workloadValue,
                AvailableHours = 40, // Default 40 hour work week
                UtilizationPercentage = workloadValue > 0 ? (workloadValue / 40.0) * 100 : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician workload: {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<TechnicianAvailabilityDto> GetTechnicianAvailabilityAsync(Guid technicianId, DateTime date)
    {
        try
        {
            // Check if technician exists and is active
            var technician = await _technicianRepository.GetByIdAsync(technicianId);
            if (technician == null || !technician.IsActive)
            {
                return new TechnicianAvailabilityDto
                {
                    TechnicianId = technicianId,
                    Date = date,
                    IsAvailable = false,
                    AvailableHours = 0,
                    ScheduledHours = 0,
                    RemainingHours = 0
                };
            }

            // For now, return default availability - this would normally check schedules/assignments
            return new TechnicianAvailabilityDto
            {
                TechnicianId = technicianId,
                Date = date,
                IsAvailable = true,
                AvailableHours = 8.0, // Default 8 hour day
                ScheduledHours = 0,
                RemainingHours = 8.0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician availability: {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> GetTechniciansByLocationAsync(Guid locationId)
    {
        try
        {
            var technicians = await _technicianRepository.GetTechniciansByLocationAsync(locationId);
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technicians by location: {LocationId}", locationId);
            throw;
        }
    }

    public async Task<TechnicianAnalyticsDto> GetTechnicianAnalyticsAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Get technician details
            var technician = await _technicianRepository.GetByIdAsync(technicianId);
            if (technician == null)
            {
                throw new ArgumentException($"Technician with ID {technicianId} not found");
            }

            // For now, return basic analytics - this would normally aggregate from work orders
            return new TechnicianAnalyticsDto
            {
                TechnicianId = technicianId,
                TechnicianName = $"{technician.FirstName} {technician.LastName}",
                StartDate = startDate,
                EndDate = endDate,
                AnalysisPeriodStart = startDate,
                AnalysisPeriodEnd = endDate,
                TotalWorkOrders = 0, // Would be calculated from actual work orders
                CompletedWorkOrders = 0,
                AverageCompletionTime = 0,
                EfficiencyRating = 0,
                TotalHoursWorked = 0,
                TotalHours = 0,
                UtilizationRate = 0,
                AverageTimePerOrder = 0,
                ComplianceRate = 100 // Default to full compliance
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician analytics: {TechnicianId}", technicianId);
            throw;
        }
    }

    public async Task<SkillUtilizationDto> GetSkillUtilizationAsync(Guid skillId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Get skill assignments to calculate utilization
            var assignments = await _skillAssignmentRepository.GetBySkillIdAsync(skillId);
            var techniciansWithSkill = assignments.Count();
            
            // For now, return basic utilization data - this would normally aggregate from work orders
            return new SkillUtilizationDto
            {
                Skill = $"Skill-{skillId}", // Would normally get actual skill name
                TechniciansWithSkill = techniciansWithSkill,
                WorkOrdersRequiringSkill = 0, // Would be calculated from actual work orders
                UtilizationPercentage = techniciansWithSkill > 0 ? 50.0m : 0m // Default 50% utilization
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting skill utilization: {SkillId}", skillId);
            throw;
        }
    }

    public async Task<IEnumerable<TechnicianDto>> FindTechniciansForWorkOrderAsync(Guid workOrderId)
    {
        try
        {
            // For now, return all active technicians - this would normally filter by skills, availability, etc.
            var technicians = await _technicianRepository.GetActiveTechniciansAsync();
            var result = new List<TechnicianDto>();
            
            foreach (var technician in technicians)
            {
                result.Add(await MapToDto(technician));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding technicians for work order: {WorkOrderId}", workOrderId);
            throw;
        }
    }

    #endregion
}
