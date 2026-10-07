using System.Text.Json;
using ErpSystem.Api.Services;
using ErpSystem.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class ApplicationEnvironmentServiceTests
{
    [Fact]
    public void Public_endpoint_is_anonymous_and_returns_only_the_safe_descriptor()
    {
        var service = CreateService(new Dictionary<string, string?>
        {
            ["Application:Environment"] = "Staging",
            ["Application:Version"] = "2026.10.05",
            ["Application:BuildId"] = "a8f27c1"
        });
        var controller = new PublicApplicationConfigurationController(service);

        typeof(PublicApplicationConfigurationController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Should().ContainSingle();
        var result = controller.GetEnvironment().Result.Should().BeOfType<OkObjectResult>().Subject;
        result.Value.Should().BeEquivalentTo(service.GetPublicDescriptor());
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Test", false)]
    [InlineData("UAT", false)]
    [InlineData("Staging", false)]
    [InlineData("Development", false)]
    public void Supported_environment_is_returned_canonically(string configured, bool production)
    {
        var descriptor = CreateService(new Dictionary<string, string?>
        {
            ["Application:Environment"] = configured,
            ["Application:Version"] = "2026.10.05",
            ["Application:BuildId"] = "a8f27c1",
            ["Application:DeployedAtUtc"] = "2026-10-05T18:42:00Z"
        }).GetPublicDescriptor();

        descriptor.Environment.Should().Be(configured);
        descriptor.IsProduction.Should().Be(production);
        descriptor.ConfigurationValid.Should().BeTrue();
        descriptor.ApplicationVersion.Should().Be("2026.10.05");
        descriptor.BuildId.Should().Be("a8f27c1");
        descriptor.DeployedAtUtc.Should().Be(DateTimeOffset.Parse("2026-10-05T18:42:00Z"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Prod")]
    [InlineData("QA")]
    public void Missing_or_invalid_environment_is_never_treated_as_production(string? configured)
    {
        var descriptor = CreateService(new Dictionary<string, string?>
        {
            ["Application:Environment"] = configured
        }).GetPublicDescriptor();

        descriptor.Environment.Should().Be("Unknown");
        descriptor.DisplayName.Should().Be("Unknown Environment");
        descriptor.IsProduction.Should().BeFalse();
        descriptor.ConfigurationValid.Should().BeFalse();
        descriptor.Message.Should().Contain("unsafe");
    }

    [Fact]
    public void Isolation_wording_is_only_used_when_explicitly_confirmed()
    {
        var unconfirmed = CreateService(new Dictionary<string, string?>
        {
            ["Application:Environment"] = "UAT"
        }).GetPublicDescriptor();
        var confirmed = CreateService(new Dictionary<string, string?>
        {
            ["Application:Environment"] = "UAT",
            ["Application:DataIsolationConfirmed"] = "true"
        }).GetPublicDescriptor();

        unconfirmed.Message.Should().Contain("has not been confirmed");
        unconfirmed.Message.Should().NotContain("do not affect Production");
        confirmed.Message.Should().Contain("do not affect Production");
    }

    [Fact]
    public void Public_descriptor_never_serializes_unrelated_configuration_or_unsafe_metadata()
    {
        const string secret = "super-secret-connection-password";
        var descriptor = CreateService(new Dictionary<string, string?>
        {
            ["Application:Environment"] = "Test",
            ["Application:BuildId"] = secret,
            ["ConnectionStrings:DefaultConnection"] = secret,
            ["JwtSettings:SecretKey"] = secret
        }).GetPublicDescriptor();

        descriptor.BuildId.Should().BeNull();
        JsonSerializer.Serialize(descriptor).Should().NotContain(secret);
    }

    private static ApplicationEnvironmentService CreateService(
        IDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new ApplicationEnvironmentService(
            configuration,
            new TestHostEnvironment(),
            NullLogger<ApplicationEnvironmentService>.Instance);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ErpSystem.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
