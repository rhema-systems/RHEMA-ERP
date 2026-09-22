# Superseded late-merged migrations

These 27 migration sources were created before
`20260916132000_DisposableDevelopmentCurrentModelBaseline` but arrived on the
merged branch after the baseline cutover. Their schema is already represented
by the zero-to-current baseline.

They are preserved here for audit and source history and are excluded from the
`ErpSystem.Data` compilation. Compiling them would order their create/alter
operations before the baseline and would make a fresh database replay the same
schema twice.

New migrations remain in `../../Migrations` and must use identities after the
current baseline.
