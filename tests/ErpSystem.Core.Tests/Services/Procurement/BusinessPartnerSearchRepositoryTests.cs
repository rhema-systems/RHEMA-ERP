using System.Data.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerSearchRepositoryTests
{
    [Theory]
    [InlineData("  HARBOURLINE  ")]
    [InlineData("sup-mixed")]
    [InlineData("CONTACT@HARBOUR.EXAMPLE")]
    [InlineData("reg-mixed")]
    public async Task SearchIsCaseInsensitiveAndRetainsTenantAndDeletedFilters(string search)
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var expected = Partner(tenantId);
        var otherTenant = Partner(Guid.NewGuid());
        var deleted = Partner(tenantId);
        deleted.IsDeleted = true;
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.BusinessPartners.AddRange(expected, otherTenant, deleted);
            await seed.SaveChangesAsync();
        }

        await using var context = new ApplicationDbContext(options, tenantId);
        var repository = Repository(context, tenantId);

        var result = await repository.GetPartnersAsync(1, 25, search);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(expected.Id);
    }

    [Fact]
    public async Task SearchTranslatesWithSqlServerBeforeAnyConnectionIsOpened()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(new StopBeforeConnectionInterceptor())
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        var repository = Repository(context, tenantId);

        // EF translates the actual repository query before requesting a connection.
        // Stop there so this regression test never contacts or changes a database.
        Func<Task> search = async () => await repository.GetPartnersAsync(1, 25, "Harbourline");

        await search.Should().ThrowAsync<TranslationCompletedException>();
    }

    private static BusinessPartnerRepository Repository(ApplicationDbContext context, Guid tenantId)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        return new BusinessPartnerRepository(context, currentUser.Object,
            NullLogger<BusinessPartnerRepository>.Instance);
    }

    private static BusinessPartner Partner(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PartnerName = "Harbourline Goods Supply Ltd",
        PartnerCode = "SUP-MiXeD",
        PrimaryEmail = "contact@harbour.example",
        BusinessRegistrationNumber = "REG-MiXeD",
        PartnerType = "Supplier"
    };

    private sealed class TranslationCompletedException : Exception
    {
    }

    private sealed class StopBeforeConnectionInterceptor : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            throw new TranslationCompletedException();
    }
}
