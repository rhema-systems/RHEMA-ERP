using ErpSystem.Core.DTOs.Identity;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Identity;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Identity;

public sealed class HrIdentityAccessService : IHrIdentityAccessService
{
    private static readonly StaffStatus[] EligibleStatuses =
        [StaffStatus.Active, StaffStatus.Probation, StaffStatus.OnLeave];

    private readonly ApplicationDbContext _db;

    public HrIdentityAccessService(ApplicationDbContext db) => _db = db;

    public async Task<HrIdentityAccessDecisionDto> EvaluateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.Id, item.IsActive, item.EmployeeId, item.TenantId })
            .SingleOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return Denied("IDENTITY_USER_NOT_FOUND", "The identity no longer exists.");
        }

        if (!user.IsActive)
        {
            return Denied("IDENTITY_USER_INACTIVE", "The identity is inactive.", user.EmployeeId);
        }

        if (!user.EmployeeId.HasValue)
        {
            return Allowed();
        }

        var employee = await _db.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == user.EmployeeId.Value)
            .Select(item => new
            {
                item.Id,
                item.TenantId,
                item.IsDeleted,
                item.IsActive,
                item.StaffStatus,
                item.TerminationDate
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (employee == null || employee.IsDeleted || employee.TenantId != user.TenantId)
        {
            return Denied(
                "HR_IDENTITY_LINK_INVALID",
                "The linked HR employment record is missing or belongs to a different tenant.",
                user.EmployeeId);
        }

        var employmentTerminated = employee.TerminationDate.HasValue && employee.TerminationDate.Value <= DateTime.UtcNow;
        if (!employee.IsActive || !EligibleStatuses.Contains(employee.StaffStatus) || employmentTerminated)
        {
            return Denied(
                "HR_EMPLOYMENT_INACTIVE",
                "The linked HR employment record is not eligible for ERP access.",
                employee.Id);
        }

        var reactivationReviewRequired = await _db.HrIdentityReconciliationStates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(item =>
                !item.IsDeleted &&
                item.TenantId == user.TenantId &&
                item.UserId == user.Id &&
                item.ReactivationReviewRequired,
                cancellationToken);

        return reactivationReviewRequired
            ? Denied(
                "HR_IDENTITY_REACTIVATION_REVIEW_REQUIRED",
                "HR has restored employment eligibility, but Identity must approve access reactivation.",
                employee.Id)
            : Allowed(employee.Id);
    }

    private static HrIdentityAccessDecisionDto Allowed(Guid? employeeId = null)
        => new(true, "ACCESS_ALLOWED", "Access is allowed.", employeeId);

    private static HrIdentityAccessDecisionDto Denied(string code, string message, Guid? employeeId = null)
        => new(false, code, message, employeeId);
}
