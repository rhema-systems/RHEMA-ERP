# Developer database cutover for Finance PR #219

This guide applies to the disposable-development baseline in draft PR #219. The
only compiled EF migration is
`20260916132000_DisposableDevelopmentCurrentModelBaseline`. Earlier migrations
are retained as source in `LegacyMigrationsArchive`, not as an executable upgrade
chain. The baseline creates a current database from an empty catalog; it is **not**
an upgrade for a database with older migration history or data to preserve.

Opening or merging the PR does not reset anyone's database. However, normal API
startup calls `MigrateAsync` unless startup initialization is explicitly skipped.
Do not start this branch's API against an existing local ERP database until its
owner has chosen one of the paths below. Do not stamp migration history, delete
tables, or rely on a failed migration as a safe compatibility test.

## Owner response before cutover

Each developer should report, without sending connection strings, passwords, or
database backups to the PR:

1. Whether the local database is fresh/empty, disposable with test data, or
   contains data that must be retained.
2. The database name and SQL instance **privately to the cutover operator**;
   never paste credentials or machine-specific backup paths into GitHub.
3. Whether their module has unmerged schema/model changes or a migration in
   progress, with a link to its branch or PR.
4. Whether they need to test protected document uploads. That is a separate
   ClamAV runtime dependency, not a reason to reset the database.

## Decision

| Local database | Action |
| --- | --- |
| No ERP database, or a confirmed empty catalog | Create/apply the new baseline using a reviewed local setup and seed; verify the resulting schema. Do not copy another developer's connection string. |
| Existing database whose owner explicitly declares all data disposable | Preserve and verify a backup, then have the owner/operator run the guarded reset on that developer's exact local target. The reset is opt-in, destructive, and must use a clean reviewed commit/tree, explicit target/attestation, disabled C6/C7/C8 flags, and a new external evidence directory. Review the result before retrying any failure. |
| Existing database with data to retain, or uncertain ownership | **No reset and no baseline application.** Keep using the previous compatible code/database pair. A separately reviewed data-preserving migration or export/import plan is required before this developer can cut over. |

The reviewed reset contract and operator-only command are in
[`GL_CUTOVER_DEPLOYMENT_READINESS.md`](GL_CUTOVER_DEPLOYMENT_READINESS.md)
under `ResetDisposableDevelopment`. Its exact local `RhemaERP` target restriction
is intentional; it is not a generic team-wide reset command. A backup of a
disposable database is a recovery precaution, not permission to destroy data
that its owner needs. Historical backups must be retained.

## Verification and merge coordination

- Before any reset, confirm the developer's target identity, owner decision,
  backup destination/space, and clean reviewed source tree. Do not run multiple
  resets concurrently against a shared database.
- After cutover, confirm exactly one migration-history row with the baseline ID,
  1,630 application tables, 835 check constraints, 493 enabled triggers, six
  programmable objects, two consistent seed passes, DBCC success, and a valid
  evidence package. Those counts describe the current reviewed PR tree; if the
  model changes, renew the expected counts and review before execution.
- Keep `Finance:AccountingEvents:Enabled`, `Finance:ProducerIntents:Enabled`, and
  `Finance:ProducerIntentGroups:Enabled` false until their separate staged tests
  and owner approvals. The baseline being installed does not activate those paths.
- Coordinate a short migration freeze while module branches rebase. Future EF
  migrations must be generated after this baseline, not appended to the archived
  chain. Resolve model-snapshot conflicts with module owners rather than choosing
  one side automatically.
- Do not mark PR #219 ready to merge until its required CI/reviews pass and every
  affected developer has a safe path: disposable reset, fresh setup, or an
  explicit hold/data-preserving plan. A retained-data case is not solved by the
  guarded disposable reset.

Suggested team notice:

> Finance draft PR #219 introduces a new development database baseline. Please
> do not start this branch's API against your existing local ERP database yet.
> Tell us whether your database is empty, disposable, or contains data you must
> keep, and flag any unmerged migrations. Back it up locally. We will coordinate
> a reviewed reset-and-reseed for disposable databases; retained data requires a
> separate plan. No one needs to reset anything merely because the PR is open.
