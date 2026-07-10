'use client';

import React, { useState, useEffect, useRef } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { 
  Plus,
  Search,
  Edit,
  Trash2,
  MoreHorizontal,
  FileText,
  CheckSquare,
  ClipboardList,
  Users,
  Clock,
  AlertTriangle,
  Eye,
  Copy,
  QrCode,
  Printer,
  ChevronUp,
  ChevronDown,
  X
} from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';
import { 
  InspectionTemplate,
  CreateInspectionTemplateDto,
  UpdateInspectionTemplateDto,
  InspectionTemplateQrPackage,
  inspectionTemplateService
} from '@/services/inspectionTemplateService';
import {
  maintenanceDataService,
  Asset,
  AssetCategory,
  WorkOrderType,
  MaintenanceType,
  PriorityLevel,
} from '@/services/maintenanceDataService';
import { printQrLabel } from '@/lib/print-qr-label';


export default function InspectionTemplatesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [sheetTypeFilter, setSheetTypeFilter] = useState('all');
  const [scopeFilter, setScopeFilter] = useState('all');
  const [frequencyFilter, setFrequencyFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isQrDialogOpen, setIsQrDialogOpen] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<InspectionTemplate | null>(null);
  const [qrTemplate, setQrTemplate] = useState<InspectionTemplate | null>(null);
  const [qrPackage, setQrPackage] = useState<InspectionTemplateQrPackage | null>(null);
  const [qrLoading, setQrLoading] = useState(false);
  const [qrError, setQrError] = useState<string | null>(null);
  const qrLabelRef = useRef<HTMLDivElement>(null);
  const [qrForm, setQrForm] = useState({
    inspectionKind: 'PreTrip',
    assetCategoryId: 'none',
    assetId: 'none',
  });
  const [templatesData, setTemplatesData] = useState<InspectionTemplate[]>([]);
  const [filteredData, setFilteredData] = useState<InspectionTemplate[]>([]);
  const [assetCategories, setAssetCategories] = useState<AssetCategory[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [workOrderTypes, setWorkOrderTypes] = useState<WorkOrderType[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<MaintenanceType[]>([]);
  const [priorityLevels, setPriorityLevels] = useState<PriorityLevel[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [formFieldErrors, setFormFieldErrors] = useState<Record<string, string>>({});
  
  // Form state
  const [formData, setFormData] = useState<CreateInspectionTemplateDto>({
    name: '',
    code: '',
    description: '',
    category: 'Safety',
    sheetType: 'InspectionSheet',
    templateScope: 'General',
    fleetInspectionKind: 'Any',
    assignedAssetCategoryId: null,
    assignedAssetId: null,
    isQrEnabled: false,
    mobileOfflineEnabled: false,
    qrPayloadVersion: 1,
    autoCreateWorkOrderOnFailure: true,
    failureWorkOrderTypeId: null,
    failureMaintenanceTypeId: null,
    failurePriorityLevelId: null,
    failureBillingType: 'Default',
    frequency: 'Monthly',
    estimatedDuration: 60,
    isActive: true,
    requiresSignature: false,
    allowPhotos: false,
    version: '1.0',
    assetTypes: [],
    inspectorRoles: [],
    priority: 'Medium',
    checklistItems: []
  });

  const apiFieldToFormField: Record<string, string> = {
    Name: 'name',
    Code: 'code',
    Description: 'description',
    Category: 'category',
    SheetType: 'sheetType',
    TemplateScope: 'templateScope',
    FleetInspectionKind: 'fleetInspectionKind',
    AssignedAssetCategoryId: 'assignedAssetCategoryId',
    AssignedAssetId: 'assignedAssetId',
    Frequency: 'frequency',
    EstimatedDuration: 'estimatedDuration',
    Priority: 'priority',
    Version: 'version'
  };

  const clearFormErrors = () => setFormFieldErrors({});

  const applyApiValidationErrors = (apiErrors: Record<string, string[]>) => {
    const next: Record<string, string> = {};
    for (const [apiField, messages] of Object.entries(apiErrors || {})) {
      if (!messages?.length) continue;
      const formField = apiFieldToFormField[apiField] ?? apiField;
      next[formField] = messages.join(' ');
    }
    setFormFieldErrors(next);
  };

  const getFriendlyErrorMessage = (err: unknown) => {
    const anyErr = err as any;
    const apiResponse = anyErr?.response;
    const apiErrors: Record<string, string[]> | undefined = apiResponse?.errors;

    if (apiErrors && Object.keys(apiErrors).length > 0) {
      const flattened = Object.entries(apiErrors)
        .flatMap(([field, messages]) => (messages || []).map((m) => `${field}: ${m}`))
        .filter(Boolean);
      if (flattened.length > 0) return flattened.join(' ');
    }

    return (
      apiResponse?.detail ||
      apiResponse?.message ||
      apiResponse?.title ||
      (err instanceof Error ? err.message : null) ||
      'An unexpected error occurred.'
    );
  };

  const validateTemplateForm = (data: CreateInspectionTemplateDto) => {
    const nextErrors: Record<string, string> = {};
    const name = (data.name || '').trim();
    const code = (data.code || '').trim();
    const category = (data.category || '').trim();
    const frequency = (data.frequency || '').trim();
    const priority = (data.priority || '').trim();
    const estimatedDuration = Number(data.estimatedDuration);
    const qrPayloadVersion = Number(data.qrPayloadVersion || 1);

    if (!name) nextErrors.name = 'Template name is required.';
    if (!code) nextErrors.code = 'Template code is required.';
    if (!category) nextErrors.category = 'Category is required.';
    if (!frequency) nextErrors.frequency = 'Frequency is required.';
    if (!priority) nextErrors.priority = 'Priority is required.';

    if (!Number.isFinite(estimatedDuration) || estimatedDuration < 1 || estimatedDuration > 1440) {
      nextErrors.estimatedDuration = 'Estimated duration must be between 1 and 1440 minutes.';
    }

    if (!Number.isFinite(qrPayloadVersion) || qrPayloadVersion < 1 || qrPayloadVersion > 99) {
      nextErrors.qrPayloadVersion = 'QR payload version must be between 1 and 99.';
    }

    const isValid = Object.keys(nextErrors).length === 0;
    return { isValid, nextErrors };
  };

  const normalizeChecklistOrders = (items: CreateInspectionTemplateDto['checklistItems']) =>
    (items || []).map((i, idx) => ({ ...i, order: idx + 1 }));

  const addChecklistItem = () => {
    setFormData((p) => {
      const next = [...(p.checklistItems || [])];
      next.push({ item: '', type: 'checklist', required: false, order: next.length + 1 });
      return { ...p, checklistItems: next };
    });
  };

  const removeChecklistItem = (index: number) => {
    setFormData((p) => {
      const next = (p.checklistItems || []).filter((_, i) => i !== index);
      return { ...p, checklistItems: normalizeChecklistOrders(next) };
    });
  };

  const moveChecklistItem = (index: number, direction: 'up' | 'down') => {
    setFormData((p) => {
      const items = [...(p.checklistItems || [])];
      const target = direction === 'up' ? index - 1 : index + 1;
      if (target < 0 || target >= items.length) return p;
      const tmp = items[index];
      items[index] = items[target];
      items[target] = tmp;
      return { ...p, checklistItems: normalizeChecklistOrders(items) };
    });
  };

  const updateChecklistItem = (index: number, patch: Partial<CreateInspectionTemplateDto['checklistItems'][number]>) => {
    setFormData((p) => {
      const next = [...(p.checklistItems || [])];
      next[index] = { ...next[index], ...patch };
      return { ...p, checklistItems: normalizeChecklistOrders(next) };
    });
  };

  // Fetch templates from API
  const fetchTemplates = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await inspectionTemplateService.getAllTemplates();
      setTemplatesData(data);
    } catch (error) {
      console.error('Error fetching inspection templates:', error);
      setError('Failed to load inspection templates. Please try again later.');
      setTemplatesData([]);
    } finally {
      setLoading(false);
    }
  };

  const fetchAssignmentLookups = async () => {
    try {
      const [categories, assetList, workOrderTypeList, maintenanceTypeList, priorityLevelList] = await Promise.all([
        maintenanceDataService.getAssetCategories(),
        maintenanceDataService.getAssets(),
        maintenanceDataService.getWorkOrderTypes(),
        maintenanceDataService.getMaintenanceTypes(),
        maintenanceDataService.getPriorityLevels(),
      ]);

      setAssetCategories((categories || []).filter((c) => c.isActive !== false));
      setAssets((assetList || []).filter((a) => a.isActive !== false));
      setWorkOrderTypes((workOrderTypeList || []).filter((item) => item.isActive !== false));
      setMaintenanceTypes((maintenanceTypeList || []).filter((item) => item.isActive !== false));
      setPriorityLevels((priorityLevelList || []).filter((item) => item.isActive !== false));
    } catch (err) {
      console.error('Error fetching inspection template assignment lookups:', err);
      setAssetCategories([]);
      setAssets([]);
      setWorkOrderTypes([]);
      setMaintenanceTypes([]);
      setPriorityLevels([]);
    }
  };
  
  useEffect(() => {
    fetchTemplates();
    fetchAssignmentLookups();
  }, []);

  useEffect(() => {
    let filtered = templatesData;

    if (searchTerm) {
      filtered = filtered.filter(item =>
        item.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.code?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.description?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        item.category?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter !== 'all') {
      filtered = filtered.filter(item =>
        statusFilter === 'active' ? item.isActive : !item.isActive
      );
    }

    if (categoryFilter !== 'all') {
      filtered = filtered.filter(item => item.category === categoryFilter);
    }

    if (sheetTypeFilter !== 'all') {
      filtered = filtered.filter(item => (item.sheetType || 'InspectionSheet') === sheetTypeFilter);
    }

    if (scopeFilter !== 'all') {
      filtered = filtered.filter(item => (item.templateScope || 'General') === scopeFilter);
    }

    if (frequencyFilter !== 'all') {
      filtered = filtered.filter(item => item.frequency === frequencyFilter);
    }

    setFilteredData(filtered);
  }, [searchTerm, statusFilter, categoryFilter, sheetTypeFilter, scopeFilter, frequencyFilter, templatesData]);

  const handleCreate = async () => {
    if (isSubmitting) return;
    
    try {
      setIsSubmitting(true);
      setError(null);
      clearFormErrors();

      const { isValid, nextErrors } = validateTemplateForm(formData);
      if (!isValid) {
        setFormFieldErrors(nextErrors);
        setError('Please fix the highlighted fields and try again.');
        return;
      }

      console.log('Creating inspection template with data:', formData);
      const newTemplate = await inspectionTemplateService.createTemplate(formData);
      setTemplatesData([...templatesData, newTemplate]);
      setIsCreateDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error creating inspection template:', error);
      const apiErrors: Record<string, string[]> | undefined = (error as any)?.response?.errors;
      if (apiErrors) applyApiValidationErrors(apiErrors);
      setError(getFriendlyErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEdit = (template: InspectionTemplate) => {
    setSelectedTemplate(template);
    setFormData({
      name: template.name ?? '',
      code: template.code ?? '',
      description: template.description ?? '',
      category: template.category || 'Safety',
      sheetType: template.sheetType || 'InspectionSheet',
      templateScope: template.templateScope || 'General',
      fleetInspectionKind: template.fleetInspectionKind || 'Any',
      assignedAssetCategoryId: template.assignedAssetCategoryId || null,
      assignedAssetId: template.assignedAssetId || null,
      isQrEnabled: !!template.isQrEnabled,
      mobileOfflineEnabled: !!template.mobileOfflineEnabled,
      qrPayloadVersion: template.qrPayloadVersion || 1,
      autoCreateWorkOrderOnFailure: template.autoCreateWorkOrderOnFailure !== false,
      failureWorkOrderTypeId: template.failureWorkOrderTypeId || null,
      failureMaintenanceTypeId: template.failureMaintenanceTypeId || null,
      failurePriorityLevelId: template.failurePriorityLevelId || null,
      failureBillingType: template.failureBillingType || 'Default',
      frequency: template.frequency || 'Monthly',
      estimatedDuration: template.estimatedDuration && template.estimatedDuration > 0 ? template.estimatedDuration : 60,
      isActive: !!template.isActive,
      requiresSignature: !!template.requiresSignature,
      allowPhotos: !!template.allowPhotos,
      version: template.version || '1.0',
      assetTypes: template.assetTypes || [],
      inspectorRoles: template.inspectorRoles || [],
      priority: template.priority || 'Medium',
      checklistItems: template.checklistItems.map((item, idx) => ({
        item: item.item,
        type: item.type,
        required: item.required,
        order: item.order ?? (idx + 1)
      }))
    });
    clearFormErrors();
    setError(null);
    setIsEditDialogOpen(true);
  };

  const handleView = (template: InspectionTemplate) => {
    setSelectedTemplate(template);
    setIsViewDialogOpen(true);
  };

  const handleUpdate = async () => {
    if (!selectedTemplate?.id || isSubmitting) return;
    
    try {
      setIsSubmitting(true);
      setError(null);
      clearFormErrors();

      const { isValid, nextErrors } = validateTemplateForm(formData);
      if (!isValid) {
        setFormFieldErrors(nextErrors);
        setError('Please fix the highlighted fields and try again.');
        return;
      }
      const updateData: UpdateInspectionTemplateDto = {
        ...formData
      };
      console.log('Updating inspection template with data:', updateData);
      
      const updatedTemplate = await inspectionTemplateService.updateTemplate(selectedTemplate.id, updateData);
      setTemplatesData(templatesData.map(t => t.id === selectedTemplate.id ? updatedTemplate : t));
      setIsEditDialogOpen(false);
      resetForm();
    } catch (error) {
      console.error('Error updating inspection template:', error);
      const apiErrors: Record<string, string[]> | undefined = (error as any)?.response?.errors;
      if (apiErrors) applyApiValidationErrors(apiErrors);
      setError(getFriendlyErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (template: InspectionTemplate) => {
    if (!confirm(`Are you sure you want to delete "${template.name}"?`)) return;
    
    try {
      setError(null);
      console.log(`Deleting inspection template: ${template.name}`);
      await inspectionTemplateService.deleteTemplate(template.id);
      setTemplatesData(templatesData.filter(t => t.id !== template.id));
    } catch (error) {
      console.error('Error deleting inspection template:', error);
      setError('Failed to delete inspection template. Please try again.');
    }
  };

  const handleDuplicate = async (template: InspectionTemplate) => {
    try {
      setError(null);
      const newName = `${template.name} (Copy)`;
      const newCode = `${template.code}-COPY`;
      console.log(`Duplicating inspection template: ${template.name}`);
      
      const duplicatedTemplate = await inspectionTemplateService.duplicateTemplate(template.id, newName, newCode);
      setTemplatesData([...templatesData, duplicatedTemplate]);
    } catch (error) {
      console.error('Error duplicating inspection template:', error);
      setError('Failed to duplicate inspection template. Please try again.');
    }
  };

  const resetForm = () => {
    setFormData({
      name: '',
      code: '',
      description: '',
      category: 'Safety',
      sheetType: 'InspectionSheet',
      templateScope: 'General',
      fleetInspectionKind: 'Any',
      assignedAssetCategoryId: null,
      assignedAssetId: null,
      isQrEnabled: false,
      mobileOfflineEnabled: false,
      qrPayloadVersion: 1,
      autoCreateWorkOrderOnFailure: true,
      failureWorkOrderTypeId: null,
      failureMaintenanceTypeId: null,
      failurePriorityLevelId: null,
      failureBillingType: 'Default',
      frequency: 'Monthly',
      estimatedDuration: 60,
      isActive: true,
      requiresSignature: false,
      allowPhotos: false,
      version: '1.0',
      assetTypes: [],
      inspectorRoles: [],
      priority: 'Medium',
      checklistItems: []
    });
    clearFormErrors();
    setSelectedTemplate(null);
  };

  const formatDuration = (minutes: number) => {
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  };

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'Critical': return 'bg-red-100 text-red-800';
      case 'High': return 'bg-orange-100 text-orange-800';
      case 'Medium': return 'bg-yellow-100 text-yellow-800';
      case 'Low': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const generateQrPackage = async (
    template: InspectionTemplate,
    nextForm = qrForm
  ) => {
    try {
      setQrLoading(true);
      setQrError(null);
      const result = await inspectionTemplateService.getQrPackage(template.id, {
        inspectionKind: nextForm.inspectionKind,
        assetCategoryId: nextForm.assetCategoryId === 'none' ? null : nextForm.assetCategoryId,
        assetId: nextForm.assetId === 'none' ? null : nextForm.assetId,
        includeEmbeddedPayload: false,
      });
      setQrPackage(result);
    } catch (err) {
      console.error('Error generating inspection QR package:', err);
      setQrPackage(null);
      setQrError(getFriendlyErrorMessage(err));
    } finally {
      setQrLoading(false);
    }
  };

  const handleQrLabel = async (template: InspectionTemplate) => {
    const nextForm = {
      inspectionKind: template.templateScope === 'Fleet'
        ? (template.fleetInspectionKind && template.fleetInspectionKind !== 'Any' ? template.fleetInspectionKind : 'PreTrip')
        : template.sheetType === 'ServiceSheet'
          ? 'Service'
          : template.sheetType === 'WeeklyChecklist'
            ? 'Weekly'
            : template.sheetType === 'PreventiveMaintenanceForm'
              ? 'PreventiveMaintenance'
              : 'Inspection',
      assetCategoryId: template.assignedAssetCategoryId || 'none',
      assetId: template.assignedAssetId || 'none',
    };
    setQrTemplate(template);
    setQrForm(nextForm);
    setQrPackage(null);
    setQrError(null);
    setIsQrDialogOpen(true);
    await generateQrPackage(template, nextForm);
  };

  const handlePrintQrLabel = () => {
    const printed = printQrLabel(qrLabelRef.current, qrPackage?.templateName || 'Inspection QR Label');
    if (!printed) {
      setQrError('The print window was blocked. Allow pop-ups for this site and try again.');
    }
  };

  const vehicleOnlyAssets = assets.filter((asset) => {
    const assetType = (asset.assetType || asset.category || '').toLowerCase();
    return assetType.includes('vehicle') || assetType.includes('fleet');
  });
  const fleetAssets = vehicleOnlyAssets.length > 0 ? vehicleOnlyAssets : assets;
  const assignmentAssets = formData.templateScope === 'Fleet' ? fleetAssets : assets;
  const scopedFleetAssets = formData.assignedAssetCategoryId
    ? assignmentAssets.filter((asset) => asset.categoryId === formData.assignedAssetCategoryId)
    : assignmentAssets;
  const qrAssignmentAssets = qrTemplate?.templateScope === 'Fleet' ? fleetAssets : assets;
  const qrScopedFleetAssets = qrForm.assetCategoryId !== 'none'
    ? qrAssignmentAssets.filter((asset) => asset.categoryId === qrForm.assetCategoryId)
    : qrAssignmentAssets;

  const getAssetCategoryName = (id?: string | null) =>
    assetCategories.find((category) => category.id === id)?.name || 'Any asset category';

  const getAssetName = (id?: string | null) => {
    const asset = assets.find((item) => item.id === id);
    return asset ? `${asset.assetName} (${asset.assetCode})` : 'Any asset';
  };

  const getChecklistTypeLabel = (type?: string | null) => {
    switch ((type || '').toLowerCase()) {
      case 'checklist':
        return 'Pass / Fail';
      case 'yesno':
        return 'Yes / No';
      case 'text':
        return 'Text';
      case 'number':
        return 'Number';
      case 'measurement':
        return 'Measurement';
      default:
        return type || 'Checklist';
    }
  };

  const updateTemplateScope = (value: string) => {
    const isFleet = value === 'Fleet';
    setFormData({
      ...formData,
      templateScope: value,
      category: isFleet ? 'Fleet' : formData.category,
      fleetInspectionKind: isFleet ? (formData.fleetInspectionKind || 'Any') : 'Any',
      assignedAssetCategoryId: formData.assignedAssetCategoryId,
      assignedAssetId: formData.assignedAssetId,
      isQrEnabled: formData.isQrEnabled,
      mobileOfflineEnabled: formData.mobileOfflineEnabled,
      qrPayloadVersion: formData.qrPayloadVersion || 1,
      autoCreateWorkOrderOnFailure: formData.autoCreateWorkOrderOnFailure,
      failureWorkOrderTypeId: formData.failureWorkOrderTypeId,
      failureMaintenanceTypeId: formData.failureMaintenanceTypeId,
      failurePriorityLevelId: formData.failurePriorityLevelId,
      failureBillingType: formData.failureBillingType,
    });
  };

  const renderFleetAssignmentFields = (prefix: string) => (
    <div className="rounded-md border bg-muted/20 p-3 space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <Label className="text-sm font-medium">Source and Mobile Assignment</Label>
          <p className="text-xs text-muted-foreground">
            Categorize sheets, assign them to asset types or individual assets, and enable QR/mobile execution where applicable.
          </p>
        </div>
        {formData.templateScope === 'Fleet' ? (
          <Badge className="bg-blue-100 text-blue-800">Fleet checklist source</Badge>
        ) : (
          <Badge variant="outline">General template</Badge>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        <div className="space-y-2">
          <Label htmlFor={`${prefix}-sheet-type`}>Sheet Type</Label>
          <Select value={formData.sheetType || 'InspectionSheet'} onValueChange={(value) => setFormData({ ...formData, sheetType: value })}>
            <SelectTrigger id={`${prefix}-sheet-type`}><SelectValue placeholder="Select sheet type" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="InspectionSheet">Inspection sheet</SelectItem>
              <SelectItem value="ServiceSheet">Service sheet</SelectItem>
              <SelectItem value="WeeklyChecklist">Weekly checklist</SelectItem>
              <SelectItem value="PreventiveMaintenanceForm">Preventive maintenance form</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor={`${prefix}-scope`}>Template Scope</Label>
          <Select value={formData.templateScope || 'General'} onValueChange={updateTemplateScope}>
            <SelectTrigger id={`${prefix}-scope`} className={formFieldErrors.templateScope ? 'border-red-500' : undefined}>
              <SelectValue placeholder="Select scope" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="General">General</SelectItem>
              <SelectItem value="Fleet">Fleet</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor={`${prefix}-inspection-kind`}>Fleet Inspection</Label>
          <Select
            value={formData.fleetInspectionKind || 'Any'}
            onValueChange={(value) => setFormData({ ...formData, fleetInspectionKind: value })}
            disabled={formData.templateScope !== 'Fleet'}
          >
            <SelectTrigger id={`${prefix}-inspection-kind`}>
              <SelectValue placeholder="Select inspection type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Any">Any trip inspection</SelectItem>
              <SelectItem value="PreTrip">Pre-trip only</SelectItem>
              <SelectItem value="PostTrip">Post-trip only</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor={`${prefix}-qr-version`}>QR Payload Version</Label>
          <Input
            id={`${prefix}-qr-version`}
            type="number"
            min={1}
            max={99}
            value={formData.qrPayloadVersion || 1}
            onChange={(e) => setFormData({ ...formData, qrPayloadVersion: parseInt(e.target.value) || 1 })}
            disabled={formData.templateScope !== 'Fleet'}
            className={formFieldErrors.qrPayloadVersion ? 'border-red-500' : undefined}
          />
          {formFieldErrors.qrPayloadVersion && (
            <p className="text-sm text-red-600">{formFieldErrors.qrPayloadVersion}</p>
          )}
        </div>
      </div>

      {(
        <>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor={`${prefix}-asset-category`}>Asset Category Rule</Label>
              <Select
                value={formData.assignedAssetCategoryId || 'none'}
                onValueChange={(value) => setFormData({
                  ...formData,
                  assignedAssetCategoryId: value === 'none' ? null : value,
                  assignedAssetId: null,
                })}
              >
                <SelectTrigger id={`${prefix}-asset-category`}>
                  <SelectValue placeholder="Any fleet asset category" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Any asset category</SelectItem>
                  {assetCategories.map((category) => (
                    <SelectItem key={category.id} value={category.id}>
                      {category.name}{category.code ? ` (${category.code})` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor={`${prefix}-asset`}>Specific Asset Rule</Label>
              <Select
                value={formData.assignedAssetId || 'none'}
                onValueChange={(value) => setFormData({ ...formData, assignedAssetId: value === 'none' ? null : value })}
              >
                <SelectTrigger id={`${prefix}-asset`}>
                  <SelectValue placeholder="Any fleet asset" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Any asset</SelectItem>
                  {scopedFleetAssets.map((asset) => (
                    <SelectItem key={asset.id} value={asset.id}>
                      {asset.assetName}{asset.assetCode ? ` (${asset.assetCode})` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Specific asset rules take priority over category rules during inspection, service, trip, and mobile lookup.
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center space-x-2">
              <Switch
                id={`${prefix}-qr-enabled`}
                checked={!!formData.isQrEnabled}
                onCheckedChange={(checked) => setFormData({ ...formData, isQrEnabled: checked })}
              />
              <Label htmlFor={`${prefix}-qr-enabled`}>QR enabled</Label>
            </div>
            <div className="flex items-center space-x-2">
              <Switch
                id={`${prefix}-mobile-offline`}
                checked={!!formData.mobileOfflineEnabled}
                onCheckedChange={(checked) => setFormData({ ...formData, mobileOfflineEnabled: checked })}
              />
              <Label htmlFor={`${prefix}-mobile-offline`}>Offline mobile enabled</Label>
            </div>
          </div>

          <div className="space-y-3 border-t pt-3">
            <div className="flex items-center space-x-2">
              <Switch
                id={`${prefix}-auto-work-order`}
                checked={formData.autoCreateWorkOrderOnFailure !== false}
                onCheckedChange={(checked) => setFormData({ ...formData, autoCreateWorkOrderOnFailure: checked })}
              />
              <Label htmlFor={`${prefix}-auto-work-order`}>Automatically create a work order for failed or flagged checks</Label>
            </div>

            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Default Work Order Type</Label>
                <Select
                  value={formData.failureWorkOrderTypeId || 'auto'}
                  onValueChange={(value) => setFormData({ ...formData, failureWorkOrderTypeId: value === 'auto' ? null : value })}
                  disabled={formData.autoCreateWorkOrderOnFailure === false}
                >
                  <SelectTrigger><SelectValue placeholder="Use active fallback" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="auto">Use active fallback</SelectItem>
                    {workOrderTypes.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Default Maintenance Type</Label>
                <Select
                  value={formData.failureMaintenanceTypeId || 'auto'}
                  onValueChange={(value) => setFormData({ ...formData, failureMaintenanceTypeId: value === 'auto' ? null : value })}
                  disabled={formData.autoCreateWorkOrderOnFailure === false}
                >
                  <SelectTrigger><SelectValue placeholder="Use active fallback" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="auto">Use active fallback</SelectItem>
                    {maintenanceTypes.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Default Priority</Label>
                <Select
                  value={formData.failurePriorityLevelId || 'auto'}
                  onValueChange={(value) => setFormData({ ...formData, failurePriorityLevelId: value === 'auto' ? null : value })}
                  disabled={formData.autoCreateWorkOrderOnFailure === false}
                >
                  <SelectTrigger><SelectValue placeholder="Use result severity" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="auto">Use result severity</SelectItem>
                    {priorityLevels.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Default Billing Type</Label>
                <Select
                  value={formData.failureBillingType || 'Default'}
                  onValueChange={(value) => setFormData({ ...formData, failureBillingType: value })}
                  disabled={formData.autoCreateWorkOrderOnFailure === false}
                >
                  <SelectTrigger><SelectValue placeholder="Select billing type" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Default">Use Fleet setting</SelectItem>
                    <SelectItem value="Repairs">Repairs</SelectItem>
                    <SelectItem value="Maintenance">Maintenance</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Inspection Templates</h1>
          <p className="text-muted-foreground">
            Manage standardized inspection templates and checklists
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Template
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[700px]">
            <DialogHeader>
              <DialogTitle>Add Inspection Template</DialogTitle>
              <DialogDescription>
                Create a new inspection template with checklist items.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Template Name</Label>
                  <Input
                    id="name"
                    value={formData.name}
                    onChange={(e) => setFormData({...formData, name: e.target.value})}
                    placeholder="Enter template name"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="code">Template Code</Label>
                  <Input
                    id="code"
                    value={formData.code}
                    onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                    placeholder="e.g., SAFE-001"
                  />
                </div>
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  value={formData.description}
                  onChange={(e) => setFormData({...formData, description: e.target.value})}
                  placeholder="Describe this inspection template..."
                  rows={3}
                />
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="category">Category</Label>
                  <Select value={formData.category} onValueChange={(value) => setFormData({...formData, category: value})}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select category" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Safety">Safety</SelectItem>
                      <SelectItem value="Fleet">Fleet</SelectItem>
                      <SelectItem value="HVAC">HVAC</SelectItem>
                      <SelectItem value="Electrical">Electrical</SelectItem>
                      <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                      <SelectItem value="Operations">Operations</SelectItem>
                      <SelectItem value="Quality">Quality</SelectItem>
                      <SelectItem value="Environmental">Environmental</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="frequency">Frequency</Label>
                  <Select value={formData.frequency} onValueChange={(value) => setFormData({...formData, frequency: value})}>
                    <SelectTrigger className={formFieldErrors.frequency ? 'border-red-500' : undefined}>
                      <SelectValue placeholder="Select frequency" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Daily">Daily</SelectItem>
                      <SelectItem value="Weekly">Weekly</SelectItem>
                      <SelectItem value="Monthly">Monthly</SelectItem>
                      <SelectItem value="Quarterly">Quarterly</SelectItem>
                      <SelectItem value="Semi-Annual">Semi-Annual</SelectItem>
                      <SelectItem value="Annual">Annual</SelectItem>
                    </SelectContent>
                  </Select>
                  {formFieldErrors.frequency && (
                    <p className="text-sm text-red-600">{formFieldErrors.frequency}</p>
                  )}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="duration">Estimated Duration (minutes)</Label>
                  <Input
                    id="duration"
                    type="number"
                    value={formData.estimatedDuration}
                    onChange={(e) => setFormData({...formData, estimatedDuration: parseInt(e.target.value) || 0})}
                    placeholder="60"
                    className={formFieldErrors.estimatedDuration ? 'border-red-500' : undefined}
                  />
                  {formFieldErrors.estimatedDuration && (
                    <p className="text-sm text-red-600">{formFieldErrors.estimatedDuration}</p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="priority">Priority</Label>
                  <Select value={formData.priority} onValueChange={(value) => setFormData({...formData, priority: value})}>
                    <SelectTrigger className={formFieldErrors.priority ? 'border-red-500' : undefined}>
                      <SelectValue placeholder="Select priority" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Critical">Critical</SelectItem>
                      <SelectItem value="High">High</SelectItem>
                      <SelectItem value="Medium">Medium</SelectItem>
                      <SelectItem value="Low">Low</SelectItem>
                    </SelectContent>
                  </Select>
                  {formFieldErrors.priority && (
                    <p className="text-sm text-red-600">{formFieldErrors.priority}</p>
                  )}
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="version">Version</Label>
                <Input
                  id="version"
                  value={formData.version}
                  onChange={(e) => setFormData({...formData, version: e.target.value})}
                  placeholder="1.0"
                />
              </div>

              {renderFleetAssignmentFields('create')}
              
              <div className="flex items-center space-x-4">
                <div className="flex items-center space-x-2">
                  <Switch
                    id="signature"
                    checked={formData.requiresSignature}
                    onCheckedChange={(checked) => setFormData({...formData, requiresSignature: checked})}
                  />
                  <Label htmlFor="signature">Requires Signature</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="photos"
                    checked={formData.allowPhotos}
                    onCheckedChange={(checked) => setFormData({...formData, allowPhotos: checked})}
                  />
                  <Label htmlFor="photos">Allow checklist item photos</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <Switch
                    id="active"
                    checked={formData.isActive}
                    onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                  />
                  <Label htmlFor="active">Active</Label>
                </div>
              </div>

              <div className="space-y-2 pt-2">
                <div className="flex items-center justify-between gap-3">
                  <Label>Checklist Items ({formData.checklistItems.length})</Label>
                  <Button type="button" size="sm" variant="outline" onClick={addChecklistItem}>
                    <Plus className="mr-2 h-4 w-4" />
                    Add Item
                  </Button>
                </div>

                {formData.checklistItems.length === 0 ? (
                  <div className="rounded-md border p-3 text-sm text-muted-foreground">
                    No checklist items yet. Add at least one for templates used in fleet pre/post trip inspections.
                  </div>
                ) : (
                  <div className="space-y-2">
                    {formData.checklistItems.map((ci, idx) => (
                      <div key={`${idx}-${ci.order}`} className="rounded-md border p-3">
                        <div className="flex items-start gap-3">
                          <div className="pt-2 text-sm font-medium text-muted-foreground w-8">{idx + 1}.</div>
                          <div className="grid flex-1 grid-cols-1 gap-3 md:grid-cols-12">
                            <div className="md:col-span-7 space-y-1">
                              <Label className="text-xs text-muted-foreground">Item</Label>
                              <Input
                                value={ci.item}
                                onChange={(e) => updateChecklistItem(idx, { item: e.target.value })}
                                placeholder="e.g. Check tyres condition"
                              />
                            </div>
                            <div className="md:col-span-3 space-y-1">
                              <Label className="text-xs text-muted-foreground">Type</Label>
                              <Select value={ci.type} onValueChange={(v) => updateChecklistItem(idx, { type: v as any })}>
                                <SelectTrigger>
                                  <SelectValue placeholder="Type" />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="checklist">Pass / Fail</SelectItem>
                                  <SelectItem value="yesno">Yes / No</SelectItem>
                                  <SelectItem value="text">Text</SelectItem>
                                  <SelectItem value="number">Number</SelectItem>
                                  <SelectItem value="measurement">Measurement</SelectItem>
                                </SelectContent>
                              </Select>
                            </div>
                            <div className="md:col-span-2 space-y-1">
                              <Label className="text-xs text-muted-foreground">Required</Label>
                              <div className="flex h-10 items-center gap-2">
                                <Switch checked={!!ci.required} onCheckedChange={(checked) => updateChecklistItem(idx, { required: checked })} />
                                <span className="text-xs text-muted-foreground">{ci.required ? 'Yes' : 'No'}</span>
                              </div>
                            </div>
                          </div>
                          <div className="flex flex-col gap-1">
                            <Button type="button" size="icon" variant="ghost" onClick={() => moveChecklistItem(idx, 'up')} disabled={idx === 0}>
                              <ChevronUp className="h-4 w-4" />
                            </Button>
                            <Button type="button" size="icon" variant="ghost" onClick={() => moveChecklistItem(idx, 'down')} disabled={idx === formData.checklistItems.length - 1}>
                              <ChevronDown className="h-4 w-4" />
                            </Button>
                            <Button type="button" size="icon" variant="ghost" onClick={() => removeChecklistItem(idx)}>
                              <X className="h-4 w-4" />
                            </Button>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)} disabled={isSubmitting}>
                Cancel
              </Button>
              <Button onClick={handleCreate} disabled={!formData.name || !formData.code || isSubmitting}>
                {isSubmitting ? 'Creating...' : 'Add Template'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration">Administration</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/administration/maintenance">Maintenance Setup</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Inspection Templates</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Error Message */}
      {error && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="pt-6">
            <div className="flex items-center text-red-800">
              <AlertTriangle className="h-4 w-4 mr-2" />
              {error}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{filteredData.length}</p>
                <p className="text-sm text-muted-foreground">Total Templates</p>
              </div>
              <FileText className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(t => t.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <CheckSquare className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {filteredData.filter(t => t.requiresSignature).length}
                </p>
                <p className="text-sm text-muted-foreground">Require Signature</p>
              </div>
              <Users className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {Math.round(filteredData.reduce((sum, t) => sum + t.estimatedDuration, 0) / filteredData.length) || 0}
                </p>
                <p className="text-sm text-muted-foreground">Avg Duration (min)</p>
              </div>
              <Clock className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-6 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search templates..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="inactive">Inactive</SelectItem>
              </SelectContent>
            </Select>

            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                <SelectItem value="Safety">Safety</SelectItem>
                <SelectItem value="Fleet">Fleet</SelectItem>
                <SelectItem value="HVAC">HVAC</SelectItem>
                <SelectItem value="Electrical">Electrical</SelectItem>
                <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                <SelectItem value="Operations">Operations</SelectItem>
                <SelectItem value="Quality">Quality</SelectItem>
                <SelectItem value="Environmental">Environmental</SelectItem>
              </SelectContent>
            </Select>

            <Select value={sheetTypeFilter} onValueChange={setSheetTypeFilter}>
              <SelectTrigger><SelectValue placeholder="Sheet type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Sheet Types</SelectItem>
                <SelectItem value="InspectionSheet">Inspection sheets</SelectItem>
                <SelectItem value="ServiceSheet">Service sheets</SelectItem>
                <SelectItem value="WeeklyChecklist">Weekly checklists</SelectItem>
                <SelectItem value="PreventiveMaintenanceForm">Preventive maintenance forms</SelectItem>
              </SelectContent>
            </Select>

            <Select value={scopeFilter} onValueChange={setScopeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Scope" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Scopes</SelectItem>
                <SelectItem value="General">General</SelectItem>
                <SelectItem value="Fleet">Fleet</SelectItem>
              </SelectContent>
            </Select>

            <Select value={frequencyFilter} onValueChange={setFrequencyFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Frequency" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Frequencies</SelectItem>
                <SelectItem value="Daily">Daily</SelectItem>
                <SelectItem value="Weekly">Weekly</SelectItem>
                <SelectItem value="Monthly">Monthly</SelectItem>
                <SelectItem value="Quarterly">Quarterly</SelectItem>
                <SelectItem value="Annual">Annual</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Templates List */}
      <Card>
        <CardHeader>
          <CardTitle>Inspection Templates</CardTitle>
          <CardDescription>
            {filteredData.length} template{filteredData.length === 1 ? '' : 's'} found
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-4">Loading inspection templates...</div>
          ) : error ? (
            <div className="text-center py-4 text-red-600">{error}</div>
          ) : filteredData.length === 0 ? (
            <div className="text-center py-4 text-muted-foreground">No inspection templates found</div>
          ) : (
          <div className="space-y-4">
            {filteredData.map((template) => (
              <div key={template.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="space-y-3 flex-1">
                    <div className="flex items-center space-x-3">
                      <ClipboardList className="h-5 w-5 text-blue-500" />
                      <h3 className="font-semibold">{template.name}</h3>
                      <Badge variant="outline">{template.code}</Badge>
                      <Badge className={getPriorityColor(template.priority)}>
                        {template.priority}
                      </Badge>
                      <Badge className={template.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                        {template.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      <Badge variant="outline">{(template.sheetType || 'InspectionSheet').replace(/([a-z])([A-Z])/g, '$1 $2')}</Badge>
                      {template.templateScope === 'Fleet' && (
                        <Badge className="bg-blue-100 text-blue-800">
                          Fleet {template.fleetInspectionKind && template.fleetInspectionKind !== 'Any' ? `- ${template.fleetInspectionKind}` : ''}
                        </Badge>
                      )}
                      {template.isQrEnabled && (
                        <Badge className="bg-cyan-100 text-cyan-800">QR</Badge>
                      )}
                      {template.mobileOfflineEnabled && (
                        <Badge className="bg-emerald-100 text-emerald-800">Offline mobile</Badge>
                      )}
                      {template.requiresSignature && (
                        <Badge className="bg-blue-100 text-blue-800">Signature Required</Badge>
                      )}
                      {template.allowPhotos && (
                        <Badge className="bg-purple-100 text-purple-800">Photos Allowed</Badge>
                      )}
                    </div>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 text-sm text-muted-foreground">
                      <div>
                        <span className="font-medium">Category:</span> {template.category}
                      </div>
                      <div>
                        <span className="font-medium">Sheet type:</span> {(template.sheetType || 'InspectionSheet').replace(/([a-z])([A-Z])/g, '$1 $2')}
                      </div>
                      <div>
                        <span className="font-medium">Frequency:</span> {template.frequency}
                      </div>
                      <div>
                        <span className="font-medium">Duration:</span> {formatDuration(template.estimatedDuration)}
                      </div>
                      <div>
                        <span className="font-medium">Items:</span> {template.checklistItems.length}
                      </div>
                      <div>
                        <span className="font-medium">Version:</span> {template.version}
                      </div>
                      <div>
                        <span className="font-medium">Source:</span> {template.templateScope || 'General'}
                      </div>
                      <div>
                        <span className="font-medium">Asset rule:</span> {template.assignedAssetId ? getAssetName(template.assignedAssetId) : getAssetCategoryName(template.assignedAssetCategoryId)}
                      </div>
                      <div>
                        <span className="font-medium">Updated:</span> {template.lastUpdated}
                      </div>
                      <div>
                        <span className="font-medium">Asset Types:</span> {template.assetTypes.join(', ')}
                      </div>
                      <div>
                        <span className="font-medium">Inspectors:</span> {template.inspectorRoles.join(', ')}
                      </div>
                    </div>
                    
                    <p className="text-sm text-muted-foreground">{template.description}</p>
                  </div>
                  
                  <div className="flex items-center space-x-2">
                    <Button size="sm" variant="outline" onClick={() => handleView(template)}>
                      <Eye className="h-4 w-4" />
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => handleEdit(template)}>
                      <Edit className="h-4 w-4" />
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => handleDuplicate(template)}>
                      <Copy className="h-4 w-4" />
                    </Button>
                    {template.isQrEnabled && (
                      <Button size="sm" variant="outline" onClick={() => handleQrLabel(template)}>
                        <QrCode className="h-4 w-4" />
                      </Button>
                    )}
                    <Button 
                      size="sm" 
                      variant="outline" 
                      onClick={() => handleDelete(template)}
                      className="text-red-600 hover:text-red-700"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                    <Button variant="outline" size="sm">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
          )}
        </CardContent>
      </Card>

      {/* View Template Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="sm:max-w-[800px]">
          <DialogHeader>
            <DialogTitle>View Template: {selectedTemplate?.name}</DialogTitle>
            <DialogDescription>
              Template details and checklist items
            </DialogDescription>
          </DialogHeader>
          {selectedTemplate && (
            <div className="space-y-4 max-h-[70vh] overflow-y-auto">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Code</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.code}</p>
                </div>
                <div>
                  <Label>Category</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.category}</p>
                </div>
                <div>
                  <Label>Frequency</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.frequency}</p>
                </div>
                <div>
                  <Label>Duration</Label>
                  <p className="text-sm text-muted-foreground">{formatDuration(selectedTemplate.estimatedDuration)}</p>
                </div>
                <div>
                  <Label>Template Scope</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.templateScope || 'General'}</p>
                </div>
                <div>
                  <Label>Fleet Inspection</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.fleetInspectionKind || 'Any'}</p>
                </div>
                <div>
                  <Label>Asset Category Rule</Label>
                  <p className="text-sm text-muted-foreground">{getAssetCategoryName(selectedTemplate.assignedAssetCategoryId)}</p>
                </div>
                <div>
                  <Label>Specific Asset Rule</Label>
                  <p className="text-sm text-muted-foreground">{getAssetName(selectedTemplate.assignedAssetId)}</p>
                </div>
                <div>
                  <Label>QR / Mobile</Label>
                  <p className="text-sm text-muted-foreground">
                    {selectedTemplate.isQrEnabled ? 'QR enabled' : 'QR disabled'} - {selectedTemplate.mobileOfflineEnabled ? 'Offline enabled' : 'Offline disabled'}
                  </p>
                </div>
                <div>
                  <Label>QR Payload Version</Label>
                  <p className="text-sm text-muted-foreground">{selectedTemplate.qrPayloadVersion || 1}</p>
                </div>
              </div>
              
              <div>
                <Label>Description</Label>
                <p className="text-sm text-muted-foreground">{selectedTemplate.description}</p>
              </div>

              <div>
                <Label>Checklist Items ({selectedTemplate.checklistItems.length})</Label>
                <div className="space-y-2 mt-2">
                  {selectedTemplate.checklistItems.map((item, index) => (
                    <div key={item.id} className="flex items-center space-x-2 p-2 border rounded">
                      <span className="text-sm font-medium">{index + 1}.</span>
                      <span className="text-sm flex-1">{item.item}</span>
                      <Badge variant="outline" className="text-xs">
                        {getChecklistTypeLabel(item.type)}
                      </Badge>
                      {item.required && (
                        <Badge className="bg-red-100 text-red-800 text-xs">Required</Badge>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* QR Label Dialog */}
      <Dialog open={isQrDialogOpen} onOpenChange={setIsQrDialogOpen}>
        <DialogContent className="sm:max-w-[760px]">
          <DialogHeader>
            <DialogTitle>Inspection and Service Sheet QR Label</DialogTitle>
            <DialogDescription>
              Generate a mobile checklist QR package for {qrTemplate?.name || 'this template'}.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-[1fr_260px]">
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                <div className="space-y-2">
                  <Label>Transaction</Label>
                  <Select
                    value={qrForm.inspectionKind}
                    onValueChange={(value) => {
                      const next = { ...qrForm, inspectionKind: value };
                      setQrForm(next);
                      setQrPackage(null);
                    }}
                    disabled={qrTemplate?.templateScope !== 'Fleet' || (!!qrTemplate?.fleetInspectionKind && qrTemplate.fleetInspectionKind !== 'Any')}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Inspection type" />
                    </SelectTrigger>
                    <SelectContent>
                      {qrTemplate?.templateScope === 'Fleet' ? (
                        <>
                          <SelectItem value="PreTrip">Pre-trip</SelectItem>
                          <SelectItem value="PostTrip">Post-trip</SelectItem>
                        </>
                      ) : (
                        <SelectItem value={qrForm.inspectionKind}>{qrForm.inspectionKind.replace(/([a-z])([A-Z])/g, '$1 $2')}</SelectItem>
                      )}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label>Asset Category</Label>
                  <Select
                    value={qrForm.assetCategoryId}
                    onValueChange={(value) => {
                      const next = { ...qrForm, assetCategoryId: value, assetId: 'none' };
                      setQrForm(next);
                      setQrPackage(null);
                    }}
                    disabled={!!qrTemplate?.assignedAssetCategoryId}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Any category" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Any fleet category</SelectItem>
                      {assetCategories.map((category) => (
                        <SelectItem key={category.id} value={category.id}>
                          {category.name}{category.code ? ` (${category.code})` : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label>Asset</Label>
                  <Select
                    value={qrForm.assetId}
                    onValueChange={(value) => {
                      const next = { ...qrForm, assetId: value };
                      setQrForm(next);
                      setQrPackage(null);
                    }}
                    disabled={!!qrTemplate?.assignedAssetId}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Any asset" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Any fleet asset</SelectItem>
                      {qrScopedFleetAssets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.assetName}{asset.assetCode ? ` (${asset.assetCode})` : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  onClick={() => qrTemplate && generateQrPackage(qrTemplate)}
                  disabled={!qrTemplate || qrLoading}
                >
                  <QrCode className="mr-2 h-4 w-4" />
                  {qrLoading ? 'Generating...' : 'Generate QR Package'}
                </Button>
                <Button type="button" variant="outline" onClick={handlePrintQrLabel} disabled={!qrPackage}>
                  <Printer className="mr-2 h-4 w-4" />
                  Print Label
                </Button>
              </div>

              {qrError && (
                <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
                  {qrError}
                </div>
              )}

              {qrPackage && (
                <div className="space-y-3 text-sm">
                  <div className="grid grid-cols-1 gap-2 md:grid-cols-2">
                    <div>
                      <span className="font-medium">Package:</span> {qrPackage.packageId}
                    </div>
                    <div>
                      <span className="font-medium">Mode:</span> {qrPackage.qrPayloadMode}
                    </div>
                    <div>
                      <span className="font-medium">Asset:</span> {qrPackage.assetName || 'Any fleet asset'}
                    </div>
                    <div>
                      <span className="font-medium">Category:</span> {qrPackage.assetCategoryName || 'Any fleet category'}
                    </div>
                    <div>
                      <span className="font-medium">Checklist items:</span> {qrPackage.checklistItems.length}
                    </div>
                  </div>

                  <div className="rounded-md border border-blue-200 bg-blue-50 p-3 text-blue-900">
                    The label contains a compact signed reference. Mobile devices download Fleet checklists during sync and use the local copy when offline.
                  </div>

                  {!qrPackage.isQrEnabled && (
                    <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-amber-900">
                      QR is not enabled on this template. Enable it before printing production labels.
                    </div>
                  )}

                  <div>
                    <Label>Mobile URL</Label>
                    <Input readOnly value={qrPackage.mobileUrl} className="mt-1 text-xs" />
                  </div>
                </div>
              )}
            </div>

            <div className="rounded-md border bg-white p-4 text-center">
              {qrPackage ? (
                <div ref={qrLabelRef} className="space-y-3">
                  <div className="label-kicker text-xs font-semibold uppercase text-muted-foreground">Fleet Inspection</div>
                  <QRCodeSVG value={qrPackage.mobileUrl} size={210} level="M" includeMargin className="mx-auto" />
                  <div>
                    <div className="label-title font-semibold">
                      {qrPackage.assetName || qrPackage.assetCategoryName || 'Fleet asset'}
                    </div>
                    {qrPackage.assetNumber && (
                      <div className="label-description text-xs text-muted-foreground">Asset No: {qrPackage.assetNumber}</div>
                    )}
                    {qrPackage.assetCategoryName && (
                      <div className="label-description text-xs text-muted-foreground">Asset Type: {qrPackage.assetCategoryName}</div>
                    )}
                    <div className="label-description text-xs text-muted-foreground">Checklist: {qrPackage.templateName}</div>
                    <div className="label-description text-xs text-muted-foreground">{qrPackage.templateCode} v{qrPackage.templateVersion}</div>
                  </div>
                  <div className="label-description text-xs text-muted-foreground">
                    Inspection: {qrPackage.inspectionKind}
                  </div>
                </div>
              ) : (
                <div className="flex h-full min-h-[260px] items-center justify-center text-sm text-muted-foreground">
                  Generate a package to preview the QR label.
                </div>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsQrDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="flex max-h-[92vh] w-[96vw] max-w-6xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>Edit Template</DialogTitle>
            <DialogDescription>
              Update the inspection template information.
            </DialogDescription>
          </DialogHeader>
          <div className="grid flex-1 gap-5 overflow-y-auto py-4 pr-1">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-name">Template Name</Label>
                <Input
                  id="edit-name"
                  value={formData.name}
                  onChange={(e) => setFormData({...formData, name: e.target.value})}
                  placeholder="Enter template name"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-code">Template Code</Label>
                <Input
                  id="edit-code"
                  value={formData.code}
                  onChange={(e) => setFormData({...formData, code: e.target.value.toUpperCase()})}
                  placeholder="e.g., SAFE-001"
                />
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                value={formData.description}
                onChange={(e) => setFormData({...formData, description: e.target.value})}
                placeholder="Describe this inspection template..."
                rows={3}
              />
            </div>
            
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-category">Category</Label>
                <Select value={formData.category} onValueChange={(value) => setFormData({...formData, category: value})}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select category" />
                  </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Safety">Safety</SelectItem>
                      <SelectItem value="Fleet">Fleet</SelectItem>
                      <SelectItem value="HVAC">HVAC</SelectItem>
                      <SelectItem value="Electrical">Electrical</SelectItem>
                      <SelectItem value="Fire Safety">Fire Safety</SelectItem>
                      <SelectItem value="Operations">Operations</SelectItem>
                      <SelectItem value="Quality">Quality</SelectItem>
                      <SelectItem value="Environmental">Environmental</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              <div className="space-y-2">
                <Label htmlFor="edit-frequency">Frequency</Label>
                <Select value={formData.frequency} onValueChange={(value) => setFormData({...formData, frequency: value})}>
                  <SelectTrigger className={formFieldErrors.frequency ? 'border-red-500' : undefined}>
                    <SelectValue placeholder="Select frequency" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Daily">Daily</SelectItem>
                    <SelectItem value="Weekly">Weekly</SelectItem>
                    <SelectItem value="Monthly">Monthly</SelectItem>
                    <SelectItem value="Quarterly">Quarterly</SelectItem>
                    <SelectItem value="Semi-Annual">Semi-Annual</SelectItem>
                    <SelectItem value="Annual">Annual</SelectItem>
                  </SelectContent>
                </Select>
                {formFieldErrors.frequency && (
                  <p className="text-sm text-red-600">{formFieldErrors.frequency}</p>
                )}
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-duration">Estimated Duration (minutes)</Label>
                <Input
                  id="edit-duration"
                  type="number"
                  value={formData.estimatedDuration}
                  onChange={(e) => setFormData({...formData, estimatedDuration: parseInt(e.target.value) || 0})}
                  placeholder="60"
                  className={formFieldErrors.estimatedDuration ? 'border-red-500' : undefined}
                />
                {formFieldErrors.estimatedDuration && (
                  <p className="text-sm text-red-600">{formFieldErrors.estimatedDuration}</p>
                )}
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-priority">Priority</Label>
                <Select value={formData.priority} onValueChange={(value) => setFormData({...formData, priority: value})}>
                  <SelectTrigger className={formFieldErrors.priority ? 'border-red-500' : undefined}>
                    <SelectValue placeholder="Select priority" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Critical">Critical</SelectItem>
                    <SelectItem value="High">High</SelectItem>
                    <SelectItem value="Medium">Medium</SelectItem>
                    <SelectItem value="Low">Low</SelectItem>
                  </SelectContent>
                </Select>
                {formFieldErrors.priority && (
                  <p className="text-sm text-red-600">{formFieldErrors.priority}</p>
                )}
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-version">Version</Label>
              <Input
                id="edit-version"
                value={formData.version}
                onChange={(e) => setFormData({...formData, version: e.target.value})}
                placeholder="1.0"
              />
            </div>

            {renderFleetAssignmentFields('edit')}
            
            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-signature"
                  checked={formData.requiresSignature}
                  onCheckedChange={(checked) => setFormData({...formData, requiresSignature: checked})}
                />
                <Label htmlFor="edit-signature">Requires Signature</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-photos"
                  checked={formData.allowPhotos}
                  onCheckedChange={(checked) => setFormData({...formData, allowPhotos: checked})}
                />
                <Label htmlFor="edit-photos">Allow checklist item photos</Label>
              </div>
              <div className="flex items-center space-x-2">
                <Switch
                  id="edit-active"
                  checked={formData.isActive}
                  onCheckedChange={(checked) => setFormData({...formData, isActive: checked})}
                />
                <Label htmlFor="edit-active">Active</Label>
              </div>
            </div>

            <div className="space-y-2 pt-2">
              <div className="flex items-center justify-between gap-3">
                <Label>Checklist Items ({formData.checklistItems.length})</Label>
                <Button type="button" size="sm" variant="outline" onClick={addChecklistItem}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add Item
                </Button>
              </div>

              {formData.checklistItems.length === 0 ? (
                <div className="rounded-md border p-3 text-sm text-muted-foreground">
                  No checklist items yet. Add items to make this template usable for inspections.
                </div>
              ) : (
                <div className="space-y-2">
                  {formData.checklistItems.map((ci, idx) => (
                    <div key={`${idx}-${ci.order}`} className="rounded-md border p-3">
                      <div className="flex items-start gap-3">
                        <div className="pt-2 text-sm font-medium text-muted-foreground w-8">{idx + 1}.</div>
                        <div className="grid flex-1 grid-cols-1 gap-3 md:grid-cols-12">
                          <div className="md:col-span-7 space-y-1">
                            <Label className="text-xs text-muted-foreground">Item</Label>
                            <Input
                              value={ci.item}
                              onChange={(e) => updateChecklistItem(idx, { item: e.target.value })}
                              placeholder="e.g. Check tyres condition"
                            />
                          </div>
                          <div className="md:col-span-3 space-y-1">
                            <Label className="text-xs text-muted-foreground">Type</Label>
                            <Select value={ci.type} onValueChange={(v) => updateChecklistItem(idx, { type: v as any })}>
                              <SelectTrigger>
                                <SelectValue placeholder="Type" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="checklist">Pass / Fail</SelectItem>
                                <SelectItem value="yesno">Yes / No</SelectItem>
                                <SelectItem value="text">Text</SelectItem>
                                <SelectItem value="number">Number</SelectItem>
                                <SelectItem value="measurement">Measurement</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                          <div className="md:col-span-2 space-y-1">
                            <Label className="text-xs text-muted-foreground">Required</Label>
                            <div className="flex h-10 items-center gap-2">
                              <Switch checked={!!ci.required} onCheckedChange={(checked) => updateChecklistItem(idx, { required: checked })} />
                              <span className="text-xs text-muted-foreground">{ci.required ? 'Yes' : 'No'}</span>
                            </div>
                          </div>
                        </div>
                        <div className="flex flex-col gap-1">
                          <Button type="button" size="icon" variant="ghost" onClick={() => moveChecklistItem(idx, 'up')} disabled={idx === 0}>
                            <ChevronUp className="h-4 w-4" />
                          </Button>
                          <Button type="button" size="icon" variant="ghost" onClick={() => moveChecklistItem(idx, 'down')} disabled={idx === formData.checklistItems.length - 1}>
                            <ChevronDown className="h-4 w-4" />
                          </Button>
                          <Button type="button" size="icon" variant="ghost" onClick={() => removeChecklistItem(idx)}>
                            <X className="h-4 w-4" />
                          </Button>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button onClick={handleUpdate} disabled={!formData.name || !formData.code || isSubmitting}>
              {isSubmitting ? 'Updating...' : 'Update Template'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
