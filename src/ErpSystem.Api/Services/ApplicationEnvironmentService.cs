using System.Reflection;
using System.Text.RegularExpressions;

namespace ErpSystem.Api.Services;

public sealed record PublicApplicationEnvironment(
    string Environment,
    bool IsProduction,
    string DisplayName,
    string Message,
    bool ConfigurationValid,
    bool DataIsolationConfirmed,
    string ApplicationVersion,
    string? BuildId,
    DateTimeOffset? DeployedAtUtc);

public interface IApplicationEnvironmentService
{
    PublicApplicationEnvironment GetPublicDescriptor();
}

/// <summary>
/// Produces the only browser-visible deployment environment descriptor. The
/// authoritative value is Application:Environment; ASPNETCORE_ENVIRONMENT keeps
/// its separate framework purpose (configuration loading and runtime behavior).
/// </summary>
public sealed partial class ApplicationEnvironmentService : IApplicationEnvironmentService
{
    private static readonly IReadOnlyDictionary<string, string> SupportedEnvironments =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Production"] = "Production",
            ["Test"] = "Test",
            ["UAT"] = "UAT",
            ["Staging"] = "Staging",
            ["Development"] = "Development"
        };

    private readonly PublicApplicationEnvironment _descriptor;

    public ApplicationEnvironmentService(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<ApplicationEnvironmentService> logger)
    {
        var configuredEnvironment = configuration["Application:Environment"]?.Trim();
        string? canonicalEnvironment = null;
        var configurationValid = configuredEnvironment is not null
            && SupportedEnvironments.TryGetValue(configuredEnvironment, out canonicalEnvironment);

        canonicalEnvironment ??= "Unknown";
        if (!configurationValid)
        {
            logger.LogError(
                "Application:Environment is missing or invalid. The public descriptor will remain Unknown. " +
                "Supported values are Production, Test, UAT, Staging, and Development.");
        }
        else if (!string.Equals(
                     canonicalEnvironment,
                     hostEnvironment.EnvironmentName,
                     StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Application environment {ApplicationEnvironment} is distinct from ASP.NET host environment {HostEnvironment}.",
                canonicalEnvironment,
                hostEnvironment.EnvironmentName);
        }

        var isProduction = configurationValid
            && string.Equals(canonicalEnvironment, "Production", StringComparison.Ordinal);
        var dataIsolationConfirmed = !isProduction
            && configurationValid
            && configuration.GetValue<bool>("Application:DataIsolationConfirmed");

        _descriptor = new PublicApplicationEnvironment(
            canonicalEnvironment,
            isProduction,
            canonicalEnvironment == "Unknown" ? "Unknown Environment" : $"{canonicalEnvironment} Environment",
            GetMessage(canonicalEnvironment, dataIsolationConfirmed),
            configurationValid,
            dataIsolationConfirmed,
            GetSafeVersion(configuration),
            GetSafeBuildId(configuration["Application:BuildId"]),
            GetSafeDeploymentDate(configuration["Application:DeployedAtUtc"]));
    }

    public PublicApplicationEnvironment GetPublicDescriptor() => _descriptor;

    private static string GetMessage(string environment, bool dataIsolationConfirmed) =>
        environment switch
        {
            "Production" => "Live production environment",
            "Development" => "Developer use only",
            "Unknown" => "Environment configuration is missing or invalid. Treat this system as unsafe until corrected.",
            _ when dataIsolationConfirmed => $"{environment} only - changes here do not affect Production",
            _ => $"{environment} application environment - data isolation has not been confirmed"
        };

    private static string GetSafeVersion(IConfiguration configuration)
    {
        var configured = GetSafeOptionalValue(configuration["Application:Version"]);
        if (configured is not null)
        {
            return configured;
        }

        return typeof(ApplicationEnvironmentService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+', 2)[0] ?? "Unknown";
    }

    private static string? GetSafeOptionalValue(string? value)
    {
        var candidate = value?.Trim();
        return candidate is not null && SafeMetadataPattern().IsMatch(candidate)
            ? candidate
            : null;
    }

    private static string? GetSafeBuildId(string? value)
    {
        var candidate = value?.Trim();
        return candidate is not null && BuildIdPattern().IsMatch(candidate)
            ? candidate
            : null;
    }

    private static DateTimeOffset? GetSafeDeploymentDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeMetadataPattern();

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex BuildIdPattern();
}
