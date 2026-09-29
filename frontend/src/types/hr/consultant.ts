/**
 * Consultant clients, engagements, timesheets and invoices — sub-area 3b of Attendance/Time.
 *
 * Backend routes: api/consultant-clients, api/client-engagements, api/consultant-timesheets,
 * api/timesheet-invoices. Same wire-format rules as `attendance.ts` (string enums, camelCase,
 * DateOnly/TimeOnly as `"YYYY-MM-DD"` / `"HH:mm:ss"`).
 *
 * Timesheets are approved through the generic workflow engine (entity type
 * `ConsultantTimesheet`); the separate *client* confirmation is a tokenised email round-trip
 * and is deliberately NOT a workflow step.
 */
import type { AuditFields } from './common';

// ── Enums ────────────────────────────────────────────────────────────────────────

export type BillingCycle =
  | 'Weekly'
  | 'Fortnightly'
  | 'Monthly'
  | 'MilestoneBased'
  | 'OnCompletion';

export type ClientEngagementStatus =
  | 'Draft'
  | 'Active'
  | 'Suspended'
  | 'Completed'
  | 'Terminated';

export type TimesheetStatus =
  | 'Draft'
  | 'Submitted'
  | 'SentToClient'
  | 'ClientConfirmed'
  | 'ClientRejected'
  | 'Billed'
  | 'Void'
  | 'Approved'
  | 'Rejected';

export type TimesheetConfirmationStatus =
  | 'Sent'
  | 'Viewed'
  | 'Confirmed'
  | 'Rejected'
  | 'Expired'
  | 'Resent';

export type TimesheetInvoiceStatus =
  | 'Draft'
  | 'Sent'
  | 'PartiallyPaid'
  | 'Paid'
  | 'Voided'
  | 'Overdue';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

export const BILLING_CYCLE_OPTIONS = opts<BillingCycle>([
  ['Weekly', 'Weekly'],
  ['Fortnightly', 'Fortnightly'],
  ['Monthly', 'Monthly'],
  ['MilestoneBased', 'Milestone based'],
  ['OnCompletion', 'On completion'],
]);

export const ENGAGEMENT_STATUS_OPTIONS = opts<ClientEngagementStatus>([
  ['Draft', 'Draft'],
  ['Active', 'Active'],
  ['Suspended', 'Suspended'],
  ['Completed', 'Completed'],
  ['Terminated', 'Terminated'],
]);

export const TIMESHEET_STATUS_OPTIONS = opts<TimesheetStatus>([
  ['Draft', 'Draft'],
  ['Submitted', 'Submitted'],
  ['Approved', 'Approved'],
  ['Rejected', 'Rejected'],
  ['SentToClient', 'Sent to client'],
  ['ClientConfirmed', 'Client confirmed'],
  ['ClientRejected', 'Client rejected'],
  ['Billed', 'Billed'],
  ['Void', 'Void'],
]);

export const INVOICE_STATUS_OPTIONS = opts<TimesheetInvoiceStatus>([
  ['Draft', 'Draft'],
  ['Sent', 'Sent'],
  ['PartiallyPaid', 'Partially paid'],
  ['Paid', 'Paid'],
  ['Overdue', 'Overdue'],
  ['Voided', 'Voided'],
]);

// ── Clients ──────────────────────────────────────────────────────────────────────

export interface ConsultantClientSummary {
  id: string;
  clientName: string;
  clientCode: string;
  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  city?: string | null;
  countryName?: string | null;
  currency: string;
  isActive: boolean;
  activeEngagementCount: number;
}

export interface ConsultantClient extends AuditFields {
  clientName: string;
  clientCode: string;
  industry?: string | null;
  description?: string | null;
  /** The Finance (Sales) customer this client is billed as; null = invoices stay HR-side (lane 8, slice 6). */
  financeCustomerId?: string | null;

  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactPhone?: string | null;

  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  region?: string | null;
  postalCode?: string | null;
  countryId?: string | null;
  countryName?: string | null;

  billingContactName?: string | null;
  billingContactEmail?: string | null;
  billingContactPhone?: string | null;
  taxIdentificationNumber?: string | null;
  currency: string;
  defaultPaymentTermsDays?: number | null;

  isActive: boolean;
  notes?: string | null;

  activeEngagementCount: number;
  timesheetCount: number;
  outstandingInvoiceCount: number;

  engagements: ClientEngagementSummary[];
}

