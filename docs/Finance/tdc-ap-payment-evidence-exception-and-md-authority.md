# TDC AP Payment Evidence, Exception, and Executive Authority

**Implemented:** 2026-08-03  
**Work package:** WP4 - AP vouchers, supporting evidence, statements, and WHT  
**Scope:** Direct Finance AP payments only; no Procurement, Payroll, bank, mobile-money, or tax-authority interface

## Outcome

Direct `VendorPayment` records now use the existing workflow and evidence platform instead of
remaining in Draft until somebody posts them. Submission resolves the applicable published
approval policy using the payment method and functional-currency amount, stores the complete
configuration as immutable JSON plus a SHA-256 digest, and starts the existing `VendorPayment`
workflow. Posting continues to reject every payment that is not `Authorized`.

## Seeded TDC defaults

The thresholds are database configuration, not controller constants. They can be replaced through
the effective-dated policy editor with maker-checker publication.

| Policy | Match | Required verified evidence | Authority |
|---|---|---|---|
| `TDC-AP-PAYMENT-BASE` | Other direct payments | Approved payment supporting pack | Chief Accountant |
| `TDC-AP-PAYMENT-CASH` | Cash | Supporting pack; cash custody/recipient acknowledgement | Chief Accountant |
| `TDC-AP-PAYMENT-MOBILE` | Mobile money | Supporting pack; transaction confirmation | Chief Accountant |
| `TDC-AP-PAYMENT-HIGH` | Functional amount at least GHS 100,000 | Supporting pack; high-value authority memorandum | Chief Accountant, then Managing Director |
| `TDC-AP-PAYMENT-HIGH-CASH` | Cash and functional amount at least GHS 100,000 | Supporting pack; cash custody; high-value authority memorandum | Chief Accountant, then Managing Director |

The combined high-value cash policy has the highest priority so both method-specific custody
evidence and high-value authority remain mandatory. Exceptional-payment or evidence-exception
requests always set the Managing Director condition, regardless of amount.

## Control flow

1. The maker reviews the Draft policy preview on the payment page.
2. Submission records any exceptional-payment or evidence-exception declaration. Each reason must
   meet the policy's configured minimum length (TDC seed: 30 characters).
3. The API freezes the policy identifier, code, JSON, and hash on `VendorPayment`, moves the payment
   to `PendingAuthorization`, and starts the workflow.
4. The maker uploads files against named requirement keys on the current approval step.
5. A separate authorized Finance reviewer verifies clean, current evidence. The uploader cannot
   verify their own file.
6. Chief Accountant approval cannot complete while required evidence is missing or unverified,
   unless a recorded evidence exception is following its configured executive route.
7. The Managing Director step activates for the high-value policy, an exceptional payment, or an
   evidence exception. Final authorization verifies that the configured executive role actually
   approved; a completed generic workflow is not sufficient.
8. Rejection returns the direct payment to Draft and clears submission-derived policy and authority
   state so a corrected resubmission receives a fresh policy snapshot. The rejection reason and
   prior identifiers remain in audit history.

## Security and governance

- Submission requires `Finance.AP.Payments.Process`; control status requires `Finance.Read`.
- Approval and rejection continue through the Finance workflow permission policies.
- The seeded `Managing Director` role can read Finance, decide assigned workflow items, and run
  reports, but cannot create, edit, post, or administer payments.
- Policy creation/update and publication require different administrator identities.
- Published policies are immutable; changes are made by cloning a new effective-dated draft.
- Workflow evidence verification permits the configured Finance reviewer roles but rejects
  self-verification at the API even if a user otherwise has the role.
- Payment bank-account access scope is checked for submission and control reads.
- Submission, blocked approval, decision, rejection, authorization, posting, and controlled voucher
  generation remain auditable events against the same payment reference.

## Existing features extended

This work deliberately reuses `VendorPayment`, `SimpleWorkflowService`, effective-dated
`WorkflowApprovalPolicySet`, conditional/sequential workflow rules, `WorkflowEvidenceDocument`,
Finance approvals, permission policies, access scopes, central posting, and the existing payment
detail page. No parallel AP approval, attachment, exception, or posting subsystem was introduced.

## Limitation disposition

- `FIN-LIM-0002`: the existing audited output foundation remains unchanged.
- `FIN-LIM-0004`, `FIN-LIM-0019`, and `FIN-LIM-0054`: WHT fields and current certificate output are
  preserved; threshold/remittance and the full statutory certificate lifecycle remain open.
- `FIN-LIM-0009`: authorized payments retain the implemented immutable compensating reversal path.
- `FIN-LIM-0014`: Finance bank-account scope is enforced on submission, control status, voucher,
  posting, and reversal paths.
- `FIN-LIM-0018`: workflow authorization changes operational status only; GL continues through the
  central posting engine.
- `FIN-LIM-0045`: supplier advances/unapplied payments remain supported and visible; approval does
  not fabricate an invoice allocation.
- `FIN-LIM-0047`: external/statutory filing packs and the managed certificate lifecycle remain open.

No documented `FIN-LIM-*` resolution was weakened or silently reclassified by this slice.

## Remaining WP4 work

- WHT threshold application, remittance tracking, certificate issue/reissue/cancel workflow, and
  statutory register/filing-pack UX without direct external submission.

Supplier statement PDF and native spreadsheet output is now implemented by
`tdc-ap-supplier-statement-controlled-output.md`.
