using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class FileStorageDeploymentConfigurationTests
{
    private const string SharedUploadMount =
        "- erp-uploads:/app/wwwroot/uploads";
    private const string SharedSecureFileMount =
        "- erp-secure-files:/app/secure-file-storage";

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
        Regex.Matches(compose, Regex.Escape(SharedSecureFileMount))
            .Should().HaveCount(
                4,
                "each production API node must see the same private DMS namespace");
        compose.Should().Contain(
            """
              erp-uploads:
                driver: local
            """);
        compose.Should().Contain(
            """
              erp-secure-files:
                driver: local
            """);
        Regex.Matches(
                compose,
                Regex.Escape(
                    "FileStorage__Local__PrivateBasePath=/app/secure-file-storage"))
            .Should().HaveCount(4);
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
            "mkdir -p /app/wwwroot/uploads /app/secure-file-storage",
            StringComparison.Ordinal);
        var nonRootSwitch = dockerfile.IndexOf(
            "USER erpuser",
            StringComparison.Ordinal);

        preparation.Should().BeGreaterThanOrEqualTo(0);
        nonRootSwitch.Should().BeGreaterThan(preparation);
        dockerfile.Should().Contain("chown -R erpuser:erpuser /app");
    }

    [Fact]
    public void LegacySupplierEvidenceIsBlockedBeforeStaticFileServing()
    {
        var program = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ErpSystem.Api",
            "Program.cs"));
        var block = program.IndexOf(
            "/uploads/supplier-registration-evidence",
            StringComparison.Ordinal);
        var staticFiles = program.IndexOf(
            "app.UseStaticFiles();",
            StringComparison.Ordinal);

        block.Should().BeGreaterThanOrEqualTo(0);
        staticFiles.Should().BeGreaterThan(block);
        program.Should().Contain(
            "context.Response.StatusCode = StatusCodes.Status404NotFound");
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
