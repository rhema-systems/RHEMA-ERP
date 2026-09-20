# Legacy EF migration source archive

This directory preserves the complete 607-migration source set from the reviewed cutover and latest-master lines
that preceded the regenerated disposable-development current-model baseline. `D3BaselineArchive` separately preserves
the prior reviewed one-baseline artifacts. `MergedBaseline20260916PreRegeneration` preserves the immediately preceding
compiled baseline and merged snapshot byte-for-byte. `PreD3HistoricalSnapshot` preserves the older excluded snapshot
away from the archive root so EF tooling cannot mistake it for the compiled snapshot. The files are intentionally excluded from `ErpSystem.Data` compilation by the project file.
They are retained for audit, recovery analysis, and review of historical upgrade behavior; they are not an executable
or stampable migration path and must not be copied back into the compiled `Migrations` directory.

New migrations belong in `../Migrations` and must follow the current baseline normally.
