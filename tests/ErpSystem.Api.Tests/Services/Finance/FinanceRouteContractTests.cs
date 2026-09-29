using System.Text.RegularExpressions;
using ErpSystem.Api.Controllers.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Guards the frontend/backend route contract for the finance module: every apiService call in
/// frontend/src/services/finance must resolve to a registered backend controller route. This is
/// the defect family behind several PR review rounds (renamed routes, wrong verbs, endpoints
/// that never existed).
/// </summary>
public sealed class FinanceRouteContractTests
{
    /// <summary>
    /// Known, intentionally-unimplemented routes. Keep this empty; add entries only with a
    /// tracking note, so gaps are explicit instead of silently drifting.
    /// </summary>
    private static readonly IReadOnlySet<string> KnownGaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void EveryFinanceFrontendServiceCall_MustMatchARegisteredBackendRoute()
    {
        var root = FindRepositoryRoot();
        var backendRoutes = CollectBackendRoutes(Path.Combine(root, "src", "ErpSystem.Api", "Controllers"));
        backendRoutes.Should().NotBeEmpty("backend controllers should be discoverable");

        var frontendCalls = CollectFrontendCalls(Path.Combine(root, "frontend", "src", "services", "finance"));
        frontendCalls.Should().NotBeEmpty("finance frontend services should be discoverable");

        var misses = frontendCalls
            .Where(call => !KnownGaps.Contains($"{call.Verb} {call.Route}"))
            .Where(call => !MatchesAnyBackendRoute(call, backendRoutes))
            .Select(call => $"{call.Verb} {call.Route}  ({call.File})")
            .Distinct()
            .ToList();

        misses.Should().BeEmpty(
            "every finance frontend service call must hit a real backend route; " +
            "fix the route/verb or implement the endpoint rather than allowlisting");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Batch", "FinanceReviewHardening")]
    public void FinanceBrowserServices_ShouldNotShipLocalhostApiFallbacks()
    {
        var root = FindRepositoryRoot();
        var checkedFiles = new[]
        {
            Path.Combine(root, "frontend", "src", "services", "document-output.service.ts"),
            Path.Combine(root, "frontend", "src", "services", "finance", "finance-data.service.ts"),
            Path.Combine(root, "frontend", "src", "services", "finance", "fixed-assets-data.service.ts"),
        };

        foreach (var file in checkedFiles)
        {
            var source = File.ReadAllText(file);
            source.Should().NotContain("localhost:53484", $"{Path.GetFileName(file)} should use same-origin /api unless NEXT_PUBLIC_API_URL is configured");
        }

        var documentOutput = File.ReadAllText(checkedFiles[0]);
        documentOutput.Should().Contain("window.location.origin", "relative document render URLs should resolve against the deployed browser origin");
    }

    [Fact]
    [Trait("Category", "RouteContract")]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    public void OpeningBalanceApprovalWithoutDisplayRoute_ShouldDeepLinkToRequestedBatch()
    {
        var batchId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var result = FinanceApprovalsController.ResolveDetailHref(
            "OpeningBalanceBatch",
            batchId,
            displayUrl: null);

        result.Should().Be(
            "/finance/opening-balances?batchId=11111111-2222-3333-4444-555555555555");
    }

    [Theory]
    [Trait("Category", "RouteContract")]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [InlineData("VendorInvoice", "/finance/ap/invoices/ap-invoice-1")]
    [InlineData("Invoice", "/finance/ar/invoices/ar-invoice-1")]
    [InlineData("OpeningBalanceBatch", "/future/canonical/opening-balance-route")]
    public void ExistingDisplayRoute_ShouldRemainAuthoritative(string entityType, string displayUrl)
    {
        FinanceApprovalsController.ResolveDetailHref(entityType, Guid.NewGuid(), displayUrl)
            .Should().Be(displayUrl);
    }

    [Fact]
    [Trait("Category", "RouteContract")]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    public void UnknownApprovalWithoutDisplayRoute_ShouldRemainInFinanceWorkbench()
    {
        FinanceApprovalsController.ResolveDetailHref("FutureFinanceDocument", Guid.NewGuid(), "   ")
            .Should().Be("/finance/approvals");
    }

    [Theory]
    [Trait("Category", "RouteContract")]
    [Trait("Batch", "FinanceGoLive-ExchangeRates")]
    [InlineData("ExchangeRate")]
    [InlineData("exchange-rate")]
    [InlineData("EXCHANGE_RATE")]
    public void ExchangeRateWorkflow_ShouldBeVisibleInFinanceApprovalQueue(string entityType)
    {
        FinanceApprovalsController.IsFinanceEntity(entityType).Should().BeTrue(
            "exchange-rate facts, decisions, and approval outcomes are handled by the Finance workbench");
    }

    [Theory]
    [InlineData("AccountingBookLifecycle")]
    [InlineData("accounting_book_lifecycle")]
    public void AccountingBookLifecycle_ShouldBeVisibleButNotUseGenericApprovalAction(string entityType)
    {
        FinanceApprovalsController.IsFinanceQueueEntity(entityType).Should().BeTrue();
        FinanceApprovalsController.IsFinanceEntity(entityType).Should().BeFalse(
            "book lifecycle decisions must run through AccountingBookService");
    }

    [Theory]
    [InlineData("BusinessPartner")]
    [InlineData("business_partner")]
    public void BusinessPartner_ShouldBeVisibleInFinanceInboxButUseItsOwnDecisionEndpoint(string entityType)
    {
        var partnerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        FinanceApprovalsController.IsFinanceQueueEntity(entityType).Should().BeTrue();
        FinanceApprovalsController.IsFinanceEntity(entityType).Should().BeFalse(
            "Business Partner decisions must run through the Procurement status adapter");
        FinanceApprovalsController.ResolveDetailHref(entityType, partnerId, null).Should().Be(
            "/procurement/business-partners/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    }

    [Theory]
    [Trait("Category", "RouteContract")]
    [Trait("Requirement", "FR-GL-006")]
    [InlineData("RecurringJournalTemplate")]
    [InlineData("RecurringJournalOccurrence")]
    [InlineData("RecurringJournalOccurrenceWaiver")]
    public void RecurringJournalWorkflows_ShouldBeVisibleInFinanceApprovalQueue(string entityType)
    {
        FinanceApprovalsController.IsFinanceEntity(entityType).Should().BeTrue(
            "recurring-journal approvals must use the shared assignment-aware Finance workbench");
    }

    private sealed record FrontendCall(string Verb, string Route, string File);

    private static bool MatchesAnyBackendRoute(FrontendCall call, IReadOnlyList<(string Verb, string[] Segments)> backendRoutes)
    {
        var callSegments = call.Route.Trim('/').Split('/');

        foreach (var (verb, segments) in backendRoutes)
        {
            if (!string.Equals(verb, call.Verb, StringComparison.OrdinalIgnoreCase))
                continue;

            if (segments.Length != callSegments.Length)
                continue;

            var matches = true;
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "*" || callSegments[i] == "*")
                    continue;

                if (!string.Equals(segments[i], callSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return true;
        }

        return false;
    }

    private static IReadOnlyList<(string Verb, string[] Segments)> CollectBackendRoutes(string controllersDir)
    {
        var routes = new List<(string, string[])>();

        foreach (var file in Directory.EnumerateFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var routeAttrs = Regex.Matches(text, "\\[Route\\(\"([^\"]+)\"\\)\\]")
                .Select(m => (m.Index, Route: m.Groups[1].Value))
                .ToList();
            var classDecls = Regex.Matches(text, @"class\s+(\w+?)Controller\b")
                .Select(m => (m.Index, Name: m.Groups[1].Value))
                .ToList();

            foreach (Match http in Regex.Matches(text, "\\[Http(Get|Post|Put|Delete|Patch)(?:\\(\"([^\"]*)\"\\))?\\]"))
            {
                var basePath = string.Empty;
                foreach (var (idx, route) in routeAttrs)
                {
                    if (idx < http.Index) basePath = route; else break;
                }

                var className = string.Empty;
                foreach (var (idx, name) in classDecls)
                {
                    if (idx < http.Index) className = name; else break;
                }

                basePath = Regex.Replace(basePath, @"\[controller\]", className, RegexOptions.IgnoreCase);
                var template = http.Groups[2].Success ? http.Groups[2].Value : string.Empty;
                var full = ($"{basePath}/{template}").Replace("//", "/").Trim('/');

                // Strip the api prefix and normalize {param} tokens to wildcards.
                if (full.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
                    full = full["api/".Length..];
                full = Regex.Replace(full, @"\{[^}]+\}", "*");

                routes.Add((http.Groups[1].Value.ToUpperInvariant(), full.Split('/')));
            }
        }

        return routes;
    }

    private static IReadOnlyList<FrontendCall> CollectFrontendCalls(string servicesDir)
    {
        var calls = new List<FrontendCall>();

        foreach (var file in Directory.EnumerateFiles(servicesDir, "*.ts", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text,
                "apiService\\.(get|post|put|delete|patch)\\s*(?:<[^;]*?>)?\\s*\\(\\s*(`[^`]+`|'[^']+'|\"[^\"]+\")"))
            {
                var raw = m.Groups[2].Value[1..^1];
                var route = Regex.Replace(raw, @"\$\{[^}]+\}", "*").Replace("//", "/");
                // Normalize template expressions before removing a query string.
                // Otherwise the '?' in a ternary such as
                // `${isActive ? 'activate' : 'deactivate'}` is mistaken for the
                // beginning of a URL query and produces a phantom route failure.
                route = route.Split('?')[0];
                if (!route.StartsWith('/'))
                    continue; // dynamic base URL - out of scope

                // `${query}` suffixes carrying their own '?' leave a star glued to a word.
                route = Regex.Replace(route, @"([^/])\*$", "$1").TrimEnd('/');

                calls.Add(new FrontendCall(
                    m.Groups[1].Value.ToUpperInvariant(),
                    route.TrimStart('/'),
                    Path.GetFileName(file)));
            }
        }

        return calls;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
