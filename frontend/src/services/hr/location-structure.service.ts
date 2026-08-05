import { apiService } from '../api.service';
import type {
  LocationStructure,
  LocationStructureSummary,
  CreateLocationStructureRequest,
  UpdateLocationStructureRequest,
} from '@/types/hr/location';

/**
 * CRUD for location structures — the top of the location hierarchy
 * (Structure -> Levels -> Locations). Backend route: api/LocationStructure.
 */
class LocationStructureService {
  private readonly baseUrl = '/LocationStructure';

  getAll(): Promise<LocationStructure[]> {
    return apiService.get<LocationStructure[]>(this.baseUrl);
  }

  getSummary(): Promise<LocationStructureSummary[]> {
    return apiService.get<LocationStructureSummary[]>(`${this.baseUrl}/summary`);
  }

  getById(id: string): Promise<LocationStructure> {
    return apiService.get<LocationStructure>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateLocationStructureRequest): Promise<LocationStructure> {
    return apiService.post<LocationStructure>(this.baseUrl, data);
  }

  update(id: string, data: UpdateLocationStructureRequest): Promise<LocationStructure> {
    return apiService.put<LocationStructure>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<boolean> {
    return apiService.delete<boolean>(`${this.baseUrl}/${id}`);
  }

  setDefault(id: string): Promise<boolean> {
    return apiService.patch<boolean>(`${this.baseUrl}/${id}/set-default`);
  }
}

export const locationStructureService = new LocationStructureService();
