using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Api.Services.Finance.FixedAssets
{
    public class CapitalProjectService : ICapitalProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IFixedAssetService _fixedAssetService;
        private readonly IFinancePostingEngine _postingEngine;
        private readonly IFixedAssetDimensionService _fixedAssetDimensions;
        private readonly IWorkflowService? _workflowService;
        private readonly IFinanceSourceBookAuthorityService? _sourceBookAuthority;
        private static readonly FinancePostingProducerContext SettlementProducer =
            new(FinanceDimensionRouteId.FinanceCapitalProjectSettlement);

        public CapitalProjectService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IFixedAssetService fixedAssetService,
            IFinancePostingEngine postingEngine,
            IFixedAssetDimensionService fixedAssetDimensions,
            IWorkflowService? workflowService = null,
            IFinanceSourceBookAuthorityService? sourceBookAuthority = null)
        {
            _context = context;
            _currentUser = currentUser;
            _fixedAssetService = fixedAssetService;
            _postingEngine = postingEngine;
            _fixedAssetDimensions = fixedAssetDimensions;
            _workflowService = workflowService;
            _sourceBookAuthority = sourceBookAuthority;
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

            if (project.Status is not (ProjectStatus.Planning or ProjectStatus.InProgress or ProjectStatus.OnHold))
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
            if (_sourceBookAuthority is null)
                throw new InvalidOperationException("Capital-project source-book authority is not configured.");
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                : null;
            var project = await GetProjectOrThrow(projectId);

            if (project.Status != ProjectStatus.InProgress)
                throw new InvalidOperationException("Costs can only be posted to projects in InProgress status.");

            if (dto.Amount <= 0)
                throw new InvalidOperationException("Cost amount must be positive.");
            if (!dto.SourceDocumentId.HasValue || dto.SourceDocumentId == Guid.Empty)
                throw new InvalidOperationException("A capital-project cost requires an exact posted source document.");

            var source = await ResolveCostSourcePostingAsync(dto, CancellationToken.None);
            var authority = await _sourceBookAuthority.RetainExistingPostedOriginalAsync(
                new FinanceSourceBookAuthorityFreezeRequest
                {
                    OriginModuleCode = FinanceModuleLockCatalog.ResolveOriginModuleCode(
                        source.PostingEvent.SourceModule, source.PostingEvent.OriginModuleCode),
                    SourceDocumentType = source.PostingEvent.SourceDocumentType,
                    SourceDocumentId = source.PostingEvent.SourceDocumentId,
                    PostingAction = source.PostingEvent.PostingAction,
                    EffectiveDate = source.Journal.EntryDate.Date,
                    TransactionCurrencyCode = source.PostingEvent.PrimaryTransactionCurrencyCode
                        ?? source.PostingEvent.FunctionalCurrencyCode,
                    FreezeStage = FinanceSourceBookAuthorityFreezeStages.LegacyPosted
                },
                source.Journal.Id,
                source.PostingEvent.Id);

            var costLine = new ProjectCostLine
            {
                TenantId = TenantId,
                CapitalProjectId = projectId,
                SourceDocumentType = dto.SourceDocumentType,
                SourceDocumentId = dto.SourceDocumentId,
                SourceDocumentReference = dto.SourceDocumentReference,
                SourceFinancePostingEventId = source.PostingEvent.Id,
                SourceBookAuthorityId = authority.AuthorityId,
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
            if (transaction is not null)
                await transaction.CommitAsync();
            return await GetByIdAsync(projectId) ?? throw new InvalidOperationException("Failed to post cost.");
        }

        public async Task<CapitalProjectDetailDto> SubmitForCapitalizationApprovalAsync(
            Guid id, CancellationToken cancellationToken = default)
        {
            if (_workflowService is null || _sourceBookAuthority is null)
                throw new InvalidOperationException("Capital-project workflow and source-book authority are required.");
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var project = await _context.CapitalProjects.Include(item => item.CostLines)
                .Include(item => item.SettlementRules)
                .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted,
                    cancellationToken) ?? throw new KeyNotFoundException("Capital project not found.");
            if (project.Status == ProjectStatus.PendingApproval &&
                project.CapitalizationWorkflowInstanceId.HasValue &&
                project.CapitalizationSourceBookAuthorityId.HasValue)
            {
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return await GetByIdAsync(id) ?? throw new InvalidOperationException("Capital project not found.");
            }
            if (project.Status is not (ProjectStatus.InProgress or ProjectStatus.OnHold))
                throw new InvalidOperationException("Only an active capital project can be submitted for capitalization approval.");
            if (project.CostLines.Count == 0 || project.CostLines.Any(item =>
                    !item.SourceBookAuthorityId.HasValue || !item.SourceFinancePostingEventId.HasValue))
                throw new InvalidOperationException("Every capital-project cost must retain exact posted source authority before submission.");
            if (project.SettlementRules.Count == 0 ||
                project.SettlementRules.Sum(item => item.AllocationPercentage) != 100m)
                throw new InvalidOperationException("Capital-project settlement rules must total exactly 100% before submission.");

            var evidenceHash = CapitalizationEvidenceHash(project);
            project.Status = ProjectStatus.PendingApproval;
            project.CapitalizationEvidenceHash = evidenceHash;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = UserName;
            await _context.SaveChangesAsync(cancellationToken);
            var workflow = await _workflowService.StartApprovalWorkflowAsync("CapitalProject", project.Id);
            if (!workflow.Success || !workflow.WorkflowInstanceId.HasValue)
                throw new InvalidOperationException(workflow.Message ?? "Capital-project workflow could not be started.");

            var origins = project.CostLines.Select(item => new FinanceSourceBookAuthorityOriginRequest
            {
                OriginAuthorityId = item.SourceBookAuthorityId!.Value,
                Role = $"PROJECT_COST:{item.Id:N}"
            }).ToArray();
            var origin = await _sourceBookAuthority.RequireBoundOriginalAsync(origins[0].OriginAuthorityId, cancellationToken);
            var frozen = await _sourceBookAuthority.FreezeInheritedAsync(
                new FinanceSourceBookAuthorityFreezeRequest
                {
                    OriginModuleCode = FinanceModuleLockCatalog.Finance,
                    SourceDocumentType = SettlementProducer.Definition.DocumentType,
                    SourceDocumentId = project.Id,
                    PostingAction = "Capitalize",
                    EffectiveDate = DateTime.UtcNow.Date,
                    TransactionCurrencyCode = origin.FunctionalCurrencyCode,
                    FreezeStage = FinanceSourceBookAuthorityFreezeStages.Submitted,
                    SourceWorkflowInstanceId = workflow.WorkflowInstanceId,
                    SourceWorkflowEntityType = "CapitalProject"
                }, origins, cancellationToken);
            project.CapitalizationWorkflowInstanceId = workflow.WorkflowInstanceId;
            project.CapitalizationSourceBookAuthorityId = frozen.AuthorityId;
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Capital project not found.");
        }

        public async Task<CapitalProjectDetailDto> RemoveCostFromProjectAsync(Guid projectId, Guid costLineId)
        {
            var project = await GetProjectOrThrow(projectId);

            if (project.Status is not (ProjectStatus.Planning or ProjectStatus.InProgress or ProjectStatus.OnHold))
                throw new InvalidOperationException(
                    "Costs cannot be removed after a capital project is submitted for approval.");

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

            if (project.Status is not (ProjectStatus.Planning or ProjectStatus.InProgress or ProjectStatus.OnHold))
                throw new InvalidOperationException(
                    "Settlement rules cannot be added after a capital project is submitted for approval.");

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

            if (project.Status is not (ProjectStatus.Planning or ProjectStatus.InProgress or ProjectStatus.OnHold))
                throw new InvalidOperationException(
                    "Settlement rules cannot be removed after a capital project is submitted for approval.");

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
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _context.Database.IsRelational()
                    ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                    : await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var project = await _context.CapitalProjects
                        .Include(p => p.CostLines)
                        .Include(p => p.SettlementRules)
                            .ThenInclude(r => r.TargetFixedAssetCategory)
                        .SingleOrDefaultAsync(p => p.TenantId == TenantId && p.Id == projectId, cancellationToken)
                        ?? throw new KeyNotFoundException("Capital project not found.");
                    if (project.Status == ProjectStatus.Completed)
                    {
                        await EnsureCompletedCapitalizationIsConsistentAsync(project, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return;
                    }
                    if (project.Status != ProjectStatus.Approved)
                        throw new InvalidOperationException(
                            "Only independently approved capital projects can be capitalized.");
                    if (project.TotalAccumulatedCost <= 0m)
                        throw new InvalidOperationException("Project has no accumulated costs to capitalize.");
                    if (!project.SettlementRules.Any())
                        throw new InvalidOperationException("Project has no settlement rules. Define how costs should be allocated.");
                    if (project.SettlementRules.Any(rule => rule.ResultingFixedAssetId.HasValue || rule.AllocatedAmount != 0m))
                        throw new InvalidOperationException("The project contains partial capitalization state and cannot be retried safely.");
                    if (dto?.FinanceDimensions is not null)
                        throw new InvalidOperationException(
                            "Capital-project settlement dimensions are inherited exclusively from frozen posted CWC evidence and cannot be overridden.");
                    var totalAllocation = project.SettlementRules.Sum(rule => rule.AllocationPercentage);
                    if (totalAllocation != 100m)
                        throw new InvalidOperationException(
                            $"Settlement rules total {totalAllocation}%. They must sum to exactly 100%.");

                    if (_sourceBookAuthority is null || !project.CapitalizationWorkflowInstanceId.HasValue ||
                        !project.CapitalizationSourceBookAuthorityId.HasValue ||
                        string.IsNullOrWhiteSpace(project.CapitalizationEvidenceHash) ||
                        !string.Equals(project.CapitalizationEvidenceHash, CapitalizationEvidenceHash(project), StringComparison.Ordinal))
                        throw new InvalidOperationException("Capital-project approval evidence is missing or no longer matches the immutable cost and settlement plan.");
                    var retainedAuthority = await _context.FinanceSourceBookAuthorities.AsNoTracking()
                        .SingleOrDefaultAsync(item => item.TenantId == TenantId &&
                            item.Id == project.CapitalizationSourceBookAuthorityId.Value && !item.IsDeleted,
                            cancellationToken)
                        ?? throw new InvalidOperationException("Capital-project frozen source-book authority was not found.");
                    var frozen = await _sourceBookAuthority.RequireForPostingAsync(
                        new FinanceSourceBookAuthorityFreezeRequest
                        {
                            OriginModuleCode = FinanceModuleLockCatalog.Finance,
                            SourceDocumentType = SettlementProducer.Definition.DocumentType,
                            SourceDocumentId = project.Id,
                            PostingAction = "Capitalize",
                            EffectiveDate = retainedAuthority.EffectiveDate,
                            TransactionCurrencyCode = retainedAuthority.TransactionCurrencyCode,
                            FreezeStage = FinanceSourceBookAuthorityFreezeStages.Authorized,
                            SourceWorkflowInstanceId = project.CapitalizationWorkflowInstanceId,
                            SourceWorkflowEntityType = "CapitalProject"
                        }, cancellationToken);
                    if (frozen.AuthorityId != project.CapitalizationSourceBookAuthorityId.Value)
                        throw new InvalidOperationException("Capital-project posting did not resolve its retained exact authority.");
                    var postingDate = frozen.EffectiveDate.Date;
                    var authority = await ResolveCapitalizationAuthorityAsync(project, postingDate, cancellationToken);
                    if (authority.AccountingBookId != frozen.AccountingBookId ||
                        !string.Equals(authority.AccountingBookCode, frozen.AccountingBookCode, StringComparison.Ordinal) ||
                        !string.Equals(authority.FunctionalCurrencyCode, frozen.FunctionalCurrencyCode, StringComparison.Ordinal))
                        throw new InvalidOperationException("Capital-project source evidence differs from the frozen exact-book authority.");
                    var postingLines = new List<FinancePostingLineDto>();
                    var createdAssets = new List<CreatedProjectAsset>();
                    var inheritedJournalBySourceLine = new Dictionary<Guid, Guid>();
                    var orderedRules = project.SettlementRules.OrderBy(rule => rule.Id).ToArray();
                    var allocatedSoFar = 0m;
                    for (var index = 0; index < orderedRules.Length; index++)
                    {
                        var rule = orderedRules[index];
                        var category = rule.TargetFixedAssetCategory
                            ?? throw new InvalidOperationException("A settlement rule is missing its fixed asset category.");
                        if (category.TenantId != TenantId || category.IsDeleted)
                            throw new InvalidOperationException("A settlement-rule category is stale or belongs to another tenant.");
                        var allocatedAmount = index == orderedRules.Length - 1
                            ? project.TotalAccumulatedCost - allocatedSoFar
                            : Math.Round(project.TotalAccumulatedCost * (rule.AllocationPercentage / 100m), 2,
                                MidpointRounding.AwayFromZero);
                        if (allocatedAmount <= 0m)
                            throw new InvalidOperationException("Every capitalization settlement must allocate a positive amount.");
                        allocatedSoFar += allocatedAmount;

                        var sourceLineId = FinanceSourceLineIdentity.Create(project.Id, "SETTLEMENT-ASSET", rule.Id);
                        postingLines.Add(new FinancePostingLineDto
                        {
                            AccountId = category.AssetAccountId,
                            SourceDocumentLineId = sourceLineId,
                            DebitAmount = allocatedAmount,
                            TransactionCurrency = authority.FunctionalCurrencyCode,
                            TransactionDebitAmount = allocatedAmount,
                            Description = $"Capitalize {project.ProjectCode} → {rule.ProposedAssetName}",
                            SourceReferenceNumber = project.ProjectCode,
                            LineNumber = postingLines.Count + 1
                        });
                        inheritedJournalBySourceLine[sourceLineId] = authority.DimensionAuthorityJournalEntryId;
                        var createdAsset = await _fixedAssetService.CreateAsync(new CreateFixedAssetDto
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
                            ResidualValue = Math.Round(
                                allocatedAmount * (category.DefaultResidualValuePercent / 100m), 2,
                                MidpointRounding.AwayFromZero)
                        });
                        rule.AllocatedAmount = allocatedAmount;
                        rule.ResultingFixedAssetId = createdAsset.Id;
                        createdAssets.Add(new CreatedProjectAsset(
                            createdAsset.Id,
                            sourceLineId,
                            category.AssetAccountId,
                            allocatedAmount,
                            category.DefaultResidualValuePercent));
                    }

                    foreach (var credit in authority.Credits.OrderBy(item => item.JournalEntryId).ThenBy(item => item.AccountId))
                    {
                        var sourceLineId = FinanceSourceLineIdentity.Create(
                            project.Id, "SETTLEMENT-CWC", credit.JournalEntryId, credit.AccountId);
                        postingLines.Add(new FinancePostingLineDto
                        {
                            AccountId = credit.AccountId,
                            SourceDocumentLineId = sourceLineId,
                            CreditAmount = credit.Amount,
                            TransactionCurrency = authority.FunctionalCurrencyCode,
                            TransactionCreditAmount = credit.Amount,
                            Description = $"Clear posted CWC cost for {project.ProjectCode}",
                            SourceReferenceNumber = project.ProjectCode,
                            LineNumber = postingLines.Count + 1
                        });
                        inheritedJournalBySourceLine[sourceLineId] = credit.JournalEntryId;
                    }
                    if (postingLines.Sum(line => line.DebitAmount) != postingLines.Sum(line => line.CreditAmount))
                        throw new InvalidOperationException("Capital-project settlement source evidence does not balance.");

                    await _fixedAssetDimensions.SynchronizeAsync(
                        SettlementProducer, project.Id, postingDate, postingLines,
                        null, inheritedJournalBySourceLine,
                        "Capital project settlement dimensions inherited from posted CWC evidence.", cancellationToken);
                    await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                        SettlementProducer, project.Id, postingDate, postingLines, cancellationToken);
                    var posting = await _postingEngine.PostAsync(new FinancePostingRequestV2Dto
                    {
                        SourceModule = SettlementProducer.Definition.PostingSourceModule,
                        OriginModuleCode = FinanceModuleLockCatalog.ResolveOriginModuleCode(
                            SettlementProducer.Definition.ProducerModule),
                        SourceDocumentType = SettlementProducer.Definition.DocumentType,
                        SourceDocumentId = project.Id,
                        SourceDocumentTenantId = TenantId,
                        SourceDocumentReference = project.ProjectCode,
                        Description = $"Capital project capitalization: {project.Name}",
                        PostingDate = postingDate,
                        PostingAction = "Capitalize",
                        JournalType = "System Generated",
                        AccountingBookCode = authority.AccountingBookCode,
                        FunctionalCurrencyCode = authority.FunctionalCurrencyCode,
                        IdempotencyKey = $"CAPITAL-PROJECT-SETTLEMENT:{TenantId:D}:{project.Id:D}",
                        ReturnExistingOnDuplicate = true,
                        Lines = postingLines
                    }, SettlementProducer, cancellationToken);
                    await _sourceBookAuthority.BindOriginalPostingAsync(
                        frozen.AuthorityId, posting.PostingEventId, posting.JournalEntryId, cancellationToken);
                    var capitalizedAt = posting.PostingDate == default ? postingDate : posting.PostingDate.Date;
                    var representations = await ResolveCapitalizationRepresentationsAsync(
                        project, authority, posting, cancellationToken);
                    foreach (var created in createdAssets)
                        await ApplyProjectCapitalizationAsync(
                            project, created, authority, posting, representations, capitalizedAt, cancellationToken);
                    project.CapitalizedAmount = project.TotalAccumulatedCost;
                    project.Status = ProjectStatus.Completed;
                    project.ActualCompletionDate = capitalizedAt;
                    project.UpdatedAt = DateTime.UtcNow;
                    project.UpdatedBy = UserName;
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    throw new InvalidOperationException(
                        "The project was modified by another user. Please refresh and try again.", exception);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });

            return await GetByIdAsync(projectId)
                ?? throw new InvalidOperationException("Failed to retrieve capitalized project.");
        }

        private async Task<CapitalizationAuthority> ResolveCapitalizationAuthorityAsync(
            CapitalProject project,
            DateTime postingDate,
            CancellationToken cancellationToken)
        {
            if (project.CostLines.Count == 0 || project.CostLines.Any(line =>
                    line.Amount <= 0m || !line.SourceDocumentId.HasValue || line.SourceDocumentId == Guid.Empty ||
                    !line.SourceFinancePostingEventId.HasValue || !line.SourceBookAuthorityId.HasValue))
                throw new InvalidOperationException(
                    "Every capital-project cost requires positive, immutable source-document evidence.");
            if (project.CostLines.Sum(line => line.Amount) != project.TotalAccumulatedCost)
                throw new InvalidOperationException(
                    "Capital-project accumulated cost no longer agrees with its source cost lines.");

            var aucAccountIds = project.SettlementRules.Select(rule =>
                    rule.TargetFixedAssetCategory?.AucAccountId
                    ?? throw new InvalidOperationException(
                        $"AUC/CIP account is not configured for settlement rule '{rule.ProposedAssetName}'."))
                .Distinct()
                .ToArray();
            var accountIds = aucAccountIds.Concat(project.SettlementRules
                    .Select(rule => rule.TargetFixedAssetCategory!.AssetAccountId))
                .Distinct()
                .ToArray();
            var validAccounts = await _context.Accounts.AsNoTracking()
                .Where(account => account.TenantId == TenantId && accountIds.Contains(account.Id) &&
                    !account.IsDeleted && account.Status == AccountStatus.Active &&
                    account.AccountType == AccountType.Asset)
                .Select(account => account.Id)
                .ToListAsync(cancellationToken);
            if (validAccounts.Count != accountIds.Length)
                throw new InvalidOperationException(
                    "A capital-project fixed-asset or CWC account is stale, inactive, invalid, or belongs to another tenant.");

            Guid? accountingBookId = null;
            string? accountingBookCode = null;
            string? functionalCurrency = null;
            (Guid? SetId, Guid? SnapshotId)? dimensionAuthority = null;
            Guid? dimensionAuthorityJournalId = null;
            var credits = new List<CapitalizationCreditAuthority>();
            foreach (var group in project.CostLines.GroupBy(line =>
                         new { line.SourceDocumentType, SourceDocumentId = line.SourceDocumentId!.Value }))
            {
                var query = _context.JournalEntries.AsNoTracking()
                    .Include(journal => journal.AccountingBook)
                    .Include(journal => journal.Transactions)
                    .Where(journal => journal.TenantId == TenantId &&
                        journal.PostingStatus == "Posted" && !journal.IsReversed && !journal.IsDeleted &&
                        journal.ReplicatedFromJournalEntryId == null);
                query = group.Key.SourceDocumentType == ProjectCostSourceType.ManualJournal
                    ? query.Where(journal => journal.Id == group.Key.SourceDocumentId)
                    : query.Where(journal => journal.SourceDocumentId == group.Key.SourceDocumentId &&
                        journal.SourceDocumentType == group.Key.SourceDocumentType.ToString());
                var sourceEventIds = group.Select(item => item.SourceFinancePostingEventId!.Value).Distinct().ToArray();
                var exactEvents = await _context.FinancePostingEvents.AsNoTracking().Where(item =>
                    item.TenantId == TenantId && sourceEventIds.Contains(item.Id) && !item.IsDeleted &&
                    item.PostingStatus == "Posted" && item.JournalEntryId.HasValue).ToListAsync(cancellationToken);
                if (exactEvents.Count != sourceEventIds.Length || exactEvents.Select(item => item.JournalEntryId).Distinct().Count() != 1)
                    throw new InvalidOperationException("Capital-project cost evidence must retain exact, unambiguous posted events.");
                var exactJournalId = exactEvents[0].JournalEntryId!.Value;
                query = query.Where(item => item.Id == exactJournalId);
                var journals = await query.Take(2).ToListAsync(cancellationToken);
                if (journals.Count != 1)
                    throw new InvalidOperationException(
                        "Capital-project source evidence must resolve to exactly one unreversed posted journal.");

                var journal = journals[0];
                if (journal.EntryDate.Date > postingDate.Date ||
                    group.Any(line => line.TransactionDate.Date != journal.EntryDate.Date))
                    throw new InvalidOperationException(
                        "Capital-project cost dates are stale or inconsistent with the posted source journal.");
                if (group.Any(line => !string.IsNullOrWhiteSpace(line.SourceDocumentReference) &&
                        !string.Equals(line.SourceDocumentReference.Trim(), journal.ReferenceNumber?.Trim(),
                            StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        "Capital-project source references do not match the posted journal authority.");

                var book = journal.AccountingBook;
                if (book is null || book.TenantId != TenantId || book.Id != journal.AccountingBookId ||
                    !book.IsActive || !book.AllowsPosting || book.IsDeleted ||
                    book.BookType != AccountingBookType.PrimaryFull ||
                    book.LifecycleStatus != AccountingBookLifecycleStatus.Active ||
                    !IsCanonicalExactBookCode(book.Code) ||
                    !string.Equals(book.Code, journal.BookClassification, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Capital-project source journal book evidence is stale or inconsistent.");
                if (accountingBookId.HasValue &&
                    (accountingBookId != book.Id || !string.Equals(accountingBookCode, book.Code, StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        "Capital-project cost sources span inconsistent accounting books.");
                accountingBookId ??= book.Id;
                accountingBookCode ??= book.Code;

                var sourceLines = journal.Transactions.Where(line =>
                        !line.IsDeleted && !line.IsReversed && line.PostingStatus == "Posted" &&
                        aucAccountIds.Contains(line.AccountId) && line.DebitAmount > line.CreditAmount)
                    .ToArray();
                var expectedAmount = group.Sum(line => line.Amount);
                if (sourceLines.Length == 0 ||
                    sourceLines.Sum(line => line.DebitAmount - line.CreditAmount) != expectedAmount)
                    throw new InvalidOperationException(
                        "Capital-project source cost does not equal the posted CWC debit evidence.");
                if (sourceLines.Any(line => line.TenantId != TenantId ||
                        line.AccountingBookId != book.Id ||
                        !string.Equals(line.BookClassification, book.Code, StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        "Capital-project CWC lines do not retain the source journal's tenant and exact-book authority.");

                var currencies = sourceLines.Select(line => line.FunctionalCurrencyCode).Distinct().ToArray();
                if (currencies.Length != 1 || !IsCanonicalCurrency(currencies[0]) ||
                    !string.Equals(currencies[0], book.FunctionalCurrencyCode, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Capital-project CWC source currency authority is missing or inconsistent with its exact book.");
                if (functionalCurrency is not null &&
                    !string.Equals(functionalCurrency, currencies[0], StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Capital-project cost sources use inconsistent functional currencies.");
                functionalCurrency ??= currencies[0];

                var sourceDimensionKeys = sourceLines
                    .Select(line => (line.FinanceDimensionSetId, line.FinanceDimensionSnapshotId))
                    .Distinct()
                    .ToArray();
                if (sourceDimensionKeys.Length != 1 ||
                    sourceDimensionKeys[0].FinanceDimensionSetId.HasValue !=
                        sourceDimensionKeys[0].FinanceDimensionSnapshotId.HasValue ||
                    dimensionAuthority.HasValue && dimensionAuthority.Value != sourceDimensionKeys[0])
                    throw new InvalidOperationException(
                        "Capital-project CWC source dimensions are inconsistent and cannot be inherited safely.");
                var dimensionLoaderLine = journal.Transactions
                    .Where(line => !line.IsDeleted && line.FinanceDimensionSetId.HasValue)
                    .OrderByDescending(line => line.DebitAmount > 0m)
                    .ThenBy(line => line.LineNumber)
                    .FirstOrDefault();
                if (sourceDimensionKeys[0].FinanceDimensionSetId.HasValue
                        ? dimensionLoaderLine is null ||
                          (dimensionLoaderLine.FinanceDimensionSetId,
                              dimensionLoaderLine.FinanceDimensionSnapshotId) != sourceDimensionKeys[0]
                        : dimensionLoaderLine is not null)
                    throw new InvalidOperationException(
                        "The posted journal's inheritable dimension line does not match its frozen CWC dimension evidence.");
                dimensionAuthority ??= sourceDimensionKeys[0];
                dimensionAuthorityJournalId ??= journal.Id;

                credits.AddRange(sourceLines.GroupBy(line => line.AccountId)
                    .Select(lines => new CapitalizationCreditAuthority(
                        journal.Id,
                        lines.Key,
                        lines.Sum(line => line.DebitAmount - line.CreditAmount))));
            }

            var settingsCurrencies = await _context.FinanceSettings.AsNoTracking()
                .Where(settings => settings.TenantId == TenantId && !settings.IsDeleted)
                .Take(2)
                .Select(settings => settings.BaseCurrency)
                .ToListAsync(cancellationToken);
            if (settingsCurrencies.Count != 1 || !IsCanonicalCurrency(settingsCurrencies[0]) ||
                !string.Equals(settingsCurrencies[0], functionalCurrency, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Capital-project source currency does not match the tenant functional-currency authority.");

            return new CapitalizationAuthority(
                accountingBookId!.Value,
                accountingBookCode!,
                functionalCurrency!,
                dimensionAuthorityJournalId!.Value,
                credits);
        }

        private async Task<CostSourcePosting> ResolveCostSourcePostingAsync(
            AddProjectCostDto dto, CancellationToken cancellationToken)
        {
            var query = _context.JournalEntries.AsNoTracking().Where(item => item.TenantId == TenantId &&
                !item.IsDeleted && !item.IsReversed && item.ReversalJournalEntryId == null &&
                item.ReplicatedFromJournalEntryId == null && item.PostingStatus == "Posted");
            query = dto.SourceDocumentType == ProjectCostSourceType.ManualJournal
                ? query.Where(item => item.Id == dto.SourceDocumentId!.Value)
                : query.Where(item => item.SourceDocumentId == dto.SourceDocumentId &&
                    item.SourceDocumentType == dto.SourceDocumentType.ToString());
            var journals = await query.Take(2).ToListAsync(cancellationToken);
            if (journals.Count != 1)
                throw new InvalidOperationException("Capital-project cost must resolve to exactly one unreversed primary posted journal.");
            var journal = journals[0];
            if (journal.EntryDate.Date != dto.TransactionDate.Date)
                throw new InvalidOperationException("Capital-project cost date must equal the posted source accounting date.");
            var events = await _context.FinancePostingEvents.AsNoTracking().Where(item =>
                item.TenantId == TenantId && item.JournalEntryId == journal.Id && item.PostingStatus == "Posted" &&
                !item.IsDeleted).Take(2).ToListAsync(cancellationToken);
            if (events.Count != 1)
                throw new InvalidOperationException("Capital-project cost source must retain exactly one primary posted event.");
            return new CostSourcePosting(journal, events[0]);
        }

        private static string CapitalizationEvidenceHash(CapitalProject project)
        {
            var canonical = string.Join("|", project.CostLines.OrderBy(item => item.Id).Select(item =>
                    $"C:{item.Id:N}:{item.SourceBookAuthorityId:N}:{item.SourceFinancePostingEventId:N}:{item.Amount:0.00}")) +
                "|" + string.Join("|", project.SettlementRules.OrderBy(item => item.Id).Select(item =>
                    $"R:{item.Id:N}:{item.TargetFixedAssetCategoryId:N}:{item.AllocationPercentage:0.00}:{item.ProposedAssetName}"));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }

        private async Task ApplyProjectCapitalizationAsync(
            CapitalProject project,
            CreatedProjectAsset created,
            CapitalizationAuthority authority,
            FinancePostingResultDto posting,
            IReadOnlyList<CapitalizationRepresentation> representations,
            DateTime capitalizationDate,
            CancellationToken cancellationToken)
        {
            var asset = _context.FixedAssets.Local.SingleOrDefault(item =>
                    item.TenantId == TenantId && item.Id == created.AssetId && !item.IsDeleted)
                ?? await _context.FixedAssets
                .Include(item => item.BookValues)
                    .ThenInclude(value => value.AccountingBook)
                .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == created.AssetId && !item.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException("The created fixed asset could not be reloaded for capitalization.");
            var representationBookIds = representations.Select(item => item.Book.Id).ToHashSet();
            foreach (var unsupported in asset.BookValues
                         .Where(value => !value.IsDeleted && !representationBookIds.Contains(value.AccountingBookId))
                         .ToArray())
            {
                asset.BookValues.Remove(unsupported);
                _context.FixedAssetBookValues.Remove(unsupported);
            }

            asset.CapitalizationDate = capitalizationDate;
            asset.FunctionalCurrencyCode = authority.FunctionalCurrencyCode;
            asset.TransactionCurrencyCode = authority.FunctionalCurrencyCode;
            asset.ExchangeRate = 1m;
            asset.ExchangeRateDate = capitalizationDate;
            asset.SourceDocumentType = SettlementProducer.Definition.DocumentType;
            asset.SourceDocumentId = project.Id;
            asset.SourceDocumentLineId = created.SourceLineId;
            asset.JournalEntryId = posting.JournalEntryId;
            asset.PostingEventId = posting.PostingEventId;
            asset.CapitalizedAt = DateTime.UtcNow;
            asset.Status = FixedAssetStatus.Capitalized;
            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = UserName;

            foreach (var representation in representations)
            {
                var journalLines = representation.Journal.Transactions.Where(line =>
                    line.TenantId == TenantId && !line.IsDeleted && !line.IsReversed &&
                    line.PostingStatus == "Posted" && line.SourceDocumentLineId == created.SourceLineId &&
                    line.AccountId == created.AssetAccountId && line.DebitAmount > line.CreditAmount).ToArray();
                if (journalLines.Length != 1)
                    throw new InvalidOperationException(
                        "A capitalization representation must retain exactly one fixed-asset debit line.");
                var journalLine = journalLines[0];
                if (journalLine.AccountingBookId != representation.Journal.AccountingBookId ||
                    !string.Equals(journalLine.BookClassification, representation.Journal.BookClassification,
                        StringComparison.Ordinal) ||
                    !string.Equals(journalLine.FunctionalCurrencyCode,
                        representation.Book.FunctionalCurrencyCode,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "A capitalization representation line does not retain its exact book and currency authority.");

                var representationAmount = journalLine.DebitAmount - journalLine.CreditAmount;
                if (representation.Journal.Id == posting.JournalEntryId && representationAmount != created.Amount)
                    throw new InvalidOperationException(
                        "The primary capitalization representation does not equal the approved settlement allocation.");
                var bookValue = asset.BookValues.SingleOrDefault(value =>
                    !value.IsDeleted && value.AccountingBookId == representation.Book.Id &&
                    string.Equals(value.BookClassification, representation.Book.Code,
                        StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        "The created fixed asset is missing a book value for an actual capitalization representation.");
                bookValue.AcquisitionCost = representationAmount;
                bookValue.AccumulatedDepreciation = 0m;
                bookValue.NetBookValue = representationAmount;
                bookValue.ResidualValue = Math.Round(
                    representationAmount * created.ResidualValuePercent / 100m, 2,
                    MidpointRounding.AwayFromZero);
                bookValue.CapitalizationDate = capitalizationDate;
                bookValue.CapitalizationJournalEntryId = representation.Journal.Id;
                bookValue.CapitalizationPostingEventId = representation.PostingEventId;
                bookValue.SourceDocumentType = SettlementProducer.Definition.DocumentType;
                bookValue.SourceDocumentId = project.Id;
                bookValue.SourceDocumentLineId = created.SourceLineId;
                bookValue.UpdatedAt = DateTime.UtcNow;
                bookValue.UpdatedBy = UserName;

                _context.AssetTransactions.Add(new AssetTransaction
                {
                    TenantId = TenantId,
                    FixedAssetId = asset.Id,
                    AccountingBookId = representation.Book.Id,
                    BookClassification = representation.Book.Code,
                    TransactionDate = capitalizationDate,
                    TransactionType = "Capitalization",
                    Description = $"Capitalized from project {project.ProjectCode}",
                    Amount = representationAmount,
                    ResultingBookValue = representationAmount,
                    RelatedEntityId = representation.PostingEventId,
                    PerformedByUserId = Guid.TryParse(_currentUser.UserId, out var userId) ? userId : Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                });
            }
        }

        private async Task<IReadOnlyList<CapitalizationRepresentation>> ResolveCapitalizationRepresentationsAsync(
            CapitalProject project,
            CapitalizationAuthority authority,
            FinancePostingResultDto posting,
            CancellationToken cancellationToken)
        {
            var persisted = await _context.JournalEntries
                .Include(journal => journal.AccountingBook)
                .Include(journal => journal.Transactions)
                .Where(journal => journal.TenantId == TenantId && !journal.IsDeleted && !journal.IsReversed &&
                    journal.PostingStatus == "Posted" &&
                    (journal.Id == posting.JournalEntryId ||
                     journal.ReplicatedFromJournalEntryId == posting.JournalEntryId))
                .ToListAsync(cancellationToken);
            var local = _context.JournalEntries.Local.Where(journal =>
                journal.TenantId == TenantId && !journal.IsDeleted && !journal.IsReversed &&
                journal.PostingStatus == "Posted" &&
                (journal.Id == posting.JournalEntryId ||
                 journal.ReplicatedFromJournalEntryId == posting.JournalEntryId));
            var journals = persisted.Concat(local).GroupBy(journal => journal.Id)
                .Select(group => group.Last()).ToArray();
            var journalBookIds = journals.Select(journal => journal.AccountingBookId).Distinct().ToArray();
            var books = await _context.AccountingBooks.AsNoTracking()
                .Where(book => book.TenantId == TenantId && journalBookIds.Contains(book.Id))
                .ToDictionaryAsync(book => book.Id, cancellationToken);
            if (books.Count != journalBookIds.Length)
                throw new InvalidOperationException(
                    "A capitalization representation does not retain its accounting-book authority.");
            var primary = journals.SingleOrDefault(journal => journal.Id == posting.JournalEntryId)
                ?? throw new InvalidOperationException(
                    "The capitalization posting did not retain its primary journal evidence.");
            if (primary.AccountingBookId != authority.AccountingBookId ||
                !string.Equals(primary.BookClassification, authority.AccountingBookCode, StringComparison.Ordinal) ||
                primary.ReplicatedFromJournalEntryId.HasValue)
                throw new InvalidOperationException(
                    "The capitalization primary journal does not match the frozen source-book authority.");

            foreach (var journal in journals)
            {
                var book = books[journal.AccountingBookId];
                if (book.IsDeleted || !book.IsActive || !book.AllowsPosting ||
                    book.LifecycleStatus != AccountingBookLifecycleStatus.Active ||
                    !IsCanonicalExactBookCode(book.Code) ||
                    !IsCanonicalCurrency(book.FunctionalCurrencyCode) ||
                    !string.Equals(book.Code, journal.BookClassification, StringComparison.Ordinal) ||
                    journal.SourceDocumentId != project.Id ||
                    !string.Equals(journal.SourceDocumentType, SettlementProducer.Definition.DocumentType,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "A capitalization representation has stale or inconsistent source and book evidence.");
                if (journal.Id != primary.Id &&
                    (journal.ReplicatedFromJournalEntryId != primary.Id ||
                     book.BookType != AccountingBookType.ParallelFull ||
                     book.BaseAccountingBookId != authority.AccountingBookId))
                    throw new InvalidOperationException(
                        "A capitalization representation is not an immutable Parallel replica of the primary journal.");
                if (journal.Id == primary.Id && book.BookType != AccountingBookType.PrimaryFull)
                    throw new InvalidOperationException(
                        "The capitalization primary representation is not an active PrimaryFull book.");
                if (journal.Id == primary.Id &&
                    !string.Equals(book.FunctionalCurrencyCode, authority.FunctionalCurrencyCode,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "The capitalization primary representation changed functional-currency authority.");
            }
            if (journals.Select(journal => journal.AccountingBookId).Distinct().Count() != journals.Length)
                throw new InvalidOperationException(
                    "Capitalization posting evidence contains duplicate representations for an accounting book.");

            var activeParallelBookIds = await _context.AccountingBooks.AsNoTracking()
                .Where(book => book.TenantId == TenantId && !book.IsDeleted && book.IsActive && book.AllowsPosting &&
                    book.LifecycleStatus == AccountingBookLifecycleStatus.Active &&
                    book.BookType == AccountingBookType.ParallelFull &&
                    book.BaseAccountingBookId == authority.AccountingBookId)
                .Select(book => book.Id)
                .ToListAsync(cancellationToken);
            if (activeParallelBookIds.Except(journals.Select(journal => journal.AccountingBookId)).Any())
                throw new InvalidOperationException(
                    "An active Parallel book is missing its capitalization replica; asset creation was rolled back.");

            var journalIds = journals.Select(journal => journal.Id).ToArray();
            var persistedEvents = await _context.FinancePostingEvents.AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                    item.PostingStatus == "Posted" && item.JournalEntryId.HasValue &&
                    journalIds.Contains(item.JournalEntryId.Value))
                .ToListAsync(cancellationToken);
            var localEvents = _context.FinancePostingEvents.Local.Where(item =>
                item.TenantId == TenantId && !item.IsDeleted && item.PostingStatus == "Posted" &&
                item.JournalEntryId.HasValue && journalIds.Contains(item.JournalEntryId.Value));
            var events = persistedEvents.Concat(localEvents).GroupBy(item => item.Id)
                .Select(group => group.Last()).ToArray();
            var result = new List<CapitalizationRepresentation>(journals.Length);
            foreach (var journal in journals)
            {
                var postingEvent = events.SingleOrDefault(item => item.JournalEntryId == journal.Id)
                    ?? throw new InvalidOperationException(
                        "A capitalization representation is missing its exact posted event evidence.");
                if (postingEvent.AccountingBookId != journal.AccountingBookId ||
                    !string.Equals(postingEvent.BookClassification, journal.BookClassification,
                        StringComparison.Ordinal) ||
                    postingEvent.SourceDocumentId != project.Id ||
                    !string.Equals(postingEvent.SourceDocumentType, SettlementProducer.Definition.DocumentType,
                        StringComparison.Ordinal) ||
                    journal.Id == primary.Id && postingEvent.Id != posting.PostingEventId)
                    throw new InvalidOperationException(
                        "A capitalization posting event does not match its exact journal representation.");
                result.Add(new CapitalizationRepresentation(journal, books[journal.AccountingBookId], postingEvent.Id));
            }
            return result.OrderBy(item => item.Journal.Id == primary.Id ? 0 : 1)
                .ThenBy(item => item.Book.Code, StringComparer.Ordinal).ToArray();
        }

        private async Task EnsureCompletedCapitalizationIsConsistentAsync(
            CapitalProject project,
            CancellationToken cancellationToken)
        {
            if (project.CapitalizedAmount != project.TotalAccumulatedCost ||
                project.SettlementRules.Count == 0 ||
                project.SettlementRules.Any(rule => !rule.ResultingFixedAssetId.HasValue || rule.AllocatedAmount <= 0m) ||
                project.SettlementRules.Sum(rule => rule.AllocatedAmount) != project.CapitalizedAmount)
                throw new InvalidOperationException(
                    "The completed project contains stale or incomplete capitalization evidence.");

            var assetIds = project.SettlementRules.Select(rule => rule.ResultingFixedAssetId!.Value).ToArray();
            var validAssetCount = await _context.FixedAssets.AsNoTracking().CountAsync(asset =>
                asset.TenantId == TenantId && assetIds.Contains(asset.Id) && !asset.IsDeleted &&
                asset.SourceDocumentType == SettlementProducer.Definition.DocumentType &&
                asset.SourceDocumentId == project.Id && asset.JournalEntryId.HasValue && asset.PostingEventId.HasValue &&
                (asset.Status == FixedAssetStatus.Capitalized || asset.Status == FixedAssetStatus.Active),
                cancellationToken);
            if (validAssetCount != assetIds.Distinct().Count())
                throw new InvalidOperationException(
                    "The completed project does not retain one posted fixed asset for every settlement rule.");
        }

        private static bool IsCanonicalExactBookCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 20 ||
                !string.Equals(value, value.Trim().ToUpperInvariant(), StringComparison.Ordinal))
                return false;
            return value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') &&
                value is not "ALL" and not "ALL_ACTIVE_BOOKS" and not "ALL_CLASSIFIED_BOOKS" and not "ALLCLASSIFIEDBOOKS";
        }

        private static bool IsCanonicalCurrency(string? value)
            => value is { Length: 3 } &&
               string.Equals(value, value.Trim().ToUpperInvariant(), StringComparison.Ordinal) &&
               value.All(character => character is >= 'A' and <= 'Z');

        private sealed record CapitalizationAuthority(
            Guid AccountingBookId,
            string AccountingBookCode,
            string FunctionalCurrencyCode,
            Guid DimensionAuthorityJournalEntryId,
            IReadOnlyList<CapitalizationCreditAuthority> Credits);

        private sealed record CapitalizationCreditAuthority(
            Guid JournalEntryId,
            Guid AccountId,
            decimal Amount);

        private sealed record CreatedProjectAsset(
            Guid AssetId,
            Guid SourceLineId,
            Guid AssetAccountId,
            decimal Amount,
            decimal ResidualValuePercent);

        private sealed record CapitalizationRepresentation(
            JournalEntry Journal,
            AccountingBook Book,
            Guid PostingEventId);

        private sealed record CostSourcePosting(JournalEntry Journal, FinancePostingEvent PostingEvent);

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
