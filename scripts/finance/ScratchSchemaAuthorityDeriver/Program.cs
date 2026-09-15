using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

if (args.Length != 5)
    throw new InvalidOperationException("Usage: <governance-helper.cs> <finance-authority.cs> <baseline.cs> <manifest.json> <output.json>.");

var helperText = File.ReadAllText(args[0]);
var authorityText = File.ReadAllText(args[1]);
var baselineText = File.ReadAllText(args[2]);
using var manifest = JsonDocument.Parse(File.ReadAllText(args[3]));
var state = new Dictionary<string, string>(StringComparer.Ordinal);

var triggerBlocks = Regex.Matches(helperText,
    @"(?ms)^\s*// TRIGGER (?<name>\S+) .+?\r?\n\s*migrationBuilder\.Sql\(""""""\r?\n(?<sql>.*?)\r?\n\s*""""""\);");
foreach (Match block in triggerBlocks)
{
    var name = block.Groups["name"].Value;
    if (!state.TryAdd(name, NormalizeRawBlock(block.Groups["sql"].Value)))
        throw new InvalidOperationException($"Duplicate initial trigger {name}.");
}
if (state.Count != 478)
    throw new InvalidOperationException($"Expected 478 archived trigger definitions, found {state.Count}.");
var manifestRoot = manifest.RootElement;
var manifestTriggerNames = manifestRoot.GetProperty("triggerDefinitions").EnumerateArray()
    .Select(item => item.GetProperty("Name").GetString() ?? throw new InvalidOperationException("Manifest trigger name is null."))
    .OrderBy(item => item, StringComparer.Ordinal).ToArray();
if (manifestTriggerNames.Length != 478 ||
    !manifestTriggerNames.SequenceEqual(state.Keys.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal))
    throw new InvalidOperationException("Manifest and helper initial trigger sets differ.");

var patchBlocks = Regex.Matches(helperText,
    @"(?ms)^\s*// POST-DEFINITION PATCH (?<targets>.+?) from .+?\r?\n\s*migrationBuilder\.Sql\(""""""\r?\n(?<sql>.*?)\r?\n\s*""""""\);");
if (patchBlocks.Count != 108)
    throw new InvalidOperationException($"Expected 108 patch blocks, found {patchBlocks.Count}.");
var stuffCallCount = patchBlocks.Cast<Match>().Sum(block =>
    Regex.Matches(block.Groups["sql"].Value, @"(?i)\bSTUFF\s*\(").Count);
if (stuffCallCount != 82)
    throw new InvalidOperationException($"Expected 82 chronological STUFF calls, found {stuffCallCount}.");
if (manifestRoot.GetProperty("postDefinitionPatches").GetArrayLength() != 108 ||
    manifestRoot.GetProperty("finalUniqueTriggerCount").GetInt32() != 493 ||
    manifestRoot.GetProperty("baselineCheckConstraintCount").GetInt32() != 835)
    throw new InvalidOperationException("Manifest authority counts do not match the reviewed closed set.");

var patchSequence = 0;
string? purchaseOrderPreAlignmentSha256 = null;
string? purchaseOrderFinalAlignmentSha256 = null;
string? physicalCountPreFreezeCursorSha256 = null;
string? physicalCountFinalSha256 = null;
foreach (Match block in patchBlocks)
{
    var targets = block.Groups["targets"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    var sql = NormalizeRawBlock(block.Groups["sql"].Value);
    var before = targets.ToDictionary(name => name, name => state[name], StringComparer.Ordinal);
    var hasDynamicDefinitionTarget = Regex.IsMatch(sql,
        @"(?i)OBJECT_DEFINITION\s*\(\s*(?:OBJECT_ID\s*\(\s*N'dbo\.'\s*\+\s*)?@");
    var physicalCountCursorTargets = patchSequence == 33
        ? GetExactPhysicalCountFreezeCursorTargets(sql)
        : null;
    if (patchSequence == 20)
        purchaseOrderPreAlignmentSha256 = Hash(CanonicalTrigger(state["TR_PurchaseOrders_ApprovedSourceProtected"]));
    HashSet<string> committed;
    try
    {
        committed = patchSequence == 5 && targets.SequenceEqual(
            new[] { "TR_ProcurementFrameworkCallOffs_Lifecycle" }, StringComparer.Ordinal)
            ? InterpretReviewedAlreadyFinalPatch(sql, state, targets[0], "AND i.AgreementEffectiveEndUtc", 2)
            : patchSequence == 20 && targets.SequenceEqual(
            new[] { "TR_PurchaseOrders_ApprovedSourceProtected" }, StringComparer.Ordinal)
            ? InterpretPurchaseOrderSupportedRoutesPatch(sql, state, targets[0])
            : patchSequence == 31 && targets.SequenceEqual(new[]
            {
                "TR_PurchaseOrderItems_ApprovedCommercialCapacity",
                "TR_PurchaseOrders_ApprovedCommercialCapacity"
            }, StringComparer.Ordinal)
            ? InterpretCommercialCapacityIdentityPatch(sql, state, targets)
            : InterpretPatch(sql, state, null, false, hasDynamicDefinitionTarget);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Patch {patchSequence} ({string.Join(',', targets)}) derivation failed.", ex);
    }
    if (hasDynamicDefinitionTarget)
    {
        if (patchSequence == 33)
        {
            var expectedStaticTargets = new[]
            {
                "TR_PhysicalCountActions_AppendOnly",
                "TR_PhysicalCountItems_ControlledMutation",
                "TR_PhysicalCounts_ControlledLifecycle"
            };
            var expectedDynamicTargets = new[]
            {
                "TR_InventoryItems_PhysicalCountFreeze",
                "TR_StockMovements_PhysicalCountFreeze",
                "TR_WarehouseQuantities_PhysicalCountFreeze"
            };
            var expectedCursorOrder = new[]
            {
                "TR_WarehouseQuantities_PhysicalCountFreeze",
                "TR_InventoryItems_PhysicalCountFreeze",
                "TR_StockMovements_PhysicalCountFreeze"
            };
            if (!committed.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(expectedStaticTargets,
                    StringComparer.Ordinal) ||
                !targets.Except(committed, StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal)
                    .SequenceEqual(expectedDynamicTargets, StringComparer.Ordinal) ||
                physicalCountCursorTargets is null ||
                !physicalCountCursorTargets.SequenceEqual(expectedCursorOrder, StringComparer.Ordinal) ||
                !physicalCountCursorTargets.OrderBy(item => item, StringComparer.Ordinal)
                    .SequenceEqual(expectedDynamicTargets, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Physical-count review patch static/dynamic target classification drifted.");
            physicalCountPreFreezeCursorSha256 = Hash(CanonicalTrigger(
                state["TR_PhysicalCounts_ControlledLifecycle"]));
        }
        foreach (var target in targets.Where(name => !committed.Contains(name)))
        {
            try { committed.UnionWith(InterpretPatch(sql, state, target, true)); }
            catch (Exception ex) { throw new InvalidOperationException($"Patch {patchSequence} dynamic target {target} derivation failed.", ex); }
        }
    }
    if (committed.Count == 0 && targets.Length == 1 &&
        targets[0] == "TR_ProcurementTenderDocumentTemplateVersions_Lifecycle")
        committed.UnionWith(InterpretTenderDocumentContentBindingPatch(sql, state, targets[0]));
    var unexpected = committed.Except(targets, StringComparer.Ordinal).ToArray();
    var preservedTargets = patchSequence == 89 &&
                           targets.SequenceEqual(new[]
                           {
                               "TR_VendorPayment_DirectEvidence",
                               "TR_VendorPayment_OptionalApproval"
                           }, StringComparer.Ordinal)
        ? new[] { "TR_VendorPayment_DirectEvidence" }
        : Array.Empty<string>();
    var missing = targets.Except(committed, StringComparer.Ordinal)
        .Except(preservedTargets, StringComparer.Ordinal).ToArray();
    if (unexpected.Length != 0 || missing.Length != 0)
        throw new InvalidOperationException(
            $"Patch {patchSequence} ({string.Join(',', targets)}) did not produce its exact target set: missing={string.Join(',', missing)}; unexpected={string.Join(',', unexpected)}.");
    if (patchSequence == 20)
        purchaseOrderFinalAlignmentSha256 = Hash(CanonicalTrigger(state["TR_PurchaseOrders_ApprovedSourceProtected"]));
    patchSequence++;
}
physicalCountFinalSha256 = Hash(CanonicalTrigger(state["TR_PhysicalCounts_ControlledLifecycle"]));
var rejectedPhysicalCountCrossTargetBody = state["TR_PhysicalCounts_ControlledLifecycle"].Replace(
    "N'InProgress',N'RecountRequired'",
    "N'InProgress',N'UnderReview',N'UnderInvestigation',N'RecountRequired'",
    StringComparison.Ordinal);
var rejectedPhysicalCountCrossTargetSha256 = Hash(CanonicalTrigger(rejectedPhysicalCountCrossTargetBody));

static HashSet<string> InterpretCommercialCapacityIdentityPatch(string sql,
    Dictionary<string, string> state, IReadOnlyCollection<string> targets)
{
    var parser = new TSql160Parser(true);
    _ = parser.Parse(new StringReader(sql), out var errors);
    if (errors.Count != 0 || Regex.Matches(sql, @"(?i)\bSTUFF\s*\(").Count != 2 ||
        Regex.Matches(sql, @"(?i)\bEXEC(?:UTE)?\s+(?:sys\.)?sp_executesql\s+@definition\b").Count != 1)
        throw new InvalidOperationException("Commercial-capacity identity patch shape is not exact.");
    const string anchor = "purchaseOrder.ProcurementSourceId AS ContractId,";
    const string endMarker = ") actual";
    var descriptionMatch = Regex.Match(sql,
        @"(?is)DECLARE\s+@description\s+nvarchar\(200\)\s*=\s*N'(?<value>(?:''|[^'])*)'\s*;");
    if (!descriptionMatch.Success)
        throw new InvalidOperationException("Commercial-capacity description replacement literal is absent or ambiguous.");
    var description = descriptionMatch.Groups["value"].Value.Replace("''", "'", StringComparison.Ordinal);
    var committed = new HashSet<string>(StringComparer.Ordinal);
    foreach (var target in targets)
    {
        var definition = state[target];
        var start = definition.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || definition.IndexOf(anchor, start + anchor.Length, StringComparison.OrdinalIgnoreCase) >= 0)
            throw new InvalidOperationException($"Commercial-capacity projection anchor is not unique for {target}.");
        var finish = definition.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        if (finish <= start) throw new InvalidOperationException($"Commercial-capacity projection boundary is absent for {target}.");
        var actual = definition.Substring(start, finish - start);
        var repaired = actual;
        var replacements = 0;
        for (;;)
        {
            var caseStart = repaired.IndexOf("CASE", StringComparison.OrdinalIgnoreCase);
            if (caseStart < 0) break;
            var caseEnd = repaired.IndexOf("END", caseStart, StringComparison.OrdinalIgnoreCase);
            if (caseEnd < 0) throw new InvalidOperationException($"Commercial-capacity CASE boundary is absent for {target}.");
            var expressionLength = caseEnd + 3 - caseStart;
            var compact = Regex.Replace(repaired.Substring(caseStart, expressionLength)
                .Replace("-- TDC0502_RFQ_ITEM_MASTER_LINEAGE", "", StringComparison.OrdinalIgnoreCase), @"\s+", "")
                .ToLowerInvariant();
            const string old = "casewhenitem.inventoryitemidisnotnullthenconcat('inventory:',lower(convert(varchar(36),item.inventoryitemid)))elseconcat('description:',lower(ltrim(rtrim(isnull(item.itemdescription,'')))))end";
            const string rfqOld = "casewhenitem.sourcerfqitemidisnotnullthendbo.fn_procurementrfqsourcelineidentity(item.tenantid,item.sourcerfqitemid)whenitem.inventoryitemidisnotnullthenconcat('inventory:',lower(convert(varchar(36),item.inventoryitemid)))elseconcat('description:',lower(ltrim(rtrim(isnull(item.itemdescription,'')))))end";
            if (compact != old && compact != rfqOld)
                throw new InvalidOperationException($"Commercial-capacity identity expression is not reviewed for {target}.");
            repaired = string.Concat(repaired.AsSpan(0, caseStart), description,
                repaired.AsSpan(caseStart + expressionLength));
            replacements++;
        }
        if (replacements == 2)
            definition = string.Concat(definition.AsSpan(0, start), repaired, definition.AsSpan(finish));
        else if (replacements != 0 || CountOrdinal(Canonical(repaired), Canonical(description)) != 2)
            throw new InvalidOperationException($"Commercial-capacity patch must replace or retain exactly two identities for {target}.");
        foreach (var marker in new[]
                 {
                     "purchaseOrder.ProcurementSourceType = 2",
                     "actual.Quantity > approved.Quantity",
                     "actual.LineTotal > approved.LineTotal",
                     "Contracts contract WITH (UPDLOCK, HOLDLOCK)"
                 })
            if (!definition.Contains(marker, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Commercial-capacity final authority lacks {marker} for {target}.");
        state[target] = definition;
        committed.Add(target);
    }
    return committed;
}

static HashSet<string> InterpretReviewedAlreadyFinalPatch(string sql, Dictionary<string, string> state,
    string target, string exactGuardMarker, int expectedStuffCount)
{
    var parser = new TSql160Parser(true);
    _ = parser.Parse(new StringReader(sql), out var errors);
    if (errors.Count != 0 || Regex.Matches(sql, @"(?i)\bSTUFF\s*\(").Count != expectedStuffCount ||
        Regex.Matches(sql, @"(?i)\bEXEC(?:UTE)?\s+(?:sys\.)?sp_executesql\s+@definition\b").Count != 1 ||
        !sql.Contains("IF CHARINDEX(", StringComparison.Ordinal) ||
        !sql.Contains(exactGuardMarker, StringComparison.Ordinal) ||
        CountOrdinal(state[target], exactGuardMarker) != 1)
        throw new InvalidOperationException($"Reviewed already-final patch guard is not exact for {target}.");
    return new HashSet<string>(StringComparer.Ordinal) { target };
}
if (purchaseOrderPreAlignmentSha256 is null || purchaseOrderFinalAlignmentSha256 is null ||
    purchaseOrderPreAlignmentSha256 == purchaseOrderFinalAlignmentSha256)
    throw new InvalidOperationException("Purchase-order supported-route patch did not produce a distinct final normalized body.");
if (purchaseOrderPreAlignmentSha256 != "3E1C6AB00565FC451ACB44F65EF317DB992874C7F5E71A5A65EC9EBC56EC8F14" ||
    purchaseOrderFinalAlignmentSha256 != "8ABDBD654E35D4811D0541BD004E510E5EFC773635CFAC08054FF5A54400E6EE")
    throw new InvalidOperationException("Purchase-order supported-route pre/final authority hashes are not the exact reviewed pair.");
if (physicalCountPreFreezeCursorSha256 != "5FA3289A2B8DF1C545EB916F5A9D7D5AFE9F09139CBEFDC2A98E9313E4B03C08" ||
    physicalCountFinalSha256 != "F778FAD506C8AD0DA20605051395073B8E0BDCE6D37A0461DFCC0308E29A2B8D" ||
    rejectedPhysicalCountCrossTargetSha256 != "B06E6EEEEDC5546F1B8E6F57D80F6EE1C5BD3ACE55F9373D245EFB374398D95F")
    throw new InvalidOperationException(
        $"Physical-count lifecycle authority was mutated by the freeze-only cursor or later patches drifted: " +
        $"afterStatic={physicalCountPreFreezeCursorSha256}; final={physicalCountFinalSha256}.");

static HashSet<string> InterpretPurchaseOrderSupportedRoutesPatch(string sql,
    Dictionary<string, string> state, string target)
{
    // 20260826210000 uses two substantive STUFF operations to replace the complete
    // THROW 51202 and THROW 51205 IF blocks. This closed transformer reproduces the
    // SQL Server 1-based CHARINDEX/STUFF boundaries exactly and fails if any source
    // marker, ordering, multiplicity, or final authority body differs.
    var parser = new TSql160Parser(true);
    _ = parser.Parse(new StringReader(sql), out var errors);
    if (errors.Count != 0)
        throw new InvalidOperationException($"Purchase-order route patch parse failed: {errors[0].Number}:{errors[0].Line}:{errors[0].Column}.");
    if (Regex.Matches(sql, @"(?i)\bSTUFF\s*\(").Count != 2 ||
        Regex.Matches(sql, @"(?i)\bEXEC(?:UTE)?\s+(?:sys\.)?sp_executesql\s+@definition\b").Count != 1)
        throw new InvalidOperationException("Purchase-order route patch must contain exactly two STUFF replacements and one final execution.");
    var definition = state[target];
    definition = ReplaceThrowGuard(definition, "51202",
        "THROW 51202, 'A complete immutable approved source lineage is required for every purchase order.', 1;",
        RequiredStuffReplacement(sql, "@start51202", "@finish51202"));
    definition = ReplaceThrowGuard(definition, "51205",
        "THROW 51205, 'The approved requisition, sourcing release, sourcing case, or award-readiness decision is invalid.', 1;",
        RequiredStuffReplacement(sql, "@start51205", "@finish51205"));
    foreach (var marker in new[]
             {
                 "i.ProcurementSourceType <> 5",
                 "i.ProcurementSourceType NOT IN (4, 5)",
                 "TDC0406_PO_AMENDMENT_ID"
             })
        if (!definition.Contains(marker, StringComparison.Ordinal))
            throw new InvalidOperationException($"Purchase-order final route authority lacks exact marker: {marker}.");
    state[target] = definition;
    return new HashSet<string>(StringComparer.Ordinal) { target };
}

static string RequiredStuffReplacement(string sql, string startVariable, string finishVariable)
{
    var pattern = @"(?is)SET\s+@definition\s*=\s*STUFF\s*\(\s*@definition\s*,\s*" +
                  Regex.Escape(startVariable) + @"\s*,\s*" + Regex.Escape(finishVariable) +
                  @"\s*-\s*" + Regex.Escape(startVariable) + @"\s*,\s*N'(?<value>(?:''|[^'])*)'\s*\)\s*;";
    var matches = Regex.Matches(sql, pattern);
    if (matches.Count != 1)
        throw new InvalidOperationException($"Expected one exact substantive STUFF boundary for {startVariable}/{finishVariable}.");
    return matches[0].Groups["value"].Value.Replace("''", "'", StringComparison.Ordinal);
}

static string ReplaceThrowGuard(string definition, string markerNumber, string throwText, string replacement)
{
    var marker = "THROW " + markerNumber;
    var markerIndex = definition.IndexOf(marker, StringComparison.Ordinal);
    if (markerIndex < 0 || definition.IndexOf(marker, markerIndex + marker.Length, StringComparison.Ordinal) >= 0)
        throw new InvalidOperationException($"Expected one exact {marker} marker in the prior trigger definition.");
    var start = definition.LastIndexOf("IF EXISTS (", markerIndex, StringComparison.Ordinal);
    if (start < 0) throw new InvalidOperationException($"The IF guard before {marker} is absent.");
    var finishStart = definition.IndexOf(throwText, start, StringComparison.Ordinal);
    if (finishStart < 0 || finishStart > markerIndex)
        throw new InvalidOperationException($"The exact reviewed {marker} THROW text is absent or misordered.");
    var finish = finishStart + throwText.Length;
    return string.Concat(definition.AsSpan(0, start), replacement, definition.AsSpan(finish));
}

static HashSet<string> InterpretTenderDocumentContentBindingPatch(string sql,
    Dictionary<string, string> state, string target)
{
    // This archived operation computes two case-insensitive source-shape replacements before
    // executing @executable. Reproduce those exact replacements; the full operation hash is
    // independently bound by the generated inventory, so prefix/guard/boundary edits invalidate it.
    var parser = new TSql160Parser(true);
    var fragment = parser.Parse(new StringReader(sql), out var errors);
    if (errors.Count != 0)
        throw new InvalidOperationException($"Tender patch parse failed: {errors[0].Number}:{errors[0].Line}:{errors[0].Column}.");
    var visitor = new AssignmentVisitor();
    fragment.Accept(visitor);
    var variables = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in visitor.Events.OrderBy(item => item.StartOffset))
    {
        if (item.Variable is null || item.Expression is null) continue;
        var value = Evaluate(item.Expression, sql, state, variables, null, false);
        if (value is not null) variables[item.Variable] = value;
    }
    string Required(string name) => variables.TryGetValue(name, out var value)
        ? value.Text
        : throw new InvalidOperationException($"Tender patch required value @{name} could not be derived.");
    var definition = state[target];
    var grouped = Required("lockedOldGrouped");
    var direct = Required("lockedOldDirect");
    var lineage = Required("lineageOld");
    var groupedCount = CountOrdinalIgnoreCase(definition, grouped);
    var directCount = CountOrdinalIgnoreCase(definition, direct);
    var lineageCount = CountOrdinalIgnoreCase(definition, lineage);
    if (groupedCount + directCount != 1 || lineageCount != 1)
        throw new InvalidOperationException("Tender patch source definition is not one exact recognized legacy shape.");
    definition = ReplaceOrdinalIgnoreCase(definition, groupedCount == 1 ? grouped : direct, Required("lockedNew"));
    definition = ReplaceOrdinalIgnoreCase(definition, lineage, Required("lineageNew"));
    if (!definition.Contains("TDC-F05B-CONTENT-BINDING-LOCK-BEGIN", StringComparison.Ordinal) ||
        !definition.Contains("TDC-F05B-CONTENT-BINDING-LINEAGE-BEGIN", StringComparison.Ordinal))
        throw new InvalidOperationException("Tender patch did not produce both required authority markers.");
    state[target] = definition;
    return new HashSet<string>(StringComparer.Ordinal) { target };
}

static int CountOrdinalIgnoreCase(string source, string value)
{
    var count = 0;
    for (var offset = 0;;)
    {
        var index = source.IndexOf(value, offset, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return count;
        count++;
        offset = index + value.Length;
    }
}

static string ReplaceOrdinalIgnoreCase(string source, string oldValue, string newValue)
{
    var index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
    if (index < 0 || source.IndexOf(oldValue, index + oldValue.Length, StringComparison.OrdinalIgnoreCase) >= 0)
        throw new InvalidOperationException("Patch replacement source must occur exactly once.");
    return string.Concat(source.AsSpan(0, index), newValue, source.AsSpan(index + oldValue.Length));
}

var authorityBlocks = Regex.Matches(authorityText,
    @"(?ms)migrationBuilder\.Sql\(@""(?<sql>(?:""""|[^""])*)""\);");
