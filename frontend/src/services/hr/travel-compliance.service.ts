import { apiService } from '../api.service';
import type {
  StaffTravelDocument,
  CreateStaffTravelDocument,
  UpdateStaffTravelDocument,
  StaffTravelVisaApplication,
  CreateStaffTravelVisaApplication,
  UpdateStaffTravelVisaApplication,
  StaffTravelVisaRequirement,
  VisaApplicationStatus,
  StaffTravelRiskAssessment,
  CreateStaffTravelRiskAssessment,
  UpdateStaffTravelRiskAssessment,
  StaffTravelAlert,
  CreateStaffTravelAlert,
  UpdateStaffTravelAlert,
  StaffTravelAlertNotification,
  StaffTravelInsurancePolicy,
  CreateStaffTravelInsurancePolicy,
  UpdateStaffTravelInsurancePolicy,
  StaffTravelHealthRequirement,
  CreateStaffTravelHealthRequirement,
  StaffTravelPolicy,
  StaffTravelPolicySummary,
  CreateStaffTravelPolicy,
  UpdateStaffTravelPolicy,
  StaffTravelPolicyRule,
  CreateStaffTravelPolicyRule,
  UpdateStaffTravelPolicyRule,
  StaffTravelPolicyException,
  CreateStaffTravelPolicyException,
} from '@/types/hr/travel-compliance';

/**
 * Staff travel compliance and policy.
 *
 * <b>What makes this surface different from the rest of the area:</b> most of it is reference data
 * that outlives any one trip — visa requirements between two countries, health requirements for a
 * destination, the spend policy — while a handful of records belong to one trip and one person.
 * The screens split the same way: reference under `/administration/hr/travel/...`, per-trip
 * compliance on the request itself.
 *
 * ⚠ <b>A policy is a draft until approved, and a draft caps nothing.</b> `approve` is
 * `HR.Travel.Admin` and also puts the policy in force. An approved policy cannot be edited — the
 * update returns 422 telling you to raise a version. Nothing on a screen should offer an edit
 * button for an approved policy.
 *
 * ⚠ <b>Acknowledgements are personal.</b> `acknowledgeRiskAssessment` succeeds only for the
 * traveller and `acknowledgeAlertNotification` only for the addressee; anyone else gets 403. Do not
 * put either behind a desk-side "mark as done" control.
 */
class TravelComplianceService {
  private readonly baseUrl = '/staff-travel/compliance';
  private readonly policiesUrl = '/staff-travel/policies';

  // ── Travel documents ───────────────────────────────────────────────────────

  getDocuments() {
    return apiService.get<StaffTravelDocument[]>(`${this.baseUrl}/documents`);
  }

  getDocument(id: string) {
    return apiService.get<StaffTravelDocument>(`${this.baseUrl}/documents/${id}`);
  }

  getDocumentsByEmployee(employeeId: string) {
    return apiService.get<StaffTravelDocument[]>(`${this.baseUrl}/documents/employee/${employeeId}`);
  }

  /** Passports and visas falling due — the reminder sweep chases from 90 days out. */
  getExpiringDocuments(daysAhead = 90) {
    return apiService.get<StaffTravelDocument[]>(`${this.baseUrl}/documents/expiring`, { daysAhead });
  }

  createDocument(payload: CreateStaffTravelDocument) {
    return apiService.post<StaffTravelDocument>(`${this.baseUrl}/documents`, payload);
  }

  updateDocument(payload: UpdateStaffTravelDocument) {
    return apiService.put<StaffTravelDocument>(`${this.baseUrl}/documents/${payload.id}`, payload);
  }

