import { apiService } from '../api.service';
import type {
  LocationLevel,
  CreateLocationLevelRequest,
  UpdateLocationLevelRequest,
} from '@/types/hr/location';

/**
 * CRUD for location levels (the tiers of the location hierarchy).
 * Backend route: api/LocationLevel.
 */
class LocationLevelService {
  private readonly baseUrl = '/LocationLevel';

  getAll(): Promise<LocationLevel[]> {
    return apiService.get<LocationLevel[]>(this.baseUrl);
  }

  getByStructure(structureId: string): Promise<LocationLevel[]> {
    return apiService.get<LocationLevel[]>(`${this.baseUrl}/structure/${structureId}`);
  }

  getById(id: string): Promise<LocationLevel> {
    return apiService.get<LocationLevel>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateLocationLevelRequest): Promise<LocationLevel> {
    return apiService.post<LocationLevel>(this.baseUrl, data);
  }

  update(id: string, data: UpdateLocationLevelRequest): Promise<LocationLevel> {
    return apiService.put<LocationLevel>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

export const locationLevelService = new LocationLevelService();
