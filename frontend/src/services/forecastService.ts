const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined' ? (localStorage.getItem('token') || localStorage.getItem('authToken')) : null;
  return { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
}

export interface ForecastSummary {
  id: string; name: string; method: string; status: string;
  periodStart: string; periodEnd: string;
  totalForecastAmount: number; totalActualAmount: number;
  variance: number; variancePercentage: number;
  ownerName?: string; lineCount: number; createdAt: string;
}
export interface ForecastDetail extends ForecastSummary {
  ownerId?: string; description?: string;
  submittedDate?: string; approvedDate?: string; approvedBy?: string;
  notes?: string; lines: ForecastLine[];
}
export interface ForecastLine {
  id: string; category?: string; productName?: string; salesRepName?: string;
  forecastQuantity: number; forecastAmount: number;
  actualQuantity: number; actualAmount: number; variance: number; notes?: string;
}
export interface CreateForecast {
  name: string; description?: string; method: string;
  periodStart: string; periodEnd: string; ownerId?: string; notes?: string;
  lines: CreateForecastLine[];
}
export interface CreateForecastLine {
  category?: string; productName?: string; productId?: string;
  salesRepName?: string; salesRepId?: string;
  forecastQuantity: number; forecastAmount: number; notes?: string;
}

export const forecastService = {
  async getForecasts(page = 1, pageSize = 20, search?: string, status?: string) {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    const res = await fetch(`${API_BASE_URL}/sales/forecasts?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch forecasts');
    return { data: await res.json() };
  },
  async getForecastById(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/forecasts/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch forecast');
    return { data: await res.json() };
  },
  async createForecast(data: CreateForecast) {
    const res = await fetch(`${API_BASE_URL}/sales/forecasts`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create forecast');
    return { data: await res.json() };
  },
  async submitForecast(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/forecasts/${id}/submit`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to submit forecast');
    return { data: await res.json() };
  },
  async approveForecast(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/forecasts/${id}/approve`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to approve forecast');
    return { data: await res.json() };
  },
  async lockForecast(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/forecasts/${id}/lock`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to lock forecast');
    return { data: await res.json() };
  },
  async refreshActuals(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/forecasts/${id}/refresh-actuals`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to refresh actuals');
    return { data: await res.json() };
  },
};
