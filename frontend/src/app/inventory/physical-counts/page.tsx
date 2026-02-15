'use client';

import React, { useState, useEffect, useCallback } from 'react';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Plus, Search, Eye, ClipboardCheck, AlertTriangle, CheckCircle,
  Clock, XCircle, BarChart3, Calculator, FileDown, FileUp, Printer,
  Play, Save, Send, ThumbsUp, ThumbsDown, Trash2, RefreshCw
} from 'lucide-react';
import {
  inventoryManagementService,
  PhysicalCountDto, PhysicalCountDetailDto, CreatePhysicalCountDto,
  PhysicalCountItemDto, WarehouseDto, InventoryItemDto, RecordCountItemDto,
  PhysicalCountFilterDto, VarianceReportDto, ImportCountItemDto
} from '@/services/inventoryManagementService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { toast } from 'sonner';

// Status configurations
const CountStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200' },
  { value: 'InProgress', label: 'In Progress', color: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200' },
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

  // Dialog states
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [detailDialogOpen, setDetailDialogOpen] = useState(false);
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
    freezeInventory: false,
    notes: ''
  });

  // Add item form
  const [addItemData, setAddItemData] = useState({
    inventoryItemId: '',
    locationId: '',
    systemQuantity: 0
  });

  // Import data
  const [importItems, setImportItems] = useState<ImportCountItemDto[]>([]);

  // Count items for recording
  const [editingItems, setEditingItems] = useState<Map<string, number>>(new Map());

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
  const pendingApprovalCount = counts.filter(c => c.status === 'PendingApproval').length;
  const postedCount = counts.filter(c => c.status === 'Posted').length;

  // Handler functions
  const handleCreate = async () => {
    try {
      setActionLoading(true);
      const newCount = await inventoryManagementService.createPhysicalCount(formData);
      setCounts(prev => [...prev, newCount]);
      setCreateDialogOpen(false);
      setFormData({ warehouseId: '', countType: 'CycleCount', freezeInventory: false, notes: '' });
    } catch (err) {
      console.error('Error creating count:', err);
      toast.error('Failed to create physical count');
    } finally {
      setActionLoading(false);
    }
  };

  const handleViewDetails = async (count: PhysicalCountDto) => {
    try {
      const details = await inventoryManagementService.getPhysicalCountById(count.id);
      setSelectedCount(details);
      setDetailDialogOpen(true);
    } catch (err) {
      console.error('Error fetching count details:', err);
      toast.error('Failed to load count details');
    }
  };

  const handleStart = async (id: string) => {
    try {
      setActionLoading(true);
      await inventoryManagementService.startPhysicalCount(id);
      fetchData();
    } catch (err) {
      console.error('Error starting count:', err);
      toast.error('Failed to start count');
    } finally {
      setActionLoading(false);
    }
  };

  const handleComplete = async (id: string) => {
    try {
      setActionLoading(true);
      await inventoryManagementService.completePhysicalCount(id);
      fetchData();
      setDetailDialogOpen(false);
    } catch (err) {
      console.error('Error completing count:', err);
      toast.error('Failed to complete count');
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

  const handleCancel = async (id: string, reason: string) => {
    try {
      setActionLoading(true);
      await inventoryManagementService.cancelPhysicalCount(id, reason);
      fetchData();
      setDetailDialogOpen(false);
      setReasonDialogOpen(false);
      setReasonText('');
    } catch (err) {
      console.error('Error cancelling count:', err);
      toast.error('Failed to cancel count');
    } finally {
      setActionLoading(false);
    }
  };

  const handleRecordItems = async () => {
    if (!selectedCount || editingItems.size === 0) return;
    try {
      setActionLoading(true);
      const items: RecordCountItemDto[] = [];
      editingItems.forEach((qty, itemId) => {
        items.push({ physicalCountItemId: itemId, countedQuantity: qty });
      });
      await inventoryManagementService.recordCountItems(selectedCount.id, items);
      const updatedDetails = await inventoryManagementService.getPhysicalCountById(selectedCount.id);
      setSelectedCount(updatedDetails);
      setEditingItems(new Map());
    } catch (err) {
      console.error('Error recording items:', err);
      toast.error('Failed to record items');
    } finally {
      setActionLoading(false);
    }
  };

  const handleExport = async (id: string) => {
    try {
      const exportData = await inventoryManagementService.exportCountSheet(id);
      // Create workbook for Excel export
      const ws = XLSX.utils.json_to_sheet(exportData.items.map(item => ({
        'Item Code': item.itemCode,
        'Item Name': item.itemName,
        'UOM': item.unitOfMeasure,
        'Location': item.locationName || '',
        'System Qty': item.systemQuantity,
        'Counted Qty': item.countedQuantity,
        'Variance': item.varianceQuantity,
        'Lot Number': item.lotNumber || '',
        'Notes': item.notes || ''
      })));
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Count Sheet');
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
                counts.map((count) => (
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
                            Warehouse: {count.warehouseName || '-'} • Date: {count.countDate ? format(new Date(count.countDate), 'MMM dd, yyyy') : '-'}
                          </p>
                          <p className="text-sm text-muted-foreground">
                            Items: {count.totalItems || 0} • With Variance: {count.itemsWithVariance || 0}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleViewDetails(count)}>
                          <Eye className="h-4 w-4 mr-1" />View
                        </Button>
                        {count.status === 'Draft' && (
                          <Button size="sm" variant="outline" onClick={() => handleStart(count.id)}>
                            <Play className="h-4 w-4 mr-1" />Start
                          </Button>
                        )}
                        {count.status === 'InProgress' && (
                          <Button size="sm" variant="outline" onClick={() => handleComplete(count.id)}>
                            <Send className="h-4 w-4 mr-1" />Complete
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
                        <Button size="sm" variant="outline" onClick={() => handleExport(count.id)}>
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
        <DialogContent className="sm:max-w-[500px]">
          <DialogHeader>
            <DialogTitle>Create Physical Count</DialogTitle>
            <DialogDescription>Start a new inventory count</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label>Warehouse *</Label>
              <Select value={formData.warehouseId} onValueChange={(v) => setFormData({...formData, warehouseId: v})}>
                <SelectTrigger><SelectValue placeholder="Select warehouse" /></SelectTrigger>
                <SelectContent>{warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}</SelectContent>
              </Select>
            </div>
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
              />
              <Label htmlFor="freezeInventory">Freeze inventory during count</Label>
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea value={formData.notes} onChange={(e) => setFormData({...formData, notes: e.target.value})} rows={3} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleCreate} disabled={!formData.warehouseId || actionLoading}>
              {actionLoading ? 'Creating...' : 'Create Count'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Detail Dialog */}
      <Dialog open={detailDialogOpen} onOpenChange={setDetailDialogOpen}>
        <DialogContent className="sm:max-w-[1000px] max-h-[90vh]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              Count Details - {selectedCount?.countNumber}
              {selectedCount && getStatusBadge(selectedCount.status)}
            </DialogTitle>
            <DialogDescription>View and manage count items</DialogDescription>
          </DialogHeader>
          {selectedCount && (
            <Tabs defaultValue="details" className="w-full">
              <TabsList className="grid w-full grid-cols-3">
                <TabsTrigger value="details">Details</TabsTrigger>
                <TabsTrigger value="items">Items ({selectedCount.items?.length || 0})</TabsTrigger>
                <TabsTrigger value="variance">Variance</TabsTrigger>
              </TabsList>
              <TabsContent value="details" className="space-y-4">
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
                {selectedCount.notes && <div><Label className="text-muted-foreground">Notes</Label><div>{selectedCount.notes}</div></div>}
              </TabsContent>
              <TabsContent value="items">
                <ScrollArea className="h-[400px]">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Item</TableHead>
                        <TableHead>Location</TableHead>
                        <TableHead className="text-right">System Qty</TableHead>
                        <TableHead className="text-right">Counted Qty</TableHead>
                        <TableHead className="text-right">Variance</TableHead>
                        <TableHead>Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {selectedCount.items?.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell>
                            <div className="font-medium">{item.itemCode}</div>
                            <div className="text-sm text-muted-foreground">{item.itemName}</div>
                          </TableCell>
                          <TableCell>{item.locationName || '-'}</TableCell>
                          <TableCell className="text-right">{item.systemQuantity}</TableCell>
                          <TableCell className="text-right">
                            {selectedCount.status === 'InProgress' ? (
                              <Input
                                type="number"
                                className="w-20 text-right"
                                value={editingItems.get(item.id) ?? item.countedQuantity}
                                onChange={(e) => updateItemQuantity(item.id, parseFloat(e.target.value) || 0)}
                              />
                            ) : (
                              item.countedQuantity
                            )}
                          </TableCell>
                          <TableCell className="text-right">
                            <Badge className={item.varianceQuantity > 0 ? 'bg-green-100 text-green-800' : item.varianceQuantity < 0 ? 'bg-red-100 text-red-800' : ''}>
                              {item.varianceQuantity > 0 ? '+' : ''}{item.varianceQuantity}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            {item.isCounted ? <CheckCircle className="h-4 w-4 text-green-600" /> : <Clock className="h-4 w-4 text-gray-400" />}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </ScrollArea>
                {selectedCount.status === 'InProgress' && editingItems.size > 0 && (
                  <div className="mt-4 flex justify-end">
                    <Button onClick={handleRecordItems} disabled={actionLoading}>
                      <Save className="h-4 w-4 mr-2" />{actionLoading ? 'Saving...' : 'Save Counts'}
                    </Button>
                  </div>
                )}
              </TabsContent>
              <TabsContent value="variance">
                <div className="space-y-4">
                  <div className="grid grid-cols-3 gap-4 p-4 bg-muted rounded-lg">
                    <div><Label className="text-muted-foreground">Items with Variance</Label><div className="text-2xl font-bold text-orange-600">{selectedCount.itemsWithVariance}</div></div>
                    <div><Label className="text-muted-foreground">Total Variance Value</Label><div className="text-2xl font-bold">${selectedCount.totalVarianceValue?.toFixed(2) || '0.00'}</div></div>
                    <div><Label className="text-muted-foreground">Counted Progress</Label><div className="text-2xl font-bold">{selectedCount.totalItems > 0 ? Math.round((selectedCount.countedItems / selectedCount.totalItems) * 100) : 0}%</div></div>
                  </div>
                  <ScrollArea className="h-[300px]">
                    {selectedCount.items?.filter(i => i.varianceQuantity !== 0).map((item) => (
                      <div key={item.id} className="flex items-center justify-between p-3 border-b">
                        <div>
                          <div className="font-medium">{item.itemCode} - {item.itemName}</div>
                          <div className="text-sm text-muted-foreground">{item.locationName || 'No location'}</div>
                        </div>
                        <div className="text-right">
                          <Badge className={item.varianceQuantity > 0 ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}>
                            {item.varianceQuantity > 0 ? '+' : ''}{item.varianceQuantity} ({item.variancePercent?.toFixed(1)}%)
                          </Badge>
                          <div className="text-sm text-muted-foreground">${Math.abs(item.varianceValue || 0).toFixed(2)}</div>
                        </div>
                      </div>
                    ))}
                  </ScrollArea>
                </div>
              </TabsContent>
            </Tabs>
          )}
          <DialogFooter className="flex justify-between">
            <div className="flex gap-2">
              {selectedCount?.status === 'InProgress' && (
                <Button variant="outline" onClick={() => handleComplete(selectedCount.id)} disabled={actionLoading}>
                  <Send className="h-4 w-4 mr-2" />Complete Count
                </Button>
              )}
              {selectedCount?.status === 'PendingApproval' && (
                <>
                  <Button variant="outline" className="text-green-600" onClick={() => handleApprove(selectedCount.id)} disabled={actionLoading}>
                    <ThumbsUp className="h-4 w-4 mr-2" />Approve
                  </Button>
                  <Button variant="outline" className="text-red-600" onClick={() => {
                    setReasonAction({ title: 'Reject Count', message: 'Enter reason for rejection:', action: (reason) => handleReject(selectedCount.id, reason) });
                    setReasonDialogOpen(true);
                  }} disabled={actionLoading}>
                    <ThumbsDown className="h-4 w-4 mr-2" />Reject
                  </Button>
                </>
              )}
              {selectedCount?.status === 'Approved' && (
                <Button onClick={() => handlePostAdjustments(selectedCount.id)} disabled={actionLoading}>
                  <CheckCircle className="h-4 w-4 mr-2" />Post Adjustments
                </Button>
              )}
            </div>
            <Button variant="outline" onClick={() => setDetailDialogOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

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
                <div><Label className="text-muted-foreground">Total Variance Value</Label><div className="text-xl font-bold text-red-600">${varianceReport.totalVarianceValue.toFixed(2)}</div></div>
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
                        <TableCell className="text-right">${item.unitCost.toFixed(2)}</TableCell>
                        <TableCell className="text-right font-medium">${item.varianceValue.toFixed(2)}</TableCell>
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
