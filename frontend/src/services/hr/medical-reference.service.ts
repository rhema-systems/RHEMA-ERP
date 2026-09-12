import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  HealthcareFacility,
  HealthcareFacilitySummary,
  HealthcareFacilityCreateRequest,
  HealthcareFacilityUpdateRequest,
  FacilityService,
  FacilityServiceCreateRequest,
  FacilityServiceUpdateRequest,
  Physician,
  PhysicianSummary,
  PhysicianCreateRequest,
  PhysicianUpdateRequest,
  MedicalInsuranceProvider,
  MedicalInsuranceProviderSummary,
  MedicalInsuranceProviderCreateRequest,
  MedicalInsuranceProviderUpdateRequest,
  MedicalInsurancePlan,
  MedicalInsurancePlanCreateRequest,
  MedicalInsurancePlanUpdateRequest,
  MedicalInsurancePolicySummary,
  MedicalInsuranceProviderFacility,
  AddMedicalInsuranceProviderFacility,
  UpdateMedicalInsuranceProviderFacility,
  MedicalInsuranceProviderDocument,
  UploadProviderDocumentFields,
  MedicalInsurancePremiumRecord,
  MedicalInsurancePremiumRecordSummary,
  CreateMedicalInsurancePremiumRecord,
  RecordPremiumPayment,
  MedicalInsuranceClaim,
  CreateMedicalInsuranceClaim,
  UpdateMedicalInsuranceClaimStatus,
  RecordMedicalInsuranceClaimPayment,
  MedicalBenefitScheme,
  MedicalBenefitSchemeSummary,
  MedicalBenefitSchemeCreateRequest,
  MedicalBenefitSchemeUpdateRequest,
  MedicalBenefitTier,
  MedicalBenefitTierCreateRequest,
  MedicalBenefitTierUpdateRequest,
} from '@/types/hr/medical';

/**
 * The facility and physician registers. Backend routes: api/healthcare-facilities,
 * api/medical-physicians.
 *
 * ⚠ Unlike the rest of the medical module, READS here are open to any authenticated user —
 * an employee filing their own claim has to name the facility they attended and the doctor
 * they saw. Writes need HR.Medical.Write and deletes HR.Medical.Admin, so an HR user will get
 * a 403 on delete by design.
 */
class MedicalFacilityService {
  private readonly facilities = '/healthcare-facilities';
  private readonly facilityServices = '/facility-services';
  private readonly physicians = '/medical-physicians';

  // ── Facilities ─────────────────────────────────────────────────────────────

  getFacilities(): Promise<HealthcareFacilitySummary[]> {
    return apiService.get<HealthcareFacilitySummary[]>(this.facilities);
  }

  getActiveFacilities(): Promise<HealthcareFacilitySummary[]> {
    return apiService.get<HealthcareFacilitySummary[]>(`${this.facilities}/active`);
  }

  /** Facilities that accept NHIS — the filter that matters when a claim is NHIS-funded. */
  getNhisFacilities(): Promise<HealthcareFacilitySummary[]> {
    return apiService.get<HealthcareFacilitySummary[]>(`${this.facilities}/nhis`);
  }

  searchFacilities(term: string): Promise<HealthcareFacilitySummary[]> {
    return apiService.get<HealthcareFacilitySummary[]>(`${this.facilities}/search`, { term });
  }

  getFacility(id: string): Promise<HealthcareFacility> {
    return apiService.get<HealthcareFacility>(`${this.facilities}/${id}`);
  }

  createFacility(payload: HealthcareFacilityCreateRequest): Promise<HealthcareFacility> {
    return apiService.post<HealthcareFacility>(this.facilities, payload);
  }

  updateFacility(
    id: string,
    payload: HealthcareFacilityUpdateRequest,
  ): Promise<HealthcareFacility> {
    return apiService.put<HealthcareFacility>(`${this.facilities}/${id}`, payload);
  }

  /**
   * ⚠ Refused with a 422 naming the count while the facility still lists services. The delete
   * is a soft delete, so nothing cascades and no foreign key objects - before slice 9 the
   * services simply stopped being listable while still existing. Deactivate the facility instead.
   */
  removeFacility(id: string): Promise<void> {
    return apiService.delete<void>(`${this.facilities}/${id}`);
  }

  // ── Facility services ──────────────────────────────────────────────────────

  /**
   * The services one facility offers. Reads are open to any authenticated user - an employee
   * filing a claim has to be able to see what the facility does.
   */
  getFacilityServices(facilityId: string): Promise<FacilityService[]> {
    return apiService.get<FacilityService[]>(`${this.facilityServices}/facility/${facilityId}`);
  }

