using System.Collections.Concurrent;

namespace ErpSystem.Web.Services
{
    public interface IResourceCleanupService
    {
        void RegisterResource(IDisposable resource, string name = "");
        void RegisterAsyncResource(IAsyncDisposable resource, string name = "");
        void RegisterCleanupAction(Func<Task> cleanupAction, string name = "");
        Task CleanupAllAsync(CancellationToken cancellationToken = default);
        Task CleanupResourceAsync(string name, CancellationToken cancellationToken = default);
        void RemoveResource(string name);
    }

    public class ResourceCleanupService : IResourceCleanupService, IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, IDisposable> _disposableResources;
        private readonly ConcurrentDictionary<string, IAsyncDisposable> _asyncDisposableResources;
        private readonly ConcurrentDictionary<string, Func<Task>> _cleanupActions;
        private readonly ILogger<ResourceCleanupService> _logger;
        private readonly object _cleanupLock = new object();
        private bool _isDisposed = false;
        private int _resourceCounter = 0;

        public ResourceCleanupService(ILogger<ResourceCleanupService> logger)
        {
            _logger = logger;
            _disposableResources = new ConcurrentDictionary<string, IDisposable>();
            _asyncDisposableResources = new ConcurrentDictionary<string, IAsyncDisposable>();
            _cleanupActions = new ConcurrentDictionary<string, Func<Task>>();
        }

        public void RegisterResource(IDisposable resource, string name = "")
        {
            if (resource == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                name = $"Resource_{Interlocked.Increment(ref _resourceCounter)}";
            }

            _disposableResources.TryAdd(name, resource);
            _logger.LogDebug("Registered disposable resource: {ResourceName} ({ResourceType})",
                name, resource.GetType().Name);
        }

        public void RegisterAsyncResource(IAsyncDisposable resource, string name = "")
        {
            if (resource == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                name = $"AsyncResource_{Interlocked.Increment(ref _resourceCounter)}";
            }

            _asyncDisposableResources.TryAdd(name, resource);
            _logger.LogDebug("Registered async disposable resource: {ResourceName} ({ResourceType})",
                name, resource.GetType().Name);
        }

        public void RegisterCleanupAction(Func<Task> cleanupAction, string name = "")
        {
            if (cleanupAction == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                name = $"CleanupAction_{Interlocked.Increment(ref _resourceCounter)}";
            }

            _cleanupActions.TryAdd(name, cleanupAction);
            _logger.LogDebug("Registered cleanup action: {ActionName}", name);
        }

        public async Task CleanupAllAsync(CancellationToken cancellationToken = default)
        {
            lock (_cleanupLock)
            {
                if (_isDisposed)
                {
                    _logger.LogDebug("ResourceCleanupService already disposed, skipping cleanup");
                    return;
                }
                _isDisposed = true;
            }

            _logger.LogInformation("Starting cleanup of all registered resources...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var tasks = new List<Task>();
            var totalResources = _disposableResources.Count + _asyncDisposableResources.Count + _cleanupActions.Count;

            try
            {
                // Cleanup custom actions first
                foreach (var kvp in _cleanupActions)
                {
                    tasks.Add(CleanupActionSafely(kvp.Key, kvp.Value, cancellationToken));
                }

                // Cleanup async disposable resources
                foreach (var kvp in _asyncDisposableResources)
                {
                    tasks.Add(CleanupAsyncResourceSafely(kvp.Key, kvp.Value, cancellationToken));
                }

                // Cleanup synchronous disposable resources
                foreach (var kvp in _disposableResources)
                {
                    tasks.Add(Task.Run(() => CleanupResourceSafely(kvp.Key, kvp.Value), cancellationToken));
                }

                await Task.WhenAll(tasks);

                stopwatch.Stop();
                _logger.LogInformation("Completed cleanup of {TotalResources} resources in {ElapsedMs}ms",
                    totalResources, stopwatch.ElapsedMilliseconds);

                // Clear all collections
                _cleanupActions.Clear();
                _asyncDisposableResources.Clear();
                _disposableResources.Clear();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error during resource cleanup after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task CleanupResourceAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Cannot cleanup resource with empty name");
                return;
            }

            var cleanedUp = false;

            // Try cleanup action first
            if (_cleanupActions.TryRemove(name, out var cleanupAction))
            {
                await CleanupActionSafely(name, cleanupAction, cancellationToken);
                cleanedUp = true;
            }

            // Try async disposable resource
            if (_asyncDisposableResources.TryRemove(name, out var asyncResource))
            {
                await CleanupAsyncResourceSafely(name, asyncResource, cancellationToken);
                cleanedUp = true;
            }

            // Try disposable resource
            if (_disposableResources.TryRemove(name, out var resource))
            {
                CleanupResourceSafely(name, resource);
                cleanedUp = true;
            }

            if (!cleanedUp)
            {
                _logger.LogWarning("Resource '{ResourceName}' not found for cleanup", name);
            }
        }

        public void RemoveResource(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var removed = false;
            removed |= _cleanupActions.TryRemove(name, out _);
            removed |= _asyncDisposableResources.TryRemove(name, out _);
            removed |= _disposableResources.TryRemove(name, out _);

            if (removed)
            {
                _logger.LogDebug("Removed resource '{ResourceName}' from cleanup registry", name);
            }
            else
            {
                _logger.LogWarning("Resource '{ResourceName}' not found for removal", name);
            }
        }

        private async Task CleanupActionSafely(string name, Func<Task> cleanupAction, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Executing cleanup action: {ActionName}", name);
                await cleanupAction();
                _logger.LogDebug("Completed cleanup action: {ActionName}", name);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Cleanup action '{ActionName}' was cancelled", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing cleanup action '{ActionName}'", name);
            }
        }

        private async Task CleanupAsyncResourceSafely(string name, IAsyncDisposable resource, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Disposing async resource: {ResourceName} ({ResourceType})",
                    name, resource.GetType().Name);

                await resource.DisposeAsync();

                _logger.LogDebug("Completed disposing async resource: {ResourceName}", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing async resource '{ResourceName}' ({ResourceType})",
                    name, resource.GetType().Name);
            }
        }

        private void CleanupResourceSafely(string name, IDisposable resource)
        {
            try
            {
                _logger.LogDebug("Disposing resource: {ResourceName} ({ResourceType})",
                    name, resource.GetType().Name);

                resource.Dispose();

                _logger.LogDebug("Completed disposing resource: {ResourceName}", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing resource '{ResourceName}' ({ResourceType})",
                    name, resource.GetType().Name);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogDebug("Synchronous dispose called on ResourceCleanupService");

            // Dispose synchronous resources only
            foreach (var kvp in _disposableResources)
            {
                CleanupResourceSafely(kvp.Key, kvp.Value);
            }

            _disposableResources.Clear();
            _isDisposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _logger.LogDebug("Async dispose called on ResourceCleanupService");
            await CleanupAllAsync();
        }
    }

    public static class ResourceCleanupServiceExtensions
    {
        public static void RegisterForCleanup(this IServiceCollection services)
        {
            services.AddSingleton<IResourceCleanupService, ResourceCleanupService>();
        }

        public static T RegisterWithCleanup<T>(this T resource, IResourceCleanupService cleanupService, string name = "")
            where T : IDisposable
        {
            cleanupService.RegisterResource(resource, name);
            return resource;
        }

        public static T RegisterWithAsyncCleanup<T>(this T resource, IResourceCleanupService cleanupService, string name = "")
            where T : IAsyncDisposable
        {
            cleanupService.RegisterAsyncResource(resource, name);
            return resource;
        }
    }
}
