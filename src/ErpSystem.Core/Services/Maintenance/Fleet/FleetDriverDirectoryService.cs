using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetDriverDirectoryService : IFleetDriverDirectoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetDriverDirectoryService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<FleetDriverDirectoryDto> GetDriversPagedAsync(
        int page,
        int pageSize,
        string? searchTerm = null,
        string? licenseStatus = null,
        bool? assigned = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, 100);

        var tenantId = _currentUserProvider.TenantId;
        var employeeRepository = _unitOfWork.Repository<Employee>();
        var licenseQuery = _unitOfWork.Repository<EmployeeIdentificationCard>()
            .GetQueryable(card =>
                card.TenantId == tenantId &&
                !card.IsDeleted &&
                card.DocumentType.ToLower().Contains("driver"));
        var assignmentQuery = _unitOfWork.Repository<FleetVehicleAssignment>()
            .GetQueryable(item => item.TenantId == tenantId && item.IsActive && !item.IsDeleted);
        var tripQuery = _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted);

        var employeeQuery = employeeRepository.GetQueryable(employee => employee.TenantId == tenantId && !employee.IsDeleted)
            .Include(employee => employee.Position)
            .Include(employee => employee.Department)
            .Where(employee =>
                employee.Position.Title.ToLower().Contains("driver") ||
                licenseQuery.Any(card => card.EmployeeId == employee.Id) ||
                assignmentQuery.Any(item => item.EmployeeId == employee.Id) ||
                tripQuery.Any(item => item.DriverEmployeeId == employee.Id));

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim();
            employeeQuery = employeeQuery.Where(employee =>
                employee.EmployeeNumber.Contains(search) ||
                employee.FirstName.Contains(search) ||
                employee.LastName.Contains(search) ||
                employee.EmailAddress.Contains(search) ||
                (employee.MobileNumber != null && employee.MobileNumber.Contains(search)) ||
                (employee.TelephoneNumber != null && employee.TelephoneNumber.Contains(search)));
        }

        var employees = await employeeQuery
            .OrderBy(employee => employee.FirstName)
            .ThenBy(employee => employee.LastName)
            .ToListAsync();

        if (employees.Count == 0)
        {
            return new FleetDriverDirectoryDto
            {
                Items = Array.Empty<FleetDriverDto>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var licenses = await licenseQuery
            .Where(card => employeeIds.Contains(card.EmployeeId))
            .ToListAsync();
        var assignments = await assignmentQuery
            .Where(item => employeeIds.Contains(item.EmployeeId))
            .Include(item => item.VehicleAsset)
            .ToListAsync();
        var trips = await tripQuery
            .Where(item => item.DriverEmployeeId.HasValue && employeeIds.Contains(item.DriverEmployeeId.Value))
            .Select(item => new
            {
                EmployeeId = item.DriverEmployeeId!.Value,
                item.Status,
                item.CreatedAt,
                item.Id,
                item.VehicleAssetId,
                item.PlannedStartAt,
                item.ActualStartAt,
                item.DispatchedAt,
                VehicleName = item.VehicleAsset.Name,
                VehicleAssetNumber = item.VehicleAsset.AssetNumber
            })
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var expiringThrough = today.AddDays(30);

        var rows = employees.Select(employee =>
        {
            var license = licenses
                .Where(card => card.EmployeeId == employee.Id)
                .OrderByDescending(card => card.ExpiryDate ?? DateOnly.MinValue)
                .ThenByDescending(card => card.UpdatedAt ?? card.CreatedAt)
                .FirstOrDefault();
            var currentAssignment = assignments
                .Where(item => item.EmployeeId == employee.Id)
                .OrderByDescending(item => item.AssignedFromUtc)
                .FirstOrDefault();
            var employeeTrips = trips.Where(item => item.EmployeeId == employee.Id).ToList();
            var status = GetLicenseStatus(license, today, expiringThrough);
            var lastTripAt = employeeTrips
                .Select(item => item.DispatchedAt ?? item.ActualStartAt ?? item.PlannedStartAt ?? item.CreatedAt)
                .OrderByDescending(value => value)
                .FirstOrDefault();
            var activeTrip = employeeTrips
                .Where(item => string.Equals(item.Status, FleetTripStatuses.Dispatched, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.DispatchedAt ?? item.ActualStartAt ?? item.CreatedAt)
                .FirstOrDefault();

            return new FleetDriverDto
            {
                EmployeeId = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
                EmailAddress = employee.EmailAddress,
                PhoneNumber = employee.MobileNumber ?? employee.TelephoneNumber,
                PositionTitle = employee.Position?.Title ?? string.Empty,
                DepartmentName = employee.Department?.Name ?? string.Empty,
                StaffStatus = employee.StaffStatus.ToString(),
                IsActive = employee.IsActive,
                DriverLicenseId = license?.Id,
                DriverLicenseNumber = license?.DocumentNumber,
                LicenseIssueDate = license?.IssueDate,
                LicenseExpiryDate = license?.ExpiryDate,
                LicenseIssuingAuthority = license?.IssuingAuthority,
                IsLicenseVerified = license?.IsVerified == true,
                LicenseVerifiedDate = license?.VerifiedDate,
                LicenseStatus = status,
                DaysUntilLicenseExpiry = license?.ExpiryDate is { } expiry ? expiry.DayNumber - today.DayNumber : null,
                CurrentAssignmentId = currentAssignment?.Id,
                CurrentVehicleAssetId = currentAssignment?.VehicleAssetId,
                CurrentVehicleName = currentAssignment?.VehicleAsset?.Name,
                CurrentVehicleAssetNumber = currentAssignment?.VehicleAsset?.AssetNumber,
                AssignedFromUtc = currentAssignment?.AssignedFromUtc,
                IsAssigned = currentAssignment != null,
                TotalTripCount = employeeTrips.Count,
                ActiveTripCount = employeeTrips.Count(item => string.Equals(item.Status, FleetTripStatuses.Dispatched, StringComparison.OrdinalIgnoreCase)),
                AvailabilityStatus = activeTrip == null ? "Available" : "Engaged",
                ActiveTripId = activeTrip?.Id,
                ActiveTripVehicleName = activeTrip?.VehicleName,
                ActiveTripVehicleAssetNumber = activeTrip?.VehicleAssetNumber,
                ActiveTripStartedAtUtc = activeTrip?.DispatchedAt ?? activeTrip?.ActualStartAt,
                LastTripAtUtc = employeeTrips.Count == 0 ? null : lastTripAt
            };
        }).ToList();

        var summary = new FleetDriverSummaryDto
        {
            TotalDrivers = rows.Count,
            ValidLicenses = rows.Count(row => row.LicenseStatus == "Valid"),
            ExpiringLicenses = rows.Count(row => row.LicenseStatus == "Expiring"),
            ExpiredLicenses = rows.Count(row => row.LicenseStatus == "Expired"),
            MissingLicenses = rows.Count(row => row.LicenseStatus is "Missing" or "Missing Expiry"),
            UnverifiedLicenses = rows.Count(row => row.LicenseStatus == "Unverified"),
            AssignedDrivers = rows.Count(row => row.IsAssigned),
            EngagedDrivers = rows.Count(row => row.AvailabilityStatus == "Engaged")
        };

        if (!string.IsNullOrWhiteSpace(licenseStatus) && !licenseStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows
                .Where(row => row.LicenseStatus.Equals(licenseStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (assigned.HasValue)
        {
            rows = rows.Where(row => row.IsAssigned == assigned.Value).ToList();
        }

        var totalCount = rows.Count;
        var items = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new FleetDriverDirectoryDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Summary = summary
        };
    }

    private static string GetLicenseStatus(
        EmployeeIdentificationCard? license,
        DateOnly today,
        DateOnly expiringThrough)
    {
        if (license == null) return "Missing";
        if (!license.IsVerified) return "Unverified";
        if (!license.ExpiryDate.HasValue) return "Missing Expiry";
        if (license.ExpiryDate.Value < today) return "Expired";
        if (license.ExpiryDate.Value <= expiringThrough) return "Expiring";
        return "Valid";
    }
}
