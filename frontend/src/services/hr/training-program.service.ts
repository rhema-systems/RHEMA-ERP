import { apiService } from '../api.service';
import type {
  TrainingProgram,
  TrainingProgramSummary,
  TrainingProgramCreateRequest,
  TrainingProgramUpdateRequest,
  TrainingMaterial,
  TrainingMaterialCreateRequest,
  TrainingMaterialUpdateRequest,
  TrainingProgramCompetency,
  TrainingProgramCompetencyCreateRequest,
  TrainingProgramSkill,
  TrainingProgramSkillCreateRequest,
} from '@/types/hr/training';
import type { PagedResult } from '@/types/hr/common';

/**
 * CRUD for the training program catalog plus its materials/competencies/skills sub-resources.
 * Backend route: api/training-programs.
 */
class TrainingProgramService {
  private readonly baseUrl = '/training-programs';

  getAll(): Promise<TrainingProgramSummary[]> {
    return apiService.get<TrainingProgramSummary[]>(this.baseUrl);
  }

  getPaged(pageNumber = 1, pageSize = 20): Promise<PagedResult<TrainingProgramSummary>> {
    return apiService.get<PagedResult<TrainingProgramSummary>>(`${this.baseUrl}/paged`, {
      pageNumber,
      pageSize,
    });
  }

  getActive(): Promise<TrainingProgramSummary[]> {
    return apiService.get<TrainingProgramSummary[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<TrainingProgram> {
    return apiService.get<TrainingProgram>(`${this.baseUrl}/${id}`);
  }

  create(data: TrainingProgramCreateRequest): Promise<TrainingProgram> {
    return apiService.post<TrainingProgram>(this.baseUrl, data);
  }

  update(id: string, data: TrainingProgramUpdateRequest): Promise<TrainingProgram> {
    return apiService.put<TrainingProgram>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Materials ─────────────────────────────────────────────────────────────

  getMaterials(programId: string): Promise<TrainingMaterial[]> {
    return apiService.get<TrainingMaterial[]>(`${this.baseUrl}/${programId}/materials`);
  }

  addMaterial(programId: string, data: Omit<TrainingMaterialCreateRequest, 'programId'>): Promise<TrainingMaterial> {
    return apiService.post<TrainingMaterial>(`${this.baseUrl}/${programId}/materials`, {
      ...data,
      programId,
    });
  }

  updateMaterial(materialId: string, data: TrainingMaterialUpdateRequest): Promise<TrainingMaterial> {
    return apiService.put<TrainingMaterial>(`${this.baseUrl}/materials/${materialId}`, {
      id: materialId,
      ...data,
    });
  }

  removeMaterial(materialId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/materials/${materialId}`);
  }

  // ── Competencies (add/remove only — no update endpoint) ──────────────────

  getCompetencies(programId: string): Promise<TrainingProgramCompetency[]> {
    return apiService.get<TrainingProgramCompetency[]>(`${this.baseUrl}/${programId}/competencies`);
  }

  addCompetency(
    programId: string,
    data: Omit<TrainingProgramCompetencyCreateRequest, 'programId'>,
  ): Promise<TrainingProgramCompetency> {
    return apiService.post<TrainingProgramCompetency>(`${this.baseUrl}/${programId}/competencies`, {
      ...data,
      programId,
    });
  }

  removeCompetency(programCompetencyId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/competencies/${programCompetencyId}`);
  }

  // ── Skills (add/remove only — no update endpoint) ────────────────────────

  getSkills(programId: string): Promise<TrainingProgramSkill[]> {
    return apiService.get<TrainingProgramSkill[]>(`${this.baseUrl}/${programId}/skills`);
  }

  addSkill(
    programId: string,
    data: Omit<TrainingProgramSkillCreateRequest, 'programId'>,
  ): Promise<TrainingProgramSkill> {
    return apiService.post<TrainingProgramSkill>(`${this.baseUrl}/${programId}/skills`, {
      ...data,
      programId,
    });
  }

  removeSkill(programSkillId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/skills/${programSkillId}`);
  }
}

export const trainingProgramService = new TrainingProgramService();
