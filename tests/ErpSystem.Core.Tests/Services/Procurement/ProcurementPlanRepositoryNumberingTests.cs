using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPlanRepositoryNumberingTests
{
    [Fact]
    public async Task GeneratorAdvancesPastSoftDeletedAndCrossTenantPlanNumbers()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.ProcurementPlans.AddRange(
            NewPlan(tenantId, departmentId, "PP-2026-0001"),
            NewPlan(tenantId, departmentId, "PP-2026-0009", isDeleted: true),
            NewPlan(otherTenantId, departmentId, "PP-2026-0012"),
            NewPlan(tenantId, departmentId, "PP-2026-LEGACY"));
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        var repository = new ProcurementPlanRepository(context, currentUser.Object);

        var planNumber = await repository.GeneratePlanNumberAsync(2026);

        planNumber.Should().Be("PP-2026-0013");
    }

    private static ProcurementPlan NewPlan(
        Guid tenantId,
        Guid departmentId,
        string planNumber,
        bool isDeleted = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        DepartmentId = departmentId,
        FiscalYear = 2026,
        PlanNumber = planNumber,
        Title = planNumber,
        PlanStartDate = new DateTime(2026, 1, 1),
        PlanEndDate = new DateTime(2026, 12, 31),
        Currency = "GHS",
        Status = "Draft",
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow
    };
}
