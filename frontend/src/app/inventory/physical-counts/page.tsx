'use client';

import React, { useState, useEffect, useCallback } from 'react';
import axios from 'axios';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Plus, Search, Eye, ClipboardCheck, AlertTriangle, CheckCircle,
  Clock, XCircle, BarChart3, Calculator, FileDown, FileUp, Printer,
  Play, Save, Send, ThumbsUp, ThumbsDown, Trash2, RefreshCw, ShieldCheck, Pencil
} from 'lucide-react';
import {
  inventoryManagementService,
  PhysicalCountDto, PhysicalCountDetailDto, CreatePhysicalCountDto,
  PhysicalCountItemDto, WarehouseDto, WarehouseLocationDto, InventoryItemDto, RecordCountItemDto,
  PhysicalCountFilterDto, VarianceReportDto, AddCountItemDto
} from '@/services/inventoryManagementService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { toast } from 'sonner';
import { PhysicalCountReviewActions } from '@/components/inventory/PhysicalCountReviewActions';
import { PhysicalCountItemsGrid } from '@/components/inventory/PhysicalCountItemsGrid';
import { PhysicalCountControlPanel } from '@/components/inventory/PhysicalCountControlPanel';
import { PhysicalCountDraftItemDialog } from '@/components/inventory/PhysicalCountDraftItemDialog';
import { PhysicalCountSheetUploadDialog } from '@/components/inventory/PhysicalCountSheetUploadDialog';
import { createCountSheet } from '@/lib/physical-count-sheet';
import { procurementCurrencyService } from '@/services/financeCommonService';
import { formatInventoryMoney, normalizeInventoryCurrency } from '@/lib/inventory-currency';

type ProblemDetails = {
  detail?: string;
  message?: string;
  title?: string;
  code?: string;
  extensions?: { code?: string };
};

const problemMessage = (error: unknown, fallback: string) => {
  const problem = axios.isAxiosError<ProblemDetails | string>(error) ? error.response?.data : undefined;
  const message = (typeof problem === 'string' ? problem : problem?.detail || problem?.message || problem?.title) ||
    (error instanceof Error ? error.message : fallback);
  const code = typeof problem === 'object' ? problem?.code || problem?.extensions?.code : undefined;
  return code ? `${message} (${code})` : message;
};

const loadPhysicalCountDetail = async (countId: string) => {
  const [detail, evidence] = await Promise.all([
    inventoryManagementService.getPhysicalCountById(countId),
    inventoryManagementService.getPhysicalCountEvidence(countId),
  ]);
  return { ...detail, evidence };
};

