using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Maintenance;

#region Maintenance Scheduling Repository Implementation

public class MaintenanceScheduleRepository : GenericRepository<MaintenanceSchedule>, IMaintenanceScheduleRepository
{
    public MaintenanceScheduleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByAssetIdAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new ArgumentException("Asset ID cannot be empty", nameof(assetId));
            
        return await _dbSet
            .Where(ms => ms.AssetId == assetId && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetActiveSchedulesAsync()
    {
        return await _dbSet
            .Where(ms => ms.IsActive && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesDueForGenerationAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(ms => ms.IsActive && !ms.IsDeleted &&
                        ms.NextDueDate <= today)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByScheduleTypeAsync(string scheduleType)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
            throw new ArgumentException("Schedule type cannot be null or empty", nameof(scheduleType));
            
        return await _dbSet
            .Where(ms => ms.ScheduleType == scheduleType && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByFrequencyAsync(string frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
            throw new ArgumentException("Frequency cannot be null or empty", nameof(frequency));
            
        return await _dbSet
            .Where(ms => ms.Frequency == frequency && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetByMaintenanceTypeAsync(Guid maintenanceTypeId)
    {
        if (maintenanceTypeId == Guid.Empty)
            throw new ArgumentException("Maintenance Type ID cannot be empty", nameof(maintenanceTypeId));
            
        return await _dbSet
            .Where(ms => ms.MaintenanceTypeId == maintenanceTypeId && !ms.IsDeleted)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesDueInDaysAsync(int days)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(days);
        return await _dbSet
            .Where(ms => ms.IsActive && !ms.IsDeleted &&
                        ms.NextDueDate <= targetDate)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }

    public async Task UpdateNextDueDateAsync(Guid scheduleId, DateTime nextDueDate)
    {
        if (scheduleId == Guid.Empty)
            throw new ArgumentException("Schedule ID cannot be empty", nameof(scheduleId));
            
        var schedule = await GetByIdAsync(scheduleId);
        if (schedule != null)
        {
            schedule.NextDueDate = nextDueDate;
            schedule.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(schedule);
        }
    }

    public async Task UpdateLastGeneratedDateAsync(Guid scheduleId, DateTime lastGeneratedDate)
    {
        if (scheduleId == Guid.Empty)
            throw new ArgumentException("Schedule ID cannot be empty", nameof(scheduleId));
            
        var schedule = await GetByIdAsync(scheduleId);
        if (schedule != null)
        {
            schedule.LastGeneratedDate = lastGeneratedDate;
            schedule.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(schedule);
        }
    }
    
    /// <summary>
    /// Gets all schedules that should generate work orders based on their frequency and next due date
    /// </summary>
    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesForWorkOrderGenerationAsync()
    {
        var today = DateTime.UtcNow.Date;
        
        return await _dbSet
            .Where(ms => ms.IsActive && !ms.IsDeleted &&
                        ms.NextDueDate <= today &&
                        (ms.LastGeneratedDate == null || 
                         ms.LastGeneratedDate < ms.NextDueDate))
            .Include(ms => ms.Asset)
                .ThenInclude(a => a!.AssetCategory)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ThenBy(ms => ms.Asset!.AssetNumber)
            .ToListAsync();
    }
    
    /// <summary>
    /// Calculates the next due date based on the schedule's frequency and current due date
    /// </summary>
    public DateTime CalculateNextDueDate(MaintenanceSchedule schedule)
    {
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));
            
        var baseDate = schedule.NextDueDate ?? DateTime.UtcNow.Date;
        
        return schedule.Frequency?.ToLower() switch
        {
            "daily" => baseDate.AddDays(schedule.IntervalValue),
            "weekly" => baseDate.AddDays(schedule.IntervalValue * 7),
            "monthly" => baseDate.AddMonths(schedule.IntervalValue),
            "quarterly" => baseDate.AddMonths(schedule.IntervalValue * 3),
            "yearly" => baseDate.AddYears(schedule.IntervalValue),
            "hours" => baseDate.AddHours(schedule.IntervalValue),
            "days" => baseDate.AddDays(schedule.IntervalValue),
            _ => baseDate.AddDays(30) // Default to 30 days if frequency is unknown
        };
    }
    
    /// <summary>
    /// Gets maintenance schedules by asset criticality level
    /// </summary>
    public async Task<IEnumerable<MaintenanceSchedule>> GetByCriticalityAsync(string criticality)
    {
        if (string.IsNullOrWhiteSpace(criticality))
            throw new ArgumentException("Criticality cannot be null or empty", nameof(criticality));
            
        return await _dbSet
            .Where(ms => !ms.IsDeleted && 
                        ms.Asset != null && 
                        ms.Asset.Criticality.ToString() == criticality)
            .Include(ms => ms.Asset)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets overdue maintenance schedules
    /// </summary>
    public async Task<IEnumerable<MaintenanceSchedule>> GetOverdueSchedulesAsync()
    {
        var today = DateTime.UtcNow.Date;
        
        return await _dbSet
            .Where(ms => ms.IsActive && !ms.IsDeleted &&
                        ms.NextDueDate < today)
            .Include(ms => ms.Asset)
                .ThenInclude(a => a!.AssetCategory)
            .Include(ms => ms.MaintenanceType)
            .OrderBy(ms => ms.NextDueDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Validates business rules for maintenance schedule
    /// </summary>
    public async Task<(bool IsValid, string[] ValidationErrors)> ValidateScheduleAsync(MaintenanceSchedule schedule)
    {
        var errors = new List<string>();
        
        try
        {
            if (schedule == null)
            {
                errors.Add("Schedule cannot be null");
                return (false, errors.ToArray());
            }
            
            // Basic validation
            if (schedule.AssetId == Guid.Empty)
                errors.Add("Valid asset ID is required");
                
            if (schedule.MaintenanceTypeId == Guid.Empty)
                errors.Add("Valid maintenance type ID is required");
                
            if (string.IsNullOrWhiteSpace(schedule.Frequency))
                errors.Add("Frequency is required");
                
            if (schedule.IntervalValue <= 0)
                errors.Add("Interval value must be greater than 0");
                
            if (string.IsNullOrWhiteSpace(schedule.ScheduleType))
                errors.Add("Schedule type is required");
                
            // Business rule validation
            var validFrequencies = new[] { "daily", "weekly", "monthly", "quarterly", "yearly", "hours", "days" };
            if (!string.IsNullOrWhiteSpace(schedule.Frequency) && 
                !validFrequencies.Contains(schedule.Frequency.ToLower()))
                errors.Add($"Invalid frequency. Must be one of: {string.Join(", ", validFrequencies)}");
                
            var validScheduleTypes = new[] { "Preventive", "Predictive", "Condition-Based", "Time-Based" };
            if (!string.IsNullOrWhiteSpace(schedule.ScheduleType) && 
                !validScheduleTypes.Contains(schedule.ScheduleType))
                errors.Add($"Invalid schedule type. Must be one of: {string.Join(", ", validScheduleTypes)}");
                
            // Check for duplicate schedules on the same asset with the same maintenance type
            var duplicateExists = await _dbSet
                .Where(s => s.AssetId == schedule.AssetId && 
                           s.MaintenanceTypeId == schedule.MaintenanceTypeId &&
                           s.Id != schedule.Id && 
                           !s.IsDeleted)
                .AnyAsync();
                
            if (duplicateExists)
                errors.Add("A schedule with the same maintenance type already exists for this asset");
                
            return (errors.Count == 0, errors.ToArray());
        }
        catch (Exception)
        {
            errors.Add("Validation failed due to system error");
            return (false, errors.ToArray());
        }
    }
    
    /// <summary>
    /// Overridden Add method with validation
    /// </summary>
    public override async Task<MaintenanceSchedule> AddAsync(MaintenanceSchedule entity)
    {
        var validation = await ValidateScheduleAsync(entity);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Schedule validation failed: {string.Join(", ", validation.ValidationErrors)}");
        }
        
        return await base.AddAsync(entity);
    }
    
    /// <summary>
    /// Overridden Update method with validation
    /// </summary>
    public override async Task UpdateAsync(MaintenanceSchedule entity)
    {
        var validation = await ValidateScheduleAsync(entity);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Schedule validation failed: {string.Join(", ", validation.ValidationErrors)}");
        }
        
        await base.UpdateAsync(entity);
    }
}

#endregion