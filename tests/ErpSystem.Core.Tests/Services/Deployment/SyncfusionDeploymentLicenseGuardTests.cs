using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Deployment;

public sealed class SyncfusionDeploymentLicenseGuardTests
{
    [Fact]
    public void Release_pipeline_licenses_server_and_frontend_without_persisting_the_key()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Program.cs"));
        var deploy = File.ReadAllText(Path.Combine(root, "scripts", "Deploy-RhemaVps.ps1"));
        var remote = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        program.Should().Contain("builder.Configuration[\"Syncfusion:LicenseKey\"]")
            .And.Contain("SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey)");

        deploy.Should().Contain("function Get-SyncfusionLicenseKey")
            .And.Contain("SYNCFUSION_LICENSE = $syncfusionLicenseKey")
            .And.Contain("node_modules\\.bin\\syncfusion-license.cmd")
            .And.Contain("syncfusionFrontendLicensed = $true")
            .And.Contain("$manifest.syncfusionFrontendLicensed -ne $true")
            .And.NotContain("syncfusionLicenseKey = $syncfusionLicenseKey");

        remote.Should().Contain("function Assert-SyncfusionLicenseConfigured")
            .And.Contain("SYNCFUSION_LICENSE|CONFIGURED")
            .And.Contain("Assert-SyncfusionLicenseConfigured");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;

        throw new DirectoryNotFoundException(
            "Repository root containing ErpSystem.sln was not found.");
    }
}
