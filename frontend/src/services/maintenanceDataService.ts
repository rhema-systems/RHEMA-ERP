// Service for fetching data needed for maintenance operations
import { apiService } from './api.service';

export interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  department?: string;
  position?: string;
  isActive: boolean;
}

export interface InventoryItem {
  id: string;
  itemName: string;
  itemCode: string;
  category?: string;
  location?: string;
  isActive: boolean;
}

export interface Asset {
  id: string;
  assetName: string;
  assetCode: string;
  category?: string;
  location?: string;
  isActive: boolean;
}

export interface WorkOrderType {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface MaintenanceType {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface PriorityLevel {
  id: string;
  name: string;
  level: number;
  color: string;
  isActive: boolean;
}

class MaintenanceDataService {
  // Fetch all active employees
  async getEmployees(): Promise<Employee[]> {
    try {
      const response = await apiService.get('/employees?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching employees:', error);
      // Return mock data as fallback
      return this.getMockEmployees();
    }
  }

  // Fetch technicians (employees in maintenance department or with technical roles)
  async getTechnicians(): Promise<Employee[]> {
    try {
      const employees = await this.getEmployees();
      // Filter for maintenance department or technical positions
      return employees.filter(emp => 
        emp.department?.toLowerCase().includes('maintenance') ||
        emp.department?.toLowerCase().includes('technical') ||
        emp.position?.toLowerCase().includes('technician') ||
        emp.position?.toLowerCase().includes('engineer') ||
        emp.position?.toLowerCase().includes('mechanic')
      );
    } catch (error) {
      console.error('Error fetching technicians:', error);
      return this.getMockTechnicians();
    }
  }

  // Fetch all active inventory items
  async getInventoryItems(): Promise<InventoryItem[]> {
    try {
      const response = await apiService.get('/inventory?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching inventory items:', error);
      return this.getMockInventoryItems();
    }
  }

  // Fetch all active assets
  async getAssets(): Promise<Asset[]> {
    try {
      const response = await apiService.get('/assets?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching assets:', error);
      return this.getMockAssets();
    }
  }

  // Fetch work order types
  async getWorkOrderTypes(): Promise<WorkOrderType[]> {
    try {
      const response = await apiService.get('/maintenance/work-order-types?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching work order types:', error);
      return this.getMockWorkOrderTypes();
    }
  }

  // Fetch maintenance types
  async getMaintenanceTypes(): Promise<MaintenanceType[]> {
    try {
      const response = await apiService.get('/maintenance/maintenance-types?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching maintenance types:', error);
      return this.getMockMaintenanceTypes();
    }
  }

  // Fetch priority levels
  async getPriorityLevels(): Promise<PriorityLevel[]> {
    try {
      const response = await apiService.get('/maintenance/priority-levels?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching priority levels:', error);
      return this.getMockPriorityLevels();
    }
  }

  // Mock data fallbacks
  private getMockEmployees(): Employee[] {
    return [
      { id: '1', firstName: 'John', lastName: 'Smith', email: 'john.smith@company.com', department: 'Maintenance', position: 'Senior Technician', isActive: true },
      { id: '2', firstName: 'Mike', lastName: 'Johnson', email: 'mike.johnson@company.com', department: 'Maintenance', position: 'Maintenance Technician', isActive: true },
      { id: '3', firstName: 'Sarah', lastName: 'Davis', email: 'sarah.davis@company.com', department: 'Technical', position: 'Systems Engineer', isActive: true },
      { id: '4', firstName: 'Tom', lastName: 'Wilson', email: 'tom.wilson@company.com', department: 'Maintenance', position: 'Electrical Technician', isActive: true },
      { id: '5', firstName: 'Lisa', lastName: 'Brown', email: 'lisa.brown@company.com', department: 'Operations', position: 'Operations Manager', isActive: true },
      { id: '6', firstName: 'David', lastName: 'Garcia', email: 'david.garcia@company.com', department: 'Maintenance', position: 'HVAC Specialist', isActive: true },
    ];
  }

  private getMockTechnicians(): Employee[] {
    return this.getMockEmployees().filter(emp => 
      emp.department?.toLowerCase().includes('maintenance') ||
      emp.department?.toLowerCase().includes('technical') ||
      emp.position?.toLowerCase().includes('technician') ||
      emp.position?.toLowerCase().includes('engineer') ||
      emp.position?.toLowerCase().includes('specialist')
    );
  }

