using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Services.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class AppEventBusScopeTests
{
    [Fact]
    public async Task Publish_UsesTheCallersScopeForTransactionalHandlers()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();
        services.AddScoped<IAppEventHandler<ProbeEvent>, ProbeHandler>();
        services.AddScoped<IAppEventBus>(provider =>
            new AppEventBus(provider, NullLogger<AppEventBus>.Instance));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var marker = scope.ServiceProvider.GetRequiredService<ScopeMarker>();
        var bus = scope.ServiceProvider.GetRequiredService<IAppEventBus>();

        var probe = new ProbeEvent();
        await bus.PublishAsync(probe);

        Assert.Same(marker, probe.HandlerScope);
    }

    private sealed class ProbeEvent
    {
        public ScopeMarker? HandlerScope { get; set; }
    }

    private sealed class ScopeMarker;

    private sealed class ProbeHandler(ScopeMarker marker) : IAppEventHandler<ProbeEvent>
    {
        public Task HandleAsync(ProbeEvent evt, CancellationToken cancellationToken = default)
        {
            evt.HandlerScope = marker;
            return Task.CompletedTask;
        }
    }
}
