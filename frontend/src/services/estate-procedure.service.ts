import { compatibleApiService as apiService } from './compatibleApiService';

export interface EstateProcedure {
  title: string;
  entityType: string;
  source: string;
  summary: string;
  icon: string;
  stageCount: number;
  accent: string;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

class EstateProcedureService {
  async getProcedures(): Promise<EstateProcedure[]> {
    const response = await apiService.get<ApiResponse<EstateProcedure[]>>('/estate/procedures');
    return response.data || [];
  }
}

export const estateProcedureService = new EstateProcedureService();
