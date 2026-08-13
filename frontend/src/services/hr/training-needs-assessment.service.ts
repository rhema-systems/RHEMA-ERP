import { apiService } from '../api.service';
import type {
  TrainingNeedsAssessment,
  TrainingNeedsAssessmentSummary,
  CreateTrainingNeedsAssessmentRequest,
  UpdateTrainingNeedsAssessmentRequest,
  BulkCreateTrainingNeedsAssessmentRequest,
  BulkNeedsAssessmentResult,
  TrainingNeedsAssessmentProgram,
  TrainingNeedsAssessmentProgramCreateRequest,
  TrainingNeedsAssessmentSkill,
  TrainingNeedsAssessmentSkillCreateRequest,
} from '@/types/hr/training';
import type { PagedResult } from '@/types/hr/common';

/**
 * CRUD for training needs assessments plus their recommended-program and skill-gap
 * sub-resources (add/remove only — no update endpoint on either).
 * Backend route: api/training-needs-assessments.
 */
class TrainingNeedsAssessmentService {
  private readonly baseUrl = '/training-needs-assessments';

  getAll(): Promise<TrainingNeedsAssessmentSummary[]> {
    return apiService.get<TrainingNeedsAssessmentSummary[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<TrainingNeedsAssessmentSummary>> {
    return apiService.get<PagedResult<TrainingNeedsAssessmentSummary>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getById(id: string): Promise<TrainingNeedsAssessment> {
    return apiService.get<TrainingNeedsAssessment>(`${this.baseUrl}/${id}`);
  }

  getByEmployeeId(employeeId: string): Promise<TrainingNeedsAssessmentSummary[]> {
    return apiService.get<TrainingNeedsAssessmentSummary[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  getByYear(year: number): Promise<TrainingNeedsAssessmentSummary[]> {
    return apiService.get<TrainingNeedsAssessmentSummary[]>(`${this.baseUrl}/year/${year}`);
  }

  getUnfulfilled(): Promise<TrainingNeedsAssessmentSummary[]> {
    return apiService.get<TrainingNeedsAssessmentSummary[]>(`${this.baseUrl}/unfulfilled`);
  }

  getByPriority(priority: string): Promise<TrainingNeedsAssessmentSummary[]> {
    return apiService.get<TrainingNeedsAssessmentSummary[]>(`${this.baseUrl}/priority/${priority}`);
  }

  create(data: CreateTrainingNeedsAssessmentRequest): Promise<TrainingNeedsAssessment> {
    return apiService.post<TrainingNeedsAssessment>(this.baseUrl, data);
  }

  bulkCreate(data: BulkCreateTrainingNeedsAssessmentRequest): Promise<BulkNeedsAssessmentResult> {
    return apiService.post<BulkNeedsAssessmentResult>(`${this.baseUrl}/bulk`, data);
  }

  update(id: string, data: UpdateTrainingNeedsAssessmentRequest): Promise<TrainingNeedsAssessment> {
    return apiService.put<TrainingNeedsAssessment>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Recommended programs (add/remove only) ───────────────────────────────

  getRecommendedPrograms(assessmentId: string): Promise<TrainingNeedsAssessmentProgram[]> {
    return apiService.get<TrainingNeedsAssessmentProgram[]>(`${this.baseUrl}/${assessmentId}/programs`);
  }

  addRecommendedProgram(
    assessmentId: string,
    data: Omit<TrainingNeedsAssessmentProgramCreateRequest, 'assessmentId'>,
  ): Promise<TrainingNeedsAssessmentProgram> {
    return apiService.post<TrainingNeedsAssessmentProgram>(`${this.baseUrl}/${assessmentId}/programs`, {
      ...data,
      assessmentId,
    });
  }

  removeRecommendedProgram(programId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/programs/${programId}`);
  }

  // ── Skill gaps (add/remove only) ──────────────────────────────────────────

  getSkillGaps(assessmentId: string): Promise<TrainingNeedsAssessmentSkill[]> {
    return apiService.get<TrainingNeedsAssessmentSkill[]>(`${this.baseUrl}/${assessmentId}/skill-gaps`);
  }

  addSkillGap(
    assessmentId: string,
    data: Omit<TrainingNeedsAssessmentSkillCreateRequest, 'assessmentId'>,
  ): Promise<TrainingNeedsAssessmentSkill> {
    return apiService.post<TrainingNeedsAssessmentSkill>(`${this.baseUrl}/${assessmentId}/skill-gaps`, {
      ...data,
      assessmentId,
    });
  }

  removeSkillGap(skillGapId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/skill-gaps/${skillGapId}`);
  }
}

export const trainingNeedsAssessmentService = new TrainingNeedsAssessmentService();