if (authorityBlocks.Count != 17)
    throw new InvalidOperationException($"Expected 17 Finance authority operations, found {authorityBlocks.Count}.");
foreach (Match block in authorityBlocks)
{
    var sql = block.Groups["sql"].Value.Replace("\"\"", "\"", StringComparison.Ordinal);
    var nameMatch = Regex.Match(sql,
        @"(?i)\b(?:CREATE|ALTER)\s+TRIGGER\s+\[(?<name>TR_[^\]]+)\]");
    if (!nameMatch.Success) throw new InvalidOperationException("Finance authority operation lacks an exact trigger identity.");
    state[nameMatch.Groups["name"].Value] = Normalize(sql);
}
if (state.Count != 493)
    throw new InvalidOperationException($"Expected 493 final triggers, found {state.Count}.");

var checks = new Dictionary<string, (string Table, string Name, string Definition)>(StringComparer.Ordinal);
string? currentTable = null;
var waitingForTableName = false;
foreach (var line in baselineText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
{
    if (line.Contains("migrationBuilder.CreateTable(", StringComparison.Ordinal))
    {
        currentTable = null;
        waitingForTableName = true;
        continue;
    }
    if (waitingForTableName)
    {
        var tableMatch = Regex.Match(line, "^\\s*name: \\\"(?<value>(?:\\\\.|[^\\\"\\\\])*)\\\",$");
        if (tableMatch.Success)
        {
            currentTable = DecodeCSharpString(tableMatch.Groups["value"].Value);
            waitingForTableName = false;
        }
    }
    var checkMatch = Regex.Match(line,
        "^\\s*table\\.CheckConstraint\\(\\\"(?<name>(?:\\\\.|[^\\\"\\\\])*)\\\", \\\"(?<sql>(?:\\\\.|[^\\\"\\\\])*)\\\"\\);\\s*$");
    if (!checkMatch.Success) continue;
    if (currentTable is null) throw new InvalidOperationException("Check constraint appeared outside an exact CreateTable scope.");
    var name = DecodeCSharpString(checkMatch.Groups["name"].Value);
    var definition = DecodeCSharpString(checkMatch.Groups["sql"].Value);
    var key = currentTable + "|" + name;
    if (!checks.TryAdd(key, (currentTable, name, definition)))
        throw new InvalidOperationException($"Duplicate baseline check constraint {key}.");
}
if (checks.Count != 835)
    throw new InvalidOperationException($"Expected 835 exact baseline check constraints, found {checks.Count}.");

var output = new
{
    schema = "RHEMA_SCRATCH_EXPECTED_SCHEMA_AUTHORITY_V3",
    sourceBindings = new
    {
        archivedGovernanceHelperSha256 = HashFile(args[0]),
        financeAuthoritySha256 = HashFile(args[1]),
        baselineMigrationSha256 = HashFile(args[2]),
        governanceManifestSha256 = HashFile(args[3])
    },
    archivedInitialCount = 478,
    patchOperationCount = 108,
    stuffAudit = new
    {
        chronologicalStuffCallCount = stuffCallCount,
        blanketNoOpAllowed = false,
        supportedRouteClosedSubstantiveStuffCount = 2,
        commercialCapacityClosedSubstantiveStuffCount = 2,
        reviewedAlreadyFinalPatchCount = 1
    },
    purchaseOrderSupportedRouteAlignment = new
    {
        target = "TR_PurchaseOrders_ApprovedSourceProtected",
        preAlignmentCanonicalSha256 = purchaseOrderPreAlignmentSha256,
        finalCanonicalSha256 = purchaseOrderFinalAlignmentSha256,
        substantiveStuffCount = 2
    },
    physicalCountMixedPatchClassification = new
    {
        patchSequence = 33,
        staticTargets = new[]
        {
            "TR_PhysicalCountActions_AppendOnly",
            "TR_PhysicalCountItems_ControlledMutation",
            "TR_PhysicalCounts_ControlledLifecycle"
        },
        dynamicCursorTargets = new[]
        {
            "TR_InventoryItems_PhysicalCountFreeze",
            "TR_StockMovements_PhysicalCountFreeze",
            "TR_WarehouseQuantities_PhysicalCountFreeze"
        },
        physicalCountAfterStaticPatchCanonicalSha256 = physicalCountPreFreezeCursorSha256,
        physicalCountFinalCanonicalSha256 = physicalCountFinalSha256,
        rejectedCrossTargetFreezeCanonicalSha256 = rejectedPhysicalCountCrossTargetSha256
    },
    finalTriggerCount = 493,
    triggerDefinitions = state.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new
    {
        name = item.Key,
        canonicalSha256 = Hash(CanonicalTrigger(item.Value))
    }),
    finalCheckConstraintCount = 835,
    checkConstraintDefinitions = checks.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new
    {
        table = item.Value.Table,
        name = item.Value.Name,
        canonicalSha256 = Hash(RemoveOuter(Canonical(item.Value.Definition)))
    })
};
File.WriteAllText(args[4], JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }) + "\n",
    new UTF8Encoding(false));
