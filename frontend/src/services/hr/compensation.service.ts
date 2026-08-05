import { apiService } from '../api.service';
import type {
  PayComponent,
  UpdatePayComponent,
  UpdatePayComponentHrAttributes,
  PayComponentProjectionResult,
  PositionPayComponent,
  CreatePositionPayComponent,
  UpdatePositionPayComponentRequest,
  EmployeePayComponent,
  CreateEmployeePayComponent,
  UpdateEmployeePayComponent,
  EmployeeEmolumentSummary,
  EncashmentRateResult,
} from '@/types/hr/compensation';

/**
 * Compensation — the pay-component master and the position/employee emoluments built on it.
 *
 * ⚠ Split ownership on pay components. Payroll owns the component master; HR mirrors it and the
 * mirror reconciles itself on read, so a component added in payroll appears on the next HR read
 * rather than instantly. On a mirrored component (`isPayrollDefined`) the payroll-owned fields
 * are read-only — `update` and `deactivate` return **409** with a message pointing at the payroll
 * screen, and `updateHrAttributes` is the only permitted edit. Components HR defined itself stay
 * fully editable, so `update` works for those.
 */

/** api/hr/pay-components */
class PayComponentService {
  private readonly baseUrl = '/hr/pay-components';

  getAll(activeOnly = true): Promise<PayComponent[]> {
    return apiService.get<PayComponent[]>(this.baseUrl, { activeOnly });
  }

  getById(id: string): Promise<PayComponent> {
    return apiService.get<PayComponent>(`${this.baseUrl}/${id}`);
  }

  /**
   * Forces a payroll → HR projection pass. Reads reconcile on their own behind a short debounce,
   * so this is for when someone has just changed payroll and does not want to wait.
   */
  sync(): Promise<PayComponentProjectionResult> {
    return apiService.post<PayComponentProjectionResult>(`${this.baseUrl}/sync`);
  }

  /**
   * Updates only the fields HR owns. Safe on a mirrored component — these survive every sync,
   * because payroll has no equivalent for any of them.
   */
  updateHrAttributes(
    id: string,
    data: UpdatePayComponentHrAttributes,
  ): Promise<PayComponent> {
    return apiService.patch<PayComponent>(`${this.baseUrl}/${id}/hr-attributes`, data);
  }

  /** Full edit. Returns 409 for a mirrored component — use `updateHrAttributes` there instead. */
  update(id: string, data: UpdatePayComponent): Promise<PayComponent> {
    return apiService.put<PayComponent>(`${this.baseUrl}/${id}`, data);
  }

  /** Returns 409 for a mirrored component; deactivate it in payroll instead. */
  deactivate(id: string): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/deactivate`);
  }
}

/** api/hr/emoluments — position defaults, employee overrides, and the resolved package. */
class EmolumentService {
  private readonly baseUrl = '/hr/emoluments';

  // ── Position level ───────────────────────────────────────────────────────────
  // A component assigned here applies to everyone in the position; `amount` overrides the
  // component's own default, and leaving it null falls back to that default.

  getPositionComponents(positionId: string): Promise<PositionPayComponent[]> {
    return apiService.get<PositionPayComponent[]>(
      `${this.baseUrl}/positions/${positionId}/components`,
    );
  }

  assignPositionComponent(data: CreatePositionPayComponent): Promise<PositionPayComponent> {
    return apiService.post<PositionPayComponent>(`${this.baseUrl}/positions/components`, data);
  }

  updatePositionComponent(
    id: string,
    data: UpdatePositionPayComponentRequest,
  ): Promise<PositionPayComponent> {
    return apiService.put<PositionPayComponent>(
      `${this.baseUrl}/positions/components/${id}`,
      data,
    );
  }

  removePositionComponent(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/positions/components/${id}`);
  }

  // ── Employee level ───────────────────────────────────────────────────────────
  // An employee row either overrides the position default for the same component, or adds a
  // component the position does not carry. Unlike position rows, the amount is required.

  getEmployeeComponents(employeeId: string): Promise<EmployeePayComponent[]> {
    return apiService.get<EmployeePayComponent[]>(
      `${this.baseUrl}/employees/${employeeId}/components`,
    );
  }

  assignEmployeeComponent(data: CreateEmployeePayComponent): Promise<EmployeePayComponent> {
    return apiService.post<EmployeePayComponent>(`${this.baseUrl}/employees/components`, data);
  }

  updateEmployeeComponent(
    id: string,
    data: UpdateEmployeePayComponent & { id: string },
  ): Promise<EmployeePayComponent> {
    return apiService.put<EmployeePayComponent>(
      `${this.baseUrl}/employees/components/${id}`,
      data,
    );
  }

  removeEmployeeComponent(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/employees/components/${id}`);
  }

  // ── Derived ──────────────────────────────────────────────────────────────────

  /**
   * The employee's resolved package as of a date: position defaults with employee overrides
   * applied, percentages already turned into money, and the basic/gross/net roll-up.
   */
  getEmployeeSummary(employeeId: string, asOf?: string): Promise<EmployeeEmolumentSummary> {
    return apiService.get<EmployeeEmolumentSummary>(
      `${this.baseUrl}/employees/${employeeId}/summary`,
      { asOf },
    );
  }

  /**
   * The per-day rate leave encashment pays at, derived from basic plus the allowances the leave
   * type links. Pass `days` to also get the payout for that many days.
   */
  getEncashmentRate(
    employeeId: string,
    leaveTypeId: string,
    days = 0,
    asOf?: string,
  ): Promise<EncashmentRateResult> {
    return apiService.get<EncashmentRateResult>(`${this.baseUrl}/encashment-rate`, {
      employeeId,
      leaveTypeId,
      days,
      asOf,
    });
  }
}

export const payComponentService = new PayComponentService();
export const emolumentService = new EmolumentService();
