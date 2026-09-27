using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Optional fallback: keep each complete model's ordered wrapper and factor only
// byte-identical generated statements with the same namespace/import context.
internal static class SharedStatementCompiler
{
    public static void Generate(string sourceDirectory, string outputDirectory)
    {
        sourceDirectory = Path.GetFullPath(sourceDirectory);
        outputDirectory = Path.GetFullPath(outputDirectory);
        if (sourceDirectory.Equals(outputDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Generated model output must be separate from source.");
        Directory.CreateDirectory(outputDirectory);
        var sources = Directory.GetFiles(sourceDirectory, "*.Designer.cs")
            .Append(Path.Combine(sourceDirectory, "ApplicationDbContextModelSnapshot.cs"))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var contexts = new Dictionary<string, Context>(StringComparer.Ordinal);
        var outputs = new List<string>();
        var models = new List<object>();
        var totalStatements = 0;
        foreach (var path in sources)
        {
            var source = File.ReadAllText(path);
            var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source).GetRoot();
            ValidateSyntax(root, path);
            if (root.Members.Count != 1 || root.Members[0] is not NamespaceDeclarationSyntax ns
                || ns.Members.Count != 1 || ns.Members[0] is not ClassDeclarationSyntax type
                || root.Externs.Count != 0 || ns.Externs.Count != 0 || ns.Usings.Count != 0)
                throw new InvalidOperationException($"Unsupported generated namespace structure: {path}");
            var methods = type.Members.OfType<MethodDeclarationSyntax>().ToArray();
            if (type.Members.Count != 1 || methods.Length != 1 || methods[0].Identifier.ValueText is not ("BuildTargetModel" or "BuildModel")
                || !methods[0].Modifiers.Any(SyntaxKind.OverrideKeyword))
                throw new InvalidOperationException($"Unsupported generated class members: {path}");
            var method = methods[0];
            var body = method.Body ?? throw new InvalidOperationException($"Missing generated body: {path}");
            if (method.ParameterList.Parameters.Count != 1 || method.ParameterList.Parameters[0].Identifier.ValueText != "modelBuilder"
                || body.Statements.Any(statement => statement is not ExpressionStatementSyntax)
                || body.DescendantNodes().Any(node => node is ThisExpressionSyntax or BaseExpressionSyntax)
                || body.DescendantNodes().OfType<IdentifierNameSyntax>().Any(name =>
                    name.Identifier.ValueText == type.Identifier.ValueText || name.Identifier.ValueText == method.Identifier.ValueText)
                || root.DescendantTrivia(descendIntoTrivia: true).Any(trivia => trivia.HasStructure
                    && trivia.GetStructure() is DirectiveTriviaSyntax and not (NullableDirectiveTriviaSyntax or PragmaWarningDirectiveTriviaSyntax)))
                throw new InvalidOperationException($"Unsupported model context for common-statement factoring: {path}");

            // Aliases, namespace-local usings and parameter types are part of the key.
            // Distinct semantic lookup contexts never share compiled statements.
            var imports = string.Concat(root.Usings.Select(value => value.ToFullString()))
                + string.Concat(ns.Usings.Select(value => value.ToFullString()));
            var nullable = string.Concat(root.DescendantTrivia(descendIntoTrivia: true)
                .Where(trivia => trivia.GetStructure() is NullableDirectiveTriviaSyntax).Select(trivia => trivia.ToFullString()));
            var contextText = ns.Name + "\n" + imports + "\n" + nullable + "\n" + method.ParameterList;
            var contextHash = Hash(contextText);
            if (!contexts.TryGetValue(contextHash, out var context))
            {
                context = new Context(contextText, ns.Name.ToString(), imports, nullable, method.ParameterList.ToString(), contextHash);
                contexts.Add(contextHash, context);
            }
            else if (context.Text != contextText) throw new InvalidOperationException("Context hash collision.");

            var originalBody = source[body.OpenBraceToken.Span.End..body.CloseBraceToken.SpanStart];
            var ordered = new List<string>();
            var reconstructed = new StringBuilder();
            var cursor = body.OpenBraceToken.Span.End;
            foreach (var statement in body.Statements)
            {
                reconstructed.Append(source[cursor..statement.FullSpan.Start]);
                var text = source[statement.FullSpan.Start..statement.FullSpan.End];
                var key = Hash(text);
                if (context.Statements.TryGetValue(key, out var retained) && retained != text)
                    throw new InvalidOperationException("Statement hash collision.");
                context.Statements[key] = text;
                ordered.Add(key);
                reconstructed.Append(context.Statements[key]);
                cursor = statement.FullSpan.End;
            }
            reconstructed.Append(source[cursor..body.CloseBraceToken.SpanStart]);
            if (reconstructed.ToString() != originalBody) throw new InvalidOperationException($"Model reconstruction mismatch: {path}");
            var wrapper = new StringBuilder("\n");
            foreach (var key in ordered)
                wrapper.AppendLine($"            global::{context.Namespace}.{context.ClassName}.Apply_{key}(modelBuilder);");
            var generated = source[..body.OpenBraceToken.Span.End] + wrapper + "        " + source[body.CloseBraceToken.SpanStart..];
            var generatedRoot = CSharpSyntaxTree.ParseText(generated).GetRoot();
            ValidateSyntax(generatedRoot, path);
            if (!root.DescendantNodes().OfType<AttributeListSyntax>().Select(value => value.ToFullString()).SequenceEqual(
                    generatedRoot.DescendantNodes().OfType<AttributeListSyntax>().Select(value => value.ToFullString()), StringComparer.Ordinal))
                throw new InvalidOperationException($"Discovery attributes changed: {path}");
            var output = Path.Combine(outputDirectory, Path.GetFileName(path));
            Write(output, generated); outputs.Add(output);
            totalStatements += ordered.Count;
            models.Add(new { File = Path.GetFileName(path), SourceSha256 = Hash(source), ContextSha256 = contextHash,
                ModelBodySha256 = Hash(originalBody), ReconstructedBodySha256 = Hash(reconstructed.ToString()), OrderedStatementHashes = ordered });
        }
        foreach (var context in contexts.Values.OrderBy(value => value.Hash, StringComparer.Ordinal))
        {
            var shared = new StringBuilder("// <auto-generated />\n");
            shared.Append(context.Imports).AppendLine().Append(context.Nullable).AppendLine();
            shared.AppendLine("#pragma warning disable 612, 618");
            shared.AppendLine($"namespace {context.Namespace}\n{{\n    internal static class {context.ClassName}\n    {{");
            foreach (var entry in context.Statements.OrderBy(value => value.Key, StringComparer.Ordinal))
                shared.AppendLine($"        internal static void Apply_{entry.Key}{context.Parameters}\n        {{\n{entry.Value}\n        }}");
            shared.AppendLine("    }\n}");
            ValidateSyntax(CSharpSyntaxTree.ParseText(shared.ToString()).GetRoot(), context.ClassName);
            var output = Path.Combine(outputDirectory, context.ClassName + ".g.cs");
            Write(output, shared.ToString()); outputs.Add(output);
        }
        Write(Path.Combine(outputDirectory, "compile-files.txt"), string.Join(Environment.NewLine, outputs));
        Write(Path.Combine(outputDirectory, "preservation-manifest.json"), JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Preserved {sources.Length} full models and {totalStatements} ordered statements; compiled {contexts.Values.Sum(value => value.Statements.Count)} distinct statements across {contexts.Count} lookup contexts.");
    }

    private sealed record Context(string Text, string Namespace, string Imports, string Nullable, string Parameters, string Hash)
    {
        public string ClassName => "__PreservedSharedModel_" + Hash;
        public Dictionary<string, string> Statements { get; } = new(StringComparer.Ordinal);
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void Write(string path, string value)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != value) File.WriteAllText(path, value, new UTF8Encoding(false));
    }
    private static void ValidateSyntax(SyntaxNode root, string path)
    {
        var errors = root.GetDiagnostics().Where(value => value.Severity == DiagnosticSeverity.Error).Take(3).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException($"Invalid model syntax in {path}: {string.Join("; ", errors.Select(value => value.ToString()))}");
    }
}
