import { apiService } from './api.service';

type ApiEnvelope<T> = { success: boolean; data: T; message?: string };

export interface EhcRootCauseCode {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface CreateEhcRootCauseCodeRequest {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export const ehcRootCauseAdminService = {
  async list(includeDeleted = false): Promise<EhcRootCauseCode[]> {
    const qs = new URLSearchParams({ includeDeleted: String(includeDeleted) });
    const res = await apiService.request<ApiEnvelope<EhcRootCauseCode[]>>(`/ehc/admin/root-causes?${qs.toString()}`, { method: 'GET' });
    return res.data ?? [];
  },

  async create(payload: CreateEhcRootCauseCodeRequest): Promise<EhcRootCauseCode> {
    const res = await apiService.request<ApiEnvelope<EhcRootCauseCode>>('/ehc/admin/root-causes', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return res.data as any;
  },

  async update(id: string, payload: CreateEhcRootCauseCodeRequest): Promise<EhcRootCauseCode> {
    const res = await apiService.request<ApiEnvelope<EhcRootCauseCode>>(`/ehc/admin/root-causes/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
    return res.data as any;
  },

  async remove(id: string): Promise<void> {
    await apiService.request<ApiEnvelope<any>>(`/ehc/admin/root-causes/${id}`, { method: 'DELETE' });
  },
};

export default ehcRootCauseAdminService;

