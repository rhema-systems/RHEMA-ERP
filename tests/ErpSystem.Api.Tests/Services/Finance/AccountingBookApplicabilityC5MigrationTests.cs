using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookApplicabilityC5MigrationTests
{
    private const string MigrationId = "20260906190846_AddAccountingBookApplicabilityFoundation";

    [Fact]
    public void Migration_PutsCompletePreflightBeforeEveryMutation_AndDoesNotBackfillRules()
    {
        var sql = ArchivedMigrationSource.Read("20260906190846_AddAccountingBookApplicabilityFoundation.cs");
        sql.Should().Contain("C5_SCHEMA_PREFLIGHT")
            .And.Contain("C5_BOOK_PREFLIGHT")
            .And.Contain("Latin1_General_100_BIN2")
            .And.Contain("ALL_ACTIVE_BOOKS")
            .And.Contain("conflicting C5 tables")
            .And.NotContain("INSERT INTO");
        sql.IndexOf("C5_SCHEMA_PREFLIGHT", StringComparison.Ordinal).Should().BeLessThan(
            sql.IndexOf("migrationBuilder.AddUniqueConstraint", StringComparison.Ordinal));
    }

    [Fact]
    public void Migration_CreatesExactLineage_CanonicalAuthority_Triggers_AndBoundedDown()
    {
        var source = ArchivedMigrationSource.Read("20260906190846_AddAccountingBookApplicabilityFoundation.cs");
        foreach (var table in new[]
        {
            "AccountingBookApplicabilityPolicies", "AccountingBookApplicabilityRules",
            "AccountingBookApplicabilityRuleBooks", "AccountingBookSelectionEvidence",
            "AccountingBookSelectionEvidenceBooks"
        }) source.Should().Contain($"name: \"{table}\"");
        foreach (var token in new[]
        {
            "AK_AccountingBooks_TenantId_Id_Code",
            "AK_AccountingBookApplicabilityPolicies_TenantId_PolicyCode_Id",
            "AK_AccountingBookApplicabilityPolicies_TenantId_Id_Version",
            "CK_AccountingBookApplicabilityPolicies_RetirementRequestShape",
            "CK_AccountingBookApplicabilityPolicies_VersionLineageShape",
            "CK_AccountingBookApplicabilityRules_ModuleSupported",
            "CK_AccountingBookSelectionEvidence_RuleLineage",
            "CK_AccountingBookSelectionEvidenceBooks_AuthorityFingerprint"
        }) source.Should().Contain(token);
        source.Should().Contain("ALL_CLASSIFIED_BOOKS");
        source.Should().Contain("C5_POLICY_REPLACEMENT_ORDER")
            .And.Contain("C5_POLICY_VERSION_LINEAGE")
            .And.Contain("C5_POLICY_TRANSITION_INVALID")
            .And.Contain("C5_POLICY_APPROVAL_IMMUTABLE")
            .And.Contain("C5_POLICY_RETIREMENT_STATUS_INVALID")
            .And.Contain("i.[PolicyStatus] IN (1,2,4)")
            .And.Contain("ISNULL(d.[PolicyStatus],0)<>3")
            .And.Contain("C5_POLICY_RETIREMENT_TRANSITION_INVALID")
            .And.Contain("C5_POLICY_RETIRED_IMMUTABLE")
            .And.Contain("d.[RetirementDecisionStatus]=N'Pending'")
            .And.Contain("i.[RetiredByUserId]=i.[RetirementDecidedByUserId]")
            .And.Contain("i.[RetirementReason] COLLATE Latin1_General_100_BIN2=d.[RetirementReason]")
            .And.Contain("CONVERT(date,d.[EffectiveTo])<CONVERT(date,i.[RetiredAtUtc])")
            .And.Contain("C5_EMPTY_SELECTION")
            .And.Contain("C5_SELECTED_BOOK_INVALID")
            .And.Contain("C5_RULE_AMBIGUITY")
            .And.Contain("UPDLOCK,HOLDLOCK")
            .And.Contain("C5_SELECTION_IMMUTABLE")
            .And.Contain("bounded governed retirement closure")
            .And.Contain("first approved use");

        source.Should().Contain("C5_DOWN_GUARD")
            .And.Contain("cannot be represented by the predecessor schema");
    }

    [Fact]
    public void Migration_IsDiscoverableWithoutOpeningDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=C5MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Keys.Should()
            .Equal("20260916132000_DisposableDevelopmentCurrentModelBaseline");
        ArchivedMigrationSource.Read("20260906190846_AddAccountingBookApplicabilityFoundation.cs")
            .Should().Contain($"Migration(\"{MigrationId}\")");
    }
}
