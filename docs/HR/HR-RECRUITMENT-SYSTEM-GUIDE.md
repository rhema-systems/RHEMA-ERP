# HR Recruitment — System Guide

**Status:** **complete for recruitment, and its gaps are closed.** All 32 screens walked
2026-09-14 → 2026-09-15; all 70 gaps closed 2026-09-15 → 2026-09-16.
Chapter 1 is the shared background; chapters 2–15 are one per screen group; chapter 16 is what is
only visible once they are all read. Start at chapter 16 if you want the conclusions first.

> ## ⚠ READ THIS BEFORE ANY GAP IN THIS DOCUMENT
>
> **The 70 gaps recorded here were closed between 2026-09-15 and 2026-09-16.** Sixty-eight were
> fixed, two were found to have been closed already, and two remain open by decision. Every
> finding below now carries a **CLOSED** line saying what the code does *today*; the diagnosis
> underneath it is kept as the record of what was wrong and why, because the reasoning is what
> stops it recurring — but **it is written in the past tense and must not be read as current
> behaviour.**
>
> The closure programme, its ten lanes, the decisions taken and the two things still open are in
> **`docs/HR/HR-RECRUITMENT-GAP-CLOSURE-PLAN.md`**. Start there if you want to know the state of
> the module rather than its history.
>
> **⚠ One correction you should know before reading any gap about workflow approval.** G-4.1,
> G-10.1 and Appendix C's auto-approve note all asserted that *no HR workflow definition is seeded
> anywhere in the solution*. **That is false, and was false when written** —
> `EnsureHrWorkflowsSeededAsync` seeds 28 published definitions covering every HR entity type, and
> landed twelve days before this walk. Those gaps are still correctly closed (the unseeded state is
> reachable, and the offer's missing segregation-of-duties check was real either way), but the
> severity was overstated. Corrected in place on 2026-09-16; the full account is in
> `docs/HR/HR-WORKFLOW-AUTOAPPROVE-CLOSURE-PLAN.md`.
>
> **Still open, deliberately:**
>
> | | Why it is still open |
> |---|---|
> | **G-7.2's unique email index** | The writer is fixed — `UpdateAsync` refuses a duplicate. The index is not unique, because soft-deleted rows occupy a unique index and a tenant carrying historical duplicates would fail the migration. Needs a data check on the live tenant first; the query is in the plan. |
> | **The auto-approve mechanism outside recruitment** | G-4.1 and G-10.1 turned out to be two instances of one HR-wide defect spanning ~22 services. Recruitment's are fixed, along with `StaffMovement`, `PerformanceImprovementPlan` and `EmploymentActionProposal`. Roughly seventeen remain — `EmployeeSalaryChangeRequest` and `Separation` first on impact. |

**Audience:** two, layered. Each screen chapter opens with a plain-language section any HR user can read, then a technical section mapping every control to its endpoint, service, entity and table.

**Method:** written from the source — page component, service layer, controller, entity, EF model snapshot. It describes what the code is built to do. Where the running system differs, that difference is recorded as a gap rather than smoothed over.

---

## How to read this document

| If you want to… | Read |
|---|---|
| Understand the shape of the module before any screen | Chapter 1 |
| Know what a screen is for and how to use it | the **What it is** and **On the page** sections of its chapter |
| Trace a field to a database column | the **Behind the page** table of its chapter |
| Know who can see or do what | the **Who can use it** section of its chapter |
| Know what *used to* be wrong, and what fixed it | the **Known gaps** block in each chapter, and Appendix C — every finding is closed and annotated |
| Know what is wrong **now** | `docs/HR/HR-RECRUITMENT-GAP-CLOSURE-PLAN.md`. Two things remain open, both by decision; this guide's gap blocks are history, not a to-do list |

Every chapter is titled by its route, exactly as typed in the browser.

---

## Conventions used throughout

**Routes.** A route such as `/hr/recruitment/requisitions` is the file
`frontend/src/app/hr/recruitment/requisitions/page.tsx`. A segment in square brackets is a
parameter — `/requisitions/[id]` is one requisition, the `id` being its `Guid` primary key.

**Table names.** There is no `HR_` prefix and no `ToTable()` mapping anywhere in the solution.
The table name is the name of the `DbSet<>` property in `ApplicationDbContext.HR.cs`. So the
`JobVacancy` entity lives in a table called `JobVacancies`, `StaffRequisition` in
`StaffRequisitions`. Where this guide names a table, that is the literal name you would query.

**Columns every recruitment table has.** Every recruitment entity derives from `TenantEntity`,
so every one of these tables carries the same housekeeping columns, and the screen chapters do
not repeat them:

| Column | Meaning |
|---|---|
| `Id` | `Guid` primary key |
| `TenantId` | the company the row belongs to; every query is filtered by it |
| `CreatedAt`, `CreatedBy`, `CreatedById` | when and by whom the row was made |
| `UpdatedAt`, `UpdatedBy`, `LastModifiedById` | last change |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | soft delete — rows are hidden, never removed |

**Soft delete matters.** Because `IsDeleted` hides rather than removes, a deleted row still
occupies any unique index it sits in. This is why, for example, a deleted candidate's email can
block a new candidate with the same address.

**`RowVersion`.** Tables whose status is changed by more than one person — requisitions,
vacancies, offers, hire records — carry a `RowVersion` concurrency token. Two approvers acting at
once means the second gets a conflict rather than silently overwriting the first.

---

## 1. How recruitment hangs together

### 1.1 The spine, in business terms

Recruitment in this system is one chain. Each link is a separate record with its own screen, its
own status vocabulary and its own approval. Nothing skips a link.

```
  A seat falls empty                 PositionVacancy
          │                          "post X lost its occupant"
          ▼
  Someone asks to fill it            StaffRequisition
          │                          "please authorise a hire for post X"
          ▼   (approved)
  HR opens a recruitment             JobVacancy
          │                          "we are hiring for post X"
          ▼
  It is advertised                   JobPosting
          │                          "here is the advert, on these channels"
          ▼
  People apply                       JobApplication ──uses──▶ JobCandidate
          │                          "person P applied to vacancy V"   "person P"
          ▼
  They are sifted                    shortlisting (scores + reviews on the application)
          │
          ▼
  They are interviewed               JobInterview ▸ JobInterviewee ▸ scores
          │
          ▼
  One is offered                     JobOffer
          │
          ▼
  They are checked                   PreEmploymentCheck ▸ items
          │
          ▼
  They are hired                     JobHireRecord
          │
          ▼
  They become staff                  Employee   ← the rest of HR starts here
```

Two things are worth fixing in mind now, because they are the two most common
misunderstandings of this module.

**A `PositionVacancy` is not a `JobVacancy`.** They sound identical and are not. A
`PositionVacancy` is the upstream *fact* that a post fell empty — it is written automatically the
moment an employee is terminated, retires, or moves off a position, whether or not anybody
intends to recruit. A `JobVacancy` is an *approved recruitment opening being advertised*, and it
cannot exist without an approved requisition behind it. Many position vacancies never become job
vacancies; they are closed, frozen, or classified as no shortfall.

**A `JobCandidate` is not a `JobApplication`.** The candidate is the person — one record, reused
across every job they ever apply for, carrying their qualifications, work history, referees,
skills and documents. The application is one person's pursuit of one vacancy. A candidate with
three applications is one `JobCandidates` row and three `JobApplications` rows.

### 1.2 The spine, in tables

| # | Step | Entity | Table | Points back to |
|---|---|---|---|---|
| 1 | Seat falls empty | `PositionVacancy` | `PositionVacancies` | `EmployeePosition`, the departing `Employee` |
| 2 | Request to fill | `StaffRequisition` | `StaffRequisitions` | `EmployeePosition`, `JobDescription`, requester `Employee` |
| 3 | Recruitment opening | `JobVacancy` | `JobVacancies` | `StaffRequisition` (required), `EmployeePosition` |
| 4 | Advert | `JobPosting` | `JobPostings` | `JobVacancy` |
| 5a | The person | `JobCandidate` | `JobCandidates` | — (stands alone) |
| 5b | The pursuit | `JobApplication` | `JobApplications` | `JobVacancy`, `JobCandidate`, optionally `JobPosting` |
| 6 | Interview | `JobInterview` | `JobInterviews` | `JobVacancy`; interviewees link to applications |
| 7 | Offer | `JobOffer` | `JobOffers` | the application |
| 8 | Checks | `PreEmploymentCheck` | `PreEmploymentChecks` | the **offer**, not the hire |
| 9 | Hire | `JobHireRecord` | `JobHireRecords` | `JobApplication`, `JobOffer`, and eventually `Employee` |

Note step 8: pre-employment checks hang off the **offer**, deliberately, so that they run during
the conditional-offer window — before a hire record exists at all.

### 1.3 Size of the module

The recruitment domain is 69 database tables. They are declared in three entity files:

| File | Entities | Covers |
|---|---|---|
| `PositionVacancyEntities.cs` | 1 | the empty-seat register |
| `StaffRequisitionEntities.cs` | 5 | requisitions, their costs, attachments, comments, history |
| `RecruitmentEntities.cs` | 63 | everything from job vacancy to hire — **plus onboarding and probation**, which live in the same file but are their own areas and are documented separately |

A further five types in `RecruitmentEntities.cs` (`ApplicationCandidateSnapshot` and its
`Snapshot*` companions) are **not tables**. They are serialised to JSON inside a column on
`JobApplications`, freezing the candidate's details as they stood when they applied, so that
later edits to the candidate's profile do not rewrite history on an old application.

The thirty-two screens are grouped as follows, and this guide follows that order:

| Group | Routes |
|---|---|
| Overview | `/hr/recruitment`, `/dashboard`, `/analytics` |
| Establishment | `/establishment` |
| Requisitions | list, `new`, `[id]`, `[id]/edit` |
| Vacancies | list, `new`, `[id]`, `[id]/pipeline`, `[id]/screening` |
| Postings | `/postings` |
| Candidates | list, `new`, `[id]`, `[id]/edit` |
| Applications | list, `[id]` |
| Interviews | list, `new`, `[id]`, `[id]/score/[intervieweeId]` |
| Offers | list, `new`, `[id]`, `[id]/edit` |
| Pre-employment checks | `/pre-employment-checks` |
| Hires | list, `[id]` |
| Talent pool | `/talent-pool` |

### 1.4 The status vocabularies

Each link in the chain has its own status enum. These are stored as integers, so the numbers
below are what you would see querying the database directly. Learning these four is most of
learning the module.

**`StaffRequisitions.Status`** — `StaffRequisitionStatus`

| # | Status | Meaning |
|---|---|---|
| 1 | Draft | being written, not yet visible to approvers |
| 2 | Submitted | sent for approval |
| 3 | Under Review | an approver has it |
| 4 | Approved | may now become a job vacancy |
| 5 | Rejected | refused; the requester may revise |
| 6 | On Hold | paused |
| 7 | Cancelled | withdrawn |
| 8 | Partially Fulfilled | some but not all of the requested positions filled |
| 9 | Fulfilled | all positions filled |

Note that a requisition can ask for several positions at once — `NumberOfPositions` against
`PositionsFilled` — which is why there are two fulfilment states rather than one.

**`JobVacancies.VacancyStatus`** — `JobVacancyStatus`

| # | Status | # | Status |
|---|---|---|---|
| 1 | Draft | 7 | Shortlisting |
| 2 | Pending Approval | 8 | Interviewing |
| 3 | Approved | 9 | Offer Stage |
| 4 | Rejected | 10 | Filled |
| 5 | Published | 11 | Cancelled |
| 6 | Closed for Applications | 12 | On Hold |

**`JobApplications.Status`** — `ApplicationStatus`. This is the longest vocabulary in the module,
because it tracks a person all the way through:

| # | Status | # | Status |
|---|---|---|---|
| 0 | Draft | 8 | Pre-Employment Check |
| 1 | New | 9 | Offer Extended |
| 2 | Submitted | 10 | Offer Accepted |
| 3 | Under Review | 11 | Offer Declined |
| 4 | Shortlisted | 12 | Rejected |
| 5 | Interview Scheduled | 13 | Withdrawn |
| 6 | Interview Completed | 14 | Hired |
| 7 | Assessment Pending | 15 | Waitlisted |

**`JobOffers.Status`** — `JobOfferStatus`

| # | Status | # | Status |
|---|---|---|---|
| 1 | Draft | 8 | Withdrawn |
| 2 | Pending Approval | 9 | Expired |
| 3 | Approved | 10 | On Hold |
| 4 | Sent | 11 | Conditionally Accepted |
| 5 | Negotiating | 12 | Checks Cleared |
| 6 | Accepted | 13 | Rejected (by an approver) |
| 7 | Declined | **14** | **Superseded** *(added 2026-09-16)* |

States 11 and 12 are the pre-employment-check window: the candidate has accepted, checks are
running, and only when every blocking check passes does the offer reach *Checks Cleared* and a
hire record become possible.

> **Two of these changed meaning in the closure pass.**
>
> **9 — Expired** was, at the time of the walk, *never written by anything in the solution*
> (G-2.4). The nightly sweep writes it now, so an offer that lapses unanswered leaves `Sent`
> instead of sitting there for ever.
>
> **14 — Superseded** is new (G-10.2). A revision used to leave its predecessor in `Sent` or
> `Negotiating` — a live status — so superseded versions went on being listed, chased and counted.
> Deliberately **not** `Withdrawn`: withdrawing is the organisation taking live terms back, being
> superseded is what happens to a version when better terms replace it, and collapsing the two
> would answer *"how many offers did we withdraw?"* wrongly.
>
> ⚠ Both are integers in the database and members were **appended, never renumbered** — the repo's
> standing rule for these enums, since existing rows would otherwise change meaning silently.

**`PositionVacancies.Status`** — `PositionVacancyStatus`: Anticipated (1), Open (2), Under Review
(3), Requisition Raised (4), Filled (5), Closed (6), Frozen (7).

> **Anticipated (1) is written now, and was not at the time of the walk** (G-3.6). A separation
> submitted with a future effective date opens an anticipated row; the nightly sweep promotes it to
> Open on the day, and a departure applied earlier promotes it too. ⚠ This is why the establishment
> screen's "open seats" and the analytics screen's "seats standing empty" will now genuinely
> differ — the first counts Anticipated, the second does not, because they answer different
> questions (G-14.3). Before this, both were the same number because nothing wrote the status.

### 1.5 Establishment: the idea that governs requisitions

A position carries an `ExpectedHeadcount` — how many people that post is *established* for. Every
position vacancy is classified against it at the moment it is detected, and the classification is
stored on the row along with a snapshot of the numbers that produced it:

| `Classification` | Meaning |
|---|---|
| Within Establishment (1) | active headcount is now below expected — a genuine, fillable shortfall |
| No Shortfall (2) | the post is still at full headcount despite the departure — flagged for HR, not auto-fillable |
| Over Establishment (3) | the post is above its expected headcount — for HR to review |

A departure is **always** logged and then classified. It is never silently dropped because the
position still looks full. That rule is the reason `PositionVacancies` exists as a register rather
than as a filtered view of departures.

The same idea reaches forward into requisitions. When a requisition is submitted, the
establishment position as it then stood is frozen onto the requisition itself —
`EstablishmentSnapshotOn`, `EstablishmentSnapshotExpected`, `EstablishmentSnapshotFilled`,
`EstablishmentSnapshotIsEstablished` — so the approver decides against the figures they were
shown, and the detail screen can flag it when the live figures have since drifted.

A requisition may also draw down an approved manpower budget line (`ManpowerBudgetLineId`). When
it does, `IsBudgeted` and `BudgetCode` are **derived by the service, never typed** — they were
formerly a self-declared checkbox and a free-text box nothing read. A requisition raised without a
budget line, or for a post with no establishment gap, needs an `ExceptionJustification` at submit;
under the strictest enforcement setting it is refused outright.

---

## Chapters

---

## 2. `/hr/recruitment` — the module front door

**File:** `frontend/src/app/hr/recruitment/page.tsx` (199 lines)
**Walked:** 2026-09-14

### 2.1 What it is

The front door to recruitment. It does two jobs and no others: it shows five counters that say
whether anything needs attention, and it offers a card for each screen in the module.

It holds no records of its own. Nothing on this page can be edited, approved or created — every
action lives one click away. If you are looking for a register, this is the index to it.

The card order is deliberate and is worth reading as a statement of how the module thinks: it
follows the order a hire actually happens in, not alphabetical order and not a grouping by record
type. Establishment comes first because a gap in the establishment is what starts everything;
applications come near the end because they are what the earlier steps produce.

### 2.2 Who can use it

There are **two independent gates**, and they do not agree with each other. This matters, so it is
worth being precise.

**Gate 1 — the page's own check.** The page asks `hasAnyRole(['SuperAdmin', 'HR'])`. If you hold
neither role, the five counters are not rendered at all and you see only the two self-service
cards. This is a client-side check on the roles in your token: it decides what is *drawn*.

**Gate 2 — the API's check.** Every one of the four endpoints behind the counters requires the
policy `HR.Policy.RecruitmentRead`, which is satisfied by any one of the permissions
`HR.Recruitment.Read`, `HR.Recruitment.Write` or `HR.Recruitment.Admin`. Each controller
additionally sits behind `InternalOnly`. This decides what is *served*.

`InternalOnly` is a **blocklist, not an allowlist**: it admits any authenticated user who is *not*
in `ExternalUser`, `Candidate` or `ConsultantClient`. It is there to keep members of the public —
a careers-site signup, an invited client — out of internal surfaces. It grants nothing on its own.

The roles that actually hold recruitment permissions are `SuperAdmin`, `TenantAdmin`, `Admin`,
`HR` and `HR User`. The page only lets `SuperAdmin` and `HR` through. See gap G-2.3.

### 2.3 On the page

**Header** — title "Recruitment", the line *From an establishment gap to an advert on a job
board*, and a back link to `/hr`.

**Five counters** (rendered only for `SuperAdmin`/`HR`). They lay out four-across on a wide
screen, so the fifth wraps to a second row.

| # | Tile | Shows | Hint line |
|---|---|---|---|
| 1 | Open establishment gaps | posts standing empty | — |
| 2 | Awaiting approval | requisitions sitting with an approver | "Requisitions with an approver" |
| 3 | Approved requisitions | requisitions cleared to become vacancies | — |
| 4 | Live adverts | postings currently published | — |
| 5 | Offers expiring soon | offers about to lapse | "Within 7 days" |

Tile 5 turns **amber** when its count is above zero — it is the only tile on the page that changes
colour, because it is the only one that means a deadline is running against you.

A tile shows an em dash (—) rather than a number while its query is in flight, **and also if that
query fails**. See gap G-2.1.

**Fourteen navigation cards.** Twelve are the recruitment desk, in chain order; two are
self-service and are shown to everyone.

| # | Card | Goes to | What it is |
|---|---|---|---|
| 1 | Dashboard | `/hr/recruitment/dashboard` | pipeline shape, SLA breaches, what starts and expires soon |
| 2 | Establishment | `/hr/recruitment/establishment` | headcount against establishment, and the resulting gaps |
| 3 | Requisitions | `/hr/recruitment/requisitions` | requests for headcount, draft → approval → fulfilment |
| 4 | Vacancies | `/hr/recruitment/vacancies` | approved requisitions turned into advertised roles |
| 5 | Live adverts | `/hr/recruitment/postings` | every posting open for applications, all channels |
| 6 | Candidates | `/hr/recruitment/candidates` | the people behind applications, plus the talent pool |
| 7 | Applications | `/hr/recruitment/applications` | every application across all vacancies |
| 8 | Interviews | `/hr/recruitment/interviews` | sessions, panels, question plans, scorecards |
| 9 | Offers | `/hr/recruitment/offers` | terms raised on an application through to the response |
| 10 | Hires | `/hr/recruitment/hires` | the handover from recruitment to employment |
| 11 | Pre-employment checks | `/hr/recruitment/pre-employment-checks` | medical, police, background and reference checks |
| 12 | Analytics | `/hr/recruitment/analytics` | time to fill, cost per hire, funnel, source, recruiter load |
| 13 | Internal job board | `/me/jobs` | roles *you* can apply for, and your own applications |
| 14 | My panel | `/me/panel` | interviews you sit on, and the scorecards you owe |

Cards 13 and 14 leave the HR desk entirely and land in the employee portal. They are linked from
here because a recruiter is also an employee, and this is the page they are already on. The desk
keeps no copy of either screen — there is one internal job board, and it is the employee's.

**Two screens are deliberately absent from this index:** the pipeline board and screening. Both
are scoped to a single vacancy and are reached from that vacancy
(`/vacancies/[id]/pipeline`, `/vacancies/[id]/screening`), never from a register of their own.
Looking for them here and not finding them is the intended outcome, not a gap.

### 2.4 Behind the page

Four independent queries, each gated on `isHr` so a non-HR viewer fires none of them. All four are
read-only; the page issues no writes at all.

| Tile | Frontend call | Endpoint | Service method |
|---|---|---|---|
| Open establishment gaps | `positionVacancyService.getStats()` | `GET /api/position-vacancies/stats` | `PositionVacancyRepository.GetStatsAsync` |
| Awaiting approval *and* Approved requisitions | `staffRequisitionService.getStatusSummary()` | `GET /api/StaffRequisitions/summary` | `StaffRequisitionService.GetStatusSummaryAsync` |
| Live adverts | `jobPostingService.getActive()` | `GET /api/job-postings/active` | `JobPostingPipelineService.GetActivePostingsAsync` |
| Offers expiring soon | `jobOfferService.getExpiring(7)` | `GET /api/job-offers/expiring?daysAhead=7` | `JobOfferHireService.GetExpiringOffersAsync` |

Note that tiles 2 and 3 are one request, not two — the summary endpoint returns every status count
at once and the page adds `submitted + underReview` itself for tile 2.

**What each number actually counts.** This is where a label and its query can drift apart, so each
is given exactly.

**Tile 1 — Open establishment gaps** → `PositionVacancies`. Counts rows whose `Status` is one of
Anticipated (1), Open (2), Under Review (3) or Requisition Raised (4). Filled, Closed and Frozen
are excluded. Note that a gap with a requisition already raised against it **still counts here** —
the tile is "gaps not yet closed", not "gaps nobody has acted on". The same endpoint also returns
`anticipated`, `underReview`, `requisitionRaised`, `withinEstablishment`, `noShortfallOrOver`,
`totalPositions` and `positionsWithVacancy`; this page uses only `totalOpen`, and the other seven
figures are shown on the Establishment screen.

**Tiles 2 and 3 — requisitions** → `StaffRequisitions`, grouped by `Status`. Tile 2 is
Submitted (2) + Under Review (3); tile 3 is Approved (4). Draft requisitions are counted by the
endpoint but shown on neither tile, which is correct — a draft is not waiting on anyone.

**Tile 4 — Live adverts** → `JobPostings` where `IsActive` is true **and** `Status` is Published.
The expiry date is not consulted. See gap G-2.2.

**Tile 5 — Offers expiring soon** → `JobOffers` where `Status` is Sent (4), `ExpiryDate` is not
null, and `ExpiryDate <= now + 7 days`. There is no lower bound on that comparison. See gap G-2.4,
which is the most consequential finding on this screen.

**Tenant isolation.** Every one of these tables carries `TenantId`, and the context applies a
single combined query filter — `!IsDeleted && (no current tenant || TenantId == current)` — to
every entity deriving from `TenantEntity`. Soft delete and tenant scoping are therefore one filter,
deliberately: EF Core allows only one query filter per entity, and applying them separately would
have let the tenant filter overwrite the soft-delete filter and resurrect deleted rows.

### 2.5 Known gaps

> **✅ All five closed, 2026-09-15 → 16.** Read the findings below as history.
>
> | | Now |
> |---|---|
> | G-2.1 | `…` while loading, `—` for no figure, and a banner when a query fails. |
> | G-2.2 | The "live adverts" query respects `ExpiryDate`, and the nightly sweep expires them too. |
> | G-2.3 | The page gates on `HR.Recruitment.*`, like the API. Four other pages had the same shape and were swept up with it. |
> | G-2.4 | The sweep writes `Expired`; the query is bounded at both ends and filters `IsLatestVersion`. |
> | G-2.5 | All five tiles link to the queue they name. |

**G-2.1 — a failed query is indistinguishable from a pending one.** Every tile renders
`data?.field ?? '—'`. There is no error branch and no loading state, so an endpoint returning 500,
a token that has expired, and a request still in flight all display the same em dash. A user cannot
tell "we are still counting" from "we could not count". *Impact: low — misleading rather than
wrong.*

**G-2.2 — "Live adverts" counts expired postings.** The query filters on `IsActive && Status ==
Published` and never looks at `ExpiryDate`. A posting whose closing date has passed still counts as
live until somebody manually unpublishes it. The repository has a `GetExpiredActivePostingsAsync`
method that finds exactly these rows (`IsActive && ExpiryDate < now`), which is direct evidence
that the condition is known to occur — but nothing on this page calls it. *Impact: medium — the
tile overstates how many roles are genuinely open for applications.*

**Confirmed in chapter 6.** The adverts screen exposes that very query as its second view, *"Past
expiry but still live"*, with an amber warning. So the distinction is understood and used
elsewhere in the module; this tile simply does not make it. Chapter 6 also establishes the root
cause: **nothing expires an advert on its closing date** (G-6.2), so the overdue set is not an
edge case but the normal resting state of any advert whose deadline has passed.

**G-2.3 — the page hides the module from three roles the API would serve.** The page draws the HR
cards and counters only for `SuperAdmin` and `HR`. The permission map grants recruitment
permissions to `SuperAdmin`, `TenantAdmin`, `Admin`, `HR` and `HR User`. A user holding
`TenantAdmin`, `Admin` or `HR User` is therefore served by every endpoint but shown nothing except
the two self-service cards — they can reach `/hr/recruitment/requisitions` by typing the URL and
it will work. The mismatch is that this page gates on **roles** while the API gates on
**permissions**. *Impact: medium — the legacy `HR User` role is the likely real-world case.*

**G-2.4 — "Offers expiring soon" never stops growing, because no offer is ever marked Expired.**
Two facts combine here:

1. The query is `Status == Sent && ExpiryDate <= now + 7 days`, with **no lower bound**. An offer
   whose expiry passed six months ago satisfies it just as well as one expiring tomorrow.
2. `JobOfferStatus.Expired` (9) is **never written anywhere in the solution**. Every assignment to
   `OfferStatus` across the codebase writes Draft, PendingApproval, Approved, Sent, Rejected,
   Withdrawn, ConditionallyAccepted or ChecksCleared; the candidate-response path writes Accepted,
   Negotiating or Declined from an allow-list that does not include Expired. No background service
   or hosted job touches offers at all.

So an offer that lapses stays in Sent for ever and accumulates in this tile permanently. The tile
is labelled "Within 7 days" and hinted the same, but it is in practice "every offer ever sent that
was not answered". The amber tone makes this worse, not better: it will be amber permanently once
the first offer lapses. The same stale Sent rows also feed the analytics screen's "Pending"
count, while its "Expired" count can only ever report zero. *Impact: high — the number is wrong,
it is wrong in a direction that grows without bound, and it is one of only five numbers on the
module's front page.*

**G-2.5 — the counters are not clickable.** `MetricTiles` supports an `href` per tile, and its
own documentation says why: *"A counter that means somebody has to do something is only half a
screen without it."* This page passes no `href` on any of the five. Tiles 2 and 5 in particular
name a queue someone must work, and neither takes you to it. *Impact: low.*

---

## 3. `/hr/recruitment/establishment` — establishment and the gaps that follow

**File:** `frontend/src/app/hr/recruitment/establishment/page.tsx` (560 lines)
**Walked:** 2026-09-14

### 3.1 What it is

The front of the funnel. It answers one question in two ways: **where is headcount short of what
was authorised?**

The two ways are the two tabs, and they are genuinely different things, not two views of one thing:

- **Position establishment** is the arithmetic — every position, its authorised headcount, how many
  people are actually in it, and the difference. It is computed live, on every load, from the
  positions and employee tables. Nothing is stored.
- **Vacancies** is the register — a row per gap that someone has *logged*, with a status, a reason,
  a note and a life of its own. These are `PositionVacancies` rows, and they persist.

The register does not maintain itself from the arithmetic. Something has to write the rows, and
that something is the **Reconcile** button. This is the single most important fact about this
screen, and § 3.6 is about what follows from it.

### 3.2 The concept you must have first: *established* vs *has a headcount number*

Every position carries an `ExpectedHeadcount` column. **That number means nothing on its own.**
The column defaults to 1, and most positions have never had it set — the DTO records that 186 of
231 live positions carried the untouched default as of 2026-09-10, and the admin screen's own
notes record 132 of 146 at an earlier count, with one position holding over a thousand people.

What makes the number an authorisation is a separate field: `EmployeePosition.EstablishmentApprovedOn`.
If it is null, the post is **not established** and no gap can be stated for it, whatever the
headcount column reads. If it is set, the post is established and the arithmetic means something.

This distinction is enforced consistently and it drives what you see:

| Situation | Established column | Gap column | Row shading |
|---|---|---|---|
| `EstablishmentApprovedOn` is null | "not established" (grey) | "—" | none |
| Established, filled < expected | the number | the shortfall | none |
| Established, filled = expected | the number | 0 | none |
| Established, filled > expected | the number | 0 | **amber**, filled count bold |

Over-strength posts are shaded amber because they are the ones actively refusing recruitment and
staff movements elsewhere in HR.

In the DTO this is four derived properties, all keyed off `IsEstablished`:

```
GapKnown            = IsEstablished
VacantCount         = IsEstablished ? max(0, ExpectedHeadcount - FilledCount) : 0
IsFullyFilled       = IsEstablished && VacantCount == 0
IsOverEstablishment = IsEstablished && FilledCount > ExpectedHeadcount
```

**Where establishment comes from.** Normally an approved manpower budget stamps it — the
`Source` column then reads *Budget <number>*, resolved from `ManpowerBudget.BudgetNumber` via
`EmployeePosition.EstablishmentSourceBudgetId`. The exception path is the **Manual Establishment**
button in the header, which goes to `/administration/hr/establishment` and lets HR establish a post
no budget covers; those rows read *Set by HR*.

### 3.3 Who can use it

Unlike the landing page, the **three read queries are not gated on role** — they fire for any
viewer. What `isHr` (`SuperAdmin` or `HR`) controls here is the header buttons and the per-row
action column. A viewer without recruitment read permission will get 403s from the API and see
empty tables rather than a refusal message.

The write endpoints split across **two different policies**, and this matters:

| Action | Endpoint | Policy required |
|---|---|---|
| Set status by hand | `PATCH /api/position-vacancies/{id}/status` | `RecruitmentWrite` |
| Save notes | `PUT /api/position-vacancies/{id}/notes` | `RecruitmentWrite` |
| Close a gap | `POST /api/position-vacancies/{id}/close` | `RecruitmentWrite` |
| Raise a requisition | `POST /api/position-vacancies/{id}/raise-requisition` | **none — see G-3.3** |
| Reconcile | `POST /api/position-vacancies/reconcile` | **`RecruitmentAdmin` — see G-3.1** |

### 3.4 On the page

**Header** — title, description, back to `/hr/recruitment`, and three buttons (HR only):

| Button | Does |
|---|---|
| Manual Establishment | navigates to `/administration/hr/establishment` — the Admin-tier exception path for establishing a post no budget covers |
| Reconcile | recomputes the register from live headcount; toasts *"N positions scanned — N opened, N closed"* |
| Plan a budget | opens a dialog that creates a Draft manpower budget from the establishment |

**Four counters**, all from the same `/stats` call met in chapter 2: Open gaps, Anticipated,
Requisition raised ("Already asked for"), and Positions affected rendered as *"N of M"*.

**Tab 1 — Vacancies** (the register). One checkbox, *Include filled and closed*, which sets
`includeClosed` on the query. The table:

| Column | Source |
|---|---|
| Position | `PositionVacancy.Position.Title` |
| Unit | `PositionVacancy.OrganizationUnit.Name` (denormalised onto the row at detection) |
| Reason | `PositionVacancy.Reason` (`VacancyReason`) |
| Vacated by | `PositionVacancy.VacatedByEmployee.FullName` |
| Since | `PositionVacancy.VacatedDate` |
| Classification | `PositionVacancy.Classification` |
| Status | `PositionVacancy.Status`, as a badge |

Then four row actions, HR only. The first is conditional:

- **View requisition** if `StaffRequisitionId` is set — goes to that requisition.
- **Raise** otherwise — opens a confirmation, then creates a draft requisition (§ 3.5).
- **💬 Annotate** — opens the notes dialog. Note the implementation detail: it does **not** seed
  the textarea from the row. The list read carries no notes, so it fetches the vacancy by id
  first; seeding from the row would open empty and save a blank over whatever was written.
- **🔄 Set the status by hand** — the override dialog (§ 3.5).
- **❌ Close this gap** — requires a reason, which is enforced on both sides.

**Tab 2 — Position establishment** (the arithmetic). A unit picker (`allowNone: "Every unit"`)
and a checkbox *Only positions below establishment*, on by default. The table is Position (with
its code), Unit, Established, Filled, Gap, Source, as described in § 3.2.

Note what *Only positions below establishment* actually filters on: `VacantCount > 0 ||
OpenVacancyId != null`. So a post that is at full strength but still has an open vacancy row
logged against it **stays visible** — deliberately, so the inconsistency is not hidden.

### 3.5 The five writes, in detail

**Reconcile** — `POST /reconcile`, policy `RecruitmentAdmin`. Walks every active position in the
tenant and, for each:

- opens a vacancy if `IsEstablished && VacantCount > 0 && no open vacancy exists`;
- closes an existing open vacancy if `!IsEstablished || VacantCount == 0`, setting status to
  **Filled** when the post is established (back at strength) or **Closed** when it is not
  (no authorised headcount, so no gap can be stated) — each with a `ClosedReason` saying which;
- **skips any vacancy at `RequisitionRaised`** whatever the establishment now says, because that
  is somebody's live work and reconcile does not close it under them.

The `IsEstablished` guard in the first branch is load-bearing. Before it was added, every
unestablished post with nobody in it produced a vacancy — 38 phantom gaps on the live tenant, none
of them on an established post.

A row reconcile opens is stamped: `Reason = Other`, `VacatedDate = now`, `IsAnticipated = false`,
`Status = Open`, `Classification = WithinEstablishment`, `CreatedBy = "reconcile"`, and a note
*"Opened by reconcile — position headcount is below establishment."* See G-3.2 for what that
means for the grid.

**Raise a requisition** — `POST /{id}/raise-requisition`. Refuses if the vacancy is Filled or
Closed, or if `StaffRequisitionId` is already set. Otherwise it builds a `CreateStaffRequisitionDto`
carrying across:

| Requisition field | Taken from |
|---|---|
| `PositionId` | the vacancy's position |
| `OrganizationUnitId` | the vacancy's unit, falling back to the position's |
| `Type` | always `Replacement` |
| `RequisitionTitle` | *"Replacement — {position title}"* unless overridden |
| `ReplacementForEmployeeId` | `VacatedByEmployeeId` |
| `ReplacementReason` | `VacancyReason` mapped to `StaffReplacementReason` |
| `EmployeeDepartureDate` | `VacatedDate` |
| `DesiredStartDate` | today + 30 days unless overridden |
| `BusinessJustification` | generated: *"Replacement for {who} ({reason}), who vacated {role} on {date}."* |
| `AllowInternalCandidates` / `AllowExternalCandidates` | both true |

It then sets `StaffRequisitionId` on the vacancy, moves it to `RequisitionRaised`, and the page
navigates to the new draft requisition. The dialog says a draft is created and can be edited
before submitting — it is, and the mapping above is what you will find pre-filled there.

The payload the page sends is `{}` — every override (`requisitionTitle`, `numberOfPositions`,
`priority`, `desiredStartDate`, `businessJustification`) is supported by the endpoint but the
screen exposes none of them, which is a reasonable choice given the draft opens immediately.

**Set status by hand** — `PATCH /{id}/status`. The id goes in the **body as well as the route**
and a mismatch is refused with a bare `"ID mismatch."`. The service refuses the change outright if
the vacancy is already Filled or Closed, and refuses the *target* statuses `RequisitionRaised` and
`Filled` as system-driven. A supplied note is **appended** with a UTC timestamp line rather than
replacing what is there.

**Save notes** — `PUT /{id}/notes`. The whole body is `{ notes }`; there is no DTO behind it, just
a nested `UpdateNotesRequest` class on the controller. Unlike the status path, this **replaces**
the notes outright.

**Close** — `POST /{id}/close`. Id in body and route again. `Reason` is `[Required]` with a 500
character cap and is enforced server-side (it is a string, so the attribute actually bites). Sets
status Closed, `ClosedDate`, `ClosedReason`.

Both mutations return the vacancy with a freshly counted headcount rather than a bare mapping — an
earlier version called `ToDto()` bare, whose `currentActiveHeadcount` defaults to 0, so changing a
status made the headcount on screen drop to zero until the next read put it back.

### 3.6 Known gaps

> **✅ All ten closed, 2026-09-15 → 16.** This was the heaviest block in the module, and the two
> structural ones are the reason the register now populates itself. Read the findings as history.
>
> | | Now |
> |---|---|
> | G-3.1 | `AdministerRecruitment` is in `HrStaffGrants`, so HR can reconcile — and delete its own drafts (G-4.8). |
> | G-3.2 | `PositionVacancyLog.LogDepartureAsync`, called from the termination, separation-completion and movement paths. **No interceptor** — explicit call sites, because only a call site knows *why* somebody left. |
> | G-3.3 | `raise-requisition` gated on `RecruitmentWrite`. The ungated *create* endpoint stays ungated, deliberately. |
> | G-3.4 | Departure-logged rows carry the real reason, employee and date. Reconcile rows leave them **honestly empty** and say so — reconcile cannot know, and a later departure upgrades the row. |
> | G-3.5 | `Classification` computed three ways; an unestablished post is `NoShortfall`, not a shortfall. |
> | G-3.6 | A separation with a future effective date opens an `Anticipated` row; the sweep promotes it on the day. |
> | G-3.7 | The dropdown offers the four statuses the service accepts. |
> | G-3.8 | The override path requires a reason for `Closed` and writes it to `ClosedReason`. |
> | G-3.9 | On `HrServingEmployees.Predicate`, with G-4.4. |
> | G-3.10 | Permission-gated, with Reconcile gated on `Admin` so it is not drawn for someone it will refuse. |

This screen carries the heaviest gap list in the module so far, and two of them are structural.

**G-3.1 — the HR role cannot press Reconcile, and Reconcile is the only thing that creates
vacancies.** Two facts:

1. `POST /reconcile` requires `HR.Policy.RecruitmentAdmin`, satisfied only by the permission
   `HR.Recruitment.Admin`.
2. `HR.Recruitment.Admin` appears in **no role's grant list**. The `HR` role's grants are
   `ViewRecruitment` and `MaintainRecruitment` only. The permission is held solely by roles
   granted everything — `SuperAdmin`, `TenantAdmin` and `Admin`.

But the button is drawn for `hasAnyRole(['SuperAdmin','HR'])`. So an HR user sees the button,
presses it, and gets a 403 surfaced as *"Could not reconcile"*. Combine this with G-3.2's finding
that reconcile is the **only** code path that ever creates a `PositionVacancy` row, and the
consequence is that the HR function cannot populate its own gap register. *Impact: high.*

**G-3.2 — `PositionVacancyInterceptor` does not exist; nothing logs a vacancy automatically.**
The entity's own documentation states that rows *"are normally created automatically by
`PositionVacancyInterceptor` when an employee's status turns to Terminated/Retired or their
`PositionId` changes (promotion / transfer), so no exit path can forget to log it"* and that
*"A departure is ALWAYS logged and then classified … never silently dropped"*.

None of that is true in the code as it stands:

- The name `PositionVacancyInterceptor` occurs in exactly two places, both XML doc comments
  (`PositionVacancyEntities.cs` and `IPositionVacancyService.cs`). There is no such class. The
  only interceptor registered is `AuditInterceptor`.
- `new PositionVacancy` is constructed in exactly **one** place in the solution: inside
  `ReconcilePositionVacanciesAsync`.

So a termination, retirement, promotion or transfer logs nothing. The register is only as current
as the last time somebody pressed Reconcile — which, per G-3.1, HR cannot do. *Impact: high. The
documentation in the entity file should be corrected either way, because it describes a guarantee
the system does not provide.*

**G-3.3 — `raise-requisition` has no permission check at all.** Every other action on this
controller carries an explicit policy. This one carries none, so it inherits only the
controller-level `InternalOnly` — which, as established in chapter 2, is a blocklist that admits
any authenticated non-external user. The service adds a tenant check but no permission check; it
only requires that the caller's account is linked to an employee record.

The effect: any ordinary employee can create a draft `StaffRequisition` and flip a position
vacancy to `RequisitionRaised`.

**Refined after walking chapter 4.** Half of this is deliberate: `POST /StaffRequisitions`
(create) is *also* ungated, because the module's stance is that any internal employee may ask for
headcount — line managers raise requisitions, not HR. So the requisition-creating half of this
endpoint is consistent with the design, not an oversight.

What remains specific to `raise-requisition` is the **side effect on the vacancy register**: it
writes `StaffRequisitionId` and moves the vacancy to `RequisitionRaised`, a status reconcile
deliberately never closes. An ordinary employee can therefore pin a position vacancy open
permanently, which the plain create endpoint cannot do. *Impact: medium (revised down from high).*

**G-3.4 — three of the seven columns in the Vacancies grid are decorative.** Because reconcile is
the only writer and it stamps fixed values, every row it creates shows:

| Column | Always shows | Why |
|---|---|---|
| Reason | "Other" | `Reason = VacancyReason.Other`, hardcoded |
| Vacated by | "—" | `VacatedByEmployeeId` is never set |
| Since | the date Reconcile was pressed | `VacatedDate = DateTime.UtcNow` |

"Since" is the misleading one: it reads as *how long this post has stood empty* and actually means
*when someone last pressed a button*. The entity has the fields to hold the real answers; nothing
fills them. *Impact: medium.*

**G-3.5 — the Classification column is a constant, and one stats figure is always zero.**
`PositionVacancy.Classification` is assigned in exactly one place — reconcile — and always to
`WithinEstablishment`. `NoShortfall` and `OverEstablishment` are never written by anything. So the
Classification column reads "Within Establishment" on every row, and `noShortfallOrOver` in the
stats payload is always 0. The three-way classification described in § 1.5 is real in the enum and
in the entity's documentation, but no code produces two of its three values. *Impact: medium.*

**G-3.6 — the Anticipated tile is structurally zero.** `PositionVacancyStatus.Anticipated` is never
written by any service, and `IsAnticipated` is only ever assigned `false` (in reconcile).
`ExpectedVacancyDate` is never written at all. The only way a vacancy reaches Anticipated is a
human choosing it in the *Set the status by hand* dialog. The tile is therefore 0 unless somebody
has manually overridden a row into it — there is no anticipated-vacancy detection, despite the
entity carrying two fields for exactly that. *Impact: medium.*

**G-3.7 — two of the six options in the status dropdown always fail.** The dialog offers
`Anticipated, Open, UnderReview, RequisitionRaised, Filled, Closed`. The service rejects
`RequisitionRaised` and `Filled` with *"Use 'Raise Requisition' or the hiring flow to move a
vacancy to that status."* Picking either produces a toast reading *"Refused"*. The dropdown should
not offer them. *Impact: low — the refusal is at least explicit.*

**G-3.8 — the status dialog is a back door around the mandatory close reason.** Closing through the
dedicated dialog requires a reason, enforced client-side (button disabled) and server-side
(`[Required]` on a string, which does bite). But setting the status to `Closed` through the
override dialog reaches `UpdateStatusAsync`, which sets `Status` and `ClosedDate` and leaves
`ClosedReason` null — its `Notes` field is optional. Two paths to the same terminal state, one of
which drops the audit reason the other insists on. *Impact: medium.*

**G-3.9 — two different definitions of "how many people are in this post".** The establishment grid
counts with `HrServingEmployees.Predicate`: `!IsDeleted && IsActive && StaffStatus ∉ {Terminated,
Retired, Inactive}`. The headcount returned after a status/notes/close mutation comes from
`CountActiveOnPositionAsync`, which checks `!IsDeleted && StaffStatus ∉ {Terminated, Retired,
Inactive}` — **omitting `IsActive`**. An employee with `IsActive = false` but a live `StaffStatus`
is counted by one and not the other, so the figure returned by a mutation can disagree with the
grid it came from. *Impact: low — the shared predicate exists precisely to stop this, and one
call site does not use it.*

**G-3.10 — same role-versus-permission mismatch as G-2.3.** The header buttons and row actions
are drawn for `SuperAdmin`/`HR` while the API authorises on permissions held also by `TenantAdmin`,
`Admin` and `HR User`. Here the read queries are *not* role-gated, so those users see the tables
but no way to act on them. *Impact: medium.*

---

## 4. `/hr/recruitment/requisitions` — asking for headcount

**Files:** `page.tsx` (199), `new/page.tsx` (125), `[id]/page.tsx` (574), `[id]/edit/page.tsx` (181)
**Shared form:** `components/hr/recruitment/RequisitionFormFields.tsx` (518)
**Walked:** 2026-09-14

Four screens, one record. They are documented together because the form is shared between *new*
and *edit*, and because the detail page is where the whole lifecycle actually happens.

### 4.1 What a requisition is

A formal request to fill a post. It is the only door into recruitment proper: a `JobVacancy`
cannot exist without an approved `StaffRequisition` behind it (`JobVacancy.StaffRequisitionId` is
non-nullable). Everything upstream — an empty seat, an establishment gap — is a reason to raise
one; everything downstream waits for it to be approved.

One requisition can ask for **several heads** (`NumberOfPositions`, capped at 100) and tracks how
many have arrived (`PositionsFilled`). That is why there are two fulfilment statuses rather than
one.

Table: `StaffRequisitions`, plus four children — `StaffRequisitionCosts`,
`StaffRequisitionAttachments`, `StaffRequisitionComments`, `StaffRequisitionHistory`.

The number is generated server-side as `REQ-{year}-{sequence:00000}` — e.g. `REQ-2026-00042`.

### 4.2 Who can do what

This controller uses a different authorisation shape from the rest of the module, and it is worth
understanding because it is the deliberate one.

**Self-or-permission.** Reading a requisition, editing it, submitting it and recalling it are
governed by `SelfOrPolicyAsync`: *you*, if you are the employee named in `RequestedById`, **or**
anyone holding the relevant recruitment permission. A line manager can therefore track and rework
their own request without any HR permission at all.

**Deliberately ungated.** Four endpoints carry no policy attribute:

| Endpoint | Why |
|---|---|
| `POST /` (create) | anyone internal may ask for headcount — requisitions are raised by line managers, not by HR |
| `POST /{id}/submit` | self-or-write |
| `POST /{id}/approve` | *"authority for this step comes from the published workflow definition, and the service refuses anyone the engine does not name as an approver"* |
| `POST /{id}/reject` | same reason |

So approval authority is **not** a role or a permission here — it is whoever the published
workflow definition names. Two service-level rules back that up: `CanUserApproveAsync` must say
yes, and a requester can never approve their own request (compared by Employee id, so a second
login does not get round it).

**Permission-gated.** Hold, cancel, fulfil, link-vacancy, and all cost/comment writes need
`RecruitmentWrite`. Delete, cost deletion, comment deletion and attachment deletion need
`RecruitmentAdmin` — which, per G-3.1, **no role currently holds** except the all-permissions
admin roles.

### 4.3 `/hr/recruitment/requisitions` — the list

**Five counters** from `GET /StaffRequisitions/summary`: Total, Draft, Awaiting approval
(`submitted + underReview`), Approved, Filled (`fulfilled` only). The endpoint also returns
Rejected, On Hold, Cancelled and Partially Fulfilled, none of which is shown — so the four
visible detail tiles do not add up to Total, by design rather than by error.

**One filter**, a status dropdown. It switches which endpoint is used, which is unusual enough to
call out:

| Selection | Endpoint | Paged? |
|---|---|---|
| All requisitions | `GET /StaffRequisitions?pageNumber&pageSize=20` | yes |
| any single status | `GET /StaffRequisitions/status/{status}` | **no** |

The page comment is explicit about why: the paged list has no server-side filters and the
by-status read is not paged, so each mode uses the endpoint built for it rather than paging in the
client over a filtered set. The consequence is that the pager disappears entirely when a status is
selected, and that view returns every matching row at once (G-4.6).

**Table columns:** Number (linked), Title, Position, Unit, Type, Priority, Heads
(`positionsFilled/numberOfPositions`), Wanted by (`desiredStartDate`), Raised by, Status. The
whole row is clickable.

Note the paging envelope: HR's `PagedResult` uses `page`/`hasPrevious`/`hasNext` — **not**
`pageNumber` — which differs from the shape Finance and AR use.

### 4.4 `/hr/recruitment/requisitions/new` — raising one

Accepts `?positionId=` to pre-select the post. Saves as a **draft**; nothing reaches an approver
from this screen.

The form is five cards:

| Card | Fields |
|---|---|
| The role | Title\*, Position\*, Job description, Type\*, Priority\*, How many heads\*, Location, Description |
| Who is being replaced *(only when Type = Replacement)* | Outgoing employee, Departure date, Why they left |
| Timing | Desired start\*, Latest acceptable start, Target fill date |
| The case for it | Business justification\*, Impact if not filled |
| Budget and audience | Budget line picker, live budget check, Exception justification, Internal/External checkboxes, Notes |

**The organisation unit is not a field.** It is taken from the chosen position —
`organizationUnitId` and `organizationLevelId` are read off the position object and sent with the
payload. The two must agree, and the position is the record that already knows.

**The job description is guided, not free.** The form looks up the position's current approved
description and shows one of four states: *choose the position first*, the current approved
description as a link, a warning that a newer approved version exists (with a "use the current
version" action), or a note that the position has no approved description yet — in which case the
requisition can still be raised. The server independently refuses a description belonging to a
*different* position: *"The job description 'X' describes a different position; choose one written
for the requisition's position."* Without that rule a requisition could read as one job and hire
for another, since the offer letter later picks the position's description.

**Save is blocked client-side** until: a position, a title, a business justification, a desired
start date, at least one head, and at least one audience checkbox. The audience rule is enforced
server-side too — *"At least one of Allow Internal Candidates or Allow External Candidates must be
selected."*

On success it toasts the new number and navigates to the detail page.

### 4.5 `/hr/recruitment/requisitions/[id]` — the detail page

Six tabs, and a header whose buttons change with status.

**Header actions**, in the order they appear:

| Button | Shown when |
|---|---|
| Status badge | always |
| Edit | status is Draft or Rejected |
| Submit / Approve / Reject (from `WorkflowApprovalActions`) | submit when Draft/Rejected; approve/reject when Submitted/UnderReview |
| Open a vacancy | status is Approved **and** no vacancy linked yet |
| Record a hire | HR, and status is Approved or PartiallyFulfilled |
| Put on hold | HR, status is live (not Cancelled/Fulfilled) and not already OnHold |
| Cancel | HR, status is live |

*Open a vacancy* does not create anything — it navigates to
`/hr/recruitment/vacancies/new?requisitionId={id}`.

*Record a hire* opens a number input with a caption that matters: **"This is the running total,
not an increment."** Recording a number equal to `NumberOfPositions` closes the requisition as
Fulfilled; anything less leaves it PartiallyFulfilled.

**Tab 1 — Overview.** The budget check panel first, then four information cards:

- **The role** — position, job description (linked), organisation unit with its level, location
  with its level, type, priority, headcount as *"N of M filled"*, audience, and the linked vacancy
  (or "Not opened yet").
- **Replacement** — only rendered when `type === 'Replacement'`: outgoing employee, reason,
  departure date.
- **Timing** — raised, desired start, latest acceptable start, why that start date, expected offer
  date, target fill date, days to fill, fulfilled date.
- **The case for it** — description, business justification, impact if not filled, notes. All
  `whitespace-pre-wrap`, so line breaks survive.
- **Budget and establishment** — see § 4.7.

**Tab 2 — Costs.** `StaffRequisitionCosts`. Costs carry their own approval status
(`StaffRequisitionCostStatus`), and the enum's documentation is emphatic about what that status
is **not**: it records HR's approval of a cost, never whether it has been paid. Payment is
Finance's to say and is not recorded HR-side at all.

**Tab 3 — Discussion.** `StaffRequisitionComments`, which support threading
(`parentCommentId` with a replies endpoint).

**Tab 4 — Attachments.** `StaffRequisitionAttachments`, routed through the HR document upload
gate — files are virus-scanned and stored in the document repository, never exposed as public
links. Deletion is HR-only *and* needs `RecruitmentAdmin`.

**Tab 5 — History.** `StaffRequisitionHistory`: when, from status, to status, by whom, comments.
Written by `RecordHistoryAsync` on submit, approve, reject, recall, hold, cancel and fulfil —
**not** on create or edit. So a requisition that has never been submitted shows an empty history,
which the empty state says plainly.

**Tab 6 — Workflow.** The generic engine's own tab, showing the instance, its steps and who is
assigned.

### 4.6 `/hr/recruitment/requisitions/[id]/edit`

The same shared form, seeded from the record. Two guards:

- **Editable only while Draft or Rejected**, checked on the client (the form is replaced by a
  message) and enforced in the service: *"Only Draft or Rejected requisitions can be edited."*
  Rejected is editable on purpose — rework and resubmit is the loop that status exists for.
- **The position is locked** once a vacancy has been opened (`positionLocked={!!data.jobVacancyId}`).

The non-editable message reads: *"Only drafts and rejected requisitions can be changed. Recall it
first if it is still awaiting approval."* See G-4.3 — there is no recall control anywhere in the UI.

### 4.7 The two enforcement checks

Both run at **submit** and again at **approve**, and both are shown on the Budget check panel
before either.

**The budget check** (`BuildBudgetCheckAsync`). It finds the manpower budget line in one of two
ways: the line the requisition is explicitly **linked** to, or — failing that — a line matching
the position and fiscal year. Only an **Approved or Active** budget counts; a Draft budget
authorises nothing, and a linked budget since rejected or withdrawn is reported by name rather
than silently treated as absent.

Two derived columns follow from the link and are **never typed**: `IsBudgeted` and `BudgetCode`.
Before this they were a self-declared checkbox and a free-text box that nothing read.

The arithmetic it reports:

```
budgeted  = line.PlannedNewPositions, or max(0, PlannedCount - CurrentFilled)
drawdown  = heads on OTHER live requisitions against the same line
            (excluding Cancelled and Rejected)
remaining = budgeted - drawdown
projected = CurrentFilled + drawdown + this requisition's heads
```

Behaviour depends on `CompanyHrPolicySettings.BudgetEnforcementMode`:

| Mode | Over budget | No approved line covering the post |
|---|---|---|
| Off | nothing | nothing |
| Warn | logged, exception justification required | exception justification required |
| Block | **refused** | **refused outright** |

The fiscal year comes from the policy's own fiscal-year start month, not the calendar year, and is
computed from the **desired start date** rather than the request date.

**The establishment check** (`BuildEstablishmentCheckAsync`). If the position has no
`EstablishmentApprovedOn`, it returns *"Nobody has established this post, so no headcount rule
applies to it"* and nothing is enforced. Otherwise it refuses (Block) or warns (Warn) when
`filled + requested > ExpectedHeadcount`.

**The exception justification** (D-4). Required at submit whenever the check says
`ExceptionRequired`, which happens in three situations: no approved line covers the post; a line
covers it but the requisition is not raised against it; or the post is established and has **no
gap**. Under Block, an unlinked requisition is refused outright rather than allowed with a reason.

**The establishment snapshot** (D-2). At every submit, four fields are stamped onto the
requisition — `EstablishmentSnapshotOn`, `…IsEstablished`, `…Expected`, `…Filled`, plus
`…SourceBudgetNumber`. The detail page shows *Establishment at submit* beside *Establishment now*
and renders an amber **"changed since submit"** chip when they differ. The drift is shown, never
used to refuse: the approver decides against the figures they were shown.

### 4.8 The lifecycle, exactly

| Action | From | To | Extra rules |
|---|---|---|---|
| Create | — | Draft | at least one audience |
| Edit | Draft, Rejected | unchanged | position locked once a vacancy exists |
| Delete | Draft only | — | needs `RecruitmentAdmin` |
| Submit | Draft, Rejected | Submitted *(or Approved — see G-4.1)* | budget + establishment enforced, exception required, snapshot stamped |
| Approve | Submitted, UnderReview | Approved | **not your own**; engine must name you an approver; budget + establishment re-enforced |
| Reject | Submitted, UnderReview | Rejected | engine must name you an approver; reason appended to Notes |
| Recall | Submitted, UnderReview | Draft | requester only — **no UI** (G-4.3) |
| Hold | anything except Cancelled, Fulfilled, OnHold | OnHold | reason required by the UI — **no way back** (G-4.2) |
| Cancel | anything except Cancelled, Fulfilled | Cancelled | final, reason required |
| Fulfil | Approved, PartiallyFulfilled | PartiallyFulfilled or Fulfilled | running total between 1 and `NumberOfPositions` |

`UnderReview` is never written by the engine — it is a state a reviewer sets by hand while
gathering information. Rejection lands on `Rejected`, not back on `Draft`, so a requester can tell
that someone ruled against the request rather than that they never sent it.

### 4.9 Known gaps

> **✅ Eight closed, one closed in principle, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-4.1 | Submit asks `HasActiveApprovalWorkflowAsync` first and lands at `Submitted`; `ApproveAsync` and its segregation-of-duties rule then run. Approve, reject **and recall** all gained no-workflow branches — fixing submit alone would have stranded the record. |
> | G-4.2 | ⚠ **The requisition's own hold is unchanged.** The lesson was applied where the map was being edited anyway: the vacancy's new `OnHold` (G-5.3) has a way out to every live status. Re-opening the requisition's hold is a separate change and is **not** done. |
> | G-4.3 | Recall has a button, a dialog and a hook command. Gated on who raised it, not on permissions. |
> | G-4.4 | On `HrServingEmployees.Predicate`. Three definitions of "filled" became one. |
> | G-4.5 | The automatic path **counts confirmed hires** rather than incrementing; the manual path refuses a number below that count. |
> | G-4.6 | The unpaged status filter is disclosed on screen, as the applications list already did. |
> | G-4.7 | The `numberOfPositions >= 1` check added to the edit page. |
> | G-4.8 | Follows from G-3.1. |

**G-4.1 — with no workflow definition published, Submit silently approves.** `WorkflowIntegrationService.SubmitAsync`
begins by asking `HasActiveApprovalWorkflowAsync`. When the answer is no — no entity-type record
for `StaffRequisition`, or no active definition against it — it returns early with
`Success = true` and **`WorkflowOutcome.Approved`**. The adapter maps that outcome straight to
`StaffRequisitionStatus.Approved`.

So pressing Submit takes the requisition Draft → **Approved** in one step, with no approver, no
`CanUserApproveAsync` check, and — critically — **no segregation-of-duties check**, because the
"you cannot approve a requisition you raised yourself" rule lives in `ApproveAsync`, which is
never called. The history row records the transition, so it looks deliberate afterwards.

> ⚠ **This finding said "no `StaffRequisition` workflow definition is seeded anywhere in the
> solution, so this is the out-of-the-box behaviour". That was false when written** (corrected
> 2026-09-16). `DatabaseSeedingService.EnsureHrWorkflowsSeededAsync` seeds 28 HR definitions —
> `StaffRequisition` among them — `IsActive` and `Published`, and it landed 2026-09-02, twelve days
> before this walk. On a seeded tenant this never fired.
>
> The gap is still correctly closed, because the unseeded state is reachable and two other HR
> services' authors hit it and wrote their own guards. But the severity was overstated. See the
> *note on method* in Appendix C — this is its fifth instance, and the costliest.

The budget and establishment enforcement *does* still run, since it happens before the workflow
call — that is the only guard left standing.

The detail page's own comment asserts the opposite: *"⚠ Inoperable until a `StaffRequisition`
workflow definition is published."* It is not inoperable; it auto-approves. *Impact: high — an
unreviewed request reaching Approved is indistinguishable, afterwards, from an approved one.*

**G-4.2 — On Hold is a dead end.** `PutOnHoldAsync` will hold a requisition in any status except
Cancelled, Fulfilled or already-OnHold — including a Draft. There is **no un-hold path anywhere**:
no `RemoveHold`, no `Resume`, no status endpoint, and the adapter explicitly never writes OnHold
so the engine cannot move it either.

Once held, every other route out is closed by its own precondition: Edit needs Draft/Rejected,
Submit needs Draft/Rejected, Approve/Reject need Submitted/UnderReview, Fulfil needs
Approved/PartiallyFulfilled. The single remaining action is **Cancel**, which is final. The
confirmation dialog tells the user *"It can be taken off hold later"*, which is not true.
*Impact: high — a reversible-sounding action is irreversible, and the dialog says otherwise.*

**G-4.3 — Recall is fully built and completely unreachable.** The endpoint exists
(`POST /{id}/recall`), the service method exists and enforces requester-only, the adapter handles
`WorkflowOutcome.Recalled`, and the client method `staffRequisitionService.recall()` exists. But
nothing calls it: `useWorkflowRecord` has no recall concept at all — its command surface is
submit, approve, reject — and a search of the whole frontend finds no caller. Other HR modules
(movements, salary change requests) *do* wire their recall to a button.

Meanwhile the edit page tells the user, in as many words, *"Recall it first if it is still
awaiting approval."* That is advice for a control the UI does not have. *Impact: medium — a
requester who has submitted prematurely cannot take it back; they must ask an approver to reject
it.*

**G-4.4 — a third definition of "how many people are in this post".** Chapter 3 recorded two.
This adds a third: `BuildEstablishmentCheckAsync` counts `e.IsActive` **only**, ignoring
`StaffStatus` entirely. So the three counts are:

| Where | Predicate |
|---|---|
| Establishment grid (ch. 3) | `!IsDeleted && IsActive && StaffStatus ∉ {Terminated, Retired, Inactive}` |
| Vacancy mutation response (ch. 3) | `!IsDeleted && StaffStatus ∉ {Terminated, Retired, Inactive}` |
| **Requisition budget/establishment check** | `IsActive` only |

A terminated employee whose `IsActive` flag was never cleared counts as filled for the requisition
check but not on the establishment screen — so the two screens can disagree about whether a gap
exists, and the requisition check is the one that refuses submissions. The shared
`HrServingEmployees.Predicate` exists precisely to prevent this and is used by none of the last
two. *Impact: medium.*

**G-4.5 — two writers advance `PositionsFilled`, and they can disagree.**

> **Corrected 2026-09-15 (chapter 12).** This gap was first recorded as *"`PositionsFilled` is
> written in exactly one place: `FulfillAsync`… nothing in the hire flow touches it."* **That was
> wrong**, and the error was mine: the search that produced it used a pattern that failed to match
> the `++` increment. Confirming a start *does* advance the requisition. What follows is the
> corrected finding.

There are two writers:

| Writer | Behaviour |
|---|---|
| `StaffRequisitionService.FulfillAsync` | sets an **absolute running total** from the number a user types into *Record a hire* |
| `JobOfferHireService.IncrementRequisitionFillAsync`, called by `ConfirmStartAsync` | **increments by one**, walking hire → offer/application → vacancy → requisition |

The automatic path is carefully written — it never over-counts past `NumberOfPositions`, never
resurrects a Cancelled or On Hold requisition, and never downgrades one already Fulfilled. It also
promotes the requisition to PartiallyFulfilled/Fulfilled, but only from Approved or
PartiallyFulfilled.

The gap is that the two are not reconciled. A recruiter who records "2 filled" by hand and then
confirms a start gets 3, because the manual path sets a total and the automatic path adds to
whatever it finds. Neither screen mentions the other, and *Record a hire* still describes itself
as *"the running total, not an increment"* without saying that confirming a start will also move
it. *Impact: low-medium — bounded by `NumberOfPositions`, but silently wrong in between.*

**G-4.6 — the status filter silently drops paging.** Selecting any status switches to
`GET /status/{status}`, which is not paged and returns every matching row. On a tenant with a long
history, "All requisitions" is a safe 20 rows while filtering to *Fulfilled* could return
thousands in one response, with no pager and no indication that the view is unbounded.
*Impact: low now, grows with data.*

**G-4.7 — the edit page omits one client-side check the new page has.** *New* requires
`numberOfPositions >= 1` before enabling Save; *edit* does not. The server carries `[Range(1, 100)]`
on both DTOs, so the write is refused either way — but on edit it surfaces as a raw ModelState 400
rather than a disabled button. *Impact: low.*

**G-4.8 — `RecruitmentAdmin` gates four destructive actions no role holds.** Delete requisition,
delete cost, delete comment and delete attachment all require `HR.Recruitment.Admin`, which per
G-3.1 is granted to no role other than the all-permissions admin roles. HR cannot delete a draft
requisition it raised in error, or remove a mistaken attachment. *Impact: medium.*

---

## 5. `/hr/recruitment/vacancies` — the advertised role

**Files:** `page.tsx` (178), `new/page.tsx` (530), `[id]/page.tsx` (517),
`[id]/pipeline/page.tsx` (442), `[id]/screening/page.tsx` (711)
**Walked:** 2026-09-15

Five screens. The detail page is the record; pipeline and screening are two workspaces scoped to
it, which is why neither appears in the module index.

### 5.1 What a vacancy is

An approved requisition turned into something you can advertise and receive applications against.
`StaffRequisitionId` is **required** — the list page says so in a line beside the filter, and there
is deliberately **no "New vacancy" button**. The only way in is *Open a vacancy* on an approved
requisition.

Three things are snapshotted from the requisition at creation and are not fields on the form:
`PositionId`, `AllowInternalCandidates` and `AllowExternalCandidates`. The audience flags are
copied rather than referenced, so each vacancy can diverge later — and they decide which adverts
get raised at publication (§ 5.5).

Everything the form *does* ask for is what makes it an advert: the title candidates see, the
deadline, the salary range, who is hiring, how it will be screened.

Numbering comes from `GetNextVacancyNumberAsync`. Status starts at Draft regardless of what the
caller sends.

### 5.2 The state machine

This is the most carefully built thing in the module so far, and it is worth reading as the
reference for how the rest ought to work. An explicit transition map, not a chain of ifs:

| From | May become |
|---|---|
| Draft | PendingApproval, Approved, Cancelled |
| PendingApproval | Approved, Rejected, Draft, Cancelled |
| Rejected | Draft, Cancelled |
| Approved | Published, Draft, Cancelled |
| Published | ClosedForApplications, Shortlisting, Draft, Cancelled |
| ClosedForApplications | Shortlisting, Published, Cancelled |
| Shortlisting | Interviewing, ClosedForApplications, Cancelled |
| Interviewing | OfferStage, Shortlisting, Cancelled |
| OfferStage | Filled, Interviewing, Cancelled |
| Filled | *(terminal)* |
| Cancelled | *(terminal)* |

A refusal names the legal next states: *"A vacancy cannot go from X to Y. From here it can only
become: …"*. The map exists because there were once two doors into the same operation, one of them
unlocked — a cancelled vacancy could be resurrected and a Draft could jump straight to Filled
without ever being approved, published or advertised.

Note what is missing: **`OnHold` (12) appears nowhere in the map**, and `Rejected` is reachable
only from `PendingApproval`. See G-5.3.

### 5.3 `/hr/recruitment/vacancies` — the list

No counters, no create button. A status dropdown that switches endpoints exactly as the
requisitions list does — paged for "All", unpaged for a single status (the same G-4.6 shape).

Columns: Number, Job title, Position, Unit, Requisition (linked, with `stopPropagation` so the
link does not also open the row), Heads, Deadline, Applications, Status.

`jobTitle` is computed, not stored: `CustomAdvertTitle ?? Requisition.JobDescription.JobTitle ?? ""`.

### 5.4 `/hr/recruitment/vacancies/new` — opening one

Requires `?requisitionId=`. Without it the page shows *"Start from a requisition"* and a button to
the requisitions list. With it, but where the requisition is not **Approved**, the form is replaced
by *"This requisition is {status} — a vacancy can only be opened once the headcount has been
approved."*

An alert states what is inherited: position, unit, and the audience, with the note that *"those
audience flags decide which adverts are raised when you publish."*

Four cards:

| Card | Fields |
|---|---|
| The advert | Advert title (blank = use the job description's title), How many heads (seeded from the requisition), Employment type, Work mode, Minimum experience, Key benefits |
| Dates and people | Application deadline (default +21 days), Shortlisting deadline, Target start date, Interview rounds, Hiring manager, Recruiter |
| Salary and assessment | Salary from/to, Currency (default GHS), Show salary on the advert, Requires written test, Requires practical test |
| Screening | Recruitment pipeline, Blind screening, Auto-shortlist threshold, Require every mandatory criterion |

**The pipeline choice matters more than it looks.** The form says so: *"Without a pipeline this
vacancy has no board, and its applications cannot be moved through stages."* Only **active**
pipelines are offered — an inactive one stays attached where it already is but should not be
picked for something new. Because there is no edit screen (G-5.1), this choice is effectively
permanent.

### 5.5 `/hr/recruitment/vacancies/[id]` — the detail page

**Four counters:** Applications, Shortlisted, Offers, Hired (as *"N of M"* against
`NumberOfPositions`). These are denormalised counters on the vacancy row, maintained by the
services that shortlist, offer and hire.

**Header actions:**

| Button | Shown when |
|---|---|
| Pipeline / Screening | always — the two per-vacancy workspaces |
| Approve | HR, status Draft or PendingApproval |
| Publish | HR, status Approved |
| Close for applications | HR, status Published |
| "Advance to…" dropdown | HR, not terminal — offers the five hiring stages |
| Cancel | HR, not terminal |

There is **no Edit button**, and no `[id]/edit` route. See G-5.1.

**Publishing has side effects, and they are the interesting part.** Entering `Published`:

1. stamps `ActualPublishDate` the first time only, leaving the planned `PublishDate` intact so
   planned-versus-actual can be compared;
2. **auto-creates adverts** — an `InternalPortal` posting if `AllowInternalCandidates`, a
   `CompanyWebsite` posting if `AllowExternalCandidates`, each skipped if one already exists on
   that channel. If both flags are somehow false it defaults to external. Each gets
   `ExpiryDate = ApplicationDeadline` and `Description = ""` (G-5.6).

**Leaving `Published` for anything** — cancelled, closed for applications, or edited back to Draft
— **expires every live advert**: status `Expired`, `IsActive = false`, `ExpiryDate = now`. The
comment explains why this was needed: publication created the postings and nothing retired them,
so a cancelled vacancy left its adverts sitting Published and IsActive, still listed by
`GET /job-postings/active` and still carrying whatever URL had been syndicated externally. The
public career portal filters on the vacancy's own status, which is exactly why the inconsistency
was invisible from outside while internal lists kept advertising a role nobody was hiring for.
Adverts are **expired, not deleted** — a posting is the record that the role was advertised on a
channel, and re-publishing raises fresh ones.

**Eight tabs:** Overview (three info cards plus key benefits and closure), Adverts, Shortlisting
criteria, Stage owners, Pool matches, Attachments, EEO report (HR only), History.

Two worth distinguishing, because their names invite confusion:

- **Stage owners** assigns *who is responsible for each stage* of this vacancy
  (`VacancyPipelineStageAssignment`).
- **`/pipeline`** moves *applications* between stages. Different thing entirely.

The criteria tab gates its remove control on `hasPermission('HR.Recruitment.Admin')` rather than
on `isHr` — deleting a criterion sits a tier above adding one. It also carries a
`usesProtectedCharacteristic` flag: where a Gender or Age criterion exists, the screen says so,
because such a criterion may inform a score but never disqualify.

**Closing.** Two different operations behind two buttons:

| | Close for applications | Cancel |
|---|---|---|
| Endpoint | `POST /{id}/close-for-applications` | `POST /{id}/close` |
| Precondition | must be Published | not already Cancelled |
| Result | `ClosedForApplications` | `Cancelled` |
| Meaning | stops accepting applications, shortlisting continues | final |
| Adverts | expired | expired |

Both take a `JobVacancyClosureReason` and free-text notes, and both write a status-history row.

### 5.6 `/hr/recruitment/vacancies/[id]/pipeline` — the board

**Deliberately not a drag-and-drop Kanban.** The page comment explains: the legacy board read
returns every card in every column at once, which does not scale past a busy vacancy. So this is a
**stage bar plus a paginated table** — the bar gives the at-a-glance shape, the table is the
endpoint that filters, sorts and pages.

**The first bucket is the inbox.** It is synthetic, with an all-zeroes stage id
(`00000000-0000-0000-0000-000000000000`), and holds applications that have arrived but have not
been placed in any stage. Most work starts there.

If the vacancy has no pipeline, the whole board is replaced by *"No pipeline assigned… Assign one
on the vacancy, then come back."* — advice that cannot be followed (G-5.1).

**Bulk operations** are partial by design: *"Each application is processed on its own, so one
refusal does not stop the rest — the per-application outcomes come back afterwards."* The result
card shows succeeded/skipped and lists up to eight failure messages. Bulk move targets only real
stages (the inbox is filtered out of the target list); bulk reject requires a reason.

Score column shows `autoScore` with an **asterisk and grey text when stale**.

### 5.7 `/hr/recruitment/vacancies/[id]/screening` — turning applications into a shortlist

Five tabs rather than five screens, *"because they are one job: you score, you compare, you
decide, you send it up."* Blind screening and the EEO report sit alongside deliberately — they are
the two checks on the decision the other tabs are making.

**Four counters:** Applications; Scored (*N / total*, amber with a *"N stale"* hint); Shortlisted
(with waitlisted count); Average score (with high/low).

**A red banner** appears when the shortlisting deadline has passed: *"Shortlisting is refused for
this vacancy until HR extends the deadline. Un-shortlisting and rejecting still work."* The guard
is real — both `ShortlistAsync` and `AutoShortlistByScoreAsync` throw on it. Extending the
deadline, however, is impossible (G-5.1).

| Tab | What it does |
|---|---|
| Shortlist | Score all, Auto-shortlist by score, bulk shortlist the selected. Rows sorted by score descending; already-shortlisted, rejected and withdrawn rows are not selectable |
| Comparison | the criteria matrix — a column per criterion with its weight and mandatory flag, cells red where the criterion was not passed |
| Blind screening | applications with name, gender, age, location and contact withheld **server-side**; a 422 here means the setting is off, not that anything failed |
| Diversity | the EEO report: all applicants / shortlisted / rejected / hired by gender, plus internal count |
| SLA | published date, shortlisting deadline, completion, time to shortlist, days remaining or overdue, approval status |

Two footnotes on those tabs are worth keeping:

- *"Time to shortlist is measured from the publish date and stamped when the shortlist is
  **approved** — not when the last candidate is shortlisted."*
- *"Compare the shortlisted row against all applicants — a large shift between the two is what
  this report exists to surface."*

Below the tabs sit the two things that come **after** the shortlist: the approval card, and
candidate notifications (*"Safe to run again — anyone already notified is skipped"*).

**Auto-shortlist by score** takes a threshold (seeded from the vacancy's own
`AutoShortlistMinScore`), a *require all mandatory* switch, and notes recorded against every
application it touches. It selects applications that are not Withdrawn / Rejected / already
Shortlisted / Hired, **have a score**, are **not stale**, and are at or above the threshold.

Two behaviours here are deliberate and easy to miss:

- **Stale scores are skipped entirely.** The screen says so twice and tells you to re-score first.
- **Adding candidates invalidates an in-flight approval.** If the shortlist was already
  `PendingApproval` or `Approved`, it resets to `NotSubmitted` so the approver re-reviews the full
  list.

### 5.8 How a score is computed

Worth writing down exactly, because the number drives auto-shortlisting.

```
1. No criteria on the vacancy      → AutoScore = null.   Never auto-shortlisted.
2. Evaluate EVERY criterion        → full breakdown stored as JSON, even after a failure
3. Any MANDATORY criterion failed  → AutoScore = 0
4. Otherwise:
     criterionScore = earned / totalWeight × 100
   where a criterion the engine cannot evaluate (type "Other") is excluded
   from BOTH earned and totalWeight — it neither lifts nor lowers anybody
5. Test blend (if TestScoreWeight > 0 and scored tests exist):
     final = criterionScore × (100 − w)/100  +  avgTestPct × w/100
6. Internal boost (if InternalCandidateBoostPoints > 0 and candidate is internal):
     final = min(100, final + boost)
```

Step 1 is a repaired defect worth knowing: it used to write **100**, so "auto-shortlist by score"
on a vacancy with no criteria admitted every applicant. Step 4's exclusion of unevaluable criteria
is the same shape — they used to pass everyone with full marks.

Scoring runs against a **snapshot** of the candidate's profile as it stood when they applied
(`ApplicationCandidateSnapshot`, § 1.3), falling back to the live profile only for legacy rows
with no snapshot.

Steps 5 and 6 never execute in practice — see G-5.2.

### 5.9 Known gaps

> **✅ All eight closed, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-5.1 | `/hr/recruitment/vacancies/[id]/edit` exists and calls the `PUT` that always did. All three screens quoted below now tell the truth. ⚠ It sends **every** field — the payload is whole-record, so an omitted field is a cleared field. |
> | G-5.2 | `TestScoreWeight` and `InternalCandidateBoostPoints` on all three write DTOs and both forms. Default 0, so an untouched vacancy scores as before. |
> | G-5.3 | `OnHold` is in the transition map **with a way out to every live status** — the G-4.2 lesson. `PendingApproval` and `Rejected` are reachable because the picker now asks the server what is legal. |
> | G-5.4 | `GuardApprovalSeparation` refuses approval by whoever opened the vacancy. Narrow by design: it says who may *not* approve. |
> | G-5.5 | `CloseAsync` goes through `GuardTransition` — it was the third door that bypassed the map. |
> | G-5.6 | Auto-created adverts carry composed copy and the position's title. Salary appears only when `IsSalaryVisible`. |
> | G-5.7 | The vacancy DTO carries `AllowedNextStatuses` from the same map the write guards with — sent, not mirrored, because the map changed in the same commit. |
> | G-5.8 | Disclosed on screen, with G-4.6. |

**G-5.1 — there is no way to edit a vacancy, and three screens tell you to.** `PUT /api/job-vacancies/{id}`
exists. `jobVacancyService.update()` exists. **Nothing calls it** — a search of the whole frontend
finds no caller, there is no `[id]/edit` route, and the detail page has no Edit button.

So once a vacancy is created, these are fixed for its entire life: advert title, heads, employment
type, work mode, minimum experience, key benefits, **application deadline**, **shortlisting
deadline**, target start date, interview rounds, hiring manager, recruiter, salary range and
visibility, test requirements, **recruitment pipeline**, **blind screening**, and the
auto-shortlist threshold. Only status, criteria, stage owners, adverts and attachments can change.

Three screens give instructions that depend on the missing screen:

| Screen | Says | Reality |
|---|---|---|
| Pipeline (no pipeline assigned) | *"Assign one on the vacancy, then come back."* | cannot be assigned after creation |
| Screening → Blind screening | *"Switch it on in the vacancy's settings."* | there are no vacancy settings |
| Screening (deadline passed) | *"…until HR extends the deadline."* | HR cannot extend it |

The pipeline case is the worst: a vacancy created without a pipeline can never have one, so its
applications can never be moved through stages at all. *Impact: high.*

**G-5.2 — two scoring inputs cannot be set by any means, so two branches of the algorithm are
dead.** `TestScoreWeight` and `InternalCandidateBoostPoints` appear in `JobVacancyDto` (the read
DTO) and nowhere else — **neither the create DTO nor the update DTO carries them**, and no
frontend file mentions either name. Both default to `0`.

Since step 5 requires `TestScoreWeight > 0` and step 6 requires
`InternalCandidateBoostPoints > 0`, the test-score blend and the internal-candidate boost can
never execute. Consequently the form's *Requires a written test* and *Requires a practical test*
checkboxes record a requirement whose results can never affect a score, and `JobApplicantTestResult`
rows are captured but never weighed. *Impact: medium — a documented, implemented feature that
cannot be switched on.*

**G-5.3 — three statuses are unreachable.** Of the twelve `JobVacancyStatus` values:

- **`OnHold` (12)** appears in no transition in the map and in no service code. It cannot be
  reached at all, by UI or API. Compare the requisition's OnHold (G-4.2), which can be reached and
  then not left.
- **`PendingApproval` (2)** is never set by any UI control — the Approve button goes straight to
  `Approved` from Draft.
- **`Rejected` (4)** is reachable only *from* `PendingApproval`, and there is no Reject button
  anywhere. Since nothing sets PendingApproval, nothing can be Rejected.

*Impact: medium — the enum describes a richer lifecycle than the screens offer.*

**G-5.4 — vacancy approval is one click by whoever is looking at it.** Unlike a requisition, a
vacancy has **no workflow integration at all** — `JobVacancy` appears nowhere in the workflow
services. *Approve* is a plain status change guarded only by `RecruitmentWrite`, with no separate
approver, no segregation of duties, and no check that the person approving is not the person who
opened it. Given that a vacancy is what authorises advertising and hiring against approved
headcount, this is a lighter gate than the requisition behind it. *Impact: medium.*

**G-5.5 — Cancel bypasses the state machine.** `CloseAsync` checks only *"is it already
Cancelled?"* and never calls `GuardTransition`. So a **Filled** vacancy — terminal in the map —
can be cancelled through the API, undoing a completed hire's vacancy record. The UI hides the
button when terminal, so this is reachable only by calling the endpoint directly, but the map is
the thing that is supposed to make that impossible. *Impact: low-medium.*

**G-5.6 — auto-created adverts have an empty body.** Both the internal and external postings raised
at publication are created with `Description = string.Empty` and a title of
`CustomAdvertTitle ?? VacancyNumber`. A vacancy published without a custom advert title produces
an advert headed with a reference number and no text. *Impact: medium — this is what candidates
see.*

**G-5.7 — the "Advance to…" dropdown offers illegal moves.** It lists all five hiring stages minus
the current one, from any non-terminal status, so a Draft vacancy is offered *Filled* and a
Published one is offered *Interviewing*. Both are refused. This is a deliberate choice — the page
comment says *"the server owns the real state machine… do not try to mirror the full map"* — and
the refusal names the legal next states, so it fails safely and informatively. Recorded as a gap
rather than a defect because the user still has to click to find out. *Impact: low.*

**G-5.8 — same unpaged status filter as the requisitions list.** See G-4.6. *Impact: low.*

**An observation, not a gap:** *Score all* exists on both the pipeline page and the screening page
and they call **different endpoints** —
`POST /api/applications/pipeline/{vacancyId}/run-scoring` and the job-application service's
`scoreAll`. Both end at the same evaluation; the pipeline one reports
*"Scored N of M"* with failures, the screening one reports a plain count.

---

## 6. `/hr/recruitment/postings` — every advert, in one list

**File:** `frontend/src/app/hr/recruitment/postings/page.tsx` (150)
**Managed from:** `components/hr/recruitment/VacancyPostingsPanel.tsx` (433)
**Walked:** 2026-09-15

### 6.1 What it is

The page is titled **Adverts**; the route is `postings`. It is a flat list of every `JobPosting`
across every vacancy and channel, for the recruiter who wants one list rather than a vacancy at a
time.

It is **entirely read-only**. There are no counters, no filters beyond a two-option view switch,
and no actions. Clicking a row goes to `/hr/recruitment/vacancies/{id}?tab=adverts` — the
per-vacancy Adverts panel, which is where adverts are actually created, published, expired and
removed. The page has no role gate of its own; both queries fire for any viewer and the API
authorises on `RecruitmentRead`.

**Why the route is not `/adverts`.** The page's own comment records it: ad blockers (EasyList)
block any URL containing `/adverts/`, which took out this page's own JavaScript chunk and left it
a `ChunkLoadError`. The folder name is a workaround for the reader's browser, not a naming
preference — worth knowing before anyone "tidies" it.

### 6.2 The two views

| View | Endpoint | Query |
|---|---|---|
| Live adverts | `GET /api/job-postings/active` | `IsActive && Status == Published` |
| Past expiry but still live | `GET /api/job-postings/expired-active` | `IsActive && ExpiryDate != null && ExpiryDate < now` |

The second view is the one that earns the page its place, and it comes with an amber warning:
*"These are past their expiry date and still accepting applications."*

That view exists because **nothing expires a posting automatically**. Only two code paths ever
write `JobPostingStatus.Expired`:

1. `JobPostingPipelineService.ExpireAsync` — the manual *Expire* button on the per-vacancy panel;
2. `JobVacancyService.ExpirePostingsIfUnpublishedAsync` — the cascade when a vacancy leaves
   `Published` (§ 5.5).

There is no background sweep. So an advert whose closing date passed last month is still
`Published`, still `IsActive`, and still listed as live until a human notices — which is exactly
what this second view is for.

**This settles G-2.2.** The landing page's "Live adverts" tile calls `/active`, which ignores
`ExpiryDate`, and so counts these overdue adverts as live. The capability to distinguish them
exists and is used *here*; it is the landing tile that does not use it.

### 6.3 The table

| Column | Source | Note |
|---|---|---|
| Title | `JobPosting.Title` | rendered as an external link when `PostingUrl` is set, with `stopPropagation` so the link does not also open the row |
| Vacancy | `JobVacancy.VacancyNumber` | monospace |
| Channel | `JobPosting.Channel` | |
| Published | `ActualPublishDate ?? PublishDate` | actual preferred, planned as fallback |
| Expires | `ExpiryDate` | |
| Applications | `JobPosting.ApplicationCount` | **always 0** — see G-6.1 |
| Status | `JobPosting.Status` | |

**The nine channels** (`JobPostingChannel`): InternalPortal (1), CompanyWebsite (2), LinkedIn (3),
JobBoard (4), Agency (5), Indeed (6), Glassdoor (7), Newspaper (8), Other (9). Only the first two
are ever created automatically, at publication (§ 5.5); the other seven are added by hand on the
per-vacancy panel.

**The five statuses** (`JobPostingStatus`): Draft (1), Published (2), Expired (3), Closed (4),
Removed (5). In practice only Draft, Published and Expired are written; Closed and Removed appear
in no service code.

### 6.4 Where adverts are actually managed

The per-vacancy **Adverts** panel, reached by clicking a row here or from the vacancy detail page.
It offers *Add an advert channel* (title, body, link, external reference, planned publish date,
expiry date) and per-row Publish, Expire, Remove and attachments.

Two server-side rules are worth knowing because they close a real hole:

- **Publishing an advert checks the vacancy, not just the advert.** `PublishAsync` refuses unless
  the vacancy itself is `Published`. The comment explains what it was fixing: an advert could
  previously be published for a vacancy still in Draft — never approved by anyone — or for one
  already cancelled. *"Refusing to advertise an unapproved role is the entire reason a vacancy has
  an approval step in front of publication, and this was the way round it."*
- **An expired posting cannot be republished.** *"Create a new posting for the channel instead."*
  And a Published posting must be expired before it can be deleted.

Two identity fields support external syndication: `PostingUrl` (the live URL on the platform) and
`ExternalPostingId` (the board's own reference — a LinkedIn job id, an Indeed job key), with
`GET /api/job-postings/external/{externalPostingId}` to look a posting up by it. Nothing in the
solution calls an external board's API; these are recorded by hand.

### 6.5 Known gaps

> **✅ All four closed, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-6.1 | **Derived** at read time from the applications that name the posting — one grouped query per page. Deliberately not a maintained counter: two writers of one counter is G-4.5's shape, and the source-correction path would have had to decrement one advert and increment another. |
> | G-6.2 | The nightly sweep expires adverts past their closing date. |
> | G-6.3 | The overdue view has a per-row **Expire** and a bulk **Take them all down**. The endpoint and client method already existed; only the button was missing. |
> | G-6.4 | The six print fields are composed in the advert dialog, shown for Newspaper and Radio, and cleared if the channel changes. |

**G-6.1 — the Applications column is always zero.** `JobPosting.ApplicationCount` is read into
both the detail and summary DTOs and is **never written by anything** — no service increments it,
and `JobPosting` carries no `Applications` navigation from which it could be derived at read time.
(The similarly-named `JobVacancy.ApplicationCount` *is* maintained, by the portal and application
services; the posting-level one is not.)

Worse, the data to derive it is only half present. `JobApplication.JobPostingId` exists, and the
**candidate portal** sets it when an external applicant applies — but the two internal application
paths in `JobApplicationService` set `Source = InternalPortal` and leave `JobPostingId` null. So
even a derived count would read zero for every internal advert.

The practical effect: the one column on this page that would answer *"which channel is working?"*
is a constant. *Impact: medium.*

**G-6.2 — nothing expires an advert on its closing date.** As above: only a manual click or a
vacancy status change. The consequences reach beyond this page — the landing tile overstates live
adverts (G-2.2), and an advert past its deadline keeps whatever URL was syndicated to an external
board. This is the same shape as G-2.4 (offers never reach `Expired`): a status that only a human
can write, on a date-driven event. *Impact: medium.*

**G-6.3 — the overdue view is a diagnosis with no cure.** "Past expiry but still live" finds
exactly the adverts that need taking down, and offers **no way to take any of them down**. There
is no Expire action, no bulk selection. The user must note each one, click through to its vacancy,
find the Adverts tab, and expire it there one at a time. Given that this is the view's entire
reason for existing, the missing action is conspicuous. *Impact: medium.*

**G-6.4 — the print-advert composition block has no UI.** Six fields exist on the entity for
composing a newspaper advert — `AdvertHeadline`, `AdvertBody`, `HowToApply`, `ClosingDateText`,
`ShowSalaryInAdvert`, `ContactDetails`. They are carried on the create DTO, the update DTO and the
read DTO, mapped in both directions, and declared in the TypeScript types. **No component reads or
writes any of them.** `Newspaper` is a supported channel with no way to compose the advert that
would run in it, and the generic add-advert dialog offers only title, body, link and dates.
*Impact: low-medium — another instance of the pattern in Appendix C, and unusual in being typed
all the way to the browser before stopping.*

---

## 7. `/hr/recruitment/candidates` — the people

**Files:** `page.tsx` (279), `new/page.tsx` (76), `[id]/page.tsx` (356), `[id]/edit/page.tsx` (129)
**Shared form:** `components/hr/recruitment/CandidateFormFields.tsx`
**Walked:** 2026-09-15

### 7.1 What a candidate is

The person, not the pursuit. One `JobCandidates` row per human being, reused across every job they
ever apply for — § 1.1 makes the distinction and this is where it pays off: the detail page's
**Applications** tab lists every application this one person has made, across all vacancies.

A candidate can come into being three ways, and they behave differently:

| Origin | `IsInternalEmployee` | `UserId` | Country |
|---|---|---|---|
| HR types one in (`/candidates/new`) | false | null | required by the form |
| Someone registers on the careers site | false | set once they confirm their email | required on the public form |
| An **employee** applies via the internal job board | **true**, with `InternalEmployeeId` | null | usually absent |

That third kind is a **shadow candidate** — a `JobCandidate` minted automatically from an
`Employee` so an internal applicant has something to attach an application to.

**The country FK story is worth reading**, because it is the clearest example in this module of a
rule the database invented rather than the business. `CountryId` used to be required. That made the
internal job board unusable: both internal-apply paths had to refuse an employee with no country
on file, and on the DEFAULT tenant on 2026-08-27 that was **8,072 of 8,077 live employees**. The
refusal told them to "complete the employee record first" — a field only HR can change. The entity
comment concludes: *"A country is a requirement the foreign key invented, not one the business
asked for, so the key gives way."* It is now nullable, and the HR form still requires one because
an HR-typed record should name one.

### 7.2 Who can use it

The list page's own comment says *"HR-only, including the reads"*. That is the intent, but not
literally what the code does, and the difference is the same role-versus-permission split as
G-2.3:

- **The API** gates every read on `RecruitmentRead` and every write on `RecruitmentWrite`, with
  sub-resource deletes on `RecruitmentAdmin`. Held by `SuperAdmin`, `TenantAdmin`, `Admin`, `HR`
  and `HR User`.
- **The page** role-gates only the *New candidate* button and the *Edit* button. The list, the
  talent-pool tab and the email lookup all fire for any viewer; a viewer without the permission
  gets 403s and empty tables.

The caution behind the comment is real, though, and worth repeating: these records carry date of
birth, contact details, identity-document numbers, CVs and recruiters' private notes.

The detail page reads permissions directly rather than roles in two places — `canRecruit`
(`HR.Recruitment.Write` or `.Admin`) gates the photo dialog and the talent-pool and engagement
panels, and `canRecruitAdmin` gates deleting an engagement event. That is the more correct
pattern; the rest of the page still uses `isHr`.

### 7.3 `/hr/recruitment/candidates` — the list

**There is no search.** The only lookup is an **exact email match** through
`GET /api/job-candidates/email/{email}`, and the page is candid about why it exists: *"Use this
before creating a candidate — the API refuses a duplicate email."* It returns null rather than 404
when nobody matches, and the page says so: *"No candidate has that email address — it is free to
use on a new record."*

So finding a candidate by **name** means paging through 20 at a time (G-7.6).

**Two tabs**, sharing one table component: *All candidates* (paged) and *Talent pool*
(`GET /talent-pool`, unpaged).

Columns: Number, Name (with photograph), Email, Phone, Location (`city, countryName`), Talent pool
(a badge or a dash), Applications.

**The photograph goes through a gate.** `GatedPhoto` is given the endpoint
`/api/job-candidates/{id}/photo` and an `enabled` flag taken from `hasPhoto` on the row — so the
image is fetched only when the record says one is on file, rather than firing a request per row
and taking 404s. Photos are served by an authenticated endpoint, never a public URL.

### 7.4 `/hr/recruitment/candidates/new`

A thin page: a header and the shared form, validated with a zod schema through react-hook-form —
the only place in recruitment so far that uses schema validation rather than hand-rolled
`canSave` booleans.

The header sets expectations correctly: *"Qualifications, work history, referees and documents are
added once the record exists."* Except that, on the HR side, they cannot be — see G-7.1.

One implementation note the code flags twice, in both new and edit: empty strings are converted to
`null` before sending, because `''` does not bind to a `Guid?` and would be a 400 before the
service ever ran.

On success it toasts the candidate number and goes to the detail page. A duplicate email is
refused by the service with the address in the message, and the page shows that verbatim.

### 7.5 `/hr/recruitment/candidates/[id]` — twelve tabs

**Header:** the photograph (clickable — opens a dialog to view or replace it, gated on
`canRecruit`), a **CV** download button, a **Talent pool** button that simply switches to that tab,
and **Edit**.

The CV download is its own route (`GET /job-candidates/{id}/cv`) and fails loudly rather than
silently: *"No CV available — this candidate has not uploaded a CV."*

| Tab | Content | Writable from here? |
|---|---|---|
| Overview | three cards — Personal, Contact, Professional profile | no |
| Applications | every application by this person, with vacancy, status, stage and score | no (links out) |
| Talent pool | pool membership, status, review date, segments, vacancy matches | **yes** |
| Engagement | the engagement timeline | **yes** |
| Qualifications | `JobCandidateQualification` | **no** (G-7.1) |
| Work history | `JobCandidateWorkHistory` | **no** (G-7.1) |
| Referees | `JobCandidateReferee` | **no** (G-7.1) |
| Skills | `JobCandidateSkill` | **no** (G-7.1) |
| Languages | `JobCandidateLanguage` | **no** (G-7.1) |
| Interests | `JobCandidateInterest` | **no** (G-7.1) |
| Documents | `JobCandidateDocument` | **yes**, gated on `isHr` |
| Notes | `JobCandidateNote` | **yes**, gated on `isHr` |

**The Professional profile card is explicitly read-only and says so on screen:** *"Supplied by the
candidate. Not editable from the ERP side."* It carries headline, current role and employer, total
experience, notice period, availability, work preference, work authorisation, expected salary and
a free-text summary — none of which appear on the create or update DTOs. This is a deliberate
boundary, clearly drawn, and the edit page repeats it in its subtitle.

**The talent pool is two things at once**, and this tab shows both:

1. **Flat membership on the candidate row** — `IsInTalentPool`, plus enrichment fields
   `TalentPoolSource`, `TalentPoolStatus`, `TalentPoolNotes`, `TalentPoolReviewDate`,
   `TalentPoolAddedDate`, `TalentPoolRemovedDate`, `TalentPoolRemovalReason`, `LastEngagedDate`.
2. **Segment membership** — `CandidateTalentSegment` and `CandidateSegmentMembership`, a
   many-to-many for grouping candidates into named pools.

The tab manages both through `talentPoolService`, not through the candidate controller. The page
comment explains the switch: *"the rich endpoints record the source, reason and review date the old
one-click toggle silently dropped."* The talent-pool chapter covers the segment side properly.

### 7.6 `/hr/recruitment/candidates/[id]/edit`

The same shared form, seeded from the record, with two small corrections applied on load: the API
returns a full timestamp for dates and `<input type="date">` needs the date part only, so
`dateOfBirth` and `nationalIdExpiryDate` are sliced to 10 characters.

There are no status preconditions — a candidate is editable at any time by anyone with
`RecruitmentWrite`.

### 7.7 Identity, files and indexes

**The identity-document trio** — `NationalIdTypeId` (a lookup FK), `NationalIdNumber`,
`NationalIdExpiryDate` — is on both the form and the overview card, rendered as
*"{type} · {number}"* with the expiry beneath.

**Files are file-upload records, not paths.** `CvFileUploadRecordId` and
`ProfilePhotoFileUploadRecordId` point at the central upload registry, so the CV and photograph
come through the scanned-document gate like every other HR attachment.

**A note on the unique indexes**, because they are inconsistent in a way worth knowing:

| Index | Unique? | Filters `IsDeleted`? |
|---|---|---|
| `IX_JobCandidate_Email` | **no** — a plain lookup index | n/a |
| `IX_JobCandidate_UserId` | yes | **yes** — `UserId IS NOT NULL AND IsDeleted = 0` |
| `IX_JobCandidate_Tenant_Number` | yes | **no** — a deleted candidate's number stays reserved |

So the general warning in *Conventions* (a soft-deleted row still occupies its unique index) holds
for the candidate number but **not** for `UserId`, which was deliberately filtered. Do not assume
either way; check the index.

### 7.8 Known gaps

> **⚠ G-7.1 BELOW IS WRONG — it was already closed when checked.** The rest are closed as of
> 2026-09-15 → 16. Read the findings as history.
>
> | | Now |
> |---|---|
> | G-7.1 | ⚠ **The finding was incorrect.** `CandidateSubResourceTabs.tsx` does *not* contain "`useQuery` and nothing else": all six tabs ride `ResourceCollectionTab` with create, update and remove wired to the matching service methods. The work landed days before the walk, which appears to have read a stale checkout. **HR could add a referee all along.** Kept here, uncorrected, as the record — see *A note on method* in Appendix C. |
> | G-7.2 | `UpdateAsync` refuses a duplicate email. ⚠ **The index is still not unique** — soft-deleted rows occupy a unique index, so a tenant with historical duplicates would fail the migration. Needs a data check first; see the box at the top of this document. |
> | G-7.3 | `Nationality` on both write DTOs and the form. Free text — not the same question as country of residence. |
> | G-7.4 | Both endpoints and both client methods deleted. The service methods remain, marked *do not wire a new door onto these*. |
> | G-7.5 | The comment now describes the permission gate that exists, and says narrowing it is a controller decision. |
> | G-7.6 | The register uses the **same** multi-field search predicate the talent pool always had, so two searches over one table cannot disagree. The exact-email box stays — it answers a different question. |

**G-7.1 — six of the twelve tabs are read-only, and eighteen write endpoints have no caller.**
`CandidateSubResourceTabs.tsx` is 680 lines containing `useQuery` and nothing else — no
`useMutation`, no `mutationFn`, no buttons. All six of Qualifications, Work history, Referees,
Skills, Languages and Interests are display-only.

The API offers the full set for each: `POST /{candidateId}/{resource}`,
`PUT /{candidateId}/{resource}/{id}` and `DELETE /{resource}/{id}` — eighteen write endpoints in
total, none of them reachable.

The consequence is sharpest for a candidate HR types in by hand. The new-candidate page promises
*"Qualifications, work history, referees and documents are added once the record exists"*, and of
those four only documents can be. In particular **HR cannot add a referee** — and referees are
what a pre-employment reference check runs against. A candidate who did not supply their own
referees through the careers site has none, and no HR user can put one in.

Unlike the Professional profile card, these tabs carry no on-screen note explaining that they are
candidate-supplied. *Impact: high.*

**G-7.2 — email uniqueness is enforced on create only, and by nothing else.** `CreateAsync` rejects
a duplicate; `UpdateAsync` performs no such check. And `IX_JobCandidate_Email` is **not unique** —
it is a plain lookup index — so the database will not catch it either.

Two candidates can therefore end up sharing an email address by editing one of them, and the
duplicate-check the list page recommends before creating a record will then return whichever row
the query happens to hit first. The careers-site account link (`UserId`) *is* uniquely indexed, so
account adoption stays safe; it is the plain email that is unguarded. *Impact: medium.*

**G-7.3 — `Nationality` is displayed and cannot be set.** The field exists on the entity and is
rendered on the Personal card. It appears on the read DTO only — no create DTO, no update DTO, no
form field, and no candidate-portal path writes it. The **only** assignment anywhere in the
solution is in `TdcDemoRecruitmentHistorySeeder`, which sets `"Ghanaian"`.

So on a real tenant the Nationality row always reads "—", and on the demo tenant it always reads
"Ghanaian". This is a good illustration of demo data masking a gap: the field looks populated in
every walkthrough and is unreachable in production. *Impact: low-medium.*

**G-7.4 — two superseded talent-pool endpoints and their client methods have no caller.**
`POST /{id}/add-to-talent-pool` and `POST /{id}/remove-from-talent-pool` remain on the controller,
and `jobCandidateService.addToTalentPool` / `removeFromTalentPool` remain in the client, but the
UI now uses the richer `talentPoolService` endpoints instead. The old pair set `IsInTalentPool`
without a source, reason or review date — exactly the data loss the replacement was written to
stop — so leaving a live door onto them is worth noting. *Impact: low.*

**G-7.5 — the "HR-only" claim in the page comment overstates the gate.** Reads are gated on
`RecruitmentRead`, not the HR role, so `TenantAdmin`, `Admin` and `HR User` can read every
candidate record including date of birth, identity numbers and recruiter notes. Whether that is
wrong depends on the tenant's role design, but the comment describes a restriction the code does
not implement. *Impact: low — noted because the data is sensitive.*

**G-7.6 — no name search.** The only lookup is exact email. There is no name search, no phone
search, no filter by talent-pool status, and no sort. Finding a candidate whose email you do not
know means paging 20 at a time through the whole register. Several indexes exist that a search
could use (`IX_JobCandidate_TalentPool`, `_TalentPoolStatus`, `_LastEngagedDate`,
`_TalentPoolReviewDate`) and none of them is reachable from this screen. *Impact: medium, and it
grows with the size of the candidate base.*

---

## 8. `/hr/recruitment/applications` — one person's pursuit of one vacancy

**Files:** `page.tsx` (251), `[id]/page.tsx` (781)
**Key components:** `ApplicationDecisionBar` (254), `RecordApplicationDialog` (225),
`ApplicationSourceDialog`, `ApplicationReviewsPanel` (196)
**Walked:** 2026-09-15

### 8.1 What an application is

The join between a candidate and a vacancy — `JobApplications`, with `JobVacancyId` and
`JobCandidateId` both required. Everything about the *pursuit* lives here; everything about the
*person* lives on the candidate (§ 7.1).

The list page states its own scope well: *"Every application across all vacancies. Work a single
vacancy from its pipeline board instead."* This screen is the cross-vacancy register; the real
day-to-day work happens on `/vacancies/[id]/pipeline` and `/screening`.

### 8.2 The list

**Two filters that combine properly**, which is more than the earlier registers manage. Both the
paged read and the by-status read accept a `vacancyId`, so vacancy + status is a real
server-side intersection rather than a client-side one.

| Selection | Endpoint | Paged? |
|---|---|---|
| any status = "Any" | `GET /api/job-applications?pageNumber&pageSize&vacancyId` | yes |
| a specific status | `GET /api/job-applications/status/{status}?vacancyId` | **no** |

**And it says so.** When a status is selected the page prints: *"Filtering by status uses an
unpaged endpoint — all N matching applications are shown."* That is the same underlying shape as
G-4.6 and G-5.8 on the requisition and vacancy lists, but here it is disclosed rather than silent,
and the page comment explains the reasoning: *"pretending otherwise would mean filtering a single
page client-side and reporting the wrong totals."* If those two earlier gaps are ever addressed,
this is the pattern to copy.

**Columns:** Number, Candidate (name over email), Vacancy, Applied, Status, Stage, Score. A stale
score is greyed and suffixed with `*`, as on the pipeline board.

**Record an application** is gated on `hasAnyPermission(['HR.Recruitment.Write', 'HR.Recruitment.Admin'])`
— a permission check, not a role check. Correct, and worth contrasting with the decision bar
(G-8.2).

### 8.3 Recording an application by hand

`RecordApplicationDialog` covers walk-ins, agency submissions and referrals — applications that
did not arrive through the careers site. It asks for the vacancy, the candidate **by exact email
lookup**, how they arrived (`ApplicationSource`), which advert they came through (picked from that
vacancy's own postings), years of experience, availability and a cover letter.

The candidate must already exist: the dialog resolves one by email and offers no way to create one
inline. With no name search anywhere (G-7.6), recording a walk-in means either knowing their email
address or leaving to create the candidate first (G-8.5).

### 8.4 The detail page

**Four tiles:** Status, Auto score (amber with *"Stale — re-score before relying on it"* when
stale), Panel score (`AggregatedReviewScore`, hinted *"Finalized reviews only"*), Current stage.

**Header actions:** *Re-score*, and then either a link to the existing offer (showing its number
and status) or an **Extend an offer** button. The page comment is candid about the gate: *"Raising
an offer is not gated by application status server-side — only that the application resolves to a
vacancy and a position — so this is offered whenever nothing has been raised yet."* See G-8.4.

**The decision bar** sits above the tabs: Shortlist (or Un-shortlist), Move stage, Waitlist,
Reject, Withdrawn. Its own comment states the design clearly and correctly:

> ⚠ **The server owns every rule here.** Terminal statuses are dimmed as a courtesy, but the real
> refusals — a passed shortlisting deadline, an already-shortlisted application, a stage at its
> attempt limit — come back as 422 with the rule's own sentence, which is what gets shown. Nothing
> is mirrored client-side.

Reject and Withdrawn require a reason; the other three do not. The dialogs are unusually
informative — *"Shortlisting also resets any shortlist already sent for approval, so the approver
reviews the full list again"*, *"A hold, not a rejection — the candidate stays in contention if
someone drops out"*, *"Use when the candidate has withdrawn. This is not a rejection."*

**Seven tabs:**

| Tab | Content |
|---|---|
| Overview | Application card (applied, source with a *Correct* link, advert, experience, availability, internal flag), Candidate card (linked), Decision card (shortlisted / waitlisted / rejected / withdrawn, each with date, actor and reason), Cover letter |
| Score breakdown | the stored JSON breakdown per criterion — name, mandatory, weight, raw, weighted, pass/fail, notes |
| Stage history | `JobApplicationStageHistory` — stage, type, entered, left, exit reason, moved by; the current stage badged |
| Panel reviews | `ShortlistReview` rows |
| Tests | `JobApplicantTestResult` — name, type, date, score/max, percentage, pass/fail |
| Communications | `JobApplicantCommunication` — when, type, direction, subject/body, sent by |
| Decision log | `ShortlistDecisionLog` — when, decision, score at decision, whether automatic, by whom, notes |

Three notes on those tabs worth keeping:

- **The Score breakdown is a stored artefact, not a recomputation.** It is parsed from
  `AutoScoreBreakdown`, the JSON written at scoring time, and it records *every* criterion —
  including the ones evaluated after a mandatory failure, so the reason for a zero is legible.
- **The Communications tab is honest about what it is:** *"This records a communication — it does
  not send one. Candidate emails go out through the vacancy's shortlist and rejection notification
  actions."* It is a log, not an outbox.
- **The Decision log is described as immutable** — *"Shortlisting, waitlisting and rejection each
  write an immutable entry here"* — and carries `AutoScoreAtDecision`, so a later re-score does not
  rewrite what the decision was made on.

**Correcting the source** (`ApplicationSourceDialog`) sets `Source` and `JobPostingId` together,
and the server validates that the advert belongs to *this* vacancy and has not been Removed:
*"That advert does not belong to this vacancy, or has been removed."*

**Recording a test result** warns that *"Name, type, date and venue cannot be amended afterwards —
only the marks."*

### 8.5 The stage-transition engine

`MoveApplicationToStageAsync` is the most heavily ruled operation in the module. Six checks, in
order:

1. **Status** — a Hired or Withdrawn application cannot be moved at all.
2. **Pipeline membership** — the target stage must belong to the vacancy's own pipeline, and the
   vacancy must have one.
3. **Not already there** — *"The application is already in the target stage."*
4. **`CanRepeat`** — moving backward, or to a stage of equal order, is refused unless the target
   stage permits repeat entries.
5. **`CanSkip` and `IsRequired`** — moving *forward* past an active, never-visited stage that
   cannot be skipped is refused by name; and the **final** stage refuses an application that never
   entered a required stage, however it got past it.
6. **`MaxAttempts`** — counts *all* visits to the target stage, including closed ones.

Rule 5 is a repaired dead promise worth recording: the interface had documented *"a stage may only
be skipped when CanSkip"* since the module shipped, and **nothing read the flag** until it was
wired. That is the same shape as the gaps in Appendix C's second pattern, caught and fixed.

The write itself is three operations in one `SaveChangesAsync` — close the current history row
(`IsCurrent = false`, `ExitedAt`, `ExitReason = Progressed`), open the new one, and update the
application — relying on SQL Server's implicit transaction rather than an explicit one, which
would be incompatible with the retrying execution strategy.

### 8.6 Where an application's status comes from — three writers, one column

This is the most important thing to understand about `JobApplications.Status`, and it is not
stated on any screen.

| Writer | Sets |
|---|---|
| The decision actions | Shortlisted, Waitlisted, Rejected, Withdrawn; un-shortlist returns it to UnderReview |
| Auto-shortlist (§ 5.7) | Shortlisted |
| **A stage move** | whatever `MapStageTypeToStatus` says for the target stage's type |

That last one **overwrites** the others. The mapping is:

| Stage type | Status it forces |
|---|---|
| ApplicationReview, Screening, HiringManagerReview | UnderReview |
| Assessment | AssessmentPending |
| Interview | InterviewScheduled |
| PreEmploymentCheck | PreEmploymentCheck |
| Offer | OfferExtended |
| Hired | Hired |

So moving a **Shortlisted** application into a Screening stage silently returns its status to
**UnderReview**. Nothing warns about this, and the decision that produced the Shortlisted status is
preserved only in `ShortlistedDate` and the decision log — which is precisely why G-8.1 bites.

### 8.7 Known gaps

> **✅ All five closed, 2026-09-15 → 16 — one of them deliberately narrowed.** Read as history.
>
> | | Now |
> |---|---|
> | G-8.1 | One definition, and it is `ShortlistedDate.HasValue` on both sides. Being shortlisted is an event that happened; the process moving on does not un-happen it. |
> | G-8.2 | The decision bar renders behind `hasAnyPermission`, as *Record an application* on the same page already did. |
> | G-8.3 | The shortlist case is now harmless, because G-8.1 made both sides read the date rather than the status. A move against a **terminal** application (Rejected, Withdrawn, Hired) is refused, guarded before anything is written. |
> | G-8.4 | ⚠ **Narrowed on purpose.** The flexibility is kept — a search-firm hire legitimately skips the funnel, and a rule that refuses real work is a worse defect. Only the unambiguous case is refused: an offer against a Rejected, Withdrawn or Hired application. |
> | G-8.5 | The walk-in dialog searches by name, email or phone, lists multiple matches to pick from, and offers a create link in a new tab so the half-filled dialog survives. |

**G-8.1 — `IsShortlisted` is derived two different ways and the two disagree.** The flag is not a
stored column; it is computed at mapping time, and there are two computations:

| Where | Expression | Used by |
|---|---|---|
| `RecruitmentMappingExtensions` (×2) | `entity.ShortlistedDate.HasValue` | every HR-side view, including this page's decision bar |
| `JobApplicationService.ToMyApplicationDto` | `entity.Status == ApplicationStatus.Shortlisted` | the employee's own *My applications* view |

They agree only while the status is still Shortlisted. The moment a stage move overwrites the
status (§ 8.6) — or any other status change lands — `ShortlistedDate` still holds a value while
`Status` no longer says Shortlisted. From then on **HR sees the candidate as shortlisted and the
candidate sees themselves as not.**

The decision bar keys off the HR-side flag, so it keeps offering *Un-shortlist* for an application
whose status has moved on, which is arguably right; the divergence is what the internal applicant
is told. *Impact: medium.*

**G-8.2 — the decision bar has no permission gate.** Every one of Shortlist, Move stage, Waitlist,
Reject and Withdrawn renders for any viewer who can load the page. Only terminal statuses dim
them. The server refuses with 403 for anyone lacking `RecruitmentWrite`, so nothing unsafe happens
— but the same page gates *Record an application* on `hasAnyPermission`, and the list page does
too. The inconsistency is within one feature. *Impact: low-medium.*

**G-8.3 — a stage move silently rewrites the decision status.** As § 8.6 sets out, two systems
write one column and the stage move always wins. A recruiter who shortlists a candidate and then
moves them into a review stage will find the shortlist decision no longer reflected in the status,
with no warning at the point of the move and no note on the stage-move dialog (which otherwise
explains its rules well). *Impact: medium.*

**G-8.4 — an offer can be raised on an application at any non-terminal status.** The page's own
comment records that the server gates offer creation only on the application resolving to a
vacancy and a position — not on status. So *Extend an offer* appears on a brand-new, unscored,
un-interviewed application, and raising one there succeeds. Every other step in this module gates
on the step before it; this one does not. *Impact: medium — to be confirmed against the offers
chapter.*

**G-8.5 — recording a walk-in requires the candidate to exist and to be found by exact email.**
`RecordApplicationDialog` resolves a candidate only by exact email and offers no inline create. A
walk-in whose email you do not have cannot be recorded without leaving the dialog, creating the
candidate, and starting again. Compounds G-7.6. *Impact: low-medium.*

**Refining G-6.1.** Chapter 6 recorded that a posting's `ApplicationCount` is always zero and that
the linking data was only half present. This chapter corrects the second half: `JobPostingId` **is**
settable — automatically by the candidate portal, and manually by HR through the source-correction
dialog, validated against the vacancy's own adverts. So per-advert attribution data does exist.
What is still missing is any code that counts it: `UpdateSourceAsync` writes the link and touches
no counter, and nothing else increments `JobPosting.ApplicationCount` either. The number is
derivable and never derived.

**Worth crediting.** A repaired defect recorded in the controller: every workflow action now takes
its subject from the route and **overwrites whatever id the body carried**. Previously a
`POST /{A}/reject` carrying `{"applicationId": B}` rejected B while the audit trail, the URL and
the client all said A. Two endpoints already did this correctly; the rest did not.

---

## 9. `/hr/recruitment/interviews` — the sessions and their scorecards

**Files:** `page.tsx` (240), `new/page.tsx` (396), `[id]/page.tsx` (388),
`[id]/score/[intervieweeId]/page.tsx` (524)
**Walked:** 2026-09-15

### 9.1 What an interview is

**A session, not an appointment.** One `JobInterviews` row holds a date, a start and end time, a
round number, a type and a mode — and then *many* candidates and *many* panelists hang off it:

```
        JobInterview  (one session: date, 09:00–11:00, Round 1, Panel, InPerson)
              │
      ┌───────┴────────┬──────────────────────┐
      ▼                ▼                      ▼
 JobInterviewee   JobInterviewPanelist   JobInterviewQuestion  (a "plan": one per section)
 (one per          (an employee)              │
  application)                                ▼
      │           JobInterviewExternal   JobInterviewSelectedQuestion
      │            Panelist                (the drawn questions)
      ▼
 JobInterviewScoreSummary  ── one per (candidate × panelist) ──┐
      │                                                        │
      ▼                                                        ▼
 JobInterviewScoreEntry (one per question)         JobInterviewScoreDraft (private, per panelist)
```

So the scorecard grid is **candidates × panelists**: five candidates seen by three panelists is
fifteen `JobInterviewScoreSummary` rows.

**An interview is opened from a vacancy**, never standalone, and the candidates bookable into it
are that vacancy's applicants — *"an application for another role is refused."*

### 9.2 A different authorization model — read this before anything else

Every other recruitment controller gates on permissions with `[Authorize(Policy = ...)]`
attributes. **This one has almost none.** Only the class-level `InternalOnly` and a
`[RecruitmentBusinessRules]` filter. The reasoning is set out in the controller's own summary and
is worth quoting, because it is the best-argued design decision in the module:

> **Authorization is enforced in the service, not by role attributes here.** … An interview cannot
> be HR-only: the people who have to open it, read the questions and file a scorecard are ordinary
> employees who happen to sit on that panel. Nor can it be open to any authenticated employee —
> which is what the bare `[Authorize]` here used to mean — because it carries the candidate's
> contact details, the panel's private comments and the hire recommendation. The rule is therefore
> per record ("HR, or a panelist on *this* interview").

Three guards implement it in `JobInterviewService`:

| Guard | Rule |
|---|---|
| `EnsureCanReadInterviewAsync` | HR, **or** the caller holds a panelist row on this interview |
| `EnsureHr(action)` | HR only — scheduling, the panel, the question plan, closing, removing a candidate |
| `EnsureCanScoreAsAsync` | you may file a scorecard **only as yourself**; HR may file on an external assessor's behalf; and the panelist must sit on the same interview as the candidate |

That last guard is a repaired hole worth recording: *"The client used to name the panelist in the
payload (or the query string, for drafts), so any authenticated user could submit — or read — a
scorecard in someone else's name."*

**But `IsHr` here means the role, not the permission** — `Constants.Roles.Hr` or `SuperAdmin`, and
nothing else. That is the opposite convention from everywhere else in recruitment, and G-9.1 is
about what it costs.

**Two anonymous endpoints** sit at the bottom of the controller: `confirm-panelist/{token}` and
`confirm-attendance/{token}`. They are reached from an emailed link by candidates and external
panelists who have no login at all, authorised by a single-use token and rate-limited.

### 9.3 `/hr/recruitment/interviews` — the schedule

**No paged search.** The page comment explains: *"There is no general paged search on this
controller — only purpose-built reads (by date range, by status, by vacancy, by round). The date
range is the default because a schedule is a diary, and `from`/`to` are required by the API rather
than optional."*

Two view modes: **By date** (default, −7 to +30 days, with a *Next two weeks* shortcut) and **By
status**.

Columns: Reference (number, with round and type beneath), Role, When (date over time range),
Candidates (comma-joined names), Panel (comma-joined names), Status.

A *My panel* button links to `/me/panel`. The page comment is blunt about why: *"A panelist gets a
403 from every read on this page; their sessions are at `/me/panel`."* The list reads are
`GetByDateRange` and `GetByStatus`, neither of which is scoped to the caller's panel seats.

### 9.4 `/hr/recruitment/interviews/new` — scheduling

Zod-validated, with a cross-field rule: *"The session must end after it starts."* Accepts
`?vacancyId=` to preselect.

| Card | Contents |
|---|---|
| The session | Vacancy\*, Round (1–20), Format (`JobInterviewType`), Mode (`InterviewMode`), date, start, end, location or link, instructions for the candidate |
| Candidates | checkbox list of that vacancy's applications, showing each one's status |
| Panel | `PanelMemberPicker` (employees **and** external associates) plus `PanelAvailabilityPanel`, which checks the chosen panel against the chosen slot |
| Questions | an optional preset |

Three behaviours the form states plainly and which are true of the server:

- **Booking a candidate advances them.** *"N candidates will be booked in and moved to the
  interview stage."* `AddIntervieweeAsync` calls `AutoAdvanceToStageTypeAsync(…, Interview, …)`,
  and so does the create path — so the pipeline stays in step, and the application's status becomes
  `InterviewScheduled` through the § 8.6 mapping.
- **A preset does two things in one call** — scaffolds a question plan per section and draws the
  questions from the bank. Both are editable afterwards.
- **Invitations are not sent automatically.** The success toast says so: *"Invitations are not sent
  automatically — send them from the interview."* (`POST /{id}/send-invites`.)

One implementation note: `<input type="time">` yields `HH:mm` and the API takes a `TimeSpan`, so
the form appends `:00`.

### 9.5 `/hr/recruitment/interviews/[id]` — the session

**Serves two audiences.** Nearly everything is conditioned on `canManage`, and the page says why:
*"the buttons are hidden to avoid offering an action that would come back a 403, not as the
security boundary."*

Header actions (HR, non-terminal only): **Reschedule**, **Cancel**, **Close interview**. A
cancelled interview instead offers **Delete**.

**Rescheduling is not a quiet edit.** The dialog warns: *"Every candidate is re-invited and issued
a new confirmation link — the old one stops working, and any attendance they had already confirmed
is cleared."* The session then permanently shows *"Moved from {date} — {reason}"*.

**Closing has a state guard** that was once missing: *"Completion had no state guard at all, so a
cancelled interview could be marked complete — reviving a session nobody attended, and with it
every downstream read that keys off Completed."*

Four tabs: **Candidates** (`JobInterviewee` — slot times, attendance, outcome, and the route into
each scorecard), **Panel** (internal and external panelists, confirmations, attendance),
**Questions** (the plans and their drawn questions), **Scorecards** (the grid).

### 9.6 The scorecard — `[id]/score/[intervieweeId]`

One panelist's card for one candidate. Whose card it is comes from `?panelistId=` when HR arrives
from the candidate list, otherwise from the caller's own panel seat — and the page is explicit that
this *"only decides what to load"*, because the server refuses anything else.

If the caller has no panel seat the form does not render at all, with different copy for HR and
for everyone else: *"HR can record on an external assessor's behalf, but a scorecard always belongs
to one named panelist."*

**Three save paths:**

| Action | Endpoint | Visibility |
|---|---|---|
| Save draft | `JobInterviewScoreDraft` | *"Only you can see it."* |
| Save scorecard | `JobInterviewScoreSummary` + entries | visible on the interview |
| Sign off | saves, then `finalizeScore` | **immutable** — *"It can no longer be changed."* |

The draft is deleted when a card is signed off, and the seeding logic handles that correctly: *"a
submitted card wins over a draft, because the draft is deleted when a card is signed off and a
lingering one would otherwise overwrite the real marks."*

**The running total is computed twice, on purpose.** This is the single best piece of engineering
judgement in the module, and the comment deserves quoting in full:

> **The running total is computed here with the same arithmetic the server uses** —
> `(mark ÷ top of band) × weight` — deliberately duplicated rather than waiting for the server's
> number. Two independent computations make a disagreement visible instead of silent, which is the
> lesson from the appraisal scoring model. If the total shown here ever differs from the one that
> comes back on save, that is a bug worth chasing, not a rounding artefact.

And it acts on it: on save, if the two differ by more than 0.01, the toast reads *"Scores saved,
but the totals disagree — this screen computed X; the server recorded Y. Please report this."*

Two smaller details of the same quality: marks are **held as strings until submit** so a half-typed
value does not read as a real 0, and out-of-band marks are detected against each question's own
`minScore`/`maxScore`.

**The finalisation gate** is mirrored client-side so the panelist knows before pressing, and
enforced server-side in `EnsureScorecardCoversRequiredQuestionsAsync`: every section must have at
least `min(RequiredQuestionCount, selectedQuestions.Count)` scored questions. The `Math.Min` is
deliberate — *"Never demand more than the bank could actually supply — that would make the
scorecard unfinalisable through no fault of the panelist."* An interview with no question plans at
all is unstructured and requires nothing.

### 9.7 Two scoring systems, easily confused

Recruitment has **two independent panel-scoring mechanisms**, and they do not meet:

| | Shortlist reviews | Interview scorecards |
|---|---|---|
| Entity | `ShortlistReview` | `JobInterviewScoreSummary` + `JobInterviewScoreEntry` |
| Scored against | the vacancy's shortlisting criteria | the interview's drawn questions |
| When | before interviews, on the screening screen | at the interview |
| Rolls up to | **`JobApplication.AggregatedReviewScore`** | nothing outside the interview |

So the **"Panel score"** tile on the application detail page (§ 8.4) is the *shortlisting* panel's
average, not the interview panel's. An interview scorecard, however carefully filled in, never
reaches the application record. See G-9.5.

### 9.8 Known gaps

> **✅ All six closed, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-9.1 | `IsHr` asks the permission question (`HrPermissions.RolesGrantAny`). No role gains access it did not already hold elsewhere in the module. |
> | G-9.2 | The panel's verdict finally has a reader: closing an interview uses it to decide where each application goes. |
> | G-9.3 | Recording the verdict is an `EnsureHr` write, like every other write on the interview record itself. |
> | G-9.4 | `CompleteAsync` advances each attendee's application. ⚠ It **never rejects** — that carries a reason and a notification, and stays HR's act through the decision bar. `ProceedToNextRound`, `OnHold` and a missing verdict move nothing. |
> | G-9.5 | Relabelled *"Shortlisting panel score"*, with the hint saying "not the interview panel". |
> | G-9.6 | HR's diary and `/me/panel` now link both ways. A merged screen was considered and rejected — the two lists carry different authority. |

**G-9.1 — the interview area uses roles where the rest of the module uses permissions.** `IsHr` is
`HasRole("HR") || HasRole("SuperAdmin")`. Every other recruitment controller authorises on
`HR.Recruitment.Read/Write/Admin`, which are granted to `SuperAdmin`, `TenantAdmin`, `Admin`, `HR`
**and `HR User`**.

The consequence is the inverse of G-2.3. There, the page hid the module from roles the API would
serve. Here, the **API itself** refuses `TenantAdmin`, `Admin` and `HR User` — they can raise a
requisition, open a vacancy, shortlist and reject, but cannot schedule an interview, manage a
panel, edit a question plan or close a session. Unless they happen to sit on the panel, they cannot
even *read* one, because `EnsureCanReadInterviewAsync` falls through to the panelist check.

That is arguably the right restriction for such sensitive data, but it is undocumented and it is
the only place in recruitment where holding `HR.Recruitment.Admin` is not enough. *Impact: medium.*

**G-9.2 — the panel's verdict on a candidate is written and never read.**
`JobInterviewee.Outcome` (`JobInterviewOutcome`) is set by `RecordOutcomeAsync` and appears in the
mapping extensions so it renders on screen. **No service reads it.** It does not change the
application's status, does not gate whether an offer can be raised, does not appear in recruitment
analytics, and does not feed the hire decision.

So the question "did this candidate pass their interview?" is answered on the interview screen and
nowhere else in the system. *Impact: medium — the same shape as G-3.5 (a classification nothing
consumes), on the module's central hiring judgement.*

**G-9.3 — any panelist can record the panel's verdict.** `RecordOutcomeAsync` guards with
`EnsureCanReadInterviewAsync` — *read* access — not `EnsureHr`. So one member of a five-person
panel can set the outcome for a candidate, overwrite one another's, and do so without having
signed off their own scorecard. Every other write on the interview record itself is HR-only. There
is one genuine guard: an outcome cannot be recorded for a candidate marked as a no-show.
*Impact: low-medium.*

**G-9.4 — closing an interview has no downstream effect.** `CompleteAsync` sets
`Status = Completed` and saves. Nothing else happens: no application status moves, no pipeline
stage advances, no notification goes out, no scorecard is chased. Contrast with *booking* a
candidate, which does auto-advance the pipeline stage. So the session ends and the applications
sit wherever the booking left them — `InterviewScheduled` — until somebody moves each one by hand.
*Impact: medium.*

**G-9.5 — "Panel score" on the application page is not the interview panel's score.** As § 9.7
sets out, `AggregatedReviewScore` is the average of finalised `ShortlistReview` rows — the
screening panel. The tile is hinted *"Finalized reviews only"*, which is accurate but does not say
*which* reviews. A recruiter reading an application after interviews have run will reasonably read
that tile as the interview verdict. *Impact: low — a labelling problem over a real structural
separation.*

**G-9.6 — the interview list cannot be scoped to the caller.** Both list reads are org-wide, so a
panelist gets 403s rather than their own diary, and the page has to signpost `/me/panel` instead.
`GET /me/panelist-slots` exists and returns exactly the right thing; this screen simply is not the
place it is used. Not a defect so much as a consequence of the schedule being HR's diary, but it
means there is no single screen showing *"interviews I am involved in, as HR or as a panelist"*.
*Impact: low.*

**Worth crediting.** Beyond the duplicated-arithmetic guard: the finalisation gate's `Math.Min`
so a scorecard can never become unfinalisable; the reschedule flow invalidating old confirmation
tokens and clearing confirmed attendance; the completion state guard; and `EnsureCanScoreAsAsync`
closing a hole where any authenticated user could file or read a scorecard in someone else's name.

---

## 10. `/hr/recruitment/offers` — the terms

**Files:** `page.tsx` (157), `new/page.tsx` (375), `[id]/page.tsx` (660), `[id]/edit/page.tsx` (353)
**Walked:** 2026-09-15

### 10.1 What an offer is

Terms raised against **an application** — `JobOffer.JobApplicationId`. Like a vacancy, it is never
created standalone: the list's empty state says *"Offers are raised from an application — open one
and use 'Extend an offer'."*

**Two things make this entity unusual in the module.**

**It is versioned.** `Version`, `IsLatestVersion` and `PreviousOfferId` form a chain. Revising an
offer after a counter-offer creates a *new* `JobOffers` row at v+1 and marks the old one
superseded. The list shows `v2` beside the number.

**Half of it is server-owned.** The overview card is titled, in the UI itself, *"The role —
server-owned, taken from the vacancy"*. `PositionId`, `PositionTitle`, `ReportsToTitle`,
`GradeTitle`, `DepartmentName`, `SalaryGradeMin/Max`, `WorkMode` and `EmploymentType` are all
copied from the position and vacancy at creation and **cannot be supplied by the caller**. The
comment explains the history: those fields used to be on the payload, *"required of the caller and
then always discarded"*, which masked a case where an unresolved vacancy would write
`PositionId = Guid.Empty` and die on a foreign-key violation with nothing to explain it. Now the
service refuses explicitly instead.

What the caller *does* supply is the negotiated part: base salary, bonus, commission, start date,
contract length, probation and notice overrides, leave, NDA, conditionality, additional terms.

### 10.2 The lifecycle

Thirteen statuses (§ 1.4), and the path forks on one flag — `IsConditional`.

```
  Draft ──submit──▶ PendingApproval ──approve──▶ Approved ──issue──▶ Sent
    ▲                     │                                            │
    │                  reject                                          ├─ Declined
    └── Rejected ◀────────┘                                            ├─ Negotiating ──revise──▶ (new v2 Draft)
                                                                       │
                          ┌────────────────────────────────────────────┤
                          ▼                                            ▼
              NOT conditional:  Accepted            conditional: ConditionallyAccepted
                          │                                            │
                          │                              pre-employment checks complete,
                          │                              no blocking failures
                          │                                            ▼
                          │                                      ChecksCleared
                          └──────────────┬─────────────────────────────┘
                                         ▼
                                  JobHireRecord
```

`Revoke` is reachable from anything not in `UNREVOKABLE_OFFER_STATUSES` and lands on `Withdrawn`.
`Expired` is never written at all (G-2.4).

**Issuing does three things**, and this is the module's best hand-over between steps:

1. sets `Sent`, stamps `OfferDate`, applies the expiry;
2. mints an `OfferCandidateToken` (a `Guid`, with `ExpiresAt` defaulting to the offer expiry or
   +14 days) and **emails the candidate their response link**;
3. **advances the pipeline** to the Offer stage type.

Compare G-9.4, where closing an interview does none of that.

### 10.3 `/hr/recruitment/offers` — the list

One dropdown with three kinds of view: **All offers**, **Expiring within 7 days**, and each of the
thirteen statuses. Each runs its own purpose-built read — the page comment notes there is *"no
general paged search on this controller"*.

Columns: Offer (number, with `v2` when versioned), Candidate, Role (title over employment type),
Salary, Start date, Expires, Status.

**"All offers" is both unpaged and unfiltered** — see G-10.3.

### 10.4 `/hr/recruitment/offers/new`

Reached with `?applicationId=`. Collects only the negotiated terms; everything about the role is
taken server-side (§ 10.1). Two server rules apply at creation:

- **The salary must sit within the grade band.** `EnsureSalaryWithinBand` refuses a base salary
  outside `SalaryGradeMin`–`SalaryGradeMax`, naming both. It deliberately no-ops when the band is
  unset or configured back-to-front, *"Either way it constrains nothing"* (G-10.5).
- **Benefits are seeded from the position's *effective* benefits.** This includes benefits reaching
  the post through a **benefit group**, not just its individual rows. The comment records why that
  matters: previously *"an offer for a post whose benefits came through a BENEFIT GROUP would have
  listed none of them — the candidate would have been sent an offer letter missing most of the
  package."*

**Nothing gates creation on the application's status** — only that the application resolves to a
vacancy and a position. This confirms G-8.4.

### 10.5 `/hr/recruitment/offers/[id]`

The header is entirely status-driven:

| Action | Available when |
|---|---|
| Edit | Draft or PendingApproval |
| Submit / Approve / Reject (workflow engine) | Draft/Rejected to submit; PendingApproval to decide |
| Issue to candidate | Approved |
| Record response | Sent |
| Accept conditionally | Sent or Negotiating |
| Revise | Sent or Negotiating |
| Revoke | any status not in `UNREVOKABLE_OFFER_STATUSES` |
| Delete | Draft only (and needs `RecruitmentAdmin`) |
| Create hire record | conditional → `ChecksCleared`; otherwise `Accepted` or `ChecksCleared` |

Once a hire record exists the button becomes a link to it — *"rather than letting create-hire be
tried twice."*

**Six tabs:** Overview (four info cards — the role, compensation, dates and terms, additional
terms), Benefits, Pre-employment checks, Letter, Notes, Workflow.

**Revising** clones every term forward, applies the three overridable fields
(`NewBaseSalary`, `NewProposedStartDate`, `NewAdditionalTerms`), carries the benefits across, and
**re-checks the salary band** — *"A revision exists to change the money after a counter-offer, so
this is precisely the path that must respect the band — and it was the one path that never
checked."* Conditionality and weekly hours are carried forward deliberately and are not
negotiable on a revision.

### 10.6 `/hr/recruitment/offers/[id]/edit`

Editable only while Draft or PendingApproval, on both client and server. Same negotiated-terms
form; the server-owned role fields are not on it.

### 10.7 Three details worth knowing

**The offer number counts soft-deleted rows.** `GenerateOfferNumberAsync` uses an
including-deleted query, and the comment explains exactly why — `JobOffers` carries a unique index
on `(TenantId, OfferNumber)` that a soft delete does **not** release, so excluding deleted rows
made *"the very next create die on a duplicate-key violation, surfaced as a 500 with raw SQL in
it. A reference number is an identifier, not a slot — once issued it is spent."* This is the
concrete case behind the general warning in *Conventions*, and it contrasts with
`IX_JobCandidate_UserId` (§ 7.7), which *is* filtered.

**The hire gate is two rules, both repaired.** A conditional offer must be at `ChecksCleared` —
previously `Accepted` was also allowed, *"so a conditional offer could be hired the moment the
candidate said yes — while its message told the reader the opposite."* A non-conditional offer
must be `Accepted` or `ChecksCleared`; *"hiring against a draft, a withdrawn or a declined offer
was never intended and nothing prevented it."* And there is now one hire per application, which
nothing enforced before: *"a repeated create left two hire records racing to become the same
person's employment."*

**Pre-employment checks distinguish three things a simpler rule conflated.** Completing a check
set refuses while any mandatory or blocking item is still `Pending`/`Requested` — *"'Not yet done'
is not 'failed'"* — because `Failed` is terminal with no reopen, and an offer could then never
reach `ChecksCleared`. And `Waived` or `NotApplicable` items do not count as blocking failures:
*"Waiving a check is a decision to accept it, not a failure — otherwise the waiver facility
guarantees the outcome it exists to avoid."*

### 10.8 Known gaps

> **✅ All five closed, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-10.1 | Submit lands at `PendingApproval` when no definition is published — a status nothing could previously produce, so Approve was itself unreachable. The offer also gained the segregation-of-duties check it never had: you cannot approve terms you prepared. |
> | G-10.2 | `ReviseOfferAsync` moves the superseded version to the new `JobOfferStatus.Superseded` — distinct from `Withdrawn`, which is the organisation taking live terms back. |
> | G-10.3 | `GET /api/job-offers/paged` plus a pager. The status and expiring views keep their unpaged reads and now disclose it. |
> | G-10.4 | Recall wired with G-4.3 — reachable only because Submit now stops at `PendingApproval`. |
> | G-10.5 | The screen says when no band applies: *"No band on this position — the salary was not checked against one"*. The band was on the DTO all along; silence read as "checked and fine". |

**G-10.1 — Submit approves the offer outright, exactly as it does for requisitions.**
`HrJobOfferWorkflowStatusAdapters` maps `WorkflowOutcome.Approved` to
`OfferStatus = Approved` *and stamps `ApprovedDate`*. As established in G-4.1,
`WorkflowIntegrationService.SubmitAsync` returns that outcome whenever no active workflow
definition exists for the entity type.

> ⚠ **This said "and no `JobOffer` definition is seeded anywhere in the solution". False** — a
> `JOB_OFFER` definition is seeded, published and active (corrected 2026-09-16; see G-4.1). What
> remains true of this finding regardless of any definition: **the offer had no
> segregation-of-duties check at all**, so whoever prepared the terms could approve them. That is
> closed too.

So pressing **Submit** takes an offer Draft → **Approved** in one step, with `ApprovedDate` set and
no approver, which immediately unlocks **Issue to candidate**. Here that matters more than it does
for a requisition: approval is what authorises sending legally-meaningful terms — salary, start
date, notice, probation — to a person outside the organisation.

The page comment makes the same incorrect claim chapter 4 recorded: *"⚠ Inoperable until a
`JobOffer` workflow definition is published and entity-types are re-seeded."* It is not
inoperable; it auto-approves. *Impact: high.*

**G-10.2 — a superseded offer stays in a live status for ever.** `ReviseOfferAsync` sets
`original.IsLatestVersion = false` and **does not change `original.OfferStatus`**. So v1 remains
`Sent` or `Negotiating` indefinitely.

Nothing filters on `IsLatestVersion`. The consequences compound through several screens already
documented:

| Reader | Effect |
|---|---|
| `GET /offers/status/Sent` | superseded versions listed alongside the live one |
| `GET /offers/expiring` | superseded versions counted as chasable |
| The landing page's *Offers expiring soon* tile (G-2.4) | inflated, permanently |
| `GET /offers` (All) | both versions listed — visible, at least, via the `v2` marker |

> **Corrected 2026-09-15 (chapter 14).** This list first included *"Analytics Pending count —
> inflated"*. **It is not.** `RecruitmentAnalyticsService` filters its offer query on
> `o.IsLatestVersion`, with the comment *"latest version only — revisions must not double-count"*.
> The analytics screen is the one reader that gets this right.

Combined with G-2.4 (nothing ever writes `Expired`), every revision permanently adds one more row
to the chase list. *Impact: medium, and it accumulates.*

**G-10.3 — the default list view is unpaged and unfiltered.** `getAll()` returns **every offer in
the tenant** in one response, ordered by creation date. This is worse than G-4.6 and G-5.8, where
the unpaged read only fires when a filter is chosen — here it is what loads when you open the
screen. There is no pager, no total, and no disclosure of the kind the applications list makes
(§ 8.2). *Impact: low now, and the only recruitment list with no bound at all.*

**G-10.4 — offer recall is built and unreachable.** `POST /api/job-offers/{id}/recall` exists, the
adapter handles `WorkflowOutcome.Recalled` by returning the offer to Draft, and
`jobOfferService.recall()` exists in the client. Nothing calls it — `useWorkflowRecord` still has
no recall concept (G-4.3). An offer sent for approval cannot be taken back by the person who
raised it. *Impact: low-medium — the second instance of the identical gap.*

**G-10.5 — the salary band check constrains nothing on most positions.**
`EnsureSalaryWithinBand` returns early when `SalaryGradeMax <= 0` or the band is inverted, which is
correct defensive behaviour. But the band comes from `position.SalaryGrade`, and a position with no
salary grade assigned has no band — so for those posts an offer can carry any base salary at all,
with no warning that the check did not run. Given § 3.2's finding that most positions are not even
established, it is likely that most are ungraded too. *Impact: low-medium — worth measuring on the
live tenant before deciding.*

**Confirming G-8.4.** Offer creation validates only that the application resolves to a vacancy and
a position. There is no check that the candidate was shortlisted, interviewed, or reached any
particular stage — so an offer can legitimately be raised on a brand-new application, and the
*Extend an offer* button on the application page correctly reflects that. Whether that is a defect
or deliberate flexibility is a business question; it is recorded here because it is the one step in
the chain that does not gate on the step before.

**Worth crediting.** The server-authoritative role snapshot; benefits seeded from *effective*
benefits including groups; the offer-number generator's deliberate inclusion of soft-deleted rows;
issuing advancing the pipeline *and* emailing a tokened link; revision re-checking the band; and
the three separate repairs to the hire gate and the pre-employment completion rule. This is the
most thoroughly hardened area of the module.

---

## 11. `/hr/recruitment/pre-employment-checks` — the clearance queue

**File:** `page.tsx` (124)
**Where the work happens:** `components/hr/recruitment/PreEmploymentChecksPanel.tsx` (1,002),
on the offer's *Pre-employment checks* tab
**Walked:** 2026-09-15

### 11.1 What a check is, and what it hangs off

A `PreEmploymentCheck` is the **master record for one hire's clearances**, and its parent is the
**offer** — `JobOfferId`, required. The entity comment gives the reason: checks are *"anchored to
the JobOffer so checks run during the conditional-offer window — before the hire record is
created."*

That is the right anchor. A check exists to decide whether the hire should happen at all, so it
cannot hang off the hire.

**Three levels:**

```
PreEmploymentCheck            one per offer — OverallStatus, coordinator, completed date
   └── PreEmploymentCheckItem      one per clearance — type, provider, status, pass/fail,
        │                          mandatory?, blocking?, evidence document, expected days
        └── ReferenceCheckResponse  only when CheckType = ReferenceCheck — the structured
                                    referee reply: rating, would-rehire, three confirmations
```

Plus two supporting tables: `PreEmploymentCheckTemplate`/`TemplateItem` (a reusable set of checks
for a role category, applied to seed an offer's items) and `PreEmploymentCheckProviderService`
(the panel of providers).

**Nine check types** (`PreEmploymentCheckType`): MedicalExamination, PoliceClearance,
BackgroundCheck, AcademicVerification, ProfessionalLicenceVerification, ReferenceCheck,
CreditCheck, DrugTest, Other.

**Seven item statuses** (`CheckItemStatus`): Pending, Requested, Received, Verified, Failed,
Waived, NotApplicable. The last two matter — see § 11.4.

### 11.2 The queue screen

Deliberately thin, and its purpose is stated well: *"the queue HR chases outstanding clearances
from, rather than opening every conditional offer to see what's stuck."*

One status dropdown, one table: Candidate, Offer (linked), Coordinator, Progress
(`completedItems / totalItems`), Failed, Status. Clicking any row goes to the **offer**, not to a
check detail page — there isn't one.

Like the overdue-adverts view (§ 6.2), it is **entirely read-only**: it finds what needs chasing
and offers no way to act on it.

And it **defaults to `InProgress`**, a status nothing in the application ever writes — see G-11.1.

### 11.3 Where the work actually happens

The offer's *Pre-employment checks* tab, a 1,002-line panel. It offers:

| Action | Notes |
|---|---|
| Create the check set | `POST /api/pre-employment-checks` with `items: []` — **manual**, never automatic |
| Apply a template | seeds items from a `PreEmploymentCheckTemplate`, with an *overwrite existing* option |
| Add a check item | type, name, provider, instructions, expected days, mandatory, blocking |
| Record a result | status, pass/fail, dates, remarks |
| Upload evidence | through the controlled-upload gate |
| Record a reference | the structured referee reply, with its own document upload |
| Complete the check set | applies the rule in § 11.4 |

**Providers are snapshotted, not referenced.** `ServiceProviderName` is a stored string, mirrored
from `ServiceProviderSupplierId` when the provider is a Procurement supplier on file and typed
when it is not. So a supplier renamed later does not rewrite what an old check says.

**Evidence goes through the upload gate.** `DocumentFileUploadRecordId`, `DocumentRecordId`,
`DocumentVersionId` and `DocumentFileName` replaced a caller-supplied `DocumentPath`, which is
retained as legacy *"only so documents stored before the change still resolve; nothing writes it
any more."* The same change was made to `ReferenceCheckResponse`.

### 11.4 The completion rule

Chapter 10 introduced this; here is the whole of it, because it is the gate on `ChecksCleared` and
therefore on every conditional hire.

`CompleteAsync` refuses outright while **any mandatory or blocking item is still `Pending` or
`Requested`**, naming them. The reasoning:

> ⚠ *"Not yet done" is not "failed".* The rule was `IsBlockingOnFail && Passed != true`, which
> treats a blocking item still sitting Pending as a failure — so completing a check while waiting
> on a police report marked the whole thing Failed. Failed is terminal, there is no reopen, and the
> offer could then never reach ChecksCleared. An outstanding item now refuses the completion
> instead, which is recoverable.

Then it decides pass or fail:

```
hasBlockingFailures = any item where
      IsBlockingOnFail
  AND Status is not Waived and not NotApplicable
  AND Passed != true

OverallStatus = hasBlockingFailures ? Failed : Completed
```

The `Waived`/`NotApplicable` exclusion is the second half of the same repair: *"Waiving a check is
a decision to accept it, not a failure — otherwise the waiver facility guarantees the outcome it
exists to avoid."*

**And then the hand-over:** if there were no blocking failures **and** the linked offer is at
`ConditionallyAccepted`, the offer advances to `ChecksCleared`. That is the only code path that
ever writes `ChecksCleared`, and `ChecksCleared` is the only status from which a conditional offer
can become a hire.

### 11.5 Known gaps

> **✅ All four closed, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-11.1 | A check set moves `Pending → InProgress` when its first item is started — written where the transition happens, so every reader agrees. One-way: a set does not become un-started because somebody reset an item. |
> | G-11.2 | `AcceptConditionallyAsync` refuses without a check set, and the message names the way out. The condition in "conditionally accepted" *is* the check set, so accepting without one is not a state worth recording. |
> | G-11.3 | A referee picker on *Record a reference* fills the snapshot fields **and** sets `RefereeId`. Typing over the name clears the link, since the snapshot would then describe someone other than the FK target. |
> | G-11.4 | Rows and offer links deep-link to `?tab=checks`. ⚠ **No bulk action, deliberately** — a clearance result is evidence about a named person, and the defect was the three clicks between finding the work and doing it, not the absence of a bulk control. |

**G-11.1 — the queue's default view can never show anything on a real tenant.**
`PreEmploymentCheck.OverallStatus` is written in exactly three places: set to `Pending` at
creation, and set by `CompleteAsync` to either `Completed` or `Failed`. **Nothing ever writes
`InProgress`, `CompletedWithCaution` or `Waived`.**

The page defaults to `InProgress`, and its comment explains the choice: *"Defaults to In Progress,
since Pending/Completed/Failed/Waived read as 'nothing to chase' or 'already resolved'."* The one
status it picks as the live queue is the one status the application never sets. On a real tenant
this screen opens permanently empty, and three of its six dropdown options can never match a row.

The only writer of those three values anywhere in the solution is
`TdcDemoRecruitmentHistorySeeder`, which assigns them by a modulo — so on the demo tenant the
default view is populated and all six statuses appear. This is the sharpest instance yet of
Appendix C's fourth pattern.

What the screen means to show — a check set that has been started but not finished — is real and
identifiable (`OverallStatus = Pending` with some items past `Pending`); there is simply no status
that records it. *Impact: medium — the screen is inert in production and convincing in a demo.*

**G-11.2 — accepting conditionally with no check set is a silent dead end.**
`AcceptConditionallyAsync` moves the offer to `ConditionallyAccepted` and **sets
`IsConditional = true`** whatever it was before. It does not require a `PreEmploymentCheck` to
exist, and one is never created automatically.

The trap follows from the gates already documented:

- `ChecksCleared` is written *only* by `CompleteAsync`, on a check set anchored to that offer;
- the hire gate refuses a conditional offer that is not at `ChecksCleared` (§ 10.7).

So an offer accepted conditionally with no check set is stuck at `ConditionallyAccepted`
permanently, and **no hire record can ever be created for it** — until somebody notices, opens the
checks tab, creates a set, adds items, records every result and completes it. Nothing on the offer
screen says a check set is required, missing, or that this is why *Create hire record* has not
appeared. *Impact: medium.*

**G-11.3 — a reference is never linked to the referee it came from.**
`ReferenceCheckResponse.RefereeId` is a nullable FK to `JobCandidateReferee`, and it is on the
create DTO. **The UI never sends it** — the *Record a reference* dialog captures the referee as
free text (`refereeName`, `refereeOrganisation`, `refereePosition`, `refereeEmail`,
`refereePhone`) with no picker.

Two consequences. `GET /api/pre-employment-checks/reference-responses/referee/{refereeId}` can
never return anything. And there is no way to answer *"which of this candidate's nominated referees
have we actually contacted?"* — the reply exists, the nomination exists, and nothing joins them.
As with G-11.1, the only place `RefereeId` is populated is the demo seeder.

The snapshot fields themselves are deliberate — the entity says they are *"captured at time of
check in case profile changes"* — but a snapshot is meant to sit *beside* the link, not replace
it. *Impact: low-medium.*

**G-11.4 — the queue diagnoses and cannot cure.** Same shape as G-6.3. The screen exists to find
outstanding clearances and offers no action on any of them: no bulk chase, no status change, no
reminder. Each row must be opened via its offer, then the checks tab, then the item. *Impact: low.*

**Refining G-7.1.** Chapter 7 recorded that HR cannot add a referee to a candidate record, and
concluded that a candidate who supplied none has no referees for a reference check to run against.
That conclusion needs softening: a reference check does **not** read `JobCandidateReferee` at all.
HR records the referee's details directly on the `ReferenceCheckResponse`, typed free-hand, so a
reference check can always be carried out. What is lost is only the link back to the candidate's
own nomination (G-11.3) — and the ability to keep referees on the candidate record for future
applications. G-7.1 stands as a gap; its worst-case consequence does not.

**Worth crediting.** The offer anchor rather than the hire anchor; the three-way separation of
"not done", "waived" and "failed"; provider names snapshotted against later renames; evidence moved
onto the controlled-upload gate with the old path retained read-only; and templates so a standard
clearance set is not retyped per offer.

---

## 12. `/hr/recruitment/hires` — the handover to employment

**Files:** `page.tsx` (140), `[id]/page.tsx` (339)
**Walked:** 2026-09-15

### 12.1 What a hire record is

The bridge out of recruitment. `JobHireRecord` points back at both the `JobApplication` and the
`JobOffer` (both required), and forward at an `Employee` — nullable, because the employee does not
exist until somebody confirms the start.

It is created from the offer screen once the gates in § 10.7 are satisfied: a conditional offer
must be at `ChecksCleared`, a plain one at `Accepted` or `ChecksCleared`, and there is one hire per
application.

**Five statuses** (`JobHireStatus`): PendingOnboarding (1), OnboardingInProgress (2),
OnboardingCompleted (3), Active (4), Cancelled (5).

The transition map is mirrored client-side, with the honest caveat that *"the server owns the real
rules… this is here to keep the picker honest, not to replace that check"*:

| From | May be set to |
|---|---|
| PendingOnboarding | OnboardingInProgress, Cancelled |
| OnboardingInProgress | OnboardingCompleted, Cancelled |
| OnboardingCompleted | Cancelled |

**`Active` is deliberately absent from that map.** It is not a status anyone chooses — it is what
confirming the start produces. The dialog says so: *"Active is not offered here — confirm the start
date instead."*

### 12.2 The list

Like the interviews screen, there is **no general list endpoint** — only by id, number,
application, employee, status and start-approaching. The page is a view picker over those.

It defaults to **Starting within 30 days**, which is the right default for this record: a hire's
urgency is its start date. Columns: Hire, Candidate, Role, Expected start, Actual start, Employee
(number, or a dash), Status.

### 12.3 The detail page

Two actions: **Update status** (the map above) and **Confirm start**. A confirmed hire shows a
green banner naming the employee, the number, and who confirmed it when.

The overview card links out to the offer and, once it exists, to the employee record.

### 12.4 Confirm start — what it actually does

This is the most consequential single action in the whole module, and the page treats it that way:
it gets a destructive-styled alert rather than the shared one-liner every other dialog uses.

> **This creates an employee record.** Confirming burns an employee number and creates the
> employee, their contract, probation period, salary assignment and position history. It cannot be
> undone — a retry is safe (it refuses once linked), but there is no way to detach the employee
> afterwards.

**Two paths.** Tick *Internal hire* and an existing `Employee` is linked (validated for tenant).
Otherwise a new employee is built from the offer and the candidate, in nine steps:

| # | Creates | From |
|---|---|---|
| 1 | `Employee` | candidate's name, email, phone, gender, DOB; offer's position, org unit, location, employment type, salary |
| 1b | `EmployeeIdentificationCard` | the candidate's identity document, **unverified**, *"Carried from the candidate record at hire"* — skipped when there is a type but no number |
| 2 | `EmployeeContractDetail` | `CTR-{employeeNumber}`, start/end dates, salary, currency, probation days, weekly hours |
| 3 | `ProbationPeriod` | only when the offer states a duration |
| 4 | `EmployeePositionHistory` | `PositionChangeReason.InitialAssignment` |
| 5 | `EmployeeSalaryAssignment` | the offer's salary level, else the position's grade — **only if on payroll** |
| 6 | `EmployeeQualification`s | the **live** candidate profile |
| 7 | `EmployeeWorkHistory` | the live candidate profile |
| 8 | `EmployeeReferee`s | the live candidate profile — *"not in snapshot"* |
| 9 | `EmployeeSkill`s | only catalogue-linked skills; free-text skills have no `SkillId` to carry |

Then, after the commit: the requisition's fill counter is advanced (§ G-4.5), and a payroll profile
is created **best-effort** — *"the hire stands whether or not payroll can take the person today; a
gap shows on the payroll reconciliation read."*

**Three decisions inside this worth knowing:**

**The employee number comes from the register, not a compiled-in format.** The offer's
`EmploymentType` selects which staff-number register issues it. The comment explains why that
matters here specifically: *"This is the path by which contract staff actually arrive, so it is the
path that most needs to honour a register with its own numbering — a hire that bypassed it would
silently issue a permanent-series number to a contract employee."* A register set to manual has no
number to offer, so the resolver refuses and the hire cannot complete — *"which is correct: nobody
should be hired into a register whose numbers the organisation issues by hand without somebody
supplying the number."*

**Payroll membership is derived, not assumed.** `IsOnPayroll` is true when the offer gives the run
something to pay from — a base salary, or a grade that resolves one. Otherwise the employee arrives
off payroll with a stated `OffPayrollReason`: `PaidByInvoice` for a consultant or freelancer,
`Allowance` for an intern, `Other` beyond that, plus a note telling HR to confirm how the person is
paid. Note the knock-on: **an off-payroll hire gets no salary assignment** (step 5 is gated on
`IsOnPayroll`).

**Probation is derived from the offer.** `ProbationPeriodMonths > 0` makes the employee's
`StaffStatus` = `Probation` rather than `Active`, and `ProbationPeriodDays` defaults to 90 when the
offer says nothing.

**The idempotency guard** refuses when the hire already has an `EmployeeId` *or* is `Active` —
*"Without this check a double-click (or any retried request) would create a SECOND employee and a
second set of all of it, and burn another employee number."*

### 12.5 Known gaps

> **✅ All four closed, 2026-09-15 → 16.** This is where five of the module's hand-over gaps
> converged — `ConfirmStartAsync` writes nine tables and used to tell nobody. Read as history.
>
> | | Now |
> |---|---|
> | G-12.1 | Confirming a start advances the application to the Hired stage — which also moves `JobVacancy.HireCount` — falling back to a direct status write when the vacancy has no pipeline. It also converts the candidate out of the talent pool (G-13.1). |
> | G-12.2 | A `Cancelled` hire is refused server-side and hidden client-side. The old guard asked "already done?"; nothing asked "called off?". |
> | G-12.3 | The dialog now names the recovery path: terminate through Separations and cancel the hire — and says the employee number stays spent and the departure opens a position vacancy. |
> | G-12.4 | ⚠ **Already closed**, with G-7.1 — which was itself an incorrect finding. Referees were addable all along, so a hand-typed candidate could always carry them into employment. |

**G-12.1 — confirming a start does not mark the application Hired.** `ApplicationStatus.Hired` is
written in exactly one place in the solution: `MapStageTypeToStatus`, when an application is moved
to a pipeline stage of type `Hired` (§ 8.6). `ConfirmStartAsync` never touches the application.

So the moment a candidate becomes an employee — contract written, probation started, employee
number burned — their application still reads `OfferExtended` or `OfferAccepted`, and
`JobVacancy.HireCount` (maintained only by `AdjustVacancyCounters` on stage moves) has not moved
either. Somebody must separately walk the application into a Hired-type stage for the front of the
funnel to agree with the back of it.

This is the same shape as G-9.4 and belongs to Appendix C's sixth pattern, but it is the most
consequential instance: the vacancy's *Hired* tile and the application register both understate
what has actually happened. *Impact: medium.*

**G-12.2 — a Cancelled hire can still be confirmed into an employee.** The server's guard is
`if (entity.EmployeeId.HasValue || entity.Status == JobHireStatus.Active) throw`. `Cancelled` is
not checked. The client's guard is `canConfirmStart = !isActive && !hire.employeeId`, which also
lets it through.

So a hire cancelled during onboarding still shows **Confirm start**, and pressing it creates the
employee, the contract, the probation period and the position history, and burns an employee
number — all of it irreversible. Every other terminal state in this module is guarded; this one is
not. *Impact: medium.*

**G-12.3 — no way back from a confirmed hire, by design, and nothing says what to do instead.**
The dialog is admirably honest — *"there is no way to detach the employee afterwards"* — but the
screen offers no guidance on the recovery path when a start is confirmed in error (presumably
terminating the employee and cancelling the hire, which leaves the employee number spent and a
`PositionVacancy` opened by the departure). Recorded as a gap in documentation rather than in
code. *Impact: low.*

**G-12.4 — referees carry over to the employee but were never linkable on the candidate.**
Step 8 copies `JobCandidateReferee` rows into `EmployeeReferee`. Per G-7.1, HR cannot add a referee
to a candidate record at all, and per G-11.3 a reference check never links to one either. So for
any candidate HR typed in by hand, this step copies nothing — the new employee starts with no
referees on file, and the reference checks that were actually performed live only on the
`ReferenceCheckResponse` rows attached to the offer. *Impact: low-medium — a downstream consequence
of G-7.1, visible only after the hire.*

**Worth crediting.** The idempotency guard and the honest destructive-action copy; the employee
number going through the register so a contract hire cannot get a permanent-series number; payroll
membership derived with a stated reason rather than assumed; the identity document carried across
as an *unverified* card; probation derived from the offer; the payroll profile attempted
best-effort after the commit rather than blocking the hire; and `IncrementRequisitionFillAsync`
being written not to over-count, resurrect or downgrade. For a single action that writes nine
tables, this is carefully done.

---

## 13. `/hr/recruitment/talent-pool` — the candidate CRM

**File:** `page.tsx` (833)
**Walked:** 2026-09-15

### 13.1 What it is, and what it is not

Recruitment's candidate CRM: who is being kept warm for future vacancies, how warm they are, and
the segments the pool is worked through.

**⚠ It is not succession's talent pools.** The page comment flags the collision explicitly:
`/hr/succession/pools` holds *employee* talent pools — internal people identified for future
roles. This screen holds *candidates* — people outside the organisation, or internal applicants,
kept on file. Two features, two entity sets, the same two words.

As § 7.5 established, the pool is **two mechanisms at once**, and this screen is where both are
worked:

| | Flat membership | Segments |
|---|---|---|
| Stored on | the `JobCandidate` row | `CandidateTalentSegment` + `CandidateSegmentMembership` |
| Fields | `IsInTalentPool`, `TalentPoolStatus`, `TalentPoolSource`, `TalentPoolAddedDate`, `TalentPoolRemovedDate`, `TalentPoolRemovalReason`, `TalentPoolNotes`, `TalentPoolReviewDate`, `LastEngagedDate` | a named, owned, coloured grouping |
| Question it answers | "is this person on file, and how warm?" | "which shortlist of people am I working for the Finance pipeline?" |

**Six pool statuses** (`TalentPoolCandidateStatus`): Active (1), Passive (2), Dormant (3),
Expired (4), Converted (5), OnHold (6).

### 13.2 The six tiles

From `GET /api/talent-pool/analytics`: In the pool, Active, Dormant, Overdue for review, Added this
month, Converted this year. The endpoint also returns Passive, Added this year, average days in
pool, and breakdowns by source and by segment, none of which this screen shows.

The `BySegment` breakdown is a repaired defect worth noting: *"BySegment was declared on the DTO
and never populated — the dashboard's segment chart rendered empty for as long as this endpoint has
existed."*

### 13.3 The Pool tab

**This is the only screen in recruitment with real search.** The box matches on *"Name, email,
headline, employer"* server-side, alongside filters for status, source, segment and an *overdue for
review* checkbox — all combining into one paged read. The full candidate register (§ 7.3) offers
only an exact-email lookup. If G-7.6 is ever addressed, this is the endpoint that already does it.

Columns: Candidate, Segments (as badges), Status, Added, Last engaged, Next review, Experience.

**Bulk operations** work on the selected rows — `AssignSegment`, `RemoveSegment`, `SetStatus` —
and return per-item outcomes, with skipped rows explained beneath the table, the same partial-result
convention as the pipeline board (§ 5.6).

### 13.4 The Segments tab

A segment is more than a label. Four fields added in lane V (D-6) give it an owner and a purpose:

| Field | Meaning |
|---|---|
| `OwnerEmployeeId` | who works this segment |
| `Purpose` | why it exists |
| `TargetPositionId` | the role it feeds |
| `JobFamilyId` | the family it feeds |

Columns: Name, Purpose, Owner, Feeds, Members, Active.

One behaviour the page comments and the API does not: **the update replaces these four fields, it
does not merge them.** All four go on every save, so a cleared picker sends `null` and clears the
column server-side (G-13.4).

Deleting a segment needs `RecruitmentAdmin`; un-assigning a candidate from one stays at Write —
*"the same-object-authoring rule"*.

### 13.5 Matching — the 40/30/20 rubric

Two directions, one rubric: `MatchToVacancyAsync` (pool → one vacancy, shown on the vacancy's
*Pool matches* tab) and `MatchCandidateToVacanciesAsync` (one candidate → vacancies, shown on the
candidate's talent-pool tab).

Only **Active** pool members are considered. Each scores out of **90**, not 100:

| Criterion | Points | Awarded when |
|---|---|---|
| Experience | +40 | the vacancy states no minimum, **or** the candidate meets it |
| Work mode | +30 | the candidate is open to *Any*, **or** their preference matches the vacancy's mode |
| Availability | +20 | the candidate has no `AvailableFrom`, **or** it has passed |

Each awarded point carries a human-readable reason (*"Meets experience requirement (5 yr)"*,
*"Open to any work arrangement"*, *"Available now"*), and an unavailable candidate still gets a
reason line stating their date.

This too replaced a dead path: *"this used to be a stub returning MatchScore 0 for everyone, which
is worse than no score because a column of zeros reads as 'nobody fits'."*

**A detail worth not 'fixing'.** The work-mode comparison is
`c.PreferredWorkArrangement.ToString() == vacancy.WorkMode.ToString()` — a comparison of enum
*names*, which looks like a smell and is in fact correct here. The two enums share member names but
**not** values: `WorkMode` is OnSite=1, Remote=2, Hybrid=3, while `PreferredWorkArrangement` is
Any=0, OnSite=1, Hybrid=2, Remote=3. Comparing numerically would silently match Remote against
Hybrid. It is fragile — renaming a member in either enum breaks it with no compiler error — but it
is not a bug.

### 13.6 Known gaps

> **✅ All four closed, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-13.1 | Confirming a start converts a pooled candidate automatically, and the tile dates off `TalentPoolRemovedDate` — written at the moment of exit and never moved by an unrelated edit. **No `ConvertedDate` column was added**: a conversion *is* an exit from the pool, so the date already existed. |
> | G-13.2 | Three tiers instead of two: a genuine match scores full, unconstrained-but-evidenced scores partial, silence scores little. An empty profile no longer tops the list. Applied symmetrically to both matchers, and `MatchScoreMax` gives the bare number a denominator. |
> | G-13.3 | `ApplyPoolExit` takes the exit status as a parameter, so leaving the pool and *why* you left are separate facts and both can be true. ⚠ Converted candidates therefore leave the `IsInTalentPool` set — which is why the analytics tile needed its own query. |
> | G-13.4 | Behaviour unchanged, because clearing an owner has to be possible. The whole-record contract is now stated on the DTO, where a new client reads it, rather than only in this page's comment. |

**G-13.1 — "Converted" is a manual bookkeeping act, measured by a proxy date.** Two problems
compound.

First, **nothing sets `Converted` automatically.** `TalentPoolStatus` is written on entry (`Active`),
on removal (`Expired`), and otherwise only by a human choosing a value in the status dropdown or a
bulk `SetStatus`. `ConfirmStartAsync` (§ 12.4) never touches the candidate's pool status — so a
pooled candidate who is genuinely hired stays `Active` in the pool unless a recruiter remembers to
change it.

Second, **the "Converted this year" tile dates conversion by `UpdatedAt`**:

```
ConvertedThisYear = candidates.Count(c => c.TalentPoolStatus == Converted
                                       && c.UpdatedAt >= startOfYear)
```

`UpdatedAt` moves on any edit. A candidate converted two years ago whose notes were touched last
week counts as converted this year. There is no `ConvertedDate` column to use instead.

The consequence matters because this is the module's **only measure of whether the talent pool
works at all** — whether keeping people warm produces hires. It measures neither reliably.
*Impact: medium.*

**G-13.2 — the match rubric rewards missing data.** All three criteria award their points for the
*absence* of a constraint as readily as for a genuine fit:

- the vacancy states no minimum experience → **+40**, whoever the candidate is;
- the candidate is open to *Any* work arrangement → **+30**;
- the candidate has no `AvailableFrom` on file → **+20**, reason *"Available now"*.

So a candidate with an entirely empty profile scores **90 out of 90** against a vacancy that states
no requirements — the top of the list. Given § 7.5's finding that the professional-profile fields
are candidate-supplied and not editable by HR, an empty profile is the normal state for any
candidate HR typed in by hand.

The score is also rendered bare, with no denominator or unit, so a reader has no way to know it is
out of 90 or what a good one looks like. *Impact: medium — the ranking is plausible-looking and
inverted for exactly the candidates HR knows least about.*

**G-13.3 — a converted candidate stays in the pool.** `ApplyPoolExit` is the only thing that clears
`IsInTalentPool`, and it forces `TalentPoolStatus = Expired`. So marking someone `Converted`
through the status dropdown leaves `IsInTalentPool = true`, and they continue to appear in the pool
list, in `TotalInPool`, and in the *Active* candidate set that matching draws from — until someone
separately removes them, which overwrites `Converted` with `Expired` and loses the conversion the
analytics tile counts. The two actions cannot both be true. *Impact: low-medium.*

**G-13.4 — segment metadata is replaced, not merged, and only the UI knows.** `PUT /segments/{id}`
overwrites `ownerEmployeeId`, `purpose`, `targetPositionId` and `jobFamilyId` with whatever the
payload carries, so a caller that omits them clears them. The page handles this correctly by always
sending all four and comments why; any other client — or a future partial-update screen — would
silently wipe a segment's owner and purpose. *Impact: low.*

**Worth crediting.** The permission-based gating (`hasAnyPermission` / `hasPermission`) rather than
role checks, which is the pattern the rest of the module should follow; the controller's own record
of a closed hole — reads and desk writes were once open to *"any internal user"*; the repaired
`BySegment` breakdown and the repaired match stub; partial bulk results with per-item reasons; and
the server-side multi-field search that the candidate register still lacks.

---

## 14. `/hr/recruitment/analytics` — the year in numbers

**File:** `page.tsx` (811)
**Service:** `ErpSystem.Core/Services/HR/RecruitmentAnalyticsService.cs` (419)
**Walked:** 2026-09-15

### 14.1 The most important thing about this screen

**It derives almost everything from records, not statuses** — and that single decision makes it
the most trustworthy surface in the module.

Every status-drift finding in this guide is sidestepped here:

| Figure | How it could have been computed | How it *is* computed | Which gap that avoids |
|---|---|---|---|
| Shortlisted | `Status == Shortlisted` | `a.ShortlistedDate != null` | G-8.3 (a stage move rewrites the status) |
| Interviewed | a status | `a.InterviewSlots.Any()` | G-9.4 (closing an interview moves nothing) |
| Offered | a status | `a.Offer != null` | — |
| Hired | `Status == Hired` | **`a.HireRecord != null`** | **G-12.1** (confirming a start never marks the application Hired) |
| Offers | every `JobOffers` row | `o.IsLatestVersion` only | **G-10.2** (superseded versions stay live) |
| Hires | every hire record | `Status != Cancelled`, timed only when there is an actual or confirmed start | G-12.2 |

The offer query's comment states the principle outright: *"latest version only — revisions must not
double-count."*

So the funnel's **Hired** figure is right even though the application register is wrong, and the
acceptance rate is right even though the offers list double-counts. If you want to know what
actually happened in a year, this screen is the place to look — not the registers.

### 14.2 Three rules the page states about itself

The file's own header sets out three ways the screen could quietly lie, and what it does instead.
They are worth reading as a model for how a reporting screen should be written:

1. **A null speed figure means "no sample", not "instant".** All four speed tiles render an em
   dash, and the sample size sits on the tile rather than being left implicit.
2. **The acceptance rate's denominator is offers that got an answer**, not offers issued. With
   nothing answered it would compute to 0, *"which would read as 'everyone declined'"* — so the card
   says which of the two situations it is **before** quoting a rate.
3. **Half this payload is point-in-time and ignores the year selector.** Open vacancies, ageing,
   empty seats and each recruiter's open count are as-of-today however far back the year goes, and
   *"every one of them says so where it is read, not in a single footnote nobody reaches."*

### 14.3 What is on the page

**Year selector** (six years back), with `keepPreviousData` so changing it dims the page rather
than blanking it.

**Two tile rows:**

| Row | Tiles |
|---|---|
| Volume | Open vacancies *(now)*, Applications *(in year)*, Offers *(issued in year)*, Hires *(started in year)* |
| Speed | Time to fill, Time to hire, Time to shortlist, Seats standing empty *(now)* |

All four speed figures share one sample, so the sample size is stated once — *"median N d · N hires
timed"*, or *"No confirmed starts in {year}"*.

**Eight cards:**

| Card | Shows | Note |
|---|---|---|
| By month | applications / offers / hires | **two plots, not one** — *"an offer or a hire is a fraction of the applications behind it, and a shared axis would flatten both onto the baseline"* |
| Funnel | Applied → Shortlisted → Interviewed → Offered → Hired | bars are a **share of applications**, not stage-to-stage conversion, *"because an application can be interviewed without having been formally shortlisted"* |
| Offer outcomes | accepted / declined / expired / withdrawn / pending, plus a computed **remainder** | draft, pending-approval, on-hold and rejected offers are in the total and in none of the buckets, so the remainder is shown *"rather than letting the rows quietly fail to add up"* |
| What it cost | total and by category | each cost line converted at **the rate held against its own line**, not a single period rate |
| Vacancy ageing | age bands of currently-open vacancies | point-in-time, and says so |
| Where candidates come from | applications by `ApplicationSource`, with a hire rate | *"one hire out of two applications is 50%, and noise"* |
| Longest open | the eight oldest open vacancies | point-in-time |
| Recruiter load | open *(now)* vs applications and hires *(in year)* | the mixed timeframe is stated on the card |

`Accepted` deliberately counts `Accepted`, `ConditionallyAccepted` **and** `ChecksCleared` — a
candidate who said yes and is waiting on checks has accepted.

### 14.4 The chart conventions, which are load-bearing

This file carries more design reasoning than any other in the module, and three of its decisions
exist because the obvious alternative is silently broken:

**Not the shadcn `--chart-*` tokens.** Their light and dark sets are different hues, so *"a series
would change identity with the theme."* The page defines its own palette — three categorical hues
keyed **to the entity, not to rank**, so applications stay one colour in every chart on the page,
plus two single-hue ordinal ramps for the genuinely ordered categories (funnel stages, age bands),
re-stepped for the dark surface rather than the same hex flipped.

**`var(--popover)`, not `hsl(var(--popover))`.** This app's theme tokens hold whole colour values
(`oklch(…)`, `#202020`) rather than the bare `H S% L%` triplets `hsl()` expects. Wrapping them
produces invalid CSS that is *"dropped without a warning — leaving recharts' default white tooltip,
which is unreadable on a dark card. Several older charts in this repo do exactly that; this one
deliberately does not."*

**A table under every chart.** The palette was validated against this app's actual card surfaces,
and one colour — aqua at 2.82:1 on the light card — falls below the 3:1 bar. That obliges the
relief rule, hence the tables, *"which is also how identity here is never carried by colour alone."*

Two smaller ones: `.dark` is the only dark selector needed because the app themes through
next-themes with `attribute="class"`, so a `prefers-color-scheme` block *"would never be the thing
that fires"*; and the tooltip hit-band spans the whole month column *"so reading a value never
means landing on a two-pixel bar."*

### 14.5 Known gaps

> **✅ All four closed, 2026-09-15 → 16 — and two of them needed no change to this service**, which
> is the point the chapter goes on to make. Read the findings as history.
>
> | | Now |
> |---|---|
> | G-14.1 | Fixed by the nightly sweep writing `Expired`, with **no code change here**. This service already counted the bucket correctly and already filtered `IsLatestVersion`; it read zero only because nothing ever wrote the status. |
> | G-14.2 | Fixed by G-3.1 and G-3.2: HR can reconcile, and departures now log themselves without waiting for one. Again no change here. |
> | G-14.3 | Both definitions kept — they answer different questions — and both screens now say so. ⚠ The difference used to be invisible because nothing wrote `Anticipated`; G-3.6 changed that, so these two numbers will genuinely diverge from now on. If they are ever equal again, check anticipated logging still works. |
> | G-14.4 | Retitled *"How far applications got"* rather than redrawn. The caption was already accurate; of a correct caption and a misleading shape, the shape is what gets believed. |

**G-14.1 — the Expired bucket is structurally zero.** `Expired = offers.Count(o => o.OfferStatus ==
JobOfferStatus.Expired)`, and per G-2.4 nothing in the solution ever writes that status. The offer
outcomes chart therefore always shows an empty Expired bar, and the offers that actually lapsed sit
in **Pending** instead — inflating the one bucket that reads as "still live".

The remainder line saves the totals from being wrong, but the *shape* is misleading: a year in
which half the offers went unanswered shows as a year with a large pending pipeline.
*Impact: medium — a reporting consequence of G-2.4 rather than a new defect.*

**G-14.2 — "Seats standing empty" inherits the establishment register's emptiness.** The tile
counts `PositionVacancies` at Open, Under Review or Requisition Raised. Per G-3.1 and G-3.2 those
rows exist only where somebody with `HR.Recruitment.Admin` has pressed **Reconcile** — which the
`HR` role cannot do, and which is the only writer of the table.

On a tenant where that has never happened, this tile reads **0** and the *"days empty on average"*
hint reads *"No open seats right now"*, however many posts are actually vacant. It is the module's
only headline measure of unfilled establishment, and it is as good as the last Reconcile.
*Impact: medium.*

**G-14.3 — two definitions of "an open seat" differ by one status.** The establishment screen's
stats count `Anticipated, Open, UnderReview, RequisitionRaised`; this screen counts
`Open, UnderReview, RequisitionRaised`, excluding **Anticipated**.

Excluding it here is defensible — an anticipated vacancy is a seat that *will* fall empty, not one
standing empty now — and the tile is labelled accordingly. But the same underlying question gets
two different answers on two screens, with nothing saying so. In practice the difference is
currently zero, because nothing ever writes `Anticipated` (G-3.6). *Impact: low.*

**G-14.4 — the funnel counts an application against every stage it reached, which is not a
funnel.** The card is honest about this — *"The bars are a share of applications rather than
stage-to-stage conversion"* — and gives the reason. But the card is titled **Funnel** and drawn as
a descending ramp, which is the visual grammar of stage-to-stage conversion. A reader who takes the
picture at face value will read drop-off rates that the numbers do not support. *Impact: low — the
text is correct; the form contradicts it.*

**Worth crediting, at length.** This screen is the counter-example to most of this document. It
derives from records rather than statuses, so it is immune to the drift the registers suffer
(§ 14.1). It states its own limitations *where they are read* rather than in a footnote. It shows a
remainder rather than letting buckets fail to add up. It distinguishes "no sample" from "zero". It
refuses to quote a rate whose denominator is empty. It converts each cost at its own line's rate.
And its chart layer documents three traps — theme-swapping tokens, the `hsl()` wrapper, and a
sub-contrast hue — that other charts in this repo fall into. If a future reader wants to know what
"done properly" looks like in this module, it is this file.

---

## 15. `/hr/recruitment/dashboard` — what needs attention this week

**File:** `page.tsx` (335)
**Endpoint:** `GET /api/recruitment-dashboard` — the whole payload in one request
**Walked:** 2026-09-15

### 15.1 What it is

One aggregated read, assembled **in the controller** from six existing services. It is not backed
by a dashboard service of its own — `RecruitmentDashboardController` calls
`IJobVacancyService`, `IJobInterviewService`, `IJobOfferService`, `IJobHireService`,
`IJobApplicationService` and `IRecruitmentAnalyticsService` in turn, *"sequentially — EF Core
DbContext is not thread-safe; concurrent queries on the same scoped instance cause concurrency
errors"*, and shapes the result itself.

Its scope is stated against the landing page's: *"The landing page's metric tiles stay as they are
(cheap, separate calls) — this is the fuller picture: pipeline shape, what needs attention this
week, and what's arriving."*

The DTO property names *"intentionally mirror the Blazor view-model classes… so the frontend
service can `ReadFromJsonAsync<RecruitmentDashboardData>` without any mapping"* — a fossil of an
earlier client, and the reason the shapes look slightly un-idiomatic.

### 15.2 Who can see it

`[Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]` at class level, with a stated reason:
*"the payload carries candidate names on recent applications, upcoming interviews, offers and hires
starting soon… a bare `[Authorize]` here would have let any authenticated employee read it."*

Note it is the **only** recruitment controller without `[Authorize(Policy = "InternalOnly")]`
alongside. In practice the permission gate covers it — no external role holds `HR.Recruitment.*` —
so this is an inconsistency rather than a hole (G-15.5).

### 15.3 What is on it

**Four tiles:** Active vacancies, Active applications, Interviews today, Offers pending response.

**An amber SLA card**, rendered only when there is something in it.

**A Pipeline card** — four labelled progress bars, each scaled against the largest of the four.

**Five lists**, each capped and each with its own window:

| Card | Window | Source |
|---|---|---|
| Recent applications | last 10 | `GetPagedAsync(1, 10)` |
| Upcoming interviews | next 7 days, first 5 | `GetByDateRangeAsync(today, +7)` |
| Hires starting soon | next 14 days, first 10 | `GetWithStartDateApproachingAsync(14)` |
| Offers expiring soon | next 7 days, first 5 | `GetExpiringOffersAsync(7)` |
| Vacancies with a deadline approaching | next 7 days, first 10 | `GetWithDeadlineApproachingAsync(7)` |

### 15.4 The contrast with the analytics screen

These two screens sit next to each other in the sidebar and are built on **opposite principles**.

| | `/analytics` (ch. 14) | `/dashboard` (this chapter) |
|---|---|---|
| Derives from | **records** — `HireRecord != null`, `ShortlistedDate != null`, `InterviewSlots.Any()` | **denormalised counters and statuses** — `v.ApplicationCount`, `v.ShortlistedCount`, `OfferStatus == Sent` |
| Offer versions | filtered to `IsLatestVersion` | **not filtered** |
| Immune to | G-8.3, G-9.4, G-10.2, G-12.1 | none of them |
| States its own limits | on every card | not at all |

Neither approach is wrong in principle — counters are cheap and a dashboard is read constantly.
But the dashboard inherits every drift the analytics screen was written to avoid, and says nothing
about it. When the two disagree, **analytics is the one to believe**.

### 15.5 Known gaps

> **✅ All five closed, 2026-09-15 → 16.** Read the findings as history.
>
> | | Now |
> |---|---|
> | G-15.1 | All four bars count **people**, all-time, on the same vacancy set. `InterviewCount` was maintained on the entity all along and merely missing from the summary DTO. |
> | G-15.2 | Filters `IsLatestVersion`, and both underlying writers are fixed too (G-2.4 marks lapsed offers Expired, G-10.2 supersedes revised ones). |
> | G-15.3 | `CandidateName` is null, and the field is nullable. A session holds several candidates, so there is no single name to give; `CandidateCount` already says how many. |
> | G-15.4 | Fires on `ShortlistingDeadline` — the date the card is named for. A vacancy with none raises no alert: an SLA nobody agreed cannot be breached. |
> | G-15.5 | `InternalOnly` paired with the permission policy, like every other recruitment controller. |

**G-15.1 — the Pipeline card compares three counts of people with one count of sessions.**

```
Applied      = activeVacancies.Sum(v => v.ApplicationCount)    ← candidates
Shortlisted  = activeVacancies.Sum(v => v.ShortlistedCount)    ← candidates
Interviewing = interviews.Count                                 ← INTERVIEW SESSIONS
Offer        = activeVacancies.Sum(v => v.OfferCount)          ← candidates
```

As chapter 9 establishes, an interview is a **session** that can hold many candidates. So the third
bar counts something different in kind from its neighbours, and they are drawn on a shared scale
as comparable progress bars.

The window differs too: three bars are all-time totals on currently-active vacancies; the
Interviewing bar is *sessions scheduled in the next seven days*. A vacancy that interviewed forty
people last month contributes nothing to it.

The payload already carries `IntervieweeCount` per interview — `interviews.Sum(i => i.IntervieweeCount)`
would at least count people — but even that would remain a seven-day window against three
all-time figures. *Impact: medium — the shape of the funnel is the card's entire purpose, and the
middle of it is not on the same axis as the rest.*

**G-15.2 — "Offers pending response" counts lapsed and superseded offers.**
`OffersMade = (await _offerService.GetByStatusAsync(Sent)).Count` — with **no `IsLatestVersion`
filter** and no expiry bound. So it includes:

- offers whose expiry passed long ago, because nothing ever writes `Expired` (G-2.4);
- v1 offers left in `Sent` by a revision, because `ReviseOfferAsync` never changes their status
  (G-10.2).

The *Offers expiring soon* card has the same two problems, being the same
`GetExpiringOffersAsync(7)` the landing tile uses. The analytics screen filters correctly for
exactly this reason; this one does not. *Impact: medium — a tile labelled "pending response" grows
monotonically and never falls.*

**G-15.3 — the interview list's `CandidateName` is filled with the literal text "Round N".**

```csharp
UpcomingInterviews = interviews.Take(5).Select(i => new DashboardInterviewDto
{
    CandidateName = $"Round {i.Round}",   // ← a round number in a name field
    Round         = i.Round,              // ← and again, correctly, here
    ...
```

The page happens not to render it — its table is Vacancy / Round / Type / When, with no Candidate
column — so nothing is visibly wrong today. But the field is wrong in the payload, any future
consumer reading `CandidateName` gets nonsense, and the controller's own docstring cites *"candidate
names on… upcoming interviews"* as part of the justification for the HR gate, which for this list
is not what it carries. *Impact: low-medium — latent, and the kind that surfaces when someone adds
the obvious column.*

**G-15.4 — the SLA card is titled for one deadline and fires on another.** The heading reads
**"Shortlisting SLA breached"**, and each row says *"Deadline was {date}"*. The condition is:

```csharp
v.ApplicationDeadline.HasValue && v.ApplicationDeadline.Value < today
```

That is the **application** deadline — the date applications close. A vacancy carries a separate
`ShortlistingDeadline`, which the screening screen (§ 5.7) does use, and which is what a
shortlisting SLA would breach.

So the card fires the day applications close, on every open vacancy, whether or not shortlisting is
late — and stays lit for the rest of the vacancy's life, since the deadline only recedes further
into the past. It is the loudest element on the page and it is measuring the wrong date.
*Impact: medium.*

**G-15.5 — the only recruitment controller without `InternalOnly`.** Every other one pairs a
permission policy with `[Authorize(Policy = "InternalOnly")]`. This one has the permission policy
alone. Since `HR.Recruitment.Read` is held by no external role, nothing is exposed — but the
defence-in-depth pattern the rest of the module follows is absent here, and a future change to the
permission map would have one fewer thing standing in its way. *Impact: low.*

**Worth crediting.** One request rather than eight; the sequential-query comment explaining a real
EF constraint rather than leaving it to be rediscovered; an explicit statement of scope against the
landing page so the two do not drift into each other; and an authorization decision with its
reasoning written down, including what the weaker alternative would have cost.

---

## 16. The module, end to end

All thirty-two recruitment screens are now walked. A few things are only visible from here.

> **✅ Written at the end of the walk, 2026-09-15; the gaps it concludes from were closed the
> following day.** The observations still hold — they are about how to read this system, not about
> which lines were wrong — but the *examples* are now history. Each is annotated below.

**The chain works, one link at a time.** Every record in § 1.1 exists, is reachable, and does what
its name says. What was thin is the *connective tissue*: a step rarely told the next step that it
happened (Appendix C, sixth pattern). Booking an interviewee and issuing an offer advance the
pipeline; closing an interview, recording a verdict and confirming a start did not.

> **Now:** closing an interview advances its attendees on the panel's verdict, confirming a start
> marks the application Hired and converts the candidate out of the talent pool, and the verdict
> itself has a reader. Two hand-overs were deliberately **not** automated — see the sixth pattern
> in Appendix C for the rule that decided which.

**Read the readers, not the writers.** The single most useful habit this walk produced. Statuses
in this module drifted — a stage move rewrote a decision, a revision left its predecessor live,
a deadline passed with nothing to mark it. Screens that derive from *records* (`/analytics`) were
right anyway; screens that read *statuses* (`/dashboard`, the landing tiles) inherited every drift.

> **This turned out to be the most load-bearing sentence in the document.** It decided four of the
> fixes: `ApplicationCount` and `PositionsFilled` are now *counted* rather than stored;
> `/analytics` needed no change at all for two findings because it was already right; and the
> dashboard's tiles were fixed by filtering the reads as well as fixing the writers. **The general
> rule it became:** give a field a reader when it records a *judgement* somebody made, and derive
> it when it merely *counts* rows the database already has.

**Three questions to ask of any screen here**, in the order that proved most productive — all
three still worth asking, with what they actually found:

1. *Does the endpoint exist, and can anyone reach it?* Different questions — **seven findings sat
   in the gap between them**, and they were the cheapest class to close, because the hard half was
   already written.
2. *Who writes this field, and who reads it?* Four fields were written faithfully and read by
   nothing; three were written **only by the demo seeder**, so they passed every walkthrough they
   were ever shown in.
3. *Does the screen's prose match its code?* Seven times it did not, and the prose was the
   confident half. ⚠ **Add a fourth:** *when they disagree, which is more dangerous to believe?*
   In five of the seven the prose was right and the code was missing. In G-4.1 and G-10.1 the prose
   was the dangerous half — it said Submit was inoperable when it silently approved.

**Where to start, if any of this is to be fixed.** ✅ **Done, 2026-09-15 → 16** — and the analysis
below proved right, which is why it is kept. The `high` rows in Appendix C were not evenly
distributed. Three of them — G-4.1, G-10.1 and the auto-approve note — were **one mechanism**:
publish a workflow definition per entity type, or make `SubmitAsync` refuse instead of approving.
Two more — G-3.1 and G-3.2 — were the establishment register having no writer the HR role could
reach. That was two changes covering five of the six worst findings.

> **What the estimate missed, and it is the lesson.** Both halves cost more than the sentence
> implies. The auto-approve mechanism was not "publish a definition or make `SubmitAsync` refuse" —
> the shared fallback serves nine modules, so it could not be changed centrally; and refusing at
> submit *alone* strands the record, because approve, reject and recall all ask the engine
> questions it cannot answer without an instance. It took four coordinated changes per entity type,
> and the defect turned out to span **22 HR services**, of which seventeen are still open.
>
> **A one-mechanism finding is cheap to describe and rarely cheap to fix.** When a gap list says
> "these five are really one change", that is a statement about the *diagnosis*, not the remedy.

---

## Appendices

### Appendix A — entity and table index

| Entity | Table | First documented in |
|---|---|---|
| `PositionVacancy` | `PositionVacancies` | ch. 1, ch. 2 |
| `StaffRequisition` | `StaffRequisitions` | ch. 1, ch. 2 |
| `JobPosting` | `JobPostings` | ch. 1, ch. 2 |
| `JobOffer` | `JobOffers` | ch. 1, ch. 2 |
| `EmployeePosition` | `EmployeePositions` | ch. 2 (counted for `totalPositions`), ch. 3 (`ExpectedHeadcount`, `EstablishmentApprovedOn`, `EstablishmentSourceBudgetId`) |
| `Employee` | `Employees` | ch. 3 (counted for `FilledCount`) |
| `OrganizationUnit` | `OrganizationUnits` | ch. 3 |
| `ManpowerBudget` | `ManpowerBudgets` | ch. 3 (`BudgetNumber` for the Source column) |
| `StaffRequisitionCost` | `StaffRequisitionCosts` | ch. 4 (Costs tab) |
| `StaffRequisitionAttachment` | `StaffRequisitionAttachments` | ch. 4 (Attachments tab) |
| `StaffRequisitionComment` | `StaffRequisitionComments` | ch. 4 (Discussion tab, threaded) |
| `StaffRequisitionHistory` | `StaffRequisitionHistories` | ch. 4 (History tab) |
| `ManpowerBudgetLine` | `ManpowerBudgetLines` | ch. 4 (the budget check) |
| `JobDescription` | `JobDescriptions` | ch. 4 (must belong to the requisition's position) |
| `JobVacancy` | `JobVacancies` | ch. 5 |
| `JobVacancyStatusHistory` | `JobVacancyStatusHistories` | ch. 5 (History tab) |
| `JobVacancyAttachment` | `JobVacancyAttachments` | ch. 5 |
| `JobShortlistingCriteria` | `JobShortlistingCriterias` | ch. 5 (criteria tab, comparison matrix) |
| `RecruitmentPipeline` / `RecruitmentPipelineStage` | `RecruitmentPipelines` / `RecruitmentPipelineStages` | ch. 5 |
| `VacancyPipelineStageAssignment` | `VacancyPipelineStageAssignments` | ch. 5 (stage **owners**) |
| `JobApplication` | `JobApplications` | ch. 5 (scored, shortlisted, moved) |
| `JobApplicantTestResult` | `JobApplicantTestResults` | ch. 5 (captured, never weighed — G-5.2) |
| `ShortlistDecisionLog` | `ShortlistDecisionLogs` | ch. 5 (written by every shortlist decision) |
| `JobPosting` | `JobPostings` | ch. 2 (landing tile), ch. 6 |
| `JobPostingAttachment` | `JobPostingAttachments` | ch. 6 (newspaper artwork, proof of publication) |
| `JobCandidate` | `JobCandidates` | ch. 7 |
| `JobCandidateQualification` | `JobCandidateQualifications` | ch. 7 (read-only tab, G-7.1) |
| `JobCandidateWorkHistory` | `JobCandidateWorkHistories` | ch. 7 (read-only tab, G-7.1) |
| `JobCandidateReferee` | `JobCandidateReferees` | ch. 7 (read-only tab — feeds reference checks) |
| `JobCandidateSkill` | `JobCandidateSkills` | ch. 7 (read-only tab, G-7.1) |
| `JobCandidateLanguage` | `JobCandidateLanguages` | ch. 7 (read-only tab, G-7.1) |
| `JobCandidateInterest` | `JobCandidateInterests` | ch. 7 (read-only tab, G-7.1) |
| `JobCandidateDocument` | `JobCandidateDocuments` | ch. 7 (writable) |
| `JobCandidateNote` | `JobCandidateNotes` | ch. 7 (writable, recruiter-private) |
| `CandidateTalentSegment` / `CandidateSegmentMembership` | same names + `s` | ch. 7, ch. 13 (segment side of the pool; owner/purpose/target from lane V) |
| `JobFamily` | `JobFamilies` | ch. 13 (a segment may feed a family) |
| `CandidateEngagementEvent` | `CandidateEngagementEvents` | ch. 7 (engagement timeline) |
| `Country` | `Countries` | ch. 7 (nullable FK — see § 7.1) |
| `JobApplicationStageHistory` | `JobApplicationStageHistories` | ch. 8 (one row per stage visit, `IsCurrent`) |
| `JobApplicantCommunication` | `JobApplicantCommunications` | ch. 8 (a log, not an outbox) |
| `ShortlistReview` | `ShortlistReviews` | ch. 8 (panel reviews → `AggregatedReviewScore`) |
| `ApplicationCandidateSnapshot` | *(JSON on `JobApplications`)* | ch. 1, ch. 5 (what scoring reads) |
| `JobInterview` | `JobInterviews` | ch. 9 (a session, not an appointment) |
| `JobInterviewee` | `JobInterviewees` | ch. 9 (one per booked application; holds `Outcome` — G-9.2) |
| `JobInterviewPanelist` / `JobInterviewExternalPanelist` | same + `s` | ch. 9 (the per-record access rule keys off the first) |
| `JobInterviewQuestion` | `JobInterviewQuestions` | ch. 9 (a **plan** — one per section, not one question) |
| `JobInterviewSelectedQuestion` | `JobInterviewSelectedQuestions` | ch. 9 (the drawn questions) |
| `JobInterviewQuestionType` / `JobInterviewQuestionDetail` | same + `s` | ch. 9 (the question bank) |
| `InterviewQuestionPreset` / `InterviewQuestionPresetItem` | same + `s` | ch. 9 (scaffolds a plan and draws questions) |
| `JobInterviewScoreSummary` | `JobInterviewScoreSummaries` | ch. 9 (one per candidate × panelist) |
| `JobInterviewScoreEntry` | `JobInterviewScoreEntries` | ch. 9 (one per question) |
| `JobInterviewScoreDraft` | `JobInterviewScoreDrafts` | ch. 9 (private to one panelist; deleted on sign-off) |
| `JobOffer` | `JobOffers` | ch. 10 (versioned; unique `(TenantId, OfferNumber)` unfiltered by `IsDeleted`) |
| `JobOfferBenefit` | `JobOfferBenefits` | ch. 10 (seeded from *effective* benefits, groups included) |
| `JobOfferNote` | `JobOfferNotes` | ch. 10 |
| `OfferCandidateToken` | `OfferCandidateTokens` | ch. 10 (single-use response link, emailed at issue) |
| `PreEmploymentCheck` / `PreEmploymentCheckItem` | same + `s` | ch. 10, ch. 11 (gates `ChecksCleared`; anchored to the **offer**) |
| `PreEmploymentCheckTemplate` / `…TemplateItem` | same + `s` | ch. 11 (seeds a standard clearance set) |
| `PreEmploymentCheckProviderService` | `PreEmploymentCheckProviderServices` | ch. 11 (the provider panel) |
| `ReferenceCheckResponse` | `ReferenceCheckResponses` | ch. 11 (`RefereeId` never set by the UI — G-11.3) |
| `Supplier` (Procurement) | `Suppliers` | ch. 11 (check providers, name snapshotted) |
| `JobHireRecord` | `JobHireRecords` | ch. 12 (the bridge; `EmployeeId` null until confirmed) |
| `EmployeeContractDetail` | `EmployeeContractDetails` | ch. 12 (created by confirm-start, `CTR-{number}`) |
| `ProbationPeriod` | `ProbationPeriods` | ch. 12 (only when the offer states a duration) |
| `EmployeePositionHistory` | `EmployeePositionHistories` | ch. 12 (`InitialAssignment`) |
| `EmployeeSalaryAssignment` | `EmployeeSalaryAssignments` | ch. 12 (**skipped when off payroll**) |
| `EmployeeIdentificationCard` | `EmployeeIdentificationCards` | ch. 12 (carried from the candidate, unverified) |
| `EmployeeQualification` / `EmployeeWorkHistory` / `EmployeeReferee` / `EmployeeSkill` | same + `s` | ch. 12 (carried from the **live** candidate profile) |
| `StaffNumberFormat` (register) | `StaffNumberFormats` | ch. 12 (issues the employee number per employment type) |
| `SalaryGrade` | `SalaryGrades` | ch. 10 (supplies the band — absent on ungraded posts, G-10.5) |
| `BenefitPolicy` | `BenefitPolicies` | ch. 10 (benefit descriptions on the offer) |

*Grows as chapters are written.*

### Appendix B — endpoint index

> **Added by the gap-closure pass, 2026-09-15 → 16:**
>
> | Endpoint | Policy | Why |
> |---|---|---|
> | `GET /api/job-offers/paged` | `HR.Policy.RecruitmentRead` | G-10.3 — the offers list was the only recruitment list with no bound at all, and that was its *default* view. The unpaged `GET /api/job-offers` stays for callers that need every row. |
> | `POST /api/recruitment-dashboard/sweep` | `HR.Policy.RecruitmentAdmin` | Runs the nightly lifecycle sweep now, for this tenant — the same code path the host runs (G-2.4, G-6.2). Admin rather than Write: it changes statuses across the whole tenant at once. |
> | `GET /api/job-candidates?search=` | `HR.Policy.RecruitmentRead` | G-7.6 — the register's first real search. Same multi-field predicate the talent pool always had. |
>
> **Changed:** `POST /api/position-vacancies/{id}/raise-requisition` gained
> `HR.Policy.RecruitmentWrite` (G-3.3); `POST /api/job-offers/{id}/recall` now takes the caller's
> employee id so the preparer-only rule can be enforced without a workflow instance (G-10.4); and
> the recruitment dashboard controller gained `InternalOnly` alongside its permission policy
> (G-15.5).

| Endpoint | Policy | Used by |
|---|---|---|
| `GET /api/position-vacancies/stats` | `HR.Policy.RecruitmentRead` | ch. 2 |
| `GET /api/StaffRequisitions/summary` | `HR.Policy.RecruitmentRead` | ch. 2 |
| `GET /api/job-postings/active` | `HR.Policy.RecruitmentRead` | ch. 2 |
| `GET /api/job-offers/expiring` | `HR.Policy.RecruitmentRead` | ch. 2 |
| `GET /api/position-vacancies/establishment` | `HR.Policy.RecruitmentRead` | ch. 3 |
| `GET /api/position-vacancies` | `HR.Policy.RecruitmentRead` | ch. 3 |
| `GET /api/position-vacancies/{id}` | `HR.Policy.RecruitmentRead` | ch. 3 (notes dialog) |
| `PATCH /api/position-vacancies/{id}/status` | `HR.Policy.RecruitmentWrite` | ch. 3 |
| `PUT /api/position-vacancies/{id}/notes` | `HR.Policy.RecruitmentWrite` | ch. 3 |
| `POST /api/position-vacancies/{id}/close` | `HR.Policy.RecruitmentWrite` | ch. 3 |
| `POST /api/position-vacancies/{id}/raise-requisition` | **none** (G-3.3) | ch. 3 |
| `POST /api/position-vacancies/reconcile` | `HR.Policy.RecruitmentAdmin` (G-3.1) | ch. 3 |
| `GET /api/StaffRequisitions/{id}` | self-or-`RecruitmentRead` | ch. 4 |
| `GET /api/StaffRequisitions/status/{status}` | `HR.Policy.RecruitmentRead` | ch. 4 (unpaged, G-4.6) |
| `GET /api/StaffRequisitions/{id}/budget-check` | `HR.Policy.RecruitmentRead` | ch. 4 |
| `POST /api/StaffRequisitions/budget-check/preview` | none stated | ch. 4 (live check on the form) |
| `POST /api/StaffRequisitions` | **none** — any internal employee | ch. 4 |
| `PUT /api/StaffRequisitions/{id}` | self-or-`RecruitmentWrite` | ch. 4 |
| `POST /api/StaffRequisitions/{id}/submit` | self-or-`RecruitmentWrite` | ch. 4 (auto-approves, G-4.1) |
| `POST /api/StaffRequisitions/{id}/approve` | **workflow definition**, not a role | ch. 4 |
| `POST /api/StaffRequisitions/{id}/reject` | **workflow definition**, not a role | ch. 4 |
| `POST /api/StaffRequisitions/{id}/recall` | requester only | ch. 4 (**no UI**, G-4.3) |
| `POST /api/StaffRequisitions/{id}/hold` | `HR.Policy.RecruitmentWrite` | ch. 4 (**no way back**, G-4.2) |
| `POST /api/StaffRequisitions/{id}/cancel` | `HR.Policy.RecruitmentWrite` | ch. 4 |
| `POST /api/StaffRequisitions/{id}/fulfill` | `HR.Policy.RecruitmentWrite` | ch. 4 |
| `DELETE /api/StaffRequisitions/{id}` | `HR.Policy.RecruitmentAdmin` (G-4.8) | ch. 4 |
| `GET/POST /api/StaffRequisitions/{id}/costs`, `/comments`, `/attachments`, `/history` | Read or Write as appropriate | ch. 4 |
| `GET /api/job-vacancies` (paged), `/status/{status}` | `HR.Policy.RecruitmentRead` | ch. 5 |
| `POST /api/job-vacancies` | `HR.Policy.RecruitmentWrite` | ch. 5 |
| `PUT /api/job-vacancies/{id}` | `HR.Policy.RecruitmentWrite` | ch. 5 (**no UI caller**, G-5.1) |
| `POST /api/job-vacancies/{id}/change-status` | `HR.Policy.RecruitmentWrite` | ch. 5 (guarded by the map) |
| `POST /api/job-vacancies/{id}/transition` | `HR.Policy.RecruitmentWrite` | ch. 5 (fields + status atomically) |
| `POST /api/job-vacancies/{id}/close` | `HR.Policy.RecruitmentWrite` | ch. 5 (**skips the map**, G-5.5) |
| `POST /api/job-vacancies/{id}/close-for-applications` | `HR.Policy.RecruitmentWrite` | ch. 5 |
| `GET /api/applications/pipeline/{vacancyId}/overview` | `HR.Policy.RecruitmentRead` | ch. 5 (inbox = `Guid.Empty`) |
| `GET /api/applications/pipeline/{vacancyId}/stages/{stageId}/applications` | `HR.Policy.RecruitmentRead` | ch. 5 (paged) |
| `POST /api/applications/bulk-move`, `/bulk-pipeline-reject`, `/move-stage` | `HR.Policy.RecruitmentWrite` | ch. 5 (partial results) |
| `POST /api/applications/pipeline/{vacancyId}/run-scoring` | `HR.Policy.RecruitmentWrite` | ch. 5 |
| `GET /api/job-postings/expired-active` | `HR.Policy.RecruitmentRead` | ch. 6 (the overdue view) |
| `GET /api/job-postings/vacancy/{vacancyId}`, `/channel/{channel}`, `/external/{id}` | `HR.Policy.RecruitmentRead` | ch. 6 |
| `POST /api/job-postings` · `PUT /{id}` | `HR.Policy.RecruitmentWrite` | ch. 6 (per-vacancy panel) |
| `POST /api/job-postings/{id}/publish` | `HR.Policy.RecruitmentWrite` | ch. 6 (refuses unless the vacancy is Published) |
| `POST /api/job-postings/{id}/expire` | `HR.Policy.RecruitmentWrite` | ch. 6 (the only manual expiry) |
| `DELETE /api/job-postings/{id}` | `HR.Policy.RecruitmentAdmin` | ch. 6 (must be expired first) |
| `GET /api/job-candidates` (paged), `/all`, `/{id}`, `/{id}/details`, `/talent-pool` | `HR.Policy.RecruitmentRead` | ch. 7 |
| `GET /api/job-candidates/email/{email}` | `HR.Policy.RecruitmentRead` | ch. 7 (returns null, not 404) |
| `GET /api/job-candidates/{id}/cv` · `/photo` | `HR.Policy.RecruitmentRead` | ch. 7 (authenticated, never public) |
| `POST /api/job-candidates` · `PUT /{id}` | `HR.Policy.RecruitmentWrite` | ch. 7 (create checks duplicate email; update does not — G-7.2) |
| `POST /api/job-candidates/{id}/photo` | `HR.Policy.RecruitmentWrite` | ch. 7 |
| `DELETE /api/job-candidates/{id}` | `HR.Policy.RecruitmentAdmin` | ch. 7 |
| ~~`POST /api/job-candidates/{id}/add-to-talent-pool` · `/remove-from-talent-pool`~~ | — | ⚠ **DELETED 2026-09-16** (G-7.4). They set `IsInTalentPool` with no source, reason or review date — the data loss their replacement exists to stop. Use the `TalentPool` endpoints. |
| `POST/PUT/DELETE` on `/{id}/qualifications`, `/work-history`, `/referees`, `/skills`, `/languages`, `/interests` | Write (deletes: Admin) | ch. 7 (**18 endpoints, no UI** — G-7.1) |
| `GET /api/job-applications` (paged, `?vacancyId`) · `/status/{status}` | `HR.Policy.RecruitmentRead` | ch. 8 (status route unpaged, **disclosed**) |
| `POST /api/job-applications` | `HR.Policy.RecruitmentWrite` | ch. 8 (walk-ins, agency, referral) |
| `POST /api/job-applications/{id}/shortlist` · `/unshortlist` · `/waitlist` · `/reject` · `/withdraw` | `HR.Policy.RecruitmentWrite` | ch. 8 (route id overwrites body id) |
| `POST /api/job-applications/{id}/move-to-stage` | `HR.Policy.RecruitmentWrite` | ch. 8 (six transition rules; rewrites Status — G-8.3) |
| `PUT /api/job-applications/{id}/source` | `HR.Policy.RecruitmentWrite` | ch. 8 (advert must belong to the vacancy) |
| `POST /api/job-applications/{id}/score` · `/vacancy/{id}/score-all` | `HR.Policy.RecruitmentWrite` | ch. 8 |
| `GET/POST` `/{id}/test-results`, `/communications`, `/reviews`, `/stage-history` | Read or Write as appropriate | ch. 8 |
| `DELETE /api/job-applications/{id}` | `HR.Policy.RecruitmentAdmin` | ch. 8 |
| **all** of `/api/job-interviews/*` | `InternalOnly` + **per-record service guards** | ch. 9 — no policy attributes; see § 9.2 |
| `GET /api/job-interviews/date-range` · `/status/{status}` | HR or a panelist on that interview | ch. 9 (no paged search) |
| `POST /api/job-interviews` · `/{id}/reschedule` · `/cancel` · `/complete` | **HR role only** (G-9.1) | ch. 9 |
| `POST /api/job-interviews/{id}/send-invites` | HR role only | ch. 9 (invites are never automatic) |
| `POST /api/job-interviews/interviewees/{id}/outcome` | **read access** — any panelist (G-9.3) | ch. 9 |
| `POST /api/job-interviews/interviewees/{id}/score-summaries` · drafts · `finalize` | only as yourself (`EnsureCanScoreAsAs`) | ch. 9 |
| `GET /api/job-interviews/me/panelist-slots` | the caller's own seats | ch. 9 (feeds `/me/panel`) |
| `GET /api/job-interviews/confirm-panelist/{token}` · `/confirm-attendance/{token}` | **AllowAnonymous** + rate-limited | ch. 9 (emailed single-use links) |
| `GET /api/job-offers` (**unpaged, unfiltered** — G-10.3) · `/status/{status}` · `/expiring` | `HR.Policy.RecruitmentRead` | ch. 2, ch. 10 |
| `GET /api/job-offers/{id}/details` · `/letter-preview` · `/application/{applicationId}` | `HR.Policy.RecruitmentRead` | ch. 8, ch. 10 |
| `POST /api/job-offers` | `HR.Policy.RecruitmentWrite` | ch. 10 (**no application-status gate** — G-8.4) |
| `POST /api/job-offers/{id}/submit-for-approval` | `HR.Policy.RecruitmentWrite` | ch. 10 (**auto-approves** — G-10.1) |
| `POST /api/job-offers/{id}/approve` · `/reject-approval` | workflow definition, not a role | ch. 10 |
| `POST /api/job-offers/{id}/recall` | `HR.Policy.RecruitmentWrite` | ch. 10 (**no UI** — G-10.4) |
| `POST /api/job-offers/{id}/issue` | `HR.Policy.RecruitmentWrite` | ch. 10 (mints a token, emails, advances the pipeline) |
| `POST /api/job-offers/{id}/record-response` · `/accept-conditionally` · `/revoke` | `HR.Policy.RecruitmentWrite` | ch. 10 |
| `POST /api/job-offers/{id}/revise` | `HR.Policy.RecruitmentWrite` | ch. 10 (new version; leaves v1 live — G-10.2) |
| `DELETE /api/job-offers/{id}` | `HR.Policy.RecruitmentAdmin` | ch. 10 (Draft only) |
| `GET /api/pre-employment-checks/status/{status}` | `HR.Policy.RecruitmentRead` | ch. 11 (the queue; default matches nothing — G-11.1) |
| `GET /api/pre-employment-checks/offer/{offerId}` · `/{id}/with-items` · `/{id}/items` | `HR.Policy.RecruitmentRead` | ch. 11 |
| `POST /api/pre-employment-checks` · `/{checkId}/items` · `PUT /items/{itemId}` | `HR.Policy.RecruitmentWrite` | ch. 11 (creation is manual — G-11.2) |
| `POST /api/pre-employment-checks/{id}/complete` | `HR.Policy.RecruitmentWrite` | ch. 11 (the only writer of `ChecksCleared`) |
| `GET/POST/PUT /api/pre-employment-checks/items/{id}/reference-responses` | Read / Write | ch. 11 |
| `GET /api/pre-employment-checks/reference-responses/referee/{refereeId}` | `HR.Policy.RecruitmentRead` | ch. 11 (**can never return rows** — G-11.3) |
| `DELETE /api/pre-employment-checks/items/{id}` · `/reference-responses/{id}` | `HR.Policy.RecruitmentAdmin` | ch. 11 |
| `GET /api/job-hires/status/{status}` · `/start-approaching` · `/application/{id}` · `/employee/{id}` | `HR.Policy.RecruitmentRead` | ch. 12 (**no general list**) |
| `POST /api/job-hires` | `HR.Policy.RecruitmentWrite` | ch. 10, ch. 12 (one per application; offer gates apply) |
| `PUT /api/job-hires/{id}/status` | `HR.Policy.RecruitmentWrite` | ch. 12 (`Active` is never chosen here) |
| `POST /api/job-hires/{id}/confirm-start` | `HR.Policy.RecruitmentWrite` | ch. 12 — **writes nine tables, irreversible**; not blocked on Cancelled (G-12.2) |
| `GET /api/talent-pool/candidates` | `HR.Policy.RecruitmentRead` | ch. 13 — **the only real search in the module** |
| `GET /api/talent-pool/analytics` | `HR.Policy.RecruitmentRead` | ch. 13 (six tiles; `ConvertedThisYear` dated by `UpdatedAt` — G-13.1) |
| `GET /api/talent-pool/match/{vacancyId}` · `/candidates/{id}/match-vacancies` | `HR.Policy.RecruitmentRead` | ch. 13 (40/30/20, max 90 — G-13.2) |
| `POST /api/talent-pool/candidates/{id}/add` · `/remove` · status · review-date | `HR.Policy.RecruitmentWrite` | ch. 13 (remove forces `Expired` — G-13.3) |
| `POST /api/talent-pool/bulk` | `HR.Policy.RecruitmentWrite` | ch. 13 (AssignSegment / RemoveSegment / SetStatus, partial results) |
| `POST /api/talent-pool/segments` · `PUT /segments/{id}` | `HR.Policy.RecruitmentWrite` | ch. 13 (**replaces, does not merge** — G-13.4) |
| `DELETE /api/talent-pool/segments/{id}` · `/events/{id}` | `HR.Policy.RecruitmentAdmin` | ch. 13 |
| `GET/POST /api/talent-pool/candidates/{id}/events` | Read / Write | ch. 7, ch. 13 (engagement timeline) |
| `GET /api/recruitment-dashboard/analytics?year=` | `HR.Policy.RecruitmentRead` | ch. 14 — derives from **records, not statuses** |
| `GET /api/recruitment-dashboard` | `HR.Policy.RecruitmentRead` (**no `InternalOnly`** — G-15.5) | ch. 15 — six services aggregated in the controller |

Every recruitment controller also sits behind `InternalOnly`. *Grows as chapters are written.*

### Appendix C — the gaps, and what closed them

**All 70 are closed.** Sixty-eight were fixed 2026-09-15 → 2026-09-16, two (G-7.1, G-12.4) were
found to have been closed already, and two carry a residue that is open by decision — see the
box at the top of this document.

The table below is the original finding in the left column and **what the code does now** in the
right. The chapter sections keep the full diagnosis of each, in the past tense.

**How to read the Fix column.** Where it says *"the sweep"*, that is
`RecruitmentLifecycleSweepBackgroundService` — the recruitment module's first scheduled job, added
for this programme. Where it says *"the fallback"*, that is `HrWorkflowFallbackAuthority`, which
governs who may approve a record whose entity type has no published workflow definition.

| ID | The finding, as recorded | What the code does now | Impact then |
|---|---|---|---|
| G-3.1 | The `HR` role lacks `HR.Recruitment.Admin`, so Reconcile 403s for it — and Reconcile is the only thing that creates vacancies | `AdministerRecruitment` joins `HrStaffGrants`. HR can reconcile, and delete its own drafts, costs, comments and attachments (G-4.8). | **high** |
| G-3.2 | `PositionVacancyInterceptor` does not exist; no departure is ever logged automatically, contradicting the entity's documented guarantee | `PositionVacancyLog.LogDepartureAsync`, called from termination, separation completion, and the movement's apply path. The entity's docs no longer promise an interceptor; they name the call sites and warn that a new exit path must call one. | **high** |
| G-5.1 | There is no way to edit a vacancy; deadline, pipeline and blind screening are fixed for life, and three screens tell the user to change them | `/hr/recruitment/vacancies/[id]/edit`, calling the `PUT` that always existed. All three screens' instructions are now true. | **high** |
| G-4.1 | With no workflow definition published, Submit takes a requisition Draft → **Approved** with no approver and no segregation-of-duties check | Submit asks `HasActiveApprovalWorkflowAsync` first and lands at `Submitted`; `ApproveAsync` and its SoD rule then run. Approve, reject and recall gained no-workflow branches so the record is not stranded instead. | **high** |
| G-4.2 | On Hold has no exit but Cancel; the dialog claims it can be taken off hold later | Addressed as a principle rather than a patch: the vacancy's new `OnHold` (G-5.3) has a way out to every live status, and the lesson is recorded on that map. ⚠ The requisition's own hold is **unchanged** — see the note under G-5.3. | **high** |
| G-10.1 | Submit approves an **offer** outright with no approver — the same auto-approve as G-4.1, on the step that authorises binding terms to an outsider | Same fix as G-4.1, landing at `PendingApproval` — which nothing could previously produce, so Approve was unreachable. The offer also gained the segregation-of-duties check it never had. | **high** |
| G-2.4 | No offer is ever marked Expired, and the "expiring soon" query has no lower bound — the tile grows without bound and is permanently amber | The sweep writes `Expired`. The query is bounded at both ends and filters `IsLatestVersion`, so a sweep that does not run cannot re-inflate the tile. | **high** |
| G-10.2 | A revised offer leaves v1 in `Sent`/`Negotiating` for ever; nothing filters on `IsLatestVersion` | `ReviseOfferAsync` moves the superseded version to the new `JobOfferStatus.Superseded`. Distinct from `Withdrawn`, which is the organisation taking live terms back. | medium |
| G-11.1 | The checks queue defaults to `InProgress`, which nothing writes — the screen is inert in production and populated only in the demo | A check set moves `Pending → InProgress` when its first item is started. One-way: it never moves back. | medium |
| G-11.2 | Accepting conditionally with no check set strands the offer at `ConditionallyAccepted`; no hire is ever possible and nothing says why | `AcceptConditionallyAsync` refuses without a check set, and the message names the way out. The condition in "conditionally accepted" *is* the check set. | medium |
| G-12.1 | Confirming a start never marks the application Hired — the application register and the vacancy's Hired tile both understate reality | `ConfirmStartAsync` advances the application to the Hired stage (which also moves `HireCount`), falling back to a direct status write when the vacancy has no pipeline. | medium |
| G-12.2 | A **Cancelled** hire can still be confirmed, creating an employee and burning a number, irreversibly | Refused server-side and hidden client-side. The old guard asked "already done?"; nothing asked "called off?". | medium |
| G-13.1 | "Converted" is never set automatically and is dated by `UpdatedAt` — the pool's only measure of whether it works is unreliable twice over | Confirming a start converts a pooled candidate out of the pool, and the tile dates off `TalentPoolRemovedDate` — written at the moment of exit, never moved by an unrelated edit. No new column needed. | medium |
| G-13.2 | The match rubric awards points for *missing* data, so an empty candidate profile tops the list | Three tiers: a genuine match scores full, unconstrained-but-evidenced scores partial, silence scores little. Applied symmetrically to both matchers, and `MatchScoreMax` gives the score a denominator. | medium |
| G-14.1 | The analytics Expired bucket is structurally zero; lapsed offers show as a large *Pending* pipeline instead | Fixed by G-2.4 with **no code change here** — the service already counted `Expired` correctly and already filtered `IsLatestVersion`. It was zero only because nothing wrote the status. | medium |
| G-14.2 | "Seats standing empty" is only as good as the last Reconcile — which the HR role cannot run (G-3.1) | Fixed by G-3.1 and G-3.2 together: HR can reconcile, and departures now log themselves without waiting for one. | medium |
| G-15.1 | The dashboard Pipeline card compares three counts of **people** with one count of interview **sessions**, on a shared scale | All four bars count people, all-time, on the same vacancy set. `InterviewCount` was maintained on the entity all along and merely missing from the summary DTO. | medium |
| G-15.2 | "Offers pending response" counts lapsed and superseded offers — the analytics screen filters for exactly this and the dashboard does not | Filters `IsLatestVersion`; both underlying writers (G-2.4, G-10.2) are fixed too. | medium |
| G-15.4 | The card headed "Shortlisting SLA breached" fires on the **application** deadline and stays lit for the vacancy's life | Fires on `ShortlistingDeadline`. A vacancy with none raises no alert — an SLA nobody agreed cannot be breached. | medium |
| G-5.2 | `TestScoreWeight` and `InternalCandidateBoostPoints` are in no write DTO, so two branches of the scoring algorithm are unreachable | Both on the create, update and transition DTOs and on both forms. Default 0, so an untouched vacancy scores exactly as it used to. | medium |
| G-5.3 | `OnHold` is unreachable entirely; `PendingApproval` and `Rejected` are unreachable from the UI | `OnHold` added to the transition map **with a way out to every live status** — the G-4.2 lesson, applied where the map was being changed anyway. `PendingApproval` and `Rejected` are reachable because the picker now offers whatever the server says is legal. | medium |
| G-5.4 | Vacancy approval has no workflow, no separate approver and no segregation of duties | `GuardApprovalSeparation` refuses approval by the person who opened the vacancy. Deliberately narrow: it says who may *not* approve, not who may — a named chain means putting `JobVacancy` on the engine. | medium |
| G-5.6 | Auto-created adverts are published with an empty body and a reference-number title | Composed from what the vacancy already knows, titled by the position where no custom title is set. Plain text, because the same string renders on three surfaces. Salary appears only when `IsSalaryVisible`. | medium |
| G-6.1 | A posting's `ApplicationCount` is never written, so the one column answering "which channel works?" is always 0 | Derived at read time from the applications that name the posting — one grouped query per page. Deliberately **not** a maintained counter: that is G-4.5's shape. | medium |
| G-6.2 | Nothing expires an advert on its closing date — only a manual click or a vacancy status change | The sweep expires them; the "live adverts" query is bounded anyway (G-2.2). | medium |
| G-6.3 / G-11.4 | Two queue screens diagnose and cannot cure — overdue adverts, and outstanding clearances — neither offers an action on what it finds | Adverts got a per-row **Expire** and a bulk **Take them all down** — the endpoint and client method already existed. The clearance queue got deep links to `?tab=checks` rather than a bulk action: a clearance result is evidence about a named person, and the defect was the three clicks, not the absence of a bulk control. | medium / low |
| G-7.1 | Six candidate tabs are read-only; 18 write endpoints have no UI, so HR cannot add a referee for a reference check | ⚠ **Already closed when checked.** All six tabs ride `ResourceCollectionTab` with create, update and remove wired. The finding appears to have been recorded against a stale checkout. | **high** |
| G-6.4 | The six print-advert copy fields are typed all the way to the browser and used by no component | Composed in the advert dialog, shown for Newspaper and Radio, and cleared when the channel changes so print copy cannot be filed against a job board. | low-medium |
| G-9.1 | Interviews authorise on the **HR role**, not recruitment permissions — `TenantAdmin`, `Admin` and `HR User` are refused outright | `IsHr` asks the permission question via `HrPermissions.RolesGrantAny`. No role gains access it did not already hold everywhere else in the module. | medium |
| G-9.2 | `JobInterviewee.Outcome` — the panel's verdict — is written and read by nothing downstream | Closing an interview reads it to decide where each application goes. It finally has a consumer, which is what makes recording it worth doing. | medium |
| G-9.4 | Closing an interview has no downstream effect; applications sit at `InterviewScheduled` until moved by hand | `CompleteAsync` advances each attendee's application on the panel's verdict. It never *rejects*: that carries a reason and a notification, and is HR's act through the decision bar. | medium |
| G-8.1 | `IsShortlisted` is computed two different ways; after a stage move HR sees "shortlisted" and the candidate sees "not" | One definition, and it is the date. Being shortlisted is an event that happened; the process moving on does not un-happen it. | medium |
| G-8.3 | A stage move silently overwrites the decision status — two systems write one column, the move wins | The shortlist case is now harmless (G-8.1 made both sides read the date). A move against a **terminal** application — Rejected, Withdrawn, Hired — is refused, guarded before anything is written. | medium |
| G-8.4 | An offer can be raised on an application at any non-terminal status; nothing gates on the step before | ⚠ **Narrowed deliberately.** The flexibility is kept — a search-firm hire legitimately skips the funnel — and only the unambiguous case is refused: an offer against a Rejected, Withdrawn or Hired application. | medium |
| G-7.2 | Candidate email uniqueness is checked on create only, and the email index is not unique | `UpdateAsync` now refuses a duplicate. ⚠ **The index is still not unique** — see the box at the top of this document. | medium |
| G-7.6 | No name search anywhere on the candidate register — exact email or paging, nothing else. **The talent-pool screen already has a server-side multi-field search (ch. 13); that endpoint is the fix** | Exactly that: the register uses the same predicate, so two searches over one table cannot disagree about what matches. The exact-email box stays, answering a different question. | medium |
| G-13.3 | A candidate marked Converted stays in the pool; only *Remove* clears the flag, and it overwrites Converted with Expired | `ApplyPoolExit` takes the exit status as a parameter. Leaving the pool and why you left are now separate facts, so both can be true. | low-medium |
| G-13.4 | Segment metadata is replaced rather than merged — any client omitting the four lane-V fields clears them | Behaviour unchanged — clearing an owner has to be possible — but the whole-record contract is now stated on the DTO, where a new client reads it, instead of only in the page's comment. | low |
| G-14.3 | Two screens define "an open seat" differently, by one status (`Anticipated`) | Both kept, because they answer different questions, and both now say so. ⚠ The difference used to be invisible because nothing wrote `Anticipated`; G-3.6 changed that, so these numbers will genuinely diverge from now on. | low |
| G-14.4 | The Funnel card is drawn as a conversion ramp but counts each application against every stage it reached | Retitled *"How far applications got"* rather than redrawn. The caption was already correct; of a correct caption and a misleading shape, the shape is what gets believed. | low |
| G-15.3 | The dashboard payload puts the literal text "Round N" in the interviews list's `CandidateName` field — latent, not rendered today | Null, and the field is nullable. A session holds several candidates, so there is no single name to give; `CandidateCount` already says how many. | low-medium |
| G-15.5 | The dashboard is the only recruitment controller without `InternalOnly` alongside its permission policy | Paired, like every other recruitment controller. | low |
| G-7.3 | `Nationality` is displayed, written only by the demo seeder, and settable by nothing | On both write DTOs and the form. Free text, because nationality is not the same question as country of residence. | low-medium |
| G-7.4 | Two superseded talent-pool endpoints remain live and lose source/reason/review date | Both endpoints and both client methods deleted. The service methods are kept but marked: do not wire a new door onto them. | low |
| G-9.5 | "Panel score" on the application page is the *shortlisting* panel's average, not the interview panel's | Relabelled *"Shortlisting panel score"*, and the hint says "not the interview panel". | low |
| G-9.6 | No single screen shows "interviews I am involved in" — HR's diary and `/me/panel` are separate | The two now link both ways. A merged screen was considered and rejected: the lists carry different authority, and one screen that changes shape with the viewer's role is harder to reason about than two that link. | low |
| G-7.5 | The candidate page's "HR-only" comment overstates a gate that is permission-based | Comment corrected to describe the gate that exists, and to say that narrowing it is a controller decision, not a role check on one page. | low |
| G-4.3 | Recall is fully built server-side and has no UI control at all; the edit page tells users to use it | `useWorkflowRecord` gained a recall command and `WorkflowApprovalActions` a button and dialog. Gated on who raised it, not on permissions. The edit page's advice now points at a control that exists. | medium |
| G-4.4 | A **third** definition of "filled" — the requisition check counts `IsActive` only, ignoring `StaffStatus` | All three call sites use `HrServingEmployees.Predicate`. Three definitions became one. | medium |
| G-4.5 | **Corrected ch. 12** — `PositionsFilled` has two writers (manual absolute, automatic increment) that are not reconciled, so a manual entry plus a confirmed start double-counts | The automatic path **counts confirmed hires** instead of incrementing, and the manual path refuses a number below that count. A confirmed hire is a fact; a typed number is a claim that may exceed the facts but not contradict them. | low-medium |
| G-12.4 | Referees carry over to the employee, but HR could never add one to the candidate (G-7.1), so a hand-typed candidate arrives with none | ⚠ **Already closed**, with G-7.1. | low-medium |
| G-4.8 | Four destructive actions need `RecruitmentAdmin`, which no HR role holds | Follows from G-3.1. | medium |
| G-3.3 | `raise-requisition` carries no permission policy; the create half is deliberate, but pinning a vacancy to `RequisitionRaised` is not | Gated on `RecruitmentWrite`. The ungated *create* endpoint stays ungated — line managers raise requisitions. | medium |
| G-3.4 | Reason / Vacated by / Since are fixed values on every reconcile-created row; "Since" means "when a button was pressed" | Departure-logged rows carry the real reason, employee and date. Reconcile rows leave them **honestly empty** and say so in the note — reconcile sweeps for gaps nobody logged, so by construction it cannot know. A later departure upgrades the row rather than opening a second. | medium |
| G-3.5 | `Classification` is always `WithinEstablishment`; two of its three values are never written and `noShortfallOrOver` is always 0 | Computed three ways against establishment. A post nobody established classifies as `NoShortfall`, not as a shortfall. | medium |
| G-3.6 | Anticipated vacancies are never detected; the tile is zero unless a human overrides a row into it | A separation submitted with a future effective date opens an `Anticipated` row; the sweep promotes it to `Open` on the day, and an earlier real departure promotes it too. | medium |
| G-3.8 | The status dialog closes a gap without the reason the dedicated close path requires | The override path asks for a reason when the target is `Closed`, and writes it to `ClosedReason` rather than only to the note. | medium |
| G-3.10 | Same role-vs-permission mismatch as G-2.3; here reads are ungated so the tables show but nothing can be acted on | Permission-gated, with Reconcile gated on `Admin` specifically so it is not drawn for someone it will refuse. | medium |
| G-2.2 | "Live adverts" ignores `ExpiryDate`; expired postings still count as live | The query respects `ExpiryDate`, and the sweep writes the status too. | medium |
| G-2.3 | Page gates on roles (`SuperAdmin`/`HR`) while the API gates on permissions — `TenantAdmin`, `Admin` and `HR User` are served but shown nothing | Five pages moved onto permissions — the two named, plus three more found by sweeping for the same shape. | medium |
| G-3.7 | Two of six options in the status dropdown are always refused by the service | The dropdown offers the four the service accepts. | low |
| G-3.9 | Two different definitions of "filled" — one call site bypasses the shared `HrServingEmployees` predicate (see G-4.4 for the third) | On the shared predicate, with G-4.4. | low |
| G-5.5 | `Cancel` skips the transition map, so a Filled vacancy can be cancelled through the API | `CloseAsync` goes through `GuardTransition`. It was the third door into a status change that bypassed the map. | low-medium |
| G-10.4 | Offer recall is built server-side with no UI caller — the second instance of G-4.3 | Wired with G-4.3. Reachable only because Submit now lands at `PendingApproval` — an approved offer is past recalling. | low-medium |
| G-10.5 | The salary-band check silently does nothing on a position with no salary grade | The offer screen says so: *"No band on this position — the salary was not checked against one"*. The band was on the DTO all along; silence read as "checked and fine". | low-medium |
| G-11.3 | A reference reply is never linked to the referee who was nominated; only the demo seeder sets `RefereeId` | A picker on *Record a reference* fills the snapshot fields and sets `RefereeId`. Typing over the name clears the link, since the snapshot would then describe someone else. | low-medium |
| G-9.3 | Any panelist can record the panel's verdict, overwrite another's, and do so without signing off their own scorecard | Recording the verdict is now an `EnsureHr` write, like every other write on the interview record itself. | low-medium |
| G-8.2 | The application decision bar renders every action for any viewer; the same page gates other actions on permissions | Renders behind `hasAnyPermission`, as *Record an application* on the same page already did. | low-medium |
| G-8.5 | A walk-in can only be recorded against a candidate found by exact email; no inline create | Searches by name, email or phone (G-7.6's endpoint), lists multiple matches to pick from, and offers a create link in a new tab so the half-filled dialog survives. | low-medium |
| G-10.3 | The offers list's **default** view is unpaged and unfiltered — every offer in the tenant, in one response | `GET /api/job-offers/paged` plus a pager. The status and expiring views keep their unpaged reads and now disclose it. | low |
| G-4.6 / G-5.8 | Filtering by status silently drops paging and returns every matching row (requisitions and vacancies alike) — **the applications list discloses the same behaviour; copy that** | Copied, onto both lists and the offers list. | low |
| G-5.7 | The "Advance to…" dropdown offers illegal transitions; they fail informatively | The vacancy DTO carries `AllowedNextStatuses`, computed from the same map the write guards with. Sent rather than mirrored, because a mirrored constant drifts — and the map changed in the same commit. | low |
| G-4.7 | The edit page omits the `numberOfPositions >= 1` client check the new page has | Added. | low |
| G-2.1 | A failed query and a pending query both render an em dash | Three states, three appearances: `…` loading, `—` no figure, and a banner naming the failure. | low |
| G-2.5 | The five counters carry no `href` despite `MetricTiles` supporting one | All five link to the queue they name. | low |

---

## The six patterns

> These were the most valuable output of the walk: not the individual findings, but the *shapes*
> they came in. All six are written below as they were found, in the past tense. **Every instance
> named is fixed** — the patterns are kept because they are what to look for in the next module,
> and because each one now carries a note on what closing it actually cost.

**A pattern worth naming.** Seven of the gaps were the same shape: a screen telling the user
something the code did not do — the entity docs promising automatic vacancy logging (G-3.2), the
hold dialog promising reversibility (G-4.2), the requisition edit page recommending a control that
did not exist (G-4.3), the detail page's comment claiming Submit was inoperable when it
auto-approved (G-4.1), and three separate screens in chapter 5 directing the user to vacancy
settings that were never built (G-5.1). When reading this system, the prose was not evidence.

> **What closing it took.** In five of the seven the *prose* was right about what should happen and
> the code was wrong, so the fix was to build the thing the screen promised. Only in G-4.1 and
> G-10.1 was the prose itself the dangerous half — it claimed Submit was inoperable when it
> auto-approved, which is the difference between "this does nothing" and "this does the most
> consequential thing unreviewed". **When prose and code disagree, ask which one is more dangerous
> to believe** before deciding which to change.

**A second pattern.** Several features were complete on the server and unreachable from the
browser: requisition recall (G-4.3), vacancy update (G-5.1), the test-score blend and internal
boost (G-5.2), three vacancy statuses (G-5.3), the print-advert copy block (G-6.4 — typed all the
way into `types/hr/recruitment.ts` before stopping). The backend was consistently ahead of the UI,
so "does the endpoint exist?" and "can anyone use it?" were different questions throughout.

> **What closing it took.** Much less than the count suggests — most were a form, a button or a DTO
> field away, because the hard half was already written and tested. The exception was G-5.1, which
> needed a whole page. **A long list of unreachable features is cheap to close and expensive to
> leave**, because each one is a feature the team believes it has.

**A third pattern: date-driven statuses that only a human could write.** An offer never reached
`Expired` (G-2.4); an advert never reached `Expired` on its closing date (G-6.2). In both cases
the deadline passed, the record kept its live status for ever, and every count built on that
status drifted. There was no scheduled job anywhere in the recruitment module.

> **What closing it took.** One background service — `RecruitmentLifecycleSweepBackgroundService`,
> the module's first — plus bounding the readers anyway, so a sweep that fails to run cannot
> re-inflate a tile. Both halves were deliberate: the failure mode being designed against is
> silence, and **nobody noticed the absence of that job for the life of the module.**

**A fourth pattern: demo data can conceal a gap.** Three findings now share this shape, each
written by the TDC demo seeder and by nothing else in the application:

| Field | Real tenant | Demo tenant |
|---|---|---|
| `JobCandidate.Nationality` (G-7.3) | always "—" | "Ghanaian" |
| `PreEmploymentCheck.OverallStatus` = InProgress / CompletedWithCaution / Waived (G-11.1) | never occurs — the queue's default view is empty | all six statuses present |
| `ReferenceCheckResponse.RefereeId` (G-11.3) | always null | linked |

When a field looks fine on the demo tenant, check who writes it before concluding it works. The
reverse check is just as useful: a field that is always empty on a real tenant may simply have no
writer. G-11.1 was the sharpest case — a whole screen that worked in a demo and was inert in
production.

> **What closing it took.** All three now have real writers. But note what the pattern cost
> *before* it was found: three features passed every walkthrough and demo they were ever shown in.
> **A demo tenant is a test of the screens, not of the writers** — and the seeder is a writer that
> production does not have.

**A fifth pattern: facts recorded with no consumer.** Several fields were written faithfully and
read by nothing downstream — `PositionVacancy.Classification` (G-3.5), `JobPosting.ApplicationCount`
(G-6.1, derivable and never derived), `StaffRequisition.PositionsFilled` (G-4.5, typed by hand and
never reconciled against hires), and `JobInterviewee.Outcome` (G-9.2), the panel's verdict on a
candidate. In each case the screen showed the value, so it looked like it worked; nothing decided
anything with it. When documenting a field, ask not only who writes it but who *reads* it.

> **What closing it took, and one thing it revealed.** Two of the four were fixed by giving the
> field a *reader* (the interview outcome now routes the application; the classification is
> computed and counted). The other two were fixed by deleting the stored fact and **deriving it**
> — `ApplicationCount` from the applications, `PositionsFilled` from the confirmed hires. The
> choice between the two is worth naming: give a field a reader when it records a *judgement*
> somebody made, and derive it when it merely *counts* something the database already knows. A
> counter that duplicates countable rows will drift, and G-4.5 is the proof.

**A sixth pattern: steps that did not hand over to the next one.** Closing an interview moved no
application (G-9.4); an offer could be raised on an application at any status (G-8.4); a stage move
rewrote the decision status rather than reconciling with it (G-8.3). The chain in § 1.1 was a
chain of *records*, not of *state transitions* — each link mostly advanced by hand.

*Two counter-examples worth holding onto*, because they showed the intended shape: **booking a
candidate into an interview** and **issuing an offer** both call
`AutoAdvanceToStageTypeAsync`, so the pipeline moves with the act. Where a step does hand over, it
does it this way — and it is the mechanism the fixes below adopted.

The pattern's sharpest instance was at the very end of the chain: **confirming a start** writes nine
tables and creates a real employee, yet never marked the application `Hired` (G-12.1). It *did*
advance the requisition's fill counter (G-4.5) — so the same action handed over in one direction and
not the other, which is what made it a consistency problem rather than a missing feature.

> **What closing it took — and where it was deliberately stopped.** Confirming a start now marks
> the application Hired and converts the candidate out of the talent pool; closing an interview
> advances its attendees on the panel's verdict. But **two hand-overs were deliberately not
> built**, and the reasoning is the useful part:
>
> - Closing an interview does **not** reject anybody, even on a `NotRecommended` verdict.
>   Rejecting records a reason and drives the candidate-notification flow; a rejection arriving as
>   a side effect of closing a session would produce rejections nobody decided and no one can
>   explain.
> - Raising an offer still does **not** require the candidate to have been shortlisted or
>   interviewed (G-8.4). A senior hire through a search firm legitimately skips the funnel, and a
>   rule that refuses real work is a worse defect than the one it fixes.
>
> **Automating a hand-over is right when the next state is a fact; it is wrong when the next state
> is a decision.** Every hand-over added here moves a record to where the evidence already says it
> is. None of them decides anything about a person.

**A note on method.** **Four** findings in this document were recorded wrongly. Two were caught
during the walk itself; two more were caught during the closure pass, by the same habit:

- **G-4.5** (ch. 4 → corrected ch. 12): a search pattern that failed to match a `++` increment
  produced *"written in exactly one place"*, and the claim survived until the hire flow was read
  directly. Where a gap rests on *"X is never written"*, it is only as good as the search behind it
   — prefer reading the writer to grepping for the absence of one.
- **G-10.2** (ch. 10 → corrected ch. 14): the consequence list asserted that analytics double-counts
  superseded offers. It does not — the analytics query filters on `IsLatestVersion`. A defect in a
  *writer* does not automatically mean every *reader* is wrong; check each reader.
- **G-7.1 and G-12.4** (caught 2026-09-16, before any code was written against them): recorded as
  *"680 lines containing `useQuery` and nothing else — no `useMutation`, no `mutationFn`, no
  buttons"*, with eighteen write endpoints unreachable. **The file did not contain that.** All six
  tabs ride `ResourceCollectionTab` with create, update and remove wired; the work had landed days
  before the walk, which appears to have read a stale checkout. G-12.4 was recorded as a downstream
  consequence of G-7.1, so it fell with it.

- **The seeding premise under G-4.1, G-10.1 and the auto-approve note** (caught 2026-09-16, while
  extending the fix to the rest of HR): all three said no HR workflow definition was seeded
  anywhere in the solution. **`EnsureHrWorkflowsSeededAsync` seeds 28 of them**, published and
  active, covering every entity type named — and it landed 2026-09-02, **twelve days before this
  walk**. So the auto-approve fired only on a tenant where seeding had not run. The findings are
  still correctly closed, and the offer's missing segregation-of-duties check was real regardless;
  what was wrong was "on every tenant, out of the box".

> **The habit that caught all five is the same one**, and it is worth more than any individual
> finding here: **before acting on "X does not exist", open X.** Some were caught by the author
> mid-walk and some by the person closing them; in both directions the cost of not checking was
> building something that already existed, or documenting a defect that was not there.
>
> **The fifth is the one to learn from**, because it was not a detail inside a finding — it was the
> *premise* under six of them, it was stated with confidence in three places, and it survived a
> full closure pass before anyone opened `DatabaseSeedingService`. A `grep` that finds nothing is
> not evidence of absence; it is evidence about the `grep`. A gap list is a set of claims about
> code, and claims decay — but a claim that was never true does not decay, it just propagates.

**The counter-example.** Chapter 14 is the reply to most of this appendix.
`RecruitmentAnalyticsService` derives its figures from **records rather than statuses** — hired
from the existence of a hire record, shortlisted from a date, interviewed from actual bookings,
offers from the latest version only. That one decision makes it immune to G-8.3, G-9.4, G-10.2 and
G-12.1 simultaneously. Where a module's statuses drift, reporting off the records is the defence,
and this service is the worked example of it.

**The auto-approve finding is systemic, not local.** G-4.1 (requisitions) and G-10.1 (offers) are
the same mechanism: `WorkflowIntegrationService.SubmitAsync` returns `WorkflowOutcome.Approved`
whenever no active definition exists for the entity type, and each adapter maps that to its own
"approved" status. Every HR record wired to the generic engine behaves this way until a definition
is published for it, and in both cases documented here the screen's own comment claimed the
opposite. Treat "this record uses the workflow engine" as a reason to check for a published
definition, not as evidence that approval happens.

> **This was the largest thing the walk found, and it was bigger than the walk knew.** Written on
> the strength of two instances, it turned out to span **27 submit call sites across 22 HR
> services** — and before the closure pass, `HasActiveApprovalWorkflowAsync` was called by *none*
> of them. Recruitment's two are fixed, along with `StaffMovement` (which also stamped the
> submitter as their own authoriser), `PerformanceImprovementPlan` (whose adapter maps Approved to
> **Active**, putting an unreviewed plan into force) and `EmploymentActionProposal`. **Roughly
> seventeen remain**, tracked in the closure plan; `EmployeeSalaryChangeRequest` and `Separation`
> are the two worth doing first.
>
> **The trap in fixing it, for whoever takes the rest.** Making Submit stop at a pending status is
> one line and it is *not enough* — it makes things worse on its own. `CanUserApproveAsync` returns
> false with no workflow instance and `RecallWorkflowAsync` answers *"No active workflow found"*,
> so the record becomes unapprovable, unrejectable and unrecallable, with cancellation its only
> exit. That is G-4.2's shape, reproduced. **Submit, approve, reject and recall have to be done
> together**; `HrWorkflowFallbackAuthority` documents the four-part shape and who rules when no
> definition exists.

**Running count of the second pattern** (built but unreachable): requisition recall (G-4.3),
vacancy update (G-5.1), the two scoring weights (G-5.2), three vacancy statuses (G-5.3), the
print-advert block (G-6.4), two superseded talent-pool endpoints (G-7.4). It was the single most
common finding in this module — and, per the note on the second pattern above, the cheapest class
to close. G-7.1's eighteen candidate sub-resource writes were originally counted here and should
not have been; see the note on method.
