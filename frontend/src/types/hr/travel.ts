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

/**
 * ⚠ This union had four members and `StaffTravelType` in C# has ten. The six missing ones —
 * every value from `OverseasAssignment` down — are states the API returns and TypeScript said
 * could not exist, so a `switch` over this type looked exhaustive and was not. Written from four
 * examples rather than from the enum; corrected 2026-08-30 by reading it.
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
  | 'GovernmentDuty';

export type StaffTravelPurpose =
  | 'BusinessDevelopment'
  | 'ClientMeeting'
  | 'Conference'
  | 'Training'
  | 'SiteVisit'
  | 'Audit'
  | 'Negotiation'
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

export type TravelInitiatorRole = 'Employee' | 'Manager' | 'HrAdmin' | 'TravelDesk';

export type TravelRiskLevel = 'Low' | 'Medium' | 'High' | 'Extreme';

export type TravelRequestCommentType = 'General' | 'Query' | 'Instruction' | 'Justification';

export type TravelAttachmentType =
  | 'Invitation'
  | 'Agenda'
  | 'Quotation'
  | 'Approval'
  | 'VisaSupport'
  | 'Other';

export type GroupTravelStatus = 'Planned' | 'Confirmed' | 'InProgress' | 'Completed' | 'Cancelled';

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
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  originCountryId: string;
  originCountryName: string;
  estimatedDurationDays: number;
  policyId?: string | null;
  requiresVisa: boolean;
  requiresHealthClearance: boolean;
  groupTravelId?: string | null;
  parentRequestId?: string | null;
  amendmentReason?: string | null;
  cancellationReason?: string | null;
  cancelledById?: string | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  completedAt?: string | null;
  comments?: StaffTravelRequestComment[];
  attachments?: StaffTravelRequestAttachment[];
}

export interface StaffGroupTravelSummary extends AuditFields {
  groupName: string;
  leadEmployeeId: string;
  leadEmployeeName: string;
  eventName?: string | null;
  destinationCity: string;
  travelStartDate: string;
  travelEndDate: string;
  status: GroupTravelStatus;
  participantCount: number;
  maxParticipants?: number | null;
}

export interface StaffGroupTravel extends StaffGroupTravelSummary {
  destinationCountryId: string;
  destinationCountryName: string;
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
 * travels in one. They are not converted to a base currency: travel does not invent a rate, and
 * Finance's conversion is currently inverted, so a converted headline would be confidently wrong
 * rather than visibly incomplete. Show one figure for one currency and this breakdown otherwise.
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
 * to `GET /api/finance/currencies`; travel deliberately keeps no currency list of its own.
 *
 * ⚠ There is no `initiatedById`. Who raised the request is the caller's employee id, stamped
 * server-side — the desk raises travel for other people, so it is neither the traveller nor
 * anything a form can be trusted to say. Only the *role* it was raised under is an input.
 */
export interface CreateStaffTravelRequest {
  employeeId: string;
  initiatedByRole: TravelInitiatorRole;
  travelType: StaffTravelType;
  travelPurpose: StaffTravelPurpose;
  purposeDescription?: string;
  organizationUnitId?: string | null;
  priority: StaffTravelPriority;
  destinationCountryId: string;
  destinationCity: string;
  originCountryId: string;
  originCity: string;
  travelStartDate: string;
  travelEndDate: string;
  estimatedTotalCost: number;
  currencyCode: string;
  policyId?: string | null;
  isInternational: boolean;
  requiresVisa: boolean;
  requiresHealthClearance: boolean;
  riskLevel: TravelRiskLevel;
  groupTravelId?: string | null;
  parentRequestId?: string | null;
  amendmentReason?: string | null;
}

export type UpdateStaffTravelRequest = Omit<
  CreateStaffTravelRequest,
  'employeeId' | 'initiatedByRole'
> & { id: string };

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
