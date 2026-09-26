# Employee Bulk Import — Design and Column Mapping

**Written:** 2026-09-03 · **Status:** phase-0 decisions taken 2026-09-03 (see §7), phase 1 in progress
**Input analysed:** `records shared/Employee Records ERP - Sample.xlsx` (TDC's register, two sheets) and
`records shared/ERP Salary Scale 2026.xlsx` (eight level sheets, S1–S3 and M1–M5, notches 1–n)
**Outputs of this analysis:** `records shared/Employee Import Template - DRAFT.xlsx` (the proposed
template) and `records shared/Employee Records ERP - Sample (converted to draft template).xlsx` (the
sample rewritten into it, with a Conversion Notes column per row)
**Parent:** [`HR-IMPORT-EXPORT-CATALOGUE.md`](../../catalogues/HR-IMPORT-EXPORT-CATALOGUE.md) §4.1 and §5 — this document is
the concrete design for the first item in that catalogue's build order.

---

## 0. The three answers

**1. Is TDC's current spreadsheet an acceptable import format? No.** It is a printed register, and a
good one, but it is not a machine-readable file:

| What the sample does | Why it cannot be the import format |
|---|---|
| Two sheets with two different column sets (16 vs 28 columns) | The employment type should be a column, not a sheet. Every rule would have to exist twice. |
| Two-row merged header on Permanent, one-row header on Contract | A merged header cannot be matched to a field by name. |
| Department banner rows ("Audit", "MIS", "Estate") interleaved with data rows | Every banner would be read as an employee with no name. |
| "Other Names" holds first and middle names together ("John L. N.", "Col. Augustus") | The system stores first, middle and title separately. Splitting is a guess. |
| Dates are real date cells on Permanent but free text on Contract ("10-July-1991", "19-nov-2025") | Text dates parse only by luck. Day-month order is ambiguous once a numeric form appears. |
| Staff IDs and phone numbers are number cells (2690, 678901343) | A leading zero on a phone number is already gone. |
| "Grade" means salary level on Permanent ("M 2") but a **job title** on Contract ("Carpenter") | The same header names two different facts. |
| "Notch" is "N4" text | The structure stores a notch **number**. |
| No email, no SSNIT, no TIN, no Ghana Card, no address, no manager | The system requires an email today. The rest are the columns TDC will want to complete later. |
| Category "Senior/Junior" per employee | Not an employee field. It is a property of the position (its staff level). |
| Contract sheet carries Basic Salary, All Allowances, All Deductions | Allowances and deductions are payroll components. Payroll owns them and has its own import. |

**2. Do we give them a template? Yes, and the application should generate it, not us.** A static
file goes stale the day a department is renamed. The template is downloaded from the import screen
and carries the tenant's live Departments, Sections, Positions, Locations and Salary Structure as
reference sheets, dropdown validation on every list column, date validation on every date column,
text formatting on every identifier column, and a hidden version stamp so the importer can refuse a
template it does not know. The draft workbook shows the shape; the reference sheets in it are filled
by hand from the sample and the salary-scale file.

For TDC's **existing** register we do a one-time conversion by script (the second output file shows
it), so nobody retypes the current data. From then on the template is the only accepted input.

