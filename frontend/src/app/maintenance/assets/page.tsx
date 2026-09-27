'use client';

import { GlobalSearchRecordOpener } from '@/components/global-search/GlobalSearchRecordOpener';
import maintenanceApiService from '@/services/maintenanceApiService';

import React, { useRef, useState, useEffect, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, Search, Eye, Edit, Calendar, AlertCircle, Settings, History, MapPin, Upload, Download, ArrowRightLeft, QrCode, Printer, Loader2, LayoutGrid, List, ArrowRight } from 'lucide-react';
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
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { useToast } from '@/hooks/use-toast';
import { useMaintenanceCurrency } from '@/hooks/useMaintenanceCurrency';
import { useRouter } from 'next/navigation';
import MaintenanceAttachmentsPanel from '@/components/maintenance/MaintenanceAttachmentsPanel';
import AssetVehicleFleetTabs from '@/components/maintenance/AssetVehicleFleetTabs';
import { QRCodeSVG } from 'qrcode.react';
import { inspectionTemplateService, InspectionTemplate, InspectionTemplateQrPackage } from '@/services/inspectionTemplateService';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { fleetService, type FleetTripInspectionDto } from '@/services/fleetService';
import { printQrLabel } from '@/lib/print-qr-label';
import { cn } from '@/lib/utils';

import { format } from 'date-fns';

interface Asset {
  id: string;
  assetCategoryId: string;
  assetNumber: string;
  name: string;
  description: string;
  category: string;
  status: 'Operational' | 'Maintenance Required' | 'Out of Service' | 'Under Maintenance';
  location: string;
  manufacturer: string;
  model: string;
  year?: number | null;
  ownershipType?: string | null;
  serialNumber: string;
  licensePlate?: string;
  vin?: string;
  fuelType?: string;
  isFleetAsset?: boolean;
  purchaseDate: string;
  warrantyExpiry: string;
  lastMaintenanceDate: string;
  nextMaintenanceDate: string;
  condition: 'Excellent' | 'Good' | 'Fair' | 'Poor';
  criticality: 'Low' | 'Medium' | 'High' | 'Critical';
  value: number;
  currentValue?: number;
  currentProjectId?: string | null;
  currentProjectName?: string | null;
  currentSiteLocationId?: string | null;
  currentSiteLocationName?: string | null;
}

type AssetsViewMode = 'list' | 'grid';

interface AssetMovementHistory {
  id: string;
  fromProjectName?: string | null;
  toProjectName?: string | null;
  fromSiteLocationName?: string | null;
  toSiteLocationName?: string | null;
  fromLocation?: string | null;
  toLocation?: string | null;
  effectiveDate: string;
  reason: string;
  notes?: string | null;
}

interface AssetWorkOrderHistory {
  id: string;
  workOrderNumber: string;
  title: string;
  status: string;
  workOrderType?: string | null;
  maintenanceType?: string | null;
  createdAt: string;
  actualCompletionDate?: string | null;
  actualCost: number;
}

interface AssetInspectionHistory {
  id: string;
  templateName: string;
  inspectionDate: string;
  status: string;
  overallResult?: string | null;
  notes?: string | null;
  failedItemCount: number;
  flaggedItemCount: number;
  generatedWorkOrderId?: string | null;
  workflowEntityType?: string | null;
}

interface AssetLifecycleHistory {
  assetId: string;
  movements: AssetMovementHistory[];
  serviceHistory: AssetWorkOrderHistory[];
  workOrderHistory: AssetWorkOrderHistory[];
  inspectionHistory: AssetInspectionHistory[];
}

interface LocationLookup {
  id: string;
  name: string;
  code?: string | null;
  isActive?: boolean;
}

const currentLocalDateTimeInput = () => {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
};

interface AssetImportResult {
  totalRows: number;
  successCount: number;
  errorCount: number;
  successfulAssetNumbers: string[];
  errors: Array<{ rowNumber: number; assetNumber?: string | null; field: string; error: string }>;
}

type InspectionReviewRow = {
  id: string;
  item: string;
  type: string;
  required: boolean;
  order: number;
  value: string;
  photo?: string | null;
};

type InspectionPhotoPreview = {
  url: string;
  item: string;
  index: number;
};

function parseInspectionRows(inspectionData?: string | null): InspectionReviewRow[] {
  if (!inspectionData) return [];
  try {
    const parsed = JSON.parse(inspectionData);
    if (!parsed || !Array.isArray(parsed.checklist)) return [];
    return parsed.checklist
      .filter((row: any) => row?.id)
      .map((row: any, index: number): InspectionReviewRow => ({
        id: String(row.id),
        item: String(row.item || `Checklist item ${index + 1}`),
        type: String(row.type || 'checklist'),
        required: !!row.required,
        order: Number(row.order ?? index + 1),
        value: row.value != null ? String(row.value) : '',
        photo: row.photo ? String(row.photo) : null,
      }))
      .sort((a: InspectionReviewRow, b: InspectionReviewRow) => (a.order ?? 0) - (b.order ?? 0));
  } catch {
    return [];
  }
}

function getInspectionPhotoExtension(photoUrl: string) {
  const match = /^data:image\/([^;]+)/i.exec(photoUrl);
  const extension = (match?.[1] || 'jpg').toLowerCase();
  return extension === 'jpeg' ? 'jpg' : extension.replace(/[^a-z0-9]/g, '') || 'jpg';
}

function getInspectionPhotoFileName(item: string, index: number, photoUrl: string) {
  const safeItem = item
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 50) || `item-${index + 1}`;

  return `inspection-photo-${index + 1}-${safeItem}.${getInspectionPhotoExtension(photoUrl)}`;
}

function downloadInspectionPhoto(photoUrl: string, item: string, index: number) {
  const link = document.createElement('a');
  link.href = photoUrl;
  link.download = getInspectionPhotoFileName(item, index, photoUrl);
  document.body.appendChild(link);
  link.click();
  link.remove();
}

type AssetFormState = {
  name: string;
  assetNumber: string;
  description: string;
  category: string;
  location: string;
  manufacturer: string;
  model: string;
  year: string;
  ownershipType: string;
  serialNumber: string;
  licensePlate: string;
  vin: string;
  fuelType: string;
  isFleetAsset: boolean;
  purchaseDate: string;
  warrantyExpiry: string;
  criticality: Asset['criticality'];
  value: number;
};

const FUEL_TYPE_OPTIONS = ['Petrol', 'Diesel', 'Electric', 'Hybrid'] as const;
const createEmptyAssetForm = (category = ''): AssetFormState => ({
  name: '',
  assetNumber: '',
  description: '',
  category,
  location: '',
  manufacturer: '',
  model: '',
  year: '',
  ownershipType: 'Owned',
  serialNumber: '',
  licensePlate: '',
  vin: '',
  fuelType: '',
  isFleetAsset: false,
  purchaseDate: '',
  warrantyExpiry: '',
  criticality: 'Medium',
  value: 0,
});

