/**
 * The staff-travel request enums as screens show them: one label map per enum, labels taken from the
 * C# `[Description]` text in `src/ErpSystem.Core/Enums/HREnums.cs`.
 *
 * ⚠ Each map is a `Record` over its TypeScript union, so the compiler refuses a missing or an extra
 * member, and every option list is generated from a map — a dropdown can no longer offer a value the
 * union does not have, or hide one it does. The unions themselves are held to the C# enums by
 * `travel-enums.test.ts`. Before this file the option lists were hand-typed beside the unions and
 * drifted from both (travel final closure, lane 0 — findings A7, A8, A9).
 */
import type {
  StaffTravelPriority,
  StaffTravelPurpose,
  StaffTravelRequestStatus,
  StaffTravelType,
  TravelAttachmentType,
  TravelInitiatorRole,
  TravelRequestCommentType,
  TravelRiskLevel,
} from '@/types/hr/travel';
import type { FlightCabinClass } from '@/types/hr/travel-bookings';

export const TRAVEL_TYPE_LABELS: Record<StaffTravelType, string> = {
  Domestic: 'Domestic',
  International: 'International',
  CrossBorder: 'Cross-Border',
  Regional: 'Regional',
  OverseasAssignment: 'Overseas Assignment',
  FieldVisit: 'Field Visit',
  Training: 'Training',
  Conference: 'Conference',
  ClientVisit: 'Client Visit',
  GovernmentDuty: 'Government Duty',
  Emergency: 'Emergency',
};

export const TRAVEL_PURPOSE_LABELS: Record<StaffTravelPurpose, string> = {
  BusinessDevelopment: 'Business Development',
  ClientMeeting: 'Client Meeting',
  Conference: 'Conference',
  Training: 'Training',
  Audit: 'Audit',
  Inspection: 'Inspection',
  ProjectWork: 'Project Work',
  SiteVisit: 'Site Visit',
  GovernmentEngagement: 'Government Engagement',
  PersonalCombined: 'Personal Combined',
  Emergency: 'Emergency',
  Other: 'Other',
};

export const TRAVEL_PRIORITY_LABELS: Record<StaffTravelPriority, string> = {
  Routine: 'Routine',
  Urgent: 'Urgent',
  Emergency: 'Emergency',
};

export const TRAVEL_INITIATOR_ROLE_LABELS: Record<TravelInitiatorRole, string> = {
  Employee: 'Employee',
  Manager: 'Manager',
  HrAdmin: 'HR Admin',
  TravelDesk: 'Travel Desk',
  System: 'System',
};

/**
 * `System` marks a request the system raised, so the desk's *Raised as* picker leaves it out — but
 * a form must still ACCEPT it, or a system-raised request could never be edited.
 */
export const TRAVEL_INITIATOR_ROLES_A_PERSON_CHOOSES: readonly TravelInitiatorRole[] = [
  'Employee',
  'Manager',
  'HrAdmin',
  'TravelDesk',
];

export const TRAVEL_RISK_LEVEL_LABELS: Record<TravelRiskLevel, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
  Prohibited: 'Prohibited',
};

/** The risk levels a screen flags with a warning icon. */
export const ELEVATED_TRAVEL_RISK_LEVELS: readonly TravelRiskLevel[] = ['High', 'Critical', 'Prohibited'];

export const TRAVEL_COMMENT_TYPE_LABELS: Record<TravelRequestCommentType, string> = {
  Comment: 'Comment',
  InternalNote: 'Internal Note',
  RejectionReason: 'Rejection Reason',
  Query: 'Query',
  Response: 'Response',
  SystemNote: 'System Note',
};

export const TRAVEL_ATTACHMENT_TYPE_LABELS: Record<TravelAttachmentType, string> = {
  InvitationLetter: 'Invitation Letter',
  ConferenceBrochure: 'Conference Brochure',
  Receipt: 'Receipt',
  VisaDocument: 'Visa Document',
  InsuranceCertificate: 'Insurance Certificate',
  MedicalCertificate: 'Medical Certificate',
  Other: 'Other',
};

export const TRAVEL_REQUEST_STATUS_LABELS: Record<StaffTravelRequestStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
  ReturnedForRevision: 'Returned For Revision',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Closed: 'Closed',
};

export const FLIGHT_CABIN_CLASS_LABELS: Record<FlightCabinClass, string> = {
  Economy: 'Economy',
  PremiumEconomy: 'Premium Economy',
  Business: 'Business',
  First: 'First',
};

/** Every member of a label map, in declaration order — the non-empty tuple `z.enum` needs. */
export function enumValues<T extends string>(labels: Record<T, string>): [T, ...T[]] {
  return Object.keys(labels) as [T, ...T[]];
}

/** Select options generated from a label map, optionally leaving members out of the choice. */
export function enumOptions<T extends string>(
  labels: Record<T, string>,
  only?: readonly T[],
): { value: T; label: string }[] {
  return enumValues(labels)
    .filter((value) => !only || only.includes(value))
    .map((value) => ({ value, label: labels[value] }));
}

/** The label for a value the API sent; an unknown value shows as itself rather than vanishing. */
export function enumLabel<T extends string>(
  labels: Record<T, string>,
  value: T | string | null | undefined,
): string {
  if (!value) return '—';
  return (labels as Record<string, string>)[value] ?? String(value);
}