  /** The verifier and the moment are both the server's. */
  verifyDocument(id: string) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/documents/${id}/verify`, { documentId: id });
  }

  /** Admin-gated. */
  deleteDocument(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/documents/${id}`);
  }

  // ── Visa requirements (reference data) ─────────────────────────────────────

  getVisaRequirements() {
    return apiService.get<StaffTravelVisaRequirement[]>(`${this.baseUrl}/visa-requirements`);
  }

  getVisaRequirementsForDestination(destinationCountryId: string) {
    return apiService.get<StaffTravelVisaRequirement[]>(
      `${this.baseUrl}/visa-requirements/destination/${destinationCountryId}`);
  }

  createVisaRequirement(payload: Omit<StaffTravelVisaRequirement, keyof StaffTravelDocument>) {
    return apiService.post<StaffTravelVisaRequirement>(
      `${this.baseUrl}/visa-requirements`, payload);
  }

  updateVisaRequirement(payload: StaffTravelVisaRequirement) {
    return apiService.put<StaffTravelVisaRequirement>(
      `${this.baseUrl}/visa-requirements/${payload.id}`, payload);
  }

  deleteVisaRequirement(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/visa-requirements/${id}`);
  }

  // ── Visa applications ──────────────────────────────────────────────────────

  getVisaApplications() {
    return apiService.get<StaffTravelVisaApplication[]>(`${this.baseUrl}/visa-applications`);
  }

  getVisaApplication(id: string) {
    return apiService.get<StaffTravelVisaApplication>(`${this.baseUrl}/visa-applications/${id}`);
  }

  getVisaApplicationsByRequest(requestId: string) {
    return apiService.get<StaffTravelVisaApplication[]>(
      `${this.baseUrl}/visa-applications/request/${requestId}`);
  }

  getVisaApplicationsByStatus(status: VisaApplicationStatus) {
    return apiService.get<StaffTravelVisaApplication[]>(
      `${this.baseUrl}/visa-applications/status/${status}`);
  }

  /** Visas falling due — a trip on an expiring visa is the case this exists to catch. */
  getExpiringVisas(daysAhead = 90) {
    return apiService.get<StaffTravelVisaApplication[]>(
      `${this.baseUrl}/visa-applications/expiring`, { daysAhead });
  }

  createVisaApplication(payload: CreateStaffTravelVisaApplication) {
    return apiService.post<StaffTravelVisaApplication>(
      `${this.baseUrl}/visa-applications`, payload);
  }

  updateVisaApplication(payload: UpdateStaffTravelVisaApplication) {
    return apiService.put<StaffTravelVisaApplication>(
      `${this.baseUrl}/visa-applications/${payload.id}`, payload);
  }

  deleteVisaApplication(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/visa-applications/${id}`);
  }

  // ── Risk assessments ───────────────────────────────────────────────────────

  getRiskAssessment(id: string) {
    return apiService.get<StaffTravelRiskAssessment>(`${this.baseUrl}/risk-assessments/${id}`);
  }

  getRiskAssessmentsByRequest(requestId: string) {
    return apiService.get<StaffTravelRiskAssessment[]>(
      `${this.baseUrl}/risk-assessments/request/${requestId}`);
  }

  getCurrentRiskAssessment(requestId: string) {
    return apiService.get<StaffTravelRiskAssessment | null>(
      `${this.baseUrl}/risk-assessments/request/${requestId}/current`);
  }

  getAssessmentsRequiringAcknowledgement() {
    return apiService.get<StaffTravelRiskAssessment[]>(
      `${this.baseUrl}/risk-assessments/requiring-acknowledgement`);
  }

  /** The assessor is the caller — there is no field for it. */
  createRiskAssessment(payload: CreateStaffTravelRiskAssessment) {
    return apiService.post<StaffTravelRiskAssessment>(
      `${this.baseUrl}/risk-assessments`, payload);
  }

  updateRiskAssessment(payload: UpdateStaffTravelRiskAssessment) {
    return apiService.put<StaffTravelRiskAssessment>(
      `${this.baseUrl}/risk-assessments/${payload.id}`, payload);
  }

  /**
   * ⚠ **Only the traveller can call this, for their own trip.** Anyone else gets 403. It records
   * that a person read a security briefing about where they are going, so it cannot be something a
   * desk does on their behalf.
   */
  acknowledgeRiskAssessment(id: string) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/risk-assessments/${id}/acknowledge`, {});
  }

  deleteRiskAssessment(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/risk-assessments/${id}`);
  }

  // ── Destination alerts ─────────────────────────────────────────────────────

  getAlert(id: string) {
    return apiService.get<StaffTravelAlert>(`${this.baseUrl}/alerts/${id}`);
  }

  getActiveAlerts() {
    return apiService.get<StaffTravelAlert[]>(`${this.baseUrl}/alerts/active`);
  }

  getAlertsByCountry(countryId: string) {
    return apiService.get<StaffTravelAlert[]>(`${this.baseUrl}/alerts/country/${countryId}`);
  }

  getCurrentAlertsForCountry(countryId: string) {
    return apiService.get<StaffTravelAlert[]>(
      `${this.baseUrl}/alerts/country/${countryId}/current`);
  }

  createAlert(payload: CreateStaffTravelAlert) {
    return apiService.post<StaffTravelAlert>(`${this.baseUrl}/alerts`, payload);
  }

  updateAlert(payload: UpdateStaffTravelAlert) {
    return apiService.put<StaffTravelAlert>(`${this.baseUrl}/alerts/${payload.id}`, payload);
  }

  deleteAlert(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/alerts/${id}`);
  }

  getAlertNotifications(employeeId: string) {
    return apiService.get<StaffTravelAlertNotification[]>(
      `${this.baseUrl}/alert-notifications/employee/${employeeId}`);
  }

  getUnacknowledgedAlerts(employeeId: string) {
    return apiService.get<StaffTravelAlertNotification[]>(
      `${this.baseUrl}/alert-notifications/employee/${employeeId}/unacknowledged`);
  }

  /** ⚠ Only the employee the alert was sent to. Anyone else gets 403. */
  acknowledgeAlertNotification(id: string) {
    return apiService.post<{ message: string }>(
      `${this.baseUrl}/alert-notifications/${id}/acknowledge`, {});
  }

  // ── Insurance ──────────────────────────────────────────────────────────────

  getInsurance(id: string) {
    return apiService.get<StaffTravelInsurancePolicy>(`${this.baseUrl}/insurance/${id}`);
  }

  getInsuranceByRequest(requestId: string) {
    return apiService.get<StaffTravelInsurancePolicy[]>(
      `${this.baseUrl}/insurance/request/${requestId}`);
  }

  createInsurance(payload: CreateStaffTravelInsurancePolicy) {
    return apiService.post<StaffTravelInsurancePolicy>(`${this.baseUrl}/insurance`, payload);
  }

  updateInsurance(payload: UpdateStaffTravelInsurancePolicy) {
    return apiService.put<StaffTravelInsurancePolicy>(
      `${this.baseUrl}/insurance/${payload.id}`, payload);
  }

  deleteInsurance(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/insurance/${id}`);
  }

  // ── Health requirements (reference data) ───────────────────────────────────

  getActiveHealthRequirements() {
    return apiService.get<StaffTravelHealthRequirement[]>(
      `${this.baseUrl}/health-requirements/active`);
  }

  getHealthRequirementsForCountry(countryId: string) {
    return apiService.get<StaffTravelHealthRequirement[]>(
      `${this.baseUrl}/health-requirements/country/${countryId}`);
  }

  getMandatoryHealthRequirements(countryId: string) {
    return apiService.get<StaffTravelHealthRequirement[]>(
      `${this.baseUrl}/health-requirements/country/${countryId}/mandatory`);
  }

  createHealthRequirement(payload: CreateStaffTravelHealthRequirement) {
    return apiService.post<StaffTravelHealthRequirement>(
      `${this.baseUrl}/health-requirements`, payload);
  }

  updateHealthRequirement(payload: StaffTravelHealthRequirement) {
    return apiService.put<StaffTravelHealthRequirement>(
      `${this.baseUrl}/health-requirements/${payload.id}`, payload);
  }

  deleteHealthRequirement(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/health-requirements/${id}`);
  }

  // ── Policies ───────────────────────────────────────────────────────────────

  getPolicies() {
    return apiService.get<StaffTravelPolicySummary[]>(this.policiesUrl);
  }

  /** The policies actually in force — approved, and not superseded. */
  getCurrentPolicies() {
    return apiService.get<StaffTravelPolicySummary[]>(`${this.policiesUrl}/current`);
  }

  getPolicy(id: string) {
    return apiService.get<StaffTravelPolicy>(`${this.policiesUrl}/${id}`);
  }

  /** What would apply to a traveller at a level, in a unit, on a date. */
  getApplicablePolicies(params: {
    staffLevelId?: string;
    organizationUnitId?: string;
    onDate: string;
  }) {
    return apiService.get<StaffTravelPolicySummary[]>(`${this.policiesUrl}/applicable`, params);
  }

  /** Creates a DRAFT. It caps nothing until approved. */
  createPolicy(payload: CreateStaffTravelPolicy) {
    return apiService.post<StaffTravelPolicy>(this.policiesUrl, payload);
  }

  /** ⚠ 422 for an approved policy — raise a new version instead. */
  updatePolicy(payload: UpdateStaffTravelPolicy) {
    return apiService.put<StaffTravelPolicy>(`${this.policiesUrl}/${payload.id}`, payload);
  }

  /**
   * ⚠ `HR.Travel.Admin`, and the caller must be linked to an employee record — the approver is
   * stored as an Employee FK, so a permission an unlinked account holds is one it cannot exercise.
   * Approving also puts the policy in force, superseding the previous one for the same scope.
   */
  approvePolicy(id: string) {
    return apiService.post<StaffTravelPolicy>(`${this.policiesUrl}/${id}/approve`, {});
  }
  /**
   * Stands an approved policy down so it stops capping bookings. `HR.Travel.Admin`.
   *
   * The policy stays approved — approval is a fact about the past, and what changes is whether it
   * is in force. Without this the only way to undo an approval was to approve a replacement with an
   * identical scope. 422 if the policy is not currently in force.
   */
  withdrawPolicy(id: string) {
    return apiService.post<StaffTravelPolicy>(`${this.policiesUrl}/${id}/withdraw`, {});
  }


  deletePolicy(id: string) {
    return apiService.delete<void>(`${this.policiesUrl}/${id}`);
  }

  getPolicyRules(policyId: string) {
    return apiService.get<StaffTravelPolicyRule[]>(`${this.policiesUrl}/${policyId}/rules`);
  }

  /**
   * ⚠ **Deliberately unwired.** The rules register is read-only until rule evaluation exists —
   * an editable control that enforces nothing creates false assurance. Kept, and covered by
   * `hr-travel/run-slice12-policy-authoring.mjs`, so enforcement is a screen change rather than
   * a rebuild.
   *
   * ⚠ **Re-using the code of a rule that was removed REVIVES that row**, overwriting its fields,
   * rather than creating a second one — the unique index on (policy, code) counts soft-deleted
   * rows. Re-using the code of a LIVE rule is refused. When rules become authorable this should
   * become a filtered unique index instead: a rule code is a label, not an identity, and reviving
   * keeps the previous rule's `createdAt`/`createdBy` on what is really a new rule.
   *
   * The payload used to be typed `Omit<StaffTravelPolicyRule, keyof StaffTravelPolicy>`, which
   * subtracts a policy's keys from a rule and describes nothing.
   */
  addPolicyRule(policyId: string, payload: CreateStaffTravelPolicyRule) {
    return apiService.post<StaffTravelPolicyRule>(
      `${this.policiesUrl}/${policyId}/rules`, { ...payload, policyId });
  }

  updatePolicyRule(payload: UpdateStaffTravelPolicyRule) {
    return apiService.put<StaffTravelPolicyRule>(
      `${this.policiesUrl}/rules/${payload.id}`, payload);
  }

  /** `HR.Travel.Admin`. */
  deletePolicyRule(ruleId: string) {
    return apiService.delete<void>(`${this.policiesUrl}/rules/${ruleId}`);
  }

  getPolicyExceptionsByRequest(requestId: string) {
    return apiService.get<StaffTravelPolicyException[]>(
      `${this.policiesUrl}/exceptions/request/${requestId}`);
  }

  getPendingPolicyExceptions() {
    return apiService.get<StaffTravelPolicyException[]>(`${this.policiesUrl}/exceptions/pending`);
  }

  /**
   * ⚠ **Deliberately unwired, along with the whole exception flow.** A `StaffTravelPolicyException`
   * is the artefact of the rules mechanism, and nothing evaluates a rule — so an exception can only
   * be raised by hand against a rule that never fires, manufacturing an audit record that implies a
   * control was in force and waived. A breach of the policy's own caps is a separate thing,
   * authorised inline on the booking by an `HR.Travel.Admin` holder.
   *
   * Kept and harness-covered so this ships with rule enforcement.
   */
  createPolicyException(payload: CreateStaffTravelPolicyException) {
    return apiService.post<StaffTravelPolicyException>(
      `${this.policiesUrl}/exceptions`, payload);
  }

  /**
   * ⚠ The decider is the token's, and only a Pending exception can be decided.
   *
   * `decisionNotes`, not `notes` — this method sent `notes` for as long as it has existed and the
   * DTO had no such property, so the model binder dropped every decision's reasoning on the floor.
   * The column exists now.
   */
  decidePolicyException(id: string, approve: boolean, decisionNotes?: string | null) {
    return apiService.post<{ message: string }>(
      `${this.policiesUrl}/exceptions/${id}/decide`,
      {
        exceptionId: id,
        status: approve ? 'Approved' : 'Rejected',
        decisionNotes: decisionNotes?.trim() || null,
      });
  }
}

export const travelComplianceService = new TravelComplianceService();