  getFacilityService(id: string): Promise<FacilityService> {
    return apiService.get<FacilityService>(`${this.facilityServices}/${id}`);
  }

  /** ⚠ 404s when `facilityId` names no facility of yours; it used to be a raw 500. */
  createFacilityService(payload: FacilityServiceCreateRequest): Promise<FacilityService> {
    return apiService.post<FacilityService>(this.facilityServices, payload);
  }

  updateFacilityService(id: string, payload: FacilityServiceUpdateRequest): Promise<FacilityService> {
    return apiService.put<FacilityService>(`${this.facilityServices}/${id}`, payload);
  }

  /** ⚠ Needs HR.Medical.Admin, like every other delete in this module. HR staff get a 403. */
  removeFacilityService(id: string): Promise<void> {
    return apiService.delete<void>(`${this.facilityServices}/${id}`);
  }

  // ── Physicians ─────────────────────────────────────────────────────────────

  getPhysicians(): Promise<PhysicianSummary[]> {
    return apiService.get<PhysicianSummary[]>(this.physicians);
  }

  getPhysiciansByFacility(facilityId: string): Promise<PhysicianSummary[]> {
    return apiService.get<PhysicianSummary[]>(`${this.physicians}/facility/${facilityId}`);
  }

  searchPhysicians(term: string): Promise<PhysicianSummary[]> {
    return apiService.get<PhysicianSummary[]>(`${this.physicians}/search`, { term });
  }

  getPhysician(id: string): Promise<Physician> {
    return apiService.get<Physician>(`${this.physicians}/${id}`);
  }

  createPhysician(payload: PhysicianCreateRequest): Promise<Physician> {
    return apiService.post<Physician>(this.physicians, payload);
  }

  updatePhysician(id: string, payload: PhysicianUpdateRequest): Promise<Physician> {
    return apiService.put<Physician>(`${this.physicians}/${id}`, payload);
  }

  /** Records that the licence has been checked. One-way — there is no un-verify. */
  verifyPhysician(id: string, verifiedBy?: string | null): Promise<{ message: string }> {
    return apiService.post<{ message: string }>(`${this.physicians}/${id}/verify`, {
      physicianId: id,
      verifiedBy: verifiedBy ?? null,
    });
  }

  removePhysician(id: string): Promise<void> {
    return apiService.delete<void>(`${this.physicians}/${id}`);
  }
}

/**
 * Insurance providers and the plans they sell. Backend route: api/medical-insurance.
 *
 * A plan hangs off a provider, and an employee policy points at both. Deleting either is
 * HR.Medical.Admin.
 */
class MedicalInsuranceService {
  private readonly baseUrl = '/medical-insurance';

  getProviders(onlyActive = false): Promise<MedicalInsuranceProviderSummary[]> {
    return apiService.get<MedicalInsuranceProviderSummary[]>(
      `${this.baseUrl}/providers`,
      onlyActive ? { onlyActive: true } : undefined,
    );
  }

  getProvider(id: string): Promise<MedicalInsuranceProvider> {
    return apiService.get<MedicalInsuranceProvider>(`${this.baseUrl}/providers/${id}`);
  }

  createProvider(
    payload: MedicalInsuranceProviderCreateRequest,
  ): Promise<MedicalInsuranceProvider> {
    return apiService.post<MedicalInsuranceProvider>(`${this.baseUrl}/providers`, payload);
  }

  updateProvider(
    id: string,
    payload: MedicalInsuranceProviderUpdateRequest,
  ): Promise<MedicalInsuranceProvider> {
    return apiService.put<MedicalInsuranceProvider>(`${this.baseUrl}/providers/${id}`, payload);
  }