export interface CreateConsultantClient {
  clientName: string;
  clientCode: string;
  industry?: string | null;
  description?: string | null;
  /** The Finance (Sales) customer this client is billed as; null = invoices stay HR-side (lane 8, slice 6). */
  financeCustomerId?: string | null;
  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactPhone?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  region?: string | null;
  postalCode?: string | null;
  countryId?: string | null;
  billingContactName?: string | null;
  billingContactEmail?: string | null;
  billingContactPhone?: string | null;
  taxIdentificationNumber?: string | null;
  currency: string;
  defaultPaymentTermsDays?: number | null;
  isActive: boolean;
  notes?: string | null;
}

/** The client code is immutable — invoices and portal accounts key off it. */
export interface UpdateConsultantClient extends Omit<CreateConsultantClient, 'clientCode'> {
  id: string;
}

// ── Portal accounts ──────────────────────────────────────────────────────────────

export interface ConsultantClientPortalAccountSummary {
  id: string;
  email: string;
  contactName?: string | null;
  contactRole?: string | null;
  isEmailVerified: boolean;
  isActive: boolean;
  isSetupPending: boolean;
  lastLoginAt?: string | null;
  createdAt: string;
}

export interface InvitePortalAccount {
  email: string;
  contactName?: string | null;
  contactRole?: string | null;
}

// ── Engagements ──────────────────────────────────────────────────────────────────

export interface ClientEngagementSummary {
  id: string;
  engagementCode: string;
  title: string;
  clientId: string;
  clientName: string;
  consultantId: string;
  consultantName: string;
  startDate: string;
  endDate?: string | null;
  hourlyRate: number;
  currency: string;
  billingCycle: BillingCycle;
  status: ClientEngagementStatus;
}

export interface ClientEngagement extends AuditFields {
  engagementCode: string;
  title: string;
  description?: string | null;

  clientId: string;
  clientName: string;
  clientCode: string;

  consultantId: string;
  consultantName: string;
  consultantNumber: string;

  startDate: string;
  endDate?: string | null;

  hourlyRate: number;
  currency: string;
  billingCycle: BillingCycle;
  maxHoursPerWeek?: number | null;
  contractValue?: number | null;
  purchaseOrderNumber?: string | null;

  status: ClientEngagementStatus;
  notes?: string | null;

  timesheetCount: number;
  timesheets: ConsultantTimesheetSummary[];
}

export interface CreateClientEngagement {
  engagementCode: string;
  title: string;
  description?: string | null;
  clientId: string;
  consultantId: string;
  startDate: string;
  endDate?: string | null;
  hourlyRate: number;
  currency: string;
  billingCycle: BillingCycle;
  maxHoursPerWeek?: number | null;
  contractValue?: number | null;
  purchaseOrderNumber?: string | null;
  status: ClientEngagementStatus;
  notes?: string | null;
}

/** Code, client, consultant and start date are fixed once the engagement exists. */
export interface UpdateClientEngagement
  extends Omit<
    CreateClientEngagement,
    'engagementCode' | 'clientId' | 'consultantId' | 'startDate'
  > {
  id: string;
}

// ── Timesheets ───────────────────────────────────────────────────────────────────

export interface ConsultantTimesheetSummary {
  id: string;
  timesheetNumber: string;
  consultantId: string;
  consultantName: string;
  clientId: string;
  clientName: string;
  periodStartDate: string;
  periodEndDate: string;
  totalHours: number;
  status: TimesheetStatus;
  submittedDate?: string | null;
}

export interface ConsultantTimesheetEntry extends AuditFields {
  timesheetId: string;
  timesheetNumber: string;
  /** DateOnly */
  workDate: string;
  /** TimeOnly */
  startTime: string;
  endTime: string;
  breakMinutes: number;
  /** Server-computed from start/end minus breaks; not sent on write. */
  totalHours: number;
  activitySummary: string;
  location?: string | null;
  notes?: string | null;
}

export interface ClientTimesheetConfirmation extends AuditFields {
  timesheetId: string;
  timesheetNumber: string;
  clientContactEmail: string;
  clientContactName?: string | null;
  tokenExpiryDate: string;
  sentDate: string;
  sentById: string;
  sentByName: string;
  status: TimesheetConfirmationStatus;
  viewedDate?: string | null;
  confirmedDate?: string | null;
  rejectedDate?: string | null;
  clientNotes?: string | null;
  resendCount: number;
}

export interface ConsultantTimesheet extends AuditFields {
  timesheetNumber: string;
  consultantId: string;
  consultantName: string;
  consultantNumber: string;
  clientId: string;
  clientName: string;
  clientCode: string;
  engagementId?: string | null;
  engagementCode?: string | null;
  engagementTitle?: string | null;
  periodStartDate: string;
  periodEndDate: string;
  totalHours: number;
  status: TimesheetStatus;
  submittedDate?: string | null;
  notes?: string | null;
  entries: ConsultantTimesheetEntry[];
  confirmations: ClientTimesheetConfirmation[];
}

