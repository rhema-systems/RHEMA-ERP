import { apiService } from '../api.service';
import type { HrAudienceTargetType } from './announcements.service';

/**
 * Area 25 slice 12d (decision D7) — the staff policy library and its acknowledgements.
 *
 * Shapes measured against the live API (`probe-slice12d.mjs`). Enums arrive as strings.
 *
 * Two things worth knowing before using this:
 * - **Outstanding is the absence of an answer**, not a status. `myOutcome` is null until the
 *   employee acts, and a DECLINED policy is still outstanding — refusing is an answer to HR,
 *   not a way to discharge the obligation.
 * - **The compliance counts are always complete; the rows are a page.** A tenant-wide policy
 *   has thousands of rows (2.3 MB unpaged, measured), so filter and page the rows and read the
 *   four totals from the top of the payload.
 */

export type HrPolicyCategory =
  | 'General' | 'CodeOfConduct' | 'HumanResources'
  | 'HealthAndSafety' | 'Finance' | 'InformationTechnology';

export type HrPolicyStatus = 'Draft' | 'Published' | 'Archived';
export type HrPolicyAcknowledgementOutcome = 'Signed' | 'Declined';
export type PolicyComplianceFilter = 'All' | 'Outstanding' | 'Signed' | 'Declined';

export const POLICY_CATEGORIES: { value: HrPolicyCategory; label: string }[] = [
  { value: 'General', label: 'General' },
  { value: 'CodeOfConduct', label: 'Code of conduct' },
  { value: 'HumanResources', label: 'Human resources' },
  { value: 'HealthAndSafety', label: 'Health & safety' },
  { value: 'Finance', label: 'Finance' },
  { value: 'InformationTechnology', label: 'IT' },
];

export const POLICY_CATEGORY_LABEL: Record<HrPolicyCategory, string> =
  POLICY_CATEGORIES.reduce(
    (m, c) => ({ ...m, [c.value]: c.label }),
    {} as Record<HrPolicyCategory, string>,
  );

export interface HrPolicyAudience {
  id?: string | null;
  targetType: HrAudienceTargetType;
  targetId?: string | null;
  isExclusion: boolean;
  targetName?: string | null;
}

export interface HrPolicy {
  id: string;
  policyNumber: string;
  title: string;
  summary?: string | null;
  category: HrPolicyCategory;
  categoryName: string;
  versionLabel?: string | null;
  status: HrPolicyStatus;
  statusName: string;
  isLive: boolean;
  effectiveFrom?: string | null;
  reviewOn?: string | null;
  requiresAcknowledgement: boolean;
  acknowledgementText?: string | null;
  acknowledgementDueDays?: number | null;
  supersedesPolicyId?: string | null;
  supersedesTitle?: string | null;
  publishedAt?: string | null;
  publishedByName?: string | null;
  archivedAt?: string | null;
  archivedByName?: string | null;
  hasDocument: boolean;
  fileName?: string | null;
  audiences: HrPolicyAudience[];
  audienceCount?: number | null;
  signedCount?: number | null;
  declinedCount?: number | null;
}

export interface MyPolicy {
  id: string;
  policyNumber: string;
  title: string;
  summary?: string | null;
  category: HrPolicyCategory;
  categoryName: string;
  versionLabel?: string | null;
  effectiveFrom?: string | null;
  publishedAt?: string | null;
  hasDocument: boolean;
  fileName?: string | null;
  requiresAcknowledgement: boolean;
  acknowledgementText?: string | null;
  /** Null while they have neither signed nor declined. */
  myOutcome?: HrPolicyAcknowledgementOutcome | null;
  myOutcomeName?: string | null;
  mySignedAt?: string | null;
  myDeclinedAt?: string | null;
  myDeclineReason?: string | null;
  acknowledgementDueBy?: string | null;
  /** True while it still wants a signature — including after a decline. */
  isOutstandingForMe: boolean;
}

export interface SaveHrPolicyRequest {
  title: string;
  summary?: string | null;
  category: HrPolicyCategory;
  versionLabel?: string | null;
  effectiveFrom?: string | null;
  reviewOn?: string | null;
  requiresAcknowledgement: boolean;
  acknowledgementText?: string | null;
  acknowledgementDueDays?: number | null;
  supersedesPolicyId?: string | null;
  audiences: { targetType: HrAudienceTargetType; targetId?: string | null; isExclusion: boolean }[];
}

