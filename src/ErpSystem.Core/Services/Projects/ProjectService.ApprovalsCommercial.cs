using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectApprovalRegisterItemDto>> GetApprovalRegisterAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await GetProjectApprovalRegisterEntitiesAsync(projectId))
            .Select(item => MapToDto(item))
            .ToList();
    }

    public async Task<ProjectApprovalRegisterItemDto> AddApprovalRegisterItemAsync(Guid projectId, CreateProjectApprovalRegisterItemDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        await ValidateProjectApprovalRegisterItemAsync(projectId, dto);

        var entity = new ProjectApprovalRegisterItem
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = dto.ProjectPhaseId,
            ApprovalType = NormalizeProjectApprovalType(dto.ApprovalType),
            Title = dto.Title.Trim(),
            AuthorityName = TrimOrNull(dto.AuthorityName),
            ReferenceNumber = TrimOrNull(dto.ReferenceNumber),
            Status = NormalizeProjectApprovalStatus(dto.Status),
            IsRequired = dto.IsRequired,
            SubmittedDate = dto.SubmittedDate,
            TargetDecisionDate = dto.TargetDecisionDate,
            ApprovedDate = dto.ApprovedDate,
            ExpiryDate = dto.ExpiryDate,
            ConditionSummary = TrimOrNull(dto.ConditionSummary),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectApprovalRegisterItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await BuildApprovalRegisterItemDtoAsync(entity);
    }

    public async Task<ProjectApprovalRegisterItemDto> UpdateApprovalRegisterItemAsync(Guid approvalRegisterItemId, UpdateProjectApprovalRegisterItemDto dto)
    {
        var entity = await GetProjectApprovalRegisterItemEntityAsync(approvalRegisterItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await ValidateProjectApprovalRegisterItemAsync(entity.ProjectId, dto);

        entity.ProjectPhaseId = dto.ProjectPhaseId;
        entity.ApprovalType = NormalizeProjectApprovalType(dto.ApprovalType);
        entity.Title = dto.Title.Trim();
        entity.AuthorityName = TrimOrNull(dto.AuthorityName);
        entity.ReferenceNumber = TrimOrNull(dto.ReferenceNumber);
        entity.Status = NormalizeProjectApprovalStatus(dto.Status);
        entity.IsRequired = dto.IsRequired;
        entity.SubmittedDate = dto.SubmittedDate;
        entity.TargetDecisionDate = dto.TargetDecisionDate;
        entity.ApprovedDate = dto.ApprovedDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.ConditionSummary = TrimOrNull(dto.ConditionSummary);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectApprovalRegisterItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await BuildApprovalRegisterItemDtoAsync(entity);
    }

    public async Task DeleteApprovalRegisterItemAsync(Guid approvalRegisterItemId)
    {
        var entity = await GetProjectApprovalRegisterItemEntityAsync(approvalRegisterItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectApprovalRegisterItem>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ProjectCommercialSummaryDto> GetCommercialSummaryAsync(Guid projectId)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToList();
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToList();
        var boqItems = (await GetProjectBoqItemEntitiesAsync(projectId)).ToList();
        var variationOrders = (await GetProjectVariationOrderEntitiesAsync(projectId)).ToList();
        var interimValuations = (await GetProjectInterimValuationEntitiesAsync(projectId)).ToList();
        var paymentCertificates = (await GetProjectPaymentCertificateEntitiesAsync(projectId)).ToList();
        var extensionOfTimeRequests = (await GetProjectExtensionOfTimeEntitiesAsync(projectId)).ToList();
        var finalAccount = await GetProjectFinalAccountEntityAsync(projectId);
        var currencyCode = await GetProjectBaseCurrencyCodeAsync();

        var alerts = new List<ProjectCommercialAlertDto>();
        if (packages.Count == 0)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "Medium",
                Message = "No construction packages have been configured yet."
            });
        }

        var unassignedPackageCount = packages.Count(x => !x.ProjectPhaseId.HasValue);
        if (unassignedPackageCount > 0)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "Medium",
                Message = $"{unassignedPackageCount} package(s) are not assigned to a project phase."
            });
        }

        var packagesOverForecast = packages.Count(x => (x.ForecastAmount ?? 0m) > (x.BudgetAmount ?? 0m) && (x.BudgetAmount ?? 0m) > 0m);
        if (packagesOverForecast > 0)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "High",
                Message = $"{packagesOverForecast} package(s) have forecast cost above budget."
            });
        }

        var phaseLinkedPackages = packages
            .Where(x => x.ProjectPhaseId.HasValue)
            .Join(phases, package => package.ProjectPhaseId!.Value, phase => phase.Id, (package, phase) => new { Package = package, Phase = phase })
            .ToList();

        var phaseAlignedPackageCount = phaseLinkedPackages.Count(item => IsPackageCommerciallyAlignedToPhase(item.Phase, item.Package));
        var phaseLaggingPackageCount = phaseLinkedPackages.Count - phaseAlignedPackageCount;

        var activeProcurementPhaseLaggingCount = phaseLinkedPackages.Count(item =>
            string.Equals(item.Phase.Status, ProjectPhaseStatuses.InProgress, StringComparison.OrdinalIgnoreCase)
            && IsProcurementPhase(item.Phase)
            && !IsPackageCommerciallyAlignedToPhase(item.Phase, item.Package));
        if (activeProcurementPhaseLaggingCount > 0)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "Medium",
                Message = $"{activeProcurementPhaseLaggingCount} package(s) in active procurement phases are still lagging procurement setup or award status."
            });
        }

        var activeConstructionPhaseLaggingCount = phaseLinkedPackages.Count(item =>
            string.Equals(item.Phase.Status, ProjectPhaseStatuses.InProgress, StringComparison.OrdinalIgnoreCase)
            && IsConstructionPhase(item.Phase)
            && !IsPackageCommerciallyAlignedToPhase(item.Phase, item.Package));
        if (activeConstructionPhaseLaggingCount > 0)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "High",
                Message = $"{activeConstructionPhaseLaggingCount} package(s) in active construction phases are not yet commercially active."
            });
        }

        var packageBudgetAmount = packages.Sum(x => x.BudgetAmount ?? 0m);
        var packageCommittedAmount = packages.Sum(x => x.CommittedAmount ?? 0m);
        var packageActualAmount = packages.Sum(x => x.ActualAmount ?? 0m);
        var packageForecastAmount = packages.Sum(x => x.ForecastAmount ?? 0m);
        var approvedBudget = project.ApprovedBudget ?? 0m;

        if (approvedBudget > 0m && packageForecastAmount > approvedBudget)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "High",
                Message = "Package forecast exceeds the approved project budget."
            });
        }

        var approvedVariationAmount = variationOrders
            .Where(x => x.Status == ProjectVariationOrderStatuses.Approved || x.Status == ProjectVariationOrderStatuses.Implemented || x.Status == ProjectVariationOrderStatuses.Closed)
            .Sum(x => x.ApprovedAmount ?? x.EstimatedAmount ?? 0m);
        var netValuationAmount = interimValuations.Sum(x => x.NetValuationAmount);
        var netCertifiedAmount = paymentCertificates.Sum(x => x.NetCertifiedAmount);
        var retentionHeldAmount = paymentCertificates.Sum(x => x.RetentionHeldAmount) + interimValuations.Sum(x => x.RetentionAmount);
        var approvedExtensionDays = extensionOfTimeRequests
            .Where(x => x.Status == ProjectExtensionOfTimeStatuses.Approved || x.Status == ProjectExtensionOfTimeStatuses.Implemented || x.Status == ProjectExtensionOfTimeStatuses.Closed)
            .Sum(x => x.DaysApproved ?? 0);

        if (variationOrders.Count == 0)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "Low",
                Message = "No variation orders have been recorded yet."
            });
        }

        if (paymentCertificates.Any() && netCertifiedAmount < netValuationAmount)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "Medium",
                Message = "Certified payment amount is below recorded interim valuation value."
            });
        }

        if (finalAccount != null && finalAccount.Status != ProjectFinalAccountStatuses.Closed)
        {
            alerts.Add(new ProjectCommercialAlertDto
            {
                Severity = "Low",
                Message = $"Final account is currently {finalAccount.Status}."
            });
        }

        var phaseRollups = phases
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(phase =>
            {
                var phasePackages = packages.Where(x => x.ProjectPhaseId == phase.Id).ToList();
                var phasePackageIds = phasePackages.Select(x => x.Id).ToHashSet();
                var phaseBoqItems = boqItems.Where(x => phasePackageIds.Contains(x.ProjectPackageId)).ToList();

                return new ProjectPhaseCommercialRollupDto
                {
                    ProjectPhaseId = phase.Id,
                    PhaseName = phase.Name,
                    PhaseSortOrder = phase.SortOrder,
                    PackageCount = phasePackages.Count,
                    BoqItemCount = phaseBoqItems.Count,
                    BudgetAmount = phasePackages.Sum(x => x.BudgetAmount ?? 0m),
                    CommittedAmount = phasePackages.Sum(x => x.CommittedAmount ?? 0m),
                    ActualAmount = phasePackages.Sum(x => x.ActualAmount ?? 0m),
                    ForecastAmount = phasePackages.Sum(x => x.ForecastAmount ?? 0m),
                    VarianceAmount = phasePackages.Sum(x => x.BudgetAmount ?? 0m) - phasePackages.Sum(x => x.ForecastAmount ?? 0m)
                };
            })
            .ToList();

        if (unassignedPackageCount > 0)
        {
            var unassignedPackages = packages.Where(x => !x.ProjectPhaseId.HasValue).ToList();
            var unassignedPackageIds = unassignedPackages.Select(x => x.Id).ToHashSet();
            var unassignedBoqItems = boqItems.Where(x => unassignedPackageIds.Contains(x.ProjectPackageId)).ToList();
            phaseRollups.Add(new ProjectPhaseCommercialRollupDto
            {
                ProjectPhaseId = null,
                PhaseName = "Unassigned",
                PhaseSortOrder = int.MaxValue,
                PackageCount = unassignedPackages.Count,
                BoqItemCount = unassignedBoqItems.Count,
                BudgetAmount = unassignedPackages.Sum(x => x.BudgetAmount ?? 0m),
                CommittedAmount = unassignedPackages.Sum(x => x.CommittedAmount ?? 0m),
                ActualAmount = unassignedPackages.Sum(x => x.ActualAmount ?? 0m),
                ForecastAmount = unassignedPackages.Sum(x => x.ForecastAmount ?? 0m),
                VarianceAmount = unassignedPackages.Sum(x => x.BudgetAmount ?? 0m) - unassignedPackages.Sum(x => x.ForecastAmount ?? 0m)
            });
        }

        return new ProjectCommercialSummaryDto
        {
            ProjectId = project.Id,
            Currency = currencyCode,
            EstimatedBudget = project.EstimatedBudget ?? 0m,
            ApprovedBudget = approvedBudget,
            PackageBudgetAmount = packageBudgetAmount,
            PackageCommittedAmount = packageCommittedAmount,
            PackageActualAmount = packageActualAmount,
            PackageForecastAmount = packageForecastAmount,
            ForecastVarianceAmount = packageBudgetAmount - packageForecastAmount,
            PackageCount = packages.Count,
            BoqItemCount = boqItems.Count,
            UnassignedPackageCount = unassignedPackageCount,
            TenderLinkedPackageCount = packages.Count(x => x.TenderId.HasValue),
            ContractLinkedPackageCount = packages.Count(x => x.ContractId.HasValue),
            PurchaseRequisitionLinkedPackageCount = packages.Count(x => x.PurchaseRequisitionId.HasValue),
            PurchaseOrderLinkedPackageCount = packages.Count(x => x.PurchaseOrderId.HasValue),
            PhaseAlignedPackageCount = phaseAlignedPackageCount,
            PhaseLaggingPackageCount = phaseLaggingPackageCount,
            VariationOrderCount = variationOrders.Count,
            ApprovedVariationAmount = approvedVariationAmount,
            InterimValuationCount = interimValuations.Count,
            NetValuationAmount = netValuationAmount,
            PaymentCertificateCount = paymentCertificates.Count,
            NetCertifiedAmount = netCertifiedAmount,
            RetentionHeldAmount = retentionHeldAmount,
            ExtensionOfTimeCount = extensionOfTimeRequests.Count,
            ApprovedExtensionDays = approvedExtensionDays,
            FinalAccountStatus = finalAccount?.Status,
            FinalAccountValue = finalAccount?.FinalAccountValue,
            PhaseRollups = phaseRollups.OrderBy(x => x.PhaseSortOrder).ThenBy(x => x.PhaseName).ToList(),
            Alerts = alerts
        };
    }

    private async Task<List<ProjectApprovalRegisterItem>> GetProjectApprovalRegisterEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectApprovalRegisterItem>();
        if (repository == null)
        {
            return [];
        }

        var items = (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.ProjectPhaseId.HasValue ? 0 : 1)
            .ThenBy(x => x.TargetDecisionDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Title)
            .ToList();

        return items;
    }

    private async Task<ProjectApprovalRegisterItem> GetProjectApprovalRegisterItemEntityAsync(Guid approvalRegisterItemId)
        => await _unitOfWork.Repository<ProjectApprovalRegisterItem>().FirstOrDefaultAsync(x =>
               x.Id == approvalRegisterItemId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project approval register item with ID {approvalRegisterItemId} not found");

    private async Task ValidateProjectApprovalRegisterItemAsync(Guid projectId, CreateProjectApprovalRegisterItemDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new InvalidOperationException("Approval title is required.");
        }

        if (dto.ProjectPhaseId.HasValue)
        {
            var phase = await GetProjectPhaseEntityAsync(dto.ProjectPhaseId.Value);
            if (phase.ProjectId != projectId)
            {
                throw new InvalidOperationException("Approval phase must belong to the selected project.");
            }
        }
    }

    private async Task<ProjectApprovalRegisterItemDto> BuildApprovalRegisterItemDtoAsync(ProjectApprovalRegisterItem entity)
    {
        ProjectPhase? phase = null;
        if (entity.ProjectPhaseId.HasValue)
        {
            phase = await _unitOfWork.Repository<ProjectPhase>().FirstOrDefaultAsync(x =>
                x.Id == entity.ProjectPhaseId.Value
                && x.TenantId == _currentUserProvider.TenantId);
        }

        return MapToDto(entity, phase);
    }

    private static string NormalizeProjectApprovalType(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectApprovalRegisterTypes.Other,
            _ => value.Trim()
        };

    private static string NormalizeProjectApprovalStatus(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectApprovalRegisterStatuses.Planned,
            _ => value.Trim()
        };

    private static ProjectApprovalRegisterItemDto MapToDto(ProjectApprovalRegisterItem entity, ProjectPhase? phase = null) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectPhaseId = entity.ProjectPhaseId,
        ProjectPhaseName = phase?.Name,
        ApprovalType = entity.ApprovalType,
        Title = entity.Title,
        AuthorityName = entity.AuthorityName,
        ReferenceNumber = entity.ReferenceNumber,
        Status = entity.Status,
        IsRequired = entity.IsRequired,
        SubmittedDate = entity.SubmittedDate,
        TargetDecisionDate = entity.TargetDecisionDate,
        ApprovedDate = entity.ApprovedDate,
        ExpiryDate = entity.ExpiryDate,
        ConditionSummary = entity.ConditionSummary,
        Notes = entity.Notes
    };
}
