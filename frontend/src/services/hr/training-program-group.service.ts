import { apiService } from '../api.service';
import type { TrainingProgramGroup, TrainingProgramGroupRequest } from '@/types/hr/training';

/**
 * CRUD for training program groups — an optional curriculum cluster above Program.
 * Backend route: api/training-program-groups.
 */
class TrainingProgramGroupService {
  private readonly baseUrl = '/training-program-groups';

  getAll(activeOnly = false): Promise<TrainingProgramGroup[]> {
    return apiService.get<TrainingProgramGroup[]>(this.baseUrl, { activeOnly });
  }

  getById(id: string): Promise<TrainingProgramGroup> {
    return apiService.get<TrainingProgramGroup>(`${this.baseUrl}/${id}`);
  }

  create(data: TrainingProgramGroupRequest): Promise<TrainingProgramGroup> {
    return apiService.post<TrainingProgramGroup>(this.baseUrl, data);
  }

  update(id: string, data: TrainingProgramGroupRequest): Promise<TrainingProgramGroup> {
    return apiService.put<TrainingProgramGroup>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const trainingProgramGroupService = new TrainingProgramGroupService();
