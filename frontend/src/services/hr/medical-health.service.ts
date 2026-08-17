import { apiService } from '../api.service';
import { hrDocumentService } from './hr-document.service';
import type {
  EmployeeHealthProfile,
  EmployeeHealthProfileCreateRequest,
  EmployeeHealthProfileUpdateRequest,
  EmployeeHealthCondition,
  EmployeeHealthConditionCreateRequest,
  EmployeeHealthConditionUpdateRequest,
  EmployeeAllergy,
  EmployeeAllergyCreateRequest,
  EmployeeAllergyUpdateRequest,
  EmployeeMedicalExam,
  EmployeeMedicalExamSummary,
  EmployeeMedicalExamCreateRequest,
  EmployeeMedicalExamUpdateRequest,
  EmployeeMedicalExamDocument,
} from '@/types/hr/medical';

/**
 * Employee occupational-health records: profiles, conditions, allergies, examinations and the
 * documents attached to an examination. Backend route: api/employee-health.
 *
 * ⚠ Special-category personal data, and gated accordingly — every route here needs
 * HR.Medical.Read at minimum, writes need Write, and deletes need Admin (which HR staff do NOT
 * hold, so a delete will 403 for them by design).
 *
 * An employee reads their OWN file through api/employee-health/me instead; that surface is
 * deliberately separate and read-only, so nothing here should be reused for it.
 */
class MedicalHealthService {
  private readonly baseUrl = '/employee-health';

  // ── Profiles ───────────────────────────────────────────────────────────────

  getProfiles(): Promise<EmployeeHealthProfile[]> {
    return apiService.get<EmployeeHealthProfile[]>(`${this.baseUrl}/profiles`);
  }

  getProfile(id: string): Promise<EmployeeHealthProfile> {
    return apiService.get<EmployeeHealthProfile>(`${this.baseUrl}/profiles/${id}`);
  }

  /** Returns null-ish when the employee has no profile yet — not a 404. */
  getProfileByEmployee(employeeId: string): Promise<EmployeeHealthProfile | null> {
    return apiService.get<EmployeeHealthProfile | null>(
      `${this.baseUrl}/employees/${employeeId}/profile`,
    );
  }

  createProfile(payload: EmployeeHealthProfileCreateRequest): Promise<EmployeeHealthProfile> {
    return apiService.post<EmployeeHealthProfile>(`${this.baseUrl}/profiles`, payload);
  }

  updateProfile(
    id: string,
    payload: EmployeeHealthProfileUpdateRequest,
  ): Promise<EmployeeHealthProfile> {
    return apiService.put<EmployeeHealthProfile>(`${this.baseUrl}/profiles/${id}`, payload);
  }

  removeProfile(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/profiles/${id}`);
  }

  // ── Conditions ─────────────────────────────────────────────────────────────

  getConditions(profileId: string, onlyActive = false): Promise<EmployeeHealthCondition[]> {
    return apiService.get<EmployeeHealthCondition[]>(
      `${this.baseUrl}/profiles/${profileId}/conditions`,
      onlyActive ? { onlyActive: true } : undefined,
    );
  }

  addCondition(payload: EmployeeHealthConditionCreateRequest): Promise<EmployeeHealthCondition> {
    return apiService.post<EmployeeHealthCondition>(`${this.baseUrl}/conditions`, payload);
  }

  updateCondition(
    id: string,
    payload: EmployeeHealthConditionUpdateRequest,
  ): Promise<EmployeeHealthCondition> {
    return apiService.put<EmployeeHealthCondition>(`${this.baseUrl}/conditions/${id}`, payload);
  }

  removeCondition(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/conditions/${id}`);
  }

  // ── Allergies ──────────────────────────────────────────────────────────────

  getAllergies(profileId: string): Promise<EmployeeAllergy[]> {
    return apiService.get<EmployeeAllergy[]>(`${this.baseUrl}/profiles/${profileId}/allergies`);
  }

  addAllergy(payload: EmployeeAllergyCreateRequest): Promise<EmployeeAllergy> {
    return apiService.post<EmployeeAllergy>(`${this.baseUrl}/allergies`, payload);
  }

  updateAllergy(id: string, payload: EmployeeAllergyUpdateRequest): Promise<EmployeeAllergy> {
    return apiService.put<EmployeeAllergy>(`${this.baseUrl}/allergies/${id}`, payload);
  }

  removeAllergy(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/allergies/${id}`);
  }

  // ── Examinations ───────────────────────────────────────────────────────────

  getExamsByProfile(profileId: string): Promise<EmployeeMedicalExamSummary[]> {
    return apiService.get<EmployeeMedicalExamSummary[]>(
      `${this.baseUrl}/profiles/${profileId}/exams`,
    );
  }

  getExam(id: string): Promise<EmployeeMedicalExam> {
    return apiService.get<EmployeeMedicalExam>(`${this.baseUrl}/exams/${id}`);
  }

  /** Examinations falling due inside the window — the recall work list. */
  getExamsDue(daysAhead = 30): Promise<EmployeeMedicalExamSummary[]> {
    return apiService.get<EmployeeMedicalExamSummary[]>(`${this.baseUrl}/exams/due`, {
      daysAhead,
    });
  }

  createExam(payload: EmployeeMedicalExamCreateRequest): Promise<EmployeeMedicalExam> {
    return apiService.post<EmployeeMedicalExam>(`${this.baseUrl}/exams`, payload);
  }

  updateExam(
    id: string,
    payload: EmployeeMedicalExamUpdateRequest,
  ): Promise<EmployeeMedicalExam> {
    return apiService.put<EmployeeMedicalExam>(`${this.baseUrl}/exams/${id}`, payload);
  }

  removeExam(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/exams/${id}`);
  }

  // ── Examination documents ──────────────────────────────────────────────────

  getExamDocuments(examId: string): Promise<EmployeeMedicalExamDocument[]> {
    return apiService.get<EmployeeMedicalExamDocument[]>(
      `${this.baseUrl}/exams/${examId}/documents`,
    );
  }

  /**
   * Uploads through the controlled gate (scan + central-DMS registration).
   *
   * Note the route takes no id — `examId` travels as a FORM FIELD, unlike most HR upload
   * endpoints where the parent is in the path.
   */
  uploadExamDocument(
    examId: string,
    file: File,
    description?: string | null,
  ): Promise<EmployeeMedicalExamDocument> {
    return hrDocumentService.upload<EmployeeMedicalExamDocument>(
      `${this.baseUrl}/exam-documents`,
      file,
      { examId, description },
    );
  }

  /** Streams the file through the authorizing endpoint — stored paths are not URLs. */
  downloadExamDocument(document: EmployeeMedicalExamDocument): Promise<void> {
    return hrDocumentService.download(
      `${this.baseUrl}/exam-documents/${document.id}/download`,
      document.fileName,
    );
  }

  removeExamDocument(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/exam-documents/${id}`);
  }
}

export const medicalHealthService = new MedicalHealthService();
