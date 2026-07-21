using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public sealed class GeofenceVerificationService : IGeofenceVerificationService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IGeofenceZoneRepository _geofenceZoneRepository;
    private readonly ILogger<GeofenceVerificationService> _logger;

    public GeofenceVerificationService(
        IEmployeeRepository employeeRepository,
        IGeofenceZoneRepository geofenceZoneRepository,
        ILogger<GeofenceVerificationService> logger)
    {
        _employeeRepository = employeeRepository;
        _geofenceZoneRepository = geofenceZoneRepository;
        _logger = logger;
    }

    public async Task<GeofenceVerificationResult> VerifyPunchAsync(
        Guid employeeId,
        Guid tenantId,
        double? latitude,
        double? longitude,
        CancellationToken ct = default)
    {
        var hasGps = latitude.HasValue && longitude.HasValue;

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != tenantId)
        {
            return GeofenceVerificationResult.Skipped(
                LocationVerificationStatus.Unverified,
                hasConfiguredZone: false,
                hasGpsCoordinates: hasGps,
                message: "Employee not found.");
        }

        if (!employee.LocationId.HasValue)
        {
            return GeofenceVerificationResult.Skipped(
                hasGps ? LocationVerificationStatus.Unverified : LocationVerificationStatus.GPSUnavailable,
                hasConfiguredZone: false,
                hasGpsCoordinates: hasGps,
                message: "Employee has no work location assigned — geofence check skipped.");
        }

        var zones = (await _geofenceZoneRepository.GetByLocationIdAsync(employee.LocationId.Value)).ToList();
        var zone = zones.FirstOrDefault();

        if (zone == null)
        {
            return GeofenceVerificationResult.Skipped(
                hasGps ? LocationVerificationStatus.Unverified : LocationVerificationStatus.GPSUnavailable,
                hasConfiguredZone: false,
                hasGpsCoordinates: hasGps,
                message: "No active geofence zone is linked to the employee's work location.");
        }

        if (!hasGps)
        {
            return GeofenceVerificationResult.Skipped(
                LocationVerificationStatus.GPSUnavailable,
                hasConfiguredZone: true,
                hasGpsCoordinates: false,
                message: "GPS coordinates were not supplied — punch accepted without location verification.");
        }

        if (zone.Shape != GeofenceShape.Circle)
        {
            return GeofenceVerificationResult.Skipped(
                LocationVerificationStatus.Unverified,
                hasConfiguredZone: true,
                hasGpsCoordinates: true,
                message: $"Geofence zone '{zone.ZoneName}' uses {zone.Shape}; only circle zones are verified automatically.");
        }

        if (!zone.CentreLatitude.HasValue || !zone.CentreLongitude.HasValue || !zone.RadiusMetres.HasValue)
        {
            return GeofenceVerificationResult.Skipped(
                LocationVerificationStatus.Unverified,
                hasConfiguredZone: true,
                hasGpsCoordinates: true,
                message: $"Geofence zone '{zone.ZoneName}' is missing circle coordinates.");
        }

        var distance = CalculateDistanceMetres(
            latitude!.Value,
            longitude!.Value,
            zone.CentreLatitude.Value,
            zone.CentreLongitude.Value);

        var withinZone = distance <= zone.RadiusMetres.Value;
        var status = withinZone
            ? LocationVerificationStatus.WithinZone
            : LocationVerificationStatus.OutsideZone;

        var shouldReject = !withinZone && zone.HardEnforcement;

        if (!withinZone)
        {
            _logger.LogInformation(
                "Geofence check for employee {EmployeeId}: {Distance:F1}m from zone {ZoneName} (radius {Radius}m) — {Status}",
                employeeId,
                distance,
                zone.ZoneName,
                zone.RadiusMetres,
                status);
        }

        return new GeofenceVerificationResult
        {
            Status = status,
            GeofenceZoneId = zone.Id,
            GeofenceZoneName = zone.ZoneName,
            DistanceFromZoneMetres = Math.Round(distance, 1),
            ShouldReject = shouldReject,
            HasConfiguredZone = true,
            HasGpsCoordinates = true,
            Message = shouldReject
                ? $"You are outside the approved work zone ({zone.ZoneName}). Check-in was rejected."
                : withinZone
                    ? null
                    : zone.SoftEnforcement
                        ? $"Outside work zone ({zone.ZoneName}) — punch recorded with a location warning."
                        : $"Outside work zone ({zone.ZoneName}).",
        };
    }

    internal static double CalculateDistanceMetres(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMetres = 6371000;
        static double ToRadians(double degrees) => degrees * Math.PI / 180;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMetres * c;
    }
}
