using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceDimensionAssignmentStoreTests
{
    [Fact]
    public async Task PersistsOneSharedHeaderDefaultAndIndependentAuthoritativeLines()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var set = DimensionSet(tenantId, "SET-A");
        db.FinanceDimensionSets.Add(set);
        await db.SaveChangesAsync();
        var store = CreateStore(db, tenantId);
        var documentId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var producer = new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceApVendorInvoice);

        var header = await store.UpsertAsync(producer, documentId, null, set.Id);
        var line = await store.UpsertAsync(producer, documentId, lineId, set.Id);
        var assignments = await store.GetDocumentAssignmentsAsync(producer, documentId);

        assignments.Should().HaveCount(2);
        header.SourceLineId.Should().BeNull();
        line.SourceLineId.Should().Be(lineId);
        assignments.Should().OnlyContain(item =>
            item.ProducerModule == "Finance"
            && item.SourceRoute == "finance.ap.vendor-invoices.manual"
            && item.SourceDocumentType == "VendorInvoice"
            && item.ContractVersion == "1.0");
    }

    [Fact]
    public async Task RejectsCrossTenantSetsAndHeaderSnapshots()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var foreignSet = DimensionSet(Guid.NewGuid(), "FOREIGN");
        db.FinanceDimensionSets.Add(foreignSet);
        await db.SaveChangesAsync();
        var store = CreateStore(db, tenantId);
        var producer = new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerInvoice);

        var crossTenant = () => store.UpsertAsync(producer, Guid.NewGuid(), Guid.NewGuid(), foreignSet.Id);
        await crossTenant.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*for this tenant*");

        var headerSnapshot = () => store.UpsertAsync(
            producer, Guid.NewGuid(), null, foreignSet.Id, Guid.NewGuid());
        await headerSnapshot.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*document default cannot be frozen*");
    }

    [Fact]
    public async Task FrozenLineEvidenceCannotBeChangedOrCleared()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var set = DimensionSet(tenantId, "SET-A");
        var otherSet = DimensionSet(tenantId, "SET-B");
        var route = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceApSupplierDebitNote);
        var snapshot = new FinanceDimensionSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceDimensionSetId = set.Id,
            CombinationHashSnapshot = set.CombinationHash,
            DisplayValueSnapshot = set.DisplayValue,
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            SourceDocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion
        };
        db.FinanceDimensionSets.AddRange(set, otherSet);
        db.FinanceDimensionSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        var store = CreateStore(db, tenantId);
        var producer = new FinancePostingProducerContext(route.Id);
        var documentId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        var frozen = await store.UpsertAsync(producer, documentId, lineId, set.Id, snapshot.Id);

        frozen.IsFrozen.Should().BeTrue();
        var change = () => store.UpsertAsync(producer, documentId, lineId, otherSet.Id);
        await change.Should().ThrowAsync<InvalidOperationException>().WithMessage("*immutable*");
        var clear = () => store.ClearAsync(producer, documentId, lineId);
        await clear.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be cleared*");
    }

    private static FinanceDimensionSet DimensionSet(Guid tenantId, string marker) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CombinationHash = marker.PadRight(64, '0'),
        DisplayValue = marker
    };

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-source-dimension-store-{Guid.NewGuid():N}").Options);

    private static FinanceSourceDimensionAssignmentStore CreateStore(
        ApplicationDbContext db,
        Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(user => user.UserName).Returns("finance.source.adapter");
        currentUser.SetupGet(user => user.Claims).Returns(new Dictionary<string, string>());
        return new FinanceSourceDimensionAssignmentStore(db, currentUser.Object);
    }
}
