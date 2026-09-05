// The careers surface's two clients:
//  - publicCareersService — the ANONYMOUS job board (api/public). Plain fetch: no bearer token,
//    and every call carries X-Tenant-Id, which the server requires because the public queries
//    are not covered by any tenant filter.
//  - candidateService — the AUTHENTICATED candidate self-service (api/candidate, Candidate role
//    on the main JWT scheme). Ordinary apiService, same token plumbing as the rest of the app.
//
// Browse public, apply logged-in: applying, drafts, documents, offers all live on the
// authenticated half. The anonymous apply/track/cv-upload endpoints were retired 2026-08-30.

import { apiService } from '@/services/api.service';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import type {
  ApplyPayload,
  CandidateApplicationSummary,
  CandidateDashboard,
  CandidateDocument,
  CandidateOffer,
  CandidateProfile,
  CandidateRegisterPayload,
  OfferLetter,
  OfferResponseChoice,
  OfferTokenValidation,
  PublicTenant,
  PublicVacancy,
  SaveCandidateProfilePayload,
} from '@/types/hr/careers';

const API_BASE = process.env.NEXT_PUBLIC_API_URL || '/api';

async function publicFetch<T>(path: string, tenantId: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      'X-Tenant-Id': tenantId,
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

class PublicCareersService {
  /**
   * Resolves the tenant the careers surface serves: an explicit
   * NEXT_PUBLIC_CAREERS_TENANT_ID wins; otherwise the single active tenant from the anonymous
   * tenant listing (the same one the login page uses). A multi-tenant host with no configured
   * id falls back to the first active tenant — configure the variable there.
   */
  async resolveTenant(): Promise<PublicTenant> {
    const configured = process.env.NEXT_PUBLIC_CAREERS_TENANT_ID;
    const res = await fetch(`${API_BASE}/tenant`);
    const body = await res.json();
    const tenants: PublicTenant[] = Array.isArray(body) ? body : body?.data ?? [];
    if (configured) {
      const match = tenants.find((t) => t.id === configured);
      if (match) return match;
      return { id: configured, name: 'Careers' };
    }
    if (tenants.length === 0) throw new Error('No tenant is available for the careers portal.');
    return tenants[0];
  }

  getVacancies(
    tenantId: string,
    filters: { search?: string; empType?: string; workMode?: string } = {},
  ): Promise<PublicVacancy[]> {
    const params = new URLSearchParams();
    if (filters.search) params.set('search', filters.search);
    if (filters.empType) params.set('empType', filters.empType);
    if (filters.workMode) params.set('workMode', filters.workMode);
    const qs = params.toString();
    return publicFetch<PublicVacancy[]>(`/public/vacancies${qs ? `?${qs}` : ''}`, tenantId);
  }

  getVacancy(tenantId: string, id: string): Promise<PublicVacancy> {
    return publicFetch<PublicVacancy>(`/public/vacancies/${id}`, tenantId);
  }

  /** POST api/auth/register-candidate — assigns the Candidate role; activation is by SMS OTP. */
  register(tenantId: string, payload: CandidateRegisterPayload): Promise<{ phoneNumber: string }> {
    return publicFetch<{ phoneNumber: string }>(`/auth/register-candidate`, tenantId, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  }

  // ── tokenised offer response ─────────────────────────────────────────────
  // The anonymous accept/negotiate/decline flow offer emails link to. The GUID token in the
  // link is the whole authority — no login, no tenant header — and it is single-use: the
  // server marks it consumed on a successful response.

  async validateOfferToken(token: string): Promise<OfferTokenValidation> {
    const res = await fetch(`${API_BASE}/offer-response/validate?token=${encodeURIComponent(token)}`);
    const body = await res.json();
    if (!res.ok) throw new Error(body?.message ?? `Request failed (${res.status})`);
    return body as OfferTokenValidation;
  }

  async respondWithOfferToken(
    token: string,
    response: OfferResponseChoice,
    notes?: string | null,
    declineReason?: string | null,
  ): Promise<{ message?: string }> {
    const res = await fetch(`${API_BASE}/offer-response/respond`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token, response, notes: notes ?? null, declineReason: declineReason ?? null }),
    });
    const body = await res.json().catch(() => null);
    if (!res.ok) throw new Error(body?.message ?? `Request failed (${res.status})`);
    return body ?? {};
  }
}

