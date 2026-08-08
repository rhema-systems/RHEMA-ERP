// Mirrors StaffLevelDto (ErpSystem.Core.DTOs.HR.StaffLevelDTOs).
export interface StaffLevel {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  rank: number;
  description?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

// Mirrors StaffLevelListDto (list view).
export interface StaffLevelListItem {
  id: string;
  name: string;
  code: string;
  rank: number;
  isActive: boolean;
}

// Mirrors CreateStaffLevelDto (server sets IsActive = true on create).
export interface CreateStaffLevelRequest {
  name: string;
  code: string;
  rank: number;
  description?: string | null;
}

// Mirrors UpdateStaffLevelDto (adds Id + IsActive).
export interface UpdateStaffLevelRequest extends CreateStaffLevelRequest {
  id: string;
  isActive: boolean;
}
