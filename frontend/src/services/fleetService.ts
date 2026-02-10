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
  currentDriverEmployeeId?: string | null;
  currentDriverEmployeeName?: string | null;
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

export interface EmployeeDto {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  emailAddress: string;
}

export interface FleetVehicleAssignmentDto {
  id: string;
  vehicleAssetId: string;
  employeeId: string;
  employeeName: string;
  assignedFromUtc: string;
  assignedToUtc?: string | null;
  assignmentType: string;
  isActive: boolean;
  notes?: string | null;
}

export interface AssignFleetDriverDto {
  vehicleAssetId: string;
  employeeId: string;
  assignedFromUtc?: string | null;
  assignmentType?: string | null;
  notes?: string | null;
}

export interface EndFleetDriverAssignmentDto {
  assignedToUtc?: string | null;
  notes?: string | null;
}

export interface FleetTripInspectionDto {
  id: string;
  fleetTripId: string;
  inspectionTemplateId: string;
  inspectionTemplateName: string;
  inspectorEmployeeId?: string | null;
  inspectorEmployeeName?: string | null;
  inspectionKind: string;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  status: string;
  overallResult?: string | null;
  inspectionData: string;
  notes?: string | null;
}

export interface StartFleetTripInspectionDto {
  fleetTripId: string;
  inspectionTemplateId: string;
  inspectorEmployeeId?: string | null;
  inspectionKind: string; // PreTrip | PostTrip
}

export interface CompleteFleetTripInspectionDto {
  completedAtUtc?: string | null;
  overallResult: string; // Pass | Fail
  inspectionData: string;
  notes?: string | null;
}

export interface FleetDefectDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  fleetTripId?: string | null;
  fleetTripInspectionId?: string | null;
  title: string;
  description?: string | null;
  severity: string;
  status: string;
  reportedAtUtc: string;
  reportedByEmployeeId?: string | null;
  reportedByEmployeeName?: string | null;
  workOrderId?: string | null;
}

export interface CreateFleetDefectDto {
  vehicleAssetId: string;
  fleetTripId?: string | null;
  fleetTripInspectionId?: string | null;
  title: string;
  description?: string | null;
  severity: string;
}

export interface UpdateFleetDefectStatusDto {
  status: string;
  notes?: string | null;
}

export interface CreateWorkOrderFromFleetDefectDto {
  defectId: string;
  workOrderTypeId: string;
  maintenanceTypeId: string;
  priorityLevelId: string;
  titleOverride?: string | null;
  descriptionOverride?: string | null;
}

export interface FleetTyreDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  serialNumber: string;
  brand?: string | null;
  size?: string | null;
  position?: string | null;
  treadDepthMm?: number | null;
  installedAtUtc: string;
  removedAtUtc?: string | null;
  status: string;
  notes?: string | null;
}

export interface FleetTyreEventDto {
  id: string;
  fleetTyreId: string;
  vehicleAssetId: string;
  eventAtUtc: string;
  eventType: string;
  fromPosition?: string | null;
  toPosition?: string | null;
  fromStatus?: string | null;
  toStatus?: string | null;
  treadDepthMm?: number | null;
  costAmount?: number | null;
  currencyCode?: string | null;
  notes?: string | null;
  createdAt: string;
  createdByUserId?: string | null;
}

export interface CreateFleetTyreDto {
  vehicleAssetId: string;
  serialNumber: string;
  brand?: string | null;
  size?: string | null;
  position?: string | null;
  treadDepthMm?: number | null;
  installedAtUtc?: string | null;
  status: string;
  costAmount?: number | null;
  currencyCode?: string | null;
  notes?: string | null;
}

export interface FleetBatteryDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  serialNumber: string;
  brand?: string | null;
  spec?: string | null;
  position?: string | null;
  installedAtUtc: string;
  removedAtUtc?: string | null;
  status: string;
  notes?: string | null;
}

export interface FleetBatteryEventDto {
  id: string;
  fleetBatteryId: string;
  vehicleAssetId: string;
  eventAtUtc: string;
  eventType: string;
  fromPosition?: string | null;
  toPosition?: string | null;
  fromStatus?: string | null;
  toStatus?: string | null;
  costAmount?: number | null;
  currencyCode?: string | null;
  notes?: string | null;
  createdAt: string;
  createdByUserId?: string | null;
}

export interface FleetBatteryKpisDto {
  vehicleAssetId: string;
  total: number;
  installed: number;
  inStock: number;
  removed: number;
  disposed: number;
  averageInstalledAgeDays?: number | null;
  latestInstalledAtUtc?: string | null;
}

export interface CreateFleetBatteryDto {
  vehicleAssetId: string;
  serialNumber: string;
  brand?: string | null;
  spec?: string | null;
  position?: string | null;
  installedAtUtc?: string | null;
  status: string;
  costAmount?: number | null;
  currencyCode?: string | null;
  notes?: string | null;
}

