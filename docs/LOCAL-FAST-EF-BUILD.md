# The fast EF build, and why `FastBuildMigrationMetadata.cs` is gone

**Status:** current as of 2026-09-19. Two audiences: anyone who hits a stale instruction telling
them to "list the migration in `FastBuildMigrationMetadata`", and anyone who wants the fast build
on their own machine.

---

## 1. The repo-wide part: that file is retired. Do not re-create it.

`src/ErpSystem.Data/Migrations/FastBuildMigrationMetadata.cs` was deleted by `bb2ccb7a`
(2026-09-13, *"finance: add disposable development current-model baseline"*), which collapsed the
migration chain into one current-model baseline and moved 607 historical migrations into
`LegacyMigrationsArchive/`, excluded from compilation.

Roughly twenty docs and build plans still say to add an entry to that file. **They are stale.** The
ones that direct live work carry a ⚠ correction; the rest are historical records and were left as
written.

⚠ **Re-creating the file breaks the build for everyone except the person who re-created it.**
Nothing in `ErpSystem.Data.csproj` excludes it any more, so a tracked copy compiles *alongside* the
generated designers and duplicates every `[DbContext]` / `[Migration]` attribute — CS0579. On a
machine running the fast build the designers are absent, so there is nothing to collide with and it
looks fine locally. CI, Release builds and every teammate fail. If you have the local fast build
installed, it refuses to compile when the file re-appears, so you find out before you push.

**What to do instead: nothing.** Scaffold the migration, edit it, update the database. There is no
listing step.

## 2. The local part: an optional per-developer fast build

Master no longer needs a fast build — it has almost no designers left to skip. A branch that has
scaffolded many migrations on top of the baseline does: each generated `*.Designer.cs` is a full
snapshot of the model (~9 MB here), and Debug builds parse all of them.

The fast build drops `Migrations\*.Designer.cs` from the compile set, keeps
`ApplicationDbContextModelSnapshot.cs` (the authoritative current model), and regenerates the
migration-discovery attributes the designers would have supplied. On `hrdev` at the time of writing
that is 35 designers and ~300 MB of C# removed from every Debug build.

It is **opt-in and local-only** — three files listed in each developer's own `.git/info/exclude`,
never committed, so teammates and CI are unaffected and full builds stay authoritative:

| File | Role |
|---|---|
| `src/ErpSystem.Data/Directory.Build.targets` | The switches and the compile-set surgery |
| `src/ErpSystem.Data/FastEfMigrationMetadata.ps1` | Generates discovery metadata into `obj/` at build time |
| `src/ErpSystem.Data/ef.ps1` | Runs `dotnet ef` with the correct flag chosen automatically |

It must be a `.targets`, not a `.props`: only `.targets` is imported after the SDK's default item
globs, which is what lets `Compile Remove` take effect.

The discovery metadata is **generated** from the designers on disk, not maintained by hand. That is
the whole point of the rewrite: the old hand-maintained file made a forgotten entry silently inert —
green build, and `MigrateAsync` quietly skipping the migration.

### Switches

| Property | Effect |
|---|---|
| `TdcFastEfBuild` | Drop the designers. Defaults **on** for Debug, off otherwise. |
| `TdcFastEfBuild=false` | Full build with the complete designer history. |
| `TdcFocusedEfToolingBuild=true` | Same trim, but allows EF tooling to run. |
| `TdcEfToolingMigrationDesigner` | Adds back one named designer for a command that needs it. |

All are read from the environment as well as `-p:`.

### EF tooling

Most `dotnet ef` commands work off the snapshot and are correct under a fast build. A few
(`migrations remove`, `migrations script`, `migrations bundle`, `database update`) generate SQL or
rewrite the snapshot from historical target models and need the full history. `ef.ps1` reads the
subcommand and picks; a guard in `Directory.Build.targets` fails loudly if EF tooling is run against
a fast build without one of the flags, so the mistake cannot be made silently.

### If migration *application* ever looks wrong

`-p:TdcFastEfBuild=false` is the first thing to rule out. Migrations applied under a fast build do
not carry their historical target model; this was the original design's accepted trade-off, and it
is the reason the EF-tooling guard exists.
