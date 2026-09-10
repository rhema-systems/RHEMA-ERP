import { apiService } from '../api.service';
import type {
  CreateRelationshipType,
  RelationshipCategory,
  RelationshipType,
  UpdateRelationshipType,
} from '@/types/hr/relationship-type';

/**
 * The relationship catalogue.
 *
 * ⚠ **Retire and delete are different acts.** `deactivate` withdraws a value from new records and
 * leaves every record already naming it alone; `remove` erases it, and the server refuses that with
 * a 422 and a count while anything still points at the row.
 */
class RelationshipTypeService {
  private readonly baseUrl = '/hr/relationship-types';

  /** Everything, retired rows included — the master screen. */
  getAll() {
    return apiService.get<RelationshipType[]>(this.baseUrl);
  }

  /**
   * What a picker may offer: active rows, of the categories that screen accepts.
   *
   * ⚠ The filter is a courtesy to the user, not the rule. The server refuses a value outside the
   * screen's set on the write, which is the only place a rule a caller can skip would hold.
   */
  getForScreen(categories: readonly RelationshipCategory[]) {
    const params = categories.map((c) => `categories=${encodeURIComponent(c)}`).join('&');
    return apiService.get<RelationshipType[]>(`${this.baseUrl}?activeOnly=true&${params}`);
  }

  getById(id: string) {
    return apiService.get<RelationshipType>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateRelationshipType) {
    return apiService.post<RelationshipType>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateRelationshipType) {
    return apiService.put<RelationshipType>(`${this.baseUrl}/${id}`, payload);
  }

  deactivate(id: string) {
    return apiService.patch<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  /** Admin-tier, and refused while any record still names the row. */
  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const relationshipTypeService = new RelationshipTypeService();
