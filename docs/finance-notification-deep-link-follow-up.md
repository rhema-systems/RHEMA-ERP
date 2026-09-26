# FIN-FOLLOW-NOTIFICATION-DEEP-LINKS: Finance Notification Action Routing

## Status

Deferred follow-up. This record does not authorize implementation in the current accounting-book lifecycle pass.

## Problem Statement

The shared notification UI already navigates when a notification contains an `ActionUrl`. Finance workflow notifications do not consistently supply one. Current database evidence shows `ActionUrl = NULL` for accounting-book lifecycle, initialization, and exact-book period notifications, so those notifications can be marked as read but cannot take the user to the relevant Finance action or review screen.

A generic Finance landing-page URL would improve navigation, but actionable notifications should ultimately resolve the exact document, book, period, approval, exception, or task that requires attention.

## Required Scope

Inventory every notification producer and workflow entity across the Finance module, including:

- accounting books, initialization evidence, exact-book periods, and lifecycle transitions;
- general-ledger journals, journal batches, recurring journals, allocations, and reversals;
- accounts payable and receivable documents, settlements, payments, returns, and credit notes;
- cash, bank, reconciliation, deposit, transfer, and returned-cheque actions;
- budgeting, unit finance, commitments, revisions, and close activities;
- fixed assets, depreciation, valuation, transfer, disposal, verification, leases, and capital projects;
- tax, statutory, reporting, period-close, exception, and approval workspaces.

The inventory must cover maker, checker, rejection, approval, assignment, escalation, failure, and completion notifications—not only `Approval Required` messages.

## Implementation Direction

1. Define an authoritative route for every Finance workflow entity and direct Finance notification type.
2. Populate `ActionUrl` when the notification is created. Prefer exact internal deep links over generic module landing pages.
3. Where the notification entity is a child record, resolve its parent route—for example, initialization and exact-book period IDs must resolve to their accounting book and the relevant readiness section.
4. Add query parameters or route segments that allow the destination page to select the referenced record and open the relevant review/action surface.
5. Retain a safe Finance landing-page fallback only where no exact destination exists.
6. Keep route generation tenant-safe and internal; never accept an arbitrary external URL from user-controlled data.
7. Preserve existing read/dismiss behavior and authorization checks. A deep link must not grant access the recipient does not already possess.

## Acceptance Criteria

- Every actionable Finance notification has a non-empty, valid internal `ActionUrl`.
- Clicking an accounting-book lifecycle notification opens the referenced book and appropriate lifecycle review context.
- Clicking an initialization or exact-book period notification opens the parent book's periods-and-initialization context and identifies the relevant record.
- Checker notifications open the precise approval or review action; maker rejection/completion notifications open the resulting Finance record and decision evidence.
- Links work from the header bell, notification center, personal notifications page, real-time delivery, and persisted notifications loaded after login.
- Unauthorized, deleted, cross-tenant, and no-longer-actionable destinations fail safely with an explanatory state.
- Automated coverage verifies route generation for every registered Finance workflow entity and representative direct-notification producers.
- A diagnostic reports Finance notifications with missing or invalid action URLs so future producers cannot silently regress.

## Ownership Boundary

Delivery requires coordination between Finance route owners and the shared workflow/notification owner. Finance owns the destination routes and parent-entity resolution rules; the shared notification layer owns consistent transport and click behavior.

