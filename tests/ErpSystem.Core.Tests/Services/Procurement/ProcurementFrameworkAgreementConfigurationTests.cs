using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementFrameworkAgreementConfigurationTests
{
    [Fact]
    public void AgreementNumberUniquenessIncludesRevisionVersion()
    {
        using var db = Database();

        var index = FindUniqueIndex<ProcurementFrameworkAgreement>(
            db,
            nameof(ProcurementFrameworkAgreement.TenantId),
            nameof(ProcurementFrameworkAgreement.AgreementNumber),
            nameof(ProcurementFrameworkAgreement.Version));

        index.GetFilter().Should().BeNull();
    }

    [Fact]
    public void ReplaceableAgreementChildrenIgnoreSoftDeletedRowsInUniqueIndexes()
    {
        using var db = Database();

        FindUniqueIndex<ProcurementFrameworkAgreementCategory>(
                db,
                nameof(ProcurementFrameworkAgreementCategory.TenantId),
                nameof(ProcurementFrameworkAgreementCategory.AgreementId),
                nameof(ProcurementFrameworkAgreementCategory.PartnerCategoryId))
            .GetFilter()
            .Should()
            .Be("[IsDeleted] = 0");

        FindUniqueIndex<ProcurementFrameworkPriceListLine>(
                db,
                nameof(ProcurementFrameworkPriceListLine.TenantId),
                nameof(ProcurementFrameworkPriceListLine.AgreementId),
                nameof(ProcurementFrameworkPriceListLine.InventoryItemId))
            .GetFilter()
            .Should()
            .Be("[IsDeleted] = 0");

        FindUniqueIndex<ProcurementFrameworkCallOffAuthority>(
                db,
                nameof(ProcurementFrameworkCallOffAuthority.TenantId),
                nameof(ProcurementFrameworkCallOffAuthority.AgreementId),
                nameof(ProcurementFrameworkCallOffAuthority.AuthorityKind),
                nameof(ProcurementFrameworkCallOffAuthority.AuthorityValue))
            .GetFilter()
            .Should()
            .Be("[IsDeleted] = 0");
    }

    [Fact]
    public void CorrectiveMigrationRebuildsRevisionAndActiveChildIndexes()
    {
        var migration = new TDC0401FrameworkRevisionIndexes();
        var createIndexes = migration.UpOperations
            .OfType<CreateIndexOperation>()
            .ToDictionary(operation => operation.Name);

        createIndexes[
                "IX_ProcurementFrameworkAgreements_TenantId_AgreementNumber_Version"]
            .Columns
            .Should()
            .Equal("TenantId", "AgreementNumber", "Version");

        foreach (var indexName in new[]
                 {
                     "IX_ProcurementFrameworkAgreementCategories_TenantId_AgreementId_PartnerCategoryId",
                     "IX_ProcurementFrameworkPriceListLines_TenantId_AgreementId_InventoryItemId",
                     "IX_ProcurementFrameworkCallOffAuthorities_TenantId_AgreementId_AuthorityKind_AuthorityValue"
                 })
        {
            createIndexes[indexName].IsUnique.Should().BeTrue();
            createIndexes[indexName].Filter.Should().Be("[IsDeleted] = 0");
        }
    }

    private static IIndex FindUniqueIndex<TEntity>(
        ApplicationDbContext db,
        params string[] propertyNames)
    {
        var entity = db.Model.FindEntityType(typeof(TEntity));
        entity.Should().NotBeNull();

        var index = entity!.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual(propertyNames));

        index.IsUnique.Should().BeTrue();
        return index;
    }

    private static ApplicationDbContext Database()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
