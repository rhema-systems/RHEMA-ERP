using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Models.HR;

/// <summary>
/// Outcome of evaluating a punch against the employee's configured geofence zone.
/// </summary>
public sealed class GeofenceVerificationResult
{
    public LocationVerificationStatus Status { get; init; } = LocationVerificationStatus.Unverified;

    public Guid? GeofenceZoneId { get; init; }

    public string? GeofenceZoneName { get; init; }

    public double? DistanceFromZoneMetres { get; init; }

    /// <summary>When true, the punch must be rejected (hard enforcement + outside zone + GPS present).</summary>
    public bool ShouldReject { get; init; }

    public string? Message { get; init; }

    /// <summary>The employee's work location has an active geofence zone assigned.</summary>
    public bool HasConfiguredZone { get; init; }

    /// <summary>GPS coordinates were supplied on the punch.</summary>
    public bool HasGpsCoordinates { get; init; }

    public static GeofenceVerificationResult Skipped(
        LocationVerificationStatus status,
        bool hasConfiguredZone,
        bool hasGpsCoordinates,
        string? message = null)
        => new()
        {
            Status = status,
            HasConfiguredZone = hasConfiguredZone,
            HasGpsCoordinates = hasGpsCoordinates,
            Message = message,
        };
}
