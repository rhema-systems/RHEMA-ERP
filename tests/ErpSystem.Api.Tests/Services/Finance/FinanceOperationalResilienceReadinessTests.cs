using System.Text.Json;
using ErpSystem.Api.Extensions;
using ErpSystem.Api.HealthChecks;
using ErpSystem.Core.Resilience;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

[Trait("Batch", "FinanceOperationalResilience")]
public sealed class FinanceOperationalResilienceReadinessTests
{
    private static readonly DateTimeOffset AssessedAt = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Assess_PassesOnlyWhenEveryRequiredEvidenceCategoryIsCurrentAndSuccessful()
    {
        var evidence = FinanceOperationalResilienceReadinessPolicy.MaximumEvidenceAge
            .Select(requirement => new FinanceOperationalEvidence(
                requirement.Key,
                Passed: true,
                AssessedAt - TimeSpan.FromTicks(requirement.Value.Ticks / 2),
                $"UAT-{requirement.Key}-001"))
            .ToArray();

        var result = FinanceOperationalResilienceReadinessPolicy.Assess(evidence, AssessedAt);

        result.Passed.Should().BeTrue();
        result.Evidence.Should().OnlyContain(item => item.Status == "Passed");
    }

    [Fact]
    public void Assess_BlocksReleaseWhenEvidenceIsMissingFailedStaleOrUnreferenced()
    {
        var evidence = new[]
        {
            Current(FinanceOperationalEvidenceType.AvailabilityProbe),
            Current(FinanceOperationalEvidenceType.DatabaseReadiness) with { Passed = false },
            Current(FinanceOperationalEvidenceType.BackupFreshness) with { ObservedAt = AssessedAt.AddDays(-2) },
            Current(FinanceOperationalEvidenceType.RestoreDrill) with { Reference = " " },
            Current(FinanceOperationalEvidenceType.PostingRetry)
            // SupportDiagnostics is intentionally absent to prove omission cannot pass silently.
        };

        var result = FinanceOperationalResilienceReadinessPolicy.Assess(evidence, AssessedAt);

        result.Passed.Should().BeFalse();
        result.Evidence.Single(item => item.Type == FinanceOperationalEvidenceType.DatabaseReadiness).Status.Should().Be("Failed");
        result.Evidence.Single(item => item.Type == FinanceOperationalEvidenceType.BackupFreshness).Status.Should().Be("Stale");
        result.Evidence.Single(item => item.Type == FinanceOperationalEvidenceType.RestoreDrill).Status.Should().Be("Invalid");
        result.Evidence.Single(item => item.Type == FinanceOperationalEvidenceType.SupportDiagnostics).Status.Should().Be("Missing");
    }

    [Fact]
    public void Assess_UsesLatestRetainedRunAndRejectsImplausibleFutureEvidence()
    {
        var evidence = FinanceOperationalResilienceReadinessPolicy.MaximumEvidenceAge.Keys
            .Select(Current)
            .Append(Current(FinanceOperationalEvidenceType.PostingRetry) with
            {
                ObservedAt = AssessedAt.AddMinutes(6),
                Reference = "UAT-POSTING-FUTURE"
            })
            .ToArray();

        var result = FinanceOperationalResilienceReadinessPolicy.Assess(evidence, AssessedAt);

        result.Passed.Should().BeFalse();
        result.Evidence.Single(item => item.Type == FinanceOperationalEvidenceType.PostingRetry).Status.Should().Be("Invalid");
    }

    [Fact]
    public async Task HealthResponseWriter_ReturnsStructuredDiagnosticsWithoutExceptionDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new(
                    HealthStatus.Unhealthy,
                    "sensitive connection detail in description",
                    TimeSpan.FromMilliseconds(25),
                    new InvalidOperationException("sensitive connection detail"),
                    data: null,
                    tags: new[] { "ready", "db" })
            },
            TimeSpan.FromMilliseconds(25));

        await HealthCheckResponseWriter.WriteAsync(context, report);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var json = document.RootElement.GetRawText();

        document.RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
        document.RootElement.GetProperty("checks")[0].GetProperty("name").GetString().Should().Be("database");
        document.RootElement.GetProperty("checks")[0].GetProperty("description").GetString()
            .Should().Be("Health check did not pass.");
        json.Should().NotContain("sensitive connection detail");
    }

    [Fact]
    public async Task HealthRegistration_SeparatesProcessLivenessFromTrafficReadiness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddErpSystemHealthChecks(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();

        var live = await healthChecks.CheckHealthAsync(registration => registration.Tags.Contains("live"));
        var ready = await healthChecks.CheckHealthAsync(registration => registration.Tags.Contains("ready"));

        // The no-dependency fixture proves the route predicates have disjoint process/startup
        // semantics. Production readiness additionally includes configured SQL/Redis checks.
        live.Entries.Keys.Should().Equal("self");
        ready.Entries.Keys.Should().Equal("startup");
    }

    private static FinanceOperationalEvidence Current(FinanceOperationalEvidenceType type)
        => new(type, Passed: true, AssessedAt.AddMinutes(-30), $"UAT-{type}-CURRENT");
}
