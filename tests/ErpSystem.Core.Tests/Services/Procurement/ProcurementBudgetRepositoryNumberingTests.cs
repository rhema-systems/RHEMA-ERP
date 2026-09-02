using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementBudgetRepositoryNumberingTests
{
    [Fact]
    public async Task GeneratorUsesTenantWideSequenceAndDoesNotReuseSoftDeletedCodes()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var firstDepartmentId = Guid.NewGuid();
        var secondDepartmentId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.ProcurementBudgets.AddRange(
            NewBudget(tenantId, firstDepartmentId, "PB-2026-0001"),
            NewBudget(tenantId, secondDepartmentId, "PB-2026-0007"),
            NewBudget(tenantId, firstDepartmentId, "PB-2026-0009", isDeleted: true),
            NewBudget(otherTenantId, firstDepartmentId, "PB-2026-0099"));
        await context.SaveChangesAsync();

        var repository = new ProcurementBudgetRepository(context);

        var code = await repository.GenerateBudgetCodeAsync(2026, tenantId);

        code.Should().Be("PB-2026-0010");
    }

    private static ProcurementBudget NewBudget(
        Guid tenantId,
        Guid departmentId,
        string code,
        bool isDeleted = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        DepartmentId = departmentId,
        BudgetCode = code,
        Title = code,
        FiscalYear = 2026,
        AllocatedAmount = 1000m,
        Currency = "GHS",
        Status = "Draft",
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow
    };
}
