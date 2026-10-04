## RHEMA ERP task continuity

- Store Finance workstream ledgers under `docs/Finance/coordination/`.
- Use one coordination ledger per substantial Finance workstream; update it before handing work to another task or stopping with unfinished work.
- Each ledger must capture the objective, branch/worktree, exact base, commits, migrations and application status, verification evidence, known failures, remaining work, and authorization boundaries.
- New tasks must inspect the applicable ledger and current Git state before repeating investigation or implementation.
- Keep unrelated UAT observations in their own workstream ledger rather than expanding an active implementation silently.

## Consolidated Finance UAT pull requests

- When the user assigns a Finance UAT integration cycle or asks for a consolidated Finance UAT PR, follow `docs/Finance/coordination/FINANCE_UAT_CONSOLIDATION_PROTOCOL.md`.
- Create each cycle ledger from `docs/Finance/coordination/FINANCE_UAT_WORKSTREAM_TEMPLATE.md` and keep its metadata current. Retain the candidate worktree until the cycle is integrated or explicitly excluded.
- Before consolidating, run `scripts/finance/Get-FinanceUatConsolidationCandidates.ps1` for the requested cycle. Treat its matching ledgers, not every local branch or dirty file, as the proposed scope.
- Never interpret “all changes” as permission to sweep the primary checkout, historical worktrees, generated artifacts, or untagged branches into a PR.
