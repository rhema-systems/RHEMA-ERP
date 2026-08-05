import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  BenefitPolicy,
  BenefitPolicyListItem,
  CreateBenefitPolicy,
  UpdateBenefitPolicy,
  BenefitPolicyLookups,
  BenefitGradeValue,
  CreateBenefitGradeValue,
  EmployeeBenefitEnrollment,
  EmployeeBenefitEnrollmentListItem,
  CreateEmployeeBenefitEnrollment,
  UpdateEmployeeBenefitEnrollment,
  EnrollmentStatusChange,
  EnrollmentBalance,
  BenefitUtilization,
  CreateBenefitUtilization,
  ClaimStatusChange,
  EmployeeBenefitPayrollLine,
  EnrollmentDependent,
  CreateEnrollmentDependent,
  UpdateEnrollmentDependent,
  RemoveDependentResult,
  BenefitBeneficiary,
  CreateBenefitBeneficiary,
  ReplaceBenefitBeneficiaries,
} from '@/types/hr/benefits';

/**
 * Benefit policies and the employee enrolments made against them.
 *
 * The enum-heavy parts of this area are driven by `getLookups()` rather than hardcoded unions —
 * the backend returns every option as `{ value, name, label }`, and `name` is what to send back
 * because enums serialize as strings.
 */

/** api/hr/benefit-policies */
class BenefitPolicyService {
  private readonly baseUrl = '/hr/benefit-policies';

  getAll(): Promise<BenefitPolicy[]> {
    return apiService.get<BenefitPolicy[]>(this.baseUrl);
  }

  getPaged(page = 1, pageSize = 20): Promise<PagedResult<BenefitPolicyListItem>> {
    return apiService.get<PagedResult<BenefitPolicyListItem>>(`${this.baseUrl}/paged`, {
      pageNumber: page,
      pageSize,
    });
  }

  getById(id: string): Promise<BenefitPolicy> {
    return apiService.get<BenefitPolicy>(`${this.baseUrl}/${id}`);
  }

  getActive(): Promise<BenefitPolicy[]> {
    return apiService.get<BenefitPolicy[]>(`${this.baseUrl}/active`);
  }

  getByType(policyType: string): Promise<BenefitPolicy[]> {
    return apiService.get<BenefitPolicy[]>(`${this.baseUrl}/type/${policyType}`);
  }

  /**
   * Policies in force on a date — the set someone can actually be enrolled into.
   * `asOfDate` is required; omitting it is a 400, not "today".
   */
  getEffective(asOfDate: string): Promise<BenefitPolicy[]> {
    return apiService.get<BenefitPolicy[]>(`${this.baseUrl}/effective`, { asOfDate });
  }

  /**
   * Every enum this area uses, plus the pay components a policy can link to. One call feeds all
   * the selects, so they never drift from the backend's enums.
   */
  getLookups(): Promise<BenefitPolicyLookups> {
    return apiService.get<BenefitPolicyLookups>(`${this.baseUrl}/lookups`);
  }

  create(data: CreateBenefitPolicy): Promise<BenefitPolicy> {
    return apiService.post<BenefitPolicy>(this.baseUrl, data);
  }

  update(id: string, data: UpdateBenefitPolicy): Promise<BenefitPolicy> {
    return apiService.put<BenefitPolicy>(`${this.baseUrl}/${id}`, data);
  }

  deactivate(id: string): Promise<void> {
    return apiService.patch<void>(`${this.baseUrl}/${id}/deactivate`);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Per-grade values ─────────────────────────────────────────────────────────
  // A policy can be worth different amounts by salary grade or staff level. Each row sets one
  // or the other, never both.

  getGradeValues(policyId: string): Promise<BenefitGradeValue[]> {
    return apiService.get<BenefitGradeValue[]>(`${this.baseUrl}/${policyId}/grade-values`);
  }

  addGradeValue(policyId: string, data: CreateBenefitGradeValue): Promise<BenefitGradeValue> {
    return apiService.post<BenefitGradeValue>(`${this.baseUrl}/${policyId}/grade-values`, data);
  }

  updateGradeValue(
    policyId: string,
    gradeValueId: string,
    data: CreateBenefitGradeValue & { id?: string },
  ): Promise<BenefitGradeValue> {
    return apiService.put<BenefitGradeValue>(
      `${this.baseUrl}/${policyId}/grade-values/${gradeValueId}`,
      data,
    );
  }

  removeGradeValue(policyId: string, gradeValueId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${policyId}/grade-values/${gradeValueId}`);
  }
}

/** api/hr/employee-benefit-enrollments */
class EmployeeBenefitEnrollmentService {
  private readonly baseUrl = '/hr/employee-benefit-enrollments';

  getById(id: string): Promise<EmployeeBenefitEnrollment> {
    return apiService.get<EmployeeBenefitEnrollment>(`${this.baseUrl}/${id}`);
  }

  getByEmployee(employeeId: string): Promise<EmployeeBenefitEnrollmentListItem[]> {
    return apiService.get<EmployeeBenefitEnrollmentListItem[]>(
      `${this.baseUrl}/by-employee/${employeeId}`,
    );
  }

  getByPolicy(policyId: string): Promise<EmployeeBenefitEnrollmentListItem[]> {
    return apiService.get<EmployeeBenefitEnrollmentListItem[]>(
      `${this.baseUrl}/by-policy/${policyId}`,
    );
  }

  create(data: CreateEmployeeBenefitEnrollment): Promise<EmployeeBenefitEnrollment> {
    return apiService.post<EmployeeBenefitEnrollment>(this.baseUrl, data);
  }

  update(
    id: string,
    data: UpdateEmployeeBenefitEnrollment,
  ): Promise<EmployeeBenefitEnrollment> {
    return apiService.put<EmployeeBenefitEnrollment>(`${this.baseUrl}/${id}`, data);
  }

  /** Moves the enrolment through its lifecycle: activate, suspend, terminate. */
  changeStatus(id: string, data: EnrollmentStatusChange): Promise<EmployeeBenefitEnrollment> {
    return apiService.post<EmployeeBenefitEnrollment>(`${this.baseUrl}/${id}/status`, data);
  }

  /**
   * Re-derives an employee's automatic enrolments from their position and grade, creating
   * the ones they now qualify for and retiring the ones they no longer do. Manual enrolments
   * are left alone.
   */
  reconcile(employeeId: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/reconcile?employeeId=${employeeId}`);
  }