  removeProvider(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/providers/${id}`);
  }

  // ── Plans ──────────────────────────────────────────────────────────────────

  getPlansByProvider(providerId: string): Promise<MedicalInsurancePlan[]> {
    return apiService.get<MedicalInsurancePlan[]>(
      `${this.baseUrl}/providers/${providerId}/plans`,
    );
  }

  createPlan(payload: MedicalInsurancePlanCreateRequest): Promise<MedicalInsurancePlan> {
    return apiService.post<MedicalInsurancePlan>(`${this.baseUrl}/plans`, payload);
  }

  updatePlan(
    id: string,
    payload: MedicalInsurancePlanUpdateRequest,
  ): Promise<MedicalInsurancePlan> {
    return apiService.put<MedicalInsurancePlan>(`${this.baseUrl}/plans/${id}`, payload);
  }

  removePlan(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/plans/${id}`);
  }

  // ── network facilities ─────────────────────────────────────────────────────

  /**
   * Facilities inside this provider's network.
   *
   * ⚠ The create DTO is `Add…`, not `Create…`, and the row joins the provider to a healthcare
   * FACILITY from the medical facility register. Neither the facility nor the effective date can
   * be changed afterwards — the update payload carries only expiry, the two flags and notes.
   */
  getNetworkFacilities(providerId: string): Promise<MedicalInsuranceProviderFacility[]> {
    return apiService.get<MedicalInsuranceProviderFacility[]>(
      `${this.baseUrl}/providers/${providerId}/network-facilities`,
    );
  }

  addNetworkFacility(
    providerId: string,
    payload: AddMedicalInsuranceProviderFacility,
  ): Promise<MedicalInsuranceProviderFacility> {
    return apiService.post<MedicalInsuranceProviderFacility>(
      `${this.baseUrl}/network-facilities`,
      { providerId, ...payload },
    );
  }

  updateNetworkFacility(
    id: string,
    payload: Omit<UpdateMedicalInsuranceProviderFacility, 'id'>,
  ): Promise<MedicalInsuranceProviderFacility> {
    return apiService.put<MedicalInsuranceProviderFacility>(
      `${this.baseUrl}/network-facilities/${id}`,
      { id, ...payload },
    );
  }

  /** ⚠ Admin-tier. */
  removeNetworkFacility(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/network-facilities/${id}`);
  }

  // ── provider documents ─────────────────────────────────────────────────────

  /**
   * The provider's own paperwork — licence, rate card, contract.
   *
   * ⚠ **Files go through the controlled gate and only through it.** `POST provider-documents`
   * still exists for metadata but now refuses every file-location field, so it can only mint a row
   * naming a file that does not exist; it is not exposed here. The path it used to accept was a
   * path-injection sink, the third instance of that defect in the medical module.
   *
   * ⚠ **`filePath` is not a URL.** Download is a token-bearing fetch that returns a blob.
   */
  getProviderDocuments(providerId: string): Promise<MedicalInsuranceProviderDocument[]> {
    return apiService.get<MedicalInsuranceProviderDocument[]>(
      `${this.baseUrl}/providers/${providerId}/documents`,
    );
  }

  uploadProviderDocument(
    file: File,
    fields: UploadProviderDocumentFields,
  ): Promise<MedicalInsuranceProviderDocument> {
    return hrDocumentService.upload<MedicalInsuranceProviderDocument>(
      `${this.baseUrl}/provider-documents/upload`,
      file,
      {
        providerId: fields.providerId,
        documentType: fields.documentType,
        description: fields.description ?? undefined,
        expiryDate: fields.expiryDate ?? undefined,
      },
    );
  }

  downloadProviderDocument(id: string, fileName: string): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/provider-documents/${id}/download`,
      fileName,
    );
  }

  /** ⚠ Admin-tier. */
  removeProviderDocument(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/provider-documents/${id}`);
  }

  // ── premium records ────────────────────────────────────────────────────────

  /**
   * What the employer was billed for cover over a period.
   *
   * ⚠ **`billingPeriodStart` / `billingPeriodEnd` are `DateOnly`** — send `YYYY-MM-DD`. `dueDate`
   * beside them is a full DateTime, so one payload carries both shapes.
   *
   * ⚠ **Nothing computes the total from the two contributions.** It is what the insurer billed, so
   * a form that omitted it recorded a premium of zero — which is why the ledger listed
   * `totalPremiumAmount` as unreachable.
   *
   * ⚠ **There is no update and no delete.** A premium record is a bill; paying it is a transition.
   */
  getPremiumRecords(providerId: string): Promise<MedicalInsurancePremiumRecord[]> {
    return apiService.get<MedicalInsurancePremiumRecord[]>(
      `${this.baseUrl}/providers/${providerId}/premium-records`,
    );
  }

  /** ⚠ Summaries, not records — this one feeds a list rather than a form. */
  getOverduePremiumRecords(): Promise<MedicalInsurancePremiumRecordSummary[]> {
    return apiService.get<MedicalInsurancePremiumRecordSummary[]>(
      `${this.baseUrl}/premium-records/overdue`,
    );
  }

  createPremiumRecord(
    providerId: string,
    payload: CreateMedicalInsurancePremiumRecord,
  ): Promise<MedicalInsurancePremiumRecord> {
    return apiService.post<MedicalInsurancePremiumRecord>(
      `${this.baseUrl}/premium-records`,
      { providerId, ...payload },
    );
  }

  /** Stamps the reference, method and date. The body repeats the id as `premiumRecordId`. */
  recordPremiumPayment(id: string, payload: RecordPremiumPayment) {
    return apiService.post(`${this.baseUrl}/premium-records/${id}/payment`, {
      premiumRecordId: id,
      ...payload,
    });
  }

  // ── insurance claims ───────────────────────────────────────────────────────

  /**
   * What was claimed from an insurer against one medical expense claim.
   *
   * ⚠ It joins a POLICY to an EXPENSE CLAIM, so it is authored from the expense claim's screen.
   * ⚠ Status and payment are two separate acts — the status route does not record money, and the
   * payment route does not move the status through the approval path.
   */
  getInsuranceClaimsForExpenseClaim(expenseClaimId: string): Promise<MedicalInsuranceClaim[]> {
    return apiService.get<MedicalInsuranceClaim[]>(
      `${this.baseUrl}/expense-claims/${expenseClaimId}/insurance-claims`,
    );
  }

  getInsuranceClaimsForPolicy(policyId: string): Promise<MedicalInsuranceClaim[]> {
    return apiService.get<MedicalInsuranceClaim[]>(
      `${this.baseUrl}/policies/${policyId}/insurance-claims`,
    );
  }

  createInsuranceClaim(payload: CreateMedicalInsuranceClaim): Promise<MedicalInsuranceClaim> {
    return apiService.post<MedicalInsuranceClaim>(`${this.baseUrl}/insurance-claims`, payload);
  }

  updateInsuranceClaimStatus(
    id: string,
    payload: Omit<UpdateMedicalInsuranceClaimStatus, 'claimId'>,
  ): Promise<MedicalInsuranceClaim> {
    return apiService.put<MedicalInsuranceClaim>(`${this.baseUrl}/insurance-claims/${id}/status`, {
      claimId: id,
      ...payload,
    });
  }

  recordInsuranceClaimPayment(
    id: string,
    payload: Omit<RecordMedicalInsuranceClaimPayment, 'claimId'>,
  ) {
    return apiService.post(`${this.baseUrl}/insurance-claims/${id}/payment`, {
      claimId: id,
      ...payload,
    });
  }

  /** The employee policies an insurance claim must point at. ⚠ HR has no policy EDITOR yet. */
  getPolicies(): Promise<MedicalInsurancePolicySummary[]> {
    return apiService.get<MedicalInsurancePolicySummary[]>(`${this.baseUrl}/policies`);
  }

  getPoliciesForEmployee(employeeId: string): Promise<MedicalInsurancePolicySummary[]> {
    return apiService.get<MedicalInsurancePolicySummary[]>(
      `${this.baseUrl}/employees/${employeeId}/policies`,
    );
  }
}

/**
 * Benefit schemes and their tiers. Backend route: api/medical-benefit-schemes.
 *
 * A tier is what an employee at a given staff level is entitled to. Its sub-limits are caps
 * WITHIN the annual limit, not additions to it.
 */
class MedicalBenefitSchemeService {
  private readonly baseUrl = '/medical-benefit-schemes';

  getSchemes(onlyActive = false): Promise<MedicalBenefitSchemeSummary[]> {
    return apiService.get<MedicalBenefitSchemeSummary[]>(
      this.baseUrl,
      onlyActive ? { onlyActive: true } : undefined,
    );
  }

  getScheme(id: string): Promise<MedicalBenefitScheme> {
    return apiService.get<MedicalBenefitScheme>(`${this.baseUrl}/${id}`);
  }

  createScheme(payload: MedicalBenefitSchemeCreateRequest): Promise<MedicalBenefitScheme> {
    return apiService.post<MedicalBenefitScheme>(this.baseUrl, payload);
  }

  updateScheme(
    id: string,
    payload: MedicalBenefitSchemeUpdateRequest,
  ): Promise<MedicalBenefitScheme> {
    return apiService.put<MedicalBenefitScheme>(`${this.baseUrl}/${id}`, payload);
  }

  removeScheme(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Tiers ──────────────────────────────────────────────────────────────────

  getTiersByScheme(schemeId: string): Promise<MedicalBenefitTier[]> {
    return apiService.get<MedicalBenefitTier[]>(`${this.baseUrl}/${schemeId}/tiers`);
  }

  createTier(payload: MedicalBenefitTierCreateRequest): Promise<MedicalBenefitTier> {
    return apiService.post<MedicalBenefitTier>(`${this.baseUrl}/tiers`, payload);
  }

  updateTier(
    id: string,
    payload: MedicalBenefitTierUpdateRequest,
  ): Promise<MedicalBenefitTier> {
    return apiService.put<MedicalBenefitTier>(`${this.baseUrl}/tiers/${id}`, payload);
  }

  removeTier(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/tiers/${id}`);
  }
}

export const medicalFacilityService = new MedicalFacilityService();
export const medicalInsuranceService = new MedicalInsuranceService();
export const medicalBenefitSchemeService = new MedicalBenefitSchemeService();
