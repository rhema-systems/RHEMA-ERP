using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets
{
    public class CapitalProjectService : ICapitalProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IFixedAssetService _fixedAssetService;
        private readonly IFinancePostingEngine _postingEngine;
        private readonly IFixedAssetDimensionService _fixedAssetDimensions;
        private static readonly FinancePostingProducerContext SettlementProducer =
            new(FinanceDimensionRouteId.FinanceCapitalProjectSettlement);

        public CapitalProjectService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IFixedAssetService fixedAssetService,
            IFinancePostingEngine postingEngine,
            IFixedAssetDimensionService fixedAssetDimensions)
        {
            _context = context;
            _currentUser = currentUser;
            _fixedAssetService = fixedAssetService;
            _postingEngine = postingEngine;
            _fixedAssetDimensions = fixedAssetDimensions;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";

        // ── Queries ──────────────────────────────────────────────────────

        public async Task<IEnumerable<CapitalProjectListDto>> GetAllAsync()
        {
            return await _context.CapitalProjects
                .Where(p => p.TenantId == TenantId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => MapToListDto(p))
                .ToListAsync();
        }

        public async Task<CapitalProjectDetailDto?> GetByIdAsync(Guid id)
        {
            var project = await _context.CapitalProjects
                .Include(p => p.CostLines.OrderByDescending(c => c.TransactionDate))
                .Include(p => p.SettlementRules)
                    .ThenInclude(r => r.TargetFixedAssetCategory)
                .Where(p => p.TenantId == TenantId && p.Id == id)
                .FirstOrDefaultAsync();

            if (project == null)
                return null;
            var result = MapToDetailDto(project);
            var lines = BuildSettlementPreviewLines(project);
            if (lines.Count > 0)
            {
                result.FinanceDimensions = await _fixedAssetDimensions.GetAsync(
                    SettlementProducer,
                    project.Id,
                    project.ActualCompletionDate ?? project.TargetCompletionDate ?? DateTime.UtcNow,
                    lines);
            }
            return result;
        }

        // ── Create / Update / Delete ─────────────────────────────────────

        public async Task<CapitalProjectDetailDto> CreateAsync(CreateCapitalProjectDto dto)
        {
            // Check for duplicate project code
            var exists = await _context.CapitalProjects
                .AnyAsync(p => p.TenantId == TenantId && p.ProjectCode == dto.ProjectCode);
            if (exists)
                throw new InvalidOperationException($"Project code '{dto.ProjectCode}' already exists.");

            var project = new CapitalProject
            {
                TenantId = TenantId,
                ProjectCode = dto.ProjectCode,
                Name = dto.Name,
                Description = dto.Description,
                StartDate = dto.StartDate,
                TargetCompletionDate = dto.TargetCompletionDate,
                TotalBudgetAmount = dto.TotalBudgetAmount,
                Status = ProjectStatus.Planning,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            _context.CapitalProjects.Add(project);
            await SaveWithConcurrencyAsync();

            return await GetByIdAsync(project.Id)
                ?? throw new InvalidOperationException("Failed to create capital project.");
        }

        public async Task<CapitalProjectDetailDto> UpdateAsync(Guid id, UpdateCapitalProjectDto dto)
        {
            var project = await GetProjectOrThrow(id);

            if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                throw new InvalidOperationException($"Cannot update a project in '{project.Status}' status.");

            project.Name = dto.Name;
            project.Description = dto.Description;
            project.TargetCompletionDate = dto.TargetCompletionDate;
            project.TotalBudgetAmount = dto.TotalBudgetAmount;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = UserName;

            await SaveWithConcurrencyAsync();
            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to update.");
        }

        public async Task DeleteAsync(Guid id)
        {
            var project = await GetProjectOrThrow(id);

            if (project.Status != ProjectStatus.Planning)
                throw new InvalidOperationException("Only projects in Planning status can be deleted.");

            _context.CapitalProjects.Remove(project);
            await SaveWithConcurrencyAsync();
        }

        // ── Status ───────────────────────────────────────────────────────

        public async Task<CapitalProjectDetailDto> UpdateStatusAsync(Guid id, UpdateProjectStatusDto dto)
        {
            var project = await GetProjectOrThrow(id);
            ValidateStatusTransition(project.Status, dto.Status);

            project.Status = dto.Status;
            if (dto.Status == ProjectStatus.Cancelled)
                project.ActualCompletionDate = DateTime.UtcNow;

            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = UserName;

            await SaveWithConcurrencyAsync();
            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to update status.");
        }

        // ── Cost Lines (tracking only — no GL) ───────────────────────────

        public async Task<CapitalProjectDetailDto> PostCostToProjectAsync(Guid projectId, AddProjectCostDto dto)
        {
            var project = await GetProjectOrThrow(projectId);

            if (project.Status != ProjectStatus.InProgress)
                throw new InvalidOperationException("Costs can only be posted to projects in InProgress status.");

            if (dto.Amount <= 0)
                throw new InvalidOperationException("Cost amount must be positive.");

            var costLine = new ProjectCostLine
            {
                TenantId = TenantId,
                CapitalProjectId = projectId,
                SourceDocumentType = dto.SourceDocumentType,
                SourceDocumentId = dto.SourceDocumentId,
                SourceDocumentReference = dto.SourceDocumentReference,
                Amount = dto.Amount,
                TransactionDate = dto.TransactionDate,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            _context.ProjectCostLines.Add(costLine);
            project.TotalAccumulatedCost += dto.Amount;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = UserName;

            await SaveWithConcurrencyAsync();
            return await GetByIdAsync(projectId) ?? throw new InvalidOperationException("Failed to post cost.");
        }

        public async Task<CapitalProjectDetailDto> RemoveCostFromProjectAsync(Guid projectId, Guid costLineId)
        {
            var project = await GetProjectOrThrow(projectId);

            if (project.Status == ProjectStatus.Completed)
                throw new InvalidOperationException("Cannot remove costs from a completed project.");

            var costLine = await _context.ProjectCostLines
                .FirstOrDefaultAsync(c => c.TenantId == TenantId && c.Id == costLineId && c.CapitalProjectId == projectId)
                ?? throw new KeyNotFoundException("Cost line not found.");

            project.TotalAccumulatedCost -= costLine.Amount;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = UserName;

            _context.ProjectCostLines.Remove(costLine);
            await SaveWithConcurrencyAsync();

            return await GetByIdAsync(projectId) ?? throw new InvalidOperationException("Failed to remove cost.");
        }

        // ── Settlement Rules ─────────────────────────────────────────────

        public async Task<CapitalProjectDetailDto> AddSettlementRuleAsync(Guid projectId, AddSettlementRuleDto dto)
        {
            var project = await _context.CapitalProjects
                .Include(p => p.SettlementRules)
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == projectId)
                ?? throw new KeyNotFoundException("Capital project not found.");

            if (project.Status == ProjectStatus.Completed || project.Status == ProjectStatus.Cancelled)
                throw new InvalidOperationException("Cannot add rules to a completed/cancelled project.");

            // Validate category exists
            var categoryExists = await _context.FixedAssetCategories
                .AnyAsync(c => c.TenantId == TenantId && c.Id == dto.TargetFixedAssetCategoryId);
            if (!categoryExists)
                throw new InvalidOperationException("Target fixed asset category not found.");

            // Validate total allocation doesn't exceed 100%
            var currentTotal = project.SettlementRules.Sum(r => r.AllocationPercentage);
            if (currentTotal + dto.AllocationPercentage > 100)
                throw new InvalidOperationException(
                    $"Total allocation would be {currentTotal + dto.AllocationPercentage}%. Maximum is 100%.");

            var rule = new ProjectSettlementRule
            {
                TenantId = TenantId,
                CapitalProjectId = projectId,
                TargetFixedAssetCategoryId = dto.TargetFixedAssetCategoryId,
                ProposedAssetName = dto.ProposedAssetName,
                AllocationPercentage = dto.AllocationPercentage,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            _context.ProjectSettlementRules.Add(rule);
            await SaveWithConcurrencyAsync();

            return await GetByIdAsync(projectId) ?? throw new InvalidOperationException("Failed to add rule.");
        }

        public async Task<CapitalProjectDetailDto> RemoveSettlementRuleAsync(Guid projectId, Guid ruleId)
        {
            var project = await GetProjectOrThrow(projectId);

            if (project.Status == ProjectStatus.Completed)
                throw new InvalidOperationException("Cannot remove rules from a completed project.");

            var rule = await _context.ProjectSettlementRules
                .FirstOrDefaultAsync(r => r.TenantId == TenantId && r.Id == ruleId && r.CapitalProjectId == projectId)
                ?? throw new KeyNotFoundException("Settlement rule not found.");

            _context.ProjectSettlementRules.Remove(rule);
            await SaveWithConcurrencyAsync();

            return await GetByIdAsync(projectId) ?? throw new InvalidOperationException("Failed to remove rule.");
        }

        // ── Capitalization ───────────────────────────────────────────────

        public async Task<CapitalProjectDetailDto> CapitalizeProjectAsync(
            Guid projectId,
            CapitalizeCapitalProjectDto? dto = null,
            CancellationToken cancellationToken = default)
        {
            var project = await _context.CapitalProjects
                .Include(p => p.CostLines)
                .Include(p => p.SettlementRules)
                    .ThenInclude(r => r.TargetFixedAssetCategory)
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == projectId, cancellationToken)
                ?? throw new KeyNotFoundException("Capital project not found.");

            if (project.Status == ProjectStatus.Completed)
                return await GetByIdAsync(projectId)
                    ?? throw new InvalidOperationException("Failed to retrieve capitalized project.");

            if (project.Status != ProjectStatus.InProgress)
                throw new InvalidOperationException("Only InProgress projects can be capitalized.");

            if (project.TotalAccumulatedCost <= 0)
                throw new InvalidOperationException("Project has no accumulated costs to capitalize.");

            if (!project.SettlementRules.Any())
                throw new InvalidOperationException("Project has no settlement rules. Define how costs should be allocated.");

            var totalAllocation = project.SettlementRules.Sum(r => r.AllocationPercentage);
            if (totalAllocation != 100)
                throw new InvalidOperationException(
                    $"Settlement rules total {totalAllocation}%. They must sum to exactly 100%.");

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                var postingDate = DateTime.UtcNow;
                var postingLines = new List<FinancePostingLineDto>();
                var orderedRules = project.SettlementRules.OrderBy(rule => rule.Id).ToArray();
                var allocatedSoFar = 0m;

                for (var index = 0; index < orderedRules.Length; index++)
                {
                    var rule = orderedRules[index];
                    var allocatedAmount = index == orderedRules.Length - 1
                        ? project.TotalAccumulatedCost - allocatedSoFar
                        : Math.Round(project.TotalAccumulatedCost * (rule.AllocationPercentage / 100m), 2);
                    allocatedSoFar += allocatedAmount;

                    // Create the fixed asset via existing service
                    var category = rule.TargetFixedAssetCategory;
                    var assetDto = new CreateFixedAssetDto
                    {
                        AssetCode = await _fixedAssetService.GenerateAssetCodeAsync(rule.TargetFixedAssetCategoryId),
                        Name = rule.ProposedAssetName,
                        Description = $"Capitalized from project {project.ProjectCode}",
                        FixedAssetCategoryId = rule.TargetFixedAssetCategoryId,
                        PurchaseDate = postingDate,
                        PurchasePrice = allocatedAmount,
                        AcquisitionCost = allocatedAmount,
                        DepreciationMethod = category.DefaultMethod,
                        UsefulLifeMonths = category.DefaultUsefulLifeMonths,
                        ResidualValue = Math.Round(allocatedAmount * (category.DefaultResidualValuePercent / 100m), 2)
                    };

                    var createdAsset = await _fixedAssetService.CreateAsync(assetDto);
                    rule.AllocatedAmount = allocatedAmount;
                    rule.ResultingFixedAssetId = createdAsset.Id;

                    var aucAccountId = category.AucAccountId
                        ?? throw new InvalidOperationException(
                            $"AUC/CIP account is not configured for category '{category.Name}'.");
                    var description = $"Capitalize {project.ProjectCode} → {rule.ProposedAssetName}";
                    postingLines.Add(new FinancePostingLineDto
                    {
                        AccountId = category.AssetAccountId,
                        SourceDocumentLineId = FinanceSourceLineIdentity.Create(project.Id, "SETTLEMENT-ASSET", rule.Id),
                        DebitAmount = allocatedAmount,
                        Description = description,
                        LineNumber = postingLines.Count + 1
                    });
                    postingLines.Add(new FinancePostingLineDto
                    {
                        AccountId = aucAccountId,
                        SourceDocumentLineId = FinanceSourceLineIdentity.Create(project.Id, "SETTLEMENT-AUC", rule.Id),
                        CreditAmount = allocatedAmount,
                        Description = description,
                        LineNumber = postingLines.Count + 1
                    });
                }

                var functionalCurrency = (await _context.FinanceSettings.AsNoTracking()
                    .Where(settings => settings.TenantId == TenantId)
                    .Select(settings => settings.BaseCurrency)
                    .FirstOrDefaultAsync(cancellationToken) ?? "GHS").Trim().ToUpperInvariant();
                await _fixedAssetDimensions.SynchronizeAsync(
                    SettlementProducer, project.Id, postingDate, postingLines,
                    dto?.FinanceDimensions, inheritedAssetJournalBySourceLine: null,
                    "Capital project settlement dimensions synchronized.", cancellationToken);
                await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                    SettlementProducer, project.Id, postingDate, postingLines, cancellationToken);
                await _postingEngine.PostAsync(new FinancePostingRequestDto
                {
                    SourceModule = SettlementProducer.Definition.PostingSourceModule,
                    OriginModuleCode = SettlementProducer.Definition.ProducerModule,
                    SourceDocumentType = SettlementProducer.Definition.DocumentType,
                    SourceDocumentId = project.Id,
                    SourceDocumentTenantId = TenantId,
                    SourceDocumentReference = project.ProjectCode,
                    Description = $"Capital project capitalization: {project.Name}",
                    PostingDate = postingDate,
                    JournalType = "System Generated",
                    FunctionalCurrencyCode = functionalCurrency,
                    IdempotencyKey = $"CAPITAL-PROJECT-SETTLEMENT:{TenantId:D}:{project.Id:D}",
                    Lines = postingLines
                }, SettlementProducer, cancellationToken);

                // Mark project as completed
                project.CapitalizedAmount = project.TotalAccumulatedCost;
                project.Status = ProjectStatus.Completed;
                project.ActualCompletionDate = DateTime.UtcNow;
                project.UpdatedAt = DateTime.UtcNow;
                project.UpdatedBy = UserName;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException(
                        "The project was modified by another user. Please refresh and try again.");
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });

            return await GetByIdAsync(projectId)
                ?? throw new InvalidOperationException("Failed to retrieve capitalized project.");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private async Task<CapitalProject> GetProjectOrThrow(Guid id)
        {
            return await _context.CapitalProjects
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == id)
                ?? throw new KeyNotFoundException("Capital project not found.");
        }

        private async Task SaveWithConcurrencyAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "The record was modified by another user. Please refresh and try again.");
            }
        }

        private static List<FinancePostingLineDto> BuildSettlementPreviewLines(CapitalProject project)
        {
            var rules = project.SettlementRules.OrderBy(item => item.Id).ToArray();
            if (rules.Length == 0 || project.TotalAccumulatedCost <= 0m)
                return [];
            var lines = new List<FinancePostingLineDto>();
            var allocated = 0m;
            for (var index = 0; index < rules.Length; index++)
            {
                var rule = rules[index];
                var amount = rule.AllocatedAmount > 0m
                    ? rule.AllocatedAmount
                    : index == rules.Length - 1
                        ? project.TotalAccumulatedCost - allocated
                        : Math.Round(project.TotalAccumulatedCost * (rule.AllocationPercentage / 100m), 2);
                allocated += amount;
                var category = rule.TargetFixedAssetCategory;
                if (category?.AucAccountId is null)
                    continue;
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = category.AssetAccountId,
                    SourceDocumentLineId = FinanceSourceLineIdentity.Create(project.Id, "SETTLEMENT-ASSET", rule.Id),
                    DebitAmount = amount
                });
                lines.Add(new FinancePostingLineDto
                {
                    AccountId = category.AucAccountId.Value,
                    SourceDocumentLineId = FinanceSourceLineIdentity.Create(project.Id, "SETTLEMENT-AUC", rule.Id),
                    CreditAmount = amount
                });
            }
            return lines;
        }

        private static void ValidateStatusTransition(ProjectStatus current, ProjectStatus target)
        {
            var valid = (current, target) switch
            {
                (ProjectStatus.Planning, ProjectStatus.InProgress) => true,
                (ProjectStatus.Planning, ProjectStatus.Cancelled) => true,
                (ProjectStatus.InProgress, ProjectStatus.OnHold) => true,
                (ProjectStatus.InProgress, ProjectStatus.Cancelled) => true,
                (ProjectStatus.OnHold, ProjectStatus.InProgress) => true,
                (ProjectStatus.OnHold, ProjectStatus.Cancelled) => true,
                _ => false
            };

            if (!valid)
                throw new InvalidOperationException(
                    $"Cannot transition from '{current}' to '{target}'.");
        }

        private static CapitalProjectListDto MapToListDto(CapitalProject p) => new()
        {
            Id = p.Id,
            ProjectCode = p.ProjectCode,
            Name = p.Name,
            StartDate = p.StartDate,
            TargetCompletionDate = p.TargetCompletionDate,
            TotalBudgetAmount = p.TotalBudgetAmount,
            TotalAccumulatedCost = p.TotalAccumulatedCost,
            CapitalizedAmount = p.CapitalizedAmount,
            Status = p.Status,
            CreatedAt = p.CreatedAt
        };

        private static CapitalProjectDetailDto MapToDetailDto(CapitalProject p) => new()
        {
            Id = p.Id,
            ProjectCode = p.ProjectCode,
            Name = p.Name,
            Description = p.Description,
            StartDate = p.StartDate,
            TargetCompletionDate = p.TargetCompletionDate,
            ActualCompletionDate = p.ActualCompletionDate,
            TotalBudgetAmount = p.TotalBudgetAmount,
            TotalAccumulatedCost = p.TotalAccumulatedCost,
            CapitalizedAmount = p.CapitalizedAmount,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy,
            UpdatedAt = p.UpdatedAt,
            UpdatedBy = p.UpdatedBy,
            CostLines = p.CostLines.Select(c => new ProjectCostLineDto
            {
                Id = c.Id,
                SourceDocumentType = c.SourceDocumentType,
                SourceDocumentId = c.SourceDocumentId,
                SourceDocumentReference = c.SourceDocumentReference,
                Amount = c.Amount,
                TransactionDate = c.TransactionDate,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                CreatedBy = c.CreatedBy
            }).ToList(),
            SettlementRules = p.SettlementRules.Select(r => new ProjectSettlementRuleDto
            {
                Id = r.Id,
                TargetFixedAssetCategoryId = r.TargetFixedAssetCategoryId,
                TargetFixedAssetCategoryName = r.TargetFixedAssetCategory?.Name,
                ProposedAssetName = r.ProposedAssetName,
                AllocationPercentage = r.AllocationPercentage,
                AllocatedAmount = r.AllocatedAmount,
                ResultingFixedAssetId = r.ResultingFixedAssetId
            }).ToList()
        };
    }
}
