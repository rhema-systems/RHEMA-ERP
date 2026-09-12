import { apiService } from '../api.service';
import type {
  TrainerProfile,
  TrainerProfileSummary,
  TrainerProfileRequest,
  TrainerSkill,
  TrainerSkillCreateRequest,
  TrainerSkillUpdateRequest,
  TrainerAvailability,
  TrainerAvailabilityCreateRequest,
  TrainerAvailabilityUpdateRequest,
} from '@/types/hr/training';

/**
 * CRUD for trainer profiles (internal or external) plus their skills and availability
 * sub-resources. Backend route: api/trainers.
 */
class TrainerService {
  private readonly baseUrl = '/trainers';

  getAll(): Promise<TrainerProfileSummary[]> {
    return apiService.get<TrainerProfileSummary[]>(this.baseUrl);
  }

  getActive(): Promise<TrainerProfileSummary[]> {
    return apiService.get<TrainerProfileSummary[]>(`${this.baseUrl}/active`);
  }

  getById(id: string): Promise<TrainerProfile> {
    return apiService.get<TrainerProfile>(`${this.baseUrl}/${id}`);
  }

  getByVendorId(vendorId: string): Promise<TrainerProfileSummary[]> {
    return apiService.get<TrainerProfileSummary[]>(`${this.baseUrl}/vendor/${vendorId}`);
  }

  create(data: TrainerProfileRequest): Promise<TrainerProfile> {
    return apiService.post<TrainerProfile>(this.baseUrl, data);
  }

  update(id: string, data: TrainerProfileRequest): Promise<TrainerProfile> {
    return apiService.put<TrainerProfile>(`${this.baseUrl}/${id}`, { id, ...data });
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  // ── Skills ────────────────────────────────────────────────────────────────

  getSkills(trainerId: string): Promise<TrainerSkill[]> {
    return apiService.get<TrainerSkill[]>(`${this.baseUrl}/${trainerId}/skills`);
  }

  addSkill(trainerId: string, data: Omit<TrainerSkillCreateRequest, 'trainerProfileId'>): Promise<TrainerSkill> {
    return apiService.post<TrainerSkill>(`${this.baseUrl}/${trainerId}/skills`, {
      ...data,
      trainerProfileId: trainerId,
    });
  }

  updateSkill(skillId: string, data: TrainerSkillUpdateRequest): Promise<TrainerSkill> {
    return apiService.put<TrainerSkill>(`${this.baseUrl}/skills/${skillId}`, { id: skillId, ...data });
  }

  removeSkill(skillId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/skills/${skillId}`);
  }

  // ── Availability ──────────────────────────────────────────────────────────

  getAvailability(trainerId: string): Promise<TrainerAvailability[]> {
    return apiService.get<TrainerAvailability[]>(`${this.baseUrl}/${trainerId}/availability`);
  }

  addAvailability(
    trainerId: string,
    data: Omit<TrainerAvailabilityCreateRequest, 'trainerProfileId'>,
  ): Promise<TrainerAvailability> {
    return apiService.post<TrainerAvailability>(`${this.baseUrl}/${trainerId}/availability`, {
      ...data,
      trainerProfileId: trainerId,
    });
  }

  updateAvailability(availabilityId: string, data: TrainerAvailabilityUpdateRequest): Promise<TrainerAvailability> {
    return apiService.put<TrainerAvailability>(`${this.baseUrl}/availability/${availabilityId}`, {
      id: availabilityId,
      ...data,
    });
  }

  removeAvailability(availabilityId: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/availability/${availabilityId}`);
  }
}

export const trainerService = new TrainerService();
