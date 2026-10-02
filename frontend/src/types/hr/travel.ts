/**
 * Staff travel — the request, its lifecycle, comments, attachments and group travel.
 * Backend route: api/staff-travel/requests (+ the per-concern controllers).
 *
 * A travel request is the paperwork for one trip: who is going, where, when, what it should cost,
 * and whether it was approved. Bookings, itineraries, advances, claims and compliance hang off it
 * and arrive in later UI slices.
 *
 * ⚠ Approval is NOT a field on this record. It moved onto the generic workflow engine, so a
 * screen must read the live approval state from the workflow record and never infer it from
 * `status` alone — `Submitted` means "out for approval", and which step it sits on is the
 * workflow instance's business.
 */

import type { AuditFields } from './common';
import type { FlightCabinClass } from './travel-bookings';

/**
 * ⚠ Every union in this file is written from its C# enum in `src/ErpSystem.Core/Enums/HREnums.cs`,
 * member for member, and `travel-enums.test.ts` fails the frontend suite if one drifts again.
 *
 * Four had drifted (travel final closure, lane 0 — findings A7, A8, A9). This one had four members,
 * then ten, while C# has eleven (`Emergency` was missing). The purpose union offered `Negotiation`,
 * which the API refuses, and hid five real purposes. The risk union offered `Extreme` (refused) and
 * hid `Critical` and `Prohibited`. The comment and attachment unions shared one member with C#
 * between them, so the desk's every comment and five of six upload types failed.
 */
export type StaffTravelType =
  | 'Domestic'
  | 'International'
  | 'CrossBorder'
  | 'Regional'
  | 'OverseasAssignment'
  | 'FieldVisit'
  | 'Training'
  | 'Conference'
  | 'ClientVisit'
  | 'GovernmentDuty'
  | 'Emergency';

export type StaffTravelPurpose =
  | 'BusinessDevelopment'
  | 'ClientMeeting'
  | 'Conference'
  | 'Training'
  | 'Audit'
  | 'Inspection'
  | 'ProjectWork'
  | 'SiteVisit'
  | 'GovernmentEngagement'
  | 'PersonalCombined'
  | 'Emergency'
  | 'Other';

export type StaffTravelPriority = 'Routine' | 'Urgent' | 'Emergency';

/**
 * `Submitted` covers the whole approval phase; the workflow record knows the step.
 * `ReturnedForRevision` is the route back for "fix this and resend" — a `Rejected` request is
 * terminal, because travel dates move and reviving a refused trip is usually wrong.
 */
export type StaffTravelRequestStatus =
  | 'Draft'
  | 'Submitted'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'ReturnedForRevision'
  | 'InProgress'
  | 'Completed'
  | 'Closed';

export type TravelInitiatorRole = 'Employee' | 'Manager' | 'HrAdmin' | 'TravelDesk' | 'System';

/** One definition for the whole area — `travel-compliance.ts` re-exports this one. */
export type TravelRiskLevel = 'Low' | 'Medium' | 'High' | 'Critical' | 'Prohibited';

export type TravelRequestCommentType =
  | 'Comment'
  | 'InternalNote'
  | 'RejectionReason'
  | 'Query'
  | 'Response'
  | 'SystemNote';

export type TravelAttachmentType =
  | 'InvitationLetter'
  | 'ConferenceBrochure'
  | 'Receipt'
  | 'VisaDocument'
  | 'InsuranceCertificate'
  | 'MedicalCertificate'
  | 'Other';

/**
 * ⚠ Written from the enum, not from guesses. The union used to read
 * `Planned | Confirmed | InProgress | Completed | Cancelled`: `Planned` and `Confirmed` are not
 * members at all, and `Open` and `Closed` — the two states a group spends its life in while people
 * are being added — were missing. A status control built on the old union would have offered two
 * values the API rejects and hidden the two that matter.
 */
export type GroupTravelStatus =
  | 'Planning'
  | 'Open'
  | 'Closed'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';

// ── Read models ──────────────────────────────────────────────────────────────

