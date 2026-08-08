import { apiService } from '../api.service';
import type { Country, CreateCountryRequest, UpdateCountryRequest } from '@/types/hr/country';

/**
 * CRUD for countries (an HR reference lookup). Backend route: api/Country.
 * GET / returns active only (for pickers); GET /all includes inactive (management).
 */
class CountryService {
  private readonly baseUrl = '/Country';

  getActive(): Promise<Country[]> {
    return apiService.get<Country[]>(this.baseUrl);
  }

  getAll(): Promise<Country[]> {
    return apiService.get<Country[]>(`${this.baseUrl}/all`);
  }

  getById(id: string): Promise<Country> {
    return apiService.get<Country>(`${this.baseUrl}/${id}`);
  }

  create(data: CreateCountryRequest): Promise<Country> {
    return apiService.post<Country>(this.baseUrl, data);
  }

  update(id: string, data: UpdateCountryRequest): Promise<Country> {
    return apiService.put<Country>(`${this.baseUrl}/${id}`, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const countryService = new CountryService();