class CandidateService {
  private readonly baseUrl = '/candidate';

  getDashboard(): Promise<CandidateDashboard> {
    return apiService.get<CandidateDashboard>(`${this.baseUrl}/dashboard`);
  }

  getProfile(): Promise<CandidateProfile> {
    return apiService.get<CandidateProfile>(`${this.baseUrl}/profile`);
  }

  saveProfile(payload: SaveCandidateProfilePayload): Promise<CandidateProfile> {
    return apiService.put<CandidateProfile>(`${this.baseUrl}/profile`, payload);
  }

  // ── email confirmation (unlocks adopting an existing candidate profile) ──

  sendEmailConfirmation(): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/confirm-email/send`, {});
  }

  confirmEmail(token: string): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.baseUrl}/confirm-email`, { token });
  }

  // ── applications ─────────────────────────────────────────────────────────

  getApplications(): Promise<CandidateApplicationSummary[]> {
    return apiService.get<CandidateApplicationSummary[]>(`${this.baseUrl}/applications`);
  }

  /** Draft + submit in one call — the apply button. */
  apply(payload: ApplyPayload): Promise<CandidateApplicationSummary> {
    return apiService.post<CandidateApplicationSummary>(`${this.baseUrl}/applications`, {
      source: 'CompanyWebsite',
      ...payload,
    });
  }

  saveDraft(payload: ApplyPayload): Promise<CandidateApplicationSummary> {
    return apiService.post<CandidateApplicationSummary>(`${this.baseUrl}/applications/draft`, {
      source: 'CompanyWebsite',
      ...payload,
    });
  }

  submitDraft(
    applicationId: string,
    edits: Pick<ApplyPayload, 'coverLetter' | 'yearsOfExperience' | 'availableFrom'> = {},
  ): Promise<CandidateApplicationSummary> {
    return apiService.post<CandidateApplicationSummary>(
      `${this.baseUrl}/applications/${applicationId}/submit`,
      edits,
    );
  }

  withdraw(applicationId: string, reason?: string | null): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/applications/${applicationId}`, {
      reason: reason ?? null,
    });
  }

  // ── documents ────────────────────────────────────────────────────────────

  getDocuments(): Promise<CandidateDocument[]> {
    return apiService.get<CandidateDocument[]>(`${this.baseUrl}/documents`);
  }

  // ⚠ Multipart through the controlled-upload gate — never a JSON filePath.
  uploadDocument(file: File, documentType: string): Promise<CandidateDocument> {
    return hrDocumentService.upload<CandidateDocument>(`${this.baseUrl}/documents`, file, {
      documentType,
    });
  }

  downloadDocument(documentId: string): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/documents/${documentId}/download`);
  }

  deleteDocument(documentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }

  downloadCv(): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/cv`);
  }

  uploadPhoto(file: File): Promise<{ url: string }> {
    return hrDocumentService.upload<{ url: string }>(`${this.baseUrl}/profile/photo`, file);
  }

  downloadPhoto(): Promise<Blob> {
    return apiService.downloadBlob(`${this.baseUrl}/profile/photo`);
  }

  // ── offers ───────────────────────────────────────────────────────────────

  getOffer(applicationId: string): Promise<CandidateOffer> {
    return apiService.get<CandidateOffer>(`${this.baseUrl}/applications/${applicationId}/offer`);
  }

  getOfferLetter(applicationId: string): Promise<OfferLetter> {
    return apiService.get<OfferLetter>(`${this.baseUrl}/applications/${applicationId}/offer/letter`);
  }

  respondToOffer(
    applicationId: string,
    response: OfferResponseChoice,
    notes?: string | null,
    declineReason?: string | null,
  ): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/applications/${applicationId}/offer/respond`,
      { response, notes: notes ?? null, declineReason: declineReason ?? null },
    );
  }
}

export const publicCareersService = new PublicCareersService();
export const candidateService = new CandidateService();
