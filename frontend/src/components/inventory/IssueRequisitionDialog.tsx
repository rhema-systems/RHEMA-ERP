/* eslint-disable @typescript-eslint/no-non-null-assertion */
'use client';

import React, { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Package, AlertCircle, CheckCircle } from 'lucide-react';
import {
  inventoryRequisitionService,
  InventoryRequisitionDetailDto, InventoryRequisitionItemDto,
  IssueRequisitionDto, IssueRequisitionItemDto, RequisitionStatusMap
} from '@/services/inventoryRequisitionService';
import { useToast } from '@/hooks/use-toast';
import { format } from 'date-fns';

interface IssueRequisitionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  requisitionId: string | null;
  onSuccess: () => void;
}

interface IssueItemState {
  itemId: string;
  itemCode: string;
  itemName: string;
  requestedQuantity: number;
  approvedQuantity: number;
  previouslyIssued: number;
  remainingToIssue: number;
  issuingQuantity: number;
  unitOfMeasure: string;
  locationId?: string;
  locationName?: string;
}

const RequisitionStatuses = [
  { value: 1, label: 'Draft', color: 'bg-gray-100 text-gray-800' },
  { value: 2, label: 'Submitted', color: 'bg-yellow-100 text-yellow-800' },
  { value: 3, label: 'Approved', color: 'bg-blue-100 text-blue-800' },
  { value: 4, label: 'In Progress', color: 'bg-indigo-100 text-indigo-800' },
  { value: 5, label: 'Partially Issued', color: 'bg-purple-100 text-purple-800' },
  { value: 6, label: 'Issued', color: 'bg-green-100 text-green-800' },
  { value: 7, label: 'Completed', color: 'bg-green-200 text-green-900' },
  { value: 8, label: 'Cancelled', color: 'bg-red-100 text-red-800' },
  { value: 9, label: 'Rejected', color: 'bg-red-100 text-red-800' }
];

// Helper to normalize status to number (API may return string or number)
const normalizeStatus = (status: number | string | undefined): number => {
  if (status === undefined || status === null) return 0;
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    'Draft': 1, 'Submitted': 2, 'Approved': 3, 'InProgress': 4,
    'PartiallyIssued': 5, 'Issued': 6, 'Completed': 7, 'Cancelled': 8, 'Rejected': 9
  };
  return statusMap[status] || 0;
};