**3. Relax validations? Relax one, keep the rest, and grade the outcome per row.** The system's own
rules stay: a staff number that already exists, a department that does not, a section under the
wrong department, an off-payroll row carrying a salary — those are refusals and must stay refusals,
because the ordinary create form refuses them too. What the importer adds is a **warning tier** for
things a register legitimately lacks (a date of birth, a qualification's institution, a phone), so
the row imports and the person is listed for follow-up instead of the load stopping. The one hard
rule I recommend relaxing is **email** — see §4.

---

## 1. What exists today (verified against the code)

- **One import door, single record.** `POST api/hr/Employees/import` (`EmployeesController.cs:162`,
  `EmployeeAdminPolicy`) → `EmployeeService.ImportEmployeeAsync` → `CreateWithNumberAsync` → then
  `StaffNumberService.AcceptImportedAsync`. It requires the staff number, runs every create-time
  validation, and advances the register counter **after** the row commits. A bulk importer must go
  through this per row (catalogue §5.7); nothing may write the table directly.
- **The payload it takes** is `CreateEmployeeDto` (`HRDTOs.cs:161`): required `FirstName`,
  `LastName`, `EmailAddress`, `DepartmentId`, `PositionId`, `OrganizationUnitId`; everything else
  optional; `IsOnPayroll` defaults on and, when off, refuses a salary or any payroll switch.
- **`EmailAddress` is `[Required]` on the entity** (`HREntities.cs:116`) and unique per tenant via
  an unfiltered index `IX_Employee_Tenant_EmailAddress` (`ApplicationDbContext.HR.cs:1177`).
- **Positions carry their organisation unit** (`EmployeePosition.OrganizationUnitId`), so the
  template does not need an Org Unit column: it is derived from the position.
- **Positions carry a staff level** (`EmployeePosition.StaffLevelId`), which is where Senior/Junior
  lives.
- **Salary structure is mirrored in HR** as `SalaryGrade → SalaryLevel (Code "M2") → SalaryNotch
  (NotchNumber int)`. TDC's scale file uses exactly these level codes. ⚠ **Not yet traced:** which
  service call records an employee's level/notch at hire. `CreateEmployeeDto` has no such field and
  the entity comment says the assignment is "not captured" when off payroll. Trace before building;
  do not add a fourth way to say it.
- **Qualifications** are `EmployeeQualification` rows with `Institution` **required**.
- **Contract end date** lives on `EmployeeContractDetail.ContractEndDate`, created through
  `AddContractAsync`, not on `Employee`.
- **Identification numbers** (Ghana Card) are `EmployeeIdentificationCard` rows under an
  identification type.
- **Two precedents to copy, not reinvent.** Finance: `IFixedAssetService.ImportAssetsFromExcelAsync
  (stream, fileName, dryRun)` + `GenerateImportTemplateAsync()` + `BulkImportResultDto` with
  row/field/error triples. Quantity survey: `QuantitySurveyBoqSpreadsheetController` — `template`,
  `preview` (creates a session), `sessions/{id}/validation-report`, `sessions/{id}/commit`. HR's
  attendance import already persists `StaffBulkAttendanceImport` + `…Row` with raw data per row.
  ClosedXML 0.105.1 is referenced by both Api and Data projects.

---

## 2. The template (sheet `Employees`, one row per person)

Required columns are marked `*`. Everything else may be blank and completed in the application.

| # | Column | Maps to | Rule |
|---|---|---|---|
| 1 | Staff Number * | `EmployeeNumber` | Text. Unique in file and tenant (soft-deleted leavers still hold theirs). Checked against the register for the employment type; shape mismatch is a **warning**, not an error (TDC open question 4). |
| 2 | Title | `Title` | |
| 3 | First Name * | `FirstName` | |
| 4 | Middle Name(s) | `MiddleName` | |
| 5 | Surname * | `LastName` | |
| 6 | Gender * | `Gender` | List. |
| 7 | Date of Birth | `DateOfBirth` | Date cell. Warn if < 18 or > 70 at Date Employed. Error if after Date Employed. |
| 8 | Marital Status | `MaritalStatus` | List. |
| 9 | Employment Type * | `EmploymentType` | List of the enum. Selects the staff-number register. |
| 10 | Date Employed * | `DateEmployed` | Date cell. Error if in the future. |
| 11 | Contract End Date | `EmployeeContractDetail.ContractEndDate` | Required when type is Contract/FixedTerm (error). Must be after Date Employed. Ignored with a warning for Permanent. |
| 12 | Department * | `DepartmentId` | Code or exact name, case-insensitive, whitespace-normalised. Unknown → error with the nearest three names suggested. |
| 13 | Section | `SectionId` | Must belong to the department (error otherwise). |
| 14 | Position * | `PositionId` → also `OrganizationUnitId` | Code or title. Warn if the position's department differs from column 12. |
| 15 | Location * | `LocationId` | Code or name. **Required** — the create path refuses an employee without one ("LocationId is required for employee assignment"); found by the smoke harness, not by reading. |
| 16 | Manager Staff Number | `ManagerId` | Resolved in a second pass so a manager may be another row in the same file. Unknown → warning, imported without manager. |
| 17 | Salary Level | `SalaryLevel.Code` | "M2", "S1". Normalised ("M 2" → "M2"). Unknown → error. |
| 18 | Notch | `SalaryNotch.NotchNumber` | Whole number. Must exist under the level → error. Level without notch → warning. |
| 19 | Monthly Basic Salary | `Salary` | **Monthly** (decision 2). Error if On Payroll = No. Warn if outside the notch's amount by more than 5 %. |
| 20 | On Payroll | `IsOnPayroll` | Yes/No, default Yes. |
| 21 | Off-Payroll Reason | `OffPayrollReason` | Required when No (error). |
| 22 | Email | `EmailAddress` | Optional (decision 1, option C in §4). Format-checked and unique in file and tenant when present; blank → warning. |
| 23 | Mobile Number | `MobileNumber` | Text. Digits, `+`, spaces only. |
| 24 | Telephone | `TelephoneNumber` | |
| 25 | SSNIT Number | `SocialSecurityNumber` | Unique in tenant (error). |
| 26 | TIN | `TINNumber` | Unique in tenant (error). |
| 27 | *one pair per active identification type:* `<Type> Number`, `<Type> Expiry` | `EmployeeIdentificationCard` | **Generated from the tenant's `IdentificationType` setup**, not hard-coded (decision 4, 2026-09-03). The generator emits a Number column for every active type and an Expiry column for those with `HasExpiryDate`, and records `column → IdentificationTypeId` in `_meta`. The importer binds by the id, so a renamed type still matches; a type deleted since download is a file-level warning and its column is ignored. Number must be unique per type in the file and tenant (error). Expiry in the past → warning. |
| 28 | Digital Address | `DigitalAddress` | |
| 29 | Residential Address | `Address` | |
| 30 | City/Town | `City` | |
| 31 | Region | `State` | |
| 32 | Hometown | `Hometown` | |
| 33 | Religion | `Religion` | |
| 34 | Highest Qualification | `EmployeeQualification` | Matched to the `Qualification` master by name/short code; unmatched → `CustomQualificationName`. |
| 35 | Institution | `EmployeeQualification.Institution` | Blank while 34 is filled → **warning**, stored as "Not recorded". (Entity says required; the importer supplies the placeholder, the profile lists it for completion.) |
| 36 | Year Completed | `EmployeeQualification.CompletionDate` (31 Dec of year) | |
| 37 | Professional Qualification | second `EmployeeQualification` row | One per cell. Several separated by "/" → warning, kept as one. |
| 38 | Notes | `Notes` | |

Reference sheets: `Departments`, `Positions`, `Locations`, `Salary Structure`, `Lists` (enum values).
Hidden `_meta`: `TemplateVersion`, `GeneratedOn`, `Tenant`. `Read Me` first.

**Deliberately absent:** Org Unit (derived), Category Senior/Junior (position's staff level; the
importer cross-checks and warns), Age (derived), allowances and deductions (payroll components),
bank details (own import, never alongside personal data), photo (upload gate).

---

## 3. The sample, column by column

### 3.1 `Permanent` sheet (16 columns, 4 filled rows of 201)

| Source | Disposition |
|---|---|
| S/N, NO. | Dropped (serial numbers). |
| Staff ID (number cell, 2690) | Staff Number, as text. |
| Other Names ("John L. N.") | First Name + Middle Name(s) by first-space split, flagged for a human look. |
| Surname | Surname. |
| Department ("Corporate Planning/Com") | Department by name. Note the banner says "Corp. Planning/Com" and the cell says "Corporate Planning/Com" — resolution must be against the ERP's list, with suggestions. |
| Date of Birth (date cell) | Date of Birth. Clean. |
| Gender | Gender. |
| Category (Senior/Junior) | Not stored. Cross-checked against the position's staff level. |
| Date Employed (date cell) | Date Employed. Clean. |
| Position | Position by title. |
| Grade ("M 2", "S1") | Salary Level after normalising. |
| Notch ("N4", "N11") | Notch number 4, 11. ⚠ N11 exceeds the five notches in the 2026 scale file for every level — either the scale file is partial or the notch is wrong; the importer would flag it as an error. |
| Highest Qualification | Qualification row; institution missing → warning. |
| Professional Qualification ("ICAG Certification", a "/"-separated list, "N/A") | Second qualification row; "N/A" → none; lists kept as one with a warning. |
| Phone Number (number cell) | Mobile Number as text; leading zero already lost in the source. |

### 3.2 `CONTRACT FINAL` sheet (28 columns, 7 filled rows of 34)

| Source | Disposition |
|---|---|
| S/N ×2, Age | Dropped. |
| Departments | Department. |
| Staff No. ("ABC123") | Staff Number. |
| Other Names / Surname | As above. "Col. Augustus" → Title "Col." + First "Augustus". "Ceasar (Rtd)" → surname with a bracketed suffix, flagged. |
| Junior/Senior | Not stored, cross-checked. |
| Date of Birth, Start Date, End Date (**text**) | Parsed with a fixed list of formats (`d-MMM-yyyy`, `d-MMMM-yyyy`, `dd/MM/yyyy`); anything else is an error. The template will not accept text dates, so this only matters for the one-time conversion. |
| Gender | Gender. |
| Grade ("Administrative Assistant", "Carpenter") | **Position** — the column is mislabelled on this sheet. |
| Salary Level ("N/A") | Blank. |
| Basic Salary (1000.99) | Monthly Basic Salary, assumed **monthly** — to confirm with TDC. |
| All Allowances, All Deductions | Not carried. Payroll components. |
| Qualification | Qualification row, institution missing → warning. |
| Section ("Operations Wing", "Revenue", "Administration") | Section; must exist under the department. "Administration" under Administration is probably "no section" → warning. |
| Mobile Number | Mobile Number as text. |

### 3.3 What both sheets lack that the system wants

Email (required today — §4), SSNIT, TIN, Ghana Card, address, region, marital status, manager,
location. None of these should block a load. All of them appear in the post-import "profiles to
complete" list.

---

## 4. The email decision

`EmailAddress` is required and unique. TDC's register has no emails, and a real register never has
one for every driver, carpenter or bill distributor. Three options:

| Option | What it takes | Verdict |
|---|---|---|
| **A. Require email in the template** | Nothing. | Blocks the load. TDC would invent addresses to get past it, which is worse than blank. |
| **B. Importer mints a placeholder** (`2690@import.invalid`) | Importer code only. Uniqueness holds. | Works, but every notification path then sends mail to nowhere, self-service invitations fail silently, and the placeholder leaks into reports. It is a lie in a required field. |
| **C. Make email optional** — nullable column, filtered unique index (`WHERE EmailAddress IS NOT NULL`), drop `[Required]` on entity and create DTO, keep format + uniqueness when present | One migration (user scaffolds), entity + DTO + service edits, and a sweep of the places that assume an email exists (user-account linking, notification senders) to skip a null instead of crash. | **Recommended.** It is the true shape of the data. The create form can still require it for new hires if HR wants that as a form rule, which is a UI decision, not a schema one. |

**Decided 2026-09-03: option C.** The sweep of every place that assumes a non-null employee email
is recorded in §8 as it is done.

---

## 5. Validation model

Three outcomes per row, computed at upload and again at commit:

- **Error** — the row will not be written. Missing required column; unknown or inactive
  department/section/position/location/level/notch; section not under department; contract type
  without end date; end date before start; DOB after start; duplicate staff number, email, SSNIT
  or TIN (within the file **or** in the tenant); On Payroll = No with a salary or without a reason;
  unparseable date or number; unknown Ghana Card type.
- **Warning** — the row is written and the person appears on the follow-up list. Blank DOB, phone,
  email (option C), institution; manager not found; notch missing under a level; salary outside the
  notch band; staff number outside the register's format; category disagrees with the position's
  staff level; position's department differs from the stated department; template older than the
  tenant's reference data (a department was renamed since download).
- **Ready** — no findings.

File-level checks come first and stop everything: wrong or missing `_meta` version, a required
column missing or renamed, an empty Employees sheet, more than the size limit (15 MB, as the BoQ
importer), and the file hash matching an already-committed session (warn, allow).

Messages always name the **cell** (`Employees!L7`), the column header and the value seen, and, for
lookups, the nearest three candidates. The same findings are written back into a copy of the
uploaded workbook as a Result column plus cell comments, so the fix-in-Excel loop needs no
retyping.

---

## 6. Architecture

Copy the BoQ importer's shape, persist like the attendance importer, resolve like nothing else in
HR does yet (name/code lookups with suggestions).

### 6.1 Persistence (new, needs a migration)

- `EmployeeImportSession` : `TenantEntity` — `Reference`, `FileName`, `FileHash`, `TemplateVersion`,
  `UploadedById` (Employee), `UploadedOn`, `Status` (Validated / Committing / Committed /
  CommittedWithErrors / Cancelled / Failed), `Mode` (CreateOnly / UpdateExisting — phase 2),
  `TotalRows`, `ReadyCount`, `WarningCount`, `ErrorCount`, `CommittedCount`, `CommitPolicy`
  (ValidRowsOnly / AllOrNothing), `StartedOn`, `CompletedOn`, `SourceFileUploadRecordId` (through
  the upload gate so the original file is retained), `Notes`.
- `EmployeeImportRow` : `TenantEntity` — `SessionId`, `RowNumber`, `StaffNumber`, `DisplayName`,
  `RawJson` (the cells as read), `ResolvedJson` (the `CreateEmployeeDto` plus child payloads it
  became), `FindingsJson` (`[{severity, column, cell, message, suggestions[]}]`), `Outcome`
  (Ready / Warning / Error / Skipped / Committed / Failed), `Skip` (user toggle), `CreatedEmployeeId`,
  `CommitError`.

### 6.2 Service `IEmployeeImportService` (Core)

- `GenerateTemplateAsync()` → bytes. Builds the workbook with live reference sheets and `_meta`.
- `CreateSessionAsync(stream, fileName, options)` → session summary. Parses with ClosedXML, runs
  file checks, resolves every lookup once into dictionaries (departments, sections, positions,
  locations, levels, notches, qualification master, existing staff numbers / emails / SSNIT / TIN
  of the tenant **including soft-deleted**), validates each row, does the manager second pass,
  persists session + rows.
- `GetSessionAsync`, `ListSessionsAsync`, `GetRowsAsync(sessionId, outcome filter, page)`,
  `SetRowSkipAsync`, `CancelSessionAsync`.
- `BuildAnnotatedWorkbookAsync(sessionId)` → the uploaded file with Result column + comments.
- `CommitAsync(sessionId, policy)` — refuses if any Error and policy is AllOrNothing; otherwise
  marks Committing and hands off to a hosted background job: for each non-skipped Ready/Warning
  row, **re-validate**, call `ImportEmployeeAsync`, then `AddContractAsync` / qualification /
  identification writes for that employee, record the outcome on the row, update counters every 50
  rows so the screen can show progress. Each row is its own unit of work: one failing row never
  rolls back the previous ones (AllOrNothing is enforced up front, not by a 8,000-row transaction).
  On finish: `StaffNumberService.ReconcileCounterAsync` for every register touched, session status,
  audit event (who, file, hash, counts).
- `GetFollowUpAsync(sessionId)` — the created employees with their warnings, for the "profiles to
  complete" screen and export.

Throughput note: `CreateWithNumberAsync` runs five uniqueness queries per row. At 8,000 rows that
is a few minutes, which is why commit is a background job with progress rather than a request.

### 6.3 Endpoints (`EmployeeImportSessionsController`, `api/hr/employees/import-sessions`, `EmployeeWritePolicy`)

> Tier changed 2026-09-03 from Admin to **Write** (option 2 of the tier decision in §8): the HR
> role holds Write but not Admin, so the admin tier hid the screen from every HR desk user.

| Verb | Route | Purpose |
|---|---|---|
| GET | `template` | Download the generated template. |
| POST | `` (multipart, 15 MB limit) | Upload → validate → session. |
| GET | `` | List sessions. |
| GET | `{id}` | Summary + counts + status. |
| GET | `{id}/rows?outcome=&page=` | Paged rows with findings. |
| PATCH | `{id}/rows/{rowId}/skip` | Toggle a row out of the commit. |
| GET | `{id}/report` | Annotated workbook. |
| POST | `{id}/commit` | `{ policy }`. Starts the job. |
| GET | `{id}/progress` | Committed / failed / remaining. |
| POST | `{id}/cancel` | Before commit only. |
| GET | `{id}/follow-up` | Profiles-to-complete list (+ `?format=xlsx`). |

The single-record `POST api/hr/Employees/import` stays for the create form's "already has a number"
path.

### 6.4 Screens (`/hr/employees/import`, sidebar under the Employees group)

1. **Sessions list** — reference, file, who, when, counts, status; "New import".
2. **Wizard step 1 · Template** — download button, the column guide, the rules in plain words.
3. **Step 2 · Upload** — drop zone; shows file checks immediately.
4. **Step 3 · Review** — four tiles (rows / ready / warnings / errors); grid filtered by outcome
   with the findings inline and the suggestion clickable into the message; "Download checked
   file"; "Skip row"; "Upload a corrected file" (creates a new session, links to the old one).
5. **Step 4 · Commit** — policy choice (valid rows only, default; or all-or-nothing, disabled while
   errors exist); confirmation naming the counts; progress bar polling `progress`.
6. **Step 5 · Result** — what was created, what failed at commit and why, the follow-up list with
   export, links into each new profile.

### 6.5 Harness

`dev-harness/hr-employee-import/` with fixture workbooks generated by script (clean, every error
class, every warning class, a 500-row throughput file, an old-version template, a re-upload of a
committed file). Assert the counts, the cell references in messages, the counter after commit, and
that a failed row leaves no employee behind. Run in Staging with the JWT key, two actors (HR admin,
plain HR).

---

## 7. Phases and what each needs from the user

| Phase | Content | Needs |
|---|---|---|
| **0. Decisions** ✅ 2026-09-03 | (1) email → **optional** (option C); (2) Basic Salary is **monthly**; (3) level and notch **import in phase 1**; (4) identification columns are **generated per tenant** from `IdentificationType` (column 27 in §2) — nothing named "Ghana Card" exists in code. | — |
| **1. Backend** | Entities, migration, template generator, parser/validator, session persistence, commit job, counter reconcile, endpoints. | migration scaffold; a build |
| **2. Frontend** | Service client, sessions list, five-step wizard. | a build |
| **3. Proof** | Harness, docs (`HR-FINISH-PLAN.md` row, runbook page, this doc's status), the one-time conversion of TDC's real register once received. | a run |
| **4. Update mode** | `UpdateExisting`: match by staff number, blank cells never overwrite, diff preview per row. | later |

## 9. Update-existing mode (phase 4)

A session carries a **mode**, chosen at upload: **Create new employees** (the default; a known
staff number is an error), **Update existing employees** (rows are matched by staff number; an
unknown number is an error), or **Create or update** (known updates, unknown creates). The same
template serves all three.

**An update changes only the cells that were filled.** A blank cell means "leave it", never "clear
it"; clearing is a profile edit, not an import. Every filled cell is checked as it would be on
create, then compared with the register, and the row carries a **diff** — field, register value,
sheet value — that the review screen shows before anything is committed. A row whose filled cells
all match the register is a warning ("nothing differs") and is written as a no-op.

| Supplied on an update row | Effect |
|---|---|
| Names, title, gender, marital status, religion, hometown, addresses, notes, phones | Set when different. |
| Employment Type | Set when different; full-time flag follows it. |
| Date Employed, Date of Birth | Set when different; the age checks use the effective (supplied or current) pair. |
| Contract End Date | **Ignored with a warning** — contracts are edited on the profile. |
| Department, Section, Location | Resolved as on create; the section must belong to the effective department. |
| Position | Set with its organisation unit (the service checks the pair); the unit change is shown too. |
| On Payroll, Off-Payroll Reason, Monthly Basic Salary | Same rules as create against the effective membership; taking somebody off payroll needs a reason. |
| Salary Level / Notch | Compared with the **current** assignment; when different, a new assignment is written (the service closes the old one). |
| Email, SSNIT, TIN | Uniqueness excludes the employee's own current value. |
| Identification numbers | Added only when the profile does not already hold that number for that type. |
| Qualifications | Added only when the profile does not already hold that name. |
| Manager Staff Number | Set when different; may be another row of the file (an update row's employee is known at check time, a create row's after it commits). |

⚠ **Two things the update path forced:** `EmployeeMappingExtensions.Apply` writes `HasDisability`
and `IsFullTime` **unconditionally**, so an import update must send the current values back or it
would reset both to false — the checker copies them from the snapshot. And a position change must
send the position's organisation unit, because the service validates the pair.

**Snapshots** — the register's current values for the staff numbers in the file — are loaded only
for update-capable modes, only for the numbers present, in chunks of 500 (SQL Server's 2,100
parameter limit; a file may hold 10,000 numbers). Soft-deleted employees are never updated: their
number is an error in every mode.

## 8. Build log

### 2026-09-03 — phase 1 written, awaiting migration + build

**Email optional (decision 1).** `Employee.EmailAddress` → `string?` with `[OptionalEmailAddress]`;
`CreateEmployeeDto.EmailAddress` likewise; index `IX_Employee_Tenant_EmailAddress` gets
`HasFilter("[EmailAddress] IS NOT NULL")`. `EmployeeService.NormalizeEmail` returns **null** for
blank (it returned `""`, which a NOT-NULL-filtered index would still compare), `IsEmailUniqueAsync`
returns true for blank (it returned **false**, refusing every emailless create), the create guard
skips uniqueness when null, and the update path now treats `""` as "clear" (before, an address could
never be removed). Same blank→null rule in `EmployeeProfileChangeService` (uniqueness pre-check and
apply). `JobApplicationService` refuses an internal application from a profile with no email, with
a message saying why (the shadow candidate is keyed on it). `TdcDemoPersonaSeeder` falls back to a
demo address for the Identity user. Read DTOs (`EmployeeDto`, `EmployeeDetailDto`'s summary,
`StaffDirectoryEntryDto`, `EmployeeProfileChangeDTOs`) now say `string?`. Frontend: the zod schema
accepts blank, the two TS types are optional, the mapper sends `undefined` not `""`.
⚠ **The migration must also convert existing `''` addresses to NULL** and drop the baseline's
single-column `IX_Employees_EmailAddress` if it still exists in a live database (the model no longer
declares it; EF will not touch it on its own).

**Import feature.** New files:
- `Core/Enums/EmployeeImportEnums.cs`, `Core/Entities/HR/EmployeeImportEntities.cs`,
  `Core/DTOs/HR/EmployeeImportDtos.cs`, `Core/Interfaces/HR/IEmployeeImportService.cs`.
- `Api/Services/HR/EmployeeImport/EmployeeImportCatalog.cs` — the column catalogue (§2), reference
  data shape, lookup-with-suggestions, cell parsing helpers.
- `…/EmployeeImportWorkbooks.cs` — writes the template, the checked copy, the follow-up list.
- `…/EmployeeImportWorkbookReader.cs` — parses and checks (§5), row by row and across rows.
- `…/EmployeeImportService.cs` — sessions, commit batches, finalisation, counter reconcile.
- `…/EmployeeImportCommitBackgroundService.cs` — polls every 5 s under `bg:employee-import-commit`.
- `Api/Controllers/HR/EmployeeImportSessionsController.cs` — the §6.3 endpoints.
- `ApplicationDbContext.HR.cs`: two DbSets + `ConfigureEmployeeImportEntities`.
  `ControlledFileUploadCategories.HrEmployeeImportWorkbooks` (declared **and** in the scan set).
  DI: scoped service beside `IStaffNumberService`; hosted committer beside the reminder engines.

**What the trace changed in the design.**
- **Level/notch is a second call, not a create field.** `CreateWithNumberAsync` writes no grade;
  the hire path (`JobOfferHireService`) writes `EmployeeSalaryAssignment` itself. The committer calls
  `IEmployeeService.AssignSalaryAsync` after the create — gated on payroll membership, so an
  off-payroll row with a level is an **error** at check time, not a surprise at commit.
- **`GradeId` is required on the assignment**; the template asks for the level only and the grade
  comes from `SalaryLevel.SalaryGradeId`.
- **No job queue exists.** The session row is the work item. The committer runs one batch of 25 rows
  per DI scope, and **acts as the requesting user** by installing a principal on
  `IHttpContextAccessor` — `CurrentUserService` reads claims from the HTTP context and every HR
  service takes its tenant from there. Without that, every row fails with "no tenant".
- **The change tracker is cleared between rows.** `EmployeeService` saves as it goes; a refused row
  would leave a poisoned `Added` entity that fails every later save. Each row reloads its own
  session and row entity before recording the outcome.
- **Source-file retention is best-effort on infrastructure failure.** A gate refusal on content
  (type, size, infected) removes the session and surfaces the gate's status; a gate that is
  unavailable (5xx) keeps the session with a file-level warning and a note, so a scanner outage does
  not stop HR from loading staff. Different from the BoQ importer, deliberately.
- **Manager links inside the file are done in finalisation**, after every row has an id, through
  `AssignManagerAsync`. Managers already in the system go on the create payload.
- **Duplicate checks include soft-deleted employees** (numbers, emails, SSNIT, TIN) because those
  indexes are unfiltered on `IsDeleted`. A deleted manager is warned about, not linked.
- **The example row is `EXAMPLE-001`** and is skipped by the reader, so a template uploaded with the
  example still in place is not an error.

### 2026-09-03 — migration applied, harness green, wizard written

**Migration** `20260903081606_AddEmployeeImportSessionsAndOptionalEmail` applied and listed in
`FastBuildMigrationMetadata`. Beyond what EF scaffolded it converts existing `''` emails to NULL
before the filtered index goes on, drops the baseline's tenant-blind `IX_Employees_EmailAddress` if
present, and in `Down` restores `''` before the column goes NOT NULL again. Verified in the
database: history row, `EmailAddress` nullable, index filter `([EmailAddress] IS NOT NULL)`, both
tables present.

**Harness** `dev-harness/hr-employee-import/run-smoke.mjs` — **74 assertions, green** on the
second run (first run: 53/60, the 7 failures one cause). It downloads the template, fills it from
the template's own reference sheets (python + openpyxl), refuses a non-template and a non-xlsx with
reasons, lands six rows on the expected outcomes, downloads the checked copy and **re-uploads it to
identical counts**, skips a row, has all-or-nothing refused, commits valid rows, polls to completion,
verifies the register (number as given, email lower-cased or absent, in-file manager link set in
finalisation, contract and qualification and identification children), reads the follow-up list,
proves a second upload of the same file warns and its numbers now fail, then soft-deletes what it
created. Recipe in its README.

⚠ **What the first run found that reading did not:** the create path requires a **Location**
(`LocationId is required for employee assignment`). Three valid rows all failed at commit with that
message. Location is now a required column with a check-time error; §2 row 15 updated.

⚠ **Two paths the smoke could not exercise on this database:** the DEFAULT tenant has **no salary
structure** (no levels, no notches) and **no numbering rule**, so the level/notch assignment and the
counter reconcile ran as "nothing to do" (`counterReconciliation: []`). Both are code paths the
harness asserts only when the tenant has the data; TDC's real tenant will. Load the salary scale and
a rule before the UAT run and the same harness covers them.

**Wizard (phase 2)** at `/hr/employees/import` (sidebar: HR → Employees → Import, `HR.Employee.Write` since the tier decision below; was Admin):
- `import/page.tsx` — sessions list with counts and status; template download; New import.
- `import/new/page.tsx` — step 1 template + column guide (read from the API, so the identification
  columns are the tenant's), step 2 drop zone; a rejected workbook shows the file-level reasons and
  creates nothing.
- `import/[id]/page.tsx` — steps 3–5 on one page driven by status: tiles, rows grid with outcome
  filter, search and paging, an expandable row showing findings (cell reference, message, "did you
  mean") beside the values as read, skip toggles, commit policy with confirmation, live progress
  (2 s poll), result with counter reconciliation, the follow-up table with profile links and
  workbook download, "fix the checked file and upload again".
- `types/hr/employee-import.ts`, `services/hr/employee-import.service.ts`,
  `components/hr/employee-import/{WizardSteps,SessionStatusBadge}.tsx`.
Scoped `tsc` over the slice: 0 errors. ⚠ Not yet opened in a browser — the harness proves the API;
the screens are proven only by types until the user runs `npm run dev` and walks the wizard.

### 2026-09-03 — committed (`f98d4a38`); runbook written

Book 1 §1.3 of `dev-harness/hr-demo-smoke/runbook/` now walks the wizard in seven steps with the demo file
`dev-harness\hr-demo-smoke\out\demo-employee-import.xlsx`, produced the evening before by
`hr-demo-smoke/make-employee-import-file.mjs` (downloads the template from the demo database and
fills six DEMO-00n rows with demo names; Book 0 step 4). It creates nothing until committed on stage.

⚠ **Template size.** The empty template weighed **1.1 MB** because the column formats were applied
to a 10,000-row *range*, which materialises a cell per row. Moved to column-level styles
(`ws.Column(c).Style`) with the header row reset to General; data validation stays on the range
(it creates no cells). Rebuilt and re-run: **74 green, template now 30 KB** (was 1,173 KB).

### 2026-09-03 — phase 4 written (update-existing mode), awaiting migration + build

§9 above is the rule set. Code: `EmployeeImportMode` / `EmployeeImportRowAction` enums; session
columns `Mode`, `CreateCount`, `UpdateCount`, `CreatedCount`, `UpdatedCount`; row columns `Action`,
`TargetEmployeeId` (indexed, no FK) — **migration `AddEmployeeImportUpdateMode` owed**. The reader
is now two passes (`ReadRaw` → snapshots → `Resolve`) with `ResolveUpdateRow` beside
`ResolveCreateRow`; shared pieces (section, salary level, ages, number shape, identifications,
qualifications) were pulled out so the two cannot drift. The committer calls
`UpdateEmployeeAsync` for field changes, `AssignSalaryAsync` when the level/notch differs, and the
add services for new documents and qualifications; a no-change row is written as "No changes".
Upload takes `mode` as a form field. Wizard: a mode choice on the upload page; an Action column,
a per-row **What changes** table with the register value struck through, and created/updated
split everywhere counts are shown. Scoped `tsc` + eslint: clean.
Harness `run-update.mjs` (seeds four employees, then proves all three modes, the diff, the commit
and the register) — **not yet run**; needs the build.

**Migration `20260903091650_AddEmployeeImportUpdateMode` applied** (defaults for `Mode` and
`Action` set to 1 by hand — EF's 0 is not an enum value). First `run-update.mjs`: **45/52**, the
7 failures one cause, and it was not the importer:

⚠ **Defect in `EmployeeService.UpdateEmployeeAsync`, found by the harness.** A position change
writes an `EmployeePositionHistory` row **without `TenantId`** — the create path stamps it, the
update path never did, and the DbContext auto-stamp is inert (the known HR tenancy gap). Every
position change made through an update, including the edit form's, failed on
`FK_EmployeePositionHistories_Tenants_TenantId`. Fixed in place, with `LocationLevelId` left null
rather than `Guid.Empty` as the create path already does. Needs a build and a re-run.

Rebuilt and re-run: **`run-update.mjs` 52/52, `run-smoke.mjs` 74/74.** Phase 4 done.

### 2026-09-03 — permission tier: Admin → Write

The user ran the app as an HR-role account and the Import entry was not there. Cause: the screen
and the endpoints were **admin-gated**, mirroring the single-record import door, and in this
database `HR.Employee.Admin` is held by SuperAdmin and TenantAdmin only — the HR role has Read and
Write. Every HR desk user, and the runbook's head of HR, was locked out; the harnesses ran as
`admin` and never noticed (⚠ a two-actor gap in the harness — it has no plain-HR login to prove
the tier from the other side).

Two options were put: grant Admin to the HR role (which also unlocks deleting employees and
reference data, the tier's actual meaning), or move the import to Write. **Decided: Write.**
Loading and amending employee records is what the write tier means in the permission catalogue;
in a manual register an HR officer types the staff number with Write anyway, and the bulk path
advances the counter past every number it loads, so the numbering rule is not bypassed. The
single-record door stays Admin as its author argued. Changed: the controller's class attribute,
the sidebar entry, the runbook's warning box, this document.

**Still owed:** the one-time conversion of TDC's real register once it arrives; a browser walk of
the wizard.
