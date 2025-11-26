using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IAssetDischargeService
{
    Task<PagedResult<AssetDischargeDto>> GetDischargesAsync(DischargeQueryParameters query);
    Task<AssetDischargeDto?> GetDischargeByIdAsync(Guid id);
    Task<IEnumerable<AssetDischargeDto>> GetDischargesByAdmissionAsync(Guid admissionId);
    Task<AssetDischargeDto> CreateDischargeAsync(CreateAssetDischargeDto dto);
    Task<AssetDischargeDto> UpdateDischargeAsync(Guid id, UpdateAssetDischargeDto dto);
    Task<MaintenanceCertificateDto> GenerateCompletionCertificateAsync(Guid dischargeId);
}

public class DischargeQueryParameters
{
    public Guid? AdmissionId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class MaintenanceCertificateDto
{
    public Guid DischargeId { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public DateTime DischargeDate { get; set; }
    public string WorkCompleted { get; set; } = string.Empty;
    public string DischargedBy { get; set; } = string.Empty;
    public string QualityCheckedBy { get; set; } = string.Empty;
    public DateTime? QualityCheckDate { get; set; }
    public int WarrantyDays { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    public string? WarrantyTerms { get; set; }
    public DateTime GeneratedDate { get; set; }
    public string? CertificatePath { get; set; }
}

