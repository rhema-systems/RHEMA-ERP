using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class SnapshotEquivalence
{
    public static void Verify(string originalPath, string generatedPath, string referenceDirectory)
    {
        referenceDirectory = Path.GetFullPath(referenceDirectory);
        var runtimeAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var paths = runtimeAssemblies.Concat(Directory.GetFiles(referenceDirectory, "*.dll"))
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
        var references = paths.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            var path = Path.Combine(referenceDirectory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        }
        AssemblyLoadContext.Default.Resolving += Resolve;
        try
        {
            var original = Render(originalPath, references, "OriginalSnapshotVerification", includeSharedHelpers: false);
            var generated = Render(generatedPath, references, "ChunkedSnapshotVerification", includeSharedHelpers: true);
            if (!string.Equals(original, generated, StringComparison.Ordinal))
                throw new InvalidOperationException("Original and chunked EF snapshots produced different complete model debug representations.");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original)));
            Console.WriteLine($"EF snapshot semantic comparison passed: {original.Length:N0} model characters; SHA256 {hash}.");
        }
        finally
        {
            AssemblyLoadContext.Default.Resolving -= Resolve;
        }
    }

    private static string Render(string path, MetadataReference[] references, string assemblyName, bool includeSharedHelpers)
    {
        const string adapter = """
            using Microsoft.EntityFrameworkCore.Infrastructure;
            public static class SnapshotVerificationAdapter
            {
                public static string Render() => new ErpSystem.Data.Migrations.ApplicationDbContextModelSnapshot()
                    .Model.ToDebugString(MetadataDebugStringOptions.LongDefault);
            }
            """;
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: Path.GetFullPath(path)),
            CSharpSyntaxTree.ParseText(adapter, path: "SnapshotVerificationAdapter.cs") };
        if (includeSharedHelpers)
            trees.AddRange(Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(path))!, "__PreservedSharedModel_*.g.cs")
                .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file)));
        var compilation = CSharpCompilation.Create(assemblyName, trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release,
                concurrentBuild: false));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        if (!emit.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine,
                emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        return (string)assembly.GetType("SnapshotVerificationAdapter")!.GetMethod("Render")!.Invoke(null, null)!;
    }
}