// Status configurations
const CountStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200' },
  { value: 'InProgress', label: 'In Progress', color: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200' },
  { value: 'RecountRequired', label: 'Awaiting review', color: 'bg-orange-100 text-orange-800' },
  { value: 'UnderReview', label: 'Under review', color: 'bg-blue-100 text-blue-800' },
  { value: 'UnderInvestigation', label: 'Under investigation', color: 'bg-orange-100 text-orange-800' },
  { value: 'PendingStoresApproval', label: 'Stores Approval', color: 'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200' },
  { value: 'PendingFinanceApproval', label: 'Finance Approval', color: 'bg-cyan-100 text-cyan-800 dark:bg-cyan-900 dark:text-cyan-200' },
  { value: 'PendingAuditAttestation', label: 'Audit Attestation', color: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900 dark:text-indigo-200' },
  { value: 'ReadyToPost', label: 'Ready to Post', color: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900 dark:text-emerald-200' },
  { value: 'PendingApproval', label: 'Pending Approval', color: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200' },
  { value: 'Approved', label: 'Approved', color: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200' },
  { value: 'Posted', label: 'Posted', color: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200' },
  { value: 'Cancelled', label: 'Cancelled', color: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200' }
];

const CountTypes = [
  { value: 'FullCount', label: 'Full Count' },
  { value: 'CycleCount', label: 'Cycle Count' },
  { value: 'SpotCheck', label: 'Spot Check' },
  { value: 'ABCCount', label: 'ABC Count' }
];

export default function PhysicalCountsPage() {
  // Data states
  const [counts, setCounts] = useState<PhysicalCountDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [selectedCount, setSelectedCount] = useState<PhysicalCountDetailDto | null>(null);
  const [varianceReport, setVarianceReport] = useState<VarianceReportDto | null>(null);

  // UI states
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [currencyCode, setCurrencyCode] = useState('GHS');
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [evidenceTitle, setEvidenceTitle] = useState('');
  const [evidenceUploading, setEvidenceUploading] = useState(false);
  const [draftNotes, setDraftNotes] = useState('');
  const [removeTarget, setRemoveTarget] = useState<PhysicalCountItemDto | null>(null);
  const [cancelTarget, setCancelTarget] = useState<PhysicalCountDto | null>(null);
  const [cancellationReason, setCancellationReason] = useState('');

  // Dialog states
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [detailDialogOpen, setDetailDialogOpen] = useState(false);
  const [countItemsFullPage, setCountItemsFullPage] = useState(false);
  const [varianceDialogOpen, setVarianceDialogOpen] = useState(false);
  const [confirmDialogOpen, setConfirmDialogOpen] = useState(false);
  const [reasonDialogOpen, setReasonDialogOpen] = useState(false);
  const [addItemDialogOpen, setAddItemDialogOpen] = useState(false);
  const [importDialogOpen, setImportDialogOpen] = useState(false);

  // Confirmation dialog state
  const [confirmAction, setConfirmAction] = useState<{
    title: string;
    message: string;
    action: () => Promise<void>;
  } | null>(null);

  // Reason dialog state
  const [reasonAction, setReasonAction] = useState<{
    title: string;
    message: string;
    action: (reason: string) => Promise<void>;
  } | null>(null);
  const [reasonText, setReasonText] = useState('');

  // Filter states
  const [filters, setFilters] = useState<PhysicalCountFilterDto>({
    warehouseId: undefined,
    status: undefined,
    countType: undefined,
    fromDate: undefined,
    toDate: undefined,
    countNumber: undefined
  });

  // Form data
  const [formData, setFormData] = useState<CreatePhysicalCountDto>({
    warehouseId: '',
    countType: 'CycleCount',
    freezeInventory: true,
    blindCount: true,
    abcClass: 'C',
    notes: ''
  });
  const [countScope, setCountScope] = useState<'warehouse' | 'location'>('warehouse');
  const [countLocations, setCountLocations] = useState<WarehouseLocationDto[]>([]);
  const [locationsLoading, setLocationsLoading] = useState(false);
  const [locationsError, setLocationsError] = useState<string | null>(null);
  const [locationsReload, setLocationsReload] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setCountLocations([]);
    setLocationsError(null);
    if (!createDialogOpen || countScope !== 'location' || !formData.warehouseId) {
      setLocationsLoading(false);
      return;
    }
    setLocationsLoading(true);
    inventoryManagementService.getWarehouseLocations(formData.warehouseId)
      .then(locations => {
        if (!cancelled) setCountLocations(locations.filter(location => location.isActive && location.warehouseId === formData.warehouseId));
      })
      .catch(error => {
        if (!cancelled) setLocationsError(problemMessage(error, 'Could not load warehouse locations.'));
      })
      .finally(() => { if (!cancelled) setLocationsLoading(false); });
    return () => { cancelled = true; };
  }, [createDialogOpen, countScope, formData.warehouseId, locationsReload]);

  const canCreateCount = !!formData.warehouseId && !actionLoading && (countScope === 'warehouse' ||
    (!locationsLoading && !locationsError && countLocations.some(location => location.id === formData.locationId)));

  // Count items for recording
  const [editingItems, setEditingItems] = useState<Map<string, number>>(new Map());


  useEffect(() => {
    let cancelled = false;

    procurementCurrencyService.getActive()
      .then((currencies) => {
        const currency = currencies.find(value => value.isBaseCurrency);
        if (!cancelled) setCurrencyCode(normalizeInventoryCurrency(currency?.code));
      })
      .catch(() => {
        if (!cancelled) setCurrencyCode(normalizeInventoryCurrency());
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // Fetch data
  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const hasFilters = filters.warehouseId || filters.status || filters.countType || filters.fromDate || filters.toDate;
      const [countData, warehouseData, itemData] = await Promise.all([
        hasFilters
          ? inventoryManagementService.getPhysicalCountsFiltered(filters)
          : inventoryManagementService.getPhysicalCounts(),
        inventoryManagementService.getWarehouses(),
        inventoryManagementService.getInventoryItems()
      ]);

      setCounts(countData);
      setWarehouses(warehouseData);
      setInventoryItems(itemData);
    } catch (err: any) {
      console.error('Error fetching data:', err);
      setError('Failed to load physical counts');
    } finally {
      setLoading(false);
    }
  }, [filters]);

  useEffect(() => { fetchData(); }, [fetchData]);

  // Get status badge
  const getStatusBadge = (status: string) => {
    const s = CountStatuses.find(st => st.value === status);
    return <Badge className={s?.color || 'bg-gray-100'}>{s?.label || status}</Badge>;
  };

  // Calculate stats
  const inProgressCount = counts.filter(c => c.status === 'InProgress').length;
  const pendingApprovalCount = counts.filter(c => c.status.startsWith('Pending') || c.status === 'ReadyToPost').length;
  const postedCount = counts.filter(c => c.status === 'Posted').length;

  // Handler functions
  const handleCreate = async () => {
    if (!canCreateCount) return;
    try {
      setActionLoading(true);
      const { locationId, ...warehouseData } = formData;
      const newCount = await inventoryManagementService.createPhysicalCount(countScope === 'location' ? { ...warehouseData, locationId } : warehouseData);
      setCounts(prev => [...prev, newCount]);
      setCreateDialogOpen(false);
      setFormData({ warehouseId: '', countType: 'CycleCount', freezeInventory: true, blindCount: true, abcClass: 'C', notes: '' });
      setCountScope('warehouse');
    } catch (err) {
      console.error('Error creating count:', err);
      toast.error(problemMessage(err, 'Failed to create physical count'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleViewDetails = async (count: PhysicalCountDto) => {
    try {
      const details = await loadPhysicalCountDetail(count.id);
      setSelectedCount(details);
      setCountItemsFullPage(false);
      setDraftNotes(details.notes || '');
      setEditingItems(new Map());

      setAddItemDialogOpen(false);
      setRemoveTarget(null);
      setDetailDialogOpen(true);
    } catch (err) {
      console.error('Error fetching count details:', err);
      toast.error(problemMessage(err, 'Failed to load count details'));
    }
  };

  const handleStart = async (id: string) => {
    try {
      setActionLoading(true);
      await inventoryManagementService.startPhysicalCount(id);
      fetchData();
    } catch (err) {
      console.error('Error starting count:', err);
      toast.error(problemMessage(err, 'Failed to start count'));
    } finally {
      setActionLoading(false);
    }
  };

  const handleApprove = async (id: string) => {
    try {
      setActionLoading(true);
      await inventoryManagementService.approveVariances(id);
      fetchData();
      setDetailDialogOpen(false);
    } catch (err) {
      console.error('Error approving count:', err);
      toast.error('Failed to approve count');
    } finally {
      setActionLoading(false);
    }
  };

  const handleReject = async (id: string, reason: string) => {
    try {
      setActionLoading(true);
      await inventoryManagementService.rejectVariances(id, reason);
      fetchData();
      setDetailDialogOpen(false);
      setReasonDialogOpen(false);
      setReasonText('');
    } catch (err) {
      console.error('Error rejecting count:', err);
      toast.error('Failed to reject count');
    } finally {
      setActionLoading(false);
    }
  };

  const handlePostAdjustments = async (id: string) => {
    try {
      setActionLoading(true);
      await inventoryManagementService.postAdjustments(id);
      fetchData();
      setDetailDialogOpen(false);
    } catch (err) {
      console.error('Error posting adjustments:', err);
      toast.error('Failed to post adjustments');
    } finally {
      setActionLoading(false);
    }
  };

  const handleCancel = async () => {
    if (!cancelTarget || cancelTarget.status !== 'Draft' || !cancellationReason.trim() || actionLoading) return false;
    try {
      setActionLoading(true);
      await inventoryManagementService.cancelPhysicalCount(cancelTarget.id, cancellationReason.trim());
      await fetchData();
      toast.success('Draft count cancelled.');
      return true;
    } catch (err) {
      toast.error(problemMessage(err, 'Failed to cancel count'));
      return false;
    } finally {
      setActionLoading(false);
    }
  };

  const handleRecordItems = async () => {
    if (!selectedCount || editingItems.size === 0) return;
    if ([...editingItems.values()].some(qty => !Number.isFinite(qty) || qty < 0)) {
      toast.error('Counted quantities must be zero or positive numbers.');
      return;
    }
    try {
      setActionLoading(true);
      const items: RecordCountItemDto[] = [];
      editingItems.forEach((qty, itemId) => {
        const line = selectedCount.items.find(item => item.id === itemId);
        if (line) items.push({ physicalCountItemId: itemId, countedQuantity: qty, rowVersion: line.rowVersion, lotNumber: line.lotNumber, serialNumber: line.serialNumber, notes: line.notes, idempotencyKey: crypto.randomUUID() });
      });
      for (const item of items) await inventoryManagementService.recordCountItem(selectedCount.id, item);
      const updatedDetails = await loadPhysicalCountDetail(selectedCount.id);
      setSelectedCount(updatedDetails);
      setEditingItems(new Map());
      toast.success('Count quantities saved.');
    } catch (err) {
      console.error('Error recording items:', err);
      toast.error(problemMessage(err, 'Failed to record items'));
    } finally {
      setActionLoading(false);
    }
  };

  const refreshSelected = async () => {
    if (!selectedCount) return;
    const current = await loadPhysicalCountDetail(selectedCount.id);
    setSelectedCount(current);
    await fetchData();
  };

  const updateDraft = async (save: () => Promise<unknown>, message: string) => {
    if (!selectedCount || selectedCount.status !== 'Draft' || actionLoading) return false;
    setActionLoading(true);
    try {
      await save();
    } catch (error) {
      toast.error(problemMessage(error, 'The draft could not be updated.'));
      setActionLoading(false);
      return false;
    }
    try {
      await refreshSelected();
      toast.success(message);
    } catch {
      // Do not leave a successful mutation open for a duplicate retry.
      setDetailDialogOpen(false);
      toast.warning('The change was saved. Refresh the register before continuing.');
    } finally {
      setActionLoading(false);
    }
    return true;
  };

  const addDraftItem = (item: AddCountItemDto) => updateDraft(
    () => inventoryManagementService.addCountItem(selectedCount!.id, item), 'Item added to draft.');

  const removeDraftItem = () => {
    if (!removeTarget) return Promise.resolve(false);
    return updateDraft(() => inventoryManagementService.removeCountItem(selectedCount!.id, removeTarget.id), 'Item removed from draft.');
  };

  const uploadCountEvidence = async () => {
    if (!selectedCount || !evidenceFile) {
      toast.error('Select a signed count sheet or reconciliation evidence file.');
      return;
    }
    setEvidenceUploading(true);
    try {
      const uploaded = await inventoryManagementService.uploadPhysicalCountEvidence(selectedCount.id, evidenceFile, evidenceTitle);
      setSelectedCount(current => current?.id === selectedCount.id
        ? { ...current, evidence: [uploaded, ...current.evidence.filter(item => item.centralDocumentVersionId !== uploaded.centralDocumentVersionId)] }
        : current);
      setEvidenceFile(null);
      setEvidenceTitle('');
      await fetchData();
      toast.success('Stock-taking evidence scanned and published in Central DMS.');
    } catch (error) {
      toast.error(problemMessage(error, 'The evidence upload failed.'));
    } finally {
      setEvidenceUploading(false);
    }
  };

  const handleControlledPost = async () => {
    if (!selectedCount) return;
    setActionLoading(true);
    try {
      await inventoryManagementService.postControlledPhysicalCount(selectedCount.id, {
        rowVersion: selectedCount.rowVersion,
        idempotencyKey: crypto.randomUUID(),
        correlationId: `count-post:${selectedCount.id}:${crypto.randomUUID()}`,
        comment: 'Post independently approved count variance through Finance.',
      });
      await refreshSelected();
    } catch { toast.error('Posting was rejected by cut-off, SOD, adjustment, or Finance controls.'); }
    finally { setActionLoading(false); }
  };

  const handleExport = async (id: string) => {
    try {
      const exportData = await inventoryManagementService.exportCountSheet(id);
      const wb = createCountSheet(exportData.items);
      XLSX.writeFile(wb, `${exportData.countNumber}_CountSheet.xlsx`);
    } catch (err) {
      console.error('Error exporting count sheet:', err);
      toast.error('Failed to export count sheet');
    }
  };

  const handleViewVarianceReport = async (id: string) => {
    try {
      const report = await inventoryManagementService.getVarianceReport(id);
      setVarianceReport(report);
      setVarianceDialogOpen(true);
    } catch (err) {
      console.error('Error fetching variance report:', err);
      toast.error('Failed to load variance report');
    }
  };

  const updateItemQuantity = (itemId: string, quantity: number) => {
    setEditingItems(prev => new Map(prev).set(itemId, quantity));
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Physical Counts</h1>
          <p className="text-muted-foreground">Manage stock counts, cycle counts, and variance analysis</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={fetchData} disabled={loading}>
            <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />Refresh
          </Button>
          <Button onClick={() => setCreateDialogOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />New Count
          </Button>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Physical Counts</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <PhysicalCountControlPanel warehouses={warehouses} onCountsChanged={fetchData} />

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{counts.length}</p><p className="text-sm text-muted-foreground">Total Counts</p></div>
            <ClipboardCheck className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-blue-600">{inProgressCount}</p><p className="text-sm text-muted-foreground">In Progress</p></div>
            <Calculator className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-yellow-600">{pendingApprovalCount}</p><p className="text-sm text-muted-foreground">Pending Approval</p></div>
            <Clock className="h-8 w-8 text-yellow-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-green-600">{counts.filter(c => c.status === 'Completed').length}</p><p className="text-sm text-muted-foreground">Completed</p></div>
            <CheckCircle className="h-8 w-8 text-green-500" />
          </div>
        </CardContent></Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <Select value={filters.warehouseId || 'all'} onValueChange={(v) => setFilters({...filters, warehouseId: v === 'all' ? undefined : v})}>
              <SelectTrigger><SelectValue placeholder="Warehouse" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Warehouses</SelectItem>
                {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={filters.status || 'all'} onValueChange={(v) => setFilters({...filters, status: v === 'all' ? undefined : v})}>
              <SelectTrigger><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                {CountStatuses.map(s => <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={filters.countType || 'all'} onValueChange={(v) => setFilters({...filters, countType: v === 'all' ? undefined : v})}>
              <SelectTrigger><SelectValue placeholder="Count Type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {CountTypes.map(t => <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>)}
              </SelectContent>
            </Select>
            <Input
              type="date"
              value={filters.fromDate || ''}
              onChange={(e) => setFilters({...filters, fromDate: e.target.value || undefined})}
              placeholder="From Date"
            />
          </div>
        </CardContent>
      </Card>

      {/* Count List */}
      <Card>
        <CardHeader>
          <CardTitle>Physical Counts</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${counts.length} count(s) found`}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {counts.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No physical counts found.</div>
              ) : (
                [...counts].sort((a, b) => {
                  const timestamp = (count: PhysicalCountDto) => {
                    const created = Date.parse(count.createdAt || '');
                    const dated = Date.parse(count.countDate || '');
                    return Number.isFinite(created) ? created : Number.isFinite(dated) ? dated : 0;
                  };
                  return timestamp(b) - timestamp(a) || b.countNumber.localeCompare(a.countNumber, undefined, { numeric: true });
                }).map((count) => (
                  <div key={count.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-blue-100 dark:bg-blue-900 flex items-center justify-center">
                          <ClipboardCheck className="h-6 w-6 text-blue-600 dark:text-blue-400" />
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{count.countNumber}</h3>
                            {getStatusBadge(count.status)}
                            <Badge variant="secondary">{CountTypes.find(t => t.value === count.countType)?.label || count.countType}</Badge>
                          </div>
                          <p className="text-sm text-muted-foreground">
                            Warehouse: {count.warehouseName || '-'} • Scope: {count.locationId ? `Location: ${count.locationName || 'Selected location'}` : 'Warehouse-wide'} • Date: {count.countDate ? format(new Date(count.countDate), 'MMM dd, yyyy') : '-'}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" aria-label={count.status === 'Draft' ? 'Edit draft' : 'View'} title={count.status === 'Draft' ? 'Edit draft' : 'View count'} onClick={() => handleViewDetails(count)}>
                          {count.status === 'Draft' ? <><Pencil className="h-4 w-4 mr-1" />Edit draft</> : <Eye className="h-4 w-4" />}
                        </Button>
                        {count.status === 'Draft' && (
                          <Button size="sm" variant="outline" className="text-red-600 hover:text-red-700" aria-label={`Cancel draft count ${count.countNumber}`} title="Cancel draft count" disabled={actionLoading} onClick={() => { setCancellationReason(''); setCancelTarget(count); }}>
                            <XCircle className="h-4 w-4" />
                          </Button>
                        )}
                        {count.status === 'Draft' && (
                          <Button size="sm" variant="outline" onClick={() => handleStart(count.id)} disabled={actionLoading || count.totalItems === 0} title={count.totalItems === 0 ? 'Add at least one item before starting.' : undefined}>
                            <Play className="h-4 w-4 mr-1" />Start
                          </Button>
                        )}

                        {count.status === 'PendingApproval' && (
                          <>
                            <Button size="sm" variant="outline" className="text-green-600" onClick={() => handleApprove(count.id)}>
                              <ThumbsUp className="h-4 w-4 mr-1" />Approve
                            </Button>
                            <Button size="sm" variant="outline" className="text-red-600" onClick={() => {
                              setReasonAction({ title: 'Reject Count', message: 'Enter reason for rejection:', action: (reason) => handleReject(count.id, reason) });
                              setReasonDialogOpen(true);
                            }}>
                              <ThumbsDown className="h-4 w-4 mr-1" />Reject
                            </Button>
                          </>
                        )}
                        {count.status === 'Approved' && (
                          <Button size="sm" onClick={() => handlePostAdjustments(count.id)}>
                            <CheckCircle className="h-4 w-4 mr-1" />Post
                          </Button>
                        )}
                        <Button size="sm" variant="outline" aria-label={`Download count sheet ${count.countNumber}`} onClick={() => handleExport(count.id)}>
                          <FileDown className="h-4 w-4" />
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => handleViewVarianceReport(count.id)}>
                          <BarChart3 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Create Dialog */}
      <Dialog open={createDialogOpen} onOpenChange={setCreateDialogOpen}>
        <DialogContent className="sm:max-w-[560px] flex h-[640px] max-h-[90dvh] flex-col overflow-hidden">
          <DialogHeader className="shrink-0">
            <DialogTitle>Create Physical Count</DialogTitle>
            <DialogDescription>Start a new inventory count</DialogDescription>
          </DialogHeader>
          <div className="min-h-0 flex-1 space-y-4 overflow-y-auto py-2">
            <div className="space-y-2">
              <Label htmlFor="count-warehouse">Warehouse *</Label>
              <Select value={formData.warehouseId} disabled={actionLoading} onValueChange={(v) => { setCountLocations([]); setFormData(previous => ({...previous, warehouseId: v, locationId: undefined})); }}>
                <SelectTrigger id="count-warehouse"><SelectValue placeholder="Select warehouse" /></SelectTrigger>
                <SelectContent>{warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="count-scope">Count scope</Label>
                <Select value={countScope} disabled={actionLoading} onValueChange={value => { setCountScope(value === 'location' ? 'location' : 'warehouse'); setCountLocations([]); setFormData(previous => ({ ...previous, locationId: undefined })); }}>
                  <SelectTrigger id="count-scope"><SelectValue /></SelectTrigger>
                  <SelectContent><SelectItem value="warehouse">Warehouse-wide</SelectItem><SelectItem value="location">Selected location</SelectItem></SelectContent>
                </Select>
              </div>
              {countScope === 'location' && <div className="space-y-2">
                <Label htmlFor="count-location">Location *</Label>
                <Select value={formData.locationId || ''} onValueChange={value => setFormData(previous => ({ ...previous, locationId: value }))} disabled={actionLoading || !formData.warehouseId || locationsLoading || !!locationsError || countLocations.length === 0}>
                  <SelectTrigger id="count-location"><SelectValue placeholder={locationsLoading ? 'Loading locations...' : 'Select location'} /></SelectTrigger>
                  <SelectContent>{countLocations.map(location => <SelectItem key={location.id} value={location.id}>{location.locationCode}{location.name ? ` - ${location.name}` : ''}</SelectItem>)}</SelectContent>
                </Select>
              </div>}
            </div>
            <p className="text-xs text-muted-foreground">{countScope === 'warehouse' ? 'Count items across this warehouse, separated by stock location.' : 'Only stock in the selected location is included.'} The same scope is used for the draft, count sheet and variances.</p>
            {countScope === 'location' && locationsError && <div role="alert" className="flex items-center justify-between gap-2 text-sm text-red-600"><span>{locationsError}</span><Button size="sm" variant="outline" onClick={() => setLocationsReload(value => value + 1)}>Retry</Button></div>}
            {countScope === 'location' && formData.warehouseId && !locationsLoading && !locationsError && countLocations.length === 0 && <p role="status" className="text-sm text-amber-700">No active locations are available for this warehouse.</p>}
            <div className="space-y-2">
              <Label>Count Type</Label>
              <Select value={formData.countType} onValueChange={(v) => setFormData({...formData, countType: v})}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{CountTypes.map(t => <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="flex items-center space-x-2">
              <Checkbox
                id="freezeInventory"
                checked={formData.freezeInventory}
                onCheckedChange={(v) => setFormData({...formData, freezeInventory: !!v})}
                disabled={formData.countType === 'CycleCount'}
              />
              <Label htmlFor="freezeInventory">Freeze inventory during count</Label>
            </div>
            {formData.countType === 'CycleCount' && (
              <div className="grid grid-cols-2 gap-3 rounded-md border border-blue-200 bg-blue-50 p-3 dark:border-blue-900 dark:bg-blue-950/20">
                <div className="space-y-2"><Label>ABC class</Label><Select value={formData.abcClass || 'C'} onValueChange={(v) => setFormData({...formData, abcClass: v})}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['A', 'B', 'C'].map(value => <SelectItem key={value} value={value}>{value}</SelectItem>)}</SelectContent></Select></div>
                <div className="flex items-end"><Badge>Blind count required</Badge></div>
              </div>
            )}
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea value={formData.notes} onChange={(e) => setFormData({...formData, notes: e.target.value})} rows={3} />
            </div>
          </div>
          <DialogFooter className="shrink-0">
            <Button variant="outline" onClick={() => setCreateDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleCreate} disabled={!canCreateCount}>
              {actionLoading ? 'Creating...' : 'Create Count'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Detail Dialog */}
      <ConfirmationDialog open={!!cancelTarget} onOpenChange={open => { if (!open && !actionLoading) { setCancelTarget(null); setCancellationReason(''); } }}
        title="Cancel draft count?" description={`Cancel ${cancelTarget?.countNumber ?? 'this draft'}? The record and reason will be retained; stock quantities will not change.`}
        confirmText="Cancel count" cancelText="Keep draft" variant="destructive" isLoading={actionLoading}
        confirmDisabled={!cancellationReason.trim()} onConfirm={handleCancel}>
        <Label htmlFor="count-cancellation-reason">Cancellation reason</Label>
        <Textarea id="count-cancellation-reason" value={cancellationReason} onChange={event => setCancellationReason(event.target.value)} placeholder="Why is this draft being cancelled?" disabled={actionLoading} rows={3} />
      </ConfirmationDialog>
      <Dialog open={detailDialogOpen} onOpenChange={setDetailDialogOpen}>
        <DialogContent className={countItemsFullPage
          ? "!left-0 !top-0 flex !h-dvh !max-h-none !w-screen !max-w-none !translate-x-0 !translate-y-0 flex-col overflow-hidden !rounded-none p-4"
          : "sm:max-w-[1000px] flex h-[680px] max-h-[90dvh] flex-col overflow-hidden"}>
          <DialogHeader className="shrink-0">
            <DialogTitle className="flex items-center gap-2">
              {countItemsFullPage ? 'Count Items' : selectedCount?.status === 'Draft' ? 'Edit Draft Count' : 'Count Details'} - {selectedCount?.countNumber}
              {selectedCount && getStatusBadge(selectedCount.status)}
            </DialogTitle>
            <DialogDescription>{selectedCount?.status === 'Draft' ? 'Add or remove items before starting. Each item change is saved immediately.' : 'View and manage count items'}</DialogDescription>
          </DialogHeader>
          {selectedCount && (
            <Tabs defaultValue="details" className="flex min-h-0 flex-1 flex-col">
              <TabsList className={countItemsFullPage ? "hidden" : "grid w-full shrink-0 grid-cols-3"}>
                <TabsTrigger value="details">Details</TabsTrigger>
                <TabsTrigger value="items">Items ({selectedCount.items?.length || 0})</TabsTrigger>
                <TabsTrigger value="history">Control history</TabsTrigger>
              </TabsList>
              <TabsContent value="details" className="min-h-0 flex-1 overflow-y-auto space-y-4">
                <div className="grid grid-cols-4 gap-4">
                  <div><Label className="text-muted-foreground">Type</Label><div className="font-medium">{CountTypes.find(t => t.value === selectedCount.countType)?.label}</div></div>
                  <div><Label className="text-muted-foreground">Warehouse</Label><div className="font-medium">{selectedCount.warehouseName}</div></div>
                  <div><Label className="text-muted-foreground">Date</Label><div className="font-medium">{selectedCount.countDate ? format(new Date(selectedCount.countDate), 'MMM dd, yyyy') : '-'}</div></div>
                  <div><Label className="text-muted-foreground">Freeze</Label><div className="font-medium">{selectedCount.freezeInventory ? 'Yes' : 'No'}</div></div>
                </div>
                <div className="grid grid-cols-3 gap-4">
                  <div><Label className="text-muted-foreground">Total Items</Label><div className="font-medium">{selectedCount.totalItems}</div></div>
                  <div><Label className="text-muted-foreground">Counted</Label><div className="font-medium">{selectedCount.countedItems}</div></div>
                  <div><Label className="text-muted-foreground">With Variance</Label><div className="font-medium text-orange-600">{selectedCount.itemsWithVariance}</div></div>
                </div>
                <p className="text-sm"><span className="text-muted-foreground">Count scope: </span>{selectedCount.locationId ? `Selected location — ${selectedCount.locationName || 'Location name unavailable'}` : 'Warehouse-wide (all locations)'}</p>
                {selectedCount.items.some(item => !item.locationId) && !['Posted', 'Cancelled'].includes(selectedCount.status) && <p role="status" className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">Items without a saved bin will use this warehouse’s default location when the count starts or is submitted. Stock already spread across other bins requires a new scoped count.</p>}
                {selectedCount.status === 'Draft' ? <div className="space-y-2"><Label htmlFor="draft-count-notes">Notes (optional)</Label><Textarea id="draft-count-notes" value={draftNotes} onChange={event => setDraftNotes(event.target.value)} disabled={actionLoading} /><Button variant="outline" disabled={actionLoading || draftNotes === (selectedCount.notes || '')} onClick={() => void updateDraft(() => inventoryManagementService.updatePhysicalCount(selectedCount.id, { notes: draftNotes }), 'Draft notes saved.')}><Save className="mr-2 h-4 w-4" />Save draft notes</Button></div> : selectedCount.notes && <div><Label className="text-muted-foreground">Notes</Label><div>{selectedCount.notes}</div></div>}
                <div className="space-y-3 rounded-md border p-4">
                  <div><Label>Count sheet (optional)</Label><p className="text-sm text-muted-foreground">Edit quantities in Items and select Save Counts, or import an Excel sheet. Saved quantities are used for review.</p></div>
                  {selectedCount.evidence?.some(item => item.isCurrentCountSheet) ? selectedCount.evidence.filter(item => item.isCurrentCountSheet).map(item => <div key={item.centralDocumentVersionId} className="flex items-center justify-between gap-3 rounded-md bg-green-50 p-3 text-sm text-green-900"><div><div className="font-medium">{item.fileName || item.title}</div><div>{new Date(item.uploadedAtUtc).toLocaleString()} · {selectedCount.countedItems}/{selectedCount.totalItems} counted</div></div><Badge variant="outline">Current</Badge></div>) : <p className="text-sm text-muted-foreground">{selectedCount.status === 'Draft' ? 'No file needed to prepare this draft. Upload after starting the count.' : selectedCount.evidence?.some(item => item.isImportedCountSheet) ? 'Quantities were edited in Items. Earlier Excel files remain in history; no re-upload is needed.' : 'Excel import is optional. Enter quantities directly in Items.'}</p>}
                  {(['InProgress', 'UnderReview'].includes(selectedCount.status) && !!selectedCount.canReview) && <Button disabled={actionLoading || editingItems.size > 0} onClick={() => setImportDialogOpen(true)}><FileUp className="mr-2 h-4 w-4" />{selectedCount.evidence?.some(item => item.isImportedCountSheet) ? 'Replace' : 'Upload'}</Button>}
                  <details className="rounded-md border p-3"><summary className="cursor-pointer text-sm font-medium">Supporting files and upload history ({(selectedCount.evidence ?? []).filter(item => !item.isCurrentCountSheet).length})</summary>
                    <p className="my-3 text-sm text-muted-foreground">These files are kept for review. They do not supply or change count quantities.</p>
                    <div className="space-y-2">{(selectedCount.evidence ?? []).filter(item => !item.isCurrentCountSheet).map(item => <div key={item.centralDocumentVersionId} className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-muted p-3 text-sm"><div><div className="font-medium">{item.fileName || item.title}</div><div className="text-xs text-muted-foreground">{new Date(item.uploadedAtUtc).toLocaleString()} · {item.scanStatus}</div></div><Badge variant="outline">{item.isImportedCountSheet ? 'Previous count sheet' : 'Attachment only'}</Badge></div>)}</div>
                    {['InProgress', 'RecountRequired', 'UnderReview', 'UnderInvestigation'].includes(selectedCount.status) && <div className="mt-3 grid gap-3 md:grid-cols-[1fr_1fr_auto]"><Input value={evidenceTitle} onChange={event => setEvidenceTitle(event.target.value)} placeholder="Supporting file title (optional)" /><Input aria-label="Supporting evidence file" key={evidenceFile?.name ?? 'empty-evidence'} type="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.csv,.jpg,.jpeg,.png" onChange={event => setEvidenceFile(event.target.files?.[0] ?? null)} /><Button type="button" onClick={() => void uploadCountEvidence()} disabled={!evidenceFile || evidenceUploading}>{evidenceUploading ? 'Uploading' : 'Attach supporting file'}</Button></div>}
                  </details>
                </div>
              </TabsContent>
              <TabsContent value="items" className="min-h-0 flex-1 flex-col overflow-hidden data-[state=active]:flex">
                <PhysicalCountItemsGrid key={selectedCount.id} count={selectedCount} edits={editingItems} busy={actionLoading}
                  currencyCode={currencyCode}
                  fullPage={countItemsFullPage} onToggleFullPage={() => setCountItemsFullPage(value => !value)}
                  onQuantity={(id, value) => {
                    if (value === undefined) setEditingItems(previous => { const next = new Map(previous); next.delete(id); return next; });
                    else updateItemQuantity(id, value);
                  }}
                  onRemove={setRemoveTarget} onAdd={() => setAddItemDialogOpen(true)}
                  onImport={() => setImportDialogOpen(true)} onExport={() => void handleExport(selectedCount.id)} />
              </TabsContent>
              <TabsContent value="history" className="min-h-0 flex-1 overflow-y-auto">
                <div className="space-y-2">{[...selectedCount.actions].sort((a, b) => b.sequence - a.sequence).map(action => <div key={action.id} className="rounded-md border p-3"><div className="flex items-center justify-between"><div className="font-medium">#{action.sequence} {action.actionType}</div><Badge variant="outline">{action.actorRole}</Badge></div><div className="mt-1 text-sm text-muted-foreground">{new Date(action.occurredAtUtc).toLocaleString()} · {action.comment || 'No comment'}</div><div className="mt-1 truncate font-mono text-[10px] text-muted-foreground">{action.integrityHash}</div></div>)}</div>
              </TabsContent>
            </Tabs>
          )}
          <DialogFooter className="flex shrink-0 flex-row items-start gap-2 sm:justify-between sm:space-x-0" data-testid="count-action-footer">
            <div className="max-h-[32vh] min-w-0 flex-1 space-y-2 overflow-y-auto">
              {selectedCount && <PhysicalCountReviewActions count={selectedCount} unsaved={editingItems.size > 0}
                saving={actionLoading} onSaveCounts={() => void handleRecordItems()}
                onUploadSheet={() => setImportDialogOpen(true)} onChanged={refreshSelected} />}
              {selectedCount?.status === 'ReadyToPost' && selectedCount.canPost === true && <Button onClick={handleControlledPost} disabled={actionLoading}><CheckCircle className="mr-2 h-4 w-4" />Post</Button>}
            </div>
            <Button variant="outline" onClick={() => setDetailDialogOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {selectedCount?.status === 'Draft' && <PhysicalCountDraftItemDialog open={addItemDialogOpen} count={selectedCount} busy={actionLoading} onOpenChange={setAddItemDialogOpen} onSave={addDraftItem} />}
      <ConfirmationDialog open={!!removeTarget} onOpenChange={open => { if (!open) setRemoveTarget(null); }} title="Remove count item?" description={`Remove ${removeTarget?.itemCode} - ${removeTarget?.itemName} from this draft? Stock balances are not changed.`} confirmText="Remove item" variant="destructive" isLoading={actionLoading} onConfirm={removeDraftItem} />

      {/* Reason Dialog */}
      <Dialog open={reasonDialogOpen} onOpenChange={setReasonDialogOpen}>
        <DialogContent className="sm:max-w-[400px]">
          <DialogHeader>
            <DialogTitle>{reasonAction?.title}</DialogTitle>
            <DialogDescription>{reasonAction?.message}</DialogDescription>
          </DialogHeader>
          <div className="py-4">
            <Textarea
              value={reasonText}
              onChange={(e) => setReasonText(e.target.value)}
              placeholder="Enter reason..."
              rows={3}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setReasonDialogOpen(false); setReasonText(''); }}>Cancel</Button>
            <Button onClick={() => reasonAction?.action(reasonText)} disabled={!reasonText || actionLoading}>
              {actionLoading ? 'Processing...' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {importDialogOpen && selectedCount && <PhysicalCountSheetUploadDialog
        key={selectedCount.id}
        count={selectedCount}
        errorMessage={problemMessage}
        onSaved={refreshSelected}
        onClose={() => { setImportDialogOpen(false); void refreshSelected().catch(() => toast.warning('Refresh the register to reload the count.')); }}
      />}

      {/* Variance Report Dialog */}
      <Dialog open={varianceDialogOpen} onOpenChange={setVarianceDialogOpen}>
        <DialogContent className="sm:max-w-[800px] max-h-[90vh]">
          <DialogHeader>
            <DialogTitle>Variance Report - {varianceReport?.countNumber}</DialogTitle>
            <DialogDescription>{varianceReport?.warehouseName} • {varianceReport?.countDateFormatted}</DialogDescription>
          </DialogHeader>
          {varianceReport && (
            <div className="space-y-4">
              <div className="grid grid-cols-4 gap-4 p-4 bg-muted rounded-lg">
                <div><Label className="text-muted-foreground">Total Items</Label><div className="text-xl font-bold">{varianceReport.totalItems}</div></div>
                <div><Label className="text-muted-foreground">Items with Variance</Label><div className="text-xl font-bold text-orange-600">{varianceReport.itemsWithVariance}</div></div>
                <div><Label className="text-muted-foreground">Total Variance Value</Label><div className="text-xl font-bold text-red-600">{formatInventoryMoney(varianceReport.totalVarianceValue, currencyCode)}</div></div>
                <div><Label className="text-muted-foreground">Variance %</Label><div className="text-xl font-bold">{varianceReport.variancePercentage.toFixed(2)}%</div></div>
              </div>
              <ScrollArea className="h-[400px]">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Item</TableHead>
                      <TableHead className="text-right">System Qty</TableHead>
                      <TableHead className="text-right">Counted Qty</TableHead>
                      <TableHead className="text-right">Variance</TableHead>
                      <TableHead className="text-right">Unit Cost</TableHead>
                      <TableHead className="text-right">Variance Value</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {varianceReport.items.map((item, idx) => (
                      <TableRow key={idx}>
                        <TableCell>
                          <div className="font-medium">{item.itemCode}</div>
                          <div className="text-sm text-muted-foreground">{item.itemName}</div>
                        </TableCell>
                        <TableCell className="text-right">{item.systemQuantity}</TableCell>
                        <TableCell className="text-right">{item.countedQuantity}</TableCell>
                        <TableCell className="text-right">
                          <Badge className={item.varianceQuantity > 0 ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}>
                            {item.varianceQuantity > 0 ? '+' : ''}{item.varianceQuantity}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right">{formatInventoryMoney(item.unitCost, currencyCode)}</TableCell>
                        <TableCell className="text-right font-medium">{formatInventoryMoney(item.varianceValue, currencyCode)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </ScrollArea>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setVarianceDialogOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
