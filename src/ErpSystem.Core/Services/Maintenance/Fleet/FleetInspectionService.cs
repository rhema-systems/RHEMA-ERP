using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetInspectionService : IFleetInspectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetInspectionService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IReadOnlyList<FleetTripInspectionDto>> GetTripInspectionsAsync(Guid tripId)
    {
        if (tripId == Guid.Empty) return Array.Empty<FleetTripInspectionDto>();

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetTripInspection>();

        var list = await repo.GetQueryable(i => i.TenantId == tenantId && i.FleetTripId == tripId && !i.IsDeleted)
            .Include(i => i.InspectionTemplate)
            .Include(i => i.InspectorEmployee)
            .OrderByDescending(i => i.StartedAtUtc)
            .Select(i => new FleetTripInspectionDto
            {
                Id = i.Id,
                FleetTripId = i.FleetTripId,
                InspectionTemplateId = i.InspectionTemplateId,
                InspectionTemplateName = i.InspectionTemplate.Name,
                InspectorEmployeeId = i.InspectorEmployeeId,
                InspectorEmployeeName = i.InspectorEmployee != null ? (i.InspectorEmployee.FirstName + " " + i.InspectorEmployee.LastName) : null,
                InspectionKind = i.InspectionKind,
                StartedAtUtc = i.StartedAtUtc,
                CompletedAtUtc = i.CompletedAtUtc,
                Status = i.Status,
                OverallResult = i.OverallResult,
                InspectionData = i.InspectionData,
                Notes = i.Notes
            })
            .ToListAsync();

        return list;
    }

    public async Task<FleetTripInspectionDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetTripInspection>();
        var i = await repo.GetQueryable(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.InspectionTemplate)
            .Include(x => x.InspectorEmployee)
            .FirstOrDefaultAsync();

        if (i == null) return null;

        return new FleetTripInspectionDto
        {
            Id = i.Id,
            FleetTripId = i.FleetTripId,
            InspectionTemplateId = i.InspectionTemplateId,
            InspectionTemplateName = i.InspectionTemplate.Name,
            InspectorEmployeeId = i.InspectorEmployeeId,
            InspectorEmployeeName = i.InspectorEmployee != null ? (i.InspectorEmployee.FirstName + " " + i.InspectorEmployee.LastName) : null,
            InspectionKind = i.InspectionKind,
            StartedAtUtc = i.StartedAtUtc,
            CompletedAtUtc = i.CompletedAtUtc,
            Status = i.Status,
            OverallResult = i.OverallResult,
            InspectionData = i.InspectionData,
            Notes = i.Notes
        };
    }

    public async Task<FleetTripInspectionDto> StartAsync(StartFleetTripInspectionDto dto)
    {
        dto ??= new StartFleetTripInspectionDto();
        if (dto.FleetTripId == Guid.Empty) throw new ArgumentException("FleetTripId is required.");
        if (dto.InspectionTemplateId == Guid.Empty) throw new ArgumentException("InspectionTemplateId is required.");
        if (string.IsNullOrWhiteSpace(dto.InspectionKind)) dto.InspectionKind = "PreTrip";

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var trip = await _unitOfWork.Repository<FleetTrip>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == dto.FleetTripId && !t.IsDeleted);
        if (trip == null) throw new ArgumentException("Trip not found.");

        var template = await _unitOfWork.Repository<InspectionTemplate>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == dto.InspectionTemplateId && !t.IsDeleted);
        if (template == null) throw new ArgumentException("Inspection template not found.");

        var repo = _unitOfWork.Repository<FleetTripInspection>();

        var existing = await repo.FirstOrDefaultAsync(i =>
            i.TenantId == tenantId &&
            i.FleetTripId == dto.FleetTripId &&
            i.InspectionKind == dto.InspectionKind &&
            i.Status != "Cancelled" &&
            !i.IsDeleted);

        if (existing != null)
        {
            return (await GetByIdAsync(existing.Id))!;
        }

        var inspectorEmployeeId = dto.InspectorEmployeeId ?? trip.DriverEmployeeId;

        var entity = new FleetTripInspection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FleetTripId = dto.FleetTripId,
            InspectionTemplateId = dto.InspectionTemplateId,
            InspectorEmployeeId = inspectorEmployeeId,
            InspectionKind = dto.InspectionKind.Trim(),
            StartedAtUtc = now,
            Status = "InProgress",
            InspectionData = "{}",
            CreatedAt = now,
            CreatedById = userId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetTripInspectionDto> CompleteAsync(Guid inspectionId, CompleteFleetTripInspectionDto dto)
    {
        if (inspectionId == Guid.Empty) throw new ArgumentException("InspectionId is required.");
        dto ??= new CompleteFleetTripInspectionDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetTripInspection>();
        var entity = await repo.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == inspectionId && !i.IsDeleted)
            ?? throw new ArgumentException("Inspection not found.");

        entity.CompletedAtUtc = (dto.CompletedAtUtc ?? now).ToUniversalTime();
        entity.Status = "Completed";
        entity.OverallResult = dto.OverallResult.Trim();
        entity.InspectionData = string.IsNullOrWhiteSpace(dto.InspectionData) ? "{}" : dto.InspectionData;
        entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? entity.Notes : dto.Notes.Trim();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);

        // If failed, auto-create a defect (best-effort).
        if (string.Equals(entity.OverallResult, "Fail", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var trip = await _unitOfWork.Repository<FleetTrip>()
                    .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == entity.FleetTripId && !t.IsDeleted);
                if (trip != null)
                {
                    await _unitOfWork.Repository<FleetDefect>().AddAsync(new FleetDefect
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        VehicleAssetId = trip.VehicleAssetId,
                        FleetTripId = trip.Id,
                        FleetTripInspectionId = entity.Id,
                        Title = $"Inspection failed ({entity.InspectionKind})",
                        Description = entity.Notes,
                        Severity = "Medium",
                        Status = "Open",
                        ReportedAtUtc = now,
                        ReportedByEmployeeId = entity.InspectorEmployeeId,
                        AdditionalData = entity.InspectionData,
                        CreatedAt = now,
                        CreatedById = userId
                    });
                }
            }
            catch
            {
                // ignore (best-effort)
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<bool> CancelAsync(Guid inspectionId, string? notes = null)
    {
        if (inspectionId == Guid.Empty) return false;

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetTripInspection>();
        var entity = await repo.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == inspectionId && !i.IsDeleted);
        if (entity == null) return false;

        entity.Status = "Cancelled";
        entity.Notes = string.IsNullOrWhiteSpace(notes) ? entity.Notes : notes.Trim();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}

