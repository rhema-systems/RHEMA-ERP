# Hand-off to the Payroll module owner — a new employee profile cannot be created

**Raised by:** HR module work, 2026-09-02 (finish plan lane 3f, payroll membership).
**Severity:** blocks a first-class payroll workflow; no data loss, no security exposure.
**Status:** open. HR carries a temporary workaround that should be deleted once this is fixed.

This is a self-contained report — nothing in it requires reading HR's plans or code. It is also
entry **#23** in [`CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`](CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md),
where every cross-module defect HR has found is collected.

---

## 1. What is broken, in one line

`POST api/hr/payroll/employee-profiles` fails with SQL error 547 for **every employee payroll does
not already have a profile for**, so a payroll employee profile can never be created through the
API or through the Payroll → Employee Profiles screen. Updating an existing profile works.

---

## 2. Reproduce it in two minutes

Any environment, any tenant. Pick any employee id that has no `PayrollEmployeeProfiles` row:

```sql
SELECT TOP 1 e.Id, e.EmployeeNumber
FROM   Employees e
WHERE  e.IsDeleted = 0
  AND  NOT EXISTS (SELECT 1 FROM PayrollEmployeeProfiles p WHERE p.EmployeeId = e.Id);
```

Then, authenticated as any user who can reach the payroll controller:

```http
POST /api/hr/payroll/employee-profiles
Content-Type: application/json

{
  "employeeId": "<the id above>",
  "employeeNumber": "<their number>",
  "payrollActive": true,
  "payTax": true,
  "ssfApplicable": true,
  "currencyCode": "GHS",
  "salaryBasis": { "monthlyBasicSalary": 4250.50, "currencyCode": "GHS",
                   "effectiveFrom": "2026-09-01T00:00:00Z", "isActive": true },
  "paymentMethods": []
}
```

**Observed:** `500`, body `{"message":"Payroll request failed."}`. The API log carries:

```
Microsoft.Data.SqlClient.SqlException (0x80131904): The INSERT statement conflicted with the
FOREIGN KEY constraint "FK_PayrollEmployeeProfiles_PayrollPaymentMethods_DefaultPaymentMethodId".
The conflict occurred in database "ErpSystemDB", table "dbo.PayrollPaymentMethods", column 'Id'.
Error Number:547
```

**Also tried, same failure:** sending one explicit payment method
(`[{ "paymentType": "Bank", "paymentMode": "Percentage", "paymentPercent": 100,
"currencyCode": "GHS", "sequenceNo": 1, "isActive": true }]`). So it is not the empty-list branch.

The request above is enough on its own. If you would rather run it: a one-file script that logs in,
finds an employee with no profile and posts both variants is at
`D:\Rhema\TDC ERPS\dev-harness\hr-payroll-membership\repro-payroll-profile-create.mjs`
— plain node 18+, no dependencies, no configuration:

```
node repro-payroll-profile-create.mjs                                  # localhost:5000, admin
node repro-payroll-profile-create.mjs http://localhost:5000 user pass  # or point it anywhere
```

⚠ It lives **outside the repository**, with HR's other verification harnesses (`…\TDC ERPS\dev-harness\`),
so it is not in a `git pull`. Ask for the file, or just use the request above.

---

## 3. Why it happens

Two rows point at each other, and both are written in one `SaveChanges`:

| | |
| --- | --- |
| `PayrollPaymentMethod.EmployeeProfileId` | → `PayrollEmployeeProfiles.Id` |
| `PayrollEmployeeProfile.DefaultPaymentMethodId` | → `PayrollPaymentMethods.Id` |

`ApplicationDbContext.cs:5646` configures the second as a required-target `HasOne … WithMany …
OnDelete(Restrict)`. Keys are client-generated Guids, so EF cannot order its way out of the cycle:
it inserts the profile first, with `DefaultPaymentMethodId` already pointing at a payment-method row
that has not been inserted yet, and SQL Server rejects it.

Both branches of `UpsertPaymentMethods` (`src/ErpSystem.Api/Services/HR/PayrollService.cs`) set the
pointer inside the same unit of work as the insert:

- **line 13440** — the "no methods supplied, seed a default" branch: `profile.DefaultPaymentMethod = defaultMethod;`
- **line 13510** — the "methods supplied" branch: `profile.DefaultPaymentMethod = profile.PaymentMethods.Where(e => e.IsActive)…FirstOrDefault();`

`UpsertEmployeeProfileAsync` (line 2358) calls it before its single `SaveChangesAsync`.

**Why nobody has hit it before:** every `PayrollEmployeeProfile` in the database today was written by
the legacy reconciliation import (`PayrollService.cs:4206`), which constructs the profile **without**
a default payment method and saves it — so the cycle never forms there. The update path is fine for
the same reason: the profile row already exists, so setting the pointer is an UPDATE, not an INSERT.

---

## 4. What it blocks

- **Payroll's own screen.** Payroll → Employee Profiles can maintain the imported population and
  nobody else. A new hire cannot be set up in payroll at all through the UI.
- **HR's payroll-membership bridge** (shipped 2026-09-02). When HR marks an employee "on payroll",
  HR enrols them by calling `IPayrollService.UpsertEmployeeProfileAsync` — create-only, never an
  update, never a deactivation. That call is this one, so it always fails.

---

## 5. Suggested fix

Any of these closes it; the first is the smallest.

1. **Two saves.** In `UpsertEmployeeProfileAsync`, when the profile is new: save the profile and its
   payment methods first, then set `DefaultPaymentMethodId` and save again. `UpsertPaymentMethods`
   would set the pointer only when `profile.Id` already exists in the database.
2. **Make the default derived, not stored.** Drop `DefaultPaymentMethodId` and treat "the active
   method with the lowest `SequenceNo`" as the default. That is already exactly what line 13510
   computes, so the column is a cache of a derivable value.
3. **Break the cycle in the model.** Keep the column but make the FK deferrable in practice by not
   assigning it until after the first save — same effect as (1), configured rather than coded.

Whichever you choose, please leave the seeded/imported profiles alone: HR's demo seeder and the
legacy import both write the same shape (profile + salary basis + one 100 % bank method + pointer)
and depend on it staying valid.

---

## 6. What HR did meanwhile, and what to do when this is fixed

`PayrollMembershipService.EnsurePayrollProfileAsync`
(`src/ErpSystem.Core/Services/HR/PayrollMembershipService.cs`) calls your upsert **first**. When it
throws, HR falls back to `CreateMinimalProfileDirectlyAsync`, which writes the identical shape in
two saves and logs a warning naming this defect:

```
Payroll's employee-profile upsert failed for {EmployeeNumber} ({EmployeeId}); falling back to a
direct minimal profile (cross-module defect #23).
```

The fallback exists only because HR could not ship a feature that depends on a broken call, and it
is deliberately loud so it cannot quietly become permanent.

**When you have fixed the upsert:** tell the HR side, and that fallback method plus its call site get
deleted. The verification is already written — `dev-harness/hr-payroll-membership/run-payroll-membership.mjs`
(84 assertions) covers profile creation end to end; after the fix it should still read `84 passed,
0 failed` while the `defect #23 fallback` line stops appearing in the log.

---

## 7. Evidence

- Reproduced 2026-09-02 against `ErpSystemDB`, API in `Staging`, on the current `hrdev` build.
- Four occurrences in one harness run, all with the same FK message; both payload variants tried.
- Repro script (outside the repo): `…\TDC ERPS\dev-harness\hr-payroll-membership\repro-payroll-profile-create.mjs`, verified 2026-09-02 — both variants 500, six FK-547 entries in the API log.
- Full defect register: `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`, entry #23.
