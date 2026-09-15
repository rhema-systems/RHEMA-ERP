using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

const string AuthoritySchema = "RHEMA_CHECK_CONSTRAINT_DUAL_AUTHORITY_V1";
const string SourceSchema = "RHEMA_RAW_CHECK_SOURCE_V2";
const string ActualSchema = "RHEMA_RAW_CHECK_ACTUAL_V2";
const string Comparator = "ORDINAL_ASCII_TABLE_THEN_NAME_V1";
const string KeySerialization = "COUNT32BE|TABLE_LENGTH32BE|TABLE_UTF8|NAME_LENGTH32BE|NAME_UTF8";
const string BaselineFile = "20260913162402_DisposableDevelopmentCurrentModelBaseline.cs";
const string BaselineBlob = "8ab077153bbed273f6302b156ceccbbad1c8b0d6";
const int BaselineBytes = 8998463;
const string BaselineSha256 = "B9FE09EFA8538107B5230095FA9292F9C5B4C3E85DC5DD4F322CFFDBD3EDD578";
const string TerminalManifestSha256 = "858FC627204D5A78DF181E1351828E56DAE1AAC1296939CA2184C770A5EE35EC";
const string TerminalOutcome = "RAW_CHECK_CAPTURE_COMPLETE_REVIEW_REQUIRED_DROPPED";
const string ReviewedCommit = "dac0ce8eb9fad30b65584ee4f90a17ea88f7e4f5";
const string ReviewedTree = "39013e19b887c2c7a8d9767089e709b9b58dc7d4";
const string SourceCorpusSha256 = "716A7DD2CB8584B565C5EFD572582EE04F62F23A377D5D5FDAACEBD5BEBC229C";
const string ActualCorpusSha256 = "ECE6645A27F20545629F647E4EAF52E51FD012B635BD56D30E6F920F3634C0CE";
const string ComparisonSha256 = "C603CFE64EC04613CCB33C229F1F78916023AB0C3401820A6B8FA7034A7674C7";
const string KeysetSha256 = "46593A80E6CAF68DDC12A2F76C0BBF7ACCF0D8F6F8D774A75FBFB6D58D7BBCB3";
const string PreDropManifestSha256 = "0EC548B529B639FB50F19950101B6F3E8C359B245A25C98EDB5E4C112711DF34";
const string SourceIdentitySetSha256 = "6DE783A26DA95BC08A5434FCF8D910A711ACC0F72458D6F4C45B2E2E6B00D783";
const string StorageIdentitySetSha256 = "C7927443B4F57E4B161C594D10EA894B4290D7E888D8E0D3E29CEEA0443EEA67";
const string SemanticIdentitySetSha256 = "F7E31699B655C81901268BAE0A239170A88FA85CC8E073B0076F9EDCD25684E6";

if (args.Length == 1 && args[0] == "--self-test")
{
    SelfTest();
    Console.WriteLine("PASS: structural check-constraint identity preserves reviewed semantic boundaries.");
    return;
}
if (args.Length == 3 && args[0] == "--verify")
{
    var authority = LoadAuthority(args[2]);
    VerifySource(args[1], authority);
    Console.WriteLine("PASS: committed dual authority matches the exact baseline source identity.");
    return;
}
if (args.Length != 6 || args[0] != "--generate")
    throw new InvalidOperationException("Usage: --generate <baseline.cs> <source.jsonl> <actual.jsonl> <terminal-manifest.json> <output.json> | --verify <baseline.cs> <authority.json> | --self-test");

Generate(args[1], args[2], args[3], args[4], args[5]);

