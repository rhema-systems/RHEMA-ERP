import { apiService } from './api.service';

/**
 * Compatible API service that provides axios-like interface for migrated services
 * This ensures backward compatibility while using the unified api.service.ts
 */
class CompatibleApiService {
  async get<T>(endpoint: string): Promise<T> {
    try {
      return await apiService.get<T>(endpoint);
    } catch (error) {
      console.error(`GET ${endpoint} failed:`, error);
      throw error;
    }
  }

  // Silent GET method - doesn't log errors to console (useful for expected 404s)
  async silentGet<T>(endpoint: string): Promise<T> {
    return await apiService.silentGet<T>(endpoint);
  }

  async post<T>(endpoint: string, data?: any): Promise<T> {
    try {
      return await apiService.post<T>(endpoint, data);
    } catch (error) {
      console.error(`POST ${endpoint} failed:`, error);
      throw error;
    }
  }

  async put<T>(endpoint: string, data?: any): Promise<T> {
    try {
      return await apiService.put<T>(endpoint, data);
    } catch (error) {
      console.error(`PUT ${endpoint} failed:`, error);
      throw error;
    }
  }

  async delete<T>(endpoint: string): Promise<T> {
    try {
      return await apiService.delete<T>(endpoint);
    } catch (error) {
      console.error(`DELETE ${endpoint} failed:`, error);
      throw error;
    }
  }
}

export const compatibleApiService = new CompatibleApiService();