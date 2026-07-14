using System.Text.RegularExpressions;
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
                raw = raw.Split('?')[0];

                var route = Regex.Replace(raw, @"\$\{[^}]+\}", "*").Replace("//", "/");
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
