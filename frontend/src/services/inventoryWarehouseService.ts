import { apiService } from './api.service';

export interface InventoryWarehouseOption {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  isDefault: boolean;
  warehouseType: string;
}

export interface InventoryWarehouseLocationOption {
  id: string;
  warehouseId: string;
  locationCode: string;
  name?: string;
  locationType?: string;
  isActive: boolean;
  isDefault?: boolean;
  isPickingLocation: boolean;
  isReceivingLocation: boolean;
}

export const inventoryWarehouseService = {
  getActiveWarehouses() {
    return apiService.get<InventoryWarehouseOption[]>('/inventory/warehouses/active');
  },

  getWarehouseLocations(warehouseId?: string) {
    return apiService.get<InventoryWarehouseLocationOption[]>('/inventory/warehouse-locations', {
      warehouseId: warehouseId || undefined,
    });
  },
};
