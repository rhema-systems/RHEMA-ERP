using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

public static class JobArchitectureMappingExtensions
{
    #region JobFamily

    public static JobFamilyDto ToDto(this JobFamily e)
    {
        return new JobFamilyDto
        {
            Id = e.Id,
            TenantId = e.TenantId,
            Code = e.Code,
            Name = e.Name,
            Description = e.Description,
            IsActive = e.IsActive,
            SubFamilyCount = e.SubFamilies?.Count ?? 0,
            SubFamilies = e.SubFamilies?.Select(s => s.ToDto()).ToList() ?? new List<JobSubFamilyDto>(),
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? string.Empty,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy
        };
    }

    public static JobFamily ToEntity(this CreateJobFamilyDto dto) => new()
    {
        Code = dto.Code, Name = dto.Name, Description = dto.Description, IsActive = dto.IsActive
    };

    public static void UpdateEntity(this UpdateJobFamilyDto dto, JobFamily e)
    {
        e.Code = dto.Code; e.Name = dto.Name; e.Description = dto.Description; e.IsActive = dto.IsActive;
    }

    public static List<JobFamilyDto> ToDtoList(this IEnumerable<JobFamily> entities) => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobSubFamily

    public static JobSubFamilyDto ToDto(this JobSubFamily e)
    {
        return new JobSubFamilyDto
        {
            Id = e.Id,
            TenantId = e.TenantId,
            JobFamilyId = e.JobFamilyId,
            JobFamilyName = e.JobFamily?.Name,
            Code = e.Code,
            Name = e.Name,
            Description = e.Description,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? string.Empty,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy
        };
    }

    public static JobSubFamily ToEntity(this CreateJobSubFamilyDto dto) => new()
    {
        JobFamilyId = dto.JobFamilyId, Code = dto.Code, Name = dto.Name, Description = dto.Description, IsActive = dto.IsActive
    };

    public static void UpdateEntity(this UpdateJobSubFamilyDto dto, JobSubFamily e)
    {
        e.Code = dto.Code; e.Name = dto.Name; e.Description = dto.Description; e.IsActive = dto.IsActive;
    }

    public static List<JobSubFamilyDto> ToDtoList(this IEnumerable<JobSubFamily> entities) => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region JobLevel

    public static JobLevelDto ToDto(this CareerLevel e)
    {
        return new JobLevelDto
        {
            Id = e.Id,
            TenantId = e.TenantId,
            Code = e.Code,
            Name = e.Name,
            Rank = e.Rank,
            Description = e.Description,
            SalaryGradeId = e.SalaryGradeId,
            SalaryGradeName = e.SalaryGrade?.Name,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? string.Empty,
            UpdatedAt = e.UpdatedAt,
            UpdatedBy = e.UpdatedBy
        };
    }

    public static CareerLevel ToEntity(this CreateJobLevelDto dto) => new()
    {
        Code = dto.Code, Name = dto.Name, Rank = dto.Rank, Description = dto.Description, SalaryGradeId = dto.SalaryGradeId, IsActive = dto.IsActive
    };

    public static void UpdateEntity(this UpdateJobLevelDto dto, CareerLevel e)
    {
        e.Code = dto.Code; e.Name = dto.Name; e.Rank = dto.Rank; e.Description = dto.Description; e.SalaryGradeId = dto.SalaryGradeId; e.IsActive = dto.IsActive;
    }

    public static List<JobLevelDto> ToDtoList(this IEnumerable<CareerLevel> entities) => entities.Select(e => e.ToDto()).ToList();

    #endregion
}