Console.WriteLine($"PASS: derived {state.Count} exact final trigger hashes and {checks.Count} exact baseline check hashes through {patchBlocks.Count} ordered patches.");

static string DecodeCSharpString(string value) => JsonSerializer.Deserialize<string>('"' + value + '"')
    ?? throw new InvalidOperationException("C# string literal decoded to null.");

static string[] GetExactPhysicalCountFreezeCursorTargets(string sql)
{
    var parser = new TSql160Parser(true);
    var fragment = parser.Parse(new StringReader(sql), out var errors);
    if (errors.Count != 0)
        throw new InvalidOperationException(
            $"Physical-count patch parse failed: {errors[0].Number}:{errors[0].Line}:{errors[0].Column}.");

    var executableSql = string.Join(' ', fragment.ScriptTokenStream
        .Where(token => token.TokenType is not TSqlTokenType.WhiteSpace and
                        not TSqlTokenType.SingleLineComment and
                        not TSqlTokenType.MultilineComment)
        .Select(token => token.Text));
    var cursorBlocks = Regex.Matches(executableSql,
        @"(?is)\bDECLARE\s+@freezeName\b.*?\bDEALLOCATE\s+freezeGuards\s*;");
    if (cursorBlocks.Count != 1)
        throw new InvalidOperationException(
            $"Physical-count patch must contain exactly one executable freeze cursor block; found {cursorBlocks.Count}.");
    var cursorBlock = cursorBlocks[0].Value;
    // This hash covers the executable token stream from DECLARE @freezeName through
    // DEALLOCATE. Comments and formatting are excluded, while the validation query,
    // cursor source, fetch/loop progression and definition transformation/EXEC flow
    // are all bound byte-for-byte.
    const string reviewedCursorBlockSha256 = "3F1DC4FC90BA53AC81D9C9738FF301A5ED3A228BE44BE2BB7262E722D93FDA7F";
    var actualCursorBlockSha256 = Hash(cursorBlock);
    if (actualCursorBlockSha256 != reviewedCursorBlockSha256)
        throw new InvalidOperationException(
            $"Physical-count freeze cursor executable structure drifted ({actualCursorBlockSha256}).");

    var listMatches = Regex.Matches(cursorBlock,
        @"(?is)\bname\s+IN\s*\((?<items>\s*N'TR_[A-Za-z0-9_]+'(?:\s*,\s*N'TR_[A-Za-z0-9_]+')*\s*)\)");
    if (listMatches.Count != 2)
        throw new InvalidOperationException(
            $"Physical-count freeze cursor must contain exactly two closed name IN lists; found {listMatches.Count}.");

    var parsedLists = listMatches.Cast<Match>().Select(match =>
    {
        var items = match.Groups["items"].Value;
        var tokenMatches = Regex.Matches(items, @"N'(?<name>TR_[A-Za-z0-9_]+)'");
        var names = tokenMatches.Cast<Match>().Select(item => item.Groups["name"].Value).ToArray();
        var compact = Regex.Replace(items, @"\s+", string.Empty, RegexOptions.CultureInvariant);
        var reconstructed = string.Join(',', names.Select(name => $"N'{name}'"));
        if (names.Length == 0 || compact != reconstructed ||
            names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidOperationException(
                "Physical-count freeze cursor target list is malformed or contains duplicate names.");
        return names;
    }).ToArray();
    if (!parsedLists[0].SequenceEqual(parsedLists[1], StringComparer.Ordinal))
        throw new InvalidOperationException(
            "Physical-count freeze validation and cursor target lists do not agree exactly.");
    return parsedLists[0];
}

static string RemoveOuter(string value)
{
    var current = value;
    while (current.Length >= 2 && current[0] == '(' && current[^1] == ')')
    {
        var depth = 0;
        var enclosesAll = true;
        for (var index = 0; index < current.Length; index++)
        {
            depth += current[index] == '(' ? 1 : current[index] == ')' ? -1 : 0;
            if (depth == 0 && index < current.Length - 1) { enclosesAll = false; break; }
            if (depth < 0) throw new InvalidOperationException("Unbalanced expected check definition.");
        }
        if (!enclosesAll || depth != 0) break;
        current = current[1..^1];
    }
    return current;
}

static HashSet<string> InterpretPatch(string sql, Dictionary<string, string> state, string? dynamicTarget,
    bool dynamicOnly, bool allowUnboundDynamicStuff = false)
{
    var parser = new TSql160Parser(true);
    var fragment = parser.Parse(new StringReader(sql), out var errors);
    if (errors.Count != 0)
        throw new InvalidOperationException($"Patch parse failed: {errors[0].Number}:{errors[0].Line}:{errors[0].Column}.");
    var visitor = new AssignmentVisitor();
    fragment.Accept(visitor);
    var events = visitor.Events.OrderBy(item => item.StartOffset).ToArray();
    var variables = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
    var committed = new HashSet<string>(StringComparer.Ordinal);
    foreach (var item in events)
    {
        if (item.ExecuteVariable is not null)
        {
            if (variables.TryGetValue(item.ExecuteVariable, out var executable) && executable.Origin is not null &&
                (!dynamicOnly || executable.Origin == dynamicTarget))
            {
                state[executable.Origin] = executable.Text;
                committed.Add(executable.Origin);
            }
            continue;
        }
        if (item.Variable is null || item.Expression is null) continue;
        var value = Evaluate(item.Expression, sql, state, variables, dynamicTarget, dynamicOnly);
        if (value is null)
        {
            var expressionText = sql.Substring(item.Expression.StartOffset, item.Expression.FragmentLength);
            if (Regex.IsMatch(expressionText, @"(?i)\bSTUFF\s*\("))
            {
                var inputVariable = item.Expression is FunctionCall stuff && stuff.Parameters.Count != 0 &&
                                    stuff.Parameters[0] is VariableReference inputReference
                    ? VariableKey(inputReference.Name)
                    : null;
                if ((dynamicOnly || allowUnboundDynamicStuff) && inputVariable is not null &&
                    !variables.ContainsKey(inputVariable))
                    continue;
                throw new InvalidOperationException("A STUFF assignment was not an exact supported header promotion or closed substantive transformation: " +
                                                    Regex.Replace(expressionText, @"\s+", " "));
            }
        }
        if (value is not null) variables[item.Variable] = value;
    }
    return committed;
}

static Value? Evaluate(ScalarExpression expression, string source, Dictionary<string, string> state,
    Dictionary<string, Value> variables, string? dynamicTarget, bool dynamicOnly)
{
    switch (expression)
    {
        case StringLiteral literal:
            return new Value(NormalizeLiteral(literal.Value), null);
        case IntegerLiteral integer:
            return new Value(integer.Value, null, true);
        case VariableReference variable:
            return variables.TryGetValue(VariableKey(variable.Name), out var value) ? value : null;
        case ParenthesisExpression parenthesis:
            return Evaluate(parenthesis.Expression, source, state, variables, dynamicTarget, dynamicOnly);
        case BinaryExpression binary when binary.BinaryExpressionType is BinaryExpressionType.Add or BinaryExpressionType.Subtract:
        {
            var left = Evaluate(binary.FirstExpression, source, state, variables, dynamicTarget, dynamicOnly);
            var right = Evaluate(binary.SecondExpression, source, state, variables, dynamicTarget, dynamicOnly);
            if (left is null || right is null) return null;
            if (left.IsNumber && right.IsNumber && long.TryParse(left.Text, out var leftNumber) &&
                long.TryParse(right.Text, out var rightNumber))
                return new Value((binary.BinaryExpressionType == BinaryExpressionType.Add
                    ? leftNumber + rightNumber
                    : leftNumber - rightNumber).ToString(), null, true);
            if (binary.BinaryExpressionType != BinaryExpressionType.Add) return null;
            if (left.Origin is not null && right.Origin is not null && left.Origin != right.Origin)
                throw new InvalidOperationException("Patch concatenates values from different trigger definitions.");
            return new Value(left.Text + right.Text, left.Origin ?? right.Origin);
        }
        case FunctionCall function:
        {
            var name = function.FunctionName.Value.ToUpperInvariant();
            if (name == "OBJECT_DEFINITION")
            {
                var fragment = source.Substring(function.StartOffset, function.FragmentLength);
                var match = Regex.Match(fragment, @"(?i)TR_[A-Za-z0-9_]+");
                if (match.Success)
                {
                    var target = match.Value;
                    if (dynamicOnly && target != dynamicTarget) return null;
                    return state.TryGetValue(target, out var definition) ? new Value(definition, target) : null;
                }
                if (!dynamicOnly || dynamicTarget is null) return null;
                return new Value(state[dynamicTarget], dynamicTarget);
            }
            if (name is "CHAR" or "NCHAR")
            {
                if (function.Parameters.SingleOrDefault() is IntegerLiteral integer && int.TryParse(integer.Value, out var code))
                    return new Value(((char)code).ToString(), null);
                return null;
            }
            if (name is "UPPER" or "LOWER" && function.Parameters.Count == 1)
            {
                var input = Evaluate(function.Parameters[0], source, state, variables, dynamicTarget, dynamicOnly);
                if (input is null || input.IsNumber) return null;
                return new Value(name == "UPPER" ? input.Text.ToUpperInvariant() : input.Text.ToLowerInvariant(), input.Origin);
            }
            if (name == "LEN" && function.Parameters.Count == 1)
            {
                var input = Evaluate(function.Parameters[0], source, state, variables, dynamicTarget, dynamicOnly);
                if (input is null || input.IsNumber) return null;
                return new Value(input.Text.TrimEnd(' ').Length.ToString(), null, true);
            }
            if (name == "CHARINDEX" && function.Parameters.Count is 2 or 3)
            {
                var needle = Evaluate(function.Parameters[0], source, state, variables, dynamicTarget, dynamicOnly);
                var haystack = Evaluate(function.Parameters[1], source, state, variables, dynamicTarget, dynamicOnly);
                var start = function.Parameters.Count == 3
                    ? Evaluate(function.Parameters[2], source, state, variables, dynamicTarget, dynamicOnly)
                    : new Value("1", null, true);
                if (needle is null || haystack is null || start is null || !start.IsNumber ||
                    !int.TryParse(start.Text, out var oneBasedStart)) return null;
                var zeroBasedStart = Math.Max(0, oneBasedStart - 1);
                var index = haystack.Text.IndexOf(needle.Text, zeroBasedStart, StringComparison.OrdinalIgnoreCase);
                return new Value((index < 0 ? 0 : index + 1).ToString(), null, true);
            }
            if (name == "SUBSTRING" && function.Parameters.Count == 3)
            {
                var input = Evaluate(function.Parameters[0], source, state, variables, dynamicTarget, dynamicOnly);
                var start = Evaluate(function.Parameters[1], source, state, variables, dynamicTarget, dynamicOnly);
                var length = Evaluate(function.Parameters[2], source, state, variables, dynamicTarget, dynamicOnly);
                if (input is null || start is null || length is null || !start.IsNumber || !length.IsNumber ||
                    !int.TryParse(start.Text, out var oneBasedStart) || !int.TryParse(length.Text, out var count) ||
                    oneBasedStart < 1 || count < 0) return null;
                var zeroBasedStart = Math.Min(input.Text.Length, oneBasedStart - 1);
                count = Math.Min(count, input.Text.Length - zeroBasedStart);
                return new Value(input.Text.Substring(zeroBasedStart, count), input.Origin);
            }
            if (name == "REPLACE" && function.Parameters.Count == 3)
            {
                var input = Evaluate(function.Parameters[0], source, state, variables, dynamicTarget, dynamicOnly);
                var oldValue = Evaluate(function.Parameters[1], source, state, variables, dynamicTarget, dynamicOnly);
                var newValue = Evaluate(function.Parameters[2], source, state, variables, dynamicTarget, dynamicOnly);
                if (input is null || oldValue is null || newValue is null) return null;
                return new Value(input.Text.Replace(oldValue.Text, newValue.Text, StringComparison.Ordinal), input.Origin);
            }
            if (name == "STUFF" && function.Parameters.Count == 4)
            {
                var fragmentText = Regex.Replace(source.Substring(function.StartOffset, function.FragmentLength), @"\s+", string.Empty);
                var headerOnly = Regex.IsMatch(fragmentText,
                        @"(?i)^STUFF\(@[A-Za-z0-9_]+,(?:1|@[A-Za-z0-9_]+),(?:@[A-Za-z0-9_]+-1|@[A-Za-z0-9_]+-@[A-Za-z0-9_]+|LEN\(N?'CREATE'\)),N?'(?:CREATEORALTER|ALTER)?'\)$") ||
                    Regex.IsMatch(fragmentText,
                        @"(?i)^STUFF\(@[A-Za-z0-9_]+,CHARINDEX\(N?'CREATE',UPPER\(@[A-Za-z0-9_]+\)\),LEN\(N?'CREATE'\),N?'CREATEORALTER'\)$") ||
                    Regex.IsMatch(fragmentText,
                        @"(?i)^STUFF\(@[A-Za-z0-9_]+,1,CHARINDEX\(N?'TRIGGER',UPPER\(@[A-Za-z0-9_]+\)\)-1,N?'CREATEORALTER'\)$");
                if (headerOnly)
                    return Evaluate(function.Parameters[0], source, state, variables, dynamicTarget, dynamicOnly);
                var input = Evaluate(function.Parameters[0], source, state, variables, dynamicTarget, dynamicOnly);
                var start = Evaluate(function.Parameters[1], source, state, variables, dynamicTarget, dynamicOnly);
                var length = Evaluate(function.Parameters[2], source, state, variables, dynamicTarget, dynamicOnly);
                var replacement = Evaluate(function.Parameters[3], source, state, variables, dynamicTarget, dynamicOnly);
                if (input is null || replacement is null) return null;
                if (start is null || length is null || !start.IsNumber || !length.IsNumber ||
                    !int.TryParse(start.Text, out var oneBasedStart) || !int.TryParse(length.Text, out var count) ||
                    oneBasedStart < 1 || count < 0 || oneBasedStart - 1 > input.Text.Length)
                {
                    // A chronological patch can be an exact retry against a later selected CREATE
                    // body that already contains its entire intended replacement. Accept only one
                    // exact occurrence; otherwise the substantive transformation remains unresolved.
                    if (replacement.Text.Length != 0 && CountOrdinal(input.Text, replacement.Text) == 1)
                        return input;
                    return null;
                }
                var zeroBasedStart = oneBasedStart - 1;
                count = Math.Min(count, input.Text.Length - zeroBasedStart);
                return new Value(string.Concat(input.Text.AsSpan(0, zeroBasedStart), replacement.Text,
                    input.Text.AsSpan(zeroBasedStart + count)), input.Origin);
            }
            if (name == "CONCAT")
            {
                var values = function.Parameters.Select(item => Evaluate(item, source, state, variables, dynamicTarget, dynamicOnly)).ToArray();
                if (values.Any(item => item is null)) return null;
                var origins = values.Where(item => item!.Origin is not null).Select(item => item!.Origin!).Distinct(StringComparer.Ordinal).ToArray();
                if (origins.Length > 1) throw new InvalidOperationException("Patch CONCAT spans multiple triggers.");
                return new Value(string.Concat(values.Select(item => item!.Text)), origins.SingleOrDefault());
            }
            return null;
        }
        default:
            return null;
    }
}

static string NormalizeRawBlock(string value)
{
    var lines = Normalize(value).Split('\n');
    var minimumIndent = lines.Where(line => line.Length != 0)
        .Select(line => line.TakeWhile(char.IsWhiteSpace).Count()).DefaultIfEmpty(0).Min();
    return string.Join("\n", lines.Select(line => line.Length >= minimumIndent ? line[minimumIndent..] : line)).Trim();
}

static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal)
    .Replace("\r", "\n", StringComparison.Ordinal).Trim();
