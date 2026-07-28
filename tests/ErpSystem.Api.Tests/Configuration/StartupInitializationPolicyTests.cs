using ErpSystem.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ErpSystem.Api.Tests.Configuration;

public class StartupInitializationPolicyTests
{
    [Fact]
    public void DevelopmentEnvironment_ShouldPermitRequestedDevelopmentDataSeeding()
    {
        StartupInitializationPolicy.ShouldRunDevelopmentDataSeeding(
                Environments.Development,
                seedDevelopmentData: true,
                allowOutsideDevelopment: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ProductionEnvironment_ShouldRejectDevelopmentDataSeedingByDefault()
    {
        StartupInitializationPolicy.ShouldRunDevelopmentDataSeeding(
                Environments.Production,
                seedDevelopmentData: true,
                allowOutsideDevelopment: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ExplicitTestServerOverride_ShouldPermitSeedingOutsideDevelopment()
    {
        StartupInitializationPolicy.ShouldRunDevelopmentDataSeeding(
                Environments.Production,
                seedDevelopmentData: true,
                allowOutsideDevelopment: true)
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void DisabledSeedFlag_ShouldAlwaysPreventDevelopmentDataSeeding(
        string environmentName)
    {
        StartupInitializationPolicy.ShouldRunDevelopmentDataSeeding(
                environmentName,
                seedDevelopmentData: false,
                allowOutsideDevelopment: true)
            .Should()
            .BeFalse();
    }
}
