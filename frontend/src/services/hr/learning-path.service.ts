import { apiService } from '../api.service';
import type {
  LearningPath,
  LearningPathSummary,
  LearningPathCreate,
  LearningPathUpdate,
  LearningPathProgram,
  LearningPathProgramCreate,
  LearningPathProgramUpdate,
  LearningPathSkill,
  LearningPathSkillCreate,
  LearningPathStatus,
  EmployeeLearningPath,
  EmployeeLearningPathSummary,
  EnrollmentListItem,
  EnrollEmployeeRequest,
  UpdateEnrollmentRequest,
  UpdateLearningPathStepRequest,
  StepDetailPage,
  TrainingStatusHistoryEntry,
} from '@/types/hr/learning-paths';

/**
 * Learning paths — an ordered curriculum — and the enrolments generated from them.
 * Backend route: api/learning-paths.
 *
 * Enrolling generates one step per programme in a single transaction, so an enrolment can never be
 * left step-less (which would pin its progress at 0% forever).
 */
class LearningPathService {
  private readonly baseUrl = '/learning-paths';

  // ── Path definitions ────────────────────────────────────────────────────────
  getAll(): Promise<LearningPathSummary[]> {
    return apiService.get<LearningPathSummary[]>(this.baseUrl);
  }

  getActive(): Promise<LearningPathSummary[]> {
    return apiService.get<LearningPathSummary[]>(`${this.baseUrl}/active`);
  }

  getByStatus(status: LearningPathStatus): Promise<LearningPathSummary[]> {
    return apiService.get<LearningPathSummary[]>(`${this.baseUrl}/status/${status}`);
  }

  getById(id: string): Promise<LearningPath> {
    return apiService.get<LearningPath>(`${this.baseUrl}/${id}`);
  }

  create(data: LearningPathCreate): Promise<LearningPath> {
    return apiService.post<LearningPath>(this.baseUrl, data);
  }

  update(id: string, data: LearningPathUpdate): Promise<LearningPath> {
    return apiService.put<LearningPath>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Sequenced programmes ────────────────────────────────────────────────────
  getPrograms(pathId: string): Promise<LearningPathProgram[]> {
    return apiService.get<LearningPathProgram[]>(`${this.baseUrl}/${pathId}/programs`);
  }

  addProgram(pathId: string, data: Omit<LearningPathProgramCreate, 'learningPathId'>): Promise<LearningPathProgram> {
    return apiService.post<LearningPathProgram>(`${this.baseUrl}/${pathId}/programs`, {
      ...data,
      learningPathId: pathId,
    });
  }

  updateProgram(programRowId: string, data: LearningPathProgramUpdate): Promise<LearningPathProgram> {
    return apiService.put<LearningPathProgram>(`${this.baseUrl}/programs/${programRowId}`, {
      id: programRowId,
      ...data,
    });
  }

  removeProgram(programRowId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/programs/${programRowId}`);
  }

  // ── Target skills ───────────────────────────────────────────────────────────
  getSkills(pathId: string): Promise<LearningPathSkill[]> {
    return apiService.get<LearningPathSkill[]>(`${this.baseUrl}/${pathId}/skills`);
  }

  addSkill(pathId: string, data: Omit<LearningPathSkillCreate, 'learningPathId'>): Promise<LearningPathSkill> {
    return apiService.post<LearningPathSkill>(`${this.baseUrl}/${pathId}/skills`, {
      ...data,
      learningPathId: pathId,
    });
  }

  removeSkill(skillRowId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/skills/${skillRowId}`);
  }

  // ── Enrolments ──────────────────────────────────────────────────────────────
  /** Org-wide list, richer than the summary — carries where each learner sits. */
  getAllEnrollments(): Promise<EnrollmentListItem[]> {
    return apiService.get<EnrollmentListItem[]>(`${this.baseUrl}/enrollments`);
  }

  getEnrollmentsForPath(pathId: string): Promise<EmployeeLearningPathSummary[]> {
    return apiService.get<EmployeeLearningPathSummary[]>(`${this.baseUrl}/${pathId}/enrollments`);
  }

  /** The caller's own enrolments — token-derived, the "My Learning" read. */
  getMyEnrollments(): Promise<EmployeeLearningPathSummary[]> {
    return apiService.get<EmployeeLearningPathSummary[]>(`${this.baseUrl}/enrollments/mine`);
  }

  getEnrollmentsForEmployee(employeeId: string): Promise<EmployeeLearningPathSummary[]> {
    return apiService.get<EmployeeLearningPathSummary[]>(
      `${this.baseUrl}/enrollments/employee/${employeeId}`,
    );
  }

  getEnrollmentById(enrollmentId: string): Promise<EmployeeLearningPath> {
    return apiService.get<EmployeeLearningPath>(`${this.baseUrl}/enrollments/${enrollmentId}`);
  }

  enroll(data: EnrollEmployeeRequest): Promise<EmployeeLearningPath> {
    return apiService.post<EmployeeLearningPath>(`${this.baseUrl}/enroll`, data);
  }

  updateEnrollment(enrollmentId: string, data: Omit<UpdateEnrollmentRequest, 'id'>): Promise<EmployeeLearningPath> {
    return apiService.put<EmployeeLearningPath>(`${this.baseUrl}/enrollments/${enrollmentId}`, {
      id: enrollmentId,
      ...data,
    });
  }

  removeEnrollment(enrollmentId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/enrollments/${enrollmentId}`);
  }

  // ── Steps ───────────────────────────────────────────────────────────────────
  /**
   * Completing or reopening a step.
   *
   * The server enforces who may call this and on what basis: a learner needs attendance or a
   * completion record behind the step, while HR may record one without either against a mandatory
   * `reason`. Both rejections come back as 422 with a message meant to be shown as-is.
   */
  updateStep(stepId: string, data: Omit<UpdateLearningPathStepRequest, 'id'>): Promise<any> {
    return apiService.put(`${this.baseUrl}/enrollments/steps/${stepId}`, { id: stepId, ...data });
  }

  /** How this step's completion was arrived at — evidenced, or recorded by HR and why. */
  getStepHistory(stepId: string): Promise<TrainingStatusHistoryEntry[]> {
    return apiService.get<TrainingStatusHistoryEntry[]>('/training-status-history', {
      entityType: 'EmployeeLearningPathStep',
      entityId: stepId,
    });
  }

  /** Returns the recalculated enrolment, so the caller sees the new percentage directly. */
  recalculateProgress(enrollmentId: string): Promise<EmployeeLearningPath> {
    return apiService.post<EmployeeLearningPath>(
      `${this.baseUrl}/enrollments/${enrollmentId}/recalculate-progress`,
      {},
    );
  }

  /** Purpose-built read model for the learner's step page — programme, materials, schedules, own records. */
  getStepDetail(stepId: string): Promise<StepDetailPage> {
    return apiService.get<StepDetailPage>(`${this.baseUrl}/steps/${stepId}/detail`);
  }
}

export const learningPathService = new LearningPathService();
