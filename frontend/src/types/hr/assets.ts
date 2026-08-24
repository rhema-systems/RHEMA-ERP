/**
 * HR Area 16 — Staff / Company Assets.
 *
 * ⚠ Every shape here was written from a MEASURED payload, not from an endpoint name. The harness
 * at `dev-harness/hr-assets/run-slice6.mjs` dumps the real JSON of each portal read to
 * `slice6-payloads.json`, and `probe-slice6-ui.mjs` does the same for the two form pickers. That is
 * not ceremony: the first guess at the direct-reports route in that probe answered 404, and an
 * area-12 lesson is that a TypeScript type written from what a route is called is fiction that
 * type-checks all the way to a blank screen.
 *
 * Slice 6 covers the employee's own surface only. The admin/HR register types arrive with its
 * screens; this file grows then rather than being written ahead of anything that reads it.
 */

/** Serialised as a string on every read DTO in this area, never as an int. */
export type AssignmentStatus =
  | 'Active'
  | 'Returned'
  | 'Overdue'
  | 'Lost'
  | 'Damaged'
  | 'Transferred';

export type AssignmentType = 'Permanent' | 'Temporary' | 'ProjectBased' | 'ShortTermLoan';

export type AssetRequisitionStatus =
  | 'Draft'
  | 'Submitted'
  | 'UnderReview'
  | 'Approved'
  | 'Rejected'
  | 'Fulfilled'
  | 'Cancelled';

/**
 * ⚠ The numbers run the opposite way to the reading order: Urgent is 1 and Low is 4. The create
 * payload takes the number, so a form that offers "1, 2, 3, 4" as increasing urgency would file
 * every emergency as an afterthought. Always pick from `ASSET_REQUISITION_PRIORITIES`.
 */
export type AssetRequisitionPriority = 'Urgent' | 'High' | 'Medium' | 'Low';

export const ASSET_REQUISITION_PRIORITIES: ReadonlyArray<{
  value: number;
  label: AssetRequisitionPriority;
}> = [
  { value: 1, label: 'Urgent' },
  { value: 2, label: 'High' },
  { value: 3, label: 'Medium' },
  { value: 4, label: 'Low' },
];

/**
 * One row of "what I hold" or "what I have held".
 *
 * The asset id, number, type name and the two acknowledgement fields were added in slice 6: the
 * list could name an asset but not identify it, and could not say whether the holder had signed
 * for it — so a screen would have had to re-fetch every row in full to render either.
 */
export interface AssetAssignmentSummary {
  id: string;
  assignmentNumber: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  assetTypeName: string;
  employeeId: string;
  employeeName: string;
  /** DateOnly — `YYYY-MM-DD`, no time part. */
  assignmentDate: string;
  expectedReturnDate: string | null;
  type: AssignmentType;
  typeName: string;
  status: AssignmentStatus;
  statusName: string;
  /** Full timestamp, unlike the two dates above. */
  returnDate: string | null;
  /**
   * Signed, so a late custody is negative — slice 11 added it and the slice-12 probe found it
   * missing from this type. Null where no return was ever expected. Server-computed: never derive
   * it in the browser, or a page open past midnight starts disagreeing with the watchlist.
   */
  daysUntilReturnDue: number | null;
  employeeAcknowledged: boolean;
  acknowledgementDate: string | null;

  /**
   * What the holder is charged for this, per period — AST-10.
   *
   * ⚠ `rentalAmount: 0` is not the same as `null`. Zero means the asset is provided free — a
   * stated arrangement, and usually a taxable one — while null means no terms have been set. A
   * screen that treats them alike loses the difference between "free accommodation" and "nobody
   * has set this up yet".
   */
  rentalAmount: number | null;
  rentalCurrencyCode: string | null;
  rentalFrequencyName: string | null;
  isBenefitInKind: boolean;
}

/**
 * The full assignment record — what one row opens into.
 *
 * ⚠ It extends the summary because, as of the slice-12 content audit, it genuinely is a superset.
 * It was not: `assetTypeName` was on the summary and missing from the record (defect **D-ll**), so
 * this declaration was fiction until the backend caught up with it. If a field is ever added to
 * one side again, this `extends` will keep type-checking while the field arrives `undefined` at
 * runtime — which is why the audit asserts the two reads agree rather than trusting the type.
 */
export interface AssetAssignment extends Omit<AssetAssignmentSummary, 'type' | 'status'> {
  tenantId: string;
  type: AssignmentType;
  status: AssignmentStatus;
  employeeNumber: string | null;
  requisitionId: string | null;
  requisitionNumber: string | null;
  transferId: string | null;
  transferNumber: string | null;
  termsDocumentSentAt: string | null;
  termsDocumentSentTo: string | null;
  termsDocumentSentByName: string | null;
  purpose: string;
  purposeName: string;
  assignmentNotes: string | null;
  isPrimaryUser: boolean;
  conditionAtAssignment: string;
  conditionAtAssignmentName: string;
  conditionNotes: string | null;
  approvedById: string | null;
  approvedByName: string | null;
  approvalDate: string | null;
  /** What the holder is answerable for. The signed terms document states these in words. */
  responsibleForLoss: boolean;
  responsibleForDamage: boolean;
  termsAndConditions: string | null;
  conditionAtReturn: string | null;
  conditionAtReturnName: string | null;
  returnNotes: string | null;
  returnedInGoodCondition: boolean;
  returnedToId: string | null;
  returnedToName: string | null;
  damageReported: boolean;
  damageDescription: string | null;
  employeeLiable: boolean;
  repairCost: number | null;
  replacementCost: number | null;

  // Rental — the full record carries the terms; the summary carries only what a list column needs.
  /** The enum member, where the summary gives only `rentalFrequencyName`. */
  rentalFrequency: RentalDeductionFrequency | null;
  rentalEffectiveFrom: string | null;
  rentalEffectiveTo: string | null;
  benefitInKindValue: number | null;
  /** Server-computed: terms have been set at all. Distinguishes `rentalAmount: 0` from no terms. */
  hasRentalTerms: boolean;
  /** Server-computed: the terms are in force today, not merely recorded. */
  isRentalRunning: boolean;
}

/**
 * What the three custody-closing writes actually answer.
 *
 * ⚠ `return`, `acknowledge` and `report-incident` return a **message**, not the updated
 * assignment — measured, and the opposite of what their siblings do. A caller that read the
 * response as a record would get `undefined` for every field and no error to explain it; refetch
 * the assignment instead.
 */
