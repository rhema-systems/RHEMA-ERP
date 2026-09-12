import { apiService } from '../api.service';

/**
 * Area 25 slice 12c (decision D7) — staff announcements.
 *
 * Shapes measured against the live API (`probe-slice12c.mjs`). Enums arrive as strings.
 *
 * ⚠ `body` is PLAIN TEXT and must never be rendered as markup. It reaches every employee, so
 * treating it as HTML would be a stored-XSS vector aimed at the whole staff. Render it with
 * `whitespace-pre-line`, not `dangerouslySetInnerHTML`. (The HR letters of slice 12b are the
 * opposite case — server-composed HTML with no user string in it.)
 */

export type HrAudienceTargetType =
  | 'AllEmployees'
  | 'OrganizationUnit'
  | 'OrganizationLevel'
  | 'Position'
  | 'Location'
  | 'Employee';

/**
 * Where somebody sits is OrganizationUnit + OrganizationLevel — the org chart is generic and a
 * tenant names its own tiers. There is deliberately no "Department" axis: that column is a
 * parallel legacy one, and on live data it is both coarser and less complete than the unit tree.
 */
export const AUDIENCE_TARGETS: { value: HrAudienceTargetType; label: string; needsTarget: boolean; hint: string }[] = [
  { value: 'AllEmployees', label: 'Everyone', needsTarget: false, hint: 'Every active employee.' },
  { value: 'OrganizationUnit', label: 'Organisation unit', needsTarget: true, hint: 'The unit and everything beneath it.' },
  { value: 'OrganizationLevel', label: 'Organisation level', needsTarget: true, hint: 'A tier of the org chart.' },
  { value: 'Position', label: 'Position', needsTarget: true, hint: 'Everyone holding this job.' },
  { value: 'Location', label: 'Location', needsTarget: true, hint: 'Everyone based at this site.' },
  { value: 'Employee', label: 'One person', needsTarget: true, hint: 'Mostly useful as an exception.' },
];

export type HrAnnouncementCategory =
  | 'General' | 'Policy' | 'Benefits' | 'Safety' | 'Event' | 'Urgent';

export type HrAnnouncementStatus = 'Draft' | 'Published' | 'Archived';

export const ANNOUNCEMENT_CATEGORIES: { value: HrAnnouncementCategory; label: string }[] = [
  { value: 'General', label: 'General' },
  { value: 'Policy', label: 'Policy' },
  { value: 'Benefits', label: 'Benefits' },
  { value: 'Safety', label: 'Safety' },
  { value: 'Event', label: 'Event' },
  { value: 'Urgent', label: 'Urgent' },
];

export interface HrAnnouncementAudience {
  id?: string | null;
  targetType: HrAudienceTargetType;
  targetId?: string | null;
  isExclusion: boolean;
  /** Resolved by the server so no screen keeps its own lookup map. */
  targetName?: string | null;
}

export interface HrAnnouncement {
  id: string;
  title: string;
  summary?: string | null;
  body: string;
  category: HrAnnouncementCategory;
  categoryName: string;
  status: HrAnnouncementStatus;
  statusName: string;
  isPinned: boolean;
  isLive: boolean;
  publishedAt?: string | null;
  publishedByName?: string | null;
  effectiveFrom?: string | null;
  expiresOn?: string | null;
  archivedAt?: string | null;
  archivedByName?: string | null;
  hasAttachment: boolean;
  fileName?: string | null;
  audiences: HrAnnouncementAudience[];
  /** How many people it actually reaches. Desk-side only. */
  audienceCount?: number | null;
}

/** The employee's view — deliberately without the targeting rules that selected them. */
export interface MyAnnouncement {
  id: string;
  title: string;
  summary?: string | null;
  body: string;
  category: HrAnnouncementCategory;
  categoryName: string;
  isPinned: boolean;
  publishedAt: string;
  publishedByName?: string | null;
  expiresOn?: string | null;
  hasAttachment: boolean;
  fileName?: string | null;
}

export interface SaveHrAnnouncementRequest {
  title: string;
  summary?: string | null;
  body: string;
  category: HrAnnouncementCategory;
  isPinned?: boolean;
  effectiveFrom?: string | null;
  expiresOn?: string | null;
  audiences: { targetType: HrAudienceTargetType; targetId?: string | null; isExclusion: boolean }[];
}

class MyAnnouncementsService {
  private readonly baseUrl = '/employee-portal/announcements';

  getMine(): Promise<MyAnnouncement[]> {
    return apiService.get<MyAnnouncement[]>(this.baseUrl);
  }

  /** 404s when it is not live or not aimed at the caller — the two are not distinguished. */
  getById(id: string): Promise<MyAnnouncement> {
    return apiService.get<MyAnnouncement>(`${this.baseUrl}/${id}`);
  }

  attachmentEndpoint(id: string): string {
    return `${this.baseUrl}/${id}/attachment`;
  }
}

class HrAnnouncementsService {
  private readonly baseUrl = '/hr/announcements';

  getAll(status?: HrAnnouncementStatus): Promise<HrAnnouncement[]> {
    return apiService.get<HrAnnouncement[]>(
      status ? `${this.baseUrl}?status=${status}` : this.baseUrl,
    );
  }

  getById(id: string): Promise<HrAnnouncement> {
    return apiService.get<HrAnnouncement>(`${this.baseUrl}/${id}`);
  }

  /** How many people a rule set reaches — shown while the form is being edited, not after. */
  async previewAudience(
    audiences: SaveHrAnnouncementRequest['audiences'],
  ): Promise<number> {
    const r = await apiService.post<{ count: number }>(`${this.baseUrl}/audience-preview`, audiences);
    return r.count;
  }

  create(payload: SaveHrAnnouncementRequest): Promise<HrAnnouncement> {
    return apiService.post<HrAnnouncement>(this.baseUrl, payload);
  }

  /** Drafts only — a published announcement is corrected by archiving and republishing. */
  update(id: string, payload: SaveHrAnnouncementRequest): Promise<HrAnnouncement> {
    return apiService.put<HrAnnouncement>(`${this.baseUrl}/${id}`, payload);
  }

  /** Refused when the audience reaches nobody. */
  publish(id: string): Promise<HrAnnouncement> {
    return apiService.post<HrAnnouncement>(`${this.baseUrl}/${id}/publish`, {});
  }

  archive(id: string): Promise<HrAnnouncement> {
    return apiService.post<HrAnnouncement>(`${this.baseUrl}/${id}/archive`, {});
  }

  deleteDraft(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  attachmentEndpoint(id: string): string {
    return `${this.baseUrl}/${id}/attachment`;
  }
}

export const myAnnouncementsService = new MyAnnouncementsService();
export const hrAnnouncementsService = new HrAnnouncementsService();
