using ErpSystem.Core.Interfaces.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Events;

public class AppEventBus : IAppEventBus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppEventBus> _logger;

    public AppEventBus(IServiceScopeFactory scopeFactory, ILogger<AppEventBus> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default) where TEvent : class
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IAppEventHandler<TEvent>>().ToList();
        if (handlers.Count == 0) return;

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(evt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "App event handler failed for {EventType}: {HandlerType}", typeof(TEvent).Name, handler.GetType().Name);
            }
        }
    }
}