export function IssueRequisitionDialog({ open, onOpenChange, requisitionId, onSuccess }: IssueRequisitionDialogProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [issuing, setIssuing] = useState(false);
  const [requisition, setRequisition] = useState<InventoryRequisitionDetailDto | null>(null);
  const [issueItems, setIssueItems] = useState<IssueItemState[]>([]);
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (open && requisitionId) {
      loadRequisition();
    }
  }, [open, requisitionId]);

  const loadRequisition = async () => {
    if (!requisitionId) return;
    try {
      setLoading(true);
      const detail = await inventoryRequisitionService.getById(requisitionId);
      setRequisition(detail);
      // Initialize issue items from requisition items
      const items: IssueItemState[] = detail.items.map(item => ({
        itemId: item.id,
        itemCode: item.itemCode,
        itemName: item.itemName,
        requestedQuantity: item.requestedQuantity,
        approvedQuantity: item.approvedQuantity || item.requestedQuantity,
        previouslyIssued: item.issuedQuantity || 0,
        remainingToIssue: (item.approvedQuantity || item.requestedQuantity) - (item.issuedQuantity || 0),
        issuingQuantity: 0,
        unitOfMeasure: item.unitOfMeasure,
        locationId: item.locationId || detail.locationId,
        locationName: item.locationName || detail.locationName,
      }));
      setIssueItems(items);
      setNotes('');
    } catch (err) {
      console.error('Error loading requisition:', err);
      toast({ title: 'Error', description: 'Failed to load requisition', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  const handleQuantityChange = (itemId: string, quantity: number) => {
    setIssueItems(items => items.map(item => {
      if (item.itemId === itemId) {
        const maxQty = item.remainingToIssue;
        const validQty = Math.max(0, Math.min(quantity, maxQty));
        return { ...item, issuingQuantity: validQty };
      }
      return item;
    }));
  };

  const handleIssueAll = () => {
    setIssueItems(items => items.map(item => ({
      ...item,
      issuingQuantity: item.remainingToIssue
    })));
  };

  const handleIssue = async () => {
    const itemsToIssue = issueItems.filter(item => item.issuingQuantity > 0);
    if (itemsToIssue.length === 0) {
      toast({ title: 'Validation Error', description: 'Please enter quantities to issue', variant: 'destructive' });
      return;
    }
    try {
      setIssuing(true);
      const dto: IssueRequisitionDto = {
        items: itemsToIssue.map(item => ({
          itemId: item.itemId,
          issuedQuantity: item.issuingQuantity,
          locationId: item.locationId,
        })),
        notes: notes || undefined
      };
      if (!requisitionId) {
        throw new Error('Requisition ID is missing');
      }
      await inventoryRequisitionService.issue(requisitionId, dto);
      toast({ title: 'Success', description: 'Items issued successfully' });
      onSuccess();
      onOpenChange(false);
    } catch (err: unknown) {
      console.error('Error issuing items:', err);
      const errorMessage = err instanceof Error ? err.message : 'Failed to issue items';
      toast({ title: 'Error', description: errorMessage, variant: 'destructive' });
    } finally {
      setIssuing(false);
    }
  };

  const getStatusBadge = (status: number | string) => {
    const numStatus = normalizeStatus(status);
    const s = RequisitionStatuses.find(st => st.value === numStatus);
    return <Badge className={s?.color || 'bg-gray-100'}>{s?.label || RequisitionStatusMap[numStatus] || status}</Badge>;
  };

  const totalIssuing = issueItems.reduce((sum, item) => sum + item.issuingQuantity, 0);
  const hasItemsToIssue = issueItems.some(item => item.remainingToIssue > 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex flex-col gap-1">
            <DialogTitle>
              Issue Items
              {requisition && <span className="ml-2 text-muted-foreground">#{requisition.requisitionNumber}</span>}
            </DialogTitle>
            <div className="flex items-center gap-2">
              <span className="text-sm text-muted-foreground">
                Issue inventory items for this requisition
              </span>
              {requisition && getStatusBadge(requisition.status)}
            </div>
          </div>
        </DialogHeader>

        {loading ? (
          <div className="flex justify-center py-8"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div></div>
        ) : requisition ? (
          <div className="space-y-4">
            {/* Requisition Info */}
            <Card>
              <CardContent className="pt-4">
                <div className="grid grid-cols-3 gap-4 text-sm">
                  <div><span className="text-muted-foreground">Department:</span> <span className="font-medium">{requisition.departmentName}</span></div>
                  <div><span className="text-muted-foreground">Warehouse:</span> <span className="font-medium">{requisition.warehouseName}</span></div>
                  <div><span className="text-muted-foreground">Request Date:</span> <span className="font-medium">{requisition.requestDateFormatted || format(new Date(requisition.requestDate), 'dd/MM/yyyy')}</span></div>
                  <div><span className="text-muted-foreground">Location:</span> <span className="font-medium">{requisition.locationName || 'Warehouse level'}</span></div>
                </div>
              </CardContent>
            </Card>

            {/* Items Table */}
            <div className="flex justify-between items-center">
              <h4 className="font-medium">Items to Issue</h4>
              {hasItemsToIssue && <Button variant="outline" size="sm" onClick={handleIssueAll}>Issue All Remaining</Button>}
            </div>

            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Item</TableHead>
                  <TableHead className="text-center">Requested</TableHead>
                  <TableHead className="text-center">Approved</TableHead>
                  <TableHead className="text-center">Previously Issued</TableHead>
                  <TableHead className="text-center">Remaining</TableHead>
                  <TableHead className="text-center">Issue Qty</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>UoM</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {issueItems.map((item) => (
                  <TableRow key={item.itemId}>
                    <TableCell>
                      <div className="font-medium">{item.itemCode}</div>
                      <div className="text-sm text-muted-foreground">{item.itemName}</div>
                    </TableCell>
                    <TableCell className="text-center">{item.requestedQuantity}</TableCell>
                    <TableCell className="text-center">{item.approvedQuantity}</TableCell>
                    <TableCell className="text-center">
                      {item.previouslyIssued > 0 ? (
                        <Badge variant="outline" className="bg-green-50">{item.previouslyIssued}</Badge>
                      ) : '-'}
                    </TableCell>
                    <TableCell className="text-center">
                      {item.remainingToIssue > 0 ? (
                        <Badge variant="outline" className="bg-yellow-50">{item.remainingToIssue}</Badge>
                      ) : (
                        <Badge className="bg-green-100 text-green-800"><CheckCircle className="h-3 w-3 mr-1" />Complete</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-center">
                      {item.remainingToIssue > 0 ? (
                        <Input
                          type="number"
                          min="0"
                          max={item.remainingToIssue}
                          value={item.issuingQuantity}
                          onChange={(e) => handleQuantityChange(item.itemId, parseInt(e.target.value) || 0)}
                          className="w-20 text-center"
                        />
                      ) : '-'}
                    </TableCell>
                    <TableCell>{item.locationName || requisition.locationName || 'Warehouse level'}</TableCell>
                    <TableCell>{item.unitOfMeasure}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            {/* Notes */}
            <div className="space-y-2">
              <Label>Issue Notes</Label>
              <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Optional notes for this issue..." rows={2} />
            </div>

            {/* Summary */}
            {totalIssuing > 0 && (
              <Card className="bg-blue-50">
                <CardContent className="pt-4">
                  <div className="flex items-center gap-2">
                    <Package className="h-5 w-5 text-blue-600" />
                    <span className="font-medium">Total items to issue: {totalIssuing}</span>
                  </div>
                </CardContent>
              </Card>
            )}
          </div>
        ) : (
          <div className="text-center py-8 text-muted-foreground">No requisition data</div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={handleIssue} disabled={issuing || totalIssuing === 0}>
            {issuing ? 'Issuing...' : `Issue ${totalIssuing} Items`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

