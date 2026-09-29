# Complete migration models with shared statements

Normal `ErpSystem.Data` builds run this SDK-only C# tool before compilation. The repository's pinned .NET 9 SDK supplies Roslyn; there are no new NuGet dependencies or production runtime dependencies.

The generated EF designers and snapshot repeat very large model methods. Compiling their nested lambdas exhausted the compiler heap even with serial compilation, optimization, and a 12 GB cap. Normal builds retain each complete model's ordered wrapper and factor byte-identical generated statements into shared static helpers. Statements share helpers only when their namespace, imports, nullable context and parameter types match. No migration source or model statement is removed.

Generated files and a preservation manifest live exclusively under the Data project's intermediate `obj/MigrationSharedModels` directory. Before emitting a file, the tool verifies valid syntax, the supported expression-only method structure, exact reconstructed original body content and ordering, each model statement, and all attributes. It rejects class members other than the model override, namespace-local imports, instance references and conditional directives. Unsupported structures fail the build. MSBuild reads an exact output manifest instead of a wildcard, so stale generated files cannot introduce removed migration identities.

The explicit `TdcFastEfBuild` diagnostic path bypasses this transformation and retains its existing restrictions. It remains unsuitable for deployment, migration scaffolding, or migration acceptance. Normal builds continue to include every current migration designer, the full snapshot, and the existing SDK/EF analyzers.

For a standalone preservation check:

```powershell
dotnet build tools/ErpSystem.MigrationModelCompiler/ErpSystem.MigrationModelCompiler.csproj
dotnet tools/ErpSystem.MigrationModelCompiler/bin/Debug/net9.0/ErpSystem.MigrationModelCompiler.dll --share-statements src/ErpSystem.Data/Migrations src/ErpSystem.Data/obj/Debug/net8.0/MigrationSharedModels
```

The preservation manifest is supporting evidence; a successful full build, EF discovery/model checks, and migration acceptance remain required.

After a normal API build, an additional semantic check can independently compile the untouched and generated snapshots against the application assemblies, construct both EF models, and compare their complete model debug representations:

```powershell
dotnet tools/ErpSystem.MigrationModelCompiler/bin/Debug/net9.0/ErpSystem.MigrationModelCompiler.dll --verify-snapshot src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs src/ErpSystem.Data/obj/Debug/net8.0/MigrationSharedModels/ApplicationDbContextModelSnapshot.cs src/ErpSystem.Api/bin/Debug/net8.0
```
