using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IJobArchitectureService
{
    // Families
    Task<IEnumerable<JobFamilyDto>> GetFamiliesAsync(CancellationToken ct = default);
    Task<IEnumerable<JobFamilyDto>> GetActiveFamiliesAsync(CancellationToken ct = default);
    Task<JobFamilyDto> CreateFamilyAsync(CreateJobFamilyDto dto, CancellationToken ct = default);
    Task<JobFamilyDto> UpdateFamilyAsync(UpdateJobFamilyDto dto, CancellationToken ct = default);
    Task<bool> DeleteFamilyAsync(Guid id, CancellationToken ct = default);

    // Sub-families
    Task<IEnumerable<JobSubFamilyDto>> GetSubFamiliesAsync(Guid familyId, CancellationToken ct = default);
    Task<IEnumerable<JobSubFamilyDto>> GetActiveSubFamiliesAsync(CancellationToken ct = default);
    Task<JobSubFamilyDto> CreateSubFamilyAsync(CreateJobSubFamilyDto dto, CancellationToken ct = default);
    Task<JobSubFamilyDto> UpdateSubFamilyAsync(UpdateJobSubFamilyDto dto, CancellationToken ct = default);
    Task<bool> DeleteSubFamilyAsync(Guid id, CancellationToken ct = default);

    // Levels
    Task<IEnumerable<JobLevelDto>> GetLevelsAsync(CancellationToken ct = default);
    Task<IEnumerable<JobLevelDto>> GetActiveLevelsAsync(CancellationToken ct = default);
    Task<JobLevelDto> CreateLevelAsync(CreateJobLevelDto dto, CancellationToken ct = default);
    Task<JobLevelDto> UpdateLevelAsync(UpdateJobLevelDto dto, CancellationToken ct = default);
    Task<bool> DeleteLevelAsync(Guid id, CancellationToken ct = default);
}
