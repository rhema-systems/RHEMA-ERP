const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(isFormData: boolean = false): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  const headers: HeadersInit = {};
  if (!isFormData) headers['Content-Type'] = 'application/json';
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return headers;
}

export interface MaintenanceAttachmentDto {
  id: string;
  fileName: string;
  filePath: string;
  contentType: string;
  fileSizeBytes: number;
  fileSizeFormatted?: string;
  description?: string | null;
  attachmentType: string;
  entityType: string;
  entityId: string;
  uploadedDate: string;
  uploadedByUserName: string;
  isMainImage: boolean;
  imageWidth?: number | null;
  imageHeight?: number | null;
  thumbnailPath?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  locationDescription?: string | null;
  documentVersion?: string | null;
  category?: string | null;
}

async function ensureOk(response: Response): Promise<Response> {
  if (response.ok) return response;

  let message = `HTTP ${response.status}: ${response.statusText}`;
  try {
    const text = await response.text();
    if (text) {
      try {
        const json = JSON.parse(text);
        message = json?.message || json?.title || json?.error || message;
      } catch {
        message = text;
      }
    }
  } catch {
    // ignore
  }

  throw new Error(message);
}

export type MaintenanceAttachmentEntityType =
  | 'WorkOrder'
  | 'Asset'
  | 'Inspection'
  | 'FleetCompliance'
  | 'FleetIncident'
  | 'FleetTrip'
  | 'FleetFuelTransaction'
  | 'FleetCostEntry';

export const maintenanceAttachmentsService = {
  async list(entityType: MaintenanceAttachmentEntityType, entityId: string, opts?: { category?: string; attachmentType?: string }) {
    const qs = new URLSearchParams();
    if (opts?.category) qs.set('category', opts.category);
    if (opts?.attachmentType) qs.set('attachmentType', opts.attachmentType);

    const url = `${API_BASE_URL}/maintenance/attachments/${encodeURIComponent(entityType)}/${encodeURIComponent(entityId)}${qs.toString() ? `?${qs}` : ''}`;
    const res = await fetch(url, { headers: getAuthHeaders(false) });
    await ensureOk(res);
    return (await res.json()) as MaintenanceAttachmentDto[];
  },

  async upload(
    entityType: MaintenanceAttachmentEntityType,
    entityId: string,
    files: File[],
    opts?: { description?: string; category?: string; isMainImage?: boolean }
  ) {
    const form = new FormData();
    files.forEach((f) => form.append('files', f));
    if (opts?.description) form.append('description', opts.description);
    if (opts?.category) form.append('category', opts.category);
    if (typeof opts?.isMainImage === 'boolean') form.append('isMainImage', String(opts.isMainImage));

    const url = `${API_BASE_URL}/maintenance/attachments/upload/${encodeURIComponent(entityType)}/${encodeURIComponent(entityId)}`;
    const res = await fetch(url, { method: 'POST', headers: getAuthHeaders(true), body: form });
    await ensureOk(res);
    return (await res.json()) as MaintenanceAttachmentDto[];
  },

  async remove(attachmentId: string) {
    const url = `${API_BASE_URL}/maintenance/attachments/${encodeURIComponent(attachmentId)}`;
    const res = await fetch(url, { method: 'DELETE', headers: getAuthHeaders(false) });
    await ensureOk(res);
    return true;
  },

  async downloadBlob(attachmentId: string) {
    const url = `${API_BASE_URL}/maintenance/attachments/${encodeURIComponent(attachmentId)}/download`;
    const res = await fetch(url, { headers: getAuthHeaders(false) });
    await ensureOk(res);
    return await res.blob();
  },
};
