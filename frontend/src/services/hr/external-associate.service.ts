import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  CreateExternalAssociateRequest,
  ExternalAssociate,
  ExternalAssociateSearchResult,
  ExternalAssociateSummary,
  UpdateExternalAssociateRequest,
} from '@/types/hr/external-associate';

/**
 * The external-associate register. Backend: `api/external-associates`.
 *
 * ⚠ **The whole surface is SuperAdmin / TenantAdmin / HR — reads included.** Tighter than the union
 * register next door, and deliberately: a union is a noticeboard, this is a directory of named third
 * parties' personal email addresses and phone numbers. It costs nothing in reach, because the one
 * endpoint anything consumes — `search`, behind `PanelMemberPicker` — is only ever rendered inside
 * an action `JobInterviewService` already restricts to HR.
 *
 * ⚠ `remove` refuses an associate who sits on an interview panel, with a message naming how many.
 * The delete is a soft delete, so the `OnDelete.Restrict` on the panel's foreign key never fires;
 * before slice 8 the associate simply vanished from every panel that carried them, and the orphaned
 * panelist row could no longer be reached to be tidied up. Deactivate instead — the picker filters
 * on `isActive`, so history keeps its members.
 */
class ExternalAssociateService {
  private readonly baseUrl = '/external-associates';

  /** Every associate, active or not, ordered by surname. */
  getAll(): Promise<ExternalAssociateSummary[]> {
    return apiService.get<ExternalAssociateSummary[]>(this.baseUrl);
  }

  /**
   * The register's page.
   *
   * ⚠ `isActive` is new in slice 8; before it the parameter did not exist and the screen could only
   * ever show everyone. `pageNumber` below 1 used to reach SQL as a negative OFFSET and answer 500;
   * both bounds are now clamped server-side, and `pageSize` is capped at 100.
   */
  getPaged(params: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    isActive?: boolean;
  } = {}): Promise<PagedResult<ExternalAssociateSummary>> {
    return apiService.get<PagedResult<ExternalAssociateSummary>>(`${this.baseUrl}/paged`, {
      pageNumber: params.pageNumber ?? 1,
      pageSize: params.pageSize ?? 20,
      ...(params.searchTerm ? { searchTerm: params.searchTerm } : {}),
      ...(params.isActive === undefined ? {} : { isActive: params.isActive }),
    });
  }

  /** Active associates only — the same set the panel picker draws from. */
  getActive(): Promise<ExternalAssociateSummary[]> {
    return apiService.get<ExternalAssociateSummary[]>(`${this.baseUrl}/active`);
  }

  /** Carries `interviewPanelCount`; the list rows do not. */
  getById(id: string): Promise<ExternalAssociate> {
    return apiService.get<ExternalAssociate>(`${this.baseUrl}/${id}`);
  }

  /** ⚠ 404s when the number names nobody. It used to answer 200 with a null body. */
  getByNumber(associateNumber: string): Promise<ExternalAssociate> {
    return apiService.get<ExternalAssociate>(`${this.baseUrl}/number/${associateNumber}`);
  }

  /** Typeahead; needs at least 2 characters or it returns an empty list. Active associates only. */
  search(q: string, limit = 20): Promise<ExternalAssociateSearchResult[]> {
    return apiService.get<ExternalAssociateSearchResult[]>(`${this.baseUrl}/search`, { q, limit });
  }

  /** The `EXT-nnnn` number is minted server-side and is never reissued, even after a delete. */
  create(data: CreateExternalAssociateRequest): Promise<ExternalAssociate> {
    return apiService.post<ExternalAssociate>(this.baseUrl, data);
  }

  update(id: string, data: UpdateExternalAssociateRequest): Promise<ExternalAssociate> {
    return apiService.put<ExternalAssociate>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Puts them back in the panel picker. 409 if they are already active. */
  activate(id: string): Promise<ExternalAssociate> {
    return apiService.post<ExternalAssociate>(`${this.baseUrl}/${id}/activate`, {});
  }

  /**
   * Takes them out of the panel picker while leaving every interview that used them intact.
   * This is the answer to "we are done with this person", not delete.
   */
  deactivate(id: string): Promise<ExternalAssociate> {
    return apiService.post<ExternalAssociate>(`${this.baseUrl}/${id}/deactivate`, {});
  }
}

export const externalAssociateService = new ExternalAssociateService();
