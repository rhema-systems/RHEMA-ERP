const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined' ? (localStorage.getItem('token') || localStorage.getItem('authToken')) : null;
  return { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
}

export interface JournalTemplateSummary {
  id: string; name: string; description?: string; transactionType: string;
  isActive: boolean; lineCount: number; createdAt: string;
}
export interface JournalTemplateDetail extends JournalTemplateSummary {
  postingBehavior?: string; autoPost: boolean;
  lines: JournalTemplateLine[];
}
export interface JournalTemplateLine {
  id: string; sequence: number; accountType: string; accountCode?: string;
  accountName?: string; entryType: string; amountSource: string;
  fixedAmount?: number; percentage?: number; description?: string;
}
export interface CreateJournalTemplate {
  name: string; description?: string; transactionType: string;
  postingBehavior?: string; autoPost: boolean;
  lines: CreateJournalTemplateLine[];
}
export interface CreateJournalTemplateLine {
  sequence: number; accountType: string; accountCode?: string;
  accountName?: string; entryType: string; amountSource: string;
  fixedAmount?: number; percentage?: number; description?: string;
}

export const salesJournalTemplateService = {
  async getTemplates(page = 1, pageSize = 20, search?: string, isActive?: boolean) {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (isActive !== undefined) params.append('isActive', isActive.toString());
    const res = await fetch(`${API_BASE_URL}/sales/journal-templates?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch templates');
    return { data: await res.json() };
  },
  async getTemplateById(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/journal-templates/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch template');
    return { data: await res.json() };
  },
  async createTemplate(data: CreateJournalTemplate) {
    const res = await fetch(`${API_BASE_URL}/sales/journal-templates`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create template');
    return { data: await res.json() };
  },
  async updateTemplate(id: string, data: CreateJournalTemplate) {
    const res = await fetch(`${API_BASE_URL}/sales/journal-templates/${id}`, { method: 'PUT', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to update template');
    return { data: await res.json() };
  },
  async activateTemplate(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/journal-templates/${id}/activate`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to activate template');
    return true;
  },
  async deactivateTemplate(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/journal-templates/${id}/deactivate`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to deactivate template');
    return true;
  },
};
