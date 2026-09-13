import { apiService } from '../api.service';
import type {
  CreateDisabilityType,
  DisabilityCategory,
  DisabilityType,
  UpdateDisabilityType,
} from '@/types/hr/disability-type';

/**
 * The tenant's disability catalogue (round 3, lane P2). Backend: `api/hr/disability-types`.
 *
 * Reads are `HR.Employee.Read`, writes `HR.Employee.Write`, delete `HR.Employee.Admin` — and the
 * delete is refused with a 422 and a count while any employee or dependant still names the row.
 * Retire instead.
 */
class DisabilityTypeService {
  private readonly baseUrl = '/hr/disability-types';

  /** Everything, retired rows included — the master screen. */
  getAll() {
    return apiService.get<DisabilityType[]>(this.baseUrl);
  }

  /** What a picker offers: live rows only, optionally of some categories. */
  getActive(categories?: readonly DisabilityCategory[]) {
    const params = (categories ?? []).map((c) => `&categories=${encodeURIComponent(c)}`).join('');
    return apiService.get<DisabilityType[]>(`${this.baseUrl}?activeOnly=true${params}`);
  }

  getById(id: string) {
    return apiService.get<DisabilityType>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateDisabilityType) {
    return apiService.post<DisabilityType>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateDisabilityType) {
    return apiService.put<DisabilityType>(`${this.baseUrl}/${id}`, payload);
  }

  deactivate(id: string) {
    return apiService.patch<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const disabilityTypeService = new DisabilityTypeService();
