using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderLineageRecoveryMigrationGuardTests
{
    [Fact]
    public void Trigger_allows_only_the_governed_null_to_matching_case_recovery()
    {
        var source = File.ReadAllText(FindMigration());

        source.Should().Contain("d.[SourcingCaseId] IS NOT NULL");
        source.Should().Contain("i.[SourcingCaseId] IS NULL OR d.[SourcingCaseId] <> i.[SourcingCaseId]");
        source.Should().Contain("sc.[PurchaseRequisitionId] <> i.[SourcePurchaseRequisitionId]");
        source.Should().Contain("sc.[SourcingReleaseId] <> i.[SourcingReleaseId]");
        source.Should().Contain("sc.[SourceControlFingerprint] <> sr.[ControlFingerprint]");
        source.Should().Contain("d.[SourcingReleaseId] <> i.[SourcingReleaseId]");
    }

    private static string FindMigration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "ErpSystem.Data",
                "LegacyMigrationsArchive",
                "20260901033000_AllowGovernedTenderLineageRecovery.cs");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the governed tender-lineage recovery migration.");
    }
}
