using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets
{
    public class CapitalProjectService : ICapitalProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IFixedAssetService _fixedAssetService;

        public CapitalProjectService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IFixedAssetService fixedAssetService)
        {
            _context = context;
            _currentUser = currentUser;
            _fixedAssetService = fixedAssetService;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
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

            return project == null ? null : MapToDetailDto(project);
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

        public async Task<CapitalProjectDetailDto> CapitalizeProjectAsync(Guid projectId)
        {
            var project = await _context.CapitalProjects
                .Include(p => p.CostLines)
                .Include(p => p.SettlementRules)
                    .ThenInclude(r => r.TargetFixedAssetCategory)
                .FirstOrDefaultAsync(p => p.TenantId == TenantId && p.Id == projectId)
                ?? throw new KeyNotFoundException("Capital project not found.");

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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var journalLines = new List<(Guid DebitAccountId, Guid CreditAccountId, decimal Amount, string Description)>();

                foreach (var rule in project.SettlementRules)
                {
                    var allocatedAmount = Math.Round(project.TotalAccumulatedCost * (rule.AllocationPercentage / 100m), 2);

                    // Create the fixed asset via existing service
                    var category = rule.TargetFixedAssetCategory;
                    var assetDto = new CreateFixedAssetDto
                    {
                        AssetCode = await _fixedAssetService.GenerateAssetCodeAsync(rule.TargetFixedAssetCategoryId),
                        Name = rule.ProposedAssetName,
                        Description = $"Capitalized from project {project.ProjectCode}",
                        FixedAssetCategoryId = rule.TargetFixedAssetCategoryId,
                        PurchaseDate = DateTime.UtcNow,
                        PurchasePrice = allocatedAmount,
                        AcquisitionCost = allocatedAmount,
                        DepreciationMethod = category.DefaultMethod,
                        UsefulLifeMonths = category.DefaultUsefulLifeMonths,
                        ResidualValue = Math.Round(allocatedAmount * (category.DefaultResidualValuePercent / 100m), 2)
                    };

                    var createdAsset = await _fixedAssetService.CreateAsync(assetDto);
                    rule.AllocatedAmount = allocatedAmount;
                    rule.ResultingFixedAssetId = createdAsset.Id;

                    // Prepare GL: DR Asset Account, CR AUC Account (if RevaluationSurplusAccountId is available as fallback)
                    if (category.RevaluationSurplusAccountId.HasValue)
                    {
                        journalLines.Add((
                            category.AssetAccountId,
                            category.RevaluationSurplusAccountId.Value,
                            allocatedAmount,
                            $"Capitalize {project.ProjectCode} → {rule.ProposedAssetName}"
                        ));
                    }
                }

                // Post consolidated GL journal if we have lines
                if (journalLines.Any())
                {
                    var journal = new JournalEntry
                    {
                        TenantId = TenantId,
                        EntryDate = DateTime.UtcNow,
                        ReferenceNumber = $"AUC-CAP-{project.ProjectCode}",
                        Description = $"Capital project capitalization: {project.Name}",
                        PostingStatus = "Posted",
                        JournalType = "System Generated",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };

                    foreach (var line in journalLines)
                    {
                        journal.Transactions.Add(new AccountTransaction
                        {
                            TenantId = TenantId,
                            AccountId = line.DebitAccountId,
                            TransactionDate = DateTime.UtcNow,
                            DebitAmount = line.Amount,
                            CreditAmount = 0,
                            Description = line.Description,
                            CreatedAt = DateTime.UtcNow
                        });

                        journal.Transactions.Add(new AccountTransaction
                        {
                            TenantId = TenantId,
                            AccountId = line.CreditAccountId,
                            TransactionDate = DateTime.UtcNow,
                            DebitAmount = 0,
                            CreditAmount = line.Amount,
                            Description = line.Description,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    _context.JournalEntries.Add(journal);
                }

                // Mark project as completed
                project.CapitalizedAmount = project.TotalAccumulatedCost;
                project.Status = ProjectStatus.Completed;
                project.ActualCompletionDate = DateTime.UtcNow;
                project.UpdatedAt = DateTime.UtcNow;
                project.UpdatedBy = UserName;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    "The project was modified by another user. Please refresh and try again.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

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
