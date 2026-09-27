using ErpSystem.Api.Controllers.Sales;
using ErpSystem.Api.Extensions;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Sales;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class SalesDeliveryDependencyTests
{
    [Fact]
    public void ProductionRegistrations_ActivateDeliveryControllerWithScopedOwner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddErpSystemRepositories();
        services.AddErpSystemServices();

        // Check the actual composition root, not a separately recreated service registration.
        var owner = Assert.Single(services.Where(item => item.ServiceType == typeof(IDeliveryService)));
        Assert.Equal(typeof(DeliveryService), owner.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, owner.Lifetime);
        foreach (var parameter in typeof(DeliveryService).GetConstructors().Single().GetParameters())
        {
            Assert.Contains(services, item => item.ServiceType == parameter.ParameterType ||
                (parameter.ParameterType.IsGenericType &&
                 item.ServiceType == parameter.ParameterType.GetGenericTypeDefinition()));
        }

        // No database or authenticated request is needed to validate controller activation.
        services.AddScoped(_ => Mock.Of<IGenericRepository<DeliveryNote>>());
        services.AddScoped(_ => Mock.Of<IGenericRepository<DeliveryNoteLine>>());
        services.AddScoped(_ => Mock.Of<IGenericRepository<SalesOrder>>());
        services.AddScoped(_ => Mock.Of<IGenericRepository<SalesOrderLine>>());
        services.AddScoped(_ => Mock.Of<IGenericRepository<SalesOrderStatusHistory>>());
        services.AddScoped(_ => Mock.Of<IUnitOfWork>());
        services.AddScoped(_ => Mock.Of<ICurrentUserProvider>());
        services.AddScoped(_ => Mock.Of<IDocumentNumberingService>());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.NotNull(ActivatorUtilities.CreateInstance<DeliveryController>(first.ServiceProvider));
        var firstOwner = Assert.IsType<DeliveryService>(first.ServiceProvider.GetRequiredService<IDeliveryService>());
        Assert.Same(firstOwner, first.ServiceProvider.GetRequiredService<IDeliveryService>());
        Assert.NotSame(firstOwner, second.ServiceProvider.GetRequiredService<IDeliveryService>());
    }
}
