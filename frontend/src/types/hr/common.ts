// Shared HR types mirroring the ported HR backend (ErpSystem.Core.DTOs).
// NOTE: HR's PagedResult (ErpSystem.Core.DTOs.Common.CommonDTOs) differs from the
// finance/AR PagedResult — it exposes `page`/`hasPrevious`/`hasNext`, NOT
// `pageNumber`/`hasPreviousPage`. Do not reuse the AR one for HR endpoints.
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

// Audit fields present on every read DTO that extends BaseDto.
export interface AuditFields {
  id: string;
  createdAt: string;
  createdBy?: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}