export interface PolicyComplianceRow {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  organizationUnitName?: string | null;
  positionTitle?: string | null;
  /** Null means outstanding. */
  outcome?: HrPolicyAcknowledgementOutcome | null;
  outcomeName?: string | null;
  signedAt?: string | null;
  declinedAt?: string | null;
  declineReason?: string | null;
}

export interface PolicyCompliance {
  policyId: string;
  policyNumber: string;
  title: string;
  versionLabel?: string | null;
  /** These four always cover the WHOLE audience, whatever the filter or page. */
  audienceCount: number;
  signedCount: number;
  declinedCount: number;
  outstandingCount: number;
  compliancePercent: number;
  filter: PolicyComplianceFilter;
  page: number;
  pageSize: number;
  /** How many rows match the filter — what the paging runs over. */
  totalRows: number;
  rows: PolicyComplianceRow[];
}

class MyPoliciesService {
  private readonly baseUrl = '/employee-portal/policies';

  getMine(): Promise<MyPolicy[]> {
    return apiService.get<MyPolicy[]>(this.baseUrl);
  }

  getOutstanding(): Promise<MyPolicy[]> {
    return apiService.get<MyPolicy[]>(`${this.baseUrl}/outstanding`);
  }

  /** 404s when it is not live or does not apply to the caller — the two are not distinguished. */
  getById(id: string): Promise<MyPolicy> {
    return apiService.get<MyPolicy>(`${this.baseUrl}/${id}`);
  }

  /**
   * Signs it. The declaration must be echoed back exactly as displayed — a mismatch is refused,
   * so nobody signs wording that changed while the page was open.
   */
  acknowledge(id: string, acknowledgementText: string): Promise<MyPolicy> {
    return apiService.post<MyPolicy>(`${this.baseUrl}/${id}/acknowledge`, { acknowledgementText });
  }

  decline(id: string, reason: string): Promise<MyPolicy> {
    return apiService.post<MyPolicy>(`${this.baseUrl}/${id}/decline`, { reason });
  }

  documentEndpoint(id: string): string {
    return `${this.baseUrl}/${id}/document`;
  }
}

class HrPoliciesService {
  private readonly baseUrl = '/hr/policies';

  getAll(status?: HrPolicyStatus): Promise<HrPolicy[]> {
    return apiService.get<HrPolicy[]>(status ? `${this.baseUrl}?status=${status}` : this.baseUrl);
  }

  getById(id: string): Promise<HrPolicy> {
    return apiService.get<HrPolicy>(`${this.baseUrl}/${id}`);
  }

  /** Counts are complete; rows are a page. Default to the outstanding slice — it is the work. */
  getCompliance(
    id: string,
    filter: PolicyComplianceFilter = 'Outstanding',
    page = 1,
    pageSize = 50,
  ): Promise<PolicyCompliance> {
    return apiService.get<PolicyCompliance>(
      `${this.baseUrl}/${id}/compliance?filter=${filter}&page=${page}&pageSize=${pageSize}`,
    );
  }

  create(payload: SaveHrPolicyRequest): Promise<HrPolicy> {
    return apiService.post<HrPolicy>(this.baseUrl, payload);
  }

  /** Drafts only — a published policy is superseded, never rewritten. */
  update(id: string, payload: SaveHrPolicyRequest): Promise<HrPolicy> {
    return apiService.put<HrPolicy>(`${this.baseUrl}/${id}`, payload);
  }

  /** Refused without a document, without a declaration when one is asked for, or with no audience. */
  publish(id: string): Promise<HrPolicy> {
    return apiService.post<HrPolicy>(`${this.baseUrl}/${id}/publish`, {});
  }

  archive(id: string): Promise<HrPolicy> {
    return apiService.post<HrPolicy>(`${this.baseUrl}/${id}/archive`, {});
  }

  deleteDraft(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  documentEndpoint(id: string): string {
    return `${this.baseUrl}/${id}/document`;
  }
}

export const myPoliciesService = new MyPoliciesService();
export const hrPoliciesService = new HrPoliciesService();
