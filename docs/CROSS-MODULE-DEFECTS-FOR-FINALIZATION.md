# Cross-Module Defects Found During HR Work — For Finalization

**Opened 2026-08-17.**

Defects found in **other teams' modules** while building HR. They are recorded rather than fixed,
because the module is not HR's to change — the same rule that governs payroll. Each entry states
what is broken, what was proven, what it blocks, and what a fix needs.

**Owner action required.** Nothing here is speculative: every item was reproduced against the
running API on the reference database, and where a fix was trialled the result is recorded.

---

## 1. Procurement — `SuppliersController` is entirely non-functional

**Severity: blocking.** Found 2026-08-17 while retiring HR travel's duplicate vendor master onto
Procurement's `Supplier` (area 12, slice 3).

### What is broken

Two repositories the controller depends on are **never DI-registered**:

| dependency | registered | implementation |
|---|---|---|
| `ISupplierRepository` | ✅ | `SupplierRepository` |
| `ISupplierContactRepository` | ❌ | `SupplierContactRepository` — exists, ctor takes only `ApplicationDbContext` |
| `ISupplierItemCatalogRepository` | ❌ | `SupplierItemCatalogRepository` — exists, same ctor |
| `IProcurementMasterDataChangeService` | ✅ | — |

DI cannot activate the controller, so **every endpoint on it answers 400** with
`Unable to resolve service for type 'ISupplierContactRepository'`. It reports only the first
missing dependency, which is why the second is easy to miss.

All three implementations sit in the same file
(`src/ErpSystem.Data/Repositories/Procurement/SupplierRepositories.cs`), next to the one that *is*
registered. There is no assembly scanning — every repository is registered by hand in
`ServiceCollectionExtensions.cs` (~line 251) — so these two were simply skipped in the list.

`SuppliersController` is the **only** consumer of both interfaces, so the blast radius is that one
controller and adding the registrations cannot disturb anything currently working.

### What was proven

The two registrations were added temporarily, the solution rebuilt, and the controller exercised.
**Registration is necessary but not sufficient.**

Reads work correctly once registered:

```
200  GET /api/Suppliers                 1 row      ← and it contained the seeded supplier
200  GET /api/Suppliers?isActive=true   1 row
200  GET /api/Suppliers/{id}            object
200  GET /api/Suppliers/{id}/contacts   0 rows
```

**The create is a dead path:**

```
POST /api/Suppliers  →  201 Created, empty body
Suppliers in database: 1 (unchanged)
```

`CreateSupplier` → `CreateSupplierAsync` → `GenericRepository.AddAsync`, which only calls
`_dbSet.AddAsync(entity)` and **never `SaveChangesAsync`**. The controller has no unit-of-work
commit either, so the entity is tracked, never written, and the request ends. The 201 comes from
`CreatedAtAction`; its body is empty because the follow-up read cannot find a row that was never
saved. This is the `AddAsync`-with-no-commit dead-path shape.

Two further problems in the same handler, which a fix should take together:

- **`TenantId` is never set** on the new `Supplier`. The DbContext's auto-stamp is inert — its
  tenant-resolving constructor is commented out at `ApplicationDbContext.cs:60` — so even once the
  save happens the row would violate the tenant FK.
- The response is `CreatedAtAction(...)` with a **null body** rather than the created DTO.

`POST /api/Suppliers/{id}/contacts` also returned 400 on a plausible payload; not investigated,
because the module is not ours.

**The trial changes were reverted.** `ServiceCollectionExtensions.cs` is back to its committed
state — nothing in Procurement has been modified.

### What it blocks

HR area 12 slice 3 retired travel's duplicate vendor master: six `VendorId` FKs (flights, hotels,
ground transport, car rentals, visa applications, insurance) now point at `Suppliers`. That is the
right design — an airline paid through travel and the same airline paid through procurement must
not be two records — but **travel's vendor selection cannot work until this controller does**, and
no supplier can be onboarded through the API at all while the create is dead.

The HR travel harness works around it by seeding a supplier directly in SQL
(`dev-harness/hr-travel/fixtures.json`), which tests HR's side of the seam and deliberately does
not depend on Procurement's endpoint.

### What a fix needs

1. Register both repositories beside `ISupplierRepository` in `ServiceCollectionExtensions.cs`.
2. Commit the create — `SaveChangesAsync`, via the repository or a unit of work.
3. Set `TenantId` explicitly on the new `Supplier`.
4. Return the created DTO in the response body.
5. Then exercise every remaining endpoint on the controller: **it has never once activated**, so
   nothing on it has ever executed and none of it is proven. Expect more of the same shapes.

### ⚠ A correction worth carrying

While this was undiagnosed, the 400 on `POST /api/Suppliers` was attributed to the staged
master-data change guard (`GuardDirectMutationAsync`), and it was reported that the travel desk
could no longer add vendors because Procurement gates supplier creation. **That was wrong.** The
400 was the DI failure, identical to every other endpoint. The guard exists but returned *allowed*
and is not blocking anything. Any decision taken on the basis of "supplier creation is gated"
should be revisited.

---

## How to use this file

Add an entry whenever HR work uncovers a defect in a module HR does not own. Keep the same shape:
what is broken, what was proven, what it blocks, what a fix needs. Resist fixing them inline — the
value here is that the owning team gets a reproducible report, not a surprise diff in their module.
