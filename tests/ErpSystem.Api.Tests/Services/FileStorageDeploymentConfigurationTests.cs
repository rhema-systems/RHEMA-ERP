using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class FileStorageDeploymentConfigurationTests
{
    private const string SharedUploadMount =
        "- erp-uploads:/app/wwwroot/uploads";

    [Fact]
    public void ProductionApiNodesUseOneSharedUploadVolume()
    {
        var root = RepositoryRoot();
        var compose = File.ReadAllText(
            Path.Combine(root, "docker-compose.production.yml"));

        Regex.Matches(compose, Regex.Escape(SharedUploadMount))
            .Should().HaveCount(
                4,
                "each production API node must see the same local-storage namespace");
        compose.Should().Contain(
            """
              erp-uploads:
                driver: local
            """);
    }

    [Fact]
    public void ApiImagePreparesSharedUploadMountForTheNonRootUser()
    {
        var dockerfile = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Dockerfile"));
        var preparation = dockerfile.IndexOf(
            "mkdir -p /app/wwwroot/uploads",
            StringComparison.Ordinal);
        var nonRootSwitch = dockerfile.IndexOf(
            "USER erpuser",
            StringComparison.Ordinal);

        preparation.Should().BeGreaterThanOrEqualTo(0);
        nonRootSwitch.Should().BeGreaterThan(preparation);
        dockerfile.Should().Contain("chown -R erpuser:erpuser /app");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "docker-compose.production.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root from the test output path.");
    }
}
