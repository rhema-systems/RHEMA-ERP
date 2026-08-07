using System.Text.Json;
using ErpSystem.Core.Models;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Configuration;

public class CandidatePortalOptionsTests
{
    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://careers.example.com", true)]
    [InlineData("/careers/portal", false)]
    [InlineData("careers.example.com", false)]
    [InlineData("file:///tmp/portal", false)]
    [InlineData("", false)]
    public void PortalUrl_validation_requires_an_absolute_http_or_https_url(
        string portalUrl,
        bool expected)
    {
        CandidatePortalOptions.IsValidPortalUrl(portalUrl).Should().Be(expected);
    }

    [Fact]
    public void Development_settings_template_supplies_a_valid_candidate_portal_base_url()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settingsPath = Path.Combine(
            repositoryRoot,
            "src",
            "ErpSystem.Api",
            "appsettings.Development.json.template");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var portalUrl = document.RootElement
            .GetProperty(CandidatePortalOptions.SectionName)
            .GetProperty(nameof(CandidatePortalOptions.PortalUrl))
            .GetString();

        CandidatePortalOptions.IsValidPortalUrl(portalUrl).Should().BeTrue();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
