# Supplier helpdesk entry controls (requirements 15–17)

Implementation is present; coordinated backend tests and authenticated browser acceptance are pending.

The supplier portal now starts with Category. The selected category or its nearest configured parent determines Type through the existing `EhcTicketCategory.AppliesToType` configuration. Type is displayed after selection, Priority is hidden, and Website is displayed without an editable Channel field. Unmapped options are disabled, and server validation rejects unmapped, deleted, foreign-tenant, cyclic or unrelated category/subcategory input.

`EhcExternalTicketPolicy` runs only from `CreateExternalTicketAsync`, before ticket persistence, SLA selection, workflow routing and audit creation. It replaces client Type, Priority and Source. Source is always `Web`. No category/type priority setting exists in the current entities, so the policy preserves the portal's existing Medium default; when configured Medium is disabled it uses the first active priority by existing sort order and priority order. With no configured priorities, Medium remains the default. With configuration present but no enabled priority, creation returns an actionable validation error.

Internal ticket creation and Estate's dedicated property-enquiry methods continue through their existing paths without this normalization. No schema changes or historical-ticket rewrites are involved. Existing external authorization and CAPTCHA enforcement remain in place.

Validation completed: `git diff --check`; targeted TypeScript transpilation of the supplier entry page (zero syntax errors). Added 19 `EhcExternalTicketPolicyTests` cases for client-field spoofing, nested mapping, invalid category relationships/configuration, tenant isolation and default-priority behavior; these await the coordinated test build.

Remaining acceptance:

- Run the focused Core tests and existing Helpdesk/Estate regressions in the coordinated backend build.
- As an external supplier, select categories mapped to different Types and submit; verify saved Type, Priority and Web source, plus SLA/workflow/audit values.
- Submit altered Type/Priority/Channel through the external API and verify the configured values prevail.
- Confirm unmapped categories and disabled priority configuration return HTTP 400 with an actionable message.
- Confirm internal ticket entry still permits its existing Type/Priority/Channel choices and Estate property enquiries still use their existing defaults.
