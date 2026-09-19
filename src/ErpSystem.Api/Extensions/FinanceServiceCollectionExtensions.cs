using System.Reflection;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ErpSystem.Api.Extensions;

public static class FinanceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Finance-owned account provisioning boundary used by producer modules.
    /// Keep this narrow registration reusable by command hosts that do not load the entire
    /// web application graph.
    /// </summary>
    public static IServiceCollection AddFinanceAccountProvisioning(this IServiceCollection services)
    {
        services.TryAddScoped<IFinanceAccountProvisioningService, FinanceAccountProvisioningService>();
        return services;
    }

    public static IServiceCollection AddErpSystemFinanceServices(this IServiceCollection services)
    {
        services.AddFinanceAccountProvisioning();

        // Assemblies to scan
        var coreAssembly = Assembly.Load("ErpSystem.Core");
        var dataAssembly = Assembly.Load("ErpSystem.Data");
        var apiAssembly = Assembly.Load("ErpSystem.Api");

        // 1. Find all Finance Interfaces
        var financeInterfaces = coreAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace != null && 
                        (t.Namespace.Contains(".Finance") || 
                         t.Namespace.Contains(".UnitAccounting")))
            .ToList();

        // 2. Find all implementation classes
        var implementations = new List<Type>();
        
        var assembliesToScan = new[] { dataAssembly, coreAssembly, apiAssembly };
        
        foreach (var assembly in assembliesToScan)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null && 
                            (t.Namespace.Contains(".Finance") || 
                             t.Namespace.Contains(".UnitAccounting")))
                .ToList();
                
            implementations.AddRange(types);
        }

        // 3. Register them
        foreach (var iface in financeInterfaces)
        {
            if (iface.IsGenericType) continue;

            // Find an implementation that implements this interface
            // We favor implementations that have the same name as the interface (minus the 'I')
            var interfaceName = iface.Name.Substring(1);
            
            var implementation = implementations.FirstOrDefault(t => 
                iface.IsAssignableFrom(t) && t.Name == interfaceName);
                
            // Fallback to any implementation if exact name match not found
            if (implementation == null)
            {
                implementation = implementations.FirstOrDefault(t => iface.IsAssignableFrom(t));
            }

            if (implementation != null)
            {
                // TryAddScoped ensures we don't duplicate if ServiceCollectionExtensions already registered it
                services.TryAddScoped(iface, implementation);
            }
        }
        
        // Manual fallbacks for anything that might have been missed by the convention scan
        // Ex: FinanceSettingsService -> IFinanceSettingsService
        // Just in case the namespace doesn't contain .Finance
        var explicitTypes = new[] 
        {
            ("ErpSystem.Core.Interfaces.IFinanceSettingsService", "ErpSystem.Api.Services.Finance.Settings.FinanceSettingsService"),
            ("ErpSystem.Core.Interfaces.IGeneralLedgerService", "ErpSystem.Api.Services.Finance.GL.GeneralLedgerService"),
            ("ErpSystem.Api.Services.Finance.GL.IBookValidationService", "ErpSystem.Api.Services.Finance.GL.BookValidationService"),
            ("ErpSystem.Core.Interfaces.ITenantSettingsService", "ErpSystem.Api.Services.TenantSettingsService"),
            ("ErpSystem.Core.Interfaces.Inventory.IInventoryReceiptService", "ErpSystem.Api.Services.Inventory.InventoryReceiptService"),
            ("ErpSystem.Core.Interfaces.Inventory.IInventoryReturnService", "ErpSystem.Api.Services.Inventory.InventoryReturnService")
        };
        
        foreach (var (iName, cName) in explicitTypes)
        {
            var iType = coreAssembly.GetType(iName);
            var cType = apiAssembly.GetType(cName);
            if (iType != null && cType != null)
            {
                services.TryAddScoped(iType, cType);
            }
        }

        // Concrete services/engines that don't use interfaces
        services.TryAddScoped<ErpSystem.Api.Services.Finance.Cash.BankReconciliationEngine>();
        services.TryAddScoped<ErpSystem.Api.Services.Finance.Taxation.TaxCalculationEngine>();

        return services;
    }
}
