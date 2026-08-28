# HR delivery-coverage instruments

Three independent static checks that answer one question: **is a thing that exists in the
backend actually reachable by a user?**

Run them from anywhere; they locate the repo root themselves and write JSON to
`scripts/hr-coverage/out/`. That directory is intermediate output and is already covered by the
root `.gitignore`'s `out/` rule — the committed artefact is `docs/HR-CLOSURE-LEDGER.md`, which
`04_build_ledger.py` builds from it.

```bash
py -3 scripts/hr-coverage/01_endpoint_coverage.py   # must run first — writes hr_routes.json + unwired.json
py -3 scripts/hr-coverage/02_leaf_token_coverage.py # consumes 01's output
py -3 scripts/hr-coverage/03_dto_field_coverage.py  # independent
```

## What each one catches

| # | Instrument | Catches | Blind to |
|---|---|---|---|
| 01 | Route matching — every `[Http*]` in `Controllers/HR` vs every client call in `frontend/src` | Endpoints no screen calls | URLs built by helper methods (`this.sub(id, 'contacts')`) — these look unwired but are not |
| 02 | Leaf-token coverage — does the endpoint's distinctive path segment (`duty-items`, `ppe-requirements`) appear anywhere in a URL position? | Same, but survives any URL-assembly style | Segments whose word is common in the frontend for other reasons |
| 03 | Write-DTO field coverage — every `Create*Dto`/`Update*Dto` property vs every identifier in the frontend | Fields a **wired** form still cannot set | Fields present in a type but never bound to an input |

01 and 02 disagree in useful ways, so **treat only their intersection as high confidence** and
hand-verify the rest. 02 alone is conservative; 01 alone over-reports.

## Known false-positive sources — check these before filing a defect

- **Path-builder helpers.** `EmployeesController` reads as 73/81 unwired; it is fully wired via
  `employeeService.sub(employeeId, 'contacts')`. Instrument 01 cannot resolve a method call.
- **Alias routes.** A controller with two `[Route]` attributes yields two route rows per action;
  the unused alias always reads as unwired. `PerformanceImprovementPlansController` carries both
  `api/PerformanceImprovementPlans` and `api/Pip` — 18 of its 22 "gaps" are that alias.
- **Server-written entities.** Some writes are meant to have no UI caller because a service writes
  them (`EmployeeCareerPath` is written by the movement engine).
- **Other teams' modules.** `PayrollController` is not HR's to wire.
- **Portal endpoints.** Anonymous/candidate/client routes are served by a portal surface, so check
  `app/external-portal` before concluding they are unreachable.

## Interpreting a run

A rising "wired" count is the goal. If a number moves in the wrong direction after a change,
something was built without a caller — which is the exact failure this exists to prevent.
