import { apiService } from '../api.service';
import type { ContractType, CreateContractType, UpdateContractType } from '@/types/hr/contract-type';

/**
 * The contract-kind vocabulary.
 *
 * ⚠ **There is no delete.** Contracts name their kind by foreign key, so a kind that is no longer
 * offered is retired — `deactivate` — and the contracts already written against it keep resolving.
 */
class ContractTypeService {
  private readonly baseUrl = '/hr/contract-types';

  /** Everything, retired kinds included — the master screen. */
  getAll() {
    return apiService.get<ContractType[]>(this.baseUrl);
  }

  /** What a picker may offer. */
  getActive() {
    return apiService.get<ContractType[]>(`${this.baseUrl}?activeOnly=true`);
  }

  getById(id: string) {
    return apiService.get<ContractType>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateContractType) {
    return apiService.post<ContractType>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateContractType) {
    return apiService.put<ContractType>(`${this.baseUrl}/${id}`, payload);
  }

  deactivate(id: string) {
    return apiService.patch<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }
}

export const contractTypeService = new ContractTypeService();
