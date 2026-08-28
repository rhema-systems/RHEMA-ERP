using ErpSystem.Core.Performance;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Provider-neutral safeguards for the Finance volume and performance release policy. The opt-in
/// SQL Server runbook exercises the real database; these fast tests ensure every pull request keeps
/// the policy complete and cannot accidentally weaken its percentile or query-count evaluation.
/// </summary>
public sealed class FinancePerformanceReadinessTests
{
    [Fact]
    public void Policy_ShouldCoverEveryCriticalFinanceWorkload()
    {
        FinancePerformanceReadinessPolicy.Thresholds.Keys
            .Should().BeEquivalentTo(Enum.GetValues<FinancePerformanceWorkload>());

        FinancePerformanceReadinessPolicy.Thresholds.Values.Should().OnlyContain(threshold =>
            threshold.MaximumQueryCount > 0 && threshold.MaximumP95Milliseconds > 0);
    }

    [Fact]
    public void TdcRepresentativeProfile_ShouldExerciseMaterialFinanceVolumes()
    {
        var profile = FinancePerformanceVolumeProfile.TdcRepresentative;

        profile.AccountCount.Should().BeGreaterThanOrEqualTo(1_000);
        profile.LedgerLineCount.Should().BeGreaterThanOrEqualTo(100_000);
        profile.AccountsPayableDocumentCount.Should().BeGreaterThanOrEqualTo(25_000);
        profile.AccountsReceivableDocumentCount.Should().BeGreaterThanOrEqualTo(25_000);
        profile.BankStatementLineCount.Should().BeGreaterThanOrEqualTo(50_000);
        profile.PeriodCloseTaskCount.Should().BeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public void Assessment_ShouldUseWorstQueryCountAndNearestRankP95()
    {
        var samples = Enumerable.Range(1, 20)
            .Select(index => new FinancePerformanceSample(index == 8 ? 4 : 2, index * 10))
            .ToArray();

        var result = FinancePerformanceReadinessPolicy.Assess(
            FinancePerformanceWorkload.JournalInquiry,
            samples);

        result.MaximumObservedQueryCount.Should().Be(4);
        result.P95DatabaseElapsedMilliseconds.Should().Be(190);
        result.Passed.Should().BeFalse("the observed query count exceeds the central journal-inquiry budget");
    }

    [Fact]
    public void Assessment_ShouldPassOnlyWhenBothBudgetsAreMet()
    {
        var threshold = FinancePerformanceReadinessPolicy.Thresholds[FinancePerformanceWorkload.Posting];
        var samples = Enumerable.Range(1, FinancePerformanceReadinessPolicy.MinimumMeasuredSampleCount)
            .Select(index => new FinancePerformanceSample(
                index == 1 ? threshold.MaximumQueryCount : threshold.MaximumQueryCount - 1,
                index == 2 ? threshold.MaximumP95Milliseconds : threshold.MaximumP95Milliseconds - 1))
            .ToArray();

        FinancePerformanceReadinessPolicy
            .Assess(FinancePerformanceWorkload.Posting, samples)
            .Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    public void Assessment_ShouldRejectAnUnderSampledReleaseGate(int sampleCount)
    {
        var samples = Enumerable.Range(0, sampleCount)
            .Select(_ => new FinancePerformanceSample(1, 1))
            .ToArray();
        var action = () => FinancePerformanceReadinessPolicy.Assess(
            FinancePerformanceWorkload.TrialBalance,
            samples);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("samples")
            .WithMessage("At least 20 measured performance samples*");
    }

    [Fact]
    public void LedgerPerformanceMigration_ShouldCreateOnlyTheReviewedCompositeIndex()
    {
        var upBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableLedgerPerformanceMigration().ApplyUp(upBuilder);

        var created = upBuilder.Operations.Should().ContainSingle()
            .Which.Should().BeOfType<CreateIndexOperation>().Which;
        created.Table.Should().Be("AccountTransactions");
        created.Name.Should().Be("IX_AccountTransactions_TenantId_BookClassification_TransactionDate_AccountId");
        created.Columns.Should().Equal("TenantId", "BookClassification", "TransactionDate", "AccountId");

        // Down symmetry keeps development database resets safe while the application is still
        // pre-production and its migration chain is regularly replayed by collaborating teams.
        var downBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableLedgerPerformanceMigration().ApplyDown(downBuilder);
        var dropped = downBuilder.Operations.Should().ContainSingle()
            .Which.Should().BeOfType<DropIndexOperation>().Which;
        dropped.Table.Should().Be(created.Table);
        dropped.Name.Should().Be(created.Name);
    }

    private sealed class TestableLedgerPerformanceMigration : AddFinanceLedgerPerformanceIndex
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
        public void ApplyDown(MigrationBuilder migrationBuilder) => Down(migrationBuilder);
    }
}
