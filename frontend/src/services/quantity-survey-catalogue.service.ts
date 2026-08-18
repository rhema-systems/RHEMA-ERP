import { apiService } from '@/services/api.service';
import type {
  CreateProjectCatalogEntryDto,
  ProjectCatalogEntryDto,
} from '@/services/projectService';

const root = '/quantity-survey/catalogues';

export interface QuantitySurveyCatalogueQuery {
  catalogType?: string;
  search?: string;
  standardCode?: string;
  effectiveAt?: string;
  includeInactive?: boolean;
}

export interface QuantitySurveyUnitOption {
  code: string;
  name: string;
}

export const quantitySurveyCatalogueService = {
  list: (query?: QuantitySurveyCatalogueQuery) =>
    apiService.get<ProjectCatalogEntryDto[]>(
      root,
      query ? { ...query } : undefined
    ),
  unitsOfMeasure: () =>
    apiService.get<QuantitySurveyUnitOption[]>(
      `${root}/lookups/units-of-measure`
    ),
  create: (request: CreateProjectCatalogEntryDto) =>
    apiService.post<ProjectCatalogEntryDto>(root, request),
  update: (id: string, request: CreateProjectCatalogEntryDto) =>
    apiService.put<ProjectCatalogEntryDto>(`${root}/${id}`, request),
  delete: (id: string) => apiService.delete<void>(`${root}/${id}`),
};
