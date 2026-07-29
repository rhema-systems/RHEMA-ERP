import { apiService } from '@/services/api.service';
import type {
  CloneFrameworkAgreement,
  CreateFrameworkAgreement,
  DecideFrameworkExtension,
  FrameworkAgreement,
  FrameworkAgreementDocument,
  FrameworkAgreementExtension,
  FrameworkAgreementPage,
  FrameworkAgreementSearch,
  FrameworkAgreementSummary,
  FrameworkCategoryOption,
  FrameworkItemOption,
  FrameworkLifecycleRequest,
  FrameworkSourceOption,
  FrameworkWorkflowOption,
  RequestFrameworkExtension,
  UpdateFrameworkAgreement,
} from '@/types/procurement-framework-agreement';

const root = '/procurement/framework-agreements';

export const procurementFrameworkAgreementService = {
  summary: () => apiService.get<FrameworkAgreementSummary>(`${root}/summary`),
  search: (request: FrameworkAgreementSearch) =>
    apiService.get<FrameworkAgreementPage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) => apiService.get<FrameworkAgreement>(`${root}/${id}`),
  workflowOptions: () =>
    apiService.get<FrameworkWorkflowOption[]>(`${root}/options/workflows`),
  categoryOptions: () =>
    apiService.get<FrameworkCategoryOption[]>(`${root}/options/categories`),
  itemOptions: () =>
    apiService.get<FrameworkItemOption[]>(`${root}/options/items`),
  sourceOptions: () =>
    apiService.get<FrameworkSourceOption[]>(`${root}/options/sources`),
  create: (request: CreateFrameworkAgreement) =>
    apiService.post<FrameworkAgreement>(root, request),
  update: (id: string, request: UpdateFrameworkAgreement) =>
    apiService.put<FrameworkAgreement>(`${root}/${id}`, request),
  clone: (id: string, request: CloneFrameworkAgreement) =>
    apiService.post<FrameworkAgreement>(`${root}/${id}/clone`, request),
  submit: (id: string, request: FrameworkLifecycleRequest) =>
    apiService.post<FrameworkAgreement>(`${root}/${id}/submit`, request),
  approve: (id: string, request: FrameworkLifecycleRequest) =>
    apiService.post<FrameworkAgreement>(`${root}/${id}/approve`, request),
  reject: (id: string, request: FrameworkLifecycleRequest) =>
    apiService.post<FrameworkAgreement>(`${root}/${id}/reject`, request),
  terminate: (id: string, request: FrameworkLifecycleRequest) =>
    apiService.post<FrameworkAgreement>(`${root}/${id}/terminate`, request),
  uploadDocument: (
    id: string,
    file: File,
    documentType: string,
    title: string,
    isRequired: boolean
  ) => {
    const form = new FormData();
    form.append('file', file);
    form.append('documentType', documentType);
    form.append('title', title);
    form.append('isRequired', String(isRequired));
    return apiService.post<FrameworkAgreementDocument>(
      `${root}/${id}/documents`,
      form
    );
  },
  retireDocument: (id: string, documentId: string) =>
    apiService.delete<FrameworkAgreementDocument>(
      `${root}/${id}/documents/${documentId}`
    ),
  downloadDocument: (id: string, documentId: string) =>
    apiService.downloadBlob(`${root}/${id}/documents/${documentId}/content`),
  requestExtension: (id: string, request: RequestFrameworkExtension) =>
    apiService.post<FrameworkAgreementExtension>(
      `${root}/${id}/extensions`,
      request
    ),
  decideExtension: (
    id: string,
    extensionId: string,
    request: DecideFrameworkExtension
  ) =>
    apiService.post<FrameworkAgreementExtension>(
      `${root}/${id}/extensions/${extensionId}/decision`,
      request
    ),
  processLifecycle: () =>
    apiService.post<{ processed: number }>(`${root}/process-lifecycle`, {}),
};
