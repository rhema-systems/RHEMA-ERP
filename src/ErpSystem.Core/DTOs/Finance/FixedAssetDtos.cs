using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public class FixedAssetCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepreciationMethod DefaultMethod { get; set; }
    public int DefaultUsefulLifeMonths { get; set; }
    public decimal DefaultResidualValuePercent { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public Guid? GainOnDisposalAccountId { get; set; }
    public Guid? LossOnDisposalAccountId { get; set; }
    public Guid? RevaluationSurplusAccountId { get; set; }
    public Guid? AucAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateFixedAssetCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepreciationMethod DefaultMethod { get; set; } = DepreciationMethod.StraightLine;
    public int DefaultUsefulLifeMonths { get; set; } = 36;
    public decimal DefaultResidualValuePercent { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public Guid? GainOnDisposalAccountId { get; set; }
    public Guid? LossOnDisposalAccountId { get; set; }
    public Guid? RevaluationSurplusAccountId { get; set; }
    public Guid? AucAccountId { get; set; }
}

public class UpdateFixedAssetCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DepreciationMethod DefaultMethod { get; set; } = DepreciationMethod.StraightLine;
    public int DefaultUsefulLifeMonths { get; set; } = 36;
    public decimal DefaultResidualValuePercent { get; set; }
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDepreciationAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public Guid? GainOnDisposalAccountId { get; set; }
    public Guid? LossOnDisposalAccountId { get; set; }
    public Guid? RevaluationSurplusAccountId { get; set; }
    public Guid? AucAccountId { get; set; }
}

public class FixedAssetDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public Guid FixedAssetCategoryId { get; set; }
    public string? FixedAssetCategoryName { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal InstallationCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal NetBookValue { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public DepreciationConvention DepreciationConvention { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public FixedAssetStatus Status { get; set; }
    public DateTime? DisposalDate { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class CreateFixedAssetDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public Guid FixedAssetCategoryId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal InstallationCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public DepreciationConvention DepreciationConvention { get; set; } = DepreciationConvention.FullMonth;
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public string? SerialNumber { get; set; }
}

public class UpdateFixedAssetDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public Guid FixedAssetCategoryId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal InstallationCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public DepreciationConvention DepreciationConvention { get; set; } = DepreciationConvention.FullMonth;
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Draft;
    public DateTime? DisposalDate { get; set; }
    public Guid? MaintenanceAssetId { get; set; }
    public string? SerialNumber { get; set; }
}

public class RunDepreciationDto
{
    public Guid FiscalPeriodId { get; set; }
    public Guid? FixedAssetId { get; set; }
    public bool PostToGl { get; set; } = true;
    public DateTime? PostingDate { get; set; }
}

public class AssetDepreciationScheduleDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public Guid FiscalPeriodId { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedDate { get; set; }
    public Guid? JournalEntryId { get; set; }
    public bool IsProjected { get; set; }
}

public class FixedAssetGlAccountOptionDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AccountCategory { get; set; }
    public string? AccountSubCategory { get; set; }
}

public class FixedAssetGlAccountOptionsDto
{
    public List<FixedAssetGlAccountOptionDto> AssetAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> AccumulatedDepreciationAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> DepreciationExpenseAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> GainOnDisposalAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> LossOnDisposalAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> RevaluationSurplusAccounts { get; set; } = new();
    public List<FixedAssetGlAccountOptionDto> AucAccounts { get; set; } = new();
}

public class AssetTransferDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public string? FixedAssetName { get; set; }
    public string? AssetCode { get; set; }
    public DateTime TransferDate { get; set; }
    public AssetTransferType TransferType { get; set; }
    public AssetTransferStatus Status { get; set; }
    public string? FromLocation { get; set; }
    public Guid? FromCustodianId { get; set; }
    public string? FromCustodianName { get; set; }
    public string ToLocation { get; set; } = string.Empty;
    public Guid? ToCustodianId { get; set; }
    public string? ToCustodianName { get; set; }
    public string? Reason { get; set; }
    public decimal? TransferCost { get; set; }
    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Comments { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RequestAssetTransferDto
{
    public Guid FixedAssetId { get; set; }
    public DateTime TransferDate { get; set; }
    public AssetTransferType TransferType { get; set; } = AssetTransferType.Internal;
    public string ToLocation { get; set; } = string.Empty;
    public Guid? ToCustodianId { get; set; }
    public string? Reason { get; set; }
    public decimal? TransferCost { get; set; }
}

public class ApproveAssetTransferDto
{
    public string? Comments { get; set; }
}

public class AssetDisposalDto
{
    public Guid Id { get; set; }
    public Guid FixedAssetId { get; set; }
    public string? FixedAssetName { get; set; }
    public string? AssetCode { get; set; }
    public DateTime DisposalDate { get; set; }
    public DisposalType DisposalType { get; set; }
    public AssetDisposalStatus Status { get; set; }
    public string? Reason { get; set; }
    public decimal? SaleProceeds { get; set; }
    public decimal? DisposalCost { get; set; }
    public decimal NetBookValueAtDisposal { get; set; }
    public decimal GainOrLoss { get; set; }
    public string? BuyerName { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RequestAssetDisposalDto
{
    public Guid FixedAssetId { get; set; }
    public DateTime DisposalDate { get; set; }
    public DisposalType DisposalType { get; set; }
    public string? Reason { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal DisposalCost { get; set; }
    public string? BuyerName { get; set; }
}

public class ApproveAssetDisposalDto
{
    public string? Comments { get; set; }
}

public class AssetVerificationSessionDto
{
    public Guid Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public VerificationSessionStatus Status { get; set; }
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public int TotalItems { get; set; }
    public int VerifiedItems { get; set; }
}

public class CreateAssetVerificationSessionDto
{
    public string SessionName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string? Description { get; set; }
    public List<Guid>? AssetIds { get; set; } // If null, include all active assets
}

public class AssetVerificationItemDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid FixedAssetId { get; set; }
    public string? FixedAssetName { get; set; }
    public string? AssetCode { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public AssetCondition Condition { get; set; }
    public string? CurrentLocation { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

public class VerifyAssetDto
{
    public AssetCondition Condition { get; set; }
    public string? CurrentLocation { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }
}

// Bulk Import DTOs
public class BulkImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public bool IsDryRun { get; set; }
    public List<BulkImportErrorDto> Errors { get; set; } = new();
    public List<string> SuccessfulAssetCodes { get; set; } = new();
}

public class BulkImportErrorDto
{
    public int RowNumber { get; set; }
    public string? AssetCode { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public class BulkAssetImportRowDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public DateTime? PlacedInServiceDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? InstallationCost { get; set; }
    public decimal? TaxAmount { get; set; }
    public int? UsefulLifeMonths { get; set; }
    public decimal? ResidualValue { get; set; }
    public string? SerialNumber { get; set; }
    public FixedAssetStatus? Status { get; set; }
    public string? RawStatus { get; set; }
}
