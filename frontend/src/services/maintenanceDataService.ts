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
  categoryId?: string;
  assetType?: string;
  location?: string;
  isActive: boolean;
}

export interface AssetCategory {
  id: string;
  name: string;
  code: string;
  description?: string;
  maintenanceType?: string;
  maintenanceFrequency?: string;
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
      console.log('👥 Using /api/employees endpoint (confirmed to exist in backend)');
      
      const response = await apiService.get('/employees?pageSize=1000&isActive=true');
      console.log('👥 SUCCESS! Response from /api/employees:', response);
      
      // The backend controller returns an array of EmployeeDto directly in the response body
      // but also sets headers for pagination info (X-Total-Count, X-Page, X-Page-Size)
      const employees = Array.isArray(response) ? response : [];
      
      console.log(`👥 Found ${employees.length} employees from /api/employees`);
      
      if (employees.length > 0) {
        console.log('👥 Sample employee data from /api/employees:', employees[0]);
        
        const mappedEmployees = employees.map((emp: any) => {
          const mapped = {
            id: emp.id,
            firstName: emp.firstName,
            lastName: emp.lastName,
            email: emp.emailAddress, // EmployeeDto uses emailAddress
            department: emp.departmentName, // EmployeeDto uses departmentName
            position: emp.positionTitle, // EmployeeDto uses positionTitle
            isActive: emp.isActive
          };
          console.log('👥 Mapped employee:', mapped);
          return mapped;
        });
        
        console.log(`✅ Successfully loaded ${mappedEmployees.length} employees from /api/employees`);
        return mappedEmployees;
      } else {
        console.warn('⚠️ No employees found in response');
        return [];
      }
      
    } catch (error) {
      console.error('❌ Error calling /api/employees:', error);
      console.error('❌ Error type:', typeof error);
      console.error('❌ Error constructor:', error?.constructor?.name);
      console.error('❌ Full error object keys:', Object.keys(error || {}));
      
      // Try to extract more detailed error information
      const errorDetails = {
        name: (error as any)?.name,
        message: (error as any)?.message,
        status: (error as any)?.status || (error as any)?.response?.status,
        statusText: (error as any)?.statusText || (error as any)?.response?.statusText,
        data: (error as any)?.data || (error as any)?.response?.data,
        response: (error as any)?.response,
        stack: (error as any)?.stack?.split('\n')[0] // Just first line of stack
      };
      console.error('❌ Detailed error info:', errorDetails);
      
      // Let's also test if the endpoint is reachable at all
      console.log('🗺 Testing basic endpoint accessibility...');
      try {
        const testResponse = await fetch('/api/employees', {
          method: 'HEAD', // Just test if endpoint exists
          headers: {
            'Authorization': `Bearer ${localStorage.getItem('authToken')}`,
            'Content-Type': 'application/json'
          }
        });
        console.log('🗺 Basic HEAD test - Status:', testResponse.status, 'OK:', testResponse.ok);
        console.log('🗺 Response headers:', Array.from(testResponse.headers.entries()));
      } catch (headError) {
        console.error('🗺 HEAD test also failed:', headError);
      }
      
      // Test if other endpoints work
      console.log('⚙️ Testing other working endpoints for comparison...');
      try {
        const userTest = await apiService.get('/user');
        console.log('⚙️ /user endpoint works, response length:', Array.isArray(userTest) ? userTest.length : 'not array');
      } catch (userError) {
        console.log('⚙️ /user endpoint also fails:', (userError as any)?.message);
      }
      
      // Test the maintenance/assets endpoint we know works
      try {
        const assetTest = await apiService.get('/maintenance/assets?page=1&pageSize=5');
        console.log('⚙️ /maintenance/assets endpoint works, has data:', !!assetTest?.data);
      } catch (assetError) {
        console.log('⚙️ /maintenance/assets also fails:', (assetError as any)?.message);
      }
      
      throw new Error(`Failed to fetch employees: ${errorDetails.message || errorDetails.name || 'Unknown error'}`);
    }
  }

  // Fetch technicians (users available for maintenance assignments)
  async getTechnicians(): Promise<Employee[]> {
    try {
      console.log('🔧 Fetching technicians from /user endpoint (ApplicationUser)...');
      
      try {
        const response = await apiService.get('/user');
        console.log('🔧 SUCCESS! Users response:', response);
        
        const users = Array.isArray(response) ? response : [];
        
        if (users.length > 0) {
          const mappedTechnicians = users.map((user: any) => ({
            id: user.id, // This is the UserId (ApplicationUser ID)
            firstName: user.firstName,
            lastName: user.lastName,
            email: user.email,
            department: user.department || '',
            position: user.position || '',
            isActive: user.isActive
          }));
          
          console.log(`✅ Successfully loaded ${mappedTechnicians.length} users from /user endpoint`);
          return mappedTechnicians;
        }
      } catch (userError) {
        console.warn('⚠️ /user endpoint failed:', userError);
      }
      
      // Fallback to all employees if users endpoint fails
      console.log('🔧 Falling back to employees endpoint...');
      const employees = await this.getEmployees();
      const activeTechnicians = employees.filter(emp => emp.isActive);
      
      console.log(`🔧 Using ${activeTechnicians.length} active employees as fallback technicians`);
      return activeTechnicians;
      
    } catch (error) {
      console.error('❌ Error fetching technicians:', error);
      throw error; // Re-throw so the UI can handle it
    }
  }

  // Fetch all active inventory items
  async getInventoryItems(): Promise<InventoryItem[]> {
    try {
      const response = await apiService.get('/inventoryitems');
      // Map the InventoryItemDto to InventoryItem interface
      const items = response.data || [];
      return items.map((item: any) => ({
        id: item.id,
        itemName: item.name || item.itemName,
        itemCode: item.itemCode,
        category: item.categoryName || item.category,
        location: item.locationName || item.location,
        isActive: item.isActive ?? true
      }));
    } catch (error) {
      console.error('Error fetching inventory items:', error);
      return [];
    }
  }

  // Fetch all active assets
  async getAssets(): Promise<Asset[]> {
    try {
      const response = await apiService.get('/maintenance/assets?page=1&pageSize=1000');
      // Map the MaintenanceAssetListDto to Asset interface
      const assets = response.data.items || response.data.data || response.data || [];
      console.log('🏢 Assets array before mapping:', assets);
      
      const mappedAssets = assets.map((asset: any) => ({
        id: asset.id,
        assetName: asset.name || asset.assetName, // Backend sends 'name', not 'assetName'
        assetCode: asset.assetNumber || asset.assetCode || asset.code, // Backend sends 'assetNumber', not 'assetCode'
        category: asset.categoryName || asset.category,
        categoryId: asset.assetCategoryId, // Backend now includes AssetCategoryId
        assetType: asset.assetType, // Backend now includes AssetType from category
        location: asset.location,
        isActive: asset.isActive ?? true
      }));
      
      return mappedAssets;
    } catch (error) {
      console.error('Error fetching assets:', error);
      return [];
    }
  }

  // Fetch work order types
  async getWorkOrderTypes(): Promise<WorkOrderType[]> {
    try {
      const response = await apiService.get('/maintenance/work-order-types?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching work order types:', error);
      return [];
    }
  }

  // Fetch maintenance types
  async getMaintenanceTypes(): Promise<MaintenanceType[]> {
    try {
      console.log('🔧 Fetching maintenance types...');
      const response = await apiService.get('/maintenance/maintenance-types/active');
      console.log('🔧 Maintenance types response:', response);
      
      // The active endpoint returns the data directly, not wrapped in pagination
      const types = response || [];
      console.log('🔧 Parsed maintenance types:', types);
      
      return types;
    } catch (error) {
      console.error('Error fetching maintenance types:', error);
      return [];
    }
  }

  // Fetch priority levels
  async getPriorityLevels(): Promise<PriorityLevel[]> {
    try {
      const response = await apiService.get('/maintenance/priority-levels?isActive=true');
      return response.data.data || [];
    } catch (error) {
      console.error('Error fetching priority levels:', error);
      return [];
    }
  }

  // Fetch asset categories
  async getAssetCategories(): Promise<AssetCategory[]> {
    try {
      const response = await apiService.get('/maintenance/asset-categories?pageSize=1000');
      
      // The controller returns data directly, not wrapped in data object
      const categories = response || [];
      console.log('🏷️ Categories array:', categories);
      
      const mappedCategories = categories.map((category: any) => {
        const mapped = {
          id: category.id,
          name: category.name,
          code: category.code,
          description: category.description,
          maintenanceType: category.maintenanceType,
          maintenanceFrequency: category.maintenanceFrequency,
          isActive: category.isActive ?? true
        };
        console.log('🏷️ Mapped category:', mapped);
        return mapped;
      });
      
      console.log('🏷️ Final mapped categories:', mappedCategories);
      return mappedCategories;
    } catch (error) {
      console.error('Error fetching asset categories:', error);
      return [];
    }
  }

  // Get asset type for an asset (now included directly in asset response)
  async getAssetTypeForAsset(assetId: string): Promise<string | null> {
    try {
      console.log('=== Getting asset type for asset:', assetId);
      
      const assets = await this.getAssets();
      const asset = assets.find(a => a.id === assetId);
      console.log('Found asset:', asset);
      
      if (!asset) {
        console.log('Asset not found');
        return null;
      }
      
      const assetType = asset.assetType;
      console.log('Asset type from asset:', assetType);
      
      return assetType || null;
    } catch (error) {
      console.error('Error getting asset type for asset:', error);
      return null;
    }
  }

  // Keep the old method name for backward compatibility, but redirect to assetType
  async getMaintenanceTypeForAsset(assetId: string): Promise<string | null> {
    return this.getAssetTypeForAsset(assetId);
  }

  // Get the maintenance schedule configuration from the asset's category
  // so that UIs like /maintenance/scheduled can pre-populate default triggers.
  async getAssetCategorySchedule(assetId: string): Promise<{
    maintenanceScheduleType: string;
    maintenanceType: string;
    maintenanceFrequency?: string | null;
    maintenanceValue?: number | null;
    maintenanceUnit?: string | null;
    secondaryMaintenanceType?: string | null;
    secondaryMaintenanceFrequency?: string | null;
    secondaryMaintenanceValue?: number | null;
    secondaryMaintenanceUnit?: string | null;
  } | null> {
    try {
      const response = await apiService.get(`/maintenance/assets/${assetId}`);
      const asset = response;
      const category = asset?.assetCategory;

      if (!category) {
        return null;
      }

      return {
        maintenanceScheduleType: category.maintenanceScheduleType || 'single',
        maintenanceType: category.maintenanceType || 'Time',
        maintenanceFrequency: category.maintenanceFrequency ?? null,
        maintenanceValue: typeof category.maintenanceValue === 'number' ? category.maintenanceValue : null,
        maintenanceUnit: category.maintenanceUnit ?? null,
        secondaryMaintenanceType: category.secondaryMaintenanceType ?? null,
        secondaryMaintenanceFrequency: category.secondaryMaintenanceFrequency ?? null,
        secondaryMaintenanceValue: typeof category.secondaryMaintenanceValue === 'number' ? category.secondaryMaintenanceValue : null,
        secondaryMaintenanceUnit: category.secondaryMaintenanceUnit ?? null,
      };
    } catch (error) {
      console.error('Error fetching asset category schedule for asset', assetId, error);
      return null;
    }
  }

}

export const maintenanceDataService = new MaintenanceDataService();