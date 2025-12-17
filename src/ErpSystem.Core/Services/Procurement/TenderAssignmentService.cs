using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderAssignmentService : ITenderAssignmentService
{
    private readonly ITenderAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TenderAssignmentService> _logger;

    public TenderAssignmentService(
        ITenderAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        ILogger<TenderAssignmentService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<IEnumerable<TenderAssignmentDto>> GetByTenderIdAsync(Guid tenderId)
    {
        var assignments = await _repository.GetByTenderIdAsync(tenderId);
        return assignments.Select(MapToDto);
    }

    public async Task<IEnumerable<TenderAssignmentDto>> GetByUserIdAsync(Guid userId)
    {
        var assignments = await _repository.GetByUserIdAsync(userId);
        return assignments.Select(MapToDto);
    }

    public async Task<IEnumerable<TenderAssignmentDto>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        var assignments = await _repository.GetByBusinessPartnerIdAsync(businessPartnerId);
        return assignments.Select(MapToDto);
    }

    public async Task<TenderAssignmentDto> CreateAsync(CreateTenderAssignmentDto dto)
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var currentUserId = _currentUserService.UserId != null ? Guid.Parse(_currentUserService.UserId) : Guid.Empty;

            if (dto.AssignmentType == "AllUsers")
            {
                // Create a single assignment for all users
                var assignment = new TenderAssignment
                {
                    Id = Guid.NewGuid(),
                    TenderId = dto.TenderId,
                    BusinessPartnerId = dto.BusinessPartnerId,
                    AssignedToUserId = null,
                    AssignmentType = "AllUsers",
                    AssignedAt = DateTime.UtcNow,
                    AssignedById = currentUserId,
                    Notes = dto.Notes,
                    TenantId = tenantId
                };

                await _repository.CreateAsync(assignment);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Created tender assignment for all users: Tender {TenderId}, BusinessPartner {BusinessPartnerId}", 
                    dto.TenderId, dto.BusinessPartnerId);

                var created = await _repository.GetByIdAsync(assignment.Id);
                return MapToDto(created!);
            }
            else if (dto.AssignmentType == "Self")
            {
                // Create assignment for current user
                var assignment = new TenderAssignment
                {
                    Id = Guid.NewGuid(),
                    TenderId = dto.TenderId,
                    BusinessPartnerId = dto.BusinessPartnerId,
                    AssignedToUserId = currentUserId,
                    AssignmentType = "Self",
                    AssignedAt = DateTime.UtcNow,
                    AssignedById = currentUserId,
                    Notes = dto.Notes,
                    TenantId = tenantId
                };

                await _repository.CreateAsync(assignment);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Created tender assignment for self: Tender {TenderId}, User {UserId}", 
                    dto.TenderId, currentUserId);

                var created = await _repository.GetByIdAsync(assignment.Id);
                return MapToDto(created!);
            }
            else if (dto.AssignmentType == "SelectedUsers" && dto.AssignedUserIds != null && dto.AssignedUserIds.Any())
            {
                // Create assignments for selected users
                var firstAssignment = new TenderAssignment
                {
                    Id = Guid.NewGuid(),
                    TenderId = dto.TenderId,
                    BusinessPartnerId = dto.BusinessPartnerId,
                    AssignedToUserId = dto.AssignedUserIds.First(),
                    AssignmentType = "SelectedUsers",
                    AssignedAt = DateTime.UtcNow,
                    AssignedById = currentUserId,
                    Notes = dto.Notes,
                    TenantId = tenantId
                };

                await _repository.CreateAsync(firstAssignment);

                // Create additional assignments for other users
                foreach (var userId in dto.AssignedUserIds.Skip(1))
                {
                    var assignment = new TenderAssignment
                    {
                        Id = Guid.NewGuid(),
                        TenderId = dto.TenderId,
                        BusinessPartnerId = dto.BusinessPartnerId,
                        AssignedToUserId = userId,
                        AssignmentType = "SelectedUsers",
                        AssignedAt = DateTime.UtcNow,
                        AssignedById = currentUserId,
                        Notes = dto.Notes,
                        TenantId = tenantId
                    };

                    await _repository.CreateAsync(assignment);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Created tender assignments for {Count} selected users: Tender {TenderId}", 
                    dto.AssignedUserIds.Count, dto.TenderId);

                var created = await _repository.GetByIdAsync(firstAssignment.Id);
                return MapToDto(created!);
            }

            throw new InvalidOperationException("Invalid assignment type or missing user IDs");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender assignment");
            throw;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted tender assignment {Id}", id);
    }

    public async Task<bool> HasAccessAsync(Guid userId, Guid tenderId)
    {
        var assignments = await _repository.GetByUserIdAsync(userId);
        return assignments.Any(a => a.TenderId == tenderId);
    }

    public async Task<IEnumerable<Guid>> GetAccessibleTenderIdsAsync(Guid userId, Guid businessPartnerId)
    {
        var assignments = await _repository.GetByBusinessPartnerIdAsync(businessPartnerId);

        return assignments
            .Where(a => a.AssignmentType == "AllUsers" || a.AssignedToUserId == userId)
            .Select(a => a.TenderId)
            .Distinct()
            .ToList();
    }

    private static TenderAssignmentDto MapToDto(TenderAssignment entity)
    {
        return new TenderAssignmentDto
        {
            Id = entity.Id,
            TenderId = entity.TenderId,
            TenderNumber = entity.Tender?.TenderNumber,
            TenderTitle = entity.Tender?.Title,
            BusinessPartnerId = entity.BusinessPartnerId,
            BusinessPartnerName = entity.BusinessPartner?.PartnerName,
            AssignedToUserId = entity.AssignedToUserId,
            AssignedToUserName = entity.AssignedToUser?.FullName,
            AssignmentType = entity.AssignmentType,
            AssignedAt = entity.AssignedAt,
            AssignedByName = entity.AssignedBy?.FullName,
            Notes = entity.Notes
        };
    }
}