static void Generate(string baselinePath, string sourcePath, string actualPath, string terminalPath, string outputPath)
{
    var baselineBytes = ReadAndValidateBaselineGitBlob(baselinePath);
    ValidateCaptureProvenance(sourcePath, actualPath, terminalPath);
    var extracted = ExtractSource(baselineBytes);
    var captured = ReadRows<SourceRow>(sourcePath);
    var actual = ReadRows<ActualRow>(actualPath);
    ValidateRows(captured, SourceSchema);
    ValidateRows(actual, ActualSchema);
    if (captured.Length != 835 || actual.Length != 835 || extracted.Count != 835)
        throw new InvalidOperationException("Dual authority requires exactly 835 source and storage records.");

    var entries = new List<AuthorityEntry>(835);
    for (var i = 0; i < 835; i++)
    {
        var source = captured[i];
        var storage = actual[i];
        var definition = extracted[i];
        if (!SameTuple(source.table, source.name, storage.table, storage.name) ||
            !SameTuple(source.table, source.name, definition.Table, definition.Name))
            throw new InvalidOperationException($"Positional tuple drift at ordinal {i}.");
        var capturedSource = ValidateRaw(source.originalUtf16LeBase64, source.originalUtf16LeBytes,
            source.originalUtf16LeSha256, source.normalizedUtf8Base64, source.normalizedUtf8Bytes,
            source.normalizedUtf8Sha256, source.normalizedUtf8Encoding, source.key);
        var storageRaw = ValidateRaw(storage.originalUtf16LeBase64, storage.originalUtf16LeBytes,
            storage.originalUtf16LeSha256, storage.normalizedUtf8Base64, storage.normalizedUtf8Bytes,
            storage.normalizedUtf8Sha256, storage.normalizedUtf8Encoding, storage.key);
        if (!StringComparer.Ordinal.Equals(capturedSource, definition.Raw))
            throw new InvalidOperationException("Captured source raw body differs from exact baseline token: " + source.key);
        if (storage.serverDataLength != storage.originalUtf16LeBytes ||
            !StringComparer.Ordinal.Equals(storage.serverUtf16LeSha256, storage.originalUtf16LeSha256))
            throw new InvalidOperationException("Server/client storage identity differs: " + storage.key);
        var sourceSemantic = Semantic(capturedSource);
        var storageSemantic = Semantic(storageRaw);
        if (!StringComparer.Ordinal.Equals(sourceSemantic, storageSemantic))
            throw new InvalidOperationException("Genuine semantic check-constraint drift: " + source.key);
        entries.Add(new AuthorityEntry(i, source.table, source.name,
            source.originalUtf16LeBytes, source.originalUtf16LeSha256,
            source.normalizedUtf8Bytes, source.normalizedUtf8Sha256,
            storage.originalUtf16LeBytes, storage.originalUtf16LeSha256,
            storage.normalizedUtf8Bytes, storage.normalizedUtf8Sha256,
            sourceSemantic));
    }

    var document = new AuthorityDocument(
        AuthoritySchema, 835, Comparator, KeySerialization, KeysetSha256,
        new BaselineBinding(BaselineFile, "100644", BaselineBlob, BaselineBytes, BaselineSha256),
        new CaptureBinding("GL-Scratch-Baseline-Evidence-20260915-15", TerminalManifestSha256,
            TerminalOutcome, ReviewedCommit, ReviewedTree, SourceCorpusSha256, ActualCorpusSha256,
            ComparisonSha256, PreDropManifestSha256, "CATALOG_DROPPED", false),
        new SemanticBinding("SCRIPT_DOM_TSQL160_CLOSED_V1", "BUILTIN_FUNCTION_IDENTIFIERS_CASE_INSENSITIVE_V1",
            "SHA256_UTF16LE_LENGTH_PREFIXED_STRUCTURAL_TREE_V2", 835, 0, 0),
        Aggregate(entries, AuthorityIdentityKind.Source),
        Aggregate(entries, AuthorityIdentityKind.Storage),
        Aggregate(entries, AuthorityIdentityKind.Semantic), entries.ToArray());
    WriteNewJson(outputPath, document);
    Console.WriteLine("PASS: generated 835-row source/storage/structural check-constraint authority with zero semantic drift.");
}

static AuthorityDocument LoadAuthority(string path)
{
    var value = JsonSerializer.Deserialize<AuthorityDocument>(File.ReadAllText(path), JsonOptions())
        ?? throw new InvalidOperationException("Authority document is null.");
    if (value.schema != AuthoritySchema || value.rowCount != 835 || value.comparator != Comparator ||
        value.keySerialization != KeySerialization || value.keysetSha256 != KeysetSha256 ||
        value.entries.Length != 835 || value.semantic.semanticMatchCount != 835 ||
        value.semantic.semanticDriftCount != 0 || value.semantic.unsupportedCount != 0 ||
        value.semantic.grammar != "SCRIPT_DOM_TSQL160_CLOSED_V1" ||
        value.semantic.functionIdentifierPolicy != "BUILTIN_FUNCTION_IDENTIFIERS_CASE_INSENSITIVE_V1" ||
        value.semantic.encoding != "SHA256_UTF16LE_LENGTH_PREFIXED_STRUCTURAL_TREE_V2" ||
        value.sourceIdentitySetSha256 != SourceIdentitySetSha256 ||
        value.storageIdentitySetSha256 != StorageIdentitySetSha256 ||
        value.semanticIdentitySetSha256 != SemanticIdentitySetSha256 ||
        value.sourceIdentitySetSha256 != Aggregate(value.entries, AuthorityIdentityKind.Source) ||
        value.storageIdentitySetSha256 != Aggregate(value.entries, AuthorityIdentityKind.Storage) ||
        value.semanticIdentitySetSha256 != Aggregate(value.entries, AuthorityIdentityKind.Semantic) ||
        value.baseline != new BaselineBinding(BaselineFile, "100644", BaselineBlob, BaselineBytes, BaselineSha256) ||
        value.capture != new CaptureBinding("GL-Scratch-Baseline-Evidence-20260915-15", TerminalManifestSha256,
            TerminalOutcome, ReviewedCommit, ReviewedTree, SourceCorpusSha256, ActualCorpusSha256,
            ComparisonSha256, PreDropManifestSha256, "CATALOG_DROPPED", false))
        throw new InvalidOperationException("Authority header or provenance drifted.");
    string? priorTable = null, priorName = null;
    for (var i = 0; i < value.entries.Length; i++)
    {
        var entry = value.entries[i];
        if (entry.ordinal != i || !SafeIdentifier(entry.table) || !SafeIdentifier(entry.name) ||
            priorTable is not null && CompareTuple(priorTable, priorName!, entry.table, entry.name) >= 0 ||
            entry.sourceOriginalUtf16LeBytes <= 0 || entry.sourceNormalizedUtf8Bytes <= 0 ||
            entry.storageOriginalUtf16LeBytes <= 0 || entry.storageNormalizedUtf8Bytes <= 0 ||
            !IsSha(entry.sourceOriginalUtf16LeSha256) || !IsSha(entry.sourceNormalizedUtf8Sha256) ||
            !IsSha(entry.storageOriginalUtf16LeSha256) || !IsSha(entry.storageNormalizedUtf8Sha256) ||
            !IsSha(entry.semanticSha256))
            throw new InvalidOperationException("Authority entry is malformed or unordered at ordinal " + i);
        priorTable = entry.table; priorName = entry.name;
    }
    return value;
}