export interface StaffTravelRequestSummary extends AuditFields {
  requestNumber: string;
  employeeId: string;
  employeeName: string;
  travelType: StaffTravelType;
  travelPurpose: StaffTravelPurpose;
  status: StaffTravelRequestStatus;
  priority: StaffTravelPriority;
  destinationCountryId: string;
  destinationCountryName: string;
  destinationCity: string;
  originCity: string;
  travelStartDate: string;
  travelEndDate: string;
  estimatedTotalCost: number;
  approvedBudget?: number | null;
  currencyCode: string;
  isInternational: boolean;
  riskLevel: TravelRiskLevel;
  submittedAt?: string | null;
}

export interface StaffTravelRequestComment extends AuditFields {
  staffTravelRequestId: string;
  authorId: string;
  /** Resolved server-side; blank means the author could not be loaded, not that there is none. */
  authorName: string;
  commentType: TravelRequestCommentType;
  body: string;
  isVisibleToTraveller: boolean;
  parentCommentId?: string | null;
  replies?: StaffTravelRequestComment[];
}

/**
 * ⚠ `fileUrl` is EMPTY for anything uploaded through the controlled gate. Use `documentRecordId`
 * to tell a stored document from a legacy row, and download through
 * `GET .../attachments/{id}/download` rather than dereferencing a path.
 */
export interface StaffTravelRequestAttachment extends AuditFields {
  staffTravelRequestId: string;
  fileName: string;
  fileUrl: string;
  fileSizeBytes: number;
  mimeType: string;
  attachmentType: TravelAttachmentType;
  attachmentTypeName: string;
  fileUploadRecordId?: string | null;
  documentRecordId?: string | null;
  documentVersionId?: string | null;
  uploadedById: string;
  uploadedByName: string;
  uploadedAt: string;
}

export interface StaffTravelRequest extends StaffTravelRequestSummary {
  tenantId: string;
  initiatedById: string;
  initiatedByName: string;
  initiatedByRole: TravelInitiatorRole;
  purposeDescription?: string | null;
  /** The traveller's own unit, set by the server from their employee record (lane 1, O-5). */
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  originCountryId: string;
  originCountryName: string;
  estimatedDurationDays: number;
  /** The policy the trip was checked against when it was submitted; null before, or when none covers it. */
  policyId?: string | null;
  policyName?: string | null;
  requiresVisa: boolean;
  requiresHealthClearance: boolean;
  groupTravelId?: string | null;
  parentRequestId?: string | null;
  amendmentReason?: string | null;
  /** Lane 6 (D-33): a driver's own request names the trip whose company vehicle they drive. */
  driverForRequestId?: string | null;
  driverForRequestNumber?: string | null;
  cancellationReason?: string | null;
  cancelledById?: string | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  completedAt?: string | null;
  /**
   * Who decided and what happened after (travel final closure, lane 1). The approval stamps stay when
   * a change is requested — they record the approval being changed until the next one replaces them.
   */
  approvedById?: string | null;
  approvedByName?: string | null;
  returnedAt?: string | null;
  returnedById?: string | null;
  returnedByName?: string | null;
  returnReason?: string | null;
  changeRequestedAt?: string | null;
  changeRequestedById?: string | null;
  changeRequestedByName?: string | null;
  changeReason?: string | null;
  closedAt?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
  comments?: StaffTravelRequestComment[];
  attachments?: StaffTravelRequestAttachment[];
}

/**
 * A group trip as the list returns it.
 *
 * ⚠ Two fields here were fiction. `leadEmployeeId` is on the FULL record and not the summary, and
 * the count is `currentParticipantCount` — `participantCount` matched nothing the API sends, so
 * every list row rendered `undefined` participants while type-checking clean. An edit form must
 * fetch by id for the same reason: the summary carries neither owner id nor destination country.
 */
export interface StaffGroupTravelSummary extends AuditFields {
  groupName: string;
  leadEmployeeName: string;
  eventName?: string | null;
  destinationCity: string;
  travelStartDate: string;
  travelEndDate: string;
  status: GroupTravelStatus;
  statusName: string;
  currentParticipantCount: number;
  maxParticipants?: number | null;
}

export interface StaffGroupTravel extends StaffGroupTravelSummary {
  leadEmployeeId: string;
  destinationCountryId: string;
  destinationCountryName?: string | null;
  /** The participants' own travel requests — one per person, raised by adding them. */
  requests?: StaffTravelRequestSummary[];
}

