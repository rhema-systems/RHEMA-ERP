'use client';

import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Plus, Search, Eye, Calendar, AlertTriangle, CheckCircle2, Clock, FileText, ExternalLink, MapPin, MoreHorizontal } from 'lucide-react';
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
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import assetAdmissionService, {
  AssetAdmission,
  CreateAdmissionRequest,
  CreateDischargeRequest,
  AssetDischarge
} from '@/services/assetAdmissionService';
import maintenanceApiService, { Asset, JobCard as MaintenanceJobCard } from '@/services/maintenanceApiService';
import assetConditionService, {
  AssetConditionChecklistTemplateDto,
  AssetConditionRecordDto,
  CreateAssetConditionRecordDto,
  SubmitAssetConditionItemDto
} from '@/services/assetConditionService';
import { ClientOnly } from '@/components/ClientOnly';
import { Switch } from '@/components/ui/switch';
import { ClipboardCheck, CheckCircle, XCircle } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

export default function AssetAdmissionManagement() {
  const { toast } = useToast();
  const searchParams = useSearchParams();
  const [admissions, setAdmissions] = useState<AssetAdmission[]>([]);
  const [discharges, setDischarges] = useState<AssetDischarge[]>([]);
  const [filteredAdmissions, setFilteredAdmissions] = useState<AssetAdmission[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [viewMode, setViewMode] = useState<'admissions' | 'discharges'>('admissions');

  // Dialog states
  const [isAdmissionDialogOpen, setIsAdmissionDialogOpen] = useState(false);
  const [isDischargeDialogOpen, setIsDischargeDialogOpen] = useState(false);
  const [selectedAdmission, setSelectedAdmission] = useState<AssetAdmission | null>(null);
  const [selectedDischarge, setSelectedDischarge] = useState<AssetDischarge | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);

  // Condition Inspection states
  const [isConditionDialogOpen, setIsConditionDialogOpen] = useState(false);
  const [conditionTemplates, setConditionTemplates] = useState<AssetConditionChecklistTemplateDto[]>([]);
  const [selectedTemplate, setSelectedTemplate] = useState<AssetConditionChecklistTemplateDto | null>(null);
  const [currentInspection, setCurrentInspection] = useState<AssetConditionRecordDto | null>(null);
  const [inspectionType, setInspectionType] = useState<'Admission' | 'Discharge'>('Admission');
  const [itemResponses, setItemResponses] = useState<Record<string, SubmitAssetConditionItemDto>>({});
  const [existingAdmissionRecord, setExistingAdmissionRecord] = useState<AssetConditionRecordDto | null>(null);
  const [existingDischargeRecord, setExistingDischargeRecord] = useState<AssetConditionRecordDto | null>(null);
  const [isSubmittingInspection, setIsSubmittingInspection] = useState(false);

  // Data from services
  const [assets, setAssets] = useState<Asset[]>([]);
  const [workOrders, setWorkOrders] = useState<any[]>([]);
  const [jobCards, setJobCards] = useState<MaintenanceJobCard[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  const [stats, setStats] = useState({
    totalActive: 0,
    totalCompleted: 0,
    averageStayDays: 0
  });
  const [initialFilter, setInitialFilter] = useState<{ assetId?: string; workOrderId?: string; jobCardId?: string } | null>(null);

  // Form states
  const [newAdmission, setNewAdmission] = useState({
    assetId: '',
    jobCardId: '',
    workOrderId: '',
    admissionType: 'Scheduled' as const,
    assetConditionOnAdmission: 'Good' as const,
    admissionNotes: '',
    observedProblems: '',
    mileageReading: 0,
    hoursReading: 0,
    fuelLevel: 0,
    admissionLocation: '',
    bayOrStation: '',
    estimatedCompletionDate: '',
    estimatedDischargeDate: ''
  });

  const [newDischarge, setNewDischarge] = useState({
    admissionId: '',
    assetConditionOnDischarge: 'Good' as const,
    dischargeNotes: '',
    workCompleted: '',
    remainingIssues: '',
    mileageReading: 0,
    hoursReading: 0,
    fuelLevel: 0,
    qualityCheckPassed: false,
    qualityCheckNotes: '',
    customerAcceptance: false,
    acceptanceNotes: '',
    requiresFollowUp: false,
    followUpDate: '',
    followUpInstructions: '',
    warrantyDays: 0,
    warrantyTerms: ''
  });

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);
      try {
        const [assetsResponse, admissionsResponse, dischargesResponse, statsResponse, workOrdersResponse, jobCardsResponse] = await Promise.all([
          maintenanceApiService.getAssets(),
          assetAdmissionService.getAdmissions(initialFilter || {}),
          assetAdmissionService.getDischarges(),
          assetAdmissionService.getAdmissionStats(),
          maintenanceApiService.getWorkOrders().catch(() => ({ items: [] })),
          maintenanceApiService.getJobCards().catch(() => [])
        ]);

        setAssets(assetsResponse.items || []);
        setAdmissions(admissionsResponse.items || []);
        setDischarges(dischargesResponse.items || []);
        setFilteredAdmissions(admissionsResponse.items || []);
        setStats(statsResponse || { totalActive: 0, totalCompleted: 0, averageStayDays: 0 });
        setWorkOrders(workOrdersResponse.items || []);
        setJobCards(Array.isArray(jobCardsResponse) ? jobCardsResponse : []);
      } catch (error) {
        console.error('Error loading data:', error);
        setAssets([]);
        setAdmissions([]);
        setDischarges([]);
        setFilteredAdmissions([]);
        setWorkOrders([]);
        setJobCards([]);
      } finally {
        setLoadingData(false);
      }
    };

    loadData();
  }, [initialFilter]);

  // Apply initial query-parameter-based filters and form defaults
  useEffect(() => {
    const assetId = searchParams?.get('assetId') || undefined;
    const workOrderId = searchParams?.get('workOrderId') || undefined;
    const jobCardId = searchParams?.get('jobCardId') || undefined;
    const view = searchParams?.get('view');

    // Pre-select view mode if provided
    if (view === 'discharges') {
      setViewMode('discharges');
    }

    // Capture initial filter so that the first data load can use it
    if (assetId || workOrderId || jobCardId) {
      setInitialFilter({ assetId, workOrderId, jobCardId });
    }

    // If an assetId is supplied, default the form asset
    if (assetId) {
      setNewAdmission(prev => ({ ...prev, assetId }));
    }

    // If a workOrderId is supplied, default the form work order
    if (workOrderId) {
      setNewAdmission(prev => ({ ...prev, workOrderId }));
    }

    // If a jobCardId is supplied, default the form job card
    if (jobCardId) {
      setNewAdmission(prev => ({ ...prev, jobCardId }));
    }
  }, [searchParams]);

  const getJobCardReference = React.useCallback((jobCardId?: string) => {
    if (!jobCardId) return null;

    const jobCard = jobCards.find(card => card.id === jobCardId);
    const linkedWorkOrder = workOrders.find(order => order.jobCardId === jobCardId);

    return {
      id: jobCardId,
      number: jobCard?.jobCardNumber || linkedWorkOrder?.jobCardNumber || 'Open Job Card',
      title: jobCard?.title,
    };
  }, [jobCards, workOrders]);

  const openJobCard = (jobCardId?: string) => {
    if (!jobCardId) return;
    window.open(`/maintenance/job-cards?id=${jobCardId}`, '_blank', 'noopener,noreferrer');
  };

  const renderJobCardCell = (jobCardId?: string) => {
    const jobCard = getJobCardReference(jobCardId);

    if (!jobCard) {
      return <span className="text-muted-foreground text-sm">-</span>;
    }

    return (
      <button
        type="button"
        onClick={() => openJobCard(jobCard.id)}
        className="inline-flex items-center gap-1 hover:opacity-70 hover:scale-105 transition-all cursor-pointer"
        title={jobCard.title ? `Open ${jobCard.number} - ${jobCard.title}` : 'Open job card in new tab'}
      >
        <Badge variant="outline" className="text-xs hover:border-blue-400">
          {jobCard.number}
        </Badge>
        <ExternalLink className="h-3 w-3 text-muted-foreground" />
      </button>
    );
  };

  const handleViewAdmission = (admission: AssetAdmission) => {
    setSelectedDischarge(null);
    setSelectedAdmission(admission);
    setIsViewDialogOpen(true);
  };

  const handleViewDischarge = (discharge: AssetDischarge) => {
    setSelectedAdmission(null);
    setSelectedDischarge(discharge);
    setIsViewDialogOpen(true);
  };

  // Filter admissions based on search and filters
  useEffect(() => {
    let filtered = admissions;

    if (searchTerm) {
      const search = searchTerm.toLowerCase();
      filtered = filtered.filter(admission =>
        admission.assetName.toLowerCase().includes(search) ||
        admission.admissionNumber.toLowerCase().includes(search) ||
        admission.assetNumber.toLowerCase().includes(search) ||
        (getJobCardReference(admission.jobCardId)?.number || '').toLowerCase().includes(search)
      );
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter(admission => admission.status === statusFilter);
    }

    if (typeFilter && typeFilter !== 'all') {
      filtered = filtered.filter(admission => admission.admissionType === typeFilter);
    }

    setFilteredAdmissions(filtered);
  }, [admissions, searchTerm, statusFilter, typeFilter, getJobCardReference]);

  const handleCreateAdmission = async () => {
    try {
      const selectedAsset = assets.find(a => a.id === newAdmission.assetId);
      if (!selectedAsset) {
        console.error('Asset not found');
        return;
      }

      const createRequest: CreateAdmissionRequest = {
        assetId: newAdmission.assetId,
        jobCardId: newAdmission.jobCardId || undefined,
        workOrderId: newAdmission.workOrderId || undefined,
        admissionType: newAdmission.admissionType,
        assetConditionOnAdmission: newAdmission.assetConditionOnAdmission,
        admissionNotes: newAdmission.admissionNotes,
        observedProblems: newAdmission.observedProblems,
        mileageReading: newAdmission.mileageReading || undefined,
        hoursReading: newAdmission.hoursReading || undefined,
        fuelLevel: newAdmission.fuelLevel || undefined,
        admissionLocation: newAdmission.admissionLocation,
        bayOrStation: newAdmission.bayOrStation,
        estimatedCompletionDate: newAdmission.estimatedCompletionDate || undefined,
        estimatedDischargeDate: newAdmission.estimatedDischargeDate || undefined
      };

      await assetAdmissionService.createAdmission(createRequest);

      // Refresh admissions list
      await refreshData();

      setIsAdmissionDialogOpen(false);
      resetAdmissionForm();
    } catch (error) {
      console.error('Error creating admission:', error);
    }
  };

  const handleCreateDischarge = async () => {
    try {
      const createRequest: CreateDischargeRequest = {
        admissionId: newDischarge.admissionId,
        assetConditionOnDischarge: newDischarge.assetConditionOnDischarge,
        dischargeNotes: newDischarge.dischargeNotes,
        workCompleted: newDischarge.workCompleted,
        remainingIssues: newDischarge.remainingIssues,
        mileageReading: newDischarge.mileageReading || undefined,
        hoursReading: newDischarge.hoursReading || undefined,
        fuelLevel: newDischarge.fuelLevel || undefined,
        qualityCheckPassed: newDischarge.qualityCheckPassed,
        qualityCheckNotes: newDischarge.qualityCheckNotes,
        customerAcceptance: newDischarge.customerAcceptance,
        acceptanceNotes: newDischarge.acceptanceNotes,
        requiresFollowUp: newDischarge.requiresFollowUp,
        followUpDate: newDischarge.followUpDate || undefined,
        followUpInstructions: newDischarge.followUpInstructions,
        warrantyDays: newDischarge.warrantyDays || 0,
        warrantyTerms: newDischarge.warrantyTerms
      };

      await assetAdmissionService.createDischarge(createRequest);

      // Refresh data
      await refreshData();

      setIsDischargeDialogOpen(false);
      resetDischargeForm();
    } catch (error) {
      console.error('Error creating discharge:', error);
    }
  };

  const handleGenerateCertificate = async (discharge: AssetDischarge) => {
    try {
      await assetAdmissionService.generateCompletionCertificate(discharge.id);
      toast({
        title: 'Certificate generated',
        description: `Completion certificate generated for ${discharge.dischargeNumber}.`,
      });
      await refreshData();
    } catch (error: any) {
      console.error('Error generating certificate:', error);
      toast({
        title: 'Error',
        description: error?.message || 'Failed to generate completion certificate.',
        variant: 'destructive',
      });
    }
  };

  const refreshData = async () => {
    try {
      const [admissionsResponse, dischargesResponse, statsResponse, jobCardsResponse] = await Promise.all([
        assetAdmissionService.getAdmissions(),
        assetAdmissionService.getDischarges(),
        assetAdmissionService.getAdmissionStats(),
        maintenanceApiService.getJobCards().catch(() => [])
      ]);

      setAdmissions(admissionsResponse.items || []);
      setDischarges(dischargesResponse.items || []);
      setFilteredAdmissions(admissionsResponse.items || []);
      setStats(statsResponse || { totalActive: 0, totalCompleted: 0, averageStayDays: 0 });
      setJobCards(Array.isArray(jobCardsResponse) ? jobCardsResponse : []);
    } catch (error) {
      console.error('Error refreshing data:', error);
    }
  };

  const resetAdmissionForm = () => {
    setNewAdmission({
      assetId: '',
      jobCardId: '',
      workOrderId: '',
      admissionType: 'Scheduled',
      assetConditionOnAdmission: 'Good',
      admissionNotes: '',
      observedProblems: '',
      mileageReading: 0,
      hoursReading: 0,
      fuelLevel: 0,
      admissionLocation: '',
      bayOrStation: '',
      estimatedCompletionDate: '',
      estimatedDischargeDate: ''
    });
  };

  const resetDischargeForm = () => {
    setNewDischarge({
      admissionId: '',
      assetConditionOnDischarge: 'Good',
      dischargeNotes: '',
      workCompleted: '',
      remainingIssues: '',
      mileageReading: 0,
      hoursReading: 0,
      fuelLevel: 0,
      qualityCheckPassed: false,
      qualityCheckNotes: '',
      customerAcceptance: false,
      acceptanceNotes: '',
      requiresFollowUp: false,
      followUpDate: '',
      followUpInstructions: '',
      warrantyDays: 0,
      warrantyTerms: ''
    });
  };

  // Condition Inspection handlers
  const handleOpenConditionInspection = async (admission: AssetAdmission, type: 'Admission' | 'Discharge') => {
    setSelectedAdmission(admission);
    setInspectionType(type);
    setItemResponses({});
    setSelectedTemplate(null);
    setCurrentInspection(null);
    setExistingAdmissionRecord(null);
    setExistingDischargeRecord(null);

    try {
      // Check for existing records
      const [admissionRecord, dischargeRecord] = await Promise.all([
        assetConditionService.getAdmissionRecordForAdmission(admission.id),
        assetConditionService.getDischargeRecordForAdmission(admission.id)
      ]);
      setExistingAdmissionRecord(admissionRecord);
      setExistingDischargeRecord(dischargeRecord);

      // Get templates for the asset's category first
      const asset = assets.find(a => a.id === admission.assetId);
      let templates: AssetConditionChecklistTemplateDto[] = [];
      if (asset?.assetCategoryId) {
        templates = await assetConditionService.getTemplatesByAssetCategory(asset.assetCategoryId);
        setConditionTemplates(templates);
      } else {
        // Get all active templates as fallback
        templates = await assetConditionService.getAllTemplates(false);
        setConditionTemplates(templates);
      }

      // If showing an existing record, load it
      if (type === 'Admission' && admissionRecord) {
        setCurrentInspection(admissionRecord);
        // Find and set the template used in the record
        const template = templates.find(t => t.id === admissionRecord.templateId);
        if (template) {
          setSelectedTemplate(template);
        }
        // Populate responses
        const responses: Record<string, SubmitAssetConditionItemDto> = {};
        admissionRecord.itemResults.forEach(result => {
          responses[result.checklistItemId] = {
            checklistItemId: result.checklistItemId,
            isPresent: result.isPresent,
            textValue: result.textValue,
            numericValue: result.numericValue,
            selectedOption: result.selectedOption,
            comment: result.comment,
            repairReplacementAction: result.repairReplacementAction as 'None' | 'Repair' | 'Replace' | undefined,
            photoPaths: result.photoPaths
          };
        });
        setItemResponses(responses);
      } else if (type === 'Discharge' && dischargeRecord) {
        setCurrentInspection(dischargeRecord);
        // Find and set the template used in the record
        const template = templates.find(t => t.id === dischargeRecord.templateId);
        if (template) {
          setSelectedTemplate(template);
        }
        const responses: Record<string, SubmitAssetConditionItemDto> = {};
        dischargeRecord.itemResults.forEach(result => {
          responses[result.checklistItemId] = {
            checklistItemId: result.checklistItemId,
            isPresent: result.isPresent,
            textValue: result.textValue,
            numericValue: result.numericValue,
            selectedOption: result.selectedOption,
            comment: result.comment,
            repairReplacementAction: result.repairReplacementAction as 'None' | 'Repair' | 'Replace' | undefined,
            photoPaths: result.photoPaths
          };
        });
        setItemResponses(responses);
      } else {
        // No existing record - auto-select default template
        const defaultTemplate = templates.find(t => t.isDefault) || templates[0];
        if (defaultTemplate) {
          setSelectedTemplate(defaultTemplate);
        }
      }
    } catch (err) {
      console.error('Error loading condition inspection data:', err);
    }

    setIsConditionDialogOpen(true);
  };

  const handleStartInspection = async () => {
    if (!selectedAdmission || !selectedTemplate) return;

    try {
      setIsSubmittingInspection(true);
      const dto: CreateAssetConditionRecordDto = {
        assetId: selectedAdmission.assetId,
        templateId: selectedTemplate.id,
        inspectionType: inspectionType,
        admissionId: selectedAdmission.id,
        generalNotes: ''
      };

      const record = await assetConditionService.startConditionInspection(dto);
      setCurrentInspection(record);

      // Initialize responses
      const responses: Record<string, SubmitAssetConditionItemDto> = {};
      selectedTemplate.checklistItems.forEach(item => {
        responses[item.id] = {
          checklistItemId: item.id,
          isPresent: undefined,
          textValue: '',
          numericValue: undefined,
          selectedOption: '',
          comment: '',
          repairReplacementAction: item.allowRepairReplacement
            ? (item.defaultRepairReplacementAction || 'None')
            : undefined
        };
      });
      setItemResponses(responses);
    } catch (err) {
      console.error('Error starting inspection:', err);
    } finally {
      setIsSubmittingInspection(false);
    }
  };

  const handleSubmitItemResponse = async (itemId: string) => {
    if (!currentInspection) return;

    const response = itemResponses[itemId];
    if (!response) return;

    try {
      await assetConditionService.submitItemResult(currentInspection.id, response);
    } catch (err) {
      console.error('Error submitting item response:', err);
    }
  };

  const handleSaveDraft = async () => {
    if (!currentInspection) {
      setIsConditionDialogOpen(false);
      return;
    }

    try {
      setIsSubmittingInspection(true);

      // Submit all responses to save them as draft
      for (const itemId of Object.keys(itemResponses)) {
        const response = itemResponses[itemId];
        // Only submit if there's actual data
        if (response.isPresent !== undefined || response.textValue || response.numericValue !== undefined ||
            response.selectedOption || response.comment ||
            (response.repairReplacementAction && response.repairReplacementAction !== 'None')) {
          await assetConditionService.submitItemResult(currentInspection.id, response);
        }
      }

      toast({
        title: 'Draft Saved',
        description: 'Your inspection draft has been saved. You can resume it later.',
      });

      setIsConditionDialogOpen(false);
    } catch (err) {
      console.error('Error saving draft:', err);
      toast({
        title: 'Error',
        description: 'Failed to save draft. Please try again.',
        variant: 'destructive',
      });
    } finally {
      setIsSubmittingInspection(false);
    }
  };

  const handleCompleteInspection = async () => {
    if (!currentInspection) return;

    try {
      setIsSubmittingInspection(true);

      // Submit all responses
      for (const itemId of Object.keys(itemResponses)) {
        await assetConditionService.submitItemResult(currentInspection.id, itemResponses[itemId]);
      }

      // Complete the inspection
      await assetConditionService.completeInspection(currentInspection.id, { generalNotes: '' });

      setIsConditionDialogOpen(false);
      setCurrentInspection(null);
      setItemResponses({});
    } catch (err) {
      console.error('Error completing inspection:', err);
    } finally {
      setIsSubmittingInspection(false);
    }
  };

  const getStatusBadge = (status: AssetAdmission['status']) => {
    const colors = {
      'Active': 'bg-blue-100 text-blue-800',
      'Completed': 'bg-green-100 text-green-800',
      'Cancelled': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[status]}>
        {status}
      </Badge>
    );
  };

  const getConditionBadge = (condition: string) => {
    const colors = {
      'Excellent': 'bg-green-100 text-green-800',
      'Good': 'bg-blue-100 text-blue-800',
      'Fair': 'bg-yellow-100 text-yellow-800',
      'Poor': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    };

    return (
      <Badge className={colors[condition as keyof typeof colors] || 'bg-gray-100 text-gray-800'}>
        {condition}
      </Badge>
    );
  };

  const getTypeBadge = (type: string) => {
    const colors = {
      'Scheduled': 'bg-blue-100 text-blue-800',
      'Emergency': 'bg-red-100 text-red-800',
      'Breakdown': 'bg-orange-100 text-orange-800',
    };

    return (
      <Badge className={colors[type as keyof typeof colors] || 'bg-gray-100 text-gray-800'}>
        {type}
      </Badge>
    );
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Asset Admission & Discharge</h1>
          <p className="text-muted-foreground">
            Hospital-like asset management for maintenance operations
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
            <BreadcrumbPage>Asset Admission</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Statistics Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Admissions</CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.totalActive}</div>
            <p className="text-xs text-muted-foreground">
              Assets currently in maintenance
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Completed</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.totalCompleted}</div>
            <p className="text-xs text-muted-foreground">
              Assets discharged this month
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg. Stay</CardTitle>
            <Calendar className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{stats.averageStayDays}</div>
            <p className="text-xs text-muted-foreground">
              Days in maintenance bay
            </p>
          </CardContent>
        </Card>
      </div>

      {/* View Toggle and Actions */}
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <Button
            variant={viewMode === 'admissions' ? 'default' : 'outline'}
            onClick={() => setViewMode('admissions')}
          >
            Admissions
          </Button>
          <Button
            variant={viewMode === 'discharges' ? 'default' : 'outline'}
            onClick={() => setViewMode('discharges')}
          >
            Discharges
          </Button>
        </div>

        <div className="flex items-center space-x-2">
          <ClientOnly>
            <Dialog open={isAdmissionDialogOpen} onOpenChange={setIsAdmissionDialogOpen}>
              <DialogTrigger asChild>
                <Button>
                  <Plus className="mr-2 h-4 w-4" />
                  Admit Asset
                </Button>
              </DialogTrigger>
              <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
                <DialogHeader>
                  <DialogTitle>Admit Asset to Maintenance</DialogTitle>
                  <DialogDescription>
                    Record asset admission with condition assessment and maintenance details.
                  </DialogDescription>
                </DialogHeader>
                <div className="grid gap-4">
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="assetId">Asset</Label>
                      <Select value={newAdmission.assetId} onValueChange={(value) => setNewAdmission(prev => ({ ...prev, assetId: value }))}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select asset" />
                        </SelectTrigger>
                        <SelectContent>
                          {assets.map((asset) => (
                            <SelectItem key={asset.id} value={asset.id}>
                              {asset.name} ({asset.assetNumber})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="workOrderId">Work Order (Optional)</Label>
                      <Select value={newAdmission.workOrderId || "none"} onValueChange={(value) => setNewAdmission(prev => ({ ...prev, workOrderId: value === "none" ? "" : value }))}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select work order" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">None</SelectItem>
                          {workOrders.map((wo) => (
                            <SelectItem key={wo.id} value={wo.id}>
                              {wo.workOrderNumber} - {wo.title}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="admissionType">Admission Type</Label>
                      <Select value={newAdmission.admissionType} onValueChange={(value: any) => setNewAdmission(prev => ({ ...prev, admissionType: value }))}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select type" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Scheduled">Scheduled</SelectItem>
                          <SelectItem value="Emergency">Emergency</SelectItem>
                          <SelectItem value="Breakdown">Breakdown</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="assetCondition">Asset Condition</Label>
                      <Select value={newAdmission.assetConditionOnAdmission} onValueChange={(value: any) => setNewAdmission(prev => ({ ...prev, assetConditionOnAdmission: value }))}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select condition" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Excellent">Excellent</SelectItem>
                          <SelectItem value="Good">Good</SelectItem>
                          <SelectItem value="Fair">Fair</SelectItem>
                          <SelectItem value="Poor">Poor</SelectItem>
                          <SelectItem value="Critical">Critical</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="admissionNotes">Admission Notes</Label>
                    <Textarea
                      id="admissionNotes"
                      value={newAdmission.admissionNotes}
                      onChange={(e) => setNewAdmission(prev => ({ ...prev, admissionNotes: e.target.value }))}
                      placeholder="General notes about the admission"
                      rows={2}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="observedProblems">Observed Problems</Label>
                    <Textarea
                      id="observedProblems"
                      value={newAdmission.observedProblems}
                      onChange={(e) => setNewAdmission(prev => ({ ...prev, observedProblems: e.target.value }))}
                      placeholder="Specific problems or issues observed"
                      rows={2}
                    />
                  </div>

                  <div className="grid grid-cols-3 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="mileageReading">Mileage Reading</Label>
                      <Input
                        id="mileageReading"
                        type="number"
                        value={newAdmission.mileageReading}
                        onChange={(e) => setNewAdmission(prev => ({ ...prev, mileageReading: parseFloat(e.target.value) || 0 }))}
                        placeholder="0"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="hoursReading">Hours Reading</Label>
                      <Input
                        id="hoursReading"
                        type="number"
                        value={newAdmission.hoursReading}
                        onChange={(e) => setNewAdmission(prev => ({ ...prev, hoursReading: parseFloat(e.target.value) || 0 }))}
                        placeholder="0"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="fuelLevel">Fuel Level (%)</Label>
                      <Input
                        id="fuelLevel"
                        type="number"
                        min="0"
                        max="100"
                        value={newAdmission.fuelLevel}
                        onChange={(e) => setNewAdmission(prev => ({ ...prev, fuelLevel: parseFloat(e.target.value) || 0 }))}
                        placeholder="0"
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="admissionLocation">Location</Label>
                      <Input
                        id="admissionLocation"
                        value={newAdmission.admissionLocation}
                        onChange={(e) => setNewAdmission(prev => ({ ...prev, admissionLocation: e.target.value }))}
                        placeholder="Maintenance facility location"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="bayOrStation">Bay/Station</Label>
                      <Input
                        id="bayOrStation"
                        value={newAdmission.bayOrStation}
                        onChange={(e) => setNewAdmission(prev => ({ ...prev, bayOrStation: e.target.value }))}
                        placeholder="Bay A1, Station 3, etc."
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="estimatedCompletionDate">Est. Completion Date</Label>
                      <Input
                        id="estimatedCompletionDate"
                        type="date"
                        value={newAdmission.estimatedCompletionDate}
                        onChange={(e) => setNewAdmission(prev => ({ ...prev, estimatedCompletionDate: e.target.value }))}
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="estimatedDischargeDate">Est. Discharge Date</Label>
                      <Input
                        id="estimatedDischargeDate"
                        type="date"
                        value={newAdmission.estimatedDischargeDate}
                        onChange={(e) => setNewAdmission(prev => ({ ...prev, estimatedDischargeDate: e.target.value }))}
                      />
                    </div>
                  </div>
                </div>
                <DialogFooter>
                  <Button variant="outline" onClick={() => setIsAdmissionDialogOpen(false)}>
                    Cancel
                  </Button>
                  <Button onClick={handleCreateAdmission}>
                    Admit Asset
                  </Button>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </ClientOnly>
        </div>
      </div>

      {/* Filters */}
      {viewMode === 'admissions' && (
        <Card>
          <CardContent className="p-4">
            <div className="flex items-center space-x-4">
              <div className="flex-1 max-w-sm">
                <Label htmlFor="search" className="sr-only">Search</Label>
                <div className="relative">
                  <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    id="search"
                    placeholder="Search admissions..."
                    className="pl-8"
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                  />
                </div>
              </div>
              <div className="space-y-1">
                <Label htmlFor="status-filter" className="text-sm">Status</Label>
                <Select value={statusFilter} onValueChange={setStatusFilter}>
                  <SelectTrigger className="w-[140px]">
                    <SelectValue placeholder="All Status" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Status</SelectItem>
                    <SelectItem value="Active">Active</SelectItem>
                    <SelectItem value="Completed">Completed</SelectItem>
                    <SelectItem value="Cancelled">Cancelled</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="type-filter" className="text-sm">Type</Label>
                <Select value={typeFilter} onValueChange={setTypeFilter}>
                  <SelectTrigger className="w-[140px]">
                    <SelectValue placeholder="All Types" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Types</SelectItem>
                    <SelectItem value="Scheduled">Scheduled</SelectItem>
                    <SelectItem value="Emergency">Emergency</SelectItem>
                    <SelectItem value="Breakdown">Breakdown</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Admissions Table */}
      {viewMode === 'admissions' && (
        <Card>
          <CardHeader>
            <CardTitle>Asset Admissions ({filteredAdmissions.length})</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Admission #</TableHead>
                  <TableHead>Asset</TableHead>
                  <TableHead>Job Card</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Condition</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Admitted</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredAdmissions.map((admission) => (
                  <TableRow key={admission.id}>
                    <TableCell className="font-medium">{admission.admissionNumber}</TableCell>
                    <TableCell>
                      <div>
                        <div className="font-medium">{admission.assetName}</div>
                        <div className="text-sm text-muted-foreground">{admission.assetNumber}</div>
                      </div>
                    </TableCell>
                    <TableCell>{renderJobCardCell(admission.jobCardId)}</TableCell>
                    <TableCell>{getTypeBadge(admission.admissionType)}</TableCell>
                    <TableCell>{getConditionBadge(admission.assetConditionOnAdmission)}</TableCell>
                    <TableCell>
                      <div className="flex items-center text-sm">
                        <MapPin className="h-3 w-3 mr-1" />
                        {admission.bayOrStation || admission.admissionLocation || 'Not specified'}
                      </div>
                    </TableCell>
                    <TableCell>{getStatusBadge(admission.status)}</TableCell>
                    <TableCell>{new Date(admission.admissionDate).toLocaleDateString()}</TableCell>
                    <TableCell className="text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" title="Actions" aria-label={`Actions for ${admission.admissionNumber}`}>
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-56">
                          <DropdownMenuItem onSelect={() => handleViewAdmission(admission)}>
                            <Eye className="mr-2 h-4 w-4" />
                            View Details
                          </DropdownMenuItem>
                          <DropdownMenuItem onSelect={() => void handleOpenConditionInspection(admission, 'Admission')}>
                            <ClipboardCheck className="mr-2 h-4 w-4" />
                            Admission Checklist
                          </DropdownMenuItem>
                          {admission.status === 'Active' && (
                            <>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem
                                className="text-amber-700 focus:text-amber-800"
                                onSelect={() => void handleOpenConditionInspection(admission, 'Discharge')}
                              >
                                <ClipboardCheck className="mr-2 h-4 w-4" />
                                Discharge Checklist
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-green-700 focus:text-green-800"
                                onSelect={() => {
                                  setNewDischarge(prev => ({ ...prev, admissionId: admission.id }));
                                  setIsDischargeDialogOpen(true);
                                }}
                              >
                                <CheckCircle2 className="mr-2 h-4 w-4" />
                                Discharge Asset
                              </DropdownMenuItem>
                            </>
                          )}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Discharges Table */}
      {viewMode === 'discharges' && (
        <Card>
          <CardHeader>
            <CardTitle>Asset Discharges ({discharges.length})</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Discharge #</TableHead>
                  <TableHead>Asset</TableHead>
                  <TableHead>Job Card</TableHead>
                  <TableHead>Condition</TableHead>
                  <TableHead>Quality Check</TableHead>
                  <TableHead>Customer Acceptance</TableHead>
                  <TableHead>Certificate</TableHead>
                  <TableHead>Discharged</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {discharges.map((discharge) => (
                  <TableRow key={discharge.id}>
                    <TableCell className="font-medium">{discharge.dischargeNumber}</TableCell>
                    <TableCell>
                      <div>
                        <div className="font-medium">{discharge.assetName}</div>
                        <div className="text-sm text-muted-foreground">{discharge.assetNumber}</div>
                      </div>
                    </TableCell>
                    <TableCell>{renderJobCardCell(discharge.jobCardId)}</TableCell>
                    <TableCell>{getConditionBadge(discharge.assetConditionOnDischarge)}</TableCell>
                    <TableCell>
                      {discharge.qualityCheckPassed ? (
                        <Badge className="bg-green-100 text-green-800">
                          <CheckCircle2 className="h-3 w-3 mr-1" />
                          Passed
                        </Badge>
                      ) : (
                        <Badge className="bg-red-100 text-red-800">
                          <AlertTriangle className="h-3 w-3 mr-1" />
                          Failed
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell>
                      {discharge.customerAcceptance ? (
                        <Badge className="bg-green-100 text-green-800">Accepted</Badge>
                      ) : (
                        <Badge className="bg-yellow-100 text-yellow-800">Pending</Badge>
                      )}
                    </TableCell>
                    <TableCell>
                      {discharge.certificateGenerated ? (
                        <Badge className="bg-blue-100 text-blue-800">
                          <FileText className="h-3 w-3 mr-1" />
                          Generated
                        </Badge>
                      ) : (
                        <Badge className="bg-gray-100 text-gray-800">Not Generated</Badge>
                      )}
                    </TableCell>
                    <TableCell>{new Date(discharge.dischargeDate).toLocaleDateString()}</TableCell>
                    <TableCell className="text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" title="Actions" aria-label={`Actions for ${discharge.dischargeNumber}`}>
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-56">
                          <DropdownMenuItem onSelect={() => handleViewDischarge(discharge)}>
                            <Eye className="mr-2 h-4 w-4" />
                            View Details
                          </DropdownMenuItem>
                          {!discharge.certificateGenerated && (
                            <DropdownMenuItem
                              className="text-blue-700 focus:text-blue-800"
                              onSelect={() => void handleGenerateCertificate(discharge)}
                            >
                              <FileText className="mr-2 h-4 w-4" />
                              Generate Certificate
                            </DropdownMenuItem>
                          )}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Discharge Dialog */}
      <ClientOnly>
        <Dialog open={isDischargeDialogOpen} onOpenChange={setIsDischargeDialogOpen}>
          <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Discharge Asset from Maintenance</DialogTitle>
              <DialogDescription>
                Complete the asset discharge process with quality control and customer acceptance.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="assetConditionOnDischarge">Asset Condition on Discharge</Label>
                  <Select value={newDischarge.assetConditionOnDischarge} onValueChange={(value: any) => setNewDischarge(prev => ({ ...prev, assetConditionOnDischarge: value }))}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select condition" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Excellent">Excellent</SelectItem>
                      <SelectItem value="Good">Good</SelectItem>
                      <SelectItem value="Fair">Fair</SelectItem>
                      <SelectItem value="Poor">Poor</SelectItem>
                      <SelectItem value="Critical">Critical</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="qualityCheckPassed">Quality Check</Label>
                  <Select value={newDischarge.qualityCheckPassed.toString()} onValueChange={(value) => setNewDischarge(prev => ({ ...prev, qualityCheckPassed: value === 'true' }))}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select result" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="true">Passed</SelectItem>
                      <SelectItem value="false">Failed</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="workCompleted">Work Completed</Label>
                <Textarea
                  id="workCompleted"
                  value={newDischarge.workCompleted}
                  onChange={(e) => setNewDischarge(prev => ({ ...prev, workCompleted: e.target.value }))}
                  placeholder="Describe the work that was completed"
                  rows={3}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="dischargeNotes">Discharge Notes</Label>
                <Textarea
                  id="dischargeNotes"
                  value={newDischarge.dischargeNotes}
                  onChange={(e) => setNewDischarge(prev => ({ ...prev, dischargeNotes: e.target.value }))}
                  placeholder="Additional notes about the discharge"
                  rows={2}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="remainingIssues">Remaining Issues</Label>
                <Textarea
                  id="remainingIssues"
                  value={newDischarge.remainingIssues}
                  onChange={(e) => setNewDischarge(prev => ({ ...prev, remainingIssues: e.target.value }))}
                  placeholder="Any issues that remain unresolved"
                  rows={2}
                />
              </div>

              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="dischargeMileage">Mileage Reading</Label>
                  <Input
                    id="dischargeMileage"
                    type="number"
                    value={newDischarge.mileageReading}
                    onChange={(e) => setNewDischarge(prev => ({ ...prev, mileageReading: parseFloat(e.target.value) || 0 }))}
                    placeholder="0"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dischargeHours">Hours Reading</Label>
                  <Input
                    id="dischargeHours"
                    type="number"
                    value={newDischarge.hoursReading}
                    onChange={(e) => setNewDischarge(prev => ({ ...prev, hoursReading: parseFloat(e.target.value) || 0 }))}
                    placeholder="0"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dischargeFuel">Fuel Level (%)</Label>
                  <Input
                    id="dischargeFuel"
                    type="number"
                    min="0"
                    max="100"
                    value={newDischarge.fuelLevel}
                    onChange={(e) => setNewDischarge(prev => ({ ...prev, fuelLevel: parseFloat(e.target.value) || 0 }))}
                    placeholder="0"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="qualityCheckNotes">Quality Check Notes</Label>
                <Textarea
                  id="qualityCheckNotes"
                  value={newDischarge.qualityCheckNotes}
                  onChange={(e) => setNewDischarge(prev => ({ ...prev, qualityCheckNotes: e.target.value }))}
                  placeholder="Quality control inspection notes"
                  rows={2}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="customerAcceptance">Customer Acceptance</Label>
                  <Select value={newDischarge.customerAcceptance.toString()} onValueChange={(value) => setNewDischarge(prev => ({ ...prev, customerAcceptance: value === 'true' }))}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select acceptance" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="true">Accepted</SelectItem>
                      <SelectItem value="false">Pending</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="warrantyDays">Warranty Days</Label>
                  <Input
                    id="warrantyDays"
                    type="number"
                    min="0"
                    value={newDischarge.warrantyDays}
                    onChange={(e) => setNewDischarge(prev => ({ ...prev, warrantyDays: parseInt(e.target.value) || 0 }))}
                    placeholder="0"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="acceptanceNotes">Acceptance Notes</Label>
                <Textarea
                  id="acceptanceNotes"
                  value={newDischarge.acceptanceNotes}
                  onChange={(e) => setNewDischarge(prev => ({ ...prev, acceptanceNotes: e.target.value }))}
                  placeholder="Customer acceptance notes"
                  rows={2}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="warrantyTerms">Warranty Terms</Label>
                <Textarea
                  id="warrantyTerms"
                  value={newDischarge.warrantyTerms}
                  onChange={(e) => setNewDischarge(prev => ({ ...prev, warrantyTerms: e.target.value }))}
                  placeholder="Warranty terms and conditions"
                  rows={2}
                />
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsDischargeDialogOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleCreateDischarge}>
                Discharge Asset
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </ClientOnly>

      {/* View Details Dialog */}
      <ClientOnly>
        <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
          <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>
                {selectedAdmission
                  ? `Admission ${selectedAdmission.admissionNumber}`
                  : selectedDischarge
                    ? `Discharge ${selectedDischarge.dischargeNumber}`
                    : 'Maintenance Admission Details'}
              </DialogTitle>
              <DialogDescription>
                {selectedAdmission
                  ? 'Asset admission details and linked job card information.'
                  : 'Asset discharge details and completion information.'}
              </DialogDescription>
            </DialogHeader>

            {selectedAdmission && (
              <div className="space-y-5">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label className="text-sm text-muted-foreground">Asset</Label>
                    <p className="font-medium">{selectedAdmission.assetName}</p>
                    <p className="text-sm text-muted-foreground">{selectedAdmission.assetNumber}</p>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Job Card</Label>
                    <div className="mt-1">{renderJobCardCell(selectedAdmission.jobCardId)}</div>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Admission Type</Label>
                    <div className="mt-1">{getTypeBadge(selectedAdmission.admissionType)}</div>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Status</Label>
                    <div className="mt-1">{getStatusBadge(selectedAdmission.status)}</div>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Condition</Label>
                    <div className="mt-1">{getConditionBadge(selectedAdmission.assetConditionOnAdmission)}</div>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Admission Date</Label>
                    <p className="font-medium">{new Date(selectedAdmission.admissionDate).toLocaleString()}</p>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Location</Label>
                    <p className="font-medium">{selectedAdmission.bayOrStation || selectedAdmission.admissionLocation || 'Not specified'}</p>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Admitted By</Label>
                    <p className="font-medium">{selectedAdmission.admittedBy || 'N/A'}</p>
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-4 rounded-md border bg-muted/20 p-3">
                  <div>
                    <Label className="text-sm text-muted-foreground">Mileage</Label>
                    <p className="font-medium">{selectedAdmission.mileageReading ?? '-'}</p>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Hours</Label>
                    <p className="font-medium">{selectedAdmission.hoursReading ?? '-'}</p>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Fuel Level</Label>
                    <p className="font-medium">{selectedAdmission.fuelLevel ?? '-'}%</p>
                  </div>
                </div>

                {(selectedAdmission.admissionNotes || selectedAdmission.observedProblems) && (
                  <div className="space-y-3">
                    {selectedAdmission.admissionNotes && (
                      <div>
                        <Label className="text-sm text-muted-foreground">Admission Notes</Label>
                        <p className="mt-1 rounded-md border bg-muted/20 p-3 text-sm">{selectedAdmission.admissionNotes}</p>
                      </div>
                    )}
                    {selectedAdmission.observedProblems && (
                      <div>
                        <Label className="text-sm text-muted-foreground">Observed Problems</Label>
                        <p className="mt-1 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">{selectedAdmission.observedProblems}</p>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}

            {selectedDischarge && (
              <div className="space-y-5">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label className="text-sm text-muted-foreground">Asset</Label>
                    <p className="font-medium">{selectedDischarge.assetName}</p>
                    <p className="text-sm text-muted-foreground">{selectedDischarge.assetNumber}</p>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Job Card</Label>
                    <div className="mt-1">{renderJobCardCell(selectedDischarge.jobCardId)}</div>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Discharge Date</Label>
                    <p className="font-medium">{new Date(selectedDischarge.dischargeDate).toLocaleString()}</p>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Condition on Discharge</Label>
                    <div className="mt-1">{getConditionBadge(selectedDischarge.assetConditionOnDischarge)}</div>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Quality Check</Label>
                    <div className="mt-1">
                      {selectedDischarge.qualityCheckPassed ? (
                        <Badge className="bg-green-100 text-green-800">Passed</Badge>
                      ) : (
                        <Badge className="bg-red-100 text-red-800">Failed</Badge>
                      )}
                    </div>
                  </div>
                  <div>
                    <Label className="text-sm text-muted-foreground">Customer Acceptance</Label>
                    <div className="mt-1">
                      {selectedDischarge.customerAcceptance ? (
                        <Badge className="bg-green-100 text-green-800">Accepted</Badge>
                      ) : (
                        <Badge className="bg-yellow-100 text-yellow-800">Pending</Badge>
                      )}
                    </div>
                  </div>
                </div>

                {(selectedDischarge.workCompleted || selectedDischarge.dischargeNotes || selectedDischarge.remainingIssues) && (
                  <div className="space-y-3">
                    {selectedDischarge.workCompleted && (
                      <div>
                        <Label className="text-sm text-muted-foreground">Work Completed</Label>
                        <p className="mt-1 rounded-md border bg-muted/20 p-3 text-sm">{selectedDischarge.workCompleted}</p>
                      </div>
                    )}
                    {selectedDischarge.dischargeNotes && (
                      <div>
                        <Label className="text-sm text-muted-foreground">Discharge Notes</Label>
                        <p className="mt-1 rounded-md border bg-muted/20 p-3 text-sm">{selectedDischarge.dischargeNotes}</p>
                      </div>
                    )}
                    {selectedDischarge.remainingIssues && (
                      <div>
                        <Label className="text-sm text-muted-foreground">Remaining Issues</Label>
                        <p className="mt-1 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">{selectedDischarge.remainingIssues}</p>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}
          </DialogContent>
        </Dialog>
      </ClientOnly>

      {/* Condition Inspection Dialog */}
      <ClientOnly>
        <Dialog open={isConditionDialogOpen} onOpenChange={setIsConditionDialogOpen}>
          <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <ClipboardCheck className="h-5 w-5" />
                {inspectionType} Condition Inspection
              </DialogTitle>
              <DialogDescription>
                {selectedAdmission && `Asset: ${selectedAdmission.assetName} (${selectedAdmission.assetNumber})`}
              </DialogDescription>
            </DialogHeader>

            {/* Template Selection - only show if no inspection started */}
            {!currentInspection && (
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label>Select Checklist Template</Label>
                  <Select
                    value={selectedTemplate?.id || ''}
                    onValueChange={(value) => setSelectedTemplate(conditionTemplates.find(t => t.id === value) || null)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select a checklist template" />
                    </SelectTrigger>
                    <SelectContent>
                      {conditionTemplates.map(template => (
                        <SelectItem key={template.id} value={template.id}>
                          {template.name} ({template.itemCount} items)
                          {template.isDefault && ' - Default'}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {selectedTemplate && (
                  <div className="p-4 border rounded-lg bg-muted/30">
                    <h4 className="font-medium mb-2">{selectedTemplate.name}</h4>
                    <p className="text-sm text-muted-foreground mb-2">{selectedTemplate.description}</p>
                    <div className="text-sm">
                      <span className="font-medium">{selectedTemplate.itemCount}</span> items to check
                    </div>
                  </div>
                )}

                {/* Show existing records info */}
                {existingAdmissionRecord && inspectionType === 'Admission' && (
                  <div className={`p-4 border rounded-lg ${existingAdmissionRecord.status === 'Completed' ? 'bg-green-50' : 'bg-yellow-50'}`}>
                    <div className={`flex items-center gap-2 ${existingAdmissionRecord.status === 'Completed' ? 'text-green-700' : 'text-yellow-700'}`}>
                      {existingAdmissionRecord.status === 'Completed' ? (
                        <>
                          <CheckCircle className="h-5 w-5" />
                          <span>Admission inspection completed on {new Date(existingAdmissionRecord.inspectionDate).toLocaleDateString()}</span>
                        </>
                      ) : (
                        <>
                          <Clock className="h-5 w-5" />
                          <span>Draft inspection in progress - started on {new Date(existingAdmissionRecord.inspectionDate).toLocaleDateString()}</span>
                        </>
                      )}
                    </div>
                  </div>
                )}

                {existingDischargeRecord && inspectionType === 'Discharge' && (
                  <div className={`p-4 border rounded-lg ${existingDischargeRecord.status === 'Completed' ? 'bg-green-50' : 'bg-yellow-50'}`}>
                    <div className={`flex items-center gap-2 ${existingDischargeRecord.status === 'Completed' ? 'text-green-700' : 'text-yellow-700'}`}>
                      {existingDischargeRecord.status === 'Completed' ? (
                        <>
                          <CheckCircle className="h-5 w-5" />
                          <span>Discharge inspection completed on {new Date(existingDischargeRecord.inspectionDate).toLocaleDateString()}</span>
                        </>
                      ) : (
                        <>
                          <Clock className="h-5 w-5" />
                          <span>Draft inspection in progress - started on {new Date(existingDischargeRecord.inspectionDate).toLocaleDateString()}</span>
                        </>
                      )}
                    </div>
                  </div>
                )}

                {/* Show Resume button for in-progress inspections */}
                {((inspectionType === 'Admission' && existingAdmissionRecord?.status === 'InProgress') ||
                  (inspectionType === 'Discharge' && existingDischargeRecord?.status === 'InProgress')) ? (
                  <Button
                    onClick={() => {
                      const record = inspectionType === 'Admission' ? existingAdmissionRecord : existingDischargeRecord;
                      if (record) {
                        setCurrentInspection(record);
                        // Find and set the template
                        const template = conditionTemplates.find(t => t.id === record.templateId);
                        if (template) {
                          setSelectedTemplate(template);
                        }
                        // Populate item responses from the saved record
                        const responses: Record<string, SubmitAssetConditionItemDto> = {};
                        record.itemResults.forEach(result => {
                          responses[result.checklistItemId] = {
                            checklistItemId: result.checklistItemId,
                            isPresent: result.isPresent,
                            textValue: result.textValue,
                            numericValue: result.numericValue,
                            selectedOption: result.selectedOption,
                            comment: result.comment,
                            repairReplacementAction: result.repairReplacementAction as 'None' | 'Repair' | 'Replace' | undefined,
                            photoPaths: result.photoPaths
                          };
                        });
                        setItemResponses(responses);
                      }
                    }}
                    className="w-full bg-yellow-600 hover:bg-yellow-700"
                  >
                    Resume Draft Inspection
                  </Button>
                ) : (
                  <Button
                    onClick={handleStartInspection}
                    disabled={!selectedTemplate || isSubmittingInspection ||
                      (inspectionType === 'Admission' && existingAdmissionRecord?.status === 'Completed') ||
                      (inspectionType === 'Discharge' && existingDischargeRecord?.status === 'Completed')}
                    className="w-full"
                  >
                    {isSubmittingInspection ? 'Starting...' : 'Start New Inspection'}
                  </Button>
                )}
              </div>
            )}

            {/* Inspection Form - show checklist items */}
            {currentInspection && selectedTemplate && (
              <div className="space-y-4">
                <div className="flex items-center justify-between p-3 border rounded-lg bg-blue-50">
                  <div>
                    <span className="font-medium">Inspection #{currentInspection.inspectionNumber}</span>
                    <span className="text-sm text-muted-foreground ml-4">
                      Status: {currentInspection.status}
                    </span>
                  </div>
                  <Badge>
                    {Object.keys(itemResponses).filter(k => itemResponses[k].isPresent !== undefined || itemResponses[k].textValue || itemResponses[k].numericValue !== undefined || itemResponses[k].selectedOption).length}
                    /{selectedTemplate.checklistItems.length} completed
                  </Badge>
                </div>

                <div className="space-y-3 max-h-[400px] overflow-y-auto">
                  {selectedTemplate.checklistItems.map((item, index) => (
                    <div key={item.id} className="p-4 border rounded-lg">
                      <div className="flex items-start justify-between mb-2">
                        <div>
                          <span className="font-medium">{index + 1}. {item.itemName}</span>
                          {item.isRequired && <Badge variant="destructive" className="ml-2 text-xs">Required</Badge>}
                          {item.requiresPhoto && <Badge variant="outline" className="ml-2 text-xs">📷</Badge>}
                        </div>
                        <Badge variant="outline">{item.category}</Badge>
                      </div>
                      {item.helpText && (
                        <p className="text-sm text-muted-foreground mb-3">{item.helpText}</p>
                      )}

                      {/* Response input based on item type */}
                      {item.itemType === 'Boolean' && (
                        <div className="flex items-center gap-4">
                          <Button
                            size="sm"
                            variant={itemResponses[item.id]?.isPresent === true ? 'default' : 'outline'}
                            onClick={() => setItemResponses(prev => ({
                              ...prev,
                              [item.id]: { ...prev[item.id], checklistItemId: item.id, isPresent: true }
                            }))}
                            className="flex items-center gap-2"
                          >
                            <CheckCircle className="h-4 w-4" /> Present
                          </Button>
                          <Button
                            size="sm"
                            variant={itemResponses[item.id]?.isPresent === false ? 'destructive' : 'outline'}
                            onClick={() => setItemResponses(prev => ({
                              ...prev,
                              [item.id]: { ...prev[item.id], checklistItemId: item.id, isPresent: false }
                            }))}
                            className="flex items-center gap-2"
                          >
                            <XCircle className="h-4 w-4" /> Absent
                          </Button>
                        </div>
                      )}

                      {item.itemType === 'Text' && (
                        <Textarea
                          value={itemResponses[item.id]?.textValue || ''}
                          onChange={(e) => setItemResponses(prev => ({
                            ...prev,
                            [item.id]: { ...prev[item.id], checklistItemId: item.id, textValue: e.target.value }
                          }))}
                          placeholder="Enter description..."
                          rows={2}
                        />
                      )}

                      {item.itemType === 'Numeric' && (
                        <div className="flex items-center gap-2">
                          <Input
                            type="number"
                            value={itemResponses[item.id]?.numericValue ?? ''}
                            onChange={(e) => setItemResponses(prev => ({
                              ...prev,
                              [item.id]: { ...prev[item.id], checklistItemId: item.id, numericValue: parseFloat(e.target.value) || undefined }
                            }))}
                            placeholder={`${item.minValue ?? 0} - ${item.maxValue ?? 100}`}
                            className="w-32"
                          />
                          {item.unit && <span className="text-sm text-muted-foreground">{item.unit}</span>}
                        </div>
                      )}

                      {item.itemType === 'Choice' && item.choiceOptions && (
                        <Select
                          value={itemResponses[item.id]?.selectedOption || ''}
                          onValueChange={(value) => setItemResponses(prev => ({
                            ...prev,
                            [item.id]: { ...prev[item.id], checklistItemId: item.id, selectedOption: value }
                          }))}
                        >
                          <SelectTrigger className="w-48">
                            <SelectValue placeholder="Select option" />
                          </SelectTrigger>
                          <SelectContent>
                            {item.choiceOptions.map(opt => (
                              <SelectItem key={opt} value={opt}>{opt}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      )}

                      {/* Comment field for all types */}
                      <div className="mt-2">
                        <Input
                          placeholder="Add comment (optional)"
                          value={itemResponses[item.id]?.comment || ''}
                          onChange={(e) => setItemResponses(prev => ({
                            ...prev,
                            [item.id]: { ...prev[item.id], checklistItemId: item.id, comment: e.target.value }
                          }))}
                        />
                      </div>

                      {item.allowRepairReplacement && inspectionType === 'Admission' && (
                        <div className="mt-3 p-3 border rounded-lg bg-orange-50 dark:bg-orange-950/20">
                          <div className="flex flex-wrap items-center gap-4">
                            <Label className="text-sm font-medium">Repair/Replace Action</Label>
                            <Select
                              value={itemResponses[item.id]?.repairReplacementAction || item.defaultRepairReplacementAction || 'None'}
                              onValueChange={(value) => {
                                const repairReplacementAction = value as SubmitAssetConditionItemDto['repairReplacementAction'];
                                setItemResponses(prev => ({
                                  ...prev,
                                  [item.id]: { ...prev[item.id], checklistItemId: item.id, repairReplacementAction }
                                }));
                              }}
                            >
                              <SelectTrigger className="w-40">
                                <SelectValue placeholder="Select action" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="None">None</SelectItem>
                                <SelectItem value="Repair">Repair</SelectItem>
                                <SelectItem value="Replace">Replace</SelectItem>
                              </SelectContent>
                            </Select>
                            {itemResponses[item.id]?.repairReplacementAction && itemResponses[item.id]?.repairReplacementAction !== 'None' && (
                              <span className="text-xs text-muted-foreground">
                                Est. {itemResponses[item.id]?.repairReplacementAction === 'Repair'
                                  ? `${item.estimatedRepairHours ?? 1} hrs`
                                  : `${item.estimatedReplacementHours ?? 1} hrs`}
                              </span>
                            )}
                          </div>
                        </div>
                      )}
                    </div>
                  ))}
                </div>

                <DialogFooter>
                  <Button variant="outline" onClick={handleSaveDraft} disabled={isSubmittingInspection}>
                    {isSubmittingInspection ? 'Saving...' : 'Save Draft'}
                  </Button>
                  <Button onClick={handleCompleteInspection} disabled={isSubmittingInspection}>
                    {isSubmittingInspection ? 'Submitting...' : 'Complete Inspection'}
                  </Button>
                </DialogFooter>
              </div>
            )}
          </DialogContent>
        </Dialog>
      </ClientOnly>
    </div>
  );
}
