using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public class FleetTripDestinationService : IFleetTripDestinationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetTripDestinationService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IReadOnlyList<FleetTripDestinationDto>> GetAllAsync(bool activeOnly = false)
    {
        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetTripDestination>();

        var q = repo.GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted);
        if (activeOnly)
        {
            q = q.Where(d => d.IsActive);
        }

        var list = await q
            .OrderBy(d => d.Name)
            .Select(d => new FleetTripDestinationDto
            {
                Id = d.Id,
                Name = d.Name,
                Origin = d.Origin,
                Destination = d.Destination,
                ExpectedHours = d.ExpectedHours,
                ExpectedMileage = d.ExpectedMileage,
                IsActive = d.IsActive
            })
            .ToListAsync();

        return list;
    }

    public async Task<FleetTripDestinationDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;

        var tenantId = _currentUserProvider.TenantId;
        var entity = await _unitOfWork.Repository<FleetTripDestination>()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted);

        return entity == null
            ? null
            : new FleetTripDestinationDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Origin = entity.Origin,
                Destination = entity.Destination,
                ExpectedHours = entity.ExpectedHours,
                ExpectedMileage = entity.ExpectedMileage,
                IsActive = entity.IsActive
            };
    }

    public async Task<FleetTripDestinationDto> CreateAsync(CreateFleetTripDestinationDto dto)
    {
        dto ??= new CreateFleetTripDestinationDto();
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Name is required.");
        if (dto.ExpectedHours.HasValue && dto.ExpectedHours.Value < 0) throw new ArgumentException("ExpectedHours cannot be negative.");
        if (dto.ExpectedMileage.HasValue && dto.ExpectedMileage.Value < 0) throw new ArgumentException("ExpectedMileage cannot be negative.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTripDestination>();

        var entity = new FleetTripDestination
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            Origin = string.IsNullOrWhiteSpace(dto.Origin) ? null : dto.Origin.Trim(),
            Destination = string.IsNullOrWhiteSpace(dto.Destination) ? null : dto.Destination.Trim(),
            ExpectedHours = dto.ExpectedHours,
            ExpectedMileage = dto.ExpectedMileage,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetTripDestinationDto> UpdateAsync(Guid id, UpdateFleetTripDestinationDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.");
        dto ??= new UpdateFleetTripDestinationDto();
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Name is required.");
        if (dto.ExpectedHours.HasValue && dto.ExpectedHours.Value < 0) throw new ArgumentException("ExpectedHours cannot be negative.");
        if (dto.ExpectedMileage.HasValue && dto.ExpectedMileage.Value < 0) throw new ArgumentException("ExpectedMileage cannot be negative.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTripDestination>();
        var entity = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted)
            ?? throw new ArgumentException($"Trip destination with ID {id} not found.");

        entity.Name = dto.Name.Trim();
        entity.Origin = string.IsNullOrWhiteSpace(dto.Origin) ? null : dto.Origin.Trim();
        entity.Destination = string.IsNullOrWhiteSpace(dto.Destination) ? null : dto.Destination.Trim();
        entity.ExpectedHours = dto.ExpectedHours;
        entity.ExpectedMileage = dto.ExpectedMileage;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTripDestination>();
        var entity = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId && !d.IsDeleted);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _currentUserProvider.Username;
        entity.LastModifiedById = userId;
        entity.UpdatedAt = DateTime.UtcNow;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}
