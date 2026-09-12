import { apiService } from '../api.service';
import type {
  CreateEmployeeRelieverRequest,
  EmployeeReliever,
  UpdateEmployeeRelieverRequest,
} from '@/types/hr/employee-reliever';

/**
 * The pre-defined reliever roster. Backend route: `api/hr/employee-relievers`.
 *
 * ⚠ **Gated self-or-HR.** An employee may read and change their own roster; HR and the admin roles
 * may touch anyone's. Anything else is a 403 — so a screen that lists another employee's relievers
 * must be an HR screen.
 *
 * ⚠ The server holds five rules this client does not: both parties must exist in the tenant and
 * still be on strength, nobody may relieve themselves, a priority may be used once per employee,
 * and one person may hold only one slot on a given roster. Each arrives as a 400 or 404 with a
 * sentence, and the sentence is what the toast shows.
 */
class EmployeeRelieverService {
  private readonly baseUrl = '/hr/employee-relievers';

  /** Ordered by priority: row 1 is the primary reliever. */
  getForEmployee(employeeId: string, activeOnly = false): Promise<EmployeeReliever[]> {
    return apiService.get<EmployeeReliever[]>(`${this.baseUrl}/employee/${employeeId}`, {
      activeOnly,
    });
  }

  /**
   * The caller's own roster, taken from the token rather than from an id the client supplies.
   * What the leave request form reads to seed its reliever slots.
   */
  getMine(activeOnly = true): Promise<EmployeeReliever[]> {
    return apiService.get<EmployeeReliever[]>(`${this.baseUrl}/mine`, { activeOnly });
  }

  create(data: CreateEmployeeRelieverRequest): Promise<EmployeeReliever> {
    return apiService.post<EmployeeReliever>(this.baseUrl, data);
  }

  update(id: string, data: UpdateEmployeeRelieverRequest): Promise<EmployeeReliever> {
    return apiService.put<EmployeeReliever>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const employeeRelieverService = new EmployeeRelieverService();
