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
        var operations = new ExposedMigration().BuildUpOperations();

        operations[0].Should().BeOfType<SqlOperation>();
        var sql = ((SqlOperation)operations[0]).Sql;
        sql.Should().Contain("C5_SCHEMA_PREFLIGHT")
            .And.Contain("C5_BOOK_PREFLIGHT")
            .And.Contain("Latin1_General_100_BIN2")
            .And.Contain("ALL_ACTIVE_BOOKS")
            .And.Contain("conflicting C5 tables")
            .And.NotContain("INSERT INTO");
        operations[1].Should().BeOfType<AddUniqueConstraintOperation>();
    }

    [Fact]
    public void Migration_CreatesExactLineage_CanonicalAuthority_Triggers_AndBoundedDown()
    {
        var migration = new ExposedMigration();
        var up = migration.BuildUpOperations();
        var tables = up.OfType<CreateTableOperation>().ToDictionary(item => item.Name);

        tables.Keys.Should().BeEquivalentTo(new[]
        {
            "AccountingBookApplicabilityPolicies", "AccountingBookApplicabilityRules",
            "AccountingBookApplicabilityRuleBooks", "AccountingBookSelectionEvidence",
            "AccountingBookSelectionEvidenceBooks"
        });
        up.OfType<AddUniqueConstraintOperation>().Should().Contain(item =>
            item.Name == "AK_AccountingBooks_TenantId_Id_Code"
            && item.Columns.SequenceEqual(new[] { "TenantId", "Id", "Code" }));

        tables["AccountingBookApplicabilityPolicies"].UniqueConstraints.Should().Contain(item =>
            item.Name == "AK_AccountingBookApplicabilityPolicies_TenantId_PolicyCode_Id");
        tables["AccountingBookApplicabilityPolicies"].UniqueConstraints.Should().Contain(item =>
            item.Name == "AK_AccountingBookApplicabilityPolicies_TenantId_Id_Version");
        tables["AccountingBookSelectionEvidence"].ForeignKeys.Should().Contain(item =>
            item.Columns.SequenceEqual(new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "PolicyVersion" })
            && item.PrincipalColumns.SequenceEqual(new[] { "TenantId", "Id", "Version" }));
        tables["AccountingBookSelectionEvidence"].ForeignKeys.Should().Contain(item =>
            item.Columns.SequenceEqual(new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "AccountingBookApplicabilityRuleId" })
            && item.PrincipalColumns.SequenceEqual(new[] { "TenantId", "AccountingBookApplicabilityPolicyId", "Id" }));
        tables["AccountingBookApplicabilityRuleBooks"].ForeignKeys.Should().Contain(item =>
            item.Columns.SequenceEqual(new[] { "TenantId", "AccountingBookId", "AccountingBookCodeSnapshot" })
            && item.PrincipalColumns.SequenceEqual(new[] { "TenantId", "Id", "Code" }));

        var constraints = tables.SelectMany(pair => pair.Value.CheckConstraints).ToDictionary(item => item.Name);
        constraints.Keys.Should().Contain(new[]
        {
            "CK_AccountingBookApplicabilityPolicies_RetirementRequestShape",
            "CK_AccountingBookApplicabilityPolicies_VersionLineageShape",
            "CK_AccountingBookApplicabilityRules_ModuleSupported",
            "CK_AccountingBookSelectionEvidence_RuleLineage",
            "CK_AccountingBookSelectionEvidenceBooks_AuthorityFingerprint"
        });
        constraints.Values.Select(item => item.Sql).Should().Contain(sql => sql.Contains("ALL_CLASSIFIED_BOOKS", StringComparison.Ordinal));

        var triggerSql = string.Join('\n', up.OfType<SqlOperation>().Skip(1).Select(item => item.Sql));
        triggerSql.Should().Contain("C5_POLICY_REPLACEMENT_ORDER")
            .And.Contain("C5_POLICY_VERSION_LINEAGE")
            .And.Contain("C5_POLICY_TRANSITION_INVALID")
            .And.Contain("C5_POLICY_APPROVAL_IMMUTABLE")
            .And.Contain("C5_POLICY_RETIREMENT_TRANSITION_INVALID")
            .And.Contain("C5_EMPTY_SELECTION")
            .And.Contain("C5_SELECTED_BOOK_INVALID")
            .And.Contain("C5_RULE_AMBIGUITY")
            .And.Contain("UPDLOCK,HOLDLOCK")
            .And.Contain("C5_SELECTION_IMMUTABLE")
            .And.Contain("bounded governed retirement closure")
            .And.Contain("first approved use");

        var down = migration.BuildDownOperations();
        down[0].Should().BeOfType<SqlOperation>();
        ((SqlOperation)down[0]).Sql.Should().Contain("C5_DOWN_GUARD")
            .And.Contain("cannot be represented by the predecessor schema");
        down.OfType<DropTableOperation>().Select(item => item.Name).Should().BeEquivalentTo(tables.Keys);
    }

    [Fact]
    public void Migration_IsDiscoverableWithoutOpeningDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=C5MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Should().ContainKey(MigrationId);
    }

    private sealed class ExposedMigration : AddAccountingBookApplicabilityFoundation
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }
}
