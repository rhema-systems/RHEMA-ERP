import { compatibleApiService as apiService } from './compatibleApiService';

export interface EstatePlotSettings {
  squareMetersPerPlot?: number | null;
}

interface ApiResponse<T> {
  success?: boolean;
  data?: T;
  message?: string;
}

class EstateSettingsService {
  async getPlotSettings(): Promise<EstatePlotSettings> {
    const response = await apiService.get<ApiResponse<EstatePlotSettings>>(
      '/estate/settings/plots'
    );
    return response.data || {};
  }

  async savePlotSettings(
    payload: EstatePlotSettings
  ): Promise<EstatePlotSettings> {
    const response = await apiService.put<ApiResponse<EstatePlotSettings>>(
      '/estate/settings/plots',
      payload
    );
    if (!response.data) {
      throw new Error(response.message || 'Unable to save Estate plot setup.');
    }
    return response.data;
  }
}

export const estateSettingsService = new EstateSettingsService();
