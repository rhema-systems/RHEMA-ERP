namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown when a punch is rejected by hard geofence enforcement (outside zone with GPS present).
/// </summary>
public sealed class GeofenceVerificationRejectedException : Exception
{
    public GeofenceVerificationRejectedException(string message) : base(message) { }
}
