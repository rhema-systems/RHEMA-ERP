import { apiService } from '../api.service';
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
