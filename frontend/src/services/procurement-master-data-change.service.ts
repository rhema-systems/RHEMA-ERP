import { apiService } from '@/services/api.service';
import type {
  ProcurementMasterDataChange,
  ProcurementMasterDataChangePage,
  ProcurementMasterDataChangeSearch,
  ProcurementMasterDataChangeSummary,
  ProcurementMasterDataPolicy,
  ProcurementMasterDataResourceDefinition,
  SaveProcurementMasterDataChange,
  SaveProcurementMasterDataPolicy,
} from '@/types/procurement-master-data-change';

const root = '/procurement/master-data-changes';

export const procurementMasterDataChangeService = {
  registry: () =>
    apiService.get<ProcurementMasterDataResourceDefinition[]>(
      `${root}/registry`
    ),
  summary: () =>
    apiService.get<ProcurementMasterDataChangeSummary>(`${root}/summary`),
  policies: () =>
    apiService.get<ProcurementMasterDataPolicy[]>(`${root}/policies`),
  savePolicy: (
    id: string | undefined,
    request: SaveProcurementMasterDataPolicy
  ) =>
    id
      ? apiService.put<ProcurementMasterDataPolicy>(
          `${root}/policies/${id}`,
          request
        )
      : apiService.post<ProcurementMasterDataPolicy>(
          `${root}/policies`,
          request
        ),
  activatePolicy: (id: string, reason: string, rowVersion: string) =>
    apiService.post<ProcurementMasterDataPolicy>(
      `${root}/policies/${id}/activate`,
      { reason, rowVersion }
    ),
  retirePolicy: (id: string, reason: string, rowVersion: string) =>
    apiService.post<ProcurementMasterDataPolicy>(
      `${root}/policies/${id}/retire`,
      { reason, rowVersion }
    ),
  search: (request: ProcurementMasterDataChangeSearch) =>
    apiService.get<ProcurementMasterDataChangePage>(
      root,
      request as Record<string, unknown>
    ),
  get: (id: string) =>
    apiService.get<ProcurementMasterDataChange>(`${root}/${id}`),
  saveDraft: (
    id: string | undefined,
    request: SaveProcurementMasterDataChange
  ) =>
    id
      ? apiService.put<ProcurementMasterDataChange>(`${root}/${id}`, request)
      : apiService.post<ProcurementMasterDataChange>(root, request),
  submit: (id: string, rowVersion: string, comment?: string) =>
    apiService.post<ProcurementMasterDataChange>(`${root}/${id}/submit`, {
      rowVersion,
      comment,
    }),
  revalidate: (id: string, rowVersion: string, comment?: string) =>
    apiService.post<ProcurementMasterDataChange>(`${root}/${id}/revalidate`, {
      rowVersion,
      comment,
    }),
  approve: (id: string, rowVersion: string, comment: string) =>
    apiService.post<ProcurementMasterDataChange>(`${root}/${id}/approve`, {
      rowVersion,
      comment,
    }),
  reject: (id: string, rowVersion: string, comment: string) =>
    apiService.post<ProcurementMasterDataChange>(`${root}/${id}/reject`, {
      rowVersion,
      comment,
    }),
  apply: (id: string, rowVersion: string, comment?: string) =>
    apiService.post<ProcurementMasterDataChange>(`${root}/${id}/apply`, {
      rowVersion,
      comment,
    }),
  cancel: (id: string, rowVersion: string, comment: string) =>
    apiService.post<ProcurementMasterDataChange>(`${root}/${id}/cancel`, {
      rowVersion,
      comment,
    }),
};
