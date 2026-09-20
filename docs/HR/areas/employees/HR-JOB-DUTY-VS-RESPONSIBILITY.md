# Job Duty vs Job Responsibility — What Goes Where in Job Analysis

**Generated:** 2026-09-07
**Purpose:** The distinction between a *duty* and a *responsibility* in standard job analysis, and
how the two are modelled and consumed in this system — so an HR author filling in a job description
knows which tab a statement belongs on, and why it matters where it lands.

**Audience:** HR officers authoring job descriptions; developers touching `JobAnalysisEntities.cs`.

---

## 1. The standard distinction

> **A responsibility is an outcome you are answerable for. A duty is an activity you perform.**

Job analysis nests them:

```
Job
 └── Responsibility  (key result area — what the post is answerable for)
      └── Duty / task  (the recurring activity that discharges it)
           └── Step / element  (not modelled here, and rightly so)
```

A responsibility is *why the post exists*. The duties are *how it gets discharged*.

| | Responsibility | Duty / task |
|---|---|---|
| Expresses | accountability, an end result | an observable, recurring activity |
| Phrasing | "Ensures…", "Is accountable for…" | an action verb: "Reconciles…", "Inspects…" |
| How many | few — typically 5–8 | many — often 15–30 |
| Measured by | a KPI or standard (*how well*) | completion (*whether it was done*) |
| Stability | survives process and system change | changes when the process or tool changes |
| Weighted? | yes — % of time, importance | no |

### The quickest test

**Ask what happens when it goes wrong.**

- "The post-holder is answerable for the result" → **responsibility**.
- "That particular task didn't get done" → **duty**.

### Worked example — Accounts Officer

**Responsibility:** *Ensures the general ledger is complete and accurate, and closes within the
monthly timetable.*

**Duties under it:**

1. Reconciles all bank accounts by the 5th working day.
2. Posts and files supplier invoices daily.
3. Prepares the monthly ledger schedules for review.

One responsibility, several duties. **If you cannot name the responsibility a duty serves, either
the duty does not belong to this job or you are missing a responsibility.** That test is the whole
value of keeping the two apart.

---

## 2. How this system models them

The two live on separate tabs of a job description and feed **different machinery**. This is not a
presentation choice — putting a statement on the wrong tab changes what the organisation can do
with it.

### `JobResponsibility` — the analytical layer

`JobAnalysisEntities.cs`

| Field | Why it is there |
|---|---|
| `ResponsibilityDescription` | the accountability statement |
| `Type` | `Core` / `Secondary` / `Occasional` |
| `PercentageOfTime` | how much of the job this accountability is |
| `ImportanceWeight` | how much it matters, independent of time spent |
| `Kpis` | how the outcome is judged |
| `Qualifications`, `Competencies` | what discharging it requires |

**What reads it:** appraisal criteria, the competency gap analysis, and job valuation — the monetary
values that produce the estimated salary range hang off qualifications and competencies, which hang
off responsibilities. **None of it leaves the HR module.**

### `JobDutyItem` — the contractual enumeration

| Field | Why it is there |
|---|---|
| `SequenceNumber` | the order the schedule is printed in |
| `DutyStatement` | the duty itself |
| `Notes` | internal annotation |

**What reads it:** the **offer letter**.
[`OfferLetterService.BuildDutiesList`](../../src/ErpSystem.Core/Services/HR/OfferLetterService.cs)
loads `DutyItems` only, orders them by `SequenceNumber` and prints them as the bulleted duties
schedule. Responsibilities are never loaded there.

> ⚠ **The XML comment on `JobDutyItem` describes the split backwards.** It calls duties "the *what
> the job is*" statements and responsibilities "the specific tasks performed" — the reverse of the
> convention in §1. The **structure** follows the standard convention (weights, KPIs and
> requirements hang off responsibilities; duties are a flat numbered list that gets printed), and
> the structure is what the rest of the system acts on. Read the shape, not the comment.

---

## 3. So, practically

**Duties tab** — write the numbered schedule you would be content to see in an appointment letter,
because that is literally where it goes. Plain contractual statements, in a deliberate order.

**Responsibilities tab** — write 5–8 accountabilities, weight them, and hang the KPIs,
qualifications and competencies off each. This is what appraisal and competency gap analysis read,
and what the valuation adds up.

### Consequences of getting it wrong

| Mistake | What breaks |
|---|---|
| Accountabilities written as duties | they print in the offer letter, and appraisal has no criteria to score — the KPIs have nothing to hang off |
| Tasks written as responsibilities | the weighting becomes meaningless (30 rows at 3% each), and the competency requirements scatter across trivia |
| Duties left empty | the offer letter's duties schedule prints nothing at all |

---

## 4. Three things to watch

**A qualification or competency may be attached to a responsibility — and only to one of its own
job description's.** That attachment is what turns a requirement into an argument: *the job needs
this degree BECAUSE of that accountability.* "The job as a whole" is a legitimate answer, not an
empty field. It is editable (2026-09-07), it shows on both lists, and the API refuses a
responsibility belonging to a different document — which nothing checked before, so a requirement
could be filed under another document's accountability with the foreign key perfectly satisfied.

**Nothing validates that `PercentageOfTime` totals 100.** The column is free, and no service checks
the sum across a job's responsibilities. That is a convention HR has to hold — or a rule worth
adding to `JobDescriptionService`.

**The duties list is contractual.** A change to it is a change to what people were engaged to do,
which is why a duty edit goes through a **new version** rather than being tweaked in place. That is
now enforced on **both** sides: the authoring panels stand down outside `Draft` and `UnderRevision`
(`AUTHORABLE_JOB_DESCRIPTION_STATUSES`), and every child write calls
`RequireAuthorableJobDescriptionAsync` — or `RequireAuthorableForResponsibilityAsync` for a KPI,
which reaches its document through its responsibility — and refuses with a 409 naming the new-version
route. Ledger defect D-03 (child writes accepted on an approved description) is **closed**; earlier
notes in this folder that describe it as open are stale.

---

## 5. Related documents

| Document | Focus |
|---|---|
| [`HR-MODULE-INTEGRATION-MAP.md`](../../integration/HR-MODULE-INTEGRATION-MAP.md) | How HR entities reach other modules. |
| [`HR-PAYROLL-BOUNDARY.md`](../../integration/HR-PAYROLL-BOUNDARY.md) | Who owns grades and pay components. |
| `plans/HR-Area-17-Job-Architecture-Competency-Establishment-Build-Plan.md` | The area-17 build plan and its measured ground truth. |