static void VerifySource(string baselinePath, AuthorityDocument authority)
{
    var baselineBytes = ReadAndValidateBaselineGitBlob(baselinePath);
    var source = ExtractSource(baselineBytes);
    if (source.Count != authority.entries.Length) throw new InvalidOperationException("Authority row count drifted.");
    for (var i = 0; i < source.Count; i++)
    {
        var expected = authority.entries[i];
        var actual = source[i];
        var identity = Identity(actual.Raw);
        if (expected.ordinal != i || !SameTuple(expected.table, expected.name, actual.Table, actual.Name) ||
            expected.sourceOriginalUtf16LeBytes != identity.utf16.Length ||
            expected.sourceOriginalUtf16LeSha256 != Hash(identity.utf16) ||
            expected.sourceNormalizedUtf8Bytes != identity.wtf8.Length ||
            expected.sourceNormalizedUtf8Sha256 != Hash(identity.wtf8) ||
            expected.semanticSha256 != Semantic(actual.Raw))
            throw new InvalidOperationException("Source authority drifted: " + actual.Table + "|" + actual.Name);
    }
}

static void ValidateCaptureProvenance(string sourcePath, string actualPath, string terminalPath)
{
    if (HashFile(sourcePath) != SourceCorpusSha256 || HashFile(actualPath) != ActualCorpusSha256 ||
        HashFile(terminalPath) != TerminalManifestSha256)
        throw new InvalidOperationException("Pinned raw-capture artifact identity drifted.");
    using var json = JsonDocument.Parse(File.ReadAllText(terminalPath));
    var root = json.RootElement;
    if (root.GetProperty("schema").GetString() != "RHEMA_SCRATCH_BASELINE_EVIDENCE_V1" ||
        root.GetProperty("outcome").GetString() != TerminalOutcome ||
        root.GetProperty("reviewedCommit").GetString() != ReviewedCommit ||
        root.GetProperty("reviewedTree").GetString() != ReviewedTree ||
        root.GetProperty("durableStage").GetString() != "CATALOG_DROPPED")
        throw new InvalidOperationException("Terminal capture provenance drifted.");
    var artifacts = root.GetProperty("artifacts").EnumerateArray().ToDictionary(
        x => x.GetProperty("name").GetString()!, x => x.GetProperty("sha256").GetString()!, StringComparer.Ordinal);
    RequireArtifact(artifacts, "check-constraint-source-raw.jsonl", SourceCorpusSha256);
    RequireArtifact(artifacts, "check-constraint-actual-raw.jsonl", ActualCorpusSha256);
    RequireArtifact(artifacts, "check-constraint-raw-identity-comparison.jsonl", ComparisonSha256);
    RequireArtifact(artifacts, "review-required-pre-drop-manifest.json", PreDropManifestSha256);
}

static void RequireArtifact(Dictionary<string,string> artifacts, string name, string hash)
{
    if (!artifacts.TryGetValue(name, out var actual) || actual != hash)
        throw new InvalidOperationException("Terminal manifest artifact binding drifted: " + name);
}

static byte[] ReadAndValidateBaselineGitBlob(string path)
{
    var fullPath = Path.GetFullPath(path);
    var repository = GitText(Path.GetDirectoryName(fullPath)!, "rev-parse", "--show-toplevel").Trim();
    var relative = Path.GetRelativePath(repository, fullPath).Replace('\\', '/');
    if (relative.StartsWith("../", StringComparison.Ordinal) || Path.GetFileName(fullPath) != BaselineFile)
        throw new InvalidOperationException("Baseline path is outside the Git repository.");
    var tree = GitText(repository, "ls-tree", "HEAD", "--", relative).Trim();
    var match = Regex.Match(tree, "\\A(?<mode>[0-9]{6}) blob (?<oid>[0-9a-f]{40})\\t(?<path>.+)\\z", RegexOptions.CultureInvariant);
    if (!match.Success || match.Groups["mode"].Value != "100644" || match.Groups["oid"].Value != BaselineBlob ||
        match.Groups["path"].Value != relative)
        throw new InvalidOperationException("Baseline Git tree binding drifted.");
    var bytes = GitBytes(repository, "cat-file", "blob", BaselineBlob);
    if (bytes.Length != BaselineBytes ||
        Hash(bytes) != BaselineSha256 || GitBlobId(bytes) != BaselineBlob)
        throw new InvalidOperationException("Baseline raw Git identity drifted.");
    return bytes;
}

static List<SourceDefinition> ExtractSource(byte[] bytes)
{
    var text = new UTF8Encoding(false, true).GetString(bytes);
    var currentTable = "";
    var result = new List<SourceDefinition>();
    var unique = new HashSet<(string,string)>();
    foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
    {
        var table = Regex.Match(line, "^\\s*name: \\\"(?<name>(?:\\\\.|[^\\\"\\\\])*)\\\",\\s*$");
        if (table.Success) currentTable = Regex.Unescape(table.Groups["name"].Value);
        var check = Regex.Match(line, "^\\s*table\\.CheckConstraint\\(\\\"(?<name>(?:\\\\.|[^\\\"\\\\])*)\\\", \\\"(?<sql>(?:\\\\.|[^\\\"\\\\])*)\\\"\\);\\s*$");
        if (!check.Success) continue;
        var name = Regex.Unescape(check.Groups["name"].Value);
        var sql = Regex.Unescape(check.Groups["sql"].Value);
        if (!SafeIdentifier(currentTable) || !SafeIdentifier(name) || !unique.Add((currentTable, name)))
            throw new InvalidOperationException("Unsafe or duplicate baseline check constraint.");
        result.Add(new SourceDefinition(currentTable, name, sql));
    }
    result.Sort((a,b) => CompareTuple(a.Table, a.Name, b.Table, b.Name));
    return result;
}

