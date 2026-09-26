using ErpSystem.Core.Interfaces.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Events;

public class AppEventBus : IAppEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppEventBus> _logger;

    public AppEventBus(IServiceProvider serviceProvider, ILogger<AppEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default) where TEvent : class
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        // AppEventBus is scoped. Resolve handlers from that same scope so event/outbox writes
        // participate in the caller's unit of work and explicit transaction. Creating a child
        // scope here gives handlers a second DbContext; when a workflow publishes before its
        // transaction commits, that context can wait on the caller's locks while the caller
        // waits for the handler, producing a self-deadlock.
        var handlers = _serviceProvider.GetServices<IAppEventHandler<TEvent>>().ToList();
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

