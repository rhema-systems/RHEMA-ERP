using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Sales;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Sales;

public class SalesCanonicalPartnerQueryTests
{
    [Fact]
    public async Task OpportunityQueries_ShouldExecuteWithIgnoredCustomerNavigationAndReturnCanonicalName()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.Model.FindEntityType(typeof(Opportunity))!.FindNavigation(nameof(Opportunity.Customer)).Should().BeNull();
        var service = new OpportunityService(fixture.Repo<Opportunity>(), fixture.Unit.Object, fixture.User,
            NullLogger<OpportunityService>.Instance);

        (await service.GetByIdAsync(fixture.OpportunityId))!.CustomerName.Should().Be("Canonical customer");
        (await service.GetAllAsync()).Items.Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
        (await service.GetPipelineAsync()).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
        (await service.GetByCustomerAsync(fixture.PartnerId)).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
    }

    [Fact]
    public async Task ActivityQueries_ShouldExecuteAndKeepCanonicalCustomerNames()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new ActivityService(fixture.Repo<Activity>(), fixture.Unit.Object, fixture.User,
            NullLogger<ActivityService>.Instance);
        (await service.GetByIdAsync(fixture.ActivityId))!.CustomerName.Should().Be("Canonical customer");
        (await service.GetAllAsync()).Items.Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
        (await service.GetTimelineAsync()).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
        (await service.GetUpcomingAsync()).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
        (await service.GetOverdueAsync()).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
    }

    [Fact]
    public async Task QuoteQueries_ShouldExecuteAndKeepCanonicalCustomerNames()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new QuoteService(fixture.Repo<Quote>(), fixture.Repo<QuoteLineItem>(), Mock.Of<ISalesOrderService>(),
            fixture.Unit.Object, fixture.User, NullLogger<QuoteService>.Instance, Mock.Of<IDocumentNumberingService>());
        (await service.GetByIdAsync(fixture.QuoteId))!.CustomerName.Should().Be("Canonical customer");
        (await service.GetAllAsync()).Items.Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
        (await service.GetByOpportunityAsync(fixture.OpportunityId)).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
        (await service.GetExpiringQuotesAsync()).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
    }

    [Fact]
    public async Task CampaignMembers_ShouldExecuteAndKeepCanonicalCustomerNames()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new CampaignService(fixture.Repo<Campaign>(), fixture.Repo<CampaignMember>(), fixture.Unit.Object,
            fixture.User, NullLogger<CampaignService>.Instance);
        (await service.GetMembersAsync(fixture.CampaignId)).Should().ContainSingle().Which.CustomerName.Should().Be("Canonical customer");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomerNameLookup_ShouldNotExposeAnotherTenantOrDeletedPartner(bool deleted)
    {
        await using var fixture = await Fixture.CreateAsync();
        var partner = await fixture.Db.BusinessPartners.SingleAsync();
        if (deleted) partner.IsDeleted = true;
        else partner.TenantId = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var service = new OpportunityService(fixture.Repo<Opportunity>(), fixture.Unit.Object, fixture.User,
            NullLogger<OpportunityService>.Instance);
        (await service.GetByIdAsync(fixture.OpportunityId))!.CustomerName.Should().BeNull();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid PartnerId { get; } = Guid.NewGuid();
        public Guid OpportunityId { get; } = Guid.NewGuid();
        public Guid ActivityId { get; } = Guid.NewGuid();
        public Guid QuoteId { get; } = Guid.NewGuid();
        public Guid CampaignId { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; }
        public Mock<IUnitOfWork> Unit { get; } = new();
        public ICurrentUserProvider User { get; }
        private Fixture()
        {
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, TenantId);
            User = Mock.Of<ICurrentUserProvider>(user => user.TenantId == TenantId);
            Unit.Setup(unit => unit.Repository<BusinessPartner>()).Returns(Repo<BusinessPartner>());
        }
        public GenericRepository<T> Repo<T>() where T : BaseEntity => new(Db);
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            f.Db.Add(new BusinessPartner { Id = f.PartnerId, TenantId = f.TenantId, PartnerCode = "CANONICAL",
                PartnerName = "Canonical customer", PartnerType = "Customer", IsActive = false });
            f.Db.Add(new Opportunity { Id = f.OpportunityId, TenantId = f.TenantId, CustomerId = f.PartnerId,
                Name = "Pipeline", Stage = "Prospecting", ExpectedCloseDate = DateTime.UtcNow.AddDays(10) });
            f.Db.Add(new Activity { Id = f.ActivityId, TenantId = f.TenantId, CustomerId = f.PartnerId,
                OpportunityId = f.OpportunityId, Subject = "Follow up", ActivityStatus = "Planned", DueDate = DateTime.UtcNow.AddDays(-1) });
            f.Db.Add(new Quote { Id = f.QuoteId, TenantId = f.TenantId, CustomerId = f.PartnerId,
                OpportunityId = f.OpportunityId, DocumentNumber = "Q-1", QuoteName = "Proposal", QuoteStatus = "Sent",
                ValidUntil = DateTime.UtcNow.AddDays(1) });
            f.Db.Add(new Campaign { Id = f.CampaignId, TenantId = f.TenantId, Name = "Campaign" });
            f.Db.Add(new CampaignMember { TenantId = f.TenantId, CampaignId = f.CampaignId, CustomerId = f.PartnerId });
            await f.Db.SaveChangesAsync();
            f.Db.ChangeTracker.Clear();
            return f;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
