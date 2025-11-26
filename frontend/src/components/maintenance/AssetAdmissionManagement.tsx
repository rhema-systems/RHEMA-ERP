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
import { Plus, Search, Eye, Calendar, AlertTriangle, CheckCircle2, Clock, FileText, Camera, MapPin } from 'lucide-react';
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
import assetAdmissionService, {
  AssetAdmission,
  CreateAdmissionRequest,
  CreateDischargeRequest,
  AssetDischarge
} from '@/services/assetAdmissionService';
import maintenanceApiService, { Asset } from '@/services/maintenanceApiService';
import { ClientOnly } from '@/components/ClientOnly';

export default function AssetAdmissionManagement() {
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

  // Data from services
  const [assets, setAssets] = useState<Asset[]>([]);
  const [workOrders, setWorkOrders] = useState<any[]>([]);
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
        const [assetsResponse, admissionsResponse, dischargesResponse, statsResponse, workOrdersResponse] = await Promise.all([
          maintenanceApiService.getAssets(),
          assetAdmissionService.getAdmissions(initialFilter || {}),
          assetAdmissionService.getDischarges(),
          assetAdmissionService.getAdmissionStats(),
          maintenanceApiService.getWorkOrders().catch(() => ({ items: [] }))
        ]);

        setAssets(assetsResponse.items || []);
        setAdmissions(admissionsResponse.items || []);
        setDischarges(dischargesResponse.items || []);
        setFilteredAdmissions(admissionsResponse.items || []);
        setStats(statsResponse || { totalActive: 0, totalCompleted: 0, averageStayDays: 0 });
        setWorkOrders(workOrdersResponse.items || []);
      } catch (error) {
        console.error('Error loading data:', error);
        setAssets([]);
        setAdmissions([]);
        setDischarges([]);
        setFilteredAdmissions([]);
        setWorkOrders([]);
      } finally {
        setLoadingData(false);
      }
    };

    loadData();
  }, [initialFilter]);

  // Apply initial query-parameter-based filters and form defaults
  useEffect(() => {
    const assetId = searchParams.get('assetId') || undefined;
    const workOrderId = searchParams.get('workOrderId') || undefined;
    const jobCardId = searchParams.get('jobCardId') || undefined;
    const view = searchParams.get('view');

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

  // Filter admissions based on search and filters
  useEffect(() => {
    let filtered = admissions;

    if (searchTerm) {
      filtered = filtered.filter(admission =>
        admission.assetName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        admission.admissionNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
        admission.assetNumber.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter(admission => admission.status === statusFilter);
    }

    if (typeFilter && typeFilter !== 'all') {
      filtered = filtered.filter(admission => admission.admissionType === typeFilter);
    }

    setFilteredAdmissions(filtered);
  }, [admissions, searchTerm, statusFilter, typeFilter]);

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

  const refreshData = async () => {
    try {
      const [admissionsResponse, dischargesResponse, statsResponse] = await Promise.all([
        assetAdmissionService.getAdmissions(),
        assetAdmissionService.getDischarges(),
        assetAdmissionService.getAdmissionStats()
      ]);

      setAdmissions(admissionsResponse.items || []);
      setDischarges(dischargesResponse.items || []);
      setFilteredAdmissions(admissionsResponse.items || []);
      setStats(statsResponse || { totalActive: 0, totalCompleted: 0, averageStayDays: 0 });
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
                      <Select value={newAdmission.workOrderId} onValueChange={(value) => setNewAdmission(prev => ({ ...prev, workOrderId: value }))}>
                        <SelectTrigger>
                          <SelectValue placeholder="Select work order" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="">None</SelectItem>
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
                    <TableCell>
                      <div className="flex items-center space-x-2">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setSelectedAdmission(admission);
                            setIsViewDialogOpen(true);
                          }}
                        >
                          <Eye className="h-4 w-4" />
                        </Button>

                        {admission.status === 'Active' && (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setNewDischarge(prev => ({ ...prev, admissionId: admission.id }));
                              setIsDischargeDialogOpen(true);
                            }}
                            className="bg-green-50 hover:bg-green-100"
                          >
                            Discharge
                          </Button>
                        )}
                      </div>
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
                    <TableCell>
                      <div className="flex items-center space-x-2">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setSelectedDischarge(discharge);
                            setIsViewDialogOpen(true);
                          }}
                        >
                          <Eye className="h-4 w-4" />
                        </Button>

                        {!discharge.certificateGenerated && (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => assetAdmissionService.generateCompletionCertificate(discharge.id)}
                            className="bg-blue-50 hover:bg-blue-100"
                          >
                            <FileText className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
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
    </div>
  );
}