static string NormalizeLiteral(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal)
    .Replace("\r", "\n", StringComparison.Ordinal);
static string Canonical(string value) => Regex.Replace(value, @"\s+", string.Empty,
    RegexOptions.CultureInvariant).ToUpperInvariant();
static string CanonicalTrigger(string value) => Regex.Replace(Canonical(value),
    @"^(?:CREATEORALTER|CREATE|ALTER)TRIGGER", "TRIGGER", RegexOptions.CultureInvariant);
static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static int CountOrdinal(string source, string value)
{
    var count = 0;
    for (var offset = 0;;)
    {
        var index = source.IndexOf(value, offset, StringComparison.Ordinal);
        if (index < 0) return count;
        count++;
        offset = index + value.Length;
    }
}
static string VariableKey(string value) => value.TrimStart('@');

sealed record Value(string Text, string? Origin, bool IsNumber = false);
sealed record Event(int StartOffset, string? Variable, ScalarExpression? Expression, string? ExecuteVariable);

sealed class AssignmentVisitor : TSqlFragmentVisitor
{
    public List<Event> Events { get; } = new();
    public override void ExplicitVisit(DeclareVariableElement node)
    {
        if (node.Value is not null) Events.Add(new Event(node.StartOffset, node.VariableName.Value.TrimStart('@'), node.Value, null));
    }
    public override void ExplicitVisit(SetVariableStatement node) =>
        Events.Add(new Event(node.StartOffset, node.Variable.Name.TrimStart('@'), node.Expression, null));
    public override void ExplicitVisit(SelectSetVariable node) =>
        Events.Add(new Event(node.StartOffset, node.Variable.Name.TrimStart('@'), node.Expression, null));
    public override void ExplicitVisit(ExecuteStatement node)
    {
        var text = string.Concat(node.ScriptTokenStream.Skip(node.FirstTokenIndex)
            .Take(node.LastTokenIndex - node.FirstTokenIndex + 1).Select(token => token.Text));
        var match = Regex.Match(text, @"(?i)\bEXEC(?:UTE)?\s+(?:sys\.)?sp_executesql\s+@(?<variable>[A-Za-z0-9_]+)");
        if (match.Success) Events.Add(new Event(node.StartOffset, null, null, match.Groups["variable"].Value.TrimStart('@')));
    }
}
