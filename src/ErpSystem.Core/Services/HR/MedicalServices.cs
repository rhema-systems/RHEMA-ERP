using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// HEALTHCARE FACILITY SERVICE
// ============================================================================

#region Healthcare Facility Service

public class HealthcareFacilityService : IHealthcareFacilityService
{
    private readonly IHealthcareFacilityRepository _facilityRepository;
    private readonly IPhysicianRepository _physicianRepository;
    private readonly IFacilityServiceRepository _facilityServiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HealthcareFacilityService> _logger;

    public HealthcareFacilityService(
        IHealthcareFacilityRepository facilityRepository,
        IPhysicianRepository physicianRepository,
        IFacilityServiceRepository facilityServiceRepository,
        IUnitOfWork unitOfWork,
        ILogger<HealthcareFacilityService> logger)
    {
        _facilityRepository = facilityRepository;
        _physicianRepository = physicianRepository;
        _facilityServiceRepository = facilityServiceRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<HealthcareFacilityDto> GetFacilityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Healthcare facility with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<HealthcareFacilityDetailDto> GetFacilityWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Healthcare facility with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<HealthcareFacilityDto?> GetFacilityByCodeAsync(string facilityCode, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityRepository.GetByFacilityCodeAsync(facilityCode);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<HealthcareFacilitySummaryDto>> GetAllFacilitiesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _facilityRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<HealthcareFacilitySummaryDto>> GetActiveFacilitiesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _facilityRepository.GetActiveFacilitiesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<HealthcareFacilitySummaryDto>> GetFacilitiesByTypeAsync(HealthFacilityType facilityType, CancellationToken cancellationToken = default)
    {
        var entities = await _facilityRepository.GetByFacilityTypeAsync(facilityType);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<HealthcareFacilitySummaryDto>> GetFacilitiesAcceptingNHISAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _facilityRepository.GetFacilitiesAcceptingNHISAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<HealthcareFacilitySummaryDto>> SearchFacilitiesAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var entities = await _facilityRepository.SearchFacilitiesAsync(searchTerm);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<HealthcareFacilitySummaryDto>> GetFacilitiesPagedAsync(int pageNumber, int pageSize, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _facilityRepository.GetQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(f =>
                f.FacilityName.ToLower().Contains(term) ||
                f.FacilityCode.ToLower().Contains(term) ||
                (f.City != null && f.City.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(f => f.FacilityName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<HealthcareFacilitySummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<HealthcareFacilityDto> CreateFacilityAsync(CreateHealthcareFacilityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _facilityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Healthcare facility created: {FacilityCode}", entity.FacilityCode);

        return entity.ToDto();
    }

    public async Task<HealthcareFacilityDto> UpdateFacilityAsync(UpdateHealthcareFacilityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Healthcare facility with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _facilityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Healthcare facility updated: {FacilityCode}", entity.FacilityCode);

        return entity.ToDto();
    }

    public async Task<bool> DeleteFacilityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Healthcare facility with ID '{id}' not found.");

        await _facilityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Healthcare facility deleted: {FacilityCode}", entity.FacilityCode);

        return true;
    }

    public async Task<PhysicianDto> GetPhysicianByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _physicianRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Physician with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<PhysicianSummaryDto>> GetAllPhysiciansAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _physicianRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<PhysicianSummaryDto>> GetPhysiciansByFacilityAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        var entities = await _physicianRepository.GetByFacilityIdAsync(facilityId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<PhysicianSummaryDto>> SearchPhysiciansAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var entities = await _physicianRepository.SearchPhysiciansAsync(searchTerm);
        return entities.ToSummaryDtoList();
    }

    public async Task<PhysicianDto> CreatePhysicianAsync(CreatePhysicianDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _physicianRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Physician created: {PhysicianId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<PhysicianDto> UpdatePhysicianAsync(UpdatePhysicianDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _physicianRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Physician with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _physicianRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> VerifyPhysicianAsync(VerifyPhysicianDto verifyDto, CancellationToken cancellationToken = default)
    {
        var entity = await _physicianRepository.GetByIdAsync(verifyDto.PhysicianId);

        if (entity == null)
            throw new ArgumentException($"Physician with ID '{verifyDto.PhysicianId}' not found.");

        verifyDto.ApplyTo(entity);

        await _physicianRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Physician verified: {PhysicianId}", entity.Id);

        return true;
    }

    public async Task<bool> DeletePhysicianAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _physicianRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Physician with ID '{id}' not found.");

        await _physicianRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<FacilityServiceDto> GetFacilityServiceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityServiceRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Facility service with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<FacilityServiceDto>> GetFacilityServicesAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        var entities = await _facilityServiceRepository.GetByFacilityIdAsync(facilityId);
        return entities.ToDtoList();
    }

    public async Task<FacilityServiceDto> CreateFacilityServiceAsync(CreateFacilityServiceDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _facilityServiceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<FacilityServiceDto> UpdateFacilityServiceAsync(UpdateFacilityServiceDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityServiceRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Facility service with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _facilityServiceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteFacilityServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _facilityServiceRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Facility service with ID '{id}' not found.");

        await _facilityServiceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// MEDICAL INSURANCE SERVICE
// ============================================================================

#region Medical Insurance Service

public class MedicalInsuranceService : IMedicalInsuranceService
{
    private readonly IMedicalInsuranceProviderRepository _providerRepository;
    private readonly IMedicalInsurancePlanRepository _planRepository;
    private readonly IEmployeeMedicalInsurancePolicyRepository _policyRepository;
    private readonly IMedicalInsurancePolicyDependentRepository _policyDependentRepository;
    private readonly IMedicalInsuranceClaimRepository _insuranceClaimRepository;
    private readonly IMedicalInsuranceProviderFacilityRepository _networkFacilityRepository;
    private readonly IMedicalInsuranceProviderDocumentRepository _providerDocumentRepository;
    private readonly IMedicalInsurancePremiumRecordRepository _premiumRecordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MedicalInsuranceService> _logger;

    public MedicalInsuranceService(
        IMedicalInsuranceProviderRepository providerRepository,
        IMedicalInsurancePlanRepository planRepository,
        IEmployeeMedicalInsurancePolicyRepository policyRepository,
        IMedicalInsurancePolicyDependentRepository policyDependentRepository,
        IMedicalInsuranceClaimRepository insuranceClaimRepository,
        IMedicalInsuranceProviderFacilityRepository networkFacilityRepository,
        IMedicalInsuranceProviderDocumentRepository providerDocumentRepository,
        IMedicalInsurancePremiumRecordRepository premiumRecordRepository,
        IUnitOfWork unitOfWork,
        ILogger<MedicalInsuranceService> logger)
    {
        _providerRepository = providerRepository;
        _planRepository = planRepository;
        _policyRepository = policyRepository;
        _policyDependentRepository = policyDependentRepository;
        _insuranceClaimRepository = insuranceClaimRepository;
        _networkFacilityRepository = networkFacilityRepository;
        _providerDocumentRepository = providerDocumentRepository;
        _premiumRecordRepository = premiumRecordRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<MedicalInsuranceProviderDto> GetProviderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _providerRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance provider with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<MedicalInsuranceProviderDetailDto> GetProviderWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _providerRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance provider with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<MedicalInsuranceProviderDto?> GetProviderByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _providerRepository.GetByCodeAsync(code);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> GetAllProvidersAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _providerRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> GetProvidersByTypeAsync(MedicalInsuranceProviderType providerType, CancellationToken cancellationToken = default)
    {
        var entities = await _providerRepository.GetByProviderTypeAsync(providerType);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> GetActiveProvidersAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _providerRepository.GetActiveProvidersAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> SearchProvidersAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var entities = await _providerRepository.SearchProvidersAsync(searchTerm);
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalInsuranceProviderDto> CreateProviderAsync(CreateMedicalInsuranceProviderDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _providerRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical insurance provider created: {ProviderCode}", entity.Code);

        return entity.ToDto();
    }

    public async Task<MedicalInsuranceProviderDto> UpdateProviderAsync(UpdateMedicalInsuranceProviderDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _providerRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance provider with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _providerRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _providerRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance provider with ID '{id}' not found.");

        await _providerRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalInsurancePlanDto> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance plan with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalInsurancePlanDto>> GetPlansByProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByProviderIdAsync(providerId);
        return entities.ToDtoList();
    }

    public async Task<MedicalInsurancePlanDto> CreatePlanAsync(CreateMedicalInsurancePlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<MedicalInsurancePlanDto> UpdatePlanAsync(UpdateMedicalInsurancePlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance plan with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance plan with ID '{id}' not found.");

        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeMedicalInsurancePolicyDto> GetPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee medical insurance policy with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<EmployeeMedicalInsurancePolicyDetailDto> GetPolicyWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee medical insurance policy with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _policyRepository.GetQueryable()
            .Include(p => p.Employee)
            .Include(p => p.MedicalInsuranceProvider)
            .Include(p => p.MedicalInsurancePlan)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>> GetPoliciesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _policyRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeMedicalInsurancePolicyDto?> GetActivePolicyForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetActivePolicyAsync(employeeId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>> GetExpiringPoliciesAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _policyRepository.GetExpiringPoliciesAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeMedicalInsurancePolicyDto> CreatePolicyAsync(CreateEmployeeMedicalInsurancePolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _policyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee medical insurance policy created: {PolicyNumber}", entity.PolicyNumber);

        return entity.ToDto();
    }

    public async Task<EmployeeMedicalInsurancePolicyDto> UpdatePolicyAsync(UpdateEmployeeMedicalInsurancePolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Employee medical insurance policy with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _policyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CancelPolicyAsync(CancelEmployeeMedicalInsurancePolicyDto cancelDto, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetByIdAsync(cancelDto.PolicyId);

        if (entity == null)
            throw new ArgumentException($"Employee medical insurance policy with ID '{cancelDto.PolicyId}' not found.");

        cancelDto.ApplyTo(entity);

        await _policyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee medical insurance policy cancelled: {PolicyNumber}", entity.PolicyNumber);

        return true;
    }

    public async Task<bool> DeletePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _policyRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee medical insurance policy with ID '{id}' not found.");

        await _policyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalInsurancePolicyDependentDto> AddPolicyDependentAsync(AddMedicalInsurancePolicyDependentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _policyDependentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalInsurancePolicyDependentDto>> GetPolicyDependentsAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        var entities = await _policyDependentRepository.GetByPolicyIdAsync(policyId);
        return entities.ToDtoList();
    }

    public async Task<MedicalInsurancePolicyDependentDto> UpdatePolicyDependentAsync(UpdateMedicalInsurancePolicyDependentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _policyDependentRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance policy dependent with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _policyDependentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeletePolicyDependentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _policyDependentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance policy dependent with ID '{id}' not found.");

        await _policyDependentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalInsuranceClaimDto> GetInsuranceClaimByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _insuranceClaimRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical insurance claim with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalInsuranceClaimSummaryDto>> GetInsuranceClaimsByPolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        var entities = await _insuranceClaimRepository.GetByPolicyIdAsync(policyId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalInsuranceClaimSummaryDto>> GetInsuranceClaimsByExpenseClaimAsync(Guid medicalExpenseClaimId, CancellationToken cancellationToken = default)
    {
        var entities = await _insuranceClaimRepository.GetByMedicalExpenseClaimIdAsync(medicalExpenseClaimId);
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalInsuranceClaimDto> CreateInsuranceClaimAsync(CreateMedicalInsuranceClaimDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        if (string.IsNullOrWhiteSpace(entity.InsuranceClaimNumber))
            entity.InsuranceClaimNumber = $"IC-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        await _insuranceClaimRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical insurance claim created: {InsuranceClaimNumber}", entity.InsuranceClaimNumber);

        return entity.ToDto();
    }

    public async Task<MedicalInsuranceClaimDto> UpdateInsuranceClaimStatusAsync(UpdateMedicalInsuranceClaimStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await _insuranceClaimRepository.GetByIdAsync(statusDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"Medical insurance claim with ID '{statusDto.ClaimId}' not found.");

        statusDto.ApplyTo(entity);

        await _insuranceClaimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical insurance claim status updated: {InsuranceClaimNumber}, Status: {Status}", entity.InsuranceClaimNumber, entity.Status);

        return entity.ToDto();
    }

    public async Task<MedicalInsuranceClaimDto> RecordInsuranceClaimPaymentAsync(RecordMedicalInsuranceClaimPaymentDto paymentDto, CancellationToken cancellationToken = default)
    {
        var entity = await _insuranceClaimRepository.GetByIdAsync(paymentDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"Medical insurance claim with ID '{paymentDto.ClaimId}' not found.");

        paymentDto.ApplyTo(entity);

        await _insuranceClaimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical insurance claim payment recorded: {InsuranceClaimNumber}", entity.InsuranceClaimNumber);

        return entity.ToDto();
    }

    public async Task<MedicalInsuranceProviderFacilityDto> AddNetworkFacilityAsync(AddMedicalInsuranceProviderFacilityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _networkFacilityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderFacilityDto>> GetNetworkFacilitiesByProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var entities = await _networkFacilityRepository.GetByProviderIdAsync(providerId);
        return entities.ToDtoList();
    }

    public async Task<MedicalInsuranceProviderFacilityDto> UpdateNetworkFacilityAsync(UpdateMedicalInsuranceProviderFacilityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _networkFacilityRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Network facility with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _networkFacilityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveNetworkFacilityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _networkFacilityRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Network facility with ID '{id}' not found.");

        await _networkFacilityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> IsFacilityInNetworkAsync(Guid providerId, Guid facilityId, CancellationToken cancellationToken = default)
    {
        return await _networkFacilityRepository.IsFacilityInNetworkAsync(providerId, facilityId);
    }

    public async Task<MedicalInsuranceProviderDocumentDto> AddProviderDocumentAsync(CreateMedicalInsuranceProviderDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _providerDocumentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalInsuranceProviderDocumentDto>> GetProviderDocumentsAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var entities = await _providerDocumentRepository.GetByProviderIdAsync(providerId);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteProviderDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _providerDocumentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Provider document with ID '{id}' not found.");

        await _providerDocumentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalInsurancePremiumRecordDto> GetPremiumRecordByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _premiumRecordRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Premium record with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalInsurancePremiumRecordSummaryDto>> GetPremiumRecordsByProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var entities = await _premiumRecordRepository.GetByProviderIdAsync(providerId);
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalInsurancePremiumRecordDto> CreatePremiumRecordAsync(CreateMedicalInsurancePremiumRecordDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _premiumRecordRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<MedicalInsurancePremiumRecordDto> RecordPremiumPaymentAsync(RecordMedicalInsurancePremiumPaymentDto paymentDto, CancellationToken cancellationToken = default)
    {
        var entity = await _premiumRecordRepository.GetByIdAsync(paymentDto.PremiumRecordId);

        if (entity == null)
            throw new ArgumentException($"Premium record with ID '{paymentDto.PremiumRecordId}' not found.");

        paymentDto.ApplyTo(entity);

        await _premiumRecordRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Premium payment recorded for record: {PremiumRecordId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalInsurancePremiumRecordSummaryDto>> GetOverduePremiumsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _premiumRecordRepository.GetOverduePremiumsAsync();
        return entities.ToSummaryDtoList();
    }
}

#endregion

// ============================================================================
// MEDICAL BENEFIT SCHEME SERVICE
// ============================================================================

#region Medical Benefit Scheme Service

public class MedicalBenefitSchemeService : IMedicalBenefitSchemeService
{
    private readonly IMedicalBenefitSchemeRepository _schemeRepository;
    private readonly IMedicalBenefitTierRepository _tierRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MedicalBenefitSchemeService> _logger;

    public MedicalBenefitSchemeService(
        IMedicalBenefitSchemeRepository schemeRepository,
        IMedicalBenefitTierRepository tierRepository,
        IUnitOfWork unitOfWork,
        ILogger<MedicalBenefitSchemeService> logger)
    {
        _schemeRepository = schemeRepository;
        _tierRepository = tierRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<MedicalBenefitSchemeDto> GetSchemeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _schemeRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical benefit scheme with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<MedicalBenefitSchemeDetailDto> GetSchemeWithTiersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _schemeRepository.GetWithTiersAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical benefit scheme with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<MedicalBenefitSchemeDto?> GetSchemeByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _schemeRepository.GetByCodeAsync(code);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<MedicalBenefitSchemeSummaryDto>> GetAllSchemesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _schemeRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalBenefitSchemeSummaryDto>> GetActiveSchemesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _schemeRepository.GetActiveSchemesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalBenefitSchemeDto> CreateSchemeAsync(CreateMedicalBenefitSchemeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _schemeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical benefit scheme created: {SchemeCode}", entity.Code);

        return entity.ToDto();
    }

    public async Task<MedicalBenefitSchemeDto> UpdateSchemeAsync(UpdateMedicalBenefitSchemeDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _schemeRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical benefit scheme with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _schemeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteSchemeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _schemeRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical benefit scheme with ID '{id}' not found.");

        await _schemeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalBenefitTierDto> GetTierByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _tierRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical benefit tier with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalBenefitTierDto>> GetTiersBySchemeAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        var entities = await _tierRepository.GetBySchemeIdAsync(schemeId);
        return entities.ToDtoList();
    }

    public async Task<MedicalBenefitTierDto> CreateTierAsync(CreateMedicalBenefitTierDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _tierRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<MedicalBenefitTierDto> UpdateTierAsync(UpdateMedicalBenefitTierDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _tierRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical benefit tier with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _tierRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteTierAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _tierRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical benefit tier with ID '{id}' not found.");

        await _tierRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// EMPLOYEE HEALTH SERVICE
// ============================================================================

#region Employee Health Service

public class EmployeeHealthService : IEmployeeHealthService
{
    private readonly IEmployeeHealthProfileRepository _profileRepository;
    private readonly IEmployeeHealthConditionRepository _conditionRepository;
    private readonly IEmployeeAllergyRepository _allergyRepository;
    private readonly IEmployeeMedicalExamRepository _examRepository;
    private readonly IEmployeeMedicalExamDocumentRepository _examDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeHealthService> _logger;

    public EmployeeHealthService(
        IEmployeeHealthProfileRepository profileRepository,
        IEmployeeHealthConditionRepository conditionRepository,
        IEmployeeAllergyRepository allergyRepository,
        IEmployeeMedicalExamRepository examRepository,
        IEmployeeMedicalExamDocumentRepository examDocumentRepository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeHealthService> logger)
    {
        _profileRepository = profileRepository;
        _conditionRepository = conditionRepository;
        _allergyRepository = allergyRepository;
        _examRepository = examRepository;
        _examDocumentRepository = examDocumentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<EmployeeHealthProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _profileRepository.GetQueryable()
            .Include(x => x.Employee)
            .Include(x => x.PreferredFacility)
            .Include(x => x.PreferredPhysician)
            .OrderBy(x => x.Employee != null ? x.Employee.FirstName : string.Empty)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToDto());
    }

    public async Task<EmployeeHealthProfileDto> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee health profile with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<EmployeeHealthProfileDetailDto> GetProfileWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee health profile with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<EmployeeHealthProfileDto?> GetProfileByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetByEmployeeIdAsync(employeeId);
        return entity?.ToDto();
    }

    public async Task<EmployeeHealthProfileDto> CreateProfileAsync(CreateEmployeeHealthProfileDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _profileRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee health profile created for employee: {EmployeeId}", entity.EmployeeId);

        return entity.ToDto();
    }

    public async Task<EmployeeHealthProfileDto> UpdateProfileAsync(UpdateEmployeeHealthProfileDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Employee health profile with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _profileRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _profileRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee health profile with ID '{id}' not found.");

        await _profileRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeHealthConditionDto> AddConditionAsync(CreateEmployeeHealthConditionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _conditionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeHealthConditionDto>> GetConditionsAsync(Guid healthProfileId, CancellationToken cancellationToken = default)
    {
        var entities = await _conditionRepository.GetByHealthProfileIdAsync(healthProfileId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<EmployeeHealthConditionDto>> GetActiveConditionsAsync(Guid healthProfileId, CancellationToken cancellationToken = default)
    {
        var entities = await _conditionRepository.GetActiveConditionsAsync(healthProfileId);
        return entities.ToDtoList();
    }

    public async Task<EmployeeHealthConditionDto> UpdateConditionAsync(UpdateEmployeeHealthConditionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _conditionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Employee health condition with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _conditionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteConditionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _conditionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee health condition with ID '{id}' not found.");

        await _conditionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeAllergyDto> AddAllergyAsync(CreateEmployeeAllergyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _allergyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeAllergyDto>> GetAllergiesAsync(Guid healthProfileId, CancellationToken cancellationToken = default)
    {
        var entities = await _allergyRepository.GetByHealthProfileIdAsync(healthProfileId);
        return entities.ToDtoList();
    }

    public async Task<EmployeeAllergyDto> UpdateAllergyAsync(UpdateEmployeeAllergyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _allergyRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Employee allergy with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _allergyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAllergyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _allergyRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee allergy with ID '{id}' not found.");

        await _allergyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeMedicalExamDto> GetExamByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _examRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee medical exam with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<EmployeeMedicalExamDetailDto> GetExamWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _examRepository.GetWithDocumentsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee medical exam with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeMedicalExamSummaryDto>> GetExamsByProfileAsync(Guid healthProfileId, CancellationToken cancellationToken = default)
    {
        var entities = await _examRepository.GetByHealthProfileIdAsync(healthProfileId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeMedicalExamSummaryDto>> GetExamsDueAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _examRepository.GetDueForExamAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeMedicalExamDto> CreateExamAsync(CreateEmployeeMedicalExamDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _examRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeMedicalExamDto> UpdateExamAsync(UpdateEmployeeMedicalExamDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _examRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Employee medical exam with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _examRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteExamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _examRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee medical exam with ID '{id}' not found.");

        await _examRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeMedicalExamDocumentDto> AddExamDocumentAsync(CreateEmployeeMedicalExamDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _examDocumentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeMedicalExamDocumentDto>> GetExamDocumentsAsync(Guid examId, CancellationToken cancellationToken = default)
    {
        var entities = await _examDocumentRepository.GetByExamIdAsync(examId);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteExamDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _examDocumentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Employee medical exam document with ID '{id}' not found.");

        await _examDocumentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// MEDICAL CLINICAL SERVICE
// ============================================================================

#region Medical Clinical Service

public class MedicalClinicalService : IMedicalClinicalService
{
    private readonly IMedicalClaimPreAuthorizationRepository _preAuthorizationRepository;
    private readonly IMedicalReferralRepository _referralRepository;
    private readonly IMedicalAppointmentRepository _appointmentRepository;
    private readonly IEmployeeMedicalInsurancePolicyRepository _policyRepository;
    private readonly IMedicalInsuranceProviderFacilityRepository _networkFacilityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MedicalClinicalService> _logger;

    public MedicalClinicalService(
        IMedicalClaimPreAuthorizationRepository preAuthorizationRepository,
        IMedicalReferralRepository referralRepository,
        IMedicalAppointmentRepository appointmentRepository,
        IEmployeeMedicalInsurancePolicyRepository policyRepository,
        IMedicalInsuranceProviderFacilityRepository networkFacilityRepository,
        IUnitOfWork unitOfWork,
        ILogger<MedicalClinicalService> logger)
    {
        _preAuthorizationRepository = preAuthorizationRepository;
        _referralRepository = referralRepository;
        _appointmentRepository = appointmentRepository;
        _policyRepository = policyRepository;
        _networkFacilityRepository = networkFacilityRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Rejects a facility that is outside the insurer's provider network for the given policy.
    /// Enforced only when the provider has a configured network; an unconfigured network is treated
    /// as open access so demo/early-setup data is not blocked.
    /// </summary>
    private async Task ValidateFacilityInNetworkAsync(Guid policyId, Guid? facilityId)
    {
        if (facilityId == null)
            return;

        var policy = await _policyRepository.GetByIdAsync(policyId);
        if (policy == null)
            return; // policy existence is validated by the FK on save

        var network = await _networkFacilityRepository.GetActiveNetworkFacilitiesAsync(policy.ProviderId);
        if (network == null || !network.Any())
            return; // no network configured → treat as open access

        var inNetwork = await _networkFacilityRepository.IsFacilityInNetworkAsync(policy.ProviderId, facilityId.Value);
        if (!inNetwork)
            throw new MedicalWorkflowException(
                MedicalWorkflowFailureReason.FacilityNotInNetwork,
                "The selected facility is not in the insurer's provider network for this policy.");
    }

    public async Task<MedicalClaimPreAuthorizationDto> GetPreAuthorizationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _preAuthorizationRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical claim pre-authorization with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<MedicalClaimPreAuthorizationDto?> GetPreAuthorizationByNumberAsync(string authorizationNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _preAuthorizationRepository.GetByAuthorizationNumberAsync(authorizationNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>> GetAllPreAuthorizationsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _preAuthorizationRepository.GetQueryable()
            .Include(x => x.Employee)
            .OrderByDescending(x => x.RequestDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>> GetPreAuthorizationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _preAuthorizationRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>> GetPendingPreAuthorizationsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _preAuthorizationRepository.GetPendingApprovalsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalClaimPreAuthorizationDto> CreatePreAuthorizationAsync(CreateMedicalClaimPreAuthorizationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.AuthorizationNumber = $"PA-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        await ValidateFacilityInNetworkAsync(entity.PolicyId, entity.FacilityId);

        await _preAuthorizationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical claim pre-authorization created: {AuthorizationNumber}", entity.AuthorizationNumber);

        return entity.ToDto();
    }

    public async Task<MedicalClaimPreAuthorizationDto> UpdatePreAuthorizationAsync(UpdateMedicalClaimPreAuthorizationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _preAuthorizationRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical claim pre-authorization with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _preAuthorizationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> ApprovePreAuthorizationAsync(ApproveMedicalClaimPreAuthorizationDto approveDto, CancellationToken cancellationToken = default)
    {
        var entity = await _preAuthorizationRepository.GetByIdAsync(approveDto.PreAuthorizationId);

        if (entity == null)
            throw new ArgumentException($"Medical claim pre-authorization with ID '{approveDto.PreAuthorizationId}' not found.");

        approveDto.ApplyTo(entity);

        await _preAuthorizationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical claim pre-authorization approved: {AuthorizationNumber}", entity.AuthorizationNumber);

        return true;
    }

    public async Task<bool> RejectPreAuthorizationAsync(RejectMedicalClaimPreAuthorizationDto rejectDto, CancellationToken cancellationToken = default)
    {
        var entity = await _preAuthorizationRepository.GetByIdAsync(rejectDto.PreAuthorizationId);

        if (entity == null)
            throw new ArgumentException($"Medical claim pre-authorization with ID '{rejectDto.PreAuthorizationId}' not found.");

        rejectDto.ApplyTo(entity);

        await _preAuthorizationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical claim pre-authorization rejected: {AuthorizationNumber}", entity.AuthorizationNumber);

        return true;
    }

    public async Task<bool> DeletePreAuthorizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _preAuthorizationRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical claim pre-authorization with ID '{id}' not found.");

        await _preAuthorizationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalReferralDto> GetReferralByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _referralRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical referral with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<MedicalReferralDto?> GetReferralByNumberAsync(string referralNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _referralRepository.GetByReferralNumberAsync(referralNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<MedicalReferralSummaryDto>> GetAllReferralsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _referralRepository.GetQueryable()
            .Include(x => x.Employee)
            .OrderByDescending(x => x.ReferralDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalReferralSummaryDto>> GetReferralsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _referralRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalReferralSummaryDto>> GetPendingReferralsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _referralRepository.GetPendingReferralsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalReferralDto> CreateReferralAsync(CreateMedicalReferralDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ReferralNumber = $"RF-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        await _referralRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical referral created: {ReferralNumber}", entity.ReferralNumber);

        return entity.ToDto();
    }

    public async Task<MedicalReferralDto> UpdateReferralAsync(UpdateMedicalReferralDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _referralRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical referral with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _referralRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> UpdateReferralStatusAsync(UpdateMedicalReferralStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await _referralRepository.GetByIdAsync(statusDto.ReferralId);

        if (entity == null)
            throw new ArgumentException($"Medical referral with ID '{statusDto.ReferralId}' not found.");

        statusDto.ApplyTo(entity);

        await _referralRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> CompleteReferralAsync(CompleteMedicalReferralDto completeDto, CancellationToken cancellationToken = default)
    {
        var entity = await _referralRepository.GetByIdAsync(completeDto.ReferralId);

        if (entity == null)
            throw new ArgumentException($"Medical referral with ID '{completeDto.ReferralId}' not found.");

        completeDto.ApplyTo(entity);

        await _referralRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical referral completed: {ReferralNumber}", entity.ReferralNumber);

        return true;
    }

    public async Task<bool> DeleteReferralAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _referralRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical referral with ID '{id}' not found.");

        await _referralRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalAppointmentDto> GetAppointmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical appointment with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<MedicalAppointmentDto?> GetAppointmentByNumberAsync(string appointmentNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByAppointmentNumberAsync(appointmentNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<MedicalAppointmentSummaryDto>> GetAllAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _appointmentRepository.GetQueryable()
            .Include(x => x.Employee)
            .Include(x => x.Facility)
            .OrderByDescending(x => x.AppointmentDateTime)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalAppointmentSummaryDto>> GetAppointmentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _appointmentRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalAppointmentSummaryDto>> GetUpcomingAppointmentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var entities = await _appointmentRepository.GetUpcomingAppointmentsAsync(daysAhead);
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalAppointmentDto> CreateAppointmentAsync(CreateMedicalAppointmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.AppointmentNumber = $"AP-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        await _appointmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical appointment created: {AppointmentNumber}", entity.AppointmentNumber);

        return entity.ToDto();
    }

    public async Task<MedicalAppointmentDto> UpdateAppointmentAsync(UpdateMedicalAppointmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical appointment with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _appointmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> UpdateAppointmentStatusAsync(UpdateMedicalAppointmentStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByIdAsync(statusDto.AppointmentId);

        if (entity == null)
            throw new ArgumentException($"Medical appointment with ID '{statusDto.AppointmentId}' not found.");

        statusDto.ApplyTo(entity);

        await _appointmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> CancelAppointmentAsync(CancelMedicalAppointmentDto cancelDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByIdAsync(cancelDto.AppointmentId);

        if (entity == null)
            throw new ArgumentException($"Medical appointment with ID '{cancelDto.AppointmentId}' not found.");

        cancelDto.ApplyTo(entity);

        await _appointmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical appointment cancelled: {AppointmentNumber}", entity.AppointmentNumber);

        return true;
    }

    public async Task<bool> CheckInAppointmentAsync(CheckInMedicalAppointmentDto checkInDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByIdAsync(checkInDto.AppointmentId);

        if (entity == null)
            throw new ArgumentException($"Medical appointment with ID '{checkInDto.AppointmentId}' not found.");

        checkInDto.ApplyTo(entity);

        await _appointmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> CheckOutAppointmentAsync(CheckOutMedicalAppointmentDto checkOutDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByIdAsync(checkOutDto.AppointmentId);

        if (entity == null)
            throw new ArgumentException($"Medical appointment with ID '{checkOutDto.AppointmentId}' not found.");

        checkOutDto.ApplyTo(entity);

        await _appointmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAppointmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appointmentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical appointment with ID '{id}' not found.");

        await _appointmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// NHIS SERVICE
// ============================================================================

#region NHIS Service

public class NHISService : INHISService
{
    private readonly INHISClaimRepository _claimRepository;
    private readonly INHISClaimDocumentRepository _claimDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NHISService> _logger;

    public NHISService(
        INHISClaimRepository claimRepository,
        INHISClaimDocumentRepository claimDocumentRepository,
        IUnitOfWork unitOfWork,
        ILogger<NHISService> logger)
    {
        _claimRepository = claimRepository;
        _claimDocumentRepository = claimDocumentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NHISClaimDto> GetClaimByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"NHIS claim with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<NHISClaimDetailDto> GetClaimWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetWithDocumentsAsync(id);

        if (entity == null)
            throw new ArgumentException($"NHIS claim with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<NHISClaimDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByClaimNumberAsync(claimNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<NHISClaimSummaryDto>> GetAllClaimsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _claimRepository.GetQueryable()
            .Include(c => c.Employee)
            .OrderByDescending(c => c.ServiceDate)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<NHISClaimSummaryDto>> GetClaimsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _claimRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<NHISClaimSummaryDto>> GetPendingClaimsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _claimRepository.GetPendingClaimsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<NHISClaimDto> CreateClaimAsync(CreateNHISClaimDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ClaimNumber = $"NH-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        await _claimRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("NHIS claim created: {ClaimNumber}", entity.ClaimNumber);

        return entity.ToDto();
    }

    public async Task<NHISClaimDto> UpdateClaimAsync(UpdateNHISClaimDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"NHIS claim with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> UpdateClaimStatusAsync(UpdateNHISClaimStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(statusDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"NHIS claim with ID '{statusDto.ClaimId}' not found.");

        statusDto.ApplyTo(entity);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("NHIS claim status updated: {ClaimNumber}, Status: {Status}", entity.ClaimNumber, entity.Status);

        return true;
    }

    public async Task<bool> SubmitClaimAsync(SubmitNHISClaimDto submitDto, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(submitDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"NHIS claim with ID '{submitDto.ClaimId}' not found.");

        submitDto.ApplyTo(entity);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("NHIS claim submitted: {ClaimNumber}", entity.ClaimNumber);

        return true;
    }

    public async Task<bool> RecordClaimPaymentAsync(RecordNHISClaimPaymentDto paymentDto, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(paymentDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"NHIS claim with ID '{paymentDto.ClaimId}' not found.");

        paymentDto.ApplyTo(entity);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("NHIS claim payment recorded: {ClaimNumber}", entity.ClaimNumber);

        return true;
    }

    public async Task<bool> DeleteClaimAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"NHIS claim with ID '{id}' not found.");

        await _claimRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<NHISClaimDocumentDto> AddClaimDocumentAsync(CreateNHISClaimDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _claimDocumentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<NHISClaimDocumentDto>> GetClaimDocumentsAsync(Guid nhisClaimId, CancellationToken cancellationToken = default)
    {
        var entities = await _claimDocumentRepository.GetByNHISClaimIdAsync(nhisClaimId);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteClaimDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _claimDocumentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"NHIS claim document with ID '{id}' not found.");

        await _claimDocumentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// MEDICAL EXPENSE CLAIM SERVICE
// ============================================================================

#region Medical Expense Claim Service

public class MedicalExpenseClaimService : IMedicalExpenseClaimService
{
    private readonly IMedicalExpenseClaimRepository _claimRepository;
    private readonly IMedicalExpenseApprovalRepository _approvalRepository;
    private readonly IMedicalExpenseItemRepository _itemRepository;
    private readonly IMedicalExpenseDocumentRepository _documentRepository;
    private readonly IMedicalExpenseClaimNoteRepository _noteRepository;
    private readonly IEmployeeMedicalInsurancePolicyRepository _policyRepository;
    private readonly IMedicalInsurancePolicyDependentRepository _policyDependentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MedicalExpenseClaimService> _logger;

    public MedicalExpenseClaimService(
        IMedicalExpenseClaimRepository claimRepository,
        IMedicalExpenseApprovalRepository approvalRepository,
        IMedicalExpenseItemRepository itemRepository,
        IMedicalExpenseDocumentRepository documentRepository,
        IMedicalExpenseClaimNoteRepository noteRepository,
        IEmployeeMedicalInsurancePolicyRepository policyRepository,
        IMedicalInsurancePolicyDependentRepository policyDependentRepository,
        IUnitOfWork unitOfWork,
        ILogger<MedicalExpenseClaimService> logger)
    {
        _claimRepository = claimRepository;
        _approvalRepository = approvalRepository;
        _itemRepository = itemRepository;
        _documentRepository = documentRepository;
        _noteRepository = noteRepository;
        _policyRepository = policyRepository;
        _policyDependentRepository = policyDependentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<MedicalExpenseClaimDto> GetClaimByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical expense claim with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<MedicalExpenseClaimDetailDto> GetClaimWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical expense claim with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<MedicalExpenseClaimDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByClaimNumberAsync(claimNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<MedicalExpenseClaimSummaryDto>> GetClaimsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _claimRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<MedicalExpenseClaimSummaryDto>> GetClaimsPagedAsync(int pageNumber, int pageSize, ClaimStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _claimRepository.GetQueryable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.ClaimDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MedicalExpenseClaimSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<MedicalExpenseClaimSummaryDto>> GetPendingClaimsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _claimRepository.GetPendingClaimsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<MedicalExpenseClaimSummaryDto>> GetFlaggedClaimsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _claimRepository.GetFlaggedForReviewAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<MedicalExpenseClaimDto> CreateClaimAsync(CreateMedicalExpenseClaimDto createDto, Guid employeeId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.EmployeeId = employeeId;
        entity.ClaimNumber = $"MC-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        await _claimRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical expense claim created: {ClaimNumber} for employee {EmployeeId}", entity.ClaimNumber, employeeId);

        return entity.ToDto();
    }

    public async Task<MedicalExpenseClaimDto> UpdateClaimAsync(UpdateMedicalExpenseClaimDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical expense claim with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> ProcessApprovalAsync(ProcessMedicalExpenseClaimDto processDto, Guid tenantId, Guid approverEmployeeId, Guid processedByUserId, CancellationToken cancellationToken = default)
    {
        var claim = await _claimRepository.GetByIdAsync(processDto.ClaimId);

        if (claim == null)
            throw new ArgumentException($"Medical expense claim with ID '{processDto.ClaimId}' not found.");

        // A claim is adjudicated exactly once; this keeps insurance utilization consistent.
        if (claim.Status is ClaimStatus.Approved or ClaimStatus.Rejected or ClaimStatus.Paid or ClaimStatus.Cancelled)
            throw new MedicalWorkflowException(
                MedicalWorkflowFailureReason.InvalidState,
                $"Claim '{claim.ClaimNumber}' has already been processed ({claim.Status}) and cannot be re-adjudicated.");

        var approval = processDto.ToEntity(tenantId, processedByUserId, approverEmployeeId);

        if (processDto.Status == MedicalExpenseApprovalStatus.Approved)
        {
            var approvedAmount = processDto.AmountApproved ?? claim.AmountRequested;
            if (approvedAmount <= 0)
                throw new MedicalWorkflowException(
                    MedicalWorkflowFailureReason.InvalidState,
                    "An approved amount greater than zero is required to approve a claim.");

            // Validates insurance limits and decrements utilization (no-op for out-of-pocket claims).
            await ConsumePolicyUtilizationAsync(claim, approvedAmount);

            claim.Status = ClaimStatus.Approved;
            claim.AmountApproved = approvedAmount;
        }
        else if (processDto.Status == MedicalExpenseApprovalStatus.Rejected)
        {
            claim.Status = ClaimStatus.Rejected;
        }

        await _approvalRepository.AddAsync(approval);
        await _claimRepository.UpdateAsync(claim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical expense claim approval processed: {ClaimNumber}, Status: {Status}", claim.ClaimNumber, processDto.Status);

        return true;
    }

    public async Task<bool> ProcessPaymentAsync(ProcessMedicalExpensePaymentDto paymentDto, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(paymentDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"Medical expense claim with ID '{paymentDto.ClaimId}' not found.");

        paymentDto.ApplyTo(entity);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Medical expense claim payment processed: {ClaimNumber}", entity.ClaimNumber);

        return true;
    }

    public async Task<bool> FlagClaimAsync(FlagMedicalExpenseClaimDto flagDto, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(flagDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"Medical expense claim with ID '{flagDto.ClaimId}' not found.");

        flagDto.ApplyTo(entity);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> UnflagClaimAsync(UnflagMedicalExpenseClaimDto unflagDto, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(unflagDto.ClaimId);

        if (entity == null)
            throw new ArgumentException($"Medical expense claim with ID '{unflagDto.ClaimId}' not found.");

        unflagDto.ApplyTo(entity);

        await _claimRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteClaimAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _claimRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical expense claim with ID '{id}' not found.");

        // Restore any insurance limit this claim had consumed at approval.
        if (entity.Status is ClaimStatus.Approved or ClaimStatus.Paid)
            await ReleasePolicyUtilizationAsync(entity, entity.AmountApproved ?? 0m);

        await _claimRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Validates that an approved amount fits within the linked policy (and dependent, where applicable)
    /// remaining limits, then increments utilization. No-op for claims without an insurance policy.
    /// Changes are tracked and persisted by the caller's <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    private async Task ConsumePolicyUtilizationAsync(MedicalExpenseClaim claim, decimal amount)
    {
        if (claim.InsurancePolicyId == null || amount <= 0)
            return;

        var policy = await _policyRepository.GetByIdAsync(claim.InsurancePolicyId.Value);
        if (policy == null)
            throw new MedicalWorkflowException(
                MedicalWorkflowFailureReason.NotFound,
                "The insurance policy linked to this claim no longer exists.");

        if (policy.Status != MedicalInsurancePolicyStatus.Active || !policy.IsActive)
            throw new MedicalWorkflowException(
                MedicalWorkflowFailureReason.PolicyInactive,
                $"Policy '{policy.PolicyNumber}' is not active and cannot absorb new claims.");

        // Resolve the dependent sub-limit up front so all validation happens before any mutation.
        MedicalInsurancePolicyDependent? dependent = null;
        if (claim.IsForDependent && claim.DependentId.HasValue)
        {
            var dependents = await _policyDependentRepository.GetByPolicyIdAsync(policy.Id);
            dependent = dependents.FirstOrDefault(d => d.DependentId == claim.DependentId.Value);

            if (dependent == null || !dependent.IsActive)
                throw new MedicalWorkflowException(
                    MedicalWorkflowFailureReason.DependentNotCovered,
                    "The dependent on this claim is not actively covered under the policy.");

            if (amount > dependent.RemainingLimit)
                throw new MedicalWorkflowException(
                    MedicalWorkflowFailureReason.LimitExceeded,
                    $"Approved amount {amount:N2} exceeds the dependent's remaining limit {dependent.RemainingLimit:N2}.");
        }

        // The policy annual limit is the overall (family) cap and is always charged.
        if (amount > policy.RemainingLimit)
            throw new MedicalWorkflowException(
                MedicalWorkflowFailureReason.LimitExceeded,
                $"Approved amount {amount:N2} exceeds the policy's remaining annual limit {policy.RemainingLimit:N2}.");

        if (dependent != null)
        {
            dependent.UtilizedAmount += amount;
            await _policyDependentRepository.UpdateAsync(dependent);
        }

        policy.UtilizedAmount += amount;
        await _policyRepository.UpdateAsync(policy);
    }

    /// <summary>
    /// Reverses utilization previously charged by <see cref="ConsumePolicyUtilizationAsync"/> when an
    /// approved claim is removed. Clamped at zero to stay resilient to manual data adjustments.
    /// </summary>
    private async Task ReleasePolicyUtilizationAsync(MedicalExpenseClaim claim, decimal amount)
    {
        if (claim.InsurancePolicyId == null || amount <= 0)
            return;

        var policy = await _policyRepository.GetByIdAsync(claim.InsurancePolicyId.Value);
        if (policy != null)
        {
            policy.UtilizedAmount = Math.Max(0m, policy.UtilizedAmount - amount);
            await _policyRepository.UpdateAsync(policy);
        }

        if (claim.IsForDependent && claim.DependentId.HasValue)
        {
            var dependents = await _policyDependentRepository.GetByPolicyIdAsync(claim.InsurancePolicyId.Value);
            var dependent = dependents.FirstOrDefault(d => d.DependentId == claim.DependentId.Value);
            if (dependent != null)
            {
                dependent.UtilizedAmount = Math.Max(0m, dependent.UtilizedAmount - amount);
                await _policyDependentRepository.UpdateAsync(dependent);
            }
        }
    }

    public async Task<MedicalExpenseItemDto> AddItemAsync(CreateMedicalExpenseItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _itemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalExpenseItemDto>> GetItemsAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var entities = await _itemRepository.GetByClaimIdAsync(claimId);
        return entities.ToDtoList();
    }

    public async Task<MedicalExpenseItemDto> UpdateItemAsync(UpdateMedicalExpenseItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
            throw new ArgumentException($"Medical expense item with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _itemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _itemRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical expense item with ID '{id}' not found.");

        await _itemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalExpenseDocumentDto> AddDocumentAsync(CreateMedicalExpenseDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalExpenseDocumentDto>> GetDocumentsAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var entities = await _documentRepository.GetByClaimIdAsync(claimId);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Medical expense document with ID '{id}' not found.");

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<MedicalExpenseClaimNoteDto> AddNoteAsync(AddMedicalExpenseClaimNoteDto createDto, Guid tenantId, Guid authorEmployeeId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId, authorEmployeeId);

        await _noteRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<MedicalExpenseClaimNoteDto>> GetNotesAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var entities = await _noteRepository.GetByClaimIdAsync(claimId);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<MedicalExpenseClaimNoteDto>> GetInternalNotesAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var entities = await _noteRepository.GetInternalNotesAsync(claimId);
        return entities.ToDtoList();
    }
}

#endregion

// ============================================================================
// MEDICAL DASHBOARD SERVICE
// ============================================================================

#region Medical Dashboard Service

public class MedicalDashboardService : IMedicalDashboardService
{
    private readonly IMedicalExpenseClaimRepository _claimRepository;
    private readonly IEmployeeMedicalInsurancePolicyRepository _policyRepository;
    private readonly IMedicalInsurancePremiumRecordRepository _premiumRepository;
    private readonly IEmployeeMedicalExamRepository _examRepository;
    private readonly IMedicalClaimPreAuthorizationRepository _preAuthorizationRepository;
    private readonly IMedicalReferralRepository _referralRepository;
    private readonly IMedicalAppointmentRepository _appointmentRepository;

    public MedicalDashboardService(
        IMedicalExpenseClaimRepository claimRepository,
        IEmployeeMedicalInsurancePolicyRepository policyRepository,
        IMedicalInsurancePremiumRecordRepository premiumRepository,
        IEmployeeMedicalExamRepository examRepository,
        IMedicalClaimPreAuthorizationRepository preAuthorizationRepository,
        IMedicalReferralRepository referralRepository,
        IMedicalAppointmentRepository appointmentRepository)
    {
        _claimRepository = claimRepository;
        _policyRepository = policyRepository;
        _premiumRepository = premiumRepository;
        _examRepository = examRepository;
        _preAuthorizationRepository = preAuthorizationRepository;
        _referralRepository = referralRepository;
        _appointmentRepository = appointmentRepository;
    }

    public async Task<MedicalDashboardDto> GetDashboardAsync(int upcomingDays = 30, CancellationToken cancellationToken = default)
    {
        // Lightweight scalar projection of every claim for in-memory aggregation.
        var claims = await _claimRepository.GetQueryable()
            .Select(c => new ClaimRow
            {
                Status = c.Status,
                AmountRequested = c.AmountRequested,
                AmountApproved = c.AmountApproved,
                ClaimDate = c.ClaimDate,
                IsFlaggedForReview = c.IsFlaggedForReview,
            })
            .ToListAsync(cancellationToken);

        var dto = new MedicalDashboardDto
        {
            TotalClaims = claims.Count,
            PendingClaims = claims.Count(c => c.Status == ClaimStatus.Pending),
            FlaggedClaims = claims.Count(c => c.IsFlaggedForReview),
            ApprovedClaims = claims.Count(c => c.Status == ClaimStatus.Approved),
            PaidClaims = claims.Count(c => c.Status == ClaimStatus.Paid),
            TotalReimbursedAmount = claims
                .Where(c => c.Status == ClaimStatus.Approved || c.Status == ClaimStatus.Paid)
                .Sum(c => c.AmountApproved ?? 0m),
            PendingClaimsAmount = claims
                .Where(c => c.Status == ClaimStatus.Pending)
                .Sum(c => c.AmountRequested),
        };

        dto.ClaimsByStatus = claims
            .GroupBy(c => c.Status)
            .Select(g => new MedicalClaimStatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderByDescending(s => s.Count)
            .ToList();

        // Monthly claim trend over the trailing six calendar months (by claim date).
        var anchor = DateTime.UtcNow;
        for (var i = 5; i >= 0; i--)
        {
            var month = anchor.AddMonths(-i);
            dto.MonthlyClaimTrend.Add(new MedicalMonthlyClaimCountDto
            {
                Year = month.Year,
                Month = month.Month,
                Label = new DateTime(month.Year, month.Month, 1).ToString("MMM yyyy"),
                Count = claims.Count(c => c.ClaimDate.Year == month.Year && c.ClaimDate.Month == month.Month),
            });
        }

        // Insurance metrics.
        dto.ActivePolicies = await _policyRepository.GetQueryable()
            .CountAsync(p => p.Status == MedicalInsurancePolicyStatus.Active && p.IsActive, cancellationToken);
        dto.ExpiringPolicies = (await _policyRepository.GetExpiringPoliciesAsync(upcomingDays)).Count();

        var overduePremiums = (await _premiumRepository.GetOverduePremiumsAsync()).ToList();
        dto.OverduePremiums = overduePremiums.Count;
        dto.OverduePremiumAmount = overduePremiums.Sum(p => p.TotalPremiumAmount);

        // Clinical metrics.
        dto.PendingPreAuthorizations = (await _preAuthorizationRepository.GetPendingApprovalsAsync()).Count();
        dto.PendingReferrals = (await _referralRepository.GetPendingReferralsAsync()).Count();
        dto.ExamsDue = (await _examRepository.GetDueForExamAsync(upcomingDays)).Count();

        // Spotlights — recent and pending claims (with employee name).
        dto.RecentClaims = await _claimRepository.GetQueryable()
            .Include(c => c.Employee)
            .OrderByDescending(c => c.ClaimDate)
            .Take(5)
            .Select(c => new MedicalClaimSpotlightDto
            {
                Id = c.Id,
                ClaimNumber = c.ClaimNumber,
                EmployeeName = c.Employee != null ? c.Employee.FullName : string.Empty,
                ExpenseType = c.ExpenseType,
                Status = c.Status,
                AmountRequested = c.AmountRequested,
                ClaimDate = c.ClaimDate,
            })
            .ToListAsync(cancellationToken);

        dto.PendingApprovalClaims = await _claimRepository.GetQueryable()
            .Include(c => c.Employee)
            .Where(c => c.Status == ClaimStatus.Pending)
            .OrderBy(c => c.ClaimDate)
            .Take(5)
            .Select(c => new MedicalClaimSpotlightDto
            {
                Id = c.Id,
                ClaimNumber = c.ClaimNumber,
                EmployeeName = c.Employee != null ? c.Employee.FullName : string.Empty,
                ExpenseType = c.ExpenseType,
                Status = c.Status,
                AmountRequested = c.AmountRequested,
                ClaimDate = c.ClaimDate,
            })
            .ToListAsync(cancellationToken);

        // Spotlight — upcoming appointments (with employee + facility names).
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(upcomingDays);
        var upcoming = await _appointmentRepository.GetQueryable()
            .Include(a => a.Employee)
            .Include(a => a.Facility)
            .Where(a => a.AppointmentDateTime >= now
                     && a.AppointmentDateTime <= horizon
                     && a.Status != MedicalAppointmentStatus.Cancelled)
            .OrderBy(a => a.AppointmentDateTime)
            .ToListAsync(cancellationToken);

        dto.UpcomingAppointments = upcoming.Count;
        dto.UpcomingAppointmentList = upcoming
            .Take(5)
            .Select(a => new MedicalAppointmentSpotlightDto
            {
                Id = a.Id,
                AppointmentNumber = a.AppointmentNumber,
                EmployeeName = a.Employee != null ? a.Employee.FullName : string.Empty,
                FacilityName = a.Facility != null ? a.Facility.FacilityName : string.Empty,
                AppointmentDateTime = a.AppointmentDateTime,
                Status = a.Status,
            })
            .ToList();

        return dto;
    }

    private sealed class ClaimRow
    {
        public ClaimStatus Status { get; set; }
        public decimal AmountRequested { get; set; }
        public decimal? AmountApproved { get; set; }
        public DateTime ClaimDate { get; set; }
        public bool IsFlaggedForReview { get; set; }
    }
}

#endregion
