import { apiService } from './api.service';

interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message?: string;
}

export interface FileUploadPolicyDto {
  category: string;
  isEnabled: boolean;
  maxFileSizeBytes?: number | null;
  maxTenantTotalBytes?: number | null;
  maxCategoryTotalBytes?: number | null;
  allowedExtensionsCsv?: string | null;
  allowedMimeTypesCsv?: string | null;
  requireVirusScan: boolean;
}

export interface FileUploadUsageByCategoryDto {
  category: string;
  bytes: number;
  files: number;
}

export interface FileUploadUsageDto {
  totalBytes: number;
  totalFiles: number;
  byCategory: FileUploadUsageByCategoryDto[];
}

export const fileUploadPolicyService = {
  async listPolicies(): Promise<FileUploadPolicyDto[]> {
    const res = await apiService.request<ApiEnvelope<FileUploadPolicyDto[]>>('/settings/file-uploads/policies', { method: 'GET' });
    return res.data || [];
  },

  async upsertPolicy(category: string, payload: Omit<FileUploadPolicyDto, 'category'> & { category?: string }): Promise<void> {
    await apiService.request<ApiEnvelope<unknown>>(`/settings/file-uploads/policies/${encodeURIComponent(category)}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async deletePolicy(category: string): Promise<void> {
    await apiService.request<ApiEnvelope<unknown>>(`/settings/file-uploads/policies/${encodeURIComponent(category)}`, { method: 'DELETE' });
  },

  async getUsage(): Promise<FileUploadUsageDto> {
    const res = await apiService.request<ApiEnvelope<FileUploadUsageDto>>('/settings/file-uploads/usage', { method: 'GET' });
    return (
      res.data || {
        totalBytes: 0,
        totalFiles: 0,
        byCategory: [],
      }
    );
  },
};

