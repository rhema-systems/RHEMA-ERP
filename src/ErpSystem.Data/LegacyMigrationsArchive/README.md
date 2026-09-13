# Legacy EF migration source archive

This directory preserves the complete 456-migration source set that preceded the disposable-development
current-model baseline. The files are intentionally excluded from `ErpSystem.Data` compilation by the project file.
They are retained for audit, recovery analysis, and review of historical upgrade behavior; they are not an executable
or stampable migration path and must not be copied back into the compiled `Migrations` directory.

New migrations belong in `../Migrations` and must follow the current baseline normally.
