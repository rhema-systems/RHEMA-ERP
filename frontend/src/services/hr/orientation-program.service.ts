import { apiService } from '../api.service';
import type {
  OrientationProgram,
  OrientationProgramSummary,
  OrientationProgramCreateRequest,
  OrientationProgramUpdateRequest,
  OrientationProgramStatus,
  OrientationProgramType,
  ChangeOrientationProgramStatusRequest,
  OrientationModule,
  OrientationModuleCreateRequest,
  OrientationModuleUpdateRequest,
  OrientationContentItem,
  OrientationContentItemCreateRequest,
  OrientationContentItemUpdateRequest,
  OrientationPrerequisite,
  OrientationPrerequisiteCreateRequest,
  OrientationAudienceRule,
  OrientationAudienceRuleCreateRequest,
  OrientationAudienceRuleUpdateRequest,
  OrientationAssessmentQuestion,
  OrientationAssessmentQuestionCreateRequest,
  OrientationAssessmentQuestionUpdateRequest,
} from '@/types/hr/orientation';
import type { PagedResult } from '@/types/hr/common';

/**
 * The orientation catalogue: programs plus their modules, content items, prerequisites, audience
 * rules and assessment questions. Backend route: api/orientation-programs.
 *
 * ⚠ HR-only end to end (class-level role gate on the controller). This is the AUTHORING surface —
 * questions come back with `isCorrect` populated. Never render these DTOs to a participant; the
 * participant reads their paper through `employeeOrientationService.getAssessment`, which strips
 * the answer key server-side.
 */
class OrientationProgramService {
  private readonly baseUrl = '/orientation-programs';

  // ── Programs ──────────────────────────────────────────────────────────────

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<OrientationProgramSummary>> {
    return apiService.get<PagedResult<OrientationProgramSummary>>(this.baseUrl, {
      pageNumber,
      pageSize,
    });
  }

  getAll(): Promise<OrientationProgramSummary[]> {
    return apiService.get<OrientationProgramSummary[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<OrientationProgram> {
    return apiService.get<OrientationProgram>(`${this.baseUrl}/${id}`);
  }

  getByCode(programCode: string): Promise<OrientationProgram | null> {
    return apiService.get<OrientationProgram | null>(
      `${this.baseUrl}/code/${encodeURIComponent(programCode)}`,
    );
  }

  getByStatus(status: OrientationProgramStatus): Promise<OrientationProgramSummary[]> {
    return apiService.get<OrientationProgramSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getByCategory(categoryId: string): Promise<OrientationProgramSummary[]> {
    return apiService.get<OrientationProgramSummary[]>(`${this.baseUrl}/category/${categoryId}`);
  }

  getByType(programType: OrientationProgramType): Promise<OrientationProgramSummary[]> {
    return apiService.get<OrientationProgramSummary[]>(`${this.baseUrl}/type/${programType}`);
  }

  getActive(): Promise<OrientationProgramSummary[]> {
    return apiService.get<OrientationProgramSummary[]>(`${this.baseUrl}/active`);
  }

  getByOrganizationUnit(organizationUnitId: string): Promise<OrientationProgramSummary[]> {
    return apiService.get<OrientationProgramSummary[]>(
      `${this.baseUrl}/organization-unit/${organizationUnitId}`,
    );
  }

  create(data: OrientationProgramCreateRequest): Promise<OrientationProgram> {
    return apiService.post<OrientationProgram>(this.baseUrl, data);
  }

  update(id: string, data: OrientationProgramUpdateRequest): Promise<OrientationProgram> {
    return apiService.put<OrientationProgram>(`${this.baseUrl}/${id}`, data);
  }

  changeStatus(id: string, data: ChangeOrientationProgramStatusRequest): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/status`, data);
  }

  /** Refused with 422 while the program is Active — suspend or retire it first. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Modules ───────────────────────────────────────────────────────────────

  getModules(programId: string): Promise<OrientationModule[]> {
    return apiService.get<OrientationModule[]>(`${this.baseUrl}/${programId}/modules`);
  }

  addModule(programId: string, data: OrientationModuleCreateRequest): Promise<OrientationModule> {
    return apiService.post<OrientationModule>(`${this.baseUrl}/${programId}/modules`, data);
  }

  updateModule(
    moduleId: string,
    data: OrientationModuleUpdateRequest,
  ): Promise<OrientationModule> {
    return apiService.put<OrientationModule>(`${this.baseUrl}/modules/${moduleId}`, data);
  }

  removeModule(moduleId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/modules/${moduleId}`);
  }

  // ── Content items ─────────────────────────────────────────────────────────

  getContentItems(moduleId: string): Promise<OrientationContentItem[]> {
    return apiService.get<OrientationContentItem[]>(
      `${this.baseUrl}/modules/${moduleId}/content-items`,
    );
  }

  addContentItem(
    moduleId: string,
    data: OrientationContentItemCreateRequest,
  ): Promise<OrientationContentItem> {
    return apiService.post<OrientationContentItem>(
      `${this.baseUrl}/modules/${moduleId}/content-items`,
      data,
    );
  }

  updateContentItem(
    itemId: string,
    data: OrientationContentItemUpdateRequest,
  ): Promise<OrientationContentItem> {
    return apiService.put<OrientationContentItem>(`${this.baseUrl}/content-items/${itemId}`, data);
  }

  removeContentItem(itemId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/content-items/${itemId}`);
  }

  // ── Prerequisites ─────────────────────────────────────────────────────────

  getPrerequisites(programId: string): Promise<OrientationPrerequisite[]> {
    return apiService.get<OrientationPrerequisite[]>(`${this.baseUrl}/${programId}/prerequisites`);
  }

  addPrerequisite(
    programId: string,
    data: OrientationPrerequisiteCreateRequest,
  ): Promise<OrientationPrerequisite> {
    return apiService.post<OrientationPrerequisite>(
      `${this.baseUrl}/${programId}/prerequisites`,
      data,
    );
  }

  removePrerequisite(prerequisiteId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/prerequisites/${prerequisiteId}`);
  }