export interface AssetActionAck {
  message: string;
}

/** One row of "requests I raised, or that were raised for me". */
export interface AssetRequisitionSummary {
  id: string;
  requisitionNumber: string;
  requestedById: string;
  requestedByName: string;
  /** Null when the request is for the requester themselves. */
  beneficiaryEmployeeId: string | null;
  beneficiaryEmployeeName: string | null;
  /** The beneficiary where one is named, the requester otherwise. Computed server-side. */
  forEmployeeName: string;
  isOnBehalf: boolean;
  requestDate: string;
  assetTypeName: string;
  quantity: number;
  priority: AssetRequisitionPriority;
  priorityName: string;
  status: AssetRequisitionStatus;
  statusName: string;
  requiredByDate: string | null;
}

/** The full requisition record. */
export interface AssetRequisition extends AssetRequisitionSummary {
  tenantId: string;
  forEmployeeId: string;
  assetTypeId: string;
  description: string;
  justification: string;
  approvedById: string | null;
  approvedByName: string | null;
  approvalDate: string | null;
  approvalComments: string | null;
  rejectedDate: string | null;
  rejectionReason: string | null;
  isFulfilled: boolean;
  fulfilledWith: AssetRequisitionFulfilment[];
  fulfilledDate: string | null;
  fulfilledById: string | null;
  fulfilledByName: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** What a requisition actually produced — one entry per assignment that cites it. */
export interface AssetRequisitionFulfilment {
  assignmentId: string;
  assignmentNumber: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  employeeId: string;
  employeeName: string;
}

export interface CreateAssetRequisitionRequest {
  assetTypeId: string;
  /** Omit for yourself. Naming somebody else requires being their line manager, or HR. */
  beneficiaryEmployeeId?: string | null;
  description: string;
  quantity: number;
  /** The NUMBER, not the name — see the warning on `AssetRequisitionPriority`. */
  priority: number;
  justification: string;
  requiredByDate?: string | null;
}

export type UpdateAssetRequisitionRequest = CreateAssetRequisitionRequest;

// ── Surcharges — AST-3, decision D9 ────────────────────────────────────────────

export type AssetSurchargeStatus =
  | 'Draft'
  /** Put to the employee. They may or may not have answered — see `employeeResponse`. */
  | 'WithEmployee'
  | 'Submitted'
  | 'Approved'
  | 'Rejected'
  | 'Recovering'
  | 'Recovered'
  | 'Waived'
  | 'Cancelled';

export type AssetSurchargeReason = 'Damage' | 'Loss' | 'NotReturned' | 'Other';

export type AssetSurchargeEmployeeResponse = 'NotYetGiven' | 'Accepted' | 'Disputed';

export type AssetSurchargeRecoveryMethod = 'PayrollDeduction' | 'DirectPayment' | 'ExitSettlement';

/**
 * One row of "what I am being charged for".
 *
 * ⚠ A charge is invisible to its subject until it has been served on them — the backend answers
 * 404, not 403, so nothing here ever shows an employee a draft being written about them.
 */
export interface AssetSurchargeSummary {
  id: string;
  surchargeNumber: string;
  assignmentId: string;
  assignmentNumber: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  employeeId: string;
  employeeName: string;
  reason: AssetSurchargeReason;
  reasonName: string;
  assessedAmount: number;
  currencyCode: string;
  amountRecovered: number;
  amountOutstanding: number;
  status: AssetSurchargeStatus;
  statusName: string;
  employeeResponse: AssetSurchargeEmployeeResponse;
  employeeResponseName: string;
  isDisputed: boolean;
  raisedAt: string;
  recoveryStartDate: string | null;
}

export interface AssetSurcharge extends AssetSurchargeSummary {
  tenantId: string;
  description: string;
  /** What the assignment said the damage cost when this was raised — the basis, kept. */
  basisRepairCost: number | null;
  basisReplacementCost: number | null;
  /** True where the charge was set below what the damage actually cost. */
  isBelowAssessedCost: boolean;
  raisedById: string | null;
  raisedByName: string | null;
  /** When the charge was put to the employee. Null means it has not been. */
  notifiedAt: string | null;
  employeeRespondedAt: string | null;
  employeeResponseComments: string | null;
  /** Why it went for approval although the employee never answered. */
  proceededWithoutResponseReason: string | null;
  isAwaitingEmployee: boolean;
  approvedById: string | null;
  approvedByName: string | null;
  approvalDate: string | null;
  approvalComments: string | null;
  rejectedDate: string | null;
  rejectionReason: string | null;
  recoveryMethod: AssetSurchargeRecoveryMethod | null;
  recoveryMethodName: string | null;
  instalmentCount: number | null;
  recoveryStartDate: string | null;
  /** What one instalment comes to — a statement of intent to payroll, not a schedule. */
  instalmentAmount: number | null;
  recoveries: AssetSurchargeRecovery[];
  waivedById: string | null;
  waivedByName: string | null;
  waivedAt: string | null;
  waiverReason: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface AssetSurchargeRecovery {
  id: string;
  surchargeId: string;
  amount: number;
  recoveredOn: string;
  method: AssetSurchargeRecoveryMethod;
  methodName: string;
  reference: string | null;
  notes: string | null;
  recordedById: string | null;
  recordedByName: string | null;
}

/** The employee's answer. Both outcomes send the charge on for approval. */
export interface RespondToAssetSurchargeRequest {
  accepted: boolean;
  comments?: string | null;
}

/** The counters the portal landing opens with, and the short lists behind them. */
export interface EmployeeAssetSummary {
  employeeId: string;
  heldCount: number;
  awaitingAcknowledgementCount: number;
  overdueReturnCount: number;
  openRequisitionCount: number;
  draftRequisitionCount: number;
  /** Charges served on them that they have not yet answered — AST-3, decision D9. */
  surchargesAwaitingMyResponseCount: number;
  /** Charges against them that are still live: not rejected, waived or fully recovered. */
  openSurchargeCount: number;
  outstandingSurchargeAmount: number;
  held: AssetAssignmentSummary[];
  awaitingAcknowledgement: AssetAssignmentSummary[];
  openRequisitions: AssetRequisitionSummary[];
  surchargesAwaitingMyResponse: AssetSurchargeSummary[];
}

/**
 * The responsibility-and-terms document — AST-5.
 *
 * `htmlBody` is a complete, self-contained HTML document rendered from an HR-editable template.
 * It is meant to be printed and signed, so it is opened in its own window rather than injected
 * into this page: the letter carries its own `<html>` and styling, and putting a whole document
 * inside a screen's DOM is how a print stylesheet ends up printing the navigation.
 */
export interface AssetTermsLetter {
  assignmentId: string;
  assignmentNumber: string;
  employeeId: string;
  employeeName: string;
  assetId: string;
  assetNumber: string;
  subject: string;
  htmlBody: string;
  termsDocumentSentAt: string | null;
  termsDocumentSentTo: string | null;
}

/** The asset-type picker on the request form. `GET api/Assets/types` — open to any employee. */
export interface AssetTypeSummary {
  id: string;
  name: string;
  description: string | null;
  hasExtraAttributes: boolean;
  assetCount: number;
  attributeCount: number;
}

// ═══════════════════════════════════════════════════════════════════════════════
// SLICE 12 — the HR register. Everything below was measured, not named.
//
// Source: `dev-harness/hr-assets/slice12-ui-payloads.json`, written by
// `probe-slice12-ui.mjs` against fixtures it creates itself. That file is the type source; when a
// backend read changes, re-run the probe and diff it rather than editing these by hand.
//
// The probe caught three guesses before a line of this was written:
//   · the employee picker is `POST /hr/Employees/paged?page=` — a GET answered **405**;
//   · the register report's population total is `assetCount`, NOT `totalAssets`;
//   · a served surcharge cannot be DELETEd — it is cancelled, and the delete answers 409.
// ═══════════════════════════════════════════════════════════════════════════════

/** Serialised as the member NAME on every read. ⚠ `status/{status}` takes the name too. */
export type CompanyAssetStatus =
  | 'Available'
  | 'Assigned'
  | 'InMaintenance'
  | 'Damaged'
  | 'LostStolen'
  | 'Disposed'
  | 'Reserved';

export type HRAssetCondition = 'Excellent' | 'Good' | 'Fair' | 'Poor' | 'NonFunctional';

/**
 * Where the asset came from, and therefore who owns its money — decision D1.
 *
 * `FixedAssetsModule` means Finance holds the purchase figures, the depreciation and the disposal;
 * HR refuses to edit them. `HrCreated` means HR owns everything about it.
 */
export type AssetSource = 'HrCreated' | 'FixedAssetsModule';

export type HRAssetTransferStatus =
  | 'Draft'
  | 'Pending'
  | 'Approved'
  | 'InTransit'
  | 'Completed'
  | 'Rejected'
  | 'Cancelled';

/** ⚠ `DepartmentToDepartment` is refused by the API — nothing anywhere records a department. */
export type HRAssetTransferType =
  | 'EmployeeToEmployee'
  | 'LocationToLocation'
  | 'DepartmentToDepartment'
  | 'UnitToUnit';

export type AssetMaintenanceStatus = 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled';

export type AssetMaintenanceType =
  | 'Preventive'
  | 'Corrective'
  | 'Inspection'
  | 'Repair'
  | 'Upgrade';

export type AssignmentPurpose =
  | 'RegularWork'
  | 'SpecialProject'
  | 'Travel'
  | 'Training'
  | 'Replacement';

export type RentalDeductionFrequency = 'Monthly' | 'Fortnightly' | 'Weekly' | 'Annually';

/**
 * ⚠ **Alphabetical, not the order anyone would guess** — `Checkbox` is 1 and `Text` is 6, although
 * the C# default is Text. The probe measured `dataType: 1` coming back as `"Checkbox"` from a field
 * a form would naturally have labelled "Text". Never write the number by hand.
 */
export type AssetAttributeDataType =
  | 'Checkbox'
  | 'Date'
  | 'Decimal'
  | 'Dropdown'
  | 'Integer'
  | 'Text';

// ── The numeric ladders the WRITE payloads take ────────────────────────────────
//
// Reads give names; creates and updates take numbers. Every one of these is a place a form built
// from reading order would file the wrong value, so each is a named constant and no screen writes
// a literal.

export const COMPANY_ASSET_STATUSES: ReadonlyArray<{ value: number; label: CompanyAssetStatus; text: string }> = [
  { value: 1, label: 'Available', text: 'Available' },
  { value: 2, label: 'Assigned', text: 'Assigned' },
  { value: 3, label: 'InMaintenance', text: 'In maintenance' },
  { value: 4, label: 'Damaged', text: 'Damaged' },
  { value: 5, label: 'LostStolen', text: 'Lost / stolen' },
  { value: 6, label: 'Disposed', text: 'Disposed' },
  { value: 7, label: 'Reserved', text: 'Reserved' },
];

export const ASSET_CONDITIONS: ReadonlyArray<{ value: number; label: HRAssetCondition; text: string }> = [
  { value: 1, label: 'Excellent', text: 'Excellent' },
  { value: 2, label: 'Good', text: 'Good' },
  { value: 3, label: 'Fair', text: 'Fair' },
  { value: 4, label: 'Poor', text: 'Poor' },
  { value: 5, label: 'NonFunctional', text: 'Non-functional' },
];

export const ASSIGNMENT_TYPES: ReadonlyArray<{ value: number; label: AssignmentType; text: string }> = [
  { value: 1, label: 'Permanent', text: 'Permanent assignment' },
  { value: 2, label: 'Temporary', text: 'Temporary assignment' },
  { value: 3, label: 'ProjectBased', text: 'Project-based' },
  { value: 4, label: 'ShortTermLoan', text: 'Short-term loan' },
];

export const ASSIGNMENT_PURPOSES: ReadonlyArray<{ value: number; label: AssignmentPurpose; text: string }> = [
  { value: 1, label: 'RegularWork', text: 'Regular work' },
  { value: 2, label: 'SpecialProject', text: 'Special project' },
  { value: 3, label: 'Travel', text: 'Travel' },
  { value: 4, label: 'Training', text: 'Training' },
  { value: 5, label: 'Replacement', text: 'Replacement' },
];

/** ⚠ `DepartmentToDepartment` (3) is deliberately absent — the API refuses it in words. */
export const ASSET_TRANSFER_TYPES: ReadonlyArray<{ value: number; label: HRAssetTransferType; text: string }> = [
  { value: 1, label: 'EmployeeToEmployee', text: 'Employee to employee' },
  { value: 2, label: 'LocationToLocation', text: 'Location to location' },
  { value: 4, label: 'UnitToUnit', text: 'Unit to unit' },
];

export const ASSET_MAINTENANCE_TYPES: ReadonlyArray<{ value: number; label: AssetMaintenanceType; text: string }> = [
  { value: 1, label: 'Preventive', text: 'Preventive maintenance' },
  { value: 2, label: 'Corrective', text: 'Corrective maintenance' },
  { value: 3, label: 'Inspection', text: 'Inspection' },
  { value: 4, label: 'Repair', text: 'Repair' },
  { value: 5, label: 'Upgrade', text: 'Upgrade' },
];

export const ASSET_MAINTENANCE_STATUSES: ReadonlyArray<{ value: number; label: AssetMaintenanceStatus; text: string }> = [
  { value: 1, label: 'Scheduled', text: 'Scheduled' },
  { value: 2, label: 'InProgress', text: 'In progress' },
  { value: 3, label: 'Completed', text: 'Completed' },
  { value: 4, label: 'Cancelled', text: 'Cancelled' },
];

export const RENTAL_FREQUENCIES: ReadonlyArray<{ value: number; label: RentalDeductionFrequency; text: string }> = [
  { value: 1, label: 'Monthly', text: 'Monthly' },
  { value: 2, label: 'Fortnightly', text: 'Fortnightly' },
  { value: 3, label: 'Weekly', text: 'Weekly' },
  { value: 4, label: 'Annually', text: 'Annually' },
];

export const ASSET_ATTRIBUTE_DATA_TYPES: ReadonlyArray<{ value: number; label: AssetAttributeDataType; text: string }> = [
  { value: 6, label: 'Text', text: 'Text' },
  { value: 5, label: 'Integer', text: 'Whole number' },
  { value: 3, label: 'Decimal', text: 'Decimal number' },
  { value: 2, label: 'Date', text: 'Date' },
  { value: 1, label: 'Checkbox', text: 'Yes / no' },
  { value: 4, label: 'Dropdown', text: 'Dropdown' },
];

export const DISPOSAL_METHODS: ReadonlyArray<{ value: number; label: string }> = [
  { value: 1, label: 'Sold' },
  { value: 2, label: 'Donated' },
  { value: 3, label: 'Recycled' },
  { value: 4, label: 'Discarded' },
  { value: 5, label: 'Transferred' },
];

// ── The register ───────────────────────────────────────────────────────────────

/**
 * One row of the register list, and of every watchlist that answers with assets.
 *
 * ⚠ `purchaseCost` is the ACQUISITION cost. It was called `currentValue` until slice 11 (defect
 * D-jj) — a name that promised a depreciated figure over a number that never was one. HR holds no
 * valuation; Finance does (decision D1). Do not label this column "value" on any screen.
 *
 * ⚠ There is no `assetTypeId` here, only the name — filtering by type means passing the id the
 * picker gave you, not one read off a row.
 */
export interface CompanyAssetSummary {
  id: string;
  assetNumber: string;
  assetTag: string;
  assetName: string;
  assetTypeName: string;
  status: CompanyAssetStatus;
  statusName: string;
  condition: HRAssetCondition;
  conditionName: string;
  locationId: string | null;
  locationName: string | null;
  unitId: string | null;
  unitName: string | null;
  isCurrentlyAssigned: boolean;
  currentAssignedToName: string | null;
  purchaseCost: number | null;
  isRentable: boolean;
  source: AssetSource;
  sourceName: string;
}

/** The full register record — what the detail screen and the edit form load. */
export interface CompanyAsset {
  id: string;
  tenantId: string;
  assetNumber: string;
  assetTag: string;
  assetName: string;
  description: string | null;
  /** AST-7 — "Additional Remarks". */
  additionalRemarks: string | null;
  assetTypeId: string;
  assetTypeName: string;

