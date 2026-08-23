import { apiService } from '../api.service';
import type { PagedResult } from '@/types/hr/common';
import type {
  Employee,
  EmployeeDetail,
  EmployeeSearchRequest,
  CreateEmployeeRequest,
  UpdateEmployeeRequest,
  TerminateEmployeeRequest,
} from '@/types/hr/employee';
import type {
  EmployeeContact,
  CreateEmployeeContactRequest,
  UpdateEmployeeContactRequest,
  EmployeeEmergencyContact,
  CreateEmployeeEmergencyContactRequest,
  UpdateEmployeeEmergencyContactRequest,
  EmployeeDependent,
  CreateEmployeeDependentRequest,
  UpdateEmployeeDependentRequest,
  EmployeeDependentBenefit,
  CreateEmployeeDependentBenefitRequest,
  UpdateEmployeeDependentBenefitRequest,
  EmployeeQualification,
  CreateEmployeeQualificationRequest,
  UpdateEmployeeQualificationRequest,
  EmployeeSkill,
  CreateEmployeeSkillRequest,
  UpdateEmployeeSkillRequest,
  EmployeeIdentificationCard,
  CreateEmployeeIdentificationCardRequest,
  UpdateEmployeeIdentificationCardRequest,
  VerifyIdentificationCardRequest,
  EmployeeWorkHistory,
  CreateEmployeeWorkHistoryRequest,
  UpdateEmployeeWorkHistoryRequest,
  EmployeeContract,
  CreateEmployeeContractRequest,
  UpdateEmployeeContractRequest,
  TerminateContractRequest,
  ExpatriateAssignment,
  CreateExpatriateAssignmentRequest,
  UpdateExpatriateAssignmentRequest,
  EmployeePositionHistory,
  CreateEmployeePositionHistoryRequest,
  UpdateEmployeePositionHistoryRequest,
  EmployeeSalaryAssignment,
  CreateEmployeeSalaryAssignmentRequest,
  UpdateEmployeeSalaryAssignmentRequest,
  EmployeeReferee,
  CreateEmployeeRefereeRequest,
  UpdateEmployeeRefereeRequest,
  EmployeeGuarantor,
  CreateEmployeeGuarantorRequest,
  UpdateEmployeeGuarantorRequest,
  VerifyGuarantorRequest,
  EmployeeBankDetail,
  CreateEmployeeBankDetailRequest,
  UpdateEmployeeBankDetailRequest,
} from '@/types/hr/employee-subresources';

/**
 * Employee access. Backend route: api/hr/Employees.
 *
 * NOTE: the list endpoint is `POST /paged` with an EmployeeSearchDto body plus
 * page/pageSize query params (not a GET query-string).
 *
 * All 13 profile sub-resources hang off `{employeeId}/...` on this same controller, so
 * they live here rather than in 13 separate services. Update payloads are patch-style:
 * the backend requires `id` in the body as well as the route.
 */
class EmployeeService {
  private readonly baseUrl = '/hr/Employees';

  private sub(employeeId: string, path: string): string {
    return `${this.baseUrl}/${employeeId}/${path}`;
  }