export interface FleetExternalRepairDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  vendorBusinessPartnerId?: string | null;
  vendorBusinessPartnerName?: string | null;
  title: string;
  description?: string | null;
  status: string;
  estimatedCost?: number | null;
  actualCost?: number | null;
  currencyCode?: string | null;
  requestedAtUtc: string;
  approvedAtUtc?: string | null;
  completedAtUtc?: string | null;
  invoicedAtUtc?: string | null;
  workOrderId?: string | null;
}

export interface CreateFleetExternalRepairDto {
  vehicleAssetId: string;
  vendorBusinessPartnerId?: string | null;
  title: string;
  description?: string | null;
  estimatedCost?: number | null;
  currencyCode?: string | null;
}

export interface UpdateFleetExternalRepairStatusDto {
  status: string;
  actualCost?: number | null;
  currencyCode?: string | null;
}

export interface FleetDashboardSummaryDto {
  activeVehicles: number;
  tripsThisMonth: number;
  openDefects: number;
  complianceDueSoon: number;
  complianceOverdue: number;
  fuelCostThisMonth: number;
  externalRepairCostThisMonth: number;
  internalMaintenanceCostThisMonth: number;
  totalCostThisMonth: number;
  averageFuelCostPerKm?: number | null;
  averageKmPerLiter?: number | null;
}

export interface FleetCostSummaryRowDto {
  vehicleAssetId: string;
  vehicleName: string;
  entryCount: number;
  totalAmount: number;
  fuelAmount: number;
  externalRepairAmount: number;
  internalMaintenanceAmount: number;
  otherAmount: number;
  completedTrips: number;
  totalKm: number;
  totalHours: number;
  costPerKm?: number | null;
  costPerHour?: number | null;
}

export interface FleetCostSummaryDto {
  fromUtc: string;
  toUtc: string;
  totalAmount: number;
  rows: FleetCostSummaryRowDto[];
}

export interface FleetUtilizationRowDto {
  vehicleAssetId: string;
  vehicleName: string;
  completedTrips: number;
  totalKm: number;
  totalHours: number;
  averageKmPerTrip?: number | null;
  averageHoursPerTrip?: number | null;
}

export interface FleetUtilizationSummaryDto {
  fromUtc: string;
  toUtc: string;
  completedTrips: number;
  totalKm: number;
  totalHours: number;
  rows: FleetUtilizationRowDto[];
}

export interface FleetHealthDto {
  vehicleCategoriesCount: number;
  vehiclesCount: number;
  maintenanceEmployeesCount: number;
  employeesWithDriverLicenseCount: number;
  warnings: string[];
}

export interface FleetCostEntryDto {
  id: string;
  vehicleAssetId: string;
  vehicleName: string;
  costDateUtc: string;
  costType: string;
  amount: number;
  currencyCode?: string | null;
  notes?: string | null;
  fleetTripId?: string | null;
  workOrderId?: string | null;
  fleetExternalRepairId?: string | null;
  fleetFuelTransactionId?: string | null;
}

