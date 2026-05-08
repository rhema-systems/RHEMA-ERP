using ErpSystem.Core.Entities.Finance.FixedAssets;

namespace ErpSystem.Core.DTOs.Finance
{
    // ── Capital Project DTOs ─────────────────────────────────────────────

    public class CapitalProjectListDto
    {
        public Guid Id { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? TargetCompletionDate { get; set; }
        public decimal TotalBudgetAmount { get; set; }
        public decimal TotalAccumulatedCost { get; set; }
        public decimal CapitalizedAmount { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CapitalProjectDetailDto : CapitalProjectListDto
    {
        public string? Description { get; set; }
        public DateTime? ActualCompletionDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public List<ProjectCostLineDto> CostLines { get; set; } = new();
        public List<ProjectSettlementRuleDto> SettlementRules { get; set; } = new();
    }

    public class ProjectCostLineDto
    {
        public Guid Id { get; set; }
        public ProjectCostSourceType SourceDocumentType { get; set; }
        public Guid? SourceDocumentId { get; set; }
        public string? SourceDocumentReference { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class ProjectSettlementRuleDto
    {
        public Guid Id { get; set; }
        public Guid TargetFixedAssetCategoryId { get; set; }
        public string? TargetFixedAssetCategoryName { get; set; }
        public string ProposedAssetName { get; set; } = string.Empty;
        public decimal AllocationPercentage { get; set; }
        public decimal AllocatedAmount { get; set; }
        public Guid? ResultingFixedAssetId { get; set; }
    }

    public class CreateCapitalProjectDto
    {
        public string ProjectCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? TargetCompletionDate { get; set; }
        public decimal TotalBudgetAmount { get; set; }
    }

    public class UpdateCapitalProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? TargetCompletionDate { get; set; }
        public decimal TotalBudgetAmount { get; set; }
    }

    public class UpdateProjectStatusDto
    {
        public ProjectStatus Status { get; set; }
        public string? Reason { get; set; }
    }

    public class AddProjectCostDto
    {
        public ProjectCostSourceType SourceDocumentType { get; set; } = ProjectCostSourceType.ManualJournal;
        public Guid? SourceDocumentId { get; set; }
        public string? SourceDocumentReference { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Description { get; set; }
    }

    public class AddSettlementRuleDto
    {
        public Guid TargetFixedAssetCategoryId { get; set; }
        public string ProposedAssetName { get; set; } = string.Empty;
        public decimal AllocationPercentage { get; set; }
    }
}
