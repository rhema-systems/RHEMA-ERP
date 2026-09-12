'use client';

import React, { useState, useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Separator } from '@/components/ui/separator';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';
import { Plus, Trash2, Search, Package, AlertCircle, Barcode, Layers, Pencil, Maximize2, Minimize2 } from 'lucide-react';
import {
  inventoryManagementService,
  InventoryTransferDto, InventoryTransferDetailDto, InventoryTransferItemDto,
  WarehouseDto, AddTransferItemDto, UpdateTransferItemDto, WarehouseLocationDto,
  InventoryTransferEvidenceRequest, WarehouseInventoryItemDto
} from '@/services/inventoryManagementService';
import { documentManagementService, CentralDocumentRecord } from '@/services/document-management.service';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import {
  getInventoryTransferControlCapability,
  getInventoryTransferProblemMessage,
  getInventoryTransferStatusLabel,
} from '@/lib/inventory-transfer-controls';
import { format } from 'date-fns';
import { formatInventoryMoney } from '@/lib/inventory-currency';
import { inventoryTrackingControlService, InventoryTrackingRequirements } from '@/services/inventoryTrackingControlService';
import {
  InventoryTrackingExceptionSelect,
  useAvailableInventoryTrackingExceptions,
} from '@/components/inventory/InventoryTrackingExceptionSelect';

interface TransferDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  transfer?: InventoryTransferDto | null;
  mode: 'create' | 'edit' | 'view';
  initialTab?: 'details' | 'items' | 'approvals' | 'controls';
  warehouses: WarehouseDto[];
  currencyCode?: string;
  onSuccess: () => void;
}

interface FormData {
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  requiredDate: string;
  notes: string;
}

interface ItemFormData {
  inventoryItemId: string;
  requestedQuantity: number;
  sourceLocationId: string;
  destinationLocationId: string;
  lotNumber: string;
  batchNumber: string;
  serialNumber: string;
  manufactureDate: string;
  expiryDate: string;
  inventoryTrackingExceptionId: string;
  notes: string;
}

const TransferStatuses = [
  { value: 'Draft', label: 'Draft', color: 'bg-gray-100 text-gray-800' },
  { value: 'Submitted', label: 'Pending Approval', color: 'bg-yellow-100 text-yellow-800' },
  { value: 'Approved', label: 'Approved', color: 'bg-blue-100 text-blue-800' },
  { value: 'InTransit', label: 'In Transit', color: 'bg-purple-100 text-purple-800' },
  { value: 'Received', label: 'Received', color: 'bg-green-100 text-green-800' },
  { value: 'Completed', label: 'Completed', color: 'bg-green-200 text-green-900' },
  { value: 'Cancelled', label: 'Cancelled', color: 'bg-red-100 text-red-800' }
];