  searchPaged(
    search: EmployeeSearchRequest = {},
    page = 1,
    pageSize = 20,
  ): Promise<PagedResult<Employee>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    return apiService.post<PagedResult<Employee>>(`${this.baseUrl}/paged?${params.toString()}`, search);
  }

  /**
   * The signed-in manager's own direct reports.
   *
   * Prefer this over the `manager/{id}/direct-reports` form: the client has no employee id of its
   * own, and screens that fetch one in order to pass it back are how several of this module's
   * authorization holes started.
   */
  getMyDirectReports(): Promise<Employee[]> {
    return apiService.get<Employee[]>(`${this.baseUrl}/manager/me/direct-reports`);
  }

  getById(id: string): Promise<Employee> {
    return apiService.get<Employee>(`${this.baseUrl}/${id}`);
  }

  getDetails(id: string): Promise<EmployeeDetail> {
    return apiService.get<EmployeeDetail>(`${this.baseUrl}/${id}/details`);
  }

  create(data: CreateEmployeeRequest): Promise<EmployeeDetail> {
    return apiService.post<EmployeeDetail>(this.baseUrl, data);
  }

  update(id: string, data: UpdateEmployeeRequest): Promise<EmployeeDetail> {
    return apiService.put<EmployeeDetail>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  deactivate(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/deactivate`);
  }

  activate(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/activate`);
  }

  terminate(id: string, data: TerminateEmployeeRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/terminate`, data);
  }

  reinstate(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/reinstate`, {});
  }

  // ── Headline stats (bare scalars / dictionaries, not wrapped) ─────────────────

  getTotalCount(): Promise<number> {
    return apiService.get<number>(`${this.baseUrl}/stats/total`);
  }

  getActiveCount(): Promise<number> {
    return apiService.get<number>(`${this.baseUrl}/stats/active`);
  }

  getCountByStatus(): Promise<Record<string, number>> {
    return apiService.get<Record<string, number>>(`${this.baseUrl}/stats/by-status`);
  }

  // `getCountByDepartment` was removed in slice 10 with the endpoint behind it. Department is the
  // deprecated dimension — an employee must have an organisation unit and need not have a
  // department — and nothing ever called this. For headcount by unit use `organogramService`,
  // whose `units` view carries `employeeCount` and a subtree rollup.

  // ── Contacts (addresses) ──────────────────────────────────────────────────────

  getContacts(employeeId: string): Promise<EmployeeContact[]> {
    return apiService.get<EmployeeContact[]>(this.sub(employeeId, 'contacts'));
  }

  addContact(employeeId: string, data: CreateEmployeeContactRequest): Promise<EmployeeContact> {
    return apiService.post<EmployeeContact>(this.sub(employeeId, 'contacts'), data);
  }

  updateContact(
    employeeId: string,
    contactId: string,
    data: UpdateEmployeeContactRequest,
  ): Promise<EmployeeContact> {
    return apiService.put<EmployeeContact>(this.sub(employeeId, `contacts/${contactId}`), data);
  }

  removeContact(employeeId: string, contactId: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `contacts/${contactId}`));
  }

  setPrimaryContact(employeeId: string, contactId: string): Promise<EmployeeContact> {
    return apiService.post<EmployeeContact>(
      this.sub(employeeId, `contacts/${contactId}/set-primary`),
    );
  }

  // ── Emergency contacts ────────────────────────────────────────────────────────

  getEmergencyContacts(employeeId: string): Promise<EmployeeEmergencyContact[]> {
    return apiService.get<EmployeeEmergencyContact[]>(this.sub(employeeId, 'emergency-contacts'));
  }

  addEmergencyContact(
    employeeId: string,
    data: CreateEmployeeEmergencyContactRequest,
  ): Promise<EmployeeEmergencyContact> {
    return apiService.post<EmployeeEmergencyContact>(
      this.sub(employeeId, 'emergency-contacts'),
      data,
    );
  }

  updateEmergencyContact(
    employeeId: string,
    id: string,
    data: UpdateEmployeeEmergencyContactRequest,
  ): Promise<EmployeeEmergencyContact> {
    return apiService.put<EmployeeEmergencyContact>(
      this.sub(employeeId, `emergency-contacts/${id}`),
      data,
    );
  }

  removeEmergencyContact(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `emergency-contacts/${id}`));
  }

  setPrimaryEmergencyContact(employeeId: string, id: string): Promise<EmployeeEmergencyContact> {
    return apiService.post<EmployeeEmergencyContact>(
      this.sub(employeeId, `emergency-contacts/${id}/set-primary`),
    );
  }

  activateEmergencyContact(employeeId: string, id: string): Promise<EmployeeEmergencyContact> {
    return apiService.post<EmployeeEmergencyContact>(
      this.sub(employeeId, `emergency-contacts/${id}/activate`),
    );
  }

  deactivateEmergencyContact(employeeId: string, id: string): Promise<EmployeeEmergencyContact> {
    return apiService.post<EmployeeEmergencyContact>(
      this.sub(employeeId, `emergency-contacts/${id}/deactivate`),
    );
  }

  // ── Dependents (and their benefit enrolments) ─────────────────────────────────

  getDependents(employeeId: string): Promise<EmployeeDependent[]> {
    return apiService.get<EmployeeDependent[]>(this.sub(employeeId, 'dependents'));
  }

  addDependent(
    employeeId: string,
    data: CreateEmployeeDependentRequest,
  ): Promise<EmployeeDependent> {
    return apiService.post<EmployeeDependent>(this.sub(employeeId, 'dependents'), data);
  }

  updateDependent(
    employeeId: string,
    id: string,
    data: UpdateEmployeeDependentRequest,
  ): Promise<EmployeeDependent> {
    return apiService.put<EmployeeDependent>(this.sub(employeeId, `dependents/${id}`), data);
  }

  removeDependent(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `dependents/${id}`));
  }

  getDependentBenefits(
    employeeId: string,
    dependentId: string,
  ): Promise<EmployeeDependentBenefit[]> {
    return apiService.get<EmployeeDependentBenefit[]>(
      this.sub(employeeId, `dependents/${dependentId}/benefits`),
    );
  }

  addDependentBenefit(
    employeeId: string,
    dependentId: string,
    data: CreateEmployeeDependentBenefitRequest,
  ): Promise<EmployeeDependentBenefit> {
    return apiService.post<EmployeeDependentBenefit>(
      this.sub(employeeId, `dependents/${dependentId}/benefits`),
      data,
    );
  }

  updateDependentBenefit(
    employeeId: string,
    dependentId: string,
    benefitId: string,
    data: UpdateEmployeeDependentBenefitRequest,
  ): Promise<EmployeeDependentBenefit> {
    return apiService.put<EmployeeDependentBenefit>(
      this.sub(employeeId, `dependents/${dependentId}/benefits/${benefitId}`),
      data,
    );
  }

  removeDependentBenefit(
    employeeId: string,
    dependentId: string,
    benefitId: string,
  ): Promise<void> {
    return apiService.delete<void>(
      this.sub(employeeId, `dependents/${dependentId}/benefits/${benefitId}`),
    );
  }

  activateDependentBenefit(
    employeeId: string,
    dependentId: string,
    benefitId: string,
  ): Promise<EmployeeDependentBenefit> {
    return apiService.post<EmployeeDependentBenefit>(
      this.sub(employeeId, `dependents/${dependentId}/benefits/${benefitId}/activate`),
    );
  }

  deactivateDependentBenefit(
    employeeId: string,
    dependentId: string,
    benefitId: string,
  ): Promise<EmployeeDependentBenefit> {
    return apiService.post<EmployeeDependentBenefit>(
      this.sub(employeeId, `dependents/${dependentId}/benefits/${benefitId}/deactivate`),
    );
  }

  // ── Qualifications ────────────────────────────────────────────────────────────

  getQualifications(employeeId: string): Promise<EmployeeQualification[]> {
    return apiService.get<EmployeeQualification[]>(this.sub(employeeId, 'qualifications'));
  }

  addQualification(
    employeeId: string,
    data: CreateEmployeeQualificationRequest,
  ): Promise<EmployeeQualification> {
    return apiService.post<EmployeeQualification>(this.sub(employeeId, 'qualifications'), data);
  }

  updateQualification(
    employeeId: string,
    id: string,
    data: UpdateEmployeeQualificationRequest,
  ): Promise<EmployeeQualification> {
    return apiService.put<EmployeeQualification>(
      this.sub(employeeId, `qualifications/${id}`),
      data,
    );
  }

  removeQualification(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `qualifications/${id}`));
  }

  verifyQualification(employeeId: string, id: string): Promise<EmployeeQualification> {
    return apiService.post<EmployeeQualification>(
      this.sub(employeeId, `qualifications/${id}/verify`),
    );
  }

  unverifyQualification(employeeId: string, id: string): Promise<EmployeeQualification> {
    return apiService.post<EmployeeQualification>(
      this.sub(employeeId, `qualifications/${id}/unverify`),
    );
  }

  // ── Skills ────────────────────────────────────────────────────────────────────

  getEmployeeSkills(employeeId: string): Promise<EmployeeSkill[]> {
    return apiService.get<EmployeeSkill[]>(this.sub(employeeId, 'skills'));
  }

  addEmployeeSkill(employeeId: string, data: CreateEmployeeSkillRequest): Promise<EmployeeSkill> {
    return apiService.post<EmployeeSkill>(this.sub(employeeId, 'skills'), data);
  }

  updateEmployeeSkill(
    employeeId: string,
    id: string,
    data: UpdateEmployeeSkillRequest,
  ): Promise<EmployeeSkill> {
    return apiService.put<EmployeeSkill>(this.sub(employeeId, `skills/${id}`), data);
  }

  removeEmployeeSkill(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `skills/${id}`));
  }

  verifyEmployeeSkill(employeeId: string, id: string): Promise<EmployeeSkill> {
    return apiService.post<EmployeeSkill>(this.sub(employeeId, `skills/${id}/verify`));
  }

  unverifyEmployeeSkill(employeeId: string, id: string): Promise<EmployeeSkill> {
    return apiService.post<EmployeeSkill>(this.sub(employeeId, `skills/${id}/unverify`));
  }

  // ── Identification cards ──────────────────────────────────────────────────────

  getIdentificationCards(employeeId: string): Promise<EmployeeIdentificationCard[]> {
    return apiService.get<EmployeeIdentificationCard[]>(
      this.sub(employeeId, 'identification-cards'),
    );
  }

  getIdentificationCard(employeeId: string, id: string): Promise<EmployeeIdentificationCard> {
    return apiService.get<EmployeeIdentificationCard>(
      this.sub(employeeId, `identification-cards/${id}`),
    );
  }

  addIdentificationCard(
    employeeId: string,
    data: CreateEmployeeIdentificationCardRequest,
  ): Promise<EmployeeIdentificationCard> {
    return apiService.post<EmployeeIdentificationCard>(
      this.sub(employeeId, 'identification-cards'),
      data,
    );
  }

  updateIdentificationCard(
    employeeId: string,
    id: string,
    data: UpdateEmployeeIdentificationCardRequest,
  ): Promise<EmployeeIdentificationCard> {
    return apiService.put<EmployeeIdentificationCard>(
      this.sub(employeeId, `identification-cards/${id}`),
      data,
    );
  }

  removeIdentificationCard(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `identification-cards/${id}`));
  }

  verifyIdentificationCard(
    employeeId: string,
    id: string,
    data: VerifyIdentificationCardRequest,
  ): Promise<EmployeeIdentificationCard> {
    return apiService.post<EmployeeIdentificationCard>(
      this.sub(employeeId, `identification-cards/${id}/verify`),
      data,
    );
  }

  unverifyIdentificationCard(employeeId: string, id: string): Promise<EmployeeIdentificationCard> {
    return apiService.post<EmployeeIdentificationCard>(
      this.sub(employeeId, `identification-cards/${id}/unverify`),
    );
  }

  // ── Work histories ────────────────────────────────────────────────────────────

  getWorkHistories(employeeId: string): Promise<EmployeeWorkHistory[]> {
    return apiService.get<EmployeeWorkHistory[]>(this.sub(employeeId, 'work-histories'));
  }

  addWorkHistory(
    employeeId: string,
    data: CreateEmployeeWorkHistoryRequest,
  ): Promise<EmployeeWorkHistory> {
    return apiService.post<EmployeeWorkHistory>(this.sub(employeeId, 'work-histories'), data);
  }

  updateWorkHistory(
    employeeId: string,
    id: string,
    data: UpdateEmployeeWorkHistoryRequest,
  ): Promise<EmployeeWorkHistory> {
    return apiService.put<EmployeeWorkHistory>(this.sub(employeeId, `work-histories/${id}`), data);
  }

  removeWorkHistory(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `work-histories/${id}`));
  }

  // ── Contracts ─────────────────────────────────────────────────────────────────

  getContracts(employeeId: string): Promise<EmployeeContract[]> {
    return apiService.get<EmployeeContract[]>(this.sub(employeeId, 'contracts'));
  }

  getActiveContract(employeeId: string): Promise<EmployeeContract | null> {
    return apiService.get<EmployeeContract | null>(this.sub(employeeId, 'contracts/active'));
  }

  addContract(
    employeeId: string,
    data: CreateEmployeeContractRequest,
  ): Promise<EmployeeContract> {
    return apiService.post<EmployeeContract>(this.sub(employeeId, 'contracts'), data);
  }

  updateContract(
    employeeId: string,
    id: string,
    data: UpdateEmployeeContractRequest,
  ): Promise<EmployeeContract> {
    return apiService.put<EmployeeContract>(this.sub(employeeId, `contracts/${id}`), data);
  }

  removeContract(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `contracts/${id}`));
  }

  activateContract(employeeId: string, id: string): Promise<EmployeeContract> {
    return apiService.post<EmployeeContract>(this.sub(employeeId, `contracts/${id}/activate`));
  }

  deactivateContract(employeeId: string, id: string): Promise<EmployeeContract> {
    return apiService.post<EmployeeContract>(this.sub(employeeId, `contracts/${id}/deactivate`));
  }

  terminateContract(
    employeeId: string,
    id: string,
    data: TerminateContractRequest,
  ): Promise<EmployeeContract> {
    return apiService.post<EmployeeContract>(
      this.sub(employeeId, `contracts/${id}/terminate`),
      data,
    );
  }

  // ── Expatriate assignments ────────────────────────────────────────────────────

  getExpatriateAssignments(employeeId: string): Promise<ExpatriateAssignment[]> {
    return apiService.get<ExpatriateAssignment[]>(this.sub(employeeId, 'expatriate-assignments'));
  }

  addExpatriateAssignment(
    employeeId: string,
    data: CreateExpatriateAssignmentRequest,
  ): Promise<ExpatriateAssignment> {
    return apiService.post<ExpatriateAssignment>(
      this.sub(employeeId, 'expatriate-assignments'),
      data,
    );
  }

  updateExpatriateAssignment(
    employeeId: string,
    id: string,
    data: UpdateExpatriateAssignmentRequest,
  ): Promise<ExpatriateAssignment> {
    return apiService.put<ExpatriateAssignment>(
      this.sub(employeeId, `expatriate-assignments/${id}`),
      data,
    );
  }

  removeExpatriateAssignment(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `expatriate-assignments/${id}`));
  }

  // ── Position histories ────────────────────────────────────────────────────────

  getPositionHistories(employeeId: string): Promise<EmployeePositionHistory[]> {
    return apiService.get<EmployeePositionHistory[]>(this.sub(employeeId, 'position-histories'));
  }

  addPositionHistory(
    employeeId: string,
    data: CreateEmployeePositionHistoryRequest,
  ): Promise<EmployeePositionHistory> {
    return apiService.post<EmployeePositionHistory>(
      this.sub(employeeId, 'position-histories'),
      data,
    );
  }

  updatePositionHistory(
    employeeId: string,
    id: string,
    data: UpdateEmployeePositionHistoryRequest,
  ): Promise<EmployeePositionHistory> {
    return apiService.put<EmployeePositionHistory>(
      this.sub(employeeId, `position-histories/${id}`),
      data,
    );
  }

  removePositionHistory(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `position-histories/${id}`));
  }

  // ── Salary assignments (grade/level/notch mirrored from payroll) ──────────────

  getSalaryAssignments(employeeId: string): Promise<EmployeeSalaryAssignment[]> {
    return apiService.get<EmployeeSalaryAssignment[]>(this.sub(employeeId, 'salary-assignments'));
  }

  addSalaryAssignment(
    employeeId: string,
    data: CreateEmployeeSalaryAssignmentRequest,
  ): Promise<EmployeeSalaryAssignment> {
    return apiService.post<EmployeeSalaryAssignment>(
      this.sub(employeeId, 'salary-assignments'),
      data,
    );
  }

  updateSalaryAssignment(
    employeeId: string,
    id: string,
    data: UpdateEmployeeSalaryAssignmentRequest,
  ): Promise<EmployeeSalaryAssignment> {
    return apiService.put<EmployeeSalaryAssignment>(
      this.sub(employeeId, `salary-assignments/${id}`),
      data,
    );
  }

  removeSalaryAssignment(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `salary-assignments/${id}`));
  }

  // ── Referees ──────────────────────────────────────────────────────────────────

  getReferees(employeeId: string): Promise<EmployeeReferee[]> {
    return apiService.get<EmployeeReferee[]>(this.sub(employeeId, 'referees'));
  }

  addReferee(employeeId: string, data: CreateEmployeeRefereeRequest): Promise<EmployeeReferee> {
    return apiService.post<EmployeeReferee>(this.sub(employeeId, 'referees'), data);
  }

  updateReferee(
    employeeId: string,
    id: string,
    data: UpdateEmployeeRefereeRequest,
  ): Promise<EmployeeReferee> {
    return apiService.put<EmployeeReferee>(this.sub(employeeId, `referees/${id}`), data);
  }

  removeReferee(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `referees/${id}`));
  }

  setPrimaryReferee(employeeId: string, id: string): Promise<EmployeeReferee> {
    return apiService.post<EmployeeReferee>(this.sub(employeeId, `referees/${id}/set-primary`));
  }

  activateReferee(employeeId: string, id: string): Promise<EmployeeReferee> {
    return apiService.post<EmployeeReferee>(this.sub(employeeId, `referees/${id}/activate`));
  }

  deactivateReferee(employeeId: string, id: string): Promise<EmployeeReferee> {
    return apiService.post<EmployeeReferee>(this.sub(employeeId, `referees/${id}/deactivate`));
  }

  // ── Guarantors ────────────────────────────────────────────────────────────────

  getGuarantors(employeeId: string): Promise<EmployeeGuarantor[]> {
    return apiService.get<EmployeeGuarantor[]>(this.sub(employeeId, 'guarantors'));
  }

  addGuarantor(
    employeeId: string,
    data: CreateEmployeeGuarantorRequest,
  ): Promise<EmployeeGuarantor> {
    return apiService.post<EmployeeGuarantor>(this.sub(employeeId, 'guarantors'), data);
  }

  updateGuarantor(
    employeeId: string,
    id: string,
    data: UpdateEmployeeGuarantorRequest,
  ): Promise<EmployeeGuarantor> {
    return apiService.put<EmployeeGuarantor>(this.sub(employeeId, `guarantors/${id}`), data);
  }

  removeGuarantor(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `guarantors/${id}`));
  }

  setPrimaryGuarantor(employeeId: string, id: string): Promise<EmployeeGuarantor> {
    return apiService.post<EmployeeGuarantor>(this.sub(employeeId, `guarantors/${id}/set-primary`));
  }

  verifyGuarantor(
    employeeId: string,
    id: string,
    data: VerifyGuarantorRequest,
  ): Promise<EmployeeGuarantor> {
    return apiService.post<EmployeeGuarantor>(
      this.sub(employeeId, `guarantors/${id}/verify`),
      data,
    );
  }

  unverifyGuarantor(employeeId: string, id: string): Promise<EmployeeGuarantor> {
    return apiService.post<EmployeeGuarantor>(this.sub(employeeId, `guarantors/${id}/unverify`));
  }

  activateGuarantor(employeeId: string, id: string): Promise<EmployeeGuarantor> {
    return apiService.post<EmployeeGuarantor>(this.sub(employeeId, `guarantors/${id}/activate`));
  }

  deactivateGuarantor(employeeId: string, id: string): Promise<EmployeeGuarantor> {
    return apiService.post<EmployeeGuarantor>(this.sub(employeeId, `guarantors/${id}/deactivate`));
  }

  // ── Bank details ──────────────────────────────────────────────────────────────

  getBankDetails(employeeId: string): Promise<EmployeeBankDetail[]> {
    return apiService.get<EmployeeBankDetail[]>(this.sub(employeeId, 'bank-details'));
  }

  addBankDetail(
    employeeId: string,
    data: CreateEmployeeBankDetailRequest,
  ): Promise<EmployeeBankDetail> {
    return apiService.post<EmployeeBankDetail>(this.sub(employeeId, 'bank-details'), data);
  }

  updateBankDetail(
    employeeId: string,
    id: string,
    data: UpdateEmployeeBankDetailRequest,
  ): Promise<EmployeeBankDetail> {
    return apiService.put<EmployeeBankDetail>(this.sub(employeeId, `bank-details/${id}`), data);
  }

  removeBankDetail(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(this.sub(employeeId, `bank-details/${id}`));
  }

  setPrimaryBankDetail(employeeId: string, id: string): Promise<EmployeeBankDetail> {
    return apiService.post<EmployeeBankDetail>(
      this.sub(employeeId, `bank-details/${id}/set-primary`),
    );
  }

  /**
   * Verification takes the verifier and timestamp as QUERY parameters on this endpoint,
   * unlike the guarantor/ID-card equivalents which take a body.
   */
  verifyBankDetail(
    employeeId: string,
    id: string,
    verifiedById: string,
    verifiedDate: string,
  ): Promise<EmployeeBankDetail> {
    const params = new URLSearchParams({ verifiedById, verifiedDate });
    return apiService.post<EmployeeBankDetail>(
      `${this.sub(employeeId, `bank-details/${id}/verify`)}?${params.toString()}`,
    );
  }

  unverifyBankDetail(employeeId: string, id: string): Promise<EmployeeBankDetail> {
    return apiService.post<EmployeeBankDetail>(
      this.sub(employeeId, `bank-details/${id}/unverify`),
    );
  }

  activateBankDetail(employeeId: string, id: string): Promise<EmployeeBankDetail> {
    return apiService.post<EmployeeBankDetail>(
      this.sub(employeeId, `bank-details/${id}/activate`),
    );
  }

  deactivateBankDetail(employeeId: string, id: string): Promise<EmployeeBankDetail> {
    return apiService.post<EmployeeBankDetail>(
      this.sub(employeeId, `bank-details/${id}/deactivate`),
    );
  }
}

export const employeeService = new EmployeeService();
