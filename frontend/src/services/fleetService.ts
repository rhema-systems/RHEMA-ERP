const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface FleetVehicleListDto {
  id: string;
  name: string;
  assetNumber: string;
  assetCategoryId: string;
  licensePlate?: string | null;
  vin?: string | null;
  status: string;
  categoryName: string;
  manufacturer?: string | null;
  model?: string | null;
  mileage?: number | null;
  operatingHours?: number | null;
}

export interface CreateFleetVehicleDto {
  name: string;
  assetCategoryId: string;
  licensePlate?: string | null;
  vin?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  location?: string | null;
  mileage?: number | null;
  operatingHours?: number | null;
}

export interface UpdateFleetVehicleDto extends CreateFleetVehicleDto {
  status: string;
}

export interface FleetTripDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  vehicleAssetNumber?: string | null;
  vehicleLicensePlate?: string | null;

  requestedByUserId: string;
  driverEmployeeId?: string | null;
  driverEmployeeName?: string | null;

  status: string;
  purpose?: string | null;
  origin?: string | null;
  destination?: string | null;
  notes?: string | null;

  plannedStartAt?: string | null;
  plannedEndAt?: string | null;
  actualStartAt?: string | null;
  actualEndAt?: string | null;

  createdAt: string;
  approvedAt?: string | null;
  rejectedAt?: string | null;
  rejectionReason?: string | null;
  dispatchedAt?: string | null;
  completedAt?: string | null;

  startMileage?: number | null;
  endMileage?: number | null;
  startOperatingHours?: number | null;
  endOperatingHours?: number | null;
}

export interface CreateFleetTripDto {
  vehicleAssetId: string;
  driverEmployeeId?: string | null;
  purpose?: string | null;
  origin?: string | null;
  destination?: string | null;
  notes?: string | null;
  plannedStartAt?: string | null;
  plannedEndAt?: string | null;
}

export interface DispatchFleetTripDto {
  dispatchedAt?: string | null;
  startMileage?: number | null;
  startOperatingHours?: number | null;
}

export interface CompleteFleetTripDto {
  completedAt?: string | null;
  endMileage?: number | null;
  endOperatingHours?: number | null;
  notes?: string | null;
}

export interface FleetComplianceItemDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  complianceType: string;
  referenceNumber?: string | null;
  issueDate?: string | null;
  expiryDate: string;
  isCritical: boolean;
  notes?: string | null;
  createdAt: string;
}

export interface CreateFleetComplianceItemDto {
  vehicleAssetId: string;
  complianceType: string;
  referenceNumber?: string | null;
  issueDate?: string | null;
  expiryDate: string;
  isCritical: boolean;
  notes?: string | null;
  documentLinks?: string | null;
}

export interface FleetFuelTransactionDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  fleetTripId?: string | null;
  fuelledAt: string;
  quantity: number;
  unit: string;
  unitCost?: number | null;
  totalCost?: number | null;
  mileageAtFuel?: number | null;
  operatingHoursAtFuel?: number | null;
  vendorName?: string | null;
  receiptReference?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface CreateFleetFuelTransactionDto {
  vehicleAssetId: string;
  fleetTripId?: string | null;
  fuelledAt: string;
  quantity: number;
  unit: string;
  unitCost?: number | null;
  mileageAtFuel?: number | null;
  operatingHoursAtFuel?: number | null;
  vendorName?: string | null;
  receiptReference?: string | null;
  notes?: string | null;
}

async function readError(response: Response): Promise<string> {
  try {
    const text = await response.text();
    return text || response.statusText;
  } catch {
    return response.statusText;
  }
}

export const fleetService = {
  async getVehicles(params?: {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    categoryId?: string;
  }): Promise<PagedResult<FleetVehicleListDto>> {
    const usp = new URLSearchParams();
    usp.set('page', String(params?.page ?? 1));
    usp.set('pageSize', String(params?.pageSize ?? 25));
    if (params?.searchTerm) usp.set('searchTerm', params.searchTerm);
    if (params?.categoryId) usp.set('categoryId', params.categoryId);

    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/vehicles?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createVehicle(dto: CreateFleetVehicleDto) {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/vehicles`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async updateVehicle(id: string, dto: UpdateFleetVehicleDto) {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/vehicles/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getTrips(params?: {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: string;
    vehicleAssetId?: string;
  }): Promise<PagedResult<FleetTripDto>> {
    const usp = new URLSearchParams();
    usp.set('page', String(params?.page ?? 1));
    usp.set('pageSize', String(params?.pageSize ?? 25));
    if (params?.searchTerm) usp.set('searchTerm', params.searchTerm);
    if (params?.status) usp.set('status', params.status);
    if (params?.vehicleAssetId) usp.set('vehicleAssetId', params.vehicleAssetId);

    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getTrip(id: string): Promise<FleetTripDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createTrip(dto: CreateFleetTripDto): Promise<FleetTripDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async updateTrip(id: string, dto: CreateFleetTripDto): Promise<FleetTripDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async submitTrip(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}/submit-for-approval`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async approveTrip(id: string, comments?: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}/approve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ comments }),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async rejectTrip(id: string, reason: string, comments?: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}/reject`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ reason, comments }),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async dispatchTrip(id: string, dto: DispatchFleetTripDto): Promise<FleetTripDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}/dispatch`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async completeTrip(id: string, dto: CompleteFleetTripDto): Promise<FleetTripDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}/complete`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async cancelTrip(id: string, reason: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/trips/${id}/cancel`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ reason }),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async getCompliance(vehicleAssetId: string, page = 1, pageSize = 25): Promise<PagedResult<FleetComplianceItemDto>> {
    const response = await fetch(
      `${API_BASE_URL}/maintenance/fleet/compliance/vehicle/${vehicleAssetId}/paged?page=${page}&pageSize=${pageSize}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createCompliance(dto: CreateFleetComplianceItemDto): Promise<FleetComplianceItemDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/compliance`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async updateCompliance(id: string, dto: CreateFleetComplianceItemDto): Promise<FleetComplianceItemDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/compliance/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async deleteCompliance(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/compliance/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async getFuel(vehicleAssetId: string, page = 1, pageSize = 25): Promise<PagedResult<FleetFuelTransactionDto>> {
    const response = await fetch(
      `${API_BASE_URL}/maintenance/fleet/fuel/vehicle/${vehicleAssetId}/paged?page=${page}&pageSize=${pageSize}`,
      { headers: getAuthHeaders() }
    );
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createFuel(dto: CreateFleetFuelTransactionDto): Promise<FleetFuelTransactionDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/fuel`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async deleteFuel(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/fuel/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
  },
};

export default fleetService;
