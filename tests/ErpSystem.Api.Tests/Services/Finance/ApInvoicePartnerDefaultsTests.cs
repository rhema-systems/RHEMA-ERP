using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ApInvoicePartnerDefaultsTests
{
    [Fact]
    public async Task DefaultsUseApprovedEffectiveProfileAndNeverLegacyControlOrChargeAccounts()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var result = await f.Service.GetSupplierDefaultsAsync(f.Partner.Id, invoiceDate: new DateTime(2026,7,5));
        result!.BusinessPartnerId.Should().Be(f.Partner.Id);
        result.PostingDefaults.DefaultExpenseAccountId.Should().Be(f.Profile.DefaultExpenseAccountId);
        result.PostingDefaults.DefaultTaxGroupId.Should().Be(f.Profile.DefaultTaxGroupId);
        result.PostingDefaults.DefaultApAccountId.Should().BeNull();
        result.PostingDefaults.DefaultFreightAccountId.Should().BeNull();
        result.PostingDefaults.DefaultTaxAccountId.Should().BeNull();
        result.PostingDefaults.DefaultCashAccountId.Should().BeNull();
        (await f.Db.Suppliers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AccountingDateSelectsEffectiveVersionInsteadOfLatestMaster()
    {
        await using var f = new Fixture();
        f.Profile.EffectiveTo = new DateTime(2026,12,31);
        var later = new BusinessPartnerApProfileVersion { TenantId=f.Tenant, BusinessPartnerRoleId=f.Role.Id,
            VersionNumber=2, Status=BusinessPartnerFinanceProfileStatus.Approved, EffectiveFrom=new DateTime(2027,1,1),
            DefaultExpenseAccountId=Guid.NewGuid() };
        f.Db.Set<BusinessPartnerApProfileVersion>().Add(later); await f.SeedAsync();
        (await f.Service.GetSupplierDefaultsAsync(f.Partner.Id, invoiceDate:new DateTime(2026,7,5)))!.PostingDefaults.DefaultExpenseAccountId.Should().Be(f.Profile.DefaultExpenseAccountId);
        (await f.Service.GetSupplierDefaultsAsync(f.Partner.Id, invoiceDate:new DateTime(2027,1,2)))!.PostingDefaults.DefaultExpenseAccountId.Should().Be(later.DefaultExpenseAccountId);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task MissingApprovedProfileOrInactiveRoleBlocksDefaultResolution(bool inactiveRole)
    {
        await using var f = new Fixture();
        if(inactiveRole) f.Role.Status=BusinessPartnerRoleStatus.Inactive; else f.Profile.Status=BusinessPartnerFinanceProfileStatus.Draft;
        await f.SeedAsync();
        await ((Func<Task>)(()=>f.Service.GetSupplierDefaultsAsync(f.Partner.Id,invoiceDate:new DateTime(2026,7,5))))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExplicitRoleResolvesPartnerWithBothApRoles()
    {
        await using var f = new Fixture();
        f.Db.Set<BusinessPartnerRole>().Add(new BusinessPartnerRole { TenantId=f.Tenant,
            BusinessPartnerId=f.Partner.Id,RoleType=BusinessPartnerRoleType.Contractor,
            Status=BusinessPartnerRoleStatus.Active,ActiveFromUtc=new DateTime(2020,1,1) });
        await f.SeedAsync();
        var result=await f.Service.GetSupplierDefaultsAsync(f.Partner.Id,invoiceDate:new DateTime(2026,7,5),businessPartnerRoleId:f.Role.Id);
        result!.PostingDefaults.DefaultExpenseAccountId.Should().Be(f.Profile.DefaultExpenseAccountId);
    }

    [Fact]
    public async Task LegacySupplierIdCannotBecomeCanonicalIdentity()
    {
        await using var f = new Fixture(); await f.SeedAsync();
        var legacy=new Supplier { TenantId=f.Tenant, SupplierCode=f.Partner.PartnerCode, Name=f.Partner.PartnerName, IsActive=true, Status="Active" };
        f.Db.Suppliers.Add(legacy); await f.Db.SaveChangesAsync();
        await ((Func<Task>)(()=>f.Service.GetSupplierDefaultsAsync(legacy.Id,invoiceDate:new DateTime(2026,7,5))))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task PurchaseOrderForAnotherPartnerCannotSupplyDefaults()
    {
        await using var f = new Fixture();
        var order=new PurchaseOrder { TenantId=f.Tenant, BusinessPartnerId=Guid.NewGuid(), OrderNumber="OTHER" };
        f.Db.PurchaseOrders.Add(order); await f.SeedAsync();
        await ((Func<Task>)(()=>f.Service.GetSupplierDefaultsAsync(f.Partner.Id,order.Id,invoiceDate:new DateTime(2026,7,5))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*different Business Partner*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant {get;}=Guid.NewGuid();
        public ApplicationDbContext Db {get;}=new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public BusinessPartner Partner {get;}
        public BusinessPartnerRole Role {get;}
        public BusinessPartnerApProfileVersion Profile {get;}
        public VendorInvoiceService Service {get;}
        public Fixture()
        {
            Partner=new BusinessPartner { TenantId=Tenant,PartnerCode="BP",PartnerName="Governed partner",IsActive=true,RegistrationStatus="Approved",
                DefaultExpenseAccountId=Guid.NewGuid(),DefaultApAccountId=Guid.NewGuid(),DefaultFreightAccountId=Guid.NewGuid(),
                DefaultTaxAccountId=Guid.NewGuid(),DefaultCashAccountId=Guid.NewGuid() };
            Role=new BusinessPartnerRole { TenantId=Tenant,BusinessPartnerId=Partner.Id,BusinessPartner=Partner,
                RoleType=BusinessPartnerRoleType.Supplier,Status=BusinessPartnerRoleStatus.Active,ActiveFromUtc=new DateTime(2020,1,1) };
            Profile=new BusinessPartnerApProfileVersion { TenantId=Tenant,BusinessPartnerRoleId=Role.Id,BusinessPartnerRole=Role,
                VersionNumber=1,Status=BusinessPartnerFinanceProfileStatus.Approved,EffectiveFrom=new DateTime(2020,1,1),
                DefaultExpenseAccountId=Guid.NewGuid(),DefaultTaxGroupId=Guid.NewGuid() };
            var user=new Mock<ICurrentUserService>(); user.SetupGet(x=>x.TenantId).Returns(Tenant);
            Service=new VendorInvoiceService(new UnitOfWork(Db),user.Object,Mock.Of<IInventoryValuationService>(),
                NullLogger<VendorInvoiceService>.Instance,Mock.Of<IDocumentNumberingService>(),Mock.Of<IWorkflowService>());
        }
        public async Task SeedAsync() { Db.BusinessPartners.Add(Partner);Db.Set<BusinessPartnerRole>().Add(Role);Db.Set<BusinessPartnerApProfileVersion>().Add(Profile);await Db.SaveChangesAsync(); }
        public ValueTask DisposeAsync()=>Db.DisposeAsync();
    }
}
