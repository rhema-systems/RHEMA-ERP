import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

const headers = () => {
  const token = typeof window === 'undefined' ? null : localStorage.getItem('token') || localStorage.getItem('authToken');
  return { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
};

export const supplierReturnReasons = ['Quality', 'Damage', 'Excess', 'Wrong', 'Other'] as const;
export type SupplierReturnReason = typeof supplierReturnReasons[number];

export interface SupplierReturnSourceLine {
  id: string;
  inventoryItemId: string;
  itemCode?: string;
  itemName?: string;
  acceptedQuantity: number;
  unitCost: number;
  unitOfMeasure?: string;
  storageLocationId?: string;
}

export interface SupplierReturnSourceGrn {
  id: string;
  grnNumber: string;
  supplierId?: string;
  supplierName?: string;
  warehouseId: string;
  warehouseName?: string;
  status: string | number;
  items: SupplierReturnSourceLine[];
}

export interface SupplierReturn {
  id: string;
  returnNumber: string;
  returnDate: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  goodsReceiptNoteId?: string;
  grnNumber?: string;
  status: string;
  returnReason: string;
  totalItems: number;
  totalQuantity: number;
  totalValue: number;
  requestedByName?: string;
  approvedByName?: string;
  notes?: string;
  createdAtFormatted: string;
  approvedDate?: string;
  shippedDate?: string;
  trackingNumber?: string;
  creditNoteNumber?: string;
  creditNoteAmount?: number;
  items?: SupplierReturnLine[];
}

export interface SupplierReturnLine {
  id: string;
  inventoryItemId: string;
  itemCode: string;
  itemName: string;
  returnQuantity: number;
  unitOfMeasure: string;
  unitCost: number;
  totalCost: number;
  returnReason: string;
  grnItemId?: string;
  notes?: string;
}

export interface CreateSupplierReturn {
  supplierId: string;
  warehouseId: string;
  goodsReceiptNoteId: string;
  returnReason: SupplierReturnReason;
  notes?: string;
  items: Array<{
    inventoryItemId: string;
    returnQuantity: number;
    returnReason: SupplierReturnReason;
    grnItemId: string;
    notes?: string;
  }>;
}

const url = `${API_URL}/inventory/supplier-returns`;

export const supplierReturnService = {
  async getAll(): Promise<SupplierReturn[]> {
    return (await axios.get<SupplierReturn[]>(url, { headers: headers() })).data;
  },
  async getSourceGrns(): Promise<SupplierReturnSourceGrn[]> {
    return (await axios.get<SupplierReturnSourceGrn[]>(`${url}/source-grns`, { headers: headers() })).data;
  },
  async get(id: string): Promise<SupplierReturn> {
    return (await axios.get<SupplierReturn>(`${url}/${id}`, { headers: headers() })).data;
  },
  async create(request: CreateSupplierReturn): Promise<SupplierReturn> {
    return (await axios.post<SupplierReturn>(url, request, { headers: headers() })).data;
  },
  async submit(id: string): Promise<void> { await axios.post(`${url}/${id}/submit`, {}, { headers: headers() }); },
  async approve(id: string): Promise<void> { await axios.post(`${url}/${id}/approve`, {}, { headers: headers() }); },
  async reject(id: string, reason: string): Promise<void> { await axios.post(`${url}/${id}/reject`, { reason }, { headers: headers() }); },
  async ship(id: string, trackingNumber?: string): Promise<void> { await axios.post(`${url}/${id}/ship`, { trackingNumber }, { headers: headers() }); },
  async cancel(id: string, reason: string): Promise<void> { await axios.post(`${url}/${id}/cancel`, { reason }, { headers: headers() }); },
};
