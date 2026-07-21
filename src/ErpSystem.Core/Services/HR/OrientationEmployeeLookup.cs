using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Batch-resolves employee display names for Orientation DTOs. Orientation entities
/// reference employees by Guid only (no navigation), so name fields are filled here
/// in the service layer with a single keyed lookup instead of per-row queries.
/// </summary>
internal static class OrientationEmployeeLookup
{
    public static async Task<IReadOnlyDictionary<Guid, (string Name, string Number)>> ResolveEmployeesAsync(
        this IUnitOfWork uow, IEnumerable<Guid?> ids)
    {
        var idList = ids.Where(i => i.HasValue && i.Value != Guid.Empty).Select(i => i!.Value).Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, (string, string)>();

        var employees = await uow.Repository<Employee>().FindAsync(e => idList.Contains(e.Id));
        return employees.ToDictionary(e => e.Id, e => (e.FullName, e.EmployeeNumber));
    }

    public static void FillNames(this IEnumerable<EmployeeOrientationSummaryDto> items, IReadOnlyDictionary<Guid, (string Name, string Number)> map)
    {
        foreach (var i in items)
            if (map.TryGetValue(i.EmployeeId, out var e)) { i.EmployeeName = e.Name; i.EmployeeNumber = e.Number; }
    }

    public static void FillNames(this EmployeeOrientationDto dto, IReadOnlyDictionary<Guid, (string Name, string Number)> map)
    {
        if (map.TryGetValue(dto.EmployeeId, out var e)) { dto.EmployeeName = e.Name; dto.EmployeeNumber = e.Number; }
        if (dto.EnrolledByEmployeeId.HasValue && map.TryGetValue(dto.EnrolledByEmployeeId.Value, out var by)) dto.EnrolledByName = by.Name;
        dto.Certificates.FillNames(map);
        dto.Feedbacks.FillNames(map);
        dto.AttendanceRecords.FillNames(map);
    }

    public static void FillNames(this IEnumerable<OrientationCertificateDto> items, IReadOnlyDictionary<Guid, (string Name, string Number)> map)
    {
        foreach (var c in items)
        {
            if (c.EmployeeId.HasValue && map.TryGetValue(c.EmployeeId.Value, out var e)) c.EmployeeName = e.Name;
            if (c.IssuedByEmployeeId.HasValue && map.TryGetValue(c.IssuedByEmployeeId.Value, out var by)) c.IssuedByName = by.Name;
        }
    }

    public static void FillNames(this IEnumerable<OrientationSessionFacilitatorDto> items, IReadOnlyDictionary<Guid, (string Name, string Number)> map)
    {
        foreach (var f in items)
            if (f.EmployeeId.HasValue && map.TryGetValue(f.EmployeeId.Value, out var e)) f.EmployeeName = e.Name;
    }

    public static void FillNames(this IEnumerable<OrientationFeedbackDto> items, IReadOnlyDictionary<Guid, (string Name, string Number)> map)
    {
        foreach (var f in items)
            if (f.SubmittedByEmployeeId.HasValue && map.TryGetValue(f.SubmittedByEmployeeId.Value, out var e)) f.SubmittedByName = e.Name;
    }

    public static void FillNames(this IEnumerable<OrientationAttendanceRecordDto> items, IReadOnlyDictionary<Guid, (string Name, string Number)> map)
    {
        foreach (var a in items)
        {
            if (a.EmployeeId.HasValue && map.TryGetValue(a.EmployeeId.Value, out var e)) a.EmployeeName = e.Name;
            if (a.MarkedByEmployeeId.HasValue && map.TryGetValue(a.MarkedByEmployeeId.Value, out var by)) a.MarkedByName = by.Name;
        }
    }

    // Id collectors for the resolver
    public static IEnumerable<Guid?> EmployeeIds(this IEnumerable<EmployeeOrientationSummaryDto> items)
        => items.Select(i => (Guid?)i.EmployeeId);

    public static IEnumerable<Guid?> EmployeeIds(this EmployeeOrientationDto dto)
    {
        yield return dto.EmployeeId;
        yield return dto.EnrolledByEmployeeId;
        foreach (var c in dto.Certificates) { yield return c.EmployeeId; yield return c.IssuedByEmployeeId; }
        foreach (var f in dto.Feedbacks) yield return f.SubmittedByEmployeeId;
        foreach (var a in dto.AttendanceRecords) { yield return a.EmployeeId; yield return a.MarkedByEmployeeId; }
    }

    public static IEnumerable<Guid?> EmployeeIds(this IEnumerable<OrientationCertificateDto> items)
        => items.SelectMany(c => new[] { c.EmployeeId, c.IssuedByEmployeeId });
}
