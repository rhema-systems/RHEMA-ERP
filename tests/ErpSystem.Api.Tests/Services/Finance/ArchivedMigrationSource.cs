namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Reads the recoverable, deliberately uncompiled migration archive for historical
/// transition-contract tests. Product assemblies never compile archived migration types;
/// the test project links only the eight C2-C8 predecessor bodies needed by its existing
/// prefix-safe SQL Server rehearsals.
/// </summary>
internal static class ArchivedMigrationSource
{
    private const string ArchiveDirectory = "LegacyMigrationsArchive";

    public static string Read(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || !fileName.EndsWith(".cs", StringComparison.Ordinal))
        {
            throw new ArgumentException("An archive migration source file name is required.", nameof(fileName));
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "ErpSystem.Data",
                ArchiveDirectory,
                fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            }
        }

        throw new FileNotFoundException($"Archived migration source was not found: {fileName}");
    }

    public static IReadOnlyList<string> SqlBlocks(string fileName)
    {
        var source = Read(fileName);
        var blocks = new List<string>();
        const string call = "migrationBuilder.Sql(";
        for (var search = 0; ;)
        {
            var callStart = source.IndexOf(call, search, StringComparison.Ordinal);
            if (callStart < 0) break;
            var start = callStart + call.Length;
            while (start < source.Length && char.IsWhiteSpace(source[start])) start++;

            if (source.AsSpan(start).StartsWith("\"\"\""))
            {
                start += 3;
                var end = source.IndexOf("\"\"\"", start, StringComparison.Ordinal);
                if (end < 0) throw new InvalidDataException($"Unterminated raw SQL literal in {fileName}.");
                blocks.Add(source[start..end]);
                search = end + 3;
                continue;
            }

            if (source.AsSpan(start).StartsWith("@\""))
            {
                start += 2;
                var cursor = start;
                var value = new System.Text.StringBuilder();
                while (cursor < source.Length)
                {
                    if (source[cursor] != '"')
                    {
                        value.Append(source[cursor++]);
                        continue;
                    }
                    if (cursor + 1 < source.Length && source[cursor + 1] == '"')
                    {
                        value.Append('"');
                        cursor += 2;
                        continue;
                    }
                    break;
                }
                if (cursor >= source.Length) throw new InvalidDataException($"Unterminated verbatim SQL literal in {fileName}.");
                blocks.Add(value.ToString());
                search = cursor + 1;
                continue;
            }

            var callEnd = source.IndexOf(");", start, StringComparison.Ordinal);
            if (callEnd < 0) throw new InvalidDataException($"Unterminated archived SQL call in {fileName}.");
            search = callEnd + 2;
        }

        return blocks;
    }

    public static string SqlContaining(string fileName, string token) =>
        SqlBlocks(fileName).Single(sql => sql.Contains(token, StringComparison.Ordinal));

    public static string RawStringConstant(string fileName, string constantName)
    {
        if (string.IsNullOrWhiteSpace(constantName) ||
            constantName.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("A C# identifier is required.", nameof(constantName));
        }

        var source = Read(fileName);
        var prefix = $"public const string {constantName} = \"\"\"";
        var start = source.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException($"Archived raw-string constant was not found: {constantName}.");
        start += prefix.Length;
        var end = source.IndexOf("\"\"\";", start, StringComparison.Ordinal);
        if (end < 0) throw new InvalidDataException($"Archived raw-string constant is unterminated: {constantName}.");
        return source[start..end].Trim('\r', '\n');
    }
}