static T[] ReadRows<T>(string path) => File.ReadLines(path).Select(line =>
    JsonSerializer.Deserialize<T>(line, JsonOptions()) ?? throw new InvalidOperationException("Null raw row.")).ToArray();

static void ValidateRows<T>(IReadOnlyList<T> rows, string schema) where T : IRawRow
{
    string? priorTable = null, priorName = null;
    var unique = new HashSet<(string,string)>();
    foreach (var row in rows)
    {
        if (row.schema != schema || !SafeIdentifier(row.table) || !SafeIdentifier(row.name) ||
            row.key != row.table + "|" + row.name || !unique.Add((row.table, row.name)) ||
            priorTable is not null && CompareTuple(priorTable, priorName!, row.table, row.name) >= 0)
            throw new InvalidOperationException("Raw corpus keyset/order is invalid.");
        priorTable = row.table; priorName = row.name;
    }
}

static string ValidateRaw(string base64, int bytes, string sha, string normalizedBase64, int normalizedBytes,
    string normalizedSha, string encoding, string key)
{
    var rawBytes = Convert.FromBase64String(base64);
    if (rawBytes.Length != bytes || Hash(rawBytes) != sha) throw new InvalidOperationException("Raw identity mismatch: " + key);
    var raw = DecodeUtf16Le(rawBytes);
    var normalized = EncodeWtf8(NormalizeLf(raw));
    if (encoding != "WTF8_CODE_UNIT_PRESERVING" || normalized.Length != normalizedBytes ||
        Hash(normalized) != normalizedSha || !normalized.AsSpan().SequenceEqual(Convert.FromBase64String(normalizedBase64)))
        throw new InvalidOperationException("Normalized identity mismatch: " + key);
    return raw;
}

