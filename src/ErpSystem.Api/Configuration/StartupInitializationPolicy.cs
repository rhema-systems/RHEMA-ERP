using Microsoft.Extensions.Hosting;

namespace ErpSystem.Api.Configuration;

public static class StartupInitializationPolicy
{
    public const string AllowDevelopmentDataSeedingOutsideDevelopmentKey =
        "StartupInitialization:AllowDevelopmentDataSeedingOutsideDevelopment";

    public static bool IsDevelopmentDataSeedingPermitted(
        string? environmentName,
        bool allowOutsideDevelopment)
        => string.Equals(
                environmentName,
                Environments.Development,
                StringComparison.OrdinalIgnoreCase)
            || allowOutsideDevelopment;

    public static bool ShouldRunDevelopmentDataSeeding(
        string? environmentName,
        bool seedDevelopmentData,
        bool allowOutsideDevelopment)
        => seedDevelopmentData
            && IsDevelopmentDataSeedingPermitted(
                environmentName,
                allowOutsideDevelopment);
}
