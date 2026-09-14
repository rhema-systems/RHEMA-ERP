using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.SqlServer.TransactSql.ScriptDom;

const string SnapshotRelativePath = "src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs";
const string BaselineRelativePath = "src/ErpSystem.Data/Migrations/20260913162402_DisposableDevelopmentCurrentModelBaseline.cs";
const string ArchivedChecksRelativePath = "src/ErpSystem.Data/Configuration/ArchivedCheckConstraintBaselineModel.cs";
const string ImmutableLotTrigger = "TR_ProcurementSourcingCaseLots_Immutable";
const string ImmutableLotItemTrigger = "TR_ProcurementSourcingCaseLotItems_Immutable";
const string ModelLotTrigger = "TR_ProcurementSourcingCaseLots_NoMutation";
const string ModelLotItemTrigger = "TR_ProcurementSourcingCaseLotItems_NoMutation";

if (args.Length == 2 && args[0] == "--verify-generated-sql")
{
    VerifyGeneratedSqlGrammar(Path.GetFullPath(args[1]));
    return;
}

var separateFinanceAuthorityTriggers = new HashSet<string>(new[]
{
    "TR_AccountingBookApplicabilityPolicies_C5Authority", "TR_AccountingBookApplicabilityRules_C5Immutable",
    "TR_AccountingBookApplicabilityRuleBooks_C5Immutable", "TR_AccountingBookSelectionEvidence_C5Immutable",
    "TR_AccountingBookSelectionEvidenceBooks_C5Immutable", "TR_AccountingEvents_C6Authority",
    "TR_AccountingEventPostings_C6Authority", "TR_AccountingEventAttempts_C6AppendOnly",
    "TR_AccountingEventProducerReceipts_C7Immutable", "TR_AccountingEvents_C7ProducerDecision",
    "TR_ProducerIntentGroupMembers_C8Immutable", "TR_ProducerIntentGroupReceipts_C8Immutable",
    "TR_ProducerIntentGroupAttempts_C8Immutable", "TR_ProducerIntentGroupAttempts_C8NoMutation",
    "TR_ProducerIntentGroups_C8Authority"
}, StringComparer.Ordinal);