export function TransferDialog({ open, onOpenChange, transfer, mode, initialTab = 'details', warehouses, onSuccess, currencyCode }: TransferDialogProps) {
  const { toast } = useToast();
  const { user, hasPermission } = useAuth();
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState('details');
  const [itemsFullPage, setItemsFullPage] = useState(false);
  const [transferDetail, setTransferDetail] = useState<InventoryTransferDetailDto | null>(null);
  const [warehouseInventoryItems, setWarehouseInventoryItems] = useState<WarehouseInventoryItemDto[]>([]);
  const [itemTrackingRequirements, setItemTrackingRequirements] = useState<InventoryTrackingRequirements | null>(null);
  const [itemTrackingError, setItemTrackingError] = useState<string | null>(null);
  const [itemTrackingAttempt, setItemTrackingAttempt] = useState(0);
  const [loadingItems, setLoadingItems] = useState(false);
  const [sourceLocations, setSourceLocations] = useState<WarehouseLocationDto[]>([]);
  const [destinationLocations, setDestinationLocations] = useState<WarehouseLocationDto[]>([]);
  const [itemSearchTerm, setItemSearchTerm] = useState('');
  const [showAddItem, setShowAddItem] = useState(false);
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [controlBusy, setControlBusy] = useState(false);
  const [controlComment, setControlComment] = useState('');
  const [resolutionCodes, setResolutionCodes] = useState<Record<string, string>>({});
  const [resolutionCode, setResolutionCode] = useState('CONFIRMED_LOSS');
  const [selectedDiscrepancyIds, setSelectedDiscrepancyIds] = useState<string[]>([]);
  const [controlEvidence, setControlEvidence] = useState<InventoryTransferEvidenceRequest[]>([]);
  const [dmsRecords, setDmsRecords] = useState<CentralDocumentRecord[]>([]);
  const [showResolveConfirmation, setShowResolveConfirmation] = useState(false);
  const controlKeyRef = useRef<{ fingerprint: string; key: string } | null>(null);
  const trackingExceptions = useAvailableInventoryTrackingExceptions(open && mode !== 'create' && Boolean(transfer?.id));
  
  const [formData, setFormData] = useState<FormData>({
    sourceWarehouseId: '',
    destinationWarehouseId: '',
    requiredDate: '',
    notes: ''
  });

  const [itemFormData, setItemFormData] = useState<ItemFormData>({
    inventoryItemId: '',
    requestedQuantity: 1,
    sourceLocationId: '',
    destinationLocationId: '',
    lotNumber: '',
    batchNumber: '',
    serialNumber: '',
    manufactureDate: '',
    expiryDate: '',
    inventoryTrackingExceptionId: '',
    notes: ''
  });

  const isEditable = hasPermission('procurement.inventory.transfer') && mode !== 'view' && (mode === 'create' || transferDetail?.status === 'Draft');
  const workflowPolicy = useWorkflowSummary({
    entityType: 'InventoryTransfer', entityId: transfer?.id,
    loadWorkflowSummary: open && mode !== 'create',
  });
  const workflowPolicyProps = {
    entityType: 'InventoryTransfer', entityId: transfer?.id,
    workflowSummary: workflowPolicy.summary, workflowSummaryLoading: workflowPolicy.loading,
    workflowSummaryError: workflowPolicy.error, loadWorkflowSummary: false,
  };
  useEffect(() => {
    if (!workflowPolicy.visibility.showTab && activeTab === 'approvals') setActiveTab('details');
  }, [workflowPolicy.visibility.showTab, activeTab]);

  // Fetch transfer details when editing/viewing
  useEffect(() => {
    setItemsFullPage(false);
    if (open && transfer?.id && mode !== 'create') {
      setActiveTab(initialTab);
      loadTransferDetails();
    } else if (open && mode === 'create') {
      resetForm();
    }
  }, [open, transfer?.id, mode, initialTab]);

  // Load inventory items when source warehouse changes
  useEffect(() => {
    if (formData.sourceWarehouseId && isEditable) {
      loadWarehouseInventoryItems(formData.sourceWarehouseId);
      loadSourceLocations(formData.sourceWarehouseId);
    } else {
      setWarehouseInventoryItems([]);
      setSourceLocations([]);
    }
  }, [formData.sourceWarehouseId, isEditable]);

  // Load destination locations when destination warehouse changes
  useEffect(() => {
    if (formData.destinationWarehouseId && isEditable) {
      loadDestinationLocations(formData.destinationWarehouseId);
    } else {
      setDestinationLocations([]);
    }
  }, [formData.destinationWarehouseId, isEditable]);

  const loadTransferDetails = async () => {
    if (!transfer?.id) return;
    try {
      setLoading(true);
      const [detail, resolutions, records] = await Promise.all([
        inventoryManagementService.getInventoryTransferById(transfer.id),
        inventoryManagementService.getTransferDiscrepancyResolutions(),
        documentManagementService.getRecords(),
      ]);
      setTransferDetail(detail);
      setResolutionCodes(resolutions);
      setDmsRecords(records.filter((record) => record.lifecycleStatus === 'Active' && record.versionStatus === 'Published' && Boolean(record.currentVersion)));
      setSelectedDiscrepancyIds(detail.discrepancies.filter((item) => item.status === 'Open').map((item) => item.id));
      setFormData({
        sourceWarehouseId: detail.sourceWarehouseId,
        destinationWarehouseId: detail.destinationWarehouseId,
        requiredDate: detail.expectedDeliveryDate ? detail.expectedDeliveryDate.split('T')[0] : '',
        notes: detail.notes || ''
      });
    } catch (err) {
      console.error('Error loading transfer details:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadWarehouseInventoryItems = async (warehouseId: string) => {
    if (!warehouseId) {
      setWarehouseInventoryItems([]);
      return;
    }
    try {
      setLoadingItems(true);
      // Load items from the source warehouse - endpoint returns items WITH stock
      const items = await inventoryManagementService.getWarehouseInventoryItems(warehouseId);
      // Filter to only show items with available stock > 0
      const itemsWithStock = items.filter((item) => item.availableStock > 0);
      setWarehouseInventoryItems(itemsWithStock);
    } catch (err) {
      console.error('Error loading warehouse inventory items:', err);
      setWarehouseInventoryItems([]);
    } finally {
      setLoadingItems(false);
    }
  };

  const resetForm = () => {
    setFormData({
      sourceWarehouseId: '',
      destinationWarehouseId: '',
      requiredDate: '',
      notes: ''
    });
    setTransferDetail(null);
    setActiveTab('details');
    setControlComment('');
    setControlEvidence([]);
    setSelectedDiscrepancyIds([]);
    setShowResolveConfirmation(false);
    controlKeyRef.current = null;
    setWarehouseInventoryItems([]);
    resetItemForm();
  };

  const resetItemForm = () => {
    setItemFormData({
      inventoryItemId: '',
      requestedQuantity: 1,
      sourceLocationId: '',
      destinationLocationId: '',
      lotNumber: '',
      batchNumber: '',
      serialNumber: '',
      manufactureDate: '',
      expiryDate: '',
      inventoryTrackingExceptionId: '',
      notes: ''
    });
    setShowAddItem(false);
    setEditingItemId(null);
  };

  const loadSourceLocations = async (warehouseId: string) => {
    try {
      const locations = await inventoryManagementService.getWarehouseLocations(warehouseId);
      setSourceLocations((locations || []).filter(l => l.isActive));
    } catch (err) {
      console.error('Error loading source locations:', err);
      setSourceLocations([]);
    }
  };

  const loadDestinationLocations = async (warehouseId: string) => {
    try {
      const locations = await inventoryManagementService.getWarehouseLocations(warehouseId);
      setDestinationLocations((locations || []).filter(l => l.isActive));
    } catch (err) {
      console.error('Error loading destination locations:', err);
      setDestinationLocations([]);
    }
  };

  const getStatusBadge = (status: string, approvalRequired?: boolean) => {
    const s = TransferStatuses.find(st => st.value === status);
    return <Badge className={s?.color || 'bg-gray-100'}>{getInventoryTransferStatusLabel(status, approvalRequired)}</Badge>;
  };

  const transferSubtotal = transferDetail?.items?.reduce((sum, item) => {
    const lineCost = item.totalCost ?? ((item.unitCost || 0) * (item.requestedQuantity || 0));
    return sum + lineCost;
  }, 0) ?? 0;
  const transferShippingCost = transferDetail?.shippingCost || 0;
  const transferMiscCost = transferDetail?.miscellaneousCost || 0;
  const transferAdditionalCost = transferDetail?.totalAdditionalCost ?? (transferShippingCost + transferMiscCost);
  const transferGrandTotal = transferSubtotal + transferAdditionalCost;

  const selectedItem = warehouseInventoryItems.find(i => i.inventoryItemId === itemFormData.inventoryItemId);
  const selectedTracking = itemTrackingRequirements?.inventoryItemId === itemFormData.inventoryItemId
    ? itemTrackingRequirements : null;

  useEffect(() => {
    let current = true;
    setItemTrackingRequirements(null);
    setItemTrackingError(null);
    if (!open || !showAddItem || !itemFormData.inventoryItemId) return;
    // The warehouse projection has no tracking flags. Use the central profile,
    // which also includes inherited category requirements, for this selected item.
    inventoryTrackingControlService.getRequirements(itemFormData.inventoryItemId)
      .then(requirements => { if (current) setItemTrackingRequirements(requirements); })
      .catch(error => { if (current) setItemTrackingError(getInventoryTransferProblemMessage(error, 'Unable to load item tracking requirements.')); });
    return () => { current = false; };
  }, [open, showAddItem, itemFormData.inventoryItemId, itemTrackingAttempt]);

  // Filter warehouse inventory items by search term
  const filteredInventoryItems = warehouseInventoryItems.filter(item =>
    (item.itemName ?? '').toLowerCase().includes(itemSearchTerm.toLowerCase()) ||
    (item.itemCode ?? '').toLowerCase().includes(itemSearchTerm.toLowerCase())
  );

  const handleSaveTransfer = async () => {
    try {
      setSaving(true);
      if (mode === 'create') {
        await inventoryManagementService.createInventoryTransfer({
          sourceWarehouseId: formData.sourceWarehouseId,
          destinationWarehouseId: formData.destinationWarehouseId,
          expectedDeliveryDate: formData.requiredDate || undefined,
          notes: formData.notes || undefined,
          items: []
        });
      } else if (mode === 'edit' && transfer?.id) {
        await inventoryManagementService.updateInventoryTransfer(transfer.id, {
          sourceWarehouseId: formData.sourceWarehouseId,
          destinationWarehouseId: formData.destinationWarehouseId,
          requiredDate: formData.requiredDate || undefined,
          notes: formData.notes || undefined
        });
      }
      onSuccess();
      onOpenChange(false);
    } catch (err) {
      console.error('Error saving transfer:', err);
      toast({
        title: 'Error',
        description: getInventoryTransferProblemMessage(err, 'Failed to save transfer'),
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const handleAddItem = async () => {
    if (!transfer?.id || !itemFormData.inventoryItemId || itemFormData.requestedQuantity <= 0) return;
    try {
      setSaving(true);

      const isInterBin = !!formData.sourceWarehouseId &&
        !!formData.destinationWarehouseId &&
        formData.sourceWarehouseId === formData.destinationWarehouseId;

      if (isInterBin) {
        if (!itemFormData.sourceLocationId || !itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'For inter-bin transfers, please select both a source bin and a destination bin.', variant: 'destructive' });
          return;
        }
        if (itemFormData.sourceLocationId === itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'Source bin and destination bin must be different.', variant: 'destructive' });
          return;
        }
      }

      const dto: AddTransferItemDto = {
        inventoryItemId: itemFormData.inventoryItemId,
        requestedQuantity: itemFormData.requestedQuantity,
        sourceLocationId: itemFormData.sourceLocationId || undefined,
        destinationLocationId: itemFormData.destinationLocationId || undefined,
        lotNumber: itemFormData.lotNumber || undefined,
        batchNumber: itemFormData.batchNumber || undefined,
        serialNumber: itemFormData.serialNumber || undefined,
        manufactureDate: itemFormData.manufactureDate || undefined,
        expiryDate: itemFormData.expiryDate || undefined,
        inventoryTrackingExceptionId: itemFormData.inventoryTrackingExceptionId || undefined,
        notes: itemFormData.notes || undefined
      };
      await inventoryManagementService.addTransferItem(transfer.id, dto);
      await loadTransferDetails();
      resetItemForm();
    } catch (err: any) {
      console.error('Error adding item:', err);
      toast({
        title: 'Error',
        description: getInventoryTransferProblemMessage(err, 'Failed to add item'),
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const handleUpdateItem = async (itemId: string) => {
    if (!transfer?.id) return;
    try {
      setSaving(true);

      const isInterBin = !!formData.sourceWarehouseId &&
        !!formData.destinationWarehouseId &&
        formData.sourceWarehouseId === formData.destinationWarehouseId;

      if (isInterBin) {
        if (!itemFormData.sourceLocationId || !itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'For inter-bin transfers, please select both a source bin and a destination bin.', variant: 'destructive' });
          return;
        }
        if (itemFormData.sourceLocationId === itemFormData.destinationLocationId) {
          toast({ title: 'Validation', description: 'Source bin and destination bin must be different.', variant: 'destructive' });
          return;
        }
      }

      const dto: UpdateTransferItemDto = {
        requestedQuantity: itemFormData.requestedQuantity,
        sourceLocationId: itemFormData.sourceLocationId || undefined,
        destinationLocationId: itemFormData.destinationLocationId || undefined,
        lotNumber: itemFormData.lotNumber || undefined,
        batchNumber: itemFormData.batchNumber || undefined,
        serialNumber: itemFormData.serialNumber || undefined,
        manufactureDate: itemFormData.manufactureDate || undefined,
        expiryDate: itemFormData.expiryDate || undefined,
        inventoryTrackingExceptionId: itemFormData.inventoryTrackingExceptionId || undefined,
        notes: itemFormData.notes || undefined
      };
      await inventoryManagementService.updateTransferItem(transfer.id, itemId, dto);
      await loadTransferDetails();
      resetItemForm();
    } catch (err: any) {
      console.error('Error updating item:', err);
      toast({
        title: 'Error',
        description: getInventoryTransferProblemMessage(err, 'Failed to update item'),
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const handleRemoveItem = async (itemId: string) => {
    if (!transfer?.id) return;
    try {
      setSaving(true);
      await inventoryManagementService.removeTransferItem(transfer.id, itemId);
      await loadTransferDetails();
      toast({
        title: 'Success',
        description: 'Item removed from transfer',
      });
    } catch (err: any) {
      console.error('Error removing item:', err);
      toast({
        title: 'Error',
        description: getInventoryTransferProblemMessage(err, 'Failed to remove item'),
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const startEditItem = (item: InventoryTransferItemDto) => {
    setItemFormData({
      inventoryItemId: item.inventoryItemId,
      requestedQuantity: item.requestedQuantity,
      sourceLocationId: item.sourceLocationId || '',
      destinationLocationId: item.destinationLocationId || '',
      lotNumber: item.lotNumber || '',
      batchNumber: item.batchNumber || '',
      serialNumber: item.serialNumber || '',
      manufactureDate: item.manufactureDate?.slice(0, 10) || '',
      expiryDate: item.expiryDate?.slice(0, 10) || '',
      inventoryTrackingExceptionId: item.inventoryTrackingExceptionId || '',
      notes: item.notes || ''
    });
    setEditingItemId(item.id);
    setShowAddItem(true);
  };

  const controlKeyFor = (kind: string, payload: unknown) => {
    const fingerprint = `${kind}:${JSON.stringify(payload)}`;
    if (controlKeyRef.current?.fingerprint !== fingerprint) {
      controlKeyRef.current = { fingerprint, key: crypto.randomUUID() };
    }
    return controlKeyRef.current.key;
  };

  const addControlEvidence = async (recordId: string) => {
    try {
      const detail = await documentManagementService.getRecord(recordId);
      const version = detail?.versions.find((item) => item.versionNumber === detail.record.currentVersion && item.status === 'Published' && item.fileUploadRecordId);
      if (!detail || !version) throw new Error('Select a central-DMS record with a current published repository version.');
      setControlEvidence((items) => items.some((item) => item.centralDocumentVersionId === version.id) ? items : [...items, {
        centralDocumentVersionId: version.id,
        evidenceReference: `${detail.record.documentReference} / ${version.versionNumber}`,
      }]);
    } catch (error) {
      toast({ title: 'Evidence unavailable', description: error instanceof Error ? error.message : 'Unable to link central-DMS evidence.', variant: 'destructive' });
    }
  };

  const resolveDiscrepancies = async (): Promise<boolean> => {
    if (!transferDetail || selectedDiscrepancyIds.length === 0 || !controlComment.trim() || controlEvidence.length === 0) {
      toast({ title: 'Resolution evidence required', description: 'Select open discrepancies and provide resolution notes plus current published central-DMS evidence.', variant: 'destructive' });
      return false;
    }
    const payload = { selectedDiscrepancyIds, resolutionCode, controlComment, controlEvidence };
    const idempotencyKey = controlKeyFor('resolve', payload);
    try {
      setControlBusy(true);
      await inventoryManagementService.resolveTransferDiscrepancies(transferDetail.id, {
        discrepancyIds: selectedDiscrepancyIds,
        resolutionCode,
        resolutionNotes: controlComment.trim(),
        evidence: controlEvidence,
        rowVersion: transferDetail.rowVersion,
        idempotencyKey,
        correlationId: `transfer-resolution:${transferDetail.id}:${idempotencyKey}`,
        comment: controlComment.trim(),
      });
      toast({ title: 'Discrepancies resolved' });
      setControlComment('');
      setControlEvidence([]);
      controlKeyRef.current = null;
      await loadTransferDetails();
      onSuccess();
      return true;
    } catch (error: any) {
      toast({
        title: 'Resolution blocked',
        description: getInventoryTransferProblemMessage(error, 'The controlled resolution failed.'),
        variant: 'destructive',
      });
      return false;
    } finally {
      setControlBusy(false);
    }
  };

  const showApprovalsTab = !!transferDetail && transferDetail.approvalRequired !== false && mode !== 'create' && workflowPolicy.visibility.showTab;
  const showControlsTab = !!transferDetail && mode !== 'create';
  const hasTransferPermission = hasPermission('procurement.inventory.transfer');
  const resolveCapability = transferDetail
    ? getInventoryTransferControlCapability({
        kind: 'resolve',
        status: transferDetail.status,
        hasOpenDiscrepancy: transferDetail.hasOpenDiscrepancy,
        hasTransferPermission,
        approvalRequired: transferDetail.approvalRequired,
        currentUserId: user?.id,
        actions: transferDetail.actions,
      })
    : { allowed: false, reason: 'Transfer details are unavailable.' };

  return (
    <>
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={`flex max-w-[calc(100vw-32px)] min-w-0 flex-col overflow-hidden ${itemsFullPage && activeTab === 'items' ? 'h-[calc(100dvh-32px)] w-[calc(100vw-32px)]' : 'h-[85vh] max-h-[900px] w-[800px]'}`}>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {mode === 'create' ? 'Create Inventory Transfer' : `Transfer ${transferDetail?.transferNumber || ''}`}
            {transferDetail && getStatusBadge(transferDetail.status, transferDetail.approvalRequired)}
          </DialogTitle>
          <DialogDescription className="sr-only">
            {mode === 'create' ? 'Create a new stock transfer (inter-warehouse or inter-bin)' :
             mode === 'edit' ? 'Edit transfer details and manage items' : 'View transfer details'}
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="py-8 text-center text-muted-foreground">Loading...</div>
        ) : (
          <Tabs value={activeTab} onValueChange={(tab) => { setActiveTab(tab); setItemsFullPage(false); }} className="flex min-h-0 min-w-0 flex-1 flex-col">
            <TabsList className={`grid w-full ${showApprovalsTab && showControlsTab ? 'grid-cols-4' : showApprovalsTab || showControlsTab ? 'grid-cols-3' : 'grid-cols-2'}`}>
              <TabsTrigger value="details">Transfer Details</TabsTrigger>
              <TabsTrigger value="items" disabled={mode === 'create' && !transfer?.id}>
                Items ({transferDetail?.items?.length || 0})
              </TabsTrigger>
              {showApprovalsTab && (
                <WorkflowTabTrigger value="approvals" {...workflowPolicyProps} />
              )}
              {showControlsTab && <TabsTrigger value="controls">History</TabsTrigger>}
            </TabsList>
            <div className="min-h-0 min-w-0 flex-1 overflow-auto pr-1">
            {/* Details Tab */}
            <TabsContent value="details" className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Source Warehouse *</Label>
                  <Select
                    value={formData.sourceWarehouseId}
                    onValueChange={(v) => setFormData({...formData, sourceWarehouseId: v})}
                    disabled={!isEditable}
                  >
                    <SelectTrigger><SelectValue placeholder="Select source warehouse" /></SelectTrigger>
                    <SelectContent>
                      {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Destination Warehouse *</Label>
                  <Select
                    value={formData.destinationWarehouseId}
                    onValueChange={(v) => setFormData({...formData, destinationWarehouseId: v})}
                    disabled={!isEditable}
                  >
                    <SelectTrigger><SelectValue placeholder="Select destination warehouse" /></SelectTrigger>
                    <SelectContent>
                      {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Required Date</Label>
                  <Input
                    type="date"
                    value={formData.requiredDate}
                    onChange={(e) => setFormData({...formData, requiredDate: e.target.value})}
                    disabled={!isEditable}
                  />
                </div>
                {transferDetail && (
                  <div className="space-y-2">
                    <Label>Request Date</Label>
                    <Input
                      type="text"
                      value={transferDetail.requestedDate ? format(new Date(transferDetail.requestedDate), 'MMM dd, yyyy') : '-'}
                      disabled
                    />
                  </div>
                )}
              </div>

              <div className="space-y-2">
                <Label>Notes</Label>
                <Textarea
                  value={formData.notes}
                  onChange={(e) => setFormData({...formData, notes: e.target.value})}
                  disabled={!isEditable}
                  rows={3}
                  placeholder="Add a note"
                />
              </div>

              {/* Transfer Summary */}
              {transferDetail && (
                <div className="grid grid-cols-1 gap-4">
                  <Card>
                    <CardHeader className="pb-2">
                      <CardTitle className="text-sm">Quantity Summary</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="grid grid-cols-2 gap-4 text-sm">
                        <div>
                          <span className="text-muted-foreground">Total Items:</span>
                          <span className="ml-2 font-medium">{transferDetail.items?.length || 0}</span>
                        </div>
                        <div>
                          <span className="text-muted-foreground">Total Quantity:</span>
                          <span className="ml-2 font-medium">
                            {transferDetail.items?.reduce((sum, i) => sum + i.requestedQuantity, 0) || 0}
                          </span>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              )}
            </TabsContent>

            {/* Items Tab - will be added next */}
            <TabsContent value="items" className="space-y-4">
              {renderItemsTab()}
            </TabsContent>

            {showControlsTab && transferDetail && (
              <TabsContent value="controls" className="space-y-4">
                <Card>
                  <CardHeader><CardTitle className="text-base">History</CardTitle></CardHeader>
                  <CardContent>
                    {transferDetail.actions.length === 0 ? <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No history yet.</div> : (
                      <div className="overflow-x-auto rounded-md border"><Table><TableHeader><TableRow><TableHead>#</TableHead><TableHead>Action</TableHead><TableHead>Actor</TableHead><TableHead>UTC time</TableHead><TableHead>Comment</TableHead><TableHead>Correlation</TableHead></TableRow></TableHeader><TableBody>{transferDetail.actions.map((action) => <TableRow key={action.id}><TableCell>{action.sequence}</TableCell><TableCell><Badge variant="outline">{action.actionType}</Badge></TableCell><TableCell className="font-mono text-xs">{action.actorUserId}</TableCell><TableCell>{format(new Date(action.occurredAtUtc), 'MMM dd, yyyy HH:mm')}</TableCell><TableCell>{action.comment || '—'}</TableCell><TableCell className="max-w-52 truncate font-mono text-xs" title={action.correlationId}>{action.correlationId}</TableCell></TableRow>)}</TableBody></Table></div>
                    )}
                  </CardContent>
                </Card>

                {transferDetail.discrepancies.length > 0 && <Card>
                  <CardHeader><CardTitle className="text-base">Discrepancies</CardTitle></CardHeader>
                  <CardContent className="space-y-4">
                    {transferDetail.discrepancies.length === 0 ? <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">No transfer discrepancies.</div> : transferDetail.discrepancies.map((item) => (
                      <div key={item.id} className="flex items-start gap-3 rounded-md border p-3 text-sm">
                        <Checkbox checked={selectedDiscrepancyIds.includes(item.id)} disabled={item.status !== 'Open'} onCheckedChange={(checked) => setSelectedDiscrepancyIds((ids) => checked ? [...new Set([...ids, item.id])] : ids.filter((id) => id !== item.id))} aria-label={`Select discrepancy ${item.id}`} />
                        <div className="flex-1"><div className="flex flex-wrap items-center gap-2"><Badge variant={item.status === 'Open' ? 'destructive' : 'secondary'}>{item.status}</Badge><span className="font-medium">{item.reasonCode}</span><span>Damaged {item.damagedQuantity.toFixed(2)} · Shortage {item.shortageQuantity.toFixed(2)}</span></div><p className="mt-1 text-muted-foreground">{item.reason}</p>{item.resolutionCode && <p className="mt-1">Resolution: {item.resolutionCode} · {item.resolutionNotes}</p>}<div className="mt-2 flex flex-wrap gap-2">{item.evidence.map((evidence) => <Badge key={evidence.id} variant="outline">{evidence.evidenceReference}</Badge>)}</div></div>
                      </div>
                    ))}

                    {transferDetail.status === 'Received' && transferDetail.hasOpenDiscrepancy && resolveCapability.allowed && (
                      <div className="space-y-3 rounded-md border border-amber-200 bg-amber-50/50 p-4">
                        <div className="grid gap-3 md:grid-cols-2"><div className="space-y-1"><Label>Resolution outcome *</Label><Select value={resolutionCode} onValueChange={setResolutionCode}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{Object.entries(resolutionCodes).map(([code, label]) => <SelectItem key={code} value={code}>{label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Current published Central DMS evidence *</Label><Select onValueChange={(value) => void addControlEvidence(value)}><SelectTrigger><SelectValue placeholder="Link protected resolution evidence" /></SelectTrigger><SelectContent>{dmsRecords.map((record) => <SelectItem key={record.id} value={record.id}>{record.documentReference} · {record.title}</SelectItem>)}</SelectContent></Select></div></div>
                        <div className="flex flex-wrap gap-2">{controlEvidence.map((evidence) => <Badge key={evidence.centralDocumentVersionId} variant="outline" className="gap-2">{evidence.evidenceReference}<button type="button" aria-label={`Remove ${evidence.evidenceReference}`} onClick={() => setControlEvidence((values) => values.filter((value) => value.centralDocumentVersionId !== evidence.centralDocumentVersionId))}>×</button></Badge>)}</div>
                        <div className="space-y-1"><Label>Resolution notes *</Label><Textarea value={controlComment} onChange={(event) => setControlComment(event.target.value)} placeholder="State the independently verified disposition and supporting basis" /></div>
                        <Button disabled={controlBusy || selectedDiscrepancyIds.length === 0 || !controlComment.trim() || controlEvidence.length === 0} onClick={() => setShowResolveConfirmation(true)}>Resolve selected discrepancies</Button>
                      </div>
                    )}

                    {transferDetail.status === 'Received' && transferDetail.hasOpenDiscrepancy && !resolveCapability.allowed && (
                      <div className="rounded-md border border-amber-200 bg-amber-50/50 p-4 text-sm text-amber-900">{resolveCapability.reason}</div>
                    )}

                  </CardContent>
                </Card>}
              </TabsContent>
            )}

            {showApprovalsTab && transferDetail && (
              <WorkflowTabContent
                {...workflowPolicyProps}
                value="approvals"
                entityType="InventoryTransfer"
                entityId={transferDetail.id}
                entityLabel="Inventory Transfer"
                entityNumber={transferDetail.transferNumber}
                status={transferDetail.status}
                currentStepName={transferDetail.currentWorkflowStepName}
                canSubmit={hasTransferPermission && transferDetail.status === 'Draft'}
                canApproveReject={hasTransferPermission && transferDetail.status === 'Submitted'}
                onSubmit={async () => {
                  await inventoryManagementService.submitTransferForApproval(transferDetail.id);
                }}
                onApprove={async (comments) => {
                  await inventoryManagementService.approveTransfer(transferDetail.id, comments || undefined);
                }}
                onReject={async (comments) => {
                  await inventoryManagementService.rejectTransfer(transferDetail.id, comments);
                }}
                onAfterAction={async () => {
                  await loadTransferDetails();
                  await workflowPolicy.refresh();
                }}
              />
            )}
            </div>
          </Tabs>
        )}

        <DialogFooter className="flex shrink-0 flex-wrap justify-between gap-2 border-t pt-3">
          <div className="flex gap-2">
            {transferDetail && transferDetail.approvalRequired !== false && transferDetail.items?.length > 0 && (
              <WorkflowApprovalActions
                {...workflowPolicyProps}
                entityType="InventoryTransfer"
                entityId={transferDetail.id}
                entityLabel="Inventory Transfer"
                entityNumber={transferDetail.transferNumber}
                status={transferDetail.status}
                currentStepName={transferDetail.currentWorkflowStepName}
                showStepBadge={transferDetail.status === 'Submitted' && !!transferDetail.currentWorkflowStepName}
                canSubmit={hasTransferPermission && transferDetail.status === 'Draft'}
                canApproveReject={hasTransferPermission && transferDetail.status === 'Submitted'}
                onSubmit={async () => {
                  try {
                    await inventoryManagementService.submitTransferForApproval(transferDetail.id);
                  } catch (err: any) {
                    throw new Error(getInventoryTransferProblemMessage(err, 'Failed to submit transfer'));
                  }
                }}
                onApprove={async (comments) => {
                  try {
                    await inventoryManagementService.approveTransfer(transferDetail.id, comments || undefined);
                  } catch (err: any) {
                    const msg =
                      err?.response?.data?.error ||
                      err?.response?.data ||
                      err?.message ||
                      'Failed to approve transfer';
                    throw new Error(typeof msg === 'string' ? msg : 'Failed to approve transfer');
                  }
                }}
                onReject={async (comments) => {
                  try {
                    await inventoryManagementService.rejectTransfer(transferDetail.id, comments);
                  } catch (err: any) {
                    const msg =
                      err?.response?.data?.error ||
                      err?.response?.data ||
                      err?.message ||
                      'Failed to reject transfer';
                    throw new Error(typeof msg === 'string' ? msg : 'Failed to reject transfer');
                  }
                }}
                onAfterAction={async () => {
                  await loadTransferDetails();
                  onSuccess();
                  await workflowPolicy.refresh();
                }}
                onOpenWorkflows={() => router.push('/administration/workflow')}
                size="sm"
              />
            )}
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {mode === 'view' ? 'Close' : 'Cancel'}
            </Button>
            {isEditable && (
              <Button onClick={handleSaveTransfer} disabled={saving || !formData.sourceWarehouseId || !formData.destinationWarehouseId}>
                {saving ? 'Saving...' : mode === 'create' ? 'Create Transfer' : 'Save Changes'}
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
    <ConfirmationDialog
      open={showResolveConfirmation}
      onOpenChange={setShowResolveConfirmation}
      title="Resolve selected transfer discrepancies"
      description={`Resolve ${selectedDiscrepancyIds.length} selected discrepancy(s) as ${resolutionCodes[resolutionCode] || resolutionCode}?`}
      confirmText="Confirm resolution"
      cancelText="Review details"
      onConfirm={resolveDiscrepancies}
      isLoading={controlBusy}
      confirmDisabled={!resolveCapability.allowed || selectedDiscrepancyIds.length === 0 || !controlComment.trim() || controlEvidence.length === 0}
      maxWidth="560px"
    />
    </>
  );

  function renderItemsTab() {
    return (
      <>
        <div className="flex justify-end">
          <Button type="button" variant="outline" size="sm" aria-pressed={itemsFullPage} onClick={() => setItemsFullPage(value => !value)}>
            {itemsFullPage ? <Minimize2 className="mr-2 h-4 w-4" /> : <Maximize2 className="mr-2 h-4 w-4" />}
            {itemsFullPage ? 'Restore' : 'Full page'}
          </Button>
        </div>
        {/* Add Item Section */}
        {isEditable && (
          <Card>
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-sm">Add Items to Transfer</CardTitle>
                {!showAddItem && (
                  <Button size="sm" onClick={() => setShowAddItem(true)}>
                    <Plus className="h-4 w-4 mr-1" /> Add Item
                  </Button>
                )}
              </div>
            </CardHeader>
            {showAddItem && (
              <CardContent className="space-y-4">
                {/* Warning if no source warehouse selected */}
                {!formData.sourceWarehouseId && (
                  <div className="flex items-center gap-2 p-3 rounded-md bg-yellow-50 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400">
                    <AlertCircle className="h-4 w-4" />
                    <span className="text-sm">Please select a source warehouse first to see available items.</span>
                  </div>
                )}

                {/* Item Search */}
                {formData.sourceWarehouseId && (
                <>
                <div className="space-y-2">
                  <Label>Search & Select Item from Source Warehouse</Label>
                  <div className="relative">
                    <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                    <Input
                      placeholder="Search by item code or name..."
                      className="pl-8"
                      value={itemSearchTerm}
                      onChange={(e) => setItemSearchTerm(e.target.value)}
                    />
                  </div>
                  <Select
                    value={itemFormData.inventoryItemId}
                    onValueChange={(v) => setItemFormData({...itemFormData, inventoryItemId: v})}
                    disabled={loadingItems}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={loadingItems ? "Loading items..." : (warehouseInventoryItems.length === 0 ? "No items with stock available" : "Select an inventory item")} />
                    </SelectTrigger>
                    <SelectContent className="max-h-60">
                      {filteredInventoryItems.slice(0, 50).map(item => (
                        <SelectItem key={item.inventoryItemId} value={item.inventoryItemId}>
                          <div className="flex items-center gap-2">
                            <span className="font-mono text-xs">{item.itemCode}</span>
                            <span>{item.itemName}</span>
                            <span className="text-muted-foreground text-xs">({item.availableStock} avail)</span>
                          </div>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Selected Item Info */}
                {selectedItem && (
                  <Card className="bg-muted/50">
                    <CardContent className="pt-4">
                      <div className="grid grid-cols-4 gap-4 text-sm">
                        <div>
                          <span className="text-muted-foreground">Available in Source:</span>
                          <span className="ml-2 font-medium">{selectedItem.availableStock} {selectedItem.unitOfMeasure}</span>
                        </div>
                        <div className="flex items-center gap-1">
                          {selectedTracking?.requiresSerial && (
                            <Badge variant="outline" className="text-xs"><Barcode className="h-3 w-3 mr-1" />Serial Tracked</Badge>
                          )}
                          {selectedTracking?.requiresLot && (
                            <Badge variant="outline" className="text-xs"><Layers className="h-3 w-3 mr-1" />Lot Tracked</Badge>
                          )}
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                )}

                {/* Quantity & Tracking Fields */}
                {itemFormData.inventoryItemId && !selectedTracking && (
                  <div className="text-sm text-muted-foreground" role={itemTrackingError ? 'alert' : 'status'}>
                    {itemTrackingError || 'Loading tracking requirements…'}
                    {itemTrackingError && <Button type="button" variant="link" size="sm" onClick={() => setItemTrackingAttempt(value => value + 1)}>Retry</Button>}
                  </div>
                )}
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Requested Quantity *</Label>
                    <Input
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={itemFormData.requestedQuantity}
                      onChange={(e) => setItemFormData({...itemFormData, requestedQuantity: parseFloat(e.target.value) || 0})}
                    />
                  </div>
                  {selectedTracking?.requiresSerial && (
                    <div className="space-y-2">
                      <Label>Serial Number</Label>
                      <Input
                        value={itemFormData.serialNumber}
                        onChange={(e) => setItemFormData({...itemFormData, serialNumber: e.target.value})}
                        placeholder="Enter serial number"
                      />
                    </div>
                  )}
                  {selectedTracking?.requiresLot && (
                    <div className="space-y-2">
                      <Label>Lot Number</Label>
                      <Input
                        value={itemFormData.lotNumber}
                        onChange={(e) => setItemFormData({...itemFormData, lotNumber: e.target.value})}
                        placeholder="Enter lot number"
                      />
                    </div>
                  )}
                  {selectedTracking?.requiresBatch && (
                    <div className="space-y-2"><Label>Batch Number</Label><Input value={itemFormData.batchNumber} onChange={(e) => setItemFormData({...itemFormData, batchNumber: e.target.value})} placeholder="Enter batch number" /></div>
                  )}
                  {selectedTracking?.requiresManufactureDate && (
                    <div className="space-y-2"><Label>Manufacture Date</Label><Input type="date" value={itemFormData.manufactureDate} onChange={(e) => setItemFormData({...itemFormData, manufactureDate: e.target.value})} /></div>
                  )}
                  {selectedTracking?.requiresExpiryDate && (
                    <div className="space-y-2"><Label>Expiry Date</Label><Input type="date" value={itemFormData.expiryDate} onChange={(e) => setItemFormData({...itemFormData, expiryDate: e.target.value})} /></div>
                  )}
                  <div className="space-y-2 col-span-2">
                    <Label>Approved tracking exception</Label>
                    <InventoryTrackingExceptionSelect
                      value={itemFormData.inventoryTrackingExceptionId}
                      onValueChange={(inventoryTrackingExceptionId) => setItemFormData({
                        ...itemFormData,
                        inventoryTrackingExceptionId: inventoryTrackingExceptionId || '',
                      })}
                      exceptions={trackingExceptions.exceptions}
                      loading={trackingExceptions.loading}
                      error={trackingExceptions.error}
                      onRetry={trackingExceptions.refresh}
                      context={{
                        inventoryItemId: itemFormData.inventoryItemId,
                        warehouseId: formData.sourceWarehouseId,
                        locationId: itemFormData.sourceLocationId,
                        referenceId: transfer?.id,
                        lotNumber: itemFormData.lotNumber,
                        batchNumber: itemFormData.batchNumber,
                        serialNumber: itemFormData.serialNumber,
                      }}
                    />
                  </div>
                </div>

                {/* Bin/Location Selection */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Source Bin</Label>
                    <Select
                      value={itemFormData.sourceLocationId || '__none__'}
                      onValueChange={(v) => setItemFormData({ ...itemFormData, sourceLocationId: v === '__none__' ? '' : v })}
                      disabled={!formData.sourceWarehouseId}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select source bin (optional)" />
                      </SelectTrigger>
                      <SelectContent className="max-h-60">
                        <SelectItem value="__none__">Not specified</SelectItem>
                        {sourceLocations.map(loc => (
                          <SelectItem key={loc.id} value={loc.id}>
                            {loc.locationCode}{loc.name ? ` - ${loc.name}` : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <Label>Destination Bin</Label>
                    <Select
                      value={itemFormData.destinationLocationId || '__none__'}
                      onValueChange={(v) => setItemFormData({ ...itemFormData, destinationLocationId: v === '__none__' ? '' : v })}
                      disabled={!formData.destinationWarehouseId}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select destination bin (optional)" />
                      </SelectTrigger>
                      <SelectContent className="max-h-60">
                        <SelectItem value="__none__">Not specified</SelectItem>
                        {destinationLocations.map(loc => (
                          <SelectItem key={loc.id} value={loc.id}>
                            {loc.locationCode}{loc.name ? ` - ${loc.name}` : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Item Notes</Label>
                  <Input
                    value={itemFormData.notes}
                    onChange={(e) => setItemFormData({...itemFormData, notes: e.target.value})}
                    placeholder="Optional notes for this item"
                  />
                </div>

                <div className="flex justify-end gap-2">
                  <Button variant="outline" size="sm" onClick={resetItemForm}>Cancel</Button>
                  {editingItemId ? (
                    <Button size="sm" onClick={() => handleUpdateItem(editingItemId)} disabled={saving}>
                      {saving ? 'Saving...' : 'Update Item'}
                    </Button>
                  ) : (
                    <Button size="sm" onClick={handleAddItem} disabled={saving || !itemFormData.inventoryItemId || itemFormData.requestedQuantity <= 0}>
                      {saving ? 'Adding...' : 'Add Item'}
                    </Button>
                  )}
                </div>
                </>
                )}
              </CardContent>
            )}
          </Card>
        )}

        {/* Items Table */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Transfer Items</CardTitle>
          </CardHeader>
          <CardContent>
            {(!transferDetail?.items || transferDetail.items.length === 0) ? (
              <div className="py-8 text-center text-muted-foreground">
                <Package className="h-12 w-12 mx-auto mb-2 opacity-50" />
                <p>No items added yet</p>
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Item</TableHead>
                    <TableHead>From Bin</TableHead>
                    <TableHead>To Bin</TableHead>
                    <TableHead className="text-right">Qty Requested</TableHead>
                    <TableHead className="text-right">Qty Shipped</TableHead>
                    <TableHead className="text-right">Qty Received</TableHead>
                    <TableHead>Tracking</TableHead>
                    {isEditable && <TableHead></TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {transferDetail.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <div>
                          <div className="font-medium">{item.itemName || item.itemCode}</div>
                          <div className="text-xs text-muted-foreground">{item.itemCode}</div>
                        </div>
                      </TableCell>
                      <TableCell className="text-sm">{item.sourceLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                      <TableCell className="text-sm">{item.destinationLocationName || <span className="text-muted-foreground">-</span>}</TableCell>
                      <TableCell className="text-right">{item.requestedQuantity} {item.unitOfMeasure}</TableCell>
                      <TableCell className="text-right">{item.shippedQuantity}</TableCell>
                      <TableCell className="text-right">{item.receivedQuantity}</TableCell>
                      <TableCell>
                        <div className="text-xs">
                          {item.serialNumber && <div><Barcode className="h-3 w-3 inline mr-1" />SN: {item.serialNumber}</div>}
                          {item.lotNumber && <div><Layers className="h-3 w-3 inline mr-1" />Lot: {item.lotNumber}</div>}
                          {!item.serialNumber && !item.lotNumber && <span className="text-muted-foreground">-</span>}
                        </div>
                      </TableCell>
                      {isEditable && (
                        <TableCell>
                          <div className="flex gap-1">
                            <Button variant="ghost" size="icon" aria-label="Edit" title="Edit" onClick={() => startEditItem(item)}><Pencil className="h-4 w-4 text-blue-500" /></Button>
                            <Button variant="ghost" size="sm" className="text-red-600" onClick={() => handleRemoveItem(item.id)}>
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </>
    );
  }
}
