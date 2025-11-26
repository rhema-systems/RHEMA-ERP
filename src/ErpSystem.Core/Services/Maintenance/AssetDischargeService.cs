using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Core.Services.Maintenance;

public class AssetDischargeService : IAssetDischargeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly IAssetAdmissionService _admissionService;
    private readonly IAssetDowntimeService _downtimeService;

    public AssetDischargeService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMaintenanceAssetService assetService,
        IAssetAdmissionService admissionService,
        IAssetDowntimeService downtimeService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _assetService = assetService;
        _admissionService = admissionService;
        _downtimeService = downtimeService;
    }

    public async Task<PagedResult<AssetDischargeDto>> GetDischargesAsync(DischargeQueryParameters query)
    {
        var repo = _unitOfWork.Repository<AssetDischarge>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var discharges = await repo.FindAsync(d => d.TenantId == tenantId);

        if (query.AdmissionId.HasValue)
            discharges = discharges.Where(d => d.AdmissionId == query.AdmissionId.Value);
        if (query.AssetId.HasValue)
            discharges = discharges.Where(d => d.AssetId == query.AssetId.Value);
        if (query.WorkOrderId.HasValue)
            discharges = discharges.Where(d => d.WorkOrderId == query.WorkOrderId.Value);
        if (query.FromDate.HasValue)
            discharges = discharges.Where(d => d.DischargeDate >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            discharges = discharges.Where(d => d.DischargeDate <= query.ToDate.Value);

        var totalCount = discharges.Count();
        var items = discharges
            .OrderByDescending(d => d.DischargeDate)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var assetIds = items.Select(d => d.AssetId).Distinct().ToList();
        var assets = await _assetService.GetAssetsByIdsAsync(assetIds);
        var assetMap = assets.ToDictionary(a => a.Id, a => a);

        var dtos = items.Select(d => MapToDto(d, assetMap.GetValueOrDefault(d.AssetId))).ToList();

        return new PagedResult<AssetDischargeDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<AssetDischargeDto?> GetDischargeByIdAsync(Guid id)
    {
        var repo = _unitOfWork.Repository<AssetDischarge>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var discharge = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);
        if (discharge == null)
            return null;

        var asset = await _assetService.GetAssetByIdAsync(discharge.AssetId);
        return MapToDto(discharge, asset);
    }

    public async Task<IEnumerable<AssetDischargeDto>> GetDischargesByAdmissionAsync(Guid admissionId)
    {
        var repo = _unitOfWork.Repository<AssetDischarge>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var discharges = await repo.FindAsync(d => d.TenantId == tenantId && d.AdmissionId == admissionId);

        var assetIds = discharges.Select(d => d.AssetId).Distinct().ToList();
        var assets = await _assetService.GetAssetsByIdsAsync(assetIds);
        var assetMap = assets.ToDictionary(a => a.Id, a => a);

        return discharges
            .OrderByDescending(d => d.DischargeDate)
            .Select(d => MapToDto(d, assetMap.GetValueOrDefault(d.AssetId)))
            .ToList();
    }

    public async Task<AssetDischargeDto> CreateDischargeAsync(CreateAssetDischargeDto dto)
    {
        // Ensure admission exists and belongs to tenant
        var admissionDto = await _admissionService.GetAdmissionByIdAsync(dto.AdmissionId)
                          ?? throw new KeyNotFoundException($"Admission {dto.AdmissionId} not found");

        var asset = await _assetService.GetAssetByIdAsync(admissionDto.AssetId)
                    ?? throw new KeyNotFoundException($"Asset {admissionDto.AssetId} not found");

        var repo = _unitOfWork.Repository<AssetDischarge>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        var discharge = new AssetDischarge
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DischargeNumber = $"DIS-{DateTime.UtcNow:yyyyMMddHHmmss}",
            AdmissionId = dto.AdmissionId,
            AssetId = admissionDto.AssetId,
            JobCardId = admissionDto.JobCardId,
            WorkOrderId = admissionDto.WorkOrderId,
            DischargeDate = DateTime.UtcNow,
            DischargedById = Guid.TryParse(_currentUserService.UserId, out var dischargedById) ? dischargedById : Guid.Empty,
            AssetConditionOnDischarge = dto.AssetConditionOnDischarge,
            DischargeNotes = dto.DischargeNotes,
            WorkCompleted = dto.WorkCompleted,
            RemainingIssues = dto.RemainingIssues,
            MileageReading = dto.MileageReading,
            HoursReading = dto.HoursReading,
            FuelLevel = dto.FuelLevel,
            QualityCheckPassed = dto.QualityCheckPassed,
            QualityCheckNotes = dto.QualityCheckNotes,
            DischargeChecklist = dto.DischargeChecklistJson,
            CustomerAcceptance = dto.CustomerAcceptance,
            AcceptanceNotes = dto.AcceptanceNotes,
            RequiresFollowUp = dto.RequiresFollowUp,
            FollowUpDate = dto.FollowUpDate,
            FollowUpInstructions = dto.FollowUpInstructions,
            WarrantyDays = dto.WarrantyDays ?? 0,
            WarrantyTerms = dto.WarrantyTerms
        };

        await repo.AddAsync(discharge);
        await _unitOfWork.SaveChangesAsync();

        // Link to admission and mark admission completed
        var admissionRepo = _unitOfWork.Repository<AssetAdmission>();
        var admission = await admissionRepo.FirstOrDefaultAsync(a => a.Id == dto.AdmissionId && a.TenantId == tenantId);
        if (admission != null)
        
        {
            admission.DischargeId = discharge.Id;
            admission.Status = "Completed";
            await admissionRepo.UpdateAsync(admission);
            await _unitOfWork.SaveChangesAsync();
        }

        // End any active downtime for this asset & work order
        var activeDowntimes = await _downtimeService.GetActiveDowntimeAsync();
        foreach (var downtime in activeDowntimes.Where(d => d.AssetId == discharge.AssetId && d.RelatedWorkOrderId == discharge.WorkOrderId))
        {
            await _downtimeService.EndDowntimeAsync(downtime.Id, DateTime.UtcNow, $"Asset discharged: {discharge.DischargeNumber}");
        }

        return MapToDto(discharge, asset);
    }

    public async Task<AssetDischargeDto> UpdateDischargeAsync(Guid id, UpdateAssetDischargeDto dto)
    {
        var repo = _unitOfWork.Repository<AssetDischarge>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var discharge = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId)
                        ?? throw new KeyNotFoundException($"Discharge {id} not found");

        discharge.AssetConditionOnDischarge = dto.AssetConditionOnDischarge ?? discharge.AssetConditionOnDischarge;
        discharge.DischargeNotes = dto.DischargeNotes ?? discharge.DischargeNotes;
        discharge.WorkCompleted = dto.WorkCompleted ?? discharge.WorkCompleted;
        discharge.RemainingIssues = dto.RemainingIssues ?? discharge.RemainingIssues;
        discharge.MileageReading = dto.MileageReading ?? discharge.MileageReading;
        discharge.HoursReading = dto.HoursReading ?? discharge.HoursReading;
        discharge.FuelLevel = dto.FuelLevel ?? discharge.FuelLevel;
        discharge.QualityCheckPassed = dto.QualityCheckPassed ?? discharge.QualityCheckPassed;
        discharge.QualityCheckNotes = dto.QualityCheckNotes ?? discharge.QualityCheckNotes;
        discharge.DischargeChecklist = dto.DischargeChecklistJson ?? discharge.DischargeChecklist;
        discharge.CustomerAcceptance = dto.CustomerAcceptance ?? discharge.CustomerAcceptance;
        discharge.AcceptanceNotes = dto.AcceptanceNotes ?? discharge.AcceptanceNotes;
        discharge.RequiresFollowUp = dto.RequiresFollowUp ?? discharge.RequiresFollowUp;
        discharge.FollowUpDate = dto.FollowUpDate ?? discharge.FollowUpDate;
        discharge.FollowUpInstructions = dto.FollowUpInstructions ?? discharge.FollowUpInstructions;
        discharge.WarrantyDays = dto.WarrantyDays ?? discharge.WarrantyDays;
        discharge.WarrantyTerms = dto.WarrantyTerms ?? discharge.WarrantyTerms;

        await repo.UpdateAsync(discharge);
        await _unitOfWork.SaveChangesAsync();

        var asset = await _assetService.GetAssetByIdAsync(discharge.AssetId);
        return MapToDto(discharge, asset);
    }

    public async Task<MaintenanceCertificateDto> GenerateCompletionCertificateAsync(Guid dischargeId)
    {
        var repo = _unitOfWork.Repository<AssetDischarge>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var discharge = await repo.FirstOrDefaultAsync(d => d.Id == dischargeId && d.TenantId == tenantId)
                        ?? throw new KeyNotFoundException($"Discharge {dischargeId} not found");

        var asset = await _assetService.GetAssetByIdAsync(discharge.AssetId);

        // For P1 we keep certificate generation minimal: just log key fields and return a DTO
        var cert = new MaintenanceCertificateDto
        {
            DischargeId = discharge.Id,
            CertificateNumber = $"CERT-{DateTime.UtcNow:yyyyMMddHHmmss}",
            AssetName = asset?.Name ?? string.Empty,
            AssetNumber = asset?.AssetNumber ?? string.Empty,
            DischargeDate = discharge.DischargeDate,
            WorkCompleted = discharge.WorkCompleted ?? $"Asset discharged: {discharge.DischargeNumber}",
            DischargedBy = string.Empty, // Would need to fetch user name
            QualityCheckedBy = string.Empty, // Would need to fetch user name
            QualityCheckDate = discharge.QualityCheckDate,
            WarrantyDays = discharge.WarrantyDays,
            WarrantyExpiration = discharge.WarrantyExpiration,
            WarrantyTerms = discharge.WarrantyTerms,
            GeneratedDate = DateTime.UtcNow,
            CertificatePath = discharge.CertificatePath
        };

        // Update discharge to mark certificate as generated
        discharge.CertificateGenerated = true;
        discharge.CertificateGeneratedDate = DateTime.UtcNow;
        await repo.UpdateAsync(discharge);
        await _unitOfWork.SaveChangesAsync();

        // Persist certificate if/when we introduce a MaintenanceCertificate entity in P2+.
        return cert;
    }

    private static AssetDischargeDto MapToDto(AssetDischarge discharge, MaintenanceAssetDto? asset)
    {
        return new AssetDischargeDto
        {
            Id = discharge.Id,
            DischargeNumber = discharge.DischargeNumber,
            AdmissionId = discharge.AdmissionId,
            AssetId = discharge.AssetId,
            AssetName = asset?.Name ?? string.Empty,
            AssetNumber = asset?.AssetNumber ?? string.Empty,
            JobCardId = discharge.JobCardId,
            WorkOrderId = discharge.WorkOrderId,
            DischargeDate = discharge.DischargeDate,
            DischargedById = discharge.DischargedById,
            DischargedBy = string.Empty,
            AssetConditionOnDischarge = discharge.AssetConditionOnDischarge,
            DischargeNotes = discharge.DischargeNotes,
            WorkCompleted = discharge.WorkCompleted,
            RemainingIssues = discharge.RemainingIssues,
            MileageReading = discharge.MileageReading,
            HoursReading = discharge.HoursReading,
            FuelLevel = discharge.FuelLevel,
            QualityCheckPassed = discharge.QualityCheckPassed,
            QualityCheckedById = discharge.QualityCheckedById,
            QualityCheckedBy = string.Empty,
            QualityCheckDate = discharge.QualityCheckDate,
            QualityCheckNotes = discharge.QualityCheckNotes,
            DischargeChecklistJson = discharge.DischargeChecklist,
            CertificateGenerated = discharge.CertificateGenerated,
            CertificateGeneratedDate = discharge.CertificateGeneratedDate,
            CertificatePath = discharge.CertificatePath,
            CustomerAcceptance = discharge.CustomerAcceptance,
            AcceptedById = discharge.AcceptedById,
            AcceptedBy = string.Empty,
            AcceptedDate = discharge.AcceptedDate,
            AcceptanceNotes = discharge.AcceptanceNotes,
            RequiresFollowUp = discharge.RequiresFollowUp,
            FollowUpDate = discharge.FollowUpDate,
            FollowUpInstructions = discharge.FollowUpInstructions,
            WarrantyDays = discharge.WarrantyDays,
            WarrantyExpiration = discharge.WarrantyExpiration,
            WarrantyTerms = discharge.WarrantyTerms,
            CreatedAt = discharge.CreatedAt
        };
    }
}

