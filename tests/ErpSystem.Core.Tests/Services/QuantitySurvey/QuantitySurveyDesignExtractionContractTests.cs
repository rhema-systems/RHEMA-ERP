using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyDesignExtractionContractTests
{
    private static readonly string[] CsvHeaders =
    [
        "ExternalLineId", "BoqLineKey", "BoqItemCode", "Description", "UnitCode",
        "PreviousQuantity", "RevisedQuantity", "ChangeType", "MeasurementCode",
        "SourceElementIds", "ExtractionMethod", "ConfidencePercent", "Comment"
    ];

    [Fact]
    public void Manifest_schema_is_strict_versioned_and_tenant_neutral()
    {
        using var schema = Parse("schemas/tdc-qs-design-extraction-manifest-v1.schema.json");
        var root = schema.RootElement;

        root.GetProperty("$schema").GetString().Should().Be("https://json-schema.org/draft/2020-12/schema");
        root.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Required(root).Should().Contain([
            "schemaVersion", "exchangeId", "adapterProfileCode", "projectReference",
            "designComparison", "sourceArtifacts", "lines", "controlTotals", "normalizedPayloadSha256"
        ]);
        root.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString()
            .Should().Be("tdc.qs.design-extraction.v1");

        var line = root.GetProperty("$defs").GetProperty("extractionLine");
        line.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        Required(line).Should().Contain([
            "externalLineId", "boqLineKey", "unitCode", "previousQuantity", "revisedQuantity",
            "changeType", "measurementCode", "sourceElementIds", "extractionMethod", "confidencePercent"
        ]);
        line.GetProperty("properties").TryGetProperty("tenantId", out _).Should().BeFalse();
        root.GetProperty("properties").TryGetProperty("actorUserId", out _).Should().BeFalse();
    }

    [Fact]
    public void Manifest_example_has_valid_lineage_identity_hashes_and_control_totals()
    {
        using var example = Parse("examples/tdc-qs-design-extraction-manifest-v1.example.json");
        var root = example.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be("tdc.qs.design-extraction.v1");
        Guid.TryParse(root.GetProperty("exchangeId").GetString(), out _).Should().BeTrue();
        root.TryGetProperty("tenantId", out _).Should().BeFalse();
        root.TryGetProperty("userId", out _).Should().BeFalse();
        Hash(root.GetProperty("normalizedPayloadSha256").GetString()).Should().BeTrue();

        var comparison = root.GetProperty("designComparison");
        var previous = comparison.GetProperty("previousDrawing");
        var revised = comparison.GetProperty("revisedDrawing");
        revised.GetProperty("drawingNumber").GetString().Should().Be(previous.GetProperty("drawingNumber").GetString());
        revised.GetProperty("revision").GetString().Should().NotBe(previous.GetProperty("revision").GetString());

        var artifacts = root.GetProperty("sourceArtifacts").EnumerateArray().ToArray();
        artifacts.Should().NotBeEmpty();
        artifacts.Should().OnlyContain(item => Hash(item.GetProperty("sha256").GetString()));

        var lines = root.GetProperty("lines").EnumerateArray().ToArray();
        lines.Select(item => item.GetProperty("externalLineId").GetString()).Should().OnlyHaveUniqueItems();
        lines.SelectMany(item => item.GetProperty("sourceElementIds").EnumerateArray().Select(value => value.GetString()))
            .Should().OnlyHaveUniqueItems();

        var totals = root.GetProperty("controlTotals");
        totals.GetProperty("lineCount").GetInt32().Should().Be(lines.Length);
        var previousTotal = lines.Sum(item => item.GetProperty("previousQuantity").GetDecimal());
        var revisedTotal = lines.Sum(item => item.GetProperty("revisedQuantity").GetDecimal());
        totals.GetProperty("previousQuantityTotal").GetDecimal().Should().Be(previousTotal);
        totals.GetProperty("revisedQuantityTotal").GetDecimal().Should().Be(revisedTotal);
        totals.GetProperty("netQuantityChange").GetDecimal().Should().Be(revisedTotal - previousTotal);
    }

    [Fact]
    public void Csv_profile_matches_manifest_order_identity_and_totals()
    {
        var rows = File.ReadAllLines(PathFor("examples/tdc-qs-design-extraction-lines-v1.csv"));
        rows.Should().HaveCountGreaterThan(1);
        rows[0].Split(',').Should().Equal(CsvHeaders);

        var values = rows.Skip(1).Select(row => row.Split(',')).ToArray();
        values.Should().OnlyContain(row => row.Length == CsvHeaders.Length);
        values.Select(row => row[0]).Should().OnlyHaveUniqueItems();

        using var manifest = Parse("examples/tdc-qs-design-extraction-manifest-v1.example.json");
        var manifestLines = manifest.RootElement.GetProperty("lines").EnumerateArray().ToArray();
        values.Select(row => row[0]).Should().Equal(
            manifestLines.Select(line => line.GetProperty("externalLineId").GetString()));
        values.Sum(row => decimal.Parse(row[5], System.Globalization.CultureInfo.InvariantCulture)).Should()
            .Be(manifestLines.Sum(line => line.GetProperty("previousQuantity").GetDecimal()));
        values.Sum(row => decimal.Parse(row[6], System.Globalization.CultureInfo.InvariantCulture)).Should()
            .Be(manifestLines.Sum(line => line.GetProperty("revisedQuantity").GetDecimal()));
    }

    [Fact]
    public void Reconciliation_contract_is_strict_hash_bound_and_blocks_unresolved_lines()
    {
        using var schema = Parse("schemas/tdc-qs-design-extraction-reconciliation-v1.schema.json");
        schema.RootElement.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        schema.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString()
            .Should().Be("tdc.qs.design-extraction.reconciliation.v1");

        using var example = Parse("examples/tdc-qs-design-extraction-reconciliation-v1.example.json");
        var root = example.RootElement;
        Hash(root.GetProperty("manifestSha256").GetString()).Should().BeTrue();
        Hash(root.GetProperty("reconciliationSha256").GetString()).Should().BeTrue();
        root.GetProperty("status").GetString().Should().Be("NeedsResolution");

        var lines = root.GetProperty("lines").EnumerateArray().ToArray();
        var issues = root.GetProperty("blockingIssues").EnumerateArray().ToArray();
        issues.Should().Contain(item => item.GetProperty("severity").GetString() == "Error");
        lines.Should().Contain(item => item.GetProperty("outcome").GetString() == "NewExternalLine" &&
                                       item.GetProperty("proposedRoute").GetString() == "Unresolved");
        root.GetProperty("summary").GetProperty("externalLineCount").GetInt32().Should().Be(lines.Length);
    }

    [Fact]
    public void Human_contract_preserves_central_owners_and_non_mutating_boundary()
    {
        var contract = File.ReadAllText(PathFor("README.md"));

        contract.Should().Contain("central Document Management System (DMS)");
        contract.Should().Contain("real `Clean` outcome");
        contract.Should().Contain("macro-enabled workbooks");
        contract.Should().Contain("client-provided tenant");
        contract.Should().Contain("maker/checker");
        contract.Should().Contain("Idempotency binds `exchangeId`");
        contract.Should().Contain("never updates a BoQ, creates a variation, or records a measurement directly");
        contract.Should().Contain("`QS-CFG-014`");
        contract.Should().Contain("`QS-0405` remains `In progress`");
    }

    private static HashSet<string> Required(JsonElement element) =>
        element.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).ToHashSet();

    private static bool Hash(string? value) =>
        value is not null && Regex.IsMatch(value, "^[A-F0-9]{64}$", RegexOptions.CultureInvariant);

    private static JsonDocument Parse(string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(PathFor(relativePath)));

    private static string PathFor(string relativePath) =>
        Path.Combine(ContractRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string ContractRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "integrations", "quantity-survey");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("The Quantity Survey design-extraction contract folder was not found.");
    }
}
