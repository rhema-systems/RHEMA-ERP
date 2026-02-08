namespace ErpSystem.Core.Interfaces.Events;

public interface IAppEventBus
{
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default) where TEvent : class;
}

