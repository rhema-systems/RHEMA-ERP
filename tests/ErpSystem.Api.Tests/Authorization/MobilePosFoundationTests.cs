using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.MobilePos;
using ErpSystem.Api.Extensions;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ErpSystem.Api.Tests.Authorization;

public sealed class MobilePosFoundationTests
{
    [Fact]
    public void PermissionCatalogue_ShouldExposeUniqueCapabilitiesForDynamicRoleManagement()
    {
        MobilePosPermissions.All.Should().NotBeEmpty();
        MobilePosPermissions.All.Select(item => item.Name).Should().OnlyHaveUniqueItems();
        MobilePosPermissions.AllNames.Should().Equal(MobilePosPermissions.All.Select(item => item.Name));
        MobilePosPermissions.All.Should().OnlyContain(item =>
            item.Name.StartsWith("MobilePOS.", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(item.DisplayName)
            && !string.IsNullOrWhiteSpace(item.Description)
            && item.Category.StartsWith("Mobile POS", StringComparison.Ordinal));
    }

    [Fact]
    public void RegisteredPolicies_ShouldRequireDatabaseBackedPermissionsWithoutRoleNames()
    {
        var services = new ServiceCollection();
        services.AddErpSystemAuthorization();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        foreach (var permission in MobilePosPermissions.All)
        {
            var policy = options.GetPolicy(permission.Name);
            policy.Should().NotBeNull(permission.Name);
            var requirement = policy!.Requirements.Should().ContainSingle().Which
                .Should().BeOfType<PermissionRequirement>().Which;
            if (permission.Name == MobilePosPermissions.ViewStore)
            {
                requirement.Permissions.Should().BeEquivalentTo(
                    MobilePosPermissions.ViewStore,
                    MobilePosPermissions.ManageStore,
                    MobilePosPermissions.ApproveDevice,
                    MobilePosPermissions.ReviewTill);
            }
            else
            {
                requirement.Permissions.Should().Equal(permission.Name);
            }
            policy.Requirements.OfType<RolesAuthorizationRequirement>().Should().BeEmpty();
        }
    }

    [Fact]
    public void RuntimeController_ShouldProtectEveryActionWithAMobilePosPermission()
    {
        var actions = typeof(MobilePosRuntimeController).GetMethods()
            .Where(method => method.DeclaringType == typeof(MobilePosRuntimeController)
                             && method.IsPublic
                             && method.GetCustomAttributes(inherit: true)
                                 .Any(attribute => attribute is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute));

        actions.Should().NotBeEmpty();
        actions.Should().OnlyContain(action => action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Any(attribute => MobilePosPermissions.AllNames.Contains(attribute.Policy)));
    }

    [Fact]
    public void OfflineGrantEndpoint_ShouldRequireOfflineAndTillPermissions()
    {
        var action = typeof(MobilePosRuntimeController).GetMethod(nameof(MobilePosRuntimeController.IssueOfflineGrant));

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().BeEquivalentTo(MobilePosPermissions.UseOffline, MobilePosPermissions.OperateTill);
    }

    [Theory]
    [InlineData(nameof(MobilePosRuntimeController.SearchCustomers))]
    [InlineData(nameof(MobilePosRuntimeController.GetOutstandingInvoices))]
    public void CustomerReadEndpoints_ShouldRequireTheDynamicCustomerViewPermission(string actionName)
    {
        var action = typeof(MobilePosRuntimeController).GetMethod(actionName);

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().ContainSingle(policy => policy == MobilePosPermissions.ViewCustomer);
    }

    [Fact]
    public void CompleteSaleEndpoint_ShouldRequireTillMobileAndUnderlyingFinancePermissions()
    {
        var action = typeof(MobilePosRuntimeController).GetMethod(nameof(MobilePosRuntimeController.CompleteSale));

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().BeEquivalentTo(
                MobilePosPermissions.OperateTill,
                MobilePosPermissions.CreateInvoice,
                MobilePosPermissions.PostInvoice,
                MobilePosPermissions.CollectPayment,
                FinancePermissions.CreateArInvoices,
                FinancePermissions.ApprovePostArInvoices,
                FinancePermissions.ReceiveCustomerPayments);
    }

    [Fact]
    public void CompleteCollectionEndpoint_ShouldRequireTillCollectionAndUnderlyingFinancePermissions()
    {
        var action = typeof(MobilePosRuntimeController).GetMethod(nameof(MobilePosRuntimeController.CompleteCollection));

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().BeEquivalentTo(
                MobilePosPermissions.OperateTill,
                MobilePosPermissions.CollectPayment,
                FinancePermissions.ReceiveCustomerPayments);
    }

    [Fact]
    public void CatalogueAndPreviewEndpoints_ShouldRequireTillAndInvoicePermissions()
    {
        var catalogue = typeof(MobilePosRuntimeController).GetMethod(nameof(MobilePosRuntimeController.SearchCatalogue));
        var preview = typeof(MobilePosRuntimeController).GetMethod(nameof(MobilePosRuntimeController.PreviewSale));

        catalogue.Should().NotBeNull();
        catalogue!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().BeEquivalentTo(MobilePosPermissions.OperateTill, MobilePosPermissions.CreateInvoice);
        preview.Should().NotBeNull();
        preview!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().BeEquivalentTo(
                MobilePosPermissions.OperateTill,
                MobilePosPermissions.CreateInvoice,
                FinancePermissions.CreateArInvoices);
    }

    [Fact]
    public void ReceiptEndpoints_ShouldUseDynamicAccessAndReprintPermissions()
    {
        var getReceipt = typeof(MobilePosRuntimeController).GetMethod(nameof(MobilePosRuntimeController.GetReceipt));
        var reprint = typeof(MobilePosRuntimeController).GetMethod(nameof(MobilePosRuntimeController.RecordReceiptReprint));

        getReceipt.Should().NotBeNull();
        getReceipt!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().ContainSingle(policy => policy == MobilePosPermissions.Access);
        reprint.Should().NotBeNull();
        reprint!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().BeEquivalentTo(MobilePosPermissions.Access, MobilePosPermissions.ReprintReceipt);
    }

    [Fact]
    public void AdministrationController_ShouldProtectEveryActionWithAMobilePosPermission()
    {
        var actions = typeof(MobilePosAdministrationController).GetMethods()
            .Where(method => method.DeclaringType == typeof(MobilePosAdministrationController)
                             && method.IsPublic
                             && method.GetCustomAttributes(inherit: true)
                                 .Any(attribute => attribute is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute));

        actions.Should().NotBeEmpty();
        actions.Should().OnlyContain(action => action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Any(attribute => MobilePosPermissions.AllNames.Contains(attribute.Policy)));
    }

    [Fact]
    public void Model_ShouldEnforceTenantScopedStoreTillAssignmentAndDeviceIdentityKeys()
    {
        using var db = CreateContext();

        AssertUniqueFilteredIndex<MobilePosStore>(db, nameof(MobilePosStore.TenantId), nameof(MobilePosStore.Code));
        AssertUniqueFilteredIndex<MobilePosTill>(db, nameof(MobilePosTill.TenantId), nameof(MobilePosTill.TillNumber));
        AssertUniqueFilteredIndex<MobilePosUserStoreAssignment>(
            db,
            nameof(MobilePosUserStoreAssignment.TenantId),
            nameof(MobilePosUserStoreAssignment.UserId));
        AssertUniqueFilteredIndex<MobilePosDevice>(
            db,
            nameof(MobilePosDevice.TenantId),
            nameof(MobilePosDevice.InstallationIdHash));
    }

    [Fact]
    public void StoreModel_ShouldRequireGovernedWalkInCustomerAndRoleReferences()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(MobilePosStore));