  // ── Audience rules ────────────────────────────────────────────────────────

  getAudienceRules(programId: string): Promise<OrientationAudienceRule[]> {
    return apiService.get<OrientationAudienceRule[]>(`${this.baseUrl}/${programId}/audience-rules`);
  }

  addAudienceRule(
    programId: string,
    data: OrientationAudienceRuleCreateRequest,
  ): Promise<OrientationAudienceRule> {
    return apiService.post<OrientationAudienceRule>(
      `${this.baseUrl}/${programId}/audience-rules`,
      data,
    );
  }

  updateAudienceRule(
    ruleId: string,
    data: OrientationAudienceRuleUpdateRequest,
  ): Promise<OrientationAudienceRule> {
    return apiService.put<OrientationAudienceRule>(`${this.baseUrl}/audience-rules/${ruleId}`, data);
  }

  removeAudienceRule(ruleId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/audience-rules/${ruleId}`);
  }

  // ── Assessment questions ──────────────────────────────────────────────────

  getQuestions(programId: string): Promise<OrientationAssessmentQuestion[]> {
    return apiService.get<OrientationAssessmentQuestion[]>(`${this.baseUrl}/${programId}/questions`);
  }

  addQuestion(
    programId: string,
    data: OrientationAssessmentQuestionCreateRequest,
  ): Promise<OrientationAssessmentQuestion> {
    return apiService.post<OrientationAssessmentQuestion>(
      `${this.baseUrl}/${programId}/questions`,
      data,
    );
  }

  /**
   * ⚠ Replace-set: `options` is the WHOLE option set for the question. Omitting an existing option
   * deletes it — send the full list every time, not just the ones that changed.
   */
  updateQuestion(
    questionId: string,
    data: OrientationAssessmentQuestionUpdateRequest,
  ): Promise<OrientationAssessmentQuestion> {
    return apiService.put<OrientationAssessmentQuestion>(
      `${this.baseUrl}/questions/${questionId}`,
      data,
    );
  }

  removeQuestion(questionId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/questions/${questionId}`);
  }
}

export const orientationProgramService = new OrientationProgramService();
export default orientationProgramService;
