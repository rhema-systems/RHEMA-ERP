import { apiService } from './api.service';
import type {
  ExecuteReportDto,
  ExportReportDto,
  ReportResult,
} from './reports';

export interface FinanceAdHocDatasetField {
  key: string;
  label: string;
  dataType: string;
  canFilter: boolean;
  canGroup: boolean;
  canAggregate: boolean;
}

export interface FinanceAdHocDataset {
  code: string;
  name: string;
  description: string;
  fields: FinanceAdHocDatasetField[];
}

export interface FinanceAdHocColumn {
  field: string;
  aggregation: 'None' | 'Count' | 'Sum' | 'Average' | 'Minimum' | 'Maximum';
}

export interface FinanceAdHocFilter {
  field: string;
  operator: string;
  value?: string;
  valueTo?: string;
}

export interface FinanceAdHocSort {
  field: string;
  descending: boolean;
}

export interface FinanceAdHocDefinition {
  id: string;
  reportId: string;
  name: string;
  description: string;
  datasetCode: string;
  datasetName: string;
  columns: FinanceAdHocColumn[];
  filters: FinanceAdHocFilter[];
  sorts: FinanceAdHocSort[];
  visibility: 'Private' | 'Finance';
  maximumRows: number;
  ownerUserId: string;
  ownerName: string;
  canMaintain: boolean;
  createdAt: string;
  lastRun?: string;
  rowVersion: string;
}

export interface FinanceAdHocWorkspace {
  datasets: FinanceAdHocDataset[];
  definitions: FinanceAdHocDefinition[];
  privateDefinitions: number;
  sharedDefinitions: number;
}

export interface SaveFinanceAdHocDefinition {
  name: string;
  description: string;
  datasetCode: string;
  columns: FinanceAdHocColumn[];
  filters: FinanceAdHocFilter[];
  sorts: FinanceAdHocSort[];
  visibility: 'Private' | 'Finance';
  maximumRows: number;
  rowVersion?: string;
}

const endpoint = '/finance/ad-hoc-reports';

/**
 * Finance-specific client for FR-RP-012. The browser sends catalogue keys only;
 * it never receives or constructs SQL, table names, joins, or tenant predicates.
 */
export const financeAdHocReportsService = {
  getWorkspace: () =>
    apiService.request<FinanceAdHocWorkspace>(`${endpoint}/workspace`),
  create: (request: SaveFinanceAdHocDefinition) =>
    apiService.request<FinanceAdHocDefinition>(endpoint, {
      method: 'POST',
      body: JSON.stringify(request),
    }),
  update: (id: string, request: SaveFinanceAdHocDefinition) =>
    apiService.request<FinanceAdHocDefinition>(`${endpoint}/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    }),
  delete: (definition: FinanceAdHocDefinition) =>
    apiService.request<void>(
      `${endpoint}/${definition.id}?rowVersion=${encodeURIComponent(definition.rowVersion)}`,
      {
        method: 'DELETE',
      }
    ),
  execute: (id: string, request: ExecuteReportDto) =>
    apiService.request<ReportResult>(`${endpoint}/${id}/execute`, {
      method: 'POST',
      body: JSON.stringify(request),
    }),
  async export(
    id: string,
    request: ExportReportDto
  ): Promise<{ fileName: string; blob: Blob }> {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || '/api';
    const token =
      typeof window === 'undefined'
        ? null
        : localStorage.getItem('token') || localStorage.getItem('authToken');
    const response = await fetch(`${baseUrl}${endpoint}/${id}/export`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token && { Authorization: `Bearer ${token}` }),
      },
      body: JSON.stringify(request),
    });
    if (!response.ok)
      throw new Error((await response.text()) || 'Report export failed.');
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const fileNameMatch = disposition.match(
      /filename\*?=(?:UTF-8'')?"?([^";]+)/i
    );
    let fileName = `finance-ad-hoc-report.${request.format}`;
    if (fileNameMatch?.[1]) {
      // A malformed third-party/proxy header must not prevent an otherwise valid export download.
      try {
        fileName = decodeURIComponent(fileNameMatch[1]);
      } catch {
        fileName = fileNameMatch[1];
      }
    }
    return { fileName, blob: await response.blob() };
  },
};
