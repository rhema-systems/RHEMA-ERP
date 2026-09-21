import { apiService } from '../api.service';

/**
 * The suppliers an HR screen may name as the payee of a cost (round 2b, R7).
 *
 * ⚠ **Not `api/Suppliers`.** Procurement's own list answers 400 to every caller (measured
 * 2026-09-10, recorded for its owner), and even when it works this is the same shape and reason
 * as `hr-currency.service` and `finance-account.service`: another module owns what a supplier IS;
 * HR reads id, code, name and active — nothing else — through its own narrow door.
 */
export interface HrSupplierOption {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
}

class HrSupplierService {
  private readonly baseUrl = '/hr/suppliers';

  search(params: { search?: string; take?: number; includeInactive?: boolean } = {}) {
    return apiService.get<HrSupplierOption[]>(this.baseUrl, params);
  }

  /** Answers for an inactive supplier too, so a cost already paid to one still shows who. */
  getById(id: string) {
    return apiService.get<HrSupplierOption>(`${this.baseUrl}/${id}`);
  }
}

export const hrSupplierService = new HrSupplierService();
