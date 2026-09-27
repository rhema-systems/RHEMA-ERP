import { compatibleApiService as apiService } from './compatibleApiService';
import {
  resolveProcedureWorkspaceType,
  type ProcedureWorkspaceType,
} from '@/lib/procedure-workspace';

export interface EstateProcedure {
  title: string;
  entityType: string;
  icon: string;
  stageCount: number;
  accent: string;
  workspaceType?: ProcedureWorkspaceType;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export function findEstateProcedure(entityType: string, procedures: EstateProcedure[]): EstateProcedure | null {
  return procedures.find((item) => item.entityType.toLowerCase() === entityType.toLowerCase()) ?? null;
}

class EstateProcedureService {
  async getProcedures(): Promise<EstateProcedure[]> {
    const response = await apiService.get<ApiResponse<EstateProcedure[]>>('/estate/procedures');
    return (response.data || []).map((procedure) => ({
      ...procedure,
      workspaceType: resolveProcedureWorkspaceType(
        procedure.entityType,
        procedure.workspaceType
      ),
    }));
  }
}

export const estateProcedureService = new EstateProcedureService();
