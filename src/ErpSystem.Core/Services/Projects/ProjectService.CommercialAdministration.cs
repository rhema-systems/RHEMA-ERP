using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectVariationOrderDto>> GetProjectVariationOrdersAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectVariationOrdersAsync((await GetProjectVariationOrderEntitiesAsync(projectId)).ToList(), projectId);
    }

    public async Task<ProjectVariationOrderDto> AddProjectVariationOrderAsync(Guid projectId, CreateProjectVariationOrderDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(projectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);

        var entity = new ProjectVariationOrder
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            ProjectPackageId = package?.Id,
            ContractId = contract?.Id,
            ReferenceNumber = TrimOrNull(dto.ReferenceNumber),
            Title = dto.Title.Trim(),
            Description = TrimOrNull(dto.Description),
            VariationType = NormalizeProjectVariationOrderType(dto.VariationType),
            Status = NormalizeProjectVariationOrderStatus(dto.Status),
            RequestedDate = dto.RequestedDate ?? DateTime.UtcNow,
            ApprovedDate = dto.ApprovedDate,
            ImplementedDate = dto.ImplementedDate,
            RequestedByName = TrimOrNull(dto.RequestedByName),
            ApprovedByName = TrimOrNull(dto.ApprovedByName),
            EstimatedAmount = dto.EstimatedAmount,
            ApprovedAmount = dto.ApprovedAmount,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency),
            ScheduleImpactDays = dto.ScheduleImpactDays,
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectVariationOrder>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectVariationOrderDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectVariationOrderDto> UpdateProjectVariationOrderAsync(Guid variationOrderId, UpdateProjectVariationOrderDto dto)
    {
        var entity = await GetProjectVariationOrderEntityAsync(variationOrderId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(entity.ProjectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);

        entity.ProjectPhaseId = phase?.Id;
        entity.ProjectPackageId = package?.Id;
        entity.ContractId = contract?.Id;
        entity.ReferenceNumber = TrimOrNull(dto.ReferenceNumber);
        entity.Title = dto.Title.Trim();
        entity.Description = TrimOrNull(dto.Description);
        entity.VariationType = NormalizeProjectVariationOrderType(dto.VariationType);
        entity.Status = NormalizeProjectVariationOrderStatus(dto.Status);
        entity.RequestedDate = dto.RequestedDate ?? entity.RequestedDate;
        entity.ApprovedDate = dto.ApprovedDate;
        entity.ImplementedDate = dto.ImplementedDate;
        entity.RequestedByName = TrimOrNull(dto.RequestedByName);
        entity.ApprovedByName = TrimOrNull(dto.ApprovedByName);
        entity.EstimatedAmount = dto.EstimatedAmount;
        entity.ApprovedAmount = dto.ApprovedAmount;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? entity.Currency);
        entity.ScheduleImpactDays = dto.ScheduleImpactDays;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectVariationOrder>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectVariationOrderDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectVariationOrderAsync(Guid variationOrderId)
    {
        var entity = await GetProjectVariationOrderEntityAsync(variationOrderId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectVariationOrder>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectInterimValuationDto>> GetProjectInterimValuationsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectInterimValuationsAsync((await GetProjectInterimValuationEntitiesAsync(projectId)).ToList(), projectId);
    }

    public async Task<ProjectInterimValuationDto> AddProjectInterimValuationAsync(Guid projectId, CreateProjectInterimValuationDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(projectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);

        var entity = new ProjectInterimValuation
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            ProjectPackageId = package?.Id,
            ContractId = contract?.Id,
            ValuationNumber = TrimOrNull(dto.ValuationNumber),
            Title = dto.Title.Trim(),
            Status = NormalizeProjectInterimValuationStatus(dto.Status),
            ValuationDate = dto.ValuationDate ?? DateTime.UtcNow,
            GrossWorkValue = dto.GrossWorkValue,
            MaterialsOnSiteValue = dto.MaterialsOnSiteValue,
            VariationValue = dto.VariationValue,
            RetentionPercentage = dto.RetentionPercentage,
            RetentionAmount = dto.RetentionAmount,
            PreviousCertifiedAmount = dto.PreviousCertifiedAmount,
            NetValuationAmount = dto.NetValuationAmount,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectInterimValuation>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectInterimValuationDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectInterimValuationDto> UpdateProjectInterimValuationAsync(Guid interimValuationId, UpdateProjectInterimValuationDto dto)
    {
        var entity = await GetProjectInterimValuationEntityAsync(interimValuationId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(entity.ProjectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);

        entity.ProjectPhaseId = phase?.Id;
        entity.ProjectPackageId = package?.Id;
        entity.ContractId = contract?.Id;
        entity.ValuationNumber = TrimOrNull(dto.ValuationNumber);
        entity.Title = dto.Title.Trim();
        entity.Status = NormalizeProjectInterimValuationStatus(dto.Status);
        entity.ValuationDate = dto.ValuationDate ?? entity.ValuationDate;
        entity.GrossWorkValue = dto.GrossWorkValue;
        entity.MaterialsOnSiteValue = dto.MaterialsOnSiteValue;
        entity.VariationValue = dto.VariationValue;
        entity.RetentionPercentage = dto.RetentionPercentage;
        entity.RetentionAmount = dto.RetentionAmount;
        entity.PreviousCertifiedAmount = dto.PreviousCertifiedAmount;
        entity.NetValuationAmount = dto.NetValuationAmount;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? entity.Currency);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectInterimValuation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectInterimValuationDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectInterimValuationAsync(Guid interimValuationId)
    {
        var entity = await GetProjectInterimValuationEntityAsync(interimValuationId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectInterimValuation>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectPaymentCertificateDto>> GetProjectPaymentCertificatesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectPaymentCertificatesAsync((await GetProjectPaymentCertificateEntitiesAsync(projectId)).ToList(), projectId);
    }

    public async Task<ProjectPaymentCertificateDto> AddProjectPaymentCertificateAsync(Guid projectId, CreateProjectPaymentCertificateDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(projectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);
        var interimValuation = await ValidateProjectCommercialInterimValuationAsync(projectId, dto.ProjectInterimValuationId);

        var entity = new ProjectPaymentCertificate
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            ProjectPackageId = package?.Id,
            ContractId = contract?.Id,
            ProjectInterimValuationId = interimValuation?.Id,
            CertificateNumber = TrimOrNull(dto.CertificateNumber),
            Title = dto.Title.Trim(),
            Status = NormalizeProjectPaymentCertificateStatus(dto.Status),
            IssueDate = dto.IssueDate ?? DateTime.UtcNow,
            PaymentDueDate = dto.PaymentDueDate,
            GrossCertifiedAmount = dto.GrossCertifiedAmount,
            RetentionHeldAmount = dto.RetentionHeldAmount,
            RetentionReleasedAmount = dto.RetentionReleasedAmount,
            OtherDeductionsAmount = dto.OtherDeductionsAmount,
            NetCertifiedAmount = dto.NetCertifiedAmount,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectPaymentCertificate>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectPaymentCertificateDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectPaymentCertificateDto> UpdateProjectPaymentCertificateAsync(Guid paymentCertificateId, UpdateProjectPaymentCertificateDto dto)
    {
        var entity = await GetProjectPaymentCertificateEntityAsync(paymentCertificateId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(entity.ProjectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);
        var interimValuation = await ValidateProjectCommercialInterimValuationAsync(entity.ProjectId, dto.ProjectInterimValuationId);

        entity.ProjectPhaseId = phase?.Id;
        entity.ProjectPackageId = package?.Id;
        entity.ContractId = contract?.Id;
        entity.ProjectInterimValuationId = interimValuation?.Id;
        entity.CertificateNumber = TrimOrNull(dto.CertificateNumber);
        entity.Title = dto.Title.Trim();
        entity.Status = NormalizeProjectPaymentCertificateStatus(dto.Status);
        entity.IssueDate = dto.IssueDate ?? entity.IssueDate;
        entity.PaymentDueDate = dto.PaymentDueDate;
        entity.GrossCertifiedAmount = dto.GrossCertifiedAmount;
        entity.RetentionHeldAmount = dto.RetentionHeldAmount;
        entity.RetentionReleasedAmount = dto.RetentionReleasedAmount;
        entity.OtherDeductionsAmount = dto.OtherDeductionsAmount;
        entity.NetCertifiedAmount = dto.NetCertifiedAmount;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? entity.Currency);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectPaymentCertificate>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectPaymentCertificateDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectPaymentCertificateAsync(Guid paymentCertificateId)
    {
        var entity = await GetProjectPaymentCertificateEntityAsync(paymentCertificateId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectPaymentCertificate>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectExtensionOfTimeDto>> GetProjectExtensionOfTimeRequestsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectExtensionOfTimeRequestsAsync((await GetProjectExtensionOfTimeEntitiesAsync(projectId)).ToList(), projectId);
    }

    public async Task<ProjectExtensionOfTimeDto> AddProjectExtensionOfTimeRequestAsync(Guid projectId, CreateProjectExtensionOfTimeDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(projectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);

        var entity = new ProjectExtensionOfTime
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            ProjectPackageId = package?.Id,
            ContractId = contract?.Id,
            ReferenceNumber = TrimOrNull(dto.ReferenceNumber),
            Title = dto.Title.Trim(),
            Reason = TrimOrNull(dto.Reason),
            Status = NormalizeProjectExtensionOfTimeStatus(dto.Status),
            RequestedDate = dto.RequestedDate ?? DateTime.UtcNow,
            DecisionDate = dto.DecisionDate,
            DaysRequested = dto.DaysRequested,
            DaysApproved = dto.DaysApproved,
            RevisedCompletionDate = dto.RevisedCompletionDate,
            RequestedByName = TrimOrNull(dto.RequestedByName),
            DecidedByName = TrimOrNull(dto.DecidedByName),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectExtensionOfTime>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectExtensionOfTimeDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectExtensionOfTimeDto> UpdateProjectExtensionOfTimeRequestAsync(Guid extensionOfTimeId, UpdateProjectExtensionOfTimeDto dto)
    {
        var entity = await GetProjectExtensionOfTimeEntityAsync(extensionOfTimeId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(entity.ProjectId, dto.ProjectPackageId);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);

        entity.ProjectPhaseId = phase?.Id;
        entity.ProjectPackageId = package?.Id;
        entity.ContractId = contract?.Id;
        entity.ReferenceNumber = TrimOrNull(dto.ReferenceNumber);
        entity.Title = dto.Title.Trim();
        entity.Reason = TrimOrNull(dto.Reason);
        entity.Status = NormalizeProjectExtensionOfTimeStatus(dto.Status);
        entity.RequestedDate = dto.RequestedDate ?? entity.RequestedDate;
        entity.DecisionDate = dto.DecisionDate;
        entity.DaysRequested = dto.DaysRequested;
        entity.DaysApproved = dto.DaysApproved;
        entity.RevisedCompletionDate = dto.RevisedCompletionDate;
        entity.RequestedByName = TrimOrNull(dto.RequestedByName);
        entity.DecidedByName = TrimOrNull(dto.DecidedByName);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectExtensionOfTime>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectExtensionOfTimeDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectExtensionOfTimeRequestAsync(Guid extensionOfTimeId)
    {
        var entity = await GetProjectExtensionOfTimeEntityAsync(extensionOfTimeId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectExtensionOfTime>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ProjectFinalAccountDto?> GetProjectFinalAccountAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var entity = await GetProjectFinalAccountEntityAsync(projectId);
        return entity == null ? null : await MapProjectFinalAccountAsync(entity);
    }

    public async Task<ProjectFinalAccountDto> UpsertProjectFinalAccountAsync(Guid projectId, UpsertProjectFinalAccountDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var contract = await ValidateProjectCommercialContractAsync(dto.ContractId);
        var entity = await GetProjectFinalAccountEntityAsync(projectId);
        var isNew = entity == null;

        entity ??= new ProjectFinalAccount
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        entity.ContractId = contract?.Id;
        entity.Status = NormalizeProjectFinalAccountStatus(dto.Status);
        entity.SettlementDate = dto.SettlementDate;
        entity.OriginalContractValue = dto.OriginalContractValue;
        entity.ApprovedVariationAmount = dto.ApprovedVariationAmount;
        entity.CertifiedToDate = dto.CertifiedToDate;
        entity.RetentionHeldAmount = dto.RetentionHeldAmount;
        entity.RetentionReleasedAmount = dto.RetentionReleasedAmount;
        entity.FinalAccountValue = dto.FinalAccountValue;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        if (isNew)
        {
            await _unitOfWork.Repository<ProjectFinalAccount>().AddAsync(entity);
        }
        else
        {
            await _unitOfWork.Repository<ProjectFinalAccount>().UpdateAsync(entity);
        }

        await _unitOfWork.SaveChangesAsync();
        return await MapProjectFinalAccountAsync(entity);
    }

    private async Task<ProjectVariationOrderDto> GetProjectVariationOrderDtoAsync(Guid projectId, Guid variationOrderId)
    {
        var entity = await GetProjectVariationOrderEntityAsync(variationOrderId);
        return (await MapProjectVariationOrdersAsync([entity], projectId)).Single();
    }

    private async Task<ProjectInterimValuationDto> GetProjectInterimValuationDtoAsync(Guid projectId, Guid interimValuationId)
    {
        var entity = await GetProjectInterimValuationEntityAsync(interimValuationId);
        return (await MapProjectInterimValuationsAsync([entity], projectId)).Single();
    }

    private async Task<ProjectPaymentCertificateDto> GetProjectPaymentCertificateDtoAsync(Guid projectId, Guid paymentCertificateId)
    {
        var entity = await GetProjectPaymentCertificateEntityAsync(paymentCertificateId);
        return (await MapProjectPaymentCertificatesAsync([entity], projectId)).Single();
    }

    private async Task<ProjectExtensionOfTimeDto> GetProjectExtensionOfTimeDtoAsync(Guid projectId, Guid extensionOfTimeId)
    {
        var entity = await GetProjectExtensionOfTimeEntityAsync(extensionOfTimeId);
        return (await MapProjectExtensionOfTimeRequestsAsync([entity], projectId)).Single();
    }

    private async Task<List<ProjectVariationOrder>> GetProjectVariationOrderEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectVariationOrder>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<List<ProjectInterimValuation>> GetProjectInterimValuationEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectInterimValuation>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<List<ProjectPaymentCertificate>> GetProjectPaymentCertificateEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectPaymentCertificate>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<List<ProjectExtensionOfTime>> GetProjectExtensionOfTimeEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectExtensionOfTime>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<ProjectVariationOrder> GetProjectVariationOrderEntityAsync(Guid variationOrderId)
        => await _unitOfWork.Repository<ProjectVariationOrder>().FirstOrDefaultAsync(x =>
               x.Id == variationOrderId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("Project variation order was not found.");

    private async Task<ProjectInterimValuation> GetProjectInterimValuationEntityAsync(Guid interimValuationId)
        => await _unitOfWork.Repository<ProjectInterimValuation>().FirstOrDefaultAsync(x =>
               x.Id == interimValuationId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("Project interim valuation was not found.");

    private async Task<ProjectPaymentCertificate> GetProjectPaymentCertificateEntityAsync(Guid paymentCertificateId)
        => await _unitOfWork.Repository<ProjectPaymentCertificate>().FirstOrDefaultAsync(x =>
               x.Id == paymentCertificateId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("Project payment certificate was not found.");

    private async Task<ProjectExtensionOfTime> GetProjectExtensionOfTimeEntityAsync(Guid extensionOfTimeId)
        => await _unitOfWork.Repository<ProjectExtensionOfTime>().FirstOrDefaultAsync(x =>
               x.Id == extensionOfTimeId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("Project extension of time request was not found.");

    private async Task<ProjectFinalAccount?> GetProjectFinalAccountEntityAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectFinalAccount>();
        if (repository == null)
        {
            return null;
        }

        return await repository.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId
            && x.TenantId == _currentUserProvider.TenantId);
    }

    private async Task<Contract?> ValidateProjectCommercialContractAsync(Guid? contractId)
    {
        if (!contractId.HasValue)
        {
            return null;
        }

        return await _unitOfWork.Repository<Contract>().FirstOrDefaultAsync(x =>
                   x.Id == contractId.Value
                   && x.TenantId == _currentUserProvider.TenantId)
               ?? throw new InvalidOperationException("Selected contract was not found.");
    }

    private async Task<ProjectInterimValuation?> ValidateProjectCommercialInterimValuationAsync(Guid projectId, Guid? interimValuationId)
    {
        if (!interimValuationId.HasValue)
        {
            return null;
        }

        var interimValuation = await GetProjectInterimValuationEntityAsync(interimValuationId.Value);
        if (interimValuation.ProjectId != projectId)
        {
            throw new InvalidOperationException("Selected interim valuation must belong to the current project.");
        }

        return interimValuation;
    }

    private async Task<Dictionary<Guid, Contract>> GetProjectCommercialContractLookupAsync(IEnumerable<Guid?> contractIds)
    {
        var ids = contractIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return (await _unitOfWork.Repository<Contract>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ids.Contains(x.Id)))
            .ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<Guid, ProjectInterimValuation>> GetProjectCommercialInterimValuationLookupAsync(IEnumerable<Guid?> interimValuationIds)
    {
        var ids = interimValuationIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return (await _unitOfWork.Repository<ProjectInterimValuation>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ids.Contains(x.Id)))
            .ToDictionary(x => x.Id);
    }

    private async Task<List<ProjectVariationOrderDto>> MapProjectVariationOrdersAsync(IReadOnlyCollection<ProjectVariationOrder> entities, Guid projectId)
    {
        var phaseLookup = (await GetProjectPhaseEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var packageLookup = (await GetProjectPackageEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var contractLookup = await GetProjectCommercialContractLookupAsync(entities.Select(x => x.ContractId));

        return entities
            .OrderByDescending(x => x.RequestedDate)
            .ThenBy(x => x.Title)
            .Select(entity =>
            {
                contractLookup.TryGetValue(entity.ContractId ?? Guid.Empty, out var contract);
                return new ProjectVariationOrderDto
                {
                    Id = entity.Id,
                    ProjectId = entity.ProjectId,
                    ProjectPhaseId = entity.ProjectPhaseId,
                    ProjectPhaseName = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
                    ProjectPackageId = entity.ProjectPackageId,
                    ProjectPackageName = entity.ProjectPackageId.HasValue && packageLookup.TryGetValue(entity.ProjectPackageId.Value, out var package) ? package.Name : null,
                    ContractId = entity.ContractId,
                    ContractNumber = contract?.ContractNumber,
                    ContractTitle = contract?.ContractTitle,
                    ReferenceNumber = entity.ReferenceNumber,
                    Title = entity.Title,
                    Description = entity.Description,
                    VariationType = entity.VariationType,
                    Status = entity.Status,
                    RequestedDate = entity.RequestedDate,
                    ApprovedDate = entity.ApprovedDate,
                    ImplementedDate = entity.ImplementedDate,
                    RequestedByName = entity.RequestedByName,
                    ApprovedByName = entity.ApprovedByName,
                    EstimatedAmount = entity.EstimatedAmount,
                    ApprovedAmount = entity.ApprovedAmount,
                    Currency = entity.Currency,
                    ScheduleImpactDays = entity.ScheduleImpactDays,
                    Notes = entity.Notes
                };
            })
            .ToList();
    }

    private async Task<List<ProjectInterimValuationDto>> MapProjectInterimValuationsAsync(IReadOnlyCollection<ProjectInterimValuation> entities, Guid projectId)
    {
        var phaseLookup = (await GetProjectPhaseEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var packageLookup = (await GetProjectPackageEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var contractLookup = await GetProjectCommercialContractLookupAsync(entities.Select(x => x.ContractId));

        return entities
            .OrderByDescending(x => x.ValuationDate)
            .ThenBy(x => x.Title)
            .Select(entity =>
            {
                contractLookup.TryGetValue(entity.ContractId ?? Guid.Empty, out var contract);
                return new ProjectInterimValuationDto
                {
                    Id = entity.Id,
                    ProjectId = entity.ProjectId,
                    ProjectPhaseId = entity.ProjectPhaseId,
                    ProjectPhaseName = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
                    ProjectPackageId = entity.ProjectPackageId,
                    ProjectPackageName = entity.ProjectPackageId.HasValue && packageLookup.TryGetValue(entity.ProjectPackageId.Value, out var package) ? package.Name : null,
                    ContractId = entity.ContractId,
                    ContractNumber = contract?.ContractNumber,
                    ContractTitle = contract?.ContractTitle,
                    ValuationNumber = entity.ValuationNumber,
                    Title = entity.Title,
                    Status = entity.Status,
                    ValuationDate = entity.ValuationDate,
                    GrossWorkValue = entity.GrossWorkValue,
                    MaterialsOnSiteValue = entity.MaterialsOnSiteValue,
                    VariationValue = entity.VariationValue,
                    RetentionPercentage = entity.RetentionPercentage,
                    RetentionAmount = entity.RetentionAmount,
                    PreviousCertifiedAmount = entity.PreviousCertifiedAmount,
                    NetValuationAmount = entity.NetValuationAmount,
                    Currency = entity.Currency,
                    Notes = entity.Notes
                };
            })
            .ToList();
    }

    private async Task<List<ProjectPaymentCertificateDto>> MapProjectPaymentCertificatesAsync(IReadOnlyCollection<ProjectPaymentCertificate> entities, Guid projectId)
    {
        var phaseLookup = (await GetProjectPhaseEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var packageLookup = (await GetProjectPackageEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var contractLookup = await GetProjectCommercialContractLookupAsync(entities.Select(x => x.ContractId));
        var interimValuationLookup = await GetProjectCommercialInterimValuationLookupAsync(entities.Select(x => x.ProjectInterimValuationId));

        return entities
            .OrderByDescending(x => x.IssueDate)
            .ThenBy(x => x.Title)
            .Select(entity =>
            {
                contractLookup.TryGetValue(entity.ContractId ?? Guid.Empty, out var contract);
                interimValuationLookup.TryGetValue(entity.ProjectInterimValuationId ?? Guid.Empty, out var interimValuation);
                return new ProjectPaymentCertificateDto
                {
                    Id = entity.Id,
                    ProjectId = entity.ProjectId,
                    ProjectPhaseId = entity.ProjectPhaseId,
                    ProjectPhaseName = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
                    ProjectPackageId = entity.ProjectPackageId,
                    ProjectPackageName = entity.ProjectPackageId.HasValue && packageLookup.TryGetValue(entity.ProjectPackageId.Value, out var package) ? package.Name : null,
                    ContractId = entity.ContractId,
                    ContractNumber = contract?.ContractNumber,
                    ContractTitle = contract?.ContractTitle,
                    ProjectInterimValuationId = entity.ProjectInterimValuationId,
                    InterimValuationNumber = interimValuation?.ValuationNumber,
                    InterimValuationTitle = interimValuation?.Title,
                    CertificateNumber = entity.CertificateNumber,
                    Title = entity.Title,
                    Status = entity.Status,
                    IssueDate = entity.IssueDate,
                    PaymentDueDate = entity.PaymentDueDate,
                    GrossCertifiedAmount = entity.GrossCertifiedAmount,
                    RetentionHeldAmount = entity.RetentionHeldAmount,
                    RetentionReleasedAmount = entity.RetentionReleasedAmount,
                    OtherDeductionsAmount = entity.OtherDeductionsAmount,
                    NetCertifiedAmount = entity.NetCertifiedAmount,
                    Currency = entity.Currency,
                    Notes = entity.Notes
                };
            })
            .ToList();
    }

    private async Task<List<ProjectExtensionOfTimeDto>> MapProjectExtensionOfTimeRequestsAsync(IReadOnlyCollection<ProjectExtensionOfTime> entities, Guid projectId)
    {
        var phaseLookup = (await GetProjectPhaseEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var packageLookup = (await GetProjectPackageEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var contractLookup = await GetProjectCommercialContractLookupAsync(entities.Select(x => x.ContractId));

        return entities
            .OrderByDescending(x => x.RequestedDate)
            .ThenBy(x => x.Title)
            .Select(entity =>
            {
                contractLookup.TryGetValue(entity.ContractId ?? Guid.Empty, out var contract);
                return new ProjectExtensionOfTimeDto
                {
                    Id = entity.Id,
                    ProjectId = entity.ProjectId,
                    ProjectPhaseId = entity.ProjectPhaseId,
                    ProjectPhaseName = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
                    ProjectPackageId = entity.ProjectPackageId,
                    ProjectPackageName = entity.ProjectPackageId.HasValue && packageLookup.TryGetValue(entity.ProjectPackageId.Value, out var package) ? package.Name : null,
                    ContractId = entity.ContractId,
                    ContractNumber = contract?.ContractNumber,
                    ContractTitle = contract?.ContractTitle,
                    ReferenceNumber = entity.ReferenceNumber,
                    Title = entity.Title,
                    Reason = entity.Reason,
                    Status = entity.Status,
                    RequestedDate = entity.RequestedDate,
                    DecisionDate = entity.DecisionDate,
                    DaysRequested = entity.DaysRequested,
                    DaysApproved = entity.DaysApproved,
                    RevisedCompletionDate = entity.RevisedCompletionDate,
                    RequestedByName = entity.RequestedByName,
                    DecidedByName = entity.DecidedByName,
                    Notes = entity.Notes
                };
            })
            .ToList();
    }

    private async Task<ProjectFinalAccountDto> MapProjectFinalAccountAsync(ProjectFinalAccount entity)
    {
        var contract = await ValidateProjectCommercialContractAsync(entity.ContractId);
        return new ProjectFinalAccountDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            ContractId = entity.ContractId,
            ContractNumber = contract?.ContractNumber,
            ContractTitle = contract?.ContractTitle,
            Status = entity.Status,
            SettlementDate = entity.SettlementDate,
            OriginalContractValue = entity.OriginalContractValue,
            ApprovedVariationAmount = entity.ApprovedVariationAmount,
            CertifiedToDate = entity.CertifiedToDate,
            RetentionHeldAmount = entity.RetentionHeldAmount,
            RetentionReleasedAmount = entity.RetentionReleasedAmount,
            FinalAccountValue = entity.FinalAccountValue,
            Currency = entity.Currency,
            Notes = entity.Notes
        };
    }

    private static string NormalizeProjectVariationOrderStatus(string? value)
        => value?.Trim() switch
        {
            ProjectVariationOrderStatuses.Submitted => ProjectVariationOrderStatuses.Submitted,
            ProjectVariationOrderStatuses.UnderReview => ProjectVariationOrderStatuses.UnderReview,
            ProjectVariationOrderStatuses.Approved => ProjectVariationOrderStatuses.Approved,
            ProjectVariationOrderStatuses.Rejected => ProjectVariationOrderStatuses.Rejected,
            ProjectVariationOrderStatuses.Implemented => ProjectVariationOrderStatuses.Implemented,
            ProjectVariationOrderStatuses.Closed => ProjectVariationOrderStatuses.Closed,
            _ => ProjectVariationOrderStatuses.Draft
        };

    private static string NormalizeProjectVariationOrderType(string? value)
        => value?.Trim() switch
        {
            ProjectVariationOrderTypes.ScopeChange => ProjectVariationOrderTypes.ScopeChange,
            ProjectVariationOrderTypes.QuantityAdjustment => ProjectVariationOrderTypes.QuantityAdjustment,
            ProjectVariationOrderTypes.ProvisionalSum => ProjectVariationOrderTypes.ProvisionalSum,
            ProjectVariationOrderTypes.RateChange => ProjectVariationOrderTypes.RateChange,
            ProjectVariationOrderTypes.Omission => ProjectVariationOrderTypes.Omission,
            _ => ProjectVariationOrderTypes.Other
        };

    private static string NormalizeProjectInterimValuationStatus(string? value)
        => value?.Trim() switch
        {
            ProjectInterimValuationStatuses.Submitted => ProjectInterimValuationStatuses.Submitted,
            ProjectInterimValuationStatuses.UnderReview => ProjectInterimValuationStatuses.UnderReview,
            ProjectInterimValuationStatuses.Certified => ProjectInterimValuationStatuses.Certified,
            ProjectInterimValuationStatuses.Paid => ProjectInterimValuationStatuses.Paid,
            ProjectInterimValuationStatuses.Rejected => ProjectInterimValuationStatuses.Rejected,
            _ => ProjectInterimValuationStatuses.Draft
        };

    private static string NormalizeProjectPaymentCertificateStatus(string? value)
        => value?.Trim() switch
        {
            ProjectPaymentCertificateStatuses.Issued => ProjectPaymentCertificateStatuses.Issued,
            ProjectPaymentCertificateStatuses.Approved => ProjectPaymentCertificateStatuses.Approved,
            ProjectPaymentCertificateStatuses.Paid => ProjectPaymentCertificateStatuses.Paid,
            ProjectPaymentCertificateStatuses.Cancelled => ProjectPaymentCertificateStatuses.Cancelled,
            _ => ProjectPaymentCertificateStatuses.Draft
        };

    private static string NormalizeProjectExtensionOfTimeStatus(string? value)
        => value?.Trim() switch
        {
            ProjectExtensionOfTimeStatuses.Submitted => ProjectExtensionOfTimeStatuses.Submitted,
            ProjectExtensionOfTimeStatuses.UnderReview => ProjectExtensionOfTimeStatuses.UnderReview,
            ProjectExtensionOfTimeStatuses.Approved => ProjectExtensionOfTimeStatuses.Approved,
            ProjectExtensionOfTimeStatuses.Rejected => ProjectExtensionOfTimeStatuses.Rejected,
            ProjectExtensionOfTimeStatuses.Implemented => ProjectExtensionOfTimeStatuses.Implemented,
            ProjectExtensionOfTimeStatuses.Closed => ProjectExtensionOfTimeStatuses.Closed,
            _ => ProjectExtensionOfTimeStatuses.Draft
        };

    private static string NormalizeProjectFinalAccountStatus(string? value)
        => value?.Trim() switch
        {
            ProjectFinalAccountStatuses.UnderReview => ProjectFinalAccountStatuses.UnderReview,
            ProjectFinalAccountStatuses.Agreed => ProjectFinalAccountStatuses.Agreed,
            ProjectFinalAccountStatuses.Approved => ProjectFinalAccountStatuses.Approved,
            ProjectFinalAccountStatuses.Closed => ProjectFinalAccountStatuses.Closed,
            _ => ProjectFinalAccountStatuses.Draft
        };
}
