using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ExchangeRateScheduleLifecycleTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-ExchangeRates")]
    public async Task Approval_should_close_open_predecessor_and_retain_lineage()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var predecessor = Rate(tenantId, new DateTime(2024, 12, 15), RateApprovalStatus.Approved);
        var pending = Rate(tenantId, new DateTime(2026, 9, 1), RateApprovalStatus.Pending);
        db.ExchangeRates.AddRange(predecessor, pending);
        await db.SaveChangesAsync();

        var result = await ExchangeRateScheduleLifecycle.ApproveAsync(
            db, tenantId, pending.Id, approverId, "Approved schedule", new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc), default);

        result.Should().NotBeNull();
        predecessor.EndDate.Should().Be(new DateTime(2026, 8, 31));
        pending.ApprovalStatus.Should().Be(RateApprovalStatus.Approved);
        pending.PreviousRateId.Should().Be(predecessor.Id);
        pending.EndDate.Should().BeNull();
        pending.ApprovedByUserId.Should().Be(approverId);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ExchangeRates")]
    public async Task Approval_should_bound_inserted_rate_before_later_approved_rate()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var predecessor = Rate(
            tenantId,
            new DateTime(2026, 1, 1),
            RateApprovalStatus.Approved,
            new DateTime(2026, 6, 30));
        var pending = Rate(tenantId, new DateTime(2026, 7, 1), RateApprovalStatus.Pending);
        var successor = Rate(tenantId, new DateTime(2026, 9, 1), RateApprovalStatus.Approved);
        successor.PreviousRateId = predecessor.Id;
        db.ExchangeRates.AddRange(predecessor, pending, successor);
        await db.SaveChangesAsync();

        await ExchangeRateScheduleLifecycle.ApproveAsync(
            db, tenantId, pending.Id, Guid.NewGuid(), null, DateTime.UtcNow, default);

        predecessor.EndDate.Should().Be(new DateTime(2026, 6, 30));
        pending.EndDate.Should().Be(new DateTime(2026, 8, 31));
        pending.PreviousRateId.Should().Be(predecessor.Id);
        successor.PreviousRateId.Should().Be(pending.Id);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ExchangeRates")]
    public async Task Rejection_should_leave_approved_schedule_unchanged()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var predecessor = Rate(tenantId, new DateTime(2024, 12, 15), RateApprovalStatus.Approved);
        var pending = Rate(tenantId, new DateTime(2026, 9, 1), RateApprovalStatus.Pending);
        db.ExchangeRates.AddRange(predecessor, pending);
        await db.SaveChangesAsync();

        var rejected = await ExchangeRateScheduleLifecycle.RejectAsync(
            db, tenantId, pending.Id, Guid.NewGuid(), "Provider evidence missing", DateTime.UtcNow, default);

        rejected.Should().BeTrue();
        pending.ApprovalStatus.Should().Be(RateApprovalStatus.Rejected);
        predecessor.EndDate.Should().BeNull();
        predecessor.ApprovalStatus.Should().Be(RateApprovalStatus.Approved);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ExchangeRates")]
    public async Task Approval_should_fail_when_same_start_date_is_already_approved()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var existing = Rate(tenantId, new DateTime(2026, 9, 1), RateApprovalStatus.Approved);
        var pending = Rate(tenantId, new DateTime(2026, 9, 1), RateApprovalStatus.Pending);
        db.ExchangeRates.AddRange(existing, pending);
        await db.SaveChangesAsync();

        var action = () => ExchangeRateScheduleLifecycle.ApproveAsync(
            db, tenantId, pending.Id, Guid.NewGuid(), null, DateTime.UtcNow, default);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already starts on 2026-09-01*");
        pending.ApprovalStatus.Should().Be(RateApprovalStatus.Pending);
        existing.EndDate.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ExchangeRates")]
    public async Task Approval_should_not_read_or_modify_another_tenants_schedule()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var otherTenantRate = Rate(otherTenantId, new DateTime(2024, 1, 1), RateApprovalStatus.Approved);
        var pending = Rate(tenantId, new DateTime(2026, 9, 1), RateApprovalStatus.Pending);
        db.ExchangeRates.AddRange(otherTenantRate, pending);
        await db.SaveChangesAsync();

        await ExchangeRateScheduleLifecycle.ApproveAsync(
            db, tenantId, pending.Id, Guid.NewGuid(), null, DateTime.UtcNow, default);

        otherTenantRate.EndDate.Should().BeNull();
        pending.PreviousRateId.Should().BeNull();
        pending.ApprovalStatus.Should().Be(RateApprovalStatus.Approved);
    }

    private static ExchangeRate Rate(
        Guid tenantId,
        DateTime effectiveDate,
        RateApprovalStatus approvalStatus,
        DateTime? endDate = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = "USD",
            Rate = 12.5m,
            InverseRate = 0.08m,
            EffectiveDate = effectiveDate,
            EndDate = endDate,
            RateType = ExchangeRateType.Daily,
            QuoteSide = ExchangeRateQuoteSide.Mid,
            RateSource = "Bank of Ghana",
            ApprovalStatus = approvalStatus,
            IsActive = true,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = effectiveDate.AddDays(-1)
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"exchange-rate-schedule-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