  /**
   * What payroll picks up for one employee as of a date — a machine feed keyed by id, not a
   * report. It is per-employee, not a whole-period batch, and `employeeId` is required.
   */
  getPayrollLines(employeeId: string, asOf?: string): Promise<EmployeeBenefitPayrollLine[]> {
    return apiService.get<EmployeeBenefitPayrollLine[]>(`${this.baseUrl}/payroll-lines`, {
      employeeId,
      asOf,
    });
  }

  // ── Balance and claims ───────────────────────────────────────────────────────

  /** Coverage limit against what has been used, for the current limit period. */
  getBalance(id: string): Promise<EnrollmentBalance> {
    return apiService.get<EnrollmentBalance>(`${this.baseUrl}/${id}/balance`);
  }

  getUtilizations(id: string): Promise<BenefitUtilization[]> {
    return apiService.get<BenefitUtilization[]>(`${this.baseUrl}/${id}/utilizations`);
  }

  recordUtilization(id: string, data: CreateBenefitUtilization): Promise<BenefitUtilization> {
    return apiService.post<BenefitUtilization>(`${this.baseUrl}/${id}/utilizations`, data);
  }

  /** Claims are approved, rejected, paid or cancelled through this one endpoint. */
  changeClaimStatus(claimId: string, data: ClaimStatusChange): Promise<BenefitUtilization> {
    return apiService.post<BenefitUtilization>(
      `${this.baseUrl}/utilizations/${claimId}/status`,
      data,
    );
  }

  // ── Covered dependants ───────────────────────────────────────────────────────
  // Sourced from the employee's own registered dependants (`employeeService.getDependents`);
  // this endpoint only decides which of them this enrolment covers.

  /** Includes dependants whose cover has ended, so the history stays visible. */
  getDependents(id: string): Promise<EnrollmentDependent[]> {
    return apiService.get<EnrollmentDependent[]>(`${this.baseUrl}/${id}/dependents`);
  }

  /**
   * Refused (409) when the policy covers staff only, the dependant is deceased or already
   * covered, or the policy's `maxDependents` cap is met; 400 when the dependant belongs to a
   * different employee.
   */
  addDependent(id: string, data: CreateEnrollmentDependent): Promise<EnrollmentDependent> {
    return apiService.post<EnrollmentDependent>(`${this.baseUrl}/${id}/dependents`, data);
  }

  updateDependent(
    id: string,
    dependentBenefitId: string,
    data: UpdateEnrollmentDependent,
  ): Promise<EnrollmentDependent> {
    return apiService.put<EnrollmentDependent>(
      `${this.baseUrl}/${id}/dependents/${dependentBenefitId}`,
      data,
    );
  }

  /**
   * Check `deleted` on the result: a dependant with claims is kept as inactive rather than
   * deleted, and the caller should say so instead of reporting a clean removal.
   */
  removeDependent(id: string, dependentBenefitId: string): Promise<RemoveDependentResult> {
    return apiService.delete<RemoveDependentResult>(
      `${this.baseUrl}/${id}/dependents/${dependentBenefitId}`,
    );
  }

  // ── Beneficiaries ────────────────────────────────────────────────────────────

  getBeneficiaries(id: string): Promise<BenefitBeneficiary[]> {
    return apiService.get<BenefitBeneficiary[]>(`${this.baseUrl}/${id}/beneficiaries`);
  }

  /**
   * Sends the whole set at once — shares must total 100, which no sequence of single-row edits
   * can preserve. Pass an empty array to clear the nomination.
   */
  replaceBeneficiaries(
    id: string,
    beneficiaries: CreateBenefitBeneficiary[],
  ): Promise<BenefitBeneficiary[]> {
    return apiService.put<BenefitBeneficiary[]>(`${this.baseUrl}/${id}/beneficiaries`, {
      beneficiaries,
    } satisfies ReplaceBenefitBeneficiaries);
  }
}

export const benefitPolicyService = new BenefitPolicyService();
export const employeeBenefitEnrollmentService = new EmployeeBenefitEnrollmentService();