export interface CreateFleetCostEntryDto {
  vehicleAssetId: string;
  costDateUtc?: string | null;
  costType: string;
  amount: number;
  currencyCode?: string | null;
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
  async getEmployees(params?: { page?: number; pageSize?: number; search?: string }): Promise<EmployeeDto[]> {
    const usp = new URLSearchParams();
    usp.set('page', String(params?.page ?? 1));
    usp.set('pageSize', String(params?.pageSize ?? 50));
    if (params?.search) usp.set('search', params.search);

    const response = await fetch(`${API_BASE_URL}/employees?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

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

  async getAssignments(vehicleAssetId: string): Promise<FleetVehicleAssignmentDto[]> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/assignments/vehicle/${vehicleAssetId}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getCurrentAssignment(vehicleAssetId: string): Promise<FleetVehicleAssignmentDto | null> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/assignments/vehicle/${vehicleAssetId}/current`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async assignDriver(dto: AssignFleetDriverDto): Promise<FleetVehicleAssignmentDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/assignments`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async endAssignment(assignmentId: string, dto: EndFleetDriverAssignmentDto): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/assignments/${assignmentId}/end`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
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

  async getTripInspections(tripId: string): Promise<FleetTripInspectionDto[]> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/inspections/trip/${tripId}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async startTripInspection(dto: StartFleetTripInspectionDto): Promise<FleetTripInspectionDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/inspections/start`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async completeTripInspection(inspectionId: string, dto: CompleteFleetTripInspectionDto): Promise<FleetTripInspectionDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/inspections/${inspectionId}/complete`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async cancelTripInspection(inspectionId: string, notes?: string): Promise<void> {
    const usp = new URLSearchParams();
    if (notes) usp.set('notes', notes);
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/inspections/${inspectionId}/cancel?${usp.toString()}`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
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

  async getDefects(params?: {
    page?: number;
    pageSize?: number;
    vehicleAssetId?: string;
    status?: string;
    searchTerm?: string;
  }): Promise<PagedResult<FleetDefectDto>> {
    const usp = new URLSearchParams();
    usp.set('page', String(params?.page ?? 1));
    usp.set('pageSize', String(params?.pageSize ?? 25));
    if (params?.vehicleAssetId) usp.set('vehicleAssetId', params.vehicleAssetId);
    if (params?.status) usp.set('status', params.status);
    if (params?.searchTerm) usp.set('searchTerm', params.searchTerm);

    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/defects?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createDefect(dto: CreateFleetDefectDto): Promise<FleetDefectDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/defects`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async updateDefectStatus(defectId: string, dto: UpdateFleetDefectStatusDto): Promise<FleetDefectDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/defects/${defectId}/status`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createWorkOrderFromDefect(dto: CreateWorkOrderFromFleetDefectDto): Promise<{ workOrderId: string }> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/defects/work-orders`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getTyres(vehicleAssetId: string, page = 1, pageSize = 25): Promise<PagedResult<FleetTyreDto>> {
    const usp = new URLSearchParams();
    usp.set('page', String(page));
    usp.set('pageSize', String(pageSize));
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/tyres/vehicle/${vehicleAssetId}?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createTyre(dto: CreateFleetTyreDto): Promise<FleetTyreDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/tyres`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async updateTyre(id: string, dto: CreateFleetTyreDto): Promise<FleetTyreDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/tyres/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async deleteTyre(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/tyres/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async getTyreEvents(id: string): Promise<FleetTyreEventDto[]> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/tyres/${id}/events`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getBatteries(vehicleAssetId: string, page = 1, pageSize = 25): Promise<PagedResult<FleetBatteryDto>> {
    const usp = new URLSearchParams();
    usp.set('page', String(page));
    usp.set('pageSize', String(pageSize));
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/batteries/vehicle/${vehicleAssetId}?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getBatteryKpis(vehicleAssetId: string): Promise<FleetBatteryKpisDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/batteries/vehicle/${vehicleAssetId}/kpis`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createBattery(dto: CreateFleetBatteryDto): Promise<FleetBatteryDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/batteries`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async updateBattery(id: string, dto: CreateFleetBatteryDto): Promise<FleetBatteryDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/batteries/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async deleteBattery(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/batteries/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async getBatteryEvents(id: string): Promise<FleetBatteryEventDto[]> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/batteries/${id}/events`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getExternalRepairs(params?: { page?: number; pageSize?: number; vehicleAssetId?: string; status?: string }): Promise<PagedResult<FleetExternalRepairDto>> {
    const usp = new URLSearchParams();
    usp.set('page', String(params?.page ?? 1));
    usp.set('pageSize', String(params?.pageSize ?? 25));
    if (params?.vehicleAssetId) usp.set('vehicleAssetId', params.vehicleAssetId);
    if (params?.status) usp.set('status', params.status);
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/external-repairs?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createExternalRepair(dto: CreateFleetExternalRepairDto): Promise<FleetExternalRepairDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/external-repairs`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async updateExternalRepairStatus(id: string, dto: UpdateFleetExternalRepairStatusDto): Promise<FleetExternalRepairDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/external-repairs/${id}/status`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getDashboardSummary(vehicleAssetId?: string): Promise<FleetDashboardSummaryDto> {
    const usp = new URLSearchParams();
    if (vehicleAssetId) usp.set('vehicleAssetId', vehicleAssetId);
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/dashboard/summary?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getCosts(vehicleAssetId: string, page = 1, pageSize = 25): Promise<PagedResult<FleetCostEntryDto>> {
    const usp = new URLSearchParams();
    usp.set('page', String(page));
    usp.set('pageSize', String(pageSize));
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/costs/vehicle/${vehicleAssetId}?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async createCost(dto: CreateFleetCostEntryDto): Promise<FleetCostEntryDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/costs`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async deleteCost(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/costs/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async deleteFuel(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/fuel/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
  },

  async getCostSummary(params?: {
    fromUtc?: string;
    toUtc?: string;
    top?: number;
    vehicleAssetId?: string;
  }): Promise<FleetCostSummaryDto> {
    const usp = new URLSearchParams();
    if (params?.fromUtc) usp.set('fromUtc', params.fromUtc);
    if (params?.toUtc) usp.set('toUtc', params.toUtc);
    if (params?.top) usp.set('top', String(params.top));
    if (params?.vehicleAssetId) usp.set('vehicleAssetId', params.vehicleAssetId);

    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/reports/cost-summary?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getUtilization(params?: {
    fromUtc?: string;
    toUtc?: string;
    top?: number;
    vehicleAssetId?: string;
  }): Promise<FleetUtilizationSummaryDto> {
    const usp = new URLSearchParams();
    if (params?.fromUtc) usp.set('fromUtc', params.fromUtc);
    if (params?.toUtc) usp.set('toUtc', params.toUtc);
    if (params?.top) usp.set('top', String(params.top));
    if (params?.vehicleAssetId) usp.set('vehicleAssetId', params.vehicleAssetId);

    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/reports/utilization?${usp.toString()}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },

  async getHealth(): Promise<FleetHealthDto> {
    const response = await fetch(`${API_BASE_URL}/maintenance/fleet/health`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error(await readError(response));
    return response.json();
  },
};

export default fleetService;
