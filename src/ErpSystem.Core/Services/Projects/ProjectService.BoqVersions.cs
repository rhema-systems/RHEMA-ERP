using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectBoqVersionWorkspaceDto> GetProjectBoqVersionWorkspaceAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToDictionary(item => item.Id);
        var workingItems = (await GetProjectBoqItemEntitiesAsync(projectId)).ToList();
        var workingLines = BuildWorkingVersionLines(
            projectId,
            workingItems,
            packages,
            await GetProjectBoqWorkItemLookupAsync(projectId, workingItems));
        var versionEntities = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == projectId))
            .OrderByDescending(item => item.VersionNumber)
            .ToList();
        var policy = await GetEffectiveBoqVersionPolicyAsync();
        var currentPublication = versionEntities
            .Where(IsCurrentBoqPublication)
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.VersionNumber)
            .FirstOrDefault();
        var summaries = new List<ProjectBoqVersionSummaryDto>(versionEntities.Count);
        foreach (var version in versionEntities)
        {
            var summary = MapBoqVersionSummary(version);
            if (string.Equals(version.Status, ProjectBoqVersionStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
            {
                summary.CanCurrentUserApprove = await _workflowIntegrationService.CanUserApproveAsync(
                    QuantitySurveyWorkflowBindingRegistry.Boq,
                    version.Id,
                    _currentUserProvider.UserId);
            }
            summaries.Add(summary);
        }

        return new ProjectBoqVersionWorkspaceDto
        {
            ProjectId = projectId,
            WorkingSetHash = ComputeBoqSnapshotHash(workingLines),
            WorkingLineCount = workingLines.Count,
            WorkflowRequired = policy?.RequireWorkflowBeforeUse == true,
            ApprovedVersionsImmutable = policy?.ApprovedVersionsImmutable == true,
            CurrentPublishedVersionId = currentPublication?.Id,
            AllowedVersionTypes = GetDirectSnapshotTypes(policy, versionEntities),
            Versions = summaries
        };
    }

    public async Task<ProjectBoqVersionDetailDto> GetProjectBoqVersionAsync(Guid projectId, Guid versionId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var version = await GetProjectBoqVersionEntityAsync(projectId, versionId);
        var lines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == projectId
                && item.ProjectBoqVersionId == versionId))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.LineNumber)
            .Select(MapBoqVersionLine)
            .ToList();
        return MapBoqVersionDetail(version, lines);
    }

    public async Task<ProjectBoqVersionDetailDto> GetPublishedProjectBoqVersionAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var version = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == projectId
                && item.VersionType == QuantitySurveyBoqVersionType.Approved
                && item.Status == ProjectBoqVersionStatuses.Approved
                && item.PublishedAt.HasValue))
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.VersionNumber)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "This project has no approved published BoQ. Tender, valuation, and certificate processing must remain blocked until the configured BoQ workflow completes.");
        var lines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == projectId
                && item.ProjectBoqVersionId == version.Id))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.LineNumber)
            .Select(MapBoqVersionLine)
            .ToList();
        return MapBoqVersionDetail(version, lines);
    }

    public async Task<ProjectBoqVersionDetailDto> CreateProjectBoqVersionAsync(
        Guid projectId,
        CreateProjectBoqVersionDto dto,
        string correlationId)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (!Enum.IsDefined(dto.VersionType))
        {
            throw new InvalidOperationException("Select a recognized BoQ version type.");
        }

        if (dto.VersionType == QuantitySurveyBoqVersionType.Approved)
        {
            throw new InvalidOperationException("An Approved BoQ version can only be created by the configured BoQ approval workflow.");
        }

        var expectedHash = NormalizeSha256(dto.ExpectedWorkingSetHash);
        var summary = dto.ChangeSummary?.Trim();
        if (string.IsNullOrWhiteSpace(summary) || summary.Length < 5)
        {
            throw new InvalidOperationException("Enter a meaningful change summary of at least 5 characters.");
        }

        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var versionId = await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                transactionStarted = true;
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"qs-boq-version:{_currentUserProvider.TenantId:N}:{projectId:N}");

                var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToDictionary(item => item.Id);
                var workingItems = (await GetProjectBoqItemEntitiesAsync(projectId)).ToList();
                var workingLines = BuildWorkingVersionLines(
                    projectId,
                    workingItems,
                    packages,
                    await GetProjectBoqWorkItemLookupAsync(projectId, workingItems));
                if (workingLines.Count == 0)
                {
                    throw new InvalidOperationException("Add at least one BoQ line before creating a version snapshot.");
                }

                var actualHash = ComputeBoqSnapshotHash(workingLines);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The working BoQ changed after the version page was loaded. Reload it before creating the snapshot.");
                }

                var versions = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(item =>
                        item.TenantId == _currentUserProvider.TenantId
                        && item.ProjectId == projectId))
                    .OrderBy(item => item.VersionNumber)
                    .ToList();
                var policy = await GetEffectiveBoqVersionPolicyAsync();
                ValidateBoqVersionCreation(dto.VersionType, dto.SourceVersionId, versions, policy);

                ProjectBoqVersion? source = null;
                if (dto.SourceVersionId.HasValue)
                {
                    source = versions.SingleOrDefault(item => item.Id == dto.SourceVersionId.Value)
                        ?? throw new InvalidOperationException("The selected source version does not belong to this project.");
                }

                var now = DateTime.UtcNow;
                QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.CreateBoqVersion);
                var version = new ProjectBoqVersion
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = projectId,
                    SourceVersionId = source?.Id,
                    VersionNumber = versions.Select(item => item.VersionNumber).DefaultIfEmpty(0).Max() + 1,
                    VersionType = dto.VersionType,
                    Status = ProjectBoqVersionStatuses.Draft,
                    ApprovalStatus = ProjectBoqVersionStatuses.Draft,
                    ChangeSummary = summary,
                    AuditAction = QuantitySurveyAuditEventMap.CreateBoqVersion,
                    SnapshotHash = actualHash,
                    LineCount = workingLines.Count,
                    SnapshotAt = now,
                    ActorRoles = string.Join(',', _currentUserProvider.Roles.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    CorrelationId = NormalizeCorrelationId(correlationId),
                    CreatedAt = now,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                };

                await _unitOfWork.Repository<ProjectBoqVersion>().AddAsync(version);
                foreach (var line in workingLines)
                {
                    line.ProjectBoqVersionId = version.Id;
                    line.CreatedAt = now;
                    line.CreatedBy = _currentUserProvider.Username;
                    line.CreatedById = _currentUserProvider.UserId;
                    await _unitOfWork.Repository<ProjectBoqVersionLine>().AddAsync(line);
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionStarted = false;
                return version.Id;
            }
            catch
            {
                try
                {
                    if (transactionStarted && _unitOfWork.HasActiveTransaction)
                    {
                        await _unitOfWork.RollbackAsync();
                    }
                }
                finally
                {
                    _unitOfWork.ClearTrackedChanges();
                }

                throw;
            }
        });

        return await GetProjectBoqVersionAsync(projectId, versionId);
    }

    public async Task<ProjectBoqVersionComparisonDto> CompareProjectBoqVersionsAsync(
        Guid projectId,
        Guid baselineVersionId,
        Guid comparisonVersionId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        if (baselineVersionId == comparisonVersionId)
        {
            throw new InvalidOperationException("Select two different BoQ versions to compare.");
        }

        var versions = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == projectId
                && (item.Id == baselineVersionId || item.Id == comparisonVersionId)))
            .ToDictionary(item => item.Id);
        if (!versions.TryGetValue(baselineVersionId, out var baseline)
            || !versions.TryGetValue(comparisonVersionId, out var comparison))
        {
            throw new InvalidOperationException("One or both selected BoQ versions were not found in this project.");
        }

        var allLines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == projectId
                && (item.ProjectBoqVersionId == baselineVersionId
                    || item.ProjectBoqVersionId == comparisonVersionId)))
            .ToList();
        var baselineLines = allLines
            .Where(item => item.ProjectBoqVersionId == baselineVersionId)
            .ToDictionary(item => item.LineKey);
        var comparisonLines = allLines
            .Where(item => item.ProjectBoqVersionId == comparisonVersionId)
            .ToDictionary(item => item.LineKey);
        var lineComparisons = baselineLines.Keys
            .Union(comparisonLines.Keys)
            .Select(lineKey => CompareBoqVersionLine(
                lineKey,
                baselineLines.GetValueOrDefault(lineKey),
                comparisonLines.GetValueOrDefault(lineKey)))
            .OrderBy(item => item.ComparisonLine?.SortOrder ?? item.BaselineLine?.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.ComparisonLine?.LineNumber ?? item.BaselineLine?.LineNumber)
            .ToList();

        var currencies = baselineLines.Values.Select(item => item.Currency)
            .Concat(comparisonLines.Values.Select(item => item.Currency))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Select(currency =>
            {
                var baselineAmount = baselineLines.Values
                    .Where(item => string.Equals(item.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.LineAmount ?? 0m);
                var comparisonAmount = comparisonLines.Values
                    .Where(item => string.Equals(item.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.LineAmount ?? 0m);
                return new ProjectBoqVersionCurrencyDeltaDto
                {
                    Currency = currency,
                    BaselineAmount = baselineAmount,
                    ComparisonAmount = comparisonAmount,
                    DeltaAmount = comparisonAmount - baselineAmount
                };
            })
            .ToList();

        return new ProjectBoqVersionComparisonDto
        {
            Baseline = MapBoqVersionSummary(baseline),
            Comparison = MapBoqVersionSummary(comparison),
            AddedLineCount = lineComparisons.Count(item => item.ChangeType == "Added"),
            RemovedLineCount = lineComparisons.Count(item => item.ChangeType == "Removed"),
            ChangedLineCount = lineComparisons.Count(item => item.ChangeType == "Changed"),
            UnchangedLineCount = lineComparisons.Count(item => item.ChangeType == "Unchanged"),
            CurrencyTotals = currencies,
            Lines = lineComparisons
        };
    }

    private async Task<ProjectBoqVersion> GetProjectBoqVersionEntityAsync(Guid projectId, Guid versionId)
        => await _unitOfWork.Repository<ProjectBoqVersion>().FirstOrDefaultAsync(item =>
               item.TenantId == _currentUserProvider.TenantId
               && item.ProjectId == projectId
               && item.Id == versionId)
           ?? throw new InvalidOperationException("The requested BoQ version was not found in this project.");

    private async Task<QsBoqVersionPolicyValue?> GetEffectiveBoqVersionPolicyAsync()
    {
        var now = DateTime.UtcNow;
        var profiles = await _unitOfWork.Repository<QuantitySurveyConfigurationProfile>().FindAsync(profile =>
            profile.TenantId == _currentUserProvider.TenantId
            && profile.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published
            && profile.EffectiveFrom <= now
            && (!profile.EffectiveTo.HasValue || profile.EffectiveTo.Value >= now));
        var profile = profiles
            .OrderByDescending(item => item.IsDefault)
            .ThenByDescending(item => item.Version)
            .FirstOrDefault();
        if (profile == null) return null;

        var decisions = await _unitOfWork.Repository<QuantitySurveyConfigurationDecision>().FindAsync(decision =>
            decision.TenantId == _currentUserProvider.TenantId
            && decision.ProfileId == profile.Id
            && decision.DecisionKey == "QS-DEC-003"
            && decision.Status == QuantitySurveyConfigurationDecisionStatus.Approved
            && decision.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved
            && (!decision.EffectiveFrom.HasValue || decision.EffectiveFrom.Value <= now)
            && (!decision.EffectiveTo.HasValue || decision.EffectiveTo.Value >= now));
        var decision = decisions.OrderByDescending(item => item.DecisionDate).FirstOrDefault();
        if (decision == null) return null;

        try
        {
            return JsonSerializer.Deserialize<QsBoqVersionPolicyValue>(
                decision.ValueJson,
                QuantitySurveyDecisionJsonOptions)
                ?? throw new JsonException("The policy value is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The effective QS BoQ version policy is invalid and must be corrected before a version can be created.",
                exception);
        }
    }

    private static IReadOnlyList<QuantitySurveyBoqVersionType> GetDirectSnapshotTypes(
        QsBoqVersionPolicyValue? policy,
        IReadOnlyCollection<ProjectBoqVersion> versions)
    {
        var configured = policy?.RequiredVersionTypes.Count > 0
            ? policy.RequiredVersionTypes
            : Enum.GetValues<QuantitySurveyBoqVersionType>().ToList();
        var hasCurrentPublication = versions.Any(IsCurrentBoqPublication);
        var hasUnpublishedCandidate = versions.Any(item =>
            item.VersionType != QuantitySurveyBoqVersionType.Approved
            && item.Status is ProjectBoqVersionStatuses.Draft or ProjectBoqVersionStatuses.PendingApproval or ProjectBoqVersionStatuses.Rejected);
        if (versions.Count > 0 && !hasCurrentPublication) return [];
        if (hasUnpublishedCandidate) return [];

        return configured
            // Every configured lifecycle needs the Original candidate before approval can publish it.
            .Append(QuantitySurveyBoqVersionType.Original)
            .Where(item => item != QuantitySurveyBoqVersionType.Approved)
            .Where(item => !hasCurrentPublication
                ? item == QuantitySurveyBoqVersionType.Original
                : item != QuantitySurveyBoqVersionType.Original)
            .Distinct()
            .OrderBy(item => (int)item)
            .ToList();
    }

    private static void ValidateBoqVersionCreation(
        QuantitySurveyBoqVersionType versionType,
        Guid? sourceVersionId,
        IReadOnlyCollection<ProjectBoqVersion> versions,
        QsBoqVersionPolicyValue? policy)
    {
        if (versionType != QuantitySurveyBoqVersionType.Original
            && policy?.RequiredVersionTypes.Count > 0
            && !policy.RequiredVersionTypes.Contains(versionType))
        {
            throw new InvalidOperationException($"BoQ version type '{versionType}' is not enabled by the effective QS-DEC-003 policy.");
        }

        if (versions.Count == 0 && versionType != QuantitySurveyBoqVersionType.Original)
        {
            throw new InvalidOperationException("The first BoQ snapshot for a project must be the Original version.");
        }

        if (versionType == QuantitySurveyBoqVersionType.Original
            && versions.Any(item => item.VersionType == QuantitySurveyBoqVersionType.Original))
        {
            throw new InvalidOperationException("This project already has an Original BoQ version. Create a later version from it instead.");
        }

        if (versions.Count > 0 && !sourceVersionId.HasValue)
        {
            throw new InvalidOperationException("Select the prior BoQ version that this snapshot is based on.");
        }

        if (versions.Count == 0 && sourceVersionId.HasValue)
        {
            throw new InvalidOperationException("An Original BoQ version cannot reference a source version.");
        }

        var currentPublication = versions
            .Where(IsCurrentBoqPublication)
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.VersionNumber)
            .FirstOrDefault();
        var openCandidate = versions.FirstOrDefault(item =>
            item.VersionType != QuantitySurveyBoqVersionType.Approved
            && item.Status is ProjectBoqVersionStatuses.Draft or ProjectBoqVersionStatuses.PendingApproval or ProjectBoqVersionStatuses.Rejected);
        if (openCandidate != null)
        {
            throw new InvalidOperationException(
                $"BoQ v{openCandidate.VersionNumber} must complete its approval or revision lifecycle before another snapshot can be created.");
        }

        if (versions.Count > 0 && currentPublication == null)
        {
            throw new InvalidOperationException("The existing BoQ candidate must be approved and published before a later version can be created.");
        }

        if (currentPublication != null && sourceVersionId != currentPublication.Id)
        {
            throw new InvalidOperationException(
                $"A revision must be based on the current approved publication v{currentPublication.VersionNumber}.");
        }
    }

    private static bool IsCurrentBoqPublication(ProjectBoqVersion version)
        => version.VersionType == QuantitySurveyBoqVersionType.Approved
           && string.Equals(version.Status, ProjectBoqVersionStatuses.Approved, StringComparison.OrdinalIgnoreCase)
           && version.PublishedAt.HasValue;

    private List<ProjectBoqVersionLine> BuildWorkingVersionLines(
        Guid projectId,
        IReadOnlyCollection<ProjectBoqItem> items,
        IReadOnlyDictionary<Guid, ProjectPackage> packages,
        IReadOnlyDictionary<Guid, ProjectWorkItem> workItems)
    {
        var duplicateKey = items
            .Where(item => item.VersionLineKey == Guid.Empty)
            .Select(item => item.Id)
            .FirstOrDefault();
        if (duplicateKey != Guid.Empty)
        {
            throw new InvalidOperationException("A BoQ line is missing its version lineage key. Apply the QS-0104 database migration before versioning this project.");
        }

        return items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.LineNumber)
            .Select(item =>
            {
                packages.TryGetValue(item.ProjectPackageId, out var package);
                var workItem = item.ProjectWorkItemId.HasValue
                    && workItems.TryGetValue(item.ProjectWorkItemId.Value, out var resolvedWorkItem)
                        ? resolvedWorkItem
                        : null;
                return new ProjectBoqVersionLine
                {
                    TenantId = _currentUserProvider.TenantId,
                    ProjectId = projectId,
                    LineKey = item.VersionLineKey,
                    SourceBoqItemId = item.Id,
                    ProjectPackageId = item.ProjectPackageId,
                    PackageCode = package?.Code,
                    PackageName = package?.Name,
                    ProjectWorkItemId = workItem?.Id,
                    ActivityNodeType = workItem?.NodeType,
                    ActivityTitle = workItem?.Title,
                    SectionCode = item.SectionCode,
                    SectionName = item.SectionName,
                    TradeCode = item.TradeCode,
                    TradeName = item.TradeName,
                    CostCode = item.CostCode,
                    CostCodeName = item.CostCodeName,
                    MeasurementStandard = item.MeasurementStandard,
                    MeasurementCode = item.MeasurementCode,
                    MeasurementRule = item.MeasurementRule,
                    LineNumber = item.LineNumber,
                    ItemCode = item.ItemCode,
                    ItemType = item.ItemType,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitOfMeasure = item.UnitOfMeasure,
                    UnitRate = item.UnitRate,
                    LineAmount = item.UnitRate.HasValue
                        ? decimal.Round(item.Quantity * item.UnitRate.Value, 2, MidpointRounding.AwayFromZero)
                        : null,
                    Currency = item.Currency,
                    SortOrder = item.SortOrder
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, ProjectWorkItem>> GetProjectBoqWorkItemLookupAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectBoqItem> items)
    {
        var workItemIds = items
            .Where(item => item.ProjectWorkItemId.HasValue)
            .Select(item => item.ProjectWorkItemId!.Value)
            .Distinct()
            .ToList();
        if (workItemIds.Count == 0)
        {
            return new Dictionary<Guid, ProjectWorkItem>();
        }

        return (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(item =>
                item.TenantId == _currentUserProvider.TenantId
                && item.ProjectId == projectId
                && workItemIds.Contains(item.Id)))
            .ToDictionary(item => item.Id);
    }

    private static string ComputeBoqSnapshotHash(IEnumerable<ProjectBoqVersionLine> lines)
    {
        var canonical = lines
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.LineNumber, StringComparer.Ordinal)
            .ThenBy(item => item.LineKey)
            .Select(item => new
            {
                item.LineKey,
                item.SourceBoqItemId,
                item.ProjectPackageId,
                item.PackageCode,
                item.PackageName,
                item.ProjectWorkItemId,
                item.ActivityNodeType,
                item.ActivityTitle,
                item.SectionCode,
                item.SectionName,
                item.TradeCode,
                item.TradeName,
                item.CostCode,
                item.CostCodeName,
                item.MeasurementStandard,
                item.MeasurementCode,
                item.MeasurementRule,
                item.LineNumber,
                item.ItemCode,
                item.ItemType,
                item.Description,
                item.Quantity,
                item.UnitOfMeasure,
                item.UnitRate,
                item.LineAmount,
                item.Currency,
                item.SortOrder
            });
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static ProjectBoqVersionLineComparisonDto CompareBoqVersionLine(
        Guid lineKey,
        ProjectBoqVersionLine? baseline,
        ProjectBoqVersionLine? comparison)
    {
        var changedFields = GetChangedBoqFields(baseline, comparison);
        var changeType = baseline == null
            ? "Added"
            : comparison == null
                ? "Removed"
                : changedFields.Count == 0 ? "Unchanged" : "Changed";
        return new ProjectBoqVersionLineComparisonDto
        {
            LineKey = lineKey,
            ChangeType = changeType,
            BaselineLine = baseline == null ? null : MapBoqVersionLine(baseline),
            ComparisonLine = comparison == null ? null : MapBoqVersionLine(comparison),
            QuantityDelta = (comparison?.Quantity ?? 0m) - (baseline?.Quantity ?? 0m),
            UnitRateDelta = baseline?.UnitRate.HasValue == true || comparison?.UnitRate.HasValue == true
                ? (comparison?.UnitRate ?? 0m) - (baseline?.UnitRate ?? 0m)
                : null,
            AmountDelta = baseline?.LineAmount.HasValue == true || comparison?.LineAmount.HasValue == true
                ? (comparison?.LineAmount ?? 0m) - (baseline?.LineAmount ?? 0m)
                : null,
            ChangedFields = changedFields
        };
    }

    private static IReadOnlyList<string> GetChangedBoqFields(ProjectBoqVersionLine? baseline, ProjectBoqVersionLine? comparison)
    {
        if (baseline == null || comparison == null) return [];
        var fields = new List<string>();
        AddIfDifferent(fields, "Package", baseline.ProjectPackageId, comparison.ProjectPackageId);
        AddIfDifferent(fields, "Activity", baseline.ProjectWorkItemId, comparison.ProjectWorkItemId);
        AddIfDifferent(fields, "Section", baseline.SectionCode, comparison.SectionCode);
        AddIfDifferent(fields, "Trade", baseline.TradeCode, comparison.TradeCode);
        AddIfDifferent(fields, "CostCode", baseline.CostCode, comparison.CostCode);
        AddIfDifferent(fields, "MeasurementCode", baseline.MeasurementCode, comparison.MeasurementCode);
        AddIfDifferent(fields, "LineNumber", baseline.LineNumber, comparison.LineNumber);
        AddIfDifferent(fields, "ItemCode", baseline.ItemCode, comparison.ItemCode);
        AddIfDifferent(fields, "ItemType", baseline.ItemType, comparison.ItemType);
        AddIfDifferent(fields, "Description", baseline.Description, comparison.Description);
        AddIfDifferent(fields, "Quantity", baseline.Quantity, comparison.Quantity);
        AddIfDifferent(fields, "UnitOfMeasure", baseline.UnitOfMeasure, comparison.UnitOfMeasure);
        AddIfDifferent(fields, "UnitRate", baseline.UnitRate, comparison.UnitRate);
        AddIfDifferent(fields, "Currency", baseline.Currency, comparison.Currency);
        AddIfDifferent(fields, "SortOrder", baseline.SortOrder, comparison.SortOrder);
        return fields;
    }

    private static void AddIfDifferent<T>(ICollection<string> fields, string name, T baseline, T comparison)
    {
        if (!EqualityComparer<T>.Default.Equals(baseline, comparison)) fields.Add(name);
    }

    private static ProjectBoqVersionSummaryDto MapBoqVersionSummary(ProjectBoqVersion item) => new()
    {
        Id = item.Id,
        ProjectId = item.ProjectId,
        SourceVersionId = item.SourceVersionId,
        VersionNumber = item.VersionNumber,
        VersionType = item.VersionType,
        Status = item.Status,
        ApprovalStatus = item.ApprovalStatus,
        WorkflowInstanceId = item.WorkflowInstanceId,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        SubmittedById = item.SubmittedById,
        SubmittedAt = item.SubmittedAt,
        ApprovedById = item.ApprovedById,
        ApprovedAt = item.ApprovedAt,
        PublishedById = item.PublishedById,
        PublishedAt = item.PublishedAt,
        RejectionReason = item.RejectionReason,
        IsPublished = item.VersionType == QuantitySurveyBoqVersionType.Approved
                      && item.PublishedAt.HasValue,
        ChangeSummary = item.ChangeSummary,
        SnapshotHash = item.SnapshotHash,
        LineCount = item.LineCount,
        SnapshotAt = item.SnapshotAt,
        CreatedBy = item.CreatedBy,
        CreatedById = item.CreatedById
    };

    private static ProjectBoqVersionDetailDto MapBoqVersionDetail(
        ProjectBoqVersion item,
        IReadOnlyList<ProjectBoqVersionLineDto> lines)
    {
        var summary = MapBoqVersionSummary(item);
        return new ProjectBoqVersionDetailDto
        {
            Id = summary.Id,
            ProjectId = summary.ProjectId,
            SourceVersionId = summary.SourceVersionId,
            VersionNumber = summary.VersionNumber,
            VersionType = summary.VersionType,
            Status = summary.Status,
            ApprovalStatus = summary.ApprovalStatus,
            WorkflowInstanceId = summary.WorkflowInstanceId,
            WorkflowDefinitionId = summary.WorkflowDefinitionId,
            SubmittedById = summary.SubmittedById,
            SubmittedAt = summary.SubmittedAt,
            ApprovedById = summary.ApprovedById,
            ApprovedAt = summary.ApprovedAt,
            PublishedById = summary.PublishedById,
            PublishedAt = summary.PublishedAt,
            RejectionReason = summary.RejectionReason,
            IsPublished = summary.IsPublished,
            CanCurrentUserApprove = summary.CanCurrentUserApprove,
            ChangeSummary = summary.ChangeSummary,
            SnapshotHash = summary.SnapshotHash,
            LineCount = summary.LineCount,
            SnapshotAt = summary.SnapshotAt,
            CreatedBy = summary.CreatedBy,
            CreatedById = summary.CreatedById,
            Lines = lines
        };
    }

    private static ProjectBoqVersionLineDto MapBoqVersionLine(ProjectBoqVersionLine item) => new()
    {
        Id = item.Id,
        LineKey = item.LineKey,
        SourceBoqItemId = item.SourceBoqItemId,
        ProjectPackageId = item.ProjectPackageId,
        PackageCode = item.PackageCode,
        PackageName = item.PackageName,
        ProjectWorkItemId = item.ProjectWorkItemId,
        ActivityNodeType = item.ActivityNodeType,
        ActivityTitle = item.ActivityTitle,
        SectionCode = item.SectionCode,
        SectionName = item.SectionName,
        TradeCode = item.TradeCode,
        TradeName = item.TradeName,
        CostCode = item.CostCode,
        CostCodeName = item.CostCodeName,
        MeasurementStandard = item.MeasurementStandard,
        MeasurementCode = item.MeasurementCode,
        MeasurementRule = item.MeasurementRule,
        LineNumber = item.LineNumber,
        ItemCode = item.ItemCode,
        ItemType = item.ItemType,
        Description = item.Description,
        Quantity = item.Quantity,
        UnitOfMeasure = item.UnitOfMeasure,
        UnitRate = item.UnitRate,
        LineAmount = item.LineAmount,
        Currency = item.Currency,
        SortOrder = item.SortOrder
    };

    private static string NormalizeSha256(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized == null
            || normalized.Length != 64
            || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException("Reload the BoQ version workspace before creating a snapshot.");
        }

        return normalized;
    }

    private static string NormalizeCorrelationId(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
        return normalized[..Math.Min(normalized.Length, 100)];
    }
}
