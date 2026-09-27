import { apiService } from '@/services/api.service';

const root = '/quantity-survey/measurements';

export type MeasurementSourceType = 'Design' | 'Site';
export type MeasurementFormulaType = 'Count' | 'Length' | 'Area' | 'Volume';
export type MeasurementEvidenceType =
  'DrawingMarkup' | 'SitePhoto' | 'MeasurementWorkbook' | 'SupportingDocument';

export interface MeasurementLookup {
  id: string;
  label: string;
  group: string;
  description?: string | null;
}
export interface MeasurementLookups {
  approvedBoqLines: MeasurementLookup[];
  approvedDrawings: MeasurementLookup[];
}
export interface MeasurementLineInput {
  clientLineKey: string;
  sequence: number;
  description: string;
  formulaType: MeasurementFormulaType;
  timesing: number;
  length?: number;
  width?: number;
  height?: number;
  isDeduction: boolean;
  notes?: string;
}
export interface MeasurementLine extends MeasurementLineInput {
  id: string;
  formula: string;
  calculatedQuantity: number;
}
export interface MeasurementAttachment {
  id: string;
  evidenceType: MeasurementEvidenceType | number;
  title: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  checksumSha256: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  uploadedByName: string;
  createdAt: string;
}
export interface MeasurementSheet {
  id: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  projectBoqVersionId: string;
  projectBoqVersionLineId: string;
  boqLineKey: string;
  boqLineLabel: string;
  boqQuantity: number;
  unitOfMeasure?: string | null;
  projectDrawingId?: string | null;
  drawingLabel?: string | null;
  sourceType: MeasurementSourceType | number;
  sheetReference: string;
  title: string;
  measurementDate: string;
  siteLocation?: string | null;
  totalMeasuredQuantity: number;
  status: 'Draft' | 'Recorded';
  evidenceMetadataTemplateCode: string;
  preparedByName: string;
  preparedAt: string;
  recordedByName?: string | null;
  recordedAt?: string | null;
  rowVersion: string;
  lines: MeasurementLine[];
  attachments: MeasurementAttachment[];
}
export interface MeasurementPage {
  items: MeasurementSheet[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface MeasurementRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  createdAt: string;
}
export interface MeasurementSaveRequest {
  clientRequestId: string;
  projectId?: string;
  projectBoqVersionLineId?: string;
  projectDrawingId?: string;
  sourceType: MeasurementSourceType;
  title: string;
  measurementDate: string;
  siteLocation?: string;
  lines: MeasurementLineInput[];
  rowVersion?: string;
}

export const quantitySurveyMeasurementService = {
  lookups: (projectId: string) =>
    apiService.get<MeasurementLookups>(`${root}/lookups`, { projectId }),
  list: (query: {
    projectId?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }) => apiService.get<MeasurementPage>(root, query),
  get: (id: string) => apiService.get<MeasurementSheet>(`${root}/${id}`),
  create: (request: MeasurementSaveRequest) =>
    apiService.post<MeasurementSheet>(root, request),
  update: (id: string, request: MeasurementSaveRequest) =>
    apiService.put<MeasurementSheet>(`${root}/${id}`, request),
  record: (
    id: string,
    request: { clientRequestId: string; rowVersion: string }
  ) => apiService.post<MeasurementSheet>(`${root}/${id}/record`, request),
  addAttachment: async (
    id: string,
    request: {
      clientRequestId: string;
      evidenceType: MeasurementEvidenceType;
      title: string;
      file: File;
    }
  ) => {
    const form = new FormData();
    form.append('clientRequestId', request.clientRequestId);
    form.append('evidenceType', request.evidenceType);
    form.append('title', request.title);
    form.append('file', request.file, request.file.name);
    // Central DMS scanning may take 60 seconds, longer than the ordinary API timeout.
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 120_000);
    try {
      return await apiService.post<MeasurementAttachment>(
        `${root}/${id}/attachments`,
        form,
        controller.signal
      );
    } catch (error) {
      if (controller.signal.aborted) {
        throw new Error('Evidence upload timed out after 120 seconds. Check the sheet before retrying.');
      }
      throw error;
    } finally {
      clearTimeout(timeout);
    }
  },
  history: (id: string) =>
    apiService.get<MeasurementRevision[]>(`${root}/${id}/history`),
  attachmentContent: (id: string, attachmentId: string) =>
    apiService.downloadBlob(
      `${root}/${id}/attachments/${attachmentId}/content`
    ),
};
