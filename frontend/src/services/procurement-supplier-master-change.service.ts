import { apiService } from '@/services/api.service';
import { procurementMasterDataChangeService } from '@/services/procurement-master-data-change.service';
import type {
  ProcurementMasterDataChangeSearch,
  SaveProcurementMasterDataChange,
} from '@/types/procurement-master-data-change';

export interface SupplierMasterOption {
  id: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  status: string;
  approvalStatus?: string;
}

export interface SupplierCategoryOption {
  id: string;
  categoryCode: string;
  categoryName: string;
  categoryType: string;
  isActive: boolean;
}

interface SupplierPage {
  items: SupplierMasterOption[];
}

export const procurementSupplierMasterChangeService = {
  summary: procurementMasterDataChangeService.summary,
  registry: procurementMasterDataChangeService.registry,
  policies: procurementMasterDataChangeService.policies,
  search: (request: ProcurementMasterDataChangeSearch) =>
    procurementMasterDataChangeService.search(request),
  saveDraft: (request: SaveProcurementMasterDataChange) =>
    procurementMasterDataChangeService.saveDraft(undefined, request),
  submit: procurementMasterDataChangeService.submit,
  revalidate: procurementMasterDataChangeService.revalidate,
  approve: procurementMasterDataChangeService.approve,
  reject: procurementMasterDataChangeService.reject,
  apply: procurementMasterDataChangeService.apply,
  cancel: procurementMasterDataChangeService.cancel,
  suppliers: async () => {
    const page = await apiService.get<SupplierPage>(
      '/procurement/business-partners',
      { page: 1, pageSize: 100 }
    );
    return page.items.filter(
      (item) =>
        item.partnerType.toLowerCase().includes('supplier') ||
        item.partnerType.toLowerCase().includes('contractor') ||
        item.partnerType.toLowerCase().includes('both')
    );
  },
  categories: () =>
    apiService.get<SupplierCategoryOption[]>(
      '/procurement/partner-categories/active'
    ),
};
