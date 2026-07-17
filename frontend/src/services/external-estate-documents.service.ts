import { compatibleApiService as apiService } from './compatibleApiService';

export interface ExternalEstateDocument {
  id: string;
  documentReference: string;
  title: string;
  sourceLabel: string;
  sourceEntityType?: string | null;
  sourceRecordReference?: string | null;
  lifecycleStatus: string;
  versionStatus: string;
  publishedAt?: string | null;
  createdAt: string;
  fileName?: string | null;
  repositoryPath?: string | null;
  renditionPath?: string | null;
  contentType?: string | null;
  version?: string | null;
  dispatchChannel?: string | null;
  dispatchReference?: string | null;
  dispatchedAt?: string | null;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

class ExternalEstateDocumentsService {
  async getMyDocuments(): Promise<ExternalEstateDocument[]> {
    const response = await apiService.get<ApiResponse<ExternalEstateDocument[]>>(
      '/estate/external/documents'
    );
    return response.data || [];
  }
}

export const externalEstateDocumentsService =
  new ExternalEstateDocumentsService();
