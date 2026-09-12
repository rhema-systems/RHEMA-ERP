import { apiService } from '../api.service';
import type { TrainingCategoryOption, TrainingCategoryOptionRequest } from '@/types/hr/training';

/**
 * CRUD for training categories — a user-configurable classification for training programs.
 * Backend route: api/training-category-options.
 */
class TrainingCategoryService {
  private readonly baseUrl = '/training-category-options';

  getAll(activeOnly = false): Promise<TrainingCategoryOption[]> {
    return apiService.get<TrainingCategoryOption[]>(this.baseUrl, { activeOnly });
  }

  getById(id: string): Promise<TrainingCategoryOption> {
    return apiService.get<TrainingCategoryOption>(`${this.baseUrl}/${id}`);
  }

  create(data: TrainingCategoryOptionRequest): Promise<TrainingCategoryOption> {
    return apiService.post<TrainingCategoryOption>(this.baseUrl, data);
  }

  update(id: string, data: TrainingCategoryOptionRequest): Promise<TrainingCategoryOption> {
    return apiService.put<TrainingCategoryOption>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const trainingCategoryService = new TrainingCategoryService();