  source: AssetSource;
  sourceName: string;
  /** The Finance fixed asset this mirrors, where it came from there. */
  fixedAssetId: string | null;
  /** True on a `FixedAssetsModule` asset: the purchase fields are Finance's and read-only here. */
  isFinanceOwned: boolean;
  /** This asset's counterpart in the Maintenance module's register — decision D10. */
  maintenanceAssetId: string | null;
  isKnownToMaintenance: boolean;

  manufacturer: string | null;
  modelNumber: string | null;
  serialNumber: string | null;

  /** DateOnly — `YYYY-MM-DD`. */
  purchaseDate: string | null;
  purchaseCost: number | null;
  supplier: string | null;
  invoiceNumber: string | null;

  hasWarranty: boolean;
  warrantyStartDate: string | null;
  warrantyEndDate: string | null;
  warrantyProvider: string | null;
  /** Server-computed against today. */
  isWarrantyActive: boolean;

  color: string | null;
  size: string | null;
  specifications: string | null;

  status: CompanyAssetStatus;
  statusName: string;
  condition: HRAssetCondition;
  conditionName: string;

  locationId: string | null;
  locationName: string | null;
  locationDetails: string | null;
  unitId: string | null;
  unitName: string | null;

  isAssignable: boolean;
  isCurrentlyAssigned: boolean;
  currentAssignedToId: string | null;
  currentAssignedToName: string | null;

