using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

public class JobCardRepository : IJobCardRepository
{
    private const string WorkOrderBillingTypeField = "workOrderBillingType";

    private readonly ApplicationDbContext _context;

    public JobCardRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private static string GetWorkOrderBillingType(string? customFieldValues)
    {
        if (string.IsNullOrWhiteSpace(customFieldValues))
        {
            return "Repairs";
        }

        try
        {
            using var document = JsonDocument.Parse(customFieldValues);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "Repairs";
            }

            if (document.RootElement.TryGetProperty(WorkOrderBillingTypeField, out var billingType) &&
                billingType.ValueKind == JsonValueKind.String &&
                string.Equals(billingType.GetString(), "Maintenance", StringComparison.OrdinalIgnoreCase))
            {
                return "Maintenance";
            }

            if (document.RootElement.TryGetProperty("billingType", out var legacyBillingType) &&
                legacyBillingType.ValueKind == JsonValueKind.String &&
                string.Equals(legacyBillingType.GetString(), "Maintenance", StringComparison.OrdinalIgnoreCase))
            {
                return "Maintenance";
            }
        }
        catch (JsonException)
        {
            return "Repairs";
        }

        return "Repairs";
    }

    private static Dictionary<string, object>? DeserializeCustomFields(string? customFieldValues)
    {
        if (string.IsNullOrWhiteSpace(customFieldValues))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(customFieldValues);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<ErpSystem.Core.DTOs.Maintenance.PagedResult<JobCardListDto>> GetPagedAsync(JobCardFilterDto filter)
    {
        var query = _context.JobCards.AsQueryable();

        // Apply filters if provided
        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(j => j.JobCardNumber.Contains(filter.SearchTerm) ||
                                        j.Title.Contains(filter.SearchTerm) ||
                                        j.Description.Contains(filter.SearchTerm));
            }

            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(j => j.JobCardStatus == filter.Status);
            }

            if (filter.AssetId.HasValue)
            {
                query = query.Where(j => j.AssetId == filter.AssetId.Value
                    || j.FixedAssetId == filter.AssetId.Value
                    || j.EstateManagedAssetId == filter.AssetId.Value);
            }

            if (filter.RequestedById.HasValue)
            {
                query = query.Where(j => j.RequestedById == filter.RequestedById.Value);
            }

            if (!string.IsNullOrEmpty(filter.ApprovalStatus))
            {
                query = query.Where(j => j.ApprovalStatus == filter.ApprovalStatus);
            }

            if (filter.MaintenanceTypeId.HasValue)
            {
                query = query.Where(j => j.MaintenanceTypeId == filter.MaintenanceTypeId.Value);
            }

            if (filter.PriorityLevelId.HasValue)
            {
                query = query.Where(j => j.PriorityLevelId == filter.PriorityLevelId.Value);
            }

            if (filter.AssignedTechnicianId.HasValue)
            {
                query = query.Where(j => j.AssignedTechnicianId == filter.AssignedTechnicianId.Value);
            }

            if (filter.RequestedFrom.HasValue)
            {
                query = query.Where(j => j.RequestedDate >= filter.RequestedFrom.Value);
            }

            if (filter.RequestedTo.HasValue)
            {
                query = query.Where(j => j.RequestedDate <= filter.RequestedTo.Value);
            }

            if (filter.RequiredFrom.HasValue)
            {
                query = query.Where(j => j.RequiredCompletionDate >= filter.RequiredFrom.Value);
            }

            if (filter.RequiredTo.HasValue)
            {
                query = query.Where(j => j.RequiredCompletionDate <= filter.RequiredTo.Value);
            }

            if (filter.HasWorkOrder.HasValue)
            {
                if (filter.HasWorkOrder.Value)
                {
                    query = query.Where(j => j.GeneratedWorkOrderId != null);
                }
                else
                {
                    query = query.Where(j => j.GeneratedWorkOrderId == null);
                }
            }
        }

        var totalCount = await query.CountAsync();

        var page = filter?.Page ?? 1;
        var pageSize = filter?.PageSize ?? 25;
        var skip = (page - 1) * pageSize;

        var jobCards = await query
            .Include(j => j.Asset)
            .Include(j => j.FixedAsset)
                .ThenInclude(asset => asset!.Category)
            .Include(j => j.EstateManagedAsset)
            .Include(j => j.MaintenanceType)
            .Include(j => j.PriorityLevel)
            .Include(j => j.RequestedBy)
            .Include(j => j.CustomerBusinessPartner)
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(j => new JobCardListDto
            {
                Id = j.Id,
                JobCardNumber = j.JobCardNumber,
                Title = j.Title,
                Description = j.Description,
                ProblemDescription = j.ProblemDescription,
                AssetId = j.AssetId,
                AssetSource = j.AssetSource,
                SourceAssetId = j.AssetSource == JobCardAssetSource.FixedAsset && j.FixedAssetId.HasValue
                    ? j.FixedAssetId.Value
                    : j.AssetSource == JobCardAssetSource.EstateManagedAsset && j.EstateManagedAssetId.HasValue
                        ? j.EstateManagedAssetId.Value
                        : j.AssetId,
                AssetName = j.FixedAsset != null
                    ? j.FixedAsset.Name
                    : j.EstateManagedAsset != null
                        ? j.EstateManagedAsset.Name
                        : j.Asset != null ? j.Asset.Name : "Unknown",
                AssetCode = j.FixedAsset != null
                    ? j.FixedAsset.AssetCode
                    : j.EstateManagedAsset != null
                        ? j.EstateManagedAsset.AssetCode
                        : j.Asset != null ? j.Asset.AssetNumber : "N/A",
                MaintenanceTypeId = j.MaintenanceTypeId,
                MaintenanceType = j.MaintenanceType != null ? j.MaintenanceType.Name : "Unknown",
                PriorityLevelId = j.PriorityLevelId,
                Priority = j.PriorityLevel != null ? j.PriorityLevel.Name : "Medium",
                PriorityColor = j.PriorityLevel != null ? j.PriorityLevel.Color : "#FFA500",
                CustomerBusinessPartnerId = j.CustomerBusinessPartnerId,
                CustomerBusinessPartnerName = j.CustomerBusinessPartner != null ? j.CustomerBusinessPartner.PartnerName : null,
                WorkOrderBillingType = j.CustomFieldValues != null &&
                    (j.CustomFieldValues.Contains("\"workOrderBillingType\":\"Maintenance\"") ||
                     j.CustomFieldValues.Contains("\"billingType\":\"Maintenance\""))
                    ? "Maintenance"
                    : "Repairs",
                JobCardStatus = j.JobCardStatus,
                ApprovalStatus = j.ApprovalStatus,
                RequestedBy = j.RequestedBy != null ? j.RequestedBy.FullName : "Unknown",
                RequestedDate = j.RequestedDate,
                RequiredCompletionDate = j.RequiredCompletionDate,
                EstimatedHours = j.EstimatedHours,
                EstimatedCost = j.EstimatedCost,
                RequiresShutdown = j.RequiresShutdown,
                RequiresSafetyPermit = j.RequiresSafetyPermit,
                GeneratedWorkOrderId = j.GeneratedWorkOrderId,
                WorkOrderGeneratedAt = j.WorkOrderGeneratedAt,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Maintenance.PagedResult<JobCardListDto>
        {
            Items = jobCards,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<JobCardDto?> GetByIdWithDetailsAsync(Guid id)
    {
        var jobCard = await _context.JobCards.FindAsync(id);
        if (jobCard == null)
        {
            return null;
        }

        // Manually load related entities
        var asset = await _context.MaintenanceAssets
            .Include(a => a.AssetCategory)
            .FirstOrDefaultAsync(a => a.Id == jobCard.AssetId);

        var fixedAsset = jobCard.FixedAssetId.HasValue
            ? await _context.FixedAssets
                .Include(item => item.Category)
                .FirstOrDefaultAsync(item => item.Id == jobCard.FixedAssetId.Value)
            : null;
        var estateAsset = jobCard.EstateManagedAssetId.HasValue
            ? await _context.EstateManagedAssets
                .FirstOrDefaultAsync(item => item.Id == jobCard.EstateManagedAssetId.Value)
            : null;

        var maintenanceType = await _context.MaintenanceTypes
            .FirstOrDefaultAsync(m => m.Id == jobCard.MaintenanceTypeId);

        var priorityLevel = await _context.PriorityLevels
            .FirstOrDefaultAsync(p => p.Id == jobCard.PriorityLevelId);

        var customerBusinessPartner = jobCard.CustomerBusinessPartnerId.HasValue
            ? await _context.BusinessPartners.FirstOrDefaultAsync(bp => bp.Id == jobCard.CustomerBusinessPartnerId.Value)
            : null;

        var requestedBy = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == jobCard.RequestedById);

        var approvedBy = jobCard.ApprovedById.HasValue
            ? await _context.Employees.FirstOrDefaultAsync(e => e.Id == jobCard.ApprovedById.Value)
            : null;

        var preferredTechnician = jobCard.PreferredTechnicianId.HasValue
            ? await _context.Employees.FirstOrDefaultAsync(e => e.Id == jobCard.PreferredTechnicianId.Value)
            : null;

        var assignedTechnician = jobCard.AssignedTechnicianId.HasValue
            ? await _context.Employees.FirstOrDefaultAsync(e => e.Id == jobCard.AssignedTechnicianId.Value)
            : null;

        var qualityCheckedBy = jobCard.QualityCheckedById.HasValue
            ? await _context.Employees.FirstOrDefaultAsync(e => e.Id == jobCard.QualityCheckedById.Value)
            : null;

        var documents = await _context.JobCardDocuments
            .Where(d => d.JobCardId == id)
            .Include(d => d.UploadedBy)
            .ToListAsync();

        var comments = await _context.JobCardComments
            .Where(c => c.JobCardId == id)
            .Include(c => c.CommentBy)
            .OrderBy(c => c.CommentDate)
            .ToListAsync();

        var approvalSteps = await _context.JobCardApprovalSteps
            .Where(a => a.JobCardId == id)
            .Include(a => a.Approver)
            .OrderBy(a => a.StepOrder)
            .ToListAsync();

        // Load work order if exists
        var workOrder = jobCard.GeneratedWorkOrderId.HasValue
            ? await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == jobCard.GeneratedWorkOrderId.Value)
            : null;

        return new JobCardDto
        {
            Id = jobCard.Id,
            JobCardNumber = jobCard.JobCardNumber,
            Title = jobCard.Title,
            Description = jobCard.Description,
            ProblemDescription = jobCard.ProblemDescription,

            // Asset Information
            AssetId = jobCard.AssetId,
            AssetSource = jobCard.AssetSource,
            SourceAssetId = jobCard.FixedAssetId
                ?? jobCard.EstateManagedAssetId
                ?? jobCard.AssetId,
            AssetName = fixedAsset?.Name ?? estateAsset?.Name ?? asset?.Name ?? "Unknown",
            AssetCode = fixedAsset?.AssetCode ?? estateAsset?.AssetCode ?? asset?.AssetNumber ?? "N/A",
            AssetType = fixedAsset?.Category?.Name
                ?? estateAsset?.UnitType
                ?? estateAsset?.AssetType.ToString()
                ?? asset?.AssetCategory?.Name
                ?? asset?.AssetCategory?.AssetType
                ?? "N/A",
            AssetLocation = fixedAsset?.Location ?? estateAsset?.Location ?? asset?.Location ?? "N/A",

            // Maintenance Details
            MaintenanceTypeId = jobCard.MaintenanceTypeId,
            MaintenanceType = maintenanceType?.Name ?? "Unknown",
            MaintenanceCategory = maintenanceType?.Category ?? "N/A",
            PriorityLevelId = jobCard.PriorityLevelId,
            Priority = priorityLevel?.Name ?? "Medium",
            PriorityColor = priorityLevel?.Color ?? "#FFA500",
            PriorityLevel = priorityLevel?.Level ?? 2,

            MaintenanceLocation = jobCard.MaintenanceLocation ?? "Internal",
            CustomerBusinessPartnerId = jobCard.CustomerBusinessPartnerId,
            CustomerBusinessPartnerName = customerBusinessPartner?.PartnerName,
            WorkOrderBillingType = GetWorkOrderBillingType(jobCard.CustomFieldValues),

            // Request Information
            RequestedById = jobCard.RequestedById,
            RequestedBy = requestedBy?.FullName ?? "Unknown",
            RequestedDate = jobCard.RequestedDate,
            RequiredCompletionDate = jobCard.RequiredCompletionDate,

            // Estimates
            EstimatedHours = jobCard.EstimatedHours,
            EstimatedCost = jobCard.EstimatedCost,

            // Assignment
            PreferredTechnicianId = jobCard.PreferredTechnicianId,
            PreferredTechnician = preferredTechnician?.FullName,
            PreferredTeamId = jobCard.PreferredTeamId,
            PreferredTeam = null, // Would need to load TechnicianTeam
            ContractorId = jobCard.ContractorId,

            // Requirements
            RequiresSpecialTools = jobCard.RequiresSpecialTools,
            RequiresShutdown = jobCard.RequiresShutdown,
            RequiresSafetyPermit = jobCard.RequiresSafetyPermit,
            SpecialInstructions = jobCard.SpecialInstructions,
            SafetyRequirements = jobCard.SafetyRequirements,

            // Status Information
            JobCardStatus = jobCard.JobCardStatus,
            ApprovalStatus = jobCard.ApprovalStatus,
            SubmittedAt = jobCard.SubmittedDate,
            ApprovedAt = jobCard.ApprovedDate,
            ApprovedById = jobCard.ApprovedById,
            ApprovedBy = approvedBy?.FullName,

            // Planning
            PlannedStartDate = jobCard.PlannedStartDate,
            PlannedEndDate = jobCard.PlannedEndDate,
            AssignedTechnicianId = jobCard.AssignedTechnicianId,
            AssignedTechnician = assignedTechnician?.FullName,
            AssignedTeamId = jobCard.AssignedTeamId,
            AssignedTeam = null, // Would need to load TechnicianTeam

            // Work Order Generation
            GeneratedWorkOrderId = jobCard.GeneratedWorkOrderId,
            GeneratedWorkOrderNumber = workOrder?.WorkOrderNumber,
            WorkOrderGeneratedAt = jobCard.WorkOrderGeneratedAt,

            // Completion
            CompletedDate = jobCard.CompletedDate,
            CompletionNotes = jobCard.CompletionNotes,
            WorkCompletedSummary = jobCard.WorkCompletedSummary,

            // Quality Control
            QualityCheckPassed = jobCard.QualityCheckPassed,
            QualityCheckedBy = qualityCheckedBy?.FullName,
            QualityCheckDate = jobCard.QualityCheckDate,

            // Certificate
            CertificateGenerated = jobCard.CertificateGenerated,
            CertificateGeneratedDate = jobCard.CertificateGeneratedDate,

            // Documents, Comments, Approval Steps
            Documents = documents.Select(d => new JobCardDocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                FilePath = d.FilePath,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
                DocumentType = d.DocumentType,
                UploadedAt = d.UploadedDate,
                UploadedBy = d.UploadedBy?.FullName ?? "Unknown"
            }).ToList(),

            Comments = comments.Select(c => new JobCardCommentDto
            {
                Id = c.Id,
                Comment = c.Comment,
                CommentType = c.CommentType,
                IsInternal = c.IsInternal,
                CommentDate = c.CommentDate,
                CommentBy = c.CommentBy?.FullName ?? "Unknown"
            }).ToList(),

            ApprovalSteps = approvalSteps.Select(a => new JobCardApprovalStepDto
            {
                Id = a.Id,
                StepOrder = a.StepOrder,
                StepName = a.StepName,
                ApproverId = a.ApproverId,
                ApproverName = a.Approver?.FullName ?? "Unknown",
                Status = a.Status,
                ActionDate = a.ActionDate,
                Comments = a.Comments,
                IsRequired = a.IsRequired
            }).ToList(),

            CustomFieldValues = DeserializeCustomFields(jobCard.CustomFieldValues),

            // Metadata
            CreatedAt = jobCard.CreatedAt,
            UpdatedAt = jobCard.UpdatedAt
        };
    }

    public async Task<JobCard?> GetByIdAsync(Guid id)
    {
        // Use FindAsync which tracks the entity by default
        // This ensures EF will detect all property changes including ProblemDescription
        return await _context.JobCards.FindAsync(id);
    }

    public async Task<JobCardDto?> GetByNumberAsync(string jobCardNumber)
    {
        var jobCard = await _context.JobCards.FirstOrDefaultAsync(j => j.JobCardNumber == jobCardNumber);
        return jobCard != null ? await GetByIdWithDetailsAsync(jobCard.Id) : null;
    }

    public async Task AddAsync(JobCard jobCard)
    {
        await _context.JobCards.AddAsync(jobCard);
        // Note: JobCardService handles SaveChanges via Unit of Work
    }

    public Task UpdateAsync(JobCard jobCard)
    {
        // Explicitly mark entity as modified and ensure all properties are tracked
        var entry = _context.Entry(jobCard);

        // If entity is not being tracked, attach and mark as modified
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        {
            _context.JobCards.Update(jobCard);
        }
        else
        {
            // Entity is already tracked, explicitly mark ProblemDescription as modified
            entry.Property(j => j.ProblemDescription).IsModified = true;
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        }

        // Note: JobCardService handles SaveChanges via Unit of Work
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var jobCard = await GetByIdAsync(id);
        if (jobCard != null)
        {
            _context.JobCards.Remove(jobCard);
            // Note: JobCardService handles SaveChanges via Unit of Work
        }
    }

    public Task<ErpSystem.Core.DTOs.Maintenance.PagedResult<JobCardListDto>> GetPendingApprovalsAsync(JobCardFilterDto filter, Guid userId)
    {
        return Task.FromResult(new ErpSystem.Core.DTOs.Maintenance.PagedResult<JobCardListDto> { Items = new List<JobCardListDto>(), TotalCount = 0, Page = 1, PageSize = 25 });
    }

    public Task<List<JobCardListDto>> GetApprovedJobCardsAsync() => Task.FromResult(new List<JobCardListDto>());
    public Task<List<JobCardListDto>> GetByAssetAsync(Guid assetId) => Task.FromResult(new List<JobCardListDto>());
    public Task<List<JobCardListDto>> GetByRequesterAsync(Guid requesterId) => Task.FromResult(new List<JobCardListDto>());
    public Task<List<JobCardListDto>> GetByTechnicianAsync(Guid technicianId) => Task.FromResult(new List<JobCardListDto>());
    public Task<JobCardDashboardStatsDto> GetDashboardStatsAsync() => Task.FromResult(new JobCardDashboardStatsDto());

    public async Task<int> GetNextSequenceNumberAsync(int year)
    {
        // Use JobCardNumber pattern instead of CreatedAt.Year to avoid timezone issues
        var prefix = $"JC-{year}-";
        var jobCardsInYear = await _context.JobCards
            .Where(j => j.JobCardNumber.StartsWith(prefix))
            .Select(j => j.JobCardNumber)
            .ToListAsync();

        if (!jobCardsInYear.Any())
        {
            return 1;
        }

        // Parse sequence numbers and find the maximum
        var maxSequence = jobCardsInYear
            .Select(jcn =>
            {
                var parts = jcn.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                {
                    return seq;
                }

                return 0;
            })
            .Max();

        return maxSequence + 1;
    }

    public async Task<int> GetNextCertificateSequenceNumberAsync(int year)
    {
        var count = await _context.JobCardCertificates.CountAsync(c => c.CreatedAt.Year == year);
        return count + 1;
    }

    public async Task AddCommentAsync(JobCardComment comment) => await _context.JobCardComments.AddAsync(comment);
    public Task<List<JobCardCommentDto>> GetCommentsAsync(Guid jobCardId) => Task.FromResult(new List<JobCardCommentDto>());
    public Task<List<JobCardApprovalStepDto>> GetApprovalHistoryAsync(Guid jobCardId) => Task.FromResult(new List<JobCardApprovalStepDto>());
    public async Task AddDocumentAsync(JobCardDocument document) => await _context.JobCardDocuments.AddAsync(document);

    public async Task DeleteDocumentAsync(Guid documentId)
    {
        var document = await _context.JobCardDocuments.FindAsync(documentId);
        if (document != null)
        {
            _context.JobCardDocuments.Remove(document);
        }
    }

    public async Task<List<JobCardDocumentDto>> GetDocumentsAsync(Guid jobCardId)
    {
        return await _context.JobCardDocuments
            .Where(d => d.JobCardId == jobCardId && !d.IsDeleted)
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.UploadedDate)
            .Select(d => new JobCardDocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                FilePath = d.FilePath,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
                DocumentType = d.DocumentType,
                UploadedAt = d.UploadedDate,
                UploadedBy = d.UploadedBy != null ? d.UploadedBy.FullName : "Unknown"
            })
            .ToListAsync();
    }

    public async Task<JobCardDocument?> GetDocumentAsync(Guid jobCardId, Guid documentId)
    {
        return await _context.JobCardDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.JobCardId == jobCardId && !d.IsDeleted);
    }

    public async Task AddCertificateAsync(JobCardCertificate certificate) => await _context.JobCardCertificates.AddAsync(certificate);

    public async Task<List<JobCardCertificateDto>> GetCertificatesAsync(Guid jobCardId)
    {
        return await _context.JobCardCertificates
            .Where(c => c.JobCardId == jobCardId)
            .Include(c => c.IssuedBy)
            .Include(c => c.JobCard)
            .Include(c => c.Asset)
            .Select(c => new JobCardCertificateDto
            {
                Id = c.Id,
                JobCardId = c.JobCardId,
                JobCardNumber = c.JobCard.JobCardNumber,
                AssetId = c.AssetId,
                AssetName = c.Asset.Name,
                CertificateNumber = c.CertificateNumber,
                CertificateType = c.CertificateType,
                IssuedDate = c.IssuedDate,
                ValidUntil = c.ValidUntil,
                IssuedBy = c.IssuedBy.FullName,
                FilePath = c.FilePath,
                FileFormat = c.FileFormat,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }
    public async Task<List<WorkOrderTypeDto>> GetWorkOrderTypesAsync()
    {
        return await _context.WorkOrderTypes
            .Where(wot => wot.IsActive)
            .Select(wot => new WorkOrderTypeDto
            {
                Id = wot.Id,
                Name = wot.Name,
                Code = wot.Code,
                Description = wot.Description,
                Color = wot.Color,
                Icon = wot.Icon,
                IsActive = wot.IsActive,
                RequiresApproval = wot.RequiresApproval,
                DefaultPriority = wot.DefaultPriority
            })
            .ToListAsync();
    }
}
