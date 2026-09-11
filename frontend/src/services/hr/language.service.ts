import { apiService } from '../api.service';
import type { CreateLanguage, Language, UpdateLanguage } from '@/types/hr/language';

/** The language catalogue (round 3, lane C1). The anonymous careers form reads `api/public/catalogue/languages` instead. */
class LanguageService {
  private readonly baseUrl = '/hr/languages';

  getAll() {
    return apiService.get<Language[]>(this.baseUrl);
  }

  getActive() {
    return apiService.get<Language[]>(`${this.baseUrl}?activeOnly=true`);
  }

  getById(id: string) {
    return apiService.get<Language>(`${this.baseUrl}/${id}`);
  }

  create(payload: CreateLanguage) {
    return apiService.post<Language>(this.baseUrl, payload);
  }

  update(id: string, payload: UpdateLanguage) {
    return apiService.put<Language>(`${this.baseUrl}/${id}`, payload);
  }

  deactivate(id: string) {
    return apiService.patch<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }

  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const languageService = new LanguageService();
