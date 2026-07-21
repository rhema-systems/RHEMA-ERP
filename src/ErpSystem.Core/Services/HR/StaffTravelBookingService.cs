using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelBookingService> _logger;

    public StaffTravelBookingService(
        IStaffTravelFlightBookingRepository flightRepository,
        IStaffTravelFlightSegmentRepository segmentRepository,
        IStaffTravelHotelBookingRepository hotelRepository,
        IStaffTravelGroundTransportRepository groundRepository,
        IStaffTravelCarRentalBookingRepository carRentalRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelBookingService> logger)
    {
        _flightRepository = flightRepository;
        _segmentRepository = segmentRepository;
        _hotelRepository = hotelRepository;
        _groundRepository = groundRepository;
        _carRentalRepository = carRentalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ---- Flight bookings ---------------------------------------------------

    public async Task<StaffTravelFlightBookingDto> GetFlightByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _flightRepository.GetWithSegmentsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Flight booking with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _flightRepository.GetByRequestIdAsync(requestId)).Select(f => f.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByStatusAsync(TravelBookingStatus status, CancellationToken cancellationToken = default)
        => (await _flightRepository.GetByStatusAsync(status)).Select(f => f.ToSummaryDto()).ToList();

    public async Task<StaffTravelFlightBookingDto> CreateFlightAsync(CreateStaffTravelFlightBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _flightRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelFlightBookingDto> UpdateFlightAsync(UpdateStaffTravelFlightBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _flightRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Flight booking with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _flightRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await _flightRepository.GetWithSegmentsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> DeleteFlightAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _flightRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Flight booking with ID '{id}' not found.");

        await _flightRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Flight segments ---------------------------------------------------

    public async Task<StaffTravelFlightSegmentDto> AddSegmentAsync(CreateStaffTravelFlightSegmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _segmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelFlightSegmentDto>> GetSegmentsAsync(Guid flightBookingId, CancellationToken cancellationToken = default)
        => (await _segmentRepository.GetByBookingIdAsync(flightBookingId)).Select(s => s.ToDto()).ToList();

    public async Task<StaffTravelFlightSegmentDto> UpdateSegmentAsync(UpdateStaffTravelFlightSegmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _segmentRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Flight segment with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _segmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSegmentAsync(Guid segmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _segmentRepository.GetByIdAsync(segmentId);
        if (entity == null)
            throw new ArgumentException($"Flight segment with ID '{segmentId}' not found.");

        await _segmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Hotel bookings ----------------------------------------------------

    public async Task<StaffTravelHotelBookingDto> GetHotelByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _hotelRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Hotel booking with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelHotelBookingSummaryDto>> GetHotelsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _hotelRepository.GetByRequestIdAsync(requestId)).Select(h => h.ToSummaryDto()).ToList();

    public async Task<StaffTravelHotelBookingDto> CreateHotelAsync(CreateStaffTravelHotelBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _hotelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelHotelBookingDto> UpdateHotelAsync(UpdateStaffTravelHotelBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _hotelRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Hotel booking with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _hotelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHotelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _hotelRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Hotel booking with ID '{id}' not found.");

        await _hotelRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Ground transport --------------------------------------------------

    public async Task<StaffTravelGroundTransportDto> GetGroundTransportByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _groundRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Ground transport with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelGroundTransportDto>> GetGroundTransportsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _groundRepository.GetByRequestIdAsync(requestId)).Select(g => g.ToDto()).ToList();

    public async Task<StaffTravelGroundTransportDto> CreateGroundTransportAsync(CreateStaffTravelGroundTransportDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _groundRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelGroundTransportDto> UpdateGroundTransportAsync(UpdateStaffTravelGroundTransportDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _groundRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Ground transport with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _groundRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteGroundTransportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _groundRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Ground transport with ID '{id}' not found.");

        await _groundRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Car rental bookings -----------------------------------------------

    public async Task<StaffTravelCarRentalBookingDto> GetCarRentalByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _carRentalRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Car rental booking with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelCarRentalBookingDto>> GetCarRentalsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _carRentalRepository.GetByRequestIdAsync(requestId)).Select(c => c.ToDto()).ToList();

    public async Task<StaffTravelCarRentalBookingDto> CreateCarRentalAsync(CreateStaffTravelCarRentalBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _carRentalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelCarRentalBookingDto> UpdateCarRentalAsync(UpdateStaffTravelCarRentalBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _carRentalRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Car rental booking with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _carRentalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCarRentalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _carRentalRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Car rental booking with ID '{id}' not found.");

        await _carRentalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
