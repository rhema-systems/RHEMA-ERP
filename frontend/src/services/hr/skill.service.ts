import { apiService } from '../api.service';
import type { Skill, SkillRequest } from '@/types/hr/skill';
import type { SkillCertification } from '@/types/hr/certification';

/**
 * CRUD for skills (an HR reference lookup, optionally grouped by category).
 * Backend route: api/hr/skills. Lists come back ordered by name.
 */
class SkillService {
  private readonly baseUrl = '/hr/skills';

  getAll(): Promise<Skill[]> {
    return apiService.get<Skill[]>(this.baseUrl);
  }

  getActive(): Promise<Skill[]> {
    return apiService.get<Skill[]>(`${this.baseUrl}/active`);
  }

  getCategories(): Promise<string[]> {
    return apiService.get<string[]>(`${this.baseUrl}/categories`);
  }

  getById(id: string): Promise<Skill> {
    return apiService.get<Skill>(`${this.baseUrl}/${id}`);
  }

  /** The credentials that evidence a skill (round 2, lane C2). Written with the skill save. */
  getCertifications(id: string): Promise<SkillCertification[]> {
    return apiService.get<SkillCertification[]>(`${this.baseUrl}/${id}/certifications`);
  }

  create(data: SkillRequest): Promise<Skill> {
    return apiService.post<Skill>(this.baseUrl, data);
  }

  update(id: string, data: SkillRequest): Promise<Skill> {
    return apiService.put<Skill>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const skillService = new SkillService();
