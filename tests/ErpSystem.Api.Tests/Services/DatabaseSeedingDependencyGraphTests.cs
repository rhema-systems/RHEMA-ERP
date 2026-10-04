using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class DatabaseSeedingDependencyGraphTests
{
    private static readonly Guid DefaultTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void ProductionSeedingRegistrations_ShouldResolveEveryRegisteredSeederWithScopedFinanceBoundary()
    {
        var services = CreateProductionSeedingServices();
        var registeredSeederTypes = services
            .Where(descriptor =>
                descriptor.ServiceType.IsClass &&
                descriptor.ServiceType.Namespace == typeof(ProcurementSupplierOnboardingTestSeeder).Namespace &&
                descriptor.ServiceType.Name.EndsWith("Seeder", StringComparison.Ordinal))
            .Select(descriptor => descriptor.ServiceType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        registeredSeederTypes.Should().HaveCount(12);
        registeredSeederTypes.Should().Contain(typeof(PaymentTermBaselineSeeder));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        foreach (var seederType in registeredSeederTypes)
        {
            scope.ServiceProvider.GetRequiredService(seederType).Should().BeOfType(seederType);
        }

        var provisioner = scope.ServiceProvider.GetRequiredService<IFinanceAccountProvisioningService>();
        provisioner.Should().BeOfType<FinanceAccountProvisioningService>();
        scope.ServiceProvider.GetRequiredService<IFinanceAccountProvisioningService>()
            .Should().BeSameAs(provisioner, "Finance provisioning and its DbContext are scoped together");

        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        var currentUserProvider = scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>();
        currentUser.Should().BeSameAs(currentUserProvider);
        currentUser.TenantId.Should().Be(DefaultTenantId);
        currentUser.Claims[ErpSystem.Shared.Constants.Claims.TenantId]
            .Should().Be(DefaultTenantId.ToString());

        using var secondScope = provider.CreateScope();
        secondScope.ServiceProvider.GetRequiredService<IFinanceAccountProvisioningService>()
            .Should().NotBeSameAs(provisioner, "provisioning must never capture a scoped DbContext across seed scopes");
    }

    [Fact]
    public void ProductionSeedingRegistrations_ShouldNotOverrideHostCurrentUserOrFinanceProvisioner()
    {
        var services = new ServiceCollection();
        var currentUser = Mock.Of<ICurrentUserService>();
        var currentUserProvider = Mock.Of<ICurrentUserProvider>();
        var provisioner = Mock.Of<IFinanceAccountProvisioningService>();
        services.AddScoped(_ => currentUser);
        services.AddScoped(_ => currentUserProvider);
        services.AddScoped(_ => provisioner);

        services.AddDatabaseSeeding();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserService>().Should().BeSameAs(currentUser);
        scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>().Should().BeSameAs(currentUserProvider);
        scope.ServiceProvider.GetRequiredService<IFinanceAccountProvisioningService>().Should().BeSameAs(provisioner);
    }

    [Fact]
    public void SupplierOnboardingCommandBoundary_ShouldResolveWithoutUnrelatedIdentityOrWebServices()
    {
        var services = CreateBoundaryServices();
        services.AddDatabaseSeedingFinanceBoundary();
        services.AddScoped<ProcurementSupplierOnboardingTestSeeder>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ProcurementSupplierOnboardingTestSeeder>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IFinanceAccountProvisioningService>()
            .Should().BeOfType<FinanceAccountProvisioningService>();
    }

    [Fact]
    public void SupplierOnboardingSeeder_ShouldRetainFinanceOwnedProvisioningBoundary()
    {
        var constructor = typeof(ProcurementSupplierOnboardingTestSeeder).GetConstructors().Single();
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Contain(typeof(IFinanceAccountProvisioningService));

        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ErpSystem.Data", "Seeders", "ProcurementSupplierOnboardingTestSeeder.cs"));
        var source = File.ReadAllText(sourcePath);
        source.Should().Contain("_financeAccountProvisioning.ProvisionAsync");
        source.Should().NotContain("_context.Accounts");
        source.Should().NotContain("new Account");
        source.Should().NotContain("AccountAccountingBooks.Add");
    }

    private static ServiceCollection CreateProductionSeedingServices()
    {
        var services = CreateBoundaryServices();
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddDatabaseSeeding();
        return services;
    }

    private static ServiceCollection CreateBoundaryServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Mock.Of<IWebHostEnvironment>(environment =>
            environment.EnvironmentName == "Development"));
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"database-seeding-di-{Guid.NewGuid():N}"));
        return services;
    }
}
