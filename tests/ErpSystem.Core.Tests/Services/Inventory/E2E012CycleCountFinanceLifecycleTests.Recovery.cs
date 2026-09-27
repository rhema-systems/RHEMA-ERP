using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed partial class E2E012CycleCountFinanceLifecycleTests
{
    private async Task<Guid> SeedRecoveryEmployeeAsync(Guid userId)
    {
        var employee = new Employee { TenantId = _tenantId, EmployeeNumber = "COUNT-" + userId.ToString("N")[..8],
            FirstName = "Recovery", LastName = "Counter", EmailAddress = "counter@example.test", IsActive = true };
        var user = await _context.Users.SingleAsync(x => x.Id == userId);
        user.EmployeeId = employee.Id;
        await _context.AddRangeAsync(employee, new UserTenant { TenantId = _tenantId, UserId = userId,
            User = user, Status = UserTenantStatus.Active });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return employee.Id;
    }

    private AssignPhysicalCountCountersRequest RecoveryAssignment(Guid employeeId, string key = "recover-committee") => new()
    {
        EmployeeIds = [employeeId], RowVersion = Convert.ToBase64String(_countRowVersion),
        IdempotencyKey = key, Comment = "Independent investigation requires a retained recount sheet."
    };

    private async Task<Guid> SeedLegacyInvestigationAsync(string status = "UnderInvestigation")
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.CycleCount, ABCClass = "A"
        }, _initiatorId);
        var count = await LoadCountAsync(created.Id);
        count.Status = status;
        count.CountedById = _counterId;
        count.ObservationSubmittedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await SetRowVersionsAsync(count.Id);
        return count.Id;
    }

    private async Task RecoverLegacyRecountAndAssertAsync(Guid countId, Guid lineId)
    {
        var employeeId = await SeedRecoveryEmployeeAsync(_counterId);
        _currentUser.Switch(_financeApproverId, "finance", "TDC_FINANCE_REVIEWER");
        var assignment = RecoveryAssignment(employeeId);
        (await _counts.GetByIdAsync(countId))!.CanManageCounters.Should().BeTrue();
        await _counts.AssignCountersAsync(countId, _financeApproverId, assignment);
        await _counts.AssignCountersAsync(countId, _financeApproverId, assignment);
        (await _context.Set<PhysicalCountCounter>().CountAsync(x => x.PhysicalCountId == countId)).Should().Be(1);
        (await _context.Set<Notification>().CountAsync(x => x.EntityId == countId)).Should().Be(2);
        var audit = await _context.Set<PhysicalCountAction>().SingleAsync(x => x.PhysicalCountId == countId && x.ActionType == PhysicalCountActionType.CountersAssigned);
        audit.ActorRole.Should().Be("LegacyCommitteeRecovery");
        audit.Comment.Should().Be(assignment.Comment);
        (await _counts.GetByIdAsync(countId))!.CanManageCounters.Should().BeFalse();
        var request = new CreatePhysicalCountRecountRequest {
            RowVersion = Convert.ToBase64String(_countRowVersion), IdempotencyKey = "legacy-recount",
            Items = [new() { PhysicalCountItemId = lineId, ItemRowVersion = Convert.ToBase64String(_lineRowVersion), Reason = "Confirm the omitted unit." }]
        };
        var child = await _counts.CreateRecountAsync(countId, _financeApproverId, request);
        (await _counts.CreateRecountAsync(countId, _financeApproverId, request)).Id.Should().Be(child.Id);
        await SetRowVersionsAsync(child.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(child.Id, _counterId);
        var childLine = (await LoadCountAsync(child.Id)).Items.Single();
        await SaveQuantityAsync(childLine.Id, 8, "recount-observation");
        var original = await LoadCountAsync(countId);
        original.Items.Single().CountedQuantity.Should().Be(7);
        original.Items.Single().FirstCountQuantity.Should().Be(7);
        original.Items.Single().SupersededByPhysicalCountId.Should().Be(child.Id);
        original.FreezeReleasedAtUtc.Should().BeNull();
        childLine = (await LoadCountAsync(child.Id)).Items.Single();
        childLine.CountedQuantity.Should().Be(8);
        childLine.PredecessorPhysicalCountItemId.Should().Be(lineId);
        var editOriginal = () => SaveQuantityAsync(lineId, 9, "rewrite-original");
        await editOriginal.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("UnderReview")]
    [InlineData("ReadyToPost")]
    public async Task Legacy_recovery_rejects_non_investigation_states(string status)
    {
        var id = await SeedLegacyInvestigationAsync(status);
        var employee = await SeedRecoveryEmployeeAsync(_counterId);
        _currentUser.Switch(_financeApproverId, "finance");
        var action = () => _counts.AssignCountersAsync(id, _financeApproverId, RecoveryAssignment(employee));
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*assigned once*");
        (await _context.Set<PhysicalCountCounter>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Legacy_recovery_requires_independent_actor_and_approval_capability()
    {
        var id = await SeedLegacyInvestigationAsync();
        var employee = await SeedRecoveryEmployeeAsync(_counterId);
        _currentUser.Switch(_counterId, "cycle.counter");
        var self = () => _counts.AssignCountersAsync(id, _counterId, RecoveryAssignment(employee));
        await self.Should().ThrowAsync<InvalidOperationException>().WithMessage("*independent actor*");
        _currentUser.Switch(_financeApproverId, "finance");
        _access.Setup(x => x.EnforceCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(r => r.PermissionCode == "procurement.inventory.adjust.approve"),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new UnauthorizedAccessException("Approval scope denied."));
        var denied = () => _counts.AssignCountersAsync(id, _financeApproverId, RecoveryAssignment(employee));
        await denied.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _context.Set<PhysicalCountCounter>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Legacy_recovery_requires_reason_and_eligible_tenant_employee()
    {
        var id = await SeedLegacyInvestigationAsync();
        _currentUser.Switch(_financeApproverId, "finance");
        var request = RecoveryAssignment(Guid.NewGuid()); request.Comment = " ";
        var missingReason = () => _counts.AssignCountersAsync(id, _financeApproverId, request);
        await missingReason.Should().ThrowAsync<InvalidOperationException>().WithMessage("*investigation reason*");
        request.Comment = "Investigate discrepancy.";
        var invalidEmployee = () => _counts.AssignCountersAsync(id, _financeApproverId, request);
        await invalidEmployee.Should().ThrowAsync<ArgumentException>().WithMessage("*employee is inactive or does not belong*");
        (await _context.Set<PhysicalCountCounter>().CountAsync()).Should().Be(0);
    }
}
