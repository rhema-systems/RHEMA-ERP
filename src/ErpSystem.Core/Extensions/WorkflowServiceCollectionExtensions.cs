using System.Reflection;
using ErpSystem.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ErpSystem.Core.Extensions;

public static class WorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowStatusAdaptersFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var adapterTypes = assemblies
            .Where(assembly => assembly != null)
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                typeof(IWorkflowStatusAdapter).IsAssignableFrom(type.AsType()))
            .Select(type => type.AsType())
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var adapterType in adapterTypes)
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IWorkflowStatusAdapter), adapterType));
        }

        return services;
    }
}
