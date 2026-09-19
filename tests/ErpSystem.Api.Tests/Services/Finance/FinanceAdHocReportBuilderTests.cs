using ErpSystem.Api.Authorization;
using ErpSystem.Api.Services.Finance.Reporting;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceAdHocReportBuilderTests
{
    [Fact]
    public void Catalogue_ExposesCuratedFinanceDatasetsWithoutBrowserSuppliedSql()
    {
        FinanceAdHocReportCatalog.All.Select(item => item.Code).Should().BeEquivalentTo(
            "gl-lines", "ap-invoices", "ar-invoices", "fixed-assets", "chart-of-accounts", "book-balances");
        FinanceAdHocReportCatalog.All.Should().OnlyContain(dataset =>
            dataset.Fields.Count > 0 && dataset.Fields.Values.All(field =>
                !string.IsNullOrWhiteSpace(field.SqlExpression)));
    }

    [Fact]
    public void ChartOfAccountsCatalogue_UsesOneExactActiveDefaultBookAndTenantSafeClassificationLineage()
    {
        var dataset = FinanceAdHocReportCatalog.Required("chart-of-accounts");

        dataset.FromSql.Should().NotContain("TOP (1)");
        dataset.FromSql.Should().Contain("COUNT_BIG(*)").And.Contain(") = 1");
        dataset.FromSql.Should().Contain("[ab].[TenantId] = [a].[TenantId]")
            .And.Contain("[ab].[IsActive] = 1")
            .And.Contain("[ab].[AllowsPosting] = 1")
            .And.Contain("[ac].[TenantId] = [a].[TenantId]")
            .And.Contain("[ac].[AccountingBookId] = [ab].[Id]")
            .And.Contain("[ac].[Status] = 2")
            .And.Contain("[parent].[TenantId] = [a].[TenantId]")
            .And.Contain("[parent].[AccountingBookId] = [ab].[Id]");
    }

    [Fact]
    public void BookBalancesCatalogue_ExposesOnlyBookScopedBalances()
    {
        var dataset = FinanceAdHocReportCatalog.Required("book-balances");

        dataset.FromSql.Should().Contain("[b].[AccountingBookId]")
            .And.Contain("[b].[FiscalPeriodId]")
            .And.Contain("[a].[TenantId] = [b].[TenantId]");
        dataset.Fields.Should().ContainKey("closingBalance");
        FinanceAdHocReportCatalog.Required("chart-of-accounts").Fields.Should().NotContainKey("balance");
    }

    [Fact]
    public void Compiler_ParameterisesHostileFilterAndAlwaysAddsTenantPredicate()
    {
        var tenantId = Guid.NewGuid();
        var stored = new FinanceAdHocStoredDefinition
        {
            DatasetCode = "ap-invoices",
            Columns = [new() { Field = "supplierName", Aggregation = "None" }],
            Filters = [new() { Field = "supplierName", Operator = "Contains", Value = "TDC%' OR 1=1--" }]
        };

        var compiled = FinanceAdHocSqlCompiler.Compile(
            FinanceAdHocReportService.ValidateStored(stored), stored, tenantId, 5000,
            new ExecuteReportDto { Page = 1, PageSize = 100 });

        compiled.PageSql.Should().Contain("[i].[TenantId] = @tenantId");
        compiled.PageSql.Should().Contain("LIKE @filter0");
        compiled.PageSql.Should().NotContain("OR 1=1");
        compiled.Parameters.Should().Contain(item => item.Name == "@tenantId"
            && Equals(item.Value, tenantId));
        compiled.Parameters.Should().Contain(item => item.Name == "@filter0"
            && Convert.ToString(item.Value)!.Contains("OR 1=1"));
    }

    [Fact]
    public void Compiler_GroupsPlainColumnsAndHonoursDefinitionRowCeiling()
    {
        var stored = new FinanceAdHocStoredDefinition
        {
            DatasetCode = "gl-lines",
            Columns =
            [
                new() { Field = "accountNumber", Aggregation = "None" },
                new() { Field = "debit", Aggregation = "Sum" }
            ],
            Sorts = [new() { Field = "debit", Descending = true }]
        };

        var compiled = FinanceAdHocSqlCompiler.Compile(
            FinanceAdHocReportService.ValidateStored(stored), stored, Guid.NewGuid(), 125,
            new ExecuteReportDto { Page = 1, PageSize = 500, MaxRows = 1000 });

        compiled.PageSql.Should().Contain("SUM([t].[DebitAmount]) AS [debit]");
        compiled.PageSql.Should().Contain("GROUP BY [a].[AccountNumber]");
        compiled.PageSql.Should().Contain("ORDER BY [debit] DESC");
        compiled.CountSql.Should().Contain("TOP (126)")
            .And.Contain("[GovernedRows]");
        compiled.MaximumRows.Should().Be(125);
        compiled.PageSize.Should().Be(125);
    }

    [Fact]
    public void Compiler_ClampsUntrustedPagingValuesWithoutRelaxingSavedCeiling()
    {
        var stored = new FinanceAdHocStoredDefinition
        {
            DatasetCode = "chart-of-accounts",
            Columns = [new() { Field = "accountNumber", Aggregation = "None" }]
        };

        var compiled = FinanceAdHocSqlCompiler.Compile(
            FinanceAdHocReportService.ValidateStored(stored), stored, Guid.NewGuid(), 5000,
            new ExecuteReportDto { Page = int.MaxValue, PageSize = int.MaxValue, MaxRows = 0 });

        compiled.MaximumRows.Should().Be(1);
        compiled.PageSize.Should().Be(1);
        compiled.Offset.Should().Be(1);
    }

    [Fact]
    public void ExportExecution_UsesExportAuthorityAndSavedDefinitionCeiling()
    {
        var sharedTransportRequest = new ExecuteReportDto
        {
            Page = 2,
            PageSize = 1000,
            MaxRows = 1000,
            IsExportExecution = true
        };

        var governed = FinanceAdHocReportService.GovernExecutionRequest(5000, sharedTransportRequest);

        FinanceAdHocReportService.RequiredExecutionPermission(governed)
            .Should().Be(FinancePermissions.ExportFinanceReports);
        governed.MaxRows.Should().Be(5000,
            "the saved Finance definition governs the complete multi-page export");
        governed.Page.Should().Be(2);
        governed.PageSize.Should().Be(1000);
    }

    [Fact]
    public void InteractiveExecution_RetainsRunAuthorityAndAnyStricterRequestedCeiling()
    {
        var request = new ExecuteReportDto { MaxRows = 250 };

        var governed = FinanceAdHocReportService.GovernExecutionRequest(5000, request);

        governed.Should().BeSameAs(request);
        governed.MaxRows.Should().Be(250);
        FinanceAdHocReportService.RequiredExecutionPermission(governed)
            .Should().Be(FinancePermissions.RunFinanceReports);
    }

    [Fact]
    public void DefinitionLifecycle_WrapsEveryTransactionInTheConfiguredRetryStrategy()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api",
            "Services", "Finance", "Reporting", "FinanceAdHocReportService.cs"));

        source.Should().Contain("CreateExecutionStrategy()",
            "SQL Server retrying execution is enabled for the application DbContext");
        source.Split("ExecuteInTransactionAsync", StringSplitOptions.None).Length.Should().Be(4,
            "create, update, and delete must each execute as one retryable transaction");
        source.Should().NotContain(".Database.BeginTransactionAsync",
            "a user-initiated transaction outside the execution strategy causes saves to fail");
        source.Split("_db.ChangeTracker.Clear()", StringSplitOptions.None).Length.Should().BeGreaterThan(3,
            "rolled-back tracked state must not leak into a retried lifecycle operation");
    }

    [Theory]
    [InlineData("GetWorkspace", FinancePermissions.BuildAdHocReports)]
    [InlineData("Create", FinancePermissions.BuildAdHocReports)]
    [InlineData("Execute", FinancePermissions.RunFinanceReports)]
    [InlineData("Export", FinancePermissions.ExportFinanceReports)]
    public void PolicyMap_UsesPurposeSpecificReportAuthority(string action, string permission)
    {
        FinancePermissionPolicyMap.GetRequiredPolicies(
            "FinanceAdHocReportsController", action, ["POST"], [""])
            .Should().ContainSingle().Which.Should().Be(permission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCatalogue_AsksProviderAboutEachDefinitionBeforeExposingMetadata(
        bool bypassCustomRoleFiltering)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allowedId = Guid.NewGuid();
        var privateId = Guid.NewGuid();
        var reports = new[]
        {
            SystemReport("Visible Finance analysis", allowedId, tenantId),
            SystemReport("Another analyst's private report", privateId, tenantId)
        };
        var reportRepository = new Mock<IReportRepository>();
        reportRepository.Setup(repository => repository.GetReportsByTenantAsync(tenantId, null, null))
            .ReturnsAsync(reports);
        reportRepository.Setup(repository => repository.IsReportFavoriteAsync(
                It.IsAny<Guid>(), userId, tenantId))
            .ReturnsAsync(false);
        var roleAssignments = new Mock<IReportRoleAssignmentRepository>();
        roleAssignments.Setup(repository => repository.GetAccessibleReportIdsForUserAsync(userId, tenantId))
            .ReturnsAsync(Array.Empty<Guid>());
        roleAssignments.Setup(repository => repository.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ReportRoleAssignment, bool>>>()))
            .ReturnsAsync(Array.Empty<ReportRoleAssignment>());
        roleAssignments.Setup(repository => repository.GetAssignmentsByReportAsync(It.IsAny<Guid>(), tenantId))
            .ReturnsAsync(Array.Empty<ReportRoleAssignment>());

        var service = new DatabaseReportsService(
            reportRepository.Object,
            Mock.Of<IReportScheduleRepository>(),
            Mock.Of<IReportTemplateRepository>(),
            Mock.Of<IReportExecutionRepository>(),
            Mock.Of<IUserReportFavoriteRepository>(),
            Mock.Of<IReportExportRepository>(),
            roleAssignments.Object,
            NullLogger<DatabaseReportsService>.Instance,
            Mock.Of<IUnitOfWork>(),
            new ConfigurationBuilder().Build(),
            [new RecordVisibilityProvider(allowedId)]);

        var result = await service.GetReportsAsync(
            tenantId, userId, bypassRoleFiltering: bypassCustomRoleFiltering);

        result.Should().ContainSingle(item => item.Name == "Visible Finance analysis");
        result.Should().NotContain(item => item.Name.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SharedCatalogue_AuthorizesStaticProviderOnceForAllOfItsReports()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reports = new[]
        {
            StaticSystemReport("Static one", "system://static/one", tenantId),
            StaticSystemReport("Static two", "system://static/two", tenantId)
        };
        var reportRepository = new Mock<IReportRepository>();
        reportRepository.Setup(repository => repository.GetReportsByTenantAsync(tenantId, null, null))
            .ReturnsAsync(reports);
        reportRepository.Setup(repository => repository.IsReportFavoriteAsync(
                It.IsAny<Guid>(), userId, tenantId))
            .ReturnsAsync(false);
        var roleAssignments = new Mock<IReportRoleAssignmentRepository>();
        roleAssignments.Setup(repository => repository.GetAccessibleReportIdsForUserAsync(userId, tenantId))
            .ReturnsAsync(Array.Empty<Guid>());
        roleAssignments.Setup(repository => repository.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ReportRoleAssignment, bool>>>()))
            .ReturnsAsync(Array.Empty<ReportRoleAssignment>());
        roleAssignments.Setup(repository => repository.GetAssignmentsByReportAsync(It.IsAny<Guid>(), tenantId))
            .ReturnsAsync(Array.Empty<ReportRoleAssignment>());
        var provider = new ProviderWideVisibilityProvider();
        var service = new DatabaseReportsService(
            reportRepository.Object,
            Mock.Of<IReportScheduleRepository>(),
            Mock.Of<IReportTemplateRepository>(),
            Mock.Of<IReportExecutionRepository>(),
            Mock.Of<IUserReportFavoriteRepository>(),
            Mock.Of<IReportExportRepository>(),
            roleAssignments.Object,
            NullLogger<DatabaseReportsService>.Instance,
            Mock.Of<IUnitOfWork>(),
            new ConfigurationBuilder().Build(),
            [provider]);

        var result = await service.GetReportsAsync(tenantId, userId);

        result.Should().HaveCount(2);
        provider.AuthorizationCalls.Should().Be(1,
            "provider-wide permissions should not be queried once per catalogue row");
    }

    private static Report SystemReport(string name, Guid definitionId, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        Description = name,
        Type = "table",
        Status = "published",
        Query = FinanceAdHocReportValues.QueryPrefix + definitionId
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static Report StaticSystemReport(string name, string query, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        Description = name,
        Type = "table",
        Status = "published",
        Query = query
    };

    /// <summary>
    /// A small provider double that models one visible and one private persisted definition.
    /// The test protects the shared catalogue boundary, not merely the Finance endpoint itself.
    /// </summary>
    private sealed class RecordVisibilityProvider(Guid allowedId) : ISystemReportProvider
    {
        public bool RequiresRecordLevelReadAuthorization => true;
        public bool CanHandle(string? reportQuery) => OwnsIdentifier(reportQuery);
        public bool OwnsIdentifier(string? reportQuery) =>
            reportQuery?.StartsWith(FinanceAdHocReportValues.QueryPrefix, StringComparison.OrdinalIgnoreCase) == true;
        public string? ResolveCode(string? reportQuery) => reportQuery;
        public Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanReadReportAsync(string reportQuery, bool isAdministrator,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(reportQuery.EndsWith(allowedId.ToString(), StringComparison.OrdinalIgnoreCase));
        public Task<ReportResultDto> ExecuteAsync(string reportQuery, ExecuteReportDto request,
            bool isAdministrator, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task AuthorizeExportAsync(string reportQuery, bool isAdministrator,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ProviderWideVisibilityProvider : ISystemReportProvider
    {
        public int AuthorizationCalls { get; private set; }
        public bool CanHandle(string? reportQuery) => OwnsIdentifier(reportQuery);
        public bool OwnsIdentifier(string? reportQuery) =>
            reportQuery?.StartsWith("system://static/", StringComparison.OrdinalIgnoreCase) == true;
        public string? ResolveCode(string? reportQuery) => reportQuery;
        public Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
        {
            AuthorizationCalls++;
            return Task.FromResult(true);
        }

        public Task<ReportResultDto> ExecuteAsync(string reportQuery, ExecuteReportDto request,
            bool isAdministrator, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task AuthorizeExportAsync(string reportQuery, bool isAdministrator,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
