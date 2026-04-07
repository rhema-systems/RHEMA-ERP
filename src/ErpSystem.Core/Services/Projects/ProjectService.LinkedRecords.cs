using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectLinkOptionsDto> GetProjectLinkOptionsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await BuildProjectLinkOptionsAsync(projectId);
    }

    private async Task<ProjectLinkOptionsDto> BuildProjectLinkOptionsAsync(Guid projectId)
    {
        var units = (await GetProjectUnitEntitiesAsync(projectId)).ToList();
        var assetLinks = (await _unitOfWork.Repository<ProjectAssetLink>().FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .ToList();

        var customerIds = units
            .Where(x => x.CustomerBusinessPartnerId.HasValue)
            .Select(x => x.CustomerBusinessPartnerId!.Value)
            .Distinct()
            .ToList();
        var propertyReferences = units
            .SelectMany(x => new[] { TrimOrNull(x.Code), string.IsNullOrWhiteSpace(x.Code) ? TrimOrNull(x.Name) : null })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var salesAgreements = (await _unitOfWork.Repository<SalesAgreement>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ((customerIds.Count > 0 && customerIds.Contains(x.BusinessPartnerId))
                    || (propertyReferences.Count > 0 && x.PropertyReference != null && propertyReferences.Contains(x.PropertyReference)))))
            .OrderBy(x => x.DocumentNumber)
            .ThenBy(x => x.AgreementTitle)
            .ToList();

        var salesOrders = (await _unitOfWork.Repository<SalesOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ((customerIds.Count > 0 && customerIds.Contains(x.BusinessPartnerId))
                    || (propertyReferences.Count > 0 && x.PropertyReference != null && propertyReferences.Contains(x.PropertyReference)))))
            .OrderBy(x => x.DocumentNumber)
            .ThenBy(x => x.CustomerName)
            .ToList();

        var maintenanceAssetIds = assetLinks
            .Where(x => x.MaintenanceAssetId.HasValue)
            .Select(x => x.MaintenanceAssetId!.Value)
            .Distinct()
            .ToList();
        var linkedJobCardIds = assetLinks
            .Where(x => x.JobCardId.HasValue)
            .Select(x => x.JobCardId!.Value)
            .Distinct()
            .ToList();

        var jobCards = (await _unitOfWork.Repository<JobCard>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ((maintenanceAssetIds.Count > 0 && maintenanceAssetIds.Contains(x.AssetId))
                    || linkedJobCardIds.Contains(x.Id))))
            .OrderBy(x => x.JobCardNumber)
            .ThenBy(x => x.Title)
            .ToList();
        var jobCardIds = jobCards.Select(x => x.Id).Distinct().ToList();

        var workOrders = (await _unitOfWork.Repository<WorkOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ((maintenanceAssetIds.Count > 0 && maintenanceAssetIds.Contains(x.AssetId))
                    || (x.JobCardId.HasValue && jobCardIds.Contains(x.JobCardId.Value)))))
            .OrderBy(x => x.WorkOrderNumber)
            .ThenBy(x => x.Title)
            .ToList();

        var assetLookup = await GetMaintenanceAssetLookupAsync(
            maintenanceAssetIds
                .Concat(jobCards.Select(x => x.AssetId))
                .Concat(workOrders.Select(x => x.AssetId))
                .Distinct());
        var jobCardLookup = jobCards.ToDictionary(x => x.Id);

        return new ProjectLinkOptionsDto
        {
            SalesAgreements = salesAgreements.Select(x => new ProjectSalesAgreementLinkOptionDto
            {
                Id = x.Id,
                BusinessPartnerId = x.BusinessPartnerId,
                DocumentNumber = x.DocumentNumber,
                AgreementTitle = x.AgreementTitle,
                CustomerName = x.CustomerName,
                PropertyReference = x.PropertyReference,
                AgreementType = x.AgreementType.ToString(),
                AgreementStatus = x.AgreementStatus.ToString()
            }).ToList(),
            SalesOrders = salesOrders.Select(x => new ProjectSalesOrderLinkOptionDto
            {
                Id = x.Id,
                BusinessPartnerId = x.BusinessPartnerId,
                OrderNumber = x.DocumentNumber,
                CustomerName = x.CustomerName,
                PropertyReference = x.PropertyReference,
                Status = x.OrderStatus.ToString()
            }).ToList(),
            JobCards = jobCards.Select(x => new ProjectJobCardLinkOptionDto
            {
                Id = x.Id,
                AssetId = x.AssetId,
                JobCardNumber = x.JobCardNumber,
                Title = x.Title,
                Status = x.JobCardStatus,
                AssetName = assetLookup.TryGetValue(x.AssetId, out var asset) ? asset.Name : null
            }).ToList(),
            WorkOrders = workOrders.Select(x => new ProjectWorkOrderLinkOptionDto
            {
                Id = x.Id,
                AssetId = x.AssetId,
                WorkOrderNumber = x.WorkOrderNumber,
                Title = x.Title,
                Status = x.Status,
                AssetName = assetLookup.TryGetValue(x.AssetId, out var asset) ? asset.Name : null,
                JobCardId = x.JobCardId,
                JobCardNumber = x.JobCardId.HasValue && jobCardLookup.TryGetValue(x.JobCardId.Value, out var jobCard) ? jobCard.JobCardNumber : null
            }).ToList()
        };
    }

    private async Task<Dictionary<Guid, SalesAgreement>> GetSalesAgreementLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, SalesAgreement>();
        }

        return (await _unitOfWork.Repository<SalesAgreement>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctIds.Contains(x.Id)))
            .ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<Guid, SalesOrder>> GetSalesOrderLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, SalesOrder>();
        }

        return (await _unitOfWork.Repository<SalesOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctIds.Contains(x.Id)))
            .ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<Guid, JobCard>> GetJobCardLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, JobCard>();
        }

        return (await _unitOfWork.Repository<JobCard>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctIds.Contains(x.Id)))
            .ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<Guid, WorkOrder>> GetWorkOrderLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, WorkOrder>();
        }

        return (await _unitOfWork.Repository<WorkOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctIds.Contains(x.Id)))
            .ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<Guid, MaintenanceAsset>> GetMaintenanceAssetLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, MaintenanceAsset>();
        }

        return (await _unitOfWork.Repository<MaintenanceAsset>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && distinctIds.Contains(x.Id)))
            .ToDictionary(x => x.Id);
    }

    private async Task<SalesAgreement?> EnsureTenantSalesAgreementExistsAsync(Guid? salesAgreementId)
    {
        if (!salesAgreementId.HasValue)
        {
            return null;
        }

        return await _unitOfWork.Repository<SalesAgreement>().FirstOrDefaultAsync(x =>
                   x.Id == salesAgreementId.Value
                   && x.TenantId == _currentUserProvider.TenantId)
               ?? throw new InvalidOperationException("The selected sales agreement could not be found.");
    }

    private async Task<SalesOrder?> EnsureTenantSalesOrderExistsAsync(Guid? salesOrderId)
    {
        if (!salesOrderId.HasValue)
        {
            return null;
        }

        return await _unitOfWork.Repository<SalesOrder>().FirstOrDefaultAsync(x =>
                   x.Id == salesOrderId.Value
                   && x.TenantId == _currentUserProvider.TenantId)
               ?? throw new InvalidOperationException("The selected sales order could not be found.");
    }

    private async Task<JobCard?> EnsureTenantJobCardExistsAsync(Guid? jobCardId)
    {
        if (!jobCardId.HasValue)
        {
            return null;
        }

        return await _unitOfWork.Repository<JobCard>().FirstOrDefaultAsync(x =>
                   x.Id == jobCardId.Value
                   && x.TenantId == _currentUserProvider.TenantId)
               ?? throw new InvalidOperationException("The selected job card could not be found.");
    }

    private async Task<WorkOrder?> EnsureTenantWorkOrderExistsAsync(Guid? workOrderId)
    {
        if (!workOrderId.HasValue)
        {
            return null;
        }

        return await _unitOfWork.Repository<WorkOrder>().FirstOrDefaultAsync(x =>
                   x.Id == workOrderId.Value
                   && x.TenantId == _currentUserProvider.TenantId)
               ?? throw new InvalidOperationException("The selected work order could not be found.");
    }
}