  /** AST-9 — whether a holder can be charged for this. The rate is the default, not the charge. */
  isRentable: boolean;
  standardRentalAmount: number | null;
  rentalCurrencyCode: string | null;

  requiresRegularMaintenance: boolean;
  maintenanceIntervalDays: number | null;
  lastMaintenanceDate: string | null;
  /**
   * ⚠ Not on the CREATE payload. An asset is registered unscheduled and dated by an edit — which
   * is why `unscheduled-maintenance` exists at all, and why a create form must not pretend to set it.
   */
  nextMaintenanceDate: string | null;

  isInsured: boolean;
  insurancePolicyNumber: string | null;
  insuredValue: number | null;
  /** AST-4 — when the cover lapses. */
  insuranceExpiryDate: string | null;
  isInsuranceExpired: boolean;

  disposalDate: string | null;
  disposalMethod: string | null;
  disposalNotes: string | null;

  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** The detail read — the record plus the four panels the detail screen renders beside it. */
export interface CompanyAssetDetail extends CompanyAsset {
  fixedAsset: FixedAssetLink | null;
  attributeValues: AssetAttributeValue[];
  recentAssignments: AssetAssignmentSummary[];
  recentMaintenance: AssetMaintenanceSummary[];
  attachments: AssetAttachment[];
}

/** What Finance says about a linked fixed asset. Read-only on every HR screen. */
export interface FixedAssetLink {
  fixedAssetId: string;
  assetCode: string;
  name: string;
  categoryName: string | null;
  netBookValue: number | null;
  statusName: string | null;
}

/**
 * ⚠ **This payload is FULL-REPLACE** (defect D-j): any field omitted is written as null. An edit
 * form must load the record, spread it, and send the whole thing — and `id` must be in the BODY as
 * well as the route or the call answers `400 ID mismatch`.
 */
export interface UpdateCompanyAssetRequest {
  id: string;
  assetNumber?: string;
  assetTag?: string;
  assetName: string;
  description?: string | null;
  additionalRemarks?: string | null;
  assetTypeId: string;
  manufacturer?: string | null;
  modelNumber?: string | null;
  serialNumber?: string | null;
  purchaseDate?: string | null;
  purchaseCost?: number | null;
  supplier?: string | null;
  invoiceNumber?: string | null;
  hasWarranty?: boolean;
  warrantyStartDate?: string | null;
  warrantyEndDate?: string | null;
  warrantyProvider?: string | null;
  color?: string | null;
  size?: string | null;
  specifications?: string | null;
  /** The NUMBER — see `COMPANY_ASSET_STATUSES`. */
  status: number;
  /** The NUMBER — see `ASSET_CONDITIONS`. */
  condition: number;
  locationId?: string | null;
  locationDetails?: string | null;
  unitId?: string | null;
  isAssignable?: boolean;
  maintenanceAssetId?: string | null;
  requiresRegularMaintenance?: boolean;
  maintenanceIntervalDays?: number | null;
  /** Settable on the UPDATE only — the create payload has no such field. */
  nextMaintenanceDate?: string | null;
  lastMaintenanceDate?: string | null;
  isInsured?: boolean;
  insurancePolicyNumber?: string | null;
  insuredValue?: number | null;
  insuranceExpiryDate?: string | null;
  isRentable?: boolean;
  standardRentalAmount?: number | null;
  rentalCurrencyCode?: string | null;
}

/** ⚠ No `nextMaintenanceDate` — it is set by a subsequent edit. See `UpdateCompanyAssetRequest`. */
export type CreateCompanyAssetRequest = Omit<
  UpdateCompanyAssetRequest,
  'id' | 'nextMaintenanceDate' | 'lastMaintenanceDate'
> & {
  attributeValues?: Array<{ assetTypeAttributeId: string; value: string }>;
};

/**
 * ⚠ `assetId` goes in the BODY as well as the route — `DisposeAssetDto` marks it `[Required]`, so
 * omitting it is a ModelState 400 rather than anything the route can supply. The same shape as the
 * register PUT's `id`, and the same trap.
 */
export interface DisposeAssetRequest {
  assetId: string;
  disposalDate: string;
  /** The NUMBER — see `DISPOSAL_METHODS`. */
  disposalMethod: number;
  disposalNotes?: string | null;
}

/**
 * Registering an HR asset from a Finance fixed asset — AST-11, decision D1.
 *
 * ⚠ `assetTypeId` is asked for rather than derived. Finance's categories are accounting classes and
 * do not map onto the things HR issues to people: "Office Equipment" is a depreciation class, not a
 * laptop. The purchase figures come across from Finance and stay Finance's — the resulting asset
 * reads `source: 'FixedAssetsModule'` and HR refuses to edit them.
 */
export interface CreateAssetFromFixedAssetRequest {
  fixedAssetId: string;
  assetTypeId: string;
  assetTag?: string;
  /** The NUMBER — see `ASSET_CONDITIONS`. */
  condition: number;
  locationId?: string | null;
  unitId?: string | null;
  isAssignable: boolean;
  additionalRemarks?: string | null;
}

/** ⚠ `assetId` and `assetTypeAttributeId` are both required in the body on the UPDATE too. */
export interface SetAssetAttributeValueRequest {
  assetId: string;
  assetTypeAttributeId: string;
  value: string;
}

/** Recording that money actually came back — the other half of a recovery plan. */
export interface RecordSurchargeRecoveryRequest {
  amount: number;
  /** DateOnly — `YYYY-MM-DD`. */
  recoveredOn: string;
  /** The NUMBER — 1 = PayrollDeduction, 2 = DirectPayment, 3 = ExitSettlement. */
  method: number;
  reference?: string | null;
  notes?: string | null;
}

/**
 * Raising a requisition from the register, on somebody's behalf — AST-6b.
 *
 * ⚠ `requiredByDate` is a full `DateTime` here, not the DateOnly the asset's own dates use.
 */
export interface CreateAssetRequisitionFromRegisterRequest {
  assetTypeId: string;
  /** Omit for yourself. Naming somebody else needs the HR role, or being their line manager. */
  beneficiaryEmployeeId?: string | null;
  description: string;
  quantity: number;
  /** The NUMBER — see `ASSET_REQUISITION_PRIORITIES`, and read the warning on it. */
  priority: number;
  justification: string;
  requiredByDate?: string | null;
}

// ── Types and their custom attributes ──────────────────────────────────────────

export interface AssetType {
  id: string;
  tenantId: string;
  name: string;
  description: string | null;
  hasExtraAttributes: boolean;
  attributeCount: number;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface AssetTypeDetail extends AssetType {
  attributes: AssetTypeAttribute[];
}

export interface AssetTypeAttribute {
  id: string;
  tenantId: string;
  assetTypeId: string;
  assetTypeName: string;
  attributeName: string;
  dataType: AssetAttributeDataType;
  dataTypeName: string;
  isRequired: boolean;
  isExpiryDate: boolean;
  /** For `Dropdown` — the option list, as the backend stores it. */
  attributeOptions: string;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface AssetAttributeValue {
  id: string;
  tenantId: string;
  assetId: string;
  assetTypeAttributeId: string;
  attributeName: string;
  dataType: AssetAttributeDataType;
  value: string;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/**
 * A file recorded against an asset — an invoice, a manual, a warranty certificate.
 *
 * ⚠ There is **no `attachmentType`** here. The first draft of this type declared one (and a
 * `attachmentTypeName` beside it) because `AssetAttachmentType` exists as an enum, and the file
 * table simply does not use it. The slice-12 probe measured this read as empty — an empty list is
 * not a shape — so the audit had to create a file before the truth was visible. A detail screen
 * built on the earlier guess rendered an empty column over a 200 response.
 */
export interface AssetAttachment {
  id: string;
  tenantId: string;
  assetId: string;
  fileName: string;
  /** NOT a URL — see the note above. Use `downloadAttachment`. */
  filePath: string;
  description: string | null;
  uploadDate: string;
  /** The EMPLOYEE who filed it. Null on a row written before the gate — slice 12b. */
  uploadedById: string | null;
  fileSizeBytes: number | null;
  /**
   * True once a file is really stored and scanned behind this row.
   *
   * False on anything written by the old JSON endpoint, which recorded a name and a path and
   * stored nothing. A download button on one of those answers 404, so the panel says
   * "No file stored" instead of offering one.
   */
  isStored: boolean;
  fileUploadRecordId: string | null;
  documentRecordId: string | null;
  documentVersionId: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/**
 * A photograph of an asset. Like the attachment above, it carries no type or description.
 *
 * ⚠ `createdAt` comes back as `0001-01-01T00:00:00` on this one — the create path never stamps
 * it. `uploadDate` is the field with the real timestamp; render that.
 */
export interface AssetImage {
  id: string;
  tenantId: string;
  assetId: string;
  fileName: string;
  /** NOT a URL — the files live outside the web root. Use `openImage`. */
  filePath: string;
  /** What the photograph is of — added with the upload gate in slice 12b. */
  caption: string | null;
  uploadDate: string;
  /** A USER id, not an employee id, and not resolved to a name. */
  uploadedBy: string | null;
  /** The EMPLOYEE who filed it — slice 12b. */
  uploadedById: string | null;
  fileSizeBytes: number | null;
  /** See the note on `AssetAttachment.isStored`. */
  isStored: boolean;
  fileUploadRecordId: string | null;
  documentRecordId: string | null;
  documentVersionId: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface CreateAssetTypeRequest {
  name: string;
  description?: string | null;
  hasExtraAttributes: boolean;
}

export interface CreateAssetTypeAttributeRequest {
  assetTypeId: string;
  attributeName: string;
  /** The NUMBER — see `ASSET_ATTRIBUTE_DATA_TYPES`, and read the warning on it. */
  dataType: number;
  isRequired: boolean;
  isExpiryDate: boolean;
  attributeOptions?: string;
}

// ── The two link pickers ───────────────────────────────────────────────────────

/**
 * A Finance fixed asset HR could mirror — slice 2b.
 *
 * ⚠ Rows another HR asset already claims are RETURNED and flagged, not filtered out, so the user
 * can see why the one they want is unavailable instead of hunting for a row that is nowhere.
 */
export interface FixedAssetPick {
  id: string;
  assetCode: string;
  name: string;
  categoryName: string | null;
  serialNumber: string | null;
  location: string | null;
  netBookValue: number | null;
  statusName: string | null;
  alreadyLinked: boolean;
  linkedCompanyAssetId: string | null;
}

/**
 * A Maintenance-module asset HR could point at — slice 9b, decision D10.
 *
 * ⚠ That register is effectively EMPTY on this tenant (its one live row is HR's own litter from
 * area 12). A screen here must carry a real empty state that says so.
 */
export interface MaintenanceAssetPick {
  id: string;
  assetNumber: string;
  name: string;
  categoryName: string | null;
  serialNumber: string | null;
  location: string | null;
  statusName: string | null;
  alreadyLinked: boolean;
  linkedCompanyAssetId: string | null;
}

// ── The watchlists ─────────────────────────────────────────────────────────────

/**
 * One row of the three maintenance watchlists — AST-1.
 *
 * ⚠ `due-maintenance` is **inclusive** of the overdue rows; `overdue-maintenance` is the exception
 * list cut out of it. The register report's counts are the other way round — DISJOINT, and named
 * for it (`maintenanceDueSoonCount`). Both behaviours are deliberate: a list opened by "what is
 * due?" must not hide what has already lapsed, while three counts in a row get added up.
 */
export interface AssetMaintenanceDueItem {
  id: string;
  assetNumber: string;
  assetTag: string;
  assetName: string;
  assetTypeName: string;
  status: CompanyAssetStatus;
  statusName: string;
  condition: HRAssetCondition;
  conditionName: string;
  locationId: string | null;
  locationName: string | null;
  unitId: string | null;
  unitName: string | null;
  isCurrentlyAssigned: boolean;
  currentAssignedToId: string | null;
  currentAssignedToName: string | null;
  requiresRegularMaintenance: boolean;
  maintenanceIntervalDays: number | null;
  lastMaintenanceDate: string | null;
  nextMaintenanceDate: string | null;
  /** False on the unscheduled list — the rows every other maintenance read filters away. */
  isScheduled: boolean;
  /** Signed, so the worst sorts first. Null where the asset has never been dated. */
  daysRemaining: number | null;
  isOverdue: boolean;
  /** The date the server answered as at — echo it, never recompute it in the browser. */
  asOf: string;
}

/**
 * One row of the three insurance watchlists — AST-4, slice 11.
 *
 * ⚠ `Disposed` assets are excluded and `LostStolen` ones are NOT — the opposite of the maintenance
 * reads — because a theft claim is exactly what the policy is for.
 */
export interface AssetInsuranceWatchItem {
  id: string;
  assetNumber: string;
  assetTag: string;
  assetName: string;
  assetTypeName: string;
  status: CompanyAssetStatus;
  statusName: string;
  condition: HRAssetCondition;
  conditionName: string;
  locationId: string | null;
  locationName: string | null;
  unitId: string | null;
  unitName: string | null;
  isCurrentlyAssigned: boolean;
  currentAssignedToId: string | null;
  currentAssignedToName: string | null;
  isInsured: boolean;
  insurancePolicyNumber: string | null;
  insuredValue: number | null;
  insuranceExpiryDate: string | null;
  /** False on the undated list — insured, and never given an expiry. */
  isDated: boolean;
  daysRemaining: number | null;
  isExpired: boolean;
  asOf: string;
}

// ── Maintenance records ────────────────────────────────────────────────────────

export interface AssetMaintenanceSummary {
  id: string;
  maintenanceNumber: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  /** Full timestamp on this one, not a DateOnly. */
  maintenanceDate: string;
  type: AssetMaintenanceType;
  typeName: string;
  status: AssetMaintenanceStatus;
  statusName: string;
  cost: number | null;
  nextMaintenanceDate: string | null;
  /** Set once the job has been pushed to the Maintenance module — slice 9b. */
  maintenanceAdmissionNumber: string | null;
  isAtWorkshop: boolean;
}

export interface AssetMaintenance extends AssetMaintenanceSummary {
  tenantId: string;
  description: string;
  workPerformed: string | null;
  partsReplaced: string | null;
  isInternalMaintenance: boolean;
  performedById: string | null;
  performedByName: string | null;
  externalServiceProvider: string | null;
  serviceTicketNumber: string | null;
  notes: string | null;
  maintenanceAdmissionId: string | null;
  maintenanceDischargeId: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

export interface CreateAssetMaintenanceRequest {
  assetId: string;
  /** ⚠ A full `DateTime` here, unlike the DateOnly fields on the asset. */
  maintenanceDate: string;
  /** The NUMBER — see `ASSET_MAINTENANCE_TYPES`. */
  type: number;
  description: string;
  isInternalMaintenance: boolean;
  performedById?: string | null;
  externalServiceProvider?: string | null;
  serviceTicketNumber?: string | null;
  cost?: number | null;
  workPerformed?: string | null;
  partsReplaced?: string | null;
  nextMaintenanceDate?: string | null;
  /** The NUMBER — see `ASSET_MAINTENANCE_STATUSES`. */
  status: number;
  notes?: string | null;
}

/**
 * Sending an asset to the Maintenance module's workshop — slice 9b, decision D10.
 *
 * ⚠ The required field is **`description`**, not `reason`. A payload built from the route's name
 * answers `400 The Description field is required` — the slice-12 audit walked into exactly that,
 * which is the whole argument for measuring a write payload rather than naming it.
 *
 * ⚠ `admissionType` is passed through to the other module untranslated: it starts a downtime
 * record for `Emergency` and `Breakdown` and not for `Scheduled`, and mapping HR's words onto
 * theirs would decide something about their data that is not HR's to decide.
 */
export interface SendAssetForMaintenanceRequest {
  description: string;
  /** The NUMBER — see `ASSET_MAINTENANCE_TYPES`. Defaults to Corrective server-side. */
  type?: number;
  /** `'Scheduled'` | `'Emergency'` | `'Breakdown'` — the Maintenance module's own vocabulary. */
  admissionType?: string;
  observedProblems?: string | null;
  admissionLocation?: string | null;
  estimatedCompletionDate?: string | null;
  cost?: number | null;
}

// ── Assignments: the write side the register screens own ───────────────────────

export interface CreateAssetAssignmentRequest {
  assetId: string;
  employeeId: string;
  assignmentDate: string;
  expectedReturnDate?: string | null;
  /** The NUMBER — see `ASSIGNMENT_TYPES`. */
  type: number;
  /** The NUMBER — see `ASSIGNMENT_PURPOSES`. */
  purpose: number;
  /** The NUMBER — see `ASSET_CONDITIONS`. */
  conditionAtAssignment: number;
  conditionNotes?: string | null;
  isPrimaryUser?: boolean;
  responsibleForLoss?: boolean;
  responsibleForDamage?: boolean;
  termsAndConditions?: string | null;
  assignmentNotes?: string | null;
  requisitionId?: string | null;
}

/**
 * Taking an asset back.
 *
 * ⚠ `returnedToId` is REQUIRED and is an **employee** id — the person receiving it. Omitting it
 * answers 404 naming `Guid.Empty`, which reads as a broken route rather than a missing field. The
 * slice-12 probe walked into exactly that.
 */
export interface ReturnAssetRequest {
  returnDate: string;
  returnedToId: string;
  /** The NUMBER — see `ASSET_CONDITIONS`. */
  conditionAtReturn: number;
  returnedInGoodCondition: boolean;
  returnNotes?: string | null;
  damageReported?: boolean;
  damageDescription?: string | null;
  /** Establishes that a charge is PERMISSIBLE and seeds its default — it is not the charge. */
  employeeLiable?: boolean;
  repairCost?: number | null;
  replacementCost?: number | null;
}

/** Recording that an asset never came back — slice 7's D-z. */
export interface ReportAssetIncidentRequest {
  /** 4 = Lost, 5 = Damaged, matching `AssignmentStatus`. */
  outcome: number;
  description: string;
  incidentDate?: string | null;
}

/**
 * AST-9/AST-10 — what the holder is charged for holding this.
 *
 * ⚠ `rentalAmount: 0` is a stated arrangement (provided free, usually taxable). Clearing the terms
 * is a DELETE, not a zero.
 */
export interface SetAssetRentalTermsRequest {
  rentalAmount: number;
  rentalCurrencyCode: string;
  /** The NUMBER — see `RENTAL_FREQUENCIES`. */
  rentalFrequency: number;
  rentalStartDate: string;
  rentalEndDate?: string | null;
  isBenefitInKind: boolean;
  /** A flag cannot carry the value of a subsidy, so the amount sits beside it — slice 8. */
  benefitInKindValue?: number | null;
  rentalNotes?: string | null;
}

// ── Transfers ──────────────────────────────────────────────────────────────────

export interface AssetTransferSummary {
  id: string;
  transferNumber: string;
  assetName: string;
  transferDate: string;
  type: HRAssetTransferType;
  typeName: string;
  /** Already resolved server-side to whichever side of the transfer the type implies. */
  fromName: string | null;
  toName: string | null;
  status: HRAssetTransferStatus;
  statusName: string;
}

export interface AssetTransfer {
  id: string;
  tenantId: string;
  transferNumber: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  transferDate: string;
  type: HRAssetTransferType;
  typeName: string;
  /** Where it is coming from — taken from the register, never supplied by the caller. */
  fromEmployeeId: string | null;
  fromEmployeeName: string | null;
  fromLocationId: string | null;
  fromLocationName: string | null;
  fromUnitId: string | null;
  fromUnitName: string | null;
  toEmployeeId: string | null;
  toEmployeeName: string | null;
  toLocationId: string | null;
  toLocationName: string | null;
  toUnitId: string | null;
  toUnitName: string | null;
  transferReason: string | null;
  initiatedById: string | null;
  initiatedByName: string | null;
  status: HRAssetTransferStatus;
  statusName: string;
  approvedById: string | null;
  approvedByName: string | null;
  approvalDate: string | null;
  completionDate: string | null;
  notes: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** ⚠ Exactly one destination, and it must match `type` — the API refuses the mismatch in words. */
export interface CreateAssetTransferRequest {
  assetId: string;
  transferDate: string;
  /** The NUMBER — see `ASSET_TRANSFER_TYPES`. `3` (department) is refused. */
  type: number;
  toEmployeeId?: string | null;
  toLocationId?: string | null;
  toUnitId?: string | null;
  transferReason?: string | null;
  notes?: string | null;
}

// ── The register report — the one screen with a print layout ───────────────────

/** One row of a breakdown. `id` is null for the "(no unit set)" / "(no location set)" group. */
export interface AssetRegisterGroup {
  id: string | null;
  name: string;
  assetCount: number;
  totalPurchaseCost: number;
  assignedCount: number;
}

/**
 * The register counted and totalled on one page — slice 11.
 *
 * ⚠ **The population total is `assetCount`, not `totalAssets`.** That was the second guess this
 * area's UI probe caught, and it would have rendered `undefined` over a 200 response.
 *
 * ⚠ **The watchlist counts here are DISJOINT** — `maintenanceDueSoonCount` excludes the overdue
 * rows — while the watchlist *reads* are inclusive. The identity that ties them together is
 * `expiringSoon + expired === what insurance/expiring returns`; if a screen ever needs both, use
 * that rather than assuming either.
 *
 * ⚠ The filters propagate: a report about one location carries THAT location's overdue count, not
 * the organisation's. The echoed `unitName` / `locationName` / `assetTypeName` are what the printed
 * header must say, so the page cannot be mistaken for the whole estate.
 *
 * ⚠ `totalPurchaseCost` and `totalInsuredValue` are summed WITHOUT a currency — the register holds
 * no currency code for either. Safe only because nothing in it can express a second currency;
 * registered for the HR↔Finance sweep rather than solved here.
 */
export interface AssetRegisterReport {
  generatedAt: string;
  asOf: string;

  assetTypeId: string | null;
  assetTypeName: string | null;
  unitId: string | null;
  unitName: string | null;
  locationId: string | null;
  locationName: string | null;
  status: CompanyAssetStatus | null;
  statusName: string | null;

  assetCount: number;
  assignedCount: number;
  unassignedCount: number;
  assignableCount: number;
  rentableCount: number;
  insuredCount: number;
  uninsuredCount: number;
  fromFixedAssetsCount: number;
  hrCreatedCount: number;
  linkedToMaintenanceCount: number;

  totalPurchaseCost: number;
  totalInsuredValue: number;
  /** The honest denominator for the total above. */
  assetsWithoutPurchaseCost: number;

  /** Every breakdown sums to `assetCount` — unplaced assets are GROUPED and named, never dropped. */
  byStatus: AssetRegisterGroup[];
  byAssetType: AssetRegisterGroup[];
  byCondition: AssetRegisterGroup[];
  byUnit: AssetRegisterGroup[];
  byLocation: AssetRegisterGroup[];

  maintenanceDueSoonCount: number;
  maintenanceOverdueCount: number;
  maintenanceUnscheduledCount: number;
  insuranceExpiringSoonCount: number;
  insuranceExpiredCount: number;
  insuranceUndatedCount: number;
  returnsOverdueCount: number;
  outstandingSurchargeCount: number;
  outstandingSurchargeAmount: number;
}

export interface AssetRegisterReportFilters {
  assetTypeId?: string;
  unitId?: string;
  locationId?: string;
  /** The NUMBER — see `COMPANY_ASSET_STATUSES`. */
  status?: number;
  asOf?: string;
}

// ── The two payroll projections. HR declares; payroll deducts (decision D2). ────

/** Measured in `slice7-payloads.json` — only APPROVED charges on a recovery plan reach this. */
export interface AssetSurchargePayrollLine {
  surchargeId: string;
  surchargeNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string | null;
  assetName: string;
  currencyCode: string;
  assessedAmount: number;
  amountRecovered: number;
  amountOutstanding: number;
  instalmentCount: number | null;
  instalmentAmount: number | null;
  recoveryStartDate: string | null;
  approvalDate: string | null;
}

/**
 * AST-10 — what payroll may deduct for a rented asset.
 *
 * ⚠ **Not prorated.** Each line carries the FULL periodic rate and the window it applies to; how
 * much of it falls in a given pay run is payroll's calculation with payroll's calendar.
 * `isPartialPeriod` says the window is short — it does not mean the amount was reduced.
 */
export interface AssetRentalPayrollLine {
  assignmentId: string;
  assignmentNumber: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string | null;
  assetId: string;
  assetName: string;
  assetNumber: string;
  assetTypeName: string;
  rentalAmount: number;
  currencyCode: string;
  frequency: RentalDeductionFrequency;
  frequencyName: string;
  /** False for a benefit in kind: declared for tax, not deducted from pay. */
  isDeductible: boolean;
  isBenefitInKind: boolean;
  benefitInKindValue: number | null;
  effectiveFrom: string;
  effectiveTo: string | null;
  periodStart: string;
  periodEnd: string;
  isPartialPeriod: boolean;
}

// ── The reminder engine — AST-1, slices 9 and 11 ───────────────────────────────

export type AssetReminderKind =
  | 'MaintenanceDueSoon'
  | 'MaintenanceOverdue'
  | 'MaintenanceUnscheduled'
  | 'InsuranceExpiringSoon'
  | 'InsuranceExpired'
  | 'InsuranceUndated'
  | 'ReturnDueSoon'
  | 'ReturnOverdue';

/**
 * One rung the sweep would fire.
 *
 * ⚠ The engine has horizons the watchlist reads do not — a 30-day due window and a **90-day backlog
 * floor**, both measured from the run date. A preview at a far-future `asOf` queues nothing, and
 * that is the engine working, not failing.
 *
 * ⚠ The return rungs key on the **assignment**, not the asset, or a replacement laptop would share
 * a dedupe key with the one it replaced.
 */
export interface AssetReminderPreviewItem {
  kind: AssetReminderKind;
  itemType: string;
  /** The asset OR the assignment, depending on the rung. */
  entityId: string;
  assetId: string | null;
  reference: string;
  dueDate: string | null;
  daysRemaining: number | null;
  escalationTier: number;
  dedupeKey: string;
  /** True where a previous sweep already claimed this key. */
  alreadySent: boolean;
}

export interface AssetReminderRun {
  id: string;
  startedAt: string;
  completedAt: string | null;
  trigger: string;
  triggeredByUserId: string | null;
  remindersQueued: number;
}

export interface AssetReminderLogEntry {
  id: string;
  runId: string;
  kind: AssetReminderKind;
  itemType: string;
  entityId: string;
  assetId: string | null;
  reference: string;
  dueDate: string | null;
  daysRemaining: number | null;
  escalationTier: number;
  createdAt: string;
}

export interface AssetReminderRunResult {
  runId: string;
  startedAt: string;
  completedAt: string | null;
  remindersQueued: number;
  skippedAsAlreadySent: number;
}
