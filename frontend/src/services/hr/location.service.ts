import { apiService } from '../api.service';
import type {
  Location,
  CreateLocationRequest,
  UpdateLocationRequest,
} from '@/types/hr/location';

/**
 * CRUD for locations (physical work locations; hierarchical nodes of the
 * location structure). Backend route: api/Location.
 */
class LocationService {
  private readonly baseUrl = '/Location';

  getAll(): Promise<Location[]> {
    return apiService.get<Location[]>(this.baseUrl);
  }

  getByStructure(structureId: string): Promise<Location[]> {
    return apiService.get<Location[]>(`${this.baseUrl}/structure/${structureId}`);
  }

  getById(id: string): Promise<Location> {
    return apiService.get<Location>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateLocationRequest): Promise<Location> {
    return apiService.post<Location>(this.baseUrl, data);
  }

  update(id: string, data: UpdateLocationRequest): Promise<Location> {
    return apiService.put<Location>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }
}

export const locationService = new LocationService();