/** One month of the trailing six-month trend, by creation date. */
export interface StaffTravelMonthlyCount {
  year: number;
  month: number;
  label: string;
  count: number;
}

/**
 * ⚠ Travel cost for ONE currency.
 *
 * `totalEstimatedCost` and `totalApprovedBudget` on the dashboard add every request's figure
 * together regardless of the currency it was costed in, so they are only meaningful when a tenant
 * travels in one. They are not converted to a base currency: travel does not invent a rate, and a
 * headline converted at one day's rate would hide that the trips were costed in different
 * currencies. Show one figure for one currency and this breakdown otherwise. (This comment also said
 * Finance's conversion was inverted; Finance fixed that on 2026-09-10.)
 */
export interface StaffTravelCurrencyTotal {
  currencyCode: string;
  estimatedTotal: number;
  approvedBudget: number;
  requestCount: number;
}

export interface StaffTravelDashboard {
  totalRequests: number;
  draftCount: number;
  pendingApprovalCount: number;
  approvedCount: number;
  inProgressCount: number;
  completedCount: number;
  rejectedCount: number;
  cancelledCount: number;
  internationalCount: number;
  domesticCount: number;
  highRiskCount: number;
  /** The COUNT of upcoming trips; `upcomingTrips` below is the list. */
  upcomingTripCount: number;
  /** ⚠ Sums across currencies — see StaffTravelCurrencyTotal. */
  totalEstimatedCost: number;
  totalApprovedBudget: number;
  byStatus: { status: StaffTravelRequestStatus; statusName: string; count: number }[];
  byTravelType: { travelType: StaffTravelType; travelTypeName: string; count: number }[];
  monthlyTrend: StaffTravelMonthlyCount[];
  costByCurrency: StaffTravelCurrencyTotal[];
  pendingApprovals: StaffTravelRequestSummary[];
  upcomingTrips: StaffTravelRequestSummary[];
  recentRequests: StaffTravelRequestSummary[];
}


// ── Write models ─────────────────────────────────────────────────────────────

/**
 * ⚠ `currencyCode` must be one Finance holds — the server refuses anything else. Bind the picker
 * to `GET /api/hr/currencies` (`useCurrencyOptions`), **not** `/api/finance/currencies`: that one
 * needs Finance's own read permission, so it answers 403 to the HR desk and to every traveller, and
 * the picker renders empty (travel final closure, lane 0 — finding O-19). Travel keeps no currency
 * list of its own.
 *
 * ⚠ There is no `initiatedById`. Who raised the request is the caller's employee id, stamped
 * server-side — the desk raises travel for other people, so it is neither the traveller nor
 * anything a form can be trusted to say. Only the *role* it was raised under is an input.
 *
 * ⚠ No organisation unit, `isInternational` or `policyId` either (travel final closure, lane 1 —
 * findings A5, O-5). The unit is the traveller's own, the international flag follows from the two
 * countries, and the policy is the one the trip is checked against at submission — all three set by
 * the server, which ignores them if they are sent.
 */
export interface CreateStaffTravelRequest {
  employeeId: string;
  initiatedByRole: TravelInitiatorRole;
  travelType: StaffTravelType;
  travelPurpose: StaffTravelPurpose;
  purposeDescription?: string;
  priority: StaffTravelPriority;
  destinationCountryId: string;
  destinationCity: string;
  originCountryId: string;
  originCity: string;
  travelStartDate: string;
  travelEndDate: string;
  estimatedTotalCost: number;
  currencyCode: string;
  requiresVisa: boolean;
  requiresHealthClearance: boolean;
  riskLevel: TravelRiskLevel;
  parentRequestId?: string | null;
  amendmentReason?: string | null;
}

/**
 * ⚠ A REPLACE, not a patch: the server writes every field it receives, and an omitted one as its
 * default. Since lane 1 it writes only what the requester may change — the unit, the international
 * flag, the policy, the approved budget and (slice 1c) the group are no longer on it — and only while
 * the request is a Draft or returned for revision. Group membership is the group's own routes'
 * (add, link, remove): an edit that omitted the group link used to take the traveller out of their
 * group (finding A8).
 */
export type UpdateStaffTravelRequest = Omit<
  CreateStaffTravelRequest,
  'employeeId' | 'initiatedByRole'
> & { id: string };