  private getMockInventoryItems(): InventoryItem[] {
    return [
      { id: '1', itemName: 'Engine Oil - 5W30', itemCode: 'OIL-5W30-001', category: 'Lubricants', location: 'Warehouse A-1', isActive: true },
      { id: '2', itemName: 'Air Filter - Standard', itemCode: 'FILTER-AIR-001', category: 'Filters', location: 'Warehouse A-2', isActive: true },
      { id: '3', itemName: 'Hydraulic Fluid', itemCode: 'HYD-FLUID-001', category: 'Hydraulics', location: 'Warehouse B-1', isActive: true },
      { id: '4', itemName: 'Bearing Set - 6203', itemCode: 'BEARING-6203', category: 'Mechanical Parts', location: 'Warehouse C-1', isActive: true },
      { id: '5', itemName: 'V-Belt - A38', itemCode: 'BELT-A38-001', category: 'Belts', location: 'Warehouse A-3', isActive: true },
      { id: '6', itemName: 'Electrical Wire 12AWG', itemCode: 'WIRE-12AWG-001', category: 'Electrical', location: 'Warehouse D-1', isActive: true },
    ];
  }

  private getMockAssets(): Asset[] {
    return [
      { id: '1', assetName: 'Main Building HVAC Unit A', assetCode: 'HVAC-001', category: 'HVAC Systems', location: 'Main Building - Roof', isActive: true },
      { id: '2', assetName: 'Backup Generator B2', assetCode: 'GEN-002', category: 'Power Generation', location: 'Generator Room', isActive: true },
      { id: '3', assetName: 'Main Elevator C1', assetCode: 'ELEV-001', category: 'Vertical Transportation', location: 'Main Building - Lobby', isActive: true },
      { id: '4', assetName: 'Building Fire System', assetCode: 'FIRE-001', category: 'Safety Systems', location: 'Throughout Building', isActive: true },
      { id: '5', assetName: 'Chiller Unit #1', assetCode: 'CHILL-001', category: 'HVAC Systems', location: 'Mechanical Room', isActive: true },
      { id: '6', assetName: 'Air Compressor #3', assetCode: 'COMP-003', category: 'Compressed Air', location: 'Workshop', isActive: true },
    ];
  }

  private getMockWorkOrderTypes(): WorkOrderType[] {
    return [
      { id: '1', name: 'Preventive Maintenance', description: 'Scheduled preventive maintenance tasks', isActive: true },
      { id: '2', name: 'Corrective Maintenance', description: 'Repair and corrective maintenance', isActive: true },
      { id: '3', name: 'Emergency Repair', description: 'Emergency maintenance and repairs', isActive: true },
      { id: '4', name: 'Safety Inspection', description: 'Safety and compliance inspections', isActive: true },
      { id: '5', name: 'Calibration', description: 'Equipment calibration and testing', isActive: true },
      { id: '6', name: 'Installation', description: 'New equipment installation', isActive: true },
    ];
  }

  private getMockMaintenanceTypes(): MaintenanceType[] {
    return [
      { id: '1', name: 'Preventive', description: 'Scheduled preventive maintenance', isActive: true },
      { id: '2', name: 'Corrective', description: 'Corrective maintenance and repairs', isActive: true },
      { id: '3', name: 'Predictive', description: 'Predictive maintenance based on monitoring', isActive: true },
      { id: '4', name: 'Emergency', description: 'Emergency maintenance', isActive: true },
      { id: '5', name: 'Safety', description: 'Safety-related maintenance', isActive: true },
      { id: '6', name: 'Inspection', description: 'Inspection and testing', isActive: true },
    ];
  }

  private getMockPriorityLevels(): PriorityLevel[] {
    return [
      { id: '1', name: 'Low', level: 1, color: 'green', isActive: true },
      { id: '2', name: 'Medium', level: 2, color: 'blue', isActive: true },
      { id: '3', name: 'High', level: 3, color: 'orange', isActive: true },
      { id: '4', name: 'Critical', level: 4, color: 'red', isActive: true },
    ];
  }
}

export const maintenanceDataService = new MaintenanceDataService();