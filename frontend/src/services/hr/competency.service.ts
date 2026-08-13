import { apiService } from '../api.service';

// Mirrors CompetencyLookupDto (ErpSystem.Core.DTOs.HR.SuccessionPlanDTOs) — the minimal shape
// needed by pickers. Full competency CRUD belongs to its own area (Job Analysis / Competency),
// not yet built; this is just enough to link a competency onto a training program.
export interface CompetencyLookup {
  id: string;
  name: string;
  code: string;
  competencyCategory: string;
  proficiencyScaleMax: number;
}

class CompetencyService {
  private readonly baseUrl = '/competencies';

  getLookup(): Promise<CompetencyLookup[]> {
    return apiService.get<CompetencyLookup[]>(`${this.baseUrl}/lookup`);
  }
}

export const competencyService = new CompetencyService();
