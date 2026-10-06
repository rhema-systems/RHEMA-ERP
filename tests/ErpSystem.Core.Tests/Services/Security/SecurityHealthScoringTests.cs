using ErpSystem.Shared;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Security;

public sealed class SecurityHealthScoringTests
{
    [Fact]
    public void Calculate_WhenEveryEvidenceBackedControlPasses_ReturnsOneHundred()
    {
        var result = SecurityHealthScoring.Calculate(new SecurityHealthEvidenceDto
        {
            EligibleUsers = 10,
            MfaEnabledUsers = 10,
            PrivilegedUsers = 2,
            PrivilegedUsersWithMfa = 2,
            ActiveSessions = 4,
            HasAuditEvents = true,
            HasSecurityEvents = true,
            SuccessfulLogins = 20,
            FailedLogins = 0,
            HasPersistedSecuritySettings = true,
            PasswordPolicyConfigured = true,
            LockoutConfigured = true,
            LoginRateLimitConfigured = true,
            SessionTimeoutConfigured = true,
            RetentionConfigured = true
        });

        result.Score.Should().Be(100);
        result.MaximumScore.Should().Be(100);
        result.Factors.Should().HaveCount(6);
        result.Factors.Sum(factor => factor.MaximumPoints).Should().Be(100);
    }

    [Fact]
    public void Calculate_UsesDocumentedEvidenceInsteadOfArbitraryFallbackScores()
    {
        var result = SecurityHealthScoring.Calculate(new SecurityHealthEvidenceDto
        {
            EligibleUsers = 10,
            MfaEnabledUsers = 5,
            PrivilegedUsers = 2,
            PrivilegedUsersWithMfa = 1,
            ActiveSessions = 10,
            StaleSessions = 2,
            DisabledUsersWithActiveSessions = 1,
            HasAuditEvents = true,
            HasSecurityEvents = false,
            SuccessfulLogins = 5,
            FailedLogins = 5,
            LockedAccounts = 1,
            UnresolvedHighRiskSignals = 1,
            HasPersistedSecuritySettings = false
        });

        result.Factors.Single(factor => factor.Key == "mfa-coverage").EarnedPoints.Should().Be(10);
        result.Factors.Single(factor => factor.Key == "privileged-mfa").EarnedPoints.Should().Be(10);
        result.Factors.Single(factor => factor.Key == "audit-telemetry").EarnedPoints.Should().Be(8);
        result.Factors.Single(factor => factor.Key == "security-configuration").EarnedPoints.Should().Be(0);
        result.Factors.Single(factor => factor.Key == "authentication-anomalies").EarnedPoints.Should().Be(0);
        result.Score.Should().Be(result.Factors.Sum(factor => factor.EarnedPoints));
    }
}
