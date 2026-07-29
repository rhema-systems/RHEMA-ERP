using System.Net;
using ErpSystem.Api.Services.Estate;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class EstateGisNetworkPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.20.30.40")]
    [InlineData("172.16.1.10")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.10.20")]
    [InlineData("100.64.1.10")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    public void PrivateAndReservedAddresses_AreRestricted(string value)
    {
        EstateGisNetworkPolicy.IsRestrictedAddress(IPAddress.Parse(value)).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddresses_ArePermitted(string value)
    {
        EstateGisNetworkPolicy.IsRestrictedAddress(IPAddress.Parse(value)).Should().BeFalse();
    }

    [Fact]
    public async Task LoopbackTarget_RequiresDeploymentAllowlist()
    {
        var blockedPolicy = CreatePolicy();
        var allowedPolicy = CreatePolicy("localhost");

        var blocked = async () => await blockedPolicy.ValidateHttpTargetAsync(
            "http://localhost:8080/geoserver",
            "GeoServer URL",
            CancellationToken.None);
        var allowed = async () => await allowedPolicy.ValidateHttpTargetAsync(
            "http://localhost:8080/geoserver",
            "GeoServer URL",
            CancellationToken.None);

        await blocked.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not approved*");
        await allowed.Should().NotThrowAsync();
    }

    [Fact]
    public void WildcardAllowlist_DoesNotApproveTheParentOrLookalikeDomain()
    {
        var policy = CreatePolicy("*.tdc.gov.gh");

        policy.IsHostExplicitlyAllowed("gis.tdc.gov.gh").Should().BeTrue();
        policy.IsHostExplicitlyAllowed("tdc.gov.gh").Should().BeFalse();
        policy.IsHostExplicitlyAllowed("gis.tdc.gov.gh.example.com").Should().BeFalse();
    }

    private static EstateGisNetworkPolicy CreatePolicy(params string[] allowedHosts)
        => new(Options.Create(new EstateGisNetworkSecurityOptions
        {
            AllowedHosts = allowedHosts.ToList()
        }));
}
