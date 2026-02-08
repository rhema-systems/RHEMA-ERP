namespace ErpSystem.Core.Interfaces.Events;

public interface IAppEventHandler<in TEvent> where TEvent : class
{
    Task HandleAsync(TEvent evt, CancellationToken cancellationToken = default);
}