export interface CreateConsultantTimesheet {
  consultantId: string;
  clientId: string;
  engagementId?: string | null;
  periodStartDate: string;
  periodEndDate: string;
  notes?: string | null;
}

/** Consultant and client are fixed; only the engagement and period can be corrected. */
export interface UpdateConsultantTimesheet {
  id: string;
  engagementId?: string | null;
  periodStartDate: string;
  periodEndDate: string;
  notes?: string | null;
}

export interface CreateTimesheetEntry {
  timesheetId: string;
  workDate: string;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  activitySummary: string;
  location?: string | null;
  notes?: string | null;
}

export interface UpdateTimesheetEntry extends Omit<CreateTimesheetEntry, 'timesheetId'> {
  id: string;
}

export interface SendTimesheetConfirmationRequest {
  clientContactEmail: string;
  clientContactName?: string | null;
  tokenExpiryDate?: string | null;
}

export interface SendTimesheetConfirmationResult {
  confirmation: ClientTimesheetConfirmation;
  confirmationToken: string;
  emailSent: boolean;
}

// ── Invoices ─────────────────────────────────────────────────────────────────────

export interface TimesheetInvoiceSummary {
  id: string;
  invoiceNumber: string;
  clientId: string;
  clientName: string;
  consultantId: string;
  consultantName: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  totalHours: number;
  totalAmount: number;
  currency: string;
  status: TimesheetInvoiceStatus;
  dueDate?: string | null;
  paidDate?: string | null;
}

export interface TimesheetInvoiceLink extends AuditFields {
  invoiceId: string;
  invoiceNumber: string;
  timesheetId: string;
  timesheetNumber: string;
  timesheetPeriodStart: string;
  timesheetPeriodEnd: string;
  hours: number;
  amount: number;
}

export interface TimesheetInvoice extends AuditFields {
  invoiceNumber: string;
  clientId: string;
  clientName: string;
  clientCode: string;
  consultantId: string;
  consultantName: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;

  totalHours: number;
  hourlyRate: number;
  subTotal: number;
  taxPercentage: number;
  taxAmount: number;
  totalAmount: number;
  currency: string;

  status: TimesheetInvoiceStatus;
  issuedDate?: string | null;
  dueDate?: string | null;
  paidDate?: string | null;
  notes?: string | null;

  linkedTimesheets: TimesheetInvoiceLink[];
}

export interface GenerateTimesheetInvoice {
  clientId: string;
  consultantId: string;
  billingPeriodStart: string;
  billingPeriodEnd: string;
  hourlyRate: number;
  taxPercentage: number;
  currency: string;
  dueDate?: string | null;
  notes?: string | null;
  /** Confirmed timesheets to bill; the server recomputes hours and totals from them. */
  timesheetIds: string[];
}

/** The update DTO reshapes the money fields — it takes a flat total, not rate + tax. */
export interface UpdateTimesheetInvoice {
  id: string;
  invoiceDate: string;
  dueDate: string;
  totalAmount: number;
  notes?: string | null;
}

export interface RecordInvoicePayment {
  invoiceId: string;
  paidDate: string;
  notes?: string | null;
}

// ── Client portal (the consultant-client contact's own surface) ──────────────────
// api/client-portal — main-scheme ConsultantClient role since 2026-08-31 (the bespoke
// PortalBearer portal retired). Shapes read from the C# DTOs, not guessed from names.

export interface ClientTimesheetConfirmationPublic {
  timesheetNumber: string;
  consultantName: string;
  clientName: string;
  /** DateOnly */
  periodStartDate: string;
  periodEndDate: string;
  totalHours: number;
  status: TimesheetConfirmationStatus;
  isExpired: boolean;
  canRespond: boolean;
  tokenExpiryDate: string;
  entries: ConsultantTimesheetEntry[];
}

export interface ClientPortalClientSection {
  consultantClientId: string;
  clientName: string;
  clientCode: string;
  contactRole?: string | null;
  pendingConfirmationCount: number;
  pendingTimesheets: ConsultantTimesheetSummary[];
}

/** A contact invited by several clients holds one section per client. */
export interface ClientPortalDashboard {
  email: string;
  contactName?: string | null;
  pendingConfirmationCount: number;
  clients: ClientPortalClientSection[];
}

/** A Finance customer as HR's read door (`api/hr/customers`) offers it to the client picker. */
export interface HrCustomerOption {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  currencyCode: string;
}