static string Semantic(string sql) => Hash(Encoding.UTF8.GetBytes(Boolean(Parse(NormalizeLf(sql)))));
static BooleanExpression Parse(string sql)
{
    var fragment = new TSql160Parser(true).Parse(new StringReader("SELECT 1 WHERE " + sql + ";"), out var errors);
    if (errors.Count != 0) throw new InvalidOperationException($"Unsupported SQL grammar {errors[0].Number}.");
    return ((QuerySpecification)((SelectStatement)((TSqlScript)fragment).Batches.Single().Statements.Single()).QueryExpression).WhereClause.SearchCondition;
}
static string Boolean(BooleanExpression value) => value switch
{
    BooleanParenthesisExpression x => Boolean(x.Expression),
    BooleanBinaryExpression x => BooleanBinary(x),
    BooleanComparisonExpression x => Comparison(x.ComparisonType.ToString(), x.FirstExpression, x.SecondExpression),
    BooleanTernaryExpression x => Ternary(x),
    InPredicate x => In(x),
    BooleanNotExpression x => Pack("NOT", Boolean(x.Expression)),
    BooleanIsNullExpression x => Pack(x.IsNot ? "IS_NOT_NULL" : "IS_NULL", Scalar(x.Expression)),
    LikePredicate x => Like(x),
    _ => throw new InvalidOperationException("Unsupported boolean node " + value.GetType().Name)
};
static string BooleanBinary(BooleanBinaryExpression value)
{
    var items = new List<string>(); Add(value.FirstExpression); Add(value.SecondExpression); items.Sort(StringComparer.Ordinal);
    return Pack(value.BinaryExpressionType.ToString().ToUpperInvariant(), items.ToArray());
    void Add(BooleanExpression item)
    {
        while (item is BooleanParenthesisExpression p) item = p.Expression;
        if (item is BooleanBinaryExpression b && b.BinaryExpressionType == value.BinaryExpressionType) { Add(b.FirstExpression); Add(b.SecondExpression); }
        else if (value.BinaryExpressionType == BooleanBinaryExpressionType.And && item is BooleanTernaryExpression t && !t.TernaryExpressionType.ToString().StartsWith("Not", StringComparison.OrdinalIgnoreCase))
        { items.Add(Comparison("GreaterThanOrEqualTo", t.FirstExpression, t.SecondExpression)); items.Add(Comparison("LessThanOrEqualTo", t.FirstExpression, t.ThirdExpression)); }
        else if (value.BinaryExpressionType == BooleanBinaryExpressionType.Or && item is InPredicate i && !i.NotDefined)
            items.AddRange(i.Values.Select(x => Comparison("Equals", i.Expression, x)));
        else items.Add(Boolean(item));
    }
}
static string Comparison(string kind, ScalarExpression first, ScalarExpression second)
{
    var left = Scalar(first); var right = Scalar(second);
    if (kind is "Equals" or "NotEqualToBrackets" or "NotEqualToExclamation")
    {
        if (StringComparer.Ordinal.Compare(left, right) > 0) (left, right) = (right, left);
        if (kind.StartsWith("NotEqual", StringComparison.Ordinal)) kind = "NotEqual";
    }
    return Pack("CMP_" + kind.ToUpperInvariant(), left, right);
}
static string Ternary(BooleanTernaryExpression value)
{
    var terms = new[] { Comparison("GreaterThanOrEqualTo", value.FirstExpression, value.SecondExpression), Comparison("LessThanOrEqualTo", value.FirstExpression, value.ThirdExpression) }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var result = Pack("AND", terms); return value.TernaryExpressionType.ToString().StartsWith("Not", StringComparison.OrdinalIgnoreCase) ? Pack("NOT", result) : result;
}
static string In(InPredicate value)
{
    if (value.Subquery is not null || value.Values.Count == 0)
        throw new InvalidOperationException("IN predicates require a nonempty reviewed literal/expression list; subqueries are unsupported.");
    var terms = value.Values.Select(x => Comparison("Equals", value.Expression, x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    var result = terms.Length == 1 ? terms[0] : Pack("OR", terms); return value.NotDefined ? Pack("NOT", result) : result;
}
static string Like(LikePredicate value)
{
    if (value.OdbcEscape) throw new InvalidOperationException("ODBC LIKE escape syntax is unsupported.");
    var result = Pack("LIKE", Scalar(value.FirstExpression), Scalar(value.SecondExpression),
        value.EscapeExpression is null ? "" : Scalar(value.EscapeExpression));
    return value.NotDefined ? Pack("NOT", result) : result;
}
static string Scalar(ScalarExpression value)
{
    var core = ScalarCore(value);
    return value is PrimaryExpression p && p.Collation is not null ? Pack("COLLATE", core, p.Collation.Value) : core;
}
static string ScalarCore(ScalarExpression value) => value switch
{
    ParenthesisExpression x => Scalar(x.Expression),
    ColumnReferenceExpression x => Column(x),
    IntegerLiteral x => Literal(x, LiteralType.Integer, "INTEGER"), NumericLiteral x => Literal(x, LiteralType.Numeric, "NUMERIC"),
    RealLiteral x => Literal(x, LiteralType.Real, "REAL"), MoneyLiteral x => Literal(x, LiteralType.Money, "MONEY"),
    StringLiteral x => StringLiteralValue(x), NullLiteral x => Literal(x, LiteralType.Null, "NULL"),
    FunctionCall x => Function(x), LeftFunctionCall x => Pack("FUNC_LEFT", x.Parameters.Select(Scalar).ToArray()),
    RightFunctionCall x => Pack("FUNC_RIGHT", x.Parameters.Select(Scalar).ToArray()),
    BinaryExpression x => Pack("BINARY_" + x.BinaryExpressionType, Scalar(x.FirstExpression), Scalar(x.SecondExpression)),
    UnaryExpression x => Pack("UNARY_" + x.UnaryExpressionType, Scalar(x.Expression)),
    NullIfExpression x => Pack("NULLIF", Scalar(x.FirstExpression), Scalar(x.SecondExpression)),
    ConvertCall x => Pack("CONVERT", Fragment(x.DataType), Scalar(x.Parameter), x.Style is null ? "" : Scalar(x.Style)),
    CastCall x => Pack("CAST", Scalar(x.Parameter), Fragment(x.DataType)),
    SearchedCaseExpression x => Pack("SEARCHED_CASE", x.WhenClauses.Select(w => Pack("WHEN", Boolean(w.WhenExpression), Scalar(w.ThenExpression))).Append(Pack("ELSE", x.ElseExpression is null ? "" : Scalar(x.ElseExpression))).ToArray()),
    SimpleCaseExpression x => Pack("SIMPLE_CASE", new[] { Scalar(x.InputExpression) }.Concat(x.WhenClauses.Select(w => Pack("WHEN", Scalar(w.WhenExpression), Scalar(w.ThenExpression)))).Append(Pack("ELSE", x.ElseExpression is null ? "" : Scalar(x.ElseExpression))).ToArray()),
    _ => throw new InvalidOperationException("Unsupported scalar node " + value.GetType().Name)
};
static string Column(ColumnReferenceExpression value)
{
    if (value.ColumnType != ColumnType.Regular || value.MultiPartIdentifier is null || value.MultiPartIdentifier.Identifiers.Count == 0)
        throw new InvalidOperationException("Only nonempty regular column references are supported in check authority.");
    return Pack("COLUMN", value.MultiPartIdentifier.Identifiers.Select(i => i.Value).ToArray());
}
static string Literal(Literal value, LiteralType expected, string tag)
{
    if (value.LiteralType != expected) throw new InvalidOperationException("Literal node/type identity is inconsistent.");
    return value is NullLiteral ? Pack(tag) : Pack(tag, value.Value);
}
static string StringLiteralValue(StringLiteral value)
{
    if (value.LiteralType != LiteralType.String || value.IsLargeObject)
        throw new InvalidOperationException("Unreviewed string literal modifier.");
    return Pack(value.IsNational ? "NSTRING" : "ASTRING", value.Value);
}
static string Function(FunctionCall value)
{
    if (value.OverClause is not null || value.WithinGroupClause is not null || value.JsonOrderByClause is not null ||
        value.TrimOptions is not null || value.WithArrayWrapper || value.IgnoreRespectNulls.Count != 0 ||
        value.JsonParameters.Count != 0 || value.AbsentOrNullOnNull.Count != 0 || value.ReturnType.Count != 0)
        throw new InvalidOperationException("Unreviewed function modifier in check constraint.");
    if (value.CallTarget is not null) throw new InvalidOperationException("Qualified/user-defined functions are unsupported.");
    if (value.FunctionName.QuoteType != QuoteType.NotQuoted)
        throw new InvalidOperationException("Quoted function identifiers are unsupported.");
    var name = value.FunctionName.Value.ToUpperInvariant();
    var allowed = new HashSet<string>(StringComparer.Ordinal) { "ABS", "ASCII", "CHARINDEX", "DATALENGTH", "DATEADD", "DATEDIFF", "DATEPART", "DAY", "ISJSON", "ISNULL", "LEFT", "LEN", "LOWER", "LTRIM", "MONTH", "PATINDEX", "REPLACE", "REPLICATE", "RIGHT", "ROUND", "RTRIM", "SUBSTRING", "UPPER", "YEAR" };
    if (!allowed.Contains(name)) throw new InvalidOperationException("Unreviewed function in check constraint: " + name);
    if (name == "DAY" && value.Parameters.Count == 1) return Pack("FUNC_DATEPART_DAY", Scalar(value.Parameters[0]));
    if (name == "DATEPART" && value.Parameters.Count == 2 && TryDatePart(value.Parameters[0], out var datePart))
        return Pack("FUNC_DATEPART_" + datePart, Scalar(value.Parameters[1]));
    if (name is "DATEADD" or "DATEDIFF" && value.Parameters.Count == 3 && TryDatePart(value.Parameters[0], out datePart))
        return Pack("FUNC_" + name + "_" + datePart, Scalar(value.Parameters[1]), Scalar(value.Parameters[2]));
    return Pack("FUNC_" + name, new[] { value.UniqueRowFilter.ToString() }.Concat(value.Parameters.Select(Scalar)).ToArray());
}
static bool TryDatePart(ScalarExpression value, out string datePart)
{
    datePart = "";
    if (value is not ColumnReferenceExpression column || column.MultiPartIdentifier?.Identifiers.Count != 1)
        return false;
    var candidate = column.MultiPartIdentifier.Identifiers[0].Value.ToUpperInvariant();
    var allowed = new HashSet<string>(StringComparer.Ordinal) { "DAY", "DAYOFYEAR", "HOUR", "ISO_WEEK", "MICROSECOND", "MILLISECOND", "MINUTE", "MONTH", "NANOSECOND", "QUARTER", "SECOND", "WEEK", "WEEKDAY", "YEAR" };
    if (!allowed.Contains(candidate)) return false;
    datePart = candidate; return true;
}
static string Fragment(TSqlFragment value)
{
    var generator = new Sql160ScriptGenerator(new SqlScriptGeneratorOptions { KeywordCasing = KeywordCasing.Uppercase, IncludeSemicolons = false });
    generator.GenerateScript(value, out var text); return Pack("FRAGMENT", text);
}
static string Pack(string tag, params string[] values)
{
    var builder = new StringBuilder(); Append(tag); foreach (var value in values) Append(value); return builder.ToString();
    void Append(string value) { var bytes = EncodeUtf16Le(value); builder.Append(bytes.Length).Append(':').Append(Convert.ToBase64String(bytes)).Append(';'); }
}
static string Aggregate(IEnumerable<AuthorityEntry> entries, AuthorityIdentityKind kind)
{
    var records = entries.Select(entry => kind switch
    {
        AuthorityIdentityKind.Source => Pack("SOURCE", entry.ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.table, entry.name,
            entry.sourceOriginalUtf16LeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.sourceOriginalUtf16LeSha256,
            entry.sourceNormalizedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.sourceNormalizedUtf8Sha256),
        AuthorityIdentityKind.Storage => Pack("STORAGE", entry.ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.table, entry.name,
            entry.storageOriginalUtf16LeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.storageOriginalUtf16LeSha256,
            entry.storageNormalizedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.storageNormalizedUtf8Sha256),
        AuthorityIdentityKind.Semantic => Pack("SEMANTIC", entry.ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.table, entry.name, entry.semanticSha256),
        _ => throw new InvalidOperationException("Unknown identity set.")
    }).ToArray();
    return Hash(Encoding.UTF8.GetBytes(Pack("AUTHORITY_SET", records)));
}

static void SelfTest()
{
    Equal("LEN([Code]) > 0", "len(([Code]))>(0)", "built-in identifier casing");
    Equal("[Status] IN (1,2,3)", "([Status]=(3) OR [Status]=1 OR [Status]=2)", "IN/OR under SQL 3VL");
    Equal("[Value] BETWEEN 1 AND 9", "[Value]>=1 AND [Value]<=9", "BETWEEN range");
    Equal("DATEADD(day,1,[At])>[At]", "dateadd(DAY,(1),[At])>[At]", "date-part identifier casing");
    Different("[Code]='A'", "[Code]='a'", "literal case");
    Different("[Code]='a b'", "[Code]='ab'", "literal whitespace");
    Different("[Code]=N'A'", "[Code]='A'", "N prefix");
    Different("[Code] COLLATE Latin1_General_BIN2='A'", "[Code] COLLATE Latin1_General_CI_AS='A'", "collation");
    Different("CAST([Value] AS int)>0", "CAST([Value] AS bigint)>0", "cast type");
    Different("CONVERT(date,[Value],112) IS NOT NULL", "CONVERT(date,[Value],120) IS NOT NULL", "convert style");
    Different("DATEADD(day,1,[At])>[At]", "DATEADD(month,1,[At])>[At]", "date-part identity");
    Different("[Value]>0", "[Value]>=0", "operator");
    Different("NOT ([A]=1 AND [B]=1)", "NOT [A]=1 AND [B]=1", "NOT scope");
    Different("[Value] IS NULL", "[Value]=NULL", "SQL three-valued logic");
    Different("[Value] NOT IN (1,2)", "[Value]<>1 AND [Value]<>2", "NOT IN null semantics");
    Different("[Code]='ab' AND [Other]='c'", "[Code]='a' AND [Other]='bc'", "length-prefix collision");
    Different("[Code]=N'\uD800'", "[Code]=N'\uD801'", "distinct lone high-surrogate code units");
    Different("[Code]=N'\uD800\uDC00'", "[Code]=N'\uD800x\uDC00'", "valid pair versus lone components");
    Different("[Code]=N'\uD800\uDC00'", "[Code]=N'\uD800'", "valid pair versus lone high surrogate");
    if (Pack("SURROGATE", "\uD800") == Pack("SURROGATE", "\uD801"))
        throw new InvalidOperationException("UTF-16LE structural frames collided for distinct lone surrogates.");
    const string exactUnits = "\uD800\uDC00\uD801\uDC01";
    if (!StringComparer.Ordinal.Equals(exactUnits, DecodeUtf16Le(EncodeUtf16Le(exactUnits))))
        throw new InvalidOperationException("UTF-16LE structural frame did not preserve exact code units.");
    Refuses("dbo.CustomPredicate([Value])=1", "qualified/user-defined function");
    Refuses("LEN([Code]) OVER () > 0", "OVER modifier");
    Refuses("LEN([Code]) OVER (ORDER BY [Code]) > 0", "OVER ORDER BY modifier");
    Refuses("LEN([Code]) WITHIN GROUP (ORDER BY [Code]) > 0", "WITHIN GROUP modifier");
    Refuses("[Code] IN (SELECT [Other] FROM [AnyTable])", "IN subquery");
    Refuses("[Code] IN ()", "empty IN list");
    Refuses("IDENTITYCOL=1", "IDENTITYCOL pseudo-column");
    Refuses("ROWGUIDCOL IS NOT NULL", "ROWGUIDCOL pseudo-column");
    Refuses("$IDENTITY=1", "$IDENTITY pseudo-column");
    Refuses("$ROWGUID IS NOT NULL", "$ROWGUID pseudo-column");
    Refuses("CHECK THIS IS NOT SQL", "unparseable syntax");
    RefusesFunctionModifier(x => x.OverClause = new OverClause(), "manual OVER property");
    RefusesFunctionModifier(x => x.WithinGroupClause = new WithinGroupClause(), "manual WITHIN GROUP property");
    RefusesFunctionModifier(x => x.JsonOrderByClause = new OrderByClause(), "manual JSON ORDER BY property");
    RefusesFunctionModifier(x => x.TrimOptions = new Identifier { Value = "BOTH" }, "manual TRIM option property");
    RefusesFunctionModifier(x => x.WithArrayWrapper = true, "manual array-wrapper property");
    RefusesFunctionModifier(x => x.IgnoreRespectNulls.Add(new Identifier { Value = "IGNORE" }), "manual null-handling property");
    RefusesFunctionModifier(x => x.JsonParameters.Add(new JsonKeyValue()), "manual JSON parameter property");
    RefusesFunctionModifier(x => x.AbsentOrNullOnNull.Add(new Identifier { Value = "ABSENT" }), "manual JSON null property");
    RefusesFunctionModifier(x => x.ReturnType.Add(new SqlDataTypeReference()), "manual return-type property");
    var subquery = new InPredicate { Expression = new IntegerLiteral { Value = "1" }, Subquery = new ScalarSubquery() };
    RefusesAction(() => In(subquery), "manual IN subquery property");
    var empty = new InPredicate { Expression = new IntegerLiteral { Value = "1" } };
    RefusesAction(() => In(empty), "manual empty IN values");
    var odbcLike = new LikePredicate
    {
        FirstExpression = new StringLiteral { Value = "value" },
        SecondExpression = new StringLiteral { Value = "%" },
        OdbcEscape = true
    };
    RefusesAction(() => Like(odbcLike), "manual ODBC LIKE modifier");
    foreach (var kind in new[] { ColumnType.IdentityCol, ColumnType.RowGuidCol, ColumnType.PseudoColumnIdentity, ColumnType.PseudoColumnRowGuid })
        RefusesAction(() => Column(new ColumnReferenceExpression { ColumnType = kind }), "manual non-regular column kind " + kind);
    void Equal(string left, string right, string label) { if (Semantic(left) != Semantic(right)) throw new InvalidOperationException("Equivalent pair drifted: " + label); }
    void Different(string left, string right, string label) { if (Semantic(left) == Semantic(right)) throw new InvalidOperationException("Non-equivalent pair collided: " + label); }
    void Refuses(string sql, string label) { try { _ = Semantic(sql); } catch { return; } throw new InvalidOperationException("Unsupported input was accepted: " + label); }
    void RefusesAction(Action action, string label) { try { action(); } catch { return; } throw new InvalidOperationException("Unsupported AST state was accepted: " + label); }
    void RefusesFunctionModifier(Action<FunctionCall> mutation, string label)
    {
        var function = new FunctionCall { FunctionName = new Identifier { Value = "LEN" } };
        function.Parameters.Add(new IntegerLiteral { Value = "1" }); mutation(function);
        RefusesAction(() => Function(function), label);
    }
}

static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = false, WriteIndented = true };
static void WriteNewJson(string path, object value)
{
    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    JsonSerializer.Serialize(stream, value, JsonOptions()); stream.WriteByte((byte)'\n'); stream.Flush(true);
}
static bool SafeIdentifier(string value) => Regex.IsMatch(value, "\\A[A-Za-z0-9_]+\\z", RegexOptions.CultureInvariant);
static bool IsSha(string value) => Regex.IsMatch(value, "\\A[0-9A-F]{64}\\z", RegexOptions.CultureInvariant);
static bool SameTuple(string aTable, string aName, string bTable, string bName) => aTable == bTable && aName == bName;
static int CompareTuple(string aTable, string aName, string bTable, string bName) { var c = StringComparer.Ordinal.Compare(aTable, bTable); return c != 0 ? c : StringComparer.Ordinal.Compare(aName, bName); }
static string NormalizeLf(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
static (byte[] utf16, byte[] wtf8) Identity(string value) => (EncodeUtf16Le(value), EncodeWtf8(NormalizeLf(value)));
static byte[] EncodeUtf16Le(string value) { var bytes = new byte[value.Length * 2]; for (var i=0;i<value.Length;i++){var u=(int)value[i];bytes[i*2]=(byte)u;bytes[i*2+1]=(byte)(u>>8);} return bytes; }
static string DecodeUtf16Le(byte[] bytes) { if ((bytes.Length & 1) != 0) throw new InvalidOperationException("Odd UTF-16LE length."); var chars=new char[bytes.Length/2]; for(var i=0;i<chars.Length;i++) chars[i]=(char)(bytes[i*2]|bytes[i*2+1]<<8); return new string(chars); }
static byte[] EncodeWtf8(string value) { var bytes=new List<byte>();for(var i=0;i<value.Length;i++){var u=(int)value[i];if(u is >=0xD800 and <=0xDBFF&&i+1<value.Length&&value[i+1] is >= '\uDC00' and <= '\uDFFF'){var s=0x10000+((u-0xD800)<<10)+(value[++i]-0xDC00);bytes.Add((byte)(0xF0|s>>18));bytes.Add((byte)(0x80|(s>>12)&0x3F));bytes.Add((byte)(0x80|(s>>6)&0x3F));bytes.Add((byte)(0x80|s&0x3F));}else if(u<=0x7F)bytes.Add((byte)u);else if(u<=0x7FF){bytes.Add((byte)(0xC0|u>>6));bytes.Add((byte)(0x80|u&0x3F));}else{bytes.Add((byte)(0xE0|u>>12));bytes.Add((byte)(0x80|(u>>6)&0x3F));bytes.Add((byte)(0x80|u&0x3F));}}return bytes.ToArray(); }
static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
static string HashFile(string path) => Hash(File.ReadAllBytes(path));
static string GitBlobId(byte[] bytes) { var header=Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"); var all=new byte[header.Length+bytes.Length]; header.CopyTo(all,0); bytes.CopyTo(all,header.Length); return Convert.ToHexString(SHA1.HashData(all)).ToLowerInvariant(); }
static string GitText(string workingDirectory, params string[] arguments) => new UTF8Encoding(false, true).GetString(GitBytes(workingDirectory, arguments));
static byte[] GitBytes(string workingDirectory, params string[] arguments)
{
    var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Could not start Git.");
    using var output = new MemoryStream(); process.StandardOutput.BaseStream.CopyTo(output);
    var error = process.StandardError.ReadToEnd(); process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException("Git source-object read failed without using worktree bytes: " + error.Trim());
    return output.ToArray();
}

interface IRawRow { string schema { get; } string key { get; } string table { get; } string name { get; } }
sealed record SourceRow(string schema,string key,string table,string name,int originalUtf16LeBytes,string originalUtf16LeSha256,string originalUtf16LeBase64,int normalizedUtf8Bytes,string normalizedUtf8Sha256,string normalizedUtf8Base64,string normalizedUtf8Encoding,string semanticAnalysis) : IRawRow;
sealed record ActualRow(string schema,string key,string table,string name,int serverDataLength,string serverUtf16LeSha256,int originalUtf16LeBytes,string originalUtf16LeSha256,string originalUtf16LeBase64,int normalizedUtf8Bytes,string normalizedUtf8Sha256,string normalizedUtf8Base64,string normalizedUtf8Encoding,string semanticAnalysis) : IRawRow;
sealed record SourceDefinition(string Table,string Name,string Raw);
sealed record BaselineBinding(string path,string mode,string blobOid,int bytes,string rawSha256);
sealed record CaptureBinding(string evidenceId,string terminalManifestSha256,string terminalOutcome,string reviewedCommit,string reviewedTree,string sourceCorpusSha256,string storageCorpusSha256,string comparisonSha256,string preDropManifestSha256,string terminalStage,bool storageAuthorityAccepted);
sealed record SemanticBinding(string grammar,string functionIdentifierPolicy,string encoding,int semanticMatchCount,int semanticDriftCount,int unsupportedCount);
sealed record AuthorityEntry(int ordinal,string table,string name,int sourceOriginalUtf16LeBytes,string sourceOriginalUtf16LeSha256,int sourceNormalizedUtf8Bytes,string sourceNormalizedUtf8Sha256,int storageOriginalUtf16LeBytes,string storageOriginalUtf16LeSha256,int storageNormalizedUtf8Bytes,string storageNormalizedUtf8Sha256,string semanticSha256);
sealed record AuthorityDocument(string schema,int rowCount,string comparator,string keySerialization,string keysetSha256,BaselineBinding baseline,CaptureBinding capture,SemanticBinding semantic,string sourceIdentitySetSha256,string storageIdentitySetSha256,string semanticIdentitySetSha256,AuthorityEntry[] entries);
enum AuthorityIdentityKind { Source, Storage, Semantic }
