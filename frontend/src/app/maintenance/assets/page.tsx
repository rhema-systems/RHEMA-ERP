'use client';

import React, { useRef, useState, useEffect, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, Settings, History, MapPin } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/hooks/use-toast';
import { useRouter } from 'next/navigation';

import { format } from 'date-fns';

interface Asset {
  id: string;
  assetNumber: string;
  name: string;
  description: string;
  category: string;
  status: 'Operational' | 'Maintenance Required' | 'Out of Service' | 'Under Maintenance';
  location: string;
  manufacturer: string;
  model: string;
  serialNumber: string;
  licensePlate?: string;
  vin?: string;
  purchaseDate: string;
  warrantyExpiry: string;
  lastMaintenanceDate: string;
  nextMaintenanceDate: string;
  condition: 'Excellent' | 'Good' | 'Fair' | 'Poor';
  criticality: 'Low' | 'Medium' | 'High' | 'Critical';
  value: number;
  currentValue?: number;
}

interface MaintenanceHistory {
  id: string;
  assetId: string;
  workOrderId: string;
  date: string;
  type: string;
  technician: string;
  description: string;
  status: string;
  cost: number;
}


function AssetsPageContent() {
  const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
  const { toast } = useToast();
  const searchParams = useSearchParams();
  const [assets, setAssets] = useState<Asset[]>([]);
  const [filteredAssets, setFilteredAssets] = useState<Asset[]>([]);
  const [maintenanceHistory, setMaintenanceHistory] = useState<MaintenanceHistory[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [assetTypes, setAssetTypes] = useState<Array<{ id: string; name: string; description?: string; assetType?: string | null; isActive?: boolean }>>([]);
  const initialCreateHandledRef = useRef(false);
  const initialEditHandledRef = useRef(false);

  const [newAsset, setNewAsset] = useState({
    name: '',
    assetNumber: '',
    description: '',
    category: '',
    location: '',
    manufacturer: '',
    model: '',
    serialNumber: '',
    licensePlate: '',
    vin: '',
    purchaseDate: '',
    warrantyExpiry: '',
    criticality: 'Medium' as const,
    value: 0,
  });

  // Helper function to format date for HTML input (YYYY-MM-DD)
  const formatDateForInput = (dateString: string | null | undefined): string => {
    if (!dateString) return '';
    try {
      const date = new Date(dateString);
      if (isNaN(date.getTime())) return '';
      return date.toISOString().split('T')[0]; // Returns YYYY-MM-DD format
    } catch {
      return '';
    }
  };

  const router = useRouter();

  // Helper function to map backend assets to frontend format
  const mapAssets = (rawAssets: any[]) => {
    console.log('Raw assets for mapping:', rawAssets); // Debug logging
    // Log first asset in detail to see all available properties
    if (rawAssets.length > 0) {
      console.log('First asset detailed properties:', Object.keys(rawAssets[0]));
      console.log('First asset PurchaseDate:', rawAssets[0].purchaseDate || rawAssets[0].PurchaseDate);
      console.log('First asset WarrantyEndDate:', rawAssets[0].warrantyEndDate || rawAssets[0].WarrantyEndDate);
    }
    return rawAssets.map((asset: any) => {
      // Backend usually sends enum.ToString(): "Active", "Maintenance", "OutOfService", etc.
      const rawStatus: string = asset.status || asset.Status || 'Active';
      let mappedStatus: Asset['status'];

      switch (rawStatus) {
        case 'Active':
        case 'InUse':
          mappedStatus = 'Operational';
          break;
        case 'Maintenance':
          mappedStatus = 'Under Maintenance';
          break;
        case 'OutOfService':
        case 'Retired':
        case 'Disposed':
        case 'Inactive':
          mappedStatus = 'Out of Service';
          break;
        default:
          mappedStatus = 'Operational';
          break;
      }

      const mapped = {
        ...asset,
        assetNumber: asset.assetNumber || asset.AssetNumber || 'N/A',
        value: asset.currentValue || asset.CurrentValue || 0,
        currentValue: asset.currentValue || asset.CurrentValue || 0,
        category: asset.assetCategory?.name || asset.categoryName || asset.CategoryName || 'Unknown',
        licensePlate: asset.licensePlate || asset.LicensePlate || '',
        vin: asset.vin || asset.VIN || asset.Vin || '',
        // Map date fields with proper formatting
        purchaseDate: formatDateForInput(asset.purchaseDate || asset.PurchaseDate),
        warrantyExpiry: formatDateForInput(asset.warrantyEndDate || asset.WarrantyEndDate || asset.warrantyExpiry),
        // Map other potential field name variations
        manufacturer: asset.manufacturer || asset.Manufacturer || '',
        model: asset.model || asset.Model || '',
        serialNumber: asset.serialNumber || asset.SerialNumber || '',
        location: asset.location || asset.Location || '',
        description: asset.description || asset.Description || '',
        name: asset.name || asset.Name || '',
        status: mappedStatus,
        criticality: asset.criticality || asset.Criticality || 'Medium',
        condition: asset.condition || asset.Condition || 'Good'
      };
      console.log('Mapped asset:', mapped); // Debug logging
      return mapped;
    });
  };

  const isVehicleCategoryName = (categoryName: string | null | undefined) => {
    if (!categoryName) return false;
    return (assetTypes.find((t) => t.name === categoryName)?.assetType || '').toLowerCase() === 'vehicle';
  };

  const showVehicleFields = isVehicleCategoryName(newAsset.category);
  const selectedAssetIsVehicle = selectedAsset ? isVehicleCategoryName(selectedAsset.category) : false;

  // Load assets and maintenance history from API
  useEffect(() => {
    const loadAssetsData = async () => {
      setLoading(true);
      try {
        const token = localStorage.getItem('authToken');
        const [assetsResponse, historyResponse, categoriesResponse] = await Promise.all([
          fetch(`${API_URL}/maintenance/assets`, {
            headers: {
              'Authorization': token ? `Bearer ${token}` : '',
              'Content-Type': 'application/json'
            }
          }),
          fetch(`${API_URL}/maintenance/assets/history`, {
            headers: {
              'Authorization': token ? `Bearer ${token}` : '',
              'Content-Type': 'application/json'
            }
          }),
          fetch(`${API_URL}/maintenance/asset-categories`, {
            headers: {
              'Authorization': token ? `Bearer ${token}` : '',
              'Content-Type': 'application/json'
            }
          })
        ]);

        if (assetsResponse.ok) {
          const assetsData = await assetsResponse.json();
          const rawAssets = assetsData.data || assetsData.items || assetsData || [];
          setAssets(mapAssets(rawAssets));
        }

        if (historyResponse.ok) {
          const historyData = await historyResponse.json();
          setMaintenanceHistory(historyData.data || historyData.items || historyData || []);
        }

        console.log('Asset categories response status:', categoriesResponse.status);

        if (categoriesResponse.ok) {
          const categoriesData = await categoriesResponse.json();
          console.log('Asset categories loaded from API:', categoriesData);
          console.log('Number of asset categories:', categoriesData?.length || 0);

          if (categoriesData && categoriesData.length > 0) {
            setAssetTypes(categoriesData);
            console.log('Asset categories set successfully');
          } else {
            console.warn('API returned empty asset categories');
            setAssetTypes([]);
          }
        } else {
          console.error('Asset categories API failed:', {
            status: categoriesResponse.status,
            statusText: categoriesResponse.statusText
          });
          const errorText = await categoriesResponse.text();
          console.error('Asset categories API error details:', errorText);
          setAssetTypes([]);
        }
      } catch (error) {
        console.error('Failed to load assets data:', error);
        setAssets([]);
        setMaintenanceHistory([]);

        // Add some default asset types for testing if API fails
        console.warn('Using fallback asset types for testing');
        setAssetTypes([
          { id: 'temp-1', name: 'Electrical Equipment', description: 'Electrical systems and equipment' },
          { id: 'temp-2', name: 'Mechanical Equipment', description: 'Mechanical systems and equipment' },
          { id: 'temp-3', name: 'HVAC Equipment', description: 'Heating, ventilation, and air conditioning' },
          { id: 'temp-4', name: 'Safety Equipment', description: 'Safety and security systems' }
        ]);
      } finally {
        setLoading(false);
      }
    };

    loadAssetsData();
  }, []);

  // Handle opening asset from URL parameter and initial category filter
  useEffect(() => {
    const assetId = searchParams.get('id');
    const categoryParam = searchParams.get('category');
    const editParam = (searchParams.get('edit') || '').trim().toLowerCase();
    const shouldEdit = editParam === '1' || editParam === 'true';

    // If a specific asset ID is provided, open its details dialog once assets are loaded
    if (assetId && assets.length > 0 && !isViewDialogOpen && !shouldEdit) {
      const asset = assets.find(a => a.id === assetId);
      if (asset) {
        console.log('Opening asset from URL:', asset);
        setSelectedAsset(asset);
        setIsViewDialogOpen(true);
      } else {
        console.warn('Asset not found with ID:', assetId);
        toast({
          title: 'Asset not found',
          description: 'The requested asset could not be found.',
          variant: 'destructive'
        });
      }
    }

    // If a category is provided in the URL, use it to pre-filter the grid
    if (categoryParam && categoryFilter === 'all') {
      console.log('Applying initial category filter from URL:', categoryParam);
      setCategoryFilter(categoryParam);
    }
  }, [assets, searchParams, isViewDialogOpen, toast, categoryFilter]);

  // Optional: deep-link helper to open Create dialog pre-selected by asset type (e.g. /maintenance/assets?assetType=Vehicle&create=1)
  useEffect(() => {
    if (initialCreateHandledRef.current) return;

    const createParam = (searchParams.get('create') || '').trim().toLowerCase();
    if (createParam !== '1' && createParam !== 'true') return;
    if (assetTypes.length === 0) return;

    const assetTypeParam = (searchParams.get('assetType') || '').trim();
    if (assetTypeParam) {
      const match = assetTypes.find((t) => (t.assetType || '').toLowerCase() === assetTypeParam.toLowerCase());
      if (match) {
        setNewAsset((prev) => ({ ...prev, category: match.name }));
        if (categoryFilter === 'all') setCategoryFilter(match.name);
      }
    }

    initialCreateHandledRef.current = true;
    setIsCreateDialogOpen(true);
  }, [assetTypes, categoryFilter, searchParams]);

  // Filter assets
  useEffect(() => {
    let filtered = assets;

    if (searchTerm) {
      filtered = filtered.filter(asset =>
        asset.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        asset.location.toLowerCase().includes(searchTerm.toLowerCase()) ||
        asset.manufacturer.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (categoryFilter && categoryFilter !== 'all') {
      filtered = filtered.filter(asset => asset.category === categoryFilter);
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter(asset => asset.status === statusFilter);
    }

    setFilteredAssets(filtered);
  }, [assets, searchTerm, categoryFilter, statusFilter]);

  const handleCreateAsset = async () => {
    try {
      // Find the selected asset type
      const selectedType = assetTypes.find(type => type.name === newAsset.category);
      const typeId = selectedType?.id || assetTypes[0]?.id;
      const isVehicleCategory = (selectedType?.assetType || '').toLowerCase() === 'vehicle';

      // Log debugging info
      console.log('Creating asset with data:', {
        newAsset,
        selectedType,
        typeId,
        assetTypes: assetTypes.slice(0, 3) // Log first 3 types
      });

      // Validate required fields
      if (!newAsset.name?.trim()) {
        toast({
          title: "Validation Error",
          description: "Asset name is required",
          variant: "destructive"
        });
        return;
      }

      if (!typeId) {
        toast({
          title: "Validation Error",
          description: "Asset type is required",
          variant: "destructive"
        });
        return;
      }

      // Validate that we have actual asset types from the API
      if (assetTypes.length === 0) {
        toast({
          title: "Configuration Error",
          description: "No asset types available. Please contact your administrator.",
          variant: "destructive"
        });
        return;
      }

      const token = localStorage.getItem('authToken');

      // Basic API connectivity test
      try {
        const testResponse = await fetch(`${API_URL}/maintenance/assets`, {
          method: 'GET',
          headers: {
            'Authorization': token ? `Bearer ${token}` : '',
            'Content-Type': 'application/json'
          }
        });
        console.log('Assets API connectivity test - Status:', testResponse.status);
        if (!testResponse.ok && testResponse.status !== 401) {
          toast({
            title: "API Error",
            description: `API is not responding properly. Status: ${testResponse.status}`,
            variant: "destructive"
          });
          return;
        }
      } catch (apiError) {
        console.error('API connectivity failed:', apiError);
        toast({
          title: "Connection Error",
          description: "Cannot connect to the API server. Please check if the backend is running.",
          variant: "destructive"
        });
        return;
      }

      const payload = {
        name: newAsset.name.trim(),
        assetNumber: newAsset.assetNumber?.trim() || '', // Use form value or empty for auto-generation
        description: newAsset.description?.trim() || '',
        assetCategoryId: typeId,
        manufacturer: newAsset.manufacturer?.trim() || null,
        model: newAsset.model?.trim() || null,
        serialNumber: newAsset.serialNumber?.trim() || null,
        licensePlate: isVehicleCategory ? (newAsset.licensePlate?.trim() || null) : null,
        vin: isVehicleCategory ? (newAsset.vin?.trim() || null) : null,
        location: newAsset.location?.trim() || null,
        status: 'Active',
        criticality: newAsset.criticality || 'Medium',
        purchaseDate: newAsset.purchaseDate || null,
        warrantyEndDate: newAsset.warrantyExpiry || null,
        currentValue: newAsset.value || null
      };

      console.log('Sending payload:', payload);
      console.log('Token available:', !!token);
      console.log('Request URL:', `${API_URL}/maintenance/assets`);

      const response = await fetch(`${API_URL}/maintenance/assets`, {
        method: 'POST',
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        let errorDetails;
        let errorJson = null;
        try {
          const responseClone = response.clone();
          try {
            errorJson = await response.json();
            errorDetails = JSON.stringify(errorJson, null, 2);
          } catch {
            errorDetails = await responseClone.text();
          }
        } catch (e) {
          errorDetails = 'Could not read error response';
        }

        const errorInfo = {
          status: response.status,
          statusText: response.statusText,
          error: errorDetails,
          token: token ? `Token present (${token.substring(0, 20)}...)` : 'No token found',
          payload: JSON.stringify(payload, null, 2)
        };

        console.error('Asset creation failed:');
        console.error('Status:', response.status);
        console.error('Status Text:', response.statusText);
        console.error('Error Details:', errorDetails);
        console.error('Payload:', payload);

        // Try to parse error details for more specific message
        let userMessage = `Failed to create asset: ${response.status} ${response.statusText}`;
        if (errorDetails && errorDetails.includes('AssetCategory')) {
          userMessage = 'Invalid asset category. Please try selecting a different category.';
        } else if (errorDetails && errorDetails.includes('unique')) {
          userMessage = 'Asset number already exists. Please use a different number.';
        }

        toast({
          title: "Error",
          description: userMessage,
          variant: "destructive"
        });
        throw new Error(userMessage);
      }

      // Refresh the list
      const assetsResponse = await fetch(`${API_URL}/maintenance/assets`, {
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        }
      });

      if (assetsResponse.ok) {
        const assetsData = await assetsResponse.json();
        const rawAssets = assetsData.data || assetsData.items || assetsData || [];
        setAssets(mapAssets(rawAssets));
      }

      setIsCreateDialogOpen(false);
      setNewAsset({
        name: '',
        assetNumber: '',
        description: '',
        category: assetTypes.length > 0 ? assetTypes[0].name : '',
        location: '',
        manufacturer: '',
        model: '',
        serialNumber: '',
        licensePlate: '',
        vin: '',
        purchaseDate: '',
        warrantyExpiry: '',
        criticality: 'Medium',
        value: 0,
      });
    } catch (error) {
      console.error('Error creating asset:', error);
    }
  };

  const handleEditAsset = (asset: Asset) => {
    setSelectedAsset(asset);
    setNewAsset({
      name: asset.name || '',
      assetNumber: asset.assetNumber || '',
      description: asset.description || '',
      category: asset.category || '',
      location: asset.location || '',
      manufacturer: asset.manufacturer || '',
      model: asset.model || '',
      serialNumber: asset.serialNumber || '',
      licensePlate: asset.licensePlate || '',
      vin: asset.vin || '',
      purchaseDate: formatDateForInput(asset.purchaseDate),
      warrantyExpiry: formatDateForInput(asset.warrantyExpiry),
      criticality: asset.criticality || 'Medium',
      value: asset.value || 0,
    });
    setIsEditDialogOpen(true);
  };

  // Deep-link helper to open edit dialog for a specific asset (e.g. /maintenance/assets?id={id}&edit=1)
  useEffect(() => {
    if (initialEditHandledRef.current) return;

    const assetId = (searchParams.get('id') || '').trim();
    const editParam = (searchParams.get('edit') || '').trim().toLowerCase();
    const shouldEdit = editParam === '1' || editParam === 'true';

    if (!shouldEdit) return;
    if (!assetId) return;
    if (assets.length === 0) return;

    const asset = assets.find((a) => a.id === assetId);
    initialEditHandledRef.current = true;

    if (!asset) {
      toast({
        title: 'Asset not found',
        description: 'The requested asset could not be found.',
        variant: 'destructive',
      });
      return;
    }

    handleEditAsset(asset);
  }, [assets, searchParams, toast]);

  const handleUpdateAsset = async () => {
    if (!selectedAsset?.id) return;

    try {
      const token = localStorage.getItem('authToken');
      const selectedType = assetTypes.find(type => type.name === newAsset.category);
      const assetCategoryId = selectedType?.id || assetTypes[0]?.id || '';
      const isVehicleCategory = (selectedType?.assetType || '').toLowerCase() === 'vehicle';

      const response = await fetch(`${API_URL}/maintenance/assets/${selectedAsset.id}`, {
        method: 'PUT',
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          name: newAsset.name,
          description: newAsset.description,
          assetCategoryId,
          manufacturer: newAsset.manufacturer,
          model: newAsset.model,
          serialNumber: newAsset.serialNumber,
          licensePlate: isVehicleCategory ? (newAsset.licensePlate?.trim() || null) : null,
          vin: isVehicleCategory ? (newAsset.vin?.trim() || null) : null,
          location: newAsset.location,
          status: 'Active',
          criticality: newAsset.criticality,
          purchaseDate: newAsset.purchaseDate || null,
          warrantyEndDate: newAsset.warrantyExpiry || null,
          currentValue: newAsset.value || null
        })
      });

      if (!response.ok) {
        throw new Error('Failed to update asset');
      }

      // Refresh the list
      const assetsResponse = await fetch(`${API_URL}/maintenance/assets`, {
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        }
      });

      if (assetsResponse.ok) {
        const assetsData = await assetsResponse.json();
        const rawAssets = assetsData.data || assetsData.items || assetsData || [];
        setAssets(mapAssets(rawAssets));
      }

      setIsEditDialogOpen(false);
      setSelectedAsset(null);
      setNewAsset({
        name: '',
        assetNumber: '',
        description: '',
        category: assetTypes.length > 0 ? assetTypes[0].name : '',
        location: '',
        manufacturer: '',
        model: '',
        serialNumber: '',
        licensePlate: '',
        vin: '',
        purchaseDate: '',
        warrantyExpiry: '',
        criticality: 'Medium',
        value: 0,
      });
    } catch (error) {
      console.error('Error updating asset:', error);
    }
  };

  const handleDeleteAsset = async (id: string) => {
    if (!confirm('Are you sure you want to delete this asset?')) return;

    try {
      const token = localStorage.getItem('authToken');
      const response = await fetch(`${API_URL}/maintenance/assets/${id}`, {
        method: 'DELETE',
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error('Failed to delete asset');
      }

      // Refresh the list
      const assetsResponse = await fetch(`${API_URL}/maintenance/assets`, {
        headers: {
          'Authorization': token ? `Bearer ${token}` : '',
          'Content-Type': 'application/json'
        }
      });

      if (assetsResponse.ok) {
        const assetsData = await assetsResponse.json();
        const rawAssets = assetsData.data || assetsData.items || assetsData || [];
        setAssets(mapAssets(rawAssets));
      }
    } catch (error) {
      console.error('Error deleting asset:', error);
    }
  };

  const getStatusBadge = (status: Asset['status']) => {
    const colors = {
      'Operational': 'bg-green-100 text-green-800',
      'Maintenance Required': 'bg-yellow-100 text-yellow-800',
      'Out of Service': 'bg-red-100 text-red-800',
      'Under Maintenance': 'bg-blue-100 text-blue-800',
    };

    return (
      <Badge className={colors[status]}>
        {status}
      </Badge>
    );
  };

  const getConditionBadge = (condition: Asset['condition']) => {
    const colors = {
      'Excellent': 'bg-green-100 text-green-800',
      'Good': 'bg-blue-100 text-blue-800',
      'Fair': 'bg-yellow-100 text-yellow-800',
      'Poor': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[condition]}>
        {condition}
      </Badge>
    );
  };

  const getCriticalityBadge = (criticality: Asset['criticality']) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[criticality]}>
        {criticality}
      </Badge>
    );
  };

  const getAssetMaintenanceHistory = (assetId: string) => {
    return maintenanceHistory.filter(record => record.assetId === assetId);
  };

  const formatDate = (value: string | Date | null | undefined) => {
    if (!value) return '';
    const d = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(d.getTime())) return '';
    return format(d, 'MMM dd, yyyy');
  };

  const isMaintenanceOverdue = (nextMaintenanceDate: string | Date | null | undefined) => {
    if (!nextMaintenanceDate) return false;
    const d = nextMaintenanceDate instanceof Date ? nextMaintenanceDate : new Date(nextMaintenanceDate);
    if (Number.isNaN(d.getTime())) return false;
    return d < new Date();
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Asset Management</h1>
          <p className="text-muted-foreground">
            Manage and track all maintenance assets and their conditions
          </p>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Assets</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="flex items-center justify-between">
        <div></div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Asset
            </Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Add New Asset</DialogTitle>
              <DialogDescription>
                Register a new asset for maintenance management.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Asset Name</Label>
                  <Input
                    id="name"
                    value={newAsset.name}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, name: e.target.value }))}
                    placeholder="Asset name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="assetNumber">Asset Number (Optional)</Label>
                  <Input
                    id="assetNumber"
                    value={newAsset.assetNumber}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, assetNumber: e.target.value }))}
                    placeholder="Leave empty to auto-generate"
                  />
                  <p className="text-xs text-muted-foreground">
                    If left empty, will be auto-generated based on asset type
                  </p>
                </div>
              </div>
              <div className="grid grid-cols-1 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="category">Asset Category</Label>
                  <Select value={newAsset.category} onValueChange={(value) => setNewAsset(prev => ({ ...prev, category: value }))}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {assetTypes.map((type) => (
                        <SelectItem key={type.id} value={type.name}>
                          {type.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={newAsset.description}
                  onChange={(e) => setNewAsset(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Detailed description of the asset"
                  rows={3}
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="location">Location</Label>
                  <Input
                    id="location"
                    value={newAsset.location}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, location: e.target.value }))}
                    placeholder="Asset location"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="criticality">Criticality</Label>
                  <Select value={newAsset.criticality} onValueChange={(value: any) => setNewAsset(prev => ({ ...prev, criticality: value }))}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Low">Low</SelectItem>
                      <SelectItem value="Medium">Medium</SelectItem>
                      <SelectItem value="High">High</SelectItem>
                      <SelectItem value="Critical">Critical</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="manufacturer">Manufacturer</Label>
                  <Input
                    id="manufacturer"
                    value={newAsset.manufacturer}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, manufacturer: e.target.value }))}
                    placeholder="Manufacturer"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="model">Model</Label>
                  <Input
                    id="model"
                    value={newAsset.model}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, model: e.target.value }))}
                    placeholder="Model number"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="serialNumber">Serial Number</Label>
                  <Input
                    id="serialNumber"
                    value={newAsset.serialNumber}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, serialNumber: e.target.value }))}
                    placeholder="Serial number"
                  />
                </div>
              </div>
              {showVehicleFields && (
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="licensePlate">Plate Number</Label>
                    <Input
                      id="licensePlate"
                      value={newAsset.licensePlate}
                      onChange={(e) => setNewAsset(prev => ({ ...prev, licensePlate: e.target.value }))}
                      placeholder="Plate number"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="vin">VIN</Label>
                    <Input
                      id="vin"
                      value={newAsset.vin}
                      onChange={(e) => setNewAsset(prev => ({ ...prev, vin: e.target.value }))}
                      placeholder="Vehicle identification number"
                    />
                  </div>
                </div>
              )}
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="purchaseDate">Purchase Date</Label>
                  <Input
                    id="purchaseDate"
                    type="date"
                    value={newAsset.purchaseDate}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, purchaseDate: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="warrantyExpiry">Warranty Expiry</Label>
                  <Input
                    id="warrantyExpiry"
                    type="date"
                    value={newAsset.warrantyExpiry}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, warrantyExpiry: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="value">Asset Value ($)</Label>
                  <Input
                    id="value"
                    type="number"
                    min="0"
                    step="100"
                    value={newAsset.value}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, value: parseFloat(e.target.value) || 0 }))}
                    placeholder="0"
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateAsset}>
                Add Asset
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Edit Asset Dialog */}
        <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Edit Asset</DialogTitle>
              <DialogDescription>
                Update the asset information.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4">
              {selectedAsset && (
                <div className="bg-muted p-3 rounded">
                  <Label className="text-sm font-medium text-muted-foreground">Asset Number</Label>
                  <p className="text-sm font-mono mt-1">{selectedAsset.assetNumber}</p>
                </div>
              )}
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-name">Asset Name</Label>
                  <Input
                    id="edit-name"
                    value={newAsset.name}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, name: e.target.value }))}
                    placeholder="Asset name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-category">Asset Category</Label>
                  <Select value={newAsset.category} onValueChange={(value) => setNewAsset(prev => ({ ...prev, category: value }))}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {assetTypes.map((type) => (
                        <SelectItem key={type.id} value={type.name}>
                          {type.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-description">Description</Label>
                <Textarea
                  id="edit-description"
                  value={newAsset.description}
                  onChange={(e) => setNewAsset(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Detailed description of the asset"
                  rows={3}
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-location">Location</Label>
                  <Input
                    id="edit-location"
                    value={newAsset.location}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, location: e.target.value }))}
                    placeholder="Physical location"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-criticality">Criticality</Label>
                  <Select value={newAsset.criticality} onValueChange={(value) => setNewAsset(prev => ({ ...prev, criticality: value as Asset['criticality'] }))}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Low">Low</SelectItem>
                      <SelectItem value="Medium">Medium</SelectItem>
                      <SelectItem value="High">High</SelectItem>
                      <SelectItem value="Critical">Critical</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-manufacturer">Manufacturer</Label>
                  <Input
                    id="edit-manufacturer"
                    value={newAsset.manufacturer}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, manufacturer: e.target.value }))}
                    placeholder="Manufacturer"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-model">Model</Label>
                  <Input
                    id="edit-model"
                    value={newAsset.model}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, model: e.target.value }))}
                    placeholder="Model number"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-serial">Serial Number</Label>
                  <Input
                    id="edit-serial"
                    value={newAsset.serialNumber}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, serialNumber: e.target.value }))}
                    placeholder="Serial number"
                  />
                </div>
              </div>
              {showVehicleFields && (
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="edit-licensePlate">Plate Number</Label>
                    <Input
                      id="edit-licensePlate"
                      value={newAsset.licensePlate}
                      onChange={(e) => setNewAsset(prev => ({ ...prev, licensePlate: e.target.value }))}
                      placeholder="Plate number"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="edit-vin">VIN</Label>
                    <Input
                      id="edit-vin"
                      value={newAsset.vin}
                      onChange={(e) => setNewAsset(prev => ({ ...prev, vin: e.target.value }))}
                      placeholder="Vehicle identification number"
                    />
                  </div>
                </div>
              )}
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-purchaseDate">Purchase Date</Label>
                  <Input
                    id="edit-purchaseDate"
                    type="date"
                    value={newAsset.purchaseDate}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, purchaseDate: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-warrantyExpiry">Warranty Expiry</Label>
                  <Input
                    id="edit-warrantyExpiry"
                    type="date"
                    value={newAsset.warrantyExpiry}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, warrantyExpiry: e.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-value">Asset Value ($)</Label>
                  <Input
                    id="edit-value"
                    type="number"
                    min="0"
                    step="100"
                    value={newAsset.value}
                    onChange={(e) => setNewAsset(prev => ({ ...prev, value: parseFloat(e.target.value) || 0 }))}
                    placeholder="0"
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleUpdateAsset}>
                Update Asset
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {/* Asset Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Assets</CardTitle>
            <Settings className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{assets.length}</div>
            <p className="text-xs text-muted-foreground">
              Registered assets
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Operational</CardTitle>
            <Settings className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">
              {assets.filter(a => a.status === 'Operational').length}
            </div>
            <p className="text-xs text-muted-foreground">
              Currently operational
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Needs Maintenance</CardTitle>
            <AlertCircle className="h-4 w-4 text-yellow-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-yellow-600">
              {assets.filter(a => a.status === 'Maintenance Required' || isMaintenanceOverdue(a.nextMaintenanceDate)).length}
            </div>
            <p className="text-xs text-muted-foreground">
              Requires attention
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Out of Service</CardTitle>
            <AlertCircle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">
              {assets.filter(a => a.status === 'Out of Service').length}
            </div>
            <p className="text-xs text-muted-foreground">
              Not operational
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="p-4">
          <div className="flex items-center space-x-4">
            <div className="flex-1 max-w-sm">
              <Label htmlFor="search" className="sr-only">Search</Label>
              <div className="relative">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  id="search"
                  placeholder="Search assets..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="category-filter" className="text-sm">Asset Category</Label>
              <Select value={categoryFilter} onValueChange={setCategoryFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Asset Categories" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Asset Categories</SelectItem>
                  {assetTypes.map((type) => (
                    <SelectItem key={type.id} value={type.name}>
                      {type.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="status-filter" className="text-sm">Status</Label>
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="Operational">Operational</SelectItem>
                  <SelectItem value="Maintenance Required">Maintenance Required</SelectItem>
                  <SelectItem value="Under Maintenance">Under Maintenance</SelectItem>
                  <SelectItem value="Out of Service">Out of Service</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Assets Table */}
      <Card>
        <CardHeader>
          <CardTitle>Assets ({filteredAssets.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Asset Number</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Asset Type</TableHead>
                <TableHead>Location</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Condition</TableHead>
                <TableHead>Criticality</TableHead>
                <TableHead>Next Maintenance</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredAssets.map((asset) => (
                <TableRow key={asset.id}>
                  <TableCell>
                    <div className="font-mono text-sm font-medium">
                      {asset.assetNumber}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div>
                      <p className="font-medium">{asset.name}</p>
                      <p className="text-sm text-muted-foreground">{asset.manufacturer} {asset.model}</p>
                    </div>
                  </TableCell>
                  <TableCell>{asset.category}</TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-1">
                      <MapPin className="h-3 w-3 text-muted-foreground" />
                      <span className="text-sm">{asset.location}</span>
                    </div>
                  </TableCell>
                  <TableCell>{getStatusBadge(asset.status)}</TableCell>
                  <TableCell>{getConditionBadge(asset.condition)}</TableCell>
                  <TableCell>{getCriticalityBadge(asset.criticality)}</TableCell>
                  <TableCell>
                    <div className="text-sm">
                      {asset.nextMaintenanceDate ? (
                        (() => {
                          const next = new Date(asset.nextMaintenanceDate);
                          const overdue = isMaintenanceOverdue(next);
                          return (
                            <div className={overdue ? 'text-red-600 font-medium' : ''}>
                              {formatDate(next)}
                              {overdue && (
                                <div className="text-xs">Overdue</div>
                              )}
                            </div>
                          );
                        })()
                      ) : (
                        'Not scheduled'
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setSelectedAsset(asset);
                          setIsViewDialogOpen(true);
                        }}
                      >
                        <Eye className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => router.push(`/maintenance/asset-admission?assetId=${asset.id}`)}
                        className="text-xs"
                      >
                        Admit
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => handleEditAsset(asset)}
                      >
                        <Edit className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* View Asset Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Asset Details</DialogTitle>
            <DialogDescription>
              Complete information and maintenance history for this asset
            </DialogDescription>
          </DialogHeader>
          {selectedAsset && (
            <Tabs defaultValue="details" className="space-y-4">
              <div className="h-[60vh] flex flex-col">
                <TabsList>
                  <TabsTrigger value="details">Asset Details</TabsTrigger>
                  <TabsTrigger value="schedule">Maintenance Schedule</TabsTrigger>
                  <TabsTrigger value="history">Maintenance History</TabsTrigger>
                </TabsList>

                <div className="flex-1 overflow-y-auto mt-2">
                <TabsContent value="details" className="space-y-4">
                <div className="grid grid-cols-2 gap-6">
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Asset Number</Label>
                      <p className="text-sm font-mono bg-muted p-2 rounded">{selectedAsset.assetNumber || 'N/A'}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Asset Name</Label>
                      <p className="text-sm font-medium">{selectedAsset.name || 'N/A'}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Description</Label>
                      <p className="text-sm">{selectedAsset.description || 'No description'}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Location</Label>
                      <p className="text-sm">{selectedAsset.location || 'Not specified'}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Category</Label>
                      <p className="text-sm">{selectedAsset.category || 'Uncategorized'}</p>
                    </div>
                  </div>
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                      <div className="pt-1">{getStatusBadge(selectedAsset.status)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Condition</Label>
                      <div className="pt-1">{getConditionBadge(selectedAsset.condition)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Criticality</Label>
                      <div className="pt-1">{getCriticalityBadge(selectedAsset.criticality)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Asset Value</Label>
                      <p className="text-sm">
                        {selectedAsset.currentValue
                          ? `$${selectedAsset.currentValue.toLocaleString()}`
                          : 'N/A'
                        }
                      </p>
                    </div>
                  </div>
                </div>

                <div className="border-t pt-4">
                  <h4 className="text-sm font-medium mb-4">Technical Information</h4>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Manufacturer</Label>
                      <p className="text-sm">{selectedAsset.manufacturer || 'Not specified'}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Model</Label>
                      <p className="text-sm">{selectedAsset.model || 'Not specified'}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Serial Number</Label>
                      <p className="text-sm">{selectedAsset.serialNumber || 'Not specified'}</p>
                    </div>
                  </div>
                  {selectedAssetIsVehicle && (
                    <div className="mt-4 grid grid-cols-2 gap-4">
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Plate Number</Label>
                        <p className="text-sm">{selectedAsset.licensePlate || 'Not specified'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">VIN</Label>
                        <p className="text-sm">{selectedAsset.vin || 'Not specified'}</p>
                      </div>
                    </div>
                  )}
                </div>

                <div className="border-t pt-4">
                  <h4 className="text-sm font-medium mb-4">Dates & Warranty</h4>
                  <div className="grid grid-cols-3 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Purchase Date</Label>
                      <p className="text-sm">
                        {selectedAsset.purchaseDate
                          ? formatDate(selectedAsset.purchaseDate)
                          : 'Not specified'
                        }
                      </p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Warranty Expiry</Label>
                      <p className="text-sm">
                        {selectedAsset.warrantyExpiry
                          ? formatDate(selectedAsset.warrantyExpiry)
                          : 'Not specified'
                        }
                      </p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Last Maintenance</Label>
                      <p className="text-sm">
                        {selectedAsset.lastMaintenanceDate
                          ? formatDate(selectedAsset.lastMaintenanceDate)
                          : 'Never'
                        }
                      </p>
                    </div>
                  </div>
                </div>
              </TabsContent>

              <TabsContent value="schedule">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Maintenance Schedule</h4>
                  <div className="border rounded-lg p-4">
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="font-medium text-sm">Next Scheduled Maintenance</p>
                        <p className="text-sm text-muted-foreground">
                          {selectedAsset.nextMaintenanceDate
                            ? formatDate(selectedAsset.nextMaintenanceDate)
                            : 'Not scheduled'
                          }
                        </p>
                      </div>
                      <div className="flex space-x-2">
                        <Button size="sm" variant="outline">
                          <Calendar className="h-4 w-4 mr-2" />
                          Schedule Maintenance
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>
              </TabsContent>

              <TabsContent value="history">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Maintenance History</h4>
                  <div className="space-y-3">
                    {getAssetMaintenanceHistory(selectedAsset.id).map((record) => (
                      <div key={record.id} className="border rounded-lg p-4">
                        <div className="flex justify-between items-start">
                          <div>
                            <p className="font-medium text-sm">{record.description}</p>
                            <div className="flex items-center space-x-4 mt-2 text-xs text-muted-foreground">
                              <span>{formatDate(record.date)}</span>
                              <span>{record.type}</span>
                              <span>{record.technician}</span>
                              <span>WO: {record.workOrderId}</span>
                            </div>
                          </div>
                          <div className="text-right">
                            <Badge variant="outline" className="mb-1">{record.status}</Badge>
                            <p className="text-sm font-medium">${record.cost}</p>
                          </div>
                        </div>
                      </div>
                    ))}
                    {getAssetMaintenanceHistory(selectedAsset.id).length === 0 && (
                      <p className="text-sm text-muted-foreground text-center py-8">
                        No maintenance history found for this asset.
                      </p>
                    )}
                  </div>
                </div>
              </TabsContent>
              </div>
            </div>
            </Tabs>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

export default function AssetsPage() {
  return (
    <Suspense fallback={<div className="flex items-center justify-center h-screen">Loading...</div>}>
      <AssetsPageContent />
    </Suspense>
  );
}
