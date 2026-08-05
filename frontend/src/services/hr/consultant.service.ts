import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  ConsultantClient,
  ConsultantClientSummary,
  CreateConsultantClient,
  UpdateConsultantClient,
  ConsultantClientPortalAccountSummary,
  InvitePortalAccount,
  ClientEngagement,
  ClientEngagementSummary,
  CreateClientEngagement,
  UpdateClientEngagement,
  ClientEngagementStatus,
  BillingCycle,
  ConsultantTimesheet,
  ConsultantTimesheetSummary,
  CreateConsultantTimesheet,
  UpdateConsultantTimesheet,
  ConsultantTimesheetEntry,
  CreateTimesheetEntry,
  UpdateTimesheetEntry,
  TimesheetStatus,
  ClientTimesheetConfirmation,
  SendTimesheetConfirmationRequest,
  SendTimesheetConfirmationResult,
  TimesheetInvoice,
  TimesheetInvoiceSummary,
  GenerateTimesheetInvoice,
  UpdateTimesheetInvoice,
  TimesheetInvoiceStatus,
} from '@/types/hr/consultant';

/**
 * Consultant clients, engagements, timesheets and invoices — sub-area 3b of Attendance/Time.
 *
 * The lifecycle runs: engagement (Active) → timesheet with day entries → submit → workflow
 * approval → send to the client for confirmation → invoice the confirmed timesheets.
 *
 * Timesheet approval goes through the generic workflow engine (entity type
 * `ConsultantTimesheet`), so screens use `useWorkflowRecord` + `WorkflowApprovalActions` and
 * refetch instead of assuming a status. The *client* confirmation is a separate, tokenised
 * email round-trip — deliberately not a workflow step, since the confirmer is external and
 * has no user account in the ERP.
 *
 * As across the rest of this area, the acting user is taken from the caller's token, so no
 * endpoint here accepts an actor id.
 */

/** api/consultant-clients — client organisations, their engagements and portal accounts. */
class ConsultantClientService {
  private readonly baseUrl = '/consultant-clients';

