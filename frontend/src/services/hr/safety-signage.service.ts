import { apiService } from '../api.service';
import type {
  SafetySign,
  SafetySignCreateRequest,
  SafetySignUpdateRequest,
  SheSafetySignType,
  SheSafetySignStatus,
} from '@/types/hr/safety-governance';

/**
 * Safety signage register (SoW 15.0 / FR-SHE-150-ish surface that exists).
 * Backend route: api/safety/signs.
 *
 * Sign codes are user-entered, unique per tenant (duplicate → 422), and immutable.
 * Inspection due dates are hand-kept; the reminder engine chases them automatically
 * and the due-for-inspection view is the work queue.
 */
class SafetySignageService {
  private readonly baseUrl = '/safety/signs';

  getAll(): Promise<SafetySign[]> {
    return apiService.get<SafetySign[]>(this.baseUrl);
  }

  getById(id: string): Promise<SafetySign> {
    return apiService.get<SafetySign>(`${this.baseUrl}/${id}`);
  }

  getByCode(code: string): Promise<SafetySign | null> {
    return apiService.get<SafetySign | null>(`${this.baseUrl}/code/${encodeURIComponent(code)}`);
  }

  getByLocation(locationId: string): Promise<SafetySign[]> {
    return apiService.get<SafetySign[]>(`${this.baseUrl}/location/${locationId}`);
  }

  getByType(type: SheSafetySignType): Promise<SafetySign[]> {
    return apiService.get<SafetySign[]>(`${this.baseUrl}/type/${type}`);
  }

  getByStatus(status: SheSafetySignStatus): Promise<SafetySign[]> {
    return apiService.get<SafetySign[]>(`${this.baseUrl}/status/${status}`);
  }

  getActive(): Promise<SafetySign[]> {
    return apiService.get<SafetySign[]>(`${this.baseUrl}/active`);
  }

  getDueForInspection(daysAhead = 30): Promise<SafetySign[]> {
    return apiService.get<SafetySign[]>(`${this.baseUrl}/due-for-inspection`, { daysAhead });
  }

  /** Refused (422) when the sign code is already taken. */
  create(data: SafetySignCreateRequest): Promise<SafetySign> {
    return apiService.post<SafetySign>(this.baseUrl, data);
  }

  update(id: string, data: SafetySignUpdateRequest): Promise<SafetySign> {
    return apiService.put<SafetySign>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const safetySignageService = new SafetySignageService();
export default safetySignageService;
