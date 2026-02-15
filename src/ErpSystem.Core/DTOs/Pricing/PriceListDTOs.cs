using ErpSystem.Core.Entities.Pricing;

namespace ErpSystem.Core.DTOs.Pricing;

#region Price List DTOs

/// <summary>
/// DTO for displaying price list in lists
/// </summary>
public class PriceListDto
{
    public Guid Id { get; set; }
    public string PriceListCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PriceListType Type { get; set; }
    public string TypeName => Type.ToString();
    public string Currency { get; set; } = "USD";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public PriceListStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public PriceListApplicableEntityType ApplicableEntityType { get; set; }
    public string ApplicableEntityTypeName => ApplicableEntityType.ToString();
    public Guid? ApplicableEntityId { get; set; }
    public string? ApplicableEntityName { get; set; } // Resolved name
    public PriceListApprovalStatus ApprovalStatus { get; set; }
    public string ApprovalStatusName => ApprovalStatus.ToString();
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public int Version { get; set; }
    public int LineCount { get; set; }
    public bool IsEffective { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new price list
/// </summary>
public class CreatePriceListDto
{
    public string PriceListCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PriceListType Type { get; set; } = PriceListType.Purchase;
    public string Currency { get; set; } = "USD";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsDefault { get; set; } = false;
    public PriceListApplicableEntityType ApplicableEntityType { get; set; } = PriceListApplicableEntityType.All;
    public Guid? ApplicableEntityId { get; set; }
    public string? Notes { get; set; }
    public List<CreatePriceListLineDto> Lines { get; set; } = new();
}

/// <summary>
/// DTO for updating a price list
/// </summary>
public class UpdatePriceListDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public PriceListApplicableEntityType ApplicableEntityType { get; set; }
    public Guid? ApplicableEntityId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for price list with all details including lines
/// </summary>
public class PriceListDetailDto : PriceListDto
{
    public string? Notes { get; set; }
    public string? ApprovalComments { get; set; }
    public Guid? SupersededPriceListId { get; set; }
    public string? SupersededPriceListCode { get; set; }
    public List<PriceListLineDto> Lines { get; set; } = new();
}

#endregion

#region Price List Line DTOs

/// <summary>
/// DTO for displaying price list line
/// </summary>
public class PriceListLineDto
{
    public Guid Id { get; set; }
    public Guid PriceListId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemDescription { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal BasePrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal NetPrice { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsTaxInclusive { get; set; }
    public PriceRoundingRule RoundingRule { get; set; }
    public string? SupplierItemCode { get; set; }
    public decimal? MinimumOrderQuantity { get; set; }
    public decimal? OrderMultiple { get; set; }
    public DateTime? LastPriceUpdate { get; set; }
    public decimal? PreviousPrice { get; set; }
    public decimal? PriceChangePercent { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for displaying price list lines for a specific inventory item (includes price list info)
/// </summary>
public class ItemPriceListLineDto
{
    public Guid Id { get; set; }
    public Guid PriceListId { get; set; }
    public string PriceListCode { get; set; } = string.Empty;
    public string PriceListName { get; set; } = string.Empty;
    public PriceListType PriceListType { get; set; }
    public PriceListStatus PriceListStatus { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemDescription { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal BasePrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal NetPrice { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public string? SupplierItemCode { get; set; }
    public DateTime? LastPriceUpdate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a price list line
/// </summary>
public class CreatePriceListLineDto
{
    public Guid PriceListId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal BasePrice { get; set; }
    public decimal DiscountPercent { get; set; } = 0;
    public decimal MinQuantity { get; set; } = 0;
    public decimal? MaxQuantity { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsTaxInclusive { get; set; } = false;
    public PriceRoundingRule RoundingRule { get; set; } = PriceRoundingRule.None;
    public string? SupplierItemCode { get; set; }
    public decimal? MinimumOrderQuantity { get; set; }
    public decimal? OrderMultiple { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating a price list line
/// </summary>
public class UpdatePriceListLineDto : CreatePriceListLineDto
{
    public bool IsActive { get; set; } = true;
}

#endregion

#region Customer/Supplier Group DTOs

/// <summary>
/// DTO for customer group
/// </summary>
public class CustomerGroupDto
{
    public Guid Id { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultDiscountPercent { get; set; }
    public string? DefaultPaymentTerms { get; set; }
    public decimal? DefaultCreditLimit { get; set; }
    public Guid? DefaultPriceListId { get; set; }
    public string? DefaultPriceListName { get; set; }
    public bool IsActive { get; set; }
    public int CustomerCount { get; set; }
}

/// <summary>
/// DTO for creating customer group
/// </summary>
public class CreateCustomerGroupDto
{
    public string GroupCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultDiscountPercent { get; set; } = 0;
    public string? DefaultPaymentTerms { get; set; }
    public decimal? DefaultCreditLimit { get; set; }
    public Guid? DefaultPriceListId { get; set; }
}

/// <summary>
/// DTO for supplier group
/// </summary>
public class SupplierGroupDto
{
    public Guid Id { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DefaultPaymentTerms { get; set; }
    public int DefaultLeadTimeDays { get; set; }
    public Guid? DefaultPriceListId { get; set; }
    public string? DefaultPriceListName { get; set; }
    public bool IsActive { get; set; }
    public int SupplierCount { get; set; }
}

/// <summary>
/// DTO for creating supplier group
/// </summary>
public class CreateSupplierGroupDto
{
    public string GroupCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DefaultPaymentTerms { get; set; }
    public int DefaultLeadTimeDays { get; set; } = 7;
    public Guid? DefaultPriceListId { get; set; }
}

/// <summary>
/// DTO for updating customer group
/// </summary>
public class UpdateCustomerGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultDiscountPercent { get; set; }
    public string? DefaultPaymentTerms { get; set; }
    public decimal? DefaultCreditLimit { get; set; }
    public Guid? DefaultPriceListId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for updating supplier group
/// </summary>
public class UpdateSupplierGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DefaultPaymentTerms { get; set; }
    public int DefaultLeadTimeDays { get; set; }
    public Guid? DefaultPriceListId { get; set; }
    public bool IsActive { get; set; } = true;
}

#endregion

#region Price List Change History DTOs

/// <summary>
/// DTO for price list change history
/// </summary>
public class PriceListChangeHistoryDto
{
    public Guid Id { get; set; }
    public Guid PriceListLineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal? OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal? ChangePercent { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? ChangeReason { get; set; }
    public DateTime EffectiveDate { get; set; }
    public Guid? ChangedById { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

#endregion

#region Price Resolution DTOs

/// <summary>
/// Request for price resolution
/// </summary>
public class PriceResolutionRequest
{
    public Guid InventoryItemId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? CustomerId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string? Currency { get; set; }
    public DateTime? AsOfDate { get; set; }
}

/// <summary>
/// Result of price resolution
/// </summary>
public class PriceResolutionResult
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal NetPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public string Currency { get; set; } = "USD";
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Source of the price
    /// </summary>
    public string PriceSource { get; set; } = string.Empty; // PriceList, ItemSupplier, StandardCost, Fallback

    /// <summary>
    /// ID of the price list used
    /// </summary>
    public Guid? PriceListId { get; set; }
    public string? PriceListCode { get; set; }
    public string? PriceListName { get; set; }

    /// <summary>
    /// ID of the price list line used
    /// </summary>
    public Guid? PriceListLineId { get; set; }

    /// <summary>
    /// Resolution path taken
    /// </summary>
    public string ResolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Minimum order quantity from price list
    /// </summary>
    public decimal? MinimumOrderQuantity { get; set; }

    /// <summary>
    /// Lead time from price list
    /// </summary>
    public int? LeadTimeDays { get; set; }

    /// <summary>
    /// Is the price estimated (not from active price list)?
    /// </summary>
    public bool IsEstimated { get; set; }

    /// <summary>
    /// Warnings about the price
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Bulk price resolution request
/// </summary>
public class BulkPriceResolutionRequest
{
    public List<PriceResolutionRequest> Items { get; set; } = new();
    public Guid? SupplierId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Currency { get; set; }
}

#endregion

#region Approval DTOs

/// <summary>
/// DTO for submitting price list for approval
/// </summary>
public class SubmitPriceListApprovalDto
{
    public Guid PriceListId { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// DTO for approving/rejecting price list
/// </summary>
public class PriceListApprovalDecisionDto
{
    public Guid PriceListId { get; set; }
    public bool IsApproved { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// DTO for activating a price list
/// </summary>
public class ActivatePriceListDto
{
    public Guid PriceListId { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public bool SupersedePrevious { get; set; } = true;
}

#endregion

#region Import/Export DTOs

/// <summary>
/// DTO for importing price list from external source
/// </summary>
public class ImportPriceListDto
{
    public string PriceListCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PriceListType Type { get; set; }
    public string Currency { get; set; } = "USD";
    public List<ImportPriceListLineDto> Lines { get; set; } = new();
}

/// <summary>
/// DTO for importing price list line
/// </summary>
public class ImportPriceListLineDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = "EA";
    public decimal BasePrice { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public string? SupplierItemCode { get; set; }
}

/// <summary>
/// Result of price list import
/// </summary>
public class ImportPriceListResultDto
{
    public bool Success { get; set; }
    public Guid? PriceListId { get; set; }
    public int TotalLines { get; set; }
    public int ImportedLines { get; set; }
    public int FailedLines { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

#endregion

