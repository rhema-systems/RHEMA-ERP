using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 4: BOOKINGS SERVICE
// ============================================================================

#region Staff Travel Booking Service

public class StaffTravelBookingService : IStaffTravelBookingService
{
    private readonly IStaffTravelFlightBookingRepository _flightRepository;
    private readonly IStaffTravelFlightSegmentRepository _segmentRepository;
    private readonly IStaffTravelHotelBookingRepository _hotelRepository;
    private readonly IStaffTravelGroundTransportRepository _groundRepository;
    private readonly IStaffTravelCarRentalBookingRepository _carRentalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelBookingService> _logger;

    public StaffTravelBookingService(
        IStaffTravelFlightBookingRepository flightRepository,
        IStaffTravelFlightSegmentRepository segmentRepository,
        IStaffTravelHotelBookingRepository hotelRepository,
        IStaffTravelGroundTransportRepository groundRepository,
        IStaffTravelCarRentalBookingRepository carRentalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelBookingService> logger)
    {
        _flightRepository = flightRepository;
        _segmentRepository = segmentRepository;
        _hotelRepository = hotelRepository;
        _groundRepository = groundRepository;
        _carRentalRepository = carRentalRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffTravelFlightBooking> GetOwnedFlightAsync(Guid id)
    {
        var entity = await _flightRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Flight booking with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelFlightSegment> GetOwnedSegmentAsync(Guid id)
    {
        var entity = await _segmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Flight segment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelHotelBooking> GetOwnedHotelAsync(Guid id)
    {
        var entity = await _hotelRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Hotel booking with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelGroundTransport> GetOwnedGroundTransportAsync(Guid id)
    {
        var entity = await _groundRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Ground transport with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelCarRentalBooking> GetOwnedCarRentalAsync(Guid id)
    {
        var entity = await _carRentalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Car rental booking with ID '{id}' not found.");
        return entity;
    }

    // ---- Flight bookings ---------------------------------------------------

    public async Task<StaffTravelFlightBookingDto> GetFlightByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _flightRepository.GetWithSegmentsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Flight booking with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _flightRepository.GetByRequestIdAsync(requestId))
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByStatusAsync(TravelBookingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _flightRepository.GetByStatusAsync(status))
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelFlightBookingDto> CreateFlightAsync(CreateStaffTravelFlightBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _flightRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelFlightBookingDto> UpdateFlightAsync(UpdateStaffTravelFlightBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _flightRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _flightRepository.GetWithSegmentsAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Flight booking with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> DeleteFlightAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(id);
        await _flightRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Flight segments ---------------------------------------------------

    public async Task<StaffTravelFlightSegmentDto> AddSegmentAsync(CreateStaffTravelFlightSegmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedFlightAsync(createDto.StaffTravelFlightBookingId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _segmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelFlightSegmentDto>> GetSegmentsAsync(Guid flightBookingId, CancellationToken cancellationToken = default)
    {
        await GetOwnedFlightAsync(flightBookingId);
        var tenantId = GetTenantId();
        return (await _segmentRepository.GetByBookingIdAsync(flightBookingId))
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.ToDto())
            .ToList();
    }

    public async Task<StaffTravelFlightSegmentDto> UpdateSegmentAsync(UpdateStaffTravelFlightSegmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _segmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSegmentAsync(Guid segmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(segmentId);
        await _segmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Hotel bookings ----------------------------------------------------

    public async Task<StaffTravelHotelBookingDto> GetHotelByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelHotelBookingSummaryDto>> GetHotelsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hotelRepository.GetByRequestIdAsync(requestId))
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelHotelBookingDto> CreateHotelAsync(CreateStaffTravelHotelBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _hotelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelHotelBookingDto> UpdateHotelAsync(UpdateStaffTravelHotelBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _hotelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHotelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(id);
        await _hotelRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Ground transport --------------------------------------------------

    public async Task<StaffTravelGroundTransportDto> GetGroundTransportByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelGroundTransportDto>> GetGroundTransportsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _groundRepository.GetByRequestIdAsync(requestId))
            .Where(g => g.TenantId == tenantId)
            .Select(g => g.ToDto())
            .ToList();
    }

    public async Task<StaffTravelGroundTransportDto> CreateGroundTransportAsync(CreateStaffTravelGroundTransportDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _groundRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelGroundTransportDto> UpdateGroundTransportAsync(UpdateStaffTravelGroundTransportDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _groundRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteGroundTransportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(id);
        await _groundRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Car rental bookings -----------------------------------------------

    public async Task<StaffTravelCarRentalBookingDto> GetCarRentalByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelCarRentalBookingDto>> GetCarRentalsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _carRentalRepository.GetByRequestIdAsync(requestId))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToDto())
            .ToList();
    }

    public async Task<StaffTravelCarRentalBookingDto> CreateCarRentalAsync(CreateStaffTravelCarRentalBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _carRentalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelCarRentalBookingDto> UpdateCarRentalAsync(UpdateStaffTravelCarRentalBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _carRentalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCarRentalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(id);
        await _carRentalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