var repositoryRoot = FindRepositoryRoot();
var snapshotPath = Path.Combine(repositoryRoot, SnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
var baselinePath = Path.Combine(repositoryRoot, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
var migrations = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
    .Select(type => new MigrationType(type, type.GetCustomAttribute<MigrationAttribute>()?.Id))
    .Where(item => item.Id is not null)
    .OrderBy(item => item.Id, StringComparer.Ordinal)
    .ToArray();

if (args.Length == 1 && args[0] == "--migration-ids")
{
    foreach (var migration in migrations) Console.WriteLine(migration.Id);
    return;
}

if (migrations.Length != 596)
{
    throw new InvalidOperationException($"Expected 596 archived migrations, found {migrations.Length}.");
}

var migrationOperations = migrations.Select(migration =>
{
    var instance = (Migration)Activator.CreateInstance(migration.Type, nonPublic: true)!;
    return new ArchivedMigrationOperations(migration.Id!, instance.UpOperations.ToArray());
}).ToArray();

var sqlOperations = migrationOperations.SelectMany(migration =>
{
    return migration.Operations.OfType<SqlOperation>()
        .Select((operation, index) => new ArchivedSql(migration.Id!, index, operation.Sql));
}).ToArray();

if (args.Length == 1 && args[0] == "--check-sql-operations")
{
    foreach (var operation in sqlOperations.Where(item =>
                 item.Sql.Contains("CONSTRAINT", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine($"OPERATION={operation.MigrationId}:{operation.OperationIndex}");
        Console.WriteLine(NormalizeNewlines(operation.Sql));
        Console.WriteLine("END_OPERATION");
    }
    return;
}

var triggerCandidates = sqlOperations.SelectMany(ParseTriggerDefinitions).ToArray();
var snapshotTriggers = ParseSnapshotTriggers(File.ReadAllText(snapshotPath));
var selectedTriggers = new Dictionary<string, SqlDefinition>(StringComparer.Ordinal);
foreach (var modelTrigger in snapshotTriggers.Keys.OrderBy(value => value, StringComparer.Ordinal))
{
    var candidates = triggerCandidates
        .Where(candidate => candidate.Name == modelTrigger)
        .OrderBy(candidate => candidate.MigrationId, StringComparer.Ordinal)
        .ThenBy(candidate => candidate.OperationIndex)
        .ToArray();
    if (candidates.Length != 0)
    {
        selectedTriggers.Add(modelTrigger, candidates[^1]);
    }
}

AddCheckedAlias(selectedTriggers, triggerCandidates, snapshotTriggers, ImmutableLotTrigger, ModelLotTrigger);
AddCheckedAlias(selectedTriggers, triggerCandidates, snapshotTriggers, ImmutableLotItemTrigger, ModelLotItemTrigger);

var missingTriggers = snapshotTriggers.Keys.Except(selectedTriggers.Keys, StringComparer.Ordinal).ToArray();
if (snapshotTriggers.Count != 386 || selectedTriggers.Count != 386 || missingTriggers.Length != 0)
{
    throw new InvalidOperationException(
        $"Trigger parity failed. Snapshot={snapshotTriggers.Count}; selected={selectedTriggers.Count}; missing={string.Join(',', missingTriggers)}.");
}
var modelTriggerNames = selectedTriggers.Keys.ToHashSet(StringComparer.Ordinal);
var baselineTableColumns = ParseBaselineTableColumns(File.ReadAllText(baselinePath));
var aliasSources = new HashSet<string>(new[] { ImmutableLotTrigger, ImmutableLotItemTrigger }, StringComparer.Ordinal);
var triggerDisposition = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var group in triggerCandidates.GroupBy(item => item.Name, StringComparer.Ordinal))
{
    if (selectedTriggers.ContainsKey(group.Key)) { triggerDisposition.Add(group.Key, "CURRENT_MODEL"); continue; }
    if (separateFinanceAuthorityTriggers.Contains(group.Key)) { triggerDisposition.Add(group.Key, "SEPARATE_FINANCE_AUTHORITY"); continue; }
    if (aliasSources.Contains(group.Key)) { triggerDisposition.Add(group.Key, "SUPERSEDED_BY_EXACT_MODEL_ALIAS"); continue; }
    var finalDefinition = group.OrderBy(item => item.MigrationId, StringComparer.Ordinal).ThenBy(item => item.OperationIndex).Last();
    var targetTable = ParseTriggerTargetTable(finalDefinition.Sql);
    if (!baselineTableColumns.ContainsKey(targetTable)) { triggerDisposition.Add(group.Key, "TARGET_TABLE_ABSENT"); continue; }
    if (HasLaterExplicitTriggerDrop(finalDefinition, sqlOperations)) { triggerDisposition.Add(group.Key, "EXPLICITLY_DROPPED_AFTER_FINAL_DEFINITION"); continue; }
    selectedTriggers.Add(group.Key, finalDefinition with { Derivation = "ACTIVE_NON_MODEL_ARCHIVED_FINAL_DEFINITION" });
    triggerDisposition.Add(group.Key, "ACTIVE_NON_MODEL");
}
var archivedUniqueTriggerCount = triggerCandidates.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count();
if (triggerDisposition.Count != archivedUniqueTriggerCount)
    throw new InvalidOperationException($"Archived trigger audit is not exhaustive: {triggerDisposition.Count}/{archivedUniqueTriggerCount} names classified.");
var nonModelTriggerNames = selectedTriggers.Keys.Except(modelTriggerNames, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

foreach (var selected in selectedTriggers.Values)
{
    var actualTable = ParseTriggerTargetTable(selected.Sql);
    if (snapshotTriggers.TryGetValue(selected.Name, out var expectedTable) &&
        !string.Equals(expectedTable, actualTable, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Trigger {selected.Name} targets {actualTable}, but the snapshot binds it to {expectedTable}.");
    }
    if (CountTriggerDefinitions(selected.Sql) != 1 || selected.Sql.Contains("\nGO\n", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Trigger {selected.Name} is not one isolated definition.");
    }
}
var staticallyValidatedColumnReferenceCount = ValidateStaticTriggerColumnReferences(selectedTriggers.Values, baselineTableColumns);

var patches = SelectPostDefinitionPatches(sqlOperations, selectedTriggers);
var programmableDefinitions = SelectProgrammableDefinitions(sqlOperations);
var programmableDisposition = AuditProgrammableLifecycle(sqlOperations);
var audit = AuditGovernanceObjects(sqlOperations, triggerCandidates);
var archivedCheckConstraints = AuditArchivedCheckConstraintLifecycle(migrationOperations);
var baselineCheckConstraints = AuditCurrentBaselineCheckConstraints();
var archivedChecksMissingFromBaseline = archivedCheckConstraints.Values
    .Where(item => !baselineCheckConstraints.ContainsKey(CheckConstraintKey(item.Table, item.Name)))
    .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
var archivedCheckDefinitionMismatches = archivedCheckConstraints.Values
    .Where(item => baselineCheckConstraints.TryGetValue(CheckConstraintKey(item.Table, item.Name), out var baseline) &&
                   CanonicalSql(item.Sql) != CanonicalSql(baseline.Sql))
    .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
if (archivedCheckConstraints.Count != 675 || baselineCheckConstraints.Count != 835 ||
    archivedChecksMissingFromBaseline.Length != 0 || archivedCheckDefinitionMismatches.Length != 0)
{
    throw new InvalidOperationException(
        $"Archived-final check parity failed. Archived={archivedCheckConstraints.Count}; baseline={baselineCheckConstraints.Count}; " +
        $"missing={archivedChecksMissingFromBaseline.Length}; drifted={archivedCheckDefinitionMismatches.Length}.");
}
var manifest = new
{
    schema = "RHEMA_DISPOSABLE_BASELINE_GOVERNANCE_V2",
    archiveMigrationCount = migrations.Length,
    archiveSqlOperationCount = sqlOperations.Length,
    archivedUniqueTriggerCount,
    modelTriggerCount = snapshotTriggers.Count,
    selectedModelTriggerCount = modelTriggerNames.Count,
    activeNonModelTriggerCount = nonModelTriggerNames.Length,
    additionalFinanceAuthorityTriggerCount = 15,
    finalUniqueTriggerCount = selectedTriggers.Count + 15,
    baselineTableCount = baselineTableColumns.Count,
    staticallyValidatedColumnReferenceCount,
    triggerDefinitions = selectedTriggers.Values.OrderBy(item => item.Name, StringComparer.Ordinal).Select(item => new
    {
        item.Name,
        table = ParseTriggerTargetTable(item.Sql),
        item.MigrationId,
        item.OperationIndex,
        item.Derivation,
        bodySha256 = Sha256(item.Sql)
    }),
    modelTriggerNames = modelTriggerNames.OrderBy(value => value, StringComparer.Ordinal),
    activeNonModelTriggerNames = nonModelTriggerNames,
    archivedTriggerDisposition = triggerDisposition.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new { name=item.Key, disposition=item.Value }),
    postDefinitionPatches = patches.Select(patch => new
    {
        patch.MigrationId,
        patch.OperationIndex,
        patch.TargetNames,
        sqlSha256 = Sha256(patch.Sql)
    }),
    programmableObjects = programmableDefinitions.Select(item => new
    {
        item.Kind,
        item.Name,
        item.MigrationId,
        item.OperationIndex,
        bodySha256 = Sha256(item.Sql)
    }),
    programmableObjectDisposition = programmableDisposition,
    finalArchivedCheckConstraints = archivedCheckConstraints.Values
        .OrderBy(item => item.Table, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal)
        .Select(item => new
        {
            item.Table,
            item.Name,
            item.MigrationId,
            item.OperationIndex,
            item.Derivation,
            sqlSha256 = Sha256(CanonicalSql(item.Sql))
        }),
    baselineCheckConstraintCount = baselineCheckConstraints.Count,
    archivedCheckConstraintCount = archivedCheckConstraints.Count,
    audit
};

if (args.Length == 0 || args[0] == "--summary")
{
    Console.WriteLine($"migrations={migrations.Length}");
    Console.WriteLine($"sqlOperations={sqlOperations.Length}");
    Console.WriteLine($"directTriggerDefinitions={triggerCandidates.Length}");
    Console.WriteLine($"directTriggerNames={triggerCandidates.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count()}");
    Console.WriteLine($"modelTriggerNames={snapshotTriggers.Count}");
    Console.WriteLine($"selectedModelTriggers={modelTriggerNames.Count}");
    Console.WriteLine($"activeNonModelTriggers={nonModelTriggerNames.Length}");
    Console.WriteLine($"finalUniqueTriggers={selectedTriggers.Count + 15}");
    foreach (var group in triggerDisposition.GroupBy(item => item.Value).OrderBy(group => group.Key, StringComparer.Ordinal))
        Console.WriteLine($"triggerDisposition={group.Key}|count={group.Count()}");
    Console.WriteLine($"postDefinitionPatchOperations={patches.Length}");
    Console.WriteLine($"baselineTables={baselineTableColumns.Count}");
    Console.WriteLine($"staticallyValidatedInsertedDeletedColumnReferences={staticallyValidatedColumnReferenceCount}");
    Console.WriteLine($"programmableObjects={programmableDefinitions.Length}");
    Console.WriteLine($"archivedActiveCheckConstraints={archivedCheckConstraints.Count}");
    Console.WriteLine($"baselineCheckConstraints={baselineCheckConstraints.Count}");
    Console.WriteLine($"archivedChecksMissingFromBaseline={archivedChecksMissingFromBaseline.Length}");
    Console.WriteLine($"archivedCheckDefinitionMismatches={archivedCheckDefinitionMismatches.Length}");
    foreach (var item in archivedChecksMissingFromBaseline)
        Console.WriteLine($"missingCheck={item.Table}|{item.Name}|{item.MigrationId}|{item.OperationIndex}");
    foreach (var item in archivedCheckDefinitionMismatches)
        Console.WriteLine($"mismatchedCheck={item.Table}|{item.Name}|{item.MigrationId}|{item.OperationIndex}");
    foreach (var item in programmableDefinitions)
    {
        Console.WriteLine($"programmable={item.Kind}|{item.Name}|{item.MigrationId}|{item.OperationIndex}|{Sha256(item.Sql)}");
    }
    foreach (var disposition in programmableDisposition)
        Console.WriteLine($"programmableDisposition={disposition.Kind}|{disposition.Name}|{disposition.Disposition}");
    foreach (var group in audit)
    {
        Console.WriteLine($"audit={group.Kind}|definitions={group.DefinitionCount}|names={group.UniqueNameCount}");
    }
    return;
}

if (args.Length != 3 || args[0] is not ("--generate" or "--verify"))
{
    throw new InvalidOperationException("Usage: --summary, --migration-ids, --check-sql-operations, --generate <helper.cs> <manifest.json>, --verify <helper.cs> <manifest.json>, or --verify-generated-sql <script.sql>.");
}

var helperContent = RenderHelper(programmableDefinitions, selectedTriggers.Values, patches);
var archivedCheckModelContent = RenderArchivedCheckConstraintModel(archivedCheckConstraints.Values);
var manifestContent = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
var helperPath = Path.GetFullPath(args[1]);
var manifestPath = Path.GetFullPath(args[2]);
var archivedCheckModelPath = Path.Combine(repositoryRoot, ArchivedChecksRelativePath.Replace('/', Path.DirectorySeparatorChar));
if (args[0] == "--verify")
{
    VerifyExactFile(helperPath, helperContent);
    VerifyExactFile(manifestPath, manifestContent);
    VerifyExactFile(archivedCheckModelPath, archivedCheckModelContent);
    Console.WriteLine("PASS: archived governance helper and manifest are deterministic and current.");
    return;
}

WriteAtomic(helperPath, helperContent);
WriteAtomic(manifestPath, manifestContent);
WriteAtomic(archivedCheckModelPath, archivedCheckModelContent);
Console.WriteLine($"GENERATED_TRIGGERS={selectedTriggers.Count}");
Console.WriteLine($"GENERATED_PATCHES={patches.Length}");
Console.WriteLine($"GENERATED_PROGRAMMABLE_OBJECTS={programmableDefinitions.Length}");

static void VerifyGeneratedSqlGrammar(string path)
{
    if (!File.Exists(path)) throw new InvalidOperationException($"Generated SQL is missing: {path}.");
    var sql = NormalizeNewlines(File.ReadAllText(path));
    var fragment = ParseSql(sql, out var errors);
    if (errors.Count != 0)
    {
        var first = errors[0];
        throw new InvalidOperationException(
            $"Generated zero-to-current SQL has {errors.Count} T-SQL grammar error(s); first at {first.Line}:{first.Column}: {first.Message}");
    }

    var visitor = new ThrowCountingVisitor();
    fragment.Accept(visitor);
    if (visitor.Count == 0) throw new InvalidOperationException("Generated SQL grammar audit found no THROW statements.");

    const string correctedBoundary =
        "WHERE m.[TenantId]=g.[TenantId] AND m.[ProducerIntentGroupId]=g.[Id] AND e.[Status]<>N'Posted')))))\n" +
        "  THROW 51000, 'C8_ATTEMPT_AUTHORITY: one exact terminal attempt must atomically drive an authorized group transition.', 1;";
    if (CountOrdinal(sql, correctedBoundary) != 1)
        throw new InvalidOperationException("The exact corrected C8 attempt-authority grammar boundary is missing or duplicated.");

    var brokenBoundary = correctedBoundary.Replace("N'Posted')))))", "N'Posted'))))", StringComparison.Ordinal);
    var brokenSql = sql.Replace(correctedBoundary, brokenBoundary, StringComparison.Ordinal);
    _ = ParseSql(brokenSql, out var brokenErrors);
    if (brokenErrors.Count != 1 || brokenErrors[0].Number != 46005 || brokenErrors[0].Line <= 0 ||
        !brokenErrors[0].Message.Contains("THROW", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("The parser regression did not detect the archived missing-parenthesis failure at THROW.");

    Console.WriteLine($"PASS: generated SQL parses with TSql160Parser; THROW_STATEMENTS={visitor.Count}");
    Console.WriteLine("PASS: archived C8 missing-parenthesis boundary reproduces TSql160Parser grammar error 46005 near THROW");
}

static TSqlFragment ParseSql(string sql, out IList<ParseError> errors)
{
    var parser = new TSql160Parser(initialQuotedIdentifiers: true);
    using var reader = new StringReader(sql);
    return parser.Parse(reader, out errors);
}

static int CountOrdinal(string value, string needle)
{
    var count = 0;
    for (var index = 0; (index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0; index += needle.Length)
        count++;
    return count;
}

static string FindRepositoryRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new InvalidOperationException("Repository root could not be located.");
}

static Dictionary<string, string> ParseSnapshotTriggers(string snapshot)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    var tableBlocks = Regex.Matches(snapshot,
        @"b\.ToTable\(""(?<table>[^""]+)""[^;]*?t\s*=>\s*\{(?<body>.*?)\}\);",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);
    foreach (Match tableBlock in tableBlocks)
    {
        foreach (Match trigger in Regex.Matches(tableBlock.Groups["body"].Value,
                     @"HasTrigger\(""([^""]+)""\)", RegexOptions.CultureInvariant))
        {
            var name = trigger.Groups[1].Value;
            if (!result.TryAdd(name, tableBlock.Groups["table"].Value))
            {
                throw new InvalidOperationException($"Duplicate snapshot trigger name: {name}.");
            }
        }
    }
    return result;
}

static Dictionary<string, HashSet<string>> ParseBaselineTableColumns(string baseline)
{
    var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    var tableBlocks = Regex.Matches(baseline,
        @"migrationBuilder\.CreateTable\(\s*name:\s*""(?<table>[^""]+)"".*?columns:\s*table\s*=>\s*new\s*\{(?<columns>.*?)\},\s*constraints:",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);
    foreach (Match tableBlock in tableBlocks)
    {
        var columns = Regex.Matches(tableBlock.Groups["columns"].Value,
                @"(?m)^\s*@?(?<column>[A-Za-z0-9_]+)\s*=\s*table\.Column")
            .Select(match => match.Groups["column"].Value).ToHashSet(StringComparer.Ordinal);
        if (columns.Count == 0 || !result.TryAdd(tableBlock.Groups["table"].Value, columns))
            throw new InvalidOperationException($"Baseline table/column extraction is ambiguous: {tableBlock.Groups["table"].Value}.");
    }
    if (result.Count == 0) throw new InvalidOperationException("Baseline table/column extraction returned no tables.");
    return result;
}

static int ValidateStaticTriggerColumnReferences(IEnumerable<SqlDefinition> triggers,
    IReadOnlyDictionary<string, HashSet<string>> tableColumns)
{
    var validated = 0;
    foreach (var trigger in triggers)
    {
        var table = ParseTriggerTargetTable(trigger.Sql);
        if (!tableColumns.TryGetValue(table, out var columns))
            throw new InvalidOperationException($"Trigger {trigger.Name} targets absent baseline table {table}.");

        var aliases = Regex.Matches(trigger.Sql,
                @"\b(?:FROM|JOIN)\s+(?:(?:\[dbo\]|dbo)\.)?\[?(?<pseudo>inserted|deleted)\]?\s+(?:AS\s+)?\[?(?<alias>[A-Za-z0-9_]+)\]?",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Groups["alias"].Value).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            // A nested query can legally shadow i/d. Only validate aliases whose meaning is unambiguous
            // throughout the definition; ambiguous scopes remain for SQL Server's compile-time check.
            var shadowPattern = @"\b(?:FROM|JOIN)\s+(?!\[?(?:inserted|deleted)\]?\b)(?:\[[^\]]+\](?:\.\[[^\]]+\])?|[A-Za-z0-9_.]+)\s+(?:AS\s+)?\[?" +
                                Regex.Escape(alias) + @"\]?\b";
            if (Regex.IsMatch(trigger.Sql, shadowPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) continue;
            foreach (Match reference in Regex.Matches(trigger.Sql,
                         @"\b" + Regex.Escape(alias) + @"\.\[?(?<column>[A-Za-z0-9_]+)\]?",
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var column = reference.Groups["column"].Value;
                if (!columns.Contains(column))
                    throw new InvalidOperationException($"Trigger {trigger.Name} references absent baseline column {table}.{column}.");
                validated++;
            }
        }
        foreach (Match reference in Regex.Matches(trigger.Sql,
                     @"\b(?:inserted|deleted)\.\[?(?<column>[A-Za-z0-9_]+)\]?",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var column = reference.Groups["column"].Value;
            if (!columns.Contains(column))
                throw new InvalidOperationException($"Trigger {trigger.Name} references absent baseline column {table}.{column}.");
            validated++;
        }
    }
    if (validated == 0) throw new InvalidOperationException("No statically detectable inserted/deleted column references were validated.");
    return validated;
}

static IEnumerable<SqlDefinition> ParseTriggerDefinitions(ArchivedSql operation)
{
    foreach (var sql in ExpandExecutableSql(operation.Sql))
    {
        var header = Regex.Match(sql,
            @"^\s*(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+TRIGGER\s+(?:(?:\[dbo\]|dbo)\.)?(?:\[([^\]]+)\]|(TR_[A-Za-z0-9_]+))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!header.Success) continue;
        var name = header.Groups[1].Success ? header.Groups[1].Value : header.Groups[2].Value;
        var normalized = Regex.Replace(NormalizeNewlines(sql).Trim(),
            @"^(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+TRIGGER", "CREATE OR ALTER TRIGGER",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        yield return new SqlDefinition(name, operation.MigrationId, operation.OperationIndex, normalized, "ARCHIVED_FINAL_DEFINITION");
    }
}

static IEnumerable<string> ExpandExecutableSql(string operationSql)
{
    var normalized = NormalizeNewlines(operationSql).Trim();
    var execMatches = Regex.Matches(normalized,
        @"EXEC\s*\(\s*N?'(?<body>(?:''|[^'])*)'\s*\)\s*;?",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    foreach (Match exec in execMatches)
    {
        yield return exec.Groups["body"].Value.Replace("''", "'", StringComparison.Ordinal).Trim();
    }
    if (Regex.IsMatch(normalized,
            @"^\s*(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(?:TRIGGER|FUNCTION|VIEW|PROCEDURE|PROC|SYNONYM|SECURITY\s+POLICY|SEQUENCE)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
    {
        yield return normalized;
    }
}

static void AddCheckedAlias(IDictionary<string, SqlDefinition> selected,
    IReadOnlyCollection<SqlDefinition> candidates, IReadOnlyDictionary<string, string> snapshotTriggers,
    string sourceName, string targetName)
{
    if (selected.ContainsKey(targetName))
        throw new InvalidOperationException($"Alias target {targetName} unexpectedly has a direct archived definition.");
    var source = candidates.Where(candidate => candidate.Name == sourceName)
        .OrderBy(candidate => candidate.MigrationId, StringComparer.Ordinal).ThenBy(candidate => candidate.OperationIndex)
        .LastOrDefault() ?? throw new InvalidOperationException($"Alias source trigger is missing: {sourceName}.");
    var aliasedSql = source.Sql.Replace(sourceName, targetName, StringComparison.Ordinal);
    if (aliasedSql.Contains(sourceName, StringComparison.Ordinal) ||
        !string.Equals(ParseTriggerTargetTable(aliasedSql), snapshotTriggers[targetName], StringComparison.Ordinal))
        throw new InvalidOperationException($"Alias lineage could not be bound safely: {sourceName} -> {targetName}.");
    selected.Add(targetName, source with { Name = targetName, Sql = aliasedSql, Derivation = $"EXACT_RENAME_ALIAS_FROM:{sourceName}" });
}

static string ParseTriggerTargetTable(string sql)
{
    var match = Regex.Match(sql,
        @"\bON\s+(?:(?:\[dbo\]|dbo)\.)?(?:\[([^\]]+)\]|([A-Za-z0-9_]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    if (!match.Success) throw new InvalidOperationException("Trigger target table is absent.");
    return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
}

static int CountTriggerDefinitions(string sql) => Regex.Matches(sql,
    @"(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+TRIGGER\b",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;

static bool HasLaterExplicitTriggerDrop(SqlDefinition definition, IEnumerable<ArchivedSql> operations)
{
    var escapedName = Regex.Escape(definition.Name);
    var dropPattern = @"\bDROP\s+TRIGGER(?:\s+IF\s+EXISTS)?\s+(?:(?:\[dbo\]|dbo)\.)?\[?" + escapedName + @"\]?\b";
    return operations.Any(operation =>
        (string.CompareOrdinal(operation.MigrationId, definition.MigrationId) > 0 ||
         operation.MigrationId == definition.MigrationId && operation.OperationIndex > definition.OperationIndex) &&
        Regex.IsMatch(operation.Sql, dropPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
}

static ArchivedSql[] SelectPostDefinitionPatches(IReadOnlyCollection<ArchivedSql> operations,
    IReadOnlyDictionary<string, SqlDefinition> selected)
{
    var result = new List<ArchivedSql>();
    foreach (var operation in operations)
    {
        if (!operation.Sql.Contains("OBJECT_DEFINITION", StringComparison.OrdinalIgnoreCase)) continue;
        var names = Regex.Matches(operation.Sql, @"TR_[A-Za-z0-9_]+", RegexOptions.CultureInvariant)
            .Select(match => match.Value).Distinct(StringComparer.Ordinal).Where(selected.ContainsKey)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (names.Length == 0) continue;
        var requiresPatch = names.Any(name =>
        {
            var definition = selected[name];
            return string.CompareOrdinal(operation.MigrationId, definition.MigrationId) > 0 ||
                   operation.MigrationId == definition.MigrationId && operation.OperationIndex > definition.OperationIndex;
        });
        if (requiresPatch)
        {
            var normalized = NormalizePostDefinitionPatch(operation);
            RefusePredecessorDependentPatchSql(normalized);
            result.Add(operation with { Sql = normalized, TargetNames = names });
        }
    }
    return result.OrderBy(item => item.MigrationId, StringComparer.Ordinal).ThenBy(item => item.OperationIndex).ToArray();
}

static SqlDefinition[] SelectProgrammableDefinitions(IReadOnlyCollection<ArchivedSql> operations)
{
    var definitions = ParseProgrammableDefinitions(operations);
    return definitions.GroupBy(item => $"{item.Derivation}|{item.Name}", StringComparer.Ordinal)
        .Select(group => group.OrderBy(item => item.MigrationId, StringComparer.Ordinal).ThenBy(item => item.OperationIndex).Last())
        .Where(definition => !HasLaterExplicitProgrammableDrop(definition, operations))
        .OrderBy(item => item.Derivation == "FUNCTION" ? 0 : 1).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
}

static Dictionary<string, CheckConstraintDefinition> AuditArchivedCheckConstraintLifecycle(
    IReadOnlyCollection<ArchivedMigrationOperations> migrations)
{
    var active = new Dictionary<string, CheckConstraintDefinition>(StringComparer.Ordinal);
    foreach (var migration in migrations.OrderBy(item => item.Id, StringComparer.Ordinal))
    {
        for (var operationIndex = 0; operationIndex < migration.Operations.Count; operationIndex++)
        {
            var operation = migration.Operations[operationIndex];
            switch (operation)
            {
                case CreateTableOperation create:
                    foreach (var check in create.CheckConstraints)
                    {
                        var definition = new CheckConstraintDefinition(
                            create.Name, check.Name, check.Sql, migration.Id, operationIndex, "CREATE_TABLE");
                        active[CheckConstraintKey(definition.Table, definition.Name)] = definition;
                    }
                    break;
                case AddCheckConstraintOperation add:
                    var added = new CheckConstraintDefinition(
                        add.Table, add.Name, add.Sql, migration.Id, operationIndex, "ADD_CHECK_CONSTRAINT");
                    active[CheckConstraintKey(added.Table, added.Name)] = added;
                    break;
                case DropCheckConstraintOperation drop:
                    active.Remove(CheckConstraintKey(drop.Table, drop.Name));
                    break;
                case DropTableOperation dropTable:
                    foreach (var key in active.Values.Where(item => item.Table == dropTable.Name)
                                 .Select(item => CheckConstraintKey(item.Table, item.Name)).ToArray())
                        active.Remove(key);
                    break;
                case RenameTableOperation rename:
                    var newTableName = rename.NewName ??
                        throw new InvalidOperationException($"Archived table rename has no target: {migration.Id}:{operationIndex}.");
                    var renamed = active.Values.Where(item => item.Table == rename.Name).ToArray();
                    foreach (var item in renamed)
                    {
                        active.Remove(CheckConstraintKey(item.Table, item.Name));
                        var replacement = item with { Table = newTableName };
                        active[CheckConstraintKey(replacement.Table, replacement.Name)] = replacement;
                    }
                    break;
                case SqlOperation sql:
                    ApplyRawCheckConstraintLifecycle(active, sql.Sql, migration.Id, operationIndex);
                    break;
            }
        }
    }
    return active;
}

static void ApplyRawCheckConstraintLifecycle(IDictionary<string, CheckConstraintDefinition> active,
    string sql, string migrationId, int operationIndex)
{
    var normalized = NormalizeNewlines(sql);
    var fragment = ParseSql(normalized, out var errors);
    if (errors.Count == 0)
    {
        var visitor = new CheckConstraintLifecycleVisitor(normalized);
        fragment.Accept(visitor);
        foreach (var item in visitor.Events)
        {
            var eventKey = CheckConstraintKey(item.Table, item.Name);
            if (item.Sql is null) active.Remove(eventKey);
            else active[eventKey] = new CheckConstraintDefinition(item.Table, item.Name, item.Sql,
                migrationId, operationIndex, "RAW_SQL_FINAL_STATE");
        }
    }

    var patch = Regex.Match(normalized,
        @"SELECT\s+definition\s+FROM\s+sys\.check_constraints\s+WHERE\s+name=N'(?<name>(?:''|[^'])+)'\s+AND\s+parent_object_id=OBJECT_ID\(N'dbo\.(?<table>(?:''|[^'])+)'\).*?DECLARE\s+@before\s+nvarchar\(max\)=REPLACE\(N'(?<before>(?:''|[^'])*)'.*?DECLARE\s+@after\s+nvarchar\(max\)=REPLACE\(N'(?<after>(?:''|[^'])*)'",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    if (!patch.Success) return;
    if (Regex.Matches(normalized, @"SELECT\s+definition\s+FROM\s+sys\.check_constraints",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count != 1)
        throw new InvalidOperationException($"Ambiguous archived check patch: {migrationId}:{operationIndex}.");
    var table = UnescapeSqlLiteral(patch.Groups["table"].Value);
    var name = UnescapeSqlLiteral(patch.Groups["name"].Value);
    var before = UnescapeSqlLiteral(patch.Groups["before"].Value);
    var after = UnescapeSqlLiteral(patch.Groups["after"].Value);
    var key = CheckConstraintKey(table, name);
    if (!active.TryGetValue(key, out var current) || CountOrdinal(current.Sql, before) != 1)
        throw new InvalidOperationException($"Archived check patch does not match exactly once: {migrationId}:{operationIndex}:{key}.");
    active[key] = current with
    {
        Sql = current.Sql.Replace(before, after, StringComparison.Ordinal),
        MigrationId = migrationId,
        OperationIndex = operationIndex,
        Derivation = "RAW_SQL_EXACT_PATCH"
    };
}

static string UnescapeSqlLiteral(string value) => value.Replace("''", "'", StringComparison.Ordinal);

static string NormalizePostDefinitionPatch(ArchivedSql operation)
{
    var sql = NormalizeNewlines(operation.Sql).Trim();
    return operation.MigrationId switch
    {
        "20260907033000_AlignPettyPurchaseQuotationLifecycle" => SliceFromUniqueMarker(sql, "DECLARE @trigger"),
        "20260911210000_PhysicalCountReviewDecisions" => SliceFromUniqueMarker(sql, "DECLARE @line"),
        "20260912003000_WarehouseDefaultLocations" => SliceFromUniqueMarker(sql, "DECLARE @actions"),
        "20260912013000_AlignStockAdjustmentLocationValuation" => SliceFromUniqueMarker(sql, "DECLARE @definition"),
        _ => sql
    };
}

static string SliceFromUniqueMarker(string sql, string marker)
{
    if (CountOrdinal(sql, marker) != 1)
        throw new InvalidOperationException($"Mixed archived patch marker is missing or ambiguous: {marker}.");
    return sql[sql.IndexOf(marker, StringComparison.Ordinal)..].Trim();
}

static void RefusePredecessorDependentPatchSql(string sql)
{
    var prohibited = Regex.Match(sql,
        @"(?im)^\s*(?:ALTER\s+TABLE|DROP\s+(?:CONSTRAINT|INDEX|FUNCTION|VIEW|PROCEDURE|TRIGGER|TABLE)|INSERT\s+INTO|UPDATE\s+|DELETE\s+FROM|MERGE\s+)");
    if (prohibited.Success)
        throw new InvalidOperationException($"Archived trigger patch retained predecessor/data-dependent SQL: {prohibited.Value.Trim()}.");
}

static Dictionary<string, CheckConstraintDefinition> AuditCurrentBaselineCheckConstraints()
{
    var migrationType = typeof(ApplicationDbContext).Assembly.GetTypes()
        .Single(type => !type.IsAbstract && typeof(Migration).IsAssignableFrom(type) &&
                        type.GetCustomAttribute<MigrationAttribute>()?.Id == "20260913162402_DisposableDevelopmentCurrentModelBaseline");
    var migration = (Migration)Activator.CreateInstance(migrationType, nonPublic: true)!;
    var result = new Dictionary<string, CheckConstraintDefinition>(StringComparer.Ordinal);
    foreach (var create in migration.UpOperations.OfType<CreateTableOperation>())
    foreach (var check in create.CheckConstraints)
    {
        var definition = new CheckConstraintDefinition(create.Name, check.Name, check.Sql,
            "20260913162402_DisposableDevelopmentCurrentModelBaseline", 0, "CURRENT_BASELINE");
        result.Add(CheckConstraintKey(definition.Table, definition.Name), definition);
    }
    return result;
}

static string CheckConstraintKey(string table, string name) => table + "|" + name;
static string CanonicalSql(string sql) => Regex.Replace(sql, @"\s+", string.Empty,
    RegexOptions.CultureInvariant).ToUpperInvariant();

static List<SqlDefinition> ParseProgrammableDefinitions(IReadOnlyCollection<ArchivedSql> operations)
{
    var definitions = new List<SqlDefinition>();
    foreach (var operation in operations)
    foreach (var sql in ExpandExecutableSql(operation.Sql))
    {
        var match = Regex.Match(sql,
            @"^\s*(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(FUNCTION|VIEW|PROCEDURE|PROC)\s+(?:(?:\[dbo\]|dbo)\.)?(?:\[([^\]]+)\]|([A-Za-z0-9_]+))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) continue;
        var kind = match.Groups[1].Value.Equals("PROC", StringComparison.OrdinalIgnoreCase) ? "PROCEDURE" : match.Groups[1].Value.ToUpperInvariant();
        var name = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
        var normalized = Regex.Replace(NormalizeNewlines(sql).Trim(),
            @"^(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+" + match.Groups[1].Value,
            "CREATE OR ALTER " + kind, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        definitions.Add(new SqlDefinition(name, operation.MigrationId, operation.OperationIndex, normalized, kind));
    }
    return definitions;
}

static ProgrammableObjectDisposition[] AuditProgrammableLifecycle(IReadOnlyCollection<ArchivedSql> operations)
{
    return ParseProgrammableDefinitions(operations)
        .GroupBy(item => $"{item.Derivation}|{item.Name}", StringComparer.Ordinal)
        .Select(group => group.OrderBy(item => item.MigrationId, StringComparer.Ordinal).ThenBy(item => item.OperationIndex).Last())
        .Select(definition => new ProgrammableObjectDisposition(
            definition.Name,
            definition.Derivation,
            definition.MigrationId,
            definition.OperationIndex,
            HasLaterExplicitProgrammableDrop(definition, operations)
                ? "EXPLICITLY_DROPPED_AFTER_FINAL_DEFINITION"
                : "ACTIVE_FINAL_DEFINITION"))
        .OrderBy(item => item.Kind, StringComparer.Ordinal)
        .ThenBy(item => item.Name, StringComparer.Ordinal)
        .ToArray();
}

static bool HasLaterExplicitProgrammableDrop(SqlDefinition definition, IEnumerable<ArchivedSql> operations)
{
    var kindPattern = definition.Derivation == "PROCEDURE" ? "(?:PROCEDURE|PROC)" : definition.Derivation;
    var dropPattern = @"\bDROP\s+" + kindPattern + @"(?:\s+IF\s+EXISTS)?\s+(?:(?:\[dbo\]|dbo)\.)?\[?" +
                      Regex.Escape(definition.Name) + @"\]?\b";
    return operations.Any(operation =>
        (string.CompareOrdinal(operation.MigrationId, definition.MigrationId) > 0 ||
         operation.MigrationId == definition.MigrationId && operation.OperationIndex > definition.OperationIndex) &&
        Regex.IsMatch(operation.Sql, dropPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
}

static GovernanceAudit[] AuditGovernanceObjects(IReadOnlyCollection<ArchivedSql> operations,
    IReadOnlyCollection<SqlDefinition> triggers)
{
    var programmable = SelectProgrammableDefinitions(operations);
    return new[]
    {
        new GovernanceAudit("TRIGGER", triggers.Count, triggers.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count()),
        new GovernanceAudit("FUNCTION", programmable.Count(item => item.Derivation == "FUNCTION"), programmable.Where(item => item.Derivation == "FUNCTION").Select(item => item.Name).Distinct(StringComparer.Ordinal).Count()),
        new GovernanceAudit("VIEW", programmable.Count(item => item.Derivation == "VIEW"), programmable.Where(item => item.Derivation == "VIEW").Select(item => item.Name).Distinct(StringComparer.Ordinal).Count()),
        new GovernanceAudit("PROCEDURE", programmable.Count(item => item.Derivation == "PROCEDURE"), programmable.Where(item => item.Derivation == "PROCEDURE").Select(item => item.Name).Distinct(StringComparer.Ordinal).Count()),
        new GovernanceAudit("SYNONYM", 0, 0), new GovernanceAudit("SECURITY_POLICY", 0, 0), new GovernanceAudit("SEQUENCE", 0, 0)
    };
}

static string RenderHelper(IReadOnlyCollection<SqlDefinition> programmable, IEnumerable<SqlDefinition> triggers,
    IReadOnlyCollection<ArchivedSql> patches)
{
    var builder = new StringBuilder();
    builder.AppendLine("// <auto-generated />");
    builder.AppendLine("using Microsoft.EntityFrameworkCore.Migrations;");
    builder.AppendLine(); builder.AppendLine("namespace ErpSystem.Data.Migrations;"); builder.AppendLine();
    builder.AppendLine("/// <summary>");
    builder.AppendLine("/// Deterministically extracted from the archived 596-migration Up operations.");
    builder.AppendLine("/// Contains all current-model and still-active non-model triggers, later exact definition patches, and final programmable dependencies.");
    builder.AppendLine("/// </summary>");
    builder.AppendLine("internal static class ArchivedGovernanceBaselineSql"); builder.AppendLine("{");
    builder.AppendLine("    internal static void Apply(MigrationBuilder migrationBuilder)"); builder.AppendLine("    {");
    foreach (var definition in programmable.Where(item => item.Derivation == "FUNCTION"))
        AppendSql(builder, definition.Sql, $"{definition.Derivation} {definition.Name} from {definition.MigrationId}:{definition.OperationIndex}");
    foreach (var trigger in triggers.OrderBy(item => item.Name, StringComparer.Ordinal))
        AppendSql(builder, trigger.Sql, $"TRIGGER {trigger.Name} from {trigger.MigrationId}:{trigger.OperationIndex} ({trigger.Derivation})");
    foreach (var patch in patches)
        AppendSql(builder, patch.Sql, $"POST-DEFINITION PATCH {string.Join(',', patch.TargetNames)} from {patch.MigrationId}:{patch.OperationIndex}");
    foreach (var definition in programmable.Where(item => item.Derivation != "FUNCTION"))
        AppendSql(builder, definition.Sql, $"{definition.Derivation} {definition.Name} from {definition.MigrationId}:{definition.OperationIndex}");
    builder.AppendLine("    }"); builder.AppendLine("}");
    return builder.ToString();
}

static void AppendSql(StringBuilder builder, string sql, string provenance)
{
    if (sql.Contains("\"\"\"", StringComparison.Ordinal))
        throw new InvalidOperationException($"SQL cannot be represented by the fixed raw-string delimiter: {provenance}.");
    builder.AppendLine($"        // {provenance}"); builder.AppendLine("        migrationBuilder.Sql(\"\"\"");
    foreach (var line in NormalizeNewlines(sql).Split('\n'))
    {
        if (string.IsNullOrWhiteSpace(line)) builder.AppendLine();
        else builder.Append("            ").AppendLine(line.TrimEnd());
    }
    builder.AppendLine("            \"\"\");");
}

static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
static string NormalizeNewlines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

static void WriteAtomic(string path, string content)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
    File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path, overwrite: true);
}

static void VerifyExactFile(string path, string expected)
{
    if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"Generated artifact is stale or missing: {path}.");
}

static string RenderArchivedCheckConstraintModel(IEnumerable<CheckConstraintDefinition> constraints)
{
    var builder = new StringBuilder();
    builder.AppendLine("// <auto-generated />");
    builder.AppendLine("using Microsoft.EntityFrameworkCore;");
    builder.AppendLine();
    builder.AppendLine("namespace ErpSystem.Data.Configuration;");
    builder.AppendLine();
    builder.AppendLine("/// <summary>");
    builder.AppendLine("/// Exact final active check-constraint state derived chronologically from the archived 596-migration Up chain.");
    builder.AppendLine("/// Applied declaratively to the current model so an empty-schema baseline never replays predecessor DDL.");
    builder.AppendLine("/// </summary>");
    builder.AppendLine("internal static class ArchivedCheckConstraintBaselineModel");
    builder.AppendLine("{");
    builder.AppendLine("    internal static void Apply(ModelBuilder modelBuilder)");
    builder.AppendLine("    {");
    foreach (var item in constraints.OrderBy(item => item.Table, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal))
    {
        builder.Append("        Apply(modelBuilder, \"").Append(EscapeCSharp(item.Table)).Append("\", \"")
            .Append(EscapeCSharp(item.Name)).Append("\", \"").Append(EscapeCSharp(item.Sql)).AppendLine("\");");
    }
    builder.AppendLine("    }");
    builder.AppendLine();
    builder.AppendLine("    private static void Apply(ModelBuilder modelBuilder, string table, string name, string sql)");
    builder.AppendLine("    {");
    builder.AppendLine("        var roots = modelBuilder.Model.GetEntityTypes()");
    builder.AppendLine("            .Where(entity => entity.BaseType is null && entity.GetTableName() == table)");
    builder.AppendLine("            .ToArray();");
    builder.AppendLine("        if (roots.Length != 1)");
    builder.AppendLine("            throw new InvalidOperationException($\"Archived check-constraint table binding is not unique: {table}.\");");
    builder.AppendLine("        modelBuilder.Entity(roots[0].ClrType).ToTable(table, tableBuilder => tableBuilder.HasCheckConstraint(name, sql));");
    builder.AppendLine("    }");
    builder.AppendLine("}");
    return builder.ToString();
}

static string EscapeCSharp(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
    .Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal)
    .Replace("\n", "\\n", StringComparison.Ordinal);

internal sealed record MigrationType(Type Type, string? Id);
internal sealed record ArchivedMigrationOperations(string Id, IReadOnlyList<MigrationOperation> Operations);
internal sealed record ArchivedSql(string MigrationId, int OperationIndex, string Sql)
{
    public string[] TargetNames { get; init; } = Array.Empty<string>();
}
internal sealed record SqlDefinition(string Name, string MigrationId, int OperationIndex, string Sql, string Derivation)
{
    public string Kind => Derivation;
}
internal sealed record GovernanceAudit(string Kind, int DefinitionCount, int UniqueNameCount);
internal sealed record ProgrammableObjectDisposition(
    string Name, string Kind, string MigrationId, int OperationIndex, string Disposition);
internal sealed record CheckConstraintDefinition(
    string Table, string Name, string Sql, string MigrationId, int OperationIndex, string Derivation);
internal sealed record RawCheckConstraintEvent(string Table, string Name, string? Sql);

internal sealed class CheckConstraintLifecycleVisitor : TSqlFragmentVisitor
{
    private readonly string _source;
    public CheckConstraintLifecycleVisitor(string source) => _source = source;
    public List<RawCheckConstraintEvent> Events { get; } = new();

    public override void ExplicitVisit(AlterTableAddTableElementStatement node)
    {
        var table = node.SchemaObjectName.BaseIdentifier?.Value;
        if (string.IsNullOrEmpty(table)) return;
        foreach (var check in node.Definition.TableConstraints.OfType<Microsoft.SqlServer.TransactSql.ScriptDom.CheckConstraintDefinition>())
        {
            var name = check.ConstraintIdentifier?.Value;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("CK_", StringComparison.Ordinal)) continue;
            var condition = _source.Substring(check.CheckCondition.StartOffset, check.CheckCondition.FragmentLength);
            Events.Add(new RawCheckConstraintEvent(table, name, condition.Trim()));
        }
    }

    public override void ExplicitVisit(AlterTableDropTableElementStatement node)
    {
        var table = node.SchemaObjectName.BaseIdentifier?.Value;
        if (string.IsNullOrEmpty(table)) return;
        foreach (var element in node.AlterTableDropTableElements)
        {
            var name = element.Name?.Value;
            if (!string.IsNullOrEmpty(name) && name.StartsWith("CK_", StringComparison.Ordinal))
                Events.Add(new RawCheckConstraintEvent(table, name, null));
        }
    }
}

internal sealed class ThrowCountingVisitor : TSqlFragmentVisitor
{
    public int Count { get; private set; }
    public override void ExplicitVisit(ThrowStatement node) => Count++;
}
