using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class PurchaseRequisitionRepositoryNumberingTests
{
    [Fact]
    public async Task GeneratorIncludesSoftDeletedNumbersButExcludesOtherTenants()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        // Use the runtime tenant abstraction exercised by the repository. Supplying a
        // fixed tenant to the context captures it in EF's shared model cache and can
        // leak this test's random tenant into later in-process test contexts.
        await using var context = new ApplicationDbContext(options);

        context.PurchaseRequisitions.AddRange(
            NewRequisition(tenantId, "PR-2026-0009", isDeleted: true),
            NewRequisition(Guid.NewGuid(), "PR-2026-0099", isDeleted: false));
        await context.SaveChangesAsync();

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(x => x.GetCurrentTenantId()).Returns(tenantId);
        var repository = new PurchaseRequisitionRepository(
            context,
            Mock.Of<IProcurementSettingsRepository>(),
            tenantContext.Object);

        var number = await repository.GenerateRequisitionNumberAsync();

        number.Should().Be("PR-2026-0010");
    }

    private static PurchaseRequisition NewRequisition(Guid tenantId, string number, bool isDeleted) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        RequisitionNumber = number,
        RequestedById = Guid.NewGuid(),
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
