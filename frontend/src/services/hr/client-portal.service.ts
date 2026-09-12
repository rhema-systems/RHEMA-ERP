// The consultant-client surface's two clients:
//  - clientPortalService — the AUTHENTICATED contact surface (api/client-portal,
//    ConsultantClient role on the main JWT scheme). Ordinary apiService plumbing.
//  - publicTimesheetConfirmationService — the ANONYMOUS emailed-link flow
//    (api/client-timesheet-confirmation). Plain fetch: the GUID token is the whole
//    authority, no bearer token and no X-Tenant-Id (the server resolves the tenant
//    from the globally-unique token row).
//  - completeClientSetup — the invite-completion call (api/auth/complete-client-setup),
//    anonymous by design: the setup token from the email is the proof.

import { apiService } from '@/services/api.service';
import type {
  ClientPortalDashboard,
  ClientTimesheetConfirmation,
  ClientTimesheetConfirmationPublic,
} from '@/types/hr/consultant';

const API_BASE = process.env.NEXT_PUBLIC_API_URL || '/api';

async function anonymousFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  });
  const text = await res.text();
  const body = text ? JSON.parse(text) : null;
  if (!res.ok) {
    throw new Error(body?.message ?? `Request failed (${res.status})`);
  }
  return body as T;
}

class ClientPortalService {
  private readonly baseUrl = '/client-portal';

  getDashboard(): Promise<ClientPortalDashboard> {
    return apiService.get<ClientPortalDashboard>(`${this.baseUrl}/dashboard`);
  }

  getTimesheet(id: string): Promise<ClientTimesheetConfirmationPublic> {
    return apiService.get<ClientTimesheetConfirmationPublic>(`${this.baseUrl}/timesheets/${id}`);
  }

  confirmTimesheet(id: string, clientNotes?: string | null): Promise<ClientTimesheetConfirmation> {
    return apiService.post<ClientTimesheetConfirmation>(
      `${this.baseUrl}/timesheets/${id}/confirm`,
      { clientNotes: clientNotes || null },
    );
  }

  /** A reason is required — the server refuses a blank rejection. */
  rejectTimesheet(id: string, clientNotes: string): Promise<ClientTimesheetConfirmation> {
    return apiService.post<ClientTimesheetConfirmation>(
      `${this.baseUrl}/timesheets/${id}/reject`,
      { clientNotes },
    );
  }
}

class PublicTimesheetConfirmationService {
  /** Also marks the confirmation Viewed server-side on first open. */
  validate(token: string): Promise<ClientTimesheetConfirmationPublic> {
    return anonymousFetch<ClientTimesheetConfirmationPublic>(
      `/client-timesheet-confirmation/validate?token=${encodeURIComponent(token)}`,
    );
  }

  confirm(token: string, clientNotes?: string | null): Promise<ClientTimesheetConfirmation> {
    return anonymousFetch<ClientTimesheetConfirmation>(`/client-timesheet-confirmation/confirm`, {
      method: 'POST',
      body: JSON.stringify({ confirmationToken: token, clientNotes: clientNotes || null }),
    });
  }

  reject(token: string, clientNotes: string): Promise<ClientTimesheetConfirmation> {
    return anonymousFetch<ClientTimesheetConfirmation>(`/client-timesheet-confirmation/reject`, {
      method: 'POST',
      body: JSON.stringify({ confirmationToken: token, clientNotes }),
    });
  }
}

/** Completes an HR invite: consumes the emailed setup token, sets the password, activates. */
export function completeClientSetup(
  email: string,
  token: string,
  newPassword: string,
  confirmPassword: string,
): Promise<{ success: boolean; message: string }> {
  return anonymousFetch<{ success: boolean; message: string }>(`/auth/complete-client-setup`, {
    method: 'POST',
    body: JSON.stringify({ email, token, newPassword, confirmPassword }),
  });
}

export const clientPortalService = new ClientPortalService();
export const publicTimesheetConfirmationService = new PublicTimesheetConfirmationService();
