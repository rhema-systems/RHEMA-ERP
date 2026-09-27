if (args.Length == 4 && args[0] == "--verify-snapshot")
{
    SnapshotEquivalence.Verify(args[1], args[2], args[3]);
    return;
}

if (args.Length == 3 && args[0] == "--share-statements")
{
    SharedStatementCompiler.Generate(args[1], args[2]);
    return;
}

throw new ArgumentException("Usage: ErpSystem.MigrationModelCompiler --share-statements <migration-source-directory> <generated-output-directory> OR --verify-snapshot <original-snapshot> <generated-snapshot> <API-output-directory>");
