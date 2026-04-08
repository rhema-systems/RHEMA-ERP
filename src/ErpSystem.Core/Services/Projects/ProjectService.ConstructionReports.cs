using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectPhaseGateReadinessReportItemDto>> GetPhaseGateReadinessReportAsync(Guid? projectId = null, int take = 250)
    {
        var projects = await LoadProjectsForConstructionReportAsync(projectId, projectId.HasValue ? 5 : Math.Max((take / 6) + 20, 40));
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var profiles = (await _unitOfWork.Repository<ProjectDevelopmentProfile>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.ProjectId, x => x);
        var phases = (await _unitOfWork.Repository<ProjectPhase>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var packages = (await _unitOfWork.Repository<ProjectPackage>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var boqItems = (await _unitOfWork.Repository<ProjectBoqItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var approvalItems = (await _unitOfWork.Repository<ProjectApprovalRegisterItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var documents = (await _unitOfWork.Repository<ProjectDocument>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var commissioningItems = (await _unitOfWork.Repository<ProjectCommissioningItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var handoverItems = (await _unitOfWork.Repository<ProjectHandoverItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var snagItems = (await _unitOfWork.Repository<ProjectSnagItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();

        var phaseTemplates = (await _unitOfWork.Repository<ProjectPhaseTemplate>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.IsActive))
            .ToList();
        var templateIds = phaseTemplates.Select(x => x.Id).ToList();
        var stageGateRules = templateIds.Count == 0
            ? []
            : (await _unitOfWork.Repository<ProjectStageGateRule>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.IsActive
                    && templateIds.Contains(x.ProjectPhaseTemplateId)))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();

        var phaseRulesByTemplateId = stageGateRules
            .GroupBy(x => x.ProjectPhaseTemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var phasesByProjectId = phases
            .GroupBy(x => x.ProjectId)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList());
        var packagesByProjectId = packages.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var boqItemsByProjectId = boqItems.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var approvalsByProjectId = approvalItems.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var documentsByProjectId = documents.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var commissioningByProjectId = commissioningItems.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var handoverByProjectId = handoverItems.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var snagByProjectId = snagItems.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());

        var rows = new List<ProjectPhaseGateReadinessReportItemDto>();
        foreach (var project in projects.OrderBy(x => x.ProjectCode))
        {
            var profile = profiles.GetValueOrDefault(project.Id);
            var applicableTemplates = ResolveApplicablePhaseTemplatesForReport(project, profile, phaseTemplates);
            var projectPhases = phasesByProjectId.GetValueOrDefault(project.Id, []);
            var projectPackages = packagesByProjectId.GetValueOrDefault(project.Id, []);
            var projectBoqItems = boqItemsByProjectId.GetValueOrDefault(project.Id, []);
            var projectApprovals = approvalsByProjectId.GetValueOrDefault(project.Id, []);
            var projectDocuments = documentsByProjectId.GetValueOrDefault(project.Id, []);
            var projectCommissioning = commissioningByProjectId.GetValueOrDefault(project.Id, []);
            var projectHandover = handoverByProjectId.GetValueOrDefault(project.Id, []);
            var projectSnags = snagByProjectId.GetValueOrDefault(project.Id, []);

            foreach (var phase in projectPhases)
            {
                var template = FindMatchingPhaseTemplate(phase, applicableTemplates);
                var configuredRules = template != null && phaseRulesByTemplateId.TryGetValue(template.Id, out var templateRules)
                    ? templateRules
                    : [];
                var requirementResults = configuredRules
                    .Select(rule => EvaluateStageGateRule(rule, phase, projectPackages, projectBoqItems, projectApprovals, projectDocuments, projectCommissioning, projectHandover, projectSnags))
                    .ToList();
                var blockingRuleCount = requirementResults.Count(result => result.IsBlocking);
                var blockingFailureCount = requirementResults.Count(result => result.IsBlocking && !result.IsSatisfied);
                var satisfiedRuleCount = requirementResults.Count(result => result.IsSatisfied);
                var hasConfiguredRules = requirementResults.Count > 0;
                var isReady = phase.IsStageGateRequired
                    ? hasConfiguredRules && blockingFailureCount == 0
                    : blockingFailureCount == 0;

                rows.Add(new ProjectPhaseGateReadinessReportItemDto
                {
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    ProjectStatus = project.Status,
                    ProjectPhaseId = phase.Id,
                    ProjectPhaseCode = phase.Code,
                    ProjectPhaseName = phase.Name,
                    ProjectPhaseSortOrder = phase.SortOrder,
                    IsStageGateRequired = phase.IsStageGateRequired,
                    HasConfiguredRules = hasConfiguredRules,
                    IsReady = isReady,
                    ConfiguredRuleCount = requirementResults.Count,
                    BlockingRuleCount = blockingRuleCount,
                    BlockingFailureCount = blockingFailureCount,
                    SatisfiedRuleCount = satisfiedRuleCount,
                    GateStatus = ResolvePhaseGateStatus(phase.IsStageGateRequired, hasConfiguredRules, blockingFailureCount),
                    TopBlockingMessage = requirementResults.FirstOrDefault(result => result.IsBlocking && !result.IsSatisfied)?.Message
                });
            }
        }

        return rows
            .OrderBy(item => GetPhaseGateStatusRank(item.GateStatus))
            .ThenBy(item => item.ProjectCode)
            .ThenBy(item => item.ProjectPhaseSortOrder)
            .ThenBy(item => item.ProjectPhaseName)
            .Take(take)
            .ToList();
    }

    private async Task<List<Project>> LoadProjectsForConstructionReportAsync(Guid? projectId, int take)
    {
        var accessibleProjects = (await GetAccessibleProjectsAsync(take: Math.Max(take, 100))).ToList();
        if (!projectId.HasValue)
        {
            return accessibleProjects;
        }

        return accessibleProjects.Where(item => item.Id == projectId.Value).ToList();
    }

    private static List<ProjectPhaseTemplate> ResolveApplicablePhaseTemplatesForReport(
        Project project,
        ProjectDevelopmentProfile? profile,
        IReadOnlyCollection<ProjectPhaseTemplate> templates)
    {
        var applicableTemplates = templates
            .Where(template =>
                (!template.ProjectTypeId.HasValue || template.ProjectTypeId == project.ProjectTypeId)
                && MatchesPhaseTemplateApplicability(template, profile))
            .ToList();

        if (!project.ProjectTypeId.HasValue)
        {
            return applicableTemplates
                .Where(template => !template.ProjectTypeId.HasValue)
                .ToList();
        }

        var projectTypeTemplates = applicableTemplates
            .Where(template => template.ProjectTypeId == project.ProjectTypeId)
            .ToList();

        return projectTypeTemplates.Count > 0
            ? projectTypeTemplates
            : applicableTemplates.Where(template => !template.ProjectTypeId.HasValue).ToList();
    }

    private static string ResolvePhaseGateStatus(bool isStageGateRequired, bool hasConfiguredRules, int blockingFailureCount)
    {
        if (blockingFailureCount > 0)
        {
            return "Blocked";
        }

        if (isStageGateRequired && !hasConfiguredRules)
        {
            return "NeedsSetup";
        }

        if (!isStageGateRequired && !hasConfiguredRules)
        {
            return "Optional";
        }

        return "Ready";
    }

    private static int GetPhaseGateStatusRank(string gateStatus)
        => gateStatus switch
        {
            "Blocked" => 0,
            "NeedsSetup" => 1,
            "Ready" => 2,
            "Optional" => 3,
            _ => 4
        };

    private static int GetConstructionSeverityRank(string severity)
        => severity switch
        {
            "Critical" => 0,
            "High" => 1,
            "Warning" => 2,
            "Medium" => 3,
            "Low" => 4,
            "None" => 5,
            _ => 6
        };

    public async Task<IEnumerable<ProjectApprovalWatchReportItemDto>> GetApprovalWatchReportAsync(Guid? projectId = null, int take = 250)
    {
        var projects = await LoadProjectsForConstructionReportAsync(projectId, projectId.HasValue ? 5 : Math.Max(take, 80));
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var approvals = (await _unitOfWork.Repository<ProjectApprovalRegisterItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var phases = (await _unitOfWork.Repository<ProjectPhase>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var projectsById = projects.ToDictionary(x => x.Id);
        var today = DateTime.UtcNow.Date;

        return approvals
            .Select(item =>
            {
                var watch = EvaluateApprovalWatchState(item, today);
                if (watch == null || !projectsById.TryGetValue(item.ProjectId, out var project))
                {
                    return null;
                }

                return new ProjectApprovalWatchReportItemDto
                {
                    ProjectId = item.ProjectId,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    ProjectStatus = project.Status,
                    ApprovalRegisterItemId = item.Id,
                    ProjectPhaseId = item.ProjectPhaseId,
                    ProjectPhaseName = item.ProjectPhaseId.HasValue ? phases.GetValueOrDefault(item.ProjectPhaseId.Value) : null,
                    ApprovalType = item.ApprovalType,
                    Title = item.Title,
                    Status = item.Status,
                    WatchState = watch.Value.WatchState,
                    Severity = watch.Value.Severity,
                    IsRequired = item.IsRequired,
                    AuthorityName = item.AuthorityName,
                    ReferenceNumber = item.ReferenceNumber,
                    SubmittedDate = item.SubmittedDate,
                    TargetDecisionDate = item.TargetDecisionDate,
                    ApprovedDate = item.ApprovedDate,
                    ExpiryDate = item.ExpiryDate,
                    DaysToTargetDecision = watch.Value.DaysToTargetDecision,
                    DaysToExpiry = watch.Value.DaysToExpiry
                };
            })
            .Where(item => item != null)
            .Cast<ProjectApprovalWatchReportItemDto>()
            .OrderBy(item => GetConstructionSeverityRank(item.Severity))
            .ThenBy(item => item.DaysToExpiry ?? item.DaysToTargetDecision ?? int.MaxValue)
            .ThenBy(item => item.ProjectCode)
            .ThenBy(item => item.Title)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectCommercialAdministrationReportItemDto>> GetCommercialAdministrationReportAsync(Guid? projectId = null, int take = 200)
    {
        var projects = await LoadProjectsForConstructionReportAsync(projectId, projectId.HasValue ? 5 : Math.Max(take, 60));
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var packages = (await _unitOfWork.Repository<ProjectPackage>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var variationOrders = (await _unitOfWork.Repository<ProjectVariationOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var interimValuations = (await _unitOfWork.Repository<ProjectInterimValuation>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var paymentCertificates = (await _unitOfWork.Repository<ProjectPaymentCertificate>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var extensionOfTimeRequests = (await _unitOfWork.Repository<ProjectExtensionOfTime>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var finalAccounts = (await _unitOfWork.Repository<ProjectFinalAccount>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var currencyCode = await GetProjectBaseCurrencyCodeAsync();

        var packagesByProjectId = packages.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var variationsByProjectId = variationOrders.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var valuationsByProjectId = interimValuations.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var certificatesByProjectId = paymentCertificates.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var eotByProjectId = extensionOfTimeRequests.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var finalAccountByProjectId = finalAccounts.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.CreatedAt).First());

        var rows = new List<ProjectCommercialAdministrationReportItemDto>();
        foreach (var project in projects)
        {
            var projectPackages = packagesByProjectId.GetValueOrDefault(project.Id, []);
            var projectVariations = variationsByProjectId.GetValueOrDefault(project.Id, []);
            var projectValuations = valuationsByProjectId.GetValueOrDefault(project.Id, []);
            var projectCertificates = certificatesByProjectId.GetValueOrDefault(project.Id, []);
            var projectEots = eotByProjectId.GetValueOrDefault(project.Id, []);
            finalAccountByProjectId.TryGetValue(project.Id, out var finalAccount);

            var packageForecastAmount = projectPackages.Sum(x => x.ForecastAmount ?? 0m);
            var approvedBudget = project.ApprovedBudget ?? 0m;
            var unassignedPackageCount = projectPackages.Count(x => !x.ProjectPhaseId.HasValue);
            var packagesOverForecast = projectPackages.Count(x => (x.ForecastAmount ?? 0m) > (x.BudgetAmount ?? 0m) && (x.BudgetAmount ?? 0m) > 0m);
            var approvedVariationAmount = projectVariations
                .Where(x => x.Status == ProjectVariationOrderStatuses.Approved || x.Status == ProjectVariationOrderStatuses.Implemented || x.Status == ProjectVariationOrderStatuses.Closed)
                .Sum(x => x.ApprovedAmount ?? x.EstimatedAmount ?? 0m);
            var netValuationAmount = projectValuations.Sum(x => x.NetValuationAmount);
            var netCertifiedAmount = projectCertificates.Sum(x => x.NetCertifiedAmount);
            var retentionHeldAmount = projectCertificates.Sum(x => x.RetentionHeldAmount) + projectValuations.Sum(x => x.RetentionAmount);
            var approvedExtensionDays = projectEots
                .Where(x => x.Status == ProjectExtensionOfTimeStatuses.Approved || x.Status == ProjectExtensionOfTimeStatuses.Implemented || x.Status == ProjectExtensionOfTimeStatuses.Closed)
                .Sum(x => x.DaysApproved ?? 0);

            var alerts = new List<string>();
            if (projectPackages.Count == 0)
            {
                alerts.Add("No construction packages have been configured.");
            }

            if (unassignedPackageCount > 0)
            {
                alerts.Add($"{unassignedPackageCount} package(s) are not assigned to a project phase.");
            }

            if (packagesOverForecast > 0)
            {
                alerts.Add($"{packagesOverForecast} package(s) have forecast cost above budget.");
            }

            if (approvedBudget > 0m && packageForecastAmount > approvedBudget)
            {
                alerts.Add("Package forecast exceeds the approved project budget.");
            }

            if (projectCertificates.Any() && netCertifiedAmount < netValuationAmount)
            {
                alerts.Add("Certified amount is below recorded interim valuation value.");
            }

            if (finalAccount != null && !string.Equals(finalAccount.Status, ProjectFinalAccountStatuses.Closed, StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add($"Final account is currently {finalAccount.Status}.");
            }

            rows.Add(new ProjectCommercialAdministrationReportItemDto
            {
                ProjectId = project.Id,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                ProjectStatus = project.Status,
                Currency = currencyCode,
                ApprovedBudget = approvedBudget,
                PackageForecastAmount = packageForecastAmount,
                ForecastVarianceAmount = approvedBudget - packageForecastAmount,
                PackageCount = projectPackages.Count,
                UnassignedPackageCount = unassignedPackageCount,
                VariationOrderCount = projectVariations.Count,
                ApprovedVariationAmount = approvedVariationAmount,
                InterimValuationCount = projectValuations.Count,
                NetValuationAmount = netValuationAmount,
                PaymentCertificateCount = projectCertificates.Count,
                NetCertifiedAmount = netCertifiedAmount,
                RetentionHeldAmount = retentionHeldAmount,
                ExtensionOfTimeCount = projectEots.Count,
                ApprovedExtensionDays = approvedExtensionDays,
                FinalAccountStatus = finalAccount?.Status,
                AlertCount = alerts.Count,
                WatchState = ResolveCommercialWatchState(alerts, approvedBudget, packageForecastAmount),
                TopAlertMessage = alerts.FirstOrDefault()
            });
        }

        return rows
            .OrderBy(item => GetCommercialWatchStateRank(item.WatchState))
            .ThenByDescending(item => item.AlertCount)
            .ThenBy(item => item.ForecastVarianceAmount)
            .ThenBy(item => item.ProjectCode)
            .Take(take)
            .ToList();
    }

    private static (string WatchState, string Severity, int? DaysToTargetDecision, int? DaysToExpiry)? EvaluateApprovalWatchState(
        ProjectApprovalRegisterItem item,
        DateTime today)
    {
        var status = item.Status?.Trim() ?? string.Empty;
        var daysToTargetDecision = item.TargetDecisionDate.HasValue
            ? (item.TargetDecisionDate.Value.Date - today).Days
            : (int?)null;
        var daysToExpiry = item.ExpiryDate.HasValue
            ? (item.ExpiryDate.Value.Date - today).Days
            : (int?)null;

        var isApprovedLike =
            string.Equals(status, ProjectApprovalRegisterStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "ConditionallyApproved", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(status, ProjectApprovalRegisterStatuses.Expired, StringComparison.OrdinalIgnoreCase)
            || (isApprovedLike && daysToExpiry.HasValue && daysToExpiry.Value < 0))
        {
            return ("Expired", "Critical", daysToTargetDecision, daysToExpiry);
        }

        if (isApprovedLike && daysToExpiry.HasValue && daysToExpiry.Value <= 45)
        {
            return ("ExpiringSoon", daysToExpiry.Value <= 14 ? "High" : "Warning", daysToTargetDecision, daysToExpiry);
        }

        if (string.Equals(status, ProjectApprovalRegisterStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            if (!daysToTargetDecision.HasValue)
            {
                return ("PendingDecision", "Warning", null, daysToExpiry);
            }

            if (daysToTargetDecision.Value < 0)
            {
                return ("DecisionOverdue", "High", daysToTargetDecision, daysToExpiry);
            }

            if (daysToTargetDecision.Value <= 14)
            {
                return ("PendingDecision", "Warning", daysToTargetDecision, daysToExpiry);
            }
        }

        if (item.IsRequired
            && (string.Equals(status, ProjectApprovalRegisterStatuses.Planned, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, ProjectApprovalRegisterStatuses.InPreparation, StringComparison.OrdinalIgnoreCase))
            && daysToTargetDecision.HasValue)
        {
            if (daysToTargetDecision.Value < 0)
            {
                return ("SubmissionOverdue", "High", daysToTargetDecision, daysToExpiry);
            }

            if (daysToTargetDecision.Value <= 21)
            {
                return ("PendingSubmission", "Warning", daysToTargetDecision, daysToExpiry);
            }
        }

        return null;
    }

    private static string ResolveCommercialWatchState(IReadOnlyCollection<string> alerts, decimal approvedBudget, decimal packageForecastAmount)
    {
        if (approvedBudget > 0m && packageForecastAmount > approvedBudget)
        {
            return "Critical";
        }

        if (alerts.Count > 0)
        {
            return "Attention";
        }

        return "Stable";
    }

    private static int GetCommercialWatchStateRank(string watchState)
        => watchState switch
        {
            "Critical" => 0,
            "Attention" => 1,
            "Stable" => 2,
            _ => 3
        };

    public async Task<IEnumerable<ProjectPostHandoverWatchReportItemDto>> GetPostHandoverWatchReportAsync(Guid? projectId = null, int take = 200)
    {
        var projects = await LoadProjectsForConstructionReportAsync(projectId, projectId.HasValue ? 5 : Math.Max(take, 60));
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var handoverItems = (await _unitOfWork.Repository<ProjectHandoverItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var defectCases = (await _unitOfWork.Repository<ProjectDefectLiabilityCase>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var handoverByProjectId = handoverItems.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var defectsByProjectId = defectCases.GroupBy(x => x.ProjectId).ToDictionary(group => group.Key, group => group.ToList());
        var today = DateTime.UtcNow.Date;

        var rows = new List<ProjectPostHandoverWatchReportItemDto>();
        foreach (var project in projects)
        {
            var projectHandoverItems = handoverByProjectId.GetValueOrDefault(project.Id, []);
            var projectDefects = defectsByProjectId.GetValueOrDefault(project.Id, []);
            var activeDefects = projectDefects.Where(item =>
                    !string.Equals(item.Status, ProjectDefectLiabilityStatuses.Resolved, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(item.Status, ProjectDefectLiabilityStatuses.Closed, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(item.Status, ProjectDefectLiabilityStatuses.WarrantyExpired, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var openHandoverItemCount = projectHandoverItems.Count(item =>
                !string.Equals(item.Status, ProjectHandoverItemStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, ProjectHandoverItemStatuses.Waived, StringComparison.OrdinalIgnoreCase));

            var responseBreachCount = 0;
            var resolutionBreachCount = 0;
            var warrantyExpiringSoonCount = 0;
            var hasExpiredWarranty = false;
            var highestSeverity = "None";

            foreach (var defect in activeDefects)
            {
                var responseDueDate = defect.ResponseSlaDays.HasValue
                    ? defect.ReportedDate.Date.AddDays(defect.ResponseSlaDays.Value)
                    : (DateTime?)null;
                var resolutionDueDate = defect.TargetResolutionDate?.Date
                    ?? (defect.ResolutionSlaDays.HasValue ? defect.ReportedDate.Date.AddDays(defect.ResolutionSlaDays.Value) : null);

                if (responseDueDate.HasValue && !defect.FirstResponseDate.HasValue && responseDueDate.Value < today)
                {
                    responseBreachCount++;
                    highestSeverity = MaxSeverityLabel(highestSeverity, "High");
                }

                if (resolutionDueDate.HasValue && !defect.ResolvedDate.HasValue && resolutionDueDate.Value < today)
                {
                    resolutionBreachCount++;
                    highestSeverity = MaxSeverityLabel(highestSeverity, "Critical");
                }

                if (defect.IsWarrantyRelated && defect.WarrantyExpiryDate.HasValue)
                {
                    var daysToExpiry = (defect.WarrantyExpiryDate.Value.Date - today).Days;
                    if (daysToExpiry <= 30)
                    {
                        warrantyExpiringSoonCount++;
                        highestSeverity = MaxSeverityLabel(highestSeverity, daysToExpiry < 0 ? "Critical" : "Warning");
                        hasExpiredWarranty |= daysToExpiry < 0;
                    }
                }
            }

            if (highestSeverity == "None" && openHandoverItemCount > 0)
            {
                highestSeverity = "Warning";
            }

            if (!projectId.HasValue
                && openHandoverItemCount == 0
                && activeDefects.Count == 0
                && responseBreachCount == 0
                && resolutionBreachCount == 0
                && warrantyExpiringSoonCount == 0)
            {
                continue;
            }

            rows.Add(new ProjectPostHandoverWatchReportItemDto
            {
                ProjectId = project.Id,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                ProjectStatus = project.Status,
                OpenHandoverItemCount = openHandoverItemCount,
                ActiveDefectLiabilityCount = activeDefects.Count,
                WarrantyCaseCount = projectDefects.Count(item => item.IsWarrantyRelated),
                ChargeableCaseCount = projectDefects.Count(item => !item.IsWarrantyRelated),
                ResponseBreachCount = responseBreachCount,
                ResolutionBreachCount = resolutionBreachCount,
                WarrantyExpiringSoonCount = warrantyExpiringSoonCount,
                AlertCount = responseBreachCount + resolutionBreachCount + warrantyExpiringSoonCount,
                HighestSeverity = highestSeverity,
                WatchState = ResolvePostHandoverWatchState(responseBreachCount, resolutionBreachCount, warrantyExpiringSoonCount, hasExpiredWarranty, openHandoverItemCount),
                TotalRectificationExposure = projectDefects.Sum(item => item.RectificationCost ?? 0m),
                ChargeableExposure = projectDefects.Sum(item => item.ChargeableAmount ?? 0m),
                WarrantyExposure = projectDefects.Where(item => item.IsWarrantyRelated).Sum(item => item.RectificationCost ?? 0m)
            });
        }

        return rows
            .OrderBy(item => GetConstructionSeverityRank(item.HighestSeverity))
            .ThenByDescending(item => item.AlertCount)
            .ThenBy(item => item.ProjectCode)
            .Take(take)
            .ToList();
    }

    private static string ResolvePostHandoverWatchState(
        int responseBreachCount,
        int resolutionBreachCount,
        int warrantyExpiringSoonCount,
        bool hasExpiredWarranty,
        int openHandoverItemCount)
    {
        if (resolutionBreachCount > 0 || hasExpiredWarranty)
        {
            return "Critical";
        }

        if (responseBreachCount > 0 || warrantyExpiringSoonCount > 0 || openHandoverItemCount > 0)
        {
            return "Attention";
        }

        return "Stable";
    }

    public async Task<IEnumerable<ProjectDesignControlReportItemDto>> GetDesignControlWatchReportAsync(Guid? projectId = null, int take = 250)
    {
        var projects = await LoadProjectsForConstructionReportAsync(projectId, projectId.HasValue ? 5 : Math.Max(take, 80));
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var drawings = (await _unitOfWork.Repository<ProjectDrawing>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var submittals = (await _unitOfWork.Repository<ProjectSubmittal>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var phases = (await _unitOfWork.Repository<ProjectPhase>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var packages = (await _unitOfWork.Repository<ProjectPackage>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var projectsById = projects.ToDictionary(x => x.Id);
        var today = DateTime.UtcNow.Date;

        var rows = new List<ProjectDesignControlReportItemDto>();
        foreach (var drawing in drawings)
        {
            if (!projectsById.TryGetValue(drawing.ProjectId, out var project))
            {
                continue;
            }

            var watch = EvaluateDrawingWatchState(drawing, today);
            if (!projectId.HasValue && watch == null)
            {
                continue;
            }

            rows.Add(new ProjectDesignControlReportItemDto
            {
                ProjectId = drawing.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                ProjectStatus = project.Status,
                ItemType = "Drawing",
                RecordId = drawing.Id,
                ProjectPhaseId = drawing.ProjectPhaseId,
                ProjectPhaseName = drawing.ProjectPhaseId.HasValue ? phases.GetValueOrDefault(drawing.ProjectPhaseId.Value) : null,
                ReferenceCode = drawing.DrawingNumber,
                Title = drawing.Title,
                Category = drawing.Discipline,
                Status = drawing.Status,
                WatchState = watch?.WatchState ?? "Stable",
                Severity = watch?.Severity ?? "None",
                ActionDueDate = drawing.ReviewDueDate,
                DaysToActionDue = drawing.ReviewDueDate.HasValue ? (drawing.ReviewDueDate.Value.Date - today).Days : null,
                ResponsibleParty = drawing.ResponsibleParty,
                IsAsBuilt = drawing.IsAsBuilt
            });
        }

        foreach (var submittal in submittals)
        {
            if (!projectsById.TryGetValue(submittal.ProjectId, out var project))
            {
                continue;
            }

            var watch = EvaluateSubmittalWatchState(submittal, today);
            if (!projectId.HasValue && watch == null)
            {
                continue;
            }

            rows.Add(new ProjectDesignControlReportItemDto
            {
                ProjectId = submittal.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                ProjectStatus = project.Status,
                ItemType = "Submittal",
                RecordId = submittal.Id,
                ProjectPhaseId = submittal.ProjectPhaseId,
                ProjectPhaseName = submittal.ProjectPhaseId.HasValue ? phases.GetValueOrDefault(submittal.ProjectPhaseId.Value) : null,
                ProjectPackageId = submittal.ProjectPackageId,
                ProjectPackageName = submittal.ProjectPackageId.HasValue ? packages.GetValueOrDefault(submittal.ProjectPackageId.Value) : null,
                ReferenceCode = submittal.ReferenceNumber ?? submittal.Title,
                Title = submittal.Title,
                Category = submittal.SubmittalType,
                Status = submittal.Status,
                WatchState = watch?.WatchState ?? "Stable",
                Severity = watch?.Severity ?? "None",
                ActionDueDate = submittal.ResponseDueDate,
                DaysToActionDue = submittal.ResponseDueDate.HasValue ? (submittal.ResponseDueDate.Value.Date - today).Days : null,
                ResponsibleParty = submittal.ResponsibleParty,
                IsAsBuilt = false
            });
        }

        return rows
            .OrderBy(item => GetConstructionSeverityRank(item.Severity))
            .ThenBy(item => item.ActionDueDate ?? DateTime.MaxValue)
            .ThenBy(item => item.ProjectCode)
            .ThenBy(item => item.ItemType)
            .ThenBy(item => item.ReferenceCode)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectSiteControlReportItemDto>> GetSiteControlsWatchReportAsync(Guid? projectId = null, int take = 250)
    {
        var projects = await LoadProjectsForConstructionReportAsync(projectId, projectId.HasValue ? 5 : Math.Max(take, 80));
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var rfis = (await _unitOfWork.Repository<ProjectRfi>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var siteInstructions = (await _unitOfWork.Repository<ProjectSiteInstruction>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var phases = (await _unitOfWork.Repository<ProjectPhase>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var packages = (await _unitOfWork.Repository<ProjectPackage>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var projectsById = projects.ToDictionary(x => x.Id);
        var today = DateTime.UtcNow.Date;

        var rows = new List<ProjectSiteControlReportItemDto>();
        foreach (var rfi in rfis)
        {
            if (!projectsById.TryGetValue(rfi.ProjectId, out var project))
            {
                continue;
            }

            var watch = EvaluateRfiWatchState(rfi, today);
            if (!projectId.HasValue && watch == null)
            {
                continue;
            }

            rows.Add(new ProjectSiteControlReportItemDto
            {
                ProjectId = rfi.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                ProjectStatus = project.Status,
                ItemType = "RFI",
                RecordId = rfi.Id,
                ProjectPhaseId = rfi.ProjectPhaseId,
                ProjectPhaseName = rfi.ProjectPhaseId.HasValue ? phases.GetValueOrDefault(rfi.ProjectPhaseId.Value) : null,
                ProjectPackageId = rfi.ProjectPackageId,
                ProjectPackageName = rfi.ProjectPackageId.HasValue ? packages.GetValueOrDefault(rfi.ProjectPackageId.Value) : null,
                ReferenceCode = rfi.ReferenceNumber ?? rfi.Subject,
                Title = rfi.Subject,
                Category = rfi.Priority,
                Status = rfi.Status,
                WatchState = watch?.WatchState ?? "Stable",
                Severity = watch?.Severity ?? "None",
                ActionDueDate = rfi.ResponseDueDate,
                DaysToActionDue = rfi.ResponseDueDate.HasValue ? (rfi.ResponseDueDate.Value.Date - today).Days : null,
                ResponsibleParty = rfi.RespondedByName,
                ScheduleImpactDays = null
            });
        }

        foreach (var instruction in siteInstructions)
        {
            if (!projectsById.TryGetValue(instruction.ProjectId, out var project))
            {
                continue;
            }

            var watch = EvaluateSiteInstructionWatchState(instruction, today);
            if (!projectId.HasValue && watch == null)
            {
                continue;
            }

            rows.Add(new ProjectSiteControlReportItemDto
            {
                ProjectId = instruction.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                ProjectStatus = project.Status,
                ItemType = "SiteInstruction",
                RecordId = instruction.Id,
                ProjectPhaseId = instruction.ProjectPhaseId,
                ProjectPhaseName = instruction.ProjectPhaseId.HasValue ? phases.GetValueOrDefault(instruction.ProjectPhaseId.Value) : null,
                ProjectPackageId = instruction.ProjectPackageId,
                ProjectPackageName = instruction.ProjectPackageId.HasValue ? packages.GetValueOrDefault(instruction.ProjectPackageId.Value) : null,
                ReferenceCode = instruction.ReferenceNumber ?? instruction.Title,
                Title = instruction.Title,
                Category = instruction.InstructionType,
                Status = instruction.Status,
                WatchState = watch?.WatchState ?? "Stable",
                Severity = watch?.Severity ?? "None",
                ActionDueDate = instruction.EffectiveDate ?? instruction.IssuedDate,
                DaysToActionDue = (instruction.EffectiveDate ?? instruction.IssuedDate).Date == default
                    ? null
                    : ((instruction.EffectiveDate ?? instruction.IssuedDate).Date - today).Days,
                ResponsibleParty = instruction.ResponsibleParty,
                EstimatedCostImpact = instruction.EstimatedCostImpact,
                ScheduleImpactDays = instruction.ScheduleImpactDays
            });
        }

        return rows
            .OrderBy(item => GetConstructionSeverityRank(item.Severity))
            .ThenBy(item => item.ActionDueDate ?? DateTime.MaxValue)
            .ThenBy(item => item.ProjectCode)
            .ThenBy(item => item.ItemType)
            .ThenBy(item => item.ReferenceCode)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectUnitCommercializationReportItemDto>> GetUnitCommercializationWatchReportAsync(Guid? projectId = null, int take = 250)
    {
        var projects = await LoadProjectsForConstructionReportAsync(projectId, projectId.HasValue ? 5 : Math.Max(take, 80));
        if (projects.Count == 0)
        {
            return [];
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var units = (await _unitOfWork.Repository<ProjectUnit>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToList();
        var buildings = (await _unitOfWork.Repository<ProjectBuilding>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var floors = (await _unitOfWork.Repository<ProjectFloor>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var releaseBatches = (await _unitOfWork.Repository<ProjectUnitReleaseBatch>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectIds.Contains(x.ProjectId)))
            .ToDictionary(x => x.Id, x => x.Name);
        var customerIds = units.Where(x => x.CustomerBusinessPartnerId.HasValue).Select(x => x.CustomerBusinessPartnerId!.Value).Distinct().ToList();
        var salesAgreementIds = units.Where(x => x.SalesAgreementId.HasValue).Select(x => x.SalesAgreementId!.Value).Distinct().ToList();
        var salesOrderIds = units.Where(x => x.SalesOrderId.HasValue).Select(x => x.SalesOrderId!.Value).Distinct().ToList();
        var customers = customerIds.Count == 0
            ? new Dictionary<Guid, BusinessPartner>()
            : (await _unitOfWork.Repository<BusinessPartner>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && customerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x);
        var salesAgreements = salesAgreementIds.Count == 0
            ? new Dictionary<Guid, SalesAgreement>()
            : (await _unitOfWork.Repository<SalesAgreement>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && salesAgreementIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x);
        var salesOrders = salesOrderIds.Count == 0
            ? new Dictionary<Guid, SalesOrder>()
            : (await _unitOfWork.Repository<SalesOrder>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && salesOrderIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x);
        var projectsById = projects.ToDictionary(x => x.Id);

        return units
            .Select(unit =>
            {
                if (!projectsById.TryGetValue(unit.ProjectId, out var project))
                {
                    return null;
                }

                customers.TryGetValue(unit.CustomerBusinessPartnerId ?? Guid.Empty, out var customer);
                salesAgreements.TryGetValue(unit.SalesAgreementId ?? Guid.Empty, out var salesAgreement);
                salesOrders.TryGetValue(unit.SalesOrderId ?? Guid.Empty, out var salesOrder);
                var commercialStatus = DeriveProjectUnitCommercialStatus(unit, salesAgreement, salesOrder);
                var commercialIntent = DeriveProjectUnitCommercialIntent(salesAgreement, salesOrder);
                var handoverStatus = DeriveProjectUnitHandoverStatus(unit, commercialStatus);
                var watch = EvaluateUnitCommercializationWatchState(unit, commercialStatus, commercialIntent, handoverStatus, salesAgreement, salesOrder);
                if (!projectId.HasValue && watch == null)
                {
                    return null;
                }

                return new ProjectUnitCommercializationReportItemDto
                {
                    ProjectId = unit.ProjectId,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    ProjectStatus = project.Status,
                    ProjectUnitId = unit.Id,
                    ProjectBuildingName = unit.ProjectBuildingId.HasValue ? buildings.GetValueOrDefault(unit.ProjectBuildingId.Value) : null,
                    ProjectFloorName = unit.ProjectFloorId.HasValue ? floors.GetValueOrDefault(unit.ProjectFloorId.Value) : null,
                    ProjectUnitReleaseBatchName = unit.ProjectUnitReleaseBatchId.HasValue ? releaseBatches.GetValueOrDefault(unit.ProjectUnitReleaseBatchId.Value) : null,
                    UnitCode = unit.Code,
                    UnitName = unit.Name,
                    UnitType = unit.UnitType,
                    Status = unit.Status,
                    CommercialStatus = commercialStatus,
                    CommercialIntent = commercialIntent,
                    HandoverStatus = handoverStatus,
                    IsReleasedForMarket = unit.IsReleasedForMarket,
                    ReleaseState = unit.IsReleasedForMarket ? "Released" : "Withheld",
                    CustomerBusinessPartnerName = customer?.PartnerName ?? customer?.PrimaryContactName,
                    SalesAgreementNumber = salesAgreement?.DocumentNumber,
                    SalesOrderNumber = salesOrder?.DocumentNumber,
                    AreaSquareMeters = unit.AreaSquareMeters,
                    BasePrice = unit.BasePrice,
                    Currency = unit.Currency,
                    WatchState = watch?.WatchState ?? "Stable",
                    Severity = watch?.Severity ?? "None",
                    WatchMessage = watch?.Message
                };
            })
            .Where(item => item != null)
            .Cast<ProjectUnitCommercializationReportItemDto>()
            .OrderBy(item => GetConstructionSeverityRank(item.Severity))
            .ThenBy(item => item.ProjectCode)
            .ThenBy(item => item.ProjectBuildingName)
            .ThenBy(item => item.ProjectFloorName)
            .ThenBy(item => item.UnitCode ?? item.UnitName)
            .Take(take)
            .ToList();
    }

    private static (string WatchState, string Severity)? EvaluateDrawingWatchState(ProjectDrawing drawing, DateTime today)
    {
        if (string.Equals(drawing.Status, ProjectDrawingStatuses.Archived, StringComparison.OrdinalIgnoreCase)
            || string.Equals(drawing.Status, ProjectDrawingStatuses.Superseded, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(drawing.Status, ProjectDrawingStatuses.ForReview, StringComparison.OrdinalIgnoreCase))
        {
            if (drawing.ReviewDueDate.HasValue && drawing.ReviewDueDate.Value.Date < today)
            {
                return ("ReviewOverdue", "High");
            }

            if (drawing.ReviewDueDate.HasValue && drawing.ReviewDueDate.Value.Date <= today.AddDays(7))
            {
                return ("ReviewDueSoon", "Warning");
            }

            return ("PendingReview", "Warning");
        }

        if (string.Equals(drawing.Status, ProjectDrawingStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return ("Draft", "Low");
        }

        if (string.Equals(drawing.Status, ProjectDrawingStatuses.ApprovedForConstruction, StringComparison.OrdinalIgnoreCase))
        {
            return ("ReadyForConstruction", "None");
        }

        return null;
    }

    private static (string WatchState, string Severity)? EvaluateSubmittalWatchState(ProjectSubmittal submittal, DateTime today)
    {
        if (string.Equals(submittal.Status, ProjectSubmittalStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(submittal.Status, ProjectSubmittalStatuses.Rejected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(submittal.Status, ProjectSubmittalStatuses.ResubmissionRequired, StringComparison.OrdinalIgnoreCase))
        {
            return ("ResubmissionRequired", "High");
        }

        if (string.Equals(submittal.Status, ProjectSubmittalStatuses.Submitted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(submittal.Status, ProjectSubmittalStatuses.UnderReview, StringComparison.OrdinalIgnoreCase))
        {
            if (submittal.ResponseDueDate.HasValue && submittal.ResponseDueDate.Value.Date < today)
            {
                return ("ResponseOverdue", "High");
            }

            if (submittal.ResponseDueDate.HasValue && submittal.ResponseDueDate.Value.Date <= today.AddDays(7))
            {
                return ("ResponseDueSoon", "Warning");
            }

            return ("AwaitingResponse", "Warning");
        }

        if (string.Equals(submittal.Status, ProjectSubmittalStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return ("Draft", "Low");
        }

        return ("Approved", "None");
    }

    private static (string WatchState, string Severity)? EvaluateRfiWatchState(ProjectRfi rfi, DateTime today)
    {
        if (string.Equals(rfi.Status, ProjectRfiStatuses.Closed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rfi.Status, ProjectRfiStatuses.Void, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(rfi.Status, ProjectRfiStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            var severity = string.Equals(rfi.Priority, ProjectRfiPriorities.Critical, StringComparison.OrdinalIgnoreCase)
                ? "Critical"
                : string.Equals(rfi.Priority, ProjectRfiPriorities.High, StringComparison.OrdinalIgnoreCase)
                    ? "High"
                    : "Warning";

            if (rfi.ResponseDueDate.HasValue && rfi.ResponseDueDate.Value.Date < today)
            {
                return ("ResponseOverdue", severity);
            }

            if (rfi.ResponseDueDate.HasValue && rfi.ResponseDueDate.Value.Date <= today.AddDays(3))
            {
                return ("ResponseDueSoon", severity);
            }

            return ("AwaitingResponse", severity);
        }

        if (string.Equals(rfi.Status, ProjectRfiStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return ("Draft", "Low");
        }

        return ("AnsweredPendingClose", "Low");
    }

    private static (string WatchState, string Severity)? EvaluateSiteInstructionWatchState(ProjectSiteInstruction instruction, DateTime today)
    {
        if (string.Equals(instruction.Status, ProjectSiteInstructionStatuses.Closed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(instruction.Status, ProjectSiteInstructionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(instruction.Status, ProjectSiteInstructionStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return ("Draft", "Low");
        }

        var hasCommercialImpact = (instruction.EstimatedCostImpact ?? 0m) > 0m || (instruction.ScheduleImpactDays ?? 0) > 0;
        var baselineDate = instruction.EffectiveDate ?? instruction.IssuedDate;
        if (baselineDate.Date < today.AddDays(-7)
            && !string.Equals(instruction.Status, ProjectSiteInstructionStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            return ("ImplementationOutstanding", hasCommercialImpact ? "High" : "Warning");
        }

        if (hasCommercialImpact)
        {
            return ("CommercialImpact", "Warning");
        }

        return ("OpenInstruction", "Low");
    }

    private static (string WatchState, string Severity, string Message)? EvaluateUnitCommercializationWatchState(
        ProjectUnit unit,
        string commercialStatus,
        string? commercialIntent,
        string handoverStatus,
        SalesAgreement? salesAgreement,
        SalesOrder? salesOrder)
    {
        if (string.Equals(unit.Status, ProjectUnitStatuses.Archived, StringComparison.OrdinalIgnoreCase)
            || string.Equals(unit.Status, ProjectUnitStatuses.Planned, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(unit.Status, ProjectUnitStatuses.Sold, StringComparison.OrdinalIgnoreCase) && salesOrder == null)
        {
            return ("SalesLinkMissing", "Critical", "Unit is marked sold without a linked sales order.");
        }

        if (string.Equals(unit.Status, ProjectUnitStatuses.Leased, StringComparison.OrdinalIgnoreCase) && salesAgreement == null)
        {
            return ("LeaseLinkMissing", "High", "Unit is marked leased without a linked lease or tenancy agreement.");
        }

        if ((string.Equals(handoverStatus, ProjectUnitHandoverStatuses.HandedOver, StringComparison.OrdinalIgnoreCase)
            || string.Equals(handoverStatus, ProjectUnitHandoverStatuses.Occupied, StringComparison.OrdinalIgnoreCase))
            && !unit.HandoverDate.HasValue)
        {
            return ("HandoverDataMissing", "High", "Unit handover status is set without a handover date.");
        }

        if (unit.IsReleasedForMarket && !unit.BasePrice.HasValue)
        {
            return ("PricingMissing", "High", "Released unit does not have a base price configured.");
        }

        if (!unit.IsReleasedForMarket
            && (string.Equals(unit.Status, ProjectUnitStatuses.Available, StringComparison.OrdinalIgnoreCase)
                || string.Equals(commercialStatus, ProjectUnitStatuses.Available, StringComparison.OrdinalIgnoreCase)))
        {
            return ("NotReleased", "Warning", "Available unit has not been released to market.");
        }

        if ((string.Equals(unit.Status, ProjectUnitStatuses.Reserved, StringComparison.OrdinalIgnoreCase)
                || string.Equals(commercialStatus, ProjectUnitStatuses.Reserved, StringComparison.OrdinalIgnoreCase))
            && salesAgreement == null
            && salesOrder == null)
        {
            return ("ReservationUnlinked", "Warning", "Reserved unit has no linked sales agreement or order.");
        }

        if (unit.IsReleasedForMarket
            && string.Equals(commercialStatus, ProjectUnitStatuses.Available, StringComparison.OrdinalIgnoreCase))
        {
            return ("MarketReady", "None", "Released unit is ready for commercialization.");
        }

        if (salesAgreement != null || salesOrder != null || !string.IsNullOrWhiteSpace(commercialIntent))
        {
            return ("Commercialized", "None", "Unit has downstream commercial linkage.");
        }

        return ("Commercialized", "None", "Unit has downstream commercial linkage.");
    }

    private static string MaxSeverityLabel(string current, string next)
        => GetConstructionSeverityRank(next) < GetConstructionSeverityRank(current) ? next : current;
}