function AssetsPageContent() {
  const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
  const { toast } = useToast();
  const { assetValueLabel, formatMoney } = useMaintenanceCurrency();
  const searchParams = useSearchParams();
  const [assets, setAssets] = useState<Asset[]>([]);
  const [filteredAssets, setFilteredAssets] = useState<Asset[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [viewMode, setViewMode] = useState<AssetsViewMode>('list');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [assetTypes, setAssetTypes] = useState<Array<{ id: string; name: string; description?: string; assetType?: string | null; isActive?: boolean }>>([]);
  const initialCreateHandledRef = useRef(false);
  const initialEditHandledRef = useRef(false);
  const initialInspectionHandledRef = useRef(false);
  const assetQrLabelRef = useRef<HTMLDivElement>(null);
  const [vehiclePicturesOpen, setVehiclePicturesOpen] = useState(false);
  const [assetViewTab, setAssetViewTab] = useState<string>('details');
  const [lifecycleHistory, setLifecycleHistory] = useState<AssetLifecycleHistory | null>(null);
  const [lifecycleLoading, setLifecycleLoading] = useState(false);
  const [locations, setLocations] = useState<LocationLookup[]>([]);
  const [locationLookupError, setLocationLookupError] = useState<string | null>(null);
  const [isMoveDialogOpen, setIsMoveDialogOpen] = useState(false);
  const [moveForm, setMoveForm] = useState({ siteLocationId: 'none', location: '', reason: '', notes: '', effectiveDate: currentLocalDateTimeInput() });
  const [movingAsset, setMovingAsset] = useState(false);
  const [isImportDialogOpen, setIsImportDialogOpen] = useState(false);
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState<AssetImportResult | null>(null);
  const [isQrDialogOpen, setIsQrDialogOpen] = useState(false);
  const [qrTemplates, setQrTemplates] = useState<InspectionTemplate[]>([]);
  const [selectedQrTemplateId, setSelectedQrTemplateId] = useState('');
  const [qrPackage, setQrPackage] = useState<InspectionTemplateQrPackage | null>(null);
  const [qrLoading, setQrLoading] = useState(false);
  const [assetInspections, setAssetInspections] = useState<FleetTripInspectionDto[]>([]);
  const [assetInspectionsLoading, setAssetInspectionsLoading] = useState(false);
  const [assetInspectionsError, setAssetInspectionsError] = useState<string | null>(null);
  const [assetInspectionDialogOpen, setAssetInspectionDialogOpen] = useState(false);
  const [selectedAssetInspection, setSelectedAssetInspection] = useState<FleetTripInspectionDto | null>(null);
  const [assetInspectionTemplate, setAssetInspectionTemplate] = useState<InspectionTemplate | null>(null);
  const [assetInspectionTemplateLoading, setAssetInspectionTemplateLoading] = useState(false);
  const [inspectionPhotoPreview, setInspectionPhotoPreview] = useState<InspectionPhotoPreview | null>(null);

  const [newAsset, setNewAsset] = useState<AssetFormState>(createEmptyAssetForm());

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
        assetCategoryId: asset.assetCategoryId || asset.AssetCategoryId || asset.assetCategory?.id || '',
        assetNumber: asset.assetNumber || asset.AssetNumber || 'N/A',
        value: asset.currentValue || asset.CurrentValue || 0,
        currentValue: asset.currentValue || asset.CurrentValue || 0,
        category: asset.assetCategory?.name || asset.categoryName || asset.CategoryName || 'Unknown',
        licensePlate: asset.licensePlate || asset.LicensePlate || '',
        vin: asset.vin || asset.VIN || asset.Vin || '',
        fuelType: asset.fuelType || asset.FuelType || '',
        isFleetAsset: asset.isFleetAsset ?? asset.IsFleetAsset ?? false,
        // Map date fields with proper formatting
        purchaseDate: formatDateForInput(asset.purchaseDate || asset.PurchaseDate),
        warrantyExpiry: formatDateForInput(asset.warrantyEndDate || asset.WarrantyEndDate || asset.warrantyExpiry),
        // Map other potential field name variations
        manufacturer: asset.manufacturer || asset.Manufacturer || '',
        model: asset.model || asset.Model || '',
        year: asset.year ?? asset.Year ?? null,
        ownershipType: asset.ownershipType || asset.OwnershipType || 'Owned',
        serialNumber: asset.serialNumber || asset.SerialNumber || '',
        location: asset.location || asset.Location || '',
        currentProjectId: asset.currentProjectId || asset.CurrentProjectId || null,
        currentProjectName: asset.currentProjectName || asset.CurrentProjectName || null,
        currentSiteLocationId: asset.currentSiteLocationId || asset.CurrentSiteLocationId || null,
        currentSiteLocationName: asset.currentSiteLocationName || asset.CurrentSiteLocationName || null,
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
    const normalized = categoryName.trim().toLowerCase();
    const match = assetTypes.find((t) => (t.name || '').trim().toLowerCase() === normalized);
    if (match) return (match.assetType || '').toLowerCase() === 'vehicle';
    return normalized.includes('vehicle');
  };

  const showVehicleFields = isVehicleCategoryName(newAsset.category);
  const selectedAssetIsVehicle = selectedAsset ? isVehicleCategoryName(selectedAsset.category) : false;
  const selectedAssetSupportsPreStart = !!selectedAsset && (selectedAsset.isFleetAsset || selectedAssetIsVehicle);

  useEffect(() => {
    if (!isViewDialogOpen) return;
    const requestedTab = searchParams?.get('tab');
    setAssetViewTab(requestedTab === 'inspections' ? 'inspections' : 'details');
  }, [isViewDialogOpen, searchParams, selectedAsset?.id]);

  useEffect(() => {
    const loadMovementLookups = async () => {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      setLocationLookupError(null);
      try {
        const locationsResponse = await fetch(`${API_URL}/Location/summary`, { headers: { Authorization: token ? `Bearer ${token}` : '' } });
        if (!locationsResponse.ok) {
          throw new Error(`Site lookup returned HTTP ${locationsResponse.status}`);
        }

        const locationPayload = await locationsResponse.json();
        const locationItems: LocationLookup[] = Array.isArray(locationPayload)
          ? locationPayload
          : Array.isArray(locationPayload?.items)
            ? locationPayload.items
            : Array.isArray(locationPayload?.data)
              ? locationPayload.data
              : [];
        setLocations(locationItems.filter((item) => item.isActive !== false));
      } catch (lookupError) {
        console.error('Failed to load HR location lookups', lookupError);
        setLocations([]);
        setLocationLookupError(lookupError instanceof Error ? lookupError.message : 'Sites could not be loaded.');
      }
    };
    void loadMovementLookups();
  }, [API_URL]);

  useEffect(() => {
    if (!isViewDialogOpen || !selectedAsset?.id) return;
    const loadLifecycleHistory = async () => {
      setLifecycleLoading(true);
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      try {
        const response = await fetch(`${API_URL}/maintenance/assets/${selectedAsset.id}/lifecycle-history`, {
          headers: { Authorization: token ? `Bearer ${token}` : '' },
        });
        if (!response.ok) throw new Error(await response.text());
        setLifecycleHistory(await response.json());
      } catch (historyError) {
        console.error('Failed to load asset lifecycle history', historyError);
        setLifecycleHistory(null);
      } finally {
        setLifecycleLoading(false);
      }
    };
    void loadLifecycleHistory();
  }, [API_URL, isViewDialogOpen, selectedAsset?.id]);

  const loadAssetInspections = React.useCallback(async (assetId?: string | null) => {
    if (!assetId) {
      setAssetInspections([]);
      return;
    }

    setAssetInspectionsLoading(true);
    setAssetInspectionsError(null);
    try {
      const inspections = await fleetService.getAssetInspections(assetId, 100);
      setAssetInspections(inspections.slice().sort((left, right) => {
        const leftTime = new Date(left.completedAtUtc || left.startedAtUtc).getTime();
        const rightTime = new Date(right.completedAtUtc || right.startedAtUtc).getTime();
        return (Number.isNaN(rightTime) ? 0 : rightTime) - (Number.isNaN(leftTime) ? 0 : leftTime);
      }));
    } catch (inspectionError) {
      console.error('Failed to load asset inspections', inspectionError);
      setAssetInspections([]);
      setAssetInspectionsError(inspectionError instanceof Error ? inspectionError.message : 'The inspection records could not be loaded.');
    } finally {
      setAssetInspectionsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!isViewDialogOpen || !selectedAsset?.id) return;
    void loadAssetInspections(selectedAsset.id);
  }, [isViewDialogOpen, loadAssetInspections, selectedAsset?.id]);

  useEffect(() => {
    if (initialInspectionHandledRef.current || !isViewDialogOpen || assetInspectionsLoading) return;
    const inspectionId = searchParams?.get('inspectionId');
    if (!inspectionId) return;
    const inspection = assetInspections.find((item) => item.id === inspectionId);
    if (!inspection) return;
    initialInspectionHandledRef.current = true;
    setAssetViewTab('inspections');
    void openAssetInspectionReview(inspection);
  }, [assetInspections, assetInspectionsLoading, isViewDialogOpen, searchParams]);

  // Load assets and maintenance history from API
  useEffect(() => {
    const loadAssetsData = async () => {
      setLoading(true);
      try {
        const token = localStorage.getItem('authToken');
        const [assetsResponse, categoriesResponse] = await Promise.all([
          fetch(`${API_URL}/maintenance/assets`, {
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
    const assetId = searchParams?.get('id');
    const categoryParam = searchParams?.get('category');
    const editParam = (searchParams?.get('edit') || '').trim().toLowerCase();
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

    const createParam = (searchParams?.get('create') || '').trim().toLowerCase();
    if (createParam !== '1' && createParam !== 'true') return;
    if (assetTypes.length === 0) return;

    const assetTypeParam = (searchParams?.get('assetType') || '').trim();
    const addToFleetParam = (searchParams?.get('addToFleet') || '').trim().toLowerCase();
    const addToFleet = addToFleetParam === '1' || addToFleetParam === 'true';
    if (assetTypeParam) {
      const match = assetTypes.find((t) => (t.assetType || '').toLowerCase() === assetTypeParam.toLowerCase());
      if (match) {
        setNewAsset((prev) => ({
          ...prev,
          category: match.name,
          isFleetAsset: (match.assetType || '').toLowerCase() === 'vehicle' ? addToFleet : false,
        }));
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

  useEffect(() => {
    const saved = window.localStorage.getItem('maintenanceAssetsViewMode');
    if (saved === 'list' || saved === 'grid') setViewMode(saved);
  }, []);

  const changeViewMode = (mode: AssetsViewMode) => {
    setViewMode(mode);
    window.localStorage.setItem('maintenanceAssetsViewMode', mode);
  };

  const handleCreateAsset = async () => {
    try {
      // Find the selected asset type
      const selectedType = assetTypes.find(type => type.name === newAsset.category);
      const typeId = selectedType?.id || assetTypes[0]?.id;
      const isVehicleCategory = (selectedType?.assetType || '').toLowerCase() === 'vehicle';
      const yearNumber = newAsset.year ? parseInt(newAsset.year, 10) : null;

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
        year: yearNumber,
        ownershipType: newAsset.ownershipType || 'Owned',
        serialNumber: newAsset.serialNumber?.trim() || null,
        licensePlate: isVehicleCategory ? (newAsset.licensePlate?.trim() || null) : null,
        vin: isVehicleCategory ? (newAsset.vin?.trim() || null) : null,
        fuelType: isVehicleCategory ? (newAsset.fuelType || null) : null,
        isFleetAsset: isVehicleCategory ? !!newAsset.isFleetAsset : false,
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
      setNewAsset(createEmptyAssetForm(assetTypes[0]?.name ?? ''));
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
      year: asset.year != null ? String(asset.year) : '',
      ownershipType: asset.ownershipType || 'Owned',
      serialNumber: asset.serialNumber || '',
      licensePlate: asset.licensePlate || '',
      vin: asset.vin || '',
      fuelType: asset.fuelType || '',
      isFleetAsset: !!asset.isFleetAsset,
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

    const assetId = (searchParams?.get('id') || '').trim();
    const editParam = (searchParams?.get('edit') || '').trim().toLowerCase();
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
      const yearNumber = newAsset.year ? parseInt(newAsset.year, 10) : null;

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
          year: yearNumber,
          ownershipType: newAsset.ownershipType || 'Owned',
          serialNumber: newAsset.serialNumber,
          licensePlate: isVehicleCategory ? (newAsset.licensePlate?.trim() || null) : null,
          vin: isVehicleCategory ? (newAsset.vin?.trim() || null) : null,
          fuelType: isVehicleCategory ? (newAsset.fuelType || null) : null,
          isFleetAsset: isVehicleCategory ? !!newAsset.isFleetAsset : false,
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
      setNewAsset(createEmptyAssetForm(assetTypes[0]?.name ?? ''));
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

  const refreshAssets = async () => {
    const token = localStorage.getItem('token') || localStorage.getItem('authToken');
    const response = await fetch(`${API_URL}/maintenance/assets?pageSize=100`, {
      headers: { Authorization: token ? `Bearer ${token}` : '' },
    });
    if (!response.ok) throw new Error(await response.text());
    const data = await response.json();
    setAssets(mapAssets(data.data || data.items || data || []));
  };

  const downloadImportTemplate = async () => {
    const token = localStorage.getItem('token') || localStorage.getItem('authToken');
    const response = await fetch(`${API_URL}/maintenance/assets/import-template`, {
      headers: { Authorization: token ? `Bearer ${token}` : '' },
    });
    if (!response.ok) throw new Error(await response.text());
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = 'maintenance-asset-import-template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  };

  const importAssets = async () => {
    if (!importFile) return;
    setImporting(true);
    setImportResult(null);
    const token = localStorage.getItem('token') || localStorage.getItem('authToken');
    const data = new FormData();
    data.append('file', importFile);
    try {
      const response = await fetch(`${API_URL}/maintenance/assets/import`, {
        method: 'POST',
        headers: { Authorization: token ? `Bearer ${token}` : '' },
        body: data,
      });
      if (!response.ok) throw new Error(await response.text());
      const result: AssetImportResult = await response.json();
      setImportResult(result);
      await refreshAssets();
      toast({ title: 'Asset upload completed', description: `${result.successCount} imported, ${result.errorCount} failed.` });
    } catch (importError) {
      toast({ title: 'Asset upload failed', description: importError instanceof Error ? importError.message : 'The asset file could not be uploaded.', variant: 'destructive' });
    } finally {
      setImporting(false);
    }
  };

  const openMoveDialog = () => {
    if (!selectedAsset) return;
    setIsViewDialogOpen(false);
    setMoveForm({
      siteLocationId: selectedAsset.currentSiteLocationId || 'none',
      location: selectedAsset.location || '',
      reason: '',
      notes: '',
      effectiveDate: currentLocalDateTimeInput(),
    });
    setIsMoveDialogOpen(true);
  };

  const moveAsset = async () => {
    if (!selectedAsset || !moveForm.reason.trim()) return;
    setMovingAsset(true);
    const token = localStorage.getItem('token') || localStorage.getItem('authToken');
    try {
      const response = await fetch(`${API_URL}/maintenance/assets/${selectedAsset.id}/move`, {
        method: 'POST',
        headers: { Authorization: token ? `Bearer ${token}` : '', 'Content-Type': 'application/json' },
        body: JSON.stringify({
          projectId: selectedAsset.currentProjectId || null,
          siteLocationId: moveForm.siteLocationId === 'none' ? null : moveForm.siteLocationId,
          location: moveForm.location.trim() || null,
          reason: moveForm.reason.trim(),
          notes: moveForm.notes.trim() || null,
          effectiveDate: new Date(moveForm.effectiveDate).toISOString(),
        }),
      });
      if (!response.ok) throw new Error(await response.text());
      const updated = mapAssets([await response.json()])[0];
      setSelectedAsset(updated);
      setAssets((items) => items.map((item) => item.id === updated.id ? updated : item));
      setIsMoveDialogOpen(false);
      const historyResponse = await fetch(`${API_URL}/maintenance/assets/${updated.id}/lifecycle-history`, { headers: { Authorization: token ? `Bearer ${token}` : '' } });
      if (historyResponse.ok) setLifecycleHistory(await historyResponse.json());
      toast({ title: 'Asset moved', description: 'The current assignment and movement history were updated.' });
    } catch (moveError) {
      toast({ title: 'Asset move failed', description: moveError instanceof Error ? moveError.message : 'The asset could not be moved.', variant: 'destructive' });
    } finally {
      setMovingAsset(false);
    }
  };

  const generateAssetQr = async (templateId: string, templateOverride?: InspectionTemplate) => {
    if (!selectedAsset || !templateId) return;
    const template = templateOverride || qrTemplates.find(item => item.id === templateId);
    const inspectionKind = template?.templateScope === 'Fleet'
      ? (template.fleetInspectionKind && template.fleetInspectionKind !== 'Any' ? template.fleetInspectionKind : 'PreTrip')
      : template?.sheetType === 'ServiceSheet'
        ? 'Service'
        : template?.sheetType === 'WeeklyChecklist'
          ? 'Weekly'
          : template?.sheetType === 'PreventiveMaintenanceForm'
            ? 'PreventiveMaintenance'
            : 'Inspection';
    setQrLoading(true);
    setSelectedQrTemplateId(templateId);
    try {
      setQrPackage(await inspectionTemplateService.getQrPackage(templateId, {
        assetId: selectedAsset.id,
        assetCategoryId: selectedAsset.assetCategoryId,
        inspectionKind,
        includeEmbeddedPayload: true,
        maxQrPayloadBytes: 2500,
      }));
    } catch (qrError) {
      toast({ title: 'QR generation failed', description: qrError instanceof Error ? qrError.message : 'The QR package could not be generated.', variant: 'destructive' });
      setQrPackage(null);
    } finally {
      setQrLoading(false);
    }
  };

  const openQrDialog = async () => {
    if (!selectedAsset) return;
    setIsViewDialogOpen(false);
    setIsQrDialogOpen(true);
    setQrLoading(true);
    setQrPackage(null);
    try {
      const templates = await inspectionTemplateService.getAllTemplates({
        activeOnly: true,
        assignedAssetCategoryId: selectedAsset.assetCategoryId,
        assignedAssetId: selectedAsset.id,
        isQrEnabled: true,
      });
      setQrTemplates(templates);
      if (templates.length > 0) await generateAssetQr(templates[0].id, templates[0]);
    } catch (templateError) {
      toast({ title: 'Checklist lookup failed', description: templateError instanceof Error ? templateError.message : 'No checklist could be loaded.', variant: 'destructive' });
    } finally {
      setQrLoading(false);
    }
  };

  const formatDate = (value: string | Date | null | undefined) => {
    if (!value) return '';
    const d = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(d.getTime())) return '';
    return format(d, 'MMM dd, yyyy');
  };

  const formatDateTime = (value: string | Date | null | undefined) => {
    if (!value) return '';
    const d = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(d.getTime())) return '';
    return format(d, 'MMM dd, yyyy HH:mm');
  };

  const getInspectionStatusBadge = (status?: string | null) => {
    const label = status || 'Unknown';
    const normalized = label.trim().toLowerCase();
    if (['rejected', 'failed', 'cancelled', 'canceled'].includes(normalized)) return <Badge variant="destructive">{label}</Badge>;
    if (['completed', 'approved', 'submitted', 'synced'].includes(normalized)) {
      return <Badge variant="outline" className="border-green-200 bg-green-100 text-green-800 hover:bg-green-100">{label}</Badge>;
    }
    if (['inprogress', 'in progress', 'started', 'draft'].includes(normalized)) {
      return <Badge variant="outline" className="border-blue-200 bg-blue-100 text-blue-800 hover:bg-blue-100">{label}</Badge>;
    }
    return <Badge variant="outline" className="border-slate-200 bg-slate-100 text-slate-700 hover:bg-slate-100">{label}</Badge>;
  };

  const getWorkOrderStatusBadge = (status?: string | null) => {
    const label = status || 'Unknown';
    const normalized = label.trim().toLowerCase();
    if (['failed', 'rejected', 'cancelled', 'canceled', 'overdue'].includes(normalized)) return <Badge variant="destructive">{label}</Badge>;
    if (['completed', 'complete', 'closed', 'approved'].includes(normalized)) {
      return <Badge variant="outline" className="border-green-200 bg-green-100 text-green-800 hover:bg-green-100">{label}</Badge>;
    }
    if (['inprogress', 'in progress', 'started', 'assigned'].includes(normalized)) {
      return <Badge variant="outline" className="border-blue-200 bg-blue-100 text-blue-800 hover:bg-blue-100">{label}</Badge>;
    }
    if (['pending', 'scheduled', 'onhold', 'on hold'].includes(normalized)) {
      return <Badge variant="outline" className="border-amber-200 bg-amber-100 text-amber-800 hover:bg-amber-100">{label}</Badge>;
    }
    return <Badge variant="outline" className="border-slate-200 bg-slate-100 text-slate-700 hover:bg-slate-100">{label}</Badge>;
  };

  const getInspectionResultBadge = (result?: string | null) => {
    if (!result) return <Badge variant="outline">Pending</Badge>;
    const normalized = result.trim().toLowerCase();
    if (normalized === 'fail' || normalized === 'failed') return <Badge variant="destructive">{result}</Badge>;
    if (normalized === 'pass' || normalized === 'passed') {
      return <Badge variant="outline" className="border-green-200 bg-green-100 text-green-800 hover:bg-green-100">{result}</Badge>;
    }
    return <Badge variant="outline">{result}</Badge>;
  };

  const getSheetTypeLabel = (sheetType?: string | null) => {
    switch (sheetType) {
      case 'ServiceSheet':
        return 'Service Sheet';
      case 'WeeklyChecklist':
        return 'Weekly Checklist';
      case 'PreventiveMaintenanceForm':
        return 'Preventive Maintenance';
      default:
        return 'Inspection Sheet';
    }
  };

  const openAssetInspectionReview = async (inspection: FleetTripInspectionDto) => {
    setSelectedAssetInspection(inspection);
    setAssetInspectionDialogOpen(true);
    setAssetInspectionTemplate(null);
    setAssetInspectionTemplateLoading(true);
    try {
      setAssetInspectionTemplate(await inspectionTemplateService.getTemplateById(inspection.inspectionTemplateId));
    } catch (templateError) {
      console.error('Failed to load inspection template for review', templateError);
      setAssetInspectionTemplate(null);
    } finally {
      setAssetInspectionTemplateLoading(false);
    }
  };

  const refreshSelectedAssetInspection = async (updated?: FleetTripInspectionDto) => {
    if (updated) setSelectedAssetInspection(updated);
    if (selectedAsset?.id) {
      await loadAssetInspections(selectedAsset.id);
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      const response = await fetch(`${API_URL}/maintenance/assets/${selectedAsset.id}/lifecycle-history`, {
        headers: { Authorization: token ? `Bearer ${token}` : '' },
      });
      if (response.ok) setLifecycleHistory(await response.json());
    }
  };

  const isMaintenanceOverdue = (nextMaintenanceDate: string | Date | null | undefined) => {
    if (!nextMaintenanceDate) return false;
    const d = nextMaintenanceDate instanceof Date ? nextMaintenanceDate : new Date(nextMaintenanceDate);
    if (Number.isNaN(d.getTime())) return false;
    return d < new Date();
  };

  const selectedAssetInspectionRows = selectedAssetInspection ? parseInspectionRows(selectedAssetInspection.inspectionData) : [];
  const selectedAssetTemplateRows = assetInspectionTemplate?.checklistItems
    ?.slice()
    .sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
    .map((item, index) => ({
      id: item.id,
      item: item.item || `Checklist item ${index + 1}`,
      type: item.type || 'checklist',
      required: !!item.required,
      order: item.order ?? index + 1,
      value: '',
      photo: null,
    })) || [];
  const assetInspectionReviewRows = selectedAssetInspectionRows.length ? selectedAssetInspectionRows : selectedAssetTemplateRows;
  const sortedMovementHistory = (lifecycleHistory?.movements || []).slice().sort((left, right) => {
    const leftTime = new Date(left.effectiveDate).getTime();
    const rightTime = new Date(right.effectiveDate).getTime();
    return (Number.isNaN(rightTime) ? 0 : rightTime) - (Number.isNaN(leftTime) ? 0 : leftTime);
  });
  const sortedWorkOrderHistory = (lifecycleHistory?.workOrderHistory || []).slice().sort((left, right) => {
    const leftTime = new Date(left.actualCompletionDate || left.createdAt).getTime();
    const rightTime = new Date(right.actualCompletionDate || right.createdAt).getTime();
    return (Number.isNaN(rightTime) ? 0 : rightTime) - (Number.isNaN(leftTime) ? 0 : leftTime);
  });
  const sortedServiceHistory = (lifecycleHistory?.serviceHistory || []).slice().sort((left, right) => {
    const leftTime = new Date(left.actualCompletionDate || left.createdAt).getTime();
    const rightTime = new Date(right.actualCompletionDate || right.createdAt).getTime();
    return (Number.isNaN(rightTime) ? 0 : rightTime) - (Number.isNaN(leftTime) ? 0 : leftTime);
  });

  const getMovementPlace = (
    projectName?: string | null,
    siteName?: string | null,
    location?: string | null,
  ) => {
    const primary = siteName?.trim() || location?.trim() || projectName?.trim() || 'Unassigned';
    const context = projectName?.trim() && projectName.trim() !== primary ? projectName.trim() : null;
    return { primary, context };
  };

  return (
    <div className="space-y-6">
      <GlobalSearchRecordOpener<Asset>
        load={async id => mapAssets([await maintenanceApiService.getAssetById(id)])[0]}
        onOpen={asset => {
          setSelectedAsset(asset);
          setAssetViewTab('details');
          setIsViewDialogOpen(true);
        }}
      />
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
        <Dialog open={isImportDialogOpen} onOpenChange={setIsImportDialogOpen}>
          <DialogTrigger asChild>
            <Button variant="outline"><Upload className="mr-2 h-4 w-4" />Upload Assets</Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Upload Asset Master Data</DialogTitle>
              <DialogDescription>Import maintenance assets from the standard Excel template.</DialogDescription>
            </DialogHeader>
            <div className="space-y-4">
              <Button type="button" variant="outline" onClick={() => void downloadImportTemplate()}>
                <Download className="mr-2 h-4 w-4" />Download Template
              </Button>
              <div className="space-y-2">
                <Label htmlFor="asset-import-file">Excel File</Label>
                <Input id="asset-import-file" type="file" accept=".xlsx" onChange={(event) => setImportFile(event.target.files?.[0] || null)} />
              </div>
              {importResult && (
                <div className="rounded-md border p-3 text-sm">
                  <div className="mb-2 flex gap-4">
                    <span>{importResult.successCount} imported</span>
                    <span>{importResult.errorCount} failed</span>
                    <span>{importResult.totalRows} total</span>
                  </div>
                  {importResult.errors.length > 0 && (
                    <div className="max-h-48 overflow-y-auto border-t pt-2">
                      {importResult.errors.map((item, index) => (
                        <p key={`${item.rowNumber}-${item.field}-${index}`} className="text-red-700">
                          Row {item.rowNumber}: {item.field} - {item.error}
                        </p>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsImportDialogOpen(false)}>Close</Button>
              <Button onClick={() => void importAssets()} disabled={!importFile || importing}>
                {importing && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Upload
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Asset
            </Button>
          </DialogTrigger>
          <DialogContent className="w-[95vw] max-w-6xl max-h-[85vh] overflow-y-auto">
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
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
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
                  <Select value={newAsset.criticality} onValueChange={(value: Asset['criticality']) => setNewAsset(prev => ({ ...prev, criticality: value }))}>
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
              <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
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
                <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
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
                  <div className="space-y-2">
                    <Label>Fuel Type</Label>
                    <Select value={newAsset.fuelType || 'none'} onValueChange={(value) => setNewAsset((prev) => ({ ...prev, fuelType: value === 'none' ? '' : value }))}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select fuel type" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">Select fuel type</SelectItem>
                        {FUEL_TYPE_OPTIONS.map((option) => (
                          <SelectItem key={option} value={option}>
                            {option}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="year">Year</Label>
                    <Input
                      id="year"
                      type="number"
                      min="1900"
                      max={String(new Date().getFullYear() + 1)}
                      value={newAsset.year}
                      onChange={(e) => setNewAsset((prev) => ({ ...prev, year: e.target.value }))}
                      placeholder="e.g. 2021"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Ownership</Label>
                    <Select value={newAsset.ownershipType} onValueChange={(value) => setNewAsset((prev) => ({ ...prev, ownershipType: value }))}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select ownership" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Owned">Owned</SelectItem>
                        <SelectItem value="Leased">Leased</SelectItem>
                        <SelectItem value="Rented">Rented</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex items-start gap-3 rounded-md border bg-muted/10 p-3 lg:col-span-4">
                    <Switch checked={!!newAsset.isFleetAsset} onCheckedChange={(v) => setNewAsset((prev) => ({ ...prev, isFleetAsset: v }))} />
                    <div className="space-y-1">
                      <Label>Add to Fleet</Label>
                      <p className="text-xs text-muted-foreground">
                        Only assets flagged as Fleet will appear in Fleet Management.
                      </p>
                    </div>
                  </div>
                </div>
              )}
              <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
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
                  <Label htmlFor="value">{assetValueLabel}</Label>
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
          <DialogContent className="w-[95vw] max-w-6xl max-h-[85vh] overflow-y-auto">
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
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
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
              <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
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
                <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-5">
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
                  <div className="space-y-2">
                    <Label>Fuel Type</Label>
                    <Select value={newAsset.fuelType || 'none'} onValueChange={(value) => setNewAsset((prev) => ({ ...prev, fuelType: value === 'none' ? '' : value }))}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select fuel type" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">Select fuel type</SelectItem>
                        {FUEL_TYPE_OPTIONS.map((option) => (
                          <SelectItem key={option} value={option}>
                            {option}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="edit-year">Year</Label>
                    <Input
                      id="edit-year"
                      type="number"
                      min="1900"
                      max={String(new Date().getFullYear() + 1)}
                      value={newAsset.year}
                      onChange={(e) => setNewAsset((prev) => ({ ...prev, year: e.target.value }))}
                      placeholder="e.g. 2021"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Ownership</Label>
                    <Select value={newAsset.ownershipType} onValueChange={(value) => setNewAsset((prev) => ({ ...prev, ownershipType: value }))}>
                      <SelectTrigger>
                        <SelectValue placeholder="Select ownership" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Owned">Owned</SelectItem>
                        <SelectItem value="Leased">Leased</SelectItem>
                        <SelectItem value="Rented">Rented</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex items-start gap-3 rounded-md border bg-muted/10 p-3 lg:col-span-4">
                    <Switch checked={!!newAsset.isFleetAsset} onCheckedChange={(v) => setNewAsset((prev) => ({ ...prev, isFleetAsset: v }))} />
                    <div className="space-y-1">
                      <Label>Add to Fleet</Label>
                      <p className="text-xs text-muted-foreground">
                        Only assets flagged as Fleet will appear in Fleet Management.
                      </p>
                    </div>
                  </div>
                </div>
              )}
              {showVehicleFields && selectedAsset?.id ? (
                <div className="flex items-center justify-between rounded-md border bg-muted/10 p-3">
                  <div>
                    <div className="text-sm font-medium">Vehicle Pictures</div>
                    <div className="text-xs text-muted-foreground">Upload and view photos for this vehicle asset.</div>
                  </div>
                  <Button variant="outline" onClick={() => setVehiclePicturesOpen(true)}>
                    Manage Pictures
                  </Button>
                </div>
              ) : null}
              <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
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
                  <Label htmlFor="edit-value">{assetValueLabel}</Label>
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

        <Dialog open={vehiclePicturesOpen} onOpenChange={setVehiclePicturesOpen}>
          <DialogContent className="max-w-5xl">
            <DialogHeader>
              <DialogTitle>Vehicle Pictures</DialogTitle>
              <DialogDescription>Upload and view pictures for this vehicle. Stored securely as Asset attachments.</DialogDescription>
            </DialogHeader>

            {selectedAsset?.id ? (
              <MaintenanceAttachmentsPanel
                entityType="Asset"
                entityId={selectedAsset.id}
                category="VehiclePictures"
                title="Pictures"
                description="Upload photos and related documents for this vehicle."
              />
            ) : (
              <div className="py-6 text-sm text-muted-foreground">Select a vehicle to manage pictures.</div>
            )}

            <DialogFooter>
              <Button variant="outline" onClick={() => setVehiclePicturesOpen(false)}>
                Close
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

      {/* Asset list and card views */}
      <div className="space-y-4">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">Assets</h2>
            <p className="text-sm text-muted-foreground">{filteredAssets.length.toLocaleString()} assets in the current filter</p>
          </div>
          <TooltipProvider delayDuration={150}>
            <div className="inline-flex h-10 w-fit items-center rounded-md border bg-background p-1">
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    variant={viewMode === 'list' ? 'default' : 'ghost'}
                    size="icon"
                    className={cn('h-8 w-8', viewMode !== 'list' && 'text-muted-foreground')}
                    onClick={() => changeViewMode('list')}
                    aria-label="Show list view"
                  >
                    <List className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>List view</TooltipContent>
              </Tooltip>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    variant={viewMode === 'grid' ? 'default' : 'ghost'}
                    size="icon"
                    className={cn('h-8 w-8', viewMode !== 'grid' && 'text-muted-foreground')}
                    onClick={() => changeViewMode('grid')}
                    aria-label="Show card view"
                  >
                    <LayoutGrid className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Card view</TooltipContent>
              </Tooltip>
            </div>
          </TooltipProvider>
        </div>

        {viewMode === 'list' ? (
          <div className="overflow-x-auto rounded-md border bg-card">
            <Table>
            <TableHeader>
              <TableRow className="bg-muted/40">
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
              {loading ? (
                <TableRow><TableCell colSpan={9} className="h-28 text-center text-muted-foreground">Loading assets...</TableCell></TableRow>
              ) : filteredAssets.length === 0 ? (
                <TableRow><TableCell colSpan={9} className="h-28 text-center text-muted-foreground">No assets match the current filters.</TableCell></TableRow>
              ) : filteredAssets.map((asset, index) => (
                <TableRow key={asset.id} className={index % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
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
          </div>
        ) : loading ? (
          <div className="rounded-md border bg-card py-10 text-center text-muted-foreground">Loading assets...</div>
        ) : filteredAssets.length === 0 ? (
          <div className="rounded-md border bg-card py-10 text-center text-muted-foreground">No assets match the current filters.</div>
        ) : (
          <div className="rounded-md bg-slate-100 p-4">
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
              {filteredAssets.map((asset) => {
                const maintenanceOverdue = isMaintenanceOverdue(asset.nextMaintenanceDate);
                const makeAndModel = [asset.manufacturer, asset.model].filter(Boolean).join(' ');

                return (
                  <Card key={asset.id} className="overflow-hidden border-slate-200 bg-white shadow-sm transition-shadow hover:shadow-md">
                    <CardContent className="flex min-h-72 flex-col gap-4 p-4">
                      <div className="flex items-start gap-3">
                        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-blue-50 text-blue-600 ring-1 ring-blue-100">
                          <Settings className="h-5 w-5" />
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-mono text-base font-semibold text-slate-950">{asset.assetNumber}</p>
                          <p className="truncate text-sm text-slate-500">{asset.name}</p>
                        </div>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 shrink-0 text-slate-700 hover:text-blue-700"
                          onClick={() => handleEditAsset(asset)}
                          title="Edit asset"
                        >
                          <Edit className="h-4 w-4" />
                          <span className="sr-only">Edit asset</span>
                        </Button>
                      </div>

                      <div className="space-y-2 text-sm text-slate-500">
                        <p className="truncate font-medium text-slate-700">{makeAndModel || asset.category}</p>
                        <div className="flex min-w-0 items-center gap-2">
                          <MapPin className="h-4 w-4 shrink-0 text-emerald-500" />
                          <span className="truncate">{asset.location || 'No location'}</span>
                        </div>
                      </div>

                      <div className="flex flex-wrap items-center gap-2">
                        {getStatusBadge(asset.status)}
                        {getConditionBadge(asset.condition)}
                        {getCriticalityBadge(asset.criticality)}
                      </div>

                      <div className="mt-auto flex items-start gap-2 border-t border-slate-100 pt-4 text-sm">
                        <Calendar className={cn('mt-0.5 h-4 w-4 shrink-0', maintenanceOverdue ? 'text-red-600' : 'text-slate-400')} />
                        <div className={cn('min-w-0', maintenanceOverdue ? 'font-medium text-red-700' : 'text-slate-500')}>
                          <p>{asset.nextMaintenanceDate ? formatDate(asset.nextMaintenanceDate) : 'Not scheduled'}</p>
                          {maintenanceOverdue && <p className="text-xs">Overdue</p>}
                        </div>
                      </div>

                      <div className="flex items-center justify-between gap-2">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() => router.push(`/maintenance/asset-admission?assetId=${asset.id}`)}
                        >
                          Admit
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          className="bg-blue-600 hover:bg-blue-700"
                          onClick={() => {
                            setSelectedAsset(asset);
                            setIsViewDialogOpen(true);
                          }}
                        >
                          <ArrowRight className="h-4 w-4" />
                          Detail
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          </div>
        )}
      </div>

      {/* View Asset Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="w-[95vw] max-w-6xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Asset Details</DialogTitle>
            <DialogDescription>
              Complete information and maintenance history for this asset
            </DialogDescription>
          </DialogHeader>
          {selectedAsset && (
            <Tabs value={assetViewTab} onValueChange={setAssetViewTab} className="space-y-4">
              <div className="h-[60vh] flex flex-col">
                <TabsList className="flex flex-wrap justify-start gap-1 h-auto">
                  <TabsTrigger value="details">Asset Details</TabsTrigger>
                  <TabsTrigger value="movement">Movement</TabsTrigger>
                  <TabsTrigger value="service">Service History</TabsTrigger>
                  <TabsTrigger value="inspections">Inspections</TabsTrigger>
                  <TabsTrigger value="work-orders">Work Orders</TabsTrigger>
                  <TabsTrigger value="schedule">Maintenance Schedule</TabsTrigger>
                  {selectedAssetIsVehicle && (
                    <TabsTrigger value="fleet">Fleet</TabsTrigger>
                  )}
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
                      <Label className="text-sm font-medium text-muted-foreground">Current Site</Label>
                      <p className="text-sm">{selectedAsset.currentSiteLocationName || 'Not assigned'}</p>
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
                          ? formatMoney(selectedAsset.currentValue)
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
                    <div className="mt-4 grid grid-cols-3 gap-4">
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Plate Number</Label>
                        <p className="text-sm">{selectedAsset.licensePlate || 'Not specified'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">VIN</Label>
                        <p className="text-sm">{selectedAsset.vin || 'Not specified'}</p>
                      </div>
                      <div>
                        <Label className="text-sm font-medium text-muted-foreground">Fuel Type</Label>
                        <p className="text-sm">{selectedAsset.fuelType || 'Not specified'}</p>
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

              <TabsContent value="schedule" className="space-y-3">
                <div className="overflow-hidden rounded-md border border-blue-100 bg-blue-50/40 shadow-sm">
                  <div className="flex flex-col gap-4 border-b border-blue-100 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex items-center gap-3">
                      <span className="flex h-10 w-10 items-center justify-center rounded-md bg-blue-600 text-white shadow-sm">
                        <Calendar className="h-5 w-5" />
                      </span>
                      <div>
                        <h4 className="text-sm font-semibold text-slate-900">Maintenance Schedule</h4>
                        <p className="text-xs text-slate-500">Planned and previous maintenance dates for this asset.</p>
                      </div>
                    </div>
                    <Button
                      size="sm"
                      className="bg-blue-600 text-white hover:bg-blue-700"
                      onClick={() => router.push(`/maintenance/scheduled?create=1&assetId=${selectedAsset.id}`)}
                    >
                      <Calendar className="mr-2 h-4 w-4" />
                      Schedule Maintenance
                    </Button>
                  </div>
                  <div className="grid gap-3 p-4 sm:grid-cols-2">
                    <div className="rounded-md border border-slate-200 bg-white p-3">
                      <p className="text-xs font-medium text-slate-500">Last Maintenance</p>
                      <p className="mt-1 text-base font-semibold text-slate-900">
                        {selectedAsset.lastMaintenanceDate ? formatDate(selectedAsset.lastMaintenanceDate) : 'No maintenance recorded'}
                      </p>
                    </div>
                    <div className={cn(
                      'rounded-md border p-3',
                      isMaintenanceOverdue(selectedAsset.nextMaintenanceDate)
                        ? 'border-red-200 bg-red-50'
                        : selectedAsset.nextMaintenanceDate
                          ? 'border-emerald-200 bg-emerald-50'
                          : 'border-slate-200 bg-white',
                    )}>
                      <div className="flex items-center justify-between gap-3">
                        <p className="text-xs font-medium text-slate-500">Next Maintenance</p>
                        {isMaintenanceOverdue(selectedAsset.nextMaintenanceDate) ? (
                          <Badge variant="destructive">Overdue</Badge>
                        ) : selectedAsset.nextMaintenanceDate ? (
                          <Badge variant="outline" className="border-green-200 bg-green-100 text-green-800 hover:bg-green-100">Scheduled</Badge>
                        ) : (
                          <Badge variant="outline" className="border-slate-200 bg-slate-100 text-slate-700 hover:bg-slate-100">Not scheduled</Badge>
                        )}
                      </div>
                      <p className={cn(
                        'mt-1 text-base font-semibold',
                        isMaintenanceOverdue(selectedAsset.nextMaintenanceDate) ? 'text-red-800' : 'text-slate-900',
                      )}>
                        {selectedAsset.nextMaintenanceDate ? formatDate(selectedAsset.nextMaintenanceDate) : 'No date set'}
                      </p>
                    </div>
                  </div>
                </div>
              </TabsContent>

              <TabsContent value="movement">
                {lifecycleLoading ? (
                  <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Loading movement history...
                  </div>
                ) : sortedMovementHistory.length ? (
                  <div className="overflow-hidden rounded-md border bg-background">
                    <div className="hidden grid-cols-[150px_minmax(0,1fr)_28px_minmax(0,1fr)_minmax(140px,0.75fr)] items-center gap-2 border-b bg-muted/40 px-3 py-1.5 text-xs font-medium text-muted-foreground md:grid">
                      <span>Date / Time</span>
                      <span>From</span>
                      <span />
                      <span>To</span>
                      <span>Reason</span>
                    </div>
                    <div className="divide-y">
                      {sortedMovementHistory.map((movement, index) => {
                        const from = getMovementPlace(movement.fromProjectName, movement.fromSiteLocationName, movement.fromLocation);
                        const to = getMovementPlace(movement.toProjectName, movement.toSiteLocationName, movement.toLocation);

                        return (
                          <div
                            key={movement.id}
                            className={cn(
                              'grid grid-cols-[minmax(0,1fr)_28px_minmax(0,1fr)] items-center gap-2 px-3 py-2 md:grid-cols-[150px_minmax(0,1fr)_28px_minmax(0,1fr)_minmax(140px,0.75fr)]',
                              index % 2 === 1 && 'bg-muted/10',
                            )}
                          >
                            <div className="col-span-3 flex items-center justify-between gap-3 text-xs text-muted-foreground md:col-span-1 md:block">
                              <span className="md:hidden">Moved</span>
                              <span className="whitespace-nowrap">{formatDateTime(movement.effectiveDate)}</span>
                            </div>
                            <div className="min-w-0">
                              <p className="truncate text-sm font-medium" title={from.primary}>{from.primary}</p>
                              {from.context ? <p className="truncate text-xs text-muted-foreground" title={from.context}>{from.context}</p> : null}
                            </div>
                            <span className="flex h-7 w-7 items-center justify-center rounded-full bg-blue-50 text-blue-600">
                              <ArrowRight className="h-4 w-4" />
                            </span>
                            <div className="min-w-0">
                              <p className="truncate text-sm font-semibold text-blue-700" title={to.primary}>{to.primary}</p>
                              {to.context ? <p className="truncate text-xs text-muted-foreground" title={to.context}>{to.context}</p> : null}
                            </div>
                            <div className="col-span-3 min-w-0 text-sm md:col-span-1">
                              <p className="truncate" title={movement.reason}>{movement.reason || 'Asset movement'}</p>
                              {movement.notes ? <p className="truncate text-xs text-muted-foreground" title={movement.notes}>{movement.notes}</p> : null}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ) : (
                  <p className="py-8 text-center text-sm text-muted-foreground">No movement history.</p>
                )}
              </TabsContent>

              <TabsContent value="service" className="space-y-3">
                {lifecycleLoading ? (
                  <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Loading service history...
                  </div>
                ) : sortedServiceHistory.length ? (
                  <div className="overflow-x-auto rounded-md border bg-background">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-slate-100 hover:bg-slate-100">
                          <TableHead>Work Order</TableHead>
                          <TableHead>Service</TableHead>
                          <TableHead>Type</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Completed / Created</TableHead>
                          <TableHead>Cost</TableHead>
                          <TableHead className="text-right">Action</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {sortedServiceHistory.map((record, index) => (
                          <TableRow key={record.id} className={cn('hover:bg-blue-50/70', index % 2 === 1 && 'bg-slate-50')}>
                            <TableCell className="whitespace-nowrap font-mono text-sm font-medium">{record.workOrderNumber}</TableCell>
                            <TableCell className="min-w-56 font-medium">{record.title}</TableCell>
                            <TableCell>{record.maintenanceType || record.workOrderType || 'Service'}</TableCell>
                            <TableCell>{getWorkOrderStatusBadge(record.status)}</TableCell>
                            <TableCell className="whitespace-nowrap text-sm">{formatDateTime(record.actualCompletionDate || record.createdAt)}</TableCell>
                            <TableCell className="whitespace-nowrap font-medium">{formatMoney(record.actualCost || 0)}</TableCell>
                            <TableCell className="text-right">
                              <Button
                                size="sm"
                                className="bg-blue-600 text-white hover:bg-blue-700"
                                onClick={() => router.push(`/maintenance/work-orders?id=${record.id}`)}
                              >
                                Review
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                ) : (
                  <p className="py-8 text-center text-sm text-muted-foreground">No completed service history.</p>
                )}
              </TabsContent>

              <TabsContent value="inspections" className="space-y-3">
                {assetInspectionsLoading ? (
                  <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Loading inspections...
                  </div>
                ) : assetInspectionsError ? (
                  <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-800">
                    <p className="font-medium">Inspection records could not be loaded.</p>
                    <p className="mt-1">{assetInspectionsError}</p>
                    <Button className="mt-3" size="sm" variant="outline" onClick={() => void loadAssetInspections(selectedAsset.id)}>
                      Try Again
                    </Button>
                  </div>
                ) : assetInspections.length ? (
                  <div className="overflow-x-auto rounded-md border bg-background">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-slate-100 hover:bg-slate-100">
                          <TableHead>Date / Time</TableHead>
                          <TableHead>Sheet</TableHead>
                          <TableHead>Kind</TableHead>
                          <TableHead>Result</TableHead>
                          <TableHead>Workflow</TableHead>
                          <TableHead>Follow-up</TableHead>
                          <TableHead className="text-right">Action</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {assetInspections.map((inspection, index) => (
                          <TableRow
                            key={inspection.id}
                            className={cn('cursor-pointer hover:bg-blue-50/70', index % 2 === 1 && 'bg-slate-50')}
                            onClick={() => void openAssetInspectionReview(inspection)}
                          >
                            <TableCell className="whitespace-nowrap">
                              {formatDateTime(inspection.completedAtUtc || inspection.startedAtUtc) || '-'}
                              {inspection.capturedOfflineAtUtc ? (
                                <div className="text-xs text-muted-foreground">Offline: {formatDateTime(inspection.capturedOfflineAtUtc)}</div>
                              ) : null}
                            </TableCell>
                            <TableCell>
                              <div className="font-medium">{inspection.inspectionTemplateName}</div>
                              <div className="text-xs text-muted-foreground">{getSheetTypeLabel(inspection.sheetType)}</div>
                            </TableCell>
                            <TableCell>{inspection.inspectionKind || '-'}</TableCell>
                            <TableCell>{getInspectionResultBadge(inspection.overallResult)}</TableCell>
                            <TableCell>{getInspectionStatusBadge(inspection.status)}</TableCell>
                            <TableCell>
                              {inspection.workOrderId ? (
                                <span className="text-xs">Work Order linked</span>
                              ) : inspection.defectId ? (
                                <span className="text-xs">Defect linked</span>
                              ) : (
                                <span className="text-xs text-muted-foreground">None</span>
                              )}
                            </TableCell>
                            <TableCell className="text-right">
                              <Button
                                size="sm"
                                className="bg-blue-600 text-white hover:bg-blue-700"
                                onClick={(event) => {
                                  event.stopPropagation();
                                  void openAssetInspectionReview(inspection);
                                }}
                              >
                                Review
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                ) : (
                  <p className="py-8 text-center text-sm text-muted-foreground">No inspection history.</p>
                )}
              </TabsContent>

              <TabsContent value="work-orders" className="space-y-3">
                {lifecycleLoading ? (
                  <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Loading work orders...
                  </div>
                ) : sortedWorkOrderHistory.length ? (
                  <div className="overflow-x-auto rounded-md border bg-background">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-slate-100 hover:bg-slate-100">
                          <TableHead>Work Order</TableHead>
                          <TableHead>Title</TableHead>
                          <TableHead>Type</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Completed / Created</TableHead>
                          <TableHead>Cost</TableHead>
                          <TableHead className="text-right">Action</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {sortedWorkOrderHistory.map((record, index) => (
                          <TableRow key={record.id} className={cn('hover:bg-blue-50/70', index % 2 === 1 && 'bg-slate-50')}>
                            <TableCell className="whitespace-nowrap font-mono text-sm font-medium">{record.workOrderNumber}</TableCell>
                            <TableCell className="min-w-56 font-medium">{record.title}</TableCell>
                            <TableCell>{record.workOrderType || record.maintenanceType || 'Work order'}</TableCell>
                            <TableCell>{getWorkOrderStatusBadge(record.status)}</TableCell>
                            <TableCell className="whitespace-nowrap text-sm">{formatDateTime(record.actualCompletionDate || record.createdAt)}</TableCell>
                            <TableCell className="whitespace-nowrap font-medium">{formatMoney(record.actualCost || 0)}</TableCell>
                            <TableCell className="text-right">
                              <Button
                                size="sm"
                                className="bg-blue-600 text-white hover:bg-blue-700"
                                onClick={() => router.push(`/maintenance/work-orders?id=${record.id}`)}
                              >
                                Review
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                ) : (
                  <p className="py-8 text-center text-sm text-muted-foreground">No work-order history.</p>
                )}
              </TabsContent>

              {selectedAssetIsVehicle && (
                <TabsContent value="fleet" className="space-y-4">
                  <AssetVehicleFleetTabs vehicleAssetId={selectedAsset.id} />
                </TabsContent>
              )}
              </div>
            </div>
            </Tabs>
          )}
          <DialogFooter>
            {selectedAssetSupportsPreStart && (
              <Button variant="outline" onClick={() => void openQrDialog()}>
                <QrCode className="mr-2 h-4 w-4" />QR Checklist
              </Button>
            )}
            <Button variant="outline" onClick={openMoveDialog}>
              <ArrowRightLeft className="mr-2 h-4 w-4" />Move Asset
            </Button>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isMoveDialogOpen} onOpenChange={setIsMoveDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Move Asset</DialogTitle>
            <DialogDescription>{selectedAsset?.assetNumber} · {selectedAsset?.name}</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Site</Label>
              <Select value={moveForm.siteLocationId} onValueChange={(value) => {
                const site = locations.find((item) => item.id === value);
                setMoveForm((current) => ({ ...current, siteLocationId: value, location: value === 'none' ? current.location : (site?.name || current.location) }));
              }}>
                <SelectTrigger><SelectValue placeholder="Unassigned" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Unassigned</SelectItem>
                  {locations.map((location) => <SelectItem key={location.id} value={location.id}>{location.code ? `${location.code} · ` : ''}{location.name}</SelectItem>)}
                  {locations.length === 0 && <SelectItem value="no-sites" disabled>No active HR locations configured</SelectItem>}
                </SelectContent>
              </Select>
              <p className={`text-xs ${locationLookupError ? 'text-destructive' : 'text-muted-foreground'}`}>
                {locationLookupError || 'Sites come from active HR Location master records (/api/Location/summary).'}
              </p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="move-location">Location Detail</Label>
              <Input id="move-location" value={moveForm.location} onChange={(event) => setMoveForm((current) => ({ ...current, location: event.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="move-date">Effective Date / Time</Label>
              <Input id="move-date" type="datetime-local" value={moveForm.effectiveDate} onChange={(event) => setMoveForm((current) => ({ ...current, effectiveDate: event.target.value }))} />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="move-reason">Reason *</Label>
              <Input id="move-reason" value={moveForm.reason} onChange={(event) => setMoveForm((current) => ({ ...current, reason: event.target.value }))} />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="move-notes">Notes</Label>
              <Textarea id="move-notes" value={moveForm.notes} onChange={(event) => setMoveForm((current) => ({ ...current, notes: event.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsMoveDialogOpen(false)}>Cancel</Button>
            <Button onClick={() => void moveAsset()} disabled={!moveForm.reason.trim() || movingAsset}>{movingAsset && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Move</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={assetInspectionDialogOpen}
        onOpenChange={(open) => {
          setAssetInspectionDialogOpen(open);
          if (!open) setInspectionPhotoPreview(null);
        }}
      >
        <DialogContent className="flex max-h-[90vh] w-[95vw] max-w-4xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>Inspection Review</DialogTitle>
            <DialogDescription>
              {selectedAssetInspection?.vehicleAssetNumber || selectedAsset?.assetNumber || 'Asset'} · {selectedAssetInspection?.inspectionTemplateName || 'Inspection'}
            </DialogDescription>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto pr-1">
            {selectedAssetInspection ? (
              <div className="space-y-4">
                <div className="grid grid-cols-1 gap-3 rounded-md border p-3 text-sm md:grid-cols-4">
                  <div>
                    <div className="text-xs text-muted-foreground">Date / Time</div>
                    <div className="font-medium">{formatDateTime(selectedAssetInspection.completedAtUtc || selectedAssetInspection.startedAtUtc) || '-'}</div>
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground">Sheet</div>
                    <div className="font-medium">{getSheetTypeLabel(selectedAssetInspection.sheetType)}</div>
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground">Result</div>
                    <div>{getInspectionResultBadge(selectedAssetInspection.overallResult)}</div>
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground">Workflow</div>
                    <div>{getInspectionStatusBadge(selectedAssetInspection.status)}</div>
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground">Inspector</div>
                    <div className="font-medium">{selectedAssetInspection.inspectorEmployeeName || 'Mobile user'}</div>
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground">Kind</div>
                    <div className="font-medium">{selectedAssetInspection.inspectionKind || '-'}</div>
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground">Offline Captured</div>
                    <div className="font-medium">{formatDateTime(selectedAssetInspection.capturedOfflineAtUtc) || '-'}</div>
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground">Synced</div>
                    <div className="font-medium">{formatDateTime(selectedAssetInspection.syncedAtUtc) || '-'}</div>
                  </div>
                </div>

                <div className="rounded-md border">
                  <div className="border-b px-3 py-2 text-sm font-medium">Checklist Answers</div>
                  {assetInspectionTemplateLoading ? (
                    <div className="flex items-center gap-2 p-4 text-sm text-muted-foreground">
                      <Loader2 className="h-4 w-4 animate-spin" /> Loading checklist context...
                    </div>
                  ) : assetInspectionReviewRows.length ? (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="w-16">#</TableHead>
                          <TableHead>Item</TableHead>
                          <TableHead className="w-44">Response</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {assetInspectionReviewRows.map((row, index) => (
                          <TableRow key={row.id || index}>
                            <TableCell>{index + 1}</TableCell>
                            <TableCell>
                              <div className="font-medium">{row.item}</div>
                              <div className="text-xs text-muted-foreground">
                                {row.type}{row.required ? ' · Required' : ''}
                              </div>
                              {row.photo ? (
                                <div className="mt-2 space-y-2">
                                  <button
                                    type="button"
                                    className="group relative block w-full overflow-hidden rounded-md border bg-muted/30 text-left"
                                    onClick={() => setInspectionPhotoPreview({ url: row.photo as string, item: row.item, index })}
                                    title={`View ${row.item} photo evidence`}
                                  >
                                    <img src={row.photo} alt={`${row.item} evidence`} className="max-h-56 w-full object-contain transition group-hover:scale-[1.01]" />
                                    <span className="absolute inset-x-0 bottom-0 bg-black/60 px-2 py-1 text-xs font-medium text-white opacity-0 transition group-hover:opacity-100">
                                      Click to enlarge
                                    </span>
                                  </button>
                                  <div className="flex flex-wrap gap-2">
                                    <Button
                                      type="button"
                                      size="sm"
                                      variant="outline"
                                      onClick={() => setInspectionPhotoPreview({ url: row.photo as string, item: row.item, index })}
                                    >
                                      <Eye className="mr-2 h-4 w-4" />
                                      View photo
                                    </Button>
                                    <Button
                                      type="button"
                                      size="sm"
                                      variant="outline"
                                      onClick={() => downloadInspectionPhoto(row.photo as string, row.item, index)}
                                    >
                                      <Download className="mr-2 h-4 w-4" />
                                      Download
                                    </Button>
                                  </div>
                                </div>
                              ) : null}
                            </TableCell>
                            <TableCell>
                              {row.value ? (
                                <Badge variant={['Fail', 'Failed', 'No'].includes(row.value) ? 'destructive' : 'outline'}>{row.value}</Badge>
                              ) : (
                                <span className="text-sm text-muted-foreground">No response</span>
                              )}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  ) : (
                    <div className="p-4 text-sm text-muted-foreground">No checklist answers were stored for this inspection.</div>
                  )}
                </div>

                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Notes</div>
                    <div className="mt-1 whitespace-pre-wrap text-sm">{selectedAssetInspection.notes || 'No notes.'}</div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Follow-up</div>
                    <div className="mt-1 text-sm">
                      {selectedAssetInspection.workOrderId ? 'Linked Work Order created.' : selectedAssetInspection.defectId ? 'Linked Fleet Defect created.' : 'No defect or Work Order was created.'}
                    </div>
                  </div>
                </div>
              </div>
            ) : (
              <div className="py-8 text-center text-sm text-muted-foreground">No inspection selected.</div>
            )}
          </div>

          <DialogFooter className="flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:space-x-0">
            {selectedAssetInspection && !['InProgress', 'Cancelled'].includes(selectedAssetInspection.status) ? (
              <WorkflowApprovalActions
                entityType="FleetTripInspection"
                entityId={selectedAssetInspection.id}
                entityLabel="Asset Inspection"
                entityNumber={selectedAssetInspection.vehicleAssetNumber || selectedAsset?.assetNumber}
                status={selectedAssetInspection.status}
                loadWorkflowSummary
                canSubmit={selectedAssetInspection.status === 'Completed' || selectedAssetInspection.status === 'Rejected'}
                canApproveReject={selectedAssetInspection.status === 'Submitted'}
                onSubmit={async () => {
                  await refreshSelectedAssetInspection(await fleetService.submitInspectionApproval(selectedAssetInspection.id));
                }}
                onApprove={async (comments) => {
                  await refreshSelectedAssetInspection(await fleetService.approveInspection(selectedAssetInspection.id, comments));
                }}
                onReject={async (comments) => {
                  await refreshSelectedAssetInspection(await fleetService.rejectInspection(selectedAssetInspection.id, comments));
                }}
                onAfterAction={async () => {
                  await refreshSelectedAssetInspection();
                }}
              />
            ) : <div />}
            <Button variant="outline" onClick={() => setAssetInspectionDialogOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={!!inspectionPhotoPreview} onOpenChange={(open) => {
        if (!open) setInspectionPhotoPreview(null);
      }}>
        <DialogContent className="flex max-h-[92vh] w-[95vw] max-w-5xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>Inspection Photo Evidence</DialogTitle>
            <DialogDescription>
              {inspectionPhotoPreview?.item || 'Checklist item'} · Original mobile attachment
            </DialogDescription>
          </DialogHeader>

          <div className="flex-1 overflow-auto rounded-lg border bg-slate-950 p-3">
            {inspectionPhotoPreview ? (
              <img
                src={inspectionPhotoPreview.url}
                alt={`${inspectionPhotoPreview.item} evidence preview`}
                className="mx-auto max-h-[70vh] max-w-full object-contain"
              />
            ) : null}
          </div>

          <DialogFooter>
            {inspectionPhotoPreview ? (
              <Button
                type="button"
                variant="outline"
                onClick={() => downloadInspectionPhoto(
                  inspectionPhotoPreview.url,
                  inspectionPhotoPreview.item,
                  inspectionPhotoPreview.index,
                )}
              >
                <Download className="mr-2 h-4 w-4" />
                Download photo
              </Button>
            ) : null}
            <Button type="button" onClick={() => setInspectionPhotoPreview(null)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isQrDialogOpen} onOpenChange={setIsQrDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Asset Pre-Start QR</DialogTitle>
            <DialogDescription>{selectedAsset?.assetNumber} · {selectedAsset?.name}</DialogDescription>
          </DialogHeader>
          {qrTemplates.length > 1 && (
            <div className="space-y-2">
              <Label>Checklist</Label>
              <Select value={selectedQrTemplateId} onValueChange={(value) => void generateAssetQr(value)}>
                <SelectTrigger><SelectValue placeholder="Select checklist" /></SelectTrigger>
                <SelectContent>{qrTemplates.map((template) => <SelectItem key={template.id} value={template.id}>{template.name}</SelectItem>)}</SelectContent>
              </Select>
            </div>
          )}
          <div className="flex min-h-72 items-center justify-center rounded-md border bg-white p-5 text-black">
            {qrLoading ? <Loader2 className="h-6 w-6 animate-spin" /> : qrPackage ? (
              <div ref={assetQrLabelRef} className="text-center">
                <div className="label-kicker text-xs font-semibold uppercase text-muted-foreground">Fleet Asset Inspection</div>
                <QRCodeSVG value={qrPackage.mobileUrl} size={220} level="M" includeMargin />
                <div className="label-title mt-2 font-semibold">
                  {qrPackage.assetName || selectedAsset?.name || 'Fleet asset'}
                </div>
                <div className="label-description text-sm">
                  Asset No: {qrPackage.assetNumber || selectedAsset?.assetNumber || 'Not assigned'}
                </div>
                <div className="label-description text-sm">
                  Asset Type: {qrPackage.assetCategoryName || selectedAsset?.category || 'Fleet asset'}
                </div>
                <div className="label-description text-sm">Checklist: {qrPackage.templateName}</div>
              </div>
            ) : <p className="text-sm text-muted-foreground">No active QR-enabled pre-start checklist matches this asset.</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsQrDialogOpen(false)}>Close</Button>
            <Button
              onClick={() => {
                if (!printQrLabel(assetQrLabelRef.current, qrPackage?.assetNumber || 'Asset QR Label')) {
                  toast({ title: 'Print window blocked', description: 'Allow pop-ups for this site and try again.', variant: 'destructive' });
                }
              }}
              disabled={!qrPackage}
            ><Printer className="mr-2 h-4 w-4" />Print</Button>
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
