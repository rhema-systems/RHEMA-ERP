# HR Recruitment Closure — Build Plan

> The closure programme's recruitment block: the 31 BUILD endpoints in `docs/HR-CLOSURE-LEDGER.md`
> (sections C/D2) that close out HR recruitment. Written 2026-08-30 at kickoff, from the ledger,
> the port plan's area 6/26 rows, the Blazor spec (`D:\ERP Demo\HRApi\ErpSystem.BlazorServer\Pages\Careers\`,
> 15 pages), and two code surveys. The loop per slice: **backend checks → build → harness → stage**
> (user commits; user runs builds; user scaffolds migrations, I edit + list, user updates).

## Decisions taken at kickoff (2026-08-30, user-confirmed)

1. **Candidates get a separate `Candidate` role** — NOT `ExternalUser`. Reason:
   `ExternalUserAccessMiddleware`'s allowlist for ExternalUser includes `/api/procurement`
   wholesale (live cross-module hole #11a) plus projects/estate/support surfaces. Candidates are
   anonymous self-registrants; business partners are vetted counterparties.
2. **Browse public, apply logged-in.** `api/public` keeps the anonymous vacancy board (with
   `X-Tenant-Id`); apply + CV upload move behind candidate login. Matches the Blazor-era flow.
3. **The candidate portal is retired — code AND schema.** `api/portal/*`, the candidate half of
   `PortalAuth`, and the `CandidatePortalAccounts` table all go. ⚠ `PortalBearer` itself STAYS —
   the consultant-client portal runs on it.
4. **Scope = the 31 recruitment endpoints.** Consultant-client portal (area 27) and the Awards
   REVIEW classification are OUT.

## Survey facts the slices are built on (verified 2026-08-30)

- `CandidatePortalController` has **17 endpoints** (the ledger's 8 counts writes only);
  `CandidatePortalAuthController` has 6. **No frontend anywhere calls `api/portal/*`** — the only
  UI ever built for it was the Blazor app. Retirement is backend + schema + docs only.
- `CandidatePortalAccounts` (created in `20260720181131_AddHRModule.cs:7222`) has **only outbound
  FKs** (→ JobCandidates nullable, → Tenants). Nothing references it. Dropping orphans nothing.
  The one datum worth keeping if rows exist: the account→JobCandidate email link (pre-provisioning
  input, not a referential blocker). **Check row count before the drop migration.**
- `CandidatePortalService` (12 methods) is **reusable almost verbatim** — every method keys on
  accountId+tenantId and resolves `CandidatePortalAccount → JobCandidateId` via
  `GetOwnedAccountAsync` (`CandidatePortalService.cs:91`). The rebuild swaps that one resolver
  for `ApplicationUser → JobCandidate`.
- ⚠ **`InternalOnly` is a blocklist of exactly one role**
  (`ServiceCollectionExtensions.cs:1334-1337`: authenticated AND NOT ExternalUser). A `Candidate`
  role **satisfies it** everywhere until it is extended. This is the sharpest trap in the block.
- ⚠ **`RecordPortalCandidateResponseAsync`** (`JobOfferHireService.cs:1037`) validates ownership
  **in the calling controller only** (`CandidatePortalController.cs:525-527`). Deleting that
  controller without re-implementing the check leaves an unguarded application-id mutation.
- `CurrentUserService.IsExternalUser` (`:187-193`) is role-based (`ExternalUser` only) and consumed
  across Procurement/Projects/Estate — a Candidate reads as `false` in all of them. Candidates are
  fenced by their own middleware + policy instead (below), so this stays correct; do not widen it.
- Frontend: the external-portal shell authenticates with the ordinary `apiService` main JWT — no
  new client auth plumbing needed. `external-sidebar.tsx` menu is **hardcoded, not role-filtered**
  (18 partner items at `:45-137`). `isExternalPortalUser` (`lib/auth-routing.ts:11`) is a
  single-role predicate branched in login/dashboard/use-auth/external-portal-layout.
- The **supplier-application flow is the worked precedent**: public form → register → portal
  (`app/supplier-application/`, `SupplierApplicantAccessMiddleware` at `Program.cs:554`, the
  claim-based `SupplierApplicantOnly` policy at `ServiceCollectionExtensions.cs:1327`).
- The public CV upload is already on the controlled gate (`HrCandidateCv` category, scanning,
  hashed 2h single-claim tickets, sweeper). Keep the mechanism; behind login the actor becomes the
  real user id instead of `PublicPortalAnonymous` and `X-Tenant-Id` yields to the token's tenant.
- Register flow deltas to reconcile when cloning `AuthController.Register` (`:1558-1717`): main
  flow = username+phone, SMS OTP, tenant by host; candidate flow was email-only, email-link
  verification, tenant by explicit `X-Tenant-Id`. Candidates arrive from a tenant-specific job
  board, so the candidate register keeps the explicit tenant.

## Slices

### Slice 1 — Internal recruitment residue (16 endpoints; no prerequisite)

`TalentPool` singular (11 — candidate CRM, folds into the area-6 candidate screens),
`JobVacancy` stage assignments (3), `JobCandidate` interest edit (1), `JobInterview`
external-panellist edit (1). ⚠ `api/talent-pool` singular ≠ succession's `api/talent-pools`.

Standing pre-build checks ran via survey 2026-08-30. Actors: **clean** (all token-derived).
TenantId on create: **clean**. Number generators: **clean** (sequence-based). The defects found:

Backend fixes before UI (all file:line verified):
1. 🔴 `ToTalentPoolDto` (`RecruitmentMappingExtensions.cs:1306-1346`) populates ~21 of ~40
   declared fields — no CV link, city/country, salary expectations, work authorisation,
   middle name. D-09 shape. Fill it (delegate to the full `JobCandidateDto` mapper + overlay).
2. 🔴 `GetTalentPoolFilteredAsync` (`JobCandidateRepositories.cs:90-168`) has **no TenantId
   predicate** — counts and pages cross-tenant, service filters after paging → wrong TotalCount,
   short/empty pages. Same shape in `GetTalentPoolCandidatesAsync` (:54-62; in-memory filtered
   by callers, so correct but loads every tenant). Push tenantId into both queries.
3. 🔴 `IX_VacancyStageAssignment_Vacancy_Stage` unique, **no IsDeleted filter**
   (`AddHRModule.cs:33554`), delete is soft, `GetByVacancyAndStageAsync` filters `!IsDeleted` →
   remove-then-reassign a stage owner = 500. **Eighth face.** Fix = revive-on-upsert
   (the D-21/D-24 idiom: `IgnoreQueryFilters` + re-apply tenant by hand), no migration.
4. 🔴 `AddToTalentPoolDto.SegmentIds` (`:5840`) is **dead** — `AddToTalentPoolRichAsync`
   (`JobCandidateService.cs:288-304`) never reads it. Implement it (reuse membership creation).
5. 🔴 `TalentPoolAnalyticsDto.BySegment` never populated (`JobCandidateService.cs:373-394`).
6. 🔴 Bulk op (`:397-457`): `Results` never appended; `Succeeded++` fires when the switch matched
   no arm (null SegmentId/Status read as success); catch swallows the reason. Fix per-item
   results (+ additive `CandidateId` on the shared item DTO — `ApplicationId` is another
   caller's key, don't rename).
7. `RemoveFromTalentPoolRichAsync` (`:306-320`) never sets `TalentPoolRemovedDate` (dead column);
   the **flat** remove (`:264-286`, wired from the candidates screen) sets no status/date at all —
   align both doors, repoint the UI to the rich pair.
8. `AddCandidateAsync` (`TalentPoolManagementService.cs:128-160`) validates the segment but never
   the candidate's tenant — unvalidated FK write. Same check bulk already does at `:405`.
9. Engagement reads: `CandidateName` never assigned, `RecordedByName` hardcoded null
   (`RecruitmentMappingExtensions.cs:1386-1401`) — the timeline can't say who logged contact.
   `CandidateSegmentMembershipDto` lacks `JobCandidateId`; the add-membership response returns a
   nameless segment (`GetByCandidateAndSegmentAsync` has no Include, `JobCandidateRepositories.cs:268-275`).
10. `UpdateExternalPanelistAsync` (`JobInterviewService.cs:1150-1161`) and
    `RecordExternalPanelistAttendanceAsync` (`:1176`) take `updatedByUserId` and stamp nothing.
11. D-17 shape, unmitigated instances: stage-assignment create/update `AssignedToId` /
    `PipelineStageId` — `Guid.Empty` passes validation and 500s at the FK. Guard naming the field.
12. Wrap the two bare-scalar PATCH bodies (`candidates/{id}/status` takes a raw enum,
    `review-date` a raw DateTime — `TalentPoolController.cs:93,:104`); nothing calls them yet.
13. `MatchToVacancyAsync` (`JobCandidateService.cs:459-483`) is a stub (score 0, constant reason)
    while the reverse direction (`:485-560`) is a real 40/30/20 scorer — give the forward
    direction the same rubric so the vacancy-side match panel isn't fake numbers.

Frontend (fold-in points from survey): new `/hr/recruitment/talent-pool` (list + segments +
analytics; precedent `candidates/page.tsx`), pool/engagement tabs on `candidates/[id]`
(tab list `:130-141`; repoint the old flat toggle at `:54,:107-116`), "Stage owners" tab on
`vacancies/[id]` (tab list `:260-268`; NOT the pipeline page — that's the application board),
interests edit = delete stale comment `CandidateSubResourceTabs.tsx:418` + real `update` at
`:429-430`, external-panelist edit dialog in `InterviewPanelPanel.tsx:242-253` (wire the dark
internal `updatePanelist` too). New `services/hr/talent-pool.service.ts` +
`types/hr/talent-pool.ts` — reuse the never-referenced `TALENT_POOL_STATUSES` stub at
`types/hr/recruitment-pipeline.ts:164-172` (it matches the backend enum exactly). Sidebar child
at `sidebar.tsx:~1142`, `HR.Recruitment.Read`. ⚠ Don't send the filter default `SortBy:"LastName"`
— server sort keys are `fullname|dateadded|lastengaged|reviewdate|experience`.

Harness: new `dev-harness\hr-recruitment\slice-e\` on the B/C/D helper surface (token-cached
`api.mjs` — the two helper surfaces don't cross-import). Fixtures precedent: slice-b (candidates)
+ slice-c (external associates). Must assert: tenant-scoped paging counts, revive-on-upsert for
stage owners, SegmentIds honoured on add, per-item bulk results (including the null-arm refusal),
engagement names populated, both audit stamps moving with a second actor, and the Guid.Empty 400s.

### Slice 2 — Retire the candidate portal (backend + schema)

Removal checklist (all file:line refs verified by survey):
- Delete: `CandidatePortalController.cs`, `CandidatePortalAuthController.cs`,
  `CandidatePortalAuthService.cs` + `ICandidatePortalAuthService.cs`,
  `CandidateJwtService.cs` + `ICandidateJwtService.cs`.
- **Keep but refactor**: `CandidatePortalService.cs` + interface — slice 4 reuses it with the new
  account resolver. (If sequencing makes it cleaner, move the refactor here.)
- DI: remove `HrModuleServiceRegistration.cs:819,820,837,840`
  (**keep** `:821,:822,:838,:841` — consultant-client).
- Policies: delete `"CandidatePortal"` (`ServiceCollectionExtensions.cs:1389-1393`); keep
  `"ConsultantClientPortal"`, `"PublicPortalPolicy"`, `"PublicApplyPolicy"`, `"PublicUploadPolicy"`.
- `PortalAuth.cs`: trim `CandidateUserType` (:32) and the apparently-dead `ExternalUserTypes`
  (:36 — verify no consumer first); everything else stays.
- DTOs (`RecruitmentDTOs.cs`): delete the six auth DTOs (:5423-:5477 range) — keep the profile/
  application/dashboard/offer DTOs (:3124, :3165, :5489+) for slice 4.
- Config: `JwtSettings:PortalSecretKey`/`PortalAudience` STAY (consultant). `CandidatePortal:PortalUrl`
  option STAYS (shared consumers: `ConsultantServices.cs:567`, `JobInterviewService.cs:296`,
  `TemplatedEmailService.cs:148`); renaming the section is a separate change.
- Tooling: remove the two hardcoded verdicts in `scripts/hr-coverage/04_build_ledger.py:56-59`;
  delete `build-candidate-portal.log`.
- Keep: `HREnums.cs:8015` `CandidatePortal = 8` (stored data), `CandidatePortalOptionsTests.cs`.
- **Schema**: FIRST check `CandidatePortalAccounts` row count in the DB (if rows exist, stop and
  report — the email→JobCandidateId links feed pre-provisioning). Then remove the entity
  (`RecruitmentEntities.cs:708-742`) + DbSet (`ApplicationDbContext.HR.cs:265`); user scaffolds
  the drop migration; I edit + list in `FastBuildMigrationMetadata`; snapshot regenerates with
  the scaffold. **Needs a backend rebuild.**

### Slice 3 — Candidate identity on the main scheme

- `Constants.Roles.Candidate = "Candidate"` + add to `IsProtectedSystemRole` (`Constants.cs:67-84`);
  seed in `DatabaseSeedingService.SeedRolesAsync` (`:7788` array) — idempotent on existing DBs.
- **Extend `InternalOnly`** to refuse Candidate as well as ExternalUser (`ServiceCollectionExtensions.cs:1334`).
- New `CandidateAccessMiddleware` (template: `SupplierApplicantAccessMiddleware`, registered
  beside it at `Program.cs:554-556`): candidates allowed ONLY
  `/api/auth`, `/api/tenant`, `/api/user/profile`, `/api/user/change-password`,
  `/api/notifications`, `/api/hubs`, `/api/public`, `/api/candidate`, `/health` — 403 elsewhere.
  Each prefix audited for bare `[Authorize]` before it goes on the list (they are all already
  ExternalUser-reachable except `/api/candidate`, which is new and candidate-gated).
- `CandidateOnly` policy (role-based; the claim-based `SupplierApplicantOnly` shape is the
  fallback if role granting proves awkward).
- Candidate registration: clone of the register flow taking explicit tenant (the job-board
  context), assigning `Candidate`, email-first verification; CAPTCHA kept.
- `JobCandidate ↔ ApplicationUser` link (new nullable `UserId` on `JobCandidate`, adopt-by-email
  on first login/registration mirroring `CandidatePortalAuthService.VerifyEmail`'s adoption) —
  second migration point, same user-scaffolds workflow.
- Frontend: `isCandidateUser` in `lib/auth-routing.ts` + the third arm in
  `getAuthenticatedHomePath`; branch sites: login, dashboard, use-auth, external-portal layout.

### Slice 4 — Candidate API on the main scheme

New `api/candidate/*` controller(s), `[Authorize(Policy="CandidateOnly")]`, reusing
`CandidatePortalService` with the `ApplicationUser → JobCandidate` resolver: dashboard, profile
(save creates+links the JobCandidate on first call), applications (draft+submit, expose the two
phases the old controller collapsed), withdraw, documents/CV/photo through the controlled gate
with the REAL actor id, offers (view/letter/respond — **re-implement the ownership check** before
`RecordPortalCandidateResponseAsync`). Apply + CV upload require the candidate token; the ticket
keeps its tenant+vacancy binding. `api/public` keeps: vacancies list/detail, countries/skills/
qualifications catalogues. `POST api/public/apply`, the anonymous tracker and `api/public/cv-upload`
retire (application state lives in "my applications" behind login).
**Open sub-decision (recommendation: keep):** the tokenised anonymous `api/offer-response`
validate/respond pair — offer emails link to it and it is one of the 31. Keeping it costs one
small public page; candidates who register see the same offer in the portal.

### Slice 5 — Candidate frontend

Public: `/careers` job board + vacancy detail (anonymous; tenant resolved the same way the
register page resolves it). Portal: candidate sections in the external-portal shell — the sidebar
menu becomes role-aware (partner items for ExternalUser, candidate items for Candidate: Dashboard,
Job Board, My Applications + apply flow, Documents/CV, Offers, Profile, Notifications, Support).
Blazor spec pages that collapse into existing surfaces: login/register/verify/forgot/reset (main
auth pages). UI-payload probes precede any TypeScript (the standing area-12/14 lesson).

### Slice 6 — Harness + ledger closure

`dev-harness\hr-recruitment\` extended: full candidate lifecycle (register → verify → browse →
apply with CV ticket → track in portal → offer → respond), forged-actor probes, and the boundary
probes that make the role split real: a Candidate token 403s outside its allowlist (procurement
especially), an ExternalUser token 403s on `/api/candidate`, an internal endpoint refuses both,
HR screens read the applications candidates filed. Then regenerate the ledger (dispositions into
`04_build_ledger.py`), update the survey memories, stage.

## Standing rules that apply throughout

- Run harnesses in Staging with the JWT key (dev exception page hides status codes).
- Stop the running API before asking the user to rebuild; stage, never commit.
- Any new upload category goes in BOTH `ControlledFileUploadCategories` AND
  `SystemCleanScanRequired` (D-11).
- Type-check with a scoped tsconfig against a stashed baseline — full `tsc` crashes on the clean
  tree (D-22 note).
- Money events (if any surface — offers carry salary) go to `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`,
  never an HR-side posting mechanism.
