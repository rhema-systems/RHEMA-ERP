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

/** The full assignment record — what one row opens into. */
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
