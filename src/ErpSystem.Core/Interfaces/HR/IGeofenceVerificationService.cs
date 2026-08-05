using ErpSystem.Core.Enums;
using ErpSystem.Core.Models.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Validates attendance punch GPS coordinates against geofence zones.
/// Only applies when coordinates are present; other capture channels are unaffected.
/// </summary>
public interface IGeofenceVerificationService
{
    Task<GeofenceVerificationResult> VerifyPunchAsync(
        Guid employeeId,
        Guid tenantId,
        double? latitude,
        double? longitude,
        CancellationToken ct = default);
}
