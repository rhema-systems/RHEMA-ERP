# Receipt landed-cost supplier selection

## Implemented

- Each charge in **Add receipt costs / Edit draft costs** has a saved **Cost supplier** selector. No goods supplier is assigned automatically.
- The selector uses the existing Business Partner lookup; active, approved, non-blacklisted supplier/contractor records are shown. Lookup failures are visible and retryable; existing draft supplier IDs are preserved.
- Supplier remains optional while preparing a draft. It must be resolved for supplier invoicing; this change does not introduce automatic invoice generation.
- **Supplier totals** is a collapsed summary in the entry dialog and saved voucher. Grouping uses supplier ID, charge currency and bill reference, not supplier name. Linked invoices remain separate from unlinked references.
- Existing DTO/entity fields persist supplier IDs and names. No migration, Finance behavior change, account mapping change or posting was made.
- Regular frontend source and the active rehearsal frontend copy have the same implementation. The running main UAT frontend still requires its next build/deployment.
- The walkthrough Markdown and HTML now include supplier selection and the summary.

## Checks

- Focused tests: 17 passed (11 entry-form tests, 6 grouping tests), including two independent supplier selections, supplier persistence in the save request, lookup retry, duplicate display names, different currencies/references, and unlinked invoices.
- Affected-source type check reports only the existing duplicate `fiscalPeriodId` declarations in `frontend/src/types/finance.ts` at lines 307 and 351; no remaining error is reported in the changed supplier-selection/grouping files. That unrelated Finance file was not changed.
- Rehearsal browser: opened REC260003 → Landed Cost → Add receipt costs. Supplier options loaded from saved records. Selected a supplier and entered GHS 310 to inspect the supplier total, then clicked **Cancel**. These were unsaved UI test inputs, not assignment of the existing freight charge to that supplier.
- LC26090977 remains the original posted GHS 360 voucher with no supplier invoice created by this change.

## AP integration follow-up

The existing inventory posting has already debited inventory and credited GRV accrual. A subsequent landed-cost supplier invoice must clear that accrual and credit the selected supplier payable, with applicable taxes handled by AP. It must not debit inventory a second time.

The user subsequently authorized a narrow AP-owned integration. `VendorInvoiceService.LandedCosts.cs` now generates grouped drafts with a protected charge-to-invoice source link. Ordinary expense lines still reject control accounts; only verified posted landed-cost source lines can clear their original accrual account.

The handoff preserves existing AP approval/posting controls, supplier/currency validation and tax review. It requires migration `20260909213000_LinkLandedCostsToSupplierInvoiceLines`. Live supplier assignment, draft creation and AP posting are not yet claimed as tested: the billing supplier identities, references and tax treatment remain user inputs. No direct GL writes or approval bypasses have been added.
