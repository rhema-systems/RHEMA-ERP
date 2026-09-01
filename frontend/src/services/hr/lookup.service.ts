import { apiService } from '../api.service';
import type {
  Qualification,
  QualificationRequest,
  IdentificationType,
  CreateIdentificationTypeRequest,
  UpdateIdentificationTypeRequest,
  ReasonCode,
  ReasonCodeRequest,
  Department,
  QualificationLevel,
  QualificationLevelRequest,
  CertifyingBody,
  CertifyingBodyRequest,
  StaffNumberFormat,
  StaffNumberFormatRequest,
  IdentificationExpiryItem,
  IdentificationExpiryRunResult,
  IdentificationExpiryRun,
  IdentificationExpiryLogEntry,
} from '@/types/hr/lookups';

/**
 * HR reference lookups. Each sits on its own controller with its own route — there is no
 * uniform `/hr` prefix in the ported module, so the base paths differ deliberately.
 */

/** api/hr/qualifications — the qualification catalogue behind employee qualifications. */
class QualificationService {
  private readonly baseUrl = '/hr/qualifications';

  getAll(): Promise<Qualification[]> {
    return apiService.get<Qualification[]>(this.baseUrl);
  }

  getActive(): Promise<Qualification[]> {
    return apiService.get<Qualification[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<Qualification> {
    return apiService.get<Qualification>(`${this.baseUrl}/${id}`);
  }

  create(data: QualificationRequest): Promise<Qualification> {
    return apiService.post<Qualification>(this.baseUrl, data);
  }

  /** Update reuses the create DTO on the backend. */
  update(id: string, data: QualificationRequest): Promise<Qualification> {
    return apiService.put<Qualification>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** api/hr/IdentificationTypes — ID document types behind employee identification cards. */
class IdentificationTypeService {
  private readonly baseUrl = '/hr/IdentificationTypes';

  getAll(): Promise<IdentificationType[]> {
    return apiService.get<IdentificationType[]>(this.baseUrl);
  }

  getActive(): Promise<IdentificationType[]> {
    return apiService.get<IdentificationType[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<IdentificationType> {
    return apiService.get<IdentificationType>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateIdentificationTypeRequest): Promise<IdentificationType> {
    return apiService.post<IdentificationType>(this.baseUrl, data);
  }

  update(id: string, data: UpdateIdentificationTypeRequest): Promise<IdentificationType> {
    return apiService.put<IdentificationType>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  activate(id: string): Promise<IdentificationType> {
    return apiService.put<IdentificationType>(`${this.baseUrl}/${id}/activate`);
  }

  deactivate(id: string): Promise<IdentificationType> {
    return apiService.put<IdentificationType>(`${this.baseUrl}/${id}/deactivate`);
  }
}

/** api/reason-codes — note: no `/hr` prefix on this one. */
class ReasonCodeService {
  private readonly baseUrl = '/reason-codes';

  getAll(): Promise<ReasonCode[]> {
    return apiService.get<ReasonCode[]>(this.baseUrl);
  }

  getById(id: string): Promise<ReasonCode> {
    return apiService.get<ReasonCode>(`${this.baseUrl}/${id}`);
  }

  create(data: ReasonCodeRequest): Promise<ReasonCode> {
    return apiService.post<ReasonCode>(this.baseUrl, data);
  }

  update(id: string, data: ReasonCodeRequest): Promise<ReasonCode> {
    return apiService.put<ReasonCode>(`${this.baseUrl}/${id}`, data);
  }

  deactivate(id: string): Promise<ReasonCode> {
    return apiService.patch<ReasonCode>(`${this.baseUrl}/${id}/deactivate`);
  }
}

/**
 * api/departments — READ ONLY. DepartmentsController exposes GET only (list, active,
 * by id); its create/update/delete service methods are deliberately not HTTP-wired, so
 * there is no department CRUD screen. Departments feed Employee.DepartmentId and other
 * modules; HR's own hierarchy is Organization Structure → Level → Unit.
 */
class DepartmentService {
  private readonly baseUrl = '/departments';

  getAll(): Promise<Department[]> {
    return apiService.get<Department[]>(this.baseUrl);
  }

  getActive(): Promise<Department[]> {
    return apiService.get<Department[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<Department> {
    return apiService.get<Department>(`${this.baseUrl}/${id}`);
  }
}

export const qualificationService = new QualificationService();
export const identificationTypeService = new IdentificationTypeService();
export const reasonCodeService = new ReasonCodeService();
export const departmentService = new DepartmentService();

// ── Lane 3b: reference dimensions ───────────────────────────────────────────

/**
 * api/hr/reference — the qualification ladder, the certifying-body catalogue, and the per-register
 * staff-numbering rules.
 */
class ReferenceDimensionService {
  private readonly baseUrl = '/hr/reference';

  // Qualification levels
  getQualificationLevels(activeOnly = false): Promise<QualificationLevel[]> {
    return apiService.get<QualificationLevel[]>(`${this.baseUrl}/qualification-levels`, { activeOnly });
  }
  createQualificationLevel(data: QualificationLevelRequest): Promise<QualificationLevel> {
    return apiService.post<QualificationLevel>(`${this.baseUrl}/qualification-levels`, data);
  }
  updateQualificationLevel(id: string, data: QualificationLevelRequest): Promise<QualificationLevel> {
    return apiService.put<QualificationLevel>(`${this.baseUrl}/qualification-levels/${id}`, { id, ...data });
  }
  removeQualificationLevel(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/qualification-levels/${id}`);
  }

  // Certifying bodies
  getCertifyingBodies(activeOnly = false): Promise<CertifyingBody[]> {
    return apiService.get<CertifyingBody[]>(`${this.baseUrl}/certifying-bodies`, { activeOnly });
  }
  createCertifyingBody(data: CertifyingBodyRequest): Promise<CertifyingBody> {
    return apiService.post<CertifyingBody>(`${this.baseUrl}/certifying-bodies`, data);
  }
  updateCertifyingBody(id: string, data: CertifyingBodyRequest): Promise<CertifyingBody> {
    return apiService.put<CertifyingBody>(`${this.baseUrl}/certifying-bodies/${id}`, { id, ...data });
  }
  removeCertifyingBody(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/certifying-bodies/${id}`);
  }

  // Staff number formats
  /**
   * ⚠ An EMPTY list is meaningful, not an unconfigured screen: with no rule, every register is
   * numbered by hand. The screen must say so rather than showing an empty table.
   */
  getStaffNumberFormats(): Promise<StaffNumberFormat[]> {
    return apiService.get<StaffNumberFormat[]>(`${this.baseUrl}/staff-number-formats`);
  }
  createStaffNumberFormat(data: StaffNumberFormatRequest): Promise<StaffNumberFormat> {
    return apiService.post<StaffNumberFormat>(`${this.baseUrl}/staff-number-formats`, data);
  }
  updateStaffNumberFormat(id: string, data: StaffNumberFormatRequest): Promise<StaffNumberFormat> {
    return apiService.put<StaffNumberFormat>(`${this.baseUrl}/staff-number-formats/${id}`, { id, ...data });
  }
  removeStaffNumberFormat(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/staff-number-formats/${id}`);
  }
  /** What a format would produce, composed by the server rather than re-implemented here. */
  previewStaffNumberFormat(data: {
    prefix: string; separator: string; includeYear: boolean;
    yearDigits: number; sequenceDigits: number; suffix: string;
  }): Promise<{ example: string }> {
    return apiService.post<{ example: string }>(`${this.baseUrl}/staff-number-formats/preview`, data);
  }
}

/** api/hr/identification-expiry — the sweep. Kept apart: that is an engine, not reference data. */
class IdentificationExpiryService {
  private readonly baseUrl = '/hr/identification-expiry';

  /**
   * What is expiring across the workforce, at BOTH tiers.
   *
   * ⚠ Gated on a policy, not filtered by recipient — so this is HR's whole view. `routedToEmployeeId`
   * says whose job a card is, not who may see it.
   */
  preview(): Promise<IdentificationExpiryItem[]> {
    return apiService.get<IdentificationExpiryItem[]>(`${this.baseUrl}/preview`);
  }

  /** Runs the sweep now. The nightly host runs the same code path. */
  run(): Promise<IdentificationExpiryRunResult> {
    return apiService.post<IdentificationExpiryRunResult>(`${this.baseUrl}/run`, {});
  }

  /**
   * The most recent passes, newest first.
   *
   * A pass that queued nothing still appears — that is the point. "It ran and found nothing" and
   * "it never ran" are indistinguishable from the dispatch log alone.
   */
  getRuns(count = 20): Promise<IdentificationExpiryRun[]> {
    return apiService.get<IdentificationExpiryRun[]>(`${this.baseUrl}/runs`, { count });
  }

  /** Reminders actually raised, over a trailing window. */
  getLog(days = 14): Promise<IdentificationExpiryLogEntry[]> {
    return apiService.get<IdentificationExpiryLogEntry[]>(`${this.baseUrl}/log`, { days });
  }
}

export const referenceDimensionService = new ReferenceDimensionService();
export const identificationExpiryService = new IdentificationExpiryService();
