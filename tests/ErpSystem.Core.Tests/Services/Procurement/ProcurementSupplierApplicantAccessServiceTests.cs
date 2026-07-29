using System.Reflection;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierApplicantAccessServiceTests
{
    private static readonly Type ServiceType =
        typeof(ProcurementSupplierApplicantAccessService);

    [Fact]
    public void DecisionRegisterCoversEveryApprovedConfigurationDecision()
    {
        var field = ServiceType.GetField(
            "DecisionKeys",
            BindingFlags.NonPublic | BindingFlags.Static);

        field.Should().NotBeNull();
        ((IReadOnlyList<string>)field!.GetValue(null)!).Should().Equal(
            Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}"));
    }

    [Fact]
    public void ApplicantDefaultsKeepSessionsShortAndTemporaryCredentialAtSevenDays()
    {
        var options = new SupplierApplicantAccessOptions();

        options.ApplicantSessionMinutes.Should().Be(60);
        options.TemporaryPasswordExpiryDays.Should().Be(7);
        options.ApprovedIdentityRoles.Should().Equal("ExternalUser");
        options.ApprovedBusinessPartnerRole.Should().Be("Admin");
    }

    [Fact]
    public void GeneratedTemporaryCredentialsAreRandomAndMeetTheIdentityComplexityFloor()
    {
        var method = ServiceType.GetMethod(
            "GenerateTemporaryPassword",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var first = (string)method.Invoke(null, null)!;
        var second = (string)method.Invoke(null, null)!;

        first.Should().NotBe(second);
        first.Should().HaveLength(30);
        first.Should().MatchRegex("[A-Z]");
        first.Should().MatchRegex("[a-z]");
        first.Should().MatchRegex("[0-9]");
        first.Should().MatchRegex("[^A-Za-z0-9]");
    }

    [Theory]
    [InlineData(ProcurementSupplierApplicantVerificationChannel.Email,
        " SUPPLIER@EXAMPLE.COM ", "supplier@example.com")]
    [InlineData(ProcurementSupplierApplicantVerificationChannel.Sms,
        " +233 24-123-4567 ", "+233241234567")]
    public void VerifiedContactsAreNormalizedBeforeHashingOrDelivery(
        ProcurementSupplierApplicantVerificationChannel channel,
        string input,
        string expected)
    {
        var method = ServiceType.GetMethod(
            "NormalizeContact",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(null, [channel, input]).Should().Be(expected);
    }
}
