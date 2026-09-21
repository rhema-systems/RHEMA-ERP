import { apiService } from '../api.service';
import type {
  Certification,
  CertificationExpiryLogEntry,
  CertificationExpiryReminderItem,
  CertificationExpiryRunResult,
  CertificationRequest,
  CreateEmployeeCertificationRequest,
  EmployeeCertification,
  EmployeeCertificationCompliance,
  PositionCertificationRequirement,
  RevokeEmployeeCertificationRequest,
  SkillCertification,
  UpdateEmployeeCertificationRequest,
} from '@/types/hr/certification';

/**
 * The certification model (demo feedback round 2, lane C2, plan § 6.3).
 *
 * - The catalogue under `api/hr/reference/certifications`, beside the certifying bodies it hangs
 *   off; `certifying-bodies/{id}/certifications` is the second half of the body → certification
 *   cascade every picker uses.
 * - A skill's accepted credentials and a position's required credentials are READ here; both are
 *   WRITTEN with the skill / position save, as the whole set (the replace-set convention the
 *   position's skills and benefits already follow).
 * - What an employee holds lives under `api/hr/Employees/{id}/certifications`; the evidence file
 *   goes through the document gate (`employeeDocumentService.uploadCertificationEvidence`).
 * - The expiry sweep under `api/hr/certification-expiry`.
 */
class CertificationService {
  private readonly catalogue = '/hr/reference/certifications';

  // ── Catalogue ──────────────────────────────────────────────────────────────

  getAll(params: { bodyId?: string; activeOnly?: boolean; search?: string } = {}): Promise<Certification[]> {
    return apiService.get<Certification[]>(this.catalogue, {
      ...(params.bodyId ? { bodyId: params.bodyId } : {}),
      ...(params.activeOnly ? { activeOnly: true } : {}),
      ...(params.search ? { search: params.search } : {}),
    });
  }

  /** The credentials one body issues — what the picker's second select lists. */
  getForBody(bodyId: string, activeOnly = true): Promise<Certification[]> {
    return apiService.get<Certification[]>(`/hr/reference/certifying-bodies/${bodyId}/certifications`, { activeOnly });
  }

  getById(id: string): Promise<Certification> {
    return apiService.get<Certification>(`${this.catalogue}/${id}`);
  }

  create(data: CertificationRequest): Promise<Certification> {
    return apiService.post<Certification>(this.catalogue, data);
  }

  update(id: string, data: CertificationRequest): Promise<Certification> {
    return apiService.put<Certification>(`${this.catalogue}/${id}`, { id, ...data });
  }

  /** ⚠ Refused (422) while any skill, position or employee cites it — make it inactive instead. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.catalogue}/${id}`);
  }

  // ── Consumers ──────────────────────────────────────────────────────────────

  getSkillCertifications(skillId: string): Promise<SkillCertification[]> {
    return apiService.get<SkillCertification[]>(`/hr/skills/${skillId}/certifications`);
  }

  getPositionRequirements(positionId: string): Promise<PositionCertificationRequirement[]> {
    return apiService.get<PositionCertificationRequirement[]>(`/EmployeePositions/${positionId}/certification-requirements`);
  }

  // ── Employee credentials ───────────────────────────────────────────────────

  private employee(employeeId: string) {
    return `/hr/Employees/${employeeId}/certifications`;
  }

  getEmployeeCertifications(employeeId: string): Promise<EmployeeCertification[]> {
    return apiService.get<EmployeeCertification[]>(this.employee(employeeId));
  }

  getCompliance(employeeId: string): Promise<EmployeeCertificationCompliance> {
    return apiService.get<EmployeeCertificationCompliance>(`/hr/Employees/${employeeId}/certification-compliance`);
  }

  addEmployeeCertification(employeeId: string, data: CreateEmployeeCertificationRequest): Promise<EmployeeCertification> {
    return apiService.post<EmployeeCertification>(this.employee(employeeId), data);
  }

  updateEmployeeCertification(employeeId: string, id: string, data: UpdateEmployeeCertificationRequest): Promise<EmployeeCertification> {
    return apiService.put<EmployeeCertification>(`${this.employee(employeeId)}/${id}`, { ...data, id });
  }

  revokeEmployeeCertification(employeeId: string, id: string, data: RevokeEmployeeCertificationRequest): Promise<EmployeeCertification> {
    return apiService.post<EmployeeCertification>(`${this.employee(employeeId)}/${id}/revoke`, data);
  }

  /** The verifier is the caller, stamped from the token; a person cannot verify their own. */
  verifyEmployeeCertification(employeeId: string, id: string): Promise<EmployeeCertification> {
    return apiService.post<EmployeeCertification>(`${this.employee(employeeId)}/${id}/verify`, {});
  }

  /** ⚠ Admin tier. */
  removeEmployeeCertification(employeeId: string, id: string): Promise<void> {
    return apiService.delete<void>(`${this.employee(employeeId)}/${id}`);
  }

  // ── The expiry sweep ───────────────────────────────────────────────────────

  previewExpiry(): Promise<CertificationExpiryReminderItem[]> {
    return apiService.get<CertificationExpiryReminderItem[]>('/hr/certification-expiry/preview');
  }

  runExpirySweep(): Promise<CertificationExpiryRunResult> {
    return apiService.post<CertificationExpiryRunResult>('/hr/certification-expiry/run', {});
  }

  getExpiryLog(days = 14): Promise<CertificationExpiryLogEntry[]> {
    return apiService.get<CertificationExpiryLogEntry[]>('/hr/certification-expiry/log', { days });
  }
}

export const certificationService = new CertificationService();
