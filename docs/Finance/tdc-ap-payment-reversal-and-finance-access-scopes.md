# TDC Finance Control Foundation: AP Payment Reversal and Access Scopes

**Implementation date:** 2026-08-02  
**Plan coverage:** WP0, AP subset of WP2/WP4, initial WP9  
**Limitation coverage:** `FIN-LIM-0009` and Finance mitigation for `FIN-LIM-0014`

## Outcome

This slice adds a controlled correction path for posted Accounts Payable payments and the first enforceable Finance data-scope boundary. It extends the existing AP services, Finance settings, audit infrastructure, permission policies, and `FinancePostingEngine`; it does not create a second posting or settlement implementation.

Posted AP payments are never edited or deleted to correct accounting. An authorized reversal posts compensating entries, restores invoice settlement values, records negative allocation history, links original and reversal evidence, and retains the complete source-to-ledger trace.

## TDC defaults

The following defaults favor strong public-sector financial control while keeping the development environment operable:

| Control | Default | Reason |
|---|---|---|
| Reversal date | Current open Finance period | Preserves closed-period reporting and creates a clear current-period correction trail. |
| Minimum reason | 20 characters | Requires a meaningful explanation without making ordinary correction descriptions impractical. |
| Access-scope enforcement | Off until grants are prepared | Prevents accidental lockout during migration. Enabling the setting is blocked unless at least one effective supported grant exists. |
| Administrative recovery | `SuperAdmin` and `TenantAdmin` bypass | Ensures an authorized tenant administrator can repair an incorrect grant configuration. All normal Finance users remain scope-bound. |

The reversal date policy is tenant configuration. The default does not reopen a closed period or rewrite the original journal date.

## AP reversal rules

A posted vendor payment can be reversed only when all of the following are true:

- The caller has `Finance.AP.Payments.Reverse` and the required effective Finance data scope.
- The payment is processed or cleared, has an original Finance posting, and has not already been reversed.
- The payment is not bank-reconciled.
- Supplier-advance amounts from the payment have not already been applied to later invoices. Those downstream applications require their own reversal before the source payment can be reversed.
- The reason meets the configured minimum length and the reversal date satisfies the tenant policy and fiscal-period controls.

The service then performs one atomic operation:

1. Locks/reloads the payment and rechecks eligibility.
2. Posts the linked compensating AP journal through `FinancePostingEngine`.
3. Reverses related realized-FX settlement postings through the same engine where applicable.
4. Restores invoice paid, outstanding, withholding-tax, discount, and status values.
5. Adds negative allocation records instead of overwriting the original settlement history.
6. Marks the payment `Reversed`, stores original/reversal lineage, and records a Finance audit event.

Repeated submission is idempotent: once a valid reversal exists, the command returns the existing reversed payment rather than posting a second journal.

## Source-to-ledger trace

`GET /api/ap/payments/{id}/trace` returns a single read model containing:

- payment and reversal lineage;
- positive and reversal allocations;
- Finance posting events and journal lines;
- realized-FX reversal evidence where relevant; and
- Finance audit events for the source document.

The AP payment detail screen presents this evidence chain and gives authorized users the reversal dialog. The API permission and data-scope checks remain authoritative even if a client calls the endpoint directly.

## Finance access scopes

The initial scope model separates action permission from data access. A normal user needs both:

- an existing Finance action permission; and
- an effective grant with at least the access level required by the operation.

Supported operational scopes in this slice are:

| Scope | Behavior |
|---|---|
| Tenant | Allows all Finance records in the current tenant up to the grant's access level. |
| Bank account | Restricts AP payment reads and commands to the selected tenant bank account. |

`GeneralLedgerAccount`, `Branch`, and `CostCentre` enum values are reserved for later work, but grant administration rejects them for now. This is intentional: accepting an unenforced grant would create a false security control.

Bank-account scope is applied to AP payment list queries and checked again on direct reads, trace, creation, allocation, posting, clearing, voiding, allocation reversal, and posted-payment reversal. Denied attempts generate audit events. Grant rows are effective-dated, concurrency-protected, and deactivated rather than deleted so historic access decisions remain explainable.

## Administration and endpoints

Users with `Finance.AccessScopes.Manage` can administer grants at `/administration/finance/access-scopes`.

| Method and route | Purpose |
|---|---|
| `GET /api/finance/access-scopes` | List grants, optionally for one user. |
| `GET /api/finance/access-scopes/users` | List active tenant users. |
| `GET /api/finance/access-scopes/bank-accounts` | List selectable tenant bank accounts. |
| `POST /api/finance/access-scopes` | Create an effective-dated grant. |
| `PUT /api/finance/access-scopes/{id}` | Update a grant with optimistic concurrency. |
| `POST /api/finance/access-scopes/{id}/deactivate` | End a grant while preserving history. |
| `GET /api/finance/settings` | Read reversal/access-control policy. |
| `PUT /api/finance/settings` | Update policy and activate enforcement after validation. |
| `POST /api/ap/payments/{id}/reverse` | Post a controlled AP payment reversal. |

## Safe activation sequence

1. Apply migration `20260802120000_AddFinanceAccessScopesAndApPaymentReversals`.
2. Assign `Finance.AccessScopes.Manage` only to the intended Finance security administrator role(s).
3. Assign `Finance.AP.Payments.Reverse` only to the approved correction/authorization role(s).
4. Create and review effective tenant or bank-account grants for every AP user.
5. Test representative list, direct-ID, posting, trace, and reversal operations for an in-scope and out-of-scope bank account.
6. Enable **Enforce Finance access scopes** in Finance settings.
7. Re-run the negative authorization scenarios and retain the result as UAT evidence.

## Deferred follow-on work

This slice deliberately does not claim completion of the full work packages:

- AR receipt/customer-payment reversal (`FIN-LIM-0010`) is implemented in the next documented slice; see `tdc-ar-receipt-reversal.md`.
- Cash/bank reversal (`FIN-LIM-0011`) follows after AR.
- Report, export, schedule, and attachment scope propagation remains in WP8/WP9.
- Branch, GL-account, cost-centre, and other Finance-dimension enforcement remains in WP9.
- A separate maker-checker request/approval workflow for AP reversals remains a WP0/WP2 control enhancement; this slice uses a dedicated reversal permission as its authorization gate.
- Platform-wide tenant-scoped role-grant redesign remains outside Finance and is not represented as resolved by this mitigation.

## Verification coverage

Automated coverage includes:

- posted payment reversal, invoice restoration, balancing journal, negative allocation, lineage, and audit evidence;
- duplicate reversal idempotency;
- access enforcement disabled behavior;
- fail-closed behavior when enabled without a matching user grant;
- bank-account and access-level restrictions; and
- tenant-administrator recovery behavior.

The EF Core snapshot has also been compared with the runtime model to confirm the migration represents the complete model change.
