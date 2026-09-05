import { apiService } from '../api.service';
import type {
  CollectiveBargainingAgreement,
  CreateAgreementRequest,
  CreateUnionRequest,
  Union,
  UpdateAgreementRequest,
  UpdateUnionRequest,
} from '@/types/hr/union';

/**
 * The trade-union register and its collective bargaining agreements. Backend: `api/hr/unions`.
 *
 * ⚠ Reads are open to any authenticated user; **writes are SuperAdmin / TenantAdmin / HR**. The
 * union a role falls under is printed on the job description every employee can read, and a
 * collective agreement is published to the members it binds.
 *
 * ⚠ `remove` refuses a union that still has agreements, with a message naming how many. The delete
 * is a soft delete and the configured cascade only fires on a hard one, so deleting a union with
 * agreements would leave them alive and unreachable — every read of them goes through the union.
 */
class UnionService {
  private readonly baseUrl = '/hr/unions';

  /** Every union, each carrying its agreements. */
  getAll(): Promise<Union[]> {
    return apiService.get<Union[]>(this.baseUrl);
  }

  /** Active unions only — the picker source for the job-description form. */
  getActive(): Promise<Union[]> {
    return apiService.get<Union[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<Union> {
    return apiService.get<Union>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateUnionRequest): Promise<Union> {
    return apiService.post<Union>(this.baseUrl, data);
  }

  /** The response carries the agreements, so a screen may re-render straight from it. */
  update(id: string, data: UpdateUnionRequest): Promise<Union> {
    return apiService.put<Union>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  getAgreements(unionId: string): Promise<CollectiveBargainingAgreement[]> {
    return apiService.get<CollectiveBargainingAgreement[]>(`${this.baseUrl}/${unionId}/agreements`);
  }

  addAgreement(unionId: string, data: CreateAgreementRequest): Promise<CollectiveBargainingAgreement> {
    return apiService.post<CollectiveBargainingAgreement>(`${this.baseUrl}/${unionId}/agreements`, data);
  }

  /** ⚠ Route is `/agreements/{id}`, NOT nested under the union. An agreement cannot change union. */
  updateAgreement(id: string, data: UpdateAgreementRequest): Promise<CollectiveBargainingAgreement> {
    return apiService.put<CollectiveBargainingAgreement>(`${this.baseUrl}/agreements/${id}`, data);
  }

  removeAgreement(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/agreements/${id}`);
  }
}

export const unionService = new UnionService();