        entity.Should().NotBeNull();
        entity!.FindProperty(nameof(MobilePosStore.DefaultWalkInBusinessPartnerId))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(MobilePosStore.DefaultWalkInBusinessPartnerRoleId))!.IsNullable.Should().BeFalse();
        entity.GetForeignKeys().Should().Contain(key =>
            key.Properties.Single().Name == nameof(MobilePosStore.DefaultWalkInBusinessPartnerId)
            && key.DeleteBehavior == DeleteBehavior.Restrict);
        entity.GetForeignKeys().Should().Contain(key =>
            key.Properties.Single().Name == nameof(MobilePosStore.DefaultWalkInBusinessPartnerRoleId)
            && key.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void TransactionModel_ShouldEnforceDeviceMutationSaleLineAndTenderIdentities()
    {
        using var db = CreateContext();

        AssertUniqueFilteredIndex<MobileMutationReceipt>(db,
            nameof(MobileMutationReceipt.TenantId),
            nameof(MobileMutationReceipt.MobilePosDeviceId),
            nameof(MobileMutationReceipt.ClientMutationId));
        AssertUniqueFilteredIndex<MobilePosSale>(db,
            nameof(MobilePosSale.TenantId),
            nameof(MobilePosSale.MobilePosDeviceId),
            nameof(MobilePosSale.ClientMutationId));
        AssertUniqueFilteredIndex<MobilePosSaleLine>(db,
            nameof(MobilePosSaleLine.TenantId),
            nameof(MobilePosSaleLine.MobilePosSaleId),
            nameof(MobilePosSaleLine.ClientLineId));
        AssertUniqueFilteredIndex<MobilePosTender>(db,
            nameof(MobilePosTender.TenantId),
            nameof(MobilePosTender.MobilePosSaleId),
            nameof(MobilePosTender.Sequence));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mobile-pos-foundation-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void AssertUniqueFilteredIndex<TEntity>(
        ApplicationDbContext db,
        params string[] expectedPropertyNames)
        where TEntity : class
    {
        var entity = db.Model.FindEntityType(typeof(TEntity));
        entity.Should().NotBeNull();
        var index = entity!.GetIndexes().SingleOrDefault(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(expectedPropertyNames));
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().NotBeNullOrWhiteSpace();
    }
}