  getAll(): Promise<ConsultantClientSummary[]> {
    return apiService.get<ConsultantClientSummary[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<ConsultantClientSummary>> {
    return apiService.get<PagedResult<ConsultantClientSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<ConsultantClient> {
    return apiService.get<ConsultantClient>(`${this.baseUrl}/${id}`);
  }

  getByCode(clientCode: string): Promise<ConsultantClient | null> {
    return apiService.get<ConsultantClient | null>(`${this.baseUrl}/code/${clientCode}`);
  }

  getActive(): Promise<ConsultantClientSummary[]> {
    return apiService.get<ConsultantClientSummary[]>(`${this.baseUrl}/active`);
  }

  getByIndustry(industry: string): Promise<ConsultantClientSummary[]> {
    return apiService.get<ConsultantClientSummary[]>(
      `${this.baseUrl}/industry/${encodeURIComponent(industry)}`,
    );
  }

  /** The client record with its `engagements` collection populated. */
  getWithEngagements(id: string): Promise<ConsultantClient> {
    return apiService.get<ConsultantClient>(`${this.baseUrl}/${id}/engagements`);
  }

  create(data: CreateConsultantClient): Promise<ConsultantClient> {
    return apiService.post<ConsultantClient>(this.baseUrl, data);
  }

  update(id: string, data: UpdateConsultantClient): Promise<ConsultantClient> {
    return apiService.put<ConsultantClient>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Engagements nested under a client. `api/client-engagements` is the flat equivalent;
  // note the collection GET is `/engagements/list` because `/engagements` returns the
  // parent client with its engagements attached.

  getEngagements(clientId: string): Promise<ClientEngagementSummary[]> {
    return apiService.get<ClientEngagementSummary[]>(`${this.baseUrl}/${clientId}/engagements/list`);
  }

  addEngagement(clientId: string, data: CreateClientEngagement): Promise<ClientEngagement> {
    return apiService.post<ClientEngagement>(`${this.baseUrl}/${clientId}/engagements`, data);
  }

  // Portal accounts — external client contacts who confirm timesheets in the portal.

  getPortalAccounts(clientId: string): Promise<ConsultantClientPortalAccountSummary[]> {
    return apiService.get<ConsultantClientPortalAccountSummary[]>(
      `${this.baseUrl}/${clientId}/portal-accounts`,
    );
  }

  /** Emails a set-up link; the account stays "setup pending" until the contact completes it. */
  invitePortalAccount(
    clientId: string,
    data: InvitePortalAccount,
  ): Promise<ConsultantClientPortalAccountSummary> {
    return apiService.post<ConsultantClientPortalAccountSummary>(
      `${this.baseUrl}/${clientId}/portal-invite`,
      data,
    );
  }

  resendPortalInvite(clientId: string, email: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${clientId}/portal-invite/resend`, { email });
  }
}

/** api/client-engagements — placement contracts, with their status lifecycle. */
class ClientEngagementService {
  private readonly baseUrl = '/client-engagements';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<ClientEngagementSummary>> {
    return apiService.get<PagedResult<ClientEngagementSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<ClientEngagement> {
    return apiService.get<ClientEngagement>(`${this.baseUrl}/${id}`);
  }

  getByCode(engagementCode: string): Promise<ClientEngagement | null> {
    return apiService.get<ClientEngagement | null>(`${this.baseUrl}/code/${engagementCode}`);
  }

  getByClient(clientId: string): Promise<ClientEngagementSummary[]> {
    return apiService.get<ClientEngagementSummary[]>(`${this.baseUrl}/client/${clientId}`);
  }

  getByConsultant(consultantEmployeeId: string): Promise<ClientEngagementSummary[]> {
    return apiService.get<ClientEngagementSummary[]>(
      `${this.baseUrl}/consultant/${consultantEmployeeId}`,
    );
  }

  getByStatus(status: ClientEngagementStatus): Promise<ClientEngagementSummary[]> {
    return apiService.get<ClientEngagementSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getActive(): Promise<ClientEngagementSummary[]> {
    return apiService.get<ClientEngagementSummary[]>(`${this.baseUrl}/active`);
  }

  getByBillingCycle(cycle: BillingCycle): Promise<ClientEngagementSummary[]> {
    return apiService.get<ClientEngagementSummary[]>(`${this.baseUrl}/billing-cycle/${cycle}`);
  }

  /** Engagements whose end date falls within the next `days` — the renewal watchlist. */
  getEndingWithin(days = 30): Promise<ClientEngagementSummary[]> {
    return apiService.get<ClientEngagementSummary[]>(`${this.baseUrl}/ending-within`, { days });
  }

  create(data: CreateClientEngagement): Promise<ClientEngagement> {
    return apiService.post<ClientEngagement>(this.baseUrl, data);
  }

  update(id: string, data: UpdateClientEngagement): Promise<ClientEngagement> {
    return apiService.put<ClientEngagement>(`${this.baseUrl}/${id}`, data);
  }

  activate(id: string): Promise<ClientEngagement> {
    return apiService.post<ClientEngagement>(`${this.baseUrl}/${id}/activate`);
  }

  complete(id: string, actualEndDate: string): Promise<ClientEngagement> {
    return apiService.post<ClientEngagement>(`${this.baseUrl}/${id}/complete`, { actualEndDate });
  }

  /** Pauses billing without ending the contract; `resume` puts it back to Active. */
  suspend(id: string): Promise<ClientEngagement> {
    return apiService.post<ClientEngagement>(`${this.baseUrl}/${id}/suspend`);
  }

  resume(id: string): Promise<ClientEngagement> {
    return apiService.post<ClientEngagement>(`${this.baseUrl}/${id}/resume`);
  }

  terminate(id: string, reason?: string | null): Promise<ClientEngagement> {
    return apiService.post<ClientEngagement>(`${this.baseUrl}/${id}/terminate`, {
      reason: reason ?? null,
    });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/consultant-timesheets — billing-period timesheets, their entries and confirmations. */
class ConsultantTimesheetService {
  private readonly baseUrl = '/consultant-timesheets';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<ConsultantTimesheetSummary>> {
    return apiService.get<PagedResult<ConsultantTimesheetSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<ConsultantTimesheet> {
    return apiService.get<ConsultantTimesheet>(`${this.baseUrl}/${id}`);
  }

  getByNumber(timesheetNumber: string): Promise<ConsultantTimesheet | null> {
    return apiService.get<ConsultantTimesheet | null>(`${this.baseUrl}/number/${timesheetNumber}`);
  }

  getByConsultant(consultantEmployeeId: string): Promise<ConsultantTimesheetSummary[]> {
    return apiService.get<ConsultantTimesheetSummary[]>(
      `${this.baseUrl}/consultant/${consultantEmployeeId}`,
    );
  }

  getByEngagement(engagementId: string): Promise<ConsultantTimesheetSummary[]> {
    return apiService.get<ConsultantTimesheetSummary[]>(
      `${this.baseUrl}/engagement/${engagementId}`,
    );
  }

  getByStatus(status: TimesheetStatus): Promise<ConsultantTimesheetSummary[]> {
    return apiService.get<ConsultantTimesheetSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByPeriod(from: string, to: string): Promise<ConsultantTimesheetSummary[]> {
    return apiService.get<ConsultantTimesheetSummary[]>(`${this.baseUrl}/period`, { from, to });
  }

  getPendingApproval(): Promise<ConsultantTimesheetSummary[]> {
    return apiService.get<ConsultantTimesheetSummary[]>(`${this.baseUrl}/pending-approval`);
  }

  /** Approved internally, now waiting on the external client contact. */
  getPendingClientConfirmation(): Promise<ConsultantTimesheetSummary[]> {
    return apiService.get<ConsultantTimesheetSummary[]>(
      `${this.baseUrl}/pending-client-confirmation`,
    );
  }

  /** Client-confirmed and not yet on an invoice. */
  getApprovedForInvoicing(clientId?: string): Promise<ConsultantTimesheetSummary[]> {
    return apiService.get<ConsultantTimesheetSummary[]>(`${this.baseUrl}/approved-for-invoicing`, {
      clientId,
    });
  }

  create(data: CreateConsultantTimesheet): Promise<ConsultantTimesheet> {
    return apiService.post<ConsultantTimesheet>(this.baseUrl, data);
  }

  update(id: string, data: UpdateConsultantTimesheet): Promise<ConsultantTimesheet> {
    return apiService.put<ConsultantTimesheet>(`${this.baseUrl}/${id}`, data);
  }

  /** Starts the approval workflow. */
  submit(id: string): Promise<ConsultantTimesheet> {
    return apiService.post<ConsultantTimesheet>(`${this.baseUrl}/${id}/submit`);
  }

  approve(id: string, comments?: string | null): Promise<ConsultantTimesheet> {
    return apiService.post<ConsultantTimesheet>(`${this.baseUrl}/${id}/approve`, {
      comments: comments ?? null,
    });
  }

  reject(id: string, rejectionReason: string): Promise<ConsultantTimesheet> {
    return apiService.post<ConsultantTimesheet>(`${this.baseUrl}/${id}/reject`, {
      rejectionReason,
    });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Day entries. `totalHours` is computed server-side from start/end minus breaks.

  getEntries(id: string): Promise<ConsultantTimesheetEntry[]> {
    return apiService.get<ConsultantTimesheetEntry[]>(`${this.baseUrl}/${id}/entries`);
  }

  addEntry(id: string, data: CreateTimesheetEntry): Promise<ConsultantTimesheetEntry> {
    return apiService.post<ConsultantTimesheetEntry>(`${this.baseUrl}/${id}/entries`, data);
  }

  updateEntry(entryId: string, data: UpdateTimesheetEntry): Promise<ConsultantTimesheetEntry> {
    return apiService.put<ConsultantTimesheetEntry>(`${this.baseUrl}/entries/${entryId}`, data);
  }

  removeEntry(entryId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/entries/${entryId}`);
  }

  // Client confirmation — a tokenised link emailed to the client contact.

  getConfirmation(id: string): Promise<ClientTimesheetConfirmation | null> {
    return apiService.get<ClientTimesheetConfirmation | null>(`${this.baseUrl}/${id}/confirmation`);
  }

  sendConfirmation(
    id: string,
    data: SendTimesheetConfirmationRequest,
  ): Promise<SendTimesheetConfirmationResult> {
    return apiService.post<SendTimesheetConfirmationResult>(
      `${this.baseUrl}/${id}/send-confirmation`,
      data,
    );
  }

  /** Issues a fresh token and re-sends; the old link stops working. */
  resendConfirmation(
    id: string,
    data: SendTimesheetConfirmationRequest,
  ): Promise<SendTimesheetConfirmationResult> {
    return apiService.post<SendTimesheetConfirmationResult>(
      `${this.baseUrl}/${id}/resend-confirmation`,
      data,
    );
  }
}

/** api/timesheet-invoices — invoices raised from client-confirmed timesheets. */
class TimesheetInvoiceService {
  private readonly baseUrl = '/timesheet-invoices';

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<TimesheetInvoiceSummary>> {
    return apiService.get<PagedResult<TimesheetInvoiceSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<TimesheetInvoice> {
    return apiService.get<TimesheetInvoice>(`${this.baseUrl}/${id}`);
  }

  getByNumber(invoiceNumber: string): Promise<TimesheetInvoice | null> {
    return apiService.get<TimesheetInvoice | null>(`${this.baseUrl}/number/${invoiceNumber}`);
  }

  getByEngagement(engagementId: string): Promise<TimesheetInvoiceSummary[]> {
    return apiService.get<TimesheetInvoiceSummary[]>(`${this.baseUrl}/engagement/${engagementId}`);
  }

  getByClient(clientId: string): Promise<TimesheetInvoiceSummary[]> {
    return apiService.get<TimesheetInvoiceSummary[]>(`${this.baseUrl}/client/${clientId}`);
  }

  getByStatus(status: TimesheetInvoiceStatus): Promise<TimesheetInvoiceSummary[]> {
    return apiService.get<TimesheetInvoiceSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getOverdue(): Promise<TimesheetInvoiceSummary[]> {
    return apiService.get<TimesheetInvoiceSummary[]>(`${this.baseUrl}/overdue`);
  }

  getByPeriod(from: string, to: string): Promise<TimesheetInvoiceSummary[]> {
    return apiService.get<TimesheetInvoiceSummary[]>(`${this.baseUrl}/period`, { from, to });
  }

  /** Builds the invoice from the supplied timesheets; hours and totals are recomputed. */
  generate(data: GenerateTimesheetInvoice): Promise<TimesheetInvoice> {
    return apiService.post<TimesheetInvoice>(`${this.baseUrl}/generate`, data);
  }

  update(id: string, data: UpdateTimesheetInvoice): Promise<TimesheetInvoice> {
    return apiService.put<TimesheetInvoice>(`${this.baseUrl}/${id}`, data);
  }

  send(id: string): Promise<TimesheetInvoice> {
    return apiService.post<TimesheetInvoice>(`${this.baseUrl}/${id}/send`);
  }

  /** A payment below the invoice total lands the invoice on PartiallyPaid. */
  markPaid(id: string, paidDate: string, paidAmount: number): Promise<TimesheetInvoice> {
    return apiService.post<TimesheetInvoice>(`${this.baseUrl}/${id}/mark-paid`, {
      paidDate,
      paidAmount,
    });
  }

  void(id: string, reason: string): Promise<TimesheetInvoice> {
    return apiService.post<TimesheetInvoice>(`${this.baseUrl}/${id}/void`, { reason });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const consultantClientService = new ConsultantClientService();
export const clientEngagementService = new ClientEngagementService();
export const consultantTimesheetService = new ConsultantTimesheetService();
export const timesheetInvoiceService = new TimesheetInvoiceService();