/** How the caller decides a request at the stage it is on (travel final closure, lane 2 — D-7). */
export type TravelDecidesAs = 'LineAuthority' | 'TravelDesk' | 'Approver';

/**
 * What the caller may decide on a request, at which stage and as whom — the screen's answer for whether
 * to offer Approve, Reject and Return, and whether the approve dialog asks for the budget (lane 2).
 */
export interface StaffTravelViewerActions {
  requestId: string;
  canDecide: boolean;
  /** The stage the request is on, as its route names it; null when it is not out for approval. */
  stageName?: string | null;
  isLineStage: boolean;
  /** The last approval stage of the route — its approval approves the trip and sets the budget. */
  isFinalStage: boolean;
  /** The stage the request goes to after this one; null at the last. */
  nextStageName?: string | null;
  decidesAs?: TravelDecidesAs | null;
  /** As a line authority: "supervisor", or "head of …". */
  relation?: string | null;
  /** At the line manager's stage: the line authorities it waits for, named. */
  waitingFor: string[];
  /** Why the caller cannot decide, when the request is out for approval and they cannot. */
  reason?: string | null;
}

/** One row of an approver's travel queue: the request, and the caller's part in it (lane 2). */
export interface StaffTravelApprovalQueueItem {
  request: StaffTravelRequestSummary;
  originCity?: string | null;
  stageName?: string | null;
  isLineStage: boolean;
  isFinalStage: boolean;
  decidesAs: TravelDecidesAs;
  relation?: string | null;
  /** Whole days since it was submitted. */
  daysWaiting: number;
}

/** What an approval did: approved the trip, or sent it on to the next stage (lane 2). */
export interface StaffTravelApproveResult {
  message: string;
  status: StaffTravelRequestStatus;
}

/** What a submission did (lane 1). */
export interface StaffTravelSubmitResult {
  message: string;
  status: StaffTravelRequestStatus;
  statusName: string;
  /** The policy the trip was checked against; null when no approved policy covers it. */
  policyId?: string | null;
  policyName?: string | null;
  /**
   * What did not stop the submission but should be known — approved leave over the same days. A
   * conflict that must stop it is refused with a 422 instead.
   */
  warnings: string[];
}

/**
 * The approved policy a trip would be checked against, before it is saved (finding T-16): the same
 * resolution the server applies at submission — the traveller's own unit, the two countries, the
 * departure date.
 */
export interface StaffTravelPolicyPreview {
  employeeId: string;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  isInternational: boolean;
  hasPolicy: boolean;
  policyId?: string | null;
  policyName?: string | null;
  versionNumber?: number | null;
  /** The currency the policy's money limits are set in. */
  currencyCode?: string | null;
  /** The most one trip may be estimated at; null when the policy sets no such limit. */
  maxSingleTripBudget?: number | null;
  maxFlightClass?: FlightCabinClass | null;
  maxHotelRatePerNight?: number | null;
}

/**
 * ⚠ No `authorId`. Authorship is taken from the caller's token — sending one is ignored, because
 * accepting it let any caller post a comment under a colleague's name.
 */
export interface CreateStaffTravelRequestComment {
  staffTravelRequestId?: string;
  commentType: TravelRequestCommentType;
  body: string;
  isVisibleToTraveller: boolean;
  parentCommentId?: string | null;
}

export interface UpdateStaffTravelRequestComment {
  id: string;
  body: string;
  isVisibleToTraveller: boolean;
}

/** Withdrawing your own request from the self-service surface: the reason and nothing else. */
export interface CancelMyStaffTravelRequest {
  cancellationReason: string;
}

export interface CreateStaffGroupTravel {
  groupName: string;
  leadEmployeeId: string;
  eventName?: string | null;
  destinationCountryId: string;
  destinationCity: string;
  travelStartDate: string;
  travelEndDate: string;
  maxParticipants?: number | null;
}

/**
 * The create's fields plus the id — and no status. Since travel closure lane 1 (slice 1c) a group
 * opens, closes and is cancelled by its own routes; the edit used to carry the status, and a form that
 * left it out moved the trip back to Planning. A new destination or new dates are given to the
 * travellers whose trips are still drafts or returned for revision.
 */
export type UpdateStaffGroupTravel = CreateStaffGroupTravel & {
  id: string;
};
